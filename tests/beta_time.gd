extends SceneTree
## Focused live-simulator checks for clock units, fractional time and external trade.
## Ready freight is a unit-test fixture, not a claimed normal-play route.
const Game=preload("res://core/game.gd")
const Store=preload("res://core/storage.gd")
var failures:Array=[]
var checks=0

func check(ok:bool,label:String) -> void:
	checks+=1
	if not ok:failures.append(label);printerr("FAIL "+label)

func near(a:float,b:float,tolerance:float=1e-8) -> bool:
	return absf(a-b)<=tolerance*maxf(1,absf(b))

func fresh():
	var g=Game.new()
	g.new_game(2041,1000.0);g.select_block(0);g.choose_origin(str(g.s.origin_choices[0]))
	g.s.settings.user_paused=false
	return g

func freight(g) -> void:
	for tile in g.s.cells:
		if g.s.cells[tile].building=="" and "tile.water" not in g.s.cells[tile].traits:
			g.create_building("freight",tile,"steel",true)
			break
	g.refresh_metrics()

func partition_checks() -> void:
	for mode in ["normal","advance"]:
		var whole=fresh()
		whole.Clock.advance(whole,60,mode)
		for pieces in [10,60]:
			var split=fresh()
			for i in pieces:split.Clock.advance(split,60.0/pieces,mode)
			var largest=0.0
			var comparisons={"currency":[whole.s.currency,split.s.currency],"population":[whole.s.metrics.population,split.s.metrics.population]}
			for resource in ["food","material","goods"]:comparisons[resource]=[whole.inventory_total(resource),split.inventory_total(resource)]
			for values in comparisons.values():largest=maxf(largest,absf(float(values[0])-float(values[1]))/maxf(1,absf(float(values[0]))))
			print("PARTITION ",mode," pieces=",pieces," largest_relative_economy_error=",largest)
			# The engine recomputes utilization at segment starts. Civil-day Euler
			# integration has bounded truncation error, not bitwise partition identity.
			var tolerance=1e-9 if mode=="normal" else 1e-4
			check(largest<tolerance,"partition economy "+mode+"/"+str(pieces)+" below "+str(tolerance))
			check(near(whole.s.dt,split.s.dt,1e-11) and near(whole.s.beta_time.local_elapsed_seconds,split.s.beta_time.local_elapsed_seconds,1e-11),"partition clocks and exact DT "+mode+"/"+str(pieces))

func coincident_end_check() -> void:
	var g=fresh()
	g.Clock.advance(g,1,"normal")
	g.Themes.select(g,"industrial")
	# Boundary fixture: a saved city's first two milestones and almost two
	# months of verified M3 stability. The last receipt is a real live Sim tick.
	g.s.block.theme.stage=3
	g.s.block.theme.milestone_ids=["industrial:M1","industrial:M2"]
	g.s.projects.municipal.inventory.goods=18.0
	check(g.Themes.deliver(g,"material",24)=="" and g.Themes.deliver(g,"goods",18)=="","final-boundary fixture delivers material and goods through actual inventory receipts")
	var final_local=.01*1244160.0*77760.0/(77760.0-1.0)
	g.s.block.theme.steady_seconds=2.0*1555200.0-final_local
	g.s.dt=.01
	var r=g.Clock.advance(g,1,"burn")
	print("COINCIDENT ",JSON.stringify({"result":r,"dt":g.s.dt,"expected":final_local,"theme":g.s.block.theme.completed,"steady":g.s.block.theme.steady_seconds}))
	check(g.s.dt==0 and near(r.local_seconds,final_local,1e-11),"coincident completion commits exact final affordable time")
	check(g.s.block.theme.completed and g.s.block.get("ending",{}).get("eligible_theme","")=="industrial","theme finishing on final DT segment qualifies before ending freezes eligibility")
	if not g.s.block.has("ending"):return
	var count=g.s.block.reward_choices.size()
	var restored=Game.new();var decoded=Store.decode(Store.encode(g.s));var loaded=restored.migrate_state(decoded.state)
	check(loaded.ok and restored.s.block.theme.completed and restored.s.block.ending.eligible_theme=="industrial","coincident ending and theme qualification survive same-version save/load")
	restored.Clock.resume(restored,1000+90*86400)
	restored.Block.on_dt_zero(restored)
	check(restored.s.block.reward_choices.size()==count,"loading offline at ending does not duplicate reward records")
	check(restored.s.beta_time.pause_reason=="dt_zero" and restored.s.settings.user_paused,"resume preserves mandatory ending presentation")

func annual_pause_check() -> void:
	var g=fresh()
	var r=g.Clock.advance(g,1440,"advance")
	check(near(r.local_seconds,18662400) and int(g.s.block.cycles.year)==1,"large advance hard-stops at first annual decision")
	check(g.s.settings.user_paused and g.s.beta_time.pause_reason=="annual","annual decision requires explicit resume")
	var after=JSON.stringify(g.s)
	r=g.Clock.advance(g,60)
	check(r.local_seconds==0 and JSON.stringify(g.s)==after,"repeated live pump while annual is pending has no gameplay effect")
	g.Clock.resume(g,2000)
	check(g.s.beta_time.pause_reason=="annual" and g.s.beta_time.mode=="normal","offline return preserves pending annual decision and disables acceleration")

func spatial_cache_checks() -> void:
	var g=fresh()
	g.Clock.advance(g,1,"normal")
	g.Clock.advance(g,1,"normal")
	check(g.spatial_cache.get("hit",false),"unchanged spatial inputs reuse source-aware access snapshot")
	var source=Store.decode(Store.encode(g.s)).state
	var cold=Game.new();cold.migrate_state(source);cold.s.settings.user_paused=false
	var warm=Game.new();warm.migrate_state(source);warm.s.settings.user_paused=false
	warm.Sim.spatial_snapshot(warm,warm.compiled)
	cold.Clock.advance(cold,1,"normal");warm.Clock.advance(warm,1,"normal")
	check(Store.encode(cold.s)==Store.encode(warm.s),"cold/warm spatial cache produces exactly equal complete committed states")
	check(not cold.s.has("spatial_cache") and not Store.encode(cold.s).contains("spatial_cache"),"cache is absent from saved gameplay state")
	var id=str(g.playable_ids()[0]);var original=JSON.stringify(g.s)
	g.Sim.spatial_snapshot(g,g.compiled)
	check(JSON.stringify(g.s)==original,"spatial snapshot lookup is read-only for gameplay state and RNG")
	g.s.projects[id].paused=true
	var access=g.Sim.spatial_snapshot(g,g.compiled)
	check(not g.spatial_cache.hit and access==g.Fields.access(g,g.compiled),"pause invalidates spatial cache with exact uncached parity")
	g.s.projects[id].paused=false;g.s.projects[id].status="construction"
	access=g.Sim.spatial_snapshot(g,g.compiled)
	check(not g.spatial_cache.hit and access==g.Fields.access(g,g.compiled),"construction status invalidates spatial cache")
	g.s.projects[id].status="running";g.refresh_metrics()
	g.Sim.spatial_snapshot(g,g.compiled)
	g.compiled.stats[id].service_range=float(g.compiled.stats[id].service_range)+1
	access=g.Sim.spatial_snapshot(g,g.compiled)
	check(not g.spatial_cache.hit and access==g.Fields.access(g,g.compiled),"changed upgrade/rule spatial stats invalidate cache")
	var field_id=g.define_field(id,"medical",.3,1,.2)
	access=g.Sim.spatial_snapshot(g,g.compiled)
	check(not g.spatial_cache.hit and access==g.Fields.access(g,g.compiled) and access.fields.has(field_id),"authored source fields invalidate cache and preserve source identity")
	var gid=str(g.s.groups.keys()[0]);var host=str(g.s.groups[gid].host)
	g.compiled.config[host].volunteer_education={"threshold":16.0,"capacity_per_pop":1.0,"labor_mult":1.0}
	g.s.groups[gid].education=15
	g.Sim.spatial_snapshot(g,g.compiled)
	g.s.groups[gid].education=20
	access=g.Sim.spatial_snapshot(g,g.compiled)
	check(not g.spatial_cache.hit and access==g.Fields.access(g,g.compiled),"volunteer education threshold invalidates cache")
	var population_field=g.define_field(gid,"medical",.4,1,.2)
	g.Sim.spatial_snapshot(g,g.compiled)
	for other in g.playable_ids():
		if str(other)!=host:g.s.groups[gid].host=other;break
	access=g.Sim.spatial_snapshot(g,g.compiled)
	check(not g.spatial_cache.hit and access==g.Fields.access(g,g.compiled) and access.fields.has(population_field),"population source relocation invalidates spatial cache")

func _initialize() -> void:
	var g=fresh()
	check(near(g.s.dt,75),"all origins start with exactly 75 DT")
	check(g.Clock.calendar(g,129600).day==2,"one Laisheng day = 129600 seconds")
	check(g.Clock.calendar(g,1555200).month==2,"one Laisheng month = 1555200 seconds")
	check(g.Clock.calendar(g,18662400).year==2,"one Laisheng year = 18662400 seconds")
	var before_dt=float(g.s.dt)
	var original_state:Dictionary=g.s
	var original_encoded=Store.encode(original_state)
	var r=g.Clock.advance(g,0.01,"normal")
	check(Store.encode(original_state)==original_encoded,"successful Sim commit leaves retained rollback source immutable")
	check(near(r.local_seconds,.01,1e-12) and float(g.s.local_hours)>0,"subsecond normal time is not rounded away")
	check(near(g.s.dt,before_dt),"normal flow never consumes DT")
	check(g.last_committed_revision==g.revision,"successful live tick marks HUD data as committed")
	g.Clock.set_mode(g,"burn")
	check(g.last_committed_revision!=g.revision,"time-mode command dirties forecast before next tick")
	g.Clock.advance(g,.01)
	check(g.last_committed_revision==g.revision,"next successful commit clears command forecast marker")
	g.Clock.set_mode(g,"normal")
	before_dt=float(g.s.dt)
	var saved=Store.decode(Store.encode(g.s))
	check(saved.ok and near(saved.state.local_hours,g.s.local_hours,1e-15),"save preserves fractional economic month")
	var snapshot=JSON.stringify(g.s)
	g.s.settings.user_paused=true
	var paused=JSON.stringify(g.s)
	r=g.Clock.advance(g,60,"normal")
	check(r.local_seconds==0 and JSON.stringify(g.s)==paused,"paused clock freezes all gameplay")
	g.s.settings.user_paused=false
	var resources={"food":g.inventory_total("food"),"material":g.inventory_total("material"),"goods":g.inventory_total("goods")}
	var cash=float(g.s.currency);var people=float(g.s.metrics.population);var rng=g.s.rng.duplicate(true)
	var clock=float(g.s.beta_time.local_elapsed_seconds)
	var offline=g.Clock.resume(g,1000+90*86400)
	check(near(offline,90*86400),"90 offline days recorded")
	check(g.Clock.resume(g,1000+90*86400)==0 and g.Clock.resume(g,900)==0,"offline record is idempotent and rollback-safe")
	check(g.s.beta_time.local_elapsed_seconds==clock and g.s.currency==cash and g.s.dt==before_dt and g.s.metrics.population==people and g.s.rng==rng,"offline creates no money time DT population or RNG draws")
	check(g.inventory_total("food")==resources.food and g.inventory_total("material")==resources.material and g.inventory_total("goods")==resources.goods,"offline leaves inventories untouched")
	check(g.s.settings.user_paused and g.s.beta_time.mode=="normal","return always normal and paused")
	g=fresh();r=g.Clock.advance(g,60,"advance")
	check(near(r.local_seconds,1555200) and near(g.s.local_hours,1),"60 real seconds advance exactly one month")
	check(near(r.spent,(1555200.0-60.0)/1244160.0),"DT charges extra local time only")
	check(near(g.s.beta_time.active_real_seconds,60),"external active time remains real seconds")
	g=fresh();g.s.dt=.01;r=g.Clock.advance(g,60,"burn")
	var expected_real=.01*1244160.0/(77760.0-1.0)
	check(g.s.dt==0 and near(r.active_real_seconds,expected_real,1e-11),"last .01 DT submits only affordable real segment")
	check(near(r.local_seconds,expected_real*77760.0,1e-11),"DT-zero local time has no unbilled tail")
	check(g.s.block.ending.triggered and g.s.settings.user_paused,"true zero opens ending and hard pauses")
	g=fresh();snapshot=JSON.stringify(g.s);g.c.balance.head_tax=INF
	r=g.Clock.advance(g,1,"normal")
	check(r.local_seconds==0 and r.spent==0 and not str(r.reason).is_empty(),"invalid commit charges no time or DT")
	check(JSON.stringify(g.s)==snapshot,"invalid commit restores full gameplay state")
	g=fresh();freight(g)
	check(near(g.Market._total_capacity(g),120),"one standard freight =120 units per real hour")
	g.s.block.market_state.resources.food.pressure=10.0
	g.Market.tick(g,.1,900)
	check(near(g.s.block.market_state.resources.food.pressure,10.0*exp(-1)),"market natural pressure uses 900 real-second time constant")
	g.s.projects.municipal.inventory.food=20
	check(g.Market.order(g,"food","sell",10,-100)=="","freight accepts real sell order")
	g.Market.tick(g,.1,60)
	check(near(g.s.block.trade_orders[0].filled,2),"60 seconds creates two units of trade throughput")
	check(g.Market.quote(g,"food").buy_price>g.Market.quote(g,"food").sell_price,"spread preserves buy greater than sell")
	g=fresh();freight(g);g.s.block.market_state.resources.food.pressure=-100000
	g.s.projects.municipal.inventory.food=0
	check(g.Market.order(g,"food","buy",1,100)=="","negative-price buy allowed")
	cash=g.s.currency;g.Market.tick(g,.1,30)
	check(g.s.currency>cash and near(g.inventory_total("food"),1),"negative-price import pays recipient and adds stock")
	g.s.block.market_state.resources.food.pressure=-100000
	check(g.Market.order(g,"food","sell",1,-100)=="","negative-price export allowed")
	cash=g.s.currency;g.Market.tick(g,.1,30)
	check(g.s.currency<cash and near(g.inventory_total("food"),0),"negative-price export pays disposal and removes stock")
	partition_checks()
	coincident_end_check()
	annual_pause_check()
	spatial_cache_checks()
	print("BETA_TIME ",checks-failures.size(),"/",checks," passed")
	quit(0 if failures.is_empty() else 1)
