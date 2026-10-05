extends RefCounted
## Same-step, read-only comparisons. Units never share an invented combined score.
const P=preload("res://ui/presentation.gd")
const Data=preload("res://ui/view_models/map_data.gd")
const RESOURCES=["food","material","goods"]

static func build(g,reference:Dictionary,result:Dictionary) -> Dictionary:
	var after:Dictionary=result.get("map_snapshot",{})
	var valid=bool(result.get("ok",false)) and not after.is_empty() and not reference.is_empty()
	var model={"ok":valid,"resources":[],"energy":{},"environment":{},"housing":float(result.get("housing",0)),"snapshot_id":str(result.get("snapshot_id","")),"map_snapshot_forecast":true}
	if not valid:return model
	for kind in RESOURCES:
		var before=P.resource(g,reference,kind);var next=P.resource(g,after,kind)
		var capacity=0.
		for id in g.s.projects:capacity+=g.stock_cap_for(str(id),kind)
		var known=reference.get("audit",{}).get("resources",{}).has(kind) and after.get("audit",{}).get("resources",{}).has(kind)
		var output=float(result.get("outputs",{}).get(kind,0))
		var status="供需平稳"
		if not known:status="等待有效结算"
		elif float(next.need)>float(next.consumed)+.001:status="仍有需求未满足"
		elif float(next.lost)>.001:status="仓储正在损耗"
		elif float(next.net)<-.001:status="储备正在减少"
		elif float(next.net)>.001:status="储备正在增加"
		model.resources.append({"id":kind,"name":P.RES[kind],"known":known,"stock":float(before.stock),"capacity":capacity,
			"before_supply":float(before.produced)+float(before.imported),"before_demand":float(before.need),
			"after_supply":float(next.produced)+float(next.imported),"after_demand":float(next.need),
			"before_consumed":float(before.consumed),"after_consumed":float(next.consumed),
			"before_net":float(before.net),"after_net":float(next.net),"net_delta":float(next.net)-float(before.net),
			"output":output,"need_share":output/float(next.need) if float(next.need)>.001 else 0.,"share_known":known and float(next.need)>.001,
			"runway_before":float(before.stock)/-float(before.net) if float(before.net)<-.001 else -1.,
			"runway_after":float(before.stock)/-float(next.net) if float(next.net)<-.001 else -1.,
			"loss":float(next.lost),"status":status})
	var old:Dictionary=reference.get("metrics",{});var next:Dictionary=after.get("metrics",{})
	model.energy={"known":old.has("energy") and next.has("energy"),
		"before_supply":float(old.get("energy",0)),"before_need":float(old.get("energy_need",0)),
		"after_supply":float(next.get("energy",0)),"after_need":float(next.get("energy_need",0)),
		"before_ratio":float(old.get("energy_ratio",1)),"after_ratio":float(next.get("energy_ratio",1)),
		"supply_delta":float(next.get("energy",0))-float(old.get("energy",0)),"need_delta":float(next.get("energy_need",0))-float(old.get("energy_need",0))}
	var env={"total_tiles":g.s.cells.size(),"affected_tiles":0,"increased_tiles":0,"reduced_tiles":0,"harmed":float(result.get("harmed",0)),
		"exposure_delta":float(result.get("exposure_delta",0)),"max_increase":0.,"max_reduction":0.}
	for key in g.s.cells:
		var amount=float(Data.tile_info(g,after,str(key),"pollution").value)-float(Data.tile_info(g,reference,str(key),"pollution").value)
		if amount>.00001:env.increased_tiles+=1;env.max_increase=maxf(float(env.max_increase),amount)
		elif amount<-.00001:env.reduced_tiles+=1;env.max_reduction=minf(float(env.max_reduction),amount)
	env.affected_tiles=int(env.increased_tiles)+int(env.reduced_tiles);model.environment=env
	return model

static func tile_delta(g,before:Dictionary,after:Dictionary,key:String) -> Dictionary:
	var services={};var resources={};var energy={};var positive=false;var negative=false
	for kind in Data.SERVICES:
		var a=Data.service(before,key,str(kind));var b=Data.service(after,key,str(kind))
		var delta=float(b.served)-float(a.served);services[kind]=delta
		positive=positive or delta>.001;negative=negative or delta<-.001
	for kind in RESOURCES:
		var a=Data.tile_info(g,before,key,str(kind));var b=Data.tile_info(g,after,key,str(kind))
		var delta=float(b.get("value",0))-float(a.get("value",0))
		resources[kind]={"net":delta,"produced":float(b.get("produced",0))-float(a.get("produced",0)),
			"consumed":float(b.get("consumed",0))-float(a.get("consumed",0)),"stock":float(b.get("stock",0))-float(a.get("stock",0))}
		positive=positive or delta>.001;negative=negative or delta<-.001
	var a=Data.tile_info(g,before,key,"energy");var b=Data.tile_info(g,after,key,"energy")
	energy={"supply":float(b.get("produced",0))-float(a.get("produced",0)),"need":float(b.get("need",0))-float(a.get("need",0)),
		"before_ratio":float(a.get("ratio",1)),"after_ratio":float(b.get("ratio",1))}
	positive=positive or float(energy.supply)>.001
	# New demand is an opportunity cost, not a declaration of a power failure.
	negative=negative or float(energy.need)>.001
	var pollution=float(Data.tile_info(g,after,key,"pollution").value)-float(Data.tile_info(g,before,key,"pollution").value)
	var heat=float(Data.tile_info(g,after,key,"heat").value)-float(Data.tile_info(g,before,key,"heat").value)
	var old_id=str(before.get("tiles",{}).get(key,{}).get("building",""))
	var new_id=str(after.get("tiles",{}).get(key,{}).get("building",""))
	var utilization=0.
	if old_id!="" and old_id==new_id:
		utilization=float(after.get("metrics",{}).get("utilization",{}).get(new_id,{}).get("utilization",0))-float(before.get("metrics",{}).get("utilization",{}).get(old_id,{}).get("utilization",0))
	positive=positive or pollution<-.00001 or utilization>.001
	negative=negative or pollution>.00001 or utilization<-.001
	return {"services":services,"resources":resources,"energy":energy,"pollution":pollution,"heat":heat,"utilization":utilization,
		"positive":positive,"negative":negative,"changed":positive or negative or absf(heat)>.00001}
