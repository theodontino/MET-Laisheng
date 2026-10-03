extends RefCounted
static func root() -> String:
	var path=OS.get_environment("MET_EVIDENCE_DIR")
	if path=="":path=ProjectSettings.globalize_path("user://test_results")
	DirAccess.make_dir_recursive_absolute(path)
	return path
static func file(name:String) -> String:return root().path_join(name)
