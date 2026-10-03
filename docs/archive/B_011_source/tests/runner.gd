extends RefCounted
const Paths=preload("res://tests/paths.gd")
const Game=preload("res://core/game.gd")
const Inv=preload("res://core/inventory.gd")
const Store=preload("res://core/storage.gd")
const Hex=preload("res://core/hex.gd")
var checks=[]
func check(ok:bool,name:String):
	checks.append({"name":name,"pass":ok});print(("PASS " if ok else "FAIL ")+name)
func close(a:float,b:float,t:float=.001) -> bool:return absf(a-b)<t
func fresh(seed_value:int=7):
	var g=Game.new();g.new_game(seed_value,1000000);g.select_block(0);g.choose_origin(g.s.origin_choices[0]);return g
func fixture(defs:Array,cells:Array=[],pop:float=18):
	var g=fresh();g.s.projects={};g.s.sites={};g.s.relations={};g.s.events=[];g.s.stage=1;g.s.stage_started=-5
	for k in g.s.cells:g.s.cells[k].site="";g.s.cells[k].zone=""
	var locations=["0,1","0,0","1,1","1,0","2,0","2,-1","0,2","1,-1","3,-1"] if cells.is_empty() else cells
	for i in defs.size():g._create_starter(defs[i],[locations[i]])
	var allocated=false
	for p in g.s.projects.values():
		if g.c.projects[p.def].population>0 and not allocated:p.residents=pop;allocated=true
	g.s.migration_paused=true;g.s.debug_used=true;g.s.currency=1000;g.s.dt=90
	g.c.balance.base_food=20;g.c.balance.base_material=10;g.c.balance.base_energy=50
	g.refresh_metrics();return g
func advance(g,h:float):
	var left=h
	while left>.000001 and g.s.phase=="play":
		while not g.s.events.is_empty():g.opportunity("skip")
		var r=g.Sim.advance(g,minf(left,.5),false,false)
		if r.advanced<=0:break
		left-=r.advanced
	while not g.s.events.is_empty():g.opportunity("skip")
func first_id(g,id:String) -> String:
	for p in g.s.projects.values():
		if p.def==id:return p.id
	return ""
func run() -> int:
	var g=fresh();var twin=fresh()
	check(g.s.schema==2 and g.s.build=="0.1.1-b-population","B population save lineage identified")
	check(g.c.projects.size()==34 and g.c.upgrades.size()==12,"34 projects / 12 upgrades, old B IDs preserved")
	check(g.s.cells.size()==61 and Hex.connected(["0,-1","1,-1"]),"B hex map and connected footprint retained")
	check(not Hex.connected(["0,-1","3,-1"]),"disconnected site rejected")
	check(JSON.stringify(g.s)==JSON.stringify(twin.s),"deterministic opening")
	check(close(g.s.metrics.population,18),"actual initial population is 18, not capacity")
	check(close(g.s.metrics.housing,24),"housing capacity separately totals 24")
	check(close(Inv.total(g.s.stocks,"goods"),0) and not "goods" in g.s.revealed_resources,"no initial manufactured goods, not a hidden usable stock")
	check(not "industry" in g.s.policy_tags and not "education" in g.s.policy_tags,"later-phase policies unavailable at start")
	var stock={};Inv.add(stock,"material",["heavy"],7,100);Inv.add(stock,"material",["light"],9,100)
	check(stock.size()==1 and close(Inv.total(stock,"material",["advanced"]),16),"resource interfaces disabled; material fungible")
	check(close(Inv.take(stock,"material",[],20).taken,16),"withdraw cannot create missing material")
	Inv.add(stock,"food",[],9,100)
	var ratio=Inv.consume_recipe(stock,[{"resource":"food","rate":6,"tags":[]},{"resource":"food","rate":6,"tags":[]}],1)
	check(close(ratio,.75,.001) and Inv.total(stock,"food")<.001,"atomic overlapping recipes remain conserved")
	var save=Store.decode(Store.encode(g.s))
	check(save.ok and g.validate_state(save.state)=="","schema2 populated state roundtrip")
	var bad=g.s.duplicate(true);bad.schema=1
	check(g.validate_state(bad)!="","old B schema rejected explicitly, never silently overwritten")
	bad=g.s.duplicate(true);bad.projects[bad.projects.keys()[0]].residents=-3
	check(g.validate_state(bad)!="","negative residents rejected")
	check(not Store.decode("not json").ok,"corrupt save rejected")
	check(g.s.cells["0,-1"].zone=="" and g.create_tender(["0,-1"],["agriculture"])=="","tender directly on unzoned land")
	var ids=g.s.draft.offers.duplicate();var count=0
	for id in ids:
		if "agriculture" in g.c.projects[id].families:count+=1
	check(count>=2,"agriculture intent guarantees at least two agricultural choices")
	check(ids.size()==3 and ids[0]!=ids[1] and ids[1]!=ids[2] and ids[0]!=ids[2],"three distinct proposals")
	var cursor=g.s.rng.duplicate()
	check(g.create_tender(["1,-1"],["housing"])!="" and g.s.rng==cursor,"unresolved offer cannot be rerolled by changing site")
	var price=g.Draft.reroll_cost(g);var cash=float(g.s.currency)
	check(g.reroll()=="" and close(g.s.currency,cash-price.currency),"paid redraw charges exactly once")
	g.reroll();check(g.Draft.reroll_cost(g).dt>0,"later rerolls cost DT")
	var offer=g.s.draft.offers[0];check(g.accept_offer(offer)=="","proposal begins actual construction")
	var building=""
	for p in g.s.projects.values():
		if p.status=="construction":building=p.id
	check(building!="" and g.s.projects[building].job.goods==0,"early construction never requires manufactured goods")
	var before=float(g.s.local_hours);var materials=Inv.total(g.s.stocks,"material")
	var pulse=g.pulse(.5)
	check(pulse.advanced>0 and g.s.local_hours>before and pulse.spent>0,"DT still advances entire B simulation and charges")
	check(Inv.total(g.s.stocks,"material")<materials+1,"DT doesn't invent construction material")
	var snapshot=g.s.duplicate(true);var offline=g.resume_time(g.s.wall_mark+3600*6)
	check(offline>0 and g.s.local_hours==snapshot.local_hours,"offline stores DT without city catch-up")
	check(g.s.projects[building].residents==snapshot.projects[building].residents,"offline does not grow residents")
	check(g.s.stocks==snapshot.stocks,"offline does not create resources")
	check(g.resume_time(g.s.wall_mark)==0,"same offline interval not claimable twice")
	var all_good=true
	for seed_value in range(1,31):
		var trial=fresh(seed_value);trial.create_tender(["0,-1"],["agriculture"])
		var farm_count=0
		for id in trial.s.draft.offers:
			if "agriculture" in trial.c.projects[id].families:farm_count+=1
			if trial.c.projects[id].stage>0:all_good=false
		if farm_count<2:all_good=false
	check(all_good,"30 opening seeds: agricultural intent and phase eligibility respected")
	g=fixture(["hamlet","farm","timber"]);var home=first_id(g,"hamlet")
	var cap=g.s.metrics.housing;advance(g,1)
	check(g.s.metrics.population<cap and close(g.s.metrics.population,18),"pausing migration keeps actual residents below capacity")
	var basefood=Inv.total(g.s.stocks,"food");g.s.projects[home].residents=0;g.refresh_metrics();advance(g,.25)
	check(g.s.metrics.income==0,"empty houses generate no resident tax")
	g=fixture(["hamlet","farm","timber","works","school"],[],8);advance(g,1)
	check(g.s.metrics.staff_ratio<.5 and g.s.projects[first_id(g,"timber")].usage<.5,"labor shortage reduces actual output")
	var q=first_id(g,"works");var unskilled=float(g.s.projects[q].skill_ratio)
	g.s.projects[first_id(g,"hamlet")].education=70;g.refresh_metrics()
	check(g.s.projects[q].skill_ratio>unskilled,"resident education improves skilled-job qualification")
	var noisy=fixture(["hamlet","quarry"],["1,0","2,0"]);advance(noisy,4)
	var quiet=fixture(["hamlet","quarry"],["-1,2","3,-1"]);advance(quiet,4)
	var nh=noisy.s.projects[first_id(noisy,"hamlet")];var qh=quiet.s.projects[first_id(quiet,"hamlet")]
	check(nh.health<qh.health-2 and nh.happiness<qh.happiness-2,"quarry near housing lowers health and happiness vs distant quarry")
	check(nh.impact_sources.size()>0 and noisy.s.metrics.exposure_links.size()>0,"pollution source-to-home explanation is retained")
	noisy.set_paused(first_id(noisy,"quarry"),true);advance(noisy,1)
	check(noisy.s.projects[first_id(noisy,"hamlet")].living.pollution<.001,"paused industry stops emissions")
	var bare=fixture(["hamlet","farm"]);advance(bare,5)
	var serviced=fixture(["hamlet","farm","park","clinic","market","school"]);advance(serviced,5)
	var bh=bare.s.projects[first_id(bare,"hamlet")];var sh=serviced.s.projects[first_id(serviced,"hamlet")]
	check(sh.health>bh.health+3,"clinic measurably improves residents' health")
	check(sh.happiness>bh.happiness+4,"park and market measurably improve happiness")
	check(sh.education>bh.education+3 and serviced.s.knowledge>0,"education stock and knowledge flow both respond to teaching")
	check(serviced.s.metrics.service_served.market>0 and serviced.s.metrics.income>bare.s.metrics.income,"real commercial customers produce tax revenue")
	var loc=fixture(["hamlet","clinic"],["-1,2","3,-1"]);advance(loc,1)
	check(loc.s.metrics.service_served.care==0,"clinic outside service radius has no coverage")
	loc.s.stable_hours=3
	check(not loc.Progress.contract_met(loc,"care"),"unserved distant medical capacity cannot fulfill a residents promise")
	g=fixture(["hamlet","clinic","clinic","clinic"]);advance(g,1)
	check(g.s.metrics.service_served.care<=g.s.metrics.population+.001,"duplicate providers cannot double count served residents")
	g=fixture(["hamlet","farm","timber"]);g.s.migration_paused=false;advance(g,4)
	check(g.s.metrics.population>18 and g.s.metrics.population<=g.s.metrics.housing,"available houses and jobs permit bounded migration")
	g=fresh();g.s.migration_paused=true;advance(g,1)
	check(g.s.stage==0 and Inv.total(g.s.stocks,"goods")==0,"one survival hour does not unlock goods or stages by timer alone")
	g=fixture(["hamlet","farm","school"]);g.s.projects[first_id(g,"hamlet")].residents=32;g.s.projects[first_id(g,"hamlet")].education=40;g.s.stable_hours=2;g.refresh_metrics()
	check(g.Population.can_town(g) and g.stage_up()=="","town stage requires residents and education, then explicit command")
	check(g.s.stage==2 and "goods" in g.s.revealed_resources and "industry" in g.s.policy_tags,"only town transition reveals goods and industry")
	g=fixture(["hamlet","farm","park"]);advance(g,10)
	var rid=""
	for rel in g.s.relations.values():
		if rel.progress>=rel.required:rid=rel.id;break
	check(rid!="","effective neighbors still mature Fusion relationship")
	if rid!="":
		check(g.create_fusion(rid)=="","Fusion proposal available")
		var c=float(g.Draft.offer_cost(g,g.s.draft.offers[0]).material)
		check(c==8 and g.Draft.offer_cost(g,g.s.draft.offers[0]).goods==0,"early Fusion uses materials, not locked goods")
		check(g.accept_offer(g.s.draft.offers[0])=="" and g.s.totals.fusions==1,"Fusion commits and retains two independent projects")
	g=fixture(["hamlet","farm"]);advance(g,5);home=first_id(g,"hamlet")
	check(g.milestone_status(home).ready,"milestone growth retained")
	check(g.create_upgrade(home,"community")=="","upgrade tender retained with preference")
	check(g.accept_offer(g.s.draft.offers[0])=="","upgrade begins work without locked resources")
	advance(g,4);check(g.s.totals.upgrades==1,"upgrade completes through normal simulation")
	g=fixture(["hamlet","farm"]);var n=float(g.s.metrics.population);g.demolish(first_id(g,"hamlet"))
	check(close(g.s.displaced,n) and close(g.s.metrics.population,n),"demolishing housing displaces residents instead of deleting them")
	g=fixture(["hamlet","farm"]);g.s.mandate=9;g.s.support=50;g.Progress.check_crisis(g)
	check(g.s.crisis.get("active",false),"double legitimacy retains hard governance crisis")
	g.crisis_action("leave");check(g.s.phase=="failed" and g.s.projects.size()==2,"loss freezes authority, preserves city")
	var failed=0
	for item in checks:
		if not item.pass:failed+=1
	var f=FileAccess.open(Paths.file("population-rules.json"),FileAccess.WRITE)
	f.store_string(JSON.stringify({"build":"0.1.1-b-population","checks":checks,"passed":checks.size()-failed,"failed":failed,"note":"Unit fixtures deliberately modify state; these are not normal gameplay completion claims."},"\t"));f.close()
	print("POPULATION RULES %d/%d"%[checks.size()-failed,checks.size()])
	return 0 if failed==0 else 1
