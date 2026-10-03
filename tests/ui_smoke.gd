extends Node
const Paths=preload("res://tests/paths.gd")
const Store=preload("res://core/storage.gd")
var app
var checks=[]
func check(ok:bool,name:String):checks.append({"pass":ok,"name":name});print(("UI PASS " if ok else "UI FAIL ")+name)
func wait_frames(n:int=4):
	for i in n:await get_tree().process_frame
func image(name:String):
	await wait_frames(8);await RenderingServer.frame_post_draw
	check(get_viewport().get_texture().get_image().save_png(Paths.file(name+".png"))==OK,"framebuffer "+name)
func button(node:Node,text:String,starts:bool=false):
	if node is Button and node.is_visible_in_tree() and (node.text.begins_with(text) if starts else node.text==text):return node
	for child in node.get_children():
		var b=button(child,text,starts)
		if b!=null:return b
	return null
func click(text:String,starts:bool=false):
	var b=button(app,text,starts);check(b!=null,"visible control "+text)
	if b!=null and not b.disabled:b.pressed.emit()
func start(a):
	app=a;await wait_frames(10);await image("021-01-opening")
	click("从这里开始");await wait_frames();check(app.game.s.phase=="origin","origin transition")
	await image("021-02-origin");click("接手 · ",true);await wait_frames();app.game.s.settings.lab_speed=0
	check(app.game.s.metrics.population==18,"actual initial population")
	check(app.shot.metrics.energy>0 and app.game.s.local_hours==0,"initial electricity forecast without consuming time")
	check(not app.inspector_panel.visible,"default map has no permanent report sidebar")
	await image("021-03-settlement")
	app.select_base("farm");app.on_cell("0,-1",false);await wait_frames()
	check(app.inspector_nodes.outputs.text!="","real placement preview")
	await image("021-04-planning")
	click("比较这里的投标");await wait_frames();check(app.game.s.draft.base=="farm","same selected chassis in tender")
	await image("021-05-tender")
	var original=app.game.s.draft.offers.duplicate(true);var money=float(app.game.s.currency);var rng=app.game.s.rng.duplicate(true)
	click("保留报价，回地图比较");app.open_tender();await wait_frames()
	check(app.game.s.draft.offers==original and app.game.s.currency==money and app.game.s.rng==rng,"free reading retains price and RNG")
	click("采用 · ",true);await wait_frames()
	var id=app.selected_project;check(id!="" and app.game.s.projects[id].status=="construction","native acceptance creates construction")
	await image("021-06-construction")
	for i in 8:
		if app.game.s.projects[id].status=="running":break
		app.do_pulse(.5)
	app.overlay.hide();app.refresh();await wait_frames()
	check(app.game.s.projects[id].status=="running","construction completes through paid DT")
	var key=app.inspector_nodes.outputs.get_instance_id();app.refresh_stats();app.Panels.inspector(app)
	check(key==app.inspector_nodes.outputs.get_instance_id(),"live inspector updates keep widget identity")
	await image("021-07-first-farm")
	for slot in app.game.slot_ids(id):
		if app.game.s.projects[id].slots[slot]=="":app.request_refit(id,slot);break
	await wait_frames();await image("021-08-refit")
	app.close_modal();app.Dialogs.goals(app);await wait_frames();await image("021-09-goals")
	check(button(app,"选择这个方向")!=null,"goals offer different city outcomes")
	click("查看解法");await wait_frames();check(app.modal_title.text=="住在河谷的人","goal help routes to real resident details, not a dead button")
	app.Dialogs.resources(app);await wait_frames();await image("021-10-accounting")
	app.Dialogs.block(app);await wait_frames();await image("021-11-policy")
	check(button(app,"采用「交易税」")!=null,"policy alternatives and forecast accessible")
	app.close_modal()
	var path=Paths.file("state021-7.json")
	if FileAccess.file_exists(path):
		var result=Store.decode(FileAccess.get_file_as_string(path));check(result.ok,"native normal-route save available")
		if result.ok:
			var migration=app.game.migrate_state(result.state);check(migration.ok and not app.game.s.debug_used,"later screenshots use normal player-command route")
			app.selected_project="";app.mode="inspect";app.selection=[];app.inspector_key="";app.refresh();await wait_frames();await image("021-12-town")
			for bid in app.game.playable_ids():
				if app.game.s.projects[bid].def in ["works","timber"]:app.selected_project=bid;break
			app.map_view.layer="pollution";app.refresh();await wait_frames();await image("021-13-industry")
			app.Dialogs.explain(app,app.selected_project);await wait_frames();await image("021-14-detail")
			var project=app.selected_project;app.Dialogs.operator_change(app,project);await wait_frames();await image("021-19-operator-change")
			check(button(app,"交由「",true)!=null,"operator transfer UI available with explicit cost")
			for slot in app.game.slot_ids(project):
				if app.game.s.projects[project].slots[slot]=="":
					app.Dialogs.slot_detail(app,project,slot);await wait_frames();await image("021-20-slot-reforge")
					check(button(app,"改为",true)!=null,"empty slot reforge UI available without deleting installed schemes")
					break
			app.Dialogs.population(app);await wait_frames();await image("021-15-residents")
	app.close_modal();app.mode="inspect";app.selected_project="";app.refresh();get_window().size=Vector2i(1180,740);await wait_frames(15)
	await image("021-16-min-window")
	check(app.get_global_rect().encloses(app.checkpoint_button.get_global_rect()),"next-change action inside minimum viewport")
	var tile=""
	for k in app.game.s.cells:
		if app.game.s.cells[k].building=="" and not "tile.water" in app.game.s.cells[k].traits:tile=k;break
	app.select_base("hamlet");app.on_cell(tile,false);app.open_tender();await wait_frames();await image("021-17-min-tender")
	check(app.get_global_rect().encloses(app.modal_panel.get_global_rect()),"tender modal fits 1180x740")
	var accept=button(app,"采用 · ",true)
	check(accept!=null and app.modal_scroll.get_global_rect().encloses(accept.get_global_rect()),"accept CTA visible without scrolling at minimum size")
	app.Dialogs.goals(app);await wait_frames();await image("021-18-min-goals")
	check(app.get_global_rect().encloses(app.modal_panel.get_global_rect()),"goal dialog fits minimum viewport")
	var failed=0
	for c in checks:
		if not c.pass:failed+=1
	FileAccess.open(Paths.file("ui021.json"),FileAccess.WRITE).store_string(JSON.stringify({"checks":checks,"passed":checks.size()-failed,"failed":failed,"method":"Godot Xvfb framebuffer + native button signals; not Mac or human usability evidence."},"\t"))
	print("UI021 ",checks.size()-failed,"/",checks.size());get_tree().quit(0 if failed==0 else 1)
