extends SceneTree
## Actual Forward+ fullscreen input checks. All pointer/key actions enter via
## Input.parse_input_event; no direct camera or mode callbacks replace actions.
const Hero = preload("res://ui/world3d/hero_scene.gd")
const Suite = preload("res://tests/living_world.gd")
const Terrain = preload("res://ui/world3d/terrain/terrain_builder.gd")
const Buildings = preload("res://ui/world3d/city/building_renderer.gd")
const Paths = preload("res://tests/paths.gd")
var checks: Array[Dictionary] = []
var inputs: Array[Dictionary] = []
var selected: Array[String] = []
var captures: Array[Dictionary] = []


func _initialize() -> void:
	call_deferred("run")


func check(condition: bool, name: String) -> void:
	checks.append({"name": name, "pass": condition})
	print(("PASS " if condition else "FAIL ") + name)


func run() -> void:
	var source_before := Suite.source_hashes()
	source_before["res://tests/living_world_native.gd"] = FileAccess.get_sha256("res://tests/living_world_native.gd")
	var hero := Hero.new()
	root.add_child(hero)
	await frames(8)
	check(Hero.renderer_method() == "forward_plus", "native Living World uses actual Forward+ renderer")
	check(root.mode == Window.MODE_FULLSCREEN and root.size == Vector2i(1920, 1080), "native window uses fullscreen 1920x1080 baseline")
	check(root.get_visible_rect().size == Vector2(1920, 1080), "native viewport is 1920x1080")
	check(not hero.mode_2d and hero.world.visible and not hero.legacy_map.visible, "scene initially displays genuine 3D world")
	check(hero.world.camera is Camera3D and hero.world.camera.projection == Camera3D.PROJECTION_ORTHOGONAL, "view uses an actual orthographic Camera3D")
	check(hero.world.stats.buildings.buildings == 4 and hero.world.stats.foliage.trees > 0, "native renderer displays four GLBs and instanced forest")
	check(not hero.world.visual_debug.grid.visible, "normal landscape begins without the Hex analysis overlay")
	var live_before := Suite.live_digest(hero.fixture_game)
	var snapshot_signature: String = hero.world.visual_snapshot.signature
	var initial_build_count: int = hero.world.build_count
	var content_id: int = hero.world.content.get_instance_id()
	var terrain_id: int = hero.world.results.terrain.node.get_instance_id()
	var initial_pan: Vector3 = hero.world.pan_target
	var initial_position: Vector3 = hero.world.camera.position
	var center := Vector2(960, 540)
	await mouse_button(MOUSE_BUTTON_MIDDLE, true, center)
	await mouse_motion(center + Vector2(140, 50), Vector2(140, 50), MOUSE_BUTTON_MASK_MIDDLE)
	await mouse_button(MOUSE_BUTTON_MIDDLE, false, center + Vector2(140, 50))
	await frames(5)
	check(not hero.world.pan_target.is_equal_approx(initial_pan), "native middle drag changes camera pan target")
	check(not hero.world.camera.position.is_equal_approx(initial_position), "native camera moves smoothly after drag")
	check(not hero.world.dragging, "native middle release ends dragging")
	var initial_size: float = hero.world.camera.size
	await mouse_button(MOUSE_BUTTON_WHEEL_UP, true, center)
	await mouse_button(MOUSE_BUTTON_WHEEL_UP, false, center)
	await frames(5)
	check(hero.world.size_target < initial_size and hero.world.camera.size < initial_size, "native wheel zoom changes target and actual orthographic camera")
	await key(KEY_HOME)
	await frames(3)
	check(hero.world.pan_target.is_equal_approx(Vector3.ZERO) and is_equal_approx(hero.world.camera.size, 12.0), "native Home key restores fixed golden camera")
	await key(KEY_H)
	check(hero.world.analysis_visible and hero.world.visual_debug.grid.visible, "native H key reveals analysis Hex geometry")
	await key(KEY_H)
	check(not hero.world.analysis_visible and not hero.world.visual_debug.grid.visible, "native H key returns to continuous landscape")
	var tile := "0,0"
	var point := Terrain.world_position(hero.snapshot, Buildings.tile_center(tile))
	var screen: Vector2 = hero.world.camera.unproject_position(point)
	check(Rect2(Vector2.ZERO, Vector2(1920, 1080)).has_point(screen), "known occupied tile projects inside the native viewport")
	hero.world.tile_selected.connect(func(value: String): selected.append(value))
	await mouse_motion(screen, Vector2.ZERO, 0)
	check(hero.world.visual_debug.hover.visible and hero.world.pick_tile(screen) == tile, "native hover identifies actual terrain tile and displays contextual overlay")
	await mouse_button(MOUSE_BUTTON_LEFT, true, screen)
	await mouse_button(MOUSE_BUTTON_LEFT, false, screen)
	check(not selected.is_empty() and selected[-1] == tile, "native selection emits the actual logical Hex without modifying gameplay")
	await capture("living-world-native-3d.png")
	var fallback_button := button_named(hero.toolbar, "2D 基线")
	check(fallback_button != null, "fallback remains available through a real toolbar button")
	if fallback_button != null:
		await click_control(fallback_button)
		check(hero.mode_2d and hero.legacy_map.visible and not hero.world.visible, "native fallback button switches to retained 2D map")
		await capture("living-world-native-2d.png")
	var world_button := button_named(hero.toolbar, "3D 景观")
	check(world_button != null, "return to 3D remains available through a real toolbar button")
	if world_button != null:
		await click_control(world_button)
		check(not hero.mode_2d and hero.world.visible and not hero.legacy_map.visible, "native toolbar returns from fallback to 3D")
	check(hero.world.build_count == initial_build_count, "native pan zoom hover analysis and renderer toggles do not rebuild the world")
	check(hero.world.content.get_instance_id() == content_id and hero.world.results.terrain.node.get_instance_id() == terrain_id, "camera and analysis preserve retained world and terrain geometry identities")
	check(hero.world.visual_snapshot.signature == snapshot_signature, "native interaction preserves the committed visual snapshot")
	check(Suite.live_digest(hero.fixture_game) == live_before, "native camera picking analysis and fallback preserve simulation wallets RNG entities compiled data and revision")
	var source_after := Suite.source_hashes()
	source_after["res://tests/living_world_native.gd"] = FileAccess.get_sha256("res://tests/living_world_native.gd")
	check(source_before == source_after, "native suite uses unchanged source and GLB assets")
	var failures := checks.filter(func(row): return not row["pass"]).size()
	var output := {"suite": "living_world_native", "passed": checks.size() - failures, "failed": failures,
		"checks": checks, "renderer_method": Hero.renderer_method(), "device": RenderingServer.get_video_adapter_name(),
		"window_mode": root.mode, "window_size": [root.size.x, root.size.y],
		"input_events": inputs, "native_input_count": inputs.size(), "captures": captures,
		"source_before": source_before, "source_after": source_after, "source_unchanged": source_before == source_after,
		"live_state_unchanged": Suite.live_digest(hero.fixture_game) == live_before,
		"world_build_count_before": initial_build_count, "world_build_count_after": hero.world.build_count,
		"snapshot_signature": snapshot_signature, "stats": hero.world.stats}
	FileAccess.open(Paths.file("living_world_native.json"), FileAccess.WRITE).store_string(JSON.stringify(output, "\t", true, true))
	hero.queue_free()
	print("LIVING WORLD NATIVE %d passed, %d failed" % [checks.size() - failures, failures])
	quit(1 if failures else 0)


func frames(count: int) -> void:
	for index in count: await process_frame


func mouse_button(button: MouseButton, pressed: bool, position: Vector2) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = button
	event.pressed = pressed
	event.position = position
	event.global_position = position
	inputs.append({"kind": "mouse_button", "button": button, "pressed": pressed, "position": position})
	Input.parse_input_event(event)
	await frames(2)


func mouse_motion(position: Vector2, relative: Vector2, mask: int) -> void:
	var event := InputEventMouseMotion.new()
	event.position = position
	event.global_position = position
	event.relative = relative
	event.button_mask = mask
	inputs.append({"kind": "mouse_motion", "position": position, "relative": relative, "mask": mask})
	Input.parse_input_event(event)
	await frames(2)


func key(code: Key) -> void:
	for pressed in [true, false]:
		var event := InputEventKey.new()
		event.keycode = code
		event.physical_keycode = code
		event.pressed = pressed
		inputs.append({"kind": "key", "keycode": code, "pressed": pressed})
		Input.parse_input_event(event)
		await frames(2)


func click_control(control: Control) -> void:
	var position := control.get_global_rect().get_center()
	await mouse_motion(position, Vector2.ZERO, 0)
	await mouse_button(MOUSE_BUTTON_LEFT, true, position)
	await mouse_button(MOUSE_BUTTON_LEFT, false, position)
	await frames(4)


static func button_named(node: Node, label: String) -> Button:
	if node is Button and node.text == label: return node
	for child in node.get_children():
		var result := button_named(child, label)
		if result != null: return result
	return null


func capture(filename: String) -> void:
	await frames(3)
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	var path := Paths.file(filename)
	var error := image.save_png(path)
	check(error == OK and image.get_width() == 1920 and image.get_height() == 1080, "native full-resolution screenshot: " + filename)
	captures.append({"path": path, "sha256": FileAccess.get_sha256(path), "size": [image.get_width(), image.get_height()]})
