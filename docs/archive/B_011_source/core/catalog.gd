extends Resource
## Read-only content catalog; instance state must never mutate these definitions.
var data:Dictionary
var balance:Dictionary
var projects:Dictionary={}
var upgrades:Dictionary={}
var origins:Dictionary={}
var fusions:Dictionary={}
var opportunities:Dictionary={}
func _init() -> void:
	data=JSON.parse_string(FileAccess.get_file_as_string("res://content/catalog.json"))
	balance=JSON.parse_string(FileAccess.get_file_as_string("res://content/balance.json"))
	for pair in [["projects",projects],["upgrades",upgrades],["origins",origins],["fusions",fusions],["opportunities",opportunities]]:
		for d in data[pair[0]]:pair[1][d.id]=d
func tag_name(t:String) -> String:
	return str(data.tags.get(t,t))
func tags_text(tags:Array) -> String:
	var s=[]
	for t in tags:s.append(tag_name(str(t)))
	return " · ".join(s)
