extends RefCounted
const W=preload("res://ui/widgets.gd")
const P=preload("res://ui/presentation.gd")
const Art=preload("res://ui/map/art.gd")
static func node(app,key:String,text:String="",size:int=14,color:Color=W.TEXT) -> Label:
	var l=W.label(text,size,color);app.inspector.add_child(l);app.inspector_nodes[key]=l;return l
static func inspector(app):
	var id=app.selected_project
	var selected=app.issue_index<0 and app.game.s.projects.has(id) and not app.game.s.projects[id].special
	app.inspector031.visible=selected
	app.inspector_scroll.visible=not selected
	if selected:
		app.inspector_panel.show()
		app.inspector031.update_view(app.game,app.shot,id)
		return
	legacy_inspector(app)
static func legacy_inspector(app):
	if app.issue_index>=0:issue(app);return
	var g=app.game;var id=app.selected_project
	var tile=str(app.selection[0]) if not app.selection.is_empty() else app.hovered_tile
	var is_building=g.s.projects.has(id) and not g.s.projects[id].special
	var is_plan=not is_building and app.mode=="site" and g.s.phase=="play"
	app.inspector_panel.visible=is_building or is_plan
	if not app.inspector_panel.visible:return
	var key="building:"+id+":"+JSON.stringify([g.s.projects[id].slots,g.s.projects[id].get("slot_types",{}),g.s.projects[id].status,g.s.projects[id].operator_org]) if is_building else "plan:"+app.selected_base
	if key!=app.inspector_key:
		W.clear(app.inspector);app.inspector_nodes={};app.inspector_key=key;app.inspector_scroll.scroll_vertical=0
		var v=app.inspector
		v.add_child(W.flat(W.icon_button("back","回到地图",func():app.set_mode("inspect"))))
		if is_plan:
			v.add_child(Art.picture(app.selected_base,Vector2(120,100)))
			node(app,"title",g.c.projects[app.selected_base].name,23)
			node(app,"purpose",P.primary(app.selected_base),15,W.MUTED)
			node(app,"place","",14,W.GOLD)
			W.section(v,"建造预览")
			node(app,"outputs","",16,W.GREEN);node(app,"costs","",13,W.MUTED)
			node(app,"risk","",14,W.RED)
			var b=W.primary(W.button("比较这里的投标",func():
				if app.selection.is_empty() and app.hovered_tile!="":app.selection=[app.hovered_tile]
				app.open_tender()))
			v.add_child(b);app.inspector_nodes.cta=b
			v.add_child(W.button("重新选位置",func():app.selection=[];app.inspector_key="";app.refresh()))
			node(app,"assumption","按当前城市条件估计；建成后的产出随投入和运营情况变化。",14,W.MUTED)
		else:
			var p=g.s.projects[id]
			node(app,"title",g.project_name(id),23)
			node(app,"identity",g.c.operators[p.operator_org].name+"  ·  "+g.c.tags_text(p.traits),12,W.MUTED)
			node(app,"reading","",11,W.GOLD)
			node(app,"outputs","",17,W.GREEN)
			node(app,"people","",14,W.TEXT)
			node(app,"inputs","",13,W.MUTED)
			W.section(v,"当前最值得处理")
			node(app,"bottleneck","",14,W.RED);node(app,"utilization","",12,W.MUTED)
			var pr=W.hbox(v,4)
			for pair in [[-1,"让出人手"],[0,"普通"],[1,"优先供人"]]:pr.add_child(W.button(pair[1],func():app.act(g.set_priority(id,pair[0]))))
			W.section(v,"运营方案")
			for slot in g.slot_ids(id):
				var iid=p.slots[slot];var name="空位 · 可部署许可" if iid=="" else g.c.bonuses[g.s.bonus_instances[iid].bonus_id].name
				var b=W.button(g.slot_label(id,slot)+"  /  "+name,func():app.Dialogs.slot_detail(app,id,slot),p.status!="running")
				b.clip_text=true;v.add_child(b)
			node(app,"cohesion","",12,W.MUTED)
			v.add_child(W.button("产出、库存与来源详情",func():app.Dialogs.explain(app,id)))
			v.add_child(W.button("查看建筑成长",func():app.desk.open("city040","upgrade:"+id),p.status!="running"))
			var row=W.hbox(v,6)
			var pause=W.button("",func():app.act(g.set_paused(id,not g.s.projects[id].paused)),g.s.phase!="play");row.add_child(pause);app.inspector_nodes.pause=pause
			row.add_child(W.button("拆除…",func():app.Dialogs.demolition(app,id),g.s.phase!="play"))
			
	if is_plan:
		var valid=g.s.cells.has(tile) and g.s.cells[tile].building=="" and not "tile.water" in g.s.cells[tile].traits
		app.inspector_nodes.cta.disabled=not valid
		var d=g.c.projects[app.selected_base]
		app.inspector_nodes.costs.text="建造 %.0f通货 + %.0f材料\n施工量 %.1f CP；基础维护 %.1f/月"%[d.cost,d.material,d.construction_required,d.stats.maintenance]
		if valid:
			var result=app.planner.compare(g,app.selected_base,tile)
			app.map_view.plan_result=result;app.map_view.plan_tile=tile
			app.inspector_nodes.place.text=tile+"  ·  "+g.c.tags_text(g.s.cells[tile].traits)+( "  · 已选中" if not app.selection.is_empty() else "  · 悬停")
			app.inspector_nodes.outputs.text="\n".join(P.plan_lines(g,result))
			var risks=P.plan_cautions(g,result)
			app.inspector_nodes.risk.text="\n".join(risks) if not risks.is_empty() else "当前未见明显投入短缺。"
			app.inspector_nodes.risk.add_theme_color_override("font_color",W.RED if not risks.is_empty() else W.GREEN)
			if result.get("ok",false):app.inspector_nodes.costs.text+="\n预计新增用电 %+.1f · 收支 %+.1f/月"%[result.energy_demand,result.cashflow]
		else:
			app.map_view.plan_result={};app.map_view.plan_tile=""
			app.inspector_nodes.place.text="将鼠标放到一块空地上"
			app.inspector_nodes.outputs.text="看清当地的预计产出、服务和代价，再选位置。"
			app.inspector_nodes.risk.text=""
		return
	var p=g.s.projects[id];var predicted=app.shot.projects.get(id,p);var m=app.shot.metrics;var st=g.project_stats(id)
	var actual=not app.shot.get("forecast",false) and not g.s.metrics.get("utilization",{}).get(id,{}).is_empty() and p.status=="running" and int(p.get("commissioned_tick",-1))!=int(g.s.simulation_tick)
	var metrics=g.s.metrics if actual else m;var effective=p if actual else predicted
	var a=metrics.get("utilization",{}).get(id,{})
	app.inspector_nodes.reading.text="最近周期实际贡献" if actual else "下一周期预计（无需先快进查看）"
	var lines=[]
	if p.status=="construction":
		lines.append("施工 %.0f%%"%(100*(1-float(p.job.work)/maxf(.001,p.job.total))))
		lines.append("剩余 %.1f CP · %.1f材料"%[p.job.work,p.job.material])
	else:
		for r in ["food","material","goods","energy"]:
			var value=float(effective.outputs_last.get(r,0))
			if value>.001:lines.append("%s %.1f%s"%[P.RES[r],value,"" if r=="energy" else "/月"])
		for kind in g.c.balance.service_kinds:
			var served=0.
			for link in metrics.get("service_links",[]):
				if link.source==id and link.kind==kind:served+=float(link.people)
			if st.get("service."+kind,0)>0 or served>0:lines.append("%s %.1f人 / %.1f容量"%[P.KIND[kind],served,float(st.get("service."+kind,0))*float(st.efficiency)*float(effective.usage)])
		if "building.logistics" in p.traits:
			for r in ["food","material","goods"]:lines.append("%s %.1f / %.0f"%[P.RES[r],p.inventory.get(r,0),g.stock_cap_for(id,r)])
	var gid=g.Population.home_group(g.s,id)
	if gid!="":
		var group=g.s.groups[gid];lines.insert(0,"住着 %.1f人 / %.0f位置"%[group.count,float(st.housing)*float(st.efficiency)])
		app.inspector_nodes.people.text="健康 %.0f · 幸福 %.0f · 教育 %.0f"%[group.health,group.happiness,group.education]
	else:app.inspector_nodes.people.text=""
	app.inspector_nodes.outputs.text="\n".join(lines) if not lines.is_empty() else "没有物资产出；查看下方调度与方案作用。"
	var inputs=[]
	for entry in metrics.get("activity_ledger",{}).get(id,[]):
		if entry.kind=="input" and entry.rate>.001:inputs.append(P.RES.get(entry.resource,entry.resource)+" %.1f/月"%entry.rate)
	app.inspector_nodes.inputs.text="消耗："+("、".join(inputs) if not inputs.is_empty() else "本周期无物资扣除")+"\n固定维护 %.1f/月"%st.maintenance
	var working=0.
	for link in metrics.get("employment_links",[]):
		if link.target==id:working+=float(link.people)
	app.inspector_nodes.bottleneck.text="施工需要材料、工人和时间。" if p.status=="construction" else ("已暂停，仍保养建筑。" if p.paused else str(effective.bottleneck))
	app.inspector_nodes.utilization.text="到岗 %.1f / %.1f · 运行 %.0f%%\n建筑效率 %.0f%% · 供人优先级 %s"%[working,st.jobs,100*float(a.get("utilization",0)),100*st.efficiency,{"-1":"低","0":"普通","1":"高"}.get(str(p.get("work_priority",0)),"普通")]
	var cohesion=g.cohesion(id)
	app.inspector_nodes.cohesion.text="自营 %d/%d · 满配奖励：建筑效率 +3%%（可混搭）"%[cohesion.owned,cohesion.total]
	app.inspector_nodes.pause.text="恢复运行" if p.paused else "暂停运行"

static func issue(app):
	if app.issue_index>=app.alerts.size():app.issue_index=-1;app.inspector_panel.hide();return
	var alert=app.alerts[app.issue_index];var key="issue:"+str(alert.action)+":"+str(alert.target)
	app.inspector_panel.show()
	if app.inspector_key!=key:
		W.clear(app.inspector);app.inspector_key=key;app.inspector_nodes={};var v=app.inspector
		v.add_child(W.flat(W.icon_button("back","回到地图",func():app.set_mode("inspect"))))
		node(app,"issue_title","",20);node(app,"issue_detail","",16,W.MUTED)
		var target=""
		for id in app.game.playable_ids():
			if app.game.s.projects[id].def==alert.action:target=id;break
		if target!="":v.add_child(W.button("先检查现有设施",func():app.issue_index=-1;app.selected_project=target;app.inspector_key="";app.refresh();app.map_view.focus_tile(app.game.s.projects[target].tile)))
		if app.game.c.projects.has(str(alert.action)):v.add_child(W.button("规划新的"+app.game.c.projects[alert.action].name,func():app.select_base(alert.action)))
		v.add_child(W.button("检查人口、需求与目标",func():app.Dialogs.goals(app)))
		v.add_child(W.button("检查收支与物资来源",func():app.Dialogs.resources(app)))
		v.add_child(W.button("调整地区政策",app.policy_categories))
	app.inspector_nodes.issue_title.text=alert.title;app.inspector_nodes.issue_detail.text=alert.detail
