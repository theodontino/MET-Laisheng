# Unity 城市社区资产接入

2026-10-13。本轮使用项目已下载的 Kenney 官方免费套件，并实际接入动态城市；原 Hero 实验、四个原建筑模型和原树保留。

四套来源均在官方页面和包内 `License.txt` 标明 CC0：

- [City Kit Suburban 2.0](https://kenney.nl/assets/city-kit-suburban)
- [City Kit Commercial 2.1](https://kenney.nl/assets/city-kit-commercial)
- [City Kit Industrial 2.0](https://kenney.nl/assets/city-kit-industrial)
- [Factory Kit 3.0](https://kenney.nl/assets/factory-kit)

选择前已检查原包 `Previews` 图片与所选 GLB 的 POSITION accessor 范围。运行时仍仅由官方 glTFast 导入 GLB；没有自制 GLB 导入器。原有 MET 资产继续使用 `LicenseRef-MET-Project-Original`，不改称 CC0。

## 实际登记

登记源为 `content/presentation3d/unity_city_assets.json`；Unity 同内容副本为 `Assets/Resources/CityModels/Kenney/unity_city_assets.json`。

- 21 个玩法建筑定义具有明确绑定。
- 19 个不同主模型：18 个 Kenney 主模型、1 个 MET 原农居主模型。
- 29 个 Kenney GLB 实际进入 Resources：18 个主模型、11 个附属模型。
- 另保留四个 MET 原建筑和一棵原树，合计 34 个登记 GLB。原农居也是三类农业定义的共同主模型。
- 动态城市运行时的 `AssetCoverage` 分开报告注册资产、不同主模型、可见主模型、CC0 来源、项目原创来源及当前定义绑定。施工状态不计作已绘制主模型。

医院、诊所和学校使用社区通用楼体，并增加医疗十字、开放书本和操场标记；这些是 MET 用途分配，不能宣称 Kenney 套件原生提供相应医院或学校。园区使用社区花坛与遮阳伞，并保留原树。工业、货运、能源使用 Industrial 楼体、集装箱、太阳能板、储罐以及 Factory Kit 起重机、机器、料斗和传送带。农业保留原农居，加田垄；现代农业另有太阳能附属资产。

模型尺寸以导入后的实际 MeshFilter bounds 经节点变换测得。统一按地块占地宽度和最大高度约束等比缩放，水平居中并以最小 Y 落地，留下道路与庭院间距。glTFast 的坐标转换已完成，运行时不再次镜像模型；原 GLB 材质与色图保留，使用同一实时日光、阴影和 URP 色调映射。

## 地表与加载

用户反馈首版地面单薄后，新增 [Poly Haven Grass Ground](https://polyhaven.com/a/grass_ground) 的官方 1K Diffuse JPG；[官方许可](https://polyhaven.com/license)为 CC0，可商业使用。源图完整保存在 `Assets/Resources/CityTerrain/GrassGround.jpg`，来源与许可记录随 Player 放入 `ThirdPartyNotices/PolyHaven-CC0.txt`。它作为低幅重复细节图与按真实地貌生成的宏观草土颜色组合，使用官方 URP Lit；不是烘焙光照。地表材质及其 detail shader keyword 在 Editor 序列化，保证 Player 保留实际使用的变体。

运行时按当前建筑绑定和附属设施加载 prefab；不再一次预载全部 34 个登记 GLB。实际加载数与资源键由 `AssetCoverage` 分开报告。原树的源网格保持可读以支持实例合并，合并后的树、庭院和可见地表释放自己的 CPU 网格副本；分析与碰撞沿同一可见地形三角面裁切。背景延展没有地块 ID 或额外碰撞，不增加可建设区域。

## 验证边界

源文件登记与资源路径核对、官方许可证核对、真实 Unity Managed DLL 接口编译属于当前源检查。Main 统一执行实际 Editor 导入、macOS 构建与运行后，再写入相应执行证据。此接入不等于已完成 Civilization VI 美术，也不代表尚未运行的画面或性能验收。
