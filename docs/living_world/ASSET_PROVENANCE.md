# Living World V1 资产来源与导出

本批全部为 MET 项目自制的参数化几何，没有下载第三方素材，也没有使用参考游戏的网格、材质或贴图。四栋建筑直接调用既有 `tools/make_art040.py` 中的农居、邻里中心、作坊、住宅构造函数；一种河谷原生蕨树调用同一生成器的植物几何。没有重新设计其余 17 个建筑。

## 原始作品与许可口径

Manifest 的 `LicenseRef-MET-Project-Original` 表示本项目原始作品：可随本次获得授权的 MET 源码仓库和游戏运行资产交付。它不是第三方 CC0 声明，也不是新授予任意第三方的开源美术许可证。当前仓库没有总许可证；本任务不替项目所有者重新授权其历史作品。对外独立复用、许可或出售的权利，仍由项目所有者另行决定。`attribution_required: false` 表示本批没有第三方署名义务，不表示作品属于公共领域。

`author` 记录项目和程序化制作方式，`source_pack` 记录项目原始生成器，`source_url` 指向本项目仓库。由于没有外部下载，`download_date` 为本批原始资产导出日期，并由 `download_date_note` 明示。每项记录可编辑 `.blend` 的 SHA-256、运行 GLB 的 SHA-256、生成器及其依赖的 SHA-256。

## 坐标、尺度与绑定

旧资源以 Blender XY 为地面、Z 为高度。运行 GLB 转为 `[x,z,-y]` 的 Godot/glTF Y-up 坐标，统一乘 **0.055**，直接烘入顶点；根节点与 registry 的 scale 都为 `[1,1,1]`，pivot 为原始地面原点 `[0,0,0]`。新世界 Hex 半径为 1，现有 elevation/foundation 的地图坐标另按 `/64` 适配。建筑不以每栋独立缩放掩盖真实尺寸差异。

运行 footprint、bounds、connector anchors 使用同一坐标转换。Manifest 另保留 `legacy_binding` 中的原 logical asset ID、definition ID、原 footprint、world bounds 和连接锚点，供追溯与旧 2D A/B。logical ID 与运行路径独立，例如 `building.homestead.t1` 对应 `assets/world3d/buildings/homestead.glb`。

## 管线

在仓库根目录运行：

```bash
blender --background --python-exit-code 1 --python tools/make_art040.py -- --runtime3d
python tools/validate_assets.py --expected-count 5
```

也可以直接以 Blender 执行 `tools/make_world3d_assets.py`。本批实际使用 Blender 4.3.2；旧 PNG 历史管线曾使用 4.5.1，不能将二者冒称为同一导出结果。默认、`--all`、`--preview` 等旧模式继续走原 PNG 逻辑；`--runtime3d` 是独立模式，不渲染或覆盖 `assets/world040` 与 `content/presentation040/art_catalog.json`。

源场景保留可编辑组件；运行导出先求值现有 bevel/normal modifier、烘入变换，再合为一个网格的多材质面。GLB 只包含网格和原始 metallic-roughness PBR 材质：没有 PNG、灯光、相机、烘焙方向光、烘焙 AO、动画或玩法对象。植被实例系统读取树网格，按稳定视觉种子放置，不能为树创造 Gameplay Entity。

同一种蕨树保留原叶片、树干、细枝位置与轮廓，将细枝圆柱改为无倒角五边截面。先导原型 8,932 三角形中大多数来自地图观察时不可见的细枝倒角；简化后单树低于 2,000 三角形，避免从第一版就把这些细节乘上森林实例数。建筑仍保留现有几何和倒角，不新增多级 LOD 系统。

运行树使用独立 `met.valley_foliage_pbr.v1` 材质档案：原生绿叶略加深、提高饱和度，并将 roughness 设为 0.90，使固定白昼和 filmic 色调映射下的树冠更易辨认。这是实时 PBR 的 base color 校色，没有烘焙灯光或新增 specular 扩展。生成器仅在构造该树时临时覆写 `green / green_dark / leaf / leaf_light / leaf_dark`，以 `try/finally` 恢复既有调色板；建筑与旧 PNG 的调色板保持原样。具体 sRGB 色值记录在 manifest 的 foliage profile；源场景、GLB extras 与 registry 使用同一 profile ID。

## 目录与再分发边界

- `art_source/original/world3d/`：本项目自制、可随项目分发的可编辑源场景。
- `art_source/third_party_cc0/`：未来明确登记 CC0 且允许源码分发的素材；本轮为空。
- `art_source/vendor_local/`：私有或禁止再分发的原始包。Git 忽略和源码打包器均排除该目录；不能把其中内容挪进可分发源目录绕过限制。
- `assets/world3d/`：已登记 provenance 与 runtime hash 的运行资产。

第三方“可商用”不等于允许放入源码仓库。未来引入资产时，必须分别确认 source 与 runtime 再分发权，并登记许可证原文、下载日期和哈希；未明确的资产不进入正式 registry。既有引擎/字体等第三方声明继续保留在 `assets/licenses/THIRD-PARTY-NOTICES.txt`。
