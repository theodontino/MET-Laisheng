extends SceneTree
## Boundary fixtures exercise committed simulation, lifecycle persistence and
## gameplay outcomes. They are not a claim of a balanced normal-play route.
const Game = preload("res://core/game.gd")
const Store = preload("res://core/storage.gd")
const MONTH = 1555200.0
var checks = 0
var failures = 0

func _initialize() -> void:
	call_deferred("_run")

func check(ok:bool, label:String) -> void:
	checks += 1
	if not ok: failures += 1
	print(("BETA LIFECYCLE PASS " if ok else "BETA LIFECYCLE FAIL ") + label)

func near(a:float,b:float,tolerance:float=0.000001) -> bool:
	return absf(a-b) <= tolerance * maxf(1,absf(b))

func fresh():
	var g=Game.new()
	g.new_game(41965,1000000.)
	g.select_block(0)
	g.choose_origin(str(g.s.origin_choices[0]))
	g.s.settings.user_paused=false
	return g

func empty_tile(g) -> String:
	for tile in g.Rules.ids(g.s.cells):
		if str(g.s.cells[tile].building)=="" and not "tile.water" in g.s.cells[tile].traits:return str(tile)
	return ""

func add_building(g,kind:String) -> String:
	var p:Dictionary=g.create_building(kind,empty_tile(g),"commons",true)
	return str(p.id)

func freeze_births(g) -> void:
	g.c.balance.birth_per_hour=0.
	g.s.migration_paused=true

func prepare_agriculture():
	var g=fresh()
	freeze_births(g)
	g.s.projects.municipal.inventory.food=150.
	g.refresh_metrics()
	g.Clock.advance(g,1.,"normal")
	g.Themes.select(g,"agricultural")
	g.s.block.theme.stage=3
	g.s.block.theme.milestone_ids=["agricultural:M1","agricultural:M2"]
	check(g.Themes.deliver(g,"food",50.)=="","agriculture boundary fixture pays final 50 food through delivery API")
	return g

func ending_boundary_suite() -> void:
	var g=prepare_agriculture()
	var final_dt=.01
	var speed=g.Clock.acceleration(g,"burn")
	var final_seconds=final_dt*float(g.c.beta.time.earth_seconds_per_dt)*speed/(speed-1.)
	g.s.block.theme.steady_seconds=2.*MONTH-final_seconds
	g.s.dt=final_dt
	var result=g.Clock.advance(g,1.,"burn")
	check(int(result.steps)>0 and near(float(result.local_seconds),final_seconds,1e-11),"agricultural final affordable segment commits actual simulation")
	check(float(g.s.dt)==0. and bool(g.s.block.theme.completed),"agricultural hold completes on the same segment as DT exhaustion")
	check(str(g.s.block.get("ending",{}).get("eligible_theme",""))=="agricultural","final-segment completion qualifies for agriculture landmark")
	check(g.Block.claim_capstone(g)=="","qualified final-segment city can claim its landmark")
	var restored=Game.new()
	var decoded=Store.decode(Store.encode(g.s))
	var loaded=restored.migrate_state(decoded.state)
	check(bool(loaded.ok) and str(restored.s.block.ending.eligible_theme)=="agricultural" and bool(restored.s.block.ending.capstone_claimed),"final-segment entitlement and claimed landmark survive save reload")

	var late=prepare_agriculture()
	late.s.block.theme.steady_seconds=2.*MONTH-final_seconds-1.
	late.s.dt=final_dt
	late.Clock.advance(late,1.,"burn")
	check(float(late.s.dt)==0. and not bool(late.s.block.theme.completed) and str(late.s.block.ending.eligible_theme)=="","unfinished agriculture at exhaustion receives naming but no landmark eligibility")
	var ending_snapshot=Store.encode(late.s.block.ending)
	late.s.settings.user_paused=false
	var continuation=late.Clock.advance(late,2.,"normal")
	check(float(continuation.local_seconds)>0. and bool(late.s.block.theme.completed),"city can finish its remaining theme by continuing at normal speed")
	check(str(late.s.block.ending.eligible_theme)=="" and late.Block.claim_capstone(late)!="","completion after exhaustion does not retroactively award landmark")
	check(Store.encode(late.s.block.ending)==ending_snapshot,"late completion leaves immutable exhaustion record intact")
	check(bool(late.s.block.naming_rights),"late-completion city still retains universal naming right")

func population_endpoint_suite() -> void:
	var g=fresh()
	freeze_births(g)
	add_building(g,"homestead")
	g.refresh_metrics()
	for group in g.s.groups.values():group.count=26.
	g.Population.rehouse(g)
	g.refresh_metrics()
	g.s.migration_paused=false
	# Place the saved city one second before the scheduled third-month arrival.
	g.s.beta_time.local_elapsed_seconds=3.*MONTH-1.
	g.s.local_hours=float(g.s.beta_time.local_elapsed_seconds)/MONTH
	g.s.event_cursor=0
	check(float(g.s.metrics.housed)<32.,"arrival fixture begins below first stable-housing threshold")
	var result=g.Clock.advance(g,1.,"normal")
	check(near(float(result.local_seconds),1.) and float(g.s.metrics.housed)>=32.,"real Local-Time migration crosses housing threshold at segment end")
	check(not "32" in g.s.block.cycles.population_awards and near(float(g.s.block.cycles.population_stability.get("32",0)),0.),"newly housed arrivals earn none of preceding segment stability")
	g.s.migration_paused=true
	g.s.settings.user_paused=false
	# Cohort then actually remains housed for a full month of normal simulation.
	var held=g.Clock.advance(g,MONTH-1.,"normal")
	check(float(held.local_seconds)>0. and not "32" in g.s.block.cycles.population_awards,"one second short of full stable month yields no population reward")
	g.s.settings.user_paused=false
	g.Clock.advance(g,1.,"normal")
	check("32" in g.s.block.cycles.population_awards,"full month after actual arrival grants population reward")
	var awards=0
	for reward in g.s.block.reward_choices:
		if str(reward.source)=="population:32":awards+=1
	check(awards==1,"stable housing creates exactly one persistent reward record")

func fill_map(g) -> void:
	while empty_tile(g)!="":add_building(g,"homestead")
	g.refresh_metrics()

func numeric_landmark_suite() -> void:
	var cases=[
		{"theme":"industrial","kind":"workshop","field":"output.goods","factor":1.12,"other":"personnel_mult","other_factor":1.08},
		{"theme":"civic","kind":"medical_clinic","field":"service.medical","factor":1.15},
		{"theme":"commercial","kind":"commercial_center","field":"service.commercial","factor":1.15},
		{"theme":"logistics","kind":"freight","field":"trade_capacity","factor":1.25,"other":"storage.material","other_factor":1.2},
		{"theme":"agricultural","kind":"agriculture","field":"output.food","factor":1.15}]
	for scenario in cases:
		var g=fresh()
		var target=add_building(g,str(scenario.kind))
		fill_map(g)
		var before:Dictionary=g.compiled.stats[target].duplicate(true)
		var municipal_capacity=float(g.compiled.stats.municipal["storage.food"])
		var cells=Store.encode(g.s.cells)
		var projects=g.s.projects.keys()
		g.Themes.select(g,str(scenario.theme))
		g.s.block.theme.completed=true
		g.s.dt=0.
		g.Block.on_dt_zero(g)
		check(g.Block.claim_capstone(g)=="","full map still receives "+str(scenario.theme)+" landmark")
		check(Store.encode(g.s.cells)==cells and g.s.projects.keys()==projects,"landmark never evicts a built city tile: "+str(scenario.theme))
		check(near(float(g.compiled.stats[target][scenario.field]),float(before[scenario.field])*float(scenario.factor)),"landmark applies advertised numeric effect once: "+str(scenario.theme))
		if scenario.has("other"):
			check(near(float(g.compiled.stats[target][scenario.other]),float(before[scenario.other])*float(scenario.other_factor)),"landmark secondary numeric effect is real: "+str(scenario.theme))
		if str(scenario.theme)=="commercial":check(near(float(g.compiled.stats[target].get("commercial_tax_multiplier",1)),1.08),"commercial landmark exposes a real 1.08 tax coefficient")
		if str(scenario.theme)=="agricultural":check(near(float(g.compiled.stats.municipal["storage.food"]),municipal_capacity*1.25),"agriculture storage benefit includes off-map municipal reserves")
		var named_sources=0
		for row in g.compiled.trace[target][scenario.field].rules:
			if str(row.source)==str(g.s.block.capstone.name):named_sources+=1
		check(named_sources==1,"numeric effect traces to one named landmark source: "+str(scenario.theme))
		var final_stats=Store.encode(g.compiled.stats)
		check(g.Block.claim_capstone(g)!="" and Store.encode(g.compiled.stats)==final_stats,"repeat claim adds no effect or reward: "+str(scenario.theme))
		var restored=Game.new()
		var loaded=restored.migrate_state(Store.decode(Store.encode(g.s)).state)
		check(bool(loaded.ok) and near(float(restored.compiled.stats[target][scenario.field]),float(g.compiled.stats[target][scenario.field])) and restored.Block.claim_capstone(restored)!="","save reload preserves one landmark without replay: "+str(scenario.theme))

func corrupt_archive_suite() -> void:
	var g=fresh()
	g.s.dt=0.
	g.Block.on_dt_zero(g)
	var id="beta-lifecycle-corrupt-"+str(Time.get_ticks_usec())
	g.s.block.ending.archive_id=id
	var path="user://city_records/"+id+".json"
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("user://city_records"))
	var file=FileAccess.open(path,FileAccess.WRITE)
	check(file!=null,"archive fault fixture can create isolated corrupt destination")
	if file==null:return
	var corrupt_text=JSON.stringify({"id":id,"read_only":false,"city_state":"corrupt payload"})
	file.store_string(corrupt_text)
	file.close()
	var city=Store.encode(g.s)
	var result=g.Block.archive(g)
	check(not bool(result.get("ok",false)),"existing corrupt city record refuses archival success")
	check(Store.encode(g.s)==city,"failed archive preserves complete playable city and ending state")
	check(FileAccess.get_file_as_string(path)==corrupt_text,"failed archive preserves corrupt evidence rather than overwriting it")
	DirAccess.remove_absolute(ProjectSettings.globalize_path(path))

func _run() -> void:
	ending_boundary_suite()
	population_endpoint_suite()
	numeric_landmark_suite()
	corrupt_archive_suite()
	print("BETA LIFECYCLE RESULT %d/%d passed"%[checks-failures,checks])
	quit(1 if failures else 0)
