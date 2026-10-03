# Living World V1：实现与验收

日期：2026-10-03。基于同一 Beta.2 系谱，建立独立 Runtime 3D 视觉轴。默认游戏仍使用现有 GL/2D；Forward+ Hero Valley 需显式启动。玩法版本不变。本任务没有推送 GitHub。

## 交付与固定场景

[架构审计与迁移计划](ARCHITECTURE.md) · [来源与许可证](ASSET_PROVENANCE.md) · [资产导出记录](ASSET_EXPORT.json) · [可交互 2D/3D 对照](../../build/MET_Living_World_V1_Visual.html)

固定 world seed **905003**、visual seed **314159**，37 个现有逻辑 Hex。保留半径三的主要河谷并置换三个西部边缘格，纳入原有上游弯道，共八个水格；没有改游戏地形/traits。真实 Game 创建并结算一次后暂停，输出 committed snapshot。新层只接收深拷贝，拒绝 forecast；现有 Foundation/Fabric 的派生函数由 detached facade 调用。

摄影为正交 Camera3D，size 12，方向 `[9,13.2,12]`，接近既有 41.35° elevation / −53.13° azimuth；1920×1080 全屏。固定白昼、固定环境。正常视图隐藏 Hex；H 显示跟随地面及水面的分析网格。中/右键平移、滚轮缩放，F1 隐藏工具栏，Home 重置。

- 单个连续 terrain lattice：13,819 三角形，没有逐 Hex surface。
- 连续 3D river：1,581 三角形。河床、河岸与水面共用连续曲线和宽度，微小 meander 属表现数据。
- 农居、邻里中心、作坊、住宅四栋既有建筑 GLB，合计 94,032 三角形；统一尺度/pivot、实时 PBR。原 PNG 管线与 catalog 保留。
- 一种原创蕨树，**29 个实例 / 6 个 MultiMesh 材质批次**，每树 1,892 三角形，零独立树逻辑节点。Runtime 树色独立校准，旧 PNG 色板不变。
- 两段道路来自两条实际 committed Fabric 关系；建筑平台、边坡和挡墙转换为真正 Mesh。
- registry 登记五项原创资产及 `.blend`、GLB、源/运行/生成器哈希、许可与尺度。未引入第三方包。vendor raw 同时由 Git 与源码打包排除。

`EnvironmentState` 预留 world_time、sun_state、cloudiness、fog_density、humidity、wetness、wind、temperature_band。Controller 统一拥有光照及已注册 shader uniforms；V1 不推进这些状态。

## 实际验收

| 项目 | 证据与结果 |
| --- | --- |
| 无 UI 可辨河谷聚落 | 原生固定截图可辨四栋建筑、河流、林丛及高地；截图由 root 检视，独立 Astra 审查原生图 |
| 连续景观与统一光照 | 正常图不呈现独立 Hex 拼板；实际阴影方向一致；修正了 Godot clockwise-front 绕序与河面 TBN 问题 |
| 建筑落地 | 实际 GLB ancestor transform 与 TerrainMesh/platform 三角面采样；默认及非默认 scale/pivot 两组共 **160 点**，最大偏差 **0.00354 world**，容差 0.01 |
| 数据、随机及存档隔离 | **85/85** 数据/几何检查；完整 state/RNG/wallet/compiled/revision 哈希保持；实际 Godot custom user directory 的主档/备份 guard 在 Hero 启动前后字节一致 |
| 镜头与交互 | **28/28** 原生检查，注入真实 Input 鼠标/键盘；平移、缩放、hover/select、H 与工具栏 2D/3D 切换；build_count 与 mesh/node identity 不变 |
| 确定性 | 两次进程启动的 snapshot/mesh/scatter 一致，1920×1080 PNG SHA-256 完全一致；第三次存档 guard 启动也得到同图。保证指本平台/固定条件，不承诺跨 GPU 像素一致 |
| 原核心与玩法 | **401/401** Beta 核心、**6/6** 真实命令路线；native Forward+ 再跑 **69/69** acceptance 与 **6/6** 路线 |
| 原 UI / 2D | GL 与 Forward+ 原生 UI 各 **78/78**；空间回归 elevation **11/11**、fabric **27/27**；GL 同夹具 2D 可启动 |
| 安全入口 | GL 启动 3D 明确以 exit 2 拒绝；默认 GL 主游戏不受影响 |
| 资产 | 五项实际资产验证及 **18/18** 异常输入检查通过 |

核心/UI 检查后只进行了新视觉层的校色、河岸和连接修整。`regression-linkage.json` 核对所有 core 与 Beta UI 使用的源文件保持相同；最终视觉检查、截图与性能全部用最终资产重跑。与 Beta.2 的基线 `f454b37750ffa6a9883dbe054795c29f7f382a94` 比较，206 个核心、旧地图/视图模型、旧世界资产/catalog 与项目默认文件均字节不变。此前 Beta/Beta.2 五个交付文件的 SHA-256 保持不变。

3D PNG SHA-256：`3ce8dec54fbc70787010b96e90f81b2e343a296b2330337cb6e511b5b40ee692`。Snapshot：`2de4ddd315c6eacc13b76d5d3458d79f8b77d3281e2513e57b1914dab12f3827`。

## 性能：模拟与渲染分别测量

环境：Godot 4.7.2、Linux/X11/Xvfb、Xeon Platinum 8573C，cgroup 四核预算。Forward+ / Vulkan 1.4.305，**Mesa 25.0.7 lavapipe / llvmpipe LLVM 19.1.7，CPU 软件渲染**。驱动本地提取，不是系统 dpkg 已安装包，因此 JSON 的系统包版本为 unavailable；实际映射的 ICD 路径、设备 API 与安装记录一起提供。

渲染测量只创建 paused fixture + detached World，没有 Simulation 回调和隐藏 2D/UI。每阶段预热 8 帧，记录 72 个实际 frame_post_draw 间隔；相机正常平滑移动。以下包含引擎、软件驱动、同步、调度与显示节奏，**不是纯 GPU 耗时或硬件 FPS 认证**：

| 阶段 | 样本帧 | p50 ms | p95 ms |
| --- | --- | --- | --- |
| static | 72 | 280.26 | 304.44 |
| pan | 72 | 284.21 | 325.40 |
| zoom | 72 | 324.08 | 407.28 |

世界同步构建 wall **2.82s**，进程 CPU **3.45s**；到第一帧 wall **4.50s**。这不包括引擎 Vulkan 初始化、fixture 或 snapshot 创建。builder 内部 `build_cpu_ms` / `construction_cpu_ms` 是 CPU 侧构建的 monotonic elapsed wall timer；真实进程 CPU 来自 `/proc/self/stat`，不能混用。渲染阶段 RSS 峰值约 **932 MiB**，包含整个 Godot/软件 Vulkan 进程，不是纯资产内存。

Simulation 在**另一个 headless 进程**关闭 rendering 后，用真实 `Game.advance_active(1/60)` 测量 **120 调用 / 120 个已提交步骤**。p50 **6.66ms**、p95 **7.84ms**、mean **6.79ms**，进程 CPU **0.84s**。包括 Clock/validation，并非单独 `Sim.tick` 的 CPU timer。实际 local/active 时间、tick 与 segment 增量均校验。此小夹具不是大型城市性能证明。

软件 Vulkan 帧耗时很高；它证明功能链路与隔离成立，**不满足流畅度认证**。迁移结论是保留 Forward+ 为显式开发路径、默认继续 GL/2D。下一阶段先在目标桌面 GPU 测量 shadow/SSAO/材质与 instance 开销，再决定默认迁移。

## 视觉限制与下一阶段

本次建立了可运行、可重复验收的首张 3D 河谷，尚未达到 Civilization VI 同类商业级完整美术。树冠/地表与岩坡较首图改善，但仍是一种树的重复轮廓，地貌与河岸仍偏简化，建筑院落较独立，街区密度和自然组合还需要美术打磨。下一步建议依序处理地表/河岸材质及少量自然组合、真实连接的连续街区、目标桌面硬件 profile 和按 registry 分批扩展建筑；保持 snapshot 只读。昼夜、天气、多 biome 与玩法扩张不属于本交付。

## 复现及源码包

```bash
export GODOT=/absolute/path/to/Godot_v4.7.2-stable_linux.x86_64
"$GODOT" --headless --path . --editor --import --quit
bash tools/test_living_world.sh
MET_LIVING_NATIVE=1 bash tools/test_living_world.sh
"$GODOT" --path . --rendering-method forward_plus --rendering-driver vulkan --script res://tests/living_world_performance.gd -- --render-only
"$GODOT" --headless --path . --script res://tests/living_world_performance.gd -- --simulation-only
bash tools/test_beta.sh
python3 tools/package_beta.py --output build/MET_Living_World_V1_Source.zip
```

原生命令要求 1920×1080 桌面 display；云环境软件 ICD 的路径与启动方法保存在 setup 草稿。源码 ZIP 的成员 SHA/CRC 以及独立提取导入、原生 Hero 启动和 GL 默认启动由交付包验证 JSON 记录；不是原生 macOS/Windows 安装包。证据 ZIP 包含原始 PNG、JSON、命令、源码指纹和日志；不把 headless timing 当画面验收。
