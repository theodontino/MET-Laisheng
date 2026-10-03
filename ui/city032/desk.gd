extends Control
## 0.3.2 City Desk: concrete information entry points on the existing map.
const Beta=preload("res://ui/beta/interface.gd")
const W=preload("res://ui/widgets.gd")
const C=preload("res://ui/components031/components.gd")
const D=preload("res://ui/components031/design.gd")
const Art=preload("res://ui/map/art.gd")
const M=preload("res://ui/city032/models.gd")
const Chart=preload("res://ui/city032/history_chart.gd")
const DragScroll=preload("res://ui/city032/drag_scroll.gd")
const Civic=preload("res://core/civic032.gd")
const UI040=preload("res://ui/city040/city.gd")
const Topics040=preload("res://ui/city040/topics.gd")
const SLOTS=[["Tax","税制","决定哪些真实活动形成财政收入"],["Labor","就业分配","决定有限岗位如何匹配居民"],["Environment","环境治理","决定排放的传播与责任"],["Procurement","物资采购","决定短缺时资源如何分配"],["Land","用地制度","决定土地与财政的关系"]]
var app
var kind=""
var arg=""
var subtopic=""
var detail_id=""
var nav_stack:Array=[]
var task_tab="active"
var task_filter="all"
var finance_mode="rates"
var rail_expanded=true
var previous_layer="normal"
var previous_selected=""
var previous_manual_layer=false
var pending_policy=""
var policy_version=""
var policy_prediction={}
var updating_policy=false
var task_signature=""
var page_signature=""
var time_panel:PanelContainer
var time_clock:Label
var budget_bar:ProgressBar
var mode_buttons={}
var time_risk:Label
var main_alert:Button
var next_condition_label:Label
var pause_button:Button
var dt_button:Button
var clock_row:HBoxContainer
var time_actions:HBoxContainer
var resource_panel:PanelContainer
var resource_grid:HBoxContainer
var resource_buttons={}
var compact_resource:Button
var task_panel:PanelContainer
var task_root:VBoxContainer
var task_toggle:Button
var task_summary:Button
var task_list:VBoxContainer
var task_scroll:ScrollContainer
var active_task_count=0
var detail_clock:Label
var overview_button:Button
var task_count:Label
var shield:ColorRect
var page:PanelContainer
var page_root:VBoxContainer
var header:HBoxContainer
var title:Label
var subtitle:Label
var cost:Label
var close_button:Button
var scroll:ScrollContainer
var body:VBoxContainer
var tabs:HBoxContainer
var footer:VBoxContainer
var footer_text:Label
var footer_buttons:HBoxContainer
var primary:Button
var secondary:Button
var policy_buttons={}
var kind_is_fullscreen=false
var last_wall_second=""
var trade_resource="food"
var trade_side="buy"
var trade_amount:float=1.
var trade_price:float=0.
var trade_price_set:bool=false
var carry_selection:Array=[]
func setup(owner):
	app=owner;set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);mouse_filter=Control.MOUSE_FILTER_IGNORE
	_build_time();_build_resources();_build_tasks();_build_page()
func _build_time():
	time_panel=W.panel(self,W.PANEL);time_panel.name="BetaTimePanel"
	var style=W.box(W.PANEL,W.SOFT,10);style.content_margin_top=5;style.content_margin_bottom=5;style.content_margin_left=8;style.content_margin_right=8;time_panel.add_theme_stylebox_override("panel",style)
	var v=W.vbox(time_panel,4);clock_row=W.hbox(v,5)
	dt_button=W.button("",func():open("time"),false,"高速发展预算、来生历与时间规则");dt_button.custom_minimum_size=Vector2(100,40);clock_row.add_child(dt_button)
	app.dt_label=W.label("",16,W.GREEN,false);dt_button.add_child(app.dt_label);app.dt_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);app.dt_label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;app.dt_label.vertical_alignment=VERTICAL_ALIGNMENT_CENTER
	time_clock=W.label("",14,W.MUTED,false);time_clock.size_flags_horizontal=Control.SIZE_EXPAND_FILL;clock_row.add_child(time_clock)
	pause_button=W.button("暂停",toggle_pause);pause_button.name="BetaPause";pause_button.custom_minimum_size.x=58;clock_row.add_child(pause_button)
	app.checkpoint_button=W.primary(W.button("停止推进",func():Beta.set_mode(app,"normal")));app.checkpoint_button.name="BetaStop";app.checkpoint_button.custom_minimum_size=Vector2(84,40);clock_row.add_child(app.checkpoint_button)
	budget_bar=W.bar(1);budget_bar.custom_minimum_size.y=4;v.add_child(budget_bar)
	time_actions=W.hbox(v,4);app.pulse_buttons=[]
	for mode in Beta.mode_rows(app.game):
		var id=str(mode[0]);var button=W.button(str(mode[1]),func():Beta.set_mode(app,id));button.name="BetaMode_"+id;button.custom_minimum_size=Vector2(0,34);button.size_flags_horizontal=Control.SIZE_EXPAND_FILL;time_actions.add_child(button);mode_buttons[id]=button
	time_risk=W.label("",13,W.MUTED);v.add_child(time_risk)
func _build_resources():
	resource_panel=W.panel(self);resource_panel.name="ResourceDock032"
	var st=W.box(W.PANEL,W.OUTLINE,7);st.content_margin_left=5;st.content_margin_right=5;st.content_margin_top=4;st.content_margin_bottom=4;resource_panel.add_theme_stylebox_override("panel",st)
	resource_grid=W.hbox(resource_panel,0)
	var resource_ids=["food","material","goods","energy"]
	for index in resource_ids.size():
		var id=resource_ids[index]
		var b=W.button("",func():open("resource",id));b.name="Resource_"+id;b.custom_minimum_size=Vector2(0,48);b.size_flags_horizontal=Control.SIZE_EXPAND_FILL;resource_grid.add_child(b)
		W.ghost(b)
		var margin=MarginContainer.new();margin.mouse_filter=Control.MOUSE_FILTER_IGNORE;margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		for k in ["margin_left","margin_right"]:margin.add_theme_constant_override(k,7)
		for k in ["margin_top","margin_bottom"]:margin.add_theme_constant_override(k,3)
		b.add_child(margin);var v=W.vbox(margin,0);v.mouse_filter=Control.MOUSE_FILTER_IGNORE
		var quantity=D.quantity(id);var heading=W.hbox(v,5);heading.mouse_filter=Control.MOUSE_FILTER_IGNORE
		var icon=TextureRect.new();icon.texture=Art.icon(str(quantity.icon),Color(quantity.ink));icon.custom_minimum_size=Vector2(20,20);icon.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;icon.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED;icon.mouse_filter=Control.MOUSE_FILTER_IGNORE;heading.add_child(icon)
		var head=W.label("",17,Color(quantity.ink),false);head.add_theme_font_override("font",W._semibold());head.vertical_alignment=VERTICAL_ALIGNMENT_CENTER;heading.add_child(head)
		var tail=W.label("",13,W.MUTED,false);v.add_child(tail);resource_buttons[id]={"button":b,"head":head,"tail":tail,"icon":icon}
		if index<resource_ids.size()-1:
			var divider=ColorRect.new();divider.color=W.DIVIDER;divider.custom_minimum_size=Vector2(1,32);divider.size_flags_vertical=Control.SIZE_SHRINK_CENTER;divider.mouse_filter=Control.MOUSE_FILTER_IGNORE;resource_grid.add_child(divider)
	compact_resource=W.icon_button("ledger","物资",func():open("resource","food"),"粮食、材料、制成品和电力详情");compact_resource.name="CompactResources032";resource_panel.add_child(compact_resource);compact_resource.hide()
func _build_tasks():
	task_panel=W.panel(self);task_panel.name="TaskRail032";task_panel.clip_contents=true
	var panel_style=W.box(W.PANEL,W.SOFT,8);panel_style.content_margin_left=7;panel_style.content_margin_right=7;panel_style.content_margin_top=3;panel_style.content_margin_bottom=3;task_panel.add_theme_stylebox_override("panel",panel_style)
	task_root=W.vbox(task_panel,2)
	var h=W.hbox(task_root,4)
	task_summary=W.button("当前目标",func():open("tasks"));task_summary.name="TaskSummary032";task_summary.size_flags_horizontal=Control.SIZE_EXPAND_FILL;task_summary.custom_minimum_size.y=32;task_summary.alignment=HORIZONTAL_ALIGNMENT_LEFT;task_summary.set_meta("font_base",14);task_summary.add_theme_font_size_override("font_size",int(14*W.text_scale));task_summary.tooltip_text="查看当前委托、地区目标与历史"
	var summary_style=W.button_box(W.SOFT,W.SOFT,6);summary_style.content_margin_left=5;summary_style.content_margin_right=5;summary_style.content_margin_top=2;summary_style.content_margin_bottom=2;task_summary.add_theme_stylebox_override("normal",summary_style)
	var summary_hover=W.button_box(W.PANEL,W.GREEN,6);summary_hover.content_margin_left=5;summary_hover.content_margin_right=5;summary_hover.content_margin_top=2;summary_hover.content_margin_bottom=2;task_summary.add_theme_stylebox_override("hover",summary_hover);h.add_child(task_summary)
	task_count=W.label("待领 0",13,W.MUTED,false);task_count.horizontal_alignment=HORIZONTAL_ALIGNMENT_RIGHT;task_count.vertical_alignment=VERTICAL_ALIGNMENT_CENTER;task_count.custom_minimum_size.x=56;h.add_child(task_count)
	task_toggle=W.button("",toggle_rail,false,"展开委托历史");task_toggle.name="TaskRailToggle032";task_toggle.icon=Art.icon("ledger",W.TEXT);task_toggle.expand_icon=true;task_toggle.add_theme_constant_override("icon_max_width",20);task_toggle.custom_minimum_size=Vector2(36,32);h.add_child(task_toggle)
	next_condition_label=W.label("",13,W.MUTED);task_root.add_child(next_condition_label)
	main_alert=W.button("",func():app.on_beta_source(app.feedback_layer.summary().get("main",{})));main_alert.autowrap_mode=TextServer.AUTOWRAP_WORD_SMART;main_alert.custom_minimum_size.y=38;task_root.add_child(main_alert);main_alert.hide()
	task_scroll=ScrollContainer.new();task_scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;task_scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;task_root.add_child(task_scroll)
	task_list=W.vbox(task_scroll,4);task_list.size_flags_horizontal=Control.SIZE_EXPAND_FILL
func _build_page():
	shield=ColorRect.new();shield.color=Color(0,0,0,0);shield.mouse_filter=Control.MOUSE_FILTER_STOP;shield.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(shield);shield.hide()
	page=W.panel(self);page.name="CityPage032";page.clip_contents=true
	var p=W.box(W.PANEL,W.SOFT,12);p.content_margin_left=16;p.content_margin_right=16;p.content_margin_top=12;p.content_margin_bottom=12;page.add_theme_stylebox_override("panel",p)
	page_root=W.vbox(page,8);header=W.hbox(page_root,8)
	title=W.label("",24);header.add_child(title)
	cost=W.label("",16,W.GOLD,false);cost.size_flags_horizontal=Control.SIZE_SHRINK_END;header.add_child(cost)
	close_button=W.button("返回地图",back);close_button.name="CityPageClose040";header.add_child(close_button)
	subtitle=W.label("",14,W.MUTED);page_root.add_child(subtitle)
	tabs=W.hbox(page_root,8)
	scroll=DragScroll.new();scroll.name="CityPageScroll032";scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;page_root.add_child(scroll)
	body=W.vbox(scroll,14);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	footer=W.vbox(page_root,6);footer_text=W.label("",16,W.GREEN);footer.add_child(footer_text);footer_buttons=W.hbox(footer,8)
	secondary=W.button("返回",close);secondary.name="CitySecondary032";footer_buttons.add_child(secondary)
	primary=W.primary(W.button("",func():pass));primary.name="CityPrimary032";primary.size_flags_horizontal=Control.SIZE_EXPAND_FILL;footer_buttons.add_child(primary)
	page.hide()
func is_open() -> bool:return kind!=""
func is_fullscreen() -> bool:return is_open() and kind_is_fullscreen
func blocks_time() -> bool:return is_open()
func open(view:String,context:String=""):
	app.flush_beta_time()
	if app.game.s.phase not in ["play","failed"]:app.message("先选择地区并安顿居民。");return
	if not is_open():
		previous_layer=app.map_view.layer;previous_selected=app.selected_project;previous_manual_layer=app.manual_layer
	if is_open() and kind in ["resource","population"] and view not in ["resource","population"]:
		app.map_view.layer=previous_layer;app.manual_layer=previous_manual_layer;app.map_view.invalidate();app.refresh_legend()
	kind=view;arg=context;subtopic="";detail_id="";nav_stack=[];page_signature="";kind_is_fullscreen=view in ["policies","finance","tasks","city040"]
	app.overlay.hide();app.build_palette.hide();app.layer_picker.hide();app.issue_index=-1
	if view=="policies":
		pending_policy="";policy_prediction={};policy_buttons={};policy_version=""
	if view=="population":app.map_view.layer="population";app.manual_layer=true;app.map_view.invalidate()
	if view=="resource":
		if arg not in ["food","material","goods","energy"]:arg="food"
		app.map_view.layer=arg;app.manual_layer=true;app.map_view.invalidate()
	page.show();render(false);app.layout_ui();update_hud()
func _capture_page() -> Dictionary:
	return {"kind":kind,"arg":arg,"subtopic":subtopic,"detail_id":detail_id,"task_tab":task_tab,"finance_mode":finance_mode,"pending_policy":pending_policy,"policy_version":policy_version,"policy_prediction":policy_prediction.duplicate(true),"scroll":scroll.scroll_vertical}
func navigate(view:String,context:String="",section:String="",detail:String="") -> void:
	if is_open():nav_stack.append(_capture_page())
	var old=kind;kind=view;arg=context;subtopic=section;detail_id=detail;page_signature="";kind_is_fullscreen=view in ["policies","finance","tasks","city040"]
	if old in ["resource","population"] and view not in ["resource","population"]:
		app.map_view.layer=previous_layer;app.manual_layer=previous_manual_layer;app.map_view.invalidate();app.refresh_legend()
	page.show();render(false);app.layout_ui()
func push_section(section:String,detail:String="") -> void:
	navigate(kind,arg,section,detail)
func replace(view:String,context:String="",section:String="",detail:String="") -> void:
	var saved=nav_stack
	if is_open() and kind!=view and kind in ["resource","population"] and view not in ["resource","population"]:
		app.map_view.layer=previous_layer;app.manual_layer=previous_manual_layer;app.map_view.invalidate();app.refresh_legend()
	kind=view;arg=context;subtopic=section;detail_id=detail;page_signature="";kind_is_fullscreen=view in ["policies","finance","tasks","city040"];nav_stack=saved
	page.show();render(false);app.layout_ui()
func back() -> void:
	if nav_stack.is_empty():close();return
	var target:Dictionary=nav_stack.pop_back();kind=str(target.kind);arg=str(target.arg);subtopic=str(target.get("subtopic",""));detail_id=str(target.get("detail_id",""));task_tab=str(target.get("task_tab","active"));finance_mode=str(target.get("finance_mode","rates"));pending_policy=str(target.get("pending_policy",""));policy_version=str(target.get("policy_version",""));policy_prediction=target.get("policy_prediction",{}).duplicate(true);kind_is_fullscreen=kind in ["policies","finance","tasks","city040"]
	if kind=="resource":app.map_view.layer=arg;app.manual_layer=true;app.map_view.invalidate()
	elif kind=="population":app.map_view.layer="population";app.manual_layer=true;app.map_view.invalidate()
	else:app.map_view.layer=previous_layer;app.manual_layer=previous_manual_layer;app.map_view.invalidate()
	page.show();render(false);call_deferred("restore_scroll",int(target.get("scroll",0)),page_signature);app.layout_ui()
func restore_scroll(value:int,expected:String) -> void:
	# A newly rebuilt ScrollContainer has no content height until layout settles.
	await get_tree().process_frame
	await get_tree().process_frame
	if is_open() and page_signature==expected:scroll.scroll_vertical=value
func close():
	app.reset_beta_clock()
	if not is_open():return
	var was=kind;kind="";arg="";subtopic="";detail_id="";nav_stack=[];page.hide();page_signature="";pending_policy="";policy_buttons={};policy_prediction={}
	if was in ["resource","population"]:
		app.map_view.layer=previous_layer;app.manual_layer=previous_manual_layer;app.map_view.invalidate()
	app.refresh();app.play_cue("back")
func toggle_pause():
	app.flush_beta_time()
	var g=app.game;g.Civic.ensure(g)
	if bool(g.s.settings.get("user_paused",false)):
		if app.beta_mandatory_pending():app.open_beta_settlement();return
		g.s.settings.user_paused=false;g.s.beta_time.pause_reason=""
	else:g.s.settings.user_paused=true;g.s.beta_time.pause_reason="manual"
	app.save_game();update_hud()
	if kind=="time":render(true)
	app.message("城市已暂停。" if g.s.settings.user_paused else "城市已继续；关闭查看页面后推进时间。")
func advance(_months:float):
	Beta.set_mode(app,"advance")
func toggle_rail():
	rail_expanded=not rail_expanded;task_signature=""
	app.display_settings.task_rail_expanded=rail_expanded;app.save_display_settings();update_hud();app.layout_ui()
func update_hud():
	if app==null or app.game.s.is_empty():return
	var g=app.game;var paused=bool(g.s.settings.get("user_paused",false));var state=g.s.get("beta_time",{});var a=Beta.acceleration(g)
	time_clock.text=(Beta.date(float(state.get("local_elapsed_seconds",0))).trim_prefix("来生 ") if app.compact else Beta.date(float(state.get("local_elapsed_seconds",0))))+"\n"+("已暂停" if paused or blocks_time() or app.decision_kind!="" else Beta.mode_name(g)+" ×%.0f"%a)
	pause_button.text="" if app.compact else ("继续" if paused else "暂停");pause_button.icon=Art.icon("play" if paused else "pause",W.TEXT) if app.compact else null;pause_button.expand_icon=true;pause_button.add_theme_constant_override("icon_max_width",24);pause_button.tooltip_text="暂停与决策期间城市、外贸均冻结"
	app.checkpoint_button.text="■" if app.compact else "停止推进";app.checkpoint_button.tooltip_text="停止推进，返回常速"
	app.checkpoint_button.visible=a>1;app.checkpoint_button.disabled=g.s.phase!="play"
	budget_bar.value=clampf(float(g.s.dt)/float(g.c.beta.time.initial_dt),0,1);budget_bar.tooltip_text="剩余 %.2f DT · 可额外推进 %.2f 年"%[float(g.s.dt),float(g.s.dt)*Beta.dt_seconds(g)/Beta.YEAR]
	for id in mode_buttons:
		var button=mode_buttons[id];button.disabled=(float(g.s.dt)<=0 and id!="normal") or app.decision_kind!=""
		button.add_theme_stylebox_override("normal",W.button_box(W.SOFT,W.GREEN if str(state.get("mode","normal"))==id else W.SOFT))
	var forecast=Beta.risk(g,Beta.risk_snapshot(app))
	var local=float(state.get("local_elapsed_seconds",0));var next_year=(Beta.YEAR-fmod(local,Beta.YEAR))/a
	time_risk.text=str(forecast.text) if a>1 else "资源详情 › 点击物资"
	time_risk.tooltip_text="下一年约 "+Beta.seconds_text(next_year)+"；剩余额外预算 %.2f 年"%(float(g.s.dt)*Beta.dt_seconds(g)/Beta.YEAR)
	if a>1:
		time_risk.text+="\n年度约 "+Beta.seconds_text(next_year)+" · DT %.3f/分"%((a-1)/Beta.dt_seconds(g)*60)
	for id in resource_buttons:
		var n=resource_buttons[id];var quantity=D.quantity(id);var x=M.resource(g,app.shot,id)
		n.head.add_theme_color_override("font_color",Color(quantity.ink));n.icon.texture=Art.icon(str(quantity.icon),Color(quantity.ink))
		if id=="energy":n.head.text="电力 %.1f"%x.supply;n.tail.text="需 %.1f"%x.need
		else:n.head.text="%s %.0f"%[x.name,x.stock];n.tail.text=("弃 %.1f/月"%x.lost if x.lost>.1 else "%+.1f/月"%x.net)+"  ›"
		n.button.tooltip_text=str(x.get("status","当前实际供电"))+"；点击查看供给、消耗与来源。"
	var warning=app.feedback_layer.summary().get("main",{}) if app.feedback_layer!=null else {}
	main_alert.visible=not warning.is_empty();main_alert.text=str(warning.get("text",""));main_alert.tooltip_text="定位实际来源"
	var theme=g.Themes.view(g);var queue=Beta.pending(g);var claimable=queue.size()
	var name=str(theme.get("name","选择委任主题"));task_summary.text=name+" · "+str(theme.get("stage_name",""));task_count.text="待领 %d"%claimable
	task_summary.tooltip_text="当前主题、下一条件、事件与奖励";task_count.add_theme_color_override("font_color",W.GOLD if claimable>0 else W.MUTED)
	var next_condition="主题已完成" if theme.get("completed",false) else "选择这座城市的发展方向"
	for goal in theme.get("goals",[]):
		if not goal.get("met",false):next_condition=str(goal.get("label",""))+" %.1f / %.1f"%[float(goal.get("value",0)),float(goal.get("target",0))];break
	next_condition_label.text=next_condition
	var sig=JSON.stringify([name,next_condition,claimable,rail_expanded,app.compact,theme.get("events",[])])
	active_task_count=claimable
	if sig!=task_signature:
		task_signature=sig;W.clear(task_list);task_list.add_child(W.button("城市消息 ›",func():app.open_city_messages()))
		if claimable>0:task_list.add_child(W.button("领取已保存奖励 ›",func():open("tasks")))
		elif not theme.get("events",[]).is_empty():task_list.add_child(W.button("处理主题事件 ›",func():open("tasks")))
	if is_open() and kind not in ["policies","time"]:
		var version=app.planner.version(g)+":"+kind+":"+arg+":"+subtopic+":"+detail_id+":"+task_tab
		if page_signature!=version:render(true)
func relayout(area:Rect2):
	var compact=app.compact;var scale=W.text_scale;var top=maxf(56.,44.*scale+16.) if compact else 76.
	var tw=app.time_hud_width()
	task_summary.add_theme_font_size_override("font_size",int(14*scale));task_count.add_theme_font_size_override("font_size",int(13*scale))
	var wide_desktop=not compact and scale<=1.2 and area.size.x>=1280 and area.size.y>=800
	time_actions.visible=true
	dt_button.custom_minimum_size=Vector2((104 if scale>1.2 else 82) if compact else 94,40)
	time_clock.visible=true
	pause_button.custom_minimum_size=Vector2((44 if compact else (64 if scale>1.2 else 58)),40)
	app.checkpoint_button.custom_minimum_size=Vector2(44 if compact else 98,40)
	var compact_time_width=minf(maxf(0.,area.size.x),maxf(tw,time_panel.get_combined_minimum_size().x)) if compact else tw
	time_panel.position=Vector2(area.end.x-compact_time_width,area.position.y);time_panel.size=Vector2(compact_time_width,150 if Beta.acceleration(app.game)>1 else 128)
	if wide_desktop:
		time_panel.size=Vector2(tw,maxf(188 if Beta.acceleration(app.game)>1 else 128,time_panel.get_combined_minimum_size().y));time_panel.position=Vector2(area.end.x-tw-16,area.end.y-time_panel.size.y-16)
	for pair in app.pulse_buttons:pair[0].custom_minimum_size.x=64 if scale<=1 else 76
	var resource_w=minf(area.size.x*.55,620.*(1.15 if scale>1 else 1.))
	var resource_h=78.
	resource_grid.visible=false;compact_resource.visible=true
	compact_resource.text="物资 · "+str(Beta.risk(app.game,Beta.risk_snapshot(app)).get("name","稳定"))
	resource_w=minf(area.size.x*.48,280.*scale);resource_h=56
	if compact:resource_w=maxf(104 if scale<=1.2 else 140,compact_resource.get_combined_minimum_size().x+12.);resource_h=56
	if wide_desktop:
		resource_w=minf(280.*scale,area.size.x*.36);resource_h=64
		resource_panel.position=Vector2(area.end.x-resource_w-16,area.position.y+14)
	else:resource_panel.position=Vector2(area.end.x-resource_w-8,area.end.y-resource_h-8)
	resource_panel.size=Vector2(resource_w,resource_h)
	for id in resource_buttons:
		resource_buttons[id].button.custom_minimum_size.x=0
		resource_buttons[id].button.custom_minimum_size.y=48
		resource_buttons[id].head.add_theme_font_size_override("font_size",int((17 if resource_w>=680 else 15)*scale))
		resource_buttons[id].tail.add_theme_font_size_override("font_size",int(13*scale))
	var has_inspector=app.selected_project!="" or app.mode=="site" or is_open() or app.decision_kind!=""
	var expanded=rail_expanded and not has_inspector
	var task_width=minf(maxf(0.,area.size.x-16.),248.+maxf(0.,scale-1.)*60.)
	task_scroll.visible=expanded;task_count.visible=true
	if wide_desktop:task_panel.position=Vector2(area.end.x-task_width-16,area.position.y+94)
	else:task_panel.position=Vector2(area.end.x-task_width-8,maxf(area.position.y+top+12,time_panel.position.y+time_panel.get_combined_minimum_size().y+12) if compact else area.position.y+top+12)
	task_panel.size=Vector2(task_width,(160 if expanded else 88)+(main_alert.get_combined_minimum_size().y+6 if main_alert.visible else 0))
	task_panel.visible=not app.screenshot_mode and app.game.s.phase=="play" and (not is_fullscreen() or wide_desktop) and not has_inspector and not is_open()
	if is_open():
		if kind_is_fullscreen:
			if compact:
				page.position=area.position+Vector2(8,8);page.size=area.size-Vector2(16,16)
			else:
				var window_width=minf(1040,area.size.x*.86);var window_height=area.size.y*.84
				if kind=="finance" and subtopic=="":
					var content_height=body.get_combined_minimum_size().y
					var chrome_height=24.+header.get_combined_minimum_size().y+subtitle.get_combined_minimum_size().y+tabs.get_combined_minimum_size().y+32.
					if footer.visible:chrome_height+=footer.get_combined_minimum_size().y+8.
					window_height=minf(window_height,maxf(320.,content_height+chrome_height))
				var window_size=Vector2(window_width,window_height)
				page.size=window_size;page.position=area.position+(area.size-window_size)*.5
		else:
			var pw=296. if compact else (516. if scale>1. else 464.)
			page.position=Vector2(area.end.x-pw-8,area.position.y+(0. if compact else top+8));page.size=Vector2(pw,area.size.y-(0. if compact else top+104))
		title.add_theme_font_size_override("font_size",int((18 if compact else 24)*scale))
		close_button.text="" if compact else ("返回上页" if not nav_stack.is_empty() else "返回地图");close_button.icon=Art.icon("back" if not nav_stack.is_empty() else "close",W.TEXT) if compact else null;close_button.expand_icon=true;close_button.add_theme_constant_override("icon_max_width",24);close_button.tooltip_text="返回上一级" if not nav_stack.is_empty() else "关闭并返回地图";close_button.custom_minimum_size.x=48 if compact else 108
		if compact:
			secondary.text="‹" if secondary.text in ["返回地图","返回"] else secondary.text
			secondary.tooltip_text="返回上一级" if not nav_stack.is_empty() else "返回地图"
			if kind=="policies":primary.text="选择政策" if pending_policy=="" else ("正在采用" if primary.disabled and pending_policy==str(app.game.s.block.policies.get(app.game.c.policies[pending_policy].slot,"")) else "采用 · %.0f通货"%app.game.c.balance.policy_switch_price)
		cost.visible=not compact or kind=="policies"
		subtitle.visible=not(compact and area.size.y<400)
		scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL
		if compact and scale>1.2:cost.add_theme_font_size_override("font_size",16)
	shield.color=Color(0.015,0.035,0.04,.36)
	shield.visible=is_fullscreen()
	time_panel.visible=not app.screenshot_mode and (not is_fullscreen() or wide_desktop)
	resource_panel.visible=not app.screenshot_mode and (not is_fullscreen() or wide_desktop)
	if compact and not is_fullscreen() and (is_open() or app.inspector_panel.visible or app.decision_kind!=""):
		resource_panel.position=Vector2(area.end.x-296-resource_w-8,area.end.y-resource_h)
		var hud_width=maxf(0.,area.size.x-296.)
		time_panel.position.x=area.position.x;time_panel.size.x=minf(hud_width,maxf(tw,time_panel.get_combined_minimum_size().x));time_clock.visible=hud_width>=tw
	if is_fullscreen():app.workbench.hide()
	elif app.decision_kind!="":app.workbench.show()
func _reset():
	W.clear(body);W.clear(tabs);detail_clock=null;policy_buttons={};footer.hide();primary.show();secondary.show();cost.text="";footer_text.text="";primary.disabled=false
	for cb in primary.pressed.get_connections():primary.pressed.disconnect(cb.callable)
	for cb in secondary.pressed.get_connections():secondary.pressed.disconnect(cb.callable)
	secondary.pressed.connect(back);secondary.text="返回地图"
func render(keep_scroll:bool=true):
	var sy=scroll.scroll_vertical if keep_scroll else 0;_reset()
	match kind:
		"finance":render_finance()
		"policies":render_policies()
		"population":render_population()
		"resource":render_resource()
		"time":render_time()
		"tasks":UI040.render(self,"commissions" if subtopic=="" else subtopic+(":"+detail_id if detail_id!="" else ""))
		"city040":UI040.render(self,arg if subtopic=="" else subtopic+(":"+detail_id if detail_id!="" else ""))
	page_signature=app.planner.version(app.game)+":"+kind+":"+arg+":"+subtopic+":"+detail_id+":"+task_tab
	scroll.set_deferred("scroll_vertical",sy)
	app.request_layout()
func _metric(parent,title_text:String,value:String,detail:String="",color:Color=W.TEXT):
	var v=_group(parent,title_text);v.add_child(W.label(value,28,color))
	if detail!="":v.add_child(W.label(detail,14,W.MUTED))
	return v
func _group(parent:Node,title_text:String="",bg:Color=W.PANEL) -> VBoxContainer:
	var content=C.group(parent,title_text,bg)
	var panel=content.get_parent()
	if panel is PanelContainer:panel.add_theme_stylebox_override("panel",W.box(bg,W.SOFT,10))
	return content
func _row(parent,key:String,value:String,action:Callable=Callable(),color:Color=W.TEXT):
	var h=W.hbox(parent,12);h.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	h.add_child(W.label(key,16,W.MUTED))
	if action.is_valid():
		var b=W.button(value+"  ›",action);b.custom_minimum_size.x=0;b.size_flags_horizontal=Control.SIZE_EXPAND_FILL;h.add_child(b);return b
	var l=W.label(value,16,color);l.horizontal_alignment=HORIZONTAL_ALIGNMENT_RIGHT;h.add_child(l);return l
func _table(parent,columns:Array,rows:Array):
	var hs=ScrollContainer.new();hs.vertical_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;hs.size_flags_horizontal=Control.SIZE_EXPAND_FILL;hs.custom_minimum_size.y=48+36*rows.size();parent.add_child(hs)
	var grid=GridContainer.new();grid.columns=columns.size();grid.size_flags_horizontal=Control.SIZE_EXPAND_FILL;grid.add_theme_constant_override("h_separation",16);grid.add_theme_constant_override("v_separation",10);hs.add_child(grid)
	for text in columns:
		var l=W.label(str(text),14,W.MUTED,false);l.custom_minimum_size.x=80;grid.add_child(l)
	for row in rows:
		for text in row:
			var l=W.label(str(text),16,W.TEXT,false);l.custom_minimum_size.x=80;grid.add_child(l)
	return grid
func render_finance():
	Topics040.render_finance(self)

func render_resource():
	Topics040.render_resource(self)

func render_population():
	Topics040.render_population(self)

func coverage_layer(k:String):
	app.map_view.layer=k;app.manual_layer=true;app.map_view.invalidate();app.refresh_legend();app.layout_ui()
func locate(id:String):
	var g=app.game
	if not g.s.projects.has(id) or g.s.projects[id].get("special",false):app.message("此对象在地图外；已计入总量。详情可在表中查看。");return
	app.selected_project=id;app.map_view.selected_project=id;app.map_view.focus_tile(g.s.projects[id].tile);app.map_view.invalidate()
func render_time():
	Beta.render_time(self)

func render_policies():
	Topics040.render_policies(self)

func choose_policy(id:String):
	pending_policy=id;policy_version=app.planner.version(app.game);policy_prediction={};updating_policy=true
	if subtopic=="preview":detail_id=id;render(true)
	else:push_section("preview",id)
	footer_text.text="计算当前城市下一周期差异…";primary.disabled=true
	call_deferred("calculate_policy",id,policy_version)
func calculate_policy(id:String,version:String):
	if kind!="policies" or pending_policy!=id:return
	var result=app.planner.policy(app.game,id)
	if version!=app.planner.version(app.game) or pending_policy!=id:return
	policy_prediction=result;updating_policy=false;render(true)
func update_policy_footer():
	var g=app.game
	if pending_policy=="" or not g.c.policies.has(pending_policy):return
	var def=g.c.policies[pending_policy];var current=g.s.block.policies[def.slot]==pending_policy
	if current:
		footer_text.text="「"+def.name+"」正在采用；不会重复付费。";primary.text="正在采用";primary.disabled=true;return
	var cost_value=float(g.c.balance.policy_switch_price)
	primary.text=("采用 · %.0f通货"%cost_value) if app.compact else "采用「%s」 · %.0f 通货"%[def.name,cost_value]
	primary.disabled=updating_policy or not policy_prediction.get("ok",false) or g.s.currency<cost_value or policy_version!=app.planner.version(g)
	if updating_policy:footer_text.text="正在计算…";return
	if not policy_prediction.get("ok",false):footer_text.text="预测失败："+str(policy_prediction.get("error","请重新选择"));return
	footer_text.text=("收支 %+.1f/月 · 变化 %+.1f/月"%[policy_prediction.cashflow,policy_prediction.delta]) if app.compact else "预计收支 %+.2f/月 · 比不更换 %+.2f/月 · 只消耗 %.0f 通货"%[policy_prediction.cashflow,policy_prediction.delta,cost_value]
	if g.s.currency<cost_value:footer_text.text+=" · 预算不足"
func apply_policy():
	var g=app.game
	if pending_policy=="" or updating_policy:return
	if policy_version!=app.planner.version(g):choose_policy(pending_policy);app.message("城市已变化，已重新计算；请再次确认。");return
	var err=g.switch_policy(pending_policy)
	if err!="":footer_text.text=err;return
	g.Civic.finalize(g);app.save_game();app.display_pending_tick=int(g.s.simulation_tick);app.refresh();pending_policy="";policy_prediction={};subtopic="";detail_id="";nav_stack=[];render(false);app.play_cue("confirm")
func render_tasks():
	UI040.render_commissions(self)
