extends RefCounted
## Six deterministic steps. Rules read a stable tick; receipts commit once.
const R=preload("res://core/rules.gd")
const F=preload("res://core/fields.gd")
const A=preload("res://core/allocation.gd")
const Pop=preload("res://core/population.gd")
const Progress=preload("res://core/progress.gd")
const Store=preload("res://core/storage.gd")
const RES=["food","material","goods"]

static func spatial_snapshot(g,compiled:Dictionary) -> Dictionary:
	# This deliberately conservative key contains every input read by F.access.
	# It is independent of stocks, temporal counters and allocations. Full stats
	# may invalidate more often than needed, but can never hide a range/capacity
	# or Rule change. Only the last snapshot is retained outside saved state.
	var projects=[]
	for id in R.ids(g.s.projects):
		var p:Dictionary=g.s.projects[id]
		projects.append([id,p.get("tile",""),p.get("status",""),p.get("paused",false),p.get("special",false),p.get("traits",[])])
	var volunteer=[]
	for id in R.ids(compiled.config):
		var gate:Dictionary=compiled.config[id].get("volunteer_education",{})
		if gate.is_empty():continue
		var eligible=false
		for group in g.s.groups.values():
			if group.host==id and float(group.education)>=float(gate.threshold):eligible=true;break
		volunteer.append([id,eligible])
	var group_origins=[]
	for id in R.ids(g.s.get("authored_fields",{})):
		var field:Dictionary=g.s.authored_fields[id]
		var source=str(field.get("source_id",""))
		if str(field.get("origin",""))=="" and g.s.groups.has(source):group_origins.append([source,g.s.groups[source].host])
	var key=JSON.stringify([R.ids(g.s.cells),projects,compiled.stats,compiled.config,compiled.emitters,compiled.options,
		g.s.get("authored_fields",{}),group_origins,volunteer,g.c.balance.service_kinds],"",true,true)
	if g.spatial_cache.get("key","")==key:
		var reused:Dictionary=g.spatial_cache.access
		g.spatial_cache={"key":key,"access":reused,"hit":true}
		return reused
	var access=F.access(g,compiled)
	g.spatial_cache={"key":key,"access":access,"hit":false}
	return access

static func total(inventories:Dictionary,r:String) -> float:
	var n=0.
	for id in R.ids(inventories):n+=float(inventories[id].get(r,0))
	return n
static func advance(g,months:float,paid:bool,_stop_events:bool=false) -> Dictionary:
	# Compatibility entry only: all advancement uses the same clock service.
	var mode="advance" if paid else "normal"
	var acceleration=25920.0 if paid else 1.0
	return g.Clock.advance(g,months*float(g.c.beta.units.earth_seconds_per_month)/acceleration,mode)
static func tick(g,h:float,context:Dictionary={}) -> String:
	if h<=0 or not is_finite(h):return "结算时间必须为正的有限月数。"
	var s=g.s;var previous_population=0.
	for group in s.groups.values():previous_population+=float(group.count)
	var arrived=0.
	var next=s.duplicate(true);var compiled=R.compile(g);var stats=compiled.stats;var config=compiled.config
	# 1. Spatial snapshot. Every receiver retains each Field's source.
	var access=spatial_snapshot(g,compiled)
	# 2. Offers/claims resolve globally using explicit ordering, not object traversal order.
	var labor=A.labor(s,compiled,access);var claims={};var recipe_claims={};var population_energy={};var energy_requests={}
	var kitchens={}
	for gid in R.ids(s.groups):kitchens[gid]=0.
	for bid in R.ids(s.projects):
		var p=s.projects[bid];var capacity=float(config[bid].get("kitchen_capacity",0))
		if capacity<=0 or p.status!="running" or p.paused:continue
		for gid in R.ids(s.groups):
			var group=s.groups[gid]
			if R.near(s,bid,group.host)>1:continue
			var n=minf(capacity,maxf(0,float(group.count)-float(kitchens[gid])));capacity-=n;kitchens[gid]+=n
	for bid in R.ids(s.projects):
		var p=s.projects[bid];var st=stats[bid]
		if p.special:continue
		var needs={}
		if p.status=="construction":
			var work=0.;var fraction=work/maxf(.000001,float(p.job.work))
			needs.material=float(p.job.material)*fraction
			claims["work:"+bid]={"owner":bid,"needs":needs};energy_requests[bid]=.5
		elif p.status=="running" and not p.paused:
			for r in RES:
				if st["input."+r]>0:needs[r]=float(st["input."+r])*h
			claims["run:"+bid]={"owner":bid,"needs":needs}
			energy_requests[bid]=maxf(0,float(st.energy_demand)-minf(float(p.next_heat_credit),float(st.energy_demand)*float(st.heat_discount)))
		for recipe in compiled.recipes[bid]:
			if p.status!="running" or p.paused:continue
			if recipe.get("targets","")=="service_area":
				var reachable=false
				for kind in g.c.balance.service_kinds:
					if F.source_value(access.tiles,p.tile,kind,recipe.source_id)>0:reachable=true
				if not reachable:continue
			var cid="recipe:"+recipe.uid;var inputs={}
			for resource in recipe.input:inputs[resource]=float(recipe.input[resource])*float(recipe.rate)*h
			claims[cid]={"owner":bid,"needs":inputs};recipe_claims[cid]={"owner":bid,"recipe":recipe}
	for gid in R.ids(s.groups):
		var group=s.groups[gid];var st=stats[group.host];var n=float(group.count);var demand=float(group.demand)
		var inputs={"food":n*float(st.population_food)*h,"material":n*float(st.population_material)*h}
		inputs.food*=1-.2*float(kitchens[gid])/maxf(.001,n)
		if s.goods_active:inputs.goods=n*float(st.population_goods)*demand*h
		claims["pop:"+gid]={"owner":group.host,"needs":inputs}
		population_energy[gid]=n*float(st.population_energy)*demand
	# Reserve only activity that has people to operate it. Then free reservations
	# blocked by other recipe ingredients, before any inventory is committed.
	var ceilings={}
	for cid in R.ids(claims):
		var owner=claims[cid].owner;var st=stats[owner]
		var labor_cap=1. if st.jobs<=0 else minf(1,float(labor.effective.get(owner,0))/float(st.jobs))
		ceilings[cid]=1. if cid.begins_with("pop:") or cid.begins_with("work:") else labor_cap
	var reserved=A.feasible_resources(s,compiled,claims,access,ceilings);var actual={};var activity={};var energy_supplied=float(g.c.balance.base_energy);var recipe_energy={};var energy_sources={"municipal":energy_supplied};var demand_ceiling={}
	var happy_energy=compiled.options.get("happiness_energy",{})
	if not happy_energy.is_empty():
		for group in s.groups.values():
			if group.happiness>=happy_energy.threshold:energy_supplied+=float(group.count)*float(happy_energy.per_pop);energy_sources[group.host]=float(energy_sources.get(group.host,0))+float(group.count)*float(happy_energy.per_pop)
	# Fuel and labor constraints on generation are resolved before electricity demand.
	for bid in R.ids(s.projects):
		var p=s.projects[bid];var st=stats[bid]
		if p.special or p.status!="running" or p.paused or st["output.energy"]<=0:continue
		var lr=1. if st.jobs<=0 else minf(1,float(labor.effective[bid])/float(st.jobs))
		var rr=A.supply_ratio(claims.get("run:"+bid,{}).get("needs",{}),reserved.grants.get("run:"+bid,{}))
		var supply=float(st["output.energy"])*float(st.efficiency)*pow(minf(lr,rr),float(config[bid].get("utilization_exponent",st.get("utilization_exponent",1.08))))
		energy_supplied+=supply;energy_sources[bid]=float(energy_sources.get(bid,0))+supply
	for cid in R.ids(recipe_claims):
		var item=recipe_claims[cid];var recipe=item.recipe
		if not recipe.output.has("energy"):continue
		var ratio=A.supply_ratio(claims[cid].needs,reserved.grants[cid]);recipe_energy[cid]=ratio
		var supply=float(recipe.output.energy)*float(recipe.rate)*ratio
		energy_supplied+=supply;energy_sources[item.owner]=float(energy_sources.get(item.owner,0))+supply
	for bid in R.ids(energy_requests):
		var p=s.projects[bid];var st=stats[bid]
		var cid="work:"+bid if p.status=="construction" else "run:"+bid
		var rr=A.supply_ratio(claims.get(cid,{}).get("needs",{}),reserved.grants.get(cid,{}))
		var lr=1. if p.status=="construction" or st.jobs<=0 else minf(1,float(labor.effective[bid])/float(st.jobs))
		demand_ceiling[bid]=minf(lr,rr);energy_requests[bid]*=float(demand_ceiling[bid])
	var energy_need=0.
	for v in energy_requests.values():energy_need+=float(v)
	for v in population_energy.values():energy_need+=float(v)
	var batteries={};var shortage=maxf(0,energy_need-energy_supplied)*h
	for id in R.ids(s.projects):
		var amount=minf(shortage,maxf(0,float(s.projects[id].inventory.get("energy",0))))
		if float(stats[id].get("storage.energy",0))>0 and amount>0:batteries[id]=amount;shortage-=amount;energy_supplied+=amount/h;energy_sources[id]=float(energy_sources.get(id,0))+amount/h
	var energy_ratio=minf(1,energy_supplied/maxf(.000001,energy_need))
	# Energy is now known. Release stock reserved for electrically blocked work.
	# Generator/energy-conversion fuel is not retroactively borrowed by consumers.
	for cid in R.ids(ceilings):
		if cid.begins_with("pop:") or recipe_energy.has(cid):continue
		var owner=claims[cid].owner
		if float(energy_requests.get(owner,0))>0:ceilings[cid]=minf(float(ceilings[cid]),energy_ratio*float(demand_ceiling.get(owner,1.)))
	reserved=A.feasible_resources(s,compiled,claims,access,ceilings)
	# 3. Utilization: shortest fulfilled input, with a documented non-linear penalty.
	for bid in R.ids(s.projects):
		var p=s.projects[bid];var st=stats[bid];var a={"utilization":0.,"labor":1.,"resources":1.,"energy":1.,"product_heat":0.,"outputs":{},"bottleneck":"停运"}
		if p.special:activity[bid]=a;continue
		var cid="work:"+bid if p.status=="construction" else "run:"+bid
		if claims.has(cid):
			a.resources=A.supply_ratio(claims[cid].needs,reserved.grants[cid]);a.energy=energy_ratio*float(demand_ceiling.get(bid,1.)) if energy_requests.get(bid,0)>0 else 1.
			a.labor=1. if p.status=="construction" or st.jobs<=0 else minf(1,float(labor.effective[bid])/float(st.jobs))
			var short=minf(a.labor,minf(a.resources,a.energy));a.utilization=pow(short,float(config[bid].get("utilization_exponent",st.get("utilization_exponent",1.08))))
			if short<.99999:a.utilization=minf(a.utilization,float(st.jit_cap))
			a.bottleneck="劳力/人员效率" if a.labor<=minf(a.resources,a.energy) else ("物资" if a.resources<=a.energy else "能源")
			if short>.999:a.bottleneck="投入齐备"
			# Storage backpressure limits the planned batch, never erases inputs retroactively.
			if p.status=="running":
				for r in RES:
					var expected=float(st["output."+r])*float(st.efficiency)*a.utilization*h
					if expected<=0:continue
					var room=storage_room(s,compiled,bid,r)
					if room<expected:a.utilization*=maxf(.1,room/expected);a.bottleneck="仓储压力（余量低；残余溢出会损耗）"
			actual[cid]={}
			for r in claims[cid].needs:actual[cid][r]=float(claims[cid].needs[r])*float(a.utilization)
		activity[bid]=a
	# Population consumes its own finite receipt, not an imaginary shared satisfaction scalar.
	var people_inputs={}
	for gid in R.ids(s.groups):
		var cid="pop:"+gid;actual[cid]={};people_inputs[gid]={"energy":energy_ratio if population_energy[gid]>0 else 1.}
		for r in claims[cid].needs:
			actual[cid][r]=float(reserved.grants[cid].get(r,0))
			people_inputs[gid][r]=minf(1,float(actual[cid][r])/maxf(1e-15,float(claims[cid].needs[r])))
	for cid in R.ids(recipe_claims):
		var recipe=recipe_claims[cid].recipe;var bid=recipe_claims[cid].owner
		var fraction=recipe_energy.get(cid,minf(A.supply_ratio(claims[cid].needs,reserved.grants[cid]),float(activity[bid].utilization)))
		actual[cid]={}
		for r in claims[cid].needs:actual[cid][r]=float(claims[cid].needs[r])*float(fraction)
	# 4. Commit resource receipts to a private next-state and place actual output.
	var receipt=A.settle_resources(s,reserved,actual);var inventories=receipt.inventories;var before=A.stocks(s);var produced={};var spilled={};var movements=[];var kept_production=0.;var ledger={}
	for bid in R.ids(s.projects):ledger[bid]=[]
	for id in batteries:inventories[id].energy-=batteries[id]
	# Storage capacity changes redistribute old excess, not silently delete it.
	for bid in R.ids(inventories):
		for r in RES:
			var cap=storage_cap(compiled,bid,r);var excess=maxf(0,float(inventories[bid].get(r,0))-cap)
			if excess>0:inventories[bid][r]-=excess;deposit(s,compiled,inventories,bid,r,excess,spilled,movements)
	for bid in R.ids(s.projects):
		var p=s.projects[bid];var st=stats[bid];var a=activity[bid]
		if p.special:continue
		next.projects[bid].usage=a.utilization;next.projects[bid].bottleneck=a.bottleneck;next.projects[bid].outputs_last={};next.projects[bid].effective_hours+=a.utilization*h
		if p.status=="construction":
			var work=0.*a.utilization
			next.projects[bid].job.work=maxf(0,float(p.job.work)-work)
			next.projects[bid].job.material=maxf(0,float(p.job.material)-float(actual.get("work:"+bid,{}).get("material",0)))
		elif p.status=="running" and not p.paused:
			for r in RES:
				var raw=maxf(0,float(st["output."+r])*float(st.efficiency)*float(a.utilization))
				var heat=raw*float(config[bid].get("redirect_product_heat",0));a.product_heat+=heat
				var amount=(raw-heat)*h
				produced[r]=float(produced.get(r,0))+amount
				a.outputs[r]=amount/h;next.projects[bid].outputs_last[r]=amount/h
				var kept=deposit(s,compiled,inventories,bid,r,amount,spilled,movements);kept_production+=kept
				ledger[bid].append({"kind":"output","resource":r,"rate":amount/h,"kept":kept/h,"lost":(amount-kept)/h,"source":"基础生产与产量方案"})
	for cid in R.ids(recipe_claims):
		var recipe=recipe_claims[cid].recipe;var bid=recipe_claims[cid].owner
		var fraction=A.supply_ratio(claims[cid].needs,actual[cid])
		for r in recipe.output:
			if r=="energy":continue
			var n=float(recipe.output[r])*float(recipe.rate)*fraction*h
			produced[r]=float(produced.get(r,0))+n
			var kept=deposit(s,compiled,inventories,bid,r,n,spilled,movements);kept_production+=kept
			next.projects[bid].outputs_last[r]=float(next.projects[bid].outputs_last.get(r,0))+n/h
			activity[bid].outputs[r]=float(activity[bid].outputs.get(r,0))+n/h
			ledger[bid].append({"kind":"output","resource":r,"rate":n/h,"kept":kept/h,"lost":(n-kept)/h,"source":str(recipe.source)})
	for bid in energy_sources:
		var supply=float(energy_sources[bid])
		next.projects[bid].outputs_last["energy"]=supply
		ledger[bid].append({"kind":"output","resource":"energy","rate":supply,"kept":supply,"lost":0.,"source":"本期发电 / 余料转换 / 居民贡献"})
	# Surplus energy can enter stock ONLY through explicitly declared storage capacity.
	var extra=maxf(0,energy_supplied-energy_need)*h
	for bid in R.ids(s.projects):
		var room=maxf(0,float(stats[bid].get("storage.energy",0))-float(inventories[bid].get("energy",0)))
		var n=minf(room,extra)
		if n>0:inventories[bid].energy=float(inventories[bid].get("energy",0))+n;extra-=n
	# 5. Actual emissions, quota-conserving conversions, finite services, then group state.
	var env=F.environment(g,compiled,activity,h);var recycled=0.;var heat_used=0.;var heat_receipts=[]
	for bid in R.ids(s.projects):
		var cfg=config[bid];var p=s.projects[bid]
		if not cfg.has("field_conversion") or p.status!="running" or p.paused:continue
		var cv=cfg.field_conversion;var request=float(cv.amount)*float(cv.rate)*h*float(activity[bid].utilization)
		var n=F.consume_local_pollution(env,p.tile,request,h);recycled+=n
		for r in cv.output:
			var result=n/float(cv.amount)*float(cv.output[r]);produced[r]=float(produced.get(r,0))+result
			var kept=deposit(s,compiled,inventories,bid,r,result,spilled,movements);kept_production+=kept
			next.projects[bid].outputs_last[r]=float(next.projects[bid].outputs_last.get(r,0))+result/h
			activity[bid].outputs[r]=float(activity[bid].outputs.get(r,0))+result/h
			ledger[bid].append({"kind":"output","resource":r,"rate":result/h,"kept":kept/h,"lost":(result-kept)/h,"source":"污染回收"})
		activity[bid]["recycled"]=n
	for bid in R.ids(s.projects):
		next.projects[bid].next_heat_bonus=0.;next.projects[bid].next_heat_credit=0.
		var p=s.projects[bid];var st=stats[bid]
		if p.special or p.status!="running" or p.paused:continue
		var desired=0.
		if st.heat_service>0:desired=float(st.heat_cap)/float(st.heat_service)*h
		if st.heat_discount>0:desired+=float(st.energy_demand)*float(st.heat_discount)*h
		if desired<=0:continue
		var allocation=F.reserve_heat(env,p.tile,desired);heat_used+=allocation.amount
		if st.heat_service>0:next.projects[bid].next_heat_bonus=minf(float(st.heat_cap),float(allocation.amount)/h*float(st.heat_service))
		else:next.projects[bid].next_heat_credit=minf(float(st.energy_demand)*float(st.heat_discount),float(allocation.amount)/h)
		if allocation.amount>0:heat_receipts.append({"target":bid,"amount":allocation.amount,"sources":allocation.sources})
	var service=A.services(g,compiled,access,activity)
	Pop.update(g,next,compiled,people_inputs,service,env,labor,h)
	# 6. Actual activity becomes fiscal tax bases. Structural changes only now commit.
	var fiscal=[]
	var tax=0.;var taxmode=str(compiled.options.get("tax_mode","head"));var trade=0.;var production=0.;var people=0.;var maintenance=0.
	for group in s.groups.values():
		people+=float(group.count)
		var cost=float(group.count)*float(stats[group.host].population_maintenance)
		maintenance+=cost*h;fiscal.append({"kind":"expense","source_id":group.host,"name":"居民基础维护","rate":cost})
	for bid in R.ids(s.projects):
		var p=s.projects[bid]
		if p.special:continue
		maintenance+=maxf(0,float(stats[bid].maintenance))*h
		fiscal.append({"kind":"expense","source_id":bid,"name":"建筑固定维护","rate":maxf(0,float(stats[bid].maintenance))})
		trade+=float(service.delivered.get(bid+"|commercial",0))*float(stats[bid].get("commercial_tax_multiplier",1))*h
		trade+=float(service.delivered.get(bid+"|medical",0))*float(config[bid].get("medical_activity",0))*h
	production=kept_production
	match taxmode:
		"head":tax=people*float(g.c.balance.get("head_tax",.85))*h
		"production":tax=production*float(g.c.balance.get("production_tax",1.35))
		"trade":tax=trade*float(g.c.balance.get("trade_tax",.85))
	var rent=float(compiled.options.get("land_rent",0))*g.playable_ids().size()*h
	var pollution_tax=env.pollution_produced*float(compiled.options.get("pollution_fee",0))
	fiscal.append({"kind":"income","source_id":"municipal","name":{"head":"人头税","production":"有效生产税","trade":"消费税"}.get(taxmode,"税收"),"rate":tax/h})
	if rent>0:fiscal.append({"kind":"income","source_id":"municipal","name":"用地收入","rate":rent/h})
	if pollution_tax>0:fiscal.append({"kind":"income","source_id":"municipal","name":"计源排污收入","rate":pollution_tax/h})
	next.currency=float(next.currency)+tax+rent+pollution_tax-maintenance
	for id in R.ids(inventories):
		next.projects[id].inventory=inventories[id]
		for r in next.projects[id].get("retained_storage",{}):next.projects[id].retained_storage[r]=minf(float(next.projects[id].retained_storage[r]),float(inventories[id].get(r,0)))
	for tile in R.ids(next.cells):
		next.cells[tile].fields=access.tiles[tile].duplicate(true)
		for k in env.tiles[tile]:next.cells[tile].fields[k]=env.tiles[tile][k].duplicate(true)
	next.fields=access.fields.duplicate(true);next.fields.merge(env.fields)
	next.local_hours=float(next.local_hours)+h;next.simulation_tick=int(next.simulation_tick)+1
	for group in next.groups.values():
		if group.host!="homeless" and group.health>50 and group.happiness>40:group.count*=exp(float(g.c.balance.birth_per_hour)*h)
	var flows={}
	for r in RES:flows[r]=(total(inventories,r)-total(before,r))/h
	var audit={"resources":{},"pollution_generated":env.pollution_produced,"pollution_recycled":recycled,"heat_generated":env.heat_produced,"heat_used":heat_used,"hours":h,"phase_order":["空间","分配","运行","生产仓储","场与人群","财政与结构"]}
	audit.population_food_requested=0.0
	audit.commercial_service_people=0.0
	audit.population_counts={}
	for gid in R.ids(s.groups):
		audit.population_food_requested+=float(claims.get("pop:"+gid,{}).get("needs",{}).get("food",0))
		audit.commercial_service_people+=float(service.covered.get(gid,{}).get("commercial",0))
		audit.population_counts[gid]=float(s.groups[gid].count)
	audit.population_food_demand_rate=float(audit.population_food_requested)/h
	for r in RES:
		var a0=total(before,r);var a1=total(inventories,r);var output=float(produced.get(r,0));var input=float(receipt.consumed.get(r,0));var loss=float(spilled.get(r,0))
		audit.resources[r]={"before":a0,"produced":output,"consumed":input,"lost":loss,"after":a1,"error":a0+output-input-loss-a1}
		if absf(audit.resources[r].error)>.00001:return "资源守恒失败："+r
	if recycled>env.pollution_produced+.00001 or heat_used>env.heat_produced+.00001:return "Field额度重复结算。"
	next.audit=audit
	next.metrics={"flows":flows,"income":(tax+rent+pollution_tax)/h,"upkeep":maintenance/h,"cashflow":(tax+rent+pollution_tax-maintenance)/h,"energy":energy_supplied,"energy_need":energy_need,
		"service_links":service.links,"exposure_links":[],"employment_links":labor.links,"allocation":receipt.receipts,"stock_movements":movements,"spilled":spilled,"utilization":activity,
		"service_pools":service.pools,"service_covered":service.covered,"pollution_recycled":recycled/h,"heat_used":heat_used/h,"heat_receipts":heat_receipts,
		"activity_ledger":ledger,"fiscal_ledger":fiscal,"population_energy":population_energy,"energy_sources":energy_sources,"energy_requests":energy_requests,"requested":{},"energy_ratio":energy_ratio,"reconcile_rounds":reserved.get("reconcile_rounds",0),
		"tax_basis":{"mode":taxmode,"people":people,"production":production/h,"trade":trade/h,"pollution_fee":pollution_tax/h,"land_rent":rent/h}}
	for cid in R.ids(claims):
		for r in claims[cid].needs:next.metrics.requested[r]=float(next.metrics.requested.get(r,0))+float(claims[cid].needs[r])/h
	for used in receipt.receipts:
		var owner=claims[used.consumer].owner
		ledger[owner].append({"kind":"input","resource":used.resource,"rate":used.used/h,"source":g.project_name(used.source),"source_id":used.source,"consumer":used.consumer})
	for bid in energy_requests:
		if float(energy_requests[bid])>0:ledger[bid].append({"kind":"input","resource":"energy","rate":float(energy_requests[bid])*energy_ratio,"source":"地区供电（见账本）"})
	for gid in population_energy:
		if float(population_energy[gid])>0:ledger[s.groups[gid].host].append({"kind":"input","resource":"energy","rate":float(population_energy[gid])*energy_ratio,"source":"住户用电"})
	for gid in R.ids(next.groups):
		var home=next.groups[gid].host
		for f in env.tiles.get(next.projects[home].tile,{}).get("pollution",[]):
			next.metrics.exposure_links.append({"home":home,"source":f.source_id,"pollution":f.value,"noise":0.})
	var happiness=0.
	for group in next.groups.values():happiness+=float(group.count)*float(group.happiness)
	happiness/=maxf(1,people)
	next.support=clampf(lerpf(float(s.support),happiness,1-exp(-h*.15)),0,100)
	if produced.get("goods",0)>0:next.goods_available=true
	if float(next.get("goods_requested_at",-1))>=0 and float(next.local_hours)+.0000001>=float(next.goods_requested_at)+float(g.c.balance.goods_grace_hours):next.goods_active=true
	g.recording_cycle=true
	g.s=next;g.compiled=compiled
	g.Construction.tick(g,h)
	# Refresh once after the committed fields and possible construction changes.
	# Trade mutates stock/cash; migration/rehouse mutates groups. Neither changes
	# the building/field/slot inputs of Rules.compile, so reuse it below.
	g.refresh_metrics()
	g.Market.tick(g,h,float(context.get("external_seconds",h*float(g.c.beta.units.earth_seconds_per_month)/g.Clock.acceleration(g))))
	var arrival_interval=float(g.c.beta.economy_candidates.arrival_interval_months)
	while not g.preview_mode and float(g.s.local_hours)+1e-10>=(int(g.s.event_cursor)+1)*arrival_interval:
		g.s.event_cursor+=1
		if not g.s.migration_paused:
			var count=float(g.c.beta.economy_candidates.arrival_people)
			Pop.create(g,"homeless",count);arrived+=count
			g.log_event("arrival",{"count":count},"%.0f名新居民抵达，优先填入空余住房。"%count)
	if not g.preview_mode:Pop.rehouse(g)
	var final_metrics:Dictionary=g.s.metrics.duplicate(true)
	final_metrics.merge(Pop.summary(g),true)
	g.s.metrics=final_metrics
	if not g.preview_mode:
		g.recording_cycle=false
		g.Civic.cycle(g,h,previous_population,arrived)
		Progress.after_commit(g,h)
		g.Civic.sync(g);g.Civic.finalize(g)
	g.recording_cycle=false
	return g.validate_state(g.s)
static func storage_cap(c:Dictionary,id:String,r:String) -> float:
	var cap=maxf(0,float(c.stats[id].get("storage."+r,0)))
	if id=="municipal":cap*=float(c.options.get("municipal_storage",1))
	return maxf(cap,float(c.stats[id].get("retained_storage."+r,0)))
static func storage_room(s:Dictionary,c:Dictionary,producer:String,r:String) -> float:
	var capacity=0.
	for id in R.ids(s.projects):
		var p=s.projects[id]
		if id!=producer and (p.status!="running" or p.paused):continue
		if id!=producer and not (id=="municipal" or "building.logistics" in p.traits or c.config[id].get("overflow_relay",false)):continue
		capacity+=maxf(0,storage_cap(c,id,r)-float(p.inventory.get(r,0)))
	return capacity
static func deposit(s:Dictionary,c:Dictionary,inventories:Dictionary,producer:String,r:String,amount:float,spilled:Dictionary,movements:Array) -> float:
	if amount<=0:return 0.
	var targets=[producer];var secondary=[]
	for id in R.ids(s.projects):
		if id==producer:continue
		var p=s.projects[id]
		if p.status!="running" or p.paused:continue
		var priority=99
		if c.config[id].get("overflow_relay",false) and R.near(s,producer,id)==1:priority=0
		elif "building.logistics" in p.traits:priority=1
		elif id=="municipal":priority=2
		if priority<99:secondary.append({"id":id,"priority":priority})
	secondary.sort_custom(func(a,b):return a.priority<b.priority if a.priority!=b.priority else a.id<b.id)
	for x in secondary:targets.append(x.id)
	var left=amount
	for id in targets:
		var room=maxf(0,storage_cap(c,id,r)-float(inventories[id].get(r,0)));var n=minf(room,left)
		if n<=0:continue
		inventories[id][r]=float(inventories[id].get(r,0))+n;left-=n
		if id!=producer:movements.append({"source":producer,"target":id,"resource":r,"amount":n})
		if left<.000001:break
	if left>0:spilled[r]=float(spilled.get(r,0))+left

	return amount-left
