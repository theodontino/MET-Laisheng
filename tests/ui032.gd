extends Node
const Paths=preload("res://tests/paths.gd")
const Store=preload("res://core/storage.gd")
var app
var checks=[]
func check(ok:bool,label:String):
	checks.append({"pass":ok,"name":label});print(("PASS032 " if ok else "FAIL032 ")+label)
func frames(n:int=10):
	for i in n:await get_tree().process_frame
func image(name:String):
	await frames(14);await RenderingServer.frame_post_draw
	check(get_viewport().get_texture().get_image().save_png(Paths.file(name+".png"))==OK,"framebuffer "+name)
func visible_bounds(c:Control) -> bool:
	return app.safe_area.encloses(c.get_global_rect())
func start(a):
	app=a;app.test_mode=true;await frames(12)
	app.game.new_game(7,1000000.);app.game.select_block(0);app.game.choose_origin(app.game.s.origin_choices[0]);app.overlay.hide();app.refresh();app.map_view.focus_community();await frames()
	await image("032-01-new-city")
	var old=Store.decode(FileAccess.get_file_as_string(Paths.file("legacy031-fixture.json")))
	check(old.ok,"031 real city save decodes")
	if old.ok:check(app.game.migrate_state(old.state).ok,"031 real city migration")
	app.game.pulse(.5);app.refresh();app.map_view.focus_community();await frames()
	await image("032-02-city-desk")
	check(visible_bounds(app.desk.time_panel),"time dock is in viewport upper right")
	check(visible_bounds(app.desk.resource_panel),"resource dock is in viewport lower right")
	var saved=Store.encode(app.game.s)
	app.desk.open("resource","food");await image("032-03-food")
	check(app.desk.kind=="resource" and app.map_view.layer=="food","resource click is detail plus map view")
	check(Store.encode(app.game.s)==saved,"resource detail is read-only")
	app.desk.close();app.desk.open("finance");await image("032-04-finance")
	check(app.desk.is_fullscreen(),"finance uses full managed page")
	app.desk.open("population");await image("032-05-population")
	check(app.map_view.layer=="population","population opens housing and employment overlay")
	app.desk.arg="coverage";app.desk.render(false);await image("032-06-coverage")
	app.desk.close();app.show_policy();await image("032-07-all-policies")
	check(app.desk.policy_buttons.size()==app.game.c.policies.size(),"all policy options are on the same scroll page")
	check(not app.desk.resource_panel.visible,"policy page hides unrelated resources")
	check(not app.desk.time_panel.visible,"policy page only shows possible costs, not DT")
	var pid="tax_production"
	if not app.game.c.policies.has(pid):
		for id in app.game.c.policies:
			if app.game.c.policies[id].slot=="Tax" and app.game.s.block.policies.Tax!=id:pid=id;break
	app.desk.choose_policy(pid);await frames(12);await image("032-08-policy-preview")
	check(app.desk.policy_prediction.get("ok",false),"policy preview uses actual engine")
	app.desk.close();app.desk.open("time");await image("032-09-time")
	app.desk.toggle_pause();var h=app.game.s.local_hours;app.game.advance_online(1);check(app.game.s.local_hours==h,"manual pause stops natural time")
	app.desk.close();app.do_pulse(.5);check(app.game.s.local_hours==h,"manual pause stops manual advance")
	app.desk.toggle_pause();app.desk.open("tasks");await image("032-10-tasks")
	app.desk.task_tab="available";app.desk.render(false);await image("032-11-available")
	app.desk.task_tab="history";app.desk.render(false);await image("032-12-task-history")
	app.desk.close()
	for size in [[1024,640,1.],[844,390,1.],[640,320,1.],[640,320,1.5]]:
		get_window().size=Vector2i(size[0],size[1]);app.display_settings.text_scale=size[2];app.apply_display();await frames(24);app.selected_project="";app.refresh();await frames()
		await image("032-%dx%d-%d-hud"%[size[0],size[1],100*size[2]])
		app.desk.open("population");await frames(24);await image("032-%dx%d-%d-population"%[size[0],size[1],100*size[2]])
		check(visible_bounds(app.desk.close_button),"population back fits "+str(size))
		check(visible_bounds(app.desk.primary),"population drilldown fits "+str(size))
		app.show_policy();await frames(24);await image("032-%dx%d-%d-policy"%[size[0],size[1],100*size[2]])
		check(visible_bounds(app.desk.primary),"policy action fits "+str(size));check(visible_bounds(app.desk.close_button),"policy back fits "+str(size))
		app.desk.close()
	# Native touch scrolling of the same full policy page, not a Scroll value assignment.
	app.show_policy();await frames(20)
	var rect=app.desk.scroll.get_global_rect();var pt=rect.position+Vector2(rect.size.x*.58,rect.size.y*.8)
	var old_scroll=app.desk.scroll.scroll_vertical;var state_before=Store.encode(app.game.s)
	var touch=InputEventScreenTouch.new();touch.index=2;touch.pressed=true;touch.position=pt;Input.parse_input_event(touch);await frames(2)
	for step in 6:
		var drag=InputEventScreenDrag.new();drag.index=2;drag.position=pt-Vector2(0,(step+1)*12);drag.relative=Vector2(0,-12);Input.parse_input_event(drag);await frames(2)
	touch=InputEventScreenTouch.new();touch.index=2;touch.pressed=false;touch.position=pt-Vector2(0,72);Input.parse_input_event(touch);await frames(16)
	check(app.desk.scroll.scroll_vertical>old_scroll,"native touch drags the full policy page")
	check(Store.encode(app.game.s)==state_before,"scrolling policy page never adopts or charges a policy")
	check(app.desk.close_button.icon!=null,"compact back control uses a visible SVG, not a clipped glyph")
	await image("032-touch-policy-scrolled")
	app.desk.close();app.selected_project=app.game.playable_ids()[0];app.refresh();await frames()
	check(app.desk.resource_panel.get_global_rect().end.x<=app.inspector_panel.get_global_rect().position.x+1,"resource dock does not cover compact inspector")
	check(app.desk.time_panel.get_global_rect().end.x<=app.inspector_panel.get_global_rect().position.x+1,"time controls do not cover compact inspector")
	check(app.desk.pause_button.icon!=null,"compact pause has visible pause or play icon")
	var fail=checks.filter(func(x):return not x.pass).size();FileAccess.open(Paths.file("ui032.json"),FileAccess.WRITE).store_string(JSON.stringify({"checks":checks,"passed":checks.size()-fail,"failed":fail},"\t"));get_tree().quit(1 if fail else 0)
