extends RefCounted
## Native 0.4 core acceptance. Fixtures are deliberately direct core fixtures: this
## suite proves state-machine and conservation contracts, not UI or human playthrough.

const Game=preload("res://core/game.gd")
const Store=preload("res://core/storage.gd")

var checks:Array=[]

func check(ok:bool,name:String,detail:String="") -> void:
	checks.append({"pass":ok,"name":name,"detail":detail})
	print(("040 PASS " if ok else "040 FAIL ")+name+("" if detail=="" else " :: "+detail))

func near(a:float,b:float,epsilon:float=.00001) -> bool:
	return absf(a-b)<=epsilon

func fresh(seed_value:int=7040):
	var g=Game.new()
	g.new_game(seed_value,1000000.)
	g.select_block(0)
	g.choose_origin(g.s.origin_choices[0])
	g.Block.ensure(g)
	return g

func clone_game(g):
	var twin=Game.new()
	var decoded=Store.decode(Store.encode(g.s))
	if decoded.ok:
		var migrated=twin.migrate_state(decoded.state)
		if migrated.ok:return twin
	return null

func playable(g) -> Array:
	var out=[]
	for id in g.playable_ids():
		if not g.s.projects[id].get("special",false):out.append(id)
	out.sort()
	return out

func choice_id(choice) -> String:
	if choice is String:return choice
	if choice is Dictionary:
		for key in ["node_id","id","target"]:
			if choice.has(key):return str(choice[key])
	return ""

func first_upgrade(g,id:String) -> String:
	var choices=g.Development.choices(g,id)
	if not choices is Array or choices.is_empty():return ""
	return choice_id(choices[0])

func all_material_zero(g) -> void:
	for p in g.s.projects.values():
		if p.get("inventory") is Dictionary:p.inventory["material"]=0.

func add_material(g,amount:float) -> void:
	if g.s.projects.has("municipal"):
		g.s.projects.municipal.inventory.material=amount
	else:
		for p in g.s.projects.values():
			if p.get("inventory") is Dictionary:
				p.inventory.material=amount
				return

func material_total(g) -> float:
	var total=0.
	for p in g.s.projects.values():
		if p.get("inventory") is Dictionary:total+=float(p.inventory.get("material",0.))
	return total

func inventory_snapshot(g) -> Dictionary:
	var result={}
	for id in g.s.projects:result[id]=g.s.projects[id].inventory.duplicate(true)
	return result

func queued_work(g) -> float:
	var total=0.
	for row in g.Construction.queue(g):total+=float(row.work)
	return total

func find_reward(value,c):
	if value is Dictionary:
		for key in ["choices","bonus_ids","options","cards"]:
			var candidate=value.get(key)
			if candidate is Array and candidate.size()==3:
				var valid=true
				for item in candidate:
					var bid=str(item.get("bonus_id",item.get("id",""))) if item is Dictionary else str(item)
					if not c.bonuses.has(bid):valid=false
				if valid:return value
		for child in value.values():
			var found=find_reward(child,c)
			if not found.is_empty():return found
	elif value is Array:
		for child in value:
			var found=find_reward(child,c)
			if not found.is_empty():return found
	return {}

func reward_ids(reward:Dictionary) -> Array:
	for key in ["choices","bonus_ids","options","cards"]:
		if reward.get(key) is Array:
			var out=[]
			for item in reward[key]:out.append(str(item.get("bonus_id",item.get("id",""))) if item is Dictionary else str(item))
			return out
	return []

func reward_id(reward:Dictionary) -> String:
	return str(reward.get("reward_id",reward.get("id","")))

func wallet(g) -> Dictionary:
	if g.s.get("block") is Dictionary and g.s.block.get("bonus_wallet") is Dictionary:return g.s.block.bonus_wallet
	if g.s.get("bonus_wallet") is Dictionary:return g.s.bonus_wallet
	return {}

func charges(entry) -> int:
	return int(entry.get("charges",0)) if entry is Dictionary else int(entry)

func empty_tile(g) -> String:
	var keys=g.s.cells.keys();keys.sort()
	for key in keys:
		var cell=g.s.cells[key]
		if cell.get("building","")=="" and not "tile.water" in cell.get("traits",[]):return key
	return ""

func trade_project_def(g) -> String:
	for id in g.c.projects:
		var def=g.c.projects[id]
		var stats=def.get("stats",{})
		if float(stats.get("trade_capacity",def.get("trade_capacity",0.)))>0:return str(id)
	return ""

func market_price(quote:Dictionary,side:String) -> float:
	return float(quote.get(side+"_price",quote.get(side,0.)))

func set_market_pressure(g,resource:String,value:float) -> bool:
	var market=g.s.block.get("market_state",{})
	var state=market.get("resources",{}) if market is Dictionary else {}
	if not state is Dictionary or not state.has(resource):return false
	if state[resource] is Dictionary:state[resource].pressure=value
	else:state[resource]={"pressure":value}
	return true

func installed_license_fixture(g) -> Dictionary:
	var metadata=g.c.v040.get("bonus_economy",{}).get("metadata",{})
	for bid in metadata:
		var meta=metadata[bid];var scope=str(meta.get("scope","building"));var hosts=[]
		if scope=="building":
			for project_id in playable(g):
				for slot in g.s.projects[project_id].get("slots",{}):hosts.append({"type":"building","id":project_id,"slot":slot})
		elif scope=="organization":hosts.append({"type":"organization","id":"commons","slot":""})
		elif scope=="block":hosts.append({"type":"block","id":g.s.block.id,"slot":""})
		for host in hosts:
			var trial=clone_game(g)
			if trial==null:continue
			if trial.Bonus.grant(trial,str(bid),"acceptance040-install-"+str(bid))!="":continue
			var err=trial.Bonus.install(trial,str(bid),host.type,host.id,host.slot)
			if err=="":return {"game":trial,"bonus":str(bid),"host":host}
	return {}

func test_schema_and_preview() -> void:
	var g=fresh()
	check(int(g.s.get("schema",0))==5,"new game uses schema5")
	check(g.validate_state(g.s)=="","new schema5 game validates",g.validate_state(g.s))
	var candidate="";var node=""
	for id in playable(g):
		node=first_upgrade(g,id)
		if node!="":candidate=id;break
	check(candidate!="","opening contains an upgradeable Tier1 project")
	if candidate=="":return
	var before=Store.encode(g.s)
	var preview=g.Development.preview(g,candidate,node)
	check(preview is Dictionary and preview.get("error","")=="","upgrade preview returns a usable model")
	var has_cost=preview.has("currency") or preview.has("cost") or preview.has("resources")
	var has_cp=preview.has("construction_required") or preview.has("construction") or preview.has("cp")
	check(has_cost and has_cp,"upgrade preview exposes price/resources and construction points")
	check(Store.encode(g.s)==before,"upgrade preview changes neither state nor RNG")
	var yard=g.create_building("yard","2,0","river",true)
	var specialization=g.Development.preview(g,yard.id,"manufacturing")
	check(specialization.losses.any(func(row):return row.stat=="construction_capacity" and near(row.before,2.) and near(row.after,0.)),"specialization preview includes abilities absent from the new node")

func test_license_and_stable_reward() -> void:
	var g=fresh(7041)
	var before=Store.encode(g.s)
	var reward=g.Bonus.draw(g,"acceptance040-stable",[])
	check(reward is Dictionary and not reward.is_empty(),"bonus reward draft can be created")
	if not reward is Dictionary or reward.is_empty():reward=find_reward(g.s,g.c)
	var ids=reward_ids(reward)
	var unique=[]
	for id in ids:
		if not id in unique:unique.append(id)
	check(ids.size()==3 and unique.size()==3,"reward is three distinct Bonus choices")
	check(Store.encode(g.s)!=before,"drawing a reward commits the draft")
	var saved=Store.encode(g.s);var twin=clone_game(g)
	check(twin!=null and Store.encode(twin.s)==saved,"reward choices survive save/load exactly")
	var duplicate=g.Bonus.draw(g,"acceptance040-stable",[])
	check(duplicate is Dictionary and reward_ids(duplicate)==ids and reward_id(duplicate)==reward_id(reward) and Store.encode(g.s)==saved,"same reward source returns the stable choices without mutating state")
	if ids.is_empty() or reward_id(reward)=="":return
	var picked=ids[0]
	var err=g.Bonus.choose(g,reward_id(reward),picked)
	check(err=="" and charges(wallet(g).get(picked,{}))==1,"choosing once grants exactly one deployment license",err)
	var after_choose=Store.encode(g.s)
	check(g.Bonus.choose(g,reward_id(reward),picked)!="" and Store.encode(g.s)==after_choose,"completed reward cannot be claimed twice")
	# Grant with a stable source also has at-most-once semantics, independent of UI.
	var grant_source="acceptance040-license-source"
	var other=ids[1]
	check(g.Bonus.grant(g,other,grant_source)=="","direct reward source grants one license")
	var one=charges(wallet(g).get(other,{}));var state=Store.encode(g.s)
	check(one==1 and g.Bonus.grant(g,other,grant_source)!="" and Store.encode(g.s)==state,"same source cannot duplicate a normal license")
	var deployed=installed_license_fixture(g)
	check(not deployed.is_empty(),"fixture finds a legally installable licensed Bonus")
	if not deployed.is_empty():
		var installed=deployed.game;var installed_id=deployed.bonus;var host=deployed.host
		check(charges(wallet(installed).get(installed_id,{}))==0,"install consumes the one deployment charge")
		var installed_state=Store.encode(installed.s)
		check(installed.Bonus.install(installed,installed_id,host.type,host.id,host.slot)!="" and Store.encode(installed.s)==installed_state,"spent license cannot deploy the same Bonus again")

func test_construction_and_upgrade_identity() -> void:
	var g=fresh(7042);g.s.currency=1000000.;add_material(g,1000000.)
	var targets=[];var nodes={}
	for id in playable(g):
		var node=first_upgrade(g,id)
		if node!="":targets.append(id);nodes[id]=node
		if targets.size()==2:break
	check(targets.size()==2,"fixture has two upgrades competing for construction capacity")
	if targets.size()<2:return
	var identity={}
	for id in targets:
		var p=g.s.projects[id]
		identity[id]={"tile":p.tile,"operator_org":p.operator_org,"inventory":p.inventory.duplicate(true),"node_id":str(p.get("node_id",p.get("def",""))),"tier":int(p.get("tier",1))}
		check(g.Development.start_upgrade(g,id,nodes[id])=="","upgrade job starts for "+id)
	var capacity=float(g.Construction.capacity(g));var queue=g.Construction.queue(g)
	check(capacity>0. and queue is Array and queue.size()>=2,"shared construction capacity exposes a two-job queue")
	var twin=clone_game(g)
	if twin!=null:
		var reversed={};var project_ids=twin.s.projects.keys();project_ids.reverse()
		for id in project_ids:reversed[id]=twin.s.projects[id]
		twin.s.projects=reversed
		check(g.Construction.queue(g)==twin.Construction.queue(twin),"construction queue is independent of project dictionary order")
		var cp_before=queued_work(twin);twin.Construction.tick(twin,.25);var cp_used=cp_before-queued_work(twin)
		check(cp_used>0. and cp_used<=capacity*.25+.00001,"one tick shares no more construction points than capacity allows")
	all_material_zero(g);var old_nodes={}
	for id in targets:old_nodes[id]=str(g.s.projects[id].get("node_id",g.s.projects[id].get("def","")))
	g.pulse(.5)
	var no_free_completion=true
	for id in targets:no_free_completion=no_free_completion and str(g.s.projects[id].get("node_id",g.s.projects[id].get("def","")))==old_nodes[id]
	check(no_free_completion,"zero material cannot complete queued upgrades")
	add_material(g,1000000.);var material_before=material_total(g);g.pulse(.1);var material_after=material_total(g)
	check(material_after<=material_before+.00001 and material_after>=-.00001,"construction consumes finite material without negative stock")
	# Complete through the authoritative transition to isolate identity invariants from balance duration.
	var id=targets[0];var old=identity[id];var before_complete=g.s.projects[id].duplicate(true)
	g.Development.complete(g,id)
	var p=g.s.projects[id]
	check(p.get("upgrade_job",{}).is_empty(),"upgrade completion transition succeeds")
	check(p.id==id and p.tile==old.tile and p.operator_org==old.operator_org,"upgrade preserves project ID, tile and operator")
	check(int(p.get("tier",1))==old.tier+1 and str(p.get("node_id",p.get("def","")))==nodes[id],"upgrade advances exactly to the chosen node")
	check(Store.canonical(p.inventory)==Store.canonical(before_complete.inventory),"upgrade completion preserves the project's exact inventory")
	var old_slots=before_complete.slots.keys();var slot_identity=true
	for slot in old_slots:slot_identity=slot_identity and p.slots.has(slot) and p.slot_types.get(slot)==before_complete.slot_types.get(slot)
	check(slot_identity and int(p.unlocked_slots)==int(before_complete.unlocked_slots)+1,"upgrade retains prior slot identities and unlocks one stable track slot")

func test_negative_market() -> void:
	var g=fresh(7043);g.s.currency=1000000.;g.Block.ensure(g)
	var def_id=trade_project_def(g);var tile=empty_tile(g)
	check(def_id!="" and tile!="","trade fixture can create a freight-capable project")
	if def_id=="" or tile=="":return
	var freight=g.create_building(def_id,tile,"commons",true)
	g.Development.ensure_project(g,freight)
	var resource="goods"
	check(set_market_pressure(g,resource,-1000000.),"Block market exposes resource pressure")
	var quote=g.Market.quote(g,resource)
	var buy=market_price(quote,"buy");var sell=market_price(quote,"sell")
	check(buy>sell and buy<0. and sell<0.,"negative market still keeps buy strictly above sell")
	var capacity_game=clone_game(g)
	if capacity_game!=null:
		var cap=float(capacity_game.project_stats(freight.id).get("trade_capacity",0.))
		capacity_game.Market.order(capacity_game,resource,"buy",100.,buy+.01)
		capacity_game.Market.order(capacity_game,resource,"buy",100.,buy+.01)
		var history_start=capacity_game.s.block.trade_history.size()
		capacity_game.Market.tick(capacity_game,.1)
		var traded=0.
		for row in capacity_game.s.block.trade_history.slice(history_start):traded+=float(row.amount)
		check(traded>0. and traded<=cap*.1+.00001,"two orders share one tick's freight capacity instead of each receiving a fresh allowance")
	var cash=float(g.s.currency);var stock=float(g.inventory_total(resource))
	var err=g.Market.order(g,resource,"buy",1.,buy+.01)
	check(err=="","negative-price buy order accepted",err)
	g.Market.tick(g,1.)
	check(g.inventory_total(resource)>stock and g.s.currency>cash,"negative buy adds stock and pays the city")
	stock=float(g.inventory_total(resource));cash=float(g.s.currency)
	err=g.Market.order(g,resource,"sell",1.,sell-.01)
	check(err=="","negative-price disposal order accepted",err)
	g.Market.tick(g,1.)
	check(g.inventory_total(resource)<stock and g.s.currency<cash,"negative sell removes stock and charges disposal cost")
	check(g.validate_state(g.s)=="","negative-price trades leave a valid conserved state",g.validate_state(g.s))

func test_block_naming() -> void:
	var g=fresh(7044);g.Block.grant_naming(g)
	check(g.Block.name_block(g,"提前命名")!="" and not g.s.block.naming_rights,"unfinished city goal cannot grant naming")
	g.s.city_goal.completed=true;g.Block.grant_naming(g)
	var cash=float(g.s.currency);var dt=float(g.s.dt);var inventories=inventory_snapshot(g)
	check(g.Block.name_block(g,"验收街区")=="" and g.s.block.display_name=="验收街区","completed Block receives one naming action")
	check(near(g.s.currency,cash) and near(g.s.dt,dt) and Store.canonical(inventories)==Store.canonical(inventory_snapshot(g)),"naming spends no money, DT or inventory")
	var named=Store.encode(g.s)
	check(g.Block.name_block(g,"第二次命名")!="" and Store.encode(g.s)==named,"Block naming right is consumed and cannot be replayed")

func evidence_path() -> String:
	var root=OS.get_environment("MET_EVIDENCE_DIR")
	if root=="":root=ProjectSettings.globalize_path("res://evidence040/local")
	DirAccess.make_dir_recursive_absolute(root)
	return root.path_join("acceptance040.json")

func run() -> int:
	test_schema_and_preview()
	test_license_and_stable_reward()
	test_construction_and_upgrade_identity()
	test_negative_market()
	test_block_naming()
	if checks.size()!=45:check(false,"suite executed every planned assertion","expected 45, reached "+str(checks.size()))
	var failed=checks.filter(func(item):return not item.pass).size()
	var report={"build":"MET 0.4.0","passed":checks.size()-failed,"failed":failed,"checks":checks,"method":"Native Godot headless core acceptance using direct fixtures; not UI, macOS hardware, or human playthrough evidence."}
	var file=FileAccess.open(evidence_path(),FileAccess.WRITE)
	if file!=null:file.store_string(JSON.stringify(report,"\t"))
	print("ACCEPTANCE040 ",checks.size()-failed,"/",checks.size()," evidence=",evidence_path())
	return 0 if failed==0 else 1
