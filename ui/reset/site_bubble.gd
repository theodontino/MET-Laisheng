extends PanelContainer
## Local site preview. It reads the planner; committing remains a separate L3 action.
const W=preload("res://ui/widgets.gd")
const Art=preload("res://ui/map/art.gd")
const P=preload("res://ui/presentation.gd")
var app
var tile:String=""
var base:String=""
var prediction:Dictionary={}
var nodes:Dictionary={}
var signature:String=""
func setup(owner):
	app=owner;name="SitePreviewBubble";mouse_filter=Control.MOUSE_FILTER_STOP
	var style=W.box(W.PANEL,W.ACCENT,16);style.content_margin_left=18;style.content_margin_right=18;style.content_margin_top=14;style.content_margin_bottom=14;add_theme_stylebox_override("panel",style)
	var root=W.vbox(self,10);var head=W.hbox(root,10)
	var thumbnail=TextureRect.new();thumbnail.custom_minimum_size=Vector2(70,52);thumbnail.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;thumbnail.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED;head.add_child(thumbnail);nodes.image=thumbnail
	var title=W.label("",21,W.TEXT);head.add_child(title);nodes.title=title
	var metrics=W.hbox(root,16);nodes.metrics=metrics
	var risk=W.label("",14,W.MUTED);root.add_child(risk);nodes.risk=risk
	var costs=W.label("",13,W.MUTED);root.add_child(costs);nodes.costs=costs
	var action=W.primary(W.button("比较投标 ›",func():
		app.selection=[tile];app.open_tender()))
	action.name="CompareSiteTender";action.set_meta("primary_action",true);root.add_child(action);nodes.action=action
	var details=W.flat(W.button("预测详情 ›",show_details));details.name="SiteDetails";root.add_child(details);nodes.details=details
	hide()
func refresh():
	if app==null:return
	tile=str(app.selection[0]) if not app.selection.is_empty() else str(app.hovered_tile);base=str(app.selected_base)
	var g=app.game
	if not g.s.cells.has(tile) or g.s.cells[tile].building!="" or "tile.water" in g.s.cells[tile].traits:hide();return
	var key=tile+":"+base+":"+app.planner.version(g)
	if key!=signature:
		signature=key;prediction=app.planner.compare(g,base,tile)
		app.map_view.plan_result=prediction;app.map_view.plan_tile=tile
		nodes.title.text=str(g.c.projects[base].name);nodes.image.texture=Art.thumbnail(base)
		W.clear(nodes.metrics)
		var items=[]
		if base=="homestead":
			items=[{"icon":"home","value":float(prediction.get("housing",0)),"unit":"住房"},{"icon":"food","value":float(prediction.get("outputs",{}).get("food",0)),"unit":"粮食 /月"}]
		elif base=="workshop":
			items=[{"icon":"material","value":float(prediction.get("outputs",{}).get("material",0)),"unit":"材料 /月"},{"icon":"goods","value":float(prediction.get("outputs",{}).get("goods",0)),"unit":"制品 /月"}]
		else:
			for kind in ["medical","commercial","education","entertainment"]:
				if float(prediction.get("services",{}).get(kind,0))>0:items.append({"icon":kind,"value":float(prediction.services[kind]),"unit":str(P.KIND[kind])+" 人"})
		for item in items.slice(0,2):
			var column=W.vbox(nodes.metrics,3);column.set_meta("preview_metric",item.icon)
			var row=W.hbox(column,7);var icon=TextureRect.new();icon.texture=Art.icon(item.icon,W.ACCENT);icon.custom_minimum_size=Vector2(26,26);icon.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;row.add_child(icon);row.add_child(W.label("%.1f"%item.value,23,W.TEXT,false));column.add_child(W.label("预计 "+item.unit,13,W.MUTED,false))
		var risks=P.plan_cautions(g,prediction);var state=risk_state(prediction)
		nodes.risk.text="● "+str(state.text);nodes.risk.tooltip_text="\n".join(risks)
		nodes.risk.add_theme_color_override("font_color",W.WARNING if state.attention else W.SUCCESS)
		var def=g.c.projects[base];nodes.costs.text="%.0f 通货 · %.0f 材料 · %.1f CP"%[def.cost,def.material,def.construction_required]
		nodes.action.disabled=not prediction.get("ok",false)
	show();reposition()
func show_details():
	app.open_modal(str(app.game.c.projects[base].name)+" · 选址预测","同一城市快照下，假定建设完成后的下一结算；未购买、未扣款。")
	for line in P.plan_lines(app.game,prediction):app.modal_body.add_child(W.label(line,18,W.TEXT))
	for line in P.plan_cautions(app.game,prediction):app.modal_body.add_child(W.label(line,16,W.MUTED if str(line).begins_with("当前粮食有余量") else W.WARNING))
	app.modal_body.add_child(W.label("位置："+tile+" · "+app.game.c.tags_text(app.game.s.cells[tile].traits),14,W.MUTED))
func _process(_delta):reposition()
func reposition():
	if app==null:return
	if app.mode!="site" or app.desk.is_open() or app.overlay!=null and app.overlay.visible or app.decision_kind!="" or app.screenshot_mode:hide();return
	if tile=="" or not app.game.s.cells.has(tile):hide();return
	var anchor=app.map_view.global_position+app.map_view.tile_screen_anchor(tile)
	var footprint=Rect2(anchor,Vector2.ZERO)
	for vertex in app.map_view.surface_polygon(tile):footprint=footprint.expand(app.map_view.global_position+vertex)
	var dimensions=Vector2(358,maxf(240,get_combined_minimum_size().y));size=dimensions
	var safe=Rect2(app.safe_area.position+Vector2(16,146),app.safe_area.size-Vector2(32,264))
	var candidates=[Vector2(footprint.end.x+16,anchor.y-dimensions.y*.5),Vector2(footprint.position.x-dimensions.x-16,anchor.y-dimensions.y*.5),Vector2(anchor.x-dimensions.x*.5,footprint.end.y+16),Vector2(anchor.x-dimensions.x*.5,footprint.position.y-dimensions.y-16)]
	var selected=candidates[0];var best=1e20
	for candidate in candidates:
		var at=Vector2(clampf(candidate.x,safe.position.x,safe.end.x-dimensions.x),clampf(candidate.y,safe.position.y,safe.end.y-dimensions.y));var rectangle=Rect2(at,dimensions)
		var score=at.distance_to(candidate)
		for obstacle in app.context_obstacles():
			if rectangle.intersects(obstacle):score+=rectangle.intersection(obstacle).get_area()
		if rectangle.intersects(footprint.grow(8)):score+=100000
		if score<best:best=score;selected=at
	position=selected

static func risk_state(result:Dictionary) -> Dictionary:
	var text="暂无明显缺口";var activity:Dictionary=result.get("activity",{});var stats:Dictionary=result.get("stats",{})
	if not result.get("ok",false):text="暂不可建设"
	elif float(activity.get("labor",1))<.95 and float(stats.get("jobs",0))>0:text="缺工"
	elif float(activity.get("energy",1))<.98:text="供电不足"
	elif float(activity.get("resources",1))<.98:text="原料不足"
	elif float(result.get("harmed",0))>.1:text="环境影响"
	elif not result.get("affected",[]).is_empty():text="挤占投入"
	else:
		var lost=0.
		for amount in result.get("losses",{}).values():lost+=float(amount)
		if lost>.01:text="仓储不足"
	return {"text":text,"attention":text!="暂无明显缺口"}
