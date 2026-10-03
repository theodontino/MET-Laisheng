extends RefCounted
## Original cartographic shapes. All marks read real terrain/traits; no RNG or
## entities are created. Coordinates remain on MapView's projected ground.
const Art=preload("res://ui/map/art.gd")
const Hex=preload("res://core/hex.gd")
const COLORS={"plain":Color("C7D19E"),"forest":Color("90B091"),"hill":Color("C9BEA2"),"wet":Color("A4C2B0"),"water":Color("79A9B9")}
static var _scales:Dictionary={}
static func building_scale(def_id:String) -> float:
	if _scales.has(def_id):return _scales[def_id]
	var info=Art.metadata(def_id);var bounds=info.get("bounds",[]);var size=info.get("size",[768,768])
	var tier=clampi(int(info.get("tier",1)),1,3)
	var visible=(float(bounds[2])-float(bounds[0]))/maxf(1,float(size[0])) if bounds.size()==4 else .55
	var value=[.88,1.04,1.22][tier-1]/maxf(.25,visible)
	_scales[def_id]=value;return value
static func terrain_symbols(tile:Dictionary) -> Array:
	var result=[];var traits=tile.get("traits",[])
	for record in [["tile.fertile","fertile","肥沃"],["tile.forest","forest","森林"],["tile.highland","highland","高地"],["tile.wet","wet","湿地"],["tile.water","water","水域"],["tile.waterfront","waterfront","水岸"]]:
		if record[0] in traits:result.append({"trait":record[0],"id":record[1],"label":record[2]})
	return result
static func terrain(view:Object,c:CanvasItem) -> void:
	var natural=view.layer in ["normal","terrain","operator"]
	c.draw_rect(Rect2(-8192,-8192,16384,16384),Color("DFE4D2") if natural else Color("D8E0DA"))
	var keys=view.game.s.cells.keys();keys.sort_custom(func(a,b):return view.flat_point(str(a)).y<view.flat_point(str(b)).y)
	# The board edge and every tile use the exact same projected surface as picking.
	for key in keys:
		var poly:PackedVector2Array=view.surface_polygon(str(key));var shadow=PackedVector2Array()
		for p in poly:shadow.append(p+Vector2(0,4))
		c.draw_colored_polygon(shadow,Color("758C73",.18))
	for key in keys:
		var tile:Dictionary=view.game.s.cells[key];var terrain=str(tile.get("terrain","plain"))
		var col:Color=COLORS.get(terrain,COLORS.plain)
		var relief=clampf(float(view.elevation_at(view.flat_point(str(key))))/100.,0.,1.)
		col=col.lightened(relief*.055)
		if not natural:col=col.lerp(Color("D5DED5"),.72)
		var poly:PackedVector2Array=view.surface_polygon(str(key));c.draw_colored_polygon(poly,col)
		var inset:PackedVector2Array=view.surface_polygon(str(key),.95)
		c.draw_colored_polygon(inset,col.lightened(.025))
		poly.append(poly[0]);c.draw_polyline(poly,Color("526E63",.22 if natural else .14),.65,true)
		var center:Vector2=view.flat_point(str(key))
		if terrain=="water":
			for row in [-1,0,1]:
				var wave=PackedVector2Array()
				for step in 7:wave.append(view.surface_point(center+Vector2((step-3)*7,row*18+sin(step*.9)*2)))
				c.draw_polyline(wave,Color("D6ECE0",.44),1.2,true)
			var neighbors=Hex.neighbors(str(key))
			for i in 6:
				var neighbor=str(neighbors[i])
				if not view.game.s.cells.has(neighbor) or str(view.game.s.cells[neighbor].terrain)=="water":continue
				view._edge(c,str(key),i,Color("E7DCBD"),2.4)
		elif str(tile.get("building",""))=="":
			landform(view,c,str(key),tile,natural)
		else:
			var yard:PackedVector2Array=view.surface_polygon(str(key),.72)
			c.draw_colored_polygon(yard,Color("DFDCC6",.56 if natural else .32))
			var edge=yard.duplicate();edge.append(edge[0]);c.draw_polyline(edge,Color("B9B99E",.28),1.0,true)
		# Compact corner stamp states a real immutable land condition, not a stock.
		if natural:
			var symbols=terrain_symbols(tile)
			if not symbols.is_empty() and terrain!="water":
				var at=view.surface_point(center+Vector2(-36,30))
				glyph(c,at,str(symbols[0].id),.62,Color("4E6850",.72))
static func landform(view:Object,c:CanvasItem,key:String,tile:Dictionary,natural:bool) -> void:
	var origin:Vector2=view.flat_point(key);var kind=str(tile.get("terrain","plain"))
	var ink=Color("567B61") if natural else Color("94A69B")
	var h=stable_hash(key+":"+str(view.game.s.get("seed",0)))
	if kind=="forest":
		var p:Vector2=view.surface_point(origin+Vector2(0,8))
		c.draw_colored_polygon(PackedVector2Array([p+Vector2(-33,-3),p+Vector2(-18,-15),p+Vector2(10,-19),p+Vector2(31,-2),p+Vector2(20,14),p+Vector2(-16,16)]),Color("6B9474",.16))
		var offsets=[Vector2(-20,0),Vector2(0,-16),Vector2(22,-2),Vector2(-6,17),Vector2(18,21)]
		for i in offsets.size():
			var off:Vector2=offsets[i]+Vector2(float((h>>(i+1))%7)-3,float((h>>(i+4))%5)-2)
			var at:Vector2=view.surface_point(origin+off)
			tree(c,at,.73+float((h>>(i+2))%4)*.065,ink,(h+i)%3)
	elif kind=="hill":
		# Low polygon shoulders, a lit ridge and a shaded face. The small summit
		# variation is stable per source tile, with no procedural texture/RNG.
		var p:Vector2=view.surface_point(origin+Vector2(0,6))
		c.draw_colored_polygon(PackedVector2Array([p+Vector2(-36,5),p+Vector2(-25,-9),p+Vector2(10,-14),p+Vector2(34,0),p+Vector2(27,15),p+Vector2(-17,18)]),Color("ACAD90",.32))
		mound(c,p+Vector2(-14,-4),18+float(h%6),10+float((h>>4)%5),Color("A9AA91") if natural else Color("AAB7AD"),h%3)
		mound(c,p+Vector2(12,2),23+float((h>>6)%5),14+float((h>>8)%5),Color("B9B49A") if natural else Color("B8C3B8"),(h>>2)%3)
		if h%2==0:mound(c,p+Vector2(-13,11),11,5,Color("A8AA90"),1)
	elif kind=="wet":
		var p:Vector2=view.surface_point(origin)
		c.draw_colored_polygon(PackedVector2Array([p+Vector2(-29,3),p+Vector2(-9,-8),p+Vector2(24,-3),p+Vector2(32,9),p+Vector2(-5,16)]),Color("80AEA9",.65))
		for x in [-20,-8,11,24]:glyph(c,p+Vector2(x,7+(x%3)),"wet",.72,ink)
	elif kind=="plain":
		for off in [Vector2(-10,-2),Vector2(5,2),Vector2(19,8)]:glyph(c,view.surface_point(origin+off),"fertile",.75,Color("8A9F60",.72))
static func stable_hash(value:String) -> int:
	var result=5381
	for i in value.length():result=int(((result<<5)+result)^value.unicode_at(i))&0x7fffffff
	return result
static func mound(c:CanvasItem,p:Vector2,width:float,height:float,base:Color,variant:int) -> void:
	var left=p+Vector2(-width,3);var right=p+Vector2(width,5)
	var back=p+Vector2(width*.22,-height-4)
	var ridge=p+Vector2(-width*.24-float(variant),-height)
	var toe=p+Vector2(width*.10,10)
	c.draw_colored_polygon(PackedVector2Array([left,ridge,back,right,toe]),base)
	c.draw_colored_polygon(PackedVector2Array([left,ridge,toe]),base.lightened(.12))
	c.draw_colored_polygon(PackedVector2Array([ridge,back,right,toe]),base.darkened(.07))
	c.draw_colored_polygon(PackedVector2Array([ridge,back,back+Vector2(4,5),ridge+Vector2(1,5)]),base.lightened(.17))
	c.draw_line(left,toe,Color(base.darkened(.12),.5),1.1,true)
static func tree(c:CanvasItem,p:Vector2,scale:float,ink:Color,variant:int=0) -> void:
	c.draw_colored_polygon(PackedVector2Array([p+Vector2(-8,2)*scale,p+Vector2(1,-2)*scale,p+Vector2(10,1)*scale,p+Vector2(2,5)*scale]),Color("4C7158",.16))
	c.draw_line(p,p+Vector2(0,-15)*scale,Color("6E7760"),2.0*scale,true)
	var top=p+Vector2(-1-variant,-25-variant*2)*scale
	var left=p+Vector2(-10,-13)*scale;var bottom=p+Vector2(0,-6)*scale
	var right=p+Vector2(10,-14)*scale;var shoulder=p+Vector2(5,-22)*scale
	c.draw_colored_polygon(PackedVector2Array([left,p+Vector2(-8,-21)*scale,top,shoulder,right,bottom]),ink)
	c.draw_colored_polygon(PackedVector2Array([top,shoulder,right,bottom,p+Vector2(1,-16)*scale]),ink.lightened(.16))
	c.draw_colored_polygon(PackedVector2Array([left,p+Vector2(1,-16)*scale,bottom]),ink.darkened(.09))
static func glyph(c:CanvasItem,p:Vector2,kind:String,scale:float,ink:Color) -> void:
	if kind in ["water","waterfront"]:
		for row in 2:c.draw_polyline(PackedVector2Array([p+Vector2(-8,row*5)*scale,p+Vector2(-3,row*5-2)*scale,p+Vector2(3,row*5+1)*scale,p+Vector2(8,row*5-1)*scale]),ink,maxf(.8,1.3*scale),true)
	elif kind=="highland":
		c.draw_polyline(PackedVector2Array([p+Vector2(-9,4)*scale,p+Vector2(-2,-8)*scale,p+Vector2(2,-1)*scale,p+Vector2(6,-5)*scale,p+Vector2(10,4)*scale]),ink,1.4,true)
	elif kind=="forest":tree(c,p+Vector2(0,5)*scale,scale*.47,ink)
	elif kind=="wet":
		for x in [-4,2,6]:
			c.draw_line(p+Vector2(x,4)*scale,p+Vector2(x-1,-10)*scale,ink,maxf(.8,1.1*scale),true)
			c.draw_line(p+Vector2(x-1,-5)*scale,p+Vector2(x+3,-9)*scale,ink,maxf(.8,1.1*scale),true)
	else:
		c.draw_line(p+Vector2(0,5)*scale,p+Vector2(0,-11)*scale,ink,maxf(.8,1.2*scale),true)
		for y in [-2,-7]:
			c.draw_line(p+Vector2(0,y)*scale,p+Vector2(-5,y-4)*scale,ink,maxf(.8,1.4*scale),true)
			c.draw_line(p+Vector2(0,y+2)*scale,p+Vector2(5,y-2)*scale,ink,maxf(.8,1.4*scale),true)
static func courtyard(view:Object,c:CanvasItem,tile:String,definition:Dictionary,tier:int) -> void:
	var origin:Vector2=view.flat_point(tile);var domain=str(definition.get("domain",""))
	var p:Vector2=view.surface_point(origin+Vector2(0,30))
	c.draw_line(view.surface_point(origin+Vector2(-29,28)),view.surface_point(origin+Vector2(30,28)),Color("B8B99F",.60),3.5,true)
	if domain in ["residential","public","commercial","community"]:
		for side in [-1,1]:tree(c,view.surface_point(origin+Vector2(side*34,17)),.32,Color("6A9474"))
	else:
		for i in tier:
			var at=p+Vector2((i-(tier-1)*.5)*8,0)
			c.draw_colored_polygon(PackedVector2Array([at+Vector2(-3,-3),at+Vector2(3,-3),at+Vector2(4,1),at+Vector2(-2,1)]),Color("A5A18B"))
	# Three unobtrusive paving markers encode permanent Tier, never operation.
	for i in tier:c.draw_circle(p+Vector2((i-(tier-1)*.5)*6,8),1.7,Color("698C84"))
