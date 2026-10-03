extends Control
const W=preload("res://ui/widgets.gd")
const GROUND_Y=0.661
const HEIGHT_Y=0.42
const Elevation=preload("res://ui/map/elevation040.gd")
const Foundation=preload("res://ui/map/foundation041.gd")
const Connections=preload("res://ui/map/connections041.gd")
const Hex=preload("res://core/hex.gd")
const Art=preload("res://ui/map/art.gd")
const AtlasStyle=preload("res://ui/map/atlas_style.gd")
const Fabric=preload("res://ui/map/fabric040.gd")
const Data=preload("res://ui/view_models/map_data.gd")
signal cell_clicked(key:String,dragging:bool)
signal cell_hovered(key:String)
signal camera_changed
var game
var selected:Array=[]
var hovered:String=""
var selected_project:String=""
var mode:String="inspect"
var zoom:float=1.0
var pan=Vector2.ZERO
var layer:String="normal"
var show_relations:bool=true
var show_all_links:bool=false
var show_impacts:bool=true
var reduce_motion:bool=false
var show_labels:bool=true
var beta_feedback_active:bool=false
var font:Font
var dragging:bool=false
var panning:bool=false
var visual_time:float=0.
var flash_time:float=0.
var flash_project_id:String=""
var planned_base:String=""
var planning_values={}
var plan_result={}
var plan_tile:String=""
var display_shot={}
var compact:bool=false
var available_rect=Rect2()
var ground:Node2D
var buildings:Node2D
var effects:Node2D
var world_viewport:SubViewport
var world_texture:TextureRect
var mobile_quality_override:bool=false
var world_render_scale:float=1.0
var _camera_position=Vector2(INF,INF)
var _camera_scale=Vector2.ZERO
var _last_ground=""
var _last_buildings=""
var _last_effect=0.
var _press=Vector2.ZERO
var _last_pointer=Vector2.ZERO
var _touches={}
var _touch_start={}
var _touch_moved=false
var _pinched=false
var _left_pressed=false
var _focus_pending=true
var hidden_links:int=0
var label_rects:Array=[]
var label_hidden:int=0
var ui_font_scale:float=1.0
var view_values={}
var footprint_cache={}
var elevation_field={}
var elevation_key=""
var elevation_samples={}
var foundation_height_samples={}
var surface_colors={}
var terrain_mesh:ArrayMesh
var terrain_mesh_key=""
var _drawing_world=false
var shadow_texture:Texture2D
var render_stats={"terrain_mesh_builds":0,"ground_draws":0,"structure_draws":0,"fabric_derives":0,"foundation_builds":0,"foundation_signature_scans":0,"connector_builds":0}
var _data_key=""
var ground_footprints={}
var ground_bounds={}
var pick_order=[]
var fabric_snapshot={}
var _last_fabric_key=""
var _last_connections_key=""
var _last_structure_source_key=""
var _structure_appearance_cache=""
var foundation_field={}
var foundation_render_items:Array[Dictionary]=[]
var foundation_key=""
var _foundation_guard_key=""
var connector_render_items:Array[Dictionary]=[]
var water_geometry:Array=[]
var water_key:String=""
var river_mesh:ArrayMesh
var terrain_colors={"plain":Color("A9B396"),"forest":Color("91A187"),"hill":Color("B6B8A2"),"wet":Color("9AABA0"),"water":Color("79999A")}
var landscape_noise=FastNoiseLite.new()
func _ready():
	mouse_filter=Control.MOUSE_FILTER_STOP;clip_contents=true;Art.initialize()
	font=W.theme().default_font
	landscape_noise.seed=4071;landscape_noise.frequency=.008;landscape_noise.fractal_octaves=3
	var world_host:Node=self
	for arg in OS.get_cmdline_user_args():
		if str(arg)=="--mobile-quality":mobile_quality_override=true
	if OS.has_feature("mobile") or mobile_quality_override:
		world_viewport=SubViewport.new()
		world_viewport.transparent_bg=false
		world_viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
		add_child(world_viewport)
		world_texture=TextureRect.new()
		world_texture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		world_texture.texture=world_viewport.get_texture()
		world_texture.expand_mode=TextureRect.EXPAND_IGNORE_SIZE
		world_texture.stretch_mode=TextureRect.STRETCH_SCALE
		world_texture.texture_filter=CanvasItem.TEXTURE_FILTER_LINEAR
		world_texture.mouse_filter=Control.MOUSE_FILTER_IGNORE
		add_child(world_texture)
		world_host=world_viewport
		resized.connect(_update_world_viewport_size)
	ground=Node2D.new();world_host.add_child(ground);ground.draw.connect(_draw_ground_world)
	buildings=Node2D.new();world_host.add_child(buildings);buildings.draw.connect(_draw_structures_world)
	var shader=Shader.new();shader.code="shader_type canvas_item; uniform float saturation=1.0; void fragment(){ vec4 t=COLOR; float grey=dot(t.rgb,vec3(0.2126,0.7152,0.0722)); COLOR=vec4(mix(vec3(grey),t.rgb,saturation),t.a); }"
	var mat=ShaderMaterial.new();mat.shader=shader;buildings.material=mat
	effects=Node2D.new();add_child(effects);effects.draw.connect(_overlays.bind(effects))
	mouse_exited.connect(func():hovered="";cell_hovered.emit(""))
func _draw_ground_world():
	_drawing_world=true;render_stats.ground_draws+=1
	_terrain(ground)
	_drawing_world=false
func _draw_structures_world():
	_ensure_elevation();_ensure_foundations()
	_drawing_world=true;render_stats.structure_draws+=1
	_structures(buildings)
	_drawing_world=false
func _sync_camera():
	if ground==null:return
	var raster_scale=world_render_scale if world_viewport!=null else 1.0
	var origin=(size*.5+pan)*raster_scale
	var node_scale=Vector2.ONE*zoom*raster_scale
	if origin==_camera_position and node_scale==_camera_scale:return
	_camera_position=origin;_camera_scale=node_scale
	for node in [ground,buildings]:node.position=origin;node.scale=node_scale
func _update_world_viewport_size():
	if world_viewport==null:return
	var previous_scale=world_render_scale
	var short_side=maxf(1.0,minf(size.x,size.y))
	world_render_scale=minf(1.0,720.0/short_side)
	var target=Vector2i(maxi(1,roundi(size.x*world_render_scale)),maxi(1,roundi(size.y*world_render_scale)))
	var resized_viewport=world_viewport.size!=target
	if resized_viewport:world_viewport.size=target
	if resized_viewport or not is_equal_approx(previous_scale,world_render_scale):
		_camera_position=Vector2(INF,INF);_sync_camera()
func _process(delta):
	visual_time+=delta;flash_time=maxf(0,flash_time-delta)
	if game==null or game.s.cells.is_empty():return
	_update_world_viewport_size()
	_ensure_elevation()
	_ensure_foundations()
	if _focus_pending:focus_community();_focus_pending=false
	_sync_camera()
	var data_key=str(game.s.block_index)+":"+str(game.revision)+":"+str(game.s.simulation_tick)+":"+str(display_shot.get("snapshot_id",""))+":"+layer
	if _data_key!=data_key:
		_data_key=data_key;view_values={}
		if not display_shot.is_empty() and layer not in ["normal","terrain","operator"]:
			for key in game.s.cells:view_values[key]=Data.tile_info(game,display_shot,key,layer)
	var ground_data_key=data_key if layer not in ["normal","terrain","operator"] else ""
	var gkey=elevation_key+foundation_key+ground_data_key+str(mode)+str(display_shot.get("forecast",false))
	if _last_ground!=gkey:_last_ground=gkey;ground.queue_redraw()
	var fkey=str(game.get_instance_id())+":"+str(game.s.get("seed",0))+":"+str(game.s.block_index)+":"+str(game.revision)+":"+str(game.s.simulation_tick)+":"+str(display_shot.get("snapshot_id",""))
	if _last_fabric_key!=fkey:
		_last_fabric_key=fkey;fabric_snapshot=Fabric.derive(game,display_shot);render_stats.fabric_derives+=1
	var connections_key=foundation_key+":"+_connector_geometry_key()
	if _last_connections_key!=connections_key:
		_last_connections_key=connections_key
		Connections.clear_cache()
		connector_render_items=Connections.items(self,fabric_snapshot.get("connectors",[]))
		render_stats.connector_builds+=1
	var bkey=JSON.stringify(game.s.get("block",{}).get("capstone",{}))+elevation_key+foundation_key+str(layer)+str(mode)+str(hovered if mode=="site" else "")+_structure_appearance_key()+_filler_appearance_key()+connections_key
	if _last_buildings!=bkey:
		_last_buildings=bkey;buildings.material.set_shader_parameter("saturation",1.0 if layer in ["normal","terrain","operator"] else .28);buildings.queue_redraw()
	if visual_time-_last_effect>.033:
		_last_effect=visual_time;effects.queue_redraw()
func invalidate():
	if mode=="site" or layer=="change":_last_ground=""
	if mode=="site":_last_buildings=""
	if effects!=null:effects.queue_redraw()
func invalidate_camera():
	_sync_camera()
	if effects!=null:effects.queue_redraw()
func draw_scale() -> float:return 1.0 if _drawing_world else zoom
func draw_origin() -> Vector2:return Vector2.ZERO if _drawing_world else size*.5+pan
func radius() -> float:return 64.0*draw_scale()
func flat_point(k:String) -> Vector2:
	var q=Hex.coords(k);return Vector2(sqrt(3.)*64*(q.x+q.y*.5),96.*q.y)
func _ensure_elevation():
	if game==null or game.s.cells.is_empty():return
	var key=str(game.s.get("seed",0))+":"+str(game.s.block_index)+":"+str(game.s.cells.size())
	if elevation_key==key:return
	elevation_key=key;elevation_field=Elevation.derive(game.s.cells,int(game.s.get("seed",0)));elevation_samples.clear();foundation_height_samples.clear();surface_colors.clear();ground_footprints.clear();ground_bounds.clear();foundation_key="";_foundation_guard_key="";foundation_field={};foundation_render_items.clear();terrain_mesh_key=""
	_ensure_foundations()
func _natural_elevation_at(p:Vector2) -> float:
	if elevation_field.is_empty():_ensure_elevation()
	var key=Vector2(roundf(p.x*100.)*.01,roundf(p.y*100.)*.01)
	if not elevation_samples.has(key):
		var raw=Elevation.height_at(elevation_field,key) if key.length_squared()<2100000. else 0.
		elevation_samples[key]=raw
	return float(elevation_samples[key])
func _connector_geometry_key() -> String:
	var parts:Array[String]=[]
	for item in fabric_snapshot.get("connectors",[]):
		parts.append("%s/%s/%s/%s/%s/%d/%d"%[str(item.get("id","")),str(item.get("project_a","")),str(item.get("project_b","")),str(item.get("kind","")),str(item.get("variant",0)),int(item.get("tier_a",1)),int(item.get("tier_b",1))])
	return "|".join(parts)
func _filler_appearance_key() -> String:
	var parts:Array[String]=[]
	for item in fabric_snapshot.get("fillers",[]):
		parts.append("%s/%s/%d/%d"%[str(item.get("key","")),str(item.get("asset","")),int(item.get("variant",0)),roundi(float(item.get("strength",0))*4.0)])
	return "|".join(parts)
func _structure_appearance_key() -> String:
	if game==null:return ""
	var source_key=str(game.get_instance_id())+":"+str(game.revision)
	if source_key==_last_structure_source_key:return _structure_appearance_cache
	_last_structure_source_key=source_key
	var parts:Array[String]=[]
	var project_ids=game.playable_ids();project_ids.sort()
	for id_value in project_ids:
		var id=str(id_value);var project:Dictionary=game.s.projects[id]
		parts.append("%s/%s/%s/%s/%s/%s/%s/%s"%[id,str(project.get("tile","")),str(project.get("def","")),str(project.get("status","")),str(project.get("paused",false)),str(project.get("operator_org","")),str(project.get("tier",1)),str(not project.get("upgrade_job",{}).is_empty())])
	var bonus_ids=game.Rules.ids(game.s.bonus_instances);bonus_ids.sort()
	for bonus_id_value in bonus_ids:
		var bonus=game.s.bonus_instances[str(bonus_id_value)]
		if bool(bonus.get("active",false)):
			parts.append("bonus/%s/%s/%s/%s"%[str(bonus_id_value),str(bonus.get("bonus_id","")),str(bonus.get("host_type","")),str(bonus.get("host_id",""))])
	_structure_appearance_cache="|".join(parts)
	return _structure_appearance_cache
func _foundation_signature() -> String:
	var snapshot=display_shot if not display_shot.is_empty() else {}
	var projects=snapshot.get("projects",game.s.projects)
	var tiles=snapshot.get("tiles",snapshot.get("cells",game.s.cells))
	var parts:Array[String]=[elevation_key]
	if projects is Dictionary:
		var project_ids=projects.keys();project_ids.sort_custom(func(a,b):return str(a)<str(b))
		for id_value in project_ids:
			var project=projects[id_value]
			if project is Dictionary:
				parts.append("%s@%s:%s"%[str(id_value),str(project.get("tile",project.get("tile_key",""))),str(project.get("def",""))])
	if tiles is Dictionary:
		var water_keys:Array[String]=[]
		for tile_value in tiles:
			var tile=tiles[tile_value]
			if tile is Dictionary and Foundation._is_water(tile):water_keys.append(str(tile_value))
		water_keys.sort();parts.append("water="+",".join(water_keys))
	return "|".join(parts)
func _foundation_source_key() -> String:
	var snapshot=display_shot if not display_shot.is_empty() else {}
	var projects=snapshot.get("projects",game.s.projects)
	var tiles=snapshot.get("tiles",snapshot.get("cells",game.s.cells))
	var project_count=projects.size() if projects is Dictionary else -1
	var tile_count=tiles.size() if tiles is Dictionary else -1
	return "%s:%s:%s:%s:%s:%s:%s:%d:%d"%[str(game.get_instance_id()),str(game.s.get("seed",0)),str(game.s.block_index),str(game.revision),str(game.s.simulation_tick),str(snapshot.get("snapshot_id","")),str(snapshot.get("forecast",false)),project_count,tile_count]
func _ensure_foundations():
	if game==null or game.s.cells.is_empty():return
	if elevation_field.is_empty():_ensure_elevation()
	var source_key=_foundation_source_key()
	if source_key==_foundation_guard_key and not foundation_field.is_empty():return
	_foundation_guard_key=source_key
	render_stats.foundation_signature_scans+=1
	var signature=_foundation_signature()
	if signature==foundation_key:return
	foundation_key=signature
	foundation_field=Foundation.derive(game,display_shot,Callable(self,"_natural_elevation_at"))
	foundation_height_samples.clear();surface_colors.clear();terrain_mesh_key=""
	foundation_render_items=Foundation.foundation_items(foundation_field,Callable(self,"_natural_elevation_at"))
	render_stats.foundation_builds+=1
	foundation_render_items.sort_custom(func(a,b):
		var ay=float(a.center.y);var by=float(b.center.y)
		return ay<by if ay!=by else str(a.project_id)<str(b.project_id)
	)
	_rebuild_ground_footprints()
func _rebuild_ground_footprints():
	if game==null or game.s.cells.is_empty():return
	pick_order=game.s.cells.keys();pick_order.sort_custom(func(a,b):return flat_point(a).y>flat_point(b).y)
	ground_footprints.clear();ground_bounds.clear()
	for k in pick_order:
		var points=_world_footprint(str(k));ground_footprints[k]=points
		var rect=Rect2(points[0],Vector2.ZERO)
		for point_value in points:rect=rect.expand(point_value)
		ground_bounds[k]=rect
func elevation_at(p:Vector2) -> float:
	if foundation_field.is_empty():_ensure_foundations()
	var key=Vector2(roundf(p.x*100.)*.01,roundf(p.y*100.)*.01)
	if not foundation_height_samples.has(key):
		foundation_height_samples[key]=Foundation.height_at(foundation_field,key,Callable(self,"_natural_elevation_at"))
	return float(foundation_height_samples[key])
func geometry_radius() -> float:return 64.0
func geometry_version() -> String:return elevation_key+":"+foundation_key
func project_flat(p:Vector2) -> Vector2:return Vector2(p.x,p.y*GROUND_Y-elevation_at(p)*HEIGHT_Y)
func surface_point(p:Vector2) -> Vector2:return draw_origin()+project_flat(p)*draw_scale()
func world_point(k:String) -> Vector2:
	if not _drawing_world:
		_ensure_elevation()
		if foundation_field.is_empty() or _foundation_source_key()!=_foundation_guard_key:_ensure_foundations()
	return project_flat(flat_point(k))
func point(k:String) -> Vector2:return draw_origin()+world_point(k)*draw_scale()
func tile_screen_anchor(k:String) -> Vector2:
	return size*.5+pan+world_point(k)*zoom
func building_visual_scale(def_id:String) -> float:return AtlasStyle.building_scale(def_id)
func building_screen_rect(id:String) -> Rect2:
	if game==null or not game.s.projects.has(id):return Rect2()
	var project:Dictionary=game.s.projects[id];var art_id=str(project.def)
	var texture=Art.sprite(art_id)
	if texture==null:return Rect2()
	var extent=Vector2.ONE*64.*zoom*building_visual_scale(art_id)
	extent.y*=texture.get_height()/float(texture.get_width())
	return Rect2(tile_screen_anchor(str(project.tile))-Art.anchor(art_id)*extent,extent)
func building_visible_rect(id:String) -> Rect2:
	var rect=building_screen_rect(id)
	if rect.size==Vector2.ZERO:return rect
	var info=Art.metadata(str(game.s.projects[id].def));var bounds=info.get("bounds",[]);var pixels=info.get("size",[768,768])
	if bounds.size()!=4:return rect
	var ratio=rect.size/Vector2(float(pixels[0]),float(pixels[1]))
	return Rect2(rect.position+Vector2(float(bounds[0]),float(bounds[1]))*ratio,Vector2(float(bounds[2])-float(bounds[0]),float(bounds[3])-float(bounds[1]))*ratio)
func terrain_symbols(tile:String) -> Array:
	return AtlasStyle.terrain_symbols(game.s.cells.get(tile,{})) if game!=null else []
func _world_footprint(k:String,scale_value:float=1.) -> PackedVector2Array:
	var points=PackedVector2Array();var center=flat_point(k)
	for i in 6:
		var a=Vector2(cos(deg_to_rad(60*i-30)),sin(deg_to_rad(60*i-30)))*64.*scale_value
		var b=Vector2(cos(deg_to_rad(60*(i+1)-30)),sin(deg_to_rad(60*(i+1)-30)))*64.*scale_value
		for j in 4:points.append(project_flat(center+a.lerp(b,float(j)/4.)))
	return points
func surface_polygon(k:String,scale_value:float=1.) -> PackedVector2Array:
	var points=PackedVector2Array()
	for vertex in _world_footprint(k,scale_value):points.append(draw_origin()+vertex*draw_scale())
	return points
func polygon(center:Vector2,rr:float) -> PackedVector2Array:
	var poly=PackedVector2Array()
	for i in 6:poly.append(center+Vector2(cos(deg_to_rad(60*i-30)),sin(deg_to_rad(60*i-30))*GROUND_Y)*rr)
	return poly
func bounds_rect() -> Rect2:
	if available_rect.size.x>0:return available_rect
	return Rect2(Vector2(20,100),size-Vector2(40,240))
func focus_community():
	if game==null or game.s.cells.is_empty():return
	var ids=game.playable_ids();var points=[]
	for id in ids:points.append(world_point(game.s.projects[id].tile))
	if points.is_empty():points.append(Vector2.ZERO)
	_fit(points,145.,1.8 if not compact else .98)
func focus_all():
	var points=[]
	for k in game.s.cells:points.append(world_point(k))
	_fit(points,80.,1.0)
func _fit(points:Array,padding:float,maxzoom:float):
	if points.is_empty():return
	var lo=points[0];var hi=points[0]
	for p in points:lo=lo.min(p);hi=hi.max(p)
	var rect=bounds_rect();var extent=hi-lo+Vector2.ONE*padding*2
	zoom=clampf(minf(rect.size.x/extent.x,rect.size.y/extent.y),.28,maxzoom)
	pan=rect.get_center()-size*.5-(lo+hi)*.5*zoom;invalidate_camera();camera_changed.emit()
func focus_tile(k:String):
	if not game.s.cells.has(k):return
	pan=bounds_rect().get_center()-size*.5-world_point(k)*zoom;invalidate_camera();camera_changed.emit()
func ensure_visible(k:String):
	if not game.s.cells.has(k):return
	var r=bounds_rect().grow(-radius()*.8);var p=point(k)
	var target=Vector2(clampf(p.x,r.position.x,r.end.x),clampf(p.y,r.position.y,r.end.y))
	pan+=target-p;invalidate_camera()
func set_zoom(value:float,anchor:Vector2):
	var before=(anchor-size*.5-pan)/zoom;zoom=clampf(value,.28,2.25);pan=anchor-size*.5-before*zoom;invalidate_camera();camera_changed.emit()
func hit(pos:Vector2) -> String:
	if game==null:return ""
	_ensure_elevation();_ensure_foundations();var local=pos if _drawing_world else (pos-size*.5-pan)/zoom
	for k in pick_order:
		if ground_bounds[k].has_point(local) and Geometry2D.is_point_in_polygon(local,ground_footprints[k]):return str(k)
	return ""
func _tap(pos:Vector2):
	var k=hit(pos)
	if mode=="inspect":
		var building=building_hit(pos)
		if building!="":k=str(game.s.projects[building].tile)
		else:
			var related=connection_hit(pos)
			if related!="" and game.s.projects.has(related):k=str(game.s.projects[related].tile)
	if k=="":return
	if radius()<30.:set_zoom(.76,pos);focus_tile(k);return
	cell_clicked.emit(k,false)
func connection_hit(pos:Vector2) -> String:
	if game==null or connector_render_items.is_empty():return ""
	var local=(pos-size*.5-pan)/zoom
	for index in range(connector_render_items.size()-1,-1,-1):
		var item:Dictionary=connector_render_items[index]
		var polygons:Array=[]
		for face in item.get("roof_faces",[]):polygons.append(face.get("points",PackedVector2Array()))
		for face in item.get("body_faces",[]):polygons.append(face.get("points",PackedVector2Array()))
		polygons.append(item.get("platform",PackedVector2Array()))
		for stair in item.get("stairs",[]):polygons.append(stair.get("points",PackedVector2Array()))
		var contains=false
		for polygon_points in polygons:
			if polygon_points is PackedVector2Array and polygon_points.size()>=3 and Geometry2D.is_point_in_polygon(local,polygon_points):contains=true;break
		if not contains:continue
		var project_a=str(item.get("project_a",""));var project_b=str(item.get("project_b",""))
		if not game.s.projects.has(project_a):return project_b if game.s.projects.has(project_b) else ""
		if not game.s.projects.has(project_b):return project_a
		var point_a=project_flat(flat_point(str(game.s.projects[project_a].get("tile",""))))
		var point_b=project_flat(flat_point(str(game.s.projects[project_b].get("tile",""))))
		var distance_a=local.distance_squared_to(point_a);var distance_b=local.distance_squared_to(point_b)
		return project_a if distance_a<distance_b else (project_b if distance_b<distance_a else (project_a if project_a<project_b else project_b))
	return ""
func building_hit(pos:Vector2) -> String:
	if game==null:return ""
	var ids=game.playable_ids()
	ids.sort_custom(func(a,b):return point(game.s.projects[a].tile).y>point(game.s.projects[b].tile).y if point(game.s.projects[a].tile).y!=point(game.s.projects[b].tile).y else str(game.s.projects[a].tile)>str(game.s.projects[b].tile))
	for id in ids:
		var b=game.s.projects[id];var texture=Art.sprite(str(b.def))
		if texture==null:continue
		var extent=Vector2.ONE*radius()*building_visual_scale(str(b.def));extent.y*=texture.get_height()/float(texture.get_width())
		var origin=point(str(b.tile))-Art.anchor(str(b.def))*extent
		if Art.opaque_at(str(b.def),(pos-origin)/extent):return str(id)
	return ""
func _gui_input(event):
	if event is InputEventPanGesture:pan-=event.delta*16;invalidate_camera();accept_event();return
	if event is InputEventMagnifyGesture:set_zoom(zoom*event.factor,event.position);accept_event();return
	if event is InputEventScreenTouch:
		if event.pressed:
			_touches[event.index]=event.position;_touch_start[event.index]=event.position
			if _touches.size()==1:_touch_moved=false;_pinched=false
			else:_pinched=true;_touch_moved=true
		else:
			if _touches.size()==1 and not _touch_moved and not _pinched:_tap(event.position)
			_touches.erase(event.index);_touch_start.erase(event.index)
		accept_event();return
	if event is InputEventScreenDrag:
		if not _touches.has(event.index):return
		var old=_touches[event.index]
		if _touches.size()>=2:
			var ids=_touches.keys();var other=ids[0] if ids[0]!=event.index else ids[1];var op=_touches[other]
			var oldmid=(old+op)*.5;var newmid=(event.position+op)*.5;var distance=old.distance_to(op)
			if distance>2:set_zoom(zoom*event.position.distance_to(op)/distance,oldmid)
			pan+=newmid-oldmid;_pinched=true;_touch_moved=true
		else:
			if event.position.distance_to(_touch_start.get(event.index,event.position))>8:_touch_moved=true
			if _touch_moved:pan+=event.position-old
		_touches[event.index]=event.position;invalidate_camera();accept_event();return
	if event is InputEventMouseButton:
		if event.device==InputEvent.DEVICE_ID_EMULATION:return
		if event.button_index==MOUSE_BUTTON_WHEEL_UP and event.pressed:set_zoom(zoom*1.10,event.position);accept_event();return
		if event.button_index==MOUSE_BUTTON_WHEEL_DOWN and event.pressed:set_zoom(zoom/1.10,event.position);accept_event();return
		if event.button_index in [MOUSE_BUTTON_MIDDLE,MOUSE_BUTTON_RIGHT]:panning=event.pressed;accept_event();return
		if event.button_index==MOUSE_BUTTON_LEFT:
			if event.pressed:_left_pressed=true;dragging=false;_press=event.position
			else:
				if _left_pressed and not dragging:_tap(event.position)
				_left_pressed=false;dragging=false
			accept_event();return
	if event is InputEventMouseMotion:
		if panning:pan+=event.relative;invalidate_camera()
		if _left_pressed and event.position.distance_to(_press)>8:dragging=true
		if _left_pressed and dragging:pan+=event.relative;invalidate_camera()
		var k=hit(event.position)
		if mode=="inspect":
			var building=building_hit(event.position)
			if building!="":k=str(game.s.projects[building].tile)
		if k!=hovered:hovered=k;cell_hovered.emit(k);if mode=="site":buildings.queue_redraw()
func _outline(c:CanvasItem,center:Vector2,rr:float,color:Color,width:float=1.):
	var key=hit(center);var pts=surface_polygon(key,rr/radius()) if key!="" else polygon(center,rr)
	pts.append(pts[0]);c.draw_polyline(pts,color,width,true)
func _edge(c:CanvasItem,k:String,index:int,color:Color,width:float=1.,dashed:bool=false):
	var points=surface_polygon(k);var j=((6-index)%6)*4
	for n in 4:
		if dashed:c.draw_dashed_line(points[(j+n)%24],points[(j+n+1)%24],color,width,7.,true)
		else:c.draw_line(points[(j+n)%24],points[(j+n+1)%24],color,width,true)
func _land_color(p:Vector2) -> Color:
	var n=(landscape_noise.get_noise_2d(p.x,p.y)+1.)*.5
	return Color("8D987B").lerp(Color("B5B89B"),clampf(n*1.1-.22,0,.68))
func _surface_color(p:Vector2) -> Color:
	if surface_colors.has(p):return surface_colors[p]
	var height=elevation_at(p)
	var dx=(elevation_at(p+Vector2(6,0))-elevation_at(p-Vector2(6,0)))/12.
	var dy=(elevation_at(p+Vector2(0,6))-elevation_at(p-Vector2(0,6)))/12.
	var slope=Vector2(dx,dy).length()
	var normal=Vector3(-dx,-dy,1.).normalized()
	var sunlight=clampf(.40+.78*normal.dot(Vector3(-.55,-.66,.64).normalized()),.48,1.15)
	var col=_land_color(p).lerp(Color("B3AD91"),clampf(height/160.,0,.38))
	col=col.lerp(Color("89917B"),clampf((slope-.22)*1.5,0,.72))
	col=Color(col.r*sunlight,col.g*sunlight,col.b*sunlight,1.)
	surface_colors[p]=col;return col
func _terrain(c:CanvasItem):
	var natural=layer in ["normal","terrain","operator"]
	if game==null:return
	_ensure_elevation();_ensure_foundations()
	AtlasStyle.terrain(self,c)
	var keys=game.s.cells.keys();keys.sort()
	for id in game.playable_ids():
		var building=game.s.projects[id]
		if building.def in ["agriculture","modern_agriculture"]:_draw_fields(c,str(building.tile),natural)
	if layer not in ["normal","terrain","operator"]:
		for k in keys:
			var info=view_values.get(k,{});var col=Color(0,0,0,0);var hatch=false
			if Data.SERVICES.has(layer):
				if not info.get("known",false):col=Color(.52,.56,.56,.18);hatch=true
				elif float(info.get("people",0))>0:
					var ratio=float(info.ratio);col=Color("94bca4") if ratio>=.75 else (Color("dfc593") if ratio>=.5 else Color("d6a193"));col.a=.77
					hatch=ratio<.999 and info.reachable
				elif info.get("reachable",false):col=Color(.28,.58,.50,.10)
			elif layer=="population" and info.get("applicable",false):
				col=Color("9FCBB1") if float(info.get("housing",0))>0 else Color("AAC6DF");col.a=.68
			elif layer in ["employment","unemployed"] and info.get("applicable",false):
				var ratio=float(info.value);var good=ratio>.85 if layer=="employment" else ratio<.1
				col=Color("9ec2b0") if good else Color("d9b29a");col.a=.72
			elif layer=="pollution":
				var v=float(info.get("value",0));col=Color("aac9c0") if v<0 else Color("d6a178");col.a=minf(.75,absf(v)/Data.display_max("pollution",2.)*.75)
			elif layer=="heat":col=Color("d4a26a");col.a=minf(.62,maxf(0,float(info.get("value",0)))/Data.display_max("heat",4.))
			elif layer in ["food","material","goods","energy"] and info.get("applicable",false):
				col=Color("9dbbb0") if float(info.value)>=0 else Color("d3b693");col.a=.67
			elif layer=="change" and plan_result.get("ok",false):
				var delta=plan_result.get("map_delta",{}).get(k,{})
				var sum=0.
				for v in delta.get("services",{}).values():sum+=float(v)
				sum+=float(delta.get("utilization",0))*10-float(delta.get("pollution",0))*10
				if absf(sum)>.001:col=Color("86b59b") if sum>0 else Color("d9a38c");col.a=.72
			if col.a>0:c.draw_colored_polygon(surface_polygon(k,.99),col)
			if hatch:
				var rr=radius()*.58
				for i in range(-2,3):c.draw_line(point(k)+Vector2(-rr,i*8*draw_scale()),point(k)+Vector2(rr,i*8*draw_scale()-rr*.65),Color(.22,.35,.31,.3),1.,true)
	if mode=="site" or layer=="terrain":
		for k in keys:
			_outline(c,point(k),radius(),Color(.31,.48,.40,.22),1.)
			if mode=="site" and planning_values.get(k,{}).get("ratio",1.)>1.01:_outline(c,point(k),radius()*.94,Color(.22,.46,.34,.45),2.)
func _foundation_point(vertex:Vector3) -> Vector2:
	return Vector2(vertex.x,vertex.y*GROUND_Y-vertex.z*HEIGHT_Y)
func _draw_triangle_stream(c:CanvasItem,vertices:PackedVector3Array,color:Color):
	for index in range(0,vertices.size()-2,3):
		var triangle=PackedVector2Array([_foundation_point(vertices[index]),_foundation_point(vertices[index+1]),_foundation_point(vertices[index+2])])
		Connections._draw_surface_polygon(c,triangle,color)
func _draw_foundation_surfaces(c:CanvasItem):
	for item in foundation_render_items:
		_draw_triangle_stream(c,item.get("edge_triangles",PackedVector3Array()),Color("A7A083",.045))
		_draw_triangle_stream(c,item.get("platform_triangles",PackedVector3Array()),Color("C0B998",.09))
		_draw_triangle_stream(c,item.get("retaining_walls",PackedVector3Array()),Color("847B65",.36))
func _draw_fields(c:CanvasItem,key:String,natural:bool):
	var origin=flat_point(key)+Vector2(-3,25)
	var soil=PackedVector2Array()
	for offset in [Vector2(-27,-12),Vector2(27,-12),Vector2(27,12),Vector2(-27,12)]:soil.append(surface_point(origin+offset))
	c.draw_colored_polygon(soil,Color("B8A24E") if natural else Color("B8C1A6"))
	for row in 6:
		var line=PackedVector2Array();var crop=PackedVector2Array()
		for j in 9:
			var p=origin+Vector2(-25.+j*6.25,-9.+row*3.5)
			line.append(surface_point(p));crop.append(surface_point(p+Vector2(0,1.1)))
		c.draw_polyline(line,Color("817C39") if natural else Color("91A291"),1.2*draw_scale(),true)
		c.draw_polyline(crop,Color("D1C269") if natural else Color("BFC9B2"),.9*draw_scale(),true)
func _sprite(c:CanvasItem,id:String,pos:Vector2,width:float,modulate:Color=Color.WHITE):
	var resolved_id=id
	var tex=Art.sprite(resolved_id)
	if tex==null:
		resolved_id={"prop_tree_broadleaf":"tree_broadleaf","prop_tree_conifer":"tree_conifer","prop_shrub":"shrub"}.get(id,id)
		tex=Art.sprite(resolved_id)
	if tex==null:return
	var ratio=tex.get_height()/float(tex.get_width());var extent=Vector2(width,width*ratio)
	c.draw_texture_rect(tex,Rect2(pos-Art.anchor(resolved_id)*extent,extent),false,modulate)
func _shadow(c:CanvasItem,pos:Vector2,rr:Vector2,alpha:float=.13):
	if shadow_texture==null:
		var image=Image.create(64,64,false,Image.FORMAT_RGBA8)
		for y in 64:
			for x in 64:
				var distance=Vector2((x-31.5)/31.5,(y-31.5)/31.5).length()
				image.set_pixel(x,y,Color(.19,.27,.24,pow(maxf(0.,1.-distance),1.5)))
		shadow_texture=ImageTexture.create_from_image(image)
	c.draw_texture_rect(shadow_texture,Rect2(pos-rr,rr*2.),false,Color(1,1,1,alpha*1.3))
func _structures(c:CanvasItem):
	if game==null:return
	var keys=game.s.cells.keys();keys.sort_custom(func(a,b):return point(a).y<point(b).y if point(a).y!=point(b).y else str(a)<str(b))
	var r=radius();var neutral=Color(.91,.96,.94,.80) if layer not in ["normal","terrain","operator"] else Color.WHITE
	for item in connector_render_items:Connections.draw_item(c,self,item)
	for k in keys:
		var t=game.s.cells[k];var p=point(k)
		if t.building=="":continue
		var b=game.s.projects[t.building];var color=neutral
		if b.status=="construction":color.a=.40
		elif b.paused:color=Color(.73,.78,.73,.76)
		if mode=="site" and hovered!="" and Hex.distance(k,hovered)<=1 and point(k).y>point(hovered).y:color.a*=.35
		_shadow(c,p+Vector2(0,r*.08),Vector2(r*.40,r*.18))
		var def=game.c.projects.get(str(b.def),{})
		var art_id=str(b.def)
		if Art.sprite(art_id)==null:art_id=str(def.get("art",b.get("art",b.def)))
		AtlasStyle.courtyard(self,c,str(k),def,int(b.get("tier",def.get("tier",1))))
		_sprite(c,art_id,p,r*building_visual_scale(art_id),color)
		if b.status=="construction" or not b.get("upgrade_job",{}).is_empty():_construction(c,p,r)
		if true:

			var modules=Art.attachments(game,str(b.id))
			for j in mini(2,modules.size()):_sprite(c,modules[j],p+Vector2((j*2-1)*.49,.26)*r,r*.38,color)
	_draw_fabric_fillers(c)
	_draw_capstone(c)


func _draw_capstone(c:CanvasItem):
	# A civic landmark is a real Block entity, not another buildable slot or population host.
	var capstone:Dictionary=game.s.get("block",{}).get("capstone",{})
	var tile=str(capstone.get("tile_anchor",""))
	if capstone.is_empty() or not game.s.cells.has(tile):return
	var role=str(capstone.get("function_role",capstone.get("theme","civic")))
	var color:Color={"industrial":Color("D5AF60"),"civic":Color("ADA5CA"),"commercial":Color("82B3D5"),"logistics":Color("79BDBA"),"agricultural":Color("98B580")}.get(role,Color("ADA5CA"))
	var r=radius()*.58
	var p=point(tile)+Vector2(-.73,-.53)*radius()
	_shadow(c,p+Vector2(0,r*.10),Vector2(r*.8,r*.35),.18)
	var roof=PackedVector2Array([p+Vector2(-.84,-.64)*r,p+Vector2(-.12,-1.04)*r,p+Vector2(.81,-.64)*r,p+Vector2(.09,-.26)*r])
	var front=PackedVector2Array([roof[0],roof[3],p+Vector2(.09,.35)*r,p+Vector2(-.84,-.03)*r])
	var side=PackedVector2Array([roof[3],roof[2],p+Vector2(.81,-.04)*r,p+Vector2(.09,.35)*r])
	c.draw_colored_polygon(front,Color("CBC3A8"));c.draw_colored_polygon(side,Color("8B998D"));c.draw_colored_polygon(roof,color)
	for i in 3:
		var base=p+Vector2(-.65+i*.24,-.25+i*.10)*r
		c.draw_line(base,base+Vector2(0,.31)*r,Color("5B6865"),maxf(2,3*draw_scale()),true)
	var pole=p+Vector2(.45,-.83)*r
	c.draw_line(pole,pole-Vector2(0,.85)*r,Color("D6D6BE"),maxf(1,2*draw_scale()),true)
	c.draw_colored_polygon(PackedVector2Array([pole-Vector2(0,.85)*r,pole+Vector2(.45,-.69)*r,pole+Vector2(0,-.47)*r]),color)
	# Theme emblems stay legible without pretending to be new simulation buildings.
	var emblem=p+Vector2(-.04,-.63)*r
	if role=="agricultural":
		c.draw_circle(emblem,6*draw_scale(),Color("42694C"))
	elif role=="industrial":
		c.draw_rect(Rect2(emblem-Vector2(4,8)*draw_scale(),Vector2(8,14)*draw_scale()),Color("6B604B"))
	else:
		c.draw_circle(emblem,6*draw_scale(),Color("E5E2C9"),false,maxf(1,2*draw_scale()))

func _construction(c:CanvasItem,p:Vector2,r:float):
	# Visual scaffolding has no capacity or construction-time effects.
	for side in [-1.,1.]:
		var a=p+Vector2(side*.65,.05)*r;var b=p+Vector2(side*.65,-.60)*r
		c.draw_line(a,b,Color("A28C68"),2.*draw_scale(),true)
		for level in [0.,.25,.5]:
			c.draw_line(p+Vector2(-.65,-level)*r,p+Vector2(.65,-level)*r,Color("D7BD89"),2.*draw_scale(),true)
	_sprite(c,"materials",p+Vector2(.50,.38)*r,r*.40)
func _draw_fabric_fillers(c:CanvasItem):
	for item in fabric_snapshot.get("fillers",[]):
		if not game.s.cells.has(item.a) or not game.s.cells.has(item.b):continue
		var axis:Vector2=(flat_point(item.b)-flat_point(item.a)).normalized()
		var pos=surface_point(flat_point(item.a).lerp(flat_point(item.b),.42 if int(item.variant)%2==0 else .61)-axis.orthogonal()*geometry_radius()*.22)
		var width=radius()*.29
		_shadow(c,pos+Vector2(0,width*.20),Vector2(width*.38,width*.17),.08)
		var strength_bucket=roundf(clampf(float(item.strength),0.0,1.0)*4.0)/4.0
		_sprite(c,str(item.asset),pos,width,Color(1,1,1,clampf(strength_bucket+.18,.36,.82)))
func marker(c:CanvasItem,pos:Vector2,text:String,color:Color=Color("24473f"),fontsize:int=14):
	if text=="":return
	fontsize=int(fontsize*ui_font_scale)
	var width=font.get_string_size(text,HORIZONTAL_ALIGNMENT_LEFT,-1,fontsize).x
	var bg=StyleBoxFlat.new();bg.bg_color=Color("172329");bg.border_color=Color(color,.65);bg.set_border_width_all(1);bg.set_corner_radius_all(5)
	var rect=Rect2(pos+Vector2(-width*.5-6,-fontsize-4),Vector2(width+12,fontsize+11))
	var placed=false
	for off in [Vector2.ZERO,Vector2(0,-24),Vector2(0,24),Vector2(-20,0),Vector2(20,0)]:
		var proposed=Rect2(rect.position+off,rect.size)
		if not Rect2(Vector2.ZERO,size).encloses(proposed):continue
		if label_rects.any(func(existing):return existing.intersects(proposed.grow(2))):continue
		rect=proposed;pos+=off;placed=true;break
	if not placed:label_hidden+=1;return
	label_rects.append(rect);c.draw_style_box(bg,rect)
	c.draw_string(font,pos+Vector2(-width*.5,0),text,HORIZONTAL_ALIGNMENT_LEFT,-1,fontsize,Color("EDF3EF"))
func _overlays(c:CanvasItem):
	label_rects=[];label_hidden=0
	if game==null or game.s.cells.is_empty():return
	var r=radius();var keys=game.s.cells.keys();var metrics=display_shot.get("metrics",game.s.metrics);var label_count=0
	var links=Data.links(game,display_shot,selected_project,layer) if not display_shot.is_empty() and show_relations else []
	hidden_links=maxi(0,links.size()-6) if not show_all_links else 0
	for i in mini(links.size(),links.size() if show_all_links else 6):
		var l=links[i];var src=Data.source_tile(display_shot,l.source);var dst=Data.source_tile(display_shot,l.target)
		if not game.s.cells.has(src) or not game.s.cells.has(dst):continue
		var a=point(src);var b=point(dst);var col=Color("598a78")
		if l.kind=="pollution":col=Color("b07150")
		elif l.kind=="employment":col=Color("597d96")
		elif l.kind=="heat":col=Color("aa772c")
		var width=clampf(1.+sqrt(absf(l.amount))*.35,1.4,4.)
		c.draw_dashed_line(a,b,col,width,8.,true)
		var direction=(b-a).normalized();var end=a.lerp(b,.75);var normal=direction.orthogonal()
		c.draw_colored_polygon(PackedVector2Array([end+direction*5,end-direction*4+normal*4,end-direction*4-normal*4]),col)
		if not reduce_motion:c.draw_circle(a.lerp(b,fmod(visual_time*.18+i*.17,1)),2.5,Color("fff7ce"))
	if Data.SERVICES.has(layer):
		for k in keys:
			var sources=view_values.get(k,{}).get("sources",{});var reached=not sources.is_empty() if selected_project=="" else float(sources.get(selected_project,0))>0
			if not reached:continue
			var ns=Hex.neighbors(k)
			for i in 6:
				var other=view_values.get(ns[i],{}).get("sources",{});var inside=not other.is_empty() if selected_project=="" else float(other.get(selected_project,0))>0
				if not inside:_edge(c,k,i,Color("377e73"),2.0)
	# The proposed source's reach is a separate dashed boundary, not served population.
	if Data.SERVICES.has(layer) and plan_result.get("ok",false):
		var forecast=plan_result.get("map_snapshot",{});var source=str(plan_result.get("target",""));var preview_cells=forecast.get("tiles",{})
		for k in keys:
			if float(Data.best_sources(preview_cells.get(k,{}),layer).get(source,0))<=0:continue
			var ns=Hex.neighbors(k)
			for i in 6:
				if float(Data.best_sources(preview_cells.get(ns[i],{}),layer).get(source,0))<=0:_edge(c,k,i,Color("99733c"),2.,true)
	for id in game.playable_ids():
		var b=game.s.projects[id];var pos=point(b.tile)
		if not Rect2(Vector2(-80,-80),size+Vector2(160,160)).has_point(pos):continue
		var a=metrics.get("utilization",{}).get(id,{});var u=float(a.get("utilization",0))
		if b.status=="construction":
			var p=clampf(1-float(b.job.work)/maxf(.001,b.job.total),0,1)
			c.draw_rect(Rect2(pos+Vector2(-r*.44,r*.36),Vector2(r*.88,5)),Color("b8bdac"));c.draw_rect(Rect2(pos+Vector2(-r*.44,r*.36),Vector2(r*.88*p,5)),Color("a77539"))
		elif b.paused:marker(c,pos+Vector2(r*.4,-r*.34),"Ⅱ",Color("53636a"))
		elif not beta_feedback_active and not a.is_empty() and u<.48 and (id==selected_project or b.tile==hovered or layer!="normal"):
			if label_count<10:
				var problem=str(a.get("bottleneck",""));var symbol="人" if "劳力" in problem else ("电" if "能源" in problem else ("料" if "物资" in problem else "仓"))
				marker(c,pos+Vector2(r*.45,-r*.30),symbol,Color("a53332"));label_count+=1
		# Dynamic geometry refers to current operating status and actual/forecast caption.
		if b.status=="running" and not b.paused and not reduce_motion and layer=="normal":
			if b.def=="works" and u>.02:
				for n in 3:
					var t=fmod(visual_time*.42+n*.34,1.);var smoke=pos+Vector2(r*.5+t*8,-r*.65-t*r*.32)
					c.draw_circle(smoke,(3+t*4)*zoom,Color(.83,.84,.74,(1-t)*.3*u))
			if b.def=="power":
				var hub=pos+Vector2(-r*.42,-r*.73)
				for i in 3:
					var ang=visual_time*u*1.6+i*TAU/3;c.draw_line(hub,hub+Vector2(cos(ang),sin(ang))*(r*.22),Color("eff0d7"),2.5,true)
		if layer=="operator":
			var idx=game.Rules.ids(game.c.operators).find(str(b.operator_org));var color=Color.from_hsv(float(idx)/8.,.36,.72)
			_outline(c,pos,r*.88,color,2.0);c.draw_texture_rect(Art.icon("org_"+b.operator_org),Rect2(pos+Vector2(r*.5,-r*.38),Vector2(26,26)),false)
		if id==selected_project or b.tile==hovered:
			_outline(c,pos,r*.90,Color("fff3c3"),3.0)
			if show_labels and label_count<12:marker(c,pos+Vector2(0,r*.70),game.project_name(id));label_count+=1
		var info=view_values.get(b.tile,{})
		if Data.SERVICES.has(layer) and float(info.get("people",0))>0 and label_count<12:
			marker(c,pos+Vector2(0,r*.40),("%.0f%%"%(float(info.ratio)*100)) if info.get("known",false) else "?",Color("17665a") if info.get("ratio",0)>=.75 else Color("a53332"));label_count+=1
		elif layer=="population" and info.get("applicable",false) and label_count<12:
			if float(info.get("housing",0))>0:
				_outline(c,pos,r*.90,Color("3D8A61"),2.)
				marker(c,pos+Vector2(0,r*.46),"住 %.0f/%.0f"%[info.count,info.housing],Color("286342"));label_count+=1
			if float(info.get("jobs",0))>0:
				marker(c,pos+Vector2(0,-r*.60),"岗 %.0f/%.0f"%[info.assigned,info.jobs],Color("356EA0"));label_count+=1
		elif layer in ["employment","unemployed"] and info.get("applicable",false) and label_count<12:
			marker(c,pos+Vector2(0,r*.48),"%.0f%%"%(float(info.value)*100),Color("53636a"));label_count+=1
		elif layer in ["food","material","goods","energy"] and info.get("applicable",false) and label_count<12:
			marker(c,pos+Vector2(0,r*.48),"%+.1f"%float(info.value),Color("17665a") if info.value>=0 else Color("8b641d"));label_count+=1
	if plan_result.get("ok",false):
		for k in plan_result.get("map_delta",{}):
			if not game.s.cells.has(k) or label_count>=12:continue
			var d=plan_result.map_delta[k];var sum=0.;var service_name="服务"
			for kind in d.get("services",{}):
				var n=float(d.services[kind])
				if absf(n)>absf(sum):sum=n;service_name={"medical":"医疗","education":"教育","commercial":"购物","entertainment":"休闲"}.get(kind,"服务")
			var util=float(d.get("utilization",0));var pollution=float(d.get("pollution",0))
			if absf(sum)>.1:marker(c,point(k)+Vector2(0,-r*.6),service_name+" %+.1f人"%sum,Color("17665a") if sum>0 else Color("a53332"));label_count+=1
			elif absf(util)>.1:marker(c,point(k)+Vector2(0,-r*.6),"运行 %+.0f%%"%(util*100),Color("17665a") if util>0 else Color("a53332"));label_count+=1
			elif pollution>.05:_outline(c,point(k),r*.87,Color("ae634b"),2.)
	var tile=plan_tile if plan_tile!="" else (str(selected[0]) if not selected.is_empty() else hovered)
	if mode=="site" and game.s.cells.has(tile) and game.s.cells[tile].building=="":
		_outline(c,point(tile),r*.94,Color("8b641d"),3.)
		if planned_base!="":_sprite(c,planned_base,point(tile),r*building_visual_scale(planned_base),Color(1,1,1,.64))
		if zoom>.5:marker(c,point(tile)+Vector2(0,r*.65),"规划 · 尚未建设",Color("8b641d"))
	if flash_time>0 and flash_project_id!="" and game.s.projects.has(flash_project_id) and not reduce_motion:
		c.draw_arc(point(game.s.projects[flash_project_id].tile),r*(1.0+(2.-flash_time)*.08),0,TAU,40,Color(1,.93,.65,flash_time*.35),3.,true)
func center_project(p:Dictionary) -> Vector2:return point(p.tile)

func _draw_river(c:CanvasItem):
	var water_cells=[]
	for k in game.s.cells:
		if game.s.cells[k].terrain=="water":water_cells.append(str(k))
	water_cells.sort_custom(func(a,b):return flat_point(a).y<flat_point(b).y)
	var natural=layer in ["normal","terrain","operator"]
	var key=",".join(water_cells)+str(natural)
	if key!=water_key:
		water_key=key;water_geometry=[]
		if water_cells.size()<2:return
		var controls=[]
		for k in water_cells:controls.append(flat_point(k))
		var first=controls[0];var last=controls[-1]
		controls.push_front(first+(first-controls[1]).normalized()*1800.)
		controls.append(last+(last-controls[-2]).normalized()*1800.)
		var path=PackedVector2Array()
		for i in controls.size()-1:
			var a:Vector2=controls[maxi(0,i-1)];var b:Vector2=controls[i]
			var d:Vector2=controls[i+1];var e:Vector2=controls[mini(i+2,controls.size()-1)]
			var length=b.distance_to(d);var samples=maxi(12,int(length/10.))
			var start_handle=b+(d-a).normalized()*length*.32
			var end_handle=d-(e-b).normalized()*length*.32
			for j in samples:path.append(b.bezier_interpolate(start_handle,end_handle,d,float(j)/samples))
		path.append(controls[-1]);water_geometry.append(path)
		var vertices=PackedVector3Array();var colors=PackedColorArray();var indices=PackedInt32Array();var uvs=PackedVector2Array()
		# Triangle strips keep tightly curved banks continuous at every zoom.
		for band in [[47.,Color("84945380")],[41.,Color("A79969BB")],[35.,Color("91A38A")],[32.,Color("608B85")],[26.,Color("456F79")]]:
			var start=vertices.size();var color:Color=band[1]
			if not natural:color=color.lerp(Color("C5D3D0"),.58)
			for i in path.size():
				var direction=(path[mini(i+1,path.size()-1)]-path[maxi(i-1,0)]).normalized()
				var width=float(band[0])*(1.+sin(path[i].y*.018)*.10)
				var roughness=maxf(0.,float(band[0])-28.)*.24
				var left_width=width+landscape_noise.get_noise_2d(path[i].x*7.,path[i].y*7.)*roughness
				var right_width=width+landscape_noise.get_noise_2d(path[i].x*7.+219,path[i].y*7.-512)*roughness
				var lp:Vector2=path[i]+direction.orthogonal()*left_width;var rp:Vector2=path[i]-direction.orthogonal()*right_width
				vertices.append(Vector3(lp.x,lp.y*GROUND_Y,0));vertices.append(Vector3(rp.x,rp.y*GROUND_Y,0))
				uvs.append(lp*.16);uvs.append(rp*.16)
				colors.append(color);colors.append(color)
				if i<path.size()-1:
					var n=start+i*2;indices.append_array(PackedInt32Array([n,n+1,n+3,n,n+3,n+2]))
		var arrays=[];arrays.resize(Mesh.ARRAY_MAX);arrays[Mesh.ARRAY_VERTEX]=vertices;arrays[Mesh.ARRAY_COLOR]=colors;arrays[Mesh.ARRAY_INDEX]=indices;arrays[Mesh.ARRAY_TEX_UV]=uvs
		river_mesh=ArrayMesh.new();river_mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	if water_geometry.is_empty():return
	c.draw_mesh(river_mesh,null,Transform2D(Vector2(draw_scale(),0),Vector2(0,draw_scale()),draw_origin()))
	var path:PackedVector2Array=water_geometry[0]
	# Broken current streaks follow the river instead of repeating per hex.
	for i in range(4,path.size()-5,7):
		var streak=PackedVector2Array()
		for j in 4:
			var p:Vector2=path[i+j]+Vector2(sin(float(i)*1.77)*15.,0)
			streak.append(draw_origin()+Vector2(p.x,p.y*GROUND_Y)*draw_scale())
		c.draw_polyline(streak,Color(.69,.83,.80,.19),maxf(.7,draw_scale()*.7),true)
