extends RefCounted
const W=preload("res://ui/widgets.gd")
const Inv=preload("res://core/inventory.gd")
const Dialogs=preload("res://ui/dialogs.gd")
static func inspector(app):
	var holder=app.inspector;var scroll=app.inspector_scroll.scroll_vertical
	W.clear(holder)
	var g=app.game
	if app.selected_project!="" and g.s.projects.has(app.selected_project):project(app,holder,app.selected_project)
	else:overview(app,holder)
	app.inspector_scroll.set_deferred("scroll_vertical",scroll)
static func overview(app,v):
	var g=app.game
	W.section(v,"当前任期", "B人口版 / 让居民需求推动建设")
	if g.s.phase in ["select_block","origin"]:
		v.add_child(W.label("先选择一片地方",22,W.TEXT))
		v.add_child(W.label("你选择在哪里开始，世界给出几种可能。起源决定家底，不决定这片城市最后的身份。",14,W.MUTED))
		v.add_child(W.button("继续开局",func():
			if g.s.phase=="select_block":Dialogs.start(app)
			else:Dialogs.origin(app)))
		return
	if g.s.phase=="failed":
		v.add_child(W.label("任期已结束",23,W.RED));v.add_child(W.label(g.s.failed_reason,14,W.TEXT));v.add_child(W.button("查看结算",func():Dialogs.failure(app)));return
	v.add_child(W.label("照顾好住在这里的人",20,W.TEXT))
	var m=g.s.metrics
	v.add_child(W.label("居民 %.1f人 · 住房 %.0f\n健康 %.0f · 幸福 %.0f"%[m.get("population",0),m.get("housing",0),m.get("health",78),m.get("happiness",72)],15,W.GREEN))
	for hint in g.Population.urgent(g).slice(0,3):v.add_child(W.label(hint,13,W.TEXT))
	v.add_child(W.button("逐户生活 / 就业 / 阶段",func():Dialogs.population(app)))
	W.section(v,"首块纪念（非建设指令）")
	for item in g.checklist():
		var row=W.hbox(v,7)
		row.add_child(W.label("●" if item.done else "○",13,W.GREEN if item.done else W.MUTED,false))
		var title=W.label(item.name,13,W.TEXT);title.custom_minimum_size.x=168;row.add_child(title)
		var value=W.label(item.value,12,W.GREEN if item.done else W.MUTED,false);value.size_flags_horizontal=0;row.add_child(value)
	if g.s.complete:
		v.add_child(W.label("主流程已完成。没有下一道锁；这片地图依然属于你的构筑。",13,W.GOLD))
		v.add_child(W.button("看看奖状",func():Dialogs.award(app)))
	W.section(v,"下一步",next_hint(g))
	v.add_child(W.button("希望城与居民来信",func():Dialogs.governance(app)))
	v.add_child(W.button("供给与财政",func():Dialogs.resources(app)))
	if g.s.crisis.get("active",false):v.add_child(W.button("处理治理危机",func():Dialogs.governance(app)))
	var ready=[]
	for rel in g.s.relations.values():
		if rel.choice=="" and rel.progress>=rel.required and g.relation_active(rel):ready.append(rel)
	if not ready.is_empty():
		W.section(v,"已经相处成熟")
		for rel in ready.slice(0,3):
			v.add_child(W.button(g.c.fusions[rel.template].name+" · 选择协作",func():
				var error=g.create_fusion(rel.id)
				if error=="":app.save_game();Dialogs.tender(app)
				else:app.message(error)))
	if not g.s.tag_rewards.is_empty():v.add_child(W.button("有新的政策可纳入",func():Dialogs.research(app)))
	W.section(v,"最近发生")
	var log=g.s.log
	var shown=0
	for i in range(log.size()-1,-1,-1):
		if log[i].text=="" or log[i].kind in ["dt_pulse","new_game"]:continue
		v.add_child(W.label(log[i].text,12,W.MUTED));shown+=1
		if shown>=3:break
static func next_hint(g) -> String:
	if not g.s.events.is_empty():return "承包方提出一个新机会。可以接受改变，也可以按原计划继续。"
	if not g.s.draft.is_empty():return "当前投标已经锁定。可以保留、付费重招，或选一个方案真正落地。"
	for p in g.s.projects.values():
		if not p.job.is_empty():return "已有工程在施工。库存、能源与其他项目竞争；注入DT能推动整个Block，不会凭空补材料。"
	for p in g.s.projects.values():
		if g.milestone_status(p.id).ready:return "有项目达到成长里程碑。点地图上的金色标记，看看它这一轮会提出什么升级。"
	if g.s.totals.built<4:return "圈1～7格场地，选农业/住房/建材等发展Tag。没有涂区前置；土地与意图共同影响方案。"
	return "把可协作的项目放在一起，并让两边有效运行。关系成熟后，不是加一个百分比，而是获得新的机制。"
static func project(app,v,id:String):
	var g=app.game;var p=g.s.projects[id];var d=g.c.projects[p.def];var st=g.project_stats(id)
	v.add_child(W.label(d.name,23,W.TEXT))
	v.add_child(W.label(app.ZONES[d.zone]+"  ·  "+("初建" if p.level==0 else "第%d里程碑"%p.level)+"  ·  %d格"%g.s.sites[p.site].cells.size(),12,W.GOLD))
	v.add_child(W.label(g.c.tags_text(st.tags),12,W.GREEN))
	if not p.job.is_empty():
		var job=p.job;var ratio=clampf(1-float(job.work)/maxf(float(job.total),.001),0,1)
		var requirements="剩余工作 %.2fh · 供给不足会延长\n材料 %.1f"%[job.work,job.material]
		if g.Population.stage(g)>=2 and float(job.goods)>0:requirements+=" · 低级制成品 %.1f"%job.goods
		W.section(v,"施工 / 改造",requirements)
		v.add_child(W.bar(ratio));v.add_child(W.label(p.bottleneck,13,W.GOLD))
		var cost=45+float(job.work)*35
		v.add_child(W.button("压缩工期40%% · %.0f通货"%cost,func():app.act(g.rush(id)),job.get("rushed",false) or g.s.currency<cost,"不减少材料总需求；每项工程只能压缩一次。"))
	else:
		W.section(v,"实际运行", "利用率 %.0f%%  ·  %s"%[float(p.usage)*100,p.bottleneck])
		v.add_child(W.bar(float(p.usage)))
		if not st.outputs.is_empty():
			W.section(v,"满负荷产出 / 本地小时")
			for out in st.outputs:
				v.add_child(W.label("+%.2f %s  %s"%[out.rate,resource_name(out.resource),""],12,W.TEXT))
		if not st.inputs.is_empty():
			W.section(v,"满负荷输入 / 本地小时")
			for input in st.inputs:
				v.add_child(W.label("−%.2f %s  %s"%[input.rate,resource_name(input.resource),""],12,W.MUTED))
		var services=[]
		for k in st.services:services.append("%s %.0f"%[{"education":"教育","care":"医疗","market":"消费","leisure":"休闲"}.get(k,k),st.services[k]])
		if not services.is_empty():W.section(v,"服务容量"," / ".join(services))
		v.add_child(W.label("能源需求 %.1f · 供能 %.1f\n商业税收上限 %.1f/h · 固定维护 %.1f/h"%[st.energy,st.supply_energy,st.income,st.upkeep],12,W.MUTED))
		var milestone=g.milestone_status(id)
		W.section(v,"下一成长里程碑",milestone.text);v.add_child(W.bar(milestone.progress))
		v.add_child(W.button("发起升级招标",func():Dialogs.upgrade_preference(app,id),not milestone.ready))
	W.section(v,"人，而不是卡牌")
	if st.population>0:
		v.add_child(W.label("实际住户 %.1f / %.0f 人"%[p.get("residents",0),st.population],17,W.GREEN))
		v.add_child(W.label("健康 %.0f → %.0f\n幸福 %.0f → %.0f\n教育 %.0f"%[p.health,p.health_target,p.happiness,p.happiness_target,p.education],14,W.TEXT))
		v.add_child(W.label("箭头是按当前生活条件趋向的目标，不是即时加成。",11,W.MUTED))
		if not p.living.is_empty():
			for kind in ["market","leisure","care","education"]:
				if int(g.s.stage)<1:continue
				v.add_child(W.label("%s满足 %.0f%%"%[g.Population.LABELS[kind],100*float(p.living.get(kind,0))],13,W.TEXT))
			v.add_child(W.label("噪音 −%.1f幸福 / 污染 −%.1f健康"%[p.living.noise,p.living.pollution],12,W.RED))
		for source in p.impact_sources:
			v.add_child(W.label("来自 "+g.c.projects[g.s.projects[source.source].def].name+"：噪音%.1f / 污染%.1f"%[source.noise,source.pollution],11,W.RED))
	else:
		v.add_child(W.label("岗位 %.1f / %.1f\n技能适配 %.0f%%（教育要求 %.0f）"%[p.workers,st.jobs,100*float(p.skill_ratio),st.skill],13,W.TEXT))
		if st.noise+st.pollution>0:v.add_child(W.label("满载噪音 %.0f / 污染 %.0f\n邻格100%%，隔一格40%%；停运不排放。"%[st.noise,st.pollution],12,W.RED))
		var seen={}
		for link in g.s.metrics.get("service_links",[]):
			if link.source!=id:continue
			var k=link.home+"|"+link.kind;seen[k]=float(seen.get(k,0))+float(link.people)
		for k in seen:
			var parts=k.split("|")
			v.add_child(W.label("→ %s %s %.1f人"%[g.c.projects[g.s.projects[parts[0]].def].name,g.Population.LABELS[parts[1]],seen[k]],12,W.GREEN))
	W.section(v,"邻接关系")
	var found=false
	for rel in g.s.relations.values():
		if not id in [rel.a,rel.b]:continue
		found=true
		var other=rel.b if rel.a==id else rel.a
		var name=g.c.projects[g.s.projects[other].def].name
		v.add_child(W.label(name+" · "+g.c.fusions[rel.template].name,13,W.TEXT))
		if rel.choice!="":
			v.add_child(W.label("已建立："+g.Draft.definition(g,"fusion",rel.choice).name,12,W.GREEN))
		else:
			v.add_child(W.bar(rel.progress/rel.required))
			v.add_child(W.label("有效共存 %.1f / %.1fh"%[rel.progress,rel.required],12,W.MUTED))
			if rel.progress>=rel.required:
				v.add_child(W.button("看看协作可能",func():
					var error=g.create_fusion(rel.id)
					if error=="":app.save_game();Dialogs.tender(app)
					else:app.message(error)))
	if not found:v.add_child(W.label("让兼容项目相邻并共同运行。这里只记录有效共存，不追踪每一份原料去向。",12,W.MUTED))
	var row=W.hbox(v,6)
	row.add_child(W.button("恢复运行" if p.paused else "暂时停运",func():app.act(g.set_paused(id,not p.paused)),g.s.phase!="play"))
	row.add_child(W.button("拆除改建",func():Dialogs.confirm_demolish(app,id),g.s.phase!="play"))
static func resource_name(id:String) -> String:
	return {"food":"食物","material":"材料","goods":"低级制成品","knowledge":"知识","energy":"能源"}.get(id,id)
