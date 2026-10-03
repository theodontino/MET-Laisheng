extends RefCounted
## Retained, presentation-only structures that visually bridge adjacent buildings.
## items() is called when the displayed connector specs change; draw_item() only
## paints cached world-space geometry and never reads or writes simulation state.

static var _geometry_cache: Dictionary = {}
const MAX_GEOMETRY_CACHE := 384

static func clear_cache() -> void:
	_geometry_cache.clear()

static func items(view: Object, specs: Array) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for spec_value in specs:
		if not spec_value is Dictionary:
			continue
		var spec: Dictionary = spec_value
		var a_key := str(spec.get("a", ""))
		var b_key := str(spec.get("b", ""))
		if a_key == "" or b_key == "":
			continue
		var a: Vector2 = view.flat_point(a_key)
		var b: Vector2 = view.flat_point(b_key)
		var radius := float(view.geometry_radius()) if view.has_method("geometry_radius") else float(view.radius())
		var elevation_a := float(view.elevation_at(a))
		var elevation_b := float(view.elevation_at(b))
		var across := (b - a).normalized().orthogonal()
		var side_probe: float = float(view.elevation_at(a.lerp(b, 0.5) + across * radius * 0.3))
		var surface_version := str(view.call("geometry_version")) if view.has_method("geometry_version") else ""
		var signature := "%s|%s|%s|%.1f|%.1f|%.1f|%.1f|%.1f|%s|%s|%s|%s|%s|%d|%d|%d" % [
			str(spec.get("id", a_key + "|" + b_key)),
			str(spec.get("project_a", "")), str(spec.get("project_b", "")),
			a.x, a.y, b.x, b.y, radius, surface_version,
			str(roundi(elevation_a * 10.0)), str(roundi(elevation_b * 10.0)), str(roundi(side_probe * 10.0)),
			str(spec.get("kind", "")), int(spec.get("variant", 0)), int(spec.get("tier_a", 1)), int(spec.get("tier_b", 1))]
		if not _geometry_cache.has(signature):
			if _geometry_cache.size() >= MAX_GEOMETRY_CACHE:
				_geometry_cache.clear()
			var geometry := _build_geometry(view, spec, a, b, radius, elevation_a, elevation_b)
			if geometry.is_empty():
				continue
			_geometry_cache[signature] = geometry
		result.append(_geometry_cache[signature])
	result.sort_custom(func(x, y):
		if not is_equal_approx(float(x.depth), float(y.depth)):
			return float(x.depth) < float(y.depth)
		return str(x.key) < str(y.key)
	)
	return result

static func draw_item(c: CanvasItem, _view: Object, item: Dictionary) -> void:
	var shadow: PackedVector2Array = item.get("shadow", PackedVector2Array())
	if shadow.size() >= 3:
		_draw_surface_polygon(c, shadow, Color(0.08, 0.10, 0.12, 0.20))
	var platform: PackedVector2Array = item.get("platform", PackedVector2Array())
	if platform.size() >= 3:
		_draw_surface_polygon(c, platform, item.platform_color)
	var body_faces: Array = item.get("body_faces", [])
	for face in body_faces:
		_draw_surface_polygon(c, face.points, face.color)
	var roof_faces: Array = item.get("roof_faces", [])
	for face in roof_faces:
		_draw_surface_polygon(c, face.points, face.color)
	var stairs: Array = item.get("stairs", [])
	for step in stairs:
		_draw_surface_polygon(c, step.points, step.color)
	for line in item.get("supports", []):
		c.draw_line(line.a, line.b, line.color, line.width, true)
	for line in item.get("details", []):
		c.draw_line(line.a, line.b, line.color, line.width, true)

static func _draw_surface_polygon(c: CanvasItem, points: PackedVector2Array, color: Color) -> void:
	if points.size() == 4:
		# Terrain projection can fold a world-space quad across a slope. Use a
		# diagonal only when both projected triangles have the same winding.
		var candidates := [[0, 1, 2, 0, 2, 3], [1, 2, 3, 1, 3, 0]]
		for candidate in candidates:
			var area_a := _triangle_area(points[candidate[0]], points[candidate[1]], points[candidate[2]])
			var area_b := _triangle_area(points[candidate[3]], points[candidate[4]], points[candidate[5]])
			if absf(area_a) <= 0.05 or absf(area_b) <= 0.05 or area_a * area_b <= 0.0:
				continue
			c.draw_colored_polygon(PackedVector2Array([points[candidate[0]], points[candidate[1]], points[candidate[2]]]), color)
			c.draw_colored_polygon(PackedVector2Array([points[candidate[3]], points[candidate[4]], points[candidate[5]]]), color)
			return
		return
	if points.size() >= 3 and absf(_polygon_area(points)) > 0.05:
		c.draw_colored_polygon(points, color)

static func _triangle_area(a: Vector2, b: Vector2, c: Vector2) -> float:
	return (b - a).cross(c - a) * 0.5

static func _polygon_area(points: PackedVector2Array) -> float:
	var area := 0.0
	for index in points.size():
		var next := (index + 1) % points.size()
		area += points[index].x * points[next].y - points[next].x * points[index].y
	return area * 0.5

static func _build_geometry(view: Object, spec: Dictionary, a: Vector2, b: Vector2, radius: float, elevation_a: float, elevation_b: float) -> Dictionary:
	var height_delta := absf(elevation_b - elevation_a)
	# A short connector across a cliff face reads as a bridge over empty space. Let
	# the neighboring buildings stand independently when the grade is too steep.
	if height_delta > 25.0:
		return {}
	var axis := (b - a).normalized()
	var across := axis.orthogonal()
	var kind := str(spec.get("kind", "mixed_court"))
	var industrial := kind.begins_with("industrial_")
	var residential := kind.begins_with("residential_")
	var shop := kind == "street_shop"
	var base_t0 := 0.18
	var base_t1 := 0.82
	var half_width := radius * (0.25 if industrial else (0.31 if shop else 0.29))
	var body_t0 := 0.29
	var body_t1 := 0.71
	if kind == "residential_annex":
		body_t0 = 0.25
		body_t1 = 0.75
		half_width = radius * 0.34
	elif kind == "industrial_shed":
		body_t0 = 0.24
		body_t1 = 0.76
		half_width = radius * 0.34
	elif kind == "industrial_gallery":
		body_t0 = 0.22
		body_t1 = 0.78
		half_width = radius * 0.22
	elif shop:
		body_t0 = 0.27
		body_t1 = 0.73
		half_width = radius * 0.36
	var platform_points := _surface_quad(view, a.lerp(b, base_t0), a.lerp(b, base_t1), across, half_width * 1.35)
	var shadow_points := _surface_quad(view, a.lerp(b, base_t0) + Vector2(0.0, radius * 0.035), a.lerp(b, base_t1) + Vector2(0.0, radius * 0.035), across, half_width * 1.40)
	var p0 := a.lerp(b, body_t0)
	var p1 := a.lerp(b, body_t1)
	var body_width := half_width * 0.78
	var ground := _surface_quad(view, p0, p1, across, body_width)
	var height := radius * (0.19 if industrial else (0.22 if kind == "residential_annex" or shop else 0.16))
	if int(spec.get("tier_a", 1)) >= 3 or int(spec.get("tier_b", 1)) >= 3:
		height *= 1.12
	var top: Array[Vector2] = []
	for point in ground:
		top.append(point - Vector2(0.0, height))
	var ridge_0 := _surface_point(view, p0.lerp(p1, 0.0)) - Vector2(0.0, height * 1.35)
	var ridge_1 := _surface_point(view, p0.lerp(p1, 1.0)) - Vector2(0.0, height * 1.35)
	var roof_mid_0 := _surface_point(view, p0 + across * body_width * 0.45) - Vector2(0.0, height * 0.76)
	var roof_mid_1 := _surface_point(view, p1 + across * body_width * 0.45) - Vector2(0.0, height * 0.76)
	var roof_mid_2 := _surface_point(view, p1 - across * body_width * 0.45) - Vector2(0.0, height * 0.76)
	var roof_mid_3 := _surface_point(view, p0 - across * body_width * 0.45) - Vector2(0.0, height * 0.76)
	var palette := _palette(kind, int(spec.get("variant", 0)))
	var body_faces: Array[Dictionary] = []
	body_faces.append({"points": PackedVector2Array([ground[0], ground[1], top[1], top[0]]), "color": palette.wall_far})
	body_faces.append({"points": PackedVector2Array([ground[1], ground[2], top[2], top[1]]), "color": palette.wall_end})
	body_faces.append({"points": PackedVector2Array([ground[2], ground[3], top[3], top[2]]), "color": palette.wall_front})
	body_faces.append({"points": PackedVector2Array([ground[3], ground[0], top[0], top[3]]), "color": palette.wall_side})
	var roof_faces: Array[Dictionary] = []
	roof_faces.append({"points": PackedVector2Array([top[0], top[1], ridge_1, ridge_0]), "color": palette.roof_light})
	roof_faces.append({"points": PackedVector2Array([ridge_0, ridge_1, top[2], top[3]]), "color": palette.roof_dark})
	var supports: Array[Dictionary] = []
	if residential or kind == "mixed_court" or kind == "street_shop":
		for t in [body_t0 + 0.04, body_t1 - 0.04]:
			for side in [-1.0, 1.0]:
				var foot := _surface_point(view, a.lerp(b, t) + across * body_width * side * 0.72)
				var cap := foot - Vector2(0.0, height * 0.78)
				supports.append({"a": foot, "b": cap, "color": palette.post, "width": maxf(2.0, radius * 0.035)})
	var details: Array[Dictionary] = []
	if industrial:
		var pipe_offset := across * body_width * 0.53
		for side in [-1.0, 1.0]:
			var start := _surface_point(view, p0 + pipe_offset * side) - Vector2(0.0, height * 0.68)
			var finish := _surface_point(view, p1 + pipe_offset * side) - Vector2(0.0, height * 0.68)
			details.append({"a": start, "b": finish, "color": palette.detail_light, "width": maxf(2.5, radius * 0.055)})
		for t in [0.37, 0.50, 0.63]:
			var brace_a := _surface_point(view, a.lerp(b, t) + pipe_offset) - Vector2(0.0, height * 0.68)
			var brace_b := _surface_point(view, a.lerp(b, t) - pipe_offset) - Vector2(0.0, height * 0.68)
			details.append({"a": brace_a, "b": brace_b, "color": palette.detail_dark, "width": maxf(1.5, radius * 0.025)})
	else:
		details.append({"a": roof_mid_0.lerp(roof_mid_1, 0.50), "b": roof_mid_3.lerp(roof_mid_2, 0.50), "color": palette.ridge, "width": maxf(1.5, radius * 0.025)})
	var stairs: Array[Dictionary] = []
	if height_delta >= 8.0:
		# The footprint remains terrain-conforming; short cross-steps make the grade
		# legible instead of allowing an elevated bridge to hang over the slope.
		for t in [0.30, 0.45, 0.60, 0.75]:
			var center := a.lerp(b, t)
			var stair := _surface_quad(view, center - axis * radius * 0.055, center + axis * radius * 0.055, across, half_width * 1.15)
			stairs.append({"points": stair, "color": palette.step})
	var midpoint := _surface_point(view, a.lerp(b, 0.5))
	return {
		"key": str(spec.get("id", "")), "kind": kind,
		"project_a": str(spec.get("project_a", "")), "project_b": str(spec.get("project_b", "")),
		"depth": midpoint.y + radius * 0.10,
		"shadow": shadow_points, "platform": platform_points,
		"platform_color": palette.platform, "body_faces": body_faces,
		"roof_faces": roof_faces, "supports": supports, "details": details,
		"stairs": stairs, "height_mode": "stepped" if height_delta >= 8.0 else "continuous",
	}

static func _surface_quad(view: Object, start: Vector2, finish: Vector2, across: Vector2, half_width: float) -> PackedVector2Array:
	return PackedVector2Array([
		_surface_point(view, start + across * half_width),
		_surface_point(view, finish + across * half_width),
		_surface_point(view, finish - across * half_width),
		_surface_point(view, start - across * half_width),
	])

static func _surface_point(view: Object, point: Vector2) -> Vector2:
	# Geometry is cached in world coordinates so pan and zoom only move the
	# retained CanvasItem transform. Never bake screen position or zoom into it.
	if view.has_method("project_flat"):
		return view.project_flat(point)
	return view.surface_point(point)

static func _palette(kind: String, variant: int) -> Dictionary:
	if kind.begins_with("industrial_"):
		var tint := Color("718277") if variant % 2 == 0 else Color("7D806A")
		return {"platform": Color("5E665F"), "wall_far": Color("87765C"), "wall_end": Color("9C855F"), "wall_front": Color("74634D"), "wall_side": Color("605747"), "roof_light": tint.lightened(0.12), "roof_dark": tint.darkened(0.22), "post": Color("514B3F"), "detail_light": Color("C49A57"), "detail_dark": Color("4C5550"), "ridge": Color("D2AC68"), "step": Color("B8A27A")}
	if kind.begins_with("residential_"):
		var roof := Color("A75742") if variant % 2 == 0 else Color("8E5D45")
		return {"platform": Color("B6A77D"), "wall_far": Color("C7B88E"), "wall_end": Color("DAC9A0"), "wall_front": Color("AE9C76"), "wall_side": Color("99896A"), "roof_light": roof.lightened(0.10), "roof_dark": roof.darkened(0.18), "post": Color("725C40"), "detail_light": Color("E1C78E"), "detail_dark": Color("68533F"), "ridge": Color("E2BF7D"), "step": Color("D0BD94")}
	var roof := Color("8B5A40") if kind == "street_shop" else Color("8B7550")
	return {"platform": Color("B2A27D"), "wall_far": Color("C3B58F"), "wall_end": Color("D4C39A"), "wall_front": Color("A99875"), "wall_side": Color("928165"), "roof_light": roof.lightened(0.10), "roof_dark": roof.darkened(0.18), "post": Color("705A40"), "detail_light": Color("D5B46D"), "detail_dark": Color("635642"), "ridge": Color("D5B46D"), "step": Color("CBBB96")}
