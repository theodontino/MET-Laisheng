extends Control
const Game=preload("res://core/game.gd")
const Store=preload("res://core/storage.gd")
const W=preload("res://ui/widgets.gd")
const MapView=preload("res://ui/map_view.gd")
const Panels=preload("res://ui/panels.gd")
const Dialogs=preload("res://ui/dialogs.gd")
const Inv=preload("res://core/inventory.gd")
var game=Game.new()
var map_view
var mode:String="inspect"
var brush:String="settlement"
var selection:Array=[]
var preferences:Array=["agriculture"]
var selected_project:String=""
var inspector:VBoxContainer
var inspector_scroll:ScrollContainer
var left:VBoxContainer
var overlay:Control
var modal_panel:PanelContainer
var modal_body:VBoxContainer
var modal_title:Label
var modal_subtitle:Label
var toast:Label
var cash_label:Label
var dt_label:Label
var governance_label:Label
var clock_label:Label
var resource_row:HBoxContainer
var pulse_buttons:Array=[]
var elapsed:float=0
var save_elapsed:float=0
var ui_elapsed:float=0
var last_wall:float=0
var ignore_save:bool=false
var closing:bool=false
var feedback:String=""
var celebration_shown:bool=false
var selection_hint:Label
var test_mode:bool=false
var save_path:String=Store.SAVE
var shown_stage:int=-1
const ZONES={"settlement":"定居","production":"生产","circulation":"流通","public":"公共"}
const BLOCKS=["河湾","林缘旧村","北部高台"]
func _ready():
	theme=W.theme()
	get_tree().auto_accept_quit=false
	var args=OS.get_cmdline_user_args()
	if "--export-licenses" in args:
		ignore_save=true;test_mode=true
		get_tree().quit(load("res://tests/license_export.gd").new().run());return
	if "--playthrough" in args:
		ignore_save=true;test_mode=true
		get_tree().quit(load("res://tests/playthrough.gd").new().run());return
	if "--test" in args:
		ignore_save=true;test_mode=true
		var Runner=load("res://tests/runner.gd")
		if Runner!=null:
			var code=Runner.new().run();get_tree().quit(code)
		else:get_tree().quit(2)
		return
	if "--ui-smoke" in args or "--ui-demo" in args:ignore_save=true;test_mode=true
	if "--input-smoke" in args:save_path="user://input_smoke.json"
	last_wall=Time.get_unix_time_from_system()
	var saved=Store.read(save_path)
	if saved.ok and not ignore_save:
		var error=game.validate_state(saved.state)
		if error=="":
			game.s=saved.state;var recovered=game.resume_time(last_wall)
			feedback="欢迎回来，离线储存 %.1f DT。外部委托仍按现实截止。"%recovered
		else:
			game.new_game(20260922,last_wall);feedback="旧档校验未通过，已保留原文件："+error
	else:game.new_game(20260922,last_wall)
	build_ui()
	refresh()
	if game.s.phase=="select_block":Dialogs.start(self)
	elif game.s.phase=="origin":Dialogs.origin(self)
	elif game.s.phase=="failed":Dialogs.failure(self)
	elif not game.s.events.is_empty():Dialogs.opportunity(self)
	if "--ui-smoke" in args or "--ui-demo" in args:call_deferred("run_ui_smoke")
func build_ui():
	var bg=ColorRect.new();bg.color=Color("0a2027");bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(bg)
	var margin=MarginContainer.new();margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side in ["left","right","top","bottom"]:margin.add_theme_constant_override("margin_"+side,14)
	add_child(margin)
	var root=W.vbox(margin,10)
	var top=W.panel(root,Color("102c34"));top.custom_minimum_size.y=76
	var header=W.hbox(top,18)
	var namebox=W.vbox(header,0);namebox.custom_minimum_size.x=245
	namebox.add_child(W.label("MET B   /   人口与生活",24,W.TEXT,false))
	namebox.add_child(W.label("B LINE · 人口版 · 0.1.1",11,W.GOLD,false))
	var finances=W.vbox(header,1);finances.custom_minimum_size.x=130
	finances.add_child(W.label("可支配通货",11,W.MUTED,false));cash_label=W.label("",23,W.TEXT,false);finances.add_child(cash_label)
	var timebox=W.vbox(header,1);timebox.custom_minimum_size.x=110
	timebox.add_child(W.label("瓶中时光  DT",11,W.MUTED,false));dt_label=W.label("",23,W.GOLD,false);timebox.add_child(dt_label)
	var govbox=W.vbox(header,1);govbox.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	govbox.add_child(W.label("政治背书  /  满意支持",11,W.MUTED,false));governance_label=W.label("",21,W.GREEN,false);govbox.add_child(governance_label)
	header.add_child(W.button("人口与阶段",func():Dialogs.population(self)))
	header.add_child(W.button("任期记事",func():Dialogs.journal(self)))
	header.add_child(W.button("怎么玩",func():Dialogs.help(self)))
	header.add_child(W.button("存档 / 设置",func():Dialogs.settings(self)))
	var middle=W.hbox(root,10);middle.size_flags_vertical=Control.SIZE_EXPAND_FILL
	var left_panel=W.panel(middle);left_panel.custom_minimum_size.x=176
	var ls=ScrollContainer.new();ls.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;left_panel.add_child(ls)
	left=W.vbox(ls,5);left.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	var map_panel=W.panel(middle,Color("102d34"));map_panel.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	map_view=MapView.new();map_view.game=game;map_view.size_flags_horizontal=Control.SIZE_EXPAND_FILL;map_view.size_flags_vertical=Control.SIZE_EXPAND_FILL
	map_view.cell_clicked.connect(on_cell);map_view.cell_hovered.connect(on_hover);map_panel.add_child(map_view)
	var right_panel=W.panel(middle);right_panel.custom_minimum_size.x=304
	inspector_scroll=ScrollContainer.new();inspector_scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;right_panel.add_child(inspector_scroll)
	inspector=W.vbox(inspector_scroll,9);inspector.size_flags_horizontal=Control.SIZE_EXPAND_FILL;inspector.custom_minimum_size.x=276
	var bottom=W.panel(root,Color("122f35"));bottom.custom_minimum_size.y=113
	var bh=W.hbox(bottom,16)
	var tbox=W.vbox(bh,7);tbox.custom_minimum_size.x=450
	clock_label=W.label("",13,W.MUTED,false);tbox.add_child(clock_label)
	var pulses=W.hbox(tbox,6)
	for h in [.5,1.0,3.0,6.0]:
		var b=W.button("",func():do_pulse(h));b.custom_minimum_size=Vector2(106,45);pulses.add_child(b);pulse_buttons.append([b,h])
	resource_row=W.hbox(bh,8);resource_row.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	toast=W.label(feedback if feedback!="" else "先看看河谷，再决定你愿意从哪里开始。",13,W.MUTED,false)
	toast.text_overrun_behavior=TextServer.OVERRUN_TRIM_ELLIPSIS;root.add_child(toast)
	# Modal keeps player decisions intact. Dismissing a tender never redraws it.
	overlay=Control.new();overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(overlay)
	var shade=ColorRect.new();shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);shade.color=Color(.015,.035,.04,.82);overlay.add_child(shade)
	var center=CenterContainer.new();center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);overlay.add_child(center)
	modal_panel=W.panel(center,Color("15353b"));modal_panel.custom_minimum_size=Vector2(1040,660)
	var outer=W.vbox(modal_panel,12)
	var mh=W.hbox(outer,14);var titlebox=W.vbox(mh,3);titlebox.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	modal_title=W.label("",26,W.TEXT);titlebox.add_child(modal_title)
	modal_subtitle=W.label("",13,W.MUTED);titlebox.add_child(modal_subtitle)
	mh.add_child(W.button("返回地图  Esc",func():close_modal()))
	var body_scroll=ScrollContainer.new();body_scroll.custom_minimum_size=Vector2(1000,520);body_scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;body_scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED
	outer.add_child(body_scroll);modal_body=W.vbox(body_scroll,10);modal_body.size_flags_horizontal=Control.SIZE_EXPAND_FILL;modal_body.custom_minimum_size.x=990
	overlay.hide()
func open_modal(title:String,subtitle:String=""):
	W.clear(modal_body);modal_title.text=title;modal_subtitle.text=subtitle;overlay.show()
func close_modal():
	if game.s.phase in ["select_block","origin"]:return
	overlay.hide();refresh()
func refresh():
	if test_mode and map_view==null:return
	map_view.game=game;map_view.selected=selection;map_view.selected_project=selected_project;map_view.mode=mode
	refresh_left();Panels.inspector(self);refresh_stats();map_view.queue_redraw()
func refresh_left():
	W.clear(left)
	left.add_child(W.label(BLOCKS[maxi(0,int(game.s.block_index))] if game.s.block_index>=0 else "选择起点",19,W.TEXT))
	left.add_child(W.label("FIRST BLOCK / 单区开发",11,W.MUTED,false))
	for pair in [["inspect","1  观察居民与项目"],["site","2  圈地招标"]]:
		var b=W.button(pair[1],func():set_mode(pair[0]))
		if mode==pair[0]:b.add_theme_stylebox_override("normal",W.button_box(Color("3c574f"),W.GOLD))
		left.add_child(b)
	left.add_child(W.label("不再涂区。圈地 → Tag → 招标。",11,W.MUTED))
	if game.s.phase=="play":
		left.add_child(W.label(game.Population.STAGES[int(game.s.stage)],16,W.GOLD))
		left.add_child(W.button("恢复迁入" if game.s.migration_paused else "暂停新迁入",func():game.toggle_migration();save_game();refresh()))
	W.section(left,"场地", "%d / 7 格  ·  必须连通"%selection.size())
	left.add_child(W.button("清除圈选",func():selection=[];refresh()))
	W.section(left,"招标偏好  ·  最多2项")
	var taggrid=GridContainer.new();taggrid.columns=2;left.add_child(taggrid)
	for tag in game.s.policy_tags:
		var b=W.button(game.c.tag_name(tag),func():toggle_preference(tag));b.custom_minimum_size.x=67;b.add_theme_font_size_override("font_size",12)
		if tag in preferences:b.add_theme_stylebox_override("normal",W.button_box(Color("415b50"),W.GOLD))
		taggrid.add_child(b)
	var tenderbutton=W.button("继续开标" if not game.s.draft.is_empty() else "向社会发起招标",func():open_tender())
	tenderbutton.name="TenderButton";left.add_child(tenderbutton)
	var tools=W.hbox(left,4)
	tools.add_child(W.button("邻接影响",func():map_view.show_impacts=not map_view.show_impacts))
	tools.add_child(W.button("归位",func():map_view.zoom=1;map_view.pan=Vector2.ZERO))
func refresh_stats():
	cash_label.text="%.0f"%game.s.currency;cash_label.add_theme_color_override("font_color",W.RED if game.s.currency<0 else W.TEXT)
	dt_label.text="%.1f / %.0f"%[game.s.dt,game.c.balance.dt_cap]
	governance_label.text="%.0f   /   %.0f"%[game.s.mandate,game.s.support]
	governance_label.add_theme_color_override("font_color",W.RED if game.s.crisis.get("active",false) else W.GREEN)
	var time=float(game.s.local_hours)
	clock_label.text="本地经历 %d日 %02d:%02d  ·  %s  ·  时间负载 %.2f"%[int(time/24)+1,int(time)%24,int(fmod(time,1)*60),"实验×%.0f"%game.s.settings.lab_speed if game.s.settings.lab_speed>1 else "自然1:1",game.temporal_load()]
	for pair in pulse_buttons:
		var b=pair[0];var h=float(pair[1]);var cost=h*game.temporal_load()
		b.text="推进%sh\n约%.1f DT"%[str(h),cost];b.disabled=game.s.phase!="play" or game.s.dt<.08
		b.tooltip_text="整体推进本Block。施工、生产与消费一起走；断供会停工。若遇施工提议会停在事件处，只扣实际已推进的DT。"
	W.clear(resource_row)
	var m=game.s.metrics
	var supply=W.panel(resource_row,Color("15343a"));supply.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	var v=W.vbox(supply,2);v.add_child(W.label("基础供给",12,W.GOLD,false))
	for r in ["food","material"]:
		var flow=float(m.get("flows",{}).get(r,0))
		v.add_child(W.label("%s  %.0f   %+.1f/h"%[Panels.resource_name(r),Inv.total(game.s.stocks,r),flow],13,W.TEXT if flow>=0 else W.RED,false))
	v.add_child(W.label("能源  %.1f / %.1f"%[m.get("energy_need",0),m.get("energy",0)],13,W.TEXT,false))
	supply.tooltip_text="使用/供给；点击查看收支。材料和食物不再区分资源标签。"
	supply.gui_input.connect(func(e):
		if e is InputEventMouseButton and e.button_index==MOUSE_BUTTON_LEFT and e.pressed:Dialogs.resources(self))
	if int(game.s.get("stage",0))>=2:
		var goods=W.panel(resource_row,Color("203b40"));goods.size_flags_horizontal=Control.SIZE_EXPAND_FILL
		var gv=W.vbox(goods,3);gv.add_child(W.label("制造体系",12,W.GOLD,false))
		gv.add_child(W.label("低级制成品  %.1f"%Inv.total(game.s.stocks,"goods"),13,W.TEXT,false))
		gv.add_child(W.label("%+.1f/h"%m.get("flows",{}).get("goods",0),12,W.MUTED,false))
		goods.gui_input.connect(func(e):
			if e is InputEventMouseButton and e.button_index==MOUSE_BUTTON_LEFT and e.pressed:Dialogs.resources(self))
	var people=W.panel(resource_row,Color("1a3e38"));people.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	var pv=W.vbox(people,2);pv.add_child(W.label("居民  %.0f 人"%m.get("population",0),15,W.GREEN,false))
	pv.add_child(W.label("住房 %.0f / %.0f  ·  就业 %.0f / %.0f"%[m.get("housed",0),m.get("housing",0),m.get("employed",0),m.get("workforce",0)],12,W.TEXT,false))
	pv.add_child(W.label("健康 %.0f   幸福 %.0f"%[m.get("health",78),m.get("happiness",72)],12,W.TEXT,false))
	if int(game.s.get("stage",0))>=1:pv.add_child(W.label("教育 %.0f   知识 %.1f"%[m.get("education",12),game.s.knowledge],12,W.MUTED,false))
	people.tooltip_text="人口不是住房容量。点击展开逐户生活、就业和阶段条件。"
	people.gui_input.connect(func(e):
		if e is InputEventMouseButton and e.button_index==MOUSE_BUTTON_LEFT and e.pressed:Dialogs.population(self))
func set_mode(value:String):
	mode=value
	if mode!="inspect":selected_project=""
	message({"inspect":"点击项目查看运行、里程碑与关系。","zone":"拖动左键涂用途。道路与基础配套默认随项目形成。","site":"点击1～7个相邻空地，直接选择发展Tag招标；不再涂用途。"}[mode])
	refresh()
func toggle_preference(tag:String):
	if tag in preferences:preferences.erase(tag)
	elif preferences.size()<2:preferences.append(tag)
	else:message("最多2个偏好；先取消一个。")
	refresh_left()
func on_cell(key:String,dragging:bool):
	if game.s.phase!="play":
		if game.s.phase=="failed":selected_project=project_for_cell(key);Panels.inspector(self)
		return
	if mode=="zone":
		game.zone_cells([key],brush);map_view.queue_redraw();save_game()
	elif mode=="site":
		var tile=game.s.cells[key]
		if tile.site!="" or tile.terrain=="water":message("圈选尚未开发的陆地；已有项目可在观察模式查看。")
		elif key in selection:selection.erase(key)
		elif selection.size()<7:selection.append(key)
		else:message("一个场地最多7格。")
		refresh()
	else:
		selected_project=project_for_cell(key)
		map_view.selected_project=selected_project;Panels.inspector(self)
func project_for_cell(key:String) -> String:
	if not game.s.cells.has(key):return ""
	var site=game.s.cells[key].site
	if site!="" and game.s.sites.has(site) and not game.s.sites[site].project_ids.is_empty():return game.s.sites[site].project_ids[0]
	return ""
func on_hover(key:String):
	if key=="" or not game.s.cells.has(key):return
	var t=game.s.cells[key]
	var names={"plain":"冲积平地","forest":"林地","hill":"岩丘","wet":"低洼湿地","water":"河流"}
	var text=names[t.terrain]+(" · 临水" if t.waterfront else "")+"  |  "+key
	if t.site!="":text+="  |  已开发"
	if mode=="site":text+="  ·  已圈 %d / 7格"%selection.size()
	toast.text=text
func open_tender():
	if game.s.draft.is_empty():
		var error=game.create_tender(selection,preferences)
		if error!="":message(error);return
		save_game()
	Dialogs.tender(self)
func act(result:String):
	if result!="":message(result);return
	save_game();refresh()
func accept_offer(id:String):
	var kind=game.s.draft.get("kind","")
	var error=game.accept_offer(id)
	if error!="":message(error);return
	if kind=="development":selection=[]
	save_game();overlay.hide();refresh();message("方案已落地。材料随施工取用；让城市运行，或注入DT。")
func do_pulse(h:float):
	if not game.s.events.is_empty():Dialogs.opportunity(self);return
	var result=game.pulse(h);save_game();refresh()
	message("推进 %.2fh，消耗 %.2fDT。%s"%[result.advanced,result.spent,result.reason])
	if not game.s.events.is_empty():Dialogs.opportunity(self)
	elif game.s.phase=="failed":Dialogs.failure(self)
	elif game.s.complete and not celebration_shown:celebration_shown=true;Dialogs.award(self)
func message(text:String):
	feedback=text
	if toast!=null:toast.text=text
func save_game():
	if ignore_save or game.s.is_empty():return
	game.s.wall_mark=maxf(float(game.s.wall_mark),Time.get_unix_time_from_system())
	var error=Store.write(game.s,save_path)
	if error!="":message(error)
func _process(delta):
	if test_mode or game.s.is_empty() or closing:return
	var now=Time.get_unix_time_from_system()
	var gap=now-last_wall
	if gap>8:
		game.s.wall_mark=last_wall;game.resume_time(now)
	elif game.s.phase=="play" and not overlay.visible:
		game.advance_online(minf(delta,2.0)/3600.0*float(game.s.settings.lab_speed))
	last_wall=now;game.update_global(now)
	save_elapsed+=delta;ui_elapsed+=delta
	if save_elapsed>=5:save_elapsed=0;save_game()
	if ui_elapsed>=1.0:
		ui_elapsed=0;refresh_stats()
		if shown_stage!=int(game.s.get("stage",0)):shown_stage=int(game.s.get("stage",0));refresh_left()
		if save_path=="user://input_smoke.json":dump_input_layout()
		if not overlay.visible:
			Panels.inspector(self)
			if not game.s.events.is_empty():save_game();Dialogs.opportunity(self)
			elif game.s.phase=="failed":save_game();Dialogs.failure(self)
			elif game.s.complete and not celebration_shown:celebration_shown=true;save_game();Dialogs.award(self)
func _notification(what):
	if what==NOTIFICATION_WM_CLOSE_REQUEST:
		closing=true;save_game();get_tree().quit()
func _unhandled_key_input(event):
	if not event is InputEventKey or not event.pressed or event.echo:return
	match event.keycode:
		KEY_ESCAPE:close_modal()
		KEY_1:if not overlay.visible:set_mode("inspect")
		KEY_2:if not overlay.visible:set_mode("site")
		KEY_3:if not overlay.visible:set_mode("site")
		KEY_F8:Dialogs.debug(self)
		KEY_SPACE:if not overlay.visible:do_pulse(1)
func export_file(kind:String):
	var dialog=FileDialog.new();dialog.use_native_dialog=true;dialog.access=FileDialog.ACCESS_FILESYSTEM
	dialog.file_mode=FileDialog.FILE_MODE_SAVE_FILE;dialog.filters=PackedStringArray(["*.json ; JSON"])
	dialog.current_file="MET_FirstBlock_"+kind+".json";add_child(dialog)
	dialog.file_selected.connect(func(path):
		var file=FileAccess.open(path,FileAccess.WRITE)
		if file!=null:
			file.store_string(Store.encode(game.s) if kind=="save" else JSON.stringify({"build":game.s.build,"seed":game.s.seed,"debug_used":game.s.debug_used,"state":game.s,"feedback":feedback},"\t"));file.close();message("已导出："+path)
		else:message("导出失败。")
		dialog.queue_free())
	dialog.canceled.connect(func():dialog.queue_free());dialog.popup_centered_ratio(.7)
func import_file():
	var dialog=FileDialog.new();dialog.use_native_dialog=true;dialog.access=FileDialog.ACCESS_FILESYSTEM;dialog.file_mode=FileDialog.FILE_MODE_OPEN_FILE;dialog.filters=PackedStringArray(["*.json ; JSON"]);add_child(dialog)
	dialog.file_selected.connect(func(path):
		var result=Store.decode(FileAccess.get_file_as_string(path))
		if not result.ok:message(result.error)
		else:
			var error=game.validate_state(result.state)
			if error!="":message(error)
			else:
				game.s=result.state;game.resume_time(Time.get_unix_time_from_system());selection=[];selected_project="";save_game();overlay.hide();refresh();message("已恢复备份。种子、手牌和重抽计数没有重置。")
		dialog.queue_free())
	dialog.canceled.connect(func():dialog.queue_free());dialog.popup_centered_ratio(.7)
func restart(seed_value:int):
	game.new_game(seed_value,Time.get_unix_time_from_system());selection=[];selected_project="";preferences=["agriculture"];mode="inspect";celebration_shown=false;save_game();refresh();Dialogs.start(self)
func run_ui_smoke():
	var Runner=load("res://tests/ui_smoke.gd")
	if Runner==null:get_tree().quit(2);return
	var runner=Runner.new();add_child(runner);runner.start(self)

func dump_input_layout():
	var items=[]
	_collect_buttons(self,items)
	var Paths=load("res://tests/paths.gd")
	var file=FileAccess.open(Paths.file("input_layout.json"),FileAccess.WRITE)
	if file!=null:
		file.store_string(JSON.stringify({"buttons":items,"phase":game.s.phase,"selection":selection,"mode":mode,"map_pos":[map_view.global_position.x,map_view.global_position.y],"cells":{"0,-1":[map_view.point("0,-1").x+map_view.global_position.x,map_view.point("0,-1").y+map_view.global_position.y],"1,-1":[map_view.point("1,-1").x+map_view.global_position.x,map_view.point("1,-1").y+map_view.global_position.y]}}));file.close()
func _collect_buttons(node:Node,items:Array):
	if node is Button and node.is_visible_in_tree():
		var r=node.get_global_rect();items.append({"text":node.text,"x":r.get_center().x,"y":r.get_center().y,"disabled":node.disabled})
	for child in node.get_children():_collect_buttons(child,items)
