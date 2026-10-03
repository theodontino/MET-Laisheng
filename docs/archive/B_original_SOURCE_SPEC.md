> 历史档案：B 0.1.0-exp003 原文，已被 B 0.1.1 人口版决策覆盖。不得作为本次测试结果或当前玩法规格。

# 实施依据快照

来源：MET_Gameplay_Context_Handoff_2026-09-22_structured.md，第16章。以下原文不静默修改；实现默认值在其他文档列出。

# 16. EXP-003 / MVP-1 — First Block Vertical Slice Specification

> **定位：** 第一版不按“系统模块”切，而按**玩家真实时间轴**切。目标是完整模拟从 New Game 到首个 Block 建成的全过程。
>
> **范围原则：** 内容可以很少，机制不能在玩家已经接触到的阶段缺席；玩家此时尚未自然接触到的后期机制，不提前实现。
>
> **核心验证：** Rogue 选择是否真的通过城市建设动作物化为一个具有本局历史、空间结构与 Build 身份的 Block；以及玩家是否会在通关目标达成后仍自发继续玩。

## 16.1 MVP 决策账（MVP-01 ~ MVP-28）

### MVP-01 — 验证范围：从 New Game 到首个 Block 建成

**状态：DECIDED / E1**

- 第一 MVP 只真正开发 **1 个 Block**；
- 不是单机制 Lab，而是一个完整 First Block Vertical Slice；
- 机制按玩家首次接触顺序出现；
- 内容规模可以非常薄，但玩家在这一阶段已经接触到的核心机制必须闭环；
- 第二 Block、跨 Block Buffer、District Overdrive、自治外交等后期机制不为“完整感”强行提前做。

### MVP-02 — 地图：固定 District + 可选首个 Block + 薄随机

**状态：DECIDED / E1**

- 使用一张固定、手工可控的 District 地图；
- 地图中存在少量候选 Block，玩家决定从哪一块开始；
- 可加入极薄的机械随机生成：Opening 条件、隐藏局部条件、Origin 候选等；
- 不在 MVP-1 证明程序化地形生成能力。

### MVP-03 — Opening / Survey：有限公开信息 + 薄揭示

**状态：DECIDED / E1**

- 玩家选 Block 前能看到肉眼可知的重要地理、边界与明显条件；
- MVP 不实现复杂独立 Survey 系统；
- 深层条件可以在选定 Block 前后、与 Opening 同阶段薄揭示；
- 目标是让玩家在**不是盲赌、也不是全知 Wiki**的情况下做第一次判断。

### MVP-04 — Origin：暗抽候选，明选其一

**状态：DECIDED / E1**

- 系统根据 Block 条件从小型 Origin 池中**暗抽若干候选**；
- 玩家看到候选并主动选择其一；
- Origin 可以发生在 Survey 之前或与 Opening Reveal 同阶段；
- Origin 决定起始家底、历史起点与部分 Build 语言，但不锁死终局。

### MVP-05 — Development Draw = 区域招投标

**状态：DECIDED / E1**

玩家先表达城市建设意图，社会 / 外部组织再提交方案：

1. 玩家划定功能区域；
2. 选择区域类型；
3. 从可用 Policy Tag 池中选择 1~2 个 Tag；
4. 后台生成三份 Proposal：
   - 一份使用**交集偏置权重**；
   - 一份使用**并集偏置权重**；
   - 一份使用**上下文随机权重**；
5. 三份 Proposal 打乱展示，不告诉玩家哪张来自交集 / 并集 / 随机槽；
6. 交集 / 并集都只是权重算法，不是硬筛选，玩家能表达意图但不能预测下一手牌。

### MVP-06 — Policy Tag 池来源

**状态：DECIDED / E1**

可用于招标偏好的 Tag 来自：

- 一小组**基础政策 Tag**；
- **科技 / 制度解锁 Tag**；
- 事件、奖励、抽取中获得的新 Tag，玩家可选择将其纳入自己的可用池。

MVP 控制可见复杂度：

- 基础池约 6 个量级；
- 首个 Block 全流程只新增少量 Tag；
- 不把 Tag 菜单做成关键词搜索数据库。

### MVP-07 — 区域类型采用功能型四分类

**状态：DECIDED / E1**

MVP 先固定四类：

- **定居**
- **生产**
- **流通**
- **公共**

区域类型回答“这里主要拿来做什么”；Policy Tag 回答“希望它以什么方式发展”。

### MVP-08 — Proposal 落地 = Development Project

**状态：DECIDED / E1**

- 玩家中标后投入 Wild 通货并开始施工；
- 施工持续消耗 / 占用资源供给，供给不足则施工减缓或停滞；
- 外部供货未来可缓解，但受物流能力约束；
- 一个 Proposal 在机械上对应一个**主体 Development Project**；
- 美术 / 叙事上可以自然包含厂房、院场、小仓库、内部道路、变电设施、货车站等附属内容，但这些不拆成玩家微操对象。

### MVP-09 — 道路与附属设施默认存在，不做基础设施税

**状态：DECIDED / E1**

- 道路默认随城市开发形成；
- 不设置专门“道路地块”，不做堵车模拟器；
- 不要求玩家亲手铺下水管、厂内变电站、小仓库等常识性配套；
- 真正有玩法价值的道路强化未来可以表现为道路插槽、Block 基建升级或覆盖多建筑的区域政策；
- 机械上的进一步成长主要通过 Upgrade，而不是附属建筑微操。

### MVP-10 — Development Draw 可自由主动发起

**状态：DECIDED / E1**

- 有土地、有资源、符合条件时，玩家可以主动发起 Development Tender；
- 不增加“抽卡券 / 抽卡体力 / 抽卡冷却”；
- 真正约束来自土地、通货、建设资源与后续运营能力；
- Reroll 才需要额外成本。

### MVP-11 — 资源 = Stock + Flow

**状态：DECIDED / E1**

资源不是纯钱包，也不是纯市场吞吐：

- 可储存资源具有真实 Stock；
- 生产 / 外部供给决定补充速度；
- 建设与运营从库存中持续取用；
- Material、Goods 等耐储资源可以积累；
- Food 可使用较小 Buffer / 易腐差异；
- Energy 默认更接近实时 Capacity，专业储能以后再展开；
- 每个 Block 自带基础隐式储存，不要求玩家建设必修仓库。

### MVP-12 — 资源 UI 按玩家首次需要逐步显露

**状态：DECIDED / E1**

- Food / Material / Energy / Goods 底层可以从一开始存在；
- UI 不在第一秒把所有变量拍到玩家脸上；
- 某种资源第一次真正产生决策价值时，再提高其可见度与教学权重；
- 原则：**没有正在制造决策的变量，就不要占玩家脑子。**

### MVP-13 — 起始家底 = 统一安全底线 + Origin 差异

**状态：DECIDED / E1**

- 所有 Origin 共享一层足以避免前三分钟坐牢的安全底线；
- Origin 再提供明显不同的库存、人口、Tag、通货或初始能力；
- 这同时作为很干净的难度调节手段：难度主要调安全垫厚度，而不是简单把数字乘倍数。

### MVP-14 — 空间：自由图区 + 1~7 Hex 功能区

**状态：DECIDED / E1**

空间操作分两层：

1. **Zoning / 图区**：玩家自由涂 `定居 / 生产 / 流通 / 公共`；
2. **Development Site / 功能区**：真正发起项目时，从兼容图区中选取最多 **7 个相邻 Hex** 组成项目场地。

地形、形状、邻接可以影响 Proposal 与后续 Build。Site Trait / Perk 可做很薄的实验，但不让玩家再手动填一套 Perk 菜单。

### MVP-15 — 功能区内部机械结构

**状态：EXPERIMENT / OPEN**

尚不锁死：

- 一个 Proposal 是否机械上独占整个功能区；
- 功能区是否作为未来可继续容纳机械内容的容器。

MVP 实现时优先选择最低成本版本，并专门观察：

- 独占模型是否太死；
- 容器模型是否重新引入微操；
- 哪种更有“塑造片区”的感觉。

### MVP-16 — 施工过程允许少量 Construction Opportunity

**状态：DECIDED / E1**

- 普通项目施工期间最多触发少量、MVP 通常最多 1 次 Opportunity；
- Opportunity 来自项目、地形、资源状况、周边关系等上下文；
- 玩家可以选择改变项目历史 / Tag / 关系，也应始终存在“按原计划继续”的低摩擦选项；
- 施工不是另一套项目管理小游戏。

### MVP-17 — 成长结构：状态驱动 Upgrade + Milestone + Fusion Slot

**状态：DECIDED / E1**

项目成长分两条轴：

- **自身成长：里程碑式 Upgrade Draw**；
- **关系成长：邻接建筑之间的 Fusion Slot**。

Upgrade Pool 由项目真实历史加权：运行状态、Tag、邻接、瓶颈、Construction Opportunity、既有升级等都会改变下一次什么更值得出现。

Fusion 不把两栋建筑合成一栋；它属于**两栋建筑之间的关系槽**，成熟后抽取一个能真正改变双方交互规则的机制。

### MVP-18 — Fusion 必须经历关系成熟

**状态：DECIDED / E1**

- 邻接只是“认识”；
- Tag 模板兼容意味着“有资格建立关系”；
- 长期共同运行才积累 Relationship Maturity；
- 达到阈值后触发 Fusion Draw；
- 不做“摆一下就领奖”的即时融合。

### MVP-19 — Fusion 关系不追踪真实资源来源

**状态：DECIDED / E1**

MVP 使用低成本代理规则：

> **邻接 + Tag 模板兼容 + 双方有效运行 → 随 Local Time 累积关系。**

- 不新增建筑间有向资源流图；
- 不追踪“这 3 单位 Material 到底由谁供给”；
- Block 内仍保持自动撮合 / 低摩擦资源逻辑。

### MVP-20 — 里程碑 = 模板条件 + 局面特色条件

**状态：DECIDED / E1**

- 每类项目先使用少数共通里程碑模板；
- 再从当前已有系统中生成一个局面相关的特色条件；
- MVP 每个项目最多做 2 个主要升级里程碑；
- 如果试玩证明过于机械，再把高价值建筑逐步升级成手写特色里程碑，而不是第一版就炸内容成本。

### MVP-21 — Upgrade 使用软分叉

**状态：DECIDED / E1**

- 一次升级选择会显著改变后续 Upgrade Pool、Tag、资格与路线权重；
- 不要求永久封死所有其他方向；
- 想回头需要新的 Tag、Fusion、特殊机会、更高里程碑或更昂贵 Redevelopment；
- **选择留下历史，但不是终身合同。**

### MVP-22 — Upgrade Draw 允许 1 个 Policy Tag 软偏好

**状态：DECIDED / E1**

- Upgrade Pool 主要由项目历史与当前状态决定；
- 玩家可额外指定 **1 个 Policy Tag** 作为软偏好；
- 只改变权重，不保证结果；
- Development Draw 是“主动规划这里长什么”，Upgrade Draw 更像“它已经活成这样，我再推一个方向”。

### MVP-23 — Reroll：先烧钱，连续洗牌后开始烧 DT

**状态：DECIDED / E1**

- 前几次 Reroll 只消耗递增 Wild 通货；
- 连续 Reroll 到后段后，开始额外消耗 DT；
- 目的是防止后期通货宽裕后无限洗牌，同时不在第一次不满意时就抽走核心时间资源；
- “锁一张只洗其余 Proposal”保留为可选实验，不是 MVP 必须项。

### MVP-24 — MVP-1 时间范围只做单 Block 闭环

**状态：DECIDED / E1**

第一版只实现：

- 游戏运行时 Block Local Time 正常推进；
- 退出游戏后不做复杂城市 catch-up；
- 现实离线时长按较低效率、受上限约束地恢复 DT；
- DT 可一次性推进当前 Block Local Time；
- Temporal Load 只做很轻的规模成本；
- Stock / 施工 / 运营会真实消耗被推进的时间与资源。

第一 MVP 不做：

- 跨 Block 时间同步；
- 跨 Block Buffer；
- District Overdrive；
- 全产业链同步加速。

### MVP-25 — Mandate / Support：薄，但真的能咬人

**状态：DECIDED / E1**

- 第一 Block 阶段就显示 District 级 Mandate / Support；
- 初始安全垫较厚，正常玩家不容易无故暴毙；
- MVP 只做极少来源：例如 1 个 Hope City 主任务、少量居民请求、严重失信 / 明显经营失败 / 高风险事件失败；
- 如果玩家故意或严重经营失败，真的可以触发一次简化 Governance Crisis；
- MVP 不实现自治外交；被罢免后本次试玩直接结束，并保留最终城市状态作为结算证据。

### MVP-26 — 首个 Block 完成 = 硬 Checklist + Infinite Mode

**状态：DECIDED / E1**

- MVP 使用明确硬 Checklist 作为“首个 Block 已打通”的实验判据；
- Checklist 可暂包含：一定数量主体项目、基础供给未持续崩溃、至少一次 Milestone Upgrade、至少一次成熟 Fusion、治理状态未处于 Crisis 等；精确数值由实现阶段调；
- 达成后给轻量奖状 / 完成记录；
- **不锁地图、不强制结束、不提高惩罚。**
- 玩家可以继续 Infinite Mode 自由建设。

Infinite Mode 本身是核心测试：

> **当试玩目标已经完成，玩家还会不会主动说“等下，我把这个 Fusion 做出来再关”？**

### MVP-27 — 内容预算：小而完整

**状态：DECIDED / E1**

首版内容量级：

- 4 个 Origin；
- 16~20 个 Development Proposal；
- 10~12 个 Upgrade Proposal；
- 3~4 个 Fusion 模板；
- 5~6 个 Construction Opportunity；
- 1 个 Hope City 主任务 + 2 个居民请求；
- 1 条 Governance Crisis 小事件链；
- 约 6 个基础 Policy Tag + 约 3 个可获得 Tag；
- 少量 Perk / 特殊奖励。

内容设计必须覆盖不同机械角色，不做 20 张“同结构换数字”的卡。

### MVP-28 — Playtest Pass / Fail Gate

**状态：DECIDED / E1**

首版验收不以“系统都能运行”作为成功，而看玩家心理现象：

1. **Draw Desire**：会不会因为当前城市状态而产生“我想再抽一次看看”；
2. **City Impact**：至少一次 Proposal 真正改变空间 / Build 计划；
3. **Reverse Value**：至少一次既有城市状态让原本普通的 Proposal 突然变得很好；
4. **Spatial Meaning**：换位置会不会明显改变选择；如果放哪都一样，空间系统失败；
5. **Attachment / Growth**：玩家会不会主动关注某项目的下一里程碑或 Fusion；
6. **Good Pressure**：DT、资源、Mandate / Support、Reroll 成本等是否让玩家改变决策，而不是只觉得被阻止；
7. **Infinite Pull**：完成 Checklist 后是否仍自发继续玩；
8. **Kill Question**：如果没有 Life、没有效率工具、没有长期 Meta，玩家是否仍愿意单纯为了这个 Block 再开一局？

若长期答案是否定的，不允许用“未来系统还没做”无限解释。

## 16.2 MVP-1 当前最小玩家流程

```text
New Game
↓
进入固定 District 地图
↓
观察少量候选 Block
↓
选择首个 Block
↓
Opening Reveal / 薄 Survey + 暗抽 Origin 候选
↓
玩家选择 Origin
↓
获得统一安全底线 + Origin 差异家底
↓
自由涂图区：定居 / 生产 / 流通 / 公共
↓
在图区内圈 1~7 个相邻 Hex 形成功能区
↓
选择 1~2 个 Policy Tag
↓
后台：交集偏置 / 并集偏置 / 上下文随机 → 洗牌
↓
展示 3 份 Proposal
↓
选择中标项目 / 必要时 Reroll
↓
投入通货 + Stock / Capacity 开始施工
↓
可能触发 0~1 次 Construction Opportunity
↓
项目投产并真实运行
↓
状态 / Tag / 邻接 / 历史改变下一轮 Proposal 与 Upgrade Pool
↓
里程碑 → Upgrade Draw（可指定 1 个软偏好 Tag）
↓
兼容邻接 + 双方有效运行 → Relationship Maturity
↓
Fusion Draw
↓
Hope City / 居民请求 / Mandate / Support 形成薄治理压力
↓
反复建设、抽取、升级、融合
↓
达成 First Block Checklist
↓
获得奖状 / 主流程完成
↓
Infinite Mode：继续玩，或结束试玩
```

## 16.3 MVP-1 明确不做

为了防止范围重新爆炸，首版明确不做：

- Life 模式；
- 第二 Block 的正式开发；
- 跨 Block 资源 Buffer / 供应链同步；
- District Overdrive；
- Autonomous District 外交 / 贸易 / 投资系统；
- 完整 Handover / 回任；
- 程序化地形生成器；
- 细粒度道路 / 堵车模拟；
- 手动下水、管网、厂内配套建设；
- 建筑间真实资源 provenance / 有向物流图；
- 大规模仓储物流微操；
- 复杂食物腐败；
- 每栋建筑手写一套里程碑；
- 大量 Unique Fusion；
- 强制预制套装；
- 大型 Meta Progression。

## 16.4 当前实施原则

1. **按玩家进程切功能，不按系统图切功能。**
2. **机制要闭环，内容可以薄。**
3. **默认实现现实常识，只暴露会产生新决策的部分。**
4. **抽卡是机会呈现，不是世界本体。**
5. **卡牌优先改变机制 / 关系 /空间 / 权重，而不是纯数值。**
6. **城市状态必须改变下一抽的价值；否则不是 Build。**
7. **不为 MVP 新增细粒度物流、道路、管网等低收益模拟。**
8. **任何新增机制先问：它是否让 First Block 更好玩，还是只让系统更完整。**

---
