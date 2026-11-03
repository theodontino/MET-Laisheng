# UI6 道路入口独立图证复核

复核日期：2026-10-23。本记录只核对已生成的 macOS Player 图像、JSON 和对应源码；本次复核没有启动 Unity 或注入原生输入。

## 实际证据

两次报告均记录 Unity 6000.6.5f1、OSXPlayer、Apple M5 Max、Metal、1920×1080 全屏，状态为 `passed`。

| 内容 | 修改前诊断基线 | 修改后实际画面 |
| --- | --- | --- |
| 全部道路 | [roads-before / all](evidence/ui6/road-before-ui6-roads-all.png) | [native-left / all](evidence/ui6/road-after-ui6-roads-all.png) |
| 隐藏地块环路和支路 | [roads-before / steps-only](evidence/ui6/road-before-ui6-roads-steps-only.png) | [native-left / steps-only](evidence/ui6/road-after-ui6-roads-steps-only.png) |
| 隐藏门前入口连接 | [roads-before / lanes-only](evidence/ui6/road-before-ui6-roads-lanes-only.png) | [native-left / lanes-only](evidence/ui6/road-after-ui6-roads-lanes-only.png) |
| 入口几何 | [修改前 JSON](evidence/ui6/road-before-road-geometry.json) | [修改后 JSON](evidence/ui6/road-after-road-geometry.json) |
| 执行检查 | [修改前报告](evidence/ui6/road-before-ui6-validation.json) | [修改后报告](evidence/ui6/road-after-ui6-validation.json) |

修改前采用同时指定 `--validate-ui6 --roads-baseline` 的受限诊断分支，复现原有 `footprint.yMax + .24` 入口、完整膨胀避障矩形及仅采样顶点的环路目标。修改后采用正常入口逻辑。该基线只用于入口几何比较，不代表重新运行历史源码版本。

两份几何 JSON 的九个地块 ID、建筑 footprint 和 floor 完全一致，没有因本次入口调整移动这些建筑。两组报告分别确认切换可见性时相机位置、旋转和正交尺寸保持不变。两次截图的选择高亮地块不同，因此不能把整张图的每处像素差都归因于道路；建筑、地形和道路的画面位置可以对应复核。

## 核对结果

| 项目 | 修改前 | 修改后 |
| --- | --- | --- |
| 主建筑场地 | 9 | 9 |
| 入口相对环路中心线的位置 | 外侧约 `+.095` | 内侧约 `-.045` |
| 近水平门前连接 | `none` × 9 | `apron` × 9 |
| 本场景实际阶梯连接 | 0 | 0 |
| 入口终点可达检查 | 23 / 23 | 23 / 23 |
| 未到达终点 | 0 | 0 |

放大两张 `all` 图，可以看到原农场环路左下侧向外伸出的短斜支路在修改后消失。修改后 `steps-only` 隐藏了环路，保留门前短铺面；`lanes-only` 隐藏这些短铺面，保留环路和附属建筑支路。结合 `entrance_connection: apron` 与入口坐标，可确认本场景门前铺面位于环路内侧并延伸至建筑门槛。这里实际出现的是平缓入口铺面，不是阶梯。

原生报告的 23 个终点全部可达，覆盖主入口与附属建筑连接；它证明当前路径图没有丢失这些连接，不替代角色行走或真人操作验收。

## 三条深色线是田垄

图中两处小型农场旁的三条深色平行线仍然存在。`CityWorld.cs` 的 `BuildUseDetails` 为 `farmland` / `modern_farmland` 明确生成三组 `FieldSoil` / `CropRow` 带状几何。它们不属于 `MergedParcelEntranceAndSideLanes` 或 `TerrainToDoorEntranceSteps`。

进一步对原始 1920×1080 PNG 的东侧田垄区域 `(1007,430)–(1036,442)` 做像素比较：修改前、修改后各组三张 `all` / `lanes-only` / `steps-only` 在该区域完全相同。这些田垄不能用来判定入口短支路仍未修复，也不应作为道路尾巴删除。

## 证据边界

本场景九个入口都是近水平铺面，没有实际生成需要上台阶的入口，因此这组图不提供坡地阶梯效果的新验收。`TerrainToDoorEntranceSteps` 保留原有可见性组名，但现在同时包含近水平 `apron` 与真正的 `stairs`；图名 `steps-only` 表示该入口连接组。

本次独立复核只新增本记录，没有修改道路、模拟、存档或玩法数据。实际 macOS 图像和入口可达检查来自上述已执行 Player 报告；没有把 .NET 检查或源码计算称为原生截图。

## 随后的坡地补充

Main 随后在实际 Player 执行了原有脱离模拟的坡地 fixture，结果与来源边界详见 [UI6 验收记录](UI6_ACCEPTANCE.md)。十座展示建筑中七处需要坡地支撑，24 个入口终点全部可达，地形高度、实际存档及 RNG 保持不变。该补充与上文九个近水平场地的独立复核属于不同场景，不把前者改写为坡地证据。
