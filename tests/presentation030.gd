extends RefCounted
const Game=preload("res://core/game.gd")
const Planner=preload("res://core/planning.gd")
const Store=preload("res://core/storage.gd")
const Data=preload("res://ui/view_models/map_data.gd")
const Art=preload("res://ui/map/art.gd")
const Paths=preload("res://tests/paths.gd")
var checks=[]
func check(ok:bool,name:String):checks.append({"pass":ok,"name":name});print(("VIEW PASS " if ok else "VIEW FAIL ")+name)
func fresh():
	var g=Game.new();g.new_game(7,1000000.);g.select_block(0);g.choose_origin(g.s.origin_choices[0]);return g
func run() -> Dictionary:
	print("VIEW BOOT")
	var g=fresh();print("VIEW FRESH")
	var planner=Planner.new();var before=Store.encode(g.s)
	print("VIEW PLANNER")
	var snap=Data.current(g,planner);check(snap.get("forecast",false),"first display is explicitly a forecast")
	check(Store.encode(g.s)==before,"initial display changes no stored world value")
	g.pulse(.1);snap=Data.current(g,planner);check(not snap.forecast,"committed results identified as actual")
	var home=g.playable_ids()[0];var tile=g.s.projects[home].tile
	var gid=g.s.groups.keys()[0]
	# Deliberately conflicting service dimensions: never replace one with best-of-all.
	var fixture={"tiles":{tile:{"fields":{"medical":[{"field_id":"m1","source_id":"med","value":1.,"via":""},{"field_id":"m1","source_id":"med","value":.5,"via":"relay"}],"education":[{"field_id":"e1","source_id":"edu","value":1.,"via":""}]}}},"groups":{gid:{"host":home,"count":20.,"living":{"medical":.5,"education":1.},"employment":{}}},"projects":{home:{"tile":tile},"med":{"status":"running","paused":false},"edu":{"status":"running","paused":false}},"metrics":{"service_links":[],"service_covered":{gid:{"medical":10.,"education":20.}},"utilization":{"med":{"utilization":1.},"edu":{"utilization":1.}}},"error":""}
	var med=Data.service(fixture,tile,"medical");var edu=Data.service(fixture,tile,"education")
	check(absf(med.ratio-.5)<.000001 and absf(edu.ratio-1)<.000001,"medical and education never share a maximum")
	check(med.served==10 and med.people==20,"finite 10-place service does not color 20 people as fully served")
	check(med.sources.size()==1 and med.sources.med==1.,"duplicate relay paths remain one reachable provider")
	check(med.reason=="可达，但有效容量不足","inside coverage but capacity-short has a separate reason")
	fixture.projects.med.paused=true;med=Data.service(fixture,tile,"medical");check(med.reason=="提供方未运行","paused provider is not mislabeled a distance issue")
	fixture.tiles[tile].fields.medical=[];med=Data.service(fixture,tile,"medical");check(med.reason=="范围外 / 无可达来源","inaccessible is distinct from capacity shortage")
	fixture.error="not_ready";med=Data.service(fixture,tile,"medical");check(not med.known and med.reason=="未结算","missing data is not zero coverage")
	fixture.error="";fixture.groups={};med=Data.service(fixture,tile,"medical");check(med.reason=="空地 / 无居民","empty tiles are not failed service recipients")
	var mapped=Art.bindings();check(mapped.size()==40,"all 40 running bonuses have explicit visual bindings")
	for id in g.c.bonuses:check(mapped.has(id),"visual registration "+str(id))
	Art.initialize();check(Art.catalog.assets.size()==30,"ten buildings, twelve props, eight reusable modules")
	check(Art.atlases.size()==2,"two shared world atlases rather than texture per card")
	for id in g.c.projects:check(Art.sprite(id)!=null,"real runtime texture for "+str(id))
	before=Store.encode(g.s);g.create_tender(["0,-1"],"farm");var draft=g.s.draft.duplicate(true);before=Store.encode(g.s)
	var all_results=[]
	for offer in draft.offers:
		var result=planner.compare(g,"farm","0,-1",offer);all_results.append(result)
		check(result.get("ok",false) and result.has("map_delta") and result.has("map_snapshot"),"candidate yields source-aware map difference "+offer.id)
	check(Store.encode(g.s)==before,"planning all candidates preserves world, wallet, RNG, tick, IDs and log")
	check(all_results.all(func(x):return x.snapshot_id==all_results[0].snapshot_id),"all offers share same immutable baseline version")
	var p=all_results[0];var source=planner.snapshot(g);var actual=0.
	for entry in source.metrics.service_links:
		if entry.kind=="medical":actual+=entry.people
	var new_amount=0.
	for entry in p.world.service_links:
		if entry.kind=="medical":new_amount+=entry.people
	var deltas=0.
	for t in p.map_delta:deltas+=p.map_delta[t].services.medical
	check(absf(deltas-(new_amount-actual))<.00001,"map service deltas reconcile with two forecast worlds")
	var cached_started=Time.get_ticks_usec();planner.compare(g,"farm","0,-1",draft.offers[0]);var cached_ms=(Time.get_ticks_usec()-cached_started)/1000.0
	check(cached_ms<50,"cached candidate lookup under 50ms on this runner")
	for k in g.Rules.ids(g.s.cells):
		if g.s.cells[k].building=="" and not "tile.water" in g.s.cells[k].traits:planner.compare(g,"farm",k)
	check(planner.lru.size()<=16,"prediction cache bounded to 16 recent entries")
	check(Store.encode(g.s)==before,"many map positions cannot consume gameplay resources or redraw offers")
	var old_version=planner.version(g);g.pulse(.1);check(planner.version(g)!=old_version,"committed world invalidates prediction versions")
	var policy_before=Store.encode(g.s);var pp=planner.policy(g,"tax_trade");check(pp.ok and pp.has("map_delta"),"policy uses same-engine differential map")
	check(Store.encode(g.s)==policy_before,"policy preview leaves current tax policy unchanged")
	var original_file=FileAccess.get_file_as_string(Paths.file("legacy021-fixture.json"));var imported=Store.decode(original_file)
	check(imported.ok,"native 021 baseline fixture is valid")
	if imported.ok:
		var g2=Game.new();var original=imported.state.duplicate(true);var migration=g2.migrate_state(imported.state)
		check(migration.ok,"021-to-030 migration accepted")
		for name in ["groups","projects","bonus_instances","rng","quote_bank","currency","dt"]:
			check(Store.canonical(g2.s[name])==Store.canonical(original[name]),"migration preserves "+name)
		check(FileAccess.get_file_as_string(Paths.file("legacy021-fixture.json"))==original_file,"original old-city bytes unchanged")
		var duplicate=g2.migrate_state(original.duplicate(true));check(duplicate.ok and g2.s.block_opportunities==original.block_opportunities,"reimport awards nothing twice")
	var times=planner.timings.duplicate();times.sort();var p95=float(times[int(times.size()*.95)]) if not times.is_empty() else 0.
	var failures=checks.filter(func(x):return not x.pass).size()
	var result={"passed":checks.size()-failures,"failed":failures,"checks":checks,"preview_uncached_p95_ms":p95,"preview_cached_ms":cached_ms,"platform":"Linux x86_64 native Godot 4.7.2, not Mac/mobile performance","world_atlas_estimated_gpu_mib":42.67}
	FileAccess.open(Paths.file("presentation030.json"),FileAccess.WRITE).store_string(JSON.stringify(result,"\t"));return result
