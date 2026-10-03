extends RefCounted
## Project identity survives specialization; only its definition and unlocked track grow.
static func ensure_project(g,p:Dictionary) -> void:
	if p.special:return
	var d=g.c.projects[p.def]
	if p.has("family_id"):return
	p.family_id=d.family_id;p.node_id=p.def;p.tier=int(d.tier)
	p.upgrade_choice="";p.upgrade_job={};p.construction_priority=0
	p.slot_track=g.c.operators[p.operator_org].slot_track.duplicate()
	p.unlocked_slots=0;p.slots={};p.slot_types={}
	if p.status=="running":unlock(p)
static func unlock(p:Dictionary) -> void:
	while int(p.unlocked_slots)<int(p.tier):
		var index=int(p.unlocked_slots);var type=str(p.slot_track[index]);var key=type;var suffix=2
		while p.slots.has(key):key=type+":"+str(suffix);suffix+=1
		p.slots[key]="";p.slot_types[key]=type;p.unlocked_slots=index+1
static func choices(g,id:String) -> Array:
	if not g.s.projects.has(id):return []
	return g.c.projects.get(g.s.projects[id].def,{}).get("upgrades",[]).duplicate()
static func preview(g,id:String,node:String) -> Dictionary:
	if not node in choices(g,id):return {"ok":false,"error":"这不是该建筑的升级方向。"}
	var p=g.s.projects[id];var d=g.c.projects[node];var old=g.c.projects[p.def];var gains=[];var losses=[]
	var keys=old.stats.keys()
	for key in d.stats:
		if not key in keys:keys.append(key)
	keys.sort()
	for key in keys:
		var delta=float(d.stats.get(key,0))-float(old.stats.get(key,0))
		if absf(delta)<.001:continue
		var row={"stat":key,"before":old.stats.get(key,0),"after":d.stats.get(key,0),"delta":delta}
		if delta>0:gains.append(row)
		else:losses.append(row)
	return {"ok":true,"node":node,"name":d.name,"cost":d.cost,"material":d.material,"construction_required":d.construction_required,"eta":float(d.construction_required)/maxf(.01,g.Construction.capacity(g)),"gains":gains,"losses":losses,"operation_factor":g.c.v040.progression.upgrade_operation_factor,"slot":p.slot_track[int(p.tier)]}
static func start_upgrade(g,id:String,node:String) -> String:
	var view=preview(g,id,node)
	if not view.ok:return view.error
	var p=g.s.projects[id]
	if g.s.phase!="play" or p.status!="running" or not p.upgrade_job.is_empty():return "请等当前施工完成。"
	if g.s.currency<float(view.cost):return "升级经费不足。"
	g.s.currency-=float(view.cost);g.s.totals.currency_spent+=float(view.cost)
	p.upgrade_choice=node;p.upgrade_job={"work":view.construction_required,"total":view.construction_required,"material":view.material,"cp":true}
	g.log_event("upgrade_started",{"project":id,"node":node},g.project_name(id)+"开始升级。");g.refresh_metrics();return ""
static func complete(g,id:String) -> void:
	var p=g.s.projects[id]
	if not p.upgrade_job.is_empty():
		var previous=p.def;var d=g.c.projects[p.upgrade_choice]
		p.history.append({"kind":"upgrade","from":previous,"to":d.id,"h":g.s.local_hours})
		p.def=d.id;p.node_id=d.id;p.tier=int(d.tier);p.traits=d.traits.duplicate();p.upgrade_choice="";p.upgrade_job={}
		# Stock already owned remains; excess drains normally rather than being erased on upgrade.
		p.retained_storage=p.inventory.duplicate(true)
		g.s.sites[p.site].zone=d.zone;g.s.cells[p.tile].zone=d.zone
		g.log_event("upgraded",{"project":id,"node":d.id},d.name+"升级完成，解锁新方案槽。")
	else:
		p.status="running";p.job={};p.commissioned_tick=g.s.simulation_tick;g.s.totals.built+=1
		g.log_event("built",{"project":id},g.project_name(id)+"建成。")
	unlock(p)
