using System;
using MET.Core;
using UnityEngine;

namespace MET.City {
public sealed partial class CityInterface {
 string _wildCounterparty="hope";
 string TradeCounterparty=>WildDesign.Enabled(G)?_wildCounterparty:"";
 void WildTradeSection(Transform parent) {
  if(!WildDesign.Enabled(G))return;
  var tech=W.Get("wild_technology",V.Map());
  var section=OperatingSection(parent,"希望城 · 技术贸易","住房设计 I");
  Label(section,"贸易路线：作坊逐级升级为货运站 → 出口材料 → 购买科技。",13,Muted);
  double exported=tech.Get("material_exported",0).Double;
  bool purchased=tech.Get("housing_levels",0).Long>0;
  OperatingMeter(section,"material","累计实际出口",exported,CitySession.WildHousingRequiredExports,"材料","向希望城出售材料；挂单和撤单不计入。",Green);
  Label(section,purchased?"已掌握 · 每栋建筑设计容量 +2":"贸易完成后，支付 60 通货获得每栋 +2 设计容量。",15,purchased?Green:Ink);
  var partners=OperatingGrid(section,"Trade partner");
  OperatingButton(partners,"希望城",()=>{_wildCounterparty="hope";RenderPage();},_wildCounterparty=="hope");
  OperatingButton(partners,"普通市场",()=>{_wildCounterparty="";RenderPage();},_wildCounterparty=="");
  bool eligible=exported+1e-6>=CitySession.WildHousingRequiredExports;
  OperatingButton(section,purchased?"住房设计 I · 已获得":"购买住房设计 I · 60 通货",()=>{
   Confirm("购买住房设计 I？","支付 60 通货，余额将为 "+F(S["currency"].Double-60)+"。每栋现有及未来建筑增加 2 设计点；已出口材料不会再次扣除。",()=>{var error=Runtime.ExecuteWorld(city=>city.BuyWildHousing());if(error=="")ShowMessage("住房设计 I 已掌握；全城建筑设计容量 +2。");RenderPage();});
  },eligible&&!purchased,purchased||!eligible||S["currency"].Double<60||!Runtime.ViewCanEditFocused);
  if(!eligible)Label(section,"还需实际出口 "+F(Math.Max(0,10-exported))+" 材料。",13,Muted);
  var ii=CitySession.WildHousingIIView(G.c,W,S,Runtime.ViewCanEditFocused);bool second=ii["purchased"].Bool;
  OperatingButton(section,second?"住房设计 II · 已获得":"购买住房设计 II · "+F(ii["cost"].Double)+" 通货",()=>{
   Confirm("购买住房设计 II？",ii["reason"].Text,()=>{Runtime.ExecuteWorld(city=>city.BuyWildHousingII());RenderPage();});
  },ii["can_purchase"].Bool,!ii["can_purchase"].Bool);
  foreach(var reward in CitySession.WildRewardsView(G.c,W,W["focused_block_id"].Text,Runtime.ViewCanEditFocused,S.Get("phase","").Text=="play")) {
   Label(section,"有限任务："+reward["name"].Text,14,Ink);
   if(reward["claimed"].Bool)Label(section,"任务奖励已领取",13,Green);
   else {
    foreach(var option in reward["choices"]) {
     string choice=option["id"].Text,rewardId=reward["id"].Text;
     OperatingButton(section,option["name"].Text,()=>{Runtime.ExecuteWorld(city=>city.ClaimWildReward(rewardId,choice));RenderPage();},true,!reward["can_claim"].Bool);
    }
    Label(section,"方案进入库存，需手动装配；请在完成任务的街区领取。",13,Muted);
   }
  }
  Label(section,"低级工业品储备 "+F(S["wild"]["stock"]["low_industrial"].Double,0)+" 件 · 开局补给，农居升阶时使用",13,Blue);
 }
}
}
