extends SceneTree
## Exact comparison to the prior reconciliation algorithm, including the capped
## nonconvergent branch and complete real-simulator state/receipt parity.
const Game=preload("res://core/game.gd")
const Alloc=preload("res://core/allocation.gd")
const Legacy=preload("res://tests/beta_allocation_reference.gd")
const Store=preload("res://core/storage.gd")
const Paths=preload("res://tests/paths.gd")
var checks:Array=[]

func check(ok:bool,label:String) -> void:
	checks.append({"pass":ok,"name":label})
	print(("PASS " if ok else "FAIL ")+label)

func fresh():
	var g=Game.new();g.new_game(9831,1000.0);g.select_block(0);g.choose_origin(g.s.origin_choices[0])
	g.s.settings.user_paused=false
	return g

func exact_case(name:String,stocks:Dictionary,needs:Array,ceilings:Dictionary={},mode:String="near") -> Dictionary:
	var g=fresh()
	for p in g.s.projects.values():p.inventory={}
	g.s.projects.municipal.inventory=stocks.duplicate(true)
	for id in g.compiled.config:g.compiled.config[id]={}
	g.compiled.options.procure_mode=mode
	var ids=g.playable_ids();var claims={}
	for i in needs.size():
		claims["claim:"+str(i)]={"owner":ids[i%ids.size()],"needs":needs[i],"priority":10-i}
	var before=Store.encode(g.s)
	var access=g.Fields.access(g,g.compiled)
	var previous=Legacy.feasible_resources(g.s,g.compiled,claims,access,ceilings)
	var current=Alloc.feasible_resources(g.s,g.compiled,claims,access,ceilings)
	check(current==previous,name+": grants, ordered source transfers, remainder and round count match exactly")
	check(Store.encode(g.s)==before,name+": allocation leaves full source state and RNG unchanged")
	var previous_receipt=Alloc.settle_resources(g.s,previous,previous.grants)
	var current_receipt=Alloc.settle_resources(g.s,current,current.grants)
	check(previous_receipt==current_receipt,name+": committed stock ledger and source receipts match exactly")
	return current

func simulation_parity() -> void:
	# Only replace the two resource reconciliation calls in an in-memory copy.
	# All other simulation phases, content, construction and market code are the
	# identical production modules; no test-only production branch is introduced.
	var source=FileAccess.get_file_as_string("res://core/simulation.gd")
	source=source.replace("extends RefCounted","extends RefCounted\nconst Legacy=preload(\"res://tests/beta_allocation_reference.gd\")")
	source=source.replace("A.feasible_resources(","Legacy.feasible_resources(")
	var previous_sim=GDScript.new();previous_sim.source_code=source
	check(previous_sim.reload()==OK,"old-algorithm reference simulator compiles")
	for scenario in ["stocked","starved","split_procurement","energy_limited"]:
		var old=fresh()
		var cells=old.s.cells.keys();cells.sort()
		var defs=["homestead","neighborhood","workshop","residence","agriculture","power_station","public_center","medical_clinic","primary_school","yard","freight","manufacturing"]
		for tile in cells:
			if old.playable_ids().size()>=12:break
			if old.s.cells[tile].building!="" or "tile.water" in old.s.cells[tile].traits:continue
			old.create_building(defs[old.playable_ids().size()%defs.size()],tile,"commons",true)
		if scenario=="starved":
			for p in old.s.projects.values():p.inventory={"food":0.0,"material":0.0,"goods":0.0}
		if scenario=="split_procurement":old.s.block.policies.Procurement="procure_split"
		if scenario=="energy_limited":
			old.c.balance.base_energy=0.05
			for p in old.s.projects.values():
				if p.get("def","")=="power_station":p.paused=true
		old.refresh_metrics()
		var current=Game.new();current.c=old.c;current.s=old.s.duplicate(true);current.refresh_metrics()
		var equal=true;var errors=[]
		# Cross the 3-month immigration edge, while exercising both tiny normal
		# intervals and ordinary daily capped production batches.
		old.s.local_hours=2.98;current.s.local_hours=2.98
		for months in [0.0000001,0.01,0.02,1.0/12.0]:
			var context={"external_seconds":months*60.0}
			var old_error=previous_sim.tick(old,months,context)
			var new_error=current.Sim.tick(current,months,context)
			if old_error!="" or new_error!="":errors.append([old_error,new_error])
			if Store.encode(old.s)!=Store.encode(current.s):equal=false
		check(errors.is_empty(),scenario+": both real simulators produce valid committed states")
		check(equal,scenario+": full state, RNG, money, population, trade and every receipt are byte-identical")

func _initialize() -> void:
	var r=exact_case("ample stocks",{"food":100.0,"material":100.0},[{"food":10.0,"material":10.0},{"material":4.0}])
	check(r.reconcile_rounds==1,"ample stocks converges on the first allocation")
	r=exact_case("no inputs",{},[{"food":10.0,"material":10.0},{"material":4.0}])
	check(float(r.grants["claim:0"].food)==0.0 and float(r.grants["claim:1"].material)==0.0,"missing inputs never receive fabricated grants")
	r=exact_case("cross-ingredient release",{"food":2.0,"material":10.0},[{"food":10.0,"material":10.0},{"material":10.0}])
	check(float(r.grants["claim:1"].material)==8.0,"material blocked by a different input is released to the next consumer")
	r=exact_case("energy utilization ceiling",{"food":100.0,"material":100.0},[{"food":10.0,"material":10.0},{"material":10.0}],{"claim:0":0.25,"claim:1":0.5})
	check(float(r.grants["claim:0"].food)==2.5 and float(r.grants["claim:1"].material)==5.0,"energy/labor ceilings remain effective")
	r=exact_case("eight-round nonconvergent split",{"food":100.0,"material":100.0},[{"food":10.0,"material":10.0}],{},"split")
	check(int(r.reconcile_rounds)==8 and absf(float(r.grants["claim:0"].food)-10.0*pow(.6,9))<1e-12,"iteration cap recomputes allocation against the final changed bounds")
	simulation_parity()
	var failed=checks.filter(func(x):return not x.pass).size()
	var report={"passed":checks.size()-failed,"failed":failed,"checks":checks}
	FileAccess.open(Paths.file("beta_allocation.json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("BETA ALLOCATION ",checks.size()-failed," passed, ",failed," failed")
	quit(0 if failed==0 else 1)
