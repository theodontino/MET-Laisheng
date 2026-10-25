# MET Civic / 01

52 枚原创、可持续复用的城市界面图标。本库采用 32 × 32 网格、1.9 单位圆角线条、白色主体与透明填充层。保持 MET 的深色市政界面风格，不使用商业游戏素材、字体字形或第三方图标包。

## 文件与使用

- `index.html`：离线图标浏览器，可搜索、调整 16/20/24/32/48 尺寸，预览默认、提醒、禁用与浅底状态。
- `preview.png` / `preview.svg`：整套概览及 20/24/32 尺寸样张。
- `svg/`：可编辑矢量源文件，语义 ID 与文件名一致。网页内联使用时以 `color` 着色。
- `png/32/`、`png/128/`：透明白色 PNG。标准 UI 的 Image/Sprite 可直接着色；128 px 版本用于 2K、UI 放大或高清显示。
- `manifest.json`：中文名称、分组、稳定 ID、兼容别名。

常用 ID：`home` 住房，`population` 人口，`employment` 就业/岗位，`build` 建造力，`crew` 临时施工队，`medical` 医疗，`archive` 档案。`service` 是旧兼容名称，映射为 `medical`；新代码请使用具体服务名。图标不能仅靠颜色区分用途，旁边仍应保留短名称或说明。

推荐显示尺寸：顶栏 28、导航 23–24、页内标题 24、内联 20。最小 16 仅用于空间紧张的操作图标；复杂资源/服务图标优先 20 以上。始终保持正方形，外部容器可宽但不要拉伸图形。不用随机变化的图标或 emoji 代替语义 ID。

状态颜色由页面决定。推荐默认 `#B6D9CD`、提醒 `#EAC482`、禁用 `#71888A`；业务既有的资源颜色也可继续使用。不要将颜色直接烘焙进透明 PNG。需要放大展示时用 SVG 或重新导出较大 PNG。

## Unity 接入

当前游戏的 `Resources/CityIcons/` 保存 128 px 导出；`CityIconCatalog` 规范化 ID；`CityHudGlyph` 缓存贴图并在中心正方形内绘制，保留 uGUI MaskableGraphic 遮罩与状态着色。横排标题给予图标固定宽度，文字承接剩余空间。面板边框继续使用独立向量绘制，不受图标比例限制影响。

新项目也可直接导入 PNG 为 Sprite，在 uGUI Image 勾选 Preserve Aspect。使用 Clamp、Bilinear、Alpha Is Transparency、不生成 Mipmap、不压缩，避免小图形边缘受块压缩影响。

## 增加或更新

1. 编辑 `svg/<id>.svg`，保持 32 单位坐标和既有线条。语义不变时保留 ID。
2. 新图标在 `manifest.json` 增加 ID、中文名和分组；兼容旧名称用 aliases，避免复制同一图形。
3. 在仓库根目录运行 `node tools/build_city_icons.mjs`。脚本需要 Node.js 和 `sharp`（本次使用 0.35.5）；如果依赖装在其他目录，设置 `MET_SHARP_MODULE=/absolute/path/to/node_modules/sharp`。
4. 脚本导出两种 PNG、Unity 资源、C# catalog 和离线总览，保留既有 Unity meta GUID。
5. 检查小尺寸轮廓与 Unity 中的实际显示，再扩充使用位置。

原图为可编辑 SVG，导出 PNG 和样张由标准 SVG 渲染器生成，不是图像模型生成。源文件属于 MET 项目原创资产；详见 LICENSE.txt。工程检查不替代玩家对观感与操作的验收。
