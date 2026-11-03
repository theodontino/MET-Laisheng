# Wild 内容增量：2026-10-29 实际验证

此轮为既有 Wild/Beta 游戏的本地增量，未推送、发布或打包交付。住房设计 II 的 20 材料累计真实出口/90 通货、材料10/基础制成品1/W01三选一均为本轮试验值，不能作为自然经济平衡已通过的证据。W02未接入。

## 核心执行

命令环境：仓库 `.toolcache/dotnet/dotnet`，`DOTNET_CLI_HOME=$PWD/.toolcache/dotnet-home`、`NUGET_PACKAGES=$PWD/.toolcache/nuget`；restore 使用本地 `.toolcache/nuget` 源及 `-p:NuGetAudit=false`。执行 `dotnet run --project tools/unity_core/<name>.Checks.csproj --no-restore -- content`。日志位于 `build/rollback/wild-content-20261029/`。

- WildContent：115项通过（`test.log`）。真实订单/结算收据累计20材料；II扣90且不重复扣出口材料；重复购买全状态不变；重载、反复投影保持6DP。
- 无新增II/奖励字段的0级与1级旧结构均经过实际 Restore，0级不获得奖励、1级保持原技术与出口进度。此为隔离生成旧结构夹具，不是逐一加载用户历史存档，更不保证Godot/Unity存档兼容。
- 两资源奖励分别验证满仓拒领且整份快照不变、准确发放10/1、重载后拒绝重复且全状态不变。
- W01领取增加一份库存、无自动安装；重载拒绝重复。通过真实 Sim/Construction 队列完成 compound→housing→organization，再调用手动安装API，消耗一份库存，食物乘1.15。未完成升级时移除加成，暂停未完成升级仍禁用，任务清空后恢复。
- 既有 WildTrade：59项通过；WildG0：33项通过；WildG1：241项通过；CityNetwork：102项通过。见各同名日志。
- 通用Core：Foundation 17、Rules20、Simulation10、Progression127/127通过，退出0；本轮未跑Godot全状态parity（checkpoints=0）。Core需额外records参数，实际使用 `content build/rollback/wild-content-20261029/core-records`。首次漏此参数曾在Main读取args[1]时失败，补全独立目录参数后通过，未改产品代码。

### 本轮失败的诊断与修正

旧夹具在第二笔卖单后一次调用 `SettleNetwork(36000)`。该方法先按10小时补充外部库存，再结算订单。实际诊断为订单 filled=0、reason=`库存、库容或结算资金不足`，外部材料仓240/240，本地材料70；累计出口仍10是正确结果。夹具改为按实际运力计算恰好10材料的结算窗口，并断言第二单确实filled=10。没有修改贸易规则、注入成交收据或绕过出口断言。

夹具明确预置货运站、运行usage、资源和资金；手动安装检查另预置施工吞吐与库存。它验证结算与持久化，不证明从自然开局达到II或奖励的耗时、经济可达性和选择质量。自然路径、至少两项奖励当下可用的玩家体验仍待验收。

## Unity 与展示边界

将 Assets/Packages/ProjectSettings 复制到独立 `/tmp/met-wild-content-unity-20261029`，执行 Unity 6000.6.5f1 batchmode/nographics/quit，未启动原工程Editor或Player。首次沙箱执行因UPM本地IPC socket EPERM失败（`unity-compile.log`）。获准扩大执行权限后，隔离进程出现 Licensing Client `ObjectDisposedException`、`Licensing is not yet initialized`（`unity-compile-isolated.log`）；主动中断该自建进程，退出130。没有实际编译成功证据，也没有真实鼠标键盘、2560×1440全屏截图或Player验收。不得把.NET核心通过等同于Unity UI完成。

### 许可恢复与实际 Unity 编译（后续执行）

未把前次失败当作最终阻塞。获准执行只读进程检查后，确认仅剩 Licensing Client PID95130，无 Unity Editor、Player 或 Hub。首轮 `unity-compile.log` 明确记载此 PID 由本轮隔离测试启动；官方 Licensing Client 日志显示后续 PID95191/95319 因 global mutex 被其他实例占用而失败。仅对本轮自建的孤儿 PID95130 发送 TERM，未结束用户 Editor，未读取许可证/凭据，未改变账号或激活设置。

随后使用相同 Unity 6000.6.5f1、相同隔离工程，正常执行 `-batchmode -nographics -quit`：日志 `unity-compile-recovered.log` 显示许可 entitlement 成功解析、实际脚本编译和 AssetDatabase 导入，最终 `Batchmode quit successfully invoked`，进程退出 **0**。本次没有 `error CS`；此前 TagManager parser failure 未重现，未修改该文件。日志仍有第三方 XCharts 编译警告及退出时 Curl callback aborted，不能宣称全日志无警告。

`unity-recovery-hashes.json` 实测原工作树全部10份目标文件与 `after_hashes.json` 一致，隔离工程对应6份 Unity 文件也一致。此次未修改产品源码，无需刷新源文件基线。编译运行采用 NullGfxDevice，不构成 Metal Player 或截图/鼠标键盘验收。

真实 UI 验收仍未完成：本轮通过 CUA 请求访问 `Unity Hub`，等待约158秒后工具返回 `aborted by user`，没有获得应用界面，也没有确认登录/许可弹窗。已停止该 UI 路径，未通过其他应用或输入方式绕过。继续住房设计II、任务三选一、手动安装与隔离存档重开，以及2560×1440全屏截图，需要重新允许相应游戏/Unity应用访问；当前没有证据要求用户重登或重新激活。

## 备份与恢复实测

`build/rollback/wild-content-20261029/` 保存7份原源码/内容与3个新增文件标记。`after_hashes.json`记录最终10文件，`original_hashes.json`记录备份完整性；原先3项基线hash复核通过，另外4份既存备份本轮补录hash。刷新最终hash前确认只有本轮夹具发生预期变动，其余9项与上一轮记录一致。

`restore.py`默认只校验；`--apply`只恢复本轮源码，把新增文件移到`removed-new`，不自动恢复存档。先校验全部当前文件与备份hash、路径和归档冲突，任何不符即拒绝，避免覆盖后续并发修改。

可复跑脚本`verify_restore.py`，真实执行记录见`restore-drill.log`：在独立临时树执行`--apply`，7原文件字节/hash完全还原，3新文件归档；分别篡改当前源码和备份，均拒绝且没有任何目标写入。最后复核真实工作树10文件仍为最终hash。未对真实工作树执行回滚。

`saves_manifest.json`记录16个备份目录到 `/Users/lizhirui/Library/Application Support/<同名目录>` 的映射及逐文件SHA256，来源目录均存在。备份含各历史Godot/Unity版本、显示设置和日志，并非全部都是当前可加载游戏存档。源码恢复和存档恢复是两项不同操作：先退出对应游戏/Editor，人工选择匹配版本与目录，保留现状副本后才恢复相应旧档。II新档不可直接交给不认识II的旧代码；本轮没有写回任何用户存档，也未演练覆盖真实用户存档。

### 主代理补充观察
主代理并发发起的 Unity Hub getApp 调用耗时281秒后返回“Hub is starting”窗口；此前子代理同类请求已返回用户中止，主代理获知后未继续任何UI操作。此启动中窗口不构成游戏界面或许可状态验收。最终再次运行 restore.py 默认dry run：10份源码及原备份hash校验通过。

## Player follow-up (actual execution)
Official isolated BuildMac succeeded, exit0. App: /tmp/MET Wild Content 20261029.app, universal arm64/x86_64, deep/strict ad-hoc signature passed, not notarized. Evidence: player-build.log and player-build-hashes.json. Existing Hub binding worked without a repeated Hub access request.
First sandbox command-line Player exited134. CUA getApp then automatically launched the same app without the isolation environment, displaying a loaded default city. No successful gameplay action was confirmed; default save lifecycle writes cannot be excluded. No automatic restoration overwrote that save. Only this known test PID98599 was terminated.
A subsequent approved normal command-line launch used MET_UNITY_SAVE_ROOT=/tmp/met-wild-content-player-save and created its met_city_v1.json. Actual screenshot showed the origin/create-first-district screen. Log confirms Apple M5 Max / Metal. Requested fullscreen2560x1440, but actual Metal surface was3456x2168; therefore 2K acceptance is NOT established. No system display settings changed.
CUA first click/read timed out, then ScreenCaptureKit -3811 capture failure. After isolated restart screenshots worked, but clicking create-first-district returned noWindowsAvailable; outcome uncertain, no blind retries. II purchase, three-choice reward, manual installation and save/reopen UI remain unverified. Natural play reached only origin screen; no economic reachability/timing conclusion. Actual log: player-isolated.log.
Final restore.py dry run passed all10 source and original-backup hashes. No product source changes.

## Default-save risk closure
Read-only audit confirms default save was written, not unchanged. Current main and current .bak differ only in three timestamps, but both are post-launch. Earlier16-directory backup omitted the real bundle-ID save directory, so full pre-launch delta cannot be proven. See [precise audit](DEFAULT_SAVE_AUDIT_20261029.md) and preserved copies under rollback/default-save-audit. No restoration was performed.
