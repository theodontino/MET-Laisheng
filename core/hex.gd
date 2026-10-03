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
static func make_map(kind:int) -> Dictionary:
	var cells={}
	for q in range(-4,5):
		for r in range(-4,5):
			if maxi(absi(q),maxi(absi(r),absi(q+r)))>4: continue
			var terrain="plain"
			if q==-2 and r>=-2 and r<=3: terrain="water"
			elif q==-1 and r==-3: terrain="water"
			elif q==0 and r==-4: terrain="water"
			elif q>=2 and r<=0: terrain="hill"
			elif q==1 and r>=1 or q==-3 and r>=1: terrain="forest"
			elif q<=-3 and r<=0: terrain="wet"
			if kind==1 and q>=1 and terrain=="plain": terrain="forest"
			if kind==2 and q>=1 and terrain=="forest": terrain="hill"
			cells[key(q,r)]={"q":q,"r":r,"terrain":terrain,"zone":"","site":"","hidden":""}
	for k in cells:
		cells[k]["waterfront"]=false
		for n in neighbors(k):
			if cells.has(n) and cells[n]["terrain"]=="water":cells[k]["waterfront"]=true
	return cells

static func distance(a:String,b:String) -> int:
	if _distance_cache.has(a) and _distance_cache[a].has(b):return _distance_cache[a][b]
	var d=coords(a)-coords(b)
	var result=maxi(absi(d.x),maxi(absi(d.y),absi(d.x+d.y)))
	if _distance_entries>=DISTANCE_CACHE_LIMIT:
		_distance_cache.clear();_distance_entries=0
	if not _distance_cache.has(a):_distance_cache[a]={}
	_distance_cache[a][b]=result;_distance_entries+=1
	return result
