extends VBoxContainer
## Fixed identity/tabs/actions; a single scrolling body, updated without replacing controls each tick.
const W=preload("res://ui/widgets.gd")
const D=preload("res://ui/components031/design.gd")
const C=preload("res://ui/components031/components.gd")
const Art=preload("res://ui/map/art.gd")
const Model=preload("res://ui/view_models/inspector_model.gd")
const P=preload("res://ui/presentation.gd")
var app
var model={}
var selected_id:String=""
var tab:String="overview"
var signature:String=""
var scroll_positions:Dictionary={}
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
func setup(a):
	app=a;add_theme_constant_override("separation",0);size_flags_vertical=Control.SIZE_EXPAND_FILL;size_flags_horizontal=Control.SIZE_EXPAND_FILL
	header=W.panel(self,W.PANEL);header.add_theme_stylebox_override("panel",W.box(W.PANEL,W.SOFT,10));var hv=W.vbox(header,4);var h=W.hbox(hv,8)
	portrait=Art.picture("homestead_t1",Vector2(64,64));h.add_child(portrait)
	var text=W.vbox(h,3);text.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	domain_label=W.label("",13,W.GREEN);text.add_child(domain_label);title_label=W.label("",21);text.add_child(title_label);subtitle=W.label("",14,W.MUTED);text.add_child(subtitle)
	var close=W.flat(W.button("×",func():app.set_mode("inspect")));close.custom_minimum_size=Vector2(40,40);h.add_child(close)
	reading=W.label("",14,W.MUTED);hv.add_child(reading)
	var tabs_panel=W.panel(self,W.PANEL);var ts=W.box(W.PANEL,W.SOFT,0);ts.content_margin_top=0;ts.content_margin_bottom=0;ts.content_margin_left=8;ts.content_margin_right=8;tabs_panel.add_theme_stylebox_override("panel",ts)
	var tr=W.hbox(tabs_panel,6)
	for item in [["overview","概况"],["slots","方案"],["details","明细"]]:
		var b=W.flat(W.button(item[1],func():switch_tab(item[0])));b.custom_minimum_size=Vector2(0,38);b.size_flags_horizontal=Control.SIZE_EXPAND_FILL;tr.add_child(b);tabs[item[0]]=b
	scroll=ScrollContainer.new();scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;scroll.size_flags_horizontal=Control.SIZE_EXPAND_FILL;scroll.custom_minimum_size.y=24;add_child(scroll)
	var margin=MarginContainer.new();margin.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	for side in ["left","right"]:margin.add_theme_constant_override("margin_"+side,12)
	for side in ["top","bottom"]:margin.add_theme_constant_override("margin_"+side,10)
	scroll.add_child(margin);body=W.vbox(margin,10);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	var fp=W.panel(self,W.PANEL);var fs=W.box(W.PANEL,W.SOFT,0);fs.content_margin_top=4;fs.content_margin_bottom=4;fs.content_margin_left=8;fs.content_margin_right=8;fp.add_theme_stylebox_override("panel",fs);footer=W.hbox(fp,6)
	primary=W.primary(W.button("查看运营方案",func():switch_tab("slots") if tab!="slots" else switch_tab("overview")));primary.custom_minimum_size=Vector2(0,40);primary.size_flags_horizontal=Control.SIZE_EXPAND_FILL;footer.add_child(primary)
	more=W.button("更多",manage);more.custom_minimum_size=Vector2(64,40);footer.add_child(more)
func adapt():
	var compact=app.compact;var altered=compact!=compact_layout or W.text_scale!=current_font
	compact_layout=compact;current_font=W.text_scale
	portrait.visible=not compact;subtitle.visible=not compact;domain_label.visible=not compact
	reading.visible=not compact or app.safe_area.size.y>=420
	title_label.add_theme_font_size_override("font_size",int((18 if compact else 21)*W.text_scale))
	more.text="⋯" if compact and W.text_scale>1.2 else "更多";more.tooltip_text="更多管理操作";more.custom_minimum_size.x=48 if compact else 72
	primary.text=("看方案" if compact else "查看运营方案") if tab!="slots" else ("看概况" if compact else "回到运行概况")
	if altered:signature=""
func update_view(g,shot:Dictionary,id:String):
	adapt()
	var data=Model.build(g,shot,id)
	if selected_id!=id:
		scroll_positions={};selected_id=id;signature=""
		if tab=="details":tab="overview"
	model=data
	var d=D.domain(data.domain)
	header.add_theme_stylebox_override("panel",W.box(W.PANEL,W.SOFT,10))
	title_label.text=data.name;subtitle.text=data.operator+" · 运营者";domain_label.text=str(d.label);domain_label.add_theme_color_override("font_color",W.GREEN);portrait.texture=Art.thumbnail(str(data.base))
	reading.text=data.reading+" · 第%d周期"%data.tick
	var key=data.signature+":"+tab+":"+str(compact_layout)+":"+str(W.text_scale)
	if signature!=key:
		var old=float(scroll.scroll_vertical) if signature!="" else float(scroll_positions.get(tab,0))
		rebuild();signature=key;scroll.set_deferred("scroll_vertical",int(old))
	refresh_values()
	for keytab in tabs:
		var b=tabs[keytab];var style=W.button_box(W.SOFT if keytab==tab else W.PANEL,W.GREEN if keytab==tab else W.SOFT)
		style.border_width_bottom=3 if keytab==tab else 0;b.add_theme_stylebox_override("normal",style)
	primary.text=("看方案" if compact_layout else "查看运营方案") if tab!="slots" else ("看概况" if compact_layout else "回到运行概况")
func switch_tab(value:String):
	if value==tab:return
	scroll_positions[tab]=scroll.scroll_vertical;tab=value;signature="";app.play_cue("select")
	if selected_id!="":update_view(app.game,app.shot,selected_id)
func text(parent:Node,key:String,value:String="",size:int=16,color:Color=W.TEXT):
	var l=W.label(value,size,color);parent.add_child(l);nodes[key]=l;return l
func _group(parent:Node,title:String="",bg:Color=W.PANEL) -> VBoxContainer:
	var content=C.group(parent,title,bg)
	var panel=content.get_parent()
	if panel is PanelContainer:
		panel.add_theme_stylebox_override("panel",W.box(bg,W.SOFT,10))
		content.add_theme_constant_override("separation",8)
	return content
func _resource_row(parent:Node,id:String,label_text:String,value:String,unit:String="") -> Dictionary:
	var q=D.quantity(id);var row=W.hbox(parent,8);row.custom_minimum_size.y=30
	C.icon(row,str(q.icon),W.GREEN,18);var name=W.label(label_text,15,W.TEXT);row.add_child(name)
	var val=W.label(value,16,W.TEXT,false);val.size_flags_horizontal=Control.SIZE_SHRINK_END;row.add_child(val)
	var suffix=W.label(unit,13,W.MUTED,false);suffix.size_flags_horizontal=Control.SIZE_SHRINK_END;row.add_child(suffix)
	return {"root":row,"label":name,"value":val,"unit":suffix}
func rebuild():
	W.clear(body);nodes={}
	if tab=="overview":overview()
	elif tab=="slots":slots()
	else:details()
func overview():
	var m=model;var narrow=compact_layout or W.text_scale>1.2
	if m.current.status=="construction":
		var v=_group(body,"施工进度");text(v,"construction","",20);var bar=W.bar(0);v.add_child(bar);nodes.construction_bar=bar
	if not m.house.is_empty():
		var v=_group(body,"居住情况");text(v,"housing","",24);text(v,"vacancies","",14,W.MUTED);var b=W.bar(0);v.add_child(b);nodes.housing_bar=b
		var grid=GridContainer.new();grid.columns=1 if narrow else 3;grid.add_theme_constant_override("h_separation",8);grid.add_theme_constant_override("v_separation",8);v.add_child(grid)
		for key in ["health","happiness","education"]:
			var q=D.quantity(key);var group=_group(grid,"",W.SOFT);var row=W.hbox(group,6);C.icon(row,q.icon,W.GREEN,18);row.add_child(W.label(q.label,14,W.MUTED));text(group,key,"",21,W.TEXT)
	if not m.outputs.is_empty():
		var v=_group(body,"实际贡献" if not app.shot.get("forecast",false) else "预计贡献")
		for x in m.outputs:
			var row=_resource_row(v,x.id,P.RES.get(x.id,x.id),"",x.unit);nodes["out:"+x.id]=row
			if x.id!="energy":text(v,"kept:"+x.id,"",14,W.MUTED)
	if not m.services.is_empty():
		var v=_group(body,"服务居民")
		for x in m.services:
			var q=_resource_row(v,x.id,P.KIND[x.id],"","人 / 有效容量");nodes["service:"+x.id]=q
			var bar=W.bar(0);v.add_child(bar);nodes["servicebar:"+x.id]=bar
	var status=W.vbox(body,4);text(status,"status","",16);text(status,"status_detail","",14,W.MUTED)
	if m.jobs>0:
		var v=_group(body,"用工与运行");text(v,"workforce","",18);text(v,"workforce_detail","",14,W.MUTED)
		var pr=W.vbox(v,8) if narrow else W.hbox(v,8)
		for pair in [[-1,"让出人手"],[0,"普通"],[1,"优先供人"]]:
			var b=W.button(pair[1],func():app.act(app.game.set_priority(selected_id,pair[0])));b.custom_minimum_size.x=0;b.size_flags_horizontal=Control.SIZE_EXPAND_FILL;pr.add_child(b);nodes["priority:"+str(pair[0])]=b
	var inputs=_group(body,"投入与维护")
	for x in m.inputs:
		var q=_resource_row(inputs,x.id,("电力合计" if x.id=="energy" else P.RES.get(x.id,x.id)),"",x.unit);nodes["in:"+x.id]=q
	if m.inputs.is_empty():text(inputs,"no_inputs","本周期无物资扣除" if m.known else "投入待结算",14,W.MUTED)
	C.divider(inputs)
	for i in m.fees.size():nodes["fee:"+str(i)]=_resource_row(inputs,"currency",m.fees[i].label,"","通货/h")
	var source=W.flat(W.button("展开投入来源",func():switch_tab("details")));source.custom_minimum_size.x=0;inputs.add_child(source)
	if "building.logistics" in m.current.traits and not m.inventory.is_empty():
		var v=_group(body,"可用仓储 · 当前库存")
		for x in m.inventory:nodes["stock:"+x.id]=_resource_row(v,x.id,P.RES.get(x.id,x.id),"","库存 / 仓容")
	var group=_group(body,"方案位",W.PANEL);text(group,"cohesion","",14,W.MUTED)
	var rack=GridContainer.new();rack.columns=1 if W.text_scale>1.2 else (2 if compact_layout else 3);rack.add_theme_constant_override("h_separation",8);rack.add_theme_constant_override("v_separation",8);group.add_child(rack)
	for s in m.slots:C.socket(rack,app.game,selected_id,s.stable_id,func():app.Dialogs.slot_detail(app,selected_id,s.stable_id),true)
	text(group,"cohesion_note","满自营奖励 +3% · 允许混搭",14,W.MUTED)
func slots():
	var v=_group(body,"运营体系");var row=W.hbox(v,8);C.icon(row,"org_"+model.operator_org,W.GREEN,22);row.add_child(W.label(model.operator,17));text(v,"cohesion","",15,W.MUTED)
	v.add_child(W.label("行业色看主题，插口看类型。",14,W.MUTED))
	for s in model.slots:
		var slot=C.socket(body,app.game,selected_id,s.stable_id,func():app.Dialogs.slot_detail(app,selected_id,s.stable_id))
		if s.state=="conditional":C.badge(slot.root.get_child(0),s.reason,W.GOLD,W.SOFT)
		if s.instance_id!="":
			var data=app.game.c.bonuses[s.bonus_id];var inside=slot.root.get_child(0)
			var detail=D.card(app.game,s.bonus_id)
			for effect_data in detail.get("effect_rows",[]):C.effect(inside,effect_data,detail.visual_domain)
			inside.add_child(W.label("当前签约 · "+app.game.c.operators.get(s.contract_org,{}).get("name","无记录"),14,W.MUTED))
	body.add_child(W.flat(W.button("管理建筑",manage)))
func details():
	var m=model
	var inputs=_group(body,"投入来源 · "+m.reading)
	for x in m.inputs:
		text(inputs,"detail_in:"+x.id,"",17,W.TEXT)
		text(inputs,"detail_sources:"+x.id,"",14,W.MUTED)
	var out=_group(body,"产出与去向 · "+m.reading)
	for x in m.outputs:
		text(out,"detail_out:"+x.id,"",17,W.TEXT);text(out,"detail_out_sources:"+x.id,"",14,W.MUTED)
	if m.outputs.is_empty():out.add_child(W.label("本周期无物资产出。",14,W.MUTED))
	var fees=_group(body,"财政负担 · "+m.reading)
	for i in m.fees.size():nodes["fee:"+str(i)]=_resource_row(fees,"currency",m.fees[i].label,"","通货/h")
	var storage=_group(body,"库存 · 当前持有量")
	for x in m.inventory:
		nodes["stock:"+x.id]=_resource_row(storage,x.id,P.RES.get(x.id,x.id),"","库存 / 仓容")
		storage.add_child(W.label("可接收外部溢出" if x.public else "只计本地可用仓容",14,W.MUTED))
	var links=_group(body,"居民与工作关系");text(links,"links","",14,W.MUTED)
	var environment=_group(body,"所在位置的场来源");text(environment,"fields","",14,W.MUTED)
	var trace=_group(body,"数值解释");text(trace,"trace","",14,W.MUTED)
	var history=_group(body,"原方案与当前合同");text(history,"history","",14,W.MUTED)
func refresh_values():
	var m=model
	if nodes.has("construction"):
		nodes.construction.text="剩余 %.1f CP · 材料 %.1f"%[m.current.job.work,m.current.job.material];nodes.construction_bar.value=1-float(m.current.job.work)/maxf(.001,m.current.job.total)
	if nodes.has("housing"):
		nodes.housing.text="%.1f 人 / %.0f 位置"%[m.house.count,m.house.capacity];nodes.vacancies.text="%.0f%% 入住 · %.1f 空位"%[100*m.house.count/maxf(.001,m.house.capacity),m.house.vacant];nodes.housing_bar.value=m.house.count/maxf(.001,m.house.capacity)
		for k in ["health","happiness","education"]:nodes[k].text="%.0f"%m.house[k] if m.house.count>0 else "—"
	for x in m.inputs:
		if nodes.has("in:"+x.id):nodes["in:"+x.id].value.text="%.1f"%x.value if m.known else "—"
		if nodes.has("detail_in:"+x.id):
			nodes["detail_in:"+x.id].text=P.RES.get(x.id,x.id)+"合计 %.3f %s"%[x.value,x.unit]
			var lines=[]
			for e in x.children:
				var scope="住户" if str(e.get("consumer","")).begins_with("pop:") or e.get("source","")=="住户用电" else "楼体 / 配方"
				lines.append(scope+" · %.3f %s ← %s"%[e.rate,x.unit,str(e.get("source",""))])
			nodes["detail_sources:"+x.id].text="\n".join(lines)
	for x in m.outputs:
		if nodes.has("out:"+x.id):
			nodes["out:"+x.id].value.text="%.1f"%x.value if m.known else "—"
			if nodes.has("kept:"+x.id):nodes["kept:"+x.id].text="其中可入库 %.1f/h · 丢弃 %.1f/h"%[x.kept,x.lost]
		if nodes.has("detail_out:"+x.id):
			nodes["detail_out:"+x.id].text=P.RES.get(x.id,x.id)+" %.3f %s"%[x.value,x.unit]
			var lines=[]
			for e in x.children:lines.append(str(e.source)+" · %.3f；留下 %.3f，丢弃 %.3f"%[e.rate,e.kept,e.lost])
			nodes["detail_out_sources:"+x.id].text="\n".join(lines)
	for x in m.services:
		if nodes.has("service:"+x.id):nodes["service:"+x.id].value.text="%.1f / %.1f"%[x.value,x.capacity];nodes["servicebar:"+x.id].value=x.value/maxf(.001,x.capacity)
	for i in m.fees.size():
		if nodes.has("fee:"+str(i)):nodes["fee:"+str(i)].value.text="%.1f"%m.fees[i].value
	for x in m.inventory:
		if nodes.has("stock:"+x.id):nodes["stock:"+x.id].value.text="%.1f / %.1f"%[x.stock,x.capacity]
	if nodes.has("status"):
		var s=D.status(m.status.code);nodes.status.text=("✓ 运行正常 · " if m.status.code=="ok" else s.label+" · ")+m.status.message
		nodes.status.add_theme_color_override("font_color",W.GREEN if m.status.code=="ok" else (W.GOLD if m.status.code in ["warning","danger"] else W.MUTED));nodes.status_detail.text=m.status.detail
	if nodes.has("workforce"):
		nodes.workforce.text="到岗 %.1f / %.1f 人"%[m.assigned,m.jobs]
		nodes.workforce_detail.text="有效劳动 %.1f · 运行 %.0f%%\n建筑效率 %.0f%%"%[m.effective,100*m.utilization,100*m.stats.efficiency]
		for k in [-1,0,1]:nodes["priority:"+str(k)].add_theme_stylebox_override("normal",W.button_box(W.GREEN if k==m.priority else W.PANEL,W.GREEN if k==m.priority else W.SOFT))
	if nodes.has("cohesion"):nodes.cohesion.text="%d / %d 已装 · 自营 %d / %d"%[m.cohesion.occupied,m.cohesion.total,m.cohesion.owned,m.cohesion.total]
	if tab=="details":
		var lines=[]
		for l in m.employment:lines.append("来自 "+app.game.project_name(app.shot.groups.get(l.group,{}).get("host",""))+" · %.2f 人 / %.2f 有效劳动"%[l.people,l.effective])
		for group in m.groups:
			for bid in group.employment:lines.append("本住区 → "+app.game.project_name(bid)+" · %.2f 人"%group.employment[bid])
		for l in m.service_links:lines.append(P.KIND.get(l.kind,l.kind)+" → "+app.game.project_name(l.home)+" · %.2f 人"%l.people)
		nodes.links.text="\n".join(lines) if not lines.is_empty() else "本周期无分配记录。"
		lines=[]
		for kind in m.fields:
			for f in m.fields[kind]:lines.append(app.MapData.NAMES.get(kind,kind)+" %+.3f ← "%f.value+app.game.project_name(f.source_id))
		nodes.fields.text="\n".join(lines) if not lines.is_empty() else "本周期无场来源。"
		lines=[]
		for field in m.trace:
			var x=m.trace[field];lines.append(field+" = %.3f"%float(x.get("value",0)))
			for rule in x.get("rules",[]):lines.append("  "+str(rule.source)+" · "+str(rule.mode)+" "+str(rule.value))
		nodes.trace.text="\n".join(lines) if not lines.is_empty() else "没有额外数值修正。"
		lines=[]
		for s in m.slots:
			if s.instance_id=="":continue
			lines.append(app.game.c.bonuses[s.bonus_id].name+"\n原提供方："+app.game.c.operators.get(s.provider_org,{}).get("name",s.provider_org)+"\n当前签约："+app.game.c.operators.get(s.contract_org,{}).get("name",s.contract_org))
		nodes.history.text="\n\n".join(lines) if not lines.is_empty() else "尚未安装运营方案。"
func manage():
	var g=app.game;var id=selected_id
	app.open_modal(g.project_name(id)+" · 管理","暂停是可逆操作；拆除需要单独确认。")
	var v=_group(app.modal_body,"运行与组织")
	v.add_child(W.button("恢复运行" if g.s.projects[id].paused else "暂停运行",func():app.act(g.set_paused(id,not g.s.projects[id].paused));app.close_modal()))
	v.add_child(W.button("查看建筑成长",func():app.close_modal();app.desk.open("city040","upgrade:"+id),g.s.projects[id].get("status","")!="running"))
	v.add_child(W.button("查看部署许可",func():app.close_modal();app.desk.open("city040","permits")))
	var danger=_group(app.modal_body,"危险操作",W.SOFT);var b=W.button("拆除建筑…",func():app.Dialogs.demolition(app,id));b.add_theme_stylebox_override("normal",W.button_box(W.PANEL,W.GOLD));b.add_theme_color_override("font_color",W.GOLD);danger.add_child(b)
