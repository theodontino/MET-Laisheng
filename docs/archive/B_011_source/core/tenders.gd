extends RefCounted
const Rng=preload("res://core/rng.gd")
const Population=preload("res://core/population.gd")
const Hex=preload("res://core/hex.gd")

static func development(g,cells:Array,tags:Array) -> String:
	if g.s.phase!="play":return "当前不能招标。"
	if not g.s.draft.is_empty():return "已有未处理的投标，关面板不会更换候选。"
	if cells.is_empty() or cells.size()>7 or not Hex.connected(cells):return "请选择1～7个连通地块。"
	if tags.is_empty() or tags.size()>2:return "选择1～2个招标偏好。"
	for tag in tags:
		if not tag in g.s.policy_tags:return "这项政策还没有获得。"
	var zone=""
	for k in cells:
		if not g.s.cells.has(k):return "场地超出地图。"
		var tile=g.s.cells[k]
		if tile.site!="" or tile.terrain=="water":return "请选择尚未开发的陆地；不需要预先涂区。"
	var temp={"kind":"development","cells":cells.duplicate(),"zone":zone,"tags":tags.duplicate(),"rerolls":0,"target":"","offers":[]}
	var pool=legal_pool(g,temp)
	if pool.is_empty():return "当前场地没有合法投标；尝试扩大场地或改变发展Tag。"
	temp.offers=draw(g,temp,pool,[])
	g.s.draft=temp
	g.log_event("tender_open",temp,"三份投标已保存。偏好只影响概率，不保证标签命中。")
	return ""

static func upgrade(g,id:String,tag:String) -> String:
	if g.s.phase!="play" or not g.s.projects.has(id):return "项目不可用。"
	if not g.s.draft.is_empty():return "先处理已有投标。"
	var p=g.s.projects[id]
	var ms=g.milestone_status(id)
	if not ms.ready:return ms.text
	if tag!="" and not tag in g.s.policy_tags:return "政策不可用。"
	var temp={"kind":"upgrade","target":id,"cells":g.s.sites[p.site].cells.duplicate(),"zone":g.c.projects[p.def].zone,"tags":[tag] if tag!="" else [],"rerolls":0,"offers":[]}
	var pool=legal_pool(g,temp)
	if pool.is_empty():return "当前没有新的升级候选。"
	temp.offers=draw(g,temp,pool,[]);g.s.draft=temp
	g.log_event("upgrade_draw",temp,"里程碑升级：历史与当前状态决定这一轮的可能性。")
	return ""

static func fusion(g,id:String) -> String:
	if g.s.phase!="play" or not g.s.relations.has(id):return "关系不可用。"
	if not g.s.draft.is_empty():return "先处理已有投标。"
	var rel=g.s.relations[id]
	if not g.relation_active(rel) or float(rel.progress)<float(rel.required) or rel.choice!="":return "关系尚未成熟或已经选择。"
	var choices=[]
	for choice in g.c.fusions[rel.template].choices:choices.append(choice.id)
	g.s.draft={"kind":"fusion","target":id,"zone":"","cells":[],"tags":[],"rerolls":0,"offers":Rng.shuffled(g.s,"draft",choices)}
	g.log_event("fusion_draw",{"relationship":id},"它们一起运行的时间，终于长成一次新的关系选择。")
	return ""

static func legal_pool(g,draft:Dictionary) -> Array:
	var pool=[]
	if draft.kind=="fusion":
		var rel=g.s.relations[draft.target]
		for item in g.c.fusions[rel.template].choices:pool.append(item.id)
		return pool
	if draft.kind=="development":
		var traits=g.site_traits(draft.cells)
		for def in g.c.projects.values():
			if int(def.get("stage",0))>Population.stage(g) or def.get("starter_only",false):continue
			if draft.cells.size()<int(def.min_cells) or draft.cells.size()>int(def.max_cells):continue
			var valid=true
			for condition in def.hard:
				if not condition["has"] in traits:valid=false
			if valid:pool.append(def.id)
	else:
		var p=g.s.projects[draft.target]
		var stats=g.project_stats(p.id)
		for def in g.c.upgrades.values():
			if not draft.zone in def.zones or def.id in p.upgrades or int(def.get("stage",0))>Population.stage(g):continue
			var cond=def.get("condition","")
			if cond=="has_emissions" and stats.noise+stats.pollution<=0:continue
			if cond=="waterfront" and not g.site_has(p.site,"waterfront"):continue
			if cond=="has_tagged_input":
				var found=false
				for input in stats.inputs:
					if not input.tags.is_empty():found=true
				if not found:continue
			if cond=="material_or_goods_output":
				var found=false
				for out in stats.outputs:
					if out.resource in ["material","goods"]:found=true
				if not found:continue
			pool.append(def.id)
	return pool

static func definition(g,kind:String,id:String) -> Dictionary:
	if kind=="development":return g.c.projects[id]
	if kind=="upgrade":return g.c.upgrades[id]
	for template in g.c.fusions.values():
		for choice in template.choices:
			if choice.id==id:return choice
	return {}

static func draw(g,draft:Dictionary,pool:Array,exclude:Array) -> Array:
	if draft.kind=="development":return draw_contextual(g,draft,pool,exclude)
	var remaining=[]
	for id in pool:
		if not id in exclude:remaining.append(id)
	if remaining.size()<mini(3,pool.size()):
		for id in pool:
			if not id in remaining:remaining.append(id)
	var result=[]
	var fresh=[]
	for fid in pool:
		if not fid in exclude:fresh.append(fid)
	for slot in range(mini(3,remaining.size())):
		var weights=[]
		for id in remaining:
			var def=definition(g,draft.kind,id)
			var hits=0
			for tag in draft.tags:
				if tag in def.get("tags",[]):hits+=1
			var weight=1.0
			if slot==0:
				weight=6.0 if hits==draft.tags.size() and hits>0 else (1.8 if hits>0 else .7)
			elif slot==1:weight=3.5 if hits>0 else 1.0
			# Context has influence in every slot, including the unprompted one.
			for t in g.site_traits(draft.cells):
				if t in def.get("tags",[]):weight*=1.25
			if draft.kind=="upgrade":
				var tags=g.base_tags(draft.target)
				for t in def.tags:
					if t in tags:weight*=1.35
				var p=g.s.projects[draft.target]
				var neighbors={}
				for other in g.s.projects:
					if g.adjacent_projects(p.id,other) and g.s.projects[other].status=="running":
						for tag in g.base_tags(other):neighbors[tag]=true
				for tag in def.tags:
					if neighbors.has(tag):weight*=1.2
				for effect in def.effects:
					if p.ever_bottleneck and effect.op=="input_any":weight*=1.6
					if "能源" in p.bottleneck and effect.op=="supply_energy":weight*=1.7
					if p.high_usage_hours>=2 and effect.op=="output_tag":weight*=1.25
			else:
				var owned={}
				for pid in g.s.projects:
					for tag in g.base_tags(pid):owned[tag]=true
				for tag in def.tags:
					if owned.has(tag):weight*=1.12
			if slot==0 and not exclude.is_empty() and not fresh.is_empty() and not id in fresh:weight=0.0
			weights.append(weight)
		var chosen=Rng.pick_weighted(g.s,"draft",remaining,weights)
		result.append(chosen);remaining.erase(chosen)
	return Rng.shuffled(g.s,"draft",result)

static func reroll_cost(g) -> Dictionary:
	var n=int(g.s.draft.get("rerolls",0))+int(g.s.tender_debt)
	var prices=g.c.balance.reroll_currency
	return {"currency":float(prices[mini(n,prices.size()-1)])*pow(1.4,maxi(0,n-prices.size()+1)),"dt":maxf(0,n-1)*1.0}

static func reroll(g) -> String:
	if g.s.phase!="play" or g.s.draft.is_empty():return "没有当前投标。"
	var d=g.s.draft
	if d.kind=="fusion":return "关系模板当前只有这两种机制，不收钱重发同一组。"
	var pool=legal_pool(g,d)
	if pool.size()<=d.offers.size():return "当前合法池没有新方案；不会收费重排同一手牌。"
	var cost=reroll_cost(g)
	if g.s.currency<cost.currency or g.s.dt<cost.dt:return "通货或DT不足，原投标仍然保留。"
	g.s.currency-=cost.currency;g.s.dt-=cost.dt;g.s.totals.currency_spent+=cost.currency;g.s.totals.dt_used+=cost.dt
	d.offers=draw(g,d,pool,d.offers);d.rerolls+=1;g.s.totals.rerolls+=1
	g.log_event("reroll",{"cost":cost,"offers":d.offers.duplicate()},"重新开标：花费 %.0f 通货 / %.1f DT。"%[cost.currency,cost.dt])
	return ""

static func withdraw(g) -> String:
	if g.s.phase!="play" or g.s.draft.is_empty():return "没有当前投标。"
	if g.s.draft.kind=="fusion":
		g.s.draft={}
		g.log_event("fusion_deferred",{},"暂不采纳协作；成熟关系保留，固定的两种方案也保留。")
		return ""
	var cost=reroll_cost(g)
	if g.s.currency<cost.currency or g.s.dt<cost.dt:return "撤回并重新规划，需要当前这次重招成本。关面板可以免费保留原案。"
	g.s.currency-=cost.currency;g.s.dt-=cost.dt
	g.s.tender_debt+=int(g.s.draft.rerolls)+1
	g.log_event("withdraw_tender",cost,"撤回投标；换地、换偏好不会重置连续重招成本。")
	g.s.draft={};return ""

static func offer_cost(g,id:String) -> Dictionary:
	var d=g.s.draft
	if d.is_empty():return {}
	if d.kind=="fusion":return {"currency":75.0,"material":8.0,"goods":0.0,"hours":0.0}
	var def=definition(g,d.kind,id)
	var scale=1.0
	if d.kind=="development":scale=1.0+.13*maxi(0,d.cells.size()-1)
	else:scale=1.0+.65*int(g.s.projects[d.target].level)
	return {"currency":float(def.cost)*scale,"material":float(def.material)*scale*(.9 if g.s.reveal=="stable_ground" else 1.0),"goods":float(def.goods)*scale,"hours":float(def.hours)*scale}

static func accept(g,id:String) -> String:
	if g.s.phase!="play" or g.s.draft.is_empty() or not id in g.s.draft.offers:return "该方案不属于当前投标。"
	var d=g.s.draft
	var pool=legal_pool(g,d)
	if not id in pool:return "城市状态已经改变，这项方案现在不合法。"
	var cost=offer_cost(g,id)
	if g.s.currency<cost.currency:return "通货不足；可以关面板保留这份投标。"
	if d.kind=="fusion":
		var rel=g.s.relations[d.target]
		if not g.relation_active(rel):return "两项目当前不满足共同运行条件。"
		var Inv=preload("res://core/inventory.gd")
		if Inv.total(g.s.stocks,"material")<cost.material:return "建立协作需要8单位材料；开局不依赖制成品。"
		Inv.take(g.s.stocks,"material",[],cost.material)
		rel.choice=id;g.s.totals.fusions+=1
		g.log_event("fusion_committed",{"id":id,"pair":[rel.a,rel.b]},"新关系：「"+definition(g,"fusion",id).name+"」。")
	elif d.kind=="development":
		for k in d.cells:
			if g.s.cells[k].site!="":return "场地已改变，原投标不能在这里落地。"
		var site_id="S%d"%int(g.s.next_id);g.s.next_id+=1
		g.s.sites[site_id]={"id":site_id,"cells":d.cells.duplicate(),"zone":g.c.projects[id].zone,"project_ids":[]}
		for k in d.cells:g.s.cells[k].site=site_id;g.s.cells[k].zone=g.c.projects[id].zone
		var p=g.create_project(id,site_id)
		g.s.sites[site_id].project_ids.append(p.id)
		g.log_event("construction_start",{"project":p.id,"proposal":id,"cost":cost,"cells":d.cells.duplicate()},"中标：「"+g.c.projects[id].name+"」。资源随实际施工逐步消耗。")
		if cost.goods>0 and not "goods" in g.s.revealed_resources:g.s.revealed_resources.append("goods")
		if g.s.projects.size()>=3 and not "energy" in g.s.revealed_resources:g.s.revealed_resources.append("energy")
	else:
		var p=g.s.projects[d.target]
		if p.status!="running" or not p.job.is_empty():return "项目当前不能继续升级。"
		p.status="upgrading"
		p.job={"kind":"upgrade","work":cost.hours,"total":cost.hours,"material":cost.material,"goods":cost.goods,"energy":1.0,"target":id,"progress":0.0}
		g.log_event("upgrade_start",{"project":p.id,"upgrade":id},"开始升级：「"+g.c.upgrades[id].name+"」。新机制竣工后生效。")
	g.s.currency-=cost.currency;g.s.totals.currency_spent+=cost.currency
	g.s.draft={};g.s.tender_debt=0
	g.refresh_metrics()
	return ""

const FAMILIES=["housing","agriculture","materials","energy","health","leisure","commerce","education","industry","logistics"]
const RELATED={"housing":["leisure","commerce"],"agriculture":["housing","commerce","industry"],"materials":["energy","industry"],"energy":["materials"],"health":["leisure"],"leisure":["health","commerce"],"commerce":["agriculture","leisure","logistics"],"education":["housing","industry"],"industry":["materials","energy","education"],"logistics":["commerce","industry"]}
static func intentions(g,d:Dictionary) -> Array:
	var families=[]
	for tag in d.tags:
		if tag in FAMILIES and not tag in families:families.append(tag)
	if families.is_empty():
		var traits=g.site_traits(d.cells)
		if "community" in d.tags:families.append("housing")
		elif "hill" in traits or "forest" in traits:families.append("materials")
		else:families.append("agriculture")
	return families

static func draw_contextual(g,d:Dictionary,pool:Array,exclude:Array) -> Array:
	var intents=intentions(g,d);d["intentions"]=intents
	var result=[]
	var traits=g.site_traits(d.cells)
	for slot in 3:
		var intent=str(intents[mini(slot,intents.size()-1)])
		var matching=[];var nearby=[]
		for id in pool:
			if id in result:continue
			var def=g.c.projects[id]
			if intent in def.families:matching.append(id)
			else:
				for neighbor in RELATED.get(intent,[]):
					if neighbor in def.families:nearby.append(id);break
		var available=matching
		# The third response may offer an adjacent idea, never a random unrelated building.
		if slot==2 and not nearby.is_empty() and (matching.is_empty() or Rng.next(g.s,"draft")<.28):available=nearby
		if available.is_empty():available=nearby
		if available.is_empty():continue
		var weights=[]
		for id in available:
			var def=g.c.projects[id];var weight=float(def.get("weight",1))
			for tag in d.tags:
				if tag in def.tags:weight*=2.0 if slot<2 else 1.3
			for terrain_trait in traits:
				if terrain_trait=="plain" and "agriculture" in def.families:weight*=1.5
				if terrain_trait in def.tags:weight*=1.3
				if terrain_trait=="forest" and "forestry" in def.tags:weight*=1.6
			if id in exclude:weight*=.18
			weights.append(weight)
		result.append(Rng.pick_weighted(g.s,"draft",available,weights))
	return Rng.shuffled(g.s,"draft",result)
