# MET Hero Valley · Unity 移植切片

这是同一 MET 项目的固定视觉切片：37 Hex、连续河谷与河流、4 栋原有建筑、29 个共享树木实例、固定白昼和摄影。它不包含城市玩法模拟或完整 Beta UI；原 Godot 游戏保留。

**当前验收状态：**Godot 导出/回读、几何、确定性和模拟隔离已验证。Unity 的包解析、编辑器编译/导入、原生截图、帧测量和 Mac 构建需要实际授权环境；源码准备不能代替这些结果。完整状态见仓库 `docs/unity_port/ACCEPTANCE.md`，独立源码 ZIP 中为 `PortDocumentation/ACCEPTANCE.md`。

## 免费依赖

Unity **6000.0.58f2**；官方 URP **17.0.3**、glTFast **6.20.0**、Newtonsoft JSON **3.2.1**。使用 Unity Package Manager 解析；首次打开需联网。没有付费美术包，也没有手写新的 GLB 解析器。建筑与树为 MET 原有自制资产，仍为 `LicenseRef-MET-Project-Original`，不能称作 CC0。

Unity 编辑器本身需有效授权。符合 Unity Personal 当期资格的个人开发者可通过 Unity Hub 创建 Unity ID、登录并取得免费 Personal license；不必先买 Pro。不要把密码、许可证或账号数据写入项目或发送到聊天。

## 打开切片

1. 在已授权的 Unity Hub 中添加本目录，使用固定的 **6000.0.58f2**。需要 macOS 构建时安装对应 Mac Build Support（Mono）。
2. 等待 UPM 解析和资源导入。首次准备先建立 URP，再重载脚本域并重新导入 GLB；不能使用 Built-in 材质代替 URP。
3. 通过 `MET → Hero Valley` 菜单重建场景、验证导入几何，运行 `Assets/Scenes/HeroValley.unity`。
4. 中键/右键拖动，滚轮缩放，Home 回到固定机位，H 显示 Hex，F1 隐藏辅助文字。正常视图不显示 Hex。

场景与 URP 资源由官方 Editor API 创建。不要手写假场景 YAML，也不要将 Godot 截图充当 Unity 截图。默认桌面全屏 1920×1080，不安排移动端或不同窗口尺寸测试。

命令行入口、实际图形验证和 macOS universal `.app` 构建命令见仓库 `tools/test_unity_port.sh` 与 `docs/unity_port/SETUP.md`；源码包附带同一指南。编辑器几何验证和实际 Player 渲染是分别记录的两项验收。

## 数据边界

`Assets/Resources/Hero/world.glb`、`foliage.glb` 和 `hero_valley.json` 来自现有 Godot Hero fixture 的只读导出。glTFast 负责 glTF 坐标转换；JSON 坐标只镜像 X 一次。地表仅将原 shader 的基色近似为顶点颜色，统一实时光照仍由 Unity 负责；没有烘焙固定方向光。

此工程没有 Game、钱包、Gameplay RNG 或玩家存档接口。完整再生成需对应 MET 源码，运行切片只需要此独立 Unity 工程。源码包 `MET_PROVENANCE/` 保存原资产、登记及授权说明；`SOURCE_MANIFEST.json` 记录所有随包文件 SHA256 与 MET 来源提交。

独立源码 ZIP 自带 Python 验证器；在解压后的工程根目录运行 `python3 Tools/validate_unity_port.py --project . --portable-only`，无需原 Godot 工程。`--portable-only` 检查随包 GLB/JSON，原 MET 来源 SHA256 在完整仓库中另行核对。
