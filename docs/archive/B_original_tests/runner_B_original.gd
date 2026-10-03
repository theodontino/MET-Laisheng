extends RefCounted
const Paths=preload("res://tests/paths.gd")
const Game=preload("res://core/game.gd")
const Inv=preload("res://core/inventory.gd")
const Store=preload("res://core/storage.gd")
const Hex=preload("res://core/hex.gd")
var checks:Array=[]
func check(value:bool,name:String):
	checks.append({"name":name,"pass":value})
	print(("PASS " if value else "FAIL ")+name)
func close(a:float,b:float,tolerance:float=.0001) -> bool:return absf(a-b)<tolerance
func fresh(seed_value:int=7):
	var g=Game.new();g.new_game(seed_value,1000000.0);g.select_block(0);g.choose_origin(g.s.origin_choices[0]);return g
func fixture(defs:Array):
	var g=fresh();g.s.projects={};g.s.sites={};g.s.relations={};g.s.events=[]
	for k in g.s.cells:g.s.cells[k].site=""
	var cells=["0,0","0,1","1,0","1,1","2,0","2,-1","0,2","1,-1","3,-1"]
	for i in defs.size():g._create_starter(defs[i],[cells[i]])
	g.s.currency=1200;g.s.dt=72;g.refresh_metrics();return g
func advance(g,h:float):
	var left=h
	while left>.00001 and g.s.phase=="play":
		if not g.s.events.is_empty():g.opportunity("skip")
		var result=g.Sim.advance(g,minf(left,1.0),false,false)
		left-=result.advanced
		if result.advanced<=0:break
	while not g.s.events.is_empty():g.opportunity("skip")
func first_id(g,def:String) -> String:
	for p in g.s.projects.values():
		if p.def==def:return p.id
	return ""
func build(g,def:String,cell:String):
	g.zone_cells([cell],g.c.projects[def].zone)
	var err=g.create_tender([cell],[g.s.policy_tags[0]])
	if err!="":return err
	# Fixture controls the selected definition, but still executes the real command.
	g.s.draft.offers=[def]
	return g.accept_offer(def)
func run() -> int:
	var g=fresh()
	check(g.c.projects.size()==20 and g.c.upgrades.size()==12 and g.c.origins.size()==4,"content budget: 20/12/4")
	check(g.c.fusions.size()==4 and g.c.opportunities.size()==6,"content budget: 4 fusion families / 6 opportunities")
	check(g.s.cells.size()==61,"fixed axial hex map has 61 cells")
	check(Hex.connected(["0,0","0,1","1,0"]) and not Hex.connected(["0,0","3,0"]),"site connectivity")
	check(Hex.adjacent(["0,0"],["1,0"]) and not Hex.adjacent(["0,0"],["2,0"]),"footprint perimeter adjacency")
	var twin=fresh()
	check(JSON.stringify(g.s,"",true,true)==JSON.stringify(twin.s,"",true,true),"same seed and commands reproduce opening")
	var stocks={}
	Inv.add(stocks,"material",["basic","heavy"],10,100);Inv.add(stocks,"material",["basic","light"],8,100)
	check(close(Inv.total(stocks,"material"),18) and close(Inv.total(stocks,"material",["heavy"]),10),"inventory signatures distinguish compatible stock")
	check(close(Inv.take(stocks,"material",["heavy"],12).taken,10) and close(Inv.total(stocks,"material"),8),"no incompatible material substitution")
	var overlapping={};Inv.add(overlapping,"goods",["basic"],9,100)
	var ratio=Inv.consume_recipe(overlapping,[{"resource":"goods","rate":6.0,"tags":[]},{"resource":"goods","rate":6.0,"tags":["basic"]}],1)
	check(close(ratio,.75,.0001) and Inv.total(overlapping,"goods")<.001,"overlapping recipes are atomic, no double counting")
	check(close(Inv.add(stocks,"material",[],200,25),17),"stock caps prevent overflow")
	var encoded=Store.encode(g.s);var decoded=Store.decode(encoded)
	check(decoded.ok and g.validate_state(decoded.state)=="","versioned save roundtrip validation")
	var malformed=g.s.duplicate(true);malformed.projects[malformed.projects.keys()[0]]="bad"
	check(g.validate_state(malformed)!="","malformed nested project is rejected without executing")
	malformed=g.s.duplicate(true);malformed.settings.lab_speed=INF
	check(g.validate_state(malformed)!="","nonfinite nested settings are rejected")
	check(not Store.decode(encoded.replace('"sha256": "','"sha256": "x')).ok,"corruption is rejected")
	g.zone_cells(["0,-1"],"production")
	check(g.create_tender(["0,-1"],["industry"])=="","development tender command")
	var offers=g.s.draft.offers.duplicate();var unique={}
	for id in offers:unique[id]=true
	check(offers.size()==3 and unique.size()==3,"three nonduplicate legal offers")
	var original_rng=g.s.rng.duplicate()
	check(g.create_tender(["1,-1"],["agriculture"])!="" and g.s.draft.offers==offers and g.s.rng==original_rng,"reopen or change site cannot redraw free")
	var saved_game=Game.new();saved_game.s=Store.decode(Store.encode(g.s)).state
	check(saved_game.s.draft.offers==offers and int(saved_game.s.rng.draft)==int(g.s.rng.draft),"pending offers and RNG survive save/reload")
	g.s.currency=1000;g.s.dt=50
	saved_game.s.currency=1000;saved_game.s.dt=50
	saved_game.reroll()
	var expected_rerolled=saved_game.s.draft.offers.duplicate()
	check(g.reroll()=="" and close(g.s.currency,982) and close(g.s.dt,50),"first reroll costs currency only")
	check(g.s.draft.offers==expected_rerolled,"same next command after disk-format restore reproduces actual draw")
	check(g.reroll()=="" and close(g.s.currency,950) and close(g.s.dt,50),"second reroll costs increase")
	check(g.reroll()=="" and close(g.s.currency,895) and close(g.s.dt,49),"third reroll adds DT")
	var more_fresh=false
	for id in g.s.draft.offers:
		if not id in offers:more_fresh=true
	var updated_unique={}
	for offer in g.s.draft.offers:updated_unique[offer]=true
	check(updated_unique.size()==3,"draft uniqueness invariant after rerolls")
	var stale_cash=g.s.currency
	check(g.accept_offer("not-an-offer")!="" and close(g.s.currency,stale_cash),"invalid acceptance does not charge")
	var candidate=g.s.draft.offers[0]
	check(g.accept_offer(candidate)=="","commit starts a real project")
	var id=first_id(g,candidate)
	# If an origin has the same def, find the actually building project.
	for p in g.s.projects.values():
		if p.status=="construction":id=p.id
	check(g.s.projects[id].status=="construction" and not g.s.projects[id].job.is_empty(),"accepted proposal is not instantly built")
	var material_before=Inv.total(g.s.stocks,"material");var world_before=g.s.global_now;var time_before=g.s.local_hours;var dt_before=g.s.dt
	var pulse=g.pulse(.5)
	check(pulse.advanced>.0 and g.s.dt<dt_before and g.s.local_hours>time_before and close(g.s.global_now,world_before),"DT advances local, never global time")
	check(g.s.projects[id].spend_material>0 and Inv.total(g.s.stocks,"material")>=0,"construction actually draws materials")
	g.s.projects[id].opportunity_checked=true;advance(g,4)
	check(g.s.projects[id].status=="running","construction finishes through common simulator")
	var g1=fixture(["hamlet","farm","timber"]);var g2=fixture(["hamlet","farm","timber"])
	advance(g1,3)
	for i in 36:advance(g2,1.0/12.0)
	check(close(g1.s.currency,g2.s.currency) and close(Inv.total(g1.s.stocks,"material"),Inv.total(g2.s.stocks,"material")),"large advance agrees with fixed-step advances")
	var paused=fixture(["hamlet","farm","school"])
	var tl=paused.temporal_load();paused.set_paused(first_id(paused,"school"),true)
	check(close(tl,paused.temporal_load()),"pausing cannot erase structural temporal load")
	var off=fresh();off.s.dt=5;var before=off.s.duplicate(true)
	var recovered=off.resume_time(float(off.s.wall_mark)+10*3600)
	check(close(recovered,5) and close(off.s.local_hours,before.local_hours) and off.s.stocks==before.stocks,"offline stores DT without city catch-up")
	off.resume_time(float(off.s.wall_mark)+365*24*3600)
	check(close(off.s.dt,off.c.balance.dt_cap),"long absence cannot exceed DT cap")
	var mark=off.s.wall_mark;off.resume_time(mark-100000)
	check(close(off.s.wall_mark,mark),"wall-clock rollback grants no recovery")
	var gov=fresh();gov.contract_action("school","accept");var oldsupport=gov.s.support
	gov.update_global(gov.s.global_now+25*3600)
	check(gov.s.contracts.school.status=="failed" and close(gov.s.support,oldsupport-12),"accepted global deadline applies one penalty")
	gov.update_global(gov.s.global_now+2*3600)
	check(close(gov.s.support,oldsupport-12),"missed promise does not punish twice")
	var no=fresh();var support_before=no.s.support;no.contract_action("school","decline");no.update_global(no.s.global_now+30*3600)
	check(close(no.s.support,support_before),"unaccepted/declined request is not broken promise")
	var crisis=fresh();crisis.s.mandate=11;crisis.Progress.check_crisis(crisis)
	check(crisis.s.crisis.get("active",false) and crisis.s.phase=="play","one very low legitimacy starts a buffer crisis")
	var crisis_roll=crisis.s.crisis.risk_roll
	check(Store.decode(Store.encode(crisis.s)).state.crisis.risk_roll==crisis_roll,"crisis risk result is stored before commitment")
	check(crisis.crisis_action("repair")=="" and not crisis.s.crisis.active and crisis.s.phase=="play","costly repair can save tenure")
	crisis.s.mandate=1;crisis.s.support=1;crisis.s.local_hours+=4;crisis.Progress.check_crisis(crisis);crisis.crisis_action("leave")
	check(crisis.s.phase=="failed" and not crisis.s.projects.is_empty(),"exit freezes governance, preserves city")
	check(crisis.create_tender(["1,0"],["industry"])!="","failed tenure cannot build")
	var grow=fixture(["hamlet","school","timber","farm","power","clinic"])
	grow.s.events=[];advance(grow,12)
	var school=first_id(grow,"school");var timber=first_id(grow,"timber")
	check(grow.milestone_status(school).ready,"effective runtime + context unlocks milestone")
	check(not grow.s.relations.is_empty(),"compatible neighbors gain relationship without source graph")
	var matured=""
	for rel in grow.s.relations.values():
		if rel.progress>=rel.required:matured=rel.id
	check(matured!="","effective co-running matures at least one relationship")
	if matured=="":
		print("FATAL no mature relation in fixture",grow.s.relations)
		return 2
	var relation=grow.s.relations[matured];var progress_before=relation.progress
	grow.set_paused(relation.a,true);advance(grow,1)
	check(close(relation.progress,progress_before),"paused partner does not gain relationship")
	grow.set_paused(relation.a,false)
	check(grow.create_fusion(matured)=="","mature relationship opens a fusion choice")
	var fusion_id=grow.s.draft.offers[0]
	check(grow.Draft.withdraw(grow)=="" and grow.s.draft.is_empty() and grow.s.relations[matured].choice=="","unfunded fusion can be deferred without blocking new tenders")
	grow.create_fusion(matured)
	check(grow.accept_offer(fusion_id)=="" and grow.s.totals.fusions==1,"fusion commits persistent relationship rule")
	check(grow.create_fusion(matured)!="","same relationship cannot claim twice")
	check(grow.create_upgrade(timber,"education")=="","milestone opens weighted upgrade draft")
	grow.s.draft.offers=["u_quality"]
	var stock_signature=Inv.signature("material",["light","basic"])
	var old_legacy=float(grow.s.stocks.get(stock_signature,0))
	check(grow.accept_offer("u_quality")=="","upgrade commits construction, not instant perks")
	advance(grow,3)
	check(grow.s.projects[timber].level==1 and "u_quality" in grow.s.projects[timber].upgrades,"upgrade construction installs chosen mechanism")
	var advanced=Inv.total(grow.s.stocks,"material",["advanced"])
	check(advanced>0 and grow.s.stocks.has(stock_signature),"new output gains tag; legacy stock retains old signature")
	check(grow.s.complete and grow.s.phase=="play","checklist awards badge without ending city")
	var completion_count=0
	for e in grow.s.log:
		if e.kind=="first_block_complete":completion_count+=1
	advance(grow,2)
	var after_count=0
	for e in grow.s.log:
		if e.kind=="first_block_complete":after_count+=1
	check(completion_count==1 and after_count==1,"infinite mode does not duplicate completion rewards")
	grow.s.knowledge=10;grow.s.currency=500
	check(grow.research("precision")=="","technology enters a real construction/resource process")
	advance(grow,6)
	check("precision" in grow.s.tag_rewards and not "precision" in grow.s.policy_tags,"researched policy waits for explicit inclusion")
	check(grow.add_policy_tag("precision")=="" and "precision" in grow.s.policy_tags,"player can add discovered policy tag")
	check(grow.add_policy_tag("precision")!="","policy reward is not repeatable")
	# Failing input does not create output; energy zero also stops powered production.
	var starve=fixture(["works"]);starve.s.stocks={};starve.c.balance.base_food=0;starve.c.balance.base_material=0;advance(starve,1)
	check(close(Inv.total(starve.s.stocks,"goods"),0) and close(starve.s.projects.values()[0].usage,0),"incompatible or absent inputs cannot create output")
	var exhausted=fixture(["timber"]);exhausted.c.balance.base_energy=0;exhausted.c.balance.base_material=0;exhausted.s.stocks={};advance(exhausted,1)
	check(close(Inv.total(exhausted.s.stocks,"material"),0),"zero energy stops powered extraction")
	var savepath="user://test_roundtrip.json"
	check(Store.write(grow.s,savepath)=="" and Store.read(savepath).ok,"real disk atomic snapshot roundtrip")
	Store.write(g1.s,savepath)
	var file=FileAccess.open(savepath,FileAccess.WRITE);file.store_string("CORRUPT");file.close()
	var restored=Store.read(savepath)
	check(restored.ok and restored.from_backup,"corrupt main snapshot falls back to last valid backup")
	# Long-run guardrail: no negative stocks / NaN in a variety of openings.
	var valid=true
	for seed in range(1,31):
		var sim=fresh(seed)
		for i in 8:advance(sim,3)
		if sim.validate_state(sim.s)!="":valid=false
	check(valid,"30 opening seeds survive 24 local hours without corrupt/negative stock")
	var fail=0
	for item in checks:
		if not item.pass:fail+=1
	var report={"engine":Engine.get_version_info(),"checks":checks,"passed":checks.size()-fail,"failed":fail,"test_scope":"headless Godot release runtime; fixtures are not playtest evidence"}
	var out=FileAccess.open(Paths.file("rules.json"),FileAccess.WRITE)
	if out!=null:out.store_string(JSON.stringify(report,"\t"));out.close()
	print("RESULT %d/%d passed"%[checks.size()-fail,checks.size()])
	return 0 if fail==0 else 1
