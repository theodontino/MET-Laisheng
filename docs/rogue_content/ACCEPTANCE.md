# 街区构筑与永久成果 · content.1 本地验收

2026-10-14，`0.6.0-content.1-local` 已实际编译、运行和打包。接续 [本轮合同](CONTRACT.md)，只交付这一轮，不推送、发布或开启下一轮。现有城市、此前视觉与 CityArc 修改保留；所有验证使用隔离测试目录。

本轮新增近邻装配、厂区防护缓冲、邻里照护网络、街坊夜校四张方案及两级蓝图，并补齐热量管理的两级蓝图。基础方案 40→44，蓝图系列 12→17，主题议案 15→21。六组议案各有两项带真实成本的选择及免费婉拒；已有研究奖励须先处理，缺少可研究的已拥有方案时不能付款接受研究分支。

首次 DT 实际归零锁定最近模拟切片的已部署实例、实际运行等级和正向贡献。服务使用真实分配与合池成员份额，污染/余热使用真实转换与来源收据，保护规则必须命中有居民的目标。刚部署或升级但尚未运行的方案不能抬高成果。挂牌后，每个来源街区可以单选一项长期成果；原地标留在原 Block。匹配方向的新区域可付费采用规约，来源扣款和目标交付只发生一次，收益与维护、产能或住房代价一起生效。

| 新正常命令路线 | 实际构筑和终局 | 后续选择 |
| --- | --- | --- |
| 工业 | 真实招标、投资和升级，赚取部署 P06/P09，工业主题交付完成，真实 DT0 锁二级循环材料工艺；实际污染回收率 1.5391 | 原地工会总部；后续工业街区支付启动总额及规约额外投入，材料增益伴随制成品和维护代价 |
| 安居 | 真实制造、农场、医院、商业、公园、双中学与住宅投资，实际移民和 B10 奖励，安居主题完成，真实 DT0 锁二级居住学园；实际教育覆盖 83.67% | 原地管委会；后续科研街区付费采用教育/知识规约，同时减少住房容量 |

两条路线各 **11 项通过**，均从 fresh city 使用实际命令、生产、奖励与有限 DT 运行，没有注入库存、完成主题或终局资格。[工业完整证据](evidence/content-1/industrial-normal.json)、[安居完整证据](evidence/content-1/civic-normal.json) 含实际待选议案、归零和接续付款前的存档。它们是逻辑时间命令验收，不是玩家体验或真人节奏验收。

[专项夹具](evidence/content-1/fixtures.json) **297/297 通过**：四卡的距离、运营者、供电与容量取舍，1/3 副本追加和两级蓝图，12议案分支真扣款/回读/拒绝重复，研究资格及队列，四种成果与保存，负贡献/远处方案排除，尚未运行部署和升级不追补，R05正向医疗贡献，转换及志愿教育后的最终容量增益和真实住房代价。余热和照护成果的证明属于明确准备的夹具，不能称为这两条正常经营路线自然形成的成果。

回归重新执行：核心 **174/174**，[Godot/C# 完整状态对照 88 个检查点](evidence/content-1/core-regression.log)；[地区治理 42 项](evidence/content-1/governance-regression.log)、[区域经济 102 项](evidence/content-1/network-regression.log)、[原 CityArc 完整链 27 项](evidence/content-1/city-arc-regression.log)。旧视觉 241 项没有作为新内容验收。

Unity **6000.6.5f1** 实际 Editor 编译及通用 Player 构建成功。[最终 Mac 内容报告](evidence/content-1/city-content-validation.json) **107/107、0 errors**，Apple M5 Max / Metal / **1920×1080 全屏**。从上述新命令链的真实检查点复用城市，通过实际 uGUI 回调选择分支、扣除通货和库存、挂牌、选成果、选择规约、支付并建立新街区，随后导出与回读。新议案优先显示，两项方案与婉拒并列；百分比与真实规则一致。这是自动化回调验收，没有冒充真人点击。

实机图：[议案及成本](evidence/content-1/content-event-industrial_byproducts.png) · [工业结算](evidence/content-1/content-industrial-ending.png) · [安居成果](evidence/content-1/content-civic-result.png) · [二级学园规约](evidence/content-1/content-civic-startup.png)。运行中发现并修复了验收定位误点“科研与工业”导航、议案被长进度页挤到下方和百分比显示问题；失败中间报告留在 `.toolcache/rogue-content/native/content-run-1`。

Astra 独立审查发现的无贡献提级、付费研究降成副本、学园容量收益反转和卡名错误已修复，最终限定复查未发现剩余实质阻塞。环路接头的小圆角补丁保留；不再继续 v16/v17 视觉打磨。相关旧视觉复测末段因窗口失焦未完成，不能当作本轮内容或常速性能证明。

本地 app：`build/MET Rogue Content.app`。ZIP：`build/MET-Laisheng-0.6.0-content.1-local-macOS-universal.zip`，**72,120,369 bytes**。[打包验收](evidence/content-1/package-check.json) 已实际解压，确认版本、arm64/x86_64、可执行权限、内容与来源一致及深度严格 ad-hoc 签名。Intel 没有实机执行，未做 Developer ID 签名或 Apple 公证。来源为现有 `codex/met-beta-v3` checkout、基于 `72aee5b` 的保留未提交工作；未创建发布或源提交。

本轮未进行真人长时间平衡测试、新的正常占用/GPU基准或真实系统失焦切换验收；不据此声称 Civ6 美术目标或长期性能完成。旧终局没有本轮首次零证据时不回溯资格，原存档不静默重置。
