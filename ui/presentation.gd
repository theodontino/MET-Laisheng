extends RefCounted
## Human-facing descriptions of real accounting; not a second economic simulation.
const RES={"food":"粮食","material":"材料","goods":"制成品","energy":"电力"}
const KIND={"medical":"医疗","education":"教育","commercial":"购物","entertainment":"休闲"}
static func primary(base:String) -> String:
	return {"neighborhood":"为街坊补齐基础服务","workshop":"让建设与生产有一个起点","homestead":"为新居民留出住处","agriculture":"让粮食供给站稳","extraction":"给建设持续供料","power_station":"给城镇接上电","manufacturing":"生产生活与交付用品","commercial_center":"让居民方便购物","primary_school":"让教育走进社区","medical_clinic":"照顾附近居民健康","garden":"让街坊多一处休闲","yard":"留住多出来的物资"}.get(base,"")
static func resource(g,snapshot:Dictionary,r:String) -> Dictionary:
	var h=maxf(1e-12,float(snapshot.audit.get("hours",g.c.balance.tick_hours)));var a=snapshot.audit.get("resources",{}).get(r,{})
	var produced=float(a.get("produced",0))/h;var consumed=float(a.get("consumed",0))/h;var lost=float(a.get("lost",0))/h
	var imported=float(a.get("imported",0))/h;var exported=float(a.get("exported",0))/h
	var delivered=float(a.get("commission_delivered",0))/h
	var net=produced+imported-consumed-exported-delivered-lost;var stock=g.inventory_total(r)
	var need=float(snapshot.metrics.get("requested",{}).get(r,consumed))
	var status="供需平稳"
	if lost>.05:status="每月浪费 %.1f"%lost
	elif need>consumed+.1:status="未满足需求 %.1f/月"%(need-consumed)
	elif net<-.05:status="按当前用量约 %.1f月 耗尽"%(stock/-net)
	elif net>.1:status="正在积累储备"
	return {"stock":stock,"produced":produced,"consumed":consumed,"lost":lost,"net":net,"need":need,"imported":imported,"exported":exported,"delivered":delivered,"status":status}
static func alerts(g,shot:Dictionary) -> Array:
	var alerts=[];var m=shot.metrics
	if g.s.phase!="play":return alerts
	if g.s.currency<0:alerts.append({"title":"财政已赤字","detail":"先检查闲置维护和当前税制。","action":"policies","target":"","severity":3})
	elif m.get("cashflow",0)<-1:alerts.append({"title":"财政每月 %+.1f"%m.cashflow,"detail":"按当前收支，预算约还能支持 %.1f月。"%(g.s.currency/-float(m.cashflow)),"action":"policies","target":"","severity":2})
	if m.get("homeless",0)>.1:alerts.append({"title":"%.0f位居民等待住房"%m.homeless,"detail":"建房、增加现有住宅容量，或先暂停新的迁入。","action":"homestead","target":"","severity":3})
	var food=resource(g,shot,"food")
	var family_food=0.;var family_count=0.
	for group in shot.groups.values():family_food+=float(group.count)*float(group.living.get("food",1));family_count+=float(group.count)
	if family_food/maxf(.001,family_count)<.99:alerts.append({"title":"粮食供给未满足","detail":"先看农居、农业站的用工和库存；有建筑不等于供给充足。","action":"agriculture","target":"","severity":3})
	elif food.net<-.1:alerts.append({"title":"粮食储备正在减少","detail":food.status+"；可增建农居，或沿农业方向升级。","action":"agriculture","target":"","severity":2})
	if m.get("energy_ratio",1)<.98:alerts.append({"title":"电力只满足 %.0f%%"%(100*m.energy_ratio),"detail":"发电站缺工也会少发电，先看缺口来源。","action":"power_station","target":"","severity":3})
	var arrive=g.next_arrival()
	if arrive>=0 and not g.s.migration_paused and m.get("vacancies",0)<g.c.balance.arrival_count:
		alerts.append({"title":"%.1f月后有6位新邻居"%arrive,"detail":"空房仅 %.0f；可建房或暂停这批迁入。"%m.get("vacancies",0),"action":"homestead","target":"","severity":2})
	for id in g.playable_ids():
		var p=g.s.projects[id];var a=m.get("utilization",{}).get(id,{})
		if p.status!="running" or p.paused or a.is_empty():continue
		if float(a.utilization)<.45 and float(a.labor)<.7 and g.project_stats(id).jobs>0:
			alerts.append({"title":g.project_name(id)+"缺工","detail":"运行率约%.0f%%。可优先供人，不必盲目再建同类。"%(100*a.utilization),"action":"inspect","target":id,"severity":2});break
	if alerts.size()<2 and g.s.city_goal.get("route","")=="community" and not g.s.city_goal.get("completed",false):
		var lowest="";var ratio=1.
		for pair in [["medical","medical_clinic"],["commercial","commercial_center"],["entertainment","garden"]]:
			var covered=0.;var people=0.
			for group in shot.groups.values():covered+=float(group.count)*float(group.living.get(pair[0],0));people+=float(group.count)
			var value=covered/maxf(.001,people)
			if value<ratio:ratio=value;lowest=pair[0]
		if lowest!="" and ratio<.75 and g.s.local_hours>=2:
			alerts.append({"title":KIND[lowest]+"覆盖还差一点","detail":"实际约%.0f%%，宜居成果需要75%%；先看范围和有效容量。"%(ratio*100),"action":{"medical":"medical_clinic","commercial":"commercial_center","entertainment":"garden"}[lowest],"target":"","severity":1})
	if alerts.size()<2 and g.s.migration_paused and m.get("vacancies",0)<2:
		alerts.append({"title":"住宅需要一点增长余地","detail":"已暂停新迁入，但现有住户仍会自然增长。","action":"homestead","target":"","severity":1})
	if alerts.size()<2 and g.s.get("goods_available",false) and not g.s.goods_active and float(g.s.get("goods_requested_at",-1))<0:
		alerts.append({"title":"生活用品已经能生产","detail":"主动公布消费计划，2月后新需求才生效。","action":"goals","target":"","severity":1})
	if alerts.size()<2 and g.s.block.commissions.get("pending_reward","")!="":alerts.append({"title":"委托奖励待领取","detail":"从已保存的三份候选中选择一份部署许可。","action":"goals","target":"","severity":1})
	if alerts.size()<2 and food.lost>.1:alerts.append({"title":"粮食没有留住","detail":food.status+"；可暂缓扩产或增设仓储。","action":"yard","target":"","severity":1})
	if alerts.size()<2 and g.s.city_goal.get("completed",false):alerts.append({"title":"社区已建成，城市仍然属于你","detail":"继续改善住区、调整方案，或保存这座河谷。完整跨地区发展尚未开放。","action":"goals","target":"","severity":0})
	if alerts.size()<2:alerts.append({"title":"选择河谷的发展成果","detail":"宜居、供给交付或旧区改善，不必把所有建筑造一遍。","action":"goals","target":"","severity":0})
	alerts.sort_custom(func(a,b):return a.severity>b.severity)
	return alerts.slice(0,2)
static func plan_lines(g,result:Dictionary) -> Array:
	if not result.get("ok",false):return [str(result.get("error","不能在此规划。"))]
	var lines=[]
	if result.housing>0:lines.append("+%.0f 住房位置（不是立即增加人口）"%result.housing)
	for r in ["food","material","goods"]:
		var n=float(result.outputs.get(r,0))
		if n>.001:lines.append("预计%s %.1f/月 · 可入库 %.1f"%[RES[r],n,result.retained.get(r,0)])
	if result.energy_supply>.05:lines.append("预计供电 +%.1f"%result.energy_supply)
	for k in g.c.balance.service_kinds:
		if result.services[k]>.01:lines.append("%s预计服务 %.0f人 · 全城新增 %+.0f"%[KIND[k],result.services[k],result.added_service[k]])
	if lines.is_empty():lines.append("仓储 / 调度 / 规则作用，详情见下方")
	return lines
static func plan_cautions(g,result:Dictionary) -> Array:
	if not result.get("ok",false):return []
	var lines=[];var st=result.stats;var a=result.activity
	if float(a.get("labor",1))<.95 and st.jobs>0:lines.append("缺工：预计 %.1f / %.1f岗位"%[result.workers,st.jobs])
	if float(a.get("energy",1))<.98:lines.append("当前电力只能支撑约 %.0f%%"%(100*a.energy))
	if float(a.get("resources",1))<.98:lines.append("现有原料不足，预计运行 %.0f%%"%(100*a.utilization))
	if result.harmed>.1:lines.append("附近约 %.0f位住户的环境负担增加"%result.harmed)
	if result.affected.size()>0:lines.append("会挤占 "+g.project_name(result.affected[0].id)+" 的投入")
	if float(result.outputs.get("food",0))>.1 and float(result.get("existing_food_produced",0))>float(result.get("existing_food_consumed",0))*1.2:
		lines.append("当前粮食有余量；新增产能主要为后续扩张。")
	var loss=0.
	for n in result.losses.values():loss+=float(n)
	if loss>.01:lines.append("现有仓储不足，预计损耗 %.1f/月"%loss)
	return lines
