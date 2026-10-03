extends SceneTree
## Real full App / native software-rendered window. Explicit scale fixtures,
## never natural playthroughs or hardware-GPU performance certification.
const Paths=preload("res://tests/paths.gd")
const Store=preload("res://core/storage.gd")
const Feedback=preload("res://core/feedback.gd")
class MeasuredGame extends "res://core/game.gd":
	var perf_enabled=false
	var cpu={"advance_active":[],"refresh_metrics":[]}
	var segment_counts:Array=[]
	func advance_active(seconds:float) -> Dictionary:
		var started=Time.get_ticks_usec();var result=super.advance_active(seconds)
		if perf_enabled:
			cpu.advance_active.append((Time.get_ticks_usec()-started)/1000.)
			segment_counts.append(int(result.get("steps",0)))
		return result
	func refresh_metrics() -> void:
		var started=Time.get_ticks_usec();super.refresh_metrics()
		if perf_enabled:cpu.refresh_metrics.append((Time.get_ticks_usec()-started)/1000.)
class MeasuredApp extends "res://ui/app.gd":
	var perf_enabled=false
	var cpu={"refresh":[],"refresh_stats":[],"layout_ui":[],"process":[]}
	var last_flush_cpu_ms=0.0
	func flush_beta_time():
		var started=Time.get_ticks_usec();super.flush_beta_time()
		last_flush_cpu_ms=(Time.get_ticks_usec()-started)/1000.
	func refresh():
		var started=Time.get_ticks_usec();super.refresh()
		if perf_enabled:cpu.refresh.append((Time.get_ticks_usec()-started)/1000.)
	func refresh_stats():
		var started=Time.get_ticks_usec();super.refresh_stats()
		if perf_enabled:cpu.refresh_stats.append((Time.get_ticks_usec()-started)/1000.)
	func layout_ui():
		var started=Time.get_ticks_usec();super.layout_ui()
		if perf_enabled:cpu.layout_ui.append((Time.get_ticks_usec()-started)/1000.)
	func _process(delta):
		var started=Time.get_ticks_usec();super._process(delta)
		if perf_enabled:cpu.process.append((Time.get_ticks_usec()-started)/1000.)
var failures:Array=[]
var app
var native=false
var seconds_per_stage=20.0
var draw_cpu={"terrain":[],"structures":[],"overlays":[]}
var draw_original:Dictionary={}
func _initialize():call_deferred("run")
func stats(values:Array) -> Dictionary:
	var a=values.duplicate();a.sort();var total=0.
	for v in a:total+=float(v)
	if a.is_empty():return {"n":0}
	var over33=0;var over50=0
	for v in a:
		if float(v)>33.333:over33+=1
		if float(v)>50:over50+=1
	return {"n":a.size(),"mean_ms":total/a.size(),"p50_ms":a[int(ceil(a.size()*.5))-1],"p95_ms":a[int(ceil(a.size()*.95))-1],"p99_ms":a[int(ceil(a.size()*.99))-1],"max_ms":a[-1],"over_33ms_percent":100.*over33/a.size(),"over_50ms_percent":100.*over50/a.size()}
func rss() -> Dictionary:
	var result:Dictionary={}
	if not FileAccess.file_exists("/proc/self/status"):return result
	var file=FileAccess.open("/proc/self/status",FileAccess.READ)
	while not file.eof_reached():
		var line=file.get_line()
		if line.begins_with("VmRSS:") or line.begins_with("VmHWM:"):
			var parts=line.split(":",true,1)
			result[parts[0]+"_KiB"]=int(parts[1].strip_edges().split(" ",false)[0])
	return result
func cpu_ticks() -> float:
	var file=FileAccess.open("/proc/self/stat",FileAccess.READ)
	var source=file.get_line()
	var fields=source.substr(source.rfind(")")+2).split(" ",false)
	return float(fields[11])+float(fields[12]) if fields.size()>12 else 0.
func first_line(path:String) -> String:
	var file=FileAccess.open(path,FileAccess.READ)
	return file.get_line() if file!=null else "unavailable"
func fingerprint() -> Dictionary:
	var result={}
	for path in ["core/game.gd","core/hex.gd","core/planning.gd","core/time_model.gd","core/simulation.gd","core/rules.gd","core/fields.gd","core/allocation.gd","core/commission_theme.gd","core/cycles.gd","ui/app.gd","ui/city032/desk.gd","ui/city032/models.gd","ui/map_view.gd","ui/beta_feedback.gd","ui/beta/interface.gd","tests/beta_performance.gd"]:
		result[path]=FileAccess.get_file_as_string("res://"+path).sha256_text()
	return result
func fixture(count:int):
	var g=MeasuredGame.new();g.new_game(905003,1000000.);g.select_block(0);g.choose_origin(g.s.origin_choices[0])
	var ids=g.s.cells.keys();ids.sort_custom(func(a,b):return g.Hex.distance(str(a),"0,0")<g.Hex.distance(str(b),"0,0") if g.Hex.distance(str(a),"0,0")!=g.Hex.distance(str(b),"0,0") else str(a)<str(b))
	var defs=["homestead","neighborhood","workshop","residence","agriculture","power_station","public_center","medical_clinic","primary_school","yard","freight"]
	for tile in ids:
		if g.playable_ids().size()>=count:break
		var cell=g.s.cells[tile]
		if str(cell.building)!="" or "tile.water" in cell.traits:continue
		var n=g.playable_ids().size();g.create_building(defs[n%defs.size()],tile,["commons","civic","steel","river"][n%4],true)
	g.refresh_metrics()
	for id in g.playable_ids():
		var capacity=float(g.compiled.stats[id].get("housing",0))*.75
		if capacity>0:
			var gid=g.Population.home_group(g.s,str(id))
			if gid=="":g.Population.create(g,str(id),capacity)
			else:g.s.groups[gid].count=capacity
	for id in g.s.projects:
		for resource in ["food","material","goods"]:g.s.projects[id].inventory[resource]=g.stock_cap_for(str(id),resource)*.65
	g.Population.rehouse(g);g.refresh_metrics();g.s.settings.user_paused=false
	g.Clock.set_mode(g,"burn");g.advance_active(.1);g.s.settings.user_paused=true
	return g
func _instrument_draws() -> void:
	for entry in [[app.map_view.ground,"terrain"],[app.map_view.buildings,"structures"],[app.map_view.effects,"overlays"]]:
		var node:CanvasItem=entry[0];var key=str(entry[1]);var originals:Array=[]
		for connection in node.get_signal_connection_list("draw"):
			originals.append(connection.callable);node.disconnect("draw",connection.callable)
		draw_original[key]=originals;node.draw.connect(_draw_timed.bind(key))
func _draw_timed(key:String) -> void:
	var started=Time.get_ticks_usec()
	for callback in draw_original.get(key,[]):callback.call()
	draw_cpu[key].append((Time.get_ticks_usec()-started)/1000.)
func _clear_measurements(g) -> void:
	for key in draw_cpu:draw_cpu[key].clear()
	for key in app.cpu:app.cpu[key].clear()
	for key in g.cpu:g.cpu[key].clear()
	g.segment_counts.clear()
func _summarize_methods(source:Dictionary) -> Dictionary:
	var output={}
	for key in source:output[key]=stats(source[key])
	return output
func stage(g,kind:String) -> Dictionary:
	var active=kind=="active"
	app.test_mode=not active;app.set_process(active);g.s.settings.user_paused=not active
	g.s.beta_time.pause_reason="";app.beta_frame_seconds=0.;app.beta_boundary_seen="";app.ui_elapsed=0.;app.save_elapsed=0.
	if active:g.Clock.set_mode(g,"burn")
	app.overlay.hide();if app.desk.is_open():app.desk.close()
	var before=Store.encode(g.s)
	var local_before=float(g.s.beta_time.local_elapsed_seconds)
	var real_before=float(g.s.beta_time.active_real_seconds)
	var frames:Array=[];var memory:Array=[]
	var started=Time.get_ticks_usec();app.reset_beta_clock();var previous=started;var ticks_before=cpu_ticks();var count=0
	_clear_measurements(g);g.perf_enabled=true;app.perf_enabled=true
	while (Time.get_ticks_usec()-started)/1000000.<seconds_per_stage if native else count<100:
		if kind=="pan":app.map_view.pan+=Vector2(1. if count%120<60 else -1.,.2 if count%120<60 else -.2);app.map_view.invalidate_camera()
		elif kind=="zoom":app.map_view.set_zoom(.90+.08*sin(count*.045),Vector2(720,450))
		await process_frame
		var now=Time.get_ticks_usec();frames.append((now-previous)/1000.);previous=now;count+=1
		if count%30==0:memory.append(rss())
	var elapsed=(Time.get_ticks_usec()-started)/1000000.
	var ticks_delta=cpu_ticks()-ticks_before
	g.perf_enabled=false;app.perf_enabled=false
	var result={"stage":kind,"duration_seconds":elapsed,"full_frame_wall_ms":stats(frames),"app_callback_cpu_ms":_summarize_methods(app.cpu),"simulation_callback_cpu_ms":_summarize_methods(g.cpu),"segments_per_advance":g.segment_counts.duplicate(),"draw_callback_cpu_ms":_summarize_methods(draw_cpu),"process_memory":rss(),"memory_samples":memory,"process_cpu_ticks":ticks_delta,"clock_ticks_per_second":100,"process_cpu_seconds":ticks_delta/100.,"local_seconds_advanced":float(g.s.beta_time.local_elapsed_seconds)-local_before,"active_real_seconds_committed":float(g.s.beta_time.active_real_seconds)-real_before,"pending_active_seconds":float(app.beta_frame_seconds),"pause_reason_after":str(g.s.beta_time.pause_reason)}
	if not active:
		result.presentation_is_read_only=Store.encode(g.s)==before
		if not result.presentation_is_read_only:failures.append("presentation mutated state "+str(g.playable_ids().size())+" "+kind)
	else:
		result.state_error=g.validate_state(g.s)
		if result.state_error!="":failures.append("active state invalid: "+str(result.state_error))
		var stop_started=Time.get_ticks_usec()
		result.wall_seconds_at_stop_request=(stop_started-started)/1000000.
		app.Beta.set_mode(app,"normal")
		result.stop_handler_cpu_ms=(Time.get_ticks_usec()-stop_started)/1000.
		result.stop_pending_time_flush_cpu_ms=app.last_flush_cpu_ms
		result.stop_control_and_ui_cpu_ms=maxf(0,float(result.stop_handler_cpu_ms)-app.last_flush_cpu_ms)
		result.stop_mode_applied=str(g.s.beta_time.mode)=="normal"
		result.active_real_seconds_after_stop=float(g.s.beta_time.active_real_seconds)-real_before
		result.wall_credit_gap_after_stop_seconds=float(result.wall_seconds_at_stop_request)-float(result.active_real_seconds_after_stop)
		result.clock_credit_matches_wall=absf(float(result.wall_credit_gap_after_stop_seconds))<.025
		if not result.clock_credit_matches_wall:failures.append("active clock omitted wall time at "+str(g.playable_ids().size()))
		result.stop_scope="Single real UI mode command; its own pending-time flush is measured separately, without a benchmark-added flush. Excludes OS input dispatch. In-flight frame stalls remain visible in max/p99 full-frame intervals."
		if not result.stop_mode_applied:failures.append("stop command failed at "+str(g.playable_ids().size()))
	g.s.settings.user_paused=true;app.test_mode=true;app.set_process(false)
	print("BETA PERF ",g.playable_ids().size()," ",kind," n=",frames.size()," p95=",result.full_frame_wall_ms.p95_ms,"ms RSS=",result.process_memory)
	return result
func readonly_probes(g) -> Dictionary:
	var before=Store.encode(g.s);var output={};var samples:Dictionary={"compile_rules":[],"spatial_access":[],"allocate_labor":[],"allocate_services":[],"feedback_refresh":[],"storage_encode":[]}
	var voice=Feedback.new()
	for i in 24:
		var started=Time.get_ticks_usec();var compiled=g.Rules.compile(g);samples.compile_rules.append((Time.get_ticks_usec()-started)/1000.)
		started=Time.get_ticks_usec();var access=g.Fields.access(g,compiled);samples.spatial_access.append((Time.get_ticks_usec()-started)/1000.)
		started=Time.get_ticks_usec();g.Alloc.labor(g.s,compiled,access);samples.allocate_labor.append((Time.get_ticks_usec()-started)/1000.)
		started=Time.get_ticks_usec();g.Alloc.services(g,compiled,access,g.s.metrics.get("utilization",{}));samples.allocate_services.append((Time.get_ticks_usec()-started)/1000.)
		started=Time.get_ticks_usec();voice.refresh(g,Time.get_ticks_msec()/1000.);samples.feedback_refresh.append((Time.get_ticks_usec()-started)/1000.)
		started=Time.get_ticks_usec();Store.encode(g.s);samples.storage_encode.append((Time.get_ticks_usec()-started)/1000.)
	output.timings_ms=_summarize_methods(samples);output.scope="Isolated read-only cost probes, not additive subdivisions of a simulation tick. First call includes cold caches."
	output.state_unchanged=Store.encode(g.s)==before
	if not output.state_unchanged:failures.append("readonly probe mutated game")
	return output
func run():
	root.size=Vector2i(1440,900);native=DisplayServer.get_name()!="headless"
	var custom=OS.get_environment("MET_PERF_STAGE_SECONDS")
	if custom!="":seconds_per_stage=maxf(.1,float(custom))
	var source_before=fingerprint();var start_seconds=Time.get_ticks_usec()
	app=MeasuredApp.new();app.test_mode=true;app.ignore_save=true;root.add_child(app);app.test_mode=true;app.ignore_save=true;app.set_process(false)
	Engine.max_fps=60 if native else 0
	_instrument_draws()
	var result={"evidence_type":"explicit_scale_fixture_actual_app","fixture_population":"75% of real ready housing capacity; 65% starting inventory, deterministic seed; not a player route","renderer":RenderingServer.get_video_adapter_name(),"display":DisplayServer.get_name(),"timing_scope":"Native software-rendered window full wall-frame intervals at 1440x900, capped60; not target GPU hardware" if native else "Headless CPU and SceneTree turnaround only; no FPS claim","godot":Engine.get_version_info().string,"environment":{"cpu":OS.get_processor_name(),"logical_processors":OS.get_processor_count(),"cpu_quota":first_line("/sys/fs/cgroup/cpu.max"),"memory_limit_bytes":first_line("/sys/fs/cgroup/memory.max")},"autosave":"Disabled to preserve user saves; serialization cost measured separately, no disk I/O latency included","source_sha256":source_before,"fixtures":[],"failures":failures}
	for count in [2,12,53]:
		var g=fixture(count);app.game=g;app.map_view.game=g;app.feedback_layer.configure(g,app.map_view);app.overlay.hide();app.display_pending_tick=-1
		if app.desk.is_open():app.desk.close()
		app.selected_project="";app.refresh();app.map_view.focus_all()
		for i in 20:await process_frame
		var row={"requested_buildings":count,"actual_buildings":g.playable_ids().size(),"population":float(g.s.metrics.population),"population_groups":g.s.groups.size(),"fixture":true,"state_valid":g.validate_state(g.s)=="","stages":[]}
		if not row.state_valid:failures.append("fixture invalid "+str(count)+": "+g.validate_state(g.s))
		for kind in ["static","pan","zoom","active"]:row.stages.append(await stage(g,kind))
		row.readonly_probes=readonly_probes(g);row.render_stats=app.map_view.render_stats.duplicate(true)
		result.fixtures.append(row)
		print("BETA PERF fixture ",count," complete")
	result.duration_seconds=(Time.get_ticks_usec()-start_seconds)/1000000.;result.passed=failures.is_empty();result.process_memory=rss();result.source_changed_during_run=fingerprint()!=source_before
	var out=Paths.file("beta_performance_native.json" if native else "beta_performance.json")
	var file=FileAccess.open(out,FileAccess.WRITE);file.store_string(JSON.stringify(result,"\t"));file.close()
	print("BETA PERFORMANCE ","PASS" if failures.is_empty() else "FAIL", " ",out)
	quit(0 if failures.is_empty() else 1)
