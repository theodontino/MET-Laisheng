# MET 0.2.0 源码与呈现审查：证据摘录

日期：2026-09-25。基线是当前交付的 **0.2.0-b-buildlab.1**，不是旧恢复包，也不是另一套概念原型。

- 源码附件：`MET_B_0.2.0_BuildLab_Source.zip`。
- ZIP SHA256：`ad0d2ac4a4de97da3557cd51b11b03667bae572e73f2b75a5aab9d7d7b9f9dbc`。
- 包内 `evidence020/source-manifest020.json` 列出的源码与配置哈希：**29/29** 匹配。
- 图片依据：本包 `evidence020/020-03-first-settlement.png`、`020-04-tender.png`、`020-08-industrial-build.png`，并参照对话中的三张 `规则之城` 旧呈现。
- **版本差异**：对话内旧图标题为“规则之城”、DT上限80；当前源码标题为“构筑试验场”、上限96。不能拿旧图的60通货农场、11种按钮和80DT上限替代当前源码90通货、10种底盘、96DT的事实。
- 此次未改交付源码、未改GitHub；在独立探针目录复制原模拟模块做定点复现。运行日志：`probe_run_final.log`，Godot 4.7.2。没有把探针当真人试玩或新游戏版本。

## 直接复现与快照复核

1. **首帧缺失运营信息**：开局metrics有5岗位，但已就业0；能源字段不存在、Field数为0。UI用默认0显示。执行一个0.1本地小时Tick后，原世界可显示供电3、需求2.19、已就业5。这是未初始化运行展示，而非玩家真的没有电源。
2. **闲置预约不回流**：来源10材料；A/B各要8，先预约A=8、B=2；A因其他条件不能运行而实际用0，B仍只拿2，结算后8材料留仓。当前轮不会把A释放的额度再分给B。资源没有丢，但产生了可避免的短缺。
3. **9小时事件边界**：seed7保存为8.99999999999998小时，界面显示9.0；迁入事件计数只有2。再前进0.1h才触发第3次事件。3/6/9/12小时事件不应以浮点显示值作为可靠触发依据。
4. **同一终点不等于同一经济状态**：复核6份真实路线存档如下。它们使用同一建设清单和偏好策略；人口主要受同一出生率、定时迁入控制，不能据此判定所有Build等价。

| Seed | 人口 | 净现金流/h | 制成品净变化/h | 材料库存 | 岗位 | 用电/供电 | 学校运行率 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 7 | 32.71 | +17.01 | +9.06 | 23.46 | 36.5 | 11.64/11.91 | 2.2% |
| 18 | 32.71 | +11.41 | +3.54 | 37.39 | 37.5 | 10.12/13.10 | 0.0% |
| 31 | 32.71 | +3.84 | +4.58 | 28.28 | 35.0 | 10.13/13.80 | 30.2% |
| 49 | 32.71 | -0.57 | +2.77 | 5.49 | 35.0 | 10.14/12.13 | 84.1% |
| 67 | 32.71 | +20.70 | +11.18 | 22.93 | 36.5 | 11.62/15.00 | 2.2% |
| 88 | 32.71 | +13.12 | +9.05 | 23.90 | 36.0 | 11.63/13.08 | 51.4% |

### Seed7食物“+0.0/h”的真实组成

最近一Tick约0.1本地小时：产出1.2049208，消耗0.4905193，损耗0.7144015，库存102→102。因此折合每小时：**生产12.05、消耗4.91、丢弃7.14、净变化0**。主栏只给净变化，看不出产能过剩和仓储溢出。来源为该保存中的 `audit.resources.food`，不是估算另一套模型。

### 明确区分：事实、判断、修法

下面只摘录原代码。对“无聊”“难读”“缺少目标”的解释及修法属于修正计划的设计判断，不是这份代码自动证明的结论。未重新验证Mac、触控板、Retina或长时效能。


## S01｜目标与奖励


### `core/progress.gd:1–26`

```text
1: extends RefCounted
2: ## B progression kept as thin feedback, no mandatory upgrade / Fusion tree.
3: static func checklist(g) -> Array:
4: 	return [
5: 		{"name":"7个项目开始运营","done":g.s.totals.built>=7,"value":"%d / 7"%int(g.s.totals.built)},
6: 		{"name":"安顿24位真实居民","done":g.s.metrics.get("housed",0)>=24,"value":"%.0f / 24"%g.s.metrics.get("housed",0)},
7: 		{"name":"累计选择4份建筑/区域方案","done":g.s.totals.installed>=4,"value":"%d / 4"%int(g.s.totals.installed)},
8: 		{"name":"居民幸福至少55","done":g.s.metrics.get("happiness",0)>=55,"value":"%.0f / 55"%g.s.metrics.get("happiness",0)}]
9: static func after_commit(g) -> void:
10: 	var level=maxi(0,(int(g.s.totals.built)-2)/3)
11: 	if level>int(g.s.award_level):
12: 		g.s.block_opportunities+=level-int(g.s.award_level);g.s.award_level=level
13: 		g.log_event("scope_opportunity",{},"新的区域/组织方案机会已开放。一次只能选一项，不是无限累积免费全局加成。")
14: 	if g.s.stage==0 and g.s.totals.built>=4:g.s.stage=1
15: 	if g.s.goods_active:g.s.stage=2
16: 	if not g.s.complete and checklist(g).all(func(x):return x.done):
17: 		g.s.complete=true;g.log_event("first_block_ready",{},"首块实验目标达成。可以继续构筑；这不是好玩与否的自动判定。")
18: 	if not g.s.crisis.get("active",false) and (minf(g.s.mandate,g.s.support)<15 or maxf(g.s.mandate,g.s.support)<30):
19: 		g.s.crisis={"active":true};g.log_event("crisis",{},"治理危机：可以修复生活与财政，或主动离任。不会自动删除城市。")
20: static func crisis(g,choice:String) -> String:
21: 	if not g.s.crisis.get("active",false):return "当前没有治理危机。"
22: 	if choice=="repair":
23: 		if g.s.currency<80:return "整改需要80通货。"
24: 		g.s.currency-=80;g.s.mandate=maxf(50,g.s.mandate);g.s.support=maxf(40,g.s.support);g.s.crisis={};return ""
25: 	if choice=="leave":g.s.phase="failed";g.s.failed_reason="交出本Block治理权，城市留作只读记录。";return ""
26: 	return "无效选项。"
```


## S02｜开局状态与固定三槽


### `core/game.gd:18–31`

```text
18: func new_game(seed_value:int,now:float) -> void:
19: 	var v=absi(seed_value)%2147483000+1
20: 	s={"schema":3,"build":c.data.build,"seed":seed_value,"phase":"select_block","block_index":-1,
21: 		"rng":{"opening":v,"draft":(v+1701)%2147483646+1,"events":(v+5303)%2147483646+1},
22: 		"wall_mark":now,"global_now":now,"started_at":now,"local_hours":0.,"remainder":0.,"simulation_tick":0,
23: 		"currency":float(c.balance.initial_money),"dt":float(c.balance.initial_dt),"support":75.,"mandate":75.,
24: 		"block":{"id":"block:1","traits":["block.river_valley"],"labels":[],"policies":{"Tax":"tax_head","Labor":"jobs_access","Environment":"pollution_default","Procurement":"procure_near","Land":"land_normal"}},
25: 		"cells":{},"sites":{},"projects":{},"groups":{},"organizations":{},"fields":{},"authored_fields":{},"bonus_instances":{},
26: 		"next_id":1,"origin_choices":[],"origin":"","draft":{},"tender_debt":0,"log":[],"events":[],"event_cursor":0,
27: 		"metrics":{},"audit":{},"complete":false,"failed_reason":"","crisis":{},"governance_bad_hours":0.,
28: 		"totals":{"built":0,"installed":0,"rerolls":0,"dt_used":0.,"currency_spent":0.},"block_opportunities":0,"award_level":0,
29: 		"debug_used":false,"settings":{"sound":true,"lab_speed":1.},"migration_paused":false,"knowledge":0.,"stage":0,"goods_active":false}
30: 	for oid in Rules.ids(c.operators):s.organizations[oid]={"id":oid,"traits":c.operators[oid].traits.duplicate(),"labels":[]}
31: 	compiled={};log_event("new_game",{"seed":seed_value},"B 0.2.0：从同一底盘开始，构筑由规则形成。")
```


### `core/game.gd:52–83`

```text
52: func choose_origin(id:String) -> String:
53: 	if s.phase!="origin" or not id in s.origin_choices:return "不是本次起源。"
54: 	var origin=c.origins[id];s.origin=id;s.phase="play";s.currency+=float(origin.cash);s.dt+=float(origin.dt)
55: 	_create_special("municipal","市政储备",false)
56: 	_create_special("homeless","待安置住区",true)
57: 	for r in ["food","material","goods"]:s.projects.municipal.inventory[r]=float(c.balance.initial_stock[r])+float(origin.get(r,0))
58: 	var home=_create_starter("hamlet",["0,1"],"commons")
59: 	var anchor={"village":"farm","charter":"power","miners":"timber","free":"timber"}[id]
60: 	_create_starter(anchor,["0,0" if anchor!="timber" else "1,1"],"civic" if id=="charter" else ("farmers" if anchor=="farm" else "steel"))
61: 	Population.create(self,home,float(c.balance.initial_people))
62: 	refresh_metrics();log_event("origin",{"id":id},"18位居民已安顿。直接选择基础建筑，比较运营商与初始方案。")
63: 	return ""
64: func _create_special(id:String,name:String,home:bool) -> void:
65: 	var base=c.projects.hamlet.stats.duplicate(true)
66: 	for k in base:base[k]=0.
67: 	base.efficiency=1.;base.personnel_mult=1.;base.exposure_multiplier=1.;base.shortage_multiplier=1.;base.population_maintenance=float(c.balance.population_maintenance)
68: 	base.population_food=.13;base.population_material=.025;base.population_goods=.025;base.population_energy=.08
69: 	for r in ["food","material","goods"]:base["storage."+r]=0. if home else float(c.balance.municipal_capacity[r])
70: 	base.housing=0. if not home else 1000000.;base["output.energy"]=float(c.balance.base_energy) if not home else 0.
71: 	s.projects[id]={"id":id,"name":name,"def":"__"+id,"tile":"offmap:"+id,"site":"","traits":["building.residential"] if home else ["building.warehouse"],"labels":[],"operator_org":"civic","special":true,"base_stats":base,"inventory":{"food":0.,"material":0.,"goods":0.},"status":"running","paused":false,"job":{},"slots":{},"usage":1.,"bottleneck":"","next_heat_bonus":0.,"next_heat_credit":0.,"outputs_last":{},"history":[]}
72: func _create_starter(def_id:String,cells:Array,operator:String="commons") -> String:
73: 	return create_building(def_id,str(cells[0]),operator,true).id
74: func create_building(def_id:String,tile:String,operator:String,ready:bool=false) -> Dictionary:
75: 	var def=c.projects[def_id];var id=uid("B");var site=uid("S")
76: 	var p={"id":id,"def":def_id,"tile":tile,"site":site,"traits":def.traits.duplicate(),"labels":[],"operator_org":operator,"special":false,
77: 		"inventory":{"food":0.,"material":0.,"goods":0.},"slots":{"Production":"","Logistics":"","Operations":""},"level":0,
78: 		"status":"running" if ready else "construction","paused":false,"job":{},"usage":1. if ready else 0.,"bottleneck":"尚未运行",
79: 		"next_heat_bonus":0.,"next_heat_credit":0.,"outputs_last":{},"history":[],"effective_hours":0.,"size_scale":1.}
80: 	if not ready:p.job={"work":float(def.hours),"total":float(def.hours),"material":float(def.material),"energy":.5}
81: 	s.projects[id]=p;s.sites[site]={"id":site,"cells":[tile],"project_ids":[id],"zone":def.zone}
82: 	s.cells[tile].building=id;s.cells[tile].site=site;s.cells[tile].zone=def.zone
83: 	if ready:s.totals.built+=1
```


### `core/game.gd:218–229`

```text
218: func refresh_metrics() -> void:
219: 	if s.projects.is_empty():return
220: 	for id in playable_ids():s.projects[id].labels.erase("use.commercial")
221: 	compiled=Rules.compile(self)
222: 	var changed=false
223: 	for id in playable_ids():
224: 		if compiled.config[id].get("commercial_use",false):
225: 			s.projects[id].labels.append("use.commercial");changed=true
226: 	if changed:compiled=Rules.compile(self)
227: 	var m=s.metrics.duplicate(true)
228: 	m.merge(Population.summary(self),true)
229: 	s.metrics=m
```


## S03｜基础建筑全开、悬停只有标签


### `ui/app.gd:131–185`

```text
131: func refresh_left():
132: 	W.clear(left)
133: 	left.add_child(W.label(BLOCKS[maxi(0,int(game.s.block_index))],19,W.TEXT))
134: 	left.add_child(W.label("单格 / 单等级 · 三个方案槽",11,W.GOLD))
135: 	for pair in [["inspect","1  观察"],["site","2  选择地块"]]:
136: 		var b=W.button(pair[1],func():set_mode(pair[0]));left.add_child(b)
137: 		if mode==pair[0]:b.add_theme_stylebox_override("normal",W.button_box(Color("3c574f"),W.GOLD))
138: 	W.section(left,"先选基础建筑")
139: 	for id in ["hamlet","farm","timber","power","works","market","school","clinic","park","warehouse"]:
140: 		var d=game.c.projects[id]
141: 		var b=W.button(d.name,func():selected_base=id;mode="site";selected_project="";refresh(),false,d.description)
142: 		if selected_base==id:b.add_theme_stylebox_override("normal",W.button_box(Color("3c574f"),W.GOLD))
143: 		left.add_child(b)
144: 	left.add_child(W.label("已选地块："+(str(selection[0]) if not selection.is_empty() else "无"),12,W.MUTED))
145: 	left.add_child(W.label("招标只决定运营商与初始方案，不改基础底盘。",12,W.MUTED))
146: 	left.add_child(W.button("区域方案 · %d"%int(game.s.block_opportunities),func():Dialogs.scope_choice(self)))
147: 	left.add_child(W.button("已实现 Bonus 图鉴",func():Dialogs.catalog(self)))
148: 	left.add_child(W.button("恢复迁入" if game.s.migration_paused else "暂停迁入事件",func():game.toggle_migration();save_game();refresh()))
149: 	for pair in [["normal","地形"],["pollution","污染"],["heat","供热"],["operator","组织"]]:
150: 		left.add_child(W.button(pair[1]+"视图",func():map_view.layer=pair[0];map_view.queue_redraw()))
151: func refresh_stats():
152: 	cash_label.text="%.0f"%game.s.currency;dt_label.text="%.1f / %.0f"%[game.s.dt,game.c.balance.dt_cap]
153: 	cash_label.add_theme_color_override("font_color",W.RED if game.s.currency<0 else W.TEXT)
154: 	governance_label.text="%.0f / %.0f"%[game.s.mandate,game.s.support]
155: 	clock_label.text="本地 %.1fh · 1 Tick = 0.1h · 未操作时自然1:1"%game.s.local_hours
156: 	for pair in pulse_buttons:
157: 		var b=pair[0];var h=pair[1];b.text="推进%sh\n约%.1f DT"%[str(h),game.temporal_load()*h];b.disabled=game.s.phase!="play" or game.s.dt<.01
158: 	W.clear(resource_row)
159: 	var stock=W.vbox(resource_row,2);stock.size_flags_horizontal=Control.SIZE_EXPAND_FILL
160: 	stock.add_child(W.label("区域资源总览",12,W.GOLD,false))
161: 	for r in ["food","material","goods"]:
162: 		if r=="goods" and not game.s.goods_active:continue
163: 		stock.add_child(W.label("%s %.1f  %+.1f/h"%[game.c.RESOURCE_NAMES[r],game.inventory_total(r),game.s.metrics.get("flows",{}).get(r,0)],12,W.TEXT,false))
164: 	stock.add_child(W.button("来源与收支",func():Dialogs.resources(self)))
165: 	var people=W.vbox(resource_row,2);people.size_flags_horizontal=Control.SIZE_EXPAND_FILL;var m=game.s.metrics
166: 	people.add_child(W.label("居民 %.1f / 住房 %.0f"%[m.get("population",0),m.get("housing",0)],13,W.GREEN,false))
167: 	people.add_child(W.label("健康 %.0f · 幸福 %.0f"%[m.get("health",0),m.get("happiness",0)],12,W.TEXT,false))
168: 	people.add_child(W.label("就业 %.1f / %.1f"%[m.get("employed",0),m.get("population",0)],12,W.TEXT,false))
169: 	people.add_child(W.label("能源 %.1f / %.1f"%[m.get("energy_need",0),m.get("energy",0)],12,W.GOLD,false))
170: 	people.add_child(W.button("人群与工作",func():Dialogs.population(self)))
171: func set_mode(value:String):
172: 	mode=value
173: 	if mode!="inspect":selected_project=""
174: 	message("点一块空地，再比较三份投标。" if value=="site" else "点建筑查看方案、来源与实际运行率。")
175: 	refresh()
176: func project_for_cell(key:String) -> String:return str(game.s.cells.get(key,{}).get("building",""))
177: func on_cell(key:String,_dragging:bool):
178: 	var id=project_for_cell(key)
179: 	if id!="":selected_project=id;mode="inspect";selection=[];refresh();return
180: 	if game.s.phase!="play":return
181: 	if "tile.water" in game.s.cells[key].traits:message("水面不能直接放基础建筑。请选择陆地。");return
182: 	selection=[key];selected_project="";mode="site";refresh()
183: func on_hover(key:String):
184: 	if key=="" or not game.s.cells.has(key):return
185: 	toast.text=key+" · "+game.c.tags_text(game.s.cells[key].traits)
```


## S04｜招标文本与全局挂起报价


### `ui/dialogs.gd:35–72`

```text
35: static func tender(app):
36: 	var g=app.game;var d=g.s.draft
37: 	if d.is_empty():return
38: 	var base=g.c.projects.get(d.base,{})
39: 	var title="为「"+base.get("name","")+"」选择运营方案" if d.kind=="development" else ("调整建筑方案，不升级建筑等级" if d.kind=="refit" else "一次区域机会 · 选一套规则")
40: 	app.open_modal(title,"三份方案已经锁定。返回地图可继续经营；同一基础底盘、同样基础造价，差异来自运营商规则与Bonus。")
41: 	var row=W.hbox(app.modal_body,12)
42: 	for offer in d.offers:
43: 		var b=g.c.bonuses[offer.bonus_id];var org=g.c.operators[offer.provider_org];var price=g.Draft.offer_cost(g,offer)
44: 		var v=card(row,b.name,org.name+" · "+(g.c.SLOT_NAMES.get(offer.slot,offer.slot) if d.host_type=="building" else {"block":"Block方案","organization":"组织方案"}[d.host_type]),b.summary)
45: 		if d.kind=="development":
46: 			W.section(v,"这栋建筑由谁运营",org.summary)
47: 		elif d.kind=="refit":
48: 			var operator=g.c.operators[g.s.projects[d.host].operator_org]
49: 			W.section(v,"提供者 ≠ 运营者","建筑仍由「"+operator.name+"」运营。本次方案提供者为「"+org.name+"」。"+("自营关系成立。" if operator.id==org.id else "外部方案，不自动转换运营权。"))
50: 		W.section(v,"规则会读哪些东西",g.c.tags_text(b.traits) if not b.traits.is_empty() else "符合挂载与槽位条件的对象")
51: 		if d.kind=="development":
52: 			var st=base.stats
53: 			var lines=["岗位 %.0f · 能源 %.1f · 维护 %.1f/h"%[st.jobs,st.energy_demand,st.maintenance]]
54: 			if st.housing>0:lines.append("住房容量 %.0f人"%st.housing)
55: 			for resource in ["food","material","goods","energy"]:
56: 				if st.get("output."+resource,0)>0:lines.append("产出"+g.c.RESOURCE_NAMES[resource]+" %.1f/h"%st["output."+resource])
57: 				if st.get("input."+resource,0)>0:lines.append("消耗"+g.c.RESOURCE_NAMES[resource]+" %.1f/h"%st["input."+resource])
58: 			for service in g.c.balance.service_kinds:
59: 				if st["service."+service]>0:lines.append(g.c.SERVICE_NAMES[service]+"容量 %.0f人"%st["service."+service])
60: 			lines.append("材料 %.0f · 工期 %.1fh"%[price.material,price.hours])
61: 			W.section(v,"相同的基础底盘","\n".join(lines))
62: 		v.add_child(W.button("采纳方案 · %.0f通货"%price.currency,func():app.accept_offer(offer.id),g.s.currency<price.currency))
63: 	var buttons=W.hbox(app.modal_body,10);var rc=g.Draft.reroll_cost(g)
64: 	buttons.add_child(W.button("重投 · %.0f通货 / %.1fDT"%[rc.currency,rc.dt],func():
65: 		var e=g.reroll()
66: 		if e=="":app.save_game();tender(app);app.refresh_stats()
67: 		else:app.modal_subtitle.text=e))
68: 	buttons.add_child(W.button("保留报价，返回地图",func():app.close_modal()))
69: 	buttons.add_child(W.button("付重投价撤回",func():
70: 		var e=g.cancel_draft()
71: 		if e=="":app.save_game();app.close_modal()
72: 		else:app.modal_subtitle.text=e))
```


### `core/tenders.gd:5–27`

```text
5: static func common(g) -> String:
6: 	if g.s.phase!="play":return "当前不可建设。"
7: 	if not g.s.draft.is_empty():return "先处理已锁定的方案；关闭面板不会重抽。"
8: 	return ""
9: static func open_build(g,cells:Array,base:String) -> String:
10: 	var e=common(g)
11: 	if e!="":return e
12: 	if cells.size()!=1:return "0.2.0只选一个地块；多格开发已暂时关闭。"
13: 	var tile=str(cells[0])
14: 	if not g.s.cells.has(tile) or g.s.cells[tile].building!="" or "tile.water" in g.s.cells[tile].traits:return "请选择未开发的陆地。"
15: 	if not g.c.projects.has(base):return "请选择基础建筑。"
16: 	g.s.draft={"kind":"development","base":base,"tile":tile,"host_type":"building","host":"","slot":"","offers":[]}
17: 	g.s.draft.offers=draw(g,g.s.draft)
18: 	return ""
19: static func open_refit(g,id:String,slot:String) -> String:
20: 	var e=common(g)
21: 	if e!="":return e
22: 	if not g.s.projects.has(id) or g.s.projects[id].special or g.s.projects[id].status!="running":return "建筑完工后才能调整方案。"
23: 	var p=g.s.projects[id]
24: 	if not p.slots.has(slot):return "插槽不存在。"
25: 	var candidates=eligible(g,{"kind":"refit","base":p.def,"tile":p.tile,"host_type":"building","host":id,"slot":slot})
26: 	if candidates.is_empty():return "没有适用于此槽的未安装方案。"
27: 	g.s.draft={"kind":"refit","base":p.def,"tile":p.tile,"host_type":"building","host":id,"slot":slot,"offers":[]};g.s.draft.offers=draw(g,g.s.draft);return ""
```


### `core/game.gd:103–108`

```text
103: func reroll() -> String:return Draft.reroll(self)
104: func cancel_draft() -> String:
105: 	if s.draft.is_empty():return ""
106: 	var cost=Draft.reroll_cost(self)
107: 	if s.currency<cost.currency or s.dt<cost.dt:return "撤回重投需要支付当前重投代价，避免免费洗牌。"
108: 	s.currency-=cost.currency;s.dt-=cost.dt;s.tender_debt+=1;s.draft={};return ""
```


## S05｜右栏信息顺序及每秒重建


### `ui/panels.gd:1–66`

```text
1: extends RefCounted
2: const W=preload("res://ui/widgets.gd")
3: static func resource_name(r:String) -> String:return {"food":"食物","material":"材料","goods":"低级制成品","energy":"能源"}.get(r,r)
4: static func inspector(app):
5: 	var g=app.game;var v=app.inspector;var scroll=app.inspector_scroll.scroll_vertical
6: 	W.clear(v)
7: 	if not g.s.projects.has(app.selected_project):
8: 		W.section(v,"B 0.2.0 / 构筑，不是升级树")
9: 		v.add_child(W.label("先决定建什么，\n再决定怎么做。",23,W.TEXT))
10: 		v.add_child(W.label("所有同类型投标的基础造价、岗位、产能一致。比较的是运营商、初始方案和它将占用的槽位。",14,W.MUTED))
11: 		W.section(v,"这座城正在遇到什么")
12: 		var m=g.s.metrics
13: 		if m.get("homeless",0)>.1:v.add_child(W.label("%.1f人待安置。造住房或选择增加容量的方案。"%m.homeless,14,W.RED))
14: 		if m.get("jobs",0)>m.get("population",0):v.add_child(W.label("岗位多于人口。盲目再造工厂会分散有限劳力。",14,W.RED))
15: 		if m.get("flows",{}).get("food",0)<0:v.add_child(W.label("粮食净消耗；农场、家庭农场或都市农业是不同解法。",14,W.GOLD))
16: 		if m.get("energy_need",0)>m.get("energy",0):v.add_child(W.label("能源不足。发电、余热回用、人口幸福都可能形成不同路线。",14,W.GOLD))
17: 		W.section(v,"首块纪念（不是强制套装）")
18: 		for row in g.Progress.checklist(g):v.add_child(W.label(("✓ " if row.done else "○ ")+row.name+"  "+row.value,12,W.GREEN if row.done else W.MUTED))
19: 		W.section(v,"最近发生的事")
20: 		for i in range(g.s.log.size()-1,maxi(-1,g.s.log.size()-5),-1):
21: 			if g.s.log[i].text!="":v.add_child(W.label(g.s.log[i].text,12,W.MUTED))
22: 		return
23: 	var id=app.selected_project;var p=g.s.projects[id];var st=g.project_stats(id)
24: 	v.add_child(W.label(g.project_name(id),23,W.TEXT));v.add_child(W.label(g.c.tags_text(p.traits),12,W.GOLD))
25: 	v.add_child(W.label("运营："+g.c.operators[p.operator_org].name,14,W.GREEN))
26: 	v.add_child(W.label(g.c.operators[p.operator_org].summary,12,W.MUTED))
27: 	if not p.job.is_empty():
28: 		W.section(v,"施工","剩余 %.2fh / 材料 %.1f；缺供减速。"%[p.job.work,p.job.material])
29: 		v.add_child(W.bar(1-float(p.job.work)/maxf(.001,float(p.job.total))))
30: 	W.section(v,"当前运行","建筑效率 %.0f%% × 运行率 %.0f%%\n限制：%s"%[100*st.efficiency,100*p.usage,p.bottleneck])
31: 	W.section(v,"三类方案槽 · 运营商不占槽")
32: 	for slot in ["Production","Logistics","Operations","Flex"]:
33: 		if not p.slots.has(slot):continue
34: 		var iid=p.slots[slot];var label=g.c.SLOT_NAMES.get(slot,slot)
35: 		if iid!="":
36: 			var ins=g.s.bonus_instances[iid];var b=g.c.bonuses[ins.bonus_id]
37: 			v.add_child(W.label(label+"："+b.name,15,W.GOLD))
38: 			v.add_child(W.label("提供："+g.c.operators[ins.provider_org].name+(" · 自营" if ins.provider_org==p.operator_org else " · 外部方案"),11,W.MUTED))
39: 		else:v.add_child(W.label(label+"：空",14,W.MUTED))
40: 		v.add_child(W.button("查看 / 更换方案" if iid!="" else "征集「"+label+"」方案",func():
41: 			if iid!="":app.Dialogs.slot_detail(app,id,slot)
42: 			else:app.request_refit(id,slot),p.status!="running" or g.s.phase!="play"))
43: 	W.section(v,"实际运行","建筑效率 %.0f%% × 运行率 %.0f%%\n当前限制：%s"%[100*st.efficiency,100*p.usage,p.bottleneck])
44: 	var a=g.s.metrics.get("utilization",{}).get(id,{})
45: 	if not a.is_empty():v.add_child(W.label("劳力满足 %.0f%% / 物资 %.0f%% / 能源 %.0f%%"%[100*a.labor,100*a.resources,100*a.energy],12,W.MUTED))
46: 	for r in ["food","material","goods"]:
47: 		if st["input."+r]>0:v.add_child(W.label("额定输入 "+resource_name(r)+" %.2f/h"%st["input."+r],12,W.TEXT))
48: 		if st["output."+r]>0:v.add_child(W.label("额定输出 "+resource_name(r)+" %.2f/h；实际 %.2f/h"%[st["output."+r],p.outputs_last.get(r,0)],12,W.GREEN))
49: 	v.add_child(W.label("岗位 %.1f / 用能 %.1f / 固定维护 %.2f/h"%[st.jobs,st.energy_demand,st.maintenance],12,W.MUTED))
50: 	v.add_child(W.button("为什么：数值与来源",func():app.Dialogs.explain(app,id)))
51: 	W.section(v,"资源属于具体建筑")
52: 	for r in ["food","material","goods"]:v.add_child(W.label(resource_name(r)+" %.1f / %.0f"%[p.inventory.get(r,0),g.stock_cap_for(id,r)],12,W.TEXT))
53: 	var gid=g.Population.home_group(g.s,id)
54: 	if gid!="":
55: 		var group=g.s.groups[gid]
56: 		W.section(v,"住在这里的人群","%.1f / %.0f人 · 健康%.0f · 幸福%.0f\n教育%.0f · 人员效率%.0f%% · 需求%.2f"%[group.count,st.housing,group.health,group.happiness,group.education,100*group.efficiency,group.demand])
57: 		for b in group.employment:v.add_child(W.label("→ "+g.project_name(b)+" %.1f人"%group.employment[b],12,W.TEXT))
58: 	for k in g.c.balance.service_kinds:
59: 		if st["service."+k]>0:v.add_child(W.label(g.c.SERVICE_NAMES[k]+"额定容量 %.1f"%st["service."+k],13,W.GREEN))
60: 	W.section(v,"所在地的场 / 来源保留")
61: 	for k in g.s.cells[p.tile].fields:
62: 		if not k in ["pollution","heat"]:continue
63: 		var value=g.Rules.aggregate(g.s.cells[p.tile].fields[k]);v.add_child(W.label(("污染" if k=="pollution" else "余热")+" %.2f"%value,13,W.GOLD))
64: 	v.add_child(W.button("暂停运行" if not p.paused else "恢复运行",func():app.act(g.set_paused(id,not p.paused)),g.s.phase!="play"))
65: 	v.add_child(W.button("拆除…（原住户保留）",func():app.Dialogs.demolition(app,id),g.s.phase!="play"))
66: 	app.inspector_scroll.set_deferred("scroll_vertical",scroll)
```


### `ui/app.gd:218–228`

```text
218: func _process(delta):
219: 	if test_mode or game.s.is_empty() or closing:return
220: 	var now=Time.get_unix_time_from_system()
221: 	if now-last_wall>8:game.s.wall_mark=last_wall;game.resume_time(now)
222: 	elif game.s.phase=="play" and not overlay.visible:game.advance_online(minf(delta,2.)/3600.*float(game.s.settings.lab_speed))
223: 	last_wall=now;game.update_global(now);save_elapsed+=delta;ui_elapsed+=delta
224: 	if save_elapsed>=5:save_elapsed=0;save_game()
225: 	if ui_elapsed>=1:
226: 		ui_elapsed=0;refresh_stats()
227: 		if save_path=="user://input_smoke.json":dump_input_layout()
228: 		if not overlay.visible:Panels.inspector(self)
```


### `ui/widgets.gd:54–62`

```text
54: 	var c=Control.new();c.size_flags_horizontal=Control.SIZE_EXPAND_FILL;c.size_flags_vertical=Control.SIZE_EXPAND_FILL;parent.add_child(c);return c
55: static func clear(parent:Node):
56: 	for child in parent.get_children():parent.remove_child(child);child.queue_free()
57: static func bar(value:float) -> ProgressBar:
58: 	var p=ProgressBar.new();p.min_value=0;p.max_value=1;p.value=clampf(value,0,1);p.show_percentage=false;p.custom_minimum_size.y=7
59: 	return p
60: static func section(parent:Node,title:String,body:String=""):
61: 	parent.add_child(label(title,13,GOLD))
62: 	if body!="":parent.add_child(label(body,13,MUTED))
```


## S06｜统一流量、转换产出与解释缺口


### `core/simulation.gd:144–183`

```text
144: 	for bid in R.ids(s.projects):
145: 		var p=s.projects[bid];var st=stats[bid];var a=activity[bid]
146: 		if p.special:continue
147: 		next.projects[bid].usage=a.utilization;next.projects[bid].bottleneck=a.bottleneck;next.projects[bid].outputs_last={};next.projects[bid].effective_hours+=a.utilization*h
148: 		if p.status=="construction":
149: 			var work=minf(h,float(p.job.work))*a.utilization
150: 			next.projects[bid].job.work=maxf(0,float(p.job.work)-work)
151: 			next.projects[bid].job.material=maxf(0,float(p.job.material)-float(actual.get("work:"+bid,{}).get("material",0)))
152: 			if next.projects[bid].job.work<.000001:completed.append(bid)
153: 		elif p.status=="running" and not p.paused:
154: 			for r in RES:
155: 				var raw=maxf(0,float(st["output."+r])*float(st.efficiency)*float(a.utilization))
156: 				var heat=raw*float(config[bid].get("redirect_product_heat",0));a.product_heat+=heat
157: 				var amount=(raw-heat)*h
158: 				produced[r]=float(produced.get(r,0))+amount
159: 				a.outputs[r]=amount/h;next.projects[bid].outputs_last[r]=amount/h
160: 				deposit(s,compiled,inventories,bid,r,amount,spilled,movements)
161: 	for cid in R.ids(recipe_claims):
162: 		var recipe=recipe_claims[cid].recipe;var bid=recipe_claims[cid].owner
163: 		var fraction=A.supply_ratio(claims[cid].needs,actual[cid])
164: 		for r in recipe.output:
165: 			if r=="energy":continue
166: 			var n=float(recipe.output[r])*float(recipe.rate)*fraction*h
167: 			produced[r]=float(produced.get(r,0))+n;deposit(s,compiled,inventories,bid,r,n,spilled,movements)
168: 	# Surplus energy can enter stock ONLY through explicitly declared storage capacity.
169: 	var extra=maxf(0,energy_supplied-energy_need)*h
170: 	for bid in R.ids(s.projects):
171: 		var room=maxf(0,float(stats[bid].get("storage.energy",0))-float(inventories[bid].get("energy",0)))
172: 		var n=minf(room,extra)
173: 		if n>0:inventories[bid].energy=float(inventories[bid].get("energy",0))+n;extra-=n
174: 	# 5. Actual emissions, quota-conserving conversions, finite services, then group state.
175: 	var env=F.environment(g,compiled,activity,h);var recycled=0.;var heat_used=0.;var heat_receipts=[]
176: 	for bid in R.ids(s.projects):
177: 		var cfg=config[bid];var p=s.projects[bid]
178: 		if not cfg.has("field_conversion") or p.status!="running" or p.paused:continue
179: 		var cv=cfg.field_conversion;var request=float(cv.amount)*float(cv.rate)*h*float(activity[bid].utilization)
180: 		var n=F.consume_local_pollution(env,p.tile,request,h);recycled+=n
181: 		for r in cv.output:
182: 			var result=n/float(cv.amount)*float(cv.output[r]);produced[r]=float(produced.get(r,0))+result
183: 			deposit(s,compiled,inventories,bid,r,result,spilled,movements)
```


### `core/simulation.gd:225–237`

```text
225: 	var flows={}
226: 	for r in RES:flows[r]=(total(inventories,r)-total(before,r))/h
227: 	var audit={"resources":{},"pollution_generated":env.pollution_produced,"pollution_recycled":recycled,"heat_generated":env.heat_produced,"heat_used":heat_used,"phase_order":["空间","分配","运行","生产仓储","场与人群","财政与结构"]}
228: 	for r in RES:
229: 		var a0=total(before,r);var a1=total(inventories,r);var output=float(produced.get(r,0));var input=float(receipt.consumed.get(r,0));var loss=float(spilled.get(r,0))
230: 		audit.resources[r]={"before":a0,"produced":output,"consumed":input,"lost":loss,"after":a1,"error":a0+output-input-loss-a1}
231: 		if absf(audit.resources[r].error)>.00001:return "资源守恒失败："+r
232: 	if recycled>env.pollution_produced+.00001 or heat_used>env.heat_produced+.00001:return "Field额度重复结算。"
233: 	next.audit=audit
234: 	next.metrics={"flows":flows,"income":(tax+rent+pollution_tax)/h,"upkeep":maintenance/h,"cashflow":(tax+rent+pollution_tax-maintenance)/h,"energy":energy_supplied,"energy_need":energy_need,
235: 		"service_links":service.links,"exposure_links":[],"employment_links":labor.links,"allocation":receipt.receipts,"stock_movements":movements,"spilled":spilled,"utilization":activity,
236: 		"service_pools":service.pools,"service_covered":service.covered,"pollution_recycled":recycled/h,"heat_used":heat_used/h,"heat_receipts":heat_receipts,
237: 		"tax_basis":{"mode":taxmode,"people":people,"production":production/h,"trade":trade/h,"pollution_fee":pollution_tax/h,"land_rent":rent/h}}
```


### `ui/dialogs.gd:122–149`

```text
122: static func resources(app):
123: 	var g=app.game;app.open_modal("资源真的在哪？", "Block内部自动流通，短缺时按来源距离和规则统一分配。先预约，实际运行才扣；没使用的预约不销毁。")
124: 	for r in ["food","material","goods"]:
125: 		W.section(app.modal_body,g.c.RESOURCE_NAMES[r]+" %.1f / %.0f"%[g.inventory_total(r),g.stock_cap(r)])
126: 		for id in g.Rules.ids(g.s.projects):
127: 			var n=float(g.s.projects[id].inventory.get(r,0))
128: 			if n>.01:app.modal_body.add_child(W.label(g.project_name(id)+"  %.2f"%n,13,W.TEXT))
129: 	var m=g.s.metrics
130: 	W.section(app.modal_body,"财政","收入 %.2f/h · 维护（建筑+人群） %.2f/h · 净现金流 %+.2f/h"%[m.get("income",0),m.get("upkeep",0),m.get("cashflow",0)])
131: 	W.section(app.modal_body,"最近一周期资源审计")
132: 	for r in g.s.audit.get("resources",{}):
133: 		var x=g.s.audit.resources[r]
134: 		app.modal_body.add_child(W.label("%s：%.3f + %.3f产出 − %.3f消耗 − %.3f溢出 = %.3f"%[g.c.RESOURCE_NAMES[r],x.before,x.produced,x.consumed,x.lost,x.after],13,W.MUTED))
135: 	W.section(app.modal_body,"回收和热量只有一份额度","最近污染回收 %.2f/h · 热量实际分配 %.2f/h；不会因为覆盖很多格就重复生产。热量回馈下一周期，避免当期自循环。"%[m.get("pollution_recycled",0),m.get("heat_used",0)])
136: static func explain(app,id:String):
137: 	var g=app.game;app.open_modal(g.project_name(id)+" · 为什么是这个数？","基础值、规则来源、空间关系与实际上班人数分别显示；没有预制组合总开关。")
138: 	for key in g.Rules.ids(g.compiled.trace.get(id,{})):
139: 		var x=g.compiled.trace[id][key];var label={"efficiency":"建筑效率","output.food":"额定食物产出","output.goods":"额定制成品产出","output.material":"额定材料产出","maintenance":"固定维护","service.education":"教育容量","service.medical":"医疗容量","heat":"收到余热的后续增益","spatial_synergy":"空间协同"}.get(key,key);var v=card(app.modal_body,label,"结果 %.3f"%float(x.get("value",0)))
140: 		for rule in x.get("rules",[]):v.add_child(W.label(str(rule.source)+" · "+str(rule.mode)+" "+str(rule.value),13,W.TEXT))
141: 	W.section(app.modal_body,"Field到这个地块：保留源身份")
142: 	for kind in g.Rules.ids(g.s.cells[g.s.projects[id].tile].fields):
143: 		var source={}
144: 		for f in g.s.cells[g.s.projects[id].tile].fields[kind]:
145: 			var key=f.source_id;source[key]=maxf(float(source.get(key,-INF)),float(f.value)) if kind in g.c.balance.service_kinds or kind=="employment" else float(source.get(key,0))+float(f.value)
146: 		for sid in source:app.modal_body.add_child(W.label(kind+" ← "+g.project_name(sid)+"  %.3f"%source[sid],12,W.MUTED))
147: 	W.section(app.modal_body,"实际领到了哪些资源")
148: 	for receipt in g.s.metrics.get("allocation",[]):
149: 		if receipt.consumer.ends_with(id):app.modal_body.add_child(W.label(resource_name(receipt.resource)+" %.3f ← "%receipt.used+g.project_name(receipt.source),12,W.TEXT))
```


## S07｜短缺预约及未用额度


### `core/allocation.gd:44–122`

```text
44: static func resources(s:Dictionary,compiled:Dictionary,claims:Dictionary,access:Dictionary) -> Dictionary:
45: 	var remaining=stocks(s);var grants={};var transfers=[];var pairs=[];var source_used={}
46: 	for cid in R.ids(claims):
47: 		grants[cid]={};var claim=claims[cid];var owner=claim.owner;var target=s.projects[owner]
48: 		var st=compiled.stats[owner];var conf=compiled.config[owner]
49: 		for resource in R.ids(claim.needs):
50: 			grants[cid][resource]=0.;source_used[cid+"|"+resource]={}
51: 			for sid in R.ids(s.projects):
52: 				var source=s.projects[sid]
53: 				if float(source.inventory.get(resource,0))<=0:continue
54: 				var distance=100. if source.get("special",false) or target.get("special",false) else float(Hex.distance(source.tile,target.tile))
55: 				if sid==owner:distance=0.
56: 				distance*=float(st.distance_cost)
57: 				if resource=="material":distance*=float(conf.get("material_distance",1))
58: 				if not access.tiles.get(target.tile,{}).get("drone",[]).is_empty():distance*=.4
59: 				var same_rank=0
60: 				if conf.get("same_supplier",false):same_rank=0 if source.operator_org==target.operator_org else 1
61: 				var score=float(st.allocation_priority)+float(claim.get("priority",0))
62: 				var group_distance=distance
63: 				if conf.get("group_procurement",false):
64: 					for other in R.ids(claims):
65: 						var op=s.projects[claims[other].owner]
66: 						if op.operator_org==target.operator_org and float(claims[other].needs.get(resource,0))>0 and not op.get("special",false) and not source.get("special",false):group_distance=minf(group_distance,Hex.distance(source.tile,op.tile))
67: 				pairs.append({"consumer":cid,"source":sid,"resource":resource,"priority":score,"same_rank":same_rank,"group_distance":group_distance,"distance":distance})
68: 	pairs.sort_custom(func(a,b):
69: 		if a.resource!=b.resource:return a.resource<b.resource
70: 		if a.priority!=b.priority:return a.priority>b.priority
71: 		if a.same_rank!=b.same_rank:return a.same_rank<b.same_rank
72: 		if a.group_distance!=b.group_distance:return a.group_distance<b.group_distance
73: 		if a.distance!=b.distance:return a.distance<b.distance
74: 		if a.source!=b.source:return a.source<b.source
75: 		return a.consumer<b.consumer)
76: 	var processed_groups={}
77: 	for pair in pairs:
78: 		var original=pair.consumer;var sid=pair.source;var r=pair.resource
79: 		var owner=claims[original].owner;var group=compiled.config[owner].get("group_procurement",false)
80: 		var consumers=[original]
81: 		if group:
82: 			var org=s.projects[owner].operator_org;var group_key=org+"|"+sid+"|"+r
83: 			if processed_groups.has(group_key):continue
84: 			processed_groups[group_key]=true;var members=[]
85: 			for cid in R.ids(claims):
86: 				var host=claims[cid].owner
87: 				if s.projects[host].operator_org!=org or not compiled.config[host].get("group_procurement",false) or not claims[cid].needs.has(r):continue
88: 				var distance=100. if s.projects[host].special or s.projects[sid].special else float(Hex.distance(s.projects[sid].tile,s.projects[host].tile))
89: 				members.append({"id":cid,"distance":distance,"priority":float(compiled.stats[host].allocation_priority)})
90: 			members.sort_custom(func(a,b):
91: 				if a.priority!=b.priority:return a.priority>b.priority
92: 				if a.distance!=b.distance:return a.distance<b.distance
93: 				return a.id<b.id)
94: 			consumers=[]
95: 			for x in members:consumers.append(x.id)
96: 		# A procurement group has obtained its turn; allocate the finite source
97: 		# among its members before the next external buyer, retaining every receipt.
98: 		for cid in consumers:
99: 			var need=maxf(0,float(claims[cid].needs[r])-float(grants[cid][r]));var available=float(remaining[sid].get(r,0))
100: 			var amount=minf(need,available)
101: 			if compiled.options.get("procure_mode","near")=="split":amount=minf(amount,float(claims[cid].needs[r])*.6)
102: 			if amount<=0:continue
103: 			grants[cid][r]+=amount;remaining[sid][r]-=amount
104: 			transfers.append({"consumer":cid,"source":sid,"resource":r,"reserved":amount,"distance":pair.distance,"priority":pair.priority,"procurement_group":s.projects[owner].operator_org if group else ""})
105: 
106: 	return {"grants":grants,"transfers":transfers,"remaining":remaining}
107: static func supply_ratio(needs:Dictionary,grants:Dictionary) -> float:
108: 	var result=1.
109: 	for r in needs:
110: 		if float(needs[r])>0:result=minf(result,float(grants.get(r,0))/float(needs[r]))
111: 	return clampf(result,0,1)
112: static func settle_resources(s:Dictionary,allocation:Dictionary,actual:Dictionary) -> Dictionary:
113: 	var inventory=stocks(s);var consumed={};var receipts=[]
114: 	var left=actual.duplicate(true)
115: 	for transfer in allocation.transfers:
116: 		var cid=transfer.consumer;var r=transfer.resource;var sid=transfer.source
117: 		var n=minf(float(left.get(cid,{}).get(r,0)),float(transfer.reserved))
118: 		if n<=0:continue
119: 		n=minf(n,float(inventory[sid].get(r,0)))
120: 		inventory[sid][r]-=n;left[cid][r]-=n;consumed[r]=float(consumed.get(r,0))+n
121: 		var receipt=transfer.duplicate();receipt.used=n;receipts.append(receipt)
122: 	return {"inventories":inventory,"consumed":consumed,"receipts":receipts}
```


### `core/simulation.gd:102–137`

```text
102: 	# 3. Utilization: shortest fulfilled input, with a documented non-linear penalty.
103: 	for bid in R.ids(s.projects):
104: 		var p=s.projects[bid];var st=stats[bid];var a={"utilization":0.,"labor":1.,"resources":1.,"energy":1.,"product_heat":0.,"outputs":{},"bottleneck":"停运"}
105: 		if p.special:activity[bid]=a;continue
106: 		var cid="work:"+bid if p.status=="construction" else "run:"+bid
107: 		if claims.has(cid):
108: 			a.resources=A.supply_ratio(claims[cid].needs,reserved.grants[cid]);a.energy=energy_ratio if energy_requests.get(bid,0)>0 else 1.
109: 			a.labor=1. if p.status=="construction" or st.jobs<=0 else minf(1,float(labor.effective[bid])/float(st.jobs))
110: 			var short=minf(a.labor,minf(a.resources,a.energy));a.utilization=pow(short,float(config[bid].get("utilization_exponent",1.15)))
111: 			if short<.99999:a.utilization=minf(a.utilization,float(st.jit_cap))
112: 			a.bottleneck="劳力/人员效率" if a.labor<=minf(a.resources,a.energy) else ("物资" if a.resources<=a.energy else "能源")
113: 			if short>.999:a.bottleneck="投入齐备"
114: 			# Storage backpressure limits the planned batch, never erases inputs retroactively.
115: 			if p.status=="running":
116: 				for r in RES:
117: 					var expected=float(st["output."+r])*float(st.efficiency)*a.utilization*h
118: 					if expected<=0:continue
119: 					var room=storage_room(s,compiled,bid,r)
120: 					if room<expected:a.utilization*=maxf(.1,room/expected);a.bottleneck="仓储压力（余量低；残余溢出会损耗）"
121: 			actual[cid]={}
122: 			for r in claims[cid].needs:actual[cid][r]=float(claims[cid].needs[r])*float(a.utilization)
123: 		activity[bid]=a
124: 	# Population consumes its own finite receipt, not an imaginary shared satisfaction scalar.
125: 	var people_inputs={}
126: 	for gid in R.ids(s.groups):
127: 		var cid="pop:"+gid;actual[cid]={};people_inputs[gid]={"energy":energy_ratio if population_energy[gid]>0 else 1.}
128: 		for r in claims[cid].needs:
129: 			actual[cid][r]=float(reserved.grants[cid].get(r,0))
130: 			people_inputs[gid][r]=minf(1,float(actual[cid][r])/maxf(.000001,float(claims[cid].needs[r])))
131: 	for cid in R.ids(recipe_claims):
132: 		var recipe=recipe_claims[cid].recipe;var bid=recipe_claims[cid].owner
133: 		var fraction=recipe_energy.get(cid,minf(A.supply_ratio(claims[cid].needs,reserved.grants[cid]),float(activity[bid].utilization)))
134: 		actual[cid]={}
135: 		for r in claims[cid].needs:actual[cid][r]=float(claims[cid].needs[r])*float(fraction)
136: 	# 4. Commit resource receipts to a private next-state and place actual output.
137: 	var receipt=A.settle_resources(s,reserved,actual);var inventories=receipt.inventories;var before=A.stocks(s);var produced={};var spilled={};var movements=[];var completed=[]
```


## S08｜人口增长、迁入事件、隐藏需求切换


### `core/simulation.gd:70–76`

```text
70: 	for gid in R.ids(s.groups):
71: 		var group=s.groups[gid];var st=stats[group.host];var n=float(group.count);var demand=float(group.demand)
72: 		var inputs={"food":n*float(st.population_food)*h,"material":n*float(st.population_material)*h}
73: 		inputs.food*=1-.2*float(kitchens[gid])/maxf(.001,n)
74: 		if s.goods_active:inputs.goods=n*float(st.population_goods)*demand*h
75: 		claims["pop:"+gid]={"owner":group.host,"needs":inputs}
76: 		population_energy[gid]=n*float(st.population_energy)*demand
```


### `core/simulation.gd:221–256`

```text
221: 	for id in completed:next.projects[id].status="running";next.projects[id].job={};next.totals.built+=1
222: 	next.local_hours+=h;next.simulation_tick=int(next.simulation_tick)+1
223: 	for group in next.groups.values():
224: 		if group.host!="homeless" and group.health>50 and group.happiness>40:group.count+=float(group.count)*float(g.c.balance.birth_per_hour)*h
225: 	var flows={}
226: 	for r in RES:flows[r]=(total(inventories,r)-total(before,r))/h
227: 	var audit={"resources":{},"pollution_generated":env.pollution_produced,"pollution_recycled":recycled,"heat_generated":env.heat_produced,"heat_used":heat_used,"phase_order":["空间","分配","运行","生产仓储","场与人群","财政与结构"]}
228: 	for r in RES:
229: 		var a0=total(before,r);var a1=total(inventories,r);var output=float(produced.get(r,0));var input=float(receipt.consumed.get(r,0));var loss=float(spilled.get(r,0))
230: 		audit.resources[r]={"before":a0,"produced":output,"consumed":input,"lost":loss,"after":a1,"error":a0+output-input-loss-a1}
231: 		if absf(audit.resources[r].error)>.00001:return "资源守恒失败："+r
232: 	if recycled>env.pollution_produced+.00001 or heat_used>env.heat_produced+.00001:return "Field额度重复结算。"
233: 	next.audit=audit
234: 	next.metrics={"flows":flows,"income":(tax+rent+pollution_tax)/h,"upkeep":maintenance/h,"cashflow":(tax+rent+pollution_tax-maintenance)/h,"energy":energy_supplied,"energy_need":energy_need,
235: 		"service_links":service.links,"exposure_links":[],"employment_links":labor.links,"allocation":receipt.receipts,"stock_movements":movements,"spilled":spilled,"utilization":activity,
236: 		"service_pools":service.pools,"service_covered":service.covered,"pollution_recycled":recycled/h,"heat_used":heat_used/h,"heat_receipts":heat_receipts,
237: 		"tax_basis":{"mode":taxmode,"people":people,"production":production/h,"trade":trade/h,"pollution_fee":pollution_tax/h,"land_rent":rent/h}}
238: 	for gid in R.ids(next.groups):
239: 		var home=next.groups[gid].host
240: 		for f in env.tiles.get(next.projects[home].tile,{}).get("pollution",[]):
241: 			next.metrics.exposure_links.append({"home":home,"source":f.source_id,"pollution":f.value,"noise":0.})
242: 	var happiness=0.
243: 	for group in next.groups.values():happiness+=float(group.count)*float(group.happiness)
244: 	happiness/=maxf(1,people)
245: 	next.support=clampf(lerpf(float(s.support),happiness,1-exp(-h*.15)),0,100)
246: 	var failing=happiness<30 or next.currency<0
247: 	next.governance_bad_hours=float(s.governance_bad_hours)+h if failing else maxf(0,float(s.governance_bad_hours)-h)
248: 	if next.governance_bad_hours>3:next.mandate=maxf(0,float(next.mandate)-h)
249: 	if produced.get("goods",0)>0:next.goods_active=true
250: 	g.s=next;g.compiled=compiled
251: 	while int(g.s.event_cursor)<g.c.balance.group_events_hours.size() and g.s.local_hours>=g.c.balance.group_events_hours[int(g.s.event_cursor)]:
252: 		g.s.event_cursor+=1
253: 		if not g.s.migration_paused:
254: 			Pop.create(g,"homeless",6)
255: 			g.log_event("arrival",{"count":6},"6名新居民抵达，优先填入空余住房。")
256: 	g.compiled=R.compile(g);Pop.rehouse(g);g.refresh_metrics();Progress.after_commit(g)
```


### `core/population.gd:62–88`

```text
62: static func update(g,next:Dictionary,c:Dictionary,inputs:Dictionary,service:Dictionary,env:Dictionary,labor:Dictionary,h:float) -> void:
63: 	var stage=int(g.s.stage)
64: 	for gid in R.ids(g.s.groups):
65: 		var old=g.s.groups[gid];var p=next.groups[gid];var host=g.s.projects[old.host];var st=c.stats[old.host];var n=float(old.count)
66: 		p.employment=labor.employment[gid].duplicate(true);p.services=service.allocations.get(gid,{}).duplicate(true)
67: 		var supplied=inputs.get(gid,{});var live={};var providers={}
68: 		for resource in ["food","material","goods","energy"]:live[resource]=float(supplied.get(resource,1))
69: 		for kind in g.c.balance.service_kinds:live[kind]=minf(1,float(service.covered.get(gid,{}).get(kind,0))/maxf(.001,n))
70: 		var pollution=R.aggregate(env.tiles.get(host.tile,{}).get("pollution",[]),str(c.config[old.host].get("pollution_aggregate","sum")))
71: 		var exposure=maxf(0,pollution)*float(st.exposure_multiplier)
72: 		var unemployment=float(labor.unemployed.get(gid,0))/maxf(1,n)
73: 		var med_happiness=0.
74: 		for link in service.links:
75: 			if link.group==gid and link.kind=="medical" and c.config[link.source].get("medical_happiness",true):med_happiness+=float(link.people)/maxf(1,n)
76: 		var foodlack=1-live.food;var materiallack=1-live.material;var energylack=1-live.energy;var goodslack=1-live.goods
77: 		var health_target=80.-42*foodlack-6*materiallack-6*energylack-9*exposure+15*live.medical
78: 		var happiness_target=76.-45*foodlack-8*materiallack-8*energylack-12*goodslack-12*unemployment-maxf(0,65-p.health)*.25-4*exposure
79: 		if stage>=1:happiness_target+=float(st.shortage_multiplier)*(10*live.commercial+12*live.entertainment+4*med_happiness-14)
80: 		if old.host=="homeless":health_target-=12;happiness_target-=24
81: 		p.health=clampf(lerpf(float(old.health),health_target,1-exp(-h*.22)),0,100)
82: 		p.happiness=clampf(lerpf(float(old.happiness),happiness_target,1-exp(-h*.35)),0,100)
83: 		p.education=clampf(float(old.education)+h*(5*live.education*(1-float(old.education)/100)-.03),0,100)
84: 		p.efficiency=clampf((.6+.4*float(p.health)/100)*(.9+.2*float(p.education)/100),.3,1.3)
85: 		p.demand=clampf(1+(float(p.education)-15)*.004,.85,1.4)
86: 		live.pollution=pollution;live.unemployment=unemployment;live.health_target=clampf(health_target,0,100);live.happiness_target=clampf(happiness_target,0,100)
87: 		p.living=live
88: 		next.knowledge+=float(service.covered.get(gid,{}).get("education",0))*.02*h
```


## S09｜仓容口径与溢出


### `core/game.gd:204–214`

```text
204: func inventory_total(resource:String) -> float:
205: 	var total=0.
206: 	for id in Rules.ids(s.projects):total+=float(s.projects[id].inventory.get(resource,0))
207: 	return total
208: func stock_cap_for(id:String,resource:String) -> float:
209: 	if compiled.is_empty():compiled=Rules.compile(self)
210: 	return Sim.storage_cap(compiled,id,resource)
211: func stock_cap(resource:String) -> float:
212: 	var total=0.
213: 	for id in Rules.ids(s.projects):total+=stock_cap_for(id,resource)
214: 	return total
```


### `core/simulation.gd:258–290`

```text
258: static func storage_cap(c:Dictionary,id:String,r:String) -> float:
259: 	var cap=maxf(0,float(c.stats[id].get("storage."+r,0)))
260: 	if id=="municipal":cap*=float(c.options.get("municipal_storage",1))
261: 	return cap
262: static func storage_room(s:Dictionary,c:Dictionary,producer:String,r:String) -> float:
263: 	var capacity=0.
264: 	for id in R.ids(s.projects):
265: 		var p=s.projects[id]
266: 		if id!=producer and not (id=="municipal" or "building.logistics" in p.traits or c.config[id].get("overflow_relay",false)):continue
267: 		capacity+=maxf(0,storage_cap(c,id,r)-float(p.inventory.get(r,0)))
268: 	return capacity
269: static func deposit(s:Dictionary,c:Dictionary,inventories:Dictionary,producer:String,r:String,amount:float,spilled:Dictionary,movements:Array) -> void:
270: 	if amount<=0:return
271: 	var targets=[producer];var secondary=[]
272: 	for id in R.ids(s.projects):
273: 		if id==producer:continue
274: 		var p=s.projects[id]
275: 		if p.status!="running":continue
276: 		var priority=99
277: 		if c.config[id].get("overflow_relay",false) and R.near(s,producer,id)==1:priority=0
278: 		elif "building.logistics" in p.traits:priority=1
279: 		elif id=="municipal":priority=2
280: 		if priority<99:secondary.append({"id":id,"priority":priority})
281: 	secondary.sort_custom(func(a,b):return a.priority<b.priority if a.priority!=b.priority else a.id<b.id)
282: 	for x in secondary:targets.append(x.id)
283: 	var left=amount
284: 	for id in targets:
285: 		var room=maxf(0,storage_cap(c,id,r)-float(inventories[id].get(r,0)));var n=minf(room,left)
286: 		if n<=0:continue
287: 		inventories[id][r]=float(inventories[id].get(r,0))+n;left-=n
288: 		if id!=producer:movements.append({"source":producer,"target":id,"resource":r,"amount":n})
289: 		if left<.000001:break
290: 	if left>0:spilled[r]=float(spilled.get(r,0))+left
```


## S10｜税收与维护


### `core/simulation.gd:199–215`

```text
199: 	# 6. Actual activity becomes fiscal tax bases. Structural changes only now commit.
200: 	var tax=0.;var taxmode=str(compiled.options.get("tax_mode","head"));var trade=0.;var production=0.;var people=0.;var maintenance=0.
201: 	for group in s.groups.values():people+=float(group.count);maintenance+=float(group.count)*float(stats[group.host].population_maintenance)*h
202: 	for bid in R.ids(s.projects):
203: 		var p=s.projects[bid]
204: 		if p.special:continue
205: 		maintenance+=maxf(0,float(stats[bid].maintenance))*h
206: 		trade+=float(service.delivered.get(bid+"|commercial",0))*h
207: 		trade+=float(service.delivered.get(bid+"|medical",0))*float(config[bid].get("medical_activity",0))*h
208: 	for r in RES:production+=float(produced.get(r,0))
209: 	match taxmode:
210: 		"head":tax=people*.85*h
211: 		"production":tax=production*1.35
212: 		"trade":tax=trade*.85
213: 	var rent=float(compiled.options.get("land_rent",0))*g.playable_ids().size()*h
214: 	var pollution_tax=env.pollution_produced*float(compiled.options.get("pollution_fee",0))
215: 	next.currency=snappedf(float(next.currency)+tax+rent+pollution_tax-maintenance,.000001)
```


## S11｜地形和邻接实际作用


### `core/rules.gd:163–186`

```text
163: 		# Real spatial synergy baseline: data selects categories; no named combo bonuses.
164: 		var count=0.;var industrial=0.;var commercial=0.
165: 		for other in ids(stats):
166: 			if near(s,id,other)!=1 or s.projects[other].status!="running" or s.projects[other].paused:continue
167: 			var t=semantic_traits(s,other)
168: 			if "building.industrial" in t:industrial+=1
169: 			if "building.commercial" in t:commercial+=1
170: 			var own=semantic_traits(s,id)
171: 			var compatible=("building.industrial" in own and "building.industrial" in t) or ("building.commercial" in own and "building.industrial" in t)
172: 			if (configs[id].get("medical_commercial_relation",false) and "building.commercial" in t) or (configs[other].get("medical_commercial_relation",false) and "building.commercial" in own):compatible=true
173: 			var amount=.1 if compatible else .1*float(configs[id].get("universal_adjacency",0))
174: 			if compatible and "building.industrial" in own and s.projects[id].operator_org==s.projects[other].operator_org:amount*=float(stats[id].get("synergy.same_industrial",1))
175: 			if compatible and "building.commercial" in own and "building.industrial" in t:amount*=float(configs[id].get("industrial_adjacency",1))
176: 			count+=amount
177: 		if count>0:
178: 			stats[id].efficiency*=1+count;trace[id]["spatial_synergy"]={"base":1.,"value":1+count,"rules":[{"source":"相邻建筑关系","mode":"multiply","value":1+count}]}
179: 		# Previous settlement result affects this tick; never recurse into current output.
180: 		var heat_bonus=float(s.projects[id].get("next_heat_bonus",0))
181: 		stats[id].efficiency*=1+heat_bonus
182: 		if heat_bonus>0:trace[id]["heat"]={"base":1.,"value":1+heat_bonus,"rules":[{"source":"上周期实际收到的余热","mode":"multiply","value":1+heat_bonus}]}
183: 		for k in stats[id]:
184: 			if not is_finite(float(stats[id][k])):push_error("Non-finite compiled stat "+id+":"+k)
185: 	for k in options:options[k]=options[k].value
186: 	return {"stats":stats,"config":configs,"recipes":recipes,"emitters":emitters,"trace":trace,"options":options}
```


### `content/core020.json:2838–2872`

```text
2838:   "world_rules": [
2839:     {
2840:       "op": "modify",
2841:       "phase": "spatial",
2842:       "field": "output.food",
2843:       "targets": "self",
2844:       "mode": "multiply",
2845:       "value": 1.25,
2846:       "when": {
2847:         "traits": [
2848:           "building.agricultural"
2849:         ],
2850:         "tile_traits": [
2851:           "tile.fertile"
2852:         ]
2853:       },
2854:       "id": "world.0"
2855:     },
2856:     {
2857:       "op": "modify",
2858:       "phase": "spatial",
2859:       "field": "output.material",
2860:       "targets": "self",
2861:       "mode": "multiply",
2862:       "value": 1.25,
2863:       "when": {
2864:         "traits": [
2865:           "building.extractive"
2866:         ],
2867:         "tile_traits": [
2868:           "tile.forest"
2869:         ]
2870:       },
2871:       "id": "world.1"
2872:     }
```


## S12｜地图图层、恒显名称、图形动画


### `ui/map_view.gd:79–113`

```text
79: 	for k in keys:
80: 		var t=game.s.cells[k];var pos=point(k)
81: 		var color=terrain_colors[t.terrain]
82: 		if t.zone!="":color=color.lerp(zone_colors[t.zone],.27)
83: 		if t.site!="":color=color.lightened(.045)
84: 		if layer in ["pollution","heat"]:
85: 			var value=game.Rules.aggregate(t.get("fields",{}).get(layer,[]))
86: 			color=color.lerp(Color("cc7f58") if layer=="pollution" else Color("efbd6b"),clampf(value/4.,0,.8))
87: 		elif layer=="operator" and t.get("building","")!="":
88: 			var orgs=game.Rules.ids(game.c.operators);var oi=orgs.find(game.s.projects[t.building].operator_org)
89: 			color=color.lerp(Color.from_hsv(float(oi)/8.,.4,.8),.6)
90: 		draw_colored_polygon(polygon(pos+Vector2(0,5*zoom),r*.965),Color("123e42"))
91: 		draw_colored_polygon(polygon(pos,r*.965),color)
92: 		var outline=polygon(pos,r*.965);outline.append(outline[0])
93: 		draw_polyline(outline,Color(0.06,.15,.17,.35),1.0,true)
94: 		if t.terrain=="water":
95: 			for w in 3:
96: 				var yy=pos.y+(w-1)*r*.36;var shift=sin(visual_time*.4+w+k.hash()%9)*r*.09
97: 				draw_line(Vector2(pos.x-r*.44+shift,yy),Vector2(pos.x+r*.44+shift,yy),Color(.38,.68,.73,.35),1.5,true)
98: 		elif t.site=="":
99: 			if t.terrain=="forest":
100: 				for offset in [Vector2(-.28,.10),Vector2(.24,.27),Vector2(.1,-.22)]:draw_tree(pos+offset*r,r*.36)
101: 			elif t.terrain=="hill":
102: 				draw_colored_polygon(PackedVector2Array([pos+Vector2(-.5,.28)*r,pos+Vector2(-.07,-.38)*r,pos+Vector2(.46,.26)*r]),Color("a4a08a"))
103: 				draw_line(pos+Vector2(-.07,-.38)*r,pos+Vector2(.13,.06)*r,Color("d3cdb5"),2,true)
104: 			else:
105: 				for i in 3:
106: 					var offset=Vector2(sin(float(k.hash()%23+i*2))*r*.46,cos(float(i*3))*r*.36)
107: 					draw_line(pos+offset,pos+offset+Vector2(2,-3)*zoom,Color(.72,.78,.57,.28),1,true)
108: 		if k in selected:
109: 			draw_colored_polygon(polygon(pos,r*.91),Color(1.0,.77,.36,.16))
110: 			draw_polyline(outline,Color("eac076"),2.5,true)
111: 		elif k==hovered:
112: 			draw_polyline(outline,Color("d9e9d9"),2,true)
113: 		if t.site=="" and t.zone!="":draw_circle(pos,3*zoom,zone_colors[t.zone])
```


### `ui/map_view.gd:145–167`

```text
145: 	for id in pids:
146: 		var p=game.s.projects[id];var def=game.c.projects[p.def];var pos=center_project(p)
147: 		var zone=def.zone;var color=zone_colors[zone]
148: 		var scale=zoom*(1.0+.08*mini(game.s.sites[p.site].cells.size()-1,3))
149: 		if id==selected_project:
150: 			for k in game.s.sites[p.site].cells:
151: 				var line=polygon(point(k),r*.93);line.append(line[0]);draw_polyline(line,Color("f6d28b"),2.5,true)
152: 		draw_building(pos,p,def,color,scale)
153: 		var text=def.name
154: 		var gid=game.Population.home_group(game.s,id)
155: 		if gid!="":
156: 			draw_string(font,pos+Vector2(-12,-34)*scale,"%.0f人"%game.s.groups[gid].count,HORIZONTAL_ALIGNMENT_LEFT,-1,int(11*zoom),Color("ecdbab"))
157: 		var fs=int(12*zoom)
158: 		var tw=font.get_string_size(text,HORIZONTAL_ALIGNMENT_LEFT,-1,fs).x
159: 		draw_style_box(name_box(),Rect2(pos+Vector2(-tw*.5-6,27*scale),Vector2(tw+12,20*zoom)))
160: 		draw_string(font,pos+Vector2(-tw*.5,42*scale),text,HORIZONTAL_ALIGNMENT_LEFT,-1,fs,Color("f0edda"))
161: 		if not p.job.is_empty():
162: 			var ratio=clampf(1.0-float(p.job.work)/maxf(.01,float(p.job.total)),0,1)
163: 			draw_rect(Rect2(pos+Vector2(-23,21)*scale,Vector2(46,3)*scale),Color("1b3135"))
164: 			draw_rect(Rect2(pos+Vector2(-23,21)*scale,Vector2(46*ratio,3)*scale),Color("e1b56e"))
165: 		elif p.paused:draw_circle(pos+Vector2(20,-25)*scale,5*scale,Color("a19889"))
166: 		elif p.slots.values().any(func(x):return x==""):
167: 			draw_circle(pos+Vector2(22,-29)*scale,4*scale,Color("e8c477"))
```


### `ui/map_view.gd:182–223`

```text
182: func draw_building(pos:Vector2,p:Dictionary,def:Dictionary,color:Color,scale:float):
183: 	draw_ellipse_shadow(pos+Vector2(5,7)*scale,Vector2(30,13)*scale)
184: 	if p.status=="construction":
185: 		draw_colored_polygon(PackedVector2Array([pos+Vector2(-22,0)*scale,pos+Vector2(0,-12)*scale,pos+Vector2(22,0)*scale,pos+Vector2(0,12)*scale]),Color("c7b894"))
186: 		for x in [-17,0,17]:
187: 			draw_line(pos+Vector2(x,4)*scale,pos+Vector2(x,-27)*scale,Color("d7bb82"),2*scale,true)
188: 		draw_line(pos+Vector2(-17,-27)*scale,pos+Vector2(17,-27)*scale,Color("d7bb82"),2*scale,true)
189: 		return
190: 	var height=(18+float(p.level)*9)*scale
191: 	match def.id:
192: 		"farm","vegetables":
193: 			for n in 4:draw_line(pos+Vector2(-24,-2+n*5)*scale,pos+Vector2(6,-15+n*5)*scale,Color("c9be79"),3*scale,true)
194: 			cube(pos+Vector2(13,4)*scale,19*scale,5*scale,height*.7,Color("bc9f74"))
195: 		"orchard":
196: 			for o in [Vector2(-17,3),Vector2(0,-8),Vector2(15,5)]:
197: 				draw_tree(pos+o*scale,16*scale)
198: 				draw_circle(pos+(o+Vector2(3,-9))*scale,3*scale,Color("d99865"))
199: 		"pasture":
200: 			draw_arc(pos,23*scale,0,TAU,20,Color("c6bc90"),2*scale,true)
201: 			for o in [Vector2(-10,2),Vector2(7,-5),Vector2(12,8)]:draw_circle(pos+o*scale,5*scale,Color("e5dfc5"))
202: 		"bamboo":
203: 			for o in [Vector2(-15,3),Vector2(0,-5),Vector2(15,4)]:draw_tree(pos+o*scale,20*scale)
204: 		"power","wind","solar":
205: 			cube(pos,26*scale,8*scale,height,color)
206: 			draw_circle(pos+Vector2(-20,-4)*scale,12*scale,Color("7c9ea2"));draw_arc(pos+Vector2(-20,-4)*scale,9*scale,0,TAU,20,Color("c0d6cb"),2*scale,true)
207: 			for i in 4:
208: 				var angle=visual_time*.5+i*PI*.5;draw_line(pos+Vector2(-20,-4)*scale,pos+Vector2(-20,-4)*scale+Vector2(cos(angle),sin(angle))*9*scale,Color("d0d9bd"),1.5*scale,true)
209: 		"park","square":
210: 			draw_circle(pos,21*scale,Color("709777"));draw_tree(pos+Vector2(-8,4)*scale,16*scale);draw_tree(pos+Vector2(9,0)*scale,12*scale)
211: 		_:
212: 			cube(pos,35*scale,10*scale,height,color)
213: 			if def.zone=="settlement":cube(pos+Vector2(-17,9)*scale,17*scale,5*scale,height*.6,color.lightened(.08))
214: 			if def.zone=="production":
215: 				cube(pos+Vector2(12,-5)*scale,7*scale,3*scale,height+12*scale,Color("8a958c"))
216: 			if def.zone=="public":
217: 				draw_line(pos+Vector2(0,-height),pos+Vector2(0,-height-15*scale),Color("ede2bf"),1.4*scale,true)
218: 				draw_colored_polygon(PackedVector2Array([pos+Vector2(0,-height-15*scale),pos+Vector2(11*scale,-height-12*scale),pos+Vector2(0,-height-9*scale)]),Color("edc281"))
219: 			for i in 3:draw_rect(Rect2(pos+Vector2(-12+i*7,-9)*scale,Vector2(3,4)*scale),Color("efdb9a"))
220: func draw_ellipse_shadow(center:Vector2,axes:Vector2):
221: 	var poly=PackedVector2Array()
222: 	for i in 20:poly.append(center+Vector2(cos(TAU*i/20)*axes.x,sin(TAU*i/20)*axes.y))
223: 	draw_colored_polygon(poly,Color(0,0,0,.17))
```


## S13｜报价仅资格和权重，无局面效果比较


### `core/tenders.gd:36–95`

```text
36: static func eligible(g,d:Dictionary) -> Array:
37: 	var result=[]
38: 	for bid in R.ids(g.c.bonuses):
39: 		var b=g.c.bonuses[bid]
40: 		if not d.host_type in b.hosts:continue
41: 		if d.host_type=="building":
42: 			var def=g.c.projects[d.base]
43: 			if not R.any_trait(R.semantic_traits(g.s,d.host) if d.host!="" else def.traits,b.traits):continue
44: 			var gate=b.get("gate",{})
45: 			var valid=true
46: 			for t in gate.get("tile_traits",[]):
47: 				if not t in g.s.cells[d.tile].traits:valid=false
48: 			if gate.get("has_inputs",false):
49: 				var found=false
50: 				for k in def.stats:
51: 					if k.begins_with("input.") and float(def.stats[k])>0:found=true
52: 				if not found:valid=false
53: 			if not valid:continue
54: 			if d.slot!="":
55: 				if d.slot=="Flex" and not b.slot in ["Production","Logistics"]:continue
56: 				if d.slot!="Flex" and not d.slot in b.allowed_slots:continue
57: 			if d.host!="":
58: 				var replaced=g.s.projects[d.host].slots.get(d.slot,"")
59: 				if g.bonus_eligible(bid,"building",d.host,d.slot,replaced)!="":continue
60: 		else:
61: 			if g.bonus_eligible(bid,d.host_type,d.host)!="":continue
62: 		result.append(bid)
63: 	return result
64: static func draw(g,d:Dictionary) -> Array:
65: 	var pool=eligible(g,d);var offers=[];var organizations=R.ids(g.c.operators);var used_org=[];var used_bonus=[]
66: 	for i in mini(3,pool.size()):
67: 		var org_options=[];var weights=[]
68: 		for oid in organizations:
69: 			if oid in used_org:continue
70: 			org_options.append(oid);weights.append(4. if d.base in g.c.operators[oid].preferred_buildings else 1.)
71: 		var oid=str(Rng.pick_weighted(g.s,"draft",org_options,weights));used_org.append(oid)
72: 		var available=[];weights=[]
73: 		for bid in pool:
74: 			if bid in used_bonus:continue
75: 			available.append(bid);var b=g.c.bonuses[bid]
76: 			var w=float(b.weight)*(4. if bid in g.c.operators[oid].preferred_bonuses else 1.)
77: 			if b.rules.any(func(r):return r.op=="slot_add"):w*=.2
78: 			weights.append(w)
79: 		var bid=str(Rng.pick_weighted(g.s,"draft",available,weights));used_bonus.append(bid)
80: 		offers.append({"id":"offer:"+str(i),"base":d.base,"operator_org":oid,"provider_org":oid,"bonus_id":bid,"slot":d.slot if d.slot!="" else g.c.bonuses[bid].slot})
81: 	return offers
82: static func reroll_cost(g) -> Dictionary:
83: 	var n=int(g.s.tender_debt)
84: 	return {"currency":12.+9.*n,"dt":0. if n<2 else .5*(n-1)}
85: static func reroll(g) -> String:
86: 	if g.s.draft.is_empty():return "尚未开标。"
87: 	var cost=reroll_cost(g)
88: 	if g.s.currency<cost.currency or g.s.dt<cost.dt:return "重投预算不足。"
89: 	g.s.currency-=cost.currency;g.s.dt-=cost.dt;g.s.tender_debt+=1;g.s.totals.rerolls+=1
90: 	g.s.draft.offers=draw(g,g.s.draft);return ""
91: static func offer_cost(g,offer:Dictionary) -> Dictionary:
92: 	var d=g.s.draft
93: 	if d.kind=="development":
94: 		var base=g.c.projects[d.base];return {"currency":float(base.cost),"material":float(base.material),"hours":float(base.hours)}
95: 	return {"currency":float(g.c.balance.block_offer_price if d.kind=="scope" else g.c.balance.bonus_offer_price),"material":0.,"hours":0.}
```


## S14｜固定建设策略而非六条自主发展路线


### `tests/playthrough.gd:9–25`

```text
9: const PREF={"hamlet":["O03","R18","R10","O02","M01"],"farm":["R15","P07","R14","M01","L05"],"timber":["L05","M01","L02","P06"],"power":["L05","M01","L02"],"works":["P01","P05","B09","M01","L03","O06"],"market":["R08","L01","O07","R10"],"school":["O04","P03","L04","M01"],"clinic":["P02","R05","O01","M01"],"park":["P04","L04","M01"],"warehouse":["L05","P06","M01","L02"]}
10: func has(base:String)->bool:
11: 	for id in g.playable_ids():
12: 		if g.s.projects[id].def==base:return true
13: 	return false
14: func pulse() -> bool:
15: 	var r=g.pulse(.5)
16: 	trace.append({"action":"DT","hours":r.advanced,"dt":r.spent,"money":g.s.currency,"people":g.s.metrics.population})
17: 	if r.advanced<.0001:failure="DT不足/推进失败："+r.reason;return false
18: 	if g.validate_state(g.s)!="":failure=g.validate_state(g.s);return false
19: 	return true
20: func choose(preference:Array) -> Dictionary:
21: 	var best=g.s.draft.offers[0];var best_rank=999
22: 	for offer in g.s.draft.offers:
23: 		var rank=preference.find(offer.bonus_id)
24: 		if rank<0:rank=100
25: 		if rank<best_rank:best_rank=rank;best=offer
```


### `tests/playthrough.gd:51–80`

```text
51: func play(seed_value:int)->Dictionary:
52: 	g=Game.new();trace=[];failure="";g.new_game(seed_value,1000000);g.select_block(0);g.choose_origin(g.s.origin_choices[0]);var ok=true
53: 	for pair in [["farm","-1,1"],["timber","0,0"],["power","1,0"],["hamlet","0,2"],["works","0,-1"],["market","1,2"],["clinic","-1,2"],["school","1,-1"],["park","-1,3"]]:
54: 		if pair[0]!="hamlet" and has(pair[0]):continue
55: 		if not build(pair[0],pair[1]):ok=false;break
56: 		# Actual production is now the tax basis. This is a paid public policy command.
57: 		if pair[0]=="works":
58: 			var e=g.switch_policy("tax_production")
59: 			if e!="":failure=e;ok=false;break
60: 	if ok and g.s.block_opportunities>0:
61: 		var e=g.create_block_offer()
62: 		if e!="":failure=e;ok=false
63: 		elif not settle(choose(["P05","B02","B09","B05","B06","B01","P01","B03"])):ok=false
64: 	if ok:
65: 		# One second-stage refit demonstrates Provider != Operator and finite slots.
66: 		var target=""
67: 		for id in g.playable_ids():
68: 			if g.s.projects[id].def=="works":target=id;break
69: 		var e=g.create_refit(target,"Logistics")
70: 		if e!="":failure=e;ok=false
71: 		elif not settle(choose(["L03","L05","L02"])):ok=false
72: 	if ok:
73: 		for i in 40:
74: 			if g.s.complete and g.inventory_total("goods")>.1:break
75: 			if not pulse():ok=false;break
76: 	ok=ok and g.s.complete and not g.s.debug_used and g.validate_state(g.s)==""
77: 	var report={"seed":seed_value,"success":ok,"error":failure,"origin":g.s.origin,"local_hours":g.s.local_hours,"dt":g.s.dt,"currency":g.s.currency,"people":g.s.metrics.population,"happiness":g.s.metrics.happiness,"jobs":g.s.metrics.jobs,"buildings":g.playable_ids().size(),"installed":g.s.totals.installed,"scope_opportunities":g.s.block_opportunities,"complete":g.s.complete,"debug":g.s.debug_used,"trace":trace}
78: 	FileAccess.open(Paths.file("route020-"+str(seed_value)+".json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
79: 	FileAccess.open(Paths.file("state020-"+str(seed_value)+".json"),FileAccess.WRITE).store_string(Store.encode(g.s))
80: 	print("ROUTE ",seed_value," ",ok," ",failure," h=",g.s.local_hours," money=",g.s.currency," dt=",g.s.dt," pop=",g.s.metrics.population," happiness=",g.s.metrics.happiness)
```


## S15｜当前资源与人口参数


### `content/core020.json:3254–3290`

```text
3254:   "balance": {
3255:     "initial_money": 1100,
3256:     "initial_dt": 42,
3257:     "dt_cap": 96,
3258:     "offline_dt_per_hour": 0.5,
3259:     "tick_hours": 0.1,
3260:     "base_load": 0.65,
3261:     "building_load": 0.04,
3262:     "initial_people": 18,
3263:     "base_energy": 3,
3264:     "bonus_offer_price": 65,
3265:     "block_offer_price": 140,
3266:     "policy_switch_price": 20,
3267:     "birth_per_hour": 0.012,
3268:     "population_maintenance": 0.22,
3269:     "initial_stock": {
3270:       "food": 55,
3271:       "material": 85,
3272:       "goods": 0
3273:     },
3274:     "municipal_capacity": {
3275:       "food": 80,
3276:       "material": 100,
3277:       "goods": 45
3278:     },
3279:     "group_events_hours": [
3280:       3,
3281:       6,
3282:       9,
3283:       12
3284:     ],
3285:     "service_kinds": [
3286:       "medical",
3287:       "education",
3288:       "entertainment",
3289:       "commercial"
3290:     ]
```


## S16｜服务容量分配与人员效率


### `core/allocation.gd:6–39`

```text
6: static func labor(s:Dictionary,c:Dictionary,access:Dictionary) -> Dictionary:
7: 	var people_left={};var jobs_left={};var jobs={};var effective={};var personnel={};var links=[];var offers=[]
8: 	for gid in R.ids(s.groups):
9: 		var group=s.groups[gid];people_left[gid]=float(group.count);jobs[gid]={}
10: 		var host=group.host;var cf=c.config.get(host,{});var pe=float(group.efficiency)
11: 		if cf.has("volunteer_education") and float(group.education)>=float(cf.volunteer_education.threshold):pe*=float(cf.volunteer_education.labor_mult)
12: 		personnel[gid]=pe
13: 	for id in R.ids(s.projects):
14: 		var p=s.projects[id];var st=c.stats[id];jobs_left[id]=maxf(0,float(st.jobs));effective[id]=0.
15: 		if p.status!="running" or p.paused or p.get("special",false):jobs_left[id]=0;continue
16: 		for gid in R.ids(s.groups):
17: 			var group=s.groups[gid];var home=s.projects[group.host]
18: 			var reachable=.05 if home.get("special",false) else F.source_value(access.tiles,home.tile,"employment",id)
19: 			var hc=c.config[group.host]
20: 			if hc.get("local_employment",false) and home.id==id:reachable+=2
21: 			if hc.get("neighbor_employment",false) and R.near(s,home.id,id)==1:reachable+=.25
22: 			if hc.get("family_employment",false) and home.operator_org==p.operator_org and "building.industrial" in p.traits and reachable>0:reachable+=.4
23: 			if reachable<=0:continue
24: 			var sort=personnel[gid] if c.options.get("labor_mode","access")=="merit" else reachable
25: 			offers.append({"group":gid,"building":id,"score":sort,"access":reachable,"existing":float(group.employment.get(id,0))>0})
26: 	offers.sort_custom(func(a,b):
27: 		if a.score!=b.score:return a.score>b.score
28: 		if a.access!=b.access:return a.access>b.access
29: 		if a.existing!=b.existing:return a.existing
30: 		if a.group!=b.group:return a.group<b.group
31: 		return a.building<b.building)
32: 	for offer in offers:
33: 		var gid=offer.group;var id=offer.building
34: 		var n=minf(people_left[gid],jobs_left[id])
35: 		if n<=.000001:continue
36: 		people_left[gid]-=n;jobs_left[id]-=n;jobs[gid][id]=n
37: 		effective[id]+=n*personnel[gid]*float(c.stats[id].personnel_mult)
38: 		links.append({"group":gid,"source":gid,"target":id,"people":n,"access":offer.access,"effective":n*personnel[gid]*float(c.stats[id].personnel_mult)})
39: 	return {"employment":jobs,"effective":effective,"links":links,"unemployed":people_left}
```


### `core/allocation.gd:123–173`

```text
123: static func services(g,compiled:Dictionary,access:Dictionary,usage:Dictionary) -> Dictionary:
124: 	var s=g.s;var pools={};var source_pool={};var capacities={};var covered={};var allocations={};var pairs=[]
125: 	for gid in R.ids(s.groups):covered[gid]={};allocations[gid]={}
126: 	for kind in g.c.balance.service_kinds:
127: 		var todo=[]
128: 		for id in R.ids(s.projects):
129: 			var p=s.projects[id];var capacity=float(compiled.stats[id]["service."+kind])*float(usage.get(id,{}).get("utilization",0))*float(compiled.stats[id].efficiency)
130: 			var cfg=compiled.config[id]
131: 			if kind=="education" and cfg.has("volunteer_education"):
132: 				for group in s.groups.values():
133: 					if group.host==id and group.education>=cfg.volunteer_education.threshold:capacity+=float(group.count)*float(cfg.volunteer_education.capacity_per_pop)
134: 			if p.status=="running" and not p.paused and capacity>0:capacities[id+"|"+kind]=capacity;todo.append(id)
135: 		while not todo.is_empty():
136: 			var root=todo.pop_front();var members=[root];var frontier=[root]
137: 			if compiled.options.get("service_pool",false):
138: 				while not frontier.is_empty():
139: 					var x=frontier.pop_front()
140: 					for y in todo.duplicate():
141: 						if R.near(s,x,y)==1:members.append(y);frontier.append(y);todo.erase(y)
142: 			var pid=root+"|"+kind;var total=0.
143: 			for source in members:source_pool[source+"|"+kind]=pid;total+=capacities[source+"|"+kind]
144: 			pools[pid]={"kind":kind,"members":members,"capacity":total,"remaining":total}
145: 			for gid in R.ids(s.groups):
146: 				var group=s.groups[gid];var home=s.projects[group.host];var best=0.;var source=root
147: 				for member in members:
148: 					var value=F.source_value(access.tiles,home.tile,kind,member)
149: 					if value>0 and compiled.config[member].get("adjacent_service_priority",false) and R.near(s,member,home.id)<=1:value+=.1
150: 					if compiled.options.get("service_pool",false) and members.size()>1:
151: 						if R.near(s,member,home.id)<=1:value*=1.25
152: 						else:value=maxf(0,value-.15)
153: 					if value>best:best=value;source=member
154: 				if best>0:pairs.append({"group":gid,"pool":pid,"kind":kind,"source":source,"access":best})
155: 	pairs.sort_custom(func(a,b):
156: 		if a.access!=b.access:return a.access>b.access
157: 		if a.pool!=b.pool:return a.pool<b.pool
158: 		return a.group<b.group)
159: 	var links=[];var delivered={}
160: 	for pair in pairs:
161: 		var gid=pair.group;var kind=pair.kind;var pool=pools[pair.pool]
162: 		var remaining=maxf(0,float(s.groups[gid].count)-float(covered[gid].get(kind,0)))
163: 		var n=minf(remaining,float(pool.remaining))
164: 		if n<=0:continue
165: 		pool.remaining-=n;covered[gid][kind]=float(covered[gid].get(kind,0))+n
166: 		if not allocations[gid].has(kind):allocations[gid][kind]={}
167: 		allocations[gid][kind][pair.pool]=n
168: 		# Charge delivered activity proportionally to finite actual provider capacities.
169: 		for member in pool.members:
170: 			var fraction=float(capacities[member+"|"+kind])/float(pool.capacity)
171: 			delivered[member+"|"+kind]=float(delivered.get(member+"|"+kind,0))+n*fraction
172: 		links.append({"source":pair.source,"home":s.groups[gid].host,"group":gid,"kind":kind,"people":n,"access":pair.access,"pool":pair.pool})
173: 	return {"covered":covered,"allocations":allocations,"links":links,"pools":pools,"delivered":delivered}
```


### `core/population.gd:31–61`

```text
31: static func rehouse(g) -> void:
32: 	var stats=g.compiled.stats
33: 	for bid in R.ids(g.s.projects):
34: 		var p=g.s.projects[bid]
35: 		if p.special:continue
36: 		var capacity=maxf(0,float(stats[bid].housing)*float(stats[bid].efficiency)) if p.status=="running" else 0.
37: 		var gid=home_group(g.s,bid)
38: 		if gid!="" and g.s.groups[gid].count>capacity:transfer(g,gid,"homeless",float(g.s.groups[gid].count)-capacity)
39: 	for gid in R.ids(g.s.groups):
40: 		if not g.s.groups.has(gid) or g.s.groups[gid].host!="homeless":continue
41: 		for bid in R.ids(g.s.projects):
42: 			var p=g.s.projects[bid]
43: 			if p.special or p.status!="running":continue
44: 			var resident=home_group(g.s,bid)
45: 			var used=0. if resident=="" else float(g.s.groups[resident].count)
46: 			var room=maxf(0,float(stats[bid].housing)*float(stats[bid].efficiency)-used)
47: 			if room<=0:continue
48: 			transfer(g,gid,bid,minf(room,float(g.s.groups[gid].count)))
49: 			if not g.s.groups.has(gid) or g.s.groups[gid].host!="homeless":break
50: static func summary(g) -> Dictionary:
51: 	var people=0.;var housed=0.;var health=0.;var happiness=0.;var education=0.;var employed=0.;var housing=0.;var jobs=0.
52: 	for id in g.playable_ids():
53: 		var p=g.s.projects[id];var st=g.compiled.stats[id]
54: 		if p.status=="running":housing+=maxf(0,float(st.housing)*float(st.efficiency))
55: 		if p.status=="running" and not p.paused:jobs+=maxf(0,float(st.jobs))
56: 	for gid in R.ids(g.s.groups):
57: 		var group=g.s.groups[gid];var n=float(group.count);people+=n
58: 		if group.host!="homeless":housed+=n
59: 		health+=n*float(group.health);happiness+=n*float(group.happiness);education+=n*float(group.education)
60: 		for j in group.employment.values():employed+=j
61: 	return {"population":people,"housed":housed,"homeless":people-housed,"housing":housing,"vacancies":maxf(0,housing-housed),"workforce":people,"jobs":jobs,"employed":employed,"employment":minf(1,employed/maxf(1,people)),"health":health/maxf(1,people),"happiness":happiness/maxf(1,people),"education":education/maxf(1,people)}
```


## S17｜Rule 与 Field：已有可保留的内核


### `core/rules.gd:53–75`

```text
53: static func reduce_number(base:float,modifiers:Array) -> Dictionary:
54: 	var add=0.0;var mult=1.0;var lo=-INF;var hi=INF;var override={};var trail=[]
55: 	var ordered=modifiers.duplicate();ordered.sort_custom(func(a,b):return str(a.uid)<str(b.uid))
56: 	for m in ordered:
57: 		var v=float(m.value)
58: 		match str(m.mode):
59: 			"add":add+=v
60: 			"multiply":mult*=v
61: 			"floor":lo=maxf(lo,v)
62: 			"cap":hi=minf(hi,v)
63: 			"override":
64: 				if override.is_empty() or wins(m,override):override=m
65: 		trail.append({"source":m.source,"rule":m.uid,"mode":m.mode,"value":v})
66: 	var result=(base+add)*mult
67: 	# Content constraints combine deterministically; invalid inverted bounds are rejected at authoring.
68: 	if lo>hi:lo=hi
69: 	result=clampf(result,lo,hi)
70: 	if not override.is_empty():result=float(override.value)
71: 	return {"value":result,"base":base,"add":add,"multiply":mult,"cap":hi if is_finite(hi) else null,"floor":lo if is_finite(lo) else null,"override":override.get("source",""),"rules":trail}
72: static func wins(a:Dictionary,b:Dictionary) -> bool:
73: 	if int(a.specificity)!=int(b.specificity):return int(a.specificity)>int(b.specificity)
74: 	if int(a.priority)!=int(b.priority):return int(a.priority)>int(b.priority)
75: 	return str(a.uid)<str(b.uid)
```


### `core/fields.gd:9–17`

```text
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


### `core/fields.gd:89–124`

```text
89: static func environment(g,compiled:Dictionary,activity:Dictionary,h:float) -> Dictionary:
90: 	var tiles=empty_tiles(g.s);var fields={};var budgets={};var pollution_produced=0.;var heat_produced=0.
91: 	for id in R.ids(g.s.projects):
92: 		var p=g.s.projects[id]
93: 		if p.get("special",false) or p.status!="running" or p.paused:continue
94: 		var a=activity.get(id,{});var st=compiled.stats[id];var cfg=compiled.config[id]
95: 		var u=float(a.get("utilization",0))
96: 		var pollution=float(st.pollution)*u
97: 		var redirected=pollution*float(cfg.get("redirect_pollution_heat",0));pollution-=redirected
98: 		var heat=float(a.get("product_heat",0))+redirected
99: 		pollution_produced+=maxf(0,pollution)*h;heat_produced+=maxf(0,heat)*h
100: 		for kind in ["pollution","heat"]:
101: 			var strength=pollution if kind=="pollution" else heat
102: 			if absf(strength)<.000001:continue
103: 			var radius=int(st.pollution_range) if kind=="pollution" else 2
104: 			var decay=float(st.pollution_decay) if kind=="pollution" else .5
105: 			var f=make_field(id,kind,strength,radius,decay)
106: 			fields[f.id]=f;var weights={};var sum=0.
107: 			var component=road_component(g.s,id) if kind=="heat" and compiled.options.get("heat_network",false) else []
108: 			for tile in R.ids(g.s.cells):
109: 				var dist=Hex.distance(p.tile,tile);var w=0.
110: 				if kind=="pollution" and dist<=radius:
111: 					var d=decay*float(cfg.get("water_pollution_decay",1)) if "tile.waterfront" in g.s.cells[tile].traits else decay
112: 					w=pow(maxf(.001,1-d),dist)
113: 				elif kind=="heat":
114: 					w=access_strength(dist,radius,decay)
115: 					for other in component:
116: 						w=maxf(w,.8*access_strength(Hex.distance(g.s.projects[other].tile,tile),2,.5))
117: 				if w>0:weights[tile]=w;sum+=w
118: 			var local={}
119: 			for tile in weights:
120: 				# Pollution mass is partitioned; heat values express reach into ONE finite source pool.
121: 				var value=strength*float(weights[tile])/maxf(.001,sum) if kind=="pollution" else strength*float(weights[tile])
122: 				sample(tiles,tile,kind,f.id,id,value)
123: 				local[tile]=value*h
124: 			budgets[f.id]={"type":kind,"source_id":id,"total":maxf(0,strength*h),"remaining":maxf(0,strength*h),"local":local}
```


## 数据摘录：基础成本与主要能力

```json
{
  "hamlet": {
    "name": "河谷住宅",
    "cost": 80,
    "material": 10,
    "hours": 0.6,
    "jobs": 0,
    "housing": 24,
    "food_output": 0,
    "goods_output": 0,
    "energy_demand": 0.25,
    "energy_supply": 0,
    "population_food": 0.13,
    "population_goods": 0.025
  },
  "farm": {
    "name": "基础农场",
    "cost": 90,
    "material": 12,
    "hours": 0.7,
    "jobs": 6,
    "housing": 0,
    "food_output": 9,
    "goods_output": 0,
    "energy_demand": 0.4,
    "energy_supply": 0,
    "population_food": 0.13,
    "population_goods": 0.025
  },
  "timber": {
    "name": "基础料场",
    "cost": 90,
    "material": 12,
    "hours": 0.7,
    "jobs": 5,
    "housing": 0,
    "food_output": 0,
    "goods_output": 0,
    "energy_demand": 0.5,
    "energy_supply": 0,
    "population_food": 0.13,
    "population_goods": 0.025
  },
  "power": {
    "name": "基础动力站",
    "cost": 110,
    "material": 15,
    "hours": 0.8,
    "jobs": 3,
    "housing": 0,
    "food_output": 0,
    "goods_output": 0,
    "energy_demand": 0,
    "energy_supply": 12,
    "population_food": 0.13,
    "population_goods": 0.025
  },
  "works": {
    "name": "基础工业厂",
    "cost": 150,
    "material": 22,
    "hours": 1.2,
    "jobs": 8,
    "housing": 0,
    "food_output": 0,
    "goods_output": 6,
    "energy_demand": 3,
    "energy_supply": 0,
    "population_food": 0.13,
    "population_goods": 0.025
  },
  "market": {
    "name": "基础市场",
    "cost": 100,
    "material": 12,
    "hours": 0.8,
    "jobs": 3,
    "housing": 0,
    "food_output": 0,
    "goods_output": 0,
    "energy_demand": 0.8,
    "energy_supply": 0,
    "population_food": 0.13,
    "population_goods": 0.025
  },
  "school": {
    "name": "基础学校",
    "cost": 110,
    "material": 14,
    "hours": 0.8,
    "jobs": 4,
    "housing": 0,
    "food_output": 0,
    "goods_output": 0,
    "energy_demand": 1,
    "energy_supply": 0,
    "population_food": 0.13,
    "population_goods": 0.025
  },
  "clinic": {
    "name": "基础诊所",
    "cost": 115,
    "material": 14,
    "hours": 0.8,
    "jobs": 4,
    "housing": 0,
    "food_output": 0,
    "goods_output": 0,
    "energy_demand": 1,
    "energy_supply": 0,
    "population_food": 0.13,
    "population_goods": 0.025
  },
  "park": {
    "name": "街坊公园",
    "cost": 80,
    "material": 10,
    "hours": 0.6,
    "jobs": 2,
    "housing": 0,
    "food_output": 0,
    "goods_output": 0,
    "energy_demand": 0.3,
    "energy_supply": 0,
    "population_food": 0.13,
    "population_goods": 0.025
  },
  "warehouse": {
    "name": "公共仓储站",
    "cost": 90,
    "material": 12,
    "hours": 0.7,
    "jobs": 2,
    "housing": 0,
    "food_output": 0,
    "goods_output": 0,
    "energy_demand": 0.3,
    "energy_supply": 0,
    "population_food": 0.13,
    "population_goods": 0.025
  }
}
```
