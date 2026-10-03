# 本地运行与导出

## 当前视觉版 pre-alpha.2

新版包含连续高程、Blender 建筑和随包中文字体。运行与检查见 [VISUAL_040.md](VISUAL_040.md)，本轮结果见 [VISUAL_ACCEPTANCE_040.md](VISUAL_ACCEPTANCE_040.md)。

```sh
python3 tools/package_visual040.py --godot "$GODOT"
python3 tools/package_source040.py
```

`package_visual040.py` 调用 Godot 官方 `--export-pack` 分别生成 Mac / Windows PCK，复用 `build/MET · 河谷新章.app` 与 `build/MET_0.4.0_Windows_x64.zip` 内匹配 4.7.2 的平台引擎。可通过 `--mac-runtime` / `--windows-runtime` 指向其他本地匹配运行时（参数以 `--help` 为准）。它替换完整资源包并重新签名 Mac 应用，保留旧包，输出名称带 `Visual`。源码包工具包括 Blender 源场景、当前文档和本轮 JSON/log，不收录缓存、旧快照或玩家存档。

源码 ZIP 不自带引擎二进制。独立重建应先安装 Godot 4.7.2 及匹配模板，用下面的标准导出方法生成运行时，或向打包器传入已有对应运行时。

## 标准平台导出与原 0.4 构建

使用 Godot 4.7.2 Standard 打开根目录 `project.godot`。macOS 导出需要匹配版本的官方 export templates；导出预设保留通用双架构。

```sh
export GODOT=/path/to/Godot.app/Contents/MacOS/Godot
export MET_EVIDENCE_DIR="$PWD/evidence040/local"
"$GODOT" --headless --path . --script tests/run040.gd
"$GODOT" --headless --path . --script tests/playthrough040.gd
"$GODOT" --path . -- --ui040
python3 tools/export040.py
```

`run040` 使用专门构造的核心验收状态；`playthrough040` 从新局走实际招标、施工、委托和交付路径，不调用调试补给；图形测试使用专用运行参数，避免读写默认玩家档。结果在各自 JSON/log 中记录，不能把任意一种替代另外一种。

导出脚本在 macOS 上运行，使用已安装的匹配模板；也可设置 `MET_MACOS_TEMPLATE=/path/to/macos.zip` 指向官方模板。脚本把 Godot 许可证与第三方通知放入 `.app/Contents/Resources/`，重新生成并验证 ad-hoc 签名，再生成 ZIP。它不进行 Apple 公证或网络发布。

Windows x64 导出使用同一 Godot 4.7.2 的 `windows_release_x86_64.exe` 模板：

```sh
export GODOT=/path/to/Godot.app/Contents/MacOS/Godot
export MET_WINDOWS_TEMPLATE=/path/to/windows_release_x86_64.exe
python3 tools/export040_windows.py
```

Windows 导出脚本也可使用已安装的匹配模板，此时省略 `MET_WINDOWS_TEMPLATE`。交付 ZIP 含 EXE、同名 PCK、运行说明和引擎许可证通知。用户应解压整个文件夹后运行 EXE；本机 macOS 只验证了 ZIP、x64 PE 文件与 PCK 可独立加载，Windows 实机运行仍需在 Windows 上确认。

旧的 `--test` / `--test032` 包含已经退出 0.4 的付费抽 Bonus、三槽直建与旧任务契约，只适用于保留的 0.3.2 基线。0.4 以当前格式的新系统验收和完整路线为准；不再运行跨版本迁移验收。

当前存档路径是 `MET-RiverValley-040/river_valley_v5.json`。整个 pre-alpha 阶段不考虑跨版本兼容，格式不符时开启新游戏。安装包内不包含测试证据、源码快照、旧补丁或开发文档。签名/公证和手机真机情况见测试报告。
