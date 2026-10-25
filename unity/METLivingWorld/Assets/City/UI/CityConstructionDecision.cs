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
  _constructionTitle.text="建造 · "+name;_constructionSubtitle.text="地块 "+_constructionTile+"  ·  "+ConstructionPurpose(definition)+"  ·  选择运营者，再确认开工";
  _constructionBaseline=_constructionPlanner.snapshot(G);
  var previews=V.Map();foreach(var offer in offers)previews[offer["id"]]=_constructionPlanner.compare(G,_constructionId,_constructionTile,offer);
  var selected=offers.First(x=>x["id"].Text==_constructionOffer);_constructionProposal=previews[_constructionOffer];
  Label(_constructionHeader,"选中运营者："+G.c.operators[selected["operator_org"]]["name"].Text+"  ·  T1 → T2 → T3："+string.Join(" → ",selected["slot_track"].Select(x=>Catalog.SLOT_NAMES.Get(x,x).Text)),17,Green,32*TextScale);
  var columns=ConstructionColumns(_constructionBody,"Operator Comparison",offers.Count);
  for(int i=0;i<offers.Count;i++){
   var offer=offers[i];var card=columns[i];var info=G.c.operators[offer["operator_org"]];var forecast=previews[offer["id"]];var cost=Draft.offer_cost(G,offer);var chosen=offer["id"].Text==_constructionOffer;
   Label(card,info["name"].Text,21,chosen?Green:Ink);Label(card,ConstructionPurpose(definition),15,Muted);
   Label(card,"槽轨："+string.Join(" → ",offer["slot_track"].Select(x=>Catalog.SLOT_NAMES.Get(x,x).Text)),16,Blue);
   Label(card,info.Get("summary","").Text,14,Muted);
   Label(card,ConstructionGainSummary(_constructionBaseline,forecast),15,forecast.Get("ok",false).Bool?Green:Gold);
   Label(card,"即付 "+F(cost["currency"].Double,0)+" 通货 · "+F(cost["material"].Double,0)+" 材料 / "+F(cost["cp"].Double)+" CP",14,Muted);
   var id=offer["id"].Text;var choose=Button(card,chosen?"✓ 已选方案":"选择此方案",()=>{_constructionOffer=id;RefreshConstructionWindow();},chosen);choose.name="Construction Operator "+id;
  }
  ConstructionFold(_constructionBody,"reroll","另找运营者 · 付费重抽",details=>{
   var rerollCost=Draft.reroll_cost(G)["currency"].Double;string expected=draft.ToJson(false,true);
   Label(details,"立即支付费用，重新征集三名运营者；重抽费用会增长且不退还。关闭窗口会保留当前报价。",15,Muted);
   Button(details,"支付 "+F(rerollCost,0)+" 通货 · 另找一组",()=>{
    if(!_constructionOpen||_constructionMode!="build")return;
    var error=Runtime.Execute(g=>g.s["draft"].ToJson(false,true)!=expected?"报价已经变化，请重新查看。":g.reroll());
    if(error=="")_constructionOffer="";RefreshConstructionWindow();
   },false,S["currency"].Double<rerollCost);
  });
  RenderConstructionImpact(_constructionBaseline,_constructionProposal,_constructionProposal.Get("target","").Text,"不建设","假定建成");
  var selectedCost=Draft.offer_cost(G,selected);double cash=selectedCost["currency"].Double;
  var blocked=!_constructionProposal.Get("ok",false).Bool?_constructionProposal.Get("error","预测暂不可用").Text:S["currency"].Double<cash?"还差 "+F(cash-S["currency"].Double,0)+" 通货；可以关闭窗口，经营后沿用此报价。":"";
  var offerId=_constructionOffer;var expectedDraft=draft.ToJson(false,true);var expectedVersion=_constructionPlanner.version(G);
  ConstructionCosts(cash,selectedCost["material"].Double,selectedCost["cp"].Double,blocked,()=>{
   if(!_constructionOpen||_constructionSubmitting||_constructionMode!="build"||_constructionOffer!=offerId)return;
   _constructionSubmitting=true;
   var error=Runtime.Execute(g=>_constructionPlanner.version(G)!=expectedVersion||g.s["draft"].ToJson(false,true)!=expectedDraft?"城市或报价已变化，已刷新，请重新确认。":g.accept_offer(offerId));
   _constructionSubmitting=false;
   if(error!=""){RefreshConstructionWindow();return;}ConstructionSucceeded(name+"已开工。施工会随城市运行推进。");
  },"开始建造 · "+F(cash,0)+" 通货");
 }
 string ConstructionGainSummary(V baseline,V proposal){
  if(!proposal.Get("ok",false).Bool)return proposal.Get("error","预测暂不可用").Text;
  var lines=V.Arr();var world=proposal["map_snapshot"]["metrics"];var before=baseline["metrics"];
  foreach(var resource in new[]{"food","material","goods"}){
   double diff=world.Get("flows",V.Map()).Get(resource,0).Double-before.Get("flows",V.Map()).Get(resource,0).Double;
   if(Math.Abs(diff)>.001)lines.Add(Resource(resource)+" "+Signed(diff)+"/月");
  }
  double housing=world.Get("housing",0).Double-before.Get("housing",0).Double;if(Math.Abs(housing)>.001)lines.Add("住房 "+Signed(housing)+" 人");
  foreach(var kind in G.c.balance["service_kinds"]){string key=kind.Text;double diff=world.Get("service_links",V.Arr()).Where(x=>x["kind"].Text==key).Sum(x=>x["people"].Double)-before.Get("service_links",V.Arr()).Where(x=>x["kind"].Text==key).Sum(x=>x["people"].Double);if(Math.Abs(diff)>.001)lines.Add(Service(key)+" "+Signed(diff)+" 人");}
  double energy=world.Get("energy",0).Double-before.Get("energy",0).Double;if(Math.Abs(energy)>.001)lines.Add("供电 "+Signed(energy));
  lines.Add("经营 "+Signed(world.Get("cashflow",0).Double-before.Get("cashflow",0).Double)+"/月");return string.Join(" · ",lines.Select(x=>x.Text));
 }
 void RenderConstructionUpgrades(){
  var id=_constructionId;
  if(!S["projects"].Has(id)){_constructionTitle.text="建筑已不可用";Button(_constructionFooter,"返回地图",CloseConstructionWindow);return;}
  var p=S["projects"][id];var choices=Development.choices(G,id).Select(x=>x.Text).ToArray();
  _constructionTitle.text="升级 · "+G.project_name(id);_constructionSubtitle.text="地块 "+_constructionTile+"  ·  当前 T"+p["tier"].Text+"  ·  运营者、已有槽位、库存与历史继续保留";
  Label(_constructionHeader,"运营者："+G.c.operators[p["operator_org"]]["name"].Text+"  ·  成长槽轨："+string.Join(" → ",p["slot_track"].Select(x=>Catalog.SLOT_NAMES.Get(x,x).Text)),17,Green,32*TextScale);
  if(!choices.Contains(_constructionUpgrade))_constructionUpgrade=choices.FirstOrDefault()??"";
  if(_constructionUpgrade==""){Label(_constructionBody,"当前没有后续升级方向。",21,Muted);Button(_constructionFooter,"返回建筑",CloseConstructionWindow);return;}
  var cards=ConstructionColumns(_constructionBody,"Upgrade Direction Comparison",choices.Length);
  for(int i=0;i<choices.Length;i++){
   var node=choices[i];var view=Development.preview(G,id,node);var card=cards[i];var definition=G.c.projects[node];bool chosen=node==_constructionUpgrade;
   Label(card,view["name"].Text+" · T"+(p["tier"].Long+1),21,chosen?Green:Ink);Label(card,ConstructionPurpose(definition),15,Muted);
   Label(card,"新槽："+Catalog.SLOT_NAMES.Get(view["slot"],view["slot"]).Text+" · 槽轨延续",16,Blue);
   foreach(var diff in view["gains"].Concat(view["losses"]))Label(card,Stat(diff["stat"].Text)+"："+F(diff["before"].Double)+" → "+F(diff["after"].Double)+"（"+Signed(diff["delta"].Double)+"）",15,diff["delta"].Double>=0?Green:Gold);
   Label(card,"即付 "+F(view["cost"].Double,0)+" 通货 · "+F(view["material"].Double,0)+" 材料 / "+F(view["construction_required"].Double)+" CP",14,Muted);
   var choose=Button(card,chosen?"✓ 已选方向":"选择此方向",()=>{_constructionUpgrade=node;RefreshConstructionWindow();},chosen);choose.name="Construction Upgrade "+node;
  }
  var selected=_constructionUpgrade;var upgrade=Development.preview(G,id,selected);
  _constructionBaseline=_constructionPlanner.snapshot(G);var clone=_constructionPlanner.copy_game(G);
  clone.s["projects"][id]["upgrade_choice"]=selected;clone.s["projects"][id]["upgrade_job"]=V.Map("work",0.0,"total",upgrade["construction_required"],"material",0.0,"cp",true);
  Development.complete(clone,id);clone.refresh_metrics();var previewError=Sim.tick(clone,G.c.balance["tick_hours"].Double);
  _constructionProposal=V.Map("ok",previewError=="","error",previewError,"map_snapshot",_constructionPlanner.display_copy(clone));
  RenderConstructionImpact(_constructionBaseline,_constructionProposal,id,"不升级","假定升级完工");
  ConstructionFold(_constructionBody,"upgrade-continuity","施工与保留内容",details=>{
   Label(details,"升级施工期间运营系数 "+F(upgrade["operation_factor"].Double,2)+"；工程完成后解锁下一槽。",16,Muted);
   foreach(var resource in new[]{"food","material","goods"})Label(details,"当前"+Resource(resource)+"库存 "+F(p["inventory"].Get(resource,0).Double),16,Muted);
   Raw(details,G.c.projects[selected]);Raw(details,p["history"]);
  });
  var cash=upgrade["cost"].Double;
  string blocked=p["status"].Text!="running"||p["upgrade_job"].Count!=0?"请等当前施工完成后再升级。":S["currency"].Double<cash?"升级经费不足，还差 "+F(cash-S["currency"].Double,0)+" 通货。":previewError;
  var expectedVersion=_constructionPlanner.version(G);
  ConstructionCosts(cash,upgrade["material"].Double,upgrade["construction_required"].Double,blocked,()=>{
   if(!_constructionOpen||_constructionSubmitting||_constructionMode!="upgrade"||_constructionUpgrade!=selected)return;
   _constructionSubmitting=true;var error=Runtime.Execute(g=>_constructionPlanner.version(G)!=expectedVersion?"城市已变化，预测已刷新，请重新确认。":Development.start_upgrade(g,id,selected));_constructionSubmitting=false;
   if(error!=""){RefreshConstructionWindow();return;}ConstructionSucceeded(G.project_name(id)+"已开始升级。");
  },"开始升级 · "+F(cash,0)+" 通货");
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
 void ConstructionCompareNumber(Transform parent,string title,double before,double after,string unit,int digits=1){ConstructionCompareLine(parent,title,F(before,digits)+unit,F(after,digits)+unit,Signed(after-before,digits)+unit);}
 void ConstructionCompareLine(Transform parent,string title,string before,string after,string delta,bool heading=false){
  var row=Row(parent,"Construction Comparison "+title,34*TextScale);var fields=new[]{title,before,after,delta};
  for(int i=0;i<fields.Length;i++){var text=Label(row,fields[i],heading?15:16,heading?Blue:i==0?Muted:Ink,31*TextScale);var element=text.GetComponent<LayoutElement>();element.minWidth=0;element.preferredWidth=0;element.flexibleWidth=i==0?1.4f:1;if(i>0)text.alignment=TextAnchor.MiddleRight;}
 }
}
}
