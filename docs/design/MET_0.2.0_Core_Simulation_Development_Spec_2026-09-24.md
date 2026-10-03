# MET 0.2.0 核心模拟开发规格

**版本日期：2026-09-24**  
**用途：作为 0.2.0 重构的设计合同与实现验收依据。**  
**目标：先把“少量对象 + 通用规则 + Bonus/Policy 改写规则”这套 Build 发动机做对，再扩系统。**  
**本次修订：补入 Building Bonus Slot、跨尺度 Bonus、Operator / Tender、Bonus 来源组织等已确认设计。**

---

# 0. 一句话目标

0.2.0 不追求“系统更多”，而追求：

> **同一个基础建筑，因为地块、组织、Field、Bonus、Policy 与当前城市状态不同，价值可以发生明显变化，并形成玩家自己识别出来的 Build。**

0.2.0 的核心验证问题：

> **单 Block、单地块、单等级建筑，能不能仅靠少量基础对象和通用 Rule 产生明显的路径依赖、价值翻转和空间组合？**

---

# 1. 0.2.0 的范围边界

## 1.1 本版必须做

- 单 Block 可玩闭环；
- 单地块建筑；
- 单等级建筑；
- PopulationGroup；
- Building；
- Tile；
- Field；
- Resource / Inventory；
- Organization / Operator；
- Block Trait / Bonus / Policy；
- Building Bonus Slots：Production / Logistics / Operations；
- Rule；
- 基础招标：玩家先选基础建筑，再从同底盘的“Operator + 初始 Bonus”完整方案中选择；
- 可解释 UI：玩家能看懂“为什么这个数变了 / 为什么这个建筑现在值钱”。

## 1.2 本版暂不做

- 多地块建筑玩法；
- 建筑升级树；
- 跨 Block / 跨 District 的完整物流网络；
- 企业财务；
- 复杂组织外交；
- 定时 Bonus；
- 大量制造品层级；
- 复杂 District / City 终局系统；
- 为了“丰富”而新增大量独立系统。

原则：

> **如果一个系统不能帮助验证 Build 发动机，就先不进 0.2.0。**

---

# 2. 总体对象模型

当前 0.2.0 核心对象：

1. **Block**：当前模拟与治理的区域容器。
2. **Tile**：空间置物架。
3. **Building**：生产、居住、服务等活动的主要载体。
4. **PopulationGroup**：人口群体实体。
5. **Field**：某个实体向空间投射的影响 / 可达性实体。
6. **Organization**：建筑的运营组织。
7. **Bonus**：挂载在实体上的一组 Rule。
8. **Policy**：挂载在 Block 固定政策槽中的一组 Rule。
9. **Rule**：最小可执行规则句子。

另有非 Entity 的基础数据：

- **ResourceType**：资源类型名称；
- **Inventory / Storage Capacity**：建筑持有的资源数量与容量；
- **Label**：后天形成、可变化的描述性标签；暂不作为 Trait 使用。

---

# 3. 统一概念

## 3.1 Trait

Trait 表示：

> **对象先天是什么。**

规则：

- Trait 应当稳定；
- 如果一个性质是发展过程中获得或失去的，它原则上不叫 Trait；
- 不同 Entity 使用自己的 Trait 命名空间，如 BuildingTrait、TileTrait、BlockTrait、OrganizationTrait；
- Trait 本身不等于 Buff，只用于描述身份、性质和供 Rule 查询。

例：

- TileTrait：[水岸]、[森林]、[肥沃]；
- BuildingTrait：[工业]、[住宅]、[服务]；
- BlockTrait：[河谷]、[港湾]。

## 3.2 Label

Label 表示：

> **对象后来变成了什么 / 当前处于什么状态。**

例如“工业中心”如果是城市发展后形成的，就更适合是 Label，而不是 Trait。

0.2.0 可以保留接口，但不需要围绕 Label 做大量玩法。

## 3.3 Bonus

Bonus 表示：

> **某个 Entity 获得的一组额外 Rule。**

Bonus 基本字段：

- ID；
- Allowed Host Type(s)；
- 对 Building Bonus：Allowed Slot Type(s)；
- Rules[]；
- 互斥声明 / 互斥组；
- Provider / Provenance Organization（若该 Bonus 来自某个运营组织）；
- 当前是否 Active 属于挂载状态，不属于 Bonus 定义本身。

一般规则：

- Bonus 默认可以共存；
- 只有明确声明互斥，或 Host 的 Slot 结构产生冲突时，Bonus 才不能同时生效；
- 同一个 Bonus 可以允许挂在多个尺度，例如 Building / Organization / Block；
- 同一 Bonus 挂载尺度不同，Rule 语义可保持一致，主要通过作用域、获取难度、代价、稀有度等形成差分；
- “获取难度 / 价格 / 出现权重”属于 Offer / Tender / Reward 层，不要硬写进 Bonus 本体；
- 0.2.0 不做“持续 N Tick”的定时 Bonus。

## 3.4 Policy

Policy 表示：

> **Block 当前主动采用的制度规则。**

Policy 本质也是一组 Rules，但获取和激活方式不同于 Bonus：

- Block 有固定 Policy Slots；
- 每个槽位同时只能激活一个 Policy；
- 同槽 Policy 天然互斥；
- Policy 可以影响 Block 内几乎所有对象和计算。

例：税制槽可以在“人头税 / 交易税 / 生产税”中选择一个。

---

# 4. Block

Block 是 0.2.0 的核心模拟容器，可以理解为“大尺度对象”。

## 4.1 基础字段

- ID；
- Tiles；
- BlockTraits；
- BlockLabels（可选）；
- ActiveBonuses；
- PolicySlots；
- Block 级统计 / 状态（仅在真正需要时增加）。

## 4.2 Block Trait

只记录先天、稳定的区域性质。

例如：

- [水岸]
- [河谷]
- [肥沃]

后天发展结果不要塞进 Trait。

## 4.3 Block Bonus

- 默认可叠加；
- 特定 Bonus 可以自行声明互斥；
- Bonus 通过 Rules 改写 Block 内的模拟规则。

## 4.4 Block Policy

- 固定政策槽；
- 每槽一个当前选项；
- Policy 通过 Rules 作用于整个 Block。

---

# 5. Tile

Tile 的定位：

> **空间置物架。**

Tile 负责描述：

1. 自己在哪里；
2. 自己有什么先天性质；
3. 当前放了什么建筑；
4. 当前有哪些 Field 投射结果落在这里。

Tile 不负责解释这些东西如何相互作用。

## 5.1 基础字段

- 坐标；
- TileTraits；
- 当前 Building；
- 运行时 Field Cache / Field Registry；
- 邻接关系由坐标派生。

## 5.2 重要原则

- Tile 不持有“污染如何影响健康”这种规则；
- Tile 可以缓存 Field 的计算结果，但缓存不是 Field 本体；
- 地形也通过 TileTrait 表达，而不是独立再造一套地形系统。

---

# 6. Building

Building 是城市活动的主要载体。

## 6.1 基础字段

- Building Type；
- 所在 Tile；
- Operator Organization；
- BuildingTraits；
- ActiveBonuses；
- 状态；
- Building Efficiency；
- Utilization；
- Maintenance；
- Job Capacity；
- Footprint（0.2.0 玩家侧固定为单地块）；
- Construction Cost；
- Operating Inputs；
- Base Outputs；
- Service Capacity；
- Inventory；
- Storage Capacity；
- 其他真正必要的建筑变量。

## 6.2 Building Bonus Slots

0.2.0 的 Building 使用 **3 类有限 Bonus Slot**：

1. **Production**：这栋建筑“怎么干活”。主要影响输入、输出、Capacity、Efficiency、Conversion 等。
2. **Logistics**：这栋建筑“怎么与外界交换”。主要影响 Access Field、Resource Allocation、Storage、距离衰减、Redirect 等。
3. **Operations**：这栋建筑“以什么方式被运营”。主要影响 Organization、就业关系、身份解释、同运营组织协同等。

重要：

- `operator_org` **不是第四个 Slot**，而是 Building 与 Organization 的关系字段；
- Building Bonus 必须声明自己允许占用的 Slot Type；
- Slot 用来制造“不能全要”的构筑约束；
- 少数高价值 Meta Bonus 可以修改 Slot 结构，例如：
  - 增加某类 Slot；
  - 将某类 Slot 替换成另一类；
  - 允许一个 Slot 临时视为另一类；
- 修改 Slot 的 Bonus 应当稀少，避免 Slot 限制被大量 `+1 Slot` 轻易绕过；
- 0.2.0 不要求把 Block Bonus 也强行套用同样的三槽结构；Block Bonus 的细分槽位待内容池验证后再冻结。

## 6.3 三个必须分开的概念

### Building Efficiency

建筑自身的生产/转换效率。

### Personnel Efficiency

PopulationGroup 中 1 Pop 可以贡献多少有效劳动力。

### Utilization

建筑在当前劳力、资源、能源等约束下实际能运行多少。

概念式：

> 实际产出 = 基础产出 × Building Efficiency × Utilization

Utilization 以“短板原则”为基础，但函数不要求线性；具体数值函数后续调试。

## 6.4 维护费

- Maintenance 为固定维护；
- 不因为低 Utilization 自动降低；
- 建筑“开得少”不等于“楼就不用维护”。

## 6.5 建筑输出

按建筑类型不同：

- Residential：提供 Housing Capacity，不“生产人口”；
- Production：转换 Resource；
- Service：提供 Service Capacity；
- 其他建筑通过自己的 Rules / Fields 产生作用。

Job 是 Capacity，不是 Output。

---

# 7. PopulationGroup

不模拟单独个人，人口以群体为单位。

默认一个住宅建筑可以承载一个 PopulationGroup；人口群本身独立存在。

## 7.1 为什么是独立 Entity

住宅被拆除后，人口仍然存在；他们可以迁移到其他住宅或进入 Homeless Pool，并保留健康、教育、幸福、就业等状态。

## 7.2 基础字段

- Count；
- 当前 Housing / Host；
- Health；
- Education；
- Happiness；
- Personnel Efficiency；
- Demand；
- Employment Allocation：多少 Pop 在哪些建筑工作。

## 7.3 Homeless Pool

无家可归人口进入一个地图外的特殊“住宅容器”，而不是只存一个人数。

这样可以保留：

- 健康；
- 教育；
- 幸福；
- 就业；
- 其他人群状态。

## 7.4 消耗

人口体系涉及：

- Food；
- Material；
- Manufactured Goods（后期逐步开放）；
- Energy；
- 政府承担的 Population Maintenance Fee；
- 服务通过 Capacity / Field 影响人口，而不是普通库存消耗。

## 7.5 产出

人口只直接产出：

- Labor；
- Tax（仅在相应 Policy / Rule 存在时）。

其他如需求、幸福等属于状态或派生量，不叫“产出”。

## 7.6 就业

- 1 Pop 最多对应 1 单位基础 Labor；
- 实际有效劳动力受 Personnel Efficiency 影响；
- Block 内默认自由流动；
- 短缺时优先分配给可达性更好 / 距离更近的岗位；
- 不做真实路径寻路。

---

# 8. Field

Field 是独立 Entity，不是 Building 或 Tile 的字段。

定义：

> **某个 Source Entity 向空间中投射出来的一份有类型、有来源、有传播规则的影响。**

## 8.1 基础字段

- Field Type；
- Source Entity；
- Base Value / Strength；
- Propagation Rule。

Field 不必永久保存完整 Coverage Map。

当相关结构变化、Tick 需要或 Field 被标记为 Dirty 时重新求值，并把结果写入 Tile 的运行时 Field Cache。

## 8.2 一个 Source 的不同 Field Type

分别生成不同 Field Entity。

例如同一工厂可以同时有：

- Pollution Field；
- EmploymentAccess Field；
- Heat Field。

## 8.3 Field 不自带“善恶解释”

Pollution = 8 只表示“这里有 8 的污染场贡献”。

“它如何影响健康 / 税收 / 工业效率”由 Rule 决定。

## 8.4 来源保留

Field 计算默认保留 Source。

例如同一 Tile：

- Factory A Pollution +5；
- Factory B Pollution +3；
- Park C Pollution -2。

某 Rule 可以选择：

- SUM；
- MAX；
- MIN；
- 不聚合，逐 Source 处理；
- 其他特定解释。

## 8.5 Access Field

Access Field 的值表示：

> **可达性 / 分配优先程度。**

它不直接降低已经成功分配后的人员效率。

例如 EmploymentAccess = 0.5 表示“更不容易优先分到这个岗位”，而不是“上班以后只能贡献 50% 劳动力”。

## 8.6 传播规则

默认由 Field Source / Source Rules 定义。

Rule 可以修改传播强度、范围、衰减方式等。

冲突时遵守“特殊 > 一般”。

---

# 9. Resource / Inventory

Resource 本身不是 Entity。

ResourceType 只是资源名称 / 类型：

- Food；
- Material；
- Manufactured Goods；
- Energy；
- 后续其他真正需要的资源。

## 9.1 库存

资源数量挂靠在 Building 的 Inventory 上。

生产建筑有自己的本地库存；城市还拥有少量公共仓储能力，可实现为地图外的特殊仓库建筑。

Bonus、建筑特殊能力、科技等可以修改 Storage Capacity。

## 9.2 Block 内物流

0.2.0 不模拟完整物流网络。

在同一个 Block 内：

> **只要资源可以被搬运，就默认可以在区域内流通。**

当供给不足时：

> **距离资源来源更近的消费者优先得到资源。**

真正的完整物流网络留到 Block↔Block / Block↔District 等更高层级再做。

## 9.3 库存满时

顺序：

1. 产物进入生产建筑本地库存；
2. 本地满后自动尝试进入可用仓库；
3. 仓储压力过高时，生产效率受到限制；
4. 实在无法容纳的部分直接损耗（“送到火星”）。

具体阈值后续调数值。

## 9.4 能源

Energy 仍然属于 Resource，但默认不可储存。

每 Tick 进行即时供需结算：

- Energy Supply；
- Energy Demand；
- 分配；
- 不足则影响建筑 Utilization。

只有后续获得储能能力后，Energy 才能进入 Inventory。

## 9.5 不属于 Resource 的东西

玩家 / 政府控制的类货币资源，例如：

- Money；
- DT；
- Mandate / Support 等治理状态；

不放入普通 Resource 系统，单独保存。

---

# 10. Organization / Operator

Organization 是负责运营一组建筑的实体；在招标语境中，它就是 **Operator / 运营商**。

建筑字段使用 `operator_org`，避免把“建设者 / 所有权 / 运营者”混为一谈。Operator 不是 Building Bonus Slot。

## 10.1 基础字段

- OrganizationTraits；
- ActiveBonuses；
- Rules（如果 Organization 定义本身需要基础规则）；
- Preferred Bonus Pool / Weight Bias；
- Eligible Building Categories（哪些基础建筑类型通常会收到该 Operator 的投标）；
- 它运营哪些建筑可以由 Building.operator_org 反查，不要求双向重复存储。

Operator 的“偏好池”采用 **soft bias**：

- 某些 Bonus 在该 Operator 的 Tender 中出现概率更高；
- 不等于某个 Operator 永久绑定某个 Bonus；
- 不用“选了这个 Operator = 宣布这局只能玩这个流派”的 hard lock；
- 运营商的意义主要来自 OrganizationTrait / OrganizationRule、同组织关系网络、以及对 Tender 候选分布的偏好。

## 10.2 组织不做什么

0.2.0 不做：

- 企业自己的钱；
- 企业库存；
- 企业利润表；
- 独立企业经营模拟。

## 10.3 组织关系

暂不造独立“外交关系系统”。

如果需要描述合作、竞争等关系，优先通过 Trait / Bonus / Rule 表达。

## 10.4 Bonus 来源组织 / Provenance

Bonus 可以记录 `provider_org` / `provenance_org`，表示这份方案由哪个 Operator 提供。

这与 Building 的 `operator_org` 是两个可分别读取的关系：

- Building Operator：当前谁运营这栋建筑；
- Bonus Provider：这项 Bonus / 方案是谁提供的。

Rule 可以查询：

> `bonus.provider_org == building.operator_org`

从而表达“自营方案”“同组织改造”“内部协同”等通用机制，而不是为每个 Operator × Bonus 手写特殊套装。

Bonus Provider 本身不是 Trait；它是来源关系。

## 10.5 作用范围

Organization Rule 只直接影响 Building。

它可以查询空间、同组织关系、Trait 等，但不要直接把 Organization 做成全城 Buff 源。

---

# 11. Bonus

Bonus 是一组 Rules 的容器，也是 0.2.0 Build 系统的主要可组合内容单位。

## 11.1 字段

- ID；
- Allowed Host Type(s)；
- Allowed Slot Type(s)（Building Bonus 时使用）；
- Rules[]；
- Exclusion / Exclusive Group；
- Provider / Provenance Organization（可选）。

## 11.2 一般规则

- 默认可叠加；
- 只有明确声明互斥，或 Host Slot 冲突时才不能共存；
- 生命周期不属于 Bonus 定义本身；
- Bonus 可以被挂载、Active、被互斥 Bonus 挤下去；
- 0.2.0 不做定时 Bonus。

## 11.3 跨尺度 Bonus

同一个 Bonus 可以允许多个 Host Type，例如同时允许：

- Building；
- Organization；
- Block。

只要 Rule 在不同作用域下仍然有清晰语义，就不必人为拆成“建筑版”和“Block 版”。

尺度差分优先来自：

- 作用范围；
- 出现概率；
- 获取代价；
- Offer / Reward 条件；
- 互斥和前置限制。

不要默认用“Block 版数值更弱”来做平衡。

## 11.4 Slot 修改类 Bonus

少数 Bonus 可以把 Slot 本身当作可修改对象，例如：

- `+1 Logistics Slot`；
- `-1 Production Slot, +1 Operations Slot`；
- 某 Logistics Slot 可视为 Production Slot。

这类 Bonus 会改变后续选择空间，因此属于高价值 Meta Bonus，应当稀少并重点测试。

---

# 12. Policy

Policy 与 Bonus 一样，本质是 Rules 的容器，但属于 Block Policy Slot。

## 12.1 字段

- ID；
- 所属 Policy Slot；
- Rules[]。

## 12.2 激活规则

- 每个 Slot 同时一个 Active Policy；
- 替换 Policy 时，旧 Policy Rules 失活，新 Policy Rules 生效；
- Policy 可以广泛作用于 Block 内的 Building、PopulationGroup、Field、Resource 结算、Organization、财政等。

---

# 13. Rule：核心模拟语言

Rule 是整个系统最小的可执行句子。

原则：

> **一条 Rule 只做一件事。**

一条 Bonus 可以包含多条 Rule。

## 13.1 Rule 可以读取什么

Rule 条件可以同时查询：

- Building；
- Tile；
- PopulationGroup；
- Field；
- Organization；
- Block；
- Trait；
- Bonus；
- Policy；
- Resource / Capacity / State 等当前值。

## 13.2 Rule 的基础动作类型

0.2.0 至少支持以下语义：

### Modify
修改一个数值：Add / Multiply / Set / Cap / Clamp 等。

### Convert
把一种可计量量转换成另一种：

> 输入 → 输出。

### Emit / Modify Field
创建 Field，或修改 Field 的强度、传播范围、衰减等。

### Gate
改变资格：谁能参与某种分配、建设、服务、规则。

### Relate
建立 / 改写特定语义关系，而不是伪造物理事实。

### Redirect
改变资源、服务、效果等流向 / 接收者。

### Interpret / Aggregate
决定多个来源的数据如何被读取，例如 Field 的 SUM / MAX / Source-wise。

必要时以后可以扩展，但不要为了单个内容造一次性特殊操作符。

## 13.3 Rule 的基本字段

- ID；
- Phase / Trigger；
- Conditions / Selector；
- Target；
- Operator；
- Value / Formula；
- Priority / Specificity 信息。

---

# 14. Rule 冲突原则

## 14.1 特殊 > 一般

当两条 Rule 真正修改同一个目标、同一个语义且发生冲突时：

> **更具体的 Rule 优先于更一般的 Rule。**

未来如果仍无法决出，再使用显式 Priority。

## 14.2 不冲突则共同生效

例如：

- 一条修改 Pollution Range；
- 一条修改 Pollution Strength；

二者互不冲突，全部执行。

## 14.3 Phase 与 Priority 不得混淆

- **Phase**：因果上什么时候算；
- **Modifier Pipeline**：同一数值的数学运算顺序；
- **Priority / Specificity**：真正冲突时谁覆盖谁。

三者是不同概念。

---

# 15. 0.2.0 Tick：简化结算管线

用户侧只需要理解“分轮结算”。

基本原则：

> **同一步里的对象读取同一份稳定状态，先提交需求 / 效果，统一结算后再进入下一步。**

这样既合理，也为以后并行计算留出空间。

## Step 1：算空间

- Tile / Trait；
- Field 传播；
- Employment / Service 等可达性。

## Step 2：分东西

- Population / Labor 分配；
- Food / Material / Goods 等资源分配；
- Energy 单独做即时供需结算。

短缺时按既定规则，例如距离来源近者优先。

## Step 3：建筑运行

每栋建筑根据：

- Labor 满足度；
- Resource 满足度；
- Energy 满足度；
- Building Efficiency；

计算 Utilization。

## Step 4：生产与仓储

- 实际吞资源；
- 实际吐资源；
- 本地库存；
- 仓库；
- 仓储瓶颈；
- 最终损耗。

## Step 5：环境 / 服务 / 人口结算

根据实际运行结果产生：

- Pollution；
- Heat；
- 医疗 / 教育 / 娱乐等服务；
- 其他空间影响。

随后更新 PopulationGroup：

- Health；
- Education；
- Happiness；
- Personnel Efficiency；
- Demand。

## Step 6：财政 / 治理 / 结构变化

最后处理：

- Tax / Money；
- Support / Mandate（若 0.2.0 使用）；
- 人口迁移 / 增减；
- Entity 创建 / 删除；
- Bonus / Policy 状态变化；
- 其他结构性修改。

重要限制：

> **结构性增删不要在 Tick 中途直接发生。**

Rule 可以要求“产生 1 Population”“创建 Field”等，但真正改变结构在最后 Commit。

---

# 16. 同一数值的统一 Modifier Pipeline

为了保证规则可预测、可调试，所有普通数值修改默认采用统一顺序：

> **Base → Flat Add/Subtract → Multiply → Cap/Floor/Clamp → Final Override**

例：

- Base = 10；
- +5；
- ×1.5；
- 最大值 20；

结果：

> (10 + 5) × 1.5 = 22.5 → 20。

Final Override 只用于真正表达“固定为 X”的特殊 Rule，并遵守“特殊 > 一般”。

---

# 17. 实现层必须遵守的三条约束

这些是给 Codex / 工程实现看的，不要求玩家理解。

## 17.1 Rule 不直接抢着改共享世界

同一步里先计算需求 / Effect / Intent，再统一结算。

例：只有 10 Material，A/B 都申请 8，不能因为程序先算 A 就让 A 自动拿走 8；必须统一按规则分配。

## 17.2 同样输入必须得到同样结果

同一个 Save / Seed / 玩家操作，不应因为机器、线程顺序不同而产生不同模拟结果。

## 17.3 先按“可并行”结构写，0.2.0 不要求立刻多线程

0.2.0 可以单线程实现；但同阶段对象之间不要依赖“谁先执行”。

目标是未来性能不够时，只需要并行 Job，而不是重写游戏规则。

---

# 18. 0.2.0 招标 / Operator / Build Loop

0.2.0 推荐采用以下核心交互：

1. 玩家先选择自己要建设的**基础建筑类型**；
2. 基础建筑底盘由玩家意图决定，不由随机池决定；
3. 游戏生成 2～3 个完整 Tender Offer；
4. 每个 Offer 由：
   - **Operator / Organization**；
   - **一个初始 Bonus**；
   - 必要的价格 / 条件等 Offer 元数据；
   共同组成；
5. 各 Offer 的基础建筑数值保持相同，差异主要来自 Operator 与初始 Bonus；
6. 玩家只做一次选择：**选哪份完整投标方案**；
7. 选中后：
   - Building.operator_org = 该 Operator；
   - 初始 Bonus 挂入它对应的 Production / Logistics / Operations Slot；
   - 后续其余 Slot 可以通过未来机会继续构筑；
8. 新建筑进入 Organization 关系网，并通过 Bonus / Trait / Field / Policy 改变后续选项价值。

## 18.1 Tender 的核心原则

> **玩家决定“建什么”；Tender 随机决定“谁来做，以及准备怎么做”。**

因此：

- 随机性决定“怎么建”，不要决定基本城市功能能不能建；
- Operator 不是一个需要玩家额外选择的第四槽；
- 不要让玩家在一次建造里依次选择“建筑 → Operator → Bonus → Slot”；Tender 应把 Operator + 初始 Bonus 打包成一次可读决策。

## 18.2 Operator 与 Bonus 不做硬绑定

每个 Operator 可以有自己的 Bonus 偏好池 / 权重，但这只是 soft bias：

- 某些方案更常见；
- 某些方案较少见；
- 不等于某 Bonus 只能由某 Operator 提供；
- 不等于选择某 Operator 就被迫进入固定流派。

这样 Operator 可以保留辨识度，又不会把 Build 变成预制职业。

## 18.3 Same-Operator Synergy

Building.operator_org 与 Bonus.provider_org 可以被 Rule 同时读取。

若两者相同，某些 Organization Rule / Bonus Rule 可以触发额外效果。

重要：

> 不要手写“Operator A + Bonus B = 特殊套装”。

应优先写通用语法，例如“自营改造方案”“同组织项目”“内部供应链”等，让多个 Bonus 都能复用。

## 18.4 初始 Operator 数量

0.2.0 不需要很多 Operator。内容设计阶段优先控制在约 6～8 个可辨识 Operator，再观察是否足够形成候选差异。

Operator 可以对建筑领域有明显偏好，例如工业、商业、文化教育、公共服务、住宅，以及少量通用 Operator；但领域偏好不等于绝对限制。

---

# 19. 0.2.0 的 Build 测试内容方向

0.2.0 不要手写“农业套 / 工业套”。

可以预埋若干通用规则用于测试，例如：

- 高污染工业；
- 污染回收；
- Heat 产生与供热；
- 同 Organization 建筑协同；
- Building 三类 Bonus Slot 的竞争；
- 少量 Slot 修改型 Meta Bonus；
- 同一个 Bonus 在 Building / Block 不同尺度上的价值差分；
- Bonus Provider 与 Building Operator 的 same-operator synergy；
- Field 传播改写；
- 不同税基 Policy；
- 仓储能力；
- 资源短缺时的空间优先级。

这些规则应能交叉组合，目标是让玩家自然发现：

- 污染有时会从负担变成资源；
- 同一位置的价值会因为已有 Field / Organization / Policy 改变；
- 同一基础建筑在不同局面中候选 Bonus 的价值不同；
- 至少存在两个明显不同的可行结构，并最好出现设计者没有硬编码的第三种玩法。

---

# 20. 可解释性 UI 是 0.2.0 的必要功能

此前试玩已经证明：系统即使存在，如果玩家看不见关系，也不会形成 Build 感。

因此 0.2.0 必须让玩家能回答：

> **“为什么？”**

至少需要：

- 建筑实际 Utilization 来源；
- 缺什么导致短板；
- 当前受到哪些 Bonus / Policy / Organization Rule 影响；
- Field 来源；
- 某项数值是 Base + 哪些修改得到的；
- 资源为什么分给 / 没分给这个建筑；
- 人口为什么选择这个岗位 / 服务。

不要求做复杂 debug UI，但必须能追因果。

---

# 21. 0.2.0 重构顺序

不要一次全推倒。

## 阶段 A：冻结数据合同

先实现 / 整理：

- Entity 类型；
- Trait 命名空间；
- Bonus / Policy 容器；
- Rule 数据结构；
- Block / Tile / Building / PopulationGroup / Field / Organization 数据边界。

目标：数据模型先统一。

## 阶段 B：重写核心 Tick

按六步结算：

1. 空间；
2. 分配；
3. 运行；
4. 生产仓储；
5. 环境服务人口；
6. 财政与结构 Commit。

目标：把“谁先执行谁占便宜”的隐式顺序清掉。

## 阶段 C：接入 Rule

先支持少量通用 Operator：

- Modify；
- Convert；
- Emit；
- Gate；
- Redirect；
- Interpret；
- Relate（如 0.2.0 测试内容需要）。

目标：Build 内容尽量通过 Rule 数据表达，而不是散落在建筑专用 if/else 中。

## 阶段 D：接 Building Slot、招标与 Bonus / Policy

- Building 接入 Production / Logistics / Operations 三类 Slot；
- 基础建筑由玩家主动选择；
- 同底盘 Tender 候选；
- 每份候选打包 Operator + 初始 Bonus；
- Operator 使用 Trait / Rule + Bonus 偏好池，不与 Bonus 硬绑定；
- Bonus 记录可选 Provider / Provenance；
- 支持 same-operator Rule 查询；
- Block Policy Slots 可切换。

## 阶段 E：补可解释 UI

让每个重要结果能回溯原因。

## 阶段 F：Build Playtest

不要看“系统有没有跑起来”，而看：

> **玩家有没有开始因为既有城市结构而重新评价新选项。**

---

# 22. 必须通过的验收用例

0.2.0 至少应有以下自动 / 半自动测试：

1. 同一输入重复运行得到同一结果；
2. 两个消费者争同一资源时，不受代码遍历顺序影响；
3. 短缺时距离来源近者按规则优先；
4. Energy 默认不进入普通库存；
5. 有储能能力后 Energy 才可以被保存；
6. 生产建筑库存满后先转移仓库，再出现生产限制 / 损耗；
7. 住宅拆除后 PopulationGroup 不消失，而是迁移 / 进入 Homeless Pool；
8. 同一 Source 的不同 Field Type 是不同 Field Entity；
9. Field Source 不被 Tile 汇总永久抹掉；
10. Access Field 只影响可达性 / 分配，不二次削减 Personnel Efficiency；
11. Organization Rule 只直接作用于 Building；
12. Bonus 默认共存，声明互斥时才互斥；
13. Policy 同槽只能激活一个；
14. Rule 一条只做一件事；
15. 同数值修改遵守统一 Modifier Pipeline；
16. 特殊 Rule 覆盖一般 Rule 时结果稳定可解释；
17. Tick 中途不直接插入结构性 Entity 改动；
18. UI 能解释至少：Utilization、Field 来源、Bonus/Policy 修改、资源短缺原因；
19. Building 只有 Production / Logistics / Operations 三类 Bonus Slot，Operator 不占 Slot；
20. Building Bonus 只能进入自己允许的 Slot；
21. Slot 修改型 Bonus 能稳定改变后续可装载空间；
22. 同一 Bonus 若允许 Building / Block 多尺度挂载，在不同 Host 上作用域正确；
23. Tender 中同一基础建筑的底盘数值一致，差异来自 Operator + 初始 Bonus；
24. Operator 的偏好池只改变候选权重，不形成 Bonus 硬锁；
25. Bonus Provider 与 Building Operator 可分别查询，并能稳定触发 / 不触发 same-operator Rule。

---

# 23. 0.2.0 成功标准

不是“功能都做完”。

成功标准是试玩中频繁出现以下感觉：

- “这个建筑本来一般，但我现在特别需要它。”
- “这个 Bonus 上一局没用，这局居然特别强。”
- “我为了利用已有污染 / 热量 / 组织关系，主动改变了布局。”
- “我现在特别想抽到某类规则把这套机器接起来。”
- “同一张东西放在这里和放在那里价值完全不同。”
- “这个组合不是游戏告诉我的套装，是我自己发现的。”

如果系统很多，但这些感觉仍然没有出现：

> **0.2.0 仍然没有解决 Build 发动机。**

---

# 24. 当前仍未冻结、但不阻塞 0.2.0 的事项

以下内容以后再设计，不要为了它们阻塞当前重构：

- City / District 完整宏观对象；
- Block 与 District 之间的物流；
- 完整 Government / Mandate / Support 数值模型；
- DT 的最终经济参数；
- Event 系统；
- Label 的完整用途；
- Block Bonus 是否以及如何使用更细的 Slot 分类；
- Operator 更换 / 收购 / 改组 / 国有化等后续组织玩法；
- 多地块建筑；
- 建筑升级；
- 制成品完整分级；
- 组织之间复杂关系；
- Bonus 定时持续时间；
- 完整科技树；
- 长局终局与托卡马克之冠实现。

这些都不影响 0.2.0 先验证 Build。

---

# 25. 给开发 Agent 的一句话

> **不要继续往旧系统上补功能。先把 0.2.0 重构成一个“Block + Entity + Field + Resource + Organization/Operator + Building Bonus Slots + Bonus/Policy + Rule”的确定性分轮模拟内核。玩家决定建什么，Tender 只决定谁来做、准备怎么做；Operator 与 Bonus 不硬绑定。再用少量内容验证同一基础建筑的价值能否随上下文显著变化。不要写预制套装，不要用大量建筑专用 if/else 伪装涌现。**
