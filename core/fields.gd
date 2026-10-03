extends RefCounted
## Source-aware Field entities; Tile holds derived samples, never the authority.
const R=preload("res://core/rules.gd")
const Hex=preload("res://core/hex.gd")
static func empty_tiles(s:Dictionary) -> Dictionary:
	var tiles={}
	for id in R.ids(s.cells):tiles[id]={}
	return tiles
static func sample(tiles:Dictionary,tile:String,kind:String,id:String,source:String,value:float,via:String="") -> void:
	if not tiles.has(tile):return
	if not tiles[tile].has(kind):tiles[tile][kind]=[]
	tiles[tile][kind].append({"field_id":id,"source_id":source,"value":value,"via":via})
static func make_field(source:String,kind:String,strength:float,radius:int,decay:float,rule:String="base") -> Dictionary:
	return {"id":"F:"+source+":"+kind+":"+rule,"type":kind,"source_id":source,"strength":strength,"propagation":{"radius":radius,"decay":decay,"rule":rule},"active":true}
static func access_strength(distance:int,radius:int,decay:float) -> float:
	if distance>radius:return 0
	return maxf(0,1.0-maxi(0,distance-1)*decay)
static func road_component(s:Dictionary,start:String) -> Array:
	var result=[];var todo=[start]
	while not todo.is_empty():
		var id=todo.pop_front()
		if id in result or not s.projects.has(id):continue
		var p=s.projects[id]
		if not "building.road" in p.traits or p.paused or p.status!="running":continue
		result.append(id)
		for other in R.ids(s.projects):
			if not other in result and R.near(s,id,other)==1:todo.append(other)
	return result
static func source_value(tiles:Dictionary,tile:String,kind:String,source:String) -> float:
	var best=0.0
	for x in tiles.get(tile,{}).get(kind,[]):
		if x.source_id==source:best=maxf(best,float(x.value))
	return best
static func access(g,compiled:Dictionary) -> Dictionary:
	var s=g.s;var tiles=empty_tiles(s);var entities={};var st=compiled.stats
	for id in R.ids(s.projects):
		var p=s.projects[id]
		if p.get("special",false) or p.status!="running" or p.paused:continue
		var kinds=[]
		if st[id].jobs>0:kinds.append("employment")
		for k in g.c.balance.service_kinds:
			if st[id]["service."+k]>0:kinds.append(k)
		if compiled.config[id].has("volunteer_education"):
			for group in s.groups.values():
				if group.host==id and group.education>=compiled.config[id].volunteer_education.threshold and not "education" in kinds:kinds.append("education")
		for kind in kinds:
			var rad=int(st[id].employment_range if kind=="employment" else st[id].service_range)
			var decay=float(st[id].employment_decay if kind=="employment" else st[id].service_decay)
			var component=road_component(s,id) if compiled.options.get("access_network",false) else []
			var f=make_field(id,kind,1,rad,decay);entities[f.id]=f
			for key in R.ids(s.cells):
				var dist=Hex.distance(p.tile,key)
				var value=access_strength(dist,rad,decay)
				for other in component:
					var network_distance=Hex.distance(s.projects[other].tile,key)
					value=maxf(value,access_strength(network_distance,rad,decay*.6)*.9)
				if value>0:sample(tiles,key,kind,f.id,id,value)
		for e in compiled.emitters[id]:
			var f=make_field(id,e.type,float(e.strength),int(e.range),float(e.decay),e.uid);entities[f.id]=f
			for key in R.ids(s.cells):
				var v=access_strength(Hex.distance(p.tile,key),int(e.range),float(e.decay))*float(e.strength)
				if v!=0:sample(tiles,key,e.type,f.id,id,v)
	# Non-building sources (a Tile, PopulationGroup, or an explicitly located entity)
	# use the same Field definition/projection mechanism.
	for fid in R.ids(s.get("authored_fields",{})):
		var f=s.authored_fields[fid]
		if not f.active:continue
		var origin=str(f.get("origin",""));var source=str(f.source_id)
		if origin=="":
			if s.cells.has(source):origin=source
			elif s.projects.has(source):origin=s.projects[source].tile
			elif s.groups.has(source):origin=s.projects[s.groups[source].host].tile
		if not s.cells.has(origin):continue
		entities[fid]=f.duplicate(true)
		for tile in R.ids(s.cells):
			var value=float(f.strength)*access_strength(Hex.distance(origin,tile),int(f.propagation.radius),float(f.propagation.decay))
			if value!=0:sample(tiles,tile,f.type,fid,source,value)
	# One-hop relay reads the original access snapshot. The provider ID never changes.
	var original=tiles.duplicate(true)
	for id in R.ids(s.projects):
		var p=s.projects[id];var relay=float(compiled.config[id].get("service_relay",0))
		if relay<=0 or p.status!="running" or p.paused:continue
		for kind in g.c.balance.service_kinds:
			for x in original.get(p.tile,{}).get(kind,[]):
				if x.source_id==id or R.near(s,id,x.source_id)!=1:continue
				for key in R.ids(s.cells):
					if Hex.distance(p.tile,key)<=1:sample(tiles,key,kind,x.field_id,x.source_id,float(x.value)*relay,id)
	return {"fields":entities,"tiles":tiles}
static func environment(g,compiled:Dictionary,activity:Dictionary,h:float) -> Dictionary:
	var tiles=empty_tiles(g.s);var fields={};var budgets={};var pollution_produced=0.;var heat_produced=0.
	for id in R.ids(g.s.projects):
		var p=g.s.projects[id]
		if p.get("special",false) or p.status!="running" or p.paused:continue
		var a=activity.get(id,{});var st=compiled.stats[id];var cfg=compiled.config[id]
		var u=float(a.get("utilization",0))
		var pollution=float(st.pollution)*u
		var redirected=pollution*float(cfg.get("redirect_pollution_heat",0));pollution-=redirected
		var heat=float(a.get("product_heat",0))+redirected
		pollution_produced+=maxf(0,pollution)*h;heat_produced+=maxf(0,heat)*h
		for kind in ["pollution","heat"]:
			var strength=pollution if kind=="pollution" else heat
			if absf(strength)<.000001:continue
			var radius=int(st.pollution_range) if kind=="pollution" else 2
			var decay=float(st.pollution_decay) if kind=="pollution" else .5
			var f=make_field(id,kind,strength,radius,decay)
			fields[f.id]=f;var weights={};var sum=0.
			var component=road_component(g.s,id) if kind=="heat" and compiled.options.get("heat_network",false) else []
			for tile in R.ids(g.s.cells):
				var dist=Hex.distance(p.tile,tile);var w=0.
				if kind=="pollution" and dist<=radius:
					var d=decay*float(cfg.get("water_pollution_decay",1)) if "tile.waterfront" in g.s.cells[tile].traits else decay
					w=pow(maxf(.001,1-d),dist)
				elif kind=="heat":
					w=access_strength(dist,radius,decay)
					for other in component:
						w=maxf(w,.8*access_strength(Hex.distance(g.s.projects[other].tile,tile),2,.5))
				if w>0:weights[tile]=w;sum+=w
			var local={}
			for tile in weights:
				# Pollution mass is partitioned; heat values express reach into ONE finite source pool.
				var value=strength*float(weights[tile])/maxf(.001,sum) if kind=="pollution" else strength*float(weights[tile])
				sample(tiles,tile,kind,f.id,id,value)
				local[tile]=value*h
			budgets[f.id]={"type":kind,"source_id":id,"total":maxf(0,strength*h),"remaining":maxf(0,strength*h),"local":local}
	# Authored Tile/Group/Organization sources use the same environment resolver.
	# Negative values remain signed influences; only positive emitted quantity is reclaimable.
	for fid in R.ids(g.s.get("authored_fields",{})):
		var f=g.s.authored_fields[fid]
		if not f.active or not f.type in ["pollution","heat"]:continue
		var origin=str(f.get("origin",""));var source=str(f.source_id)
		if origin=="":
			if g.s.cells.has(source):origin=source
			elif g.s.projects.has(source):origin=g.s.projects[source].tile
			elif g.s.groups.has(source):origin=g.s.projects[g.s.groups[source].host].tile
		if not g.s.cells.has(origin):continue
		var weights={};var weight_sum=0.;var local={};var strength=float(f.strength)
		for tile in R.ids(g.s.cells):
			var w=access_strength(Hex.distance(origin,tile),int(f.propagation.radius),float(f.propagation.decay))
			if w>0:weights[tile]=w;weight_sum+=w
		fields[fid]=f.duplicate(true)
		for tile in weights:
			var contribution=strength*float(weights[tile])
			if f.type=="pollution":contribution/=maxf(.000001,weight_sum)
			sample(tiles,tile,f.type,fid,source,contribution);local[tile]=maxf(0,contribution*h)
		budgets[fid]={"type":f.type,"source_id":source,"total":maxf(0,strength*h),"remaining":maxf(0,strength*h),"local":local}
		if f.type=="pollution":pollution_produced+=maxf(0,strength)*h
		else:heat_produced+=maxf(0,strength)*h
	return {"fields":fields,"tiles":tiles,"budgets":budgets,"pollution_produced":pollution_produced,"heat_produced":heat_produced,"hours":h}
static func consume_local_pollution(env:Dictionary,tile:String,amount:float,h:float) -> float:
	var taken=0.
	for fid in R.ids(env.budgets):
		var pool=env.budgets[fid]
		if pool.type!="pollution":continue
		var n=minf(amount-taken,minf(pool.remaining,maxf(0,float(pool.local.get(tile,0)))))
		if n<=0:continue
		pool.remaining-=n;pool.local[tile]-=n;taken+=n
		for entry in env.tiles.get(tile,{}).get("pollution",[]):
			if entry.field_id==fid:entry.value-=n/maxf(.000001,h)
		if taken>=amount-.000001:break
	return taken
static func reserve_heat(env:Dictionary,tile:String,requested:float) -> Dictionary:
	var got=0.;var sources=[];var options=[]
	for contribution in env.tiles.get(tile,{}).get("heat",[]):
		options.append(contribution)
	options.sort_custom(func(a,b):return a.value>b.value if a.value!=b.value else a.field_id<b.field_id)
	for e in options:
		var pool=env.budgets[e.field_id];var n=minf(requested-got,minf(pool.remaining,maxf(0,float(pool.local.get(tile,0)))))
		if n<=0:continue
		pool.remaining-=n;pool.local[tile]-=n;got+=n;sources.append({"source":pool.source_id,"amount":n})
		if got>=requested-.000001:break
	return {"amount":got,"sources":sources}
