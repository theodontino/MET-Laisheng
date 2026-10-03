extends Node3D
## Independent Living World axis: detached data in, pixels out. No Game reference.
const Terrain = preload("res://ui/world3d/terrain/terrain_builder.gd")
const River = preload("res://ui/world3d/terrain/river_builder.gd")
const Buildings = preload("res://ui/world3d/city/building_renderer.gd")
const Fabric = preload("res://ui/world3d/city/fabric_renderer.gd")
const Foliage = preload("res://ui/world3d/foliage/foliage_scatter.gd")
const EnvironmentController = preload("res://ui/world3d/environment/environment_controller.gd")
const Debug = preload("res://ui/world3d/debug/visual_debug.gd")
const Elevation = preload("res://ui/map/elevation040.gd")
signal tile_hovered(key: String)
signal tile_selected(key: String)
var visual_snapshot: Dictionary = {}
var stats: Dictionary = {}
var results: Dictionary = {}
var content: Node3D
var environment_controller: Node3D
var camera: Camera3D
var visual_debug: Node3D
var build_count := 0
var camera_target := Vector3(0, 0.35, 0)
var pan_target := Vector3.ZERO
var pan_current := Vector3.ZERO
var size_target := 12.0
var direction_to_camera := Vector3(9, 13.2, 12).normalized()
var dragging := false
var input_enabled := true
var analysis_visible := false

func _ready() -> void:
	environment_controller = EnvironmentController.new()
	add_child(environment_controller)
	camera = Camera3D.new()
	camera.name = "MiniatureCamera"
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = size_target
	camera.near = 0.05
	camera.far = 100.0
	camera.current = true
	add_child(camera)
	_update_camera()

func set_visual_snapshot(value: Dictionary) -> bool:
	if not value.get("ok", false) or value.get("forecast", true) or value.get("source_phase", "") != "committed_tick": return false
	if not visual_snapshot.is_empty() and visual_snapshot.signature == value.signature: return false
	visual_snapshot = value.duplicate(true)
	if content != null: remove_child(content); content.queue_free()
	content = Node3D.new()
	content.name = "DetachedVisualWorld"
	add_child(content)
	results = {}
	stats = {}
	environment_controller.clear_materials()
	for kind in ["terrain", "river", "buildings", "fabric", "foliage"]:
		var started := Time.get_ticks_usec()
		match kind:
			"terrain": results[kind] = Terrain.build(visual_snapshot)
			"river": results[kind] = River.build(visual_snapshot)
			"buildings": results[kind] = Buildings.build(visual_snapshot)
			"fabric": results[kind] = Fabric.build(visual_snapshot)
			"foliage": results[kind] = Foliage.build(visual_snapshot)
		results[kind].stats.construction_cpu_ms = (Time.get_ticks_usec() - started) / 1000.0
	stats = {}
	for kind in results:
		content.add_child(results[kind].node)
		stats[kind] = results[kind].stats.duplicate(true)
		if results[kind].has("material"):
			environment_controller.register_material(results[kind].material)
	visual_debug = Debug.new()
	content.add_child(visual_debug)
	visual_debug.configure(visual_snapshot)
	visual_debug.show_grid(analysis_visible)
	build_count += 1
	return true

func _process(delta: float) -> void:
	var blend := 1.0 - exp(-12.0 * delta)
	pan_current = pan_current.lerp(pan_target, blend)
	if camera != null:
		camera.size = lerpf(camera.size, size_target, blend)
		_update_camera()

func _update_camera() -> void:
	if camera == null: return
	var target := camera_target + pan_current
	camera.position = target + direction_to_camera * 24.0
	camera.look_at(target, Vector3.UP)

func pan_camera(screen_delta: Vector2) -> void:
	var world_delta := -camera.global_basis.x * screen_delta.x + camera.global_basis.y * screen_delta.y
	world_delta.y = 0.0
	pan_target += world_delta * size_target / maxf(1.0, get_viewport().get_visible_rect().size.y)

func zoom_camera(factor: float) -> void:
	size_target = clampf(size_target * factor, 7.0, 24.0)

func reset_camera() -> void:
	pan_target = Vector3.ZERO
	pan_current = Vector3.ZERO
	size_target = 12.0
	camera.size = size_target
	_update_camera()

func set_analysis_visible(value: bool) -> void:
	analysis_visible = value
	if visual_debug != null: visual_debug.show_grid(value)

func pick_tile(screen_position: Vector2) -> String:
	if visual_snapshot.is_empty(): return ""
	var origin := camera.project_ray_origin(screen_position)
	var direction := camera.project_ray_normal(screen_position)
	if absf(direction.y) < 0.0001: return ""
	var distance := -origin.y / direction.y
	var p := origin + direction * distance
	for iteration in 8:
		var height := Terrain.height_at(visual_snapshot, Vector2(p.x, -p.z) * 64.0) / 64.0
		distance = (height - origin.y) / direction.y
		p = origin + direction * distance
	var flat := Vector2(p.x, -p.z) * 64.0
	var nearest := 65.0
	var found := ""
	for key in visual_snapshot.tiles:
		var tile: Dictionary = visual_snapshot.tiles[key]
		var center := Elevation.world_center(Vector2i(int(tile.q), int(tile.r)))
		var gap := flat.distance_to(center)
		if gap < nearest: nearest = gap; found = str(key)
	return found

func _unhandled_input(event: InputEvent) -> void:
	if not input_enabled or not visible: return
	if event is InputEventMouseButton:
		if event.button_index in [MOUSE_BUTTON_MIDDLE, MOUSE_BUTTON_RIGHT]: dragging = event.pressed
		if event.pressed and event.button_index == MOUSE_BUTTON_WHEEL_UP: zoom_camera(0.90)
		if event.pressed and event.button_index == MOUSE_BUTTON_WHEEL_DOWN: zoom_camera(1.10)
		if event.pressed and event.button_index == MOUSE_BUTTON_LEFT: tile_selected.emit(pick_tile(event.position))
	elif event is InputEventMouseMotion:
		if dragging: pan_camera(event.relative)
		else:
			var key := pick_tile(event.position)
			visual_debug.show_hover(key)
			tile_hovered.emit(key)
