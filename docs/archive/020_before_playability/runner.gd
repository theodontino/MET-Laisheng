extends RefCounted
const Game=preload("res://core/game.gd")
const R=preload("res://core/rules.gd")
const F=preload("res://core/fields.gd")
const A=preload("res://core/allocation.gd")
const Pop=preload("res://core/population.gd")
const Store=preload("res://core/storage.gd")
const Paths=preload("res://tests/paths.gd")
var checks=[]
func check(ok:bool,name:String) -> void:
	checks.append({"name":name,"pass":ok});print(("PASS " if ok else "FAIL ")+name)
func near(a:float,b:float,eps:float=.00001)->bool:return absf(a-b)<=eps
func fresh(seed_value:int=7):
	var g=Game.new();g.new_game(seed_value,1000000);g.select_block(0);g.choose_origin(g.s.origin_choices[0]);return g
func fixture(defs:Array,tiles:Array):
	var g=fresh();g.s.projects={};g.s.sites={};g.s.groups={};g.s.bonus_instances={};g.s.fields={};g.s.totals.built=0
	for k in g.s.cells:g.s.cells[k].building="";g.s.cells[k].site="";g.s.cells[k].zone="";g.s.cells[k].fields={}
	g._create_special("homeless","待安置",true);g._create_special("municipal","公共仓储",false)
	for r in ["food","material","goods"]:g.s.projects.municipal.inventory[r]=30.
	g.s.currency=1000;g.s.dt=90;g.s.debug_used=true;g.s.migration_paused=true
	for i in defs.size():g._create_starter(defs[i],[tiles[i]],"commons")
	g.refresh_metrics();return g
func id(g,def:String) -> String:
	for bid in g.playable_ids():
		if g.s.projects[bid].def==def:return bid
	return ""
func mount(g,bid:String,bonus:String,slot:String="",provider:String="commons") -> String:
	var s=slot if slot!="" else g.c.bonuses[bonus].slot
	return g.mount_bonus(bonus,"building",bid,provider,s)
func run() -> int:
	var g=fresh();var b=fresh()
	check(g.s.schema==3 and g.s.build=="0.2.0-b-buildlab.1","schema3 version and B lineage")
	check(g.validate_state(g.s)=="","initial authoritative state valid")
	check(g.c.projects.size()==10 and g.c.bonuses.size()==40 and g.c.operators.size()==8,"10 basic chassis / 40 active definitions / 8 operator profiles")
	check(g.c.data.backlog.size()==63,"all 63 ID candidates retained, including inactive backlog")
	check(JSON.stringify(g.s,"",true,true)==JSON.stringify(b.s,"",true,true),"AC01 same seed creates identical opening")
	check(near(g.s.metrics.population,18) and near(g.s.metrics.workforce,18),"PopulationGroup has no labor-participation ratio")
	check(g.s.groups.size()==1 and not g.s.projects[id(g,"hamlet")].has("residents"),"population is independent, not authoritative building resident fields")
	check(g.create_tender(["0,-1","1,-1"],"farm")!="","single tile only; multigrid interface disabled")
	check(g.create_tender(["0,-1"],"farm")=="","selected basic building opens tender on empty unzoned tile")
	var same=true;var prices=[];var offers=g.s.draft.offers.duplicate(true)
	for offer in offers:
		if offer.base!="farm" or g.c.bonuses[offer.bonus_id].slot!=offer.slot:same=false
		prices.append(g.Draft.offer_cost(g,offer))
	check(offers.size()==3 and same and prices[0]==prices[1] and prices[1]==prices[2],"AC23 same chassis and base cost; Operator + Bonus variation only")
	var rng=g.s.rng.duplicate();var olddraft=g.s.draft.duplicate(true)
	check(g.create_tender(["1,-1"],"works")!="" and g.s.rng==rng and g.s.draft==olddraft,"closing/changing target cannot silently redraw")
	var encoded=Store.encode(g.s);var restored=Store.decode(encoded)
	check(restored.ok and g.validate_state(restored.state)=="","current save checksum and schema roundtrip")
	b.s=restored.state;b.refresh_metrics();g.reroll();b.reroll()
	check(g.s.draft==b.s.draft and g.s.rng==b.s.rng,"AC01 save restores deterministic offer stream")
	check(not Store.decode(encoded.replace('farm','tampered')).ok,"tampered save checksum rejected")
	var old=g.s.duplicate(true);old.schema=2
	check(g.validate_state(old)!="","0.1.1 save not silently imported or overwritten")
	var picked=g.s.draft.offers[0];check(g.accept_offer(picked.id)=="","real offered proposal accepted")
	var newest=g.playable_ids()[-1]
	check(g.s.projects[newest].slots.size()==3 and not g.s.projects[newest].slots.has("Operator"),"AC19 operator is relation, not a fourth slot")
	check(g.s.projects[newest].operator_org==picked.operator_org,"tender installs proposed operator")
	g=fresh();var prior=g.s.duplicate(true);var gain=g.resume_time(g.s.wall_mark+6*3600)
	check(gain>0 and g.s.local_hours==prior.local_hours and g.s.groups==prior.groups,"offline recovery changes DT, not group simulation")
	check(g.resume_time(g.s.wall_mark)==0,"offline interval cannot be claimed twice")
	var x=[{"uid":"a","source":"base","mode":"add","value":5.,"specificity":0,"priority":0},{"uid":"b","source":"bonus","mode":"multiply","value":1.5,"specificity":10,"priority":0},{"uid":"c","source":"limit","mode":"cap","value":20.,"specificity":0,"priority":0}]
	check(near(R.reduce_number(10,x).value,20),"AC15 base plus add times multiplier then cap")
	x.append({"uid":"d","source":"special","mode":"override","value":7.,"specificity":30,"priority":0});x.append({"uid":"e","source":"general","mode":"override","value":9.,"specificity":10,"priority":999})
	check(near(R.reduce_number(10,x).value,7),"AC16 special overrides general, separately from phase order")
	x.reverse();check(near(R.reduce_number(10,x).value,7),"Modifier reduction independent of collection order")
	check(near(R.aggregate([{"value":5.},{"value":3.},{"value":-2.}]),6) and near(R.aggregate([{"value":5.},{"value":3.}],"max"),5),"Field negative values and aggregate readers")
	# Finite resource arbitration and unused reservations.
	g=fixture(["timber","works","works"],["0,0","1,0","3,0"])
	var source=id(g,"timber");var targets=[]
	for bid in g.playable_ids():
		if g.s.projects[bid].def=="works":targets.append(bid)
	for p in g.s.projects.values():p.inventory={"food":0.,"material":0.,"goods":0.}
	g.s.projects[source].inventory.material=10;g.refresh_metrics();var access=F.access(g,g.compiled)
	var claims={"near":{"owner":targets[0],"needs":{"material":8.}},"far":{"owner":targets[1],"needs":{"material":8.}}}
	var res=A.resources(g.s,g.compiled,claims,access)
	check(near(res.grants.near.material,8) and near(res.grants.far.material,2),"AC03 shortage gives nearer consumer priority")
	var reorder={"far":claims.far,"near":claims.near}
	check(A.resources(g.s,g.compiled,reorder,access)==res,"AC02 consumer request dictionary order cannot alter allocation")
	var settled=A.settle_resources(g.s,res,{"near":{"material":4.},"far":{"material":2.}})
	check(near(settled.inventories[source].material,4) and near(g.s.projects[source].inventory.material,10),"reservations only consume used receipt; resolver never mutates snapshot")
	# Per-source access, personnel and finite service pools.
	g=fixture(["hamlet","works","clinic"],["-1,1","1,1","0,1"])
	var home=id(g,"hamlet");var worker=id(g,"works");var clinic=id(g,"clinic");var gid=Pop.create(g,home,12);g.refresh_metrics()
	access=F.access(g,g.compiled);var keys=access.fields.keys();var fields_for_clinic=[]
	for fid in keys:
		if access.fields[fid].source_id==clinic:fields_for_clinic.append(access.fields[fid].type)
	check("employment" in fields_for_clinic and "medical" in fields_for_clinic,"AC08 distinct Field entity per Source/type")
	check(F.source_value(access.tiles,g.s.projects[home].tile,"medical",clinic)>0,"AC09 access samples retain concrete service provider")
	var jobs=A.labor(g.s,g.compiled,access);var employed=0.
	for n in jobs.employment[gid].values():employed+=n
	check(employed<=12.000001,"one Pop cannot occupy two jobs")
	var effective=0.
	for n in jobs.effective.values():effective+=n
	check(near(effective,employed),"AC10 subunit access is not multiplied into successful labor")
	var uses={}
	for bid in g.s.projects:uses[bid]={"utilization":1.}
	var service=A.services(g,g.compiled,access,uses)
	check(service.covered[gid].get("medical",0)<=12 and service.pools[clinic+"|medical"].remaining>=0,"service reservations obey population demand and provider capacity")
	var beforepeople=g.s.groups[gid].count;var edu=g.s.groups[gid].education
	g.demolish(home)
	check(g.s.groups.has(gid) and g.s.groups[gid].host=="homeless" and near(g.s.groups[gid].count,beforepeople) and near(g.s.groups[gid].education,edu),"AC07 demolished housing preserves independent group/state")
	check(g.s.groups[gid].employment.has(worker) or g.s.groups[gid].employment.is_empty(),"group employment links not tied to residence identity")
	# Slot/host/exclusion/provenance semantics.
	g=fixture(["works","works","clinic","school"],["0,0","1,0","0,1","1,1"]);worker=id(g,"works");clinic=id(g,"clinic")
	check(mount(g,worker,"P01","Logistics")!="","AC20 reject incompatible slot")
	check(mount(g,worker,"P01")=="" and mount(g,worker,"L05")=="","AC12 unrelated bonuses coexist")
	g.c.bonuses["M01"].excludes=["P01"]
	check(mount(g,worker,"M01")!="","AC12 declared mutual exclusion enforced")
	g.c.bonuses["M01"].excludes=[];check(mount(g,worker,"M01")=="" and g.s.projects[worker].slots.has("Flex"),"AC21 module grants Flex without becoming a base slot")
	check(mount(g,worker,"P05","Flex")=="","Flex accepts another production rule")
	check(g.clear_slot(worker,"Operations")!="","cannot remove parent while dependent Flex occupied")
	g.clear_slot(worker,"Flex");check(g.clear_slot(worker,"Operations")=="" and not g.s.projects[worker].slots.has("Flex"),"clearing dependency removes slot cleanly")
	var other=g.playable_ids()[1];var base2=float(g.project_stats(other)["output.goods"])
	check(g.project_stats(worker)["output.goods"]>base2,"AC22 building-host effect limited to its building")
	check(g.mount_bonus("P01","block",g.s.block.id,"commons")=="" and near(g.project_stats(other)["output.goods"],base2*2),"AC22 same Bonus at Block scope reaches other eligible buildings")
	var initial=R.reduce_number(10,[]);check(initial.value==10,"compilation has no cumulative mutation")
	g=fixture(["works"],["0,0"]);worker=id(g,"works")
	check(mount(g,worker,"O06","Operations","civic")=="" and near(g.project_stats(worker)["output.goods"],0),"AC25 foreign Provider makes no self-operation retention")
	check(mount(g,worker,"O06","Operations","commons")=="" and near(g.project_stats(worker)["output.goods"],1.2),"AC25 same Provider/Operator activates specific override")
	check("building.industrial" in g.s.projects[worker].traits,"reclassification does not mutate innate Trait")
	g=fixture(["clinic","school"],["0,0","1,0"]);clinic=id(g,"clinic");var school=id(g,"school")
	check(mount(g,clinic,"P02")=="","research hospital accepts medical chassis")
	var st=g.project_stats(clinic)
	check(near(st["service.medical"],16.8) and near(st["service.education"],7.2),"capacity conversion pays medical capacity once")
	check(near(g.project_stats(school).efficiency,1.2),"neighbor selector applies education bonus to actual neighbor")
	g.refresh_metrics();check(near(g.project_stats(clinic)["service.education"],7.2),"compiling twice cannot duplicate converted capacity")
	# Policies replace rather than accumulate and organizations target buildings only.
	g=fresh();check(g.switch_policy("tax_trade")=="" and g.s.block.policies.Tax=="tax_trade","AC13 policy replaces one fixed slot")
	check(g.compiled.options.tax_mode=="trade","inactive tax mode no longer executes")
	var groupcopy=g.s.groups.duplicate(true);g.mount_bonus("G02","organization","commons","commons")
	check(g.s.groups==groupcopy and not g.s.organizations.commons.has("inventory"),"AC11 organization modifiers do not directly mutate groups or own inventories")
	var softness=false
	for seed_value in range(1,61):
		var t=fresh(seed_value);t.create_tender(["0,-1"],"works")
		for offer in t.s.draft.offers:
			if not offer.bonus_id in t.c.operators[offer.provider_org].preferred_bonuses:softness=true
	check(softness,"AC24 operator preferences allow nonpreferred legal bonuses")
	# Emission/recycling quota is one pool even when many tiles see the source.
	g=fixture(["works","warehouse","power","hamlet"],["0,0","1,0","2,0","0,1"])
	worker=id(g,"works");var warehouse=id(g,"warehouse");home=id(g,"hamlet");Pop.create(g,home,24);g.refresh_metrics()
	mount(g,worker,"P01");mount(g,warehouse,"P06")
	var out=g.pulse(.5)
	check(out.advanced>.49 and g.s.audit.pollution_recycled<=g.s.audit.pollution_generated+.00001,"recyclers cannot consume copied pollution mass")
	check(g.s.audit.pollution_recycled>0,"advanced recycling creates actual output from finite local emissions")
	check(g.s.audit.resources.material.error<.00001 and g.s.audit.resources.goods.error<.00001,"atomic recycling outputs enter resource conservation ledger")
	check(g.s.fields.size()>0 and g.s.cells["0,1"].fields.has("pollution"),"AC09 real environment reaches tile with provenance")
	check(g.s.metrics.utilization.has(worker) and g.compiled.trace[worker].has("output.goods"),"AC18 UI has real utilization and modifier evidence")
	check(not g.s.projects[id(g,"power")].inventory.has("energy"),"AC04 default power does not accumulate ordinary stock")
	g=fixture(["power"],["0,0"]);var power=id(g,"power");g.c.projects.power.stats["storage.energy"]=12;g.c.projects.power.stats.jobs=0;g.refresh_metrics();g.pulse(.5)
	check(g.s.projects[power].inventory.get("energy",0)>0 and g.s.projects[power].inventory.energy<=12,"AC05 explicit energy-storage capability enables finite stock")
	g=fixture(["timber","warehouse"],["0,0","1,0"]);source=id(g,"timber");warehouse=id(g,"warehouse");var inv=A.stocks(g.s);var spill={};var moves=[]
	inv[source].material=g.stock_cap_for(source,"material")
	g.Sim.deposit(g.s,g.compiled,inv,source,"material",8.,spill,moves)
	check(near(inv[warehouse].material,8) and spill.get("material",0)==0,"AC06 local overflow reaches available warehouse before loss")
	for bid in inv:inv[bid].material=g.stock_cap_for(bid,"material")
	g.Sim.deposit(g.s,g.compiled,inv,source,"material",8,spill,moves)
	check(near(spill.material,8),"AC06 true full storage records loss explicitly")
	g=fixture([] ,[]);var construction=g.create_building("farm","0,0","commons",false);construction.job.work=.05;construction.job.total=.05;construction.job.material=.1
	g.pulse(.1)
	check(construction.id in g.s.projects and g.s.projects[construction.id].status=="running" and g.s.projects[construction.id].outputs_last.is_empty(),"AC17 completed building does not produce retroactively in its construction tick")
	g=fixture(["works","power","hamlet"],["0,0","1,0","0,1"]);worker=id(g,"works");home=id(g,"hamlet");Pop.create(g,home,24);g.refresh_metrics()
	mount(g,worker,"P05");g.mount_bonus("B02","block",g.s.block.id,"commons");g.pulse(.5)
	check(g.s.audit.heat_generated>0 and g.s.audit.heat_used<=g.s.audit.heat_generated+.000001,"heat remains a finite source budget across receivers")
	check(g.s.projects[home].next_heat_bonus>0,"actual consumed heat schedules next-tick effect, not recursive current output")
	var twin=Game.new();twin.s=Store.decode(Store.encode(g.s)).state;twin.refresh_metrics();g.pulse(.5);twin.pulse(.5)
	check(Store.encode(g.s)==Store.encode(twin.s),"AC01 deterministic continuation across native JSON save/load")
	# General source entities and schema validation, not just building-only fixtures.
	g=fixture(["hamlet"],["0,0"]);home=id(g,"hamlet");Pop.create(g,home,12);g.refresh_metrics()
	var holy=g.define_field("0,0","holy",-2.,2,.5)
	access=F.access(g,g.compiled)
	check(holy!="" and access.fields.has(holy) and near(R.aggregate(access.tiles["1,0"].holy),-2),"Tile Entity can emit a separately identified negative Field")
	check(access.tiles["1,0"].holy[0].source_id=="0,0","non-building Field provenance survives Tile projection")
	check(g.define_field("not-an-entity","holy",1,1,.5)=="","Field creation rejects a nonexistent source")
	g.pulse(.1)
	check(g.s.cells["1,0"].fields.has("holy"),"authored Field coverage is derived into the committed Tile registry")
	var malformed=g.s.duplicate(true);malformed.projects[home].inventory=[]
	check(g.validate_state(malformed)!="","malformed inventory rejected before import simulation")
	malformed=g.s.duplicate(true);malformed.groups[malformed.groups.keys()[0]].employment=[]
	check(g.validate_state(malformed)!="","malformed employment map rejected before import simulation")
	g=fixture(["works"],["0,0"]);worker=id(g,"works");mount(g,worker,"O06")
	check("building.commercial" in R.semantic_traits(g.s,worker) and g.bonus_eligible("L01","building",worker,"Logistics")=="","redeveloped industry gains commercial eligibility without changing innate Trait")
	g.clear_slot(worker,"Operations")
	check(not "building.commercial" in R.semantic_traits(g.s,worker) and g.bonus_eligible("L01","building",worker,"Logistics")!="","removing redevelopment drops derived commercial role immediately")
	# Service-area recipes read actual Access: extending service reach changes consumers.
	g=fixture(["hamlet","clinic"],["3,0","0,0"]);home=id(g,"hamlet");clinic=id(g,"clinic");Pop.create(g,home,12)
	g.c.projects.clinic.stats.service_range=3;g.c.projects.clinic.stats.service_decay=.1
	g.refresh_metrics();mount(g,clinic,"P04");g.pulse(.1)
	var used_recipe=false
	for receipt in g.s.metrics.allocation:
		if str(receipt.consumer).begins_with("recipe:") and receipt.used>0:used_recipe=true
	check(used_recipe,"biomass recipients follow service Access rather than a hardcoded two-hex radius")
	g=fixture(["hamlet"],["0,0"]);home=id(g,"hamlet");Pop.create(g,home,12);g.refresh_metrics()
	var deposit_field=g.define_field("0,0","pollution",10,1,.5)
	g.pulse(.1)
	check(g.s.fields.has(deposit_field) and g.s.groups.values()[0].living.pollution>0,"Tile-sourced environmental Field reaches the population interpretation rule")
	var cleanup_field=g.define_field("0,0","pollution",-5,1,.5)
	g.pulse(.1)
	check(g.s.fields.has(cleanup_field) and near(g.s.audit.pollution_generated,1.),"negative environmental contributions do not mint recyclable pollution mass")
	# Every shipping Bonus is exercised as an actual mounted rule through one native tick.
	var bonus_exercises=[]
	for bonus_id in R.ids(g.c.bonuses):
		var trial=fixture(["hamlet","farm","timber","power","works","market","school","clinic","park","warehouse"],["0,0","0,1","1,0","2,0","2,1","1,1","0,2","1,2","-1,1","-1,2"])
		Pop.create(trial,id(trial,"hamlet"),24,{"education":70.,"happiness":85.})
		for key in trial.s.cells:
			if not "tile.waterfront" in trial.s.cells[key].traits:trial.s.cells[key].traits.append("tile.waterfront")
		trial.refresh_metrics();var def=trial.c.bonuses[bonus_id];var host_type=str(def.hosts[0]);var host="";var slot=""
		if host_type=="building":
			for target in trial.playable_ids():
				if trial.bonus_eligible(bonus_id,host_type,target,def.slot)=="":host=target;slot=def.slot;break
		elif host_type=="organization":host="commons"
		else:host=trial.s.block.id
		var err="No eligible fixture" if host=="" else trial.mount_bonus(bonus_id,host_type,host,"commons",slot)
		var result={"advanced":0.}
		if err=="":result=trial.pulse(.1)
		var ok=err=="" and result.advanced>.0999 and trial.validate_state(trial.s)==""
		bonus_exercises.append({"bonus":bonus_id,"name":def.name,"pass":ok,"error":err})
		check(ok,"shipping Bonus tick: "+bonus_id+" "+def.name)
	FileAccess.open(Paths.file("bonus-exercises020.json"),FileAccess.WRITE).store_string(JSON.stringify({"fixtures":bonus_exercises,"note":"Direct rule fixtures; proves each can mount and simulate without runtime/ledger failure, not that its gameplay effect is balanced."},"\t"))
	# All listed operators are understood; every Rule is one registered action.
	var valid=true
	for bonus in g.c.bonuses.values():
		for rule in bonus.rules:
			if not rule.op in R.SUPPORTED or not rule.has("phase"):valid=false
	check(valid,"AC14 all content rules use one registered operator and declared phase")
	var fails=0
	for c0 in checks:
		if not c0.pass:fails+=1
	FileAccess.open(Paths.file("rules020.json"),FileAccess.WRITE).store_string(JSON.stringify({"build":"0.2.0-b-buildlab.1","checks":checks,"passed":checks.size()-fails,"failed":fails,"method":"Native Godot rule fixtures; direct fixture setup is not claimed as player playthrough."},"\t"))
	print("RULES020 ",checks.size()-fails,"/",checks.size());return 0 if fails==0 else 1
