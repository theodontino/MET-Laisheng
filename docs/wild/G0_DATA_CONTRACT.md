# MET Wild v0.1 · G0 建筑构筑数据合同草案

日期：2026-10-26。状态：**仅合同草案，非已实现 schema，非 G1 玩法定稿**。当前真实字段和命令见 [引擎审计](G0_ENGINE_AUDIT.md)。本合同不写入 Wild 运行时字段、不新建 Wild 存档、不生成假节点或假经济结果。G0 可选 Toolkit 窗口只连接现有真实升级命令；当前阶段没有实现下列未来构筑 schema。

## 1. 已确认方向与尚未确认的细节

- 设计容量属于每栋建筑，并受全局最大设计容量约束；不是全城共享的待消费点池，也不是施工 CP 或产能。
- 保留 Tier 谱系；高级形态取得后可涉及直建路径，但“取得”时点待 Q5。直建不能假造这栋楼真实经历过的升级历史。
- 未来无消耗型升级许可；不因此把当前 Bonus 副本消费删除或解释为已决定的新 Bonus 经济。
- 未来不采用运营者；组织/联系/融合 Bonus 的分类与槽型映射待定。
- 模板配额限制“已保存的设计方案”数量，不能变成建筑数量、部署次数或消耗许可证限制。配额数值、归属范围与取得来源未在 G0 定案。

## 2. 单一数据真相及字段映射

下面 snake_case 是讨论用字段名，并未预留到 V 状态。已有字段继续由已有 owner 持有；新增字段只在后续授权时选择一个 owner。UI 只建立投影，不保存第二份可写能力/钱包/节点。

| 草案概念 / 字段 | 当前权威来源 | 后续处理 / 未决事项 |
| --- | --- | --- |
| building_id、tile、site | Game.s.projects[id] 的 id/tile/site 与 cells/sites 引用；Game.create_building | 复用实体身份；升级不换 ID。模板不能持有地块占用或 live inventory。 |
| current_form_id | projects[id].def；node_id 当前等于定义节点 | 复用 def 作为实际形态入口；不再新增一个独立可写 current_form。若 node_id 改义，须明确迁移与唯一 owner。 |
| family / current_tier | 实例 family_id/tier 及 Catalog 定义 | 保留谱系身份；定义与实例一致性继续由命令维持。谱系节点与“当前能力等级”不应混同。 |
| tier_lineage | 当前 history 仅有 from/to/h 事件；upgrades 为目录边 | TODO：真正保留可撤销的 Tier 节点及其选择。不能把旧 history 自动充当完整可撤销状态，也不能把模板的计划谱系冒充真实历史。Q1 决定撤销后依赖如何处理。 |
| design_capacity | 无对应可用权威字段；size_scale 不是 DP | TODO：每建筑容量的一个权威来源；受 max_design_capacity 约束。继承/增长规则 Q2；已用 DP 从合法构筑推导，不再另存任意可改余额。 |
| max_design_capacity | 无 Wild 全局上限；block.construction_capacity 是 CP/月 | TODO：在已明确的全局 owner 计算/保存上限来源，投影给各建筑；时代增长、旧建筑继承和超限处理 Q2。G0 不设数值。 |
| construct_nodes / choices | Catalog.upgrades 是整栋建筑升级边；不是自由节点表 | TODO：节点 ID、前置、Tier 归属、效果引用、选择/撤销关系。节点效果最终进入既有 Rules.compile 一条链；不把按 UI 估算的 stats 存成第二份真相。 |
| slots / slot_types | projects[id].slots/slot_types；当前 slot_track 来自 operator_org | 现行引用结构可调查复用；未来槽轨道来源要改，不能继续隐藏依赖运营者。槽成本 Q3、类别映射 Q4。已安装 Bonus 仍指向真实 instance ID。 |
| bonus_instances | Game.s.bonus_instances 的 bonus_id/host_type/host_id/slot/active/provider_org 等 | 当前记录是真实 Beta。未来无运营者时需重新确定规则来源及历史表达；不直接删 provider/contract 字段或伪造组织。组织/联系/融合语义 Q4。 |
| costs / work | Catalog.cost/material/construction_required；job/upgrade_job 实付施工状态 | 可复用实付通货和按进度材料/CP 结算框架；节点成本与槽位指数成本待设计。DP 与资源分别有单位，不能使用一个 cost 数字代替。Q1 退款、Q3 指数范围未定。 |
| effective_stats / rule_trace | Rules.compile 的 stats/config/trace，经 Sim 产生 metrics/audit | 全部为派生结果/预测缓存。普通建筑目前优先读 Catalog stats；后续新增构筑效果必须接入此处，不能仅填 base_stats 期望生效。 |
| obtained_forms / direct_build_eligibility | 当前不存在；Draft.open_build 限 progression.tier1 | TODO：在一个适当全局 owner 记取得事实/来源；Q5 先决定时点，再设计资格。不能由 UI 点选或预览写入。资格不是耗一次的升级许可。 |
| saved_designs | 当前 BlueprintDesk 的 bonus_wallet/blueprint_levels 是 Bonus，不是设计库 | TODO：单一模板集合，保存定义 ID、选择和可复现设计所需版本/引用；不复制运行库存、usage、工作队列、实体 ID 或 RNG。保存方案不建楼。 |
| template_quota | 当前无设计模板配额 | TODO：只约束 saved_designs 的数量；配额已用值从集合计数。owner/数值/解锁来源待定，不能沿用 Bonus charges 或建筑上限。 |
| construct_draft | 当前 UI _constructionUpgrade 等是暂存选择；不是权威玩法状态 | G0 可保持本地只读草稿与旧命令入口。后续若需断线恢复才设计草稿保存归属；不提前落盘。 |
| preview metadata | Planning.version / snapshot_id / source_phase / forecast | 复用当前版本与来源标签；新状态接入后扩展失效依据。预测不写 obtained_forms、不保存模板、不消费 live DP/钱包、不抽 live RNG。 |

## 3. 命令与展示合同

G0 已接入独立 Toolkit 升级窗口：设置页标为“UI Toolkit · G0 试用”，默认关闭；窗口副标题为“现有建筑升级规则”。`CityToolkitUpgrade.cs` 展示真实 Development.choices/preview，实际按钮经版本检查后只调用 Runtime.Execute → Development.start_upgrade。当前这个动作只是旧建筑定义升级，不应伪装成 Wild 节点构筑。G0 不增加可点击的未来节点、无来源容量条或虚构的撤销结果。展示模型仅承载现有数据，不能据此认为 Wild 构筑已可玩。Core 的隔离节奏检查见 [G0 节奏基线](G0_PACING.md)；Toolkit 的 Player 实际画面与操作、真人节奏验收仍待独立证据。

后续命令建议以职责描述，暂不承诺接口签名：

| 职责 | 读取 | 唯一提交 | G0 状态 |
| --- | --- | --- | --- |
| 检查/预览构筑 | 已提交建筑状态 + Catalog + 用户草稿 | 无；克隆态同一步模拟、回传成本/来源/限制 | TODO，现有 Planning 是技术基座。 |
| 提交节点/升级 | 最新权威状态及合法性；不信任缓存预测 | Runtime 串行命令更新建筑构筑并走编译/施工/历史 | TODO；当前仅旧 start_upgrade。 |
| 撤销 | 实际谱系、投入、后续节点、已装 Bonus | 同一命令处理依赖与退款，不能 UI 单删节点 | Q1 未定。 |
| 直建 | 已取得形态资格 + 当前设计 + 地块/资源 | 实际建造命令；不补造历史和取得事实 | Q5 未定，现行玩家入口不可直建高级形态。 |
| 保存/应用设计 | 设计选择、模板配额、当前资格 | 保存只写模板集合；应用先预览，实际施工另提交 | TODO，不能复用 Bonus 钱包作为模板库。 |

实际提交重新检查城市版本、资格、资源和依赖。当前 Execute 并非通用事务回滚器，未来新命令应先完成必要检查，再一次性更新相关权威状态；不需要为 G0 建新事务框架。

## 4. 保存与迁移边界

当前 live owner：局部 Game.s 持有建筑与 Bonus，CitySession.Games 持有每个局部引擎；CitySession.s 持有整城元数据。CitySession.snapshot 将各 Game 快照装配入 blocks[id].state；不得额外存一份可变建筑账本。

候选新增状态的归属原则：逐建筑构筑随 projects[id]；全局资格/容量来源及设计库在最终确认的全局 owner（范围未定时不选 city/block/account）。派生 stats/DP 已用/模板已用/预览不另存独立权威值。目录引用的含义变更须与保存校验一起考虑。G0 不修改 schema 6 / envelope schema 1，也不承诺跨版本兼容或为 Godot 导入提供支持。

## 5. Q1–Q5：全部开放

| 问题 | 待决定内容 | 当前实现仅作对照，不能当答案 |
| --- | --- | --- |
| Q1 撤销 | 材料是否退还？后续依赖节点与已安装 Bonus 如何处理？ | 现有升级无逐级撤销；拆楼的库存转仓与损耗不是节点退款。 |
| Q2 容量成长 | 全局最大 DP 如何随时代成长？已有建筑如何继承、升级容量？ | 当前 tier 解锁槽、CP/月增长均不是 DP 规则。 |
| Q3 指数成本 | 槽位指数成本作用于资源、DP 还是两者？是否逐 Tier 重置？ | 当前 slot_track/unlocked_slots 没有此成本。 |
| Q4 Bonus 结构 | 组织、联系、融合类别如何对应槽型及规则？ | 当前 organization 是运营组织 host；Production/Logistics/Operations/Flex 不是这些新类别的既定映射。 |
| Q5 首次取得 | 首次选择、开工或完工哪个事件算取得高级形态，何时开放直建？ | 当前 upgraded 事件发生于完工，但这不自动决定 Wild 取得时点。 |

G0 退出条件是：源码入口、状态归属、现状与目标差异可以被检查，Q1–Q5 仍清楚列为开放。本文不以“字段已画好”宣布玩法完成，不触发 G1 实现。

本轮后续运行结果见 [G0 交付与运行记录](G0_DELIVERY.md)，其中区分实际 Player 截图、合成控件事件与待真人确认的体验。
