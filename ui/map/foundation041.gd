extends RefCounted
## Presentation-only grading for placed projects. Derive once from a committed
## game snapshot, then use height_at() for map ground and foundation_items()
## for mesh-ready world-space vertices. Coordinates are unprojected map units.
## No simulation state is written, and this helper never reads or advances RNG.

const SQRT3 := 1.7320508075688772
const CELL_RADIUS := 64.0
const ROW_STEP := 96.0
const AtlasStyle=preload("res://ui/map/atlas_style.gd")
const BIN_SIZE := 128.0
const MIN_BLEND := 20.0
const MAX_BLEND := 60.0
const BLEND_PER_HEIGHT := 2.8
const MIN_HALF_X := 18.0
const MIN_HALF_Y := 16.0
const MAX_HALF_X := 58.0
const MAX_HALF_Y := 54.0
const FOOTPRINT_MARGIN := 3.0
const WATER_RADIUS := 64.5
const RING_STEPS := [0.0, 0.35, 0.70, 1.0]
const OUTLINE_SEGMENTS := 24
const RETAINING_WALL_MIN_HEIGHT := 3.0
const ART_CATALOG_PATH := "res://content/presentation040/art_catalog.json"

static var _asset_sizes: Dictionary = {}
static var _catalog_cache: Dictionary = {}


static func derive(g: Variant, snapshot: Dictionary, natural_height_callable: Callable) -> Dictionary:
	## Projects (including construction sites) are read from snapshot or game state.
	## Each occupied non-water tile gets a small platform matching its sprite bounds.
	var state: Variant = _get_value(g, "s", {})
	var projects: Variant = snapshot.get("projects", _get_value(state, "projects", {}))
	var tiles: Variant = snapshot.get("tiles", _get_value(state, "cells", {}))
	if not projects is Dictionary:
		projects = {}
	if not tiles is Dictionary:
		tiles = {}
	_ensure_asset_sizes()

	var water_bins: Dictionary = {}
	var water_centers: Array[Vector2] = []
	var tile_keys: Array = tiles.keys()
	tile_keys.sort_custom(func(a, b): return str(a) < str(b))
	for key in tile_keys:
		var tile: Variant = tiles[key]
		if not tile is Dictionary or not _is_water(tile):
			continue
		var parsed := _parse_axial(key, tile)
		if not bool(parsed.get("valid", false)):
			continue
		var center := _world_center(parsed["axial"])
		water_centers.append(center)
		_index_circle(water_bins, center, WATER_RADIUS, water_centers.size() - 1)

	var sites: Array[Dictionary] = []
	var site_bins: Dictionary = {}
	var project_ids: Array = projects.keys()
	project_ids.sort_custom(func(a, b): return str(a) < str(b))
	for project_id_value in project_ids:
		var raw_project: Variant = projects[project_id_value]
		if not raw_project is Dictionary:
			continue
		var project: Dictionary = raw_project
		var tile_value: Variant = project.get("tile", project.get("tile_key", ""))
		var axial_info := _parse_axial(tile_value, {})
		if not bool(axial_info.get("valid", false)):
			continue
		var tile_key := _axial_key(axial_info["axial"])
		var tile: Variant = tiles.get(tile_key, {})
		if not tile is Dictionary:
			tile = {}
		if _is_water(tile):
			continue
		var center := _world_center(axial_info["axial"])
		var def_id := str(project.get("def", ""))
		var definition: Dictionary = g.c.projects.get(def_id, {})
		var terrain_following: bool = "building.green" in definition.get("traits", [])
		var footprint := _footprint_for(def_id)
		var platform_height := _call_height(natural_height_callable, center)
		var max_grade_delta := 0.0
		for vertex_index in OUTLINE_SEGMENTS:
			var edge_point := _outline_point(center, float(footprint["half_x"]), float(footprint["half_y"]), vertex_index)
			max_grade_delta = maxf(max_grade_delta, absf(_call_height(natural_height_callable, edge_point) - platform_height))
		var blend_radius := clampf(maxf(MIN_BLEND, max_grade_delta * BLEND_PER_HEIGHT), MIN_BLEND, MAX_BLEND)
		var site := {
			"project_id": str(project_id_value),
			"tile": tile_key,
			"def": def_id,
			"asset_id": str(footprint["asset_id"]),
			"center": center,
			"platform_height": platform_height,
			"half_x": float(footprint["half_x"]),
			"half_y": float(footprint["half_y"]),
			"blend_radius": blend_radius,
			"terrain_following": terrain_following,
		}
		var site_index := sites.size()
		sites.append(site)
		_index_rect(site_bins, center, float(site["half_x"]) + blend_radius, float(site["half_y"]) + blend_radius, site_index)

	return {
		"version": 1,
		"bin_size": BIN_SIZE,
		"sites": sites,
		"site_bins": site_bins,
		"water_centers": water_centers,
		"water_bins": water_bins,
		"source_project_count": projects.size(),
	}


static func height_at(field: Dictionary, p: Vector2, natural_height: Callable) -> float:
	## Returns natural terrain outside sites and a smooth, locally levelled grade
	## around them. Query cost is limited to sites in p's spatial bin.
	var natural := _call_height(natural_height, p)
	var bin := _bin_for(p, float(field.get("bin_size", BIN_SIZE)))
	for water_index in _indices_at(field.get("water_bins", {}), bin):
		var water_centers: Array = field.get("water_centers", [])
		if water_index >= 0 and water_index < water_centers.size() and p.distance_to(water_centers[water_index]) <= WATER_RADIUS:
			return natural

	var sites: Array = field.get("sites", [])
	var candidates: Array = _indices_at(field.get("site_bins", {}), bin)
	var best_core_index := -1
	var best_core_distance := INF
	var blended_weight := 0.0
	var remaining_blend := 1.0
	var blended_height := 0.0
	for index_value in candidates:
		var index := int(index_value)
		if index < 0 or index >= sites.size():
			continue
		var site: Dictionary = sites[index]
		var center: Vector2 = site["center"]
		var offset := p - center
		var half_x := float(site["half_x"])
		var half_y := float(site["half_y"])
		if bool(site.get("terrain_following", false)):
			continue
		var normalized_radius := _superellipse_radius(offset, half_x, half_y)
		var core := normalized_radius <= 1.0
		var platform := float(site["platform_height"])
		if core:
			var center_distance := offset.length_squared()
			if center_distance < best_core_distance:
				best_core_distance = center_distance
				best_core_index = index
			continue
		var distance_to_footprint := (normalized_radius - 1.0) * minf(half_x, half_y)
		var blend_radius := float(site.get("blend_radius", MIN_BLEND))
		if distance_to_footprint >= blend_radius:
			continue
		var weight := 1.0 - _smoothstep(0.0, blend_radius, distance_to_footprint)
		blended_height += platform * weight
		blended_weight += weight
		remaining_blend *= 1.0 - weight

	if best_core_index >= 0:
		return float(sites[best_core_index]["platform_height"])
	if blended_weight <= 0.0:
		return natural
	var blend := 1.0 - remaining_blend
	return lerpf(natural, blended_height / blended_weight, blend)


static func foundation_items(field: Dictionary, natural_height: Callable) -> Array[Dictionary]:
	## Mesh-ready data in unprojected world coordinates. Each item's
	## platform_triangles fill the level top, edge_triangles grade through three
	## rings to natural terrain, and retaining_walls contains short vertical
	## faces where the local cut/fill difference is visually significant.
	var result: Array[Dictionary] = []
	for site_value in field.get("sites", []):
		if not site_value is Dictionary:
			continue
		var site: Dictionary = site_value
		if bool(site.get("terrain_following", false)):
			continue
		var center: Vector2 = site["center"]
		var half_x := float(site["half_x"])
		var half_y := float(site["half_y"])
		var platform := float(site["platform_height"])
		var rings: Array[PackedVector3Array] = []
		var blend_radius := float(site.get("blend_radius", MIN_BLEND))
		for step in RING_STEPS:
			var ring := PackedVector3Array()
			for vertex_index in OUTLINE_SEGMENTS:
				var point := _outline_point(center, half_x + blend_radius * float(step), half_y + blend_radius * float(step), vertex_index)
				var terrain_height := _call_height(natural_height, point)
				var rise := _smoothstep(0.0, 1.0, float(step))
				var ring_height := lerpf(platform, terrain_height, rise)
				ring.append(Vector3(point.x, point.y, ring_height))
			rings.append(ring)

		var top_ring: PackedVector3Array = rings[0]
		var platform_triangles := PackedVector3Array()
		var center_vertex := Vector3(center.x, center.y, platform)
		for vertex_index in OUTLINE_SEGMENTS:
			platform_triangles.append(center_vertex)
			platform_triangles.append(top_ring[vertex_index])
			platform_triangles.append(top_ring[(vertex_index + 1) % OUTLINE_SEGMENTS])
		var edge_triangles := PackedVector3Array()
		for ring_index in range(rings.size() - 1):
			var inner: PackedVector3Array = rings[ring_index]
			var outer: PackedVector3Array = rings[ring_index + 1]
			for vertex_index in OUTLINE_SEGMENTS:
				var next_index := (vertex_index + 1) % OUTLINE_SEGMENTS
				_append_quad(edge_triangles, inner[vertex_index], outer[vertex_index], outer[next_index], inner[next_index])

		var retaining_walls := PackedVector3Array()
		for vertex_index in OUTLINE_SEGMENTS:
			var next_index := (vertex_index + 1) % OUTLINE_SEGMENTS
			var point_a := Vector2(top_ring[vertex_index].x, top_ring[vertex_index].y)
			var point_b := Vector2(top_ring[next_index].x, top_ring[next_index].y)
			var ground_a := _call_height(natural_height, point_a)
			var ground_b := _call_height(natural_height, point_b)
			if maxf(absf(platform - ground_a), absf(platform - ground_b)) < RETAINING_WALL_MIN_HEIGHT:
				continue
			var top_a := Vector3(point_a.x, point_a.y, platform)
			var top_b := Vector3(point_b.x, point_b.y, platform)
			var wall_a := clampf(ground_a, platform - 12.0, platform + 12.0)
			var wall_b := clampf(ground_b, platform - 12.0, platform + 12.0)
			var bottom_a := Vector3(point_a.x, point_a.y, wall_a)
			var bottom_b := Vector3(point_b.x, point_b.y, wall_b)
			_append_quad(retaining_walls, top_a, bottom_a, bottom_b, top_b)

		result.append({
			"project_id": str(site.get("project_id", "")),
			"tile": str(site.get("tile", "")),
			"def": str(site.get("def", "")),
			"asset_id": str(site.get("asset_id", "")),
			"center": center,
			"platform_height": platform,
			"half_size": Vector2(half_x, half_y),
			"rings": rings,
			"platform_triangles": platform_triangles,
			"edge_triangles": edge_triangles,
			"retaining_walls": retaining_walls,
		})
	return result


static func _footprint_for(def_id: String) -> Dictionary:
	var asset_id := ""
	if _asset_sizes.has("building_" + def_id):
		asset_id = "building_" + def_id
	elif _asset_sizes.has(def_id):
		asset_id = def_id
	else:
		var data := _load_catalog()
		for table_name in ["aliases", "legacy_aliases", "bindings"]:
			var table: Variant = data.get(table_name, {})
			if not table is Dictionary:
				continue
			var mapped := str(table.get(def_id, table.get("building." + def_id, "")))
			if _asset_sizes.has(mapped):
				asset_id = mapped
				break
	if asset_id == "" and _asset_sizes.has("building_homestead"):
		asset_id = "building_homestead"
	var size: Array = _asset_sizes.get(asset_id, [20.0, 18.0])
	return {
		"asset_id": asset_id,
		"half_x": clampf(float(size[0]) * AtlasStyle.building_scale(def_id) * 0.5 + FOOTPRINT_MARGIN, MIN_HALF_X, MAX_HALF_X),
		"half_y": clampf(float(size[1]) * AtlasStyle.building_scale(def_id) * 0.5 + FOOTPRINT_MARGIN, MIN_HALF_Y, MAX_HALF_Y),
	}


static func _ensure_asset_sizes() -> void:
	if not _asset_sizes.is_empty():
		return
	var assets: Variant = _load_catalog().get("assets", {})
	if not assets is Dictionary:
		return
	for id_value in assets:
		var id := str(id_value)
		var item: Variant = assets[id_value]
		if not item is Dictionary:
			continue
		var footprint: Variant = item.get("footprint_world_bounds", [])
		if footprint is Array and footprint.size() >= 4:
			var footprint_width := absf(float(footprint[2]) - float(footprint[0]))
			var footprint_depth := absf(float(footprint[3]) - float(footprint[1]))
			if footprint_width > 0.0 and footprint_depth > 0.0:
				_asset_sizes[id] = [footprint_width, footprint_depth]
				continue
		var bounds: Variant = item.get("world_bounds", [])
		if bounds is Array and bounds.size() >= 5:
			var width := absf(float(bounds[3]) - float(bounds[0]))
			var depth := absf(float(bounds[4]) - float(bounds[1]))
			if width > 0.0 and depth > 0.0:
				_asset_sizes[id] = [width, depth]
				continue
		if item.get("kind", "") == "building":
			_asset_sizes[id] = [20.0, 18.0]


static func _load_catalog() -> Dictionary:
	if not _catalog_cache.is_empty():
		return _catalog_cache
	if not FileAccess.file_exists(ART_CATALOG_PATH):
		return {}
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(ART_CATALOG_PATH))
	_catalog_cache = parsed if parsed is Dictionary else {}
	return _catalog_cache


static func _parse_axial(value: Variant, cell: Dictionary) -> Dictionary:
	if cell.has("q") and cell.has("r"):
		return {"valid": true, "axial": Vector2i(int(cell["q"]), int(cell["r"]))}
	if value is Vector2i:
		return {"valid": true, "axial": value}
	if value is Array and value.size() >= 2:
		return {"valid": true, "axial": Vector2i(int(value[0]), int(value[1]))}
	var pieces := str(value).split(",", false, 1)
	if pieces.size() == 2 and pieces[0].is_valid_int() and pieces[1].is_valid_int():
		return {"valid": true, "axial": Vector2i(int(pieces[0]), int(pieces[1]))}
	return {"valid": false, "axial": Vector2i.ZERO}


static func _world_center(axial: Vector2i) -> Vector2:
	return Vector2(SQRT3 * CELL_RADIUS * (float(axial.x) + float(axial.y) * 0.5), ROW_STEP * float(axial.y))


static func _axial_key(axial: Vector2i) -> String:
	return "%d,%d" % [axial.x, axial.y]


static func _is_water(tile: Dictionary) -> bool:
	var terrain := str(tile.get("terrain", "plain")).to_lower()
	if terrain in ["water", "sea", "ocean", "river", "channel"] or bool(tile.get("water", false)) or bool(tile.get("is_water", false)):
		return true
	var traits: Variant = tile.get("traits", [])
	if traits is Array or traits is PackedStringArray:
		for trait_value in traits:
			if str(trait_value) in ["tile.water", "water", "tile.river"]:
				return true
	return false


static func _get_value(source: Variant, key: String, default_value: Variant) -> Variant:
	if source is Dictionary:
		return source.get(key, default_value)
	if source is Object:
		var value: Variant = source.get(key)
		return default_value if value == null else value
	return default_value


static func _call_height(height_callable: Callable, point: Vector2) -> float:
	if not height_callable.is_valid():
		return 0.0
	var result: Variant = height_callable.call(point)
	return float(result) if result is int or result is float else 0.0


static func _index_rect(bins: Dictionary, center: Vector2, half_x: float, half_y: float, index: int) -> void:
	var first_x := int(floor((center.x - half_x) / BIN_SIZE))
	var last_x := int(floor((center.x + half_x) / BIN_SIZE))
	var first_y := int(floor((center.y - half_y) / BIN_SIZE))
	var last_y := int(floor((center.y + half_y) / BIN_SIZE))
	for x in range(first_x, last_x + 1):
		for y in range(first_y, last_y + 1):
			_add_bin_index(bins, Vector2i(x, y), index)


static func _index_circle(bins: Dictionary, center: Vector2, radius: float, index: int) -> void:
	var first_x := int(floor((center.x - radius) / BIN_SIZE))
	var last_x := int(floor((center.x + radius) / BIN_SIZE))
	var first_y := int(floor((center.y - radius) / BIN_SIZE))
	var last_y := int(floor((center.y + radius) / BIN_SIZE))
	for x in range(first_x, last_x + 1):
		for y in range(first_y, last_y + 1):
			_add_bin_index(bins, Vector2i(x, y), index)


static func _add_bin_index(bins: Dictionary, key: Vector2i, index: int) -> void:
	var values: Array = bins.get(key, [])
	values.append(index)
	bins[key] = values


static func _indices_at(bins: Variant, key: Vector2i) -> Array:
	if not bins is Dictionary:
		return []
	return bins.get(key, [])


static func _bin_for(point: Vector2, bin_size: float) -> Vector2i:
	return Vector2i(int(floor(point.x / bin_size)), int(floor(point.y / bin_size)))


static func _outline_point(center: Vector2, half_x: float, half_y: float, index: int) -> Vector2:
	var angle := TAU * float(index) / float(OUTLINE_SEGMENTS)
	var cosine := cos(angle)
	var sine := sin(angle)
	# Superellipse exponent keeps a compact, softly rounded rectangle footprint.
	var x := signf(cosine) * pow(absf(cosine), 0.68) * half_x
	var y := signf(sine) * pow(absf(sine), 0.68) * half_y
	return center + Vector2(x, y)


static func _superellipse_radius(offset: Vector2, half_x: float, half_y: float) -> float:
	const EXPONENT := 1.4705882352941178
	var x := pow(absf(offset.x) / maxf(half_x, 0.001), EXPONENT)
	var y := pow(absf(offset.y) / maxf(half_y, 0.001), EXPONENT)
	return pow(x + y, 1.0 / EXPONENT)


static func _append_quad(vertices: PackedVector3Array, a: Vector3, b: Vector3, c: Vector3, d: Vector3) -> void:
	vertices.append(a)
	vertices.append(b)
	vertices.append(c)
	vertices.append(a)
	vertices.append(c)
	vertices.append(d)


static func _smoothstep(edge0: float, edge1: float, value: float) -> float:
	var t := clampf((value - edge0) / (edge1 - edge0), 0.0, 1.0)
	return t * t * (3.0 - 2.0 * t)
