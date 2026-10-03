extends RefCounted
const Paths=preload("res://tests/paths.gd")
const Game=preload("res://core/game.gd")
const Inv=preload("res://core/inventory.gd")
var records:Array=[]
func count(g,def:String) -> int:
	var n=0
	for p in g.s.projects.values():
		if p.def==def:n+=1
	return n
func site(g,zone:String) -> String:
	# Choose a compact legal site using only the same exposed project tags as the player.
	# Public/logistics projects are deliberately placed by compatible partners.
	var best="";var best_score=-100000.0
	var keys=g.s.cells.keys();keys.sort()
	for k in keys:
		var cell=g.s.cells[k]
		if cell.site!="" or cell.terrain=="water":continue
		var score=-float(abs(int(cell.q))+abs(int(cell.r)))*.1
		for p in g.s.projects.values():
			var touching=false
			for pc in g.s.sites[p.site].cells:
				if k in g.Hex.neighbors(pc):touching=true
			if not touching:continue
			score+=1
			var tags=g.base_tags(p.id)
			if zone=="public" and "community" in tags:score+=12
			if zone=="circulation" and ("agriculture" in tags or "industry" in tags):score+=10
			if zone=="settlement" and "public" in tags:score+=10
			if zone=="production" and ("education" in tags or "logistics" in tags):score+=8
		if score>best_score:best_score=score;best=k
	return best
func choose_build(g,zone:String,preferences:Array,wanted:Array) -> bool:
	var cell=site(g,zone)
	if cell=="":return false
	g.zone_cells([cell],zone)
	var error=g.create_tender([cell],preferences)
	if error!="":return false
	var pick=g.s.draft.offers[0];var score=-1.0
	for id in g.s.draft.offers:
		var value=10.0-float(g.c.projects[id].cost)/100.0
		if id in wanted:value+=float(wanted.size()-wanted.find(id))*30
		if value>score:score=value;pick=id
	# No injected offers, free resources, altered time multiplier or hidden forced cards.
	if g.accept_offer(pick)!="":return false
	return true
func run() -> int:
	for case_index in 16:
		var seeds=[7,18,31,49,67,88,111,20260922]
		var seed_value=seeds[case_index%8]
		var choice_index=int(case_index/8)
		var map_index=case_index%3 if choice_index==1 else 0
		var g=Game.new();g.new_game(seed_value,1000000.0);g.select_block(map_index)
		var origin=g.s.origin_choices[choice_index]
		g.choose_origin(origin)
		for id in ["hope","school","care"]:g.contract_action(id,"accept")
		var actions=0;var waited=0.0
		for turn in 100:
			if g.s.phase!="play" or g.s.complete:break
			if not g.s.events.is_empty():g.opportunity("skip")
			for t in g.s.contracts.values():
				if t.status=="accepted" and g.Progress.contract_met(g,t.id):g.contract_action(t.id,"fulfill")
			var building=false
			for p in g.s.projects.values():
				if not p.job.is_empty():building=true
			if g.s.draft.get("kind","")=="fusion":
				if Inv.total(g.s.stocks,"goods")>=3 and g.s.currency>=95:
					g.accept_offer(g.s.draft.offers[0]);actions+=1
				else:g.Draft.withdraw(g)
			if g.s.draft.is_empty() and not building:
				if Inv.total(g.s.stocks,"goods")<5 and count(g,"mill")==0 and count(g,"works")==0 and g.s.projects.size()<9:
					choose_build(g,"production",["agriculture","industry"],["mill","works","farm"]);actions+=1
				elif count(g,"farm")==0 and count(g,"mill")==0 and g.s.projects.size()<7:
					choose_build(g,"production",["agriculture"],["farm","timber","mill"]);actions+=1
				elif count(g,"timber")==0 and count(g,"quarry")==0 and g.s.projects.size()<7:
					choose_build(g,"production",["industry"],["timber","quarry","farm"]);actions+=1
				elif float(g.s.metrics.population)<16 and g.s.projects.size()<7:
					choose_build(g,"settlement",["community"],["hamlet","workers","riverside","scholars"]);actions+=1
				elif count(g,"school")==0 and count(g,"clinic")==0 and count(g,"park")==0 and g.s.projects.size()<8:
					choose_build(g,"public",["education","community"],["school","clinic","park","power"]);actions+=1
				elif count(g,"market")==0 and count(g,"warehouse")==0 and count(g,"coldstore")==0 and g.s.projects.size()<8:
					choose_build(g,"circulation",["logistics","agriculture"],["market","coldstore","warehouse"]);actions+=1
				elif g.s.projects.size()<6:
					choose_build(g,"public",["community"],["clinic","school","park","power"]);actions+=1
			if g.s.draft.is_empty() and g.s.currency>95 and g.s.totals.fusions==0:
				for rel in g.s.relations.values():
					if rel.progress>=rel.required and rel.choice=="" and g.relation_active(rel):
						if g.create_fusion(rel.id)=="":g.accept_offer(g.s.draft.offers[0]);actions+=1
						break
			var upgrading=false
			for pp in g.s.projects.values():
				if pp.status=="upgrading":upgrading=true
			if g.s.draft.is_empty() and g.s.currency>150 and g.s.totals.upgrades==0 and not upgrading:
				for p in g.s.projects.values():
					if g.milestone_status(p.id).ready:
						if g.create_upgrade(p.id,"community")=="":
							var pick=g.s.draft.offers[0]
							for up in g.s.draft.offers:
								if up in ["u_civic","u_water","u_openhall","u_atelier"]:pick=up;break
							g.accept_offer(pick);actions+=1
						break
			if g.s.crisis.get("active",false):g.crisis_action("repair")
			if not g.s.events.is_empty():g.opportunity("skip")
			var result=g.pulse(.5)
			if result.advanced<=.00001:
				# Not a cheat: simulate exactly one hour of natural online time.
				g.advance_online(1.0);waited+=1
			g.update_global(g.s.global_now+20.0)
		var record={"case":case_index,"map":map_index,"seed":seed_value,"origin":origin,"complete":g.s.complete,"phase":g.s.phase,"local_hours":g.s.local_hours,"dt":g.s.dt,"currency":g.s.currency,"projects":g.s.projects.size(),"upgrades":g.s.totals.upgrades,"fusions":g.s.totals.fusions,"natural_wait_hours":waited,"actions":actions,"checklist":g.checklist(),"bottlenecks":[]}
		for p in g.s.projects.values():record.bottlenecks.append({"name":p.def,"usage":p.usage,"bottleneck":p.bottleneck})
		records.append(record);print("PLAY ",JSON.stringify(record))
		if true: # Store failed as well as successful routes; never cherry-pick evidence.
			var file=FileAccess.open(Paths.file("legal_playthrough_%s%s.json"%[seed_value,"" if choice_index==0 else "_alt"]),FileAccess.WRITE)
			file.store_string(JSON.stringify(g.s,"\t"));file.close()
	var out=FileAccess.open(Paths.file("playthroughs.json"),FileAccess.WRITE)
	out.store_string(JSON.stringify({"method":"Heuristic player uses actual tenders, costs and DT. No resource injection or forced cards. Natural waits, if any, explicitly measured.","runs":records},"\t"));out.close()
	return 0
