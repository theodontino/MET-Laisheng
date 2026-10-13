# MET 文明 VI 质感与 Unity 复用方案调研

调研日期：2026-10-13。价格为当日公开页面显示的美元税前价，促销与地区显示可能变化。

当前约束更新为 **素材与第三方插件预算 0**。优先使用下文的免费、可商用素材和开源组件，统一材质、光照与战略视角；付费组合保留为前次调研参考，不作为当前采购方案。文明 VI 的视觉目标继续有效，现成免费资产仍需要风格调整。

本次已查商品页、作者文档及原始作品，并实际检视文明 VI 官方截图、Dreamscape Village、Dreamscape Mountains 作者作品、FANTASTIC City Pack 和 Seaside Town 画廊。以下风格取舍是我的判断；商店兼容性是作者声明，尚未在 MET 中导入、编译或运行。游戏源码及现有验收记录未修改。

## 当前零预算方案

| 用途 | 免费候选 | 可直接复用的内容与限制 |
| --- | --- | --- |
| 树石与地表 | [Quaternius Ultimate Stylized Nature](https://quaternius.com/packs/ultimatestylizednature.html) | 63 个自然模型，含纹理、法线贴图及 FBX/glTF/Blend 格式；CC0，可商用。已检视作者示例，具有比纯色几何更丰富的植被与地表层次。需要给 Unity 配好材质和统一色板。 |
| 早期聚落 | [RG Poly 免费 Stylized Medieval Village](https://rg-poly.itch.io/stylized-medieval-village-unity-urp) | 210 模型/预制体、演示场景、水与草的 Shader Graph；素材 CC0、代码 MIT。页面提供自定价格下载，可选择 0。包目标 Unity 2022.3.6f1，Unity 6 中需实际导入检查；中世纪建筑作为可改造部件。 |
| 工业建筑体块 | [Kenney City Kit Industrial](https://kenney.nl/assets/city-kit-industrial) | 40 项工业相关资源，CC0、免费下载。可补工厂、仓储、能源的形体；风格较简洁，应统一材质与细节，而非原样当作最终画面。 |
| 居住与城市补充 | [Kenney City Kit Suburban](https://kenney.nl/assets/city-kit-suburban) | 40 项住宅相关资源，CC0、免费下载。适合补充通用建筑部件；仍需和现有 MET 建筑匹配比例与用途辨识。 |
| 水体 | [Uber Stylized Water](https://github.com/MatrixRex/Uber-Stylized-Water) | MIT 开源，作者声明 Unity 6000.0.30+ / URP，可作用于 Mesh。示例使用 URP 17.0.4，当前 MET 为 17.0.3；Mac/Metal和现有河面通道需验证。其依赖 [RexTools Unity6](https://github.com/MatrixRex/RexTools-Unity6) 也是 MIT 免费代码。 |
| 地图基础 | [Catlike Coding Hex Map](https://catlikecoding.com/unity/hex-map/) | 项目代码与资产 MIT-0，可选择性复用六角地形、河流、道路和摆放实现。最新项目版本高于 MET 当前编辑器，先选择合适版本或提取局部代码。 |

推荐先用 **RG Poly 聚落 + Quaternius 自然资产 + 开源水体**，配合现有 MET 建筑；Kenney 作为工业/住宅体块补充。素材与插件采购费用为 **0**。主要工作是合并色板、补材质、统一光照、重组街区和处理版本差异。仍以正常战略视角下的实际运行画面判断质感，不能仅凭候选列表保证达到文明 VI 的完整商业制作水平。

这条路线并不要求所有建筑重新制作，也不要求因素材格式而立即全量迁移引擎。Quaternius、Kenney 的通用模型可用于 Godot；Unity 的额外价值在于现成 URP 示例、Shader Graph 和可复用渲染代码。先在既有 Unity 河谷中检查效果与修改效率，再决定更广泛接入。

免费资源的下载范围要分清：Quaternius 新版 Medieval Village MegaKit 的免费版仅覆盖部分模型，完整模型和已配置引擎工程属于其他版本，不能把 304 个模型全部计为免费。本方案选的是上述旧版 Ultimate Stylized Nature 完整自然包。[作者版本说明](https://quaternius.com/packs/medievalvillagemegakit.html)

[Unity Fantasy Kingdom](https://unity.com/demos/fantasy-kingdom) 的免费示例限定非商业/教育用途，其素材不纳入准备销售的 MET。公开的 [Boat Attack](https://github.com/Unity-Technologies/BoatAttack) 可作为 Unity 水体源码参考，但主工程仍基于旧 URP，移植工作比上述 Unity 6 水体更大。当前优先候选已提供可商用素材与代码，无需为演示资源另购商业许可。

此次免费补充已核对作者页面和公开源码许可，实际查看了 RG Poly 与 Quaternius 的页面示例；未下载、安装或编译这些包。

## 免费 UI 与通用功能方案

用户进一步希望复用功能和 UI。现成资源确实包括可运行的页面、控件和通用系统，以下采购费用均可保持 0。当前 MET Unity 工程仍是固定河谷展示，`Packages/manifest.json` 尚未加入这些 UI、输入或存档包；这里记录候选，未完成接入。

### 界面、控件与图形

| 候选 | 能复用什么 | 对 MET 的判断与免费边界 |
| --- | --- | --- |
| [Dragon Crashers UI Toolkit 官方样例](https://assetstore.unity.com/packages/templates/packs/dragon-crashers-ui-toolkit-sample-project-231178) | 可运行的菜单、设置、标签页、库存页面、主题、数据绑定和本地化示例 | 最值得拆读的完整 UI 样板；将库存格改为 Bonus 库、角色信息改为建筑档案仍需业务适配。FREE；商品元数据列 Standard EULA，描述列 Unity Companion License，采用时按包内许可登记。可在相应许可下作为商业 Unity 游戏组件复用，不视为 CC0。最新 1.7 描述要求 Unity 6000.5.10f1 / URP 17.5；虽兼容表仍列旧 Unity 6.0，不能直接承诺适用于 MET 6000.0.58f2。 |
| [UI Toolkit / QuizU 官方资料](https://docs.unity.com/en-us/engine/6000.0/manual/best-practice-guides/bpg-uiad-index/install-ui-toolkit-and-sample-projects) | Unity 6 内置运行时 UI；UXML 布局、USS 样式；样例提供页面组织和交互模式 | 适合 HUD、建筑详情、管理页及长列表。样式和布局以文本保存，便于代码修改；这是对我们工作方式的选型判断。QuizU 为免费官方样例，实际使用须查看其包内许可。 |
| [uGUI / TextMesh Pro](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.ugui.html) | Canvas、布局、按钮、滚动区和清晰文本排版 | 社区控件、预制体和图表较多，可作为另一条主 UI 路线；地图气泡也可用。随 Unity 提供，无额外插件费用；字体资源另按具体字体授权选择。 |
| [UI Extensions](https://unity-ui-extensions.github.io/presskit/) | 分页、滚动吸附、折叠区、范围滑块及其他控件 | uGUI 包 3.0.0 列 101 控件、BSD-3-Clause；[UI Toolkit 包](https://github.com/Unity-UI-Extensions/com.unity.uitoolkitextensions) 1.0.0 列 25 控件、MIT，目标 Unity 6000.0。两者都可免费商用，按所选 UI 路线取对应包。Toolkit 分支较新，不把旧 uGUI 的积累当成新分支实测；免费 Git/OpenUPM 路径即可。 |
| [Kenney UI Pack](https://kenney.nl/assets/ui-pack) / [Game Icons](https://kenney.nl/assets/game-icons) | 面板、按钮、滑杆和通用图标图片 | CC0 免费商用，分别列 430 / 105 文件；用于构建统一视觉基础。属于美术部件，需要接入控件、字体、布局与交互，不提供完整管理系统。 |
| [XCharts 核心库](https://github.com/XCharts-Team/XCharts) | 折线、柱形、饼图等，支持动态数据、主题和提示框 | MIT 免费商用，基于 uGUI，适合人口/库存历史、供需与方案比较。仅选择免费核心；桑基、关系图、甘特、高级 UI 等扩展属于付费范围。作者声明 Unity 5.6+，当前 Unity 6 工程仍须编译验证。 |
| [Unity Localization](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.localization.html) | 文本与图片语言表、占位符、复数规则、CSV 导入导出 | 官方免费包，Unity 6.0 文档列 1.5.9；适合统一建筑、资源、按钮与事件文案，不代替翻译或中文字体。 |

Unity 官方样例的内容和许可均以下载包为准；[Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) 允许在有效 Unity 引擎许可下复用和分发相应作品，[Asset Store 条款](https://unity.com/legal/as-terms) 对嵌入原创产品的使用另有规定。这里选择的是商业 Unity 项目候选，不建议把 Unity 限定示例搬到 Godot。

### 通用系统与框架

| 候选 | 现成功能 | 接入范围 |
| --- | --- | --- |
| [QFramework 免费 Toolkits](https://github.com/liangxiegame/QFramework) | UIKit、ResKit、AudioKit、ActionKit，以及事件和架构工具 | MIT 免费商用；README 明确 Unity 2018.4～6.x，免费 Toolkits 已包括上述模块。可以评估界面管理、音频和资源加载；ToolkitsPro、课程和会员另算，不是采用免费版的前提。它提供程序结构与工具，不提供 MET 经济系统或最终皮肤。 |
| [Game Framework / UnityGameFramework](https://github.com/EllanJiang/UnityGameFramework) | 19 模块，包含流程、界面、音频、配置表、对象池、资源、场景和本地化 | MIT 免费商用，是更全面的基础框架备选。接入会影响项目流程和资源体系；本次未取得 Unity 6.0.58 的实际编译证据。与 QFramework 择需评估，不同时引入两套基础框架。 |
| [Input System](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.inputsystem.html) + [Cinemachine](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.cinemachine.html) | 输入动作、快捷键重绑定、设备管理、相机平滑和镜头过渡 | 官方免费包，Unity 6.0 文档提供发行版本；适合地图操作与聚焦建筑，仍要实现具体移动方式、弹窗焦点和地图边界规则。 |
| [DOTween Free](https://dotween.demigiant.com/download.php) | 缓动与动画序列，数值/位置/透明度过渡 | [免费版许可](https://dotween.demigiant.com/license.php) 允许商业游戏使用；Pro 编辑工具收费，不能误称 MIT。适合面板进出、提示和镜头过渡；代码编排不需 Pro。 |
| [Save Game Free](https://github.com/BayatGames/SaveGameFree) | 数据保存/读取与异步文件操作 | MIT，README 声明支持 Unity 6。可用于展示偏好或未来 Unity 存档的底层读写；MET 城市格式、版本迁移、待领奖励和随机状态仍需由 MET 定义。当前不会接管 Godot 城市存档。 |
| [AI Navigation](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.ai.navigation.html) | NavMesh、避障和路径移动 | 官方免费包，适合后续展示行人。MET 六角道路、物流、服务覆盖和真实结算规则继续独立；本轮没有新增行人功能。 |

上述官方输入、相机和导航包适用 Unity 项目的包内许可，无需另购插件。各包当前文档版本不等于旧 `.58f2` 编辑器已实际解析通过。

### 对 MET 的选型建议

若主要诉求是直接复用社区现成控件，我会先评估 **uGUI + QFramework 免费 UIKit + XCharts 免费核心 + DOTween Free**：前两者组织 HUD、建筑档案、奖励库和管理页，XCharts 处理历史/供需图，DOTween 补反馈动画。配合 Kenney 的少量切片与图标，统一字体、配色、间距和面板材质。QFramework 提供预制体管理与代码结构，皮肤和 MET 数据连接仍由我们做。

另一条主路线是 **UI Toolkit + 官方样例 + 对应 Extensions**，更适合以 UXML/USS 集中管理布局和主题；Dragon Crashers 可参考页面组织。两套主 UI 路线先选一条验证，避免为收集资源同时接两套界面基础。XCharts 是 uGUI 组件，不能直接当成 UI Toolkit 控件。

验证对象应是一个 MET 建筑详情页、一次升级/Bonus 比较、一个管理历史页和地图气泡，均读取同一份已有状态；观察实际外观、中文排版、点击反馈和接入工作量后确定主路线。城市经济、DT、建筑专业化、Bonus 和委托规则仍沿用现有设计；此次没有找到并验证可直接替代整套 MET 规则的免费成品框架。

本节已核对官方文档、作者仓库及公开许可，未下载、安装、编译或接入这些候选。本次仅更新调研文档，没有修改游戏代码或 `manifest.json`。

## 文明 VI 的参考重点

此前项目确实已将文明 VI 提升为主要视觉参考，见 [历史视觉说明](../v040/VISUAL_040.md) 与 [尚未得到用户美术认可的记录](../v040/VISUAL_ACCEPTANCE_040.md)。这个目标继续有效。

Firaxis 的美术访谈强调远距离可辨的轮廓、功能配色和自然的城市组合。[官方访谈](https://store.steampowered.com/news/posts/?appids=289070&enddate=1468504862)

从官方战略截图来看，MET 应重点接近以下特征：

- 建筑有明确的大轮廓，屋顶、墙体、设备通过材质和细节形成层次。
- 草地、裸土、山坡、岩面和河岸自然过渡；逻辑地格融入连续景观。
- 统一日光、接地阴影和克制的环境遮蔽，让物体有重量和体积。
- 小房屋、农田、道路、庭院和树群共同形成聚落密度。
- 在正常游戏缩放下仍然清楚；近距离宣传镜头只能作为素材参考。

![文明 VI 官方商店战略截图](https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/289070/ss_7f598198526afc260d939a98af4d76d95f5349e4.jpg)

参考来源：[文明 VI 官方商店](https://store.steampowered.com/app/289070/Sid_Meiers_Civilization_VI/)。图中屋顶、农田、山体和植被在整体视角下同时可辨，是本次选型的尺度参考。

## 前次付费资产参考

| 候选 | 可复用内容 | 价格 | 对 MET 的判断与接入条件 |
| --- | --- | --- | --- |
| [Dreamscape Village](https://assetstore.unity.com/packages/3d/environments/fantasy/dreamscape-village-stylized-fantasy-open-world-244797) | 模块建筑、聚落装饰与配套植被 | 标价 79；观察到促销 25.28 | 已检视首张画廊图：屋顶、石墙和植被细节丰富，幻想风格明显。需调整饱和度、屋顶比例和装饰密度。商品列 Unity 6000.0.62f1、URP 支持。 |
| [Dreamscape Nature Mountains](https://assetstore.unity.com/packages/3d/environments/fantasy/dreamscape-nature-mountains-stylized-open-world-environment-264352) | 树、岩石、地表素材和自然环境部件 | 54.99 | 已检视作者作品：树冠、草地和远山层次丰富。适合提供自然部件，需去掉魔法装饰并统一色板。商品列 Unity 6000.0.62f1、URP 支持；展示图的旧管线与额外工具影响见下文。 |
| [FANTASTIC City Pack](https://assetstore.unity.com/packages/3d/environments/urban/fantastic-city-pack-245572) | 236 个模块建筑部件、43 个完整建筑预制体、多种街景道具；作者另提供 FBX 和 Blender 格式 | 79.99 | 已检视第 2 张画廊图：街巷和建筑拼装丰富，材质较简洁，适合密集城镇。商品列 Unity 6 的 URP 支持。名称中的 City 并不代表提供现代工业城市。 |
| [FANTASTIC Nature Pack](https://assetstore.unity.com/packages/3d/environments/fantastic-nature-pack-150447) | 树木、石头、风动植被、LOD | 29.99 | 与 City 同作者同系列，可能更容易保持统一风格；这个优势仍须同场景验证。商品列 Unity 6 的 URP 支持。 |
| [FANTASTIC Seaside Town](https://assetstore.unity.com/packages/3d/environments/fantastic-seaside-town-323176) | 143 个模块建筑部件、14 个完整建筑、码头与海滨道具 | 59.99 | 已检视第 13 张画廊图：木材、瓦片和河边组合有细节。适合作为港务主题补充；与 City 有重叠，先选一个建筑主包。商品列 Unity 6 的 URP 支持。 |
| [Stylized Water 3](https://assetstore.unity.com/packages/vfx/shaders/stylized-water-3-287769) | 水面材质、岸边交互、波纹及水体渲染组件 | 50 | 作者声明支持正交相机、macOS/Metal。最新版最低 Unity 6000.0.60f1，URP 17.0.3+，必须启用 Render Graph；当前工程 6000.0.58f2 不满足最低要求。 |

FANTASTIC 的详细资产与源文件信息见作者的 [City 页面](https://tidalflask.com/store/MJMpo/fantastic-city-pack) 和 [Nature 页面](https://tidalflask.com/store/eYr9/fantastic-nature-pack)。这些源文件有利于改造造型，所购买渠道的具体交付内容仍需确认。

![Dreamscape Mountains 作者的 Unity 示例](https://cdna.artstation.com/p/assets/images/images/040/525/934/large/ioana-santamarian-screenshot-5b5.jpg?1629131500)

这是 [作者作品](https://ioanasm.artstation.com/projects/w6RVqX)，不是 MET 的运行结果。作者说明该展示使用 Amplify、MapMagic 和 Nature Renderer；商品也注明示例图来自旧 Standard 管线。不能据此承诺当前 URP 包导入后得到同样画面。可先抽取树石和材质，避免一开始引入整个演示工具组合。

## 前次付费组合参考

### Dreamscape 组合

Village + Nature Mountains + Stylized Water 3。按标价合计 **183.99 美元**；按本次观察到的 Village 促销价合计 **130.27 美元**，均未含税。

我的判断是它更值得优先尝试材质与自然环境的质感。第一轮应重组建筑轮廓、降低幻想装饰和过强饱和度，并从固定战略相机检查屋顶、树冠和街区。其示例制作使用了额外工具，实际 URP 材质与光照效果是重要的接入验证点。

### FANTASTIC 组合

City Pack + Nature Pack + Stylized Water 3。按当前页面价合计 **159.98 美元**，未含税。

我的判断是它更适合快速拼装有密度的城镇，模块建筑和可编辑源文件有利于 Codex 持续改造。相较 Dreamscape，已看过的建筑示例材质更简洁。先组合成完整街区，再调整地表、色板和阴影；港务场景有需要时才补 Seaside Town。

两套都不能直接覆盖 MET 的 21 个专业化建筑节点。后期工厂、公共设施、都市高层和外星植物需要补充；现成模块可以承担通用体块、院落、街景和材质，少量专门模型承担用途辨识与成长差异。

## 可以直接借用的地图代码与工具

[Catlike Coding Hex Map](https://catlikecoding.com/unity/hex-map/) 有公开项目和版本教程。六角地形三角化、地表混合、河道、道路和特征摆放可以选择性借用，MET 仍提供真实地块和已结算状态。项目代码与资产采用 [MIT-0](https://catlikecoding.com/unity/tutorials/license/)，可修改和商用；教程正文与插图使用另一套许可。它是几何和代码基础，仍需要上述美术资产与光照。

最新 Hex Map 5.6 使用 Unity 6000.3.23f1，高于当前工程与水体建议基线。应选择合适固定版本或提取局部实现；为最新教程整包升级工程并无必要。[当前教程](https://catlikecoding.com/unity/hex-map/5-6/)

[官方 Terrain Tools](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.terrain-tools.html) 提供地形笔刷与工具，可作为改用 Unity Terrain 的免费路线。当前 HeroWorld 导入的是 GLB Mesh，使用 Terrain Tools 或 MicroVerse 意味着转换展示地形。第一轮保留已有连续 Mesh，接入树石和水体，改动范围更明确。

Gaia 和 MicroVerse 提供较完整的世界制作工作流，但目前的小河谷首先需要选定美术与地图接线。暂不把它们作为首轮依赖。Hand Painted Nature Kit 可作为植被补充；简单 Toon Farm、极简 Polygon 建筑和单栋升级包不作为这次质感目标的主资产体系。

## Codex 实施与实际验证

现有 Unity 工程锁定 Unity 6000.0.58f2 / URP 17.0.3，已有只读河谷 GLB、相机、建筑和树木桥接。第一轮沿用这个场景输入，将选定预制体接到真实建筑与地块位置；统一比例、接地点、材质、日光和渲染设置。

当前零预算路线优先核对免费水体的 Unity 6 / URP 版本及 Mac shader 编译。以下保留付费水体的版本说明：若未来另行选择 Stylized Water 3，其商品兼容表使用 **6000.0.68f1**，需检查 Render Graph 开关并明确调整编辑器基线。[水体商品](https://assetstore.unity.com/packages/vfx/shaders/stylized-water-3-287769)、[作者文档](https://staggart.xyz/unity/stylized-water-3/sw3-docs/)

Codex 可通过 Unity 的 C# 与 Editor 脚本组装标准预制体、修改材质参数和构建场景。预制体和公开源码比只能手动操作的闭合工具更适合持续迭代。作者水体文档允许修改源码；购买后仍需检查真实接口和包内内容。既有河面是否满足新 shader 的 UV 与顶点通道要求，也要在接入时核对。

用同一河谷、相机和白昼展示树群、农田、建筑群、道路与河岸，并在 1920×1080 正常游戏缩放下看整体。主要看是否已有接近文明 VI 的材质层次、自然地貌和聚落密度，建筑用途是否清楚，以及修改一个街区是否能通过脚本顺利完成。用户认可这个画面后，再扩展完整资产覆盖和实时城市接入。

现有 [Unity 验收记录](ACCEPTANCE.md) 尚无本工程 Unity 实际运行截图、性能或 macOS 包。此次调查没有购买、安装或运行这些资产，以上成本也不包含工业都市模型、可能需要的制作插件或后续改造工作。

第三方原始包和自己的源码分别管理。尤其水体作者明确限制公开仓库散发原包；沿用项目已有 vendor_local 与源码打包排除机制，按实际许可处理交付。[作者说明](https://staggart.xyz/unity/stylized-water-3/sw3-docs/)
