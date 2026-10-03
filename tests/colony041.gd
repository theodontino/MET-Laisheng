extends Node
class_name Colony041
## Repeatable, real-game-state fixture for 0.4.1 colony art and native frame probes.
## Run the app with --colony041; screenshots and JSON go to MET_EVIDENCE_DIR.

const Game = preload("res://core/game.gd")
const Hex = preload("res://core/hex.gd")
const Store = preload("res://core/storage.gd")
const Paths = preload("res://tests/paths.gd")
const SEED := 410041
const TOWN_SIZE := 12
const NEW_BASES: Array[String] = ["neighborhood", "homestead", "workshop"]
const GROWTH_PATHS := {
	"homestead": [
		{"tier2": "residence", "tier3": "advanced_residence"},
		{"tier2": "agriculture", "tier3": "modern_agriculture"}
	],
	"neighborhood": [
		{"tier2": "public_center", "tier3": "commercial_center"},
		{"tier2": "medical_clinic", "tier3": "hospital"},
		{"tier2": "primary_school", "tier3": "secondary_school"},
		{"tier2": "public_center", "tier3": "garden"}
	],
	"workshop": [
		{"tier2": "yard", "tier3": "freight"},
		{"tier2": "extraction", "tier3": "material_factory"},
		{"tier2": "power_station", "tier3": "power_plant"}
	]
}

var app: Control
var checks: Array[Dictionary] = []
var captures: Array[Dictionary] = []
var native_performance: Array[Dictionary] = []
var errors: Array[String] = []
var mobile_quality_requested := false

func start(target_app: Control) -> void:
	app = target_app
	call_deferred("_run")

func _run() -> void:
	await _frames(12)
	app.test_mode = true
	app.ignore_save = true
	mobile_quality_requested = "--mobile-quality" in OS.get_cmdline_user_args()
	app.overlay.hide()
	if app.desk.is_open():
		app.desk.close()
	app.mode = "inspect"
	app.game = Game.new()
	var err := Colony041.start_town(app.game, SEED)
	_check(err == "", "start deterministic River Valley fixture: " + err)
	if err != "":
		_finish()
		return
	app.game.s.settings.user_paused = true
	app.display_pending_tick = -1
	app.refresh()
	app.map_view.focus_community()
	if mobile_quality_requested:
		await _frames(2)
		_check_mobile_quality_branch()
	await _capture("01-initial-town", "official two-building starter town")

	var colony := Colony041.build_town(app.game, TOWN_SIZE)
	_check(str(colony.get("error", "")) == "", "build 12-cell plain-terrain colony fixture")
	if not colony.has("paths"):
		_finish()
		return
	_check(_town_has_plain_terrain(app.game, colony.tiles), "all 12 fixture buildings share plain terrain")
	var all_ids: Array[String] = colony.ids
	var paths: Dictionary = colony.paths
	var stage_rows: Array[Dictionary] = []
	await _present_stage("tier1", 0, all_ids, colony.tiles, stage_rows)
	for growth_tier in [2, 3]:
		var growth_error := Colony041.grow_to(app.game, paths, growth_tier)
		_check(growth_error == "", "advance actual project definitions to tier %d: %s" % [growth_tier, growth_error])
		if growth_error != "":
			break
		await _present_stage("tier%d" % growth_tier, growth_tier - 1, all_ids, colony.tiles, stage_rows)

	# Record a same-state camera check: drawing and camera movement must not alter
	# RNG, projects, residents, inventory, or other saved simulation state.
	var camera := await _native_probe("mature-12-cell", false)
	native_performance.append(camera)

	# Performance includes the official starter and a real full-land occupancy
	# fixture; it does not use mock records or a second renderer.
	app.game = Game.new()
	err = Colony041.start_town(app.game, SEED)
	app.display_pending_tick = -1
	app.refresh()
	app.map_view.focus_community()
	native_performance.append(await _native_probe("starter", false))

	app.game = Game.new()
	err = Colony041.start_town(app.game, SEED)
	var full_map := Colony041.fill_land(app.game)
	_check(str(full_map.get("error", "")) == "", "fill all non-water map cells for fullmap probe")
	app.display_pending_tick = -1
	app.refresh()
	app.map_view.focus_all()
	native_performance.append(await _native_probe("fullmap", true))
	_check(bool(native_performance[-1].get("camera_preserved_state", false)), "fullmap camera stages preserve simulation state")
	_finish({"town_stages": stage_rows, "native_performance": native_performance})

func _present_stage(name: String, stage_index: int, ids: Array[String], tiles: Array[String], rows: Array[Dictionary]) -> void:
	app.display_pending_tick = -1
	app.game.refresh_metrics()
	app.game.Progress.after_commit(app.game, 0.0)
	app.refresh()
	app.map_view.focus_community()
	await _capture("%02d-%s" % [stage_index + 2, name], "%d-cell colony, growth tier %d" % [ids.size(), stage_index + 1])
	var definitions: Array[String] = []
	var tiers: Dictionary = {"1": 0, "2": 0, "3": 0}
	var terrain_profile: Array[Dictionary] = []
	app.map_view._ensure_elevation()
	for tile in tiles:
		terrain_profile.append({
			"tile": tile,
			"terrain": str(app.game.s.cells[tile].terrain),
			"elevation": app.map_view.elevation_at(app.map_view.flat_point(tile))
		})
	for id in ids:
		var project: Dictionary = app.game.s.projects[id]
		definitions.append(str(project.def))
		var key := str(project.tier)
		tiers[key] = int(tiers.get(key, 0)) + 1
	var row := {"name": name, "cells": tiles.duplicate(), "terrain_profile": terrain_profile, "building_ids": ids.duplicate(), "definitions": definitions, "tier_counts": tiers, "city_stage": int(app.game.s.stage)}
	rows.append(row)
	_check(app.game.validate_state(app.game.s) == "", name + " state passes current schema validation")
	_check(ids.size() == TOWN_SIZE, name + " keeps 12 building identities across growth")

func _native_probe(label: String, full_map: bool) -> Dictionary:
	if full_map:
		app.map_view.focus_all()
	else:
		app.map_view.focus_community()
	app.game.s.settings.user_paused = true
	app.map_view.mode = "inspect"
	app.map_view.hovered = ""
	await _frames(60)
	var state_before_camera := Store.encode(app.game.s)
	var warm_stats: Dictionary = app.map_view.render_stats.duplicate(true)
	var static_ms: Array[float] = await _render_intervals(120)
	var static_stats := _stats_delta(warm_stats, app.map_view.render_stats)
	var pan_ms: Array[float] = []
	var pan_cpu: Array[float] = []
	for i in 36:
		var started := Time.get_ticks_usec()
		app.map_view.pan += Vector2(4.0 if i % 2 == 0 else -4.0, 2.0 if i % 3 == 0 else -2.0)
		app.map_view.invalidate_camera()
		pan_cpu.append(float(Time.get_ticks_usec() - started) / 1000.0)
		pan_ms.append(await _next_render_interval())
	var zoom_ms: Array[float] = []
	var zoom_cpu: Array[float] = []
	for i in 36:
		var target_zoom := 0.96 if i % 2 == 0 else 1.0
		var started := Time.get_ticks_usec()
		app.map_view.set_zoom(target_zoom, Vector2(720.0 + float(i % 5) * 3.0, 450.0))
		zoom_cpu.append(float(Time.get_ticks_usec() - started) / 1000.0)
		zoom_ms.append(await _next_render_interval())
	var pan_zoom_stats := _stats_delta(warm_stats, app.map_view.render_stats)
	var state_after_camera := Store.encode(app.game.s)
	var camera_unchanged := state_after_camera == state_before_camera
	_check(camera_unchanged, label + " static, pan, and zoom presentation do not mutate game state")
	_check(int(pan_zoom_stats.get("terrain_mesh_builds", 0)) == 0, label + " pan and zoom reuse the terrain mesh")
	_check(int(pan_zoom_stats.get("fabric_derives", 0)) == 0, label + " pan and zoom do not rebuild town fabric")

	var tick_hours := float(app.game.c.balance.tick_hours)
	var simulation_cpu: Array[float] = []
	var simulation_frame_ms: Array[float] = []
	var snapshot_cpu: Array[float] = []
	var simulation_render_stats: Dictionary = app.map_view.render_stats.duplicate(true)
	app.game.s.settings.user_paused = false
	for i in 12:
		var started := Time.get_ticks_usec()
		app.game.advance_online(tick_hours)
		simulation_cpu.append(float(Time.get_ticks_usec() - started) / 1000.0)
		app.display_pending_tick = -1
		started = Time.get_ticks_usec()
		app.refresh()
		snapshot_cpu.append(float(Time.get_ticks_usec() - started) / 1000.0)
		simulation_frame_ms.append(await _next_render_interval())
	app.game.s.settings.user_paused = true
	var row := {
		"name": label,
		"playable_buildings": app.game.playable_ids().size(),
		"map_cells": app.game.s.cells.size(),
		"occupied_land_cells": app.game.playable_ids().size(),
		"rendering_method": RenderingServer.get_current_rendering_method(),
		"world_render_target_pixels": _world_target_size(),
		"world_render_scale": app.map_view.world_render_scale,
		"effects_remain_full_resolution": app.map_view.effects.get_parent() == app.map_view,
		"max_fps_setting": Engine.max_fps,
		"display_refresh_rate_hz": DisplayServer.screen_get_refresh_rate(),
		"real_window_frame_interval_ms": {
			"static": _summarize(static_ms),
			"pan": _summarize(pan_ms),
			"zoom": _summarize(zoom_ms),
			"post_draw_wait_after_tick_refresh": _summarize(simulation_frame_ms)
		},
		"camera_handler_cpu_ms": {"pan": _summarize(pan_cpu), "zoom": _summarize(zoom_cpu)},
		"simulation_cpu_ms_per_tick": _summarize(simulation_cpu),
		"snapshot_and_ui_refresh_cpu_ms": _summarize(snapshot_cpu),
		"simulation_render_counter_delta": _stats_delta(simulation_render_stats, app.map_view.render_stats),
		"memory_monitors": _memory_monitors(),
		"static_render_counter_delta": static_stats,
		"pan_zoom_render_counter_delta": pan_zoom_stats,
		"camera_preserved_state": camera_unchanged,
		"method": "Native rendered window; static, pan, and zoom intervals are between RenderingServer post-draw signals. The post_draw_wait_after_tick_refresh interval starts only after advance_online and app.refresh return, so it excludes synchronous simulation and snapshot/UI CPU, which are reported separately."
	}
	return row

func _render_intervals(count: int) -> Array[float]:
	var result: Array[float] = []
	for _i in count:
		result.append(await _next_render_interval())
	return result

func _next_render_interval() -> float:
	var started := Time.get_ticks_usec()
	await RenderingServer.frame_post_draw
	return float(Time.get_ticks_usec() - started) / 1000.0

func _stats_delta(before: Dictionary, after: Dictionary) -> Dictionary:
	var result := {}
	for key in after:
		result[str(key)] = int(after[key]) - int(before.get(key, 0))
	return result

func _memory_monitors() -> Dictionary:
	return {
		"memory_static_bytes_lower_bound_not_rss": Performance.get_monitor(Performance.MEMORY_STATIC),
		"render_texture_mem_used_bytes": Performance.get_monitor(Performance.RENDER_TEXTURE_MEM_USED),
		"scope": "Godot monitors; MEMORY_STATIC is not process RSS."
	}

func _hardware_context() -> Dictionary:
	var viewport_texture := get_viewport().get_texture()
	var render_size := viewport_texture.get_size() if viewport_texture != null else Vector2i.ZERO
	return {
		"os_name": OS.get_name(),
		"os_version": OS.get_version(),
		"processor": OS.get_processor_name(),
		"display_server": DisplayServer.get_name(),
		"renderer": RenderingServer.get_video_adapter_name(),
		"renderer_vendor": RenderingServer.get_video_adapter_vendor(),
		"rendering_method": RenderingServer.get_current_rendering_method(),
		"display_refresh_rate_hz": DisplayServer.screen_get_refresh_rate(),
		"screen_pixels": _size_pair(DisplayServer.screen_get_size()),
		"window_pixels": _size_pair(DisplayServer.window_get_size()),
		"viewport_pixels": _size_pair(render_size),
		"logical_viewport": _size_pair(Vector2i(get_viewport().get_visible_rect().size)),
		"mobile_quality_requested": mobile_quality_requested,
		"world_render_target_pixels": _world_target_size(),
		"effects_remain_full_resolution": app != null and app.map_view != null and app.map_view.effects.get_parent() == app.map_view
	}

func _size_pair(size: Vector2i) -> Array[int]:
	return [size.x, size.y]

func _world_target_size() -> Array[int]:
	if app == null or app.map_view == null or app.map_view.world_viewport == null:
		return []
	return _size_pair(app.map_view.world_viewport.size)

func _check_mobile_quality_branch() -> void:
	var view = app.map_view
	var world_size: Vector2i = view.world_viewport.size if view.world_viewport != null else Vector2i.ZERO
	var short_side := mini(world_size.x, world_size.y)
	_check(view.world_viewport != null and view.world_texture != null and short_side == 720, "mobile-quality native branch maps the world to a 720-pixel short side")
	_check(view.ground.get_parent() == view.world_viewport and view.buildings.get_parent() == view.world_viewport, "mobile-quality world geometry renders inside its scaled viewport")
	_check(view.effects.get_parent() == view, "mobile-quality overlays stay on the full-resolution UI layer")

func _capture(name: String, description: String) -> void:
	app.map_view.hovered = ""
	await _frames(12)
	await RenderingServer.frame_post_draw
	var path := Paths.file("colony041-" + name + ".png")
	var image := get_viewport().get_texture().get_image()
	var save_error := image.save_png(path)
	_check(save_error == OK and image.get_width() >= 640 and image.get_height() >= 320, "render screenshot " + name)
	captures.append({
		"name": name,
		"description": description,
		"path": path,
		"viewport": [image.get_width(), image.get_height()],
		"camera": {
			"zoom": app.map_view.zoom,
			"pan": [app.map_view.pan.x, app.map_view.pan.y],
			"available_rect": [app.map_view.available_rect.position.x, app.map_view.available_rect.position.y, app.map_view.available_rect.size.x, app.map_view.available_rect.size.y]
		}
	})
	print("COLONY041_CAPTURE=", path)

func _town_has_plain_terrain(game: Object, tiles: Array[String]) -> bool:
	for tile in tiles:
		if str(game.s.cells[tile].terrain) != "plain":
			return false
		var id := str(game.s.cells[tile].building)
		if id == "" or not game.s.projects.has(id):
			return false
	return true

func _summarize(values: Array[float]) -> Dictionary:
	if values.is_empty():
		return {"n": 0, "p50_ms": 0.0, "p95_ms": 0.0, "p99_ms": 0.0, "max_ms": 0.0, "mean_ms": 0.0}
	var sorted: Array[float] = values.duplicate()
	sorted.sort()
	var total := 0.0
	for value in values:
		total += value
	return {
		"n": values.size(),
		"p50_ms": sorted[mini(sorted.size() - 1, maxi(0, int(ceil(values.size() * 0.50)) - 1))],
		"p95_ms": sorted[mini(sorted.size() - 1, maxi(0, int(ceil(values.size() * 0.95)) - 1))],
		"p99_ms": sorted[mini(sorted.size() - 1, maxi(0, int(ceil(values.size() * 0.99)) - 1))],
		"max_ms": sorted[-1],
		"mean_ms": total / float(values.size())
	}

func _frames(count: int) -> void:
	for _i in count:
		await get_tree().process_frame

func _check(ok: bool, label: String) -> void:
	checks.append({"pass": ok, "name": label})
	print(("COLONY041 PASS " if ok else "COLONY041 FAIL ") + label)
	if not ok:
		errors.append(label)

func _finish(extra: Dictionary = {}) -> void:
	var report := {
		"passed": errors.is_empty(), "errors": errors, "checks": checks,
		"captures": captures, "town_stages": extra.get("town_stages", []),
		"native_performance": extra.get("native_performance", native_performance),
		"hardware_context": _hardware_context(),
		"memory_monitors": _memory_monitors(),
		"godot": Engine.get_version_info().get("string", "unknown"),
		"method": "Game entities and valid tier transitions rendered in the native application window; not a physical input or mobile-device test."
	}
	var file := FileAccess.open(Paths.file("colony041.json"), FileAccess.WRITE)
	if file == null:
		push_error("Cannot write colony041 report")
		get_tree().quit(2)
		return
	file.store_string(JSON.stringify(report, "  "))
	file.close()
	print("COLONY041 ", checks.size() - errors.size(), "/", checks.size())
	print("COLONY041_RESULT=" + Paths.file("colony041.json"))
	get_tree().quit(1 if not errors.is_empty() else 0)

static func start_town(game: Object, seed_value: int = SEED) -> String:
	game.new_game(seed_value, Time.get_unix_time_from_system())
	var err := str(game.select_block(0))
	if err != "":
		return err
	if game.s.origin_choices.is_empty():
		return "origin fixture produced no choices"
	return str(game.choose_origin(str(game.s.origin_choices[0])))

static func build_town(game: Object, target_count: int = TOWN_SIZE) -> Dictionary:
	var tiles := _plain_layout(game, target_count)
	if tiles.size() != target_count:
		return {"error": "expected %d same-terrain cells; found %d" % [target_count, tiles.size()]}
	var by_tile := {}
	for id in game.playable_ids():
		by_tile[str(game.s.projects[id].tile)] = str(id)
	var path_cursors: Dictionary = {"homestead": 0, "neighborhood": 0, "workshop": 0}
	var paths := {}
	var ids: Array[String] = []
	var new_base_index := 0
	for tile in tiles:
		var id := str(by_tile.get(tile, ""))
		if id == "":
			var base := NEW_BASES[new_base_index % NEW_BASES.size()]
			new_base_index += 1
			var created: Dictionary = game.create_building(base, tile, "commons", true)
			id = str(created.get("id", ""))
			if id == "":
				return {"error": "game rejected fixture building at " + tile}
		var base_def := str(game.s.projects[id].def)
		var family := str(game.c.projects[base_def].family_id)
		var options: Array = GROWTH_PATHS.get(family, [])
		if options.is_empty():
			return {"error": "no tier path for " + base_def}
		var cursor := int(path_cursors.get(family, 0))
		var path: Dictionary = options[cursor % options.size()]
		path_cursors[family] = cursor + 1
		paths[id] = path.duplicate(true)
		ids.append(id)
	if ids.size() != target_count:
		return {"error": "fixture created %d instead of %d entities" % [ids.size(), target_count]}
	game.refresh_metrics()
	game.Population.rehouse(game)
	game.refresh_metrics()
	game.Progress.after_commit(game, 0.0)
	var error := str(game.validate_state(game.s))
	if error != "":
		return {"error": "tier1 fixture invalid: " + error}
	return {"ids": ids, "tiles": tiles, "paths": paths, "terrain": "plain"}

static func grow_to(game: Object, paths: Dictionary, target_tier: int) -> String:
	if target_tier < 2 or target_tier > 3:
		return "growth tier must be 2 or 3"
	for id_value in paths:
		var id := str(id_value)
		if not game.s.projects.has(id):
			return "missing building identity " + id
		var project: Dictionary = game.s.projects[id]
		while int(project.get("tier", 1)) < target_tier:
			var next_tier := int(project.tier) + 1
			var key := "tier%d" % next_tier
			var next_def := str(paths[id].get(key, ""))
			if next_def == "" or not game.Development.choices(game, id).has(next_def):
				return "invalid tier %d growth for %s" % [next_tier, id]
			# Exercise the game's actual identity-preserving completion transition.
			# Construction costs/work are intentionally omitted for this visual fixture.
			project.upgrade_choice = next_def
			project.upgrade_job = {"work": 0.0, "total": 1.0, "material": 0.0, "cp": true}
			game.Development.complete(game, id)
	game.refresh_metrics()
	game.Population.rehouse(game)
	game.refresh_metrics()
	game.Progress.after_commit(game, 0.0)
	return str(game.validate_state(game.s))

static func fill_land(game: Object) -> Dictionary:
	var cells: Array[String] = []
	for key in game.s.cells:
		if str(game.s.cells[key].terrain) != "water":
			cells.append(str(key))
	cells.sort_custom(func(a: String, b: String) -> bool:
		var da := Hex.distance("0,1", a)
		var db := Hex.distance("0,1", b)
		return da < db if da != db else a < b
	)
	var by_tile := {}
	for id in game.playable_ids():
		by_tile[str(game.s.projects[id].tile)] = str(id)
	var bases: Array = game.c.v040.progression.tier1
	for tile in cells:
		if by_tile.has(tile):
			continue
		var index: int = game.playable_ids().size() % bases.size()
		game.create_building(str(bases[index]), tile, "commons", true)
	game.refresh_metrics()
	game.Population.rehouse(game)
	game.refresh_metrics()
	var error := str(game.validate_state(game.s))
	if error != "":
		return {"error": error}
	return {"buildings": game.playable_ids().size(), "land_cells": cells.size(), "cells": game.s.cells.size()}

static func _plain_layout(game: Object, count: int) -> Array[String]:
	var result: Array[String] = []
	# Preserve both official starter positions in each growth screenshot.
	for key in ["0,1", "0,0"]:
		if game.s.cells.has(key) and str(game.s.cells[key].terrain) == "plain":
			result.append(key)
	var free: Array[String] = []
	for key in game.s.cells:
		var tile: Dictionary = game.s.cells[key]
		if str(tile.terrain) != "plain" or str(tile.building) != "":
			continue
		free.append(str(key))
	free.sort_custom(func(a: String, b: String) -> bool:
		var da := Hex.distance("0,1", a)
		var db := Hex.distance("0,1", b)
		return da < db if da != db else a < b
	)
	for tile in free:
		if result.size() >= count:
			break
		result.append(tile)
	return result
