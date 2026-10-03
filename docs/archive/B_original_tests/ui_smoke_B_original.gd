extends Node
const Paths=preload("res://tests/paths.gd")
var a
var checks=[]
func check(ok:bool,name:String):
	checks.append({"name":name,"pass":ok});print(("UI PASS " if ok else "UI FAIL ")+name)
func frames(n:int=4):
	for i in n:await get_tree().process_frame
func capture(name:String):
	await RenderingServer.frame_post_draw
	var image=get_viewport().get_texture().get_image()
	var err=image.save_png(Paths.file(name+".png"))
	check(err==OK,"screenshot "+name)
func find_button(root:Node,text:String):
	if root is Button and root.text==text and root.is_visible_in_tree():return root
	for child in root.get_children():
		var result=find_button(child,text)
		if result!=null:return result
	return null
func click(text:String):
	var button=find_button(a,text)
	check(button!=null,"button exists: "+text)
	if button!=null:button.pressed.emit()
func start(app):
	a=app
	await frames(8)
	await capture("01-opening-map")
	click("从这里开始")
	await frames()
	check(a.game.s.phase=="origin","block selection reaches origin")
	await capture("02-origin")
	var origin_id=a.game.s.origin_choices[0]
	click("接手 · "+a.game.c.origins[origin_id].name)
	await frames()
	check(a.game.s.phase=="play" and a.game.s.projects.size()==2,"origin starts real local simulation")
	await capture("03-new-settlement")
	a.brush="production";a.set_mode("zone")
	a.on_cell("0,-1",false);a.on_cell("1,-1",false)
	a.set_mode("site");a.on_cell("0,-1",false);a.on_cell("1,-1",false)
	a.preferences=["community"]
	a.refresh()
	click("向社会发起招标")
	await frames()
	check(a.game.s.draft.get("offers",[]).size()==3,"UI opens three valid tender cards")
	await capture("04-tender")
	var def=a.game.s.draft.offers[0]
	var price=a.game.Draft.offer_cost(a.game,def).currency
	click("采纳方案 · %.0f通货"%price)
	await frames()
	check(a.game.s.projects.size()==3,"UI award starts construction")
	var last=""
	for p in a.game.s.projects.values():
		if p.status=="construction":last=p.id
	a.selected_project=last;a.mode="inspect";a.refresh()
	await capture("05-construction")
	for i in 6:
		if not a.game.s.events.is_empty():a.game.opportunity("skip")
		a.game.pulse(.5)
	a.overlay.hide();a.refresh();await frames()
	await capture("06-first-project")
	check(a.game.s.projects[last].status=="running","UI-created project completes through real DT/stock engine")
	# Rich state is explicitly a test fixture, not a claim of normal balance.
	var Runner=load("res://tests/runner.gd")
	var runner=Runner.new()
	a.game=runner.fixture(["hamlet","school","timber","farm","power","clinic","warehouse","mill"])
	a.game.s.debug_used=true
	runner.advance(a.game,12)
	a.selected_project=runner.first_id(a.game,"school");a.overlay.hide();a.refresh();await frames(5)
	await capture("07-growth-fixture")
	var relation_id=""
	for rel in a.game.s.relations.values():
		if rel.progress>=rel.required:relation_id=rel.id;break
	check(relation_id!="","fixture has mature fusion")
	if relation_id!="":
		a.game.create_fusion(relation_id);a.Dialogs.tender(a);await frames();await capture("08-fusion")
		a.accept_offer(a.game.s.draft.offers[0]);await frames()
	a.Dialogs.research(a);await frames();await capture("09-research")
	a.Dialogs.governance(a);await frames();await capture("10-governance")
	a.game.s.mandate=10;a.game.s.support=20;a.game.Progress.check_crisis(a.game)
	a.Dialogs.governance(a);await frames();await capture("11-crisis")
	check(a.game.s.crisis.active,"governance crisis visible without DT failure")
	a.Dialogs.settings(a);await frames();await capture("12-settings")
	a.overlay.hide();a.selected_project="";a.selection=[]
	var real_path=Paths.file("legal_playthrough_20260922.json")
	if FileAccess.file_exists(real_path):
		a.game.s=JSON.parse_string(FileAccess.get_file_as_string(real_path));a.refresh();await frames()
		check(a.game.s.complete and not a.game.s.debug_used,"legal command playthrough yields completed non-debug city")
		await capture("13-normal-play-complete")
		get_window().size=Vector2i(1180,740);await frames(10)
		await capture("14-minimum-window")
		a.Dialogs.tender(a) if not a.game.s.draft.is_empty() else a.Dialogs.help(a)
		await frames();await capture("15-minimum-modal")
	var failed=0
	for c in checks:
		if not c.pass:failed+=1
	var file=FileAccess.open(Paths.file("ui.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"passed":checks.size()-failed,"failed":failed,"platform":"Linux Xvfb / Godot GL Compatibility","fixture_note":"Screenshots 07–12 use declared test fixtures, not ordinary-play claims"},"\t"));file.close()
	print("UI RESULT %d/%d"%[checks.size()-failed,checks.size()])
	get_tree().quit(0 if failed==0 else 1)
