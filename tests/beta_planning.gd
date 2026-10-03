extends SceneTree
## Preview-cache regressions exercise real market/orders/withdrawals and compare
## the actual one-step receipts; neither a preview nor a cache hit may mutate play.
const Game=preload("res://core/game.gd")
const Planning=preload("res://core/planning.gd")
const Store=preload("res://core/storage.gd")
var checks=0
var failures=0
func _initialize() -> void:call_deferred("_run")
func check(ok:bool,label:String) -> void:
	checks+=1
	if not ok:failures+=1
	print(("BETA PLANNING PASS " if ok else "BETA PLANNING FAIL ")+label)
func near(a:float,b:float) -> bool:return absf(a-b)<0.000001
func fresh():
	var g=Game.new();g.new_game(16835,1000000.);g.select_block(0);g.choose_origin(str(g.s.origin_choices[0]))
	g.s.settings.user_paused=false
	for tile in g.Rules.ids(g.s.cells):
		if g.s.cells[tile].building=="" and not "tile.water" in g.s.cells[tile].traits:
			g.create_building("freight",tile,"steel",true);break
	g.refresh_metrics()
	g.Clock.advance(g,1,"normal")
	return g
func imports(shot:Dictionary,resource:String="food") -> float:
	return float(shot.get("audit",{}).get("resources",{}).get(resource,{}).get("imported",0))
func stock(shot:Dictionary,resource:String="food") -> float:
	var total=0.
	for p in shot.projects.values():total+=float(p.get("inventory",{}).get(resource,0))
	return total
func read_only_preview(planner,g) -> Dictionary:
	var before=Store.encode(g.s)
	var shot=planner.snapshot(g)
	check(str(shot.get("error",""))=="","one-step preview commits valid simulation only in clone")
	check(Store.encode(g.s)==before,"forecast preserves player stock orders RNG and full saved state")
	return shot
func _run() -> void:
	var g=fresh();var planner=Planning.new()
	var no_order=read_only_preview(planner,g)
	check(near(imports(no_order),0),"no order forecast contains no imported food")
	check(g.Market.order(g,"food","buy",10,100)=="","real market API accepts test import")
	var live_tick=int(g.s.simulation_tick)
	var normal=read_only_preview(planner,g)
	check(imports(normal)>1 and int(g.s.simulation_tick)==live_tick,"placing order changes real forecast import before any live tick")
	check(g.Clock.set_mode(g,"burn")=="","real clock API switches to burn without time advance")
	var burn=read_only_preview(planner,g)
	check(imports(burn)>=0 and imports(burn)<imports(normal)*.01,"switching to burn immediately lowers per-local-step external receipts")
	check(str(burn.snapshot_id)!=str(normal.snapshot_id),"mode-specific forecast does not reuse normal snapshot")
	check(g.Clock.set_mode(g,"normal")=="","switch back to normal succeeds")
	var normal_again=read_only_preview(planner,g)
	check(near(imports(normal_again),imports(normal)),"switching back restores full normal external throughput forecast")
	var order_id=str(g.s.block.trade_orders.back().id)
	check(g.Market.cancel(g,order_id)=="","real market API cancels unfilled order")
	var cancelled=read_only_preview(planner,g)
	check(near(imports(cancelled),0),"cancelling order immediately removes forecast delivery")
	var cached_version=planner.version(g)
	var before_cached=Store.encode(g.s)
	var cached=planner.snapshot(g)
	check(str(cached.snapshot_id)==str(cancelled.snapshot_id) and planner.version(g)==cached_version and Store.encode(g.s)==before_cached,"unchanged city reuses its snapshot without invalidation or mutation")

	# Current-stock mutation without a rule refresh must still invalidate planning.
	var cash=float(g.s.currency)
	var revision=int(g.revision)
	check(g.withdraw_stock("food",5),"inventory withdrawal uses real game inventory path")
	var withdrawn=read_only_preview(planner,g)
	check(int(g.revision)==revision and near(float(g.s.currency),cash) and stock(withdrawn)<stock(cancelled)-4.9,"same-revision inventory withdrawal changes forecast by actual resource loss")

	# Theme delivery is a one-off command. Future flow must not repeat its loss.
	check(g.Themes.select(g,"agricultural")=="","select agriculture through public API")
	g.s.block.theme.stage=3
	g.s.block.theme.milestone_ids=["agricultural:M1","agricultural:M2"]
	var before_delivery=read_only_preview(planner,g)
	check(g.Themes.deliver(g,"food",5)=="","theme command really delivers five food")
	var after_delivery=read_only_preview(planner,g)
	check(stock(after_delivery)<stock(before_delivery)-4.9,"theme delivery refreshes predicted inventory before next live tick")
	check(near(float(after_delivery.audit.resources.food.get("commission_delivered",0)),0),"next-step prediction never repeats completed commission delivery")
	check(near(float(after_delivery.metrics.flows.food),float(before_delivery.metrics.flows.food)),"one-time delivery is not extrapolated as sustained food consumption")

	# Paid event can change future affordability without a settled simulation tick.
	g.Themes.ensure(g)
	g.s.block.theme.events.append({"id":"agricultural_review","theme":"agricultural","status":"offered"})
	g.s.currency=60.
	g.Market.order(g,"food","buy",30,100)
	var before_payment=read_only_preview(planner,g)
	check(g.Themes.choose_event(g,"agricultural_review","accept")=="","paid event uses actual currency command")
	var after_payment=read_only_preview(planner,g)
	check(imports(after_payment)<imports(before_payment),"event payment immediately reduces affordable forecast imports")

	# A Planning object reused after an independent new game must not alias its old
	# same-tick and same-revision cache, even with the same map/building count.
	var other=fresh()
	other.revision=g.revision
	other.s.simulation_tick=g.s.simulation_tick
	var other_shot=read_only_preview(planner,other)
	check(near(imports(other_shot),0),"different live game cannot reuse prior city's cached order forecast")
	print("BETA PLANNING RESULT %d/%d passed"%[checks-failures,checks])
	quit(1 if failures else 0)
