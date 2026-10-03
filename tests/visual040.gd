extends Node
## Presentation regression: explicit visual fixtures, not a gameplay completion claim.
const Paths=preload("res://tests/paths.gd")
const Store=preload("res://core/storage.gd")
const Art=preload("res://ui/map/art.gd")
var app
var checks=[]
var captures=[]
func check(ok:bool,label:String):
	checks.append({"pass":ok,"name":label});print(("VISUAL PASS " if ok else "VISUAL FAIL ")+label)
func frames(count:int=8):
	for i in count:await get_tree().process_frame
func capture(name:String):
	app.map_view.hovered=""
	await frames(12);await RenderingServer.frame_post_draw
	var path=Paths.file(name+".png")
	check(get_viewport().get_texture().get_image().save_png(path)==OK,"capture "+name)
	captures.append({"name":name,"path":path,"viewport":str(app.get_viewport_rect().size),"font_scale":app.W.text_scale})
func inside(c:Control) -> bool:return app.safe_area.grow(1).encloses(c.get_global_rect())
func window_size(width:int,height:int,scale:float=1.):
	get_window().size=Vector2i(Vector2(width,height)*DisplayServer.screen_get_scale())
	app.display_settings.text_scale=scale;app.apply_display();await frames(20)
func start(a):
	app=a;app.test_mode=true;app.ignore_save=true;await frames(12)
	app.game.new_game(7040,1000000.);app.game.select_block(0);app.game.choose_origin(app.game.s.origin_choices[0])
	app.overlay.hide();app.desk.close();app.game.s.settings.user_paused=true;app.refresh()
	await window_size(1440,900);app.map_view.focus_community();await capture("01-city-1440")
	check(inside(app.top_panel) and inside(app.bottom_panel) and inside(app.desk.resource_panel) and inside(app.desk.time_panel),"desktop HUD stays inside viewport")
	var defs=app.game.c.v040.buildings
	var missing=[]
	for id in defs:
		if Art.sprite(id)==null or Art.metadata(id).is_empty():missing.append(id)
	check(missing.is_empty(),"all 21 building definitions have new artwork: "+str(missing))
	var clipped=[]
	for key in Art.visual_catalog.assets:
		var entry=Art.visual_catalog.assets[key];var bounds=entry.bounds;var dimensions=entry.size
		if bounds[0]<2 or bounds[1]<2 or bounds[2]>dimensions[0]-2 or bounds[3]>dimensions[1]-2:clipped.append(key)
	check(clipped.is_empty(),"all sprite silhouettes clear image edges: "+str(clipped))
	var high=0.;var water_flat=true
	for k in app.game.s.cells:
		var height=app.map_view.elevation_at(app.map_view.flat_point(k));high=maxf(high,height)
		if app.game.s.cells[k].terrain=="water" and absf(height)>.001:water_flat=false
	check(high>50. and water_flat,"terrain has real raised vertices and a level river")
	var state=Store.encode(app.game.s)
	for k in app.game.s.cells:
		check(app.map_view.hit(app.map_view.point(k))==k,"projected map picking "+str(k))
	var point=app.map_view.bounds_rect().get_center()
	var before=(point-app.map_view.size*.5-app.map_view.pan)/app.map_view.zoom
	app.map_view.set_zoom(1.9,point)
	var after=(point-app.map_view.size*.5-app.map_view.pan)/app.map_view.zoom
	check(before.distance_to(after)<.001,"pointer-anchored zoom preserves world position")
	app.map_view.focus_community();await frames()
	check(Store.encode(app.game.s)==state,"drawing and camera changes preserve simulation and RNG")
	var id=app.game.playable_ids()[0]
	app.selected_project=id;app.selection=[app.game.s.projects[id].tile];app.refresh();app.map_view.ensure_visible(app.game.s.projects[id].tile)
	await capture("02-building-1440")
	check(inside(app.inspector_panel),"building inspector fits desktop")
	var target=str(app.game.Development.choices(app.game,id)[0])
	app.desk.open("city040","preview:"+id+":"+target);await capture("03-upgrade-1440")
	check(inside(app.desk.page),"upgrade window fits desktop")
	app.desk.close();app.selected_project="";app.selection=[];app.select_base("neighborhood");app.on_cell("-1,1",false);app.open_tender()
	await capture("04-tender-1440")
	check(inside(app.workbench),"operator comparison fits desktop")
	app.exit_workbench();app.set_mode("inspect")
	app.desk.open("finance");await capture("05-budget-1440")
	app.show_policy();await capture("06-policies-1440")
	app.desk.choose_policy("tax_trade");await frames(40);await capture("07-policy-preview-1440")
	app.desk.close();app.selected_project="";app.selection=[]
	# All nodes are shown in a test-only town to review silhouettes and occlusion.
	var terrain=[]
	for k in app.game.s.cells:
		if app.game.s.cells[k].terrain!="water" and app.game.s.cells[k].building=="":terrain.append(k)
	terrain.sort_custom(func(x,y):return app.game.Hex.distance(x,"0,0")<app.game.Hex.distance(y,"0,0"))
	var index=0
	for definition in defs:
		if index>=terrain.size():break
		app.game.create_building(definition,terrain[index],"river",true);index+=1
	app.game.refresh_metrics();app.refresh();app.map_view.focus_community();await capture("08-grown-town-1440")
	var roof_checked=false
	for bid in app.game.playable_ids():
		var building=app.game.s.projects[bid]
		if building.def!="advanced_residence":continue
		var texture=Art.sprite(building.def);var rectangle=app.map_view.building_screen_rect(bid);var extent=rectangle.size
		var origin=rectangle.position
		for y in range(190,350,8):
			for x in range(240,520,8):
				var uv=Vector2(x,y)/texture.get_size();var screen=origin+uv*extent
				if Art.opaque_at(building.def,uv) and app.map_view.building_hit(screen)==bid and app.map_view.hit(screen)!=building.tile:
					roof_checked=true;break
			if roof_checked:break
	check(roof_checked,"tall building roof selects the visible building across ground-cell boundaries")
	app.screenshot_mode=true;app.layout_ui();await capture("09-town-art")
	app.screenshot_mode=false;app.layout_ui()
	app.map_view.layer="terrain";app.map_view.focus_all();app.refresh_legend();await capture("14-elevation-1440");app.map_view.layer="normal";app.refresh_legend()
	await window_size(1920,1080);app.map_view.focus_community();await capture("10-town-1920")
	app.desk.open("city040","market");await capture("11-market-1920");app.desk.close()
	await window_size(1024,640,1.5);app.map_view.focus_community();await capture("12-compact-150")
	check(inside(app.desk.time_panel),"compact time controls remain visible at 150%")
	check(inside(app.desk.resource_panel),"compact resource control stays inside viewport at 150%")
	check(not "\n" in app.cash_label.text and not "\n" in app.governance_label.text,"compact budget and population use one line after resize")
	check("预算" in app.header_nodes.finance.tooltip_text and "居住" in app.header_nodes.people.tooltip_text,"compact status retains full detail in tooltips")
	app.show_policy();await capture("13-policies-compact-150")
	check(inside(app.desk.close_button),"compact management return remains visible at 150%")
	app.desk.close();await window_size(1440,900);app.map_view.focus_community()
	# Short native-rendered frame sample after assets and shaders are warm.
	await frames(45)
	var times=[]
	for i in 90:
		var now=Time.get_ticks_usec();await get_tree().process_frame;times.append(float(Time.get_ticks_usec()-now)/1000.)
	times.sort();var perf={"median_frame_ms":times[45],"p95_frame_ms":times[85],"rendering_method":RenderingServer.get_current_rendering_method(),"os":OS.get_name(),"method":"Native rendered 21-building fixture; includes vsync and desktop scheduling. Not Windows acceptance."}
	var failed=checks.filter(func(x):return not x.pass).size()
	FileAccess.open(Paths.file("visual040.json"),FileAccess.WRITE).store_string(JSON.stringify({"passed":checks.size()-failed,"failed":failed,"checks":checks,"captures":captures,"performance":perf,"method":"Native framebuffer and callbacks using explicit visual fixtures; camera/picking math checked independently. Pointer acceptance reported separately."},"\t"))
	print("VISUAL040 ",checks.size()-failed,"/",checks.size()," ",perf)
	if "--visual040-interactive" in OS.get_cmdline_user_args():return
	get_tree().quit(1 if failed else 0)
