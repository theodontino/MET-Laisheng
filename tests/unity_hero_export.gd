extends SceneTree
## Executes the official exporter twice, reimports both GLBs through the
## official importer, and checks actual geometry and read-only provenance.
## This is Godot bridge evidence; it does not claim Unity editor execution.
const Export = preload("res://tools/export_unity_hero.gd")
const Buildings = preload("res://ui/world3d/city/building_renderer.gd")
var checks: Array[Dictionary] = []

func _initialize() -> void:
	call_deferred("run")

func check(condition: bool, name: String) -> void:
	checks.append({"name": name, "pass": condition})
	print(("PASS " if condition else "FAIL ") + name)

func run() -> void:
	var output := OS.get_environment("MET_UNITY_EXPORT_TEST_DIR")
	if output == "": output = OS.get_cache_dir().path_join("met-unity-export-tests")
	DirAccess.make_dir_recursive_absolute(output)
	var first := Export.export_bundle(root, output.path_join("first"))
	var second := Export.export_bundle(root, output.path_join("second"))
	check(first.get("ok", false) and second.get("ok", false), "official GLTFDocument exports both deterministic runs")
	if not first.get("ok", false) or not second.get("ok", false):
		finish(output, {"first": first, "second": second})
		return
	check(first.files == second.files, "world GLB, shared tree GLB and sidecar are byte-identical across repeated exports")
	var metadata: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(output.path_join("first/hero_valley.json")))
	check(int(metadata.schema) == 1 and metadata.source_phase == "committed_tick" and not metadata.forecast, "portable JSON is versioned committed presentation data")
	check(int(metadata.world_seed) == 905003 and int(metadata.visual_seed) == 314159, "fixture world and independent visual seeds are fixed")
	check(metadata.snapshot_signature == first.snapshot_signature and first.snapshot_signature == second.snapshot_signature, "identical fixture produces identical committed visual snapshot signature")
	var proof: Dictionary = metadata.isolation_proof
	check(proof.before == proof.after and proof.live_state_unchanged, "complete encoded state, RNG, wallet, compiled data and revision remain unchanged")
	check(proof.snapshot_before_sha256 == proof.snapshot_after_sha256 and proof.snapshot_unchanged, "export does not mutate detached visual snapshot")
	check(not proof.player_save_read and not proof.player_save_written and not proof.gameplay_rng_used_by_presentation, "fixture export never reads or writes player save or advances gameplay RNG")
	check(int(metadata.model_stats.tiles) == 37 and metadata.tiles.size() == 37, "portable Hero Valley has 37 logical Hex tiles")
	check(int(metadata.model_stats.buildings) == 4 and metadata.buildings.size() == 4, "four existing buildings retain logical identity")
	check(int(metadata.model_stats.trees) == 29 and metadata.foliage.size() == 29, "29 existing tree transforms are exported separately from prototype")
	check(int(metadata.model_stats.foliage_batches) == 6 and int(metadata.model_stats.triangles_per_tree) == 1892, "single existing tree prototype retains six shared material batches and 1892 triangles")
	check(metadata.coordinate_system.handedness == "right" and metadata.coordinate_system.up == "+Y", "JSON and official GLB both declare original RH Y-up coordinates")
	check(metadata.coordinate_system.unity_conversion.contains("mirror JSON X once"), "Unity conversion boundary explicitly prevents geometry double reflection")
	check(metadata.albedo_conversion.mode == "linear_vertex_color_PBR" and not metadata.albedo_conversion.lighting_baked, "only static shader ALBEDO is sampled, with no directional lighting baked")
	check(metadata.camera.projection == "orthographic" and is_equal_approx(float(metadata.camera.vertical_span), 12.0), "fixed orthographic camera retains original vertical span")
	check(vector(metadata.camera.position).distance_to(vector(metadata.camera.target)) > 23.99, "fixed camera preserves original miniature viewing distance")
	check(metadata.environment.color_space == "srgb" and is_equal_approx(float(metadata.environment.sun_intensity), 1.15), "single fixed daylight has explicit color and intensity semantics")
	check(is_equal_approx(vector(metadata.environment.sun_direction).length(), 1.0) and is_equal_approx(float(metadata.environment.wetness), 0.0), "fixed environment direction and dry weather are valid")
	check(no_gameplay_payload(metadata), "portable sidecar excludes Simulation wallets, entities, groups, rules and resource inventories")
	var tile_keys: Dictionary = {}
	var tiles_valid := true
	for tile in metadata.tiles:
		tiles_valid = tiles_valid and not tile_keys.has(tile.key) and tile.outline.size() == 48 and finite_vector(tile.center)
		tile_keys[tile.key] = true
		for point in tile.outline: tiles_valid = tiles_valid and finite_vector(point)
	check(tiles_valid, "unique logical tile centers and terrain-following 48-point outlines are finite")
	var trees_valid := true
	var tree_ids: Dictionary = {}
	for tree in metadata.foliage:
		var rotation := Quaternion(tree.rotation[0], tree.rotation[1], tree.rotation[2], tree.rotation[3])
		trees_valid = trees_valid and finite_vector(tree.position) and finite_vector(tree.scale) and not tree_ids.has(tree.id)
		trees_valid = trees_valid and is_equal_approx(rotation.length(), 1.0) and vector(tree.scale).x > 0.0 and tile_keys.has(tree.tile)
		tree_ids[tree.id] = true
	check(trees_valid, "tree positions, normalized quaternions, positive scales and tile references are valid")
	var paths_match := true
	for source_path in metadata.source_files_sha256:
		paths_match = paths_match and FileAccess.get_sha256("res://" + source_path) == metadata.source_files_sha256[source_path]
	check(paths_match, "recorded source and registered asset SHA256 map is traceable")
	check(metadata.outputs.world.sha256 == first.files["world.glb"] and metadata.outputs.foliage.sha256 == first.files["foliage.glb"], "sidecar records actual exported GLB SHA256")
	var world := import_glb(output.path_join("first/world.glb"))
	var tree := import_glb(output.path_join("first/foliage.glb"))
	check(world != null and tree != null, "both exported GLBs reimport through official Godot GLTFDocument")
	if world != null and tree != null:
		var world_info := Export.geometry_info(world)
		var tree_info := Export.geometry_info(tree)
		check(int(world_info.triangles) == int(metadata.model_stats.triangles_world), "round-trip world preserves actual terrain, river, foundation, building and road triangle count")
		check(int(tree_info.triangles) == 1892 and int(tree_info.mesh_instances) == 6, "round-trip prototype remains six shared meshes, without per-tree scene duplication")
		check(int(world_info.mesh_instances) == int(metadata.model_stats.mesh_instances_world), "round-trip world preserves actual mesh-instance count")
		check(bounds_close(world_info.bounds, metadata.bounds), "round-trip world preserves actual transformed geometry bounds")
		check(count_kind(world, "Camera3D") == 0 and count_kind(world, "Light3D") == 0 and count_kind(world, "MultiMeshInstance3D") == 0, "portable world contains geometry only, without hidden camera, lighting or instanced forest")
		check(valid_materials(world) and valid_materials(tree), "all round-trip materials are standard lit PBR with no Godot-only shader")
		check(valid_normals(world) and valid_normals(tree), "actual round-trip mesh positions and normals are finite with consistent clockwise front faces")
		check(check_grounding(world, metadata.grounding_samples), "80 registered building ground-contact samples remain supported by actual exported terrain and platform triangles")
		world.free(); tree.free()
	var world_json := glb_json(output.path_join("first/world.glb"))
	var tree_json := glb_json(output.path_join("first/foliage.glb"))
	check(not world_json.is_empty() and not tree_json.is_empty(), "official outputs have valid binary glTF 2.0 container headers and JSON chunks")
	check(has_vertex_colors(world_json), "portable glTF primitives contain baked base-color attributes")
	check(not world_json.has("cameras") and not str(world_json.get("extensionsUsed", [])).contains("KHR_lights_punctual"), "official glTF payload does not embed camera or directional illumination")
	finish(output, {"first": first, "second": second, "metadata": metadata})

func finish(output: String, evidence: Dictionary) -> void:
	var failed := 0
	for item in checks:
		if not item.pass: failed += 1
	evidence["checks"] = checks
	evidence["passed"] = checks.size() - failed
	evidence["failed"] = failed
	evidence["scope"] = "Godot official GLTF export/import and data bridge; Unity editor and target macOS runtime unverified"
	var file := FileAccess.open(output.path_join("unity-hero-export-tests.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(evidence, "\t", true, true) + "\n")
	file.close()
	print("UNITY_HERO_EXPORT_TESTS %d/%d " % [checks.size() - failed, checks.size()] + output)
	quit(1 if failed > 0 else 0)

static func vector(value: Array) -> Vector3:
	return Vector3(float(value[0]), float(value[1]), float(value[2]))

static func finite_vector(value: Array) -> bool:
	return value.size() == 3 and is_finite(float(value[0])) and is_finite(float(value[1])) and is_finite(float(value[2]))

static func no_gameplay_payload(metadata: Dictionary) -> bool:
	for forbidden in ["state", "projects", "groups", "wallet", "inventory", "rules", "compiled", "metrics", "entities"]:
		if metadata.has(forbidden): return false
	return true

static func import_glb(path: String) -> Node3D:
	var document := GLTFDocument.new()
	var state := GLTFState.new()
	if document.append_from_file(path, state) != OK: return null
	var scene: Node = document.generate_scene(state)
	return scene if scene is Node3D else null

static func bounds_close(a: Dictionary, b: Dictionary) -> bool:
	return vector(a.min).distance_to(vector(b.min)) < 0.0001 and vector(a.max).distance_to(vector(b.max)) < 0.0001

static func count_kind(node: Node, kind: String) -> int:
	var count := 1 if node.is_class(kind) else 0
	for child in node.get_children(): count += count_kind(child, kind)
	return count

static func valid_materials(node: Node3D) -> bool:
	for info in Buildings.mesh_instances(node):
		var instance: MeshInstance3D = info.node
		for index in instance.mesh.get_surface_count():
			var material := instance.get_active_material(index)
			if not material is StandardMaterial3D or material.shading_mode == BaseMaterial3D.SHADING_MODE_UNSHADED: return false
	return true

static func valid_normals(node: Node3D) -> bool:
	var wrong := 0
	var examined := 0
	for info in Buildings.mesh_instances(node):
		var instance: MeshInstance3D = info.node
		for surface_index in instance.mesh.get_surface_count():
			var arrays: Array = instance.mesh.surface_get_arrays(surface_index)
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
			var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX] is PackedInt32Array else PackedInt32Array()
			if normals.size() != vertices.size(): return false
			for index in vertices.size():
				if not vertices[index].is_finite() or not normals[index].is_finite() or normals[index].length() < 0.9: return false
			var count := indices.size() if not indices.is_empty() else vertices.size()
			for start in range(0, count, 3):
				var a: int = indices[start] if not indices.is_empty() else start
				var b: int = indices[start + 1] if not indices.is_empty() else start + 1
				var c: int = indices[start + 2] if not indices.is_empty() else start + 2
				var cross := (vertices[b] - vertices[a]).cross(vertices[c] - vertices[a])
				if cross.length_squared() < 0.0000000001: continue
				examined += 1
				if cross.dot(normals[a] + normals[b] + normals[c]) > 0.00001: wrong += 1
	return examined > 0 and wrong == 0

static func check_grounding(node: Node3D, samples: Array) -> bool:
	var triangles: Array = []
	for info in Buildings.mesh_instances(node):
		var instance: MeshInstance3D = info.node
		if str(instance.name) != "TerrainMesh" and not str(instance.name).ends_with("_platform_triangles"): continue
		for surface_index in instance.mesh.get_surface_count():
			var arrays: Array = instance.mesh.surface_get_arrays(surface_index)
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX] is PackedInt32Array else PackedInt32Array()
			var count := indices.size() if not indices.is_empty() else vertices.size()
			for start in range(0, count, 3):
				var triangle: Array[Vector3] = []
				for corner in 3: triangle.append(info.transform * vertices[indices[start + corner] if not indices.is_empty() else start + corner])
				triangles.append(triangle)
	if samples.size() != 80 or triangles.is_empty(): return false
	for sample in samples:
		var point := vector(sample.position)
		var top := -INF
		for triangle in triangles:
			var a := Vector2(triangle[0].x, triangle[0].z)
			var b := Vector2(triangle[1].x, triangle[1].z)
			var c := Vector2(triangle[2].x, triangle[2].z)
			var flat := Vector2(point.x, point.z)
			var determinant := (b - a).cross(c - a)
			if absf(determinant) < 0.0000001: continue
			var w_b := (flat - a).cross(c - a) / determinant
			var w_c := (b - a).cross(flat - a) / determinant
			var w_a := 1.0 - w_b - w_c
			if minf(w_a, minf(w_b, w_c)) < -0.0001: continue
			top = maxf(top, triangle[0].y * w_a + triangle[1].y * w_b + triangle[2].y * w_c)
		if not is_finite(top) or absf(top - point.y) > float(sample.tolerance): return false
	return true

static func glb_json(path: String) -> Dictionary:
	var bytes := FileAccess.get_file_as_bytes(path)
	if bytes.size() < 20 or bytes.decode_u32(0) != 0x46546c67 or bytes.decode_u32(4) != 2 or bytes.decode_u32(8) != bytes.size(): return {}
	var length := bytes.decode_u32(12)
	if bytes.decode_u32(16) != 0x4e4f534a or int(length) + 20 > bytes.size(): return {}
	var parsed: Variant = JSON.parse_string(bytes.slice(20, 20 + length).get_string_from_utf8())
	return parsed if parsed is Dictionary else {}

static func has_vertex_colors(document: Dictionary) -> bool:
	for mesh in document.get("meshes", []):
		for primitive in mesh.get("primitives", []):
			if primitive.get("attributes", {}).has("COLOR_0"): return true
	return false
