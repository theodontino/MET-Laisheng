extends RefCounted
## Read-only data for clickable HUD, population map, account book and tasks.
const R=preload("res://core/rules.gd")
const P=preload("res://ui/presentation.gd")
const DATA=preload("res://ui/view_models/map_data.gd")
const RES=["food","material","goods","energy"]
const SERVICES=["medical","education","commercial","entertainment"]
static func stamp(shot:Dictionary) -> String:
	return "下一周期预计" if shot.get("forecast",false) else "最近结算 · 实际"
static func population(g,shot:Dictionary) -> Dictionary:
	var count=0.;var housed=0.;var employed=0.;var health=0.;var happiness=0.;var education=0.;var rows=[];var totals={}
	var m=shot.get("metrics",{});var groups=shot.get("groups",{});var projects=shot.get("projects",{});var active=["food","material","energy"]
	if g.s.goods_active:active.append("goods")
	if float(g.s.local_hours)>=float(g.c.balance.service_start_hours):active.append_array(SERVICES)
	for k in RES+SERVICES:totals[k]=0.
	for gid in R.ids(groups):
		var group=groups[gid];var n=float(group.count);var workers=0.;var home=str(group.host)
		for v in group.employment.values():workers+=float(v)
		count+=n;employed+=workers;health+=n*float(group.health);happiness+=n*float(group.happiness);education+=n*float(group.education)
		if home!="homeless":housed+=n
		var coverage={};var mean=0.
		for k in RES+SERVICES:
			var ratio=float(group.living.get(k,0))
			if k in SERVICES and m.has("service_covered"):ratio=float(m.service_covered.get(gid,{}).get(k,0))/maxf(.000001,n)
			ratio=clampf(ratio,0,1);coverage[k]=ratio;totals[k]+=ratio*n
			if k in active:mean+=ratio
		rows.append({"id":gid,"home":home,"name":g.project_name(home),"count":n,"employed":workers,"health":float(group.health),"happiness":float(group.happiness),"education":float(group.education),"coverage":coverage,"mean_coverage":mean/maxi(1,active.size()),"jobs":group.employment.duplicate(true)})
	for k in totals:totals[k]/=maxf(.000001,count)
	var mean=0.
	for k in active:mean+=totals[k]
	var j=g.s.get("civic032",{})
	return {"count":count,"housed":housed,"homeless":count-housed,"housing":float(m.get("housing",0)),"employed":employed,"unemployed":maxf(0,count-employed),"jobs":float(m.get("jobs",0)),"happiness":happiness/maxf(.000001,count),"health":health/maxf(.000001,count),"education":education/maxf(.000001,count),"coverage":mean/maxi(1,active.size()),"coverage_kinds":totals,"active_kinds":active,"rows":rows,"population_rate":j.get("last_population_rate"),"birth_rate":j.get("last_birth_rate"),"arrival_rate":j.get("last_arrival_rate"),"sample_h":float(j.get("last_cycle_h",0)),"stamp":stamp(shot),"known":m.has("service_links")}
static func budget(g,shot:Dictionary) -> Dictionary:
	var m=shot.get("metrics",{});var income=[];var expense=[]
	for row in m.get("fiscal_ledger",[]):
		var r=row.duplicate(true);r.source=g.project_name(str(row.source_id))
		if row.kind=="income":income.append(r)
		else:expense.append(r)
	var j=g.s.get("civic032",{})
	return {"balance":float(g.s.currency),"income":float(m.get("income",0)),"expense":float(m.get("upkeep",0)),"net":float(m.get("cashflow",0)),"income_rows":income,"expense_rows":expense,"history":j.get("history",[]),"transactions":j.get("transactions",[]),"started_h":float(j.get("started_h",g.s.local_hours)),"stamp":stamp(shot)}
static func resource(g,shot:Dictionary,id:String) -> Dictionary:
	var sources=[];var sinks=[];var stores=[];var m=shot.get("metrics",{})
	if id=="energy":
		for bid in R.ids(m.get("energy_sources",{})):sources.append({"id":bid,"name":g.project_name(bid),"rate":float(m.energy_sources[bid]),"note":"供电"})
		for bid in R.ids(m.get("energy_requests",{})):sinks.append({"id":bid,"name":g.project_name(bid),"rate":float(m.energy_requests[bid]),"note":"建筑申请"})
		for gid in R.ids(m.get("population_energy",{})):
			var host=str(shot.groups.get(gid,{}).get("host","homeless"));sinks.append({"id":host,"name":g.project_name(host)+" · 住户","rate":float(m.population_energy[gid]),"note":"居民申请"})
		return {"id":id,"name":"电力","supply":float(m.get("energy",0)),"need":float(m.get("energy_need",0)),"ratio":float(m.get("energy_ratio",1)),"sources":sources,"sinks":sinks,"stores":[],"stamp":stamp(shot)}
	var result=P.resource(g,shot,id)
	if float(result.get("imported",0))>0:sources.append({"id":"external_market","name":"外部市场进口","rate":float(result.imported),"note":"真实成交，受限价与外贸运力约束","kept":float(result.imported),"lost":0.0})
	if float(result.get("exported",0))>0:sinks.append({"id":"external_market","name":"外部市场出口","rate":float(result.exported),"note":"真实成交"})
	if float(result.get("delivered",0))>0:sinks.append({"id":"commission","name":"主题交付","rate":float(result.delivered),"note":"已从库存支付"})
	for bid in R.ids(m.get("activity_ledger",{})):
		for row in m.activity_ledger[bid]:
			if row.resource!=id or absf(float(row.rate))<.000001:continue
			var entry={"id":bid,"name":g.project_name(bid),"rate":float(row.rate),"note":str(row.get("source","")),"kept":float(row.get("kept",0)),"lost":float(row.get("lost",0))}
			if row.kind=="output":sources.append(entry)
			else:sinks.append(entry)
	for bid in R.ids(g.s.projects):
		var p=g.s.projects[bid];var capacity=g.stock_cap_for(bid,id);var stock=float(p.inventory.get(id,0))
		if capacity<=0 and stock<=0:continue
		stores.append({"id":bid,"name":g.project_name(bid),"stock":stock,"capacity":capacity,"external":(bid=="municipal" or "building.logistics" in p.traits or g.compiled.config[bid].get("overflow_relay",false))})
	result.merge({"id":id,"name":P.RES[id],"sources":sources,"sinks":sinks,"stores":stores,"stamp":stamp(shot)},true)
	return result
