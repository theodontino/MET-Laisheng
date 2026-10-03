extends RefCounted
const TEXT=Color("e3e8da")
const MUTED=Color("93b0a9")
const GOLD=Color("e6be79")
const GREEN=Color("8acbb7")
const RED=Color("e59d89")
const PANEL=Color("16363d")
static func box(color:Color=PANEL,border:Color=Color("315157"),radius:int=8) -> StyleBoxFlat:
	var style=StyleBoxFlat.new();style.bg_color=color
	style.border_color=border;style.set_border_width_all(1);style.set_corner_radius_all(radius)
	style.content_margin_left=12;style.content_margin_right=12;style.content_margin_top=10;style.content_margin_bottom=10
	return style
static func button_box(color:Color,border:Color=Color("375c61")) -> StyleBoxFlat:
	var style=box(color,border,5)
	style.content_margin_top=6;style.content_margin_bottom=6
	style.content_margin_left=9;style.content_margin_right=9
	return style
static func theme() -> Theme:
	var t=Theme.new();var f=SystemFont.new()
	f.font_names=PackedStringArray(["PingFang SC","Noto Sans CJK SC","WenQuanYi Zen Hei","sans-serif"])
	t.default_font=f;t.default_font_size=14
	t.set_color("font_color","Label",TEXT)
	t.set_color("font_color","Button",TEXT)
	t.set_color("font_hover_color","Button",Color("fff1d3"))
	t.set_color("font_disabled_color","Button",Color("617d7b"))
	t.set_stylebox("normal","Button",button_box(Color("23444a")))
	t.set_stylebox("hover","Button",button_box(Color("34565b"),GOLD))
	t.set_stylebox("pressed","Button",button_box(Color("42605c"),GOLD))
	t.set_stylebox("disabled","Button",button_box(Color("183239"),Color("26444a")))
	var focus=box(Color(0,0,0,0),GOLD,5);focus.set_border_width_all(2);t.set_stylebox("focus","Button",focus)
	t.set_stylebox("panel","PanelContainer",box())
	t.set_stylebox("normal","LineEdit",box(Color("0e282e")))
	t.set_color("font_color","LineEdit",TEXT)
	t.set_constant("separation","VBoxContainer",8);t.set_constant("separation","HBoxContainer",8)
	t.set_stylebox("background","ProgressBar",box(Color("0c252b"),Color("25474a"),3))
	t.set_stylebox("fill","ProgressBar",box(GREEN,GREEN,3))
	return t
static func label(text:String,size:int=14,color:Color=TEXT,wrap:bool=true) -> Label:
	var l=Label.new();l.text=text;l.add_theme_font_size_override("font_size",size);l.add_theme_color_override("font_color",color)
	if wrap:l.autowrap_mode=TextServer.AUTOWRAP_WORD_SMART
	l.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	return l
static func button(text:String,fn:Callable,disabled:bool=false,tooltip:String="") -> Button:
	var b=Button.new();b.text=text;b.disabled=disabled;b.tooltip_text=tooltip;b.mouse_default_cursor_shape=Control.CURSOR_POINTING_HAND
	b.custom_minimum_size.y=36;b.pressed.connect(fn)
	return b
static func vbox(parent:Node,separation:int=8) -> VBoxContainer:
	var v=VBoxContainer.new();v.add_theme_constant_override("separation",separation);parent.add_child(v);return v
static func hbox(parent:Node,separation:int=8) -> HBoxContainer:
	var h=HBoxContainer.new();h.add_theme_constant_override("separation",separation);parent.add_child(h);return h
static func panel(parent:Node,color:Color=PANEL) -> PanelContainer:
	var p=PanelContainer.new();p.add_theme_stylebox_override("panel",box(color));parent.add_child(p);return p
static func spacer(parent:Node):
	var c=Control.new();c.size_flags_horizontal=Control.SIZE_EXPAND_FILL;c.size_flags_vertical=Control.SIZE_EXPAND_FILL;parent.add_child(c);return c
static func clear(parent:Node):
	for child in parent.get_children():parent.remove_child(child);child.queue_free()
static func bar(value:float) -> ProgressBar:
	var p=ProgressBar.new();p.min_value=0;p.max_value=1;p.value=clampf(value,0,1);p.show_percentage=false;p.custom_minimum_size.y=7
	return p
static func section(parent:Node,title:String,body:String=""):
	parent.add_child(label(title,13,GOLD))
	if body!="":parent.add_child(label(body,13,MUTED))
