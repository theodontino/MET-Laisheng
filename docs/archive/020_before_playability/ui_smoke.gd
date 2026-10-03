extends Node
const Paths=preload("res://tests/paths.gd")
const Store=preload("res://core/storage.gd")
var app
var checks=[]
func check(ok:bool,name:String):
	checks.append({"pass":ok,"name":name});print(("UI PASS " if ok else "UI FAIL ")+name)
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
	if b!=null:b.pressed.emit()
func start(a):
	app=a;await wait_frames(10)
	await image("020-01-opening")
	click("从这里开始");await wait_frames()
	check(app.game.s.phase=="origin","UI enters actual Origin choices")
	await image("020-02-origin")
	click("接手 · ",true);await wait_frames()
	check(app.game.s.groups.size()==1 and app.game.s.metrics.population==18,"real initial group loaded")
	check(button(app,"2  涂用途")==null,"no zoning button")
	await image("020-03-first-settlement")
	app.selected_base="farm";app.on_cell("0,-1",false);click("向社会发起招标");await wait_frames()
	check(app.game.s.draft.base=="farm" and app.game.s.draft.offers.size()==3,"chosen chassis + three actual offers")
	await image("020-04-tender")
	click("采纳方案 · ",true);await wait_frames()
	var id=app.game.playable_ids()[-1];app.selected_project=id;app.refresh()
	check(app.game.s.projects[id].slots.size()==3,"initial bonus occupies one of three slots")
	await image("020-05-construction")
	app.do_pulse(1);await wait_frames();app.overlay.hide()
	check(app.game.s.projects[id].status=="running","paid construction reaches operating state")
	app.request_refit(id,"Logistics");await wait_frames()
	check(app.game.s.draft.kind=="refit","existing building can procure a new slot solution")
	await image("020-06-refit")
	click("采纳方案 · ",true);await wait_frames()
	check(app.game.s.projects[id].slots.Logistics!="","refit activates chosen logical slot")
	var path=Paths.file("state020-7.json")
	check(FileAccess.file_exists(path),"unassisted route snapshot available")
	if FileAccess.file_exists(path):
		var restored=Store.decode(FileAccess.get_file_as_string(path));check(restored.ok,"route snapshot checksum")
		app.game.s=restored.state;app.game.refresh_metrics();app.selection=[];app.selected_project="";app.overlay.hide();app.refresh();await wait_frames()
		check(app.game.s.complete and not app.game.s.debug_used,"later screenshots use genuine public-command route, not resource grants")
		await image("020-07-built-city")
		for bid in app.game.playable_ids():
			if app.game.s.projects[bid].def=="works":app.selected_project=bid;break
		app.map_view.layer="pollution";app.refresh();await wait_frames();await image("020-08-industrial-build")
		app.Dialogs.explain(app,app.selected_project);await wait_frames();await image("020-09-explanation")
		app.Dialogs.population(app);await wait_frames();await image("020-10-groups")
		app.Dialogs.block(app);await wait_frames();await image("020-11-policy")
		check(button(app,"采用「交易税」")!=null,"policy alternatives visible")
		app.Dialogs.resources(app);await wait_frames();await image("020-12-resources")
		app.Dialogs.catalog(app);await wait_frames();await image("020-13-catalog")
		app.Dialogs.scope_choice(app);await wait_frames();await image("020-14-scope")
		app.close_modal();get_window().size=Vector2i(1180,740);await wait_frames(15)
		await image("020-15-min-window")
		var b=button(app,"向社会发起招标");check(b!=null and app.get_global_rect().encloses(b.get_global_rect()),"main tender CTA visible at minimum viewport")
		app.Dialogs.block(app);await wait_frames();await image("020-16-min-policy")
		check(app.get_global_rect().encloses(app.modal_panel.get_global_rect()),"policy modal fits minimum viewport")
	var failed=0
	for c in checks:
		if not c.pass:failed+=1
	FileAccess.open(Paths.file("ui020.json"),FileAccess.WRITE).store_string(JSON.stringify({"checks":checks,"passed":checks.size()-failed,"failed":failed,"method":"Godot Xvfb framebuffer and real UI signals. Includes screenshot checks; not separate human playtests. Physical mouse uses a separate harness."},"\t"))
	print("UI020 ",checks.size()-failed,"/",checks.size());get_tree().quit(0 if failed==0 else 1)
