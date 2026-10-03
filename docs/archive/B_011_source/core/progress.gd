extends RefCounted
const Population=preload("res://core/population.gd")
const Rng=preload("res://core/rng.gd")
const Inv=preload("res://core/inventory.gd")

static func milestone(g,id:String) -> Dictionary:
	var p=g.s.projects[id]
	if p.status!="running" or p.paused:return {"ready":false,"progress":0.0,"text":"先完成施工并投入运行。"}
	if p.level>=2:return {"ready":false,"progress":1.0,"text":"两个里程碑均已兑现；仍可发展关系协作。"}
	var need=float(g.c.balance.milestone_hours[int(p.level)])
	var second=false;var reason=""
	if p.context_goal=="neighbor":
		for other in g.s.projects:
			if g.adjacent_projects(id,other) and g.s.projects[other].status=="running":second=true
		reason="与一个已运行项目邻接"
	else:
		second=Inv.total(g.s.stocks,"material")>=12
		reason="材料库存达到12"
	# A visible alternative avoids an impossible generated condition.
	if p.effective_hours>=need*1.6:second=true
	var ready=float(p.effective_hours)>=need and second
	return {"ready":ready,"progress":minf(1.0,float(p.effective_hours)/need),"text":"有效运行 %.1f / %.1f 小时\n%s；或运行满 %.1f 小时。"%[p.effective_hours,need,reason,need*1.6]}

static func initialize_contracts(g) -> void:
	var now=float(g.s.global_now)
	g.s.contracts={
		"hope":{"id":"hope","name":"希望城 · 第一份开发报告","body":"建成并运行5个项目，完成1次里程碑升级，且实际人口达到24。拒绝不算失信；抵达后的48现实小时内需兑现。","status":"offered","deadline":now+48*3600,"hours":48.0,"currency":160.0,"dt":6.0,"bar":"mandate","reward":14.0,"penalty":16.0},
		"school":{"id":"school","name":"居民来信 · 孩子们的学堂","body":"让至少12位居民实际得到教育服务。社区阶段开放后的24现实小时内完成，奖励地方支持与储存时间。","status":"locked","deadline":now+24*3600,"hours":24.0,"currency":55.0,"dt":4.0,"bar":"support","reward":12.0,"penalty":12.0},
		"care":{"id":"care","name":"居民来信 · 安稳的一天","body":"基本供给稳定累计2小时，至少10位居民实际获得医疗服务。完成后可任选一项新政策纳入招标池。","status":"locked","deadline":now+36*3600,"hours":36.0,"currency":65.0,"dt":3.0,"bar":"support","reward":10.0,"penalty":12.0}}

static func contract_met(g,id:String) -> bool:
	var n=0
	for p in g.s.projects.values():
		if p.status=="running" and not p.paused:n+=1
	# A promised service is fulfilled by served residents, not unused nominal capacity.
	var served=g.s.metrics.get("service_served",{})
	var education=float(served.get("education",0))
	var care=float(served.get("care",0))
	match id:
		"hope":return n>=5 and g.s.totals.upgrades>=1 and float(g.s.metrics.get("population",0))>=24
		"school":return education>=12-.00001
		"care":return g.s.stable_hours>=2 and care>=10-.00001
	return false

static func contract(g,id:String,action:String) -> String:
	if g.s.phase!="play" or not g.s.contracts.has(id):return "委托不可用。"
	var t=g.s.contracts[id]
	if action=="accept":
		if t.status!="offered":return "这份委托已经处理。"
		if g.s.global_now>=t.deadline:return "外部窗口已关闭。"
		t.status="accepted"
		g.log_event("promise",{"id":id,"deadline":t.deadline},"作出承诺："+t.name+"。截止时间按现实世界推进，退出也不会延长。")
	elif action=="decline":
		if t.status!="offered":return "已承诺的任务不能无代价取消。"
		t.status="declined";g.log_event("decline",{"id":id},"婉拒委托，不扣失信；真实民生需求仍然存在。")
	elif action=="fulfill":
		if t.status!="accepted" or not contract_met(g,id):return "尚未承诺，或条件尚未满足。"

		t.status="completed"
		g.s.currency+=t.currency;g.s.dt=minf(float(g.c.balance.dt_cap),float(g.s.dt)+float(t.dt))
		g.s[t.bar]=minf(100,float(g.s[t.bar])+float(t.reward))
		if id=="care":
			g.s.tag_rewards=[]
			for tag in ["precision","coldchain","circular"]:
				if not tag in g.s.policy_tags:g.s.tag_rewards.append(tag)
			g.s.tag_reward_pick_one=true
			t["policy_pick_pending"]=true
		g.log_event("promise_kept",{"id":id},"兑现承诺："+t.name+"。奖励已经自动入账。")
	else:return "未知操作。"
	return ""

static func global_update(g) -> void:
	if g.s.phase!="play":return
	for t in g.s.contracts.values():
		if t.status=="locked" and Population.stage(g)>=1:
			t.status="offered";t.deadline=float(g.s.global_now)+float(t.hours)*3600
		if t.status in ["offered","accepted"] and g.s.global_now>=float(t.deadline):
			if t.status=="accepted":
				g.s[t.bar]=maxf(0,float(g.s[t.bar])-float(t.penalty));t.status="failed"
				g.log_event("promise_failed",{"id":t.id},"已承诺的「"+t.name+"」逾期，失信已记入治理评价。")
			else:t.status="expired";g.log_event("opportunity_expired",{"id":t.id},"未承诺的外部机会过去了，没有失信处罚。")
	check_crisis(g)

static func try_construction_event(g,p:Dictionary) -> void:
	if Rng.next(g.s,"events")>.46:return
	var traits=g.site_traits(g.s.sites[p.site].cells)
	var pool=[]
	for event in g.c.opportunities.values():
		if int(event.get("stage",0))<=Population.stage(g) and (event.context=="any" or event.context in traits):pool.append(event.id)
	if pool.is_empty():return
	var id=pool[int(Rng.next(g.s,"events")*pool.size())]
	g.s.events.append({"id":id,"project":p.id})
	g.log_event("construction_opportunity",{"event":id,"project":p.id},g.c.opportunities[id].name+"；可按原计划继续。")

static func opportunity(g,choice:String) -> String:
	if g.s.phase!="play" or g.s.events.is_empty():return "没有待处理的施工提议。"
	var event=g.s.events[0]
	if not g.s.projects.has(event.project):g.s.events.pop_front();return ""
	var p=g.s.projects[event.project];var def=g.c.opportunities[event.id]
	if choice=="accept":
		if g.s.currency<float(def.cost):return "通货不足；可以按原计划施工。"
		g.s.currency-=float(def.cost)
		p.effects.append_array(def.effects.duplicate(true));p.history.append(event.id)
		if not p.job.is_empty():
			p.job.work+=float(def.hours);p.job.total+=float(def.hours);p.job.material+=float(def.material)
		g.log_event("construction_offer_accepted",event,"采纳提议："+def.name)
	else:g.log_event("construction_offer_skipped",event,"按原方案继续，不额外收费或扣支持。")
	g.s.events.pop_front();g.refresh_metrics()
	return ""

static func research(g,tag:String) -> String:
	if g.s.phase!="play" or Population.stage(g)<1:return "安居社区阶段先建设学校；教育改善居民能力，有效教学再产生知识。"
	if not tag in ["precision","coldchain","circular"]:return "未知方向。"
	if tag in g.s.researched or tag in g.s.policy_tags:return "这项方向已获得。"
	if not g.s.research.is_empty():return "当前已有一项研究。"
	if g.s.knowledge<4 or g.s.currency<80:return "需要4知识和80通货；知识由实际运行的教育/研究项目积累。"
	g.s.knowledge-=4;g.s.currency-=80
	g.s.research={"kind":"research","target":tag,"work":4.0,"total":4.0,"material":8.0,"goods":0.0,"energy":1.0,"progress":0.0}
	g.log_event("research_start",{"tag":tag},"开始研究「"+g.c.tag_name(tag)+"」；完成后获得新的招标偏好。")
	return ""

static func tick(g,h:float,stats:Dictionary) -> void:
	# A relationship stores pair IDs and history, not a per-resource provenance graph.
	var ids=g.s.projects.keys();ids.sort()
	for ai in ids.size():
		for bi in range(ai+1,ids.size()):
			var a=ids[ai];var b=ids[bi]
			if not g.adjacent_projects(a,b):continue
			var at=g.base_tags(a);var bt=g.base_tags(b)
			var key=a+"~"+b
			if not g.s.relations.has(key):
				for template in g.c.fusions.values():
					if template.a in at and template.b in bt or template.a in bt and template.b in at:
						g.s.relations[key]={"id":key,"a":a,"b":b,"template":template.id,"progress":0.0,"required":float(template.hours),"choice":"","notified":false}
						break
	for rel in g.s.relations.values():
		if not g.relation_active(rel):continue
		var a=g.s.projects[rel.a];var b=g.s.projects[rel.b]
		if rel.choice=="":
			rel.progress=minf(float(rel.required),float(rel.progress)+h*float(a.usage)*float(b.usage)*float(g.s.settings.relationship_scale))
			if rel.progress>=rel.required and not rel.notified:
				rel.notified=true
				g.log_event("relationship_mature",{"id":rel.id},g.c.projects[a.def].name+" × "+g.c.projects[b.def].name+"：关系成熟，可以选择协作机制。")
	for p in g.s.projects.values():
		var ms=milestone(g,p.id)
		if ms.ready and not p.level in p.opened_milestones:
			p.opened_milestones.append(p.level)
			g.log_event("milestone_ready",{"project":p.id,"level":p.level},g.c.projects[p.def].name+"达到成长里程碑，可以发起升级招标。")
	var m=g.s.metrics
	var care=0.0;var support_bonus=0.0
	for id in stats:
		var p=g.s.projects[id]
		if p.status=="running" and not p.paused:
			care+=float(stats[id].services.get("care",0))*float(p.usage)
			support_bonus+=float(stats[id].support_bonus)*float(p.usage)
	var care_ratio=minf(1.0,care/maxf(1.0,float(m.population)))
	if float(g.s.shortage_ratio)>=.99 and float(m.energy_ratio)>=.8:
		g.s.stable_hours+=h;g.s.shortage_hours=maxf(0,float(g.s.shortage_hours)-h)
	else:
		g.s.stable_hours=0.0;g.s.shortage_hours+=h

	check_crisis(g)
	if g.s.crisis.get("active",false) and g.s.local_hours>=float(g.s.crisis.review):
		if float(g.s.support)>=35 and float(g.s.mandate)>=12 or float(g.s.mandate)>=35 and float(g.s.support)>=12:
			g.s.crisis.active=false;g.s.crisis.resolved_at=g.s.local_hours
			g.log_event("crisis_resolved",{},"治理复核通过，保住了任期。")
		else:fail(g,"危机复核时两项支持均未恢复，治理权被收回。")
	if not g.s.complete and g.s.phase=="play":
		var met=true
		for item in checklist(g):
			if not item.done:met=false
		if met:
			g.s.complete=true;g.s.completion_hours=g.s.local_hours
			g.log_event("first_block_complete",{},"河谷初章完成。奖状属于你，地图仍然开放；继续养你的项目。")

static func check_crisis(g) -> void:
	if g.s.phase!="play" or g.s.crisis.get("active",false):return
	if g.s.local_hours<float(g.s.crisis.get("resolved_at",-100))+3:return
	var low=minf(float(g.s.mandate),float(g.s.support))<12 or (g.s.mandate<28 and g.s.support<28)
	if not low:return
	g.s.crisis={"active":true,"review":float(g.s.local_hours)+4.0,"risk_roll":Rng.next(g.s,"risk"),"resolved_at":-100.0}
	g.log_event("governance_crisis",{},"治理危机：4个本地小时后复核。可以修复、承担明确风险，或主动退出。")

static func crisis_action(g,action:String) -> String:
	if g.s.phase!="play" or not g.s.crisis.get("active",false):return "当前没有治理危机。"
	match action:
		"repair":
			if g.s.currency<140 or Inv.total(g.s.stocks,"food")<8:return "修复需要140通货和8食物。"
			g.s.currency-=140;Inv.take(g.s.stocks,"food",[],8)
			g.s.support=maxf(38,minf(100,float(g.s.support)+18));g.s.mandate=maxf(18,minf(100,float(g.s.mandate)+8))
			g.s.crisis.active=false;g.s.crisis.resolved_at=g.s.local_hours
			g.log_event("crisis_repair",{},"付出物资，暂时稳住了治理。根本供需问题仍要解决。")
		"risk":
			if float(g.s.crisis.risk_roll)<.65:
				g.s.support=maxf(40,minf(100,float(g.s.support)+28));g.s.mandate=maxf(45,minf(100,float(g.s.mandate)+30))
				g.s.dt=minf(float(g.c.balance.dt_cap),float(g.s.dt)+5)
				g.s.crisis.active=false;g.s.crisis.resolved_at=g.s.local_hours
				g.log_event("crisis_gamble_win",{},"紧急改革成功：取得新的治理支持和5DT。")
			else:fail(g,"你接受了65%成功率的紧急改革；失败后被强制撤换。")
		"leave":fail(g,"主动移交，结束本次试验任期。地图保留供回顾。")
		_:return "未知危机路线。"
	return ""

static func fail(g,reason:String) -> void:
	g.s.phase="failed";g.s.failed_reason=reason
	g.log_event("governance_ended",{"reason":reason},reason)

static func checklist(g) -> Array:
	var count=0
	for p in g.s.projects.values():
		if p.status=="running" and not p.paused:count+=1
	return [
		{"name":"安顿30位居民","done":g.s.metrics.get("population",0)>=30,"value":"%.0f / 30"%g.s.metrics.get("population",0)},
		{"name":"生活满意度达到65","done":g.s.metrics.get("happiness",0)>=65,"value":"%.0f / 65"%g.s.metrics.get("happiness",0)},
		{"name":"基础供给稳定1小时","done":g.s.stable_hours>=1,"value":"%.1f / 1h"%minf(1,g.s.stable_hours)},
		{"name":"完成1次里程碑升级","done":g.s.totals.upgrades>=1,"value":"%d / 1"%g.s.totals.upgrades},
		{"name":"建立1项关系协作","done":g.s.totals.fusions>=1,"value":"%d / 1"%g.s.totals.fusions},
		{"name":"治理不处于危机","done":not g.s.crisis.get("active",false) and g.s.phase=="play","value":"正常" if not g.s.crisis.get("active",false) else "危机"}]
