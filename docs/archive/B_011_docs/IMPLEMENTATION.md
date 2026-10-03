# B 人口版实现入口

保留 B 的 `core/game.gd → simulation/progress/tenders/storage` 分工。新人口模块是现有模拟的一部分，没有第二套游戏引擎或UI状态副本。

| 文件 | 职责 |
|---|---|
| core/game.gd | 权威状态、玩家动作、项目统计、阶段动作与存档校验 |
| core/population.gd | 实际住户、就业/效率、有限服务覆盖、排放、健康教育幸福、迁入、阶段与支持传导 |
| core/simulation.gd | 原 B 单时间入口、DT、能量和库存分配、施工、真实收入；调用人口和成长 |
| core/tenders.gd | 无涂区建造；场地合法性→发展家族→确定性候选；重抽、升级和Fusion仍复用 B |
| core/progress.gd | 里程碑、Fusion成熟、施工事件、知识研究、居民承诺、治理危机、通关 |
| core/inventory.gd | 每类资源统一库存；不再用资源Tag分包 |
| core/storage.gd | schema2完整性/序列化、独立文件名；不覆写旧版存档 |
| ui/app.gd | 保留 B 主窗口与布局，删除涂区操作，底栏分组、阶段与快捷键 |
| ui/panels.gd | 项目/住户信息、需求缺口、服务对象、噪音来源 |
| ui/dialogs.gd | 人口与阶段、知识研究、简化资源、响应土地的候选说明 |
| ui/map_view.gd | 保留 B Hex/道路表现；住户人数、服务/污染连线与新增项目基础画法 |
| content/catalog.json | 34项目、12升级、4协作模板、6施工事件 |
| content/balance.json | 所有本轮基础平衡数值 |

## 时序

原 B 的唯一时间推进入口保持。每步先计算真实人口和岗位/能源，有限资源分配到施工/运行，再计算实际产出与商业顾客税，更新住户生活和迁入，最后推进里程碑/关系/危机。

离线只恢复有限 DT、更新已有外部窗口；不会执行人口增长或城市资源模拟。DT 推进本地发展，不延长现实委托截止时间。新居民请求从安居阶段才开放窗口，不让未开放的学校任务先逾期。

## 空间与资源

服务不是有向物流：通过住宅距离、服务半径、实际利用率和容量分配。每个住宅的生活面板保留来源，地图视图只读这些结果。就业在Block自动共享。

内容的 `zone` 字段尚存在，用于原 B 建筑图形、升级类别兼容；它不再要求玩家预涂地，也不参与 Development 招标的硬筛选。这不是残留的玩家区划功能。

## 存档

schema=2，build=0.1.1-b-population。每住宅记录 residents/health/education/happiness/living；全区记录stage、stage_started、migration_paused和displaced等。旧 schema1 拒绝，原文件不变。

## 后续优先项

先根据真实存档校准初期就业/迁入、服务半径/维护及商业原料消耗，不急于扩充City系统。更多人口层级、三阶制造品、道路政策仍是未来设计，不暗中塞进这次数据结构。
