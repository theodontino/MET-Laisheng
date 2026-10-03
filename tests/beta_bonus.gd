extends SceneTree
## Beta reward persistence, actual deployment, blueprint and source-reduction contracts.
const Game = preload("res://core/game.gd")
const Rules = preload("res://core/rules.gd")
const Economy = preload("res://core/bonus_economy040.gd")
const Fields = preload("res://core/fields.gd")
const Store = preload("res://core/storage.gd")
var checks = 0
var failures = 0

func _initialize() -> void:
	call_deferred("_run")

func check(ok:bool, label:String) -> void:
	checks += 1
	if not ok: failures += 1
	print(("BETA BONUS PASS " if ok else "BETA BONUS FAIL ") + label)

func near(a:float,b:float) -> bool:
	return absf(a-b) < 0.00001

func fresh():
	var g = Game.new()
	g.new_game(81276, 1000000.0)
	g.select_block(0)
	g.choose_origin(str(g.s.origin_choices[0]))
	g.s.block.reward_choices = []
	g.s.block.bonus_wallet = {}
	g.s.block.blueprint_levels = {}
	return g

func building(g, kind:String, slots:Array) -> String:
	var tile = ""
	for key in Rules.ids(g.s.cells):
		if str(g.s.cells[key].get("building", "")) == "" and not "tile.water" in g.s.cells[key].get("traits", []):
			tile = str(key)
			break
	var p:Dictionary = g.create_building(kind, tile, "commons", true)
	p.slots = {}
	p.slot_types = {}
	for index in slots.size():
		var slot = "test:" + str(index)
		p.slots[slot] = ""
		p.slot_types[slot] = slots[index]
	g.refresh_metrics()
	return str(p.id)

func value(g, bonus_id:String, rule_id:String, count:int=1):
	for rule in Rules.bonus_rules(g, bonus_id, count):
		if str(rule.id) == rule_id: return rule.get("value", rule.get("fraction", rule.get("rate", 0)))
	return null

func reward_suite() -> void:
	var g = fresh()
	for count in 4:
		var pool = ["P01", "P07", "L05"].slice(0,count)
		var reward = Economy.draw(g, "variable:"+str(count), [], {"bonus_ids":pool})
		check(reward.choices.size() == maxi(1,count), "draft accepts %d candidates" % count)
		var rng = g.s.rng.duplicate(true)
		var reopened = Economy.draw(g, "variable:"+str(count), [], {"bonus_ids":["R10"]})
		check(reopened == reward and g.s.rng == rng, "source persists candidates without reroll %d" % count)
		var dt = float(g.s.dt)
		var cash = float(g.s.currency)
		var bid = str(reward.choices[0].bonus_id)
		check(Economy.choose(g, str(reward.reward_id), bid) == "", "claim variable draft %d" % count)
		if count == 0: check(near(float(g.s.currency)-cash,30) and near(float(g.s.dt),dt), "empty pool pays cash once without DT")
		check(Economy.choose(g,str(reward.reward_id),bid) != "", "claim is idempotent %d" % count)
	var a = Economy.draw(g,"annual:1",[],{"bonus_ids":["P01"]})
	var b = Economy.draw(g,"population:32",[],{"bonus_ids":["P01"]})
	var c = Economy.draw(g,"theme:M1",[],{"bonus_ids":["P01"]})
	check(Economy.pending(g).size()==3, "simultaneous annual population theme rewards retain three records")
	check(Economy.choose(g,str(c.reward_id),"P01")!="", "reward queue enforces stable order")
	var snapshot:Dictionary = Store.decode(Store.encode(g.s)).state
	var twin = Game.new()
	twin.s = snapshot
	check(Store.encode({"choices":Economy.draw(twin,"population:32",[],{}).choices}) == Store.encode({"choices":b.choices}), "save reload preserves queued candidates")
	var before = int(g.s.block.bonus_wallet.P01.charges)
	for record in [a,b,c]: check(Economy.choose(g,str(record.reward_id),"P01")=="", "same card remains legal across reward records")
	check(int(g.s.block.bonus_wallet.P01.charges)==before+3, "repeated rewards accumulate copies")

func deployment_suite() -> void:
	var g = fresh()
	var first = building(g,"workshop",["Production","Production","Production"])
	var second = building(g,"workshop",["Production"])
	for i in 4: Economy.grant(g,"P01","fixture:"+str(i))
	for i in 3: check(Economy.install(g,"P01","building",first,"test:"+str(i))=="", "same host copy installs into legal slot %d" % i)
	check(Economy.install(g,"P01","building",second,"test:0")=="", "different host takes full strength first copy")
	check(int(g.s.block.bonus_wallet.P01.charges)==0, "each installation consumes exactly one copy")
	check(near(float(value(g,"P01","P01.4",3)),8.0/3.0), "three x2 copies merge to x2.6667")
	check(near(float(value(g,"P01","P01.1",3)),11.0/6.0), "numeric input penalty scales with copies")
	check(near(float(value(g,"P07","P07.1",3)),40.0/3.0), "additive strength combines linearly")
	var first_modifiers:Array = g.compiled.trace[first]["output.goods"].rules
	var copies_sources = 0
	for mod in first_modifiers:
		if str(mod.source).begins_with("粗野制造"):
			copies_sources += 1
			check(near(float(mod.value),8.0/3.0), "actual compiler emits merged x2.6667 modifier")
	check(copies_sources==1, "actual compiler emits one source per same-host bonus group")
	var full_second = false
	for mod in g.compiled.trace[second]["output.goods"].rules:
		if str(mod.source).begins_with("粗野制造"): full_second = near(float(mod.value),2)
	check(full_second,"different host remains x2 in actual compiler")
	check(g.clear_slot(first,"test:0")=="" and int(g.s.block.bonus_wallet.P01.charges)==0, "uninstall never refunds a copy")
	Economy.grant(g,"P01","extra")
	check(Economy.install(g,"P01","building",first,"test:1")!="" and int(g.s.block.bonus_wallet.P01.charges)==1, "same-slot no-op refuses without consuming copy")
	var residence=building(g,"homestead",["Operations","Operations"])
	Economy.grant(g,"O02","structural:1")
	Economy.grant(g,"O02","structural:2")
	check(Economy.install(g,"O02","building",residence,"test:0")=="", "first structural bonus installs")
	check(Economy.install(g,"O02","building",residence,"test:1")!="" and int(g.s.block.bonus_wallet.O02.charges)==1,"idempotent structure duplicate keeps unspent copy")
	check(not Rules.duplicate_has_benefit(g,"M01",1) and not Rules.duplicate_has_benefit(g,"L02",1),"slot and Field networks never duplicate just to charge costs")
	check(near(float(value(g,"R05","R05.2",100)),0),"diminishing multipliers cap at zero, never negative")
	check(Economy.install(g,"P01","block",str(g.s.block.id))!="", "copy scope remains immutable despite blueprint capabilities")

func research_suite() -> void:
	var g=fresh()
	var host=building(g,"workshop",["Production"])
	Economy.grant(g,"P01","owned")
	Economy.install(g,"P01","building",host,"test:0")
	var initial=float(g.compiled.stats[host]["output.goods"])
	check(Rules.bonus_content().upgrades.size()==12,"exactly twelve declared upgrade blueprints")
	check(Economy.research_candidates(g,["P01","P07"]).size()==1,"research requires owned or installed thematic blueprint")
	for level in [1,2]:
		var reward=Economy.draw(g,"research:"+str(level),[],{"kind":"research","bonus_ids":["P01"]})
		check(str(reward.kind)=="research" and Economy.choose(g,str(reward.reward_id),"P01")=="","research opportunity applies one blueprint level")
		check(Economy.blueprint_level(g,"P01")==level and int(g.s.block.bonus_wallet.P01.charges)==0,"research upgrades existing instance without copy cost")
		check(near(float(value(g,"P01","P01.4")),2.0+float(level)*0.2),"blueprint output strength matches explicit content")
		check(near(float(value(g,"P01","P01.1")),1.5),"upgrade preserves input penalty")
	check(float(g.compiled.stats[host]["output.goods"])>initial,"research recompiles installed effects immediately")
	var exhausted=Economy.draw(g,"research:max",[],{"kind":"research","bonus_ids":["P01"]})
	check(str(exhausted.kind)=="copy" and Economy.choose(g,str(exhausted.reward_id),"P01")=="","max-level research becomes thematic copy")
	check(Economy.blueprint_level(g,"P01")==2 and int(g.s.block.bonus_wallet.P01.charges)==1,"blueprint cannot exceed level two")
	check(near(float(value(g,"P01","P01.4",3)),10.0/3.0),"upgrade is applied before duplicate weighting")
	var new_game=fresh()
	check(Economy.blueprint_level(new_game,"P01")==0 and new_game.s.block.bonus_wallet.is_empty(),"independent new Block inherits neither blueprint nor copies")
	var stale=fresh()
	Economy.grant(stale,"P01","stale:owned")
	stale.s.block.blueprint_levels.P01=1
	var x=Economy.draw(stale,"research:queued1",[],{"kind":"research","bonus_ids":["P01"]})
	var y=Economy.draw(stale,"research:queued2",[],{"kind":"research","bonus_ids":["P01"]})
	Economy.choose(stale,str(x.reward_id),"P01")
	check(Economy.choose(stale,str(y.reward_id),"P01")!="" and str(Economy.pending(stale)[0].kind)=="copy","stale queued research persists a fallback instead of disappearing")
	check(Economy.choose(stale,str(y.reward_id),"P01")=="","stale research fallback remains claimable")

func conversion_suite() -> void:
	var g=fresh()
	var conversions=[]
	for rule in Rules.bonus_rules(g,"P06",4):
		if rule.op=="field_convert": conversions.append(rule)
	check(conversions.size()==1 and near(float(conversions[0].rate),2),"repeated recycling changes throughput, does not clone conversion chain")
	g.s.block.blueprint_levels.P06=2
	for rule in Rules.bonus_rules(g,"P06",4):
		if rule.op=="field_convert":check(near(float(rule.output.material),3.6) and near(float(rule.amount),10),"recycling research keeps real pollution cost")
	var env={"budgets":{"source":{"type":"pollution","remaining":10.,"local":{"0,0":10.}}},"tiles":{"0,0":{"pollution":[{"field_id":"source","value":10.}]}}}
	var first=Fields.consume_local_pollution(env,"0,0",20,1)
	var second=Fields.consume_local_pollution(env,"0,0",20,1)
	check(near(first,10) and near(second,0),"shared pollution quota cannot be consumed twice")
	var capacities=Rules.bonus_rules(g,"P02",100)
	check(near(float(capacities[0].fraction),1),"conversion capacity respects conservation cap")

func _run() -> void:
	reward_suite()
	deployment_suite()
	research_suite()
	conversion_suite()
	print("BETA BONUS RESULT %d/%d passed" % [checks-failures,checks])
	quit(1 if failures else 0)
