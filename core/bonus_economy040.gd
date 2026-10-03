extends RefCounted
## Commission rewards are persistent deployment licenses. Installed instances remain
## owned by Game/Rules; this module owns the license and reward-choice ledger.
const Rng = preload("res://core/rng.gd")
const Rules = preload("res://core/rules.gd")

static func _ensure(g) -> void:
	if not g.s.get("block") is Dictionary:
		g.s.block = {"id":"block:1"}
	var b:Dictionary = g.s.block
	if not b.get("bonus_wallet") is Dictionary: b.bonus_wallet = {}
	if not b.get("deployed_bonuses") is Dictionary: b.deployed_bonuses = {}
	if not b.get("reward_choices") is Array: b.reward_choices = []
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
		return str(g.bonus_eligible(bonus_id, host_type, host_id, slot)) == ""
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
		"preview_reason":reason,"suggested_host":host}

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

static func draw(g, source:String, tags:Array=[]) -> Dictionary:
	_ensure(g)
	var source_id = source.strip_edges()
	if source_id == "": return {}
	for record in g.s.block.reward_choices:
		if str(record.get("source", "")) == source_id: return record.duplicate(true)

	var matching:Array = []
	var fallback:Array = []
	var metadata:Dictionary = g.c.v040.get("bonus_economy", {}).get("metadata", {})
	var ids:Array = g.c.bonuses.keys()
	ids.sort()
	for raw_id in ids:
		var bid = str(raw_id)
		var meta = _metadata(g, bid)
		if meta.is_empty(): continue
		if int(g.s.block.bonus_wallet.get(bid, {}).get("charges", 0)) > 0: continue
		var max_deployments = int(meta.get("max_deployments", 1))
		if _deployed_count(g, bid) >= max_deployments: continue
		var card = _card(g, bid)
		if not card.deployable_now and not card.near_future: continue
		fallback.append(card)
		if _tags_match(meta, tags): matching.append(card)
	if matching.size() >= 3: fallback = matching
	elif matching.size() > 0:
		# Keep the commission's domain recognizable, then fill from eligible cards.
		fallback = matching.duplicate(true)
		for card in _all_reward_cards(g):
			if fallback.size() >= 3: break
			var seen = false
			for existing in fallback:
				if existing.bonus_id == card.bonus_id: seen = true
			if not seen: fallback.append(card)
	if fallback.size() < 3: fallback = _all_reward_cards(g)

	var immediate:Array = []
	var future:Array = []
	for card in fallback:
		if card.deployable_now: immediate.append(card)
		elif card.near_future: future.append(card)
	var choices:Array = []
	if immediate.size() >= 2 and not future.is_empty():
		for i in range(2):
			var selected = _pick(g, immediate)
			choices.append(selected)
			immediate.erase(selected)
		var preview = _pick(g, future)
		choices.append(preview)
	else:
		var pool = fallback.duplicate(true)
		while choices.size() < 3 and not pool.is_empty():
			var selected = _pick(g, pool)
			choices.append(selected)
			pool.erase(selected)
	if choices.size() != 3: return {}
	var record = {"reward_id":g.uid("R"),"source":source_id,"tags":tags.duplicate(true),"choices":choices,
		"created_tick":int(g.s.get("simulation_tick", 0)),"created_at":float(g.s.get("local_hours", 0)),
		"claimed":false,"chosen_bonus":""}
	g.s.block.reward_choices.append(record)
	return record.duplicate(true)

static func _all_reward_cards(g) -> Array:
	var result:Array = []
	var ids:Array = g.c.bonuses.keys()
	ids.sort()
	for raw_id in ids:
		var bid = str(raw_id)
		var meta = _metadata(g, bid)
		if meta.is_empty() or _deployed_count(g, bid) >= int(meta.get("max_deployments", 1)): continue
		if int(g.s.block.bonus_wallet.get(bid, {}).get("charges", 0)) > 0: continue
		var card = _card(g, bid)
		if card.deployable_now or card.near_future: result.append(card)
	return result

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
	var allowed = false
	for choice in record.get("choices", []):
		var id = str(choice.get("bonus_id", choice.get("id", ""))) if choice is Dictionary else str(choice)
		if id == bonus_id: allowed = true
	if not allowed: return "请选择这份奖励中的方案。"
	var error = grant(g, bonus_id, reward_id)
	if error != "": return error
	record.claimed = true
	record.chosen_bonus = bonus_id
	g.s.block.reward_choices[record_index] = record
	var commissions:Dictionary = g.s.block.get("commissions", {})
	if str(commissions.get("pending_reward", "")) == reward_id: commissions.pending_reward = ""
	if not commissions.is_empty(): g.s.block.commissions = commissions
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
	if _deployed_count(g, bid) >= int(meta.get("max_deployments", 1)): return "这个方案在本Block已经部署过。"
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
