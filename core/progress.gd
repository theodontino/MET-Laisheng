extends RefCounted
## City outcomes, not how many buttons or prototype systems were exercised.
const R=preload("res://core/rules.gd")
static func initialize(g) -> void:
	if not g.s.has("city_goal"):g.s.city_goal={}
	if not g.s.city_goal.is_empty():return
	var anchors=[];var baseline={}
	for gid in R.ids(g.s.groups):
		var p=g.s.groups[gid]
		if p.host=="homeless":continue
		anchors.append(gid)
		baseline[gid]={"health":p.health,"pollution":p.living.get("pollution",0),"service":best_two(p.living)}
	g.s.city_goal={"route":"community","stable":0.,"community":0.,"renewal":0.,"delivered":false,"completed":false,"awards":[],"anchors":anchors,"baseline":baseline}
static func best_two(living:Dictionary) -> float:
	var a=[]
	for k in ["medical","education","entertainment","commercial"]:a.append(float(living.get(k,0)))
	a.sort();return (a[2]+a[3])*.5
static func service_ratio(g,kind:String) -> float:
	var sum=0.;var people=0.
	for p in g.s.groups.values():sum+=float(p.count)*float(p.living.get(kind,0));people+=float(p.count)
	return sum/maxf(.001,people)
static func food_ratio(g) -> float:
	var sum=0.;var people=0.
	for p in g.s.groups.values():sum+=float(p.count)*float(p.living.get("food",1));people+=float(p.count)
	return sum/maxf(.001,people)
static func safe(g) -> bool:
	var m=g.s.metrics
	return m.get("homeless",0)<.1 and food_ratio(g)>=.95 and m.get("energy_ratio",1)>=.95 and g.s.currency>=0 and (m.get("cashflow",0)>=0 or g.s.currency/maxf(.01,-float(m.cashflow))>=4.)
static func renewal_ok(g) -> bool:
	var goal=g.s.city_goal;var found=0
	for gid in goal.anchors:
		if not g.s.groups.has(gid):continue
		var p=g.s.groups[gid];var baseline=goal.baseline[gid]
		if p.host=="homeless":return false
		var service=best_two(p.living)
		if service<.7 or service<float(baseline.service)+.25 or p.health<float(baseline.health)-1:return false
		found+=1
	return found>0
static func select(g,route:String) -> String:
	if not g.c.data.city_goals.has(route):return "未知发展成果。"
	initialize(g);var previous=str(g.s.city_goal.route);g.s.city_goal.route=route;g.Civic.route_selected(g,previous);g.refresh_metrics();return ""
static func after_commit(g,h:float=.1) -> void:
	initialize(g)
	var goal=g.s.city_goal;var m=g.s.metrics
	if float(g.s.local_hours)>=float(g.c.balance.service_start_hours):g.s.stage=1
	if g.s.goods_active:g.s.stage=2
	for id in g.playable_ids():g.s.stage=maxi(g.s.stage,int(g.s.projects[id].get("tier",1))-1)
	g.s.block.stage=g.s.stage
	var is_safe=safe(g)
	goal.stable=float(goal.stable)+h if is_safe else 0.
	var pleasant=is_safe and m.happiness>=72 and m.health>=72 and service_ratio(g,"medical")>=.75 and service_ratio(g,"commercial")>=.75 and service_ratio(g,"entertainment")>=.75
	goal.community=float(goal.community)+h if pleasant else 0.
	goal.renewal=float(goal.renewal)+h if is_safe and renewal_ok(g) else 0.
	# Commissions own Bonus rewards; the city goal grants naming and completion rewards.
	var ready=false
	match str(goal.route):
		"community":ready=goal.community>=float(g.c.balance.goal_duration)
		"delivery":ready=goal.delivered
		"renewal":ready=goal.renewal>=float(g.c.balance.goal_duration)
	if not goal.completed and g.Civic.route_permitted(g) and ready and m.housed>=float(g.c.balance.target_people) and goal.stable>=float(g.c.balance.target_stable_hours):
		goal.completed=true;g.s.complete=true;g.s.currency+=120;g.s.dt=minf(g.c.balance.dt_cap,g.s.dt+6)
		g.log_event("community_complete",{"route":goal.route},"河谷新章完成：这座社区能独立运转了。获得120通货、6DT与地区命名权。")
	if not g.s.crisis.get("active",false) and (minf(g.s.mandate,g.s.support)<15 or maxf(g.s.mandate,g.s.support)<30):
		g.s.crisis={"active":true};g.log_event("crisis",{},"治理危机：生活与财政需要优先修复。城市不会突然消失。")
static func award(g,id:String,title:String,money:float,dt:float) -> void:
	if id in g.s.city_goal.awards or not g.Civic.permitted(g,id):return
	g.s.city_goal.awards.append(id);g.s.currency+=money;g.s.dt=minf(g.c.balance.dt_cap,g.s.dt+dt)
	g.log_event("city_outcome",{"id":id,"money":money,"dt":dt},title+" · 获得一份地区方案机会")
static func deliver(g) -> String:
	initialize(g)
	if g.s.city_goal.delivered:return "交付已完成。"
	if not g.Civic.route_permitted(g) or str(g.s.city_goal.route)!="delivery":return "请先在任务面板接取供给交付成果。"
	var mat=float(g.c.balance.goal_delivery_material);var goods=float(g.c.balance.goal_delivery_goods)
	if not safe(g) or g.s.city_goal.stable<float(g.c.balance.target_stable_hours):return "先让住房、粮食、电力与财政稳定1本地小时。"
	if g.inventory_total("material")<mat or g.inventory_total("goods")<goods:return "需要20材料和12制成品，交付时真正扣除。"
	var foodneed=0.
	for p in g.s.groups.values():foodneed+=float(p.count)*float(g.compiled.stats[p.host].population_food)
	if g.inventory_total("food")<foodneed*2:return "交付前留足当前居民至少2小时的粮食。"
	g.withdraw_stock("material",mat);g.withdraw_stock("goods",goods);g.s.city_goal.delivered=true
	g.log_event("delivery",{"material":mat,"goods":goods},"发展物资已交付，基础生活储备保留。");g.refresh_metrics();after_commit(g,0);return ""
static func checklist(g) -> Array:
	initialize(g)
	var m=g.s.metrics;var goal=g.s.city_goal;var route=str(goal.route)
	var done=goal.community>=g.c.balance.goal_duration if route=="community" else (goal.delivered if route=="delivery" else goal.renewal>=g.c.balance.goal_duration)
	return [
		{"name":"安顿30位居民，无人待安置","done":m.get("housed",0)>=30 and m.get("homeless",0)<.1,"value":"%.0f / 30人"%m.get("housed",0)},
		{"name":"生活与财政稳定1小时","done":goal.stable>=1,"value":"%.1f / 1.0h"%goal.stable},
		{"name":g.c.data.city_goals[route].name,"done":done,"value":"已达成" if done else ("交付20材料 + 12制成品" if route=="delivery" else "%.1f / %.1fh"%[goal[route],g.c.balance.goal_duration])}]
static func crisis(g,choice:String) -> String:
	if not g.s.crisis.get("active",false):return "当前没有治理危机。"
	if choice=="repair":
		if g.s.currency<80:return "整改需要80通货。"
		g.s.currency-=80;g.s.mandate=maxf(50,g.s.mandate);g.s.support=maxf(40,g.s.support);g.s.crisis={};g.log_event("crisis_repair",{},"治理整改费用");return ""
	if choice=="leave":g.s.phase="failed";g.s.failed_reason="交出本地区治理权，城市留作只读记录。";return ""
	return "无效选项。"

static func requirements(g) -> Array:
	initialize(g)
	var m=g.s.metrics;var route=str(g.s.city_goal.route);var rows=[]
	if route=="community":
		rows.append({"name":"健康 / 幸福","value":"%.0f / %.0f，分别需要72"%[m.get("health",0),m.get("happiness",0)],"done":m.get("health",0)>=72 and m.get("happiness",0)>=72,"action":"population"})
		for pair in [["medical","医疗","medical_clinic"],["commercial","购物","commercial_center"],["entertainment","休闲","garden"]]:
			var ratio=service_ratio(g,pair[0])
			rows.append({"name":pair[1]+"实际覆盖","value":"%.0f%% / 75%%"%(100*ratio),"done":ratio>=.75,"action":pair[2]})
	elif route=="delivery":
		rows.append({"name":"可交付材料","value":"%.1f / 20"%g.inventory_total("material"),"done":g.inventory_total("material")>=20,"action":"extraction"})
		rows.append({"name":"可交付制成品","value":"%.1f / 12"%g.inventory_total("goods"),"done":g.inventory_total("goods")>=12,"action":"manufacturing"})
		rows.append({"name":"交付前保留两小时口粮","value":"交付只扣材料和制成品，不扣粮食","done":safe(g),"action":"agriculture"})
	else:
		for gid in g.s.city_goal.anchors:
			if not g.s.groups.has(gid):continue
			var group=g.s.groups[gid];var before=g.s.city_goal.baseline[gid]
			var ratio=best_two(group.living);var target=maxf(.7,float(before.service)+.25)
			rows.append({"name":g.project_name(group.host)+"生活改善","value":"最好的两种服务 %.0f%% / %.0f%%；健康 %.0f / %.0f"%[100*ratio,100*target,group.health,float(before.health)-1],"done":ratio>=target and group.health>=float(before.health)-1,"action":"population"})
	return rows
