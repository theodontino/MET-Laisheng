extends RefCounted
## Read-only, one-step forecasts using the real engine. Never touches player RNG or stock.
const R=preload("res://core/rules.gd")
var cache={}
var token=""
var lru:Array=[]
var timings:Array=[]
func version(g) -> String:return str(g.revision)+":"+str(g.s.simulation_tick)+":"+str(g.s.projects.size())
func copy_game(g):
	var other=g.get_script().new();other.c=g.c;other.s=g.s.duplicate(true);other.preview_mode=true;other.refresh_metrics();return other
func reset_for(g) -> void:
	var key=version(g)
	if key!=token:cache={};token=key;lru=[]
func snapshot(g) -> Dictionary:
	reset_for(g)
	if cache.has("world"):return cache.world
	if not g.s.phase in ["play","failed"] or g.s.projects.is_empty():return {"metrics":g.s.metrics,"audit":{},"projects":g.s.projects,"groups":g.s.groups,"tiles":g.s.cells,"error":""}
	var clone=copy_game(g);var err=clone.Sim.tick(clone,float(g.c.balance.tick_hours))
	var result={"metrics":clone.s.metrics,"audit":clone.s.audit,"projects":clone.s.projects,"groups":clone.s.groups,"tiles":clone.s.cells,"compiled":clone.compiled,"error":err,"forecast":true,"tick":clone.s.simulation_tick,"snapshot_id":version(g)+":forecast","source_phase":"next_tick"}
	cache.world=result;return result
func compare(g,base:String,tile:String,offer:Dictionary={},host:String="",slot:String="") -> Dictionary:
	reset_for(g)
	var key=JSON.stringify([base,tile,offer,host,slot])
	if cache.has(key):return cache[key]
	var began=Time.get_ticks_usec()
	var reference=snapshot(g);var clone=copy_game(g);var target=host
	if host=="":
		if not clone.s.cells.has(tile) or clone.s.cells[tile].building!="" or "tile.water" in clone.s.cells[tile].traits:return {"ok":false,"error":"请选择未开发的陆地。"}
		var operator=str(offer.get("operator_org","commons"))
		target=clone.create_building(base,tile,operator,true).id
		if offer.is_empty():clone.s.projects[target].operator_unresolved=true
	if not offer.is_empty() and offer.has("bonus_id"):
		var chosen_slot=str(offer.get("slot",slot))
		var err=clone._install_bonus(offer.bonus_id,"building",target,str(offer.get("provider_org","commons")),chosen_slot)
		if err!="":return {"ok":false,"error":err}
	clone.refresh_metrics()
	var err=clone.Sim.tick(clone,float(g.c.balance.tick_hours))
	if err!="":return {"ok":false,"error":err}
	var st=clone.project_stats(target);var a=clone.s.metrics.utilization.get(target,{})
	var services={};var additional={}
	for kind in g.c.balance.service_kinds:
		var own=0.;var total_now=0.;var total_before=0.
		for link in clone.s.metrics.service_links:
			if link.kind==kind:
				total_now+=float(link.people)
				if link.source==target:own+=float(link.people)
		for link in reference.metrics.get("service_links",[]):
			if link.kind==kind:total_before+=float(link.people)
		services[kind]=own;additional[kind]=total_now-total_before
	var harmed=0.;var exposure_delta=0.
	for gid in g.s.groups:
		var old=reference.groups.get(gid,{});var now=clone.s.groups.get(gid,{})
		if old.is_empty() or now.is_empty():continue
		var diff=float(now.living.get("pollution",0))-float(old.living.get("pollution",0))
		if diff>.015:harmed+=float(g.s.groups[gid].count)
		exposure_delta+=diff*float(g.s.groups[gid].count)
	var inputs={};var retained={};var losses={}
	for entry in clone.s.metrics.activity_ledger.get(target,[]):
		if entry.kind=="input":inputs[entry.resource]=float(inputs.get(entry.resource,0))+float(entry.rate)
		else:
			retained[entry.resource]=float(retained.get(entry.resource,0))+float(entry.kept)
			losses[entry.resource]=float(losses.get(entry.resource,0))+float(entry.lost)
	var workers=0.
	for link in clone.s.metrics.employment_links:
		if link.target==target:workers+=float(link.people)
	var effects=[]
	for bid in g.playable_ids():
		var before=reference.metrics.get("utilization",{}).get(bid,{}).get("utilization",0)
		var after=clone.s.metrics.utilization.get(bid,{}).get("utilization",0)
		if float(after)<float(before)-.1:effects.append({"id":bid,"delta":float(after)-float(before)})
	var result={"ok":true,"target":target,"stats":st,"activity":a,"outputs":clone.s.projects[target].outputs_last,"retained":retained,"losses":losses,"inputs":inputs,"services":services,"added_service":additional,
		"workers":workers,"housing":clone.s.metrics.housing-reference.metrics.get("housing",0),"cashflow":clone.s.metrics.cashflow-reference.metrics.get("cashflow",0),
		"energy_demand":clone.s.metrics.energy_need-reference.metrics.get("energy_need",0),"energy_supply":clone.s.metrics.energy-reference.metrics.get("energy",0),
		"energy_ratio":clone.s.metrics.energy_ratio,"harmed":harmed,"exposure_delta":exposure_delta,"affected":effects,
		"existing_food_produced":reference.audit.get("resources",{}).get("food",{}).get("produced",0)/float(g.c.balance.tick_hours),"existing_food_consumed":reference.audit.get("resources",{}).get("food",{}).get("consumed",0)/float(g.c.balance.tick_hours),"recycled":clone.s.metrics.pollution_recycled-reference.metrics.get("pollution_recycled",0),"tiles":clone.s.cells,"trace":clone.compiled.trace.get(target,{}),"world":clone.s.metrics}
	result["map_delta"]=map_diff(reference,clone)
	result["map_snapshot"]=display_copy(clone)
	result["snapshot_id"]=version(g);result["candidate_key"]=key;result["forecast"]=true
	result["elapsed_ms"]=(Time.get_ticks_usec()-began)/1000.0
	timings.append(result.elapsed_ms)
	if timings.size()>200:timings.pop_front()
	remember(key,result);return result
func policy(g,id:String) -> Dictionary:
	reset_for(g);var key="policy:"+id
	if cache.has(key):return cache[key]
	var reference=snapshot(g);var clone=copy_game(g)
	clone.s.block.policies[clone.c.policies[id].slot]=id
	var err=clone.Sim.tick(clone,float(g.c.balance.tick_hours))
	var result={"ok":err=="","error":err,"cashflow":clone.s.metrics.cashflow,"delta":clone.s.metrics.cashflow-reference.metrics.get("cashflow",0),"tax":clone.s.metrics.income,"map_delta":map_diff(reference,clone),"map_snapshot":display_copy(clone),"snapshot_id":version(g)}
	remember(key,result);return result
func terrain(g,base:String) -> Dictionary:
	reset_for(g);var key="terrain:"+base
	if cache.has(key):return cache[key]
	var result={}
	if not g.c.projects.has(base):return result
	for tile in R.ids(g.s.cells):
		if g.s.cells[tile].building!="" or "tile.water" in g.s.cells[tile].traits:continue
		var clone=copy_game(g);var target=clone.create_building(base,tile,"commons",true).id
		clone.s.projects[target].operator_unresolved=true;clone.refresh_metrics()
		var st=clone.compiled.stats[target];var original=g.c.projects[base].stats;var ratio=1.
		var name="通用地块"
		for field in ["output.food","output.material","output.goods","output.energy","service.entertainment"]:
			if float(original.get(field,0))>0:
				var x=float(st.get(field,0))/float(original[field])
				if x>ratio:ratio=x;name="额定产能 ×%.2f"%ratio
		if base=="works" and st.energy_demand<original.energy_demand:ratio=1.25;name="水岸冷却 · 用电减少20%"
		result[tile]={"ratio":ratio,"label":name}
	remember(key,result);return result

func remember(key:String,result:Dictionary):
	cache[key]=result;lru.erase(key);lru.append(key)
	while lru.size()>16:
		var oldest=lru.pop_front();cache.erase(oldest)
func display_copy(g) -> Dictionary:
	return {"metrics":g.s.metrics,"audit":g.s.audit,"projects":g.s.projects,"groups":g.s.groups,"tiles":g.s.cells,"compiled":g.compiled,"forecast":true,"tick":g.s.simulation_tick,"source_phase":"next_tick","error":""}
func map_diff(before:Dictionary,after) -> Dictionary:
	var result={}
	for tile in after.s.cells:
		var entry={"services":{},"pollution":0.,"utilization":0.,"resources":{}}
		for kind in after.c.balance.service_kinds:
			var a=0.;var b=0.
			for link in before.metrics.get("service_links",[]):
				if link.kind==kind and before.projects.get(str(link.home),{}).get("tile","")==tile:a+=float(link.people)
			for link in after.s.metrics.get("service_links",[]):
				if link.kind==kind and after.s.projects.get(str(link.home),{}).get("tile","")==tile:b+=float(link.people)
			entry.services[kind]=b-a
		entry.pollution=R.aggregate(after.s.cells[tile].fields.get("pollution",[]))-R.aggregate(before.tiles.get(tile,{}).get("fields",{}).get("pollution",[]))
		var bid=str(after.s.cells[tile].building)
		if bid!="" and before.projects.has(bid):
			entry.utilization=float(after.s.metrics.utilization.get(bid,{}).get("utilization",0))-float(before.metrics.get("utilization",{}).get(bid,{}).get("utilization",0))
		result[tile]=entry
	return result
func scope(g,offer:Dictionary,host_type:String,host:String) -> Dictionary:
	reset_for(g);var key="scope:"+str(offer.get("bonus_id",""))+":"+host_type+":"+host+":"+str(offer.get("provider_org",""))
	if cache.has(key):return cache[key]
	var reference=snapshot(g);var clone=copy_game(g)
	var err=clone._install_bonus(str(offer.bonus_id),host_type,host,str(offer.provider_org),"")
	if err!="":return {"ok":false,"error":err}
	err=clone.Sim.tick(clone,float(g.c.balance.tick_hours))
	var result={"ok":err=="","error":err,"map_delta":map_diff(reference,clone),"map_snapshot":display_copy(clone),"snapshot_id":version(g),"cashflow":clone.s.metrics.cashflow-reference.metrics.get("cashflow",0)}
	remember(key,result);return result
