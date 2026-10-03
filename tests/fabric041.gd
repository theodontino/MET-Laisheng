extends SceneTree
## Focused checks for deterministic, snapshot-only neighbor connectors.
const Game = preload("res://core/game.gd")
const Store = preload("res://core/storage.gd")
const Fabric = preload("res://ui/map/fabric040.gd")
const Foundation = preload("res://ui/map/foundation041.gd")
const Connections = preload("res://ui/map/connections041.gd")
const MapView = preload("res://ui/map_view.gd")
const Hex = preload("res://core/hex.gd")

class ViewStub extends RefCounted:
	var height_step := 0.0
	var ridge_midpoint := Vector2.ZERO
	var ridge_direction := Vector2.RIGHT
	func set_ridge(a: Vector2, b: Vector2, height: float) -> void:
		height_step = height
		ridge_midpoint = a.lerp(b, 0.5)
		ridge_direction = (b - a).normalized()
	func flat_point(key: String) -> Vector2:
		var axial := Hex.coords(key)
		return Vector2(sqrt(3.0) * 64.0 * (float(axial.x) + float(axial.y) * 0.5), 96.0 * float(axial.y))
	func elevation_at(point: Vector2) -> float:
		return height_step if (point - ridge_midpoint).dot(ridge_direction) > 0.0 else 0.0
	func surface_point(point: Vector2) -> Vector2:
		return Vector2(point.x, point.y * 0.661 - elevation_at(point) * 0.751)
	func project_flat(point: Vector2) -> Vector2:
		return surface_point(point)
	func geometry_radius() -> float:
		return 64.0
	func geometry_version() -> String:
		return "fixture:" + str(height_step)
	func radius() -> float:
		return 64.0

var checks := 0
var failures := 0

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var game := Game.new()
	game.new_game(7040, 1000000.0)
	game.select_block(0)
	game.choose_origin(game.s.origin_choices[0])
	var residential_pair := _free_land_pair(game, [])
	var residential_a: Dictionary = game.create_building("residence", residential_pair[0], "commons", true)
	var residential_b: Dictionary = game.create_building("advanced_residence", residential_pair[1], "commons", true)
	var used := [residential_pair[0], residential_pair[1]]
	var industrial_pair := _free_land_pair(game, used)
	game.create_building("extraction", industrial_pair[0], "steel", true)
	game.create_building("manufacturing", industrial_pair[1], "steel", true)
	var state_before := Store.encode(game.s)
	var first: Dictionary = Fabric.derive(game)
	var repeated: Dictionary = Fabric.derive(game)
	_check(Store.encode(game.s) == state_before, "derive does not change RNG, world state, or saved entities")
	_check(JSON.stringify(first.get("connectors", [])) == JSON.stringify(repeated.get("connectors", [])), "same snapshot produces stable connector specs")
	_check(_contains_kind(first, "residential_"), "adjacent completed residences receive a visible residential connector")
	_check(_contains_kind(first, "industrial_"), "adjacent completed industrial buildings receive a visible industrial connector")
	var renderer_view := ViewStub.new()
	var rendered := Connections.items(renderer_view, first.get("connectors", []))
	_check(rendered.size() == first.get("connectors", []).size(), "renderer builds depth-sortable retained items")
	var rendered_keys := rendered.map(func(item): return str(item.key))
	_check(rendered_keys == rendered_keys.duplicate() and _is_depth_sorted(rendered), "renderer items are sorted by world depth")
	var sample_spec: Dictionary = first.connectors[0]
	var sample_a := renderer_view.flat_point(str(sample_spec.a))
	var sample_b := renderer_view.flat_point(str(sample_spec.b))
	renderer_view.set_ridge(sample_a, sample_b, 10.0)
	var stepped := Connections.items(renderer_view, [sample_spec])
	_check(not stepped.is_empty() and str(stepped[0].height_mode) == "stepped", "moderate elevation differences use grounded step geometry")
	renderer_view.set_ridge(sample_a, sample_b, 32.0)
	var cliff := Connections.items(renderer_view, [sample_spec])
	_check(cliff.is_empty(), "large elevation differences omit the connector instead of spanning a cliff")
	var first_project_id := str(game.playable_ids()[0])
	var first_project: Dictionary = game.s.projects[first_project_id]
	var first_center := renderer_view.flat_point(str(first_project.tile))
	renderer_view.set_ridge(first_center - Vector2(100.0, 0.0), first_center + Vector2(100.0, 0.0), 10.0)
	var foundation_field: Dictionary = Foundation.derive(game, {"projects": game.s.projects, "tiles": game.s.cells}, Callable(renderer_view, "elevation_at"))
	var foundation_site: Dictionary = _site_for(foundation_field, first_project_id)
	var foundation_height := Foundation.height_at(foundation_field, first_center, Callable(renderer_view, "elevation_at"))
	var foundation_mesh: Array[Dictionary] = Foundation.foundation_items(foundation_field, Callable(renderer_view, "elevation_at"))
	var foundation_item: Dictionary = _foundation_for(foundation_mesh, first_project_id)
	_check(Store.encode(game.s) == state_before, "foundation grading and mesh data leave saved entities and RNG unchanged")
	_check(not foundation_site.is_empty() and is_equal_approx(foundation_height, float(foundation_site.get("platform_height", -1000.0))), "the occupied footprint core is leveled to its natural center grade")
	_check(not foundation_item.is_empty() and foundation_item.get("platform_triangles", PackedVector3Array()).size() >= 72, "foundation renderer receives an asset-sized, triangulated footprint")
	_check(not foundation_item.is_empty() and not foundation_item.get("retaining_walls", PackedVector3Array()).is_empty(), "steep footprint edges retain a visible retaining wall")
	_check(not foundation_site.is_empty() and float(foundation_site.get("blend_radius", 0.0)) >= 28.0, "foundation transitions extend beyond a sharp footprint edge")
	var max_wall_height := 0.0
	var wall_vertices: PackedVector3Array = foundation_item.get("retaining_walls", PackedVector3Array())
	for index in range(0, wall_vertices.size() - 3, 6):max_wall_height = maxf(max_wall_height, absf(wall_vertices[index].z - wall_vertices[index + 1].z))
	_check(max_wall_height <= 12.01, "retaining faces remain short where the ground falls away")
	var landscape_projects: Dictionary = game.s.projects.duplicate(true)
	landscape_projects[first_project_id].def = "garden"
	var landscape_field: Dictionary = Foundation.derive(game, {"projects": landscape_projects, "tiles": game.s.cells}, Callable(renderer_view, "elevation_at"))
	var landscape_site: Dictionary = _site_for(landscape_field, first_project_id)
	var landscape_items: Array[Dictionary] = Foundation.foundation_items(landscape_field, Callable(renderer_view, "elevation_at"))
	_check(bool(landscape_site.get("terrain_following", false)) and _foundation_for(landscape_items, first_project_id).is_empty(), "green landscape buildings follow the native ground without a pedestal")
	var water_site_found := false
	for site in foundation_field.get("sites", []):
		if Foundation._is_water(game.s.cells.get(str(site.get("tile", "")), {})):water_site_found = true
	_check(not water_site_found, "water tiles never receive a visual building foundation")
	var channel_tiles: Dictionary = game.s.cells.duplicate(true)
	channel_tiles[str(residential_b.tile)].terrain = "channel"
	var channel_snapshot := {"projects": game.s.projects, "groups": game.s.groups, "tiles": channel_tiles, "metrics": game.s.metrics, "compiled": game.compiled}
	var channel_fabric: Dictionary = Fabric.derive(game, channel_snapshot)
	_check(not _has_project_pair(channel_fabric, str(residential_a.id), str(residential_b.id)), "snapshot channels block visual connectors across water")
	var unfinished_id := str(residential_b.id)
	var original_status := str(game.s.projects[unfinished_id].get("status", "running"))
	game.s.projects[unfinished_id].status = "construction"
	var unfinished := Fabric.derive(game)
	_check(not _has_project_pair(unfinished, str(residential_a.id), unfinished_id), "a building under construction does not grow a visual connector")
	game.s.projects[unfinished_id].status = original_status
	await _check_map_view_cache_and_picking(game, first)
	print("FABRIC041 ", checks, " checks, ", failures, " failed")
	quit(1 if failures > 0 else 0)

func _check_map_view_cache_and_picking(game, snapshot: Dictionary) -> void:
	var map_view = MapView.new()
	map_view.game = game
	map_view.display_shot = snapshot
	var mobile_test := "--mobile-quality" in OS.get_cmdline_user_args()
	var test_size := Vector2(1536.0, 1024.0) if mobile_test else Vector2(1024.0, 720.0)
	map_view.size = test_size
	map_view.available_rect = Rect2(Vector2.ZERO, map_view.size)
	map_view._focus_pending = false
	root.size = Vector2i(test_size)
	root.add_child(map_view)
	await process_frame
	map_view._process(1.0 / 60.0)
	await process_frame
	var ready_stats: Dictionary = map_view.render_stats.duplicate(true)
	if mobile_test:
		var low_size: Vector2i = map_view.world_viewport.size if map_view.world_viewport != null else Vector2i.ZERO
		_check(map_view.world_viewport != null and map_view.world_texture != null and map_view.effects.get_parent() == map_view and mini(low_size.x, low_size.y) <= 720 and mini(low_size.x, low_size.y) >= 719, "mobile quality renders world at a 720-pixel short side while effects remain in the sharp UI layer")
	var connector: Dictionary = map_view.connector_render_items[0] if not map_view.connector_render_items.is_empty() else {}
	var picked_project := ""
	if not connector.is_empty():
		var platform: PackedVector2Array = connector.get("platform", PackedVector2Array())
		var center := Vector2.ZERO
		for point in platform:center += point
		center /= maxf(1.0, float(platform.size()))
		var screen_point: Vector2 = map_view.size * 0.5 + map_view.pan + center * map_view.zoom
		picked_project = map_view.connection_hit(screen_point)
	_check(not connector.is_empty() and picked_project in [str(connector.get("project_a", "")), str(connector.get("project_b", ""))], "clicking a visible connection resolves one of its related buildings")
	var signature_scans := int(map_view.render_stats.get("foundation_signature_scans", 0))
	var probe_tile := str(game.s.projects[str(game.playable_ids()[0])].tile)
	for _i in 80:map_view.world_point(probe_tile)
	_check(int(map_view.render_stats.get("foundation_signature_scans", 0)) == signature_scans, "repeated world-point projection reuses the foundation revision guard")
	var zoom_anchor := Vector2(560.0, 380.0)
	map_view.set_zoom(map_view.zoom * 1.08, zoom_anchor)
	map_view.pan += Vector2(13.0, -7.0)
	map_view.invalidate_camera()
	map_view._process(1.0 / 60.0)
	var moved_stats: Dictionary = map_view.render_stats
	_check(int(moved_stats.get("terrain_mesh_builds", 0)) == int(ready_stats.get("terrain_mesh_builds", 0)), "pan and zoom keep the static terrain mesh")
	_check(int(moved_stats.get("ground_draws", 0)) == int(ready_stats.get("ground_draws", 0)) and int(moved_stats.get("structure_draws", 0)) == int(ready_stats.get("structure_draws", 0)), "pan and zoom do not redraw static ground or structures")
	_check(int(moved_stats.get("fabric_derives", 0)) == int(ready_stats.get("fabric_derives", 0)) and int(moved_stats.get("connector_builds", 0)) == int(ready_stats.get("connector_builds", 0)), "pan and zoom do not regenerate city fabric or connector geometry")
	var before_tick_stats: Dictionary = map_view.render_stats.duplicate(true)
	var original_revision := int(game.revision)
	var original_tick := int(game.s.simulation_tick)
	var next_tick_snapshot: Dictionary = snapshot.duplicate()
	next_tick_snapshot.merge({"projects": game.s.projects, "groups": game.s.groups, "tiles": game.s.cells, "metrics": game.s.metrics, "compiled": game.compiled, "snapshot_id": "fabric041_tick:%d" % (original_tick + 1)}, true)
	game.revision = original_revision + 1
	game.s.simulation_tick = original_tick + 1
	map_view.display_shot = next_tick_snapshot
	map_view.invalidate()
	map_view._process(1.0 / 60.0)
	await process_frame
	var after_tick_stats: Dictionary = map_view.render_stats.duplicate(true)
	_check(int(after_tick_stats.get("fabric_derives", 0)) == int(before_tick_stats.get("fabric_derives", 0)) + 1, "a committed tick refreshes snapshot-derived city fabric")
	_check(int(after_tick_stats.get("terrain_mesh_builds", 0)) == int(before_tick_stats.get("terrain_mesh_builds", 0)) and int(after_tick_stats.get("foundation_builds", 0)) == int(before_tick_stats.get("foundation_builds", 0)) and int(after_tick_stats.get("ground_draws", 0)) == int(before_tick_stats.get("ground_draws", 0)) and int(after_tick_stats.get("structure_draws", 0)) == int(before_tick_stats.get("structure_draws", 0)) and int(after_tick_stats.get("connector_builds", 0)) == int(before_tick_stats.get("connector_builds", 0)), "a tick with unchanged visible state does not rebuild or redraw static map layers")
	game.revision = original_revision
	game.s.simulation_tick = original_tick
	map_view.display_shot = snapshot
	map_view.invalidate()
	map_view._process(1.0 / 60.0)
	await process_frame
	var changed_project_id := str(game.playable_ids()[0])
	var changed_project: Dictionary = game.s.projects[changed_project_id]
	var saved_revision := int(game.revision)
	var saved_definition := str(changed_project.get("def", ""))
	var replacement_definition := "advanced_residence" if game.c.projects.has("advanced_residence") and saved_definition != "advanced_residence" else "residence"
	var saved_foundation_key := str(map_view.foundation_key)
	var saved_footprint: PackedVector2Array = map_view.ground_footprints[str(changed_project.tile)].duplicate()
	changed_project.def = replacement_definition
	game.revision = saved_revision + 1
	map_view._ensure_foundations()
	var refreshed_footprint: PackedVector2Array = map_view.ground_footprints[str(changed_project.tile)]
	_check(map_view.foundation_key != saved_foundation_key and saved_footprint != refreshed_footprint, "a changed building footprint refreshes terrain and tile-picking polygons together")
	var refreshed_center: Vector2 = map_view.point(str(changed_project.tile))
	_check(map_view.hit(refreshed_center) == str(changed_project.tile), "building-center picking remains aligned after foundation regeneration")
	changed_project.def = saved_definition
	game.revision = saved_revision
	map_view.invalidate()
	map_view._ensure_foundations()
	map_view.queue_free()
	await process_frame

func _free_land_pair(game, excluded: Array) -> Array:
	var keys: Array = game.s.cells.keys()
	keys.sort()
	for key_value in keys:
		var key := str(key_value)
		if key in excluded or not _land(game.s.cells[key]):
			continue
		for neighbor in Hex.neighbors(key):
			if game.s.cells.has(neighbor) and not neighbor in excluded and _land(game.s.cells[neighbor]):
				return [key, neighbor]
	return []

func _land(cell: Dictionary) -> bool:
	return str(cell.get("terrain", "plain")).to_lower() not in ["water", "sea", "ocean"] and not "tile.water" in cell.get("traits", []) and str(cell.get("building", "")) == ""

func _contains_kind(result: Dictionary, prefix: String) -> bool:
	for item in result.get("connectors", []):
		if str(item.get("kind", "")).begins_with(prefix):
			return true
	return false

func _has_project_pair(result: Dictionary, a: String, b: String) -> bool:
	for item in result.get("connectors", []):
		if (str(item.get("project_a", "")) == a and str(item.get("project_b", "")) == b) or (str(item.get("project_a", "")) == b and str(item.get("project_b", "")) == a):
			return true
	return false

func _site_for(field: Dictionary, project_id: String) -> Dictionary:
	for site in field.get("sites", []):
		if str(site.get("project_id", "")) == project_id:return site
	return {}

func _foundation_for(items: Array[Dictionary], project_id: String) -> Dictionary:
	for item in items:
		if str(item.get("project_id", "")) == project_id:return item
	return {}

func _is_depth_sorted(items: Array) -> bool:
	for index in range(1, items.size()):
		if float(items[index - 1].depth) > float(items[index].depth):
			return false
	return true

func _check(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures += 1
	print(("PASS " if condition else "FAIL ") + label)
