extends RefCounted
## Detached, committed presentation data. No Game/Planner/Simulation references.
const Elevation = preload("res://ui/map/elevation040.gd")
const Foundation = preload("res://ui/map/foundation041.gd")
const Fabric = preload("res://ui/map/fabric040.gd")
const MAP_UNITS_PER_WORLD_UNIT := 64.0

static func from_display(display: Dictionary, definitions: Dictionary, world_seed: int, visual_seed: int, registry: Dictionary) -> Dictionary:
	if display.get("forecast", true) or str(display.get("source_phase", "")) != "committed_tick":
		return {"ok": false, "error": "Living World requires an explicitly committed display snapshot."}
	if not display.get("tiles") is Dictionary or display.get("tiles", {}).is_empty():
		return {"ok": false, "error": "Committed snapshot has no terrain."}
	var result: Dictionary = {}
	for key in ["tiles", "projects", "groups", "metrics", "compiled", "audit"]:
		result[key] = display.get(key, {}).duplicate(true)
	result.definitions = definitions.duplicate(true)
	result.world_seed = world_seed
	result.visual_seed = visual_seed
	result.snapshot_id = str(display.get("snapshot_id", ""))
	result.tick = int(display.get("tick", 0))
	result.source_phase = "committed_tick"
	result.forecast = false
	result.asset_registry = registry.duplicate(true)
	result.elevation_field = Elevation.derive(result.tiles, world_seed)
	var natural: Callable = func(p: Vector2) -> float: return Elevation.height_at(result.elevation_field, p)
	# Existing helpers read c/s even when their snapshot fields are present.
	# Give them copied dictionaries, never the live Game object.
	var facade := {"c": {"projects": result.definitions}, "compiled": result.compiled,
		"s": {"seed": visual_seed, "cells": result.tiles, "projects": result.projects,
		"groups": result.groups, "metrics": result.metrics}}
	result.foundation_field = Foundation.derive(facade, result, natural)
	_resize_platforms(result.foundation_field, registry)
	result.foundation_items = Foundation.foundation_items(result.foundation_field, natural)
	result.fabric = Fabric.derive(facade, result)
	result.river_path = _river_path(result.tiles)
	result.ground_bounds = _bounds(result.tiles)
	result.foliage_profile = {"forest_candidates": 22, "center_density": 0.92,
		"edge_density": 0.40, "plain_candidates": 2, "plain_density": 0.26,
		"scale_min": 0.80, "scale_max": 1.16, "minimum_spacing": 16.0,
		"river_clearance": 66.0}
	result.ok = true
	result.signature = digest(result)
	return result

static func digest(value: Dictionary) -> String:
	return JSON.stringify(value, "", true, true).sha256_text()

static func _resize_platforms(field: Dictionary, registry: Dictionary) -> void:
	var records := {}
	var entries: Variant = registry.get("assets", [])
	if entries is Dictionary: entries = entries.values()
	for entry in entries: records[str(entry.id)] = entry
	field.site_bins = {}
	for index in field.sites.size():
		var site: Dictionary = field.sites[index]
		var record: Dictionary = records.get(str(registry.get("bindings", {}).get(site.def, "")), {})
		var footprint: Array = record.get("footprint", {}).get("xz_bounds", [])
		if footprint.size() == 4:
			# Runtime transforms use (source position - pivot) * scale. Grade that
			# same full asymmetric rectangle, including non-default registry metadata.
			var scale: Array = record.get("scale", [1.0, 1.0, 1.0])
			var pivot: Array = record.get("pivot", [0.0, 0.0, 0.0])
			var min_x := (float(footprint[0]) - float(pivot[0])) * float(scale[0])
			var min_z := (float(footprint[1]) - float(pivot[2])) * float(scale[2])
			var max_x := (float(footprint[2]) - float(pivot[0])) * float(scale[0])
			var max_z := (float(footprint[3]) - float(pivot[2])) * float(scale[2])
			var position: Vector2 = site.center + Vector2(min_x, -max_z) * 64.0
			var size := Vector2(max_x - min_x, max_z - min_z) * 64.0
			site.runtime_footprint = Rect2(position, size).grow(3.0)
			site.half_x = maxf(absf(min_x), absf(max_x)) * 64.0 * 1.20 + 3.0
			site.half_y = maxf(absf(min_z), absf(max_z)) * 64.0 * 1.20 + 3.0
			site.blend_radius = maxf(float(site.blend_radius), 24.0)
		Foundation._index_rect(field.site_bins, site.center, site.half_x + site.blend_radius, site.half_y + site.blend_radius, index)

static func _bounds(tiles: Dictionary) -> Rect2:
	var bounds := Rect2()
	var first := true
	for tile in tiles.values():
		var p := Elevation.world_center(Vector2i(int(tile.q), int(tile.r)))
		if first: bounds = Rect2(p, Vector2.ZERO); first = false
		else: bounds = bounds.expand(p)
	return bounds.grow(86.0)

static func _river_path(tiles: Dictionary) -> PackedVector2Array:
	var centers: Array[Vector2] = []
	for tile in tiles.values():
		if Foundation._is_water(tile):
			centers.append(Elevation.world_center(Vector2i(int(tile.q), int(tile.r))))
	centers.sort_custom(func(a: Vector2, b: Vector2): return a.y < b.y if a.y != b.y else a.x < b.x)
	if centers.size() < 2: return PackedVector2Array()
	# Same continuous cubic-curve approach as the legacy renderer, in world space.
	var bounds := _bounds(tiles)
	centers.push_front(centers[0] + (centers[0] - centers[1]).normalized() * bounds.size.length())
	centers.append(centers[-1] + (centers[-1] - centers[-2]).normalized() * bounds.size.length())
	var path := PackedVector2Array()
	for i in centers.size() - 1:
		var a: Vector2 = centers[maxi(0, i - 1)]
		var b: Vector2 = centers[i]
		var c: Vector2 = centers[i + 1]
		var d: Vector2 = centers[mini(centers.size() - 1, i + 2)]
		var length := b.distance_to(c)
		var h1 := b + (c - a).normalized() * length * 0.32
		var h2 := c - (d - b).normalized() * length * 0.32
		var samples := maxi(12, int(length / 8.0))
		for j in samples: path.append(b.bezier_interpolate(h1, h2, c, float(j) / samples))
	path.append(centers[-1])
	# A small continuous within-channel meander softens straight authored runs;
	# the logical water cells and source curve remain unchanged.
	var meander := PackedVector2Array()
	for index in path.size():
		var point := path[index]
		var direction := (path[mini(index + 1, path.size() - 1)] - path[maxi(0, index - 1)]).normalized()
		var offset := sin(point.y * 0.014 + point.x * 0.004) * 4.0 + sin(point.y * 0.036 - 0.5) * 2.0
		meander.append(point + direction.orthogonal() * offset)
	return meander
