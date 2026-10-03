extends RefCounted
## Persistent, bounded civic history and the lifecycle of EXISTING city outcomes.
## This module does not generate resources or invent retrospective history.
const OUTCOMES={
	"food":{"name":"稳住粮食供给","desc":"居民口粮满足99%，粮食不再净减少，并经营至少0.5来生月。","money":35.,"dt":0.},
	"housing":{"name":"安顿新邻居","desc":"安置至少24位居民，待安置人口少于0.1。","money":35.,"dt":0.},
	"services":{"name":"接通生活服务","desc":"医疗和购物实际覆盖分别达到65%。","money":40.,"dt":0.},
	"manufacturing":{"name":"第一批生活用品","desc":"让真实制成品库存达到6。","money":30.,"dt":0.}}
const ROUTES=["community","delivery","renewal"]
const HISTORY_LIMIT=720
const ACTION_LIMIT=300
static func ensure(g) -> void:
	if g.s.has("civic032"):return
	var records={}
	for id in OUTCOMES:
		records[id]={"status":"completed" if id in g.s.get("city_goal",{}).get("awards",[]) else "active","accepted_h":null,"finished_h":null}
	g.s.civic032={"version":1,"started_h":float(g.s.get("local_hours",0)),"history":[],"transactions":[],"task_history":[],"tasks":records,"route_active":not bool(g.s.get("city_goal",{}).get("completed",false)),"cash_anchor":float(g.s.currency),"dt_anchor":float(g.s.dt),"last_population_rate":null,"last_birth_rate":null,"last_arrival_rate":null,"last_cycle_h":0.,"history_trimmed":false,"chapter_recorded":bool(g.s.get("city_goal",{}).get("completed",false))}
	if not g.s.settings.has("user_paused"):g.s.settings.user_paused=false
static func permitted(g,id:String) -> bool:
	ensure(g)
	return g.s.civic032.tasks.get(id,{}).get("status","active")=="active"
static func route_permitted(g) -> bool:
	ensure(g);return bool(g.s.civic032.route_active)
static func route_selected(g,previous:String) -> void:
	ensure(g)
	var now=str(g.s.city_goal.route)
	if previous!=now and g.s.civic032.route_active:
		_task_event(g,"route:"+previous,"switched","改选城市成果")
	g.s.civic032.route_active=true
	if previous!=now:_task_event(g,"route:"+now,"accepted","选择城市成果")
static func event(g,kind:String,payload:Dictionary,text:String) -> void:
	if g.preview_mode or g.recording_cycle or not g.s.has("civic032"):return
	var j=g.s.civic032
	var delta=float(g.s.currency)-float(j.cash_anchor)
	var dt_delta=float(g.s.dt)-float(j.dt_anchor)
	if absf(delta)>.000001 or absf(dt_delta)>.000001:
		j.transactions.append({"h":float(g.s.local_hours),"tick":int(g.s.simulation_tick),"kind":kind,"label":text if text!="" else kind,"cash":delta,"dt":dt_delta,"balance":float(g.s.currency)})
		while j.transactions.size()>ACTION_LIMIT:j.transactions.pop_front()
	j.cash_anchor=float(g.s.currency);j.dt_anchor=float(g.s.dt)
static func cycle(g,h:float,previous_population:float,arrivals:float) -> void:
	if g.preview_mode:return
	ensure(g)
	var j=g.s.civic032;var m=g.s.metrics;var count=float(m.get("population",0))
	var change=count-previous_population
	j.last_population_rate=change/maxf(1e-15,h)
	j.last_arrival_rate=arrivals/maxf(1e-15,h)
	j.last_birth_rate=(change-arrivals)/maxf(1e-15,h)
	j.last_cycle_h=h
	var row={"h":float(g.s.local_hours),"tick":int(g.s.simulation_tick),"span":h,"balance":float(g.s.currency),"dt":float(g.s.dt),"income":float(m.get("income",0)),"upkeep":float(m.get("upkeep",0)),"net":float(m.get("cashflow",0)),"population":count,"population_rate":j.last_population_rate,"known":true}
	j.history.append(row)
	while j.history.size()>HISTORY_LIMIT:j.history.pop_front();j.history_trimmed=true
	# Operating cash is described by the actual cycle, not mislabeled as an action.
	j.cash_anchor=float(g.s.currency);j.dt_anchor=float(g.s.dt)
static func finalize(g) -> void:
	if g.preview_mode or not g.s.has("civic032"):return
	var j=g.s.civic032
	if not j.history.is_empty() and int(j.history[-1].tick)==int(g.s.simulation_tick):
		j.history[-1].balance=float(g.s.currency);j.history[-1].dt=float(g.s.dt)
	j.cash_anchor=float(g.s.currency);j.dt_anchor=float(g.s.dt)
static func sync(g) -> void:
	if g.preview_mode:return
	ensure(g)
	var j=g.s.civic032
	for id in g.s.get("city_goal",{}).get("awards",[]):
		if not j.tasks.has(id):continue
		if j.tasks[id].status!="completed":
			j.tasks[id].status="completed";j.tasks[id].finished_h=float(g.s.local_hours);_task_event(g,id,"completed","成果已兑现；奖励不会重复领取")
	if g.s.get("city_goal",{}).get("completed",false) and not j.get("chapter_recorded",false):
		j.chapter_recorded=true
		_task_event(g,"route:"+str(g.s.city_goal.route),"completed","河谷新章完成")
static func task_action(g,id:String,action:String) -> String:
	if g.s.phase!="play":return "当前城市不能调整任务。"
	ensure(g);var j=g.s.civic032
	if action not in ["accept","abandon"]:return "未知任务操作。"
	if id.begins_with("route:"):
		var route=id.trim_prefix("route:")
		if route not in ROUTES:return "未知城市成果。"
		if g.s.city_goal.get("completed",false):return "这一章已经完成，不能重新领奖。"
		if action=="abandon":
			if str(g.s.city_goal.route)!=route or not j.route_active:return "这个成果并非当前任务。"
			j.route_active=false;_task_event(g,id,"abandoned","停止追求此成果；城市与已有进度保留")
		else:
			if j.route_active and str(g.s.city_goal.route)==route:return ""
			var previous=str(g.s.city_goal.route)
			var err=g.Progress.select(g,route)
			if err!="":return err
			if previous==route:_task_event(g,id,"accepted","重新接取；不追发已领取奖励")
	else:
		if not OUTCOMES.has(id):return "未知城市任务。"
		var record=j.tasks[id]
		if record.status=="completed" or id in g.s.city_goal.awards:return "这个成果已经兑现，不可重复领取。"
		if action=="abandon":
			if record.status!="active":return "此任务不在进行中。"
			record.status="abandoned";record.finished_h=float(g.s.local_hours);_task_event(g,id,"abandoned","停止任务与自动奖励；不撤走已有设施")
		else:
			if record.status=="active":return ""
			record.status="active";record.accepted_h=float(g.s.local_hours);record.finished_h=null;_task_event(g,id,"accepted","重新接取")
	g.refresh_metrics();g.log_event("task_"+action,{"id":id},("接取" if action=="accept" else "放弃")+task_name(g,id))
	return ""
static func task_name(g,id:String) -> String:
	if id.begins_with("route:"):return str(g.c.data.city_goals.get(id.trim_prefix("route:"),{}).get("name",id))
	return str(OUTCOMES.get(id,{}).get("name",id))
static func _task_event(g,id:String,status:String,note:String) -> void:
	var j=g.s.civic032
	j.task_history.append({"id":id,"name":task_name(g,id),"status":status,"h":float(g.s.local_hours),"tick":int(g.s.simulation_tick),"note":note})
	while j.task_history.size()>ACTION_LIMIT:j.task_history.pop_front()
static func task_rows(g) -> Array:
	var j=g.s.get("civic032",{});var goal=g.s.get("city_goal",{});var rows=[]
	if goal.is_empty():return rows
	for id in OUTCOMES:
		var def=OUTCOMES[id];var state=str(j.get("tasks",{}).get(id,{}).get("status","active"))
		if id in goal.get("awards",[]):state="completed"
		var progress=0.;var reading="";var m=g.s.metrics
		match id:
			"food":
				var measured=m.has("flows") and int(g.s.get("simulation_tick",0))>0
				var flow=float(m.get("flows",{}).get("food",0))
				progress=minf(1,minf(g.Progress.food_ratio(g)/.99,float(g.s.local_hours)/.5)) if measured and flow>=-.01 else 0.
				reading=("实际口粮 %.0f%% · 净变 %+.1f/h · 运行 %.1f/0.5h"%[100*g.Progress.food_ratio(g),flow,float(g.s.local_hours)]) if measured else "待首次实际结算 · 运行至少0.5h后判定"
			"housing":
				progress=minf(1,float(m.get("housed",0))/24.);reading="已安置 %.1f / 24人 · 待安置 %.1f"%[m.get("housed",0),m.get("homeless",0)]
			"services":
				var med=g.Progress.service_ratio(g,"medical");var shopping=g.Progress.service_ratio(g,"commercial")
				progress=minf(1,minf(med,shopping)/.65);reading="医疗 %.0f%% · 购物 %.0f%% / 目标65%%"%[100*med,100*shopping]
			"manufacturing":progress=minf(1,g.inventory_total("goods")/6.);reading="制成品 %.1f / 6"%g.inventory_total("goods")
		rows.append({"id":id,"name":def.name,"description":def.desc,"status":state,"progress":progress,"reading":reading,"reward":"历史城市目标（本版奖励由年度与委任提供）","chapter":false})
	for route in ROUTES:
		var active=str(goal.get("route","community"))==route and bool(j.get("route_active",true))
		var status="active" if active else "available"
		if goal.get("completed",false):status="completed" if str(goal.route)==route else "closed"
		var progress=minf(1,float(goal.get(route,0))/maxf(.1,float(g.c.balance.goal_duration))) if route!="delivery" else (1. if goal.get("delivered",false) else 0.)
		var desc=str(g.c.data.city_goals[route].get("description",""))
		if desc=="":desc={"community":"健康、幸福达到72；医疗、购物、休闲分别覆盖75%，保持1.2小时。","delivery":"交付20材料与12制成品，保留至少2小时口粮。","renewal":"改善原住区最好的两种服务且保持健康，持续1.2小时。"}[route]
		rows.append({"id":"route:"+route,"name":g.c.data.city_goals[route].name,"description":desc,"status":status,"progress":progress,"reading":"共用底线：安置30人、生活稳定1小时；"+("已交付" if progress>=1 and route=="delivery" else ("等待物资交付" if route=="delivery" else "保持 %.1f / %.1fh"%[goal.get(route,0),g.c.balance.goal_duration])),"reward":"本章一次：120通货 · 6DT · 1次地区方案机会","chapter":true})
	return rows
static func validate(value:Dictionary) -> String:
	if not value.has("civic032"):return ""
	var j=value.civic032
	var settings=value.get("settings",{})
	if settings is Dictionary and settings.has("user_paused") and not settings.user_paused is bool:return "暂停状态损坏。"
	if not j is Dictionary:return "城市记录格式损坏。"
	for key in ["history","transactions","task_history"]:
		if not j.get(key) is Array:return "城市记录缺少列表："+key
		if j[key].size()>2000:return "城市记录长度异常。"
		for row in j[key]:
			if not row is Dictionary:return "城市记录条目损坏。"
			if not row.get("h") is float and not row.get("h") is int:return "城市记录时间无效。"
			if not is_finite(float(row.h)):return "城市记录时间无效。"
	if not j.get("tasks") is Dictionary:return "任务状态损坏。"
	for required in OUTCOMES:
		if not j.tasks.has(required):return "缺少任务状态："+required
	if not j.get("route_active") is bool:return "当前成果状态损坏。"
	for key in ["last_population_rate","last_birth_rate","last_arrival_rate"]:
		if j.get(key)!=null and (not (j[key] is int or j[key] is float) or not is_finite(float(j[key]))):return "人口变化记录损坏。"
	for id in j.tasks:
		if not j.tasks[id] is Dictionary or j.tasks[id].get("status","") not in ["active","abandoned","completed"]:return "任务状态无效。"
	for key in ["cash_anchor","dt_anchor","started_h"]:
		if not (j.get(key) is int or j.get(key) is float) or not is_finite(float(j[key])):return "城市记录数值无效。"
	for row in j.history:
		for key in ["tick","span","balance","dt","income","upkeep","net","population","population_rate"]:
			if not (row.get(key) is int or row.get(key) is float) or not is_finite(float(row[key])):return "历史账本数值无效。"
	for row in j.transactions:
		for key in ["tick","cash","dt","balance"]:
			if not (row.get(key) is int or row.get(key) is float) or not is_finite(float(row[key])):return "收支记录数值无效。"
		if not row.get("label") is String or not row.get("kind") is String:return "收支记录来源无效。"
	for row in j.task_history:
		if not row.get("id") is String or not row.get("name") is String or not row.get("note") is String:return "任务历史损坏。"
		if row.get("status","") not in ["accepted","abandoned","completed","switched"]:return "任务历史状态无效。"
	return ""
