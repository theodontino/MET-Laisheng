extends Node
## Native renderer fixtures exercise UI callbacks. These are not natural playthroughs.
const Paths=preload("res://tests/paths.gd")
const Beta=preload("res://ui/beta/interface.gd")
const Store=preload("res://core/storage.gd")
var app
var checks=[]
var captures=[]
var input_actions=[]
func check(value:bool,label:String):
	checks.append({"pass":value,"name":label});print(("BETA UI PASS " if value else "BETA UI FAIL ")+label)
func frames(n:int=5):
	for i in n:await get_tree().process_frame
func capture(name:String):
	await frames();await RenderingServer.frame_post_draw
	var path=Paths.file(name+".png")
	check(get_viewport().get_texture().get_image().save_png(path)==OK,"framebuffer "+name)
	captures.append({"path":path,"fixture":true,"renderer":"Godot GL compatibility / Mesa llvmpipe","window":str(get_window().size),"text_scale":app.W.text_scale})
func add(def:String,org:String="commons") -> String:
	var keys=app.game.s.cells.keys();keys.sort()
	for tile in keys:
		var cell=app.game.s.cells[tile]
		if cell.building=="" and not "tile.water" in cell.traits:return str(app.game.create_building(def,tile,org,true).id)
	return ""
func press(label:String) -> bool:
	var found=find(app,label)
	check(found!=null and not found.disabled,"button "+label)
	if found==null or found.disabled:return false
	found.pressed.emit();return true
func find(node:Node,label:String):
	if node is Button and label in node.text and node.is_visible_in_tree():return node
	for child in node.get_children():
		var item=find(child,label)
		if item!=null:return item
	return null
func labels(node:Node) -> String:
	var result=node.text+"\n" if node is Label else ""
	for child in node.get_children():result+=labels(child)
	return result
func visible_labels(node:Node) -> String:
	if node is Control and not node.is_visible_in_tree():return ""
	var result=node.text+"\n" if node is Label else ""
	for child in node.get_children():result+=visible_labels(child)
	return result
func visible_nodes(node:Node,meta:String,value:Variant=null) -> Array:
	var result=[]
	if node is Control and node.is_visible_in_tree() and node.has_meta(meta) and (value==null or node.get_meta(meta)==value):result.append(node)
	for child in node.get_children():result.append_array(visible_nodes(child,meta,value))
	return result
func by_meta(node:Node,meta:String,value:Variant=null):
	var found=visible_nodes(node,meta,value)
	return null if found.is_empty() else found[0]
func click(control:Control,label:String="") -> bool:
	if control!=null:
		var ancestor=control.get_parent()
		while ancestor!=null:
			if ancestor is ScrollContainer:ancestor.ensure_control_visible(control);await frames(3)
			ancestor=ancestor.get_parent()
	var available=control!=null and control.is_visible_in_tree() and get_viewport().get_visible_rect().grow(1).encloses(control.get_global_rect()) and (not control is BaseButton or not control.disabled)
	check(available,"native pointer target visible: "+(label if label!="" else str(control.name) if control!=null else "missing"))
	if not available:return false
	var at=control.get_global_rect().get_center();var move=InputEventMouseMotion.new();move.position=at;app.get_viewport().push_input(move);await frames(2)
	var down=InputEventMouseButton.new();down.button_index=MOUSE_BUTTON_LEFT;down.position=at;down.pressed=true;app.get_viewport().push_input(down)
	await frames(1)
	var up=InputEventMouseButton.new();up.button_index=MOUSE_BUTTON_LEFT;up.position=at;up.pressed=false;app.get_viewport().push_input(up)
	input_actions.append({"kind":"native_pointer","target":label,"x":at.x,"y":at.y});await frames(5);return true
func start(a):
	app=a;app.ignore_save=true;app.test_mode=true;await frames(10)
	var command_line=OS.get_cmdline_args()
	var display_override=false
	for arg in command_line:
		if str(arg) in ["--fullscreen","--windowed","--resolution","--position","--headless"]:display_override=true
	check(not display_override,"main-scene startup uses no CLI display override")
	check(app.get_window().size==Vector2i(1920,1080) and app.get_window().mode in [Window.MODE_FULLSCREEN,Window.MODE_EXCLUSIVE_FULLSCREEN],"normal main scene starts at contracted fullscreen without a CLI window override")
	check(is_equal_approx(app.beta_active_interval(1000000,31000000,true),30.0),"slow rendered frame retains30realactiveseconds")
	check(app.beta_active_interval(1000000,31000000,false)==0.0,"backgroundandpausedintervalsnevercatchup")
	check(not app.beta_should_submit(.49) and app.beta_should_submit(.5),"halfsecondbatchpreservesboundedcommitcadence")
	var g=app.game;g.new_game(5126,1000000);g.select_block(0);g.choose_origin("village");app.overlay.hide();g.s.settings.user_paused=false;g.Themes.select(g,"industrial")
	var residence=g.playable_ids()[0];var production=add("workshop");var service=add("medical_clinic");var logistics=add("freight","civic")
	g.refresh_metrics();g.Population.rehouse(g);app.refresh();app.map_view.focus_all();await capture("beta-01-normal")
	var focus=app.header_nodes.people;focus.grab_focus();app.open_modal("回归弹窗");var escape=InputEventKey.new();escape.keycode=KEY_ESCAPE;escape.pressed=true;app._unhandled_key_input(escape)
	check(not app.overlay.visible and app.get_viewport().gui_get_focus_owner()==focus,"Esc closes modal and restores previous focus")
	app.desk.open("resource","food");app.desk.push_section("supply");app._unhandled_key_input(escape);check(app.desk.is_open() and app.desk.subtopic=="","Esc returns one desk level");app._unhandled_key_input(escape);check(not app.desk.is_open(),"secondEsc returns map")
	app.on_beta_source({"building_id":production,"layer":"material","kind":"input"});check(app.selected_project==production and app.map_view.layer=="material","hardalert source command locates its building")
	app.selected_project="";app.map_view.layer="normal";app.refresh()
	var forecast=app.shot.duplicate(true);forecast.forecast=false;forecast.audit={"hours":1.0,"resources":{"food":{"produced":0.0,"consumed":3.0},"material":{},"goods":{}}}
	var stock_before=Beta.risk(g,forecast);forecast.audit.resources.food.commission_delivered=20.0;var after_delivery=Beta.risk(g,forecast)
	check(is_equal_approx(float(stock_before.get("months",0)),float(after_delivery.get("months",0))),"oneoffdeliverydoesnotbecomeongoingriskconsumption")
	forecast.audit.resources.food.imported=3.0;var no_live_order=Beta.risk(g,forecast);check(is_equal_approx(float(no_live_order.get("months",0)),float(after_delivery.get("months",0))),"closedimportsarenotpromisedasfuturestock")
	check(app.desk.mode_buttons.size()==4,"four time modes replace old pulse buttons")
	check(app.safe_area.grow(2).encloses(app.desk.time_panel.get_global_rect()),"desktop time HUD fits")
	app.Beta.set_mode(app,"burn");await capture("beta-02-accelerated")
	check(g.s.beta_time.mode=="burn" and "DT" in app.desk.dt_button.tooltip_text and "现实" in app.desk.dt_button.tooltip_text,"accelerated DT rate and remaining budget available on controller")
	app.desk.open("time");check("下一大周期" in labels(app.desk.body) and "预算可运行" in labels(app.desk.body),"time archive retains budget and cycle ETA");app.desk.close()
	var active_before=float(g.s.beta_time.active_real_seconds);app.beta_frame_seconds=.37;app.Beta.set_mode(app,"normal")
	check(is_equal_approx(float(g.s.beta_time.active_real_seconds)-active_before,.37) and app.beta_frame_seconds==0,"stopflushesfractionalactiveprefixwithoutlosstime")
	check(app.display_pending_tick==-1,"normalstopdoesnotforceplanningforecast")
	app.desk.open("tasks");await capture("beta-02b-theme-progress");app.desk.close()
	for pair in [[residence,"residence"],[production,"production"],[service,"service"],[logistics,"logistics"]]:
		app.selected_project=pair[0];app.inspector_key="";app.refresh();app.map_view.focus_tile(g.s.projects[pair[0]].tile);await capture("beta-03-"+pair[1])
		check(not app.inspector031.primary.get_global_rect().intersects(app.desk.time_panel.get_global_rect()),"L2 main action clear of time HUD: "+pair[1])
		check(app.inspector031.primary.is_visible_in_tree() and not app.inspector031.primary.disabled,"L2 primary enabled: "+pair[1])
	if app.inspector031.model.primary_action.id=="market":await click(app.inspector031.primary)
	else:
		# A real shortage now gets the L2 primary action; trade is still available
		# in the explicit building archive, without suppressing the shortage.
		await click(app.inspector031.more);await click(find(app,"打开贸易"))
	check(app.desk.kind=="city040" and app.desk.arg=="market","native pointer reaches freight trade through its available context");app.desk.close()
	g.s.block.trade_ledger.append({"tick":g.s.simulation_tick,"used_capacity":2.5,"capacity":4.0})
	await click(app.inspector031.more);check("2.50 / 4.00" in labels(app.modal_body),"logistics L3 retains actual used capacity");app.close_modal()
	app.desk.open("city040","preview:"+residence+":"+str(g.Development.choices(g,residence)[0]));await capture("beta-04-upgrade")
	app.desk.close();app.selected_project=""
	var queued=add("neighborhood");g.s.projects[queued].status="construction";g.s.projects[queued].job={"work":2.4,"total":2.4,"material":4.0,"cp":true};g.s.projects[queued].slots={};g.s.projects[queued].slot_types={}
	g.refresh_metrics();app.refresh()
	app.desk.open("city040","construction");await capture("beta-04b-construction");app.desk.close()
	g.Bonus.grant(g,"P01","ui-fixture");g.Bonus.grant(g,"P01","ui-fixture")
	var host=add("manufacturing","steel")
	if host!="":
		var slots=g.slot_ids(host)
		if not slots.is_empty():
			var error=g.Bonus.install(g,"P01","building",host,slots[0]);check(error=="","install first numeric duplicate fixture")
			if slots.size()>1:Beta.install_preview(app,"P01","building",host,slots[1]);await capture("beta-05-duplicate-preview");app.overlay.hide()
	g.Bonus.draw(g,"ui:research",[],{"kind":"research","bonus_ids":["P01"]});app.desk.open("tasks");await capture("beta-06-blueprint-research")
	press("研究升级");check(int(g.s.block.blueprint_levels.get("P01",0))==1,"research callback upgrades the existing blueprint")
	for reward in Beta.pending(g):
		if not reward.choices.is_empty():g.Bonus.choose(g,str(reward.reward_id),str(reward.choices[0].bonus_id))
	app.desk.close();g.s.beta_time.local_elapsed_seconds=2*Beta.YEAR;g.s.local_hours=24;g.Cycles.tick(g,0);g.Bonus.draw(g,"ui:theme",[],{"kind":"copy"});app.open_beta_settlement();await capture("beta-07-year-major-theme")
	check(Beta.pending(g).size()>=2,"annual and theme records preserved together")
	check(app.beta_decisions_pending(),"unresolved rewards block resume")
	app.desk.scroll.scroll_vertical=99999;await capture("beta-07b-theme-event")
	app.desk.close();g.s.dt=0;g.s.block.theme.completed=false;g.Block.on_dt_zero(g);app.open_beta_settlement()
	check(app.dt_label.text=="%.1f DT"%float(g.s.dt) and is_zero_approx(float(app.desk.budget_bar.value)),"DT-zero settlement immediately synchronizes the authoritative L0 budget label and bar")
	await capture("beta-08-ending-unfinished")
	check(g.s.block.ending.eligible_theme=="","unfinished ending gives naming only")
	g.s.block.ending.eligible_theme="industrial";app.desk.render(false);await capture("beta-09-ending-qualified")
	check(press("正式挂牌"),"capstone UI available")
	check(g.s.block.ending.capstone_claimed,"capstone UI claims once")
	await capture("beta-10-landmark")
	g.log_event("ui_record_test",{},"原生回归的重要事件");var record=g.Block.archive(g);check(record.get("ok",false),"archive persists before restart")
	var saved_record:Dictionary=record.get("record",{});var record_id=str(saved_record.get("id",""))
	var record_state=Store.encode(g.s);var record_file=FileAccess.get_file_as_string(str(record.get("path","")))
	app.open_city_records();await frames()
	check(visible_nodes(app.modal_body,"city_record_id").size()==g.Block.list_archives().size() and by_meta(app.modal_body,"city_records_overview")!=null,"city records overview presents every saved city as a graphical card")
	await capture("beta-10a-city-records-overview")
	await click(by_meta(app.modal_body,"city_record_view",record_id),"open saved city record")
	var record_metrics=by_meta(app.modal_body,"city_record_metrics",record_id)
	check(record_metrics!=null and visible_nodes(record_metrics,"desk_visual","metric").size()==3 and "%.1f"%float(saved_record.get("population",0)) in visible_labels(record_metrics) and "%.1f"%float(saved_record.get("currency",0)) in visible_labels(record_metrics) and "%.2f"%float(saved_record.get("dt_spent",0)) in visible_labels(record_metrics),"record metrics read the saved population, cash and DT expenditure")
	var bonus_card=by_meta(app.modal_body,"city_record_bonus","P01");var copies=0
	for instance in saved_record.get("bonus_instances",{}).values():
		if str(instance.get("bonus_id",""))=="P01":copies+=1
	check(bonus_card!=null and g.c.bonuses.P01.name in visible_labels(bonus_card) and "装配%d份"%copies in visible_labels(bonus_card) and "蓝图等级%d"%int(saved_record.get("blueprint_levels",{}).get("P01",0)) in visible_labels(bonus_card),"record Bonus card preserves saved copies and blueprint level")
	check(by_meta(app.modal_body,"city_record_theme",str(saved_record.get("theme",{}).get("id","")))!=null and visible_nodes(app.modal_body,"desk_visual","steps").size()>0,"record theme has the saved graphical stage track")
	var history_toggle=by_meta(app.modal_body,"city_record_history")
	check(history_toggle!=null and not history_toggle.button_pressed and visible_nodes(app.modal_body,"city_record_event").is_empty(),"record history starts behind its actual disclosure")
	await capture("beta-10b-city-record")
	await click(history_toggle,"expand saved city event history")
	var saved_events:Array=saved_record.get("history",[]).slice(-20);var event_rows=visible_nodes(app.modal_body,"city_record_event");var exact_events=event_rows.size()==saved_events.size()
	for index in mini(event_rows.size(),saved_events.size()):
		var event:Dictionary=saved_events[index]
		exact_events=exact_events and int(event_rows[index].get_meta("city_record_event"))==index and str(event.get("message",event.get("text",event.get("kind","城市记录")))) in visible_labels(event_rows[index])
	check(history_toggle!=null and history_toggle.button_pressed and exact_events and "原生回归的重要事件" in visible_labels(app.modal_body),"native disclosure exposes the saved last twenty events and important event")
	if not event_rows.is_empty():app.modal_scroll.ensure_control_visible(event_rows[-1]);await frames()
	await capture("beta-10c-city-record-events")
	await click(find(app.modal_body,"返回历届城市"),"return from record to saved city overview")
	check(by_meta(app.modal_body,"city_records_overview")!=null,"record detail returns to the graphical city overview")
	check(Store.encode(g.s)==record_state and FileAccess.get_file_as_string(str(record.get("path","")))==record_file,"city record graphics, disclosures and navigation preserve exact state, RNG and saved archive")
	app.desk.close();app.overlay.hide();app.selected_project="";g.s.beta_time.pause_reason="";app.refresh();await capture("beta-11-fullscreen-desktop")
	check(app.get_window().size==Vector2i(1920,1080) and app.get_window().mode in [Window.MODE_FULLSCREEN,Window.MODE_EXCLUSIVE_FULLSCREEN],"contracted 1080p fullscreen desktop")
	check(app.safe_area.grow(2).encloses(app.desk.time_panel.get_global_rect()),"1080p time controls fit")
	check(not app.desk.task_panel.get_global_rect().intersects(app.desk.time_panel.get_global_rect()),"task rail does not cover time controls")
	for button in app.desk.mode_buttons.values():check(button.is_visible_in_tree() and app.desk.time_panel.get_global_rect().encloses(button.get_global_rect()),"all modes visible: "+button.text)
	var output={"checks":checks,"captures":captures,"input_actions":input_actions,"command_line":command_line,"evidence_type":"scripted native UI fixtures, not human playtest"};var file=FileAccess.open(Paths.file("beta-ui.json"),FileAccess.WRITE);file.store_string(JSON.stringify(output,"\t"));file.close()
	var failed=checks.filter(func(c):return not c.pass).size();print("BETA UI RESULT ",checks.size()-failed," passed, ",failed," failed");get_tree().quit(1 if failed else 0)
