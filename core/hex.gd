extends RefCounted
## Pure mathematical memoization only; never retains cities, fields, RNG or entities.
const COORD_CACHE_LIMIT=4096
const DISTANCE_CACHE_LIMIT=8192
static var _coordinate_cache:Dictionary={}
static var _distance_cache:Dictionary={}
static var _distance_entries:int=0

static func clear_math_cache() -> void:
	_coordinate_cache.clear();_distance_cache.clear();_distance_entries=0

const DIRS = [Vector2i(1,0),Vector2i(1,-1),Vector2i(0,-1),Vector2i(-1,0),Vector2i(-1,1),Vector2i(0,1)]
static func key(q:int,r:int) -> String:
	return "%d,%d" % [q,r]
static func coords(k:String) -> Vector2i:
	if _coordinate_cache.has(k):return _coordinate_cache[k]
	var s=k.split(",")
	var result=Vector2i(int(s[0]),int(s[1]))
	if _coordinate_cache.size()>=COORD_CACHE_LIMIT:_coordinate_cache.clear()
	_coordinate_cache[k]=result
	return result
static func neighbors(k:String) -> Array:
	var p=coords(k)
	var result=[]
	for d in DIRS:
		result.append(key(p.x+d.x,p.y+d.y))
	return result
static func connected(cells:Array) -> bool:
	if cells.is_empty(): return false
	var seen={str(cells[0]):true}
	var queue=[str(cells[0])]
	while not queue.is_empty():
		var p=queue.pop_back()
		for k in neighbors(p):
			if k in cells and not seen.has(k):
				seen[k]=true
				queue.append(k)
	return seen.size()==cells.size()
static func adjacent(a:Array,b:Array) -> bool:
	for p in a:
		for n in neighbors(str(p)):
			if n in b: return true
	return false
# Geography has its own integer field derivation: it never advances gameplay RNG.
# Keep these constants/operations identical to MetCore/Foundation/Hex.cs.
static func _field(seed:int,q:int,r:int,salt:int) -> int:
	const PRIME=2147483647
	var n=((seed%PRIME)+PRIME)%PRIME
	n=(n+(q+17)*92821+(r+17)*68917+salt*19349663)%PRIME
	n=n*48271%PRIME;n=n*n%PRIME
	return n%1000
static func _smooth(seed:int,q:int,r:int,salt:int) -> int:
	var total=_field(seed,q,r,salt)
	for d in DIRS:total+=_field(seed,q+d.x,r+d.y,salt)
	return int(total/7)
static func _radius(q:int,r:int) -> int:
	return maxi(absi(q),maxi(absi(r),absi(q+r)))
static func _bump(q:int,r:int,cq:int,cr:int,falloff:int) -> int:
	return maxi(0,1000-_radius(q-cq,r-cr)*falloff)
static func make_map(kind:int,seed:int=0) -> Dictionary:
	var geo_seed=((seed%2147483647)+2147483647)%2147483647+kind*104729
	var river:Array[Vector2i]=[];var river_q=_field(geo_seed,0,0,1)%3
	river.append(Vector2i(river_q,-4))
	# One adjacent water cell per axial row. Alternating the two downhill
	# directions creates bends, while the central starter clearing stays dry.
	for r in range(-3,5):
		var left=_field(geo_seed,r,0,2)<530
		if r<=-1 and river_q>-1 and river_q+1>=-r:left=true
		var next=river_q-(1 if left else 0);var minimum=maxi(-3,-4-r)
		river_q=maxi(minimum,next)
		if r>=-1:river_q=mini(-1,river_q)
		river.append(Vector2i(river_q,r))
	var ridge_q=2+_field(geo_seed,0,0,3)%3;var ridge_r=-2+_field(geo_seed,0,0,4)%3
	var north_q=_field(geo_seed,0,0,5)%3;var north_r=-3
	var grove_q=1+_field(geo_seed,0,0,6)%2;var grove_r=1+_field(geo_seed,0,0,7)%2
	var west_q=-3+_field(geo_seed,0,0,8)%2;var west_r=_field(geo_seed,0,0,9)%3
	var cells={}
	for q in range(-4,5):
		for r in range(-4,5):
			if _radius(q,r)>4:continue
			var nearest=0;var river_distance=99;var order=-1
			for i in range(river.size()):
				var d=_radius(q-river[i].x,r-river[i].y)
				if d<river_distance:nearest=i;river_distance=d
				if d==0:order=i
			var ridge=maxi(_bump(q,r,ridge_q,ridge_r,240),int(_bump(q,r,north_q,north_r,290)*(100 if kind==2 else 65)/100))
			var grove=maxi(_bump(q,r,grove_q,grove_r,240),_bump(q,r,west_q,west_r,270))
			if kind==1:grove=maxi(grove,_bump(q,r,1,0,240))
			var river_height=200-nearest*12
			var height=mini(940,river_height+45+river_distance*45+int(ridge*42/100)+int((_smooth(geo_seed,q,r,10)-500)/8))
			var moisture=maxi(0,mini(1000,320+maxi(0,3-river_distance)*130+int(grove*22/100)+int((_smooth(geo_seed,q,r,11)-500)/4)))
			var terrain="plain"
			if order>=0:terrain="water";height=200-order*12;moisture=1000
			elif ridge>=580:terrain="hill"
			elif river_distance==1 and height<430 and moisture>=650:terrain="wet"
			elif grove+int((_smooth(geo_seed,q,r,12)-500)/3)>=(450 if kind==1 else 520):terrain="forest"
			if q==0 and (r==0 or r==1):terrain="plain"
			cells[key(q,r)]={"q":q,"r":r,"terrain":terrain,"zone":"","site":"","hidden":"","elevation01":height/1000.0,"moisture01":moisture/1000.0,"river_order":order}
	for k in cells:
		cells[k]["waterfront"]=false
		for n in neighbors(k):
			if cells.has(n) and cells[n]["terrain"]=="water":cells[k]["waterfront"]=true
	return cells
static func geography(cells:Dictionary,seed:int,kind:int) -> Dictionary:
	var river=[];var ordered={}
	for id in cells:
		if cells[id].river_order>=0:ordered[int(cells[id].river_order)]=id
	var indexes=ordered.keys();indexes.sort()
	for index in indexes:river.append(ordered[index])
	return {"version":1,"seed":seed,"landing":kind,"river_ids":river,"elevation_unit":"normalized_0_1"}

static func distance(a:String,b:String) -> int:
	if _distance_cache.has(a) and _distance_cache[a].has(b):return _distance_cache[a][b]
	var d=coords(a)-coords(b)
	var result=maxi(absi(d.x),maxi(absi(d.y),absi(d.x+d.y)))
	if _distance_entries>=DISTANCE_CACHE_LIMIT:
		_distance_cache.clear();_distance_entries=0
	if not _distance_cache.has(a):_distance_cache[a]={}
	_distance_cache[a][b]=result;_distance_entries+=1
	return result
