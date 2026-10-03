# MET 0.4.0 开发架构方案 v1

**基线：MET 0.3.2 CityDesk / schema4**  
**目标：把“已经好玩的单 Block 建城”扩展成有纵向成长、委托奖励、Bonus 许可经济、市场贸易和 Block 继承的完整发展弧。**

## 1. 总体判断

0.4.0 不应重写现有 Rule / Field / Population / Allocation 内核。现有内核已经能支持 Bonus、空间场、有限服务与资源分配。真正需要重构的是：

- 建筑内容模型：从 10 个直接可建基础建筑，改成 3 个 Tier1 起点 + 专业化升级树。
- Tender：从 `Operator + Bonus` 改成只选择 Operator；Operator 决定 Slot Track 与领域偏好。
- Bonus：从随时征集改成 Commission 奖励产生的“一次部署许可”。
- Progress/Civic：从硬编码成果奖励改成数据驱动 Commission + Block Goal。
- Construction：从“每栋建筑自己按小时倒计时”改成共享 Construction Capacity。
- 新增 Market：货运站把 Block 接入外部市场。
- 新增最小 District Shell：负责跨 Block Bonus 启动资产、District Bonus、市场状态与已完成 Block。

## 2. 0.4 核心循环

`建 Tier1 → 选 Operator → 城市运行 → 接委托 → 完成委托 → Bonus 三选一 → 获得一次部署许可 → 建筑升级 → 解锁新 Slot → 安装 Bonus → 专业化 / 贸易 → 完成 Block Goal → 可继续经营或移交 → 选择 2 个可携 Bonus 作为下一 Block 启动资产`

Block 结束不强制立即退出；达成目标只是开启移交资格。

## 3. 建筑成长

### 3.1 三级树

- 农居
  - Tier2：住宅 / 农业
  - Tier3：高级住宅 / 现代农业
- 邻里中心
  - Tier2：公共中心 / 诊所 / 初级学校
  - Tier3：公共中心→商圈或公园；诊所→医院；初级学校→中学校
- 作坊
  - Tier2：动力站 / 开采站 / 料场
  - Tier3：动力站→发电厂；开采站→材料工厂；料场→制造工厂或货运站

设计原则：Tier1 多用途、低效率；越升级越专业、吞吐更高、相互依赖更强。

### 3.2 Slot Track

Operator 定义三步 Slot Track，例如 `Production → Production → Operations`。

- Tier1 建成：解锁第 1 个 Slot。
- 升 Tier2：解锁第 2 个 Slot。
- 升 Tier3：解锁第 3 个 Slot。
- Slot 类型一旦生成不允许常规改型。
- `reforge_slot` 从普通 UI 移除；如未来保留，只作为罕见事件 / Bonus。
- Operator 也不再通过常规付费按钮随意更换；变更运营者属于罕见组织事件。

### 3.3 Upgrade Job

Building 保持同一 Project ID / Tile / Operator / Population Host / Inventory。升级改变 `node_id / def`，通过 Upgrade Job 完成。

建议字段：

```text
family_id
node_id
tier
upgrade_choice
slot_track
unlocked_slots
upgrade_job
legacy_mature
```

升级预览必须显示：新能力、失去的多元能力、资源成本、Construction Points、运营受影响程度、新解锁 Slot。

## 4. Construction Capacity

当前 `simulation.gd` 中施工直接按 `h` 减少 `job.work`。0.4 改为共享施工能力：

- Block 有 `construction_capacity`（CP/h）。
- 建筑和升级有 `construction_required`（CP）。
- 同时施工的项目争用 CP，按玩家优先级稳定分配。
- Material 仍按施工进度实际消耗；不能有材料时凭空完成。
- 豪华建筑 / Tier3 升级显著增加 Material、Currency、Construction Required。
- DT 仍然只是推进本地时间；它不会绕过施工能力。高 CP 城市用同样 DT 能完成更多建设，因此“投资施工能力”成为发展决策。

可让 Rule 修改 `construction_capacity` / `construction_efficiency`，但施工调度本身不强塞进 Rule 解释器。

## 5. Operator / Tender 重写

当前 `tenders.gd` 同时抽 Operator 和 Bonus；0.4 拆开。

### 新建 Tender

玩家选 Tier1 基础建筑与地块 → 抽 3 个 Operator → 选一家 → 建造。

Offer 只包含：

- Operator
- 领域身份 / Organization Rule
- Slot Track（未来三阶段会开什么槽）
- 必要时 Operator 的施工 / 运营差异

不再携带 Bonus。

### Operator 领域权重

每个 Operator 定义 `domain_weights`，例如工业公司：工业 100、基础设施 70、商业 15、公共 5。跨界不是禁止，而是低概率。

现有 8 家可以先重新分类，不急着增加新公司。

`preferred_bonuses` 从 Tender 决策中退役；Bonus 由 Commission 系统产生。

## 6. Bonus Economy

### 6.1 License 与 Instance 分离

现有 `bonus_instances` 继续表示“已经装上的效果实例”。新增：

```text
bonus_wallet:
  P06: {charges: 1, acquired_from: commission_id, portable: true, ...}
```

普通 Bonus：每获得一次奖励，只得到 1 次部署许可。

- 安装时消耗 1 charge。
- 同一个 Bonus 在同一 Block 默认最多部署一次。
- 卸下 / 替换不返还许可；这代表方案已经在这个项目上落地过。
- 多次部署必须来自罕见“标准化 / 复制 / 扩散”奖励，不是普通规则。

### 6.2 作用域与持久性分开

不要把“挂在哪里”和“能不能带走”混成一个字段。

- Deployment Scope：Building / Organization / Block / District。
- Persistence Class：Local / Portable / Institutional。

建议四类奖励体验：

1. Local License：普通建筑 / 组织 Bonus，本 Block 一次。
2. Block Doctrine：激活后作用整个 Block，移交后结束。
3. Portable Bonus：Block 结束时可被选为下一 Block 启动许可。
4. District Doctrine：极少数，制度化后跨 Block 永久作用于本 District。

### 6.3 安装资格

Bonus 的 Trait / Gate 用于“能否安装”。安装成功后不应因为建筑升级导致 Trait 改变而静默失效；如果某效果需要持续条件，条件应写进 Rule 自己。

因此建议在 0.4 把 `install_gate` 与 `rule selectors/when` 语义彻底分开。

## 7. Commission 系统

现有 `civic032.gd` 的 4 个硬编码成果与 3 条章节成果保留为迁移/教程来源，但正式 Commission 改为数据驱动。

建议新文件：`content/040/commissions.json`。

一个 Commission 至少包含：

```text
id / domain / trigger / prerequisites / goals[] / fail_conditions
reward_pool_tags / reward_scope / weight / repeatable
```

### 节奏

- 同时 1 个主要 Commission；可有少量城市事件，但不要任务墙。
- 城市状态触发 2~3 个可接 Commission，玩家选一个。
- 完成后进入 Bonus 三选一。
- Reward Pool 根据 Commission 领域、当前城市状态、当前 / 近未来可用 Slot 过滤与加权。
- 奖励建议：2 张现在可部署 + 1 张“近未来有意义”的前瞻牌，避免三张死牌。

Commission 例：

- 工业交付：限时完成制成品交付且不发生断电。
- 社区服务：把特定住宅群医疗覆盖提高到目标并保持一段时间。
- 组织发展：让同一 Operator 的若干项目形成真实关联。
- 市场合同：在价格不利时完成出口，换取稀有物流 / 贸易 Bonus。

任务奖励不再直接发 `block_opportunities`；`block_opportunities` 逐步退役。

## 8. Block 结束与最小 District Shell

0.4 如果要真正实现“把 Bonus 带到下一 Block”，必须有最小 District 外壳，不能只做一张结束页面。

新增：

```text
city: {id, name, ...}
district: {
  id, name,
  legacy_bonus_pool,
  district_bonus_instances,
  market_state,
  completed_blocks,
  active_block_id
}
```

Block Goal 完成后：

- 获得地区命名权（如果尚未命名）。
- 开启“移交本 Block”按钮，不强制结束。
- 玩家决定移交时，从本 Block 获得过的 Portable Bonus 中选 2 个。
- 下一 Block 开局获得这 2 个 Bonus 各 1 次 License。
- District Doctrine 自动继续生效，不占这 2 个名额。
- 旧 Block 保留为永久历史快照 / 自治区域；0.4 不要求做跨 Block 实时物流。

## 9. Market / Trade

### 9.1 接口

Tier3 `货运站` 提供 External Market Access 和 Trade Capacity。

0.4 首版只交易：Food / Material / Goods。Electricity 暂不跨区交易。

玩家不是点击后瞬移物资，而是创建 Trade Order；订单按货运站的 `trade_capacity` 在 Tick 中实际执行。

### 9.2 价格模型

每个资源在 District 级市场维护：

```text
base_price
pressure
market_depth
spread
fee
reversion_half_life
amplitude
```

每 Tick：

```text
pressure *= exp(-h / half_life)
mid = base_price + amplitude * tanh(pressure)
buy_price  = mid + spread/2 + fee
sell_price = mid - spread/2 - fee
```

- 从外部买入：pressure 上升。
- 向外卖出：pressure 下降。
- `amplitude > base_price` 时允许出现负价格。
- 始终保证 buy_price > sell_price，因此同一时刻买入再卖出无法套利。
- 负买价：外部补贴玩家接收过剩资源。
- 负卖价：玩家支付处置费才能把过剩资源送出去。

交易还必须受库存、Trade Capacity、通货约束。

### 9.3 税制命名

当前 `tax_trade` 实际针对居民消费服务 / 商业活动，不是外部贸易。0.4 UI 建议改称“消费税 / 商业税”，ID 可保留以兼容存档。外部关税以后单独做。

## 10. 城市视觉成长：Urban Fabric Layer

0.4 城市成长不能只在面板中发生。

新增纯视觉层，不给每栋小房子创建 Entity：

- Tile 的 `visual_development_level` 由 Building Tier、入住 / 利用率、周边关系决定。
- 相邻两个已开发 Tile 计算 `connection_strength`：邻接基础 + 同组织 + 就业 / 服务 / 资源真实链接。
- 达到阈值时，在两格之间确定性生成小屋、铺面、仓棚、院落、树、支路等 filler。
- Tier 提升后主体建筑更高、更现代，filler 数量 / 密度同步提升。
- 使用 world seed + stable IDs 选视觉变体，不消耗游戏 RNG。

0.4 中 filler 纯视觉；以后若需要再考虑把它们变成模拟对象。

## 11. 内容拆分

不要继续把 0.4 内容塞进 3660 行 `core020.json`。

建议：

```text
content/040/
  buildings.json
  progression.json
  operators.json
  bonus_economy.json
  commissions.json
  market.json
  policies.json
```

现有 `core020.json` 保留用于 legacy 定义和迁移。Catalog 同时加载旧定义和 040 新内容。

## 12. Core 模块落点

建议新增：

```text
core/development040.gd    # Tier树、升级、Stage、Slot unlock
core/construction040.gd   # Construction Capacity与施工队列
core/bonus_economy040.gd  # License、一次部署、reward draft
core/commissions040.gd    # 委托生成、进度、三选一
core/market040.gd         # 价格、订单、交易容量
core/district040.gd       # Block移交、Legacy、District Bonus
ui/map/fabric040.gd       # 视觉街区填充
```

尽量保留：`rules.gd / fields.gd / allocation.gd / population.gd / inventory.gd / planning.gd`。

重点重写 / 瘦身：

- `tenders.gd`：只负责 Operator Tender，不再抽 Bonus。
- `progress.gd`：只负责 Block Stage / Goal / Handover gate。
- `civic032.gd`：历史账本部分拆出；任务逻辑迁到 commissions040。
- `simulation.gd`：施工阶段接 Construction Allocator；生产内核不整体推翻。
- `game.gd`：做 orchestrator，不继续把所有 0.4 状态机塞进去。

Rule Engine 只需要增加 `district` host context；Market / Commission / Upgrade 不强行写成 Rule。

## 13. Save Schema 5

0.4 建议升级 schema5，不在 schema4 上继续塞字段。

新项目状态至少加入：

```text
family_id / node_id / tier / upgrade_job
slot_track / unlocked_slots
```

Block：

```text
stage / bonus_wallet / commissions / naming_right / handover_ready
```

District：

```text
legacy_bonus_pool / district_bonus_instances / market_state / completed_blocks
```

### 0.3.2 导入原则

旧 10 个建筑定义保留为隐藏 `legacy_*` 定义，不强行套用新 Tier3 数值；原输出、库存、槽位和 Bonus 效果保持。

- 已装 Bonus：视为 `legacy_deployed`，不额外发 License。
- pending development Tender：保留原三家 Operator 候选，移除附带 Bonus。
- pending refit / scope 三选一：转为一次 legacy Bonus Reward Choice，保留原三个 Bonus ID。
- 旧 `block_opportunities`：转换为等量或有上限的待选 Bonus Reward Token，不能无声丢失。

旧城市可以继续经营，但新建建筑走 0.4 三 Tier 系统。

## 14. 0.4 内部开发顺序

1. 合并 0.3.3 的信息收敛 / 地区命名设计到 0.4 UI 基线。
2. schema5 + content/040 + 旧档迁移骨架。
3. 三级建筑树 + City Stage + Operator-only Tender + Slot Track。
4. Construction Capacity + Upgrade Job + 预览。
5. Bonus License Economy；删除普通“花钱随机征集 Bonus”。
6. Commission Engine + Reward 三选一。
7. Market / Freight / 动态价格与 Trade Order。
8. District Shell + Block Handover + 2张 Legacy Carryover + District Bonus。
9. Urban Fabric / Tier视觉成长。
10. 统一 UI、数值平衡、完整路线、旧档与发布验收。

这些是同一个 0.4.0 的内部工序，不向玩家发布 10 个碎片版本。

## 15. 关键验收

- 新游戏开局只需要理解三个 Tier1 建筑。
- Tender 只决定 Operator，不再白送 Bonus。
- T1/T2/T3 按升级逐个解锁 Slot，Slot 类型稳定不自动洗牌。
- 没有 Commission 奖励就不能凭通货无限抽 Bonus。
- 普通 Bonus License 同 Block 只能部署一次；重复部署只有明确稀有规则允许。
- 完成 Commission 后只能领取一次三选一；重载、暂停、切页不能重抽奖励。
- Block Handover 后 local Bonus 不泄漏到新 Block；选中的 2 个 portable Bonus 正确成为启动 License；District Doctrine 保留。
- Market 同 Tick buy > sell，负价格也不能形成无成本循环；交易量、仓容、通货严格守恒。
- 两个施工项目争 Construction Capacity 不依赖字典遍历顺序。
- DT 推进时间而不是绕过 CP；升级价格和 ETA 可预览。
- Urban Fabric 不修改模拟状态 / RNG，只改变画面。
- 0.3.2 旧档导入不丢建筑、人口、库存、Slot、已装 Bonus、运营商与城市历史。

## 16. 明确不进 0.4

- City Life 式人口文化 / 阶层系统。
- 组织友好度、总部、完整组织发展树。
- 跨 Block 实时物流。
- 噪音 / 垃圾完整新链。
- 企业财务与金融市场。
- 完整 City 终局 / 托卡马克之冠。

这些都应建立在 0.4 的成长、Bonus、Commission、Trade 已经稳定之后。
