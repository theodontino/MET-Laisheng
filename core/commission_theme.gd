extends RefCounted
## Beta themes consume committed simulation receipts. Rendering never advances
## goals, consumes RNG, creates rewards, or replays pre-selection trade history.
const MONTH_SECONDS := 1555200.0
const Rng = preload("res://core/rng.gd")
const Catalog = preload("res://core/catalog.gd")
static var _theme_data:Array = []
static var _event_data:Array = []

static func _themes() -> Array:
	if _theme_data.is_empty():
		var value = JSON.parse_string(FileAccess.get_file_as_string("res://content/beta/commission_themes.json"))
		if value is Array: _theme_data = value
	return _theme_data

static func _events() -> Array:
	if _event_data.is_empty():
		var value = JSON.parse_string(FileAccess.get_file_as_string("res://content/beta/commission_events.json"))
		if value is Array: _event_data = value
	return _event_data

static func _definition(id:String) -> Dictionary:
	for row in _themes():
		if str(row.id) == id: return row
	return {}

static func _event_definition(id:String) -> Dictionary:
	for row in _events():
		if str(row.id) == id: return row
	return {}

static func _reforge_cost(g) -> float:
	return float(g.c.beta.get("commission_candidates", {}).get("reforge_dt_cost", 7.5))

static func _token_chance(g) -> float:
	return float(g.c.beta.get("commission_candidates", {}).get("reforge_opportunity_chance_per_major_cycle", 0.15))

static func _now(g) -> float:
	return float(g.s.get("beta_time", {}).get("local_elapsed_seconds", float(g.s.get("local_hours", 0)) * MONTH_SECONDS))

static func ensure(g) -> void:
	if not g.s.get("block") is Dictionary: g.s.block = {"id":"block:1"}
	if not g.s.block.get("theme") is Dictionary:
		g.s.block.theme = {"id":"", "stage":1, "completed":false, "steady_seconds":0.0, "counters":{},
			"milestone_ids":[], "reforge_tokens":0, "events":[], "event_ids":[], "history":[], "arrivals":[],
			"trade_cursor":g.s.block.get("trade_history", []).size(), "last_tick":int(g.s.get("simulation_tick", 0)),
			"last_major_cycle":0, "last_metrics":{}, "started_seconds":_now(g)}
	var t:Dictionary = g.s.block.theme
	for field in ["events", "event_ids", "history", "milestone_ids", "arrivals"]:
		if not t.get(field) is Array: t[field] = []
	for field in ["counters", "last_metrics"]:
		if not t.get(field) is Dictionary: t[field] = {}
	if not g.s.get("rng") is Dictionary: g.s.rng = {}
	if not g.s.rng.has("theme_events"):
		g.s.rng.theme_events = (absi(int(g.s.get("seed", 1))) + 7919) % 2147483646 + 1

static func _pause(g, reason:String) -> void:
	if not g.s.get("settings") is Dictionary: g.s.settings = {}
	g.s.settings.user_paused = true
	if g.s.get("beta_time") is Dictionary: g.s.beta_time.pause_reason = reason

static func _record(g, kind:String, detail:Dictionary={}) -> void:
	var item = detail.duplicate(true)
	item.kind = kind
	item.local_seconds = _now(g)
	g.s.block.theme.history.append(item)

static func _start(g, id:String) -> void:
	var t:Dictionary = g.s.block.theme
	t.id = id
	t.stage = 1
	while int(t.stage) <= 3 and (id + ":M" + str(t.stage)) in t.milestone_ids: t.stage = int(t.stage) + 1
	t.completed = int(t.stage) > 3
	t.stage = mini(3, int(t.stage))
	t.steady_seconds = 0.0
	t.counters = {}
	t.last_metrics = _metrics(g)
	t.trade_cursor = g.s.block.get("trade_history", []).size()
	t.last_tick = int(g.s.get("simulation_tick", 0))
	t.started_seconds = _now(g)

static func select(g, id:String) -> String:
	ensure(g)
	if g.s.get("phase", "") != "play": return "当前不能选择发展主题。"
	if str(g.s.block.theme.id) != "": return "已有发展主题；更换需要委任重铸权和%.2f DT。" % _reforge_cost(g)
	if _definition(id).is_empty(): return "未知发展主题。"
	_start(g, id)
	_record(g, "selected", {"id":id})
	g.revision += 1
	return ""

static func reforge(g, id:String) -> String:
	ensure(g)
	var t:Dictionary = g.s.block.theme
	if g.s.get("phase", "") != "play": return "当前不能重铸委任。"
	if str(t.id) == "": return "请先选择发展主题。"
	if bool(t.completed): return "主题已完成，本局发展方向已定型。"
	if _definition(id).is_empty() or id == str(t.id): return "请选择另一个发展主题。"
	if int(t.reforge_tokens) < 1: return "需要一枚稀有委任重铸权；使用还需%.2f DT。" % _reforge_cost(g)
	if float(g.s.get("dt", 0)) + 0.000000001 < _reforge_cost(g): return "DT不足%.2f，委任与重铸权均未改变。" % _reforge_cost(g)
	var previous = str(t.id)
	# Every prerequisite was checked before touching the token, currency or state.
	t.reforge_tokens = int(t.reforge_tokens) - 1
	g.s.dt = maxf(0.0, float(g.s.dt) - _reforge_cost(g))
	g.s.totals.dt_used = float(g.s.totals.get("dt_used", 0)) + _reforge_cost(g)
	for event in t.events:
		if str(event.get("status", "")) not in ["offered", "active"]: continue
		# Cancelling a paid contract is not an implicit failure: return escrow.
		g.s.currency = float(g.s.currency) + float(event.get("deposit", 0))
		event.deposit = 0.0
		event.status = "cancelled"
	_start(g, id)
	_record(g, "reforged", {"from":previous, "id":id, "dt_cost":_reforge_cost(g)})
	if float(g.s.dt) <= 0.0: g.Block.on_dt_zero(g)
	g.revision += 1
	return ""

static func _stage(g) -> Dictionary:
	var t:Dictionary = g.s.block.theme
	var def = _definition(str(t.id))
	if def.is_empty(): return {}
	return def.stages[clampi(int(t.stage) - 1, 0, 2)]

static func _current_food_demand(g) -> float:
	# Reserve promises concern the current population, including migrants which
	# arrived at the preceding boundary. Reuse the simulation's kitchen allocation.
	var covered = {}
	var groups:Array = g.s.get("groups", {}).keys()
	groups.sort()
	for gid in groups: covered[gid] = 0.0
	var buildings:Array = g.s.get("projects", {}).keys()
	buildings.sort()
	for bid in buildings:
		var p:Dictionary = g.s.projects[bid]
		var capacity = float(g.compiled.get("config", {}).get(bid, {}).get("kitchen_capacity", 0))
		if capacity <= 0 or str(p.get("status", "")) != "running" or bool(p.get("paused", false)): continue
		for gid in groups:
			var group:Dictionary = g.s.groups[gid]
			if g.Rules.near(g.s, str(bid), str(group.host)) > 1: continue
			var n = minf(capacity, maxf(0, float(group.count) - float(covered[gid])))
			capacity -= n
			covered[gid] += n
	var demand = 0.0
	for gid in groups:
		var group:Dictionary = g.s.groups[gid]
		var n = maxf(0, float(group.get("count", 0)))
		var st:Dictionary = g.compiled.get("stats", {}).get(str(group.get("host", "")), {})
		demand += n * float(st.get("population_food", g.c.balance.get("population_food", 0.18))) * (1.0 - 0.2 * float(covered[gid]) / maxf(0.001, n))
	return demand

static func _population_metrics(g) -> Dictionary:
	var out = {"population":0.0, "housed":0.0, "homeless":0.0, "food_demand":0.0,
		"food":0.0, "medical":0.0, "education":0.0, "commercial":0.0, "entertainment":0.0,
		"health":0.0, "happiness":0.0, "commercial_people":0.0}
	for group in g.s.get("groups", {}).values():
		var n = maxf(0.0, float(group.get("count", 0)))
		out.population += n
		if str(group.get("host", "")) == "homeless": out.homeless += n
		else: out.housed += n
		var st:Dictionary = g.compiled.get("stats", {}).get(str(group.get("host", "")), {})
		out.food_demand += n * float(st.get("population_food", g.c.balance.get("population_food", 0.18)))
		for field in ["food", "medical", "education", "commercial", "entertainment"]:
			out[field] += n * clampf(float(group.get("living", {}).get(field, 0)), 0, 1)
		out.health += n * float(group.get("health", 0))
		out.happiness += n * float(group.get("happiness", 0))
	out.commercial_people = float(g.s.get("audit", {}).get("commercial_service_people", out.commercial))
	out.food_demand = _current_food_demand(g)
	for field in ["food", "medical", "education", "commercial", "entertainment", "health", "happiness"]:
		out[field] /= maxf(0.000001, float(out.population))
	return out

static func _metrics(g) -> Dictionary:
	var out = _population_metrics(g)
	out.currency = float(g.s.get("currency", 0))
	out.energy = float(g.s.get("metrics", {}).get("energy_ratio", 0))
	var hours = maxf(1e-15, float(g.s.get("audit", {}).get("hours", 0)))
	var resources:Dictionary = g.s.get("audit", {}).get("resources", {})
	out.goods_rate = float(resources.get("goods", {}).get("produced", 0)) / hours
	out.food_rate = float(resources.get("food", {}).get("produced", 0)) / hours
	out.food_self_sufficiency = float(out.food_rate) / maxf(0.000001, float(g.s.get("audit", {}).get("population_food_demand_rate", out.food_demand)))
	out.food_stock_months = g.inventory_total("food") / maxf(0.000001, float(out.food_demand))
	out.food_surplus = float(out.food_rate) - float(out.food_demand)
	out.vacancies = maxf(0, float(g.s.get("metrics", {}).get("housing", 0)) - float(out.housed))
	out.industrial_labor = 0.0
	out.industrial_usage = 0.0
	out.commercial_usage = 0.0
	var weights = {"industrial":0.0, "commercial":0.0}
	var effective_labor = {}
	for link in g.s.get("metrics", {}).get("employment_links", []):
		var target = str(link.get("target", ""))
		effective_labor[target] = float(effective_labor.get(target, 0)) + maxf(0.0, float(link.get("effective", 0)))
	for id in g.playable_ids():
		var p:Dictionary = g.s.projects[id]
		var st:Dictionary = g.compiled.get("stats", {}).get(id, {})
		var base:Dictionary = g.c.projects.get(str(p.get("def", "")), {}).get("stats", {})
		var industrial = "building.industrial" in p.get("traits", [])
		var commercial = "building.commercial" in p.get("traits", []) or "use.commercial" in p.get("labels", [])
		if industrial:
			var weight = maxf(0.0, float(base.get("output.material", 0)) + float(base.get("output.goods", 0)) + float(base.get("output.energy", 0)))
			weights.industrial += weight
			if p.get("status", "") == "running" and not p.get("paused", false):
				out.industrial_usage += weight * float(p.get("usage", 0))
				# Effective workers, not headcount or a simple building mean.
				out.industrial_labor += float(effective_labor.get(str(id), 0))
		if commercial:
			var weight = maxf(0.0, float(base.get("service.commercial", 0)))
			if weight <= 0: weight = maxf(0.0, float(st.get("service.commercial", 0)))
			weights.commercial += weight
			if p.get("status", "") == "running" and not p.get("paused", false): out.commercial_usage += weight * float(p.get("usage", 0))
	out.industrial_usage /= maxf(0.000001, float(weights.industrial))
	out.commercial_usage /= maxf(0.000001, float(weights.commercial))
	return out

static func _met(value:float, goal:Dictionary) -> bool:
	if goal.has("min") and value + 0.000001 < float(goal.min): return false
	if goal.has("max") and value - 0.000001 > float(goal.max): return false
	return true

static func _state_conditions(stage:Dictionary, metrics:Dictionary) -> bool:
	for goal in stage.get("goals", []):
		if str(goal.get("kind", "state")) != "state": continue
		if not _met(float(metrics.get(str(goal.metric), 0)), goal): return false
	return true

static func _reward(g, source:String, theme_id:String, kind:String, custom_ids:Array=[]) -> Dictionary:
	var ids = custom_ids if not custom_ids.is_empty() else _definition(theme_id).get("bonus_ids", [])
	return g.Bonus.draw(g, source, [theme_id], {"kind":kind, "bonus_ids":ids})

static func _finish_if_ready(g, metrics:Dictionary) -> void:
	var t:Dictionary = g.s.block.theme
	if str(t.id) == "" or bool(t.completed): return
	var stage = _stage(g)
	if float(t.steady_seconds) + 0.0001 < float(stage.get("hold_months", 0)) * MONTH_SECONDS: return
	for goal in stage.get("goals", []):
		var value = float(metrics.get(str(goal.metric), 0)) if str(goal.kind) == "state" else float(t.counters.get(str(goal.metric), 0))
		if not _met(value, goal): return
	var milestone = str(t.id) + ":M" + str(t.stage)
	if not milestone in t.milestone_ids:
		t.milestone_ids.append(milestone)
		_reward(g, "theme:" + milestone, str(t.id), "copy" if int(t.stage) == 1 else "research")
		_record(g, "milestone", {"id":milestone})
		g.log_event("theme_milestone", {"id":milestone}, str(_definition(str(t.id)).name) + "·" + str(stage.name) + "目标达成。")
	if int(t.stage) >= 3:
		t.completed = true
		g.log_event("theme_completed", {"id":t.id}, str(_definition(str(t.id)).name) + "建设目标达成，地区发展有了新的方向。")
	else:
		t.stage = int(t.stage) + 1
		t.steady_seconds = 0.0
		t.counters = {}
		t.trade_cursor = g.s.block.get("trade_history", []).size()
		t.started_seconds = _now(g)
	_pause(g, "发展主题阶段完成，请查看奖励与下一目标。")

static func tick(g, local_seconds:float) -> void:
	if local_seconds <= 0 or bool(g.get("preview_mode")): return
	ensure(g)
	var t:Dictionary = g.s.block.theme
	var tick_id = int(g.s.get("simulation_tick", 0))
	if tick_id <= int(t.get("last_tick", -1)): return
	t.last_tick = tick_id
	var metrics = _metrics(g)
	var previous_metrics:Dictionary = t.last_metrics
	t.last_metrics = metrics
	var history:Array = g.s.block.get("trade_history", [])
	var cursor = clampi(int(t.trade_cursor), 0, history.size())
	var trades:Array = history.slice(cursor)
	t.trade_cursor = history.size()
	if str(t.id) != "" and not bool(t.completed):
		for row in trades: t.counters.trade = float(t.counters.get("trade", 0)) + maxf(0, float(row.get("amount", 0)))
		var stage = _stage(g)
		var conditions = _state_conditions(stage, metrics)
		# The endpoint may include new arrivals or a completed building. That
		# cannot retroactively satisfy the whole slice which preceded it.
		var held_before = _state_conditions(stage, previous_metrics)
		t.steady_seconds = float(t.steady_seconds) + local_seconds if conditions and held_before else 0.0
		if conditions:
			t.counters.commercial_person_months = float(t.counters.get("commercial_person_months", 0)) + float(metrics.commercial_people) * local_seconds / MONTH_SECONDS
		_finish_if_ready(g, metrics)
	_update_events(g, metrics, local_seconds)
	_arrivals(g)

static func _audit_delivery(g, resource:String, amount:float, source:String) -> bool:
	if amount <= 0 or not g.withdraw_stock(resource, amount): return false
	if not g.s.block.get("theme_deliveries") is Array: g.s.block.theme_deliveries = []
	g.s.block.theme_deliveries.append({"source":source, "resource":resource, "amount":amount, "local_seconds":_now(g)})
	var receipt:Dictionary = g.s.get("audit", {}).get("resources", {}).get(resource, {})
	if not receipt.is_empty():
		receipt.commission_delivered = float(receipt.get("commission_delivered", 0)) + amount
		receipt.after = g.inventory_total(resource)
		receipt.error = float(receipt.get("before", 0)) + float(receipt.get("produced", 0)) + float(receipt.get("imported", 0)) - float(receipt.get("consumed", 0)) - float(receipt.get("lost", 0)) - float(receipt.get("exported", 0)) - float(receipt.commission_delivered) - float(receipt.after)
	return true

static func deliver(g, resource:String="", amount:float=-1.0, event_id:String="") -> String:
	ensure(g)
	if g.s.get("phase", "") != "play": return "当前不能交付。"
	if not is_finite(amount) or (amount <= 0 and amount != -1.0): return "交付数量无效。"
	if event_id != "":
		var error = _deliver_event(g, event_id, amount)
		if error == "": g.revision += 1
		return error
	var t:Dictionary = g.s.block.theme
	if str(t.id) == "" or bool(t.completed): return "当前没有待交付的主题目标。"
	var goal:Dictionary = {}
	for item in _stage(g).get("goals", []):
		if str(item.get("kind", "")) != "delivery": continue
		if resource == "" or str(item.metric) == "delivery_" + resource:
			if float(t.counters.get(str(item.metric), 0)) + 0.000001 >= float(item.min): continue
			goal = item
			break
	if goal.is_empty(): return "该资源没有未完成的主动交付目标。"
	resource = str(goal.metric).trim_prefix("delivery_")
	var remaining = maxf(0, float(goal.min) - float(t.counters.get(str(goal.metric), 0)))
	var available = g.inventory_total(resource)
	if resource == "food" and str(t.id) == "agricultural": available = maxf(0.0, available - 3.0 * float(_population_metrics(g).food_demand))
	var take = minf(remaining, available)
	if amount > 0: take = minf(take, amount)
	if take <= 0.000001: return "可交付库存不足；农业主题必须保留3月口粮。" if resource == "food" else "没有可交付库存。"
	if not _audit_delivery(g, resource, take, "theme:" + str(t.id) + ":M" + str(t.stage)): return "库存不足，未交付。"
	t.counters[str(goal.metric)] = float(t.counters.get(str(goal.metric), 0)) + take
	_finish_if_ready(g, _metrics(g))
	g.revision += 1
	return ""

static func _has_production_slot(g) -> bool:
	for p in g.s.projects.values():
		for slot in p.get("slots", {}):
			if str(p.slots[slot]) == "" and str(p.get("slot_types", {}).get(slot, slot)) == "Production": return true
	return false

static func _has_service(g) -> bool:
	for id in g.playable_ids():
		if g.s.projects[id].get("status", "") != "running": continue
		var st:Dictionary = g.compiled.get("stats", {}).get(id, {})
		for kind in ["medical", "education", "commercial", "entertainment"]:
			if float(st.get("service." + kind, 0)) > 0: return true
	return false

static func _storage_ratio(g) -> float:
	var current = 0.0
	var cap = 0.0
	for id in g.s.projects:
		var st:Dictionary = g.compiled.get("stats", {}).get(id, {})
		for r in ["food", "material", "goods"]:
			current += float(g.s.projects[id].get("inventory", {}).get(r, 0))
			cap += maxf(0.0, float(st.get("storage." + r, 0)))
	return current / maxf(0.000001, cap)

static func _eligible(g, def:Dictionary, metrics:Dictionary) -> bool:
	var t:Dictionary = g.s.block.theme
	if str(def.theme) != str(t.id) or str(def.id) in t.event_ids: return false
	var trigger:Dictionary = def.get("trigger", {})
	for key in trigger:
		var expected = trigger[key]
		if str(key).ends_with("_min"):
			var value = _storage_ratio(g) if key == "storage_ratio_min" else float(metrics.get(str(key).trim_suffix("_min"), 0))
			if key == "food_surplus_min" and value <= 0: return false
			if value + 0.000001 < float(expected): return false
		elif str(key).ends_with("_max"):
			if float(metrics.get(str(key).trim_suffix("_max"), 0)) >= float(expected): return false
		else:
			match str(key):
				"milestone":
					if not (str(t.id) + ":M" + str(int(expected))) in t.milestone_ids: return false
				"production_slot":
					if not _has_production_slot(g): return false
				"service_building":
					if not _has_service(g): return false
				"freight":
					if g.Market._total_capacity(g) <= 0: return false
				"has_trade":
					if g.s.block.get("trade_history", []).is_empty(): return false
				"actual_overflow":
					var spill = 0.0
					for n in g.s.get("metrics", {}).get("spilled", {}).values(): spill += float(n)
					if spill <= 0.000001: return false
				"research":
					if g.Bonus.research_candidates(g, _definition(str(t.id)).get("bonus_ids", []), []).is_empty(): return false
	return true

static func on_major_cycle(g, index:int) -> void:
	ensure(g)
	var t:Dictionary = g.s.block.theme
	if index <= int(t.last_major_cycle): return
	t.last_major_cycle = index
	if str(t.id) == "": return
	for e in t.events:
		if str(e.get("status", "")) in ["offered", "active"]: return
	var candidates:Array = []
	var weights:Array = []
	var metrics = _metrics(g)
	for def in _events():
		if _eligible(g, def, metrics):
			candidates.append(def)
			weights.append(float(def.get("weight", 1)))
	if not candidates.is_empty():
		var picked:Dictionary = Rng.pick_weighted(g.s, "theme_events", candidates, weights)
		t.event_ids.append(str(picked.id))
		t.events.append({"id":str(picked.id), "status":"offered", "revealed_seconds":_now(g), "theme":t.id})
		_pause(g, "新的主题事件等待选择。")
	if not bool(t.completed) and Rng.next(g.s, "theme_events") < _token_chance(g):
		var id = "reforge_opportunity:" + str(index)
		t.events.append({"id":id, "status":"offered", "revealed_seconds":_now(g), "theme":t.id, "token":true})
		t.event_ids.append(id)
		_pause(g, "发现稀有委任重铸权；使用仍需%.2f DT。" % _reforge_cost(g))

static func _find_event(g, id:String) -> Dictionary:
	for event in g.s.block.theme.events:
		if str(event.id) == id: return event
	return {}

static func _event_choice_definition(def:Dictionary, choice_id:String) -> Dictionary:
	for choice in def.get("choices", []):
		if str(choice.id) != choice_id: continue
		var selected = def.duplicate(true)
		selected.erase("choices")
		for key in choice:
			if key not in ["id", "label"]:
				selected[key] = choice[key].duplicate(true) if choice[key] is Dictionary or choice[key] is Array else choice[key]
		selected.name = str(def.name) + " · " + str(choice.label)
		selected.choice_id = choice_id
		selected.choice_label = choice.label
		return selected
	return {}

static func _effective_event_definition(event:Dictionary) -> Dictionary:
	var def = _event_definition(str(event.get("id", "")))
	return def if str(event.get("choice_id", "")) == "" else _event_choice_definition(def, str(event.choice_id))

static func _event_cost_text(def:Dictionary) -> String:
	var parts:Array[String] = []
	var description = str(def.get("description", ""))
	if description != "": parts.append(description)
	var money = float(def.get("currency", 0))
	var deposit = float(def.get("deposit", 0))
	if money + deposit > 0:
		parts.append("即时支付 %.1f 通货%s。" % [money + deposit, ("（其中保证金 %.1f，成功退还）" % deposit) if deposit > 0 else ""])
	for resource in def.get("resources", {}):
		parts.append("扣除 %.1f %s。" % [float(def.resources[resource]), str(Catalog.RESOURCE_NAMES.get(resource, resource))])
	if float(def.get("deadline_months", 0)) > 0: parts.append("接受后限期 %.1f 来生月。" % float(def.deadline_months))
	if float(def.get("cash_reward", 0)) > 0: parts.append("完成奖励 %.1f 通货。" % float(def.cash_reward))
	if str(def.get("reward_kind", "")) != "": parts.append("完成后领取一项蓝图研究。" if str(def.reward_kind) == "research" else "完成后领取一张方案副本。")
	return " ".join(parts)

static func _event_unavailable_reason(g, def:Dictionary) -> String:
	if str(def.get("reward_kind", "")) != "research": return ""
	for reward in g.Bonus.pending(g):
		if str(reward.get("kind", "")) == "research": return "先领取已有蓝图研究奖励，再决定这条付费研究分支。"
	if g.Bonus.research_candidates(g, _event_pool(def)).is_empty():
		return "尚未拥有这条分支可升级的方案；先取得副本、选择其他分支或免费婉拒。"
	return ""

static func choose_event(g, event_id:String, choice_id:String) -> String:
	ensure(g)
	if g.s.get("phase", "") != "play": return "当前不能处理事件。"
	var event = _find_event(g, event_id)
	if event.is_empty() or str(event.get("status", "")) != "offered": return "该事件已经处理或不存在。"
	if choice_id == "decline":
		event.status = "declined"
		_record(g, "event_declined", {"id":event_id})
		g.revision += 1
		return ""
	if bool(event.get("token", false)):
		if choice_id != "accept": return "未知事件选择。"
		g.s.block.theme.reforge_tokens = int(g.s.block.theme.reforge_tokens) + 1
		event.status = "completed"
		_record(g, "reforge_token", {"id":event_id, "use_dt_cost":_reforge_cost(g)})
		g.revision += 1
		return ""
	var def = _event_definition(event_id)
	if def.is_empty(): return "事件定义不存在。"
	var branched = not def.get("choices", []).is_empty()
	if branched: def = _event_choice_definition(def, choice_id)
	elif choice_id != "accept": return "未知事件选择。"
	if def.is_empty(): return "未知事件选择。"
	if branched:
		var unavailable = _event_unavailable_reason(g, def)
		if unavailable != "": return unavailable
	var baseline = float(_metrics(g).get(str(def.get("metric", "")), 0))
	if def.has("improve") and baseline + float(def.improve) > 1.000001:
		return "当前覆盖率已较高，无法再提高合同要求的百分点；可以免费婉拒。"
	var cost = float(def.get("currency", 0)) + float(def.get("deposit", 0))
	if float(g.s.currency) + 0.000001 < cost: return "通货不足，尚未接受事件。"
	for r in def.get("resources", {}):
		if g.inventory_total(str(r)) + 0.000001 < float(def.resources[r]): return "所需库存不足，尚未接受事件。"
	# Costs are committed only after all resource and currency checks pass.
	g.s.currency = float(g.s.currency) - cost
	for r in def.get("resources", {}): _audit_delivery(g, str(r), float(def.resources[r]), "event:" + event_id)
	if branched: event.choice_id = choice_id
	event.status = "active"
	event.accepted_seconds = _now(g)
	event.deposit = float(def.get("deposit", 0))
	event.deadline_at = _now(g) + float(def.get("deadline_months", 0)) * MONTH_SECONDS if float(def.get("deadline_months", 0)) > 0 else -1.0
	event.trade_cursor = g.s.block.get("trade_history", []).size()
	event.progress = 0.0
	event.baseline = baseline
	var receipt = {"id":event_id, "currency_cost":cost}
	if branched:
		receipt.choice_id = choice_id
		receipt.choice_label = def.choice_label
		receipt.resources = def.get("resources", {}).duplicate(true)
	_record(g, "event_accepted", receipt)
	if def.has("arrival_people"):
		var due = (floorf(_now(g) / MONTH_SECONDS) + 1.0) * MONTH_SECONDS
		g.s.block.theme.arrivals.append({"event_id":event_id, "people":float(def.arrival_people), "due_seconds":due, "arrived":false})
		_complete_event(g, event, def)
	elif not def.has("metric") and not def.has("delivery_resource") and not def.has("trade_amount") and not def.has("person_months"):
		_complete_event(g, event, def)
	g.revision += 1
	return ""

static func _event_pool(def:Dictionary) -> Array:
	if def.has("bonus_ids"): return def.bonus_ids
	var ids:Array = []
	for theme_id in def.get("reward_themes", [str(def.theme)]):
		for id in _definition(str(theme_id)).get("bonus_ids", []):
			if not id in ids: ids.append(id)
	return ids

static func _complete_event(g, event:Dictionary, def:Dictionary) -> void:
	if str(event.status) != "active": return
	event.status = "completed"
	event.finished_seconds = _now(g)
	g.s.currency = float(g.s.currency) + float(event.get("deposit", 0)) + float(def.get("cash_reward", 0))
	event.deposit = 0.0
	if def.has("reward_kind"):
		_reward(g, "theme_event:" + str(event.id), str(def.theme), str(def.reward_kind), _event_pool(def))
	_record(g, "event_completed", {"id":event.id})
	g.log_event("theme_event_completed", {"id":event.id}, str(def.name) + "已完成。")

static func _update_events(g, metrics:Dictionary, local_seconds:float) -> void:
	for event in g.s.block.theme.events:
		if str(event.get("status", "")) != "active": continue
		var def = _effective_event_definition(event)
		if def.is_empty(): continue
		var deadline = float(event.get("deadline_at", -1))
		# Exact boundary counts; later ticks cannot retroactively satisfy a contract.
		if deadline >= 0 and _now(g) > deadline + 0.0001:
			_fail_event(g, event)
			continue
		if def.has("trade_amount"):
			var history:Array = g.s.block.get("trade_history", [])
			for row in history.slice(clampi(int(event.get("trade_cursor", 0)), 0, history.size())):
				if def.has("trade_side") and str(row.get("side", "")) != str(def.trade_side): continue
				if def.has("trade_resource") and str(row.get("resource", "")) != str(def.trade_resource): continue
				event.progress = float(event.progress) + maxf(0, float(row.get("amount", 0)))
			event.trade_cursor = history.size()
		if def.has("person_months"): event.progress = float(event.progress) + float(metrics.commercial_people) * local_seconds / MONTH_SECONDS
		var done = false
		if def.has("metric"): done = float(metrics.get(str(def.metric), 0)) + 0.000001 >= float(event.baseline) + float(def.get("improve", 0))
		if def.has("trade_amount"): done = float(event.progress) + 0.000001 >= float(def.trade_amount)
		if def.has("person_months"): done = float(event.progress) + 0.000001 >= float(def.person_months)
		if def.has("delivery_amount"): done = float(event.progress) + 0.000001 >= float(def.delivery_amount)
		if done:
			_complete_event(g, event, def)
		elif deadline >= 0 and _now(g) + 0.0001 >= deadline:
			_fail_event(g, event)

static func _fail_event(g, event:Dictionary) -> void:
	event.status = "failed"
	event.finished_seconds = _now(g)
	event.deposit = 0.0
	_record(g, "event_failed", {"id":event.id, "reason":"超过本地时间期限；仅失去该机会与已支付保证金。"})

static func _deliver_event(g, id:String, amount:float) -> String:
	var event = _find_event(g, id)
	var def = _effective_event_definition(event)
	if event.is_empty() or str(event.get("status", "")) != "active" or not def.has("delivery_resource"): return "没有可主动交付的事件合同。"
	if float(event.get("deadline_at", -1)) >= 0 and _now(g) > float(event.deadline_at) + 0.0001: return "该合同已经超过本地时间期限。"
	var resource = str(def.delivery_resource)
	var available = g.inventory_total(resource)
	if resource == "food": available -= float(def.get("reserve_months", 0)) * float(_population_metrics(g).food_demand)
	var take = minf(maxf(0, available), maxf(0, float(def.delivery_amount) - float(event.progress)))
	if amount > 0: take = minf(take, amount)
	if take <= 0.000001: return "库存不足或交付后口粮不满足合同要求。"
	if not _audit_delivery(g, resource, take, "event:" + id): return "库存不足，未交付。"
	event.progress = float(event.progress) + take
	if float(event.progress) + 0.000001 >= float(def.delivery_amount): _complete_event(g, event, def)
	return ""

static func _arrivals(g) -> void:
	var arrived = false
	for row in g.s.block.theme.arrivals:
		if bool(row.get("arrived", false)) or _now(g) + 0.0001 < float(row.due_seconds): continue
		g.Population.create(g, "homeless", float(row.people))
		row.arrived = true
		arrived = true
		_record(g, "event_arrival", {"id":row.event_id, "people":row.people})
	if arrived:
		g.Population.rehouse(g)
		g.refresh_metrics()

static func next_boundary_seconds(g) -> float:
	# Pure lookup; this is called before committing an economic slice.
	var t:Dictionary = g.s.get("block", {}).get("theme", {})
	if t.is_empty(): return INF
	var result = INF
	if str(t.get("id", "")) != "" and not bool(t.get("completed", false)):
		var stage = _stage(g)
		var remaining = float(stage.get("hold_months", 0)) * MONTH_SECONDS - float(t.get("steady_seconds", 0))
		if remaining > 0.0001: result = minf(result, remaining)
	for event in t.get("events", []):
		if str(event.get("status", "")) != "active": continue
		var remaining = float(event.get("deadline_at", -1)) - _now(g)
		if remaining > 0.0001: result = minf(result, remaining)
	for row in t.get("arrivals", []):
		if bool(row.get("arrived", false)): continue
		var remaining = float(row.due_seconds) - _now(g)
		if remaining > 0.0001: result = minf(result, remaining)
	return result

static func _event_view(g, event:Dictionary) -> Dictionary:
	var token = bool(event.get("token", false))
	var def = _effective_event_definition(event)
	var row = {"id":str(event.id), "name":"委任重铸权" if token else str(def.get("name", event.id)),
		"status":str(event.get("status", "")), "description":("获得一次重铸权；使用仍需%.2f DT，完成主题后不可更换。" % _reforge_cost(g)) if token else str(def.get("description", "")),
		"deadline_seconds":maxf(0, float(event.get("deadline_at", -1)) - _now(g)) if float(event.get("deadline_at", -1)) >= 0 else -1.0,
		"progress":float(event.get("progress", 0)), "target":float(def.get("delivery_amount", def.get("trade_amount", def.get("person_months", 0)))),
		"choices":[], "delivery_resource":str(def.get("delivery_resource", "")), "delivery_remaining":maxf(0, float(def.get("delivery_amount", 0)) - float(event.get("progress", 0)))}
	if def.has("metric"):
		row.progress = float(_metrics(g).get(str(def.metric), 0))
		row.target = float(event.get("baseline", row.progress)) + float(def.get("improve", 0))
		row.unit = "%"
		row.description += " 目标实际覆盖：%.0f%%。" % (float(row.target) * 100.0)
	if row.status == "offered":
		for choice in def.get("choices", []):
			var selected = _event_choice_definition(def, str(choice.id))
			var reason = _event_unavailable_reason(g, selected)
			row.choices.append({"id":choice.id, "label":choice.label, "cost_text":_event_cost_text(selected) + (" " + reason if reason != "" else ""), "available":reason == "", "reason":reason})
		if row.choices.is_empty(): row.choices.append({"id":"accept", "label":"接受", "cost_text":row.description if token else _event_cost_text(def)})
		row.choices.append({"id":"decline", "label":"婉拒", "cost_text":"不支付代价，不附加惩罚。"})
	row.definition = def.duplicate(true)
	return row

static func _history_view(entry:Dictionary) -> Dictionary:
	var row = entry.duplicate(true)
	var id = str(row.get("id", ""))
	var kind = str(row.get("kind", ""))
	var event = _event_definition(id)
	if str(row.get("choice_id", "")) != "": event = _event_choice_definition(event, str(row.choice_id))
	var parts = id.split(":")
	var theme = _definition(str(parts[0])) if not parts.is_empty() else {}
	row.name = str(event.get("name", theme.get("name", id)))
	if id.begins_with("reforge_opportunity:"): row.name = "委任重铸权"
	if kind == "milestone" and parts.size() > 1:
		var names = {"M1":"立足", "M2":"成形", "M3":"证明"}
		row.name += " · " + str(names.get(parts[1], parts[1]))
	row.status_label = {"selected":"确定发展主题", "reforged":"委任已重铸", "milestone":"阶段达成",
		"event_declined":"已婉拒", "event_accepted":"已接受", "event_completed":"已完成", "event_failed":"逾期未达成",
		"event_arrival":"居民已抵达", "reforge_token":"获得重铸权"}.get(kind, "城市记录")
	row.reason = str(row.get("reason", ""))
	if kind == "reforged":
		row.reason = "从「%s」改向，消耗1份重铸权与%.2f DT；已获得和待领取奖励保留。" % [str(_definition(str(row.get("from", ""))).get("name", "原主题")), float(row.get("dt_cost", 0))]
	elif kind == "event_declined": row.reason = "未支付代价，没有额外惩罚。"
	elif kind == "event_accepted":
		row.reason = str(event.get("description", ""))
		if float(row.get("currency_cost", 0)) > 0: row.reason += " 已支付%.1f通货。" % float(row.currency_cost)
	elif kind == "event_arrival": row.reason = "本地时间约定已到，%.0f位新居民实际抵达。" % float(row.get("people", 0))
	elif kind == "reforge_token": row.reason = "使用仍需%.2f DT；获得重铸权不等于免费换向。" % float(row.get("use_dt_cost", 0))
	return row

static func view(g) -> Dictionary:
	# No ensure call: presentation must be fully read-only, including RNG state.
	var t:Dictionary = g.s.get("block", {}).get("theme", {})
	var id = str(t.get("id", ""))
	var def = _definition(id)
	var row = {"id":id, "name":str(def.get("name", "尚未选择发展主题")), "stage":int(t.get("stage", 1)), "completed":bool(t.get("completed", false)),
		"stage_name":"", "goals":[], "steady_seconds":float(t.get("steady_seconds", 0)), "steady_required_seconds":0.0,
		"reforge_tokens":int(t.get("reforge_tokens", 0)), "reforge_dt_cost":_reforge_cost(g), "reforge_opportunity_chance":_token_chance(g),
		"can_reforge":id != "" and not bool(t.get("completed", false)) and int(t.get("reforge_tokens", 0)) > 0 and float(g.s.get("dt", 0)) >= _reforge_cost(g),
		"choices":[], "events":[], "history":[]}
	for entry in t.get("history", []): row.history.append(_history_view(entry))
	for item in _themes(): row.choices.append({"id":str(item.id), "name":str(item.name), "description":str(item.description)})
	if not def.is_empty():
		var stage:Dictionary = def.stages[clampi(int(row.stage) - 1, 0, 2)]
		row.stage_name = str(stage.name)
		row.steady_required_seconds = float(stage.get("hold_months", 0)) * MONTH_SECONDS
		var metrics = _metrics(g)
		for goal in stage.goals:
			var value = float(metrics.get(str(goal.metric), 0)) if str(goal.kind) == "state" else float(t.get("counters", {}).get(str(goal.metric), 0))
			row.goals.append({"id":str(goal.metric), "label":str(goal.label), "value":value, "target":float(goal.get("min", goal.get("max", 0))), "unit":str(goal.unit), "met":_met(value, goal), "comparison":"max" if goal.has("max") else "min", "kind":str(goal.kind)})
	for event in t.get("events", []):
		if str(event.get("status", "")) in ["offered", "active"]: row.events.append(_event_view(g, event))
	return row
