extends Control
const SiteBubble=preload("res://ui/reset/site_bubble.gd")
const DeskVisuals=preload("res://ui/reset/desk_visuals.gd")
var site_bubble
var focused_tool:String=""
var command_notice:Label
var command_notice_until:int=0
var workbench_shield:ColorRect
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
var display_settings={"text_scale":1.0,"reduced_motion":false,"high_contrast":false,"force_compact":false,"all_links":false,"task_rail_expanded":false,"citizen_bubbles":false,"frame_limit":0}
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
	# The pre-1.0 presentation contract is a full-screen desktop at 1080p or above.
	get_window().min_size=Vector2i(1920,1080)
	get_window().mode=Window.MODE_FULLSCREEN
	ensure_startup_fullscreen()
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
func ensure_startup_fullscreen() -> void:
	# The desktop backend applies the main scene's initial window flags after
	# _ready. Reapply the requested default once that initialization has settled.
	await get_tree().process_frame
	await get_tree().process_frame
	if is_inside_tree():get_window().mode=Window.MODE_FULLSCREEN
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
	var food=W.button("",func():desk.open("resource","food"),false,"粮食库存 · 点击打开供需档案");food.name="L0Food";food.set_meta("l0_item","food");header.add_child(food);header_nodes.food=food
	var food_text=W.label("",26,W.TEXT,false);food.add_child(food_text);food_text.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);food_text.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;food_text.vertical_alignment=VERTICAL_ALIGNMENT_CENTER;header_nodes.food_text=food_text
	people.set_meta("l0_item","population");people.name="L0Population"
	for pair in [["people",people,"population"],["food",food,"food"]]:
		var icon=TextureRect.new();icon.texture=load("res://ui/reset/population.svg") if pair[2]=="population" else Art.icon("food",W.GOLD);icon.anchor_top=.5;icon.anchor_bottom=.5;icon.offset_left=8;icon.offset_right=34;icon.offset_top=-13;icon.offset_bottom=13;icon.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;icon.mouse_filter=Control.MOUSE_FILTER_IGNORE;pair[1].add_child(icon);header_nodes[pair[0]+"_icon"]=icon
	governance_label.offset_left=30;food_text.offset_left=30
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
	for pair in [["home","农居","homestead"],["medical","邻里中心","neighborhood"],["production","作坊","workshop"]]:
		var b=W.flat(W.button(pair[1],func():select_base(pair[2])));b.icon=Art.thumbnail(pair[2]);b.expand_icon=true;b.add_theme_constant_override("icon_max_width",34);b.custom_minimum_size=Vector2(112,58);b.set_meta("font_base",15);b.name="BuildTool_"+str(pair[2]);b.tooltip_text=game.c.projects[pair[2]].name+" · 在地图选择建设位置";dock.add_child(b);category_buttons.append(b)
	var place=W.primary(W.button("选择建筑",primary_build_action,false,"选择起点建筑；选中地块后比较运营者方案"));place.name="BuildPrimary032";place.custom_minimum_size=Vector2(144,48);bottom_nodes.place=place;dock.add_child(place)
	var mobile_build=W.icon_button("build","建设",func():toggle_palette("categories"));dock.add_child(mobile_build);bottom_nodes.build=mobile_build
	var mobile_layers=W.icon_button("layers","图层",toggle_layers);dock.add_child(mobile_layers);bottom_nodes.layers=mobile_layers
	var mobile_stats=W.icon_button("ledger","任务",func():desk.open("tasks"));dock.add_child(mobile_stats);bottom_nodes.stats=mobile_stats
	var growth=W.icon_button("build","发展",open_archive,"城市档案 · 主题、预算、政策、市场、方案与历史");dock.add_child(growth);bottom_nodes.growth=growth
	resource_row=W.hbox(bottom,0);resource_row.hide()
	clock_label=W.label("",14,W.MUTED,false);clock_label.hide();bottom.add_child(clock_label)
	toast=W.label("",14,W.TEXT,false);toast.text_overrun_behavior=TextServer.OVERRUN_TRIM_ELLIPSIS;toast.add_theme_stylebox_override("normal",W.box(Color("172329"),Color("34474D"),8));hud_root.add_child(toast);toast.minimum_size_changed.connect(request_layout)
	build_palette=W.panel(hud_root);build_palette.hide()
	layer_picker=W.panel(hud_root);layer_picker.hide()
	workbench_shield=ColorRect.new();workbench_shield.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);workbench_shield.color=Color(.025,.035,.04,.62);workbench_shield.mouse_filter=Control.MOUSE_FILTER_STOP;hud_root.add_child(workbench_shield);workbench_shield.hide()
	workbench=Workbench.new();hud_root.add_child(workbench);workbench.setup(self);workbench.hide()
	desk=CityDesk.new();hud_root.add_child(desk);desk.setup(self);desk.rail_expanded=false
	site_bubble=SiteBubble.new();hud_root.add_child(site_bubble);site_bubble.setup(self)
	overlay=Control.new();overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(overlay)
	var shade=ColorRect.new();shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);shade.color=Color(.035,.07,.085,.66);shade.offset_top=140;overlay.add_child(shade)
	modal_center=CenterContainer.new();modal_center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);modal_center.offset_top=140;overlay.add_child(modal_center)
	modal_panel=W.panel(modal_center);modal_outer=W.vbox(modal_panel,8)
	var mh=W.hbox(modal_outer,8);var titles=W.vbox(mh,4);titles.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	modal_title=W.label("",24);titles.add_child(modal_title);modal_subtitle=W.label("",16,W.MUTED);titles.add_child(modal_subtitle)
	mh.add_child(W.button("返回",close_modal))
	modal_scroll=ScrollContainer.new();modal_scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;modal_scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;modal_outer.add_child(modal_scroll)
	modal_body=W.vbox(modal_scroll,10);modal_body.size_flags_horizontal=Control.SIZE_EXPAND_FILL;overlay.hide()
	command_notice=W.label("",16,W.TEXT);command_notice.name="CommandNotice";command_notice.set_meta("presentation_layer","command_feedback");command_notice.add_theme_stylebox_override("normal",W.box(W.RAISED,W.OUTLINE,12));command_notice.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;command_notice.vertical_alignment=VERTICAL_ALIGNMENT_CENTER;command_notice.mouse_filter=Control.MOUSE_FILTER_IGNORE;command_notice.z_index=100;add_child(command_notice);command_notice.hide()
	chime=AudioStreamPlayer.new();add_child(chime)
	for panel in [top_panel,bottom_panel,inspector_panel,workbench,legend_panel,build_palette,modal_panel]:panel.minimum_size_changed.connect(request_layout)
	wire_tool_focus(hud_root)
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
	if decision_kind=="" and not desk.is_open():refresh_context()
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
	dt_label.text="%.1f DT"%game.s.dt
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
	governance_label.text="%.0f"%population
	header_nodes.food_text.text="%.0f"%game.inventory_total("food")
	header_nodes.food_text.add_theme_color_override("font_color",W.TEXT)
	for warning in feedback_layer.summary().get("alerts",[]):
		if warning.get("cause","")=="food":header_nodes.food_text.add_theme_color_override("font_color",W.DANGER if int(warning.get("severity",0))>=2 else W.WARNING)
	header_nodes.food.tooltip_text="粮食库存 %.1f · 点击查看真实供给与消耗"%game.inventory_total("food")
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
	focused_tool=""
	var focused=get_viewport().gui_get_focus_owner()
	if focused!=null:focused.release_focus()
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
func message(text:String,show_result:bool=true):
	feedback=text
	if toast!=null:toast.text=text
	if show_result and command_notice!=null:
		command_notice.text=text;command_notice_until=Time.get_ticks_msec()+6000;command_notice.show();layout_ui()
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
	if command_notice!=null and command_notice.visible and Time.get_ticks_msec()>=command_notice_until:command_notice.hide()
	if test_mode or game.s.is_empty() or closing or overlay==null:return
	var now=Time.get_unix_time_from_system()
	# Engine delta can be capped under slow rendering. Monotonic elapsed time
	# is the sole online clock; pause/focus/decision boundaries reset its origin.
	capture_beta_seconds()
	if beta_can_run() and beta_should_submit(beta_frame_seconds):flush_beta_time()
	last_wall=now;save_elapsed+=delta;ui_elapsed+=delta
	var boundary=str(game.s.get("beta_time",{}).get("pause_reason",""))+":"+str(game.s.get("beta_time",{}).get("local_elapsed_seconds",0))
	if beta_mandatory_pending() and boundary!=beta_boundary_seen and not overlay.visible and decision_kind=="":
		beta_boundary_seen=boundary;open_beta_settlement()
	if hover_pending:
		hover_wait-=delta
		if hover_wait<=0:
			hover_pending=false
			if mode=="site" and selection.is_empty() and decision_kind=="":refresh_context();layout_ui()
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
	var dialog=FileDialog.new();dialog.use_native_dialog=true;dialog.access=FileDialog.ACCESS_FILESYSTEM;dialog.file_mode=FileDialog.FILE_MODE_SAVE_FILE;dialog.filters=PackedStringArray(["*.json ; JSON"]);dialog.current_file="MET_Beta_"+kind+".json";add_child(dialog)
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
	desk.theme_ready_before=-1;desk.theme_seen_rewards=[];beta_frame_seconds=0;reset_beta_clock()
	game.resume_time(Time.get_unix_time_from_system());selection=[];selected_project="";inspector_key="";ignore_save=false;save_game();overlay.hide();refresh();map_view.focus_community();message("当前版本的城市备份已载入。")
func import_file():
	var dialog=FileDialog.new();dialog.use_native_dialog=true;dialog.access=FileDialog.ACCESS_FILESYSTEM;dialog.file_mode=FileDialog.FILE_MODE_OPEN_FILE;dialog.filters=PackedStringArray(["*.json ; MET Beta 当前版本存档备份"]);add_child(dialog)
	dialog.file_selected.connect(func(path):load_saved(path);dialog.queue_free());dialog.canceled.connect(func():dialog.queue_free());dialog.popup_centered_ratio(.7)
func restart(seed_value:int):
	beta_boundary_seen="";beta_frame_seconds=0.0;desk.theme_ready_before=-1;desk.theme_seen_rewards=[]
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
	var win=get_window();var logical=Vector2(win.size)
	var design_size=Vector2i(maxi(1920,int(logical.x)),maxi(1080,int(logical.y)))
	if win.content_scale_size!=design_size:win.content_scale_size=design_size
	layout_ui()
func _rect(node:Control,rect:Rect2):
	node.position=rect.position;node.size=rect.size
func time_hud_width() -> float:return 638.
func layout_ui():
	if top_panel==null or desk==null:return
	var area=Rect2(Vector2.ZERO,get_viewport_rect().size);safe_area=area;compact=false;W.touch=false
	map_view.compact=false;map_view.reduce_motion=bool(display_settings.reduced_motion);map_view.show_all_links=bool(display_settings.all_links);map_view.ui_font_scale=W.text_scale
	sync_header_values()
	var archived=desk.is_open() or decision_kind!="" or overlay!=null and overlay.visible
	var topstyle=W.box(W.PANEL,W.DIVIDER,18);topstyle.content_margin_top=12;topstyle.content_margin_bottom=12;topstyle.content_margin_left=16;topstyle.content_margin_right=16;top_panel.add_theme_stylebox_override("panel",topstyle)
	_rect(top_panel,Rect2(area.position+Vector2(24,24),Vector2(314,112)))
	for key in ["brand","finance","policy","ledger","menu"]:header_nodes[key].hide()
	for key in ["people","food"]:
		var button=header_nodes[key];button.show();button.custom_minimum_size=Vector2(126,72);W.flat(button)
	governance_label.add_theme_font_size_override("font_size",28);header_nodes.food_text.add_theme_font_size_override("font_size",28)
	header_nodes.people.icon=null
	header_nodes.food.icon=null
	dt_label.add_theme_font_size_override("font_size",28)
	var bs=W.box(W.PANEL,W.DIVIDER,18);bs.content_margin_left=14;bs.content_margin_right=14;bs.content_margin_top=8;bs.content_margin_bottom=8;bottom_panel.add_theme_stylebox_override("panel",bs)
	_rect(bottom_panel,Rect2(Vector2((area.size.x-430)/2,area.end.y-100),Vector2(430,76)))
	for button in category_buttons:
		button.visible=true;button.text="";button.custom_minimum_size=Vector2(64,56);button.add_theme_constant_override("icon_max_width",46)
	bottom_nodes.place.hide();bottom_nodes.build.hide();bottom_nodes.stats.hide()
	bottom_nodes.layers.show();bottom_nodes.layers.text="";bottom_nodes.layers.icon=Art.icon("layers",W.TEXT);bottom_nodes.layers.tooltip_text="分析图层 · 医疗、人口、资源与环境";bottom_nodes.layers.custom_minimum_size=Vector2(64,56)
	bottom_nodes.growth.show();bottom_nodes.growth.text="档案";bottom_nodes.growth.icon=Art.icon("ledger",W.TEXT);bottom_nodes.growth.custom_minimum_size=Vector2(126,56);bottom_nodes.growth.name="OpenCityArchive"
	tool_panel.visible=not screenshot_mode and not archived
	_rect(tool_panel,Rect2(Vector2(24,area.end.y-80),Vector2(332,48)))
	layer_button.hide();lock_button.hide();header_nodes.policy.hide();header_nodes.ledger.hide()
	header_nodes.menu.show();if header_nodes.menu.get_parent()!=header_nodes.toolrow:header_nodes.menu.reparent(header_nodes.toolrow)
	header_nodes.menu.custom_minimum_size=Vector2(48,40)
	legend_panel.visible=map_view.layer!="normal" and not screenshot_mode and not archived
	_rect(legend_panel,Rect2(Vector2(24,area.end.y-284),Vector2(286,182)))
	var side=352.;var panel_height=minf(590,area.size.y-310)
	_rect(inspector_panel,Rect2(Vector2(area.end.x-side-24,area.position.y+150),Vector2(side,panel_height)));inspector.custom_minimum_size.x=0
	if decision_kind!="":
		var work_height=minf((480. if W.text_scale<=1. else 580.) if workbench.page=="summary" else 800.,area.size.y-180)
		var dimensions=Vector2(minf(1300,area.size.x-160),work_height);_rect(workbench,Rect2(Vector2((area.size.x-dimensions.x)*.5,140+(area.size.y-140-dimensions.y)*.5),dimensions))
	workbench_shield.offset_top=140;workbench_shield.visible=decision_kind!="";workbench.visible=decision_kind!=""
	map_view.available_rect=Rect2(Vector2(380,150),Vector2(maxf(640,area.size.x-780),area.size.y-300))
	notice_panel.hide();resource_row.hide();clock_label.hide()
	_rect(toast,Rect2(Vector2((area.size.x-620)/2,area.end.y-148),Vector2(620,36)))
	toast.visible=not screenshot_mode and not archived and ((mode=="site" and selection.is_empty()) or focused_tool!="");toast.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER
	if build_palette.visible:_rect(build_palette,Rect2(Vector2((area.size.x-780)/2,area.end.y-420),Vector2(780,290)))
	if layer_picker.visible:_rect(layer_picker,Rect2(Vector2((area.size.x-320)/2,area.end.y-700),Vector2(320,560)))
	var mw=minf(1280,area.size.x-96);var mh=minf(860,area.size.y-140);modal_panel.custom_minimum_size=Vector2(mw,mh);modal_panel.size=Vector2(mw,mh);modal_scroll.custom_minimum_size=Vector2(0,maxf(60,mh-150));modal_body.custom_minimum_size.x=0
	top_panel.visible=not screenshot_mode;bottom_panel.visible=not screenshot_mode and not archived
	if archived or screenshot_mode:inspector_panel.hide();legend_panel.hide();build_palette.hide();layer_picker.hide()
	refresh_build_dock();desk.relayout(area)
	if command_notice!=null:
		var left_edge=top_panel.get_global_rect().end.x+24;var right_edge=desk.time_panel.position.x-24;var notice_width=minf(760,maxf(200,right_edge-left_edge))
		_rect(command_notice,Rect2(Vector2((left_edge+right_edge-notice_width)*.5,32),Vector2(notice_width,96)))
	if site_bubble!=null:site_bubble.reposition()
	refresh_legend()
func refresh_context():
	if mode=="site":
		inspector_panel.hide();inspector031.hide();site_bubble.refresh()
	else:
		site_bubble.hide();Panels.inspector(self)
func context_obstacles() -> Array:
	var result=[]
	if desk==null:return result
	for node in [top_panel,desk.time_panel,desk.task_panel,desk.alert_panel,legend_panel,bottom_panel]:
		if node!=null and node.visible:result.append(node.get_global_rect())
	return result
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
	var captions={"terrain":"图标辨地貌 · 边线定地格","medical":"绿色可达 · 色条为实际服务","education":"绿色可达 · 色条为实际服务","commercial":"绿色可达 · 色条为实际服务","entertainment":"绿色可达 · 色条为实际服务","pollution":"青色净化 ━ 赭色负担","heat":"0 ━ 4+ 余热场强","population":"入住 / 容量","employment":"到岗 / 岗位","unemployed":"待就业居民比例","food":"产出 − 消耗 · 线为真实调拨","material":"产出 − 消耗 · 线为真实调拨","goods":"产出 − 消耗 · 线为真实调拨","energy":"实际供电 / 需求","operator":"外圈颜色代表运营组织","change":"实线获益 · 虚线受损（预计）"}
	legend_body.text=str(captions.get(kind,""))
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
	flush_beta_time()
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
	decision_kind="";workbench.hide();workbench_shield.hide();reset_beta_clock();map_view.plan_result={};map_view.plan_tile="";inspector_key="";save_game();refresh();play_cue("back")
func play_cue(id:String):
	if chime==null or not game.s.settings.get("sound",true):return
	var path="res://assets/audio030/"+id+".wav"
	if not FileAccess.file_exists(path):
		if ResourceLoader.exists(path):chime.stream=load(path);chime.play()
		return
	var data=FileAccess.get_file_as_bytes(path)
	var audio=AudioStreamWAV.new();audio.format=AudioStreamWAV.FORMAT_16_BITS;audio.mix_rate=22050;audio.data=data.slice(44);chime.stream=audio;chime.play()
func display_options():
	open_modal("显示与交互","桌面全屏显示；文字与地图分别缩放。")
	var row=option_row(modal_body,8)
	for factor in [1.0,1.25,1.5]:row.add_child(W.button("字号 %d%%"%int(factor*100),func():display_settings.text_scale=factor;apply_display();display_options()))
	for pair in [["reduced_motion","减少动态"],["high_contrast","提高对比"]]:
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
			message(game.project_name(id)+"已建成。首次正式运营结果将在后续周期显示。",false)
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
		message(game.project_name(id)+"首次运营 · "+(" / ".join(outputs.slice(0,2)) if not outputs.is_empty() else "实际服务 %.1f人次"%served),false);first_operations.erase(id);map_view.flash_project_id=id;map_view.flash_time=2.
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
	game.Clock.set_mode(game,"normal");display_pending_tick=-1;overlay.hide()
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
	open_modal("已结算的城市记录","只读留念；查看保存时的城市成果与实际历程。")
	var records:Array=game.Block.list_archives()
	var heading=DeskVisuals.card(modal_body,"历届城市","已保存 %d 座城市 · 选择一座查看成果"%records.size(),"world")
	heading.set_meta("city_records_overview",true)
	if records.is_empty():
		DeskVisuals.card(modal_body,"还没有已结算记录","DT 耗尽后，可在城市尾声保存这座城市。","clock",W.MUTED)
		return
	var grid=DeskVisuals.grid(modal_body,2)
	for item in records:
		var row:Dictionary=item;var id=str(row.get("id",""));var city_name=str(row.get("name","城市"))
		var theme_id=str(row.get("theme",{}).get("id",""));var theme_def:Dictionary=game.Themes._definition(theme_id)
		var theme_name=str(theme_def.get("name",theme_id if theme_id!="" else "未选择"))
		var card=DeskVisuals.card(grid,city_name,Beta.date(float(row.get("local_elapsed_seconds",0))),"home")
		card.set_meta("city_record_id",id)
		var stats=DeskVisuals.grid(card,2)
		DeskVisuals.metric(stats,"population","人口","%.1f"%float(row.population) if row.has("population") else "未保存","保存时的居民人数",W.GREEN)
		DeskVisuals.metric(stats,"time","DT 开销","%.2f"%float(row.dt_spent) if row.has("dt_spent") else "未保存","本局累计发展预算",W.GOLD)
		DeskVisuals.badge(card,"主题 · "+theme_name,W.ACCENT)
		var view=W.button(city_name+" · 查看记录 ›",func():open_city_record(row));view.name="CityRecordView_"+id;view.set_meta("city_record_view",id);view.size_flags_horizontal=Control.SIZE_EXPAND_FILL;card.add_child(view)

func open_city_record(row:Dictionary):
	var city_name=str(row.get("name","城市记录"));var record_id=str(row.get("id",""))
	open_modal(city_name,"城市记录 / "+Beta.date(float(row.get("local_elapsed_seconds",0)))+" · 只读本局成果")
	var back=W.flat(W.button("‹ 返回历届城市",open_city_records));back.name="CityRecordsBack";modal_body.add_child(back)
	var metrics=DeskVisuals.grid(modal_body,3);metrics.set_meta("city_record_metrics",record_id)
	DeskVisuals.metric(metrics,"population","人口","%.1f"%float(row.population) if row.has("population") else "未保存","保存时的居民人数",W.GREEN)
	DeskVisuals.metric(metrics,"currency","通货","%.1f"%float(row.currency) if row.has("currency") else "未保存","保存时的财政余额",W.GOLD)
	DeskVisuals.metric(metrics,"time","DT 开销","%.2f"%float(row.dt_spent) if row.has("dt_spent") else "未保存","本局累计发展预算",W.ACCENT)
	var theme:Dictionary=row.get("theme",{});var theme_id=str(theme.get("id",""));var theme_def:Dictionary=game.Themes._definition(theme_id)
	var theme_name=str(theme_def.get("name",theme_id if theme_id!="" else "未选择"))
	var identity=DeskVisuals.grid(modal_body,2)
	var theme_card=DeskVisuals.card(identity,"主题 · "+theme_name,str(theme_def.get("description","记录中未选择发展主题。" if theme_id=="" else "主题定义暂不可用；保留保存的标识。")),Beta._theme_icon(theme_id))
	theme_card.set_meta("city_record_theme",theme_id)
	if theme_id!="":
		var stage_labels:Array=[]
		for stage in theme_def.get("stages",[]):stage_labels.append(str(stage.get("name",stage.get("id","阶段"))))
		if not stage_labels.is_empty():DeskVisuals.steps(theme_card,stage_labels,stage_labels.size() if bool(theme.get("completed",false)) else clampi(int(theme.get("stage",1))-1,0,stage_labels.size()-1))
		DeskVisuals.badge(theme_card,"主题已完成" if bool(theme.get("completed",false)) else "保存时处于第 %d 阶段"%int(theme.get("stage",1)),W.GREEN if bool(theme.get("completed",false)) else W.MUTED)
	var capstone:Dictionary=row.get("capstone",{})
	var landmark=DeskVisuals.card(identity,"城市定型","记录中保存的正式挂牌成果。","home",W.GOLD)
	DeskVisuals.badge(landmark,str(capstone.get("name","无定型建筑")),W.GOLD if not capstone.is_empty() else W.MUTED)
	var copies:Dictionary={};var blueprints:Dictionary=row.get("blueprint_levels",{})
	for instance in row.get("bonus_instances",{}).values():
		var bid=str(instance.get("bonus_id",""));copies[bid]=int(copies.get(bid,0))+1
	var ids:Array=copies.keys()
	for bid in blueprints:
		if bid not in ids:ids.append(bid)
	ids.sort()
	var bonuses=DeskVisuals.card(modal_body,"方案与蓝图","以此城保存的装配副本与蓝图等级为准。","bonus")
	if ids.is_empty():bonuses.add_child(W.label("此记录没有已装配方案或蓝图研究。",14,W.MUTED))
	else:
		var grid=DeskVisuals.grid(bonuses,3)
		for value in ids:
			var bid=str(value);var definition:Dictionary=game.c.bonuses.get(bid,{})
			var card=DeskVisuals.card(grid,str(definition.get("name",bid)),"已装配方案" if int(copies.get(bid,0))>0 else "研究蓝图","bonus")
			card.set_meta("city_record_bonus",bid)
			var amount=W.hbox(card,10);DeskVisuals.icon(amount,"bonus",26,W.GREEN)
			amount.add_child(W.label("装配%d份"%int(copies.get(bid,0)),24,W.GREEN,false))
			var research=W.hbox(card,10);DeskVisuals.icon(research,"research",24,W.ACCENT)
			research.add_child(W.label("蓝图等级%d"%int(blueprints.get(bid,0)),16,W.ACCENT,false))
	var events:Array=row.get("history",[]).slice(-20)
	var history=DeskVisuals.fold(modal_body,"最近城市事件","实际保存的最后 %d 条事件 · 展开查看原始历程"%events.size())
	var toggle=history.get_parent().get_child(0);toggle.name="CityRecordHistory";toggle.set_meta("city_record_history",true)
	if events.is_empty():history.add_child(W.label("此记录没有城市事件。",14,W.MUTED))
	for index in events.size():
		var event:Dictionary=events[index];var line=W.hbox(history,12);line.size_flags_horizontal=Control.SIZE_EXPAND_FILL
		var rail=W.vbox(line,6);rail.custom_minimum_size.x=40
		DeskVisuals.icon(rail,"clock",22,W.MUTED)
		if index<events.size()-1:
			var connector=ColorRect.new();connector.color=W.DIVIDER;connector.custom_minimum_size=Vector2(2,26);connector.size_flags_horizontal=Control.SIZE_SHRINK_CENTER;connector.mouse_filter=Control.MOUSE_FILTER_IGNORE;rail.add_child(connector)
		var entry=DeskVisuals.card(line,"",("%.2f 来生月"%float(event.h)) if event.has("h") else "保存事件")
		entry.set_meta("city_record_event",index)
		entry.add_child(W.label(str(event.get("message",event.get("text",event.get("kind","城市记录")))),15,W.TEXT))

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

func avoid_time_hud(panel:Control):
	var bounds=panel.get_global_rect();var clock=desk.time_panel.get_global_rect()
	if not bounds.intersects(clock):return
	if clock.position.y<=bounds.position.y:
		var shift=clock.end.y+8.-bounds.position.y
		panel.position.y+=shift;panel.size.y=maxf(160.,bounds.size.y-shift)
	else:panel.size.y=maxf(160.,clock.position.y-bounds.position.y-8.)

static func beta_should_submit(buffered_seconds:float) -> bool:
	return buffered_seconds>=0.5

func archive_page(view:String,context:String=""):
	close_modal();desk.open(view,context)
func open_archive():
	open_modal("城市档案","从城市状态进入管理；查看期间时间暂停。")
	var metrics:Dictionary=shot.get("metrics",{})
	var block:Dictionary=game.s.get("block",{})
	var theme_view:Dictionary=game.Themes.view(game)
	var rewards=0
	for reward in block.get("reward_choices",[]):
		if not bool(reward.get("claimed",false)):rewards+=1
	var active_orders=0
	for order in block.get("trade_orders",[]):
		if str(order.get("status","")) in ["open","partial"]:active_orders+=1
	var copies=0
	for wallet in block.get("bonus_wallet",{}).values():copies+=int(wallet.get("charges",0))
	var queue:Array=game.Construction.queue(game)
	var population=float(metrics.get("population",0))
	var theme_name=str(theme_view.get("name","")) if str(theme_view.get("id",""))!="" else "选择发展方向"
	var theme_note="%d 份奖励待选"%rewards if rewards>0 else ("第 %d 阶段 · %s"%[int(theme_view.get("stage",1)),str(theme_view.get("stage_name",""))] if str(theme_view.get("id",""))!="" else "五个主题，塑造这一局的城市")
	if bool(theme_view.get("completed",false)) and rewards==0:theme_note="主题已完成 · 查看事件与历程"
	var entries=[
		{"title":"主题与奖励","icon":"theme","value":theme_name,"note":theme_note,"view":"tasks","context":"","color":W.GOLD,"source":"block.theme / reward_choices"},
		{"title":"财政","icon":"currency","value":"%.0f 通货"%float(game.s.currency),"note":"经营净流 %+.2f / 来生月"%float(metrics.get("cashflow",0)),"view":"finance","context":"","color":W.GOLD,"source":"currency / metrics.cashflow"},
		{"title":"人口","icon":"population","value":"%.0f 位居民"%population,"note":"%.0f 个岗位 · %.0f 住房容量"%[float(metrics.get("jobs",0)),float(metrics.get("housing",0))],"view":"population","context":"","color":W.GREEN,"source":"metrics.population / jobs / housing"},
		{"title":"物资","icon":"material","value":"%.1f 粮食"%game.inventory_total("food"),"note":"材料 %.1f · 制成品 %.1f"%[game.inventory_total("material"),game.inventory_total("goods")],"view":"resource","context":"food","color":W.ACCENT,"source":"project inventories"},
		{"title":"建设","icon":"construction","value":"%d 项施工中"%queue.size(),"note":"共享施工能力 %.1f CP / 来生月"%game.Construction.capacity(game),"view":"city040","context":"development","color":W.GOLD,"source":"Construction.queue / capacity"},
		{"title":"市场","icon":"logistics","value":"%d 笔开放订单"%active_orders,"note":"限价交易 · 运力与成交记录","view":"city040","context":"market","color":W.INFO,"source":"block.trade_orders open / partial"},
		{"title":"政策","icon":"policy","value":"%d 项制度生效"%block.get("policies",{}).size(),"note":"查看取舍与预计影响","view":"policies","context":"","color":W.INFO,"source":"block.policies"},
		{"title":"方案","icon":"bonus","value":"%d 份可用副本"%copies,"note":"部署、蓝图与已安装方案","view":"city040","context":"permits","color":W.ACCENT,"source":"block.bonus_wallet.charges"},
		{"title":"城市","icon":"world","value":str(block.get("display_name","本局城市")),"note":"已进入尾声" if bool(block.get("ending",{}).get("triggered",false)) else "命名、地标与本局历程","view":"city040","context":"block","color":W.GREEN,"source":"block.display_name / ending"}
	]
	var grid=DeskVisuals.grid(modal_body,3);grid.name="ArchiveNavigationGrid"
	for entry in entries:_archive_navigation_card(grid,entry)
	var bottom=W.hbox(modal_body,12)
	bottom.add_child(W.icon_button("ledger","城市消息",open_city_messages,"已发生的城市消息与来源"))
	bottom.add_child(W.icon_button("world","历届城市",open_city_records,"只读查看结算后的城市记录"))
	bottom.add_child(W.icon_button("clock","时间规则",func():archive_page("time"),"来生历、发展预算与推进规则"))
func _archive_navigation_card(parent:Node,entry:Dictionary) -> void:
	var card=DeskVisuals.card(parent);card.custom_minimum_size=Vector2(320,148)
	card.set_meta("graphic_role","archive_navigation");card.set_meta("destination",str(entry.view)+":"+str(entry.context));card.set_meta("status_value",str(entry.value));card.set_meta("source",str(entry.source))
	var top=W.hbox(card,12);DeskVisuals.icon(top,str(entry.icon),36,entry.color)
	var button=W.flat(W.button(str(entry.title)+"  ›",func():archive_page(str(entry.view),str(entry.context))));button.name="Archive_"+str(entry.view)+"_"+str(entry.context)
	button.alignment=HORIZONTAL_ALIGNMENT_LEFT;button.custom_minimum_size.y=36;button.size_flags_horizontal=Control.SIZE_EXPAND_FILL;button.add_theme_font_size_override("font_size",18);top.add_child(button)
	var value=W.label(str(entry.value),24,entry.color);card.add_child(value)
	card.add_child(W.label(str(entry.note),14,W.MUTED))
func open_alert_archive():
	open_modal("待处理问题","只列正在发生的严重缺口；点击定位真实来源。")
	for row in desk.severe_alerts:
		var button=W.button(str(row.text)+"  ›",func():close_modal();on_beta_source(row));button.add_theme_color_override("font_color",W.DANGER);modal_body.add_child(button)

func wire_tool_focus(node:Node):
	if node is Button and node.tooltip_text!="":
		var button=node
		button.focus_entered.connect(func():focused_tool=str(button.tooltip_text).split("\n")[0];toast.text=focused_tool;request_layout())
		button.focus_exited.connect(func():focused_tool="";request_layout())
	for child in node.get_children():wire_tool_focus(child)
