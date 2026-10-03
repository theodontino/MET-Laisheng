extends RefCounted
## Shared archive visuals. Callers supply authoritative values; no Game or RNG access.
const W=preload("res://ui/widgets.gd")
const Art=preload("res://ui/map/art.gd")
const D=preload("res://ui/components031/design.gd")

static func grid(parent:Node,count:int=3) -> GridContainer:
	var node=GridContainer.new();node.columns=maxi(1,count)
	node.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	node.add_theme_constant_override("h_separation",16);node.add_theme_constant_override("v_separation",16)
	parent.add_child(node);return node

static func icon(parent:Node,id:String,extent:float=32,tint:Color=W.ACCENT) -> TextureRect:
	var node=TextureRect.new();var mapped={"currency":"ledger","money":"ledger","finance":"ledger","housing":"home","health":"medical","happiness":"entertainment","jobs":"employment","capacity":"build","construction":"build","time":"clock","theme":"world","bonus":"layers","research":"education"}.get(id,id)
	node.texture=load("res://ui/reset/population.svg") if id=="population" else Art.icon(D.icon_id(str(mapped)),tint)
	node.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;node.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	node.custom_minimum_size=Vector2.ONE*extent;node.size_flags_vertical=Control.SIZE_SHRINK_CENTER
	node.mouse_filter=Control.MOUSE_FILTER_IGNORE;parent.add_child(node);return node

static func card(parent:Node,title:String="",subtitle:String="",icon_id:String="",tint:Color=W.ACCENT) -> VBoxContainer:
	var panel=PanelContainer.new();panel.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	var style=W.box(W.PANEL,W.DIVIDER,12)
	style.content_margin_left=16;style.content_margin_right=16;style.content_margin_top=16;style.content_margin_bottom=16
	panel.add_theme_stylebox_override("panel",style);parent.add_child(panel)
	var body=W.vbox(panel,12);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	if title!="" or subtitle!="" or icon_id!="":
		var heading=W.hbox(body,12);heading.size_flags_horizontal=Control.SIZE_EXPAND_FILL
		if icon_id!="":icon(heading,icon_id,40,tint)
		var words=W.vbox(heading,4);words.size_flags_horizontal=Control.SIZE_EXPAND_FILL
		if title!="":words.add_child(W.label(title,19,W.TEXT))
		if subtitle!="":words.add_child(W.label(subtitle,14,W.MUTED))
	return body

static func _track(parent:Node,ratio:float,tint:Color) -> ProgressBar:
	var bar=ProgressBar.new();bar.min_value=0;bar.max_value=1;bar.value=clampf(ratio,0,1);bar.show_percentage=false
	bar.custom_minimum_size.y=8;bar.size_flags_horizontal=Control.SIZE_EXPAND_FILL;bar.mouse_filter=Control.MOUSE_FILTER_IGNORE
	bar.add_theme_stylebox_override("background",W.box(W.BASE,Color.TRANSPARENT,4))
	bar.add_theme_stylebox_override("fill",W.box(tint,Color.TRANSPARENT,4))
	parent.add_child(bar);return bar

static func metric(parent:Node,icon_id:String,label:String,value:String,note:String="",tint:Color=W.ACCENT,ratio:float=-1.) -> VBoxContainer:
	var body=card(parent);body.set_meta("desk_visual","metric")
	var top=W.hbox(body,10)
	if icon_id!="":icon(top,icon_id,28,tint)
	top.add_child(W.label(label,14,W.MUTED))
	body.add_child(W.label(value,30,tint))
	if ratio>=0:_track(body,ratio,tint)
	if note!="":body.add_child(W.label(note,14,W.MUTED))
	return body

static func meter(parent:Node,label:String,value:float,target:float,unit:String="",tint:Color=W.ACCENT,icon_id:String="") -> VBoxContainer:
	var body=W.vbox(parent,7);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL;body.set_meta("desk_visual","meter")
	var row=W.hbox(body,10)
	if icon_id!="":icon(row,icon_id,24,tint)
	var name=W.label(label,15,W.TEXT);row.add_child(name)
	var amount=W.label(("%.1f / %.1f"%[value,target])+((" "+unit) if unit!="" else ""),16,tint,false)
	amount.size_flags_horizontal=Control.SIZE_SHRINK_END;row.add_child(amount)
	_track(body,value/target if target>0 else 0.,tint if target>0 else W.MUTED)
	if target<=0:body.add_child(W.label("暂无有效容量或目标",14,W.MUTED))
	return body

static func badge(parent:Node,text:String,tint:Color=W.ACCENT) -> Label:
	var label=W.label(text,14,tint,false);label.size_flags_horizontal=Control.SIZE_SHRINK_BEGIN
	var style=W.box(Color(tint.r,tint.g,tint.b,.10),Color(tint.r,tint.g,tint.b,.35),5)
	style.content_margin_left=9;style.content_margin_right=9;style.content_margin_top=4;style.content_margin_bottom=4
	label.add_theme_stylebox_override("normal",style);parent.add_child(label);return label

static func flow(parent:Node,items:Array,caption:String="") -> Control:
	var body=W.vbox(parent,10);body.size_flags_horizontal=Control.SIZE_EXPAND_FILL;body.set_meta("desk_visual","flow")
	var row=W.hbox(body,12);row.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	for index in items.size():
		if index>0:
			var arrow=W.label("→",28,W.MUTED,false);arrow.size_flags_horizontal=Control.SIZE_SHRINK_CENTER;arrow.size_flags_vertical=Control.SIZE_SHRINK_CENTER;row.add_child(arrow)
		var item:Dictionary=items[index];var tint:Color=item.get("color",W.ACCENT)
		var cell=card(row);cell.set_meta("desk_visual","flow_node")
		var center=CenterContainer.new();cell.add_child(center)
		if str(item.get("id",""))!="":icon(center,str(item.id),40,tint)
		var label=W.label(str(item.get("label","")),15,W.MUTED);label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;cell.add_child(label)
		var value=W.label(str(item.get("value","")),23,tint);value.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;cell.add_child(value)
		if str(item.get("note",""))!="":
			var note=W.label(str(item.note),14,W.MUTED);note.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;cell.add_child(note)
	if caption!="":body.add_child(W.label(caption,14,W.MUTED))
	return body

static func fold(parent:Node,title:String,summary:String="",opened:bool=false) -> VBoxContainer:
	var wrapper=W.vbox(parent,8);wrapper.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	var toggle=Button.new();toggle.toggle_mode=true;toggle.button_pressed=opened;toggle.alignment=HORIZONTAL_ALIGNMENT_LEFT
	toggle.custom_minimum_size.y=42;toggle.size_flags_horizontal=Control.SIZE_EXPAND_FILL;toggle.text=("▾ " if opened else "▸ ")+title
	toggle.tooltip_text=summary;toggle.set_meta("desk_visual","disclosure");wrapper.add_child(toggle)
	if summary!="":wrapper.add_child(W.label(summary,14,W.MUTED))
	var body=W.vbox(wrapper,10);body.visible=opened;body.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	toggle.toggled.connect(func(value:bool):body.visible=value;toggle.text=("▾ " if value else "▸ ")+title)
	return body

static func building(parent:Node,def_id:String,title:String,subtitle:String="") -> VBoxContainer:
	var body=card(parent);body.set_meta("desk_visual","building")
	var hero=W.hbox(body,18);var picture=TextureRect.new();picture.texture=Art.thumbnail(def_id)
	picture.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;picture.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.custom_minimum_size=Vector2(136,108);picture.mouse_filter=Control.MOUSE_FILTER_IGNORE;hero.add_child(picture)
	var words=W.vbox(hero,6);words.size_flags_horizontal=Control.SIZE_EXPAND_FILL;words.add_child(W.label(title,21))
	if subtitle!="":words.add_child(W.label(subtitle,14,W.MUTED))
	return body

static func steps(parent:Node,labels:Array,current:int) -> HBoxContainer:
	var row=W.hbox(parent,8);row.size_flags_horizontal=Control.SIZE_EXPAND_FILL;row.set_meta("desk_visual","steps")
	for index in labels.size():
		if index>0:
			var line=ColorRect.new();line.color=W.ACCENT if index<=current else W.DIVIDER;line.custom_minimum_size=Vector2(22,2);line.size_flags_vertical=Control.SIZE_SHRINK_CENTER;line.mouse_filter=Control.MOUSE_FILTER_IGNORE;row.add_child(line)
		var cell=W.vbox(row,7);cell.size_flags_horizontal=Control.SIZE_EXPAND_FILL
		var tint=W.ACCENT if index<=current else W.MUTED
		var number=W.label("✓" if index<current else "%02d"%(index+1),25,tint,false);number.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;cell.add_child(number)
		var label=W.label(str(labels[index]),15,W.TEXT if index==current else tint);label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;cell.add_child(label)
	return row
