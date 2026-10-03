extends ScrollContainer
## Mouse drag on page background; native touch/wheel scrolling remains intact.
var finger=-1
var finger_origin=Vector2.ZERO
var finger_scroll=0
var finger_dragged=false
var mouse_drag=false
var origin=Vector2.ZERO
var initial_scroll=0
func _ready():
	scroll_deadzone=8;horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;follow_focus=true
func _input(event):
	if not is_visible_in_tree():mouse_drag=false;finger=-1;return
	if event is InputEventScreenTouch:
		if event.pressed and finger<0 and get_global_rect().has_point(event.position):
			if get_v_scroll_bar().get_global_rect().has_point(event.position):return
			finger=event.index;finger_origin=event.position;finger_scroll=scroll_vertical;finger_dragged=false
		elif not event.pressed and event.index==finger:
			if finger_dragged:propagate_notification(NOTIFICATION_SCROLL_END)
			finger=-1;finger_dragged=false
		return
	if event is InputEventScreenDrag and event.index==finger:
		if event.position.distance_to(finger_origin)>8:
			if not finger_dragged:propagate_notification(NOTIFICATION_SCROLL_BEGIN);finger_dragged=true
			scroll_vertical=finger_scroll-int(event.position.y-finger_origin.y)
			get_viewport().set_input_as_handled()
		return
	if event is InputEventMouseButton and event.button_index==MOUSE_BUTTON_LEFT:
		if not event.pressed:mouse_drag=false;return
		if not get_global_rect().has_point(event.position):return
		var control=get_viewport().gui_get_hovered_control()
		while control!=null and control!=self:
			if control is BaseButton or control is LineEdit or control is ScrollBar:return
			control=control.get_parent()
		mouse_drag=true;origin=event.position;initial_scroll=scroll_vertical
	elif event is InputEventMouseMotion and mouse_drag:
		if not (event.button_mask&MOUSE_BUTTON_MASK_LEFT):mouse_drag=false;return
		if event.position.distance_to(origin)>8:
			scroll_vertical=initial_scroll-int(event.position.y-origin.y);get_viewport().set_input_as_handled()
func _gui_input(event):
	if event is InputEventPanGesture:
		scroll_vertical+=int(event.delta.y*24);accept_event()
