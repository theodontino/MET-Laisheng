extends PanelContainer
## L3 decision workbench. Selection only changes a detached comparison view.
const W=preload("res://ui/widgets.gd")
const C031=preload("res://ui/components031/components.gd")
const D031=preload("res://ui/components031/design.gd")
const P=preload("res://ui/presentation.gd")
const Art=preload("res://ui/map/art.gd")
const ImpactModel=preload("res://ui/view_models/impact_model.gd")
const ImpactPanel=preload("res://ui/reset/impact_panel.gd")
var app
var selected_offer:String=""
var preview:Dictionary={}
var preview_version:String=""
var reference:Dictionary={}
var candidate_previews:Dictionary={}
var candidate_cache_version:String=""
var candidate_draft_key:String=""
var buttons:Dictionary={}
var cards:Dictionary={}
var map_buttons:Dictionary={}
var apply_button:Button
var info_label:Label
var impact_holder:VBoxContainer
var impact_model:Dictionary={}
var page:String="summary"
var mode:String="tender"
var policy_id:String=""
var policy_slot:String="Tax"
var title_label:Label
var footer:HBoxContainer

func setup(a):
	app=a;add_theme_stylebox_override("panel",W.box(W.PANEL,W.SOFT,10));show()

func _group(parent:Node,title:String,bg:Color=W.PANEL) -> VBoxContainer:
	var content=C031.group(parent,title,bg);var panel=content.get_parent()
	if panel is PanelContainer:
		var style=W.box(bg,W.SOFT,8);style.content_margin_left=10;style.content_margin_right=10;style.content_margin_top=8;style.content_margin_bottom=8
		panel.add_theme_stylebox_override("panel",style);content.add_theme_constant_override("separation",5)
	return content

func show_tender():
	mode="tender";page="summary";selected_offer="";app.map_view.preview_map_enabled=true;rebuild()

func show_policy(slot:String="Tax"):
	mode="policy";policy_slot=slot;policy_id="";page="summary";rebuild()

func offers() -> Array:
	if mode=="tender":return app.game.s.draft.get("offers",[])
	var result=[]
	for id in app.game.Rules.ids(app.game.c.policies):
		if app.game.c.policies[id].slot==policy_slot:result.append({"id":id})
	return result

func _ensure_tender_previews(items:Array) -> void:
	var g=app.game;var version=app.planner.version(g)
	var key=JSON.stringify([g.s.draft.get("base",""),g.s.draft.get("tile",""),items])
	if version==candidate_cache_version and key==candidate_draft_key:return
	candidate_previews={};reference=app.planner.snapshot(g)
	for item in items:candidate_previews[item.id]=app.planner.compare(g,str(g.s.draft.base),str(g.s.draft.tile),item)
	candidate_cache_version=version;candidate_draft_key=key

func _selected(items:Array) -> Dictionary:
	for item in items:
		if item.id==selected_offer:return item
	return {}

func rebuild():
	if app==null:return
	W.clear(self);buttons={};cards={};map_buttons={};impact_holder=null;info_label=null
	var g=app.game;var items=offers()
	if items.is_empty():hide();return
	if selected_offer=="" or not items.any(func(x):return x.id==selected_offer):selected_offer=items[0].id
	if mode=="tender":_ensure_tender_previews(items)
	var root=W.vbox(self,compact_gap());root.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	var top=W.hbox(root,8)
	var name="地区政策" if mode=="policy" else "投标 · "+str(g.c.projects.get(g.s.draft.get("base",""),{}).get("name","新建建筑"))
	title_label=W.label(name,18,W.TEXT,false);title_label.clip_text=true;title_label.text_overrun_behavior=TextServer.OVERRUN_TRIM_ELLIPSIS;top.add_child(title_label)
	if mode=="tender":_build_map_toggle(top)
	else:top.add_child(W.label("比较中 · 本地时间暂停",12,W.GOLD,false))
	var common={"currency":g.c.balance.policy_switch_price,"material":0.,"cp":0.}
	if mode=="tender":common=g.Draft.offer_cost(g,items[0])
	var shared="%.0f通货"%float(common.currency)
	if float(common.get("material",0))>0:shared+=" · %.0f材料 · %.1f CP"%[float(common.material),float(common.get("cp",0))]
	var cost=W.label("共同成本  "+shared+(" · 假定完工" if mode=="tender" else ""),12,W.GOLD,false)
	cost.tooltip_text="预测假定候选方案已完工，比较同一下一结算周期。预测不扣款、不推进城市；施工时间、材料和支付另计。";top.add_child(cost)
	var scroll=ScrollContainer.new();scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;root.add_child(scroll)
	if page=="details":
		var body=W.vbox(scroll,9);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL;_build_details(body,_selected(items))
	elif mode=="tender":
		var body=W.hbox(scroll,12);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL
		var candidates=W.vbox(body,6);candidates.custom_minimum_size.x=290;candidates.size_flags_horizontal=Control.SIZE_FILL
		_build_candidates(candidates,items)
		impact_holder=W.vbox(body,0);impact_holder.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	else:
		var body:Container=W.vbox(scroll,8) if app.compact else W.hbox(scroll,12);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL
		_build_policy_cards(body,items)
	if mode=="policy" and page=="summary":
		info_label=W.label("",14,W.GREEN);info_label.max_lines_visible=2;root.add_child(info_label)
	_build_footer(root)
	update_preview();app.request_layout()

func _small_button(text:String,fn:Callable,tip:String="") -> Button:
	var button=W.ghost(W.button(text,fn,false,tip));button.custom_minimum_size=Vector2(78,26*W.text_scale)
	button.add_theme_font_size_override("font_size",int(12*W.text_scale));return button

func _build_map_toggle(parent:Node) -> void:
	for proposal in [false,true]:
		var button=_small_button("建成后" if proposal else "现状预测",func():show_forecast_map(proposal),"地图使用同一下一周期的预测；选择不会提交方案。")
		button.name="MapForecastProposal" if proposal else "MapForecastBaseline";button.set_meta("ui_role","preview_map_toggle");button.set_meta("map_proposal",proposal)
		parent.add_child(button);map_buttons[proposal]=button
	refresh_preview_buttons()

func refresh_preview_buttons() -> void:
	for proposal in map_buttons:
		var selected=bool(proposal)==app.map_view.preview_map_enabled
		map_buttons[proposal].add_theme_stylebox_override("normal",W.button_box(W.SOFT if selected else W.PANEL,W.ACCENT if selected else W.DIVIDER))
		map_buttons[proposal].set_meta("selected",selected)

func show_forecast_map(proposal:bool) -> void:
	app.map_view.preview_map_enabled=proposal;refresh_preview_buttons();app.map_view.invalidate();app.refresh_legend()

func _candidate_metrics(items:Array) -> Array:
	var choices=[]
	for id in ["food","material","goods"]:
		var maximum=0.
		for item in items:maximum=maxf(maximum,float(candidate_previews.get(item.id,{}).get("outputs",{}).get(id,0)))
		if maximum>.001:choices.append({"id":id,"label":P.RES[id],"unit":"/月","max":maximum,"kind":"output"})
	for spec in [{"id":"housing","label":"住房","unit":"位","kind":"value"},{"id":"energy_supply","label":"供电","unit":"能力","kind":"value"}]:
		var maximum=0.
		for item in items:maximum=maxf(maximum,absf(float(candidate_previews.get(item.id,{}).get(spec.id,0))))
		if maximum>.001:
			var entry=spec.duplicate();entry["max"]=maximum;choices.append(entry)
	if choices.size()<2:
		for kind in app.game.c.balance.service_kinds:
			var maximum=0.
			for item in items:maximum=maxf(maximum,absf(float(candidate_previews.get(item.id,{}).get("added_service",{}).get(kind,0))))
			if maximum>.001:choices.append({"id":str(kind),"label":str(P.KIND[kind]),"unit":"人","max":maximum,"kind":"service"})
	return choices.slice(0,2)

func _candidate_metric(parent:Node,result:Dictionary,spec:Dictionary) -> void:
	var value=float(result.get("outputs",{}).get(spec.id,0)) if spec.kind=="output" else (float(result.get("added_service",{}).get(spec.id,0)) if spec.kind=="service" else float(result.get(spec.id,0)))
	var group=W.vbox(parent,2);group.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	group.set_meta("candidate_metric",str(spec.id));group.set_meta("metric_value",value);group.set_meta("metric_unit",str(spec.unit));group.set_meta("axis_max",float(spec.max))
	group.add_child(W.label(str(spec.label)+" %+.1f"%value+str(spec.unit),11,W.TEXT,false))
	group.add_child(ImpactPanel.thin_bar(absf(value)/float(spec.max),W.DANGER if value<0 else W.INFO))
	group.tooltip_text="同一指标在三家运营者之间使用同一比例尺。"+("住房为容量，不是新增人口。" if spec.id=="housing" else ("电力为全城供给能力变化，不是库存或/月。" if spec.id=="energy_supply" else ""))

func _build_candidates(parent:Node,items:Array) -> void:
	var g=app.game;var metrics=_candidate_metrics(items)
	for item in items:
		var org=g.c.operators[item.operator_org];var selected=item.id==selected_offer
		var card=_group(parent,"",W.SOFT if selected else W.PANEL);cards[item.id]=card
		card.set_meta("operator_offer",str(item.id));card.set_meta("operator_id",str(item.operator_org))
		var button=W.button(("✓ " if selected else "")+str(org.name),func():choose(str(item.id)),false,str(org.get("summary","")))
		button.icon=Art.icon("org_"+str(item.operator_org),W.TEXT);button.expand_icon=true;button.add_theme_constant_override("icon_max_width",24)
		button.custom_minimum_size.x=0;button.size_flags_horizontal=Control.SIZE_EXPAND_FILL;button.custom_minimum_size.y=34*W.text_scale
		button.add_theme_font_size_override("font_size",int(14*W.text_scale));button.set_meta("offer_id",str(item.id));_selection_style(button,selected)
		card.add_child(button);buttons[item.id]=button
		var weights:Array=org.get("domain_weights",{}).keys();weights.sort_custom(func(a,b):return float(org.domain_weights[a])>float(org.domain_weights[b]))
		var domains=[]
		for domain in weights.slice(0,2):domains.append(str(D031.domain(str(domain)).get("label",domain)))
		var growth=W.hbox(card,5)
		var subtitle=W.label((str(domains[0]) if not domains.is_empty() else "运营者")+" · T1 → T2 → T3",11,W.MUTED,false);subtitle.tooltip_text=" / ".join(domains)+"：投标偏好领域，实际作用见右侧预测。";growth.add_child(subtitle)
		var track:Array=item.get("slot_track",org.get("slot_track",[]));var strip=Art.slot_strip(g,track);strip.set_meta("slot_track",track.duplicate());strip.tooltip_text=" → ".join(track.map(func(type):return D031.slot_label(str(type))))+"；槽位随阶段逐个出现。";growth.add_child(strip)
		var result:Dictionary=candidate_previews.get(item.id,{})
		if result.get("ok",false):
			var row=W.hbox(card,8)
			for metric in metrics:_candidate_metric(row,result,metric)
		else:card.add_child(W.label(str(result.get("error","预测暂不可用")),11,W.MUTED))

func _build_policy_cards(parent:Node,items:Array) -> void:
	var g=app.game
	for item in items:
		var policy=g.c.policies[item.id];var card=_group(parent,"制度 · "+policy_slot)
		var button=W.button(policy.name,func():choose(str(item.id)));button.custom_minimum_size.x=0;card.add_child(button);buttons[item.id]=button
		_selection_style(button,item.id==selected_offer)
		card.add_child(W.label("正在采用" if g.s.block.policies[policy_slot]==item.id else "可选择",14,W.MUTED))
		card.add_child(W.label(policy.summary,16))

func _fold(parent:Node,title:String,lines:Array) -> void:
	var group=W.vbox(parent,4);var content=W.vbox(group,5);group.remove_child(content)
	var toggle=W.ghost(W.button(title+" ›",func():content.visible=not content.visible));toggle.set_meta("full_detail",title);group.add_child(toggle);group.add_child(content)
	for line in lines:content.add_child(W.label(str(line),14,W.MUTED))
	content.hide()

func _build_details(parent:Node,item:Dictionary) -> void:
	var g=app.game
	if mode=="policy":
		var policy=g.c.policies[item.id];W.section(parent,policy.name,policy.summary);parent.add_child(W.button("切换政策类别",func():app.policy_categories()))
		preview=app.planner.policy(g,selected_offer)
		W.section(parent,"下一周期预测","预计收支 %+.1f/月 · 比不更换 %+.1f/月"%[float(preview.get("cashflow",0)),float(preview.get("delta",0))]);return
	preview=candidate_previews.get(item.id,{})
	var org=g.c.operators[item.operator_org];var identity=_group(parent,"运营者 · "+str(org.name),W.SOFT)
	identity.add_child(W.label(str(org.get("summary","")),15,W.TEXT))
	var track:Array=item.get("slot_track",org.get("slot_track",[]));identity.add_child(Art.slot_strip(g,track))
	identity.add_child(W.label("T1 → T2 → T3："+" → ".join(track.map(func(type):return D031.slot_label(str(type)))),13,W.MUTED))
	var weights=[]
	for domain in org.get("domain_weights",{}):weights.append(str(D031.domain(str(domain)).get("label",domain))+" "+str(org.domain_weights[domain]))
	identity.add_child(W.label("投标偏好："+" · ".join(weights),13,W.MUTED))
	if not preview.get("ok",false):W.section(parent,"预测不可用",str(preview.get("error","当前无法生成预测。")));return
	ImpactPanel.build(parent,ImpactModel.build(g,reference,preview),func(kind):app.show_plan_layer(str(kind)))
	var production=[]
	for resource in ["food","material","goods"]:
		production.append("%s：产出 %.3f/月 · 投入 %.3f/月 · 可入库 %.3f/月 · 损耗 %.3f/月"%[P.RES[resource],float(preview.get("outputs",{}).get(resource,0)),float(preview.get("inputs",{}).get(resource,0)),float(preview.get("retained",{}).get(resource,0)),float(preview.get("losses",{}).get(resource,0))])
	production.append("住房容量 %+.0f位；财政收支相对现状预测 %+.3f/月。"%[float(preview.get("housing",0)),float(preview.get("cashflow",0))])
	production.append("全城供电能力 %+.3f；需求能力 %+.3f；电力满足率 %.1f%%。"%[float(preview.get("energy_supply",0)),float(preview.get("energy_demand",0)),float(preview.get("energy_ratio",0))*100])
	_fold(parent,"完整产出与投入",production)
	var service=[]
	for kind in g.c.balance.service_kinds:service.append("%s：该建筑实际服务 %.2f人；全城覆盖变化 %+.2f人。"%[P.KIND[kind],float(preview.get("services",{}).get(kind,0)),float(preview.get("added_service",{}).get(kind,0))])
	service.append("就业：预计 %.2f / %.2f岗位；运行率 %.1f%%。"%[float(preview.get("workers",0)),float(preview.get("stats",{}).get("jobs",0)),float(preview.get("activity",{}).get("utilization",0))*100])
	service.append("劳力、电力、原料满足率：%.1f%% / %.1f%% / %.1f%%。"%[float(preview.get("activity",{}).get("labor",0))*100,float(preview.get("activity",{}).get("energy",0))*100,float(preview.get("activity",{}).get("resources",0))*100])
	for affected in preview.get("affected",[]):service.append("%s：预测运行率下降 %.1f个百分点。"%[g.project_name(str(affected.id)),-float(affected.delta)*100])
	_fold(parent,"完整服务与运行",service)
	_fold(parent,"预测口径与环境来源",[
		"前后都是同一时刻的下一结算周期：不采取行动，与假定选中方案已完工。施工、支付与一次性材料另计；预测不扣款、不推进、不洗牌。",
		"资源按来生月显示，电力是供需能力，住房是容量。覆盖范围不等于真实服务人数。需求占比为该建筑产出除以全城请求需求，超过100%仍照实显示。",
		"库存是当下真实总量。可撑月数仅按当前总库存 / 预测净消耗估算，局部库存不可达、后续贸易与一次性支出会改变结果；无净消耗不代表无限保证。",
		"污染状态增加超过0.015的现有住户：%.0f人；人数加权暴露变化 %+.5f；全城回收变化 %+.5f。"%[float(preview.get("harmed",0)),float(preview.get("exposure_delta",0)),float(preview.get("recycled",0))],
		"预测来源版本："+str(preview.get("snapshot_id",""))])
	_fold(parent,"规则与场完整追踪",[JSON.stringify(preview.get("trace",{}),"  ")])

func _build_footer(root:Node) -> void:
	var g=app.game;footer=W.hbox(root,8)
	if mode=="tender":
		var rc=g.Draft.reroll_cost(g);footer.add_child(W.ghost(W.button("换一组 · %.0f"%float(rc.currency),reroll,false,"消耗 %.0f通货 / %.1fDT，重新生成三家真实运营者。"%[float(rc.currency),float(rc.dt)])))
	else:footer.add_child(W.button("换政策类别",func():app.exit_workbench();app.Dialogs.block(app)))
	var detail=W.ghost(W.button("详情 ›" if page=="summary" else "‹ 返回对比",func():page="details" if page=="summary" else "summary";rebuild()));detail.name="TenderDetails";detail.set_meta("ui_role","details_navigation");detail.custom_minimum_size.x=92;footer.add_child(detail)
	var action_label="采用政策" if mode=="policy" else ("采用并建设" if g.s.draft.kind=="development" else "采用方案")
	apply_button=W.primary(W.button(action_label,commit));apply_button.name="TenderConfirmPrimary";apply_button.set_meta("ui_role","primary_action");apply_button.custom_minimum_size.x=160;apply_button.size_flags_horizontal=Control.SIZE_EXPAND_FILL;footer.add_child(apply_button)
	footer.add_child(W.ghost(W.button("返回地图",func():app.exit_workbench())))

func compact_gap() -> int:return 4 if app.compact else 6

func _selection_style(button:Button,selected:bool):
	button.set_meta("ui_role","candidate_selection");button.set_meta("selected",selected)
	button.add_theme_stylebox_override("normal",W.button_box(W.SOFT if selected else W.PANEL,W.ACCENT if selected else W.DIVIDER))
	button.add_theme_stylebox_override("hover",W.button_box(W.SOFT,W.ACCENT));button.add_theme_stylebox_override("pressed",W.button_box(W.BASE,W.ACCENT))
	for key in ["font_color","font_hover_color","font_pressed_color"]:button.add_theme_color_override(key,W.TEXT)

func choose(id:String):
	if not offers().any(func(item):return item.id==id):return
	selected_offer=id;app.play_cue("select")
	if page!="summary" or mode=="policy":page="summary";rebuild();return
	for item in offers():
		var selected=item.id==selected_offer;var org=app.game.c.operators[item.operator_org]
		buttons[item.id].text=("✓ " if selected else "")+str(org.name);_selection_style(buttons[item.id],selected)
		if cards.has(item.id):
			var style=W.box(W.SOFT if selected else W.PANEL,W.ACCENT if selected else W.SOFT,8);style.content_margin_left=10;style.content_margin_right=10;style.content_margin_top=8;style.content_margin_bottom=8;cards[item.id].get_parent().add_theme_stylebox_override("panel",style)
	update_preview()

func update_preview():
	var g=app.game;preview_version=app.planner.version(g)
	app.map_view.plan_result={};app.map_view.plan_reference={}
	if mode=="policy":
		preview=app.planner.policy(g,selected_offer);reference=app.planner.snapshot(g)
		apply_button.disabled=g.s.block.policies[policy_slot]==selected_offer or g.s.currency<g.c.balance.policy_switch_price or not preview.get("ok",false)
		if info_label!=null:info_label.text="预计收支 %+.1f/月  ·  比不更换 %+.1f/月"%[float(preview.get("cashflow",0)),float(preview.get("delta",0))]
	else:
		if candidate_cache_version!=preview_version:rebuild();return
		var offer=_selected(offers())
		if offer.is_empty():return
		preview=candidate_previews.get(selected_offer,{});impact_model=ImpactModel.build(g,reference,preview)
		var cost=g.Draft.offer_cost(g,offer);apply_button.disabled=g.s.currency<cost.currency or not preview.get("ok",false)
		if impact_holder!=null:
			W.clear(impact_holder)
			if impact_model.get("ok",false):ImpactPanel.build(impact_holder,impact_model,func(kind):app.show_plan_layer(str(kind)))
			else:impact_holder.add_child(W.label(str(preview.get("error","当前无法生成预测。")),14,W.GOLD))
		app.map_view.plan_tile=str(g.s.draft.tile);app.map_view.planned_base=str(g.s.draft.base) if g.s.draft.kind=="development" else ""
		apply_button.text=("采用并建设 · " if g.s.draft.kind=="development" else "采用 · ")+"%.0f通货"%float(cost.currency)
	if not preview.get("ok",false):app.message(preview.get("error","无法预测，未提交。"))
	app.map_view.plan_reference=reference;app.map_view.plan_result=preview;app.map_view.invalidate();app.refresh_legend()

func reroll():
	var error=app.game.reroll()
	if error!="":app.message(error);return
	app.save_game();selected_offer="";rebuild();app.refresh_stats()

func commit():
	if preview_version!=app.planner.version(app.game):rebuild();app.message("城市已变化，预测已刷新。请再核对后采用。");return
	if mode=="policy":
		var error=app.game.switch_policy(selected_offer)
		if error!="":app.message(error);return
		app.exit_workbench();app.act("");app.play_cue("confirm")
	else:app.accept_offer(selected_offer)
