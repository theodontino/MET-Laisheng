extends SceneTree
## Fixed 1080p native fixtures: real simulation receipts and real Control input.
## These scripted fixtures are not a human playtest or a natural city playthrough.
const App=preload("res://ui/app.gd")
const Game=preload("res://core/game.gd")
const Store=preload("res://core/storage.gd")
const Paths=preload("res://tests/paths.gd")
const ImpactModel=preload("res://ui/view_models/impact_model.gd")
const MapData=preload("res://ui/view_models/map_data.gd")
const BASE_LAYERS=["normal","food","material","goods","energy","pollution","medical","education","commercial","entertainment","population","employment","unemployed","heat","terrain","operator"]
const RESOURCES=["food","material","goods"]
var app
var checks:Array=[]
var captures:Array=[]
var input_actions:Array=[]
var fixture_ids:Dictionary={}
var comparison_evidence:Array=[]
var layout_evidence:Array=[]
var started_at:float=0.

func _initialize() -> void:call_deferred("run")
func check(ok:bool,label:String,detail:Variant=null) -> void:
	var row={"pass":ok,"name":label}
	if detail!=null:row.detail=detail
	checks.append(row);print(("UI IMPACTS PASS " if ok else "UI IMPACTS FAIL ")+label)
func frames(count:int=5) -> void:
	for i in count:await process_frame
func near(a:float,b:float) -> bool:return absf(a-b)<.000001
func visible_nodes(node:Node,key:String,value:Variant=null) -> Array:
	var found:Array=[]
	if node is Control and node.is_visible_in_tree() and node.has_meta(key):
		if value==null or node.get_meta(key)==value:found.append(node)
	for child in node.get_children():found.append_array(visible_nodes(child,key,value))
	return found
func by_meta(node:Node,key:String,value:Variant=null):
	var nodes=visible_nodes(node,key,value)
	return null if nodes.is_empty() else nodes[0]
func named(node:Node,node_name:String):
	if str(node.name)==node_name:return node
	for child in node.get_children():
		var found=named(child,node_name)
		if found!=null:return found
	return null
func button_text(node:Node,fragment:String):
	if node is Button and node.is_visible_in_tree() and fragment in node.text:return node
	for child in node.get_children():
		var found=button_text(child,fragment)
		if found!=null:return found
	return null
func label_text(node:Node) -> String:
	var result=(str(node.text)+"\n") if node is Label else ""
	for child in node.get_children():
		if not child is Control or child.is_visible_in_tree():result+=label_text(child)
	return result
func on_screen(control:Control) -> bool:
	return control!=null and control.is_visible_in_tree() and root.get_visible_rect().grow(1).encloses(control.get_global_rect())
func visible_clip(control:Control) -> Rect2:
	var result=root.get_visible_rect();var ancestor=control.get_parent()
	while ancestor!=null:
		if ancestor is Control and ancestor.clip_contents:result=result.intersection(ancestor.get_global_rect())
		ancestor=ancestor.get_parent()
	return result
func fully_visible(control:Control) -> bool:
	return on_screen(control) and visible_clip(control).grow(1).encloses(control.get_global_rect())
func containing_scroll(control:Control) -> ScrollContainer:
	var ancestor=control.get_parent()
	while ancestor!=null:
		if ancestor is ScrollContainer:return ancestor
		ancestor=ancestor.get_parent()
	return null
func pointer(at:Vector2,label:String) -> void:
	var move=InputEventMouseMotion.new();move.position=at;root.push_input(move);await frames(2)
	var down=InputEventMouseButton.new();down.position=at;down.button_index=MOUSE_BUTTON_LEFT;down.pressed=true;root.push_input(down);await frames(1)
	var up=InputEventMouseButton.new();up.position=at;up.button_index=MOUSE_BUTTON_LEFT;up.pressed=false;root.push_input(up)
	input_actions.append({"kind":"native_pointer","target":label,"x":at.x,"y":at.y});await frames(5)
func click(control:Control,label:String) -> bool:
	if control!=null:
		var ancestor=control.get_parent()
		while ancestor!=null:
			if ancestor is ScrollContainer:ancestor.ensure_control_visible(control);await frames(3)
			ancestor=ancestor.get_parent()
	var available=control!=null and on_screen(control) and (not control is BaseButton or not control.disabled)
	check(available,"native pointer target available: "+label)
	if not available:return false
	var button=control as BaseButton;var is_button=button!=null;var presses:Array=[0];var observe=func():presses[0]+=1
	if is_button:button.pressed.connect(observe)
	await pointer(control.get_global_rect().get_center(),label)
	if is_button:
		input_actions[-1]["button_pressed_callbacks"]=int(presses[0])
		check(int(presses[0])==1,"native pointer dispatches exactly one Button pressed callback: "+label)
		if is_instance_valid(button) and button.pressed.is_connected(observe):button.pressed.disconnect(observe)
	return true
func key(code:Key,label:String,shift:bool=false,unicode:int=0) -> void:
	var down=InputEventKey.new();down.keycode=code;down.unicode=unicode;down.shift_pressed=shift;down.pressed=true;root.push_input(down);await frames(2)
	var up=InputEventKey.new();up.keycode=code;up.unicode=unicode;up.shift_pressed=shift;up.pressed=false;root.push_input(up)
	input_actions.append({"kind":"native_key","target":label,"shift":shift});await frames(3)
func release_focus() -> void:
	var focus=root.gui_get_focus_owner()
	if focus!=null:focus.release_focus()
func fullscreen(label:String) -> void:
	check(root.size==Vector2i(1920,1080) and root.mode in [Window.MODE_FULLSCREEN,Window.MODE_EXCLUSIVE_FULLSCREEN],"fixed 1920x1080 fullscreen: "+label)
func capture(label:String) -> void:
	await frames();await RenderingServer.frame_post_draw
	var path=Paths.file(label+".png");var picture=root.get_texture().get_image()
	check(picture.get_size()==Vector2i(1920,1080),"1080p framebuffer: "+label)
	var result=picture.save_png(path);check(result==OK,"fresh screenshot: "+label)
	captures.append({"path":path,"width":picture.get_width(),"height":picture.get_height(),"created_at":Time.get_unix_time_from_system(),"ui_scale":app.display_settings.ui_scale,"text_scale":app.display_settings.text_scale,"map_layer":app.map_view.layer,"map_proposal":app.map_view.preview_map_enabled,"fixture":true})
func empty_tile(g) -> String:
	var cells=g.Rules.ids(g.s.cells)
	cells.sort_custom(func(a,b):
		var aa=g.Hex.distance(str(a),"0,0");var bb=g.Hex.distance(str(b),"0,0")
		return aa<bb if aa!=bb else str(a)<str(b))
	for tile in cells:
		if str(g.s.cells[tile].building)=="" and "tile.water" not in g.s.cells[tile].traits:return str(tile)
	return ""
func add_building(g,definition:String) -> String:
	var tile=empty_tile(g)
	return "" if tile=="" else str(g.create_building(definition,tile,"commons",true).id)
func fixture() -> void:
	var g=app.game;g.new_game(5126,1000000.);g.select_block(0)
	check(g.choose_origin(str(g.s.origin_choices[0]))=="","fixture enters play through real origin API")
	g.s.settings.user_paused=true;g.s.settings.sound=false;g.s.migration_paused=true
	fixture_ids.residence=add_building(g,"residence")
	check(str(fixture_ids.residence)!="","fixture supplies a real residence")
	if str(fixture_ids.residence)!="":g.Population.create(g,str(fixture_ids.residence),24.)
	for definition in ["medical_clinic","power_station","freight"]:fixture_ids[definition]=add_building(g,definition)
	g.refresh_metrics();g.Population.rehouse(g)
	check(g.Sim.tick(g,float(g.c.balance.tick_hours))=="","fixture settles through the real simulation")
	check(float(g.s.metrics.population)>=41. and float(g.s.metrics.housing)>=float(g.s.metrics.population),"fixture population has real housing and available workers",{"population":g.s.metrics.population,"housing":g.s.metrics.housing,"jobs":g.s.metrics.jobs})
	app.display_settings.ui_scale=1.;app.display_settings.text_scale=1.;app.display_settings.reduced_motion=true
	app.overlay.hide();app.decision_kind="";app.workbench.hide();app.selected_project="";app.selection=[];app.mode="inspect";app.inspector_key="";app.display_pending_tick=-1
	app.apply_display();app.map_view.focus_all();app.command_notice.hide()
func test_cycle_and_blockers() -> void:
	var before=Store.encode(app.game.s);app.set_layer("normal");release_focus()
	check(app.layer_cycle_order()==BASE_LAYERS,"idle layer order contains all sixteen analysis layers without change")
	for index in BASE_LAYERS.size():
		await key(KEY_L,"L next layer")
		check(app.map_view.layer==str(BASE_LAYERS[(index+1)%BASE_LAYERS.size()]),"L advances and wraps in the documented layer order: "+str(index))
	for index in BASE_LAYERS.size():
		await key(KEY_L,"Shift+L previous layer",true)
		check(app.map_view.layer==str(BASE_LAYERS[posmod(-index-1,BASE_LAYERS.size())]),"Shift+L reverses and wraps: "+str(index))
	await click(app.bottom_nodes.growth,"open archive modal")
	var layer=str(app.map_view.layer);await key(KEY_L,"L while archive modal is open")
	check(app.overlay.visible and app.map_view.layer==layer,"modal blocks L")
	await click(button_text(app.modal_body,"财政"),"open finance desk")
	layer=str(app.map_view.layer);await key(KEY_L,"Shift+L while finance desk is open",true)
	check(app.desk.is_open() and app.map_view.layer==layer,"management desk blocks Shift+L")
	await key(KEY_ESCAPE,"Escape close finance desk")
	check(not app.desk.is_open(),"native Escape returns from management desk")
	# A real focused text Control isolates keyboard ownership from modal/desk gates.
	for type_name in ["LineEdit","TextEdit"]:
		var input:Control=LineEdit.new() if type_name=="LineEdit" else TextEdit.new()
		input.name="ImpactKeyboardFixture"+type_name;input.position=Vector2(410,160);input.size=Vector2(280,56);app.ui_root.add_child(input);await frames()
		await click(input,"focus native "+type_name+" fixture");layer=str(app.map_view.layer)
		await key(KEY_L,"L in focused "+type_name,false,108)
		check(root.gui_get_focus_owner()==input and app.map_view.layer==layer and "l" in str(input.get("text")),"focused "+type_name+" consumes L as text")
		input.release_focus();input.queue_free();await frames()
	check(Store.encode(app.game.s)==before,"native layer shortcuts and blockers preserve authoritative state and RNG")
func footprint_screen(tile:String) -> Rect2:
	var vertices=app.map_view.surface_polygon(tile)
	var rect=Rect2(vertices[0]+app.map_view.global_position,Vector2.ZERO)
	for vertex in vertices:rect=rect.expand(vertex+app.map_view.global_position)
	return rect
func start_site() -> String:
	var tile=empty_tile(app.game)
	app.map_view.set_zoom(1.,app.map_view.size*.5);app.map_view.focus_tile(tile);await frames()
	await click(app.category_buttons[2],"choose workshop from native build dock")
	await pointer(app.map_view.point(tile)+app.map_view.global_position,"choose real empty workshop site "+tile)
	check(app.mode=="site" and app.selection==[tile],"map pointer selects the intended real site")
	check(on_screen(app.site_bubble) and not app.site_bubble.get_global_rect().intersects(footprint_screen(tile)),"site bubble fits screen without covering selected map hex")
	check(app.plan_preview_available() and "change" in app.layer_cycle_order(),"valid site forecast adds change to layer cycle")
	return tile
func test_scale(tile:String) -> void:
	var map=app.map_view;var before=Store.encode(app.game.s)
	var map_size=map.size;var map_zoom=float(map.zoom);var map_pan=map.pan;var map_point=map.point(tile);var map_anchor=map.tile_screen_anchor(tile);var hud_size=app.top_panel.get_global_rect().size
	check(str(app.ui_root.name)=="ScaledInterface","independent ScaledInterface root owns UI transforms")
	for factor in [.8,1.,1.2]:
		app.display_options();await frames()
		await click(by_meta(app.modal_body,"ui_scale",factor),"native UI scale %d%%"%roundi(factor*100))
		await key(KEY_ESCAPE,"Escape return from display settings");app.command_notice.hide();await frames()
		fullscreen("UI %d%%"%roundi(factor*100))
		check(near(float(app.display_settings.ui_scale),factor) and app.ui_root.scale.is_equal_approx(Vector2.ONE*factor),"UI scale applies only to interface root: %d%%"%roundi(factor*100))
		check(app.ui_root.size.is_equal_approx(Vector2(1920,1080)/factor) and app.top_panel.get_global_rect().size.is_equal_approx(hud_size*factor),"HUD physical dimensions scale in fixed viewport: %d%%"%roundi(factor*100))
		check(map.size==map_size and near(float(map.zoom),map_zoom) and map.pan.is_equal_approx(map_pan) and map.point(tile).is_equal_approx(map_point) and map.tile_screen_anchor(tile).is_equal_approx(map_anchor),"UI scale preserves map size, world zoom, pan and tile screen coordinates: %d%%"%roundi(factor*100))
		var screen_point=Vector2(731,428);var screen_rect=Rect2(401,203,270,149)
		check(app.ui_position_to_screen(app.ui_position_from_screen(screen_point)).is_equal_approx(screen_point) and app.ui_rect_to_screen(app.ui_rect_from_screen(screen_rect)).is_equal_approx(screen_rect),"screen/UI point and rect helpers round trip: %d%%"%roundi(factor*100))
		for control in [app.top_panel,app.desk.time_panel,app.bottom_panel,app.site_bubble,app.site_bubble.nodes.action]:check(on_screen(control),"scaled context control fits screen: %d%% %s"%[roundi(factor*100),control.name])
		check(not app.site_bubble.get_global_rect().intersects(footprint_screen(tile)),"scaled site bubble avoids actual map hex: %d%%"%roundi(factor*100))
		check(app.plan_preview_available(),"cached site restores valid plan after scale modal: %d%%"%roundi(factor*100))
		if factor!=1.:await capture("impacts-ui-%d-site"%roundi(factor*100))
	app.display_options();await frames();await click(by_meta(app.modal_body,"ui_scale",1.),"restore native UI 100%")
	await key(KEY_ESCAPE,"Escape from restored display settings");app.command_notice.hide()
	check(Store.encode(app.game.s)==before,"UI scale and native settings clicks preserve full gameplay snapshot")
func direct_snapshot(g,base:String="",tile:String="",offer:Dictionary={}) -> Dictionary:
	# Independently settle the production Game/Sim; do not call Planning.copy_game.
	var clone=Game.new();clone.c=g.c;clone.s=g.s.duplicate(true);clone.preview_mode=true;clone.refresh_metrics()
	if base!="":
		var target=str(clone.create_building(base,tile,str(offer.get("operator_org","commons")),true).id)
		if offer.is_empty():clone.s.projects[target].operator_unresolved=true
		clone.refresh_metrics()
	var error=clone.Sim.tick(clone,float(g.c.balance.tick_hours))
	check(error=="","independent real simulation settlement: "+("baseline" if base=="" else "proposal"))
	return {"metrics":clone.s.metrics,"audit":clone.s.audit,"projects":clone.s.projects,"groups":clone.s.groups,"tiles":clone.s.cells,"compiled":clone.compiled,"tick":clone.s.simulation_tick,"error":error}
func monthly_receipt(shot:Dictionary,resource:String) -> Dictionary:
	var hours=float(shot.audit.hours);var receipt:Dictionary=shot.audit.resources[resource]
	var supply=(float(receipt.produced)+float(receipt.get("imported",0)))/hours
	var consumed=float(receipt.consumed)/hours
	var net=(float(receipt.produced)+float(receipt.get("imported",0))-float(receipt.consumed)-float(receipt.get("exported",0))-float(receipt.get("commission_delivered",0))-float(receipt.lost))/hours
	return {"supply":supply,"demand":float(shot.metrics.get("requested",{}).get(resource,consumed)),"consumed":consumed,"net":net}
func raw_field(shot:Dictionary,tile:String,kind:String) -> float:
	var value=0.
	for entry in shot.tiles.get(tile,{}).get("fields",{}).get(kind,[]):value+=float(entry.value)
	return value
func model_matches_real(reference:Dictionary,result:Dictionary,offer:Dictionary) -> void:
	var g=app.game;var actual_before=direct_snapshot(g);var actual_after=direct_snapshot(g,str(g.s.draft.base),str(g.s.draft.tile),offer)
	var after:Dictionary=result.map_snapshot
	check(int(reference.tick)==int(after.tick) and int(after.tick)==int(g.s.simulation_tick)+1,"baseline and proposal are the same real next settlement")
	for field in ["metrics","audit","projects","groups","tiles"]:
		check(reference[field]==actual_before[field],"planner baseline matches independent actual settlement: "+field)
		check(after[field]==actual_after[field],"planner proposal matches independent actual settlement: "+field)
	var model=ImpactModel.build(g,reference,result);check(bool(model.ok),"impact model accepts real same-step planner result")
	check(model.resources.size()==3,"ordinary inventory impact contains food/material/goods only")
	var depletion_seen=false;var unknown_share_seen=false
	for row in model.resources:
		var resource=str(row.id);var a=monthly_receipt(actual_before,resource);var b=monthly_receipt(actual_after,resource)
		var stock=0.;var capacity=0.
		for id in g.s.projects:
			stock+=float(g.s.projects[id].inventory.get(resource,0));capacity+=g.stock_cap_for(str(id),resource)
		check(near(float(row.stock),stock) and near(float(row.capacity),capacity),"inventory/current capacity reads authoritative city: "+resource)
		check(near(float(row.before_supply),float(a.supply)) and near(float(row.before_demand),float(a.demand)) and near(float(row.after_supply),float(b.supply)) and near(float(row.after_demand),float(b.demand)),"before/after supply and demand match actual monthly receipts: "+resource)
		check(near(float(row.before_consumed),float(a.consumed)) and near(float(row.after_consumed),float(b.consumed)) and near(float(row.before_net),float(a.net)) and near(float(row.after_net),float(b.net)) and near(float(row.net_delta),float(b.net)-float(a.net)),"consumed, net flow and net delta match actual receipts: "+resource)
		var expected_before=stock/-float(a.net) if float(a.net)<-.001 else -1.;var expected_after=stock/-float(b.net) if float(b.net)<-.001 else -1.
		check(near(float(row.runway_before),expected_before) and near(float(row.runway_after),expected_after),"stock depletion uses current stock divided by actual net consumption: "+resource)
		depletion_seen=depletion_seen or expected_before>=0 or expected_after>=0
		if float(b.demand)>.001:
			check(bool(row.share_known) and near(float(row.need_share),float(result.outputs.get(resource,0))/float(b.demand)),"building share divides real output by total demand: "+resource)
		else:
			unknown_share_seen=true;check(not bool(row.share_known),"zero demand has no invented 0 percent need share: "+resource)
		check(bool(row.known),"real settled resource receipt is known: "+resource)
	check(depletion_seen,"fixture exercises a real negative net flow and finite stock runway")
	check(unknown_share_seen,"fixture exercises real zero consumption demand")
	var energy:Dictionary=model.energy
	for phase in ["before","after"]:
		var shot=actual_before if phase=="before" else actual_after
		check(near(float(energy[phase+"_supply"]),float(shot.metrics.energy)) and near(float(energy[phase+"_need"]),float(shot.metrics.energy_need)) and near(float(energy[phase+"_ratio"]),float(shot.metrics.energy_ratio)),"electricity uses actual capacity and coverage without inventory/month units: "+phase)
	check(not energy.has("stock") and not energy.has("runway") and not energy.has("need_share"),"energy remains a separate capacity measure")
	var increased=0;var reduced=0;var maximum=0.;var minimum=0.;var harmed=0.;var exposure=0.;var tile_changes:Array=[]
	for tile in g.Rules.ids(g.s.cells):
		var pollution=raw_field(actual_after,str(tile),"pollution")-raw_field(actual_before,str(tile),"pollution")
		var heat=raw_field(actual_after,str(tile),"heat")-raw_field(actual_before,str(tile),"heat")
		if pollution>.00001:increased+=1;maximum=maxf(maximum,pollution)
		elif pollution<-.00001:reduced+=1;minimum=minf(minimum,pollution)
		var delta:Dictionary=app.map_view.preview_deltas.get(tile,{})
		check(not delta.is_empty() and near(float(delta.get("pollution",0)),pollution) and near(float(delta.get("heat",0)),heat),"map raw environment delta equals independently summed real fields: "+str(tile))
		check(not delta.has("score") and not delta.has("mixed_score"),"map delta keeps independent units without a combined score: "+str(tile))
		if absf(pollution)>.00001 or absf(heat)>.00001:tile_changes.append({"tile":tile,"pollution":pollution,"heat":heat})
	for gid in g.s.groups:
		var diff=float(actual_after.groups[gid].living.get("pollution",0))-float(actual_before.groups[gid].living.get("pollution",0))
		exposure+=diff*float(g.s.groups[gid].count)
		if diff>.015:harmed+=float(g.s.groups[gid].count)
	var environment:Dictionary=model.environment
	check(int(environment.total_tiles)==g.s.cells.size(),"environment graphic uses the actual whole-map area as its denominator")
	check(int(environment.increased_tiles)==increased and int(environment.reduced_tiles)==reduced and int(environment.affected_tiles)==increased+reduced and near(float(environment.max_increase),maximum) and near(float(environment.max_reduction),minimum),"environment area and extrema match independent per-cell field sums")
	check(near(float(environment.harmed),harmed) and near(float(environment.exposure_delta),exposure),"environment resident count and exposure delta match actual household receipts")
	comparison_evidence.append({"offer_id":offer.id,"operator_org":offer.operator_org,"baseline_tick":reference.tick,"proposal_tick":after.tick,"model":model,"environment_cells":tile_changes})
func validate_impact_graphs() -> void:
	var panel=named(app.workbench,"TenderImpactPanel")
	check(panel!=null and on_screen(panel),"selected impact panel is visible in native comparison")
	if panel==null:return
	var model:Dictionary=panel.get_meta("impact_model",{})
	check(model==ImpactModel.build(app.game,app.map_view.plan_reference,app.workbench.preview),"selected impact panel consumes authoritative read model")
	check(visible_nodes(panel,"impact_resource").size()==3 and visible_nodes(panel,"impact_graphic","resource_flow").size()==3,"three resource rows each render a real before/after graphic")
	for row in model.get("resources",[]):
		var displayed=by_meta(panel,"impact_resource",str(row.id));check(displayed!=null,"graphical resource row exists: "+str(row.id))
		if displayed==null:continue
		for field in ["stock","capacity","before_supply","before_demand","after_supply","after_demand","before_net","after_net","need_share","runway_before","runway_after"]:check(near(float(displayed.get_meta(field,INF)),float(row[field])),"visible impact metadata matches receipt: "+str(row.id)+" "+field)
		var graph=by_meta(displayed,"impact_graphic","resource_flow")
		var axis=maxf(maxf(float(row.before_supply),float(row.after_supply)),maxf(float(row.before_demand),float(row.after_demand)))
		check(graph!=null and near(float(graph.get_meta("axis_max",-1)),axis) and graph.get_meta("metric_unit","")=="单位/月","before/after resource graph shares one truthful monthly axis: "+str(row.id))
		var share=by_meta(displayed,"impact_need_share",str(row.id))
		if not bool(row.share_known):check(share!=null and not bool(share.get_meta("share_known",true)) and "0%" not in share.text,"visible zero-demand share remains unavailable: "+str(row.id))
	var energy=by_meta(panel,"impact_graphic","energy")
	check(energy!=null and energy.get_meta("metric_unit","")=="供需能力" and not energy.has_meta("stock"),"electricity graphic explicitly uses supply/demand capacity")
	var area_graphs=visible_nodes(panel,"impact_graphic","environment_area")
	check(area_graphs.size()==1 and int(area_graphs[0].get_meta("total_tiles",0))==app.game.s.cells.size(),"environment burden uses a source-backed whole-map area graphic")
func assert_map_source(layer:String,proposal:bool) -> void:
	var map=app.map_view;var expected:Dictionary=map.plan_result.map_snapshot if proposal else map.plan_reference
	check(map.preview_map_enabled==proposal and map.analysis_snapshot()==expected,"analysis map selects same-step "+("proposal" if proposal else "baseline")+": "+layer)
	var matched=true
	for tile in app.game.s.cells:
		if map.view_values.get(tile,{})!=MapData.tile_info(app.game,expected,str(tile),layer):matched=false;break
	check(matched,"rendered map tile values match actual selected snapshot: "+layer)
func tender_controls_fit(label:String) -> void:
	check(fully_visible(app.workbench.title_label),"tender title fits native viewport: "+label)
	check(fully_visible(app.workbench.apply_button) and not app.workbench.apply_button.disabled,"single commit remains visible and enabled without committing: "+label)
	check(fully_visible(named(app.workbench,"TenderDetails")),"details navigation remains visible: "+label)
	for proposal in [false,true]:check(fully_visible(app.workbench.map_buttons[proposal]),"baseline/proposal map button remains visible: "+label+" "+str(proposal))
	check(visible_nodes(app.workbench,"ui_role","primary_action").size()==1,"scaled tender preserves exactly one commit: "+label)
func tender_first_screen(label:String) -> void:
	var panel=named(app.workbench,"TenderImpactPanel")
	check(panel!=null,"selected impact panel exists on first screen: "+label)
	if panel==null:return
	var scroll=containing_scroll(panel)
	check(scroll!=null and scroll.scroll_vertical==0,"first-screen acceptance starts at the real scroll origin: "+label)
	check(fully_visible(panel),"entire selected impact panel fits actual ScrollContainer clip on first screen: "+label,{"panel":str(panel.get_global_rect()),"visible_clip":str(visible_clip(panel))})
	tender_controls_fit(label)
	for offer in app.workbench.buttons:check(fully_visible(app.workbench.buttons[offer]),"operator choice fits first-screen scroll clip: "+label+" "+str(offer))
	var graphical=visible_nodes(panel,"impact_resource")+visible_nodes(panel,"impact_graphic")+visible_nodes(panel,"impact_stock")+visible_nodes(panel,"impact_need_share")+visible_nodes(panel,"impact_energy")+visible_nodes(panel,"impact_environment")
	var geometry:Array=[]
	for control in graphical:
		var kind=str(control.get_meta("impact_resource",control.get_meta("impact_graphic",control.get_meta("impact_stock",control.get_meta("impact_need_share",control.name)))))
		var fits=fully_visible(control)
		check(fits,"resource, energy or environment content fits real first-screen clip: "+label+" "+kind,{"rect":str(control.get_global_rect()),"visible_clip":str(visible_clip(control))})
		geometry.append({"role":kind,"rect":str(control.get_global_rect()),"visible_clip":str(visible_clip(control)),"fully_visible":fits})
	layout_evidence.append({"fixture":label,"ui_scale":app.display_settings.ui_scale,"text_scale":app.display_settings.text_scale,"first_screen":true,"selected_offer":app.workbench.selected_offer,"scroll_rect":str(scroll.get_global_rect()) if scroll!=null else "missing","controls":geometry})
func scroll_until_visible(control:Control,label:String) -> void:
	var scroll=containing_scroll(control)
	check(scroll!=null,"enlarged-text content uses real scroll container: "+label)
	if scroll==null:return
	var ticks=0;var direction=MOUSE_BUTTON_WHEEL_DOWN;var button_indices:Array=[]
	while not fully_visible(control) and ticks<24:
		if control.get_global_rect().position.y<visible_clip(control).position.y:direction=MOUSE_BUTTON_WHEEL_UP
		else:direction=MOUSE_BUTTON_WHEEL_DOWN
		var at=scroll.get_global_rect().get_center();var motion=InputEventMouseMotion.new();motion.position=at;root.push_input(motion)
		var wheel=InputEventMouseButton.new();wheel.position=at;wheel.button_index=direction;wheel.pressed=true;root.push_input(wheel);await frames(1)
		# Viewport captures every pressed mouse-button flag, including wheels.
		# Native injection must release the same wheel button before later clicks.
		var release=InputEventMouseButton.new();release.position=at;release.button_index=direction;release.pressed=false;root.push_input(release);await frames(3)
		button_indices.append(int(direction))
		ticks+=1
	input_actions.append({"kind":"native_wheel","target":label,"ticks":ticks,"pressed_events":ticks,"released_events":ticks,"complete_pairs":ticks,"button_indices":button_indices,"scroll_vertical":scroll.scroll_vertical})
	check(fully_visible(control),"native scrolling exposes complete enlarged-text content: "+label,{"rect":str(control.get_global_rect()),"visible_clip":str(visible_clip(control)),"scroll_vertical":scroll.scroll_vertical})
func tender_large_text(label:String) -> void:
	var panel=named(app.workbench,"TenderImpactPanel")
	check(panel!=null,"enlarged-text impact panel exists: "+label)
	if panel==null:return
	tender_controls_fit(label)
	var offer=app.workbench.selected_offer
	var controls=visible_nodes(panel,"impact_resource")+visible_nodes(panel,"impact_graphic","energy")+visible_nodes(panel,"impact_energy")+visible_nodes(panel,"impact_environment")
	for control in controls:
		var kind=str(control.get_meta("impact_resource",control.get_meta("impact_graphic",control.name)))
		await scroll_until_visible(control,label+" "+kind)
		check(app.workbench.selected_offer==offer and fully_visible(app.workbench.apply_button) and not app.workbench.apply_button.disabled,"scrolling preserves selected offer and usable commit: "+label+" "+kind)
	var chosen=app.workbench.buttons[offer]
	await scroll_until_visible(chosen,label+" selected operator")
	await click(chosen,"reselect unchanged operator at "+label)
	check(app.workbench.selected_offer==offer,"enlarged text keeps the same operator selection usable: "+label)
	tender_controls_fit(label)
	layout_evidence.append({"fixture":label,"ui_scale":app.display_settings.ui_scale,"text_scale":app.display_settings.text_scale,"first_screen":false,"access":"native wheel reveals each resource row and complete energy/environment cards","selected_offer":offer})
func tender_setting(kind:String,factor:float) -> void:
	app.display_options();await frames()
	var choice=by_meta(app.modal_body,"ui_scale",factor) if kind=="ui" else button_text(app.modal_body,"字号 %d%%"%roundi(factor*100))
	await click(choice,"native tender "+kind+" scale %d%%"%roundi(factor*100))
	await key(KEY_ESCAPE,"Escape tender display settings");app.command_notice.hide();await frames()
	var actual=float(app.display_settings.ui_scale if kind=="ui" else app.display_settings.text_scale)
	check(near(actual,factor),"native tender display setting applies: "+kind+" %d%%"%roundi(factor*100))
func test_tender_display(tile:String) -> void:
	var before=Store.encode(app.game.s);var rng=app.game.s.rng.duplicate(true);var revision=int(app.game.revision);var compiled=JSON.stringify(app.game.compiled)
	var offer=str(app.workbench.selected_offer);var map=app.map_view;var map_size=map.size;var zoom=float(map.zoom);var point=map.point(tile);var anchor=map.tile_screen_anchor(tile)
	tender_first_screen("UI 100% / text 100%")
	await capture("impacts-06-tender-first-screen-full-energy-environment")
	for factor in [.8,1.2,1.]:
		await tender_setting("ui",factor);var label="UI %d%% / text 100%%"%roundi(factor*100)
		fullscreen(label);tender_first_screen(label)
		check(map.size==map_size and near(float(map.zoom),zoom) and map.point(tile).is_equal_approx(point) and map.tile_screen_anchor(tile).is_equal_approx(anchor),"tender UI scale preserves unscaled map geometry: "+label)
		check(app.workbench.selected_offer==offer,"native UI setting preserves selected operator: "+label)
	for factor in [1.25,1.5]:
		await tender_setting("text",factor);var label="UI 100%% / text %d%%"%roundi(factor*100)
		fullscreen(label);await tender_large_text(label)
		check(map.size==map_size and near(float(map.zoom),zoom) and map.point(tile).is_equal_approx(point) and map.tile_screen_anchor(tile).is_equal_approx(anchor),"tender font size preserves map geometry: "+label)
	await tender_setting("text",1.);tender_first_screen("restored UI 100% / text 100%")
	check(Store.encode(app.game.s)==before and app.game.s.rng==rng and int(app.game.revision)==revision and JSON.stringify(app.game.compiled)==compiled,"tender scaling, enlarged text and native scrolling preserve state, RNG, compiled rules and revision")
func test_tender(tile:String) -> void:
	await click(app.site_bubble.nodes.action,"open actual operator tender")
	check(app.decision_kind=="tender" and app.workbench.is_visible_in_tree() and app.game.s.draft.get("offers",[]).size()==3,"real tender command generates three persistent operator candidates")
	# Drawing offers legitimately consumes game RNG. Purity starts after opening.
	var before=Store.encode(app.game.s);var rng=app.game.s.rng.duplicate(true);var offers:Array=app.game.s.draft.offers.duplicate(true)
	check(visible_nodes(app.workbench,"operator_offer").size()==3 and app.workbench.buttons.size()==3,"workbench presents three small operator comparison cards")
	check(visible_nodes(app.workbench,"ui_role","primary_action").size()==1 and named(app.workbench,"TenderConfirmPrimary")==app.workbench.apply_button,"comparison keeps one TenderConfirmPrimary commit")
	var rect=app.workbench.get_global_rect();var map_rect=app.map_view.available_rect;map_rect.position+=app.map_view.global_position
	check(on_screen(app.workbench) and rect.position.x>1920*.3 and rect.position.y>1080*.3 and rect.get_area()<1920.*1080.*.45 and not rect.intersects(map_rect),"comparison sits at lower right and leaves a real map analysis area",{"workbench":str(rect),"map_area":str(map_rect)})
	for offer in offers:
		await click(app.workbench.buttons[str(offer.id)],"preview operator "+str(offer.operator_org))
		check(app.workbench.selected_offer==str(offer.id) and app.workbench.preview.get("ok",false),"native candidate selection chooses an actual valid offer: "+str(offer.id))
		check(Store.encode(app.game.s)==before and app.game.s.rng==rng,"candidate preview does not mutate saved city or RNG: "+str(offer.id))
		if app.workbench.preview.get("ok",false):validate_impact_graphs();model_matches_real(app.map_view.plan_reference,app.workbench.preview,offer)
	await test_tender_display(tile)
	app.set_layer("normal");release_focus();await key(KEY_L,"L in tender comparison")
	check(app.map_view.layer=="change" and "change" in app.layer_cycle_order(),"tender workbench permits L with a valid change forecast")
	await key(KEY_L,"Shift+L in tender comparison",true)
	check(app.map_view.layer=="normal","tender workbench permits reverse layer cycle")
	await click(by_meta(app.workbench,"map_proposal",false),"show same-step baseline map")
	app.set_layer("food");await frames();assert_map_source("food",false);await capture("impacts-01-baseline-food")
	await click(by_meta(app.workbench,"map_proposal",true),"show same-step proposal map")
	await frames();assert_map_source("food",true);await capture("impacts-02-proposal-food")
	app.set_layer("energy");await frames();assert_map_source("energy",true);await capture("impacts-03-proposal-energy")
	# A layer CTA must enable the proposal even if baseline was selected first.
	await click(by_meta(app.workbench,"map_proposal",false),"select baseline before pollution CTA")
	await click(by_meta(app.workbench,"impact_layer","pollution"),"selected impact pollution map CTA")
	check(app.map_view.layer=="pollution" and app.map_view.preview_map_enabled and bool(app.workbench.map_buttons[true].get_meta("selected",false)) and not bool(app.workbench.map_buttons[false].get_meta("selected",true)),"pollution CTA switches map, enables forecast and synchronizes map toggle")
	assert_map_source("pollution",true);await capture("impacts-04-proposal-pollution")
	await click(by_meta(app.workbench,"map_proposal",false),"select baseline before change CTA")
	await click(by_meta(app.workbench,"impact_layer","change"),"selected impact change map CTA")
	check(app.map_view.layer=="change" and app.map_view.preview_map_enabled and bool(app.workbench.map_buttons[true].get_meta("selected",false)) and not bool(app.workbench.map_buttons[false].get_meta("selected",true)),"change CTA switches map, enables forecast and synchronizes map toggle")
	assert_map_source("change",true);await capture("impacts-05-proposal-change")
	check(app.map_view.plan_tile==tile and not app.map_view.preview_deltas.is_empty(),"forecast map retains selected site and actual independent deltas")
	check(Store.encode(app.game.s)==before and app.game.s.rng==rng,"map toggle, layer CTAs and analysis rendering preserve exact city and RNG")
	await key(KEY_ESCAPE,"Escape unpaid tender comparison");app.command_notice.hide();await frames()
	check(app.decision_kind=="" and not app.workbench.is_visible_in_tree() and on_screen(app.site_bubble) and app.plan_preview_available(),"returning from unpaid tender restores cached site forecast")
	app.set_layer("normal");release_focus();await key(KEY_L,"L after returning to cached site")
	check(app.map_view.layer=="change","cached site forecast restores change layer cycle")
	await key(KEY_ESCAPE,"Escape leave site preview");await frames()
	check(app.mode=="inspect" and app.map_view.plan_result.is_empty() and app.map_view.plan_reference.is_empty() and app.map_view.preview_deltas.is_empty() and not app.map_view.has_plan_preview(),"leaving planning clears proposal, reference and ghost deltas")
	check("change" not in app.layer_cycle_order() and app.map_view.analysis_snapshot()==app.map_view.display_shot,"exited planning returns analysis to current city snapshot")
	check(Store.encode(app.game.s)==before and app.game.s.rng==rng,"unpaid preview exits preserve offer records, city and RNG")
func finish() -> void:
	var failed=checks.filter(func(row):return not row.pass).size()
	var report={"passed":checks.size()-failed,"failed":failed,"count":checks.size(),"checks":checks,"captures":captures,"input_actions":input_actions,"fixture_ids":fixture_ids,"comparisons":comparison_evidence,"layout_evidence":layout_evidence,"started_at":started_at,"completed_at":Time.get_unix_time_from_system(),"evidence_type":"scripted native pointer and keyboard fixtures with real Game/Simulation settlements; no human playtest, natural playthrough or macOS hardware claim","display_server":DisplayServer.get_name(),"renderer":RenderingServer.get_video_adapter_name(),"engine_version":Engine.get_version_info(),"resolution":"1920x1080 fullscreen throughout; UI and font scale only","source_fingerprint":OS.get_environment("MET_SOURCE_FINGERPRINT")}
	FileAccess.open(Paths.file("ui-impacts.json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("UI IMPACTS RESULT %d passed, %d failed"%[checks.size()-failed,failed]);quit(1 if failed else 0)
func run() -> void:
	started_at=Time.get_unix_time_from_system();app=App.new();app.test_mode=true;app.ignore_save=true;root.add_child(app);app.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);await frames(10)
	app.test_mode=true;app.ignore_save=true;app.feedback_layer.set_process(false)
	check(DisplayServer.get_name()!="headless","native display server is active")
	check(str(Engine.get_version_info().get("string","")).begins_with("4.7.2"),"contracted Godot 4.7.2 engine")
	fullscreen("startup");check(app.size==Vector2(1920,1080) and app.map_view.size==Vector2(1920,1080),"application and unscaled map fill native 1080p viewport")
	fixture();await frames();await test_cycle_and_blockers();var tile=await start_site();await test_scale(tile);await test_tender(tile);fullscreen("completed");finish()
