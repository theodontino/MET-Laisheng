using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UIElements;
using C=MET.City.CivicUI;
namespace MET.City {
public sealed partial class CityInterface {
 void BeginCivicConstruction(string mode,string id,string tile){
  CancelDeskTipsForPanel();DismissMapInspection();EndConstructionIntent();string key=mode+":"+id+":"+tile;
  if(key!=_constructionKey){_constructionOffer="";_constructionUpgrade="";}_constructionKey=key;_constructionMode=mode;_constructionId=id;_constructionTile=tile;
  SelectedTile=tile;SelectedBuilding=S["cells"][tile].Get("building","").Text;_constructionOpen=true;Runtime.SetDecisionOpen(true);RenderCivicConstruction();SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);
 }
 void CloseCivicConstruction(){
  _constructionOpen=false;_constructionSubmitting=false;CloseCivicOperation();
  if(S["cells"].Has(_constructionTile)){SelectedTile=_constructionTile;SelectedBuilding=S["cells"][_constructionTile].Get("building","").Text;}
  Runtime.SetDecisionOpen(_archiveOpen||ObjectDrawerOpen||ToolkitUpgradeOpen);_civicTileKey="";RefreshCivicTile();RefreshCivicDrawer();SyncCivicVisibility();SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);
 }
 void RenderCivicConstruction(){
  if(!_constructionOpen||_constructionSubmitting||(_civicOperation!=""&&_civicOperation!="construction"))return;
  var draft=S["draft"];var offers=draft.Get("offers",V.Arr());
  if(draft.Get("base","").Text!=_constructionId||draft.Get("tile","").Text!=_constructionTile||offers.Count==0){OpenCivicOperation("construction","报价已变化","地块 "+_constructionTile,(b,a)=>{C.Empty(b,"请重新选择建造方案");C.Button(a,"返回地块",CloseConstructionWindow);},CloseConstructionWindow);return;}
  if(!offers.Any(x=>x["id"].Text==_constructionOffer))_constructionOffer=offers[0]["id"].Text;
  var selected=offers.First(x=>x["id"].Text==_constructionOffer);var definition=G.c.projects[_constructionId];string name=definition["name"].Text;
  _constructionBaseline=_constructionPlanner.snapshot(G);var previews=V.Map();foreach(var offer in offers)previews[offer["id"]]=_constructionPlanner.compare(G,_constructionId,_constructionTile,offer);_constructionProposal=previews[_constructionOffer];
  var cost=Draft.offer_cost(G,selected);double cash=cost["currency"].Double,material=cost["material"].Double,cp=cost["cp"].Double;
  string blocked=!Runtime.ViewCanEditFocused?"当前为只读街区":!_constructionProposal.Get("ok",false).Bool?_constructionProposal.Get("error","预测暂不可用").Text:S["currency"].Double<cash?"资金不足，还差 "+F(cash-S["currency"].Double)+" 通货":"";
  bool wild=selected.Get("wild_direct",false).Bool;string version=_constructionPlanner.version(G),draftVersion=draft.ToJson(false,true),offerId=_constructionOffer;
  OpenCivicOperation("construction","建造 · "+name,"地块 "+_constructionTile+" · "+ConstructionPurpose(definition),(body,actions)=>{
   if(wild){var form=G.c.wild["forms"][_constructionId];var row=C.Row(body);C.Value(row,"基础住房",F(form["housing"].Double,0)+" 人");C.Value(row,"食物产能",F(form["food"].Double)+" /月");double basis=G.c.wild["initial_dp"].Double,tech=Math.Max(0,G.world_effects.Get("wild_housing_levels",0).Double)*G.c.wild["technology_dp_per_level"].Double;C.Value(row,"初始设计容量",F(basis+tech,0)+" DP","基础 "+F(basis,0)+" + 科技 "+F(tech,0));C.Label(body,"完工后进入建筑星图，选择生长节点、槽位与专业化方向。","c-muted");}
   else {
    var choices=C.Section(body,"选择运营方案","同一地块 · 同一步预测");var row=C.Row(choices);row.style.alignItems=Align.Stretch;
    foreach(var offer in offers){var o=offer;string oid=o["id"].Text;var org=G.c.operators[o["operator_org"]];var card=C.Choice(row,org["name"].Text,org.Get("summary","").Text,oid==_constructionOffer,()=>{_constructionOffer=oid;RenderCivicConstruction();},"city");
     C.Label(card,"成长槽位","c-micro c-muted");var slots=C.Row(card,"c-wrap");int tier=1;foreach(var slot in o["slot_track"])C.Label(slots,"T"+(tier++)+" "+Catalog.SLOT_NAMES.Get(slot,slot).Text.Replace("方案",""),"c-small c-good");
     var candidateStats=previews[oid].Get("stats",V.Map());
     foreach(var stat in new[]{"housing","output.food","output.material","output.goods","service.medical","service.education","service.entertainment","service.commercial"}.Where(k=>candidateStats.Get(k,0).Double>0).Take(4))C.Pair(card,Stat(stat),F(candidateStats[stat].Double));
     var oc=Draft.offer_cost(G,o);if(oc["currency"]!=cost["currency"]||oc["material"]!=cost["material"]||oc["cp"]!=cost["cp"])C.Label(card,F(oc["currency"].Double,0)+" 通货 · "+F(oc["material"].Double)+" 材料 · "+F(oc["cp"].Double)+" CP","c-small");
    }
   }
   var panels=C.Row(body);panels.style.alignItems=Align.FlexStart;var effect=C.Column(panels,"c-decision-main");var limits=C.Column(panels,"c-decision-limits");
   var target=_constructionProposal.Get("target","").Text;var after=_constructionProposal.Get("map_snapshot",V.Map());var st=after.Get("compiled",V.Map()).Get("stats",V.Map()).Get(target,V.Map());
   // The planner's display snapshot does not always include compiled rules; its target_stats is the authoritative preview payload.
   if(st.Count==0)st=_constructionProposal.Get("stats",V.Map());
   C.Label(effect,"方案建成后的变化","c-heading");C.Label(effect,"基于同一步现状与假定完工的城市预测；实际结果仍受用工、材料和电力约束。","c-small c-muted");
   CivicConstructionChanges(effect,_constructionBaseline,_constructionProposal,false);
   C.Label(limits,"开工条件","c-heading");C.Meter(limits,"材料 · 库存 /本工程",G.inventory_total("material"),material,"材料",G.inventory_total("material")<material?"库存不足时等待补货，不额外预扣。":"材料随施工进度消耗。",G.inventory_total("material")<material?"c-warning":"");
   var cap=Construction.capacity_breakdown(G);C.Value(limits,"当前本区施工能力",F(cap["local_effective"].Double)+" CP/月","总队列按优先级共享");double demand=S["metrics"].Get("energy_need",0).Double,supply=S["metrics"].Get("energy",0).Double;C.Meter(limits,"电力需求 /供给",demand,supply,"",demand>supply?"缺电会降低有效能力。":"",demand>supply?"c-warning":"");C.Label(limits,blocked!=""?blocked:"可确认开工","c-small "+(blocked!=""?"c-warning":"c-good"));
   C.Fold(body,"城市整体变化 · 展开比较",parent=>{
    CivicConstructionChanges(parent,_constructionBaseline,_constructionProposal,true);
    C.Pair(parent,"全城平均污染场强",F(ConstructionPollution(_constructionBaseline),3)+" → "+F(ConstructionPollution(after),3));
    C.Pair(parent,"污染覆盖面积",F(ConstructionPollutedArea(_constructionBaseline))+"% → "+F(ConstructionPollutedArea(after))+"%");
    C.Label(parent,"面积与平均场强以全城全部地格为分母。","c-small c-muted");
    var trace=after.Get("compiled",V.Map()).Get("trace",V.Map()).Get(target,V.Map());var adjacency=trace.Get("spatial_synergy",V.Map());
    C.Pair(parent,"本楼邻接倍率",F(adjacency.Get("value",1).Double,2)+"×");foreach(var link in adjacency.Get("contributors",V.Arr()))C.Pair(parent,G.project_name(link["source_id"].Text)+" · "+link["tile"].Text,Signed(link["amount"].Double*100)+"%");
    CivicSource(parent,"本楼完工能力、投入与规则",V.Map("stats",st,"activity",after.Get("metrics",V.Map()).Get("activity_ledger",V.Map()).Get(target,V.Arr()),"trace",trace));
    CivicSource(parent,"同一步现状凭据",_constructionBaseline);CivicSource(parent,"同一步建成后凭据",after);_civic.ApplyText(parent);
   });
   if(!wild)C.Fold(body,"另找运营者 · 付费重抽",parent=>{double price=Draft.reroll_cost(G)["currency"].Double;C.Label(parent,"关闭窗口保留报价；重抽立即付费并更换候选。","c-muted");C.Button(parent,"支付 "+F(price,0)+" 通货 · 另找一组",()=>Confirm("重新征集运营者？","立即支付 "+F(price,0)+" 通货，支付后 "+F(S["currency"].Double-price,0)+"。当前候选将被替换。",()=>{var error=Runtime.Execute(g=>g.s["draft"].ToJson(false,true)!=draftVersion?"报价已经变化，请重新查看。":g.reroll());if(error=="")_constructionOffer="";RenderCivicConstruction();}),enabled:Runtime.ViewCanEditFocused&&S["currency"].Double>=price);_civic.ApplyText(parent);});
   var priceBox=C.Column(actions,"c-grow");C.Label(priceBox,"即付 "+F(cash,0)+" 通货  ·  余额 "+F(S["currency"].Double-cash,0),"c-heading "+(blocked!=""?"c-warning":""));C.Label(priceBox,"施工另需 "+F(material)+" 材料 / "+F(cp)+" CP","c-small c-muted");
   C.Button(actions,"取消",CloseConstructionWindow,false,true,"construction-cancel");C.Button(actions,"确认开工",()=>{if(!_constructionOpen||_constructionSubmitting||_constructionOffer!=offerId)return;_constructionSubmitting=true;var error=Runtime.Execute(g=>_constructionPlanner.version(G)!=version||g.s["draft"].ToJson(false,true)!=draftVersion?"城市或报价已变化，已刷新，请重新确认。":g.accept_offer(offerId));_constructionSubmitting=false;if(error!=""){RenderCivicConstruction();return;}ConstructionSucceeded(name+"已开工。完工后开始提供能力。");},true,blocked=="","construction-confirm");
  },CloseConstructionWindow);
 }
 void CivicConstructionChanges(VisualElement parent,V before,V proposal,bool full){
  var a=before.Get("metrics",V.Map());var b=proposal.Get("map_snapshot",V.Map()).Get("metrics",V.Map());if(b.Count==0){C.Label(parent,"预测暂不可用","c-muted");return;}
  int count=0;foreach(var key in new[]{"food","material","goods","cashflow","housing","energy","energy_need","medical","education","entertainment","commercial"}){
   bool flow=new[]{"food","material","goods"}.Contains(key),service=new[]{"medical","education","entertainment","commercial"}.Contains(key);double av=flow?a.Get("flows",V.Map()).Get(key,0).Double:a.Get(key,0).Double,bv=flow?b.Get("flows",V.Map()).Get(key,0).Double:b.Get(key,0).Double;
   if(service){av=ConstructionServicePeople(a,key);bv=ConstructionServicePeople(b,key);}double change=bv-av;if(!full&&Math.Abs(change)<.0005)continue;count++;
   string label=flow?Resource(key)+"净产消":key=="cashflow"?"经营净额":key=="housing"?"住房容量":key=="energy"?"供电能力":key=="energy_need"?"用电需求":Service(key)+"实际覆盖";string unit=flow?" /月":key=="cashflow"?" 通货/月":key=="housing"?" 人":service?" 人":"";
   var row=C.Row(parent,"c-pair");C.Label(row,label,"c-small c-muted c-grow");C.Label(row,F(av)+" → "+F(bv)+unit,"c-small");C.Label(row,Signed(change)+unit,"c-small "+(change>0&&key!="energy_need"?"c-good":"c-warning"));
  }if(count==0)C.Label(parent,"该步暂无可见净变化。","c-muted");
 }
}
}
