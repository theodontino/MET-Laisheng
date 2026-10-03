extends RefCounted
## One stable queue spends CP and material together, including upgrades.
static func capacity(g) -> float:
	var n=float(g.s.block.get("construction_capacity",g.c.v040.progression.construction_capacity))
	n+=float(g.compiled.get("options",{}).get("construction_capacity",0))
	for id in g.playable_ids():
		var p=g.s.projects[id]
		if p.status=="running" and not p.paused:n+=float(g.compiled.stats.get(id,{}).get("construction_capacity",0))*float(p.usage)
	return maxf(0,n*float(g.compiled.get("options",{}).get("construction_efficiency",1)))
static func queue(g) -> Array:
	var result=[]
	for id in g.playable_ids():
		var p=g.s.projects[id];var upgrading=not p.get("upgrade_job",{}).is_empty()
		if not upgrading and p.status!="construction":continue
		var job=p.upgrade_job if upgrading else p.job
		result.append({"id":id,"name":g.project_name(id),"upgrade":upgrading,"work":job.work,"total":job.total,"material":job.material,"priority":int(p.get("construction_priority",0)),"paused":p.paused})
	result.sort_custom(func(a,b):return a.priority>b.priority if a.priority!=b.priority else a.id<b.id)
	return result
static func set_priority(g,id:String,value:int) -> String:
	if not g.s.projects.has(id):return "项目不存在。"
	g.s.projects[id].construction_priority=clampi(value,-1,1);g.refresh_metrics();return ""
static func tick(g,h:float) -> void:
	var remaining=capacity(g)*h*clampf(float(g.s.metrics.get("energy_ratio",1)),0,1);var used=0.;var material_used=0.
	for row in queue(g):
		if row.paused:continue
		var p=g.s.projects[row.id];var job=p.upgrade_job if row.upgrade else p.job
		var rate=float(job.material)/maxf(.000001,float(job.work));var work=minf(remaining,float(job.work))
		if rate>0:work=minf(work,g.inventory_total("material")/rate)
		var material=work*rate
		if material>0:g.withdraw_stock("material",material)
		job.work=maxf(0,float(job.work)-work);job.material=maxf(0,float(job.material)-material)
		remaining-=work;used+=work;material_used+=material
		if not row.upgrade:p.usage=work/maxf(.000001,h*capacity(g));p.bottleneck="施工中" if work>0 else "等待施工能力或材料"
		if float(job.work)<.000001:g.Development.complete(g,row.id)
	g.s.block.construction_last={"cp":used,"material":material_used,"hours":h}
	if g.s.audit.get("resources",{}).has("material"):
		var a=g.s.audit.resources.material;a.consumed+=material_used;a.after=g.inventory_total("material");a.error=float(a.before)+float(a.produced)-float(a.consumed)-float(a.lost)-float(a.after)
		g.s.metrics.flows.material=float(g.s.metrics.flows.get("material",0))-material_used/h
	g.refresh_metrics()
