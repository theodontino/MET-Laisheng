extends RefCounted
## Persistent state for the active pre-alpha Block. No cross-Block archive exists.
const Market = preload("res://core/market040.gd")

static func ensure(g) -> void:
	if not g.s.get("block") is Dictionary: g.s.block = {"id":"block:1"}
	var block:Dictionary = g.s.block
	if not block.has("id"): block.id = "block:1"
	if not block.has("stage"): block.stage = 0
	if not block.get("bonus_wallet") is Dictionary: block.bonus_wallet = {}
	if not block.get("deployed_bonuses") is Dictionary: block.deployed_bonuses = {}
	if not block.get("reward_choices") is Array: block.reward_choices = []
	if not block.get("commissions") is Dictionary: block.commissions = {"active":{},"history":[],"completed":[],"pending_reward":""}
	if not block.get("trade_orders") is Array: block.trade_orders = []
	if not block.get("trade_history") is Array: block.trade_history = []
	if not block.get("trade_ledger") is Array: block.trade_ledger = []
	if not block.get("market_state") is Dictionary: block.market_state = {"resources":{}}
	if not block.has("construction_capacity"):
		block.construction_capacity = float(g.c.v040.get("progression", {}).get("construction_capacity", 4))
	if not block.has("display_name"):
		block.display_name = "河谷登陆点" if int(g.s.get("block_index", -1)) < 0 else "街区 " + str(int(g.s.block_index) + 1)
	if not block.has("naming_rights"): block.naming_rights = false
	if not block.has("naming_reward_granted"): block.naming_reward_granted = false
	g.s.block = block
	Market.ensure(g)

static func grant_naming(g) -> void:
	ensure(g)
	var block:Dictionary = g.s.block
	if bool(g.s.get("city_goal", {}).get("completed", false)) and not bool(block.get("naming_reward_granted", false)):
		block.naming_rights = true
		block.naming_reward_granted = true
		if g.has_method("log_event"): g.log_event("block_naming_right", {}, "地区目标已完成，获得一次命名权。")
	g.s.block = block

static func name_block(g, name:String) -> String:
	ensure(g)
	var value = name.strip_edges()
	var max_length = int(g.c.v040.get("progression", {}).get("name_max_length", 24))
	if value == "" or value.length() > max_length: return "街区名称不能为空，且最多%d个字符。" % max_length
	if not bool(g.s.block.get("naming_rights", false)): return "完成地区目标后才能命名。"
	g.s.block.display_name = value
	g.s.block.naming_rights = false
	if g.has_method("log_event"): g.log_event("block_named", {"name":value}, "街区命名为「" + value + "」。")
	return ""
