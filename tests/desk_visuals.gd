extends "res://tests/ui_reset.gd"
## Focused L3 evidence reuses native pointer/key helpers and explicit state fixtures.
## Every captured page is opened through an actual visible control.

func finish() -> void:
	var failures=checks.filter(func(row):return not row.pass).size()
	var report={"passed":checks.size()-failures,"failed":failures,"checks":checks,"captures":captures,"input_actions":input_actions,"native_input_actions":input_actions.size(),"fixture_ids":fixture_ids,"evidence_type":"scripted native L3 pointer/key fixtures; no human or natural playthrough claim","display_server":DisplayServer.get_name(),"renderer":RenderingServer.get_video_adapter_name(),"resolution":"1920x1080","source_fingerprint":OS.get_environment("MET_SOURCE_FINGERPRINT"),"command_line":OS.get_cmdline_args()}
	FileAccess.open(Paths.file("desk-visuals.json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("DESK VISUALS RESULT %d passed, %d failed"%[checks.size()-failures,failures]);quit(1 if failures else 0)

func run() -> void:
	app=App.new();app.test_mode=true;app.ignore_save=true;root.add_child(app);app.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);await frames(12)
	app.ignore_save=true;app.test_mode=true;app.feedback_layer.set_process(false)
	check(DisplayServer.get_name()!="headless","native display server is active for L3 evidence")
	check(root.size==Vector2i(1920,1080) and root.mode in [Window.MODE_FULLSCREEN,Window.MODE_EXCLUSIVE_FULLSCREEN],"L3 evidence uses default contracted fullscreen 1920x1080")
	fixture();await frames();await test_details();await test_l3_topics();await test_l3_city();await test_theme_and_alerts();await test_command_failure();finish()
