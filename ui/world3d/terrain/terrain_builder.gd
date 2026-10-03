extends RefCounted
## Continuous, lit terrain derived only from the detached visual snapshot.
## The legacy plane remains the source coordinate system. There is no hex
## surface mesh: a single lattice is clipped against the landscape perimeter.

const Elevation = preload("res://ui/map/elevation040.gd")
const Foundation = preload("res://ui/map/foundation041.gd")
const TERRAIN_SHADER = preload("res://ui/world3d/terrain/terrain_material.gdshader")
const WORLD_SCALE := 64.0
const LATTICE_STEP := 10.0
const RIVER_HALF_WIDTH := 31.0
const RIVER_BANK_WIDTH := 55.0
const RIVER_BED := -8.0
const LANDSCAPE_MARGIN := 72.0


static func height_at(snapshot: Dictionary, point: Vector2) -> float:
	var elevation: Dictionary = snapshot.get("elevation_field", {})
	var natural := func(p: Vector2) -> float: return Elevation.height_at(elevation, p)
	var field: Dictionary = snapshot.get("foundation_field", {})
	var height := Foundation.height_at(field, point, natural)
	# Legacy grading uses a rounded superellipse. A rectangular GLB must also
	# have level ground at its outer ground-contact corners. This 3D-only core
	# retains the legacy grade as its surrounding base and smoothly rejoins it.
	var closest_core := INF
	var core_platform := 0.0
	var has_core := false
	var grade_total := 0.0
	var platform_total := 0.0
	var remaining_grade := 1.0
	for site_value in field.get("sites", []):
		var site: Dictionary = site_value
		if bool(site.get("terrain_following", false)):
			continue
		var center: Vector2 = site.get("center", Vector2.ZERO)
		var footprint := _runtime_footprint(site)
		var distance := _rectangle_distance(footprint, point)
		var platform := float(site.get("platform_height", 0.0))
		if distance <= 0.0001:
			var center_distance := point.distance_squared_to(center)
			if center_distance < closest_core:
				closest_core = center_distance
				core_platform = platform
				has_core = true
			continue
		var blend_radius := maxf(1.0, float(site.get("blend_radius", 20.0)))
		if distance >= blend_radius:
			continue
		var weight := 1.0 - smoothstep(0.0, blend_radius, distance)
		grade_total += weight
		platform_total += platform * weight
		remaining_grade *= 1.0 - weight
	if has_core:
		return core_platform
	var protection := 1.0 - remaining_grade
	if grade_total > 0.0:
		height = lerpf(height, platform_total / grade_total, protection)
	var graded_height := height
	var distance := river_distance(snapshot, point) * RIVER_HALF_WIDTH / river_half_width(point)
	if distance < 94.0:
		# The existing field gives water cells a broad zero-height valley floor.
		# Carving the actual continuous path avoids independent hex-shaped pools.
		var channel_blend := smoothstep(18.0, RIVER_HALF_WIDTH + 2.0, distance)
		height = lerpf(RIVER_BED, height, channel_blend)
		var bank := 4.5 * smoothstep(24.0, 37.0, distance) * (1.0 - smoothstep(67.0, 94.0, distance))
		# The bank meets the waterline before the ribbon edge, so depth testing
		# creates an actual shoreline instead of a water strip above a dry trench.
		height = lerpf(height, maxf(height, bank), smoothstep(22.0, 32.0, distance))
	# River carving never changes a runtime platform; the surrounding protection
	# fades to zero at the same grade boundary, without a hard trench seam.
	return lerpf(height, graded_height, protection)


static func world_position(snapshot: Dictionary, point: Vector2, offset_world := 0.0) -> Vector3:
	return Vector3(point.x / WORLD_SCALE, height_at(snapshot, point) / WORLD_SCALE + offset_world, -point.y / WORLD_SCALE)


static func river_half_width(point: Vector2) -> float:
	# Shared fixed, continuous bank width for the terrain and water ribbon.
	return RIVER_HALF_WIDTH * (1.0 + sin(point.y * 0.012 + point.x * 0.003) * 0.12 + sin(point.y * 0.038 - point.x * 0.009) * 0.05)

static func river_distance(snapshot: Dictionary, point: Vector2) -> float:
	var path: Variant = snapshot.get("river_path", [])
	var nearest := INF
	for index in range(path.size() - 1):
		var a: Vector2 = path[index]
		var b: Vector2 = path[index + 1]
		nearest = minf(nearest, point.distance_to(Geometry2D.get_closest_point_to_segment(point, a, b)))
	return nearest


static func domain(snapshot: Dictionary) -> PackedVector2Array:
	var centers := PackedVector2Array()
	for tile_value in snapshot.get("tiles", {}).values():
		var tile: Dictionary = tile_value
		var center := Elevation.world_center(Vector2i(int(tile.get("q", 0)), int(tile.get("r", 0))))
		for corner in 6:
			centers.append(center + Vector2.from_angle(PI / 6.0 + TAU * corner / 6.0) * WORLD_SCALE)
	if centers.size() < 3:
		var fallback: Rect2 = snapshot.get("ground_bounds", Rect2(-256, -256, 512, 512))
		return PackedVector2Array([fallback.position, Vector2(fallback.end.x, fallback.position.y), fallback.end, Vector2(fallback.position.x, fallback.end.y)])
	var hull := Geometry2D.convex_hull(centers)
	var expanded := Geometry2D.offset_polygon(hull, LANDSCAPE_MARGIN, Geometry2D.JOIN_ROUND)
	return expanded[0] if not expanded.is_empty() else hull


static func build(snapshot: Dictionary) -> Dictionary:
	var started := Time.get_ticks_usec()
	var root := Node3D.new()
	root.name = "ContinuousValleyTerrain"
	var perimeter := domain(snapshot)
	var bounds := _bounds(perimeter)
	var cols := maxi(2, int(ceil(bounds.size.x / LATTICE_STEP)) + 1)
	var rows := maxi(2, int(ceil(bounds.size.y / LATTICE_STEP)) + 1)
	var dx := bounds.size.x / float(cols - 1)
	var dy := bounds.size.y / float(rows - 1)
	var heights := PackedFloat32Array()
	var inside := PackedByteArray()
	var points := PackedVector2Array()
	for row in rows:
		for col in cols:
			var point := bounds.position + Vector2(col * dx, row * dy)
			points.append(point)
			heights.append(height_at(snapshot, point))
			inside.append(1 if Geometry2D.is_point_in_polygon(point, perimeter) else 0)
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var colors := PackedColorArray()
	var uvs := PackedVector2Array()
	var indices := PackedInt32Array()
	for row in rows:
		for col in cols:
			var index := row * cols + col
			var derivative_x := (heights[row * cols + mini(cols - 1, col + 1)] - heights[row * cols + maxi(0, col - 1)]) / (dx * (1.0 if col in [0, cols - 1] else 2.0))
			var derivative_y := (heights[mini(rows - 1, row + 1) * cols + col] - heights[maxi(0, row - 1) * cols + col]) / (dy * (1.0 if row in [0, rows - 1] else 2.0))
			var normal := Vector3(-derivative_x, 1.0, derivative_y).normalized()
			vertices.append(Vector3(points[index].x, heights[index], -points[index].y) / WORLD_SCALE)
			normals.append(normal)
			colors.append(_surface_weights(snapshot, points[index], heights[index], normal))
			uvs.append(points[index] / WORLD_SCALE)
	for row in rows - 1:
		for col in cols - 1:
			var a := row * cols + col
			var b := a + 1
			var c := a + cols
			var d := c + 1
			for triangle in [[a, b, c], [b, d, c]]:
				if inside[triangle[0]] == 1 and inside[triangle[1]] == 1 and inside[triangle[2]] == 1:
					indices.append_array(PackedInt32Array(triangle))
					continue
				var polygon := PackedVector2Array([points[triangle[0]], points[triangle[1]], points[triangle[2]]])
				var clipped := Geometry2D.intersect_polygons(polygon, perimeter)
				for piece in clipped:
					if piece.size() < 3:
						continue
					var start := vertices.size()
					for point in piece:
						_append_sample(snapshot, point, vertices, normals, colors, uvs)
					var triangulation := Geometry2D.triangulate_polygon(piece)
					for value in triangulation:
						indices.append(start + int(value))
	var terrain := _mesh_instance("TerrainMesh", vertices, normals, colors, uvs, indices)
	var material := ShaderMaterial.new()
	material.shader = TERRAIN_SHADER
	material.set_shader_parameter("visual_seed", float(int(snapshot.get("visual_seed", 0)) % 4096))
	terrain.material_override = material
	root.add_child(terrain)
	var skirt_count := _add_landscape_edge(root, snapshot, perimeter, material)
	var foundation_stats := _add_foundations(root, snapshot)
	var world_bounds := terrain.mesh.get_aabb()
	root.set_meta("presentation_only", true)
	return {
		"node": root, "material": material, "bounds_world": world_bounds,
		"stats": {
			"terrain_vertices": vertices.size(), "terrain_triangles": indices.size() / 3,
			"terrain_mesh_instances": 1, "perimeter_vertices": perimeter.size(),
			"landscape_edge_triangles": skirt_count, "foundation_meshes": foundation_stats.meshes,
			"foundation_triangles": foundation_stats.triangles,
			"build_cpu_ms": (Time.get_ticks_usec() - started) / 1000.0,
			"grid_step_legacy": LATTICE_STEP, "individual_hex_surfaces": 0,
		},
	}


static func _append_sample(snapshot: Dictionary, point: Vector2, vertices: PackedVector3Array, normals: PackedVector3Array, colors: PackedColorArray, uvs: PackedVector2Array) -> void:
	var height := height_at(snapshot, point)
	var gradient_x := (height_at(snapshot, point + Vector2(2, 0)) - height_at(snapshot, point - Vector2(2, 0))) / 4.0
	var gradient_y := (height_at(snapshot, point + Vector2(0, 2)) - height_at(snapshot, point - Vector2(0, 2))) / 4.0
	var normal := Vector3(-gradient_x, 1, gradient_y).normalized()
	vertices.append(Vector3(point.x, height, -point.y) / WORLD_SCALE)
	normals.append(normal)
	colors.append(_surface_weights(snapshot, point, height, normal))
	uvs.append(point / WORLD_SCALE)


static func _surface_weights(snapshot: Dictionary, point: Vector2, height: float, normal: Vector3) -> Color:
	var seed := float(int(snapshot.get("visual_seed", 0)) % 4096)
	var noise := clampf(0.5 + sin(point.x * 0.021 + seed * 0.19) * sin(point.y * 0.029 - seed * 0.11) * 0.29 + sin(point.x * 0.055 + point.y * 0.017 + seed * 0.13) * 0.13, 0.0, 1.0)
	var river := river_distance(snapshot, point)
	var dirt := (1.0 - smoothstep(40.0, 76.0, river)) * 0.75
	dirt = maxf(dirt, smoothstep(0.50, 0.82, noise) * 0.45)
	# Legacy hills are broad, buildable slopes; a 30-degree threshold made
	# practically all of them grass. Rock appears on actual gentler inclines
	# and in stable upland outcrops, not as a fake illumination gradient.
	var incline := 1.0 - smoothstep(0.88, 0.995, normal.y)
	var upland := smoothstep(27.0, 65.0, height)
	var outcrop := smoothstep(0.34, 0.70, noise)
	var rock := incline * (0.30 + outcrop * 0.50) + upland * outcrop * 0.63
	for site_value in snapshot.get("foundation_field", {}).get("sites", []):
		var site: Dictionary = site_value
		if bool(site.get("terrain_following", false)):
			continue
		var edge := _rectangle_distance(_runtime_footprint(site), point)
		dirt = maxf(dirt, (1.0 - smoothstep(0.0, 12.0, edge)) * 0.83)
	var wet := (1.0 - smoothstep(34.0, 58.0, river)) * 0.36
	return Color(clampf(dirt, 0, 1), clampf(rock, 0, 1), clampf(wet, 0, 1), noise)


static func _add_landscape_edge(root: Node3D, snapshot: Dictionary, perimeter: PackedVector2Array, material: ShaderMaterial) -> int:
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var colors := PackedColorArray()
	var uvs := PackedVector2Array()
	var indices := PackedInt32Array()
	for edge in perimeter.size():
		var from: Vector2 = perimeter[edge]
		var to: Vector2 = perimeter[(edge + 1) % perimeter.size()]
		var count := maxi(1, int(ceil(from.distance_to(to) / LATTICE_STEP)))
		for segment in count:
			var a := from.lerp(to, float(segment) / count)
			var b := from.lerp(to, float(segment + 1) / count)
			var top_a := world_position(snapshot, a)
			var top_b := world_position(snapshot, b)
			var base_a := Vector3(top_a.x, -0.5, top_a.z)
			var base_b := Vector3(top_b.x, -0.5, top_b.z)
			var normal := (base_a - top_a).cross(top_b - top_a).normalized()
			var start := vertices.size()
			vertices.append_array(PackedVector3Array([top_a, base_a, base_b, top_b]))
			for point in [top_a, base_a, base_b, top_b]:
				normals.append(normal)
				colors.append(Color(0.85, 0.25, 0, 0.3))
				uvs.append(Vector2(point.x, point.y) * 2.0)
			indices.append_array(PackedInt32Array([start, start + 1, start + 2, start, start + 2, start + 3]))
	var skirt := _mesh_instance("LandscapeCutEdge", vertices, normals, colors, uvs, indices)
	skirt.material_override = material
	root.add_child(skirt)
	return indices.size() / 3


static func _add_foundations(root: Node3D, snapshot: Dictionary) -> Dictionary:
	var meshes := 0
	var triangles := 0
	var platform_material := StandardMaterial3D.new()
	platform_material.albedo_color = Color("898a77")
	platform_material.roughness = 0.94
	platform_material.cull_mode = BaseMaterial3D.CULL_BACK
	var wall_material := StandardMaterial3D.new()
	wall_material.albedo_color = Color("9a9788")
	wall_material.roughness = 0.9
	wall_material.cull_mode = BaseMaterial3D.CULL_BACK
	for item_value in snapshot.get("foundation_items", []):
		var item: Dictionary = item_value
		var site_platform := float(item.get("platform_height", 0.0))
		for entry in [["platform_triangles", platform_material, 0.003], ["retaining_walls", wall_material, 0.001]]:
			var legacy: PackedVector3Array = item.get(entry[0], PackedVector3Array())
			if entry[0] == "platform_triangles":
				for site_value in snapshot.get("foundation_field", {}).get("sites", []):
					var site: Dictionary = site_value
					if str(site.get("project_id", "")) != str(item.get("project_id", "")):
						continue
					var footprint := _runtime_footprint(site)
					var height := float(site.get("platform_height", 0.0))
					var a := Vector3(footprint.position.x, footprint.position.y, height)
					var b := Vector3(footprint.end.x, footprint.position.y, height)
					var c := Vector3(footprint.end.x, footprint.end.y, height)
					var d := Vector3(footprint.position.x, footprint.end.y, height)
					legacy = PackedVector3Array([a, b, c, a, c, d])
					break
			if legacy.is_empty():
				continue
			var vertices := PackedVector3Array()
			var normals := PackedVector3Array()
			for index in range(0, legacy.size(), 3):
				var converted := PackedVector3Array()
				for corner in 3:
					var source := legacy[index + corner]
					if entry[0] == "retaining_walls" and source.z > site_platform:
						# Old cut walls followed the natural grade, before the wider
						# registered GLB rectangle was flattened. Keep their geometry
						# below today's ground instead of piercing the platform.
						var grade := height_at(snapshot, Vector2(source.x, source.y))
						source.z = minf(source.z, maxf(site_platform, grade))
					converted.append(_convert_legacy(source, float(entry[2])))
				var a := converted[0]
				var b := converted[1]
				var c := converted[2]
				var normal := (b - a).cross(c - a).normalized()
				if entry[0] == "platform_triangles":
					normal = Vector3.UP
				vertices.append_array(PackedVector3Array([a, b, c]))
				normals.append_array(PackedVector3Array([normal, normal, normal]))
			var surface := _mesh_instance("Foundation_" + str(item.get("project_id", "site")) + "_" + str(entry[0]), vertices, normals, PackedColorArray(), PackedVector2Array(), PackedInt32Array())
			surface.material_override = entry[1]
			root.add_child(surface)
			meshes += 1
			triangles += legacy.size() / 3
	return {"meshes": meshes, "triangles": triangles}


static func _convert_legacy(point: Vector3, offset_world: float) -> Vector3:
	return Vector3(point.x / WORLD_SCALE, point.z / WORLD_SCALE + offset_world, -point.y / WORLD_SCALE)


static func _runtime_footprint(site: Dictionary) -> Rect2:
	var registered: Variant = site.get("runtime_footprint", null)
	if registered is Rect2:
		return registered
	var center: Vector2 = site.get("center", Vector2.ZERO)
	var half_size := Vector2(float(site.get("half_x", 0.0)), float(site.get("half_y", 0.0)))
	return Rect2(center - half_size, half_size * 2.0)


static func _rectangle_distance(rectangle: Rect2, point: Vector2) -> float:
	var offset := Vector2(maxf(rectangle.position.x - point.x, maxf(0.0, point.x - rectangle.end.x)), maxf(rectangle.position.y - point.y, maxf(0.0, point.y - rectangle.end.y)))
	return offset.length()


static func _mesh_instance(label: String, vertices: PackedVector3Array, normals: PackedVector3Array, colors: PackedColorArray, uvs: PackedVector2Array, indices: PackedInt32Array) -> MeshInstance3D:
	# Godot's front face is clockwise. The mathematical cross product of an
	# authored front triangle therefore points against its outward normal.
	# Normalize clipped polygons and legacy foundation triangles alike; leaving
	# culling disabled previously showed their back faces with inverted light.
	var clockwise := indices.duplicate()
	if clockwise.is_empty():
		for index in vertices.size():
			clockwise.append(index)
	for triangle in range(0, clockwise.size(), 3):
		var a := int(clockwise[triangle])
		var b := int(clockwise[triangle + 1])
		var c := int(clockwise[triangle + 2])
		var geometric := (vertices[b] - vertices[a]).cross(vertices[c] - vertices[a])
		var outward := normals[a] + normals[b] + normals[c]
		if geometric.dot(outward) > 0.0:
			clockwise[triangle + 1] = c
			clockwise[triangle + 2] = b
	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	if not colors.is_empty():
		arrays[Mesh.ARRAY_COLOR] = colors
	if not uvs.is_empty():
		arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = clockwise
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var instance := MeshInstance3D.new()
	instance.name = label
	instance.mesh = mesh
	return instance


static func _bounds(points: PackedVector2Array) -> Rect2:
	var result := Rect2(points[0], Vector2.ZERO)
	for point in points:
		result = result.expand(point)
	return result
