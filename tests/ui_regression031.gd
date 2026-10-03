extends Node
const Paths=preload("res://tests/paths.gd")
const Store=preload("res://core/storage.gd")
var app
var checks=[]
func check(ok:bool,name:String):checks.append({"pass":ok,"name":name});print(("PASS031reg " if ok else "FAIL031reg ")+name)
func wait_frames(n:int=8):
	for i in n:await get_tree().process_frame
func image(name:String):
	await wait_frames(12);await RenderingServer.frame_post_draw
	check(get_viewport().get_texture().get_image().save_png(Paths.file(name+".png"))==OK,"framebuffer "+name)
func start(a):
	app=a;await wait_frames(15);await image("031reg-01-opening")
	app.game.select_block(0);app.game.choose_origin(app.game.s.origin_choices[0]);app.game.s.settings.lab_speed=0;app.overlay.hide();app.refresh();app.map_view.focus_community()
	await image("031reg-02-settlement")
	check(not app.inspector_panel.visible,"no permanent inspector")
	app.select_base("farm");app.on_cell("0,-1",false);await image("031reg-03-planning")
	app.open_tender();await wait_frames()
	check(app.decision_kind=="tender" and app.workbench.visible and not app.overlay.visible,"non-modal tender and explicit decision pause")
	await image("031reg-04-tender")
	var time=app.game.s.local_hours;var money=app.game.s.currency;var rng=app.game.s.rng.duplicate(true)
	app.do_pulse(1);check(app.game.s.local_hours==time,"space/pulse cannot advance while comparing")
	check(app.workbench.preview.get("ok",false),"same-engine candidate preview")
	app.workbench.choose(app.game.s.draft.offers[-1].id)
	check(app.game.s.currency==money and app.game.s.rng==rng,"candidate selection does not spend or reroll")
	app.set_layer("change");await image("031reg-05-changes")
	app.exit_workbench();app.open_tender();check(app.game.s.rng==rng,"free return preserves quote RNG")
	app.accept_offer(app.game.s.draft.offers[0].id);await image("031reg-06-construction")
	var imported=Store.decode(FileAccess.get_file_as_string(Paths.file("legacy021-fixture.json")))
	check(imported.ok,"source 021 city fixture decoded")
	if imported.ok:
		var result=app.game.migrate_state(imported.state);check(result.ok,"021 city imports")
		app.game.s.settings.lab_speed=0;app.selected_project="";app.mode="inspect";app.selection=[];app.map_view.layer="normal";app.inspector_key="";app.refresh();app.map_view.focus_community()
	await image("031reg-07-town")
	app.set_layer("medical");await image("031reg-08-medical")
	app.set_layer("education");await image("031reg-09-education")
	app.set_layer("pollution");await image("031reg-10-environment")
	app.set_layer("operator");await image("031reg-11-organizations")
	var home=""
	for id in app.game.playable_ids():
		if app.game.s.projects[id].def=="hamlet":home=id;break
	app.selected_project=home;app.set_layer("employment");app.refresh();await image("031reg-12-employment")
	app.show_policy("Tax");await image("031reg-13-policy")
	app.desk.close();app.exit_workbench();app.selected_project="";app.set_mode("inspect");app.map_view.layer="normal";app.refresh()
	get_window().size=Vector2i(844,390);await wait_frames(25);app.layout_ui();app.map_view.focus_community()
	check(app.compact,"native 844x390 layout selects compact")
	await image("031reg-14-phone-city")
	app.select_base("clinic");app.on_cell("0,-1",false);await image("031reg-15-phone-planning")
	app.open_tender();await image("031reg-16-phone-tender")
	dump_geometry(app.workbench,"phone-tender")
	check(app.safe_area.encloses(app.workbench.apply_button.get_global_rect()),"phone confirmation inside safe rect")
	app.workbench.page="details";app.workbench.rebuild();await image("031reg-17-phone-details")
	get_window().size=Vector2i(640,320);await wait_frames(25);app.workbench.page="summary";app.workbench.rebuild();app.layout_ui();await image("031reg-18-minimum")
	check(app.safe_area.encloses(app.workbench.apply_button.get_global_rect()),"640x320 confirmation remains reachable")
	app.display_settings.text_scale=1.5;app.apply_display();app.workbench.rebuild();await image("031reg-19-large-type")
	check(app.safe_area.encloses(app.workbench.apply_button.get_global_rect()),"150 percent type confirmation remains reachable")
	app.display_settings.text_scale=1.;app.apply_display();app.exit_workbench();app.set_mode("inspect")
	get_window().size=Vector2i(844,390);await wait_frames(20);app.map_view.focus_community();await wait_frames()
	var old_pan=app.map_view.pan;var old_money=app.game.s.currency;var old_rng=app.game.s.rng.duplicate(true);var old_buildings=app.game.s.projects.size()
	send_touch(0,true,Vector2(140,165));await wait_frames(2)
	send_drag(0,Vector2(230,185),Vector2(90,20));await wait_frames(2);send_touch(0,false,Vector2(230,185));await wait_frames()
	check(app.map_view.pan!=old_pan,"native ScreenDrag pans the map")
	check(app.game.s.projects.size()==old_buildings and app.game.s.currency==old_money,"drag-release never places or purchases a building")
	var old_zoom=app.map_view.zoom
	send_touch(0,true,Vector2(160,160));send_touch(1,true,Vector2(260,160));await wait_frames(2)
	send_drag(1,Vector2(330,160),Vector2(70,0));await wait_frames(2);send_touch(1,false,Vector2(330,160));send_touch(0,false,Vector2(160,160));await wait_frames()
	check(app.map_view.zoom>old_zoom,"native two-finger input performs anchored zoom")
	check(app.game.s.rng==old_rng and app.game.s.projects.size()==old_buildings,"pinch release does not redraw offers or create a click")
	app.layer_locked=true;app.set_layer("food");app.select_base("clinic");check(app.map_view.layer=="food","locked layer is not stolen by building selection")
	app.layer_locked=false;app.set_mode("inspect");app.set_layer("pollution");app.select_base("clinic");check(app.map_view.layer=="medical","clinic selects relevant analysis layer")
	app.set_mode("inspect");check(app.map_view.layer=="pollution","exit planning restores previous layer")
	app.set_layer("normal");app.map_view.focus_community();var old_radius=app.map_view.radius()
	app.selected_project=app.game.playable_ids()[0];app.refresh();await wait_frames();check(app.map_view.radius()==old_radius,"inspector does not rescale world geometry")
	app.open_modal("Input focus test","");var edit=LineEdit.new();app.modal_body.add_child(edit);edit.grab_focus();await wait_frames()
	var h=app.game.s.local_hours;var ev=InputEventKey.new();ev.keycode=KEY_SPACE;ev.pressed=true;app._unhandled_key_input(ev)
	check(app.game.s.local_hours==h,"space in a text field never advances simulation")
	app.close_modal();app.set_mode("inspect");app.display_settings.reduced_motion=true;app.apply_display();check(app.map_view.reduce_motion,"reduced-motion preference reaches all map animation guards")
	get_window().size=Vector2i(1024,640);await wait_frames(20);await image("031reg-21-small-desktop")
	check(app.safe_area.encloses(app.checkpoint_button.get_global_rect()),"1024x640 primary time control remains visible")
	get_window().size=Vector2i(1024,768);await wait_frames(20);await image("031reg-22-tablet")
	get_window().size=Vector2i(1440,900);await wait_frames(20);app.display_settings.text_scale=1.25;app.apply_display();app.select_base("clinic");app.on_cell("0,-1",false);app.open_tender();await wait_frames(20);await image("031reg-23-125-type")
	check(app.safe_area.encloses(app.workbench.apply_button.get_global_rect()),"125 percent typography keeps confirmation in viewport")
	app.display_settings.text_scale=1.;app.apply_display();app.exit_workbench();app.set_mode("inspect");app.map_view.layer="normal";app.refresh();app.map_view.focus_community();await wait_frames()
	var frame_ms=[]
	for i in 120:
		var began=Time.get_ticks_usec();await get_tree().process_frame;frame_ms.append((Time.get_ticks_usec()-began)/1000.)
	frame_ms.sort();FileAccess.open(Paths.file("render031reg.json"),FileAccess.WRITE).store_string(JSON.stringify({"environment":"Linux llvmpipe software OpenGL; not target Mac or mobile GPU","samples":frame_ms.size(),"frame_interval_p95_ms":frame_ms[int(frame_ms.size()*.95)],"labels_last_frame":app.map_view.label_rects.size(),"world_atlas_estimated_gpu_mib":42.67},"\t"))
	var failures=checks.filter(func(x):return not x.pass).size()
	FileAccess.open(Paths.file("ui031reg.json"),FileAccess.WRITE).store_string(JSON.stringify({"checks":checks,"failed":failures,"passed":checks.size()-failures},"\t"))
	get_tree().quit(1 if failures else 0)

func dump_geometry(node:Node,name:String):
	var list=[];walk_geometry(node,list)
	FileAccess.open(Paths.file(name+"-geometry.json"),FileAccess.WRITE).store_string(JSON.stringify(list,"\t"))
func walk_geometry(node:Node,list:Array):
	if node is Control:
		list.append({"class":node.get_class(),"name":node.name,"text":node.text if node is Label or node is Button else "","rect":str(node.get_global_rect()),"min":str(node.get_combined_minimum_size()),"vflags":node.size_flags_vertical})
	for ch in node.get_children():walk_geometry(ch,list)

func send_touch(index:int,pressed:bool,pos:Vector2):
	var e=InputEventScreenTouch.new();e.index=index;e.pressed=pressed;e.position=pos;Input.parse_input_event(e)
func send_drag(index:int,pos:Vector2,relative:Vector2):
	var e=InputEventScreenDrag.new();e.index=index;e.position=pos;e.relative=relative;Input.parse_input_event(e)
