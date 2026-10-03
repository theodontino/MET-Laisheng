extends RefCounted
const Paths=preload("res://tests/paths.gd")
func run() -> int:
	var out=FileAccess.open(Paths.file("GODOT-LICENSE.txt"),FileAccess.WRITE)
	out.store_string(Engine.get_license_text());out.close()
	var notices="Godot Engine " + Engine.get_version_info().string + "\n\nCopyright information\n\n"
	notices+=JSON.stringify(Engine.get_copyright_info(),"\t")+"\n\nLicense texts\n\n"
	for license_name in Engine.get_license_info():notices+=str(license_name)+"\n"+str(Engine.get_license_info()[license_name])+"\n\n"
	out=FileAccess.open(Paths.file("THIRD-PARTY-NOTICES.txt"),FileAccess.WRITE);out.store_string(notices);out.close()
	return 0
