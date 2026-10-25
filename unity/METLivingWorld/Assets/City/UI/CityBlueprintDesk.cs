using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace MET.City {
public sealed partial class CityInterface {
 readonly Color Paper=new Color(.045f,.09f,.105f,.98f),PaperInk=new Color(.88f,.94f,.93f),PaperMuted=new Color(.58f,.72f,.74f),BlueprintBlue=new Color(.035f,.09f,.12f,.98f);
 readonly Dictionary<string,string> _rewardSelections=new Dictionary<string,string>();
 string _inventoryChoice="",_blueprintHost="",_blueprintSlot="",_blueprintScope="building",_blueprintVersion="";
 V _installView=V.Map();bool _blueprintBusy,_receiptSkip;RectTransform _receipt,_bonusBucket;Text _bucketCount;
 public V BlueprintDiagnostics=>V.Map("choice",_inventoryChoice,"host",_blueprintHost,"slot",_blueprintSlot,"scope",_blueprintScope,"busy",_blueprintBusy,"preview",_installView.Duplicate(true),"tooltip",DeskTipOpen,"tooltip_pending",DeskTipPending,"tooltip_deadline",_deskDwell.ReadyAt,"page",_page,"pending",Bonus.pending(G).Count);
 public void OpenBlueprintDesk(){OpenArchive(Bonus.pending(G).Count>0?"blueprints":"bonuses");}
 int BlueprintStyle(string bid)=>CityIllustrationCatalog.StyleForBonus(bid);
 Color BlueprintAccent(string bid)=>CityIllustrationCatalog.AccentForStyle(BlueprintStyle(bid));
 string ScopeName(string scope)=>scope=="building"?"建筑方案":scope=="organization"?"运营商方案":"本街区方案";
 string BlueprintRewardSource(V reward){var source=reward.Get("source","城市成长").Text;
  return source.StartsWith("annual:",StringComparison.Ordinal)?"年度奖励":source.StartsWith("population:",StringComparison.Ordinal)?"人口成长奖励":source.Contains(":")?"城市成长奖励":source;
 }
 string BlueprintEffectSummary(V candidate){
  if(candidate.Get("bonus_id","").Text=="__currency__")return "领取 "+F(candidate.Get("compensation",30).Double,0)+" 通货，不恢复 DT";
  var effects=candidate.Get("effects",V.Arr());var definition=G.c.bonuses.Get(candidate.Get("bonus_id","").Text,V.Map());
  var beneficial=effects.FirstOrDefault(x=>!BlueprintEffectAdverse(x));
  if(beneficial==null){
   var purpose=definition.Get("player_highlights",V.Arr()).FirstOrDefault(x=>!x.Text.StartsWith("代价",StringComparison.Ordinal)&&!x.Text.StartsWith("成本",StringComparison.Ordinal));
   if(purpose!=null)return purpose.Text+(effects.Count>0?" · "+Stat(effects[0].Get("label","规则").Text)+" "+FormatEffect(effects[0],effects[0].Get("before",0).Double)+" → "+FormatEffect(effects[0],effects[0].Get("after",0).Double):"");
   if(effects.Count==0)return definition.Get("summary","结构或关系规则，展开查看适用条件").Text;
  }
  var first=beneficial??effects[0];return Stat(first.Get("label","方案效果").Text)+" "+FormatEffect(first,first.Get("before",0).Double)+" → "+FormatEffect(first,first.Get("after",0).Double)+
   (effects.Count>1?" · 另有 "+(effects.Count-1)+" 项规则":"");
 }
 bool BlueprintEffectAdverse(V effect){
  string label=effect.Get("label","").Text;bool cost=label.Contains("投入")||label.Contains("需求")||label.Contains("污染")||label.Contains("维护")||label.Contains("暴露")||label.Contains("短缺")||label.Contains("衰减");
  return cost?effect.Get("after",0).Double>effect.Get("before",0).Double:effect.Get("after",0).Double<effect.Get("before",0).Double;
 }
 void BlueprintArt(Transform parent,string bid,float height,float fill=1){var node=Node("Blueprint Drawing "+bid,parent);Height(node,height);var art=node.gameObject.AddComponent<CityBlueprintArt>();art.Style=BlueprintStyle(bid);art.Fill=fill;art.Accent=BlueprintAccent(bid);art.color=parent.GetComponentInParent<CityDeskTone>()?.Light==true?new Color(.43f,.47f,.41f):Color.white;art.raycastTarget=false;}
 Text PaperLabel(Transform parent,string text,int size=17,bool muted=false,float height=0)=>Label(parent,text,size,muted?PaperMuted:PaperInk,height);
 RectTransform PaperDocument(Transform parent,string title,string subtitle=""){
  var surface=Surface("Detail "+title,parent,Paper);
  var layout=surface.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(22,22,18,18);layout.spacing=10;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;
  surface.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
  PaperLabel(surface,title,22);if(subtitle!="")PaperLabel(surface,subtitle,16,true);return surface;
 }
 void BuildBonusBucket(){
  // Keep a nonvisual destination for the existing reward flight animation.
  _bonusBucket=Node("Bonus Blueprint Bucket",_root);_bonusBucket.gameObject.SetActive(false);
  _bucketCount=Label(_bonusBucket,"",11,Gold);_bucketCount.gameObject.SetActive(false);
 }
 void RefreshBucket(){
  long count=0;foreach(var w in S["block"].Get("bonus_wallet",V.Map()).Values)count+=w.Get("charges",0).Long;
  if(_bucketCount!=null)_bucketCount.text=count.ToString();
  if(_hudBonusBadge!=null)_hudBonusBadge.text=count.ToString();
 }
 void BlueprintOffersPage(){
  var pending=Bonus.pending(G);
  if(pending.Count==0){var columns=WorkspaceColumns("Reward queue",1);PaperLabel(PaperDocument(columns[0],"所有提案已处理"),"已领取蓝图可从 Bonus 安装。",17,true);WorkspaceActions("前往 Bonus 使用",()=>Navigate("bonus_home"));return;}
  BlueprintRewardCard(pending[0],Runtime.ViewCanEditFocused);
 }

 void BlueprintRewardCard(V reward,bool actionable){
  var rid=reward["reward_id"].Text;var kind=reward.Get("kind","copy").Text;
  _rewardSelections.TryGetValue(rid,out var chosen);chosen=chosen??"";
  var count=reward["choices"].Count;var columns=WorkspaceColumns("Saved reward candidates",Enumerable.Repeat(1f,Math.Max(1,count)).ToArray());
  var index=0;foreach(var candidateValue in reward["choices"]){var candidate=candidateValue;var bid=candidate["bonus_id"].Text;var selected=chosen==bid;var parent=columns[index++];
   var tile=CompactTaskCard(parent,"Blueprint Candidate "+rid+" "+bid);var visual=Column(tile,"Candidate Paper",8);
   Label(visual,candidate.Get("name",bid=="__currency__"?"通货补偿":bid).Text,22,selected?Gold:Ink);
   Label(visual,bid=="__currency__"?"城市补助":kind=="research"?candidate.Get("grade","基础").Text+" → "+candidate.Get("target_grade","改良").Text:candidate.Get("grade","基础").Text+" · "+ScopeName(candidate.Get("scope","building").Text),16,Muted);
   var drawing=Node("Candidate Art",visual);Height(drawing,130*TextScale);var art=drawing.gameObject.AddComponent<CityBlueprintArt>();art.Style=BlueprintStyle(bid);art.Accent=BlueprintAccent(bid);art.Fill=selected?1:0;art.color=new Color(.77f,.85f,.81f);art.raycastTarget=false;
   Label(visual,BlueprintEffectSummary(candidate),17,Ink);
   Label(visual,bid=="__currency__"?"直接入账 · 不恢复 DT":kind=="research"?"升级本街区全部同名方案；不扩大部署尺度。":"领取1份部署许可；安装后才生效。",16,Muted);
   var b=Button(visual,selected?"✓ 已预选":"预选这项",()=>{if(_blueprintBusy)return;HideDeskTip();_rewardSelections[rid]=bid;PlayCue("select");RenderPage();},selected,_blueprintBusy||!actionable);b.name="Blueprint Candidate "+rid+" "+bid;
   var motion=tile.gameObject.AddComponent<CityBlueprintMotion>();motion.Art=art;motion.Visual=visual;motion.ReducedMotion=()=>Runtime.DisplaySettings.Get("reduced_motion",false).Bool;motion.SetChosen(selected);
   if(selected){var outline=tile.gameObject.AddComponent<Outline>();outline.effectColor=Gold;outline.effectDistance=new Vector2(2,2);}
   if(bid=="__currency__")Metric(parent,"通货补偿",F(reward.Get("compensation",30).Double,0));
   else {RewardRuleRows(parent,candidate.Get("effects",V.Arr()),bid);Fold(parent,"适用条件、重复规则与来源",deep=>{Label(deep,candidate.Get("preview_reason","").Text,16,Muted);Label(deep,candidate.Get("stacking_note","").Text,16,Muted);Raw(deep,G.c.bonuses[bid]);});}
  }
  var selectedCandidate=reward["choices"].FirstOrDefault(c=>c["bonus_id"].Text==chosen);
  WorkspaceHint(BlueprintRewardSource(reward)+" · "+Bonus.pending(G).Count+" 份提案按保存顺序领取 · "+(selectedCandidate==null?"先比较候选，再预选一项": "已预选："+selectedCandidate.Get("name",chosen).Text));
  if(selectedCandidate!=null)WorkspaceActions("取消预选",()=>{_rewardSelections.Remove(rid);HideDeskTip();RenderPage();});
  WorkspaceActions(kind=="research"?"确认研究":"确认领取",()=>ClaimBlueprint(rid,chosen),selectedCandidate==null||_blueprintBusy||!actionable);
 }

 void BlueprintRuleRows(Transform parent,V effects,string bid){
  if(effects.Count==0){var t=Label(parent,"关系与结构方案 · 展开查看适用规则",17,Blue);AddDeskTip(t.gameObject,"结构效果",()=>"此方案包含条件、关系、配方或结构规则；不能把它误读成无条件的产出加成。\n"+G.c.bonuses.Get(bid,V.Map()).Get("description","").Text);return;}
  foreach(var rowValue in effects){var e=rowValue;var r=Row(parent,"Blueprint Rule",42);Label(r,Stat(e.Get("label","规则").Text),17,Muted);var value=Button(r,FormatEffect(e,e.Get("before",0).Double)+" → "+FormatEffect(e,e.Get("after",0).Double),()=>{},false);value.GetComponentInChildren<Text>().color=BlueprintEffectAdverse(e)?Gold:Green;Height((RectTransform)value.transform,38*TextScale);
   AddDeskTip(value.gameObject,e.Get("label","规则").Text,()=>"这是规则参数，不是保证的实际月产。实际适配和收益将在安装前按同一步模拟。\n"+Bonus.card(G,bid)["stacking_note"].Text+"\n规则来源："+e.Get("rule_id",bid).Text);value.onClick.AddListener(()=>ShowDeskTip(value.GetComponent<CityDeskTooltip>(),true));
  }
 }
 string FormatEffect(V effect,double value){var mode=effect.Get("mode","").Text;return mode=="multiply"?"×"+F(value,3):mode=="fraction"?F(value*100,1)+"%":F(value,3)+effect.Get("unit","").Text;}
 void ClaimBlueprint(string rid,string bid){
  if(_blueprintBusy)return;var pending=Bonus.pending(G);if(pending.Count==0||pending[0]["reward_id"].Text!=rid){ShowMessage("这份提案已处理，队列已刷新。");RenderPage();return;}
  _blueprintBusy=true;var error=Runtime.Execute(g=>Bonus.choose(g,rid,bid));if(error!=""){_blueprintBusy=false;RenderPage();return;}_rewardSelections.Remove(rid);if(_confirm!=null)DismissConfirm();HideDeskTip();_inventoryChoice=bid=="__currency__"?"":bid;_blueprintSlot="";_installView=V.Map();RenderPage();RefreshBucket();StartCoroutine(BlueprintReceipt(bid));
 }
 IEnumerator BlueprintReceipt(string bid){
  _receiptSkip=false;_receipt=Surface("Approved Blueprint Flight",_root,BlueprintBlue);Pin(_receipt,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(260,286));
  var body=Column(_receipt);Stretch(body);Label(body,bid=="__currency__"?"通货补偿":G.c.bonuses[bid]["name"].Text,23,Ink);BlueprintArt(body,bid,140);var accepted=Label(body,"✓  已领取",23,Green,38);accepted.alignment=TextAnchor.MiddleCenter;
  var transfer=Node("Receipt Transfer",_receipt);Stretch(transfer,80,80,80,80);HudIcon(transfer,"bonus",Green,74);transfer.gameObject.SetActive(false);HideDeskTip();_receipt.SetAsLastSibling();
  float elapsed=0,duration=Runtime.DisplaySettings.Get("reduced_motion",false).Bool?0:1.05f;
  Vector2 destination=_root.InverseTransformPoint(_bonusBucket.TransformPoint(_bonusBucket.rect.center));
  while(elapsed<duration&&!_receiptSkip){elapsed+=Mathf.Min(UnityEngine.Time.unscaledDeltaTime,.05f);float t=Mathf.Clamp01(elapsed/duration);float travel=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.40f)/.60f));bool moving=t>.42f;if(moving){body.gameObject.SetActive(false);transfer.gameObject.SetActive(true);}_receipt.anchoredPosition=Vector2.Lerp(Vector2.zero,destination,travel);_receipt.localScale=Vector3.one*Mathf.Lerp(1,.12f,travel);yield return null;}
  if(_receipt!=null){ForgetFonts(_receipt);Destroy(_receipt.gameObject);_receipt=null;}_blueprintBusy=false;if(_archiveOpen&&(_page=="bonuses"||_page=="bonus_home"||_page=="tasks_home"||_page=="settlement"||_page=="blueprints"))RenderPage();ShowRewardReceipt(bid);
 }
 void BlueprintInventoryPage(){
  var columns=WorkspaceColumns("Bonus deployment workspace",.9f,1.05f,1.35f);var inventory=columns[0];var targets=columns[1];var comparison=columns[2];
  Label(inventory,"库存与部署",21,Gold);Label(targets,"选择目标与槽位",21,Blue);Label(comparison,"安装前后比较",21,Green);
  if(Bonus.pending(G).Count>0)Button(inventory,"待领取提案 · "+Bonus.pending(G).Count,()=>Navigate("blueprints"),true);
  var filters=Row(inventory,"Blueprint inventory filters",38*TextScale);
  foreach(var entry in new[]{new[]{"available","可用"},new[]{"deployed","已部署"},new[]{"all","全部"}}){var id=entry[0];Button(filters,entry[1],()=>{_v3BonusFilter=id;RenderPage();},_v3BonusFilter==id);}
  BlueprintSearch(inventory);var ids=FilteredBlueprintIds();
  Label(inventory,"找到 "+ids.Length+" 项 / 全目录 "+G.c.bonuses.Count+" 项",15,Muted);
  if(ids.Length==0)Label(inventory,"没有符合筛选的方案；切换全部或清除搜索。当前选择与目标保留。",17,Muted);
  if(!G.c.bonuses.Has(_inventoryChoice))_inventoryChoice=ids.FirstOrDefault()??"";
  BlueprintInventoryChoices(inventory,ids);BlueprintFullCatalogue(inventory);
  if(_inventoryChoice==""||!G.c.bonuses.Has(_inventoryChoice)){Label(targets,"先从左侧选择一份蓝图。",18,Muted);Label(comparison,"选定目标后显示同一步模拟的收益与代价。",17,Muted);WorkspaceHint("尚未选择蓝图");return;}
  var chosen=Bonus.card(G,_inventoryChoice);_blueprintScope=chosen["scope"].Text;
  if(_blueprintScope=="building")BuildingInstallTarget(targets,chosen,comparison);else ScopeInstallTarget(targets,chosen,comparison);
  var targetName=_blueprintHost==""?"待选目标":G.project_name(_blueprintHost);
  var slotName=_blueprintScope=="building"?" / "+(_blueprintSlot==""?"待选槽位":G.slot_label(_blueprintHost,_blueprintSlot)):" / "+ScopeName(_blueprintScope);
  WorkspaceHint(chosen["name"].Text+" · 未安装 "+chosen["copies"].Long+" · 已部署 "+Bonus.active_count(G,_inventoryChoice)+" · "+targetName+slotName);
  _workspaceActionBar.GetChild(_workspaceActionBar.childCount-1).SetAsFirstSibling();
 }

 void BlueprintInventoryChoices(Transform parent,string[] ids){
  foreach(var bid in ids){var id=bid;var card=Bonus.card(G,id);var box=CompactTaskCard(parent,"Inventory "+id);var row=Column(box,"Inventory choice",5);var visual=Node("Inventory illustration",row);Width(visual,82*TextScale);Height(visual,65*TextScale);BlueprintArt(visual,id,62*TextScale);var info=Column(row,"Inventory summary",2);info.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);Label(info,card["name"].Text,18,Ink,28*TextScale);Label(info,"未安装 "+card["copies"].Long+" · 已部署 "+Bonus.active_count(G,id),16,Muted);var button=Button(row,card["copies"].Long>0?"选择":"查看",()=>{_inventoryChoice=id;_blueprintScope=card["scope"].Text;_installView=V.Map();_blueprintVersion="";RenderPage();},_inventoryChoice==id,_blueprintBusy);Height((RectTransform)button.transform,42*TextScale);
   button.name="InventoryChoice "+id;
   Label(box,BlueprintEffectSummary(card),16,Ink);
   foreach(var instance in S["bonus_instances"].Values.Where(x=>x.Get("active",false).Bool&&x["bonus_id"].Text==id)){var at=instance;var location=Button(box,"↳ "+G.project_name(at["host_id"].Text)+(at["slot"].Text!=""?" / "+G.slot_label(at["host_id"].Text,at["slot"].Text):" / "+ScopeName(at["host_type"].Text)),()=>{if(at["host_type"].Text=="building"){SelectBuilding(at["host_id"].Text);Navigate("building",at["host_id"].Text);}else ShowMessage("已部署于"+G.project_name(at["host_id"].Text)+" · "+ScopeName(at["host_type"].Text));});location.name="InstalledLocation "+at["id"].Text;Fold(box,"实例来源与完整记录",deep=>Raw(deep,at));}
  }
 }
 void ChooseBlueprintTarget(string host,string slot=""){HideDeskTip();_blueprintHost=host;_blueprintSlot=slot;_installView=V.Map();_blueprintVersion="";RenderPage();if(slot!=""){var action=_body.GetComponentsInChildren<Button>().FirstOrDefault(x=>(x.GetComponentInChildren<Text>()?.text.StartsWith("安装 · ")==true||x.GetComponentInChildren<Text>()?.text.StartsWith("替换 · ")==true));if(action!=null){EventSystem.current.SetSelectedGameObject(action.gameObject);ScrollDeskFocus(action.transform);}}}
 void BuildingInstallTarget(Transform parent,V chosen,Transform comparison=null){
  var bid=chosen["bonus_id"].Text;var projects=G.playable_ids().Select(x=>x.Text).OrderByDescending(id=>G.slot_ids(id).Any(s=>G.bonus_eligible(bid,"building",id,s.Text,S["projects"][id]["slots"][s].Text)=="")).ToArray();
  if(_blueprintHost==""||!S["projects"].Has(_blueprintHost))_blueprintHost=SelectedBuilding!=""&&S["projects"].Has(SelectedBuilding)?SelectedBuilding:projects.FirstOrDefault()??"";
  foreach(var id in projects){var host=id;bool fits=G.slot_ids(host).Any(x=>G.bonus_eligible(bid,"building",host,x.Text,S["projects"][host]["slots"][x].Text)=="");Button(parent,(fits?"✓ ":"— ")+G.project_name(host)+" · "+S["projects"][host]["tile"].Text,()=>ChooseBlueprintTarget(host),_blueprintHost==host);}
  if(_blueprintHost==""){Label(parent,"先建成一座适用建筑，再安装这份方案。",18,Muted);return;}
  BuildingOverviewPaper(parent,_blueprintHost,true);
  if(_blueprintSlot!=""&&(!G.slot_ids(_blueprintHost).Any(x=>x.Text==_blueprintSlot)||G.bonus_eligible(bid,"building",_blueprintHost,_blueprintSlot,S["projects"][_blueprintHost]["slots"].Get(_blueprintSlot,"").Text)!=""))_blueprintSlot="";
  var slots=Column(parent,"Blueprint Compatible Slots",6);foreach(var s in G.slot_ids(_blueprintHost)){var slot=s.Text;var iid=S["projects"][_blueprintHost]["slots"][slot].Text;var error=G.bonus_eligible(bid,"building",_blueprintHost,slot,iid);var b=Button(slots,(_blueprintSlot==slot?"✓ ":"")+G.slot_label(_blueprintHost,slot)+(error==""?iid==""?" · 可安装":" · 可替换":" · 条件不符"),()=>ChooseBlueprintTarget(_blueprintHost,slot),_blueprintSlot==slot,error!="");if(error!="")Label(parent,G.slot_label(_blueprintHost,slot)+"："+error,15,Gold);AddDeskTip(b.gameObject,"槽位适配",()=>error==""?"真实槽位与建筑适配检查已通过。安装时仍会重新检查库存、互斥和当前状态。":error);}
  if(_blueprintSlot!="")BlueprintInstallationPreview(comparison??parent,chosen,"building",_blueprintHost,_blueprintSlot);else Label(parent,"先选择建筑上的兼容槽位。",16,Muted);
 }
 void ScopeInstallTarget(Transform parent,V card,Transform comparison=null){
  var bid=card["bonus_id"].Text;var scope=card["scope"].Text;Label(parent,ScopeName(scope),22,Blue);
  Label(parent,scope=="block"?"只作用当前街区，不扩展到整个 District。":"作用于当前街区中该运营商管理的适用建筑。",16,Muted);
  if(scope=="block")_blueprintHost=S["block"]["id"].Text;
  else {var hosts=Rules.ids(S["organizations"]).Select(x=>x.Text).ToArray();if(!hosts.Contains(_blueprintHost))_blueprintHost=hosts.FirstOrDefault()??"";foreach(var hostId in hosts){var host=hostId;Button(parent,G.project_name(host),()=>ChooseBlueprintTarget(host),host==_blueprintHost);}}
  if(_blueprintHost=="")return;
  BlueprintInstallationPreview(comparison??parent,card,scope,_blueprintHost,"");
 }
 void BlueprintInstallationPreview(Transform parent,V card,string scope,string host,string slot){
  var bid=card["bonus_id"].Text;var key=_planner.version(G)+"|"+S["block"]["bonus_wallet"].ToJson()+"|"+S["bonus_instances"].ToJson()+"|"+bid+scope+host+slot;
  if(key!=_blueprintVersion||_installView.Count==0){_installView=card["copies"].Long<=0?BlueprintPreview.Inspect(G,bid,scope,host,slot):BlueprintPreview.For(G,bid,scope,host,slot);_blueprintVersion=key;}
  var view=_installView;var box=Card(parent,"安装前后 / "+card["name"].Text,"同一步模拟 · 不提前消耗真实副本",Green);
  Label(box,"安装目标："+(scope=="building"?G.project_name(host)+" · "+G.slot_label(host,slot):G.project_name(host)+" · "+ScopeName(scope)),17,Ink);
  if(!view.Get("ok",false).Bool){BlueprintPreviewUnavailable(box,card,view);return;}
  var changedStats=view["stat_rows"].Where(e=>BlueprintVisibleChange(e["before"].Double,e["after"].Double,3)).ToArray();
  if(changedStats.Length>0)Label(box,scope=="building"?"本楼规则变化":"所选范围规则变化",17,Muted);
  foreach(var adverse in new[]{false,true}){var changes=changedStats.Where(e=>BlueprintAdverse(e["field"].Text,e["before"].Double,e["after"].Double)==adverse).ToArray();if(changes.Length>0)Label(box,adverse?"代价与负向变化":"收益与正向变化",18,adverse?Gold:Green);foreach(var changed in changes){var e=changed;var line=Row(box,"Actual Rule Difference",40);Label(line,Stat(e["field"].Text),16,Muted);var b=Button(line,F(e["before"].Double,3)+" → "+F(e["after"].Double,3),()=>{});b.GetComponentInChildren<Text>().color=BlueprintAdverse(e["field"].Text,e["before"].Double,e["after"].Double)?Gold:Green;Height((RectTransform)b.transform,36*TextScale);AddDeskTip(b.gameObject,Stat(e["field"].Text)+" / 真实乘区",()=>ExplainBlueprintTrace(view,e["field"].Text));b.onClick.AddListener(()=>ShowDeskTip(b.GetComponent<CityDeskTooltip>(),true));}}
  Label(box,"全城同一步预测 · 仅显示变化",17,Muted);
  int predictionChanges=0;var step=view["same_step"];
  foreach(var measure in new[]{new[]{"cashflow","经营净额"," /月"},new[]{"energy_need","用电需求",""},new[]{"energy_supply","电力供给容量",""}}){var pair=step[measure[0]];var adverse=measure[0]=="energy_need"?pair["after"].Double>pair["before"].Double:pair["after"].Double<pair["before"].Double;if(BlueprintChangedMetric(box,measure[1],pair["before"].Double,pair["after"].Double,2,measure[2],adverse?Gold:Green))predictionChanges++;}
  foreach(var kind in step["services"].Keys){var pair=step["services"][kind];if(BlueprintChangedMetric(box,Service(kind)+"实际人数",pair["before"].Double,pair["after"].Double,2))predictionChanges++;}
  var baseline=view["baseline"];var proposal=view["proposal"];var months=step["months"].Double;
  foreach(var resource in step.Get("resource_outputs",V.Map()).Keys){
   var before=baseline["audit"].Get("resources",V.Map()).Get(resource,V.Map());var after=proposal["audit"].Get("resources",V.Map()).Get(resource,V.Map());
   var measures=new[]{new[]{"produced","月产出"},new[]{"consumed","月消耗"},new[]{"lost","月溢出损失"},new[]{"after","步末库存"}};
   var changed=measures.Where(measure=>BlueprintVisibleChange(before.Get(measure[0],0).Double/(measure[0]=="after"?1:months),after.Get(measure[0],0).Double/(measure[0]=="after"?1:months),3)).ToArray();
   if(changed.Length==0)continue;
   var resourceCard=Card(box,Resource(resource),"",Blue);
   foreach(var measure in changed){double divisor=measure[0]=="after"?1:months;if(BlueprintChangedMetric(resourceCard,measure[1],before.Get(measure[0],0).Double/divisor,after.Get(measure[0],0).Double/divisor,3))predictionChanges++;}
  }
  foreach(var field in new[]{new[]{"heat_used","实际余热利用"},new[]{"pollution_recycled","实际污染回收"}}){var before=baseline["metrics"].Get(field[0],0).Double;var after=proposal["metrics"].Get(field[0],0).Double;if(BlueprintChangedMetric(box,field[1],before,after,3))predictionChanges++;}
  foreach(var service in step.Get("service_capacity",V.Map()).Keys){var pair=step["service_capacity"][service];if(BlueprintChangedMetric(box,Service(service)+"供给容量",pair["before"].Double,pair["after"].Double,2))predictionChanges++;}
  if(predictionChanges==0)Label(box,"当前同一步预测无可见数值变化；规则与适用条件可展开来源查看。",16,Muted);

  Metric(box,"未安装副本",card["copies"].Long<=0?"0 · 目录预览，需要副本才能安装":view["before_copies"].Long+" → "+view["after_copies"].Long);
  Fold(box,"安装实例与来源",deep=>{Raw(deep,view["before_instance_records"]);Raw(deep,view["after_instance_records"]);});
  string old=scope=="building"?S["projects"][host]["slots"].Get(slot,"").Text:"";
  Label(box,card["copies"].Long<=0?"目录比较不产生安装；领取1份副本后才能执行安装。":old!=""?"替换会停用旧方案；不返还旧副本，另消耗1份新副本。":"确认消耗1份未安装副本；拆下不返还。",15,Gold);
  WorkspaceActions("取消安装预览",()=>{_blueprintSlot="";_blueprintHost="";_installView=V.Map();_inventoryChoice="";RenderPage();});
  var expectedCopies=card["copies"].Long;var focused=Runtime.Session.FocusedBlockId;WorkspaceActions(old==""?"安装 · 消耗1份":"替换 · 消耗1份",()=>{
   if(_blueprintBusy)return;_blueprintBusy=true;
   var error=Runtime.Execute(g=>{if(Runtime.Session.FocusedBlockId!=focused||g.s["block"]["bonus_wallet"].Get(bid,V.Map()).Get("charges",0).Long!=expectedCopies)return "库存或街区已变化，请重新预览。";if(scope=="building"&&g.s["projects"].Get(host,V.Map()).Get("slots",V.Map()).Get(slot,"").Text!=old)return "槽位已变化，请重新预览。";return Bonus.install(g,bid,scope,host,slot);});
   _blueprintBusy=false;_blueprintVersion="";_installView=V.Map();if(error!=""){RenderPage();return;}PlayCue("confirm");RefreshBucket();if(scope=="building"){SelectBuilding(host);Navigate("building",host);}else {RenderPage();ShowMessage("方案已部署到"+G.project_name(host)+" · "+ScopeName(scope));}
  },card["copies"].Long<=0||_blueprintBusy||!Runtime.ViewCanEditFocused);
  Fold(box,"完整效果、代价与来源",deep=>{foreach(var stat in view["stat_rows"])Label(deep,Stat(stat["field"].Text)+" "+F(stat["before"].Double,4)+" → "+F(stat["after"].Double,4),15,Muted);Raw(deep,view["rule_trace"]);Raw(deep,view["same_step"]);});
 }
 // Compare at the precision actually shown, so rounding does not produce unchanged rows.
 bool BlueprintVisibleChange(double before,double after,int digits)=>F(before==0?0:before,digits)!=F(after==0?0:after,digits)&&Math.Round(before,digits)!=Math.Round(after,digits);
 bool BlueprintChangedMetric(Transform parent,string title,double before,double after,int digits,string unit="",Color? color=null){
  if(!BlueprintVisibleChange(before,after,digits))return false;
  Metric(parent,title,F(before,digits)+" → "+F(after,digits)+unit,"",color);return true;
 }
 bool BlueprintAdverse(string field,double before,double after){
  bool cost=field=="pollution"||field=="energy_demand"||field=="maintenance"||field=="service_decay"||
   field=="exposure_multiplier"||field=="shortage_multiplier"||field=="population_maintenance"||field.StartsWith("input.",StringComparison.Ordinal);
  return cost?after>before:after<before;
 }
 string ExplainBlueprintTrace(V view,string field){
  var trace=view["rule_trace"]["after"].Get(field,V.Map());var before=view["before_stats"].Get(field,0).Double;var after=view["after_stats"].Get(field,0).Double;
  return "即时规则："+F(before,4)+" → "+F(after,4)+"\n统一乘区：(基础值 + 加值合计) × 各来源乘数，再应用 floor / cap / override。\n"+"base="+trace.Get("base","—").Text+"  add="+trace.Get("add","—").Text+"  multiply="+trace.Get("multiply","—").Text+"\n同宿主同名先按 w=1+(n−1)/3 合成；实际修正以本卡配置与来源为准。";
 }
 RectTransform BuildingOverviewPaper(Transform parent,string id,bool compact=false){
  var p=S["projects"][id];var paper=PaperDocument(parent,G.project_name(id),"建筑概况 / T"+p.Get("tier",1).Long+" · "+G.c.operators[p["operator_org"]]["name"].Text+" · "+p["tile"].Text);
  var portrait=Node("Native Building Wire Drawing",paper);Height(portrait,compact?78*TextScale:106*TextScale);var art=portrait.gameObject.AddComponent<CityBlueprintArt>();art.Style=CityIllustrationCatalog.StyleForBuilding(p["def"].Text);art.Accent=Domain(p);art.Fill=.5f;art.color=new Color(.67f,.84f,.85f);art.raycastTarget=false;
  foreach(var slotValue in G.slot_ids(id)){var slot=slotValue.Text;var iid=p["slots"][slot].Text;var row=Row(paper,"Pinned Slot "+slot,compact?55*TextScale:68*TextScale);var icon=Node("Slot drawing",row);Width(icon,compact?48*TextScale:58*TextScale);Height(icon,compact?48*TextScale:58*TextScale);var info=Column(row,"Slot summary",2);info.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);PaperLabel(info,G.slot_label(id,slot),15);
   if(iid==""){PaperLabel(info,"空闲槽位",14,true);if(!compact){var action=Button(row,"选择蓝图",()=>{_blueprintHost=id;_blueprintSlot=slot;Navigate("bonuses");});Width((RectTransform)action.transform,145*TextScale);}}
   else {var ins=S["bonus_instances"][iid];var bid=ins["bonus_id"].Text;var drawing=icon.gameObject.AddComponent<CityBlueprintArt>();drawing.Style=BlueprintStyle(bid);drawing.Accent=BlueprintAccent(bid);drawing.Fill=1;drawing.color=new Color(.67f,.84f,.85f);drawing.raycastTarget=false;var marker=PaperLabel(info,"✓ 已部署 · "+G.c.bonuses[bid]["name"].Text,14,true);marker.gameObject.name="InstalledSlot "+iid;if(!compact){var action=Button(row,"管理 / 替换",()=>Navigate("slot",id+"|"+slot));Width((RectTransform)action.transform,145*TextScale);Fold(paper,"实例来源与完整记录",deep=>Raw(deep,ins));}}
  }
  return paper;
 }
 void NativeSlotPage(){
  var parts=_pageContext.Split('|');if(parts.Length<2||!S["projects"].Has(parts[0]))return;var host=parts[0];var slot=parts[1];var iid=S["projects"][host]["slots"].Get(slot,"").Text;
  var columns=WorkspaceColumns("Building slot workspace",.9f,1.05f,1.35f);var inventory=columns[0];var target=columns[1];var comparison=columns[2];Label(inventory,"可用库存",21,Gold);BuildingOverviewPaper(target,host);
  if(iid!=""){var rack=Card(target,"管理已安装方案","拆下不返还副本；替换另耗新副本。",Gold);var current=iid;var instance=S["bonus_instances"][iid];Label(rack,"原提供方 "+G.c.operators[instance["provider_org"]]["name"].Text+" · 合同组织 "+G.c.operators[instance.Get("contract_org",instance["provider_org"])]["name"].Text,16,Muted);Fold(rack,"已安装规则与实例来源",deep=>{Raw(deep,instance);Raw(deep,Rules.bonus_rules(G,instance["bonus_id"].Text,Math.Max(1,Bonus.active_count(G,instance["bonus_id"].Text,"building",host))));});Button(rack,"拆下当前方案（不返还副本）",()=>Confirm("拆下当前方案？","旧实例会停用，副本不会退回；依赖的弹性槽须先清空。",()=>Command(g=>{if(g.s["projects"][host]["slots"].Get(slot,"").Text!=current)return "槽位已变化，请重新查看。";return g.clear_slot(host,slot);})));Button(rack,"重签为本楼自营 · "+F(G.c.balance["recontract_cost"].Double,0),()=>Command(g=>g.recontract(host,slot)));}
  var availableIds=Rules.ids(S["block"]["bonus_wallet"]).Select(x=>x.Text).Where(bid=>S["block"]["bonus_wallet"][bid].Get("charges",0).Long>0&&Bonus.card(G,bid)["scope"].Text=="building").ToArray();
  if(availableIds.Length==0)Label(inventory,"暂无未安装的建筑方案副本。",17,Muted);
  foreach(var value in availableIds){var bid=value;var err=G.bonus_eligible(bid,"building",host,slot,iid);var card=Bonus.card(G,bid);var box=CompactTaskCard(inventory,"Slot inventory "+bid);BlueprintArt(box,bid,76);Label(box,card["name"].Text,18,Ink);Label(box,"未安装 "+card["copies"].Long+" · "+BlueprintEffectSummary(card),16,Muted);Button(box,_inventoryChoice==bid?"✓ 已选择":"比较安装",()=>{_inventoryChoice=bid;_blueprintScope="building";_blueprintHost=host;_blueprintSlot=slot;RenderPage();},_inventoryChoice==bid,err!="");if(err!="")Label(box,err,15,Gold);}
  WorkspaceHint(G.project_name(host)+" / "+G.slot_label(host,slot)+" · "+(_inventoryChoice!=""&&G.c.bonuses.Has(_inventoryChoice)?G.c.bonuses[_inventoryChoice]["name"].Text:"待选蓝图"));
  if(_inventoryChoice!=""&&G.c.bonuses.Has(_inventoryChoice)&&_blueprintHost==host&&_blueprintSlot==slot)BlueprintInstallationPreview(comparison,Bonus.card(G,_inventoryChoice),"building",host,slot);else Label(comparison,"从库存选择蓝图，比较当前槽位的真实收益与代价。",17,Muted);
 }

 RectTransform CompactTaskCard(Transform parent,string name){
  var card=Surface(name,parent,new Color(.055f,.11f,.125f,.96f));var layout=card.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(12,12,10,10);layout.spacing=6;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;card.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;return card;
 }
 string TaskAmount(double value)=>value.ToString("0.########",System.Globalization.CultureInfo.InvariantCulture);
 string TaskChoiceCost(V definition,string choiceId){
  if(choiceId=="decline")return "无支出 · 无惩罚";
  var parts=new List<string>();double money=definition.Get("currency",0).Double,deposit=definition.Get("deposit",0).Double;
  if(money+deposit>0)parts.Add("即时支付 "+TaskAmount(money+deposit)+" 通货"+(deposit>0?"（含保证金 "+TaskAmount(deposit)+"，完成退还）":""));
  foreach(var id in definition.Get("resources",V.Map()).Keys)parts.Add("交付 "+TaskAmount(definition["resources"][id].Double)+" "+Resource(id));
  return parts.Count==0?"无即时支出":string.Join(" · ",parts);
 }
 string TaskChoiceOutcome(V definition,string choiceId,string fallback){
  if(choiceId=="decline")return "婉拒这份委任，不新增任务";
  var parts=new List<string>();
  bool later=definition.Has("metric")||definition.Has("delivery_resource")||definition.Has("trade_amount")||definition.Has("person_months");
  if(definition.Get("arrival_people",0).Double>0)parts.Add("下个来生月迎来 "+TaskAmount(definition["arrival_people"].Double)+" 人");
  if(definition.Has("metric")&&definition.Has("improve"))parts.Add("目标：将"+Service(definition["metric"].Text)+"覆盖提高 "+TaskAmount(definition["improve"].Double*100)+" 个百分点");
  if(definition.Get("delivery_amount",0).Double>0)parts.Add("目标：交付 "+TaskAmount(definition["delivery_amount"].Double)+" "+Resource(definition.Get("delivery_resource","").Text));
  if(definition.Get("trade_amount",0).Double>0)parts.Add("目标：实际"+(definition.Get("trade_side","").Text=="buy"?"进口":"成交")+" "+TaskAmount(definition["trade_amount"].Double)+" "+Resource(definition.Get("trade_resource","goods").Text));
  if(definition.Get("person_months",0).Double>0)parts.Add("目标：累计服务 "+TaskAmount(definition["person_months"].Double)+" 人·月");
  if(definition.Get("deadline_months",0).Double>0)parts.Add("期限 "+TaskAmount(definition["deadline_months"].Double)+" 月");
  if(definition.Get("reward_kind","").Text=="research")parts.Add((later?"完成后":"接受即")+"获得蓝图研究提案");
  else if(definition.Get("reward_kind","").Text=="copy")parts.Add((later?"完成后":"接受即")+"获得方案选择提案");
  if(definition.Get("cash_reward",0).Double>0)parts.Add((later?"完成奖励 ":"接受即得 ")+TaskAmount(definition["cash_reward"].Double)+" 通货");
  var candidates=definition.Get("bonus_ids",V.Arr());
  if(candidates.Count>0)parts.Add("候选方向："+string.Join("、",candidates.Take(3).Select(id=>G.c.bonuses.Get(id,V.Map()).Get("name","蓝图方案").Text))+(candidates.Count>3?"等":""));
  return parts.Count==0?fallback:string.Join(" · ",parts);
 }
 string TaskChoiceAffordability(V definition,string choiceId){
  if(choiceId=="decline")return "";
  if(S.Get("currency",0).Double+1e-06<definition.Get("currency",0).Double+definition.Get("deposit",0).Double)return "当前通货不足";
  foreach(var resource in definition.Get("resources",V.Map()).Keys)if(G.inventory_total(resource)+1e-06<definition["resources"][resource].Double)return Resource(resource)+"库存不足";
  return "";
 }
 void PlanningTaskEvents(Transform parent,V view,string filter=""){
  foreach(var eventValue in view["events"]){
   var evt=eventValue;var offered=evt["status"].Text=="offered";if(filter=="offered"&&!offered||filter=="active"&&offered)continue;
   var id=evt["id"].Text;
   if(!offered){var active=CompactTaskCard(parent,"Active contract "+id);Label(active,"进行中 · "+evt["name"].Text,19,Blue,29*TextScale);Label(active,evt["description"].Text,15,Muted);if(evt.Get("deadline_seconds",-1).Double>=0)Label(active,"剩余期限  "+Clock.duration(G,evt["deadline_seconds"].Double),15,Gold);
    if(evt.Get("target",0).Double>0){var factor=evt.Get("unit","").Text=="%"?100:1;var progress=Row(active,"Contract progress",31*TextScale);Label(progress,"实际进度",15,Muted);var reading=Label(progress,F(evt.Get("progress",0).Double*factor,1)+" / "+F(evt["target"].Double*factor,1)+evt.Get("unit","").Text,15,Ink);reading.alignment=TextAnchor.MiddleRight;}
    if(evt.Get("delivery_remaining",0).Double>0){var resource=evt["delivery_resource"].Text;Button(active,"交付 "+Resource(resource)+" · 还需 "+F(evt["delivery_remaining"].Double),()=>Command(g=>Themes.deliver(g,resource,-1,id)),true);}
    Fold(active,"合同结果、代价与来源",deep=>{var definition=evt.Get("definition",Themes._event_definition(G,id));Label(deep,TaskChoiceCost(definition,"accepted"),16,Gold);Label(deep,TaskChoiceOutcome(definition,"accepted",evt["description"].Text),16,Muted);Raw(deep,evt);Raw(deep,definition);});continue;}
   var definition=evt.Get("definition",Themes._event_definition(G,id));
   var card=CompactTaskCard(parent,"Card "+evt["name"].Text);Label(card,"待决定 · "+evt["name"].Text,22,Gold,33*TextScale);Label(card,evt["description"].Text,16,Muted);
   foreach(var raw in evt["choices"]){var choice=raw;var cid=choice["id"].Text;var def=cid=="decline"?V.Map():definition.Get("choices",V.Arr()).Count>0?Themes._event_choice_definition(definition,cid):definition;
    var option=Row(card,"Proposal "+cid,110*TextScale);option.GetComponent<HorizontalLayoutGroup>().spacing=9;option.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;
    var art=Node("Proposal art",option);Width(art,86*TextScale);Height(art,86*TextScale);if(cid=="decline"){var mark=Label(art,"—",28,Muted,74);Stretch(mark.rectTransform);mark.alignment=TextAnchor.MiddleCenter;}else Illustration(art,OptionPicture(id,cid,def),82*TextScale);
    var copy=Column(option,"Proposal detail",5);copy.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);var copyWidth=copy.gameObject.AddComponent<LayoutElement>();copyWidth.minWidth=0;copyWidth.preferredWidth=0;copyWidth.flexibleWidth=1;
    Label(copy,choice["label"].Text,20,Ink);
    Label(copy,TaskChoiceOutcome(def,cid,evt["description"].Text),16,Ink);
    Label(copy,TaskChoiceCost(def,cid),16,cid=="decline"?Muted:Gold);
    var unavailable=choice.Get("available",true).Bool?TaskChoiceAffordability(def,cid):choice.Get("reason","当前不可选").Text;
    if(unavailable!="")Label(copy,unavailable,16,Gold);
    var caption=cid=="decline"?"婉拒":choice["label"].Text;
    var choose=Button(copy,caption,()=>Command(g=>Themes.choose_event(g,id,cid)),cid!="decline",!choice.Get("available",true).Bool);choose.name="ProposalChoice "+id+"|"+cid;
   }
   Fold(card,"候选、出现条件与完整规则",deep=>{foreach(var choice in evt["choices"]){Label(deep,choice["label"].Text+" · "+choice.Get("cost_text","").Text,16,Muted);}Raw(deep,definition);});
  }
 }
 void PlanningTasksPage(){ThemesPage();}

}
}
