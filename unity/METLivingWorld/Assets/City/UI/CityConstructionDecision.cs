using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
public sealed partial class CityInterface {
 // Compatibility entry only. Navigation intercepts this route before archive history changes.
 void TenderPage(){var draft=S["draft"];if(draft.Count!=0)OpenConstructionWindow(draft["base"].Text,draft["tile"].Text);}
 string ConstructionPurpose(V definition){
  var stats=definition.Get("stats",V.Map());var purposes=V.Arr();
  if(stats.Get("housing",0).Double>0)purposes.Add("提供住房");
  foreach(var resource in new[]{"food","material","goods"})if(stats.Get("output."+resource,0).Double>0)purposes.Add("生产"+Resource(resource));
  if(stats.Keys.Any(key=>key.StartsWith("service.",StringComparison.Ordinal)&&stats[key].Double>0))purposes.Add("提供社区服务");
  if(stats.Get("output.energy",0).Double>0)purposes.Add("提供电力");
  return purposes.Count>0?string.Join(" · ",purposes.Select(x=>x.Text)):"建设城市设施";
 }
 void RenderConstructionOffers(){
  var draft=S["draft"];
  if(draft.Get("base","").Text!=_constructionId||draft.Get("tile","").Text!=_constructionTile||draft.Get("offers",V.Arr()).Count==0){
   _constructionTitle.text="建造方案已变化";_constructionSubtitle.text="请返回所选地块重新打开建造窗口。";Button(_constructionFooter,"返回地块",CloseConstructionWindow);return;
  }
  var offers=draft["offers"];if(!offers.Any(x=>x["id"].Text==_constructionOffer))_constructionOffer=offers[0]["id"].Text;
  var definition=G.c.projects[_constructionId];var name=definition["name"].Text;
  _constructionBaseline=_constructionPlanner.snapshot(G);
  var previews=V.Map();foreach(var offer in offers)previews[offer["id"]]=_constructionPlanner.compare(G,_constructionId,_constructionTile,offer);
  var selected=offers.First(x=>x["id"].Text==_constructionOffer);_constructionProposal=previews[_constructionOffer];
  bool wildDirect=selected.Get("wild_direct",false).Bool;
  _constructionTitle.text=wildDirect?"公共建设 · "+name:"建造 · "+name;
  _constructionSubtitle.text="地块 "+_constructionTile+"  ·  "+(wildDirect?"提供住房 · 生产食物":ConstructionPurpose(definition))+(wildDirect?"  ·  确认建设后进入施工队列":"  ·  选择运营者，再确认开工");
  if(wildDirect){
   ConstructionText(_constructionHeader,"农居公共建设　·　住房与农业基础",14,Muted);
   var panels=ConstructionColumns(_constructionBody,"Farm Public Construction",2);var purpose=panels[0];var capacity=panels[1];var config=G.c.wild;var form=config["forms"][_constructionId];
   ConstructionText(purpose,"用途与基础能力",18,Ink);
   ConstructionText(purpose,"提供住房 · 生产食物",15,Green);
   ConstructionText(purpose,"基础住房 "+F(form["housing"].Double,0)+" 人　·　食物产能 "+F(form["food"].Double)+" /月",14,Muted);
   double initial=config["initial_dp"].Double,technology=Math.Max(0,G.world_effects.Get("wild_housing_levels",0).Double)*config["technology_dp_per_level"].Double;
   ConstructionText(capacity,"初始设计容量 "+F(initial+technology,0)+" DP",18,Ink);
   ConstructionText(capacity,"基础 "+F(initial,0)+"  +  科技 "+F(technology,0),14,Green);
   ConstructionText(capacity,"完工后打开农居星图，按需安排生长节点。",14,Muted);
  }else{
   ConstructionText(_constructionHeader,"比较运营者　·　成长槽位按 T1 / T2 / T3 顺序解锁",14,Muted);
  var columns=ConstructionColumns(_constructionBody,"Operator Comparison",offers.Count);
  var costs=offers.Select(offer=>Draft.offer_cost(G,offer)).ToArray();
  bool sameCosts=costs.All(cost=>cost["currency"]==costs[0]["currency"]&&cost["material"]==costs[0]["material"]&&cost["cp"]==costs[0]["cp"]);
  for(int i=0;i<offers.Count;i++){
   var offer=offers[i];var card=columns[i];var info=G.c.operators[offer["operator_org"]];var chosen=offer["id"].Text==_constructionOffer;var id=offer["id"].Text;
   ConstructionChoice(card,"Construction Operator "+id,info["name"].Text,chosen,()=>{_constructionOffer=id;RefreshConstructionWindow();});
   ConstructionText(card,info.Get("summary","").Text,14,Muted,2);AddDeskTip(card.parent.gameObject,info["name"].Text,info.Get("summary","").Text);
   ConstructionText(card,"成长槽位",12,Muted);
   ConstructionText(card,string.Join(" → ",offer["slot_track"].Select(x=>Catalog.SLOT_NAMES.Get(x,x).Text)),14,Blue);
   if(!sameCosts)ConstructionText(card,"通货 "+F(costs[i]["currency"].Double,0)+"　材料 "+F(costs[i]["material"].Double,0)+"　CP "+F(costs[i]["cp"].Double),13,Muted);
  }
  }
  var selectedCost=Draft.offer_cost(G,selected);double cash=selectedCost["currency"].Double;
  var blocked=!_constructionProposal.Get("ok",false).Bool?_constructionProposal.Get("error","预测暂不可用").Text:S["currency"].Double<cash?"还差 "+F(cash-S["currency"].Double,0)+" 通货；可关闭窗口，经营后沿用此报价。":"";
  RenderConstructionProposal(_constructionBaseline,_constructionProposal,_constructionProposal.Get("target","").Text,selectedCost["currency"].Double,selectedCost["material"].Double,selectedCost["cp"].Double,blocked,false);
  if(!wildDirect)ConstructionFold(_constructionBody,"reroll","另找运营者 · 付费重抽",details=>{
   var rerollCost=Draft.reroll_cost(G)["currency"].Double;string expected=draft.ToJson(false,true);
   Label(details,"立即支付费用，重新征集三名运营者；重抽费用会增长且不退还。关闭窗口会保留当前报价。",15,Muted);
   Button(details,"支付 "+F(rerollCost,0)+" 通货 · 另找一组",()=>{
    if(!_constructionOpen||_constructionMode!="build")return;
    var error=Runtime.Execute(g=>g.s["draft"].ToJson(false,true)!=expected?"报价已经变化，请重新查看。":g.reroll());
    if(error=="")_constructionOffer="";RefreshConstructionWindow();
   },false,S["currency"].Double<rerollCost);
  });
  RenderConstructionImpact(_constructionBaseline,_constructionProposal,_constructionProposal.Get("target","").Text,"不建设","假定建成");
  var offerId=_constructionOffer;var expectedDraft=draft.ToJson(false,true);var expectedVersion=_constructionPlanner.version(G);
  ConstructionCosts(cash,selectedCost["material"].Double,selectedCost["cp"].Double,blocked,()=>{
   if(!_constructionOpen||_constructionSubmitting||_constructionMode!="build"||_constructionOffer!=offerId)return;
   _constructionSubmitting=true;
   var error=Runtime.Execute(g=>_constructionPlanner.version(G)!=expectedVersion||g.s["draft"].ToJson(false,true)!=expectedDraft?"城市或报价已变化，已刷新，请重新确认。":g.accept_offer(offerId));
   _constructionSubmitting=false;
   if(error!=""){RefreshConstructionWindow();return;}ConstructionSucceeded(name+"已开工。施工会随城市运行推进。");
  },"确认建造");
 }
 void RenderConstructionUpgrades(){
  var id=_constructionId;
  if(!S["projects"].Has(id)){_constructionTitle.text="建筑已不可用";Button(_constructionFooter,"返回地图",CloseConstructionWindow);return;}
  var p=S["projects"][id];var choices=Development.choices(G,id).Select(x=>x.Text).ToArray();
  _constructionTitle.text="升级 · "+G.project_name(id);_constructionSubtitle.text="地块 "+_constructionTile+"  ·  当前 T"+p["tier"].Text+"  ·  运营者、已有槽位、库存与历史继续保留";
  ConstructionText(_constructionHeader,"运营者 · "+G.c.operators[p["operator_org"]]["name"].Text+"　　成长槽位 · "+string.Join(" → ",p["slot_track"].Select(x=>Catalog.SLOT_NAMES.Get(x,x).Text)),14,Muted);
  if(!choices.Contains(_constructionUpgrade))_constructionUpgrade=choices.FirstOrDefault()??"";
  if(_constructionUpgrade==""){Label(_constructionBody,"当前没有后续升级方向。",21,Muted);Button(_constructionFooter,"返回建筑",CloseConstructionWindow);return;}
  var cards=ConstructionColumns(_constructionBody,"Upgrade Direction Comparison",choices.Length);
  var views=choices.Select(node=>Development.preview(G,id,node)).ToArray();
  bool sameCosts=views.All(view=>view["cost"]==views[0]["cost"]&&view["material"]==views[0]["material"]&&view["construction_required"]==views[0]["construction_required"]);
  var traitKeys=views.SelectMany(view=>view["gains"].Concat(view["losses"])).Select(diff=>diff["stat"].Text).Distinct()
   .Where(key=>views.Any(other=>G.c.projects[other["node"]]["stats"].Get(key,0)!=G.c.projects[views[0]["node"]]["stats"].Get(key,0)))
   .OrderBy(ConstructionTraitPriority).Take(3).ToArray();
  for(int i=0;i<choices.Length;i++){
   var node=choices[i];var view=views[i];var card=cards[i];bool chosen=node==_constructionUpgrade;
   ConstructionChoice(card,"Construction Upgrade "+node,view["name"].Text+" · T"+(p["tier"].Long+1),chosen,()=>{_constructionUpgrade=node;RefreshConstructionWindow();});
   ConstructionText(card,"新增 "+Catalog.SLOT_NAMES.Get(view["slot"],view["slot"]).Text+"槽",14,Blue);
   // Every direction uses the same trait rows so its trade-offs can be scanned horizontally.
   ConstructionText(card,"本楼额定变化",12,Muted);
   foreach(var key in traitKeys){double before=G.c.projects[p["def"]]["stats"].Get(key,0).Double,after=G.c.projects[node]["stats"].Get(key,0).Double;ConstructionTrait(card,V.Map("stat",key,"delta",after-before));}
   if(!sameCosts)ConstructionText(card,"通货 "+F(view["cost"].Double,0)+"　材料 "+F(view["material"].Double,0)+"　CP "+F(view["construction_required"].Double),13,Muted);
  }
  var selected=_constructionUpgrade;var upgrade=Development.preview(G,id,selected);
  _constructionBaseline=_constructionPlanner.snapshot(G);_constructionProposal=ConstructionUpgradeForecast(id,selected,upgrade);
  var cash=upgrade["cost"].Double;var previewError=_constructionProposal.Get("error","").Text;
  string blocked=p["status"].Text!="running"||p["upgrade_job"].Count!=0?"请等当前施工完成后再升级。":S["currency"].Double<cash?"升级经费不足，还差 "+F(cash-S["currency"].Double,0)+" 通货。":previewError;
  RenderConstructionProposal(_constructionBaseline,_constructionProposal,id,cash,upgrade["material"].Double,upgrade["construction_required"].Double,blocked,true,upgrade["operation_factor"].Double);
  RenderConstructionImpact(_constructionBaseline,_constructionProposal,id,"不升级","假定升级完工");
  ConstructionFold(_constructionBody,"upgrade-continuity","施工与保留内容",details=>{
   Label(details,"升级施工期间运营系数 "+F(upgrade["operation_factor"].Double,2)+"；工程完成后解锁下一槽。",16,Muted);
   foreach(var resource in new[]{"food","material","goods"})Label(details,"当前"+Resource(resource)+"库存 "+F(p["inventory"].Get(resource,0).Double),16,Muted);
   Raw(details,G.c.projects[selected]);Raw(details,p["history"]);
  });
  var expectedVersion=_constructionPlanner.version(G);
  ConstructionCosts(cash,upgrade["material"].Double,upgrade["construction_required"].Double,blocked,()=>{
   if(!_constructionOpen||_constructionSubmitting||_constructionMode!="upgrade"||_constructionUpgrade!=selected)return;
   _constructionSubmitting=true;var error=Runtime.Execute(g=>_constructionPlanner.version(G)!=expectedVersion?"城市已变化，预测已刷新，请重新确认。":Development.start_upgrade(g,id,selected));_constructionSubmitting=false;
   if(error!=""){RefreshConstructionWindow();return;}ConstructionSucceeded(G.project_name(id)+"已开始升级。");
  },"确认升级");
 }
 // Reuse Planning's versioned detached-state cache when selecting a direction again.
 V ConstructionUpgradeForecast(string id,string selected,V upgrade){
  _constructionPlanner.reset_for(G);string key="construction-upgrade:"+id+":"+selected;
  if(_constructionPlanner.cache.Has(key))return _constructionPlanner.cache[key];
  var clone=_constructionPlanner.copy_game(G);clone.s["projects"][id]["upgrade_choice"]=selected;
  clone.s["projects"][id]["upgrade_job"]=V.Map("work",0.0,"total",upgrade["construction_required"],"material",0.0,"cp",true);
  Development.complete(clone,id);clone.refresh_metrics();var error=Sim.tick(clone,G.c.balance["tick_hours"].Double);
  var result=V.Map("ok",error=="","error",error,"map_snapshot",_constructionPlanner.display_copy(clone));_constructionPlanner.remember(key,result);return result;
 }
 int ConstructionTraitPriority(string stat)=>stat.StartsWith("output.",StringComparison.Ordinal)||stat.StartsWith("service.",StringComparison.Ordinal)||stat=="housing"?0:stat=="jobs"?1:stat.StartsWith("storage.",StringComparison.Ordinal)?3:2;
 bool ConstructionLowerIsBetter(string stat)=>stat=="energy_demand"||stat=="maintenance"||stat=="pollution"||stat.StartsWith("input.",StringComparison.Ordinal);
 void ConstructionTrait(Transform parent,V diff){
  var key=diff["stat"].Text;double delta=diff["delta"].Double;
  var row=Row(parent,"Direction trait "+key,23*TextScale);var title=ConstructionText(row,Stat(key),13,Muted);var value=ConstructionText(row,Signed(delta),14,Math.Abs(delta)<.001?Muted:(delta>0)!=ConstructionLowerIsBetter(key)?Green:Gold);
  var titleWidth=title.GetComponent<LayoutElement>();titleWidth.minWidth=titleWidth.preferredWidth=0;titleWidth.flexibleWidth=1;var valueWidth=value.GetComponent<LayoutElement>();valueWidth.minWidth=valueWidth.preferredWidth=0;valueWidth.flexibleWidth=.6f;value.alignment=TextAnchor.MiddleRight;
 }
 void RenderConstructionProposal(V baseline,V proposal,string target,double cash,double material,double cp,string blocked,bool upgrading,double operationFactor=1){
  var panels=ConstructionColumns(_constructionBody,"Selected Proposal",2);var changes=panels[0];var limits=panels[1];
  ConstructionText(changes,"此方案的主要改变",18,Ink);ConstructionText(changes,"本区实际变化 · 两边均结算下一步",12,Muted);
  if(!proposal.Get("ok",false).Bool)ConstructionText(changes,proposal.Get("error","预测暂不可用").Text,14,Gold,2);
  else {
   var old=baseline["metrics"];var now=proposal["map_snapshot"]["metrics"];var rows=V.Arr();
   Action<string,double,double,string> add=(label,before,after,unit)=>{if(Math.Abs(after-before)>.001)rows.Add(V.Map("label",label,"before",before,"after",after,"unit",unit));};
   add("住房 · 人",old.Get("housing",0).Double,now.Get("housing",0).Double,"");
   foreach(var resource in new[]{"food","material","goods"})add(Resource(resource)+"净流/月",old.Get("flows",V.Map()).Get(resource,0).Double,now.Get("flows",V.Map()).Get(resource,0).Double,"");
   foreach(var kind in G.c.balance["service_kinds"]){string key=kind.Text;add(Service(key)+"覆盖 · 人",ConstructionServicePeople(old,key),ConstructionServicePeople(now,key),"");}
   add("供电能力",old.Get("energy",0).Double,now.Get("energy",0).Double,"");
   ConstructionCompareLine(changes,"指标","现状","完工后","变化",true);
   foreach(var row in rows.Take(4))ConstructionCompareNumber(changes,row["label"].Text,row["before"].Double,row["after"].Double,row["unit"].Text);
   ConstructionCompareNumber(changes,"经营净额/月",old.Get("cashflow",0).Double,now.Get("cashflow",0).Double,"");
   if(rows.Count>4)ConstructionText(changes,"其余 "+(rows.Count-4)+" 项变化见城市整体对比",12,Muted);
  }
  ConstructionText(changes,upgrading?"施工期间原建筑运营系数 ×"+F(operationFactor,2):"新建筑完工前不提供产出与服务。",13,Muted);
  ConstructionText(limits,"投入与当前约束",18,Ink);
  var costs=Row(limits,"Proposal cost grid",61*TextScale);var captions=new[]{"即付通货","施工材料","所需 CP"};var amounts=new[]{cash,material,cp};
  for(int i=0;i<3;i++){var cell=Column(costs,"Proposal cost "+captions[i],2);cell.GetComponent<VerticalLayoutGroup>().padding=new RectOffset();var width=cell.gameObject.AddComponent<LayoutElement>();width.minWidth=0;width.preferredWidth=0;width.flexibleWidth=1;ConstructionText(cell,captions[i],12,Muted);var amount=ConstructionText(cell,F(amounts[i],i==2?1:0),23,i==0&&cash>S["currency"].Double?Gold:Ink);amount.name="Construction Cost Amount "+captions[i];}
  var capacity=Construction.capacity_breakdown(G);double stock=G.inventory_total("material"),queued=Construction.queue(G).Sum(job=>job["material"].Double);var metrics=S["metrics"];double energy=metrics.Get("energy",0).Double,need=metrics.Get("energy_need",0).Double;
  ConstructionConstraint(limits,"材料现存 / 本次需求",F(stock)+" / "+F(material),stock<material);
  ConstructionConstraint(limits,"队列已有材料需求",F(queued),stock<queued+material);
  ConstructionConstraint(limits,"本区有效施工能力",F(capacity["local_effective"].Double)+" CP/月",capacity["local_effective"].Double<=0);
  bool powerMeasured=S.Get("simulation_tick",0).Long>0;ConstructionConstraint(limits,"当前电力 · 供给 / 需求",powerMeasured?F(energy)+" / "+F(need):"待结算",powerMeasured&&energy<need);
  ConstructionText(limits,blocked!=""?blocked:stock<queued+material?"通货足够开工。材料不足以覆盖全部队列，后续补给影响工期。":capacity["local_effective"].Double<=0?"通货足够开工；当前施工能力为零，开工后需等待恢复。":"通货足够开工。材料按进度消耗，队列共享本区施工能力。",13,blocked!=""||stock<queued+material||capacity["local_effective"].Double<=0?Gold:Muted,2);
  if(proposal.Get("ok",false).Bool)ConstructionFold(_constructionBody,"building-capacity","本楼完工能力与口径 · 展开查看",details=>{
   ConstructionText(details,"本楼能力来自完工配置与规则编译；实际覆盖还取决于居民、可达性和其他来源。",14,Muted,2);
   var before=baseline.Get("compiled",V.Map()).Get("stats",V.Map()).Get(target,V.Map());var after=proposal["map_snapshot"].Get("compiled",V.Map()).Get("stats",V.Map()).Get(target,V.Map());
   ConstructionCompareLine(details,"本楼能力","现状","完工后","变化",true);
   foreach(var key in new[]{"housing","jobs","output.food","output.material","output.goods","output.energy","energy_demand","service.medical","service.education","service.entertainment","service.commercial"}){
    double a=before.Get(key,0).Double,b=after.Get(key,0).Double;if(Math.Abs(a)+Math.Abs(b)<.001)continue;ConstructionCompareNumber(details,Stat(key),a,b,"");
   }
  });
 }
 double ConstructionServicePeople(V metrics,string kind)=>metrics.Get("service_links",V.Arr()).Where(link=>link["kind"].Text==kind).Sum(link=>link["people"].Double);
 void ConstructionConstraint(Transform parent,string label,string value,bool warning){
  var row=Row(parent,"Construction constraint "+label,27*TextScale);var caption=ConstructionText(row,label,13,Muted);var amount=ConstructionText(row,value,14,warning?Gold:Ink);var captionWidth=caption.GetComponent<LayoutElement>();captionWidth.minWidth=captionWidth.preferredWidth=0;captionWidth.flexibleWidth=1.4f;var amountWidth=amount.GetComponent<LayoutElement>();amountWidth.minWidth=amountWidth.preferredWidth=0;amountWidth.flexibleWidth=1;amount.alignment=TextAnchor.MiddleRight;
 }
 void RenderConstructionImpact(V baseline,V proposal,string target,string beforeLabel,string afterLabel){
  if(!proposal.Get("ok",false).Bool){Label(_constructionBody,proposal.Get("error","预测暂不可用").Text,18,Gold);return;}
  ConstructionFold(_constructionBody,"city-impact","所选方案 · 同一步城市变化（展开查看）",details=>RenderConstructionImpactDetails(details,baseline,proposal,target,beforeLabel,afterLabel));
 }
 void RenderConstructionImpactDetails(Transform parent,V baseline,V proposal,string target,string beforeLabel,string afterLabel){
  var after=proposal["map_snapshot"];var old=baseline["metrics"];var now=after["metrics"];
  Label(parent,"两边均结算下一步。假定项目已完工，不扣本次建设费、不模拟施工等待；实际库存另列。",14,Muted);
  ConstructionCompareLine(parent,"指标",beforeLabel,afterLabel,"变化",true);
  foreach(var resource in new[]{"food","material","goods"})ConstructionCompareNumber(parent,Resource(resource)+"净流量",old.Get("flows",V.Map()).Get(resource,0).Double,now.Get("flows",V.Map()).Get(resource,0).Double," /月");
  ConstructionCompareNumber(parent,"经营净额",old.Get("cashflow",0).Double,now.Get("cashflow",0).Double," 通货/月");
  ConstructionCompareNumber(parent,"供电能力",old.Get("energy",0).Double,now.Get("energy",0).Double,"");
  ConstructionCompareNumber(parent,"用电需求能力",old.Get("energy_need",0).Double,now.Get("energy_need",0).Double,"");
  ConstructionCompareNumber(parent,"住房容量",old.Get("housing",0).Double,now.Get("housing",0).Double," 人");
  foreach(var kind in G.c.balance["service_kinds"]){
   string key=kind.Text;double before=old.Get("service_links",V.Arr()).Where(x=>x["kind"].Text==key).Sum(x=>x["people"].Double),value=now.Get("service_links",V.Arr()).Where(x=>x["kind"].Text==key).Sum(x=>x["people"].Double);
   ConstructionCompareNumber(parent,Service(key)+"实际覆盖",before,value," 人");
  }
  ConstructionCompareNumber(parent,"全城平均污染场强",ConstructionPollution(baseline),ConstructionPollution(after),"",3);
  ConstructionCompareNumber(parent,"污染场强 >0.001 的地格占比",ConstructionPollutedArea(baseline),ConstructionPollutedArea(after),"%",1);
  var exposure=0.0;foreach(var gid in baseline["groups"].Keys){var group=after["groups"].Get(gid,V.Map());if(group.Count!=0&&group.Get("living",V.Map()).Get("pollution",0).Double-baseline["groups"][gid].Get("living",V.Map()).Get("pollution",0).Double>.015)exposure+=baseline["groups"][gid]["count"].Double;}
  Label(parent,"污染暴露上升的现有居民："+F(exposure,0)+" 人（暴露增加超过 0.015）；面积与平均场强均以全城全部地格为分母。",14,exposure>0?Gold:Muted);
  var beforeTrace=baseline.Get("compiled",V.Map()).Get("trace",V.Map());var afterTrace=after.Get("compiled",V.Map()).Get("trace",V.Map());
  double nextSynergy=afterTrace.Get(target,V.Map()).Get("spatial_synergy",V.Map()).Get("value",1).Double;
  if(baseline["projects"].Has(target))ConstructionCompareNumber(parent,"本楼相邻加成倍率",beforeTrace.Get(target,V.Map()).Get("spatial_synergy",V.Map()).Get("value",1).Double,nextSynergy,"×",2);
  else ConstructionCompareLine(parent,"本楼相邻加成倍率","尚无建筑",F(nextSynergy,2)+"×","相邻增益 "+Signed((nextSynergy-1)*100)+"%");
  ConstructionFold(parent,"adjacency","相邻建筑、投入竞争与完整计算来源",details=>{
   foreach(var contributor in afterTrace.Get(target,V.Map()).Get("spatial_synergy",V.Map()).Get("contributors",V.Arr()))Label(details,"相邻来源："+G.project_name(contributor["source_id"].Text)+" · 地块 "+contributor["tile"].Text+" · 效率 "+Signed(contributor["amount"].Double*100)+"%",16,Blue);
   foreach(var building in baseline["projects"].Keys){
    if(!after["projects"].Has(building)||building==target)continue;
    double before=old.Get("utilization",V.Map()).Get(building,V.Map()).Get("utilization",0).Double,value=now.Get("utilization",V.Map()).Get(building,V.Map()).Get("utilization",0).Double;
    double priorSynergy=beforeTrace.Get(building,V.Map()).Get("spatial_synergy",V.Map()).Get("value",1).Double,synergy=afterTrace.Get(building,V.Map()).Get("spatial_synergy",V.Map()).Get("value",1).Double;
    if(Math.Abs(value-before)>.001||Math.Abs(synergy-priorSynergy)>.001)Label(details,G.project_name(building)+"：运行率 "+F(before*100)+"% → "+F(value*100)+"% · 相邻倍率 "+F(priorSynergy,2)+" → "+F(synergy,2),15,value>=before?Muted:Gold);
   }
   Label(details,"所选项目的实际投入、产出与规则",18,Blue);Raw(details,now.Get("activity_ledger",V.Map()).Get(target,V.Arr()));Raw(details,afterTrace.Get(target,V.Map()));
   Label(details,"同一步现状来源",18,Blue);Raw(details,baseline);Label(details,"同一步完工后来源",18,Blue);Raw(details,after);
  });
  ConstructionFold(parent,"inventory","当前库存与仓容（非预测库存）",details=>{foreach(var resource in new[]{"food","material","goods"})Meter(details,Resource(resource),G.inventory_total(resource),G.stock_cap(resource)," 单位",Blue);});
 }
 double ConstructionPollution(V snapshot){var tiles=snapshot.Get("tiles",V.Map());return tiles.Count==0?0:tiles.Values.Sum(tile=>Rules.aggregate(tile.Get("fields",V.Map()).Get("pollution",V.Arr())))/tiles.Count;}
 double ConstructionPollutedArea(V snapshot){var tiles=snapshot.Get("tiles",V.Map());return tiles.Count==0?0:100.0*tiles.Values.Count(tile=>Rules.aggregate(tile.Get("fields",V.Map()).Get("pollution",V.Arr()))>.001)/tiles.Count;}
 void ConstructionCompareNumber(Transform parent,string title,double before,double after,string unit,int digits=1){
  double delta=after-before;bool lower=title.Contains("污染")||title.Contains("用电需求");var color=Math.Abs(delta)<.001?Muted:(delta>0)!=lower?Green:Gold;
  ConstructionCompareLine(parent,title,F(before,digits)+unit,F(after,digits)+unit,Signed(delta,digits)+unit,false,color);
 }
 void ConstructionCompareLine(Transform parent,string title,string before,string after,string delta,bool heading=false,Color? deltaColor=null){
  var row=Row(parent,"Construction Comparison "+title,29*TextScale);row.GetComponent<HorizontalLayoutGroup>().spacing=6;var fields=new[]{title,before,after,delta};
  for(int i=0;i<fields.Length;i++){var text=ConstructionText(row,fields[i],heading?12:14,heading?Muted:i==0?Muted:i==3?deltaColor??Ink:Ink);var element=text.GetComponent<LayoutElement>();element.minWidth=0;element.preferredWidth=0;element.flexibleWidth=i==0?2.2f:1;if(i>0)text.alignment=TextAnchor.MiddleRight;}
 }
}
}
