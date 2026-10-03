extends RefCounted
const Inv=preload("res://core/inventory.gd")
const Population=preload("res://core/population.gd")
const Progress=preload("res://core/progress.gd")

static func advance(g,hours:float,charge_dt:bool,stop_for_events:bool) -> Dictionary:
	var wanted=maxf(0.0,hours)
	var done=0.0;var spent=0.0;var reason=""
	var tick=float(g.c.balance.tick_hours)
	if not charge_dt:
		g.s.remainder+=wanted
		wanted=floor((float(g.s.remainder)+.0000001)/tick)*tick
		g.s.remainder-=wanted
	# Cap each call for responsiveness. Interactive pulses are <=6h.
	wanted=minf(wanted,96.0)
	while done<wanted-.000001:
		if g.s.phase!="play":reason="本次治理已结束。";break
		if stop_for_events and not g.s.events.is_empty():reason="施工出现新的提议；剩余DT没有扣除。";break
		var step=minf(tick,wanted-done)
		var cost=step*g.temporal_load() if charge_dt else 0.0
		if charge_dt and g.s.dt+0.000001<cost:reason="DT不足；已有城市仍可自然运行。";break
		if charge_dt:
			g.s.dt=maxf(0,g.s.dt-cost);g.s.totals.dt_used+=cost;spent+=cost
		simulate_step(g,step)
		done+=step
	return {"advanced":done,"spent":spent,"reason":reason}

static func diagnostics(g) -> void:
	if g.s.projects.is_empty():return
	var stats=g.all_stats()
	var pop=0.0;var energy=float(g.c.balance.base_energy);var energy_need=0.0
	var services={"care":0.0,"education":0.0,"market":0.0}
	var caps=g.c.balance.stock_caps.duplicate(true)
	var people=Population.snapshot(g,stats)
	for id in stats:
		var p=g.s.projects[id];var st=stats[id]
		if p.status=="running" and not p.paused:
			energy+=st.supply_energy*Population.operating_factor(g,id,st,people);energy_need+=st.energy
			for service in st.services:services[service]=float(services.get(service,0))+float(st.services[service])
			for r in st.storage:caps[r]=float(caps.get(r,0))+float(st.storage[r])
		elif not p.job.is_empty() and not p.paused:energy_need+=float(p.job.energy)
	if not g.s.research.is_empty():energy_need+=1.0
	pop=people.population
	var old=g.s.metrics
	g.s.metrics={"population":pop,"energy":energy,"energy_need":energy_need,"energy_ratio":minf(1.0,energy/maxf(energy_need,.001)),"services":services,"caps":caps,
		"flows":old.get("flows",{}),"cashflow":old.get("cashflow",0.0),"food_ratio":g.s.shortage_ratio,"income":old.get("income",0.0),"upkeep":old.get("upkeep",0.0)}
	for key in people:g.s.metrics[key]=people[key]
	for key in ["service_served","service_links","exposure_links","migration","need_grace"]:
		if old.has(key):g.s.metrics[key]=old[key]

static func simulate_step(g,h:float) -> void:
	var stats=g.all_stats()
	diagnostics(g)
	var m=g.s.metrics
	var energy_factor=float(m.energy_ratio)
	var old={}
	for r in ["food","material","goods"]:old[r]=Inv.total(g.s.stocks,r)
	var old_money=float(g.s.currency)
	# Implicit household/garden/repair networks are part of the starting safety floor.
	var extrafood=.3 if g.s.reveal=="spring" else 0.0
	Inv.add(g.s.stocks,"food",["fresh","basic"],(float(g.c.balance.base_food)+extrafood)*h,float(m.caps.food))
	Inv.add(g.s.stocks,"material",["light","basic"],float(g.c.balance.base_material)*h,float(m.caps.material))
	var food_need=float(m.population)*float(g.c.balance.population.food_per_person)*h
	var eaten=Inv.take(g.s.stocks,"food",[],food_need).taken
	var food_ratio=minf(1.0,eaten/maxf(food_need,.0001))
	g.s.shortage_ratio=food_ratio
	var plans=[]
	var income=0.0;var upkeep=0.0
	for id in stats:
		var p=g.s.projects[id];var st=stats[id]
		p.outputs_last={}
		if p.paused:
			p.usage=0.0;p.bottleneck="已停运；时间负载仍计入已建结构";continue
		if not p.job.is_empty():
			var job=p.job
			var stepwork=minf(h,float(job.work))
			var recipe=[]
			for resource in ["material","goods"]:
				var rate=float(job[resource])/maxf(float(job.work),.000001)
				if rate>0:recipe.append({"resource":resource,"rate":rate,"tags":[]})
			plans.append({"id":id,"kind":"job","recipe":recipe,"time":stepwork,"factor":energy_factor,"allocated":0.0,"limiter":"能源" if energy_factor<.999 else ""})
			p.usage=0.0
			continue
		if p.status!="running":continue
		var factor=energy_factor if st.energy>0 else 1.0
		var limiter="能源不足" if factor<.999 else ""
		var labor_factor=Population.operating_factor(g,id,st,m)
		if labor_factor<factor:
			factor=labor_factor
			limiter="劳力不足" if p.staff_ratio<.85 else ("教育/技能不足" if p.skill_ratio<.9 else "健康与幸福影响生产率")
		if st.population>0:
			factor=minf(factor,float(p.get("residents",0))/maxf(1,float(st.population)))
			if food_ratio<factor:factor=food_ratio;limiter="居民食物不足"
			elif factor<.99:limiter="住房有空位"
		# Capacity is not a universally desirable 100% score: demand-limited is healthy.
		if not st.services.is_empty() and st.population==0:
			var use=Population.demand_factor(g,id,st,stats)
			if use<factor:factor=use;limiter="覆盖范围内顾客/受服务居民较少"
		if st.supply_energy>0:
			var use=minf(1.0,float(m.energy_need)/maxf(float(m.energy),.001))
			if use<factor:factor=use;limiter="能源有余量"
		for out in st.outputs:
			if out.resource=="knowledge":continue
			var room=maxf(0,float(m.caps.get(out.resource,0))-Inv.total(g.s.stocks,out.resource))
			var ratio=room/maxf(.000001,float(out.rate)*h)
			if ratio<factor:factor=maxf(0,ratio);limiter="仓储已满"
		plans.append({"id":id,"kind":"run","recipe":st.inputs,"time":h,"factor":factor,"allocated":0.0,"limiter":limiter})
		upkeep+=float(st.upkeep)*h
	if not g.s.research.is_empty():
		var job=g.s.research
		var recipe=[]
		for r in ["material","goods"]:
			if float(job[r])>0:recipe.append({"resource":r,"rate":float(job[r])/maxf(.000001,float(job.work)),"tags":[]})
		plans.append({"id":"research","kind":"research","recipe":recipe,"time":minf(h,float(job.work)),"factor":energy_factor,"allocated":0.0,"limiter":""})
	# Fair, deterministic small rounds; constrained interfaces get first access within
	# a round, but no resource is counted twice. No building-to-building flow graph.
	plans.sort_custom(func(a,b):
		var aa=0;var bb=0
		for input in a.recipe:aa+=input.tags.size()
		for input in b.recipe:bb+=input.tags.size()
		return aa>bb if aa!=bb else a.id<b.id)
	for round_index in 12:
		for plan in plans:
			var unit=float(plan.factor)/12.0
			if unit<=0:continue
			var ratio=Inv.consume_recipe(g.s.stocks,plan.recipe,float(plan.time)*unit)
			plan.allocated+=unit*ratio
			if ratio<.999:
				for input in plan.recipe:
					if Inv.total(g.s.stocks,input.resource,input.tags)<float(input.rate)*float(plan.time)*unit:
						plan.limiter=g.c.tags_text(input.tags)+{"food":"食物","material":"材料","goods":"制成品"}.get(input.resource,input.resource)+"不足";break
	for plan in plans:
		if plan.kind=="research":
			advance_job(g,g.s.research,plan,float(plan.time)*float(plan.allocated),null)
			continue
		var p=g.s.projects[plan.id]
		var st=stats[plan.id]
		var actual=clampf(float(plan.allocated),0,1)
		if plan.kind=="job":
			p.bottleneck=plan.limiter if actual<.999 else "施工正常"
			advance_job(g,p.job,plan,float(plan.time)*actual,p)
		else:
			p.usage=actual;p.bottleneck=plan.limiter if actual<.999 else "正常运行"
			if actual<.95 and plan.limiter not in ["当前服务需求较少","能源有余量","仓储已满",""]:p.ever_bottleneck=true
			for out in st.outputs:
				var qty=float(out.rate)*h*actual
				if out.resource=="knowledge":
					if Population.stage(g)>=1:g.s.knowledge+=qty
					else:qty=0
				else:qty=Inv.add(g.s.stocks,out.resource,out.tags,qty,float(m.caps.get(out.resource,0)))
				p.produced[out.resource]=float(p.produced.get(out.resource,0))+qty
				p.outputs_last[out.resource]=float(p.outputs_last.get(out.resource,0))+qty/h

			p.effective_hours+=h*actual
			if actual>=.7:p.high_usage_hours+=h
	# Taxes are paid by actual people and actual customers, never empty housing capacity.
	var service_actual=Population.allocate_services(g,stats)
	for id in stats:
		var st=stats[id];var p=g.s.projects[id]
		if st.income>0 and p.status=="running" and not p.paused:
			var seats=maxf(1,float(st.services.get("market",st.services.get("leisure",0))))
			var served=float(service_actual.delivered.get(id+"|market",service_actual.delivered.get(id+"|leisure",0)))
			income+=float(st.income)*minf(1,served/seats)*h
	var household_tax=float(m.population)*float(g.c.balance.population.tax_per_person)*(.5+.5*float(m.employment))*(.6+.4*float(m.happiness)/100)
	income+=household_tax*h
	g.s.currency+=income-upkeep
	g.s.local_hours+=h;g.s.simulation_tick+=1
	var flows={}
	for r in old:flows[r]=(Inv.total(g.s.stocks,r)-float(old[r]))/h
	g.s.metrics.flows=flows;g.s.metrics.income=income/h;g.s.metrics.upkeep=upkeep/h;g.s.metrics.cashflow=(g.s.currency-old_money)/h
	Population.tick(g,h,stats)
	Progress.tick(g,h,stats)
	diagnostics(g)

static func advance_job(g,job:Dictionary,plan:Dictionary,work:float,p) -> void:
	var before=float(job.work)
	var fraction=minf(1.0,work/maxf(before,.0000001))
	for resource in ["material","goods"]:
		var used=float(job[resource])*fraction
		job[resource]=maxf(0,float(job[resource])-used)
		if p!=null:p["spend_"+resource]+=used
	job.work=maxf(0,before-work)
	job.progress=float(job.get("progress",0))+work
	if p!=null and job.kind=="build" and not p.opportunity_checked and float(job.progress)>=float(job.total)*.45:
		p.opportunity_checked=true
		Progress.try_construction_event(g,p)
	if job.work<=.000001:
		if plan.kind=="research":
			var tag=str(job.target)
			g.s.researched.append(tag)
			if not tag in g.s.policy_tags and not tag in g.s.tag_rewards:g.s.tag_rewards.append(tag)
			g.log_event("research_complete",{"tag":tag},"研究完成：可以把「"+g.c.tag_name(tag)+"」纳入招标政策。")
			g.s.research={}
		elif job.kind=="upgrade":
			var uid=str(job.target)
			p.effects.append_array(g.c.upgrades[uid].effects.duplicate(true));p.upgrades.append(uid);p.level+=1;p.status="running";p.job={}
			g.s.totals.upgrades+=1
			g.log_event("upgrade_complete",{"project":p.id,"upgrade":uid},g.c.projects[p.def].name+"完成「"+g.c.upgrades[uid].name+"」。")
		else:
			p.status="running";p.job={};p.usage=0.0
			g.log_event("project_complete",{"project":p.id},g.c.projects[p.def].name+"已经投产。")
