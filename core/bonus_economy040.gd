extends RefCounted
## Annual, population and theme rewards share a persistent ordered queue.
## Copies are consumable; blueprint research belongs to this Block only.
const Rng = preload("res://core/rng.gd")
const Rules = preload("res://core/rules.gd")

static func _ensure(g) -> void:
	if not g.s.get("block") is Dictionary:
		g.s.block = {"id":"block:1"}
	var b:Dictionary = g.s.block
	if not b.get("bonus_wallet") is Dictionary: b.bonus_wallet = {}
	if not b.get("deployed_bonuses") is Dictionary: b.deployed_bonuses = {}
	if not b.get("reward_choices") is Array: b.reward_choices = []
	if not b.get("blueprint_levels") is Dictionary: b.blueprint_levels = {}
	if not g.s.get("rng") is Dictionary: g.s.rng = {}
	if not g.s.rng.has("reward"):
		var seed_value = int(g.s.get("seed", 1))
		g.s.rng.reward = posmod(seed_value * 48271 + 104729, 2147483646) + 1

static func _metadata(g, bonus_id:String) -> Dictionary:
	var all:Dictionary = g.c.v040.get("bonus_economy", {}).get("metadata", {})
	if all.get(bonus_id) is Dictionary: return all[bonus_id]
	var def:Dictionary = g.c.bonuses.get(bonus_id, {})
	if def.is_empty(): return {}
	var scope = "building"
	for host in def.get("hosts", []):
		if host in ["organization", "block"]: scope = str(host); break
	return {"scope":scope,"persistence":"Local","tags":[],"max_deployments":1}

static func _tags_match(meta:Dictionary, requested:Array) -> bool:
	if requested.is_empty(): return true
	var tags:Array = meta.get("tags", [])
	for tag in requested:
		if str(tag) in tags: return true
	return false

static func _deployed_count(g, bonus_id:String) -> int:
	var entry:Dictionary = g.s.block.deployed_bonuses.get(bonus_id, {})
	var count = int(entry.get("count", 0))
	count = maxi(count, entry.get("instances", []).size() if entry.get("instances") is Array else 0)
	return count

static func _find_host(g, bonus_id:String, meta:Dictionary) -> Dictionary:
	var scope = str(meta.get("scope", "building"))
	if not g.c.bonuses.has(bonus_id): return {}
	var def:Dictionary = g.c.bonuses[bonus_id]
	if scope == "building":
		var projects:Array = g.playable_ids() if g.has_method("playable_ids") else []
		projects.sort()
		for id in projects:
			var p:Dictionary = g.s.projects[id]
			var slots:Array = p.get("slots", {}).keys()
			slots.sort()
			for slot in slots:
				if _eligible(g, bonus_id, "building", str(id), str(slot)):
					return {"type":"building","id":str(id),"slot":str(slot)}
	elif scope == "organization":
		var organizations:Array = g.s.get("organizations", {}).keys()
		organizations.sort()
		for id in organizations:
			if _eligible(g, bonus_id, "organization", str(id), ""):
				return {"type":"organization","id":str(id),"slot":""}
	elif scope == "block":
		var block_id = str(g.s.block.get("id", ""))
		if block_id != "" and _eligible(g, bonus_id, "block", block_id, ""):
			return {"type":"block","id":block_id,"slot":""}
	return {}

static func _eligible(g, bonus_id:String, host_type:String, host_id:String, slot:String) -> bool:
	if g.has_method("bonus_eligible"):
		var replacing = str(g.s.projects.get(host_id, {}).get("slots", {}).get(slot, "")) if host_type == "building" else ""
		return str(g.bonus_eligible(bonus_id, host_type, host_id, slot, replacing)) == ""
	return false

static func _future_host(g, bonus_id:String, meta:Dictionary) -> bool:
	if str(meta.get("scope", "building")) != "building": return false
	if not g.c.bonuses.has(bonus_id): return false
	var bonus:Dictionary = g.c.bonuses[bonus_id]
	var gate:Dictionary = bonus.get("install_gate", bonus.get("gate", {}))
	var wanted:Array = bonus.get("traits", [])
	var allowed:Array = bonus.get("allowed_slots", [])
	for id in g.playable_ids():
		var p:Dictionary = g.s.projects[id]
		if int(p.get("tier", 1)) >= 3: continue
		var current:Dictionary = g.c.projects.get(str(p.get("node_id", p.get("def", ""))), {})
		for next_id in current.get("upgrades", []):
			var next:Dictionary = g.c.projects.get(str(next_id), {})
			if next.is_empty() or not Rules.any_trait(next.get("traits", []), wanted): continue
			var next_tier = int(next.get("tier", int(p.get("tier", 1)) + 1))
			var track:Array = p.get("slot_track", [])
			if next_tier < 1 or next_tier > track.size(): continue
			var slot_type = str(track[next_tier - 1])
			if slot_type == "Flex":
				if not str(bonus.get("slot", "")) in ["Production", "Logistics"]: continue
			elif not slot_type in allowed: continue
			var fits = true
			var next_traits:Array = next.get("traits", [])
			for required_trait in gate.get("traits", []):
				if not required_trait in next_traits: fits = false
			if not Rules.any_trait(next_traits, gate.get("any_traits", [])): fits = false
			var tile:Dictionary = g.s.cells.get(str(p.get("tile", "")), {})
			for tile_trait in gate.get("tile_traits", []):
				if not tile_trait in tile.get("traits", []): fits = false
			if gate.get("has_inputs", false):
				var has_input = false
				for stat in next.get("stats", {}):
					if str(stat).begins_with("input.") and float(next.stats[stat]) > 0: has_input = true
				if not has_input: fits = false
			for field in gate.get("field_min", {}):
				var amount = Rules.aggregate(tile.get("fields", {}).get(field, []), "sum")
				if amount < float(gate.field_min[field]): fits = false
			if fits: return true
	return false

static func _card(g, bonus_id:String) -> Dictionary:
	var meta = _metadata(g, bonus_id)
	var def:Dictionary = g.c.bonuses.get(bonus_id, {})
	var host = _find_host(g, bonus_id, meta) if not def.is_empty() else {}
	var now = not host.is_empty()
	var future = false if now else _future_host(g, bonus_id, meta)
	var reason = "现在可部署" if now else ("升级后可能部署" if future else "当前城市尚不适用")
	return {"bonus_id":bonus_id,"name":str(def.get("name", bonus_id)),"scope":str(meta.get("scope", "building")),
		"persistence":"Local",
		"tags":meta.get("tags", []).duplicate(true),"deployable_now":now,"near_future":future,
		"preview_reason":reason,"suggested_host":host,
		"copies":int(g.s.get("block", {}).get("bonus_wallet", {}).get(bonus_id, {}).get("charges", 0)),
		"blueprint_level":blueprint_level(g, bonus_id),"grade":str(Rules.bonus_content().grades[blueprint_level(g, bonus_id)]),
		"effects":effect_rows(g, bonus_id, 0, 1),
		"stacking_note":"首份100%，以后每份33⅓%；数值代价同时缩放；乘数不低于0、转换比例不高于1。结构效果不重复，无新增收益不消耗副本。"}

static func _pick(g, rows:Array) -> Dictionary:
	if rows.is_empty(): return {}
	var ids:Array = []
	var weights:Array = []
	for row in rows:
		ids.append(str(row.bonus_id))
		weights.append(maxf(0.01, float(g.c.bonuses.get(row.bonus_id, {}).get("weight", 1))))
	var selected = Rng.pick_weighted(g.s, "reward", ids, weights)
	for row in rows:
		if str(row.bonus_id) == str(selected): return row
	return rows[0]

static func card(g, bonus_id:String) -> Dictionary:
	return _card(g, bonus_id)

static func blueprint_level(g, bonus_id:String) -> int:
	return clampi(int(g.s.get("block", {}).get("blueprint_levels", {}).get(bonus_id, 0)), 0, 2)

static func active_count(g, bonus_id:String, host_type:String="", host_id:String="", excluding:String="") -> int:
	var count = 0
	for instance_id in g.s.get("bonus_instances", {}):
		var instance:Dictionary = g.s.bonus_instances[instance_id]
		if str(instance_id) == excluding or not instance.get("active", false): continue
		if str(instance.get("bonus_id", "")) != bonus_id: continue
		if host_type != "" and str(instance.get("host_type", "")) != host_type: continue
		if host_id != "" and str(instance.get("host_id", "")) != host_id: continue
		count += 1
	return count

static func duplicate_error(g, bonus_id:String, host_type:String, host_id:String, replacing:String="") -> String:
	if replacing != "" and str(g.s.get("bonus_instances", {}).get(replacing, {}).get("bonus_id", "")) == bonus_id:
		return "这个槽已有同名方案；原位替换没有新增效果，不消耗副本。"
	var count = active_count(g, bonus_id, host_type, host_id, replacing)
	if count > 0 and not Rules.duplicate_has_benefit(g, bonus_id, count):
		return "同名结构效果不叠加，或数值已达上限；没有新增收益，不消耗副本。"
	return ""

static func duplicate_preview(g, bonus_id:String, host_type:String, host_id:String) -> Dictionary:
	var count = active_count(g, bonus_id, host_type, host_id)
	var can_add = Rules.duplicate_has_benefit(g, bonus_id, count)
	return {"count":count,"strength":Rules.copy_strength(g, count, bonus_id),
		"next_strength":Rules.copy_strength(g, count + 1, bonus_id),"can_add":can_add,
		"effects":effect_rows(g, bonus_id, count, count + 1),
		"note":"追加数值为首份的33⅓%；副作用同时增加。" if can_add else "同名结构幂等或数值已达上限；无新增收益，不消耗副本。"}

static func effect_rows(g, bonus_id:String, before_count:int, after_count:int, before_level:int=-1, after_level:int=-1) -> Array:
	var before = Rules.bonus_rules(g, bonus_id, maxi(1, before_count), before_level)
	var after = Rules.bonus_rules(g, bonus_id, maxi(1, after_count), after_level)
	var result:Array = []
	var labels = {"input.*":"全部原料投入", "input.material":"材料投入", "energy_demand":"能源需求", "pollution":"污染", "output.goods":"制成品产出", "output.food":"食物产出", "efficiency":"建筑效率", "personnel_mult":"人员效率", "maintenance":"维护", "housing":"住房", "service_decay":"服务衰减", "service.medical":"医疗容量", "service.commercial":"商业容量", "service.entertainment":"娱乐容量", "storage.*":"全部库容", "jobs":"岗位", "exposure_multiplier":"污染暴露", "shortage_multiplier":"短缺敏感度", "population_maintenance":"人口维护", "allocation_priority":"分配优先级", "synergy.same_industrial":"同组织工业邻接", "heat_service":"余热服务", "heat_discount":"余热节能"}
	for index in after.size():
		var a:Dictionary = after[index]
		var b:Dictionary = before[index]
		if str(a.get("op", "")) == "modify" and str(a.get("mode", "")) in ["add", "multiply"]:
			var mode = str(a.mode)
			var start = float(b.value) if before_count > 0 else (1.0 if mode == "multiply" else 0.0)
			result.append({"label":str(labels.get(str(a.field), str(a.field))),"before":start,"after":float(a.value),"unit":"倍" if mode == "multiply" else "", "mode":mode,"rule_id":str(a.id)})
		elif a.has("fraction"):
			result.append({"label":"容量转换比例" if a.op == "capacity_convert" else "容量复制比例","before":float(b.fraction) if before_count > 0 else 0.0,"after":float(a.fraction),"unit":"比例","mode":"fraction","rule_id":str(a.id)})
		elif a.has("rate"):
			result.append({"label":"配方吞吐强度","before":float(b.rate) if before_count > 0 else 0.0,"after":float(a.rate),"unit":"倍","mode":"rate","rule_id":str(a.id)})
			if a.get("output") is Dictionary:
				for resource in a.output:
					result.append({"label":"每批产出" + str(g.c.RESOURCE_NAMES.get(resource, resource)),"before":float(b.output[resource]),"after":float(a.output[resource]),"unit":"","mode":"recipe_output","rule_id":str(a.id)})
		elif a.get("value") is float or a.get("value") is int:
			var policy:Dictionary = Rules.bonus_content().get("rule_policies", {}).get(str(a.get("id", "")), {})
			if policy.has("value_mode"):
				var mode = str(policy.value_mode)
				result.append({"label":str(a.get("key", a.get("field", "数值规则"))),"before":float(b.value) if before_count > 0 else (1.0 if mode == "multiply" else 0.0),"after":float(a.value),"unit":"倍" if mode == "multiply" else "","mode":mode,"rule_id":str(a.id)})
	return result

static func _owned(g, bonus_id:String) -> bool:
	return int(g.s.get("block", {}).get("bonus_wallet", {}).get(bonus_id, {}).get("charges", 0)) > 0 or active_count(g, bonus_id) > 0

static func research_candidates(g, bonus_ids:Array=[], tags:Array=[]) -> Array:
	var result:Array = []
	var ids:Array = bonus_ids.duplicate() if not bonus_ids.is_empty() else Rules.bonus_content().get("upgrades", {}).keys()
	ids.sort()
	var seen:Dictionary = {}
	for raw_id in ids:
		var bid = str(raw_id)
		if seen.has(bid) or not g.c.bonuses.has(bid): continue
		seen[bid] = true
		if not Rules.bonus_content().get("upgrades", {}).has(bid) or blueprint_level(g, bid) >= 2 or not _owned(g, bid): continue
		if not _tags_match(_metadata(g, bid), tags): continue
		var choice = _card(g, bid)
		choice.kind = "research"
		choice.target_level = blueprint_level(g, bid) + 1
		choice.target_grade = str(Rules.bonus_content().grades[choice.target_level])
		choice.effects = effect_rows(g, bid, 1, 1, blueprint_level(g, bid), int(choice.target_level))
		choice.preview_reason = "本Block全部同名副本升级为" + str(choice.target_grade) + "；不消耗库存副本，不改变部署尺度。"
		result.append(choice)
	return result

static func _reward_pool(g, tags:Array, options:Dictionary) -> Array:
	var ids:Array = options.get("bonus_ids", g.c.bonuses.keys()).duplicate()
	ids.sort()
	var all:Array = []
	var matching:Array = []
	var seen:Dictionary = {}
	for raw_id in ids:
		var bid = str(raw_id)
		if seen.has(bid) or not g.c.bonuses.has(bid) or _metadata(g, bid).is_empty(): continue
		seen[bid] = true
		var choice = _card(g, bid)
		choice.kind = "copy"
		all.append(choice)
		if _tags_match(_metadata(g, bid), tags): matching.append(choice)
	# Theme pools are authoritative. Ownership and past deployments never exclude cards.
	if options.has("bonus_ids"): return all
	return matching if not matching.is_empty() else all

static func _choose_cards(g, pool:Array) -> Array:
	var immediate:Array = []
	var future:Array = []
	for choice in pool:
		if choice.get("deployable_now", false): immediate.append(choice)
		elif choice.get("near_future", false): future.append(choice)
	var result:Array = []
	if immediate.size() >= 2 and not future.is_empty():
		for i in 2:
			var picked = _pick(g, immediate)
			result.append(picked)
			immediate.erase(picked)
		result.append(_pick(g, future))
	else:
		var remaining = pool.duplicate(true)
		while result.size() < 3 and not remaining.is_empty():
			var picked = _pick(g, remaining)
			result.append(picked)
			remaining.erase(picked)
	return result

static func _set_candidates(g, record:Dictionary) -> void:
	var options:Dictionary = record.get("options", {})
	var kind = str(options.get("kind", "copy"))
	var tags:Array = record.get("tags", [])
	var pool:Array = []
	if kind == "research":
		if not options.has("bonus_ids") or not options.bonus_ids.is_empty():
			pool = research_candidates(g, options.get("bonus_ids", []), [] if options.has("bonus_ids") else tags)
		if pool.is_empty(): kind = "copy"
	if kind == "copy": pool = _reward_pool(g, tags, options)
	record.kind = kind
	record.choices = _choose_cards(g, pool)
	if record.choices.is_empty():
		record.kind = "currency"
		record.compensation = float(Rules.bonus_content().get("fallback_currency", 30))
		record.choices = [{"bonus_id":"__currency__","name":"城市发展补助","kind":"currency",
			"compensation":record.compensation,"preview_reason":"没有候选方案，领取%.0f通货；不恢复DT。" % record.compensation}]

static func draw(g, source:String, tags:Array=[], options:Dictionary={}) -> Dictionary:
	_ensure(g)
	var source_id = source.strip_edges()
	if source_id == "": return {}
	for record in g.s.block.reward_choices:
		if str(record.get("source", "")) == source_id: return record.duplicate(true)
	var record = {"reward_id":g.uid("R"),"source":source_id,"tags":tags.duplicate(true),"options":options.duplicate(true),
		"requested_kind":str(options.get("kind", "copy")),"choices":[],
		"created_tick":int(g.s.get("simulation_tick", 0)),"created_at":float(g.s.get("local_hours", 0)),
		"claimed":false,"chosen_bonus":""}
	_set_candidates(g, record)
	g.s.block.reward_choices.append(record)
	return record.duplicate(true)

static func pending(g) -> Array:
	var result:Array = []
	for record in g.s.get("block", {}).get("reward_choices", []):
		if not record.get("claimed", false): result.append(record.duplicate(true))
	return result

static func _all_reward_cards(g) -> Array:
	return _reward_pool(g, [], {})

static func grant(g, bonus_id:String, source:String) -> String:
	_ensure(g)
	var bid = str(bonus_id)
	if not g.c.bonuses.has(bid): return "未知方案。"
	var source_id = source.strip_edges()
	if source_id == "": return "许可来源缺失。"
	var wallet:Dictionary = g.s.block.bonus_wallet
	var entry:Dictionary = wallet.get(bid, {})
	if not entry.is_empty() and source_id in entry.get("grant_sources", []): return "这份许可已经领取。"
	var meta = _metadata(g, bid)
	if meta.is_empty(): return "方案许可信息缺失。"
	if entry.is_empty():
		entry = {"charges":0,"acquired_from":source_id,"grant_sources":[],"scope":str(meta.get("scope", "building")),"persistence":"Local"}
	var sources:Array = entry.get("grant_sources", []).duplicate(true)
	if source_id in sources: return "这份许可已经领取。"
	sources.append(source_id)
	entry.grant_sources = sources
	entry.charges = int(entry.get("charges", 0)) + 1
	entry.acquired_from = source_id if sources.size() == 1 else entry.get("acquired_from", source_id)
	entry.scope = str(meta.get("scope", "building"))
	entry.persistence = "Local"
	wallet[bid] = entry
	return ""

static func choose(g, reward_id:String, bonus_id:String) -> String:
	_ensure(g)
	var record_index = -1
	for i in g.s.block.reward_choices.size():
		if str(g.s.block.reward_choices[i].get("reward_id", "")) == reward_id:
			record_index = i
			break
	if record_index < 0: return "找不到这份奖励。"
	var record:Dictionary = g.s.block.reward_choices[record_index]
	if record.get("claimed", false): return "这份奖励已经领取。"
	for i in record_index:
		if not g.s.block.reward_choices[i].get("claimed", false): return "请先领取队列前面的奖励。"
	var allowed = false
	for choice in record.get("choices", []):
		if str(choice.get("bonus_id", "")) == bonus_id: allowed = true
	if not allowed: return "请选择这份奖励中的方案。"
	var kind = str(record.get("kind", "copy"))
	if kind == "currency":
		g.s.currency = float(g.s.get("currency", 0)) + float(record.get("compensation", 30))
	elif kind == "research":
		if blueprint_level(g, bonus_id) >= 2 or not _owned(g, bonus_id):
			# Earlier queued research may have exhausted this blueprint. Reconcile once,
			# save the new offer, and never silently discard the reward opportunity.
			_set_candidates(g, record)
			g.s.block.reward_choices[record_index] = record
			return "蓝图已满级或不再持有；奖励已保存为当前合法研究或主题副本，请重新选择。"
		g.s.block.blueprint_levels[bonus_id] = blueprint_level(g, bonus_id) + 1
		record.applied_level = g.s.block.blueprint_levels[bonus_id]
	else:
		var error = grant(g, bonus_id, reward_id)
		if error != "": return error
	record.claimed = true
	record.chosen_bonus = bonus_id
	g.s.block.reward_choices[record_index] = record
	var commissions:Dictionary = g.s.block.get("commissions", {})
	if str(commissions.get("pending_reward", "")) == reward_id: commissions.pending_reward = ""
	if not commissions.is_empty(): g.s.block.commissions = commissions
	if kind == "research" and g.has_method("refresh_metrics"): g.refresh_metrics()
	return ""

static func install(g, bonus_id:String, host_type:String, host:String, slot:String="") -> String:
	_ensure(g)
	if str(g.s.get("phase", "")) != "play": return "当前不能部署方案。"
	var bid = str(bonus_id)
	if not g.c.bonuses.has(bid): return "未知方案。"
	var meta = _metadata(g, bid)
	if meta.is_empty(): return "方案许可信息缺失。"
	if str(meta.get("scope", "building")) != host_type: return "方案部署尺度不匹配。"
	var wallet:Dictionary = g.s.block.bonus_wallet
	var license:Dictionary = wallet.get(bid, {})
	if license.is_empty() or int(license.get("charges", 0)) < 1: return "没有可用的部署许可。"
	var replacing = str(g.s.projects.get(host, {}).get("slots", {}).get(slot, "")) if host_type == "building" else ""
	var repeat_error = duplicate_error(g, bid, host_type, host, replacing)
	if repeat_error != "": return repeat_error
	if not g.has_method("_install_bonus"): return "底层方案安装接口尚未接入。"
	var before_ids:Array = g.s.get("bonus_instances", {}).keys()
	var provider = "civic"
	if host_type == "building" and g.s.projects.has(host): provider = str(g.s.projects[host].get("operator_org", provider))
	elif host_type == "organization": provider = host
	var error = str(g._install_bonus(bid, host_type, host, provider, slot))
	if error != "": return error

	license.charges = int(license.get("charges", 0)) - 1
	wallet[bid] = license
	var entry:Dictionary = g.s.block.deployed_bonuses.get(bid, {})
	if not entry.get("instances") is Array: entry.instances = []
	if not entry.get("history") is Array: entry.history = []
	var instance_id = ""
	var keys:Array = g.s.bonus_instances.keys()
	keys.sort()
	for iid in keys:
		if iid in before_ids: continue
		var instance:Dictionary = g.s.bonus_instances[iid]
		if instance.get("bonus_id", "") == bid and instance.get("host_type", "") == host_type and instance.get("host_id", "") == host:
			instance_id = str(iid)
			break
	var stamp = {"instance_id":instance_id,"host_type":host_type,"host":host,"slot":slot,
		"installed_at":float(g.s.get("local_hours", 0)),"installed_tick":int(g.s.get("simulation_tick", 0))}
	entry.count = int(entry.get("count", 0)) + 1
	entry.instances.append(instance_id)
	entry.history.append(stamp)
	g.s.block.deployed_bonuses[bid] = entry
	return ""
