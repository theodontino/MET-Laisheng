# MET 0.5.0-beta.2：构建与验证

本指南描述当前源码的复现入口。验收结论、总检查数和产物校验值应以对应运行生成的证据及最终验收报告为准。设计包列出的目标不是测试结果；历史 0.4 报告不能替代本次结果。

## 工具与源码

使用 **Godot 4.7.2 Standard**。本项目运行 GDScript，不需要 .NET；检查 `--version`，不要直接假设系统 `godot` 就是指定版本。测试脚本使用 Bash、`rg`；源码打包使用 Python 3 标准库。已有运行资源随仓库提供，普通运行与测试不依赖 Blender；修改美术生成器后重新生成资源才需要相应工具。

在现有 checkout 中工作，不要为了这些检查另建 Git worktree。以下命令在仓库根目录的 Bash 中执行：

```bash
export GODOT=/absolute/path/to/Godot_v4.7.2-stable_linux.x86_64
export MET_PROJECT_ROOT="$PWD"
"$GODOT" --version
"$GODOT" --headless --path "$MET_PROJECT_ROOT" --editor --import --quit
"$GODOT" --path "$MET_PROJECT_ROOT"
```

首次导入会生成 `.godot/` 缓存。它不是源码，也不应进入源码包。macOS 与 Windows 需要对应平台的引擎；Linux 导入或测试不能证明它们的原生窗口与图形驱动可用。

## 隔离测试数据

测试会创建样例状态、城市记录与证据。建议先为测试单独指定数据和证据目录；不读取或覆盖玩家的默认存档目录：

```bash
export MET_TEST_ROOT="$(mktemp -d /tmp/met-beta-test.XXXXXX)"
export XDG_DATA_HOME="$MET_TEST_ROOT/data"
export XDG_CONFIG_HOME="$MET_TEST_ROOT/config"
export XDG_CACHE_HOME="$MET_TEST_ROOT/cache"
export MET_EVIDENCE_DIR="$MET_TEST_ROOT/evidence"
mkdir -p "$XDG_DATA_HOME" "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME" "$MET_EVIDENCE_DIR"
```

这些 XDG 设置适用于 Linux。跨平台测试需要选择对应的独立用户数据目录，并在报告中写清路径。游戏主存档为 `user://met_beta_v6.json`，只读结算记录为 `user://city_records/`；`user://` 的正常平台位置见 [README](../../README.md#存档与城市记录)。

## 必跑核心与路线检查

```bash
bash tools/test_beta.sh
```

`GODOT` 和 `MET_EVIDENCE_DIR` 使用前文环境变量。脚本缺少必需套件、子进程失败、出现脚本错误或失败标记时，会返回非零。

| 套件 | 主要证据 |
| --- | --- |
| `beta_time` | 历法、有限 DT、离线冻结、分段结算、外部贸易时间与最终边界 |
| `beta_bonus` | 奖励队列、重复副本、实际装配与规则合成、两次蓝图研究、回退与保存恢复 |
| `beta_themes` | 五个主题、事件费用与交付、实际服务/贸易指标、主题切换与期限 |
| `beta_lifecycle` | 最终段完成与晚完成资格、移民后的完整安居时间、满图地标与归档失败保护 |
| `beta_planning` | 切换时间档、订单、交付与付款后的预测失效，真实预演收据与玩家状态只读 |
| `beta_feedback` | 告警、提示、噪声控制与展示只读性质 |
| `beta_math_cache` | 六角距离与空间缓存的精确性、有界容量、失效与缓存前后结果一致 |
| `beta_allocation` | 分配收敛优化与保留的旧算法对照，实际分配、来源账本及整段模拟状态一致 |
| `beta_acceptance` | 跨模块状态、奖励、结算和存档约束 |
| `beta_playthrough` | 从开局通过玩家可用命令经营的五种主题路线，加一条实际获得稀有重铸权后改向的路线，逐条记录成功或失败 |

日志输出到 `$MET_EVIDENCE_DIR/<suite>.log`。部分套件还写出 JSON 报告。单独运行一项：

```bash
"$GODOT" --headless --path "$MET_PROJECT_ROOT" --script res://tests/beta_lifecycle.gd
```

单独复现一条经营路线，例如农业：

```bash
"$GODOT" --headless --path "$MET_PROJECT_ROOT" \
  --script res://tests/beta_playthrough.gd -- --theme=agricultural
```

其他主题标识为 `industrial`、`civic`、`commercial`、`logistics`；`--theme=reforge` 运行先选工业、获得真实稀有重铸权后转向农业的第六条路线。路线脚本生成行动记录、月度轨迹、主题结果与同版本样例存档；失败路线必须保留，不能通过修改最终状态把未完成路线写成成功。

脚本向游戏时钟提交的“真实活跃秒数”是模拟输入，不等于测试进程在现实中等待了这么久。固定种子的自动策略不代表真人操作，也不证明所有种子、顺序或成长节奏已平衡。路线 JSON 的 `cpu_seconds` 是整条自动路线的执行墙钟耗时，不是操作系统 CPU 计数或玩家游玩时长。

## 原生窗口与 UI 截图

整个 pre-1.0 默认桌面全屏，最低 **1920×1080**。本轮只验证该全屏基准；用户明确不要求不同大小窗口测试。新四层界面与地图重置入口：

```bash
bash tools/test_ui_reset.sh
```

该脚本需要有效 `DISPLAY`，使用真实鼠标/键盘输入检查 L0/L1/L2/L3 与地图选址、主动作和返回，校验测试期间产品源码指纹不变，生成 JSON、日志与原生截图。底层入口是 `--script res://tests/ui_reset.gd`。原有 Beta 决策流程回归也在同一全屏基准运行：

次级／三级页面另有专门的原生交互套件，检查资源来源、人口需求、政策预演、升级、施工、奖励队列、安装、市场报价和实际订单；报告与主界面分开保存：

```bash
MET_UI_SUITE=desk MET_EVIDENCE_DIR="$MET_TEST_ROOT/desk-evidence" \
  bash tools/test_ui_reset.sh
```

建设影响、整体 UI 缩放与图层快捷键使用独立的真实结算及原生输入夹具。所有比例均保持 1920×1080 全屏；不测试其他窗口尺寸。套件同时核对同一步预测、当前库存、全城电力、原始环境场以及玩家状态／RNG 只读：

```bash
MET_UI_SUITE=impacts MET_EVIDENCE_DIR="$MET_TEST_ROOT/impact-evidence" \
  bash tools/test_ui_reset.sh
```

结果写入 `ui-impacts.json`，原图、输入操作和前后源码指纹保存在同一证据目录。实际完成状态见 [建设影响验收](UI_IMPACTS_ACCEPTANCE.md)。

正常主场景的 Beta 决策流程回归使用以下入口，不附加窗口尺寸或全屏命令参数，直接验证游戏的默认启动行为：

```bash
"$GODOT" --path "$MET_PROJECT_ROOT" -- --beta-ui
```

没有显示器的 Linux 环境可使用 Xvfb 与 Mesa：

```bash
xvfb-run -a -s '-screen 0 1920x1080x24' \
  env LIBGL_ALWAYS_SOFTWARE=1 bash tools/test_ui_reset.sh
xvfb-run -a -s '-screen 0 1920x1080x24' \
  env LIBGL_ALWAYS_SOFTWARE=1 "$GODOT" --path "$MET_PROJECT_ROOT" -- --beta-ui
```

该入口加载真实 App 与原生渲染器，通过 UI 回调检查时间档、奖励、升级、告警、尾声等，并把帧缓冲截图写入证据目录。它使用显式状态夹具；不能把这些画面描述成自然游玩路线。`--headless` 无法代替真实窗口截图。

检查截图时同时看信息分层、图形含义、地貌与建筑尺度、滚动及可操作按钮。Godot 的“帧已保存”只证明生成了图像，不自动证明视觉验收通过。Xvfb + llvmpipe 的帧率属于软件渲染环境，不能代表桌面 GPU、M 系列 Mac 或 Windows 实机性能。此前小窗口截图仅为历史证据，不属于本轮验收。

已通过且源码稳定的主界面和档案报告可生成独立 HTML 画廊。地图目录需含同脚本的 `before/after-{2,12,53}.png` 及对应 JSON；工具校验场景元数据与原图尺寸，不修改图片：

```bash
python3 tools/build_ui_reset_gallery.py \
  --ui /path/to/final-ui-evidence --desks /path/to/desk-evidence \
  --beta-ui /path/to/default-main-beta-evidence \
  --maps /path/to/matched-map-evidence \
  --output build/MET_Laisheng_Beta_2_Visual.html
```

## 性能基线

性能探针使用真实 App，分别创建 2／12／53 栋建筑规模夹具，记录静止、平移、缩放与时间推进阶段，以及规则编译、快照、反馈等只读成本。默认每阶段采样 20 秒；短时冒烟可用 `MET_PERF_STAGE_SECONDS` 调整，但报告必须保留实际采样时长。

原生探针显式设置根 App 全屏锚点，记录实际显示器、窗口、根节点与地图尺寸，并检查绘制回调、全部地格投影和截图。地形网格构建数只作诊断，因为新地格直接绘制多边形。Beta.1 旧探针漏设根锚点，未测到完整地图；旧帧耗时不能用作本轮地图性能基线，见[历史性能报告的更正](PERFORMANCE.md)。比较时必须让两个版本使用同一修正脚本，并在运行期间停止其他 Godot 进程。

无窗口 CPU 检查：

```bash
"$GODOT" --headless --path "$MET_PROJECT_ROOT" \
  --script res://tests/beta_performance.gd
```

Linux 原生窗口基线：

```bash
xvfb-run -a -s '-screen 0 1920x1080x24' \
  env LIBGL_ALWAYS_SOFTWARE=1 "$GODOT" --path "$MET_PROJECT_ROOT" \
  --fullscreen --resolution 1920x1080 --script res://tests/beta_performance.gd
```

也可以在全部核心与路线检查后附加无窗口性能探针：

```bash
MET_BETA_PERFORMANCE=1 bash tools/test_beta.sh
```

输出分别为 `beta_performance.json` 与 `beta_performance_native.json`。保留引擎版本、源码指纹、显示后端、渲染器、样本量、耗时分位数和进程内存口径。只读探针的耗时并非一次 Tick 内互斥的各阶段，不能直接相加。无窗口耗时不是 FPS；通过性能脚本的结构检查也不等于达到帧率目标。不要把包含引擎、资源和截图开销的整个进程 RSS 当成某个平台的游戏常驻内存。

## 源码交付与独立解包

```bash
python3 tools/package_beta.py --output build/MET_Laisheng_Beta_2_Source.zip
```

本轮使用独立文件名，保留之前交付的 Beta.1 包及其校验值。包内 `SOURCE_MANIFEST.json` 记录基础提交、是否包含未提交修改及成员 SHA-256；脚本检查成员哈希和 ZIP CRC。它不收集 `.git`、`.godot`、用户存档目录或历史证据目录，不会自动发布 GitHub。

哈希正确只说明包内容一致，不能证明包可以独立运行。至少在全新目录导入并启动：

```bash
export MET_EXTRACT_ROOT="$(mktemp -d /tmp/met-beta-extract.XXXXXX)"
python3 -m zipfile -e build/MET_Laisheng_Beta_2_Source.zip "$MET_EXTRACT_ROOT"
"$GODOT" --headless --path "$MET_EXTRACT_ROOT/MET_Laisheng_Beta" \
  --editor --import --quit
"$GODOT" --headless --path "$MET_EXTRACT_ROOT/MET_Laisheng_Beta" \
  --script res://tests/beta_lifecycle.gd
"$GODOT" --path "$MET_EXTRACT_ROOT/MET_Laisheng_Beta" -- --fresh
```

最后一条需要可用桌面，可按前文使用 Xvfb。解包测试应继续使用隔离的数据目录。源码包、测试证据与样例存档分别交付；不要把当前玩家的存档自动放进源码包。旧 `build/` 中的 0.4 macOS、Windows 或其他历史文件不能重命名充当 Beta 已验证产物。

## 证据边界与剩余验证

| 类别 | 可以回答 | 不能替代 |
| --- | --- | --- |
| 数学/核心测试 | 规则、边界、守恒和状态变更是否符合断言 | 玩家是否觉得有趣 |
| 显式场景夹具 | 特定规模、告警、尾声与界面状态是否成立 | 正常开局能否自然走到该状态 |
| 玩家命令路线 | 指定种子与策略是否通过真实命令达成结果 | 所有种子、所有构筑或真人体验 |
| Linux 原生窗口 | 当前窗口后端上的布局、交互和渲染 | macOS、Windows 硬件验收 |
| 性能采样 | 指定硬件/渲染器/场景的成本与趋势 | 未测设备性能或稳定帧率承诺 |

本轮 macOS、Windows 实机与真人试玩需要分别记录，不能从 Linux 检查推断通过。整局时长、首张 Bonus、主题完成余量、贸易等待和高倍率反应窗口都是需要继续校准的体验数据。最后验收报告应按实际结果填写，不预写“全部路线通过”或“性能达标”。

整个 pre-1.0 不安排移动端开发、适配、打包、实机测试或发布。只保留架构、交互和性能资源方面的合理余量；移动端没有测试结果不阻塞本次交付。

相关入口：[Beta 设计包](design-v3/00_READ_FIRST.md)、[单位审计](UNIT_AUDIT.md)、[项目宪法](../PROJECT_CONSTITUTION.md)。历史的 [0.4 构建说明](../v040/BUILD_040.md) 与 [0.4 验收记录](../v040/LAISHENG_ACCEPTANCE_041.md) 只解释对应版本。
