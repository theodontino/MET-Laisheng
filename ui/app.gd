extends Control
const Beta=preload("res://ui/beta/interface.gd")
const FeedbackLayer=preload("res://ui/beta_feedback.gd")
var feedback_layer
var beta_frame_seconds:float=0.0
var beta_clock_usec:int=0
var modal_focus_before:WeakRef
var beta_boundary_seen:String=""
var beta_background:bool=false
var beta_background_wall:float=0.0
const CityDesk=preload("res://ui/city032/desk.gd")
var desk
const Game=preload("res://core/game.gd")
const Store=preload("res://core/storage.gd")
const W=preload("res://ui/widgets.gd")
const MapView=preload("res://ui/map_view.gd")
const Panels=preload("res://ui/panels.gd")
const Dialogs=preload("res://ui/dialogs.gd")
const Presentation=preload("res://ui/presentation.gd")
const Art=preload("res://ui/map/art.gd")
const MapData=preload("res://ui/view_models/map_data.gd")
const Workbench=preload("res://ui/shell/workbench.gd")
const UI031=preload("res://ui/components031/components.gd")
const Design031=preload("res://ui/components031/design.gd")
const Inspector031=preload("res://ui/components031/inspector.gd")
var inspector031
var inspector_stack
const Planner=preload("res://core/planning.gd")
var catalog_state={"query":"","domain":"all"}
var compact:bool=false
var issue_index:int=-1
var display_pending_tick:int=-1
var display_settings={"text_scale":1.0,"reduced_motion":false,"high_contrast":false,"force_compact":false,"all_links":false,"task_rail_expanded":false,"citizen_bubbles":true,"frame_limit":0}
var safe_area=Rect2()
var top_panel:PanelContainer
var bottom_panel:PanelContainer
var tool_panel:Control
var layer_picker:PanelContainer
var legend_panel:PanelContainer
var legend_title:Label
var legend_body:Label
var layer_button:Button
var lock_button:Button
var notice_panel:VBoxContainer
var build_palette:PanelContainer
var palette_category:String=""
var workbench
var decision_kind:String=""
var manual_layer:bool=false
var layer_locked:bool=false
var layer_before_plan:String="normal"
var header_nodes={}
var bottom_nodes={}
var category_buttons=[]
var hud_root:Control
var modal_outer:VBoxContainer
var modal_center:CenterContainer
var resize_pending:bool=false
var hover_pending:bool=false
var hover_wait:float=0.
var view_model={}
var last_phase:String=""
var first_operations={}
var observed_status={}
var observed_seed:int=-99999999
var observed_complete:bool=false
var screenshot_mode:bool=false
var planner=Planner.new()
var game=Game.new()
var map_view
var mode:String="inspect"
var brush:String="settlement"
var selection:Array=[]
var selected_base:String="homestead"
var selected_project:String=""
var hovered_tile:String=""
var inspector:VBoxContainer
var inspector_scroll:ScrollContainer
var inspector_panel:PanelContainer
var inspector_key:String=""
var inspector_nodes={}
var left:VBoxContainer
var overlay:Control
var modal_panel:PanelContainer
var modal_body:VBoxContainer
var modal_title:Label
var modal_subtitle:Label
var modal_scroll:ScrollContainer
var toast:Label
var cash_label:Label
var dt_label:Label
var governance_label:Label
var clock_label:Label
var goal_button:Button
var resource_row:HBoxContainer
var resources_labels={}
var build_buttons={}
var alert_buttons=[]
var alerts=[]
var pulse_buttons:Array=[]
var checkpoint_button:Button
var shot={}
var save_elapsed:float=0
var ui_elapsed:float=0
var last_wall:float=0
var ignore_save:bool=false
var closing:bool=false
var feedback:String=""
var celebration_shown:bool=false
var test_mode:bool=false
var save_path:String=Store.SAVE
var last_revision:int=-1
var chime:AudioStreamPlayer
const BLOCKS=["河湾","林缘旧村","北部高台"]
const ZONES={"settlement":"定居","production":"生产","circulation":"流通","public":"公共"}
func _ready():
	# macOS reports window sizes in backing pixels; keep the intended logical
	# startup size and 640x320 minimum on Retina displays as well.
	if OS.get_name()=="macOS" and DisplayServer.screen_get_scale()>1.:
		var density=DisplayServer.screen_get_scale();var win=get_window()
		win.min_size=Vector2i(Vector2(640,320)*density)
		win.size=Vector2i(Vector2(1440,900)*density).min(DisplayServer.screen_get_usable_rect().size)
	load_display_settings();theme=W.theme();get_tree().auto_accept_quit=false
	var args=OS.get_cmdline_user_args()
	if "--test032" in args:
		test_mode=true;ignore_save=true;var report=load("res://tests/civic032.gd").new().run();get_tree().quit(report);return
	if "--test031-viewmodels" in args:
		test_mode=true;ignore_save=true;var report=load("res://tests/viewmodels031.gd").new().run();get_tree().quit(1 if report.failed else 0);return
	if "--test030" in args:
		test_mode=true;ignore_save=true;var report=load("res://tests/presentation030.gd").new().run();get_tree().quit(1 if report.failed else 0);return
	for pair in [["--test","runner"],["--playthrough","playthrough"]]:
		if pair[0] in args:
			ignore_save=true;test_mode=true;get_tree().quit(load("res://tests/"+pair[1]+".gd").new().run());return
	ignore_save="--beta-ui" in args or "--colony041" in args or "--fresh" in args or "--touch032" in args or "--ui032" in args or "--ui040" in args or "--visual040" in args or "--input032" in args or "--ui-smoke" in args or "--ui030" in args or "--preview030" in args or "--ui031" in args or "--input031" in args or "--ui031-regression" in args
	if "--beta-ui" in args or "--colony041" in args or "--ui040" in args or "--visual040" in args:test_mode=true
	if "--input-smoke" in args:save_path="user://input_smoke031.json"
	if "--input032" in args:ignore_save=false;save_path="user://input_smoke032.json"
	game.new_game(25092026,Time.get_unix_time_from_system())
	if not ignore_save and FileAccess.file_exists(save_path):
		var loaded=Store.read(save_path)
		if loaded.ok:
			var result=game.migrate_state(loaded.state)
			if result.ok:game.resume_time(Time.get_unix_time_from_system())
			else:ignore_save=true;feedback=result.error+" 原档未改，已停止自动保存。"
		else:ignore_save=true;feedback="主档和备份无法校验，原文件未改，已停止自动保存。"
	celebration_shown=game.s.get("city_goal",{}).get("completed",false)
	build_ui();refresh();last_wall=Time.get_unix_time_from_system()
	if game.s.phase=="select_block":
		Dialogs.start(self)
	elif game.s.phase=="origin":Dialogs.origin(self)
	elif game.s.phase=="failed":Dialogs.failure(self)
	if "--beta-ui" in args:call_deferred("run_beta_ui")
	if "--ui-smoke" in args:call_deferred("run_ui_smoke")
	if "--ui030" in args:call_deferred("run_ui030")
	if "--ui031" in args:call_deferred("run_ui031")
	if "--ui032" in args:call_deferred("run_ui032")
	if "--ui040" in args:call_deferred("run_ui040")
	if "--visual040" in args:call_deferred("run_visual040")
	if "--colony041" in args:call_deferred("run_colony041")
	if "--touch032" in args:call_deferred("run_touch032")
	if "--ui031-regression" in args:call_deferred("run_ui031_regression")
	if "--preview030" in args:
		for i in 3:
			if game.s.phase=="select_block":game.select_block(0)
			elif game.s.phase=="origin":game.choose_origin(game.s.origin_choices[0])
		overlay.hide();game.s.settings.lab_speed=0.;refresh();map_view.focus_community()
	get_window().size_changed.connect(request_layout)
	request_layout()
func build_ui():
	map_view=MapView.new();map_view.game=game;map_view.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(map_view)
	map_view.cell_clicked.connect(on_cell);map_view.cell_hovered.connect(on_hover)
	feedback_layer=FeedbackLayer.new();add_child(feedback_layer);feedback_layer.configure(game,map_view);feedback_layer.source_clicked.connect(on_beta_source);feedback_layer.toggle_bubbles(bool(display_settings.citizen_bubbles))
	hud_root=Control.new();hud_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);hud_root.mouse_filter=Control.MOUSE_FILTER_IGNORE;add_child(hud_root)
	top_panel=W.panel(hud_root);var header=W.hbox(top_panel,8);header_nodes.row=header
	var brand=W.flat(W.button("",func():desk.open("city040","block"),false,"当前地区与命名"));brand.custom_minimum_size=Vector2(136,46);header.add_child(brand);header_nodes.brand=brand
	var brand_text=W.vbox(brand,0);brand_text.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);brand_text.mouse_filter=Control.MOUSE_FILTER_IGNORE
	header_nodes.place=W.label("MET / 来生",18,Color.WHITE,false);header_nodes.place.clip_text=true;header_nodes.place.text_overrun_behavior=TextServer.OVERRUN_TRIM_ELLIPSIS;brand_text.add_child(header_nodes.place)
	header_nodes.place_hint=W.label("来生 · 生态管理区",14,Color("C4D7CE"));brand_text.add_child(header_nodes.place_hint)
	var finance=W.button("",func():desk.open("finance"),false,"预算、收支平衡和实际历史");finance.name="Budget032";finance.custom_minimum_size=Vector2(186,56);header.add_child(finance);header_nodes.finance=finance
	cash_label=W.label("",17,W.TEXT,false);finance.add_child(cash_label);cash_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);cash_label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;cash_label.vertical_alignment=VERTICAL_ALIGNMENT_CENTER
	var people=W.button("",func():desk.open("population"),false,"人口、居住、就业与需求覆盖");people.name="Population032";people.custom_minimum_size=Vector2(164,56);header.add_child(people);header_nodes.people=people
	governance_label=W.label("",16,W.TEXT,false);people.add_child(governance_label);governance_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);governance_label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;governance_label.vertical_alignment=VERTICAL_ALIGNMENT_CENTER
	var policy=W.icon_button("policy","政策",func():show_policy(),"全屏地区政策");policy.name="Policies032";header.add_child(policy);header_nodes.policy=policy
	goal_button=W.icon_button("ledger","任务",func():desk.open("tasks"),"任务中心：当前、可接和历史");goal_button.name="Tasks032";header.add_child(goal_button);header_nodes.ledger=goal_button
	W.spacer(header)
	var menu=W.icon_button("settings","",func():Dialogs.settings(self),"菜单与设置");header.add_child(menu);header_nodes.menu=menu
	tool_panel=Control.new();tool_panel.mouse_filter=Control.MOUSE_FILTER_IGNORE;hud_root.add_child(tool_panel)
	var toolrow=W.hbox(tool_panel,6);header_nodes.toolrow=toolrow;layer_button=W.icon_button("layers","图层",toggle_layers,"分析城市");layer_button.custom_minimum_size=Vector2(84,40);toolrow.add_child(layer_button)
	lock_button=W.icon_button("lock","",func():layer_locked=not layer_locked;refresh_legend(),"锁定当前图层，不跟建设工具切换");lock_button.custom_minimum_size=Vector2(48,40);toolrow.add_child(lock_button)
	var focus_button=W.icon_button("focus","",func():map_view.focus_community(),"回到社区");focus_button.custom_minimum_size=Vector2(48,40);toolrow.add_child(focus_button)
	var world_button=W.icon_button("world","",func():map_view.focus_all(),"看全地区");world_button.custom_minimum_size=Vector2(48,40);toolrow.add_child(world_button)
	var beauty=W.icon_button("eye","",func():screenshot_mode=not screenshot_mode;layout_ui(),"欣赏城市 / Esc返回（不暂停时间）");beauty.custom_minimum_size=Vector2(48,40);toolrow.add_child(beauty)
	header_nodes.policy.reparent(toolrow);header_nodes.ledger.reparent(toolrow)
	legend_panel=W.panel(hud_root);var lv=W.vbox(legend_panel,5);legend_title=W.label("",17);lv.add_child(legend_title);legend_body=W.label("",14,W.MUTED);lv.add_child(legend_body)
	lv.add_child(W.flat(W.button("来源与图例…",layer_details)))
	notice_panel=W.vbox(hud_root,8)
	for i in 2:
		var b=W.button("",func():alert_action(i));b.clip_text=true;b.alignment=HORIZONTAL_ALIGNMENT_LEFT;notice_panel.add_child(b);alert_buttons.append(b)
	inspector_panel=W.panel(hud_root)
	var isp=W.box(Design031.color("panel"),Design031.color("divider"),16)
	isp.content_margin_left=0;isp.content_margin_right=0;isp.content_margin_top=0;isp.content_margin_bottom=0
	inspector_panel.add_theme_stylebox_override("panel",isp)
	inspector_stack=W.vbox(inspector_panel,0);inspector_stack.size_flags_vertical=Control.SIZE_EXPAND_FILL
	inspector031=Inspector031.new();inspector_stack.add_child(inspector031);inspector031.setup(self);inspector031.hide()
	inspector_scroll=ScrollContainer.new();inspector_scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;inspector_scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;inspector_stack.add_child(inspector_scroll)
	var inset=MarginContainer.new();inset.size_flags_horizontal=Control.SIZE_EXPAND_FILL;inspector_scroll.add_child(inset)
	for edge in ["margin_left","margin_right","margin_top","margin_bottom"]:inset.add_theme_constant_override(edge,16)
	inspector=W.vbox(inset,12);inspector.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	bottom_panel=W.panel(hud_root);var bottom=W.vbox(bottom_panel,0);var dock=W.hbox(bottom,4);bottom_nodes.dock=dock
	for pair in [["home","农居","homestead"],["medical","邻里","neighborhood"],["production","作坊","workshop"]]:
		var b=W.flat(W.button(pair[1],func():select_base(pair[2])));b.icon=Art.thumbnail(pair[2]);b.expand_icon=true;b.add_theme_constant_override("icon_max_width",34);b.custom_minimum_size=Vector2(112,58);b.set_meta("font_base",15);b.tooltip_text=game.c.projects[pair[2]].name+" · 选择位置后比较运营者";dock.add_child(b);category_buttons.append(b)
	var place=W.primary(W.button("选择建筑",primary_build_action,false,"选择起点建筑；选中地块后比较运营者方案"));place.name="BuildPrimary032";place.custom_minimum_size=Vector2(144,48);bottom_nodes.place=place;dock.add_child(place)
	var mobile_build=W.icon_button("build","建设",func():toggle_palette("categories"));dock.add_child(mobile_build);bottom_nodes.build=mobile_build
	var mobile_layers=W.icon_button("layers","图层",toggle_layers);dock.add_child(mobile_layers);bottom_nodes.layers=mobile_layers
	var mobile_stats=W.icon_button("ledger","任务",func():desk.open("tasks"));dock.add_child(mobile_stats);bottom_nodes.stats=mobile_stats
	var growth=W.icon_button("build","发展",func():desk.open("city040"),"升级、施工、许可、委托与贸易");dock.add_child(growth);bottom_nodes.growth=growth
	resource_row=W.hbox(bottom,0);resource_row.hide()
	clock_label=W.label("",14,W.MUTED,false);clock_label.hide();bottom.add_child(clock_label)
	toast=W.label("",14,W.TEXT,false);toast.text_overrun_behavior=TextServer.OVERRUN_TRIM_ELLIPSIS;toast.add_theme_stylebox_override("normal",W.box(Color("172329"),Color("34474D"),8));hud_root.add_child(toast);toast.minimum_size_changed.connect(request_layout)
	build_palette=W.panel(hud_root);build_palette.hide()
	layer_picker=W.panel(hud_root);layer_picker.hide()
	workbench=Workbench.new();hud_root.add_child(workbench);workbench.setup(self);workbench.hide()
	desk=CityDesk.new();hud_root.add_child(desk);desk.setup(self);desk.rail_expanded=bool(display_settings.task_rail_expanded)
	overlay=Control.new();overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(overlay)
	var shade=ColorRect.new();shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);shade.color=Color(.035,.07,.085,.66);overlay.add_child(shade)
	modal_center=CenterContainer.new();modal_center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);overlay.add_child(modal_center)
	modal_panel=W.panel(modal_center);modal_outer=W.vbox(modal_panel,8)
	var mh=W.hbox(modal_outer,8);var titles=W.vbox(mh,4);titles.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	modal_title=W.label("",24);titles.add_child(modal_title);modal_subtitle=W.label("",16,W.MUTED);titles.add_child(modal_subtitle)
	mh.add_child(W.button("返回",close_modal))
	modal_scroll=ScrollContainer.new();modal_scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;modal_scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;modal_outer.add_child(modal_scroll)
	modal_body=W.vbox(modal_scroll,10);modal_body.size_flags_horizontal=Control.SIZE_EXPAND_FILL;overlay.hide()
	chime=AudioStreamPlayer.new();add_child(chime)
	for panel in [top_panel,bottom_panel,inspector_panel,workbench,legend_panel,build_palette,modal_panel]:panel.minimum_size_changed.connect(request_layout)
	layout_ui()
func open_modal(title:String,subtitle:String=""):
	flush_beta_time()
	if not overlay.visible and get_viewport().gui_get_focus_owner()!=null:modal_focus_before=weakref(get_viewport().gui_get_focus_owner())
	W.clear(modal_body);modal_title.text=title;modal_subtitle.text=subtitle;modal_scroll.scroll_vertical=0;overlay.show();layout_ui()
func close_modal():
	reset_beta_clock()
	if game.s.phase in ["select_block","origin"]:return
	overlay.hide();refresh();play_cue("back")
	if modal_focus_before!=null:
		var previous=modal_focus_before.get_ref()
		if is_instance_valid(previous) and previous.is_visible_in_tree():previous.grab_focus()
	modal_focus_before=null
func refresh():
	if map_view==null:return
	visual_feedback()
	map_view.game=game;map_view.selected=selection;map_view.selected_project=selected_project;map_view.mode=mode
	if decision_kind=="":map_view.planned_base=selected_base if mode=="site" else ""
	map_view.planning_values=planner.terrain(game,selected_base) if mode=="site" and game.s.phase=="play" else {}
	view_model=MapData.current(game,planner,display_pending_tick==int(game.s.simulation_tick));shot=view_model;map_view.display_shot=view_model
	refresh_stats()
	if decision_kind=="" and not desk.is_open():Panels.inspector(self)
	else:inspector_panel.hide()
	if mode!="site" and decision_kind=="":map_view.plan_result={};map_view.plan_tile=""
	map_view.invalidate();last_revision=game.revision;refresh_legend();layout_ui()
	if last_phase!="play" and game.s.phase=="play":map_view.focus_community()
	last_phase=str(game.s.phase)
func refresh_stats():
	if cash_label==null or desk==null:return
	view_model=MapData.current(game,planner,display_pending_tick==int(game.s.simulation_tick));shot=view_model;map_view.display_shot=view_model
	var m=shot.get("metrics",{})
	header_nodes.place.text=str(game.s.block.get("display_name","河谷登陆点")) if game.s.phase=="play" else "MET / 来生"
	header_nodes.place_hint.text="Beta · 可命名 ›" if game.s.block.get("naming_rights",false) else "来生 · 城市试验"
	sync_header_values()
	cash_label.add_theme_color_override("font_color",Color("FFB5B5") if game.s.currency<0 else Color.WHITE)
	dt_label.text="%.1f DT"%game.s.dt if compact else "%.1f DT  ›"%game.s.dt
	goal_button.text="任务"
	alerts=Presentation.alerts(game,shot)
	for i in 2:
		alert_buttons[i].visible=i<alerts.size()
		if i<alerts.size():alert_buttons[i].text=alerts[i].title+"  ›";alert_buttons[i].tooltip_text=alerts[i].detail
	desk.update_hud()
func sync_header_values():
	if cash_label==null or governance_label==null or game.s.is_empty():return
	var m=shot.get("metrics",{})
	var balance=float(game.s.get("currency",0));var cashflow=float(m.get("cashflow",0))
	var population=float(m.get("population",0));var housed=float(m.get("housed",0));var housing=float(m.get("housing",0))
	cash_label.text=("预算 %.0f"%balance) if compact else "%.0f  通货\n%+.1f /月"%[balance,cashflow]
	governance_label.text=("人口 %.0f"%population) if compact else "%.0f  居民\n居住 %.0f / %.0f"%[population,housed,housing]
	header_nodes.finance.tooltip_text="预算：%.0f 通货 · 收支：%+.1f /月\n点击查看预算、收支平衡和实际历史。"%[balance,cashflow]
	header_nodes.people.tooltip_text="人口：%.0f · 居住：%.0f / %.0f\n点击查看人口、居住、就业与需求覆盖。"%[population,housed,housing]
func select_base(id:String):
	if not id in game.c.v040.progression.tier1:
		desk.open("city040","development");message("专业建筑需要从现有建筑升级；新建从农居、邻里中心或作坊开始。");return
	if desk.is_open():desk.close()
	issue_index=-1
	if decision_kind!="":exit_workbench()
	if mode!="site":layer_before_plan=map_view.layer;manual_layer=false
	selected_base=id;mode="site";selected_project="";selection=[];inspector_key="";build_palette.hide()
	if not layer_locked and not manual_layer:
		map_view.layer={"homestead":"terrain","neighborhood":"medical","workshop":"material"}.get(id,"terrain")
	refresh();message(Presentation.primary(id)+"。点一块地，先看后果再建设。")
func primary_build_action():
	if decision_kind!="":return
	if mode=="site" and selected_base in game.c.v040.progression.tier1:
		if not selection.is_empty():open_tender()
		else:map_view.focus_community();message("点击一块空闲陆地，选中位置后即可比较运营者方案。")
	else:toggle_palette("categories")
func refresh_build_dock():
	if bottom_nodes.is_empty() or not bottom_nodes.has("place"):return
	var bases=["homestead","neighborhood","workshop"]
	for i in mini(category_buttons.size(),bases.size()):
		var b:Button=category_buttons[i];var active=mode=="site" and selected_base==bases[i]
		if active:
			var selected=W.button_box(W.PANEL,W.ACCENT,7);selected.content_margin_left=5;selected.content_margin_right=5;selected.content_margin_top=4;selected.content_margin_bottom=4
			b.add_theme_stylebox_override("normal",selected);b.add_theme_stylebox_override("hover",W.button_box(W.RAISED,W.ACCENT,7));b.add_theme_stylebox_override("pressed",W.button_box(W.PANEL,W.ACCENT,7));b.add_theme_color_override("font_color",W.ACCENT);b.add_theme_color_override("font_hover_color",W.ACCENT)
		else:
			W.flat(b);b.remove_theme_color_override("font_color");b.remove_theme_color_override("font_hover_color")
	var place:Button=bottom_nodes.place
	place.disabled=decision_kind!=""
	if mode=="site" and selected_base in game.c.v040.progression.tier1:
		if selection.is_empty():place.text="点击地块";place.tooltip_text="选择一块空闲陆地，再比较三个运营者方案"
		else:place.text="比较运营者";place.tooltip_text="查看三个运营者的真实成本、预测和施工报价"
	else:place.text="选择建筑";place.tooltip_text="选择农居、邻里中心或作坊作为起点建筑"
func set_mode(value:String):
	issue_index=-1
	if decision_kind!="":exit_workbench()
	if value=="inspect" and mode=="site" and not manual_layer and not layer_locked:map_view.layer=layer_before_plan
	mode=value;selected_project="";selection=[];inspector_key="";map_view.plan_result={};map_view.plan_tile="";refresh()
func project_for_cell(key:String) -> String:return str(game.s.cells.get(key,{}).get("building",""))
func on_cell(key:String,_dragging:bool):
	issue_index=-1
	if not game.s.cells.has(key):return
	if decision_kind=="tender" and game.s.draft.get("kind","")=="development":
		if game.s.cells[key].building!="" or "tile.water" in game.s.cells[key].traits:message("请选择空闲陆地。比较中不会自动购买。");return
		var err=game.create_tender([key],selected_base)
		if err!="":message(err);return
		selection=[key];map_view.selected=selection;workbench.rebuild();map_view.ensure_visible(key);save_game();return
	if decision_kind!="":
		map_view.hovered=key;message("正在比较规则。可继续平移地图或选择当前方案；返回后检查建筑。");return
	var id=project_for_cell(key)
	if id!="":
		selected_project=id;mode="inspect";selection=[];inspector_key="";map_view.plan_result={};map_view.plan_tile="";refresh();map_view.ensure_visible(key);return
	if game.s.phase!="play" or "tile.water" in game.s.cells[key].traits:return
	if mode!="site":
		hovered_tile=key;message(key+" · "+game.c.tags_text(game.s.cells[key].traits)+"。从下方建设工具选择用途。");return
	selection=[key];selected_project="";hovered_tile=key;refresh();map_view.ensure_visible(key)
func on_hover(key:String):
	if key==hovered_tile:return
	hovered_tile=key
	if not game.s.cells.has(key):return
	var id=project_for_cell(key);var info=map_view.view_values.get(key,{})
	toast.text=(game.project_name(id)+" · " if id!="" else "")+game.c.tags_text(game.s.cells[key].traits)
	if info.has("reason"):toast.text+="  /  "+str(info.reason)
	elif info.get("label","")!="":toast.text+="  /  "+info.label
	if mode=="site" and selection.is_empty() and decision_kind=="":hover_pending=true;hover_wait=.18
func open_tender():
	var e=game.create_tender(selection,selected_base)
	if e!="":message(e);return
	save_game();Dialogs.tender(self)
func request_refit(id:String,slot:String):
	var e=game.create_refit(id,slot)
	if e!="":message(e);return
	save_game();Dialogs.tender(self)
func request_scope(kind:String,host:String):
	var e=game.create_block_offer(kind,host)
	if e!="":message(e);modal_subtitle.text=e;return
	save_game();Dialogs.tender(self)
func accept_offer(id:String):
	var ids=game.playable_ids();var kind=game.s.draft.kind;var host=game.s.draft.host
	var e=game.accept_offer(id)
	if e!="":message(e);return
	if kind=="development":
		for bid in game.playable_ids():
			if not bid in ids:selected_project=bid
	elif kind=="refit":selected_project=host
	display_pending_tick=int(game.s.simulation_tick)
	decision_kind="";workbench.hide();mode="inspect";selection=[];inspector_key="";map_view.plan_result={};map_view.plan_tile=""
	save_game();overlay.hide();refresh();play_cue("confirm")
	message("已签约。费用已付，选择时间档位后按共享施工能力建设。" if kind=="development" else "方案已更新。看看它在地图上改变了什么。")
func act(e:String):
	if e!="":message(e);return
	display_pending_tick=int(game.s.simulation_tick)
	save_game();inspector_key="";refresh()
func advance_checkpoint():Beta.set_mode(self,"advance")
func do_pulse(_months:float):Beta.set_mode(self,"advance")

func alert_action(index:int):
	if index>=alerts.size():return
	overlay.hide();issue_index=index;selected_project="";mode="inspect";selection=[];inspector_key=""
	var action=str(alerts[index].action)
	if action=="inspect" and alerts[index].target!="":selected_project=alerts[index].target;issue_index=-1
	var kind={"homestead":"population","agriculture":"food","power_station":"energy","yard":"material","medical_clinic":"medical","commercial_center":"commercial","garden":"entertainment","inspect":"employment"}.get(action,"normal")
	map_view.layer=kind;manual_layer=true;refresh()
	if selected_project!="":map_view.focus_tile(game.s.projects[selected_project].tile)

func handle_alert(a:Dictionary):
	if a.action in game.c.v040.progression.tier1:select_base(a.action)
	elif a.action in game.c.projects:desk.open("city040","development");message("选择现有建筑，沿成长路线升级为"+game.c.projects[a.action].name+"。")
	elif a.action=="inspect":selected_project=a.target;mode="inspect";refresh()
	elif a.action=="policies":Dialogs.block(self)
	elif a.action=="scope":desk.open("city040","permits")
	elif a.action=="population":Dialogs.population(self)
	else:Dialogs.goals(self)
func message(text:String):
	feedback=text
	if toast!=null:toast.text=text
func sound():
	if not game.s.settings.get("sound",true) or chime==null:return
	var data=PackedByteArray();var rate=22050
	for i in 5500:
		var t=float(i)/rate;var envelope=exp(-t*18)*minf(1,t*70);var v=int(5000*envelope*(sin(TAU*523.25*t)+.4*sin(TAU*784.*t)));data.append(v&255);data.append((v>>8)&255)
	var audio=AudioStreamWAV.new();audio.format=AudioStreamWAV.FORMAT_16_BITS;audio.mix_rate=rate;audio.data=data;chime.stream=audio;chime.play()
func save_game():
	if ignore_save or game.s.is_empty():return
	if not beta_background:game.s.wall_mark=maxf(game.s.wall_mark,Time.get_unix_time_from_system())
	var e=Store.write(game.s,save_path)
	if e!="":message(e)
func _process(delta):
	if test_mode or game.s.is_empty() or closing or overlay==null:return
	var now=Time.get_unix_time_from_system()
	# Engine delta can be capped under slow rendering. Monotonic elapsed time
	# is the sole online clock; pause/focus/decision boundaries reset its origin.
	capture_beta_seconds()
	if beta_can_run() and beta_frame_seconds>=0.1:flush_beta_time()
	last_wall=now;save_elapsed+=delta;ui_elapsed+=delta
	var boundary=str(game.s.get("beta_time",{}).get("pause_reason",""))+":"+str(game.s.get("beta_time",{}).get("local_elapsed_seconds",0))
	if beta_mandatory_pending() and boundary!=beta_boundary_seen and not overlay.visible and decision_kind=="":
		beta_boundary_seen=boundary;open_beta_settlement()
	if hover_pending:
		hover_wait-=delta
		if hover_wait<=0:
			hover_pending=false
			if mode=="site" and selection.is_empty() and decision_kind=="":Panels.inspector(self);layout_ui()
	if save_elapsed>=5:save_elapsed=0;save_game()
	if ui_elapsed>=1:
		ui_elapsed=0;refresh_stats()
		if not overlay.visible and decision_kind=="" and last_revision!=game.revision:refresh()
		if decision_kind!="" and workbench.preview_version!=planner.version(game):workbench.update_preview()
		if save_path=="user://input_smoke031.json" or "--input032" in OS.get_cmdline_user_args():dump_input_layout()
func _notification(what):
	if what==NOTIFICATION_WM_CLOSE_REQUEST:flush_beta_time();closing=true;save_game();save_display_settings();get_tree().quit()
	elif what in [NOTIFICATION_APPLICATION_PAUSED,NOTIFICATION_APPLICATION_FOCUS_OUT]:
		if game.s.is_empty() or beta_background:return
		flush_beta_time();beta_background=true;beta_background_wall=Time.get_unix_time_from_system();game.s.wall_mark=beta_background_wall;save_game();save_display_settings()
	elif what in [NOTIFICATION_APPLICATION_RESUMED,NOTIFICATION_APPLICATION_FOCUS_IN]:
		if not beta_background or game.s.is_empty():return
		beta_background=false;game.s.wall_mark=beta_background_wall;game.resume_time(Time.get_unix_time_from_system());beta_frame_seconds=0.0;reset_beta_clock()
		if desk!=null:desk.update_hud()

func _unhandled_key_input(event):
	if not event is InputEventKey or not event.pressed or event.echo:return
	var focus=get_viewport().gui_get_focus_owner()
	if (focus is LineEdit or focus is TextEdit) and event.keycode!=KEY_ESCAPE:return
	match event.keycode:
		KEY_ESCAPE:
			if screenshot_mode:screenshot_mode=false;layout_ui()
			elif layer_picker.visible:layer_picker.hide()
			elif overlay.visible:close_modal()
			elif desk.is_open():desk.back()
			elif decision_kind!="":exit_workbench()
			elif build_palette.visible:build_palette.hide()
			else:set_mode("inspect")
		KEY_1:if not overlay.visible and decision_kind=="":set_mode("inspect")
		KEY_2:if not overlay.visible and decision_kind=="":select_base(selected_base)
		KEY_F8:Dialogs.debug(self)
		KEY_SPACE:if not overlay.visible and decision_kind=="" and not desk.is_open():advance_checkpoint()
		KEY_P:if not overlay.visible:desk.toggle_pause()
func export_file(kind:String):
	var dialog=FileDialog.new();dialog.use_native_dialog=true;dialog.access=FileDialog.ACCESS_FILESYSTEM;dialog.file_mode=FileDialog.FILE_MODE_SAVE_FILE;dialog.filters=PackedStringArray(["*.json ; JSON"]);dialog.current_file="MET_040_"+kind+".json";add_child(dialog)
	dialog.file_selected.connect(func(path):
		var file=FileAccess.open(path,FileAccess.WRITE)
		if file!=null:file.store_string(Store.encode(game.s) if kind=="save" else JSON.stringify({"build":game.s.build,"state":game.s,"feedback":feedback},"\t"));file.close();message("已导出："+path)
		else:message("导出失败。")
		dialog.queue_free())
	dialog.canceled.connect(func():dialog.queue_free());dialog.popup_centered_ratio(.7)
func load_saved(path:String):
	if desk.is_open():desk.close()
	var result=Store.decode(FileAccess.get_file_as_string(path))
	if not result.ok:message(result.error);return
	var migrated=game.migrate_state(result.state)
	if not migrated.ok:message(migrated.error);return
	game.resume_time(Time.get_unix_time_from_system());selection=[];selected_project="";inspector_key="";ignore_save=false;save_game();overlay.hide();refresh();map_view.focus_community();message("当前版本的城市备份已载入。")
func import_file():
	var dialog=FileDialog.new();dialog.use_native_dialog=true;dialog.access=FileDialog.ACCESS_FILESYSTEM;dialog.file_mode=FileDialog.FILE_MODE_OPEN_FILE;dialog.filters=PackedStringArray(["*.json ; MET Beta 当前版本存档备份"]);add_child(dialog)
	dialog.file_selected.connect(func(path):load_saved(path);dialog.queue_free());dialog.canceled.connect(func():dialog.queue_free());dialog.popup_centered_ratio(.7)
func restart(seed_value:int):
	beta_boundary_seen="";beta_frame_seconds=0.0
	if desk.is_open():desk.close()
	ignore_save=test_mode or "--fresh" in OS.get_cmdline_user_args()
	game.new_game(seed_value,Time.get_unix_time_from_system());selection=[];selected_project="";mode="inspect";inspector_key="";celebration_shown=false;save_game();refresh();Dialogs.start(self)
func run_ui_smoke():
	var runner=load("res://tests/ui_smoke.gd").new();add_child(runner);runner.start(self)
func dump_input_layout():
	var items=[];_collect_buttons(self,items);var Paths=load("res://tests/paths.gd");var file=FileAccess.open(Paths.file("input_layout.json"),FileAccess.WRITE)
	var cells={}
	for k in ["0,-1","1,-1"]:cells[k]=[map_view.point(k).x+map_view.global_position.x,map_view.point(k).y+map_view.global_position.y]
	if file!=null:file.store_string(JSON.stringify({"buttons":items,"phase":game.s.phase,"selection":selection,"mode":mode,"desk":desk.kind,"task_tab":desk.task_tab,"layer":map_view.layer,"scroll":desk.scroll.scroll_vertical,"paused":game.s.settings.get("user_paused",false),"overlay":overlay.visible,"map_pos":[map_view.global_position.x,map_view.global_position.y],"cells":cells}));file.close()
func _collect_buttons(node:Node,items:Array):
	if node is Button and node.is_visible_in_tree():
		# Test-only geometry reports the visible clipped area, not buttons below a scroll viewport.
		var r=node.get_global_rect().intersection(get_viewport_rect());var ancestor=node.get_parent()
		while ancestor!=null:
			if ancestor is Control and ancestor.clip_contents:r=r.intersection(ancestor.get_global_rect())
			ancestor=ancestor.get_parent()
		if r.size.x>4 and r.size.y>4 and (not overlay.visible or overlay.is_ancestor_of(node)):
			items.append({"name":str(node.name),"text":node.get_meta("label",node.text),"tooltip":node.tooltip_text,"x":r.get_center().x,"y":r.get_center().y,"disabled":node.disabled})
	for child in node.get_children():_collect_buttons(child,items)

func load_display_settings():
	var path="user://display032.json"
	if FileAccess.file_exists(path):
		var value=JSON.parse_string(FileAccess.get_file_as_string(path))
		if value is Dictionary:
			for k in display_settings:
				if value.has(k):display_settings[k]=value[k]
	W.text_scale=clampf(float(display_settings.text_scale),1.,1.5);W.high_contrast=bool(display_settings.high_contrast)
	apply_frame_limit()
func apply_frame_limit():
	var chosen=int(display_settings.frame_limit)
	Engine.max_fps=chosen if chosen in [30,60] else (30 if OS.has_feature("mobile") or "--mobile-quality" in OS.get_cmdline_user_args() else 60)
func save_display_settings():
	if ignore_save:return
	var f=FileAccess.open("user://display032.json",FileAccess.WRITE)
	if f!=null:f.store_string(JSON.stringify(display_settings,"\t"))
func request_layout():
	if resize_pending:return
	resize_pending=true;call_deferred("resize_interface")
func resize_interface():
	resize_pending=false
	var win=get_window();var density=1.0
	if OS.get_name() in ["Android","iOS"]:
		var dpi=DisplayServer.screen_get_dpi();density=maxf(1.,float(dpi)/160.)
	elif DisplayServer.screen_get_scale()>1.:density=DisplayServer.screen_get_scale()
	var logical=Vector2(win.size)/density
	var design_size=Vector2i(maxi(320,int(logical.x)),maxi(240,int(logical.y)))
	if win.content_scale_size!=design_size:win.content_scale_size=design_size
	layout_ui()
func _rect(node:Control,rect:Rect2):
	node.position=rect.position;node.size=rect.size
func time_hud_width() -> float:
	return (440. if W.text_scale>1.2 else 380.) if compact else (458. if W.text_scale>1. else 416.)
func layout_ui():
	if top_panel==null or desk==null:return
	var vp=get_viewport_rect().size;var area=Rect2(Vector2.ZERO,vp)
	if OS.get_name() in ["Android","iOS"]:
		var physical=DisplayServer.get_display_safe_area();var screen=DisplayServer.screen_get_size()
		if screen.x>0 and screen.y>0 and physical.size.x>0:
			var ratio=vp/Vector2(screen);area=Rect2(Vector2(physical.position)*ratio,Vector2(physical.size)*ratio)
	safe_area=area
	var was=compact;compact=OS.has_feature("mobile") or "--mobile-quality" in OS.get_cmdline_user_args() or area.size.x<1100 or area.size.y<620 or bool(display_settings.force_compact) or (W.text_scale>1.2 and area.size.x<1380)
	W.touch=compact;map_view.compact=compact;map_view.reduce_motion=bool(display_settings.reduced_motion);map_view.show_all_links=bool(display_settings.all_links);map_view.ui_font_scale=W.text_scale
	sync_header_values()
	var th=56. if compact else 82.;var bh=64. if compact else 98.
	var side=296. if compact else (440. if W.text_scale>1. else 424.)
	var timewidth=time_hud_width()
	var in_panel=inspector_panel.visible or decision_kind!="" or (desk.is_open() and not desk.is_fullscreen())
	var left_width=area.size.x-side if compact and in_panel else area.size.x
	var topstyle=W.box(W.PANEL,W.OUTLINE,7)
	topstyle.content_margin_top=6;topstyle.content_margin_bottom=6;topstyle.content_margin_left=12;topstyle.content_margin_right=10
	top_panel.add_theme_stylebox_override("panel",topstyle)
	_rect(top_panel,Rect2(area.position+(Vector2.ZERO if compact else Vector2(16,14)),Vector2(area.size.x-timewidth if compact else 550,56 if compact else 64)))
	header_nodes.brand.visible=not compact and W.text_scale<=1.2
	for key in ["finance","people"]:
		var bw=(96 if key=="finance" else 88) if compact else (150 if key=="finance" else 128)
		header_nodes[key].custom_minimum_size=Vector2(bw,44 if compact else 48)
		header_nodes[key].add_theme_stylebox_override("normal",W.box(Color("172329"),Color("172329"),6))
	for pair in [["policy","policy","政策"],["ledger","ledger","任务"],["menu","settings",""]]:
		var btn=header_nodes[pair[0]];btn.text="" if compact else pair[2];btn.icon=Art.icon(pair[1],W.TEXT)
		btn.custom_minimum_size=Vector2(40 if compact else (40 if pair[2]=="" else 96),40)
	if compact:
		if header_nodes.policy.get_parent()!=header_nodes.row:header_nodes.policy.reparent(header_nodes.row)
	else:
		if header_nodes.policy.get_parent()!=header_nodes.toolrow:header_nodes.policy.reparent(header_nodes.toolrow)
	header_nodes.ledger.visible=not compact;header_nodes.policy.visible=not compact or area.size.x>=720;header_nodes.people.visible=not compact or area.size.x>=720
	for l in [cash_label,governance_label]:l.add_theme_font_size_override("font_size",int((14 if compact else 15)*W.text_scale));l.add_theme_color_override("font_color",W.TEXT)
	dt_label.add_theme_font_size_override("font_size",int(15*W.text_scale))
	var bs=W.box(W.PANEL,W.OUTLINE,7);bs.content_margin_left=8;bs.content_margin_right=8;bs.content_margin_top=4;bs.content_margin_bottom=4
	bottom_panel.add_theme_stylebox_override("panel",bs)
	var dock_w=580.;var dock_x=maxf(16.,(area.size.x-timewidth-dock_w)*.5)
	_rect(bottom_panel,Rect2(area.position+Vector2(8,area.size.y-64),Vector2(minf(416,left_width-116),56)) if compact else Rect2(area.position+Vector2(dock_x,area.size.y-92),Vector2(dock_w,78)))
	for button in category_buttons:button.visible=not compact
	bottom_nodes.place.visible=not compact
	bottom_nodes.place.custom_minimum_size=Vector2(144 if W.text_scale<=1.2 else 156,48)
	for name in ["build","layers","stats","growth"]:
		bottom_nodes[name].visible=(compact and not (in_panel and name=="stats")) or (name=="growth" and not compact)
		bottom_nodes[name].custom_minimum_size.x=(64 if W.text_scale<=1.2 else 80) if compact else (48 if name=="growth" else 94)
		if compact:
			bottom_nodes[name].icon=null
			if name=="growth":bottom_nodes[name].text="发展"
		elif name=="growth":bottom_nodes[name].text="";bottom_nodes[name].icon=Art.icon("build",W.TEXT)
	resource_row.hide();clock_label.hide()
	tool_panel.visible=not compact and not screenshot_mode and not desk.is_fullscreen()
	_rect(tool_panel,Rect2(area.position+Vector2(16,94),Vector2(520,40)))
	legend_panel.visible=map_view.layer!="normal" and not screenshot_mode and not desk.is_fullscreen() and not (compact and area.size.y<480)
	_rect(legend_panel,Rect2(area.position+Vector2(16,th+(12 if compact else 66)),Vector2(minf(246,left_width-32),174)))
	if compact:_rect(inspector_panel,Rect2(area.position+Vector2(area.size.x-side,0),Vector2(side,area.size.y)))
	else:_rect(inspector_panel,Rect2(area.position+Vector2(area.size.x-side-16,94),Vector2(side,area.size.y-188)))
	inspector.custom_minimum_size.x=0
	if decision_kind!="":
		if compact:_rect(workbench,Rect2(area.position+Vector2(area.size.x-side,0),Vector2(side,area.size.y)))
		else:
			var wh=300. if W.text_scale<=1. else 340.
			_rect(workbench,Rect2(area.position+Vector2(16,area.size.y-bh-wh-8),Vector2(area.size.x-32,wh)))
	var camera=Rect2(area.position+Vector2(16,th+18),Vector2(maxf(100,left_width-32),maxf(80,area.size.y-th-bh-36)))
	if not compact:
		if in_panel and decision_kind=="":camera.size.x-=side+24
		elif desk.rail_expanded:camera.size.x-=316
		if decision_kind!="":camera.size.y=maxf(100,camera.size.y-(310. if W.text_scale<=1. else 350.))
	map_view.available_rect=camera
	notice_panel.hide()
	_rect(toast,Rect2(area.position+Vector2(24,area.size.y-bh-32),Vector2(minf(maxf(120,left_width-80),maxf(90,toast.get_theme_default_font().get_string_size(toast.text,HORIZONTAL_ALIGNMENT_LEFT,-1,int(14*W.text_scale)).x+28)),36)))
	toast.visible=not compact and decision_kind=="" and not screenshot_mode and not desk.is_open() and toast.text!=""
	if build_palette.visible:
		var ph=minf(300. if compact else 248.*W.text_scale,area.size.y-th-bh-24);var pw=minf(720.,left_width-24.-(side+16. if not compact and inspector_panel.visible else 0.))
		_rect(build_palette,Rect2(area.position+Vector2(16,area.size.y-bh-ph-8),Vector2(pw,ph)))
	if layer_picker.visible:_rect(layer_picker,Rect2(area.position+Vector2(16,th+60),Vector2(minf(296,left_width-24),maxf(150,minf(530,area.size.y-th-bh-72)))))
	var mw=minf(1020,area.size.x-32);var mh=minf(680,area.size.y-32)
	modal_panel.custom_minimum_size=Vector2(mw,mh);modal_panel.size=Vector2(mw,mh)
	modal_scroll.custom_minimum_size=Vector2(0,maxf(60,mh-140));modal_scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;modal_body.custom_minimum_size.x=0
	if screenshot_mode:
		top_panel.hide();bottom_panel.hide();legend_panel.hide();notice_panel.hide();build_palette.hide();inspector_panel.hide();toast.hide();layer_picker.hide()
	else:
		top_panel.visible=not(compact and in_panel);bottom_panel.visible=not desk.is_fullscreen()
	if desk.is_open():inspector_panel.hide()
	refresh_build_dock()
	desk.relayout(area)
	if was!=compact:
		if inspector031.visible:inspector031.signature="";inspector031.call_deferred("update_view",game,shot,selected_project)
		theme=W.theme();_retune(self)
		if build_palette.visible:rebuild_palette()
		if decision_kind!="":workbench.rebuild()
		if desk.is_open():desk.call_deferred("render",true)
	refresh_legend()
func _retune(node:Node):
	if node is Label or node is Button:
		var base=node.get_meta("font_base",16)
		node.add_theme_font_size_override("font_size",int(maxi(17 if compact else 14,int(base))*W.text_scale))
	for child in node.get_children():_retune(child)
func option_row(parent:Node,gap:int=10) -> Container:
	return W.vbox(parent,gap) if compact else W.hbox(parent,gap)
func toggle_palette(category:String):
	if desk.is_open():desk.close()
	if decision_kind!="":exit_workbench()
	if palette_category==category and build_palette.visible:build_palette.hide();return
	palette_category=category;build_palette.show();layer_picker.hide();rebuild_palette();layout_ui()
func rebuild_palette():
	W.clear(build_palette);build_buttons={};var root=W.vbox(build_palette,10)
	root.add_child(W.label({"homestead":"农居 · Tier 1","neighborhood":"邻里中心 · Tier 1","workshop":"作坊 · Tier 1","categories":"新建 · 选择起点建筑"}.get(palette_category,"建设"),16))
	var scroll=ScrollContainer.new();scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;root.add_child(scroll)
	var grid=GridContainer.new();grid.columns=1 if compact else 3;grid.add_theme_constant_override("h_separation",10);grid.add_theme_constant_override("v_separation",10);grid.size_flags_horizontal=Control.SIZE_EXPAND_FILL;scroll.add_child(grid)
	var pool=["homestead","neighborhood","workshop"] if palette_category=="categories" else ([palette_category] if palette_category in ["homestead","neighborhood","workshop"] else [])
	for id in pool:build_buttons[id]=UI031.building(grid,game,id,func():select_base(id))
	if palette_category!="categories":root.add_child(W.button("显示三个起点建筑",func():palette_category="categories";rebuild_palette();layout_ui()))
func toggle_layers():
	if layer_picker.visible:layer_picker.hide();return
	build_palette.hide();W.clear(layer_picker);var v=W.vbox(layer_picker,4)
	var sc=ScrollContainer.new();sc.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;sc.size_flags_vertical=Control.SIZE_EXPAND_FILL;v.add_child(sc);var list=W.vbox(sc,6);list.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	for kind in ["normal","terrain","medical","education","commercial","entertainment","pollution","heat","population","employment","unemployed","food","material","goods","energy","operator","change"]:
		var title=MapData.NAMES[kind];var b=W.button(("● " if map_view.layer==kind else "")+title,func():set_layer(kind));b.clip_text=true;list.add_child(b)
	layer_picker.show();layout_ui()
func set_layer(kind:String,automatic:bool=false):
	if automatic and (layer_locked or manual_layer):return
	if not automatic:manual_layer=true
	map_view.layer=kind;map_view.invalidate();layer_picker.hide();play_cue("layer");refresh_legend();layout_ui()
func refresh_legend():
	if legend_title==null:return
	var kind=map_view.layer
	var short_names={"normal":"图层","terrain":"地形","medical":"医疗","education":"教育","commercial":"购物","entertainment":"休闲","pollution":"环境","heat":"余热","population":"人口","employment":"就业","unemployed":"就业","food":"粮食","material":"材料","goods":"制品","energy":"电力","operator":"运营","change":"变化"}
	layer_button.text=short_names.get(kind,"图层")
	layer_button.tooltip_text="分析图层 · "+MapData.NAMES.get(kind,kind);layer_button.custom_minimum_size.x=84
	lock_button.modulate=Color("d8b776") if layer_locked else Color.WHITE
	legend_title.text=MapData.NAMES.get(kind,kind)+( " · 已锁定" if layer_locked else "")
	var stamp="下周期预计" if map_view.display_shot.get("forecast",false) else "最近结算 · 实际"
	var text=stamp+"\n"
	if kind in MapData.SERVICES:
		text+="实线：可达范围\n住宅色条：实际服务率\n斜纹：可达但容量不足\n刻度 0 / 50 / 75 / 100%
具体目标见发展页；空地不评分"
	elif kind=="pollution":text+="环境负担指数  0 ━ 2+\n青：净化；赭：正贡献\n显示刻度，不是健康阈值"
	elif kind=="heat":text+="热场贡献  0 ━ 4+\n连线：本周期实际分配\n覆盖不等于每栋都得到热量"
	elif kind=="population":text+="绿色：已居住 / 住房\n蓝色：到岗 / 岗位\n点击住区看真实工作连接"
	elif kind=="employment":text+="到岗人数 / 岗位容量\n绿：到岗充分；赭：缺人\n不以运行率冒充人数"
	elif kind=="unemployed":text+="住宅标记：待就业比例\n点住宅查看工作去向"
	elif kind in ["food","material","goods","energy"]:text+="标记：产 − 用\n连线：实际调拨，不是货车路线\n点账本查看库存与丢弃"
	elif kind=="operator":text+="外圈 / 徽记：运营组织\n保留建筑功能原色"
	elif kind=="change":text+="同一基线的两种下一周期\n+ 实线：获益；− 虚线：受损\n仅为预测，尚未花费"
	elif kind=="terrain":text+="地形边框：额定优势\n点位置看真实投入和后果\n不是综合推荐排名"
	if map_view.hidden_links>0:text+="\n另有%d条关系（详情可展开）"%map_view.hidden_links
	legend_body.text=text
func layer_details():
	var kind=map_view.layer;open_modal(MapData.NAMES.get(kind,kind)+" · 来源", "数据只读。可达性与实际分配分开；缺数据不画成零。")
	modal_body.add_child(W.button("关系线："+("全部" if display_settings.all_links else "最多6条"),func():display_settings.all_links=not display_settings.all_links;save_display_settings();map_view.show_all_links=display_settings.all_links;layer_details()))
	var tile=game.s.projects.get(selected_project,{}).get("tile",hovered_tile)
	if MapData.SERVICES.has(kind):
		var total=0.;var delivered=0.;var homeless=0.
		for gid in view_model.get("groups",{}):
			var group=view_model.groups[gid];total+=float(group.count);delivered+=float(view_model.metrics.get("service_covered",{}).get(gid,{}).get(kind,0))
			if group.host=="homeless":homeless+=float(group.count)
		W.section(modal_body,"全地区实际分配","%.1f / %.1f人；地图外待安置 %.1f人仍计入分母。"%[delivered,total,homeless])
		if game.s.cells.has(tile):
			var x=MapData.service(view_model,tile,kind);W.section(modal_body,"所选地块 "+tile,x.reason+"；服务 %.1f / %.1f人"%[x.served,x.people])
			for source in x.sources:
				W.section(modal_body,game.project_name(source),"可达性 %.2f；来源保留，不因中转复制容量。"%x.sources[source])
	elif game.s.cells.has(tile) and kind in ["pollution","heat"]:
		for e in view_model.tiles[tile].fields.get(kind,[]):W.section(modal_body,game.project_name(e.source_id),"贡献 %+.4f · %s"%[e.value,e.field_id])
		if kind=="pollution" and selected_project!="":
			var aggregate=str(game.compiled.config[selected_project].get("pollution_aggregate","sum"));var interpreted=game.Rules.aggregate(view_model.tiles[tile].fields.get("pollution",[]),aggregate)
			W.section(modal_body,"此建筑的读取方式",aggregate+" → %.4f。地图色带是原始贡献，受体可按自身规则解释。"%interpreted)
		if kind=="heat":W.section(modal_body,"本周期额度","产生 %.4f；实际使用 %.4f"%[view_model.audit.get("heat_generated",0),view_model.audit.get("heat_used",0)])
	elif selected_project!="":
		for l in MapData.links(game,view_model,selected_project,kind):W.section(modal_body,game.project_name(l.source)+" → "+game.project_name(l.target),"%s  %.3f"%[MapData.NAMES.get(l.kind,l.kind),l.amount])
	else:W.section(modal_body,"先选一栋建筑","返回后点选建筑，再打开来源查看相关居民、提供方和分配记录。")
func show_workbench():
	if desk.is_open():desk.close()
	overlay.hide();build_palette.hide();layer_picker.hide();decision_kind="tender";inspector_panel.hide();workbench.show_tender();workbench.show();layout_ui();refresh_stats()
	if game.s.draft.get("tile","")!="":map_view.ensure_visible(game.s.draft.tile)
func show_policy(slot:String="Tax"):
	if decision_kind!="":exit_workbench()
	desk.open("policies",slot)
func policy_categories():
	show_policy()
func exit_workbench():
	if decision_kind=="tender":game.Draft.remember(game)
	decision_kind="";workbench.hide();map_view.plan_result={};map_view.plan_tile="";inspector_key="";save_game();refresh();play_cue("back")
func play_cue(id:String):
	if chime==null or not game.s.settings.get("sound",true):return
	var path="res://assets/audio030/"+id+".wav"
	if not FileAccess.file_exists(path):
		if ResourceLoader.exists(path):chime.stream=load(path);chime.play()
		return
	var data=FileAccess.get_file_as_bytes(path)
	var audio=AudioStreamWAV.new();audio.format=AudioStreamWAV.FORMAT_16_BITS;audio.mix_rate=22050;audio.data=data.slice(44);chime.stream=audio;chime.play()
func display_options():
	open_modal("显示与交互","界面文字与地图分开缩放。更大的字号使用滚动，不缩小触摸按钮。")
	var row=option_row(modal_body,8)
	for factor in [1.0,1.25,1.5]:row.add_child(W.button("字号 %d%%"%int(factor*100),func():display_settings.text_scale=factor;apply_display();display_options()))
	for pair in [["reduced_motion","减少动态"],["high_contrast","提高对比"],["force_compact","紧凑触摸布局"]]:
		modal_body.add_child(W.button(pair[1]+"："+("开" if display_settings[pair[0]] else "关"),func():display_settings[pair[0]]=not display_settings[pair[0]];apply_display();display_options()))
	var rates=option_row(modal_body,8)
	for rate in [0,30,60]:
		rates.add_child(W.button("自动帧率" if rate==0 else "%d FPS"%rate,func():display_settings.frame_limit=rate;apply_display();display_options()))
func apply_display():
	apply_frame_limit()
	W.text_scale=clampf(float(display_settings.text_scale),1.,1.5);W.high_contrast=bool(display_settings.high_contrast);theme=W.theme();save_display_settings();inspector_key="";_retune(self);layout_ui();refresh();if desk.is_open():desk.render(true)
func run_colony041():
	var runner=load("res://tests/colony041.gd").new();add_child(runner);runner.start(self)
func run_ui030():
	var script=load("res://tests/ui030.gd");var runner=script.new();add_child(runner);runner.start(self)

func city_status():
	open_modal("城市近况","先处理真正需要行动的事，完整账本始终可查。")
	for i in alerts.size():
		W.section(modal_body,alerts[i].title,alerts[i].detail)
		modal_body.add_child(W.button("查看这个问题的解法",func():alert_action(i)))
	for r in ["food","material","goods"]:
		var x=Presentation.resource(game,shot,r);W.section(modal_body,Presentation.RES[r],"库存 %.1f；产 %.1f / 用 %.1f / 弃 %.1f 每来生月。%s"%[x.stock,x.produced,x.consumed,x.lost,x.status])
	modal_body.add_child(W.button("完整来源账本",func():Dialogs.resources(self)))
	modal_body.add_child(W.button("地区政策与区域方案",policy_categories))

func visual_feedback():
	if observed_seed!=int(game.s.seed):
		observed_seed=int(game.s.seed);observed_status={};first_operations={};observed_complete=game.s.get("city_goal",{}).get("completed",false)
	for id in game.playable_ids():
		var p=game.s.projects[id]
		if observed_status.get(id,"")=="construction" and p.status=="running":
			first_operations[id]=int(game.s.simulation_tick);map_view.flash_project_id=id;map_view.flash_time=2.;play_cue("complete")
			message(game.project_name(id)+"已建成。首次正式运营结果将在后续周期显示。")
		observed_status[id]=p.status
	for id in first_operations.keys():
		if not game.s.projects.has(id):first_operations.erase(id);continue
		if int(game.s.simulation_tick)<=int(first_operations[id]):continue
		var p=game.s.projects[id];var outputs=[]
		for resource in p.get("outputs_last",{}):
			if float(p.outputs_last[resource])>.001:outputs.append(Presentation.RES.get(resource,resource)+" %.1f"%p.outputs_last[resource]+("" if resource=="energy" else "/月"))
		var served=0.
		for l in game.s.metrics.get("service_links",[]):
			if l.source==id:served+=float(l.people)
		if outputs.is_empty() and served<=.001:continue
		message(game.project_name(id)+"首次运营 · "+(" / ".join(outputs.slice(0,2)) if not outputs.is_empty() else "实际服务 %.1f人次"%served));first_operations.erase(id);map_view.flash_project_id=id;map_view.flash_time=2.
	var complete=game.s.get("city_goal",{}).get("completed",false)
	if complete and not observed_complete:play_cue("complete")
	observed_complete=complete

func _shell_tint(node:Node):
	if node is Label:node.add_theme_color_override("font_color",Color("E2EDE5"))
	if node is Button:
		for key in ["font_color","font_hover_color","font_pressed_color"]:node.add_theme_color_override(key,Color.WHITE)
		var box=W.button_box(Design031.color("shell"),Design031.color("shell"));node.add_theme_stylebox_override("normal",box)
		node.add_theme_stylebox_override("hover",W.button_box(Color("365E4F"),Color("B9D0C5")))
		if node.icon!=null:
			var found="settings"
			if node==header_nodes.get("policy"):found="policy"
			elif node==header_nodes.get("ledger"):found="ledger"
			node.icon=Art.icon(found,Color.WHITE)
	for child in node.get_children():_shell_tint(child)
func run_ui031():
	var runner=load("res://tests/ui031.gd").new();add_child(runner);runner.start(self)

func run_ui031_regression():
	var runner=load("res://tests/ui_regression031.gd").new();add_child(runner);runner.start(self)

func run_ui032():
	var runner=load("res://tests/ui032.gd").new();add_child(runner);runner.start(self)

func run_ui040():
	var runner=load("res://tests/ui040.gd").new();add_child(runner);runner.start(self)

func run_touch032():
	var tester=load("res://tests/touch032.gd").new();add_child(tester);tester.start(self)

func run_visual040():
	var runner=load("res://tests/visual040.gd").new();add_child(runner);runner.start(self)

func beta_decisions_pending() -> bool:
	if not Beta.pending(game).is_empty():return true
	for event in game.Themes.view(game).get("events",[]):
		if event.get("status","")=="offered":return true
	return false
func beta_mandatory_pending() -> bool:
	var reason=str(game.s.get("beta_time",{}).get("pause_reason",""))
	return game.s.phase=="play" and reason!="" and reason not in ["manual","resume","offline","background","返回城市；准备好后继续"]
func open_beta_settlement():
	if game.s.phase!="play":return
	game.s.settings.user_paused=true
	desk.open("city040","settlement")
func resume_beta_normal():
	reset_beta_clock()
	if beta_decisions_pending():message("请先处理本次已保存的奖励和待选事件。");return
	game.Cycles.acknowledge(game);game.s.beta_time.pause_reason="";game.s.settings.user_paused=false
	game.Clock.set_mode(game,"normal");overlay.hide()
	if desk.is_open():desk.close()
	save_game();refresh()
func on_beta_source(source:Dictionary):
	if str(source.get("kind",""))=="commission":desk.open("tasks");return
	if str(source.get("kind",""))=="capstone":desk.open("city040","block");return
	var id=str(source.get("building_id",source.get("source_id","")))
	if source.get("kind","")=="trade":desk.open("city040","market");return
	if game.s.projects.has(id):
		if desk.is_open():desk.close()
		selected_project=id;mode="inspect";inspector_key="";map_view.layer=str(source.get("layer","normal"));refresh();map_view.focus_tile(str(game.s.projects[id].tile))
	else:desk.open("resource",str(source.get("layer","food")) if str(source.get("layer","")) in ["food","material","goods","energy"] else "food")
func open_city_messages():
	open_modal("城市消息","来自实际城市状态的记录；紧急问题仍会显示地图告警。")
	for row in feedback_layer.history():
		var entry=row
		modal_body.add_child(W.button(str(entry.get("text","城市记录")),func():overlay.hide();on_beta_source(entry)))
func run_beta_ui():
	var runner=load("res://tests/beta_ui.gd").new();add_child(runner);runner.start(self)

func open_city_records():
	open_modal("已结算的城市记录","只读留念；不切换旧城、不继承资源或构筑。")
	var records=game.Block.list_archives()
	if records.is_empty():modal_body.add_child(W.label("还没有已结算记录。DT 耗尽后可在城市尾声保存。",16,W.MUTED))
	for item in records:
		var row=item
		modal_body.add_child(W.button(str(row.get("name","城市"))+" · "+Beta.date(float(row.get("local_elapsed_seconds",0))),func():
			open_modal(str(row.get("name","城市记录")),"只读本局记录")
			modal_body.add_child(W.label("人口 %.1f · 通货 %.1f · DT 开销 %.2f"%[float(row.get("population",0)),float(row.get("currency",0)),float(row.get("dt_spent",0))],17))
			modal_body.add_child(W.label("主题："+str(row.get("theme",{}).get("id","未选择"))+" · "+str(row.get("capstone",{}).get("name","无定型建筑")),16,W.MUTED))
			var copies={}
			for instance in row.get("bonus_instances",{}).values():
				var bid=str(instance.get("bonus_id",""));copies[bid]=int(copies.get(bid,0))+1
			for bid in copies:modal_body.add_child(W.label(str(game.c.bonuses.get(bid,{}).get("name",bid))+" · 装配%d份 · 蓝图等级%d"%[copies[bid],int(row.get("blueprint_levels",{}).get(bid,0))],15,W.GREEN))
			for event in row.get("history",[]).slice(-20):modal_body.add_child(W.label(str(event.get("message",event.get("text",event.get("kind","城市记录")))),14,W.MUTED))))

func flush_beta_time():
	capture_beta_seconds()
	if beta_frame_seconds<=0 or game.s.is_empty():return
	var seconds=beta_frame_seconds;beta_frame_seconds=0.0
	if not game.s.settings.get("user_paused",false):game.advance_active(seconds)

static func beta_active_interval(previous_usec:int,now_usec:int,eligible:bool) -> float:
	return maxf(0,float(now_usec-previous_usec)/1000000.) if eligible and previous_usec>0 else 0.0
func beta_can_run() -> bool:
	return not test_mode and not closing and not game.s.is_empty() and game.s.phase=="play" and not beta_background and overlay!=null and not overlay.visible and decision_kind=="" and desk!=null and not desk.blocks_time() and not game.s.settings.get("user_paused",false)
func capture_beta_seconds(now_usec:int=-1):
	var now=Time.get_ticks_usec() if now_usec<0 else now_usec
	beta_frame_seconds+=beta_active_interval(beta_clock_usec,now,beta_can_run())
	beta_clock_usec=now
func reset_beta_clock():
	beta_clock_usec=Time.get_ticks_usec()
