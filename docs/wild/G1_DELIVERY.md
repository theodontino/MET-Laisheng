# G1 原生工程预览记录

2026-10-27。本轮回到现有 Unity 工程继续开发，完成农居星图的第一段原生接入。按用户确认保留作坊→货运站→希望城贸易路径；星图按真实连线判定前置，绘线与规则共用配置。此包为 G1 首段工程预发布，不是完整 Wild 版本。

## 当前可运行内容

双击 `build/wild-g1/MET Wild G1.app`，完成原有新城地点与起源步骤，选择农居 → 建筑星图。初始 2 设计点，可施工复合及其中一条普通分支；继续时间推进真实队列，完工后生效。可取消已完成节点，查看级联清单，返还设计点但不退材料。

“资源与财政 → 市场”包含希望城实际出口资格与住房设计 I 购买。当前仍需原有货运设施；没有把预置货运的工程测试当作新开局闭环。科技购买后可用 4 点安排普通路线与 T2 进阶；更高设计容量、新 Bonus 内容与工业化路线尚未完整接入。

详细实现、已确认规则和剩余范围见 [G1 原生说明](G1_NATIVE.md)。应用使用独立 `Wild-G1` 存档子目录。现有 checkout 和未提交工作保留，没有转换玩家旧档。

## 已运行的工程检查

- 原生 Wild 核心：241 项检查通过，覆盖真实施工、预览只读、设计容量、节点依赖、槽位和无槽路线、OR 前置与级联取消、T2 基数、地形、报价及保存加载。
- 城市贸易：59 项检查通过，覆盖真实部分成交、目标贸易对象、资格续计、付款一次、科技投影及保存加载。测试预置货运设施。
- 现有专业化：145 项检查通过，核对未启用 Wild 时仍遵守原有能力逐级衰减。
- Unity **6000.6.5f1** 实际构建 macOS universal Player，在 **Apple M5 Max / Metal、2560×1440 全屏**运行。
- 最终 Player 脚本检查通过，报告无捕获错误；覆盖 UI 80/100/120% 与独立字号 100/125%、输入归属、预览、施工及取消。报告中的 322 条断言包括逐节点布局检查，不代表 322 个独立游戏功能。

Player 使用合成 `NavigationSubmitEvent`，在独立工程新城中明确补充食物80、材料40，使用直接模拟步推进施工。**不是真人鼠标滚轮验收，也不是正常游玩节奏或平衡验证。** 体验仍由用户试玩判断。

本次运行发现并修正：农居报价仍读取旧 CP；Wild 初始基础供给未接入10CP/月；标准字号下 T2 节点裁切；连线汇成公共横线而容易误读；成本缺少设计点与施工量标签。最终截图已对应修改后的 Player。2026-10-27 追加核对了配置连线与前置说明，并修复施工图例缺字；最终证据来自修复后的同一构建。

## 真实截图与记录

- [默认星图](evidence/g1/02-wild-star.png)：八个节点完整显示，左侧路径、右侧效果与投入，底部固定操作。
- [UI120% / 字号125%](evidence/g1/03-layout-ui120-font125.png)：图与详情内部滚动，固定确认区保留。
- [真实施工开始](evidence/g1/04-compound-started.png)
- [普通分支解锁](evidence/g1/06-housing-unlocked.png)
- [级联撤销确认](evidence/g1/08-cascade-removal-confirmation.png)
- [容量返还](evidence/g1/09-capacity-returned.png)
- [Player 记录](evidence/g1/player-evidence.json)

## 复现

```sh
MET_UNITY_WILD_BUILD=1 \
MET_UNITY_RUN_DIR="$PWD/build/wild-g1/build-checks" \
MET_UNITY_MAC_OUTPUT="$PWD/build/wild-g1/MET Wild G1.app" \
bash tools/test_unity_city.sh mac
```

工程截图需显式隔离目录，保存目录必须位于输出目录之内：

```sh
MET_WILD_G1_OUTPUT="$PWD/build/wild-g1/player-evidence/recheck" \
MET_UNITY_SAVE_ROOT="$PWD/build/wild-g1/player-evidence/recheck/isolated-save" \
"build/wild-g1/MET Wild G1.app/Contents/MacOS/MET 来生 Unity Beta" \
 --wild-g1 --wild-g1-checks -screen-fullscreen 1 \
 -screen-width 2560 -screen-height 1440 \
 -logFile "$PWD/build/wild-g1/player-evidence/recheck/player.log"
```

导出的包为本地 ad-hoc 签名，无 Developer ID 公证。版本标识为 `v0.7.0-wild-g1.1`，发布与来源记录见交付包和对应 GitHub 预发布。发布源码从 G0 版本继承，使用独立发布快照纳入构建必需的 Unity 源码、内容、检查工具及本阶段证据；工作分支和用户未提交文件保持原位。
