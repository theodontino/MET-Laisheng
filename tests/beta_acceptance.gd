extends SceneTree
## Beta integration/boundary assertions. Direct fixtures are clearly labeled;
## they do not demonstrate that a player can organically attain a theme.
const Game=preload("res://core/game.gd")
const Store=preload("res://core/storage.gd")
const Paths=preload("res://tests/paths.gd")
const MONTH=1555200.0
const YEAR=18662400.0
var checks=[]
var measurements={}
func _initialize():call_deferred("run")
func check(ok:bool,name:String,detail:String=""):
	checks.append({"pass":ok,"name":name,"detail":detail})
	print(("BETA PASS " if ok else "BETA FAIL ")+name+(" :: "+detail if detail!="" else ""))
func near(a:float,b:float,tolerance:float=.000001)->bool:return absf(a-b)<=tolerance
func fresh(seed_value:int=90501):
	var g=Game.new();g.new_game(seed_value,1000000.);g.select_block(0);g.choose_origin(g.s.origin_choices[0]);g.s.settings.user_paused=false
	return g
func clone_game(g):
	var other=Game.new();var decoded=Store.decode(Store.encode(g.s));var outcome=other.migrate_state(decoded.state)
	check(decoded.ok and outcome.ok,"same-version snapshot imports")
	return other
func numeric_snapshot_equal(a,b)->bool:
	if a is Dictionary and b is Dictionary:
		if a.size()!=b.size():return false
		for key in a:
			if not b.has(key) or not numeric_snapshot_equal(a[key],b[key]):return false
		return true
	if a is Array and b is Array:
		if a.size()!=b.size():return false
		for i in a.size():
			if not numeric_snapshot_equal(a[i],b[i]):return false
		return true
	if (a is float or a is int) and (b is float or b is int):return absf(float(a)-float(b))<=1e-12
	return a==b
func empty_tile(g)->String:
	var tiles=g.s.cells.keys();tiles.sort()
	for tile in tiles:
		if g.s.cells[tile].building=="" and not "tile.water" in g.s.cells[tile].traits:return tile
	return ""
func pending(g)->Array:
	return g.s.block.reward_choices.filter(func(row):return not row.get("claimed",false) and str(row.get("selected",""))=="")
func gameplay(g)->Dictionary:
	var result=g.s.duplicate(true)
	for key in ["wall_mark","global_now","settings"]:result.erase(key)
	result.erase("beta_time")
	return result
func stocks(g)->Dictionary:
	var out={"currency":g.s.currency,"dt":g.s.dt,"population":g.s.metrics.get("population",0.)}
	for key in ["food","material","goods"]:out[key]=g.inventory_total(key)
	return out
func clocks_and_offline():
	var g=fresh();check(g.validate_state(g.s)=="","new Beta state validates",g.validate_state(g.s))
	check(near(g.s.dt,75.),"origin never adds DT above canonical75")
	for pair in [[129600.,1.],[MONTH,12.],[YEAR,144.]]:
		check(near(g.Clock.calendar(g,pair[0]).days,pair[1]),"canonical calendar "+str(pair[0])+" seconds")
	check(near(float(g.Clock.calendar(g,YEAR).year),2.) and int(g.Clock.calendar(g,YEAR).month)==1,"144 days advances to year2 month1")
	var before=stocks(g);var report=g.advance_active(60.)
	check(near(report.local_seconds,60.) and near(g.s.beta_time.active_real_seconds,60.) and near(g.s.dt,before.dt),"normal60 advances both clocks exactly, noDT")
	g.s.settings.user_paused=true;var saved=Store.encode(g.s);g.advance_active(60.)
	check(Store.encode(g.s)==saved,"manual pause freezes gameplay and both clocks")
	var before_game=gameplay(g);var local_before=g.s.beta_time.local_elapsed_seconds;var external_before=g.s.beta_time.active_real_seconds
	var elapsed=g.resume_time(1000000.+90.*86400.)
	check(near(elapsed,90.*86400.) and near(g.s.beta_time.offline_recorded_seconds,90.*86400.),"offline90days records only duration")
	check(gameplay(g)==before_game and near(g.s.beta_time.local_elapsed_seconds,local_before) and near(g.s.beta_time.active_real_seconds,external_before),"offline does not alter resources/population/rewards/RNG/orders")
	check(near(g.resume_time(1000000.+90.*86400.),0.) and near(g.resume_time(999999.),0.),"repeated open and wall-clock rollback cannot duplicate offline credits")
	check(g.s.beta_time.mode=="normal" and g.s.settings.user_paused,"resume is paused normal mode")
	g=fresh();g.Clock.set_mode(g,"advance");report=g.advance_active(60.)
	check(near(report.local_seconds,MONTH,.0001) and near(report.active_real_seconds,60.,.000001),"advance60 real seconds produces one local month")
	check(near(report.spent,(MONTH-60.)/1244160.,.000001),"DT charges only extra local time")
	check(g.inventory_total("food")!=float(g.c.balance.initial_stock.food),"month advancement runs real resource simulation")
	measurements.advance_one_month={"state":stocks(g),"report":report}
	g=fresh();g.s.dt=.01;g.Clock.set_mode(g,"burn");before=stocks(g);report=g.advance_active(1.)
	var permitted=.01*1244160.*77760./77759.
	check(g.s.dt==0. and near(report.local_seconds,permitted,.000001),"last .01DT commits exactly affordable time")
	check(g.s.block.get("ending",{}).get("triggered",false) and g.s.settings.user_paused,"first actual zero freezes ending eligibility and pauses")
	check(g.Clock.set_mode(g,"burn")!="","DTzero refuses further acceleration")
	check(g.Block.name_block(g,"Beta边界城")=="","DTzero grants naming independently of theme")
	check(g.Block.claim_capstone(g)!="","unfinished theme never grants a Capstone")
	g.Themes.select(g,"industrial");g.s.block.theme.completed=true;g.Block.on_dt_zero(g)
	check(g.Block.claim_capstone(g)!="","completion after zero cannot retroactively grant Capstone")

func partition_and_save():
	var a=fresh(90502);var b=fresh(90502);var c=fresh(90502)
	a.advance_active(60.)
	for i in 60:b.advance_active(1.)
	for i in 10:c.advance_active(6.)
	var rows={};var max_delta=0.
	for key in stocks(a):
		var va=float(stocks(a)[key]);var vb=float(stocks(b)[key]);var vc=float(stocks(c)[key]);var delta=maxf(absf(va-vb),absf(va-vc));rows[key]=delta;max_delta=maxf(max_delta,delta)
	check(max_delta<.0001,"normal60 / 60x1 / 10x6 ledger partition tolerance<0.0001",JSON.stringify(rows))
	check(near(a.s.beta_time.local_elapsed_seconds,b.s.beta_time.local_elapsed_seconds) and a.s.rng==b.s.rng,"partition preserves clocks and gameplay random streams")
	measurements.normal_partition_absolute_differences=rows
	var draft=a.Bonus.draw(a,"integration:save",[],{"bonus_ids":["P01","P07","L05"]})
	var twin=clone_game(a)
	check(Store.encode({"rewards":twin.s.block.reward_choices,"rng":twin.s.rng})==Store.encode({"rewards":a.s.block.reward_choices,"rng":a.s.rng}),"save/load retains exact reward candidates and RNG")
	check(twin.s.beta_time.mode=="normal" and twin.s.settings.user_paused,"save/load does not restore accelerated running mode")
	check(numeric_snapshot_equal(twin.s.projects,a.s.projects) and numeric_snapshot_equal(twin.s.groups,a.s.groups) and near(twin.s.dt,a.s.dt),"save/load retains city/population/DT (JSON float tolerance1e-12)")
	var repeated=twin.Bonus.draw(twin,"integration:save",[],{"bonus_ids":["P01","P07","L05"]})
	check(Store.encode(draft)==Store.encode(repeated),"saved source cannot reroll choices")

func market_fixture():
	var g=fresh(90503);var freight=g.create_building("freight",empty_tile(g),"river",true)
	g.refresh_metrics();g.s.projects[freight.id].usage=1.
	return g
func markets():
	var throughputs=[]
	for speed in [1.,25920.,77760.]:
		var g=market_fixture();var before=g.inventory_total("goods")
		check(g.Market.order(g,"goods","buy",20.,100.)=="","valid freight buy order")
		g.Market.tick(g,60.*speed/MONTH,60.)
		throughputs.append(g.inventory_total("goods")-before)
	check(near(throughputs[0],2.) and near(throughputs[1],2.) and near(throughputs[2],2.),"M01 equal real60 seconds has2 units capacity at all local modes",str(throughputs))
	var g=market_fixture();var before=g.inventory_total("goods")+g.inventory_total("food")
	g.Market.order(g,"goods","buy",20.,100.);g.Market.order(g,"food","buy",20.,100.);g.Market.tick(g,1.,60.)
	check(near(g.inventory_total("goods")+g.inventory_total("food")-before,2.),"M02 multiple orders share one freight capacity budget")
	g=market_fixture();before=g.inventory_total("goods");g.Market.order(g,"goods","buy",20.,-999.);g.Market.tick(g,1.,60.)
	check(near(g.inventory_total("goods"),before),"unmet limit price never trades")
	g=market_fixture();g.s.currency=0.;before=g.inventory_total("goods");g.Market.order(g,"goods","buy",20.,100.);g.Market.tick(g,1.,60.)
	check(near(g.inventory_total("goods"),before),"positive-price purchase cannot exceed available funds")
	for side in ["buy","sell"]:
		g=market_fixture();g.s.projects.municipal.inventory.goods=2.;g.s.block.market_state.resources.goods.pressure=-10000.
		var quote=g.Market.quote(g,"goods");before=g.s.currency;var stock_before=g.inventory_total("goods")
		g.Market.order(g,"goods",side,1.,100. if side=="buy" else -100.);g.Market.tick(g,.01,30.)
		check(quote.buy>quote.sell and quote.buy<0. and quote.sell<0.,"M03 negative prices retain positive spread")
		check((g.s.currency>before and g.inventory_total("goods")>stock_before) if side=="buy" else (g.s.currency<before and g.inventory_total("goods")<stock_before),"negative "+side+" settles correct cash and resource directions")
	g=market_fixture();g.s.block.market_state.resources.food.pressure=10.;g.Market.tick(g,1.,900.)
	check(near(g.s.block.market_state.resources.food.pressure,10.*exp(-1.),.000001),"pressure uses900-real-second time constant, not half-life/local time")

func cycles_and_reforge():
	var g=fresh(90504);g.Themes.select(g,"industrial")
	var snapshot=Store.encode(g.s)
	check(g.Themes.reforge(g,"civic")!="" and Store.encode(g.s)==snapshot,"C01 noToken reforge is atomic rejection")
	g.s.block.theme.reforge_tokens=1;g.s.dt=7.49;snapshot=Store.encode(g.s)
	check(g.Themes.reforge(g,"civic")!="" and Store.encode(g.s)==snapshot,"C01 insufficientDT cannot consume rareToken")
	g.s.dt=75.;g.s.block.theme.steady_seconds=123.;var source=g.Bonus.draw(g,"integration:queued",[],{"bonus_ids":["P01"]})
	var prior_rewards=g.s.block.reward_choices.duplicate(true);var prior_local=g.s.beta_time.local_elapsed_seconds
	check(g.Themes.reforge(g,"civic")=="","token and7.5DT permit theme reforge")
	check(g.s.block.theme.id=="civic" and g.s.block.theme.reforge_tokens==0 and near(g.s.dt,67.5) and near(g.s.beta_time.local_elapsed_seconds,prior_local),"reforge changes theme and budget without advancing local time")
	check(g.s.block.reward_choices==prior_rewards and near(g.s.block.theme.steady_seconds,0.),"reforge preserves queued reward and discards unfinished steady progress")
	# Same-frame reward boundaries use direct fixtures to isolate queue semantics.
	g=fresh(90505);g.s.beta_time.local_elapsed_seconds=YEAR;g.s.local_hours=12.;g.s.metrics.housed=32.
	g.s.block.cycles.population_stability["32"]=MONTH-1.
	g.Bonus.draw(g,"theme:integration:M1",[],{"bonus_ids":["P01"]});g.Cycles.tick(g,1.)
	check(pending(g).size()==3,"R01 same-frame theme/population/year retain three reward records")
	var ids={}
	for reward in pending(g):ids[str(reward.get("id",reward.get("reward_id","")))]=true
	check(ids.size()==3,"three simultaneous rewards have distinct stable IDs")
	check(g.s.settings.user_paused and g.s.block.cycles.year==1,"annual boundary hard-pauses once")
	var count=g.s.block.reward_choices.size();g.s.metrics.housed=0.;g.Cycles.tick(g,1.);g.s.metrics.housed=32.;g.Cycles.tick(g,MONTH)
	check(g.s.block.reward_choices.size()==count,"R07 population drop/recovery cannot regrant threshold")
	g.s.beta_time.local_elapsed_seconds=2.*YEAR;g.s.local_hours=24.;g.Cycles.tick(g,1.)
	check(g.s.block.cycles.year==2 and g.s.block.cycles.major_cycle==1,"two-year major cycle coincides with second annual")
	var annual_count=0
	for reward in g.s.block.reward_choices:
		if str(reward.get("source","")).begins_with("annual:"):annual_count+=1
	check(annual_count==2,"major cycle does not duplicate the ordinary annual draft")

func capstone_and_restart():
	for theme in ["industrial","civic","commercial","logistics","agricultural"]:
		var g=fresh(90506);g.Themes.select(g,theme);g.s.block.theme.completed=true
		# Deliberate full-map boundary fixture: no vacant buildable cell remains.
		while empty_tile(g)!="":g.create_building("homestead",empty_tile(g),"commons",true)
		var count=g.playable_ids().size();g.s.dt=0.;g.Block.on_dt_zero(g)
		check(g.Block.claim_capstone(g)=="" and g.s.block.function_role==theme,"C04 full-map instantCapstone "+theme)
		check(g.playable_ids().size()==count and g.s.block.ending.capstone_claimed,"landmark does not demolish or replace player building")
		var saved=Store.encode(g.s)
		check(g.Block.claim_capstone(g)!="" and Store.encode(g.s)==saved,"Capstone is exactly once "+theme)
		if theme!="industrial":continue
		g.Block.name_block(g,"验收留档城");var archive=g.Block.archive(g)
		check(archive.get("ok",false) and archive.record.get("read_only",false),"C05 completed city creates durable read-only record")
		g.new_game(90507,2000000.);g.select_block(0);g.choose_origin(g.s.origin_choices[0])
		check(g.s.block.bonus_wallet.is_empty() and g.s.block.get("blueprint_levels",{}).is_empty() and g.s.block.get("capstone",{}).is_empty() and g.s.bonus_instances.is_empty() and near(g.s.dt,75.),"independent new game inherits no cards,upgrades,landmark,DT")
		check(FileAccess.file_exists(str(archive.path)),"old city record survives independent restart")
func run():
	clocks_and_offline();partition_and_save();markets();cycles_and_reforge();capstone_and_restart()
	var failed=checks.filter(func(row):return not row.pass)
	var report={"evidence_type":"automated_explicit_fixtures_and_integration","passed":failed.is_empty(),"checks":checks,"measurements":measurements,"limitations":["Boundary fixtures are not player reachability evidence","No human playtest or hardware FPS claim"]}
	var file=FileAccess.open(Paths.file("beta_acceptance.json"),FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close()
	print("BETA ACCEPTANCE ",checks.size()-failed.size(),"/",checks.size())
	quit(0 if failed.is_empty() else 1)
