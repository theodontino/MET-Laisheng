extends RefCounted
## A pure view of one coherent actual/forecast snapshot. No writes, no formulas of the economy.
const D=preload("res://ui/components031/design.gd")
const P=preload("res://ui/presentation.gd")
const W=preload("res://ui/widgets.gd")
# The construction engine's read-only capacity function can consume the same
# snapshot as every other indicator, without cloning or advancing the game.
class ConstructionReading:
	extends RefCounted
	var s:Dictionary
	var compiled:Dictionary
	var c
	var ids:Array
	func playable_ids() -> Array:return ids
static func build(g,shot:Dictionary,id:String) -> Dictionary:
	var current=g.s.projects[id];var p=shot.get("projects",{}).get(id,current);var metrics=shot.get("metrics",{})
	var st=shot.get("compiled",{}).get("stats",{}).get(id,g.project_stats(id));var a=metrics.get("utilization",{}).get(id,{})
	var known=shot.get("error","")=="" and metrics.has("activity_ledger")
	var m={"id":id,"name":g.project_name(id),"base":current.def,"domain":D.building_domain(current.def),"operator":g.c.operators[current.operator_org].name,"operator_org":current.operator_org,
		"reading":"下一周期预计" if shot.get("forecast",false) else "最近周期实际","snapshot_id":shot.get("snapshot_id",""),"tick":shot.get("tick",0),"known":known,"current":current,"stats":st,
		"house":{},"outputs":[],"services":[],"inputs":[],"fees":[],"inventory":[],"groups":[],"jobs":float(st.get("jobs",0)),"assigned":0.,"effective":0.,"utilization":float(a.get("utilization",0)),"activity":a,"priority":int(current.get("work_priority",0)),"slots":[],"cohesion":g.cohesion(id),"ledger":metrics.get("activity_ledger",{}).get(id,[]),"fiscal":[],"employment":[],"service_links":[],"fields":shot.get("tiles",{}).get(current.tile,{}).get("fields",{}),"trace":shot.get("compiled",{}).get("trace",{}).get(id,{})}
	var count=0.;var health=0.;var happy=0.;var education=0.
	for gid in shot.get("groups",{}):
		var group=shot.groups[gid]
		if group.host!=id:continue
		m.groups.append(group);count+=float(group.count);health+=float(group.health)*float(group.count);happy+=float(group.happiness)*float(group.count);education+=float(group.education)*float(group.count)
	var capacity=float(st.get("housing",0))*float(st.get("efficiency",1))
	if capacity>0 or count>0:m.house={"count":count,"capacity":capacity,"vacant":maxf(0,capacity-count),"health":health/maxf(.001,count),"happiness":happy/maxf(.001,count),"education":education/maxf(.001,count)}
	var input={};var output={}
	for e in m.ledger:
		var r=str(e.resource)
		if e.kind=="input":
			if not input.has(r):input[r]={"id":r,"value":0.,"children":[],"unit":"电力" if r=="energy" else "/月"}
			input[r].value+=float(e.rate);input[r].children.append(e)
		else:
			if not output.has(r):output[r]={"id":r,"value":0.,"kept":0.,"lost":0.,"children":[],"unit":"电力" if r=="energy" else "/月"}
			output[r].value+=float(e.rate);output[r].kept+=float(e.get("kept",e.rate));output[r].lost+=float(e.get("lost",0));output[r].children.append(e)
	for r in ["food","material","goods","energy"]:
		if not output.has(r) and float(st.get("output."+r,0))>0:output[r]={"id":r,"value":0.,"kept":0.,"lost":0.,"children":[],"unit":"电力" if r=="energy" else "/月"}
		if input.has(r) and input[r].value>.000001:m.inputs.append(input[r])
		if output.has(r) and (output[r].value>.000001 or float(st.get("output."+r,0))>0):m.outputs.append(output[r])
	for e in metrics.get("fiscal_ledger",[]):
		if e.source_id!=id:continue
		m.fiscal.append(e)
		if e.kind=="expense":m.fees.append({"id":"currency","label":"住户维护" if e.name=="居民基础维护" else "建筑维护","value":float(e.rate),"unit":"通货/月","source":e})
	for kind in g.c.balance.service_kinds:
		var served=0.;var cap=0.;var shared=false;var member_pools=[];var pool_members={}
		for pool_id in metrics.get("service_pools",{}):
			var pool:Dictionary=metrics.service_pools[pool_id]
			if str(pool.get("kind",""))!=str(kind) or not id in pool.get("members",[]):continue
			cap+=float(pool.get("capacity",0));served+=maxf(0,float(pool.get("capacity",0))-float(pool.get("remaining",0)))
			shared=shared or pool.get("members",[]).size()>1;member_pools.append(pool_id)
			pool_members[pool_id]=pool.get("members",[]).duplicate()
		for link in metrics.get("service_links",[]):
			if link.kind==kind and (str(link.get("pool","")) in member_pools or link.source==id):m.service_links.append(link)
		if float(st.get("service."+kind,0))>0 or cap>0:
			m.services.append({"id":kind,"value":served,"capacity":cap,"unit":"人","shared":shared,"pool_ids":member_pools,"pool_members":pool_members})
	for link in metrics.get("employment_links",[]):
		if link.target==id:m.assigned+=float(link.people);m.effective+=float(link.effective);m.employment.append(link)
	for r in ["food","material","goods","energy"]:
		var cap=g.stock_cap_for(id,r);var stock=float(current.inventory.get(r,0))
		if cap>0 or stock>0:m.inventory.append({"id":r,"stock":stock,"capacity":cap,"public":id=="municipal" or "building.logistics" in current.traits or g.compiled.config.get(id,{}).get("overflow_relay",false)})
	var status={"code":"ok","message":"投入齐备","detail":"","action":""}
	if current.status=="construction":status={"code":"inactive","message":"施工中","detail":"还需 %.1f CP、%.1f 材料"%[current.job.work,current.job.material],"action":""}
	elif current.paused:status={"code":"inactive","message":"已暂停","detail":"暂停生产，固定维护仍计入收支。","action":""}
	elif not known or a.is_empty():status={"code":"unknown","message":"待结算","detail":"当前没有可信的运营结果。","action":""}
	else:
		var short=minf(float(a.get("labor",1)),minf(float(a.get("resources",1)),float(a.get("energy",1))))
		if short<.995:
			var cause="用工";var action="workforce"
			if float(a.get("energy",1))<minf(float(a.get("labor",1)),float(a.get("resources",1))):cause="电力";action="energy"
			elif float(a.get("resources",1))<float(a.get("labor",1)):cause="物资";action="resources"
			var detail="有效投入满足 %.0f%%；运行 %.0f%%。"%[100*short,100*m.utilization]
			if cause=="用工" and m.assigned>=m.jobs-.01 and m.jobs>0:detail="人数已满；有效劳动不足，人员效率是主要限制。"
			status={"code":"danger" if m.utilization<.05 else "warning","message":cause+"限制运行","detail":detail,"action":action}
		elif m.utilization<.995:
			var pressure="仓储压力" in str(a.get("bottleneck",""))
			status={"code":"warning","message":"仓储压力" if pressure else "运行受限","detail":str(a.get("bottleneck",""))+"；运行 %.0f%%。"%(100*m.utilization),"action":"storage" if pressure else "sources"}
		elif m.jobs<=0:status.detail="本建筑当前没有用工需求。"
	m.status=status
	for slot in g.slot_ids(id):
		var iid=str(current.slots[slot]);var type=g.slot_type(id,slot);var ins=g.s.bonus_instances.get(iid,{})
		var item={"stable_id":slot,"type":type,"label":g.slot_label(id,slot),"instance_id":iid,"bonus_id":str(ins.get("bonus_id","")),"provider_org":str(ins.get("provider_org","")),"contract_org":str(ins.get("contract_org",ins.get("provider_org",""))),"state":"empty" if iid=="" else ("installed" if ins.get("active",false) else "inactive"),"reason":"","parent":""}
		if type=="Flex":
			for other in g.slot_ids(id):
				var oi=str(current.slots[other])
				if oi=="":continue
				var b=g.c.bonuses[g.s.bonus_instances[oi].bonus_id]
				for rule in b.rules:
					if rule.op=="slot_add" and rule.name==slot:item.parent=b.name
		if iid!="" and item.bonus_id=="P06" and known and float(a.get("recycled",0))<=.000001 and current.status=="running" and not current.paused:
			item.state="conditional";item.reason="本周期没有回收产出；查看当地额度与运行条件。"
		m.slots.append(item)
	m.signature=JSON.stringify([id,current.status,current.operator_org,m.house.is_empty(),m.jobs>0,m.outputs.map(func(x):return x.id),m.services.map(func(x):return [x.id,x.pool_members]),m.inputs.map(func(x):return x.id),m.fees.map(func(x):return x.label),m.inventory.map(func(x):return x.id),current.slots,current.get("slot_types",{}),m.slots.map(func(x):return [x.state,x.contract_org,x.parent])])
	_compact(g,shot,m)
	return m

static func _compact(g,shot:Dictionary,m:Dictionary) -> void:
	# L2 is a projection of this same coherent snapshot. Full rows remain in m
	# for the explicitly opened L3 archive; no economy or RNG is changed here.
	var p:Dictionary=m.current
	var building=str(p.get("def",""))
	var constructing=str(p.get("status",""))=="construction" or not p.get("upgrade_job",{}).is_empty()
	var kind="residential" if "building.residential" in p.get("traits",[]) else "production"
	if building in ["neighborhood","public_center","medical_clinic","primary_school","commercial_center","garden","hospital","secondary_school"]:kind="service"
	if "building.logistics" in p.get("traits",[]):kind="logistics"
	if constructing:kind="construction"
	m.entity_kind=kind
	m.compact_metrics=[]
	m.primary_action={"id":"manage","label":"管理运营 ›","enabled":true}
	m.status_color=W.GREEN
	m.short_status="运行中"
	if not m.known:m.short_status="待结算";m.status_color=W.MUTED
	if p.get("paused",false):
		m.entity_kind="paused";m.short_status="已暂停";m.status_color=W.GOLD
		m.compact_metrics=[]
		m.primary_action={"id":"resume","label":"恢复运行","enabled":true}
		return
	if constructing:
		var job:Dictionary=p.get("upgrade_job",{}) if not p.get("upgrade_job",{}).is_empty() else p.get("job",{})
		var remaining=float(job.get("work",0))
		var fraction=clampf(1.-remaining/maxf(.000001,float(job.get("total",remaining))),0,1)
		var source=ConstructionReading.new()
		source.s={"block":g.s.block,"projects":shot.get("projects",g.s.projects)}
		source.compiled=shot.get("compiled",g.compiled);source.c=g.c;source.ids=g.playable_ids()
		var cp=g.Construction.capacity(source)*clampf(float(shot.get("metrics",{}).get("energy_ratio",1)),0,1)
		var wait_cp=0.
		var needed_material=0.
		for entry in g.Construction.queue(g):
			if entry.paused:continue
			needed_material+=float(entry.material)
			if str(entry.id)==str(m.id):break
			wait_cp+=float(entry.work)
		var stock=g.inventory_total("material")
		# Material may be produced and entirely consumed in this same tick.
		# Zero ending stock alone cannot prove construction has stopped.
		var material_empty=float(job.get("material",0))>0 and stock<=.000001
		var shot_project:Dictionary=shot.get("projects",{}).get(str(m.id),p)
		var progressed=str(p.get("status",""))=="construction" and float(shot_project.get("usage",0))>0
		if shot.get("forecast",false):
			var shot_job:Dictionary=shot_project.get("upgrade_job",{}) if not p.get("upgrade_job",{}).is_empty() else shot_project.get("job",{})
			progressed=progressed or float(shot_job.get("work",0))<remaining-.000001
		var blocked=cp<=.000001
		var uncertain=material_empty and not progressed
		var months=(wait_cp+remaining)/maxf(.000001,cp)
		var duration="停滞" if blocked else ("待供料" if uncertain else ("≥%.1f月"%months if months>=.1 else "≥%.1f日"%(months*12.)))
		var note="缺电 / 施工能力" if blocked else ("材料当前为零" if uncertain else ("需继续供料" if needed_material>stock+.000001 else "按当前队列"))
		m.short_status=note if blocked or uncertain else ("升级施工" if not p.get("upgrade_job",{}).is_empty() else "建设中")
		m.status_color=W.RED if blocked else W.GOLD
		m.compact_metrics=[{"kind":"ring","label":"施工","value":"%.0f%%"%(100*fraction),"ratio":fraction,"color":m.status_color},
			{"kind":"number","label":"最快剩余","value":duration,"note":note,"color":m.status_color,"tooltip":"当前工程剩余量与队列；能力来源："+str(m.reading)+"。这不是保证完工时间，按该共享施工能力、电力与前排工程估计最低工期；材料持续到位、队列和能力不变时才可能达到。"},
			{"kind":"status","label":"施工优先级","value":{-1:"稍后",0:"普通",1:"优先"}.get(int(p.get("construction_priority",0)),"普通"),"color":W.INFO}]
		m.primary_action={"id":"construction","label":"调整优先级 ›","enabled":true}
		return
	var abnormal=str(m.status.code) in ["warning","danger"]
	if abnormal:
		m.status_color=W.RED if str(m.status.code)=="danger" else W.GOLD
		m.short_status={"workforce":"用工受限","energy":"供电不足","resources":"物资受限","storage":"仓储压力"}.get(str(m.status.action),"运行受限")
		m.primary_action={"id":"shortage" if str(m.status.action) in ["workforce","energy"] else "sources","label":{"workforce":"管理用工 ›","energy":"查看电力缺口 ›","resources":"查看投入来源 ›","storage":"查看仓储明细 ›"}.get(str(m.status.action),"查看运行限制 ›"),"enabled":true,"topic":str(m.status.action)}
		m.compact_metrics=[{"kind":"status","label":"当前瓶颈","value":m.short_status,"color":m.status_color,"tooltip":str(m.status.detail)},
			{"kind":"ring","label":"实际运行","value":"%.0f%%"%(100*float(m.utilization)),"ratio":float(m.utilization),"color":m.status_color}]
		return
	if kind=="residential":
		var house:Dictionary=m.house
		var count=float(house.get("count",0));var capacity=float(house.get("capacity",0))
		var active=["food","material","energy"]
		if g.s.get("goods_active",false):active.append("goods")
		if float(g.s.local_hours)>=float(g.c.balance.service_start_hours):active.append_array(g.c.balance.service_kinds)
		var satisfaction=0.
		var living_known=m.known and count>0
		for group in m.groups:
			var sum=0.
			if float(group.count)<=0:continue
			for need in active:
				if not group.get("living",{}).has(need):living_known=false
				sum+=clampf(float(group.get("living",{}).get(need,0)),0,1)
			satisfaction+=float(group.count)*sum/maxi(1,active.size())
		satisfaction/=maxf(.000001,count)
		m.compact_metrics=[{"kind":"ring","label":"入住","value":"%.0f%%"%(100*count/maxf(.000001,capacity)),"ratio":count/maxf(.000001,capacity),"note":"%.0f / %.0f"%[count,capacity],"color":W.GREEN},
			{"kind":"ring","label":"宜居","value":"%.0f%%"%(100*satisfaction) if living_known else "—","ratio":satisfaction if living_known else 0.,"note":"" if living_known else "等待结算","color":(W.GREEN if satisfaction>=.8 else W.GOLD) if living_known else W.MUTED,"tooltip":"当前已启用生活需求的实际满足率，先对每组需求求均值，再按本楼住户人数加权；不是新的模拟属性。缺少住户结算记录时显示等待结算，不视为零满足。"},
			{"kind":"number","label":"人口","value":"%.1f"%count,"icon":"population","color":W.TEXT}]
	elif kind=="logistics":
		var items=[]
		for resource in ["food","material","goods"]:
			var amount=float(p.get("inventory",{}).get(resource,0));var capacity=g.stock_cap_for(str(m.id),resource)
			items.append({"id":resource,"text":"%.1f / %.0f"%[amount,capacity],"ratio":amount/maxf(.000001,capacity)})
		m.compact_metrics=[{"kind":"bars","label":"库存 / 容量","items":items,"color":W.INFO,"tooltip":"本建筑当前粮食、材料、制成品库存；不是全城储备。"}]
		m.primary_action={"id":"market","label":"打开市场 ›","enabled":true}
		return
	elif kind=="service":
		var items=[]
		var shared=false
		for service in m.services:
			shared=shared or bool(service.get("shared",false))
		for service in m.services.slice(0,3):items.append({"id":service.id,"text":"%.0f / %.0f"%[service.value,service.capacity],"ratio":float(service.value)/maxf(.000001,float(service.capacity))})
		m.compact_metrics=[{"kind":"bars","label":"共享服务池" if shared else "服务 / 有效容量","items":items,"color":W.INFO,"tooltip":str(m.reading)+"：直接读取服务池的已服务人数 / 有效容量。共享池为相邻成员合计，不把最可达入口误当成该建筑独自承担；完整成员与来源在详情中。"},
			{"kind":"ring","label":"实际运行","value":"%.0f%%"%(100*float(m.utilization)) if m.known else "—","ratio":float(m.utilization),"color":W.GREEN}]
	else:
		var items=[]
		for resource in m.outputs.slice(0,3):items.append({"id":resource.id,"text":("%.1f"%float(resource.value)+("/月" if str(resource.id)!="energy" else "")) if m.known else "—"})
		m.compact_metrics=[{"kind":"outputs","label":"产出","items":items,"color":W.GREEN,"tooltip":"物资产出按来生月；闪电图标为当期电力能力，不是累计电量。其他能力在详情中。"},
			{"kind":"ring","label":"实际运行","value":"%.0f%%"%(100*float(m.utilization)) if m.known else "—","ratio":float(m.utilization),"color":W.GREEN},
			{"kind":"status","label":"瓶颈","value":"无" if m.known else "待结算","color":W.GREEN if m.known else W.MUTED}]
	if not g.Development.choices(g,str(m.id)).is_empty():m.primary_action={"id":"upgrade","label":"选择升级方向 ›","enabled":true}
	elif kind=="service":m.primary_action={"id":"services","label":"管理服务 ›","enabled":true}
