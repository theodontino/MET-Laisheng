extends RefCounted
const W=preload("res://ui/widgets.gd")
const P=preload("res://ui/presentation.gd")
const C031=preload("res://ui/components031/components.gd")
const D031=preload("res://ui/components031/design.gd")
const Art=preload("res://ui/map/art.gd")
const InspectorModel=preload("res://ui/view_models/inspector_model.gd")
const Hex=preload("res://core/hex.gd")
static func card(parent:Node,title:String,subtitle:String="",body:String="") -> VBoxContainer:
	var panel=W.panel(parent,W.SOFT);panel.size_flags_horizontal=Control.SIZE_EXPAND_FILL;panel.custom_minimum_size.x=0
	var v=W.vbox(panel,9);v.add_child(W.label(title,21,W.TEXT))
	if subtitle!="":v.add_child(W.label(subtitle,12,W.GOLD))
	if body!="":v.add_child(W.label(body,14,W.TEXT))
	return v
static func start(app):
	app.open_modal("来生 · 从河谷聚落开始","18位居民准备在来生安家。这里是生态管理区内的开放聚落：从农居与工坊起步，让街巷逐渐长成城市。")
	var row=app.option_row(app.modal_body,12)
	for i in 3:
		var v=card(row,["河湾","林缘旧村","北部高台"][i],["粮食与水岸","林业与邻里","开采与高地"][i],["肥沃地适合农业，水岸能帮助工业冷却。想想哪片岸边该留给居民。","林地多，材料容易起步。让生产和住区彼此照应，而不是相互打扰。","高地料场有产能优势。把工业用地和附近住区的关系安排好。"][i])
		var picture=Art.picture(["homestead","workshop","neighborhood"][i],Vector2(160,160));v.add_child(picture);v.move_child(picture,0)
		v.add_child(W.primary(W.button("从这里开始",func():
			var e=app.game.select_block(i)
			if e=="":app.save_game();app.refresh();origin(app)
			else:app.message(e))))
	W.section(app.modal_body,"先看，再建","下方选建筑 → 悬停比较位置 → 三份投标选一份。建成后先看它实际做了什么；需要时再研究方案。")
static func origin(app):
	var g=app.game;app.open_modal("他们带着什么来到河谷？","先安顿18位居民。下一批6位新邻居在3来生月后抵达；可以暂停迁入。")
	var row=app.option_row(app.modal_body,14)
	for oid in g.s.origin_choices:
		var o=g.c.origins[oid]
		var starts={"village":"随行粮食留作城市储备，早期预算与DT可用于建设。","charter":"额外资金与材料支持早期建设；还需要建立本地口粮来源。","miners":"额外材料可投入建设；尽快补足粮食供给。","free":"预算、少量粮食、建材与DT起步，发展方向由你决定。"}
		var v=card(row,o.name,"农居 + 作坊",starts.get(oid,o.story))
		var picture=Art.picture({"village":"homestead","charter":"neighborhood","miners":"workshop","free":"homestead"}.get(oid,"homestead"),Vector2(144,150));v.add_child(picture);v.move_child(picture,0)
		W.section(v,"随行家底","通货 %.0f · DT %.0f\n粮食 %.0f · 材料 %.0f"%[g.c.balance.initial_money+o.cash,g.c.balance.initial_dt,g.c.balance.initial_stock.food+o.food,g.c.balance.initial_stock.material+o.material])
		W.section(v,"起步时先想一件事",{"village":"粮食有了起点。先找材料、电力和下一批居民的住处。","charter":"电力已有保障。粮食和材料还需要你安排。","miners":"材料已有来源。把粮食和工人的生活接起来。","free":"材料与预算较灵活。先让粮食供给站稳，再选发展方向。"}[oid])
		v.add_child(W.primary(W.button("接手 · "+o.name,func():
			var e=g.choose_origin(oid)
			if e=="":app.save_game();app.overlay.hide();app.refresh();app.message("从下方选择建筑，找一个合适的位置开始建设。")
			else:app.message(e))))
static func short_rules(summary:String) -> Array:
	var s=summary.replace("；","\n").replace("。","\n").replace("Field","覆盖").replace("Flex","弹性").replace("Trait","特质").replace("Bonus","方案").replace("Population","居民")
	var result=[]
	for line in s.split("\n"):
		if line.strip_edges()!="":result.append(line.strip_edges())
	return result
static func tender(app):
	app.show_workbench()
static func offer_detail(app,offer:Dictionary):
	var g=app.game;var org=g.c.operators.get(str(offer.get("operator_org","")),{})
	app.open_modal(str(org.get("name","运营者")),"三份报价只比较运营者；建成后可随建筑升级解锁后续槽位。")
	W.section(app.modal_body,"组织方向",str(org.get("summary","")))
	var track=offer.get("slot_track",org.get("slot_track",[]));var strip=Art.slot_strip(g,track);app.modal_body.add_child(strip)
	var weights=[]
	for domain in org.get("domain_weights",{}):weights.append(str(domain)+" "+str(org.domain_weights[domain]))
	var labels=[]
	for type in track:labels.append({"Production":"生产","Logistics":"物流","Operations":"运营","Flex":"弹性"}.get(str(type),str(type)))
	W.section(app.modal_body,"未来方案槽", " → ".join(labels)+"\n偏好领域："+" · ".join(weights))
	app.modal_body.add_child(W.primary(W.button("回到这组投标",func():tender(app))))
static func goals(app):
	app.desk.open("tasks")
static func goals_legacy(app):
	var g=app.game;g.Progress.initialize(g);app.open_modal("把登陆点建成独立社区","先站稳生活底线，再完成一项你选择的发展成果。可以随时改方向，不要求造齐建筑。")
	var rows=g.Progress.checklist(g)
	for r in rows:W.section(app.modal_body,("✓ " if r.done else "○ ")+r.name,r.value)
	W.section(app.modal_body,"当前成果还差什么","以下是实际已结算状态；满足后持续计时。缺容量可扩建，缺人/电应先修瓶颈。")
	for item in g.Progress.requirements(g):
		var line=W.hbox(app.modal_body,8);var label=W.label(("✓ " if item.done else "○ ")+item.name+"  ·  "+item.value,13,W.GREEN if item.done else W.TEXT);label.size_flags_horizontal=Control.SIZE_EXPAND_FILL;line.add_child(label)
		line.add_child(W.button("查看解法",func():app.close_modal();app.handle_alert({"action":item.action,"target":""})))
	var row=app.option_row(app.modal_body,10)
	for route in ["community","delivery","renewal"]:
		var def=g.c.data.city_goals[route];var selected=g.s.city_goal.route==route
		var v=card(row,def.name,"当前方向" if selected else "另一种建城成果",def.description)
		v.add_child(W.button("选择这个方向",func():app.act(g.Progress.select(g,route));goals(app),selected))
		if route=="delivery":v.add_child(W.button("交付20材料 + 12制成品",func():
			var e=g.Progress.deliver(g)
			if e=="":app.save_game();app.refresh();goals(app)
			else:app.modal_subtitle.text=e,g.s.city_goal.delivered))
	W.section(app.modal_body,"生活底线怎么算","无人待安置，粮食与电力满足至少95%；财政非负，且收支非负或储备还能支持4来生月。连续站稳1来生月。请先修短板，不必继续盲目扩张。")
	var changes=W.hbox(app.modal_body,10)
	var arrive=g.next_arrival();var arrival_text="下一批：%.1f月后，6位居民"%arrive if arrive>=0 else "本轮约定的居民已全部到达"
	W.section(app.modal_body,"接下来会发生什么",arrival_text+"\n暂停迁入不会停止自然增长，空房仍需保留余量。\n公共服务需求从第3来生月起，在2来生月内渐入；提前建设不会突然触发惩罚。")
	changes.add_child(W.button("恢复迁入" if g.s.migration_paused else "暂停新居民迁入",func():g.toggle_migration();app.save_game();app.refresh();goals(app)))
	if not g.s.goods_active:
		if float(g.s.goods_requested_at)>=0:W.section(app.modal_body,"生活用品计划已公布","%.1f月后开始消费制成品，届时每位居民基础需求为%.3f/月。"%[maxf(0,g.s.goods_requested_at+g.c.balance.goods_grace_hours-g.s.local_hours),g.c.balance.population_goods])
		else:changes.add_child(W.button("公布生活用品计划（2月后生效）",func():
			var e=g.enable_goods()
			if e=="":app.save_game();app.refresh();goals(app)
			else:app.modal_subtitle.text=e,not g.s.get("goods_available",false)))
	else:W.section(app.modal_body,"生活用品需求正在生效","居民正在使用制成品，真实消费会出现在账本。")
static func block(app):
	app.policy_categories()
static func population(app):
	app.desk.open("population")
static func population_legacy(app):
	var g=app.game;app.open_modal("住在河谷的人","人员、就业和服务都有真实去向。远一点可能排在后面，但不会在上班后又因为距离变成‘半个人’。")
	for gid in g.Rules.ids(g.s.groups):
		var p=g.s.groups[gid];var v=card(app.modal_body,g.project_name(p.host),"%.1f位居民"%p.count,"健康 %.0f · 幸福 %.0f · 教育 %.0f · 人员效率 %.0f%%"%[p.health,p.happiness,p.education,100*p.efficiency])
		var jobs=[]
		for bid in p.employment:jobs.append(g.project_name(bid)+" %.1f人"%p.employment[bid])
		W.section(v,"在哪里工作"," / ".join(jobs) if not jobs.is_empty() else "目前未分配工作")
		var services=[]
		for k in g.c.balance.service_kinds:services.append(P.KIND[k]+" %.0f%%"%(100*float(p.living.get(k,0)) ))
		W.section(v,"实际服务满足率"," · ".join(services))
		if p.host!="homeless":v.add_child(W.button("查看这片住区",func():app.selected_project=p.host;app.mode="inspect";app.close_modal()))
static func resources(app):
	app.desk.open("finance")
static func resources_legacy(app):
	var g=app.game;var shot=app.planner.snapshot(g);app.open_modal("河谷收支账","库存是现在已有的；下面的产、用、弃是复用真实内核计算的下一周期预计。转仓不重复计产出。")
	for r in ["food","material","goods"]:
		if r=="goods" and not g.s.get("goods_available",false) and g.inventory_total(r)<=0:continue
		var x=P.resource(g,shot,r)
		W.section(app.modal_body,"%s · 库存 %.1f"%[P.RES[r],x.stock],"生产 %.2f/月 − 消耗 %.2f/月 − 丢弃 %.2f/月 = 库存净变 %+.2f/月\n%s"%[x.produced,x.consumed,x.lost,x.net,x.status])
		for id in g.Rules.ids(g.s.projects):
			var n=float(g.s.projects[id].inventory.get(r,0));var cap=g.stock_cap_for(id,r)
			if cap>0:app.modal_body.add_child(W.label("%s  %.1f / %.0f%s"%[g.project_name(id),n,cap," · 可接收外部溢出" if id=="municipal" or "building.logistics" in g.s.projects[id].traits or g.compiled.config[id].get("overflow_relay",false) else " · 本地产物仓"],13,W.MUTED))
		for id in g.Rules.ids(shot.metrics.get("activity_ledger",{})):
			for entry in shot.metrics.activity_ledger[id]:
				if entry.resource!=r or entry.rate<.001:continue
				var desc=("产 %.2f/月（入库 %.2f，丢弃 %.2f）"%[entry.rate,entry.kept,entry.lost]) if entry.kind=="output" else "用 %.2f/月"%entry.rate
				app.modal_body.add_child(W.label(g.project_name(id)+" · "+desc+" · "+str(entry.source),12,W.TEXT))
	var m=shot.metrics
	W.section(app.modal_body,"财政 · 下一周期预计","收入 %.2f/月 − 维护 %.2f/月 = 净收支 %+.2f/月\n生产税只计算留下的有效产物，不为直接丢弃物资反复发钱。"%[m.get("income",0),m.get("upkeep",0),m.get("cashflow",0)])
	for entry in m.get("fiscal_ledger",[]):
		app.modal_body.add_child(W.label(("+" if entry.kind=="income" else "−")+"%.2f/月  "%entry.rate+g.project_name(entry.source_id)+" · "+entry.name,12,W.GREEN if entry.kind=="income" else W.MUTED))
	W.section(app.modal_body,"电力 · 当期使用，不默认存储","供给 %.2f，需求 %.2f，满足 %.0f%%。只有显式储能能力能留下余电。"%[m.get("energy",0),m.get("energy_need",0),100*m.get("energy_ratio",1)])
	for bid in m.get("energy_sources",{}):app.modal_body.add_child(W.label(g.project_name(bid)+" · 供电 %.2f"%m.energy_sources[bid],12,W.GREEN))
	for bid in m.get("energy_requests",{}):app.modal_body.add_child(W.label(g.project_name(bid)+" · 用电申请 %.2f"%m.energy_requests[bid],12,W.TEXT))
	for gid in m.get("population_energy",{}):app.modal_body.add_child(W.label(g.project_name(g.s.groups[gid].host)+"住户 · 用电申请 %.2f"%m.population_energy[gid],12,W.MUTED))
static func explain(app,id:String):
	var g=app.game;app.open_modal(g.project_name(id)+" · 来源与计算","先看真实活动，再展开倍率。列表中的来源就是参与结算的对象，不是另算一套演示数。")
	var shot=app.planner.snapshot(g)
	for entry in shot.metrics.get("activity_ledger",{}).get(id,[]):
		if entry.rate>.001:W.section(app.modal_body,("产出 " if entry.kind=="output" else "消耗 ")+P.RES.get(entry.resource,entry.resource),"%.3f/月 · %s"%[entry.rate,entry.source])
	W.section(app.modal_body,"数值修正链")
	for key in g.Rules.ids(g.compiled.trace.get(id,{})):
		var x=g.compiled.trace[id][key];var name={"efficiency":"建筑效率","maintenance":"固定维护","output.food":"粮食产能","output.material":"材料产能","output.goods":"制成品产能","spatial_synergy":"空间协同","cohesion":"整套自营"}.get(key,key)
		W.section(app.modal_body,name+" = %.3f"%float(x.get("value",0)))
		for rule in x.get("rules",[]):app.modal_body.add_child(W.label(str(rule.source)+" · "+str(rule.mode)+" "+str(rule.value),13,W.MUTED))
	W.section(app.modal_body,"这个位置受到的空间影响")
	var tile=g.s.projects[id].tile
	for kind in shot.tiles.get(tile,{}).get("fields",{}):
		if kind not in ["pollution","heat","medical","education","commercial","entertainment","employment"]:continue
		for field in shot.tiles[tile].fields[kind]:
			app.modal_body.add_child(W.label(P.KIND.get(kind,{"pollution":"污染","heat":"余热","employment":"岗位可达"}.get(kind,kind))+" %.2f ← "%field.value+g.project_name(field.source_id),12,W.MUTED))
	app.modal_body.add_child(W.button("回到地图",func():app.close_modal()))
static func slot_detail(app,id:String,slot:String):
	var g=app.game
	if not g.s.projects.has(id) or not g.s.projects[id].slots.has(slot):app.message("方案位已变化，请重新选择。");return
	var p=g.s.projects[id];var iid=str(p.slots[slot]);var type=g.slot_type(id,slot)
	app.open_modal(g.project_name(id)+" · "+g.slot_label(id,slot),"安装资格在部署时检查；升级不会让已安装效果静默失效。")
	var row=W.hbox(app.modal_body,8);C031.socket_symbol(row,type,32);row.add_child(W.label(g.slot_label(id,slot),18))
	if type=="Flex":row.add_child(W.label("来自模块化设计 · 接受生产 / 物流",14,W.MUTED))
	if iid=="":
		var v=C031.group(app.modal_body,"空闲插口",D031.color("inset"));v.add_child(W.label("从年度、人口或主题奖励中获得副本后，可安装到适用的空槽。",16,W.MUTED))
		v.add_child(W.primary(W.button("查看部署许可",func():app.close_modal();app.desk.open("city040","permits"))))
		return
	var ins=g.s.bonus_instances[iid];var def=g.c.bonuses[ins.bonus_id]
	var model=InspectorModel.build(g,app.shot,id);var state={}
	for x in model.slots:
		if x.stable_id==slot:state=x
	var contract=str(ins.get("contract_org",ins.provider_org))
	C031.bonus(app.modal_body,g,ins.bonus_id,Callable(),"full",{"host":"building","provider_org":contract,"provider_name":g.c.operators[contract].name,"instance_status":state.get("reason","") if state.get("state","")=="conditional" else ("已装载 · 自营" if contract==p.operator_org else "已装载 · 外部方案"),"conditional":state.get("state","")=="conditional"})
	var actions=C031.group(app.modal_body,"装配操作")
	actions.add_child(W.button("卸下当前方案…",func():confirm_unmount(app,id,slot),p.status!="running"))
	var source=C031.group(app.modal_body,"来源与合同")
	source.add_child(W.label("原提供方："+g.c.operators[ins.provider_org].name+"\n当前签约方："+g.c.operators[contract].name+"\n建筑运营者："+g.c.operators[p.operator_org].name,16,W.MUTED))
	source.add_child(W.label("这张许可已用于当前街区；卸下不会退回许可。",14,W.MUTED))
	app.modal_body.add_child(W.button("完整规则词条",func():bonus_info(app,ins.bonus_id,func():slot_detail(app,id,slot))))
static func confirm_unmount(app,id:String,slot:String):
	var g=app.game;var iid=str(g.s.projects[id].slots.get(slot,""))
	if iid=="":return
	app.open_modal("卸下「"+g.c.bonuses[g.s.bonus_instances[iid].bonus_id].name+"」？","该方案的效果将停止；存在弹性子槽依赖时不会静默删卡。")
	app.modal_body.add_child(W.button("确认卸下",func():
		var e=g.clear_slot(id,slot)
		if e=="":app.act("");slot_detail(app,id,slot)
		else:app.modal_subtitle.text=e))
static func catalog(app):
	app.open_modal("方案图鉴 · 40 张可用方案","颜色认领域，插口认类型。这里只看方案，不花钱、不重抽，也不改变装配资格。")
	var bar=app.option_row(app.modal_body,8)
	var search=LineEdit.new();search.placeholder_text="搜索方案名称 / ID";search.custom_minimum_size.y=48;search.size_flags_horizontal=Control.SIZE_EXPAND_FILL;search.text=app.catalog_state.query;bar.add_child(search)
	var filter=OptionButton.new();filter.custom_minimum_size.y=48;filter.add_item("全部领域");var domains=["all","residential","agriculture","industrial","commercial","public","infrastructure","general"]
	for id in domains.slice(1):filter.add_item(D031.domain(id).label)
	filter.selected=maxi(0,domains.find(app.catalog_state.domain));bar.add_child(filter)
	var count=W.label("",14,W.MUTED);app.modal_body.add_child(count)
	var grid=GridContainer.new();grid.columns=1 if app.compact else (2 if W.text_scale>1. else 3);grid.add_theme_constant_override("h_separation",16);grid.add_theme_constant_override("v_separation",16);grid.size_flags_horizontal=Control.SIZE_EXPAND_FILL;app.modal_body.add_child(grid)
	var rebuild=func():
		W.clear(grid);var n=0
		var order=["P01","P02","L01","P06","O03","M01"]
		for bid in app.game.Rules.ids(app.game.c.bonuses):
			if not bid in order:order.append(bid)
		for bid in order:
			var data=D031.card(app.game,bid);var query=search.text.strip_edges().to_lower()
			if query!="" and not query in (str(data.name)+bid).to_lower():continue
			if app.catalog_state.domain!="all" and data.visual_domain!=app.catalog_state.domain:continue
			n+=1;C031.bonus(grid,app.game,bid,func():bonus_info(app,bid,func():catalog(app)))
		count.text="显示 %d / 40 · 点卡看完整规则"%n
	search.text_changed.connect(func(value):app.catalog_state.query=value;rebuild.call())
	filter.item_selected.connect(func(index):app.catalog_state.domain=domains[index];rebuild.call())
	rebuild.call()
static func bonus_info(app,bid:String,back:Callable=Callable()):
	var def=app.game.c.bonuses[bid];app.open_modal(def.name+" · 完整词条","来自当前实际规则。主题色不是稀有度，也不表示强弱。")
	if back.is_valid():app.modal_body.add_child(W.button("回到上一页",back))
	C031.bonus(app.modal_body,app.game,bid,Callable())
	var v=C031.group(app.modal_body,"完整解释",D031.color("inset"));v.add_child(W.label(def.summary,16))
	var eligibility=C031.group(app.modal_body,"挂载资格")
	eligibility.add_child(W.label("允许对象："+" / ".join(def.get("hosts",[]))+"\n允许槽型："+" / ".join(def.allowed_slots)+"\n对象特质："+" / ".join(def.get("traits",[])),16,W.MUTED))
	var rule_box=C031.group(app.modal_body,"规则来源 · 高级明细")
	for rule in def.rules:rule_box.add_child(W.label(str(rule.id)+"\n"+JSON.stringify(rule),14,W.MUTED))
static func demolition(app,id:String):
	app.open_modal("拆除「"+app.game.project_name(id)+"」？","需要20通货。原住户、健康、教育和工作关系会保留，先重新安置，剩下的人进入待安置住区。建筑方案随建筑卸除。")
	app.modal_body.add_child(W.button("确认拆除",func():
		var e=app.game.demolish(id)
		if e=="":app.selected_project="";app.inspector_key="";app.save_game();app.close_modal()
		else:app.modal_subtitle.text=e))
static func award(app):
	var g=app.game;app.open_modal("来生 · 街区建成","从一处登陆点，到一个能够独立生活的地方。你选择的街区目标已经兑现。")
	var v=card(app.modal_body,g.c.data.city_goals[g.s.city_goal.route].name,"居民 %.1f · 幸福 %.0f"%[g.s.metrics.population,g.s.metrics.happiness],"你获得了为这片街区命名的权利。可以现在命名，也可以稍后继续经营。")
	v.add_child(W.primary(W.button("现在命名",func():app.close_modal();app.desk.open("city040","block"))))
	v.add_child(W.button("稍后 · 继续经营",func():app.close_modal()))
	v.add_child(W.button("为城市留一份存档",func():app.export_file("save")))
static func governance(app):
	app.open_modal("社区需要一次整改","生活与财政持续恶化。可以先修复供给，也可以用80通货争取缓冲。")
	app.modal_body.add_child(W.button("80通货整改",func():app.act(app.game.Progress.crisis(app.game,"repair"));app.close_modal()))
	app.modal_body.add_child(W.button("离任并保留地图",func():app.game.Progress.crisis(app.game,"leave");app.save_game();failure(app)))
static func failure(app):
	app.open_modal("任期结束，城市仍在。",app.game.s.failed_reason)
	app.modal_body.add_child(W.button("返回只读地图",func():app.close_modal()));app.modal_body.add_child(W.button("导出存档",func():app.export_file("save")))
static func help(app):
	app.open_modal("在河谷里，先做一件值得做的事。","不用学代码术语。上方看城市需要什么，下方决定建什么，地图告诉你放在哪里有什么代价。")
	for pair in [["建设","新建农居、邻里中心或作坊，选择位置与运营者，再用共享施工能力成长。"],["构筑","每年和安居人口里程碑提供普通方案；主题阶段带来定向副本与蓝图升级。同宿主首份100%，追加每份33⅓%。"],["城市方向","选择五个委任主题之一。只有获得稀有重铸许可并支付DT，才可更换未完成主题。"],["时间","准备库存后选观察、推进或燃烧。DT只为额外本地时间付费；外贸跟真实在线时间，移民跟本地时间。暂停与离线冻结全部城市进程。"],["尾声","DT真实耗尽后为城市命名。此时已完成主题才获得定型建筑；仍可常速经营，也能保存只读记录后独立重开。"],["地图操作","滚轮缩放；中键或右键拖动。1观察、2建设、Esc逐层返回、空格选择推进档；P暂停。"]]:W.section(app.modal_body,pair[0],pair[1])

static func settings(app):
	app.open_modal("河谷手册与存档","MET Beta · 当前版本使用独立存档目录。")
	var row=app.option_row(app.modal_body,12)
	var a=card(row,"继续照顾这座城")
	a.add_child(W.button("显示、字号与触摸",func():app.display_options()))
	a.add_child(W.button("街区政策",func():app.policy_categories()))
	a.add_child(W.button("城市消息",func():app.open_city_messages()))
	a.add_child(W.button("市民泡泡："+("开" if app.display_settings.citizen_bubbles else "关"),func():app.display_settings.citizen_bubbles=not app.display_settings.citizen_bubbles;app.feedback_layer.toggle_bubbles(app.display_settings.citizen_bubbles);app.save_display_settings();settings(app)))
	a.add_child(W.button("发展目标与居民迁入",func():goals(app)));a.add_child(W.button("居民与工作",func():population(app)));a.add_child(W.button("方案手册",func():catalog(app)));a.add_child(W.button("怎么玩",func():help(app)))
	var b=card(row,"留住城市")
	b.add_child(W.button("已结算城市记录",func():app.open_city_records()))
	b.add_child(W.button("导出存档",func():app.export_file("save")));b.add_child(W.button("导出反馈记录",func():app.export_file("feedback")));b.add_child(W.button("导入当前版本备份",func():app.import_file()));b.add_child(W.button("打开存档目录",func():OS.shell_open(ProjectSettings.globalize_path("user://"))));b.add_child(W.button("另开一局…",func():new_game(app)))
	app.modal_body.add_child(W.button("音效："+("开" if app.game.s.settings.get("sound",true) else "关"),func():app.game.s.settings.sound=not app.game.s.settings.get("sound",true);app.save_game();settings(app)))
	app.modal_body.add_child(W.button("开发工具（使用会标记存档）",func():debug(app)))
static func new_game(app):
	app.open_modal("另开一座城市？","先导出当前城市可单独保留；本版自动保存有一份最近备份。")
	var seed_input=LineEdit.new();seed_input.text=str(int(Time.get_unix_time_from_system())%2147483000);app.modal_body.add_child(seed_input)
	app.modal_body.add_child(W.button("确认新局",func():app.restart(int(seed_input.text))))
static func debug(app):
	app.open_modal("开发工具","这里的补给会标记存档，不属于普通城市经营。")
	app.modal_body.add_child(W.button("调试补给",func():app.game.debug_grant();app.save_game();app.refresh();app.close_modal()))
static func resource_name(r:String) -> String:return P.RES.get(r,r)
