# 0.3.2 实现与数据合同

基线：MET_0.3.1_Cardcraft_Source.zip；SHA256 b92c295e5141655dc640b9e8418a2fc1521e30de039c44198924d7c8035cf802。原始来源保留，未发布GitHub。

## 新模块
- core/civic032.gd：有界实际历史、一次性资金/DT事件、已有成果的弃接/完成状态；schema4增加可选civic032对象。
- ui/city032/models.gd：只读预算、资源、人口模型；不另算经济；状态加权包含地图外人群。
- ui/city032/desk.gd：原生时间/资源/任务dock，全屏政策/预算/任务，地图旁人口/资源/时间页。
- ui/city032/history_chart.gd：真实历史曲线；没有样本不造曲线。
- ui/city032/drag_scroll.gd：触摸和背景拖页；超过阈值发送SCROLL_BEGIN取消按钮点击，不能滑动时支付。

## 既有接入
app.gd负责任务栏、响应式安全区、手动/查看/比较暂停相互独立、031旧城复制导入。dialogs旧入口路由新页面；workbench小屏使用真实SVG返回/详情图标。map_data/map_view增加住房+岗位的联合分析表达，原字段和分配逻辑保留。

## 真实记录时序
simulation每个实际周期记经营net*span与人口变化；arrival日志在cycle recording guard内，不把全部经营净流误记成“迁入收入”。成果奖金发生在经营记录之后，进入一次性记录，finalize将余额和DT修正为该实际周期的最终值。preview_mode不写任何历史，快照/RNG/费用不变化。

Game.log_event连接一次性delta账本；Civic.finalize更新余额锚点，避免下一次政策操作吞进之前经营结果。重抽、新建筑、重签、改型、运营者变更、政策、离线DT和危机整改使用原动作与实际金额。付款失败/仅查看不写假消费。

## 任务
沿用四个既有自动社区成果与一个当前章节方向。eligible检查接入Progress.award与章节完成；Progress.deliver还要求当前正在追求交付方向，防止放弃后绕过。没有重构任务成新的内容游戏。既有奖项不重发；旧完成日期unknown。UI进度读取真实状态，不用新开局默认food=1绘满任务条。

## 暂停
settings.user_paused持久化。advance_online与Sim.advance均拦截暂停，入口还拦截决策/信息页。系统日期使用Time.get_datetime_dict_from_system，按设备时区；框架继续维护全局墙钟标记，暂停不积累待补算city delta。UI信息页/政策确认临时暂停不修改manual flag。

## 回退
独立032目录；旧文件只读复制。schema4保留既有字段且验证civic032嵌套结构，损坏主档不覆盖备份。核心经济字段、所有Bonus/Policy规则与content/core020.json除版本号对照一致。

## 注意
当前预算历史720cycle、action/task event300；UI最近60，export保留全量。人口变化是最后实际周期率，不是预测。没有数据时与0明确区分。字体不打包；原生Mac渲染待实际设备检查。
