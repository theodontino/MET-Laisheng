extends RefCounted
const Game=preload("res://core/game.gd")
const Civic=preload("res://core/civic032.gd")
const Store=preload("res://core/storage.gd")
const Planner=preload("res://core/planning.gd")
const Data=preload("res://ui/view_models/map_data.gd")
const Models=preload("res://ui/city032/models.gd")
const Paths=preload("res://tests/paths.gd")
var checks=[]
func check(ok:bool,name:String):checks.append({"pass":ok,"name":name});print(("CIVIC PASS " if ok else "CIVIC FAIL ")+name)
func near(a:float,b:float)->bool:return absf(a-b)<.00001
func fresh(seed:int=7):
	var g=Game.new();g.new_game(seed,1000000.);g.select_block(0);g.choose_origin(g.s.origin_choices[0]);return g
func run() -> int:
	var g=fresh();var plan=Planner.new();var snapshot=Store.encode(g.s)
	check(g.s.build=="0.3.2-citydesk.1","single 032 release entry")
	check(g.validate_state(g.s)=="","new civic state validates")
	check(g.s.civic032.history.is_empty() and g.s.civic032.transactions.is_empty(),"no fake retrospective history at opening")
	check(Civic.task_rows(g).filter(func(x):return x.id=="food")[0].progress==0.,"food task does not show completed bar before first real cycle")
	var shot=Data.current(g,plan)
	var pop=Models.population(g,shot)
	check(near(pop.count,float(shot.metrics.population)) and pop.population_rate==null,"population snapshot known; historical growth not fabricated")
	check(near(pop.housing,shot.metrics.housing),"housing capacity is not population count")
	check(pop.coverage_kinds.size()==8,"individual food material goods power and four services")
	for resource in ["food","material","goods","energy"]:
		var r=Models.resource(g,shot,resource);check(r.id==resource and r.sources is Array and r.sinks is Array,"detail source/sink model "+resource)
	Models.budget(g,shot);Civic.task_rows(g)
	for id in g.c.policies:plan.policy(g,id)
	check(Store.encode(g.s)==snapshot,"all information pages and policy predictions leave state RNG and money unchanged")
	# Real cycles create measurable history; no free money from the views.
	var before=g.s.currency;var prior_pop=g.s.metrics.population;var advance=g.pulse(.3)
	check(near(advance.advanced,.3),"paid advance still advances chosen duration")
	check(g.s.civic032.history.size()==3,"one sample for each actual completed cycle")
	var expected=0.
	for row in g.s.civic032.history:expected+=row.net*row.span
	var action_cash=0.
	for row in g.s.civic032.transactions:action_cash+=row.cash
	check(near(g.s.currency-before,expected+action_cash),"operating history plus one-offs reconciles actual wallet")
	check(g.s.civic032.last_population_rate!=null,"growth rate recorded only after actual cycle")
	check(near(g.s.civic032.history[-1].balance,g.s.currency),"history final balance includes exact current cash")
	check(near(g.s.civic032.history[-1].dt,g.s.dt),"history final DT includes paid advance")
	var count_before=g.s.civic032.history.size();snapshot=Store.encode(g.s);plan.snapshot(g);plan.policy(g,"tax_head")
	check(Store.encode(g.s)==snapshot and g.s.civic032.history.size()==count_before,"preview creates no history samples")
	# Policy expense and only real expense.
	var policy=""
	for id in g.c.policies:
		if g.c.policies[id].slot=="Tax" and id!=g.s.block.policies.Tax:policy=id;break
	before=g.s.currency;var txcount=g.s.civic032.transactions.size()
	check(g.switch_policy(policy)=="","switch policy executes existing price action")
	check(near(before-g.s.currency,g.c.balance.policy_switch_price),"one policy costs once")
	check(g.s.civic032.transactions.size()==txcount+1 and near(g.s.civic032.transactions[-1].cash,-g.c.balance.policy_switch_price),"policy cost is labeled transaction")
	txcount=g.s.civic032.transactions.size();g.switch_policy(policy)
	check(g.s.civic032.transactions.size()==txcount,"click current policy creates no duplicate charge or history")
	# Pause does not accumulate simulation remainder; resume doesn't catch up frozen interval.
	g.s.settings.user_paused=true;snapshot=Store.encode(g.s);var h=g.s.local_hours
	g.advance_online(3.);var denied=g.pulse(.5)
	check(denied.advanced==0 and Store.encode(g.s)==snapshot,"manual pause blocks automatic and paid simulation without mutating remainder")
	g.update_global(g.s.global_now+600.);check(g.s.local_hours==h,"wall-clock can advance without moving city time")
	g.s.settings.user_paused=false;g.pulse(.1);check(near(g.s.local_hours,h+.1),"resume advances only requested time, no catch-up")
	# Abandonment changes actual reward eligibility, not just display.
	g=fresh();var currency=g.s.currency;var dt=g.s.dt;var chances=g.s.block_opportunities
	check(Civic.task_action(g,"housing","abandon")=="","abandon current community task")
	g.Progress.award(g,"housing","test",35,2.)
	check(not "housing" in g.s.city_goal.awards and near(g.s.currency,currency) and near(g.s.dt,dt) and g.s.block_opportunities==chances,"abandoned task does not issue resources or reward opportunities")
	check(Civic.task_rows(g).any(func(x):return x.id=="housing" and x.status=="abandoned"),"abandoned task remains available for deliberate reaccept")
	check(Civic.task_action(g,"housing","accept")=="","reaccept abandoned task")
	g.Progress.award(g,"housing","test",35,2.);Civic.sync(g);var rewarded=g.s.currency
	check("housing" in g.s.city_goal.awards and near(rewarded-currency,35),"accepted achievement rewards once")
	g.Progress.award(g,"housing","test",35,2.)
	check(near(g.s.currency,rewarded) and Civic.task_action(g,"housing","accept")!="","completed achievement cannot be farmed by reaccept")
	check(g.s.civic032.task_history.any(func(x):return x.id=="housing" and x.status=="abandoned") and g.s.civic032.task_history.any(func(x):return x.id=="housing" and x.status=="completed"),"task lifecycle history records abandonment and completion")
	check(Civic.task_action(g,"not_a_task","accept")!="","unknown task rejected")
	check(Civic.task_action(g,"route:community","abandon")=="" and not Civic.route_permitted(g),"chapter pursuit can be abandoned without deleting city")
	check(g.Progress.deliver(g)!="","delivery cannot bypass active route choice")
	check(Civic.task_action(g,"route:delivery","accept")=="" and g.s.city_goal.route=="delivery" and Civic.route_permitted(g),"alternative existing city goal can be chosen")
	# Preserve original city, already-earned rewards and unknown past timestamps.
	var file=FileAccess.get_file_as_string(Paths.file("legacy031-fixture.json"));var old=Store.decode(file)
	check(old.ok,"real 031 fixture exists and decodes")
	if old.ok:
		var saved=old.state.duplicate(true);var imported=Game.new();check(imported.migrate_state(old.state).ok,"031 imports into032")
		for key in ["projects","groups","bonus_instances","rng","quote_bank","currency","dt","city_goal","block_opportunities","block"]:
			check(Store.canonical(imported.s[key])==Store.canonical(saved[key]),"import preserves "+key)
		check(imported.s.civic032.history.is_empty(),"import doesn't reconstruct nonexistent budget history")
		check(FileAccess.get_file_as_string(Paths.file("legacy031-fixture.json"))==file,"original save bytes unchanged")
		var old_completed=saved.duplicate(true);old_completed.city_goal.completed=true;old_completed.complete=true;var done=Game.new();done.migrate_state(old_completed);Civic.sync(done)
		check(done.s.civic032.task_history.is_empty(),"imported chapter not falsely recorded as just completed")
		var startcash=imported.s.currency;var tickincome=0.;var prior_history=imported.s.civic032.history.size()
		imported.s.migration_paused=false;imported.s.event_cursor=2;imported.s.local_hours=8.9;imported.s.simulation_tick=89
		Civic.finalize(imported);imported.pulse(.1)
		check(near(imported.s.civic032.last_arrival_rate,60.),"6 arrivals in0.1h represented as batch rate with no smoothing fiction")
		var last=imported.s.civic032.history[-1]
		check(near(imported.s.civic032.last_population_rate,imported.s.civic032.last_birth_rate+imported.s.civic032.last_arrival_rate),"total change equals natural change plus arrivals")
		check(not imported.s.civic032.transactions.any(func(x):return x.kind=="arrival"),"arrival message does not misclassify operating cash as one-off income")
		var action=0.
		for x in imported.s.civic032.transactions:action+=x.cash
		check(near(imported.s.currency-startcash,last.net*last.span+action),"arrival-cycle wallet reconciles operating plus one-offs")
		g=imported
	# save and defensive types
	var decoded=Store.decode(Store.encode(g.s));check(decoded.ok,"032 history save checksum")
	var twin=Game.new();check(twin.migrate_state(decoded.state).ok,"032 history restore")
	g.pulse(.2);twin.pulse(.2);check(Store.canonical(g.s)==Store.canonical(twin.s),"restored history/task state replay deterministic")
	var bad=g.s.duplicate(true);bad.civic032.tasks.erase("food");check(g.validate_state(bad)!="","partial task map rejected before UI use")
	bad=g.s.duplicate(true);bad.civic032.history[0].balance="bad";check(g.validate_state(bad)!="","bad historical numbers rejected")
	bad=g.s.duplicate(true);bad.civic032.last_birth_rate="bad";check(g.validate_state(bad)!="","bad population rate rejected")
	# finite journals, no hard-to-see unbounded save bloat
	g=fresh()
	for i in 735:Civic.cycle(g,.1,g.s.metrics.population,0)
	check(g.s.civic032.history.size()==720 and g.s.civic032.history_trimmed,"history ring limit enforced")
	# offline recovery is a separate labeled DT entry and never city simulation.
	g=fresh();h=g.s.local_hours;var gain=g.resume_time(g.s.wall_mark+3600)
	check(gain>0 and g.s.local_hours==h and g.s.civic032.transactions[-1].kind=="offline_dt","offline DT recovery separately labeled")
	# Weighted coverage, including homeless, and distinct service dimensions.
	g=fresh();shot=Data.current(g,Planner.new()).duplicate(true)
	var original=shot.groups.values()[0].duplicate(true);var one=original.duplicate(true);var two=original.duplicate(true)
	one.count=10.;two.count=30.;two.host="homeless";one.happiness=20.;two.happiness=80.;one.education=0.;two.education=100.
	one.employment={};two.employment={};one.living={"food":1.,"material":1.,"energy":1.};two.living={"food":0.,"material":0.,"energy":0.}
	shot.groups={"one":one,"two":two};shot.metrics.service_covered={"one":{"medical":10.,"education":0.},"two":{"medical":0.,"education":30.}}
	pop=Models.population(g,shot)
	check(near(pop.count,40) and near(pop.homeless,30) and near(pop.happiness,65) and near(pop.education,75),"overall weighted population includes homeless group")
	check(near(pop.coverage_kinds.medical,.25) and near(pop.coverage_kinds.education,.75),"need coverage is per-service with correct total denominator")
	check(near(pop.coverage,.25),"current active needs overview weighted, not best-service proxy")
	var failures=checks.filter(func(x):return not x.pass).size();FileAccess.open(Paths.file("civic032.json"),FileAccess.WRITE).store_string(JSON.stringify({"passed":checks.size()-failures,"failed":failures,"checks":checks},"\t"));return 1 if failures else 0
