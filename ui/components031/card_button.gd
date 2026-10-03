extends Button
## A real focusable button with independently laid-out image/name/effect children.
## Selecting it previews; only the separate confirmation button buys anything.
const W=preload("res://ui/widgets.gd")
const D=preload("res://ui/components031/design.gd")
var content:VBoxContainer
var selected:bool=false
var calculating:bool=false
func initialize():
	text="";clip_text=true;custom_minimum_size.x=0;size_flags_horizontal=Control.SIZE_EXPAND_FILL
	mouse_default_cursor_shape=Control.CURSOR_POINTING_HAND
	content=VBoxContainer.new();content.add_theme_constant_override("separation",8);content.mouse_filter=Control.MOUSE_FILTER_IGNORE;add_child(content)
	content.minimum_size_changed.connect(request_fit);resized.connect(request_fit)
	apply_style();request_fit()
func request_fit():
	if calculating:return
	calculating=true;call_deferred("fit_content")
func fit_content():
	calculating=false
	if content==null:return
	content.position=Vector2(12,12);content.size.x=maxf(20.,size.x-24.)
	var h=maxf(48.,content.get_combined_minimum_size().y+24.)
	if absf(custom_minimum_size.y-h)>.5:custom_minimum_size.y=h
	content.size.y=maxf(0.,size.y-24.)
func apply_style():
	var normal=W.box(Color("1A2B3D") if selected else W.RAISED,W.ACCENT if selected else W.OUTLINE,5)
	normal.set_border_width_all(2 if selected else 1)
	normal.shadow_color=Color(0,0,0,0.18);normal.shadow_size=2;normal.shadow_offset=Vector2(0,1)
	add_theme_stylebox_override("normal",normal)
	var hover=W.box(Color("2B3D50"),Color("8B744A"),5);hover.set_border_width_all(1);hover.shadow_color=Color(0,0,0,0.2);hover.shadow_size=3;hover.shadow_offset=Vector2(0,2)
	add_theme_stylebox_override("hover",hover)
	var pressed=W.box(Color("101C2A"),Color("806B46"),5);pressed.set_border_width_all(1);add_theme_stylebox_override("pressed",pressed)
	add_theme_stylebox_override("disabled",W.box(Color("1A2735"),W.DIVIDER,5))
	var focus=W.box(Color(0,0,0,0),W.FOCUS,5);focus.bg_color=Color(0,0,0,0);focus.set_border_width_all(2)
	add_theme_stylebox_override("focus",focus)
	add_theme_color_override("font_color",W.TEXT);add_theme_color_override("font_hover_color",W.TEXT)
	add_theme_color_override("font_pressed_color",W.TEXT);add_theme_color_override("font_disabled_color",W.MUTED)
func select(value:bool):
	selected=value;apply_style()
func ignore_children(node:Node=null):
	if node==null:node=content
	if node is Control:node.mouse_filter=Control.MOUSE_FILTER_IGNORE
	for child in node.get_children():ignore_children(child)
