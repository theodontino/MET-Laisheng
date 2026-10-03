extends RefCounted
## Active Block, immutable ending eligibility and read-only completed city records.
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
	# Retained call boundary; ordinary goals no longer grant naming in Beta.
	on_dt_zero(g)

static func on_dt_zero(g) -> void:
	ensure(g)
	if float(g.s.dt)!=0.0 or g.s.phase!="play":return
	if g.s.block.get("ending",{}).get("triggered",false):return
	var theme:Dictionary=g.s.block.get("theme",{})
	var eligible=str(theme.get("id","")) if bool(theme.get("completed",false)) else ""
	g.s.block.ending={"triggered":true,"eligible_theme":eligible,"capstone_claimed":false,"archive_id":"","local_elapsed_seconds":float(g.s.get("beta_time",{}).get("local_elapsed_seconds",0)),"theme_at_zero":str(theme.get("id",""))}
	g.s.block.naming_rights=true;g.s.block.naming_reward_granted=true
	g.s.settings.user_paused=true
	if g.s.get("beta_time") is Dictionary:
		g.s.beta_time.mode="normal";g.s.beta_time.pause_reason="dt_zero"
	g.log_event("dt_zero",{"eligible_theme":eligible},"高速发展预算已耗尽。可以为这座城市命名，继续常速经营，或留存记录后独立重开。")

static func capstone_definitions() -> Dictionary:
	var value=JSON.parse_string(FileAccess.get_file_as_string("res://content/beta/capstones.json"))
	return value if value is Dictionary else {}

static func claim_capstone(g) -> String:
	ensure(g)
	var ending:Dictionary=g.s.block.get("ending",{})
	if not ending.get("triggered",false):return "高速发展预算实际耗尽后才能正式挂牌。"
	if ending.get("capstone_claimed",false):return "本区定型建筑已经落成。"
	var theme=str(ending.get("eligible_theme",""))
	var defs=capstone_definitions()
	if not defs.has(theme):return "本次预算耗尽前未完成主题，未获得定型建筑资格。"
	var item:Dictionary=defs[theme].duplicate(true)
	item.source_id=item.id
	# Municipal landmark is a separate visual civic entity, never a new buildable cell.
	# It therefore works on a full map and cannot consume or replace a player's building.
	item.tile_anchor="0,0"
	var cells=g.s.cells.keys();cells.sort()
	if not g.s.cells.has(item.tile_anchor) or "tile.water" in g.s.cells[item.tile_anchor].traits:
		for tile in cells:
			if not "tile.water" in g.s.cells[tile].traits:item.tile_anchor=tile;break
	item.created_local_seconds=float(g.s.beta_time.local_elapsed_seconds)
	g.s.block.capstone=item;g.s.block.function_role=theme
	ending.capstone_claimed=true
	g.refresh_metrics()
	g.log_event("capstone",{"id":item.id,"theme":theme,"name":item.name},str(g.s.block.display_name)+"的"+str(item.name)+"正式启用。")
	return ""

static func view(g) -> Dictionary:
	ensure(g)
	var ending:Dictionary=g.s.block.get("ending",{}).duplicate(true)
	var item:Dictionary=capstone_definitions().get(str(ending.get("eligible_theme","")),{})
	return {"name":str(g.s.block.display_name),"naming_rights":bool(g.s.block.naming_rights),"ending":ending,"capstone":g.s.block.get("capstone",{}).duplicate(true),"eligible_capstone":item,"can_archive":bool(ending.get("triggered",false))}

static func archive(g) -> Dictionary:
	ensure(g)
	if not g.s.block.get("ending",{}).get("triggered",false):return {"ok":false,"error":"本局高速发展预算尚未耗尽。"}
	var id=str(g.s.block.ending.get("archive_id",""))
	if id=="":id=(str(g.s.seed)+":"+str(g.s.started_at)+":"+str(g.s.block.ending.local_elapsed_seconds)).sha256_text().substr(0,24)
	var path="user://city_records/"+id+".json"
	var dir_error=DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("user://city_records"))
	if dir_error!=OK:return {"ok":false,"error":"无法创建城市记录目录。"}
	if FileAccess.file_exists(path):
		var existing=JSON.parse_string(FileAccess.get_file_as_string(path))
		if not existing is Dictionary or not existing.get("read_only",false) or str(existing.get("id",""))!=id or not existing.get("city_state") is Dictionary:
			return {"ok":false,"error":"已有城市记录损坏；已保留旧城，请先备份修复记录。"}
		g.s.block.ending.archive_id=id
		return {"ok":true,"path":path,"record":existing,"existing":true}
	var record={"record_version":1,"read_only":true,"id":id,"build":g.c.data.build,"seed":g.s.seed,"name":g.s.block.display_name,
		"local_elapsed_seconds":g.s.beta_time.local_elapsed_seconds,"active_real_seconds":g.s.beta_time.active_real_seconds,
		"theme":g.s.block.get("theme",{}).duplicate(true),"dt_spent":g.s.totals.dt_used,"population":g.s.metrics.get("population",0),
		"currency":g.s.currency,"capstone":g.s.block.get("capstone",{}).duplicate(true),"blueprint_levels":g.s.block.get("blueprint_levels",{}).duplicate(true),
		"bonus_instances":g.s.bonus_instances.duplicate(true),"rewards":g.s.block.reward_choices.duplicate(true),"history":g.s.log.duplicate(true),"city_state":g.s.duplicate(true)}
	var file=FileAccess.open(path+".tmp",FileAccess.WRITE)
	if file==null:return {"ok":false,"error":"无法写入城市记录，旧城未删除。"}
	file.store_string(JSON.stringify(record,"\t",true,true));file.flush()
	var error=file.get_error();file.close()
	if error!=OK:return {"ok":false,"error":"城市记录写入失败，旧城未删除。"}
	error=DirAccess.rename_absolute(ProjectSettings.globalize_path(path+".tmp"),ProjectSettings.globalize_path(path))
	if error!=OK:return {"ok":false,"error":"无法保存城市记录，旧城未删除。"}
	g.s.block.ending.archive_id=id
	return {"ok":true,"path":path,"record":record,"existing":false}

static func list_archives() -> Array:
	var result=[]
	var dir=DirAccess.open("user://city_records")
	if dir==null:return result
	var files=dir.get_files();files.sort()
	for name in files:
		if not name.ends_with(".json"):continue
		var value=JSON.parse_string(FileAccess.get_file_as_string("user://city_records/"+name))
		if value is Dictionary and value.get("read_only",false):result.append(value)
	return result

static func name_block(g, name:String) -> String:
	ensure(g)
	var value = name.strip_edges()
	var max_length = int(g.c.v040.get("progression", {}).get("name_max_length", 24))
	if value == "" or value.length() > max_length: return "街区名称不能为空，且最多%d个字符。" % max_length
	if not bool(g.s.block.get("naming_rights", false)): return "本局DT实际归零后获得一次命名权。"
	g.s.block.display_name = value
	g.s.block.naming_rights = false
	if g.has_method("log_event"): g.log_event("block_named", {"name":value}, "街区命名为「" + value + "」。")
	return ""
