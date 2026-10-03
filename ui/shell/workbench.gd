extends PanelContainer
## L3 decision workbench. Candidate selection never commits gameplay.
const W=preload("res://ui/widgets.gd")
const C031=preload("res://ui/components031/components.gd")
const D031=preload("res://ui/components031/design.gd")
const P=preload("res://ui/presentation.gd")
const Art=preload("res://ui/map/art.gd")
var app
var selected_offer:String=""
var preview:Dictionary={}
var preview_version:String=""
var buttons:Dictionary={}
var cards:Dictionary={}
var apply_button:Button
var info_label:Label
var page:String="summary"
var mode:String="tender"
var policy_id:String=""
var policy_slot:String="Tax"
var title_label:Label
var footer:HBoxContainer
func setup(a):
	app=a;add_theme_stylebox_override("panel",W.box(W.PANEL,W.SOFT,10));show()
func _group(parent:Node,title:String,bg:Color=W.PANEL) -> VBoxContainer:
	var content=C031.group(parent,title,bg)
	var panel=content.get_parent()
	if panel is PanelContainer:
		var style=W.box(bg,W.SOFT,12);style.content_margin_left=14;style.content_margin_right=14;style.content_margin_top=12;style.content_margin_bottom=12
		panel.add_theme_stylebox_override("panel",style)
		content.add_theme_constant_override("separation",10)
	return content
func show_tender():
	mode="tender";page="summary";selected_offer="";rebuild()
func show_policy(slot:String="Tax"):
	mode="policy";policy_slot=slot;policy_id="";page="summary";rebuild()
func offers() -> Array:
	if mode=="tender":return app.game.s.draft.get("offers",[])
	var result=[]
	for id in app.game.Rules.ids(app.game.c.policies):
		if app.game.c.policies[id].slot==policy_slot:result.append({"id":id})
	return result
func rebuild():
	if app==null:return
	W.clear(self);buttons={};cards={};var g=app.game;var items=offers()
	if items.is_empty():hide();return
	if selected_offer=="" or not items.any(func(x):return x.id==selected_offer):selected_offer=items[0].id
	var compact=app.compact;var root=W.vbox(self,compact_gap())
	var top=W.hbox(root,8)
	var name="地区政策" if mode=="policy" else ("运营者比较 · "+g.c.projects.get(g.s.draft.get("base",""),{}).get("name","新建建筑"))
	title_label=W.label(name,16 if compact else 18);title_label.clip_text=true;title_label.custom_minimum_size.y=28*W.text_scale;title_label.text_overrun_behavior=TextServer.OVERRUN_TRIM_ELLIPSIS;top.add_child(title_label)
	if not compact:
		top.add_child(W.label("比较中 · 本地时间暂停",14,W.GOLD,false))
		
	var common={"currency":g.c.balance.policy_switch_price,"material":0.,"hours":0.}
	if mode=="tender":common=g.Draft.offer_cost(g,items[0])
	var shared="%.0f通货"%float(common.currency)
	if common.material>0:shared+=" · %.0f材料 · %.1f CP"%[common.material,common.get("cp",0)]
	var cost=W.label(("共同成本  " if not compact else "成本 ")+shared,14,W.GOLD);
	if compact:root.add_child(cost)
	else:top.add_child(cost)
	var scroll=ScrollContainer.new();scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;root.add_child(scroll)
	var body:Container
	if compact or page=="details":body=W.vbox(scroll,8)
	else:body=W.hbox(scroll,12)
	body.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	if page=="details":
		var item={}
		for x in items:
			if x.id==selected_offer:item=x
		if mode=="policy":
			var policy=g.c.policies[item.id];W.section(body,policy.name,policy.summary)
			body.add_child(W.button("切换政策类别",func():app.policy_categories()))
		else:
			var org=g.c.operators[item.operator_org]
			var detail=_group(body,"运营者 · "+org.name,W.SOFT);detail.add_child(W.label(org.get("summary",""),16,W.TEXT))
			detail.add_child(W.label("未来三阶段方案槽",15,W.MUTED))
			var track:Array=item.get("slot_track",org.get("slot_track",[]));detail.add_child(Art.slot_strip(g,track))
			var weights=[]
			for domain in org.get("domain_weights",{}):weights.append(str(domain)+" "+str(org.domain_weights[domain]))
			detail.add_child(W.label("偏好领域："+" · ".join(weights),14,W.MUTED))
		if not preview.is_empty():
			for text in P.plan_lines(g,preview):body.add_child(W.label(text,16,W.GREEN))
		W.section(body,"预测说明","比较同一时刻的两种下一周期：不采取行动，与假定该方案完成。覆盖范围不等于真实服务人数；预测不扣款、不推进、不洗牌。")
		if mode=="tender":
			var rc=g.Draft.reroll_cost(g);body.add_child(W.button("换一组 · %.0f通货 / %.1fDT"%[rc.currency,rc.dt],reroll))
	else:
		for i in items.size():
			var o=items[i];var selected=o.id==selected_offer
			var title="";var subtitle="";var highlights=[]
			if mode=="policy":
				var policy=g.c.policies[o.id];title=policy.name;subtitle="正在采用" if g.s.block.policies[policy_slot]==o.id else "可选择";highlights=[policy.summary]
			else:
				var org=g.c.operators[o.operator_org];title=org.name;subtitle="选择这家运营者";highlights=[org.get("summary","")]
			if mode=="tender":
				var card=_group(body,title,W.SOFT if selected else W.PANEL);card.size_flags_horizontal=Control.SIZE_EXPAND_FILL
				var button=W.button(("✓ 已选择" if selected else subtitle),func():choose(o.id));button.size_flags_horizontal=Control.SIZE_EXPAND_FILL
				_selection_style(button,selected)
				card.add_child(button);buttons[o.id]=button;cards[o.id]=card
				for h in highlights:
					if str(h)!="":
						var summary=W.label(str(h),15,W.MUTED);summary.max_lines_visible=2;card.add_child(summary)
				var candidate_preview=app.planner.compare(g,g.s.draft.base,g.s.draft.tile,o)
				if candidate_preview.get("ok",false):
					var service_gain=0.
					for value in candidate_preview.get("added_service",{}).values():service_gain+=float(value)
					card.add_child(W.label("住房 %+.0f · 服务 %+.0f人 · 供电 +%.1f"%[float(candidate_preview.get("housing",0)),service_gain,float(candidate_preview.get("energy_supply",0))],14,W.TEXT))
					var outputs=candidate_preview.get("outputs",{})
					card.add_child(W.label("粮食 %.1f · 材料 %.1f · 制成品 %.1f /月"%[float(outputs.get("food",0)),float(outputs.get("material",0)),float(outputs.get("goods",0))],14,W.TEXT))
				else:card.add_child(W.label(str(candidate_preview.get("error","当前无法生成预测。")),14,W.MUTED))
				card.add_child(W.label("成长槽位 · T1 → T2 → T3",13,W.MUTED))
				card.add_child(Art.slot_strip(g,o.get("slot_track",[])))
			else:
				var card=_group(body,"制度 · "+policy_slot)
				var button=W.button(title,func():choose(o.id));button.custom_minimum_size.x=0;card.add_child(button);buttons[o.id]=button
				_selection_style(button,selected)
				card.add_child(W.label(subtitle,14,W.MUTED))
				for h in highlights:card.add_child(W.label(h,16))
	if not compact and page=="summary":
		info_label=W.label("",14,W.GREEN);info_label.max_lines_visible=2;root.add_child(info_label)
	else:info_label=null
	footer=W.hbox(root,8)
	if compact:
		var back=W.icon_button("back","" if W.text_scale>1.2 else "返回",func():app.exit_workbench(),"返回地图");back.custom_minimum_size.x=48 if W.text_scale>1.2 else 86;footer.add_child(back)
	else:
		if mode=="tender":
			var rc=g.Draft.reroll_cost(g);footer.add_child(W.button("换一组 · %.0f"%rc.currency,reroll))
		else:footer.add_child(W.button("换政策类别",func():app.exit_workbench();app.Dialogs.block(app)))
	var detail=W.button(("ⓘ" if page=="summary" else "▤") if compact and W.text_scale>1.2 else ("详情" if page=="summary" else "对比"),func():page="details" if page=="summary" else "summary";rebuild(),false,"方案详情" if page=="summary" else "返回方案对比");detail.custom_minimum_size.x=48 if compact and W.text_scale>1.2 else 60;footer.add_child(detail)
	if compact and W.text_scale>1.2:
		detail.text="";detail.icon=Art.icon("ledger" if page=="summary" else "back");detail.expand_icon=true;detail.add_theme_constant_override("icon_max_width",22)
	var action_label="采用政策" if mode=="policy" else ("采用并建设" if g.s.draft.kind=="development" else "采用方案")
	apply_button=W.primary(W.button(action_label,commit))
	apply_button.name="TenderConfirmPrimary";apply_button.set_meta("ui_role","primary_action")
	apply_button.custom_minimum_size.x=84 if compact else 160;apply_button.size_flags_horizontal=Control.SIZE_EXPAND_FILL;footer.add_child(apply_button)
	if not compact:footer.add_child(W.button("返回地图",func():app.exit_workbench()))
	app.request_layout();update_preview()
func compact_gap() -> int:return 4 if app.compact else 6
func _selection_style(button:Button,selected:bool):
	# Selection is a reversible comparison state, not the purchase action.
	button.set_meta("ui_role","candidate_selection")
	button.add_theme_stylebox_override("normal",W.button_box(W.SOFT if selected else W.PANEL,W.ACCENT if selected else W.DIVIDER))
	button.add_theme_stylebox_override("hover",W.button_box(W.SOFT,W.ACCENT))
	button.add_theme_stylebox_override("pressed",W.button_box(W.BASE,W.ACCENT))
	for key in ["font_color","font_hover_color","font_pressed_color"]:button.add_theme_color_override(key,W.TEXT)
func choose(id:String):
	selected_offer=id;app.play_cue("select")
	if page!="summary":page="summary";rebuild();return
	if mode=="policy":rebuild();return
	for key in buttons:
		var selected=key==selected_offer
		buttons[key].text="✓ 已选择" if selected else "选择这家运营者"
		_selection_style(buttons[key],selected)
		if cards.has(key):cards[key].get_parent().add_theme_stylebox_override("panel",W.box(W.SOFT if selected else W.PANEL,W.GREEN if selected else W.SOFT,8))
	update_preview()
func update_preview():
	var g=app.game
	app.map_view.plan_result={};preview_version=app.planner.version(g)
	if mode=="policy":
		preview=app.planner.policy(g,selected_offer)
		apply_button.disabled=g.s.block.policies[policy_slot]==selected_offer or g.s.currency<g.c.balance.policy_switch_price or not preview.get("ok",false)
		if info_label!=null:info_label.text="预计收支 %+.1f/h  ·  比不更换 %+.1f/h"%[preview.get("cashflow",0),preview.get("delta",0)]
	else:
		var offer={}
		for x in g.s.draft.offers:
			if x.id==selected_offer:offer=x
		if offer.is_empty():return
		var d=g.s.draft
		preview=app.planner.compare(g,d.base,d.tile,offer)
		var cost=g.Draft.offer_cost(g,offer)
		apply_button.disabled=g.s.currency<cost.currency or not preview.get("ok",false)
		if info_label!=null:
			var lines=P.plan_lines(g,preview)
			var caution=P.plan_cautions(g,preview)
			info_label.text="  ·  ".join(lines.slice(0,2))+("  /  "+caution[0] if not caution.is_empty() else "")
		app.map_view.plan_tile=str(d.tile);app.map_view.planned_base=str(d.base) if d.kind=="development" else ""
	if not preview.get("ok",false):app.message(preview.get("error","无法预测，未提交。"))
	app.map_view.plan_result=preview;app.map_view.invalidate();app.refresh_legend()
	if mode=="tender":
		for x in offers():
			if x.id==selected_offer:
				var price=g.Draft.offer_cost(g,x)
				apply_button.text="采用方案" if app.compact and W.text_scale>1.2 else (("采用 " if app.compact else ("采用并建设 · " if g.s.draft.kind=="development" else "采用 · "))+"%.0f通货"%float(price.currency))
				if info_label!=null:
					break
func reroll():
	var e=app.game.reroll()
	if e!="":app.message(e);return
	app.save_game();selected_offer="";rebuild();app.refresh_stats()
func commit():
	if preview_version!=app.planner.version(app.game):update_preview();app.message("城市已变化，预测已刷新。请再核对后采用。");return
	if mode=="policy":
		var e=app.game.switch_policy(selected_offer)
		if e!="":app.message(e);return
		app.exit_workbench();app.act("");app.play_cue("confirm")
	else:app.accept_offer(selected_offer)
