extends Resource
## B 0.2 content definitions are read-only. State never lives in this Resource.
var data:Dictionary={}
var balance:Dictionary={}
var projects:Dictionary={}
var bonuses:Dictionary={}
var operators:Dictionary={}
var policies:Dictionary={}
var origins:Dictionary={}
var v040:Dictionary={}
const SLOT_NAMES={"Production":"生产方案","Logistics":"物流方案","Operations":"运营方案","Flex":"弹性方案"}
const RESOURCE_NAMES={"food":"食物","material":"材料","goods":"低级制成品","energy":"能源"}
const SERVICE_NAMES={"medical":"医疗","education":"教育","entertainment":"娱乐","commercial":"消费"}
func _init() -> void:
	data=JSON.parse_string(FileAccess.get_file_as_string("res://content/core020.json"))
	balance=data.balance;projects=data.buildings;bonuses=data.bonuses;operators=data.operators;policies=data.policies
	for o in data.original_origins:origins[o.id]=o
	for name in ["buildings","progression","operators","bonus_economy","commissions","market","policies"]:
		v040[name]=JSON.parse_string(FileAccess.get_file_as_string("res://content/040/"+name+".json"))
	projects=v040.buildings
	data.city_goals=v040.progression.goals
	for id in v040.operators:operators[id].merge(v040.operators[id],true)
	for id in v040.policies:policies[id].merge(v040.policies[id],true)
	for bonus in bonuses.values():bonus.install_gate=bonus.get("gate",{}).duplicate(true)
	data.build="0.4.0-prealpha.2"
func tag_name(t:String) -> String:
	return str({"building.industrial":"工业","building.residential":"住宅","building.medical":"医疗","building.education":"教育","building.agricultural":"农业","building.producer":"生产","building.service":"服务","building.public_service":"公共服务","building.commercial":"商业","building.green":"绿色","building.energy":"能源","building.extractive":"开采","building.logistics":"物流","building.road":"道路接入","tile.waterfront":"水岸","tile.fertile":"肥沃","tile.forest":"森林","tile.highland":"高地","tile.wet":"湿地","tile.water":"水域"}.get(t,t))
func tags_text(tags:Array) -> String:
	var words=PackedStringArray()
	for t in tags:words.append(tag_name(t))
	return " · ".join(words)
