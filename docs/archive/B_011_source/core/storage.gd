extends RefCounted
## Versioned JSON snapshots, checksum for corruption only, never claimed anti-cheat.
## Temp -> backup -> main rename; offers, RNG, and global deadlines are in snapshot.
const SAVE="user://b_population_v2.json"
static func encode(state:Dictionary) -> String:
	var payload=JSON.stringify(state,"",true,true)
	return JSON.stringify({"schema":2,"sha256":payload.sha256_text(),"payload":payload},"\t")
static func decode(text:String) -> Dictionary:
	var parser=JSON.new()
	if parser.parse(text)!=OK:return {"ok":false,"error":"存档JSON无效。"}
	var container=parser.data
	if not container is Dictionary or not container.has("payload"):return {"ok":false,"error":"不是MET First Block存档。"}
	var payload=str(container.payload)
	if payload.sha256_text()!=str(container.get("sha256","")):return {"ok":false,"error":"校验失败，文件可能损坏。"}
	var inner=JSON.new()
	if inner.parse(payload)!=OK:return {"ok":false,"error":"存档内容JSON无效。"}
	var value=inner.data
	if not value is Dictionary:return {"ok":false,"error":"存档内容无效。"}
	return {"ok":true,"state":value}
static func write(state:Dictionary,path:String=SAVE) -> String:
	var text=encode(state)
	var f=FileAccess.open(path+".tmp",FileAccess.WRITE)
	if f==null:return "无法写入存档："+error_string(FileAccess.get_open_error())
	f.store_string(text);f.flush();f.close()
	var check=decode(FileAccess.get_file_as_string(path+".tmp"))
	if not check.ok:return "临时存档校验失败；旧档未动。"
	if FileAccess.file_exists(path):
		if FileAccess.file_exists(path+".bak"):DirAccess.remove_absolute(path+".bak")
		var err=DirAccess.rename_absolute(path,path+".bak")
		if err!=OK:return "备份旧存档失败。"
	var err=DirAccess.rename_absolute(path+".tmp",path)
	if err!=OK:return "切换新存档失败，.bak仍保留。"
	return ""
static func read(path:String=SAVE) -> Dictionary:
	for candidate in [path,path+".bak"]:
		if not FileAccess.file_exists(candidate):continue
		var decoded=decode(FileAccess.get_file_as_string(candidate))
		if decoded.ok:decoded["from_backup"]=(candidate!=path);return decoded
	return {"ok":false,"error":"没有可用存档，或主备份均损坏。"}
