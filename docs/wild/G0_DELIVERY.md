# MET Wild G0 — 引擎基线与 UI Toolkit 单窗口试验

日期：2026-10-26。版本：`0.7.0-wild-g0.1`。本轮只实施 G0 工程部分；G1 建筑星图、设计点、直建和模板均未实现，也未擅自设定待决规则。

## 交付内容

- [引擎审计](G0_ENGINE_AUDIT.md)：实际命令、快照、建筑定义、施工、Bonus 与保存通路，以及可复用/需新增/不触碰范围。
- [构筑数据合同草案](G0_DATA_CONTRACT.md)：未来状态与当前权威来源的对应关系，Q1–Q5 保持开放。
- [节奏基线](G0_PACING.md)：确定性起点、12 分钟等效工程回放与真人观察脚本。
- 可选 UI Toolkit 升级窗口：UXML 组织结构，USS 管理样式，C# 展示模型/绑定及命令适配。继续使用真实 `Development`、`Planning`、`Construction` 与 `Runtime.Execute`。
- macOS universal 应用、独立试玩启动器、原始测试存档和 ZIP。正常应用启动不自动改用户设置；独立试玩启动器默认启用 Toolkit 并使用包内隔离存档。

## 如何试玩

完整解压发布包，双击 **启动G0试用.command**。原城市场景以 2560×1440 全屏打开，起点暂停，18 人、700 通货、75 DT。地图选中农居 `0,1` → 升级，即可比较住宅/农业站。取消返回原地块；确认通过真实命令付费并加入施工队列。

试玩进度放在包内 `G0-playtest/`，再次启动会继续这份进度；`initial/` 保留暂停的原始起点。重新开始可退出游戏后将 G0-playtest 文件夹改名保留，再启动。启动器不读取或修改正常游戏存档。

若直接打开应用，则仍使用原来的正常游戏目录。可以在 **MET → 显示 → 升级窗口：UI Toolkit · G0 试用** 开启或关闭实验。新建窗口、HUD、地块、工作区继续使用原有 uGUI。

## 工程边界和实现清单

| 层次 | 文件/职责 |
|---|---|
| 接入现有游戏 | `Assets/City/UI/CityToolkitUpgrade.cs`：读取分离快照、建立展示模型、提交既有升级命令、管理模态与返回焦点 |
| 独立窗口 | `Assets/City/UI/Toolkit/UpgradeToolkitView.cs`、`UpgradeWindowModel.cs`：绑定候选/变化/限制、保留刷新时的滚动与选择 |
| 结构与样式 | `Assets/Resources/CityToolkit/`：窗口、候选、数值行、主题及 PanelSettings |
| Unity 准备 | `Assets/Editor/CityToolkitAssets.cs`：通过 Unity AssetDatabase 准备带国际文本资源的 PanelSettings |
| 输入与入口 | CityInterface、CityConstructionWindow、CityRecords、CityHud、CityTilePanel：路由开关、背景屏蔽、Esc、暂停与 HUD 恢复 |
| 重现工具 | `tools/unity_core/WildG0Program.cs`、`Assets/City/CityWildG0Evidence.cs`、`tools/run_wild_g0.command` |

上表 Assets 路径相对 `unity/METLivingWorld/`。对照 UI15 来源，Core 模拟、内容配置、资源资产、包依赖和存档格式没有变化；City 场景由原构建器重新序列化，只有生成对象编号变化。当前 checkout 和原有未提交工作保留。开始前的工作文件快照在本地 `build/wild-g0/baseline/pre-g0-source.zip`，不包含玩家存档。

## 运行证据

最终源码在 Unity **6000.6.5f1** 构建实际 macOS Player，并在 **Apple M5 Max / Metal、2560×1440 全屏**运行。最终 **111 项工程断言通过**，结果见 [Player 工程记录](evidence/g0/player-evidence.json)。本轮检查和截图使用隔离起点，不使用玩家档。

验证覆盖：

- 现有城市渲染器正常初始化；Toolkit 仅替换升级入口，原 uGUI 升级入口可切回。
- 预览/切换候选/取消保持实际会话与 RNG；实际确认只付一次费用并创建原生 upgrade_job；已有施工禁止重复确认。
- 模态期间地图输入与经营时钟归窗口管理；取消/确认后恢复地块和暂停状态下的右侧 HUD。
- UI 80/100/120% × 字号 100/125/150% 共九组布局，窗口不越界，确认区独立于滚动区；仅纵向滚动，完整对比能展开滚到底。
- 原生 Core 的 **33 项**检查通过；同种子再次运行，费用、轨迹、机会与终点存档逐值一致。

按钮检查通过 Toolkit 的合成 NavigationSubmitEvent 执行，滚动通过 ScrollView 状态执行；**不是物理鼠标、滚轮或键盘验收**。Esc 已接入现有关闭顺序，本轮没有把自动调用冒称真人按键。也未做 GPU 性能基准、完整旧 UI 全量回归或 12 分钟真人游戏性判定。

本次真实运行发现并修正：UXML/USS 同名资源加载导致样式未绑定；运行时新建 PanelSettings 缺少 ICU 文本资源；live/展示快照对象版本号混用导致确认误判过期；关闭时 HUD 未刷新。随后根据实际截图修改结构与样式，去掉横向滚动条、修正正文缩进，并压缩候选与标题区。默认字号下主要变化和投入约束可同时阅读；放大字号时正文内部滚动，底部支付与确认保持可见。

截图为真实 Player 城市场景：

- [默认布局](evidence/g0/02-toolkit-default.png)
- [UI 120% / 字号 150%](evidence/g0/04-large-type.png)
- [完整城市对比展开及滚动](evidence/g0/05-city-comparison.png)
- [确认后的实际升级工程](evidence/g0/06-upgrade-started.png)

## 复现命令

Core 回放命令见节奏基线。构建使用：

```sh
MET_UNITY_RUN_DIR="$PWD/build/wild-g0/build-checks" \
MET_UNITY_MAC_OUTPUT="$PWD/build/wild-g0/MET Wild G0.app" \
bash tools/test_unity_city.sh mac
```

工程 Player 检查必须使用显式隔离路径（每次从原始 fixture 复制）：

```sh
mkdir -p build/wild-g0/player-save build/wild-g0/player-evidence
cp build/wild-g0/initial/met_city_v1.json build/wild-g0/player-save/met_city_v1.json
MET_UNITY_SAVE_ROOT="$PWD/build/wild-g0/player-save" \
MET_WILD_G0_OUTPUT="$PWD/build/wild-g0/player-evidence" \
"build/wild-g0/MET Wild G0.app/Contents/MacOS/MET 来生 Unity Beta" \
  --wild-g0-checks -screen-fullscreen 1 -screen-width 2560 -screen-height 1440 \
  -logFile "$PWD/build/wild-g0/player-evidence/player.log"
```

## 结论与下一阶段

这次实际接入证明：可在保留现有城市和命令层的前提下，独立使用 Toolkit 编写、修改并运行升级窗口。后续新构筑窗口可以沿用这条接口；本轮没有据此宣布全 UI 迁移完成或 HTML 视觉等价。

G0 的工程底座可交付。真人操作体验与节奏仍由用户判断，G0 游戏性出关尚未成立。G1 前仍需明确取消节点的材料/依赖处理、设计容量成长、槽位指数计费对象及高级形态首次取得时点；Bonus 分类与槽匹配保持开放。不得用现有 Beta 规则默默代填。

发布为预发布版本，应用包含 arm64 与 x86_64；本地 ad-hoc 签名，无 Developer ID 公证。包装检查记录见 [package-check.json](evidence/g0/package-check.json)。
