extends RefCounted
## Aggregate by resource+interface signature, never by producer or route.
## This is not a provenance graph. Stored batches retain compatibility traits.
static func signature(resource:String,tags:Array) -> String:
	return resource+"|" # schema 2: resource interfaces are dormant
static func add(stock:Dictionary,resource:String,tags:Array,qty:float,cap:float) -> float:
	var accepted=maxf(0.0,minf(qty,cap-total(stock,resource)))
	var k=signature(resource,tags)
	stock[k]=float(stock.get(k,0.0))+accepted
	return accepted
static func matches(k:String,resource:String,required:Array) -> bool:
	return k.split("|")[0]==resource # Ignore legacy interface arguments in this ruleset.
static func total(stock:Dictionary,resource:String,required:Array=[]) -> float:
	var value=0.0
	for k in stock:
		if matches(k,resource,required): value+=float(stock[k])
	return value
static func take(stock:Dictionary,resource:String,required:Array,quantity:float) -> Dictionary:
	var result={"taken":0.0,"batches":{}}
	var keys=stock.keys();keys.sort_custom(func(a,b):return a.length()<b.length() if a.length()!=b.length() else a<b)
	for k in keys:
		if not matches(k,resource,required):continue
		var x=minf(float(stock[k]),maxf(0.0,quantity-result.taken))
		if x>0.0:
			stock[k]-=x
			result.batches[k]=x
			result.taken+=x
		if result.taken>=quantity-0.000001:break
	return result
static func refund(stock:Dictionary,batches:Dictionary,fraction:float) -> void:
	for k in batches:stock[k]=float(stock.get(k,0.0))+float(batches[k])*fraction
static func consume_recipe(stock:Dictionary,recipe:Array,scale:float) -> float:
	# Atomic transaction, including overlapping input requirements.
	if recipe.is_empty() or scale<=0:return 1.0
	var ordered=recipe.duplicate(true)
	ordered.sort_custom(func(a,b):return a.tags.size()>b.tags.size())
	var upper=1.0
	for input in ordered:
		var need=float(input.rate)*scale
		if need>0:upper=minf(upper,total(stock,input.resource,input.get("tags",[]))/need)
	upper=clampf(upper,0,1)
	var trial=_try_recipe(stock,ordered,scale*upper)
	if trial.ok:
		stock.clear();stock.merge(trial.stock,true);return upper
	var low=0.0;var high=upper
	for i in 16:
		var middle=(low+high)*.5
		trial=_try_recipe(stock,ordered,scale*middle)
		if trial.ok:low=middle
		else:high=middle
	trial=_try_recipe(stock,ordered,scale*low)
	stock.clear();stock.merge(trial.stock,true)
	return low
static func _try_recipe(stock:Dictionary,recipe:Array,scale:float) -> Dictionary:
	var temp=stock.duplicate()
	for input in recipe:
		var need=float(input.rate)*scale
		var removed=take(temp,input.resource,input.get("tags",[]),need)
		if float(removed.taken)+.00000001<need:return {"ok":false,"stock":temp}
	return {"ok":true,"stock":temp}
