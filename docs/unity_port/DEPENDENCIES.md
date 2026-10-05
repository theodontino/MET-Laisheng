# 免费组件与资产来源

优先采用现成的官方免费组件；此轮没有下载第三方美术包，没有新画建筑或树，没有自制 glTF 解析器或 URP 替代品。

| 项目 | 固定版本／来源 | 许可与使用边界 |
| --- | --- | --- |
| Unity Editor | 6000.0.58f2 | Unity 编辑器许可，须有效激活；免费 Personal 取决于当期资格。GameCI Dockerfile 的许可不能替代编辑器授权。 |
| Universal Render Pipeline | `com.unity.render-pipelines.universal` 17.0.3，Unity Technologies | Unity Companion License；用于 Unity 项目。正式 UPM 安装时保留随包许可。 |
| glTFast | `com.unity.cloud.gltfast` 6.20.0，Unity Technologies | Apache-2.0。核验官方 `release/6.20.0` 源码 commit `7625689b3a88aead413063a3154d465273c629da`；不使用 prerelease。 |
| Newtonsoft JSON | `com.unity.nuget.newtonsoft-json` 3.2.1 | 原库 MIT，James Newton-King；Unity 包另有随包说明，安装后核对并保留。 |
| Unity 内置 Camera/Light/MeshCollider/Resources/实例绘制 | 对应 Editor | Unity 许可；没有另造基础引擎或第三方付费工具。 |
| Godot GLTFDocument | Godot 4.7.2 Standard | MIT；仅作为 MET 既有场景的便携导出工具，不随 Unity Player 运行。 |
| 4 栋建筑＋河谷树原型 | `content/presentation3d/asset_manifest.json` | MET 自制 `LicenseRef-MET-Project-Original`，保持原授权；不能称为 CC0，也不授予一般第三方再利用。 |

官方组件核查来源：

- [glTFast 6.20.0](https://github.com/Unity-Technologies/com.unity.cloud.gltfast/tree/release/6.20.0)，包括 `LICENSE.md`、`Documentation~/ProjectSetup.md`。
- [Unity Graphics](https://github.com/Unity-Technologies/Graphics)，实际 Editor/URP 组合安装后仍需验证 API、导入和 shader。
- [Newtonsoft JSON](https://github.com/JamesNK/Newtonsoft.Json)。
- [Unity Personal](https://unity.com/products/unity-personal)、[Unity Hub](https://unity.com/download)。

UPM 包由 Unity registry 获取，不把包缓存、账号文件或 Editor 二进制纳入源码。资产登记仍在原 `presentation3d`，导出 sidecar 逐文件记录来源 SHA256；源码包另带原资产和 `MET_PROVENANCE/` 登记。新 Unity 运行产物发布前，应根据实际安装的包核对完整版权声明，而不能把旧 Godot Notices 当作完整的 Unity Player notices。
