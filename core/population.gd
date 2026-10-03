extends RefCounted
## Population groups survive housing demolition and keep employment provenance.
const R=preload("res://core/rules.gd")
static func create(g,host:String,count:float,template:Dictionary={}) -> String:
	var id=g.uid("G")
	var p={"id":id,"count":count,"host":host,"health":78.,"education":15.,"happiness":73.,"efficiency":1.,"demand":1.,"employment":{},"services":{},"living":{},"lineage":[id]}
	for key in ["health","education","happiness","efficiency","demand"]:
		if template.has(key):p[key]=template[key]
	g.s.groups[id]=p;return id
static func home_group(s:Dictionary,host:String) -> String:
	for gid in R.ids(s.groups):
		if s.groups[gid].host==host:return gid
	return ""
static func transfer(g,gid:String,target:String,count:float) -> void:
	if not g.s.groups.has(gid) or count<=0:return
	var source=g.s.groups[gid];count=minf(count,float(source.count));var fraction=count/maxf(.000001,float(source.count));var jobs={}
	for b in source.employment:jobs[b]=float(source.employment[b])*fraction
	var recipient=home_group(g.s,target) if target!="homeless" else ""
	# A full house can create a tiny overflow each tick. Preserve its lineage,
	# but do not create a separate homeless entity for every fractional birth.
	if target=="homeless":
		for candidate in R.ids(g.s.groups):
			var other=g.s.groups[candidate]
			if candidate==gid or other.host!="homeless":continue
			var left=source.lineage.duplicate();var right=other.lineage.duplicate();left.sort();right.sort()
			if left==right:recipient=candidate;break
	if recipient==gid:return
	if count>=float(source.count)-.000001 and recipient=="":source.host=target;return
	if recipient=="":
		recipient=create(g,target,0,source)
		g.s.groups[recipient].lineage=source.lineage.duplicate()
	var dest=g.s.groups[recipient];var old=float(dest.count)
	for key in ["health","education","happiness","efficiency","demand"]:dest[key]=(float(dest[key])*old+float(source[key])*count)/maxf(.000001,old+count)
	dest.count+=count
	for b in jobs:dest.employment[b]=float(dest.employment.get(b,0))+jobs[b];source.employment[b]-=jobs[b]
	for origin in source.lineage:
		if not origin in dest.lineage:dest.lineage.append(origin)
	source.count-=count
	if source.count<=.000001:g.s.groups.erase(gid)
static func rehouse(g) -> void:
	var stats=g.compiled.stats
	for bid in R.ids(g.s.projects):
		var p=g.s.projects[bid]
		if p.special:continue
		var capacity=maxf(0,float(stats[bid].housing)*float(stats[bid].efficiency)) if p.status=="running" else 0.
		var gid=home_group(g.s,bid)
		if gid!="" and g.s.groups[gid].count>capacity:transfer(g,gid,"homeless",float(g.s.groups[gid].count)-capacity)
	for gid in R.ids(g.s.groups):
		if not g.s.groups.has(gid) or g.s.groups[gid].host!="homeless":continue
		for bid in R.ids(g.s.projects):
			var p=g.s.projects[bid]
			if p.special or p.status!="running":continue
			var resident=home_group(g.s,bid)
			var used=0. if resident=="" else float(g.s.groups[resident].count)
			var room=maxf(0,float(stats[bid].housing)*float(stats[bid].efficiency)-used)
			if room<=0:continue
			transfer(g,gid,bid,minf(room,float(g.s.groups[gid].count)))
			if not g.s.groups.has(gid) or g.s.groups[gid].host!="homeless":break
static func summary(g) -> Dictionary:
	var people=0.;var housed=0.;var health=0.;var happiness=0.;var education=0.;var employed=0.;var housing=0.;var jobs=0.
	for id in g.playable_ids():
		var p=g.s.projects[id];var st=g.compiled.stats[id]
		if p.status=="running":housing+=maxf(0,float(st.housing)*float(st.efficiency))
		if p.status=="running" and not p.paused:jobs+=maxf(0,float(st.jobs))
	for gid in R.ids(g.s.groups):
		var group=g.s.groups[gid];var n=float(group.count);people+=n
		if group.host!="homeless":housed+=n
		health+=n*float(group.health);happiness+=n*float(group.happiness);education+=n*float(group.education)
		for j in group.employment.values():employed+=j
	return {"population":people,"housed":housed,"homeless":people-housed,"housing":housing,"vacancies":maxf(0,housing-housed),"workforce":people,"jobs":jobs,"employed":employed,"employment":minf(1,employed/maxf(1,people)),"health":health/maxf(1,people),"happiness":happiness/maxf(1,people),"education":education/maxf(1,people)}
static func update(g,next:Dictionary,c:Dictionary,inputs:Dictionary,service:Dictionary,env:Dictionary,labor:Dictionary,h:float) -> void:
	# h is economic months: response constants and service rates are per month.
	var stage=int(g.s.stage)
	for gid in R.ids(g.s.groups):
		var old=g.s.groups[gid];var p=next.groups[gid];var host=g.s.projects[old.host];var st=c.stats[old.host];var n=float(old.count)
		p.employment=labor.employment[gid].duplicate(true);p.services=service.allocations.get(gid,{}).duplicate(true)
		var supplied=inputs.get(gid,{});var live={};var providers={}
		for resource in ["food","material","goods","energy"]:live[resource]=float(supplied.get(resource,1))
		for kind in g.c.balance.service_kinds:live[kind]=minf(1,float(service.covered.get(gid,{}).get(kind,0))/maxf(.001,n))
		var pollution=R.aggregate(env.tiles.get(host.tile,{}).get("pollution",[]),str(c.config[old.host].get("pollution_aggregate","sum")))
		var exposure=maxf(0,pollution)*float(st.exposure_multiplier)
		var unemployment=float(labor.unemployed.get(gid,0))/maxf(1,n)
		var med_happiness=0.
		for link in service.links:
			if link.group==gid and link.kind=="medical" and c.config[link.source].get("medical_happiness",true):med_happiness+=float(link.people)/maxf(1,n)
		var foodlack=1-live.food;var materiallack=1-live.material;var energylack=1-live.energy;var goodslack=1-live.goods
		var health_target=80.-42*foodlack-6*materiallack-6*energylack-9*exposure+15*live.medical
		var happiness_target=76.-45*foodlack-8*materiallack-8*energylack-12*goodslack-12*unemployment-maxf(0,65-p.health)*.25-4*exposure
		var service_ramp=clampf((float(g.s.local_hours)-float(g.c.balance.service_start_hours))/float(g.c.balance.service_grace_hours),0,1)
		if stage>=1:happiness_target+=service_ramp*float(st.shortage_multiplier)*(10*live.commercial+12*live.entertainment+4*med_happiness-14)
		if old.host=="homeless":health_target-=12;happiness_target-=24
		p.health=clampf(lerpf(float(old.health),health_target,1-exp(-h*.22)),0,100)
		p.happiness=clampf(lerpf(float(old.happiness),happiness_target,1-exp(-h*.35)),0,100)
		var learning_rate=0.05*float(live.education)
		if learning_rate>0:
			var education_target=100.0-0.03/learning_rate
			p.education=clampf(education_target+(float(old.education)-education_target)*exp(-learning_rate*h),0,100)
		else:p.education=clampf(float(old.education)-0.03*h,0,100)
		# Healthy ordinary residents at education15 are the 100% reference, not a hidden 15% penalty.
		p.efficiency=clampf((.6+.4*float(p.health)/78.)*(1.+.002*(float(p.education)-15.)),.45,1.3)
		p.demand=clampf(1+(float(p.education)-15)*.004,.85,1.4)
		live.pollution=pollution;live.unemployment=unemployment;live.health_target=clampf(health_target,0,100);live.happiness_target=clampf(happiness_target,0,100)
		p.living=live
		next.knowledge+=float(service.covered.get(gid,{}).get("education",0))*.02*h
