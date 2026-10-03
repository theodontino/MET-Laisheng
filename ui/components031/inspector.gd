extends VBoxContainer
## L2 contains only this building, three graphical readings and one main verb.
## Every former data/management path lives in the explicitly opened L3 archive.
const W=preload("res://ui/widgets.gd")
const Art=preload("res://ui/map/art.gd")
const Model=preload("res://ui/view_models/inspector_model.gd")
const Graphic=preload("res://ui/reset/metric_graphic.gd")
const Archive=preload("res://ui/reset/building_archive.gd")
var app
var selected_id=""
var model:Dictionary={}
var signature=""
var tab="overview" # retained only for old callers; never creates L2 tabs.
var nodes:Dictionary={}
var title_label:Label
var reading:Label
var status_label:Label
var status_badge:PanelContainer
var default_panel_style:StyleBoxFlat
var portrait:TextureRect
var hero:PanelContainer
var metric_grid:HBoxContainer
var primary:Button
var more:Button
var metrics:Array=[]
var scroll:ScrollContainer
var body:VBoxContainer
func setup(a):
	app=a;name="InspectorCompact"
	default_panel_style=app.inspector_panel.get_theme_stylebox("panel").duplicate()
	default_panel_style.content_margin_left=14;default_panel_style.content_margin_right=14
	default_panel_style.content_margin_top=12;default_panel_style.content_margin_bottom=12
	add_theme_constant_override("separation",10)
	size_flags_horizontal=Control.SIZE_EXPAND_FILL;size_flags_vertical=Control.SIZE_EXPAND_FILL
	var head=W.hbox(self,8)
	title_label=W.label("",22);title_label.size_flags_horizontal=Control.SIZE_EXPAND_FILL;head.add_child(title_label)
	var close=W.flat(W.button("×",func():app.set_mode("inspect")));close.name="CloseBuilding";close.clip_text=false;close.custom_minimum_size=Vector2(44,36);head.add_child(close)
	reading=W.label("",11,W.MUTED);reading.name="SnapshotSource";add_child(reading)
	scroll=ScrollContainer.new();scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;scroll.size_flags_horizontal=Control.SIZE_EXPAND_FILL;add_child(scroll)
	body=W.vbox(scroll,14);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	hero=W.panel(body,W.BASE);hero.name="BuildingHero";hero.custom_minimum_size.y=176
	portrait=Art.picture("homestead",Vector2(180,180));portrait.name="BuildingPortrait";portrait.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;portrait.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED;hero.add_child(portrait)
	status_badge=W.panel(body,W.BASE);status_badge.name="BuildingStatusBadge"
	status_label=W.label("",14,W.GREEN);status_label.name="BuildingStatus";status_label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;status_badge.add_child(status_label)
	metric_grid=HBoxContainer.new();metric_grid.name="MetricGrid";metric_grid.add_theme_constant_override("separation",8);body.add_child(metric_grid)
	primary=W.primary(W.button("管理运营 ›",primary_action));primary.name="PrimaryAction";primary.set_meta("primary_action",true);primary.custom_minimum_size.y=46;add_child(primary)
	more=W.flat(W.button("详情 ›",func():Archive.open(app,selected_id)));more.name="BuildingDetails";more.custom_minimum_size.y=32;more.add_theme_color_override("font_color",W.MUTED);add_child(more)
func update_view(g,shot:Dictionary,id:String):
	if not g.s.projects.has(id):return
	selected_id=id
	model=Model.build(g,shot,id)
	title_label.text=str(model.name)
	reading.text=str(model.reading)+" · "+("库存为当前持有" if model.entity_kind=="logistics" else "本地时间 / 来生月")
	reading.tooltip_text="来源："+str(model.snapshot_id)+" · Tick "+str(model.tick)
	portrait.texture=Art.thumbnail(str(model.base))
	portrait.modulate=Color(1,1,1,.45) if model.entity_kind=="paused" else Color.WHITE
	var ink:Color=model.status_color
	var emphasized=model.entity_kind=="paused" or str(model.status.code) in ["warning","danger"] or (model.entity_kind=="construction" and ink==W.RED)
	var shell=default_panel_style.duplicate()
	if emphasized:
		shell.bg_color=W.PANEL.lerp(ink,.13);shell.border_color=ink.darkened(.2)
	app.inspector_panel.add_theme_stylebox_override("panel",shell)
	hero.add_theme_stylebox_override("panel",W.box(W.BASE,ink.darkened(.4) if model.entity_kind=="paused" or str(model.status.code) in ["warning","danger"] else W.SOFT,12))
	var badge=W.box(W.BASE.lerp(ink,.16),ink.darkened(.45),8)
	badge.content_margin_top=5;badge.content_margin_bottom=5
	status_badge.add_theme_stylebox_override("panel",badge)
	status_label.text=str(model.short_status);status_label.add_theme_color_override("font_color",ink)
	status_badge.tooltip_text="暂停可恢复；固定维护仍按原规则结算。" if model.entity_kind=="paused" else str(model.status.detail)
	var key=str(model.entity_kind)+":"+str(model.compact_metrics.size())+":"+str(W.text_scale)
	if signature!=key:
		W.clear(metric_grid);metrics=[]
		for index in mini(3,model.compact_metrics.size()):
			var graphic=Graphic.new();graphic.name="Metric_"+str(index);metric_grid.add_child(graphic);metrics.append(graphic)
		signature=key
	for index in metrics.size():metrics[index].configure(model.compact_metrics[index])
	primary.text=str(model.primary_action.label);primary.disabled=not model.primary_action.get("enabled",true)
	primary.tooltip_text=str(model.primary_action.label)
	set_meta("entity_kind",str(model.entity_kind))
	set_meta("snapshot_id",str(model.snapshot_id))
	set_meta("graphical_metric_count",metrics.size())
func primary_label() -> String:return str(model.get("primary_action",{}).get("label","管理运营 ›"))
func primary_action():
	if selected_id=="" or not app.game.s.projects.has(selected_id):return
	var action:Dictionary=model.get("primary_action",{})
	match str(action.get("id","manage")):
		"resume":app.act(app.game.set_paused(selected_id,false))
		"upgrade":app.desk.open("city040","upgrade:"+selected_id)
		"market":app.desk.open("city040","market")
		"construction":
			var archive=Archive.open(app,selected_id)
			if archive!=null:archive.manage()
		"services","sources":Archive.open(app,selected_id,"details")
		"shortage":
			match str(action.get("topic","resources")):
				"energy":app.desk.open("resource","energy")
				"workforce":app.desk.open("population")
				_:app.desk.open("resource",str(action.get("resource","material")))
		_:Archive.open(app,selected_id)
func manage():Archive.open(app,selected_id)
func switch_tab(value:String):Archive.open(app,selected_id,value)
