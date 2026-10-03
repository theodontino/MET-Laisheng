extends SceneTree
## Data, geometry, grounding and isolation regression. Headless evidence is not
## visual/GPU acceptance; native screenshots and performance are separate.
const Fixture = preload("res://tests/fixtures/hero_valley.gd")
const Snapshot = preload("res://ui/world3d/world_visual_snapshot.gd")
const Terrain = preload("res://ui/world3d/terrain/terrain_builder.gd")
const River = preload("res://ui/world3d/terrain/river_builder.gd")
const Buildings = preload("res://ui/world3d/city/building_renderer.gd")
const Fabric = preload("res://ui/world3d/city/fabric_renderer.gd")
const Foliage = preload("res://ui/world3d/foliage/foliage_scatter.gd")
const EnvironmentState = preload("res://ui/world3d/environment/environment_state.gd")
const Debug = preload("res://ui/world3d/debug/visual_debug.gd")
const Store = preload("res://core/storage.gd")
const Paths = preload("res://tests/paths.gd")
const SOURCE_PATHS := [
	"res://tests/living_world.gd", "res://tests/fixtures/hero_valley.gd",
	"res://ui/world3d/world_visual_snapshot.gd", "res://ui/world3d/world_view_3d.gd",
	"res://ui/world3d/city/building_renderer.gd", "res://ui/world3d/city/fabric_renderer.gd",
	"res://ui/world3d/foliage/foliage_scatter.gd", "res://ui/world3d/terrain/terrain_builder.gd",
	"res://ui/world3d/terrain/river_builder.gd", "res://ui/world3d/terrain/terrain_material.gdshader",
	"res://ui/world3d/terrain/river_material.gdshader", "res://content/presentation3d/asset_manifest.json",
	"res://ui/world3d/environment/environment_controller.gd", "res://ui/world3d/environment/environment_state.gd",
	"res://ui/world3d/debug/visual_debug.gd", "res://ui/world3d/hero_scene.gd",
]
var checks: Array[Dictionary] = []
var grounding: Array[Dictionary] = []
var geometry_grounding: Array[Dictionary] = []


func _initialize() -> void:
	call_deferred("run")


func check(condition: bool, name: String) -> void:
	checks.append({"name": name, "pass": condition})
	print(("PASS " if condition else "FAIL ") + name)


func run() -> void:
	var source_before := source_hashes()
	var g = Fixture.make_game()
	var live_before := live_digest(g)
	var registry := Buildings.load_manifest()
	var registry_before := Snapshot.digest(registry)
	var display: Dictionary = Fixture.committed(g)
	var display_before := Snapshot.digest(display)
	var definitions_before := Snapshot.digest(g.c.projects)
	var rejected := display.duplicate(true)
	rejected.forecast = true
	check(not make_snapshot(rejected, g, registry).get("ok", false), "forecast display is rejected")
	rejected = display.duplicate(true)
	rejected.erase("forecast")
	check(not make_snapshot(rejected, g, registry).get("ok", false), "implicit forecast status is rejected")
	rejected = display.duplicate(true)
	rejected.erase("source_phase")
	check(not make_snapshot(rejected, g, registry).get("ok", false), "missing committed source phase is rejected")
	rejected = display.duplicate(true)
	rejected.source_phase = "planner_forecast"
	check(not make_snapshot(rejected, g, registry).get("ok", false), "noncommitted source phase is rejected")
	rejected = display.duplicate(true)
	rejected.tiles = {}
	check(not make_snapshot(rejected, g, registry).get("ok", false), "empty committed terrain is rejected")
	var snapshot := make_snapshot(display, g, registry)
	var repeated := make_snapshot(display, g, registry)
	check(snapshot.get("ok", false), "explicit committed display is accepted")
	check(snapshot.tiles.size() == 37, "fixed Hero Valley contains 37 real logical tiles")
	check(snapshot.signature == repeated.signature, "same committed data and visual seed give identical detached snapshot")
	check(snapshot.source_phase == "committed_tick" and not snapshot.forecast, "visual snapshot retains committed provenance")
	# Legal future registry metadata need not use identity scale or a zero pivot.
	# An offset source ground plane must still land at the requested anchor.
	var transform_entry := {"scale": [1.7, 0.65, 1.2], "pivot": [0.3, 2.4, -0.2],
		"bounds": {"min": [-0.7, 1.1, -0.6], "max": [0.9, 4.0, 0.8]}}
	var ground_anchor := Vector3(3.0, 0.85, -2.0)
	var pivot_ground := Vector3(0.3, 1.1, -0.2)
	var registered_transform := Buildings.grounded_asset_transform(transform_entry, ground_anchor)
	check((registered_transform * pivot_ground).is_equal_approx(ground_anchor), "nonunit scale and nonzero XYZ pivot preserve horizontal anchor and align bounds minimum Y")
	var rotated_transform := Buildings.grounded_asset_transform(transform_entry, ground_anchor, 0.73, 0.86)
	check((rotated_transform * pivot_ground).is_equal_approx(ground_anchor), "instanced foliage applies pivot after local scale and heading rotation while keeping its bottom grounded")
	var local_delta := Vector3(0.42, 0.0, -0.31)
	var expected_delta := Basis(Vector3.UP, 0.73) * (local_delta * Vector3(1.7, 0.65, 1.2) * 0.86)
	check(((rotated_transform * (pivot_ground + local_delta)) - ground_anchor).is_equal_approx(expected_delta), "nonuniform registered scale precedes upright instance rotation")
	var raised_pivot_entry := transform_entry.duplicate(true)
	raised_pivot_entry.pivot[1] = 8.0
	check(Buildings.grounded_asset_transform(raised_pivot_entry, ground_anchor).is_equal_approx(registered_transform), "vertical source pivot cannot change final ground-contact height")
	var mutated := make_snapshot(display, g, registry)
	var tile_key := str(mutated.tiles.keys()[0])
	mutated.tiles[tile_key].terrain = "alias-test-only"
	var project_id := str(g.playable_ids()[0])
	mutated.projects[project_id].inventory.food += 123.0
	var group_id := str(mutated.groups.keys()[0])
	mutated.groups[group_id].count += 5.0
	mutated.compiled.stats[project_id].efficiency = 9.0
	mutated.metrics.utilization[project_id].utilization = 0.0
	mutated.definitions[str(mutated.projects[project_id].def)].stats.efficiency = 9.0
	mutated.asset_registry.assets[0].tags.append("alias-test-only")
	check(live_digest(g) == live_before, "mutating detached nested tiles projects groups metrics and compiled data cannot alias live city")
	check(Snapshot.digest(g.c.projects) == definitions_before, "detached building definitions do not alias gameplay catalog")
	check(Snapshot.digest(registry) == registry_before, "detached asset metadata does not alias caller registry")
	check(Snapshot.digest(display) == display_before, "snapshot creation and detached mutation preserve input display")
	var snapshot_before := Snapshot.digest(snapshot)
	var terrain_a := Terrain.build(snapshot)
	var terrain_b := Terrain.build(repeated)
	check(terrain_a.stats.terrain_triangles > 0 and terrain_a.stats.terrain_vertices > 0, "terrain has genuine populated 3D triangle geometry")
	check(terrain_a.stats.individual_hex_surfaces == 0 and terrain_a.stats.terrain_mesh_instances == 1, "terrain is one continuous lattice rather than individual hex surfaces")
	check(mesh_digest(terrain_a.node) == mesh_digest(terrain_b.node), "repeated terrain mesh vertices normals colors and topology are deterministic")
	check(canvas_count(terrain_a.node) == 0, "terrain subtree contains no CanvasItem substitute")
	var buildings_a := Buildings.build(snapshot)
	var buildings_b := Buildings.build(repeated)
	check(buildings_a.stats.buildings == 4 and buildings_a.stats.mesh_instances == 4, "four registered buildings instantiate real GLB meshes")
	check(buildings_a.stats.missing_assets.is_empty(), "all Hero Valley runtime building assets resolve")
	check(buildings_a.stats.offmap_sources_skipped == 2, "municipal and homeless offmap sources are not invented as visual buildings")
	check(buildings_a.signature == buildings_b.signature, "same snapshot gives identical building identities scales and grounding")
	check(canvas_count(buildings_a.node) == 0 and buildings_a.stats.triangles > 0, "building geometry uses actual meshes without PNG billboards")
	var hill_residence := false
	for site_value in snapshot.foundation_field.sites:
		var site: Dictionary = site_value
		var entry := Buildings.entry_for_definition(registry, str(site.def))
		if entry.is_empty(): continue
		var footprint: Array = entry.footprint.xz_bounds
		var center: Vector2 = site.center
		var corners := [Vector2(float(footprint[0]), -float(footprint[1])), Vector2(float(footprint[0]), -float(footprint[3])),
			Vector2(float(footprint[2]), -float(footprint[1])), Vector2(float(footprint[2]), -float(footprint[3]))]
		var maximum_error := 0.0
		for corner in corners:
			maximum_error = maxf(maximum_error, absf(Terrain.height_at(snapshot, center + corner * 64.0) - float(site.platform_height)))
		grounding.append({"project_id": site.project_id, "definition": site.def, "tile": site.tile,
			"platform_height_legacy": site.platform_height, "maximum_corner_error_legacy": maximum_error})
		check(maximum_error < 0.001, "registered ground-footprint corners lie on platform: " + str(site.def))
		if str(site.def) == "residence" and str(snapshot.tiles[site.tile].terrain) == "hill":
			hill_residence = true
			check(float(site.platform_height) > 20.0, "residence grounding test actually covers a raised hill platform")
		var rectangle: Rect2 = site.runtime_footprint
		var boundary := Vector2(rectangle.end.x, rectangle.get_center().y)
		var jump := absf(Terrain.height_at(snapshot, boundary - Vector2(0.01, 0.0)) - Terrain.height_at(snapshot, boundary + Vector2(0.01, 0.0)))
		check(jump < 0.05, "runtime foundation edge rejoins surrounding grade continuously: " + str(site.def))
	check(hill_residence, "fixture includes supported residence on real hill terrain")
	check_actual_ground(snapshot, buildings_a.node, terrain_a.node, "default registry")
	var transformed_registry := registry.duplicate(true)
	var transformed_entry := Buildings.entry_for_definition(transformed_registry, "homestead")
	transformed_entry.scale = [1.12, 0.83, 0.91]
	transformed_entry.pivot = [0.11, 0.20, -0.15]
	var transformed_snapshot := make_snapshot(display, g, transformed_registry)
	var transformed_buildings := Buildings.build(transformed_snapshot)
	var transformed_terrain := Terrain.build(transformed_snapshot)
	check_actual_ground(transformed_snapshot, transformed_buildings.node, transformed_terrain.node, "scaled offset-pivot registry")
	var transformed_site: Dictionary = {}
	for site in transformed_snapshot.foundation_field.sites:
		if str(site.def) == "homestead": transformed_site = site; break
	var transformed_scale := Vector3(1.12, 0.83, 0.91)
	var transformed_pivot := Vector3(0.11, 0.20, -0.15)
	var transformed_footprint: Array = transformed_entry.footprint.xz_bounds
	var transformed_rect_correct := not transformed_site.is_empty()
	for x in [float(transformed_footprint[0]), float(transformed_footprint[2])]:
		for z in [float(transformed_footprint[1]), float(transformed_footprint[3])]:
			var runtime_corner := (Vector3(x, 0.0, z) - transformed_pivot) * transformed_scale
			var legacy_corner: Vector2 = transformed_site.center + Vector2(runtime_corner.x, -runtime_corner.z) * 64.0
			transformed_rect_correct = transformed_rect_correct and transformed_site.runtime_footprint.has_point(legacy_corner)
			transformed_rect_correct = transformed_rect_correct and absf(Terrain.height_at(transformed_snapshot, legacy_corner) - float(transformed_site.platform_height)) < 0.001
	check(transformed_rect_correct, "legal building scale and pivot propagate through snapshot footprint resizing and analytic grounding")
	var fabric_a := Fabric.build(snapshot)
	var fabric_b := Fabric.build(repeated)
	check(fabric_a.stats.lanes > 0 and fabric_a.stats.lanes <= snapshot.fabric.connectors.size(), "at least one lane comes from an actual committed Fabric connection")
	check(fabric_a.signature == fabric_b.signature and mesh_digest(fabric_a.node) == mesh_digest(fabric_b.node), "actual Fabric placements and geometry are deterministic")
	check(canvas_count(fabric_a.node) == 0, "urban connections are real 3D geometry")
	var river_a := River.build(snapshot)
	var river_b := River.build(repeated)
	check(river_a.stats.river_mesh_instances == 1 and river_a.stats.river_triangles > 0, "one genuine river ribbon spans the valley")
	check(mesh_digest(river_a.node) == mesh_digest(river_b.node), "continuous river geometry is deterministic")
	check(canvas_count(river_a.node) == 0, "river subtree contains no 2D water substitute")
	var river_mesh: Mesh = river_a.node.get_child(0).mesh
	var river_arrays: Array = river_mesh.surface_get_arrays(0)
	var river_samples := 0
	var uncovered := 0
	var perimeter := Terrain.domain(snapshot)
	for index in range(0, snapshot.river_path.size(), 8):
		var point: Vector2 = snapshot.river_path[index]
		if not Geometry2D.is_point_in_polygon(point, perimeter): continue
		river_samples += 1
		if not mesh_covers(river_arrays, point / 64.0): uncovered += 1
	check(river_samples >= 10 and uncovered == 0, "indexed river triangles cover the sampled continuous centerline without per-hex gaps")
	var debug := Debug.new()
	debug.configure(snapshot)
	var overlay_arrays: Array = debug.grid.mesh.surface_get_arrays(0)
	var wet_overlay_samples := 0
	var submerged_overlay_samples := 0
	for vertex in overlay_arrays[Mesh.ARRAY_VERTEX]:
		var point := Vector2(vertex.x, -vertex.z) * 64.0
		if Terrain.river_distance(snapshot, point) <= 29.0:
			wet_overlay_samples += 1
			if vertex.y <= River.WATER_HEIGHT: submerged_overlay_samples += 1
	check(wet_overlay_samples > 0 and submerged_overlay_samples == 0, "actual analysis mesh remains visible above the continuous water surface")
	check(not debug.grid.visible, "logical Hex overlay is hidden in the normal landscape")
	debug.free()
	var foliage_a := Foliage.build(snapshot)
	var foliage_b := Foliage.build(repeated)
	check(foliage_a.stats.trees > 0 and foliage_a.stats.missing_assets.is_empty(), "one registered tree species populates the valley")
	check(foliage_a.signature == foliage_b.signature, "same visual seed gives identical tree positions rotations and scales")
	check(foliage_a.transforms == foliage_b.transforms, "GPU instance transforms are deterministic")
	var shifted_tree_snapshot := snapshot.duplicate(true)
	var shifted_tree_entry := Buildings.asset_entry(shifted_tree_snapshot.asset_registry, Foliage.TREE_ID)
	shifted_tree_entry.scale = [1.15, 0.90, 0.75]
	shifted_tree_entry.pivot = [0.12, 0.27, -0.09]
	var foliage_shifted := Foliage.build(shifted_tree_snapshot)
	var shifted_pivot_ground := Vector3(0.12, float(shifted_tree_entry.bounds.min[1]), -0.09)
	var shifted_grounded: bool = foliage_shifted.transforms.size() == foliage_shifted.placements.size() and not foliage_shifted.transforms.is_empty()
	for index in foliage_shifted.transforms.size():
		var placement: Dictionary = foliage_shifted.placements[index]
		var expected_anchor := Buildings.world_position(placement.legacy_position, float(placement.height_legacy))
		shifted_grounded = shifted_grounded and (foliage_shifted.transforms[index] * shifted_pivot_ground).is_equal_approx(expected_anchor)
	check(shifted_grounded, "actual instanced tree path grounds every legal scaled offset-pivot runtime instance")
	check(foliage_a.node.get_child_count() == foliage_a.stats.batches, "forest creates shared material batches rather than per-tree scene nodes")
	var batches_valid := true
	for batch in foliage_a.node.get_children():
		if not batch is MultiMeshInstance3D: batches_valid = false; continue
		batches_valid = batches_valid and batch.get_child_count() == 0 and batch.multimesh.instance_count == foliage_a.stats.trees and batch.multimesh.mesh != null
	check(batches_valid and foliage_a.stats.batches > 0, "every forest batch uses a populated MultiMesh and shared mesh")
	var changed_seed := snapshot.duplicate(true)
	changed_seed.visual_seed = int(snapshot.visual_seed) + 1
	var foliage_changed := Foliage.build(changed_seed)
	check(foliage_changed.signature != foliage_a.signature, "changing only visual seed changes presentation scatter")
	var environment := EnvironmentState.fixed_daylight()
	check(environment.has_all(["world_time", "sun_state", "cloudiness", "fog_density", "humidity", "wetness", "wind", "temperature_band"]), "fixed EnvironmentState exposes every reserved visual parameter")
	check(environment == EnvironmentState.fixed_daylight(), "V1 environment remains fixed without an implicit time or weather simulation")
	check(Snapshot.digest(snapshot) == snapshot_before, "all renderer builds leave detached source data unchanged")
	check(live_digest(g) == live_before, "terrain river buildings fabric and forest preserve live simulation wallets entities RNG compiled data and revision")
	check(Snapshot.digest(display) == display_before and Snapshot.digest(registry) == registry_before, "all geometry generation preserves caller display and registry")
	var source_after := source_hashes()
	check(source_before == source_after, "relevant source and runtime GLB fingerprints stay unchanged during suite")
	var failures := checks.filter(func(row): return not row["pass"]).size()
	var output := {"suite": "living_world_headless", "passed": checks.size() - failures, "failed": failures,
		"checks": checks, "headless_only": true, "snapshot_signature": snapshot.signature,
		"world_seed": Fixture.WORLD_SEED, "visual_seed": Fixture.VISUAL_SEED,
		"stats": {"terrain": terrain_a.stats, "river": river_a.stats, "buildings": buildings_a.stats,
			"fabric": fabric_a.stats, "foliage": foliage_a.stats}, "grounding": grounding,
		"geometry_grounding": geometry_grounding,
		"river_centerline_samples": river_samples, "river_uncovered_samples": uncovered,
		"source_before": source_before, "source_after": source_after, "source_unchanged": source_before == source_after,
		"live_state_unchanged": live_digest(g) == live_before}
	FileAccess.open(Paths.file("living_world.json"), FileAccess.WRITE).store_string(JSON.stringify(output, "\t", true, true))
	for result in [terrain_a, terrain_b, buildings_a, buildings_b, fabric_a, fabric_b, river_a, river_b, foliage_a, foliage_b, foliage_changed, foliage_shifted, transformed_buildings, transformed_terrain]:
		result.node.free()
	print("LIVING WORLD %d passed, %d failed" % [checks.size() - failures, failures])
	quit(1 if failures else 0)


static func make_snapshot(display: Dictionary, g, registry: Dictionary) -> Dictionary:
	return Snapshot.from_display(display, g.c.projects, Fixture.WORLD_SEED, Fixture.VISUAL_SEED, registry)


static func live_digest(g) -> String:
	return (Store.encode(g.s) + Snapshot.digest(g.compiled) + str(g.revision)).sha256_text()


static func source_hashes() -> Dictionary:
	var result := {}
	for path in SOURCE_PATHS:
		result[path] = FileAccess.get_sha256(path)
	var manifest := Buildings.load_manifest()
	for asset in manifest.get("assets", []):
		var path := "res://" + str(asset.runtime_path).trim_prefix("res://")
		result[path] = FileAccess.get_sha256(path)
	return result


static func mesh_digest(node: Node) -> String:
	var data: Array = []
	for info in Buildings.mesh_instances(node):
		var instance: MeshInstance3D = info.node
		var arrays: Array = []
		for surface_index in instance.mesh.get_surface_count(): arrays.append(instance.mesh.surface_get_arrays(surface_index))
		data.append({"transform": info.transform, "arrays": arrays})
	return var_to_bytes(data).hex_encode().sha256_text()


static func canvas_count(node: Node) -> int:
	var result := 1 if node is CanvasItem else 0
	for child in node.get_children(): result += canvas_count(child)
	return result


static func mesh_covers(arrays: Array, point: Vector2) -> bool:
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
	for start in range(0, indices.size(), 3):
		var triangle := PackedVector2Array()
		for offset in 3:
			var vertex := vertices[indices[start + offset]]
			triangle.append(Vector2(vertex.x, -vertex.z))
		if Geometry2D.is_point_in_polygon(point, triangle): return true
		for edge in 3:
			if point.distance_to(Geometry2D.get_closest_point_to_segment(point, triangle[edge], triangle[(edge + 1) % 3])) < 0.0001: return true
	return false


func check_actual_ground(snapshot: Dictionary, building_node: Node3D, terrain_node: Node3D, label: String) -> void:
	var ground_triangles := actual_ground_triangles(terrain_node)
	check(not ground_triangles.is_empty(), "real terrain and platform triangle samples exist: " + label)
	var projects_to_mesh: Dictionary = {}
	for info in Buildings.mesh_instances(building_node):
		var ancestor: Node = info.node
		while ancestor != null and not ancestor.has_meta("project_id"): ancestor = ancestor.get_parent()
		if ancestor != null: projects_to_mesh[str(ancestor.get_meta("project_id"))] = info
	for site_value in snapshot.foundation_field.sites:
		var site: Dictionary = site_value
		var entry := Buildings.entry_for_definition(snapshot.asset_registry, str(site.def))
		if entry.is_empty(): continue
		check(projects_to_mesh.has(str(site.project_id)), "grounding probes target an instantiated GLB mesh: " + label + " / " + str(site.def))
		if not projects_to_mesh.has(str(site.project_id)): continue
		var info: Dictionary = projects_to_mesh[str(site.project_id)]
		var transform: Transform3D = info.transform
		var footprint: Array = entry.footprint.xz_bounds
		var minimum_y := float(entry.bounds.min[1])
		var corners := [Vector3(float(footprint[0]), minimum_y, float(footprint[1])),
			Vector3(float(footprint[0]), minimum_y, float(footprint[3])),
			Vector3(float(footprint[2]), minimum_y, float(footprint[3])),
			Vector3(float(footprint[2]), minimum_y, float(footprint[1]))]
		var points: Array[Vector3] = []
		for edge in 4:
			points.append(transform * corners[edge])
			for step in [0.2, 0.4, 0.6, 0.8]:
				points.append(transform * corners[edge].lerp(corners[(edge + 1) % 4], float(step)))
		var maximum_ground_difference := 0.0
		var maximum_transform_difference := 0.0
		var missing_samples := 0
		for point in points:
			var top_height := top_ground_height(ground_triangles, Vector2(point.x, point.z))
			maximum_transform_difference = maxf(maximum_transform_difference, absf(point.y - float(site.platform_height) / 64.0))
			if not is_finite(top_height): missing_samples += 1; continue
			maximum_ground_difference = maxf(maximum_ground_difference, absf(top_height - point.y))
		geometry_grounding.append({"registry_case": label, "project_id": site.project_id,
			"definition": site.def, "sample_count": points.size(), "missing_ground_samples": missing_samples,
			"maximum_glb_transform_error_world": maximum_transform_difference,
			"maximum_mesh_ground_difference_world": maximum_ground_difference,
			"tolerance_world": 0.01, "sampled_meshes": "TerrainMesh and platform tops; retaining walls excluded"})
		check(maximum_transform_difference < 0.0001, "actual instantiated GLB bounds minimum Y lands on platform: " + label + " / " + str(site.def))
		check(missing_samples == 0 and maximum_ground_difference <= 0.01,
			"real mesh surface supports four footprint corners and sixteen edge points: " + label + " / " + str(site.def))


static func actual_ground_triangles(node: Node3D) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for info in Buildings.mesh_instances(node):
		var instance: MeshInstance3D = info.node
		# Walls and cut skirts are not ground beneath a building footprint.
		if str(instance.name) != "TerrainMesh" and not str(instance.name).ends_with("_platform_triangles"): continue
		var transform: Transform3D = info.transform
		for surface_index in instance.mesh.get_surface_count():
			var arrays: Array = instance.mesh.surface_get_arrays(surface_index)
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX] is PackedInt32Array else PackedInt32Array()
			var count := indices.size() if not indices.is_empty() else vertices.size()
			for start in range(0, count, 3):
				var triangle: Array[Vector3] = []
				for offset in 3:
					triangle.append(transform * vertices[indices[start + offset] if not indices.is_empty() else start + offset])
				var a := Vector2(triangle[0].x, triangle[0].z)
				var b := Vector2(triangle[1].x, triangle[1].z)
				var c := Vector2(triangle[2].x, triangle[2].z)
				var area := (b - a).cross(c - a)
				if absf(area) < 0.000000001: continue
				var bounds := Rect2(a, Vector2.ZERO).expand(b).expand(c).grow(0.00001)
				result.append({"vertices": triangle, "a": a, "b": b, "c": c, "inverse_area": 1.0 / area, "bounds": bounds})
	return result


static func top_ground_height(triangles: Array[Dictionary], point: Vector2) -> float:
	var top := -INF
	for triangle in triangles:
		var bounds: Rect2 = triangle.bounds
		if not bounds.has_point(point): continue
		var a: Vector2 = triangle.a
		var b: Vector2 = triangle.b
		var c: Vector2 = triangle.c
		var weight_b := (point - a).cross(c - a) * float(triangle.inverse_area)
		var weight_c := (b - a).cross(point - a) * float(triangle.inverse_area)
		var weight_a := 1.0 - weight_b - weight_c
		if minf(weight_a, minf(weight_b, weight_c)) < -0.00001: continue
		var vertices: Array = triangle.vertices
		top = maxf(top, weight_a * vertices[0].y + weight_b * vertices[1].y + weight_c * vertices[2].y)
	return top
