using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
public sealed partial class CityInterface {
 string _v3BonusFilter="available";

 // These pages consume the same detached city and commands as the full desks.
 // Real goals/capacities supply every bar denominator; zero thresholds use text.
 void V3DevMeter(Transform parent,string title,double current,double target,string unit,bool met,bool upper=false){
  var row=Row(parent,"V3 objective reading",27*TextScale);Label(row,(met?"✓ ":"")+title,16,met?Green:Muted,27*TextScale);
  var reading=Label(row,F(current,1)+unit+" / "+(upper?"≤ ":"")+F(target,1)+unit,16,Ink,27*TextScale);reading.alignment=TextAnchor.MiddleRight;
  if(target>0){var bar=Node("V3 measured goal "+title,parent);Height(bar,8*TextScale);var chart=bar.gameObject.AddComponent<CityGraphic>();chart.Kind="bar";chart.Ratio=(float)(current/target);chart.color=met?Green:upper&&current>target?Red:Gold;chart.raycastTarget=false;}
 }
 RectTransform V3DevSection(Transform parent,string title,string note=""){
  var card=CompactTaskCard(parent,"V3 "+title);Label(card,title,19,Ink,30*TextScale);if(note!="")V3DevText(card,note);return card;
 }
 Text V3DevText(Transform parent,string text,Color? color=null){
  var label=Label(parent,text,15,color??Muted);var layout=label.GetComponent<LayoutElement>();layout.minHeight=23*TextScale;layout.preferredHeight=-1;return label;
 }
 Button V3DevSmallButton(Transform parent,string caption,Action action,bool primary=false,bool disabled=false){var b=Button(parent,caption,action,primary,disabled);Height((RectTransform)b.transform,35*TextScale);var text=b.GetComponentInChildren<Text>();_fontBases[text]=15;text.fontSize=Mathf.RoundToInt(15*TextScale);return b;}

 void V3TasksPage(){
  var view=Themes.view(G);var pending=Bonus.pending(G);var heading=Row(_body,"V3 task heading",36*TextScale);Label(heading,"任务与委托",22,Ink,33*TextScale);V3DevSmallButton(heading,"成长详表 ↗",()=>OpenAdvancedWorkspace("tasks_home","growth"));
  V3DevText(_body,Clock.summary(G)["date"].Text+" · DT "+F(S.Get("dt",0).Double,2)+" · "+view["name"].Text);
  if(pending.Count>0)V3RewardChoices(_body,pending[0]);
  if(Runtime.MandatoryPending)V3DevSmallButton(_body,"处理周期结算",()=>Navigate("settlement"),true);
  if(view["id"].Text=="")V3ThemeChoices(_body,view);
  else if(view["completed"].Bool){var completed=V3DevSection(_body,"三个成长阶段已完成","成果与奖励已保存。DT 实际归零后按当时资格办理街区命名与纪念地标。");V3DevSmallButton(completed,"查看已完成成果",()=>OpenAdvancedWorkspace("tasks_home","completed"));}
  else {
   var stage=view["stages"][(int)view["stage"].Long-1];var goals=stage["goals"];int met=goals.Count(x=>x["met"].Bool);
   var section=V3DevSection(_body,"M"+view["stage"].Long+" · "+stage["name"].Text);
   V3DevMeter(section,"目标达标",met,goals.Count," 项",met==goals.Count);
   foreach(var goal in goals)V3TaskGoal(section,view,goal);
   if(stage["hold_months"].Double>0){V3DevMeter(section,"连续稳定",stage["held_months"].Double,stage["hold_months"].Double," 来生月",stage["held_months"].Double>=stage["hold_months"].Double);V3DevText(section,"全部状态目标同时满足后随时间累计；任一状态失守会重新计时。");}
   V3DevText(section,"阶段奖励："+ThemeStageReward(stage),Green);V3DevText(section,"阶段达成由真实城市结算自动确认；时间由右下控制推进。");
   Fold(section,"完整主题定义与来源",c=>Raw(c,Themes._definition(G,view["id"].Text)));
  }
  var ending=S["block"].Get("ending",V.Map());if(ending.Get("triggered",false).Bool){var end=V3DevSection(_body,"街区命名与纪念地标","高速发展预算已耗尽；可继续常速经营。");V3DevSmallButton(end,"办理街区定型 ↗",()=>Navigate("settlement"));}
  var contracts=V3DevSection(_body,"主题委托","合同代价与目标按实际条款；接受和交付分别执行原有城市命令。");
  if(view["events"].Count==0)V3DevText(contracts,"暂无可接受或进行中的委托。已完成与逾期记录在成长详表中保留。");
  foreach(var evt in view["events"].OrderBy(e=>e["status"].Text=="active"?0:1))V3TaskContract(contracts,evt);
  V3DevText(_body,"来源：当前城市快照 · 真实主题目标、库存与来生时间。");
 }
 void V3ThemeChoices(Transform parent,V view){
  var section=V3DevSection(parent,"选择发展主题","选择不消耗 DT；以后改向需要真实重铸权并支付 DT。");
  foreach(var choice in view["choices"]){var id=choice["id"].Text;var card=CompactTaskCard(section,"V3 theme "+id);var row=Row(card,"V3 theme choice",60*TextScale);var art=Node("Theme illustration",row);Width(art,62*TextScale);Illustration(art,ThemePicture(id),58*TextScale);var copy=Column(row,"Theme detail",3);copy.GetComponent<VerticalLayoutGroup>().padding=new RectOffset();Label(copy,choice["name"].Text,18,Green,28*TextScale);V3DevText(copy,choice["description"].Text);V3DevSmallButton(card,"选择「"+choice["name"].Text+"」",()=>Command(g=>Themes.select(g,id)),true,!Runtime.ViewCanEditFocused);}
 }
 void V3TaskGoal(Transform parent,V theme,V goal){
  var id=goal["id"].Text;var factor=goal["unit"].Text=="%"?100:1;var unit=goal["unit"].Text;bool met=goal["met"].Bool,upper=goal["comparison"].Text=="max";
  var box=CompactTaskCard(parent,"V3 goal "+id);V3DevMeter(box,goal["label"].Text,goal["value"].Double*factor,goal["target"].Double*factor,unit,met,upper);
  var row=Row(box,"V3 goal response",35*TextScale);V3DevText(row,met?"已达标":(upper?"仍需降低 ":"还差 ")+F(goal["missing"].Double*factor,1)+unit,met?Green:Gold);
  Button guide=null;guide=V3DevSmallButton(row,"如何达成",()=>ShowDeskTip(guide.GetComponent<CityDeskTooltip>(),true));AddDeskTip(guide.gameObject,goal["label"].Text+" · 如何达成",()=>ThemeGoalHint(id));
  if(goal["kind"].Text=="delivery"&&!met){var resource=id.Substring(9);var available=G.inventory_total(resource);var reserve=resource=="food"&&theme["id"].Text=="agricultural"?3*Themes._population_metrics(G).Get("food_demand",0).Double:0;var take=Math.Min(goal["missing"].Double,Math.Max(0,available-reserve));V3DevSmallButton(row,"交付 "+F(take)+" "+Resource(resource),()=>Command(g=>Themes.deliver(g,resource)),true,take<=.000001||!Runtime.ViewCanEditFocused);V3DevText(box,"库存 "+F(available)+(reserve>0?" · 保留口粮 "+F(reserve):"")+" · 本次扣除 "+F(take),Gold);}
  else V3DevSmallButton(row,"处理 ›",()=>V3GoalDestination(id));
 }
 void V3GoalDestination(string id){
  if(id=="trade")Navigate("market");else if(id=="currency")Navigate("economy_home");
  else if(id=="food_stock_months"||id=="food_self_sufficiency"||id=="food_surplus")Navigate("resources","food");
  else if(id=="housed"||id=="homeless"||id=="health"||id=="happiness"||id=="commercial_people"||id=="commercial_person_months"||id=="industrial_labor"||id=="food"||id=="medical"||id=="education"||id=="commercial"||id=="entertainment")Navigate("population_home");
  else Navigate("construction");
 }
 void V3TaskContract(Transform parent,V evt){
  var id=evt["id"].Text;var definition=evt["definition"];bool offered=evt["status"].Text=="offered";
  var box=V3DevSection(parent,(offered?"待决定 · ":"进行中 · ")+evt["name"].Text);
  if(offered){
   V3DevText(box,evt["description"].Text);
   foreach(var raw in evt["choices"]){var choice=raw;var cid=choice["id"].Text;var def=cid=="decline"?V.Map():definition.Get("choices",V.Arr()).Count>0?Themes._event_choice_definition(definition,cid):definition;
    var option=CompactTaskCard(box,"V3 contract choice "+id+" "+cid);if(cid!="decline"){var row=Row(option,"V3 contract option",48*TextScale);var art=Node("Contract illustration",row);Width(art,50*TextScale);Illustration(art,OptionPicture(id,cid,def),46*TextScale);V3DevText(row,choice["label"].Text,Ink);V3DevText(option,TaskChoiceOutcome(def,cid,evt["description"].Text).Replace(" 月"," 来生月"),Green);}
    V3DevText(option,TaskChoiceCost(def,cid),cid=="decline"?Muted:Gold);var reason=choice.Get("available",true).Bool?TaskChoiceAffordability(def,cid):choice.Get("reason","当前不可选").Text;if(reason!="")V3DevText(option,reason,Gold);
    V3DevSmallButton(option,cid=="decline"?"婉拒":choice["label"].Text,()=>Command(g=>Themes.choose_event(g,id,cid)),cid!="decline",!choice.Get("available",true).Bool||reason!=""||!Runtime.ViewCanEditFocused);
   }
  }else {
   if(evt.Get("deadline_seconds",-1).Double>=0)V3DevText(box,"剩余 "+F(evt["deadline_seconds"].Double/Themes.MONTH_SECONDS,2)+" 来生月",Gold);
   var factor=evt.Get("unit","").Text=="%"?100:1;if(evt.Get("target",0).Double>0)V3DevMeter(box,"实际进度",evt["progress"].Double*factor,evt["target"].Double*factor,evt.Get("unit","").Text,evt["progress"].Double>=evt["target"].Double);
   V3DevText(box,TaskChoiceOutcome(definition,"accepted",evt["description"].Text).Replace(" 月"," 来生月"),Green);V3DevText(box,"接受时的支出："+TaskChoiceCost(definition,"accepted").Replace("即时支付 ",""),Gold);
   if(definition.Get("deposit",0).Double>0)V3DevText(box,"完成退还保证金 "+F(definition["deposit"].Double)+" 通货；逾期失去该机会与已付保证金。");
   if(evt.Get("delivery_remaining",0).Double>0){var resource=evt["delivery_resource"].Text;var reserve=resource=="food"?definition.Get("reserve_months",0).Double*Themes._population_metrics(G).Get("food_demand",0).Double:0;var stock=G.inventory_total(resource);var take=Math.Min(evt["delivery_remaining"].Double,Math.Max(0,stock-reserve));V3DevText(box,Resource(resource)+"库存 "+F(stock)+" · 还需 "+F(evt["delivery_remaining"].Double)+(reserve>0?" · 保留口粮 "+F(reserve):""));V3DevSmallButton(box,"交付 "+F(take)+" "+Resource(resource),()=>Command(g=>Themes.deliver(g,resource,-1,id)),true,take<=.000001||!Runtime.ViewCanEditFocused);}
   else if(definition.Has("trade_amount"))V3DevSmallButton(box,"前往市场成交",()=>Navigate("market"));
   else if(definition.Has("metric")||definition.Has("person_months"))V3DevSmallButton(box,"查看人口与服务",()=>Navigate("population_home"));
  }
  Fold(box,"完整合同与来源",c=>{Raw(c,evt);Raw(c,definition);});
 }
 void V3RewardChoices(Transform parent,V reward){
  var rid=reward["reward_id"].Text;_rewardSelections.TryGetValue(rid,out var chosen);chosen=chosen??"";var kind=reward.Get("kind","copy").Text;
  var section=V3DevSection(parent,"待领取奖励 · "+Bonus.pending(G).Count,BlueprintRewardSource(reward)+" · "+reward["choices"].Count+" 选 1 · 按保存顺序领取");
  foreach(var raw in reward["choices"]){var candidate=raw;var bid=candidate["bonus_id"].Text;var box=CompactTaskCard(section,"V3 reward "+rid+" "+bid);var row=Row(box,"V3 reward choice",68*TextScale);var art=Node("Reward drawing",row);Width(art,70*TextScale);BlueprintArt(art,bid,66*TextScale,chosen==bid?1:.35f);var copy=Column(row,"V3 reward detail",3);copy.GetComponent<VerticalLayoutGroup>().padding=new RectOffset();V3DevText(copy,candidate.Get("name",bid=="__currency__"?"通货补偿":bid).Text,chosen==bid?Gold:Ink);V3DevText(copy,BlueprintEffectSummary(candidate));V3DevSmallButton(box,chosen==bid?"✓ 已预选":"预选这项",()=>{_rewardSelections[rid]=bid;RenderPage();},chosen==bid,_blueprintBusy||!Runtime.ViewCanEditFocused);}
  V3DevText(section,kind=="research"?"研究升级本街区全部同名方案；不增加库存副本。":"领取1份部署许可；安装后才生效。",Green);
  V3DevSmallButton(section,kind=="research"?"确认研究":"确认领取",()=>ClaimBlueprint(rid,chosen),true,chosen==""||_blueprintBusy||!Runtime.ViewCanEditFocused);
  V3DevSmallButton(section,"候选完整规则与来源 ↗",()=>Navigate("blueprints"));
 }

 void V3BonusPage(){
  var wallet=S["block"].Get("bonus_wallet",V.Map());var instances=S["bonus_instances"].Values.Where(x=>x.Get("active",false).Bool).ToArray();var pending=Bonus.pending(G);
  var header=Row(_body,"V3 bonus heading",36*TextScale);Label(header,"Bonus 安装",22,Ink,33*TextScale);V3DevSmallButton(header,"蓝图与来源 ↗",()=>OpenAdvancedWorkspace("bonus_home"));
  V3DevText(_body,"库存 "+wallet.Values.Sum(w=>w.Get("charges",0).Long)+" 份 · 已安装 "+instances.Length+" 份");
  var filters=Row(_body,"V3 bonus filters",35*TextScale);foreach(var entry in new[]{new[]{"available","库存副本"},new[]{"deployed","已安装"},new[]{"all","全部方案"}}){var id=entry[0];V3DevSmallButton(filters,entry[1],()=>{_v3BonusFilter=id;_inventoryChoice="";_blueprintHost="";_blueprintSlot="";RenderPage();},_v3BonusFilter==id);}
  if(pending.Count>0)V3DevSmallButton(_body,"待领取奖励 · "+pending.Count,()=>Navigate("tasks_home"),true);
  var ids=G.c.bonuses.Keys.Where(id=>_v3BonusFilter=="available"?wallet.Get(id,V.Map()).Get("charges",0).Long>0:_v3BonusFilter=="deployed"?instances.Any(x=>x["bonus_id"].Text==id):true).OrderBy(id=>G.c.bonuses[id].Get("name",id).Text,StringComparer.Ordinal).ToArray();
  if(ids.Length==0){V3DevText(_body,_v3BonusFilter=="available"?"暂无库存副本。完成任务并领取奖励后，可在这里安装。":"暂无已安装副本。先选择库存，再指定建筑与槽位。");return;}
  if(!ids.Contains(_inventoryChoice))_inventoryChoice=ids[0];
  for(int i=0;i<ids.Length;i+=2){var row=Row(_body,"V3 bonus inventory pair",124*TextScale);row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight=true;row.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;for(int j=i;j<Math.Min(i+2,ids.Length);j++)V3BonusChoice(row,ids[j]);if(i+1==ids.Length){var spacer=Node("Bonus grid spacer",row);spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;}}
  var card=Bonus.card(G,_inventoryChoice);_blueprintScope=card["scope"].Text;var detail=V3DevSection(_body,card["name"].Text,card["grade"].Text+" · "+ScopeName(_blueprintScope)+" · 库存 "+card["copies"].Long+" 份");
  V3DevText(detail,BlueprintEffectSummary(card),Ink);V3DevText(detail,card.Get("preview_reason","").Text);
  foreach(var installed in instances.Where(x=>x["bonus_id"].Text==_inventoryChoice)){var at=installed;V3DevSmallButton(detail,"已装于 "+G.project_name(at["host_id"].Text)+(at["slot"].Text!=""?" · "+G.slot_label(at["host_id"].Text,at["slot"].Text):" · "+ScopeName(at["host_type"].Text)),()=>{if(at["host_type"].Text=="building")Navigate("slot",at["host_id"].Text+"|"+at["slot"].Text);else OpenAdvancedWorkspace("bonus_home");});}
  if(_blueprintScope=="building")V3BonusBuildingTarget(detail,card);else V3BonusScopeTarget(detail,card);
  Fold(detail,"适用条件、重复规则与来源",deep=>{V3DevText(deep,card.Get("stacking_note","").Text);BlueprintRuleRows(deep,card.Get("effects",V.Arr()),_inventoryChoice);Raw(deep,G.c.bonuses[_inventoryChoice]);foreach(var instance in instances.Where(x=>x["bonus_id"].Text==_inventoryChoice))Raw(deep,instance);});
  V3DevText(_body,"来源：当前库存与安装实例 · 比较使用同一城市快照和同一步模拟，不推进月份。");
 }
 void V3BonusChoice(Transform parent,string bid){
  var card=Bonus.card(G,bid);var box=CompactTaskCard(parent,"V3 inventory "+bid);var layout=box.gameObject.AddComponent<LayoutElement>();layout.minWidth=0;layout.preferredWidth=0;layout.flexibleWidth=1;
  var row=Row(box,"V3 inventory graphic",56*TextScale);var art=Node("Inventory drawing",row);Width(art,52*TextScale);BlueprintArt(art,bid,54*TextScale,_inventoryChoice==bid?1:.35f);var copy=Column(row,"Inventory compact copy",2);copy.GetComponent<VerticalLayoutGroup>().padding=new RectOffset();V3DevText(copy,card["name"].Text,_inventoryChoice==bid?Green:Ink);V3DevText(copy,"库存 "+card["copies"].Long+" · 已装 "+Bonus.active_count(G,bid)+" · "+card["grade"].Text);
  V3DevText(box,BlueprintEffectSummary(card));V3DevSmallButton(box,_inventoryChoice==bid?"✓ 已选择":"选择方案",()=>{_inventoryChoice=bid;_blueprintHost="";_blueprintSlot="";_blueprintVersion="";_installView=V.Map();RenderPage();},_inventoryChoice==bid,_blueprintBusy);
 }
 void V3BonusSelect(Transform parent,string label,string[] labels,int selected,Action<int> choose){
  V3DevText(parent,label);if(labels.Length==0)return;var root=Surface("V3 select "+label,parent,Raised);Height(root,38*TextScale);var dropdown=root.gameObject.AddComponent<Dropdown>();var caption=Label(root,"",15,Ink);Stretch(caption.rectTransform,10,4,30,4);dropdown.captionText=caption;
  var arrow=Label(root,"⌄",15,Muted);Pin(arrow.rectTransform,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-8,0),new Vector2(18,24));
  var template=Surface("Template",root,Panel);template.anchorMin=new Vector2(0,0);template.anchorMax=new Vector2(1,0);template.pivot=new Vector2(.5f,1);template.anchoredPosition=Vector2.zero;template.sizeDelta=new Vector2(0,Math.Min(240,labels.Length*38));
  var scroll=template.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;var viewport=Surface("Viewport",template,Panel);Stretch(viewport);viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;scroll.viewport=viewport;
  var content=Node("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,38);scroll.content=content;
  var item=Surface("Item",content,Raised);item.anchorMin=new Vector2(0,1);item.anchorMax=new Vector2(1,1);item.pivot=new Vector2(.5f,1);item.sizeDelta=new Vector2(0,38);var toggle=item.gameObject.AddComponent<Toggle>();toggle.targetGraphic=item.GetComponent<Image>();var tick=Surface("Selection",item,new Color(.12f,.31f,.25f));Stretch(tick);toggle.graphic=tick.GetComponent<Image>();var text=Label(item,"",15,Ink);Stretch(text.rectTransform,10,4,10,4);dropdown.itemText=text;dropdown.template=template;dropdown.options=labels.Select(t=>new Dropdown.OptionData(t)).ToList();dropdown.value=Math.Max(0,selected);dropdown.RefreshShownValue();template.gameObject.SetActive(false);dropdown.onValueChanged.AddListener(index=>choose(index));
 }
 void V3BonusBuildingTarget(Transform parent,V card){
  var bid=card["bonus_id"].Text;var projects=G.playable_ids().Select(x=>x.Text).Where(id=>G.slot_ids(id).Any(s=>G.bonus_eligible(bid,"building",id,s.Text,S["projects"][id]["slots"][s].Text)==""||S["bonus_instances"].Get(S["projects"][id]["slots"][s].Text,V.Map()).Get("bonus_id","").Text==bid)).ToArray();
  if(_v3BonusFilter=="deployed")projects=projects.Where(id=>S["bonus_instances"].Values.Any(x=>x.Get("active",false).Bool&&x["bonus_id"].Text==bid&&x["host_type"].Text=="building"&&x["host_id"].Text==id)).ToArray();
  if(projects.Length==0){V3DevText(parent,"暂无适配建筑。建设适用建筑并开放对应槽位后即可安装。");V3DevSmallButton(parent,"前往建设",()=>Navigate("construction"));return;}
  if(!projects.Contains(_blueprintHost))_blueprintHost=projects.Contains(SelectedBuilding)?SelectedBuilding:projects[0];
  V3BonusSelect(parent,"目标建筑",projects.Select(id=>G.project_name(id)+" · "+S["projects"][id]["tile"].Text+(S["projects"][id].Get("paused",false).Bool?" · 停运":"")).ToArray(),Array.IndexOf(projects,_blueprintHost),index=>{_blueprintHost=projects[index];_blueprintSlot="";_blueprintVersion="";RenderPage();});
  var slots=G.slot_ids(_blueprintHost).Select(x=>x.Text).Where(slot=>G.bonus_eligible(bid,"building",_blueprintHost,slot,S["projects"][_blueprintHost]["slots"][slot].Text)==""||S["bonus_instances"].Get(S["projects"][_blueprintHost]["slots"][slot].Text,V.Map()).Get("bonus_id","").Text==bid).ToArray();
  if(!slots.Contains(_blueprintSlot))_blueprintSlot=(_v3BonusFilter=="deployed"?slots.FirstOrDefault(slot=>S["bonus_instances"].Get(S["projects"][_blueprintHost]["slots"][slot].Text,V.Map()).Get("bonus_id","").Text==bid):null)??slots[0];
  V3BonusSelect(parent,"目标槽位",slots.Select(slot=>{var instance=S["bonus_instances"].Get(S["projects"][_blueprintHost]["slots"][slot].Text,V.Map());return G.slot_label(_blueprintHost,slot)+" · "+(instance.Count==0?"空槽":G.c.bonuses[instance["bonus_id"]]["name"].Text);}).ToArray(),Array.IndexOf(slots,_blueprintSlot),index=>{_blueprintSlot=slots[index];_blueprintVersion="";RenderPage();});
  var actions=Row(parent,"V3 target links",35*TextScale);V3DevSmallButton(actions,"定位",()=>{SelectBuilding(_blueprintHost);InspectMapFromPage();});V3DevSmallButton(actions,"建筑",()=>Navigate("building",_blueprintHost));
  V3BonusPreview(parent,card,"building",_blueprintHost,_blueprintSlot);
 }
 void V3BonusScopeTarget(Transform parent,V card){
  var scope=card["scope"].Text;if(scope=="block")_blueprintHost=S["block"]["id"].Text;
  else {var hosts=Rules.ids(S["organizations"]).Select(x=>x.Text).ToArray();if(hosts.Length==0){V3DevText(parent,"暂无适用运营商。");return;}if(!hosts.Contains(_blueprintHost))_blueprintHost=hosts[0];V3BonusSelect(parent,"目标运营商",hosts.Select(G.project_name).ToArray(),Array.IndexOf(hosts,_blueprintHost),index=>{_blueprintHost=hosts[index];_blueprintVersion="";RenderPage();});}
  V3DevText(parent,G.project_name(_blueprintHost)+" · "+ScopeName(scope));V3BonusPreview(parent,card,scope,_blueprintHost,"");
 }
 bool V3BonusDifference(Transform parent,string label,double before,double after,string unit="",bool lower=false){
  if(!BlueprintVisibleChange(before,after,2))return false;var row=Row(parent,"V3 changed values",29*TextScale);V3DevText(row,label);var value=V3DevText(row,F(before,2)+" → "+F(after,2)+unit+"  ("+Signed(after-before,2)+")",(after>before)!=lower?Green:Gold);value.alignment=TextAnchor.MiddleRight;return true;
 }
 void V3BonusPreview(Transform parent,V card,string scope,string host,string slot){
  var bid=card["bonus_id"].Text;var old=scope=="building"?S["projects"][host]["slots"].Get(slot,"").Text:"";var prior=S["bonus_instances"].Get(old,V.Map());
  if(prior.Get("bonus_id","").Text==bid){V3DevText(parent,"该槽已安装「"+card["name"].Text+"」。同一槽无需重复安装；可从上方选择其他槽位。",Green);return;}
  if(card["copies"].Long<=0){V3DevText(parent,"没有库存副本；已安装记录仍保留。领取同名副本后可继续安装。");return;}
  var key=_planner.version(G)+"|"+S["block"]["bonus_wallet"].ToJson()+"|"+S["bonus_instances"].ToJson()+"|"+bid+scope+host+slot;if(key!=_blueprintVersion||_installView.Count==0){_installView=BlueprintPreview.For(G,bid,scope,host,slot);_blueprintVersion=key;}
  var view=_installView;if(!view.Get("ok",false).Bool){V3DevText(parent,view.Get("error","当前不能安装。").Text,Gold);return;}
  var comparison=V3DevSection(parent,"安装前后变化",old==""?"空槽 → "+card["name"].Text:G.c.bonuses[prior["bonus_id"]]["name"].Text+" → "+card["name"].Text);int changes=0;
  foreach(var stat in view["stat_rows"])if(V3BonusDifference(comparison,Stat(stat["field"].Text),stat["before"].Double,stat["after"].Double,"",BlueprintAdverse(stat["field"].Text,0,1)))changes++;
  var step=view["same_step"];V3DevText(comparison,"全城同一步预测 · 仅显示变化");foreach(var measure in new[]{new[]{"cashflow","经营净额"," /月"},new[]{"energy_supply","电力供给容量",""},new[]{"energy_need","用电需求",""}}){var pair=step[measure[0]];if(V3BonusDifference(comparison,measure[1],pair["before"].Double,pair["after"].Double,measure[2],measure[0]=="energy_need"))changes++;}
  foreach(var kind in step["services"].Keys){var pair=step["services"][kind];if(V3BonusDifference(comparison,Service(kind)+"实际人数",pair["before"].Double,pair["after"].Double," 人"))changes++;}
  foreach(var kind in step["service_capacity"].Keys){var pair=step["service_capacity"][kind];if(V3BonusDifference(comparison,Service(kind)+"供给容量",pair["before"].Double,pair["after"].Double))changes++;}
  var baseline=view["baseline"];var proposal=view["proposal"];double months=step["months"].Double;
  foreach(var resource in step["resource_outputs"].Keys){var a=baseline["audit"].Get("resources",V.Map()).Get(resource,V.Map());var b=proposal["audit"].Get("resources",V.Map()).Get(resource,V.Map());foreach(var measure in new[]{new[]{"produced","月产出"},new[]{"consumed","月消耗"},new[]{"lost","月溢出损失"},new[]{"after","步末库存"}}){double divisor=measure[0]=="after"?1:months;if(V3BonusDifference(comparison,Resource(resource)+measure[1],a.Get(measure[0],0).Double/divisor,b.Get(measure[0],0).Double/divisor,"",measure[0]=="consumed"||measure[0]=="lost"))changes++;}}
  foreach(var field in new[]{new[]{"heat_used","实际余热利用"},new[]{"pollution_recycled","实际污染回收"}})if(V3BonusDifference(comparison,field[1],baseline["metrics"].Get(field[0],0).Double,proposal["metrics"].Get(field[0],0).Double))changes++;
  if(changes==0)V3DevText(comparison,"同一步汇总暂无可见数值变化；停运、施工与短缺等当前条件已计入。");
  V3DevText(comparison,"消耗1份「"+card["name"].Text+"」 · 库存 "+card["copies"].Long+" → "+view["after_copies"].Long,Gold);V3DevText(comparison,old==""?"不额外消耗通货或材料；安装后才生效。":"原方案停用，旧副本不返还；另消耗1份新副本。",Gold);
  var expected=card["copies"].Long;var focused=Runtime.Session.FocusedBlockId;
  V3DevSmallButton(comparison,old==""?"安装副本":"替换安装",()=>{Action install=()=>V3InstallBonus(bid,scope,host,slot,old,expected,focused);if(old!="")Confirm("替换当前方案？","消耗1份「"+card["name"].Text+"」，原方案停用且旧副本不返还。",install);else install();},true,_blueprintBusy||!Runtime.ViewCanEditFocused);
  Fold(comparison,"完整比较与实例来源",c=>{Raw(c,view["rule_trace"]);Raw(c,view["same_step"]);Raw(c,view["before_instance_records"]);Raw(c,view["after_instance_records"]);});
 }
 void V3InstallBonus(string bid,string scope,string host,string slot,string old,long expected,string focused){
  if(_blueprintBusy)return;_blueprintBusy=true;var error=Runtime.Execute(g=>{if(Runtime.Session.FocusedBlockId!=focused||g.s["block"]["bonus_wallet"].Get(bid,V.Map()).Get("charges",0).Long!=expected)return "库存或街区已变化，请重新预览。";if(scope=="building"&&g.s["projects"].Get(host,V.Map()).Get("slots",V.Map()).Get(slot,"").Text!=old)return "槽位已变化，请重新预览。";return Bonus.install(g,bid,scope,host,slot);});_blueprintBusy=false;_blueprintVersion="";_installView=V.Map();if(error==""){_v3BonusFilter="all";RefreshBucket();PlayCue("confirm");ShowMessage("方案已安装到"+G.project_name(host)+" · "+(scope=="building"?G.slot_label(host,slot):ScopeName(scope)));}RenderPage();
 }
}
}
