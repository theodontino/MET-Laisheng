# MET 0.3.0｜UX与美术更新执行包 v1

这是基于B0.2.1源码与实际画面的**开发方案**。没有替用户改游戏、没有生成或打包新的Mac程序；也没有冒充已经做完的美术资源。

## 阅读顺序

先读根目录的 `MET_0.3.0_UX_Art_Development_Plan.md`：它是版本合同，含风格、页面、地图图层、触摸、工程落点、排期与交付边界。

美术制作看 `specs/02_ART_PRODUCTION.md` 与 `config/asset_manifest.json`；76项制作任务中只有30项需要AI视觉资产，其余是程序/矢量图标、徽记和轻量音效。

程序落地看 `config/ui_tokens.json`、`map_layers.json`、`bonus_visual_bindings.json` 与 `specs/04_DEVELOPMENT_BACKLOG.md`。配置是设计契约与样例，不是可直接拖进Godot就完成更新的插件。

交付检查用 `specs/03_ACCEPTANCE.md`。源码依据在 `specs/01_BASELINE_AUDIT.md`，`evidence/`是原0.2.1截图，**不是0.3.0效果图**。

`PLAN_VALIDATION.md`只记录本方案文件之间的一致性、ID、配色文本对比检查；不代表游戏运行、美术、手机版已经通过。

## 默认决定

沿用同一个Godot游戏。固定高俯视2.5D插画，连续地形、清楚建筑。平时看城市，分析看具体问题，投标比较看地图上的变化。Mac为主交付，同步做好横屏触摸路径；移动平台真机与上架另设证据门槛。

原模拟、内容、DT、槽形与城市保留，不以重做一个小实验替代这次更新。未改GitHub；不含字体文件。

- `PLAN_VALIDATION.md`：方案文件一致性检查，非游戏运行验收。
