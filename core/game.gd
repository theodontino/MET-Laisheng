extends RefCounted
## B 0.2 authority: the original Game API now hosts the agreed entities.
const Catalog=preload("res://core/catalog.gd")
const Hex=preload("res://core/hex.gd")
const Rng=preload("res://core/rng.gd")
const Rules=preload("res://core/rules.gd")
const Fields=preload("res://core/fields.gd")
const Alloc=preload("res://core/allocation.gd")
const Draft=preload("res://core/tenders.gd")
const Sim=preload("res://core/simulation.gd")
const Population=preload("res://core/population.gd")
const Progress=preload("res://core/progress.gd")
const Civic=preload("res://core/civic032.gd")
const Development=preload("res://core/development040.gd")
const Construction=preload("res://core/construction040.gd")
const Bonus=preload("res://core/bonus_economy040.gd")
const Commissions=preload("res://core/commissions040.gd")
const Market=preload("res://core/market040.gd")
const Block=preload("res://core/block040.gd")
const Clock=preload("res://core/time_model.gd")
const Themes=preload("res://core/commission_theme.gd")
const Cycles=preload("res://core/cycles.gd")
var c=Catalog.new()
var s:Dictionary={}
var compiled:Dictionary={}
## Derived source-aware spatial projection; never serialized in the city state.
var spatial_cache:Dictionary={}
var last_error:String=""
var preview_mode:bool=false
var recording_cycle:bool=false
var revision:int=0
## UI forecasts commands between commits; a fresh committed tick needs no rerun.
var last_committed_revision:int=-1

func new_game(seed_value:int,now:float) -> void:
	var v=absi(seed_value)%2147483000+1
	s={"schema":6,"build":c.data.build,"seed":seed_value,"phase":"select_block","block_index":-1,
		"rng":{"opening":v,"draft":(v+1701)%2147483646+1,"events":(v+5303)%2147483646+1},
		"wall_mark":now,"global_now":now,"started_at":now,"local_hours":0.,"remainder":0.,"simulation_tick":0,
		"currency":float(c.balance.initial_money),"dt":float(c.balance.initial_dt),"support":75.,"mandate":75.,
		"block":{"id":"block:1","traits":["block.river_valley"],"labels":[],"policies":{"Tax":"tax_head","Labor":"jobs_access","Environment":"pollution_default","Procurement":"procure_near","Land":"land_normal"}},
		"cells":{},"sites":{},"projects":{},"groups":{},"organizations":{},"fields":{},"authored_fields":{},"bonus_instances":{},
		"next_id":1,"origin_choices":[],"origin":"","draft":{},"tender_debt":0,"log":[],"events":[],"event_cursor":0,
		"metrics":{},"audit":{},"complete":false,"failed_reason":"","crisis":{},"governance_bad_hours":0.,
		"totals":{"built":0,"installed":0,"rerolls":0,"dt_used":0.,"currency_spent":0.},"block_opportunities":0,"award_level":0,
		"debug_used":false,"settings":{"sound":true,"lab_speed":1.},"migration_paused":false,"knowledge":0.,"stage":0,"goods_active":false,"goods_available":false,"quote_bank":{},"goods_requested_at":-1.,"city_goal":{},"arrival_deferred":false}
	for oid in Rules.ids(c.operators):s.organizations[oid]={"id":oid,"traits":c.operators[oid].traits.duplicate(),"labels":[]}
	Block.ensure(self)
	Clock.ensure(self);Themes.ensure(self);Cycles.ensure(self)
	compiled={};log_event("new_game",{"seed":seed_value},"河谷新章：安顿居民，建立供给，选择这座城的发展方向。")
func uid(prefix:String) -> String:
	var result=prefix+"%06d"%int(s.next_id);s.next_id=int(s.next_id)+1;return result
func log_event(kind:String,payload:Dictionary={},text:String="") -> void:
	s.log.append({"tick":s.simulation_tick,"h":s.local_hours,"kind":kind,"data":payload.duplicate(true),"text":text})
	if s.log.size()>1200:s.log.pop_front()
	Civic.event(self,kind,payload,text)
func select_block(index:int) -> String:
	if s.phase!="select_block" or index<0 or index>2:return "起点已确定。"
	s.block_index=index;s.cells=Hex.make_map(index,int(s.seed))
	s.block.geography=Hex.geography(s.cells,int(s.seed),index)
	s.block.display_name=["河湾","林缘旧村","北部高台"][index]
	for key in s.cells:
		var cell=s.cells[key];var ts=[]
		match str(cell.terrain):
			"plain":ts.append("tile.fertile")
			"forest":ts.append("tile.forest")
			"hill":ts.append("tile.highland")
			"wet":ts.append("tile.wet")
			"water":ts.append("tile.water")
		if cell.waterfront:ts.append("tile.waterfront")
		cell.traits=ts;cell.labels=[];cell.fields={};cell.building=""
	s.origin_choices=Rng.shuffled(s,"opening",Rules.ids(c.origins)).slice(0,2);s.phase="origin"
	return ""
func choose_origin(id:String) -> String:
	if s.phase!="origin" or not id in s.origin_choices:return "不是本次起源。"
	var origin=c.origins[id];s.origin=id;s.phase="play";s.currency+=float(origin.cash)
	_create_special("municipal","市政储备",false)
	_create_special("homeless","待安置住区",true)
	for r in ["food","material","goods"]:s.projects.municipal.inventory[r]=float(c.balance.initial_stock[r])+float(origin.get(r,0))
	var home=_create_starter("homestead",["0,1"],"commons")
	var anchor="workshop"
	_create_starter(anchor,["0,0" if anchor!="timber" else "1,1"],"civic" if id=="charter" else ("farmers" if anchor=="farm" else "steel"))
	Population.create(self,home,float(c.balance.initial_people))
	refresh_metrics();Progress.initialize(self);Civic.ensure(self);s.block.cycles.baseline=Cycles._snapshot(self);log_event("origin",{"id":id},"18位居民已安顿。先看粮食和空房，再决定河谷的下一步。")
	return ""
func _create_special(id:String,name:String,home:bool) -> void:
	var base=c.projects.homestead.stats.duplicate(true)
	for k in base:base[k]=0.
	base.efficiency=1.;base.personnel_mult=1.;base.exposure_multiplier=1.;base.shortage_multiplier=1.;base.population_maintenance=float(c.balance.population_maintenance)
	base.population_food=c.balance.population_food;base.population_material=c.balance.population_material;base.population_goods=c.balance.population_goods;base.population_energy=c.balance.population_energy
	for r in ["food","material","goods"]:base["storage."+r]=0. if home else float(c.balance.municipal_capacity[r])
	base.housing=0. if not home else 1000000.;base["output.energy"]=float(c.balance.base_energy) if not home else 0.
	s.projects[id]={"id":id,"name":name,"def":"__"+id,"tile":"offmap:"+id,"site":"","traits":["building.residential"] if home else ["building.warehouse"],"labels":[],"operator_org":"civic","special":true,"base_stats":base,"inventory":{"food":0.,"material":0.,"goods":0.},"status":"running","paused":false,"job":{},"slots":{},"usage":1.,"bottleneck":"","next_heat_bonus":0.,"next_heat_credit":0.,"outputs_last":{},"history":[]}
func _create_starter(def_id:String,cells:Array,operator:String="commons") -> String:
	return create_building(def_id,str(cells[0]),operator,true).id
func create_building(def_id:String,tile:String,operator:String,ready:bool=false) -> Dictionary:
	var def=c.projects[def_id];var id=uid("B");var site=uid("S")
	var p={"id":id,"def":def_id,"tile":tile,"site":site,"traits":def.traits.duplicate(),"labels":[],"operator_org":operator,"special":false,
		"inventory":{"food":0.,"material":0.,"goods":0.},"slots":{},"slot_types":{},"work_priority":0,"level":0,
		"status":"running" if ready else "construction","paused":false,"job":{},"usage":1. if ready else 0.,"bottleneck":"尚未运行",
		"next_heat_bonus":0.,"next_heat_credit":0.,"outputs_last":{},"history":[],"effective_hours":0.,"size_scale":1.}
	if not ready:p.job={"work":float(def.construction_required),"total":float(def.construction_required),"material":float(def.material),"energy":.5,"cp":true}
	Development.ensure_project(self,p)
	s.projects[id]=p;s.sites[site]={"id":site,"cells":[tile],"project_ids":[id],"zone":def.zone}
	s.cells[tile].building=id;s.cells[tile].site=site;s.cells[tile].zone=def.zone
	if ready:s.totals.built+=1
	return p
func playable_ids() -> Array:
	var result=[]
	for id in Rules.ids(s.projects):
		if not s.projects[id].special:result.append(id)
	return result
func project_name(id:String) -> String:
	var p=s.projects.get(id,{})
	if p.is_empty():
		if s.cells.has(id):return "地块 "+id
		if s.groups.has(id):return "人群 "+id
		if c.operators.has(id):return c.operators[id].name
		if id==s.block.id:return str(s.block.get("display_name","河谷地区"))
		return "已移除"
	return str(p.get("name",c.projects.get(p.def,{}).get("name",id)))
func create_tender(cells:Array,base:String) -> String:return Draft.open_build(self,cells,base)
func create_refit(id:String,slot:String) -> String:return Draft.open_refit(self,id,slot)
func create_block_offer(host_type:String="block",host_id:String="block:1") -> String:return Draft.open_scope(self,host_type,host_id)
func accept_offer(offer_id:String) -> String:return Draft.accept(self,offer_id)
func reroll() -> String:return Draft.reroll(self)
func cancel_draft() -> String:
	if s.draft.is_empty():return ""
	Draft.remember(self)
	s.draft={};return ""
func bonus_eligible(bonus_id:String,host_type:String,host_id:String,slot:String="",replacing:String="") -> String:
	if not c.bonuses.has(bonus_id):return "未知方案。"
	var def=c.bonuses[bonus_id]
	if not host_type in def.hosts:return "该方案不能挂载于此尺度。"
	if host_type=="building":
		if not s.projects.has(host_id) or s.projects[host_id].special:return "无效建筑。"
		var p=s.projects[host_id]
		if slot=="":return "请选择一个已解锁的方案槽。"
		if p.status!="running":return "建筑完工后才能部署方案。"
		if not Rules.any_trait(Rules.semantic_traits(s,host_id),def.traits):return "基础建筑类型不适用。"
		if not Rules.condition(s,host_id,def.get("install_gate",def.get("gate",{}))):return "场地条件不满足。"
		if def.get("install_gate",def.get("gate",{})).get("has_inputs",false):
			var any_input=false
			for k in c.projects[p.def].stats:
				if k.begins_with("input.") and float(c.projects[p.def].stats[k])>0:any_input=true
			if not any_input:return "本建筑没有常规原料配方。"
		if slot!="":
			if not p.slots.has(slot):return "插槽不存在。"
			if slot_type(host_id,slot)=="Flex":
				if not def.slot in ["Production","Logistics"]:return "弹性槽只接受生产或物流方案。"
			elif not slot_type(host_id,slot) in def.allowed_slots:return "方案与插槽类型不匹配。"
	else:
		if host_type=="organization" and not s.organizations.has(host_id):return "未知运营商。"
		if host_type=="block" and host_id!=s.block.id:return "未知Block。"
		if not def.traits.is_empty():
			var matched=false
			for id in playable_ids():
				if host_type=="organization" and s.projects[id].operator_org!=host_id:continue
				if Rules.any_trait(Rules.semantic_traits(s,id),def.traits):matched=true
			if not matched:return "目前没有可受益的建筑。"
	var duplicate_error=Bonus.duplicate_error(self,bonus_id,host_type,host_id,replacing)
	if duplicate_error!="":return duplicate_error
	for ins in s.bonus_instances.values():
		if ins.id==replacing or not ins.active or ins.host_type!=host_type or ins.host_id!=host_id:continue
		if ins.bonus_id==bonus_id:continue
		if ins.bonus_id in def.excludes or bonus_id in c.bonuses[ins.bonus_id].excludes:return "与当前方案互斥。"
	return ""
func mount_bonus(bonus_id:String,host_type:String,host_id:String,provider:String,slot:String="") -> String:
	return Bonus.install(self,bonus_id,host_type,host_id,slot)
func _install_bonus(bonus_id:String,host_type:String,host_id:String,provider:String,slot:String="") -> String:
	if not s.organizations.has(provider):return "未知方案提供方。"
	var existing=""
	if host_type=="building" and s.projects.has(host_id):existing=str(s.projects[host_id].slots.get(slot,""))
	var error=bonus_eligible(bonus_id,host_type,host_id,slot,existing)
	if error!="":return error
	if host_type=="building" and not existing.is_empty():
		var p=s.projects[host_id];var old=c.bonuses[s.bonus_instances[existing].bonus_id]
		for rule in old.rules:
			if rule.op=="slot_add" and p.slots.get("Flex","")!="":return "请先清空弹性槽，再更换提供它的模块化方案。"
		_unmount(existing)
	var iid=uid("I")
	s.bonus_instances[iid]={"id":iid,"bonus_id":bonus_id,"host_type":host_type,"host_id":host_id,"provider_org":provider,"active":true,"slot":slot}
	if host_type=="building":
		s.projects[host_id].slots[slot]=iid
		for rule in c.bonuses[bonus_id].rules:
			if rule.op=="slot_add" and not s.projects[host_id].slots.has(rule.name):
				s.projects[host_id].slots[rule.name]="";s.projects[host_id].slot_types[rule.name]="Flex"
	s.totals.installed+=1;refresh_metrics();log_event("bonus",{"instance":iid},c.bonuses[bonus_id].name+"已挂载。")
	return ""
func _unmount(iid:String) -> void:
	if not s.bonus_instances.has(iid):return
	var i=s.bonus_instances[iid];i.active=false
	if i.host_type=="building" and s.projects.has(i.host_id):
		var p=s.projects[i.host_id];p.slots[i.slot]=""
		for rule in c.bonuses[i.bonus_id].rules:
			if rule.op=="slot_add":
				var retained=false
				for other in s.bonus_instances.values():
					if not other.active or other.host_type!="building" or other.host_id!=i.host_id:continue
					for other_rule in c.bonuses[other.bonus_id].rules:
						if other_rule.op=="slot_add" and other_rule.name==rule.name:retained=true
				if not retained:p.slots.erase(rule.name);p.get("slot_types",{}).erase(rule.name)
func clear_slot(id:String,slot:String) -> String:
	if s.phase!="play" or not s.projects.has(id) or not s.projects[id].slots.has(slot):return "当前不可拆卸。"
	var iid=str(s.projects[id].slots[slot])
	if iid=="":return ""
	for r in c.bonuses[s.bonus_instances[iid].bonus_id].rules:
		if r.op=="slot_add" and s.projects[id].slots.get(r.name,"")!="":return "请先清空依赖此方案的弹性槽。"
	_unmount(iid);refresh_metrics();Population.rehouse(self);return ""
func switch_policy(policy:String) -> String:
	if s.phase!="play" or not c.policies.has(policy):return "当前不能更换政策。"
	var def=c.policies[policy]
	if s.block.policies.get(def.slot,"")==policy:return ""
	if s.currency<float(c.balance.policy_switch_price):return "政策调整经费不足。"
	s.currency-=float(c.balance.policy_switch_price);s.block.policies[def.slot]=policy;refresh_metrics()
	log_event("policy",{"id":policy},"采用「"+def.name+"」。原政策失效，不发额外奖励。")
	return ""
func set_paused(id:String,value:bool) -> String:
	if s.phase!="play" or not s.projects.has(id) or s.projects[id].special:return "不能操作这个建筑。"
	s.projects[id].paused=value;refresh_metrics();return ""
func demolish(id:String) -> String:
	if s.phase!="play" or not s.projects.has(id) or s.projects[id].special:return "不能拆除此建筑。"
	if s.currency<20:return "需要20通货处理拆除。"
	s.currency-=20;var p=s.projects[id]
	for group in s.groups.values():
		if group.host==id:group.host="homeless"
		group.employment.erase(id)
	for iid in Rules.ids(s.bonus_instances):
		if s.bonus_instances[iid].host_type=="building" and s.bonus_instances[iid].host_id==id:s.bonus_instances[iid].active=false
	var inventory_moved={};var inventory_lost={}
	var warehouses=["municipal"]
	for bid in playable_ids():
		if bid==id or s.projects[bid].status!="running" or s.projects[bid].paused:continue
		if "building.logistics" in s.projects[bid].traits or compiled.config[bid].get("overflow_relay",false):warehouses.append(bid)
	for resource in p.inventory:
		var remaining=float(p.inventory[resource]);var total=remaining
		for warehouse in warehouses:
			var cap=stock_cap_for(warehouse,resource);var old=float(s.projects[warehouse].inventory.get(resource,0));var amount=minf(remaining,maxf(0,cap-old))
			s.projects[warehouse].inventory[resource]=old+amount;remaining-=amount
		inventory_moved[resource]=total-remaining;inventory_lost[resource]=remaining
	s.cells[p.tile].building="";s.cells[p.tile].site="";s.cells[p.tile].zone="";s.cells[p.tile].labels.append("former:"+p.def)
	for fid in Rules.ids(s.authored_fields):
		if s.authored_fields[fid].source_id==id:s.authored_fields.erase(fid)
	s.sites.erase(p.site);s.projects.erase(id);refresh_metrics();Population.rehouse(self);refresh_metrics()
	var waste=0.
	for n in inventory_lost.values():waste+=float(n)
	log_event("demolish",{"id":id,"moved":inventory_moved,"lost":inventory_lost},"原住户保留并重新安置。库存已尝试转仓"+("；仓储不足，损耗%.1f单位。"%waste if waste>.001 else "，无库存损失。"))
	return ""
func toggle_migration() -> void:s.migration_paused=not s.migration_paused
func inventory_total(resource:String) -> float:
	var total=0.
	for id in Rules.ids(s.projects):total+=float(s.projects[id].inventory.get(resource,0))
	return total
func stock_cap_for(id:String,resource:String) -> float:
	if compiled.is_empty():compiled=Rules.compile(self)
	return Sim.storage_cap(compiled,id,resource)
func stock_cap(resource:String) -> float:
	var total=0.
	for id in Rules.ids(s.projects):total+=stock_cap_for(id,resource)
	return total
func project_stats(id:String) -> Dictionary:
	if compiled.is_empty():compiled=Rules.compile(self)
	return compiled.stats.get(id,{})
func refresh_metrics() -> void:
	revision+=1
	if s.projects.is_empty():return
	for id in playable_ids():s.projects[id].labels.erase("use.commercial")
	compiled=Rules.compile(self)
	var changed=false
	for id in playable_ids():
		if compiled.config[id].get("commercial_use",false):
			s.projects[id].labels.append("use.commercial");changed=true
	if changed:compiled=Rules.compile(self)
	var m=s.metrics.duplicate(true)
	m.merge(Population.summary(self),true)
	s.metrics=m

func temporal_load() -> float:
	# Compatibility price API now reports DT per economic month, not city-size tax.
	return 1555200.0 / float(c.beta.time.earth_seconds_per_dt)
func advance_active(real_seconds:float) -> Dictionary:
	return Clock.advance(self,real_seconds)
func pulse(months:float) -> Dictionary:
	# Existing callers request a local duration; route it through the same clock engine.
	if not is_finite(months) or months<=0:return {"advanced":0.0,"spent":0.0,"reason":"无效推进。"}
	var result=Clock.advance(self,months*1555200.0/25920.0,"advance")
	return result
func advance_online(hours:float) -> void:
	Clock.advance(self,hours*3600.0,"normal")
func update_global(now:float) -> void:s.global_now=maxf(float(s.global_now),now)
func resume_time(now:float) -> float:
	return Clock.resume(self,now)
func validate_state(value:Dictionary) -> String:
	var journal_error=Civic.validate(value)
	if journal_error!="":return journal_error
	if int(value.get("schema",0))!=6:return "这是其他 pre-alpha 版本的存档，请开始新游戏。"
	for k in ["projects","groups","fields","authored_fields","bonus_instances","organizations","block","cells","rng","metrics","totals","draft","settings"]:
		if not value.get(k) is Dictionary:return "存档缺少对象："+k
	for k in ["currency","dt","local_hours","next_id","wall_mark","simulation_tick","remainder","global_now","event_cursor","block_opportunities","support","mandate"]:
		if not is_num(value.get(k)):return "存档数值损坏："+k
	if not value.get("phase","") in ["select_block","origin","play","failed"]:return "未知游戏阶段。"
	if value.dt<0 or value.local_hours<0:return "时间预算不能为负。"
	if not value.get("beta_time") is Dictionary:return "缺少Beta时间状态。"
	for key in ["local_elapsed_seconds","active_real_seconds","offline_recorded_seconds","last_seen_utc","segments_committed"]:
		if not is_num(value.beta_time.get(key)) or value.beta_time[key]<0:return "时间记录损坏："+key
	if not str(value.beta_time.get("mode","")) in ["normal","observe","advance","burn"]:return "时间档位损坏。"
	if not value.block.get("cycles") is Dictionary:return "年度记录损坏。"
	var cy:Dictionary=value.block.cycles
	for key in ["year","major_cycle"]:
		if not is_num(cy.get(key)) or cy[key]<0:return "周期计数损坏。"
	for key in ["history","population_awards","pending_summaries"]:
		if not cy.get(key) is Array:return "周期记录损坏："+key
	if not cy.get("population_stability") is Dictionary:return "人口里程碑记录损坏。"
	for elapsed in cy.population_stability.values():
		if not is_num(elapsed) or elapsed<0:return "人口持续时间损坏。"
	for grade in value.block.get("blueprint_levels",{}).values():
		if not is_num(grade) or grade<0 or grade>2 or grade!=floor(grade):return "蓝图等级损坏。"
	var reward_ids={};var reward_sources={}
	for reward in value.block.get("reward_choices",[]):
		if not reward is Dictionary or not reward.get("choices") is Array:return "奖励记录损坏。"
		var rid=str(reward.get("reward_id",""));var source=str(reward.get("source",""))
		if rid=="" or source=="" or reward_ids.has(rid) or reward_sources.has(source):return "奖励来源重复或损坏。"
		reward_ids[rid]=true;reward_sources[source]=true
	if value.block.has("ending"):
		var ending=value.block.ending
		if not ending is Dictionary:return "尾声记录损坏。"
		if ending.get("triggered",false) and not is_num(ending.get("local_elapsed_seconds")):return "尾声时间损坏。"
	if value.block.has("capstone"):
		var landmark=value.block.capstone
		if not landmark is Dictionary or not Block.capstone_definitions().has(str(landmark.get("theme",""))):return "定型建筑记录损坏。"
		if not value.block.get("ending",{}).get("capstone_claimed",false):return "定型建筑缺少领取记录。"
	if not value.block.get("policies") is Dictionary:return "政策记录损坏。"
	for key in ["quote_bank","city_goal"]:
		if value.has(key) and not value[key] is Dictionary:return "新版本字段损坏："+key
	if value.has("city_goal") and not value.city_goal.is_empty():
		var goal=value.city_goal
		if not c.data.city_goals.has(goal.get("route","")):return "发展方向损坏。"
		for key in ["stable","community","renewal"]:
			if not is_num(goal.get(key)) or goal[key]<0:return "发展进度损坏。"
		if not goal.get("anchors") is Array or not goal.get("awards") is Array or not goal.get("baseline") is Dictionary:return "城市成果记录损坏。"
		for key in goal.anchors:
			if not goal.baseline.get(key) is Dictionary:return "原住民基线损坏。"
			for stat in ["health","pollution","service"]:
				if not is_num(goal.baseline[key].get(stat)):return "原住民基线数值损坏。"
	if int(value.schema)==6:
		for key in ["bonus_wallet","deployed_bonuses","commissions"]:
			if not value.block.get(key) is Dictionary:return "地区成长数据损坏："+key
		for key in ["reward_choices","trade_orders","trade_history"]:
			if not value.block.get(key) is Array:return "地区记录损坏："+key
		for bid in value.block.bonus_wallet:
			var entry=value.block.bonus_wallet[bid]
			if not c.bonuses.has(bid) or not entry is Dictionary or not is_num(entry.get("charges")) or entry.charges<0:return "方案许可记录损坏。"
		for p in value.projects.values():
			if p.get("special",false):continue
			if not p.get("upgrade_job") is Dictionary or not p.get("slot_track") is Array:return "建筑成长记录损坏。"
			if not p.upgrade_job.is_empty():
				if not p.get("upgrade_choice","") in c.projects.get(p.get("def",""),{}).get("upgrades",[]):return "升级方向损坏。"
				for key in ["work","total","material"]:
					if not is_num(p.upgrade_job.get(key)) or p.upgrade_job[key]<0:return "升级施工记录损坏。"
	for oid in value.organizations:
		if not c.operators.has(oid):return "未知运营组织。"
	if value.phase in ["play","failed"]:
		for id in ["homeless","municipal"]:
			if not value.projects.has(id):return "缺少市政/待安置容器。"
	for tile in value.cells.values():
		if not tile is Dictionary or not tile.get("traits") is Array or not tile.get("fields") is Dictionary:return "地块记录损坏。"
	for id in value.projects:
		var p=value.projects[id]
		if not p is Dictionary:return "建筑记录不是对象。"
		for k in ["inventory","slots","job"]:
			if not p.get(k) is Dictionary:return "建筑字段损坏："+k
		if p.get("id","")!=id or not p.get("traits") is Array or not p.get("labels") is Array:return "建筑身份损坏。"
		if not value.organizations.has(p.get("operator_org","")):return "未知运营者。"
		if not p.get("status","") in ["construction","running"]:return "建筑状态损坏。"
		if not p.get("special",false):
			if not c.projects.has(p.get("def","")) or not value.cells.has(p.get("tile","")):return "建筑类型或位置损坏。"
			if value.cells[p.tile].get("building","")!=id:return "地块与建筑引用不一致。"
		if p.has("slot_types"):
			if not p.slot_types is Dictionary:return "槽位类型损坏。"
			for slot in p.slots:
				if not str(p.slot_types.get(slot,slot.split(":")[0])) in ["Production","Logistics","Operations","Flex"]:return "未知方案槽类型。"
		if p.has("work_priority") and (not is_num(p.work_priority) or p.work_priority < -1 or p.work_priority>1):return "用工优先级损坏。"
		for amount in p.inventory.values():
			if not is_num(amount) or amount<-.000001:return "库存不能为负。"
		if p.status=="construction":
			for k in ["work","total","material"]:
				if not is_num(p.job.get(k)) or p.job[k]<0:return "施工数据损坏。"
		for slot in p.slots:
			var iid=p.slots[slot]
			if iid=="":continue
			var ins=value.bonus_instances.get(iid,{})
			if not ins is Dictionary or not ins.get("active",false) or ins.get("host_id","")!=id or ins.get("slot","")!=slot:return "方案槽引用无效。"
	for group in value.groups.values():
		if not group is Dictionary or not group.get("employment") is Dictionary or not group.get("services") is Dictionary:return "人群记录损坏。"
		if not value.projects.has(group.get("host","")):return "人群宿主不存在。"
		for k in ["count","health","education","happiness","efficiency","demand"]:
			if not is_num(group.get(k)) or group[k]<0:return "人群数据损坏。"
		for k in ["health","education","happiness"]:
			if group[k]>100:return "人群状态超出范围。"
		var employed=0.
		for id in group.employment:
			if not value.projects.has(id) or not is_num(group.employment[id]) or group.employment[id]<0:return "就业引用无效。"
			employed+=group.employment[id]
		if employed>group.count+.00001:return "同一Pop被重复分配。"
	for i in value.bonus_instances.values():
		if not i is Dictionary or not c.bonuses.has(i.get("bonus_id","")) or not value.organizations.has(i.get("provider_org","")):return "未知Bonus或提供方。"
		if i.has("contract_org") and not value.organizations.has(i.contract_org):return "方案签约组织不存在。"
		if not i.get("host_type","") in c.bonuses[i.bonus_id].hosts:return "方案宿主类型不兼容。"
		if not i.get("active",false):continue
		if i.host_type=="building":
			if not value.projects.has(i.host_id) or value.projects[i.host_id].slots.get(i.slot,"")!=i.id:return "活跃方案失去建筑槽。"
		elif i.host_type=="organization":
			if not value.organizations.has(i.host_id):return "方案运营组织不存在。"
		elif i.host_id!=value.block.id:return "方案Block不存在。"
	for slot in value.block.policies:
		var pid=value.block.policies[slot]
		if not c.policies.has(pid) or c.policies[pid].slot!=slot:return "政策槽无效。"
	for field in value.authored_fields.values():
		if not field is Dictionary or not field.get("propagation") is Dictionary:return "场定义损坏。"
		if not is_num(field.get("strength")):return "场强度损坏。"
		for k in ["radius","decay"]:
			if not is_num(field.propagation.get(k)):return "场传播参数损坏。"
	return ""

static func is_num(x) -> bool:return (x is float or x is int) and is_finite(float(x))
func debug_grant() -> void:
	s.debug_used=true;s.currency+=500;s.dt=minf(c.balance.dt_cap,s.dt+20)
	for r in ["food","material","goods"]:s.projects.municipal.inventory[r]=minf(stock_cap_for("municipal",r),s.projects.municipal.inventory.get(r,0)+20)
	log_event("debug",{},"使用调试补给；此局不是正常资源验收。")

func define_field(source_id:String,kind:String,strength:float,radius:int,decay:float,origin:String="") -> String:
	if kind=="" or not is_finite(strength) or not is_finite(decay) or radius<0:return ""
	if not s.projects.has(source_id) and not s.cells.has(source_id) and not s.groups.has(source_id) and not s.organizations.has(source_id) and source_id!=s.block.id:return ""
	if origin!="" and not s.cells.has(origin):return ""
	var fid=uid("F")
	s.authored_fields[fid]={"id":fid,"type":kind,"source_id":source_id,"origin":origin,"strength":strength,"propagation":{"radius":radius,"decay":decay,"rule":"authored"},"active":true}
	return fid

# Stable slot identity is separate from type, so changing type never deletes an installed bonus.
func slot_ids(id:String) -> Array:
	return s.projects[id].slots.keys()
func slot_type(id:String,slot:String) -> String:
	return str(s.projects[id].get("slot_types",{}).get(slot,slot.split(":")[0]))
func slot_label(id:String,slot:String) -> String:
	var label=str(c.SLOT_NAMES.get(slot_type(id,slot),slot_type(id,slot)))
	if ":" in slot:label+=" "+slot.get_slice(":",1)
	return label
func operator_layout(org:String) -> String:
	var labels=[]
	for t in c.operators[org].get("slot_track",["Production","Logistics","Operations"]):labels.append({"Production":"生产","Logistics":"物流","Operations":"运营"}[t])
	return " / ".join(labels)
func cohesion(id:String) -> Dictionary:
	var p=s.projects[id];var occupied=0;var own=0
	for slot in p.slots:
		var iid=str(p.slots[slot])
		if iid=="":continue
		occupied+=1;var ins=s.bonus_instances[iid]
		if ins.get("contract_org",ins.provider_org)==p.operator_org:own+=1
	return {"owned":own,"occupied":occupied,"total":p.slots.size(),"active":own==p.slots.size() and own>=3}
func set_priority(id:String,priority:int) -> String:
	if s.phase!="play" or not s.projects.has(id) or s.projects[id].special:return "无法调整。"
	s.projects[id].work_priority=clampi(priority,-1,1);refresh_metrics();return ""
func recontract(id:String,slot:String) -> String:
	if s.phase!="play" or not s.projects.has(id):return "无法重签。"
	var iid=str(s.projects[id].slots.get(slot,""))
	if iid=="":return "空槽无需重签。"
	var ins=s.bonus_instances[iid]
	if ins.get("contract_org",ins.provider_org)==s.projects[id].operator_org:return "已是自营方案。"
	if s.currency<c.balance.recontract_cost:return "重签通货不足。"
	s.currency-=c.balance.recontract_cost;ins.contract_org=s.projects[id].operator_org
	log_event("recontract",{"instance":iid},"方案已重签；原提供方记录保留，能力不变。");refresh_metrics();return ""
func reforge_slot(id:String,slot:String,type:String) -> String:
	return "槽位轨道随建筑成长解锁；常规改型已取消。"
func change_operator(id:String,org:String) -> String:
	return "运营者变更仅由组织事件触发。"
func withdraw_stock(resource:String,amount:float) -> bool:
	if amount<0 or inventory_total(resource)<amount-.000001:return false
	var remaining=amount
	for id in Rules.ids(s.projects):
		var take=minf(remaining,float(s.projects[id].inventory.get(resource,0)))
		s.projects[id].inventory[resource]=float(s.projects[id].inventory.get(resource,0))-take;remaining-=take
	return true
func next_arrival() -> float:
	var cursor=int(s.event_cursor)
	if cursor>=c.balance.group_events_hours.size():return -1.
	return maxf(0,float(c.balance.group_events_hours[cursor])-float(s.local_hours))
func enable_goods() -> String:
	if s.goods_active or float(s.get("goods_requested_at",-1))>=0:return ""
	if not s.get("goods_available",false) and inventory_total("goods")<=0:return "先建立制成品供给。"
	s.goods_requested_at=float(s.local_hours)
	log_event("goods_plan",{},"生活用品计划已公布；2来生月后居民开始消费制成品。");refresh_metrics();return ""
func next_checkpoint() -> Dictionary:
	var hours=3.;var name="观察城市运行"
	for id in playable_ids():
		var p=s.projects[id]
		if p.status!="construction":continue
		var h=float(p.job.work)/maxf(.05,Construction.capacity(self))
		if h<hours:hours=h;name=project_name(id)+"预计完工"
	for row in Construction.queue(self):
		if row.paused:continue
		var h=float(row.work)/maxf(.05,Construction.capacity(self))
		if h<hours:hours=h;name=row.name+"预计完工"
	var arrival=next_arrival()
	if not s.migration_paused and arrival>=0 and arrival<hours:hours=maxf(.1,arrival);name="下一批居民抵达"
	if float(s.get("goods_requested_at",-1))>=0 and not s.goods_active:
		var remain=float(s.goods_requested_at)+float(c.balance.goods_grace_hours)-float(s.local_hours)
		if remain>0 and remain<hours:hours=remain;name="生活用品需求生效"
	return {"hours":snappedf(maxf(.1,hours),.1),"name":name,"cost":hours*temporal_load()}
func migrate_state(value:Dictionary) -> Dictionary:
	# Pre-alpha saves are version-local. This API name remains the UI load boundary.
	if int(value.get("schema",0))!=6 or value.get("build","")!=c.data.build or value.has("district"):
		return {"ok":false,"error":"这是其他 pre-alpha 版本的存档，请开始新游戏。"}
	var error=validate_state(value)
	if error!="":return {"ok":false,"error":error}
	s=value.duplicate(true);Clock.ensure(self);Themes.ensure(self);Cycles.ensure(self);s.beta_time.mode="normal";s.settings.user_paused=true;compiled=Rules.compile(self);revision+=1
	return {"ok":true,"state":s,"migrated":false}
