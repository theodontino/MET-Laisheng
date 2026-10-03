extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var script=load("res://tests/acceptance040.gd")
	if script==null or not script.can_instantiate():
		push_error("acceptance040.gd failed to compile")
		quit(1)
		return
	var suite=script.new()
	quit(suite.run())
