extends SceneTree
## Fixed commands and complete state checkpoints for the native C# port.
const Game=preload("res://core/game.gd")
const Rules=preload("res://core/rules.gd")
const Bonus=preload("res://core/bonus_economy040.gd")
const Themes=preload("res://core/commission_theme.gd")
const Cycles=preload("res://core/cycles.gd")
const Clock=preload("res://core/time_model.gd")
const Store=preload("res://core/storage.gd")
const Development=preload("res://core/development040.gd")
const Market=preload("res://core/market040.gd")
var scenarios:Array=[]
var commands:Array=[]
var fixture_errors:Array=[]

func record(g,command:String,args:Array,result=null) -> void:
	var detached=result.duplicate(true) if result is Dictionary or result is Array else result
	commands.append({"command":command,"args":args.duplicate(true),"result":detached,"state":g.s.duplicate(true)})

func checked_record(g,command:String,args:Array,result) -> void:
	record(g,command,args,result)
	if result is String and result!="":fixture_errors.append(command+": "+result)

func free_tile(g) -> String:
	for key in Rules.ids(g.s.cells):
		if g.s.cells[key].building=="" and "tile.water" not in g.s.cells[key].traits:return key
	return ""

func mechanism_receipt(g,home:String,freight:String) -> Dictionary:
	var bought=0.0;var sold=0.0
	for trade in g.s.block.trade_history:
		if trade.side=="buy":bought+=float(trade.amount)
		else:sold+=float(trade.amount)
	return {"node":g.s.projects[home].node_id,"active_copies":Bonus.active_count(g,"P07","building",home),
		"blueprint_level":Bonus.blueprint_level(g,"P07"),"housing":g.compiled.stats[home].housing,
		"freight_storage":g.compiled.stats[freight]["storage.goods"],"bought":bought,"sold":sold}

func compound_scenario() -> void:
	commands=[]
	var g=Game.new();g.new_game(63066,1000.0);record(g,"new_game",[63066,1000.0])
	checked_record(g,"select_block",[0],g.select_block(0))
	var origin=str(g.s.origin_choices[0]);checked_record(g,"choose_origin",[origin],g.choose_origin(origin))
	# Explicit mechanism-test preconditions, not a naturally earned city:
	# cash/stock plus two ready buildings. RNG, rewards, slots and clocks are untouched.
	var injection={"currency":10000.0,"stock":{"food":60.0,"material":60.0,"goods":20.0},"buildings":[]}
	g.s.currency=injection.currency;g.s.projects.municipal.inventory=injection.stock.duplicate(true)
	var ids=[]
	for row in [["homestead","farmers"],["freight","river"]]:
		var tile=free_tile(g);injection.buildings.append({"definition":row[0],"tile":tile,"operator":row[1]})
		ids.append(str(g.create_building(row[0],tile,row[1],true).id))
	g.refresh_metrics();record(g,"fixture_inject",[injection],ids)
	var home=str(ids[0]);var freight=str(ids[1])
	for index in 2:
		var source="fixture:compound:copy:%d"%index
		var options={"bonus_ids":["P01","P07","L05"]}
		var reward=Bonus.draw(g,source,[],options);record(g,"reward_draw",[source,[],options],reward)
		checked_record(g,"reward",[reward.reward_id,"P07"],Bonus.choose(g,reward.reward_id,"P07"))
	checked_record(g,"bonus_install",["P07","building",home,"Production"],Bonus.install(g,"P07","building",home,"Production"))
	checked_record(g,"upgrade",[home,"agriculture"],Development.start_upgrade(g,home,"agriculture"))
	for seconds in [20.0,60.0]:record(g,"advance",[seconds,"advance"],Clock.advance(g,seconds,"advance"))
	checked_record(g,"bonus_install",["P07","building",home,"Production:2"],Bonus.install(g,"P07","building",home,"Production:2"))
	for level in [1,2]:
		var source="fixture:compound:research:%d"%level;var options={"kind":"research","bonus_ids":["P07"]}
		var reward=Bonus.draw(g,source,[],options);record(g,"reward_draw",[source,[],options],reward)
		checked_record(g,"reward",[reward.reward_id,"P07"],Bonus.choose(g,reward.reward_id,"P07"))
	var options={"bonus_ids":["P01","P07","L05"]}
	var reward=Bonus.draw(g,"fixture:compound:logistics",[],options)
	record(g,"reward_draw",["fixture:compound:logistics",[],options],reward)
	checked_record(g,"reward",[reward.reward_id,"L05"],Bonus.choose(g,reward.reward_id,"L05"))
	var freight_storage_before=float(g.compiled.stats[freight]["storage.goods"])
	checked_record(g,"bonus_install",["L05","building",freight,"Logistics"],Bonus.install(g,"L05","building",freight,"Logistics"))
	checked_record(g,"market_order",["goods","buy",1.0,20.0],Market.order(g,"goods","buy",1.0,20.0))
	checked_record(g,"market_order",["material","sell",1.0,-20.0],Market.order(g,"material","sell",1.0,-20.0))
	for seconds in [30.0,90.0]:record(g,"advance",[seconds,"normal"],Clock.advance(g,seconds,"normal"))
	var receipt=mechanism_receipt(g,home,freight);record(g,"mechanism_receipt",[home,freight],receipt)
	if receipt.node!="agriculture" or receipt.active_copies!=2 or receipt.blueprint_level!=2 or receipt.freight_storage!=freight_storage_before*2.0 or receipt.bought<=0 or receipt.sold<=0:
		fixture_errors.append("compound mechanisms did not execute: "+JSON.stringify(receipt))
	var state=Store.decode(Store.encode(g.s)).state
	var restored=Game.new();var loaded=restored.migrate_state(state)
	record(restored,"reload",[],{"ok":loaded.ok,"migrated":loaded.get("migrated",false)})
	scenarios.append({"id":"injected-compound-bonus-upgrade-logistics-market","test_injection":true,
		"scope":"Mechanism parity only; injected cash, stock and ready buildings, not natural progression acceptance.","commands":commands})

func scenario(seed_value:int,block:int,theme:String) -> void:
	commands=[]
	var g=Game.new();g.new_game(seed_value,1000.0)
	record(g,"new_game",[seed_value,1000.0])
	var result=g.select_block(block);record(g,"select_block",[block],result)
	var origin=str(g.s.origin_choices[0]);result=g.choose_origin(origin);record(g,"choose_origin",[origin],result)
	result=Themes.select(g,theme);record(g,"theme_select",[theme],result)
	for seconds in [.01,1.0,59.0]:
		var advanced=Clock.advance(g,seconds,"normal");record(g,"advance",[seconds,"normal"],advanced)
	var tile=""
	for key in Rules.ids(g.s.cells):
		if g.s.cells[key].building=="" and "tile.water" not in g.s.cells[key].traits:tile=key;break
	result=g.create_tender([tile],"homestead");record(g,"tender",[[tile],"homestead"],result)
	if not g.s.draft.is_empty():
		var offer=str(g.s.draft.offers[0].id)
		result=g.accept_offer(offer);record(g,"accept",[offer],result)
	for seconds in [1.0,30.0,120.0,600.0]:
		var advanced=Clock.advance(g,seconds,"advance");record(g,"advance",[seconds,"advance"],advanced)
	for item in Bonus.pending(g):
		var reward=str(item.reward_id);var card=str(item.choices[0].bonus_id)
		result=Bonus.choose(g,reward,card);record(g,"reward",[reward,card],result)
	Cycles.acknowledge(g);record(g,"acknowledge",[])
	g.s.settings.user_paused=false;g.s.beta_time.pause_reason="";record(g,"unpause",[])
	var advanced=Clock.advance(g,2.0,"normal");record(g,"advance",[2.0,"normal"],advanced)
	var elapsed=g.resume_time(1000.0+86400*90);record(g,"resume",[1000.0+86400*90],elapsed)
	var no_time=Clock.advance(g,10.0);record(g,"advance",[10.0,""],no_time)
	var state=Store.decode(Store.encode(g.s)).state
	var restored=Game.new();var loaded=restored.migrate_state(state)
	record(restored,"reload",[],{"ok":loaded.ok,"migrated":loaded.get("migrated",false)})
	scenarios.append({"id":"block-%d-%s"%[block,theme],"commands":commands})

func _initialize() -> void:
	scenario(7040,0,"industrial")
	scenario(81276,1,"civic")
	scenario(2041,2,"agricultural")
	compound_scenario()
	if not fixture_errors.is_empty():
		for error in fixture_errors:printerr(error)
		quit(2);return
	var path=OS.get_environment("MET_CORE_PARITY_OUTPUT")
	if path=="":path="/tmp/met-unity-core-parity.json"
	var file=FileAccess.open(path,FileAccess.WRITE)
	if file==null:printerr("Cannot write core parity fixture");quit(2);return
	file.store_string(JSON.stringify({"format":1,"source":"Godot current Beta core","scenarios":scenarios},"\t",true,true));file.close()
	print("CORE PARITY EXPORTED ",scenarios.size()," scenarios to ",path)
	quit(0)
