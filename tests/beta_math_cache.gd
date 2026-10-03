extends SceneTree
const Hex=preload("res://core/hex.gd")
const Game=preload("res://core/game.gd")
const Store=preload("res://core/storage.gd")
const Paths=preload("res://tests/paths.gd")
var checks=[]
func check(ok:bool,label:String) -> void:
	checks.append({"pass":ok,"name":label});print(("PASS " if ok else "FAIL ")+label)
func _initialize():call_deferred("run")
func exact_distance(a:String,b:String) -> int:
	var aa=a.split(",");var bb=b.split(",")
	var q=int(aa[0])-int(bb[0]);var r=int(aa[1])-int(bb[1])
	return maxi(absi(q),maxi(absi(r),absi(q+r)))
func run():
	Hex.clear_math_cache()
	var keys=[]
	for q in range(-7,8):
		for r in range(-7,8):keys.append(Hex.key(q,r))
	var exact=true
	for a in keys:
		for b in keys:
			if Hex.distance(a,b)!=exact_distance(a,b) or Hex.distance(a,b)!=Hex.distance(b,a):exact=false
	check(exact,"negative/positive/boundary coordinates match exact hex metric through repeated cache eviction")
	check(Hex._distance_entries<=Hex.DISTANCE_CACHE_LIMIT and Hex._coordinate_cache.size()<=Hex.COORD_CACHE_LIMIT,"distance and coordinate caches are bounded")
	var unchanged=true
	for i in 4500:
		var key=Hex.key(i,-i);var coord=Hex.coords(key);coord.x+=100
		if Hex.coords(key)!=Vector2i(i,-i):unchanged=false
	check(unchanged,"coordinate results are value copies and cannot poison future reads")
	check(Hex._coordinate_cache.size()<=Hex.COORD_CACHE_LIMIT,"coordinate limit also holds outside the standard city map")
	var a=Game.new();a.new_game(1927,1000000.);a.select_block(0);a.choose_origin(a.s.origin_choices[0])
	var tiles=a.s.cells.keys();tiles.sort();var defs=["homestead","neighborhood","workshop"]
	for tile in tiles:
		if a.playable_ids().size()>=12:break
		if a.s.cells[tile].building=="" and not "tile.water" in a.s.cells[tile].traits:a.create_building(defs[a.playable_ids().size()%3],tile,"commons",true)
	a.refresh_metrics();a.s.settings.user_paused=false;a.Clock.set_mode(a,"burn")
	var initial=Store.encode(a.s);var b=Game.new();b.s=Store.decode(initial).state;b.compiled=b.Rules.compile(b)
	Hex.clear_math_cache();var cold=a.advance_active(.1);var cold_state=Store.encode(a.s)
	for tile in tiles:
		for other in tiles:Hex.distance(tile,other)
	var warm=b.advance_active(.1)
	check(cold.reason==warm.reason and cold.steps==warm.steps and Store.encode(b.s)==cold_state,"cold vs warm cache preserves restored full tick state, every source receipt, wallets and RNG")
	var read_before=Store.encode(b.s)
	Hex.clear_math_cache();b.Rules.compile(b);b.Fields.access(b,b.compiled)
	check(Store.encode(b.s)==read_before,"mathematical cache warming cannot mutate gameplay state")
	var failed=checks.filter(func(row):return not row.pass).size()
	FileAccess.open(Paths.file("beta_math_cache.json"),FileAccess.WRITE).store_string(JSON.stringify({"passed":checks.size()-failed,"failed":failed,"checks":checks},"\t"))
	print("BETA MATH CACHE ",checks.size()-failed," passed, ",failed," failed");quit(1 if failed else 0)
