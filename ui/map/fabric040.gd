extends RefCounted
## Deterministic, presentation-only infill derived from a committed display snapshot.
const Hex=preload("res://core/hex.gd")
const PROPS=["bench","crates","flowers","lamp","stall","fence","prop_tree_broadleaf","prop_shrub"]

static func derive(g,snapshot:Dictionary={}) -> Dictionary:
	var projects:Dictionary=snapshot.get("projects",g.s.projects)
	var groups:Dictionary=snapshot.get("groups",g.s.groups)
	var tiles:Dictionary=snapshot.get("tiles",g.s.cells)
	var metrics:Dictionary=snapshot.get("metrics",g.s.metrics)
	var compiled_value:Variant=snapshot.get("compiled",g.compiled)
	var compiled:Dictionary=compiled_value if compiled_value is Dictionary else {}
	var seed_value=int(g.s.get("seed",0))
	var residents={}
	for group in groups.values():
		var host=str(group.get("host",""))
		if projects.has(host):residents[host]=float(residents.get(host,0))+float(group.get("count",0))
	var levels={}
	var project_ids=projects.keys();project_ids.sort()
	for id_value in project_ids:
		var id=str(id_value)
		var p:Dictionary=projects[id]
		if p.get("special",false):continue
		var def:Dictionary=g.c.projects.get(str(p.get("def","")),{})
		if def.is_empty():continue
		var stats:Dictionary=compiled.get("stats",{}).get(id,def.get("stats",{}))
		var capacity=maxf(.001,float(stats.get("housing",0))*float(stats.get("efficiency",1)))
		var occupancy=clampf(float(residents.get(id,0))/capacity,0,1) if float(stats.get("housing",0))>0 else clampf(float(p.get("usage",0)),0,1)
		var utilization=clampf(float(metrics.get("utilization",{}).get(id,{}).get("utilization",p.get("usage",0))),0,1)
		var tier=clampi(int(p.get("tier",def.get("tier",1))),1,3)
		var traits:Array=p.get("traits",def.get("traits",[]))
		levels[id]={"tier":tier,"occupancy":occupancy,"utilization":utilization,"development":clampf((tier-1)*.62+occupancy*.22+utilization*.16,0,2.25),"domain":str(def.get("domain","")),"traits":traits.duplicate(),"completed":str(p.get("status","running"))!="construction","def":str(p.get("def",""))}
	var relations={}
	for link in metrics.get("employment_links",[]):
		var host=str(groups.get(str(link.get("group","")),{}).get("host",""))
		_add_relation(relations,host,str(link.get("target","")),minf(.30,float(link.get("people",0))*.018))
	for link in metrics.get("service_links",[]):
		_add_relation(relations,str(link.get("source","")),str(link.get("home","")),minf(.30,float(link.get("people",0))*.015))
	for owner in metrics.get("activity_ledger",{}):
		for entry in metrics.activity_ledger[owner]:
			if entry.get("kind","")!="input":continue
			_add_relation(relations,str(entry.get("source_id","")),str(owner),minf(.24,float(entry.get("rate",0))*.04))
	var fillers=[]
	var connector_specs=[]
	var seen={}
	var ordered_tiles=tiles.keys();ordered_tiles.sort()
	for tile_value in ordered_tiles:
		var tile=str(tile_value)
		var a=str(tiles[tile].get("building",""))
		if a=="" or not levels.has(a) or not bool(levels[a].get("completed",false)) or _is_water(tiles[tile]):continue
		for neighbor in Hex.neighbors(str(tile)):
			if not tiles.has(neighbor):continue
			var b=str(tiles[neighbor].get("building",""))
			if b=="" or b==a or not levels.has(b) or not bool(levels[b].get("completed",false)) or _is_water(tiles[neighbor]):continue
			var pair_key=_pair(a,b)
			if seen.has(pair_key):continue
			seen[pair_key]=true
			var relation=float(relations.get(pair_key,0))
			var operator_a=str(projects[a].get("operator_org",""));var operator_b=str(projects[b].get("operator_org",""))
			var same_operator=operator_a!="" and operator_a==operator_b
			var link_strength=clampf(relation+(0.12 if same_operator else 0),0,1)
			var development=(float(levels[a].development)+float(levels[b].development))*.5
			var connection=clampf(link_strength*.72+development*.18+minf(float(levels[a].occupancy),float(levels[b].occupancy))*.10,0,1)
			# Even a modestly developed, same-operator pair gets a small visible
			# annex or shared yard. Stronger operational relations add density.
			if connection<.08:continue
			var kind=_connector_kind(levels[a],levels[b],development)
			var tile_pair=_pair(str(tile),neighbor)
			connector_specs.append({
				"id":tile_pair,"a":str(tile),"b":neighbor,"project_a":a,"project_b":b,"kind":kind,
				"variant":_stable_hash(str(seed_value)+"|"+tile_pair+"|"+str(levels[a].def)+"|"+str(levels[b].def))%3,
				"strength":connection,"relation":relation,"same_operator":same_operator,
				"development":development,"tier_a":int(levels[a].tier),"tier_b":int(levels[b].tier),
				"domain_a":str(levels[a].domain),"domain_b":str(levels[b].domain),"height_mode":"terrain_adaptive"
			})
			var count=clampi(1+int(floor(development*.45))+int(floor(connection*.8)),1,3)
			var h=_stable_hash(str(seed_value)+"|"+pair_key)
			for i in count:
				var prop_index=_stable_hash(pair_key+"|"+str(i)+"|"+str(h))%PROPS.size()
				fillers.append({"a":str(tile),"b":neighbor,"asset":PROPS[prop_index],"strength":connection,"variant":i,"key":pair_key})
	fillers.sort_custom(func(x,y):return str(x.key)<str(y.key) if x.key!=y.key else int(x.variant)<int(y.variant))
	connector_specs.sort_custom(func(x,y):return str(x.id)<str(y.id))
	return {"levels":levels,"connections":relations,"fillers":fillers,"connectors":connector_specs,"seed":seed_value}

static func _is_water(tile:Dictionary) -> bool:
	var terrain=str(tile.get("terrain","plain")).to_lower()
	if terrain in ["water","sea","ocean","river","channel"] or bool(tile.get("water",false)) or bool(tile.get("is_water",false)):return true
	var traits:Variant=tile.get("traits",[])
	if traits is Array or traits is PackedStringArray:
		for trait_id in traits:
			if str(trait_id) in ["tile.water","water","tile.river"]:return true
	return false

static func _connector_kind(a:Dictionary,b:Dictionary,development:float) -> String:
	var a_res=_has_trait(a,"building.residential") or str(a.get("domain","")) in ["residential","community"]
	var b_res=_has_trait(b,"building.residential") or str(b.get("domain","")) in ["residential","community"]
	var a_ind=_industrial(a);var b_ind=_industrial(b)
	if a_res and b_res:
		return "residential_annex" if development>=1.05 or maxi(int(a.get("tier",1)),int(b.get("tier",1)))>=3 else "residential_walkway"
	if a_ind and b_ind:
		return "industrial_gallery" if _has_trait(a,"building.logistics") or _has_trait(b,"building.logistics") or _has_trait(a,"building.energy") or _has_trait(b,"building.energy") else "industrial_shed"
	var a_commercial=_has_trait(a,"building.commercial") or str(a.get("domain",""))=="commercial"
	var b_commercial=_has_trait(b,"building.commercial") or str(b.get("domain",""))=="commercial"
	var a_public=_has_trait(a,"building.public_service") or str(a.get("domain",""))=="public"
	var b_public=_has_trait(b,"building.public_service") or str(b.get("domain",""))=="public"
	return "street_shop" if a_commercial or b_commercial or a_public or b_public else "mixed_court"

static func _industrial(level:Dictionary) -> bool:
	var domain=str(level.get("domain",""))
	return domain in ["industrial","infrastructure"] or _has_trait(level,"building.industrial") or _has_trait(level,"building.extractive") or _has_trait(level,"building.logistics") or _has_trait(level,"building.energy")

static func _has_trait(level:Dictionary,trait_id:String) -> bool:
	return trait_id in level.get("traits",[])

static func _add_relation(relations:Dictionary,a:String,b:String,amount:float) -> void:
	if a=="" or b=="" or a==b or amount<=0:return
	var key=_pair(a,b)
	relations[key]=clampf(float(relations.get(key,0))+amount,0,1)

static func _pair(a:String,b:String) -> String:
	return a+"|"+b if a<b else b+"|"+a

static func _stable_hash(value:String) -> int:
	var result=5381
	for i in value.length():result=int(((result<<5)+result)^value.unicode_at(i))&0x7fffffff
	return result
