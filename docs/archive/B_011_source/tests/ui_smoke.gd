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
	check(image.save_png(Paths.file(name+".png"))==OK,"capture: "+name)
func button(root:Node,text:String):
	if root is Button and root.text==text and root.is_visible_in_tree():return root
	for child in root.get_children():
		var b=button(child,text)
		if b!=null:return b
	return null
func click(text:String):
	var b=button(a,text);check(b!=null,"control exists: "+text)
	if b!=null:b.pressed.emit()
func start(app):
	a=app;await frames(8)
	await capture("b01-opening")
	click("从这里开始");await frames()
	check(a.game.s.phase=="origin","B origin selection retained")
	await capture("b02-origin")
	click("接手 · "+a.game.c.origins[a.game.s.origin_choices[0]].name);await frames()
	check(a.game.s.metrics.population==18,"18 real residents visible from opening")
	check(button(a,"2  涂用途")==null and button(a,"生产")==null,"zoning controls removed")
	await capture("b03-residents-opening")
	a.set_mode("site");a.on_cell("0,-1",false);a.preferences=["agriculture"];a.refresh()
	click("向社会发起招标");await frames()
	check(a.game.s.draft.offers.size()==3,"Tag-only tender opens from unzoned site")
	await capture("b04-agriculture-tender")
	var offered=a.game.s.draft.offers[0]
	click("采纳方案 · %.0f通货"%a.game.Draft.offer_cost(a.game,offered).currency);await frames()
	check(a.game.s.projects.size()==3,"UI acceptance starts construction")
	for i in 8:
		if not a.game.s.events.is_empty():a.game.opportunity("skip")
		a.game.pulse(.5)
	a.overlay.hide();a.refresh();await frames()
	await capture("b05-growing-settlement")
	var path=Paths.file("population-state-7.json")
	check(FileAccess.file_exists(path),"normal-play save available for later UI coverage")
	if FileAccess.file_exists(path):
		a.game.s=JSON.parse_string(FileAccess.get_file_as_string(path));a.selection=[];a.selected_project="";a.refresh();await frames()
		check(a.game.s.stage==2 and not a.game.s.debug_used,"later screenshots use unassisted gameplay state")
		await capture("b06-populated-town")
		a.Dialogs.population(a);await frames();await capture("b07-population-panel")
		check(button(a,"查看知识与研究")!=null,"education and knowledge explanation accessible")
		a.Dialogs.resources(a);await frames();await capture("b08-simple-resources")
		a.overlay.hide()
		for p in a.game.s.projects.values():
			if p.def=="hamlet":a.selected_project=p.id;break
		a.refresh();await frames();await capture("b09-home-and-impacts")
		for p in a.game.s.projects.values():
			if p.def in ["park","square","tavern"]:a.selected_project=p.id;break
		a.refresh();await frames();await capture("b10-park-coverage")
		a.Dialogs.research(a);await frames();await capture("b11-education-research")
		a.Dialogs.governance(a);await frames();await capture("b12-governance")
		a.overlay.hide();a.selected_project="";a.refresh()
		get_window().size=Vector2i(1180,740);await frames(8);await capture("b13-minimum-window")
		a.Dialogs.population(a);await frames();await capture("b14-minimum-population")
	var failed=0
	for c in checks:
		if not c.pass:failed+=1
	FileAccess.open(Paths.file("population-ui.json"),FileAccess.WRITE).store_string(JSON.stringify({"checks":checks,"passed":checks.size()-failed,"failed":failed,"method":"Real Godot Xvfb framebuffer and UI button signals; not physical mouse validation. Later screenshots load the unassisted route seed7 state."},"\t"))
	print("B UI ",checks.size()-failed,"/",checks.size());get_tree().quit(0 if failed==0 else 1)
