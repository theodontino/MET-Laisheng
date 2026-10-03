extends RefCounted
## Runtime geometry only. All placement comes from the detached visual snapshot;
## GLB materials remain lit by the common EnvironmentController.

const Terrain = preload("res://ui/world3d/terrain/terrain_builder.gd")
const MANIFEST_PATH := "res://content/presentation3d/asset_manifest.json"
const LEGACY_UNITS := 64.0
const DEFAULT_BINDINGS := {
	"homestead": "building.homestead.t1",
	"neighborhood": "building.neighborhood.t1",
	"workshop": "building.workshop.t1",
	"residence": "building.residence.t2",
}


static func build(snapshot: Dictionary) -> Dictionary:
	var root := Node3D.new()
	root.name = "CityBuildings"
	var manifest: Dictionary = snapshot.get("asset_registry", {})
	if manifest.is_empty():
		manifest = load_manifest()
	var sites_by_project: Dictionary = {}
	for site_value in snapshot.get("foundation_field", {}).get("sites", []):
		if site_value is Dictionary:
			var site: Dictionary = site_value
			sites_by_project[str(site.get("project_id", ""))] = site
	var projects: Dictionary = snapshot.get("projects", {})
	var project_ids: Array = projects.keys()
	project_ids.sort()
	var placements: Array[Dictionary] = []
	var missing: Array[String] = []
	var construction_sites := 0
	var offmap_sources := 0
	var mesh_count := 0
	var triangle_count := 0
	for id_value in project_ids:
		var project_id := str(id_value)
		var project: Dictionary = projects[id_value]
		if bool(project.get("special", false)) or str(project.get("tile", "")).begins_with("offmap:"):
			offmap_sources += 1
			continue
		if str(project.get("status", "running")) == "construction":
			construction_sites += 1
			continue
		var definition_id := str(project.get("def", ""))
		var entry := entry_for_definition(manifest, definition_id)
		if entry.is_empty():
			missing.append(project_id + ":unregistered:" + definition_id)
			continue
		var scene := load_scene(entry)
		if scene == null:
			missing.append(project_id + ":unavailable:" + str(entry.get("id", "")))
			continue
		var instance_value: Node = scene.instantiate()
		if not instance_value is Node3D:
			missing.append(project_id + ":not_node3d")
			instance_value.free()
			continue
		var instance: Node3D = instance_value
		var tile := str(project.get("tile", ""))
		var site: Dictionary = sites_by_project.get(project_id, {})
		var center: Vector2 = site.get("center", tile_center(tile))
		var platform_height := float(site.get("platform_height", Terrain.height_at(snapshot, center)))
		var scale_value := asset_scale(entry)
		var pivot: Vector3 = vector3_value(entry.get("pivot", [0.0, 0.0, 0.0]))
		var holder := Node3D.new()
		holder.name = "Building_" + project_id.validate_node_name()
		holder.position = world_position(center, platform_height)
		holder.set_meta("project_id", project_id)
		holder.set_meta("logical_asset_id", str(entry.get("id", "")))
		holder.set_meta("tile", tile)
		holder.set_meta("platform_height_legacy", platform_height)
		# Metadata pivot defines the horizontal placement anchor. Compensate the
		# model's registered minimum Y independently so even an elevated/nonzero
		# source pivot cannot bury or float its ground-contact plane.
		var local_transform := grounded_asset_transform(entry, Vector3.ZERO)
		instance.transform = local_transform * instance.transform
		holder.add_child(instance)
		root.add_child(holder)
		var meshes := mesh_instances(instance)
		for mesh_info in meshes:
			var mesh_instance: MeshInstance3D = mesh_info["node"]
			mesh_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
			mesh_count += 1
			if mesh_instance.mesh != null:
				for surface_index in mesh_instance.mesh.get_surface_count():
					var arrays: Array = mesh_instance.mesh.surface_get_arrays(surface_index)
					var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX] is PackedInt32Array else PackedInt32Array()
					var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
					triangle_count += int(float(indices.size() if not indices.is_empty() else vertices.size()) / 3.0)
		placements.append({
			"project_id": project_id,
			"tile": tile,
			"asset_id": str(entry.get("id", "")),
			"position": holder.position,
			"scale": scale_value,
			"pivot": pivot,
			"ground_transform": local_transform,
			"platform_height_legacy": platform_height,
			"ground_height_legacy": Terrain.height_at(snapshot, center),
		})
	return {
		"node": root,
		"placements": placements,
		"signature": placement_signature(placements),
		"stats": {"buildings": placements.size(), "mesh_instances": mesh_count,
			"triangles": triangle_count, "construction_sites_skipped": construction_sites,
			"offmap_sources_skipped": offmap_sources,
			"missing_assets": missing, "presentation_only": true},
	}


static func load_manifest() -> Dictionary:
	if not FileAccess.file_exists(MANIFEST_PATH):
		return {}
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST_PATH))
	return parsed if parsed is Dictionary else {}


static func asset_entry(manifest: Dictionary, asset_id: String) -> Dictionary:
	var assets: Variant = manifest.get("assets", [])
	if assets is Dictionary:
		return assets.get(asset_id, {})
	if assets is Array:
		for value in assets:
			if value is Dictionary and str(value.get("id", "")) == asset_id:
				return value
	return {}


static func entry_for_definition(manifest: Dictionary, definition_id: String) -> Dictionary:
	var bindings: Dictionary = manifest.get("bindings", DEFAULT_BINDINGS)
	var asset_id := str(bindings.get(definition_id, DEFAULT_BINDINGS.get(definition_id, "")))
	return asset_entry(manifest, asset_id)


static func load_scene(entry: Dictionary) -> PackedScene:
	var path := str(entry.get("runtime_path", ""))
	if path == "":
		return null
	if not path.begins_with("res://"):
		path = "res://" + path
	if not ResourceLoader.exists(path):
		return null
	var resource: Resource = load(path)
	return resource if resource is PackedScene else null


static func asset_scale(entry: Dictionary) -> Vector3:
	var scale_value: Variant = entry.get("scale", 1.0)
	if scale_value is float or scale_value is int:
		return Vector3.ONE * float(scale_value)
	return vector3_value(scale_value, Vector3.ONE)


static func grounded_asset_transform(entry: Dictionary, ground_anchor: Vector3, rotation := 0.0, variation := 1.0) -> Transform3D:
	## Upright Y-axis rotation is the only V1 heading variation. Scale is local
	## before rotation; the pivot offset must follow that same rotated basis.
	var scale_value := asset_scale(entry) * variation
	var basis := Basis(Vector3.UP, rotation) * Basis.from_scale(scale_value)
	var pivot := vector3_value(entry.get("pivot", [0.0, 0.0, 0.0]))
	var bounds_value: Variant = entry.get("bounds", {})
	var minimum_y := 0.0
	if bounds_value is Dictionary:
		minimum_y = vector3_value(bounds_value.get("min", [0.0, 0.0, 0.0])).y
	elif bounds_value is Array and bounds_value.size() >= 6:
		minimum_y = float(bounds_value[1])
	var origin := ground_anchor - basis * pivot
	origin.y -= (minimum_y - pivot.y) * scale_value.y
	return Transform3D(basis, origin)


static func vector3_value(value: Variant, fallback := Vector3.ZERO) -> Vector3:
	if value is Vector3:
		return value
	if value is Array and value.size() >= 3:
		return Vector3(float(value[0]), float(value[1]), float(value[2]))
	return fallback


static func tile_center(key: String) -> Vector2:
	var pieces := key.split(",", false, 1)
	if pieces.size() != 2:
		return Vector2.ZERO
	var q := float(pieces[0])
	var r := float(pieces[1])
	return Vector2(sqrt(3.0) * LEGACY_UNITS * (q + r * 0.5), LEGACY_UNITS * 1.5 * r)


static func world_position(point: Vector2, height: float) -> Vector3:
	return Vector3(point.x, height, -point.y) / LEGACY_UNITS


static func mesh_instances(node: Node, parent_transform := Transform3D.IDENTITY) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	var accumulated := parent_transform
	if node is Node3D:
		accumulated = parent_transform * node.transform
	if node is MeshInstance3D and node.mesh != null:
		result.append({"node": node, "transform": accumulated})
	for child in node.get_children():
		result.append_array(mesh_instances(child, accumulated))
	return result


static func stable_hash(value: String) -> int:
	var result := 5381
	for index in value.length():
		result = int(((result << 5) + result) ^ value.unicode_at(index)) & 0x7fffffff
	return result


static func placement_signature(placements: Array[Dictionary]) -> String:
	var signature_text := ""
	for placement in placements:
		signature_text += var_to_str(placement) + "\n"
	return signature_text.sha256_text()
