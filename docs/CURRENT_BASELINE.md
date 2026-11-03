# 当前开发基线

核验日期：2026-11-03。正式基线标签：**`v0.7.0-baseline.1`**。基于已发布 **Civic UI.2 / `6dba3da7`**，在现有 `codex/met-beta-v3` checkout 上归档，不另建游戏或工作树。

## 什么是当前版本

主游戏：`unity/METLivingWorld`，Unity **6000.6.5f1**，场景 **`Assets/Scenes/City.unity`**，全屏 **2560×1440**。本轮更改仅为版本标识与基线文档，不新增玩法。应用内版本号与构建脚本均为 `0.7.0-baseline.1`。

此次将已有本地源码、内容、工具、网页原型和设计资料正式归档，并接入 Civic UI.2 的发布历史。已有构建缓存、安装包及玩家存档不纳入源码提交。`docs/wild/proposals/` 等提案保留为参考，不能视为已批准或已实现规则。旧 Godot、Hero Valley、UI5 和网页原型保留历史意义。

## 运行

- 安装包：解压本标签对应的 `MET-Baseline.1-macOS-universal.zip`，打开 `MET Baseline.app`。支持 Apple Silicon / Intel，本地 ad-hoc 签名，未做 Apple 公证。
- 开发：Unity Hub 用 6000.6.5f1 打开工程，打开 City 场景后运行。
- 普通可经营城市：选中农居，点击“启用建筑星图”，阅读变化说明后确认；已启用时直接点击“建筑星图”。历史街区不可编辑。不需要重建城市才能显示入口。
- 独立设置 UI 80/100/120% 与字号；地图数值使用 Shift 开关，L / Shift+L 循环图层。

构建命令（仓库根目录）：

```sh
MET_UNITY_WILD_BUILD=1 \
MET_UNITY_RUN_DIR="$PWD/build/baseline-recheck/build" \
MET_UNITY_MAC_OUTPUT="$PWD/build/baseline-recheck/MET Baseline.app" \
bash tools/test_unity_city.sh mac
```

## 存档位置

生产 bundle ID：`com.projectmet.laisheng.unitybeta`。普通 Editor / 无 Wild 标记运行使用 `Application.persistentDataPath`；Wild 专用包带 `StreamingAssets/WildG1.txt`，在该目录下使用 **`Wild-G1`** 子目录。macOS 通常对应 `~/Library/Application Support/com.projectmet.laisheng.unitybeta/`。

`MET_UNITY_SAVE_ROOT` 可显式覆盖，两种工程检查入口都要求独立测试目录。基线核验只使用 `build/baseline-20261103/player-civic/save` 和 `player-star/save`；没有打开或迁移实际玩家存档。目录相同不等于已验证任意旧版本存档兼容。

## 已接入的内容及边界

| 部分 | 当前事实 |
| --- | --- |
| 城市与经营 | 原有 Unity 地图、人口、资源、施工、贸易、City/District/Block 状态与命令保留 |
| UI | UI Toolkit HUD、地图左下地块、侧栏概览、独立全屏管理、操作窗口和来源检查；原有 uGUI 仍有保留用途 |
| 星图 | 农居首段普通节点、设计点占用、真实 CP 队列、已完成节点级联撤销及 T2 公寓/农场；普通城市启用入口 |
| 科技与奖励 | 希望城贸易资格、住房设计 I/II、已配置里程碑与 W01；奖励库存、领取和装配判定已有实现 |
| 尚未完整 | 其他建筑星图、完整联系/融合构筑、高级直建报价、完整工业化与设计模板；不把旧功能当作新系统已经完成 |

规则以已确认决策和现行命令为准；历史 G0 合同、旧数值和提案不自动升级为当前需求。

## 本轮实际核验

11 组核心回归全部退出 0：Core、CityGovernance、CityNetwork、CityArc、WildG0、WildG1、WildTrade、WildContent、RogueContent、Specialization、UI15。日志及命令入口保存在 [本轮证据](baseline/20261103/)。Core 本轮未传 Godot 对照文件，**没有重跑跨引擎 parity**。

实际 Unity 编译和 Universal macOS 构建成功。实际 2K / Metal Player：Civic **869/869**、星图 **328/328**，共 **56 张截图**，均无记录错误。包含普通城市星图启用/取消/确认、施工与升阶、界面缩放、来源阅读状态和真实命令同步。

Player 交互为合成事件，使用已明示调试补给的隔离场景；不代表原生鼠标/滚轮验收、正常开局节奏、经济平衡或真人体验。本轮未将历史原生输入记录重记为新验证。包的 ZIP 解压一致性及深层签名验证已执行。

测试库的 NuGet 漏洞信息请求出现网络警告，编译与回归退出码均为 0；这不属于漏洞扫描通过的结论。

## 从此如何继续

后续从现有 `codex/met-beta-v3` 分支和本标签继续，先检查本地差异；不要回到旧 UI5 或旧 main 作为当前开发版。需要回退时使用标签对照，在保留新工作后操作。

下一开发目标：走通不依赖调试补给的正常开局到首次 Bonus 构筑，逐步核对货运/贸易/设计容量/槽位/奖励/安装的阻塞。此次建立的是**已核验的工程基线**，尚未宣布玩法闭环验收完成。
