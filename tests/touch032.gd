extends Node
const Store=preload("res://core/storage.gd")
const Paths=preload("res://tests/paths.gd")
var checks=[]
var app
func frames(n:int=8):
	for i in n:await get_tree().process_frame
func check(value:bool,name:String):checks.append({"pass":value,"name":name});print(("TOUCH PASS " if value else "TOUCH FAIL ")+name)
func tap(pt:Vector2):
	var e=InputEventScreenTouch.new();e.index=0;e.position=pt;e.pressed=true;Input.parse_input_event(e);await frames(2)
	e=InputEventScreenTouch.new();e.index=0;e.position=pt;e.pressed=false;Input.parse_input_event(e);await frames(5)
func start(a):
	app=a;app.test_mode=true;await frames(12)
	app.game.new_game(7,1000000.);app.game.select_block(0);app.game.choose_origin(app.game.s.origin_choices[0]);app.overlay.hide();app.refresh();await frames(8)
	await tap(app.header_nodes.finance.get_global_rect().get_center());check(app.desk.kind=="finance","native touch opens budget button")
	if app.desk.is_open():await tap(app.desk.close_button.get_global_rect().get_center())
	check(not app.desk.is_open(),"native touch closes managed page")
	get_window().size=Vector2i(640,320);app.display_settings.text_scale=1.5;app.apply_display();await frames(20);app.show_policy();await frames(15)
	var rect=app.desk.scroll.get_global_rect();print("SCROLL ",rect)
	var p=rect.position+Vector2(rect.size.x*.58,rect.size.y*.8);var e=InputEventScreenTouch.new();e.index=0;e.position=p;e.pressed=true;Input.parse_input_event(e);await frames(3)
	for i in 6:
		var drag=InputEventScreenDrag.new();drag.index=0;drag.position=p-Vector2(0,12*(i+1));drag.relative=Vector2(0,-12);Input.parse_input_event(drag);await frames(3)
	e=InputEventScreenTouch.new();e.index=0;e.position=p-Vector2(0,72);e.pressed=false;Input.parse_input_event(e);await frames(15)
	check(app.desk.scroll.scroll_vertical>0,"first real finger scrolls policy page")
	await tap(app.desk.close_button.get_global_rect().get_center());check(not app.desk.is_open(),"compact SVG close activates on touch")
	var old=app.game.s.settings.user_paused;await tap(app.desk.pause_button.get_global_rect().get_center());check(app.game.s.settings.user_paused!=old,"pause control responds to touch exactly once")
	var bad=checks.filter(func(x):return not x.pass).size();FileAccess.open(Paths.file("touch032.json"),FileAccess.WRITE).store_string(JSON.stringify({"passed":checks.size()-bad,"failed":bad,"checks":checks},"\t"));get_tree().quit(1 if bad else 0)
