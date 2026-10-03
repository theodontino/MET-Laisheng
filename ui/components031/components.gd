extends RefCounted
const W=preload("res://ui/widgets.gd")
const D=preload("res://ui/components031/design.gd")
const Art=preload("res://ui/map/art.gd")
const Visual=preload("res://ui/components031/visual.gd")
const CardButton=preload("res://ui/components031/card_button.gd")
static func pad(parent:Node,space:int=16) -> VBoxContainer:
	var margin=MarginContainer.new();margin.add_theme_constant_override("margin_left",space);margin.add_theme_constant_override("margin_right",space);margin.add_theme_constant_override("margin_top",space);margin.add_theme_constant_override("margin_bottom",space);parent.add_child(margin)
	var v=W.vbox(margin,12);v.size_flags_horizontal=Control.SIZE_EXPAND_FILL;return v
static func group(parent:Node,title:String="",bg:Color=W.SURFACE) -> VBoxContainer:
	var p=W.panel(parent,bg);p.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	p.add_theme_stylebox_override("panel",W.box(bg,Color(0,0,0,0),12))
	var v=W.vbox(p,12)
	if title!="":v.add_child(W.label(title,16))
	return v
static func heading(parent:Node,title:String):
	var row=W.hbox(parent,8);row.add_child(W.label(title,16));return row
static func divider(parent:Node):
	var line=ColorRect.new();line.color=D.color("divider");line.custom_minimum_size.y=1;line.mouse_filter=Control.MOUSE_FILTER_IGNORE;parent.add_child(line)
static func icon(parent:Node,id:String,color:Color=W.TEXT,size:int=20):
	var tex=TextureRect.new();tex.texture=Art.icon(D.icon_id(id),W.ink_color(color));tex.custom_minimum_size=Vector2(size,size);tex.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;tex.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED;tex.mouse_filter=Control.MOUSE_FILTER_IGNORE;parent.add_child(tex);return tex
static func badge(parent:Node,text:String,ink:Color=W.MUTED,bg:Color=W.RAISED):
	var p=W.panel(parent,bg);var box=W.box(bg,Color(0,0,0,0),6);box.content_margin_left=8;box.content_margin_right=8;box.content_margin_top=4;box.content_margin_bottom=4;p.add_theme_stylebox_override("panel",box)
	p.size_flags_horizontal=Control.SIZE_SHRINK_END;p.size_flags_vertical=Control.SIZE_SHRINK_CENTER
	var l=W.label(text,14,ink,false);p.add_child(l);return l
static func socket_symbol(parent:Node,type:String,size:int=28):
	var v=Visual.new();v.kind="socket";v.slot_type=type;v.custom_minimum_size=Vector2(size,size);parent.add_child(v);return v
static func resource_row(parent:Node,id:String,label:String,value:String,unit:String="") -> Dictionary:
	var q=D.quantity(id);var row=W.hbox(parent,10);row.custom_minimum_size.y=32
	icon(row,str(q.icon),Color(q.ink),20);var name=W.label(label,16);row.add_child(name)
	var val=W.label(value,18,W.TEXT,false);val.size_flags_horizontal=Control.SIZE_SHRINK_END;row.add_child(val)
	var u=W.label(unit,14,W.MUTED,false);u.size_flags_horizontal=Control.SIZE_SHRINK_END;row.add_child(u)
	return {"root":row,"label":name,"value":val,"unit":u}
static func effect(parent:Node,row:Dictionary,domain:String):
	var role=str(row.get("role","effect"));var cost=role in ["cost","tradeoff"]
	var outer=W.panel(parent,Color("FFF4E5") if cost else W.RAISED)
	var box=W.box(Color("FFF4E5") if cost else W.RAISED,Color("F1D3A4") if cost else Color(0,0,0,0),8);box.content_margin_left=10;box.content_margin_right=10;box.content_margin_top=8;box.content_margin_bottom=8;outer.add_theme_stylebox_override("panel",box)
	var line=W.hbox(outer,8);icon(line,str(row.get("icon","info")),Color("825918") if cost else Color(D.domain(domain).ink),18)
	var prefix={"cost":"代价 · ","tradeoff":"取舍 · ","condition":"条件 · ","conversion":"转换 · "}.get(role,"")
	var l=W.label(prefix+str(row.get("text","")),16);line.add_child(l)
	return outer
static func bonus(parent:Node,g,bid:String,fn:Callable,density:String="full",context:Dictionary={}) -> Button:
	var data=D.card(g,bid);var dom=D.domain(data.get("visual_domain","general"))
	var b=CardButton.new();b.initialize();b.set_meta("bonus_id",bid);b.set_meta("label",data.name);b.tooltip_text=data.name+" · "+g.c.bonuses[bid].summary
	if fn.is_valid():b.pressed.connect(fn)
	parent.add_child(b);var v=b.content
	if density=="micro":
		var r=W.hbox(v,8);icon(r,str(dom.icon),Color(dom.ink),22);r.add_child(W.label(data.name,16,Color(dom.ink)))
		if context.get("instance_status","")!="":badge(v,context.instance_status)
	elif density in ["compact","installed"]:
		if density=="installed":
			var art=Visual.new();art.domain_id=data.get("visual_domain","general");art.asset=D.hero(data);art.custom_minimum_size.y=58;v.add_child(art)
		var r=W.hbox(v,8);icon(r,str(dom.icon),Color(dom.ink),22);r.add_child(W.label(data.name,17,Color(dom.ink)))
		var sub=W.label(context.get("provider_name",dom.label),14,W.MUTED);v.add_child(sub)
		if context.get("instance_status","")!="":badge(v,context.instance_status)
		var line=ColorRect.new();line.color=Color(dom.accent);line.custom_minimum_size.y=3;v.add_child(line)
	else:
		var top=W.hbox(v,6)
		for type in data.get("allowed_slots",[]):socket_symbol(top,type,26)
		var types=[]
		for type in data.get("allowed_slots",[]):types.append(D.slot_label(type))
		top.add_child(W.label(" / ".join(types) if not types.is_empty() else "区域方案",14,W.MUTED))
		var hosts=[]
		if context.get("host","")!="":hosts.append(D.host_label(context.host))
		else:
			for host in data.get("allowed_hosts",[]):hosts.append(D.host_label(host))
		if not hosts.is_empty():badge(top," / ".join(hosts))
		var hero=Visual.new();hero.domain_id=data.get("visual_domain","general");hero.asset=D.hero(data);hero.custom_minimum_size.y=54 if density=="offer" else 92;v.add_child(hero)
		v.add_child(W.label(data.name,21,Color(dom.ink)))
		if density=="full" and data.get("flavor","")!="":v.add_child(W.label(data.flavor,14,W.MUTED))
		var rows=data.get("effect_rows",[])
		if density=="offer":
			var neutral=[];var costs=[];var cost_ids=[]
			for e in rows:
				if e.role in ["cost","tradeoff"]:costs.append(e.text);cost_ids.append_array(e.get("rule_ids",[]))
				else:neutral.append(e)
			for e in neutral.slice(0,2):effect(v,e,data.visual_domain)
			if not costs.is_empty():effect(v,{"role":"cost","icon":"warning","text":"；".join(costs),"rule_ids":cost_ids},data.visual_domain)
			if neutral.size()>2:v.add_child(W.label("另有%d项条件 · 详情可查"%(neutral.size()-2),14,W.MUTED))
		else:
			for e in rows:effect(v,e,data.get("visual_domain","general"))
		if not data.get("rules_match",true):badge(v,"词条已变化 · 以完整规则为准",Color("815515"),Color("FFF1D8"))
		if context.has("provider_name"):
			divider(v);var r=W.hbox(v,6)
			if context.get("provider_org","")!="":icon(r,"org_"+context.provider_org,W.MUTED,20)
			r.add_child(W.label(context.provider_name,14,W.MUTED))
		if context.get("instance_status","")!="":badge(v,context.instance_status,Color("815515") if context.get("conditional",false) else W.GREEN,Color("FFF1D8") if context.get("conditional",false) else Color("E6F2EA"))
		if not context.has("provider_name"):badge(v,bid+" · "+dom.label)
	b.ignore_children();b.request_fit();return b
static func building(parent:Node,g,id:String,fn:Callable) -> Button:
	var def=g.c.projects[id];var domain=str(def.get("domain",D.building_domain(id)));var d=D.domain(domain)
	var b=CardButton.new();b.initialize();b.set_meta("label",def.name);b.set_meta("building_id",id);b.tooltip_text=def.name+" · %.0f 通货"%def.cost;b.pressed.connect(fn);parent.add_child(b)
	var hero=Visual.new();hero.kind="building";hero.domain_id=domain;hero.building_definition_id=id;hero.custom_minimum_size.y=64;b.content.add_child(hero)
	b.content.add_child(W.label(def.name,16,Color(d.ink)))
	var row=W.hbox(b.content,5);icon(row,"currency",Color("825918"),16);row.add_child(W.label("%.0f通货 · %.0f材料"%[def.cost,def.get("material",0)],16,Color("825918")))
	b.ignore_children();b.request_fit();return b
static func socket(parent:Node,g,host:String,slot:String,fn:Callable,micro:bool=false) -> Dictionary:
	var p=g.s.projects[host];var type=g.slot_type(host,slot);var iid=str(p.slots[slot])
	var outer=W.panel(parent,D.color("inset"));outer.set_meta("slot_id",slot);outer.set_meta("slot_type",type);outer.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	var box=W.box(D.color("inset"),D.color("outline"),10);box.content_margin_top=10;box.content_margin_bottom=10;outer.add_theme_stylebox_override("panel",box)
	var v=W.vbox(outer,8);var head=W.hbox(v,6);socket_symbol(head,type,28);head.add_child(W.label(g.slot_label(host,slot).replace("方案",""),16,W.MUTED))
	var button:Button
	if iid=="":
		button=W.flat(W.button("＋" if micro else "＋  部署许可",fn));button.custom_minimum_size.x=0;button.custom_minimum_size.y=48;button.set_meta("slot_id",slot);button.tooltip_text=g.slot_label(host,slot)+" · 空闲插槽，安装已获得的许可";v.add_child(button)
		var frame=Visual.new();frame.kind="dashed";frame.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);frame.offset_left=3;frame.offset_top=3;frame.offset_right=-3;frame.offset_bottom=-3;outer.add_child(frame)
	else:
		var ins=g.s.bonus_instances[iid];var status="已装载" if ins.get("active",false) else "已停用"
		button=bonus(v,g,ins.bonus_id,fn,"micro" if micro else "installed",{"instance_status":status});button.set_meta("slot_id",slot)
		var same=[]
		for key in g.slot_ids(host):
			var installed=str(p.slots.get(key,""))
			if installed!="" and str(g.s.bonus_instances.get(installed,{}).get("bonus_id",""))==str(ins.bonus_id):same.append(installed)
		var grade=str(g.Bonus.card(g,str(ins.bonus_id)).get("grade","基础"))
		v.add_child(W.label(grade+" · "+("首份 100%" if same.find(iid)<=0 else "追加 33⅓%"),13,W.GREEN))
	if not micro and type=="Flex":
		v.add_child(W.label("来自模块化设计 · 接受生产 / 物流",14,W.MUTED))
	if not micro and p.status!="running":v.add_child(W.label("施工中 · 可以查看，暂不可改造",14,W.MUTED))
	return {"root":outer,"button":button}
