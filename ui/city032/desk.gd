extends Control
## 0.3.2 City Desk: concrete information entry points on the existing map.
const Beta=preload("res://ui/beta/interface.gd")
const W=preload("res://ui/widgets.gd")
const V=preload("res://ui/reset/desk_visuals.gd")
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
var alert_panel:VBoxContainer
var severe_buttons:Array=[]
var alert_overflow:Button
var severe_alerts:Array=[]
var theme_progress:ProgressBar
var theme_claim:Button
var theme_deadline:Label
var theme_ready_before:int=-1
var theme_seen_rewards:Array=[]
var preferred_reward:String=""
var theme_identity:String=""
var theme_attention:bool=false
var next_condition_label:Label
var risk_resource_name:String="粮食"
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
var breadcrumbs:HBoxContainer
var page_icon:TextureRect
var footer_separator:ColorRect
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
	time_panel=W.panel(self,W.PANEL);time_panel.name="L0Time";time_panel.set_meta("l0_item","time")
	var style=W.box(W.PANEL,W.DIVIDER,18);style.content_margin_top=12;style.content_margin_bottom=12;style.content_margin_left=18;style.content_margin_right=18;time_panel.add_theme_stylebox_override("panel",style)
	var outer=W.hbox(time_panel,20)
	dt_button=W.flat(W.button("",func():open("time"),false,"DT 是额外发展预算；点击查看来生历与时间规则"));dt_button.name="L0DT";dt_button.set_meta("l0_item","dt");dt_button.custom_minimum_size=Vector2(126,62);outer.add_child(dt_button)
	var dv=W.vbox(dt_button,5);dv.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);dv.mouse_filter=Control.MOUSE_FILTER_IGNORE;dv.alignment=BoxContainer.ALIGNMENT_CENTER
	app.dt_label=W.label("",28,W.ACCENT,false);dv.add_child(app.dt_label)
	budget_bar=W.bar(1);budget_bar.custom_minimum_size=Vector2(112,5);dv.add_child(budget_bar)
	var controls=W.vbox(outer,6);controls.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	clock_row=W.hbox(controls,10);time_clock=W.label("",17,W.TEXT,false);time_clock.size_flags_horizontal=Control.SIZE_EXPAND_FILL;clock_row.add_child(time_clock)
	pause_button=W.icon_button("pause","",toggle_pause,"暂停 / 继续 · P");pause_button.name="BetaPause";pause_button.custom_minimum_size=Vector2(40,32);clock_row.add_child(pause_button)
	app.checkpoint_button=W.button("停止推进",func():Beta.set_mode(app,"normal"));app.checkpoint_button.name="BetaStop";app.checkpoint_button.custom_minimum_size=Vector2(100,32);clock_row.add_child(app.checkpoint_button)
	time_actions=W.hbox(controls,5);app.pulse_buttons=[]
	for mode in Beta.mode_rows(app.game):
		var id=str(mode[0]);var button=W.button(str(mode[1]),func():Beta.set_mode(app,id));button.name="BetaMode_"+id;button.custom_minimum_size=Vector2(76,32);button.size_flags_horizontal=Control.SIZE_EXPAND_FILL;time_actions.add_child(button);mode_buttons[id]=button
	time_risk=W.label("",13,W.MUTED);time_panel.add_child(time_risk);time_risk.hide()
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
	alert_panel=W.vbox(self,8);alert_panel.name="L1Alerts"
	for index in 2:
		var button=W.button("",func():
			if index<severe_alerts.size():app.on_beta_source(severe_alerts[index]))
		button.name="SevereAlert"+str(index);button.custom_minimum_size=Vector2(320,56);button.alignment=HORIZONTAL_ALIGNMENT_LEFT;button.autowrap_mode=TextServer.AUTOWRAP_WORD_SMART
		button.add_theme_stylebox_override("normal",W.button_box(Color("39272A"),W.DANGER,12));button.add_theme_color_override("font_color",W.DANGER)
		alert_panel.add_child(button);severe_buttons.append(button)
	main_alert=severe_buttons[0]
	alert_overflow=W.button("",func():app.open_alert_archive());alert_overflow.name="AlertOverflow";alert_overflow.add_theme_stylebox_override("normal",W.button_box(W.PANEL,W.DIVIDER,10));alert_overflow.add_theme_color_override("font_color",W.MUTED);alert_panel.add_child(alert_overflow)
	task_panel=W.panel(self);task_panel.name="L1Theme";task_panel.minimum_size_changed.connect(app.request_layout)
	var style=W.box(W.PANEL,W.DIVIDER,16);style.content_margin_left=18;style.content_margin_right=18;style.content_margin_top=15;style.content_margin_bottom=15;task_panel.add_theme_stylebox_override("panel",style)
	task_root=W.vbox(task_panel,10)
	var h=W.hbox(task_root,8)
	task_summary=W.flat(W.button("",func():open("tasks")));task_summary.name="ThemeTitle";task_summary.alignment=HORIZONTAL_ALIGNMENT_LEFT;task_summary.size_flags_horizontal=Control.SIZE_EXPAND_FILL;task_summary.add_theme_font_size_override("font_size",18);h.add_child(task_summary)
	task_count=W.label("",16,W.MUTED,false);h.add_child(task_count)
	theme_progress=W.bar(1);theme_progress.name="ThemeProgress";theme_progress.custom_minimum_size.y=9;task_root.add_child(theme_progress)
	next_condition_label=W.label("",15,W.MUTED);task_root.add_child(next_condition_label)
	theme_deadline=W.label("",13,W.MUTED);task_root.add_child(theme_deadline)
	theme_claim=W.button("领取",claim_theme_reward);theme_claim.name="ThemeClaim";theme_claim.custom_minimum_size.y=40;task_root.add_child(theme_claim)
	task_toggle=W.button("",toggle_rail);task_toggle.hide();task_root.add_child(task_toggle)
	task_scroll=ScrollContainer.new();task_root.add_child(task_scroll);task_scroll.hide();task_list=W.vbox(task_scroll,4)
func _build_page():
	shield=ColorRect.new();shield.color=Color(0,0,0,0);shield.mouse_filter=Control.MOUSE_FILTER_STOP;shield.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(shield);shield.hide()
	page=W.panel(self);page.name="CityPage032";page.clip_contents=true
	var p=W.box(W.PANEL,W.SOFT,18);p.content_margin_left=24;p.content_margin_right=24;p.content_margin_top=20;p.content_margin_bottom=18;page.add_theme_stylebox_override("panel",p)
	page_root=W.vbox(page,14)
	breadcrumbs=W.hbox(page_root,8);breadcrumbs.name="CityBreadcrumbs"
	header=W.hbox(page_root,18);header.name="CityPageHeader"
	page_icon=V.icon(header,"world",58,W.ACCENT)
	var heading=W.vbox(header,6);heading.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	title=W.label("",28);heading.add_child(title)
	subtitle=W.label("",14,W.MUTED);heading.add_child(subtitle)
	cost=W.label("",15,W.GOLD);cost.custom_minimum_size.x=230;cost.size_flags_horizontal=Control.SIZE_SHRINK_END;cost.size_flags_vertical=Control.SIZE_SHRINK_CENTER;header.add_child(cost)
	close_button=W.icon_button("back","返回地图",back,"返回上一层；最外层返回地图");close_button.name="CityPageClose040";close_button.custom_minimum_size=Vector2(136,44);header.add_child(close_button)
	tabs=W.hbox(page_root,8)
	scroll=DragScroll.new();scroll.name="CityPageScroll032";scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;page_root.add_child(scroll)
	body=W.vbox(scroll,16);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL;body.minimum_size_changed.connect(app.request_layout)
	footer_separator=ColorRect.new();footer_separator.color=W.DIVIDER;footer_separator.custom_minimum_size.y=1;footer_separator.mouse_filter=Control.MOUSE_FILTER_IGNORE;page_root.add_child(footer_separator)
	footer=W.vbox(page_root,8);footer.name="CityActionDock";footer_text=W.label("",15,W.GREEN);footer.add_child(footer_text);footer_buttons=W.hbox(footer,12)
	secondary=W.button("返回",close);secondary.name="CitySecondary032";secondary.custom_minimum_size=Vector2(144,44);footer_buttons.add_child(secondary)
	var spacer=Control.new();spacer.mouse_filter=Control.MOUSE_FILTER_IGNORE;spacer.size_flags_horizontal=Control.SIZE_EXPAND_FILL;footer_buttons.add_child(spacer)
	primary=W.primary(W.button("",func():pass));primary.name="CityPrimary032";primary.custom_minimum_size=Vector2(360,48);footer_buttons.add_child(primary)
	page.hide()
func _page_icon_id() -> String:
	match kind:
		"finance":return "ledger"
		"population":return "population"
		"resource":return arg
		"policies":return "policy"
		"time":return "clock"
		"tasks":return "world"
		"city040":
			match arg.split(":")[0]:
				"market":return "logistics"
				"permits","install","block_permits","organization_permits":return "layers"
				"block":return "world"
				_:return "build"
	return "ledger"
func _decorate_page() -> void:
	var icon_id=_page_icon_id()
	page_icon.texture=load("res://ui/reset/population.svg") if icon_id=="population" else Art.icon(icon_id,W.ACCENT)
	W.clear(breadcrumbs)
	var home=W.flat(W.button("城市档案",func():close();app.open_archive()));home.name="CityArchiveHome";home.custom_minimum_size.y=30;home.add_theme_font_size_override("font_size",14);breadcrumbs.add_child(home)
	for index in nav_stack.size():
		breadcrumbs.add_child(W.label("/",14,W.MUTED,false))
		var item:Dictionary=nav_stack[index]
		var link=W.flat(W.button(str(item.get("label","上一层")),func():back_to(index)));link.custom_minimum_size.y=30;link.add_theme_font_size_override("font_size",14);breadcrumbs.add_child(link)
	breadcrumbs.add_child(W.label("/",14,W.MUTED,false))
	var current=W.label(title.text,14,W.ACCENT,false);current.clip_text=true;current.text_overrun_behavior=TextServer.OVERRUN_TRIM_ELLIPSIS;breadcrumbs.add_child(current)
	var has_action=footer.visible and primary.visible and primary.text!=""
	primary.visible=has_action
	footer.show();footer_text.visible=footer_text.text!=""
	if secondary.text=="返回地图" and not nav_stack.is_empty():secondary.text="返回上页"
	tabs.visible=tabs.get_child_count()>0
	cost.visible=cost.text!=""
	page.set_meta("graphic_role","archive_workspace");page.set_meta("section",kind+":"+arg)
func back_to(index:int) -> void:
	if index<0 or index>=nav_stack.size():return
	while nav_stack.size()>index+1:nav_stack.pop_back()
	back()
func is_open() -> bool:return kind!=""
func is_fullscreen() -> bool:return is_open()
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
	return {"label":title.text,"kind":kind,"arg":arg,"subtopic":subtopic,"detail_id":detail_id,"task_tab":task_tab,"finance_mode":finance_mode,"pending_policy":pending_policy,"policy_version":policy_version,"policy_prediction":policy_prediction.duplicate(true),"scroll":scroll.scroll_vertical}
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
	var g=app.game;var state=g.s.get("beta_time",{});var a=Beta.acceleration(g)
	var paused=bool(g.s.settings.get("user_paused",false)) or blocks_time() or app.decision_kind!="" or app.overlay.visible
	time_clock.text=Beta.date(float(state.get("local_elapsed_seconds",0))).trim_prefix("来生 ")+(" · 暂停" if paused else "")
	pause_button.icon=Art.icon("play" if bool(g.s.settings.get("user_paused",false)) else "pause",W.TEXT)
	pause_button.tooltip_text="继续 · P" if bool(g.s.settings.get("user_paused",false)) else "暂停 · P"
	app.checkpoint_button.visible=a>1;app.checkpoint_button.disabled=g.s.phase!="play"
	app.dt_label.text="%.1f DT"%float(g.s.dt)
	budget_bar.value=clampf(float(g.s.dt)/float(g.c.beta.time.initial_dt),0,1)
	var remaining=float(g.s.dt)*Beta.dt_seconds(g)/Beta.YEAR
	dt_button.tooltip_text="剩余 %.2f DT · 可额外推进 %.2f 来生年"%[float(g.s.dt),remaining]
	if a>1:dt_button.tooltip_text+="\n当前 %.3f DT / 现实分钟 · 约可持续 %s"%[(a-1)/Beta.dt_seconds(g)*60,Beta.seconds_text(float(g.s.dt)*Beta.dt_seconds(g)/(a-1))]
	for id in mode_buttons:
		var button=mode_buttons[id];var active=str(state.get("mode","normal"))==id
		button.disabled=(float(g.s.dt)<=0 and id!="normal") or app.decision_kind!="" or is_open() or app.overlay.visible
		button.add_theme_stylebox_override("normal",W.button_box(W.RAISED if active else W.PANEL,W.ACCENT if active else W.DIVIDER,8))
		button.add_theme_color_override("font_color",W.ACCENT if active else W.MUTED)
		button.tooltip_text=str(button.text)+" · "+("常速不消耗 DT" if id=="normal" else "消耗有限 DT 推进本地时间")
	var data=app.feedback_layer.summary() if app.feedback_layer!=null else {}
	severe_alerts=[]
	for warning in data.get("summaries",[]):
		if int(warning.get("severity",0))>=2:severe_alerts.append(warning)
	for index in severe_buttons.size():
		var button=severe_buttons[index];button.visible=index<severe_alerts.size()
		if button.visible:button.text="!  "+str(severe_alerts[index].text)+"  ›";button.tooltip_text="定位正在发生的问题";button.set_meta("source",severe_alerts[index])
	alert_overflow.visible=severe_alerts.size()>2;alert_overflow.text="另有 %d 条待处理 ›"%maxi(0,severe_alerts.size()-2)
	var card=Beta.theme_hud_state(g);var theme:Dictionary=card.view;active_task_count=Beta.pending(g).size()
	var ready=bool(card.ready);var identity=str(g.s.get("seed",0))+":"+str(card.id)
	if identity!=theme_identity:theme_identity=identity;theme_ready_before=-1;theme_seen_rewards=[]
	var ids=[]
	for reward in card.rewards:ids.append(str(reward.get("reward_id","")))
	if theme_ready_before>=0 and ids.any(func(id):return not id in theme_seen_rewards):app.play_cue("complete")
	theme_seen_rewards=ids;theme_ready_before=1 if ready else 0
	var ratio=float(card.progress);theme_progress.value=ratio;theme_attention=ratio>.9 and not ready and not bool(card.completed)
	var tone=W.GOLD if ready or theme_attention else W.ACCENT
	theme_progress.add_theme_stylebox_override("fill",W.box(tone,tone,5));task_count.text="%d%%"%roundi(ratio*100);task_count.add_theme_color_override("font_color",tone)
	task_summary.text=str(theme.get("name",""));next_condition_label.text="主题完成" if card.completed else str(theme.get("stage_name",""))+" · "+str(card.next)
	theme_claim.visible=ready;theme_claim.text="选择主题研究 ›" if card.reward_kind=="research" else "领取主题奖励 ›"
	theme_claim.set_meta("reward_id",str(card.reward_id));theme_claim.add_theme_stylebox_override("normal",W.button_box(W.GOLD,W.GOLD,9));theme_claim.add_theme_color_override("font_color",W.BASE)
	task_panel.set_meta("theme_state",card);theme_progress.tooltip_text="主题总进度；本阶段最弱条件 %.0f%%"%(float(card.phase_progress)*100)
	theme_deadline.hide()
	for event in theme.get("events",[]):
		if event.get("status","")=="active" and float(event.get("deadline_seconds",0))>0:
			theme_deadline.text=str(event.get("name","主题事件"))+" · 余 "+g.Clock.duration(g,float(event.deadline_seconds));theme_deadline.show();break
	if is_open() and kind not in ["policies","time"]:
		var version=app.planner.version(g)+":"+kind+":"+arg+":"+subtopic+":"+detail_id+":"+task_tab
		if page_signature!=version:render(true)
func claim_theme_reward():
	preferred_reward=str(theme_claim.get_meta("reward_id",""));open("tasks")
func _process(_delta):
	if task_panel==null or app==null:return
	var pulse=1.0
	if theme_attention and not bool(app.display_settings.get("reduced_motion",false)):pulse=.87+.13*(.5+.5*sin(Time.get_ticks_msec()/850.0))
	theme_progress.modulate=Color(1,1,1,pulse)
func relayout(area:Rect2):
	var visible=not app.screenshot_mode and not is_open() and app.decision_kind=="" and not app.overlay.visible
	time_panel.position=Vector2(area.end.x-662,area.position.y+24);time_panel.size=Vector2(638,112);time_panel.visible=not app.screenshot_mode
	resource_panel.hide()
	alert_panel.position=area.position+Vector2(24,148);alert_panel.size=Vector2(338,160);alert_panel.visible=visible and not severe_alerts.is_empty()
	task_panel.position=Vector2(area.end.x-362,area.position.y+148);task_panel.size=Vector2(338,188 if theme_claim.visible else 146)
	task_panel.visible=visible and str(app.game.Themes.view(app.game).get("id",""))!=""
	if app.inspector_panel.visible:task_panel.position=area.position+Vector2(24,330 if alert_panel.visible else 148)
	if is_open():
		var chrome=110.+breadcrumbs.get_combined_minimum_size().y+header.get_combined_minimum_size().y+footer.get_combined_minimum_size().y
		if tabs.visible:chrome+=tabs.get_combined_minimum_size().y+14
		var dimensions=Vector2(minf(1280,area.size.x-96),clampf(body.get_combined_minimum_size().y+chrome,460,minf(860,area.size.y-150)));page.size=dimensions;page.position=Vector2((area.size.x-dimensions.x)*.5,140+(area.size.y-140-dimensions.y)*.5)
		title.add_theme_font_size_override("font_size",28);close_button.text="返回上页" if not nav_stack.is_empty() else "返回地图"
		cost.visible=cost.text!="";subtitle.visible=subtitle.text!="";scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL
	shield.color=Color(.025,.035,.04,.62);shield.offset_top=140;shield.visible=is_open()
	if is_open():app.workbench.hide()
	elif app.decision_kind!="":app.workbench.show()
func _reset():
	W.clear(body);W.clear(tabs);detail_clock=null;policy_buttons={};footer.hide();primary.show();secondary.show();cost.text="";footer_text.text="";primary.text="";primary.disabled=false
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
	_decorate_page()
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
