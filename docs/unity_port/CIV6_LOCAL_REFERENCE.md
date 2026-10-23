# Civilization VI local visual reference

## 给 MET 的直接结论

本机安装确实保留了可读的地图生成 Lua、地形/城市/道路美术定义和中文 UI 样式，足够研究它的组织方式。这里已经完成的是对这些原始文件的只读分析；完整渲染器、shader 和网格拼接算法没有因此被还原。

最值得借鉴的是**“人工设计的形状与规则库，由程序组合成自然的整体”**。程序负责位置、邻接、阶段和变化，美术负责树冠、山体、区域地标及配套小建筑的统一形态。下面左列有本机文件证据，右列是对 MET 的实施判断。

| Civ6 可确认的做法 | 对 MET 的意义 |
| --- | --- |
| 生物群系与平地/丘陵/山地分别定义；山体另有基部、顶部和积雪材料。 | 地表颜色、坡面露岩和山体轮廓要分开调；提高贴图尺寸不能单独解决轮廓平庸。 |
| 森林包含单树、树丛、灌木、石块和地面贴花，并裁开河岸及建筑占地。 | 先读成一片林冠，再在边缘提供细节。当前 MET 已换用免费多树型、灌木与岩体；整体树冠与地貌构图仍需实际画面迭代。 |
| 地图生成按相邻同类地块调整森林/雨林出现概率，另有覆盖率与纬度条件。 | 连片分布来自地图层规则；一格里面怎么摆树属于另一层。两层都要可复现，不能用全图均匀撒点代替。 |
| 城市生成器有中心区块、骨架尺度、人口成长阶段、填充占比和时代分布。 | 保持区域地标与主建筑的身份，以真实人口/使用率驱动围绕街巷的填充与轮廓变化。MET 已有状态驱动生长和邻接连接，但资产组合的连贯性仍落后于目标。 |
| 道路具有段、过渡、末端淡出及正常/受损状态；建筑有依地形和沿河的放置策略。 | 道路交会、入口、庭院和地基应共同设计。文件并未公开它的精确交叉口算法，不能把字段名说成已掌握底层实现。 |
| 中文字号样式与资源语义色集中定义。 | 使用可读字重、稳定的数字层级和统一资源颜色；必须在实际 1080p Player 中确认，而非只检查 UI 配置。 |

本轮不会把 Civ6 的模型、贴图或其程序文件装进 MET。项目已有免费 CC0 资源与原创草/岩材质，参考所得落实为自己的生成和表现规则。

## Inspection scope

Read-only inspection of the installed Civilization VI assets on 2026-10-14. This note records text-visible structure and design ideas for MET; it contains no game assets or copied source blocks. No game or binary asset extractor was run.

Installed asset root:

`/Users/lizhirui/Library/Application Support/Steam/steamapps/common/Sid Meier's Civilization VI/Civ6.app/Contents/Assets/Base`

## Confirmed in readable files

- **City growth is a separate procedural presentation system.** `ArtDefs/CityGenerators.artdef` declares a `HEX_SPINED` city generator, a center block list, spine width/length ranges, and `GrowthStage` records whose fields include population, city area, filler occupancy/ratio, and scatter grouping. For example, the records named `1` and `22` declare population 1 / area 12 / occupancy 0.05 / filler ratio 0.75, and population 22 / area 48 / occupancy 1.0 / filler ratio 0.3. The XML shows tunable stages and era distributions, not the generator's runtime algorithm.
- **City silhouette has named era assignments.** `ArtDefs/Cities.artdef` maps city looks such as Ancient, Medieval, Industrial, and Modern to era IDs. `CityGenerators.artdef` also has era distribution entries for its generated city blocks. This supports discrete visual shifts as the city grows through eras; it does not prove each individual structure upgrades in place.
- **District and building identity is linked to separate landmark art.** `ArtDefs/Districts.artdef` maps each district ID (for example, City Center and Campus) to a landmark reference in `Landmarks.artdef`, and separately links strategic-view symbols and completed, pillaged, and under-construction states. The actual landmark geometry is referenced through asset data and is not exposed as readable mesh layout in these declarations.
- **Buildings expose placement and terrain-follow modes.** `ArtDefs/Buildings.artdef` declares `TerrainFollowMode` values including `AVERAGE`, `MAXIMUM`, `PIVOT`, and `NONE`, plus `ProceduralPlacementMode`. Most inspected buildings use `NONE`; `BUILDING_WATER_MILL` is explicitly `RIVERSIDE` and `MAXIMUM`. These names prove that placement/terrain policies are modeled per building, but not the engine's exact height-fitting behavior. Buildings also refer to district building sets and have state-specific strategic-view references.
- **Routes are authored as connected route descriptions.** `ArtDefs/Routes.artdef` maps route types to world-view route data. `ArtDefs/WorldViewRoutes.artdef` contains route pieces with UV rectangles and named material package references, plus route descriptions with width/blocker width and route-segment records. The visible segment categories include `TILED_PATH`, `TRANSITION`, and `FADEOUT`; states include `NORMAL` and `PILLAGED`. Descriptions layer route pieces for different eras, such as Ancient plus Classical or Industrial plus Modern, and have separate pillaged variants. This confirms a segment/state/material library for route presentation; exact directional junction geometry is in packaged data or engine logic.
- **River material is distinct from roads and coast.** `ArtDefs/Water.artdef` references separate `Water/River`, `Water/Coast`, `Water/Lake`, and `Water/Deep` materials; its secondary river material/mask slots are empty in this install. River material is therefore a declared part of shared water presentation. These declarations do not show how river paths are joined across neighboring map cells.
- **Terrain materials are organized across biome and landform.** `ArtDefs/Terrains.artdef` declares grass, plains, desert, tundra, and snow entries with separate hill and mountain variants. `ArtDefs/TerrainStyle.artdef` assigns the standard hill materials by biome; its ridge-mountain profile references distinct base, low-top, and snow-top materials, with height 32, width about 42.4, snow thresholds 24/26, and five named mountain-piece variants. The declarations show a layered biome-by-landform scheme. They do not expose shader-level texture blending.
- **Forest is a layered prop set with terrain clipping.** In `ArtDefs/Clutter.artdef`, `CLUTTER_FOREST` declares `Density=1.0`, `EdgeFalloff=0.01`, `ClipLow=-200`, `ClipHigh=10`, `TerrainHeight=true`, and `ClipRiver`, `ClipBuildings`, `ClipCoastline` all true. Its plant entries include three pine asset variants at scale `0.5`, scale variation `0.1`, and declared counts 7, 7, and 8; two pine-clump entries have count 9, and the shrub entries have count 3 each. The art data also declares Boulder-clump and dirt-decal entries. These are authored parameters; their exact runtime meaning and on-screen density are engine-dependent.
- **Forest placement algorithm encourages local grouping.** `Assets/Maps/Utility/FeatureGenerator.lua` starts with a 300 placement score, adds weight for one to three adjacent same-feature plots, then subtracts weight at four or more; it also enforces a global maximum percentage. Jungle has an additional latitude band. This is map generation logic, separate from the render-time clutter layers.
- **CJK text styles and semantic colors are explicit.** `Assets/UI/Fonts/Civ6_FontStyles_zh_Hans_CN.xml` assigns Simplified Chinese styles to Noto Sans CJK SC with named sizes from 8 to 50; `FontNormal17` uses size 18, with a comment noting a Chinese rendering adjustment. `Assets/UI/Colors/Colors.xml` maps yield roles to shared palette tokens: science blue, energy yellow, production orange, food green, and culture magenta. `Assets/UI/TopPanel.xml` uses named font styles and semantic ColorSet names for values. These files confirm reusable size styles and role-based colors, but do not establish rendered pixel sizes or accessibility contrast.

## Design ideas for MET (inference)

- Keep the land surface continuous across cells and let cell ownership/material weights vary over that surface. Let roads and rivers follow neighbor/edge connectivity so paths read as uninterrupted networks, while buildings stay snapped to stable cell or district anchors.
- Treat a district as a coherent neighborhood footprint with a small set of attachment points. Add buildings as connected stages around that footprint; use shared yard/road/terrain clipping so additions visually merge instead of appearing as isolated tile stickers.
- Use a few curated growth stages with increasing built coverage and changing silhouette. Keep signature landmarks stable while secondary fillers change density and era style. This is a MET design suggestion inspired by the separate city-generator stages and era distributions, not a claim that Civ VI performs incremental construction simulation.
- For forest depth, use a small mix of individual tree silhouettes, pre-grouped canopy clumps, low shrubs, ground patches and a few rocks. Let the forest footprint mask clip at rivers, shorelines and buildings; keep an overview-scale simplified representation.
- Give each MET cell a biome material family and let landform choose its flat, hill, or mountain treatment; use continuous blending across shared edges and reserve snow treatment for appropriate elevation and biome combinations.
- Define semantic color roles and CJK-aware text styles rather than styling each widget independently. Choose sizes by information hierarchy and inspect the actual Unity render at the supported desktop baseline.
- Make terrain adaptation explicit per structure class (average surface, highest point, pivot, or fixed) and use a riverside placement rule only for structures that need it. Validate those behaviors in MET's Unity scene rather than copying the meaning of Civ's enum names.

## 2026-10-14 visual comparison follow-up

The original environment artist's [tile concept portfolio](https://beasty.artstation.com/projects/96vdQ?album_id=99825) describes blocking out tiles before painting with their individual parts in mind. This supports an authored component workflow, not a claim about Firaxis's runtime algorithms. The [2016 in-game press screenshot](https://www.4gamer.net/games/342/G034279/20160628014/SS/003.jpg) was also opened at full image size for comparison with MET visual.9; it is reference material only and is not included in the game.

The visual comparison points to distinct silhouettes and spatial composition, beyond texture resolution: recognizable exposed rock planes with grassy footslopes, color separation between forest and grassland, and districts whose open ground is shaped around their uses. MET visual.9 still repeats nearly square courtyards and small green pads; its smooth hill crests and broad ground mottling weaken those larger shapes. The next candidate keeps the user-selected 74% / 86% building scale, differentiates partial-use forecourts, removes gratuitous open-node geometry, and sharpens connected ridge profiles. These are MET art-direction choices inferred from the comparison, not copied geometry, materials, or code.

## Building material follow-up

The official [The Art of Civilization VI interview](https://store.steampowered.com/news/posts/?appids=289070&enddate=1469109608) with art director Brian Busatti explicitly ties color, form and silhouette to recognizing buildings at a distance. His discussion of natural materials and lighting supports separating large material roles before adding small surface detail. The original artist's [Portuguese Palace portfolio](https://www.artstation.com/artwork/PoD2Yy) identifies authored modeling/texturing from Firaxis concepts, with some existing stone-base and vegetation components reused. The warm walls, cooler roofs and restrained accent hierarchy noted in that portfolio are visual interpretation, not a published engine recipe.

Inspection of MET's actual Kenney GLBs found that most use one `colormap` material and a shared 512-pixel palette atlas for walls, roof and glass. Multiplying the entire material by gray therefore suppresses all these roles together. The visual.13 candidate keeps those source models and licensed textures unchanged, and instead develops cached presentation-only palette variants. This is an adaptation of the existing CC0 resources, not an imported Civ VI asset.

## Limits of the local text inspection

The full 3D building/district/road meshes, material shader implementation, route junction selection, river geometry construction, actual texture dimensions, and exact runtime interpretation of clutter counts are stored in packed proprietary asset data or engine code. The readable `.artdef` references are sufficient to identify authoring concepts and named options, not to reproduce Civ VI's implementation or establish the final rendered appearance. No proprietary data is included in MET by this research.

## Key local references

- `ArtDefs/CityGenerators.artdef`, `Cities.artdef`, `Eras.artdef`
- `ArtDefs/Districts.artdef`, `Buildings.artdef`, `Landmarks.artdef`
- `ArtDefs/Routes.artdef`, `WorldViewRoutes.artdef`, `Water.artdef`
- `ArtDefs/Terrains.artdef`, `TerrainStyle.artdef`
- `ArtDefs/Clutter.artdef`
- `Assets/Maps/Utility/FeatureGenerator.lua`
- `Assets/UI/Fonts/Civ6_FontStyles_zh_Hans_CN.xml`, `Assets/UI/Colors/Colors.xml`, `Assets/UI/TopPanel.xml`
