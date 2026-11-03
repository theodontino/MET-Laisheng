# Wild 可配置里程碑：2026-10-29 本地实现

本轮仅修改现有游戏的任务奖励路径。未提交、推送、发布、启动Unity/Player、访问应用界面或打开默认存档。当前生产配置仍只有「住房设计 II + 累计真实出口20材料」，三选一仍为材料10 / 基础制成品1 / W01库存1；II价格90通货。未新增任务、未重平衡、未接入W02。方案仍须手动装配。

## 实际实现

`content/beta/wild.json` 的 `milestones` 列表定义稳定id、显示名、两个AND条件（`housing_levels`、`material_exported`）和choices。新增定义可复用这些现有事实条件；没有新增城市时间、科研或任务DSL。成功购买I/II与真实希望城材料贸易收据调用统一生成入口，视图和重载不生成奖励。测试中的第二任务仅存在于内存夹具，不写入生产内容。

`wild_technology.rewards` 按任务id保留完整选项、来源街区、领取状态与chosen；已经生成的待领/已领奖励不会随配置编辑或删除重抽。旧单一`reward`通过成功Restore后的迁移保留原id/选项/来源/领取结果，再次生成不重复。同一任务全城仅有一个记录，必须在原来源且当前可经营街区领取。

定义/选项验证覆盖唯一任务与选项id、非空且无两端空格的文本、已支持的kind/resource/bonus、有限正数金额、方案数量严格1、有效条件。领取和Restore检查持久记录有效性，不用当前定义覆盖旧奖励。失败的未知选项、重复领取、满仓和错误来源不会消费奖励。II付款前完整校验配置，避免先扣款后发现坏选项；II购买收据保存当时出口阈值，旧收据缺字段按历史20解释。

UI从同一II视图读取费用/条件/可购买状态，从统一奖励视图读取saved choices及来源/阶段可领取状态。选项按钮传入任务id与选项id。此为源码接线，不是实际Player视觉验收。

## 验证

使用仓库 `.toolcache/dotnet/dotnet`、`DOTNET_CLI_HOME=$PWD/.toolcache/dotnet-home`、`NUGET_PACKAGES=$PWD/.toolcache/nuget`，复用已有本地restore，以 `run --project tools/unity_core/<name>.Checks.csproj --no-restore -- content` 执行。日志目录：`build/rollback/wild-milestones-20261029/`。

- 原有115项先通过（`content-initial.log`）。最终扩展WildContent **177通过**（`content-final.log`）：多个配置任务、生成幂等、删除/编辑配置后待领与已领保留、旧单奖励未领/已领迁移、只读视图、错误来源、非play视图、非法类型/金额/重复id/未知方案、II付款前拒绝且整状态不变、历史可配置阈值重载；原有资源满仓与手动W01装配回归保持。
- WildTrade **59通过**；WildG0 **33通过**；WildG1 **241通过**；CityNetwork **102通过**，各命令退出0，见同名日志。
- Core退出0：Foundation17、Rules20、Simulation10、Progression127/127；额外传入独立 `build/rollback/wild-milestones-20261029/core-records` 输出目录。本轮parity checkpoints=0，不宣称全状态Godot parity。

这些夹具预置贸易/资金/容量条件，不证明自然开局到II的耗时或奖励选择的平衡。此次没有Unity实际编译/Player/2K全屏截图/鼠标键盘验收；以前证据仍属以前版本。没有执行发布。

## 精确文件及回滚

本轮先复制当前脏文件，逐项SHA256复核，才开始编辑；未重置为Git版本，未改变上一轮备份。新备份 `build/rollback/wild-milestones-20261029/original/` 保留以下8份文件：

1. `content/beta/wild.json`
2. `unity/METLivingWorld/Assets/StreamingAssets/MetContent/beta/wild.json`
3. `unity/METLivingWorld/Assets/MetCore/City/CityWildRewards.cs`
4. `unity/METLivingWorld/Assets/MetCore/City/CityWild.cs`
5. `unity/METLivingWorld/Assets/MetCore/City/CityStore.cs`
6. `unity/METLivingWorld/Assets/MetCore/City/CitySession.cs`
7. `unity/METLivingWorld/Assets/City/UI/CityWildTrade.cs`
8. `tools/unity_core/WildContentProgram.cs`

仅同步wild.json到对应StreamingAssets内容；未运行全量同步。`original_hashes.json`、`after_hashes.json`分别记录修改前后hash。`restore.py`默认只验证，`--apply`在全部当前/备份hash通过后恢复本轮8份原文件，不触碰存档；有并发修改则拒绝全部写入。

`verify_restore.py`在临时树实际演练恢复，8份字节/hash全还原；篡改当前内容后拒绝且无写入；最后实测真实工作树仍与after一致。见`restore-drill.log`。未对真实工作树执行回滚。本文及本轮日志/回滚工具为新增辅助文件，不列入8份产品/测试源文件恢复范围。
