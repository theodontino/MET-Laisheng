extends RefCounted
## 0.4 city actions on the existing map. All displayed state comes from Game snapshots/APIs.
const W=preload("res://ui/widgets.gd")
const C=preload("res://ui/components031/components.gd")
const D=preload("res://ui/components031/design.gd")
const Art=preload("res://ui/map/art.gd")
const RES_NAMES={"food":"粮食","material":"材料","goods":"制成品"}
const GOAL_NAMES={"food_ratio":"口粮满足","housed":"已安置居民","homeless":"待安置居民","medical":"医疗覆盖","energy_ratio":"电力满足","delivery_goods":"制成品交付","organization_links":"组织联系","adverse_exports":"逆势出口"}

static func _group(parent:Node,title:String="",bg:Color=W.PANEL) -> VBoxContainer:
	var content=C.group(parent,title,bg)
	var panel=content.get_parent()
	if panel is PanelContainer:
		panel.add_theme_stylebox_override("panel",W.box(bg,W.SOFT,10))
		var inner=content as VBoxContainer;inner.add_theme_constant_override("separation",8)
	if title!="" and content.get_child_count()>0:
		var heading=content.get_child(0) as Label
		if heading!=null:
			heading.add_theme_color_override("font_color",W.ACCENT);heading.add_theme_font_override("font",W._semibold())
		if title in ["主要收益","主要损失","升级说明"]:
			var rule=ColorRect.new();rule.color=Color(W.ACCENT,.32);rule.custom_minimum_size.y=1;rule.mouse_filter=Control.MOUSE_FILTER_IGNORE;content.add_child(rule);content.move_child(rule,1)
	return content

static func render(d,section:String="home") -> void:
	match section:
		"home":_home(d)
		"development":_development(d)
		"construction":_construction(d)
		"permits":_permit_wallet(d)
		"commissions":render_commissions(d)
		"market":_market(d)
		"block":_block(d)
		_: 
			var bits=section.split(":")
			if bits[0]=="upgrade" and bits.size()>1:_upgrade(d,bits[1])
			elif bits[0]=="preview" and bits.size()>2:_upgrade_preview(d,bits[1],bits[2])
			elif bits[0]=="commission" and bits.size()>1:_commission_detail(d,bits[1])
			elif bits[0]=="resource" and bits.size()>1:_market(d,bits[1])
			elif bits[0]=="blockgoal":_block_goal(d)
			else:_home(d)

static func _home(d) -> void:
	var g=d.app.game
	d.title.text="城市发展";d.subtitle.text="从三种起点建筑成长，再处理建设、委托和街区目标。"
	var stats=g.s.get("metrics",{})
	var row=GridContainer.new();row.columns=1 if d.app.compact else 3;row.add_theme_constant_override("h_separation",10);row.add_theme_constant_override("v_separation",10);d.body.add_child(row)
	for pair in [["人口","%.0f"%float(stats.get("population",0))],["收支","%+.1f/h"%float(stats.get("cashflow",0))],["施工能力","%.1f CP/h"%float(g.Construction.capacity(g))]]:
		var card=_group(row,pair[0]);card.add_child(W.label(pair[1],24,W.GREEN))
	var links=GridContainer.new();links.columns=1 if d.app.compact else 2;links.add_theme_constant_override("h_separation",10);links.add_theme_constant_override("v_separation",10);d.body.add_child(links)
	for pair in [["development","建筑升级","查看成长路线、预览能力变化并开始升级。"],["construction","施工队列","查看共享施工能力、材料进度并调整优先级。"],["permits","部署许可","查看委托获得的许可并安装到空槽。"],["commissions","委托与目标","处理一个主委托、领取一次三选一奖励并查看历史。"],["market","贸易订单","查看外部报价、创建订单和核对执行记录。"],["block","街区目标与命名","继续经营当前街区，并在目标达成后为街区命名。"]]:
		var item=pair;var card=_group(links,item[1]);card.add_child(W.label(item[2],14,W.MUTED));card.add_child(W.button("打开",func():d.navigate("city040",item[0])))
	var add=_group(d.body,"新建起点建筑")
	add.add_child(W.label("农居、邻里中心、作坊是当前街区可新建的三种 Tier 1 起点。专业建筑通过升级获得。",15,W.MUTED))
	add.add_child(W.primary(W.button("选择地块并建设",func():d.app.toggle_palette("categories"))))
	if d.app.selected_project!="" and g.s.projects.has(d.app.selected_project) and not g.s.projects[d.app.selected_project].get("special",false):
		add.add_child(W.button("查看地图上选中的建筑："+g.project_name(d.app.selected_project),func():d.navigate("city040","upgrade:"+d.app.selected_project)))

static func _project_ids(g) -> Array:
	var result=[]
	for id in g.playable_ids():
		if g.s.projects.has(id) and not g.s.projects[id].get("special",false):result.append(str(id))
	result.sort()
	return result

static func _development(d) -> void:
	var g=d.app.game;d.title.text="建筑成长";d.subtitle.text="选中地图上的建筑，保持原项目与位置，再为它选择一个专业方向。"
	var ids=_project_ids(g)
	if ids.is_empty():d.body.add_child(W.label("还没有可升级的建筑。先从农居、邻里中心或作坊开始。",17,W.MUTED));return
	for id in ids:
		var p=g.s.projects[id];var def=g.c.projects.get(str(p.def),{});var tier=int(p.get("tier",def.get("tier",1)))
		var status={"running":"运营中","construction":"施工中","paused":"已暂停"}.get(str(p.get("status","")),str(p.get("status","")))
		var card=_group(d.body,g.project_name(id)+" · "+Art.stage_name(tier));card.add_child(W.label(g.c.operators.get(str(p.operator_org),{}).get("name",str(p.operator_org))+" · "+status,14,W.MUTED))
		if p.status!="running":card.add_child(W.label("当前仍在施工，完工后可升级。",14,W.GOLD))
		var choices=g.Development.choices(g,id)
		if choices.is_empty():card.add_child(W.label("已到该成长分支的最高阶段。",15,W.MUTED))
		else:card.add_child(W.button("查看 "+str(choices.size())+" 个升级方向",func():d.push_section("upgrade",id),p.status!="running"))

static func _upgrade(d,id:String) -> void:
	var g=d.app.game
	if not g.s.projects.has(id):d.body.add_child(W.label("建筑已不存在。",16,W.MUTED));return
	var p=g.s.projects[id];var def=g.c.projects.get(str(p.def),{});var choices=g.Development.choices(g,id)
	d.title.text=g.project_name(id)+" · 升级方向";d.subtitle.text=Art.stage_name(int(p.get("tier",def.get("tier",1))))+" · 保留建筑身份、住户、库存与运营者。"
	if choices.is_empty():d.body.add_child(W.label("暂无可用升级。",16,W.MUTED));return
	for node in choices:
		var target=g.c.projects.get(str(node),{});var choice=str(node);var card=_group(d.body,str(target.get("name",choice))+" · "+Art.stage_name(int(target.get("tier",1))))
		card.add_child(W.label(str(target.get("description","")),15,W.MUTED))
		card.add_child(W.label("%.0f通货 · %.0f材料 · %.1f CP"%[float(target.get("cost",0)),float(target.get("material",0)),float(target.get("construction_required",0))],15,W.GOLD))
		card.add_child(W.primary(W.button("预览升级 →",func():d.push_section("preview",id+":"+choice),p.status!="running")))

static func _upgrade_preview(d,id:String,node:String) -> void:
	var g=d.app.game;var preview=g.Development.preview(g,id,node)
	d.title.text="升级预览";d.subtitle.text="施工期间会按当前共享施工能力推进，并消耗材料。"
	if not preview.get("ok",false):d.body.add_child(W.label(str(preview.get("error","升级方向不可用。")),16,W.RED));return
	var current:Dictionary=g.c.projects.get(str(g.s.projects[id].def),{});var target:Dictionary=g.c.projects.get(node,{})
	var horizontal=W.text_scale<=1.2
	var comparison:Container=HBoxContainer.new() if horizontal else VBoxContainer.new();comparison.add_theme_constant_override("separation",10);comparison.size_flags_horizontal=Control.SIZE_EXPAND_FILL;d.body.add_child(comparison)
	for pair in [["当前建筑",current],["升级后",target]]:
		var item=pair;var name=str(item[0]);var definition:Dictionary=item[1];var preview_card=_group(comparison,name,W.SOFT)
		var sprite=TextureRect.new();sprite.texture=Art.thumbnail(str(definition.get("id",node)));sprite.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;sprite.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED;sprite.custom_minimum_size=Vector2(120,92);sprite.size_flags_horizontal=Control.SIZE_EXPAND_FILL;sprite.mouse_filter=Control.MOUSE_FILTER_IGNORE;preview_card.add_child(sprite)
		preview_card.add_child(W.label(str(definition.get("name",name)),17,W.TEXT))
		preview_card.add_child(W.label(Art.stage_description(int(definition.get("tier",1))),13,W.MUTED))
		if name=="当前建筑":
			var arrow_cell=CenterContainer.new();arrow_cell.custom_minimum_size=Vector2(32,32);arrow_cell.size_flags_horizontal=Control.SIZE_SHRINK_CENTER;arrow_cell.size_flags_vertical=Control.SIZE_SHRINK_CENTER;comparison.add_child(arrow_cell)
			var arrow=W.label("→",24,W.GREEN);arrow.size_flags_horizontal=Control.SIZE_SHRINK_CENTER;arrow_cell.add_child(arrow)
	var costs=GridContainer.new();costs.columns=2 if d.app.compact or W.text_scale>1.2 else 4;costs.add_theme_constant_override("h_separation",8);costs.add_theme_constant_override("v_separation",8);costs.size_flags_horizontal=Control.SIZE_EXPAND_FILL;d.body.add_child(costs)
	for metric in [["造价","%.0f 通货"%float(preview.cost)],["材料","%.0f"%float(preview.material)],["施工量","%.1f CP"%float(preview.construction_required)],["预计工期","%.1f 小时"%float(preview.eta)]]:
		var cell=metric;var box=_group(costs,str(cell[0]),W.PANEL);box.add_child(W.label(str(cell[1]),19,W.TEXT))
	var outcomes:Container=VBoxContainer.new() if W.text_scale>1.2 else HBoxContainer.new();outcomes.add_theme_constant_override("separation",8);outcomes.size_flags_horizontal=Control.SIZE_EXPAND_FILL;d.body.add_child(outcomes)
	var changes:Array=[];var seen={}
	for source_rows in [preview.get("gains",[]),preview.get("losses",[])]:
		for source_row in source_rows:
			var row:Dictionary=source_row;var key=str(row.get("stat",""))+":"+str(row.get("before",0))+":"+str(row.get("after",0))
			if not seen.has(key):changes.append(row);seen[key]=true
	var gains=_group(outcomes,"主要收益",W.PANEL);_stat_rows(gains,g,changes,true)
	var losses=_group(outcomes,"主要损失",W.PANEL);_stat_rows(losses,g,changes,false)
	var card=_group(d.body,"升级说明")
	var slot_name={"Operations":"运营","Production":"生产","Logistics":"物流","Flex":"弹性"}.get(str(preview.get("slot","")),str(preview.get("slot","新")))
	card.add_child(W.label("施工后解锁「%s」方案槽 · 升级期间运营效率 %.0f%%"%[slot_name,float(preview.get("operation_factor",1))*100],14,W.MUTED))
	var p=g.s.projects.get(id,{})
	var start_upgrade=func():
		var error=g.Development.start_upgrade(g,id,node)
		if error!="":d.app.message(error)
		else:d.app.save_game();d.app.refresh();d.navigate("city040","construction");d.app.message(g.project_name(id)+"已加入施工队列。")
	var disabled=str(p.get("status",""))!="running" or not p.get("upgrade_job",{}).is_empty()
	d.footer.show();d.footer_text.text="升级会按当前共享施工能力推进，期间建筑效率 %.0f%%。"%[float(preview.get("operation_factor",1))*100]
	d.primary.show();d.primary.text="开始升级";d.primary.disabled=disabled;d.primary.pressed.connect(start_upgrade)
	d.secondary.show();d.secondary.text="返回"

static func _stat_rows(parent,g,rows:Array,gain:bool) -> void:
	var stat_names={"housing":"住房位置","jobs":"岗位","service.medical":"医疗","service.education":"教育","service.commercial":"购物","service.entertainment":"休闲","output.food":"粮食产出","output.material":"材料产出","output.goods":"制成品产出","output.energy":"电力产出","input.food":"粮食消耗","input.material":"材料消耗","input.goods":"制成品消耗","storage.food":"粮食仓容","storage.material":"材料仓容","storage.goods":"制成品仓容","energy_demand":"电力需求","maintenance":"维护费","service_range":"服务范围","pollution":"环境负担","efficiency":"基础效率"}
	var shown=0
	for row in rows:
		var stat=str(row.get("stat",""));var before=float(row.get("before",0));var after=float(row.get("after",0))
		if is_equal_approx(before,after):continue
		var lower_is_better=stat.begins_with("input.") or stat in ["energy_demand","maintenance","pollution"]
		var improved=after<before if lower_is_better else after>before
		if improved!=gain:continue
		var label=str(stat_names.get(stat,stat.replace("output.","").replace("input.","") ))
		parent.add_child(W.label(label+"  %s → %s"%[_format_value(row.get("before",0)),_format_value(row.get("after",0))],15,W.GREEN if improved else W.GOLD));shown+=1
	if shown==0:parent.add_child(W.label("没有明显的数值增减。",14,W.MUTED))

static func _format_value(value) -> String:
	return "%.2f"%float(value) if value is float else str(value)

static func _construction(d) -> void:
	var g=d.app.game;var rows=g.Construction.queue(g)
	d.title.text="施工队列";d.subtitle.text="共享施工能力 %.1f CP/h · 进度和材料在真实推进中结算。"%float(g.Construction.capacity(g))
	if rows.is_empty():d.body.add_child(W.label("当前没有施工中的建筑或升级。",17,W.MUTED));return
	for row in rows:
		var item=row;var card=_group(d.body,str(row.name)+(" · 升级" if row.upgrade else " · 新建"))
		var fraction=1.-float(row.work)/maxf(.001,float(row.total));card.add_child(W.bar(fraction))
		card.add_child(W.label("完成 %.0f%% · 剩余 %.1f CP · 材料 %.1f · 优先级 %s"%[fraction*100,float(row.work),float(row.material),_priority_name(int(row.priority))],15,W.MUTED))
		var actions=W.hbox(card,6)
		for value in [-1,0,1]:
			var priority=value;var b=W.button(_priority_name(value),func():
				var error=g.Construction.set_priority(g,str(item.id),priority)
				if error!="":d.app.message(error)
				else:d.app.save_game();d.app.refresh();d.render(true)
			)
			if int(row.priority)==value:W.primary(b)
			actions.add_child(b)

static func _priority_name(value:int) -> String:
	return {-1:"低",0:"普通",1:"优先"}.get(value,"普通")

static func _permits(d) -> void:
	var g=d.app.game;d.title.text="部署许可";d.subtitle.text="每张许可只提供一次安装机会；卸载后不会退回。"
	var ids=_project_ids(g)
	if ids.is_empty():d.body.add_child(W.label("先建成一处建筑，再查看它可安装的许可。",16,W.MUTED));return
	for id in ids:
		var p=g.s.projects[id];var card=_group(d.body,g.project_name(id)+" · "+Art.stage_name(int(p.get("tier",1))))
		var slots=[]
		for slot in p.get("slots",{}):slots.append(str(slot))
		card.add_child(W.label(("%d个槽位"%slots.size())+" · "+g.c.operators.get(str(p.get("operator_org","")),{}).get("name",""),14,W.MUTED))
		card.add_child(W.button("查看可安装许可",func():d.push_section("permits",id),slots.is_empty()))

static func _permit_wallet(d) -> void:
	var g=d.app.game;d.title.text="部署许可";d.subtitle.text="从委托奖励中选择许可，再安装到适用建筑、运营组织或当前街区。每张许可只部署一次。"
	var wallet:Dictionary=g.s.block.get("bonus_wallet",{});var ids:Array=wallet.keys();ids.sort();var available_count=0
	var metadata_all:Dictionary=g.c.v040.get("bonus_economy",{}).get("metadata",{})
	for value in ids:
		var bid=str(value);var license:Dictionary=wallet[bid]
		if int(license.get("charges",0))<=0:continue
		available_count+=1;var def:Dictionary=g.c.bonuses.get(bid,{});var meta:Dictionary=metadata_all.get(bid,{})
		var scope=str(meta.get("scope",license.get("scope","building")));var label={"building":"建筑许可","organization":"组织许可","block":"街区许可"}.get(scope,"建筑许可")
		var card=_group(d.body,str(def.get("name",bid))+" · "+label,W.SOFT);card.add_child(W.label("剩余 %d 次"%int(license.get("charges",0)),14,W.GREEN));card.add_child(W.label(str(def.get("summary","")),14,W.MUTED))
		var options:Array=[]
		if scope=="building":
			for project_id in _project_ids(g):
				var p:Dictionary=g.s.projects[project_id]
				for slot in p.get("slots",{}):
					if str(p.slots[slot])!="":continue
					if g.bonus_eligible(bid,"building",project_id,str(slot))=="":options.append({"type":"building","host":project_id,"slot":str(slot),"name":g.project_name(project_id)+" · "+g.slot_label(project_id,str(slot))})
		elif scope=="organization":
			var organizations={}
			for project_id in _project_ids(g):organizations[str(g.s.projects[project_id].get("operator_org",""))]=true
			for org in organizations:
				if g.bonus_eligible(bid,"organization",str(org))=="":options.append({"type":"organization","host":str(org),"slot":"","name":str(g.c.operators.get(str(org),{}).get("name",org))+" · 组织"})
		elif scope=="block":
			var block_id=str(g.s.block.get("id","block:1"))
			if g.bonus_eligible(bid,"block",block_id)=="":options.append({"type":"block","host":block_id,"slot":"","name":"当前街区"})
		if options.is_empty():card.add_child(W.label("当前没有适用的空槽或宿主。",14,W.MUTED))
		for option in options:
			var item=option
			card.add_child(W.primary(W.button("安装到「%s」"%str(item.name),func():
				var error=g.Bonus.install(g,bid,str(item.type),str(item.host),str(item.slot))
				if error!="":d.app.message(error)
				else:d.app.save_game();d.app.refresh();d.replace("city040","permits");d.app.message(str(def.get("name",bid))+"许可已安装。")
			)))
	if available_count==0:d.body.add_child(W.label("钱包里还没有待部署许可。完成委托后，可从奖励三选一领取。",16,W.MUTED))

static func _permits_for(d,id:String) -> void:
	var g=d.app.game
	if not g.s.projects.has(id):d.body.add_child(W.label("建筑已不存在。",16,W.MUTED));return
	var p=g.s.projects[id];d.title.text=g.project_name(id)+" · 安装许可";d.subtitle.text="选空槽，再从当前钱包中选择一张许可。安装资格在部署时检查。"
	var wallet:Dictionary=g.s.block.get("bonus_wallet",{})
	var ids=wallet.keys();ids.sort()
	var empty_count=0
	for slot in p.get("slots",{}):
		var iid=str(p.slots[slot]);var slot_type=str(p.get("slot_types",{}).get(slot,g.slot_type(id,str(slot))))
		var box=_group(d.body,g.slot_label(id,str(slot))+(" · 已安装" if iid!="" else " · 空槽"))
		if iid!="":
			var installed=g.s.bonus_instances.get(iid,{});var bid=str(installed.get("bonus_id",""));box.add_child(W.label(g.c.bonuses.get(bid,{}).get("name",bid),16,W.GREEN));continue
		empty_count+=1
		var eligible=[]
		for bid in ids:
			var license=wallet[bid];var def=g.c.bonuses.get(str(bid),{})
			if int(license.get("charges",0))<=0 or not "building" in def.get("hosts",[]):continue
			if slot_type=="Flex":
				if not "Production" in def.get("allowed_slots",[]) and not "Logistics" in def.get("allowed_slots",[]):continue
			elif not slot_type in def.get("allowed_slots",[]):continue
			eligible.append(str(bid))
		if eligible.is_empty():box.add_child(W.label("钱包里没有适配这个槽位的许可。",14,W.MUTED));continue
		for bid in eligible:
			var license:Dictionary=wallet[bid];var bonus:Dictionary=g.c.bonuses.get(bid,{})
			var name=str(bonus.get("name",bid))
			var button=W.button("%s · 剩余%d次"%[name,int(license.charges)],func():
				var error=g.Bonus.install(g,bid,"building",id,str(slot))
				if error!="":d.app.message(error)
				else:d.app.save_game();d.app.refresh();d.replace("city040","permits:"+id);d.app.message(name+"许可已安装。")
			)
			box.add_child(button);box.add_child(W.label(str(bonus.get("summary","")).left(100),14,W.MUTED))
	if empty_count==0:d.body.add_child(W.label("该建筑当前没有空槽；升级后可解锁新的稳定槽型。",15,W.MUTED))

static func render_commissions(d) -> void:
	var g=d.app.game;var state:Dictionary=g.s.block.get("commissions",{});var active:Dictionary=state.get("active",{})
	d.title.text="委托与街区目标";d.subtitle.text=str(g.s.block.display_name)+" · 当前委托、街区目标与完成记录。"
	for pair in [["active","主委托"],["available","可接委托"],["history","历史"]]:
		var tab=pair[0];var label={"active":"当前","available":"可接","history":"历史"}.get(tab,pair[1]) if d.app.compact else pair[1];var b=W.button(label,func():d.task_tab=tab;d.render(false));b.size_flags_horizontal=Control.SIZE_EXPAND_FILL;d.tabs.add_child(b)
		if d.task_tab==tab:W.primary(b)
	if d.task_tab=="history":
		var history=state.get("history",[])
		if history.is_empty():d.body.add_child(W.label("还没有委托记录。",17,W.MUTED))
		for entry in history.duplicate():
			var item=entry;var card=_group(d.body,str(entry.get("name",entry.get("id","委托"))))
			card.add_child(W.label(_status_name(str(entry.get("status","")))+" · "+str(entry.get("completed_at",entry.get("started_at",""))),14,W.MUTED))
			card.add_child(W.button("查看委托条件",func():d.push_section("commission",str(item.get("id","")))))
		return
	if d.task_tab=="active":
		var rewards=g.s.block.get("reward_choices",[]);var pending=str(state.get("pending_reward",""));var choice={}
		for reward in rewards:
			if str(reward.get("reward_id",""))==pending and not reward.get("claimed",false):choice=reward;break
		if not choice.is_empty():_reward_choice(d,choice)
		if not active.is_empty():
			var commission=_commission_def(g,str(active.get("id","")));var card=_group(d.body,str(commission.get("name",active.get("id","主委托"))))
			card.add_child(W.bar(_commission_progress(active,commission)))
			card.add_child(W.label(_progress_summary(active,commission),15,W.GREEN))
			card.add_child(W.button("查看委托条件",func():d.push_section("commission",str(active.get("id","")))))
			if _commission_has_goal(commission,"delivery_goods",active):
				var target=0.;for goal in _commission_goals(active,commission):
					if str(goal.get("metric",""))=="delivery_goods":target=maxf(target,float(goal.get("min",0)))
				var delivered=float(active.get("delivered_goods",0));var stock=g.inventory_total("goods")
				card.add_child(W.primary(W.button("交付制成品 · %.0f / %.0f"%[delivered,target],func():
					var error=g.Commissions.deliver(g)
					if error!="":d.app.message(error)
					else:d.app.save_game();d.app.refresh();d.render(false);d.app.message("委托交付已记录。推进城市以完成委托条件。")
				,stock<=0 or delivered>=target)))
			card.add_child(W.button("放弃当前委托",func():
				var error=g.Commissions.abandon(g)
				if error!="":d.app.message(error)
				else:d.app.save_game();d.app.refresh();d.render(false)
			))
		else:d.body.add_child(W.label("当前没有进行中的委托。去“可接委托”选择一项，其他候选仍会保留。",16,W.MUTED))
		_block_goal_card(d)
		_block_management(d)
	else:
		var choices=g.Commissions.available(g)
		if choices.is_empty():d.body.add_child(W.label("目前没有可接委托。建成更多设施或推进城市后会出现新的方向。",16,W.MUTED))
		for entry in choices:
			var item=entry;var id=str(entry.get("id",""));var def=_commission_def(g,id);var card=_group(d.body,str(def.get("name",entry.get("name",id))))
			card.add_child(W.label(str(def.get("help",def.get("description",""))),15,W.MUTED))
			card.add_child(W.button("查看条件",func():d.push_section("commission",id)))
			card.add_child(W.primary(W.button("接取为主委托",func():
				var error=g.Commissions.accept(g,id)
				if error!="":d.app.message(error)
				else:d.task_tab="active";d.app.save_game();d.app.refresh();d.render(false)
			)))

static func _reward_choice(d,choice:Dictionary) -> void:
	var g=d.app.game;var reward_id=str(choice.get("reward_id",""));var card=_group(d.body,"委托完成 · 选择一张许可",W.SOFT)
	card.add_child(W.label("领取后只生成一次部署许可。关闭页面、切页或重载不会改变选择。",14,W.MUTED))
	var options=choice.get("choices",[])
	if options.is_empty():card.add_child(W.label("奖励选项正在准备；稍后可回到这里领取。",15,W.GOLD))
	for entry in options:
		var item=entry;var bid=str(entry.get("bonus_id",""));var name=str(entry.get("name",g.c.bonuses.get(bid,{}).get("name",bid)))
		var scope_label={"building":"建筑许可","organization":"组织许可","block":"当前街区许可"}.get(str(entry.get("scope","building")),"当前街区许可")
		var button=W.primary(W.button(name+"  ·  "+scope_label+" · 一次部署",func():
			var error=g.Bonus.choose(g,reward_id,bid)
			if error!="":d.app.message(error)
			else:d.app.save_game();d.app.refresh();d.render(false);d.app.message(name+"许可已加入钱包。")
		))
		card.add_child(button);card.add_child(W.label(str(entry.get("preview_reason","适用于指定宿主，可在当前街区部署一次。")),14,W.MUTED))

static func _block_goal_card(d) -> void:
	var g=d.app.game;var card=_group(d.body,"街区目标")
	var rows=g.Progress.checklist(g)
	for row in rows:
		var item=row;card.add_child(W.label(("✓ " if bool(row.get("done",false)) else "○ ")+str(row.get("name",""))+" · "+str(row.get("value","")),14,W.GREEN if row.get("done",false) else W.MUTED))
	card.add_child(W.button("目标条件与判定",func():d.push_section("blockgoal")))
	if g.s.get("city_goal",{}).get("completed",false):card.add_child(W.label("街区目标已达成；你可以继续经营这座城市。",14,W.GREEN))

static func _block_goal(d) -> void:
	var g=d.app.game;var goal:Dictionary=g.s.get("city_goal",{});var route=str(goal.get("route","community"));var def=g.c.data.get("city_goals",{}).get(route,{})
	d.title.text="街区目标 · "+str(def.get("name","当前城市成果"));d.subtitle.text="目标与历史分开呈现，达成后仍可继续经营。"
	d.body.add_child(W.label(str(def.get("description","")),16))
	for row in g.Progress.checklist(g):d.body.add_child(W.label(("✓ " if row.get("done",false) else "○ ")+str(row.name)+" · "+str(row.value),16,W.GREEN if row.get("done",false) else W.MUTED))
	var detail=_group(d.body,"本目标条件")
	for row in g.Progress.requirements(g):detail.add_child(W.label(("✓ " if row.get("done",false) else "○ ")+str(row.name)+" · "+str(row.value),15,W.GREEN if row.get("done",false) else W.MUTED))
	if route=="delivery":detail.add_child(W.primary(W.button("交付20材料与12制成品",func():
		var error=g.Progress.deliver(g)
		if error!="":d.app.message(error)
		else:d.app.save_game();d.app.refresh();d.render(false)
	)))
	var history=_group(d.body,"街区目标历史")
	var rows=g.s.get("civic032",{}).get("task_history",[])
	var found=false
	for row in rows:
		if str(row.get("id",""))=="route:"+route:found=true;history.add_child(W.label(_status_name(str(row.get("status","")))+" · "+str(row.get("h",""))+"h",14,W.MUTED))
	if not found:history.add_child(W.label("街区目标达成后会在这里留下记录。",14,W.MUTED))

static func _commission_detail(d,id:String) -> void:
	var g=d.app.game;var def=_commission_def(g,id)
	d.title.text=str(def.get("name",id))+" · 委托条件";d.subtitle.text="条件在此完整查看；主任务栏只显示进度。"
	d.body.add_child(W.label(str(def.get("help",def.get("description",""))),16))
	var goals=_group(d.body,"完成条件")
	for goal in def.get("goals",[]):
		var metric=str(goal.get("metric",""));var text=str(GOAL_NAMES.get(metric,metric.replace("_"," ")))
		if goal.has("min"):text+=" · 至少 "+_format_value(goal.min)
		if goal.has("max"):text+=" · 至多 "+_format_value(goal.max)
		if goal.has("duration"):text+=" · 持续 %.1fh"%float(goal.duration)
		goals.add_child(W.label(text,15,W.MUTED))
	var fail=def.get("fail_conditions",{})
	if not fail.is_empty():
		var box=_group(d.body,"失败条件")
		for key in fail:box.add_child(W.label(str(GOAL_NAMES.get(str(key),str(key)))+" · "+_format_value(fail[key]),14,W.GOLD))
	var state=g.s.block.get("commissions",{});var active=state.get("active",{})
	if str(active.get("id",""))==id:
		goals.add_child(W.bar(_commission_progress(active,def)));goals.add_child(W.label(_progress_summary(active,def),15,W.GREEN))
	else:goals.add_child(W.label("领域："+str(D.domain(str(def.get("domain","community"))).label)+" · 奖励类型：部署许可",14,W.MUTED))

static func _commission_def(g,id:String) -> Dictionary:
	var defs=g.c.v040.get("commissions",[])
	if defs is Dictionary:return defs.get(id,{})
	for def in defs:
		if str(def.get("id",""))==id:return def
	return {}

static func _commission_goals(active:Dictionary,def:Dictionary) -> Array:
	var goals=active.get("goals",[])
	return goals if goals is Array and not goals.is_empty() else def.get("goals",[])

static func _commission_progress(active:Dictionary,def:Dictionary) -> float:
	var goals=_commission_goals(active,def)
	if goals.is_empty():return 0
	var now:Dictionary=active.get("progress_now",{});var elapsed:Dictionary=active.get("goal_progress",{});var sum=0.
	for i in goals.size():
		var goal:Dictionary=goals[i];var key=str(i);var metric=str(goal.get("metric",""))
		var current=float(now.get(key,active.get("delivered_goods",0) if metric=="delivery_goods" else 0))
		var target=float(goal.get("min",goal.get("max",1)))
		if float(goal.get("duration",0))>0:
			target=float(goal.duration);sum+=clampf(float(elapsed.get(key,0))/maxf(.001,target),0,1)
		elif goal.has("max"):
			sum+=1. if current<=target else clampf(target/maxf(.001,current),0,1)
		elif target<=0:sum+=1. if current<=target else 0.
		else:sum+=clampf(current/target,0,1)
	return sum/goals.size()

static func _progress_summary(active:Dictionary,def:Dictionary) -> String:
	var goals=_commission_goals(active,def);var now:Dictionary=active.get("progress_now",{});var elapsed:Dictionary=active.get("goal_progress",{});var rows=[]
	for i in goals.size():
		var goal:Dictionary=goals[i];var key=str(i);var metric=str(goal.get("metric",""));var name=str(GOAL_NAMES.get(metric,metric.replace("_"," ")))
		var value=now.get(key,active.get("delivered_goods",0) if metric=="delivery_goods" else 0)
		var row=name+" "+_format_value(value)
		if goal.has("min"):row+=" / "+_format_value(goal.min)
		elif goal.has("max"):row+=" / ≤"+_format_value(goal.max)
		if float(goal.get("duration",0))>0:row+=" · 连续 %.1f / %.1fh"%[float(elapsed.get(key,0)),float(goal.duration)]
		rows.append(row)
	return " · ".join(rows)

static func _commission_has_goal(def:Dictionary,metric:String,active:Dictionary={}) -> bool:
	for goal in _commission_goals(active,def):
		if str(goal.get("metric",""))==metric:return true
	return false

static func _status_name(status:String) -> String:
	return {"accepted":"已接取","abandoned":"已放弃","completed":"已完成","expired":"已到期","ready":"待领取"}.get(status,"记录")

static func _market(d,resource:String="") -> void:
	var g=d.app.game;var resources=["food","material","goods"]
	d.title.text="街区贸易";d.subtitle.text="订单由货运站容量逐步执行，报价使用当前街区市场状态。"
	if resource!="" and resource in resources:d.trade_resource=resource
	var selector=W.hbox(d.body,8)
	for id in resources:
		var res=id;var b=W.button(RES_NAMES[res],func():d.replace("city040","resource:"+res))
		if d.trade_resource==res:W.primary(b)
		selector.add_child(b)
	var quote=g.Market.quote(g,d.trade_resource)
	var summary_and_order=GridContainer.new();summary_and_order.columns=1 if d.app.compact or W.text_scale>1.2 else 2;summary_and_order.add_theme_constant_override("h_separation",10);summary_and_order.add_theme_constant_override("v_separation",10);summary_and_order.size_flags_horizontal=Control.SIZE_EXPAND_FILL;d.body.add_child(summary_and_order)
	var price=_group(summary_and_order,RES_NAMES[d.trade_resource]+" · 当前市场价")
	price.add_child(W.label("买入 %.2f · 卖出 %.2f · 中间价 %.2f"%[float(quote.get("buy",0)),float(quote.get("sell",0)),float(quote.get("mid",0))],20,W.GREEN))
	var station=false
	for id in _project_ids(g):
		var p=g.s.projects[id]
		if str(p.get("def",""))=="freight" and p.get("status","")=="running" and not p.get("paused",false):station=true
	price.add_child(W.label("已接通货运站" if station else "建成货运站后可创建订单。",14,W.MUTED))
	var form=_group(summary_and_order,"创建订单")
	var narrow=d.app.compact or W.text_scale>1.2
	var trade_row:Container=W.vbox(form,6) if narrow else W.hbox(form,6)
	var side=OptionButton.new();side.add_item("买入");side.add_item("卖出");side.selected=0 if d.trade_side=="buy" else 1;side.custom_minimum_size=Vector2(0 if narrow else 74,40);side.size_flags_horizontal=Control.SIZE_EXPAND_FILL;trade_row.add_child(side)
	side.item_selected.connect(func(i):d.trade_side="buy" if i==0 else "sell";_sync_trade_price(d))
	var amount=SpinBox.new();amount.min_value=.1;amount.max_value=9999;amount.step=.5;amount.value=d.trade_amount;amount.suffix="件";amount.custom_minimum_size=Vector2(0 if narrow else 94,40);amount.size_flags_horizontal=Control.SIZE_EXPAND_FILL;trade_row.add_child(amount)
	amount.value_changed.connect(func(value):d.trade_amount=value)
	var limit=SpinBox.new();limit.min_value=-1000;limit.max_value=10000;limit.step=.1;limit.value=d.trade_price if d.trade_price_set else _quote_price(quote,d.trade_side);limit.prefix="限价 ";limit.custom_minimum_size=Vector2(0 if narrow else 124,40);limit.size_flags_horizontal=Control.SIZE_EXPAND_FILL;trade_row.add_child(limit)
	limit.value_changed.connect(func(value):d.trade_price=value;d.trade_price_set=true)
	var submit=W.primary(W.button("提交订单",func():
		var error=g.Market.order(g,d.trade_resource,d.trade_side,float(amount.value),float(limit.value))
		if error!="":d.app.message(error)
		else:d.app.save_game();d.app.refresh();d.replace("city040","resource:"+d.trade_resource);d.app.message("贸易订单已创建，按货运能力执行。")
	,not station));submit.size_flags_horizontal=Control.SIZE_EXPAND_FILL;submit.custom_minimum_size.y=40;trade_row.add_child(submit)
	var orders=_group(d.body,"当前订单")
	var current=g.s.block.get("trade_orders",[])
	if current.is_empty():orders.add_child(W.label("当前没有开放订单。",14,W.MUTED))
	for row in current:
		var item=row;var row_box=W.hbox(orders,8)
		row_box.add_child(W.label("%s · %s %.1f @ %.2f · 已执行 %.1f"%[str(item.get("id","")),RES_NAMES.get(str(item.get("resource","")),str(item.get("resource",""))),float(item.get("amount",0)),float(item.get("limit_price",0)),float(item.get("filled",0))],14))
		row_box.add_child(W.button("取消",func():
			var error=g.Market.cancel(g,str(item.get("id","")))
			if error!="":d.app.message(error)
			else:d.app.save_game();d.replace("city040","resource:"+d.trade_resource)
		))
	var history=_group(d.body,"最近贸易记录")
	var trades=g.s.block.get("trade_history",[])
	if trades.is_empty():history.add_child(W.label("尚无已执行交易。",14,W.MUTED))
	for i in range(trades.size()-1,maxi(-1,trades.size()-21),-1):
		var entry=trades[i];history.add_child(W.label("%s · %s %.1f · 金额 %+.2f"%[str(entry.get("status","已执行")),RES_NAMES.get(str(entry.get("resource","")),str(entry.get("resource",""))),float(entry.get("amount",0)),float(entry.get("cash",0))],14,W.MUTED))

static func _sync_trade_price(d) -> void:
	var quote=d.app.game.Market.quote(d.app.game,d.trade_resource);d.trade_price=_quote_price(quote,d.trade_side);d.trade_price_set=false;d.render(false)

static func _quote_price(quote:Dictionary,side:String) -> float:
	return float(quote.get("buy",0) if side=="buy" else quote.get("sell",0))

static func _block(d) -> void:
	var g=d.app.game;var block:Dictionary=g.s.block
	d.title.text="当前街区";d.subtitle.text="目标达成后仍可继续经营；命名只是留下一份自己的地图标记。"
	var name=str(block.get("display_name","河谷登陆点"))
	var naming=_group(d.body,"街区名称")
	naming.add_child(W.label(name+(" · 可命名" if block.get("naming_rights",false) else ""),22,W.GREEN if block.get("naming_rights",false) else W.TEXT))
	if block.get("naming_rights",false):
		var input=LineEdit.new();input.placeholder_text="输入街区名称";input.custom_minimum_size.y=46;input.max_length=24;naming.add_child(input)
		var save_name=W.primary(W.button("保存名称",func():
			var error=g.Block.name_block(g,input.text.strip_edges())
			if error!="":d.app.message(error)
			else:d.app.save_game();d.app.refresh();d.replace("city040","block");d.app.message("街区名称已保存。")
		,input.text.strip_edges().is_empty()))
		naming.add_child(save_name)
		input.text_changed.connect(func(value:String):save_name.disabled=value.strip_edges().is_empty())
	else:
		naming.add_child(W.label("完成街区目标后会获得命名权。",14,W.MUTED))
	var goal=bool(g.s.get("city_goal",{}).get("completed",false))
	var status=_group(d.body,"街区目标",W.SOFT if goal else W.PANEL)
	status.add_child(W.label("已达成 · 可继续经营" if goal else "进行中 · 目标不会限制继续建造",17,W.GREEN if goal else W.TEXT))
	status.add_child(W.button("查看目标条件与历史",func():d.push_section("blockgoal")))

static func _block_management(d) -> void:
	var g=d.app.game;var card=_group(d.body,"居民与生活用品")
	var arrival=float(g.next_arrival())
	card.add_child(W.label("下一批居民：%.1f小时后"%arrival if arrival>=0 else "本轮约定的居民已全部到达",14,W.MUTED))
	card.add_child(W.button("恢复迁入" if g.s.get("migration_paused",false) else "暂停新居民迁入",func():
		g.toggle_migration();d.app.save_game();d.app.refresh();d.render(true)
	))
	if g.s.get("goods_active",false):
		card.add_child(W.label("生活用品需求正在生效，居民会持续消费制成品。",14,W.GREEN))
	elif float(g.s.get("goods_requested_at",-1))>=0:
		var remain=maxf(0,float(g.s.goods_requested_at)+float(g.c.balance.goods_grace_hours)-float(g.s.local_hours))
		card.add_child(W.label("生活用品计划已公布 · %.1f小时后开始消费"%remain,14,W.GREEN))
	else:
		card.add_child(W.label("公布生活用品计划后，居民会在宽限期后开始消费制成品。",14,W.MUTED))
		var available=bool(g.s.get("goods_available",false)) or g.inventory_total("goods")>0
		card.add_child(W.primary(W.button("公布生活用品计划（2小时后生效）",func():
			var error=g.enable_goods()
			if error!="":d.app.message(error)
			else:d.app.save_game();d.app.refresh();d.render(true);d.app.message("生活用品消费计划已公布。")
		,not available)))
