extends RefCounted
## Native Beta presentation. These functions read authoritative state, with mutations
## confined to explicitly selected commands. Display formatting never touches RNG.
const W=preload("res://ui/widgets.gd")
const C=preload("res://ui/components031/components.gd")
const Models=preload("res://ui/city032/models.gd")
const MONTH=1555200.0
const YEAR=18662400.0
const DAY=129600.0
const DT_SECONDS=1244160.0
const MODES=[["normal","常速",1.0],["observe","观察",5184.0],["advance","推进",25920.0],["burn","燃烧",77760.0]]

static func date(seconds:float) -> String:
	var days=int(floor(maxf(0,seconds)/DAY));var year=int(days/144)+1;var month=int((days%144)/12)+1;var day=days%12+1
	return "来生 %d年%02d月%02d日"%[year,month,day]

static func seconds_text(value:float) -> String:
	if value>=86400:return "%.1f天"%(value/86400.)
	if value>=3600:return "%.1f小时"%(value/3600.)
	if value>=60:return "%.1f分钟"%(value/60.)
	return "%.1f秒"%maxf(0,value)

static func acceleration(g) -> float:
	return float(g.Clock.acceleration(g))

static func mode_rows(g) -> Array:
	var result=[]
	for mode in g.c.beta.time.modes:result.append([str(mode.id),str(mode.get("label",mode.id)),float(mode.acceleration)])
	return result

static func dt_seconds(g) -> float:
	return float(g.c.beta.time.earth_seconds_per_dt)

static func mode_name(g) -> String:
	var id=str(g.s.get("beta_time",{}).get("mode","normal"))
	for mode in mode_rows(g):
		if mode[0]==id:return mode[1]
	return "常速"

static func pending(g) -> Array:
	var result=[]
	for reward in g.s.block.get("reward_choices",[]):
		if not reward.get("claimed",false):result.append(reward)
	return result

static func risk(g,shot:Dictionary) -> Dictionary:
	if not shot.has("audit"):return {"text":"等待城市结算","id":"food","stable":true}
	var best={};var a=acceleration(g)
	for id in ["food","material","goods"]:
		var model=Models.resource(g,shot,id);var net=float(model.get("net",0))+float(model.get("delivered",0))
		if not shot.get("forecast",false):
			if not trade_continues(g,id,"buy"):net-=float(model.get("imported",0))
			if not trade_continues(g,id,"sell"):net+=float(model.get("exported",0))
		if float(model.get("stock",0))<=0.000001 and float(model.get("need",0))>float(model.get("consumed",0))+0.000001:
			return {"id":id,"name":model.get("name",id),"months":0.0,"seconds":0.0,"text":str(model.get("name",id))+"已不足 · 需要补足实际供给","stable":false}
		# Snapshot net is produced by the shared simulator, including recipes and
		# actually executable trade. Never promise supply from a blocked order.
		if net>=-0.000001:continue
		var months=maxf(0,float(model.get("stock",0)))/-net
		if best.is_empty() or months<float(best.months):best={"id":id,"name":model.get("name",id),"months":months,"seconds":months*MONTH/a}
	if best.is_empty():return {"text":"库存稳定 / 增加","id":"food","stable":true}
	best.text="%s余量约%.2f月 / 当前档约%s"%[best.name,best.months,seconds_text(best.seconds)];best.stable=false
	return best

static func set_mode(app,id:String) -> void:
	app.flush_beta_time()
	if app.beta_mandatory_pending():app.open_beta_settlement();return
	if app.decision_kind!="":app.message("请先完成当前方案选择。");return
	var error=app.game.Clock.set_mode(app.game,id)
	if error is String and error!="":app.message(error);return
	app.display_pending_tick=int(app.game.s.simulation_tick)
	app.game.s.settings.user_paused=false;app.game.s.beta_time.pause_reason=""
	if app.desk.is_open():app.desk.close()
	app.save_game();app.refresh();app.message("时间档位："+mode_name(app.game))

static func render_time(d) -> void:
	var g=d.app.game;var state=g.s.get("beta_time",{});var local=float(state.get("local_elapsed_seconds",0));var a=acceleration(g)
	d.title.text="城市时间";d.subtitle.text=date(local)+" · 6日一旬 · 12日一月 · 144日一年";d.cost.text=""
	var grid=GridContainer.new();grid.columns=1 if d.app.compact else 2;grid.add_theme_constant_override("h_separation",10);grid.add_theme_constant_override("v_separation",10);d.body.add_child(grid)
	d._metric(grid,"高速发展预算","%.2f DT"%float(g.s.dt),"可购买额外 %.2f 来生年"%(float(g.s.dt)*dt_seconds(g)/YEAR),W.GREEN)
	var burn=maxf(0,a-1.0)/DT_SECONDS
	d._metric(grid,"当前档位",mode_name(g)+" ×%.0f"%a,"消耗 %.3f DT / 现实分钟"%(burn*60) if burn>0 else "自然运行不消耗 DT",W.TEXT)
	d._metric(grid,"下一年度",seconds_text((YEAR-fmod(local,YEAR))/a),"现实活跃时间预测；决策期间暂停",W.TEXT)
	d._metric(grid,"下一大周期",seconds_text((2*YEAR-fmod(local,2*YEAR))/a),"两年总结，不重复发年度普通卡",W.TEXT)
	if burn>0:d._metric(grid,"预算可运行",seconds_text(float(g.s.dt)/burn),"按当前档位的现实活跃时间",W.GOLD)
	var modes=C.group(d.body,"选择时间档位")
	for mode in mode_rows(g):
		var id=str(mode[0]);var disabled=float(g.s.dt)<=0 and id!="normal"
		var button=W.button("%s ×%.0f"%[mode[1],mode[2]],func():set_mode(d.app,id),disabled)
		if str(state.get("mode","normal"))==id:W.primary(button)
		modes.add_child(button)
	var estimate=risk(g,risk_snapshot(d.app));d.body.add_child(W.label(str(estimate.text),16,W.GREEN if estimate.stable else W.GOLD))
	d.body.add_child(W.label("暂停、关闭和后台均冻结城市与外贸。离开时长只记录，不发资源、DT或奖励。移民按本地时间抵达；外贸只按真实活跃在线时间成交。",14,W.MUTED))
	d.body.add_child(W.button("继续 / 暂停城市",func():d.toggle_pause()))

static func render_themes(d) -> void:
	var g=d.app.game;var view=g.Themes.view(g)
	d.title.text="委任主题";d.subtitle.text="选择这座城市的发展方向；年度和安居人口奖励独立获得。";d.cost.text=""
	var queue=pending(g)
	if not queue.is_empty():reward(d,queue[0]);d.body.add_child(W.label("待领取 %d 份 · 每次只处理一组已保存的候选"%queue.size(),14,W.MUTED))
	if str(view.get("id",""))=="":
		for option in view.get("choices",[]):
			var item=option;var group=C.group(d.body,str(item.name));group.add_child(W.label(str(item.get("description","")),15,W.MUTED))
			group.add_child(W.button("选择「%s」"%str(item.name),func():_command(d,g.Themes.select(g,str(item.id)))))
		return
	var card=C.group(d.body,str(view.get("name",""))+" · "+str(view.get("stage_name","")))
	if view.get("completed",false):card.add_child(W.label("✓ 主题完成 · DT 首次归零时具备定型建筑资格",17,W.GREEN))
	for goal in view.get("goals",[]):
		var item=goal;card.add_child(W.label(("✓ " if item.get("met",false) else "○ ")+str(item.get("label",item.id))+" · %.2f / %.2f %s"%[float(item.get("value",0))*(100 if item.get("unit","")=="%" else 1),float(item.get("target",0))*(100 if item.get("unit","")=="%" else 1),str(item.get("unit",""))],15,W.GREEN if item.get("met",false) else W.MUTED))
		if item.get("kind","")=="delivery" and not item.get("met",false):
			var resource=str(item.id).trim_prefix("delivery_")
			card.add_child(W.button("从库存交付",func():_command(d,g.Themes.deliver(g,resource))))
	var required=float(view.get("steady_required_seconds",0))
	if required>0:card.add_child(W.label("连续保持 %.2f / %.2f 来生月"%[float(view.get("steady_seconds",0))/MONTH,required/MONTH],14,W.MUTED))
	render_events(d,view)
	if int(view.get("reforge_tokens",0))>0 and not view.get("completed",false):
		var reforge=C.group(d.body,"稀有委任重铸")
		reforge.add_child(W.label("持有 %d 份 · 消耗 1 份 + %.1f DT；放弃未完成进度，保留已获和待领奖励。"%[int(view.reforge_tokens),float(view.get("reforge_dt_cost",7.5))],14,W.GOLD))
		for option in view.get("choices",[]):
			var item=option
			if str(item.id)==str(view.id):continue
			reforge.add_child(W.button("重铸为「%s」…"%str(item.name),func():confirm_reforge(d,item),not view.get("can_reforge",false)))
	var history=C.group(d.body,"主题记录")
	for entry in view.get("history",[]):history.add_child(W.label(str(entry.get("name",entry.get("id","")))+" · "+str(entry.get("status_label",entry.get("status",entry.get("kind","记录"))))+(" · "+str(entry.reason) if str(entry.get("reason",""))!="" else ""),14,W.MUTED))
	d.body.add_child(W.button("查看副本与蓝图",func():d.navigate("city040","permits")))
	d.body.add_child(W.button("本局记录与尾声",func():d.navigate("city040","block")))

static func _command(d,error:String) -> void:
	if error!="":d.app.message(error);return
	d.app.display_pending_tick=int(d.app.game.s.simulation_tick);d.app.save_game();d.app.refresh();d.render(true)

static func confirm_reforge(d,item:Dictionary) -> void:
	var app=d.app;app.open_modal("确认委任重铸","消耗稀有重铸许可与 %.1f DT，不推进日历；未完成的主题进度将放弃。"%float(app.game.Themes.view(app.game).get("reforge_dt_cost",7.5)))
	app.modal_body.add_child(W.primary(W.button("确认改为「%s」"%str(item.name),func():
		var error=app.game.Themes.reforge(app.game,str(item.id))
		if error!="":app.message(error);return
		app.close_modal();app.save_game();app.refresh();d.render(true))))

static func reward(d,choice:Dictionary) -> void:
	var g=d.app.game;var id=str(choice.get("reward_id",""));var kind=str(choice.get("kind","copy"));var card=C.group(d.body,{"research":"主题研究 · 选择蓝图升级","currency":"奖励补偿","copy":"选择一份方案副本"}.get(kind,"选择奖励"),W.SOFT)
	card.add_child(W.label("来源："+reward_source(g,str(choice.get("source",choice.get("source_id","城市成长"))))+" · 候选已保存",14,W.MUTED))
	for entry in choice.get("choices",[]):
		var item=entry;var bid=str(item.get("bonus_id",""));var name=str(item.get("name",g.c.bonuses.get(bid,{}).get("name","通货补偿")));var selected_id=bid
		var row=C.group(card,name)
		if bid=="__currency__":row.add_child(W.label("获得 %.0f 通货"%float(choice.get("compensation",item.get("compensation",30))),15))
		else:
			row.add_child(W.label(str(item.get("grade","基础"))+" · 库存 %d 份"%int(item.get("copies",0)),14,W.GREEN))
			row.add_child(W.label(str(item.get("preview_reason",g.c.bonuses.get(bid,{}).get("summary",""))),14,W.MUTED))
			effect_rows(row,item.get("effects",[]))
			row.add_child(W.label(str(item.get("stacking_note","同宿主首份 100%，追加每份 33⅓%；结构效果以详情为准。")),13,W.MUTED))
		row.add_child(W.button("研究升级" if kind=="research" else "领取",func():
			var error=g.Bonus.choose(g,id,selected_id)
			if error!="":d.app.message(error)
			d.app.display_pending_tick=int(d.app.game.s.simulation_tick);d.app.save_game();d.app.refresh();d.render(true)))

static func render_ending(d) -> void:
	var g=d.app.game;var ending=g.s.block.get("ending",{});var triggered=ending.get("triggered",false)
	d.title.text="本局城市";d.subtitle.text=date(float(g.s.get("beta_time",{}).get("local_elapsed_seconds",0)));d.cost.text=""
	var panel=C.group(d.body,str(g.s.block.get("display_name","河谷登陆点")))
	panel.add_child(W.label("高速发展预算已耗尽" if triggered else "高速发展预算：%.2f DT"%float(g.s.dt),21,W.GOLD if triggered else W.GREEN))
	if not triggered:
		panel.add_child(W.label("DT 首次实际归零时解锁命名；届时已完成委任主题，才能获得对应定型建筑。年度和待领奖励始终保留。",15,W.MUTED));return
	var input=LineEdit.new();input.max_length=24;input.placeholder_text="稍后命名，或在这里输入城市名称";input.custom_minimum_size.y=44;panel.add_child(input);input.visible=bool(g.s.block.get("naming_rights",false))
	panel.add_child(W.button("保存城市名称",func():
		var error=g.Block.name_block(g,input.text.strip_edges())
		if error!="":d.app.message(error)
		else:d.app.display_pending_tick=int(d.app.game.s.simulation_tick);d.app.save_game();d.app.refresh();d.render(true),not g.s.block.get("naming_rights",false)))
	if str(ending.get("eligible_theme",""))!="":
		panel.add_child(W.label("主题已完成 · 已取得定型建筑资格",16,W.GREEN))
		panel.add_child(W.primary(W.button("正式挂牌 · 领取主题定型建筑",func():_command(d,g.Block.claim_capstone(g)),ending.get("capstone_claimed",false))))
	else:panel.add_child(W.label("本次未达成主题奖励条件。继续常速经营可以完成主题，但不会追补此次资格。",15,W.MUTED))
	var landmark=g.s.block.get("capstone",{})
	if not landmark.is_empty():
		var source=C.group(d.body,str(landmark.get("name","主题定型建筑")))
		source.add_child(W.label("本区主题定型建筑 · 已正式挂牌",14,W.GREEN))
		for rule in landmark.get("rules",[]):source.add_child(W.label(landmark_rule(rule),14,W.TEXT))
		source.add_child(W.button("完整规则与来源",func():landmark_details(d.app,landmark)))
	var queue=pending(g)
	if not queue.is_empty():reward(d,queue[0]);d.body.add_child(W.label("仍有 %d 份奖励待领取；重开之前可以继续处理。"%queue.size(),14,W.GOLD))
	render_events(d,g.Themes.view(g))
	panel.add_child(W.button("稍后命名 · 继续常速经营",func():d.app.resume_beta_normal()))
	d.body.add_child(W.button("结算记录并独立重开…",func():confirm_archive(d.app)))

static func confirm_archive(app) -> void:
	var count=pending(app.game).size()
	app.open_modal("保存本局记录并独立重开？","将保存只读城市记录，再开启全新 Block；资源、卡、蓝图和地标不继承。"+(" 当前还有 %d 份未领取奖励。"%count if count>0 else ""))
	app.modal_body.add_child(W.label("旧城将停止经营。新局开始前，这份记录会先写入存储；写入失败时保留当前城市。",16,W.MUTED))
	app.modal_body.add_child(W.primary(W.button("确认保存记录并重开",func():
		var result=app.game.Block.archive(app.game)
		if not result.get("ok",false):app.message(str(result.get("error","保存记录失败")));return
		app.restart(int(Time.get_unix_time_from_system())))))

static func install_preview(app,bid:String,type:String,host:String,slot:String) -> void:
	var g=app.game;var data=g.Bonus.card(g,bid);var duplicate=g.Bonus.duplicate_preview(g,bid,type,host)
	app.open_modal(str(data.get("name",bid))+" · 安装预览",str(data.get("grade","基础"))+" · 安装消耗 1 份副本，卸载不返还。")
	app.modal_body.add_child(W.label(str(g.c.bonuses.get(bid,{}).get("summary","")),17))
	app.modal_body.add_child(W.label("同宿主已有 %d 份 · 合成强度 %.3f → %.3f"%[int(duplicate.get("count",0)),float(duplicate.get("strength",0)),float(duplicate.get("next_strength",1))],16,W.GREEN))
	app.modal_body.add_child(W.label(str(duplicate.get("note","首份 100%，追加每份 33⅓%；正负成本共同缩放。")),15,W.MUTED))
	effect_rows(app.modal_body,duplicate.get("effects",[]))
	app.modal_body.add_child(W.label("安装位置："+(g.project_name(host) if type=="building" else host)+(" · "+g.slot_label(host,slot) if slot!="" else ""),14,W.MUTED))
	app.modal_body.add_child(W.primary(W.button("确认消耗 1 份并安装",func():
		var error=g.Bonus.install(g,bid,type,host,slot)
		if error!="":app.message(error);return
		app.display_pending_tick=int(g.s.simulation_tick);app.save_game();app.close_modal();app.refresh()
		if app.desk.is_open():app.desk.render(true)
	,not duplicate.get("can_add",true))))

static func render_settlement(d) -> void:
	var g=d.app.game;var ending=g.s.block.get("ending",{})
	if ending.get("triggered",false):render_ending(d)
	else:
		render_themes(d)
		d.title.text="城市发展结算"
		d.subtitle.text=date(float(g.s.beta_time.local_elapsed_seconds))+" · 时间已暂停，依次处理奖励与主题事件。"
	for summary in g.s.block.get("cycles",{}).get("pending_summaries",[]):
		d.body.add_child(W.label("第 %d 年%s · 人口 %+.1f · 财政 %+.1f"%[int(summary.get("year",0))," / 两年大周期" if summary.get("major_cycle",false) else "",float(summary.get("population_delta",0)),float(summary.get("currency_delta",0))],17,W.GREEN))
	d.body.add_child(W.primary(W.button("完成结算 · 返回常速经营",func():d.app.resume_beta_normal(),d.app.beta_decisions_pending())))

static func effect_rows(parent:Node,rows:Array) -> void:
	for item in rows:
		var before=float(item.get("before",0));var after=float(item.get("after",0));var unit=str(item.get("unit",""))
		parent.add_child(W.label(str(item.get("label","数值"))+" · %.3f → %.3f %s"%[before,after,unit],14,W.TEXT))

static func reward_source(g,source:String) -> String:
	var parts=source.split(":")
	if parts[0]=="annual" and parts.size()>1:return "第%s年城市发展"%parts[1]
	if parts[0]=="population" and parts.size()>1:return "%s位居民稳定安居"%parts[1]
	if parts[0]=="theme" and parts.size()>1:
		var name=parts[1]
		for option in g.Themes.view(g).get("choices",[]):
			if str(option.id)==parts[1]:name=str(option.name)
		return name+" · 主题阶段"
	if parts[0] in ["event","theme_event"]:return "主题事件成果"
	return "城市成长奖励"

static func landmark_rule(rule:Dictionary) -> String:
	var fields={"output.goods":"制成品产出","personnel_mult":"人员效率","service.*":"服务容量","housing":"住房容量","storage.*":"仓储容量","trade_capacity":"货运能力","output.food":"粮食产出","input.food":"粮食投入","construction_capacity":"施工能力","tax_mult":"税收","output.material":"材料产出"}
	var field=str(rule.get("field",""));var value=float(rule.get("value",0))
	var targets="工业建筑" if "building.industrial" in rule.get("when",{}).get("any_traits",[]) else ("公共服务建筑" if "building.public_service" in rule.get("when",{}).get("any_traits",[]) else "适用建筑")
	return targets+" · "+str(fields.get(field,field))+((" %+.0f%%"%((value-1)*100)) if rule.get("mode","")=="multiply" else (" %+.2f"%value))

static func landmark_details(app,landmark:Dictionary) -> void:
	app.open_modal(str(landmark.get("name","地标"))+" · 规则来源","来源标记："+str(landmark.get("source_id","")))
	for rule in landmark.get("rules",[]):
		app.modal_body.add_child(W.label(landmark_rule(rule),16,W.GREEN));app.modal_body.add_child(W.label(JSON.stringify(rule),14,W.MUTED))

static func render_events(d,view:Dictionary) -> void:
	var g=d.app.game
	for event in view.get("events",[]):
		var item=event;var box=C.group(d.body,str(item.get("name","主题事件")),W.SOFT);box.add_child(W.label(str(item.get("description","")),15))
		var deadline=float(item.get("deadline_seconds",0))
		if deadline>0:box.add_child(W.label("剩余 %.2f 来生月"%(deadline/MONTH),14,W.GOLD))
		for option in item.get("choices",[]):
			var choice=option;box.add_child(W.button(str(choice.get("label",choice.id)),func():_command(d,g.Themes.choose_event(g,str(item.id),str(choice.id)))))
		if item.get("status","")=="active" and float(item.get("delivery_remaining",0))>0:
			box.add_child(W.button("交付%s · 还需%.1f"%[str(item.get("delivery_resource","物资")),float(item.delivery_remaining)],func():_command(d,g.Themes.deliver(g,str(item.get("delivery_resource","")),-1,str(item.id)))))

static func risk_snapshot(app) -> Dictionary:
	# Commands may change the next cycle before a committed tick exists. Reuse
	# the planner once then; ordinary live HUD never runs a second simulation.
	if int(app.display_pending_tick)==int(app.game.s.simulation_tick) or int(app.game.last_committed_revision)!=int(app.game.revision):return app.planner.snapshot(app.game)
	return app.shot

static func trade_continues(g,resource:String,side:String) -> bool:
	if float(g.Market._total_capacity(g))<=0:return false
	var quote=g.Market.quote(g,resource);var price=float(quote.get(side,0))
	for order in g.s.block.get("trade_orders",[]):
		if str(order.get("resource",""))!=resource or str(order.get("side",""))!=side:continue
		if str(order.get("status","")) not in ["open","partial"] or float(order.get("remaining",0))<=0:continue
		var limit=float(order.get("limit_price",0))
		if side=="buy" and (price>limit or (price>0 and float(g.s.currency)<=0)):continue
		if side=="sell" and (price<limit or (price<0 and float(g.s.currency)<=0)):continue
		return true
	return false
