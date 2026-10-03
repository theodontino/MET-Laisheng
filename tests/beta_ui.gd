extends Node
## Native renderer fixtures exercise UI callbacks. These are not natural playthroughs.
const Paths=preload("res://tests/paths.gd")
const Beta=preload("res://ui/beta/interface.gd")
var app
var checks=[]
var captures=[]
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
func start(a):
	app=a;app.ignore_save=true;app.test_mode=true;await frames(10)
	check(is_equal_approx(app.beta_active_interval(1000000,31000000,true),30.0),"slow rendered frame retains30realactiveseconds")
	check(app.beta_active_interval(1000000,31000000,false)==0.0,"backgroundandpausedintervalsnevercatchup")
	var g=app.game;g.new_game(5126,1000000);g.select_block(0);g.choose_origin("village");app.overlay.hide();g.s.settings.user_paused=false;g.Themes.select(g,"industrial")
	var residence=g.playable_ids()[0];var production=add("workshop");var service=add("medical_clinic");var logistics=add("freight","civic")
	g.refresh_metrics();g.Population.rehouse(g);app.refresh();app.map_view.focus_all();await capture("beta-01-normal")
	var focus=app.header_nodes.finance;focus.grab_focus();app.open_modal("回归弹窗");var escape=InputEventKey.new();escape.keycode=KEY_ESCAPE;escape.pressed=true;app._unhandled_key_input(escape)
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
	check(g.s.beta_time.mode=="burn" and "DT" in app.desk.time_risk.text,"accelerated risk and DT rate shown")
	app.Beta.set_mode(app,"normal")
	app.desk.open("tasks");await capture("beta-02b-theme-progress");app.desk.close()
	for pair in [[residence,"residence"],[production,"production"],[service,"service"],[logistics,"logistics"]]:
		app.selected_project=pair[0];app.inspector_key="";app.refresh();app.map_view.focus_tile(g.s.projects[pair[0]].tile);await capture("beta-03-"+pair[1])
	g.s.block.trade_ledger.append({"tick":g.s.simulation_tick,"used_capacity":2.5,"capacity":4.0})
	app.inspector031.update_context();check("2.50 / 4.00" in labels(app.inspector031.nodes.context),"logistics L2 reads actual used capacity and refreshes")
	app.desk.open("city040","preview:"+residence+":"+str(g.Development.choices(g,residence)[0]));await capture("beta-04-upgrade")
	app.desk.close();app.selected_project=""
	var queued=add("neighborhood");g.s.projects[queued].status="construction";g.s.projects[queued].job={"work":2.4,"total":2.4,"material":4.0,"cp":true};g.s.projects[queued].slots={};g.s.projects[queued].slot_types={}
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
	app.desk.close();g.s.dt=0;g.s.block.theme.completed=false;g.Block.on_dt_zero(g);app.open_beta_settlement();await capture("beta-08-ending-unfinished")
	check(g.s.block.ending.eligible_theme=="","unfinished ending gives naming only")
	g.s.block.ending.eligible_theme="industrial";app.desk.render(false);await capture("beta-09-ending-qualified")
	check(press("正式挂牌"),"capstone UI available")
	check(g.s.block.ending.capstone_claimed,"capstone UI claims once")
	await capture("beta-10-landmark")
	g.log_event("ui_record_test",{},"原生回归的重要事件");var record=g.Block.archive(g);check(record.get("ok",false),"archive persists before restart")
	app.open_city_records();press(str(g.s.block.display_name));check(g.c.bonuses.P01.name in labels(app.modal_body) and "原生回归的重要事件" in labels(app.modal_body),"record detail includes Bonus and important events");await capture("beta-10b-city-record")
	app.desk.close();app.overlay.hide();app.selected_project="";g.s.beta_time.pause_reason="";app.get_window().size=Vector2i(900,620);app.W.text_scale=1.5;app.display_settings.text_scale=1.5;app.resize_interface();app.refresh();await capture("beta-11-large-text-small-window")
	check(app.safe_area.grow(2).encloses(app.desk.time_panel.get_global_rect()),"large text time controls fit")
	check(not app.desk.task_panel.get_global_rect().intersects(app.desk.time_panel.get_global_rect()),"task rail does not cover time controls")
	for button in app.desk.mode_buttons.values():check(button.is_visible_in_tree() and app.desk.time_panel.get_global_rect().encloses(button.get_global_rect()),"all modes visible: "+button.text)
	var output={"checks":checks,"captures":captures,"evidence_type":"scripted native UI fixtures, not human playtest"};var file=FileAccess.open(Paths.file("beta-ui.json"),FileAccess.WRITE);file.store_string(JSON.stringify(output,"\t"));file.close()
	var failed=checks.filter(func(c):return not c.pass).size();print("BETA UI RESULT ",checks.size()-failed," passed, ",failed," failed");get_tree().quit(1 if failed else 0)
