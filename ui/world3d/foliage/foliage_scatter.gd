extends RefCounted
## One original tree prototype, flattened once into shared per-material meshes.
## Placement consumes only stable visual hashes; no tree creates a game entity
## or an individual scene node. The returned transforms are the proof boundary.

const Terrain = preload("res://ui/world3d/terrain/terrain_builder.gd")
const Buildings = preload("res://ui/world3d/city/building_renderer.gd")
const TREE_ID := "nature.tree.valley_01"
const DEFAULT_PROFILE := {
	"forest_candidates": 22,
	"center_density": 0.92,
	"edge_density": 0.40,
	"plain_candidates": 2,
	"plain_density": 0.26,
	"scatter_radius": 62.0,
	"minimum_spacing": 16.0,
	"scale_min": 0.80,
	"scale_max": 1.16,
	"platform_clearance": 13.0,
	"river_clearance": 66.0,
	"road_clearance": 11.0,
	"max_slope": 0.65,
}


static func build(snapshot: Dictionary) -> Dictionary:
	var root := Node3D.new()
	root.name = "InstancedValleyForest"
	var manifest: Dictionary = snapshot.get("asset_registry", {})
	if manifest.is_empty():
		manifest = Buildings.load_manifest()
	var entry := Buildings.asset_entry(manifest, TREE_ID)
	var scene := Buildings.load_scene(entry)
	if scene == null:
		return {"node": root, "transforms": [], "placements": [], "signature": "",
			"stats": {"trees": 0, "batches": 0, "missing_assets": [TREE_ID], "presentation_only": true}}
	var prototype: Node = scene.instantiate()
	var batches := _prototype_batches(prototype)
	prototype.free()
	var profile: Dictionary = DEFAULT_PROFILE.duplicate(true)
	profile.merge(snapshot.get("foliage_profile", {}), true)
	var placements := _placements(snapshot, profile)
	var transforms: Array[Transform3D] = []
	for placement in placements:
		var point: Vector2 = placement["legacy_position"]
		var ground_anchor := Buildings.world_position(point, float(placement["height_legacy"]))
		transforms.append(Buildings.grounded_asset_transform(entry, ground_anchor,
			float(placement["rotation"]), float(placement["scale"])))
	var triangles_per_tree := 0
	for batch_index in batches.size():
		var batch: Dictionary = batches[batch_index]
		var mesh: ArrayMesh = batch["mesh"]
		var multi := MultiMesh.new()
		multi.transform_format = MultiMesh.TRANSFORM_3D
		multi.mesh = mesh
		multi.instance_count = transforms.size()
		for index in transforms.size():
			multi.set_instance_transform(index, transforms[index])
		var instance := MultiMeshInstance3D.new()
		instance.name = "TreeBatch_%02d" % batch_index
		instance.multimesh = multi
		instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
		instance.set_meta("logical_asset_id", TREE_ID)
		root.add_child(instance)
		triangles_per_tree += int(batch["triangles"])
	return {"node": root, "transforms": transforms, "placements": placements,
		"signature": Buildings.placement_signature(placements),
		"stats": {"trees": placements.size(), "batches": batches.size(),
			"triangles_per_tree": triangles_per_tree, "tree_scene_nodes": 0,
			"visual_seed": int(snapshot.get("visual_seed", 0)), "profile": profile,
			"missing_assets": [], "presentation_only": true}}


static func _placements(snapshot: Dictionary, profile: Dictionary) -> Array[Dictionary]:
	var placements: Array[Dictionary] = []
	var tiles: Dictionary = snapshot.get("tiles", {})
	var keys: Array = tiles.keys()
	keys.sort()
	var seed_text := "%d|%d" % [int(snapshot.get("world_seed", 0)), int(snapshot.get("visual_seed", 0))]
	for key_value in keys:
		var key := str(key_value)
		var tile: Dictionary = tiles[key_value]
		var terrain := str(tile.get("terrain", "plain"))
		if _water(tile) or terrain in ["hill", "hills", "rock"]:
			continue
		var forest: bool = terrain == "forest" or "tile.forest" in tile.get("traits", [])
		var candidates := int(profile.get("forest_candidates", 22)) if forest else int(profile.get("plain_candidates", 2))
		var center := Buildings.tile_center(key)
		for index in candidates:
			var prefix := seed_text + "|" + key + "|" + str(index)
			var radius_ratio := sqrt(_unit(prefix + "|radius"))
			var density := lerpf(float(profile["center_density"]), float(profile["edge_density"]), pow(radius_ratio, 1.8)) if forest else float(profile["plain_density"])
			if _unit(prefix + "|density") > density:
				continue
			var angle := _unit(prefix + "|angle") * TAU
			var point := center + Vector2(cos(angle), sin(angle)) * radius_ratio * float(profile["scatter_radius"])
			if not _clear(snapshot, point, profile, placements):
				continue
			placements.append({"id": key + ":tree:" + str(index), "tile": key, "source": "forest" if forest else "open_edge",
				"legacy_position": point, "height_legacy": Terrain.height_at(snapshot, point),
				"rotation": _unit(prefix + "|rotation") * TAU,
				"scale": lerpf(float(profile["scale_min"]), float(profile["scale_max"]), _unit(prefix + "|scale"))})
	# Existing street-tree filler specifications use the same single prototype.
	for value in snapshot.get("fabric", {}).get("fillers", []):
		if not value is Dictionary or str(value.get("asset", "")) != "prop_tree_broadleaf":
			continue
		var a := Buildings.tile_center(str(value.get("a", "")))
		var b := Buildings.tile_center(str(value.get("b", "")))
		var point := a.lerp(b, 0.42 if int(value.get("variant", 0)) % 2 == 0 else 0.61) - (b - a).normalized().orthogonal() * 22.0
		if not _clear(snapshot, point, profile, placements):
			continue
		var id := str(value.get("key", "")) + ":streettree:" + str(value.get("variant", 0))
		placements.append({"id": id, "tile": str(value.get("a", "")), "source": "fabric",
			"legacy_position": point, "height_legacy": Terrain.height_at(snapshot, point),
			"rotation": _unit(seed_text + "|" + id) * TAU, "scale": 0.86})
	return placements


static func _clear(snapshot: Dictionary, point: Vector2, profile: Dictionary, existing: Array[Dictionary]) -> bool:
	if Terrain.river_distance(snapshot, point) < float(profile["river_clearance"]):
		return false
	for value in snapshot.get("foundation_field", {}).get("sites", []):
		var site: Dictionary = value
		var center: Vector2 = site.get("center", Vector2.ZERO)
		var offset := point - center
		var margin := float(profile["platform_clearance"])
		if absf(offset.x) < float(site.get("half_x", 18.0)) + margin and absf(offset.y) < float(site.get("half_y", 16.0)) + margin:
			return false
	for value in snapshot.get("fabric", {}).get("connectors", []):
		var a := Buildings.tile_center(str(value.get("a", "")))
		var b := Buildings.tile_center(str(value.get("b", "")))
		if _segment_distance(point, a, b) < float(profile["road_clearance"]):
			return false
	for placement in existing:
		var prior: Vector2 = placement["legacy_position"]
		if prior.distance_to(point) < float(profile["minimum_spacing"]):
			return false
	var step := 3.0
	var dx := (Terrain.height_at(snapshot, point + Vector2(step, 0.0)) - Terrain.height_at(snapshot, point - Vector2(step, 0.0))) / (2.0 * step)
	var dy := (Terrain.height_at(snapshot, point + Vector2(0.0, step)) - Terrain.height_at(snapshot, point - Vector2(0.0, step))) / (2.0 * step)
	return Vector2(dx, dy).length() <= float(profile["max_slope"])


static func _prototype_batches(prototype: Node) -> Array[Dictionary]:
	var batches: Array[Dictionary] = []
	for info in Buildings.mesh_instances(prototype):
		var instance: MeshInstance3D = info["node"]
		var transform: Transform3D = info["transform"]
		var normal_basis := transform.basis.inverse().transposed()
		for surface_index in instance.mesh.get_surface_count():
			if not instance.mesh is ArrayMesh or instance.mesh.surface_get_primitive_type(surface_index) != Mesh.PRIMITIVE_TRIANGLES:
				continue
			var material: Material = instance.get_active_material(surface_index)
			var target := -1
			for index in batches.size():
				if batches[index]["material"] == material:
					target = index
					break
			if target < 0:
				var surface := SurfaceTool.new()
				surface.begin(Mesh.PRIMITIVE_TRIANGLES)
				batches.append({"material": material, "surface": surface, "triangles": 0})
				target = batches.size() - 1
			var arrays: Array = instance.mesh.surface_get_arrays(surface_index)
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX] is PackedInt32Array else PackedInt32Array()
			var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL] if arrays[Mesh.ARRAY_NORMAL] is PackedVector3Array else PackedVector3Array()
			var uvs: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV] if arrays[Mesh.ARRAY_TEX_UV] is PackedVector2Array else PackedVector2Array()
			var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR] if arrays[Mesh.ARRAY_COLOR] is PackedColorArray else PackedColorArray()
			var vertex_count := indices.size() if not indices.is_empty() else vertices.size()
			var surface: SurfaceTool = batches[target]["surface"]
			for triangle_start in range(0, vertex_count, 3):
				var triangle_indices: Array[int] = []
				for offset in 3:
					triangle_indices.append(indices[triangle_start + offset] if not indices.is_empty() else triangle_start + offset)
				var p0 := transform * vertices[triangle_indices[0]]
				var p1 := transform * vertices[triangle_indices[1]]
				var p2 := transform * vertices[triangle_indices[2]]
				var fallback_normal := (p1 - p0).cross(p2 - p0).normalized()
				for vertex_index in triangle_indices:
					surface.set_normal((normal_basis * normals[vertex_index]).normalized() if normals.size() == vertices.size() else fallback_normal)
					surface.set_uv(uvs[vertex_index] if uvs.size() == vertices.size() else Vector2.ZERO)
					surface.set_color(colors[vertex_index] if colors.size() == vertices.size() else Color.WHITE)
					surface.add_vertex(transform * vertices[vertex_index])
			batches[target]["triangles"] = int(batches[target]["triangles"]) + int(float(vertex_count) / 3.0)
	for batch in batches:
		var surface: SurfaceTool = batch["surface"]
		var mesh := surface.commit()
		mesh.surface_set_material(0, batch["material"])
		batch["mesh"] = mesh
		batch.erase("surface")
	return batches


static func _unit(value: String) -> float:
	return float(Buildings.stable_hash(value) % 1000000) / 1000000.0


static func _segment_distance(point: Vector2, a: Vector2, b: Vector2) -> float:
	var segment := b - a
	var ratio := clampf((point - a).dot(segment) / maxf(segment.length_squared(), 0.0001), 0.0, 1.0)
	return point.distance_to(a + segment * ratio)


static func _water(tile: Dictionary) -> bool:
	return str(tile.get("terrain", "")) in ["water", "river", "channel", "sea", "ocean"] or bool(tile.get("water", false)) or bool(tile.get("is_water", false)) or "tile.water" in tile.get("traits", [])
