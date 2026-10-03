extends RefCounted
## Annual drafts and two-year chapters share one persistent decision queue.
const MONTH_SECONDS = 1555200.0
const YEAR_SECONDS = 18662400.0

static func ensure(g) -> void:
	if not g.s.block.get("cycles") is Dictionary:
		g.s.block.cycles={"year":0,"major_cycle":0,"population_stability":{},"population_awards":[],"history":[],"baseline":{},"pending_summaries":[]}
	var c:Dictionary=g.s.block.cycles
	if c.baseline.is_empty():c.baseline=_snapshot(g)

static func _snapshot(g) -> Dictionary:
	var stock={}
	for r in ["food","material","goods"]:stock[r]=g.inventory_total(r)
	return {"currency":float(g.s.currency),"population":float(g.s.metrics.get("population",0)),"housed":float(g.s.metrics.get("housed",0)),"stock":stock,"pollution":float(g.s.metrics.get("pollution",0)),"built":int(g.s.totals.built)}

static func next_boundary_seconds(g) -> float:
	ensure(g)
	var seconds=float(g.s.get("beta_time",{}).get("local_elapsed_seconds",0))
	var next=(int(g.s.block.cycles.year)+1)*YEAR_SECONDS
	var result=maxf(0,next-seconds)
	var cfg:Dictionary=g.c.beta.get("economy_candidates",{})
	var duration=float(cfg.get("milestone_stability_months",1))*MONTH_SECONDS
	for threshold in cfg.get("milestone_housed_people",[32,64,128,256]):
		var key=str(int(threshold))
		if key in g.s.block.cycles.population_awards:continue
		if float(g.s.metrics.get("housed",0))>=float(threshold):
			result=minf(result,maxf(0,duration-float(g.s.block.cycles.population_stability.get(key,0))))
	return result

static func tick(g,local_seconds:float,previous_housed:float=-1.0) -> void:
	ensure(g)
	var c:Dictionary=g.s.block.cycles
	var elapsed=float(g.s.get("beta_time",{}).get("local_elapsed_seconds",0))
	var cfg:Dictionary=g.c.beta.get("economy_candidates",{})
	var duration=float(cfg.get("milestone_stability_months",1))*MONTH_SECONDS
	for threshold in cfg.get("milestone_housed_people",[32,64,128,256]):
		var key=str(int(threshold))
		if key in c.population_awards:continue
		var stable=float(c.population_stability.get(key,0))
		var housed=float(g.s.metrics.get("housed",0))
		var started_housed=housed if previous_housed<0 else previous_housed
		# Arrivals/rehousing at the END of this segment must not earn its past time.
		stable=stable+local_seconds if housed+1e-8>=float(threshold) and started_housed+1e-8>=float(threshold) else 0.0
		c.population_stability[key]=stable
		if stable+1e-6>=duration:
			c.population_awards.append(key)
			g.Bonus.draw(g,"population:"+key,[],{"kind":"copy"})
			g.log_event("population_milestone",{"threshold":threshold},"%d位居民稳定安居，获得一份构筑机会。"%int(threshold))
	while elapsed+1e-6>=(int(c.year)+1)*YEAR_SECONDS:
		c.year=int(c.year)+1
		var now=_snapshot(g)
		var row={"year":c.year,"local_elapsed_seconds":int(c.year)*YEAR_SECONDS,"before":c.baseline.duplicate(true),"after":now,"currency_delta":float(now.currency)-float(c.baseline.currency),"population_delta":float(now.population)-float(c.baseline.population),"major_cycle":false}
		g.Bonus.draw(g,"annual:"+str(c.year),[],{"kind":"copy"})
		var cycle_years=maxi(1,int(g.c.beta.get("time",{}).get("major_cycle_years",2)))
		if int(c.year)%cycle_years==0:
			c.major_cycle=int(c.year)/cycle_years
			row.major_cycle=true
			g.Themes.on_major_cycle(g,int(c.major_cycle))
		c.history.append(row);c.pending_summaries.append(row.duplicate(true));c.baseline=now
		if c.history.size()>100:c.history.pop_front()
		g.s.settings.user_paused=true
		g.s.beta_time.mode="normal"
		g.s.beta_time.pause_reason="annual"
		g.log_event("annual",{"year":c.year,"major_cycle":row.major_cycle},"来生第%d年结束：查看城市变化与年度构筑机会。"%int(c.year))

static func acknowledge(g) -> void:
	ensure(g)
	g.s.block.cycles.pending_summaries.clear()

static func view(g) -> Dictionary:
	ensure(g)
	var result:Dictionary=g.s.block.cycles.duplicate(true)
	result.next_year_seconds=maxf(0,(int(result.year)+1)*YEAR_SECONDS-float(g.s.beta_time.local_elapsed_seconds))
	return result
