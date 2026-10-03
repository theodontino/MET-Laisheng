extends RefCounted
## A single 3D ribbon follows the detached snapshot's continuous river curve.
## The waterline and riverbed share TerrainBuilder's distance/width semantics.

const Terrain = preload("res://ui/world3d/terrain/terrain_builder.gd")
const RIVER_SHADER = preload("res://ui/world3d/terrain/river_material.gdshader")
const WATER_HEIGHT := 0.018
const CROSS_SECTIONS := 6


static func build(snapshot: Dictionary) -> Dictionary:
	var started := Time.get_ticks_usec()
	var root := Node3D.new()
	root.name = "ContinuousRiver"
	var source: Variant = snapshot.get("river_path", [])
	if source.size() < 2:
		return {"node": root, "stats": {"river_vertices": 0, "river_triangles": 0, "river_mesh_instances": 0}}
	var path := PackedVector2Array()
	for index in range(source.size() - 1):
		var a: Vector2 = source[index]
		var b: Vector2 = source[index + 1]
		var steps := maxi(1, int(ceil(a.distance_to(b) / 10.0)))
		for step in steps:
			path.append(a.lerp(b, float(step) / steps))
	path.append(source[-1])
	var perimeter := Terrain.domain(snapshot)
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var uvs := PackedVector2Array()
	var tangents := PackedFloat32Array()
	var colors := PackedColorArray()
	var indices := PackedInt32Array()
	var distances := PackedFloat32Array()
	var offsets := PackedFloat32Array()
	var length := 0.0
	for index in path.size():
		if index > 0:
			length += path[index].distance_to(path[index - 1])
		distances.append(length)
		var direction := (path[mini(index + 1, path.size() - 1)] - path[maxi(0, index - 1)]).normalized()
		var lateral_direction := direction.orthogonal()
		var tangent := Vector3(lateral_direction.x, 0, -lateral_direction.y).normalized()
		var width := Terrain.river_half_width(path[index])
		for section in CROSS_SECTIONS + 1:
			var lateral := float(section) / CROSS_SECTIONS * 2.0 - 1.0
			var point := path[index] + direction.orthogonal() * width * lateral
			vertices.append(Vector3(point.x / Terrain.WORLD_SCALE, WATER_HEIGHT, -point.y / Terrain.WORLD_SCALE))
			normals.append(Vector3.UP)
			tangents.append_array(PackedFloat32Array([tangent.x, tangent.y, tangent.z, 1.0]))
			uvs.append(Vector2(lateral * 0.5 + 0.5, length / Terrain.WORLD_SCALE))
			colors.append(Color(0, 0, 0, 1))
			offsets.append(lateral)
	for index in path.size() - 1:
		for section in CROSS_SECTIONS:
			var a := index * (CROSS_SECTIONS + 1) + section
			var b := a + 1
			var c := a + CROSS_SECTIONS + 1
			var d := c + 1
			for triangle in [[a, b, c], [b, d, c]]:
				var polygon := PackedVector2Array()
				for value in triangle:
					var vertex := vertices[value]
					polygon.append(Vector2(vertex.x * Terrain.WORLD_SCALE, -vertex.z * Terrain.WORLD_SCALE))
				if Geometry2D.is_point_in_polygon(polygon[0], perimeter) and Geometry2D.is_point_in_polygon(polygon[1], perimeter) and Geometry2D.is_point_in_polygon(polygon[2], perimeter):
					indices.append_array(PackedInt32Array(triangle))
					continue
				var clipped := Geometry2D.intersect_polygons(polygon, perimeter)
				for piece in clipped:
					var start := vertices.size()
					for point in piece:
						var weights := _barycentric(point, polygon[0], polygon[1], polygon[2])
						vertices.append(Vector3(point.x / Terrain.WORLD_SCALE, WATER_HEIGHT, -point.y / Terrain.WORLD_SCALE))
						normals.append(Vector3.UP)
						uvs.append(uvs[triangle[0]] * weights.x + uvs[triangle[1]] * weights.y + uvs[triangle[2]] * weights.z)
						var tangent := Vector3.ZERO
						for corner in 3:
							var base := int(triangle[corner]) * 4
							tangent += Vector3(tangents[base], tangents[base + 1], tangents[base + 2]) * weights[corner]
						tangent = tangent.normalized()
						tangents.append_array(PackedFloat32Array([tangent.x, tangent.y, tangent.z, 1.0]))
						colors.append(Color(0, 0, 0, 1))
					for value in Geometry2D.triangulate_polygon(piece):
						indices.append(start + int(value))
	if indices.is_empty():
		return {"node": root, "stats": {"river_vertices": 0, "river_triangles": 0, "river_mesh_instances": 0}}
	# Match Godot's clockwise front face while preserving the upward water
	# normals. Consistency matters even for a flat ribbon under real lighting.
	for triangle in range(0, indices.size(), 3):
		var a := int(indices[triangle])
		var b := int(indices[triangle + 1])
		var c := int(indices[triangle + 2])
		if (vertices[b] - vertices[a]).cross(vertices[c] - vertices[a]).dot(Vector3.UP) > 0.0:
			indices[triangle + 1] = c
			indices[triangle + 2] = b
	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_TANGENT] = tangents
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var instance := MeshInstance3D.new()
	instance.name = "RiverRibbon"
	instance.mesh = mesh
	var material := ShaderMaterial.new()
	material.shader = RIVER_SHADER
	instance.material_override = material
	root.add_child(instance)
	root.set_meta("presentation_only", true)
	return {"node": root, "material": material, "stats": {
		"river_vertices": vertices.size(), "river_triangles": indices.size() / 3,
		"river_mesh_instances": 1, "continuous_path_points": path.size(),
		"water_height_world": WATER_HEIGHT, "river_half_width_legacy": Terrain.RIVER_HALF_WIDTH,
		"build_cpu_ms": (Time.get_ticks_usec() - started) / 1000.0,
	}}


static func _barycentric(point: Vector2, a: Vector2, b: Vector2, c: Vector2) -> Vector3:
	var determinant := (b - a).cross(c - a)
	if absf(determinant) < 0.00001:
		return Vector3(1, 0, 0)
	var b_weight := (point - a).cross(c - a) / determinant
	var c_weight := (b - a).cross(point - a) / determinant
	return Vector3(1.0 - b_weight - c_weight, b_weight, c_weight)
