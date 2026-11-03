# MET Wild v0.1 · G0 引擎审计

日期：2026-10-26。范围：当前 checkout 的 UI15 工作区及 G0 Toolkit 试用源码（含未提交修改）。本文的引擎结论来自只读静态审计；审计本身未运行 Unity、未读取玩家存档、未修改玩法。G0 另含隔离工程回放和可选界面试用，证据分列于下文。本文描述**现行 Beta 实现，不是 Wild 目标语义或 G1 决策**。后续实现以用户批准的阶段范围为准。

## 1. 结论与复用边界

已有 Unity 引擎具备真实建造/升级命令、施工结算、Rule 编译、深复制快照、同一步预测及整城保存通路。Wild 建筑构筑不能只换 UI 名字：当前建筑能力来自全局建筑定义，槽轨道来自运营者，没有逐建筑设计容量、自由设计节点或保存设计模板的权威状态。

下文路径均相对仓库根目录。`Core/` 表示 `unity/METLivingWorld/Assets/MetCore/`；`City/` 表示 `unity/METLivingWorld/Assets/City/`。方法名是当前源码定位锚点。

| 主题 | 当前来源与行为 | G0 / 后续边界 |
| --- | --- | --- |
| 内容定义 | `Core/Foundation/Catalog.cs:Catalog` 读取 StreamingAssets/MetContent 的 core020、040/buildings/progression/operators 及 beta 内容；`projects` 最终取 040/buildings | 可复用 Catalog 入口。`content/040/buildings.json` 是仓库内容源，`tools/sync_unity_content.py` 同步 Unity 副本；不要分别修改两份产生两个真相。Wild 参数以后明确归属。 |
| 建筑实例 | `Core/Engine/Game.cs:create_building` 创建 projects/sites/cell 引用、inventory/job/slots/history；`Core/Economy/Development.cs:ensure_project` 添加 family_id/node_id/tier/slot_track | 保留现有实例 ID、地块、库存、运行状态。Wild 构筑字段尚无；不能把展示草稿当实际建筑。 |
| 实际新建 | `Core/Economy/Draft.cs:open_build/draw/accept`；Game.create_tender/accept_offer 为入口 | 当前仅三种 tier1 起点，抽三位运营者报价，签约扣通货再施工。可以用于明确标注的 G0 旧命令演示；不能称为 Wild 无运营者构筑流程。 |
| 实际升级 | `Core/Economy/Development.cs:choices/preview/start_upgrade/complete` | 使用定义的 upgrades 边；开工扣通货并写 upgrade_job，完成保留身份/运营者/槽/库存/历史，更新 def/node_id/tier 并解锁槽。可复用命令所有权和施工框架，不能直接承诺新设计节点语义。 |
| 施工 | `Core/Economy/Construction.cs:capacity_breakdown/queue/tick` | 实际 CP/月、能源、材料和优先队列可复用。**施工能力 CP ≠ Wild 设计容量 DP**。不为 G0 改施工结算。 |
| 有效能力 | `Core/Rules/Rules.cs:compile` | 普通建筑读取 `Catalog.projects[p.def].stats`；仅非目录对象回退 base_stats。已有 size_scale 字段不等于设计容量计算。以后需把构筑接入唯一编译链，不能另算一套 UI 产能。 |
| Bonus | `Core/Progression/Bonus.cs:grant/install/choose/blueprint_level`；Game.bonus_eligible/_install_bonus/_unmount | 可复用资格/Rule trace/实例引用调查；现行副本消费、重复强度及组织尺度不能自动继承为 Wild。 |
| UI15 | `City/UI/CityConstructionWindow.cs:OpenConstructionWindow/OpenUpgradeWindow`、`CityConstructionDecision.cs:RenderConstructionUpgrades`、`CityBlueprintDesk.cs` | 保留现有工作区/建造窗口、可折叠影响比较、2560×1440 全屏基线；G0 新增独立 UI Toolkit 升级窗口试用，默认关闭、设置页主动开启；仍提交旧升级命令。当前 BlueprintDesk 是 Bonus 库存/研究桌，不是建筑设计模板库。 |

## 2. 三组必须澄清的现状

### 升级与许可

`Development.start_upgrade` 检查现有升级边、running 状态、无并行升级、通货；随后写施工 job。**这里没有消耗型建筑升级许可，也没有按高级形态“首次取得”解锁直建的状态。** `Draft.open_build` 明确限制 progression.tier1；低层 create_building 能接受定义 ID，不代表玩家可合法直建高级建筑。

`Bonus.install` 则明确检查 `block.bonus_wallet[bonus_id].charges` 并在成功部署后减一；研究奖励 `Bonus.choose(kind=research)` 改 `block.blueprint_levels`，不消费库存副本。两者都出现“蓝图/许可”字样，不能混称建筑升级许可证。Wild “不采用消耗型升级许可”是未来方向；G0 不新增许可，也不顺手修改现有 Bonus 钱包。

### Tier 谱系与逐建筑设计容量

当前定义有 family_id/tier/upgrades；实例有 def/node_id/tier/history。`Development.complete` 用新定义替换建筑节点并留一条升级历史，**没有保存 T1/T2/T3 可逐级撤销的构筑节点树**。tier 还控制 unlocked_slots，槽类型取自运营者 slot_track。Wild 所需“逐建筑容量受全局最大值约束”和“保留 Tier 谱系、取得高级形态后直建”尚未实现，不由当前 T 数值自动证明。

当前 stats 中住房、就业、仓储、服务、供电是模拟量；construction_capacity 是施工队吞吐。以上均不可以直接重命名成设计容量。实例 `size_scale=1.0` 当前也不是 Wild DP 权威。

### 运营者与 Bonus

当前 `Game.new_game` 建 organizations；create_building 写 operator_org；Development 初始化运营者槽轨道；Rules.compile 编译组织 rules；Bonus 实例保留 provider_org，可有 contract_org；`Game.cohesion/recontract` 使用运营关系。`change_operator` 常规入口虽禁用，**运营者仍参与真实玩法**。

Wild 已指明未来不设运营者。G0 不把 commons/civic 偷换成新设计默认运营者，也不删除现有字段或组织规则。正式移除须覆盖报价、槽轨道、资格、组织 Bonus、Rule 来源、历史及校验，保持既有记录可解释。

当前 Bonus 槽类型 Production/Logistics/Operations/Flex 与 host 尺度 building/organization/block 是两组轴；重复同名副本按规则数值追加，结构效果不重复；blueprint_level 为 0–2。Wild 的组织/联系/融合类别和槽型映射、融合语法仍待 Q4；不能将这些旧轴直接当新分类。

## 3. 命令、快照、预测和保存

1. `City/Runtime/CityRuntime.cs:Execute` 先 Flush，等待 worker advance 完成，验证 Session.CanEditFocused，执行真实 Game 命令；成功走 `CitySession.AfterLocalCommand`，再 Publish、保存。`ExecuteWorld` 管整城命令。G0 演示旧动作继续走这里，不让 UI 直接修改 G.s。
2. `Game.snapshot` 深复制；`CitySession.snapshot/MetadataSnapshot` 从 Games 的唯一 live owner 装配局部状态。`CityRuntime.Snapshot/SessionSnapshot/Committed` 给 UI 分离副本。当前 Game 本身仍是可变对象，不应把“有 Snapshot”误写成引擎强制不可变。
3. `Core/Presentation/Planning.cs:copy_game` 深复制 s/world_effects，标 preview_mode，复用只读 Catalog。`snapshot/compare` 在 clone 跑相同步长 Sim.tick。`CityConstructionDecision.cs:ConstructionUpgradeForecast` 仅在 clone 假定升级完成，再跑下一步。它表示“假定完工后的下一步”，不是实付开工成本后的全过程收益承诺。
4. `Core/Presentation/BlueprintPreview.cs:For/Inspect/Evaluate` 分开真实库存感知安装预测与无库存目录检查；均只改 clone。Inspect 不伪造 live 钱包。共享 Catalog 不应在预测时被改写；copy_game 保留 storageRoot，不是任意 I/O 的沙箱，因此预览仍只允许不保存的模拟调用。
5. `Planning.version` 与当前 UI 的 expectedVersion 检查防止过期预测直接提交。G0 不引入新指纹体系；后续有真实构筑状态后应纳入现有 revision/版本失效通路。
6. 当前 Runtime 使用 `Core/City/CityStore.cs` 的 **met_city_v1.json** 整城 envelope（schema 1、build 约束），其中 blocks 含局部 Game schema 6。旧 `Core/Foundation/Store.cs` 的 met_unity_v1.json 不是当前 Runtime 主档。根目录来自 MET_UNITY_SAVE_ROOT 或 Application.persistentDataPath；display_settings.json 另存显示设置。
7. CityStore 在写临时档、恢复及备份候选时校验整城，Game.Validation 校验局部引用、槽/Bonus、运营者等。Wild 保存变更以后同时覆盖权威字段与这些校验；G0 不加 schema、不迁移、不重写玩家数据。Unity/Godot 存档兼容不在范围。

## 4. 后续“可复用 / 需新增 / 不触碰”清单

- 可复用：Runtime 命令串行入口，Game/Session 快照，Planning 的分离 clone 与同一步比较，Construction 队列，Rule 编译/来源 trace，现有建造升级窗口结构和地图影响比较。
- 需新增或改造（以后授权）：逐建筑构筑权威状态与单一能力编译；全局 DP 上限来源；保留 Tier 谱系与逐级撤销命令；高级形态取得/直建资格；槽构筑与成本；无运营者规则来源；保存设计模板与配额。不要在 UI DTO 内偷偷实现。
- G0 不触碰：RNG/钱包/实体/施工/存档语义，现有 UI15 的玩法状态和工作成果，时钟、离线、模拟结算，既有地形/资源同步结果。G0 仅在升级入口及模态输入所有权处接入可选 Toolkit，不能把“未改模拟”写成“未改任何 UI”。审计也不等于确认所有当前工作区功能已验收。

## 5. G0 界面试用与验证边界

`City/UI/CityToolkitUpgrade.cs:ToolkitUpgradeEnabled` 从 display_settings 的 `toolkit_upgrade` 读取开关，缺省 false；`CityRecords.cs` 设置页显示“UI Toolkit · G0 试用”。`CityConstructionWindow.cs:OpenUpgradeWindow` 只在开启时分流。`Toolkit/UpgradeToolkitView.cs` 使用独立 UIDocument/UXML/USS；`Toolkit/UpgradeWindowModel.cs` 是展示模型，没有新增模拟状态。

`ToolkitUpgradeModel` 读取 Development.choices/preview、现有 ConstructionUpgradeForecast 和同一步基线；标题旁明确“现有建筑升级规则”。`SubmitToolkitUpgrade` 经 Runtime.Execute 检查 Planning 版本，再调用 Development.start_upgrade。开启/关闭窗口管理 Runtime.DecisionOpen、焦点和既有输入屏蔽，不引入 Wild DP/节点/槽成本/运营者移除。

## 6. 待决定项及证据状态

Q1–Q5 均保持开放，定义见 [数据合同](G0_DATA_CONTRACT.md)。现行数据只作为对照证据。

审计验证：逐文件读取上述 C# 与 JSON，确认接口和实际字段。另行执行的原生 Core 隔离回放已有 33 项聚焦检查通过，方法、结果和限制见 [G0 节奏基线](G0_PACING.md)；该工程结果不等于审计直接执行或 Unity/Player 验收。

待补证据：本页不声明 Unity 编译/Player 构建、Toolkit 截图、真实鼠标键盘、GPU 性能或 10–12 分钟真人游玩的通过结果。实际运行与交付证据由本轮单独交付记录补充；未附证据前保持待验证。用户的真实节奏和乐趣接受情况仍待取得。

本轮后续运行结果见 [G0 交付与运行记录](G0_DELIVERY.md)，其中区分实际 Player 截图、合成控件事件与待真人确认的体验。
