# 当前完整游戏迁移

2026-10-13 用户选择 Unity **6000.6.5f1**。当前完整游戏场景为 `Assets/Scenes/City.unity`，免费组件解析为 URP **17.6.0**、uGUI **2.6.0**、glTFast **6.20.0**、Newtonsoft JSON **3.2.1**、嵌入式 XCharts **3.15.0**。XCharts 的 EntityId 局部适配记录在包内 `MET_PATCHES.md`。

从仓库根目录运行 `bash tools/test_unity_city.sh editor`，分两个 Editor 进程准备 URP、再重新导入 GLB；`mac` 构建 universal `.app`；`native` 在独立测试存档路径中启动真实应用并生成自动操作与截图证据。`core` 执行 Godot/C# 完整状态对照。运行需要已合法激活的 Editor。当前完成情况见 [FULL_MIGRATION.md](FULL_MIGRATION.md)。

下文保留原 Hero 切片安装历史与命令，不是新的完整游戏入口。

## 本机 Unity 6.6 构建工具修复

Hub 安装的官方 Burst `llvm-lipo`缺少执行权限，安装目录不允许修改该文件。用 `python3 tools/prepare_unity_burst_runtime.py`在 `.toolcache`准备同一官方工具的副本，恢复本地副本的执行位；`test_unity_city.sh`检测该副本并仅对当前构建使用 `UNITY_BURST_RUNTIME_PATH`。已实际完成 AOT 通用包构建，没有禁用 Burst 或改系统保护。工具副本不进入 Git。

Unity 6.6 的实际 macOS 架构由 `OSXStandalone.UserBuildSettings.architecture = OSArchitecture.x64ARM64`指定；单独的 `PlayerSettings.SetArchitecture`不足以选择通用包。`mac`构建入口随后用 `tools/sign_unity_mac.py`进行本地产物 ad-hoc 签名与严格校验。

# Unity 移植开发环境

此目录的说明不代表已经配置成功。实际结果以 `ACCEPTANCE.md` 为准。

## 已可执行的入口

在 MET 根目录，Python 3 标准库即可验证便携资产的真实 accessor/index、三角形数、PBR、顶点颜色、哈希、来源及版本钉住：

```bash
bash tools/test_unity_port.sh portable
```

已校验 Godot 4.7.2 的环境可以运行导出回归。确保 `XDG_DATA_HOME`、`XDG_CONFIG_HOME`、`XDG_CACHE_HOME` 指向可写测试目录，并串行运行：

```bash
export GODOT=/absolute/path/to/Godot_v4.7.2-stable_linux.x86_64
bash tools/test_unity_port.sh export
```

该测试会重复导出、官方回读、检查实际网格落地和玩法状态隔离。它不执行 Unity。正式资源已冻结，普通运行无需重生成。

## Unity 授权与网络

用户已确认没有 Unity 账号或开发环境。需要先创建 Unity ID，通过 Unity Hub 的正常登录/授权流程取得符合资格的免费 Personal license。编辑器镜像存在不代表已获授权；不可伪造或绕过激活。账号密码、`.ulf` 和个人数据不进入源码仓库、日志或聊天。后续 CI 的凭据只通过安全设置配置，并以当期 Unity 官方授权流程为准。

使用 Unity 6000.0.58f2，macOS 构建需要 Mac Build Support（Mono）。当前环境两次拉取 GameCI 对应大镜像均因注册 layer 时磁盘空间不足失败；没有已安装、可运行的 Unity 编辑器。Dockerfile 的 MIT 许可不代替 Unity 编辑器许可。

当前云环境 Unity 安装/UPM 请求还存在网络限制：`packages.unity.com` 与 `services.api.unity.com` 实测 CONNECT 403。安装与包下载还可能需要 `download.unity3d.com`、`download.packages.unity.com`。环境草稿可登记精确域名；保存草稿不会更改当前实例的网络规则，也不会自动授权编辑器。Git HTTPS 访问可用，GitHub API 返回 Forbidden 是另一项权限限制，不应要求用户在聊天发送 token。

## 获得有效编辑器之后

```bash
export UNITY_EDITOR=/absolute/path/to/Unity
export MET_EVIDENCE_DIR=/absolute/path/to/fresh-unity-evidence
bash tools/test_unity_port.sh editor
bash tools/test_unity_port.sh play-validation
```

脚本先启动 `HeroProject.PrepareURP` 保存官方管线并退出，再由新编辑器进程强制重新导入 GLB，避免 glTFast 首次缓存 Built-in 材质。这两步必须保留。交互首次打开由 bootstrap 建立管线、重载脚本域后导入；自动生成场景不能代替实际验证。

真实 1920×1080 图形上下文下，串行执行：

```bash
bash tools/test_unity_port.sh native
```

截图和测量只在真实图形运行中请求。Editor batchmode 不支持本入口的 `WaitForEndOfFrame` 截图，代码会明确失败。软件 Vulkan/软件 OpenGL 不等于目标 GPU；帧间隔是渲染器、驱动和调度的整体壁钟间隔，不是 GPU 计时。此 Unity 工程没有 Simulation，不能用它证明城市模拟性能改善。已有 Godot 同机位 PNG 只能作为 A/B 左侧；右侧必须由 Unity 实际生成。

此 `native` 入口打开交互 Editor 的 Play Mode，Game View 必须实际设置为 1920×1080；PlayerSettings 的全屏设置不会自动改变 Editor Game View。尺寸不符会失败，不能将较小 Game View 截图放大冒充 1080p。最终 Mac Player 还需在目标机全屏原生运行并记录单独证据。

## Mac 包

```bash
export MET_UNITY_MAC_OUTPUT=/absolute/path/to/Build/MET-Living-World-Unity.app
bash tools/test_unity_port.sh mac
```

入口要求有效授权、macOS support，构建目标为 x64+arm64 universal Mono；BuildPipeline 非 Succeeded 必须失败。构建后还需核查实际 Mach-O 双架构、app bundle、权限，最好在 Mac 启动原生 Player，再进行签名/ZIP/GitHub prerelease 上传。macOS 可用 `ditto -c -k --keepParent` 保留 bundle 打 ZIP。Linux 源码打包不能冒充 `.app`，未经实际构建不得触发空发布或上传旧 Godot app 代替。

独立源码包：

```bash
python3 tools/package_unity_port.py --output build/MET_Unity_Hero_Valley_Source.zip
```

打包器只收录 Unity Assets/Packages/ProjectSettings、说明与登记的 MET 原资产来源；排除 Library、Temp、Logs、UserSettings、账号文件和许可证。验证 ZIP CRC/逐文件 SHA256 后，独立解压再次运行便携验证。该包无需原 Godot 工程来运行视觉切片；完整再生成需来源提交的 MET 仓库。

在独立 ZIP 解压后的工程根目录，使用随包工具：

```bash
python3 Tools/validate_unity_port.py --project . --portable-only
```

此模式检验便携 GLB/JSON 和固定依赖，不声称外部 MET 源码全部存在或 Unity 已运行。原仓库脚本入口与再生成命令不包含在独立 ZIP 中；完整版本在来源提交可得。
