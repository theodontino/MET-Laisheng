extends Node3D
## Hex exists here only as an optional logical/interaction overlay.
const Terrain = preload("res://ui/world3d/terrain/terrain_builder.gd")
const River = preload("res://ui/world3d/terrain/river_builder.gd")
const Elevation = preload("res://ui/map/elevation040.gd")
var snapshot: Dictionary = {}
var grid: MeshInstance3D
var hover: MeshInstance3D

func configure(value: Dictionary) -> void:
	snapshot = value
	grid = MeshInstance3D.new()
	grid.name = "AnalysisHexOverlay"
	grid.mesh = _lines(snapshot.tiles.keys())
	grid.material_override = _ink(Color(0.88, 0.90, 0.65, 0.62))
	grid.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(grid)
	grid.hide()
	hover = MeshInstance3D.new()
	hover.name = "HoverHexOverlay"
	hover.material_override = _ink(Color("ffda83"))
	hover.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(hover)

func show_grid(value: bool) -> void:
	if grid != null: grid.visible = value

func show_hover(key: String) -> void:
	if hover == null: return
	hover.visible = snapshot.tiles.has(key)
	if hover.visible: hover.mesh = _lines([key])

func _lines(keys: Array) -> ArrayMesh:
	var vertices := PackedVector3Array()
	for key in keys:
		var tile: Dictionary = snapshot.tiles[key]
		var center := Elevation.world_center(Vector2i(int(tile.q), int(tile.r)))
		for i in 6:
			var a := center + Vector2.from_angle(PI / 6.0 + TAU * i / 6.0) * 64.0
			var b := center + Vector2.from_angle(PI / 6.0 + TAU * (i + 1) / 6.0) * 64.0
			# Follow continuous terrain instead of spanning slopes with a buried chord.
			for segment in 8:
				vertices.append(_overlay_position(a.lerp(b, float(segment) / 8.0)))
				vertices.append(_overlay_position(a.lerp(b, float(segment + 1) / 8.0)))
	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	var mesh := ArrayMesh.new()
	if not vertices.is_empty(): mesh.add_surface_from_arrays(Mesh.PRIMITIVE_LINES, arrays)
	return mesh

func _overlay_position(point: Vector2) -> Vector3:
	var position := Terrain.world_position(snapshot, point, 0.024)
	# Analysis lines follow the visible water surface, rather than the submerged
	# bed. Normal terrain and gameplay water traits remain untouched.
	if Terrain.river_distance(snapshot, point) <= Terrain.river_half_width(point):
		position.y = maxf(position.y, River.WATER_HEIGHT + 0.024)
	return position

static func _ink(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.albedo_color = color
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.no_depth_test = false
	return material
