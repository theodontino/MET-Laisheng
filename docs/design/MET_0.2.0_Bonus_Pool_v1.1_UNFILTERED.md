# MET 0.2.0 Bonus Pool v1.0

**版本日期：2026-09-24**  
**定位：0.2.0 第一版可组合内容池。**  
**目标：不是做“强卡”，而是提供足够多的规则改写，让 Building / Block / Organization / Field / Resource / Population 之间产生可发现的 Build。**

**v1.0.1 修订：** 补入土地出让年金、污染产业园、热电共生、排污权交易/污染计源；商业综合体拆分为 0.2.0 可实现的“万能半邻接”与未来 Footprint 扩展。

> 本文只定义 Bonus 原型。  
> **稀有度、抽取权重、运营商偏好、价格、互斥组、数值平衡暂不锁定。**  
> 这些在 v1.1 / Operator Pool 阶段再做。

---

## 0. Bonus Pool 规则

### 0.1 Building Bonus Slot

Building 暂定 3 类有限 Slot：

- **Production**：怎么生产 / 转换 / 提供 Capacity。
- **Logistics**：怎么交换 / 分配 / 存储 / 传播 Access。
- **Operations**：怎么运营 / 与组织、人口、身份、邻接发生关系。

`operator_org` 不是 Slot。

### 0.2 Bonus 的来源组织

所有通过 Tender 获得的 Building Bonus 实例都可以记录：

- `provider_org`

Building 自己记录：

- `operator_org`

Rule 可以查询：

> `bonus.provider_org == building.operator_org`

但**不是每张 Bonus 都必须利用这个关系**。  
Bonus Provider 是关系字段，不是 Trait。

### 0.3 跨尺度

同一个 Bonus 只有在语义自然时才允许多个 Host。

- Building Host：局部改造。
- Organization Host：组织体系。
- Block Host：区域范式。

不要为了“统一”强迫所有 Bonus 跨尺度。

### 0.4 当前状态标记

- **CORE**：建议进入 0.2.0 首轮可玩池。
- **EXP**：底层支持，但需要重点测试。
- **FUTURE**：概念保留，0.2.0 暂不实现。

文中的数值全部是 **Prototype 数值**，用于把 Rule 写完整，不代表最终平衡。

---

# 1. Production Bonus

## P01 粗野制造

**Status:** CORE  
**Allowed Host:** Building / Block  
**Building Slot:** Production  
**Selector:** `[Industrial]`

Rules：

1. Operating Inputs × **1.50**
2. Pollution Field Strength × **1.50**
3. Base Outputs × **2.00**

核心变化：

> 用更高投入和污染换高吞吐。  
> 它会自然抬高回收、供热、污染利用类 Bonus 的价值。

---

## P02 研究型医院

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Production  
**Eligible Building:** `[Medical]`

Rules：

1. 原 Medical Capacity 的 **30%** 被转换为 Education Capacity。
2. Medical Capacity 保留 **70%**。
3. 与本建筑邻接的 `[Education]` Building Efficiency × **1.20**。

核心变化：

> 医疗设施从纯服务节点变成“医疗—教育”组团核心。

---

## P03 实践教育

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Production  
**Eligible Building:** `[Education]`

Rules：

1. Education Building Efficiency × **0.85**。
2. 在邻接建筑工作的 Population，其有效 Labor × **1.20**。

核心变化：

> 少教一点“课堂教育”，换取周边实际生产的人力质量。

---

## P04 生物质能公共设施

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Production  
**Eligible Building:** `[PublicService]`

Rules：

1. Maintenance × **1.50**。
2. 位于本建筑 Service Access 范围内的 `[Residential]` Building 获得转换：
   - 5 Food → 2 Material
   - 5 Food → 2 Energy Supply

说明：

- Energy 仍然按当 Tick 即时供需结算；
- 这里不是给住宅新增库存，而是给其增加一个可用 Conversion Rule。

核心变化：

> 公共设施把居民消费体系变成小规模物质 / 能源循环。

---

## P05 热量管理

**Status:** CORE  
**Allowed Host:** Building / Block  
**Building Slot:** Production  
**Selector:** `[Industrial]`

Rules：

1. Product Output × **0.90**。
2. Pollution Output × **0.60**。
3. Emit `Heat Field`：
   - Heat Strength = 0.10 × 原 Product Output + 0.40 × 原 Pollution Output。

核心变化：

> 把工业的一部分主产物与污染副作用重新编码成 Heat。

---

## P06 高级回收

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Production

Rules：

1. Maintenance × **1.50**。
2. 本建筑所在 Tile 可读取并消耗 Pollution：
   - 每消耗 10 Pollution
   - → +3 Material
   - → +3 LowGoods

说明：

- Pollution 必须真实减少，不能“读取一次无限发电”；
- 若多个来源叠加，按 Field Source 保留后再由回收 Rule 处理。

核心变化：

> Pollution 从纯负面状态变成生产资料。

---

## P07 家庭农场

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Production  
**Eligible Building:** `[Agricultural]`

Rules：

1. 获得少量 Housing Capacity。
2. Food Output × **0.85**。
3. Hosted Population 对本建筑 Job Capacity 拥有最高 Employment Allocation 优先级。

核心变化：

> 农业、住宅和就业被压进同一个局部节点。

---

## P08 水基工厂

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Production  
**Eligible Building:** `[Industrial]`

Gate：

- Host Tile 必须具有 `[Waterfront]`。

Rules：

1. Material Resource Allocation 的距离惩罚 × **0.50**。
2. Pollution Field 沿 `[Waterfront]` Tile 传播时，衰减降低 **30%**。

核心变化：

> 水岸同时成为供应优势和污染传播通道。

---

## P09 污物固化

**Status:** EXP  
**Allowed Host:** Building  
**Building Slot:** Production  
**Eligible Building:** 产生 Pollution 的 Building

Rules：

1. Pollution 传播范围 × **0.30**。
2. 被阻止向外传播的污染转化为 `SolidWaste` 库存。
3. SolidWaste Storage 满时，本 Bonus 的传播抑制效果停止。

核心变化：

> 不“消灭污染”，而是把空间污染问题转换成仓储问题。

说明：

- 需要新增一个很轻的 `SolidWaste` ResourceType；
- 如果 0.2.0 不想扩 Resource，可暂时不进入首轮实现。

---

# 2. Logistics Bonus

## L01 电商仓储店

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Logistics  
**Eligible Building:** `[Commercial]`

Rules：

1. Service Access Field 衰减降低 **30%**。
2. Entertainment Capacity Final Override = **0**。
3. 与 `[Industrial]` 建筑之间已有的邻接影响 × **2.00**。
4. Storage Capacity × **1.50**。

核心变化：

> 商业从“线下娱乐/服务节点”转向远距离消费与工业接口。

---

## L02 无人机站

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Logistics

Rules：

1. Emit `DroneDeliveryAccess Field`。
2. 在该 Field 覆盖范围内，Resource Allocation 的距离惩罚降低 **60%**。
3. Energy Demand × **1.25**。
4. Maintenance × **1.30**。

核心变化：

> 不增加资源，只重写“距离有多重要”。

---

## L03 零库存生产

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Logistics  
**Eligible Building:** 有 Operating Inputs 的 Building

Rules：

1. Local Storage Capacity × **0.20**。
2. Resource Allocation Priority 提高。
3. Building Efficiency × **1.20**。
4. 任一关键输入 Satisfaction < 100% 时，Utilization Cap = **0.50**。

核心变化：

> 高效率但极度依赖稳定供应。

---

## L04 邻里中心

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Logistics  
**Eligible Building:** `[PublicService]` / `[Commercial]`

Rules：

1. 读取邻接 Service Field。
2. 可将这些 Service Access 以 **50% Strength** 作为 Secondary Source 再传播。
3. 不新增 Service Capacity。
4. Maintenance × **1.20**。

核心变化：

> 不是多造服务，而是改变服务的空间形状。

---

## L05 中转仓

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Logistics

Rules：

1. Storage Capacity × **2.00**。
2. 邻接 Producer 的 Overflow 在进入公共仓库前，优先 Redirect 到本建筑。
3. Building Efficiency × **0.90**。

核心变化：

> 用建筑自身效率换局部仓储与溢出缓冲。

---

# 3. Operations Bonus

## O01 商业医疗

**Status:** CORE  
**Allowed Host:** Building / Block  
**Building Slot:** Operations  
**Selector:** `[Medical]`

Rules：

1. `[Medical]` 与 `[Commercial]` 之间允许产生邻接协同。
2. Medical Building 不再提供 Happiness。
3. 每单位实际完成的 Healthcare Service 产生额外 `CommercialActivity`。
4. 任何读取 CommercialActivity 的税收 / 财政 Rule 可以读取该值。

核心变化：

> 医疗从公共幸福服务变成可以被商业化和征税的活动。

---

## O02 工厂家属区

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Operations  
**Eligible Building:** `[Residential]`

Rules：

1. 对 Same Operator 的 `[Industrial]` Building，Employment Access 衰减降低。
2. 在其他条件相同的情况下，Same Operator 工业岗位优先接纳本住宅 Population。
3. Pollution 影响不减免。

核心变化：

> 把住宅—就业—组织三张关系网绑在一起，但不免除工业外部性。

---

## O03 密集住宅

**Status:** CORE  
**Allowed Host:** Building / Block  
**Building Slot:** Operations  
**Selector:** `[Residential]`

Rules：

1. Housing Capacity × **1.75**。
2. 单位 Population 的 Population Maintenance Fee × **0.85**。
3. Pollution 与 Service Shortage 对 Population State 的负面解释 × **1.25**。

核心变化：

> 空间效率更高，但更依赖良好的公共环境与服务。

---

## O04 开放大学

**Status:** CORE  
**Allowed Host:** Building  
**Building Slot:** Operations  
**Eligible Building:** `[Education]`

Rules：

1. Education Access 覆盖范围同时产生 Entertainment Access。
2. Entertainment Capacity = **30%** × Education Capacity。

核心变化：

> 同一套教育网络被重新解释为文化娱乐网络。

---

## O05 义务教师

**Status:** CORE  
**Allowed Host:** Building / Block  
**Building Slot:** Operations  
**Selector:** `[Residential]` Population

Rules：

1. Education 高于阈值的 Population 可提供少量 Education Capacity。
2. 这些 Population 从其 Housing Source 发出 Education Access。
3. 参与义务教育的人群可提供 Labor × **0.85**。

核心变化：

> Population State 本身成为服务 Source。

---

## O06 东郊记忆

**Status:** EXP  
**Allowed Host:** Building  
**Building Slot:** Operations  
**Eligible Building:** 当前或历史上具有 `[Industrial]` 身份的 Building

Rules：

1. Host 保留其历史 `[Industrial]` 身份记录，同时获得可被商业规则读取的 `[CommercialUse]` Label。
2. 对商业 / 服务 / 邻接规则而言，可按 `[Commercial]` 资格参与。
3. Base Industrial Output Final Override = **0**。
4. Industrial Pollution Final Override = **0**。
5. 不支付“拆除 + 重建”为商业建筑的完整成本。

Same-Operator Hook：

如果：

> `bonus.provider_org == building.operator_org`

则：

- Base Industrial Output 改为保留原值的 **20%**；
- Industrial Pollution 仍为 **0**。

核心变化：

> 历史建筑不是被删掉，而是被重新解释；同运营组织可保留一部分旧生产能力。

---

# 4. Block Bonus

## B01 服务集中

**Status:** CORE  
**Allowed Host:** Block

Rules：

1. 邻接、同类 Service Building 的 Capacity 可以组成共享 Capacity Pool。
2. 共享池内部的 Service Access Strength × **1.25**。
3. 离开服务集群后的 Access 衰减 × **1.30**。

核心变化：

> 服务更集中、更强，但外围覆盖更差。

---

## B02 市政暖气

**Status:** CORE  
**Allowed Host:** Block

Rules：

1. `[Residential]` / `[Service]` Building 读取所在 Tile 的 Heat Field。
2. 每个标准化 Heat 单位提供 Building Efficiency 增益。
3. Efficiency Bonus 上限 **+30%**。

核心变化：

> Heat 从副产物变成城市服务资源。

---

## B03 市政管廊

**Status:** EXP  
**Allowed Host:** Block

Rules：

1. 与 `[Road]` 网络连接的 Building Maintenance × **1.30**。
2. 这些 Building 对 Pollution 的负面解释 × **0.50**。
3. Heat Field 可以沿 `[Road]` 网络额外传播。

核心变化：

> 用高维护费换 Field 的网络化传播与环境缓冲。

说明：

- 需要 0.2.0 至少有轻量 Road Connectivity；
- 若道路仍只是 TileTrait，可延后实现。

---

## B04 地下快速路

**Status:** EXP  
**Allowed Host:** Block

Rules：

1. Employment Access 在 `[Road]` 网络上的衰减降低 **40%**。
2. Service Access 在 `[Road]` 网络上的衰减降低 **40%**。
3. Block Infrastructure Maintenance 增加。

核心变化：

> 不提高岗位或服务 Capacity，只改变“城市有多大”。

---

## B05 战略储备

**Status:** CORE  
**Allowed Host:** Block

Rules：

1. 公共仓库 Storage Capacity × **2.00**。
2. 当某 Resource 出现 Shortage 时：
   - `[Residential]`
   - `[PublicService]`
   的需求获得更高 Allocation Priority。

核心变化：

> 仓储从“多存一点”变成短缺时的分配秩序。

---

## B06 用爱发电

**Status:** CORE  
**Allowed Host:** Block

Rules：

1. Happiness 高于阈值的 Population 才参与本 Rule。
2. 每单位符合条件的 Population 产生少量 Energy Supply。
3. Happiness 未过阈值时不产生 Energy。

核心变化：

> 人口状态直接成为能源系统的 Source，并形成潜在正反馈。

---

## B07 联合采购

**Status:** CORE  
**Allowed Host:** Block / Organization

Rules：

1. Same Operator Building 对同一 Resource 的 Demand 先聚合为一个采购组。
2. 采购组参与外部 Resource Allocation。
3. 获得资源后，再在组织内部按正常距离 / Priority 分配。

核心变化：

> 改变“谁作为一个需求主体参与资源竞争”。

---


## B08 污染产业园

**Status:** CORE  
**Allowed Host:** Block

Rules：

1. `[Industrial]` Building 位于高 Pollution Tile 时，Maintenance 降低。
2. Pollution 越高，Maintenance 优惠越明显，但存在上限。
3. 获得该优惠的 `[Industrial]` Building，其 Pollution Field Strength 同时提高。

核心变化：

> 原本应当回避的高污染空间，开始成为工业集聚的优势区；玩家用更严重的局部外部性换取工业体系效率。

---

## B09 热电共生

**Status:** CORE  
**Allowed Host:** Building / Block  
**Building Slot:** Production  
**Selector:** `[Industrial]`

Rules：

1. `[Industrial]` Building 可以读取所在 Tile 的 Heat Field。
2. Heat 可抵扣该建筑的一部分 Energy Demand。
3. Heat 抵扣存在上限，不能把 Energy Demand 无限降到负值。

核心变化：

> Heat 不只服务住宅和公共服务，也可以重新进入工业体系，形成 `Industrial → Heat → Industrial` 的反馈链。

---

# 5. Organization Bonus

## G01 企业内部协同

**Status:** CORE  
**Allowed Host:** Organization

Rules：

1. Same Operator 且 Spatially Adjacent 的 `[Industrial]` Building：
2. 对已有的“工业邻接协同”效果 × **2.00**。

重要：

- 不改变物理邻接；
- 只修改企业内部协同 Rule 的解释。

核心变化：

> Organization 成为第二张关系图。

---

## G02 内部供应链

**Status:** CORE  
**Allowed Host:** Organization

Rules：

1. 当本组织 Building 请求 Resource 时：
2. Same Operator 的 Producer Source 优先参与分配。
3. 内部供应不足后，才回到普通 Block 市场。

核心变化：

> 不增加资源，只改变来源优先级。

---

# 6. Meta Bonus

## M01 模块化设计

**Status:** EXP / Rare  
**Allowed Host:** Building  
**Building Slot:** Operations

Rules：

1. Host 获得 **1 个 Flex Slot**。
2. Flex Slot 可以安装 Production 或 Logistics Bonus。
3. Maintenance × **1.25**。

核心变化：

> 直接改变后续构筑空间。

设计要求：

- 这类 Bonus 必须稀少；
- 不要用大量 “+1 Slot” 把有限 Slot 体系冲烂。

---

# 7. 暂不进入 Bonus Pool：更适合作为 Policy 的原型

下面这些名字保留，但 **v1.0 不放进 Bonus Pool**，因为它们更像 Block Policy。

## 择优而录

候选 Policy Slot：Labor / Employment

概念：

> 岗位短缺时优先分配给 Education / Personnel Efficiency 更高的人群；距离退居次级因素。

---

## 全城比价

候选 Policy Slot：Procurement

概念：

> 资源短缺时不再以最近 Source 为最高优先，而以供应能力 / 未来价格系统中的价格为主要排序。

备注：

- 当前没有完整 Price 系统；
- 0.2.0 若实现，只能先做“全城择优采购”，不能假装真的有比价。

---

## 供应商名单

候选 Policy Slot：Procurement

概念：

> 部分 Organization 获得 Approved Supplier 资格；短缺时名单内 Source 优先。

---

## 分散采购

候选 Policy Slot：Procurement

概念：

> 同一 Resource 的采购被分散到多个 Source，限制单一 Source 的供给份额。

---

## 耕者有其田

候选 Policy Slot：Land / Labor

概念：

> `[Agricultural]` Building 优先雇佣邻接住宅 Population；Food 在进入全 Block 流通前优先满足相关人口基础需求。

---

## 开放排污

候选 Policy Slot：Environment

概念：

- Pollution Range × 2
- Pollution Strength × 0.30

它改变污染的空间形状，而不是简单减少污染。

---

## 倾销税

候选 Policy Slot：Trade / Tax

概念：

> 外部 Block 输入 Goods 增加税收 / 降低 Allocation Priority。

**FUTURE：** 需要跨 Block 物流。

---


## 土地出让年金

候选 Policy Slot：Land / Tax

概念：

> 被企业 / 经营性 Building 占用的 Tile 成为持续财政税基；政府获得土地相关收入，但相关 Building 的经营效率下降。

核心变化：

> 把“占地”从单纯空间成本改写为财政来源，用企业效率换公共财政。

---

## 排污权交易 / 污染计源

候选 Policy Slot：Environment / Tax

概念：

> Pollution Field 结算时保留 Source Identity，不只读取聚合后的总污染；不同污染 Source 可以分别承担额度、费用或税收。

核心变化：

> 利用 Field 的 Source-aware 结构，把“谁造成污染”变成可被制度读取的经济关系。

---

## 税制组

候选 Policy Slot：Tax

- 人头税
- 生产税
- 交易税

它们改变“什么活动成为税基”。

---

# 8. 暂停区：有意思，但 0.2.0 暂时不做

## 大工业

- `[Industrial]` Footprint ×2
- Output ×1.5

**FUTURE：** 当前冻结多地块建筑。

---

## 空间整合

- 一格空间视作更高空间容量
- Maintenance ×2

**FUTURE：** 当前冻结多地块 / 空间容量玩法。

---

## O07 商业综合体

**Status:** CORE  
**Allowed Host:** Building / Block  
**Building Slot:** Operations  
**Selector:** `[Commercial]`

Rules：

1. `[Commercial]` Building 可以参与任意类型 Building 的邻接协同。
2. 由跨类型关系获得的邻接协同只按原效果的 **50%** 结算。

未来附加规则：

- Footprint +1。

核心变化：

> 商业成为空间结构里的“通用连接器”：它不完全复制别人的协同，但可以把原本彼此无关的建筑组团粘在一起。

说明：

- `Footprint +1` 继续冻结到多地块建筑恢复后再启用；
- 0.2.0 只实现“万能半邻接”部分。

---

## 山水联运

概念：

> Water / Road 网络之间允许资源或 Access 接力传播。

**FUTURE：** 更适合跨 Block / 完整物流网络阶段。

---

# 9. v1.0 的“反同质化”检查

本池刻意避免全部变成 “X +20%”。新增的污染产业园、热电共生、商业综合体进一步补足了空间价值反转、Field 回流和跨类型关系改写。

目前覆盖的规则动作包括：

- **Modify**：粗野制造、密集住宅
- **Convert**：研究型医院、生物质能、高级回收
- **Field Emit / Propagation**：热量管理、无人机站、市政暖气、市政管廊
- **Gate**：水基工厂、义务教师
- **Relate**：工厂家属区、企业内部协同、商业医疗
- **Redirect / Allocation**：中转仓、战略储备、联合采购、内部供应链
- **Interpret / Reclassify**：东郊记忆、开放大学
- **Slot Meta**：模块化设计
- **状态 → 新系统 Source**：用爱发电、义务教师
- **坏状态价值反转**：高级回收、热量管理

---

# 10. v1.0 组合观察点

这不是硬编码套装，只是首轮试玩时值得观察的自然组合。

### A. 高污染循环

粗野制造  
→ Pollution 增加  
→ 高级回收价值提高  
→ 热量管理又能把污染转成 Heat  
→ 市政暖气开始吃 Heat。

如果玩家自然走出这条链，而不是 UI 告诉他“凑套装”，说明系统在工作。

### B. 组织城市

工厂家属区  
+ 企业内部协同  
+ 内部供应链 / 联合采购

可能自然长出“同一 Operator 的住宅—工业—供应链”结构。

### C. 服务网络

研究型医院  
+ 开放大学  
+ 邻里中心  
+ 服务集中

可能从“单体服务建筑”长成服务组团与转播网络。

### D. 高密度都市

密集住宅  
+ 地下快速路  
+ 服务集中  
+ 无人机站

可能用强基础设施对冲高密度的人口脆弱性。

### E. 历史再利用

东郊记忆  
+ Same-Operator Rule  
+ 商业医疗 / 开放大学等跨职能 Bonus

可能形成不拆旧城、直接重解释旧建筑的玩法。

---


### F. 工业热循环的分叉

粗野制造  
→ Pollution / Output 增加  
→ 热量管理把部分副作用转成 Heat  
→ 市政暖气把 Heat 输送给民生  
或  
→ 热电共生把 Heat 重新送回工业降低 Energy Demand。

同一个 Heat 中间层因此可以导向不同城市形态，而不是固定套装。

### G. 污染集聚与治理

污染产业园  
+ 高级回收  
+ 排污权交易 / 污染计源

可能形成：

> 主动集中污染 → 从污染中获取资源 / 成本优势 → 再按 Source 对污染责任进行结算。

这类组合用于观察“坏状态能否被玩家主动组织，而不是单纯消灭”。


# 11. v1.1 再决定的东西

v1.0 暂时 **不定**：

- Bonus 稀有度；
- Offer 价格；
- Operator 对 Bonus 的具体权重；
- 互斥组；
- Block Bonus Slot；
- Bonus 获取次数；
- Same-Operator Synergy 的统一强度；
- 具体数值平衡；
- 哪些 EXP 项进入首轮 0.2.0。

下一步推荐：

> 先拿这 30 个 Bonus 去设计 6～8 个 Operator 的偏好池。  
> 再反过来看：是不是某些 Operator 过度绑定某条 Build、哪些 Bonus 太万能、哪些 Bonus 根本没人想拿。


# 12. Unfiltered Brainstorm Backlog（先全塞，暂不去重）

> 本节遵循“先收集、后去重”的原则。  
> 这里的条目 **不代表已通过设计审查**，也不代表一定属于 Bonus；有些后续可能被重分类为 Policy、Operator Rule、Building Trait、Meta Rule 或直接删除。  
> 现阶段目的只有一个：**不要丢想法。**

---

## R01 产业学院

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Production / Operations  
**Eligible Building:** `[Education]`

Rules 草案：

1. Education Capacity / Efficiency 降低。
2. Education Access 覆盖到的 Population，在进入 `[Industrial]` 岗位时 Personnel Efficiency 提高。
3. 可选：与 `[Industrial]` Building 邻接时，上述效果增强。

核心想法：

> 教育不直接强化所有劳动，而是定向改变“谁适合进入哪种岗位”。

---

## R02 社区夜校

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Logistics / Operations  
**Eligible Building:** `[Education]`

Rules 草案：

1. Education Access 衰减降低。
2. 可读取邻接 `[Commercial]` Building 的空闲 Service Capacity。
3. 被借用的商业 Capacity 转化为临时 Education Capacity。

核心想法：

> 教育借用商业空间 / 服务网络，而不是自己无限扩建。

---

## R03 学术园区

**Status:** RAW  
**候选 Host:** Building / Block  
**候选 Slot:** Operations  
**Selector:** `[Education]`

Rules 草案：

1. 邻接 Education Building 可形成共享协同。
2. 不同 Education Bonus / Trait 越多，园区协同越强。
3. 相同效果重复堆叠时收益递减。

核心想法：

> 鼓励“多样化教育集群”，而不是单纯堆同一种学校。

---

## R04 预防医学

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Production  
**Eligible Building:** `[Medical]`

Rules 草案：

1. Medical Treatment Capacity 降低。
2. Healthcare Access 覆盖 Population 的 Health 恶化速度降低。
3. 对已经处于低 Health 的 Population 治疗能力不增强。

核心想法：

> 从“事后治疗”改为“减少问题发生”。

---

## R05 社区诊所

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Logistics  
**Eligible Building:** `[Medical]`

Rules 草案：

1. Medical Capacity 降低。
2. Healthcare Access 衰减显著降低。
3. 邻接 `[Residential]` 的 Population 获得更高服务分配优先级。

核心想法：

> 低吞吐、高覆盖、社区优先。

---

## R06 医疗产业园

**Status:** RAW  
**候选 Host:** Building / Block  
**候选 Slot:** Operations  
**Selector:** `[Medical]` / `[Industrial]`

Rules 草案：

1. `[Medical]` 与特定 `[Industrial]` Building 可以产生邻接协同。
2. Medical / Industrial 之间可共享部分 Service / Resource Allocation Priority。
3. 具体效果以后依赖制药、器械等 Trait 是否进入内容层。

核心想法：

> 把医疗服务与产业制造接成一条关系链。

---

## R07 工厂直营

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Operations / Logistics  
**Eligible Building:** `[Industrial]`

Rules 草案：

1. `[Industrial]` Building 获得少量 Commercial Capacity。
2. Produced Goods 可以优先用于该 Commercial Capacity。
3. Local Storage Capacity 降低或周转要求提高。

核心想法：

> 工业直接触达消费端，绕过一部分商业节点。

---

## R08 社区商业

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Logistics  
**Eligible Building:** `[Commercial]`

Rules 草案：

1. Service Access Range 缩短。
2. 邻接 `[Residential]` 时 Service Efficiency / Capacity 提高。
3. 对远距离 Population 的服务优先级降低。

核心想法：

> 与电商仓储店形成相反方向：更近、更小、更依赖社区。

---

## R09 批发市场

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Logistics / Operations  
**Eligible Building:** `[Commercial]`

Rules 草案：

1. 不再以 Population Demand 作为主要服务目标。
2. 邻近 Producer / Consumer Building 的 Resource Allocation Priority 提高。
3. 可形成局部 Resource Distribution Hub。

核心想法：

> 商业从“服务居民”变成“服务生产和流通”。

---

## R10 都市农业

**Status:** RAW  
**候选 Host:** Building / Block  
**候选 Slot:** Production

Rules 草案：

1. `[Residential]` / `[Commercial]` Building 可获得少量 Food Production。
2. Maintenance 增加。
3. Food Production 受建筑本身 Efficiency 影响。

核心想法：

> 把农业能力分散进非农业建筑。

---

## R11 农业循环

**Status:** RAW  
**候选 Host:** Building / Block  
**候选 Slot:** Production  
**Eligible Building:** `[Agricultural]`

Rules 草案：

1. 农业可读取特定 Field（如低 Pollution / 净化 / Water）。
2. Agriculture Production 可产生改善 Tile 状态的 Field。
3. 可选：部分 Waste / Pollution 被转化为农业输入。

核心想法：

> 农业既是生产者，也是环境状态的处理器。

---

## R12 农业合作社

**Status:** RAW  
**候选 Host:** Organization / Building  
**候选 Slot:** Operations  
**Selector:** `[Agricultural]`

Rules 草案：

1. Same Operator 的农业 Building 获得内部 Storage / Resource Allocation 协同。
2. Food Shortage 时，同组织内部优先分配。
3. 可选：相邻农业 Building 共享部分 Capacity。

核心想法：

> 用 Organization 关系组织农业，而不是单体强化。

---

## R13 自给农庄

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Production / Operations  
**Eligible Building:** `[Agricultural]`

Rules 草案：

1. 本建筑生产的 Food 优先满足 Hosted Population。
2. 剩余 Food 才进入 Block Resource 流通。
3. 可选：对外供给效率下降。

核心想法：

> 改变 Food 的流向优先级。

---

## R14 农家乐

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Operations  
**Eligible Building:** `[Agricultural]`

Rules 草案：

1. 获得少量 Entertainment Capacity。
2. Food Output 降低。
3. 对邻近 Population / Commercial 产生额外服务关系。

核心想法：

> 农业的一部分生产能力转成娱乐 / 服务。

---

## R15 精耕小农

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Production  
**Eligible Building:** `[Agricultural]`

Rules 草案：

1. 基础产出潜力提高。
2. 对 Personnel Efficiency / Education 的要求提高。
3. Labor Satisfaction 不足时，产出下降更陡。

核心想法：

> 高技能、小规模农业，对人口质量更敏感。

---

## R16 家庭作坊

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Production  
**Eligible Building:** `[Residential]`

Rules 草案：

1. Hosted Population 可消耗少量 Material。
2. 产生少量 Goods。
3. Pollution / Maintenance / Happiness 产生对应代价。

核心想法：

> 住宅成为微型生产单元。

---

## R17 职住一体

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Operations  
**Eligible Building:** `[Residential]`

Rules 草案：

1. 若 Hosted Population 的 Job Building 与本住宅邻接，则 Employment Access 视为更高。
2. Housing Capacity 略降。
3. 可选：相关 Population 的 Personnel Efficiency / Happiness 获得小幅收益。

核心想法：

> 用住宅容量换短通勤关系。

---

## R18 公共厨房

**Status:** RAW  
**候选 Host:** Building / Block  
**候选 Slot:** Operations / Logistics  
**Selector:** `[Residential]`

Rules 草案：

1. 邻接住宅可共同结算一部分 Food Consumption。
2. Food Consumption 效率提高。
3. Maintenance 增加。
4. 若公共厨房 Capacity 不足，额外效率失效。

核心想法：

> 多个住宅通过公共设施共享消费侧规模效应。

---

## R19 副产品经济

**Status:** RAW / META CANDIDATE  
**候选 Host:** Building / Block  
**候选 Slot:** Production

Rules 草案：

1. 某类生产在主 Output 之外额外产生一个 Resource 或 Field。
2. 副产物不自动拥有价值，需要其他 Rule / Bonus 读取。

示例：

> Goods Production → 同时 Emit Heat  
> Food Production → 同时产生 Biomass Waste

核心想法：

> “生产 A 时顺便产生 B”作为一个通用构筑语法。

备注：

- 很可能最终不是一张 Bonus，而是一类 Bonus 模板；
- 先保留，避免把这个设计方向忘掉。

---

## R20 污染资源化

**Status:** RAW  
**候选 Host:** Block / Building  
**候选 Slot:** Production

Rules 草案：

1. Pollution 高于阈值时，可以被某类 Building 当作输入读取。
2. 读取 Pollution 后产生 Material / Energy / Goods 中的一种。
3. 必须真实消耗 Pollution，不能只读数值。

核心想法：

> 高级回收的更泛化版本；可能最终与“高级回收”合并。

---

## R21 服务外包

**Status:** RAW  
**候选 Host:** Organization / Block  
**候选 Slot:** Operations

Rules 草案：

1. 某类 Service Capacity 可以由 Same Operator 的其他 Building 代为提供。
2. Access Field 仍由实际服务 Source 传播。
3. 外包会产生额外 Maintenance 或效率损耗。

核心想法：

> 把“谁拥有建筑”和“谁提供服务”拆开。

---

## R22 共享仓储

**Status:** RAW  
**候选 Host:** Organization / Block  
**候选 Slot:** Logistics

Rules 草案：

1. Same Operator 的 Building 可以把部分 Local Storage 视为共享池。
2. Resource Allocation 优先从共享池中匹配。
3. 共享不改变物理库存归属，只改变可访问性。

核心想法：

> 用组织关系改写库存的可用边界。

---

## R23 专业化采购

**Status:** RAW  
**候选 Host:** Organization / Block  
**候选 Slot:** Logistics

Rules 草案：

1. 对指定 ResourceType，采购 Allocation Priority 提高。
2. 对其他 ResourceType，Priority 降低或无加成。
3. 可与“联合采购 / 分散采购”发生潜在互斥或叠加。

核心想法：

> 不是“采购更强”，而是采购能力定向偏科。

---

## R24 服务共址

**Status:** RAW  
**候选 Host:** Building / Block  
**候选 Slot:** Operations

Rules 草案：

1. 两种不同 Service Building 邻接时，可共享部分 Access Field。
2. Capacity 不直接相加。
3. 共址后的 Access 传播效率提高。

核心想法：

> 服务设施通过空间共址形成“一个地点，多种服务”的节点。

---

## R25 工业余热直供

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Logistics  
**Eligible Building:** `[Industrial]`

Rules 草案：

1. Host 的 Heat Field 优先 Redirect 给邻接 `[Residential]` / `[Service]` Building。
2. 被直供的目标获得更高 Heat 利用效率。
3. 远距离 Heat 传播被削弱。

核心想法：

> 与市政暖气相反：不做全区网络，而做强局部耦合。

---

## R26 产业共生

**Status:** RAW  
**候选 Host:** Block / Organization  
**候选 Slot:** Operations

Rules 草案：

1. 一个 Building 的副产物 Resource / Field，可以被另一个符合 Trait 的 Building 优先读取。
2. Same Operator 时，分配优先级进一步提高。
3. 不新增副产物，只改变其匹配路径。

核心想法：

> 让“副产品经济”真正形成多建筑链条。

---

## R27 自营改造

**Status:** RAW  
**候选 Host:** Organization  
**候选 Slot:** N/A

Rules 草案：

1. 当 Bonus Provider 与 Building Operator 相同：
2. 再开发 / 重解释类 Bonus 的成本下降。
3. 原建筑部分能力允许被保留。

核心想法：

> 把“东郊记忆 + 城投”从专属彩蛋抽象成通用 Operator Rule。

---

## R28 多用途许可

**Status:** RAW  
**候选 Host:** Building / Block  
**候选 Slot:** Operations / Meta

Rules 草案：

1. 一个指定 Slot 可以视作另一类 Slot。
2. 或允许某张跨类型 Bonus 安装到原本不允许的 Slot。
3. Maintenance / Efficiency 付出代价。

核心想法：

> Slot 本身进入 Rule 语言。

---

## R29 复合方案

**Status:** RAW / RARE  
**候选 Host:** Building  
**候选 Slot:** Meta

Rules 草案：

1. 某一个 Slot 可以同时承载两张互不冲突的低阶 Bonus。
2. 两张 Bonus 的数值效果按一定比例衰减。
3. 两者的非数值 Rule 仍需逐条检查冲突。

核心想法：

> 用“压缩”换构筑密度。

---

## R30 专项改造

**Status:** RAW  
**候选 Host:** Building  
**候选 Slot:** Meta

Rules 草案：

1. 移除一个指定 Slot。
2. 新增一个不同类型 Slot。
3. 变更永久跟随该 Building，除非特殊规则再次改造。

核心想法：

> 建筑的 Slot 结构本身可以成为历史。

---

# 13. 这版先不做任何去重

目前明确允许以下情况同时存在：

- “高级回收”与“污染资源化”可能高度重叠；
- “农业合作社”与“联合采购 / 内部供应链”可能重叠；
- “社区诊所”与“服务集中 / 邻里中心”可能部分重叠；
- “都市农业 / 家庭农场 / 家庭作坊”都在做跨职能；
- “模块化设计 / 多用途许可 / 复合方案 / 专项改造”都在改 Slot；
- “副产品经济”可能最终只是模板，而不是实际 Bonus。

**现在不删。**

下一轮再专门做：

> 1. 完全重复  
> 2. 同语法不同体验  
> 3. 应该合并  
> 4. 应该互斥  
> 5. 应该从 Bonus 改成 Policy / Operator Rule / Trait  
> 6. 应该直接淘汰

这才是 v1.1 的工作。
