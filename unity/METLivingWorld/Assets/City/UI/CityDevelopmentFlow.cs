using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;
namespace MET.City {
public sealed partial class CityInterface {
 string _blueprintQuery="";
 string[] FilteredBlueprintIds(){
  var wallet=S["block"].Get("bonus_wallet",V.Map());
  return G.c.bonuses.Keys.Where(id=>(_v3BonusFilter=="all"||(_v3BonusFilter=="deployed"?Bonus.active_count(G,id)>0:wallet.Get(id,V.Map()).Get("charges",0).Long>0))&&
   (string.IsNullOrWhiteSpace(_blueprintQuery)||G.c.bonuses[id].ToJson().IndexOf(_blueprintQuery.Trim(),StringComparison.OrdinalIgnoreCase)>=0||id.IndexOf(_blueprintQuery.Trim(),StringComparison.OrdinalIgnoreCase)>=0))
   .OrderBy(id=>G.c.bonuses[id].Get("name",id).Text,StringComparer.Ordinal).ToArray();
 }
 void BlueprintSearch(Transform parent){
  var field=InputBox(parent,_blueprintQuery,"搜索方案名称、效果或来源");field.onValueChanged.AddListener(v=>_blueprintQuery=v);V3DevSmallButton(parent,"搜索",RenderPage);
  if(_blueprintQuery!="")V3DevSmallButton(parent,"清除搜索",()=>{_blueprintQuery="";RenderPage();});
 }
 void BlueprintPreviewUnavailable(Transform parent,V card,V view){
  V3DevText(parent,view.Get("error","当前不能生成安装比较。").Text,Gold);
  if(card["copies"].Long<=0){V3DevText(parent,"当前没有库存副本。规则与来源仍可查阅；需先满足上述目标条件，领取副本后才能安装。");V3DevSmallButton(parent,"安装副本 · 无库存",()=>{},true,true);}
 }
 void BlueprintFullCatalogue(Transform parent){
  Fold(parent,"完整方案目录与来源 · "+G.c.bonuses.Count+" 项",deep=>{
   foreach(var bid in G.c.bonuses.Keys.OrderBy(id=>G.c.bonuses[id].Get("name",id).Text,StringComparer.Ordinal)){
    var id=bid;var card=Bonus.card(G,id);var entry=CompactTaskCard(deep,"Catalogue "+id);V3DevText(entry,card["name"].Text+" · "+ScopeName(card["scope"].Text),Ink);V3DevText(entry,BlueprintEffectSummary(card));
    V3DevText(entry,"库存 "+card["copies"].Long+" · 已部署 "+Bonus.active_count(G,id));V3DevSmallButton(entry,"选择并比较",()=>{_inventoryChoice=id;_v3BonusFilter="all";_blueprintQuery="";_installView=V.Map();_blueprintVersion="";RenderPage();});
    Fold(entry,"完整规则与来源",source=>{BlueprintRuleRows(source,card.Get("effects",V.Arr()),id);Raw(source,G.c.bonuses[id]);});
   }
  });
 }
 // Share the existing confirmation ownership and Escape path with other modal operations.
 RectTransform RewardModalBody(string title,string note){
  CancelDeskTipsForPanel();DismissMapInspection();if(_confirm!=null)DismissConfirm();Runtime.Flush();
  _confirm=Surface("Reward Confirmation Shield",_root,new Color(0,0,0,.82f)).gameObject;Stretch((RectTransform)_confirm.transform);
  var card=Surface("Reward independent operation",_confirm.transform,Panel);Pin(card,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1280,800));
  float titleHeight=44*TextScale,noteHeight=38*TextScale,headerHeight=titleHeight+noteHeight+8,footerHeight=52*TextScale;
  var header=Column(card,"Reward heading",8,false);header.GetComponent<VerticalLayoutGroup>().padding=new RectOffset();Stretch(header,24,18,132,800-18-headerHeight);
  var heading=Label(header,title,25,Gold,titleHeight);heading.name="Reward modal title";var source=Label(header,note,16,Muted,noteHeight);source.name="Reward modal source";
  var close=Button(card,"关闭",DismissConfirm);close.name="Reward modal close";Pin((RectTransform)close.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-16,-12),new Vector2(90,46*TextScale));
  var viewport=Surface("Reward scroll",card,new Color(.015f,.04f,.05f,.35f));Stretch(viewport,24,headerHeight+32,24,footerHeight+40);viewport.gameObject.AddComponent<RectMask2D>();var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=48;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.viewport=viewport;
  var body=Column(viewport,"Reward body",12);body.anchorMin=new Vector2(0,1);body.anchorMax=new Vector2(1,1);body.pivot=new Vector2(.5f,1);body.sizeDelta=new Vector2(-12,0);scroll.content=body;
  var track=Surface("Reward scrollbar",viewport,new Color(.10f,.17f,.19f));track.anchorMin=new Vector2(1,0);track.anchorMax=Vector2.one;track.offsetMin=new Vector2(-7,4);track.offsetMax=new Vector2(-1,-4);var thumb=Surface("Reward scrollbar thumb",track,new Color(.26f,.59f,.55f));Stretch(thumb);var scrollbar=track.gameObject.AddComponent<Scrollbar>();scrollbar.direction=Scrollbar.Direction.BottomToTop;scrollbar.handleRect=thumb;scrollbar.targetGraphic=thumb.GetComponent<Image>();scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
  var footer=Row(card,"Reward actions",footerHeight);Stretch(footer,24,800-footerHeight-24,24,24);
  RefreshModalOwnership(true);return body;
 }
 void OpenRewardModal(string rid){if(CivicEnabled){OpenCivicReward(rid);return;}
  var queue=Bonus.pending(G);if(queue.Count==0||queue[0]["reward_id"].Text!=rid){ShowMessage("请按保存顺序领取，奖励队列已刷新。");RenderPage();return;}
  var reward=queue[0];var kind=reward.Get("kind","copy").Text;_rewardSelections.TryGetValue(rid,out var chosen);chosen=chosen??"";
  var body=RewardModalBody("选择奖励",BlueprintRewardSource(reward)+" · "+reward["choices"].Count+" 选 1 · 按保存顺序领取");
  var candidates=Row(body,"Reward candidate comparison",0);candidates.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight=false;
  foreach(var raw in reward["choices"]){var candidate=raw;var bid=candidate["bonus_id"].Text;var selected=chosen==bid;var box=CompactTaskCard(candidates,"Reward candidate "+bid);var width=box.gameObject.AddComponent<LayoutElement>();width.minWidth=0;width.preferredWidth=0;width.flexibleWidth=1;
   var title=Label(box,candidate.Get("name",bid=="__currency__"?"通货补偿":bid).Text,20,selected?Gold:Ink);title.name="Reward candidate title "+bid;BlueprintArt(box,bid,110,selected?1:.35f);V3DevText(box,BlueprintEffectSummary(candidate),Ink);
   var cost=V3DevText(box,bid=="__currency__"?"直接入账，不恢复 DT":kind=="research"?"升级本街区同名方案，不增加库存。":"获得1份部署许可，安装后生效。");cost.name="Reward candidate receipt "+bid;
   V3DevSmallButton(box,selected?"✓ 已预选":"预选这项",()=>{_rewardSelections[rid]=bid;PlayCue("select");OpenRewardModal(rid);},selected,_blueprintBusy||!Runtime.ViewCanEditFocused);
   if(bid!="__currency__"){RewardRuleRows(box,candidate.Get("effects",V.Arr()),bid);Fold(box,"适用条件与来源",source=>{V3DevText(source,candidate.Get("preview_reason","").Text);V3DevText(source,candidate.Get("stacking_note","").Text);Raw(source,G.c.bonuses[bid]);});}
  }
  var footer=_confirm.transform.Find("Reward independent operation/Reward actions");var cancel=Button(footer,"返回任务",DismissConfirm);cancel.name="Reward modal cancel";var claim=Button(footer,kind=="research"?"确认研究":"确认领取",()=>ClaimBlueprint(rid,chosen),true,chosen==""||_blueprintBusy||!Runtime.ViewCanEditFocused);claim.name="Reward modal claim";
  RefreshModalOwnership(true);
 }
 void RewardRuleRows(Transform parent,V effects,string bid){
  if(effects.Count==0){Label(parent,"关系与结构方案 · 展开查看适用规则",17,Blue);return;}
  int index=0;foreach(var raw in effects){var effect=raw;var row=CompactTaskCard(parent,"Reward rule "+bid+" "+index++);row.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(10,10,8,8);row.GetComponent<VerticalLayoutGroup>().spacing=4;
   var title=Label(row,Stat(effect.Get("label","规则").Text),17,Muted);title.name="Reward rule label";
   var value=Label(row,FormatEffect(effect,effect.Get("before",0).Double)+" → "+FormatEffect(effect,effect.Get("after",0).Double),17,BlueprintEffectAdverse(effect)?Gold:Green);value.name="Reward rule value";
   AddDeskTip(row.gameObject,Stat(effect.Get("label","规则").Text),()=>"这是规则参数，不是保证的实际月产。安装前按同一步模拟实际收益。\n"+Bonus.card(G,bid)["stacking_note"].Text+"\n规则来源："+effect.Get("rule_id",bid).Text);
  }
 }
 void ShowRewardReceipt(string bid){if(CivicEnabled){CivicRewardReceipt(bid);return;}
  var name=bid=="__currency__"?"通货补偿":G.c.bonuses[bid].Get("name",bid).Text;
  var body=RewardModalBody("奖励已领取",name+" · 已保存真实领取结果");BlueprintArt(body,bid,150);V3DevText(body,bid=="__currency__"?"补偿已入账，可继续经营与查看任务。":"领取结果已保存。可继续处理任务，或前往 Bonus 查看库存、蓝图与安装目标。",Green);
  var footer=_confirm.transform.Find("Reward independent operation/Reward actions");Button(footer,"留在任务",()=>{DismissConfirm();Navigate("tasks_home");});Button(footer,"前往 Bonus 使用",()=>{DismissConfirm();_v3BonusFilter="all";_blueprintQuery="";Navigate("bonus_home");},true);
  RefreshModalOwnership(true);
 }
}
}
