extends Control
## Non-interactive shape grammar; no game data or RNG.
const D=preload("res://ui/components031/design.gd")
const Art=preload("res://ui/map/art.gd")
const W=preload("res://ui/widgets.gd")
var kind:String="hero"
var domain_id:String="general"
var asset:String="works"
var building_definition_id:String=""
var slot_type:String="Production"
var empty:bool=false
var tint:Color=W.MUTED
func _ready():
	mouse_filter=Control.MOUSE_FILTER_IGNORE;resized.connect(queue_redraw)
func _draw():
	if kind=="socket":
		var center=size/2;var r=minf(size.x,size.y)*.39
		if slot_type=="Operations":draw_circle(center,r,W.RAISED);draw_arc(center,r,0,TAU,40,tint,1.5,true)
		elif slot_type=="Logistics":
			var points=PackedVector2Array()
			for i in 6:points.append(center+Vector2(cos(i*TAU/6),sin(i*TAU/6))*r)
			draw_colored_polygon(points,W.RAISED);points.append(points[0]);draw_polyline(points,tint,1.5,true)
		elif slot_type=="Flex":
			for dx in [-4,4]:draw_rect(Rect2(center-Vector2(r,r)+Vector2(dx,0),Vector2(r*2,r*2)),tint,false,1.3)
		else:
			draw_rect(Rect2(center-Vector2(r,r),Vector2(r*2,r*2)),W.RAISED);draw_rect(Rect2(center-Vector2(r,r),Vector2(r*2,r*2)),tint,false,1.5)
		var name={"Production":"production","Logistics":"logistics","Operations":"operations","Flex":"slots"}.get(slot_type,"slots")
		var tex=Art.icon(name,W.ink_color(tint))
		if tex:draw_texture_rect(tex,Rect2(center-Vector2(10,10),Vector2(20,20)),false)
	elif kind=="dashed":
		var end=size-Vector2(1,1)
		draw_dashed_line(Vector2(1,1),Vector2(end.x,1),tint,1.2,5.,true,true)
		draw_dashed_line(Vector2(1,end.y),end,tint,1.2,5.,true,true)
		draw_dashed_line(Vector2(1,1),Vector2(1,end.y),tint,1.2,5.,true,true)
		draw_dashed_line(Vector2(end.x,1),end,tint,1.2,5.,true,true)
	else:
		var d=D.domain(domain_id);var accent=Color(d.accent);var soft=Color(d.tint)
		draw_rect(Rect2(Vector2.ZERO,size),W.fill_color(soft))
		var pattern=Color(accent,0.25)
		if domain_id in ["industrial","commercial","public","general"]:
			for x in range(-int(size.y),int(size.x)+int(size.y),24):
				var a=Vector2(x,0);var b=Vector2(x+size.y,size.y)
				if a.x<0:a=Vector2(0,-x)
				if b.x>size.x:b=Vector2(size.x,size.x-x)
				if a.y<=size.y and b.y>=0:draw_line(a,b,pattern,1.,true)
		elif domain_id=="agriculture":
			for y in range(10,int(size.y),16):draw_line(Vector2(8,y),Vector2(size.x-8,y),pattern,1.2,true)
		elif domain_id=="residential":
			for x in range(12,int(size.x),28):
				for y in range(10,int(size.y),28):draw_rect(Rect2(x,y,12,14),pattern,false)
		else:
			for x in range(16,int(size.x),38):draw_line(Vector2(x,0),Vector2(x,size.y),pattern,1.2,true)
		var tex:Texture2D
		if kind=="building":
			tex=Art.thumbnail(building_definition_id)
		else:
			tex=Art.sprite(asset)
		if tex:
			var a=minf(size.y*1.2,size.x*.8);draw_texture_rect(tex,Rect2(Vector2((size.x-a)/2,-size.y*.13),Vector2(a,a)),false)
		draw_line(Vector2(0,size.y-2),Vector2(size.x,size.y-2),accent,3.)
