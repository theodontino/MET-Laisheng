using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
public sealed partial class CityInterface {
 // A construction decision stays attached to its site, operator and price while details scroll.
 void TenderPage(){
  var draft=S["draft"];
  if(draft.Count==0||draft.Get("offers",V.Arr()).Count==0){
   _title.text="选择建造位置";_subtitle.text="先在地图选择一块空闲陆地";
   Label(_decisionHeader,"还没有待建项目",23,Ink);
   Label(_body,"从左侧「建设」选择建筑，再点一块空地。",18,Muted);
   Button(_decisionFooter,"回到地图选位置",CloseArchive,true);return;
  }
  var offers=draft["offers"];
  if(!offers.Any(x=>x["id"].Text==_offer))_offer=offers[0]["id"].Text;
  _baseline=_planner.snapshot(G);var previews=V.Map();
  foreach(var item in offers)previews[item["id"]]=_planner.compare(G,draft["base"].Text,draft["tile"].Text,item);
  var selected=offers.First(x=>x["id"].Text==_offer);_proposal=previews[_offer];_previewVersion=_planner.version(G);PublishComparison();
  var definition=G.c.projects[draft["base"]];var cost=Draft.offer_cost(G,selected);
  var org=G.c.operators[selected["operator_org"]];string building=definition["name"].Text;
  _title.text="建造"+building;_subtitle.text="选位置  ›  选方案  ›  开工";
  var identity=Row(_decisionHeader,"Construction Identity",96*TextScale);identity.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;
  var art=Node("Construction Building Illustration",identity);Width(art,112);Height(art,88*TextScale);
  var drawing=art.gameObject.AddComponent<CityBlueprintArt>();drawing.ShowDrafting=false;drawing.Style=CityIllustrationCatalog.StyleForBuilding(draft["base"].Text);drawing.Fill=1;drawing.Accent=CityIllustrationCatalog.AccentForStyle(drawing.Style);drawing.raycastTarget=false;
  var identityText=Column(identity,"Construction identity text",2,false);identityText.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);identityText.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
  Label(identityText,building+" · 地块 "+draft["tile"].Text,21,Ink,29*TextScale);
  Label(identityText,ConstructionPurpose(definition),16,Muted,25*TextScale);
  Label(identityText,"运营者："+org["name"].Text,17,Green,27*TextScale);

  if(_proposal.Get("ok",false).Bool)ConstructionBenefits(_body,_baseline,_proposal);
  else Label(_body,_proposal.Get("error","暂时无法计算建成后的变化，请重新选择方案。").Text,18,Gold);

  Label(_body,"选择运营方案",20,Ink);
  foreach(var item in offers){
   var option=item;var operatorInfo=G.c.operators[item["operator_org"]];bool chosen=item["id"].Text==_offer;
   var optionButton=Button(_body,(chosen?"✓  ":"")+operatorInfo["name"].Text,()=>{_offer=option["id"].Text;RenderPage();},chosen);
   optionButton.name="OperatorChoice "+item["id"].Text;
   Height((RectTransform)optionButton.transform,67*TextScale);
   var optionLabel=optionButton.GetComponentInChildren<Text>();optionLabel.alignment=TextAnchor.UpperLeft;Stretch(optionLabel.rectTransform,12,6,12,30*TextScale);
   var route=Label(optionButton.transform,"成长路线："+string.Join(" → ",item["slot_track"].Select(x=>Catalog.SLOT_NAMES.Get(x,x).Text)),14,Muted);
   Stretch(route.rectTransform,12,34*TextScale,12,5);route.alignment=TextAnchor.MiddleLeft;
   AddDeskTip(optionButton.gameObject,operatorInfo["name"].Text,operatorInfo.Get("summary","").Text+"\n成长槽位："+string.Join(" → ",item["slot_track"].Select(x=>Catalog.SLOT_NAMES.Get(x,x).Text)));
  }
  Fold(_body,"运营者与成长路线",details=>{
   foreach(var item in offers){var info=G.c.operators[item["operator_org"]];Label(details,info["name"].Text,19,Ink);Label(details,info.Get("summary","").Text,16,Muted);Label(details,"T1 → T2 → T3："+string.Join(" → ",item["slot_track"].Select(x=>Catalog.SLOT_NAMES.Get(x,x).Text)),16,Blue);}
   var rerollCost=Draft.reroll_cost(G)["currency"].Double;string fingerprint=draft.ToJson(false,true);
   var reroll=Button(details,"另找一组运营者 · "+F(rerollCost,0)+" 通货",()=>{
    if(!_archiveOpen||_page!="tender")return;
    var error=Runtime.Execute(g=>g.s["draft"].ToJson(false,true)!=fingerprint?"报价已经变化，请重新查看。":g.reroll());
    if(error=="")_offer="";RenderPage();
   },false,S["currency"].Double<rerollCost);
   AddDeskTip(reroll.gameObject,"另找运营者","立即支付这笔通货，重新抽取三名运营者。之后的重抽会涨价，已支付的重抽费不退还。");
  });
  if(_proposal.Get("ok",false).Bool){
   // Keep the complete same-step source report reachable without making it the first decision screen.
   var baseSnapshot=_baseline;var result=_proposal;
   Fold(_body,"全部变化与计算依据",details=>Impact(details,baseSnapshot,result));
   Fold(_body,"在地图上比较",map=>{
    Button(map,"地图 · 现状预测",()=>ShowForecastMap("baseline",baseSnapshot));
    Button(map,"地图 · 建成后",()=>ShowForecastMap("proposal",result["map_snapshot"]));
    Button(map,"本次改变图层",()=>{SetLayer("delta");ShowForecastMap("proposal",result["map_snapshot"]);});
   });
  }

  double cash=cost["currency"].Double,balance=S["currency"].Double;
  var costText=Label(_decisionFooter,balance>=cash?"支付后余 "+F(balance-cash,0)+" 通货 · 施工需 "+F(cost["material"].Double,0)+" 材料":"还差 "+F(cash-balance,0)+" 通货 · 可保留方案稍后建造",16,balance>=cash?Muted:Gold,28*TextScale);costText.name="Construction Costs";
  var controls=Row(_decisionFooter,"Construction decision actions",46*TextScale);controls.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;
  var defer=Button(controls,"先不建",()=>{if(!_archiveOpen||_page!="tender")return;if(Runtime.Execute(g=>g.cancel_draft())=="")CloseArchive();});defer.name="Construction Defer";
  Width((RectTransform)defer.transform,126*TextScale);
  AddDeskTip(defer.gameObject,"先不建","返回地图并保留这一类建筑的报价。不支付建设费；已经支付的重抽费不会退回。");
  string offerId=_offer,expectedDraft=draft.ToJson(false,true),expectedVersion=_previewVersion,targetTile=draft["tile"].Text;
  bool submitted=false;
  var build=Button(controls,"开始建造 · "+F(cash,0)+" 通货",()=>{
   if(submitted||!_archiveOpen||_page!="tender"||_offer!=offerId)return;
   submitted=true;
   // Execute flushes before this callback. Match the detached preview version and exact draft,
   // not only offer:0, whose ID can be reused by a later tender.
   var error=Runtime.Execute(g=>_planner.version(G)!=expectedVersion||g.s["draft"].ToJson(false,true)!=expectedDraft?
    "城市或报价已变化，已刷新，请重新确认。":g.accept_offer(offerId));
   if(error!=""){RenderPage();return;}
   CloseArchive();SelectedTile=targetTile;SelectedBuilding=S["cells"][targetTile]["building"].Text;RefreshContext();SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);
   ShowMessage(building+"已开工。施工会随城市运行推进，点建筑查看进度。");
  },true,balance<cash||!_proposal.Get("ok",false).Bool);build.name="Construction Primary";var mainSize=build.GetComponent<LayoutElement>();mainSize.minWidth=0;mainSize.preferredWidth=0;mainSize.flexibleWidth=1;
  AddDeskTip(build.gameObject,"确认并开工","立即支付 "+F(cash,0)+" 通货。施工过程中逐步消耗 "+F(cost["material"].Double,0)+" 材料，需要 "+F(cost["cp"].Double)+" CP 施工量，与其他工程共享施工能力。材料不足时工程会等待。上方效果按假定已建成计算，不代表立即获得。");
 }

 string ConstructionPurpose(V definition){
  var stats=definition.Get("stats",V.Map());var purposes=V.Arr();
  if(stats.Get("housing",0).Double>0)purposes.Add("提供住房");
  foreach(var resource in new[]{"food","material","goods"})if(stats.Get("output."+resource,0).Double>0)purposes.Add("生产"+Resource(resource));
  if(stats.Keys.Any(key=>key.StartsWith("service.",StringComparison.Ordinal)&&stats[key].Double>0))purposes.Add("提供社区服务");
  return purposes.Count>0?string.Join(" · ",purposes.Select(x=>x.Text)):"在这块土地上开始建设";
 }
 void ConstructionBenefits(Transform parent,V baseline,V proposal){
  var section=Column(parent,"Construction Benefits",7);section.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,2,6);
  Label(section,"建成后会改变什么",20,Ink);
  var explain=Label(section,"与不建设相比 · 同一次结算预测",14,Muted);
  AddDeskTip(explain.gameObject,"建成后预测","两边都计算同一次结算：一边不建设，一边假定这栋楼已建成。这里不扣建设费和施工材料，也不模拟施工等待；详细记录在「全部变化与计算依据」。");
  int shown=0;
  if(Math.Abs(proposal["housing"].Double)>.0001){ConstructionBenefit(section,"住房容量",Signed(proposal["housing"].Double)+" 人",proposal["housing"].Double>=0?Green:Gold);shown++;}
  foreach(var resource in new[]{"food","material","goods"}){
   double diff=proposal["world"].Get("flows",V.Map()).Get(resource,0).Double-baseline["metrics"].Get("flows",V.Map()).Get(resource,0).Double;
   if(Math.Abs(diff)<.0001)continue;
   ConstructionBenefit(section,"全城"+Resource(resource)+"净流量",Signed(diff)+" /月",diff>=0?Green:Gold);shown++;
  }
  foreach(var kind in proposal["added_service"].Keys){double value=proposal["added_service"][kind].Double;if(Math.Abs(value)<.0001)continue;ConstructionBenefit(section,Service(kind)+"覆盖",Signed(value)+" 人",value>=0?Green:Gold);shown++;}
  if(shown==0)Label(section,"本次预测没有新增住房或资源、服务净收益。",16,Gold);
  ConstructionBenefit(section,"全城每月收支变化",Signed(proposal["cashflow"].Double)+" 通货/月",proposal["cashflow"].Double>=0?Green:Gold);
  var concerns=V.Arr();
  if(Math.Abs(proposal["energy_supply"].Double)>.0001)concerns.Add("供电 "+Signed(proposal["energy_supply"].Double));
  if(Math.Abs(proposal["energy_demand"].Double)>.0001)concerns.Add("用电 "+Signed(proposal["energy_demand"].Double));
  if(concerns.Count>0)Label(section,"电力能力："+string.Join(" · ",concerns.Select(x=>x.Text)),16,Muted);
  if(proposal["harmed"].Double>.0001)Label(section,F(proposal["harmed"].Double,0)+" 位现有居民的污染暴露将上升。",16,Gold);
  if(proposal["affected"].Count>0)Label(section,F(proposal["affected"].Count,0)+" 座现有建筑的预计运行率会下降；展开全部变化查看。",16,Gold);
  if(proposal["activity"].Get("utilization",0).Double<.99)Label(section,"预计运行率 "+F(proposal["activity"].Get("utilization",0).Double*100,0)+"% · 受人手、投入与电力等条件影响",16,Gold);
 }
 void ConstructionBenefit(Transform parent,string title,string value,Color color){
  var line=Row(parent,"Construction Benefit "+title,34*TextScale);
  var label=Label(line,title,17,Muted,30*TextScale);var figure=Label(line,value,20,color,30*TextScale);figure.alignment=TextAnchor.MiddleRight;
 }
}
}
