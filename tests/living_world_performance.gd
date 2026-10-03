extends SceneTree
## Native Forward+ throughput of the fixed detached Hero Valley world. This is
## a diagnostic, with no invented FPS acceptance threshold. Use --simulation-only
## with --headless for simulation costs without an initialized native renderer.
## Run one Godot process at a time; asset import must finish before measurement.
const Fixture = preload("res://tests/fixtures/hero_valley.gd")
const Snapshot = preload("res://ui/world3d/world_visual_snapshot.gd")
const World = preload("res://ui/world3d/world_view_3d.gd")
const Buildings = preload("res://ui/world3d/city/building_renderer.gd")
const Suite = preload("res://tests/living_world.gd")
const Paths = preload("res://tests/paths.gd")
const BASELINE_SIZE := Vector2i(1920, 1080)
const FIXED_ACTIVE_SECONDS := 1.0 / 60.0
var checks: Array[Dictionary] = []
var world
var fixture_game
var sample_frames := 72
var warmup_frames := 8
var simulation_calls := 120
var clock_ticks_per_second := 0
var simulation_only := false
var render_only := false
var run_started_usec := 0
var source_before: Dictionary = {}


func _initialize() -> void:
	call_deferred("run")


func check(condition: bool, name: String) -> void:
	checks.append({"name": name, "pass": condition})
	print(("PASS " if condition else "FAIL ") + name)


func run() -> void:
	run_started_usec = Time.get_ticks_usec()
	var args := OS.get_cmdline_user_args()
	simulation_only = "--simulation-only" in args
	render_only = "--render-only" in args
	for arg in args:
		if arg.begins_with("--sample-frames="):
			sample_frames = clampi(int(arg.get_slice("=", 1)), 60, 90)
		elif arg.begins_with("--simulation-calls="):
			simulation_calls = clampi(int(arg.get_slice("=", 1)), 60, 600)
	clock_ticks_per_second = linux_clock_tick_rate()
	source_before = source_hashes()
	var result := {
		"suite": "living_world_performance", "simulation_only": simulation_only,
		"render_only": render_only, "started_utc": Time.get_datetime_string_from_system(true),
		"godot": Engine.get_version_info(), "environment": environment_metadata(),
		"backend": backend_metadata(), "render_phases": [],
		"sampling": {"measured_frames_per_phase": sample_frames, "warmup_frames_per_phase": warmup_frames,
			"duration_policy": "A fixed 60-90 actual drawn frames, default 72. Actual duration is recorded, never predicted or enforced by throttling."},
		"timing_scope": "Synchronous real Game.advance_active monotonic call wall times and aggregate process CPU, in a separate headless process without a visual world." if simulation_only else "Consecutive frame_post_draw wall intervals after warmup, including main loop, driver, synchronization, scheduling and presentation pacing. The signal marks drawing submission, not GPU completion; these are not pure GPU timings.",
		"acceptance": "Data, renderer, fullscreen and isolation checks only; no hardware FPS threshold is asserted.",
		"limitations": ["Fixed 37-Hex visual fixture, not a large city or natural player route.",
			"World renderer only: the Hero toolbar and retained hidden 2D map are outside this measurement.",
			"Engine Vulkan device initialization before SceneTree._initialize is outside the world-initialization timer.",
			"Process CPU includes all Godot and software Vulkan worker threads and may exceed one core.",
			"Linux process CPU ticks are coarse; individual callback latency is monotonic wall time, not measured CPU time.",
			"Installed Mesa package versions identify environment packages; the active adapter and mapped ICD are recorded separately."]}
	check(not (simulation_only and render_only), "simulation-only and render-only modes are mutually exclusive")
	if has_failures():
		finish(result)
		return
	if simulation_only:
		check(DisplayServer.get_name() == "headless", "simulation-only uses a headless process")
		if DisplayServer.get_name() == "headless":
			RenderingServer.set_render_loop_enabled(false)
			result.simulation = simulation_probe("separate_headless_process")
	elif not has_failures():
		await render_probe(result)
		var shutdown_started := Time.get_ticks_usec()
		if world != null:
			world.set_process(false)
			world.input_enabled = false
			world.free()
			world = null
		RenderingServer.set_render_loop_enabled(false)
		var force_sync_called := false
		if RenderingServer.has_method("force_sync"):
			RenderingServer.call("force_sync")
			force_sync_called = true
		# Quiet main-loop turns after shutdown are outside every measured phase.
		# No frame_post_draw wait is used with drawing disabled.
		for index in 4: await process_frame
		result.renderer_shutdown = {"world_freed": world == null,
			"render_loop_enabled": RenderingServer.is_render_loop_enabled(),
			"force_sync_called": force_sync_called, "wall_ms": elapsed_ms(shutdown_started),
			"scope": "World disposal, rendering disable, optional CPU/GPU force_sync, and four quiet main-loop turns; excluded from simulation costs."}
		if not render_only and not has_failures():
			result.simulation = simulation_probe("after_world_freed_and_render_loop_disabled")
	finish(result)


func render_probe(result: Dictionary) -> void:
	check(DisplayServer.get_name() != "headless", "render phases require a native display")
	check(renderer_method() == "forward_plus", "render phases use actual Forward+")
	if has_failures(): return
	root.min_size = BASELINE_SIZE
	root.size = BASELINE_SIZE
	root.content_scale_size = BASELINE_SIZE
	root.mode = Window.MODE_FULLSCREEN
	for index in 2: await process_frame
	var visible_size := root.get_visible_rect().size
	result.display = {"window_mode": root.mode, "window_size": [root.size.x, root.size.y],
		"viewport_size": [visible_size.x, visible_size.y], "baseline": [1920, 1080],
		"vsync_mode": DisplayServer.window_get_vsync_mode(), "engine_max_fps": Engine.max_fps}
	check(root.mode == Window.MODE_FULLSCREEN and root.size == BASELINE_SIZE, "native fullscreen 1920x1080 window")
	check(visible_size == Vector2(BASELINE_SIZE), "native viewport uses 1920x1080")
	if has_failures(): return
	var fixture_started := Time.get_ticks_usec()
	fixture_game = Fixture.make_game()
	result.fixture_creation_wall_ms = elapsed_ms(fixture_started)
	var live_before := live_fingerprints(fixture_game)
	check(bool(fixture_game.s.settings.user_paused), "render fixture is paused")
	var snapshot_started := Time.get_ticks_usec()
	var snapshot := Snapshot.from_display(Fixture.committed(fixture_game), fixture_game.c.projects,
		Fixture.WORLD_SEED, Fixture.VISUAL_SEED, Buildings.load_manifest())
	result.snapshot_creation_wall_ms = elapsed_ms(snapshot_started)
	check(snapshot.get("ok", false) and snapshot.get("tiles", {}).size() == 37, "fixed detached snapshot has 37 real Hex")
	if has_failures(): return
	var snapshot_before := Snapshot.digest(snapshot)
	var init_cpu_before := cpu_snapshot()
	var init_started := Time.get_ticks_usec()
	world = World.new()
	root.add_child(world)
	world.input_enabled = false
	var built: bool = world.set_visual_snapshot(snapshot.duplicate(true))
	var sync_wall_ms := elapsed_ms(init_started)
	var sync_cpu := cpu_delta(init_cpu_before, cpu_snapshot(), sync_wall_ms / 1000.0)
	check(built, "world renderer builds only from a snapshot copy")
	if not built: return
	await RenderingServer.frame_post_draw
	var first_frame_wall_ms := elapsed_ms(init_started)
	result.renderer_initialization = {"synchronous_world_build_wall_ms": sync_wall_ms,
		"synchronous_process_cpu": sync_cpu, "through_first_draw_wall_ms": first_frame_wall_ms,
		"through_first_draw_process_cpu": cpu_delta(init_cpu_before, cpu_snapshot(), first_frame_wall_ms / 1000.0),
		"scope": "World.new, _ready, deep-copy snapshot, actual terrain/GLB/forest builds, and first frame_post_draw. Fixture/snapshot generation and engine startup are separate."}
	check(int(world.stats.buildings.buildings) == 4 and int(world.stats.foliage.trees) > 0,
		"renderer contains four actual GLB buildings and instanced trees")
	check(world.camera is Camera3D, "renderer uses an actual Camera3D")
	var build_before: int = world.build_count
	var world_snapshot_before := Snapshot.digest(world.visual_snapshot)
	result.fixture = {"id": "hero-valley-v1", "world_seed": Fixture.WORLD_SEED,
		"visual_seed": Fixture.VISUAL_SEED, "tiles": snapshot.tiles.size(),
		"snapshot_signature": snapshot.signature, "snapshot_digest": snapshot_before,
		"source_phase": snapshot.source_phase, "forecast": snapshot.forecast,
		"live_before": live_before, "stats": world.stats.duplicate(true)}
	if not has_failures():
		for kind in ["static", "pan", "zoom"]:
			result.render_phases.append(await render_phase(kind, live_before, build_before, world_snapshot_before))
	result.fixture.live_after = live_fingerprints(fixture_game)
	result.fixture.live_unchanged = result.fixture.live_after == live_before
	result.fixture.world_build_count_before = build_before
	result.fixture.world_build_count_after = world.build_count
	check(Snapshot.digest(snapshot) == snapshot_before, "render phases preserve caller snapshot copy")
	check(result.fixture.live_unchanged, "all render phases preserve complete live fixture state")


func render_phase(kind: String, live_before: Dictionary, build_before: int, snapshot_before: String) -> Dictionary:
	world.reset_camera()
	var started := 0
	for index in warmup_frames:
		await next_drawn_frame()
		started = Time.get_ticks_usec()
	# Anchor the first interval to the final warmup frame_post_draw, before the
	# sampling arrays and CPU snapshot are prepared.
	var frame_ms: Array[float] = []
	var cpu_before := cpu_snapshot()
	var previous := started
	var camera_before: Vector3 = world.camera.position
	var camera_size_before: float = world.camera.size
	var maximum_camera_displacement := 0.0
	var maximum_camera_size_change := 0.0
	for index in sample_frames:
		# Deterministic repeated camera motion, through the real renderer methods.
		# No manual _process calls; normal frame processing supplies smoothing.
		if kind == "pan":
			var direction := 1.0 if (index / 12) % 2 == 0 else -1.0
			world.pan_camera(Vector2(12.0, 4.0) * direction)
		elif kind == "zoom":
			world.zoom_camera(0.965 if (index / 12) % 2 == 0 else 1.0 / 0.965)
		await next_drawn_frame()
		var now := Time.get_ticks_usec()
		frame_ms.append(float(now - previous) / 1000.0)
		previous = now
		maximum_camera_displacement = maxf(maximum_camera_displacement, world.camera.position.distance_to(camera_before))
		maximum_camera_size_change = maxf(maximum_camera_size_change, absf(world.camera.size - camera_size_before))
	var wall_seconds := float(previous - started) / 1000000.0
	var phase_cpu := cpu_delta(cpu_before, cpu_snapshot(), wall_seconds)
	var live_after := live_fingerprints(fixture_game)
	var unchanged := live_after == live_before
	var build_unchanged: bool = world.build_count == build_before
	var snapshot_unchanged := Snapshot.digest(world.visual_snapshot) == snapshot_before
	check(unchanged and bool(fixture_game.s.settings.user_paused), kind + " preserves paused live game, RNG and wallets")
	check(build_unchanged and snapshot_unchanged, kind + " preserves snapshot and world build_count")
	if kind == "pan": check(maximum_camera_displacement > 0.0001, "pan phase actually moves the Camera3D")
	if kind == "zoom": check(maximum_camera_size_change > 0.0001, "zoom phase actually changes orthographic camera size")
	var row := {"phase": kind, "duration_seconds": wall_seconds,
		"drawn_frame_intervals_ms": distribution(frame_ms),
		"observed_throughput_fps": float(frame_ms.size()) / wall_seconds if wall_seconds > 0.0 else null,
		"process_cpu": phase_cpu, "memory_after": rss(), "live_before": live_before,
		"live_after": live_after, "live_state_rng_wallets_unchanged": unchanged,
		"snapshot_unchanged": snapshot_unchanged, "world_build_count_before": build_before,
		"world_build_count_after": world.build_count, "world_build_count_unchanged": build_unchanged,
		"maximum_camera_displacement": maximum_camera_displacement,
		"maximum_camera_size_change": maximum_camera_size_change,
		"camera_position_after": [world.camera.position.x, world.camera.position.y, world.camera.position.z],
		"orthographic_size_after": world.camera.size}
	print("LIVING WORLD PERF ", kind, " n=", frame_ms.size(), " wall_s=", wall_seconds,
		" mean_ms=", row.drawn_frame_intervals_ms.mean_ms, " p95_ms=", row.drawn_frame_intervals_ms.p95_ms)
	return row


func simulation_probe(isolation: String) -> Dictionary:
	check(not RenderingServer.is_render_loop_enabled() and world == null, "simulation measured with no world and rendering disabled")
	# A separate real Game preserves the paused renderer fixture. Normalize the
	# fixture's initial direct economic tick to the beta clock before measurement.
	var g = Fixture.make_game()
	var fixture_local_hours := float(g.s.local_hours)
	g.s.beta_time.local_elapsed_seconds = fixture_local_hours * float(g.c.beta.units.earth_seconds_per_month)
	g.s.settings.user_paused = false
	g.s.beta_time.pause_reason = ""
	var mode_error: String = g.Clock.set_mode(g, "normal")
	check(mode_error == "", "simulation uses real normal-speed Game.advance_active")
	var call_ms: Array[float] = []
	var amortized_step_ms: Array[float] = []
	var steps_per_call: Array[int] = []
	var reasons: Array[String] = []
	var before := {"simulation_tick": int(g.s.simulation_tick),
		"segments_committed": int(g.s.beta_time.segments_committed),
		"local_seconds": float(g.s.beta_time.local_elapsed_seconds),
		"active_real_seconds": float(g.s.beta_time.active_real_seconds), "dt": float(g.s.dt)}
	var cpu_before := cpu_snapshot()
	var started := Time.get_ticks_usec()
	var steps := 0
	var committed_real_seconds := 0.0
	for index in simulation_calls:
		var call_started := Time.get_ticks_usec()
		var advanced: Dictionary = g.advance_active(FIXED_ACTIVE_SECONDS)
		var cost := elapsed_ms(call_started)
		var count := int(advanced.get("steps", 0))
		call_ms.append(cost)
		steps_per_call.append(count)
		steps += count
		committed_real_seconds += float(advanced.get("active_real_seconds", 0.0))
		if count > 0: amortized_step_ms.append(cost / float(count))
		if str(advanced.get("reason", "")) != "": reasons.append(str(advanced.reason))
		if count == 0 or bool(g.s.settings.user_paused): break
	var wall_seconds := float(Time.get_ticks_usec() - started) / 1000000.0
	var phase_cpu := cpu_delta(cpu_before, cpu_snapshot(), wall_seconds)
	var tick_delta := int(g.s.simulation_tick) - int(before.simulation_tick)
	var segment_delta := int(g.s.beta_time.segments_committed) - int(before.segments_committed)
	var local_delta := float(g.s.beta_time.local_elapsed_seconds) - float(before.local_seconds)
	var active_delta := float(g.s.beta_time.active_real_seconds) - float(before.active_real_seconds)
	check(call_ms.size() == simulation_calls and steps > 0 and tick_delta == steps and segment_delta == steps,
		"simulation commits actual ticks and clock segments for every requested call")
	check(absf(active_delta - simulation_calls * FIXED_ACTIVE_SECONDS) < 0.000001 and absf(local_delta - active_delta) < 0.000001,
		"fixed active dt is genuinely committed at normal speed")
	check(float(g.s.dt) == float(before.dt) and g.validate_state(g.s) == "", "normal-speed simulation preserves DT and valid game state")
	return {"isolation": isolation, "render_loop_enabled": RenderingServer.is_render_loop_enabled(),
		"world_present": world != null, "fixture": "independent hero-valley-v1 real Game",
		"fixture_clock_alignment": {"local_hours": fixture_local_hours,
			"local_elapsed_seconds_before_measurement": before.local_seconds,
			"scope": "Align the initial direct Sim.tick to beta local time on the simulation fixture only."},
		"time_mode": "normal", "fixed_active_seconds_per_call": FIXED_ACTIVE_SECONDS,
		"requested_calls": simulation_calls, "actual_calls": call_ms.size(), "committed_steps": steps,
		"simulation_tick_delta": tick_delta, "segments_committed_delta": segment_delta,
		"steps_per_call": steps_per_call, "duration_seconds": wall_seconds,
		"advance_active_call_wall_ms": distribution(call_ms),
		"amortized_wall_ms_per_committed_step": distribution(amortized_step_ms),
		"step_cost_scope": "Real advance_active call wall time divided by its actual committed steps; includes Clock boundary and validation work, not a direct isolated Sim.tick timer.",
		"process_cpu": phase_cpu,
		"process_cpu_seconds_per_committed_step": float(phase_cpu.total_seconds) / float(steps) if phase_cpu.available and steps > 0 else null,
		"local_seconds_advanced": local_delta,
		"active_real_seconds_committed": active_delta, "reported_active_seconds": committed_real_seconds,
		"dt_before": before.dt, "dt_after": g.s.dt, "reasons": reasons, "state_error": g.validate_state(g.s),
		"native_backend_still_loaded": not simulation_only,
		"scope": "Synchronous real Game.advance_active costs after disabling rendering; renderer frame wall time is never attributed to simulation. The --headless --simulation-only invocation additionally excludes a loaded native rendering backend."}


func next_drawn_frame() -> void:
	await process_frame
	await RenderingServer.frame_post_draw


static func elapsed_ms(started: int) -> float:
	return float(Time.get_ticks_usec() - started) / 1000.0


static func distribution(values: Array[float]) -> Dictionary:
	if values.is_empty(): return {"n": 0, "mean_ms": null, "p50_ms": null, "p95_ms": null, "max_ms": null}
	var ordered := values.duplicate()
	ordered.sort()
	var total := 0.0
	for value in ordered: total += value
	return {"n": ordered.size(), "mean_ms": total / ordered.size(),
		"p50_ms": ordered[int(ceil(ordered.size() * 0.50)) - 1],
		"p95_ms": ordered[int(ceil(ordered.size() * 0.95)) - 1], "max_ms": ordered[-1]}


static func live_fingerprints(g) -> Dictionary:
	var inventories := {}
	for id in g.s.projects: inventories[id] = g.s.projects[id].get("inventory", {}).duplicate(true)
	var wallets := {"currency": g.s.currency, "dt": g.s.dt, "inventories": inventories,
		"bonus_wallet": g.s.block.get("bonus_wallet", {}), "bonus_instances": g.s.bonus_instances}
	return {"live_state_sha256": Suite.live_digest(g), "rng_sha256": Snapshot.digest(g.s.rng),
		"wallets_sha256": Snapshot.digest(wallets), "revision": g.revision,
		"simulation_tick": g.s.simulation_tick, "user_paused": g.s.settings.user_paused}


func linux_clock_tick_rate() -> int:
	if OS.get_name() != "Linux": return 0
	var output: Array = []
	var status := OS.execute("getconf", PackedStringArray(["CLK_TCK"]), output)
	if status != 0 or output.is_empty(): return 0
	var value := str(output[0]).strip_edges()
	return int(value) if value.is_valid_int() and int(value) > 0 else 0


static func cpu_snapshot() -> Dictionary:
	var file := FileAccess.open("/proc/self/stat", FileAccess.READ)
	if file == null: return {"available": false, "reason": "/proc/self/stat unavailable"}
	var line := file.get_line()
	file.close()
	var closing := line.rfind(")")
	if closing < 0: return {"available": false, "reason": "malformed /proc/self/stat"}
	var fields := line.substr(closing + 2).split(" ", false)
	if fields.size() < 13 or not fields[11].is_valid_int() or not fields[12].is_valid_int():
		return {"available": false, "reason": "missing process CPU tick fields"}
	return {"available": true, "user_ticks": int(fields[11]), "system_ticks": int(fields[12])}


func cpu_delta(before: Dictionary, after: Dictionary, wall_seconds: float) -> Dictionary:
	var available: bool = clock_ticks_per_second > 0 and before.get("available", false) and after.get("available", false)
	var output := {"available": available, "clock_ticks_per_second": clock_ticks_per_second,
		"source": "Linux /proc/self/stat process utime+stime, getconf CLK_TCK; excludes child-process CPU",
		"user_seconds": null, "system_seconds": null, "total_seconds": null, "percent_of_one_core": null}
	if available:
		output.user_seconds = float(int(after.user_ticks) - int(before.user_ticks)) / clock_ticks_per_second
		output.system_seconds = float(int(after.system_ticks) - int(before.system_ticks)) / clock_ticks_per_second
		output.total_seconds = float(output.user_seconds) + float(output.system_seconds)
		output.percent_of_one_core = 100.0 * float(output.total_seconds) / wall_seconds if wall_seconds > 0.0 else null
	else: output.reason = "Linux CPU ticks or their frequency could not be read; no CPU time is inferred from wall time."
	return output


static func renderer_method() -> String:
	return str(RenderingServer.call("get_current_rendering_method")) if RenderingServer.has_method("get_current_rendering_method") else "unknown"


static func backend_metadata() -> Dictionary:
	var name := RenderingServer.get_video_adapter_name()
	var adapter_type := RenderingServer.get_video_adapter_type()
	var lower := name.to_lower()
	var software := adapter_type == RenderingDevice.DEVICE_TYPE_CPU or "llvmpipe" in lower or "lavapipe" in lower or "swiftshader" in lower
	var headless := DisplayServer.get_name() == "headless"
	var packages: Array = []
	var mesa_package := "unavailable"
	if OS.get_name() == "Linux":
		if OS.execute("dpkg-query", PackedStringArray(["-W", "-f=${Version}", "mesa-vulkan-drivers"]), packages) == 0 and not packages.is_empty():
			mesa_package = str(packages[0]).strip_edges()
	var mapped_drivers: Array[String] = []
	var maps := FileAccess.open("/proc/self/maps", FileAccess.READ)
	if maps != null:
		while not maps.eof_reached():
			var line := maps.get_line()
			if "libvulkan_" in line:
				var path := line.substr(line.find("/")) if "/" in line else line
				if path not in mapped_drivers: mapped_drivers.append(path)
		maps.close()
	return {"rendering_method": renderer_method(),
		"driver": RenderingServer.call("get_current_rendering_driver_name") if RenderingServer.has_method("get_current_rendering_driver_name") else "unknown",
		"adapter_name": name, "adapter_vendor": RenderingServer.get_video_adapter_vendor(),
		"adapter_type": adapter_type, "adapter_api_version": RenderingServer.get_video_adapter_api_version(),
		"adapter_driver_info": OS.call("get_video_adapter_driver_info") if OS.has_method("get_video_adapter_driver_info") else null,
		"software_cpu_renderer": software, "mesa_vulkan_drivers_package_version": mesa_package,
		"mapped_vulkan_icd_libraries": mapped_drivers, "display_server": DisplayServer.get_name(),
		"classification": "Headless backend; no visual throughput or GPU evidence." if headless else ("Software Vulkan running on CPU; combined engine/driver wall throughput, not pure GPU cost or hardware GPU certification." if software else "Actual selected backend; native throughput on this device only, not a general hardware certification.")}


static func first_line(path: String) -> Variant:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null: return null
	var value := file.get_line()
	file.close()
	return value


static func environment_metadata() -> Dictionary:
	return {"os": OS.get_name(), "cpu": OS.get_processor_name(), "logical_processors": OS.get_processor_count(),
		"cpu_quota": first_line("/sys/fs/cgroup/cpu.max"), "memory_limit_bytes": first_line("/sys/fs/cgroup/memory.max"),
		"vk_icd_filenames": OS.get_environment("VK_ICD_FILENAMES"), "memory": rss()}


static func rss() -> Dictionary:
	var result := {}
	var file := FileAccess.open("/proc/self/status", FileAccess.READ)
	if file == null: return {"available": false}
	while not file.eof_reached():
		var line := file.get_line()
		if line.begins_with("VmRSS:") or line.begins_with("VmHWM:"):
			var parts := line.split(":", true, 1)
			result[parts[0] + "_KiB"] = int(parts[1].strip_edges().split(" ", false)[0])
	file.close()
	result.available = not result.is_empty()
	return result


static func source_hashes() -> Dictionary:
	var result := Suite.source_hashes()
	for path in ["res://project.godot", "res://tests/living_world_performance.gd", "res://tests/paths.gd"]:
		result[path] = FileAccess.get_sha256(path)
	for path in ["res://core", "res://ui", "res://content"]: hash_directory(path, result)
	return result


static func hash_directory(path: String, result: Dictionary) -> void:
	var directory := DirAccess.open(path)
	if directory == null: return
	for name in directory.get_files():
		if name.ends_with(".gd") or name.ends_with(".gdshader") or name.ends_with(".json"):
			var full_path := path.path_join(name)
			result[full_path] = FileAccess.get_sha256(full_path)
	for name in directory.get_directories(): hash_directory(path.path_join(name), result)


func has_failures() -> bool:
	return checks.any(func(row: Dictionary): return not row["pass"])


func finish(result: Dictionary) -> void:
	var source_after := source_hashes()
	check(source_before == source_after, "render, simulation, asset and probe sources remain stable during the run")
	result.source_before = source_before
	result.source_after = source_after
	result.source_unchanged = source_before == source_after
	result.checks = checks
	result.failed = checks.filter(func(row: Dictionary): return not row["pass"]).size()
	result.passed = checks.size() - int(result.failed)
	result.duration_seconds = float(Time.get_ticks_usec() - run_started_usec) / 1000000.0
	result.memory_after = rss()
	var path := Paths.file("living_world_performance_simulation.json" if simulation_only else "living_world_performance_native.json")
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		push_error("Cannot write performance evidence: " + path)
		quit(1)
		return
	file.store_string(JSON.stringify(result, "\t", true, true))
	file.close()
	print("LIVING WORLD PERFORMANCE ", "PASS" if not has_failures() else "FAIL", " ", path)
	quit(1 if has_failures() else 0)
