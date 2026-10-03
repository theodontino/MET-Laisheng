> 此文记录上一轮 Visual 版本。当前源码与新包已更新为来生版，参见 [来生版说明](LAISHENG_041.md) 和 [本轮验收](LAISHENG_ACCEPTANCE_041.md)；下文旧结果不代表本轮验证。

# MET 0.4 视觉与高程重做

本轮版本为 `0.4.0-prealpha.2`，保留 0.4 玩法。界面、世界渲染与素材已接入现有游戏，不是单独的界面演示。District 与跨版本存档迁移不在本轮。

## 视觉实现

- 明亮河谷搭配墨蓝悬浮面板、古铜金主操作，资源使用固定类别色；Noto Sans SC 中文字体随包分发，正文400、按钮与关键数值600。
- 顶部预算、人口和资源；底部建筑缩略图；424–440逻辑像素建筑详情；300逻辑像素运营商比较台。管理窗口有最大宽度，预算概览按内容收高。
- 升级预览展示前后模型、成本、收益与损失，维护费和用电增加归为代价，底部只保留一个执行操作。
- 21个建筑节点由 Blender 4.5.1 LTS / EEVEE Next 预渲染，按约41.35°的同一正交相机和光照生成独立模型。T1→T3由体块、楼层、院落与设备变化表达；阴影与方案挂件分开绘制。
- 原有地图着色器重复乘贴图颜色的问题已修正；界面缩略图按alpha边界裁切，地图仍使用完整贴图与接地点。

## 真实高程

高地由连续高度场形成，不用石头模型代替。hill 区域由相连的不规则长山脊与平缓顶部组成，草地、裸露坡面和河岸按高度与法线着色。地图外围平滑回落，避免远处突然截断。高程仅改变呈现，不增加通行或建造规则。

地面使用缓存 ArrayMesh，8世界单位采样，并叠加固定世界坐标的细微材质纹理。地面纵轴压缩0.661，高程投影0.751，与 Blender 相机 `(9,-12,13.2)` 对齐。建筑、地块边线、拾取多边形、道路和地景共用坐标变换；屋顶按透明度与实际绘制顺序命中。道路比原版更细；河道沿水格中心形成连续曲线，岸带与河槽单独绘制，并在可建设地图之外延续。水格的建造边界仍由原规则决定，选择图层显示完整边界。

建筑透明剪影投射方向阴影，并保留柔和接地阴影；两者与本体、方案挂件分开。树林与田垄从地格、种子及展示快照派生，地图外景仅用于衬托河谷。高程与装饰不消耗模拟随机数，不改变经济实体或库存。旧 hill_0/1/2 石头资产仍为未启用素材。

用户后续将《文明 VI》提升为主要参考。本轮采用更清楚的色彩/轮廓、较低观察角度、密集植被、连续山脊与克制的墨蓝古铜界面；T2住宅改为多翼庭院，T3为主塔与两侧翼。参考为[官方美术访谈](https://store.steampowered.com/news/posts/?appids=289070&enddate=1468936810)与[官方商店画廊](https://store.steampowered.com/app/289070/Civilization_VI/)。这是一套本项目原创几何与材质，未提取或打包文明系列游戏资产。原石墨灰/同心阶地样稿已被本轮替代，其过程证据不代表新版本验收。

## 复现

```sh
export GODOT=/path/to/Godot.app/Contents/MacOS/Godot
export MET_EVIDENCE_DIR="$PWD/evidence040_visual/local"
"$GODOT" --headless --path . --script tests/elevation040.gd
"$GODOT" --headless --path . --script tests/run040.gd
"$GODOT" --headless --path . --script tests/playthrough040.gd
"$GODOT" --path . -- --ui040
"$GODOT" --path . -- --visual040
python3 tools/package_visual040.py --godot "$GODOT"
```

`--ui040` 检查UI操作回调；`--visual040` 使用明确的视觉测试城市，覆盖21节点、地块与屋顶命中、1440×900/1920×1080/1024×640及150%字体。画面由本机原生Godot渲染，Retina framebuffer为逻辑尺寸的2倍。它们不能替代实际鼠标操作或Windows实机验收。

新包名包含 `Visual`，与旧包分开。打包器使用官方Godot资源导出器生成完整的新PCK，并复用此前匹配4.7.2的官方平台引擎；Mac重新生成并验证本地ad-hoc签名，未公证。Windows为x64 EXE+同名PCK，必须一起解压运行。字体许可证单独置于包内。各平台实际验收结果见 `VISUAL_ACCEPTANCE_040.md`。

源码与美术目录：`ui/map/elevation040.gd`、`ui/map_view.gd`、`ui/map/art.gd`、`content/presentation040/`。Blender生成方法与源场景见 [ART_040.md](ART_040.md)。
