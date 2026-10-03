extends RefCounted
## Integration paths use only public player actions and paid DT. No grants/forced cards.
const Game=preload("res://core/game.gd")
const Inv=preload("res://core/inventory.gd")
const Paths=preload("res://tests/paths.gd")
var trace=[]
var error=""
func pulse(g) -> bool:
	while not g.s.events.is_empty():
		error=g.opportunity("skip")
		if error!="":return false
	var r=g.pulse(.5)
	trace.append({"action":"pulse","hours":r.advanced,"dt":r.spent,"cash":g.s.currency,"people":g.s.metrics.get("population",0)})
	if r.advanced<.001:error="DT exhausted: "+r.reason;return false
	if g.s.phase!="play":error=g.s.failed_reason;return false
	return true
func choose_site(g,wanted:String) -> Array:
	if g.s.cells.has(wanted) and g.s.cells[wanted].site=="" and g.s.cells[wanted].terrain!="water":return [wanted]
	for id in g.s.cells:
		if g.s.cells[id].site=="" and g.s.cells[id].terrain!="water":return [id]
	return []
func construct(g,tag:String,where:String,preferred:Array) -> bool:
	var cells=choose_site(g,where)
	error=g.create_tender(cells,[tag])
	if error!="":return false
	var picked="";var rank=1000
	for id in g.s.draft.offers:
		var def=g.c.projects[id]
		if not tag in def.families:continue
		var r=preferred.find(id)
		if r<0:r=100
		if r<rank:rank=r;picked=id
	if picked=="":error="No requested family: "+tag;return false
	trace.append({"action":"choose","tag":tag,"id":picked,"cells":cells,"offers":g.s.draft.offers.duplicate()})
	for i in 120:
		var cost=g.Draft.offer_cost(g,picked)
		if g.s.currency>=cost.currency:break
		if not pulse(g):return false
	error=g.accept_offer(picked)
	if error!="":return false
	for i in 160:
		var constructing=false
		for p in g.s.projects.values():
			if not p.job.is_empty():constructing=true
		if not constructing:return true
		if not pulse(g):return false
	error="Construction stalled";return false
func has_family(g,family:String) -> bool:
	for p in g.s.projects.values():
		if family in g.c.projects[p.def].families:return true
	return false
func step(g,ok:bool,context:String) -> bool:
	if not ok:error=context+": "+error;return false
	return true
func play(seed_value:int) -> Dictionary:
	trace=[];error=""
	var g=Game.new();g.new_game(seed_value,1000000);g.select_block(0);g.choose_origin(g.s.origin_choices[0])
	var success=true
	if not has_family(g,"materials"):success=step(g,construct(g,"materials","1,0",["timber","bamboo","stoneworks","quarry"]),"materials")
	if success and not has_family(g,"agriculture"):success=step(g,construct(g,"agriculture","0,-1",["farm","orchard","pasture","vegetables"]),"food")
	if success:success=step(g,construct(g,"energy","2,0",["wind","power","solar"]),"energy")
	if success:success=step(g,construct(g,"housing","0,2",["hamlet","cabins","riverside"]),"housing")
	for i in 100:
		if not success or g.s.stage>=1:break
		success=pulse(g)
	if success:success=step(g,construct(g,"commerce","1,1",["market","stall","tavern","dock"]),"commerce")
	if success:success=step(g,construct(g,"leisure","-1,2",["park","square","tavern"]),"leisure")
	if success:success=step(g,construct(g,"health","1,2",["clinic","medic"]),"health")
	if success:success=step(g,construct(g,"education","-1,1",["school","night_school"]),"education")
	# Growth through actual operating time, with current city needs, not hand-set XP.
	if success:
		for i in 80:
			var target=""
			for p in g.s.projects.values():
				if g.c.projects[p.def].population>0 and g.milestone_status(p.id).ready:target=p.id;break
			if target!="":
				error=g.create_upgrade(target,"community");success=error==""
				if success:
					var pick=g.s.draft.offers[0]
					for id in g.s.draft.offers:
						if id in ["u_civic","u_openhall"]:pick=id;break
					for j in 100:
						if g.s.currency>=g.Draft.offer_cost(g,pick).currency:break
						if not pulse(g):success=false;break
					if success:error=g.accept_offer(pick);success=error==""
				break
			if not pulse(g):success=false;break
	if success:
		for i in 80:
			var rid=""
			for rel in g.s.relations.values():
				if rel.choice=="" and rel.progress>=rel.required and g.relation_active(rel):rid=rel.id;break
			if rid!="":
				error=g.create_fusion(rid);success=error==""
				if success:
					var pick=g.s.draft.offers[0]
					if "civic_care" in g.s.draft.offers:pick="civic_care"
					for j in 100:
						if g.s.currency>=75 and Inv.total(g.s.stocks,"material")>=8:break
						if not pulse(g):success=false;break
					if success:error=g.accept_offer(pick);success=error==""
				break
			if not pulse(g):success=false;break
	if success:
		for i in 100:
			if g.Population.can_town(g):g.stage_up();break
			if not pulse(g):success=false;break
	if success:success=step(g,construct(g,"industry","2,1",["mill","works","recycler"]),"first industry")
	if success:
		for i in 40:
			if g.s.complete and Inv.total(g.s.stocks,"goods")>.2:break
			if not pulse(g):success=false;break
	var valid=g.validate_state(g.s)
	success=success and g.s.complete and g.s.stage==2 and not g.s.debug_used and valid=="" and Inv.total(g.s.stocks,"goods")>.2
	var report={"seed":seed_value,"success":success,"error":error,"validation":valid,"origin":g.s.origin,"stage":g.s.stage,"complete":g.s.complete,"hours":g.s.local_hours,"dt":g.s.dt,"currency":g.s.currency,"metrics":g.s.metrics,"debug":g.s.debug_used,"trace":trace}
	FileAccess.open(Paths.file("population-play-"+str(seed_value)+".json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	FileAccess.open(Paths.file("population-state-"+str(seed_value)+".json"),FileAccess.WRITE).store_string(JSON.stringify(g.s))
	print("B_ROUTE ",seed_value," ",success," ",error," h=",g.s.local_hours," dt=",g.s.dt," pop=",g.s.metrics.population," cash=",g.s.currency," stage=",g.s.stage)
	return {"seed":seed_value,"pass":success,"error":error,"hours":g.s.local_hours,"dt":g.s.dt,"population":g.s.metrics.population}
func run() -> int:
	var results=[]
	for seed_value in [7,18,31,49,67,88]:results.append(play(seed_value))
	var passed=0
	for r in results:
		if r.pass:passed+=1
	FileAccess.open(Paths.file("population-playthroughs.json"),FileAccess.WRITE).store_string(JSON.stringify({"results":results,"passed":passed,"total":results.size(),"method":"Public player commands, offered cards, no grants, no task rewards, no clock injection. Every time step paid with initial DT. Script chooses locations/types with prior knowledge; not human playability proof."},"\t"))
	print("B ROUTES ",passed,"/",results.size());return 0 if passed==results.size() else 1
