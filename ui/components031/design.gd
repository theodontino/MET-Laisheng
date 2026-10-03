extends RefCounted
## 0.3.1 presentation-only dictionary. Never determines eligibility or economics.
static var tokens:Dictionary={}
static var buildings:Dictionary={}
static var cards:Dictionary={}
static var warnings:Array=[]
const W=preload("res://ui/widgets.gd")
static func init():
	if not tokens.is_empty():return
	tokens=_json040("ui_tokens.json")
	buildings=_json("building_visual_domains031.json").get("buildings",{})
	var current=JSON.parse_string(FileAccess.get_file_as_string("res://content/040/buildings.json"))
	if current is Dictionary:
		for id in current:buildings[id]={"domain":current[id].get("domain","general")}
	for card in _json("bonus_card_catalog031.json").get("cards",[]):cards[card.bonus_id]=card
static func _json040(file:String) -> Dictionary:
	var value=JSON.parse_string(FileAccess.get_file_as_string("res://content/presentation040/"+file))
	return value if value is Dictionary else {}
static func _json(file:String) -> Dictionary:
	var value=JSON.parse_string(FileAccess.get_file_as_string("res://content/presentation031/"+file))
	return value if value is Dictionary else {}
static func color(key:String) -> Color:
	init();return Color(tokens.surfaces.get(key,W.semantic_color(key)))
static func domain(key:String) -> Dictionary:
	init();var visual_key="residential" if key=="community" else key;return tokens.domains.get(visual_key,tokens.domains.general)
static func building_domain(id:String) -> String:
	init();var key=str(buildings.get(id,{}).get("domain","general"));return "residential" if key=="community" else key
static func quantity(key:String) -> Dictionary:
	init();return tokens.quantities.get(key,{"label":key,"ink":"#A7B8B7","tint":"#2A3C43","icon":key,"unit":""})
static func status(code:String) -> Dictionary:
	init();return tokens.statuses.get(code,tokens.statuses.unknown)
static func icon_id(id:String) -> String:
	init();return tokens.get("icon_aliases",{}).get(id,id)
static func slot_label(type:String) -> String:
	init();return tokens.slots.get(type,{"label":"弹性"}).label
static func card(g,id:String) -> Dictionary:
	init();var definition=g.c.bonuses.get(id,{})
	var data=cards.get(id,{}).duplicate(true)
	if data.is_empty():return {"bonus_id":id,"name":definition.get("name",id),"visual_domain":"general","effect_rows":[],"rules_match":false}
	data.rules_match=data.rules_reference==definition.get("rules",[])
	data.name=definition.name
	# Fail visibly rather than display stale percentages if content changes later.
	if not data.rules_match:
		data.effect_rows=[]
		for text in definition.get("player_highlights",[definition.summary]):data.effect_rows.append({"icon":"info","role":"effect","text":text})
		if not id in warnings:warnings.append(id);push_warning("UI031 rule catalog mismatch: "+id)
	if int(g.s.block.get("blueprint_levels",{}).get(id,0))>0:
		data.effect_rows=[]
		for row in g.Bonus.card(g,id).get("effects",[]):
			data.effect_rows.append({"icon":"info","role":"effect","text":"蓝图单份 · "+str(row.get("label","数值"))+" %.3f %s"%[float(row.get("after",0)),str(row.get("unit",""))]})
	data.allowed_hosts=definition.get("hosts",[]);data.allowed_slots=definition.get("allowed_slots",[])
	return data
static func hero(data:Dictionary) -> String:
	return str(data.get("hero_asset","works.png")).get_file().get_basename()
static func host_label(host:String) -> String:
	return {"building":"建筑","block":"地区","organization":"组织"}.get(host,host)
