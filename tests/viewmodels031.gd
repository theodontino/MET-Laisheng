extends RefCounted
const Game=preload("res://core/game.gd")
const Store=preload("res://core/storage.gd")
const Planner=preload("res://core/planning.gd")
const Data=preload("res://ui/view_models/map_data.gd")
const Model=preload("res://ui/view_models/inspector_model.gd")
const Paths=preload("res://tests/paths.gd")
var checks=[]
func check(ok:bool,name:String):checks.append({"pass":ok,"name":name});print(("VM PASS " if ok else "VM FAIL ")+name)
func run() -> Dictionary:
	var g=Game.new();g.new_game(7,1000000);g.select_block(0);g.choose_origin(g.s.origin_choices[0])
	# Local complete fixture. It tests UI reads, not normal game progression.
	var bases=["hamlet","farm","timber","power","works","market","school","clinic","park","warehouse"]
	for base in bases:
		var key=""
		for tile in g.Rules.ids(g.s.cells):
			if g.s.cells[tile].building=="" and not "tile.water" in g.s.cells[tile].traits:key=tile;break
		g.create_building(base,key,"civic",true)
	g.refresh_metrics();var planner=Planner.new();var before=Store.encode(g.s);var shot=Data.current(g,planner)
	for id in g.playable_ids():
		var m=Model.build(g,shot,id)
		check(m.id==id and m.snapshot_id==shot.snapshot_id,"one snapshot for "+m.base)
		var unique=[];var exact=true
		for input in m.inputs:
			var total=0.
			for row in input.children:total+=float(row.rate)
			exact=exact and absf(total-input.value)<.000001 and not input.id in unique;unique.append(input.id)
		check(exact,"input aggregation retains distinct resources for "+m.base)
		check(m.status.code in ["ok","warning","danger","inactive","unknown"],"typed state for "+m.base)
		if m.base=="power":check(m.outputs.any(func(x):return x.id=="energy"),"electricity remains a displayed supply, not a stock")
		if m.base=="works":check(m.outputs.any(func(x):return x.id=="goods"),"zero-running producer still has a visible output row")
	check(Store.encode(g.s)==before,"reading every building changes neither state nor RNG")
	var original_file=FileAccess.get_file_as_string(Paths.file("legacy030-fixture.json"));var old=Store.decode(original_file)
	check(old.ok,"real 030 city decode")
	if old.ok:
		var saved=old.state.duplicate(true);var h=Game.new();var migration=h.migrate_state(old.state);check(migration.ok,"030 to 031 import accepted")
		for key in ["projects","groups","bonus_instances","rng","quote_bank","currency","dt","city_goal","block_opportunities"]:
			check(Store.canonical(h.s[key])==Store.canonical(saved[key]),"030 import preserves "+key)
		check(FileAccess.get_file_as_string(Paths.file("legacy030-fixture.json"))==original_file,"original 030 file bytes untouched")
		var restore=Store.decode(Store.encode(h.s));check(restore.ok,"031 save readable after import")
		var i=Game.new();i.migrate_state(restore.state);h.pulse(.1);i.pulse(.1)
		check(Store.canonical(h.s)==Store.canonical(i.s),"imported 031 save resumes deterministically after roundtrip")
	var failures=checks.filter(func(x):return not x.pass).size();var result={"passed":checks.size()-failures,"failed":failures,"checks":checks}
	FileAccess.open(Paths.file("viewmodels031.json"),FileAccess.WRITE).store_string(JSON.stringify(result,"\t"));return result
