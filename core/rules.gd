extends RefCounted
## Rule interpreter. Content supplies selectors and operators; no Bonus ID switches.
## Compile reads only a stable world. All modifications reduce after collection.
const Hex=preload("res://core/hex.gd")
const SUPPORTED=["modify","configure","capacity_convert","capacity_copy","grant_recipe","field_convert","emit","relate","slot_add"]
# Content is immutable; only per-Block blueprint levels and live instances are state.
static var _bonus_content:Dictionary = {}
static func bonus_content() -> Dictionary:
	if _bonus_content.is_empty():
		var parsed = JSON.parse_string(FileAccess.get_file_as_string("res://content/beta/bonus_upgrades.json"))
		if parsed is Dictionary: _bonus_content = parsed
	return _bonus_content

static func copy_strength(g, count:int, bonus_id:String="") -> float:
	if count <= 0: return 0.0
	var coefficient = float(bonus_content().get("subsequent_copy_weight", 1.0 / 3.0))
	if g.c.get("beta") is Dictionary:
		coefficient = float(g.c.beta.get("bonus", {}).get("subsequent_copy_weight", coefficient))
	if g.c.bonuses.has(bonus_id):
		coefficient = float(g.c.bonuses[bonus_id].get("subsequent_copy_weight", coefficient))
	return 1.0 + float(count - 1) * maxf(0.0, coefficient)

static func _weighted(value:float, mode:String, strength:float, maximum=null) -> float:
	var result = value * strength if mode == "add" else 1.0 + (value - 1.0) * strength
	# Negative multipliers must never become negative consumption or output.
	if mode == "multiply": result = maxf(0.0, result)
	if maximum != null: result = minf(float(maximum), result)
	return result

static func bonus_rules(g, bonus_id:String, count:int=1, level_override:int=-1) -> Array:
	if not g.c.bonuses.has(bonus_id): return []
	var content = bonus_content()
	var level = clampi(int(g.s.get("block", {}).get("blueprint_levels", {}).get(bonus_id, 0)), 0, 2)
	if level_override >= 0: level = clampi(level_override, 0, 2)
	var patches:Dictionary = content.get("upgrades", {}).get(bonus_id, {})
	var policies:Dictionary = content.get("rule_policies", {})
	var strength = copy_strength(g, count, bonus_id)
	var result:Array = []
	for original in g.c.bonuses[bonus_id].get("rules", []):
		var rule:Dictionary = original.duplicate(true)
		var rule_id = str(rule.get("id", ""))
		for key in patches.get(rule_id, {}):
			var levels:Array = patches[rule_id][key]
			rule[key] = levels[mini(level, levels.size() - 1)].duplicate(true) if levels[mini(level, levels.size() - 1)] is Dictionary else levels[mini(level, levels.size() - 1)]
		var policy:Dictionary = policies.get(rule_id, {})
		if str(rule.get("op", "")) == "modify" and str(rule.get("mode", "")) in ["add", "multiply"]:
			rule.value = _weighted(float(rule.value), str(rule.mode), strength)
		elif policy.has("value_mode"):
			rule.value = _weighted(float(rule.value), str(policy.value_mode), strength, policy.get("maximum"))
		elif policy.has("dictionary_values"):
			for key in policy.dictionary_values:
				rule.value[key] = _weighted(float(rule.value[key]), str(policy.dictionary_values[key]), strength)
		elif policy.has("strength_field"):
			var field = str(policy.strength_field)
			rule[field] = _weighted(float(rule.get(field, 1.0)), "add", strength, policy.get("maximum"))
		# Unspecified structure, booleans, fields, fixed bounds and overrides are idempotent.
		result.append(rule)
	return result

static func duplicate_has_benefit(g, bonus_id:String, existing_count:int) -> bool:
	if existing_count <= 0: return true
	var benefit_ids:Array = bonus_content().get("benefit_rules", {}).get(bonus_id, [])
	var before = bonus_rules(g, bonus_id, existing_count)
	var after = bonus_rules(g, bonus_id, existing_count + 1)
	for i in mini(before.size(), after.size()):
		if str(before[i].get("id", "")) in benefit_ids and before[i] != after[i]: return true
	return false

static func ids(d:Dictionary) -> Array:
	var a=d.keys();a.sort();return a
static func near(s:Dictionary,a:String,b:String) -> int:
	if not s.projects.has(a) or not s.projects.has(b):return 999
	var x=str(s.projects[a].tile);var y=str(s.projects[b].tile)
	if not s.cells.has(x) or not s.cells.has(y):return 999
	return Hex.distance(x,y)
static func any_trait(have:Array,want:Array) -> bool:
	if want.is_empty():return true
	for t in want:
		if t in have:return true
	return false
static func semantic_traits(s:Dictionary,id:String) -> Array:
	# Labels grant query roles, never rewrite innate Trait membership.
	var ts=s.projects[id].traits.duplicate()
	if "use.commercial" in s.projects[id].get("labels",[]) and not "building.commercial" in ts:ts.append("building.commercial")
	return ts
static func condition(s:Dictionary,id:String,when:Dictionary,source:String="",provider:String="") -> bool:
	var p=s.projects[id];var traits=semantic_traits(s,id)
	for t in when.get("traits",[]):
		if not t in traits:return false
	if not any_trait(traits,when.get("any_traits",[])):return false
	var tile=s.cells.get(p.tile,{})
	for t in when.get("tile_traits",[]):
		if not t in tile.get("traits",[]):return false
	if when.get("same_provider",false) and (provider=="" or provider!=p.operator_org):return false
	for k in when.get("field_min",{}):
		if aggregate(tile.get("fields",{}).get(k,[]),"sum")<float(when.field_min[k]):return false
	return true
static func aggregate(contributions:Array,mode:String="sum") -> float:
	if contributions.is_empty():return 0
	var values=[]
	for x in contributions:values.append(float(x.value))
	match mode:
		"max":return values.max()
		"min":return values.min()
		"product":
			var v=1.0
			for x in values:v*=x
			return v
		"mean":
			var v=0.0
			for x in values:v+=x
			return v/values.size()
	var total=0.0
	for v in values:total+=v
	return total
static func reduce_number(base:float,modifiers:Array) -> Dictionary:
	var add=0.0;var mult=1.0;var lo=-INF;var hi=INF;var override={};var trail=[]
	var ordered=modifiers.duplicate();ordered.sort_custom(func(a,b):return str(a.uid)<str(b.uid))
	for m in ordered:
		var v=float(m.value)
		match str(m.mode):
			"add":add+=v
			"multiply":mult*=v
			"floor":lo=maxf(lo,v)
			"cap":hi=minf(hi,v)
			"override":
				if override.is_empty() or wins(m,override):override=m
		trail.append({"source":m.source,"rule":m.uid,"mode":m.mode,"value":v})
	var result=(base+add)*mult
	# Content constraints combine deterministically; invalid inverted bounds are rejected at authoring.
	if lo>hi:lo=hi
	result=clampf(result,lo,hi)
	if not override.is_empty():result=float(override.value)
	return {"value":result,"base":base,"add":add,"multiply":mult,"cap":hi if is_finite(hi) else null,"floor":lo if is_finite(lo) else null,"override":override.get("source",""),"rules":trail}
static func wins(a:Dictionary,b:Dictionary) -> bool:
	if int(a.specificity)!=int(b.specificity):return int(a.specificity)>int(b.specificity)
	if int(a.priority)!=int(b.priority):return int(a.priority)>int(b.priority)
	return str(a.uid)<str(b.uid)
static func selections(s:Dictionary,context:Dictionary,rule:Dictionary) -> Array:
	var sources=[];var ht=context.host_type;var host=context.host
	if ht=="building":
		if s.projects.has(host):sources.append(host)
	else:
		for id in ids(s.projects):
			var p=s.projects[id]
			if p.get("special",false) and not rule.get("include_special", false):continue
			if ht=="organization" and (p.operator_org!=host or p.get("operator_unresolved",false)):continue
			if any_trait(semantic_traits(s,id),context.get("eligibility",[])):sources.append(id)
	var result={};var shape=rule.get("targets","self")
	for source in sources:
		if not any_trait(semantic_traits(s,source),context.get("eligibility",[])):continue
		if shape=="self":result[source]=source
		else:
			if s.projects[source].status!="running" or s.projects[source].paused:continue
			for target in ids(s.projects):
				if s.projects[target].get("special",false) and not rule.get("include_special", false):continue
				var distance=near(s,source,target)
				if shape=="adjacent" and distance!=1:continue
				if shape=="radius" and distance>int(rule.get("radius",2)):continue
				result[target]=source
	var rows=[]
	for target in ids(result):
		if condition(s,target,rule.get("when",{}),result[target],context.get("provider","")):rows.append({"id":target,"source_id":result[target]})
	return rows
static func compile(g) -> Dictionary:
	var s=g.s;var c=g.c;var contexts=[];var stats={};var configs={};var recipes={};var emitters={};var trace={};var numeric={};var transforms={};var options={}
	for id in ids(s.projects):
		var p=s.projects[id]
		stats[id]=c.projects[p.def].stats.duplicate(true) if c.projects.has(p.def) else p.base_stats.duplicate(true)
		if not p.get("upgrade_job",{}).is_empty():
			stats[id].efficiency*=float(c.v040.progression.upgrade_operation_factor)
			for key in stats[id]:
				if key.begins_with("service.") or key=="output.energy":stats[id][key]*=float(c.v040.progression.upgrade_operation_factor)
		configs[id]={};recipes[id]=[];emitters[id]=[];trace[id]={};numeric[id]={};transforms[id]=[]
		for r in p.get("retained_storage",{}):stats[id]["retained_storage."+r]=float(p.retained_storage[r])
	contexts.append({"host_type":"block","host":s.block.id,"rules":c.data.world_rules,"specificity":0,"name":"基础世界","provider":"","uid":"world","eligibility":[]})
	for slot in ids(s.block.policies):
		var policy=c.policies[s.block.policies[slot]]
		contexts.append({"host_type":"block","host":s.block.id,"rules":policy.rules,"specificity":10,"name":policy.name,"provider":"","uid":"policy:"+policy.id,"eligibility":[]})
	for org_id in ids(s.organizations):
		var org=c.operators[org_id]
		contexts.append({"host_type":"organization","host":org_id,"rules":org.rules,"specificity":20,"name":org.name,"provider":org_id,"uid":"org:"+org_id,"eligibility":[]})
	var capstone:Dictionary = s.block.get("capstone", {})
	if not capstone.is_empty() and capstone.get("rules") is Array:
		contexts.append({"host_type":"block","host":s.block.id,"rules":capstone.rules,"specificity":10,"name":str(capstone.get("name", "主题地标")),"provider":"","uid":str(capstone.get("id", "capstone")),"eligibility":[]})
	# Collapse copies BEFORE reduction: x2 + two further copies becomes x2.6667,
	# not x2*x2*x2. Different hosts remain independent sources at full first strength.
	var bonus_groups:Dictionary = {}
	for instance_id in ids(s.bonus_instances):
		var instance:Dictionary=s.bonus_instances[instance_id]
		if not instance.get("active", false) or not c.bonuses.has(instance.get("bonus_id", "")):continue
		var key = str(instance.host_type) + "|" + str(instance.host_id) + "|" + str(instance.bonus_id)
		if not bonus_groups.has(key): bonus_groups[key] = {"instance":instance,"uid":instance_id,"count":0}
		bonus_groups[key].count += 1
	for key in ids(bonus_groups):
		var group:Dictionary = bonus_groups[key]
		var instance:Dictionary = group.instance
		var def:Dictionary = c.bonuses[instance.bonus_id]
		var level = clampi(int(s.block.get("blueprint_levels", {}).get(instance.bonus_id, 0)), 0, 2)
		var label = str(def.name)
		if level > 0: label += " · " + str(bonus_content().grades[level])
		if int(group.count) > 1: label += " ×%d（合成强度 %.3f；数值下限0，比例上限1）" % [int(group.count), copy_strength(g, int(group.count), str(instance.bonus_id))]
		contexts.append({"host_type":instance.host_type,"host":instance.host_id,"rules":bonus_rules(g, str(instance.bonus_id), int(group.count)),"specificity":{"building":30,"organization":20,"block":10}[instance.host_type],"name":label,"provider":instance.get("contract_org",instance.get("provider_org", "")),"uid":group.uid,"eligibility":[] if instance.host_type=="building" else def.traits})
	for ctx in contexts:
		for rule in ctx.rules:
			assert(rule.op in SUPPORTED,"Unsupported Rule operator: "+str(rule.op))
			var selected=selections(s,ctx,rule)
			# True Block-wide options have one writer per resolved rule, not per building.
			if rule.op=="configure" and ctx.host_type=="block":
				var contender={"value":rule.value,"specificity":ctx.specificity,"priority":int(rule.get("priority",0)),"uid":ctx.uid+":"+rule.id,"source":ctx.name}
				if not options.has(rule.key) or wins(contender,options[rule.key]):options[rule.key]=contender
			for sel in selected:
				var id=sel.id
				var meta={"specificity":int(ctx.specificity)+(1 if not rule.get("when",{}).is_empty() else 0),"priority":int(rule.get("priority",0)),"uid":ctx.uid+":"+rule.id+":"+id,"source":ctx.name}
				if rule.op=="modify":
					var fields=[];var field=rule.field
					if field.ends_with("*"):
						for k in stats[id]:
							if k.begins_with(field.trim_suffix("*")):fields.append(k)
					else:fields.append(field)
					for k in fields:
						if not numeric[id].has(k):numeric[id][k]=[]
						var mod=meta.duplicate();mod.mode=rule.mode;mod.value=rule.value;numeric[id][k].append(mod)
				elif rule.op=="configure":
					var value=meta.duplicate();value.value=rule.value
					if not configs[id].has(rule.key) or wins(value,configs[id][rule.key]):configs[id][rule.key]=value
				elif rule.op=="grant_recipe":
					var entry=rule.duplicate(true);entry.uid=meta.uid;entry.source=ctx.name;entry.source_id=sel.source_id;recipes[id].append(entry)
				elif rule.op=="emit":
					var entry=rule.duplicate(true);entry.uid=meta.uid;entry.source=ctx.name;emitters[id].append(entry)
				else:
					var entry=rule.duplicate(true);entry.meta=meta;entry.source_id=sel.source_id;transforms[id].append(entry)
	for id in ids(stats):
		for k in ids(numeric[id]):
			var result=reduce_number(float(stats[id].get(k,0)),numeric[id][k]);stats[id][k]=result.value;trace[id][k]=result
		for k in ids(configs[id]):configs[id][k]=configs[id][k].value
		for r in transforms[id]:
			match str(r.op):
				"capacity_convert","capacity_copy":
					var v=float(stats[id].get(r.from,0))*float(r.fraction)
					if r.op=="capacity_convert":stats[id][r.from]-=v
					stats[id][r.to]=float(stats[id].get(r.to,0))+v
					trace[id][r.to]={"base":c.projects.get(s.projects[id].def,{}).get("stats",{}).get(r.to,0),"value":stats[id][r.to],"rules":[{"source":r.meta.source,"mode":r.op,"value":v}]}
				"field_convert":configs[id]["field_conversion"]=r
				"relate":configs[id][r.relation]=true
				"slot_add":configs[id]["slot_added"]=r
		# Real spatial synergy baseline: data selects categories; no named combo bonuses.
		var count=0.;var industrial=0.;var commercial=0.
		for other in ids(stats):
			if near(s,id,other)!=1 or s.projects[other].status!="running" or s.projects[other].paused:continue
			var t=semantic_traits(s,other)
			if "building.industrial" in t:industrial+=1
			if "building.commercial" in t:commercial+=1
			var own=semantic_traits(s,id)
			var compatible=("building.industrial" in own and "building.industrial" in t) or ("building.commercial" in own and "building.industrial" in t)
			if (configs[id].get("medical_commercial_relation",false) and "building.commercial" in t) or (configs[other].get("medical_commercial_relation",false) and "building.commercial" in own):compatible=true
			var amount=.1 if compatible else .1*float(configs[id].get("universal_adjacency",0))
			if compatible and "building.industrial" in own and s.projects[id].operator_org==s.projects[other].operator_org:amount*=float(stats[id].get("synergy.same_industrial",1))
			if compatible and "building.commercial" in own and "building.industrial" in t:amount*=float(configs[id].get("industrial_adjacency",1))
			count+=amount
		if count>0:
			stats[id].efficiency*=1+count;trace[id]["spatial_synergy"]={"base":1.,"value":1+count,"rules":[{"source":"相邻建筑关系","mode":"multiply","value":1+count}]}
		# A small full self-operated reward; mixed schemes remain legitimate.
		if not s.projects[id].get("special",false) and g.cohesion(id).active:
			var reward=float(c.operators[s.projects[id].operator_org].get("cohesion_efficiency",.03))
			stats[id].efficiency*=1.+reward
			trace[id]["cohesion"]={"base":1.,"value":1.+reward,"rules":[{"source":"整套自营方案","mode":"multiply","value":1.+reward}]}
		# Previous settlement result affects this tick; never recurse into current output.
		var heat_bonus=float(s.projects[id].get("next_heat_bonus",0))
		stats[id].efficiency*=1+heat_bonus
		if heat_bonus>0:trace[id]["heat"]={"base":1.,"value":1+heat_bonus,"rules":[{"source":"上周期实际收到的余热","mode":"multiply","value":1+heat_bonus}]}
		for k in stats[id]:
			if not is_finite(float(stats[id][k])):push_error("Non-finite compiled stat "+id+":"+k)
	for k in options:options[k]=options[k].value
	return {"stats":stats,"config":configs,"recipes":recipes,"emitters":emitters,"trace":trace,"options":options}
