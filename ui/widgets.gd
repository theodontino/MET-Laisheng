extends RefCounted
const Art = preload("res://ui/map/art.gd")

const BASE = Color("182125")
const SURFACE = Color("222D32")
const RAISED = Color("334249")
const TEXT = Color("F1E8D8")
const MUTED = Color("B8B7AF")
const ACCENT = Color("79C9B4")
const WARNING = Color("E0B56D")
const DANGER = Color("E58C83")
const SUCCESS = Color("91C6A1")
const INFO = Color("9FC2D9")
const OUTLINE = Color("587178")
const DIVIDER = Color("3B4D52")
const FOCUS = Color("8AD5C0")

# Kept as aliases so 0.3.x and early 0.4 screens keep their public API.
const GOLD = WARNING
const GREEN = SUCCESS
const RED = DANGER
const PANEL = SURFACE
const SOFT = RAISED

static var tokens: Dictionary = {}
static var text_scale: float = 1.0
static var touch: bool = false
static var high_contrast: bool = false
static var _font_source: Font
static var _font_source_is_variable: bool = false
static var _regular_font: Font
static var _semibold_font: Font

static func _load_tokens() -> void:
	if not tokens.is_empty():
		return
	var path = "res://content/presentation040/ui_tokens.json"
	if FileAccess.file_exists(path):
		var loaded = JSON.parse_string(FileAccess.get_file_as_string(path))
		if loaded is Dictionary:
			tokens = loaded

static func _source_font() -> Font:
	if _font_source != null:
		return _font_source
	var path = str(tokens.get("typography", {}).get("font_regular", "res://assets/fonts/NotoSansSC-VF.ttf"))
	var loaded = load(path) if ResourceLoader.exists(path) else null
	if loaded is FontFile:
		_font_source = loaded
		_font_source_is_variable = true
	else:
		var fallback = SystemFont.new()
		fallback.font_names = PackedStringArray(["Noto Sans SC", "Source Han Sans SC", "Microsoft YaHei", "sans-serif"])
		_font_source = fallback
	return _font_source

static func _font_at_weight(weight: float) -> Font:
	var source = _source_font()
	if not _font_source_is_variable:
		return source
	var variation = FontVariation.new()
	variation.base_font = source
	# Godot resolves this font's OpenType wght axis under its canonical name "weight".
	variation.variation_opentype = {"weight": weight}
	return variation

static func _regular() -> Font:
	if _regular_font == null:
		_regular_font = _font_at_weight(float(tokens.get("typography", {}).get("font_weight_regular", 400)))
	return _regular_font

static func _semibold() -> Font:
	if _semibold_font != null:
		return _semibold_font
	_semibold_font = _font_at_weight(float(tokens.get("typography", {}).get("font_weight_semibold", 600)))
	return _semibold_font

static func semantic_color(name: String) -> Color:
	_load_tokens()
	var key = name.to_lower()
	var surfaces: Dictionary = tokens.get("surfaces", {})
	if surfaces.has(key):
		return Color(surfaces[key])
	var aliases = {
		"base": BASE, "canvas": BASE, "background": BASE,
		"surface": SURFACE, "panel": SURFACE, "card": Color("29363B"),
		"raised": RAISED, "inset": Color("1C272C"),
		"text": TEXT, "ink": TEXT, "muted": MUTED, "secondary_text": MUTED,
		"accent": ACCENT, "focus": FOCUS, "selection": FOCUS,
		"warning": WARNING, "gold": WARNING, "danger": DANGER, "red": DANGER,
		"success": SUCCESS, "green": SUCCESS, "info": INFO,
		"outline": OUTLINE, "border": OUTLINE, "divider": DIVIDER,
		"disabled": Color("7C8B98"), "on_accent": Color("152D29")
	}
	if aliases.has(key):
		return aliases[key]
	return TEXT

static func surface_color(name: String) -> Color:
	return semantic_color(name)

static func _legacy_fill(color: Color) -> Color:
	var key = color.to_html(false).to_lower()
	var fills = {
		"e8eddf": BASE, "172329": BASE,
		"f7f6f0": SURFACE, "f7f4ed": SURFACE, "fffdf7": SURFACE,
		"202f36": SURFACE,
		"ffffff": RAISED, "e9ede6": RAISED, "eef4e9": RAISED, "e4efe7": RAISED,
		"ecf0f1": RAISED, "e6ece7": RAISED, "dae6dc": RAISED, "dae3dc": RAISED,
		"fff4e5": Color("352B1D"), "fff1d8": Color("352B1D"), "fff2d9": Color("352B1D"),
		"fbe9e9": Color("39272A"), "f9ebe3": Color("3A2D29"), "e6f2ea": Color("26372D"),
		"17665a": Color("26372D"), "213e35": BASE, "203e33": BASE, "2a4b40": RAISED,
		"2a3c43": RAISED, "24373e": Color("29363B"), "34464d": DIVIDER,
		"365e4f": Color("2D4051"), "e8f3eb": Color("26372D"), "ecf4e6": Color("2B3128"),
		"eaf0f8": Color("263747"), "f0f3e1": Color("303228"), "fff3d1": Color("352E21"),
		"e9f2fd": Color("263747"), "f2ebfa": Color("302B3A"), "e5f3f3": Color("26383A"),
		"e8f2f3": Color("26383A"), "e5f4ef": Color("26372D"), "f8eaf0": Color("382A31"),
		"f0ebf7": Color("302B3A"), "fff6d5": Color("353123"), "f5f0de": Color("343127"),
		"fceddf": Color("3A2D29"), "fff7ce": Color("352B1D"), "fff3c3": Color("352B1D"),
		"eff0d7": Color("303228"), "f4ebe3": Color("342E29")
	}
	return fills.get(key, color)

static func _legacy_ink(color: Color) -> Color:
	var key = color.to_html(false).to_lower()
	var inks = {
		"ffffff": TEXT, "edf3ef": TEXT, "213831": TEXT, "18313a": TEXT, "253f35": TEXT,
		"5b6c65": MUTED, "53636a": MUTED, "455860": MUTED, "586761": MUTED,
		"68727a": MUTED, "84959c": MUTED, "819188": MUTED, "c4d7ce": MUTED,
		"8b641d": WARNING, "825918": WARNING, "815515": WARNING, "934b30": WARNING,
		"72500b": WARNING, "806208": WARNING, "75652c": WARNING, "d8b776": WARNING,
		"17665a": SUCCESS, "24634b": SUCCESS, "1d6b60": SUCCESS, "22563c": SUCCESS,
		"34652b": SUCCESS, "335d66": INFO, "a53332": DANGER, "a13439": DANGER,
		"af6543": DANGER, "9d475f": DANGER, "ffb5b5": DANGER, "66498c": INFO, "245184": INFO,
		"475b52": MUTED, "2c8066": ACCENT, "356ea0": INFO, "8f5c38": WARNING,
		"75d7be": ACCENT, "c6a56b": ACCENT
	}
	return inks.get(key, color)

static func _legacy_border(color: Color) -> Color:
	if color == RAISED:
		return Color(0, 0, 0, 0)
	var key = color.to_html(false).to_lower()
	var borders = {
		"cad1ce": DIVIDER, "d3ddd3": DIVIDER, "bbcdbf": DIVIDER,
		"b7c8bb": DIVIDER, "c8d6cc": DIVIDER, "abbcaf": DIVIDER, "34474d": DIVIDER,
		"819188": OUTLINE, "b9d0c5": OUTLINE, "52666d": OUTLINE,
		"172329": Color(0, 0, 0, 0), "f1d3a4": Color("568F82"), "ffffff": Color(0, 0, 0, 0),
		"75d7be": Color("568F82")
	}
	return borders.get(key, color)

static func ink_color(color: Color) -> Color:
	return _legacy_ink(color)

static func fill_color(color: Color) -> Color:
	return _legacy_fill(color)

static func outline_color(color: Color) -> Color:
	return _legacy_border(color)

static func box(color: Color = SURFACE, border: Color = Color(0, 0, 0, 0), radius: int = 12) -> StyleBoxFlat:
	_load_tokens()
	var style = StyleBoxFlat.new()
	style.bg_color = _legacy_fill(color)
	style.border_color = _legacy_border(border)
	style.set_border_width_all(1 if border.a > 0.0 else 0)
	style.set_corner_radius_all(clampi(radius, 0, int(tokens.get("radius", {}).get("panel", 6))))
	style.content_margin_left = 12
	style.content_margin_right = 12
	style.content_margin_top = 10
	style.content_margin_bottom = 10
	return style

static func button_box(color: Color = RAISED, border: Color = Color(0, 0, 0, 0), radius: int = 8) -> StyleBoxFlat:
	_load_tokens()
	var control_radius = int(tokens.get("radius", {}).get("control", 5))
	var style = box(color, border, mini(radius, control_radius))
	style.content_margin_top = 8
	style.content_margin_bottom = 8
	style.content_margin_left = 12
	style.content_margin_right = 12
	return style

static func _focus_box(radius: int = 8) -> StyleBoxFlat:
	_load_tokens()
	var style = box(Color(0, 0, 0, 0), FOCUS, mini(radius, int(tokens.get("radius", {}).get("control", 5))))
	style.bg_color = Color(0, 0, 0, 0)
	style.set_border_width_all(int(tokens.get("buttons", {}).get("focus_width", 2)))
	return style

static func theme() -> Theme:
	_load_tokens()
	var t = Theme.new()
	t.default_font = _regular()
	t.default_font_size = int(float(tokens.get("typography", {}).get("body", 16)) * text_scale)
	for cls in ["Label", "Button", "CheckButton", "OptionButton", "LineEdit", "TextEdit", "RichTextLabel"]:
		t.set_color("font_color", cls, TEXT)
	t.set_color("font_hover_color", "Button", TEXT)
	t.set_color("font_pressed_color", "Button", TEXT)
	t.set_color("font_focus_color", "Button", TEXT)
	t.set_color("font_disabled_color", "Button", MUTED)
	t.set_color("font_hover_pressed_color", "Button", TEXT)
	t.set_color("icon_normal_color", "Button", MUTED)
	t.set_color("icon_hover_color", "Button", ACCENT)
	t.set_color("icon_pressed_color", "Button", TEXT)
	t.set_color("icon_focus_color", "Button", ACCENT)
	t.set_color("icon_hover_pressed_color", "Button", TEXT)
	t.set_color("icon_disabled_color", "Button", MUTED)
	t.set_color("font_placeholder_color", "LineEdit", MUTED)
	t.set_color("caret_color", "LineEdit", TEXT)
	t.set_color("selection_color", "LineEdit", Color("35574F"))
	t.set_stylebox("normal", "Button", button_box(RAISED))
	var hover = button_box(Color("40545C"), OUTLINE)
	t.set_stylebox("hover", "Button", hover)
	t.set_stylebox("pressed", "Button", button_box(Color("1C272C"), Color("568F82")))
	t.set_stylebox("disabled", "Button", button_box(Color("273238"), DIVIDER))
	t.set_stylebox("focus", "Button", _focus_box())
	t.set_stylebox("normal", "CheckButton", button_box(RAISED))
	t.set_stylebox("hover", "CheckButton", hover)
	t.set_stylebox("pressed", "CheckButton", button_box(Color("2C302F"), ACCENT))
	t.set_stylebox("disabled", "CheckButton", button_box(Color("273238"), DIVIDER))
	t.set_stylebox("focus", "CheckButton", _focus_box())
	t.set_stylebox("normal", "OptionButton", button_box(RAISED))
	t.set_stylebox("hover", "OptionButton", hover)
	t.set_stylebox("pressed", "OptionButton", button_box(Color("1C272C"), Color("568F82")))
	t.set_stylebox("disabled", "OptionButton", button_box(Color("273238"), DIVIDER))
	t.set_stylebox("focus", "OptionButton", _focus_box())
	t.set_stylebox("normal", "LineEdit", box(Color("1C272C"), OUTLINE, 5))
	t.set_stylebox("focus", "LineEdit", _focus_box())
	t.set_stylebox("read_only", "LineEdit", box(SURFACE))
	t.set_stylebox("panel", "PanelContainer", box(SURFACE))
	t.set_stylebox("background", "ProgressBar", box(Color("1C272C"), Color(0, 0, 0, 0), 4))
	t.set_stylebox("fill", "ProgressBar", box(ACCENT, Color(0, 0, 0, 0), 4))
	t.set_font("font", "Button", _semibold())
	t.set_constant("separation", "VBoxContainer", 8)
	t.set_constant("separation", "HBoxContainer", 8)
	return t

static func label(text: String, size: int = 16, color: Color = TEXT, wrap: bool = true) -> Label:
	_load_tokens()
	var l = Label.new()
	var base_size = maxi(14, size)
	l.set_meta("font_base", base_size)
	l.text = text
	l.add_theme_font_size_override("font_size", int(base_size * text_scale))
	var ink = _legacy_ink(color)
	if high_contrast and (color == MUTED or ink == MUTED):
		ink = TEXT
	l.add_theme_color_override("font_color", ink)
	if base_size >= 22:
		l.add_theme_font_override("font", _semibold())
	if wrap:
		l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l

static func title(text: String, color: Color = TEXT) -> Label:
	_load_tokens()
	var heading = label(text, int(tokens.get("typography", {}).get("panel_title", 24)), color)
	heading.add_theme_font_override("font", _semibold())
	return heading

static func metric(text: String, color: Color = TEXT, wrap: bool = false) -> Label:
	_load_tokens()
	return label(text, int(tokens.get("typography", {}).get("metric", 28)), color, wrap)

static func button(text: String, fn: Callable, disabled: bool = false, tooltip: String = "") -> Button:
	_load_tokens()
	var b = Button.new()
	var button_size = int(tokens.get("typography", {}).get("button", 16))
	b.set_meta("font_base", button_size)
	b.clip_text = true
	b.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	b.text = text
	b.disabled = disabled
	b.tooltip_text = tooltip
	b.mouse_default_cursor_shape = Control.CURSOR_ARROW if disabled else Control.CURSOR_POINTING_HAND
	var longest = 0.0
	for line in text.split("\n"):
		var width = 0.0
		for i in line.length():
			width += (button_size if line.unicode_at(i) > 255 else button_size * 0.55) * text_scale
		longest = maxf(longest, width)
	b.custom_minimum_size.x = clampf(longest + 32.0, 48.0, 360.0)
	var min_height = int(tokens.get("buttons", {}).get("touch_min_height", 48) if touch else tokens.get("buttons", {}).get("desktop_min_height", 40))
	b.custom_minimum_size.y = maxf(min_height * text_scale, button_size * text_scale + 18.0)
	b.pressed.connect(fn)
	b.add_theme_font_size_override("font_size", int(button_size * text_scale))
	b.add_theme_font_override("font", _semibold())
	return b

static func secondary(b: Button) -> Button:
	b.add_theme_stylebox_override("normal", button_box(RAISED))
	b.add_theme_stylebox_override("hover", button_box(Color("40545C"), OUTLINE))
	b.add_theme_stylebox_override("pressed", button_box(Color("1C272C"), Color("568F82")))
	b.add_theme_stylebox_override("disabled", button_box(Color("273238"), DIVIDER))
	b.add_theme_stylebox_override("focus", _focus_box())
	return b

static func primary(b: Button) -> Button:
	b.add_theme_stylebox_override("normal", button_box(ACCENT, Color("508F81")))
	b.add_theme_stylebox_override("hover", button_box(Color("96DCC8"), Color("A9E9D7")))
	b.add_theme_stylebox_override("pressed", button_box(Color("5FAF9A"), Color("488674")))
	b.add_theme_stylebox_override("disabled", button_box(Color("39434A"), DIVIDER))
	b.add_theme_stylebox_override("focus", _focus_box())
	for key in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
		b.add_theme_color_override(key, Color("152D29"))
	b.add_theme_color_override("font_disabled_color", MUTED)
	for key in ["icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_focus_color", "icon_hover_pressed_color"]:
		b.add_theme_color_override(key, Color("152D29"))
	b.add_theme_color_override("icon_disabled_color", MUTED)
	return b

static func ghost(b: Button) -> Button:
	b.add_theme_stylebox_override("normal", button_box(Color(0, 0, 0, 0)))
	b.add_theme_stylebox_override("hover", button_box(RAISED))
	b.add_theme_stylebox_override("pressed", button_box(Color("1C272C"), Color("568F82")))
	b.add_theme_stylebox_override("disabled", button_box(Color(0, 0, 0, 0)))
	b.add_theme_stylebox_override("focus", _focus_box())
	return b

static func flat(b: Button) -> Button:
	return ghost(b)

static func icon_button(id: String, text: String, fn: Callable, tip: String = "") -> Button:
	var b = button(text, fn, false, tip)
	b.icon = Art.icon(id, Color.WHITE)
	b.expand_icon = true
	b.add_theme_constant_override("icon_max_width", 22)
	if text != "":
		b.custom_minimum_size.x += 28
	return b

static func vbox(parent: Node, separation: int = 8) -> VBoxContainer:
	var v = VBoxContainer.new()
	v.add_theme_constant_override("separation", separation)
	parent.add_child(v)
	return v

static func hbox(parent: Node, separation: int = 8) -> HBoxContainer:
	var h = HBoxContainer.new()
	h.add_theme_constant_override("separation", separation)
	parent.add_child(h)
	return h

static func panel(parent: Node, color: Color = SURFACE) -> PanelContainer:
	var p = PanelContainer.new()
	p.add_theme_stylebox_override("panel", box(color))
	parent.add_child(p)
	return p

static func spacer(parent: Node) -> Control:
	var c = Control.new()
	c.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	c.size_flags_vertical = Control.SIZE_EXPAND_FILL
	c.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(c)
	return c

static func clear(parent: Node) -> void:
	for child in parent.get_children():
		parent.remove_child(child)
		child.queue_free()

static func bar(value: float) -> ProgressBar:
	var p = ProgressBar.new()
	p.min_value = 0
	p.max_value = 1
	p.value = clampf(value, 0, 1)
	p.show_percentage = false
	p.custom_minimum_size.y = 7
	return p

static func section(parent: Node, heading: String, body: String = "") -> void:
	var heading_label = label(heading, 16, ACCENT)
	heading_label.add_theme_font_override("font", _semibold())
	parent.add_child(heading_label)
	var rule = ColorRect.new()
	var rule_color = ACCENT
	rule_color.a = 0.28
	rule.color = rule_color
	rule.custom_minimum_size.y = 1
	rule.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	rule.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(rule)
	if body != "":
		parent.add_child(label(body, 16, MUTED))
