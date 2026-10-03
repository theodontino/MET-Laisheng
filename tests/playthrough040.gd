extends SceneTree
## A single-Block route through actual tenders, CP, commissions, freight and naming.
const Game=preload("res://core/game.gd")
const Store=preload("res://core/storage.gd")
const Paths=preload("res://tests/paths.gd")
var g=Game.new()
var errors=[]
var steps=[]
func must(ok:bool,label:String):
	steps.append({"step":label,"ok":ok,"h":g.s.get("local_hours",0),"currency":g.s.get("currency",0),"dt":g.s.get("dt",0)})
	if not ok:errors.append(label);push_error(label)
func action(e:String,label:String):must(e=="",label+(" : "+e if e!="" else ""))
func build(base:String,tile:String,org:String="commons") -> String:
	action(g.create_tender([tile],base),"招标 "+base)
	if g.s.draft.is_empty():return ""
	var selected=g.s.draft.offers[0].id
	for offer in g.s.draft.offers:
		if offer.operator_org==org:selected=offer.id
	action(g.accept_offer(selected),"建设 "+base)
	return str(g.s.cells[tile].building)
func advance(hours:float):
	var result=g.pulse(hours)
	must(float(result.advanced)>=hours-.001,"推进 %.1fh"%hours)
	must(g.validate_state(g.s)=="","周期状态有效")
func claim_one():
	for reward in g.s.block.reward_choices:
		if not str(reward.get("selected","")).is_empty() or reward.get("claimed",false):continue
		var ids=reward.get("bonus_ids",reward.get("choices",[]))
		if ids.is_empty():continue
		var bid=ids[0]
		for card in ids:
			if card is Dictionary and bool(card.get("deployable_now",false)):bid=card;break
		if bid is Dictionary:bid=bid.get("bonus_id",bid.get("id",""))
		action(g.Bonus.choose(g,str(reward.get("reward_id",reward.get("id",""))),str(bid)),"领取委托许可")
		for project in g.playable_ids():
			for slot in g.s.projects[project].slots:
				if g.bonus_eligible(str(bid),"building",project,slot)=="":
					action(g.Bonus.install(g,str(bid),"building",project,slot),"将许可部署到实际建筑")
					return
		return
func _initialize():
	g.new_game(7040,1000000.);action(g.select_block(0),"选择登陆点");action(g.choose_origin(g.s.origin_choices[0]),"安顿首批居民")
	g.s.settings.user_paused=false
	action(g.Progress.select(g,"delivery"),"选择供给之城目标")
	action(g.Commissions.accept(g,"food_supply"),"接取稳定口粮")
	build("homestead","-1,1")
	build("neighborhood","0,2")
	var freight=build("workshop","1,1","river")
	if freight=="":
		_finish();return
	advance(2.5);claim_one()
	action(g.Commissions.accept(g,"settlement"),"接取新邻里安居")
	action(g.Development.start_upgrade(g,freight,"yard"),"作坊升级料场")
	advance(2.);claim_one()
	action(g.Commissions.accept(g,"industrial_delivery"),"接取限时工业交付委托")
	action(g.Development.start_upgrade(g,freight,"freight"),"料场升级货运站")
	advance(5.)
	must(g.s.projects[freight].def=="freight","三级货运站完工")
	must(g.s.projects[freight].slots.size()==3,"三级逐步解锁三槽")
	var q=g.Market.quote(g,"material")
	action(g.Market.order(g,"material","sell",10,float(q.sell)-5),"下达材料出口订单")
	advance(1.)
	must(not g.s.block.trade_history.is_empty(),"订单形成实际成交")
	q=g.Market.quote(g,"goods")
	action(g.Market.order(g,"goods","buy",16,float(q.buy)+5),"购买补足交付的生活用品")
	advance(1.)
	q=g.Market.quote(g,"material")
	action(g.Market.order(g,"material","buy",24,float(q.buy)+8),"补足发展交付材料")
	advance(2.5)
	while g.inventory_total("goods")<12 and g.s.dt>2 and g.s.local_hours<35:advance(1.)
	var goods_before=g.inventory_total("goods")
	action(g.Commissions.deliver(g),"真实交付工业委托的8制成品")
	must(absf(goods_before-g.inventory_total("goods")-8)<.001,"工业委托确实扣除物资")
	advance(.1);claim_one()
	must("industrial_delivery" in g.s.block.commissions.completed,"工业委托完成并且只发一次奖励")
	action(g.Progress.deliver(g),"实际交付城市目标物资")
	advance(.2)
	must(g.s.city_goal.completed,"Block Goal完成")
	action(g.Block.name_block(g,"河谷先行区"),"使用地区命名权")
	must(g.s.totals.installed>=1,"完成许可实际部署")
	var saved=Store.encode(g.s)
	var twin=Game.new();var loaded=twin.migrate_state(Store.decode(saved).state)
	must(loaded.ok,"当前版本城市可保存恢复")
	must(Store.encode(twin.s)==saved,"保存恢复后保持完整状态")
	var before=float(g.s.local_hours)
	advance(.5)
	must(g.s.local_hours>before and g.s.phase=="play","目标完成后继续经营单Block")
	must(not g.s.debug_used,"路线未使用调试补给")
	_finish()
func _finish():
	var out={"passed":errors.is_empty(),"errors":errors,"steps":steps,"state":g.s}
	var f=FileAccess.open(Paths.file("playthrough040.json"),FileAccess.WRITE);f.store_string(JSON.stringify(out,"\t"));f.close()
	print("PLAYTHROUGH040 "+str(steps.size()-errors.size())+"/"+str(steps.size()))
	quit(0 if errors.is_empty() else 1)
