extends RefCounted
## A pure view of one coherent actual/forecast snapshot. No writes, no formulas of the economy.
const D=preload("res://ui/components031/design.gd")
const P=preload("res://ui/presentation.gd")
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
		var served=0.
		for link in metrics.get("service_links",[]):
			if link.source==id and link.kind==kind:served+=float(link.people);m.service_links.append(link)
		var cap=float(st.get("service."+kind,0))*float(st.efficiency)*float(a.get("utilization",0))
		if float(st.get("service."+kind,0))>0 or served>0:m.services.append({"id":kind,"value":served,"capacity":cap,"unit":"人"})
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
		elif m.utilization<.995:status={"code":"warning","message":"运行受限","detail":"运行 %.0f%%；查看投入和仓容明细。"%(100*m.utilization),"action":"resources"}
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
	m.signature=JSON.stringify([id,current.status,current.operator_org,m.house.is_empty(),m.jobs>0,m.outputs.map(func(x):return x.id),m.services.map(func(x):return x.id),m.inputs.map(func(x):return x.id),m.fees.map(func(x):return x.label),m.inventory.map(func(x):return x.id),current.slots,current.get("slot_types",{}),m.slots.map(func(x):return [x.state,x.contract_org,x.parent])])
	return m
