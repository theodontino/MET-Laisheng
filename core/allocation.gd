extends RefCounted
## Collect -> deterministic resolve -> usage receipts. No shared-world writes.
const R=preload("res://core/rules.gd")
const F=preload("res://core/fields.gd")
const Hex=preload("res://core/hex.gd")
static func labor(s:Dictionary,c:Dictionary,access:Dictionary) -> Dictionary:
	var people_left={};var jobs_left={};var jobs={};var effective={};var personnel={};var links=[];var offers=[]
	for gid in R.ids(s.groups):
		var group=s.groups[gid];people_left[gid]=float(group.count);jobs[gid]={}
		var host=group.host;var cf=c.config.get(host,{});var pe=float(group.efficiency)
		if cf.has("volunteer_education") and float(group.education)>=float(cf.volunteer_education.threshold):pe*=float(cf.volunteer_education.labor_mult)
		personnel[gid]=pe
	for id in R.ids(s.projects):
		var p=s.projects[id];var st=c.stats[id];jobs_left[id]=maxf(0,float(st.jobs));effective[id]=0.
		if p.status!="running" or p.paused or p.get("special",false):jobs_left[id]=0;continue
		for gid in R.ids(s.groups):
			var group=s.groups[gid];var home=s.projects[group.host]
			var reachable=.05 if home.get("special",false) else F.source_value(access.tiles,home.tile,"employment",id)
			var hc=c.config[group.host]
			if hc.get("local_employment",false) and home.id==id:reachable+=2
			if hc.get("neighbor_employment",false) and R.near(s,home.id,id)==1:reachable+=.25
			if hc.get("family_employment",false) and home.operator_org==p.operator_org and "building.industrial" in p.traits and reachable>0:reachable+=.4
			if reachable<=0:continue
			var sort=personnel[gid] if c.options.get("labor_mode","access")=="merit" else reachable
			offers.append({"group":gid,"building":id,"score":sort,"access":reachable,"existing":float(group.employment.get(id,0))>0,"priority":int(p.get("work_priority",0))})
	offers.sort_custom(func(a,b):
		if a.priority!=b.priority:return a.priority>b.priority
		if a.score!=b.score:return a.score>b.score
		if a.access!=b.access:return a.access>b.access
		if a.existing!=b.existing:return a.existing
		if a.group!=b.group:return a.group<b.group
		return a.building<b.building)
	for offer in offers:
		var gid=offer.group;var id=offer.building
		var n=minf(people_left[gid],jobs_left[id])
		if n<=.000001:continue
		people_left[gid]-=n;jobs_left[id]-=n;jobs[gid][id]=n
		effective[id]+=n*personnel[gid]*float(c.stats[id].personnel_mult)
		links.append({"group":gid,"source":gid,"target":id,"people":n,"access":offer.access,"effective":n*personnel[gid]*float(c.stats[id].personnel_mult)})
	return {"employment":jobs,"effective":effective,"links":links,"unemployed":people_left}
static func stocks(s:Dictionary) -> Dictionary:
	var result={}
	for id in R.ids(s.projects):result[id]=s.projects[id].inventory.duplicate(true)
	return result
static func resources(s:Dictionary,compiled:Dictionary,claims:Dictionary,access:Dictionary) -> Dictionary:
	var remaining=stocks(s);var grants={};var transfers=[];var pairs=[];var source_used={}
	for cid in R.ids(claims):
		grants[cid]={};var claim=claims[cid];var owner=claim.owner;var target=s.projects[owner]
		var st=compiled.stats[owner];var conf=compiled.config[owner]
		for resource in R.ids(claim.needs):
			grants[cid][resource]=0.;source_used[cid+"|"+resource]={}
			for sid in R.ids(s.projects):
				var source=s.projects[sid]
				if float(source.inventory.get(resource,0))<=0:continue
				var distance=100. if source.get("special",false) or target.get("special",false) else float(Hex.distance(source.tile,target.tile))
				if sid==owner:distance=0.
				distance*=float(st.distance_cost)
				if resource=="material":distance*=float(conf.get("material_distance",1))
				if not access.tiles.get(target.tile,{}).get("drone",[]).is_empty():distance*=.4
				var same_rank=0
				if conf.get("same_supplier",false):same_rank=0 if source.operator_org==target.operator_org else 1
				var score=float(st.allocation_priority)+float(claim.get("priority",0))
				var group_distance=distance
				if conf.get("group_procurement",false):
					for other in R.ids(claims):
						var op=s.projects[claims[other].owner]
						if op.operator_org==target.operator_org and float(claims[other].needs.get(resource,0))>0 and not op.get("special",false) and not source.get("special",false):group_distance=minf(group_distance,Hex.distance(source.tile,op.tile))
				pairs.append({"consumer":cid,"source":sid,"resource":resource,"priority":score,"same_rank":same_rank,"group_distance":group_distance,"distance":distance})
	pairs.sort_custom(func(a,b):
		if a.resource!=b.resource:return str(a.resource)<str(b.resource)
		if a.priority!=b.priority:return a.priority>b.priority
		if a.same_rank!=b.same_rank:return a.same_rank<b.same_rank
		if a.group_distance!=b.group_distance:return a.group_distance<b.group_distance
		if a.distance!=b.distance:return a.distance<b.distance
		if a.source!=b.source:return a.source<b.source
		return a.consumer<b.consumer)
	var processed_groups={}
	for pair in pairs:
		var original=pair.consumer;var sid=pair.source;var r=pair.resource
		var owner=claims[original].owner;var group=compiled.config[owner].get("group_procurement",false)
		var consumers=[original]
		if group:
			var org=s.projects[owner].operator_org;var group_key=org+"|"+sid+"|"+r
			if processed_groups.has(group_key):continue
			processed_groups[group_key]=true;var members=[]
			for cid in R.ids(claims):
				var host=claims[cid].owner
				if s.projects[host].operator_org!=org or not compiled.config[host].get("group_procurement",false) or not claims[cid].needs.has(r):continue
				var distance=100. if s.projects[host].special or s.projects[sid].special else float(Hex.distance(s.projects[sid].tile,s.projects[host].tile))
				members.append({"id":cid,"distance":distance,"priority":float(compiled.stats[host].allocation_priority)})
			members.sort_custom(func(a,b):
				if a.priority!=b.priority:return a.priority>b.priority
				if a.distance!=b.distance:return a.distance<b.distance
				return a.id<b.id)
			consumers=[]
			for x in members:consumers.append(x.id)
		# A procurement group has obtained its turn; allocate the finite source
		# among its members before the next external buyer, retaining every receipt.
		for cid in consumers:
			var need=maxf(0,float(claims[cid].needs[r])-float(grants[cid][r]));var available=float(remaining[sid].get(r,0))
			var amount=minf(need,available)
			if compiled.options.get("procure_mode","near")=="split":amount=minf(amount,float(claims[cid].needs[r])*.6)
			if amount<=0:continue
			grants[cid][r]+=amount;remaining[sid][r]-=amount
			transfers.append({"consumer":cid,"source":sid,"resource":r,"reserved":amount,"distance":pair.distance,"priority":pair.priority,"procurement_group":s.projects[owner].operator_org if group else ""})

	return {"grants":grants,"transfers":transfers,"remaining":remaining}
static func supply_ratio(needs:Dictionary,grants:Dictionary) -> float:
	var result=1.
	for r in needs:
		if float(needs[r])>0:result=minf(result,float(grants.get(r,0))/float(needs[r]))
	return clampf(result,0,1)
static func settle_resources(s:Dictionary,allocation:Dictionary,actual:Dictionary) -> Dictionary:
	var inventory=stocks(s);var consumed={};var receipts=[]
	var left=actual.duplicate(true)
	for transfer in allocation.transfers:
		var cid=transfer.consumer;var r=transfer.resource;var sid=transfer.source
		var n=minf(float(left.get(cid,{}).get(r,0)),float(transfer.reserved))
		if n<=0:continue
		n=minf(n,float(inventory[sid].get(r,0)))
		inventory[sid][r]-=n;left[cid][r]-=n;consumed[r]=float(consumed.get(r,0))+n
		var receipt=transfer.duplicate();receipt.used=n;receipts.append(receipt)
	return {"inventories":inventory,"consumed":consumed,"receipts":receipts}
static func services(g,compiled:Dictionary,access:Dictionary,usage:Dictionary) -> Dictionary:
	var s=g.s;var pools={};var source_pool={};var capacities={};var covered={};var allocations={};var pairs=[]
	for gid in R.ids(s.groups):covered[gid]={};allocations[gid]={}
	for kind in g.c.balance.service_kinds:
		var todo=[]
		for id in R.ids(s.projects):
			var p=s.projects[id];var capacity=float(compiled.stats[id]["service."+kind])*float(usage.get(id,{}).get("utilization",0))*float(compiled.stats[id].efficiency)
			var cfg=compiled.config[id]
			if kind=="education" and cfg.has("volunteer_education"):
				for group in s.groups.values():
					if group.host==id and group.education>=cfg.volunteer_education.threshold:capacity+=float(group.count)*float(cfg.volunteer_education.capacity_per_pop)
			if p.status=="running" and not p.paused and capacity>0:capacities[id+"|"+kind]=capacity;todo.append(id)
		while not todo.is_empty():
			var root=todo.pop_front();var members=[root];var frontier=[root]
			if compiled.options.get("service_pool",false):
				while not frontier.is_empty():
					var x=frontier.pop_front()
					for y in todo.duplicate():
						if R.near(s,x,y)==1:members.append(y);frontier.append(y);todo.erase(y)
			var pid=root+"|"+kind;var total=0.
			for source in members:source_pool[source+"|"+kind]=pid;total+=capacities[source+"|"+kind]
			pools[pid]={"kind":kind,"members":members,"capacity":total,"remaining":total}
			for gid in R.ids(s.groups):
				var group=s.groups[gid];var home=s.projects[group.host];var best=0.;var source=root
				for member in members:
					var value=F.source_value(access.tiles,home.tile,kind,member)
					if value>0 and compiled.config[member].get("adjacent_service_priority",false) and R.near(s,member,home.id)<=1:value+=.1
					if compiled.options.get("service_pool",false) and members.size()>1:
						if R.near(s,member,home.id)<=1:value*=1.25
						else:value=maxf(0,value-.15)
					if value>best:best=value;source=member
				if best>0:pairs.append({"group":gid,"pool":pid,"kind":kind,"source":source,"access":best})
	pairs.sort_custom(func(a,b):
		if a.access!=b.access:return a.access>b.access
		if a.pool!=b.pool:return a.pool<b.pool
		return a.group<b.group)
	var links=[];var delivered={}
	for pair in pairs:
		var gid=pair.group;var kind=pair.kind;var pool=pools[pair.pool]
		var remaining=maxf(0,float(s.groups[gid].count)-float(covered[gid].get(kind,0)))
		var n=minf(remaining,float(pool.remaining))
		if n<=0:continue
		pool.remaining-=n;covered[gid][kind]=float(covered[gid].get(kind,0))+n
		if not allocations[gid].has(kind):allocations[gid][kind]={}
		allocations[gid][kind][pair.pool]=n
		# Charge delivered activity proportionally to finite actual provider capacities.
		for member in pool.members:
			var fraction=float(capacities[member+"|"+kind])/float(pool.capacity)
			delivered[member+"|"+kind]=float(delivered.get(member+"|"+kind,0))+n*fraction
		links.append({"source":pair.source,"home":s.groups[gid].host,"group":gid,"kind":kind,"people":n,"access":pair.access,"pool":pair.pool})
	return {"covered":covered,"allocations":allocations,"links":links,"pools":pools,"delivered":delivered}

static func feasible_resources(s:Dictionary,compiled:Dictionary,claims:Dictionary,access:Dictionary,ceilings:Dictionary) -> Dictionary:
	var bounded=claims.duplicate(true)
	for cid in R.ids(bounded):
		for resource in bounded[cid].needs:
			bounded[cid].needs[resource]=float(claims[cid].needs[resource])*float(ceilings.get(cid,1.))
	var allocation={}
	var rounds=0
	for iteration in 8:
		rounds=iteration+1
		allocation=resources(s,compiled,bounded,access)
		var changed=false
		for cid in R.ids(bounded):
			# Lower only quantities blocked by a DIFFERENT required ingredient.
			# A one-resource consumer can claim the released stock in the next pass.
			for resource in R.ids(bounded[cid].needs):
				var limit=float(ceilings.get(cid,1.))
				for other in claims[cid].needs:
					if other==resource or float(claims[cid].needs[other])<=0:continue
					limit=minf(limit,float(allocation.grants[cid].get(other,0))/float(claims[cid].needs[other]))
				var reduced=minf(float(bounded[cid].needs[resource]),float(claims[cid].needs[resource])*limit)
				if reduced<float(bounded[cid].needs[resource])-.0000001:
					bounded[cid].needs[resource]=reduced;changed=true
		if not changed:break
	allocation=resources(s,compiled,bounded,access)
	allocation.reconcile_rounds=rounds
	return allocation
