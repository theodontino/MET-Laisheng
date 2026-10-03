extends SceneTree
const Game=preload("res://core/game.gd")
const Feedback=preload("res://core/feedback.gd")
const Overlay=preload("res://ui/beta_feedback.gd")
const Map=preload("res://ui/map_view.gd")
const Store=preload("res://core/storage.gd")
var checks:Array=[]
func check(ok:bool,label:String) -> void:
	checks.append({"pass":ok,"name":label});print(("PASS " if ok else "FAIL ")+label)
func step(g,amount:float=.03) -> void:
	g.s.simulation_tick+=1;g.s.local_hours+=amount
func _initialize() -> void:call_deferred("run")
func run() -> void:
	var g=Game.new();g.new_game(77,1000000);g.select_block(0);g.choose_origin(g.s.origin_choices[0])
	var ids=g.playable_ids();var id=str(ids[0]);var id2=str(ids[1]) if ids.size()>1 else id
	g.s.projects[id].status="running";g.s.projects[id].paused=false
	g.s.metrics.activity_ledger={id:[]};g.s.metrics.utilization={id:{"labor":1.,"energy":.9,"resources":1.,"utilization":.9}}
	var f=Feedback.new();var before=Store.encode(g.s);var result=f.refresh(g,1)
	check(result.alerts.is_empty(),"yellow holds in local time before appearing")
	for n in 20:f.refresh(g,1+n*10)
	check(f.view(201).alerts.is_empty(),"UI real time cannot satisfy a local alert hold")
	check(Store.encode(g.s)==before,"repeated presentation leaves wallet entities and RNG byte identical")
	step(g);result=f.refresh(g,202)
	check(result.alerts.size()==1 and result.main.cause=="energy","committed energy bottleneck appears after local hold")
	g.s.metrics.utilization[id].energy=.965;g.s.metrics.utilization[id].utilization=.965;step(g);result=f.refresh(g,203)
	check(result.alerts.size()==1,"hysteresis retains warning between 95 and 98 percent")
	g.s.metrics.utilization[id].energy=.99;g.s.metrics.utilization[id].utilization=.99;step(g);result=f.refresh(g,204)
	check(result.alerts.is_empty(),"warning clears above exit threshold")
	g.s.metrics.utilization[id]={"labor":0.,"energy":0.,"resources":0.,"utilization":0.};step(g)
	result=f.refresh(g,205,{"forecast":true})
	check(result.alerts.is_empty(),"forecast never generates a hard alarm")
	result=f.refresh(g,206)
	check(result.alerts.size()==2 and result.alerts.all(func(row):return row.severity==2),"critical building is capped at main plus one critical bottleneck")
	g.s.metrics.utilization[id]={"labor":1.,"energy":.8,"resources":1.,"utilization":.8}
	if id2!=id:
		g.s.projects[id2].status="running";g.s.projects[id2].paused=false
		g.s.metrics.utilization[id2]={"labor":1.,"energy":.8,"resources":1.,"utilization":.8}
	step(g);result=f.refresh(g,207)
	check(result.summaries.size()==1 and result.main.count==result.alerts.size(),"shared power cause has one city summary and local icons")
	g.s.metrics.utilization[id]={"labor":1.,"energy":1.,"resources":1.,"utilization":1.};g.s.metrics.utilization.erase(id2)
	g.s.metrics.activity_ledger[id]=[{"kind":"output","resource":"food","rate":10.,"lost":0.}];step(g);result=f.refresh(g,208)
	check(result.alerts.is_empty(),"full warehouse alone cannot invent actual overflow")
	g.s.metrics.activity_ledger[id][0].lost=2.;step(g);result=f.refresh(g,209)
	check(result.alerts.size()==1 and result.main.evidence.actual_lost==2.,"storage alert retains actual committed loss evidence")
	# A cheap external clock alone is not an alarm; a real deficit and pending order are required.
	g.compiled.stats[id].trade_capacity=8.;g.s.metrics.activity_ledger[id]=[]
	g.s.audit={"hours":.1,"resources":{"food":{"produced":0.,"consumed":1.,"imported":0.}}}
	g.s.metrics.requested={"food":10.}
	g.s.block.trade_orders=[{"id":"T-fixture","resource":"food","side":"buy","status":"open","remaining":20.,"limit_price":.1}]
	for p in g.s.projects.values():p.inventory.food=0.
	step(g);result=f.refresh(g,220)
	check(result.alerts.any(func(row):return row.cause=="import_food" and row.evidence.actual_imports_per_month==0),"blocked import cannot count as guaranteed supply in a stock warning")
	g.s.audit.resources.food.produced=2.;step(g);result=f.refresh(g,221)
	check(not result.alerts.any(func(row):return str(row.cause).begins_with("import_")),"self-sufficient city gets no import alarm despite low external flow")
	var gid=str(g.s.groups.keys()[0]);g.s.groups[gid].host=id;g.s.groups[gid].living={"medical":.2,"commercial":1.,"food":1.,"unemployment":.5,"pollution":0.};step(g);f=Feedback.new();f.refresh(g,230)
	g.s.groups[gid].living.medical=.7;step(g);result=f.refresh(g,249)
	check(result.bubbles.is_empty(),"medical improvement must persist before a resident claims it")
	step(g);result=f.refresh(g,250)
	check(result.bubbles.size()==1 and result.bubbles[0].group_id==gid and result.bubbles[0].building_id==id,"citizen speech has actual group home and change evidence")
	check(f.view(260).bubbles.is_empty(),"speech TTL uses real seconds without sim advance")
	g.s.block.capstone={"id":"capstone:civic","source_id":"capstone:civic","name":"管委会","function_role":"civic","tile_anchor":g.s.projects[id].tile}
	before=Store.encode(g.s);result=f.refresh(g,270)
	check(result.chirps.any(func(row):return row.source_id=="capstone:civic"),"capstone news points to its real city landmark")
	for n in 10:f.refresh(g,280+n);f.history()
	check(Store.encode(g.s)==before,"opening city history leaves authoritative state untouched")
	g.s.metrics.utilization[id]={"labor":1.,"energy":0.,"resources":1.,"utilization":0.};step(g);before=Store.encode(g.s)
	var map=Map.new();map.game=g;root.add_child(map);map.size=Vector2(1024,768)
	var overlay=Overlay.new();root.add_child(overlay);overlay.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT);overlay.size=Vector2(1024,768);overlay.configure(g,map)
	var hard_count=overlay.summary().alerts.size();overlay.toggle_bubbles(false);overlay.refresh(g)
	check(hard_count>0 and overlay.summary().alerts.size()==hard_count,"atmosphere toggle keeps hard alerts active")
	check(Store.encode(g.s)==before,"native overlay and hidden speech cannot alter RNG rewards or entities")
	check(map.beta_feedback_active,"legacy instantaneous map alarm suppressed by new feedback layer")
	var failures=checks.filter(func(row):return not row.pass).size()
	var evidence=OS.get_environment("MET_EVIDENCE_DIR")
	if evidence!="":
		DirAccess.make_dir_recursive_absolute(evidence)
		FileAccess.open(evidence.path_join("beta_feedback.json"),FileAccess.WRITE).store_string(JSON.stringify({"passed":checks.size()-failures,"failed":failures,"checks":checks},"\t"))
	print("BETA FEEDBACK %d passed, %d failed"%[checks.size()-failures,failures]);map.queue_free();overlay.queue_free();quit(1 if failures else 0)
