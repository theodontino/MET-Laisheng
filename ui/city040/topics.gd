extends RefCounted
## Read-only topic -> subtopic -> detail pages for the City Desk.
const W = preload("res://ui/widgets.gd")
const C = preload("res://ui/components031/components.gd")
const M = preload("res://ui/city032/models.gd")
const P = preload("res://ui/presentation.gd")
const V = preload("res://ui/reset/desk_visuals.gd")

const RESOURCE_NAMES = {"food":"粮食", "material":"材料", "goods":"制成品", "energy":"电力"}
const NEED_NAMES = {"food":"粮食", "material":"材料", "goods":"制成品", "energy":"电力", "medical":"医疗", "education":"教育", "commercial":"购物", "entertainment":"休闲"}
const POLICY_SLOTS = [
	["Tax", "税制"],
	["Labor", "就业分配"],
	["Environment", "环境治理"],
	["Procurement", "物资采购"],
	["Land", "用地制度"]
]

class FinanceHistoryChart extends Control:
	const ThemeColors=preload("res://ui/widgets.gd")
	var samples:Array=[]
	func set_data(rows:Array) -> void:
		samples=rows.duplicate(true);queue_redraw()
	func _draw() -> void:
		var font=get_theme_default_font()
		if font==null:return
		var plot=Rect2(Vector2(62,38),Vector2(maxf(30,size.x-82),maxf(32,size.y-72)))
		draw_line(Vector2(plot.position.x,16),Vector2(plot.position.x+18,16),ThemeColors.GREEN,3.)
		draw_string(font,Vector2(plot.position.x+25,21),"收入",HORIZONTAL_ALIGNMENT_LEFT,-1,int(13*ThemeColors.text_scale),ThemeColors.TEXT)
		draw_line(Vector2(plot.position.x+90,16),Vector2(plot.position.x+108,16),ThemeColors.GOLD,3.)
		draw_string(font,Vector2(plot.position.x+115,21),"支出",HORIZONTAL_ALIGNMENT_LEFT,-1,int(13*ThemeColors.text_scale),ThemeColors.TEXT)
		var timed=[];var high=1.;var low=0.;var has_values=false
		for row in samples:
			if row.get("h")==null:continue
			timed.append(row)
			for key in ["income","upkeep"]:
				if row.get(key)==null:continue
				has_values=true;high=maxf(high,float(row[key]));low=minf(low,float(row[key]))
		if timed.is_empty() or not has_values:
			draw_string(font,plot.position+Vector2(10,plot.size.y*.55),"结算后开始绘制历史，不补造旧数据",HORIZONTAL_ALIGNMENT_LEFT,plot.size.x,int(14*ThemeColors.text_scale),ThemeColors.MUTED);return
		var span=maxf(1.,high-low)
		for i in range(4):
			var y=plot.position.y+plot.size.y*i/3.
			draw_line(Vector2(plot.position.x,y),Vector2(plot.end.x,y),ThemeColors.SOFT,1.)
			draw_string(font,Vector2(2,y+5),"%.1f"%(high-span*i/3.),HORIZONTAL_ALIGNMENT_LEFT,-1,int(13*ThemeColors.text_scale),ThemeColors.MUTED)
		var first=float(timed[0].h);var last=float(timed[-1].h);var duration=maxf(.001,last-first)
		for key in ["income","upkeep"]:
			var previous=Vector2.ZERO;var continuous=false;var tint=ThemeColors.GREEN if key=="income" else ThemeColors.GOLD
			for row in timed:
				if row.get(key)==null:continuous=false;continue
				var point=plot.position+Vector2((float(row.h)-first)/duration*plot.size.x,(high-float(row[key]))/span*plot.size.y)
				if continuous:draw_line(previous,point,tint,2.5,true)
				draw_circle(point,2.5,tint);previous=point;continuous=true
		draw_string(font,Vector2(plot.position.x,plot.end.y+24),"%.1f月"%first,HORIZONTAL_ALIGNMENT_LEFT,-1,int(13*ThemeColors.text_scale),ThemeColors.MUTED)
		if last>first:draw_string(font,Vector2(plot.end.x-70,plot.end.y+24),"%.1f月"%last,HORIZONTAL_ALIGNMENT_RIGHT,70,int(13*ThemeColors.text_scale),ThemeColors.MUTED)

static func _group(parent:Node,title:String="",bg:Color=W.PANEL) -> VBoxContainer:
	var icon="ledger"
	for pair in [["预算","currency"],["收支","currency"],["收入","currency"],["支出","currency"],["仓","material"],["住房","home"],["居住","home"],["岗位","population"],["居民","population"],["需求","population"],["制度","policy"]]:
		if str(pair[0]) in title:icon=str(pair[1]);break
	return V.card(parent,title,"",icon if title!="" else "",W.INFO if bg==W.PANEL else W.GREEN)

static func _grid(parent:Node,columns:int=3) -> GridContainer:
	return V.grid(parent,columns)

static func _note(parent:Node,text:String) -> void:
	parent.add_child(W.label(text,14,W.MUTED))

static func _fold(parent:Node,title:String) -> VBoxContainer:
	return V.fold(parent,title,"原始记录与精确数值，按需展开")

static func _number(parent:Node,title:String,value:String,note:String="",tone:Color=W.TEXT) -> VBoxContainer:
	return V.metric(parent,"",title,value,note,tone)

static func _meter(parent:Node,title:String,value:float,total:float,suffix:String,tone:Color=W.GREEN,note:String="",action:Callable=Callable(),known:bool=true) -> VBoxContainer:
	var box=W.vbox(parent,5);box.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	box.set_meta("desk_visual","meter");box.set_meta("metric_label",title);box.set_meta("metric_value",value if known else null);box.set_meta("metric_total",total if known else null);box.set_meta("metric_known",known);box.set_meta("metric_text",suffix)
	var line=W.hbox(box,8)
	if action.is_valid():
		var button=W.flat(W.button(title+" ›",action));button.size_flags_horizontal=Control.SIZE_EXPAND_FILL;line.add_child(button)
	else:
		var label=W.label(title,15,W.TEXT);label.size_flags_horizontal=Control.SIZE_EXPAND_FILL;line.add_child(label)
	line.add_child(W.label(suffix,17,tone,false))
	if known:
		var bar=W.bar(value/total if total>0 else 0.);bar.custom_minimum_size.y=12
		var bar_tone=tone if total>0 else W.MUTED
		bar.add_theme_stylebox_override("fill",W.box(bar_tone,bar_tone,4));box.add_child(bar)
		bar.tooltip_text=title+" · "+suffix+("\n"+note if note!="" else "")
	if note!="":_note(box,note)
	return box

static func _record_value(row:Dictionary,key:String,format:String="%.2f") -> String:
	return "—" if row.get(key)==null else format%float(row[key])

static func _coverage_tone(ratio:float) -> Color:
	return W.RED.lerp(W.GOLD,clampf(ratio*2,0,1)) if ratio<.5 else W.GOLD.lerp(W.GREEN,clampf((ratio-.5)*2,0,1))

static func _nav(parent:Node,d,label:String,section:String,detail:String="",summary:String="") -> Button:
	var box=_group(parent,"")
	var row=W.hbox(box,12);var icon_id="layers"
	if section in ["income","expense"]:icon_id="currency"
	elif section=="history":icon_id="clock"
	elif section=="housing":icon_id="home"
	elif section=="employment":icon_id="jobs"
	elif section=="needs":icon_id="population"
	elif section in ["supply","consumption","storage"]:icon_id=str(d.arg) if RESOURCE_NAMES.has(str(d.arg)) else "material"
	elif section=="rules":icon_id="policy"
	V.icon(row,icon_id,32,W.INFO)
	var button=W.button(label+"  ›",func():d.push_section(section,detail));button.custom_minimum_size.y=46
	button.size_flags_horizontal=Control.SIZE_EXPAND_FILL;row.add_child(button)
	if summary!="":_note(box,summary)
	return button

static func _source_action(d,id:String) -> Callable:
	if id=="external_market":return func():d.navigate("city040","market")
	if id=="commission":return func():d.navigate("tasks")
	if id=="unitemized":return Callable()
	return func():
		var g=d.app.game
		if g.s.projects.has(id) and not g.s.projects[id].get("special",false):
			d.locate(id);d.app.mode="inspect";d.app.selection=[str(g.s.projects[id].tile)];d.close()
		else:d.app.message("此项来自城市公共库存或地图外人口；完整数值已计入本页。")

static func _contributions(parent:Node,rows:Array,key:String="rate",color:Color=W.GREEN,limit:int=6,unit:String="/月",d=null) -> void:
	var merged={}
	for item in rows:
		var id=str(item.get("source_id",item.get("id","")))
		if not merged.has(id):merged[id]={"id":id,"name":str(item.get("source",item.get("name",id))),"value":0.}
		merged[id].value+=float(item.get(key,0))
	var ordered=merged.values();ordered.sort_custom(func(a,b):return absf(float(a.value))>absf(float(b.value)))
	var total=0.
	for item in ordered:total+=absf(float(item.value))
	if ordered.is_empty():_note(parent,"当前快照没有这类记录。");return
	for item in ordered.slice(0,limit):
		var id=str(item.id);var action=Callable() if d==null else _source_action(d,id)
		var meter=_meter(parent,str(item.name),absf(float(item.value)),total,"%.2f%s"%[float(item.value),unit],color,"占此组 %.0f%%"%(100.*absf(float(item.value))/maxf(.000001,total)),action)
		meter.set_meta("source_id",id)
	if ordered.size()>limit:_note(parent,"另有 %d 项；完整来源在下方展开。"%(ordered.size()-limit))

static func _chart(parent:Node,model:Dictionary) -> void:
	var chart=FinanceHistoryChart.new();chart.set_data(model.history);chart.custom_minimum_size.y=200*W.text_scale
	parent.add_child(chart)

static func render_finance(d) -> void:
	d.body.set_meta("snapshot_id",str(d.app.shot.get("snapshot_id","")))
	var model=M.budget(d.app.game,d.app.shot)
	d.title.text="预算与收支";d.cost.text="";d.subtitle.text=str(model.stamp)+" · 通货 / 来生月"
	match str(d.subtopic):
		"":_finance_summary(d,model)
		"income","expense":
			if d.detail_id=="":_finance_category(d,model,str(d.subtopic))
			else:_finance_sources(d,model,str(d.subtopic),str(d.detail_id))
		"history":
			if d.detail_id=="":_finance_history(d,model)
			else:_finance_history_detail(d,model,str(d.detail_id))
		_:d.subtopic="";render_finance(d)

static func _finance_summary(d,model:Dictionary) -> void:
	var top=_grid(d.body,2)
	_number(top,"可用预算","%.1f"%float(model.balance),"当前持有通货",W.GREEN if model.balance>=0 else W.GOLD)
	_number(top,"每月经营余量","%+.2f"%float(model.net),"收入 − 支出；一次性支出单列",W.GREEN if model.net>=0 else W.GOLD)
	var flow=_group(d.body,"持续收支")
	var scale=maxf(float(model.income),float(model.expense))
	_meter(flow,"收入",float(model.income),scale,"+%.2f /月"%float(model.income),W.GREEN)
	_meter(flow,"支出",float(model.expense),scale,"−%.2f /月"%float(model.expense),W.GOLD)
	var links=_grid(d.body,3)
	_nav(links,d,"收入构成","income","","哪些经营活动正在支付城市？")
	_nav(links,d,"支出构成","expense","","维护由哪些建筑与居民产生？")
	_nav(links,d,"历史流水","history","","已发生的经营周期与一次性操作")
	var history=_fold(d.body,"查看已结算收支走势");_chart(history,model)

static func _finance_category(d,model:Dictionary,category:String) -> void:
	var incoming=category=="income";var rows:Array=model.income_rows if incoming else model.expense_rows
	var value=float(model.income if incoming else model.expense);var tone=W.GREEN if incoming else W.GOLD
	d.title.text="收入构成" if incoming else "支出构成"
	var columns=_grid(d.body,2)
	_number(columns,"本期合计",("+" if incoming else "−")+"%.2f /月"%value,str(model.stamp),tone)
	var policy_mode=str(d.app.game.compiled.get("options",{}).get("tax_mode",""))
	var reason={"head":"实际居住人口","production":"实际物资产出","trade":"实际消费活动"}.get(policy_mode,"实际经营记录") if incoming else "建筑维护 + 居民维护"
	_number(columns,"结算依据",reason,"逐项保留原始来源")
	var graph=_group(d.body,"贡献最大的来源 · 长度为本组占比")
	_contributions(graph,rows,"rate",tone,6,"/月",d)
	_nav(d.body,d,"展开全部来源",category,"sources","%d 条结算记录，可继续查看每项来源"%rows.size())
	if incoming:d.body.add_child(W.button("比较税制 ›",func():d.navigate("policies")))

static func _finance_sources(d,model:Dictionary,category:String,detail:String) -> void:
	var incoming=category=="income";var rows:Array=model.income_rows if incoming else model.expense_rows
	var tone=W.GREEN if incoming else W.GOLD
	d.title.text="收入来源" if incoming else "支出来源"
	if detail.begins_with("source:"):
		var id=detail.trim_prefix("source:");var matches=[]
		for item in rows:
			if str(item.get("source_id",""))==id:matches.append(item)
		var total=0.
		for item in matches:total+=float(item.get("rate",0))
		_number(d.body,d.app.game.project_name(id),"%.2f /月"%total,str(model.stamp),tone)
		for item in matches:_meter(d.body,str(item.get("name","经营记录")),absf(float(item.get("rate",0))),absf(total),"%.3f /月"%float(item.get("rate",0)),tone)
		d.body.add_child(W.button("定位来源 ›",_source_action(d,id)))
		return
	var graph=_group(d.body,"来源占比");_contributions(graph,rows,"rate",tone,8,"/月",d)
	var table=_fold(d.body,"完整逐项账本 · %d 条"%rows.size())
	for item in rows:
		var id=str(item.get("source_id",""))
		d._row(table,str(item.get("name","经营记录"))+" · "+str(item.get("source","")),"%.3f /月"%float(item.get("rate",0)),func():d.push_section(category,"source:"+id))

static func _finance_history(d,model:Dictionary) -> void:
	d.title.text="预算历史";d.subtitle.text="仅显示已保存的真实结算，不补造历史"
	var plot=_group(d.body,"经营收入与支出 · 通货 / 月");_chart(plot,model)
	var links=_grid(d.body,2)
	_nav(links,d,"经营周期","history","cycles","%d 条已保存周期"%model.history.size())
	_nav(links,d,"一次性收支","history","transactions","%d 条建设、政策、贸易与奖励记录"%model.transactions.size())

static func _finance_history_detail(d,model:Dictionary,detail:String) -> void:
	if detail=="cycles":
		d.title.text="经营周期记录";d.subtitle.text="真实周期走势；精确账本按需展开"
		_chart(d.body,model)
		var table=[];var rows:Array=model.history
		for i in range(rows.size()-1,maxi(-1,rows.size()-61),-1):
			var item:Dictionary=rows[i]
			table.append([_record_value(item,"h","%.2f月"),_record_value(item,"income","+%.2f"),_record_value(item,"upkeep","−%.2f"),_record_value(item,"net","%+.2f"),_record_value(item,"balance")])
		if table.is_empty():_note(d.body,"首次结算后会出现记录。")
		else:d._table(_fold(d.body,"最近60个周期的精确数值"),["本地时间","收入/月","支出/月","净收支/月","期末余额"],table)
	elif detail=="transactions":
		d.title.text="一次性收支";d.subtitle.text="按保存顺序展示；不混入每月经营收支"
		var journal:Array=model.transactions;var table=[]
		var recent=journal.slice(maxi(0,journal.size()-60));var gain=0.;var spent=0.;var cash_known=true
		for item in recent:
			if item.get("cash")==null:cash_known=false;continue
			if float(item.cash)>0:gain+=float(item.cash)
			else:spent-=float(item.cash)
		var summary=_grid(d.body,2);_number(summary,"最近60项流入","+%.2f"%gain,"已记录通货"+("；部分记录金额未知" if not cash_known else ""),W.GREEN);_number(summary,"最近60项流出","−%.2f"%spent,"已记录通货"+("；部分记录金额未知" if not cash_known else ""),W.GOLD)
		var scale=maxf(gain,spent)
		for i in range(recent.size()-1,maxi(-1,recent.size()-9),-1):
			var item:Dictionary=recent[i];var cash=float(item.cash) if item.get("cash")!=null else 0.
			_meter(d.body,_record_value(item,"h","%.2f月")+" · "+str(item.get("label",item.get("kind","操作"))),absf(cash),scale,_record_value(item,"cash","%+.2f"),W.GREEN if cash>=0 else W.GOLD,"DT "+_record_value(item,"dt","%+.2f")+" · 操作后余额 "+_record_value(item,"balance"),Callable(),item.get("cash")!=null)
		for i in range(recent.size()-1,-1,-1):
			var item:Dictionary=recent[i];table.append([_record_value(item,"h","%.2f月"),str(item.get("label",item.get("kind","操作"))),_record_value(item,"cash","%+.2f"),_record_value(item,"dt","%+.2f"),_record_value(item,"balance")])
		if not table.is_empty():d._table(_fold(d.body,"完整60项流水"),["时间","操作","通货","DT","余额"],table)
		else:_note(d.body,"尚无已保存的一次性收支。")
	else:
		d.title.text="导出记录";_note(d.body,"导出包含已有结算与操作记录。")
		d.footer.show();d.primary.show();d.primary.text="导出完整记录";d.primary.pressed.connect(func():d.app.export_file("record"));d.footer_text.text="导出已有结算与操作记录"

static func render_resource(d) -> void:
	d.body.set_meta("snapshot_id",str(d.app.shot.get("snapshot_id","")))
	var id=str(d.arg) if RESOURCE_NAMES.has(str(d.arg)) else "food"
	var model=M.resource(d.app.game,d.app.shot,id)
	d.title.text=str(model.name);d.cost.text="";d.subtitle.text=str(model.stamp)+" · 库存为当前持有"
	var tabs=W.hbox(d.body,8)
	for key in RESOURCE_NAMES:
		var resource=str(key);var button=W.button(str(RESOURCE_NAMES[key]),func():d.navigate("resource",resource))
		button.size_flags_horizontal=Control.SIZE_EXPAND_FILL
		if resource==id:button.disabled=true
		tabs.add_child(button)
	if d.subtopic=="":_resource_summary(d,model,id)
	elif d.subtopic in ["supply","consumption","storage"]:
		if d.detail_id=="":_resource_subtopic(d,model,id,str(d.subtopic))
		else:_resource_detail(d,model,id,str(d.subtopic))
	else:d.subtopic="";render_resource(d)

static func _resource_flow(d,model:Dictionary,id:String) -> void:
	var energy=id=="energy";var unit="" if energy else "/月"
	var supply=float(model.get("supply",0)) if energy else float(model.get("produced",0))+float(model.get("imported",0))
	var used=float(model.get("need",0))*float(model.get("ratio",1)) if energy else float(model.get("consumed",0))+float(model.get("exported",0))+float(model.get("delivered",0))+float(model.get("lost",0))
	V.flow(d.body,[
		{"id":id,"label":"供给" if energy else "生产 + 进口","value":"%.2f%s"%[supply,unit],"note":str(model.stamp),"color":W.GREEN},
		{"id":id,"label":"实际供电" if energy else "消耗与流出","value":"%.2f%s"%[used,unit],"note":"满足需求" if energy else "消费 + 出口 + 交付 + 损耗","color":W.GOLD},
		{"id":id,"label":"未满足需求" if energy else "库存净变化","value":"%.2f"%maxf(0,float(model.get("need",0))-used) if energy else "%+.2f /月"%float(model.get("net",0)),"note":"电网按周期供给" if energy else "储备增加 / 减少","color":W.INFO}
	],"同一周期的供需关系；库存位置另列")
	var balance=_group(d.body,"供给与实际使用 · 同一比例尺")
	_meter(balance,"供给",supply,maxf(supply,used),"%.2f%s"%[supply,unit],W.GREEN)
	_meter(balance,"供电" if energy else "消耗与流出",used,maxf(supply,used),"%.2f%s"%[used,unit],W.GOLD)
	if not energy:
		var terms=_grid(balance,3)
		for pair in [["produced","生产"],["imported","进口"],["consumed","消耗"],["exported","出口"],["delivered","交付"],["lost","损耗"]]:
			terms.add_child(W.label("%s  %.2f /月"%[pair[1],float(model.get(pair[0],0))],14,W.MUTED))
		var shortage=maxf(0,float(model.get("need",0))-float(model.get("consumed",0)))
		if shortage>.0001:_note(balance,"实际申请仍有 %.2f /月未满足；消耗合计包含施工等实际扣除，细项见使用去向。"%shortage)

static func _resource_summary(d,model:Dictionary,id:String) -> void:
	if id!="energy":
		var capacity=0.
		for store in model.stores:capacity+=float(store.get("capacity",0))
		var stock=_group(d.body,"当前储备")
		_meter(stock,RESOURCE_NAMES[id],float(model.stock),capacity,"%.1f / %.1f 容量"%[float(model.stock),capacity],W.INFO,"各仓位置决定实际可用性；总量不等于任一建筑都能取用。")
	_resource_flow(d,model,id)
	var links=_grid(d.body,3)
	_nav(links,d,"供给来源","supply","","谁在生产，进口补了多少？")
	_nav(links,d,"使用去向","consumption","","居民、配方、施工与外部交付")
	_nav(links,d,"库存位置","storage","","按真实仓容查看储备" if id!="energy" else "电力按周期供给，不计普通库存")

static func _resource_records(model:Dictionary,id:String,section:String) -> Array:
	var field={"supply":"sources","consumption":"sinks","storage":"stores"}[section]
	var rows:Array=model.get(field,[]).duplicate(true)
	if section=="consumption" and id!="energy":
		var total=float(model.get("consumed",0))+float(model.get("exported",0))+float(model.get("delivered",0));var listed=0.
		for item in rows:listed+=float(item.get("rate",0))
		if total>listed+.000001:
			rows.append({"id":"unitemized","name":"其他实际扣除 · 总账差额","rate":total-listed,"note":"总账实际消耗与已列去向的差额，包含未单列的施工扣料；没有据此虚构建筑来源。"})
	return rows

static func _resource_subtopic(d,model:Dictionary,id:String,section:String) -> void:
	var field={"supply":"sources","consumption":"sinks","storage":"stores"}[section]
	d.title.text=RESOURCE_NAMES[id]+" · "+{"supply":"供给","consumption":"去向","storage":"仓储"}[section]
	if section=="storage":
		if id=="energy":_note(d.body,"电力按当前周期供需结算，本页没有普通电力库存。")
		for store in model.stores.slice(0,6):
			_meter(d.body,str(store.name),float(store.stock),float(store.capacity),"%.1f / %.1f"%[float(store.stock),float(store.capacity)],W.INFO,"可接收外部溢出" if store.get("external",false) else "本地产物仓",_source_action(d,str(store.id)))
	else:
		var tone=W.GREEN if section=="supply" else W.GOLD
		var chart=_group(d.body,"来源贡献" if section=="supply" else ("用电申请构成" if id=="energy" else "消耗去向构成"))
		_contributions(chart,_resource_records(model,id,section),"rate",tone,6,"" if id=="energy" else "/月",d)
		if id=="energy" and section=="consumption":_note(chart,"条形展示真实用电申请；全城满足率 %.0f%%，申请量不等于实际供电量。"%(100*float(model.get("ratio",1))))
	_nav(d.body,d,"全部来源与精确记录",section,"records","保留来源、去向、入库与损耗")

static func _resource_detail(d,model:Dictionary,id:String,section:String) -> void:
	d.title.text=RESOURCE_NAMES[id]+" · "+{"supply":"供给来源","consumption":"使用去向","storage":"库存位置"}[section]
	var rows:Array=_resource_records(model,id,section)
	if rows.is_empty():_note(d.body,"当前快照没有这类记录。");return
	if section=="storage":
		for item in rows:_meter(d.body,str(item.name),float(item.stock),float(item.capacity),"%.1f / %.1f"%[float(item.stock),float(item.capacity)],W.INFO,"可接收外部溢出" if item.get("external",false) else "本地产物仓",_source_action(d,str(item.id)))
	else:
		_contributions(d.body,rows,"rate",W.GREEN if section=="supply" else W.GOLD,8,"" if id=="energy" else "/月",d)
		var table=_fold(d.body,"完整来源记录 · %d 条"%rows.size())
		for item in rows:
			d._row(table,str(item.name),"%.3f%s"%[float(item.rate),"" if id=="energy" else "/月"],_source_action(d,str(item.id)))
			if str(item.get("note",""))!="":_note(table,str(item.note))
			if section=="supply" and id!="energy":_note(table,"入库 %.3f /月 · 损耗 %.3f /月"%[float(item.get("kept",0)),float(item.get("lost",0))])

static func render_population(d) -> void:
	d.body.set_meta("snapshot_id",str(d.app.shot.get("snapshot_id","")))
	var p=M.population(d.app.game,d.app.shot)
	d.title.text="居民";d.cost.text="";d.subtitle.text=str(p.stamp)
	match str(d.subtopic):
		"":_population_summary(d,p)
		"housing":
			if d.detail_id=="":_population_housing(d,p)
			else:_population_homes(d,p)
		"employment":
			if d.detail_id=="":_population_employment(d,p)
			else:_population_jobs(d,p)
		"needs":
			if d.detail_id=="":_population_needs(d,p)
			else:_population_need_detail(d,p,str(d.detail_id))
		_:d.subtopic="";render_population(d)

static func _population_summary(d,p:Dictionary) -> void:
	var capacity=_grid(d.body,2)
	var homes=_group(capacity,"住得下吗？");_meter(homes,"已安置 / 住房",float(p.housed),float(p.housing),"%.1f / %.1f"%[p.housed,p.housing],W.INFO,"待安置 %.1f 人"%float(p.homeless))
	var jobs=_group(capacity,"有工作吗？");_meter(jobs,"已就业 / 岗位",float(p.employed),float(p.jobs),"%.1f / %.1f"%[p.employed,p.jobs],W.GREEN,"待就业 %.1f 人"%float(p.unemployed))
	var wellbeing=_group(d.body,"居民状态 · 人数加权")
	var stats=_grid(wellbeing,3)
	_meter(stats,"健康",float(p.health),100,"%.0f / 100"%float(p.health) if float(p.count)>0 else "无居民",W.GREEN,"",Callable(),float(p.count)>0)
	_meter(stats,"幸福",float(p.happiness),100,"%.0f / 100"%float(p.happiness) if float(p.count)>0 else "无居民",W.INFO,"",Callable(),float(p.count)>0)
	_number(stats,"人口变化","—" if p.population_rate==null else "%+.2f 人/月"%float(p.population_rate),"最近实际结算")
	var links=_grid(d.body,3)
	_nav(links,d,"居住分布","housing","","容量、待安置与各住区")
	_nav(links,d,"就业分配","employment","","岗位来源与实际到岗")
	_nav(links,d,"生活需求","needs","","住区 × 需求覆盖矩阵")

static func _population_housing(d,p:Dictionary) -> void:
	d.title.text="人口 · 居住"
	var columns=_grid(d.body,2)
	var housing=_group(columns,"住房占用");_meter(housing,"已安置",float(p.housed),float(p.housing),"%.1f / %.1f"%[p.housed,p.housing],W.INFO,"空位 %.1f"%maxf(0,float(p.housing)-float(p.housed)))
	_number(columns,"等待安置","%.1f 人"%float(p.homeless),"仍计入生活需求与城市人口",W.GOLD if p.homeless>.1 else W.TEXT)
	_population_home_cards(d,p,4)
	_nav(d.body,d,"查看全部住区","housing","homes")

static func _home_rows(p:Dictionary) -> Array:
	var homes={}
	for row in p.rows:
		var id=str(row.get("home","homeless"));var count=float(row.get("count",0))
		if not homes.has(id):homes[id]={"id":id,"count":0.,"employed":0.,"health":0.,"happiness":0.,"education":0.,"coverage":0.}
		var entry:Dictionary=homes[id];entry.count+=count;entry.employed+=float(row.employed)
		for key in ["health","happiness","education"]:entry[key]+=float(row[key])*count
		entry.coverage+=float(row.get("mean_coverage",0))*count
	var result=homes.values();result.sort_custom(func(a,b):return float(a.count)>float(b.count));return result

static func _population_home_cards(d,p:Dictionary,limit:int) -> void:
	var homes=_home_rows(p);var present={}
	for home in homes:present[str(home.id)]=true
	var shot:Dictionary=d.app.shot
	for id in shot.get("projects",{}):
		var project:Dictionary=shot.projects[id];var st:Dictionary=shot.get("compiled",{}).get("stats",{}).get(id,{})
		if present.has(str(id)) or project.get("special",false) or project.get("status","")!="running" or float(st.get("housing",0))<=0:continue
		homes.append({"id":str(id),"count":0.,"employed":0.,"health":0.,"happiness":0.,"education":0.,"coverage":0.})
	var grid=_grid(d.body,2)
	for entry in homes.slice(0,limit):
		var id=str(entry.id);var n=maxf(.000001,float(entry.count));var st:Dictionary=d.app.shot.get("compiled",{}).get("stats",{}).get(id,{})
		var project:Dictionary=d.app.shot.get("projects",{}).get(id,{})
		var capacity=float(st.get("housing",0))*float(st.get("efficiency",1)) if id!="homeless" and project.get("status","")=="running" else 0.
		var name="待安置居民" if id=="homeless" else d.app.game.project_name(id)
		var box=V.building(grid,str(project.get("def","")),name,"实际住区 · 当前住房与居民") if id!="homeless" and not project.get("special",false) else V.card(grid,name,"","population",W.GOLD)
		if id=="homeless":_number(box,"等待安置","%.1f 人"%float(entry.count),"当前没有实际住房位置",W.GOLD)
		else:_meter(box,"人口 / 住房",float(entry.count),capacity,"%.1f / %.1f"%[entry.count,capacity],W.INFO)
		_meter(box,"就业",float(entry.employed),float(entry.count),"%.1f / %.1f 人"%[entry.employed,entry.count] if float(entry.count)>0 else "无住户",W.GREEN,"",Callable(),float(entry.count)>0)
		var detail=_fold(box,"居民状态与定位")
		for key in ["health","happiness","education"]:_meter(detail,{"health":"健康","happiness":"幸福","education":"教育"}[key],float(entry[key])/n,100,"%.1f"%(float(entry[key])/n) if float(entry.count)>0 else "—",W.INFO,"",Callable(),float(entry.count)>0)
		var coverage_known=float(entry.count)>0
		for row in p.rows:
			if str(row.home)!=id or float(row.count)<=0:continue
			for key in p.active_kinds:
				if not _need_known(d,row,str(key)):coverage_known=false
		_note(detail,"已启用需求平均覆盖 %.0f%%"%(100*float(entry.coverage)/n) if coverage_known else ("需求等待结算。" if float(entry.count)>0 else "暂无住户记录。"))
		if id!="homeless":detail.add_child(W.button("定位住区 ›",_source_action(d,id)))

static func _population_homes(d,p:Dictionary) -> void:
	d.title.text="居住 · 全部住区";d.subtitle.text=str(p.stamp)+" · 按居住位置合并人口"
	_population_home_cards(d,p,10000)

static func _population_employment(d,p:Dictionary) -> void:
	d.title.text="人口 · 就业"
	var columns=_grid(d.body,2)
	var capacity=_group(columns,"岗位填充");_meter(capacity,"已就业 / 岗位",float(p.employed),float(p.jobs),"%.1f / %.1f"%[p.employed,p.jobs],W.GREEN)
	var participation=_group(columns,"居民就业");_meter(participation,"已就业 / 居民",float(p.employed),float(p.count),"%.1f / %.1f"%[p.employed,p.count] if float(p.count)>0 else "无居民",W.INFO,"待就业 %.1f 人；空岗位 %.1f"%[p.unemployed,maxf(0,float(p.jobs)-float(p.employed))],Callable(),float(p.count)>0)
	_population_jobs(d,p,5)
	_nav(d.body,d,"全部岗位来源","employment","sources")

static func _population_jobs(d,p:Dictionary,limit:int=10000) -> void:
	if limit==10000:d.title.text="就业 · 岗位来源"
	var filled={}
	for row in p.rows:
		for id in row.get("jobs",{}):filled[str(id)]=float(filled.get(str(id),0))+float(row.jobs[id])
	var shot:Dictionary=d.app.shot;var records=[]
	for id in d.app.game.playable_ids():
		var stats:Dictionary=shot.get("compiled",{}).get("stats",{}).get(id,{})
		var project:Dictionary=shot.get("projects",{}).get(id,{})
		var capacity=float(stats.get("jobs",0)) if project.get("status","")=="running" and not project.get("paused",false) else 0.
		var count=float(filled.get(id,0))
		if capacity+count>.0001:records.append({"id":str(id),"capacity":capacity,"count":count})
	records.sort_custom(func(a,b):return float(a.capacity)>float(b.capacity))
	var group=_group(d.body,"实际到岗 / 岗位容量")
	for item in records.slice(0,limit):_meter(group,d.app.game.project_name(str(item.id)),float(item.count),float(item.capacity),"%.1f / %.1f"%[item.count,item.capacity],W.GREEN,"",_source_action(d,str(item.id)))
	if records.is_empty():_note(group,"当前没有可列出的岗位。")

static func _need_known(d,row:Dictionary,key:String) -> bool:
	if str(d.app.shot.get("error",""))!="" or float(row.get("count",0))<=0:return false
	var group:Dictionary=d.app.shot.get("groups",{}).get(str(row.id),{})
	# A settled group with no service entry received zero, rather than an unknown amount.
	if key in M.SERVICES:return d.app.shot.get("metrics",{}).get("service_covered",{}).has(str(row.id))
	return group.get("living",{}).has(key)

static func _population_needs(d,p:Dictionary) -> void:
	d.title.text="人口 · 生活需求";d.subtitle.text=str(p.stamp)+" · 每格为该组居民的真实满足率"
	var grid=_grid(d.body,4)
	for raw in M.RES+M.SERVICES:
		var key=str(raw);var active=key in p.active_kinds;var known=p.count>0
		for row in p.rows:
			if float(row.count)>0 and not _need_known(d,row,key):known=false
		var ratio=float(p.coverage_kinds.get(key,0));var box=V.card(grid,NEED_NAMES[key],"",key,W.INFO)
		_meter(box,"人数加权",ratio,1,"%.0f%%"%(ratio*100) if active and known else ("待结算" if active and float(p.count)>0 else ("无居民" if active else "未启用")),_coverage_tone(ratio) if active and known else W.MUTED,"",Callable(),active and known)
		box.add_child(W.flat(W.button("查看来源 ›",func():d.push_section("needs",key))))
	var matrix=_group(d.body,"已启用需求覆盖矩阵");matrix.set_meta("desk_visual","needs_matrix");matrix.set_meta("snapshot_id",str(d.app.shot.get("snapshot_id","")));matrix.set_meta("active_kinds",p.active_kinds.duplicate())
	var matrix_scroll=ScrollContainer.new();matrix_scroll.vertical_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;matrix.add_child(matrix_scroll)
	var columns=1+p.active_kinds.size();var cells=_grid(matrix_scroll,columns)
	cells.add_child(W.label("住区 / 人口",14,W.MUTED))
	for key in p.active_kinds:cells.add_child(W.label(NEED_NAMES[key],14,W.MUTED))
	for row in p.rows:
		if float(row.count)<=.000001:continue
		cells.add_child(W.label(str(row.name)+"\n%.1f 人"%float(row.count),14,W.TEXT))
		for raw in p.active_kinds:
			var key=str(raw);var known=_need_known(d,row,key);var ratio=float(row.coverage.get(key,0));var tone=_coverage_tone(ratio) if known else W.MUTED
			var tile=W.panel(cells,W.SOFT);tile.set_meta("need_id",key);tile.set_meta("population_group",str(row.id));tile.set_meta("coverage_known",known);tile.set_meta("need_active",true)
			if known:tile.set_meta("coverage_ratio",ratio)
			tile.add_theme_stylebox_override("panel",W.box(W.PANEL.lerp(tone,.30) if known else W.PANEL,W.SOFT,6))
			var label=W.label("%.0f%%"%(ratio*100) if known else "—",15,tone,false);label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;tile.add_child(label)
			tile.tooltip_text=NEED_NAMES[key]+" · "+str(row.name)+(" · 尚无结算记录" if not known else " · 实际满足率")
	for index in cells.get_child_count():cells.get_child(index).custom_minimum_size.x=140 if index%columns==0 else 64
	var legend=W.hbox(matrix,12);V.badge(legend,"0% 缺口",W.RED);V.badge(legend,"50% 部分满足",W.GOLD);V.badge(legend,"100% 满足",W.GREEN);V.badge(legend,"— 等待结算",W.MUTED)
	_note(matrix,"仅已启用需求进入矩阵与平均值。零服务也如实显示 0%；等待结算保持未知。")
	if float(p.count)<=0:_note(matrix,"当前没有居民；产生实际住户后显示各组覆盖。")

static func _population_need_detail(d,p:Dictionary,need:String) -> void:
	d.title.text="需求 · "+str(NEED_NAMES.get(need,need));var active=need in p.active_kinds
	d.subtitle.text=str(p.stamp)+(" · 当前已启用" if active else " · 当前未计入生活需求")
	var ratio=float(p.coverage_kinds.get(need,0));var known=p.count>0
	for row in p.rows:
		if float(row.count)>0 and not _need_known(d,row,need):known=false
	var summary=_group(d.body,"居民需求覆盖")
	_meter(summary,"满足率",ratio,1,"%.0f%%"%(100*ratio) if active and known else ("等待结算" if active and float(p.count)>0 else ("无居民" if active else "未启用")),_coverage_tone(ratio) if active and known else W.MUTED,"",Callable(),active and known)
	var sources={};var shot:Dictionary=d.app.shot;var units="人"
	if need in M.SERVICES:
		for link in shot.get("metrics",{}).get("service_links",[]):
			if str(link.get("kind",""))==need:sources[str(link.source)]=float(sources.get(str(link.source),0))+float(link.people)
	elif need=="energy":
		units="供电能力"
		for id in shot.get("metrics",{}).get("energy_sources",{}):sources[str(id)]=float(shot.metrics.energy_sources[id])
	else:
		units="单位 / 本周期"
		for receipt in shot.get("metrics",{}).get("allocation",[]):
			if str(receipt.get("resource",""))==need and str(receipt.get("consumer","")).begins_with("pop:"):
				var id=str(receipt.get("source",""));sources[id]=float(sources.get(id,0))+float(receipt.get("used",0))
	var graph=V.card(d.body,"服务入口 · 与居民的真实关系" if need in M.SERVICES else ("全城电网供给来源" if need=="energy" else "居民实际收到的物资"),"",need,W.INFO)
	var rows=[]
	for id in sources:rows.append({"id":str(id),"name":d.app.game.project_name(str(id)),"rate":float(sources[id])})
	_contributions(graph,rows,"rate",W.INFO,10000," "+units,d)
	if need in M.SERVICES:
		_note(graph,"共享池按最可达入口记录服务关系；入口人数不是这栋建筑独自承担的服务容量。")
		var pools=_group(d.body,"服务池 · 实际占用 / 有效容量")
		var found=false
		for pool in shot.get("metrics",{}).get("service_pools",{}).values():
			if str(pool.get("kind",""))!=need:continue
			found=true;var names=[]
			for member in pool.get("members",[]):names.append(d.app.game.project_name(str(member)))
			var capacity=float(pool.get("capacity",0));var assigned=maxf(0,capacity-float(pool.get("remaining",0)))
			_meter(pools," + ".join(names),assigned,capacity,"%.1f / %.1f 人"%[assigned,capacity],W.INFO,"按实际运行与规则计算的共享容量")
			var members=_fold(pools,"查看池内建筑 · %d 项"%pool.get("members",[]).size())
			for member in pool.get("members",[]):members.add_child(W.flat(W.button(d.app.game.project_name(str(member))+" ›",_source_action(d,str(member)))))
		if not found:_note(pools,"本周期没有有效的此类服务池。")
	elif need=="energy":
		_note(graph,"电力由全城共享电网分配；本图显示全城来源，账本没有记录某一电源到住户的独立连线。")
		var energy=shot.get("metrics",{});var requested=0.
		for value in energy.get("population_energy",{}).values():requested+=float(value)
		var supplied=requested*float(energy.get("energy_ratio",1))
		_meter(graph,"居民实际供电 / 申请",supplied,requested,"%.2f / %.2f"%[supplied,requested],W.INFO,"全城电网满足率应用于实际居民申请",Callable(),energy.has("population_energy") and energy.has("energy_ratio"))
	else:
		_note(graph,"按真实分配凭据汇总居民已收到的数量；单位为该次结算周期，不与月速率混用。")
	var records=_fold(graph,"完整来源数值 · %d 项"%rows.size())
	for item in rows:d._row(records,str(item.name),"%.3f %s"%[float(item.rate),units],_source_action(d,str(item.id)))
	var homes=_group(d.body,"各住区覆盖")
	for row in p.rows:
		var row_known=_need_known(d,row,need);var n=float(row.coverage.get(need,0))
		_meter(homes,str(row.name)+" · %.1f人"%float(row.count),n,1,"%.0f%%"%(100*n) if row_known and active else "—",_coverage_tone(n) if row_known and active else W.MUTED,"",Callable(),row_known and active)

static func render_time(d) -> void:
	preload("res://ui/beta/interface.gd").render_time(d)

static func render_policies(d) -> void:
	d.body.set_meta("snapshot_id",str(d.app.shot.get("snapshot_id","")))
	var g=d.app.game
	var selected:Dictionary=g.c.policies.get(str(d.detail_id),{})
	var current=not selected.is_empty() and str(g.s.block.policies.get(str(selected.get("slot","")),""))==str(d.detail_id)
	d.cost.text=("当前制度不重复收费" if current else "更换 %.0f 通货"%float(g.c.balance.get("policy_switch_price",0)))+" · 当前预算 %.0f"%float(g.s.currency)
	if d.subtopic=="":_policies_overview(d)
	elif d.subtopic=="preview":_policy_preview(d,str(d.detail_id))
	elif d.subtopic=="rules":_policy_rules(d,str(d.detail_id))
	else:d.subtopic="";render_policies(d)

static func _policy_summary(def:Dictionary) -> String:
	# Legacy economic descriptions use h; Beta keeps that field in economic months.
	return str(def.get("summary","")).replace("每小时","每来生月").replace("/h","/月")

static func _policies_overview(d) -> void:
	var g=d.app.game;d.title.text="城市制度";d.subtitle.text=_block_name(g)+" · 同一制度槽只采用一种方案"
	for pair in POLICY_SLOTS:
		var slot=str(pair[0]);var current_id=str(g.s.block.policies.get(slot,""));var group=V.card(d.body,str(pair[1]),"同槽互斥 · 只生效一种方案","policy",W.INFO)
		var choices=_grid(group,3)
		for raw_id in g.Rules.ids(g.c.policies):
			var id=str(raw_id);var def:Dictionary=g.c.policies[id]
			if str(def.get("slot",""))!=slot:continue
			var current=id==current_id;var box=_group(choices,"")
			box.get_parent().add_theme_stylebox_override("panel",W.box(W.SOFT if current else W.PANEL,W.GREEN if current else W.SOFT,10))
			V.badge(box,"● 当前采用" if current else "○ 可比较",W.GREEN if current else W.MUTED)
			box.add_child(W.label(str(def.name),19,W.TEXT));_note(box,_policy_summary(def))
			var button=W.button("查看现行制度" if current else "预览变化 ›",func():d.choose_policy(id));box.add_child(button);d.policy_buttons[id]=button
	d.footer.hide();d.primary.hide();d.secondary.show();d.secondary.text="返回"

static func _policy_preview(d,id:String) -> void:
	var g=d.app.game
	if not g.c.policies.has(id):_note(d.body,"找不到这项政策。");return
	var def:Dictionary=g.c.policies[id];var current_id=str(g.s.block.policies.get(str(def.slot),""));var current:Dictionary=g.c.policies.get(current_id,{})
	d.title.text="制度比较 · "+str(def.name);d.subtitle.text="同一城市下一周期：不更换 → 采用候选"
	var choices=_grid(d.body,2)
	_number(choices,"当前 · "+str(current.get("name","未设置")),"保留",_policy_summary(current),W.MUTED)
	_number(choices,"候选 · "+str(def.name),"采用",_policy_summary(def),W.INFO)
	var cost=0. if current_id==id else float(g.c.balance.policy_switch_price)
	var cash=_group(d.body,"一次性调整成本")
	_meter(cash,"调整后可用预算",maxf(0,float(g.s.currency)-cost),maxf(1,float(g.s.currency)),"%.1f → %.1f 通货"%[float(g.s.currency),float(g.s.currency)-cost],W.GOLD if g.s.currency<cost else W.INFO,"本次 %.1f 通货；持续收支另看预测。"%cost)
	if str(d.pending_policy)!=id:
		_note(d.body,"当前已生效；返回列表可比较其他方案。");d.footer.hide();return
	var prediction:Dictionary=d.policy_prediction
	if d.updating_policy or prediction.is_empty():_note(d.body,"正在计算当前城市下一周期影响…")
	elif prediction.get("ok",false):
		var before:Dictionary=d.app.planner.snapshot(g).get("metrics",{});var after:Dictionary=prediction.get("map_snapshot",{}).get("metrics",{})
		var comparison=_group(d.body,"不更换 / 更换后 · 同一预测周期")
		for pair in [["cashflow","净收支","/月"],["income","经营收入","/月"],["upkeep","维护支出","/月"],["energy_ratio","供电满足","%"],["housing","住房容量","人"],["jobs","岗位容量","人"]]:
			var key=str(pair[0]);var multiplier=100. if key=="energy_ratio" else 1.
			if before.get(key)==null or after.get(key)==null:
				_note(comparison,str(pair[1])+" · 当前缺少可比较的预测值");continue
			var a=float(before.get(key,0))*multiplier;var b=float(after.get(key,0))*multiplier
			if key!="cashflow" and is_equal_approx(a,b):continue
			var section=_group(comparison,str(pair[1]));var scale=maxf(absf(a),absf(b))
			_meter(section,"不更换",absf(a),scale,"%.2f%s"%[a,pair[2]],W.MUTED)
			_meter(section,"采用候选",absf(b),scale,"%.2f%s · %+.2f"%[b,pair[2],b-a],W.INFO)
		_note(comparison,"条形长度表示数值绝对量；正负号保留。只显示发生变化的容量项，不把预测当成已发生。")
	else:_note(d.body,str(prediction.get("error","当前无法生成预测。")))
	_nav(d.body,d,"完整规则与适用范围","rules",id)
	d.footer.show();d.primary.show();d.primary.pressed.connect(d.apply_policy);d.secondary.show();d.secondary.text="返回";d.update_policy_footer()

static func _policy_rules(d,id:String) -> void:
	var g=d.app.game
	if not g.c.policies.has(id):_note(d.body,"找不到这项政策。");return
	var def:Dictionary=g.c.policies[id];d.title.text="制度规则 · "+str(def.name);d.subtitle.text=_policy_summary(def)
	var rules:Array=def.get("rules",[])
	for item in rules:
		var box=_group(d.body,str(item.get("field",item.get("key",item.get("op","规则")))))
		var grid=_grid(box,3)
		_number(grid,"动作",str(item.get("op","")),str(item.get("mode","")))
		_number(grid,"数值",str(item.get("value","—")),"规则原始值")
		_number(grid,"作用对象",str(item.get("targets","self")),"阶段："+str(item.get("phase","")))
		_fold(box,"完整语法与条件").add_child(W.label(JSON.stringify(item,"  "),14,W.MUTED))
	if rules.is_empty():_note(d.body,"这项制度没有单独的规则条目。")
	d.footer.hide();d.primary.hide();d.secondary.show();d.secondary.text="返回"

static func _block_name(g) -> String:
	return str(g.s.get("block",{}).get("display_name","当前地区"))
