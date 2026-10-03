# 0.3.0开发工作单

工作只在B0.2.1现有工程上推进。下列路径中标“新增”的是本方案建议，不是当前已有实现。每个提交保留一个能启动的完整游戏；不把内部工序发布成多个产品。

## 可直接分派的任务

| ID | 任务 | 文件落点 | 依赖 | 完成要求 |
|---|---|---|---|---|
| UX001 | 固定基线、导入旧存档及视觉设置分离 | project.godot、core/storage.gd | 无 | 哈希和旧档不变；显示设置不消耗玩法RNG |
| UX002 | 统一Theme及响应式尺寸适配 | ui/widgets.gd；新增ui/shell/layout_profile.gd | UX001 | 字号、间距、颜色引用token；禁用1050宽固定窗口 |
| UX003 | UI状态机与本地决策暂停解耦 | ui/app.gd；新增ui/input/interaction_state.gd | UX002 | 投标暂停不依赖overlay.visible；返回不丢报价 |
| UX004 | 稳定世界坐标与独立镜头 | ui/map_view.gd；新增ui/map/camera_controller.gd | UX002 | 侧栏不改变格子世界尺寸；两个归位目标 |
| ART001 | 建立三母版并合成到现有地图 | assets/world030；asset_manifest | UX001 | 住宅、厂、诊所同视角比例，通过真实尺寸核查 |
| ART002 | 补齐十主体、共用配件、主题图标 | assets/world030、assets/ui030 | ART001 | 十主体完整替换；导入尺寸/alpha/挂点有记录 |
| MAP001 | 地表、岸线、道路与边界重画 | 新增ui/map/terrain_renderer.gd | UX004 | 共边连续；不改变地形Trait与可建性 |
| MAP002 | 精灵主体、配件、状态表现 | 新增ui/map/building_renderer.gd | ART002、MAP001 | 稳定y排序；影子、烟、停机状态准确 |
| MAP003 | 只读地图快照适配 | 新增ui/view_models/map_snapshot.gd | UX001 | 一帧来自一个周期/实际或预测模式；来源完整 |
| MAP004 | 四种服务图层、容量短缺原因 | 新增ui/map/service_overlay.gd | MAP003 | 医疗等分别与真实分配一致，去重转播路径 |
| MAP005 | 环境、热、就业、资源、电力、组织 | 新增ui/map/layer_renderer.gd、label_layout.gd | MAP003 | 不发明量纲/运输；固定图例、可查来源 |
| PLAN001 | 扩展逐人群和逐建筑预测差分 | core/planning.gd | MAP003 | 复用clone，不改经济；LRU上限、过期丢弃 |
| PLAN002 | 规划工作台与候选地图联动 | ui/app.gd、ui/dialogs.gd；新增ui/shell/tender_workspace.gd | UX003、MAP004、PLAN001 | 三份同基线比较，关键成本一致，免费取消 |
| PLAN003 | 方案槽、政策沿用工作台 | ui/panels.gd、ui/dialogs.gd | PLAN002 | 原有40Bonus/12政策可达，损失与代价明确 |
| UX005 | 目标、消息、账本新信息架构 | ui/presentation.gd、ui/app.gd、ui/dialogs.gd | UX002、MAP005 | 当前矛盾可定位，主要结果先于规则细节 |
| INPUT001 | 单指、双指、鼠标统一动作 | 新增ui/input/input_router.gd | UX003、UX004 | 拖图/缩放不会误落楼；hover非唯一入口 |
| FX001 | 完工/首次运营/成果反馈 | 新增ui/map/feedback_layer.gd | MAP002、UX005 | 用真活动结果；完工预测与实际分开 |
| RELEASE001 | 整合、性能与平台记录 | tests/、tools/、docs/、mac打包 | 全部核心项 | 单一发布包；实际设备结果与未测项分开 |

## 只读数据合同（建议新增）

```text
MapSnapshot
  id = revision + tick + actual/forecast + source_phase
  moment_label                # 如“本地8.0h · 上一周期实际”
  buildings[]                 # ID、位置、视觉用途、状态、容量、实际活动
  groups[]                    # ID、住宅、人数、按服务种类的需求和分配
  tile_contributions[]        # field_id、source_id、kind、value、via
  links[]                     # type、origin、target、amount、source_record
  missing_data[]              # 不存在的数据不假装成零

PlanDiff
  baseline_snapshot_id
  target_tile / existing_host
  candidate_offer_id
  forecast_horizon            # 现有一周期，而非无限长规划
  no_action_next_tick / candidate_next_tick resource activity
  per_group_service_delta[]
  per_building_utilization_delta[]
  per_tile_field_delta[]
  budget_and_costs
  causes[]
  status = pending / ready / stale / error
```

这些是展示数据契约，不是要求把它们写成新的模拟Entity或永久存档对象。差分由同一初始状态的两份隔离预测对照生成：无行动的下一周期 vs 方案完成的下一周期。无行动基线可供三份方案共用；不能拿现状与未来相减，将自然人口或供给变化归功于新建筑。UI不自己推测“邻近就一定服务”。

## UI动作合同（示例名，不是现有API）

- `SelectBuildType(type_id)`：只进入规划、选择相关图层。
- `PreviewTile(tile_id)`：不生成新报价，不消耗资源。
- `OpenOffers(tile_id,type_id)`：使用已有招标服务，记录／恢复报价。
- `SelectOffer(offer_id)`：只更换预览，不commit。
- `ConfirmConstruction(offer_id,baseline_revision)`：重新验证成本与资格后调用现有建造逻辑。
- `CancelPreview()`：恢复图层、镜头选择和暂停状态，保留报价记忆。
- `InspectCause(entity_id,metric_id)`：只读来源，点来源定位。

不得让按钮直接修改库存、场、槽或人口数组。不要把响应式面板“弹出/隐藏”作为经济规则的一部分。

## 典型画面合同

### 主界面

第一注意力是城市；第二是当前目标/问题；第三是当前工具。不要在顶部永久占一大片应用名称与版本口号。版本进菜单，正式游戏名保留为小标识。

### 投标

共用成本一处，候选一行/一张摘要。示例数字仅用于排版：

```text
基础诊所 · 当前位置 (2,1)
一次投入 X通货 / Y材料 · 工期 Z本地小时

研究型医院｜白霭学社｜生产/生产/运营
医疗容量转教育；邻接学校受益
预计：医疗新增8人；教育新增6人
代价：医疗供给比普通方案减少；需3名员工

[看其他方案]       [采用并建设 · X通货]
```

正式数字全部由同内核预测填入，不能把示例8人/6人/3员工抄成规则。

### 医疗图层

外圈回答够不够得到；住宅填色回答实际获得多少；斜纹回答覆盖内却抢不到。选择诊所后仅看该来源，选择住宅后看它的候选和实际获得。不能从一个绿色光圈推断满容量。

### 资源详情

先看当前可用库存/本周期产用弃，再按对象来源展开。库存净变0且有丢弃，必须写“浪费”，不能写“平衡”。电力只显示供需和有限储能能力存在时的实际储量。

## 不允许为了赶工偷换的地方

- 不能把未获服务换成低透明绿色，假装覆盖已经修好。
- 不能用AI生成的完整界面截图做可点击底图。
- 不能只调主题色，却保留覆盖全图的投标大弹窗。
- 不能把手机支持写成“窗口能缩小”，而不处理ScreenTouch/Drag。
- 不能拿素材命名中的未来服务功能修改现有Trait。
- 不能为视觉遮挡移动真实建筑或扩大真实范围。
- 不能假装本次新加了垃圾链、物价、真实交通或自由3D。

## 低高度手机投标页面

320高安全区内，右侧投标面板占完整高度，左侧仍可看地图与切预览。普通字号下预算为304逻辑高：标题32、成本32、三条56高摘要、行间距8、确认栏48、上下内边距16。该档详情使用面板内的第二页，不和三行列表同时硬塞。字号放大后滚动摘要，确认固定；禁止降低字号来保“三卡同时可见”的表面指标。
