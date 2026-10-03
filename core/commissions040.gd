extends RefCounted
## Data-driven city commissions. Progress is measured from committed simulation
## metrics and actual market receipts, then rewarded once through Bonus Economy.

static func ensure(g) -> void:
	if not g.s.get("block") is Dictionary: g.s.block = {"id":"block:1"}
	var block:Dictionary = g.s.block
	if not block.get("commissions") is Dictionary:
		block.commissions = {"active":{},"history":[],"completed":[],"pending_reward":""}
	var state:Dictionary = block.commissions
	if not state.get("active") is Dictionary: state.active = {}
	if not state.get("history") is Array: state.history = []
	if not state.get("completed") is Array: state.completed = []
	if not state.has("pending_reward"): state.pending_reward = ""
	g.s.block.commissions = state
	if not block.get("trade_history") is Array: block.trade_history = []

static func _definitions(g) -> Array:
	var value = g.c.v040.get("commissions", [])
	return value if value is Array else []

static func _definition(g, commission_id:String) -> Dictionary:
	for item in _definitions(g):
		if str(item.get("id", "")) == commission_id: return item
	return {}

static func _active(g) -> Dictionary:
	ensure(g)
	return g.s.block.commissions.active

static func _trade_capacity(g) -> float:
	var total = 0.0
	var stats:Dictionary = g.compiled.get("stats", {})
	for id in g.playable_ids():
		var p:Dictionary = g.s.projects[id]
		if p.get("status", "") != "running" or p.get("paused", false): continue
		var capacity = float(stats.get(id, {}).get("trade_capacity", g.c.projects.get(p.get("def", ""), {}).get("stats", {}).get("trade_capacity", 0)))
		if capacity <= 0: continue
		total += capacity * maxf(0.0, float(p.get("usage", 1.0)))
	return total

static func _max_tier(g) -> int:
	var result = 0
	for id in g.playable_ids(): result = maxi(result, int(g.s.projects[id].get("tier", 1)))
	return result

static func _actual_service_ratio(g, kind:String) -> float:
	var delivered = 0.0
	var people = 0.0
	for group in g.s.get("groups", {}).values():
		var count = float(group.get("count", 0))
		people += count
		delivered += count * float(group.get("living", {}).get(kind, 0))
	return delivered / maxf(0.001, people)

static func _food_ratio(g) -> float:
	var delivered = 0.0
	var people = 0.0
	for group in g.s.get("groups", {}).values():
		var count = float(group.get("count", 0))
		people += count
		delivered += count * float(group.get("living", {}).get("food", 1))
	return delivered / maxf(0.001, people)

static func _history_slice(g, active:Dictionary) -> Array:
	var rows:Array = g.s.block.get("trade_history", [])
	var start = clampi(int(active.get("trade_cursor", 0)), 0, rows.size())
	return rows.slice(start)

static func _organization_links(g) -> float:
	var edges = {}
	var projects:Dictionary = g.s.get("projects", {})
	var groups:Dictionary = g.s.get("groups", {})
	var metrics:Dictionary = g.s.get("metrics", {})
	for row in metrics.get("employment_links", []):
		var home = str(groups.get(str(row.get("group", "")), {}).get("host", ""))
		var target = str(row.get("target", ""))
		_add_org_edge(edges, projects, home, target, float(row.get("people", 0)))
	for row in metrics.get("service_links", []):
		var source = str(row.get("source", ""))
		var home = str(row.get("home", ""))
		_add_org_edge(edges, projects, source, home, float(row.get("people", 0)))
	for owner in metrics.get("activity_ledger", {}):
		for row in metrics.activity_ledger[owner]:
			if row.get("kind", "") != "input": continue
			_add_org_edge(edges, projects, str(row.get("source_id", "")), str(owner), float(row.get("rate", 0)))
	return float(edges.size())

static func _add_org_edge(edges:Dictionary, projects:Dictionary, source:String, target:String, amount:float) -> void:
	if amount <= 0.000001 or source == target: return
	if not projects.has(source) or not projects.has(target): return
	var a:Dictionary = projects[source]
	var b:Dictionary = projects[target]
	if a.get("special", false) or b.get("special", false): return
	if str(a.get("operator_org", "")) != str(b.get("operator_org", "")): return
	var left = str(a.get("id", source))
	var right = str(b.get("id", target))
	if left > right:
		var swap = left
		left = right
		right = swap
	edges[left + "|" + right] = true

static func _metric(g, metric:String, active:Dictionary) -> float:
	var m:Dictionary = g.s.get("metrics", {})
	match metric:
		"food_ratio": return _food_ratio(g)
		"medical": return _actual_service_ratio(g, "medical")
		"education": return _actual_service_ratio(g, "education")
		"commercial": return _actual_service_ratio(g, "commercial")
		"entertainment": return _actual_service_ratio(g, "entertainment")
		"tier": return float(_max_tier(g))
		"buildings":
			var count = 0
			for id in g.playable_ids():
				if not g.s.projects[id].get("special", false): count += 1
			return float(count)
		"trade_capacity": return _trade_capacity(g)
		"organization_links": return _organization_links(g)
		"delivery_goods":
			return float(active.get("delivered_goods", 0))
		"adverse_exports":
			var total = 0.0
			for row in _history_slice(g, active):
				if row.get("side", "") != "sell": continue
				var resource = str(row.get("resource", ""))
				var base = float(g.c.v040.get("market", {}).get("resources", {}).get(resource, {}).get("base_price", 0))
				if float(row.get("price", 0)) < base: total += float(row.get("amount", 0))
			return total
		_:
			if m.get(metric) is int or m.get(metric) is float: return float(m[metric])
	return 0.0

static func _trigger_value(g, key:String) -> float:
	match key:
		"tier": return float(_max_tier(g))
		"buildings":
			var count = 0
			for id in g.playable_ids():
				if not g.s.projects[id].get("special", false): count += 1
			return float(count)
		"trade_capacity": return _trade_capacity(g)
		"housed": return float(g.s.get("metrics", {}).get("housed", 0))
		"population": return float(g.s.get("metrics", {}).get("population", 0))
		"hours": return float(g.s.get("local_hours", 0))
		_:
			var value = g.s.get("metrics", {}).get(key, 0)
			return float(value) if value is int or value is float else 0.0

static func _trigger(g, def:Dictionary) -> Dictionary:
	var gap = 0.0
	var near = true
	for key in def.get("trigger", {}):
		var target = float(def.trigger[key])
		var current = _trigger_value(g, str(key))
		var shortfall = maxf(0.0, target - current)
		if shortfall <= 0.000001: continue
		gap += shortfall / maxf(1.0, target)
		var allowance = maxf(1.0, target * 0.25)
		if key == "housed": allowance = 6.0
		if key == "tier" or key == "buildings" or key == "trade_capacity": allowance = maxf(1.0, target - current)
		if shortfall > allowance: near = false
	return {"current":gap <= 0.000001,"near":near,"gap":gap}

static func _prerequisites_met(state:Dictionary, def:Dictionary) -> bool:
	for required in def.get("prerequisites", []):
		if not str(required) in state.get("completed", []): return false
	return true

static func _is_available(g, state:Dictionary, def:Dictionary) -> Dictionary:
	var commission_id = str(def.get("id", ""))
	if commission_id == "": return {}
	if not bool(def.get("repeatable", false)) and commission_id in state.get("completed", []): return {}
	if not _prerequisites_met(state, def): return {}
	var status = _trigger(g, def)
	if not status.current and not status.near: return {}
	var offer = def.duplicate(true)
	offer["current"] = status.current
	offer["near_future"] = not status.current
	offer["trigger_gap"] = status.gap
	return offer

static func available(g) -> Array:
	ensure(g)
	var state:Dictionary = g.s.block.commissions
	if not state.active.is_empty() or str(state.get("pending_reward", "")) != "": return []
	var current:Array = []
	var near:Array = []
	for def in _definitions(g):
		var offer = _is_available(g, state, def)
		if offer.is_empty(): continue
		if offer.current: current.append(offer)
		else: near.append(offer)
	current.sort_custom(func(a,b):
		if float(a.get("weight", 0)) != float(b.get("weight", 0)): return float(a.weight) > float(b.weight)
		return str(a.id) < str(b.id))
	near.sort_custom(func(a,b):
		if float(a.get("trigger_gap", 0)) != float(b.get("trigger_gap", 0)): return float(a.trigger_gap) < float(b.trigger_gap)
		if float(a.get("weight", 0)) != float(b.get("weight", 0)): return float(a.weight) > float(b.weight)
		return str(a.id) < str(b.id))
	var result:Array = []
	var selected_ids:Array = []
	for i in mini(2, current.size()):
		result.append(current[i]);selected_ids.append(str(current[i].id))
	if result.size() < 2:
		for item in near:
			if result.size() >= 2: break
			if str(item.id) in selected_ids: continue
			result.append(item)
			selected_ids.append(str(item.id))
	for item in near:
		if result.size() >= 3: break
		if str(item.id) in selected_ids: continue
		result.append(item)
		selected_ids.append(str(item.id))
	if result.size() < 3:
		for item in current:
			if result.size() >= 3: break
			if str(item.id) in selected_ids: continue
			result.append(item)
			selected_ids.append(str(item.id))
	return result

static func accept(g, commission_id:String) -> String:
	ensure(g)
	if str(g.s.get("phase", "")) != "play": return "当前不能接取委托。"
	var state:Dictionary = g.s.block.commissions
	if not state.active.is_empty(): return "请先完成或放弃当前委托。"
	if str(state.get("pending_reward", "")) != "": return "先领取上一个委托的奖励。"
	var offer:Dictionary = {}
	for row in available(g):
		if str(row.get("id", "")) == commission_id: offer = row; break
	if offer.is_empty(): return "这份委托当前不可接取。"
	var trade_rows:Array = g.s.block.get("trade_history", [])
	var active = {"id":commission_id,"name":str(offer.get("name", commission_id)),"domain":str(offer.get("domain", "")),
		"started_at":float(g.s.get("local_hours", 0)),"started_tick":int(g.s.get("simulation_tick", 0)),
		"deadline_at":-1.0,"goals":offer.get("goals", []).duplicate(true),"goal_progress":{},"progress_now":{},"delivered_goods":0.0,
		"trade_cursor":trade_rows.size(),"status":"active"}
	var deadline = float(offer.get("fail_conditions", {}).get("deadline_hours", 0))
	if deadline > 0: active.deadline_at = float(active.started_at) + deadline
	state.active = active
	state.history.append({"id":commission_id,"name":active.name,"domain":active.domain,"status":"accepted",
		"started_at":active.started_at,"tick":active.started_tick})
	g.s.block.commissions = state
	return ""

static func abandon(g) -> String:
	ensure(g)
	var state:Dictionary = g.s.block.commissions
	if state.active.is_empty(): return "当前没有进行中的委托。"
	var active:Dictionary = state.active
	state.history.append({"id":str(active.get("id", "")),"name":str(active.get("name", "")),"domain":str(active.get("domain", "")),
		"status":"abandoned","started_at":float(active.get("started_at", 0)),"finished_at":float(g.s.get("local_hours", 0)),
		"progress_now":active.get("progress_now", {}).duplicate(true)})
	state.active = {}
	g.s.block.commissions = state
	return ""

static func _goal_met(value:float, goal:Dictionary) -> bool:
	if goal.has("min") and value + 0.000001 < float(goal.min): return false
	if goal.has("max") and value - 0.000001 > float(goal.max): return false
	if goal.has("equals") and absf(value - float(goal.equals)) > 0.000001: return false
	return true

static func _fail_reason(g, active:Dictionary, def:Dictionary) -> String:
	var fail:Dictionary = def.get("fail_conditions", {})
	if fail.has("min_energy_ratio") and float(g.s.get("metrics", {}).get("energy_ratio", 1)) < float(fail.min_energy_ratio):
		return "供电低于委托底线。"
	if float(active.get("deadline_at", -1)) >= 0 and float(g.s.get("local_hours", 0)) > float(active.deadline_at) + 0.000001:
		return "委托时限已过。"
	return ""

static func _finish(g, state:Dictionary, active:Dictionary, def:Dictionary) -> void:
	var commission_id = str(active.get("id", ""))
	var done = {"id":commission_id,"name":str(def.get("name", commission_id)),"domain":str(def.get("domain", "")),
		"status":"completed","started_at":float(active.get("started_at", 0)),"completed_at":float(g.s.get("local_hours", 0)),
		"progress_now":active.get("progress_now", {}).duplicate(true),"goal_progress":active.get("goal_progress", {}).duplicate(true)}
	state.history.append(done)
	if not commission_id in state.completed: state.completed.append(commission_id)
	var ordinal = 0
	for row in state.history:
		if str(row.get("id", "")) == commission_id and row.get("status", "") == "completed": ordinal += 1
	var source = "commission:" + commission_id + ":" + str(ordinal)
	var reward:Dictionary = g.Bonus.draw(g, source, def.get("reward_pool_tags", []))
	state.pending_reward = str(reward.get("reward_id", ""))
	state.active = {}
	if g.has_method("log_event"):
		g.log_event("commission_completed", {"id":commission_id,"reward_id":state.pending_reward}, str(def.get("name", commission_id)) + "已完成，请领取一份方案许可。")

static func tick(g, h:float) -> void:
	if bool(g.get("preview_mode")) or h <= 0: return
	ensure(g)
	var state:Dictionary = g.s.block.commissions
	if state.active.is_empty(): return
	var active:Dictionary = state.active
	var def = _definition(g, str(active.get("id", "")))
	if def.is_empty(): return
	var failure = _fail_reason(g, active, def)
	if failure != "":
		state.history.append({"id":str(active.id),"name":str(active.get("name", "")),"domain":str(active.get("domain", "")),
			"status":"failed","started_at":float(active.get("started_at", 0)),"finished_at":float(g.s.get("local_hours", 0)),"reason":failure,
			"progress_now":active.get("progress_now", {}).duplicate(true)})
		state.active = {}
		g.s.block.commissions = state
		return
	var all_done = true
	for i in active.get("goals", []).size():
		var goal:Dictionary = active.goals[i]
		var key = str(i)
		var value = _metric(g, str(goal.get("metric", "")), active)
		active.progress_now[key] = value
		var met = _goal_met(value, goal)
		var duration = float(goal.get("duration", 0))
		if duration > 0:
			active.goal_progress[key] = float(active.goal_progress.get(key, 0)) + h if met else 0.0
			if float(active.goal_progress[key]) + 0.000001 < duration: all_done = false
		elif not met:
			all_done = false
	if all_done:
		_finish(g, state, active, def)
	else:
		state.active = active
	g.s.block.commissions = state

static func deliver(g) -> String:
	ensure(g)
	var active:Dictionary = g.s.block.commissions.active
	if active.is_empty(): return "当前没有进行中的委托。"
	var target = 0.0
	for goal in active.get("goals", []):
		if str(goal.get("metric", "")) == "delivery_goods": target = maxf(target, float(goal.get("min", 0)))
	if target <= 0: return "当前委托没有主动交付目标。"
	var remaining = maxf(0, target - float(active.get("delivered_goods", 0)))
	if remaining <= 0.000001: return "委托所需制成品已全部交付。"
	var amount = minf(remaining, g.inventory_total("goods"))
	if amount <= 0.000001: return "当前没有可交付的制成品。"
	if not g.withdraw_stock("goods", amount): return "制成品库存不足。"
	active.delivered_goods = float(active.get("delivered_goods", 0)) + amount
	var state:Dictionary = g.s.block.commissions
	state.active = active
	g.s.block.commissions = state
	if not g.s.block.get("commission_deliveries") is Array: g.s.block.commission_deliveries = []
	g.s.block.commission_deliveries.append({"commission_id":str(active.id),"resource":"goods","amount":amount,
		"h":float(g.s.get("local_hours", 0)),"tick":int(g.s.get("simulation_tick", 0))})
	var audit:Dictionary = g.s.get("audit", {})
	var receipt:Dictionary = audit.get("resources", {}).get("goods", {})
	if not receipt.is_empty():
		receipt.commission_delivered = float(receipt.get("commission_delivered", 0)) + amount
		receipt.after = g.inventory_total("goods")
		receipt.error = float(receipt.get("before", 0)) + float(receipt.get("produced", 0)) + float(receipt.get("imported", 0)) - float(receipt.get("consumed", 0)) - float(receipt.get("lost", 0)) - float(receipt.get("exported", 0)) - float(receipt.commission_delivered) - float(receipt.after)
		audit.resources.goods = receipt
		g.s.audit = audit
	if g.has_method("refresh_metrics"): g.refresh_metrics()
	return ""
