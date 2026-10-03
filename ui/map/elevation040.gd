extends RefCounted
## Presentation-only continuous terrain height field.
##
## Call derive(cells, seed) once per displayed map and cache its result. Cell
## centers use the unprojected map plane: x=sqrt(3)*64*(q+r*0.5), y=96*r.
## height_at(field, p) accepts that same world-space Vector2 and returns a
## vertical offset in map-world units; gradient_at returns its x/y slope for
## mesh shading. No simulation state, entities, or RNG are read or changed.
##
## Hill cells blend into regional upland plateaus near 61 units; spaced summit
## controls rise to 65-90, with 58-92 unit buildable tops and 240-324 unit
## shoulders. Plain/wet/forest controls stay near 2.5/1.5/7. Water hexes and
## waterfront cell centers are 0, with smooth fades into the lowland valley.

const SQRT3 := 1.7320508075688772
const CELL_RADIUS := 64.0
const ROW_STEP := 96.0
const CENTER_SPACING := SQRT3 * CELL_RADIUS
const WATER_EDGE := 64.0
const WATER_FADE_END := 160.0
const BANK_FADE_RADIUS := 92.0
const LOWLAND_SIGMA := 142.0
const HEIGHT_CEILING := 100.0
const HILL_PEAK_SPACING := 265.0
const AXIAL_DIRECTIONS := [
	Vector2i(1, 0), Vector2i(1, -1), Vector2i(0, -1),
	Vector2i(-1, 0), Vector2i(-1, 1), Vector2i(0, 1),
]


static func derive(cells: Dictionary, seed_value: int) -> Dictionary:
	var lowlands: Array[Dictionary] = []
	var hill_candidates: Array[Dictionary] = []
	var water_centers: Array[Vector2] = []
	var bank_centers: Array[Vector2] = []
	var records: Array[Dictionary] = []
	var water_keys: Dictionary = {}
	var ordered_keys: Array = cells.keys()
	ordered_keys.sort_custom(func(a, b): return str(a) < str(b))

	for key in ordered_keys:
		var raw: Variant = cells[key]
		if not raw is Dictionary:
			continue
		var cell: Dictionary = raw
		var parsed := _cell_axial(key, cell)
		if not parsed.get("valid", false):
			continue
		var axial: Vector2i = parsed["axial"]
		var axial_key := _axial_key(axial)
		var terrain := str(cell.get("terrain", "plain")).to_lower()
		var is_water := _is_water(cell, terrain)
		var position := world_center(axial)
		var record := {
			"key": str(key), "axial": axial, "axial_key": axial_key,
			"terrain": terrain, "position": position,
			"water": is_water,
			"waterfront": bool(cell.get("waterfront", false)) or terrain in ["shore", "bank", "riverbank"],
		}
		records.append(record)
		if is_water:
			water_keys[axial_key] = true
			water_centers.append(position)

	for record in records:
		var axial: Vector2i = record["axial"]
		var is_bank := bool(record["waterfront"])
		for direction in AXIAL_DIRECTIONS:
			var neighbor_key := _axial_key(axial + direction)
			if water_keys.has(neighbor_key):
				is_bank = true
				break
		if is_bank and not bool(record["water"]):
			bank_centers.append(record["position"])

		var terrain := str(record["terrain"])
		var base_height := 2.5
		if bool(record["water"]):
			base_height = 0.0
		elif terrain == "hill" or terrain == "hills":
			base_height = 22.0
		elif terrain == "forest":
			base_height = 7.0
		elif terrain == "wet" or terrain == "marsh":
			base_height = 1.5
		lowlands.append({"position": record["position"], "height": base_height})

		if terrain == "hill" or terrain == "hills":
			var position: Vector2 = record["position"]
			var clearance := _nearest_distance(position, water_centers)
			var score_text := "%d|%s" % [seed_value, record["axial_key"]]
			var score := _stable_hash(score_text)
			hill_candidates.append({
				"key": record["axial_key"], "position": position,
				"score": score % 100000 + int(clearance * 120.0),
				"hash": score,
			})

	# A deterministic, seed-prioritized Poisson selection creates a few broad
	# landforms over each large hill patch rather than one cone per hill cell.
	hill_candidates.sort_custom(func(a, b):
		if int(a["score"]) != int(b["score"]):
			return int(a["score"]) > int(b["score"])
		return str(a["key"]) < str(b["key"])
	)
	var peaks: Array[Dictionary] = []
	for candidate in hill_candidates:
		var center: Vector2 = candidate["position"]
		var spaced := true
		for peak in peaks:
			if center.distance_to(peak["position"]) < HILL_PEAK_SPACING:
				spaced = false
				break
		if not spaced:
			continue
		var key_text := "%d|%s" % [seed_value, candidate["key"]]
		var h: int = candidate["hash"]
		center += Vector2(_signed_hash(key_text + "|x"), _signed_hash(key_text + "|y")) * 10.0
		peaks.append({
			"position": center,
			"height": 65.0 + float(h % 26),
			"plateau_radius": 58.0 + float((h >> 5) % 35),
			"support_radius": 240.0 + float((h >> 11) % 85),
			"angle": float(h % 628) * 0.01,
			"source_cell": str(candidate["key"]),
		})

	return {
		"version": 1,
		"seed": seed_value,
		"lowlands": lowlands,
		"peaks": peaks,
		"water_centers": water_centers,
		"bank_centers": bank_centers,
		"height_ceiling": HEIGHT_CEILING,
	}


static func height_at(field: Dictionary, p: Vector2) -> float:
	var lowland := _lowland_height(field.get("lowlands", []), p)
	var remaining := 1.0
	for peak in field.get("peaks", []):
		var center: Vector2 = peak["position"]
		var offset := (p - center).rotated(float(peak.get("angle", 0.0)))
		var angle := atan2(offset.y, offset.x)
		var distance := Vector2(offset.x * 0.78, offset.y * 1.23).length()
		# Irregular elongated shoulders join into ridges, avoiding round mounds.
		distance *= 1.0 + sin(angle * 3.0 + 0.7) * 0.15 + cos(angle * 5.0) * 0.07
		var core := float(peak["plateau_radius"]) * 0.72
		var shape := _hill_profile(distance, core, float(peak["support_radius"]))
		var shoulder := _smoothstep(core, core + 55.0, distance)
		shape *= 1.0 - shoulder * (0.08 + 0.10 * sin(offset.x * 0.030 + offset.y * 0.018))
		var contribution := clampf(float(peak["height"]) * shape / HEIGHT_CEILING, 0.0, 0.999)
		remaining *= 1.0 - contribution
	var hill_height := HEIGHT_CEILING * (1.0 - remaining)
	if lowland <= 0.0 and hill_height <= 0.0:
		return 0.0
	var elevation := _smooth_max(lowland, hill_height, 2.5)

	var water_centers: Array = field.get("water_centers", [])
	if not water_centers.is_empty():
		var water_factor := 1.0
		for center in water_centers:
			var water_distance := p.distance_to(center)
			if water_distance <= WATER_EDGE:
				return 0.0
			water_factor *= _smoothstep(WATER_EDGE, WATER_FADE_END, water_distance)
		elevation *= water_factor

	var bank_centers: Array = field.get("bank_centers", [])
	if not bank_centers.is_empty():
		var bank_factor := 1.0
		for center in bank_centers:
			bank_factor *= _smoothstep(0.0, BANK_FADE_RADIUS, p.distance_to(center))
		elevation *= bank_factor
	return clampf(elevation, 0.0, HEIGHT_CEILING)


static func gradient_at(field: Dictionary, p: Vector2) -> Vector2:
	## Center-difference terrain slope, in elevation units per map-world unit.
	const DELTA := 2.0
	var x_slope := (height_at(field, p + Vector2(DELTA, 0.0)) - height_at(field, p - Vector2(DELTA, 0.0))) / (2.0 * DELTA)
	var y_slope := (height_at(field, p + Vector2(0.0, DELTA)) - height_at(field, p - Vector2(0.0, DELTA))) / (2.0 * DELTA)
	return Vector2(x_slope, y_slope)


static func world_center(axial: Vector2i) -> Vector2:
	return Vector2(SQRT3 * CELL_RADIUS * (float(axial.x) + float(axial.y) * 0.5), ROW_STEP * float(axial.y))


static func _cell_axial(key: Variant, cell: Dictionary) -> Dictionary:
	if cell.has("q") and cell.has("r"):
		return {"valid": true, "axial": Vector2i(int(cell["q"]), int(cell["r"]))}
	if key is Vector2i:
		return {"valid": true, "axial": key}
	var pieces := str(key).split(",", false, 1)
	if pieces.size() == 2 and pieces[0].is_valid_int() and pieces[1].is_valid_int():
		return {"valid": true, "axial": Vector2i(int(pieces[0]), int(pieces[1]))}
	return {"valid": false, "axial": Vector2i.ZERO}


static func _is_water(cell: Dictionary, terrain: String) -> bool:
	if terrain in ["water", "river", "channel"] or bool(cell.get("water", false)) or bool(cell.get("is_water", false)):
		return true
	var traits: Variant = cell.get("traits", [])
	if traits is Array or traits is PackedStringArray:
		for trait_value in traits:
			if str(trait_value) in ["tile.water", "water", "tile.river"]:
				return true
	return false


static func _axial_key(axial: Vector2i) -> String:
	return "%d,%d" % [axial.x, axial.y]


static func _lowland_height(controls: Array, p: Vector2) -> float:
	if controls.is_empty():
		return 0.0
	var weight_sum := 0.0
	var height_sum := 0.0
	var sigma_squared := LOWLAND_SIGMA * LOWLAND_SIGMA
	for control in controls:
		var center: Vector2 = control["position"]
		var distance_squared := p.distance_squared_to(center)
		var weight := exp(-distance_squared / (2.0 * sigma_squared))
		weight_sum += weight
		height_sum += weight * float(control["height"])
	# A small zero-height background weight fades the field outside the map.
	# Dividing by tiny sums then cutting to zero made a visible distant cliff.
	return height_sum / (weight_sum + 0.015)


static func _hill_profile(distance: float, plateau_radius: float, support_radius: float) -> float:
	if distance <= plateau_radius:
		return 1.0
	if distance >= support_radius:
		return 0.0
	var t := (distance - plateau_radius) / (support_radius - plateau_radius)
	return 1.0 - t * t * (3.0 - 2.0 * t)


static func _smoothstep(edge0: float, edge1: float, value: float) -> float:
	var t := clampf((value - edge0) / (edge1 - edge0), 0.0, 1.0)
	return t * t * (3.0 - 2.0 * t)


static func _smooth_max(a: float, b: float, softness: float) -> float:
	var difference := absf(a - b)
	return maxf(a, b) + 0.5 * (sqrt(difference * difference + softness * softness) - difference)


static func _nearest_distance(p: Vector2, centers: Array) -> float:
	if centers.is_empty():
		return INF
	var best_squared := INF
	for center in centers:
		best_squared = minf(best_squared, p.distance_squared_to(center))
	return sqrt(best_squared)


static func _stable_hash(value: String) -> int:
	var result := 5381
	for i in value.length():
		result = int(((result << 5) + result) ^ value.unicode_at(i)) & 0x7fffffff
	return result


static func _signed_hash(value: String) -> float:
	return float(_stable_hash(value) % 2001) / 1000.0 - 1.0
