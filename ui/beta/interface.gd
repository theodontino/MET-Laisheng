extends RefCounted
## Native Beta presentation. These functions read authoritative state, with mutations
## confined to explicitly selected commands. Display formatting never touches RNG.
const W=preload("res://ui/widgets.gd")
const C=preload("res://ui/components031/components.gd")
const V=preload("res://ui/reset/desk_visuals.gd")
const Rules=preload("res://core/rules.gd")
const Visual=preload("res://ui/components031/visual.gd")
const Design=preload("res://ui/components031/design.gd")
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
	app.display_pending_tick=int(app.game.s.simulation_tick) if id!="normal" else -1
	app.game.s.settings.user_paused=false;app.game.s.beta_time.pause_reason=""
	if app.desk.is_open():app.desk.close()
	app.save_game();app.refresh();app.message("时间档位："+mode_name(app.game))

static func render_time(d) -> void:
	var g=d.app.game;var state=g.s.get("beta_time",{});var local=float(state.get("local_elapsed_seconds",0));var a=acceleration(g)
	d.title.text="城市时间";d.subtitle.text=date(local)+" · 6日一旬 · 12日一月 · 144日一年";d.cost.text=""
	var budget=V.card(d.body,"高速发展预算","DT 为额外本地时间付费", "time",W.GOLD);budget.set_meta("beta_time_budget",true)
	var available=float(g.s.dt);var used=float(g.s.get("totals",{}).get("dt_used",0))
	V.meter(budget,"剩余 / 本局剩余与已用合计",available,available+used,"DT",W.GOLD,"time")
	var figures=V.grid(budget,3)
	V.metric(figures,"time","已消耗","%.2f DT"%used,"来自本局实际预算流水",W.MUTED)
	V.metric(figures,"time","可购买额外时间","%.2f 来生年"%(available*dt_seconds(g)/YEAR),"仅计算 DT 能增加的本地时间",W.GOLD)
	var burn=maxf(0,a-1.0)/DT_SECONDS
	V.metric(figures,"time","当前档位",mode_name(g)+" ×%.0f"%a,"消耗 %.3f DT / 现实分钟"%(burn*60) if burn>0 else "自然运行不消耗 DT",W.ACCENT)
	var calendar=V.card(d.body,"来生历","年度与两年大周期均读取当前本地时间", "time")
	V.meter(calendar,"本年度已走过",fmod(local,YEAR)/DAY,YEAR/DAY,"来生日",W.ACCENT,"time")
	var horizons=V.grid(calendar,3 if burn>0 else 2)
	V.metric(horizons,"bonus","下一年度",seconds_text((YEAR-fmod(local,YEAR))/a),"现实活跃时间预测；决策期间暂停")
	V.metric(horizons,"theme","下一大周期",seconds_text((2*YEAR-fmod(local,2*YEAR))/a),"两年总结，不重复发年度普通卡")
	if burn>0:V.metric(horizons,"time","预算可运行",seconds_text(available/burn),"按当前档位的现实活跃时间",W.GOLD)
	var modes=V.grid(d.body,mode_rows(g).size())
	for mode in mode_rows(g):
		var id=str(mode[0]);var disabled=available<=0 and id!="normal";var selected=str(state.get("mode","normal"))==id
		var mode_card=V.card(modes,str(mode[1]),"×%.0f 本地时间"%float(mode[2]),"time",W.ACCENT if selected else W.MUTED)
		V.badge(mode_card,"当前档位" if selected else ("预算耗尽" if disabled else "可切换"),W.ACCENT if selected else W.MUTED)
		var pace=float(mode[2]);mode_card.add_child(W.label("1 来生月 / "+seconds_text(MONTH/pace)+"现实活跃时间",14,W.MUTED))
		var button=W.button("%s ×%.0f"%[mode[1],mode[2]],func():set_mode(d.app,id),disabled)
		if selected:W.primary(button)
		mode_card.add_child(button)
	var estimate=risk(g,risk_snapshot(d.app));var caution=V.card(d.body,"推进前看库存",str(estimate.text),str(estimate.id),W.GREEN if estimate.stable else W.GOLD)
	caution.add_child(W.button("继续 / 暂停城市",func():d.toggle_pause()))
	var rules=V.fold(d.body,"时间与离开规则")
	rules.add_child(W.label("暂停、关闭和后台均冻结城市与外贸。离开时长只记录，不发资源、DT或奖励。移民按本地时间抵达；外贸只按真实活跃在线时间成交。",14,W.MUTED))

static func render_themes(d) -> void:
	var g=d.app.game;var view=g.Themes.view(g)
	d.title.text="委任主题";d.subtitle.text="选择这座城市的发展方向；年度和安居人口奖励独立获得。";d.cost.text=""
	var queue=pending(g)
	if not queue.is_empty():
		var ahead=0
		for entry in queue:
			if str(entry.get("reward_id",""))==d.preferred_reward:break
			ahead+=1
		if d.preferred_reward!="" and ahead>0 and ahead<queue.size():
			var waiting=V.card(d.body,"主题奖励已保存","前面还有 %d 份待选奖励，按序处理后即可领取。"%ahead,"bonus",W.GOLD)
			V.badge(waiting,"保留候选 · 按序领取",W.GOLD)
		reward(d,queue[0]);d.body.add_child(W.label("待领取 %d 份 · 每次按保存顺序处理一组候选"%queue.size(),14,W.MUTED))
	if str(view.get("id",""))=="":
		var selection=V.grid(d.body,3)
		for option in view.get("choices",[]):
			var item=option;var definition=_theme_definition(str(item.id));var group=V.card(selection,str(item.name),str(item.get("description","")),_theme_icon(str(item.id)))
			var labels=[]
			for stage in definition.get("stages",[]):labels.append(str(stage.get("name",stage.get("id",""))))
			V.steps(group,labels,-1)
			var first=definition.get("stages",[])
			if not first.is_empty():
				for goal in first[0].get("goals",[]):group.add_child(W.label("起步 · "+_goal_requirement(goal),14,W.MUTED))
			group.add_child(W.primary(W.button("选择「%s」"%str(item.name),func():_command(d,g.Themes.select(g,str(item.id))))))
		return
	var completed=bool(view.get("completed",false));var definition=_theme_definition(str(view.id))
	var card=V.card(d.body,str(view.get("name","")),str(view.get("stage_name",""))+" · 当前实际目标",_theme_icon(str(view.id)),W.GREEN if completed else W.ACCENT)
	var labels=[]
	for stage in definition.get("stages",[]):labels.append(str(stage.get("name",stage.get("id",""))))
	V.steps(card,labels,3 if completed else int(view.get("stage",1))-1).set_meta("beta_theme_stage",true)
	if completed:V.badge(card,"✓ 主题完成 · DT 首次归零时具备定型建筑资格",W.GREEN)
	var goals=V.grid(card,2)
	for goal in view.get("goals",[]):
		var item=goal;var goal_card=V.card(goals);goal_card.set_meta("beta_goal",str(item.get("id","")))
		_goal_meter(goal_card,item)
		if item.get("kind","")=="delivery" and not item.get("met",false):
			var resource=str(item.id).trim_prefix("delivery_")
			goal_card.add_child(W.button("从库存交付",func():_command(d,g.Themes.deliver(g,resource))))
	var required=float(view.get("steady_required_seconds",0))
	if required>0:
		var steady=V.card(card,"连续达标","状态目标需连续保持；中断按真实判定重新计时", "time")
		V.meter(steady,"已保持 / 本阶段要求",float(view.get("steady_seconds",0))/MONTH,required/MONTH,"来生月",W.GREEN)
	render_events(d,view)
	if int(view.get("reforge_tokens",0))>0 and not completed:
		var reforge=V.card(d.body,"稀有委任重铸","放弃未完成进度，保留已获和待领奖励。","theme",W.GOLD)
		V.flow(reforge,[{"id":"bonus","label":"持有重铸许可","value":"%d 份"%int(view.reforge_tokens)},{"id":"time","label":"更换代价","value":"1 份 + %.1f DT"%float(view.get("reforge_dt_cost",7.5))},{"id":"theme","label":"结果","value":"改向 · 进度重置"}])
		var options=V.grid(reforge,2)
		for option in view.get("choices",[]):
			var item=option
			if str(item.id)==str(view.id):continue
			options.add_child(W.button("重铸为「%s」…"%str(item.name),func():confirm_reforge(d,item),not view.get("can_reforge",false)))
	var history=V.fold(d.body,"主题记录","%d 条实际记录"%view.get("history",[]).size())
	if view.get("history",[]).is_empty():history.add_child(W.label("当前没有主题记录。",14,W.MUTED))
	for entry in view.get("history",[]):
		var record=V.card(history,str(entry.get("name",entry.get("id",""))),str(entry.get("status_label",entry.get("status",entry.get("kind","记录")))),"theme")
		if str(entry.get("reason",""))!="":record.add_child(W.label(str(entry.reason),14,W.MUTED))
	var links=W.hbox(d.body,12)
	links.add_child(W.button("查看副本与蓝图",func():d.navigate("city040","permits")))
	links.add_child(W.button("本局记录与尾声",func():d.navigate("city040","block")))

static func _command(d,error:String) -> void:
	if error!="":d.app.message(error);return
	d.app.display_pending_tick=int(d.app.game.s.simulation_tick);d.app.save_game();d.app.refresh();d.render(true)

static func confirm_reforge(d,item:Dictionary) -> void:
	var app=d.app;var cost=float(app.game.Themes.view(app.game).get("reforge_dt_cost",7.5))
	app.open_modal("确认委任重铸","未完成的主题进度将放弃；已经获得与待领取的奖励保留。")
	V.flow(app.modal_body,[{"id":"theme","label":"改向","value":str(item.name)},{"id":"bonus","label":"许可消耗","value":"1 份重铸权"},{"id":"time","label":"预算消耗","value":"%.1f DT"%cost}])
	V.badge(app.modal_body,"不推进日历 · 新主题从第一阶段开始",W.GOLD)
	app.modal_body.add_child(W.primary(W.button("确认改为「%s」"%str(item.name),func():
		var error=app.game.Themes.reforge(app.game,str(item.id))
		if error!="":app.message(error);return
		app.close_modal();app.save_game();app.refresh();d.render(true))))

static func reward(d,choice:Dictionary) -> void:
	var g=d.app.game;var id=str(choice.get("reward_id",""));var kind=str(choice.get("kind","copy"))
	var title={"research":"主题研究 · 选择蓝图升级","currency":"奖励补偿","copy":"选择一份方案副本"}.get(kind,"选择奖励")
	var card=V.card(d.body,title,"来源："+reward_source(g,str(choice.get("source",choice.get("source_id","城市成长"))))+" · 候选已保存","research" if kind=="research" else "bonus",W.GOLD)
	card.set_meta("beta_reward_compare",true)
	V.badge(card,"选择 1 项 · 按保存顺序处理",W.GOLD)
	var entries:Array=choice.get("choices",[]);var comparison=V.grid(card,maxi(1,entries.size()))
	for entry in entries:
		var item:Dictionary=entry;var bid=str(item.get("bonus_id",""));var name=str(item.get("name",g.c.bonuses.get(bid,{}).get("name","通货补偿")));var selected_id=bid
		var row=V.card(comparison);row.set_meta("beta_reward_candidate",true);row.set_meta("bonus_id",bid);row.set_meta("reward_kind",kind)
		var action=W.primary(W.button("研究升级" if kind=="research" else "领取",func():
			var error=g.Bonus.choose(g,id,selected_id)
			if error!="":d.app.message(error)
			d.app.display_pending_tick=int(d.app.game.s.simulation_tick);d.app.save_game();d.app.refresh();d.render(true)))
		action.set_meta("beta_reward_action",selected_id)
		if bid=="__currency__":
			V.icon(row,"currency",64,W.GOLD);row.add_child(W.label(name,21))
			V.badge(row,"通货补偿 · 不恢复 DT",W.GOLD)
			row.add_child(action)
			row.add_child(W.label("+%.0f 通货"%float(choice.get("compensation",item.get("compensation",30))),30,W.GOLD))
			row.add_child(W.label(str(item.get("preview_reason","领取城市发展补助。")),14,W.MUTED))
		else:
			_bonus_art(row,bid)
			row.add_child(W.label(name,21))
			if kind=="research":
				V.badge(row,"蓝图研究 · "+str(item.get("grade","基础"))+" → "+str(item.get("target_grade","升级")),W.INFO)
			else:
				V.badge(row,"+1 份副本 · "+str(item.get("grade","基础")),W.GREEN)
			row.add_child(action)
			if kind=="research":
				row.add_child(W.label("本局全部同名副本升级；库存副本不消耗，部署尺度不变",14,W.INFO))
			else:
				row.add_child(W.label("候选生成时库存 %d 份 · 领取后可选宿主部署"%int(item.get("copies",0)),14,W.MUTED))
			row.add_child(W.label(str(item.get("preview_reason","")),14,W.MUTED))
			var effects:Array=item.get("effects",[])
			if effects.is_empty():row.add_child(W.label("结构或条件效果 · 完整规则可展开",14,W.MUTED))
			effect_rows(row,effects.slice(0,2))
			if effects.size()>2:row.add_child(W.label("另有 %d 项数值变化 · 展开查看"%(effects.size()-2),14,W.MUTED))
			var details=V.fold(row,"完整规则与副本说明")
			details.add_child(W.label(str(item.get("stacking_note","同宿主首份 100%，追加每份 33⅓%；结构效果以详情为准。")),14,W.MUTED))
			if effects.size()>2:effect_rows(details,effects.slice(2))
			_bonus_rules(details,g,bid,1,int(item.get("target_level",item.get("blueprint_level",0))))

static func render_ending(d) -> void:
	var g=d.app.game;var ending=g.s.block.get("ending",{});var triggered=ending.get("triggered",false)
	d.title.text="本局城市";d.subtitle.text=date(float(g.s.get("beta_time",{}).get("local_elapsed_seconds",0)));d.cost.text=""
	var panel=V.card(d.body,str(g.s.block.get("display_name","河谷登陆点")),"高速发展预算已耗尽" if triggered else "城市仍在发展中","theme",W.GOLD if triggered else W.GREEN);panel.set_meta("beta_ending",true)
	var can_name=bool(g.s.block.get("naming_rights",false))
	V.steps(panel,["发展城市","命名与挂牌","留存记录"],0 if not triggered else (1 if str(ending.get("archive_id",""))=="" else 2))
	var metrics=V.grid(panel,3)
	V.metric(metrics,"population","城市人口","%.1f 人"%float(g.s.get("metrics",{}).get("population",0)),"当前城市实际人口")
	V.metric(metrics,"time","本局预算","%.2f DT"%float(g.s.dt),"已使用 %.2f DT"%float(g.s.get("totals",{}).get("dt_used",0)),W.GOLD)
	V.metric(metrics,"theme","城市方向",str(g.Themes.view(g).get("name","尚未选择")),"主题状态由实际目标判定")
	if not triggered:
		panel.add_child(W.label("DT 首次实际归零时解锁命名；届时已完成委任主题，才能获得对应定型建筑。年度和待领奖励始终保留。",15,W.MUTED));return
	var choices=V.grid(d.body,2)
	var naming=V.card(choices,"为城市命名","一次命名权 · 可以稍后使用","theme",W.GOLD)
	V.badge(naming,"命名权可用" if can_name else "名称已保存",W.GOLD if can_name else W.GREEN)
	var input=LineEdit.new();input.max_length=int(g.c.v040.get("progression",{}).get("name_max_length",24));input.placeholder_text="输入城市名称";input.custom_minimum_size.y=44;naming.add_child(input);input.visible=can_name
	naming.add_child(W.label(str(g.s.block.get("display_name","")),24,W.TEXT))
	naming.add_child(W.button("保存城市名称",func():
		var error=g.Block.name_block(g,input.text.strip_edges())
		if error!="":d.app.message(error)
		else:d.app.display_pending_tick=int(g.s.simulation_tick);d.app.save_game();d.app.refresh();d.render(true),not can_name))
	var eligible=str(ending.get("eligible_theme",""));var landmark=g.s.block.get("capstone",{})
	var capstone=landmark if not landmark.is_empty() else g.Block.capstone_definitions().get(eligible,{})
	var source=V.card(choices,str(capstone.get("name","主题定型建筑")),"市政地标 · 资格固定于 DT 首次归零时",_theme_icon(eligible),W.GREEN if eligible!="" else W.MUTED)
	if eligible!="":
		V.badge(source,"已正式挂牌" if ending.get("capstone_claimed",false) else "主题已完成 · 具备挂牌资格",W.GREEN)
		for rule in capstone.get("rules",[]):source.add_child(W.label(landmark_rule(rule),15,W.TEXT))
		source.add_child(W.primary(W.button("正式挂牌 · 领取主题定型建筑",func():_command(d,g.Block.claim_capstone(g)),ending.get("capstone_claimed",false))))
		if not landmark.is_empty():source.add_child(W.button("完整规则与来源",func():landmark_details(d.app,landmark)))
	else:
		V.badge(source,"此次未取得定型建筑资格",W.MUTED)
		source.add_child(W.label("本次未达成主题奖励条件。继续常速经营可以完成主题，但不会追补此次资格。",15,W.MUTED))
	var queue=pending(g)
	if not queue.is_empty():reward(d,queue[0]);d.body.add_child(W.label("仍有 %d 份奖励待领取；重开之前可以继续处理。"%queue.size(),14,W.GOLD))
	render_events(d,g.Themes.view(g))
	var next=V.card(d.body,"接下来","城市可以继续常速经营，也可以留存本局后开启独立新城。","theme")
	var actions=W.hbox(next,12)
	actions.add_child(W.primary(W.button("稍后命名 · 继续常速经营",func():d.app.resume_beta_normal())))
	actions.add_child(W.button("结算记录并独立重开…",func():confirm_archive(d.app)))
	actions.add_child(W.button("查看已保存城市记录",func():d.app.open_city_records()))

static func confirm_archive(app) -> void:
	var count=pending(app.game).size()
	app.open_modal("保存本局记录并独立重开？","将保存只读城市记录，再开启全新 Block；资源、卡、蓝图和地标不继承。")
	V.steps(app.modal_body,["核对当前城市","写入只读记录","开启独立新局"],0)
	V.flow(app.modal_body,[{"id":"theme","label":"本局城市","value":str(app.game.s.block.get("display_name","城市"))},{"id":"bonus","label":"待选奖励","value":"%d 份"%count,"note":"候选与领取状态写入记录"},{"id":"theme","label":"新局","value":"独立 Block","note":"城市与构筑重新开始"}])
	var storage=V.card(app.modal_body,"保存后才重开","旧城将停止经营；记录保存失败时保留当前城市。","theme",W.GOLD)
	if count>0:V.badge(storage,"当前还有 %d 份未领取奖励"%count,W.GOLD)
	storage.add_child(W.primary(W.button("确认保存记录并重开",func():
		var result=app.game.Block.archive(app.game)
		if not result.get("ok",false):app.message(str(result.get("error","保存记录失败")));return
		app.restart(int(Time.get_unix_time_from_system())))))

static func install_preview(app,bid:String,type:String,host:String,slot:String) -> void:
	var g=app.game;var data=g.Bonus.card(g,bid);var duplicate=g.Bonus.duplicate_preview(g,bid,type,host)
	app.open_modal(str(data.get("name",bid))+" · 安装预览",str(data.get("grade","基础"))+" · 安装消耗 1 份副本，卸载不返还。")
	var host_name=g.project_name(host) if type=="building" else (str(g.s.get("organizations",{}).get(host,{}).get("name",host)) if type=="organization" else str(g.s.block.get("display_name",host)))
	var location="安装位置："+host_name+(" · "+g.slot_label(host,slot) if slot!="" else "")
	var project:Dictionary=g.s.get("projects",{}).get(host,{})
	var identity=V.building(app.modal_body,str(project.get("node_id",project.get("def",""))),host_name,location) if type=="building" else V.card(app.modal_body,host_name,location,"bonus")
	V.flow(identity,[{"id":"bonus","label":"同宿主已有","value":"%d 份"%int(duplicate.get("count",0))},{"id":"bonus","label":"安装后","value":"%d 份"%(int(duplicate.get("count",0))+1)},{"id":"bonus","label":"库存消耗","value":"1 份","note":"卸载不返还"}])
	var comparison=V.card(app.modal_body,"同宿主合成强度","读取实际蓝图等级与同宿主重复副本规则", "bonus")
	V.flow(comparison,[{"id":"bonus","label":"安装前","value":"%.3f"%float(duplicate.get("strength",0))},{"id":"bonus","label":"安装后","value":"%.3f"%float(duplicate.get("next_strength",1))}])
	V.badge(comparison,"可以追加" if duplicate.get("can_add",true) else "无新增收益 · 不消耗副本",W.ACCENT if duplicate.get("can_add",true) else W.GOLD)
	comparison.add_child(W.label(str(duplicate.get("note","首份 100%，追加每份 33⅓%；正负成本共同缩放。")),15,W.MUTED))
	var effects=V.grid(app.modal_body,2)
	for effect in duplicate.get("effects",[]):effect_rows(V.card(effects),[effect])
	var full=V.fold(app.modal_body,"完整规则与安装资格")
	_bonus_rules(full,g,bid,int(duplicate.get("count",0))+1,int(data.get("blueprint_level",0)))
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
	var summaries:Array=g.s.block.get("cycles",{}).get("pending_summaries",[])
	if not summaries.is_empty():
		var records=V.card(d.body,"年度变化","来自实际周期结算记录；不同单位独立呈现。","time")
		for summary in summaries:
			var year=V.card(records,"第 %d 年%s"%[int(summary.get("year",0))," / 两年大周期" if summary.get("major_cycle",false) else ""],"本周期已记录变化","time")
			var metrics=V.grid(year,2)
			V.metric(metrics,"population","人口变化","%+.1f 人"%float(summary.get("population_delta",0)),"实际周期人口差分",W.TEXT)
			V.metric(metrics,"currency","财政变化","%+.1f 通货"%float(summary.get("currency_delta",0)),"实际周期通货差分",W.TEXT)
	d.body.add_child(W.primary(W.button("完成结算 · 返回常速经营",func():d.app.resume_beta_normal(),d.app.beta_decisions_pending())))

static func effect_rows(parent:Node,rows:Array) -> void:
	for item in rows:
		var before=float(item.get("before",0));var after=float(item.get("after",0));var unit=str(item.get("unit",""))
		var body=W.vbox(parent,5);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL;body.set_meta("beta_effect",str(item.get("rule_id","")))
		body.add_child(W.label(str(item.get("label","数值")),14,W.MUTED))
		var line=W.hbox(body,8)
		line.add_child(W.label("%.3f"%before,20,W.MUTED))
		var arrow=W.label("→",20,W.MUTED,false);arrow.size_flags_horizontal=Control.SIZE_SHRINK_CENTER;line.add_child(arrow)
		line.add_child(W.label("%.3f %s"%[after,unit],20,W.TEXT))
		body.add_child(W.label("绝对变化 %+.3f %s"%[after-before,unit],14,W.MUTED))

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
	var fields={"output.goods":"制成品产出","personnel_mult":"人员效率","service.commercial":"商业服务容量","storage.food":"粮食仓容","service.*":"服务容量","housing":"住房容量","storage.*":"仓储容量","trade_capacity":"货运能力","output.food":"粮食产出","input.food":"粮食投入","construction_capacity":"施工能力","commercial_tax_multiplier":"商业税收倍率","tax_mult":"税收","output.material":"材料产出"}
	var traits=rule.get("when",{}).get("any_traits",[]);var names={"building.industrial":"工业建筑","building.public_service":"公共服务建筑","building.commercial":"商业建筑","building.logistics":"物流建筑","building.agricultural":"农业建筑"}
	var targets=[]
	for trait_id in traits:targets.append(str(names.get(str(trait_id),str(trait_id))))
	var target=" / ".join(targets) if not targets.is_empty() else "所有适用建筑"
	if bool(rule.get("include_special",false)):target+="（含特殊实体）"
	var field=str(rule.get("field",""));var value=float(rule.get("value",0));var effect="%+.2f"%value
	match str(rule.get("mode","")):
		"multiply":effect="×%.2f（%+.0f%%）"%[value,(value-1)*100]
		"floor":effect="下限 %.2f"%value
		"cap":effect="上限 %.2f"%value
		"override":effect="设为 %.2f"%value
	return target+" · "+str(fields.get(field,field))+" "+effect

static func landmark_details(app,landmark:Dictionary) -> void:
	app.open_modal(str(landmark.get("name","地标"))+" · 规则来源","已正式挂牌的市政定型建筑")
	var identity=V.card(app.modal_body,str(landmark.get("name","地标")),"来源标记："+str(landmark.get("source_id","")),_theme_icon(str(landmark.get("theme",""))),W.GREEN)
	V.badge(identity,"本区生效 · 市政地标",W.GREEN)
	var rules=V.grid(app.modal_body,2)
	for rule in landmark.get("rules",[]):
		var row=V.card(rules,landmark_rule(rule),"实际规则 · "+str(rule.get("id","")),_field_icon(str(rule.get("field",""))))
		var source=V.fold(row,"完整规则来源")
		source.add_child(W.label(JSON.stringify(rule,"  "),14,W.MUTED))

static func render_events(d,view:Dictionary) -> void:
	var g=d.app.game
	for event in view.get("events",[]):
		var item:Dictionary=event;var def=_event_definition(str(item.id));var offered=str(item.get("status",""))=="offered"
		var box=V.card(d.body,str(item.get("name","主题事件")),str(item.get("description","")),_theme_icon(str(def.get("theme",view.get("id","")))),W.GOLD if offered else W.ACCENT);box.set_meta("beta_event",str(item.id))
		var status=W.hbox(box,12);V.badge(status,"等待决定" if offered else "合同进行中",W.GOLD if offered else W.ACCENT)
		var deadline=float(item.get("deadline_seconds",-1))
		if deadline>=0:V.badge(status,"期限已到" if deadline<=0 else "剩余 %.2f 来生月"%(deadline/MONTH),W.GOLD)
		elif offered and float(def.get("deadline_months",0))>0:V.badge(status,"接受后 %.0f 来生月内完成"%float(def.deadline_months),W.GOLD)
		var terms=V.grid(box,3)
		var condition=V.card(terms,"完成条件","接受后按实际城市变化结算","theme")
		condition.add_child(W.label(_event_goal(def,item),15,W.TEXT))
		var payment=V.card(terms,"接受时支付" if offered else "接受时已支付","检查实际通货与库存后支付","currency",W.GOLD)
		payment.add_child(W.label(_event_cost(def),18,W.GOLD))
		var outcome=V.card(terms,"成功结果","研究和副本候选按实际奖励队列领取","bonus",W.GREEN)
		outcome.add_child(W.label(_event_outcome(def),18,W.TEXT))
		var target=float(item.get("target",0))
		if not offered and target>0:
			var unit=str(item.get("unit","人·月" if def.has("person_months") else "单位"));var factor=100. if unit=="%" else 1.
			V.meter(box,"实际进度",float(item.get("progress",0))*factor,target*factor,unit,W.ACCENT,_field_icon(str(def.get("metric",def.get("delivery_resource",def.get("trade_resource",""))))))
		var facts=V.fold(box,"出现条件与合同细则")
		facts.add_child(W.label("出现条件（生成事件时检查）："+_event_trigger(def.get("trigger",{})),14,W.MUTED))
		if float(def.get("reserve_months",0))>0:facts.add_child(W.label("交付后须保留 %.0f 来生月实际口粮。"%float(def.reserve_months),14,W.GOLD))
		if float(def.get("deadline_months",0))>0:facts.add_child(W.label("未在本地时间期限内达成，只失去该机会与已经支付的保证金。",14,W.MUTED))
		for option in item.get("choices",[]):
			var choice:Dictionary=option;var button=W.button(str(choice.get("label",choice.id)),func():_command(d,g.Themes.choose_event(g,str(item.id),str(choice.id))),false,str(choice.get("cost_text","")))
			if str(choice.id)=="accept":W.primary(button)
			box.add_child(button)
		if offered:box.add_child(W.label("婉拒：不支付代价，不附加惩罚。",14,W.MUTED))
		if item.get("status","")=="active" and float(item.get("delivery_remaining",0))>0:
			var resource=str(item.get("delivery_resource",""));var resource_name=str(g.c.RESOURCE_NAMES.get(resource,resource))
			box.add_child(W.primary(W.button("交付%s · 还需%.1f"%[resource_name,float(item.delivery_remaining)],func():_command(d,g.Themes.deliver(g,str(item.get("delivery_resource","")),-1,str(item.id))))))

static func _theme_icon(id:String) -> String:
	return str({"industrial":"production","civic":"home","commercial":"commercial","logistics":"logistics","agricultural":"food"}.get(id,"theme"))

static func _field_icon(id:String) -> String:
	if "food" in id:return "food"
	if "goods" in id:return "goods"
	if "material" in id:return "material"
	if "energy" in id:return "energy"
	if "medical" in id or "health" in id:return "health"
	if "education" in id:return "education"
	if "entertainment" in id or "happiness" in id:return "happiness"
	if "commercial" in id:return "commercial"
	if "trade" in id or "storage" in id:return "logistics"
	if "currency" in id or "tax" in id or "maintenance" in id:return "currency"
	if "housed" in id or "homeless" in id or "housing" in id:return "population"
	if "labor" in id or "jobs" in id or "personnel" in id:return "jobs"
	return "bonus"

static func _theme_definition(id:String) -> Dictionary:
	var rows=JSON.parse_string(FileAccess.get_file_as_string("res://content/beta/commission_themes.json"))
	if rows is Array:
		for item in rows:
			if str(item.get("id",""))==id:return item
	return {}

static func _event_definition(id:String) -> Dictionary:
	var rows=JSON.parse_string(FileAccess.get_file_as_string("res://content/beta/commission_events.json"))
	if rows is Array:
		for item in rows:
			if str(item.get("id",""))==id:return item
	return {}

static func _goal_requirement(goal:Dictionary) -> String:
	var unit=str(goal.get("unit",""));var factor=100. if unit=="%" else 1.
	var target=float(goal.get("target",goal.get("min",goal.get("max",0))))*factor
	var upper=str(goal.get("comparison","max" if goal.has("max") else "min"))=="max"
	return str(goal.get("label",goal.get("id","")))+(" ≤ " if upper else " ≥ ")+"%.2f %s"%[target,unit]

static func _goal_meter(parent:Node,goal:Dictionary) -> void:
	var unit=str(goal.get("unit",""));var factor=100. if unit=="%" else 1.;var value=float(goal.get("value",0))*factor;var target=float(goal.get("target",0))*factor
	var met=bool(goal.get("met",false));var upper=str(goal.get("comparison","min"))=="max"
	var tint=W.GREEN if met else W.GOLD
	var title=W.hbox(parent,10);V.icon(title,_field_icon(str(goal.get("id",""))),28,tint);title.add_child(W.label(str(goal.get("label",goal.get("id",""))),16))
	var amount=W.hbox(parent,10);amount.add_child(W.label("%.2f %s"%[value,unit],27,tint));V.badge(amount,"✓ 达标" if met else "待达标",tint)
	parent.add_child(W.label(("允许上限 ≤ " if upper else "目标 ≥ ")+"%.2f %s"%[target,unit],14,W.MUTED))
	# Upper limits are constraints, not progress: growing beyond the limit is
	# never depicted as increased completion. Binary zero targets use saved met.
	if not upper and target>0:
		var bar=W.bar(value/target);bar.add_theme_stylebox_override("fill",W.box(tint,Color.TRANSPARENT,4));bar.mouse_filter=Control.MOUSE_FILTER_IGNORE;parent.add_child(bar)
	elif upper:V.badge(parent,"上限约束 · 越低越好",W.MUTED)
	else:V.badge(parent,"按实际状态判定",W.MUTED)
	var kind={"state":"实际城市状态","delivery":"主动库存交付","accumulate":"阶段实际累计"}.get(str(goal.get("kind","")),"实际城市记录")
	parent.add_child(W.label(kind,14,W.MUTED))

static func _bonus_art(parent:Node,bid:String) -> void:
	Design.init();var data:Dictionary=Design.cards.get(bid,{});var art=Visual.new()
	art.domain_id=str(data.get("visual_domain","general"));art.asset=Design.hero(data);art.custom_minimum_size.y=78;art.mouse_filter=Control.MOUSE_FILTER_IGNORE;parent.add_child(art)

static func _bonus_rules(parent:Node,g,bid:String,count:int,level:int) -> void:
	var definition:Dictionary=g.c.bonuses.get(bid,{})
	var hosts=[]
	for host in definition.get("hosts",[]):hosts.append(str({"building":"建筑","block":"地区","organization":"组织"}.get(str(host),str(host))))
	var slots=[]
	for slot in definition.get("allowed_slots",[]):slots.append(Design.slot_label(str(slot)))
	parent.add_child(W.label("宿主："+" / ".join(hosts)+(" · 槽位："+" / ".join(slots) if not slots.is_empty() else ""),14,W.MUTED))
	var gate=definition.get("install_gate",definition.get("gate",{}))
	if not gate.is_empty():parent.add_child(W.label("安装资格："+JSON.stringify(gate),14,W.MUTED))
	if not definition.get("excludes",[]).is_empty():parent.add_child(W.label("互斥方案："+" / ".join(definition.excludes),14,W.MUTED))
	parent.add_child(W.label("蓝图等级 %d · 同宿主 %d 份的完整规则"%[level,count],14,W.INFO))
	for rule in Rules.bonus_rules(g,bid,count,level):
		var row=V.fold(parent,str(rule.get("id","规则"))+" · "+str(rule.get("op","")))
		row.add_child(W.label(JSON.stringify(rule,"  "),14,W.MUTED))

static func _event_goal(def:Dictionary,item:Dictionary) -> String:
	if def.is_empty():return "接受后获得一份重铸权；使用时仍须支付 DT。"
	var result=[]
	if def.has("metric"):
		result.append("实际覆盖提高 %.0f 个百分点"%(float(def.get("improve",0))*100))
		if item.has("target"):result.append("实际目标 ≥ %.0f%%"%(float(item.target)*100))
	if def.has("delivery_amount"):result.append("主动交付 %.0f %s"%[float(def.delivery_amount),str({"food":"食物","goods":"制成品","material":"材料"}.get(str(def.get("delivery_resource","")),str(def.get("delivery_resource",""))))])
	if def.has("trade_amount"):
		var side={"buy":"真实进口","sell":"真实出口"}.get(str(def.get("trade_side","")),"真实成交")
		var resource=str(def.get("trade_resource",""));var name=str({"food":"食物","goods":"制成品","material":"材料"}.get(resource,resource))
		result.append(side+" %.0f 单位"%float(def.trade_amount)+(name if name!="" else "（各货物合计）"))
	if def.has("person_months"):result.append("累计实际商业服务 %.0f 人·月"%float(def.person_months))
	if def.has("arrival_people"):result.append("接受 %.0f 位移民；下一来生月边界抵达"%float(def.arrival_people))
	if result.is_empty():result.append("支付后即时完成")
	return "；".join(result)

static func _event_cost(def:Dictionary) -> String:
	var result=[]
	if float(def.get("currency",0))>0:result.append("%.0f 通货"%float(def.currency))
	if float(def.get("deposit",0))>0:result.append("%.0f 通货保证金"%float(def.deposit))
	for resource in def.get("resources",{}):result.append("%.0f %s"%[float(def.resources[resource]),str({"food":"食物","goods":"制成品","material":"材料"}.get(str(resource),str(resource)))])
	return " + ".join(result) if not result.is_empty() else "无即时支付"

static func _event_outcome(def:Dictionary) -> String:
	if def.is_empty():return "+1 份委任重铸权"
	var result=[]
	if float(def.get("deposit",0))>0:result.append("退还 %.0f 通货保证金"%float(def.deposit))
	if float(def.get("cash_reward",0))>0:result.append("+%.0f 通货"%float(def.cash_reward))
	if str(def.get("reward_kind",""))!="":result.append("蓝图研究机会" if str(def.reward_kind)=="research" else "方案副本选择")
	if def.has("arrival_people"):result.append("+%.0f 位居民与真实需求"%float(def.arrival_people))
	return "；".join(result) if not result.is_empty() else "按合同完成"

static func _event_trigger(trigger:Dictionary) -> String:
	var names={"milestone":"已达主题阶段","production_slot":"已有生产槽","industrial_labor_min":"工业实际用工 ≥","medical_max":"实际医疗覆盖 ≤","vacancies_min":"空闲住房 ≥","food_min":"实际食物满足 ≥","service_building":"已有服务建筑","entertainment_max":"实际休闲覆盖 ≤","commercial_people_min":"实际商业服务人数 ≥","freight":"已有货运建筑","commercial_min":"实际商业覆盖 ≥","has_trade":"已有实际成交","storage_ratio_min":"仓容使用率 ≥","actual_overflow":"实际发生溢出","food_surplus_min":"实际食物有正盈余","food_stock_months_min":"口粮耐久 ≥","research":"已有可研究蓝图"}
	var parts=[]
	for key in trigger:
		var label=str(names.get(str(key),str(key)));var value=trigger[key]
		if value is bool or str(key)=="food_surplus_min":parts.append(label)
		elif str(key) in ["medical_max","food_min","entertainment_max","commercial_min","storage_ratio_min"]:parts.append(label+" %.0f%%"%(float(value)*100))
		else:parts.append(label+" %.0f"%float(value))
	return "；".join(parts) if not parts.is_empty() else "稀有机遇已经出现"

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

static func theme_hud_state(g) -> Dictionary:
	var view=g.Themes.view(g);var id=str(view.get("id",""));var phase=1.0;var next="本阶段条件已满足"
	for goal in view.get("goals",[]):
		var ratio=1.0 if goal.get("met",false) else 0.0
		var value=float(goal.get("value",0));var target=float(goal.get("target",0))
		if not goal.get("met",false) and target>0:
			ratio=target/maxf(target,value) if str(goal.get("comparison","min"))=="max" else maxf(0,value)/target
		phase=minf(phase,clampf(ratio,0,1))
		if next=="本阶段条件已满足" and not goal.get("met",false):next=str(goal.get("label",""))
	if float(view.get("steady_required_seconds",0))>0:
		phase=minf(phase,clampf(float(view.get("steady_seconds",0))/float(view.steady_required_seconds),0,1))
		if next=="本阶段条件已满足" and phase<1:next="持续达标中"
	var completed=bool(view.get("completed",false));var progress=1.0 if completed else (float(view.get("stage",1))-1+phase)/3.0
	var rewards=[]
	for entry in pending(g):
		var source=str(entry.get("source",""));var matches=id!="" and source.begins_with("theme:"+id+":")
		if source.begins_with("theme_event:"):
			for event in g.s.block.get("theme",{}).get("events",[]):
				if str(event.get("theme",""))==id and source=="theme_event:"+str(event.get("id","")):matches=true;break
		if matches:rewards.append(entry)
	var first=rewards[0] if not rewards.is_empty() else {}
	return {"visible":id!="","id":id,"view":view,"progress":progress,"phase_progress":phase,"completed":completed,"next":next,"ready":not rewards.is_empty(),"rewards":rewards,"reward_id":str(first.get("reward_id","")),"reward_kind":str(first.get("kind","copy"))}
