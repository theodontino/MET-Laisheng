extends SceneTree

## Headless map-performance probe. It uses the real Game, MapView, snapshot,
## terrain, structure and overlay paths; it does not mock map records.
##
## Run with:
##   Godot --headless --path . --script res://tests/performance041.gd -- --output=/tmp/perf.json
## The drag/zoom stages force a draw after each camera change. They are CPU
## callback timings, not an FPS or native-interaction acceptance test.

const Game = preload("res://core/game.gd")
const Hex = preload("res://core/hex.gd")
const MapView = preload("res://ui/map_view.gd")
const MapData = preload("res://ui/view_models/map_data.gd")
const Planner = preload("res://core/planning.gd")
const Colony = preload("res://tests/colony041.gd")

var map_view
var draw_samples: Dictionary = {"terrain": [], "structures": [], "overlays": []}
var output_path := ""

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--output="):
			output_path = arg.trim_prefix("--output=")
	if output_path.is_empty():
		output_path = "/tmp/performance041.json"

	root.size = Vector2i(1440, 900)
	var result := {
		"benchmark": "MET visual map performance 0.4.1",
		"godot": Engine.get_version_info().get("string", "unknown"),
		"mode": "headless CPU; not a native frame-rate claim",
		"memory_monitors": {
			"memory_static_bytes_lower_bound_not_rss": Performance.get_monitor(Performance.MEMORY_STATIC),
			"render_texture_mem_used_bytes": Performance.get_monitor(Performance.RENDER_TEXTURE_MEM_USED),
			"scope": "Godot monitors; MEMORY_STATIC is not process RSS."
		},
		"viewport": [1440, 900],
		"fixtures": []
	}
	var failures: Array[String] = []
	for fixture_name in ["starter", "fullmap"]:
		print("PERF fixture start: ", fixture_name)
		var fixture_result := await _run_fixture(str(fixture_name))
		print("PERF fixture done: ", fixture_name)
		if fixture_result.has("error"):
			failures.append(str(fixture_name) + ": " + str(fixture_result.error))
			push_error("PERF fixture failed: " + failures[-1])
		result.fixtures.append(fixture_result)
	result["passed"] = failures.is_empty()
	result["errors"] = failures
	var file := FileAccess.open(output_path, FileAccess.WRITE)
	if file == null:
		push_error("Cannot write performance result: " + output_path)
		quit(2)
		return
	file.store_string(JSON.stringify(result, "  "))
	file.close()
	print(JSON.stringify(result, "  "))
	print("PERFORMANCE041_RESULT=" + output_path)
	quit(0 if failures.is_empty() else 1)

func _run_fixture(fixture_name: String) -> Dictionary:
	var game := Game.new()
	var fixture_error := Colony.start_town(game, 410041)
	if fixture_error != "":
		return {"fixture": fixture_name, "error": fixture_error}
	if fixture_name == "fullmap":
		var full_map: Dictionary = Colony.fill_land(game)
		fixture_error = str(full_map.get("error", ""))
		if fixture_error != "":
			return {"fixture": fixture_name, "error": fixture_error}
	game.refresh_metrics()
	var ids: Array = game.playable_ids()
	var planner := Planner.new()
	print("PERF fixture built: ", fixture_name, " with ", ids.size(), " buildings")
	var snap_started := Time.get_ticks_usec()
	var snapshot: Dictionary = MapData.current(game, planner)
	var snapshot_ms := float(Time.get_ticks_usec() - snap_started) / 1000.0
	print("PERF snapshot complete: ", fixture_name, " in ", snapshot_ms, "ms")

	map_view = MapView.new()
	map_view.game = game
	map_view.display_shot = snapshot
	map_view.compact = false
	map_view.available_rect = Rect2(Vector2(16, 96), Vector2(1408, 700))
	map_view.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(map_view)
	await process_frame
	_instrument_draw_callbacks()
	draw_samples = {"terrain": [], "structures": [], "overlays": []}
	print("PERF map ready: ", fixture_name)

	var fixture := {
		"name": fixture_name,
		"buildings": ids.size(),
		"cells": game.s.cells.size(),
		"ready_buildings": ids.size(),
		"snapshot_initial_ms": snapshot_ms,
		"timing_mode": "headless CPU and SceneTree turnaround; not a native window frame rate",
		"memory_monitors": {
			"memory_static_bytes_lower_bound_not_rss": Performance.get_monitor(Performance.MEMORY_STATIC),
			"render_texture_mem_used_bytes": Performance.get_monitor(Performance.RENDER_TEXTURE_MEM_USED),
			"scope": "Godot monitors; MEMORY_STATIC is not process RSS."
		},
		"stages": []
	}
	# Cold render includes first focus, elevation cache, terrain mesh and sprites.
	var cold := await _frame_sample("cold_build", 1.0 / 60.0, true)
	print("PERF cold complete: ", fixture_name, " process=", cold.process_ms, "ms")
	cold["draw_cpu_ms"] = _drain_draw_samples()
	fixture.stages.append(cold)
	map_view._focus_pending = false
	var warm_stats: Dictionary = map_view.render_stats.duplicate(true)

	# Static samples retain the cache and leave the camera unchanged.
	var static_proc: Array[float] = []
	var static_wait: Array[float] = []
	for i in 120:
		var row := await _frame_sample("static", 1.0 / 60.0, false)
		static_proc.append(float(row.process_ms))
		static_wait.append(float(row.headless_frame_turnaround_ms))
	fixture.stages.append({
		"name": "static_120_frames",
		"process_cpu_ms": _summarize(static_proc),
		"headless_frame_turnaround_ms": _summarize(static_wait),
		"render_counter_delta": _stats_delta(warm_stats, map_view.render_stats),
		"draw_cpu_ms": _drain_draw_samples()
	})

	# Camera movement uses the real camera invalidation path and should retain the
	# warm terrain mesh and derived neighborhood fabric.
	map_view.mode = "inspect"
	map_view.set_zoom(0.28, Vector2(720, 450))
	var pan_cpu: Array[float] = []
	var pan_wait: Array[float] = []
	for i in 48:
		var started := Time.get_ticks_usec()
		map_view.pan += Vector2(7.0 if i % 2 == 0 else -7.0, 3.0 if i % 3 == 0 else -3.0)
		map_view.invalidate_camera()
		pan_cpu.append(float(Time.get_ticks_usec() - started) / 1000.0)
		var row := await _frame_sample("pan", 1.0 / 60.0, false)
		pan_wait.append(float(row.headless_frame_turnaround_ms))
	fixture.stages.append({
		"name": "pan_48_frames_at_min_zoom_0_28",
		"camera_handler_cpu_ms": _summarize(pan_cpu),
		"headless_frame_turnaround_ms": _summarize(pan_wait),
		"render_counter_delta": _stats_delta(warm_stats, map_view.render_stats),
		"draw_cpu_ms": _drain_draw_samples()
	})

	# Ordinary zoom path: repeated zoom-in/out around the pointer, each followed
	# by a redraw. This is a camera CPU probe, not a GUI event-latency claim.
	var zoom_proc: Array[float] = []
	var zoom_wait: Array[float] = []
	for i in 48:
		var scale := 0.96 if i % 2 == 0 else 1.0
		var started := Time.get_ticks_usec()
		map_view.set_zoom(scale, Vector2(690 + i * 3, 430))
		zoom_proc.append(float(Time.get_ticks_usec() - started) / 1000.0)
		var row := await _frame_sample("zoom", 1.0 / 60.0, false)
		zoom_wait.append(float(row.headless_frame_turnaround_ms))
	fixture.stages.append({
		"name": "zoom_48_frames_0_96_to_1_0",
		"camera_handler_cpu_ms": _summarize(zoom_proc),
		"headless_frame_turnaround_ms": _summarize(zoom_wait),
		"render_counter_delta": _stats_delta(warm_stats, map_view.render_stats),
		"draw_cpu_ms": _drain_draw_samples()
	})

	# Drive one real deterministic simulation tick per sample, then refresh the
	# committed display snapshot and visible map. This is explicitly not the
	# game's wall-clock tick cadence.
	var sim_cpu: Array[float] = []
	var snap_cpu: Array[float] = []
	var sim_map_proc: Array[float] = []
	var sim_wait: Array[float] = []
	var simulation_render_stats: Dictionary = map_view.render_stats.duplicate(true)
	var tick_hours := float(game.c.balance.tick_hours)
	game.s.settings.user_paused = false
	for i in 12:
		var started := Time.get_ticks_usec()
		game.advance_online(tick_hours)
		sim_cpu.append(float(Time.get_ticks_usec() - started) / 1000.0)
		started = Time.get_ticks_usec()
		snapshot = MapData.current(game, planner)
		snap_cpu.append(float(Time.get_ticks_usec() - started) / 1000.0)
		map_view.display_shot = snapshot
		map_view.invalidate()
		var row := await _frame_sample("active_sim", 1.0 / 60.0, true)
		sim_map_proc.append(float(row.process_ms))
		sim_wait.append(float(row.headless_frame_turnaround_ms))
	fixture.stages.append({
		"name": "active_sim_12_forced_ticks",
		"tick_hours_per_sample": tick_hours,
		"simulation_cpu_ms_per_tick": _summarize(sim_cpu),
		"snapshot_cpu_ms_after_tick": _summarize(snap_cpu),
		"map_process_cpu_ms": _summarize(sim_map_proc),
		"headless_frame_turnaround_ms": _summarize(sim_wait),
		"render_counter_delta": _stats_delta(simulation_render_stats, map_view.render_stats),
		"draw_cpu_ms": _drain_draw_samples()
	})

	map_view.queue_free()
	await process_frame
	map_view = null
	return fixture

func _frame_sample(_stage: String, delta: float, invalidate_before: bool) -> Dictionary:
	if invalidate_before:
		map_view.invalidate()
	var started := Time.get_ticks_usec()
	map_view._process(delta)
	var process_ms := float(Time.get_ticks_usec() - started) / 1000.0
	var wait_started := Time.get_ticks_usec()
	await process_frame
	var turnaround_ms := float(Time.get_ticks_usec() - wait_started) / 1000.0
	return {"process_ms": process_ms, "headless_frame_turnaround_ms": turnaround_ms}

func _instrument_draw_callbacks() -> void:
	for entry in [[map_view.ground, "_terrain", "terrain"], [map_view.buildings, "_structures", "structures"], [map_view.effects, "_overlays", "overlays"]]:
		var node: CanvasItem = entry[0]
		var method := str(entry[1])
		var label := str(entry[2])
		for connection in node.get_signal_connection_list("draw"):
			node.disconnect("draw", connection.callable)
		node.draw.connect(Callable(self, "_draw_" + label).bind(node))

func _draw_terrain(target: CanvasItem) -> void:
	var started := Time.get_ticks_usec()
	map_view._terrain(target)
	draw_samples.terrain.append(float(Time.get_ticks_usec() - started) / 1000.0)

func _draw_structures(target: CanvasItem) -> void:
	var started := Time.get_ticks_usec()
	map_view._structures(target)
	draw_samples.structures.append(float(Time.get_ticks_usec() - started) / 1000.0)

func _draw_overlays(target: CanvasItem) -> void:
	var started := Time.get_ticks_usec()
	map_view._overlays(target)
	draw_samples.overlays.append(float(Time.get_ticks_usec() - started) / 1000.0)

func _drain_draw_samples() -> Dictionary:
	var result := {}
	for key in draw_samples:
		result[key] = _summarize(draw_samples[key])
		draw_samples[key] = []
	return result

func _stats_delta(before: Dictionary, after: Dictionary) -> Dictionary:
	var result := {}
	for key in after:
		result[str(key)] = int(after[key]) - int(before.get(key, 0))
	return result

func _summarize(values: Array) -> Dictionary:
	if values.is_empty():
		return {"n": 0, "p50_ms": 0.0, "p95_ms": 0.0, "p99_ms": 0.0, "max_ms": 0.0, "mean_ms": 0.0}
	var sorted: Array = values.duplicate()
	sorted.sort()
	var total := 0.0
	for value in values:
		total += float(value)
	var p50 := float(sorted[mini(sorted.size() - 1, maxi(0, int(ceil(values.size() * 0.50)) - 1))])
	var p95 := float(sorted[mini(sorted.size() - 1, maxi(0, int(ceil(values.size() * 0.95)) - 1))])
	var p99 := float(sorted[mini(sorted.size() - 1, maxi(0, int(ceil(values.size() * 0.99)) - 1))])
	return {"n": values.size(), "p50_ms": p50, "p95_ms": p95, "p99_ms": p99, "max_ms": float(sorted[-1]), "mean_ms": total / values.size()}
