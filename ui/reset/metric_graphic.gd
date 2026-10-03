extends Control
## Shared read-only vector metric. No game object, timers or simulation callbacks.
const W=preload("res://ui/widgets.gd")
const Art=preload("res://ui/map/art.gd")
const D=preload("res://ui/components031/design.gd")
const P=preload("res://ui/presentation.gd")
var data:Dictionary={}
var focus_hint:CanvasLayer
func _init():
	mouse_filter=Control.MOUSE_FILTER_PASS
	focus_mode=Control.FOCUS_ALL
	set_meta("graphical_indicator",true)
	size_flags_horizontal=Control.SIZE_EXPAND_FILL
	focus_entered.connect(_show_focus_hint)
	focus_exited.connect(_hide_focus_hint)
func configure(value:Dictionary):
	data=value.duplicate(true)
	tooltip_text=str(data.get("tooltip",data.get("label","")))
	for item in data.get("items",[]):
		var id=str(item.get("id",""))
		tooltip_text+="\n"+str(P.KIND.get(id,D.quantity(id).label))+" · "+str(item.get("text",""))
	if has_focus():_show_focus_hint()
	update_minimum_size();queue_redraw()
func _hide_focus_hint():
	if is_instance_valid(focus_hint):focus_hint.queue_free()
	focus_hint=null;queue_redraw()
func _show_focus_hint():
	_hide_focus_hint()
	if not is_inside_tree() or not is_visible_in_tree():return
	focus_hint=CanvasLayer.new();focus_hint.layer=40;add_child(focus_hint)
	var panel=PanelContainer.new();panel.mouse_filter=Control.MOUSE_FILTER_IGNORE
	panel.custom_minimum_size=Vector2(320,0);panel.add_theme_stylebox_override("panel",W.box(W.BASE,W.FOCUS,8));focus_hint.add_child(panel)
	var label=W.label(tooltip_text,13,W.TEXT);label.custom_minimum_size.x=296;label.mouse_filter=Control.MOUSE_FILTER_IGNORE;panel.add_child(label)
	var viewport=get_viewport_rect().size;var at=get_global_rect().position+Vector2(-12,-160)
	panel.position=Vector2(clampf(at.x,12,viewport.x-332),maxf(12,at.y));queue_redraw()
func _get_minimum_size() -> Vector2:
	return Vector2(82,136 if str(data.get("kind","ring"))=="bars" else 112)*W.text_scale
func _ink() -> Color:
	return data.get("color",W.GREEN)
func _text(text:String,rect:Rect2,points:int,color:Color=W.TEXT):
	var font=get_theme_default_font()
	var fs=int(points*W.text_scale)
	draw_string(font,Vector2(rect.position.x,rect.position.y+fs),text,HORIZONTAL_ALIGNMENT_CENTER,rect.size.x,fs,color)
func _icon(id:String,rect:Rect2,color:Color):
	var texture=load("res://ui/reset/population.svg") if id=="population" else Art.icon(D.icon_id(id),color)
	if texture!=null:draw_texture_rect(texture,rect,false)
func _draw():
	if data.is_empty():return
	var scale=W.text_scale
	var ink=_ink()
	var kind=str(data.get("kind","ring"))
	if has_focus():draw_style_box(W.box(Color.TRANSPARENT,W.FOCUS,8),Rect2(Vector2.ZERO,size))
	_text(str(data.get("label","")),Rect2(0,0,size.x,22*scale),12,W.MUTED)
	match kind:
		"ring":
			var center=Vector2(size.x*.5,65*scale)
			var radius=minf(size.x*.38,29*scale)
			draw_arc(center,radius,-PI*.5,PI*1.5,64,W.SOFT,5*scale,true)
			var ratio=clampf(float(data.get("ratio",0)),0,1)
			if ratio>0:draw_arc(center,radius,-PI*.5,-PI*.5+TAU*ratio,64,ink,5*scale,true)
			_text(str(data.get("value","—")),Rect2(0,center.y-12*scale,size.x,24*scale),19,ink)
			_text(str(data.get("note","")),Rect2(0,96*scale,size.x,15*scale),11,W.MUTED)
		"bars":
			var items:Array=data.get("items",[])
			for index in mini(3,items.size()):
				var item:Dictionary=items[index]
				var top=(27+35*index)*scale
				_icon(str(item.get("id","food")),Rect2(2*scale,top,20*scale,20*scale),ink)
				_text(str(item.get("text","")),Rect2(28*scale,top-1*scale,size.x-32*scale,17*scale),12)
				var rect=Rect2(29*scale,top+22*scale,maxf(10,size.x-34*scale),4*scale)
				draw_style_box(W.box(W.SOFT,Color.TRANSPARENT,2),rect)
				rect.size.x*=clampf(float(item.get("ratio",0)),0,1)
				if rect.size.x>0:draw_style_box(W.box(ink,Color.TRANSPARENT,2),rect)
		"outputs":
			var items:Array=data.get("items",[])
			for index in mini(3,items.size()):
				var item:Dictionary=items[index]
				var y=(30+25*index)*scale
				_icon(str(item.get("id","goods")),Rect2(3*scale,y,18*scale,18*scale),ink)
				_text(str(item.get("text","—")),Rect2(24*scale,y-3*scale,size.x-25*scale,22*scale),15)
		"status":
			draw_circle(Vector2(size.x*.5,46*scale),10*scale,ink)
			_text(str(data.get("value","")),Rect2(0,68*scale,size.x,25*scale),16,ink)
			_text(str(data.get("note","")),Rect2(0,94*scale,size.x,18*scale),11,W.MUTED)
		_:
			var icon=str(data.get("icon",""))
			if icon!="":_icon(icon,Rect2(size.x*.5-13*scale,27*scale,26*scale,26*scale),ink)
			_text(str(data.get("value","—")),Rect2(0,59*scale,size.x,29*scale),21,ink)
			_text(str(data.get("note","")),Rect2(0,94*scale,size.x,18*scale),11,W.MUTED)
