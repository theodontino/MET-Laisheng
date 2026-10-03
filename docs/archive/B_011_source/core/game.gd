extends RefCounted
const Catalog=preload("res://core/catalog.gd")
const Hex=preload("res://core/hex.gd")
const Rng=preload("res://core/rng.gd")
const Inv=preload("res://core/inventory.gd")
const Draft=preload("res://core/tenders.gd")
const Sim=preload("res://core/simulation.gd")
const Progress=preload("res://core/progress.gd")
const Population=preload("res://core/population.gd")
var c=Catalog.new()
var s:Dictionary={}
var last_error:String=""

func new_game(seed_value:int,now:float) -> void:
	var v=absi(seed_value)%2147483000+1
	s={"schema":2,"build":"0.1.1-b-population","seed":seed_value,"phase":"select_block",
		"rng":{"opening":v,"draft":(v+1701)%2147483646+1,"events":(v+5303)%2147483646+1,"risk":(v+9011)%2147483646+1},
		"wall_mark":now,"global_now":now,"started_at":now,"local_hours":0.0,"remainder":0.0,"block_index":-1,
		"currency":float(c.balance.initial_currency),"dt":float(c.balance.initial_dt),"mandate":75.0,"support":75.0,
		"cells":{},"sites":{},"projects":{},"relations":{},"stocks":{},"knowledge":0.0,"policy_tags":c.balance.base_policy_tags.duplicate(),
		"origin_choices":[],"origin":"","reveal":"","next_id":1,"draft":{},"research":{},"researched":[],"tag_rewards":[],
		"events":[],"log":[],"contracts":{},"crisis":{},"complete":false,"completion_hours":0.0,"failed_reason":"",
		"revealed_resources":["food","material","energy"],"stable_hours":0.0,"shortage_hours":0.0,"shortage_ratio":1.0,
		"metrics":{},"totals":{"built":0,"upgrades":0,"fusions":0,"rerolls":0,"dt_used":0.0,"currency_spent":0.0},
		"tender_debt":0,"debug_used":false,"settings":{"sound":true,"lab_speed":1.0,"load_scale":1.0,"relationship_scale":1.0},
		"notes":[],"simulation_tick":0,"stage":0,"stage_started":0.0,"displaced":0.0,"migration_paused":false,"governance_bad_hours":0.0}
	log_event("new_game",{"seed":seed_value},"新的任期还没有开始。")

func log_event(kind:String,payload:Dictionary={},message:String="") -> void:
	s.log.append({"n":s.log.size()+1,"h":s.local_hours,"kind":kind,"data":payload.duplicate(true),"text":message})
	if s.log.size()>2000:s.log.pop_front()

func select_block(index:int) -> String:
	if s.phase!="select_block" or index<0 or index>2:return "现在不能重新选择开局地块。"
	s.block_index=index;s.cells=Hex.make_map(index)
	var candidates=c.origins.keys()
	s.origin_choices=Rng.shuffled(s,"opening",candidates).slice(0,2)
	var reveals=["stable_ground","spring","alluvial"]
	s.reveal=reveals[int(Rng.next(s,"opening")*reveals.size())]
	s.phase="origin"
	log_event("block_selected",{"block":index,"origins":s.origin_choices},"已选定起点；这次勘测结果与起源候选已固定。")
	return ""

func choose_origin(id:String) -> String:
	if s.phase!="origin" or not id in s.origin_choices:return "这不是本次可接手的起源。"
	var origin=c.origins[id]
	s.origin=id;s.phase="play"
	s.currency+=float(origin.cash);s.dt+=float(origin.dt)
	Inv.add(s.stocks,"food",["basic","fresh"],float(c.balance.initial_stock.food)+float(origin.food),1000)
	var materials=float(c.balance.initial_stock.material)+float(origin.material)
	Inv.add(s.stocks,"material",["basic","light"],materials*.6,1000)
	Inv.add(s.stocks,"material",["basic","heavy"],materials*.4,1000)

	for t in origin.tags:
		if not t in s.policy_tags:s.policy_tags.append(t)
	_create_starter("hamlet",["0,1"])
	var anchor_cells=["0,0"]
	if origin.anchor=="quarry":anchor_cells=["2,-1"]
	if origin.anchor=="timber":anchor_cells=["1,1"]
	_create_starter(origin.anchor,anchor_cells)
	for home in s.projects.values():
		if c.projects[home.def].population>0:home.residents=float(c.balance.population.initial)
	Progress.initialize_contracts(self)
	log_event("origin_chosen",{"id":id},"接手「"+origin.name+"」。18位居民已经安顿。圈一块地、选发展Tag：先让每个人有饭吃、有地方住、有工作。")
	refresh_metrics()
	return ""

func _create_starter(def_id:String,cells:Array) -> void:
	var def=c.projects[def_id]
	var site_id="S%d"%int(s.next_id);s.next_id+=1
	s.sites[site_id]={"id":site_id,"cells":cells.duplicate(),"zone":def.zone,"project_ids":[]}
	for k in cells:s.cells[k].zone=def.zone;s.cells[k].site=site_id
	var project=create_project(def_id,site_id,true)
	s.sites[site_id].project_ids.append(project.id)

func create_project(def_id:String,site_id:String,ready:bool=false) -> Dictionary:
	var def=c.projects[def_id]
	var id="P%d"%int(s.next_id);s.next_id+=1
	var hours=float(def.hours)
	var site=s.sites[site_id]
	var size_scale=1.0+.13*maxi(0,site.cells.size()-1)
	var p={"id":id,"def":def_id,"site":site_id,"level":0,"status":"running" if ready else "construction",
		"usage":1.0 if ready else 0.0,"bottleneck":"","job":{},"paused":false,"effects":[],"extra_tags":[],"upgrades":[],
		"effective_hours":0.0,"high_usage_hours":0.0,"ever_bottleneck":false,"context_goal":"neighbor" if site_has(site_id,"waterfront") else "stock",
		"opportunity_checked":ready,"history":[],"spend_material":0.0,"spend_goods":0.0,"size_scale":size_scale,
		"produced":{},"opened_milestones":[],"outputs_last":{},"milestone_goal_fallback":false}
	if not ready:
		var material=float(def.material)*size_scale
		if s.reveal=="stable_ground":material*=.9
		p.job={"kind":"build","work":hours*size_scale,"total":hours*size_scale,"material":material,"goods":float(def.goods)*size_scale,"target":"","energy":.75,"progress":0.0}
	Population.ensure(p)
	s.projects[id]=p
	s.totals.built+=1
	return p

func zone_cells(keys:Array,zone:String) -> String:
	if s.phase!="play":return "当前不能开发。"
	if not zone in ["","settlement","production","circulation","public"]:return "未知用途。"
	for k in keys:
		if not s.cells.has(k):continue
		var tile=s.cells[k]
		if tile.site=="" and tile.terrain!="water":tile.zone=zone
	return ""

func site_traits(cells:Array) -> Array:
	var a=[]
	for k in cells:
		if not s.cells.has(k):continue
		if s.cells[k].waterfront and not "waterfront" in a:a.append("waterfront")
		var terrain=s.cells[k].terrain
		if not terrain in a:a.append(terrain)
	if cells.size()>=4:a.append("large")
	return a

func site_has(site_id:String,trait_id:String) -> bool:
	return s.sites.has(site_id) and trait_id in site_traits(s.sites[site_id].cells)

func adjacent_projects(a:String,b:String) -> bool:
	if a==b or not s.projects.has(a) or not s.projects.has(b):return false
	return Hex.adjacent(s.sites[s.projects[a].site].cells,s.sites[s.projects[b].site].cells)

func base_tags(id:String) -> Array:
	var p=s.projects[id];var tags=c.projects[p.def].tags.duplicate()
	for t in p.extra_tags:
		if not t in tags:tags.append(t)
	for effect in p.effects:
		if effect.op=="add_tag" and not effect.tag in tags:tags.append(effect.tag)
	if site_has(p.site,"waterfront") and not "waterfront" in tags:tags.append("waterfront")
	for rel in s.relations.values():
		if rel.choice=="learn_research" and id in [rel.a,rel.b] and relation_active(rel) and not "research" in tags:tags.append("research")
	return tags

func project_stats(id:String) -> Dictionary:
	var p=s.projects[id];var def=c.projects[p.def]
	var st={"tags":base_tags(id),"inputs":def.inputs.duplicate(true),"outputs":def.outputs.duplicate(true),
		"energy":float(def.energy),"supply_energy":float(def.get("supply_energy",0)),"upkeep":float(def.upkeep),"income":float(def.income),
		"population":float(def.population),"services":def.services.duplicate(true),"storage":{},"support_bonus":0.0,"jobs":float(def.get("jobs",0)),"skill":float(def.get("skill",0)),"noise":float(def.get("noise",0)),"pollution":float(def.get("pollution",0)),"service_range":int(def.get("service_range",2))}
	for effect in def.effects+p.effects:apply_stat_effect(st,effect,id)
	# Relationships have no effect while either partner is paused, incomplete, or moved.
	for rel in s.relations.values():
		if rel.choice=="" or not id in [rel.a,rel.b] or not relation_active(rel):continue
		var effect=rel.choice
		if effect=="learn_quality" and "industry" in st.tags:
			st.skill=maxf(0,st.skill-15);st.services["education"]=float(st.services.get("education",0))+8
		elif effect=="learn_research" and id==rel.a:
			st.outputs.append({"resource":"knowledge","rate":1.1,"tags":[]})
		elif effect=="harvest_goods" and "agriculture" in st.tags:
			st.services["market"]=float(st.services.get("market",0))+12
		elif effect=="harvest_buffer" and "agriculture" in st.tags:
			st.storage["food"]=float(st.storage.get("food",0))+90
			for out in st.outputs:
				if out.resource=="food" and not "preserved" in out.tags:out.tags.append("preserved")
		elif effect=="civic_education" and id==rel.a:
			st.services["education"]=float(st.services.get("education",0))+12
			st.outputs.append({"resource":"knowledge","rate":.5,"tags":[]})
		elif effect=="civic_care" and id==rel.a:
			st.services["care"]=float(st.services.get("care",0))+12;st.services["leisure"]=float(st.services.get("leisure",0))+12
		elif effect=="network_ports" and "industry" in st.tags:
			st.jobs=maxf(0,st.jobs-2);st.energy=maxf(0,st.energy-.4)
		elif effect=="network_cycle" and id==rel.a:
			st.outputs.append({"resource":"material","rate":.7,"tags":["light","basic"]});st.storage["material"]=90
	for other in s.projects:
		if other==id or not adjacent_projects(other,id):continue
		var op=s.projects[other]
		if op.status!="running" or op.paused:continue
		for effect in c.projects[op.def].effects+op.effects:
			if effect.op=="aura_energy_discount" and effect.tag in st.tags:st.energy*=1.0-float(effect.value)
	# An orchard needs effective running time before full production.
	if float(def.get("mature_hours",0))>0:
		var fruit_ratio=.6+.4*minf(1,float(p.effective_hours)/float(def.mature_hours))
		for out in st.outputs:out.rate*=fruit_ratio
	# Town commerce adds goods demand gradually; early commerce trades food only.
	if def.id in ["market","stall","dock","tavern"] and Population.stage(self)>=2:
		var demand_ramp=clampf((float(s.local_hours)-float(s.stage_started))/3,0,1)
		st.inputs.append({"resource":"goods","rate":float(st.services.get("market",0))*.018*demand_ramp,"tags":[]})
	if Population.stage(self)<2:
		st.inputs=st.inputs.filter(func(x):return x.resource!="goods")
		st.outputs=st.outputs.filter(func(x):return x.resource!="goods")
	# Footprint affects real capacity/cost, not the number of decorative objects.
	for input in st.inputs:input.rate*=float(p.size_scale)
	for out in st.outputs:out.rate*=float(p.size_scale)
	st.population*=float(p.size_scale)
	for service in st.services:st.services[service]*=float(p.size_scale)
	st.energy*=float(p.size_scale);st.income*=float(p.size_scale);st.upkeep*=float(p.size_scale)
	st.jobs=maxf(0,st.jobs)*float(p.size_scale);st.skill=maxf(0,st.skill);st.noise=maxf(0,st.noise);st.pollution=maxf(0,st.pollution)
	return st

func apply_stat_effect(st:Dictionary,e:Dictionary,id:String) -> void:
	match str(e.op):
		"add_tag":
			if not e.tag in st.tags:st.tags.append(e.tag)
		"input_any":
			for input in st.inputs:
				if not input.tags.is_empty():input.tags=[];break
		"output_tag":add_output_tag(st,e.tag)
		"add_output":st.outputs.append({"resource":e.resource,"rate":float(e.rate),"tags":e.tags.duplicate()})
		"add_input":st.inputs.append({"resource":e.resource,"rate":float(e.rate),"tags":e.tags.duplicate()})
		"energy":st.energy+=float(e.value)
		"supply_energy":st.supply_energy+=float(e.value)
		"jobs":st.jobs+=float(e.value)
		"skill":st.skill+=float(e.value)
		"noise":st.noise+=float(e.value)
		"pollution":st.pollution+=float(e.value)
		"upkeep":st.upkeep+=float(e.value)
		"income":st.income+=float(e.value)
		"population":st.population+=float(e.value)
		"service":st.services[e.service]=float(st.services.get(e.service,0))+float(e.value)
		"storage":st.storage[e.resource]=float(st.storage.get(e.resource,0))+float(e.value)
		"terrain_service":
			if site_has(s.projects[id].site,e.trait):st.services[e.service]=float(st.services.get(e.service,0))+float(e.value)
		"terrain_energy":
			if site_has(s.projects[id].site,e.trait):st.supply_energy+=float(e.value)
		"terrain_output":
			if site_has(s.projects[id].site,e.terrain):
				for out in st.outputs:out.rate*=float(e.value)
		"neighbor_service","neighbor_income":
			var has=false
			for other in s.projects:
				if other!=id and adjacent_projects(id,other) and e.tag in base_tags(other) and s.projects[other].status=="running":has=true;break
			if has:
				if e.op=="neighbor_income":st.income+=float(e.value)
				else:st.services[e.service]=float(st.services.get(e.service,0))+float(e.value)

func add_output_tag(st:Dictionary,tag:String) -> void:
	for out in st.outputs:
		if out.resource in ["material","goods"] and not tag in out.tags:out.tags.append(tag)

func relation_active(rel:Dictionary) -> bool:
	if not s.projects.has(rel.a) or not s.projects.has(rel.b):return false
	if not adjacent_projects(rel.a,rel.b):return false
	for id in [rel.a,rel.b]:
		var p=s.projects[id]
		if p.status!="running" or p.paused:return false
	return true

func all_stats() -> Dictionary:
	var result={}
	for id in s.projects:result[id]=project_stats(id)
	return result

func temporal_load() -> float:
	var pop=float(s.metrics.get("population",0))
	var structural=0.0
	for p in s.projects.values():structural+=float(p.size_scale)+float(p.level)*.45
	return 1.0+(structural*float(c.balance.load_project)+pop*float(c.balance.load_people))*float(s.settings.load_scale)

func pulse(hours:float) -> Dictionary:
	if s.phase!="play":return {"advanced":0.0,"spent":0.0,"reason":"当前不在治理中。"}
	if not s.events.is_empty():return {"advanced":0.0,"spent":0.0,"reason":"先处理施工提议，或选择按原计划继续。"}
	var result=Sim.advance(self,maxf(hours,0),true,true)
	log_event("dt_pulse",result,"注入 %.2f 小时，消耗 %.2f DT。"%[result.advanced,result.spent])
	return result

func advance_online(hours:float) -> void:
	if s.phase!="play" or not s.events.is_empty():return
	Sim.advance(self,hours,false,true)

func resume_time(now:float) -> float:
	var elapsed=maxf(0.0,now-float(s.wall_mark))
	var restored=0.0
	if s.phase=="play":
		restored=minf(float(c.balance.dt_cap)-float(s.dt),elapsed/3600.0*float(c.balance.offline_dt_per_hour))
		restored=maxf(0,restored);s.dt+=restored
	s.wall_mark=maxf(float(s.wall_mark),now)
	update_global(now)
	if restored>.01:log_event("offline_dt",{"hours":elapsed/3600,"restored":restored},"离开期间本地没有模拟；储存了 %.1f DT。"%restored)
	return restored

func update_global(now:float) -> void:
	s.global_now=maxf(float(s.global_now),now)
	Progress.global_update(self)

func refresh_metrics() -> void:
	Sim.diagnostics(self)

func create_tender(cells:Array,tags:Array) -> String:return Draft.development(self,cells,tags)
func create_upgrade(id:String,tag:String="") -> String:return Draft.upgrade(self,id,tag)
func create_fusion(id:String) -> String:return Draft.fusion(self,id)
func reroll() -> String:return Draft.reroll(self)
func accept_offer(id:String) -> String:return Draft.accept(self,id)
func opportunity(choice:String) -> String:return Progress.opportunity(self,choice)
func contract_action(id:String,action:String) -> String:return Progress.contract(self,id,action)
func crisis_action(action:String) -> String:return Progress.crisis_action(self,action)
func research(tag:String) -> String:return Progress.research(self,tag)
func add_policy_tag(tag:String) -> String:
	if not tag in s.tag_rewards:return "当前没有这项可纳入的政策。"
	if not tag in s.policy_tags:s.policy_tags.append(tag)
	if s.get("tag_reward_pick_one",false):
		s.tag_rewards=[];s.tag_reward_pick_one=false
	else:s.tag_rewards.erase(tag)
	log_event("policy_acquired",{"tag":tag},"新的招标语言："+c.tag_name(tag))
	return ""

func set_paused(id:String,value:bool) -> String:
	if s.phase!="play" or not s.projects.has(id):return "项目不可用。"
	s.projects[id].paused=value
	log_event("project_pause",{"id":id,"paused":value},"调整了项目运行状态；已建结构的时间负载仍然存在。")
	refresh_metrics();return ""

func rush(id:String) -> String:
	if s.phase!="play" or not s.projects.has(id):return "项目不可用。"
	var p=s.projects[id]
	if p.job.is_empty():return "没有正在进行的工程。"
	if p.job.get("rushed",false):return "这项工程已经压缩过工期。"
	var cost=45.0+float(p.job.work)*35
	if s.currency<cost:return "通货不足。"
	s.currency-=cost;s.totals.currency_spent+=cost
	p.job.work*=.6;p.job.rushed=true
	log_event("project_rush",{"id":id,"cost":cost},"压缩剩余工作时间40%；材料需求不变，仍需实际施工。")
	return ""

func demolish(id:String) -> String:
	if s.phase!="play" or not s.projects.has(id):return "项目不可用。"
	if not s.draft.is_empty() and s.draft.get("target","")==id:return "先完成或关闭这次升级选择。"
	var p=s.projects[id]
	if s.currency<35:return "拆除需要35通货。"
	s.currency-=35
	Inv.add(s.stocks,"material",["light","basic"],float(p.spend_material)*.25,stock_cap("material"))
	var site=s.sites[p.site]
	for cell in site.cells:s.cells[cell].site=""
	s.displaced+=float(p.get("residents",0))
	s.sites.erase(p.site);s.projects.erase(id)
	var remove=[]
	for rid in s.relations:
		if id in [s.relations[rid].a,s.relations[rid].b]:remove.append(rid)
	for rid in remove:s.relations.erase(rid)
	var events=[]
	for event in s.events:
		if event.project!=id:events.append(event)
	s.events=events
	log_event("demolish",{"id":id},"拆除完成，回收实际已投入材料的25%；历史升级和关系不复制到新项目。")
	refresh_metrics();return ""

func stock_cap(resource:String) -> float:
	var cap=float(c.balance.stock_caps.get(resource,0))
	for id in s.projects:
		var p=s.projects[id]
		if p.status=="running":cap+=float(project_stats(id).storage.get(resource,0))
	return cap

func milestone_status(id:String) -> Dictionary:
	return Progress.milestone(self,id)
func checklist() -> Array:return Progress.checklist(self)

func validate_state(value) -> String:
	if not value is Dictionary:return "存档不是对象。"
	if value.get("schema",0)!=2:return "这份存档属于旧B版。人口规则改变，请保留旧档并在B人口版新开一局。"
	for key in ["rng","cells","sites","projects","stocks","relations","contracts","draft","settings","research","crisis","totals","metrics"]:
		if not value.get(key) is Dictionary:return "存档缺少 "+key
	for key in ["policy_tags","origin_choices","researched","tag_rewards","events","log","revealed_resources"]:
		if not value.get(key) is Array:return "存档列表损坏："+key
	for key in ["seed","dt","currency","local_hours","global_now","wall_mark","support","mandate","knowledge","next_id","remainder","stable_hours","shortage_hours","shortage_ratio","tender_debt","simulation_tick"]:
		if not _number(value.get(key)):return "存档数值无效："+key
	if not value.get("phase","") in ["select_block","origin","play","failed"]:return "未知游戏阶段。"
	if not value.get("complete") is bool:return "完成状态损坏。"
	if value.projects.size()>128 or value.cells.size()>128 or value.relations.size()>8192 or value.log.size()>2100:return "存档规模超出当前版本。"
	if float(value.dt)<0 or float(value.dt)>float(c.balance.dt_cap)+.0001:return "DT越界。"
	if float(value.local_hours)<0 or float(value.remainder)<-.0001:return "本地时间越界。"
	if float(value.support)<0 or float(value.support)>100 or float(value.mandate)<0 or float(value.mandate)>100:return "治理状态越界。"
	for stream in ["opening","draft","events","risk"]:
		if not _number(value.rng.get(stream)) or int(value.rng[stream])<1 or int(value.rng[stream])>=2147483647:return "随机状态损坏。"
	for setting in ["lab_speed","load_scale","relationship_scale"]:
		if not _number(value.settings.get(setting)) or float(value.settings[setting])<0 or float(value.settings[setting])>1000:return "实验设置损坏。"
	for total in ["built","upgrades","fusions","rerolls","dt_used","currency_spent"]:
		if not _number(value.totals.get(total)) or float(value.totals[total])<0:return "计数损坏。"
	if value.phase!="select_block":
		if not _number(value.get("block_index")) or not int(value.block_index) in [0,1,2] or value.cells.size()!=61:return "地图损坏。"
		if value.phase!="origin" and not c.origins.has(value.get("origin","")):return "起源损坏。"
	for key in value.cells:
		var tile=value.cells[key]
		if not tile is Dictionary or not _number(tile.get("q")) or not _number(tile.get("r")):return "地块损坏。"
		if not tile.get("zone","") in ["","settlement","production","circulation","public"] or not tile.get("terrain","") in ["plain","forest","hill","wet","water"]:return "地块类型无效。"
		if tile.get("site","")!="" and not value.sites.has(tile.site):return "地块引用损坏。"
	var occupied={}
	for key in value.sites:
		var site=value.sites[key]
		if not site is Dictionary or not site.get("cells") is Array or not site.get("project_ids") is Array:return "场地损坏。"
		if site.cells.is_empty() or site.cells.size()>7:return "场地大小错误。"
		for cell in site.cells:
			if not value.cells.has(cell) or occupied.has(cell) or value.cells[cell].site!=key:return "场地重叠或引用损坏。"
			occupied[cell]=true
		for project in site.project_ids:
			if not value.projects.has(project):return "场地项目丢失。"
	for id in value.projects:
		var p=value.projects[id]
		if not p is Dictionary:return "项目不是对象。"
		if not c.projects.has(p.get("def","")) or not value.sites.has(p.get("site","")):return "项目引用损坏。"
		if p.get("id","")!=id or not p.get("status","") in ["construction","running","upgrading"]:return "项目身份损坏。"
		for field in ["effects","extra_tags","upgrades","history","opened_milestones"]:
			if not p.get(field) is Array:return "项目历史损坏。"
		for field in ["level","usage","size_scale","effective_hours","high_usage_hours","spend_material","spend_goods"]:
			if not _number(p.get(field)) or float(p[field])<0:return "项目计数损坏。"
		if not p.get("outputs_last") is Dictionary or not p.get("produced") is Dictionary:return "项目产出损坏。"
		if not _job_valid(p.get("job")):return "施工记录损坏。"
		for upgrade in p.upgrades:
			if not c.upgrades.has(upgrade):return "升级内容不存在。"
	if not _number(value.get("stage")) or not int(value.stage) in [0,1,2]:return "聚落阶段损坏。"
	if not _number(value.get("displaced")) or value.displaced<0:return "待安置人口损坏。"
	for p in value.projects.values():
		for field in ["residents","health","education","happiness"]:
			if not _number(p.get(field)) or p[field]<0:return "人口记录损坏。"
		if p.health>100 or p.education>100 or p.happiness>100:return "人口属性越界。"
	for stock in value.stocks:
		if not _number(value.stocks[stock]) or float(value.stocks[stock])<-.00001:return "库存损坏。"
		if not str(stock).split("|")[0] in ["food","material","goods"]:return "库存资源类型无效。"
	for id in value.relations:
		var rel=value.relations[id]
		if not rel is Dictionary or not value.projects.has(rel.get("a","")) or not value.projects.has(rel.get("b","")) or not c.fusions.has(rel.get("template","")):return "关系引用损坏。"
		if not _number(rel.get("progress")) or not _number(rel.get("required")):return "关系进度损坏。"
	for event in value.events:
		if not event is Dictionary or not c.opportunities.has(event.get("id","")) or not value.projects.has(event.get("project","")):return "事件引用损坏。"
	for task in value.contracts.values():
		if not task is Dictionary:return "委托损坏。"
		for field in ["deadline","currency","dt","reward","penalty"]:
			if not _number(task.get(field)):return "委托数值损坏。"
	if not _job_valid(value.research):return "研究记录损坏。"
	if not value.draft.is_empty():
		if not value.draft.get("kind","") in ["development","upgrade","fusion"] or not value.draft.get("offers") is Array or not value.draft.get("cells") is Array or not value.draft.get("tags") is Array:return "手牌损坏。"
		if value.draft.kind=="upgrade" and not value.projects.has(value.draft.get("target","")):return "升级目标丢失。"
		if value.draft.kind=="fusion" and not value.relations.has(value.draft.get("target","")):return "协作目标丢失。"
		for offer in value.draft.offers:
			if value.draft.kind=="development" and not c.projects.has(offer):return "投标内容不存在。"
			if value.draft.kind=="upgrade" and not c.upgrades.has(offer):return "升级投标不存在。"
	return ""

static func _number(value) -> bool:
	return (value is int or value is float) and is_finite(float(value)) and absf(float(value))<1e15

static func _job_valid(job) -> bool:
	if not job is Dictionary:return false
	if job.is_empty():return true
	for key in ["work","total","material","goods","energy","progress"]:
		if not _number(job.get(key)) or float(job[key])<-.00001:return false
	return job.get("kind","") in ["build","upgrade","research"]

func stage_up() -> String:
	if not Population.can_town(self):return "需要30人口、平均教育25和稳定供给1小时。可以继续经营现阶段，不必冲刺。"
	Population.advance_stage(self,2);refresh_metrics();return ""

func toggle_migration() -> void:
	s.migration_paused=not s.migration_paused
	log_event("migration_policy",{"paused":s.migration_paused},"暂停新迁入；现有居民仍正常生活。" if s.migration_paused else "重新接纳新居民。")
