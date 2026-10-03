extends RefCounted
## Complete-city regression using public player commands. No grants, forced cards or free time.
const Hex=preload("res://core/hex.gd")
const Game=preload("res://core/game.gd")
const Planner=preload("res://core/planning.gd")
const Paths=preload("res://tests/paths.gd")
const Store=preload("res://core/storage.gd")
var g
var planner=Planner.new()
var trace=[]
var failure=""
var strategy="community"
const PREF={"hamlet":["O03","R18","O02","R17","M01"],"farm":["R15","P07","R14","L05","M01"],"timber":["L05","M01","L02"],"power":["L05","M01","L02"],"works":["P01","P05","B09","M01"],"market":["R08","O07","R10","L01"],"school":["O04","P03","L04"],"clinic":["O01","M01","P02","R05"],"park":["L04","M01","L05"],"warehouse":["L05","P06","M01"]}
func has(base:String) -> bool:return find(base)!=""
func find(base:String) -> String:
	for id in g.playable_ids():
		if g.s.projects[id].def==base:return id
	return ""
func pulse() -> bool:
	var r=g.pulse(.5);trace.append({"action":"DT","hours":r.advanced,"dt":r.spent,"money":g.s.currency,"people":g.s.metrics.population,"food":g.inventory_total("food"),"cashflow":g.s.metrics.cashflow})
	if r.advanced<.001:failure="无法推进："+r.reason;return false
	if g.validate_state(g.s)!="":failure=g.validate_state(g.s);return false
	if g.s.metrics.population>=31 and not g.s.migration_paused:g.toggle_migration();trace.append({"action":"pause_arrivals"})
	if g.s.phase!="play":failure="任期结束";return false
	return true
func choose() -> Dictionary:
	var best={};var top=-INF;var d=g.s.draft
	for offer in d.offers:
		var prediction=planner.compare(g,d.base,d.tile,offer,d.host if d.kind=="refit" else "",d.slot)
		if not prediction.get("ok",false):continue
		var score=offer_score(d.base,offer,prediction)
		if score>top:top=score;best=offer
	return best
func offer_score(base:String,offer:Dictionary,prediction:Dictionary) -> float:
	var score=0.;var st=prediction.stats
	match base:
		"farm":score=prediction.outputs.get("food",0)*5.+prediction.housing*.2
		"timber":score=prediction.outputs.get("material",0)*5
		"power":score=prediction.energy_supply*4
		"works":score=prediction.outputs.get("goods",0)*6
		"hamlet":score=prediction.housing*.2
		"clinic":score=prediction.added_service.medical*4+st["service.medical"]*.05
		"market":score=prediction.added_service.commercial*4+prediction.added_service.entertainment
		"park":score=prediction.added_service.entertainment*4
		"school":score=prediction.added_service.education*4+prediction.added_service.entertainment
	var pref=PREF.get(base,[]).find(offer.bonus_id)
	if pref>=0:score+=maxf(0,4-pref*.5)
	score-=st.maintenance*.8
	return score
func settle(offer:Dictionary) -> bool:
	if offer.is_empty():failure="报价无可用方案";return false
	for i in 100:
		if g.s.currency>=g.Draft.offer_cost(g,offer).currency:break
		if not pulse():return false
	var e=g.accept_offer(offer.id)
	if e!="":failure=e;return false
	trace.append({"action":"accept","offer":offer.duplicate(true)});return true
func build(base:String,where:String) -> bool:
	print("BUILD030 ",g.s.seed," ",base," h=",g.s.local_hours," cash=",g.s.currency)
	var tile=where
	if not g.s.cells.has(tile) or g.s.cells[tile].building!="" or "tile.water" in g.s.cells[tile].traits:
		var best=999
		for k in g.Rules.ids(g.s.cells):
			if g.s.cells[k].building!="" or "tile.water" in g.s.cells[k].traits:continue
			var dist=Hex.distance(k,where)
			if dist<best:tile=k;best=dist
	var e=g.create_tender([tile],base)
	if e!="":failure=e;return false
	var selected=choose()
	# A player can freely move an uncommitted quote across the map. Read the
	# actual placement previews to cover residents, instead of waiting forever
	# beside an out-of-range clinic. This does not redraw the offer or alter RNG.
	if base in ["clinic","market","park","school"]:
		var top=-INF;var best_tile=tile;var best_offer=selected
		var candidate_tiles=g.Rules.ids(g.s.cells)
		candidate_tiles.sort_custom(func(a,b):return Hex.distance(a,"0,1")+Hex.distance(a,"0,2")<Hex.distance(b,"0,1")+Hex.distance(b,"0,2"))
		var checked=0
		for candidate in candidate_tiles:
			if g.s.cells[candidate].building!="" or "tile.water" in g.s.cells[candidate].traits:continue
			checked+=1
			if checked>12:break
			for offer in g.s.draft.offers:
				var prediction=planner.compare(g,base,candidate,offer)
				if not prediction.get("ok",false):continue
				var score=offer_score(base,offer,prediction)
				if score>top:top=score;best_tile=candidate;best_offer=offer
		tile=best_tile;g.cancel_draft();g.create_tender([tile],base);selected=best_offer
	trace.append({"action":"offer","base":base,"tile":tile,"hand":g.s.draft.offers.duplicate(true)})
	if not settle(selected):return false
	for i in 60:
		var constructing=false
		for p in g.s.projects.values():
			if p.status=="construction":constructing=true
		if not constructing:return true
		if not pulse():return false
	failure="施工长时间停滞";return false
func play(seed_value:int,route:String,map_index:int,origin_choice:int=0,tax:String="tax_head") -> Dictionary:
	g=Game.new();planner=Planner.new();trace=[];failure="";strategy=route
	g.new_game(seed_value,1000000);g.select_block(map_index);g.choose_origin(g.s.origin_choices[origin_choice]);g.Progress.select(g,route)
	if tax!="tax_head":g.switch_policy(tax);trace.append({"action":"choose_tax","id":tax})
	var ok=true
	for pair in [["farm","-1,1"],["timber","2,0"],["power","1,0"],["hamlet","0,2"]]:
		if pair[0]!="hamlet" and has(pair[0]):continue
		if not build(pair[0],pair[1]):ok=false;break
	var more=[["market","1,1"],["clinic","-1,2"],["park","1,2"]]
	if route=="delivery":more=[["works","3,0"],["market","1,2"]]
	elif route=="renewal":more=[["clinic","-1,2"],["school","1,2"],["market","1,1"]]
	for pair in more:
		if not ok:break
		if not build(pair[0],pair[1]):ok=false;break
	# One paid scheme procurement, chosen from the real hand, on a compatible actual slot.
	if ok and route=="delivery":
		var bid=find("works")
		for slot in g.slot_ids(bid):
			if g.s.projects[bid].slots[slot]!="" or g.slot_type(bid,slot)!="Logistics":continue
			if g.create_refit(bid,slot)=="":ok=settle(choose())
			break
	if ok:
		for i in 60:
			if g.s.city_goal.completed:break
			# Use the same visible city problems as the UI. Do not paper over
			# inadequate service coverage with endless time or a smaller goal.
			if i%4==0 and route=="community":
				var kind="";var smallest=.75
				for k in ["medical","commercial","entertainment"]:
					var ratio=g.Progress.service_ratio(g,k)
					if ratio<smallest:smallest=ratio;kind=k
				if g.s.metrics.vacancies<2:
					if not build("hamlet","-2,2"):ok=false;break
				elif g.s.metrics.get("energy_ratio",1)<.95:
					if not build("power","3,-1"):ok=false;break
				elif kind!="":
					if not build({"medical":"clinic","commercial":"market","entertainment":"park"}[kind],"0,3"):ok=false;break
			if route=="delivery" and g.s.city_goal.stable>=1 and g.inventory_total("material")>=20 and g.inventory_total("goods")>=12:
				var e=g.Progress.deliver(g)
				if e=="":trace.append({"action":"deliver","material":20,"goods":12})
			# Prioritize the existing service that is short of staff; no test-side stat edits.
			if route!="delivery":
				for def in (["clinic","market","park"] if route=="community" else ["clinic","school"]):
					var bid=find(def)
					if bid!="":g.set_priority(bid,1)
			if not pulse():ok=false;break
	ok=ok and g.s.city_goal.completed and not g.s.debug_used and g.validate_state(g.s)==""
	if not ok and failure=="":failure="未完成城市成果："+JSON.stringify(g.s.city_goal)
	var report={"seed":seed_value,"success":ok,"error":failure,"route":route,"map":map_index,"origin":g.s.origin,"tax":tax,"hours":g.s.local_hours,"dt":g.s.dt,"currency":g.s.currency,"people":g.s.metrics.population,"happiness":g.s.metrics.happiness,"health":g.s.metrics.health,"cashflow":g.s.metrics.cashflow,"energy_ratio":g.s.metrics.energy_ratio,"city_goal":g.s.city_goal,"buildings":g.playable_ids().size(),"debug":g.s.debug_used,"trace":trace}
	FileAccess.open(Paths.file("route030-"+str(seed_value)+("-"+tax if tax!="tax_head" else "")+".json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	FileAccess.open(Paths.file("state030-"+str(seed_value)+("-"+tax if tax!="tax_head" else "")+".json"),FileAccess.WRITE).store_string(Store.encode(g.s))
	print("ROUTE030 ",seed_value," ",route," ",tax," ",ok," hours=",g.s.local_hours," money=",g.s.currency," dt=",g.s.dt," people=",g.s.metrics.population," h=",g.s.metrics.happiness," ",failure)
	return report
func run() -> int:
	var results=[];var passed=0
	for route in [[7,"community",0],[18,"delivery",0],[31,"renewal",0],[49,"community",1],[67,"delivery",2],[88,"renewal",2],[25092026,"delivery",0,1],[7,"community",0,0,"tax_trade"],[18,"delivery",0,0,"tax_production"]]:
		var r=play(route[0],route[1],route[2],route[3] if route.size()>3 else 0,route[4] if route.size()>4 else "tax_head");r.erase("trace");results.append(r)
		if r.success:passed+=1
	FileAccess.open(Paths.file("routes030.json"),FileAccess.WRITE).store_string(JSON.stringify({"passed":passed,"total":results.size(),"routes":results,"method":"Public player commands, actual offered schemes, real costs and DT, legitimate city-outcome rewards. Heuristic uses visible forecast and map knowledge. No grants or forced cards; not human enjoyment evidence."},"\t"))
	return 0 if passed==results.size() else 1
