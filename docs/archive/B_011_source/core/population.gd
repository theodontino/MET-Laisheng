extends RefCounted
## B population iteration. Residents belong to housing projects, not capacity.
## No individual pathfinding: jobs are Block-wide; services are finite local coverage.
const Hex=preload("res://core/hex.gd")
const SERVICES=["care","leisure","market","education"]
const LABELS={"care":"医疗","leisure":"休闲","market":"消费","education":"教育"}
const STAGES=["生存聚落","安居社区","分工城镇"]

static func stage(g) -> int:return int(g.s.get("stage",0))
static func distance(g,a:String,b:String) -> int:
	var result=999
	for x in g.s.sites[g.s.projects[a].site].cells:
		for y in g.s.sites[g.s.projects[b].site].cells:
			result=mini(result,Hex.distance(x,y))
	return result

static func ensure(p:Dictionary) -> void:
	for pair in [["residents",0.0],["health",78.0],["education",12.0],["happiness",72.0],["workers",0.0],["staff_ratio",1.0],["skill_ratio",1.0],["health_target",78.0],["happiness_target",72.0],["migration",0.0]]:
		if not p.has(pair[0]):p[pair[0]]=pair[1]
	if not p.has("living"):p.living={}
	if not p.has("impact_sources"):p.impact_sources=[]

static func snapshot(g,stats:Dictionary) -> Dictionary:
	var pop=float(g.s.get("displaced",0));var housing=0.0
	var health=0.0;var education=0.0;var happiness=0.0;var housed=0.0
	var jobs=float(g.c.balance.population.base_jobs)
	for id in stats:
		var p=g.s.projects[id];ensure(p);var st=stats[id]
		if st.population>0 and p.status in ["running","upgrading"]:
			housing+=st.population
			var n=float(p.residents);housed+=n;pop+=n
			health+=n*float(p.health);education+=n*float(p.education);happiness+=n*float(p.happiness)
		if p.status=="running" and not p.paused:jobs+=maxf(0,float(st.jobs))
	var labor=pop*float(g.c.balance.population.labor_fraction)
	var staff_ratio=minf(1,labor/maxf(jobs,.001))
	var employed=minf(labor,jobs)
	var income_health=(health+float(g.s.get("displaced",0))*40)/maxf(1,pop)
	var result={"population":pop,"housed":housed,"housing":housing,"vacancies":maxf(0,housing-housed),"jobs":jobs,"workforce":labor,"employed":employed,
		"employment":minf(1,jobs/maxf(1,labor)),"staff_ratio":staff_ratio,
		"health":income_health if pop>0 else 78.0,"education":education/maxf(1,pop),
		"happiness":(happiness+float(g.s.get("displaced",0))*20)/maxf(1,pop),"homeless":float(g.s.get("displaced",0))}
	for id in stats:
		var p=g.s.projects[id];var st=stats[id]
		p.workers=0.0
		p.staff_ratio=staff_ratio if st.jobs>0 else 1.0
		p.skill_ratio=clampf(.4+.6*float(result.education)/maxf(1,float(st.skill)),.4,1.0) if st.skill>0 else 1.0
		if p.status=="running" and not p.paused:p.workers=maxf(0,float(st.jobs))*staff_ratio
	return result

static func operating_factor(g,id:String,st:Dictionary,m:Dictionary) -> float:
	var p=g.s.projects[id]
	if st.jobs<=0:return 1.0
	# Keep a recoverable floor: bad health must hurt, not kill the hospital that can fix it.
	return float(p.staff_ratio)*float(p.skill_ratio)*(.65+.35*float(m.health)/100)*(.8+.2*float(m.happiness)/100)

static func demand_factor(g,id:String,st:Dictionary,stats:Dictionary) -> float:
	if st.services.is_empty():return 1.0
	var answer=0.0
	for service in st.services:
		var offered=float(st.services[service])
		if offered<=0:continue
		var need=0.0
		for home in stats:
			if stats[home].population<=0:continue
			if distance(g,id,home)>int(st.service_range):continue
			var people=float(g.s.projects[home].get("residents",0))
			# Divide neighborhood demand among providers so duplicates don't print revenue.
			var competing=0.0
			for supplier in stats:
				var p=g.s.projects[supplier]
				if p.status!="running" or p.paused:continue
				if distance(g,supplier,home)<=int(stats[supplier].service_range):competing+=float(stats[supplier].services.get(service,0))
			need+=people*offered/maxf(offered,competing)
		answer=maxf(answer,minf(1,need/offered))
	return answer

static func allocate_services(g,stats:Dictionary) -> Dictionary:
	var cover={};var links=[];var delivered={};var demand={};var ids=stats.keys();ids.sort()
	for id in ids:
		if stats[id].population>0 and g.s.projects[id].residents>0:
			cover[id]={};demand[id]={}
			for kind in SERVICES:cover[id][kind]=0.0;demand[id][kind]=float(g.s.projects[id].residents)
	for supplier in ids:
		var p=g.s.projects[supplier];var st=stats[supplier]
		if p.status!="running" or p.paused:continue
		for kind in st.services:
			if not kind in SERVICES:continue
			var capacity=float(st.services[kind])*float(p.usage)
			var remaining=capacity
			var nearby=[]
			for home in cover:
				var dist=distance(g,supplier,home)
				if dist<=int(st.service_range):nearby.append({"id":home,"d":dist})
			# Twelve proportional rounds share finite capacity, no double service counting.
			for _pass in 12:
				var weights=0.0
				for h in nearby:weights+=float(demand[h.id][kind])/(1.0+float(h.d)*.45)
				if weights<=.00001 or remaining<=.00001:break
				var budget=remaining
				for h in nearby:
					var amount=minf(float(demand[h.id][kind]),budget*float(demand[h.id][kind])/(1.0+float(h.d)*.45)/weights)
					demand[h.id][kind]-=amount;cover[h.id][kind]+=amount;remaining-=amount
					if amount>0:links.append({"source":supplier,"home":h.id,"kind":kind,"people":amount})
			delivered[supplier+"|"+kind]=capacity-remaining
	return {"cover":cover,"links":links,"delivered":delivered}

static func tick(g,h:float,stats:Dictionary) -> void:
	var m=snapshot(g,stats);var allocation=allocate_services(g,stats)
	var grace=clampf((float(g.s.local_hours)-float(g.s.get("stage_started",0)))/float(g.c.balance.population.need_grace_hours),0,1)
	var service_totals={"care":0.0,"leisure":0.0,"market":0.0,"education":0.0}
	var noise_links=[]
	for id in stats:
		var p=g.s.projects[id];var n=float(p.residents)
		if stats[id].population<=0 or n<=0:continue
		var living={"food":float(g.s.shortage_ratio),"employment":float(m.employment),"noise":0.0,"pollution":0.0}
		p.impact_sources=[]
		for kind in SERVICES:
			var served=float(allocation.cover.get(id,{}).get(kind,0))
			living[kind]=minf(1.0,served/maxf(.001,n));service_totals[kind]+=served
		for source in stats:
			var op=g.s.projects[source];var st=stats[source]
			if op.status!="running" or op.paused:continue
			var dist=distance(g,id,source)
			if dist>2:continue
			var falloff=1.0 if dist<=1 else .4
			var noise=float(st.noise)*float(op.usage)*falloff
			var pollution=float(st.pollution)*float(op.usage)*falloff
			living.noise+=noise;living.pollution+=pollution
			if noise+pollution>.01:
				var link={"source":source,"home":id,"kind":"exposure","noise":noise,"pollution":pollution}
				p.impact_sources.append(link);noise_links.append(link)
		living.noise=minf(25,living.noise);living.pollution=minf(30,living.pollution)
		p.living=living
		var health_target=78.0-40*(1-float(living.food))-float(living.pollution)+18*float(living.care)
		if stage(g)>=1:health_target-=5*grace*(1-float(living.care))
		p.health_target=clampf(health_target,10,98)
		p.health=lerpf(float(p.health),float(p.health_target),1-exp(-h*float(g.c.balance.population.health_response)))
		var happiness_target=78.0-48*(1-float(living.food))-22*(1-float(living.employment))-float(living.noise)-maxf(0,65-float(p.health))*.35
		if stage(g)>=1:happiness_target+=grace*(12*float(living.market)+14*float(living.leisure)+4*float(living.care)-20)
		p.happiness_target=clampf(happiness_target,5,98)
		p.happiness=lerpf(float(p.happiness),float(p.happiness_target),1-exp(-h*float(g.c.balance.population.happiness_response)))
		# Education is a stock of residents' skills, not research points. Teaching takes time.
		p.education=clampf(float(p.education)+h*(5.5*float(living.education)*(1-float(p.education)/100)-.06),0,100)
		p.migration=0.0
		if float(p.happiness)<28 or float(p.health)<25:
			var leaving=minf(n,h*n*.035);p.residents-=leaving;p.migration=-leaving/maxf(h,.001)
	# Rehouse displaced residents before inviting any new settlers.
	for id in stats:
		var p=g.s.projects[id]
		if p.status not in ["running","upgrading"] or stats[id].population<=0:continue
		var room=maxf(0,float(stats[id].population)-float(p.residents))
		var rehouse=minf(room,float(g.s.get("displaced",0)))
		g.s.displaced-=rehouse;p.residents+=rehouse
	m=snapshot(g,stats)
	var added=0.0
	if not g.s.get("migration_paused",false) and float(g.s.shortage_ratio)>.9 and float(m.happiness)>50 and float(m.health)>45:
		var job_limit=float(m.jobs)/float(g.c.balance.population.labor_fraction)*1.1
		var target=minf(float(m.housing),job_limit)
		var allowance=minf(maxf(0,target-float(m.population)),h*float(g.c.balance.population.migration_per_hour)*clampf((float(m.happiness)-40)/35,.1,1.2))
		for id in stats:
			var p=g.s.projects[id]
			if p.status not in ["running","upgrading"] or stats[id].population<=0:continue
			var add=minf(allowance,maxf(0,float(stats[id].population)-float(p.residents)))
			if add<=0:continue
			# Newcomers bring basic skills, rather than inheriting the city's average instantly.
			p.education=(float(p.education)*float(p.residents)+12*add)/maxf(.001,float(p.residents)+add)
			p.residents+=add;p.migration+=add/maxf(.001,h);allowance-=add;added+=add
	m=snapshot(g,stats)
	for key in m:g.s.metrics[key]=m[key]
	g.s.metrics.service_served=service_totals
	g.s.metrics.service_links=allocation.links
	g.s.metrics.exposure_links=noise_links
	g.s.metrics.migration=added/maxf(.001,h)
	g.s.metrics.need_grace=grace
	# Support follows real life with inertia. A task can buy breathing room, not cure pollution.
	if g.s.local_hours>2:
		g.s.support=lerpf(float(g.s.support),float(m.happiness),1-exp(-h*float(g.c.balance.population.support_response)))
		var failing=float(g.s.shortage_ratio)<.5 or float(m.happiness)<35 or g.s.currency<0
		g.s.governance_bad_hours=float(g.s.get("governance_bad_hours",0))+h if failing else maxf(0,float(g.s.get("governance_bad_hours",0))-h)
		if g.s.governance_bad_hours>3:g.s.mandate=maxf(0,float(g.s.mandate)-h*.75)
		elif not failing:g.s.mandate=minf(85,float(g.s.mandate)+h*.1)
	# Solutions are available immediately on stage entry; new needs ramp over a grace period.
	if stage(g)==0 and float(m.population)>=float(g.c.balance.population.community_population) and g.s.stable_hours>=.5:
		advance_stage(g,1)

static func can_town(g) -> bool:
	var m=g.s.metrics
	return stage(g)==1 and float(m.get("population",0))>=float(g.c.balance.population.town_population) and float(m.get("education",0))>=float(g.c.balance.population.town_education) and g.s.stable_hours>=1

static func advance_stage(g,value:int) -> void:
	if value<=stage(g):return
	g.s.stage=value;g.s.stage_started=g.s.local_hours
	var tags=["health","commerce","leisure","education"] if value==1 else ["industry","logistics","efficient"]
	for tag in tags:
		if not tag in g.s.policy_tags:g.s.policy_tags.append(tag)
	if value>=2 and not "goods" in g.s.revealed_resources:g.s.revealed_resources.append("goods")
	g.log_event("settlement_stage",{"stage":value},"进入「"+STAGES[value]+"」。新的建设方向先开放，新增需求在3个本地小时内逐渐形成。")

static func urgent(g) -> Array:
	var m=g.s.metrics;var list=[];var n=float(m.get("population",0))
	if n<=0:return ["先建住房，让拓荒者能安顿下来。"]
	if float(g.s.shortage_ratio)<.95 or float(m.get("flows",{}).get("food",0))<-.1:list.append("粮食在减少：补农业、升级产能，或先放缓迁入。")
	if float(m.get("vacancies",0))<2:list.append("住房即将住满；想接纳更多人，需要扩建或升级住房。")
	if float(m.get("staff_ratio",1))<.8:list.append("岗位多于劳力：补住房吸引居民，或改造为更省工的生产方式。")
	if float(m.get("employment",1))<.8:list.append("部分居民缺少工作；建生产或服务设施，别只继续盖住宅。")
	if stage(g)>=1:
		for k in ["market","care","leisure","education"]:
			var amount=float(m.get("service_served",{}).get(k,0))
			if amount/maxf(1,n)<.55:list.append(LABELS[k]+"供给不足（%.0f/%.0f人）；先看受影响住宅，再选成本合适的解法。"%[amount,n])
	if list.is_empty():list.append("生活基本稳定。可以继续养成现有项目，也可以暂停迁入、保持这座小聚落。")
	return list
