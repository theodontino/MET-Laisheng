extends Node
## Actual macOS framebuffer and UI callbacks. Core fixtures are explicit here;
## the separate playthrough040 runner covers progression without fixture grants.
const Paths=preload("res://tests/paths.gd")
const Store=preload("res://core/storage.gd")
const Fabric=preload("res://ui/map/fabric040.gd")
var app
var checks=[]
var captures=[]
func check(ok:bool,label:String):
	checks.append({"pass":ok,"name":label});print(("UI040 PASS " if ok else "UI040 FAIL ")+label)
func frames(n:int=8):
	for i in n:await get_tree().process_frame
func image(name:String):
	await frames();await RenderingServer.frame_post_draw
	captures.append({"name":name,"window_pixels":str(get_window().size),"viewport":str(app.get_viewport_rect().size),"scale":DisplayServer.screen_get_scale(),"text_scale":app.W.text_scale})
	check(get_viewport().get_texture().get_image().save_png(Paths.file(name+".png"))==OK,"framebuffer "+name)
func find_button(node:Node,part:String):
	if node is Button and part in node.text and node.is_visible_in_tree():return node
	for child in node.get_children():
		var found=find_button(child,part)
		if found!=null:return found
	return null
func press(node:Node,part:String) -> bool:
	var button=find_button(node,part)
	check(button!=null and not button.disabled,"action available: "+part)
	if button==null or button.disabled:return false
	button.pressed.emit();return true
func bounds(control:Control) -> bool:return app.safe_area.grow(1).encloses(control.get_global_rect())
func find_input(node:Node):
	if node is LineEdit:return node
	for child in node.get_children():
		var found=find_input(child)
		if found!=null:return found
	return null
func start(a):
	app=a;app.test_mode=true;app.ignore_save=true;await frames(12)
	app.game.new_game(7040,1000000.);app.game.select_block(0);app.Dialogs.origin(app);await image("040-01-origin")
	check(find_button(app.modal_body,"接手")!=null,"origin screen has a playable start action")
	press(app.modal_body,"接手");await frames()
	check(app.game.s.phase=="play","origin UI enters current city")
	app.game.s.settings.user_paused=false;app.overlay.hide();app.refresh();app.map_view.focus_community();await image("040-02-city")
	check(app.selected_base=="homestead","initial build selection is a current Tier1")
	check(not app.desk.time_actions.visible,"precise advance buttons are absent from HUD")
	check(bounds(app.desk.time_panel) and bounds(app.desk.resource_panel),"desktop HUD fits viewport")
	var state=Store.encode(app.game.s);var fabric=Fabric.derive(app.game)
	check(fabric==Fabric.derive(app.game) and Store.encode(app.game.s)==state,"urban fabric is deterministic and state/RNG read-only")
	app.select_base("neighborhood");app.on_cell("-1,1",false);app.open_tender();await image("040-03-tender")
	check(app.game.s.draft.offers.size()==3 and app.game.s.draft.offers.all(func(o):return not o.has("bonus_id")),"UI tender contains three operator-only offers")
	var money=float(app.game.s.currency);app.workbench.apply_button.pressed.emit();await frames()
	check(app.game.s.currency<money and app.game.s.draft.is_empty(),"tender UI commits actual cost and construction")
	app.selected_project="";app.refresh();app.desk.open("city040","construction");await image("040-04-construction")
	app.desk.close();var id=app.game.playable_ids()[0]
	app.desk.open("city040","preview:"+id+":"+str(app.game.Development.choices(app.game,id)[0]));await image("040-05-upgrade")
	press(app.desk.page,"开始升级");await frames()
	check(not app.game.s.projects[id].upgrade_job.is_empty(),"upgrade UI starts shared-CP job")
	app.desk.close()
	var reward=app.game.Bonus.draw(app.game,"ui040-fixture")
	app.game.s.block.commissions.pending_reward=reward.reward_id
	app.desk.open("tasks");await image("040-06-commissions")
	var chosen=""
	for card in reward.choices:
		if card.deployable_now:chosen=card.bonus_id;break
	check(chosen!="","reward fixture has an immediately usable choice")
	if chosen!="":
		press(app.desk.body,app.game.c.bonuses[chosen].name);await frames()
		check(app.game.s.block.bonus_wallet.get(chosen,{}).get("charges",0)==1,"reward UI grants one license")
		app.desk.open("city040","permits");await image("040-06b-permits")
		press(app.desk.body,"安装到");await frames()
		check(app.game.s.block.bonus_wallet[chosen].charges==0,"permit UI consumes the deployment charge")
	app.game.create_building("freight","1,1","river",true);app.game.refresh_metrics()
	app.desk.open("city040","market");await image("040-07-market")
	var orders=app.game.s.block.trade_orders.size();press(app.desk.body,"提交订单");await frames()
	check(app.game.s.block.trade_orders.size()==orders+1,"market UI creates an actual order")
	app.game.s.block.commissions.completed=["food_supply","settlement"]
	check(app.game.Commissions.accept(app.game,"industrial_delivery")=="","industrial UI fixture can accept delivery commission")
	app.game.s.projects.municipal.inventory.goods+=8;app.game.refresh_metrics()
	app.desk.open("tasks");var goods=app.game.inventory_total("goods")
	press(app.desk.body,"交付");await frames()
	check(app.game.inventory_total("goods")<goods,"active industrial commission UI deducts real goods")
	app.desk.close();check(app.game.pulse(.2).advanced>.19,"UI fixture records actual operating history");app.refresh()
	app.desk.close();app.desk.open("population");state=Store.encode(app.game.s)
	app.desk.navigate("resource","food");app.desk.navigate("finance");app.desk.back();await frames()
	check(app.desk.kind=="resource" and app.desk.arg=="food","navigation back restores one level")
	app.desk.back();await frames();check(app.desk.kind=="population","second back restores the original topic")
	check(Store.encode(app.game.s)==state,"information navigation is read-only")
	await image("040-08-population")
	app.desk.open("finance");await image("040-09-budget")
	app.desk.open("resource","food");await image("040-10-resource")
	app.show_policy();await frames();await image("040-11-policies")
	check(app.desk.policy_buttons.size()==app.game.c.policies.size(),"all five policy categories remain on one page")
	state=Store.encode(app.game.s);app.desk.choose_policy("tax_trade");await frames(35);await image("040-12-policy-preview")
	check(app.desk.policy_prediction.get("ok",false),"policy preview uses live simulation")
	check(Store.encode(app.game.s)==state,"policy preview does not adopt or charge")
	app.desk.push_section("rules","tax_trade");await image("040-12b-policy-rules")
	app.desk.back();await frames();check(app.desk.subtopic=="preview" and app.desk.pending_policy=="tax_trade","policy rules return to the selected preview")
	app.desk.back();await frames();check(app.desk.subtopic=="","policy preview returns to the complete overview")
	state=Store.encode(app.game.s)
	for route in [["finance","","income","sources"],["finance","","expense","sources"],["finance","","history","cycles"],["finance","","history","transactions"],["resource","food","supply","records"],["resource","food","consumption","records"],["resource","food","storage","records"],["resource","energy","supply","records"],["population","","housing","homes"],["population","","employment","sources"],["population","","needs","medical"],["population","","needs","food"],["time","","advance",""],["time","","rules","pause"]]:
		app.desk.open(route[0],route[1]);app.desk.push_section(route[2],route[3]);await frames(3)
		check(app.desk.body.get_child_count()>0,"topic detail renders: "+route[0]+"/"+route[2]+"/"+route[3])
	check(Store.encode(app.game.s)==state,"all new topic details preserve simulation and RNG")
	app.show_policy();await frames();app.desk.scroll.scroll_vertical=180;await frames();var scroll_before=app.desk.scroll.scroll_vertical
	app.desk.choose_policy("tax_trade");await frames(16);app.desk.back();await frames()
	check(abs(app.desk.scroll.scroll_vertical-scroll_before)<=1,"return restores policy overview scroll position")
	app.desk.close();app.desk.open("time");await image("040-13-time")
	app.desk.close();app.desk.toggle_pause();var hours=app.game.s.local_hours;app.do_pulse(.5)
	check(app.game.s.local_hours==hours,"pause blocks DT advancement")
	app.desk.toggle_pause()
	# A completion fixture checks naming UI only; the real route proves earning it.
	app.game.s.city_goal.completed=true;app.game.Block.grant_naming(app.game);app.refresh()
	app.desk.open("city040","block");await image("040-14-naming")
	check(app.game.s.block.naming_rights,"naming can be deferred without losing its right")
	var input=find_input(app.desk.body);check(input!=null,"naming screen exposes name input")
	if input!=null:
		input.text="河湾试验城";input.text_changed.emit(input.text);await frames();press(app.desk.body,"保存名称");await frames()
		check(app.game.s.block.display_name=="河湾试验城" and not app.game.s.block.naming_rights,"name entry and submit consume the naming right")
	for size in [[1024,640,1.],[640,320,1.5]]:
		app.desk.close();get_window().size=Vector2i(Vector2(size[0],size[1])*DisplayServer.screen_get_scale());app.display_settings.text_scale=size[2];app.apply_display();app.selected_project="";app.refresh();await frames(24)
		await image("040-%dx%d-%d-hud"%[size[0],size[1],100*size[2]])
		check(bounds(app.desk.time_panel),"time HUD fits "+str(size))
		app.desk.open("tasks");await frames(16);await image("040-%dx%d-%d-tasks"%[size[0],size[1],100*size[2]])
		check(bounds(app.desk.close_button),"task return fits "+str(size))
		app.show_policy();await frames(16);await image("040-%dx%d-%d-policies"%[size[0],size[1],100*size[2]])
		check(bounds(app.desk.close_button),"policy overview return fits "+str(size))
		app.desk.choose_policy("tax_trade");await frames(20);await image("040-%dx%d-%d-policy-preview"%[size[0],size[1],100*size[2]])
		check(bounds(app.desk.close_button) and bounds(app.desk.primary),"policy preview controls fit "+str(size))
	var failed=checks.filter(func(x):return not x.pass).size()
	FileAccess.open(Paths.file("ui040.json"),FileAccess.WRITE).store_string(JSON.stringify({"passed":checks.size()-failed,"failed":failed,"checks":checks,"captures":captures,"method":"Native macOS rendered UI, logical viewport sizes, programmatic callbacks and explicit fixtures; not human pointer/touch testing."},"\t"))
	print("UI040 ",checks.size()-failed,"/",checks.size());get_tree().quit(1 if failed else 0)
