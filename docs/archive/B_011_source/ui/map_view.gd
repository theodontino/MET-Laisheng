extends Control
const Hex=preload("res://core/hex.gd")
signal cell_clicked(key:String,dragging:bool)
signal cell_hovered(key:String)
var game
var selected:Array=[]
var hovered:String=""
var selected_project:String=""
var mode:String="inspect"
var zoom:float=1.0
var pan=Vector2.ZERO
var show_relations:bool=true
var show_impacts:bool=true
var font:Font
var dragging:bool=false
var panning:bool=false
var visual_time:float=0.0
var terrain_colors={"plain":Color("657d68"),"forest":Color("345a50"),"hill":Color("8d8673"),"wet":Color("506f6c"),"water":Color("245668")}
var zone_colors={"settlement":Color("9ec998"),"production":Color("d3a36b"),"circulation":Color("7bb7d0"),"public":Color("b4abd9")}
func _ready():
	mouse_filter=Control.MOUSE_FILTER_STOP
	clip_contents=true
	font=SystemFont.new();font.font_names=PackedStringArray(["PingFang SC","Noto Sans CJK SC","WenQuanYi Zen Hei","sans-serif"])
	set_process(true)
func _process(delta):
	visual_time+=delta
	if is_visible_in_tree():queue_redraw()
func radius() -> float:return minf(size.x/16.5,size.y/14.0)*zoom
func point(k:String) -> Vector2:
	var p=Hex.coords(k);var r=radius()
	return size*.5+pan+Vector2(sqrt(3.0)*r*(p.x+p.y*.5),1.5*r*p.y)
func polygon(center:Vector2,rr:float) -> PackedVector2Array:
	var poly=PackedVector2Array()
	for i in 6:poly.append(center+Vector2(cos(deg_to_rad(60*i-30)),sin(deg_to_rad(60*i-30)))*rr)
	return poly
func hit(pos:Vector2) -> String:
	if game==null:return ""
	var rr=radius()
	var best="";var dist=INF
	for k in game.s.cells:
		var d=point(k).distance_to(pos)
		if d<dist:dist=d;best=k
	if dist>rr:return ""
	if Geometry2D.is_point_in_polygon(pos,polygon(point(best),rr)):return best
	return ""
func _gui_input(event):
	if event is InputEventMouseButton:
		if event.button_index==MOUSE_BUTTON_WHEEL_UP and event.pressed:zoom=clampf(zoom*1.1,.65,1.65);accept_event();return
		if event.button_index==MOUSE_BUTTON_WHEEL_DOWN and event.pressed:zoom=clampf(zoom/1.1,.65,1.65);accept_event();return
		if event.button_index==MOUSE_BUTTON_MIDDLE:panning=event.pressed
		if event.button_index==MOUSE_BUTTON_LEFT:
			dragging=event.pressed
			if event.pressed:
				var k=hit(event.position)
				if k!="":cell_clicked.emit(k,false)
			accept_event()
	elif event is InputEventMouseMotion:
		if panning:pan+=event.relative
		var k=hit(event.position)
		if k!=hovered:
			hovered=k;cell_hovered.emit(k)
			if dragging and mode=="site" and k!="" and not k in selected:cell_clicked.emit(k,true)
func center_project(p:Dictionary) -> Vector2:
	var sum=Vector2.ZERO;var cells=game.s.sites[p.site].cells
	for k in cells:sum+=point(k)
	return sum/maxi(1,cells.size())
func _draw():
	draw_rect(Rect2(Vector2.ZERO,size),Color("102d34"))
	for i in 13:
		var y=size.y*.08+i*size.y*.075
		draw_line(Vector2(0,y),Vector2(size.x,y+24),Color(0.24,.46,.48,.055),1)
	if game==null or game.s.cells.is_empty():
		draw_string(font,Vector2(35,50),"白霭岛 · 河谷地区",HORIZONTAL_ALIGNMENT_LEFT,-1,20,Color("90b5ae"))
		return
	var r=radius()
	var keys=game.s.cells.keys()
	keys.sort_custom(func(a,b):return point(a).y<point(b).y)
	for k in keys:
		var t=game.s.cells[k];var pos=point(k)
		var color=terrain_colors[t.terrain]
		if t.zone!="":color=color.lerp(zone_colors[t.zone],.27)
		if t.site!="":color=color.lightened(.045)
		draw_colored_polygon(polygon(pos+Vector2(0,5*zoom),r*.965),Color("123e42"))
		draw_colored_polygon(polygon(pos,r*.965),color)
		var outline=polygon(pos,r*.965);outline.append(outline[0])
		draw_polyline(outline,Color(0.06,.15,.17,.35),1.0,true)
		if t.terrain=="water":
			for w in 3:
				var yy=pos.y+(w-1)*r*.36;var shift=sin(visual_time*.4+w+k.hash()%9)*r*.09
				draw_line(Vector2(pos.x-r*.44+shift,yy),Vector2(pos.x+r*.44+shift,yy),Color(.38,.68,.73,.35),1.5,true)
		elif t.site=="":
			if t.terrain=="forest":
				for offset in [Vector2(-.28,.10),Vector2(.24,.27),Vector2(.1,-.22)]:draw_tree(pos+offset*r,r*.36)
			elif t.terrain=="hill":
				draw_colored_polygon(PackedVector2Array([pos+Vector2(-.5,.28)*r,pos+Vector2(-.07,-.38)*r,pos+Vector2(.46,.26)*r]),Color("a4a08a"))
				draw_line(pos+Vector2(-.07,-.38)*r,pos+Vector2(.13,.06)*r,Color("d3cdb5"),2,true)
			else:
				for i in 3:
					var offset=Vector2(sin(float(k.hash()%23+i*2))*r*.46,cos(float(i*3))*r*.36)
					draw_line(pos+offset,pos+offset+Vector2(2,-3)*zoom,Color(.72,.78,.57,.28),1,true)
		if k in selected:
			draw_colored_polygon(polygon(pos,r*.91),Color(1.0,.77,.36,.16))
			draw_polyline(outline,Color("eac076"),2.5,true)
		elif k==hovered:
			draw_polyline(outline,Color("d9e9d9"),2,true)
		if t.site=="" and t.zone!="":draw_circle(pos,3*zoom,zone_colors[t.zone])
	# Roads are a visual consequence, never exclusive tiles or pathfinding.
	var pids=game.s.projects.keys()
	for i in pids.size():
		for j in range(i+1,pids.size()):
			var a=pids[i];var b=pids[j]
			if game.adjacent_projects(a,b):
				draw_line(center_project(game.s.projects[a]),center_project(game.s.projects[b]),Color("b4a985"),5*zoom,true)
	# Real local effects are displayed separately from mature Fusion relationships.
	if show_impacts and selected_project!="" and game.s.projects.has(selected_project):
		var origin=center_project(game.s.projects[selected_project])
		var effects={}
		for link in game.s.metrics.get("service_links",[]):
			if selected_project not in [link.source,link.home]:continue
			var other=link.home if link.source==selected_project else link.source
			if other==selected_project:continue
			if not effects.has(other):effects[other]={"good":0.0,"bad":0.0}
			effects[other].good+=float(link.people)
		for link in game.s.metrics.get("exposure_links",[]):
			if selected_project not in [link.source,link.home]:continue
			var other=link.home if link.source==selected_project else link.source
			if other==selected_project:continue
			if not effects.has(other):effects[other]={"good":0.0,"bad":0.0}
			effects[other].bad+=float(link.noise)+float(link.pollution)
		for other in effects:
			if not game.s.projects.has(other):continue
			var e=effects[other];var col=Color("e5a16f") if e.bad>.1 else Color("8dd8ad")
			for k in game.s.sites[game.s.projects[other].site].cells:
				var outline=polygon(point(k),r*.91);outline.append(outline[0]);draw_polyline(outline,col,3.0,true)
			var dest=center_project(game.s.projects[other])
			draw_dashed_line(origin+Vector2(0,-10),dest+Vector2(0,-10),col,2,6,true)
		draw_string(font,Vector2(18,24),"绿色：实际服务   橙色：噪音/污染   金色：已建立协作",HORIZONTAL_ALIGNMENT_LEFT,-1,12,Color("d4e1c6"))
	if show_relations:
		for rel in game.s.relations.values():
			if not game.s.projects.has(rel.a) or not game.s.projects.has(rel.b):continue
			var a=center_project(game.s.projects[rel.a]);var b=center_project(game.s.projects[rel.b])
			var col=Color("ead193") if rel.choice!="" else (Color("85d8c1") if rel.progress>=rel.required else Color(.53,.81,.75,.42))
			draw_line(a+Vector2(0,-22),b+Vector2(0,-22),col,2.5*zoom,true)
			var mid=(a+b)*.5+Vector2(0,-22)
			draw_circle(mid,6*zoom,Color("183f41"));draw_arc(mid,6*zoom,-PI*.5,-PI*.5+TAU*minf(1.0,rel.progress/rel.required),24,col,2*zoom,true)
	for id in pids:
		var p=game.s.projects[id];var def=game.c.projects[p.def];var pos=center_project(p)
		var zone=def.zone;var color=zone_colors[zone]
		var scale=zoom*(1.0+.08*mini(game.s.sites[p.site].cells.size()-1,3))
		if id==selected_project:
			for k in game.s.sites[p.site].cells:
				var line=polygon(point(k),r*.93);line.append(line[0]);draw_polyline(line,Color("f6d28b"),2.5,true)
		draw_building(pos,p,def,color,scale)
		var text=def.name
		if def.population>0:text+=" %.0f人"%p.get("residents",0)
		var fs=int(12*zoom)
		var tw=font.get_string_size(text,HORIZONTAL_ALIGNMENT_LEFT,-1,fs).x
		draw_style_box(name_box(),Rect2(pos+Vector2(-tw*.5-6,27*scale),Vector2(tw+12,20*zoom)))
		draw_string(font,pos+Vector2(-tw*.5,42*scale),text,HORIZONTAL_ALIGNMENT_LEFT,-1,fs,Color("f0edda"))
		if not p.job.is_empty():
			var ratio=clampf(1.0-float(p.job.work)/maxf(.01,float(p.job.total)),0,1)
			draw_rect(Rect2(pos+Vector2(-23,21)*scale,Vector2(46,3)*scale),Color("1b3135"))
			draw_rect(Rect2(pos+Vector2(-23,21)*scale,Vector2(46*ratio,3)*scale),Color("e1b56e"))
		elif p.paused:draw_circle(pos+Vector2(20,-25)*scale,5*scale,Color("a19889"))
		elif game.milestone_status(id).ready:
			draw_circle(pos+Vector2(22,-33)*scale,6*scale,Color("e8c477"))
			draw_string(font,pos+Vector2(18,-29)*scale,"↑",HORIZONTAL_ALIGNMENT_LEFT,-1,int(12*scale),Color("183733"))
	draw_string(font,Vector2(22,size.y-18),"滚轮缩放 · 中键拖动 · 道路自动形成，不占土地",HORIZONTAL_ALIGNMENT_LEFT,-1,12,Color("98b4af"))
func name_box() -> StyleBoxFlat:
	var s=StyleBoxFlat.new();s.bg_color=Color(.035,.12,.14,.86);s.corner_radius_top_left=3;s.corner_radius_top_right=3;s.corner_radius_bottom_left=3;s.corner_radius_bottom_right=3
	return s
func draw_tree(p:Vector2,r:float):
	draw_line(p+Vector2(0,r*.35),p+Vector2(0,-r*.1),Color("5c503b"),2,true)
	draw_colored_polygon(PackedVector2Array([p+Vector2(-r*.55,r*.1),p+Vector2(0,-r),p+Vector2(r*.55,r*.1)]),Color("25493f"))
	draw_colored_polygon(PackedVector2Array([p+Vector2(-r*.36,-r*.22),p+Vector2(0,-r),p+Vector2(r*.36,-r*.22)]),Color("497762"))
func cube(p:Vector2,w:float,d:float,h:float,color:Color):
	var top=PackedVector2Array([p+Vector2(-w/2,-h),p+Vector2(0,-h-d),p+Vector2(w/2,-h),p+Vector2(0,d-h)])
	var left=PackedVector2Array([p+Vector2(-w/2,-h),p+Vector2(0,d-h),p+Vector2(0,d),p+Vector2(-w/2,0)])
	var right=PackedVector2Array([p+Vector2(0,d-h),p+Vector2(w/2,-h),p+Vector2(w/2,0),p+Vector2(0,d)])
	draw_colored_polygon(left,color.darkened(.2));draw_colored_polygon(right,color.darkened(.35));draw_colored_polygon(top,color.lightened(.15))
func draw_building(pos:Vector2,p:Dictionary,def:Dictionary,color:Color,scale:float):
	draw_ellipse_shadow(pos+Vector2(5,7)*scale,Vector2(30,13)*scale)
	if p.status=="construction":
		draw_colored_polygon(PackedVector2Array([pos+Vector2(-22,0)*scale,pos+Vector2(0,-12)*scale,pos+Vector2(22,0)*scale,pos+Vector2(0,12)*scale]),Color("c7b894"))
		for x in [-17,0,17]:
			draw_line(pos+Vector2(x,4)*scale,pos+Vector2(x,-27)*scale,Color("d7bb82"),2*scale,true)
		draw_line(pos+Vector2(-17,-27)*scale,pos+Vector2(17,-27)*scale,Color("d7bb82"),2*scale,true)
		return
	var height=(18+float(p.level)*9)*scale
	match def.id:
		"farm","vegetables":
			for n in 4:draw_line(pos+Vector2(-24,-2+n*5)*scale,pos+Vector2(6,-15+n*5)*scale,Color("c9be79"),3*scale,true)
			cube(pos+Vector2(13,4)*scale,19*scale,5*scale,height*.7,Color("bc9f74"))
		"orchard":
			for o in [Vector2(-17,3),Vector2(0,-8),Vector2(15,5)]:
				draw_tree(pos+o*scale,16*scale)
				draw_circle(pos+(o+Vector2(3,-9))*scale,3*scale,Color("d99865"))
		"pasture":
			draw_arc(pos,23*scale,0,TAU,20,Color("c6bc90"),2*scale,true)
			for o in [Vector2(-10,2),Vector2(7,-5),Vector2(12,8)]:draw_circle(pos+o*scale,5*scale,Color("e5dfc5"))
		"bamboo":
			for o in [Vector2(-15,3),Vector2(0,-5),Vector2(15,4)]:draw_tree(pos+o*scale,20*scale)
		"power","wind","solar":
			cube(pos,26*scale,8*scale,height,color)
			draw_circle(pos+Vector2(-20,-4)*scale,12*scale,Color("7c9ea2"));draw_arc(pos+Vector2(-20,-4)*scale,9*scale,0,TAU,20,Color("c0d6cb"),2*scale,true)
			for i in 4:
				var angle=visual_time*.5+i*PI*.5;draw_line(pos+Vector2(-20,-4)*scale,pos+Vector2(-20,-4)*scale+Vector2(cos(angle),sin(angle))*9*scale,Color("d0d9bd"),1.5*scale,true)
		"park","square":
			draw_circle(pos,21*scale,Color("709777"));draw_tree(pos+Vector2(-8,4)*scale,16*scale);draw_tree(pos+Vector2(9,0)*scale,12*scale)
		_:
			cube(pos,35*scale,10*scale,height,color)
			if def.zone=="settlement":cube(pos+Vector2(-17,9)*scale,17*scale,5*scale,height*.6,color.lightened(.08))
			if def.zone=="production":
				cube(pos+Vector2(12,-5)*scale,7*scale,3*scale,height+12*scale,Color("8a958c"))
			if def.zone=="public":
				draw_line(pos+Vector2(0,-height),pos+Vector2(0,-height-15*scale),Color("ede2bf"),1.4*scale,true)
				draw_colored_polygon(PackedVector2Array([pos+Vector2(0,-height-15*scale),pos+Vector2(11*scale,-height-12*scale),pos+Vector2(0,-height-9*scale)]),Color("edc281"))
			for i in 3:draw_rect(Rect2(pos+Vector2(-12+i*7,-9)*scale,Vector2(3,4)*scale),Color("efdb9a"))
func draw_ellipse_shadow(center:Vector2,axes:Vector2):
	var poly=PackedVector2Array()
	for i in 20:poly.append(center+Vector2(cos(TAU*i/20)*axes.x,sin(TAU*i/20)*axes.y))
	draw_colored_polygon(poly,Color(0,0,0,.17))
