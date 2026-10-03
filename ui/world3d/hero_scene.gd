extends Control
## Opt-in visual vertical slice, isolated from the current player's save.
const Fixture = preload("res://tests/fixtures/hero_valley.gd")
const Snapshot = preload("res://ui/world3d/world_visual_snapshot.gd")
const World = preload("res://ui/world3d/world_view_3d.gd")
const MapView = preload("res://ui/map_view.gd")
const W = preload("res://ui/widgets.gd")
const Paths = preload("res://tests/paths.gd")
var fixture_game
var world
var legacy_map
var toolbar: PanelContainer
var snapshot: Dictionary = {}
var mode_2d := false
var no_ui := false

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	get_window().min_size = Vector2i(1920, 1080)
	get_window().mode = Window.MODE_FULLSCREEN
	var args := OS.get_cmdline_user_args()
	mode_2d = "--hero-2d" in args
	no_ui = "--hero-no-ui" in args or "--capture-hero" in args
	if not mode_2d and renderer_method() != "forward_plus":
		push_error("Hero Valley requires Forward+: launch with --rendering-method forward_plus --rendering-driver vulkan -- --hero-valley. Default city remains the GL 2D fallback.")
		get_tree().quit(2)
		return
	fixture_game = Fixture.make_game()
	var registry: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://content/presentation3d/asset_manifest.json"))
	snapshot = Snapshot.from_display(Fixture.committed(fixture_game), fixture_game.c.projects, Fixture.WORLD_SEED, Fixture.VISUAL_SEED, registry)
	assert(snapshot.get("ok", false), str(snapshot.get("error", "")))
	world = World.new()
	add_child(world)
	if not mode_2d: world.set_visual_snapshot(snapshot)
	legacy_map = MapView.new()
	legacy_map.game = fixture_game
	legacy_map.display_shot = Fixture.committed(fixture_game)
	legacy_map.show_labels = false
	legacy_map.show_impacts = false
	legacy_map.show_relations = false
	legacy_map.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(legacy_map)
	_toolbar()
	set_mode_2d(mode_2d)
	await get_tree().process_frame
	await get_tree().process_frame
	get_window().mode = Window.MODE_FULLSCREEN
	legacy_map.zoom = 1.45
	legacy_map.pan = Vector2(0, 24)
	legacy_map.invalidate()
	if "--capture-hero" in args: await capture_and_exit()

static func renderer_method() -> String:
	return str(RenderingServer.call("get_current_rendering_method")) if RenderingServer.has_method("get_current_rendering_method") else "unknown"

func _toolbar() -> void:
	theme = W.theme()
	toolbar = W.panel(self)
	toolbar.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	toolbar.position = Vector2(24, 24)
	var column := W.vbox(toolbar, 8)
	column.add_child(W.label("白霭河谷 · Living World V1", 22))
	column.add_child(W.label("固定白昼 · 37 Hex · 独立视觉切片", 14, W.MUTED))
	var row := W.hbox(column, 8)
	row.add_child(W.button("3D 景观", func(): set_mode_2d(false), renderer_method() != "forward_plus", "需要以 Forward+ 启动"))
	row.add_child(W.button("2D 基线", func(): set_mode_2d(true)))
	row.add_child(W.button("Hex 分析", func(): world.set_analysis_visible(not world.analysis_visible)))
	row.add_child(W.button("重置镜头", func(): world.reset_camera()))
	column.add_child(W.label("中键／右键平移 · 滚轮缩放 · H 分析 · F1 隐藏界面", 13, W.MUTED))
	toolbar.visible = not no_ui

func set_mode_2d(value: bool) -> void:
	if not value and renderer_method() != "forward_plus":
		push_warning("3D Hero Valley requires restarting with Forward+; the 2D fallback remains active.")
		return
	mode_2d = value
	if not value and world.visual_snapshot.is_empty(): world.set_visual_snapshot(snapshot)
	world.visible = not value
	world.input_enabled = not value and not no_ui
	world.camera.current = not value
	legacy_map.visible = value

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_F1: toolbar.visible = not toolbar.visible
		if event.keycode == KEY_H: world.set_analysis_visible(not world.analysis_visible)
		if event.keycode == KEY_HOME: world.reset_camera()
		if event.keycode == KEY_ESCAPE: get_tree().quit()

func capture_and_exit() -> void:
	toolbar.hide()
	world.input_enabled = false
	for frame in 16: await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var suffix := "2d" if mode_2d else "3d"
	var image := get_viewport().get_texture().get_image()
	var path := Paths.file("hero-valley-" + suffix + ".png")
	var error := image.save_png(path)
	var output := {"fixture": "hero-valley-v1", "world_seed": Fixture.WORLD_SEED,
		"visual_seed": Fixture.VISUAL_SEED, "tiles": snapshot.tiles.size(),
		"buildings": fixture_game.playable_ids().size(), "snapshot_signature": snapshot.signature,
		"renderer_method": renderer_method(), "device": RenderingServer.get_video_adapter_name(),
		"window_mode": get_window().mode, "window_size": [get_window().size.x, get_window().size.y],
		"image_size": [image.get_width(), image.get_height()], "ui_visible": false,
		"camera_direction": [world.direction_to_camera.x, world.direction_to_camera.y, world.direction_to_camera.z],
		"camera_size": world.camera.size, "environment": world.environment_controller.state,
		"stats": world.stats, "source_phase": snapshot.source_phase, "forecast": snapshot.forecast,
		"image_path": path, "png_sha256": FileAccess.get_sha256(path), "save_result": error}
	var file := FileAccess.open(Paths.file("hero-valley-" + suffix + ".json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(output, "\t", true, true))
	file.close()
	print("HERO VALLEY ", suffix, " ", snapshot.tiles.size(), " Hex; ", renderer_method(), "; capture=", error)
	get_tree().quit(0 if error == OK else 1)
