extends RefCounted
## 0.4 city actions on the existing map. All displayed state comes from Game snapshots/APIs.
const Beta=preload("res://ui/beta/interface.gd")
const W=preload("res://ui/widgets.gd")
const C=preload("res://ui/components031/components.gd")
const D=preload("res://ui/components031/design.gd")
const Art=preload("res://ui/map/art.gd")
const V=preload("res://ui/reset/desk_visuals.gd")
const RES_NAMES={"food":"粮食","material":"材料","goods":"制成品"}
const GOAL_NAMES={"food_ratio":"口粮满足","housed":"已安置居民","homeless":"待安置居民","medical":"医疗覆盖","energy_ratio":"电力满足","delivery_goods":"制成品交付","organization_links":"组织联系","adverse_exports":"逆势出口"}

static func _group(parent:Node,title:String="",bg:Color=W.PANEL) -> VBoxContainer:
	var content=C.group(parent,title,bg)
	var panel=content.get_parent()
	if panel is PanelContainer:
		panel.add_theme_stylebox_override("panel",W.box(bg,W.SOFT,10))
		var inner=content as VBoxContainer;inner.add_theme_constant_override("separation",8)
	if title!="" and content.get_child_count()>0:
		var heading=content.get_child(0) as Label
		if heading!=null:
			heading.add_theme_color_override("font_color",W.ACCENT);heading.add_theme_font_override("font",W._semibold())
		if title in ["主要收益","主要损失","升级说明"]:
			var rule=ColorRect.new();rule.color=Color(W.ACCENT,.32);rule.custom_minimum_size.y=1;rule.mouse_filter=Control.MOUSE_FILTER_IGNORE;content.add_child(rule);content.move_child(rule,1)
	return content

static func render(d,section:String="home") -> void:
	for key in ["trade_confirm","trade_review","trade_cancel"]:
		if d.primary.has_meta(key):d.primary.remove_meta(key)
	match section:
		"home":_home(d)
		"development":_development(d)
		"construction":_construction(d)
		"permits":_permit_wallet(d)
		"commissions":render_commissions(d)
		"market":_market(d)
		"block":Beta.render_ending(d)
		"settlement":Beta.render_settlement(d)
		_: 
			var bits=section.split(":")
			if bits[0]=="upgrade" and bits.size()>1:_upgrade(d,bits[1])
			elif bits[0]=="preview" and bits.size()>2:_upgrade_preview(d,bits[1],bits[2])
			elif bits[0]=="commission" and bits.size()>1:_commission_detail(d,bits[1])
			elif bits[0]=="resource" and bits.size()>1:_market(d,bits[1])
			elif bits[0]=="permit" and bits.size()>1:_permit_hosts(d,bits[1])
			elif bits[0]=="permits" and bits.size()>1:_permits_for(d,bits[1])
			elif bits[0]=="trade" and bits.size()>1:_trade_detail(d,bits[1])
			elif bits[0]=="tradeconfirm":_trade_confirm(d)
			elif bits[0]=="order" and bits.size()>1:_order_detail(d,bits[1])
			elif bits[0]=="tradeentry" and bits.size()>1:_trade_entry(d,bits[1])
			elif bits[0]=="tradehistory":_trade_history(d,int(bits[1]) if bits.size()>1 else 0)
			elif bits[0]=="blockgoal":_block_goal(d)
			else:_home(d)

static func _home(d) -> void:
	var g=d.app.game;d.title.text="城市发展";d.subtitle.text="选择一处建筑的未来，或调配这座城市的建设与外贸。"
	var stats=g.s.get("metrics",{});var metrics=V.grid(d.body,3)
	V.metric(metrics,"population","居民","%.0f"%float(stats.get("population",0)),"已结算城市人口")
	V.metric(metrics,"currency","经营收支","%+.1f"%float(stats.get("cashflow",0)),"通货 / 来生月")
	V.metric(metrics,"build","共享施工能力","%.1f"%float(g.Construction.capacity(g)),"CP / 来生月")
	var links=V.grid(d.body,3)
	for pair in [["development","建筑成长","build","从现有建筑选择专业方向。"],["construction","施工队列","clock","安排共享能力与工程优先级。"],["permits","部署副本","bonus","把已获得的方案装到合适宿主。"],["commissions","主题与奖励","world","发展主题、事件与待领奖励。"],["market","市场与订单","goods","报价、限价订单和实际成交。"],["block","城市尾声","home","命名、定型与本局成果。"]]:
		var item=pair;var card=V.card(links,item[1],item[3],item[2]);card.add_child(W.button("打开 ›",func():d.navigate("city040",item[0])))
	var starts=V.card(d.body,"新建起点","专业建筑从三个起点逐级成长。","build");var row=V.grid(starts,3)
	for id in ["homestead","neighborhood","workshop"]:
		var def_id=str(id);var def=g.c.projects[def_id];V.building(row,def_id,str(def.name),"Tier 1 · "+str(def.get("description","")))
	starts.add_child(W.button("选择地块并建设",func():d.close();d.app.toggle_palette("categories")))

static func _project_ids(g) -> Array:
	var result=[]
	for id in g.playable_ids():
		if g.s.projects.has(id) and not g.s.projects[id].get("special",false):result.append(str(id))
	result.sort()
	return result

static func _development(d) -> void:
	var g=d.app.game;d.title.text="建筑成长";d.subtitle.text="每次升级改变用途与依赖；已有身份、位置、运营者与库存保留。"
	var nav=W.hbox(d.body,8);nav.add_child(W.button("施工队列 ›",func():d.navigate("city040","construction")));nav.add_child(W.button("部署副本 ›",func():d.navigate("city040","permits")))
	var ids=_project_ids(g)
	if ids.is_empty():V.card(d.body,"从一处起点开始","先建成农居、邻里中心或作坊，再选择专业方向。","build");return
	var grid=V.grid(d.body,2)
	for id in ids:
		var project_id=str(id);var p=g.s.projects[id];var def=g.c.projects.get(str(p.def),{});var tier=int(p.get("tier",def.get("tier",1)))
		var card=V.building(grid,str(p.def),g.project_name(id),str(g.c.operators.get(str(p.operator_org),{}).get("name",p.operator_org)))
		V.steps(card,["起点","专业化","都市设施"],tier-1)
		var status="已暂停" if p.get("paused",false) else ("升级施工中" if not p.get("upgrade_job",{}).is_empty() else ("新建施工中" if p.status=="construction" else "运营中"))
		V.badge(card,status,W.GOLD if status!="运营中" else W.GREEN)
		_abilities(card,def)
		var choices=g.Development.choices(g,id)
		if choices.is_empty():
			card.add_child(W.label("已到该分支的最高阶段。",13,W.MUTED))
		else:card.add_child(W.button("选择专业方向 · %d ›"%choices.size(),func():d.push_section("upgrade",project_id),p.status!="running" or not p.get("upgrade_job",{}).is_empty()))
		card.add_child(W.flat(W.button("查看建筑档案 ›",func():_building_archive(d,project_id))))

static func _building_archive(d,id:String) -> void:
	load("res://ui/reset/building_archive.gd").open(d.app,id)

static func _locate_building(d,id:String) -> void:
	if not d.app.game.s.projects.has(id):d.app.message("建筑已不存在。");return
	d.locate(id);d.app.mode="inspect";d.app.selection=[str(d.app.game.s.projects[id].tile)];d.close()

static func _upgrade(d,id:String) -> void:
	var g=d.app.game
	if not g.s.projects.has(id):V.card(d.body,"建筑已不存在","返回城市选择另一处建筑。","build");return
	var p=g.s.projects[id];var def=g.c.projects.get(str(p.def),{});var choices=g.Development.choices(g,id)
	d.title.text=g.project_name(id)+" · 选择专业方向";d.subtitle.text="先比较用途变化，再预览费用与完整能力差分。"
	var current=V.building(d.body,str(p.def),"当前 · "+g.project_name(id),str(g.c.operators.get(str(p.operator_org),{}).get("name",p.operator_org)))
	V.steps(current,["起点","专业化","都市设施"],int(p.get("tier",1))-1)
	if choices.is_empty():V.badge(current,"已到分支最高阶段");return
	var candidates=V.grid(d.body,mini(3,choices.size()))
	for value in choices:
		var choice=str(value);var target=g.c.projects.get(choice,{});var card=V.building(candidates,choice,str(target.get("name",choice)),"Tier %d · %s"%[int(target.get("tier",1)),Art.stage_name(int(target.get("tier",1)))])
		_abilities(card,target)
		var preview=g.Development.preview(g,id,choice);_compact_changes(card,preview)
		var costs=W.hbox(card,8);V.icon(costs,"currency",18,W.GOLD);costs.add_child(W.label("%.0f"%float(target.get("cost",0)),16,W.GOLD));V.icon(costs,"material",18,W.MUTED);costs.add_child(W.label("%.0f"%float(target.get("material",0)),16,W.MUTED));V.icon(costs,"build",18,W.MUTED);costs.add_child(W.label("%.1f CP"%float(target.get("construction_required",0)),16,W.MUTED))
		card.add_child(W.button("比较升级前后 ›",func():d.push_section("preview",id+":"+choice),p.status!="running" or not p.get("upgrade_job",{}).is_empty()))

static func _upgrade_preview(d,id:String,node:String) -> void:
	var g=d.app.game;var preview=g.Development.preview(g,id,node)
	d.title.text="确认升级";d.subtitle.text="基础能力前后对照；运营者、既有方案和建筑身份保留。"
	if not preview.get("ok",false):V.card(d.body,"无法升级",str(preview.get("error","升级方向不可用。")),"warning",W.RED);return
	var current:Dictionary=g.c.projects.get(str(g.s.projects[id].def),{});var target:Dictionary=g.c.projects.get(node,{})
	V.steps(d.body,["选择方向","比较能力","加入施工队列"],1)
	var comparison=V.grid(d.body,2)
	V.building(comparison,str(current.get("id",g.s.projects[id].def)),"现在 · "+str(current.get("name","")),Art.stage_name(int(current.get("tier",1))))
	V.building(comparison,node,"升级后 · "+str(target.get("name",node)),Art.stage_name(int(target.get("tier",1))))
	var changes:Array=[]
	changes.append_array(preview.get("gains",[]));changes.append_array(preview.get("losses",[]))
	var differences=V.card(d.body,"能力变化","定义中的基础能力；规则与运行条件会影响最终实际产出。","compare")
	var table=_table(differences,["能力","现在","升级后","变化"])
	for row in changes:
		var stat=str(row.get("stat",""));var before=float(row.get("before",0));var after=float(row.get("after",0));var delta=after-before
		var name=W.hbox(table,8);V.icon(name,_stat_icon(stat),20,W.MUTED);name.add_child(W.label(_stat_name(stat),14))
		_cell(table,_amount(before)+_stat_unit(stat));_cell(table,_amount(after)+_stat_unit(stat));_cell(table,("+" if delta>0 else "")+_amount(delta),_stat_color(stat,delta))
	if changes.is_empty():differences.add_child(W.label("没有基础数值变化。",14,W.MUTED))
	var costs=V.grid(d.body,4)
	V.metric(costs,"currency","立即支付","%.0f"%float(preview.cost),"持有 %.0f 通货"%float(g.s.currency),W.GOLD if float(g.s.currency)>=float(preview.cost) else W.RED)
	V.metric(costs,"material","施工材料","%.0f"%float(preview.material),"随工程进度消耗")
	V.metric(costs,"build","所需施工量","%.1f CP"%float(preview.construction_required),"与其他工程共享能力")
	var capacity=float(g.Construction.capacity(g));V.metric(costs,"clock","基础工期","%.2f 月"%float(preview.eta) if capacity>0 else "等待能力","独占施工能力且供给充足")
	var slot_name={"Operations":"运营","Production":"生产","Logistics":"物流","Flex":"弹性"}.get(str(preview.get("slot","")),str(preview.get("slot","")))
	var note=V.card(d.body,"施工期间","工期会受共享队列、材料与供电影响。","build")
	V.badge(note,"运营效率 %.0f%% · 完工解锁%s槽"%[float(preview.get("operation_factor",1))*100,slot_name],W.GOLD)
	var p=g.s.projects.get(id,{})
	d.footer.show();d.footer_text.text="支付 %.0f 通货，开始将「%s」升级为「%s」。"%[float(preview.cost),str(current.get("name","")),str(target.get("name",node))]
	d.primary.show();d.primary.text="支付并开始升级";d.primary.disabled=str(p.get("status",""))!="running" or not p.get("upgrade_job",{}).is_empty() or float(g.s.currency)<float(preview.cost)
	d.primary.pressed.connect(func():
		var error=g.Development.start_upgrade(g,id,node)
		if error!="":d.app.message(error)
		else:d.app.save_game();d.app.refresh();d.navigate("city040","construction");d.app.message(g.project_name(id)+"已加入施工队列。")
	)
	d.secondary.show();d.secondary.text="返回方向选择"

static func _abilities(parent:Node,definition:Dictionary) -> void:
	var stats:Dictionary=definition.get("stats",{});var visible=[]
	for key in ["housing","output.food","output.material","output.goods","output.energy","service.medical","service.education","service.commercial","service.entertainment","trade_capacity","jobs"]:
		if float(stats.get(key,0))>0:visible.append(key)
	var grid=V.grid(parent,2)
	for key in visible.slice(0,4):
		var row=W.hbox(grid,8);row.size_flags_horizontal=Control.SIZE_EXPAND_FILL;V.icon(row,_stat_icon(key),23,W.ACCENT)
		var value=W.vbox(row,3);value.size_flags_horizontal=Control.SIZE_EXPAND_FILL;value.add_child(W.label(_amount(float(stats[key]))+_stat_unit(key),18,W.ACCENT,false));value.add_child(W.label(_stat_name(key),13,W.MUTED))
	if visible.is_empty():parent.add_child(W.label("基础用途以建筑规则为准。",14,W.MUTED))
	parent.add_child(W.label("基础定义能力 · 最终能力受运营与已装方案影响",13,W.MUTED))

static func _compact_changes(parent:Node,preview:Dictionary) -> void:
	var rows:Array=[];rows.append_array(preview.get("gains",[]));rows.append_array(preview.get("losses",[]))
	var meaningful=[]
	for row in rows:
		var stat=str(row.get("stat",""))
		if stat in ["housing","jobs","energy_demand","maintenance","pollution","trade_capacity"] or stat.begins_with("output.") or stat.begins_with("input.") or stat.begins_with("service."):meaningful.append(row)
	for row in meaningful.slice(0,3):
		var stat=str(row.stat);var change=W.hbox(parent,8);V.icon(change,_stat_icon(stat),20,W.MUTED)
		change.add_child(W.label(_stat_name(stat),14,W.MUTED))
		var value=W.label("%s → %s"%[_amount(float(row.before)),_amount(float(row.after))],15,_stat_color(stat,float(row.after)-float(row.before)),false)
		value.size_flags_horizontal=Control.SIZE_SHRINK_END;change.add_child(value)

static func _table(parent:Node,headers:Array) -> GridContainer:
	var table=V.grid(parent,headers.size());table.add_theme_constant_override("h_separation",18);table.add_theme_constant_override("v_separation",12)
	for header in headers:_cell(table,str(header),W.MUTED)
	return table

static func _cell(parent:Node,value:String,tint:Color=W.TEXT) -> Label:
	var label=W.label(value,14,tint);label.size_flags_horizontal=Control.SIZE_EXPAND_FILL;label.size_flags_vertical=Control.SIZE_SHRINK_CENTER;parent.add_child(label);return label

static func _amount(value:float) -> String:
	return "%.0f"%value if is_equal_approx(value,roundf(value)) else "%.2f"%value

static func _lower_is_better(stat:String) -> bool:
	return stat.begins_with("input.") or stat in ["energy_demand","maintenance","pollution","exposure_multiplier","shortage_multiplier","population_maintenance","distance_cost"]

static func _stat_color(stat:String,delta:float) -> Color:
	if _lower_is_better(stat):return W.GREEN if delta<0 else W.GOLD
	if stat in ["housing","jobs","efficiency","personnel_mult","trade_capacity","construction_capacity","service_range"] or stat.begins_with("output.") or stat.begins_with("storage.") or stat.begins_with("service."):return W.GREEN if delta>0 else W.GOLD
	return W.MUTED

static func _stat_icon(stat:String) -> String:
	if "." in stat:
		var part=stat.split(".")[1]
		if part in ["food","material","goods","energy","medical","education","commercial","entertainment"]:return part
	return {"housing":"housing","jobs":"jobs","energy_demand":"energy","maintenance":"currency","population_maintenance":"currency","pollution":"pollution","trade_capacity":"logistics","construction_capacity":"build","efficiency":"operations","personnel_mult":"jobs","service_range":"focus","exposure_multiplier":"pollution","shortage_multiplier":"warning"}.get(stat,"info")

static func _stat_name(stat:String) -> String:
	return {"housing":"住房容量","jobs":"岗位","service.medical":"医疗容量","service.education":"教育容量","service.commercial":"商业容量","service.entertainment":"休闲容量","output.food":"粮食产出","output.material":"材料产出","output.goods":"制成品产出","output.energy":"供电能力","input.food":"粮食投入","input.material":"材料投入","input.goods":"制成品投入","storage.food":"粮食仓容","storage.material":"材料仓容","storage.goods":"制成品仓容","energy_demand":"电力需求","maintenance":"维护费","service_range":"服务范围","service_decay":"服务衰减","employment_range":"就业范围","employment_decay":"就业衰减","pollution":"环境负担","pollution_range":"污染范围","pollution_decay":"污染衰减","efficiency":"基础效率","personnel_mult":"人员效率","trade_capacity":"货运能力基值","construction_capacity":"施工能力","allocation_priority":"分配优先级","distance_cost":"距离成本","population_maintenance":"人口维护","population_food":"人口口粮需求","population_material":"人口材料需求","population_goods":"人口用品需求","population_energy":"人口电力需求","exposure_multiplier":"污染暴露系数","shortage_multiplier":"短缺敏感系数","jit_cap":"即时调配上限","heat_discount":"余热节能","heat_service":"余热服务","heat_cap":"余热上限","synergy.same_industrial":"同组织工业协同","utilization_exponent":"利用率指数"}.get(stat,stat)

static func _stat_unit(stat:String) -> String:
	if stat.begins_with("output.") or stat.begins_with("input."):return " 单位/月" if not stat.ends_with("energy") else " 单位"
	if stat.begins_with("storage.") or stat=="energy_demand":return " 单位"
	if stat=="housing" or stat=="jobs" or stat.begins_with("service."):return " 名额"
	if stat=="maintenance":return " 通货/月"
	if stat=="construction_capacity":return " CP/月"
	if stat in ["service_range","employment_range","pollution_range"]:return " 格"
	return ""

static func _stat_rows(parent,g,rows:Array,gain:bool) -> void:
	var stat_names={"housing":"住房位置","jobs":"岗位","service.medical":"医疗","service.education":"教育","service.commercial":"购物","service.entertainment":"休闲","output.food":"粮食产出","output.material":"材料产出","output.goods":"制成品产出","output.energy":"电力产出","input.food":"粮食消耗","input.material":"材料消耗","input.goods":"制成品消耗","storage.food":"粮食仓容","storage.material":"材料仓容","storage.goods":"制成品仓容","energy_demand":"电力需求","maintenance":"维护费","service_range":"服务范围","pollution":"环境负担","efficiency":"基础效率"}
	var shown=0
	for row in rows:
		var stat=str(row.get("stat",""));var before=float(row.get("before",0));var after=float(row.get("after",0))
		if is_equal_approx(before,after):continue
		var lower_is_better=stat.begins_with("input.") or stat in ["energy_demand","maintenance","pollution"]
		var improved=after<before if lower_is_better else after>before
		if improved!=gain:continue
		var label=str(stat_names.get(stat,stat.replace("output.","").replace("input.","") ))
		parent.add_child(W.label(label+"  %s → %s"%[_format_value(row.get("before",0)),_format_value(row.get("after",0))],15,W.GREEN if improved else W.GOLD));shown+=1
	if shown==0:parent.add_child(W.label("没有明显的数值增减。",14,W.MUTED))

static func _format_value(value) -> String:
	return "%.2f"%float(value) if value is float else str(value)

static func _construction(d) -> void:
	var g=d.app.game;var rows=g.Construction.queue(g);var capacity=float(g.Construction.capacity(g));var energy=clampf(float(g.s.metrics.get("energy_ratio",1)),0,1)
	d.title.text="施工队列";d.subtitle.text="队列按优先级分配同一份施工能力；每推进一段工程，同时消耗对应材料。"
	var total_work=0.
	for row in rows:total_work+=float(row.work)
	var metrics=V.grid(d.body,3);V.metric(metrics,"build","共享施工能力","%.1f CP/月"%capacity,"全城基础能力");V.metric(metrics,"energy","当前供电","%.0f%%"%(energy*100),"影响实际施工速度",W.GREEN if energy>.95 else W.GOLD,energy);V.metric(metrics,"build","待完成工程","%.1f CP"%total_work,"%d 个项目共享队列"%rows.size())
	var last:Dictionary=g.s.block.get("construction_last",{})
	if float(last.get("hours",0))>0:
		var actual=V.card(d.body,"最近一次施工结算","已提交的实际推进量；工程材料同步扣除。","build")
		var settled=W.hbox(actual,12);V.icon(settled,"build",24);settled.add_child(W.label("%.3f CP"%float(last.get("cp",0)),20,W.ACCENT));V.icon(settled,"material",24,W.MUTED);settled.add_child(W.label("%.3f 材料"%float(last.get("material",0)),20,W.MUTED));settled.add_child(W.label("覆盖 %.4f 来生月"%float(last.hours),14,W.MUTED))
	if rows.is_empty():V.card(d.body,"施工队列空闲","新建或升级后，工程会出现在这里。","build");return
	for index in rows.size():
		var item:Dictionary=rows[index];var id=str(item.id);var p:Dictionary=g.s.projects[id]
		var target=str(p.get("upgrade_choice","")) if item.upgrade else str(p.def)
		var card=V.card(d.body,"%02d  %s"%[index+1,str(item.name)],"升级" if item.upgrade else "新建","build")
		var line=W.hbox(card,16);line.add_child(Art.picture(target if target!="" else str(p.def),Vector2(126,96)))
		var progress=W.vbox(line,8);progress.size_flags_horizontal=Control.SIZE_EXPAND_FILL
		V.meter(progress,"工程进度",maxf(0,float(item.total)-float(item.work)),float(item.total),"CP",W.GOLD if item.paused else W.ACCENT,"build")
		var status="已暂停" if item.paused else ("等待施工能力" if capacity<=.000001 else ("等待材料" if float(item.material)>0 and g.inventory_total("material")<=.000001 else ("供电限制" if energy<.999 else "按队列分配施工能力")))
		V.badge(progress,status,W.GOLD if status!="按队列分配施工能力" else W.MUTED)
		progress.add_child(W.label("剩余 %.1f CP · 还需 %.1f 材料"%[float(item.work),float(item.material)],14,W.MUTED))
		var actions=W.hbox(progress,8)
		for value in [-1,0,1]:
			var priority=int(value);var b=W.button(_priority_name(priority),func():
				var error=g.Construction.set_priority(g,id,priority)
				if error!="":d.app.message(error)
				else:d.app.save_game();d.app.refresh();d.render(true)
			);b.toggle_mode=true;b.button_pressed=int(item.priority)==priority;actions.add_child(b)
		var locate=W.button("地图定位 ›",func():_locate_building(d,id));locate.size_flags_horizontal=Control.SIZE_SHRINK_END;actions.add_child(locate)

static func _priority_name(value:int) -> String:
	return {-1:"低",0:"普通",1:"优先"}.get(value,"普通")

static func _permits(d) -> void:
	var g=d.app.game;d.title.text="部署许可";d.subtitle.text="每份副本只提供一次安装机会；卸载后不会退回。"
	var ids=_project_ids(g)
	if ids.is_empty():d.body.add_child(W.label("先建成一处建筑，再查看它可安装的副本。",16,W.MUTED));return
	for id in ids:
		var p=g.s.projects[id];var card=_group(d.body,g.project_name(id)+" · "+Art.stage_name(int(p.get("tier",1))))
		var slots=[]
		for slot in p.get("slots",{}):slots.append(str(slot))
		card.add_child(W.label(("%d个槽位"%slots.size())+" · "+g.c.operators.get(str(p.get("operator_org","")),{}).get("name",""),14,W.MUTED))
		card.add_child(W.button("查看可安装许可",func():d.push_section("permits",id),slots.is_empty()))

static func _permit_wallet(d) -> void:
	var g=d.app.game;d.title.text="部署副本";d.subtitle.text="先选一张方案，再选宿主与空槽；预览确认后才消耗一份副本。"
	V.steps(d.body,["选择副本","选择宿主","预览安装"],0)
	var wallet:Dictionary=g.s.block.get("bonus_wallet",{});var ids:Array=wallet.keys();ids.sort();var count=0
	var grid=V.grid(d.body,3)
	for value in ids:
		var bid=str(value);var license:Dictionary=wallet[bid];var charges=int(license.get("charges",0))
		if charges<=0:continue
		count+=1
		var data:Dictionary=g.Bonus.card(g,bid);var scope=str(data.get("scope","building"))
		var grade=str(data.get("grade","基础"));var scope_name={"building":"建筑","organization":"组织","block":"街区"}.get(scope,"建筑")
		var button=C.bonus(grid,g,bid,func():d.push_section("permit",bid),"offer",{"instance_status":"库存 ×%d · %s"%[charges,grade],"provider_name":scope_name+"部署 · 选择宿主 ›"})
		button.set_meta("wallet_bonus_id",bid)
	if count==0:V.card(d.body,"还没有待部署副本","年度、安居人口和主题奖励会提供新的方案。","bonus")
	var rules=V.fold(d.body,"副本与蓝图规则","同宿主首份 100%，追加每份 33⅓%；卸下不返还。")
	rules.add_child(W.label("蓝图升级影响本 Block 已安装、库存及以后取得的同名副本。安装仍须满足宿主、槽位、资格与结构性收益检查。",14,W.MUTED))

static func _permit_hosts(d,bid:String) -> void:
	var g=d.app.game
	if not g.c.bonuses.has(bid):V.card(d.body,"方案已不存在","返回库存选择另一张方案。","bonus");return
	var data:Dictionary=g.Bonus.card(g,bid);var scope=str(data.get("scope","building"));var count=int(data.get("copies",0))
	d.title.text=str(data.get("name",bid))+" · 选择宿主";d.subtitle.text="先选合适的空槽或组织，再查看实际同名合成变化。"
	V.steps(d.body,["选择副本","选择宿主","预览安装"],1)
	var heading=V.grid(d.body,2)
	C.bonus(heading,g,bid,Callable(),"offer",{"instance_status":"库存 ×%d · %s"%[count,str(data.get("grade","基础"))],"provider_name":"安装消耗 1 份；卸下不返还"})
	var rules=V.card(heading,"部署范围",{"building":"建筑的已解锁空槽","organization":"指定运营组织","block":"当前街区"}.get(scope,scope),"bonus")
	V.badge(rules,str(data.get("preview_reason","")),W.GREEN if data.get("deployable_now",false) else W.GOLD)
	rules.add_child(W.label(str(data.get("stacking_note","首份 100%，追加每份 33⅓%。")),14,W.MUTED))
	if count<=0:V.card(d.body,"副本已用完","已安装方案与蓝图仍保留；新的奖励可增加库存。","bonus");return
	var grid=V.grid(d.body,2);var eligible=0
	if scope=="building":
		for id in _project_ids(g):
			var host=str(id);var p:Dictionary=g.s.projects[id];var slots:Array=p.get("slots",{}).keys();slots.sort();var free=[]
			for value in slots:
				var slot=str(value)
				if str(p.slots[slot])=="":free.append(slot)
			if free.is_empty():continue
			var card=V.building(grid,str(p.def),g.project_name(host),str(g.c.operators.get(str(p.operator_org),{}).get("name",p.operator_org)))
			for value in free:
				var slot=str(value);var reason=str(g.bonus_eligible(bid,"building",host,slot));var line=W.hbox(card,10)
				C.socket_symbol(line,g.slot_type(host,slot),28)
				var words=W.vbox(line,4);words.size_flags_horizontal=Control.SIZE_EXPAND_FILL;words.add_child(W.label(g.slot_label(host,slot),16))
				if reason!="":words.add_child(W.label(reason,13,W.MUTED))
				else:eligible+=1;words.add_child(W.label("空槽 · 可部署",13,W.GREEN))
				var button=W.button("安装预览 ›",func():Beta.install_preview(d.app,bid,"building",host,slot),reason!="")
				button.set_meta("permit_host",host);button.set_meta("permit_slot",slot);button.size_flags_horizontal=Control.SIZE_SHRINK_END;line.add_child(button)
	elif scope=="organization":
		var ids:Array=g.s.get("organizations",{}).keys();ids.sort()
		for value in ids:
			var host=str(value);var reason=str(g.bonus_eligible(bid,"organization",host));var definition:Dictionary=g.c.operators.get(host,{})
			var card=V.card(grid,str(definition.get("name",host)),str(definition.get("summary","")),"org_"+host)
			var operated=0
			for id in _project_ids(g):
				if str(g.s.projects[id].get("operator_org",""))==host:operated+=1
			V.badge(card,"经营 %d 处建筑"%operated,W.MUTED)
			if reason=="":eligible+=1
			else:card.add_child(W.label(reason,14,W.MUTED))
			var button=W.button("安装预览 ›",func():Beta.install_preview(d.app,bid,"organization",host,""),reason!="");button.set_meta("permit_host",host);card.add_child(button)
	elif scope=="block":
		var host=str(g.s.block.get("id",""));var reason=str(g.bonus_eligible(bid,"block",host))
		var card=V.card(grid,str(g.s.block.get("display_name","当前街区")),"作用于当前 Block，保留已安装来源记录。","home")
		if reason=="":eligible+=1
		else:card.add_child(W.label(reason,14,W.MUTED))
		var button=W.button("安装预览 ›",func():Beta.install_preview(d.app,bid,"block",host,""),reason!="");button.set_meta("permit_host",host);card.add_child(button)
	if eligible==0:V.card(d.body,"当前没有合适的空槽或宿主","先查看建筑成长和已安装方案；库存副本会保留。","warning",W.GOLD)
	var details=V.fold(d.body,"完整方案规则",bid+" · 定义与 Rule 来源")
	details.add_child(W.label(str(g.c.bonuses[bid].get("summary","")),15))
	for rule in g.c.bonuses[bid].get("rules",[]):details.add_child(W.label(JSON.stringify(rule),13,W.MUTED))

static func _permits_for(d,id:String) -> void:
	var g=d.app.game
	if not g.s.projects.has(id):V.card(d.body,"建筑已不存在","返回地图选择另一处建筑。","build");return
	var p:Dictionary=g.s.projects[id];d.title.text=g.project_name(id)+" · 部署副本";d.subtitle.text="查看实际槽型，先预览，再确认消耗一份副本。"
	V.building(d.body,str(p.def),g.project_name(id),str(g.c.operators.get(str(p.operator_org),{}).get("name",p.operator_org)))
	var wallet:Dictionary=g.s.block.get("bonus_wallet",{});var ids:Array=wallet.keys();ids.sort();var slots:Array=p.get("slots",{}).keys();slots.sort();var empty_count=0
	for value in slots:
		var slot=str(value);var iid=str(p.slots[slot]);var type=g.slot_type(id,slot);var box=V.card(d.body,g.slot_label(id,slot),"空槽" if iid=="" else "已安装","bonus")
		var icon_row=W.hbox(box,8);C.socket_symbol(icon_row,type,32);V.badge(icon_row,D.slot_label(type),W.MUTED)
		if iid!="":
			var installed:Dictionary=g.s.bonus_instances.get(iid,{});var bid=str(installed.get("bonus_id",""))
			if g.c.bonuses.has(bid):C.bonus(box,g,bid,func():_building_archive(d,id),"installed",{"instance_status":"已启用" if installed.get("active",false) else "已停用"})
			continue
		empty_count+=1
		var cards=V.grid(box,3);var eligible=0
		for key in ids:
			var bid=str(key)
			if int(wallet[bid].get("charges",0))<=0 or str(g.bonus_eligible(bid,"building",id,slot))!="":continue
			eligible+=1
			var button=C.bonus(cards,g,bid,func():Beta.install_preview(d.app,bid,"building",id,slot),"offer",{"instance_status":"库存 ×%d"%int(wallet[bid].get("charges",0)),"provider_name":"安装预览 ›"})
			button.set_meta("permit_host",id);button.set_meta("permit_slot",slot)
		if eligible==0:box.add_child(W.label("钱包中没有当前可装入此槽的副本；资格、地块条件与结构收益均需满足。",14,W.MUTED))
	if empty_count==0:V.card(d.body,"当前没有空槽","升级可以解锁下一阶段槽位。已装方案管理仍在建筑档案。","bonus")
	d.body.add_child(W.button("打开建筑档案 ›",func():_building_archive(d,id)))

static func render_commissions(d) -> void:
	Beta.render_themes(d)
	_block_management(d)

static func _reward_choice(d,choice:Dictionary) -> void:
	Beta.reward(d,choice)

static func _block_goal_card(d) -> void:
	var g=d.app.game;var card=_group(d.body,"街区目标")
	var rows=g.Progress.checklist(g)
	for row in rows:
		var item=row;card.add_child(W.label(("✓ " if bool(row.get("done",false)) else "○ ")+str(row.get("name",""))+" · "+str(row.get("value","")),14,W.GREEN if row.get("done",false) else W.MUTED))
	card.add_child(W.button("目标条件与判定",func():d.push_section("blockgoal")))
	if g.s.get("city_goal",{}).get("completed",false):card.add_child(W.label("街区目标已达成；你可以继续经营这座城市。",14,W.GREEN))

static func _block_goal(d) -> void:
	var g=d.app.game;var goal:Dictionary=g.s.get("city_goal",{});var route=str(goal.get("route","community"));var def=g.c.data.get("city_goals",{}).get(route,{})
	d.title.text="街区目标 · "+str(def.get("name","当前城市成果"));d.subtitle.text="目标与历史分开呈现，达成后仍可继续经营。"
	d.body.add_child(W.label(str(def.get("description","")),16))
	for row in g.Progress.checklist(g):d.body.add_child(W.label(("✓ " if row.get("done",false) else "○ ")+str(row.name)+" · "+str(row.value),16,W.GREEN if row.get("done",false) else W.MUTED))
	var detail=_group(d.body,"本目标条件")
	for row in g.Progress.requirements(g):detail.add_child(W.label(("✓ " if row.get("done",false) else "○ ")+str(row.name)+" · "+str(row.value),15,W.GREEN if row.get("done",false) else W.MUTED))
	if route=="delivery":detail.add_child(W.primary(W.button("交付20材料与12制成品",func():
		var error=g.Progress.deliver(g)
		if error!="":d.app.message(error)
		else:d.app.save_game();d.app.refresh();d.render(false)
	)))
	var history=_group(d.body,"街区目标历史")
	var rows=g.s.get("civic032",{}).get("task_history",[])
	var found=false
	for row in rows:
		if str(row.get("id",""))=="route:"+route:found=true;history.add_child(W.label(_status_name(str(row.get("status","")))+" · "+str(row.get("h",""))+"月",14,W.MUTED))
	if not found:history.add_child(W.label("街区目标达成后会在这里留下记录。",14,W.MUTED))

static func _commission_detail(d,id:String) -> void:
	var g=d.app.game;var def=_commission_def(g,id)
	d.title.text=str(def.get("name",id))+" · 委托条件";d.subtitle.text="条件在此完整查看；主任务栏只显示进度。"
	d.body.add_child(W.label(str(def.get("help",def.get("description",""))),16))
	var goals=_group(d.body,"完成条件")
	for goal in def.get("goals",[]):
		var metric=str(goal.get("metric",""));var text=str(GOAL_NAMES.get(metric,metric.replace("_"," ")))
		if goal.has("min"):text+=" · 至少 "+_format_value(goal.min)
		if goal.has("max"):text+=" · 至多 "+_format_value(goal.max)
		if goal.has("duration"):text+=" · 持续 %.1f月"%float(goal.duration)
		goals.add_child(W.label(text,15,W.MUTED))
	var fail=def.get("fail_conditions",{})
	if not fail.is_empty():
		var box=_group(d.body,"失败条件")
		for key in fail:box.add_child(W.label(str(GOAL_NAMES.get(str(key),str(key)))+" · "+_format_value(fail[key]),14,W.GOLD))
	var state=g.s.block.get("commissions",{});var active=state.get("active",{})
	if str(active.get("id",""))==id:
		goals.add_child(W.bar(_commission_progress(active,def)));goals.add_child(W.label(_progress_summary(active,def),15,W.GREEN))
	else:goals.add_child(W.label("领域："+str(D.domain(str(def.get("domain","community"))).label)+" · 奖励类型：部署许可",14,W.MUTED))

static func _commission_def(g,id:String) -> Dictionary:
	var defs=g.c.v040.get("commissions",[])
	if defs is Dictionary:return defs.get(id,{})
	for def in defs:
		if str(def.get("id",""))==id:return def
	return {}

static func _commission_goals(active:Dictionary,def:Dictionary) -> Array:
	var goals=active.get("goals",[])
	return goals if goals is Array and not goals.is_empty() else def.get("goals",[])

static func _commission_progress(active:Dictionary,def:Dictionary) -> float:
	var goals=_commission_goals(active,def)
	if goals.is_empty():return 0
	var now:Dictionary=active.get("progress_now",{});var elapsed:Dictionary=active.get("goal_progress",{});var sum=0.
	for i in goals.size():
		var goal:Dictionary=goals[i];var key=str(i);var metric=str(goal.get("metric",""))
		var current=float(now.get(key,active.get("delivered_goods",0) if metric=="delivery_goods" else 0))
		var target=float(goal.get("min",goal.get("max",1)))
		if float(goal.get("duration",0))>0:
			target=float(goal.duration);sum+=clampf(float(elapsed.get(key,0))/maxf(.001,target),0,1)
		elif goal.has("max"):
			sum+=1. if current<=target else clampf(target/maxf(.001,current),0,1)
		elif target<=0:sum+=1. if current<=target else 0.
		else:sum+=clampf(current/target,0,1)
	return sum/goals.size()

static func _progress_summary(active:Dictionary,def:Dictionary) -> String:
	var goals=_commission_goals(active,def);var now:Dictionary=active.get("progress_now",{});var elapsed:Dictionary=active.get("goal_progress",{});var rows=[]
	for i in goals.size():
		var goal:Dictionary=goals[i];var key=str(i);var metric=str(goal.get("metric",""));var name=str(GOAL_NAMES.get(metric,metric.replace("_"," ")))
		var value=now.get(key,active.get("delivered_goods",0) if metric=="delivery_goods" else 0)
		var row=name+" "+_format_value(value)
		if goal.has("min"):row+=" / "+_format_value(goal.min)
		elif goal.has("max"):row+=" / ≤"+_format_value(goal.max)
		if float(goal.get("duration",0))>0:row+=" · 连续 %.1f / %.1f月"%[float(elapsed.get(key,0)),float(goal.duration)]
		rows.append(row)
	return " · ".join(rows)

static func _commission_has_goal(def:Dictionary,metric:String,active:Dictionary={}) -> bool:
	for goal in _commission_goals(active,def):
		if str(goal.get("metric",""))==metric:return true
	return false

static func _status_name(status:String) -> String:
	return {"accepted":"已接取","abandoned":"已放弃","completed":"已完成","expired":"已到期","ready":"待领取"}.get(status,"记录")

static func _market(d,resource:String="") -> void:
	var g=d.app.game;var resources=["food","material","goods"]
	d.title.text="街区市场";d.subtitle.text="先查看实时买卖报价与运力，再选择交易方向。所有订单共享实际货运能力。"
	if resource in resources and resource!=d.trade_resource:d.trade_resource=resource;d.trade_price_set=false
	V.steps(d.body,["选择资源","选择方向","数量与限价","确认订单"],0 if resource=="" else 1)
	var quotes=V.grid(d.body,3)
	for value in resources:
		var res=str(value);var quote:Dictionary=g.Market.quote(g,res);var card=V.card(quotes,RES_NAMES[res],"通货 / 单位 · 当前快照",res)
		if quote.is_empty():card.add_child(W.label("暂无有效报价",18,W.MUTED));continue
		var line=V.grid(card,2)
		for direction in ["buy","sell"]:
			var price_side=str(direction);var column=W.vbox(line,4);column.size_flags_horizontal=Control.SIZE_EXPAND_FILL
			column.add_child(W.label("买入价" if price_side=="buy" else "卖出价",14,W.MUTED,false))
			var value_label=W.label("%.3f"%_quote_price(quote,price_side),25,W.ACCENT if price_side=="buy" else W.GREEN,false)
			value_label.set_meta("trade_quote",res+":"+price_side);value_label.set_meta("trade_quote_resource",res);value_label.set_meta("trade_quote_side",price_side);column.add_child(value_label)
		card.add_child(W.label("中间价 %.3f · 价差含手续费"%float(quote.mid),13,W.MUTED))
		var select=W.button("已选择" if d.trade_resource==res else "选择此资源 ›",func():d.replace("city040","resource:"+res))
		select.set_meta("trade_resource",res);select.toggle_mode=true;select.button_pressed=d.trade_resource==res;card.add_child(select)
	var transport=V.grid(d.body,3);var capacity=float(g.Market._total_capacity(g))
	V.metric(transport,"logistics","实际外贸运力","%.1f 单位/小时"%capacity,"真实活跃在线小时 · 全部订单共享",W.ACCENT if capacity>0 else W.GOLD)
	V.metric(transport,d.trade_resource,"当前库存","%.1f 单位"%g.inventory_total(d.trade_resource),RES_NAMES[d.trade_resource]+" · 出口不能超过可用库存")
	V.metric(transport,"currency","当前通货","%.1f"%float(g.s.currency),"正价买入与负价卖出均需要通货")
	var direction=V.grid(d.body,2);var selected:Dictionary=g.Market.quote(g,d.trade_resource)
	for value in ["buy","sell"]:
		var side=str(value);var price=_quote_price(selected,side);var card=V.card(direction,("买入 " if side=="buy" else "卖出 ")+RES_NAMES[d.trade_resource],"报价 %.3f 通货 / 单位"%price,d.trade_resource,W.ACCENT if side=="buy" else W.GREEN)
		_cash_direction(card,side,price)
		var button=W.button("设置数量与限价 ›",func():d.trade_side=side;d.trade_price_set=false;d.push_section("trade",d.trade_resource),capacity<=.000001 or selected.is_empty())
		button.set_meta("trade_side",side);card.add_child(button)
	if capacity<=.000001:V.card(d.body,"等待实际货运能力","正在运行且有利用率的货运设施才能创建并执行订单。","warning",W.GOLD)
	var sources=V.fold(d.body,"运力来源与成交规则","订单按创建顺序分配运力；DT 加速不会增加外贸额度。")
	var rows:Array=g.Market._freight_sources(g)
	if rows.is_empty():sources.add_child(W.label("当前没有提供有效运力的建筑。",14,W.MUTED))
	else:
		var table=_table(sources,["货运来源","基础能力 / 真实小时","运行率","实际能力 / 真实小时"])
		for row in rows:_cell(table,g.project_name(str(row.id)));_cell(table,"%.1f"%float(row.capacity));_cell(table,"%.0f%%"%(float(row.usage)*100));_cell(table,"%.1f"%float(row.available),W.ACCENT)
	sources.add_child(W.label("买入在买价 ≤ 限价时执行；卖出在卖价 ≥ 限价时执行。仓容、库存、通货与排在前面的订单共同约束实际成交量。报价随真实交易压力变化；没有虚构价格历史。",14,W.MUTED))
	_order_table(d,d.body,false)
	var history=V.card(d.body,"最近成交","实际成交价格、数量与通货变动。","ledger")
	_history_table(d,history,0,8)
	history.add_child(W.button("全部成交记录 ›",func():d.push_section("tradehistory","0")))

static func _cash_direction(parent:Node,side:String,price:float) -> Label:
	var text="资源：市场 → 城市；通货：城市 → 市场" if side=="buy" else "资源：城市 → 市场；通货：市场 → 城市"
	if price<0:text="负价买入 · 城市收到资源和通货" if side=="buy" else "负价卖出 · 城市交出资源并支付通货"
	elif is_zero_approx(price):text="零价交易 · 只转移资源，不转移通货"
	var label=W.label(text,14,W.GOLD if price<0 else W.MUTED);label.name="TradeCashDirection";label.set_meta("trade_cash_direction",side+":"+("negative" if price<0 else "positive"));parent.add_child(label);return label

static func _trade_detail(d,resource:String) -> void:
	var g=d.app.game
	if resource not in RES_NAMES:V.card(d.body,"不支持此资源","当前市场只交易粮食、材料与制成品。","warning");return
	d.trade_resource=resource;var quote:Dictionary=g.Market.quote(g,resource);var side=str(d.trade_side);var price=_quote_price(quote,side)
	d.title.text=("买入 " if side=="buy" else "卖出 ")+RES_NAMES[resource];d.subtitle.text="设置数量和限价，下一步查看完整订单后再提交。"
	V.steps(d.body,["选择资源","选择方向","数量与限价","确认订单"],2)
	var facts=V.grid(d.body,3)
	V.metric(facts,resource,"当前"+("买入价" if side=="buy" else "卖出价"),"%.3f"%price,"通货 / 单位；已含价差与手续费")
	V.metric(facts,"logistics","实际外贸运力","%.1f 单位/小时"%float(g.Market._total_capacity(g)),"真实在线小时 · 全部订单共享")
	V.metric(facts,resource,"可接收仓容" if side=="buy" else "可卖出库存","%.1f 单位"%(float(g.Market._buy_room(g,resource)) if side=="buy" else g.inventory_total(resource)),"不足时订单保留等待，不创造资源")
	var form=V.card(d.body,"订单参数","限价允许负数；限制实际成交价。",resource)
	var fields=V.grid(form,2)
	var quantity=W.vbox(fields,8);quantity.size_flags_horizontal=Control.SIZE_EXPAND_FILL;quantity.add_child(W.label("数量 · 单位",16))
	var amount=SpinBox.new();amount.name="TradeAmount";amount.min_value=.1;amount.max_value=9999;amount.step=.1;amount.value=d.trade_amount;amount.custom_minimum_size.y=52;amount.size_flags_horizontal=Control.SIZE_EXPAND_FILL;quantity.add_child(amount)
	amount.value_changed.connect(func(value:float):d.trade_amount=value)
	var price_field=W.vbox(fields,8);price_field.size_flags_horizontal=Control.SIZE_EXPAND_FILL;price_field.add_child(W.label("最高买价" if side=="buy" else "最低卖价",16))
	var limit=SpinBox.new();limit.name="TradeLimit";limit.min_value=-1000;limit.max_value=10000;limit.step=.001;limit.value=d.trade_price if d.trade_price_set else price;limit.custom_minimum_size.y=52;limit.size_flags_horizontal=Control.SIZE_EXPAND_FILL;price_field.add_child(limit)
	limit.value_changed.connect(func(value:float):d.trade_price=value;d.trade_price_set=true)
	var hint=W.label("",15,W.MUTED);hint.name="TradeLimitHint";form.add_child(hint)
	var direction=_cash_direction(form,side,price)
	var sync=func():
		hint.text=("仅在买价 ≤ %.3f 时成交" if side=="buy" else "仅在卖价 ≥ %.3f 时成交")%float(limit.value)
		var cash=(-price if side=="buy" else price)*float(amount.value)
		direction.text=("当前报价估算：收到 %.2f 通货" if cash>=0 else "当前报价估算：支付 %.2f 通货")%absf(cash)+(" · 负价买入收到资源和通货" if side=="buy" and price<0 else (" · 负价卖出需要支付通货" if side=="sell" and price<0 else ""))
	amount.value_changed.connect(func(_value:float):sync.call());limit.value_changed.connect(func(_value:float):sync.call());sync.call()
	form.add_child(W.label("估算假设整笔按当前报价成交；实际分批成交，以成交时价格、运力和可用资源为准。",14,W.MUTED))
	d.footer.show();d.footer_text.text="下一步查看资源方向、限价条件与通货方向。";d.primary.show();d.primary.text="预览订单 ›";d.primary.name="CityPrimary032";d.primary.set_meta("trade_review",true);d.primary.disabled=quote.is_empty() or float(g.Market._total_capacity(g))<=.000001
	d.primary.pressed.connect(func():d.trade_amount=float(amount.value);d.trade_price=float(limit.value);d.trade_price_set=true;d.push_section("tradeconfirm"))
	d.secondary.text="返回市场"

static func _trade_confirm(d) -> void:
	var g=d.app.game;var resource=str(d.trade_resource);var side=str(d.trade_side);var quote:Dictionary=g.Market.quote(g,resource);var price=_quote_price(quote,side)
	d.title.text="确认贸易订单";d.subtitle.text="确认后创建限价订单，按货运能力逐批执行。创建订单时不扣库存或通货。"
	V.steps(d.body,["选择资源","选择方向","数量与限价","确认订单"],3)
	var card=V.card(d.body,("买入 " if side=="buy" else "卖出 ")+RES_NAMES.get(resource,resource),"%.1f 单位 · 限价 %.3f 通货 / 单位"%[float(d.trade_amount),float(d.trade_price)],resource)
	var cash=(-price if side=="buy" else price)*float(d.trade_amount)
	V.flow(card,[{"id":resource,"label":"资源方向","value":"市场 → 城市" if side=="buy" else "城市 → 市场","note":"%.1f 单位"%float(d.trade_amount)},{"id":"currency","label":"当前报价的通货方向","value":"收到 %.2f"%cash if cash>=0 else "支付 %.2f"%absf(cash),"color":W.GREEN if cash>=0 else W.GOLD,"note":"假设整笔以 %.3f 成交"%price}])
	_cash_direction(card,side,price)
	var crosses=price<=float(d.trade_price)+.000001 if side=="buy" else price+.000001>=float(d.trade_price)
	V.badge(card,"当前报价满足限价" if crosses else "当前报价不满足限价 · 将等待",W.GREEN if crosses else W.GOLD)
	card.add_child(W.label(("最高买价 %.3f；只有买价不高于它才成交。" if side=="buy" else "最低卖价 %.3f；只有卖价不低于它才成交。")%float(d.trade_price),16))
	var constraints=V.grid(d.body,3)
	V.metric(constraints,"logistics","共享外贸运力","%.1f 单位/小时"%float(g.Market._total_capacity(g)),"真实在线小时；按订单创建顺序执行")
	V.metric(constraints,resource,"可接收仓容" if side=="buy" else "可卖出库存","%.1f 单位"%(float(g.Market._buy_room(g,resource)) if side=="buy" else g.inventory_total(resource)))
	V.metric(constraints,"currency","持有通货","%.2f"%float(g.s.currency),"付款余额不足时，剩余订单等待")
	var rules=V.fold(d.body,"分批成交与负价规则","实际成交时重新检查报价、库存、仓容和通货。")
	rules.add_child(W.label("正价买入支付通货，正价卖出收到通货；负价买入同时收到资源和通货，负价卖出交出资源并支付通货。成交记录保留实际价格、现金方向与货运来源。",14,W.MUTED))
	d.footer.show();d.footer_text.text="%s %s %.1f 单位 · 限价 %.3f"%["买入" if side=="buy" else "卖出",RES_NAMES.get(resource,resource),float(d.trade_amount),float(d.trade_price)]
	d.primary.show();d.primary.text="确认创建订单";d.primary.set_meta("trade_confirm",true);d.primary.disabled=quote.is_empty() or float(g.Market._total_capacity(g))<=.000001
	d.primary.pressed.connect(func():
		var error=g.Market.order(g,resource,side,float(d.trade_amount),float(d.trade_price))
		if error!="":d.app.message(error)
		else:d.app.display_pending_tick=int(g.s.simulation_tick);d.app.save_game();d.app.refresh();d.replace("city040","resource:"+resource);d.app.message("贸易订单已创建，按实际货运能力执行。")
	)
	d.secondary.text="修改订单"

static func _order_status(status:String) -> String:
	return {"open":"待成交","partial":"部分成交","filled":"已完成","cancelled":"已取消"}.get(status,status)

static func _order_table(d,parent:Node,all_orders:bool=false) -> void:
	var g=d.app.game;var box=V.card(parent,"当前订单" if not all_orders else "全部订单","各资源与买卖方向共享同一份运力。","logistics")
	var rows:Array=[]
	for item in g.s.block.get("trade_orders",[]):
		if all_orders or str(item.get("status","open")) in ["open","partial"]:rows.append(item)
	if rows.is_empty():box.add_child(W.label("没有开放订单。" if not all_orders else "尚未创建订单。",14,W.MUTED))
	var table=_table(box,["资源 / 方向","数量","已成交","剩余","限价","状态","管理"]) if not rows.is_empty() else null
	for row in rows:
		var id=str(row.get("id",""));var open=str(row.get("status","")) in ["open","partial"]
		var name=W.button(RES_NAMES.get(str(row.get("resource","")),str(row.get("resource","")))+" · "+("买入" if row.get("side","")=="buy" else "卖出"),func():d.push_section("order",id));name.set_meta("trade_order",id);name.size_flags_horizontal=Control.SIZE_EXPAND_FILL;table.add_child(name)
		_cell(table,"%.1f"%float(row.get("amount",0)));_cell(table,"%.1f"%float(row.get("filled",0)));_cell(table,"%.1f"%float(row.get("remaining",0)));_cell(table,"%.3f"%float(row.get("limit_price",0)));_cell(table,_order_status(str(row.get("status","open"))),W.GOLD if open else W.MUTED)
		if open:
			var cancel=W.button("取消",func():_cancel_order(d,id));cancel.set_meta("trade_cancel",id);table.add_child(cancel)
		else:_cell(table,"—",W.MUTED)
	if not all_orders:
		var history=V.fold(box,"已结束订单","查看完成与取消记录。")
		var ended:Array=[]
		for row in g.s.block.get("trade_orders",[]):
			if str(row.get("status","")) in ["open","partial"]:continue
			ended.append(row)
		if ended.is_empty():history.add_child(W.label("尚无结束订单。",14,W.MUTED))
		else:
			var records=_table(history,["资源 / 方向","数量","已成交","限价","状态","记录"])
			for row in ended:
				var id=str(row.get("id",""));_cell(records,RES_NAMES.get(str(row.get("resource","")),str(row.get("resource","")))+" · "+("买入" if row.get("side","")=="buy" else "卖出"));_cell(records,"%.1f"%float(row.get("amount",0)));_cell(records,"%.1f"%float(row.get("filled",0)));_cell(records,"%.3f"%float(row.get("limit_price",0)));_cell(records,_order_status(str(row.get("status",""))),W.MUTED)
				var button=W.button(id+" ›",func():d.push_section("order",id));button.set_meta("trade_order",id);records.add_child(button)

static func _cancel_order(d,id:String) -> void:
	var error=d.app.game.Market.cancel(d.app.game,id)
	if error!="":d.app.message(error)
	else:d.app.display_pending_tick=int(d.app.game.s.simulation_tick);d.app.save_game();d.app.refresh();d.render(true);d.app.message("订单已取消；已成交部分保留在记录中。")

static func _order_detail(d,id:String) -> void:
	var g=d.app.game;var item:Dictionary={}
	for row in g.s.block.get("trade_orders",[]):
		if str(row.get("id",""))==id:item=row;break
	d.title.text="订单 · "+id;d.subtitle.text="原始数量、限价、执行进度与真实成交来源。"
	if item.is_empty():V.card(d.body,"订单不存在","返回市场查看当前订单。","warning");return
	var resource=str(item.get("resource",""));var side=str(item.get("side",""));var card=V.card(d.body,RES_NAMES.get(resource,resource)+" · "+("买入" if side=="buy" else "卖出"),"创建于 %.2f 来生月"%float(item.get("created_at",0)),resource)
	V.badge(card,_order_status(str(item.get("status","open"))),W.GOLD if item.get("status","") in ["open","partial"] else W.MUTED)
	V.meter(card,"已成交",float(item.get("filled",0)),float(item.get("amount",0)),"单位")
	var metrics=V.grid(d.body,3);V.metric(metrics,resource,"原始数量","%.1f 单位"%float(item.get("amount",0)));V.metric(metrics,resource,"剩余数量","%.1f 单位"%float(item.get("remaining",0)));V.metric(metrics,"currency","限价","%.3f"%float(item.get("limit_price",0)),"最高买价" if side=="buy" else "最低卖价")
	var history=V.card(d.body,"此订单实际成交","以实际单价与通货变动为准。","ledger")
	var records:Array=[]
	for row in g.s.block.get("trade_history",[]):
		if str(row.get("order_id",""))==id:records.append(row)
	_trade_rows(d,history,records)
	if item.get("status","") in ["open","partial"]:
		d.footer.show();d.footer_text.text="取消只停止剩余部分；已成交记录保留。";d.primary.text="取消剩余订单";d.primary.set_meta("trade_cancel",id);d.primary.pressed.connect(func():_cancel_order(d,id));d.secondary.text="返回订单列表"

static func _history_table(d,parent:Node,page:int=0,page_size:int=20) -> void:
	var history:Array=d.app.game.s.block.get("trade_history",[]);var rows:Array=[];var start=history.size()-1-page*page_size
	for index in range(start,maxi(-1,start-page_size),-1):
		if index>=0:rows.append(history[index])
	_trade_rows(d,parent,rows)

static func _trade_rows(d,parent:Node,rows:Array) -> void:
	if rows.is_empty():parent.add_child(W.label("尚无已执行交易。",14,W.MUTED));return
	var table=_table(parent,["时间 / 来生月","资源 / 方向","数量","实际单价","通货变动","来源"])
	for row in rows:
		var id=str(row.get("id",""));var cash=float(row.get("currency_delta",0))
		_cell(table,"%.2f"%float(row.get("h",0)));_cell(table,RES_NAMES.get(str(row.get("resource","")),str(row.get("resource","")))+" · "+("买入" if row.get("side","")=="buy" else "卖出"));_cell(table,"%.1f"%float(row.get("amount",0)));_cell(table,"%.3f"%float(row.get("unit_price",row.get("price",0))));_cell(table,"%+.2f"%cash,W.GREEN if cash>=0 else W.GOLD)
		var button=W.button("查看 ›",func():d.push_section("tradeentry",id));button.set_meta("trade_entry",id);table.add_child(button)

static func _trade_history(d,page:int=0) -> void:
	var rows:Array=d.app.game.s.block.get("trade_history",[]);var pages=maxi(1,ceili(float(rows.size())/20));page=clampi(page,0,pages-1)
	d.title.text="全部贸易成交";d.subtitle.text="实际成交记录 · 最新在前 · 每页 20 笔"
	var box=V.card(d.body,"成交记录","第 %d / %d 页 · 共 %d 笔"%[page+1,pages,rows.size()],"ledger");_history_table(d,box,page,20)
	var nav=W.hbox(d.body,10);nav.add_child(W.button("较新记录",func():d.replace(d.kind,d.arg,"tradehistory",str(page-1)),page<=0));nav.add_child(W.button("较早记录",func():d.replace(d.kind,d.arg,"tradehistory",str(page+1)),page>=pages-1))

static func _trade_entry(d,id:String) -> void:
	var g=d.app.game;var item:Dictionary={}
	for row in g.s.block.get("trade_history",[]):
		if str(row.get("id",""))==id:item=row;break
	d.title.text="实际成交 · "+id;d.subtitle.text="该笔成交的资源、通货与实际货运来源。"
	if item.is_empty():V.card(d.body,"成交记录不存在","返回市场查看成交列表。","ledger");return
	var resource=str(item.get("resource",""));var side=str(item.get("side",""));var price=float(item.get("unit_price",item.get("price",0)));var cash=float(item.get("currency_delta",0))
	var card=V.card(d.body,RES_NAMES.get(resource,resource)+" · "+("买入" if side=="buy" else "卖出"),"订单 "+str(item.get("order_id",""))+" · %.2f 来生月"%float(item.get("h",0)),resource);_cash_direction(card,side,price)
	var metrics=V.grid(card,3);V.metric(metrics,resource,"实际数量","%.3f 单位"%float(item.get("amount",0)));V.metric(metrics,"currency","实际单价","%.3f"%price,"通货 / 单位");V.metric(metrics,"currency","实际通货变动","%+.3f"%cash,"收到" if cash>=0 else "支付",W.GREEN if cash>=0 else W.GOLD)
	var source=V.card(d.body,"运力贡献","该笔成交实际分配的运力。","logistics");var table=_table(source,["来源建筑","成交资源量"])
	for row in item.get("capacity_sources",[]):_cell(table,g.project_name(str(row.get("project_id",""))));_cell(table,"%.3f 单位"%float(row.get("amount",0)),W.ACCENT)
	if item.get("capacity_sources",[]).is_empty():source.add_child(W.label("此记录未保存运力来源。",14,W.MUTED))
	var detail=V.fold(d.body,"完整成交记录",id+" · tick "+str(item.get("tick","")));detail.add_child(W.label(JSON.stringify(item,"  "),13,W.MUTED))

static func _sync_trade_price(d) -> void:
	var quote=d.app.game.Market.quote(d.app.game,d.trade_resource);d.trade_price=_quote_price(quote,d.trade_side);d.trade_price_set=false;d.render(false)

static func _quote_price(quote:Dictionary,side:String) -> float:
	return float(quote.get("buy",0) if side=="buy" else quote.get("sell",0))

static func _block(d) -> void:
	var g=d.app.game;var block:Dictionary=g.s.block
	d.title.text="当前街区";d.subtitle.text="目标达成后仍可继续经营；命名只是留下一份自己的地图标记。"
	var name=str(block.get("display_name","河谷登陆点"))
	var naming=_group(d.body,"街区名称")
	naming.add_child(W.label(name+(" · 可命名" if block.get("naming_rights",false) else ""),22,W.GREEN if block.get("naming_rights",false) else W.TEXT))
	if block.get("naming_rights",false):
		var input=LineEdit.new();input.placeholder_text="输入街区名称";input.custom_minimum_size.y=46;input.max_length=24;naming.add_child(input)
		var save_name=W.primary(W.button("保存名称",func():
			var error=g.Block.name_block(g,input.text.strip_edges())
			if error!="":d.app.message(error)
			else:d.app.save_game();d.app.refresh();d.replace("city040","block");d.app.message("街区名称已保存。")
		,input.text.strip_edges().is_empty()))
		naming.add_child(save_name)
		input.text_changed.connect(func(value:String):save_name.disabled=value.strip_edges().is_empty())
	else:
		naming.add_child(W.label("完成街区目标后会获得命名权。",14,W.MUTED))
	var goal=bool(g.s.get("city_goal",{}).get("completed",false))
	var status=_group(d.body,"街区目标",W.SOFT if goal else W.PANEL)
	status.add_child(W.label("已达成 · 可继续经营" if goal else "进行中 · 目标不会限制继续建造",17,W.GREEN if goal else W.TEXT))
	status.add_child(W.button("查看目标条件与历史",func():d.push_section("blockgoal")))

static func _block_management(d) -> void:
	var g=d.app.game;var cards=V.grid(d.body,2)
	var paused=bool(g.s.get("migration_paused",false));var interval=float(g.c.beta.economy_candidates.arrival_interval_months)
	var arrival=maxf(0,(int(g.s.event_cursor)+1)*interval-float(g.s.local_hours))
	var migration=V.card(cards,"居民迁入","每 %.1f 来生月一批 · 每批 %.0f 人"%[interval,float(g.c.beta.economy_candidates.arrival_people)],"population")
	V.badge(migration,"已暂停迁入" if paused else "接纳新居民",W.GOLD if paused else W.GREEN)
	migration.add_child(W.label("下一迁入周期：%.1f 来生月后"%arrival,23,W.MUTED if paused else W.ACCENT))
	migration.add_child(W.label("暂停期间到期的批次会跳过；恢复后继续后续周期。",14,W.MUTED))
	migration.add_child(W.button("恢复迁入" if paused else "暂停新居民迁入",func():g.toggle_migration();d.app.save_game();d.app.refresh();d.render(true)))
	var goods=V.card(cards,"生活用品计划","公布后经过宽限期，居民开始消费制成品。","goods")
	if g.s.get("goods_active",false):
		V.steps(goods,["公布计划","宽限期","持续消费"],2);V.badge(goods,"生活用品需求已生效",W.GREEN)
		goods.add_child(W.label("制成品库存 %.1f 单位"%g.inventory_total("goods"),22,W.ACCENT))
	elif float(g.s.get("goods_requested_at",-1))>=0:
		var remain=maxf(0,float(g.s.goods_requested_at)+float(g.c.balance.goods_grace_hours)-float(g.s.local_hours))
		V.steps(goods,["公布计划","宽限期","持续消费"],1);goods.add_child(W.label("%.1f 来生月后开始消费"%remain,23,W.ACCENT))
	else:
		V.steps(goods,["公布计划","宽限期","持续消费"],0);V.badge(goods,"尚未公布",W.MUTED)
		var available=bool(g.s.get("goods_available",false)) or g.inventory_total("goods")>0
		goods.add_child(W.primary(W.button("公布生活用品计划",func():
			var error=g.enable_goods()
			if error!="":d.app.message(error)
			else:d.app.save_game();d.app.refresh();d.render(true);d.app.message("生活用品消费计划已公布。")
		,not available)))
		if not available:goods.add_child(W.label("先建立制成品供给，才能公布计划。",14,W.MUTED))
