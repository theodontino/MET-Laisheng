extends RefCounted
## Public-command scripted play. No fixture grants, forced cards, free time or save edits.
const Game=preload("res://core/game.gd")
const Paths=preload("res://tests/paths.gd")
const Store=preload("res://core/storage.gd")
var g
var trace=[]
var failure=""
const PREF={"hamlet":["O03","R18","R10","O02","M01"],"farm":["R15","P07","R14","M01","L05"],"timber":["L05","M01","L02","P06"],"power":["L05","M01","L02"],"works":["P01","P05","B09","M01","L03","O06"],"market":["R08","L01","O07","R10"],"school":["O04","P03","L04","M01"],"clinic":["P02","R05","O01","M01"],"park":["P04","L04","M01"],"warehouse":["L05","P06","M01","L02"]}
func has(base:String)->bool:
	for id in g.playable_ids():
		if g.s.projects[id].def==base:return true
	return false
func pulse() -> bool:
	var r=g.pulse(.5)
	trace.append({"action":"DT","hours":r.advanced,"dt":r.spent,"money":g.s.currency,"people":g.s.metrics.population})
	if r.advanced<.0001:failure="DT不足/推进失败："+r.reason;return false
	if g.validate_state(g.s)!="":failure=g.validate_state(g.s);return false
	return true
func choose(preference:Array) -> Dictionary:
	var best=g.s.draft.offers[0];var best_rank=999
	for offer in g.s.draft.offers:
		var rank=preference.find(offer.bonus_id)
		if rank<0:rank=100
		if rank<best_rank:best_rank=rank;best=offer
	return best
func settle(offer:Dictionary) -> bool:
	for i in 100:
		if g.s.currency>=g.Draft.offer_cost(g,offer).currency:break
		if not pulse():return false
	var e=g.accept_offer(offer.id)
	if e!="":failure=e;return false
	trace.append({"action":"accept","offer":offer.duplicate(true)})
	return true
func build(base:String,where:String) -> bool:
	var tile=where
	if g.s.cells[tile].building!="":
		for k in g.Rules.ids(g.s.cells):
			if g.s.cells[k].building=="" and not "tile.water" in g.s.cells[k].traits:tile=k;break
	var e=g.create_tender([tile],base)
	if e!="":failure=e;return false
	var offer=choose(PREF[base]);trace.append({"action":"offer","base":base,"tile":tile,"hand":g.s.draft.offers.duplicate(true)})
	if not settle(offer):return false
	for i in 80:
		var work=false
		for p in g.s.projects.values():
			if p.status=="construction":work=true
		if not work:return true
		if not pulse():return false
	failure="施工未能完成";return false
func play(seed_value:int)->Dictionary:
	g=Game.new();trace=[];failure="";g.new_game(seed_value,1000000);g.select_block(0);g.choose_origin(g.s.origin_choices[0]);var ok=true
	for pair in [["farm","-1,1"],["timber","0,0"],["power","1,0"],["hamlet","0,2"],["works","0,-1"],["market","1,2"],["clinic","-1,2"],["school","1,-1"],["park","-1,3"]]:
		if pair[0]!="hamlet" and has(pair[0]):continue
		if not build(pair[0],pair[1]):ok=false;break
		# Actual production is now the tax basis. This is a paid public policy command.
		if pair[0]=="works":
			var e=g.switch_policy("tax_production")
			if e!="":failure=e;ok=false;break
	if ok and g.s.block_opportunities>0:
		var e=g.create_block_offer()
		if e!="":failure=e;ok=false
		elif not settle(choose(["P05","B02","B09","B05","B06","B01","P01","B03"])):ok=false
	if ok:
		# One second-stage refit demonstrates Provider != Operator and finite slots.
		var target=""
		for id in g.playable_ids():
			if g.s.projects[id].def=="works":target=id;break
		var e=g.create_refit(target,"Logistics")
		if e!="":failure=e;ok=false
		elif not settle(choose(["L03","L05","L02"])):ok=false
	if ok:
		for i in 40:
			if g.s.complete and g.inventory_total("goods")>.1:break
			if not pulse():ok=false;break
	ok=ok and g.s.complete and not g.s.debug_used and g.validate_state(g.s)==""
	var report={"seed":seed_value,"success":ok,"error":failure,"origin":g.s.origin,"local_hours":g.s.local_hours,"dt":g.s.dt,"currency":g.s.currency,"people":g.s.metrics.population,"happiness":g.s.metrics.happiness,"jobs":g.s.metrics.jobs,"buildings":g.playable_ids().size(),"installed":g.s.totals.installed,"scope_opportunities":g.s.block_opportunities,"complete":g.s.complete,"debug":g.s.debug_used,"trace":trace}
	FileAccess.open(Paths.file("route020-"+str(seed_value)+".json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	FileAccess.open(Paths.file("state020-"+str(seed_value)+".json"),FileAccess.WRITE).store_string(Store.encode(g.s))
	print("ROUTE ",seed_value," ",ok," ",failure," h=",g.s.local_hours," money=",g.s.currency," dt=",g.s.dt," pop=",g.s.metrics.population," happiness=",g.s.metrics.happiness)
	return report
func run()->int:
	var results=[];var success=0
	for seed_value in [7,18,31,49,67,88]:
		var result=play(seed_value)
		if result.success:success+=1
		result.erase("trace");results.append(result)
	FileAccess.open(Paths.file("routes020.json"),FileAccess.WRITE).store_string(JSON.stringify({"passed":success,"total":results.size(),"routes":results,"method":"All time paid with initial DT; actual offered cards; no debug grants, injected resources, forced bonus, task rewards or offline time. Heuristic script has layout knowledge; not proof of human enjoyment or all seeds."},"\t"))
	print("ROUTES020 ",success,"/",results.size());return 0 if success==results.size() else 1
