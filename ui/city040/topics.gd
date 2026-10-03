extends RefCounted
## Read-only topic -> subtopic -> detail pages for the City Desk.
const W = preload("res://ui/widgets.gd")
const C = preload("res://ui/components031/components.gd")
const M = preload("res://ui/city032/models.gd")
const P = preload("res://ui/presentation.gd")

const RESOURCE_NAMES = {"food":"粮食", "material":"材料", "goods":"制成品", "energy":"电力"}
const NEED_NAMES = {"food":"粮食", "material":"材料", "goods":"制成品", "energy":"电力", "medical":"医疗", "education":"教育", "commercial":"购物", "entertainment":"休闲"}
const POLICY_SLOTS = [
	["Tax", "税制"],
	["Labor", "就业分配"],
	["Environment", "环境治理"],
	["Procurement", "物资采购"],
	["Land", "用地制度"]
]

class FinanceHistoryChart extends Control:
	const ThemeColors=preload("res://ui/widgets.gd")
	var samples:Array=[]
	func set_data(rows:Array) -> void:
		samples=rows.duplicate(true);queue_redraw()
	func _draw() -> void:
		var font=get_theme_default_font()
		if font==null:return
		var plot=Rect2(Vector2(62,38),Vector2(maxf(30,size.x-82),maxf(32,size.y-72)))
		var high=1.;var low=0.
		for row in samples:
			high=maxf(high,maxf(float(row.get("income",0)),float(row.get("upkeep",0))))
			low=minf(low,minf(float(row.get("income",0)),float(row.get("upkeep",0))))
		var span=maxf(1.,high-low)
		for i in range(4):
			var y=plot.position.y+plot.size.y*i/3.
			draw_line(Vector2(plot.position.x,y),Vector2(plot.end.x,y),ThemeColors.SOFT,1.)
			draw_string(font,Vector2(2,y+5),"%.1f"%(high-span*i/3.),HORIZONTAL_ALIGNMENT_LEFT,-1,int(13*ThemeColors.text_scale),ThemeColors.MUTED)
		draw_line(Vector2(plot.position.x,16),Vector2(plot.position.x+18,16),ThemeColors.GREEN,3.)
		draw_string(font,Vector2(plot.position.x+25,21),"收入",HORIZONTAL_ALIGNMENT_LEFT,-1,int(13*ThemeColors.text_scale),ThemeColors.TEXT)
		draw_line(Vector2(plot.position.x+90,16),Vector2(plot.position.x+108,16),ThemeColors.GOLD,3.)
		draw_string(font,Vector2(plot.position.x+115,21),"支出",HORIZONTAL_ALIGNMENT_LEFT,-1,int(13*ThemeColors.text_scale),ThemeColors.TEXT)
		if samples.size()<2:
			draw_string(font,plot.position+Vector2(10,plot.size.y*.55),"结算后开始绘制历史，不补造旧数据",HORIZONTAL_ALIGNMENT_LEFT,plot.size.x,int(14*ThemeColors.text_scale),ThemeColors.MUTED);return
		var first=float(samples[0].get("h",0));var last=float(samples[-1].get("h",0));var duration=maxf(.001,last-first)
		for key in ["income","upkeep"]:
			var points=PackedVector2Array()
			for row in samples:
				points.append(plot.position+Vector2((float(row.get("h",0))-first)/duration*plot.size.x,(high-float(row.get(key,0)))/span*plot.size.y))
			draw_polyline(points,ThemeColors.GREEN if key=="income" else ThemeColors.GOLD,2.5,true)
		draw_string(font,Vector2(plot.position.x,plot.end.y+24),"%.1fh"%first,HORIZONTAL_ALIGNMENT_LEFT,-1,int(13*ThemeColors.text_scale),ThemeColors.MUTED)
		draw_string(font,Vector2(plot.end.x-70,plot.end.y+24),"%.1fh"%last,HORIZONTAL_ALIGNMENT_RIGHT,70,int(13*ThemeColors.text_scale),ThemeColors.MUTED)

static func _group(parent:Node,title:String="",bg:Color=W.PANEL) -> VBoxContainer:
	var content=C.group(parent,title,bg)
	var panel=content.get_parent()
	if panel is PanelContainer:
		panel.add_theme_stylebox_override("panel",W.box(bg,W.SOFT,10))
		var inner=content as VBoxContainer;inner.add_theme_constant_override("separation",8)
	return content

static func render_finance(d) -> void:
	var model:Dictionary = M.budget(d.app.game, d.app.shot)
	d.title.text = "预算与收支"
	d.cost.text = "预算 %.1f 通货" % model.balance
	if d.subtopic == "":
		d.subtitle.text = "当前余额与持续收支。"
		_finance_summary(d, model)
	elif d.subtopic == "income" or d.subtopic == "expense":
		if d.detail_id == "": _finance_category(d, model, d.subtopic)
		else: _finance_sources(d, model, d.subtopic, d.detail_id)
	elif d.subtopic == "history":
		if d.detail_id == "": _finance_history(d, model)
		else: _finance_history_detail(d, model, d.detail_id)
	else:
		d.subtopic = ""
		render_finance(d)

static func _finance_summary(d, model:Dictionary) -> void:
	var grid = _grid(d)
	d._metric(grid, "持续收入", "+%.2f /h" % model.income, "当前结算", W.GREEN)
	d._metric(grid, "持续支出", "−%.2f /h" % model.expense, "当前结算", W.GOLD)
	d._metric(grid, "收支平衡", "%+.2f /h" % model.net, "不含一次性收支", W.RED if model.net < 0 else W.GREEN)
	var chart = FinanceHistoryChart.new()
	chart.set_data(model.history)
	chart.custom_minimum_size.y = 144 * W.text_scale
	d.body.add_child(chart)
	var actions = W.hbox(d.body, 8)
	_nav(actions, d, "收入", "income")
	_nav(actions, d, "支出", "expense")
	_nav(actions, d, "历史", "history")

static func _finance_category(d, model:Dictionary, category:String) -> void:
	var incoming = category == "income"
	var rows:Array = model.income_rows if incoming else model.expense_rows
	var amount = float(model.income if incoming else model.expense)
	d.title.text = "收入" if incoming else "支出"
	d.subtitle.text = "按当前经营结算 %.2f 通货/小时。" % amount
	d._metric(d.body, "当前合计", ("+" if incoming else "−") + "%.2f /h" % amount, "逐项明细在下一层。", W.GREEN if incoming else W.GOLD)
	var description = _group(d.body, "本期结构")
	d._row(description, "记录项目", "%d 项" % rows.size())
	d._row(description, "核对来源", "打开明细", func(): d.push_section(category, "sources"))
	var policy = ""
	if incoming:
		var mode = str(d.app.game.compiled.get("options", {}).get("tax_mode", ""))
		policy = {"head":"人头税", "production":"生产税", "trade":"消费税"}.get(mode, "当前税制")
	else:
		policy = "建筑与居民维护"
	var card = _group(d.body, "主要依据")
	card.add_child(W.label(policy, 18, W.TEXT))
	if incoming:
		card.add_child(W.button("查看地区政策", func(): d.navigate("policies")))

static func _finance_sources(d, model:Dictionary, category:String, detail:String) -> void:
	var incoming = category == "income"
	var rows:Array = model.income_rows if incoming else model.expense_rows
	d.title.text = "收入来源" if incoming else "支出来源"
	d.subtitle.text = "当前周期真实记录 · 点名称可定位建筑。"
	if detail.begins_with("source:"):
		var source_id = detail.trim_prefix("source:")
		var matches:Array = []
		for row in rows:
			if str(row.get("source_id", "")) == source_id: matches.append(row)
		if matches.is_empty():
			d.body.add_child(W.label("这项来源已不在当前结算中。", 16, W.MUTED))
			return
		var total = 0.0
		for row in matches: total += float(row.get("rate", 0))
		d._metric(d.body, "来源合计", ("+" if incoming else "−") + "%.2f /h" % total, d.app.game.project_name(source_id), W.GREEN if incoming else W.GOLD)
		for row in matches:
			d._row(d.body, str(row.get("name", "经营记录")), "%.2f /h" % float(row.get("rate", 0)))
		return
	var table = _group(d.body, "逐项明细")
	if rows.is_empty():
		table.add_child(W.label("当前周期没有记录。", 16, W.MUTED))
		return
	var ordered = rows.duplicate(true)
	ordered.sort_custom(func(a, b):
		if float(a.get("rate", 0)) != float(b.get("rate", 0)): return float(a.rate) > float(b.rate)
		return str(a.get("source_id", "")) < str(b.get("source_id", "")))
	for row in ordered:
		var item:Dictionary = row
		var source_id = str(item.get("source_id", ""))
		var action:Callable = func(): d.push_section(category, "source:" + source_id)
		d._row(table, str(item.get("name", item.get("source", "经营记录"))), ("+" if incoming else "−") + "%.2f /h" % float(item.get("rate", 0)), action)
		if str(item.get("source", "")) != "": table.add_child(W.label(str(item.source), 13, W.MUTED))

static func _finance_history(d, model:Dictionary) -> void:
	d.title.text = "预算历史"
	d.subtitle.text = "经营周期与一次性操作分别查看。"
	var rows:Array = model.history
	var latest = "尚无完整结算"
	if not rows.is_empty(): latest = "最近记录 %.1fh · 余额 %.1f" % [float(rows[-1].get("h", 0)), float(rows[-1].get("balance", 0))]
	d._metric(d.body, "周期记录", "%d 条" % rows.size(), latest, W.TEXT)
	var entries = W.vbox(d.body, 8)
	_nav(entries, d, "经营周期", "history", "cycles")
	_nav(entries, d, "一次性收支", "history", "transactions")

static func _finance_history_detail(d, model:Dictionary, detail:String) -> void:
	var rows:Array = model.history
	if detail == "cycles":
		d.title.text = "经营周期记录"
		d.subtitle.text = "最近60个已完成周期。"
		var table:Array = []
		for i in range(rows.size() - 1, maxi(-1, rows.size() - 61), -1):
			var row:Dictionary = rows[i]
			table.append(["%.1fh" % float(row.get("h", 0)), "+%.2f" % float(row.get("income", 0)), "−%.2f" % float(row.get("upkeep", 0)), "%+.2f" % float(row.get("net", 0)), "%.2f" % float(row.get("balance", 0))])
		if table.is_empty(): d.body.add_child(W.label("推进一个完整周期后会出现记录。", 16, W.MUTED))
		else: d._table(d.body, ["本地时间", "收入/h", "支出/h", "净收支/h", "期末余额"], table)
	elif detail == "transactions":
		d.title.text = "一次性收支"
		d.subtitle.text = "最近60项政策、建设、奖励与贸易记录。"
		var journal:Array = model.transactions
		var table:Array = []
		for i in range(journal.size() - 1, maxi(-1, journal.size() - 61), -1):
			var row:Dictionary = journal[i]
			table.append(["%.1fh" % float(row.get("h", 0)), str(row.get("label", row.get("kind", "操作"))).left(36), "%+.2f" % float(row.get("cash", 0)), "%+.2f DT" % float(row.get("dt", 0)), "%.2f" % float(row.get("balance", 0))])
		if table.is_empty(): d.body.add_child(W.label("当前还没有一次性收支记录。", 16, W.MUTED))
		else: d._table(d.body, ["时间", "操作", "通货", "DT", "余额"], table)
	else:
		d.title.text = "导出记录"
		d.subtitle.text = "导出当前完整收支记录。"
		d.body.add_child(W.label("导出会包含存档中已有的结算与操作记录。", 15, W.MUTED))
		d.body.add_child(W.primary(W.button("导出完整记录", func(): d.app.export_file("record"))))

static func render_resource(d) -> void:
	var resource_id = str(d.arg)
	if not RESOURCE_NAMES.has(resource_id): resource_id = "food"
	var model:Dictionary = M.resource(d.app.game, d.app.shot, resource_id)
	d.title.text = str(model.get("name", RESOURCE_NAMES[resource_id]))
	d.cost.text = ""
	if d.subtopic == "":
		d.subtitle.text = str(model.get("stamp", ""))
		_resource_summary(d, model, resource_id)
	elif d.subtopic == "supply" or d.subtopic == "consumption" or d.subtopic == "storage":
		if d.detail_id == "": _resource_subtopic(d, model, resource_id, d.subtopic)
		else: _resource_detail(d, model, resource_id, d.subtopic)
	else:
		d.subtopic = ""
		render_resource(d)

static func _resource_summary(d, model:Dictionary, resource_id:String) -> void:
	var grid = _grid(d)
	if resource_id == "energy":
		var supply = float(model.get("supply", 0))
		var need = float(model.get("need", 0))
		d._metric(grid, "供给", "%.2f" % supply, "当前周期", W.GREEN)
		d._metric(grid, "需求", "%.2f" % need, "当前周期", W.TEXT)
		d._metric(grid, "满足率", "%.0f%%" % (100 * float(model.get("ratio", 1))), "实际供电", W.GREEN if float(model.get("ratio", 1)) >= .98 else W.GOLD)
		d._metric(grid, "电网差额", "%+.2f" % (supply - need), "电力不默认入库", W.TEXT)
	else:
		d._metric(grid, "当前库存", "%.1f" % float(model.get("stock", 0)), "跨周期储备", W.TEXT)
		d._metric(grid, "实际生产", "%.2f/h" % float(model.get("produced", 0)), "当前周期", W.GREEN)
		d._metric(grid, "实际消耗", "%.2f/h" % float(model.get("consumed", 0)), "当前周期", W.GOLD)
		d._metric(grid, "库存净变", "%+.2f/h" % float(model.get("net", 0)), str(model.get("status", "")), W.GREEN if float(model.get("net", 0)) >= 0 else W.GOLD)
	var actions = W.hbox(d.body, 8)
	_nav(actions, d, "供给", "supply")
	_nav(actions, d, "消费", "consumption")
	_nav(actions, d, "仓储", "storage")

static func _resource_subtopic(d, model:Dictionary, resource_id:String, section:String) -> void:
	var heading = {"supply":"供给", "consumption":"消费", "storage":"仓储"}[section]
	d.title.text = str(model.get("name", RESOURCE_NAMES[resource_id])) + " · " + heading
	if section == "supply":
		var value = float(model.get("supply", 0)) if resource_id == "energy" else float(model.get("produced", 0))
		d.subtitle.text = "本期供给 %.2f%s。" % [value, "" if resource_id == "energy" else "/h"]
		d._metric(d.body, "有效供给", "%.2f%s" % [value, "" if resource_id == "energy" else "/h"], "逐建筑来源在详情中。", W.GREEN)
		_nav(d.body, d, "查看供给来源", section, "records")
	elif section == "consumption":
		var value = float(model.get("need", 0)) if resource_id == "energy" else float(model.get("consumed", 0))
		d.subtitle.text = "需求 %.2f%s · 实际消耗 %.2f%s。" % [float(model.get("need", value)), "/h" if resource_id != "energy" else "", value, "/h" if resource_id != "energy" else ""]
		d._metric(d.body, "实际使用", "%.2f%s" % [value, "" if resource_id == "energy" else "/h"], "逐项去向在详情中。", W.GOLD)
		_nav(d.body, d, "查看使用去向", section, "records")
	else:
		d.subtitle.text = "库存位置与可用容量。" if resource_id != "energy" else "电力按周期结算，不作为普通仓库存放。"
		if resource_id == "energy":
			d.body.add_child(W.label("只有明确配置储能容量的建筑会保存剩余电力。", 15, W.MUTED))
		else:
			var total = 0.0
			for store in model.get("stores", []): total += float(store.get("capacity", 0))
			d._metric(d.body, "当前库存", "%.1f" % float(model.get("stock", 0)), "可用仓储容量 %.1f" % total, W.TEXT)
		_nav(d.body, d, "查看库存位置", section, "records")

static func _resource_detail(d, model:Dictionary, resource_id:String, section:String) -> void:
	var heading = {"supply":"供给来源", "consumption":"实际去向", "storage":"库存位置"}[section]
	d.title.text = str(model.get("name", RESOURCE_NAMES[resource_id])) + " · " + heading
	d.subtitle.text = "当前快照中的实际记录。"
	var field = {"supply":"sources", "consumption":"sinks", "storage":"stores"}[section]
	var rows:Array = model.get(field, [])
	if rows.is_empty():
		d.body.add_child(W.label("当前周期没有可列出的记录。", 16, W.MUTED))
		return
	var group = _group(d.body, heading)
	for row in rows:
		var item:Dictionary = row
		var id = str(item.get("id", ""))
		var action:Callable = func(): d.locate(id)
		if section == "storage":
			d._row(group, str(item.get("name", id)), "%.1f / %.1f" % [float(item.get("stock", 0)), float(item.get("capacity", 0))], action)
			group.add_child(W.label("可接收外部溢出" if item.get("external", false) else "本地产物仓", 13, W.MUTED))
		else:
			var unit = "" if resource_id == "energy" else "/h"
			d._row(group, str(item.get("name", id)), "%.2f%s" % [float(item.get("rate", 0)), unit], action)
			if str(item.get("note", "")) != "": group.add_child(W.label(str(item.note), 13, W.MUTED))
			if section == "supply" and (float(item.get("kept", 0)) > 0 or float(item.get("lost", 0)) > 0):
				group.add_child(W.label("入库 %.2f/h · 损耗 %.2f/h" % [float(item.get("kept", 0)), float(item.get("lost", 0))], 13, W.GOLD if float(item.get("lost", 0)) > .001 else W.MUTED))

static func render_population(d) -> void:
	var p:Dictionary = M.population(d.app.game, d.app.shot)
	d.title.text = "居民"
	d.cost.text = ""
	if d.subtopic == "":
		d.subtitle.text = str(p.stamp)
		_population_summary(d, p)
	elif d.subtopic == "housing":
		if d.detail_id == "": _population_housing(d, p)
		else: _population_homes(d, p)
	elif d.subtopic == "employment":
		if d.detail_id == "": _population_employment(d, p)
		else: _population_jobs(d, p)
	elif d.subtopic == "needs":
		if d.detail_id == "": _population_needs(d, p)
		else: _population_need_detail(d, p, d.detail_id)
	else:
		d.subtopic = ""
		render_population(d)

static func _population_summary(d, p:Dictionary) -> void:
	var grid = _grid(d)
	d._metric(grid, "人口 / 住房", "%.0f / %.0f" % [p.count, p.housing], "已安置 %.0f · 待安置 %.0f" % [p.housed, p.homeless], W.TEXT)
	d._metric(grid, "就业", "%.0f / %.0f" % [p.employed, p.jobs], "岗位已填 / 岗位总数", W.GREEN)
	var growth = "尚无实际周期" if p.population_rate == null else "%+.2f 人/h" % float(p.population_rate)
	d._metric(grid, "人口变化", growth, "最近一次实际周期", W.TEXT)
	d._metric(grid, "幸福", "%.0f / 100" % float(p.happiness), "", W.TEXT)
	d._metric(grid, "健康", "%.0f / 100" % float(p.health), "", W.GREEN)
	d._metric(grid, "需求覆盖", "%.0f%%" % (100 * float(p.coverage)), "已启用需求", W.TEXT)
	var actions = W.hbox(d.body, 8)
	_nav(actions, d, "居住", "housing")
	_nav(actions, d, "就业", "employment")
	_nav(actions, d, "需求", "needs")

static func _population_housing(d, p:Dictionary) -> void:
	d.title.text = "人口 · 居住"
	d.subtitle.text = "住房容量与实际安置。"
	var grid = _grid(d)
	d._metric(grid, "住房位置", "%.1f" % float(p.housing), "全城容量", W.TEXT)
	d._metric(grid, "已安置", "%.1f" % float(p.housed), "", W.GREEN)
	d._metric(grid, "待安置", "%.1f" % float(p.homeless), "", W.GOLD if float(p.homeless) > .1 else W.TEXT)
	d._metric(grid, "空余位置", "%.1f" % maxf(0, float(p.housing) - float(p.housed)), "", W.TEXT)
	_nav(d.body, d, "查看住区", "housing", "homes")

static func _population_homes(d, p:Dictionary) -> void:
	d.title.text = "居住 · 住区明细"
	d.subtitle.text = "按实际居住位置合并展示。"
	var homes:Dictionary = {}
	for row in p.rows:
		var id = str(row.get("home", "homeless"))
		if not homes.has(id): homes[id] = {"count":0.0, "employed":0.0, "health":0.0, "happiness":0.0, "education":0.0, "coverage":0.0}
		var entry:Dictionary = homes[id]
		var count = float(row.get("count", 0))
		entry.count += count
		entry.employed += float(row.get("employed", 0))
		entry.health += float(row.get("health", 0)) * count
		entry.happiness += float(row.get("happiness", 0)) * count
		entry.education += float(row.get("education", 0)) * count
		entry.coverage += float(row.get("mean_coverage", 0)) * count
	var ids:Array = homes.keys()
	ids.sort()
	if ids.is_empty():
		d.body.add_child(W.label("当前没有住户记录。", 16, W.MUTED))
		return
	for raw_id in ids:
		var id = str(raw_id)
		var entry:Dictionary = homes[id]
		var n = maxf(.000001, float(entry.count))
		var name = "待安置" if id == "homeless" else d.app.game.project_name(id)
		var card = _group(d.body, name)
		var action:Callable = func(): d.locate(id)
		d._row(card, "居民", "%.1f 人" % float(entry.count), action)
		card.add_child(W.label("就业 %.1f · 幸福 %.0f · 健康 %.0f · 教育 %.0f · 需求 %.0f%%" % [float(entry.employed), float(entry.happiness) / n, float(entry.health) / n, float(entry.education) / n, 100 * float(entry.coverage) / n], 14, W.MUTED))

static func _population_employment(d, p:Dictionary) -> void:
	d.title.text = "人口 · 就业"
	d.subtitle.text = "已分配岗位与仍可用的岗位。"
	var grid = _grid(d)
	d._metric(grid, "已就业", "%.1f" % float(p.employed), "", W.GREEN)
	d._metric(grid, "岗位总数", "%.1f" % float(p.jobs), "", W.TEXT)
	d._metric(grid, "待就业", "%.1f" % float(p.unemployed), "", W.GOLD if float(p.unemployed) > .1 else W.TEXT)
	_nav(d.body, d, "查看岗位来源", "employment", "sources")

static func _population_jobs(d, p:Dictionary) -> void:
	d.title.text = "就业 · 岗位来源"
	d.subtitle.text = "按实际分配统计；点名称可定位建筑。"
	var filled:Dictionary = {}
	for row in p.rows:
		for raw_id in row.get("jobs", {}):
			var id = str(raw_id)
			filled[id] = float(filled.get(id, 0)) + float(row.jobs[raw_id])
	var g = d.app.game
	var project_ids:Array = g.playable_ids()
	project_ids.sort()
	var found = false
	for raw_id in project_ids:
		var id = str(raw_id)
		if not g.s.projects.has(id) or g.s.projects[id].get("special", false): continue
		var stats:Dictionary = g.compiled.get("stats", {}).get(id, {})
		var capacity = float(stats.get("jobs", 0))
		var employed = float(filled.get(id, 0))
		if capacity <= .0001 and employed <= .0001: continue
		found = true
		var action:Callable = func(): d.locate(id)
		d._row(d.body, g.project_name(id), "%.1f / %.1f" % [employed, capacity], action)
	if not found: d.body.add_child(W.label("当前没有开放岗位。", 16, W.MUTED))

static func _population_needs(d, p:Dictionary) -> void:
	d.title.text = "人口 · 需求"
	d.subtitle.text = "覆盖结果按人口加权；点击一项查看实际服务或物资。"
	var grid = _grid(d)
	for raw_key in M.RES + M.SERVICES:
		var key = str(raw_key)
		var ratio = float(p.get("coverage_kinds", {}).get(key, 0))
		var active = key in p.get("active_kinds", [])
		var value = "%.0f%%" % (100 * ratio) if active else "未启用"
		var color = W.GREEN if ratio >= .98 else (W.GOLD if ratio < .75 else W.TEXT)
		var button = W.button(NEED_NAMES.get(key, key) + "  ·  " + value, func(): d.push_section("needs", key))
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		grid.add_child(button)

static func _population_need_detail(d, p:Dictionary, need:String) -> void:
	var name = NEED_NAMES.get(need, need)
	d.title.text = "需求 · " + name
	var active = need in p.get("active_kinds", [])
	var ratio = float(p.get("coverage_kinds", {}).get(need, 0))
	d.subtitle.text = "已启用需求的实际覆盖。" if active else "当前未计入生活需求。"
	var grid = _grid(d)
	d._metric(grid, "实际覆盖", "%.0f%%" % (100 * ratio) if active else "未启用", "居民人数加权", W.GREEN if ratio >= .98 else W.GOLD)
	var g = d.app.game
	var shot:Dictionary = d.app.shot
	var sources:Dictionary = {}
	var units = "人"
	if need in M.SERVICES:
		for link in shot.get("metrics", {}).get("service_links", []):
			if str(link.get("kind", "")) != need: continue
			var source = str(link.get("source", ""))
			sources[source] = float(sources.get(source, 0)) + float(link.get("people", 0))
	elif need == "energy":
		units = "供电"
		for source in shot.get("metrics", {}).get("energy_sources", {}):
			var id = str(source)
			sources[id] = float(shot.metrics.energy_sources[source])
	else:
		units = "本周期"
		for receipt in shot.get("metrics", {}).get("allocation", []):
			if str(receipt.get("resource", "")) != need or not str(receipt.get("consumer", "")).begins_with("pop:"): continue
			var source = str(receipt.get("source", ""))
			sources[source] = float(sources.get(source, 0)) + float(receipt.get("used", 0))
	var section = _group(d.body, "实际来源")
	if sources.is_empty():
		section.add_child(W.label("当前周期没有实际供给记录。", 15, W.MUTED))
	else:
		var ids:Array = sources.keys()
		ids.sort()
		for raw_id in ids:
			var id = str(raw_id)
			var label = "市政电网" if id == "municipal" else g.project_name(id)
			var action:Callable = func(): d.locate(id)
			d._row(section, label, "%.1f %s" % [float(sources[id]), units], action)
	var households = _group(d.body, "各住区覆盖")
	for row in p.rows:
		var host = str(row.get("home", "homeless"))
		var label = "待安置" if host == "homeless" else g.project_name(host)
		var item_ratio = float(row.get("coverage", {}).get(need, 0))
		d._row(households, label + " · %.1f人" % float(row.get("count", 0)), "%.0f%%" % (100 * item_ratio))

static func render_time(d) -> void:
	var g = d.app.game
	d.title.text = "时间"
	d.cost.text = ""
	if d.subtopic == "": _time_summary(d)
	elif d.subtopic == "advance": _time_advance(d)
	elif d.subtopic == "rules":
		if d.detail_id == "": _time_rules(d)
		else: _time_rule_detail(d, d.detail_id)
	else:
		d.subtopic = ""
		render_time(d)

static func _time_summary(d) -> void:
	var g = d.app.game
	var dt = Time.get_datetime_dict_from_system()
	d.subtitle.text = ""
	var grid = _grid(d)
	d._metric(grid, "系统时间", "%02d:%02d" % [dt.hour, dt.minute], "%04d-%02d-%02d" % [dt.year, dt.month, dt.day], W.TEXT)
	d._metric(grid, "城镇时间", "%.1fh" % float(g.s.get("local_hours", 0)), "已结算时间", W.TEXT)
	d._metric(grid, "可用 DT", "%.2f / %.0f" % [float(g.s.get("dt", 0)), float(g.c.balance.get("dt_cap", 0))], "累计支付 %.2f DT" % float(g.s.get("totals", {}).get("dt_used", 0)), W.TEXT)
	var paused = bool(g.s.get("settings", {}).get("user_paused", false))
	d._metric(grid, "运行状态", "手动暂停" if paused else "运行中", "查看页面时本地时间自动暂停", W.GOLD if paused else W.GREEN)
	var next = g.next_checkpoint()
	d._metric(grid, "下一变化", str(next.get("name", "周期结算")), "约 %.1fh" % float(next.get("hours", 0)), W.TEXT)
	var actions = W.hbox(d.body, 8)
	_nav(actions, d, "精确推进", "advance")
	_nav(actions, d, "时间规则", "rules")
	var pause_label = "继续城市" if paused else "手动暂停"
	d.body.add_child(W.button(pause_label, func(): g.Civic.ensure(g); d.toggle_pause()))

static func _time_advance(d) -> void:
	var g = d.app.game
	d.title.text = "时间 · 推进"
	d.subtitle.text = "按实际运行时间支付 DT，遇到重要变化提前结束。"
	var next = g.next_checkpoint()
	var grid = _grid(d)
	d._metric(grid, "可用 DT", "%.2f" % float(g.s.get("dt", 0)), "推进成本 %.2f DT/h" % float(g.temporal_load()), W.TEXT)
	d._metric(grid, "下一变化", str(next.get("name", "周期结算")), "最多 %.1fh" % float(next.get("hours", 0)), W.GREEN)
	var disabled = bool(g.s.get("settings", {}).get("user_paused", false)) or d.app.decision_kind != "" or g.s.get("phase", "") != "play"
	var actions = _group(d.body, "选择推进时长")
	for hours in [.5, 1.0, 3.0]:
		var duration:float = hours
		actions.add_child(W.button("推进 %.1f 小时" % duration, func(): d.advance(duration), disabled))
	actions.add_child(W.primary(W.button("推进到下一变化 · %.1fh" % float(next.get("hours", 0)), func(): d.advance(float(next.get("hours", 0))), disabled)))

static func _time_rules(d) -> void:
	var g = d.app.game
	d.title.text = "时间 · 记录与规则"
	d.subtitle.text = "查看 DT 收支或暂停规则。"
	d._metric(d.body, "累计支付推进", "%.2f DT" % float(g.s.get("totals", {}).get("dt_used", 0)), "", W.TEXT)
	_nav(d.body, d, "DT收支记录", "rules", "dt")
	_nav(d.body, d, "暂停与离线规则", "rules", "offline")

static func _time_rule_detail(d, detail:String) -> void:
	var g = d.app.game
	if detail == "dt":
		d.title.text = "时间规则 · DT收支"
		d.subtitle.text = "已支付推进成本及当前余额。"
		var grid = _grid(d)
		d._metric(grid, "DT余额", "%.2f" % float(g.s.get("dt", 0)), "上限 %.0f" % float(g.c.balance.get("dt_cap", 0)), W.TEXT)
		d._metric(grid, "累计已支付", "%.2f" % float(g.s.get("totals", {}).get("dt_used", 0)), "不会混入通货收支", W.TEXT)
		var journal:Array = g.s.get("civic032", {}).get("transactions", [])
		var table:Array = []
		for i in range(journal.size() - 1, maxi(-1, journal.size() - 61), -1):
			var row:Dictionary = journal[i]
			if absf(float(row.get("dt", 0))) <= .000001: continue
			table.append(["%.1fh" % float(row.get("h", 0)), str(row.get("label", "推进")), "%+.2f DT" % float(row.get("dt", 0))])
		if table.is_empty(): d.body.add_child(W.label("尚无单项DT收支记录。", 15, W.MUTED))
		else: d._table(d.body, ["时间", "原因", "DT变化"], table)
	else:
		d.title.text = "时间规则 · 暂停与离线"
		d.subtitle.text = ""
		var text = "手动暂停会停止城镇自然结算和付费推进，不会停止设备系统时钟。打开查看页面或方案比较时，城镇时间自动暂停；关闭页面后恢复自然运行。离线期间不补算城市饥荒。"
		d.body.add_child(W.label(text, 16, W.MUTED))

static func render_policies(d) -> void:
	var g = d.app.game
	d.cost.text = "可用预算 %.0f · 调整 %.0f 通货" % [float(g.s.currency), float(g.c.balance.get("policy_switch_price", 0))]
	if d.subtopic == "": _policies_overview(d)
	elif d.subtopic == "preview": _policy_preview(d, str(d.detail_id))
	elif d.subtopic == "rules": _policy_rules(d, str(d.detail_id))
	else:
		d.subtopic = ""
		render_policies(d)

static func _policies_overview(d) -> void:
	var g = d.app.game
	d.title.text = "地区政策"
	d.subtitle.text = _block_name(g) + " · 五类制度同页查看；点候选进入变化预览。"
	for row in POLICY_SLOTS:
		var slot = str(row[0])
		var current_id = str(g.s.block.get("policies", {}).get(slot, ""))
		var current:Dictionary = g.c.policies.get(current_id, {})
		var group = _group(d.body, str(row[1]) + " · " + str(current.get("name", "未设置")), W.SOFT)
		var grid = GridContainer.new()
		grid.columns = 1 if d.app.compact else 3
		grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		grid.add_theme_constant_override("h_separation", 8)
		grid.add_theme_constant_override("v_separation", 8)
		group.add_child(grid)
		for raw_id in g.Rules.ids(g.c.policies):
			var policy_id = str(raw_id)
			var def:Dictionary = g.c.policies[policy_id]
			if str(def.get("slot", "")) != slot: continue
			var is_current = policy_id == current_id
			var keyword = str(def.get("summary", ""))
			var card = W.panel(grid, W.SOFT if is_current else W.PANEL)
			card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			card.add_theme_stylebox_override("panel", W.box(W.SOFT if is_current else W.PANEL, W.GREEN if is_current else W.SOFT, 8))
			var contents = W.vbox(card, 4)
			var name = ("✓ " if is_current else "") + str(def.get("name", policy_id))
			var pid = policy_id
			var button = W.button(name, func(): d.choose_policy(pid))
			button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			contents.add_child(button)
			d.policy_buttons[pid] = button
			contents.add_child(W.label(keyword, 14, W.MUTED))
	d.footer.hide()
	d.primary.hide()
	d.secondary.show()
	d.secondary.text = "返回地图" if d.nav_stack.is_empty() else "返回"

static func _policy_preview(d, policy_id:String) -> void:
	var g = d.app.game
	if not g.c.policies.has(policy_id):
		d.body.add_child(W.label("找不到这项政策。", 16, W.MUTED))
		return
	var def:Dictionary = g.c.policies[policy_id]
	var current_id = str(g.s.block.get("policies", {}).get(str(def.get("slot", "")), ""))
	var current:Dictionary = g.c.policies.get(current_id, {})
	d.title.text = "变化预览 · " + str(def.get("name", policy_id))
	d.subtitle.text = _block_name(g) + " · 当前：%s" % str(current.get("name", "未设置"))
	var card = _group(d.body, "主要变化")
	card.add_child(W.label(str(def.get("summary", "")), 16, W.TEXT))
	if str(d.pending_policy) != policy_id:
		card.add_child(W.label("已生效。返回政策列表可比较其他方案。", 14, W.MUTED))
		d.footer.hide()
		d.primary.hide()
		d.secondary.show()
		d.secondary.text = "返回政策列表"
		return
	var prediction:Dictionary = d.policy_prediction
	if d.updating_policy or prediction.is_empty():
		card.add_child(W.label("正在计算当前城市下一周期影响…", 14, W.MUTED))
	elif prediction.get("ok", false):
		var grid = GridContainer.new()
		grid.columns = 2
		grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		card.add_child(grid)
		d._metric(grid, "预计持续收支", "%+.2f /h" % float(prediction.get("cashflow", 0)), "更换后", W.GREEN if float(prediction.get("cashflow", 0)) >= 0 else W.GOLD)
		d._metric(grid, "相对当前变化", "%+.2f /h" % float(prediction.get("delta", 0)), "预测差值", W.GREEN if float(prediction.get("delta", 0)) >= 0 else W.GOLD)
	else:
		card.add_child(W.label(str(prediction.get("error", "本次无法生成预测。")), 14, W.GOLD))
	_nav(d.body, d, "查看完整规则", "rules", policy_id)
	d.footer.show()
	d.primary.show()
	d.primary.pressed.connect(d.apply_policy)
	d.secondary.show()
	d.secondary.text = "返回"
	d.update_policy_footer()

static func _policy_rules(d, policy_id:String) -> void:
	var g = d.app.game
	if not g.c.policies.has(policy_id):
		d.body.add_child(W.label("找不到这项政策。", 16, W.MUTED))
		return
	var def:Dictionary = g.c.policies[policy_id]
	d.title.text = "政策详情 · " + str(def.get("name", policy_id))
	d.subtitle.text = str(def.get("summary", ""))
	var rules:Array = def.get("rules", [])
	if rules.is_empty():
		d.body.add_child(W.label("这项政策没有单独的规则条目。", 15, W.MUTED))
		return
	for rule in rules:
		var item:Dictionary = rule
		var group = _group(d.body, str(item.get("id", "规则")))
		group.add_child(W.label(JSON.stringify(item), 14, W.MUTED))
	d.footer.hide()
	d.primary.hide()
	d.secondary.show()
	d.secondary.text = "返回"

static func _grid(d) -> GridContainer:
	var grid = GridContainer.new()
	grid.columns = 1 if d.app.compact and W.text_scale > 1.2 else (2 if d.app.compact else 3)
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_theme_constant_override("h_separation", 10)
	grid.add_theme_constant_override("v_separation", 10)
	d.body.add_child(grid)
	return grid

static func _nav(parent:Node, d, label:String, section:String, detail:String="") -> Button:
	var button = W.button(label, func(): d.push_section(section, detail))
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(button)
	return button

static func _block_name(g) -> String:
	return str(g.s.get("block", {}).get("display_name", "当前地区"))
