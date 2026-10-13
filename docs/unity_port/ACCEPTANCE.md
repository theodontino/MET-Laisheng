# Unity Hero Valley 移植验收状态

本文是最初只读 Hero 切片的历史验收记录。完整迁移已于 2026-10-13 获授权，Unity Hub 已登录、6.6 已安装；新核心与 Unity 原生运行的结果另记 [FULL_MIGRATION.md](FULL_MIGRATION.md)，不替换或重标本页旧证据。

**本轮完成源码移植和标准 GLB/JSON 桥接；Unity 运行、截图、性能和 macOS 包尚未验收。** 用户确认没有 Unity 账号或已激活环境。当前云环境也没有已激活的 Unity Editor，编辑器容器下载尝试遇到磁盘空间不足。现有证据不能据此比较 Godot 与 Unity 的性能或制作效率。

## 已实际执行的检查

使用现有 **Godot 4.7.2 Standard**，在独立 XDG 状态目录内串行执行 editor import、`tests/unity_hero_export.gd` 和正式导出器。测试结果为 **36/36**，零失败；结果的可追踪副本见 [export-validation.json](evidence/export-validation.json)。该副本只删除测试输出的绝对目录，保留实际 checks、文件哈希、模型计数、边界、相机、环境和隔离摘要；没有用输出时间或构建耗时伪装确定性。

| 实际检查 | 结果与边界 |
| --- | --- |
| 两次完整导出 | `world.glb`、`foliage.glb`、`hero_valley.json` 三文件逐字节相同；正式资源与两次测试输出也相同 |
| 真实 fixture | world seed `905003`、visual seed `314159`，37 Hex、4 栋建筑、29 棵树 |
| 完整 Gameplay 隔离 | 导出前后 encoded state、RNG、wallet、compiled、revision 摘要相同，detached snapshot 摘要也相同；导出器未调用玩家存档读写 |
| 官方 GLB 回读 | 两个输出均通过 Godot `GLTFDocument` 回读；实际 triangle、mesh nodes 与 indexed bounds 保持一致 |
| 材质 | 回读得到标准 lit PBR，无 Godot Shader、内嵌 Camera 或 Light；GLB 包含基色顶点属性 |
| 几何 | 实际顶点、normal、clockwise winding 有效；80 个建筑底部采样点在导出后实际地形/platform triangles 上得到支撑，容差 `0.01` 世界单位 |
| RH/LH 边界 | GLB/JSON 使用 RH Y-up，sidecar 明示 Unity 镜像 X 一次及 quaternion 转换；Unity 实际导入对应关系仍待执行 |

此测试在 Godot 中对官方导出与回读做实际检查，**没有运行 Unity Editor 或 Unity Player**。它验证的是标准输出和边界，不是 Unity API、URP Shader 或原生运行成功。

此外，Python 便携验证实际读取两个 GLB 的二进制 accessor、索引、normals、lit PBR、COLOR_0，核对导出哈希与 19 个来源文件 SHA256；结果见 [portable-validation.json](evidence/portable-validation.json)。独立源码 ZIP 已经过 CRC／逐文件 SHA256 检查和另一目录解压后的同一便携验证；这不执行 Unity。

本轮重新执行原 Godot `tools/test_beta.sh`，**401/401 核心检查、6/6 完整路线**通过；摘要见 [godot-regression.json](evidence/godot-regression.json)。默认 Godot 2D 入口另在隔离 XDG 目录完成 120 帧 headless 启动，退出正常，无脚本／资源错误。没有修改 `core/`、原 `ui/`、`project.godot` 或旧 2D renderer。headless 启动不等于 Mac 真机或本轮图形验收。

独立 Astra 审查发现并修复了首次 GLB／URP 导入顺序、Player overlay shader 引用、环境颜色二次转换、截图失败留下通过记录、相机移动未跨帧和 Mac batch 切换 target 等问题。采用先保存 URP、下一进程／脚本域强制重导入；运行证据使用独立目录并在所有请求完成后才写 passed。源码复审通过不代表实际 Editor 行为已验证。`m_InstancingStripping` 的 native serialized property 类型与 Keep All 标签仍须在实际 Editor 确认；代码会对不支持的字段／标签明确失败，没有猜写数值或套用另一个 BRG enum。

## 固定切片与资源摘要

快照签名：

```text
2de4ddd315c6eacc13b76d5d3458d79f8b77d3281e2513e57b1914dab12f3827
```

| 内容 | 固定数量 |
| --- | ---: |
| 逻辑 Hex / 现有建筑 / 树实例 | 37 / 4 / 29 |
| 树共享材质批次 / 单树 triangles | 6 / 1,892 |
| world triangles | 110,408 |
| 源 world mesh nodes / surfaces | 21 / 85 |
| 地表 / 外沿 / foundation triangles | 13,819 / 690 / 142 |
| 连续河流 / 建筑 / 道路 triangles | 1,581 / 94,032 / 144 |
| Foundation meshes / 真实道路关系 | 8 / 2 |

树的总几何工作量为 `1,892 × 29 = 54,868` triangles，原型 GLB 只保存一份。Unity glTFast 可以按 primitives 分拆 MeshFilter，不能把源节点计数当成导入后 renderer 数量的硬性相等要求。

正式资源位于 `unity/METLivingWorld/Assets/Resources/Hero/`：

| 文件 | SHA256 |
| --- | --- |
| `world.glb` | `b3c23f3b7521c4d39a9efd597955d18f26656808b66bc495e4fecd36a334f967` |
| `foliage.glb` | `77d4823d28d98e429e205668ecc85ffef1d403af6b5a399daca7208d2cbcd9cc` |
| `hero_valley.json` | `17b6e0efc294062e7a0ae563f01b5fefa1c1c23f58fdd1501854fe6b92d13a31` |

ALBEDO 是原 Shader 在实际顶点位置的线性基色取样；片元微噪声近似，水 roughness 固定 `0.50`。没有烘焙方向光。这些材质转换差异必须在后续对照中公开，不能把不同材质造成的画面差异全部归因于引擎。

## 视觉与性能证据界限

既有 Godot V1 的 [1920×1080 2D/3D 对照](../../build/MET_Living_World_V1_Visual.html)、[3D 基线 PNG](../../build/MET_Living_World_V1_3D.png)、[2D 基线 PNG](../../build/MET_Living_World_V1_2D.png)以及 [Living World 验收记录](../living_world/ACCEPTANCE.md)仍可用于原实现参考。这些是 **Godot 截图**；本轮没有新增或冒称 Unity 截图。`build/` 是本地交付目录，源码仓库或其他 checkout 中可能不包含这些历史附件，应按原交付包获取。

本轮没有测量 Unity 帧时间、Unity import/场景构建 CPU、内存、GPU 或 macOS 启动性能，也没有用旧软件 Vulkan 数据替代 Unity 结果。未产出 Unity macOS `.app`，未完成签名、公证或真机启动验证。源码存在不代表可执行包已验收，Godot 原游戏的 Mac 包也不能作为 Unity 移植包。

补充安装事实：尝试从社区 GameCI Editor layer 选择性提取真实 API／Mono／Roslyn，首遍完整压缩流 SHA256 校验成功，但工具路径检查错误导致提取目录清理；修正后的重试因持续约 1.1 MiB/s 下载在预算内停止。未留下可用的编译工具，**本轮 C# 编译也未执行**。不能用容器 manifest、官方 API 源码审查或包版本声明替代编译结果。没有尝试绕过 Unity 授权。

## 待完成的验收

1. 在用户自行注册并激活的 Unity 环境中解析官方依赖、导入两个 GLB、编译工程和 URP 材质；不要在聊天或 GitHub 中提交账号凭据或许可证文件。
2. 检查相机、太阳方向、sRGB/linear 基色、镜像转换、树实例与 80 点落地采样，实际运行观察交互。
3. 保存无 UI 的固定 1920×1080 Unity 截图；与同输入的 Godot 3D 及旧 2D 基线并列，公开基色近似和渲染设置差异。
4. 在目标 Mac 上分别记录 renderer 帧时间、CPU 场景更新、内存，以及独立的 Simulation 成本；不能用总体长 Tick 代替 GPU 测量。
5. 构建并验收 Unity macOS `.app` 后，按现有 standing authorization 上传对应 GitHub prerelease，注明版本、来源提交、签名和真机验证状态。
6. 完成一次相同的河岸或光照修改，比较实际制作工作量，再决定是否迁移完整玩法。当前不增加天气、昼夜、多 biome 或额外玩法。
