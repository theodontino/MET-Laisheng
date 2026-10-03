extends SceneTree
## Native 1080p contract fixtures. Pointer events traverse the real Control tree.
## Fixture setup is explicit; this is not a natural playthrough or a human test.
const App=preload("res://ui/app.gd")
const Store=preload("res://core/storage.gd")
const Paths=preload("res://tests/paths.gd")
const Art=preload("res://ui/map/art.gd")
const Models=preload("res://ui/city032/models.gd")
const Topics=preload("res://ui/city040/topics.gd")
var app
var checks:Array=[]
var captures:Array=[]
var fixture_ids:Dictionary={}
var input_actions:Array=[]

func _initialize() -> void:call_deferred("run")
func check(ok:bool,label:String,detail:Variant=null) -> void:
	var row={"pass":ok,"name":label}
	if detail!=null:row.detail=detail
	checks.append(row);print(("UI RESET PASS " if ok else "UI RESET FAIL ")+label)
func frames(count:int=5) -> void:
	for i in count:await process_frame
func visible_nodes(node:Node,meta:String,value:Variant=null) -> Array:
	var result:Array=[]
	if node is Control and node.is_visible_in_tree() and node.has_meta(meta):
		if value==null or node.get_meta(meta)==value:result.append(node)
	for child in node.get_children():result.append_array(visible_nodes(child,meta,value))
	return result
func named(node:Node,node_name:String):
	if str(node.name)==node_name:return node
	for child in node.get_children():
		var found=named(child,node_name)
		if found!=null:return found
	return null
func button_named(node:Node,text:String):
	if node is Button and node.is_visible_in_tree() and text in node.text:return node
	for child in node.get_children():
		var found=button_named(child,text)
		if found!=null:return found
	return null
func label_text(node:Node) -> String:
	var result=(str(node.text)+"\n") if node is Label else ""
	for child in node.get_children():
		if not child is Control or child.is_visible_in_tree():result+=label_text(child)
	return result
func visible_error_label(node:Node,fragment:String):
	if node is Label and fragment in node.text and on_screen(node):return node
	for child in node.get_children():
		var found=visible_error_label(child,fragment)
		if found!=null:return found
	return null
func on_screen(control:Control) -> bool:
	return control!=null and control.is_visible_in_tree() and root.get_visible_rect().grow(1).encloses(control.get_global_rect())
func pointer(at:Vector2,label:String="") -> void:
	var move=InputEventMouseMotion.new();move.position=at;root.push_input(move);await frames(2)
	var down=InputEventMouseButton.new();down.button_index=MOUSE_BUTTON_LEFT;down.position=at;down.pressed=true;root.push_input(down)
	await frames(1)
	var up=InputEventMouseButton.new();up.button_index=MOUSE_BUTTON_LEFT;up.position=at;up.pressed=false;root.push_input(up)
	input_actions.append({"kind":"native_pointer","target":label,"x":at.x,"y":at.y});await frames(5)
func click(control:Control,label:String="") -> bool:
	if control!=null:
		var ancestor=control.get_parent()
		while ancestor!=null:
			if ancestor is ScrollContainer:
				ancestor.ensure_control_visible(control);await frames(3)
			ancestor=ancestor.get_parent()
	var ok=control!=null and on_screen(control) and (not control is BaseButton or not control.disabled)
	check(ok,"pointer target available: "+label)
	if not ok:return false
	await pointer(control.get_global_rect().get_center(),label);return true
func click_text(node:Node,text:String) -> bool:return await click(button_named(node,text),text)
func by_meta(node:Node,key:String,value:Variant=null):
	var found=visible_nodes(node,key,value)
	return null if found.is_empty() else found[0]
func set_number(control:SpinBox,value:float,label:String) -> void:
	check(control!=null,"numeric field exists: "+label)
	if control==null:return
	if not await click(control.get_line_edit(),label):return
	var select=InputEventKey.new();select.keycode=KEY_A;select.ctrl_pressed=true;select.pressed=true;root.push_input(select);await frames(1)
	var entered=str(value)
	for index in entered.length():
		var key=InputEventKey.new();key.unicode=entered.unicode_at(index);key.pressed=true;root.push_input(key)
	var enter=InputEventKey.new();enter.keycode=KEY_ENTER;enter.pressed=true;root.push_input(enter);await frames(4)
	input_actions.append({"kind":"native_key","target":label,"entered":str(value)})
	check(is_equal_approx(control.value,value),"numeric field accepts keyboard value: "+label,{"actual":control.value,"expected":value})
func open_archive_page(label:String) -> void:
	await clear_overlay();await click(app.bottom_nodes.growth,"city archive");await click_text(app.modal_body,label)
func page_baseline(label:String) -> void:
	check(on_screen(app.desk.title) and on_screen(app.desk.breadcrumbs),"archive title and breadcrumbs visible: "+label)
	check(on_screen(app.desk.close_button) and on_screen(app.desk.secondary),"archive return controls visible: "+label)
	if app.desk.kind=="policies" and app.desk.subtopic=="":
		var slot_grids={}
		check(app.desk.policy_buttons.size()==app.game.c.policies.size(),"all policies appear as actual comparison cards")
		for id in app.desk.policy_buttons:
			var button=app.desk.policy_buttons[id];var panel=button.get_parent().get_parent();var grid=panel.get_parent();var slot=str(app.game.c.policies[id].slot)
			check(panel is PanelContainer and grid is GridContainer,"policy option uses a visual comparison card: "+str(id))
			if slot_grids.has(slot):check(slot_grids[slot]==grid,"mutually exclusive policies share the same visual slot: "+slot)
			else:slot_grids[slot]=grid
			if str(app.game.s.block.policies.get(slot,""))==str(id):check(panel.get_theme_stylebox("panel").border_color==app.W.GREEN,"current policy is visibly selected in its exclusive slot: "+str(id))
	else:check(visible_nodes(app.desk.body,"desk_visual").size()>0,"archive uses real graphical content: "+label)
	if app.desk.kind in ["finance","resource","population","policies"]:check(str(app.desk.body.get_meta("snapshot_id",""))==str(app.shot.snapshot_id),"archive uses current snapshot identity: "+label)
func metric_matches(label:String,value:float,total:float=-1.) -> void:
	var candidates=visible_nodes(app.desk.body,"metric_label",label)
	check(not candidates.is_empty(),"graphical meter available: "+label)
	if candidates.is_empty():return
	check(is_equal_approx(float(candidates[0].get_meta("metric_value")),value),"graphical meter reads real value: "+label)
	if total>=0:check(is_equal_approx(float(candidates[0].get_meta("metric_total")),total),"graphical meter reads real capacity: "+label)
func quote_extents() -> void:
	var labels=visible_nodes(app.desk.body,"trade_quote")
	check(labels.size()==6,"all six authoritative buy/sell quotes are visible")
	for label in labels:
		var resource=str(label.get_meta("trade_quote_resource"));var side=str(label.get_meta("trade_quote_side"));var quote=app.game.Market.quote(app.game,resource)
		var id=resource+":"+side;var font=label.get_theme_font("font");var font_size=label.get_theme_font_size("font_size")
		var advance=font.get_string_size(label.text,HORIZONTAL_ALIGNMENT_LEFT,-1,font_size).x
		check(label.text=="%.3f"%float(quote[side]),"quote numeric label preserves exact authoritative value: "+id)
		check(label.get_line_count()==1 and label.size.x+1>=advance and label.size.y<=font.get_height(font_size)*1.7,"quote digits fit one readable line: "+id,{"label_size":str(label.size),"text_advance":advance,"lines":label.get_line_count()})
func escape() -> void:
	var event=InputEventKey.new();event.keycode=KEY_ESCAPE;event.pressed=true;root.push_input(event);await frames()
	input_actions.append({"kind":"native_key","target":"Escape"})
func capture(name:String) -> void:
	await frames();await RenderingServer.frame_post_draw
	var path=Paths.file(name+".png");var picture=root.get_texture().get_image()
	check(picture.get_size()==Vector2i(1920,1080),"1080p framebuffer: "+name)
	check(picture.save_png(path)==OK,"capture: "+name)
	captures.append({"path":path,"width":picture.get_width(),"height":picture.get_height(),"fixture":true})
func clear_overlay() -> void:
	if app.desk.is_open():app.desk.close()
	app.overlay.hide();app.decision_kind="";app.workbench.hide();app.selected_project="";app.selection=[];app.mode="inspect";app.inspector_key="";app.refresh();await frames()
func empty_tile() -> String:
	var candidates=app.game.s.cells.keys();candidates.sort_custom(func(a,b):return app.game.Hex.distance(str(a),"0,0")<app.game.Hex.distance(str(b),"0,0"))
	for tile in candidates:
		if str(app.game.s.cells[tile].building)=="" and "tile.water" not in app.game.s.cells[tile].traits:return str(tile)
	return ""
func add_building(def_id:String) -> String:
	var tile=empty_tile()
	return "" if tile=="" else str(app.game.create_building(def_id,tile,"commons",true).id)
func fixture() -> void:
	var g=app.game;g.new_game(5126,1000000);g.select_block(0);g.choose_origin("village");g.s.settings.user_paused=true
	fixture_ids.residence=str(g.playable_ids()[0])
	for pair in [["production","workshop"],["medical","medical_clinic"],["education","primary_school"],["commercial","commercial_center"],["logistics","freight"],["construction","neighborhood"],["paused","agriculture"],["abnormal","power_station"]]:fixture_ids[pair[0]]=add_building(pair[1])
	g.refresh_metrics();g.Population.rehouse(g)
	# Known receipt fixture: nominal production, supplied labor, zero invented links.
	g.s.metrics.activity_ledger={};g.s.metrics.utilization={}
	for id in g.playable_ids():
		g.s.metrics.utilization[id]={"labor":1.0,"energy":1.0,"resources":1.0,"utilization":1.0}
		var ledger:Array=[];var stats=g.project_stats(id)
		for resource in ["food","material","goods","energy"]:
			for kind in ["input","output"]:
				var amount=float(stats.get(kind+"."+resource,0))
				if amount>0:ledger.append({"kind":kind,"resource":resource,"rate":amount,"kept":amount,"lost":0.0,"source":"UI contract receipt fixture","source_id":id})
		g.s.metrics.activity_ledger[id]=ledger
	var construction=g.s.projects[fixture_ids.construction];construction.status="construction";construction.job={"work":2.4,"total":4.8,"material":4.0,"cp":true};construction.slots={};construction.slot_types={}
	g.s.projects[fixture_ids.paused].paused=true
	g.s.metrics.utilization[fixture_ids.abnormal]={"labor":1.0,"energy":1.0,"resources":0.0,"utilization":0.0,"bottleneck":"resources"}
	app.overlay.hide();app.display_pending_tick=-1;app.selected_project="";app.inspector_key="";app.refresh();app.map_view.focus_all()
func select_building(id:String) -> void:
	await clear_overlay();app.map_view.set_zoom(1.1,app.map_view.size*.5);app.map_view.focus_tile(str(app.game.s.projects[id].tile));await frames()
	# Ground anchor can be behind the building, so use the real tile selection API
	# through a map pointer. No Inspector signal is directly emitted.
	var at=app.map_view.point(str(app.game.s.projects[id].tile))+app.map_view.global_position
	await pointer(at,"map building "+id)
	check(app.selected_project==id,"map pointer selects expected entity "+id,{"actual":app.selected_project})
func test_l0() -> void:
	var entries=visible_nodes(app,"l0_item");var names:Array=[]
	for entry in entries:names.append(str(entry.get_meta("l0_item")))
	names.sort();check(names==["dt","food","population","time"],"L0 contains exactly DT, population, food, time",names)
	check(not app.header_nodes.finance.is_visible_in_tree(),"finance is not a permanent L0 status")
	check(not app.desk.resource_panel.is_visible_in_tree(),"material, goods and power dock removed from permanent HUD")
	check(not app.desk.task_panel.is_visible_in_tree(),"no empty theme card before accepting a theme")
	for entry in entries:check(on_screen(entry),"L0 item fits 1080p: "+str(entry.get_meta("l0_item")))
	check(app.desk.mode_buttons.size()==4,"all four time controls remain available")
	for id in app.desk.mode_buttons:check(on_screen(app.desk.mode_buttons[id]),"visible time control "+str(id))
	await capture("reset-01-city-overview")
func test_l2() -> void:
	for kind in fixture_ids:
		await select_building(str(fixture_ids[kind]));var inspector=app.inspector031
		var metrics=visible_nodes(inspector,"graphical_indicator",true);var primary=visible_nodes(inspector,"primary_action",true)
		check(metrics.size()<=3,"L2 has at most three graphical indicators: "+str(kind),metrics.size())
		check(primary.size()==1,"L2 has exactly one primary action: "+str(kind),primary.size())
		check(on_screen(inspector.primary),"fixed primary action fits 1080p: "+str(kind))
		check(not inspector.primary.get_global_rect().intersects(app.desk.time_panel.get_global_rect()),"primary action is clear of time controller: "+str(kind))
		check(inspector.model.get("snapshot_id","")==app.shot.get("snapshot_id",""),"L2 consumes current snapshot identity: "+str(kind))
		check(inspector.model.get("compact_metrics",[]).size()==metrics.size(),"model and visible graphic counts agree: "+str(kind))
		check(named(inspector,"BuildingHero")!=null and on_screen(named(inspector,"BuildingHero")),"building portrait visible: "+str(kind))
		check(named(inspector,"BuildingDetails")!=null and on_screen(named(inspector,"BuildingDetails")),"low weight details reachable: "+str(kind))
		if kind=="residence":
			check(inspector.model.entity_kind=="residential" and inspector.model.compact_metrics.size()==3,"residence has occupancy, livability and population")
			check(inspector.model.compact_metrics[2].value=="%.1f"%float(inspector.model.house.count),"residential number comes from real hosted population")
			var saved_living={}
			for gid in app.game.s.groups:
				if str(app.game.s.groups[gid].host)==str(fixture_ids.residence):saved_living[gid]=app.game.s.groups[gid].get("living",{}).duplicate(true);app.game.s.groups[gid].living={}
			app.refresh();await frames();check(inspector.model.compact_metrics[1].value=="—","missing living receipts show unknown rather than fabricated zero")
			await capture("reset-02-residence-unknown")
			for gid in saved_living:app.game.s.groups[gid].living={"food":0.0,"material":0.0,"energy":0.0,"goods":0.0,"medical":0.0,"education":0.0,"commercial":0.0,"entertainment":0.0}
			app.refresh();await frames();check(inspector.model.compact_metrics[1].value=="0%" and inspector.model.compact_metrics[1].color==app.W.GOLD,"real zero receipts remain a zero warning")
			await capture("reset-02-residence-zero")
			for gid in saved_living:app.game.s.groups[gid].living=saved_living[gid]
			app.refresh();await frames()
		elif kind in ["medical","education","commercial"]:
			check(inspector.model.entity_kind=="service","service uses service-specific context: "+str(kind))
			var served=inspector.model.compact_metrics[0].get("items",[]).map(func(item):return item.id)
			var actual=inspector.model.services.slice(0,3).map(func(item):return item.id)
			check(served==actual and not served.is_empty(),"service graphic reflects actual service kind: "+str(kind),served)
		await capture("reset-02-"+str(kind))
		if kind=="paused":
			check(metrics.is_empty(),"paused context suppresses normal operation metrics")
			check("恢复" in inspector.primary.text,"paused building offers restore")
			await click(inspector.primary,"restore paused building");check(not app.game.s.projects[fixture_ids.paused].paused,"restore action reaches the actual game command")
		elif kind=="abnormal":check("恢复" not in inspector.primary.text,"shortage is not mislabeled as restoration")
		elif kind=="logistics":
			var bars=inspector.model.compact_metrics.filter(func(metric):return metric.kind=="bars")
			check(bars.size()==1 and bars[0].get("items",[]).map(func(item):return item.id)==["food","material","goods"],"freight shows all three actual inventory bars")
			await click(inspector.primary,"freight market");check(app.desk.kind=="city040" and app.desk.arg=="market","freight primary opens real orders and receipts")
			quote_extents()
			await capture("reset-03-market");await clear_overlay()
		elif kind=="construction":
			check("优先" in inspector.primary.text,"construction offers priority adjustment")
			await click(inspector.primary,"construction priority");check(app.desk.is_open() or app.overlay.visible,"construction priority management is reachable")
			await clear_overlay()
func test_details() -> void:
	await select_building(str(fixture_ids.production));var before=Store.encode(app.game.s)
	await click(named(app.inspector031,"BuildingDetails"),"building details")
	check(named(app,"BuildingArchiveView")!=null and on_screen(named(app,"BuildingArchiveView")),"building archive opened through L2 details")
	await capture("reset-04-building-archive")
	check(visible_nodes(named(app,"BuildingArchiveView"),"desk_visual","flow").size()>0,"production archive shows actual input/output relationship")
	await capture("desk-building-overview")
	await click(named(app,"ArchiveTab_details"),"complete building sources")
	var archive=named(app,"BuildingArchiveView")
	check(archive.tab=="details" and "数值解释" in label_text(archive.body) and "所在位置的场来源" in label_text(archive.body),"source, rule and relation detail remains in archive")
	await capture("reset-04b-sources")
	await capture("desk-building-sources")
	var exact=button_named(archive.body,"条明细")
	if exact!=null:
		check(not exact.button_pressed,"exact contribution ledger starts collapsed")
		await click(exact,"expand exact building contribution ledger")
		var active_rows=archive.model.inputs if not archive.model.inputs.is_empty() else archive.model.outputs
		check(exact.button_pressed and not active_rows.is_empty() and JSON.stringify(active_rows[0].children[0],"  ") in label_text(archive.body),"disclosure exposes exact activity receipt from the current snapshot")
		check(visible_nodes(archive.body,"source_record",true).size()>0,"activity disclosure keeps original structured records")
	var rule_ids=app.game.Rules.ids(archive.model.trace)
	if not rule_ids.is_empty():
		var field=str(rule_ids[0]);await click_text(archive.body,field+" =")
		check(button_named(archive.body,field+" =").button_pressed,"exact rule chain can be expanded by pointer")
		check(JSON.stringify(archive.model.trace[field],"  ") in label_text(archive.body),"expanded rule chain preserves complete current compiled trace")
	var field_kinds=app.game.Rules.ids(archive.model.fields)
	if not field_kinds.is_empty():
		await click_text(archive.body,"完整 Field 样本与来源")
		check(JSON.stringify(archive.model.fields[field_kinds[0]][0],"  ") in label_text(archive.body),"expanded Field source preserves source IDs, path and sample values")
	await click(named(app,"ArchiveTab_slots"),"building slots and Bonus")
	check(archive.tab=="slots" and visible_nodes(archive,"slot_id").size()>0,"slot management remains reachable")
	await capture("desk-building-slots")
	check(Store.encode(app.game.s)==before,"opening building archive does not mutate simulation or RNG")
	await click_text(archive,"部署许可");check(app.overlay.visible and "安装资格" in app.modal_subtitle.text,"slot click reaches actual installation detail")
	await click_text(app.modal_body,"查看部署许可");check(app.desk.kind=="city040" and app.desk.arg=="permits","slot detail reaches deployable Bonus inventory")
	await clear_overlay();await select_building(str(fixture_ids.production));await click(named(app.inspector031,"BuildingDetails"),"return to building archive")
	await click(named(app,"ArchiveTab_overview"),"operations and workforce")
	archive=named(app,"BuildingArchiveView");var priority=button_named(archive,"优先供人")
	if priority!=null:archive.scroll.ensure_control_visible(priority);await frames()
	await click(priority,"workforce priority");check(int(app.game.s.projects[fixture_ids.production].get("work_priority",0))==1,"archived workforce priority invokes game command")
	await click(archive.more,"building management")
	for text in ["暂停运行","查看建筑成长","查看部署许可","市场与订单","政策与制度","运营商档案","拆除建筑"]:check(button_named(app.modal_body,text)!=null,"management retains "+text)
	await click_text(app.modal_body,"拆除建筑");check("拆除" in app.modal_title.text and app.game.s.projects.has(fixture_ids.production),"demolition remains a separate confirmation")
	await escape();check(not app.overlay.visible,"Escape exits demolition without deleting building")
	for kind in ["residence","medical","logistics"]:
		await select_building(str(fixture_ids[kind]));var state=Store.encode(app.game.s)
		await click(named(app.inspector031,"BuildingDetails"),"L3 archive for "+kind)
		var view=named(app,"BuildingArchiveView")
		check(view!=null and visible_nodes(view,"desk_visual").size()>1,"different building type has graphical archive: "+kind)
		check(view!=null and str(view.model.snapshot_id)==str(app.shot.snapshot_id),"typed building archive reads exact current snapshot: "+kind)
		check(Store.encode(app.game.s)==state,"typed building archive is read only: "+kind)
		await capture("desk-building-"+kind)
func test_city_archive() -> void:
	for destination in [["财政","finance",""],["人口","population",""],["物资","resource","food"],["建设","city040","development"],["市场","city040","market"],["政策","policies",""],["方案","city040","permits"]]:
		await clear_overlay();await click(app.bottom_nodes.growth,"city archive")
		await click_text(app.modal_body,destination[0]);check(app.desk.kind==destination[1] and app.desk.arg==destination[2],"L3 archive reaches "+destination[0])
		check(on_screen(app.desk.close_button),"L3 return fits 1080p: "+destination[0]);await click(app.desk.close_button,"return from "+destination[0])
	await clear_overlay()
func test_l3_topics() -> void:
	var g=app.game
	await clear_overlay();var before=Store.encode(g.s);var rng=g.s.rng.duplicate(true)
	await click(app.bottom_nodes.growth,"graphical archive index")
	await capture("desk-archive-index")
	await click_text(app.modal_body,"财政");page_baseline("finance")
	var budget=Models.budget(g,app.shot)
	metric_matches("收入",float(budget.income),maxf(float(budget.income),float(budget.expense)))
	metric_matches("支出",float(budget.expense),maxf(float(budget.income),float(budget.expense)))
	await capture("desk-finance-overview")
	await click_text(app.desk.body,"收入构成");check(app.desk.subtopic=="income","budget graph leads to income sources")
	await click_text(app.desk.body,"展开全部来源");check(app.desk.detail_id=="sources","budget source breakdown leads to exact ledger")
	await capture("desk-finance-sources")
	var ledger=button_named(app.desk.body,"完整逐项账本")
	if ledger!=null:
		check(not ledger.button_pressed,"full fiscal ledger starts collapsed")
		await click(ledger,"expand full fiscal ledger");check(ledger.button_pressed,"fiscal ledger is explicitly expandable")
	await escape();check(app.desk.subtopic=="income" and app.desk.detail_id=="","Escape returns budget one level")
	await escape();check(app.desk.subtopic=="","second Escape returns budget overview")
	await click_text(app.desk.body,"历史流水");await frames()
	var charts=[]
	for node in app.desk.body.find_children("*","Control",true,false):
		if node is Topics.FinanceHistoryChart:charts.append(node)
	check(charts.size()==1,"budget history opens an actual historical chart")
	# The stored data is checked through the actual chart object, with no invented past samples.
	for chart in charts:check(chart.samples==g.s.civic032.get("history",[]),"finance chart preserves exact saved historical samples")
	await open_archive_page("物资");page_baseline("resources")
	var material_button=button_named(app.desk.body,"材料");await click(material_button,"material archive tab")
	var material=Models.resource(g,app.shot,"material");var capacity=0.
	for store in material.stores:capacity+=float(store.capacity)
	metric_matches("材料",float(material.stock),capacity)
	check(visible_nodes(app.desk.body,"desk_visual","flow").size()==1,"resource page shows one actual supply/use/stock flow")
	await capture("desk-resource-overview")
	await click_text(app.desk.body,"供给来源");check(app.desk.subtopic=="supply","resource graph leads to supply sources")
	var source_nodes=visible_nodes(app.desk.body,"source_id")
	for source in source_nodes:
		var id=str(source.get_meta("source_id"));var rate=0.
		for row in material.sources:
			if str(row.id)==id:rate+=float(row.rate)
		check(is_equal_approx(float(source.get_meta("metric_value")),absf(rate)),"resource contribution meter matches authoritative receipts: "+id)
	await click_text(app.desk.body,"全部来源与精确记录");check(app.desk.detail_id=="records","resource sources lead to exact receipt page")
	await capture("desk-resource-sources")
	await click_text(app.desk.body,"完整来源记录")
	for row in material.sources:
		check(str(row.get("note","")) in label_text(app.desk.body) and "%.3f"%float(row.rate) in label_text(app.desk.body),"resource disclosure retains exact current source note and rate: "+str(row.id))
	await escape();check(app.desk.subtopic=="supply" and app.desk.detail_id=="","Escape returns resource one level")
	await escape();await click_text(app.desk.body,"电力")
	check(app.desk.arg=="energy" and "库存净变化" not in label_text(app.desk.body),"electricity uses current power supply without a fictional stock flow")
	await capture("desk-resource-energy")
	await open_archive_page("人口");page_baseline("population")
	var population=Models.population(g,app.shot)
	metric_matches("已安置 / 住房",float(population.housed),float(population.housing))
	metric_matches("已就业 / 岗位",float(population.employed),float(population.jobs))
	await capture("desk-population-overview")
	await click_text(app.desk.body,"生活需求");check(app.desk.subtopic=="needs","population cards lead to actual needs matrix")
	var cells=visible_nodes(app.desk.body,"population_group")
	check(not cells.is_empty() and visible_nodes(app.desk.body,"desk_visual","needs_matrix").size()==1,"population has graphical source-backed need matrix")
	for cell in cells:
		var gid=str(cell.get_meta("population_group"));var need=str(cell.get_meta("need_id"));var group:Dictionary=app.shot.groups.get(gid,{})
		var actual=float(app.shot.metrics.get("service_covered",{}).get(gid,{}).get(need,0))/maxf(.000001,float(group.get("count",0))) if need in Models.SERVICES else float(group.get("living",{}).get(need,0))
		if cell.get_meta("coverage_known"):
			check(is_equal_approx(float(cell.get_meta("coverage_ratio")),clampf(actual,0,1)),"need matrix uses exact source ratio: "+gid+":"+need)
		else:
			check(not cell.has_meta("coverage_ratio") and "—" in label_text(cell),"unknown need has no fabricated numeric receipt: "+gid+":"+need)
	await capture("desk-population-needs")
	await escape();check(app.desk.kind=="population" and app.desk.subtopic=="","Escape returns population overview")
	await click_text(app.desk.body,"居住分布");await click_text(app.desk.body,"查看全部住区")
	check(app.desk.subtopic=="housing" and app.desk.detail_id=="homes","housing exposes deeper per-home records")
	await capture("desk-population-housing")
	await open_archive_page("政策");page_baseline("policies")
	await capture("desk-policy-options")
	var candidate=""
	for id in g.Rules.ids(g.c.policies):
		if str(g.s.block.policies.get(str(g.c.policies[id].slot),""))!=str(id):candidate=str(id);break
	check(candidate!="","policy fixture has a real alternative in an exclusive slot")
	if candidate!="":
		await click(app.desk.policy_buttons[candidate],"preview policy "+candidate);await frames(8)
		check(app.desk.pending_policy==candidate and app.desk.subtopic=="preview","pointer selects actual policy candidate")
		check(app.desk.policy_prediction.get("ok",false),"policy preview uses existing shared planner")
		check(on_screen(app.desk.primary) and not app.desk.primary.disabled,"policy confirmation stays in fixed action footer")
		await capture("desk-policy-preview")
		await click_text(app.desk.body,"完整规则与适用范围");check(app.desk.subtopic=="rules" and app.desk.detail_id==candidate,"policy comparison opens exact rules")
		await click_text(app.desk.body,"完整语法与条件")
		check(button_named(app.desk.body,"完整语法与条件").button_pressed,"raw policy syntax remains available through disclosure")
		await capture("desk-policy-rules")
		await escape();check(app.desk.subtopic=="preview" and app.desk.pending_policy==candidate,"Escape returns from policy rules to the same candidate")
	check(Store.encode(g.s)==before and g.s.rng==rng,"L3 resources, population, budget, policy and disclosures preserve exact simulation state and RNG")
	await clear_overlay()
func test_l3_city() -> void:
	var g=app.game;var before=Store.encode(g.s)
	await open_archive_page("建设");page_baseline("development")
	check(visible_nodes(app.desk.body,"desk_visual","building").size()>1,"growth page shows existing buildings and their real tracks")
	await capture("desk-development")
	await click_text(app.desk.body,"选择专业方向");check(app.desk.subtopic=="upgrade","growth card opens real specialization choices")
	await click_text(app.desk.body,"比较升级前后");check(app.desk.subtopic=="preview","choice leads to real before/after capability comparison")
	check(visible_nodes(app.desk.body,"desk_visual","building").size()==2,"upgrade preview shows current and proposed building portraits")
	check(on_screen(app.desk.primary) and "支付并开始升级" in app.desk.primary.text,"upgrade has an explicit fixed payment confirmation")
	await capture("desk-upgrade-preview")
	await escape();check(app.desk.subtopic=="upgrade","Escape returns upgrade preview to specialization choice")
	await escape();await click_text(app.desk.body,"施工队列")
	check(app.desk.arg=="construction" and visible_nodes(app.desk.body,"desk_visual","meter").size()>0,"construction shows actual shared queue progress")
	await capture("desk-construction")
	check(Store.encode(g.s)==before,"growth comparison and shared construction display preserve state")
	await open_archive_page("市场");page_baseline("market")
	quote_extents()
	check(visible_nodes(app.desk.body,"desk_visual","metric").size()>=3,"market begins with real quotes and transport capacity")
	await capture("desk-market")
	await click(by_meta(app.desk.body,"trade_resource","material"),"market material tab")
	await click(by_meta(app.desk.body,"trade_resource","food"),"market food tab")
	await click(by_meta(app.desk.body,"trade_side","buy"),"market buy direction")
	# Temporarily enable the real clock with user pause removed. The opened
	# desk must itself block active intervals and preserve uncommitted edits.
	var saved_pause=bool(g.s.settings.user_paused);var saved_background=app.beta_background
	app.test_mode=false;g.s.settings.user_paused=false;app.beta_background=false;app.reset_beta_clock()
	check(app.desk.blocks_time() and not app.beta_can_run(),"market quantity editing pauses the authoritative city clock")
	await set_number(named(app.desk.body,"TradeAmount"),1.5,"order amount")
	var edit=named(app.desk.body,"TradeAmount").get_line_edit();await click(edit,"uncommitted order amount")
	var select=InputEventKey.new();select.keycode=KEY_A;select.ctrl_pressed=true;select.pressed=true;root.push_input(select);await frames(1)
	for code in [50,46,53]:
		var key=InputEventKey.new();key.unicode=code;key.pressed=true;root.push_input(key)
	var pending_text=str(edit.text);var tick=int(g.s.simulation_tick);await frames(35)
	check(is_instance_valid(edit) and str(edit.text)==pending_text and "2.5" in edit.text and int(g.s.simulation_tick)==tick,"uncommitted keyboard amount survives repeated frames while the desk pauses time")
	app.test_mode=true;g.s.settings.user_paused=saved_pause;app.beta_background=saved_background;app.reset_beta_clock()
	await set_number(named(app.desk.body,"TradeAmount"),1.5,"confirmed order amount")
	var quoted=g.Market.quote(g,"food")
	await set_number(named(app.desk.body,"TradeLimit"),snappedf(float(quoted.buy),.1)+.1,"order limit")
	var count=g.s.block.trade_orders.size();var cash=float(g.s.currency);var review=Store.encode(g.s)
	await click(app.desk.primary,"review real market order")
	check(g.s.block.trade_orders.size()==count and Store.encode(g.s)==review,"review order remains read only before confirmation")
	check(on_screen(app.desk.primary) and app.desk.primary.get_meta("trade_confirm",false),"market payment/order confirmation uses fixed footer")
	await capture("desk-market-order")
	await click(app.desk.primary,"confirm trade order")
	check(g.s.block.trade_orders.size()==count+1,"pointer confirmation creates exactly one actual order")
	var order:Dictionary=g.s.block.trade_orders[-1]
	check(order.resource=="food" and order.side=="buy" and is_equal_approx(float(order.amount),1.5),"market keyboard controls reach actual order resource, direction and quantity")
	check(is_equal_approx(float(g.s.currency),cash),"creating an unfilled order does not fabricate a cash receipt")
	await click(by_meta(app.desk.body,"trade_cancel",str(order.id)),"cancel exact open order")
	check(g.s.block.trade_orders[-1].status=="cancelled" and is_equal_approx(float(g.s.currency),cash),"cancel pointer marks exact order without an unexecuted cash movement")
	# Explicit negative-quote fixture drives the same order commands and market engine.
	# It is not a naturally occurring price-history or city-progression claim.
	var original_market=g.s.block.market_state.duplicate(true)
	g.s.block.market_state.resources.food.base_price=-4.;g.s.block.market_state.resources.food.amplitude=0.;g.s.block.market_state.resources.food.pressure=0.;g.s.block.market_state.resources.food.spread=.4;g.s.block.market_state.resources.food.fee=.1
	for side in ["buy","sell"]:
		app.desk.render(false);await frames();await click(by_meta(app.desk.body,"trade_side",side),"negative-price "+side+" direction")
		var price=float(g.Market.quote(g,"food")[side]);await set_number(named(app.desk.body,"TradeAmount"),1.,"negative-price "+side+" amount")
		await set_number(named(app.desk.body,"TradeLimit"),price,"negative-price "+side+" limit")
		var direction=named(app.desk.body,"TradeCashDirection")
		check(direction!=null and ("收到" in label_text(direction) if side=="buy" else "支付" in label_text(direction)),"negative-price "+side+" explains correct cash direction")
		await click(app.desk.primary,"review negative-price "+side);await click(app.desk.primary,"confirm negative-price "+side)
		var cash_before=float(g.s.currency);g.Market.tick(g,.01,3600.);g.revision+=1;app.refresh();app.desk.render(false);await frames()
		var trade:Dictionary=g.s.block.trade_history[-1]
		check(trade.side==side and float(trade.price)<0,"negative-price "+side+" creates a real execution receipt")
		check((float(g.s.currency)>cash_before if side=="buy" else float(g.s.currency)<cash_before) and is_equal_approx(float(g.s.currency)-cash_before,float(trade.currency_delta)),"negative-price "+side+" cash delta agrees with actual wallet")
		await capture("desk-market-negative-"+side)
		await click_text(app.desk.body,"已结束订单")
		await click(by_meta(app.desk.body,"trade_order",str(g.s.block.trade_orders[-1].id)),"open exact "+side+" order receipt")
		check(app.desk.subtopic=="order" and app.desk.detail_id==str(g.s.block.trade_orders[-1].id),"order opens its exact saved fill and capacity receipts")
		await capture("desk-market-receipt-"+side);await escape()
	g.s.block.market_state=original_market;g.revision+=1;app.refresh();await clear_overlay()
func test_theme_and_alerts() -> void:
	var g=app.game
	await click(app.bottom_nodes.growth,"discover theme through archive");await click_text(app.modal_body,"主题与奖励")
	await click_text(app.desk.body,"选择「工业基地」");check(g.Themes.view(g).id=="industrial","real archive action accepts city theme")
	check(visible_nodes(app.desk.body,"beta_theme_stage").size()>0,"theme archive shows real stage track")
	await capture("desk-theme")
	await clear_overlay();check(on_screen(app.desk.task_panel),"accepted theme has a single visible L1 card")
	check(not app.desk.theme_deadline.is_visible_in_tree(),"untimed theme does not invent a deadline")
	# Explicit near-completion receipt fixture, not a claimed natural milestone.
	g.s.audit={"hours":1.0,"resources":{"goods":{"produced":2.0},"food":{},"material":{}}};g.s.metrics.energy_ratio=1.0
	g.s.block.theme.stage=3;g.s.block.theme.steady_seconds=1.9*app.Beta.MONTH;g.s.block.theme.counters.delivery_material=24.0;g.s.block.theme.counters.delivery_goods=18.0
	for project in g.s.projects.values():project.usage=1.0
	app.refresh();await frames();check(app.desk.theme_progress.value>.9 and app.desk.theme_attention,"over 90 percent progress receives attention state")
	app.display_settings.reduced_motion=true;await frames();check(is_equal_approx(app.desk.theme_progress.modulate.a,1.0),"reduced motion disables progress breathing")
	await capture("reset-05-theme-near-complete")
	g.Bonus.draw(g,"annual:1",[],{"kind":"copy","bonus_ids":["P02"]})
	g.Bonus.draw(g,"theme:industrial:M3",[],{"kind":"copy","bonus_ids":["P01"]});app.refresh();await frames()
	check(on_screen(app.desk.theme_claim),"saved theme reward exposes L1 claim action")
	await capture("reset-05b-theme-reward-ready");await click(app.desk.theme_claim,"theme reward claim")
	var pending=app.Beta.pending(g);check(pending.size()==2,"theme claim preserves an older annual reward in queue")
	check("第1年城市发展" in label_text(app.desk.body),"theme entry exposes the earlier annual reward required by FIFO")
	check("前面还有" in label_text(app.desk.body) and "按序" in label_text(app.desk.body),"theme entry explains why an earlier reward must be resolved")
	await capture("reset-05b1-reward-queue-before-theme")
	await click_text(app.desk.body,"领取")
	check(app.Beta.pending(g).size()==1 and int(g.s.block.bonus_wallet.get("P02",{}).get("charges",0))==1 and int(g.s.block.bonus_wallet.get("P01",{}).get("charges",0))==0,"actual pointer claims the earlier annual reward without bypassing FIFO")
	await capture("reset-05b2-matching-theme-reward")
	await click_text(app.desk.body,"领取");check(app.Beta.pending(g).is_empty() and int(g.s.block.bonus_wallet.get("P01",{}).get("charges",0))==1,"theme reward becomes claimable after the earlier reward")
	g.Bonus.draw(g,"ui-reset:three-comparison",[],{"kind":"copy","bonus_ids":["P01","P02","P03"]});app.desk.render(false);await frames()
	var saved=app.Beta.pending(g)[0].duplicate(true);var state_before=Store.encode(g.s);var rng=g.s.rng.duplicate(true)
	check(saved.choices.size()==3 and visible_nodes(app.desk.body,"beta_reward_candidate").size()==3,"reward archive compares three actual persistent candidates")
	var choice_actions=visible_nodes(app.desk.body,"beta_reward_action")
	check(choice_actions.size()==3,"three saved reward candidates each expose one actual choice action")
	for action in choice_actions:check(on_screen(action) and app.desk.scroll.get_global_rect().grow(1).encloses(action.get_global_rect()),"reward choice action visible on initial comparison page: "+str(action.get_meta("beta_reward_action")),str(action.get_global_rect()))
	await capture("desk-reward-three")
	await click_text(app.desk.body,"完整规则与副本说明")
	check(Store.encode(g.s)==state_before and g.s.rng==rng and app.Beta.pending(g)[0]==saved,"reward disclosure preserves candidates, wallets and RNG")
	await click_text(app.desk.body,"领取");check(app.Beta.pending(g).is_empty(),"comparison claim resolves the actual saved reward")
	await test_l3_install()
	await clear_overlay()
	# Three distinct real receipt deficits exercise capping and overflow, while
	# an unrelated zero inventory without any consumer does not create an alarm.
	for pair in [[fixture_ids.production,"labor"],[fixture_ids.medical,"energy"],[fixture_ids.abnormal,"resources"]]:
		g.s.metrics.utilization[pair[0]]={"labor":1.0,"energy":1.0,"resources":1.0,"utilization":0.0};g.s.metrics.utilization[pair[0]][pair[1]]=0.0
	app.feedback_layer.model.reset();app.feedback_layer.refresh(g);app.refresh();await frames()
	var shown=app.desk.severe_buttons.filter(func(button):return button.is_visible_in_tree())
	check(shown.size()==2 and app.desk.severe_alerts.size()>=3,"L1 caps real severe alarm cards at two")
	check(on_screen(app.desk.alert_overflow) and str(app.desk.severe_alerts.size()-2) in app.desk.alert_overflow.text,"excess severe alarms move into an accurate count")
	await capture("reset-05c-severe-alerts")
	if not shown.is_empty():
		var source:Dictionary=shown[0].get_meta("source",{});await click(shown[0],"locate first severe source")
		check(app.selected_project==str(source.get("building_id","")) and app.map_view.layer==str(source.get("layer","")),"severe alarm pointer locates exact entity and analysis layer")
	await clear_overlay();await click(app.desk.alert_overflow,"all severe alarms")
	check(app.overlay.visible and "待处理问题" in app.modal_title.text,"overflow opens full issue archive")
	await clear_overlay()
	for id in g.playable_ids():g.s.metrics.utilization[id]={"labor":1.0,"energy":1.0,"resources":1.0,"utilization":1.0}
	for p in g.s.projects.values():p.inventory.material=0.0
	g.s.projects[fixture_ids.construction].paused=true
	app.feedback_layer.model.reset();app.feedback_layer.refresh(g);app.refresh();await frames()
	check(app.desk.severe_alerts.is_empty(),"empty material stock without an active deficit does not invent a red alarm")
func test_l3_install() -> void:
	var g=app.game;var host=add_building("manufacturing");g.refresh_metrics();app.refresh()
	check(host!="","installation fixture provides a real compatible manufacturing host")
	await open_archive_page("方案");page_baseline("Bonus wallet")
	check(visible_nodes(app.desk.body,"wallet_bonus_id").size()>=2,"wallet shows actual acquired Bonus copies as visual cards")
	await capture("desk-bonus-wallet")
	var charges=int(g.s.block.bonus_wallet.P01.charges);var instances=g.s.bonus_instances.size();var wallet_before=Store.encode(g.s)
	await click(by_meta(app.desk.body,"wallet_bonus_id","P01"),"choose actual P01 copy")
	check(app.desk.subtopic=="permit" or app.desk.arg=="permit:P01","Bonus card leads to compatible hosts")
	await capture("desk-bonus-hosts")
	var install=null
	for candidate in visible_nodes(app.desk.body,"permit_host",host):
		if candidate is Button and not candidate.disabled:install=candidate;break
	var slot=str(install.get_meta("permit_slot","")) if install!=null else ""
	await click(install,"preview P01 in actual manufacturing slot")
	check(app.overlay.visible and "安装预览" in app.modal_title.text,"host/slot pointer opens explicit install preview")
	await click_text(app.modal_body,"完整规则与安装资格")
	check(Store.encode(g.s)==wallet_before,"copy, host, slot and rule preview do not consume a copy or mutate RNG")
	await capture("desk-bonus-install")
	await click_text(app.modal_body,"确认消耗 1 份并安装")
	check(int(g.s.block.bonus_wallet.P01.charges)==charges-1 and g.s.bonus_instances.size()==instances+1,"installation confirmation consumes exactly one real copy")
	check(slot!="" and str(g.s.projects[host].slots.get(slot,""))!="","actual installation fills the selected stable slot")
	await clear_overlay()
func test_site() -> void:
	await clear_overlay();var g=app.game;var tile=empty_tile();app.map_view.layer="normal"
	app.map_view.set_zoom(1.0,app.map_view.size*.5);app.map_view.focus_tile(tile)
	var before=Store.encode(g.s)
	await click(app.category_buttons[0],"choose homestead starting building");await pointer(app.map_view.point(tile),"empty plot "+tile)
	check(app.mode=="site" and app.selection==[tile],"empty plot pointer starts local placement context")
	check(on_screen(app.site_bubble) and not app.inspector_panel.is_visible_in_tree(),"empty site uses its local bubble and no building inspector")
	var metrics=visible_nodes(app.site_bubble,"preview_metric")
	check(metrics.size()<=2 and metrics.size()>0,"site bubble shows at most two preview quantities")
	check(visible_nodes(app.site_bubble,"primary_action",true).size()==1,"site has one compare-tender primary")
	var footprint=app.map_view.surface_polygon(tile);var footprint_rect=Rect2(footprint[0]+app.map_view.global_position,Vector2.ZERO)
	for vertex in footprint:footprint_rect=footprint_rect.expand(vertex+app.map_view.global_position)
	check(not app.site_bubble.get_global_rect().intersects(footprint_rect),"bubble does not cover the selected tile footprint")
	check("材料" in app.site_bubble.nodes.costs.text and "CP" in app.site_bubble.nodes.costs.text,"site preview preserves real material and construction costs")
	await capture("reset-06-local-site")
	var old_position=app.site_bubble.position;app.map_view.pan+=Vector2(110,35);app.map_view.invalidate_camera();await frames()
	check(app.site_bubble.position.distance_to(old_position)>10 and on_screen(app.site_bubble),"site bubble follows camera while staying on screen")
	await click(app.site_bubble.nodes.details,"site prediction sources");check(app.overlay.visible and "选址预测" in app.modal_title.text,"site prediction opens explicit L3 source explanation")
	check(Store.encode(g.s)==before,"site planning, camera and forecast details preserve state and RNG")
	await escape();app.site_bubble.refresh();await frames();await click(app.site_bubble.nodes.action,"compare operator tenders")
	check(app.decision_kind=="tender" and app.workbench.is_visible_in_tree() and not app.site_bubble.is_visible_in_tree(),"operator comparison opens deliberate L3 workbench")
	check(g.s.draft.get("kind","")=="development" and not g.s.draft.get("offers",[]).is_empty(),"operator candidates are generated by real tender command")
	await capture("reset-06b-operator-comparison");await escape();await clear_overlay()
func test_map_assets() -> void:
	var map=app.map_view;var g=app.game
	for def_id in g.c.projects:
		if not g.playable_ids().any(func(id):return g.s.projects[id].def==def_id):add_building(str(def_id))
	g.refresh_metrics();app.refresh();map.focus_all();await frames()
	var before=Store.encode(g.s);var ids:Array=[];var scales:Array=[];var tier_widths={1:[],2:[],3:[]}
	for id in g.playable_ids():
		var def_id=str(g.s.projects[id].def);var tier=int(g.c.projects[def_id].tier)
		check(Art.sprite(def_id)!=null,"authored building asset retained: "+def_id)
		var visible_rect=map.building_visible_rect(str(id));var ratio=float(visible_rect.size.x)/map.radius()
		# Neighbor center spacing is sqrt(3) radii. The opaque main building must
		# leave a real margin for a shared street/courtyard; exact art tuning is free.
		check(ratio>.45 and ratio<sqrt(3.0)*.88,"main building leaves inter-tile courtyard space: "+def_id,{"tier":tier,"width_per_radius":ratio})
		tier_widths[tier].append(ratio)
		var texture_rect=map.building_screen_rect(str(id))
		check(texture_rect.encloses(visible_rect),"opaque bounds lie within building texture: "+def_id)
		var hitable=false
		for y in range(1,10):
			for x in range(1,10):
				var at=visible_rect.position+visible_rect.size*Vector2(x/10.0,y/10.0)
				if Art.opaque_at(def_id,(at-texture_rect.position)/texture_rect.size) and map.building_hit(at)==str(id):hitable=true;break
			if hitable:break
		check(hitable,"visible building has a real opaque pointer target: "+def_id)
		ids.append(def_id);scales.append({"definition":def_id,"tier":tier,"width_per_radius":ratio})
	check(g.c.projects.keys().all(func(id):return id in ids),"all 21 growth nodes remain represented")
	var averages=[]
	for tier in [1,2,3]:
		var total=0.0
		for width in tier_widths[tier]:total+=float(width)
		averages.append(total/maxi(1,tier_widths[tier].size()))
	check(averages[0]<averages[1] and averages[1]<averages[2],"growth tiers remain visually distinguishable",averages)
	for tile in g.s.cells:
		for symbol in map.terrain_symbols(str(tile)):
			check(str(symbol.get("trait","")) in g.s.cells[tile].traits,"terrain symbol has a real trait source: "+str(tile))
	check(Store.encode(g.s)==before,"asset geometry and terrain symbols are pure presentation")
	await capture("reset-07-all-growth-nodes")
	map.set_zoom(1.45,map.size*.5);map.focus_tile(str(g.s.projects[fixture_ids.production].tile));await capture("reset-07b-neighborhood-connections")
	FileAccess.open(Paths.file("ui-reset-map-scales.json"),FileAccess.WRITE).store_string(JSON.stringify(scales,"\t"))
func test_read_only() -> void:
	await clear_overlay();var before=Store.encode(app.game.s);var rng=app.game.s.rng.duplicate(true)
	for layer in ["normal","terrain","population","food","energy","operator"]:
		app.map_view.layer=layer;app.map_view.invalidate();app.refresh_legend();app.layout_ui();await frames(3)
		if layer!="normal":check(app.legend_panel.is_visible_in_tree() and on_screen(app.legend_panel),"L1 legend visible for "+layer)
	for zoom in [.65,1.0,1.7]:
		app.map_view.set_zoom(zoom,app.map_view.size*.5)
		for tile in app.game.s.cells:
			check(app.map_view.hit(app.map_view.point(str(tile)))==str(tile),"hex hit remains aligned at zoom %.2f: %s"%[zoom,tile])
	app.map_view.layer="normal";app.refresh();app.map_view.focus_all();await frames()
	check(Store.encode(app.game.s)==before,"layers, legend, zoom and hit tests preserve exact authoritative snapshot")
	check(app.game.s.rng==rng,"rendering never consumes gameplay RNG")
func test_time_pointer() -> void:
	await clear_overlay();var g=app.game;g.s.settings.user_paused=true
	await click(app.desk.mode_buttons.burn,"burn time control");check(g.s.beta_time.mode=="burn" and not g.s.settings.user_paused,"pointer selects burn mode")
	var prior=float(g.s.beta_time.active_real_seconds);app.beta_frame_seconds=.37
	await click(app.checkpoint_button,"stop time control")
	check(g.s.beta_time.mode=="normal" and is_equal_approx(float(g.s.beta_time.active_real_seconds)-prior,.37),"stop submits exact pending active interval")
	await click(app.desk.pause_button,"pause time control");check(g.s.settings.user_paused,"pause button reaches actual clock setting")
func test_command_failure() -> void:
	await clear_overlay();var g=app.game
	# Explicit ending fixture isolates the error presentation. No milestone or
	# DT progression is claimed for this synthetic city.
	g.s.dt=0.0;g.Block.on_dt_zero(g);app.refresh();await frames()
	await click(app.bottom_nodes.growth,"archive after budget exhaustion");await click_text(app.modal_body,"城市")
	check(app.desk.kind=="city040" and app.desk.arg=="block","city archive exposes naming command after DT zero")
	var before=Store.encode(g.s);var old_name=str(g.s.block.get("display_name",""))
	var save_button=button_named(app.desk.body,"保存城市名称");await click(save_button,"保存城市名称")
	var error_label=visible_error_label(app,"名称不能为空")
	check(error_label!=null,"a real rejected empty-name command displays its reason on screen")
	check(error_label!=null and not error_label.get_global_rect().intersects(save_button.get_global_rect()),"command error does not cover the rejected primary action")
	check(error_label!=null and not error_label.get_global_rect().intersects(app.top_panel.get_global_rect()) and not error_label.get_global_rect().intersects(app.desk.time_panel.get_global_rect()),"command error does not obscure the four permanent state controls")
	check(str(g.s.block.get("display_name",""))==old_name and bool(g.s.block.get("naming_rights",false)),"failed name command preserves city name and naming right")
	check(Store.encode(g.s)==before,"rejected UI command leaves authoritative state unchanged")
	await capture("reset-08-command-error-feedback")
func finish() -> void:
	var failures=checks.filter(func(row):return not row.pass).size()
	var report={"passed":checks.size()-failures,"failed":failures,"checks":checks,"captures":captures,"input_actions":input_actions,"fixture_ids":fixture_ids,"evidence_type":"scripted native pointer fixtures; no human or natural playthrough claim","display_server":DisplayServer.get_name(),"renderer":RenderingServer.get_video_adapter_name(),"resolution":"1920x1080","source_fingerprint":OS.get_environment("MET_SOURCE_FINGERPRINT")}
	FileAccess.open(Paths.file("ui-reset.json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("UI RESET RESULT %d passed, %d failed"%[checks.size()-failures,failures]);quit(1 if failures else 0)
func run() -> void:
	app=App.new();app.test_mode=true;app.ignore_save=true;root.add_child(app);app.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);await frames(10)
	app.ignore_save=true;app.test_mode=true;app.feedback_layer.set_process(false)
	check(DisplayServer.get_name()!="headless","native display server is active")
	check(root.size==Vector2i(1920,1080),"only contracted 1920x1080 desktop resolution")
	check(app.size==Vector2(1920,1080) and app.map_view.size==Vector2(1920,1080),"actual app and map fill the native viewport")
	check(root.mode in [Window.MODE_FULLSCREEN,Window.MODE_EXCLUSIVE_FULLSCREEN],"desktop starts fullscreen")
	fixture();await frames();await test_l0();await test_l2();await test_details();await test_city_archive();await test_l3_topics();await test_l3_city();await test_theme_and_alerts();await test_site();await test_read_only();await test_time_pointer();await test_map_assets();await test_command_failure();finish()
