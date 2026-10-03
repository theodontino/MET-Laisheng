extends RefCounted
## Operator-only tender. Persisted offers and reroll pricing survive page navigation.
const Rng=preload("res://core/rng.gd")
const R=preload("res://core/rules.gd")
static func common(g) -> String:
	if g.s.phase!="play":return "当前不可建设。"
	
	return ""
static func open_build(g,cells:Array,base:String) -> String:
	var e=common(g)
	if e!="":return e
	if cells.size()!=1:return "请选择一个未开发的陆地地块。"
	var tile=str(cells[0])
	if not g.s.cells.has(tile) or g.s.cells[tile].building!="" or "tile.water" in g.s.cells[tile].traits:return "请选择未开发的陆地。"
	if not base in g.c.v040.progression.tier1:return "新建项目请从农居、邻里中心或作坊开始。"
	remember(g)
	g.s.draft={"kind":"development","base":base,"tile":tile,"host_type":"building","host":"","slot":"","offers":[]}
	restore_or_draw(g)
	return ""
static func open_refit(g,id:String,slot:String) -> String:
	return "方案副本来自年度、安居里程碑与主题奖励，请从库存中安装。"
static func open_scope(g,host_type:String,host:String) -> String:
	return "地区与组织方案从已有奖励副本安装。"
static func draw(g,d:Dictionary) -> Array:
	var offers=[];var used=[]
	var domain=str(g.c.projects[d.base].get("domain","public"))
	for i in 3:
		var options=[];var weights=[]
		for org in R.ids(g.c.operators):
			if org in used:continue
			options.append(org);weights.append(float(g.c.operators[org].domain_weights.get(domain,2)))
		var org=str(Rng.pick_weighted(g.s,"draft",options,weights));used.append(org)
		offers.append({"id":"offer:"+str(i),"base":d.base,"operator_org":org,"provider_org":org,"slot_track":g.c.operators[org].slot_track.duplicate()})
	return offers
static func reroll_cost(g) -> Dictionary:
	var n=int(g.s.draft.get("reroll_count",g.s.tender_debt))
	return {"currency":12.+9.*n,"dt":0.0}
static func reroll(g) -> String:
	if g.s.draft.is_empty():return "尚未开标。"
	var cost=reroll_cost(g)
	if g.s.currency<cost.currency:return "重投预算不足。"
	g.s.currency-=cost.currency;g.s.tender_debt+=1;g.s.draft.reroll_count=int(g.s.draft.get("reroll_count",0))+1;g.s.totals.rerolls+=1
	g.s.draft.offers=draw(g,g.s.draft);g.log_event("reroll",{},"重新征集一组投标方案");return ""
static func offer_cost(g,offer:Dictionary) -> Dictionary:
	var d=g.s.draft
	if d.kind=="development":
		var base=g.c.projects[d.base];return {"currency":float(base.cost),"material":float(base.material),"cp":float(base.construction_required),"hours":float(base.construction_required)/maxf(.01,g.Construction.capacity(g))}
	return {"currency":float(g.c.balance.block_offer_price if d.kind=="scope" else g.c.balance.bonus_offer_price),"material":0.,"hours":0.}
static func accept(g,oid:String) -> String:
	if g.s.phase!="play" or g.s.draft.is_empty():return "没有待处理的运营商报价。"
	var chosen={}
	for offer in g.s.draft.offers:
		if offer.id==oid:chosen=offer;break
	if chosen.is_empty():return "这不是当前报价。"
	var d=g.s.draft
	if d.kind!="development":return "旧方案报价已转入奖励选择。"
	var cost=offer_cost(g,chosen)
	if g.s.currency<float(cost.currency):return "通货不足。报价会保留，可继续经营。"
	if g.s.cells[d.tile].building!="":return "场地已被占用。"
	g.create_building(d.base,d.tile,chosen.operator_org)
	g.s.currency-=cost.currency;g.s.totals.currency_spent+=cost.currency
	g.s.quote_bank.erase(quote_key(d));g.s.draft={};g.s.tender_debt=0;g.refresh_metrics()
	g.log_event("accepted",{"offer":chosen,"kind":"development"},"签约「"+g.c.operators[chosen.operator_org].name+"」，开始建设。")
	return ""

static func quote_key(d:Dictionary) -> String:
	if d.get("kind","")=="development":return "build:"+str(d.base)
	return str(d.get("kind",""))+":"+str(d.get("host",""))+":"+str(d.get("slot",""))
static func remember(g) -> void:
	if not g.s.has("quote_bank"):g.s.quote_bank={}
	if not g.s.draft.is_empty():g.s.quote_bank[quote_key(g.s.draft)]=g.s.draft.duplicate(true)
static func restore_or_draw(g) -> void:
	var key=quote_key(g.s.draft)
	if g.s.quote_bank.has(key):
		var cached=g.s.quote_bank[key];g.s.draft.offers=cached.offers.duplicate(true)
		g.s.draft.reroll_count=int(cached.get("reroll_count",0))
	else:
		g.s.draft.offers=draw(g,g.s.draft);g.s.draft.reroll_count=0
	g.s.tender_debt=int(g.s.draft.reroll_count)
	remember(g)
