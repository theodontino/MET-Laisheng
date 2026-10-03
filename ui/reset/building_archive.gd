extends VBoxContainer
## Deliberately opened L3 archive. All former inspector detail remains reachable here.
const W=preload("res://ui/widgets.gd")
const D=preload("res://ui/components031/design.gd")
const C=preload("res://ui/components031/components.gd")
const Art=preload("res://ui/map/art.gd")
const Model=preload("res://ui/view_models/inspector_model.gd")
const P=preload("res://ui/presentation.gd")
const V=preload("res://ui/reset/desk_visuals.gd")
var app
var model={}
var selected_id:String=""
var tab:String="overview"
var signature:String=""
var scroll_positions:Dictionary={}
var disclosure_state:Dictionary={}
var header:PanelContainer
var title_label:Label
var subtitle:Label
var reading:Label
var portrait:TextureRect
var domain_label:Label
var tabs={}
var scroll:ScrollContainer
var body:VBoxContainer
var footer:HBoxContainer
var primary:Button
var more:Button
var nodes={}
var compact_layout:bool=false
var current_font:float=1.
var archive_revision:int=-1
static func open(a,id:String,section:String="overview"):
	if not a.game.s.projects.has(id):return
	var source="下一周期预计" if a.shot.get("forecast",false) else "最近周期实际"
	a.open_modal(a.game.project_name(id)+" · 建筑档案","运营、方案和完整来源 · "+source)
	var archive=load("res://ui/reset/building_archive.gd").new()
	archive.name="BuildingArchiveView"
	archive.custom_minimum_size=Vector2(600,610)
	a.modal_body.add_child(archive)
	archive.setup(a)
	archive.tab=section
	archive.update_view(a.game,a.shot,id)
	return archive
func _process(_delta:float):
	if app==null or selected_id=="" or not app.game.s.projects.has(selected_id):return
	if archive_revision!=int(app.game.revision):
		archive_revision=int(app.game.revision)
		update_view(app.game,app.shot,selected_id)

func setup(a):
	app=a;add_theme_constant_override("separation",10);size_flags_vertical=Control.SIZE_EXPAND_FILL;size_flags_horizontal=Control.SIZE_EXPAND_FILL
	header=W.panel(self,W.BASE);header.set_meta("desk_visual","building");header.add_theme_stylebox_override("panel",W.box(W.BASE,W.SOFT,12));var h=W.hbox(header,18)
	portrait=Art.picture("homestead",Vector2(152,110));h.add_child(portrait)
	var identity=W.vbox(h,6);identity.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	domain_label=W.label("",12,W.GREEN);identity.add_child(domain_label)
	title_label=W.label("",25);identity.add_child(title_label);subtitle=W.label("",14,W.MUTED);identity.add_child(subtitle)
	reading=W.label("",12,W.MUTED);identity.add_child(reading)
	var close=W.flat(W.button("×",func():app.close_modal()));close.name="ArchiveClose";close.clip_text=false;close.custom_minimum_size=Vector2(44,40);close.size_flags_vertical=Control.SIZE_SHRINK_BEGIN;h.add_child(close)
	var tr=W.hbox(self,8)
	for item in [["overview","production","运行与管理"],["slots","bonus","槽位与方案"],["details","layers","完整来源"]]:
		var b=W.icon_button(item[1],item[2],func():switch_tab(item[0]));b.name="ArchiveTab_"+str(item[0]);b.custom_minimum_size=Vector2(0,42);b.size_flags_horizontal=Control.SIZE_EXPAND_FILL;tr.add_child(b);tabs[item[0]]=b
	scroll=ScrollContainer.new();scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;scroll.size_flags_horizontal=Control.SIZE_EXPAND_FILL;scroll.custom_minimum_size.y=280;add_child(scroll)
	body=W.vbox(scroll,14);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	footer=W.hbox(self,10)
	primary=W.primary(W.button("查看运营方案",primary_action));primary.custom_minimum_size=Vector2(0,44);primary.size_flags_horizontal=Control.SIZE_EXPAND_FILL;footer.add_child(primary)
	more=W.icon_button("settings","更多",manage);more.custom_minimum_size=Vector2(108,44);footer.add_child(more)
func adapt():pass
func update_view(g,shot:Dictionary,id:String):
	var data=Model.build(g,shot,id)
	if selected_id!=id:scroll_positions={};disclosure_state={};selected_id=id;signature=""
	model=data
	var domain=D.domain(data.domain)
	title_label.text=data.name;subtitle.text=data.operator+" · Tier "+str(data.current.get("tier",1))+" · "+str(data.short_status)
	domain_label.text=str(domain.label);domain_label.add_theme_color_override("font_color",Color(domain.ink));portrait.texture=Art.thumbnail(str(data.base))
	reading.text=data.reading+" · 物资按来生月，电力为当期能力"
	reading.tooltip_text="来源："+str(data.snapshot_id)+" · Tick "+str(data.tick)
	var key=data.signature+":"+tab+":"+str(g.revision)+":"+str(data.snapshot_id)+":"+str(W.text_scale)
	if signature!=key:
		var old=float(scroll.scroll_vertical) if signature!="" else float(scroll_positions.get(tab,0))
		rebuild();signature=key;scroll.set_deferred("scroll_vertical",int(old))
	for keytab in tabs:
		var b=tabs[keytab];var style=W.button_box(W.SOFT if keytab==tab else W.PANEL,W.GREEN if keytab==tab else W.SOFT)
		style.border_width_bottom=3 if keytab==tab else 0;b.add_theme_stylebox_override("normal",style)
	primary.text=primary_label()
func switch_tab(value:String):
	if value==tab:return
	scroll_positions[tab]=scroll.scroll_vertical;tab=value;signature="";app.play_cue("select")
	if selected_id!="":update_view(app.game,app.shot,selected_id)
func _grid(parent:Node,columns:int=2) -> GridContainer:return V.grid(parent,columns)
func _group(parent:Node,title:String="",icon:String="info",ink:Color=W.GREEN) -> VBoxContainer:return V.card(parent,title,"",icon,ink)
func _metric(parent:Node,icon:String,label:String,value:String,note:String="",ink:Color=W.TEXT):
	var row=W.hbox(parent,10);row.set_meta("desk_visual","compact_metric");V.icon(row,icon,24,ink)
	var name=W.label(label,14,W.MUTED);name.size_flags_horizontal=Control.SIZE_EXPAND_FILL;row.add_child(name)
	row.add_child(W.label(value,21,ink,false))
	if note!="":parent.add_child(W.label(note,12,W.MUTED))
func _bar(parent:Node,label:String,amount:float,total:float,value:String,ink:Color=W.GREEN,icon:String=""):
	var line=W.hbox(parent,8);line.set_meta("desk_visual","meter")
	if icon!="":V.icon(line,icon,20,ink)
	var name=W.label(label,14);name.size_flags_horizontal=Control.SIZE_EXPAND_FILL;line.add_child(name);line.add_child(W.label(value,15,ink,false))
	var bar=W.bar(amount/maxf(.000001,total));bar.custom_minimum_size.y=7;bar.add_theme_stylebox_override("fill",W.box(ink,Color.TRANSPARENT,3));bar.tooltip_text=label+" · "+value;parent.add_child(bar)
func _fold(parent:Node,title:String,key:String="") -> VBoxContainer:
	var stable=key+":"+title.get_slice(" = ",0).get_slice(" · ",0)
	var content=V.fold(parent,title,"",bool(disclosure_state.get(stable,false)))
	var toggle:Button=content.get_parent().get_child(0)
	toggle.toggled.connect(func(value:bool):disclosure_state[stable]=value)
	return content
func _raw(parent:Node,value):
	var text=W.label(JSON.stringify(value,"  "),12,W.MUTED)
	text.set_meta("source_record",true);parent.add_child(text)
func _unit(resource:String) -> String:return "电力" if resource=="energy" else "/月"
func _value(value:float,resource:String) -> String:return "%.2f%s"%[value,_unit(resource)]
func rebuild():
	W.clear(body);nodes={}
	if tab=="overview":overview()
	elif tab=="slots":slots()
	else:details()
func _production_chain(parent:Node):
	var m=model
	var shell=_group(parent,"真实投入 → 建筑运行 → 产出","production")
	var chain=W.hbox(shell,14);chain.set_meta("desk_visual","flow")
	var incoming=W.vbox(chain,8);incoming.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	for item in m.inputs:_metric(incoming,str(item.id),P.RES.get(str(item.id),str(item.id)),_value(float(item.value),str(item.id)) if m.known else "—")
	if m.inputs.is_empty():incoming.add_child(W.label("无物资投入" if m.known else "投入待结算",14,W.MUTED))
	chain.add_child(W.label("→",26,W.MUTED,false))
	var center=W.vbox(chain,6);center.custom_minimum_size.x=156
	var picture=Art.picture(str(m.base),Vector2(148,86));center.add_child(picture)
	var state=W.label(str(m.short_status),14,m.status_color);state.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;center.add_child(state)
	chain.add_child(W.label("→",26,W.MUTED,false))
	var outgoing=W.vbox(chain,8);outgoing.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	for item in m.outputs:_metric(outgoing,str(item.id),P.RES.get(str(item.id),str(item.id)),_value(float(item.value),str(item.id)) if m.known else "—","",W.GREEN)
	if m.outputs.is_empty():outgoing.add_child(W.label("以居住或服务为主" if m.known else "产出待结算",14,W.MUTED))
	shell.add_child(W.label(str(m.reading)+" · 电力不作为累计库存；完整住户 / 配方来源见来源页。",12,W.MUTED))
func overview():
	var m=model;var p:Dictionary=m.current
	if p.status=="construction" or not p.get("upgrade_job",{}).is_empty():
		var job=p.get("upgrade_job",{}) if not p.get("upgrade_job",{}).is_empty() else p.job
		var work=_group(body,"共享施工队列","construction",W.GOLD)
		_bar(work,"已完成施工",float(job.total)-float(job.work),float(job.total),"%.1f / %.1f CP"%[float(job.total)-float(job.work),float(job.total)],W.GOLD)
		_metric(work,"material","尚需材料","%.1f"%float(job.material),"当前工程余量；共享施工能力、电力与材料决定实际进展。")
	if not m.outputs.is_empty() or not m.inputs.is_empty():_production_chain(body)
	var grid=_grid(body)
	if not m.house.is_empty():
		var house=_group(grid,"居住与居民","home")
		_bar(house,"已入住",m.house.count,m.house.capacity,"%.1f / %.0f 人"%[m.house.count,m.house.capacity],W.INFO)
		_metric(house,"housing","可用空房","%.1f 人"%float(m.house.vacant))
		if float(m.house.count)>0:
			for key in ["health","happiness","education"]:_bar(house,D.quantity(key).label,float(m.house[key]),100,"%.0f / 100"%float(m.house[key]),W.GREEN,str(D.quantity(key).icon))
			var needs=_fold(house,"生活需求满足率","living")
			var kinds=[]
			for group in m.groups:
				for kind in group.get("living",{}):
					if not kind in kinds:kinds.append(kind)
			for kind in kinds:
				var fulfilled=0.;var complete=true
				for group in m.groups:
					if not group.get("living",{}).has(kind):complete=false
					fulfilled+=float(group.count)*float(group.get("living",{}).get(kind,0))
				var label=P.RES.get(str(kind),P.KIND.get(str(kind),str(kind)))
				if complete:_bar(needs,label,fulfilled,float(m.house.count),"%.0f%%"%(100*fulfilled/float(m.house.count)),W.INFO,str(kind))
				else:_metric(needs,str(kind),label,"—","部分住户记录缺失。",W.MUTED)
			if kinds.is_empty():needs.add_child(W.label("生活需求等待结算。",13,W.MUTED))
		else:house.add_child(W.label("暂无住户；健康、幸福与教育等待居民入住。",13,W.MUTED))
	if not m.services.is_empty():
		var services=_group(grid,"服务名额","medical",W.INFO)
		for item in m.services:
			var label=P.KIND[item.id]+(" · 共享池" if item.shared else "")
			if m.known:_bar(services,label,item.value,item.capacity,"%.1f / %.1f 人"%[item.value,item.capacity],W.INFO,str(item.id))
			else:_metric(services,str(item.id),label,"—","服务人数与有效容量待结算。",W.MUTED)
		services.add_child(W.label("读真实服务池；共享池包括相连成员，完整成员与入口在来源页。",12,W.MUTED))
	var labor=_group(grid,"用工与运行","employment",m.status_color)
	if m.known:_bar(labor,"实际运行",m.utilization,1,"%.0f%%"%(100*m.utilization),m.status_color)
	else:_metric(labor,"production","实际运行","—","等待可信的运营结果。",W.MUTED)
	if m.jobs>0:
		if m.known:_bar(labor,"到岗人数",m.assigned,m.jobs,"%.1f / %.1f 人"%[m.assigned,m.jobs],W.INFO)
		else:_metric(labor,"employment","到岗 / 岗位","— / %.1f 人"%float(m.jobs),"到岗人数待结算。",W.MUTED)
		_metric(labor,"employment","有效劳动","%.2f"%m.effective if m.known else "—","建筑效率 %.0f%%"%(100*float(m.stats.efficiency)))
		var priorities=W.hbox(labor,6)
		for pair in [[-1,"让出人手"],[0,"普通"],[1,"优先供人"]]:
			var b=W.button(pair[1],func():app.act(app.game.set_priority(selected_id,pair[0])));b.custom_minimum_size.x=0;b.size_flags_horizontal=Control.SIZE_EXPAND_FILL;priorities.add_child(b)
			if int(pair[0])==int(m.priority):b.add_theme_stylebox_override("normal",W.button_box(W.RAISED,W.GREEN))
	labor.add_child(W.label(str(m.status.detail),12,W.MUTED))
	var finance=_group(grid,"投入与维护","currency",W.GOLD)
	var fee_total=0.
	for fee in m.fees:fee_total+=float(fee.value)
	_metric(finance,"currency","本楼维护","%.2f/月"%fee_total if m.known else "—","包含本楼与住户的实际费用。" if m.known else "费用待结算。",W.GOLD)
	for fee in m.fees:_bar(finance,str(fee.label),float(fee.value),fee_total,"%.2f/月"%float(fee.value),W.GOLD)
	finance.add_child(W.flat(W.button("展开投入来源",func():switch_tab("details"))))
	if not m.inventory.is_empty():
		var inventory=_group(grid,"当前库存 / 容量","storage",W.INFO)
		for item in m.inventory:_bar(inventory,P.RES.get(item.id,item.id),item.stock,item.capacity,"%.1f / %.1f"%[item.stock,item.capacity],W.INFO,str(item.id))
	if str(p.def)=="freight":
		var market=_group(grid,"对外市场接口","trade",W.INFO);var g=app.game
		_metric(market,"trade","全城外贸运力","%.1f"%float(g.Market._total_capacity(g)),"每现实在线小时；订单不会随本地加速同比放大。")
		var receipts:Array=g.s.block.get("trade_ledger",[])
		if not receipts.is_empty():
			var receipt:Dictionary=receipts.back()
			_bar(market,"最近结算运力",float(receipt.get("used_capacity",0)),float(receipt.get("capacity",0)),"%.2f / %.2f"%[float(receipt.get("used_capacity",0)),float(receipt.get("capacity",0))],W.INFO)
			var receipt_log=_fold(market,"原始外贸结算记录","trade")
			_raw(receipt_log,receipt)
		var orders=[]
		for order in g.s.block.get("trade_orders",[]):
			if str(order.get("status","open")) in ["open","partial"]:orders.append(order)
		_metric(market,"goods","进行中订单",str(orders.size()))
		market.add_child(W.button("打开市场",func():app.close_modal();app.desk.open("city040","market")))
	var rack=_group(body,"方案骨架 · %d / %d 已装"%[m.cohesion.occupied,m.cohesion.total],"bonus")
	var slots_grid=_grid(rack,3)
	for item in m.slots:C.socket(slots_grid,app.game,selected_id,str(item.stable_id),func():app.Dialogs.slot_detail(app,selected_id,str(item.stable_id)),true)
func slots():
	var m=model;var g=app.game
	var operator=_group(body,"运营骨架 · "+str(m.operator),"org_"+str(m.operator_org))
	operator.add_child(Art.slot_strip(g,m.current.get("slot_track",[])))
	var state=W.hbox(operator,12)
	C.badge(state,"已装 %d / %d"%[m.cohesion.occupied,m.cohesion.total],W.GREEN)
	C.badge(state,"自营 %d / %d"%[m.cohesion.owned,m.cohesion.total],W.INFO)
	state.add_child(W.label("满自营 +3% · 允许混搭 · 槽位随建筑成长解锁",13,W.MUTED))
	var grid=_grid(body,3)
	for item in m.slots:
		var slot=C.socket(grid,g,selected_id,str(item.stable_id),func():app.Dialogs.slot_detail(app,selected_id,str(item.stable_id)))
		var inside=slot.root.get_child(0)
		if item.state=="conditional":C.badge(inside,str(item.reason),W.GOLD)
		if item.instance_id!="":
			var definition=D.card(g,str(item.bonus_id))
			for effect in definition.get("effect_rows",[]).slice(0,2):C.effect(inside,effect,definition.visual_domain)
			inside.add_child(W.label("签约 · "+g.c.operators.get(item.contract_org,{}).get("name","无记录"),12,W.MUTED))
	body.add_child(W.flat(W.button("管理建筑",manage)))
func _contributions(parent:Node,rows:Array,title:String):
	var group=_group(parent,title,"production")
	if not model.known:group.add_child(W.label("没有可信的本周期活动记录，等待结算。",14,W.MUTED));return
	if rows.is_empty():group.add_child(W.label("本周期没有这类活动记录。",14,W.MUTED));return
	for item in rows:
		var resource=W.vbox(group,9)
		var heading=W.hbox(resource,8);V.icon(heading,str(item.id),24);heading.add_child(W.label(P.RES.get(str(item.id),str(item.id))+" · "+_value(float(item.value),str(item.id)),16))
		if title.begins_with("产出") and str(item.id)!="energy":
			var disposition=W.hbox(resource,12)
			V.badge(disposition,"留下 %.3f/月"%float(item.get("kept",0)),W.GREEN)
			V.badge(disposition,"损耗 %.3f/月"%float(item.get("lost",0)),W.GOLD)
		var sorted:Array=item.children.duplicate();sorted.sort_custom(func(a,b):return absf(float(a.rate))>absf(float(b.rate)))
		var total=0.
		for entry in sorted:total+=absf(float(entry.rate))
		for entry in sorted.slice(0,4):_bar(resource,str(entry.get("source","来源")),absf(float(entry.rate)),total,_value(float(entry.rate),str(item.id)),W.GREEN if title.begins_with("产出") else W.GOLD)
		var exact=_fold(resource,"全部 %d 条明细"%sorted.size(),str(item.id))
		for entry in sorted:
			var line=str(entry.get("source","来源"))+" · "+_value(float(entry.rate),str(item.id))
			if title.begins_with("产出") and str(item.id)!="energy":line+=" · 留下 %.3f，损耗 %.3f"%[float(entry.get("kept",0)),float(entry.get("lost",0))]
			elif str(entry.get("consumer","")).begins_with("pop:"):line+=" · 住户"
			exact.add_child(W.label(line,13,W.MUTED))
			_raw(exact,entry)
func details():
	var m=model
	var columns=_grid(body)
	_contributions(columns,m.inputs,"投入来源 · "+str(m.reading))
	_contributions(columns,m.outputs,"产出与去向 · "+str(m.reading))
	var storage=_fold(body,"当前库存、仓容与费用","storage")
	for item in m.inventory:_bar(storage,P.RES.get(str(item.id),str(item.id)),float(item.stock),float(item.capacity),"%.2f / %.2f"%[item.stock,item.capacity],W.INFO,str(item.id));storage.add_child(W.label("可接收外部溢出" if item.public else "只计本地可用仓容",12,W.MUTED))
	for fee in m.fees:_metric(storage,"currency",str(fee.label),"%.3f/月"%float(fee.value))
	var accounts=_fold(storage,"全部财政来源记录 · %d 条"%m.fiscal.size(),"fiscal")
	for entry in m.fiscal:_raw(accounts,entry)
	var links=_fold(body,"居民、就业与服务池成员","population")
	for link in m.employment:_metric(links,"employment","来自 "+app.game.project_name(str(app.shot.groups.get(link.group,{}).get("host",""))),"%.2f 人"%float(link.people),"有效劳动 %.3f"%float(link.effective))
	for group in m.groups:
		for bid in group.employment:_metric(links,"employment","本住区 → "+app.game.project_name(str(bid)),"%.2f 人"%float(group.employment[bid]))
	for service in m.services:
		for pool_id in service.pool_members:
			var names=[]
			for member in service.pool_members[pool_id]:names.append(app.game.project_name(str(member)))
			links.add_child(W.label(P.KIND.get(str(service.id),str(service.id))+" · "+"、".join(names),14,W.INFO))
	for link in m.service_links:links.add_child(W.label(P.KIND.get(link.kind,link.kind)+" → "+app.game.project_name(link.home)+" · %.2f 人"%float(link.people)+" · 入口 "+app.game.project_name(str(link.source)),13,W.MUTED))
	var relationship_records=_fold(links,"住户、就业与服务的原始记录","relationships")
	for group in m.groups:_raw(relationship_records,group)
	for link in m.employment:_raw(relationship_records,link)
	for link in m.service_links:_raw(relationship_records,link)
	var fields=_group(body,"所在位置的场来源","layers",W.INFO)
	if m.fields.is_empty():fields.add_child(W.label("本周期无空间场来源。",13,W.MUTED))
	for kind in app.game.Rules.ids(m.fields):
		var list:Array=m.fields[kind].duplicate();list.sort_custom(func(a,b):return absf(float(a.value))>absf(float(b.value)))
		var maximum=0.
		for field in list:maximum=maxf(maximum,absf(float(field.value)))
		var field_group=_group(fields,app.MapData.NAMES.get(str(kind),str(kind))+" · %d 个来源"%list.size(),str(kind),W.INFO)
		field_group.add_child(W.label("条长仅比较本类场的贡献绝对值；负值以数字符号区分。",12,W.MUTED))
		for field in list.slice(0,3):_bar(field_group,app.game.project_name(str(field.source_id)),absf(float(field.value)),maximum,"%+.3f"%float(field.value),W.INFO)
		var field_records=_fold(field_group,"完整 Field 样本与来源 · %d 条"%list.size(),str(kind))
		for field in list:
			var via=str(field.get("via",""))
			field_records.add_child(W.label(app.game.project_name(str(field.source_id))+" · %+.3f"%float(field.value)+(" · 经 "+app.game.project_name(via) if via!="" else ""),14,W.INFO))
			_raw(field_records,field)
	var trace=_group(body,"数值解释","settings")
	trace.add_child(W.label("每项保留最终值与完整规则链；加法与倍率分别说明，不把不同单位相加比较。",12,W.MUTED))
	for field in app.game.Rules.ids(m.trace):
		var entry=m.trace[field];var exact=_fold(trace,str(field)+" = %.3f"%float(entry.get("value",0)),"layers")
		V.flow(exact,[{"id":"layers","label":"基础值","value":"%.3f"%float(entry.get("base",0))},{"id":"settings","label":"参与规则","value":"%d 条"%entry.get("rules",[]).size()},{"id":"check","label":"最终值","value":"%.3f"%float(entry.get("value",0))}],"最终值来自当前快照；这里不另算演示结果。")
		for rule in entry.get("rules",[]):exact.add_child(W.label(str(rule.source)+" · "+str(rule.mode)+" "+str(rule.value),13,W.MUTED))
		_raw(exact,entry)
	var history=_fold(body,"原方案与当前合同","bonus")
	for item in m.slots:
		if item.instance_id=="":continue
		var source=_group(history,app.game.c.bonuses[item.bonus_id].name,"bonus")
		source.add_child(W.label("原提供方："+app.game.c.operators.get(item.provider_org,{}).get("name",item.provider_org)+" → 当前签约："+app.game.c.operators.get(item.contract_org,{}).get("name",item.contract_org),14,W.MUTED))
		_raw(source,app.game.s.bonus_instances[item.instance_id])
	var definitions=_fold(body,"完整 Rule 定义 · 建筑、运营商、已装方案","definitions")
	var base=app.game.c.projects.get(str(m.base),{})
	definitions.add_child(W.label("建筑 · "+str(m.name),16,W.INFO))
	for rule in base.get("rules",[]):_raw(definitions,rule)
	definitions.add_child(W.label("运营商 · "+str(m.operator),16,W.INFO))
	for rule in app.game.c.operators.get(str(m.operator_org),{}).get("rules",[]):_raw(definitions,rule)
	for item in m.slots:
		if item.instance_id=="":continue
		definitions.add_child(W.label(app.game.c.bonuses[item.bonus_id].name+" · "+str(item.label),16,W.INFO))
		definitions.add_child(W.label("原始定义",13,W.MUTED))
		for rule in app.game.c.bonuses[item.bonus_id].rules:_raw(definitions,rule)
		definitions.add_child(W.label("当前研究与同宿主副本后的完整规则",13,W.INFO))
		var count=app.game.Bonus.active_count(app.game,str(item.bonus_id),"building",selected_id)
		for rule in app.game.Rules.bonus_rules(app.game,str(item.bonus_id),count):_raw(definitions,rule)
func refresh_values():pass
func manage():
	# open_modal replaces (and frees) this archive. New controls must capture
	# the persistent app, never this soon-to-be-freed node.
	var a=app;var g=a.game;var id=selected_id
	if not g.s.projects.has(id):return
	a.open_modal(g.project_name(id)+" · 管理","暂停是可逆操作；拆除需要单独确认。")
	V.building(a.modal_body,str(g.s.projects[id].def),g.project_name(id),g.c.operators[g.s.projects[id].operator_org].name+" · Tier "+str(g.s.projects[id].get("tier",1)))
	var columns=V.grid(a.modal_body,2)
	var v=V.card(columns,"运行与成长","暂停仍支付固定维护。","production",W.GREEN)
	v.add_child(W.button("恢复运行" if g.s.projects[id].paused else "暂停运行",func():a.act(g.set_paused(id,not g.s.projects[id].paused));a.close_modal()))
	v.add_child(W.button("查看建筑成长",func():a.close_modal();a.desk.open("city040","upgrade:"+id),g.s.projects[id].get("status","")!="running"))
	v.add_child(W.button("查看部署许可",func():a.close_modal();a.desk.open("city040","permits")))
	var organisation=V.card(columns,"城市关系与运营","查看本楼连接的城市管理入口。","org_"+str(g.s.projects[id].operator_org),W.INFO)
	organisation.add_child(W.button("市场与订单",func():a.close_modal();a.desk.open("city040","market")))
	organisation.add_child(W.button("政策与制度",func():a.close_modal();a.desk.open("policies")))
	organisation.add_child(W.button("运营商档案",func():
		a.close_modal()
		if not g.s.projects.has(id):return
		a.open_modal(g.c.operators[g.s.projects[id].operator_org].name,"本建筑运营组织 · 原始定义与来源")
		var org=g.c.operators[g.s.projects[id].operator_org]
		var identity=V.card(a.modal_body,str(org.name),str(org.get("summary","")),"org_"+str(g.s.projects[id].operator_org),W.INFO)
		identity.add_child(Art.slot_strip(g,org.get("slot_track",[])))
		identity.add_child(W.label(g.c.tags_text(org.get("traits",[])),14,W.MUTED))
		var rules=V.fold(a.modal_body,"运营商完整 Rule · %d 条"%org.get("rules",[]).size())
		for rule in org.get("rules",[]):rules.add_child(W.label(JSON.stringify(rule,"  "),13,W.MUTED))
		a.modal_body.add_child(W.button("返回建筑档案",func():load("res://ui/reset/building_archive.gd").open(a,id)))))
	if g.s.projects[id].status=="construction" or not g.s.projects[id].get("upgrade_job",{}).is_empty():
		var queue=V.card(a.modal_body,"共享施工顺序","更高优先级先获得城市共享 CP；不会凭空增加能力。","construction",W.GOLD)
		var priorities=W.hbox(queue,10)
		for item in [[-1,"稍后施工"],[0,"普通施工"],[1,"优先施工"]]:
			var level=int(item[0])
			var choice=W.button(str(item[1]),func():a.act(g.Construction.set_priority(g,id,level));a.close_modal());choice.size_flags_horizontal=Control.SIZE_EXPAND_FILL;priorities.add_child(choice)
	var danger=V.card(a.modal_body,"拆除与重新安置","打开后查看费用、住户与方案影响。","warning",W.GOLD);var b=W.button("拆除建筑…",func():a.Dialogs.demolition(a,id));b.add_theme_stylebox_override("normal",W.button_box(W.PANEL,W.GOLD));b.add_theme_color_override("font_color",W.GOLD);danger.add_child(b)
	var back=W.flat(W.button("返回建筑档案",func():load("res://ui/reset/building_archive.gd").open(a,id)));back.name="ArchiveManagementBack";a.modal_body.add_child(back)

func primary_label() -> String:
	if tab=="slots":return "回到运行概况"
	if selected_id=="" or not app.game.s.projects.has(selected_id):return "查看运营方案"
	var p=app.game.s.projects[selected_id]
	if str(p.get("def",""))=="freight":return "打开贸易"
	if not app.game.Development.choices(app.game,selected_id).is_empty() and p.get("status","")=="running":return "预览升级方向"
	return "查看可装副本"
func primary_action():
	if tab=="slots":switch_tab("overview");return
	var a=app;var g=a.game;var id=selected_id
	if not g.s.projects.has(id):return
	var destination="permits"
	if str(g.s.projects[id].get("def",""))=="freight":destination="market"
	elif not g.Development.choices(g,id).is_empty() and g.s.projects[id].get("status","")=="running":destination="upgrade:"+id
	a.close_modal();a.desk.open("city040",destination)
