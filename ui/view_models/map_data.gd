extends RefCounted
## Pure presentation query. Source aware; missing, zero and inaccessible are distinct.
const SERVICES=["medical","education","commercial","entertainment"]
const NAMES={"population":"人口 · 居住与就业","normal":"城市","terrain":"地形与选址","medical":"医疗服务","education":"教育服务","commercial":"购物服务","entertainment":"休闲服务","pollution":"环境负担","heat":"工业余热","employment":"就业 · 建筑缺人","unemployed":"就业 · 居民找工作","food":"粮食调拨","material":"材料调拨","goods":"制成品调拨","energy":"电力供需","operator":"运营组织","change":"本次改变"}
static func current(g,planner,pending:bool=false) -> Dictionary:
	var just_finished=false
	for id in g.playable_ids():
		if int(g.s.projects[id].get("commissioned_tick",-1))==int(g.s.simulation_tick):just_finished=true
	if pending or just_finished or g.s.metrics.get("utilization",{}).is_empty():return planner.snapshot(g)
	return {"metrics":g.s.metrics,"audit":g.s.audit,"projects":g.s.projects,"groups":g.s.groups,"tiles":g.s.cells,"compiled":g.compiled,"forecast":false,"tick":g.s.simulation_tick,"snapshot_id":str(g.revision)+":"+str(g.s.simulation_tick)+":actual","source_phase":"committed_tick","error":""}
static func source_tile(s:Dictionary,id:String) -> String:
	if s.get("projects",{}).has(id):return str(s.projects[id].tile)
	if s.get("groups",{}).has(id):return source_tile(s,str(s.groups[id].host))
	if s.get("tiles",{}).has(id):return id
	return ""
static func best_sources(cell:Dictionary,kind:String) -> Dictionary:
	var sources={}
	for f in cell.get("fields",{}).get(kind,[]):
		var id=str(f.source_id);sources[id]=maxf(float(sources.get(id,0)),float(f.value))
	return sources
static func service(s:Dictionary,tile:String,kind:String) -> Dictionary:
	var people=0.;var delivered=0.;var sources=best_sources(s.tiles.get(tile,{}),kind);var live=false
	for id in sources:
		if float(sources[id])<=0:continue
		var p=s.projects.get(id,{})
		if p.get("status","")=="running" and not p.get("paused",true) and float(s.metrics.get("utilization",{}).get(id,{}).get("utilization",0))>0:live=true
	for gid in s.groups:
		var group=s.groups[gid];var host=s.projects.get(str(group.host),{})
		if host.get("tile","")!=tile:continue
		people+=float(group.count)
		if s.metrics.has("service_covered"):delivered+=float(s.metrics.service_covered.get(gid,{}).get(kind,0))
		else:delivered+=float(group.living.get(kind,0))*float(group.count)
	var known=s.get("error","")=="" and s.metrics.has("service_links")
	var ratio=clampf(delivered/maxf(.001,people),0,1)
	var reason="已服务"
	if not known:reason="未结算"
	elif people<=.001:reason="空地 / 无居民"
	elif ratio<.999:
		if sources.is_empty():reason="范围外 / 无可达来源"
		elif not live:reason="提供方未运行"
		else:reason="可达，但有效容量不足"
	return {"people":people,"served":delivered,"ratio":ratio,"sources":sources,"reachable":not sources.is_empty(),"known":known,"reason":reason}
static func tile_info(g,s:Dictionary,tile:String,kind:String) -> Dictionary:
	if SERVICES.has(kind):return service(s,tile,kind)
	var building=str(s.get("tiles",{}).get(tile,g.s.cells.get(tile,{})).get("building",""));var value=0.;var label="";var entries=[]
	if kind in ["pollution","heat"]:
		entries=s.tiles.get(tile,{}).get("fields",{}).get(kind,[])
		for e in entries:value+=float(e.value)
		label=("环境贡献 %+.3f" if kind=="pollution" else "热场贡献 %.3f")%value
	elif kind=="employment":
		var cap=float(s.get("compiled",{}).get("stats",{}).get(building,{}).get("jobs",0));var assigned=0.
		for link in s.metrics.get("employment_links",[]):
			if link.target==building:assigned+=float(link.people)
		value=assigned/maxf(.001,cap);label="到岗 %.1f / %.1f"%[assigned,cap]
		return {"value":value,"label":label,"known":s.metrics.has("employment_links"),"applicable":cap>.001,"count":cap,"assigned":assigned}
	elif kind=="population":
		var count=0.;var assigned=0.;var employed=0.
		for group in s.groups.values():
			if s.projects.get(str(group.host),{}).get("tile","")==tile:
				count+=float(group.count)
				for n in group.employment.values():employed+=float(n)
		for link in s.metrics.get("employment_links",[]):
			if link.target==building:assigned+=float(link.people)
		var st=s.get("compiled",{}).get("stats",{}).get(building,{})
		var housing=float(st.get("housing",0))*float(st.get("efficiency",1)) if s.projects.get(building,{}).get("status","")=="running" else 0.
		var jobs=float(st.get("jobs",0))
		return {"value":count/maxf(.001,housing),"known":true,"applicable":count+housing+jobs>0,"count":count,"housing":housing,"jobs":jobs,"assigned":assigned,"employed":employed,"label":"居住 %.1f/%.1f · 岗位 %.1f/%.1f"%[count,housing,assigned,jobs]}
	elif kind=="unemployed":
		var count=0.;var assigned=0.
		for gid in s.groups:
			var group=s.groups[gid]
			if s.projects.get(str(group.host),{}).get("tile","")!=tile:continue
			count+=float(group.count)
			for n in group.employment.values():assigned+=float(n)
		return {"value":(count-assigned)/maxf(.001,count),"label":"待就业 %.1f / %.1f"%[maxf(0,count-assigned),count],"known":true,"applicable":count>.001,"count":count}
	elif kind=="energy":
		var metrics:Dictionary=s.get("metrics",{});var produced=float(metrics.get("energy_sources",{}).get(building,0))
		var need=float(metrics.get("energy_requests",{}).get(building,0))
		for gid in s.get("groups",{}):
			if str(s.groups[gid].get("host",""))==building:need+=float(metrics.get("population_energy",{}).get(gid,0))
		var ratio=float(metrics.get("energy_ratio",1));var consumed=need*ratio
		return {"value":produced-consumed,"produced":produced,"consumed":consumed,"need":need,"ratio":ratio,
			"stock":0.,"lost":0.,"known":metrics.has("energy_sources") and metrics.has("energy_requests"),"applicable":produced+need>.001,
			"label":"供 %.1f / 需 %.1f · 满足 %.0f%%"%[produced,need,ratio*100.]}
	elif kind in ["food","material","goods"]:
		var prod=0.;var consume=0.;var lost=0.
		for e in s.metrics.get("activity_ledger",{}).get(building,[]):
			if e.resource!=kind:continue
			if e.kind=="input":consume+=float(e.rate)
			else:prod+=float(e.rate);lost+=float(e.get("lost",0))
		var stock=float(s.projects.get(building,{}).get("inventory",{}).get(kind,0))
		return {"value":prod-consume,"produced":prod,"consumed":consume,"stock":stock,"lost":lost,"label":"产 %.1f / 用 %.1f%s"%[prod,consume,"" if kind=="energy" else "/月"],"known":s.metrics.has("activity_ledger"),"applicable":prod+consume+stock>.001}
	return {"value":value,"label":label,"entries":entries,"known":s.get("error","")=="","applicable":true}
static func links(g,s:Dictionary,selected:String,kind:String) -> Array:
	var result=[]
	if selected=="":return result
	if SERVICES.has(kind) or kind=="normal":
		for l in s.metrics.get("service_links",[]):
			if kind!="normal" and l.kind!=kind:continue
			if selected in [l.source,l.home]:result.append({"source":str(l.source),"target":str(l.home),"amount":float(l.people),"kind":str(l.kind)})
	if kind in ["normal","employment","unemployed","population"]:
		for l in s.metrics.get("employment_links",[]):
			var home=s.groups.get(str(l.group),{}).get("host","")
			if selected in [home,l.target]:result.append({"source":str(home),"target":str(l.target),"amount":float(l.people),"kind":"employment"})
	if kind in ["pollution","normal"]:
		for l in s.metrics.get("exposure_links",[]):
			if selected in [l.source,l.home] and absf(float(l.pollution))>.00001:result.append({"source":str(l.source),"target":str(l.home),"amount":float(l.pollution),"kind":"pollution"})
	if kind=="heat":
		for l in s.metrics.get("heat_receipts",[]):
			for e in l.sources:
				if selected in [e.source,l.target]:result.append({"source":str(e.source),"target":str(l.target),"amount":float(e.amount),"kind":"heat"})
	if kind in ["food","material","goods"]:
		for id in s.metrics.get("activity_ledger",{}):
			for e in s.metrics.activity_ledger[id]:
				if e.kind=="input" and e.resource==kind and selected in [id,e.get("source_id","")]:result.append({"source":str(e.get("source_id","")),"target":str(id),"amount":float(e.rate),"kind":kind})
	result.sort_custom(func(a,b):return absf(a.amount)>absf(b.amount) if absf(a.amount)!=absf(b.amount) else (a.source+a.target)<(b.source+b.target))
	return result

static var layer_definitions:Dictionary={}
static func definition(kind:String) -> Dictionary:
	if layer_definitions.is_empty():
		var loaded=JSON.parse_string(FileAccess.get_file_as_string("res://content/presentation030/map_layers.json"))
		if loaded is Dictionary:
			for layer in loaded.get("layers",[]):layer_definitions[str(layer.get("query_kind",layer.id))]=layer
	return layer_definitions.get(kind,{})
static func display_max(kind:String,otherwise:float=1.) -> float:
	var values=definition(kind).get("legend",{}).get("display_breaks",[])
	return float(values[-1]) if not values.is_empty() else otherwise
