# 0.3.0基线审查

日期：2026-09-26。只读审查；未修改原游戏，未重跑模拟或手机真机。

源文件：`MET_B_0.2.1_RiverValley_Source.zip`。SHA256：`b5fa2c2a0384c6375b06e3ed5a0742a62ef3e4e9a6818ae793b0df080acbaac8`。

## 画面依据

- `evidence/021-12-town.png`：建成城市；格块分立、整图居中，城市在中下部较小。
- `evidence/021-05-tender.png`：投标；三列文字占据中部并遮罩地图。
- `evidence/021-04-planning.png`：现有预测；已存在功能，0.3.0不是声称从零补预览。

这些判断来自包内帧缓冲图片；不拿美术截图冒充真实触摸测试。


## 版本和显示配置

`project.godot:1–31`

```text
1: ; MET EXP-003 — First Block MVP, isolated from P0 and MET productivity data.
2: config_version=5
3: 
4: [application]
5: config/name="MET · 河谷新章"
6: config/version="0.2.1-rivervalley.1"
7: run/main_scene="res://main.tscn"
8: config/features=PackedStringArray("4.7", "GL Compatibility")
9: config/use_custom_user_dir=true
10: config/custom_user_dir_name="MET-B-Rivervalley-021"
11: 
12: [display]
13: window/size/viewport_width=1440
14: window/size/viewport_height=900
15: window/size/window_width_override=1440
16: window/size/window_height_override=900
17: window/size/min_width=1180
18: window/size/min_height=740
19: window/stretch/mode="canvas_items"
20: window/dpi/allow_hidpi=true
21: 
22: [rendering]
23: textures/vram_compression/import_etc2_astc=true
24: renderer/rendering_method="gl_compatibility"
25: renderer/rendering_method.mobile="gl_compatibility"
26: environment/defaults/default_clear_color=Color(0.039,0.075,0.09,1)
27: textures/default_filters/use_nearest_mipmap_filter=false
28: 
29: [debug]
30: gdscript/warnings/untyped_declaration=0
31: gdscript/warnings/inference_on_variant=0
```


## 地图镜头与输入

`ui/map_view.gd:27–64`

```text
27: func _ready():
28: 	mouse_filter=Control.MOUSE_FILTER_STOP;clip_contents=true;font=SystemFont.new();font.font_names=PackedStringArray(["PingFang SC","Noto Sans CJK SC","WenQuanYi Zen Hei","sans-serif"])
29: 	mouse_exited.connect(func():hovered="";cell_hovered.emit(""));set_process(true)
30: func _process(delta):
31: 	visual_time+=delta;flash_time=maxf(0,flash_time-delta)
32: 	if is_visible_in_tree():queue_redraw()
33: func radius() -> float:return minf(size.x/16.,maxf(160,size.y-64.)/14.)*zoom
34: func point(k:String) -> Vector2:
35: 	var p=Hex.coords(k);var r=radius();return size*.5+pan+Vector2(sqrt(3.)*r*(p.x+p.y*.5),1.5*r*p.y)+Vector2(0,6)
36: func polygon(center:Vector2,rr:float) -> PackedVector2Array:
37: 	var poly=PackedVector2Array()
38: 	for i in 6:poly.append(center+Vector2(cos(deg_to_rad(60*i-30)),sin(deg_to_rad(60*i-30)))*rr)
39: 	return poly
40: func hit(pos:Vector2) -> String:
41: 	if game==null:return ""
42: 	var best="";var dist=INF
43: 	for k in game.s.cells:
44: 		var d=point(k).distance_to(pos)
45: 		if d<dist:dist=d;best=k
46: 	if dist>radius():return ""
47: 	return best if Geometry2D.is_point_in_polygon(pos,polygon(point(best),radius())) else ""
48: func _gui_input(event):
49: 	if event is InputEventPanGesture:pan-=event.delta*16;accept_event();return
50: 	if event is InputEventMagnifyGesture:zoom=clampf(zoom*event.factor,.65,1.8);accept_event();return
51: 	if event is InputEventMouseButton:
52: 		if event.button_index==MOUSE_BUTTON_WHEEL_UP and event.pressed:zoom=clampf(zoom*1.1,.65,1.8);accept_event();return
53: 		if event.button_index==MOUSE_BUTTON_WHEEL_DOWN and event.pressed:zoom=clampf(zoom/1.1,.65,1.8);accept_event();return
54: 		if event.button_index in [MOUSE_BUTTON_MIDDLE,MOUSE_BUTTON_RIGHT]:panning=event.pressed;accept_event();return
55: 		if event.button_index==MOUSE_BUTTON_LEFT:
56: 			dragging=event.pressed
57: 			if event.pressed:
58: 				var k=hit(event.position)
59: 				if k!="":cell_clicked.emit(k,false)
60: 			accept_event()
61: 	elif event is InputEventMouseMotion:
62: 		if panning:pan+=event.relative
63: 		var k=hit(event.position)
64: 		if k!=hovered:hovered=k;cell_hovered.emit(k)
```


## 混合服务与场图层

`ui/map_view.gd:79–105`

```text
79: 	var r=radius();var keys=game.s.cells.keys();keys.sort_custom(func(a,b):return point(a).y<point(b).y if point(a).y!=point(b).y else str(a)<str(b))
80: 	var world=display_shot.metrics if display_shot.has("metrics") else game.s.metrics
81: 	var cells=display_shot.tiles if display_shot.has("tiles") else game.s.cells
82: 	var groups=display_shot.groups if display_shot.has("groups") else game.s.groups
83: 	for k in keys:
84: 		var t=game.s.cells[k];var pos=point(k);var color=terrain_colors.get(t.terrain,terrain_colors.plain)
85: 		color=color.lightened(float(absi(k.hash())%7)*.008)
86: 		if t.building!="":color=color.lerp(Color("d8d8bc"),.35)
87: 		if layer in ["pollution","heat"]:
88: 			var value=game.Rules.aggregate(cells.get(k,{}).get("fields",{}).get(layer,[]))
89: 			color=color.lerp(Color("bb7151") if layer=="pollution" else Color("d7a451"),clampf(value/(1. if layer=="pollution" else 4.),0,.88))
90: 		elif layer=="operator" and t.building!="":
91: 			var oi=game.Rules.ids(game.c.operators).find(game.s.projects[t.building].operator_org);color=color.lerp(Color.from_hsv(float(oi)/8.,.35,.87),.75)
92: 		elif layer=="service":
93: 			var reach=0.
94: 			for kind in ["medical","education","commercial","entertainment"]:
95: 				for f in cells.get(k,{}).get("fields",{}).get(kind,[]):reach=maxf(reach,float(f.value))
96: 			color=color.lerp(Color("65a58b"),minf(.5,reach*.4))
97: 			for group in groups.values():
98: 				if game.s.projects[group.host].tile==k:
99: 					var served=game.Progress.best_two(group.living);color=color.lerp(Color("61966b") if served>=.7 else Color("d69a67"),.6)
100: 		elif layer=="employment" and t.building!="":
101: 			var activity=world.get("utilization",{}).get(t.building,{})
102: 			if not activity.is_empty():color=color.lerp(Color("709976") if activity.labor>=.9 else Color("d3a064"),.65)
103: 		draw_colored_polygon(polygon(pos+Vector2(0,4*zoom),r*.97),Color("97a88b"))
104: 		draw_colored_polygon(polygon(pos,r*.975),color)
105: 		var outline=polygon(pos,r*.975);outline.append(outline[0]);draw_polyline(outline,Color(.38,.50,.36,.27),1.,true)
```


## 邻接绘路及关系

`ui/map_view.gd:124–147`

```text
124: 	var ids=game.playable_ids()
125: 	for i in ids.size():
126: 		for j in range(i+1,ids.size()):
127: 			var a=game.s.projects[ids[i]];var b=game.s.projects[ids[j]]
128: 			if game.Rules.near(game.s,a.id,b.id)==1:
129: 				draw_line(point(a.tile),point(b.tile),Color("abae94"),7*zoom,true);draw_line(point(a.tile),point(b.tile),Color("e7e0c4"),4.5*zoom,true)
130: 	# Relationship overlays are read from actual finite allocations, not invented traffic.
131: 	if selected_project!="" and game.s.projects.has(selected_project):
132: 		var origin=point(game.s.projects[selected_project].tile)
133: 		for link in world.get("service_links",[]):
134: 			if selected_project not in [link.source,link.home]:continue
135: 			var other=link.home if link.source==selected_project else link.source
136: 			if not game.s.projects.has(other) or game.s.projects[other].special or link.people<=0:continue
137: 			draw_dashed_line(origin,point(game.s.projects[other].tile),Color("388666"),2.,7.,true)
138: 		for link in world.get("employment_links",[]):
139: 			var home=game.s.groups.get(link.group,{}).get("host","") if link.has("group") else str(link.get("home",""))
140: 			if selected_project not in [link.target,home] or not game.s.projects.has(home) or game.s.projects[home].special:continue
141: 			var a=point(game.s.projects[home].tile);var b=point(game.s.projects[link.target].tile);draw_dashed_line(a,b,Color("477d9b"),1.7,6.,true)
142: 			if link.people>0:draw_circle(a.lerp(b,fmod(visual_time*.15,1.)),2.5,Color("f9f2d8"))
143: 		for link in world.get("exposure_links",[]):
144: 			if selected_project not in [link.source,link.home] or not game.s.projects.has(link.home):continue
145: 			var other=link.home if link.source==selected_project else link.source
146: 			if not game.s.projects.has(other) or game.s.projects[other].special:continue
147: 			draw_dashed_line(origin,point(game.s.projects[other].tile),Color("bc7953"),1.8,8.,true)
```


## 地图当前预览和图例

`ui/map_view.gd:162–177`

```text
162: 			if gid!="":text+=" · %.0f人"%game.s.groups[gid].count
163: 			marker(pos+Vector2(0,43*scale),text,Color("38574a"),12)
164: 		if flash_time>0 and id==selected_project:draw_arc(pos,r*.83,0,TAU,36,Color(1.,.91,.6,flash_time*.35),3.,true)
165: 	if mode=="site" and plan_tile!="" and game.s.cells.has(plan_tile) and game.s.cells[plan_tile].building=="":
166: 		var pos=point(plan_tile);var line=polygon(pos,r*.9);line.append(line[0]);draw_colored_polygon(polygon(pos,r*.9),Color(.97,.92,.7,.30));draw_polyline(line,Color("b77939"),2.5,true)
167: 		cube(pos,31*zoom,9*zoom,17*zoom,Color("ebdfba"));marker(pos+Vector2(0,-31*zoom),"规划中",Color("8a6339"),12)
168: 		if plan_result.get("ok",false):
169: 			for group in game.s.groups.values():
170: 				var home=game.s.projects[group.host]
171: 				if home.special:continue
172: 				var old=game.Rules.aggregate(cells.get(home.tile,{}).get("fields",{}).get("pollution",[]));var new=game.Rules.aggregate(plan_result.tiles.get(home.tile,{}).get("fields",{}).get("pollution",[]))
173: 				if new>old+.01:draw_dashed_line(pos,point(home.tile),Color("b96540"),2.,8.,true)
174: 	var legend={"normal":"肥沃 / 林地 / 高地 / 水岸 · 地形决定基础机会","pollution":"环境负担：绿 → 橙（1以上高暴露）· 选中建筑看来源","heat":"余热：浅 → 暖（0～4）· 总额度有限，不随覆盖复制","service":"淡绿：可达；住区深绿：两类服务≥70%，橙：仍有缺口","employment":"绿：人手充足；橙：缺工 · 点击建筑看工作去向","operator":"不同边界颜色表示不同运营组织"}.get(layer,"")
175: 	if mode=="site":legend="规划 · ×数值为额定地形增益；右侧预览才计入当前供给与居民"
176: 	draw_string(font,Vector2(18,26),legend,HORIZONTAL_ALIGNMENT_LEFT,-1,12,Color("425c4c"))
177: 	draw_string(font,Vector2(18,size.y-14),"河谷图志   /   滚轮缩放 · 中键 / 右键 / 触控板平移",HORIZONTAL_ALIGNMENT_LEFT,-1,11,Color("61775c"))
```


## 界面固定尺寸

`ui/app.gd:86–136`

```text
86: func build_ui():
87: 	var bg=ColorRect.new();bg.color=Color("e9eddf");bg.mouse_filter=Control.MOUSE_FILTER_IGNORE;bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(bg)
88: 	var margin=MarginContainer.new();margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
89: 	for side in ["left","right","top","bottom"]:margin.add_theme_constant_override("margin_"+side,10)
90: 	add_child(margin);var root=W.vbox(margin,7)
91: 	var top=W.panel(root);var header=W.hbox(top,14)
92: 	var names=W.vbox(header,0);names.custom_minimum_size.x=173
93: 	names.add_child(W.label("MET · 河谷新章",22,W.TEXT,false));names.add_child(W.label("0.2.1  /  一座城，慢慢长出来",10,W.MUTED,false))
94: 	goal_button=W.button("河谷的发展目标",func():Dialogs.goals(self));goal_button.custom_minimum_size.x=265;goal_button.size_flags_horizontal=Control.SIZE_EXPAND_FILL;header.add_child(goal_button)
95: 	var finance=W.vbox(header,0);finance.custom_minimum_size.x=126;finance.add_child(W.label("可用预算 / 每小时收支",10,W.MUTED,false));cash_label=W.label("",17,W.TEXT,false);finance.add_child(cash_label)
96: 	var time=W.vbox(header,0);time.custom_minimum_size.x=80;time.add_child(W.label("瓶中时光 DT",10,W.MUTED,false));dt_label=W.label("",17,W.GOLD,false);time.add_child(dt_label)
97: 	var people=W.vbox(header,0);people.custom_minimum_size.x=110;people.add_child(W.label("居民 / 住房",10,W.MUTED,false));governance_label=W.label("",17,W.TEXT,false);people.add_child(governance_label)
98: 	header.add_child(W.button("政策",func():Dialogs.block(self)));header.add_child(W.button("账本",func():Dialogs.resources(self)));header.add_child(W.button("菜单",func():Dialogs.settings(self)))
99: 	var middle=W.hbox(root,8);middle.size_flags_vertical=Control.SIZE_EXPAND_FILL
100: 	var map_stack=W.vbox(middle,5);map_stack.size_flags_horizontal=Control.SIZE_EXPAND_FILL
101: 	var issues=W.hbox(map_stack,8)
102: 	for i in 2:
103: 		var b=W.button("",func():alert_action(i));b.size_flags_horizontal=Control.SIZE_EXPAND_FILL;b.clip_text=true;b.custom_minimum_size.y=34;issues.add_child(b);alert_buttons.append(b)
104: 	var canvas=PanelContainer.new();canvas.add_theme_stylebox_override("panel",W.box(Color("c4d4bb"),Color("a6b69a"),12));map_stack.add_child(canvas);canvas.size_flags_vertical=Control.SIZE_EXPAND_FILL
105: 	map_view=MapView.new();map_view.game=game;map_view.size_flags_horizontal=Control.SIZE_EXPAND_FILL;map_view.size_flags_vertical=Control.SIZE_EXPAND_FILL
106: 	map_view.cell_clicked.connect(on_cell);map_view.cell_hovered.connect(on_hover);canvas.add_child(map_view)
107: 	var layers=W.hbox(map_stack,6)
108: 	for p in [["normal","地形"],["pollution","环境"],["heat","余热"],["service","服务"],["employment","就业"],["operator","运营商"]]:
109: 		layers.add_child(W.button(p[1],func():map_view.layer=p[0];map_view.queue_redraw(),false,"点击建筑，只看与它有关的关系。"))
110: 	W.spacer(layers);layers.add_child(W.button("归位",func():map_view.zoom=1.;map_view.pan=Vector2.ZERO;map_view.queue_redraw()))
111: 	inspector_panel=W.panel(middle);inspector_panel.custom_minimum_size.x=308
112: 	inspector_scroll=ScrollContainer.new();inspector_scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;inspector_panel.add_child(inspector_scroll)
113: 	inspector=W.vbox(inspector_scroll,8);inspector.custom_minimum_size.x=278;inspector.size_flags_horizontal=Control.SIZE_EXPAND_FILL
114: 	var dock=W.hbox(root,5)
115: 	for id in ["hamlet","farm","timber","power","works","market","school","clinic","park","warehouse"]:
116: 		var def=game.c.projects[id];var shortname={"hamlet":"住房","farm":"农场","timber":"料场","power":"动力站","works":"工业厂","market":"市场","school":"学校","clinic":"诊所","park":"公园","warehouse":"仓库"}[id]
117: 		var b=W.button(shortname+"\n%.0f通货"%def.cost,func():select_base(id),false,Presentation.primary(id));b.size_flags_horizontal=Control.SIZE_EXPAND_FILL;b.custom_minimum_size.y=48;dock.add_child(b);build_buttons[id]=b
118: 	var bottom=W.panel(root);var row=W.hbox(bottom,15)
119: 	resource_row=W.hbox(row,12);resource_row.size_flags_horizontal=Control.SIZE_EXPAND_FILL
120: 	for r in ["food","material","goods","energy"]:
121: 		var v=W.vbox(resource_row,1);v.size_flags_horizontal=Control.SIZE_EXPAND_FILL
122: 		var head=W.label("",14,W.TEXT,false);var body=W.label("",11,W.MUTED,false);v.add_child(head);v.add_child(body);resources_labels[r]={"parent":v,"head":head,"body":body}
123: 	var time_controls=W.vbox(row,2);clock_label=W.label("",11,W.MUTED,false);time_controls.add_child(clock_label)
124: 	var controls=W.hbox(time_controls,5);checkpoint_button=W.primary(W.button("推进到下一变化",advance_checkpoint));controls.add_child(checkpoint_button)
125: 	for h in [.5,1.,3.]:
126: 		var b=W.button("+%sh"%str(h),func():do_pulse(h));controls.add_child(b);pulse_buttons.append([b,h])
127: 	toast=W.label("先选一种建筑，再把鼠标放到地图上看看。",12,W.MUTED,false);toast.text_overrun_behavior=TextServer.OVERRUN_TRIM_ELLIPSIS;root.add_child(toast)
128: 	overlay=Control.new();overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);add_child(overlay)
129: 	var shade=ColorRect.new();shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);shade.color=Color(.11,.19,.16,.65);overlay.add_child(shade)
130: 	var center=CenterContainer.new();center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);overlay.add_child(center)
131: 	modal_panel=W.panel(center);modal_panel.custom_minimum_size=Vector2(1050,625)
132: 	var outer=W.vbox(modal_panel,10);var mh=W.hbox(outer,12);var titles=W.vbox(mh,4);titles.size_flags_horizontal=Control.SIZE_EXPAND_FILL
133: 	modal_title=W.label("",25,W.TEXT);titles.add_child(modal_title);modal_subtitle=W.label("",13,W.MUTED);titles.add_child(modal_subtitle)
134: 	mh.add_child(W.button("返回  Esc",close_modal))
135: 	modal_scroll=ScrollContainer.new();modal_scroll.custom_minimum_size=Vector2(1026,500);modal_scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;modal_scroll.size_flags_vertical=Control.SIZE_EXPAND_FILL;outer.add_child(modal_scroll)
136: 	modal_body=W.vbox(modal_scroll,10);modal_body.custom_minimum_size.x=1008;modal_body.size_flags_horizontal=Control.SIZE_EXPAND_FILL;overlay.hide()
```


## 模态影响本地时间

`ui/app.gd:252–261`

```text
252: func _process(delta):
253: 	if test_mode or game.s.is_empty() or closing or overlay==null:return
254: 	var now=Time.get_unix_time_from_system()
255: 	if now-last_wall>8:game.s.wall_mark=last_wall;game.resume_time(now)
256: 	elif game.s.phase=="play" and not overlay.visible:game.advance_online(minf(delta,2.)/3600.*float(game.s.settings.lab_speed))
257: 	last_wall=now;game.update_global(now);save_elapsed+=delta;ui_elapsed+=delta
258: 	if save_elapsed>=5:save_elapsed=0;save_game()
259: 	if ui_elapsed>=1:
260: 		ui_elapsed=0;refresh_stats()
261: 		if not overlay.visible and last_revision!=game.revision:refresh()
```


## 同内核只读预测

`core/planning.gd:1–35`

```text
1: extends RefCounted
2: ## Read-only, one-step forecasts using the real engine. Never touches player RNG or stock.
3: const R=preload("res://core/rules.gd")
4: var cache={}
5: var token=""
6: func version(g) -> String:return str(g.revision)+":"+str(g.s.simulation_tick)+":"+str(g.s.projects.size())
7: func copy_game(g):
8: 	var other=g.get_script().new();other.c=g.c;other.s=g.s.duplicate(true);other.preview_mode=true;other.refresh_metrics();return other
9: func reset_for(g) -> void:
10: 	var key=version(g)
11: 	if key!=token:cache={};token=key
12: func snapshot(g) -> Dictionary:
13: 	reset_for(g)
14: 	if cache.has("world"):return cache.world
15: 	if not g.s.phase in ["play","failed"] or g.s.projects.is_empty():return {"metrics":g.s.metrics,"audit":{},"projects":g.s.projects,"groups":g.s.groups,"tiles":g.s.cells,"error":""}
16: 	var clone=copy_game(g);var err=clone.Sim.tick(clone,float(g.c.balance.tick_hours))
17: 	var result={"metrics":clone.s.metrics,"audit":clone.s.audit,"projects":clone.s.projects,"groups":clone.s.groups,"tiles":clone.s.cells,"compiled":clone.compiled,"error":err,"forecast":true}
18: 	cache.world=result;return result
19: func compare(g,base:String,tile:String,offer:Dictionary={},host:String="",slot:String="") -> Dictionary:
20: 	reset_for(g)
21: 	var key=JSON.stringify([base,tile,offer,host,slot])
22: 	if cache.has(key):return cache[key]
23: 	var reference=snapshot(g);var clone=copy_game(g);var target=host
24: 	if host=="":
25: 		if not clone.s.cells.has(tile) or clone.s.cells[tile].building!="" or "tile.water" in clone.s.cells[tile].traits:return {"ok":false,"error":"请选择未开发的陆地。"}
26: 		var operator=str(offer.get("operator_org","commons"))
27: 		target=clone.create_building(base,tile,operator,true).id
28: 		if offer.is_empty():clone.s.projects[target].operator_unresolved=true
29: 	if not offer.is_empty():
30: 		var chosen_slot=str(offer.get("slot",slot))
31: 		var err=clone.mount_bonus(offer.bonus_id,"building",target,str(offer.get("provider_org","commons")),chosen_slot)
32: 		if err!="":return {"ok":false,"error":err}
33: 	clone.refresh_metrics()
34: 	var err=clone.Sim.tick(clone,float(g.c.balance.tick_hours))
35: 	if err!="":return {"ok":false,"error":err}
```


## 已有预测返回粒度

`core/planning.gd:36–81`

```text
36: 	var st=clone.project_stats(target);var a=clone.s.metrics.utilization.get(target,{})
37: 	var services={};var additional={}
38: 	for kind in g.c.balance.service_kinds:
39: 		var own=0.;var total_now=0.;var total_before=0.
40: 		for link in clone.s.metrics.service_links:
41: 			if link.kind==kind:
42: 				total_now+=float(link.people)
43: 				if link.source==target:own+=float(link.people)
44: 		for link in reference.metrics.get("service_links",[]):
45: 			if link.kind==kind:total_before+=float(link.people)
46: 		services[kind]=own;additional[kind]=total_now-total_before
47: 	var harmed=0.;var exposure_delta=0.
48: 	for gid in g.s.groups:
49: 		var old=reference.groups.get(gid,{});var now=clone.s.groups.get(gid,{})
50: 		if old.is_empty() or now.is_empty():continue
51: 		var diff=float(now.living.get("pollution",0))-float(old.living.get("pollution",0))
52: 		if diff>.015:harmed+=float(g.s.groups[gid].count)
53: 		exposure_delta+=diff*float(g.s.groups[gid].count)
54: 	var inputs={};var retained={};var losses={}
55: 	for entry in clone.s.metrics.activity_ledger.get(target,[]):
56: 		if entry.kind=="input":inputs[entry.resource]=float(inputs.get(entry.resource,0))+float(entry.rate)
57: 		else:
58: 			retained[entry.resource]=float(retained.get(entry.resource,0))+float(entry.kept)
59: 			losses[entry.resource]=float(losses.get(entry.resource,0))+float(entry.lost)
60: 	var workers=0.
61: 	for link in clone.s.metrics.employment_links:
62: 		if link.target==target:workers+=float(link.people)
63: 	var effects=[]
64: 	for bid in g.playable_ids():
65: 		var before=reference.metrics.get("utilization",{}).get(bid,{}).get("utilization",0)
66: 		var after=clone.s.metrics.utilization.get(bid,{}).get("utilization",0)
67: 		if float(after)<float(before)-.1:effects.append({"id":bid,"delta":float(after)-float(before)})
68: 	var result={"ok":true,"target":target,"stats":st,"activity":a,"outputs":clone.s.projects[target].outputs_last,"retained":retained,"losses":losses,"inputs":inputs,"services":services,"added_service":additional,
69: 		"workers":workers,"housing":clone.s.metrics.housing-reference.metrics.get("housing",0),"cashflow":clone.s.metrics.cashflow-reference.metrics.get("cashflow",0),
70: 		"energy_demand":clone.s.metrics.energy_need-reference.metrics.get("energy_need",0),"energy_supply":clone.s.metrics.energy-reference.metrics.get("energy",0),
71: 		"energy_ratio":clone.s.metrics.energy_ratio,"harmed":harmed,"exposure_delta":exposure_delta,"affected":effects,
72: 		"existing_food_produced":reference.audit.get("resources",{}).get("food",{}).get("produced",0)/float(g.c.balance.tick_hours),"existing_food_consumed":reference.audit.get("resources",{}).get("food",{}).get("consumed",0)/float(g.c.balance.tick_hours),"recycled":clone.s.metrics.pollution_recycled-reference.metrics.get("pollution_recycled",0),"tiles":clone.s.cells,"trace":clone.compiled.trace.get(target,{}),"world":clone.s.metrics}
73: 	cache[key]=result;return result
74: func policy(g,id:String) -> Dictionary:
75: 	reset_for(g);var key="policy:"+id
76: 	if cache.has(key):return cache[key]
77: 	var reference=snapshot(g);var clone=copy_game(g)
78: 	clone.s.block.policies[clone.c.policies[id].slot]=id
79: 	var err=clone.Sim.tick(clone,float(g.c.balance.tick_hours))
80: 	var result={"ok":err=="","error":err,"cashflow":clone.s.metrics.cashflow,"delta":clone.s.metrics.cashflow-reference.metrics.get("cashflow",0),"tax":clone.s.metrics.income}
81: 	cache[key]=result;return result
```


## Field来源与转播

`core/fields.gd:1–17`

```text
1: extends RefCounted
2: ## Source-aware Field entities; Tile holds derived samples, never the authority.
3: const R=preload("res://core/rules.gd")
4: const Hex=preload("res://core/hex.gd")
5: static func empty_tiles(s:Dictionary) -> Dictionary:
6: 	var tiles={}
7: 	for id in R.ids(s.cells):tiles[id]={}
8: 	return tiles
9: static func sample(tiles:Dictionary,tile:String,kind:String,id:String,source:String,value:float,via:String="") -> void:
10: 	if not tiles.has(tile):return
11: 	if not tiles[tile].has(kind):tiles[tile][kind]=[]
12: 	tiles[tile][kind].append({"field_id":id,"source_id":source,"value":value,"via":via})
13: static func make_field(source:String,kind:String,strength:float,radius:int,decay:float,rule:String="base") -> Dictionary:
14: 	return {"id":"F:"+source+":"+kind+":"+rule,"type":kind,"source_id":source,"strength":strength,"propagation":{"radius":radius,"decay":decay,"rule":rule},"active":true}
15: static func access_strength(distance:int,radius:int,decay:float) -> float:
16: 	if distance>radius:return 0
17: 	return maxf(0,1.0-maxi(0,distance-1)*decay)
```


## Field转播源保留

`core/fields.gd:77–88`

```text
77: 			if value!=0:sample(tiles,tile,f.type,fid,source,value)
78: 	# One-hop relay reads the original access snapshot. The provider ID never changes.
79: 	var original=tiles.duplicate(true)
80: 	for id in R.ids(s.projects):
81: 		var p=s.projects[id];var relay=float(compiled.config[id].get("service_relay",0))
82: 		if relay<=0 or p.status!="running" or p.paused:continue
83: 		for kind in g.c.balance.service_kinds:
84: 			for x in original.get(p.tile,{}).get(kind,[]):
85: 				if x.source_id==id or R.near(s,id,x.source_id)!=1:continue
86: 				for key in R.ids(s.cells):
87: 					if Hex.distance(p.tile,key)<=1:sample(tiles,key,kind,x.field_id,x.source_id,float(x.value)*relay,id)
88: 	return {"fields":entities,"tiles":tiles}
```


## 统一主题仍依赖系统字体

`ui/widgets.gd:1–40`

```text
1: extends RefCounted
2: const TEXT=Color("293f39")
3: const MUTED=Color("68766b")
4: const GOLD=Color("997341")
5: const GREEN=Color("367b66")
6: const RED=Color("b4513d")
7: const PANEL=Color("faf7ef")
8: const SOFT=Color("e7eadd")
9: static func box(color:Color=PANEL,border:Color=Color("cdd1bd"),radius:int=9) -> StyleBoxFlat:
10: 	var style=StyleBoxFlat.new();style.bg_color=color;style.border_color=border
11: 	style.set_border_width_all(1);style.set_corner_radius_all(radius)
12: 	style.content_margin_left=12;style.content_margin_right=12;style.content_margin_top=10;style.content_margin_bottom=10
13: 	return style
14: static func button_box(color:Color,border:Color=Color("c8ceba")) -> StyleBoxFlat:
15: 	var style=box(color,border,6);style.content_margin_top=7;style.content_margin_bottom=7
16: 	style.content_margin_left=11;style.content_margin_right=11;return style
17: static func theme() -> Theme:
18: 	var t=Theme.new();var f=SystemFont.new();f.font_names=PackedStringArray(["PingFang SC","Noto Sans CJK SC","WenQuanYi Zen Hei","sans-serif"])
19: 	t.default_font=f;t.default_font_size=14
20: 	for cls in ["Label","Button","CheckButton","OptionButton","LineEdit"]:t.set_color("font_color",cls,TEXT)
21: 	t.set_color("font_hover_color","Button",GREEN);t.set_color("font_disabled_color","Button",Color("9eaa99"))
22: 	t.set_stylebox("normal","Button",button_box(Color("f7f5ea")))
23: 	t.set_stylebox("hover","Button",button_box(Color("e4eddc"),GREEN))
24: 	t.set_stylebox("pressed","Button",button_box(Color("d1e3d0"),GREEN))
25: 	t.set_stylebox("disabled","Button",button_box(Color("ebecdf")))
26: 	var focus=box(Color(0,0,0,0),GOLD,6);focus.set_border_width_all(2);t.set_stylebox("focus","Button",focus)
27: 	t.set_stylebox("panel","PanelContainer",box());t.set_stylebox("normal","LineEdit",box(Color("fffdf5")))
28: 	t.set_constant("separation","VBoxContainer",8);t.set_constant("separation","HBoxContainer",8)
29: 	t.set_stylebox("background","ProgressBar",box(Color("dbe2d1"),Color("dbe2d1"),3))
30: 	t.set_stylebox("fill","ProgressBar",box(GREEN,GREEN,3));return t
31: static func label(text:String,size:int=14,color:Color=TEXT,wrap:bool=true) -> Label:
32: 	var l=Label.new();l.text=text;l.add_theme_font_size_override("font_size",size);l.add_theme_color_override("font_color",color)
33: 	if wrap:l.autowrap_mode=TextServer.AUTOWRAP_WORD_SMART
34: 	l.size_flags_horizontal=Control.SIZE_EXPAND_FILL;return l
35: static func button(text:String,fn:Callable,disabled:bool=false,tooltip:String="") -> Button:
36: 	var b=Button.new();b.text=text;b.disabled=disabled;b.tooltip_text=tooltip;b.mouse_default_cursor_shape=Control.CURSOR_POINTING_HAND
37: 	b.custom_minimum_size.y=34;b.pressed.connect(fn);return b
38: static func primary(b:Button) -> Button:
39: 	b.add_theme_stylebox_override("normal",button_box(GREEN,GREEN));b.add_theme_color_override("font_color",Color("fffbee"))
40: 	b.add_theme_color_override("font_hover_color",TEXT);return b
```
