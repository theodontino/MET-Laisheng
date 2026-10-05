extends SceneTree
## A portable, read-only presentation bridge. Godot's official GLTFDocument
## writes geometry; this file never implements a glTF writer or a new world.
## Shader ALBEDO is sampled into linear vertex colors, without lighting.
const Fixture = preload("res://tests/fixtures/hero_valley.gd")
const Snapshot = preload("res://ui/world3d/world_visual_snapshot.gd")
const World = preload("res://ui/world3d/world_view_3d.gd")
const Buildings = preload("res://ui/world3d/city/building_renderer.gd")
const Terrain = preload("res://ui/world3d/terrain/terrain_builder.gd")
const River = preload("res://ui/world3d/terrain/river_builder.gd")
const Store = preload("res://core/storage.gd")
const DEFAULT_OUTPUT := "res://unity/METLivingWorld/Assets/Resources/Hero"
const SOURCE_FILES := [
	"res://tools/export_unity_hero.gd", "res://tests/fixtures/hero_valley.gd",
	"res://ui/world3d/world_visual_snapshot.gd", "res://ui/world3d/world_view_3d.gd",
	"res://ui/world3d/terrain/terrain_builder.gd", "res://ui/world3d/terrain/river_builder.gd",
	"res://ui/world3d/terrain/terrain_material.gdshader", "res://ui/world3d/terrain/river_material.gdshader",
	"res://ui/world3d/city/building_renderer.gd", "res://ui/world3d/city/fabric_renderer.gd",
	"res://ui/world3d/foliage/foliage_scatter.gd", "res://ui/world3d/environment/environment_state.gd",
	"res://ui/world3d/environment/environment_controller.gd", "res://content/presentation3d/asset_manifest.json",
]

func _initialize() -> void:
	call_deferred("_run_cli")

func _run_cli() -> void:
	var output := DEFAULT_OUTPUT
	var args := OS.get_cmdline_user_args()
	for index in args.size():
		if args[index] == "--output" and index + 1 < args.size(): output = args[index + 1]
	var result := export_bundle(root, output)
	if not result.get("ok", false):
		push_error(str(result.get("error", "Hero export failed")))
		quit(1)
		return
	print("UNITY_HERO_EXPORT " + JSON.stringify(result, "", true, true))
	quit()

static func export_bundle(host: Node, output: String) -> Dictionary:
	var directory := ProjectSettings.globalize_path(output)
	var error := DirAccess.make_dir_recursive_absolute(directory)
	if error != OK: return failure("Cannot create output directory", error)
	var game = Fixture.make_game()
	var before := live_proof(game)
	var registry := Buildings.load_manifest()
	var snapshot := Snapshot.from_display(Fixture.committed(game), game.c.projects,
		Fixture.WORLD_SEED, Fixture.VISUAL_SEED, registry)
	if not snapshot.get("ok", false): return {"ok": false, "error": str(snapshot.get("error", "Invalid committed snapshot"))}
	var snapshot_before := Snapshot.digest(snapshot)
	var view := World.new()
	view.name = "HeroExportSource"
	view.input_enabled = false
	host.add_child(view)
	if not view.set_visual_snapshot(snapshot):
		view.free()
		return {"ok": false, "error": "World rejected committed snapshot"}
	var world := Node3D.new()
	world.name = "MET_HeroWorld"
	for kind in ["terrain", "river", "buildings", "fabric"]:
		world.add_child(clone_geometry(view.results[kind].node, kind, snapshot))
	var tree := Node3D.new()
	tree.name = "MET_ValleyTreePrototype"
	for batch in view.results.foliage.node.get_children():
		var source: MultiMeshInstance3D = batch
		var instance := MeshInstance3D.new()
		instance.name = source.name
		instance.mesh = portable_mesh(source.multimesh.mesh, source, "foliage", snapshot)
		tree.add_child(instance)
	var world_info := geometry_info(world)
	var tree_info := geometry_info(tree)
	var exported := write_glb(world, directory.path_join("world.glb"))
	if exported == OK: exported = write_glb(tree, directory.path_join("foliage.glb"))
	if exported != OK:
		world.free(); tree.free(); view.free()
		return failure("Official GLTFDocument export failed", exported)
	var after := live_proof(game)
	var sidecar := make_sidecar(view, snapshot, before, after, world_info, tree_info)
	sidecar.isolation_proof.snapshot_before_sha256 = snapshot_before
	sidecar.isolation_proof.snapshot_after_sha256 = Snapshot.digest(snapshot)
	sidecar.isolation_proof.snapshot_unchanged = snapshot_before == Snapshot.digest(snapshot)
	sidecar.outputs = {
		"world": {"path": "world.glb", "sha256": FileAccess.get_sha256(directory.path_join("world.glb"))},
		"foliage": {"path": "foliage.glb", "sha256": FileAccess.get_sha256(directory.path_join("foliage.glb"))},
	}
	var file := FileAccess.open(directory.path_join("hero_valley.json"), FileAccess.WRITE)
	if file == null:
		world.free(); tree.free(); view.free()
		return failure("Cannot write Hero sidecar", FileAccess.get_open_error())
	file.store_string(JSON.stringify(sidecar, "\t", true, true) + "\n")
	file.close()
	world.free(); tree.free(); view.free()
	var unchanged: bool = sidecar.isolation_proof.live_state_unchanged and sidecar.isolation_proof.snapshot_unchanged
	if not unchanged: return {"ok": false, "error": "Export mutated Gameplay or detached snapshot"}
	return {"ok": true, "output": directory, "model_stats": sidecar.model_stats,
		"snapshot_signature": str(snapshot.signature), "live_state_unchanged": unchanged,
		"files": {"world.glb": sidecar.outputs.world.sha256,
			"foliage.glb": sidecar.outputs.foliage.sha256,
			"hero_valley.json": FileAccess.get_sha256(directory.path_join("hero_valley.json"))}}

static func failure(message: String, code: int) -> Dictionary:
	return {"ok": false, "error": message + ": " + error_string(code)}

static func clone_geometry(source: Node3D, kind: String, snapshot: Dictionary, fallback_name := "Geometry") -> Node3D:
	var target: Node3D
	if source is MeshInstance3D:
		var mesh := MeshInstance3D.new()
		mesh.mesh = portable_mesh(source.mesh, source, kind, snapshot)
		mesh.cast_shadow = source.cast_shadow
		target = mesh
	else: target = Node3D.new()
	# Godot assigns unnamed nodes names containing process-global instance IDs.
	# Preserve authored names; make those automatic labels sibling-index stable.
	target.name = fallback_name if str(source.name).begins_with("@") or str(source.name) == "" else source.name
	target.transform = source.transform
	var child_index := 0
	for child in source.get_children():
		if child is Node3D and not child is MultiMeshInstance3D:
			target.add_child(clone_geometry(child, kind, snapshot, "%s_%02d" % [child.get_class(), child_index]))
		child_index += 1
	return target

static func portable_mesh(source: Mesh, instance: Node, kind: String, snapshot: Dictionary) -> ArrayMesh:
	var mesh := ArrayMesh.new()
	var normalized_faces := 0
	for surface_index in source.get_surface_count():
		var arrays: Array = source.surface_get_arrays(surface_index).duplicate(true)
		# Some legacy Fabric ribbons have a mathematical front opposite to their
		# supplied upward normals. Correct presentation winding in this detached
		# export copy only; GLTFDocument handles its own official glTF conversion.
		var points: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
		var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX] is PackedInt32Array else PackedInt32Array()
		if indices.is_empty():
			for index in points.size(): indices.append(index)
		for start in range(0, indices.size(), 3):
			var a := int(indices[start])
			var b := int(indices[start + 1])
			var c := int(indices[start + 2])
			if (points[b] - points[a]).cross(points[c] - points[a]).dot(normals[a] + normals[b] + normals[c]) > 0.0000000001:
				indices[start + 1] = c
				indices[start + 2] = b
				normalized_faces += 1
		arrays[Mesh.ARRAY_INDEX] = indices
		var material: Material = instance.get_active_material(surface_index) if instance is MeshInstance3D else source.surface_get_material(surface_index)
		var portable: StandardMaterial3D
		if material is ShaderMaterial:
			portable = StandardMaterial3D.new()
			portable.resource_name = "MET_" + kind + "_albedo_only_PBR"
			portable.albedo_color = Color.WHITE
			portable.vertex_color_use_as_albedo = true
			portable.vertex_color_is_srgb = false
			portable.roughness = 0.91 if kind == "terrain" else 0.50
			portable.metallic = 0.0
			portable.cull_mode = BaseMaterial3D.CULL_BACK
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var uvs: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
			var weights: PackedColorArray = arrays[Mesh.ARRAY_COLOR]
			var colors := PackedColorArray()
			for index in vertices.size():
				colors.append(terrain_albedo(uvs[index], weights[index], int(snapshot.visual_seed)) if kind == "terrain" else river_albedo(vertices[index], uvs[index]))
			arrays[Mesh.ARRAY_COLOR] = colors
		elif material is StandardMaterial3D:
			portable = material.duplicate(true)
		else:
			portable = StandardMaterial3D.new()
			portable.albedo_color = Color.WHITE
			portable.roughness = 0.9
		mesh.add_surface_from_arrays(source.surface_get_primitive_type(surface_index), arrays)
		mesh.surface_set_material(mesh.get_surface_count() - 1, portable)
	mesh.set_meta("normalized_front_faces", normalized_faces)
	return mesh

static func terrain_albedo(uv: Vector2, weights: Color, visual_seed: int) -> Color:
	# Source-color uniforms enter Godot shaders as linear RGB. Preserve those
	# values for standard glTF vertex-color multiplication in either engine.
	var grass := Color(0.38, 0.48, 0.27).srgb_to_linear()
	var grass_light := Color(0.57, 0.61, 0.37).srgb_to_linear()
	var dirt := Color(0.56, 0.47, 0.33).srgb_to_linear()
	var rock := Color(0.54, 0.55, 0.50).srgb_to_linear()
	var seed_value := float(visual_seed % 4096)
	var micro := surface_noise(uv * 22.0, seed_value)
	var broad := surface_noise(uv * 0.85, seed_value)
	var patch := surface_noise(uv * 3.1 + Vector2(9.1, 31.7), seed_value)
	var surface := grass.lerp(grass_light, weights.a * 0.35 + broad * 0.42 + patch * 0.12).lerp(dirt, weights.r)
	rock *= 0.88 + surface_noise(uv * 9.0, seed_value) * 0.18
	surface = surface.lerp(rock, smoothstep(0.12, 0.65, weights.g) * 0.90)
	surface *= (0.98 + micro * 0.04) * (1.0 - weights.b * 0.14)
	surface.a = 1.0
	return surface

static func surface_noise(point: Vector2, seed_value: float) -> float:
	var cell := point.floor()
	var local := point - cell
	local = local * local * (Vector2(3.0, 3.0) - local * 2.0)
	return lerpf(lerpf(surface_hash(cell, seed_value), surface_hash(cell + Vector2(1, 0), seed_value), local.x),
		lerpf(surface_hash(cell + Vector2(0, 1), seed_value), surface_hash(cell + Vector2(1, 1), seed_value), local.x), local.y)

static func surface_hash(point: Vector2, seed_value: float) -> float:
	var value := sin(point.dot(Vector2(127.1, 311.7)) + seed_value * 0.071) * 43758.5453
	return value - floor(value)

static func river_albedo(position: Vector3, uv: Vector2) -> Color:
	var bank := pow(absf(uv.x * 2.0 - 1.0), 2.3)
	var ripple := sin(position.x * 7.3 + position.z * 4.1 + sin(position.z * 2.4)) * 0.5 + 0.5
	var slow := sin(position.x * 1.1 - position.z * 1.7) * 0.5 + 0.5
	var result := Color(0.15, 0.34, 0.39).srgb_to_linear().lerp(Color(0.38, 0.54, 0.46).srgb_to_linear(), bank * 0.72)
	result *= 0.99 + ripple * 0.02 + slow * 0.018
	result.a = 1.0
	return result

static func write_glb(node: Node3D, path: String) -> int:
	var document := GLTFDocument.new()
	var state := GLTFState.new()
	var error := document.append_from_scene(node, state)
	if error != OK: return error
	return document.write_to_filesystem(state, path)

static func make_sidecar(view: Node3D, snapshot: Dictionary, before: Dictionary, after: Dictionary, world_info: Dictionary, tree_info: Dictionary) -> Dictionary:
	var tiles: Array[Dictionary] = []
	var tile_keys: Array = snapshot.tiles.keys()
	tile_keys.sort()
	for key in tile_keys:
		var tile: Dictionary = snapshot.tiles[key]
		var center := Buildings.tile_center(str(key))
		var outline: Array = []
		for edge in 6:
			var a := center + Vector2.from_angle(PI / 6.0 + TAU * edge / 6.0) * 64.0
			var b := center + Vector2.from_angle(PI / 6.0 + TAU * (edge + 1) / 6.0) * 64.0
			for segment in 8: outline.append(vector(overlay_point(snapshot, a.lerp(b, float(segment) / 8.0))))
		tiles.append({"key": str(key), "q": int(tile.q), "r": int(tile.r), "terrain": str(tile.terrain),
			"center": vector(Terrain.world_position(snapshot, center)), "outline": outline})
	var placements: Array[Dictionary] = []
	var transforms: Array = view.results.foliage.transforms
	var source_placements: Array = view.results.foliage.placements
	for index in transforms.size():
		var transform: Transform3D = transforms[index]
		var quaternion := transform.basis.orthonormalized().get_rotation_quaternion()
		placements.append({"id": str(source_placements[index].id), "tile": str(source_placements[index].tile),
			"position": vector(transform.origin), "rotation": [quaternion.x, quaternion.y, quaternion.z, quaternion.w],
			"scale": vector(transform.basis.get_scale())})
	var buildings: Array[Dictionary] = []
	var grounding: Array[Dictionary] = []
	for placement in view.results.buildings.placements:
		var entry := Buildings.asset_entry(snapshot.asset_registry, str(placement.asset_id))
		var contact_transform: Transform3D = placement.ground_transform
		contact_transform.origin += placement.position
		var footprint: Array = entry.footprint.xz_bounds
		var minimum_y := float(entry.bounds.min[1])
		var corners := [Vector3(footprint[0], minimum_y, footprint[1]), Vector3(footprint[0], minimum_y, footprint[3]),
			Vector3(footprint[2], minimum_y, footprint[3]), Vector3(footprint[2], minimum_y, footprint[1])]
		for edge in 4:
			for ratio in [0.0, 0.2, 0.4, 0.6, 0.8]:
				var point: Vector3 = contact_transform * corners[edge].lerp(corners[(edge + 1) % 4], ratio)
				grounding.append({"building_id": str(placement.project_id), "position": vector(point),
					"expected_height": float(placement.platform_height_legacy) / 64.0, "tolerance": 0.01})
		buildings.append({"id": str(placement.project_id), "asset_id": str(placement.asset_id), "tile": str(placement.tile),
			"node_name": "Building_" + str(placement.project_id).validate_node_name(), "position": vector(placement.position),
			"scale": vector(placement.scale), "pivot": vector(placement.pivot), "bounds": entry.bounds.duplicate(true)})
	var stats := stable_stats(view.stats)
	var model_stats := {"tiles": tiles.size(), "buildings": buildings.size(), "trees": placements.size(),
		"foliage_batches": int(stats.foliage.batches), "triangles_world": int(world_info.triangles),
		"triangles_per_tree": int(tree_info.triangles), "mesh_instances_world": int(world_info.mesh_instances),
		"terrain_triangles": int(stats.terrain.terrain_triangles), "terrain_skirt_triangles": int(stats.terrain.landscape_edge_triangles),
		"foundation_meshes": int(stats.terrain.foundation_meshes), "foundation_triangles": int(stats.terrain.foundation_triangles),
		"river_triangles": int(stats.river.river_triangles), "building_triangles": int(stats.buildings.triangles),
		"road_triangles": int(stats.fabric.lane_triangles), "terrain": stats.terrain, "river": stats.river,
		"buildings_detail": stats.buildings, "fabric": stats.fabric, "foliage": stats.foliage}
	var environment = view.environment_controller
	var state: Dictionary = environment.state
	var env: Environment = environment.world_environment.environment
	var sources: Dictionary = {}
	for path in SOURCE_FILES: sources[path.trim_prefix("res://")] = FileAccess.get_sha256(path)
	for asset in snapshot.asset_registry.assets:
		var path := str(asset.runtime_path).trim_prefix("res://")
		sources[path] = FileAccess.get_sha256("res://" + path)
	return {"schema": 1, "fixture": "MET Hero Valley V1", "source_phase": "committed_tick", "forecast": false,
		"world_seed": int(snapshot.world_seed), "visual_seed": int(snapshot.visual_seed),
		"snapshot_signature": str(snapshot.signature), "snapshot_id": str(snapshot.snapshot_id), "tick": int(snapshot.tick),
		"coordinate_system": {"handedness": "right", "up": "+Y", "unit": "world_hex_radius_1",
			"geometry": "Godot official GLTFDocument glTF 2.0", "json": "same right-handed coordinates as GLB",
			"unity_conversion": "glTFast mirrors X; mirror JSON X once, quaternion [x,-y,-z,w]; never reflect imported geometry again"},
		"geometry_conversion": {"writer": "Godot GLTFDocument", "front_face": "clockwise in Godot, official glTF conversion by writer",
			"front_faces_normalized": int(world_info.front_faces_normalized), "source_geometry_mutated": false,
			"bounds": "actual indexed geometry, excluding unused lattice/river vertices clipped from visible topology"},
		"albedo_conversion": {"mode": "linear_vertex_color_PBR", "lighting_baked": false,
			"terrain": "original shader ALBEDO sampled at actual mesh vertices; fine fragment noise approximated",
			"river": "original fixed shader ALBEDO sampled at actual vertices; constant roughness 0.50",
			"building_foliage": "existing standard PBR materials preserved", "shaders_ported": false},
		"toolchain": {"exporter": "Godot GLTFDocument", "godot": Engine.get_version_info().string},
		"model_stats": model_stats, "tiles": tiles, "buildings": buildings,
		"foliage": placements, "grounding_samples": grounding, "bounds": world_info.bounds,
		"camera": {"position": vector(view.camera.position), "target": vector(view.camera_target), "up": [0.0, 1.0, 0.0],
			"vertical_span": float(view.camera.size), "near": float(view.camera.near), "far": float(view.camera.far), "projection": "orthographic"},
		"environment": {"world_time": float(state.world_time), "sun_direction": vector(-environment.sun.transform.basis.z),
			"sun_color": color_values(environment.sun.light_color), "sun_intensity": float(environment.sun.light_energy),
			"background": color_values(env.background_color), "ambient_color": color_values(env.ambient_light_color),
			"ambient_intensity": float(env.ambient_light_energy), "color_space": "srgb",
			"cloudiness": float(state.cloudiness), "fog_density": float(state.fog_density), "humidity": float(state.humidity),
			"wetness": float(state.wetness), "wind": vector(state.wind), "temperature_band": str(state.temperature_band)},
		"isolation_proof": {"before": before, "after": after, "live_state_unchanged": before == after,
			"player_save_read": false, "player_save_written": false, "gameplay_rng_used_by_presentation": false},
		"source_files_sha256": sources}

static func overlay_point(snapshot: Dictionary, point: Vector2) -> Vector3:
	var result := Terrain.world_position(snapshot, point, 0.024)
	if Terrain.river_distance(snapshot, point) <= Terrain.river_half_width(point):
		result.y = maxf(result.y, River.WATER_HEIGHT + 0.024)
	return result

static func stable_stats(value: Variant) -> Variant:
	if value is Dictionary:
		var result: Dictionary = {}
		for key in value:
			if str(key) in ["build_cpu_ms", "construction_cpu_ms"]: continue
			result[key] = stable_stats(value[key])
		return result
	if value is Array:
		var result: Array = []
		for item in value: result.append(stable_stats(item))
		return result
	return value

static func live_proof(game) -> Dictionary:
	return {"state_sha256": Store.encode(game.s).sha256_text(), "rng_sha256": Snapshot.digest(game.s.rng),
		"wallet_sha256": Snapshot.digest({"currency": game.s.currency, "dt": game.s.dt}),
		"compiled_sha256": Snapshot.digest(game.compiled), "revision": int(game.revision)}

static func geometry_info(node: Node3D) -> Dictionary:
	var triangles := 0
	var vertices := 0
	var meshes := 0
	var surfaces := 0
	var normalized_faces := 0
	var first := true
	var bounds := AABB()
	for info in Buildings.mesh_instances(node):
		var instance: MeshInstance3D = info.node
		meshes += 1
		normalized_faces += int(instance.mesh.get_meta("normalized_front_faces", 0))
		for index in instance.mesh.get_surface_count():
			surfaces += 1
			var arrays: Array = instance.mesh.surface_get_arrays(index)
			var points: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX] is PackedInt32Array else PackedInt32Array()
			triangles += int((indices.size() if not indices.is_empty() else points.size()) / 3)
			vertices += points.size()
			# The source builders retain vertices outside the clipped topology.
			# They do not belong to rendered bounds; official glTF strips them.
			var count := indices.size() if not indices.is_empty() else points.size()
			for offset in count:
				var world_point: Vector3 = info.transform * points[indices[offset] if not indices.is_empty() else offset]
				if first: bounds = AABB(world_point, Vector3.ZERO); first = false
				else: bounds = bounds.expand(world_point)
	return {"triangles": triangles, "vertices": vertices, "mesh_instances": meshes, "surfaces": surfaces, "front_faces_normalized": normalized_faces,
		"bounds": {"min": vector(bounds.position), "max": vector(bounds.end)}}

static func vector(value: Vector3) -> Array:
	return [value.x, value.y, value.z]

static func color_values(value: Color) -> Array:
	return [value.r, value.g, value.b, value.a]
