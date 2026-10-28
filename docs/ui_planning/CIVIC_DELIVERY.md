# Civic UI.1：实际运行与交付记录

日期：2026-10-29。版本：`v0.7.0-civic-ui.1`。基于 `v0.7.0-wild-g1.1`。最终源码以发布标签所指提交为准。

## 已完成的变化

- HUD、全局概览、地图左下地块、建造/确认/奖励、对象抽屉使用统一 UI Toolkit 外观与输入归属；星图沿用既有 G0/G1 窗口和实际模拟命令。
- 全屏管理使用独立布局与内容：住户/岗位/服务、资源归属、市场/政策、施工调度、任务三阶段、Bonus 库存/目标/比较、档案检索。侧栏保留容量条与重点信息。
- 全屏列表支持各自的筛选、排序、选择和检查；定位暂时回地图，返回恢复原列表。栏目切换恢复阅读位置与嵌套来源的展开状态。
- 正文保持自然行高；放大字号时缩短滚动内容区。检视提示为浅色文字和不透明深底，展开来源的标题、焦点和悬停状态均指定浅色。
- 修正从住宅进入管理时选错对象、任务标签复访丢失、市政储备假定位、资源标题口径错位以及少量未翻译指标。
- 本轮没有更改经济数值、星图/科技规则、地图模型或新增后续 G 阶段。只读历史的“继续”入口增加写入前保护。

## 最终 Player 工程结果

- Unity 6000.6.5f1，macOS Player，Apple M5 Max / Metal。
- 2560×1440 全屏，869/869 项工程断言通过，40 张运行截图；最终记录为 `player-9`。早期迭代的失败不计作通过。
- HUD/地块/概览/操作窗口覆盖 UI 80/100/120% × 字号 100/125/150% 共九组布局；全屏管理另在默认及 UI120%/字号150% 运行。
- 包含取消建造不付款且候选/RNG 不变、确认后加入真实队列、保存奖励候选/领取、Bonus 真实库存、页面浏览不改经济、只读状态、键盘焦点、旧界面不接收射线、筛选/选择/滚动/来源恢复。
- 文本检查读取真实布局，检查自然行高、进度条不压文字、固定头尾与内容分区。来源恢复检查使用真实作坊活动与实际周期回执，没有填充假长文本。
- 悬停说明的实际解析颜色：文字 RGB(239,247,247)，背景 RGB(7,21,30)，均不透明；计算对比度 17.02:1。对应截图 `45-hud-tooltip-readable.png`。

全部工程事件通过 UI Toolkit 的合成导航/键盘/指针事件触发，截图由实际 Player 渲染。测试使用独立暂停存档、固定种子 20261029 和已明示的调试补给。上述结果不代表正常经济节奏、玩法平衡或真人验收。

## 原生输入记录与限制

通过电脑操作工具，在最终 Player 实际观察到：L 从地貌切到医疗；Shift+L 切回地貌；Esc 收起选中地块；Command+Q 退出。工具截图保留在本次会话记录。

鼠标完整操作流程未计为通过：此前 Player4 的多次电脑工具点击和拖动虽然移动了截图中的光标，Unity 收到的坐标仍固定在 (1779,732)，Toolkit 坐标为 (1779,708)。记录在 `pointer-observation-player4.jsonl`。坐标处的 UI 正常拥有输入，但它不是工具指向的按钮。未改生产代码去补偿工具坐标。工具还拒绝发送单独 Shift（`keyPressIncludedNoNonModifierKeys`），因此单独 Shift 的原生开关行为未验收。

鼠标、滚轮和真实游玩手感由用户验收；不得把 869 项合成工程断言称为真人输入通过。

## 可复现入口

```sh
MET_UNITY_WILD_BUILD=1 \
MET_UNITY_RUN_DIR="$PWD/build/civic-ui/build-checks" \
MET_UNITY_MAC_OUTPUT="$PWD/build/civic-ui/MET Civic.app" \
bash tools/test_unity_city.sh mac

MET_CIVIC_OUTPUT="$PWD/build/civic-ui/recheck" \
MET_UNITY_SAVE_ROOT="$PWD/build/civic-ui/recheck/save" \
'build/civic-ui/MET Civic.app/Contents/MacOS/MET 来生 Unity Beta' \
 --civic-ui-checks -screen-fullscreen 1 -screen-width 2560 -screen-height 1440 \
 -logFile "$PWD/build/civic-ui/recheck/player.log"
```

检查入口强制要求单独输出目录及其下的存档目录；缺少或错误路径时拒绝生成测试场景。发布包直接双击运行无需这些参数，沿用 Wild G1 的存档位置。

## 交付边界

提供 Universal macOS `.app`、保留 bundle 的 ZIP 和含40张图的工程证据 ZIP。应用使用 ad-hoc 签名；没有 Developer ID 签名或 Apple 公证。发布使用独立源码快照，不移动当前 checkout 的 HEAD，不修改用户暂存区。并行任务的新设计提案不混入此 UI 发布。
