extends RefCounted
const W=preload("res://ui/widgets.gd")
const P=preload("res://ui/presentation.gd")
const C031=preload("res://ui/components031/components.gd")
const D031=preload("res://ui/components031/design.gd")
const Art=preload("res://ui/map/art.gd")
const InspectorModel=preload("res://ui/view_models/inspector_model.gd")
const Hex=preload("res://core/hex.gd")
const V=preload("res://ui/reset/desk_visuals.gd")
static func source_record(parent:Node,value):
	var text=W.label(JSON.stringify(value,"  "),13,W.MUTED);text.set_meta("source_record",true);parent.add_child(text)
static func archive_back(parent:Node,app,id:String,section:String="overview"):
	parent.add_child(W.flat(W.button("返回建筑档案",func():load("res://ui/reset/building_archive.gd").open(app,id,section))))
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
	V.card(app.modal_body,str(org.get("name","运营者")),str(org.get("summary","")),"org_"+str(offer.get("operator_org","")),W.INFO)
	var track=offer.get("slot_track",org.get("slot_track",[]));var strip=Art.slot_strip(g,track);app.modal_body.add_child(strip)
	var future=V.card(app.modal_body,"随成长解锁的槽位","序号对应成长 Tier；颜色识别领域，形状识别槽型。","bonus",W.GREEN)
	var slots=V.grid(future,maxi(1,track.size()))
	for index in track.size():
		var cell=V.card(slots,"Tier "+str(index+1));var row=W.hbox(cell,8);C031.socket_symbol(row,str(track[index]),38);row.add_child(W.label(D031.slot_label(str(track[index])),18,W.GREEN))
	var preferences=V.fold(app.modal_body,"领域偏好与运营商完整规则")
	for domain in org.get("domain_weights",{}):preferences.add_child(W.label(str(D031.domain(str(domain)).label)+" · 权重 "+str(org.domain_weights[domain]),15,W.MUTED))
	for rule in org.get("rules",[]):source_record(preferences,rule)
	var original=V.fold(app.modal_body,"当前投标原始记录");source_record(original,offer)
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
	# Reuse the source-backed archive so this deeper route reads the same
	# snapshot, resource comparisons and complete disclosures as L2 details.
	if not app.game.s.projects.has(id):app.message("建筑已变化，请重新选择。");return
	load("res://ui/reset/building_archive.gd").open(app,id,"details")
static func slot_detail(app,id:String,slot:String):
	var g=app.game
	if not g.s.projects.has(id) or not g.s.projects[id].slots.has(slot):app.message("方案位已变化，请重新选择。");return
	var p=g.s.projects[id];var iid=str(p.slots[slot]);var type=g.slot_type(id,slot)
	app.open_modal(g.project_name(id)+" · "+g.slot_label(id,slot),"安装资格在部署时检查；升级不会让已安装效果静默失效。")
	archive_back(app.modal_body,app,id,"slots")
	V.building(app.modal_body,str(p.def),g.project_name(id),g.c.operators[p.operator_org].name+" · Tier "+str(p.get("tier",1)))
	var relationship=V.card(app.modal_body,"装配关系","建筑承载槽位，槽位承载获得的副本。","bonus",W.INFO)
	var relation=W.hbox(relationship,14);relation.set_meta("desk_visual","flow")
	var host=V.card(relation,"建筑宿主","","home",W.INFO);host.add_child(W.label(g.project_name(id),18))
	relation.add_child(W.label("→",26,W.MUTED,false))
	var socket=V.card(relation,g.slot_label(id,slot));var socket_row=W.hbox(socket,8);C031.socket_symbol(socket_row,type,42);socket_row.add_child(W.label(D031.slot_label(type),18,W.INFO))
	if type=="Flex":socket.add_child(W.label("模块化设计 → 生产 / 物流",14,W.MUTED))
	relation.add_child(W.label("→",26,W.MUTED,false))
	var instance=V.card(relation,"当前方案","","bonus",W.GREEN)
	if iid=="":
		instance.add_child(W.label("空闲插口",22,W.MUTED));V.badge(instance,"可安装适用副本",W.INFO)
		var v=V.card(app.modal_body,"取得副本 → 部署许可 → 安装","从年度、人口或主题奖励中获得副本。","bonus",W.GREEN)
		v.add_child(W.label("实际资格以部署页检查为准；这次查看不消耗副本。",14,W.MUTED))
		v.add_child(W.primary(W.button("查看部署许可",func():app.close_modal();app.desk.open("city040","permits"))))
		return
	if not g.s.bonus_instances.has(iid):app.message("副本记录缺失，请重新选择。");return
	var ins=g.s.bonus_instances[iid];var bid=str(ins.bonus_id)
	var model=InspectorModel.build(g,app.shot,id);var state={}
	for x in model.slots:
		if x.stable_id==slot:state=x
	var contract=str(ins.get("contract_org",ins.provider_org))
	instance.add_child(W.label(g.c.bonuses[bid].name,21,W.GREEN))
	var status=str(state.get("reason","")) if state.get("state","")=="conditional" else ("已装载 · 自营" if contract==p.operator_org else "已装载 · 外部方案")
	if not ins.get("active",false):status="已停用"
	V.badge(instance,status,W.GOLD if state.get("state","")=="conditional" else W.GREEN)
	var columns=V.grid(app.modal_body,2)
	C031.bonus(columns,g,bid,Callable(),"offer",{"host":"building","provider_org":contract,"provider_name":g.c.operators.get(contract,{}).get("name",contract)})
	var source=V.card(columns,"来源与当前合同","提供方历史和当前签约分别保留。","org_"+contract,W.INFO)
	for pair in [["原提供方",str(ins.provider_org)],["当前签约方",contract],["建筑运营者",str(p.operator_org)]]:
		var line=W.hbox(source,10);V.icon(line,"org_"+str(pair[1]),26,W.INFO);line.add_child(W.label(str(pair[0])+" · "+str(g.c.operators.get(pair[1],{}).get("name",pair[1])),16))
	var count=g.Bonus.active_count(g,bid,"building",id)
	V.badge(source,"本宿主 %d 份同名方案 · 数值总强度 %.3f 倍"%[count,g.Rules.copy_strength(g,count,bid)],W.GREEN)
	source.add_child(W.label("结构效果不重复；数值代价随副本强度同步缩放。",14,W.MUTED))
	var actions=V.card(app.modal_body,"装配操作","卸下停止效果，已经消耗的副本不会退回。","settings",W.GOLD)
	actions.add_child(W.button("卸下当前方案…",func():confirm_unmount(app,id,slot),p.status!="running"))
	actions.add_child(W.button("完整规则词条",func():bonus_info(app,bid,func():slot_detail(app,id,slot))))
	var record=V.fold(app.modal_body,"副本与部署的完整来源记录")
	source_record(record,ins)
	for stamp in g.s.block.get("deployed_bonuses",{}).get(bid,{}).get("history",[]):
		if str(stamp.get("instance_id",""))==iid:source_record(record,stamp)
static func confirm_unmount(app,id:String,slot:String):
	var g=app.game
	if not g.s.projects.has(id):app.message("建筑已变化，请重新选择。");return
	var iid=str(g.s.projects[id].slots.get(slot,""))
	if iid=="":return
	var bid=str(g.s.bonus_instances[iid].bonus_id)
	app.open_modal("卸下「"+g.c.bonuses[bid].name+"」？","先检查真实依赖，再停止当前方案。")
	V.building(app.modal_body,str(g.s.projects[id].def),g.project_name(id),g.slot_label(id,slot))
	V.flow(app.modal_body,[{"id":"bonus","label":"当前方案","value":g.c.bonuses[bid].name},{"id":"settings","label":"确认卸下","value":"效果停止","color":W.GOLD},{"id":"bonus","label":"原槽位","value":"恢复空闲"}],"已消耗的部署副本不会退回；住户按剩余真实容量重新安置。")
	var dependencies=[]
	for rule in g.c.bonuses[bid].rules:
		if str(rule.get("op",""))=="slot_add" and str(g.s.projects[id].slots.get(str(rule.get("name","")),""))!="":dependencies.append(str(rule.name))
	if not dependencies.is_empty():
		var depend=V.card(app.modal_body,"先卸下依赖的弹性方案","依赖槽仍有副本；当前卸下会被真实规则拒绝。","warning",W.GOLD)
		for dependent in dependencies:depend.add_child(W.button("查看 "+g.slot_label(id,dependent),func():slot_detail(app,id,dependent)))
	var actions=W.hbox(app.modal_body,10)
	var confirm=W.primary(W.button("确认卸下",func():
		if str(app.game.s.projects.get(id,{}).get("slots",{}).get(slot,""))!=iid:app.modal_subtitle.text="槽位内容已变化，请返回重新检查。";return
		var e=app.game.clear_slot(id,slot)
		if e=="":app.act("");slot_detail(app,id,slot)
		else:app.act(e)))
	confirm.size_flags_horizontal=Control.SIZE_EXPAND_FILL;actions.add_child(confirm)
	actions.add_child(W.button("取消 · 回到槽位",func():slot_detail(app,id,slot)))
static func catalog(app):
	app.open_modal("城市档案 › 方案图鉴","颜色识别领域，插口形状识别类型；点卡进入完整规则。")
	var domains=["all","residential","agriculture","industrial","commercial","public","infrastructure","general"]
	var legend=V.grid(app.modal_body,7)
	for domain in domains.slice(1):
		var token=D031.domain(domain);var cell=V.card(legend,"","",str(token.icon),Color(token.ink));cell.add_child(W.label(str(token.label),14,Color(token.ink)))
	var bar=W.hbox(app.modal_body,10);V.icon(bar,"search",26,W.INFO)
	var search=LineEdit.new();search.placeholder_text="搜索方案名称 / ID";search.custom_minimum_size.y=48;search.size_flags_horizontal=Control.SIZE_EXPAND_FILL;search.text=app.catalog_state.query;bar.add_child(search)
	var filter=OptionButton.new();filter.custom_minimum_size.y=48;filter.add_item("全部领域")
	for id in domains.slice(1):filter.add_item(D031.domain(id).label)
	filter.selected=maxi(0,domains.find(app.catalog_state.domain));bar.add_child(filter)
	var count=W.label("",14,W.MUTED);app.modal_body.add_child(count)
	var grid=V.grid(app.modal_body,3)
	var rebuild=func():
		W.clear(grid);var n=0
		var order=["P01","P02","L01","P06","O03","M01"]
		for bid in app.game.Rules.ids(app.game.c.bonuses):
			if not bid in order:order.append(bid)
		for bid in order:
			var data=D031.card(app.game,bid);var query=search.text.strip_edges().to_lower()
			if query!="" and not query in (str(data.name)+bid).to_lower():continue
			if app.catalog_state.domain!="all" and data.visual_domain!=app.catalog_state.domain:continue
			n+=1;C031.bonus(grid,app.game,bid,func():bonus_info(app,bid,func():catalog(app)),"offer")
		count.text="显示 %d / %d · 点卡看完整规则"%[n,app.game.c.bonuses.size()]
		if n==0:V.card(grid,"没有匹配的方案","调整名称、ID 或领域筛选。","search",W.MUTED)
	search.text_changed.connect(func(value):app.catalog_state.query=value;rebuild.call())
	filter.item_selected.connect(func(index):app.catalog_state.domain=domains[index];rebuild.call())
	rebuild.call()
static func bonus_info(app,bid:String,back:Callable=Callable()):
	var g=app.game
	if not g.c.bonuses.has(bid):app.message("方案词条已变化，请重新选择。");return
	var def=g.c.bonuses[bid];var data=D031.card(g,bid);var token=D031.domain(str(data.get("visual_domain","general")))
	app.open_modal("方案图鉴 › "+str(def.name),"关键效果、装配关系与完整 Rule 来自当前规则。")
	if back.is_valid():app.modal_body.add_child(W.button("回到上一页",back))
	var columns=V.grid(app.modal_body,2)
	C031.bonus(columns,g,bid,Callable(),"installed",{"provider_name":str(token.label)})
	var effects=V.card(columns,"关键效果 · 当前蓝图单份基准",str(token.label)+" · "+bid,str(token.icon),Color(token.ink))
	for effect in data.get("effect_rows",[]):C031.effect(effects,effect,str(data.get("visual_domain","general")))
	var bonus=g.Bonus.card(g,bid)
	V.badge(effects,str(bonus.get("grade","基础"))+" · 可用副本 %d"%int(bonus.get("copies",0)),W.INFO)
	var eligibility=V.card(app.modal_body,"宿主 → 合适槽位 → 方案","部署时检查当前建筑和位置；槽位类型不代表稀有度。","bonus",W.INFO)
	var hosts=[]
	for host in def.get("hosts",[]):hosts.append(D031.host_label(str(host)))
	var types=[]
	for type in def.get("allowed_slots",[]):types.append(D031.slot_label(str(type)))
	V.flow(eligibility,[{"id":"home","label":"允许宿主","value":" / ".join(hosts)},{"id":"layers","label":"允许槽型","value":" / ".join(types) if not types.is_empty() else "宿主方案位"},{"id":str(token.icon),"label":"挂载方案","value":str(def.name),"color":Color(token.ink)}])
	var sockets=W.hbox(eligibility,10)
	for type in def.get("allowed_slots",[]):C031.socket_symbol(sockets,str(type),32)
	eligibility.add_child(W.label("对象特质 · "+g.c.tags_text(def.get("traits",[])) if not def.get("traits",[]).is_empty() else "无额外对象特质要求",14,W.MUTED))
	eligibility.add_child(W.label(str(bonus.get("preview_reason","")),14,W.INFO))
	var explanation=V.fold(app.modal_body,"完整解释与叠加说明")
	explanation.add_child(W.label(str(def.summary),16))
	explanation.add_child(W.label(str(bonus.get("stacking_note","")),14,W.MUTED))
	var gates=V.fold(app.modal_body,"完整挂载资格与条件")
	source_record(gates,{"hosts":def.get("hosts",[]),"allowed_slots":def.get("allowed_slots",[]),"traits":def.get("traits",[]),"install_gate":def.get("install_gate",def.get("gate",{}))})
	var rule_box=V.fold(app.modal_body,"完整 Rule 定义 · %d 条"%def.get("rules",[]).size())
	for rule in def.get("rules",[]):
		var rule_group=V.card(rule_box,str(rule.get("id","规则")),str(rule.get("op","")),"layers",W.INFO)
		source_record(rule_group,rule)
	var current_rules=V.fold(app.modal_body,"当前蓝图单份 Rule · 含研究升级")
	for rule in g.Rules.bonus_rules(g,bid):source_record(current_rules,rule)
static func demolition(app,id:String):
	var g=app.game
	if not g.s.projects.has(id):app.message("建筑已变化，请重新选择。");return
	var p:Dictionary=g.s.projects[id];var residents=0.;var installed=0
	for group in g.s.groups.values():
		if str(group.host)==id:residents+=float(group.count)
	for iid in p.get("slots",{}).values():
		if str(iid)!="":installed+=1
	app.open_modal("拆除「"+g.project_name(id)+"」？","核对费用、住户与方案影响，再执行拆除。")
	V.building(app.modal_body,str(p.def),g.project_name(id),g.c.operators[p.operator_org].name+" · Tier "+str(p.get("tier",1)))
	var costs=V.grid(app.modal_body,3)
	V.metric(costs,"currency","拆除费用","20 通货","当前预算 %.2f"%float(g.s.currency),W.GOLD)
	V.metric(costs,"population","重新安置","%.1f 人"%residents,"保留居民及其健康、教育状态。",W.INFO)
	V.metric(costs,"bonus","停止已装方案","%d 份"%installed,"副本随建筑卸除；许可不退回。",W.GOLD)
	V.flow(app.modal_body,[{"id":"home","label":"当前建筑","value":g.project_name(id)},{"id":"warning","label":"拆除后","value":"地块清场","color":W.GOLD},{"id":"population","label":"原住户","value":"重新安置","color":W.INFO}],"先尝试迁入现有住房，剩余居民进入待安置住区；本楼岗位撤销，其他工作关系保留。")
	var inventory=V.fold(app.modal_body,"当前库存与拆除处理")
	for resource in p.get("inventory",{}):
		if float(p.inventory[resource])<=0:continue
		var row=W.hbox(inventory,10);V.icon(row,str(resource),24,W.INFO);row.add_child(W.label(P.RES.get(str(resource),str(resource))+" · %.3f"%float(p.inventory[resource]),16))
	inventory.add_child(W.label("执行时先尝试转入现有可用仓储；不足的部分会损耗，实际搬运与损耗写入拆除事件。",14,W.MUTED))
	var actions=W.hbox(app.modal_body,10)
	var confirm=W.primary(W.button("确认拆除",func():
		var e=app.game.demolish(id)
		if e=="":app.selected_project="";app.inspector_key="";app.save_game();app.close_modal()
		else:app.act(e)))
	confirm.size_flags_horizontal=Control.SIZE_EXPAND_FILL;actions.add_child(confirm)
	actions.add_child(W.button("取消 · 返回建筑",func():load("res://ui/reset/building_archive.gd").open(app,id)))
static func award(app):
	var g=app.game;app.open_modal("来生 · 街区建成","从一处登陆点，到一个能够独立生活的地方。你选择的街区目标已经兑现。")
	var v=card(app.modal_body,g.c.data.city_goals[g.s.city_goal.route].name,"居民 %.1f · 幸福 %.0f"%[g.s.metrics.population,g.s.metrics.happiness],"你获得了为这片街区命名的权利。可以现在命名，也可以稍后继续经营。")
	v.add_child(W.primary(W.button("现在命名",func():app.close_modal();app.desk.open("city040","block"))))
	v.add_child(W.button("稍后 · 继续经营",func():app.close_modal()))
	v.add_child(W.button("为城市留一份存档",func():app.export_file("save")))
static func governance(app):
	app.open_modal("社区需要一次整改","生活与财政持续恶化。可以先修复供给，也可以用80通货争取缓冲。")
	var choices=V.grid(app.modal_body,2)
	var repair=V.card(choices,"整改与继续经营","支付后争取缓冲，供给问题仍需处理。","currency",W.GOLD)
	V.metric(repair,"currency","整改费用","80 通货","当前预算 %.2f"%float(app.game.s.currency),W.GOLD)
	repair.add_child(W.primary(W.button("80通货整改",func():
		var e=app.game.Progress.crisis(app.game,"repair");app.act(e)
		if e=="":app.close_modal())))
	var leave=V.card(choices,"离任并保留城市","结束任期后，保留地图和存档记录。","home",W.INFO)
	leave.add_child(W.button("离任并保留地图",func():app.game.Progress.crisis(app.game,"leave");app.save_game();failure(app)))
	app.modal_body.add_child(W.button("先回到城市修复供给",func():app.close_modal()))
static func failure(app):
	app.open_modal("任期结束，城市仍在。",app.game.s.failed_reason)
	V.flow(app.modal_body,[{"id":"home","label":"城市","value":"保留地图"},{"id":"layers","label":"记录","value":"只读查看"},{"id":"ledger","label":"存档","value":"可导出","color":W.INFO}])
	app.modal_body.add_child(W.button("返回只读地图",func():app.close_modal()));app.modal_body.add_child(W.button("导出存档",func():app.export_file("save")))
static func help(app):
	app.open_modal("在河谷里，先做一件值得做的事。","不用学代码术语。上方看城市需要什么，下方决定建什么，地图告诉你放在哪里有什么代价。")
	V.steps(app.modal_body,["选择位置","比较投标","共享施工","观察实际运行"],0)
	var topics=V.grid(app.modal_body,2)
	for pair in [["construction","建设","新建农居、邻里中心或作坊，选择位置与运营者，再用共享施工能力成长。"],["bonus","构筑","每年和安居人口里程碑提供普通方案；主题阶段带来定向副本与蓝图升级。同宿主首份100%，追加每份33⅓%。"],["theme","城市方向","选择五个委任主题之一。只有获得稀有重铸许可并支付DT，才可更换未完成主题。"],["time","时间","准备库存后选观察、推进或燃烧。DT只为额外本地时间付费；外贸跟真实在线时间，移民跟本地时间。暂停与离线冻结全部城市进程。"],["world","尾声","DT真实耗尽后为城市命名。此时已完成主题才获得定型建筑；仍可常速经营，也能保存只读记录后独立重开。"],["layers","地图操作","滚轮缩放；中键或右键拖动。1观察、2建设、Esc逐层返回、空格选择推进档；P暂停。"]]:
		var topic=V.card(topics,str(pair[1]),"",str(pair[0]),W.INFO);topic.add_child(W.label(str(pair[2]),16))
	app.modal_body.add_child(W.button("返回河谷手册",func():settings(app)))

static func settings(app):
	app.open_modal("河谷手册与存档","MET Beta · 当前版本使用独立存档目录。")
	var row=V.grid(app.modal_body,3)
	var a=V.card(row,"继续照顾这座城","按主题打开城市工作台。","home",W.GREEN)
	a.add_child(W.icon_button("theme","发展目标与居民迁入",func():goals(app)));a.add_child(W.icon_button("population","居民与工作",func():population(app)));a.add_child(W.icon_button("policy","街区政策",func():app.policy_categories()));a.add_child(W.icon_button("bonus","方案手册",func():catalog(app)))
	var guide=V.card(row,"设置与手册","城市消息、阅读与显示。","settings",W.INFO)
	guide.add_child(W.icon_button("settings","显示设置",func():app.display_options()))
	guide.add_child(W.icon_button("layers","城市消息档案",func():app.open_city_messages()))
	guide.add_child(W.icon_button("info","怎么玩",func():help(app)))
	var b=V.card(row,"留住城市","导出、恢复与已结算记录。","ledger",W.GOLD)
	b.add_child(W.icon_button("ledger","已结算城市记录",func():app.open_city_records()))
	b.add_child(W.button("导出存档",func():app.export_file("save")));b.add_child(W.button("导出反馈记录",func():app.export_file("feedback")));b.add_child(W.button("导入当前版本备份",func():app.import_file()));b.add_child(W.button("打开存档目录",func():OS.shell_open(ProjectSettings.globalize_path("user://"))));b.add_child(W.button("另开一局…",func():new_game(app)))
	app.modal_body.add_child(W.button("音效："+("开" if app.game.s.settings.get("sound",true) else "关"),func():app.game.s.settings.sound=not app.game.s.settings.get("sound",true);app.save_game();settings(app)))
	app.modal_body.add_child(W.button("开发工具（使用会标记存档）",func():debug(app)))
static func new_game(app):
	app.open_modal("另开一座城市？","先导出当前城市可单独保留；本版自动保存有一份最近备份。")
	V.flow(app.modal_body,[{"id":"home","label":"当前城市","value":str(app.game.s.block.get("display_name","河谷街区"))},{"id":"ledger","label":"留存记录","value":"导出存档","color":W.INFO},{"id":"world","label":"新局","value":"从聚落开始","color":W.GREEN}],"确认后重新开始；需要单独保留当前城市时，先导出存档。")
	app.modal_body.add_child(W.button("先导出当前存档",func():app.export_file("save")))
	var seed=V.card(app.modal_body,"新城市种子","相同种子复现起点；直接输入整数。","world",W.INFO)
	var seed_input=LineEdit.new();seed_input.name="NewCitySeed";seed_input.text=str(int(Time.get_unix_time_from_system())%2147483000);seed_input.custom_minimum_size.y=44;seed.add_child(seed_input)
	var actions=W.hbox(app.modal_body,10)
	var confirm=W.primary(W.button("确认新局",func():
		if not seed_input.text.strip_edges().is_valid_int():app.act("请输入整数城市种子。");return
		var value=int(seed_input.text.strip_edges());app.restart(value)))
	confirm.size_flags_horizontal=Control.SIZE_EXPAND_FILL;actions.add_child(confirm)
	actions.add_child(W.button("取消 · 保留当前城市",func():settings(app)))
static func debug(app):
	app.open_modal("开发工具","这里的补给会标记存档，不属于普通城市经营。")
	V.flow(app.modal_body,[{"id":"home","label":"当前城市","value":"继续保留"},{"id":"material","label":"开发补给","value":"写入真实状态","color":W.GOLD},{"id":"warning","label":"存档记录","value":"标记开发工具","color":W.GOLD}])
	var actions=W.hbox(app.modal_body,10)
	actions.add_child(W.button("调试补给",func():app.game.debug_grant();app.save_game();app.refresh();app.close_modal()))
	actions.add_child(W.button("返回手册",func():settings(app)))
static func resource_name(r:String) -> String:return P.RES.get(r,r)
