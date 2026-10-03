extends RefCounted
## Detached Fabric.derive output becomes terrain-following lanes and small
## original street modules. These nodes are never simulation entities.

const Terrain = preload("res://ui/world3d/terrain/terrain_builder.gd")
const Buildings = preload("res://ui/world3d/city/building_renderer.gd")
const ROAD_HALF_WIDTH := 4.5
const ROAD_LIFT := 0.55


static func build(snapshot: Dictionary) -> Dictionary:
	var root := Node3D.new()
	root.name = "UrbanFabric"
	var fabric: Dictionary = snapshot.get("fabric", {})
	var sites_by_tile: Dictionary = {}
	for value in snapshot.get("foundation_field", {}).get("sites", []):
		if value is Dictionary:
			sites_by_tile[str(value.get("tile", ""))] = value
	var road_material := _material(Color("797F77"))
	var verge_material := _material(Color("B5B8A0"))
	var wall_material := _material(Color("C8C1A3"))
	var roof_material := _material(Color("6D8580"))
	var timber_material := _material(Color("967455"))
	var placements: Array[Dictionary] = []
	var lane_triangles := 0
	var lane_count := 0
	var annex_count := 0
	var prop_count := 0
	var connectors: Array = fabric.get("connectors", [])
	for value in connectors:
		if not value is Dictionary:
			continue
		var spec: Dictionary = value
		var a := Buildings.tile_center(str(spec.get("a", "")))
		var b := Buildings.tile_center(str(spec.get("b", "")))
		var direction := (b - a).normalized()
		if direction.length_squared() < 0.1:
			continue
		var start := a + direction * _site_radius(sites_by_tile.get(str(spec.get("a", "")), {}), direction)
		var finish := b - direction * _site_radius(sites_by_tile.get(str(spec.get("b", "")), {}), direction)
		if (finish - start).dot(direction) < 2.0:
			continue
		var lane := _ribbon(snapshot, start, finish, ROAD_HALF_WIDTH, road_material)
		lane.name = "Lane_" + str(spec.get("id", "")).validate_node_name()
		lane.set_meta("source_relation", spec.duplicate(true))
		root.add_child(lane)
		lane_triangles += 24
		lane_count += 1
		for side in [-1.0, 1.0]:
			var shift := direction.orthogonal() * (ROAD_HALF_WIDTH + 0.5) * float(side)
			var verge := _ribbon(snapshot, start + shift, finish + shift, 0.35, verge_material)
			root.add_child(verge)
			lane_triangles += 24
		placements.append({"id": str(spec.get("id", "")), "kind": "lane", "start": start,
			"finish": finish, "source_strength": float(spec.get("strength", 0.0))})
		# The same subset rule as the retained 2D connector keeps open streets.
		if int(spec.get("variant", 0)) != 0:
			continue
		var middle := start.lerp(finish, 0.5) + direction.orthogonal() * 15.0
		if _inside_site(middle, sites_by_tile.values(), 1.0):
			continue
		var annex := _annex(snapshot, middle, direction, wall_material, roof_material)
		annex.name = "Annex_" + str(spec.get("id", "")).validate_node_name()
		root.add_child(annex)
		annex_count += 1
		placements.append({"id": str(spec.get("id", "")), "kind": "annex", "position": middle})
	for value in fabric.get("fillers", []):
		if not value is Dictionary:
			continue
		var spec: Dictionary = value
		var asset := str(spec.get("asset", ""))
		# Trees use the single instanced prototype in FoliageScatter; unspecified
		# flowers/shrubs are deliberately deferred rather than inventing species.
		if asset not in ["bench", "crates", "lamp", "stall", "fence"]:
			continue
		var a := Buildings.tile_center(str(spec.get("a", "")))
		var b := Buildings.tile_center(str(spec.get("b", "")))
		var axis := (b - a).normalized()
		var point := a.lerp(b, 0.42 if int(spec.get("variant", 0)) % 2 == 0 else 0.61)
		point -= axis.orthogonal() * 18.0
		if _inside_site(point, sites_by_tile.values(), 1.0):
			continue
		var prop := _street_prop(snapshot, point, axis, asset, timber_material, roof_material)
		root.add_child(prop)
		prop_count += 1
		placements.append({"id": str(spec.get("key", "")) + ":" + str(spec.get("variant", 0)),
			"kind": asset, "position": point, "source_strength": float(spec.get("strength", 0.0))})
	return {"node": root, "placements": placements, "signature": Buildings.placement_signature(placements),
		"stats": {"lanes": lane_count, "lane_triangles": lane_triangles, "annexes": annex_count,
			"street_props": prop_count, "source_connectors": connectors.size(), "presentation_only": true}}


static func _ribbon(snapshot: Dictionary, start: Vector2, finish: Vector2, half_width: float, material: Material) -> MeshInstance3D:
	var across := (finish - start).normalized().orthogonal() * half_width
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	for index in 12:
		var a := start.lerp(finish, float(index) / 12.0)
		var b := start.lerp(finish, float(index + 1) / 12.0)
		var points := [a + across, b + across, b - across, a - across]
		for vertex_index in [0, 1, 2, 0, 2, 3]:
			var point: Vector2 = points[vertex_index]
			var dx := (Terrain.height_at(snapshot, point + Vector2(2.0, 0.0)) - Terrain.height_at(snapshot, point - Vector2(2.0, 0.0))) / 4.0
			var dy := (Terrain.height_at(snapshot, point + Vector2(0.0, 2.0)) - Terrain.height_at(snapshot, point - Vector2(0.0, 2.0))) / 4.0
			surface.set_normal(Vector3(-dx, 1.0, dy).normalized())
			surface.add_vertex(Buildings.world_position(point, Terrain.height_at(snapshot, point) + ROAD_LIFT))
	var node := MeshInstance3D.new()
	node.mesh = surface.commit()
	node.material_override = material
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return node


static func _annex(snapshot: Dictionary, point: Vector2, axis: Vector2, wall: Material, roof: Material) -> Node3D:
	var node := Node3D.new()
	node.position = Buildings.world_position(point, Terrain.height_at(snapshot, point))
	node.rotation.y = atan2(axis.y, axis.x)
	var body := _box(Vector3(0.30, 0.15, 0.20), wall)
	body.position.y = 0.065
	node.add_child(body)
	var top := _box(Vector3(0.34, 0.04, 0.24), roof)
	top.position.y = 0.155
	node.add_child(top)
	return node


static func _street_prop(snapshot: Dictionary, point: Vector2, axis: Vector2, asset: String, timber: Material, roof: Material) -> Node3D:
	var node := Node3D.new()
	node.name = "Street_" + asset
	node.position = Buildings.world_position(point, Terrain.height_at(snapshot, point))
	node.rotation.y = atan2(axis.y, axis.x)
	var size := Vector3(0.14, 0.09, 0.10)
	if asset == "bench":
		size = Vector3(0.21, 0.035, 0.075)
	elif asset == "fence":
		size = Vector3(0.26, 0.09, 0.025)
	elif asset == "lamp":
		size = Vector3(0.025, 0.30, 0.025)
	elif asset == "stall":
		size = Vector3(0.17, 0.11, 0.11)
	var body := _box(size, timber)
	body.position.y = size.y * 0.5
	node.add_child(body)
	if asset == "stall" or asset == "lamp":
		var cap := _box(Vector3(0.22, 0.025, 0.16) if asset == "stall" else Vector3(0.065, 0.035, 0.065), roof)
		cap.position.y = size.y + 0.013
		node.add_child(cap)
	return node


static func _box(size: Vector3, material: Material) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	mesh.size = size
	node.mesh = mesh
	node.material_override = material
	return node


static func _material(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.95
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	return material


static func _site_radius(site: Dictionary, direction: Vector2) -> float:
	if site.is_empty():
		return 19.0
	var footprint: Variant = site.get("runtime_footprint", null)
	if footprint is Rect2:
		# Join the visible registered platform, not the wider legacy grading area.
		# Over-clipping by that blend area used to hide most short real connections.
		var rectangle: Rect2 = footprint
		var center: Vector2 = site.center
		var x_exit := INF
		var y_exit := INF
		if absf(direction.x) > 0.0001:
			x_exit = ((rectangle.end.x if direction.x > 0.0 else rectangle.position.x) - center.x) / direction.x
		if absf(direction.y) > 0.0001:
			y_exit = ((rectangle.end.y if direction.y > 0.0 else rectangle.position.y) - center.y) / direction.y
		return maxf(0.0, minf(x_exit, y_exit))
	var x := absf(direction.x) / maxf(1.0, float(site.get("half_x", 18.0)))
	var y := absf(direction.y) / maxf(1.0, float(site.get("half_y", 16.0)))
	return 1.0 / maxf(0.0001, maxf(x, y)) + 1.0


static func _inside_site(point: Vector2, sites: Array, margin: float) -> bool:
	for value in sites:
		if not value is Dictionary:
			continue
		var site: Dictionary = value
		var center: Vector2 = site.get("center", Vector2.ZERO)
		var offset := point - center
		if absf(offset.x) <= float(site.get("half_x", 18.0)) + margin and absf(offset.y) <= float(site.get("half_y", 16.0)) + margin:
			return true
	return false
