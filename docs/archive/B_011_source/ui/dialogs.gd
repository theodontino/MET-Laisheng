extends RefCounted
const W=preload("res://ui/widgets.gd")
const Inv=preload("res://core/inventory.gd")
const MapView=preload("res://ui/map_view.gd")
const Hex=preload("res://core/hex.gd")
static func card(parent:Node,name:String,tags:String,body:String) -> VBoxContainer:
	var panel=W.panel(parent,Color("203f43"));panel.custom_minimum_size.x=310;panel.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	var v=W.vbox(panel,12)
	v.add_child(W.label(name,23,W.TEXT));v.add_child(W.label(tags,12,W.GOLD))
	var text=W.label(body,15,W.TEXT);text.custom_minimum_size.y=102;v.add_child(text)
	return v
static func start(app):
	app.open_modal("B 人口与生活 · 0.1.1", "一次地方治理任期，从选择你想建设的地方开始。固定地理，薄随机起源。")
	var row=W.hbox(app.modal_body,12)
	var descs=["河岸蜿蜒，冲积平地充足。水岸项目与公共空间会争同一片好地。","旧村旁的林地更多。建材容易起步，也要关注住房、岗位与生活需求。","岩丘与高台较多，采石产能有优势，但有粉尘。山地并不会替你解决食物和公共服务。"]
	for i in 3:
		var v=card(row,app.BLOCKS[i],"固定河谷地图  /  候选 %d"%(i+1),descs[i])
		var mini=MapView.new();mini.custom_minimum_size=Vector2(270,185);mini.mouse_filter=Control.MOUSE_FILTER_IGNORE
		var dummy=app.Game.new();dummy.new_game(1,0);dummy.s.cells=Hex.make_map(i);mini.game=dummy;v.add_child(mini)
		v.add_child(W.button("从这里开始",func():
			var error=app.game.select_block(i)
			if error=="":app.save_game();app.refresh();origin(app)
			else:app.message(error)))
	app.modal_body.add_child(W.label("首版只开发你选中的一个Block。没有账号、网络、模型或商城；这里所有数值仍等待实测。",12,W.MUTED))
static func origin(app):
	var g=app.game
	var reveal={"stable_ground":"工程勘测：地基条件稳定，本次新项目材料需求降低10%。","spring":"工程勘测：发现暗泉，基础食物补充略有增加。","alluvial":"工程勘测：冲积层分布已知；土地优势由具体场地与项目共同兑现。"}[g.s.reveal]
	app.open_modal("谁最先在这里定居？",reveal+"  起源候选已经锁定；退出重进不会重新发牌。")
	var row=W.hbox(app.modal_body,14)
	for oid in g.s.origin_choices:
		var o=g.c.origins[oid]
		var v=card(row,o.name,"历史起点，不是终身职业",o.story)
		W.section(v,"已有基石","河谷合院 + "+g.c.projects[o.anchor].name)
		W.section(v,"共同安全底线","18位居民，实际入住24人合院\n%.0f通货 · %.0f DT\n%.0f食物 · %.0f材料\n基础能源与小规模家庭自给"%[g.c.balance.initial_currency,g.c.balance.initial_dt,g.c.balance.initial_stock.food,g.c.balance.initial_stock.material])		
		W.section(v,"起源差异","通货 +%s / DT +%s\n食物 +%s / 材料 +%s"%[o.cash,o.dt,o.food,o.material])
		v.add_child(W.label(o.hint,13,W.GREEN))
		v.add_child(W.button("接手 · "+o.name,func():
			var error=g.choose_origin(oid)
			if error=="":app.save_game();app.overlay.hide();app.refresh();app.message("18位居民已落脚。按2圈地，选住房、农业或建材Tag招标；底部可展开逐户生活。")
			else:app.message(error)))
static func tender(app):
	var g=app.game;var d=g.s.draft
	if d.is_empty():return
	var title={"development":"回应这片土地的方案","upgrade":"它已经活成这样，接下来呢？","fusion":"两栋项目，长出一段新关系"}[d.kind]
	var subtitle="你的招标偏好："+g.c.tags_text(d.tags) if not d.tags.is_empty() else "项目的历史、地形与当前状态，决定这次可遇见的方向。"
	if d.kind=="development":subtitle+="  /  %d格%s场地"%[d.cells.size(),"待开发"]
	app.open_modal(title,subtitle)
	var row=W.hbox(app.modal_body,10)
	for oid in d.offers:
		var def=g.Draft.definition(g,d.kind,oid)
		var cost=g.Draft.offer_cost(g,oid)
		var v=card(row,def.name,g.c.tags_text(def.get("tags",[])) if d.kind!="fusion" else "关系槽 · 两个项目仍然独立",def.desc)
		if d.kind=="development":
			var summary=[]
			for output in def.outputs:
				summary.append("产出 %s %.1f/h"%[resource_name(output.resource),output.rate])
			for input in def.inputs:
				summary.append("消耗 %s %.1f/h %s"%[resource_name(input.resource),input.rate,""])
			if def.population>0:summary.append("居住容量 +%.0f"%def.population)
			if def.get("supply_energy",0)>0:summary.append("能源 +%.0f"%def.supply_energy)
			for service in def.services:summary.append("%s容量 +%.0f"%[{"care":"医疗","education":"教育","market":"消费","leisure":"休闲"}.get(service,service),def.services[service]])
			summary.append("岗位 %.0f · 维护 %.1f/h"%[def.get("jobs",0),def.upkeep])
			summary.append("噪音 %.0f · 污染 %.0f（影响两格内住户）"%[def.get("noise",0),def.get("pollution",0)])
			v.add_child(W.label("\n".join(summary),13,W.GREEN))
			if d.cells.size()>1:v.add_child(W.label("以上为单格基准；本场地规模系数×%.2f，实际需求与容量同步增加。"%(1.0+.13*(d.cells.size()-1)),11,W.MUTED))
		else:
			v.add_child(W.label("普通建设结果可预见；你选择的是新机制，而非施工成功的骰子。",12,W.MUTED))
		W.spacer(v)
		W.section(v,"投入与代价","通货 %.0f（开工支付）\n材料 %.1f\n工作时间 %.1fh（随供给推进）"%[cost.currency,cost.material,cost.hours])
		var disabled=g.s.currency<cost.currency or (d.kind=="fusion" and Inv.total(g.s.stocks,"material")<cost.material)
		var b=W.button("采纳方案 · %.0f通货"%cost.currency,func():app.accept_offer(oid),disabled)
		b.name="Accept_"+oid;v.add_child(b)
	var footer=W.hbox(app.modal_body,12)
	if d.kind!="fusion":
		var cost=g.Draft.reroll_cost(g)
		var b=W.button("重新开标 · %.0f通货%s"%[cost.currency," + %.1fDT"%cost.dt if cost.dt>0 else ""],func():
			var error=g.reroll()
			if error=="":app.save_game();app.refresh();tender(app)
			else:app.modal_subtitle.text=error,
			g.s.currency<cost.currency or g.s.dt<cost.dt)
		b.name="RerollButton";footer.add_child(b)
		footer.add_child(W.button("撤回并重新规划（同价）",func():
			var error=g.Draft.withdraw(g)
			if error=="":app.save_game();app.overlay.hide();app.refresh()
			else:app.modal_subtitle.text=error))
	if d.kind=="fusion":
		footer.add_child(W.button("稍后再议 · 保留关系，不占用招标",func():g.Draft.withdraw(g);app.save_game();app.close_modal()))
	else:footer.add_child(W.button("保留这份投标，回地图",func():app.close_modal()))
	app.modal_body.add_child(W.label("关闭面板免费保留手牌。已生成候选、重招次数与随机状态同时保存；换场地不会免费洗牌。",12,W.MUTED))
static func upgrade_preference(app,id:String):
	app.open_modal("发起里程碑升级", "允许一项软偏好。建筑怎么运行过、与谁相邻、以前选过什么，仍然影响这一轮。")
	var row=W.hbox(app.modal_body,8)
	row.add_child(W.button("看看它自然长出的方向",func():_open_upgrade(app,id,"")))
	var grid=GridContainer.new();grid.columns=3;app.modal_body.add_child(grid)
	for tag in app.game.s.policy_tags:
		grid.add_child(W.button(app.game.c.tag_name(tag)+" · 软偏好",func():_open_upgrade(app,id,tag)))
	W.section(app.modal_body,"不是固定职业树","软分叉改变未来权重，但不会永久封死所有其他路线。获得新的邻接、词条或关系，仍可能让旧选择产生新的意义。")
static func _open_upgrade(app,id:String,tag:String):
	var error=app.game.create_upgrade(id,tag)
	if error=="":app.save_game();tender(app)
	else:app.modal_subtitle.text=error
static func opportunity(app):
	var g=app.game
	if g.s.events.is_empty():return
	var e=g.s.events[0];var def=g.c.opportunities[e.id]
	app.open_modal("施工中，一个新的提议", "DT只扣到了这一刻。接受或拒绝都可以；不选额外方案不是失误。")
	app.modal_body.add_child(W.label(def.name,24,W.GOLD))
	app.modal_body.add_child(W.label(def.body,18,W.TEXT))
	W.section(app.modal_body,"额外代价","%.0f通货 · %.0f材料 · %.2fh工作"%[def.cost,def.material,def.hours])
	var row=W.hbox(app.modal_body,12)
	row.add_child(W.button("采纳这项改变",func():
		var error=g.opportunity("accept")
		if error=="":app.save_game();app.overlay.hide();app.refresh()
		else:app.modal_subtitle.text=error,g.s.currency<def.cost))
	row.add_child(W.button("按原方案继续 · 无额外代价",func():g.opportunity("skip");app.save_game();app.overlay.hide();app.refresh()))
static func resources(app):
	var g=app.game;var m=g.s.metrics
	app.open_modal("城市供给与财政", "开局只有食物、材料、能源。资源标签已停用；同类库存通用，地形和建筑Tag仍保留。")
	var row=W.hbox(app.modal_body,12)
	var visible=["food","material"]
	if int(g.s.stage)>=2:visible.append("goods")
	for r in visible:
		var v=card(row,resource_name(r),"%.1f / %.0f 仓容"%[Inv.total(g.s.stocks,r),g.stock_cap(r)],"净变化 %+.2f / 本地小时。生产、居民消费、商业和施工共享有限库存。"%m.get("flows",{}).get(r,0))
		for id in g.s.projects:
			var p=g.s.projects[id]
			if float(p.outputs_last.get(r,0))>0:v.add_child(W.label(g.c.projects[p.def].name+" +%.2f/h"%p.outputs_last[r],13,W.GREEN))
	W.section(app.modal_body,"能源：用量 / 容量","%.1f / %.1f。缺工也会影响发电；已建能源容量不是永远满载。"%[m.get("energy_need",0),m.get("energy",0)])
	W.section(app.modal_body,"人口带来需求，也带来税收","实际人口 %.1f · 居民食物需求 %.2f/h\n实际收入 %.2f/h · 固定维护 %.2f/h · 净现金流 %+.2f/h"%[m.get("population",0),float(m.get("population",0))*float(g.c.balance.population.food_per_person),m.get("income",0),m.get("upkeep",0),m.get("cashflow",0)])
	W.section(app.modal_body,"消费不是凭空产钱","商业按实际服务人数获得税收。安居社区以本地食物交换为基础；自愿进入分工城镇后，商业逐渐需要低级制成品。中高级制成品暂不进入这个首块切片。")

static func population(app):
	var g=app.game;var m=g.s.metrics
	app.open_modal("居民与城市生活", "人口住在住宅里；工作在Block内自动匹配，公共服务有真实半径和容量，不模拟小人寻路。")
	W.section(app.modal_body,g.Population.STAGES[int(g.s.get("stage",0))],"实际居民 %.1f / 住房 %.0f  ·  劳动力 %.1f / 岗位 %.1f\n就业 %.1f · 待安置 %.1f · 本地迁入 +%.2f人/h\n健康 %.0f · 教育 %.0f · 幸福 %.0f"%[m.get("population",0),m.get("housing",0),m.get("workforce",0),m.get("jobs",0),m.get("employed",0),m.get("homeless",0),m.get("migration",0),m.get("health",78),m.get("education",12),m.get("happiness",72)])
	if int(g.s.get("stage",0))==0:
		W.section(app.modal_body,"先安顿下来","人口到22、供给稳定半小时后，医疗/休闲/商业/教育方向开放。对应新需求有3本地小时缓冲；制成品此时仍不存在。")
	elif int(g.s.stage)==1:
		W.section(app.modal_body,"下一阶段可以自己决定","30人口、平均教育25、基础供给稳定1h后可进入分工城镇，才启用低级制成品和制造业。可以不升级阶段、暂停迁入，维持一个好住的小社区。")
		app.modal_body.add_child(W.button("进入分工城镇",func():
			var e=g.stage_up()
			if e=="":app.save_game();app.refresh();population(app)
			else:app.modal_subtitle.text=e,not g.Population.can_town(g)))
	else:W.section(app.modal_body,"首块只做到初步分工","制造业消耗原料生产低级制成品；教育影响工业技能适配。中高级制成品与托卡马克之冠属于后续发展，皇冠是City结局。")
	if int(g.s.get("stage",0))>=1:
		var row=W.hbox(app.modal_body,12)
		for k in g.Population.SERVICES:
			var box=W.vbox(row,4);box.size_flags_horizontal=Control.SIZE_EXPAND_FILL
			var served=float(m.get("service_served",{}).get(k,0));var n=maxf(1,float(m.get("population",0)))
			box.add_child(W.label(g.Population.LABELS[k]+" %.0f / %.0f人"%[served,n],15,W.TEXT));box.add_child(W.bar(served/n))
		W.section(app.modal_body,"教育与知识是两件事","学校实际覆盖居民 → 教育逐渐增长 → 改善技能型岗位效率。有效教学/研究另产生知识 → 花知识与通货开展节能、循环、技能研究。不是教育值自动变成知识库存。")
		app.modal_body.add_child(W.button("查看知识与研究",func():research(app)))
	W.section(app.modal_body,"他们住在哪")
	for id in g.s.projects:
		var p=g.s.projects[id];var st=g.project_stats(id)
		if st.population<=0:continue
		var text="%s · %.1f / %.0f人  |  健康 %.0f → %.0f  幸福 %.0f → %.0f  教育 %.0f"%[g.c.projects[p.def].name,p.residents,st.population,p.health,p.health_target,p.happiness,p.happiness_target,p.education]
		app.modal_body.add_child(W.button(text,func():app.selected_project=id;app.close_modal()))
	W.section(app.modal_body,"他们在哪工作","基础家庭自给提供4个岗位，其余来自实际运行项目；劳动力暂取人口65%。住宅本身不产生空置税收。")
	for id in g.s.projects:
		var p=g.s.projects[id];var st=g.project_stats(id)
		if st.jobs<=0:continue
		app.modal_body.add_child(W.label("%s  ·  %.1f / %.1f岗位  ·  技能适配 %.0f%%  ·  %s"%[g.c.projects[p.def].name,p.workers,st.jobs,100*float(p.skill_ratio),p.bottleneck],13,W.TEXT))
	W.section(app.modal_body,"满意支持与政治背书","满意支持缓慢趋向居民真实幸福。连续严重短缺、低幸福或赤字超过3本地小时，才逐渐侵蚀希望城背书；不是一次短缺就双重扣血。离线不模拟这些生活损耗。")

static func governance(app):
	var g=app.game
	app.open_modal("授权与居民：两种支持", "拒绝一项邀约不算失信；真实食物、照护、财政问题仍会影响居民和希望城。")
	var head=W.hbox(app.modal_body,18)
	for pair in [["希望城授权","mandate"],["地方支持","support"]]:
		var name=pair[0];var key=pair[1]
		var v=W.vbox(head,7);v.size_flags_horizontal=Control.SIZE_EXPAND_FILL
		v.add_child(W.label("%s  %.0f / 100"%[name,g.s[key]],21,W.TEXT));v.add_child(W.bar(float(g.s[key])/100))
	if g.s.crisis.get("active",false):
		W.section(app.modal_body,"治理危机 · 有代价的选择","本地时间 %.1fh 后复核。双低或任一极低会触发危机，不是DT用尽。"%maxf(0,float(g.s.crisis.review)-float(g.s.local_hours)))
		var row=W.hbox(app.modal_body,10)
		row.add_child(W.button("稳健修复 · 140通货 + 8食物",func():
			var error=g.crisis_action("repair")
			if error=="":app.save_game();app.refresh();governance(app)
			else:app.modal_subtitle.text=error))
		row.add_child(W.button("紧急改革 · 65%成功 / 35%罢免",func():confirm_risk(app)))
		row.add_child(W.button("主动交接 · 结束任期",func():confirm_leave(app)))
	for t in g.s.contracts.values():
		if t.status=="locked":continue
		var p=W.panel(app.modal_body,Color("1c3c42"));var v=W.vbox(p,7)
		v.add_child(W.label(t.name,18,W.TEXT));v.add_child(W.label(t.body,13,W.MUTED))
		var status={"offered":"可承诺","accepted":"已承诺","completed":"已兑现","failed":"已失信","expired":"未承诺，窗口已过","declined":"已婉拒"}.get(t.status,t.status)
		v.add_child(W.label("%s  ·  外部窗口剩余 %.1fh  ·  奖励 %.0f通货 / %.0fDT"%[status,maxf(0,(float(t.deadline)-float(g.s.global_now))/3600),t.currency,t.dt],12,W.GOLD))
		var row=W.hbox(v,8)
		if t.status=="offered":
			row.add_child(W.button("承诺完成",func():g.contract_action(t.id,"accept");app.save_game();governance(app)))
			row.add_child(W.button("不作承诺",func():g.contract_action(t.id,"decline");app.save_game();governance(app)))
		elif t.status=="accepted":
			row.add_child(W.button("兑现并领取支持",func():
				var error=g.contract_action(t.id,"fulfill")
				if error=="":app.save_game();app.refresh();governance(app)
				else:app.modal_subtitle.text=error,not g.Progress.contract_met(g,t.id)))
static func research(app):
	var g=app.game
	app.open_modal("知识用于改造与新技术", "知识 %.1f · 科技解锁 + 机会奖励，不是把所有标签一次塞进菜单。"%g.s.knowledge)
	if not g.s.tag_rewards.is_empty():
		W.section(app.modal_body,"获得的新方向","点击纳入你的政策池。居民奖励是一选一；科技研究是明确解锁。")
		var row=W.hbox(app.modal_body,8)
		for tag in g.s.tag_rewards:
			if tag in g.s.policy_tags:continue
			row.add_child(W.button("纳入「"+g.c.tag_name(tag)+"」",func():g.add_policy_tag(tag);app.save_game();app.refresh();research(app)))
	if not g.s.research.is_empty():
		var r=g.s.research
		W.section(app.modal_body,"正在研究："+g.c.tag_name(r.target),"剩余工作 %.2fh · 材料 %.1f"%[r.work,r.material]);app.modal_body.add_child(W.bar(1-float(r.work)/float(r.total)))
	var row=W.hbox(app.modal_body,10)
	for tag in ["precision","coldchain","circular"]:
		var text={"precision":"偏向更省工的技术制造和产学合作；并不保证下一次会抽到金色好牌。","coldchain":"偏向储备、冷链与农业流通，给持续消耗与集中DT爆发留下更多空间。","circular":"偏向材料转换、副产物回收和重构，让已有城市关系出现新的出路。"}[tag]
		var v=card(row,g.c.tag_name(tag),"科技研究 / 本地时间",text)
		W.section(v,"投入","4知识 · 80通货\n4h工作 · 8材料")
		v.add_child(W.button("开展研究",func():
			var error=g.research(tag)
			if error=="":app.save_game();research(app)
			else:app.modal_subtitle.text=error,tag in g.s.policy_tags or tag in g.s.researched or not g.s.research.is_empty()))
static func journal(app):
	app.open_modal("任期记事", "这不是待办清单，是你做过的选择。最后2000条保存在本地，可导出复现。")
	var events=app.game.s.log
	for i in range(events.size()-1,maxi(-1,events.size()-45),-1):
		var e=events[i]
		var row=W.hbox(app.modal_body,12)
		var time=W.label("%5.2fh"%e.h,12,W.GOLD,false);time.custom_minimum_size.x=65;row.add_child(time)
		row.add_child(W.label(e.text if e.text!="" else e.kind,13,W.TEXT))
	app.modal_body.add_child(W.button("导出存档与复现日志",func():app.export_file("report")))
static func help(app):
	app.open_modal("B 人口与生活 · 上手", "沿原B工程改造。开局18位居民，只有基础供给，不再预涂用途或匹配资源标签。")
	for pair in [
		["01  先看居民缺什么","底部点击居民卡，查看住房、就业、健康、教育与幸福。岗位太少会失业，太多会缺工；暂停迁入可以先把现有社区照顾好。"],
		["02  圈地、选Tag、招标","按2圈1～7个相邻空地，选农业/住房/建材/供能等Tag。农业优先给农场、果园、牧场、菜圃；少量出现相邻产业惊喜，不再混抽所有工业。"],
		["03  让整个街区走起来","下方DT推进整个Block：居民吃饭、迁入、施工、生产、学习一起发生。断供不会被快进掩盖。在线自然1:1；离线只储存有限DT。"],
		["04  把服务放在真正有人的地方","默认服务半径2格、容量有限。点击公园/诊所/商店，查看实际服务人数；点住宅看每一项需求满足率。橙色为噪音污染，绿色为服务关系。"],
		["05  升级与协作是另一种解法","少量土地可以通过扩容、减岗、清洁生产、复合服务来应对需求。邻接兼容且共同有效运行才养成协作；不是必须不断铺新楼。"],
		["06  城市分阶段变复杂","安居阶段开放服务与教育，给3本地小时缓冲。分工城镇需要人口与教育条件，由你确认，才启用低级制成品。托卡马克之冠不在首个Block。"],
		["快捷键","1观察 · 2/3圈地 · 空格推进1h · Esc回地图 · F8实验台。滚轮缩放、中键拖动。实验台会标记存档；不把加资源后的通关算成平衡证据。"]]:W.section(app.modal_body,pair[0],pair[1])
static func settings(app):
	app.open_modal("存档与实验设置", "自动存档：每次重要选择 + 每5秒。独立目录，不读取旧MET、P0或其他私人数据。")
	row_buttons(app.modal_body,[
		["立即保存",func():app.save_game();app.modal_subtitle.text="已请求保存。存档位置："+ProjectSettings.globalize_path("user://")],
		["导出城市备份",func():app.export_file("save")],
		["恢复备份",func():app.import_file()],
		["导出实测报告",func():app.export_file("report")]])
	row_buttons(app.modal_body,[["打开存档目录",func():OS.shell_open(ProjectSettings.globalize_path("user://"))],["实验工具 F8",func():debug(app)],["新开一局",func():confirm_restart(app)]])
	W.section(app.modal_body,"单时间线不是绝对防作弊","游戏固定已经生成的手牌与随机状态。备份、源代码和离线时钟都由你掌握；这里优先保障存档安全，不声称单机绝对防修改。")
	W.section(app.modal_body,"当前明确未做","第二Block、District超频、跨区物流与自治外交未登场。美术为程序化2D试制。Mac实际窗口、字体、保存路径和性能由本次真机试玩验收。")
static func row_buttons(parent:Node,items:Array):
	var row=W.hbox(parent,8)
	for item in items:row.add_child(W.button(item[0],item[1]))
static func debug(app):
	app.open_modal("实验台 · 这不是正式资源", "所有修改都会把本存档标记为实验。相同机制一起运行，只改变你要测试的参数。")
	var g=app.game
	row_buttons(app.modal_body,[["+300 通货",func():_debug_edit(app,"cash")],["+12 DT",func():_debug_edit(app,"dt")],["+30 基础库存",func():_debug_edit(app,"stock")],["触发治理危机",func():_debug_edit(app,"crisis")]])
	W.section(app.modal_body,"本地实验速度（Global仍按现实时间）")
	var row=W.hbox(app.modal_body,8)
	for speed in [1.0,60.0,240.0]:
		row.add_child(W.button("×%.0f"%speed,func():g.s.settings.lab_speed=speed;g.s.debug_used=true;app.save_game();app.refresh();app.modal_subtitle.text="本地实验倍率已设为×%.0f；正常规则是1:1。"%speed))
	W.section(app.modal_body,"Temporal Load 成本强度")
	row=W.hbox(app.modal_body,8)
	for scale in [0.0,.5,1.0,2.0]:row.add_child(W.button("负载×%.1f"%scale,func():g.s.settings.load_scale=scale;g.s.debug_used=true;app.save_game();app.refresh()))
	W.section(app.modal_body,"关系成长速度")
	row=W.hbox(app.modal_body,8)
	for scale in [1.0,3.0,10.0]:row.add_child(W.button("关系×%.0f"%scale,func():g.s.settings.relationship_scale=scale;g.s.debug_used=true;app.save_game()))
	W.section(app.modal_body,"外部期限复核")
	row_buttons(app.modal_body,[["模拟外部过去49h",func():g.s.debug_used=true;g.update_global(g.s.global_now+49*3600);app.save_game();app.refresh();governance(app)],["同种子重开",func():app.restart(int(g.s.seed))]])
static func _debug_edit(app,kind:String):
	var g=app.game;g.s.debug_used=true
	match kind:
		"cash":g.s.currency+=300
		"dt":g.s.dt=minf(g.c.balance.dt_cap,g.s.dt+12)
		"stock":
			Inv.add(g.s.stocks,"food",["fresh","basic"],30,g.stock_cap("food"));Inv.add(g.s.stocks,"material",["heavy","light","basic"],30,g.stock_cap("material"));Inv.add(g.s.stocks,"goods",["basic"],15,g.stock_cap("goods"))
		"crisis":g.s.mandate=10;g.s.support=20;g.Progress.check_crisis(g)
	g.log_event("debug",{"action":kind},"使用实验工具："+kind);app.save_game();app.refresh();app.modal_subtitle.text="已应用「"+kind+"」，存档标记为实验。"
static func award(app):
	var g=app.game
	app.open_modal("河谷初章 · 建成纪念", "MVP主流程已经完成。你不必交出地图；可以继续把这片地方变成你想看的样子。")
	app.modal_body.add_child(W.label("这不是一套被集齐的牌。\n这是你让它们共同生活的地方。",29,W.GOLD))
	W.section(app.modal_body,"这次任期","起源：%s\n本地建设历时：%.1fh\n项目：%d  ·  里程碑升级：%d  ·  关系协作：%d\n注入DT：%.1f  ·  重招：%d"%[g.c.origins[g.s.origin].name,g.s.completion_hours,g.s.projects.size(),g.s.totals.upgrades,g.s.totals.fusions,g.s.totals.dt_used,g.s.totals.rerolls])
	if g.s.debug_used:app.modal_body.add_child(W.label("本局使用过实验工具，不能当作正常数值平衡证据。",14,W.RED))
	row_buttons(app.modal_body,[["继续建设 · 无限模式",func():app.close_modal()],["导出这座城市与日志",func():app.export_file("report")]])
static func failure(app):
	app.open_modal("任期结束，城市仍然留下", app.game.s.failed_reason)
	app.modal_body.add_child(W.label("首版把它保存为只读的城市状态。\n自治外交与重新主政，留给后面的版本。",23,W.TEXT))
	row_buttons(app.modal_body,[["回到地图看一看",func():app.close_modal()],["导出这次翻车",func():app.export_file("report")],["重新开始",func():confirm_restart(app)]])
static func confirm_demolish(app,id:String):
	app.open_modal("拆除，而不是撤销历史", "支付35通货，回收已实际投入材料的25%。起源赠送项目不会刷出建设退款。")
	app.modal_body.add_child(W.label("即将拆除「"+app.game.c.projects[app.game.s.projects[id].def].name+"」。\n住宅里的居民会先成为待安置人口，不会凭空消失。已选升级与关系失效，地块恢复可用；这不是免费的重抽。",20,W.TEXT))
	row_buttons(app.modal_body,[["确认拆除",func():
		var error=app.game.demolish(id)
		if error=="":app.selected_project="";app.save_game();app.overlay.hide();app.refresh()
		else:app.modal_subtitle.text=error],["保留它",func():app.close_modal()]])
static func confirm_risk(app):
	app.open_modal("要赌这次紧急改革吗？", "成功65%：恢复治理并补充5DT。失败35%：本次任期立即结束。不是普通施工的隐性骰子。")
	row_buttons(app.modal_body,[["承担风险",func():app.game.crisis_action("risk");app.save_game();app.refresh();failure(app) if app.game.s.phase=="failed" else governance(app)],["返回，先考虑修复",func():governance(app)]])
static func confirm_leave(app):
	app.open_modal("主动移交", "这会结束本次试玩任期，地图保留为只读状态。不是退出应用程序。")
	row_buttons(app.modal_body,[["确认移交",func():app.game.crisis_action("leave");app.save_game();app.refresh();failure(app)],["继续留任",func():governance(app)]])
static func confirm_restart(app):
	app.open_modal("新的一次起点", "会建立新的单时间线。本B人口版旧主存档会成为最近备份；原B版存档目录不受影响；重要城市请先导出。")
	var seed=LineEdit.new();seed.text=str(int(app.game.s.seed)+1);seed.placeholder_text="随机种子（整数）";app.modal_body.add_child(seed)
	row_buttons(app.modal_body,[["用这个种子开局",func():app.restart(int(seed.text))],["先导出旧城市",func():app.export_file("save")]])
static func resource_name(id:String) -> String:
	return {"food":"食物","material":"材料","goods":"低级制成品","knowledge":"知识","energy":"能源"}.get(id,id)
