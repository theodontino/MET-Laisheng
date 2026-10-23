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


## unity.3 地貌与附属肌理

活跃 FullCity 地表改为原创参数化网格和 URP 顶点颜色 PBR，不再使用上一版照片草土。附楼墙体、屋顶及路径是项目参数化几何，没有新增付费包；新增原创表现不改称 CC0，也不是纯图像模型管线。社区模型由官方 glTFast 导入，纹理及许可保留；运行时复制材质处理色彩，未覆盖 Hero 或原始资源。实际结果见 [完整迁移记录](FULL_MIGRATION.md)。

## visual.13 建筑材质分层

`CityWorld.Palette.cs` 按实际导入资源处理材质。Kenney Suburban / Commercial 的墙面、平屋顶、斜屋顶，以及 Industrial 的墙面、屋面，各使用经 GLB UV 与原始色板核对的槽位。住宅使用暖墙与陶色屋面，公共楼体使用米白墙与灰蓝屋面，工业使用暖灰墙与低饱和灰绿屋面。未映射的玻璃、窗带、金属结构、烟囱标色、植被保留；不再按整栋 HSV 去色，也不再将学校整张贴图乘以 0.78。

这套调整只创建运行时材质与色板副本，不覆盖原 GLB、PNG、导入 Shader、UV 或登记身份。副本按来源纹理及用途族共享，64 个色块预先计算，上传后释放 CPU 像素副本，并随 CityWorld 销毁。完整目录涉及最多五张 512×512 RGBA32 色板副本，含 mip 的理论 GPU 像素量约 6.7 MiB；这是资源规格估算，实际进程 RSS 另行测量。原农居只按已有 `mat_ivory`、`mat_shadow_wall`、`mat_trim`、`mat_stone` 材质名调色，其他材质保留。附楼和街边填充建筑采用同一配色方向，几何与 74% / 86% 主体尺度不变。


## visual.14 类型识别色

2026-10-14 用户认可当前建筑群形态，新增配色约定：**住宅绿、工业黄、服务蓝、特殊建筑红**。本轮保持 visual.13 的几何、74% / 86% 主体尺度与布局。识别色集中在屋顶和附楼，墙面仍为米白/暖灰；农业和绿化保留自然配色。

- 住宅使用绿色屋顶 `#7FA883`；工业、采掘、电力和货运使用土黄 `#D1AE59`；社区、商业、医疗和教育服务使用蓝色 `#78A8C3`。阴影槽仍使用相应较深色阶，玻璃等未映射槽保持原色。
- 特殊红 `#C77060` 用于主题定型建筑、托卡马克之冠的现有卡片与标识。它们当前没有可放置的地图主模型，本轮未补造地图实体；普通医院、学校和发电厂仍按自身用途着色。市政储备、待安置住区等账务上的 `special` 项目不据此变成红色地标。
- 全部 21 绑定经原 GLB 的实际 UV/面方向核对。补上三组模型专属槽：Suburban `building-type-p` 的 c0r4/5、Industrial `building-m` 的 c4r4、`building-s` 的 c4r4/5。列/行从色图左上角以零起算。其他模型的同槽可用于设备/深色结构，因此不全包重染。该判断基于屋面代理与实图，不宣称这些槽在目标模型中只含屋顶。
- 主楼、用途附楼、工业棚、住宅填充和沿街商店使用同一配色方向；图标和建筑详情标题同步。设置中的“建筑识别色”解释分类。颜色不是唯一识别线索，原模型轮廓、名称和来源信息仍保留。

新增屋顶特例按模型档案隔离并复用色板；当前目录最多八张 512×512 RGBA32 含 mip 副本，理论像素规格约 10.7 MiB，不等于进程 RSS。原始 GLB/PNG、官方 glTFast 导入、许可与资产身份保持。实际截图和占用验证见 [完整迁移记录](FULL_MIGRATION.md)。

## visual.15: broader category color and independent landmarks

The user requested broader coverage of category colors. Main model palette wall regions and the shared wing/annex wall materials now carry restrained green for housing, warm yellow for industry/energy/logistics, and pale blue for services, in addition to the established roof colors. Windows, equipment and the original geometry retain their separation. The ordinary 74% and hospital/secondary-school/power-plant 86% scales remain unchanged.

The user explicitly confirmed that special red includes independent landmarks such as union headquarters, the civic committee, chamber of commerce, port authority and agricultural cooperative headquarters, together with the tokamak project. The existing capstone and project UI uses this red. Hospitals, schools and power plants keep their service-blue/industrial-yellow classification; high tier alone does not make an ordinary building a red landmark. The independent capstones/tokamak still have no registered map primary models, so no new red landmark geometry is claimed.

Original community GLB/PNG files and official glTFast import remain intact. The six new surface maps are separately documented in [nature and terrain assets](NATURE_ASSETS.md#visual15-natural-slopes-same-cell-occupancy-and-surface-families).
