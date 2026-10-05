extends RefCounted
## Pure presentation of a detached, same-step comparison model.
const W=preload("res://ui/widgets.gd")
const Art=preload("res://ui/map/art.gd")
const D=preload("res://ui/components031/design.gd")

class CompareBars extends Control:
	const W=preload("res://ui/widgets.gd")
	var data:Dictionary={}
	var energy:bool=false
	func _init():
		mouse_filter=Control.MOUSE_FILTER_PASS
		size_flags_horizontal=Control.SIZE_EXPAND_FILL
		set_meta("graphical_indicator",true)
		resized.connect(queue_redraw)
	func configure(value:Dictionary,is_energy:bool=false):
		data=value.duplicate(true);energy=is_energy
		set_meta("impact_graphic","energy" if energy else "resource_flow")
		set_meta("metric_unit","供需能力" if energy else "单位/月")
		for key in ["before_supply","before_demand","after_supply","after_demand","before_need","after_need"]:
			if data.has(key):set_meta(key,float(data[key]))
		var before_need=float(data.get("before_need",0)) if energy else float(data.get("before_demand",0))
		var after_need=float(data.get("after_need",0)) if energy else float(data.get("after_demand",0))
		var axis_max=maxf(maxf(float(data.get("before_supply",0)),float(data.get("after_supply",0))),maxf(before_need,after_need))
		set_meta("axis_max",axis_max)
		tooltip_text="同一下一周期：现状预测 → 假定建成。条长为全城供给，竖线为全城需求；前后使用同一比例尺。"
		if energy:tooltip_text+="电力为供需能力，不是库存或每月累计量。"
		update_minimum_size();queue_redraw()
	func _get_minimum_size() -> Vector2:return Vector2(180,46 if energy else 63)*W.text_scale
	func _draw():
		if data.is_empty():return
		var axis_max=float(get_meta("axis_max",0))
		var font=get_theme_default_font();var fs=int(12*W.text_scale)
		var colors=[W.OUTLINE,W.ACCENT]
		var known=bool(data.get("known",true))
		for index in 2:
			var phase="before" if index==0 else "after"
			var supply=float(data.get(phase+"_supply",0))
			var demand=float(data.get(phase+("_need" if energy else "_demand"),0))
			var y=float(index)*23*W.text_scale
			var text=("前 " if index==0 else "后 ")+("%.1f / %.1f"%[supply,demand] if known else "— / —")
			draw_string(font,Vector2(0,y+fs),text,HORIZONTAL_ALIGNMENT_LEFT,size.x,fs,W.MUTED if index==0 else W.TEXT)
			var track=Rect2(0,y+16*W.text_scale,size.x,5*W.text_scale)
			draw_style_box(W.box(W.SOFT,Color.TRANSPARENT,2),track)
			if axis_max>0 and known:
				var fill=track;fill.size.x*=clampf(supply/axis_max,0,1)
				if fill.size.x>0:draw_style_box(W.box(colors[index],Color.TRANSPARENT,2),fill)
				var mark=clampf(demand/axis_max,0,1)*maxf(0,size.x-1)
				draw_line(Vector2(mark,track.position.y-2*W.text_scale),Vector2(mark,track.end.y+2*W.text_scale),W.GOLD,2*W.text_scale)
		if not energy:
			var coverage="等待有效结算";var ink=W.MUTED
			if known:
				var demand=float(data.get("after_demand",0))
				if demand>.000001:
					var fulfilled=clampf(float(data.get("after_consumed",0))/demand,0,1)
					coverage="满足 %.0f%%"%(fulfilled*100)
					if fulfilled<.999:coverage+=" · 缺 %.0f%%"%((1-fulfilled)*100);ink=W.GOLD
					else:coverage=str(data.get("status","供需平稳"));ink=W.GREEN
				else:coverage=str(data.get("status","暂无需求"))
			draw_string(font,Vector2(0,48*W.text_scale+fs),coverage,HORIZONTAL_ALIGNMENT_LEFT,size.x,fs,ink)

class EnvironmentStrip extends Control:
	const W=preload("res://ui/widgets.gd")
	var data:Dictionary={}
	func _init():
		mouse_filter=Control.MOUSE_FILTER_PASS
		size_flags_horizontal=Control.SIZE_EXPAND_FILL
		custom_minimum_size=Vector2(120,11)
		set_meta("impact_graphic","environment_area")
		resized.connect(queue_redraw)
	func configure(value:Dictionary):
		data=value.duplicate(true)
		for key in ["total_tiles","affected_tiles","increased_tiles","reduced_tiles","harmed","exposure_delta","max_increase","max_reduction"]:
			set_meta(key,data.get(key,0))
		tooltip_text="全城地格为比例尺：红色为负担增加，绿色为减少，灰色为没有变化。住户数字指污染状态增加超过预测阈值的现有住户，不是健康损失。"
		queue_redraw()
	func _draw():
		var total=float(data.get("total_tiles",0))
		var rect=Rect2(0,2,size.x,7)
		draw_style_box(W.box(W.SOFT,Color.TRANSPARENT,2),rect)
		if total<=0:return
		var increased=float(data.get("increased_tiles",0))/total
		var reduced=float(data.get("reduced_tiles",0))/total
		var part=rect;part.size.x*=increased
		if part.size.x>0:draw_style_box(W.box(W.DANGER,Color.TRANSPARENT,2),part)
		part.position.x+=part.size.x;part.size.x=rect.size.x*reduced
		if part.size.x>0:draw_style_box(W.box(W.SUCCESS,Color.TRANSPARENT,2),part)

static func _small_button(text:String,fn:Callable,tip:String="") -> Button:
	var button=W.ghost(W.button(text,fn,false,tip))
	button.custom_minimum_size.y=25*W.text_scale
	button.add_theme_font_size_override("font_size",int(12*W.text_scale))
	return button

static func thin_bar(value:float,color:Color=W.INFO) -> ProgressBar:
	var meter=W.bar(value);meter.custom_minimum_size.y=4;meter.mouse_filter=Control.MOUSE_FILTER_IGNORE
	for key in ["background","fill"]:
		var style=StyleBoxFlat.new();style.bg_color=W.SOFT if key=="background" else color;style.set_corner_radius_all(2)
		style.content_margin_left=0;style.content_margin_right=0;style.content_margin_top=0;style.content_margin_bottom=0
		meter.add_theme_stylebox_override(key,style)
	return meter

static func _runway(value:float) -> String:
	return "无净消耗" if value<0 else "%.1f月"%value

static func _stock(parent:Node,row:Dictionary,on_layer:Callable) -> void:
	var group=W.vbox(parent,1);group.custom_minimum_size.x=122*W.text_scale
	if on_layer.is_valid():
		var button=_small_button(str(row.get("name",""))+" ›",func():on_layer.call(str(row.get("id","food"))),"在地图查看该资源的建成后供需预测。")
		button.icon=Art.icon(D.icon_id(str(row.get("id","food"))),W.TEXT);button.expand_icon=true;button.add_theme_constant_override("icon_max_width",18);button.custom_minimum_size.x=0;button.set_meta("impact_layer",str(row.get("id","food")));group.add_child(button)
	else:group.add_child(W.label(str(row.get("name","")),13,W.TEXT,false))
	var amount=W.label("%.1f / %.1f"%[float(row.get("stock",0)),float(row.get("capacity",0))],12,W.MUTED,false)
	amount.set_meta("impact_stock",str(row.get("id","")));amount.set_meta("stock",float(row.get("stock",0)));amount.set_meta("capacity",float(row.get("capacity",0)));amount.set_meta("metric_unit","库存单位")
	group.add_child(amount)
	var capacity=float(row.get("capacity",0));group.add_child(thin_bar(float(row.get("stock",0))/capacity if capacity>0 else 0))

static func _resource(parent:Node,row:Dictionary,on_layer:Callable) -> void:
	var line=W.hbox(parent,10);line.set_meta("impact_resource",str(row.get("id","")))
	for key in row:line.set_meta(str(key),row[key])
	_stock(line,row,on_layer)
	var flow=CompareBars.new();line.add_child(flow);flow.configure(row)
	var status=W.vbox(line,0);status.custom_minimum_size.x=185*W.text_scale
	var before=float(row.get("before_net",0));var after=float(row.get("after_net",0))
	var known=bool(row.get("known",true))
	var contribution="本楼 %.1f/月 · 需求占比 —"%float(row.get("output",0))
	if bool(row.get("share_known",false)):
		contribution="本楼 %.1f/月 · 需求 %.0f%%"%[float(row.get("output",0)),float(row.get("need_share",0))*100]
	elif known and float(row.get("after_demand",0))<=.000001:contribution="本楼 %.1f/月 · 暂无需求"%float(row.get("output",0))
	var label=W.label(contribution,11,W.TEXT,false);label.set_meta("impact_need_share",str(row.get("id","")));label.set_meta("share_known",bool(row.get("share_known",false)));label.set_meta("need_share",row.get("need_share",null));status.add_child(label)
	status.add_child(W.label("净流 %+.1f → %+.1f/月"%[before,after] if known else "净流 — → —",11,W.DANGER if known and after<-.001 else W.ACCENT,false))
	status.add_child(W.label("可撑 "+_runway(float(row.get("runway_before",-1)))+" → "+_runway(float(row.get("runway_after",-1))) if known else "库存支撑 —",11,W.MUTED,false))
	line.tooltip_text="本楼产出占全城下一结算需求的比例，不代表全部由本楼供给。当前库存不提前扣款或入库；可撑月数是以当前总库存除以预测净消耗的参考值，局部库存未必可达，贸易和一次支出也会改变它。满足率来自实际消耗回执。"

static func _card(parent:Node) -> VBoxContainer:
	var panel=W.panel(parent,W.BASE);panel.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	var box=W.box(W.BASE,W.DIVIDER,6);box.content_margin_left=9;box.content_margin_right=9;box.content_margin_top=6;box.content_margin_bottom=6;panel.add_theme_stylebox_override("panel",box)
	return W.vbox(panel,3)

static func build(parent:Node,model:Dictionary,on_layer:Callable) -> VBoxContainer:
	var root=W.vbox(parent,5);root.name="TenderImpactPanel";root.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	root.set_meta("impact_model",model.duplicate(true));root.set_meta("snapshot_id",model.get("snapshot_id",""));root.set_meta("map_snapshot_forecast",model.get("map_snapshot_forecast",true))
	var top=W.hbox(root,5);top.add_child(W.label("建成后的全城影响",15,W.TEXT,false))
	if on_layer.is_valid():
		var button=_small_button("差分地图 ›",func():on_layer.call("change"),"定位规划地块，查看同一步预测相对现状预测的地图差分。")
		button.name="TenderChangeLayer";button.set_meta("impact_layer","change");top.add_child(button)
	var head=W.hbox(root,10)
	var stock_title=W.label("当前库存 / 容量",11,W.MUTED,false);stock_title.custom_minimum_size.x=122*W.text_scale;stock_title.size_flags_horizontal=Control.SIZE_FILL;head.add_child(stock_title)
	var flow_title=W.label("全城产出 ▰ / 需求 │（/月）",11,W.MUTED,false);flow_title.custom_minimum_size.x=180*W.text_scale;head.add_child(flow_title)
	var net_title=W.label("净流与库存支撑（参考）",11,W.MUTED,false);net_title.custom_minimum_size.x=185*W.text_scale;net_title.size_flags_horizontal=Control.SIZE_FILL;head.add_child(net_title)
	for row in model.get("resources",[]):_resource(root,row,on_layer)
	var cards=W.hbox(root,8)
	var energy:Dictionary=model.get("energy",{})
	var power=_card(cards);power.set_meta("impact_energy",energy.duplicate(true))
	var power_title=W.hbox(power,3);power_title.add_child(W.label("电力 · 供 / 需能力",12,W.TEXT,false))
	if on_layer.is_valid():
		var button=_small_button("地图 ›",func():on_layer.call("energy"),"在地图查看建成后供电能力和电力请求。")
		button.name="TenderEnergyLayer";button.set_meta("impact_layer","energy");power_title.add_child(button)
	var meter=CompareBars.new();power.add_child(meter);meter.configure(energy,true)
	power.add_child(W.label("满足 %.0f%% → %.0f%%"%[float(energy.get("before_ratio",0))*100,float(energy.get("after_ratio",0))*100] if bool(energy.get("known",true)) else "满足率 — → —",11,W.GOLD if float(energy.get("after_ratio",0))<.999 else W.GREEN,false))
	var environment:Dictionary=model.get("environment",{})
	var env=_card(cards);env.set_meta("impact_environment",environment.duplicate(true))
	var env_title=W.hbox(env,4);env_title.add_child(W.label("环境负担",12,W.TEXT,false))
	if on_layer.is_valid():
		var button=_small_button("污染地图 ›",func():on_layer.call("pollution"),"定位规划地块，查看建成后预测的污染场。")
		button.name="TenderPollutionLayer";button.set_meta("impact_layer","pollution");env_title.add_child(button)
	var strip=EnvironmentStrip.new();env.add_child(strip);strip.configure(environment)
	env.add_child(W.label("↑ %d格  ↓ %d格 / 全城%d格"%[int(environment.get("increased_tiles",0)),int(environment.get("reduced_tiles",0)),int(environment.get("total_tiles",0))],11,W.MUTED,false))
	env.add_child(W.label("负担增加 %.0f位住户"%float(environment.get("harmed",0)),12,W.DANGER if float(environment.get("harmed",0))>0 else W.GREEN,false))
	env.tooltip_text="负担增加：现有住户的污染状态相对同一步现状预测上升超过0.015。人数加权暴露变化 %+.3f；最高场强增加 %.3f、减少 %.3f。"%[float(environment.get("exposure_delta",0)),float(environment.get("max_increase",0)),absf(float(environment.get("max_reduction",0)))]
	return root
