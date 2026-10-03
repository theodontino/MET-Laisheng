> 历史档案：B 0.1.0-exp003 原文，已被 B 0.1.1 人口版决策覆盖。不得作为本次测试结果或当前玩法规格。

# 实现与继续开发

## 结构

- `core/game.gd`：唯一游戏会话、状态、命令、项目派生属性、邻接、DT、导入校验；不拥有视图节点。
- `core/catalog.gd`：一个 Godot Resource 目录对象，加载两个 JSON 数据文件；没有为每张卡创建一份脚本或场景。
- `core/tenders.gd`：硬合法性、三槽加权、升级偏好、重招、延期与提交。
- `core/inventory.gd`：资源+接口签名库存、匹配、原子配方消耗。按标签签名区分库存，不追踪生产建筑来源。
- `core/simulation.gd`：唯一固定步长本地模拟入口，资源、能源、消费、施工、运营、使用率与瓶颈。
- `core/progress.gd`：里程碑、关系成熟、施工提议、科技、承诺、支持、危机与成就。
- `core/rng.gd`：31bit确定性RNG，opening / draft / events / risk四个流，均写入快照。
- `core/storage.gd`：版本化JSON、payload校验、临时写入、最近备份与损坏回退。
- `core/hex.gd`：固定地图、坐标、连通和邻接工具。
- `ui/app.gd`：应用时钟适配、命令入口、菜单与存档。
- `ui/map_view.gd`：程序化2D地图与项目形态、选区、缩放、拖动、关系线。
- `ui/widgets.gd / panels.gd / dialogs.gd`：主题、详情、投标、研究、治理等视图。
- `tests/`：规则、图形UI、合法操作启发式流程、许可证导出。

首版使用 Resource 目录+JSON，而非每张卡单独 `.tres`。这是内容编辑方式的实施选择，不改变机制。状态当前是集中校验的Dictionary，非全量强类型领域对象。只建立首版真正用到的条件与effect操作，不提供任意执行的“万能DSL”。

## 基本不变量

UI只能经会话命令改变正式玩法状态；诊断用的F8修改另有明确标记。绘图不会消耗玩法RNG。相同种子、命令和显式时间输入可复现；不承诺不同引擎版本浮点行为逐字节相同。

项目属性从内容定义、场地、已完成升级、施工历史、有效邻接和已签协作派生。未完成升级不提前生效。旧库存接口不会随项目升级追溯改写。

默认一个Site拥有一个Project，但`site.project_ids`保留容器接口；Hex只归一个Site，禁止重叠占地。不是为了未来无限扩展先做多重空间归属。

## 时间与供给

在线正常时间、DT Pulse和实验倍率都进入同一 `advance`，固定步长1/12本地小时。离线不调用本地advance，只结算有限DT与外部委托到期。短暂大帧间隔/挂起由现实时间间隔识别，避免恢复窗口时世界瞬间补算一整晚。

每步先准备派生需求、能源与库存，再保障基本食物，随后以小批次对生产/施工共享物资。带限制输入先匹配，配方原子消耗，不能对两个输入重复算同一份库存；新输出在当步后段入库，避免零时间循环。项目施工根据实际消耗推进，不会因缺料仍完整造出建筑。

关系积累使用邻接+模板兼容+双方有效运行比例乘积；不增加建筑间资源provenance。已成熟协作在项目停运、拆除或失去邻接时不能继续无条件增益。

DT成本每个已推进小步计算；遇到施工机会停在事件前，只收实际推进成本。库存不足会降低运行，DT不是保证城市有效产出的一键完成。

## 运行测试（装有Standard编辑器）

在项目根目录，`GODOT`设为本机Godot可执行文件路径：

```bash
export GODOT="/Applications/Godot.app/Contents/MacOS/Godot"
"$GODOT" --headless --path . --editor --import
"$GODOT" --headless --path . -- --test
"$GODOT" --headless --path . -- --playthrough
"$GODOT" --path . -- --ui-smoke
```

测试报告默认写到 `user://test_results`。设置 `MET_EVIDENCE_DIR` 可指定目录。前两套测试使用独立会话，不读取主存档。UI smoke会在最后用完成状态拍图，部分中间截图明确使用测试fixture。

`tools/test.sh`包装前两套无窗口测试。上述编辑器导入/导出命令是继续开发路径；**本次实际执行环境没有编辑器，因此没有声称编辑器路径也已实测**。

## 本次实际构建方法

复用了旧CI工件中的官方Godot 4.7.2运行模板，但不复用旧P0规则。Linux运行模板剥离旧嵌入PCK后，可加载同名兄弟PCK。`tools/pack_runtime.py`把当前源码封装为Godot支持的未加密PCK v2。新场景、脚本和数据已在该运行时实际加载执行。

```bash
python3 tools/pack_runtime.py . /path/to/godot_runtime.pck
MET_EVIDENCE_DIR=/tmp/met-tests /path/to/godot_runtime --headless -- --test
```

macOS用 `tools/package_macos.py`组装新的通用app，替换游戏数据、Info.plist、图标与资源封印，对两种架构重做ad-hoc SHA256 CodeDirectory；验证全部代码页、资源摘要和签名槽。它**不是Apple Developer ID签名，不是公证，也不是Apple codesign/spctl验收**。

正常继续开发建议使用Godot编辑器与匹配导出模板，项目内提供`export_presets.cfg`，在Mac上重新导出并用Apple工具验签。缺少DeveloperID时使用内置ad-hoc导出；不要伪装成已公证发行版。

## 推荐回仓方式

在原仓库新分支中放到例如`game/exp003/`作为独立Godot项目，保留旧P0便于比较；不要覆盖原Flutter或旧存档目录。先在Mac验证新工程导入、运行和导出，再调整CI。此次交付未替用户执行push。

## 后续修改原则

新内容先改catalog，再加效果执行与明确测试。涉及签名/存档/时钟的改变先做回归。改变schema需写迁移；本版仅schema1，不声称已有未知未来版本兼容。

F8用于对照完整系统，不把去掉资源/升级的中间状态拿来判断整个游戏死活。人类试玩必须另记是否启用调试；自动通过不是乐趣结论。
