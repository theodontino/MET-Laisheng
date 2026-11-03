# 三轮候选，尚未接入
所有新增参数为试验值，不是用户历史定案，20/90未做自然经济验证。

A 住房设计 II：依赖 HousingDesignI；希望城 hope、material、sell 的真实结算累计20；价格90 currency；科技容量再+2。I的10/60不变、不重复扣材料。现有 CityWild.cs 的 wild_technology.material_exported 可复用；housing_levels 当前只允许0/1，purchase是单记录，BuyWildHousing固定赋1。因此不能提交可加载II JSON。需单写者扩展所有权/逐科技购买来源和校验、WildView/购买入口；ApplyWorldEffects由所有权重算，保证先买后建一致、重载不累加、付款和所有权原子一致。

B 农闲整备 Organization：无未完成升级施工食物+15%，暂停施工也算未完成，权威结算时取状态。就近供给 Link：既有有效联系中存在有效旧作坊/货运形态，本建筑食物+15%，稳定def标识、存在性判断、不按邻居叠乘。Rules.condition现有 traits/any_traits/tile_traits/same_provider/field_min 不足以表达这两个条件，均标暂不可直接实现；不得用安装门槛代替动态条件，不新造关系系统。需先核有效联系的权威查询接口再实现条件。无基础食物不得产食物；融合兼容规则不扩展。

现有规则可表达的仅是数值效果片段：见 effect_fragment.json.txt。它不是完整Bonus，不得直接加载或安装；条件缺失时禁止接入。

C 现有 Bonus.draw 记录 reward_id/source/options/choices/claimed/chosen_bonus；Commissions完成调用Bonus.draw。当前卡池为Bonus/研究及空池通货补偿，不能直接表达材料/基础制成品/Bonus混合三选一。资源数量暂不定：现有奖励模型无此资源分支，不能凭旧通货补偿换算。单写者增加有限真实任务的资源选择与持久化发放后，再确定数量并自然验收。三项在生成时保存，重试重载不重抽、领取与库存同提交；Bonus入库存且手动装配。至少两项当下可用；效果暂不触发不等于不能装配，无槽必须标未来储备。

保留槽3/9/27、节点边、Tier与取消规则、农居/公寓/农场命名、旧贸易。只农居迁Wild；作坊沿旧逐级路线升货运，邻里不迁。拒绝根直升、当前3DP死锁说法、无限低工供应与硬保留最后低工。
