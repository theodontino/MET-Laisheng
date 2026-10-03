extends SceneTree
## Theme contracts use explicit committed-receipt fixtures, not balance claims.
const Game = preload("res://core/game.gd")
const Themes = preload("res://core/commission_theme.gd")
const MONTH = 1555200.0
var checks = 0
var failures = 0

func _initialize() -> void:
	call_deferred("_run")

func check(ok:bool, label:String) -> void:
	checks += 1
	if not ok: failures += 1
	print(("BETA THEME PASS " if ok else "BETA THEME FAIL ") + label)

func fresh(seed_value:int=8291):
	var g = Game.new()
	g.new_game(seed_value, 1000000.0)
	g.select_block(0)
	g.choose_origin(str(g.s.origin_choices[0]))
	Themes.ensure(g)
	return g

func hold(g, months:float, food:float=1.0) -> void:
	for group in g.s.groups.values():
		group.living = {"food":food,"medical":1.0,"education":1.0,"commercial":1.0,"entertainment":1.0}
	g.s.simulation_tick += 1
	g.s.local_hours += months
	g.s.beta_time.local_elapsed_seconds += months * MONTH
	g.s.audit = {"hours":months,"resources":{"food":{"produced":100.0 * months},"goods":{"produced":8.0 * months}},"commercial_service_people":48.0,"population_food_demand_rate":5.0}
	g.s.metrics.energy_ratio = 1.0
	Themes.tick(g, months * MONTH)

func offer(g, id:String) -> Dictionary:
	var e = {"id":id,"status":"offered","theme":str(g.s.block.theme.id),"revealed_seconds":g.s.beta_time.local_elapsed_seconds}
	g.s.block.theme.events.append(e)
	g.s.block.theme.event_ids.append(id)
	return e

func _run() -> void:
	var g = fresh()
	check(Themes._themes().size() == 5 and Themes._events().size() == 15, "five data-driven themes and fifteen distinct events")
	var state = JSON.stringify(g.s)
	Themes.view(g)
	Themes.view(g)
	check(state == JSON.stringify(g.s), "theme panel reads neither mutate state nor consume RNG")
	var revision=int(g.revision)
	check(Themes.select(g,"agricultural") == "", "initial theme selection succeeds")
	check(int(g.revision)==revision+1, "successful selection invalidates planning cache")
	revision=int(g.revision)
	check(Themes.select(g,"industrial") != "", "ordinary free switching prohibited")
	check(int(g.revision)==revision, "rejected theme command keeps planning revision unchanged")
	hold(g,.5)
	check(float(g.s.block.theme.steady_seconds)==0.0, "first satisfactory endpoint cannot retroactively credit elapsed hold")
	hold(g,.5)
	check(absf(float(g.s.block.theme.steady_seconds)-.5*MONTH)<.01, "consecutive actual local production begins stable hold")
	var elapsed = float(g.s.block.theme.steady_seconds)
	Themes.tick(g,.5*MONTH)
	check(float(g.s.block.theme.steady_seconds)==elapsed, "same committed receipt cannot count twice")
	g.s.simulation_tick += 1
	g.s.audit.resources.food.produced = 0.0
	Themes.tick(g,.5*MONTH)
	check(float(g.s.block.theme.steady_seconds)==0.0, "import inventory cannot pretend to be local production")
	hold(g,.1)
	hold(g,1)
	check(int(g.s.block.theme.stage)==2 and "agricultural:M1" in g.s.block.theme.milestone_ids, "hold completion advances one stable milestone")
	var reward_count = g.s.block.reward_choices.size()
	g.s.block.theme.reforge_tokens = 2
	g.s.dt = 7.49
	state = JSON.stringify(g.s)
	check(Themes.reforge(g,"industrial")!="" and state==JSON.stringify(g.s), "insufficient DT leaves entire selection and token untouched")
	g.s.dt = 30.0
	check(Themes.reforge(g,"industrial")=="" and int(g.s.block.theme.reforge_tokens)==1 and absf(float(g.s.dt)-22.5)<.001, "reforge atomically consumes token and 7.5 DT")
	check(Themes.reforge(g,"agricultural")=="" and int(g.s.block.theme.stage)==2, "returning to theme skips already-earned milestone")
	check(g.s.block.reward_choices.size()==reward_count, "reforge preserves reward queue without duplicating milestone")

	g = fresh()
	g.s.block.trade_history = [{"amount":1000.0,"side":"sell","resource":"food"}]
	Themes.select(g,"logistics")
	hold(g,.1)
	check(float(g.s.block.theme.counters.get("trade",0))==0.0, "trade before theme acceptance excluded by cursor")
	g.s.block.trade_history.append({"amount":12.0,"side":"buy","resource":"food"})
	hold(g,.1)
	check(int(g.s.block.theme.stage)==2 and float(g.s.block.theme.counters.get("trade",0))==0.0, "M1 trade never carries into M2")
	g.s.block.trade_history.append({"amount":48.0,"side":"sell","resource":"goods"})
	hold(g,1)
	check(int(g.s.block.theme.stage)==3, "trade accepts either direction with actual food stability")
	g.s.block.trade_history.append({"amount":96.0,"side":"buy","resource":"material"})
	g.s.currency = 100
	g.s.projects.municipal.inventory.food = 40
	hold(g,.1)
	check(bool(g.s.block.theme.completed), "logistics completes only with current cash and food reserves")
	g.s.block.theme.reforge_tokens=1
	check(Themes.reforge(g,"civic")!="", "completed development theme is locked")

	g = fresh()
	Themes.select(g,"industrial")
	g.s.block.theme.stage=3
	g.s.projects.municipal.inventory.goods=30
	g.s.projects.municipal.inventory.material=40
	var before = g.inventory_total("goods")
	revision=int(g.revision)
	check(Themes.deliver(g,"goods")=="" and absf(g.inventory_total("goods")-before+18)<.001, "industrial delivery consumes real goods")
	check(int(g.revision)==revision+1, "real inventory delivery invalidates planning cache")
	check(g.s.block.get("trade_history",[]).is_empty(), "active delivery cannot double count as market export")
	check(Themes.deliver(g,"goods")!="", "completed delivery cannot consume twice")

	g = fresh()
	Themes.select(g,"agricultural")
	g.s.block.theme.stage=3
	g.s.projects.municipal.inventory.food=10
	var old_food=g.inventory_total("food")
	var demand=float(Themes._population_metrics(g).food_demand)
	Themes.deliver(g,"food")
	check(g.inventory_total("food")+.001>=3*demand and g.inventory_total("food")<=old_food, "agricultural delivery retains three months actual demand")

	g = fresh()
	Themes.select(g,"industrial")
	var e=offer(g,"industrial_equipment")
	g.s.currency=59
	state=JSON.stringify(g.s)
	check(Themes.choose_event(g,str(e.id),"accept")!="" and JSON.stringify(g.s)==state, "event insufficient cash cannot partially take materials")
	g.s.currency=100
	g.s.projects.municipal.inventory.material=20
	reward_count=g.s.block.reward_choices.size()
	revision=int(g.revision)
	check(Themes.choose_event(g,str(e.id),"accept")=="" and float(g.s.currency)==40 and g.s.block.reward_choices.size()==reward_count+1, "paid equipment event queues a real copy reward")
	check(int(g.revision)==revision+1, "paid event invalidates planning cache once after commit")
	check(Themes.choose_event(g,str(e.id),"accept")!="", "resolved event cannot pay or reward twice")
	e=offer(g,"industrial_order")
	g.s.currency=100
	check(Themes.choose_event(g,str(e.id),"accept")=="" and float(g.s.currency)==80, "contract escrows twenty currency")
	check(absf(Themes.next_boundary_seconds(g)-MONTH)<.1, "next boundary includes theme hold before later contract deadline")
	g.s.projects.municipal.inventory.goods=20
	check(Themes.deliver(g,"",-1,str(e.id))=="" and float(g.s.currency)==180, "real contract delivery refunds escrow plus eighty currency")

	g=fresh()
	Themes.select(g,"industrial")
	e=offer(g,"industrial_order")
	g.s.currency=100
	Themes.choose_event(g,str(e.id),"accept")
	hold(g,3)
	check(str(e.status)=="failed" and float(g.s.currency)==80, "exact deadline fails unfulfilled contract and only loses paid deposit")
	state=JSON.stringify(g.s)
	var event_history=Themes.view(g).history[-1]
	check(event_history.name=="大宗订单" and event_history.status_label=="逾期未达成" and str(event_history.reason)!="", "failed event history explains actual event and consequence in player language")
	check(state==JSON.stringify(g.s), "readable history projection keeps serialized history untouched")

	g=fresh()
	Themes.select(g,"commercial")
	e=offer(g,"commercial_purchase")
	g.s.block.trade_history=[{"amount":100,"side":"buy","resource":"goods"}]
	Themes.choose_event(g,str(e.id),"accept")
	g.s.block.trade_history.append({"amount":12,"side":"sell","resource":"goods"})
	hold(g,.1)
	check(str(e.status)=="active" and float(e.progress)==0, "import contract excludes past trades and exports")
	var cash=float(g.s.currency)
	g.s.block.trade_history.append({"amount":12,"side":"buy","resource":"goods"})
	hold(g,.1)
	check(str(e.status)=="completed" and absf(float(g.s.currency)-cash-80)<.001, "actual matching imports complete contract once")

	g=fresh()
	Themes.select(g,"civic")
	e=offer(g,"civic_families")
	Themes.choose_event(g,str(e.id),"accept")
	var people=float(Themes._population_metrics(g).population)
	hold(g,.5)
	check(absf(float(Themes._population_metrics(g).population)-people)<.001, "new-family event does not spawn population before local month boundary")
	hold(g,.5)
	check(absf(float(Themes._population_metrics(g).population)-people-6)<.001, "six local-time migrants actually arrive at next month boundary")
	hold(g,.5)
	check(absf(float(Themes._population_metrics(g).population)-people-6)<.001, "event arrival does not replay")

	g=fresh()
	Themes.select(g,"industrial")
	g.s.block.theme.reforge_tokens=1
	g.s.dt=7.5
	check(Themes.reforge(g,"civic")=="" and float(g.s.dt)==0.0 and bool(g.s.block.get("ending",{}).get("triggered",false)), "exact-cost reforge triggers actual DT-zero closure")

	g=fresh()
	Themes.select(g,"civic")
	for group in g.s.groups.values(): group.living={"food":1.0,"medical":1.0,"entertainment":1.0}
	Themes.on_major_cycle(g,1)
	check(not g.s.block.theme.events.is_empty(), "major cycle freezes eligible theme event selection")
	state=JSON.stringify(g.s)
	Themes.view(g)
	Themes.on_major_cycle(g,1)
	check(state==JSON.stringify(g.s), "same major cycle or reading panel cannot redraw an event")

	g=fresh()
	Themes.select(g,"civic")
	e=offer(g,"civic_space")
	for group in g.s.groups.values(): group.living.entertainment=.9
	state=JSON.stringify(g.s)
	check(Themes.choose_event(g,str(e.id),"accept")!="" and JSON.stringify(g.s)==state, "changed coverage cannot accept an impossible improvement contract")
	for group in g.s.groups.values(): group.living.entertainment=.5
	Themes.choose_event(g,str(e.id),"accept")
	check(absf(float(Themes._event_view(g,e).target)-.7)<.001, "accepted improvement event displays its absolute target")
	hold(g,.1)
	check(str(e.status)=="completed", "actual coverage increase completes public-space event")

	g=fresh()
	Themes.select(g,"commercial")
	e=offer(g,"commercial_boom")
	Themes.choose_event(g,str(e.id),"accept")
	hold(g,1)
	check(str(e.status)=="active" and absf(float(e.progress)-48)<.001, "commercial event accumulates actual person-months")
	hold(g,1)
	check(str(e.status)=="completed", "service receipt at exact deadline succeeds before timeout")

	g=fresh()
	Themes.select(g,"industrial")
	e=offer(g,"industrial_order")
	g.s.currency=100
	Themes.choose_event(g,str(e.id),"accept")
	g.s.block.theme.reforge_tokens=1
	g.s.dt=30
	Themes.reforge(g,"civic")
	check(str(e.status)=="cancelled" and float(g.s.currency)==100, "reforge cancels unfinished contract without forfeiting escrow")

	g=fresh()
	Themes.select(g,"agricultural")
	g.s.block.theme.stage=3
	g.s.audit.population_food_demand_rate=.1
	g.Population.create(g,"homeless",6)
	var current_demand=Themes._current_food_demand(g)
	g.s.projects.municipal.inventory.food=current_demand*3
	state=JSON.stringify(g.s)
	check(Themes.deliver(g,"food")!="" and JSON.stringify(g.s)==state, "delivery reserve includes new migrants despite earlier audit demand")

	g=fresh()
	g.s.audit={"hours":1.0/MONTH,"resources":{"food":{"produced":10.0/MONTH},"goods":{"produced":2.0/MONTH}},"population_food_demand_rate":5.0}
	var metrics=Themes._metrics(g)
	check(absf(float(metrics.goods_rate)-2.0)<.000001 and absf(float(metrics.food_self_sufficiency)-2.0)<.000001, "one-second normal-speed receipts preserve true monthly rates")

	g=fresh()
	var industrial_id=""
	for id in g.playable_ids():
		if "building.industrial" in g.s.projects[id].traits: industrial_id=str(id)
	g.s.projects[industrial_id].labels.append("use.commercial")
	g.s.projects[industrial_id].usage=.8
	g.compiled.stats[industrial_id]["service.commercial"]=40
	check(absf(float(Themes._metrics(g).commercial_usage)-.8)<.0001, "converted commercial hosts participate in utilization weighting")

	print("BETA THEME RESULT %d/%d" % [checks-failures,checks])
	quit(1 if failures>0 else 0)
