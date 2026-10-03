extends SceneTree
## Deterministic real-command strategy routes. No inventory, population, DT,
## project, reward, progress or RNG injections. Running time is simulated through
## the same online clock API as the app; wall-clock wait is not required in CI.
const Game=preload("res://core/game.gd")
const Store=preload("res://core/storage.gd")
const Paths=preload("res://tests/paths.gd")
var g
var actions=[]
var rows=[]
var errors=[]
var handles={}
var first_bonus=-1.
var pause_reasons={}
var theme_id=""
var reforge_route=false
var reforge_done=false
func _initialize():call_deferred("run")
func record(kind:String,detail:Dictionary={}):
	var row={"kind":kind,"month":g.s.local_hours,"dt":g.s.dt,"cash":g.s.currency};row.merge(detail,true);actions.append(row)
func must(ok:bool,label:String):
	if not ok:errors.append(label);print("BETA ROUTE FAIL ",theme_id," ",label)
func slot_install(bid:String)->bool:
	var meta=g.Bonus._metadata(g,bid);var scope=str(meta.get("scope","building"))
	if scope=="block" and g.bonus_eligible(bid,"block",g.s.block.id)=="":
		var error=g.Bonus.install(g,bid,"block",g.s.block.id);if error=="":record("install",{"bonus":bid,"host":"block"});return true
	if scope=="organization":
		for oid in g.s.organizations:
			if g.bonus_eligible(bid,"organization",oid)=="":
				if g.Bonus.install(g,bid,"organization",oid)=="":record("install",{"bonus":bid,"host":oid});return true
	if scope=="building":
		for id in g.playable_ids():
			for slot in g.s.projects[id].slots:
				if g.s.projects[id].slots[slot]!="":continue
				if g.bonus_eligible(bid,"building",id,slot)=="":
					if g.Bonus.install(g,bid,"building",id,slot)=="":record("install",{"bonus":bid,"host":id});return true
	return false
func handle_decisions():
	for reward in g.Bonus.pending(g):
		var bids=[]
		for card in reward.get("choices",[]):bids.append(str(card.bonus_id))
		var chosen="__currency__" if bids.is_empty() else str(bids[0])
		var wanted={"industrial":["P01","P06","L05"],"agricultural":["R15","P07","R10"],"civic":["O03","R05","P02"],"commercial":["L01","R08","O07"],"logistics":["L05","B05","G02"]}
		for preferred in wanted.get(theme_id,[]):
			if preferred in bids:chosen=preferred;break
		var err=g.Bonus.choose(g,str(reward.get("id",reward.get("reward_id",""))),chosen)
		must(err=="","claim reward "+err)
		if err=="":
			if first_bonus<0:first_bonus=g.s.local_hours
			record("reward",{"source":reward.get("source",""),"choice":chosen,"reward_kind":reward.get("kind","")})
	for bid in g.s.block.bonus_wallet:
		if int(g.s.block.bonus_wallet[bid].get("charges",0))>0 and not (theme_id=="civic" and bid in ["P02","R05"]):slot_install(str(bid))
	for event in g.Themes.view(g).events:
		if event.status!="offered":continue
		# One route deliberately declines opportunities; no fake penalties imposed.
		var choice="accept" if str(event.id).begins_with("reforge_opportunity:") else "decline"
		var err=g.Themes.choose_event(g,str(event.id),choice)
		must(err=="","event choice "+err);record("event",{"id":event.id,"choice":choice})
	g.Cycles.acknowledge(g)
func ordered_tiles(service:bool)->Array:
	var ids=g.s.cells.keys();var out=[]
	for tile in ids:
		if g.s.cells[tile].building!="" or "tile.water" in g.s.cells[tile].traits:continue
		out.append(str(tile))
	out.sort_custom(func(a,b):
		var center="0,1" if service else "2,-1"
		var da=g.Hex.distance(a,center);var db=g.Hex.distance(b,center)
		return a<b if da==db else da<db)
	return out
func construct(handle:String,base:String)->bool:
	var service=base!="workshop";var tiles=ordered_tiles(service)
	for tile in tiles:
		var err=g.create_tender([tile],base)
		if err!="":continue
		if g.s.draft.get("offers",[]).is_empty():g.cancel_draft();continue
		var pick=g.s.draft.offers[0]
		var preferred="farmers" if base=="homestead" else ("civic" if base=="neighborhood" else "steel")
		for offer in g.s.draft.offers:
			if offer.operator_org==preferred:pick=offer;break
		err=g.accept_offer(str(pick.id))
		if err!="":g.cancel_draft();return false
		handles[handle]=str(g.s.cells[tile].building);record("build",{"handle":handle,"node":base,"tile":tile,"operator":pick.operator_org});return true
	return false
func advance_month(fraction:float=0.25):
	g.s.settings.user_paused=false
	var mode="burn"
	if g.Market._total_capacity(g)>0 and not bool(g.s.block.theme.completed):
		if theme_id=="industrial":mode="observe"
		if theme_id=="logistics":
			for goal in g.Themes.view(g).goals:
				if goal.id=="trade" and not goal.met:mode="observe"
	g.Clock.set_mode(g,mode)
	var report=g.advance_active(fraction*(300. if mode=="observe" else 20.))
	if str(report.get("reason",""))!="":
		var reason=str(report.reason);pause_reasons[reason]=int(pause_reasons.get(reason,0))+1
	if float(report.local_seconds)<=0 and g.s.dt>0:must(false,"clock failed: "+str(report.reason))
	handle_decisions()
func attempt_plan(step:Array)->bool:
	var handle=str(step[0]);var target=str(step[1])
	if not handles.has(handle):return construct(handle,target)
	var p=g.s.projects[handles[handle]]
	if p.def==target:return true
	if p.status!="running" or not p.upgrade_job.is_empty():return false
	var err=g.Development.start_upgrade(g,str(p.id),target)
	if err!="":return false
	record("upgrade",{"handle":handle,"target":target});return true
func support():
	# Additional homes and farms respond to real deficits, never direct stock edits.
	var m=g.s.metrics;var homeless=float(m.get("homeless",0));var housed=float(m.get("housed",0));var housing=float(m.get("housing",0))
	var home_jobs=0
	for p in g.s.projects.values():
		if p.def=="homestead" and p.status=="construction":home_jobs+=1
	if housing-housed<8. and home_jobs==0 and g.s.currency>=90. and g.inventory_total("material")>=8.:
		construct("housing_"+str(g.s.totals.built),"homestead")
	var food=float(g.s.metrics.get("flows",{}).get("food",0))
	if food<-.3 and g.inventory_total("food")<15. and home_jobs==0 and g.s.currency>=100. and g.inventory_total("material")>=12.:
		construct("food_"+str(g.s.totals.built),"homestead")
func trading():
	if theme_id not in ["logistics","industrial"] or g.Market._total_capacity(g)<=0:return
	for order in g.s.block.trade_orders:
		if order.status in ["open","partial"]:return
	var resource="goods";var side="buy";var amount=16.
	if theme_id=="industrial" and g.inventory_total("goods")>24.:resource="goods";side="sell";amount=24.
	elif g.inventory_total("material")>48.:resource="material";side="sell";amount=20.
	elif g.inventory_total("food")>60.:resource="food";side="sell";amount=20.
	elif g.inventory_total("goods")>20.:resource="goods";side="sell";amount=12.
	var quote=g.Market.quote(g,resource);var error=g.Market.order(g,resource,side,amount,float(quote.buy)+4. if side=="buy" else float(quote.sell)-4.)
	if error=="":record("trade_order",{"resource":resource,"side":side,"amount":amount})
func deliver():
	var view=g.Themes.view(g)
	for goal in view.goals:
		if goal.kind!="delivery" or goal.met:continue
		var resource=str(goal.id).trim_prefix("delivery_");var before=g.inventory_total(resource)
		if g.Themes.deliver(g,resource)=="":record("delivery",{"resource":resource,"amount":before-g.inventory_total(resource)})
func plans(id:String)->Array:
	if id=="civic":
		return [["home","homestead"],["services","neighborhood"],["materials","workshop"],["materials","extraction"],["power","power_station"],["medical","neighborhood"],["medical","medical_clinic"],["school","neighborhood"],["school","primary_school"],["services","public_center"],["civic4","neighborhood"],["civic5","neighborhood"],["civic6","neighborhood"],["civic7","neighborhood"],["civic8","neighborhood"],["civic9","neighborhood"],["civic10","neighborhood"],["civic11","neighborhood"],["power","power_plant"]]
	var common=[["home","homestead"],["services","neighborhood"],["materials","workshop"],["materials","extraction"],["power","power_station"],["farm","homestead"],["farm","agriculture"],["works","workshop"],["works","yard"]]
	var branch={
		"industrial":[["works","manufacturing"],["power","power_plant"],["materials","material_factory"],["industry2","workshop"],["industry2","yard"],["industry2","manufacturing"],["freight","workshop"],["freight","yard"],["freight","freight"]],
		"agricultural":[["farm","modern_agriculture"],["farm2","homestead"],["farm2","agriculture"],["farm2","modern_agriculture"],["services","public_center"],["medical","neighborhood"],["medical","medical_clinic"],["farm3","homestead"],["farm3","agriculture"]],
		"commercial":[["services","public_center"],["services","commercial_center"],["farm","modern_agriculture"],["shops2","neighborhood"],["shops2","public_center"],["shops2","commercial_center"],["power","power_plant"],["medical","neighborhood"],["medical","medical_clinic"],["goods","workshop"],["works","manufacturing"]],
		"civic":[["medical","neighborhood"],["medical","medical_clinic"],["school","neighborhood"],["school","primary_school"],["farm","modern_agriculture"],["services","public_center"],["power","power_plant"],["medical","hospital"],["school","secondary_school"],["services","commercial_center"],["park","neighborhood"],["park","public_center"],["park","garden"],["works","manufacturing"]],
		"logistics":[["works","freight"],["farm","modern_agriculture"],["services","public_center"],["medical","neighborhood"],["medical","medical_clinic"],["works2","workshop"],["works2","yard"],["works2","freight"]]}
	return common+branch[id]
func route(id:String,with_reforge:bool=false):
	reforge_route=with_reforge;reforge_done=false
	theme_id=id;g=Game.new();actions=[];errors=[];handles={};first_bonus=-1.;pause_reasons={}
	g.new_game(7040,1000000.);must(g.select_block(0)=="","landing selection");must(g.choose_origin(g.s.origin_choices[0])=="","origin selection")
	for bid in g.playable_ids():
		if g.s.projects[bid].def=="workshop":handles.power=bid
	must(g.Themes.select(g,"industrial" if with_reforge else id)=="","select theme");var plan=plans(id);var cursor=0;var last_month=-1;var trajectory=[]
	var started=Time.get_ticks_usec()
	for turn in 400:
		if g.s.dt<=0.:break
		handle_decisions()
		if with_reforge and not reforge_done and int(g.s.block.theme.reforge_tokens)>0:
			var dt_before=float(g.s.dt);var rewards_before=g.s.block.reward_choices.duplicate(true);var time_before=g.s.local_hours
			var err=g.Themes.reforge(g,id);must(err=="","organic rareToken reforge succeeds")
			reforge_done=err=="";must(absf(dt_before-float(g.s.dt)-7.5)<.000001 and g.s.local_hours==time_before,"reforge spends7.5DT without simulatingtime")
			must(g.s.block.reward_choices==rewards_before,"organic reforge retains earned and queuedrewards");record("reforge",{"from":"industrial","to":id,"dt_cost":7.5})
		support()
		if cursor<plan.size() and attempt_plan(plan[cursor]):cursor+=1
		if theme_id=="industrial" and g.inventory_total("goods")>24. and float(g.s.goods_requested_at)<0:
			if g.enable_goods()=="":record("goods_plan")
		trading();deliver();advance_month()
		var month=int(g.s.local_hours)
		if month!=last_month:
			last_month=month;var v=g.Themes.view(g)
			trajectory.append({"month":g.s.local_hours,"dt":g.s.dt,"cash":g.s.currency,"population":g.s.metrics.population,"housed":g.s.metrics.housed,"stock":{"food":g.inventory_total("food"),"material":g.inventory_total("material"),"goods":g.inventory_total("goods")},"theme_stage":v.stage,"complete":v.completed,"goals":v.goals,"construction":g.Construction.queue(g),"plan_cursor":cursor})
			if month%12==0:print("BETA ROUTE ",id," month ",g.s.local_hours," stage ",v.stage," dt ",g.s.dt," plan ",cursor)
		if g.s.phase!="play":must(false,"city ended unexpectedly "+g.s.phase);break
	if with_reforge:must(reforge_done,"route found and used real rareToken")
	must(g.s.block.theme.completed,"theme organically completed before DTzero")
	must(g.s.dt==0.,"route reaches exactDTzero")
	must(not g.s.debug_used,"route never uses debug supply")
	must(g.validate_state(g.s)=="","route final state validates")
	if g.s.dt==0.:
		must(g.Block.name_block(g,"Beta·"+id)=="","name at ending")
		if g.s.block.theme.completed:must(g.Block.claim_capstone(g)=="","earned themeCapstone")
	var route_key=id+("_reforge" if with_reforge else "")
	var save_path=Paths.file("beta_route_"+route_key+"_save.json");must(Store.write(g.s,save_path)=="","save organic city")
	var row={"theme":id,"route":route_key,"reforge_route":with_reforge,"passed":errors.is_empty(),"errors":errors.duplicate(),"first_bonus_month":first_bonus,"actions":actions.duplicate(true),"trajectory":trajectory,"theme_view":g.Themes.view(g),"remaining_dt":g.s.dt,"active_real_seconds":g.s.beta_time.active_real_seconds,"local_months":g.s.local_hours,"pause_reasons":pause_reasons.duplicate(),"cpu_seconds":(Time.get_ticks_usec()-started)/1000000.,"save":save_path,"uses_debug_or_fixture_injection":false}
	rows.append(row);var file=FileAccess.open(Paths.file("beta_route_"+route_key+".json"),FileAccess.WRITE);file.store_string(JSON.stringify(row,"\t"));file.close()
	print("BETA ROUTE ",id," ","PASS" if errors.is_empty() else "FAIL", " completion ",g.s.block.theme.completed)
func run():
	var selected=["industrial","civic","commercial","logistics","agricultural","reforge"]
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--theme="):selected=[arg.trim_prefix("--theme=")]
	for id in selected:
		if id=="reforge":route("agricultural",true)
		else:route(str(id))
	var failed=rows.filter(func(row):return not row.passed)
	var result={"evidence_type":"deterministic_player_command_routes","passed":failed.is_empty(),"routes":rows,"limitations":["Fixed seed automated strategy is not a human playtest or proof of all-seed balance","Recorded active real seconds are submitted game clock inputs, not actual CI waiting"]}
	var f=FileAccess.open(Paths.file("beta_playthrough.json"),FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));f.close();print("BETA PLAYTHROUGH ",rows.size()-failed.size(),"/",rows.size());quit(0 if failed.is_empty() else 1)
