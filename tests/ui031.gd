extends Node
const Paths=preload("res://tests/paths.gd")
const Store=preload("res://core/storage.gd")
const D=preload("res://ui/components031/design.gd")
const Model=preload("res://ui/view_models/inspector_model.gd")
var app
var checks=[]
func check(ok:bool,name:String):
	checks.append({"pass":ok,"name":name});print(("PASS031 " if ok else "FAIL031 ")+name)
func frames(n:int=8):
	for i in n:await get_tree().process_frame
func image(name:String):
	await frames(10);await RenderingServer.frame_post_draw
	check(get_viewport().get_texture().get_image().save_png(Paths.file(name+".png"))==OK,"framebuffer "+name)
func home_id() -> String:
	for id in app.game.playable_ids():
		if app.game.s.projects[id].def=="hamlet":return id
	return ""
func start(a):
	app=a;app.test_mode=true;await frames(12)
	app.game.new_game(7,1000000.);app.game.select_block(0)
	var origin=app.game.s.origin_choices[0]
	if "village" in app.game.s.origin_choices:origin="village"
	app.game.choose_origin(origin);app.game.s.settings.lab_speed=0;app.overlay.hide();app.refresh();app.map_view.focus_community();await frames()
	var home=home_id();app.selected_project=home;app.refresh();app.map_view.ensure_visible(app.game.s.projects[home].tile)
	await image("031-01-residence")
	var view=app.inspector031
	check(view.visible and view.tab=="overview","native inspector is the real selected building")
	check(view.model.jobs==0 and not view.nodes.has("workforce"),"zero-job residential hides staffing controls")
	check(view.model.status.code=="ok","投入齐备 is typed OK, never a red alert")
	check(view.nodes.status.get_theme_color("font_color")==Color(D.status("ok").ink),"normal status uses the specified OK ink")
	var fees=view.model.fees.map(func(x):return x.label)
	check(fees.has("住户维护") and fees.has("建筑维护"),"resident and building maintenance stay separate")
	var energy=view.model.inputs.filter(func(x):return x.id=="energy")
	check(energy.size()==1 and energy[0].children.size()>=2,"energy is one aggregate row preserving both sources")
	var expected=0.
	for e in view.model.ledger:
		if e.kind=="input" and e.resource=="energy":expected+=float(e.rate)
	check(absf(energy[0].value-expected)<.000001,"energy aggregate is exact before display rounding")
	var old_nodes=view.nodes.duplicate();view.update_view(app.game,app.shot,home)
	check(view.nodes.housing==old_nodes.housing,"refresh does not replace stable metric controls")
	view.switch_tab("slots");await image("031-02-empty-slots")
	check(view.model.slots.size()==app.game.slot_ids(home).size(),"all actual typed duplicate slots represented")
	view.scroll.scroll_vertical=999;await frames();var top=view.title_label.get_global_rect();check(app.safe_area.encloses(top),"identity remains in viewport when body is scrolled")
	view.switch_tab("details");await image("031-03-accounting")
	check(view.nodes.has("detail_in:energy"),"detail source view retains electricity breakdown")
	var before=Store.encode(app.game.s)
	for id in app.game.c.bonuses:
		var d=D.card(app.game,id);check(d.rules_match,"card text matches active rules: "+id)
		check(d.allowed_hosts==app.game.c.bonuses[id].hosts and not d.allowed_hosts.is_empty(),"card host scope matches engine hosts: "+id)
	check(Store.encode(app.game.s)==before,"reading all 40 card definitions is economically read-only")
	app.Dialogs.catalog(app);await image("031-04-catalog")
	app.Dialogs.bonus_info(app,"P02");await image("031-05-research-card")
	app.close_modal();app.selected_project=home;view.switch_tab("overview");app.refresh();app.toggle_palette("production");await image("031-06-building-palette")
	check(app.build_buttons.size()==5,"production palette keeps five real types")
	for id in app.build_buttons:
		check(app.build_buttons[id].get_meta("label")==app.game.c.projects[id].name,"separate full building title: "+id)
	app.build_palette.hide();app.select_base("works");app.on_cell("0,-1",false);app.open_tender();await image("031-07-tender")
	check(app.workbench.visible and not app.overlay.visible,"card tender remains on the real interactive map")
	var choice=app.game.s.draft.offers[-1].id;before=Store.encode(app.game.s);app.workbench.choose(choice)
	check(Store.encode(app.game.s)==before,"selecting a card does not buy or redraw offers")
	check(app.workbench.selected_offer==choice,"card button changes current preview")
	var time=app.game.s.local_hours;app.do_pulse(1);check(app.game.s.local_hours==time,"decision pause is unchanged")
	var rng=app.game.s.rng.duplicate(true);app.exit_workbench();app.open_tender();check(app.game.s.rng==rng,"free return preserves quote randomness")
	app.workbench.page="details";app.workbench.rebuild();await image("031-08-tender-details")
	app.exit_workbench();app.set_mode("inspect")
	# Direct construction fixtures below test the UI contract, NOT normal player routes.
	app.game.debug_grant();var factory=app.game.create_building("works","0,-1","steel",true).id
	var slot=""
	for sid in app.game.slot_ids(factory):
		if app.game.slot_type(factory,sid)=="Production":slot=sid;break
	check(app.game.mount_bonus("P01","building",factory,"steel",slot)=="","installed industrial card fixture")
	app.game.refresh_metrics();app.display_pending_tick=app.game.s.simulation_tick;app.selected_project=factory;app.refresh();view.switch_tab("slots");await image("031-09-installed-slot")
	app.Dialogs.slot_detail(app,factory,slot);await image("031-10-card-management");app.close_modal()
	# Responsive checks for the same game; not a claim of real phone testing.
	for pair in [[844,390,1.],[640,320,1.],[640,320,1.5]]:
		get_window().size=Vector2i(pair[0],pair[1]);app.display_settings.text_scale=pair[2];app.apply_display();await frames(24);app.selected_project=home;app.refresh();view.switch_tab("overview");await frames()
		await image("031-touch-%dx%d-%d-inspector"%[pair[0],pair[1],int(pair[2]*100)])
		check(app.safe_area.encloses(view.primary.get_global_rect()),"touch inspector primary inside %s"%str(pair))
		check(app.safe_area.encloses(view.tabs.overview.get_global_rect()),"touch tabs inside %s"%str(pair))
		app.select_base("clinic");app.on_cell("-1,0",false);app.open_tender();await frames()
		await image("031-touch-%dx%d-%d-tender"%[pair[0],pair[1],int(pair[2]*100)])
		check(app.safe_area.encloses(app.workbench.apply_button.get_global_rect()),"touch tender confirmation inside %s"%str(pair))
		app.exit_workbench();app.set_mode("inspect")
	get_window().size=Vector2i(1440,900);app.display_settings.text_scale=1.;app.apply_display();await frames(24)
	app.selected_project=home;app.refresh();view.switch_tab("overview");app.map_view.layer="normal";app.map_view.focus_community();await image("031-final-residence")
	var failures=checks.filter(func(x):return not x.pass).size()
	FileAccess.open(Paths.file("ui031.json"),FileAccess.WRITE).store_string(JSON.stringify({"checks":checks,"failed":failures,"passed":checks.size()-failures,"method":"Native Godot framebuffer and programmatic UI flows; direct fixtures explicitly marked; not Mac/mobile hardware"},"\t"))
	get_tree().quit(1 if failures else 0)
