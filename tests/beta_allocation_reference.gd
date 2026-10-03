extends RefCounted
## Frozen pre-optimization algorithm for regression parity, not game logic.
const Base=preload("res://core/allocation.gd")
const R=preload("res://core/rules.gd")
static func feasible_resources(s:Dictionary,compiled:Dictionary,claims:Dictionary,access:Dictionary,ceilings:Dictionary) -> Dictionary:
	var bounded=claims.duplicate(true)
	for cid in R.ids(bounded):
		for resource in bounded[cid].needs:
			bounded[cid].needs[resource]=float(claims[cid].needs[resource])*float(ceilings.get(cid,1.))
	var allocation={}
	var rounds=0
	for iteration in 8:
		rounds=iteration+1
		allocation=Base.resources(s,compiled,bounded,access)
		var changed=false
		for cid in R.ids(bounded):
			# Lower only quantities blocked by a DIFFERENT required ingredient.
			# A one-resource consumer can claim the released stock in the next pass.
			for resource in R.ids(bounded[cid].needs):
				var limit=float(ceilings.get(cid,1.))
				for other in claims[cid].needs:
					if other==resource or float(claims[cid].needs[other])<=0:continue
					limit=minf(limit,float(allocation.grants[cid].get(other,0))/float(claims[cid].needs[other]))
				var reduced=minf(float(bounded[cid].needs[resource]),float(claims[cid].needs[resource])*limit)
				if reduced<float(bounded[cid].needs[resource])-.0000001:
					bounded[cid].needs[resource]=reduced;changed=true
		if not changed:break
	allocation=Base.resources(s,compiled,bounded,access)
	allocation.reconcile_rounds=rounds
	return allocation
