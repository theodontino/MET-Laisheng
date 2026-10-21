# MET 完整城市发展弧接续合同

接续标识：MET-CITY-ARC-20261013-01。基线为 codex/met-beta-v3 的 unity.3（72aee5b），原生成、生长、连接及局部更新保留。

本次接续授权完整 City / District / Block 城市发展弧，覆盖 AGENTS.md、宪法与历史 0.4 的单 Block / District 仅展望边界。其余真实模拟、四层 UI、快照只读、Unity 6.6、1920×1080 全屏及 60 FPS 预算继续生效。只在当前 checkout 本地开发、构建和验证，不推送、不发布、不合并、不部署；此限制覆盖站立发布授权。完成本轮后停止，不自动开启第三轮。

完整来源附件已通读。附件的“当前已有/未做”是旧 main 0.4 盘点，不能覆盖当前 Unity 事实：有限 DT、离线只记录、Local 全系统推进和 External 市场运力已接通，不重复实现。

## 状态与模块合同

- CitySession 持有每个 Block 的 Game 实例；Game.s 是局部唯一运行真相。Sim 替换 Game.s 后，保存必须读取最新实例，不能保留陈旧 state 引用。
- Session.s 持有永久 City、地区与任期、Block 元数据、唯一 Active District、焦点、网络、遗产、科研、工程、世界史。snapshot() 将各最新 Game.snapshot() 加入 blocks[id].state；运行时不把这些副本当真相。
- 本地建筑/人口/地格 ID 不重写，跨界引用统一为 block_id + entity_id。Block 稳定不终止 District，旧地区继续实际自治，可查看并付代价回任。
- 共享 External 切片推进所有局部系统与一次世界供应；Local 加速只作用于被授权的焦点 Block。自治奖励保留队列但不阻塞经济。离线不推进世界、不补 DT。
- 网络真实扣源、入缓冲、到货、收运费并共享节点运力。世界外部库存与补给仅一份，不能每个 Block 复制一份无限供给。
- 科研消耗实际知识与投资；高级部件独立于低级 goods，从真实工业投入产生。托卡马克之冠要求多地区能力、技术、真实交付及共享施工能力，完成后同城继续经营。

## 实施和验收矩阵

| 主轴 | 玩家入口 / 归属 | 输入与跨层作用 | 保存与可见失败 | 必须实际验证 |
| --- | --- | --- | --- | --- |
| 三层永久世界 | 新城选择地区；城市页选择地区与 Block | 同城新增区域，旧实体、人口、资产和历史保留 | 全城 envelope；未知归属、非授权编辑显示原因 | 两区多 Block，导航只读，完整回读 |
| 新 Block 与依赖 | 城市页选择地点、专业方向和供给来源 | 从旧库存/财政支付启动包，地理与已建产业改变机会 | 来源/投入及关系留存；资源不足拒绝 | 真扣旧源并开局，新旧地区实际交换 |
| 双时间与物流 | 既有时间干预；城市物流页 | Local 全系统，External 守恒供应、节点/边运力、缓冲与成本 | 保存时钟、在途库存、订单、账本；无库存/库容/资金明确停运 | 倍率不造外货，多边共享容量、加速短缺与缓冲 |
| 治理与接续 | 地区治理、交班、危机、回任 | 实际粮食/财政/服务/污染与承诺改变 Support/Mandate | 唯一 Active，历次任期和理由保留 | 主动交班与真实危机离任；旧区自治；付代价回任 |
| 分层遗产 | 城市遗产和开局选择 | Local 留原地；Portable 真转移许可；District 制度和 City 能力分别生效 | 消费/来源记录；不复制奖励或搬空旧库存 | 先前运营/专业化改变后续开局和制度价值 |
| 身份与世界史 | 地区命名、城市史、地区档案 | 实际产业、人口、制度、布局及任期事件派生身份 | 永久事件 ID 与消费游标；刷新/回读不重播 | 同一世界史保存、视图与预览不改 RNG/经济 |
| 科研/高级工业/全城工程 | 城市科研与工程页 | 教育知识、投资、运行产业及高级部件、材料和 CP | 保存技术、投入、进度、完成一次反馈；前置不足拒绝 | 不注入高级资格的完整能力链，完成后继续同城 |
| 核心基础与 UI | 现有施工/升级/构筑/市场及四层页面 | 保留运营商槽轨、奖励队列、人口与服务、源信息 | 全城备份和语义恢复，独立显示设置 | 旧核心回归 + 新纯 C# 弧 + 当前 Mac 原生入口 |
| Life 边界 | 文档接口，本轮不接现实端 | 独立 Wild；桥接版本、去重和事实更正边界 | 不触碰另一个项目或真实记录 | 明确接口与未接入范围 |

本表是目标合同，不能冒称已经完成。实际状态和证据在 ACCEPTANCE.md 更新。数值默认值将可逆、数据化并记录理由；不声称好玩、真人试玩或 Civ6 美术已通过。

## 并行接口（固定）

CitySession 为 MET.Core 的 sealed partial class。主状态 s；Catalog c；Dictionary<string,Game> Games；FocusedGame（未选地区时是空 bootstrap Game）；FocusedBlockId、ActiveDistrictId、CanEditFocused；Block(id)/District(id)、Uid(prefix)、Record(kind,payload,key)、snapshot()、Validate()、New(seed,utc,name)、BeginDistrict(landing,specialization,name)、BeginBlock(landing,specialization,sponsor)、FocusBlock(id)、StabilizeBlock(id)、HandoverDistrict()、ReturnDistrict(id)、AfterLocalCommand(before)、Resume(utc)。

时间/网络 partial：Advance(externalSeconds,mode="")、CreateLink(from,to,resource,rate,cost)、SetLinkEnabled(id,bool)、NetworkView()、SettleNetwork(seconds)、ApplyWorldEffects()。在必要边界上做实际世界事务，不为普通只读入口堆防御框架。

科研/工程 partial：ScienceTick(localMonths,externalSeconds)、StartResearch(id,blockId)、StartProject()、ProjectView()/ScienceView()、ContributeProject(blockId,resource,amount)。高级部件库存保存于真实局部仓库，并纳入网络存取。Local/External 单位沿用当前正典：旧 local_hours 实际为经济月，月秒数由 beta.units 读取；运力为现实小时。
