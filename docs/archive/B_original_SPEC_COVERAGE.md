> 历史档案：B 0.1.0-exp003 原文，已被 B 0.1.1 人口版决策覆盖。不得作为本次测试结果或当前玩法规格。

# MVP-01～28 落实表

依据 `SOURCE_SPEC.md`（控制文件第16章）。这里的“实现”仅表示有对应实际规则与交互，不把程序检查等同于用户playtest通过。

| 项 | 落实情况 | 主要位置 / 证据 |
|---|---|---|
| 01 首Block完整切片 | 实现，从New Game到奖状/无限或治理失败；只开发一块 | `ui/app.gd`、`tests/playthrough.gd` |
| 02 固定地图+薄随机 | 3幅61Hex固定局部地图，固定District入口 | `core/hex.gd`、`ui/dialogs.gd:start` |
| 03 有限信息/Survey | 薄实现，选地后一次揭示；无详勘进度 | `game.select_block` |
| 04 Origin暗抽明选 | 4池暗抽2选1，候选与种子保存 | `game.select_block/choose_origin` |
| 05 招投标三权重 | 合法池先检查，交/并/上下文三槽权重，不重复并洗牌 | `core/tenders.gd` |
| 06 政策Tag三来源 | 6基础+科技解锁+居民奖励选择，显式纳入 | `core/progress.gd`、`game.add_policy_tag` |
| 07 四用途 | 定居/生产/流通/公共，决定合法池硬边界 | `catalog.json`、`zone_cells` |
| 08 项目施工 | 通货前付、物料逐步取、能源限制、实际完工 | `simulation.gd`、`tenders.accept` |
| 09 默认配套 | 道路不占格，视觉院落/厂内附属不需要玩家微操 | `ui/map_view.gd` |
| 10 自由主动发起 | 无额外券/冷却，未决投标保留；不能免费换地刷新 | `tenders.development/withdraw` |
| 11 Stock+Flow | 三类实物库存+接口签名、能源容量、产能和实际消耗 | `inventory.gd/simulation.gd` |
| 12 逐步显示 | 食物/材料先出现，后续复杂项目揭示能源/制成品 | `game.gd/tenders.gd/ui.app` |
| 13 安全底线+起源 | 共用底座与4种差异家底，v0参数集中 | `balance.json/catalog.json:origins` |
| 14 自由涂区+1～7Hex | 拖涂、圈选、同用途/连通/占用检查；规模成本和容量 | `hex.gd`、`zone_cells`、`tenders.development` |
| 15 Site内部 | **实验：一个Site一个主体**，`project_ids`保留，容器玩法未做 | `game.s.sites` |
| 16 施工Opportunity | 6小表，按上下文与概率最多一次；原计划可无罚继续 | `progress.try_construction_event/opportunity` |
| 17 双轴成长 | 里程碑升级+关系槽；升级竣工生效 | `progress.gd/game.project_stats` |
| 18 关系成熟 | 相邻且兼容，实际有效共存累积，非摆放领奖 | `progress.tick` |
| 19 无provenance | 双方运行/usage代理；不新增建筑间物流图 | `progress.tick` |
| 20 模板里程碑 | 两级模板+邻接/材料情境条件+可达替代；内容仍薄 | `progress.milestone` |
| 21 软分叉 | 升级改变现有Tag、接口和未来权重；非固定锁死职业 | `tenders.legal_pool/draw` |
| 22 升级单软偏好 | 可不指定或选1PolicyTag；叠加历史/邻接/瓶颈权重 | `dialogs.upgrade_preference`、`tenders.draw` |
| 23 递增重招 | 递增通货，第三次起DT；换地继承；**锁牌未做** | `tenders.reroll_cost/reroll/withdraw` |
| 24 单区时间 | 在线1:1、离线储DT、轻TL、同模拟Pulse、库存真实消耗 | `game.pulse/resume_time`、`simulation.gd` |
| 25 薄治理 | 1主+2居民请求、双支持、简化危机、可罢免只读 | `progress.gd`、`dialogs.governance` |
| 26 Checklist+无限 | 5项清单，奖状只领一次，继续建设；失败另一路 | `progress.checklist/tick`、`dialogs.award` |
| 27 内容预算B | 4Origin/20Proposal/12Upgrade/4Fusion模板/6Opportunity/3委托/3新增Tag/1危机 | `catalog.json`、规则测试 |
| 28 玩家验收 | **待用户实测**。日志/报告/F8/同种子重开已做，不宣告乐趣通过 | `TEST_REPORT.md`、导出实测报告 |

## 和规划相比的明确实施选择

JSON内容定义由Resource目录加载，不是一卡一tres。MVP15采用独占场地，不是未来定稿。多数项目有类型化机制，部分基础保障仍以产能数值构成。Origin“自由定居”使用固定基础料棚安全垫。未做所有高级Tag/状态的通用任意DSL，只实现20张卡实际所需操作。更完整的城市/政治/跨区规则不提前塞入第一Block。

不能把这张表读成“全部设计已验证”。尤其数量够、流程能完成，与重玩性/取舍感是两回事。
