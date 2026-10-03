extends RefCounted
## Shared Block prices with capacity-limited trade orders.
## Market receipts are accounted separately from production and operating cashflow.

static func ensure(g) -> void:
	if not g.s.get("block") is Dictionary: g.s.block = {"id":"block:1"}
	var block:Dictionary = g.s.block
	if not block.get("market_state") is Dictionary: block.market_state = {"resources":{}}
	var market_state:Dictionary = block.market_state
	if not market_state.get("resources") is Dictionary:
		var prior = market_state.duplicate(true)
		market_state = {"resources":{}}
		for key in prior:
			if key in ["resources", "last_tick"]: continue
			if prior[key] is Dictionary: market_state.resources[key] = prior[key]
	var state:Dictionary = market_state.resources
	var configured:Dictionary = g.c.v040.get("market", {}).get("resources", {})
	for resource in configured:
		if not state.get(resource) is Dictionary:
			state[resource] = configured[resource].duplicate(true)
		else:
			for key in configured[resource]:
				if not state[resource].has(key): state[resource][key] = configured[resource][key]
	if not block.get("trade_orders") is Array: block.trade_orders = []
	if not block.get("trade_history") is Array: block.trade_history = []
	if not block.get("trade_ledger") is Array: block.trade_ledger = []
	market_state.resources = state
	block.market_state = market_state
	g.s.block = block

static func quote(g, resource:String) -> Dictionary:
	var item:Dictionary = g.s.get("block", {}).get("market_state", {}).get("resources", {}).get(resource, {})
	if item.is_empty(): return {}
	var base = float(item.get("base_price", 0))
	var depth = maxf(0.000001, float(item.get("market_depth", 1)))
	var pressure = float(item.get("pressure", 0))
	var mid = base + float(item.get("amplitude", 0)) * tanh(pressure / depth)
	var spread = maxf(0, float(item.get("spread", 0)))
	var fee = maxf(0, float(item.get("fee", 0)))
	var buy = mid + spread * 0.5 + fee
	var sell = mid - spread * 0.5 - fee
	if buy <= sell: buy = sell + 0.000001
	return {"resource":resource,"base_price":base,"pressure":pressure,"market_depth":depth,"mid":mid,
		"spread":spread,"fee":fee,"buy":buy,"sell":sell,"buy_price":buy,"sell_price":sell}

static func _stats(g, project_id:String) -> Dictionary:
	var p:Dictionary = g.s.projects.get(project_id, {})
	var compiled:Dictionary = g.get("compiled") if g.get("compiled") is Dictionary else {}
	var stats:Dictionary = compiled.get("stats", {}).get(project_id, {})
	if stats.is_empty(): stats = g.c.projects.get(str(p.get("def", "")), {}).get("stats", {})
	return stats

static func _freight_sources(g) -> Array:
	var result:Array = []
	if not g.s.get("projects") is Dictionary: return result
	var ids:Array = g.playable_ids()
	ids.sort()
	for id in ids:
		var p:Dictionary = g.s.projects[id]
		if str(p.get("status", "")) != "running" or bool(p.get("paused", false)): continue
		var stats = _stats(g, str(id))
		var capacity = maxf(0, float(stats.get("trade_capacity", 0)))
		var usage = clampf(float(p.get("usage", 1)), 0, 1)
		var available = capacity * usage
		if available <= 0.000001: continue
		result.append({"id":str(id),"capacity":capacity,"usage":usage,"available":available})
	return result

static func _total_capacity(g) -> float:
	var total = 0.0
	for row in _freight_sources(g): total += float(row.available)
	return total

static func order(g, resource:String, side:String, amount:float, limit_price:float) -> String:
	ensure(g)
	if str(g.s.get("phase", "")) != "play": return "当前不能下贸易订单。"
	if resource not in ["food", "material", "goods"]: return "当前市场不交易这种资源。"
	if side not in ["buy", "sell"]: return "请选择买入或卖出。"
	if not is_finite(amount) or amount <= 0: return "订单数量必须大于零。"
	if not is_finite(limit_price): return "限价无效。"
	if _total_capacity(g) <= 0.000001: return "需要有正在运行的货运站。"
	var id = g.uid("T")
	var quote_now = quote(g, resource)
	var row = {"id":id,"resource":resource,"side":side,"amount":amount,"remaining":amount,
		"limit_price":limit_price,"created_tick":int(g.s.get("simulation_tick", 0)),"sequence":int(id.trim_prefix("T")),
		"created_at":float(g.s.get("local_hours", 0)),"status":"open","last_attempt_tick":-1,
		"quote_at_order":quote_now}
	g.s.block.trade_orders.append(row)
	return ""

static func cancel(g, order_id:String) -> String:
	ensure(g)
	for i in g.s.block.trade_orders.size():
		var row:Dictionary = g.s.block.trade_orders[i]
		if str(row.get("id", "")) != order_id: continue
		if row.get("status", "") in ["filled", "cancelled"]: return "这笔订单已经结束。"
		row.status = "cancelled"
		row.cancelled_at = float(g.s.get("local_hours", 0))
		g.s.block.trade_orders[i] = row
		return ""
	return "找不到这笔订单。"

static func _storage_cap(g, project_id:String, resource:String) -> float:
	var stats = _stats(g, project_id)
	var cap = maxf(0, float(stats.get("storage." + resource, 0)))
	if project_id == "municipal":
		var options:Dictionary = g.compiled.get("options", {}) if g.compiled is Dictionary else {}
		cap *= float(options.get("municipal_storage", 1))
	cap = maxf(cap, float(g.s.projects[project_id].get("retained_storage", {}).get(resource, 0)))
	return cap

static func _buy_room(g, resource:String) -> float:
	var room = 0.0
	for id in g.s.projects:
		room += maxf(0, _storage_cap(g, str(id), resource) - float(g.s.projects[id].get("inventory", {}).get(resource, 0)))
	return room

static func _deposit(g, resource:String, amount:float) -> float:
	var remaining = amount
	var ids:Array = g.s.projects.keys()
	ids.sort()
	for id in ids:
		var p:Dictionary = g.s.projects[id]
		var inventory:Dictionary = p.get("inventory", {})
		var current = float(inventory.get(resource, 0))
		var room = maxf(0, _storage_cap(g, str(id), resource) - current)
		var add = minf(room, remaining)
		if add <= 0: continue
		inventory[resource] = current + add
		p.inventory = inventory
		g.s.projects[id] = p
		remaining -= add
		if remaining <= 0.000001: break
	return amount - remaining

static func _consume_capacity(sources:Array, amount:float) -> Dictionary:
	var remaining = amount
	var used = []
	for source in sources:
		var take = minf(remaining, float(source.available))
		if take <= 0.000001: continue
		source.available = float(source.available) - take
		used.append({"project_id":source.id,"amount":take})
		remaining -= take
		if remaining <= 0.000001: break
	return {"amount":amount - remaining,"sources":used}

static func _orders_in_stable_order(orders:Array) -> Array:
	var result = orders.duplicate(true)
	result.sort_custom(func(a,b):
		var aseq = int(a.get("sequence", 0)); var bseq = int(b.get("sequence", 0))
		if aseq != bseq: return aseq < bseq
		return str(a.get("id", "")) < str(b.get("id", "")))
	return result

static func _audit_trade(g, resource:String, side:String, amount:float, cash_delta:float, h:float) -> void:
	if not g.s.get("audit", {}).get("resources", {}).get(resource) is Dictionary: return
	var audit:Dictionary = g.s.audit
	var receipt:Dictionary = audit.resources[resource]
	if not receipt.has("imported"): receipt.imported = 0.0
	if not receipt.has("exported"): receipt.exported = 0.0
	if not receipt.has("commission_delivered"): receipt.commission_delivered = 0.0
	if side == "buy": receipt.imported = float(receipt.imported) + amount
	else: receipt.exported = float(receipt.exported) + amount
	receipt.after = g.inventory_total(resource)
	receipt.error = float(receipt.get("before", 0)) + float(receipt.get("produced", 0)) + float(receipt.imported) - float(receipt.get("consumed", 0)) - float(receipt.get("lost", 0)) - float(receipt.exported) - float(receipt.commission_delivered) - float(receipt.after)
	audit.resources[resource] = receipt
	g.s.audit = audit
	if g.s.get("metrics", {}).get("flows") is Dictionary:
		var flow = g.s.metrics.flows
		flow[resource] = (float(receipt.after) - float(receipt.get("before", receipt.after))) / maxf(0.000001, h)
		g.s.metrics.flows = flow
	var market_cash = float(g.s.metrics.get("market_cash_delta", 0)) + cash_delta
	g.s.metrics.market_cash_delta = market_cash

static func tick(g, h:float) -> void:
	if h <= 0: return
	ensure(g)
	var block:Dictionary = g.s.block
	var market_state:Dictionary = block.market_state
	var market:Dictionary = market_state.resources
	# Revert old pressure once, then use one fixed quote snapshot for every order
	# in this tick. Net trade pressure only affects the next quote.
	for resource in market:
		var state:Dictionary = market[resource]
		var half_life = maxf(0.000001, float(state.get("reversion_half_life", 4)))
		state.pressure = float(state.get("pressure", 0)) * exp(-h / half_life)
		market[resource] = state
	market_state.resources = market
	block.market_state = market_state
	g.s.block = block
	var quotes = {}
	for resource in market: quotes[resource] = quote(g, str(resource))
	var sources = _freight_sources(g)
	var total_capacity = 0.0
	for source in sources:
		source.available = float(source.available) * h
		total_capacity += float(source.available)
	var records:Array = []
	var per_resource_pressure = {}
	var ledger = {"tick":int(g.s.get("simulation_tick", 0)),"h":float(g.s.get("local_hours", 0)),"hours":h,
		"bought":{},"sold":{},"currency_delta":0.0,"capacity":total_capacity,"used_capacity":0.0,"trades":[]}
	for order_row in _orders_in_stable_order(g.s.block.trade_orders):
		var order_id = str(order_row.get("id", ""))
		var order_index = -1
		for i in g.s.block.trade_orders.size():
			if str(g.s.block.trade_orders[i].get("id", "")) == order_id: order_index = i; break
		if order_index < 0: continue
		var live:Dictionary = g.s.block.trade_orders[order_index]
		if live.get("status", "") not in ["open", "partial"]: continue
		var resource = str(live.get("resource", ""))
		if not quotes.has(resource): continue
		var side = str(live.get("side", ""))
		var quote_row:Dictionary = quotes[resource]
		var unit_price = float(quote_row.buy_price if side == "buy" else quote_row.sell_price)
		var crosses = unit_price <= float(live.get("limit_price", 0)) + 0.000001 if side == "buy" else unit_price + 0.000001 >= float(live.get("limit_price", 0))
		live.last_attempt_tick = int(g.s.get("simulation_tick", 0))
		if not crosses:
			g.s.block.trade_orders[order_index] = live
			continue
		var remaining_capacity = 0.0
		for source in sources: remaining_capacity += float(source.available)
		var amount = minf(float(live.get("remaining", 0)), remaining_capacity)
		if side == "buy": amount = minf(amount, _buy_room(g, resource))
		else: amount = minf(amount, g.inventory_total(resource))
		if side == "buy" and unit_price > 0: amount = minf(amount, maxf(0, float(g.s.currency)) / unit_price)
		if side == "sell" and unit_price < 0: amount = minf(amount, maxf(0, float(g.s.currency)) / -unit_price)
		amount = maxf(0, amount)
		if amount <= 0.000001:
			g.s.block.trade_orders[order_index] = live
			continue
		var allocation = _consume_capacity(sources, amount)
		amount = minf(amount, float(allocation.amount))
		if amount <= 0.000001:
			g.s.block.trade_orders[order_index] = live
			continue
		if side == "buy":
			amount = _deposit(g, resource, amount)
			if amount <= 0.000001:
				g.s.block.trade_orders[order_index] = live
				continue
		else:
			if not g.withdraw_stock(resource, amount):
				g.s.block.trade_orders[order_index] = live
				continue
		var cash_delta = -amount * unit_price if side == "buy" else amount * unit_price
		g.s.currency = snappedf(float(g.s.currency) + cash_delta, 0.000001)
		var trade = {"id":g.uid("X"),"order_id":order_id,"resource":resource,"side":side,"amount":amount,
			"price":unit_price,"unit_price":unit_price,"mid_price":float(quote_row.mid),"limit_price":float(live.limit_price),
			"currency_delta":cash_delta,"tick":int(g.s.get("simulation_tick", 0)),"h":float(g.s.get("local_hours", 0)),
			"capacity_sources":allocation.sources}
		records.append(trade)
		ledger.trades.append(trade)
		ledger.used_capacity = float(ledger.used_capacity) + amount
		ledger.currency_delta = float(ledger.currency_delta) + cash_delta
		if side == "buy":
			ledger.bought[resource] = float(ledger.bought.get(resource, 0)) + amount
			per_resource_pressure[resource] = float(per_resource_pressure.get(resource, 0)) + amount
		else:
			ledger.sold[resource] = float(ledger.sold.get(resource, 0)) + amount
			per_resource_pressure[resource] = float(per_resource_pressure.get(resource, 0)) - amount
		live.remaining = maxf(0, float(live.get("remaining", 0)) - amount)
		live.filled = float(live.get("filled", 0)) + amount
		live.status = "filled" if float(live.remaining) <= 0.000001 else "partial"
		live.updated_at = float(g.s.get("local_hours", 0))
		g.s.block.trade_orders[order_index] = live
		_audit_trade(g, resource, side, amount, cash_delta, h)
		if not g.s.block.trade_history is Array: g.s.block.trade_history = []
		g.s.block.trade_history.append(trade)
	for resource in per_resource_pressure:
		var state:Dictionary = g.s.block.market_state.resources[resource]
		state.pressure = float(state.get("pressure", 0)) + float(per_resource_pressure[resource])
		g.s.block.market_state.resources[resource] = state
	for resource in g.s.block.market_state.resources:
		var quote_row = quote(g, str(resource))
		var state:Dictionary = g.s.block.market_state.resources[resource]
		state.mid = quote_row.mid;state.buy_price = quote_row.buy_price;state.sell_price = quote_row.sell_price
		g.s.block.market_state.resources[resource] = state
	if not records.is_empty():
		g.s.block.trade_ledger.append(ledger)
		if g.s.block.trade_ledger.size() > 1200: g.s.block.trade_ledger.pop_front()
		if g.s.get("metrics", {}).get("market_trades") is Array: g.s.metrics.market_trades.append_array(records)
		else: g.s.metrics.market_trades = records.duplicate(true)
		if g.s.get("metrics", {}).get("market_capacity") is Dictionary:
			g.s.metrics.market_capacity.last = total_capacity
			g.s.metrics.market_capacity.used = float(ledger.used_capacity)
		else: g.s.metrics.market_capacity = {"last":total_capacity,"used":float(ledger.used_capacity)}
		var journal:Dictionary = g.s.get("civic032", {})
		if not journal.is_empty():
			journal.transactions.append({"h":float(g.s.get("local_hours", 0)),"tick":int(g.s.get("simulation_tick", 0)),
				"kind":"trade","label":"外部市场贸易","cash":float(ledger.currency_delta),"dt":0.0,"balance":float(g.s.currency)})
			while journal.transactions.size() > 300: journal.transactions.pop_front()
			journal.cash_anchor = float(journal.get("cash_anchor", 0)) + float(ledger.currency_delta)
			g.s.civic032 = journal
