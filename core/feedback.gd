extends RefCounted
## Presentation only. One instance per city view; no simulation writes, RNG or previews.
## Economic local_hours is the compatibility field for Laisheng months in Beta.
const ENTER = 0.95
const EXIT = 0.98
const LOCAL_HOLD = 0.02
const BUBBLE_TTL = 8.0
const BUBBLE_GAP = 18.0
const HISTORY_LIMIT = 24
const RESOURCE_NAMES = {"food":"粮食", "material":"材料", "goods":"制成品", "energy":"电力"}
var _identity = ""
var _tick = -1
var _revision = -1
var _local = 0.0
var _watches:Dictionary = {}
var _groups:Dictionary = {}
var _medical_changes:Dictionary = {}
var _projects:Dictionary = {}
var _heard:Dictionary = {}
var _bubbles:Array = []
var _history:Array = []
var _alerts:Array = []
var _next_bubble = 0.0
var _capstone = ""
var _initialized = false
var _commercial_year = -1
var _commercial_served = 0.0

func reset() -> void:
	_tick = -1; _revision = -1; _local = 0; _watches.clear(); _groups.clear(); _medical_changes.clear(); _projects.clear()
	_heard.clear(); _bubbles.clear(); _history.clear(); _alerts.clear()
	_next_bubble = 0; _capstone = ""; _initialized = false
	_commercial_year = -1; _commercial_served = 0.0

func refresh(g, real_seconds:float, snapshot:Dictionary = {}) -> Dictionary:
	var identity = "%s:%s:%s" % [g.get_instance_id(), g.s.get("seed",0), g.s.get("started_at",0)]
	if identity != _identity:
		reset(); _identity = identity
	# A proposed build/bonus must never cause alerts, speech, or city history.
	if bool(snapshot.get("forecast",false)):
		return view(real_seconds)
	var tick = int(g.s.get("simulation_tick",0))
	var local = float(g.s.get("local_hours",0))
	if tick < _tick or local < _local:
		reset()
	if tick != _tick or int(g.revision) != _revision:
		var delta = maxf(0,local - _local) if _initialized else 0.0
		_update_alerts(g,delta)
		_update_voices(g,real_seconds)
		_tick = tick; _revision = int(g.revision); _local = local; _initialized = true
	_update_journal(g)
	_update_capstone(g)
	return view(real_seconds)

func view(real_seconds:float) -> Dictionary:
	_bubbles = _bubbles.filter(func(row):return float(row.expires_at)>real_seconds)
	var causes:Dictionary = {}
	for row in _alerts:
		if not causes.has(row.cause):
			causes[row.cause] = row.duplicate(true); causes[row.cause].count = 0
		causes[row.cause].count += 1
	var summaries:Array = causes.values()
	summaries.sort_custom(_priority)
	for row in summaries:
		if row.count > 1:row.text = "%s · %d处受限" % [row.text,row.count]
	return {"alerts":_alerts.duplicate(true),"summaries":summaries,"main":summaries[0] if not summaries.is_empty() else {},"bubbles":_bubbles.duplicate(true),"chirps":_history.duplicate(true)}

func history() -> Array:
	return _history.duplicate(true)

static func _priority(a:Dictionary,b:Dictionary) -> bool:
	if a.severity != b.severity:return int(a.severity)>int(b.severity)
	if a.ratio != b.ratio:return float(a.ratio)<float(b.ratio)
	return str(a.id)<str(b.id)

static func source(g,id:String,layer:String,kind:String = "building") -> Dictionary:
	var p:Dictionary = g.s.get("projects",{}).get(id,{})
	return {"source_id":id,"building_id":id,"tile":str(p.get("tile","")),"layer":layer,"kind":kind}

func _candidate(g,id:String,cause:String,text:String,ratio:float,severity:int,layer:String,evidence:Dictionary) -> Dictionary:
	var row = source(g,id,layer)
	row.merge({"id":id+":"+cause,"cause":cause,"text":text,"ratio":ratio,"severity":severity,"evidence":evidence},true)
	return row

func _update_alerts(g,delta:float) -> void:
	var metrics:Dictionary = g.s.get("metrics",{})
	if not metrics.has("activity_ledger"):return
	var candidates:Array = []
	for id in g.s.projects:
		var p:Dictionary = g.s.projects[id]
		if p.get("status","") != "running" or bool(p.get("paused",false)) or bool(p.get("special",false)):continue
		var a:Dictionary = metrics.get("utilization",{}).get(id,{})
		if a.is_empty():continue
		var utilization = float(a.get("utilization",1))
		for cause in ["energy","resources","labor"]:
			var ratio = float(a.get(cause,1))
			if ratio >= EXIT or utilization >= EXIT:continue
			var label = {"energy":"电力不足","resources":"投入物资不足","labor":"缺少有效人手"}[cause]
			var layer = {"energy":"energy","resources":"material","labor":"employment"}[cause]
			if cause == "resources":
				var st:Dictionary = g.compiled.get("stats",{}).get(id,{})
				var inputs:Array = []
				for resource in ["food","material","goods"]:
					if float(st.get("input."+resource,0))>0:inputs.append(resource)
				if inputs.size()==1:
					layer=inputs[0]; label="缺少"+str(RESOURCE_NAMES[layer])
			var evidence = {"tick":g.s.simulation_tick,"satisfaction":ratio,"utilization":utilization,"bottleneck":str(a.get("bottleneck",""))}
			candidates.append(_candidate(g,id,cause,label,ratio,2 if utilization < .05 and ratio < .05 else 1,layer,evidence))
		var lost=0.0
		for item in metrics.get("activity_ledger",{}).get(id,[]):lost += float(item.get("lost",0))
		if lost>0.000001:
			candidates.append(_candidate(g,id,"storage","仓储不足 · 已有溢出",0,1,"goods",{"tick":g.s.simulation_tick,"actual_lost":lost}))
	# Resident shortages are based on actual finite receipts, never on import ratio alone.
	for gid in g.s.get("groups",{}):
		var group:Dictionary = g.s.groups[gid]
		var id = str(group.get("host",""))
		if not g.s.projects.has(id) or bool(g.s.projects[id].get("special",false)):continue
		var food = float(group.get("living",{}).get("food",1))
		if food < EXIT and float(group.get("count",0))>0:
			var row = _candidate(g,id,"food","居民口粮不足",food,2 if food<.5 else 1,"food",{"tick":g.s.simulation_tick,"group_id":gid,"actual_food_satisfaction":food})
			row.group_id=gid; candidates.append(row)
	candidates.append_array(_import_alerts(g))
	var seen:Dictionary = {}; var active_by_building:Dictionary = {}
	for row in candidates:
		if seen.has(row.id):continue
		seen[row.id]=true
		var watch:Dictionary = _watches.get(row.id,{"held":0.0,"active":false})
		if float(row.ratio)<ENTER:
			watch.held=float(watch.held)+delta
		elif not bool(watch.active):watch.held=0.0
		if row.severity==2 or float(watch.held)>=LOCAL_HOLD:watch.active=true
		_watches[row.id]=watch
		if not bool(watch.active):continue
		if not active_by_building.has(row.building_id):active_by_building[row.building_id]=[]
		active_by_building[row.building_id].append(row)
	for key in _watches.keys():
		if not seen.has(key):_watches.erase(key)
	_alerts=[]
	for id in active_by_building:
		var rows:Array=active_by_building[id];rows.sort_custom(_priority)
		_alerts.append(rows[0])
		if rows.size()>1 and int(rows[1].severity)==2:_alerts.append(rows[1])
	_alerts.sort_custom(_priority)

func _import_alerts(g) -> Array:
	var result:Array=[]
	var elapsed=float(g.s.get("audit",{}).get("hours",0))
	if elapsed<=0:return result
	var freight=""
	for id in g.s.projects:
		if float(g.compiled.get("stats",{}).get(id,{}).get("trade_capacity",0))>0:
			freight=str(id);break
	if freight=="":return result
	for resource in ["food","material","goods"]:
		var pending:Dictionary={}
		for order in g.s.get("block",{}).get("trade_orders",[]):
			if order.get("resource","")==resource and order.get("side","")=="buy" and order.get("status","") in ["open","partial"] and float(order.get("remaining",0))>0:
				pending=order;break
		if pending.is_empty():continue
		var receipt:Dictionary=g.s.get("audit",{}).get("resources",{}).get(resource,{})
		var produced=float(receipt.get("produced",0))/elapsed
		var requested=maxf(float(receipt.get("consumed",0))/elapsed,float(g.s.metrics.get("requested",{}).get(resource,0)))
		var dependence=maxf(0,requested-produced)
		var imports=float(receipt.get("imported",0))/elapsed
		var deficit=dependence-imports
		if dependence<=0 or deficit<=0:continue
		var stock=0.0
		for p in g.s.projects.values():stock+=float(p.get("inventory",{}).get(resource,0))
		var months=stock/deficit
		if months>=1:continue
		var row=_candidate(g,freight,"import_"+resource,"%s进口跟不上当前消耗"%RESOURCE_NAMES[resource],imports/dependence,2 if requested>float(receipt.get("consumed",0))/elapsed+.000001 and stock<=.000001 else 1,resource,{"tick":g.s.simulation_tick,"order_id":pending.get("id",""),"stock":stock,"local_months_remaining":months,"required_imports_per_month":dependence,"actual_imports_per_month":imports,"limit_price":pending.get("limit_price",0)})
		row.kind="trade";result.append(row)
	return result

func _say(g,gid:String,text:String,reason:String,evidence:Dictionary,now:float) -> void:
	if now<_next_bubble or _bubbles.size()>=2:return
	var group:Dictionary=g.s.groups.get(gid,{})
	var host=str(group.get("host",""))
	if not g.s.projects.has(host) or bool(g.s.projects[host].get("special",false)):return
	var row=source(g,host,{"medical":"medical","employment":"employment","shopping":"commercial","pollution":"pollution","housing":"population"}.get(reason,"population"),"population")
	row.merge({"id":gid+":"+reason,"group_id":gid,"text":text,"reason":reason,"evidence":evidence.duplicate(true),"expires_at":now+BUBBLE_TTL},true)
	_bubbles.append(row);_next_bubble=now+BUBBLE_GAP

func _chirp(id:String,text:String,origin:Dictionary,evidence:Dictionary) -> void:
	if _heard.has(id):return
	_heard[id]=true
	while _heard.size()>512:_heard.erase(_heard.keys()[0])
	var row=origin.duplicate(true);row.merge({"id":id,"text":text,"evidence":evidence.duplicate(true)},true)
	_history.append(row)
	while _history.size()>HISTORY_LIMIT:_history.pop_front()

func _update_voices(g,now:float) -> void:
	var metrics:Dictionary=g.s.get("metrics",{})
	for id in g.s.projects:
		var p:Dictionary=g.s.projects[id]
		if _projects.has(id) and _projects[id]=="construction" and p.get("status","")=="running":
			var st:Dictionary=g.compiled.get("stats",{}).get(id,{})
			_chirp("opening:"+id,"%s已竣工，可提供%.0f个岗位。"%[p.get("name",g.c.projects.get(p.get("def",""),{}).get("name",id)),float(st.get("jobs",0))],source(g,id,"employment"),{"tick":g.s.simulation_tick,"status":"running","jobs":float(st.get("jobs",0))})
		_projects[id]=p.get("status","")
	for gid in g.s.get("groups",{}):
		var group:Dictionary=g.s.groups[gid];var living:Dictionary=group.get("living",{})
		var current={"host":group.get("host",""),"medical":float(living.get("medical",0)),"shopping":float(living.get("commercial",0)),"unemployment":float(living.get("unemployment",1)),"pollution":float(living.get("pollution",0))}
		if _groups.has(gid):
			var previous:Dictionary=_groups[gid]
			var evidence={"previous":previous,"current":current,"tick":g.s.simulation_tick}
			if previous.host=="homeless" and current.host!="homeless":_say(g,gid,"这次终于能安顿下来了。","housing",evidence,now)
			elif current.medical-previous.medical>=.1 and not _medical_changes.has(gid):
				_medical_changes[gid]={"baseline":previous.medical,"since":float(g.s.get("local_hours",0)),"evidence":evidence.duplicate(true)}
			elif previous.unemployment-current.unemployment>=.1:_say(g,gid,"最近能找到活干了。","employment",evidence,now)
			elif current.pollution-previous.pollution>=.1:_say(g,gid,"厂子忙起来了，空气也不一样了。","pollution",evidence,now)
			elif current.shopping<.5 and previous.shopping<.5:_say(g,gid,"买点日用品还真不容易。","shopping",evidence,now)
		if _medical_changes.has(gid):
			var change:Dictionary=_medical_changes[gid]
			if current.medical<float(change.baseline)+.1:_medical_changes.erase(gid)
			elif float(g.s.get("local_hours",0))-float(change.since)>=LOCAL_HOLD and now>=_next_bubble:
				var proof:Dictionary=change.evidence.duplicate(true)
				proof.current=current.duplicate(true);proof.held_local_months=float(g.s.local_hours)-float(change.since)
				_say(g,gid,"这回看诊不用到处找地方了。","medical",proof,now)
				_medical_changes.erase(gid)
		_groups[gid]=current
	for trade in metrics.get("market_trades",[]):
		if trade.get("side","")!="sell" or float(trade.get("amount",0))<=0:continue
		var id="export:"+str(trade.get("id","%s:%s"%[g.s.simulation_tick,trade.get("order_id","")]))
		var logistics_id=str(trade.get("source_id","municipal"))
		var capacity_sources:Array=trade.get("capacity_sources",[])
		if not capacity_sources.is_empty():logistics_id=str(capacity_sources[0].get("project_id",logistics_id))
		var origin=source(g,logistics_id,str(trade.get("resource","goods")),"trade")
		_chirp(id,"本区完成一批%s外运。"%RESOURCE_NAMES.get(str(trade.get("resource","")),"物资"),origin,trade)
	var year=int(floor(float(g.s.get("local_hours",0))/12.0))
	var served=0.0;var commercial_source=""
	for link in metrics.get("service_links",[]):
		if link.get("kind","")!="commercial":continue
		served+=float(link.get("people",0))
		if float(link.get("people",0))>0 and commercial_source=="":commercial_source=str(link.get("source",""))
	if year!=_commercial_year:
		if _commercial_year>=0 and served>_commercial_served+1.0 and commercial_source!="":
			_chirp("commercial-year:%d"%year,"比上一年度结算时，更多居民能在附近买到日用品。",source(g,commercial_source,"commercial"),{"previous_actual_served":_commercial_served,"actual_served":served,"tick":g.s.simulation_tick})
		_commercial_year=year;_commercial_served=served

func _update_journal(g) -> void:
	for event in g.s.get("log",[]):
		if event.get("kind","") not in ["commission_completed","commission_theme_completed","theme_completed"]:continue
		var payload:Dictionary=event.get("data",{})
		_chirp("commission:%s:%s"%[event.get("tick",0),payload.get("id",payload.get("theme",""))],str(event.get("text","城市建设目标达成。")),{"source_id":str(payload.get("id",payload.get("theme",""))),"building_id":"","tile":"","layer":"normal","kind":"commission"},event)

func _update_capstone(g) -> void:
	var capstone:Dictionary=g.s.get("block",{}).get("capstone",{})
	if capstone.is_empty():return
	var id=str(capstone.get("source_id",capstone.get("id","")))
	if id=="" or id==_capstone:return
	_capstone=id
	_chirp(id,"%s的%s正式启用。"%[g.s.block.get("display_name","本区"),capstone.get("name","地区主题建筑")],{"source_id":id,"building_id":"","tile":str(capstone.get("tile_anchor","")),"layer":"normal","kind":"capstone"},capstone)
