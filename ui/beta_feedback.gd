extends Control
## Native map feedback; drawing and opening history never consume gameplay RNG.
const Feedback=preload("res://core/feedback.gd")
const W=preload("res://ui/widgets.gd")
signal source_clicked(source:Dictionary)
var model=Feedback.new()
var game
var map_view
var bubbles_enabled=true
var _data:Dictionary={}
var _markers:Dictionary={}
var _signature=""
var _elapsed=0.0

func _ready() -> void:
	mouse_filter=Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	clip_contents=true

func configure(g,map_control) -> void:
	game=g;map_view=map_control
	map_view.beta_feedback_active=true
	refresh(g)

func toggle_bubbles(enabled:bool) -> void:
	bubbles_enabled=enabled;_signature=""
	if game!=null:refresh(game)

func history() -> Array:
	return model.history()

func summary() -> Dictionary:
	return _data.duplicate(true)

func refresh(g,snapshot:Dictionary={}) -> void:
	game=g
	_data=model.refresh(g,Time.get_ticks_msec()/1000.0,snapshot)
	var rows:Array=[]
	for item in _data.get("alerts",[]):
		if int(item.get("severity",0))<2:continue
		var row:Dictionary=item.duplicate(true);row.presentation="alert";rows.append(row)
	# Narrative messages stay in the city archive; L1 is reserved for actionable state.
	var capstone:Dictionary=g.s.get("block",{}).get("capstone",{})
	if not capstone.is_empty():
		rows.append({"id":"capstone-marker","source_id":str(capstone.get("source_id",capstone.get("id",""))),"building_id":"","tile":str(capstone.get("tile_anchor","")),"kind":"capstone","layer":"normal","text":str(capstone.get("name","主题建筑")),"presentation":"capstone"})
	var signature=str(W.text_scale)+JSON.stringify(rows)
	if signature!=_signature:
		_signature=signature
		for button in _markers.values():
			remove_child(button);button.queue_free()
		_markers.clear()
		for row in rows:
			var button=Button.new();button.theme=W.theme()
			button.mouse_filter=Control.MOUSE_FILTER_STOP
			button.set_meta("source",row)
			button.add_theme_font_size_override("font_size",roundi(14*W.text_scale))
			button.custom_minimum_size=Vector2(28,28)*W.text_scale
			button.tooltip_text=str(row.text)+"\n点击查看真实来源"
			if row.presentation=="alert":
				button.text={"energy":"电","resources":"料","labor":"人","food":"粮","storage":"仓"}.get(row.cause,"运" if str(row.cause).begins_with("import_") else "!")
				button.add_theme_color_override("font_color",W.DANGER if int(row.severity)==2 else W.WARNING)
			elif row.presentation=="bubble":
				button.text="「"+str(row.text)+"」";button.add_theme_color_override("font_color",W.TEXT)
			else:button.text=str(row.text);button.add_theme_color_override("font_color",W.ACCENT)
			button.pressed.connect(func():source_clicked.emit(row.duplicate(true)))
			add_child(button);_markers[row.id]=button
	_position_markers()

func _process(delta:float) -> void:
	if game==null or map_view==null:return
	_elapsed+=delta
	if _elapsed>=.25:
		_elapsed=0;refresh(game)
	_position_markers()

func _position_markers() -> void:
	if map_view==null or game==null:return
	var offsets:Dictionary={};var occupied:Array[Rect2]=[]
	var owner=get_parent()
	if owner.has_method("context_obstacles"):
		for rectangle in owner.context_obstacles():occupied.append(rectangle)
	for button in _markers.values():
		var row:Dictionary=button.get_meta("source")
		var tile=str(row.get("tile",""))
		button.visible=game.s.get("phase","")=="play" and game.s.cells.has(tile)
		if not button.visible:continue
		if row.presentation=="bubble" and str(map_view.mode)=="site":button.visible=false;continue
		var anchor=map_view.global_position-global_position+map_view.point(tile)
		var offset=float(offsets.get(tile,0));offsets[tile]=offset+30*W.text_scale
		if row.presentation=="capstone":anchor+=Vector2(-46,-88)*map_view.zoom
		elif row.presentation=="bubble":anchor+=Vector2(-100,-96)*map_view.zoom
		else:anchor+=Vector2(26+offset,-38)*map_view.zoom
		button.reset_size()
		button.position=anchor
		var rect=Rect2(button.position,button.size)
		if not Rect2(Vector2.ZERO,size).encloses(rect):button.visible=false;continue
		if occupied.any(func(existing):return existing.intersects(rect.grow(3))):
			button.visible=false;continue
		occupied.append(rect)
