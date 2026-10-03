extends RefCounted
## One clock service for live play. Local time is Earth-equivalent seconds;
## legacy simulation rate fields use economic months. UTC never runs gameplay.

static func ensure(g) -> void:
	if not g.s.get("beta_time") is Dictionary:
		g.s.beta_time={"local_elapsed_seconds":float(g.s.get("local_hours",0))*float(g.c.beta.units.earth_seconds_per_month),
			"active_real_seconds":0.0,"offline_recorded_seconds":0.0,"mode":"normal","pause_reason":"",
			"last_seen_utc":float(g.s.get("wall_mark",0)),"segments_committed":0}
	if not g.s.settings.has("user_paused"):g.s.settings.user_paused=false

static func acceleration(g,mode_id:String="") -> float:
	var selected=mode_id if mode_id!="" else str(g.s.get("beta_time",{}).get("mode","normal"))
	for row in g.c.beta.time.modes:
		if str(row.id)==selected:return float(row.acceleration)
	return 1.0

static func set_mode(g,mode_id:String) -> String:
	ensure(g)
	var found=false
	for row in g.c.beta.time.modes:
		if str(row.id)==mode_id:found=true;break
	if not found:return "未知时间档位。"
	if float(g.s.dt)<=0 and mode_id!="normal":return "DT 已耗尽，仍可常速经营或结算后独立重开。"
	if str(g.s.beta_time.mode)!=mode_id:g.revision+=1
	g.s.beta_time.mode=mode_id
	return ""

static func resume(g,utc_now:float) -> float:
	ensure(g)
	if not is_finite(utc_now):return 0.0
	var previous=maxf(float(g.s.beta_time.last_seen_utc),float(g.s.get("wall_mark",0)))
	var elapsed=maxf(0.0,utc_now-previous)
	g.s.beta_time.offline_recorded_seconds=float(g.s.beta_time.offline_recorded_seconds)+elapsed
	g.s.beta_time.last_seen_utc=maxf(previous,utc_now)
	g.s.wall_mark=maxf(float(g.s.get("wall_mark",0)),utc_now)
	g.s.global_now=maxf(float(g.s.get("global_now",0)),utc_now)
	set_mode(g,"normal")
	# A saved annual/theme/ending decision remains a mandatory decision when
	# returning. Generic return copy must not hide its settlement entry point.
	var previous_reason=str(g.s.beta_time.get("pause_reason",""))
	if previous_reason in ["","manual","resume","offline","background","返回城市；准备好后继续"]:
		g.s.beta_time.pause_reason="返回城市；准备好后继续"
	g.s.settings.user_paused=true
	return elapsed

static func calendar(g,seconds:float=-1.0) -> Dictionary:
	if seconds<0:seconds=float(g.s.get("beta_time",{}).get("local_elapsed_seconds",0))
	var units:Dictionary=g.c.beta.units
	var total_days=maxf(0,seconds)/float(units.earth_seconds_per_laisheng_day)
	var whole_days=int(floor(total_days+1e-10))
	var days_per_year=int(units.days_per_year)
	var days_per_month=int(units.days_per_month)
	var clock=(total_days-floor(total_days))*float(units.civil_hours_per_day)
	return {"year":whole_days/days_per_year+1,"month":(whole_days%days_per_year)/days_per_month+1,
		"day":whole_days%days_per_month+1,"hour":int(floor(clock)),"minute":int(floor(fmod(clock,1.0)*60.0)),
		"years":seconds/float(units.earth_seconds_per_year),"months":seconds/float(units.earth_seconds_per_month),"days":total_days}

static func duration(g,seconds:float) -> String:
	if not is_finite(seconds):return "持续稳定"
	var days=maxf(0,seconds)/float(g.c.beta.units.earth_seconds_per_laisheng_day)
	if days>=144:return "%.2f来生年"%(days/144.0)
	if days>=12:return "%.2f来生月"%(days/12.0)
	if days>=1:return "%.2f来生日"%days
	return "%.2f来生时"%(days*24.0)

static func summary(g) -> Dictionary:
	var clock:Dictionary=g.s.get("beta_time",{})
	var date=calendar(g)
	return {"mode":str(clock.get("mode","normal")),"acceleration":acceleration(g),
		"date":"第%d年 · %d月%d日"%[date.year,date.month,date.day],"calendar":date,
		"dt":float(g.s.get("dt",0)),"dt_years":float(g.s.get("dt",0))*float(g.c.beta.time.earth_seconds_per_dt)/float(g.c.beta.units.earth_seconds_per_year),
		"paused":bool(g.s.get("settings",{}).get("user_paused",false)),"pause_reason":str(clock.get("pause_reason","")),
		"local_elapsed_seconds":float(clock.get("local_elapsed_seconds",0)),"active_real_seconds":float(clock.get("active_real_seconds",0)),
		"offline_recorded_seconds":float(clock.get("offline_recorded_seconds",0))}

static func _next_period(now:float,period:float) -> float:
	var result=(floor(now/period+1e-12)+1.0)*period-now
	return result if result>0 else period

static func _boundary_seconds(g,limit:float) -> float:
	var month=float(g.c.beta.units.earth_seconds_per_month)
	var now=float(g.s.beta_time.local_elapsed_seconds)
	var day=float(g.c.beta.units.earth_seconds_per_laisheng_day)
	# Align the upper bound to civil days, while allowing arbitrarily small
	# real-time submissions. Annual and two-year decisions cannot be skipped.
	var result=minf(limit,_next_period(now,day*float(g.c.beta.time.max_economic_step_laisheng_days)))
	result=minf(result,_next_period(now,float(g.c.beta.units.earth_seconds_per_year)))
	var arrival=(int(g.s.event_cursor)+1)*float(g.c.beta.economy_candidates.arrival_interval_months)*month-now
	if arrival>0:result=minf(result,arrival)
	var capacity=g.Construction.capacity(g)*clampf(float(g.s.metrics.get("energy_ratio",1)),0,1)
	if capacity>0:
		# The stable priority queue gives its head all available construction.
		# Material shortage has its own earlier boundary below.
		for row in g.Construction.queue(g):
			if row.paused:continue
			var work=float(row.work)
			if work>0:result=minf(result,work/capacity*month)
			var material_rate=float(row.material)/maxf(1e-15,work)*capacity
			var material=g.inventory_total("material")
			if material_rate>0 and material>1e-9:result=minf(result,material/material_rate*month)
			break
	# Measured actual net resource flow includes construction and market receipts.
	# A stock already at zero/full is a constrained operating state, not a zero
	# length boundary that would prevent further simulation.
	for resource in ["food","material","goods"]:
		var flow=float(g.s.metrics.get("flows",{}).get(resource,0))
		var stock=g.inventory_total(resource)
		if flow< -1e-9 and stock>1e-9:result=minf(result,stock/-flow*month)
		if flow>1e-9:
			var room=g.Market._buy_room(g,resource)
			if room>1e-9:result=minf(result,room/flow*month)
	if g.Themes.has_method("next_boundary_seconds"):
		var theme_boundary=float(g.Themes.next_boundary_seconds(g))
		if theme_boundary>0:result=minf(result,theme_boundary)
	if g.Cycles.has_method("next_boundary_seconds"):
		var cycle_boundary=float(g.Cycles.next_boundary_seconds(g))
		if cycle_boundary>0:result=minf(result,cycle_boundary)
	return result

static func advance(g,active_real_seconds:float,mode_id:String="") -> Dictionary:
	ensure(g)
	var result={"advanced":0.0,"local_seconds":0.0,"active_real_seconds":0.0,"spent":0.0,"steps":0,"reason":""}
	if active_real_seconds<=0 or not is_finite(active_real_seconds) or str(g.s.phase)!="play":
		result.reason="无效推进或当前不能经营。";return result
	if mode_id!="":
		var mode_error=set_mode(g,mode_id)
		if mode_error!="":result.reason=mode_error;return result
	if bool(g.s.settings.user_paused):
		result.reason=str(g.s.beta_time.pause_reason) if str(g.s.beta_time.pause_reason)!="" else "城市已暂停。"
		return result
	var month=float(g.c.beta.units.earth_seconds_per_month)
	var seconds_per_dt=float(g.c.beta.time.earth_seconds_per_dt)
	var remaining=active_real_seconds
	while remaining>0 and str(g.s.phase)=="play" and not bool(g.s.settings.user_paused):
		var speed=acceleration(g)
		if float(g.s.dt)<=0:speed=1.0;g.s.beta_time.mode="normal"
		var available_local=remaining*speed
		var dt_limited=false
		if speed>1.0:
			var budget_local=float(g.s.dt)*seconds_per_dt*speed/(speed-1.0)
			if budget_local<=available_local:available_local=budget_local;dt_limited=true
		var local_seconds=_boundary_seconds(g,available_local)
		if local_seconds<=0 or not is_finite(local_seconds):
			result.reason="时间边界无效；本段未执行。";break
		# A coincident theme/year boundary is found by subtracting elapsed time
		# from a saved deadline, whereas the DT boundary is computed by division.
		# Normalize only floating-point cancellation (nanoseconds), to the exact
		# AFFORDABLE time. This never discards a positive playable DT budget.
		var clock_roundoff=maxf(1e-9,absf(float(g.s.beta_time.local_elapsed_seconds))*4e-16)
		if dt_limited and available_local-local_seconds<=clock_roundoff:local_seconds=available_local
		var last_budget_segment=dt_limited and local_seconds==available_local
		var real_seconds=local_seconds/speed
		var months=local_seconds/month
		var spent=maxf(0,local_seconds-real_seconds)/seconds_per_dt
		# Sim resolves against this unchanged source dictionary and builds its own
		# private next state before swapping g.s. Retain the source for rollback;
		# copying it here as well duplicated the largest per-tick allocation.
		var before:Dictionary=g.s
		var old_compiled:Dictionary=g.compiled
		var old_spatial_cache:Dictionary=g.spatial_cache
		var failure=g.Sim.tick(g,months,{"local_seconds":local_seconds,"external_seconds":real_seconds,"acceleration":speed})
		if failure=="":
			g.s.beta_time.local_elapsed_seconds=float(before.beta_time.local_elapsed_seconds)+local_seconds
			g.s.local_hours=float(g.s.beta_time.local_elapsed_seconds)/month
			g.s.beta_time.active_real_seconds=float(before.beta_time.active_real_seconds)+real_seconds
			g.s.beta_time.segments_committed=int(before.beta_time.get("segments_committed",0))+1
			g.Themes.tick(g,local_seconds)
			g.Cycles.tick(g,local_seconds,float(before.metrics.get("housed",0)))
			if last_budget_segment:spent=float(before.dt);g.s.dt=0.0
			else:g.s.dt=maxf(0,float(before.dt)-spent)
			g.s.totals.dt_used=float(g.s.totals.get("dt_used",0))+spent
			if float(before.dt)>0 and float(g.s.dt)==0:g.Block.on_dt_zero(g)
			failure=g.validate_state(g.s)
		if failure!="":
			g.s=before;g.compiled=old_compiled;g.spatial_cache=old_spatial_cache;g.recording_cycle=false
			g.last_error=failure;result.reason=failure;break
		g.Civic.finalize(g)
		g.last_committed_revision=g.revision
		result.local_seconds+=local_seconds;result.active_real_seconds+=real_seconds
		result.spent+=spent;result.steps+=1
		remaining=maxf(0,remaining-real_seconds)
		# Avoid a double-precision tail being treated as another gameplay turn.
		if remaining<=active_real_seconds*1e-14:remaining=0.0
		if bool(g.s.settings.user_paused):result.reason=str(g.s.beta_time.pause_reason)
	result.advanced=float(result.local_seconds)/month
	return result
