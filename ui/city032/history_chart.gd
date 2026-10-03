extends Control
## Native, responsive chart of recorded simulation-time samples, not invented history.
const W=preload("res://ui/widgets.gd")
var samples:Array=[]
var series=[{"key":"income","label":"收入","color":W.ACCENT},{"key":"upkeep","label":"支出","color":W.WARNING}]
var font:Font
func _ready():
	mouse_filter=Control.MOUSE_FILTER_IGNORE;custom_minimum_size.y=190*W.text_scale
	font=get_theme_default_font()
func set_data(rows:Array,mode:String="rates"):
	samples=rows
	series=[{"key":"balance","label":"余额","color":W.INFO}] if mode=="balance" else [{"key":"income","label":"收入","color":W.ACCENT},{"key":"upkeep","label":"支出","color":W.WARNING}]
	queue_redraw()
func _draw():
	if font==null:return
	var r=Rect2(Vector2(66,38),Vector2(maxf(30,size.x-88),maxf(32,size.y-78)))
	var max_value=1.;var min_value=0.
	for row in samples:
		for s in series:max_value=maxf(max_value,float(row.get(s.key,0)));min_value=minf(min_value,float(row.get(s.key,0)))
	var range_value=maxf(1,max_value-min_value)
	for i in 4:
			var y=r.position.y+r.size.y*i/3.;draw_line(Vector2(r.position.x,y),Vector2(r.end.x,y),W.DIVIDER,1.)
			var v=max_value-range_value*i/3.;draw_string(font,Vector2(2,y+5),"%.1f"%v,HORIZONTAL_ALIGNMENT_LEFT,-1,int(14*W.text_scale),W.MUTED)
	var lx=r.position.x
	for s in series:draw_line(Vector2(lx,16),Vector2(lx+20,16),s.color,3);draw_string(font,Vector2(lx+28,21),s.label,HORIZONTAL_ALIGNMENT_LEFT,-1,int(14*W.text_scale),W.TEXT);lx+=104
	if samples.size()<2:
		draw_string(font,r.position+Vector2(10,r.size.y*.52),"结算后开始绘制历史，不补造旧数据",HORIZONTAL_ALIGNMENT_LEFT,r.size.x,int(14*W.text_scale),W.MUTED);return
	var t0=float(samples[0].h);var t1=float(samples[-1].h);var span=maxf(.001,t1-t0)
	for s in series:
		var points=PackedVector2Array()
		for row in samples:
			points.append(r.position+Vector2((float(row.h)-t0)/span*r.size.x,(max_value-float(row.get(s.key,0)))/range_value*r.size.y))
		draw_polyline(points,s.color,2.5,true)
	draw_string(font,Vector2(r.position.x,r.end.y+26),"%.1fh"%t0,HORIZONTAL_ALIGNMENT_LEFT,-1,int(14*W.text_scale),W.MUTED)
	draw_string(font,Vector2(r.end.x-85,r.end.y+26),"%.1fh"%t1,HORIZONTAL_ALIGNMENT_RIGHT,85,int(14*W.text_scale),W.MUTED)
