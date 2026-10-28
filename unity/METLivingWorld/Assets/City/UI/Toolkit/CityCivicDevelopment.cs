using System;
using System.Linq;
using MET.Core;
using UnityEngine.UIElements;

namespace MET.City {
public sealed partial class CityInterface {
 string _civicBlockName="";

 // Every reading comes from the detached city. Only explicit buttons submit commands.
 bool RenderCivicDevelopment(VisualElement body,VisualElement actions){
  switch(_page){
   case "tasks_home": case "themes": case "milestones": CivicTasks(body,actions);return true;
   case "bonus_home": case "bonuses": CivicBonuses(body,actions);return true;
   case "blueprints":
    var pending=Bonus.pending(G);
    if(pending.Count==0){CivicUI.Empty(body,"所有奖励已领取","已保存的方案可在 Bonus 中选择目标并安装。");CivicUI.Button(actions,"前往 Bonus",()=>Navigate("bonus_home"),true);}
    else RenderCivicReward(body,actions,pending[0]);return true;
   case "settlement": CivicSettlement(body,actions);return true;
   case "slot": CivicSlot(body,actions);return true;
   default:return false;
  }
 }
 void CivicSource(VisualElement parent,string title,params V[] records){
  CivicUI.Fold(parent,title,source=>{foreach(var record in records)CivicUI.Label(source,record.ToJson(true,true),"c-small c-muted");});
 }
 void CivicTasks(VisualElement body,VisualElement actions){
  var theme=Themes.view(G);var pending=Bonus.pending(G);var tab=_page=="milestones"?"growth":_pageContext;
  var summary=CivicUI.Row(body,"c-wrap");
  CivicUI.Value(summary,"发展方向",theme.Get("name","尚未选择主题").Text);
  CivicUI.Value(summary,"当前日期",Clock.summary(G)["date"].Text,"DT "+F(S.Get("dt",0).Double,2));
  CivicUI.Value(summary,"待领取",pending.Count+" 份","按保存顺序办理");
  if(pending.Count>0)CivicUI.Button(actions,"选择奖励 · "+pending.Count,()=>OpenRewardModal(pending[0]["reward_id"].Text),true,!_blueprintBusy&&Runtime.ViewCanEditFocused);
  if(Runtime.MandatoryPending)CivicUI.Button(actions,"处理周期结算",()=>Navigate("settlement"));
  if(tab=="completed"){CivicCompleted(body,theme);return;}
  if(theme.Get("id","").Text==""){CivicThemeChoices(body,theme);return;}
  if(tab=="growth"){
   if(!_advancedWorkspace){
    CivicUI.Label(body,theme["name"].Text+" · 成长路线","c-heading");
    foreach(var stage in theme["stages"]){var section=CivicUI.Section(body,stage["name"].Text,stage["saved"].Bool?"✓ 成果已保存":stage["current"].Bool?"正在推进":"尚未开始");
     CivicUI.Label(section,ThemeStageReward(stage),"c-small c-good");
     if(stage["current"].Bool)CivicUI.Meter(section,"已满足目标",stage["goals"].Count(g=>g["met"].Bool),stage["goals"].Count,"项");
     else CivicUI.Label(section,stage["goals"].Count+" 项要求"+(stage["hold_months"].Double>0?" · 连续稳定 "+F(stage["hold_months"].Double)+" 来生月":""),"c-small c-muted");
    }
    CivicUI.Button(actions,"比较三阶段要求与奖励",()=>OpenAdvancedWorkspace("tasks_home","growth"),true);return;
   }
   CivicUI.Label(body,theme["name"].Text+" · 三阶段成长路线","c-heading");CivicStageRoute(body,theme,true);
   var reforge=CivicUI.Section(body,"发展方向与重铸","持有 "+theme["reforge_tokens"].Long+" 份重铸权");
   CivicUI.Label(reforge,"消耗1份重铸权与 "+F(theme["reforge_dt_cost"].Double,2)+" DT；已取得与待领取成果保留。","c-muted");
   foreach(var option in theme["choices"]){var id=option["id"].Text;if(id==theme["id"].Text)continue;
    CivicUI.Button(reforge,"改向「"+option["name"].Text+"」",()=>Confirm("确认重铸主题？","消耗1份重铸权与 "+F(theme["reforge_dt_cost"].Double,2)+" DT，已有与待领奖励保留。",()=>Command(g=>Themes.reforge(g,id))),false,theme["can_reforge"].Bool&&Runtime.ViewCanEditFocused);
   }
   if(!theme["can_reforge"].Bool)CivicUI.Label(reforge,theme["completed"].Bool?"本轮主题已完成，方向已经定型。":"当前重铸权或 DT 不足。","c-warning");
   CivicSource(reforge,"完整主题定义与来源",Themes._definition(G,theme["id"].Text));return;
  }
  CivicStageRoute(body,theme,false);
  if(theme["completed"].Bool){var done=CivicUI.Section(body,"三个成长阶段已完成");CivicUI.Label(done,"成果已保存。命名与地标资格按 DT 首次实际归零时的构筑办理。","c-good");CivicUI.Button(done,"查看已完成成果",()=>Navigate("tasks_home","completed"));}
  else {
   var stage=theme["stages"][(int)theme["stage"].Long-1];var section=CivicUI.Section(body,"M"+theme["stage"].Long+" · "+stage["name"].Text,"阶段奖励 · "+ThemeStageReward(stage));
   var goals=stage["goals"];CivicUI.Meter(section,"达标目标",goals.Count(g=>g["met"].Bool),goals.Count,"项");
   foreach(var goal in goals)CivicGoal(section,theme,goal,true);
   if(stage["hold_months"].Double>0)CivicUI.Meter(section,"连续稳定",stage["held_months"].Double,stage["hold_months"].Double,"来生月","全部状态同时达标才累计；任一状态失守会重新计时。");
   else CivicUI.Label(section,"全部目标达成即可完成本阶段，无需额外稳定时长。","c-small c-muted");
   CivicUI.Label(section,"阶段由真实城市结算自动确认；累计与交付在进入阶段后重新计算。","c-small c-muted");
   CivicSource(section,"完整主题定义与来源",Themes._definition(G,theme["id"].Text));
  }
  var contracts=CivicUI.Section(body,"当前合同与待决定委任",theme["events"].Count+" 项");
  if(theme["events"].Count==0)CivicUI.Empty(contracts,"当前没有合同事项","继续经营，新的机会会按真实条件出现。");
  foreach(var evt in theme["events"])CivicContract(contracts,evt);
 }
 void CivicThemeChoices(VisualElement body,V theme){
  var section=CivicUI.Section(body,"选择街区的发展主题","选择不消耗 DT；改向需要真实重铸权并支付代价。");
  var choices=CivicUI.Row(section,"c-wrap");foreach(var option in theme["choices"]){var id=option["id"].Text;var card=CivicUI.Section(choices,option["name"].Text);card.AddToClassList("c-third");CivicUI.Label(card,option["description"].Text,"c-muted");CivicUI.Button(card,"选择此主题",()=>Command(g=>Themes.select(g,id)),true,Runtime.ViewCanEditFocused);}
 }
 void CivicStageRoute(VisualElement body,V theme,bool detailed){
  var route=CivicUI.Row(body,"c-stages");route.name="development-three-stage-route";
  for(var i=0;i<theme["stages"].Count;i++){
   var stage=theme["stages"][i];var saved=stage["saved"].Bool;var current=stage["current"].Bool;
   var node=CivicUI.Section(route,"M"+(i+1)+" · "+stage["name"].Text,saved?"✓ 已保存":current?"当前阶段":"未来阶段");node.AddToClassList("c-third");node.EnableInClassList("is-selected",current);
   CivicUI.Label(node,ThemeStageReward(stage),"c-small c-good");
   CivicUI.Label(node,stage["hold_months"].Double>0?"同时稳定 "+F(stage["hold_months"].Double)+" 来生月":"目标达成即完成","c-small c-muted");
   if(detailed){foreach(var goal in stage["goals"]){
     if(current)CivicGoal(node,theme,goal,true);
     else {var factor=goal["unit"].Text=="%"?100:1;var unit=goal["unit"].Text;CivicUI.Pair(node,goal["label"].Text,(goal["comparison"].Text=="max"?"≤ ":"≥ ")+F(goal["target"].Double*factor,1)+unit);
      if(!saved)CivicUI.Label(node,(goal["kind"].Text=="state"?"当前城市参考：":"本阶段开始后重新累计；当前预览：")+F(goal["value"].Double*factor,1)+unit,"c-small c-muted");
      CivicUI.Label(node,ThemeGoalHint(goal["id"].Text),"c-small c-muted");
     }
    }
    if(current&&stage["hold_months"].Double>0)CivicUI.Meter(node,"已连续稳定",stage["held_months"].Double,stage["hold_months"].Double,"来生月");
    if(saved){var record=theme["history"].FirstOrDefault(x=>x.Get("kind","").Text=="milestone"&&x.Get("id","").Text==stage["id"].Text);if(record!=null)CivicSource(node,"已保存阶段凭据",record);}
   }else {CivicUI.Label(node,stage["goals"].Count+" 项目标","c-small");if(current)CivicUI.Progress(node,(double)stage["goals"].Count(x=>x["met"].Bool)/Math.Max(1,stage["goals"].Count));}
   if(detailed&&current)CivicUI.Button(node,"办理当前目标与交付",()=>Navigate("tasks_home","current"),true);
   if(i<theme["stages"].Count-1)CivicUI.Label(route,"→","c-heading c-muted");
  }
  if(detailed)CivicUI.Label(body,"未来阶段读数仅作当前城市参考；已完成只依据保存凭据。DT 实际归零时确定命名与主题地标资格。","c-small c-muted");
 }
 void CivicGoal(VisualElement parent,V theme,V goal,bool actionable){
  var box=CivicUI.Column(parent,"c-section");var id=goal["id"].Text;var factor=goal["unit"].Text=="%"?100:1;var unit=goal["unit"].Text;bool met=goal["met"].Bool,upper=goal["comparison"].Text=="max";
  CivicUI.Pair(box,(met?"✓ ":"○ ")+goal["label"].Text,F(goal["value"].Double*factor,1)+unit+"  /  "+(upper?"≤ ":"≥ ")+F(goal["target"].Double*factor,1)+unit,met?"c-good":"c-warning");
  if(!upper&&goal["target"].Double>0)CivicUI.Progress(box,goal["value"].Double/goal["target"].Double);
  CivicUI.Label(box,met?"此项目标已满足":(upper?"仍需降低 ":"还差 ")+F(goal["missing"].Double*factor,1)+unit,"c-small "+(met?"c-good":"c-warning"));
  CivicUI.Label(box,ThemeGoalHint(id),"c-small c-muted");
  if(!actionable)return;
  if(goal["kind"].Text=="delivery"&&!met){var resource=id.Substring(9);var available=G.inventory_total(resource);var reserve=resource=="food"&&theme["id"].Text=="agricultural"?3*Themes._population_metrics(G).Get("food_demand",0).Double:0;var take=Math.Min(goal["missing"].Double,Math.Max(0,available-reserve));
   CivicUI.Label(box,"库存 "+F(available)+(reserve>0?" · 保留口粮 "+F(reserve):"")+" · 本次扣除 "+F(take),"c-small c-warning");CivicUI.Button(box,"交付 "+F(take)+" "+Resource(resource),()=>Command(g=>Themes.deliver(g,resource)),true,take>.000001&&Runtime.ViewCanEditFocused);
  }else if(!met)CivicUI.Button(box,"处理此目标 ›",()=>HandleGoalDestination(id));
 }
 void CivicContract(VisualElement parent,V evt){
  var id=evt["id"].Text;var definition=evt["definition"];var offered=evt["status"].Text=="offered";var box=CivicUI.Section(parent,(offered?"待决定 · ":"进行中 · ")+evt["name"].Text);
  CivicUI.Label(box,evt["description"].Text,"c-muted");
  if(offered){var options=CivicUI.Row(box,"c-wrap");foreach(var raw in evt["choices"]){var choice=raw;var cid=choice["id"].Text;var def=cid=="decline"?V.Map():definition.Get("choices",V.Arr()).Count>0?Themes._event_choice_definition(definition,cid):definition;var option=CivicUI.Section(options,choice["label"].Text);option.AddToClassList("c-third");
    CivicUI.Label(option,TaskChoiceOutcome(def,cid,evt["description"].Text).Replace(" 月"," 来生月"));CivicUI.Label(option,TaskChoiceCost(def,cid),"c-small c-warning");
    var reason=choice.Get("available",true).Bool?TaskChoiceAffordability(def,cid):choice.Get("reason","当前不可选").Text;if(reason!="")CivicUI.Label(option,reason,"c-small c-warning");
    CivicUI.Button(option,cid=="decline"?"婉拒":choice["label"].Text,()=>Command(g=>Themes.choose_event(g,id,cid)),cid!="decline",choice.Get("available",true).Bool&&reason==""&&Runtime.ViewCanEditFocused,"ProposalChoice "+id+"|"+cid);
   }
  }else {
   if(evt.Get("deadline_seconds",-1).Double>=0)CivicUI.Pair(box,"剩余期限",F(evt["deadline_seconds"].Double/Themes.MONTH_SECONDS,2)+" 来生月","c-warning");
   if(evt.Get("target",0).Double>0){var factor=evt.Get("unit","").Text=="%"?100:1;CivicUI.Meter(box,"实际进度",evt["progress"].Double*factor,evt["target"].Double*factor,evt.Get("unit","").Text);}
   CivicUI.Label(box,"接受时的支出："+TaskChoiceCost(definition,"accepted").Replace("即时支付 ",""),"c-small c-warning");CivicUI.Label(box,TaskChoiceOutcome(definition,"accepted",evt["description"].Text).Replace(" 月"," 来生月"),"c-good");
   if(definition.Get("deposit",0).Double>0)CivicUI.Label(box,"完成退还保证金 "+F(definition["deposit"].Double)+" 通货；逾期失去该机会与已付保证金。","c-small c-muted");
   if(evt.Get("delivery_remaining",0).Double>0){var resource=evt["delivery_resource"].Text;var reserve=resource=="food"?definition.Get("reserve_months",0).Double*Themes._population_metrics(G).Get("food_demand",0).Double:0;var stock=G.inventory_total(resource);var take=Math.Min(evt["delivery_remaining"].Double,Math.Max(0,stock-reserve));CivicUI.Label(box,"库存 "+F(stock)+" · 还需 "+F(evt["delivery_remaining"].Double)+(reserve>0?" · 保留口粮 "+F(reserve):"")+" · 本次扣除 "+F(take),"c-small");CivicUI.Button(box,"交付 "+F(take)+" "+Resource(resource),()=>Command(g=>Themes.deliver(g,resource,-1,id)),true,take>.000001&&Runtime.ViewCanEditFocused);}
   else if(definition.Has("trade_amount"))CivicUI.Button(box,"前往市场成交",()=>HandleGoalDestination("trade"));
   else if(definition.Has("metric")||definition.Has("person_months"))CivicUI.Button(box,"查看人口与实际服务",()=>HandleGoalDestination(definition.Get("metric","commercial_person_months").Text));
  }
  CivicSource(box,"完整合同、条件与来源",evt,definition);
 }
 void CivicCompleted(VisualElement body,V theme){
  var stages=theme.Get("history",V.Arr()).Where(x=>x.Get("kind","").Text=="milestone").Reverse().ToArray();
  if(stages.Length==0)CivicUI.Empty(body,"尚无已保存的阶段成果","当前城市达标不会补写历史完成状态。");
  foreach(var entry in stages){var card=CivicUI.Section(body,entry["name"].Text,"第 "+F(entry.Get("local_seconds",0).Double/Themes.MONTH_SECONDS,2)+" 来生月 · 已保存");CivicUI.Label(card,entry.Get("id","").Text.EndsWith(":M1",StringComparison.Ordinal)?"阶段奖励：方案副本选择提案":"阶段奖励：蓝图研究提案","c-good");CivicSource(card,"阶段达成凭据",entry);}
  var history=CivicUI.Section(body,"合同与发展方向历史");var records=theme.Get("history",V.Arr()).Where(x=>x.Get("kind","").Text!="milestone").Reverse().ToArray();if(records.Length==0)CivicUI.Label(history,"尚无已保存记录。","c-muted");
  foreach(var entry in records){var card=CivicUI.Section(history,entry.Get("name",entry.Get("id","主题记录")).Text,entry.Get("status_label","城市记录").Text);CivicUI.Label(card,entry.Get("reason","").Text,"c-small c-muted");CivicSource(card,"来源凭据",entry);}
  CivicUI.Button(body,"领取提案与办理街区定型",()=>Navigate("settlement"));
 }
 void RenderCivicReward(VisualElement body,VisualElement actions,V reward){
  var rid=reward["reward_id"].Text;var kind=reward.Get("kind","copy").Text;_rewardSelections.TryGetValue(rid,out var chosen);chosen=chosen??"";
  CivicUI.Label(body,BlueprintRewardSource(reward)+" · "+reward["choices"].Count+" 选 1 · 按保存顺序领取","c-muted");
  var row=CivicUI.Row(body);foreach(var raw in reward["choices"]){var candidate=raw;var bid=candidate["bonus_id"].Text;var selected=chosen==bid;var card=CivicUI.Section(row,candidate.Get("name",bid=="__currency__"?"通货补偿":bid).Text);card.AddToClassList("c-third");card.EnableInClassList("is-selected",selected);
   CivicUI.Icon(card,bid=="__currency__"?"currency":"bonus");CivicUI.Label(card,BlueprintEffectSummary(candidate));CivicUI.Label(card,bid=="__currency__"?"直接入账，不恢复 DT":kind=="research"?candidate.Get("grade","基础").Text+" → "+candidate.Get("target_grade","改良").Text+"；升级本街区同名方案，不增加库存、不扩大部署尺度。":"获得1份部署许可，安装后才生效。","c-small c-muted");
   CivicUI.Button(card,selected?"✓ 已预选":"预选这项",()=>{_rewardSelections[rid]=bid;PlayCue("select");RenderPage();},selected,!_blueprintBusy&&Runtime.ViewCanEditFocused,"reward-candidate-"+bid);
   if(bid!="__currency__"){CivicEffectRows(card,candidate.Get("effects",V.Arr()));CivicUI.Label(card,candidate.Get("preview_reason","").Text,"c-small c-muted");CivicUI.Label(card,candidate.Get("stacking_note","").Text,"c-small c-muted");CivicSource(card,"适用条件与完整来源",G.c.bonuses[bid]);}
  }
  var valid=reward["choices"].Any(c=>c["bonus_id"].Text==chosen);CivicUI.Button(actions,"取消预选",()=>{_rewardSelections.Remove(rid);RenderPage();},false,valid&&!_blueprintBusy);
  CivicUI.Button(actions,kind=="research"?"确认研究":"确认领取",()=>ClaimBlueprint(rid,chosen),true,valid&&!_blueprintBusy&&Runtime.ViewCanEditFocused,"reward-confirm");
 }
 void CivicEffectRows(VisualElement parent,V effects){
  if(effects.Count==0)CivicUI.Label(parent,"包含结构、关系或条件规则，请查阅完整来源。","c-small c-muted");
  foreach(var effect in effects)CivicUI.Pair(parent,Stat(effect.Get("label","规则").Text),FormatEffect(effect,effect.Get("before",0).Double)+" → "+FormatEffect(effect,effect.Get("after",0).Double),BlueprintEffectAdverse(effect)?"c-warning":"c-good");
 }
 void CivicBonuses(VisualElement body,VisualElement actions){
  var wallet=S["block"].Get("bonus_wallet",V.Map());var instances=S["bonus_instances"].Values.Where(x=>x.Get("active",false).Bool).ToArray();var summary=CivicUI.Row(body);
  CivicUI.Value(summary,"库存副本",wallet.Values.Sum(x=>x.Get("charges",0).Long)+" 份");CivicUI.Value(summary,"已安装",instances.Length+" 份");CivicUI.Value(summary,"待领取奖励",Bonus.pending(G).Count+" 份");
  var filter=CivicUI.Row(body,"c-wrap");foreach(var entry in new[]{new[]{"available","库存副本"},new[]{"deployed","已安装"},new[]{"all","全部方案"}}){var id=entry[0];CivicUI.Button(filter,entry[1],()=>{_v3BonusFilter=id;RenderPage();},_v3BonusFilter==id);}
  var search=CivicUI.Row(body,"c-wrap");CivicUI.Field(search,"搜索方案",_blueprintQuery,value=>_blueprintQuery=value).name="bonus-search";CivicUI.Button(search,"搜索",RenderPage);if(_blueprintQuery!="")CivicUI.Button(search,"清除",()=>{_blueprintQuery="";RenderPage();});
  var ids=FilteredBlueprintIds();if(ids.Length==0){CivicUI.Empty(body,"没有符合筛选的方案","可切换全部方案或清除搜索；已选择的目标保留。档案与副本不会因筛选而改变。");return;}
  if(!G.c.bonuses.Has(_inventoryChoice))_inventoryChoice=ids[0];
  var columns=CivicUI.Row(body);var inventory=CivicUI.Column(columns,"c-third");var detail=CivicUI.Column(columns,"c-grow");
  foreach(var bid in ids){var id=bid;var card=Bonus.card(G,id);CivicUI.Choice(inventory,card["name"].Text,"库存 "+card["copies"].Long+" · 已装 "+Bonus.active_count(G,id)+" · "+card["grade"].Text+"\n"+BlueprintEffectSummary(card),_inventoryChoice==id,()=>{_inventoryChoice=id;_blueprintVersion="";_installView=V.Map();RenderPage();},"bonus");}
  var selected=Bonus.card(G,_inventoryChoice);_blueprintScope=selected["scope"].Text;var header=CivicUI.Section(detail,selected["name"].Text,selected["grade"].Text+" · "+ScopeName(_blueprintScope));
  CivicUI.Label(header,BlueprintEffectSummary(selected));CivicUI.Label(header,selected["preview_reason"].Text,"c-small c-muted");
  foreach(var installed in instances.Where(x=>x["bonus_id"].Text==_inventoryChoice)){var at=installed;var record=CivicUI.Section(header,"已装于 "+G.project_name(at["host_id"].Text),at["host_type"].Text=="building"?G.slot_label(at["host_id"].Text,at["slot"].Text):ScopeName(at["host_type"].Text));
   if(at["host_type"].Text=="building")CivicUI.Button(record,"管理这个槽位",()=>Navigate("slot",at["host_id"].Text+"|"+at["slot"].Text));CivicSource(record,"安装实例与历史来源",at);
  }
  CivicBonusTargets(detail,selected);CivicUI.Fold(detail,"适用条件、重复规则与来源",deep=>{CivicUI.Label(deep,selected["stacking_note"].Text,"c-muted");CivicEffectRows(deep,selected["effects"]);CivicSource(deep,"完整方案定义",G.c.bonuses[_inventoryChoice]);});
  var pending=Bonus.pending(G);if(pending.Count>0)CivicUI.Button(actions,"选择奖励 · "+pending.Count,()=>OpenRewardModal(pending[0]["reward_id"].Text),true,Runtime.ViewCanEditFocused);
 }
 void CivicBonusTargets(VisualElement parent,V card){
  var bid=card["bonus_id"].Text;var scope=card["scope"].Text;
  if(scope=="building"){
   var hosts=G.playable_ids().Select(x=>x.Text).Where(id=>G.slot_ids(id).Any(s=>G.bonus_eligible(bid,"building",id,s.Text,S["projects"][id]["slots"][s].Text)==""||S["bonus_instances"].Get(S["projects"][id]["slots"][s].Text,V.Map()).Get("bonus_id","").Text==bid)).ToArray();
   if(hosts.Length==0){CivicUI.Empty(parent,"暂无适配建筑","建设适用建筑并开放对应槽位后即可安装。");CivicUI.Button(parent,"前往建设",()=>Navigate("construction"));return;}
   if(!hosts.Contains(_blueprintHost))_blueprintHost=hosts.Contains(SelectedBuilding)?SelectedBuilding:hosts[0];
   CivicUI.Select(parent,"目标建筑",hosts.Select(id=>G.project_name(id)+" · "+S["projects"][id]["tile"].Text+(S["projects"][id].Get("paused",false).Bool?" · 停运":"")).ToList(),Array.IndexOf(hosts,_blueprintHost),index=>{_blueprintHost=hosts[index];_blueprintSlot="";_blueprintVersion="";RenderPage();});
   var slots=G.slot_ids(_blueprintHost).Select(x=>x.Text).Where(slot=>G.bonus_eligible(bid,"building",_blueprintHost,slot,S["projects"][_blueprintHost]["slots"][slot].Text)==""||S["bonus_instances"].Get(S["projects"][_blueprintHost]["slots"][slot].Text,V.Map()).Get("bonus_id","").Text==bid).ToArray();
   if(!slots.Contains(_blueprintSlot))_blueprintSlot=slots[0];
   CivicUI.Select(parent,"目标槽位",slots.Select(slot=>G.slot_label(_blueprintHost,slot)+" · "+CivicSlotBonusName(_blueprintHost,slot)).ToList(),Array.IndexOf(slots,_blueprintSlot),index=>{_blueprintSlot=slots[index];_blueprintVersion="";RenderPage();});
   var links=CivicUI.Row(parent);CivicUI.Button(links,"定位建筑",()=>{SelectBuilding(_blueprintHost);InspectMapFromPage();});CivicUI.Button(links,"建筑详情",()=>Navigate("building",_blueprintHost));CivicUI.Button(links,"管理槽位",()=>Navigate("slot",_blueprintHost+"|"+_blueprintSlot));
  }else if(scope=="block"){_blueprintHost=S["block"]["id"].Text;_blueprintSlot="";CivicUI.Label(parent,G.project_name(_blueprintHost)+" · 本街区方案");}
  else {
   var hosts=Rules.ids(S["organizations"]).Select(x=>x.Text).ToArray();if(hosts.Length==0){CivicUI.Empty(parent,"暂无适用运营商");return;}if(!hosts.Contains(_blueprintHost))_blueprintHost=hosts[0];_blueprintSlot="";
   CivicUI.Select(parent,"目标运营商",hosts.Select(G.project_name).ToList(),Array.IndexOf(hosts,_blueprintHost),index=>{_blueprintHost=hosts[index];_blueprintVersion="";RenderPage();});
  }
  CivicBonusPreview(parent,card,scope,_blueprintHost,_blueprintSlot);
 }
 string CivicSlotBonusName(string host,string slot){var instance=S["bonus_instances"].Get(S["projects"][host]["slots"].Get(slot,"").Text,V.Map());return instance.Count==0?"空槽":G.c.bonuses[instance["bonus_id"]]["name"].Text;}
 bool CivicDifference(VisualElement parent,string title,double before,double after,string unit="",bool lower=false){
  if(!BlueprintVisibleChange(before,after,2))return false;CivicUI.Pair(parent,title,F(before,2)+" → "+F(after,2)+unit+"  ("+Signed(after-before,2)+")",(after>before)!=lower?"c-good":"c-warning");return true;
 }
 void CivicBonusPreview(VisualElement parent,V card,string scope,string host,string slot){
  var bid=card["bonus_id"].Text;var old=scope=="building"?S["projects"][host]["slots"].Get(slot,"").Text:"";var prior=S["bonus_instances"].Get(old,V.Map());
  if(prior.Get("bonus_id","").Text==bid){CivicUI.Label(parent,"该槽已经安装「"+card["name"].Text+"」。同名原位替换没有新增效果，不消耗副本。","c-good");return;}
  var key=_planner.version(G)+"|"+S["block"]["bonus_wallet"].ToJson()+"|"+S["bonus_instances"].ToJson()+"|"+bid+scope+host+slot;
  if(key!=_blueprintVersion||_installView.Count==0){_installView=card["copies"].Long<=0?BlueprintPreview.Inspect(G,bid,scope,host,slot):BlueprintPreview.For(G,bid,scope,host,slot);_blueprintVersion=key;}
  var view=_installView;if(!view.Get("ok",false).Bool){CivicUI.Label(parent,view.Get("error","当前不能生成安装比较。").Text,"c-warning");if(card["copies"].Long<=0)CivicUI.Label(parent,"当前没有库存副本，领取后才能安装。","c-small c-muted");return;}
  var comparison=CivicUI.Section(parent,"安装前后变化",old==""?"空槽 → "+card["name"].Text:G.c.bonuses[prior["bonus_id"]]["name"].Text+" → "+card["name"].Text);var changes=0;
  foreach(var stat in view["stat_rows"])if(CivicDifference(comparison,Stat(stat["field"].Text),stat["before"].Double,stat["after"].Double,"",BlueprintAdverse(stat["field"].Text,0,1)))changes++;
  var step=view["same_step"];CivicUI.Label(comparison,"来源：当前城市分离快照 · 全城下一模拟步预测 · 仅显示变化；预览不推进当前城市。","c-small c-muted");
  foreach(var measure in new[]{new[]{"cashflow","经营净额"," /月"},new[]{"energy_supply","电力供给容量",""},new[]{"energy_need","用电需求",""}}){var pair=step[measure[0]];if(CivicDifference(comparison,measure[1],pair["before"].Double,pair["after"].Double,measure[2],measure[0]=="energy_need"))changes++;}
  foreach(var kind in step["services"].Keys){var pair=step["services"][kind];if(CivicDifference(comparison,Service(kind)+"实际人数",pair["before"].Double,pair["after"].Double," 人"))changes++;}
  foreach(var kind in step["service_capacity"].Keys){var pair=step["service_capacity"][kind];if(CivicDifference(comparison,Service(kind)+"供给容量",pair["before"].Double,pair["after"].Double))changes++;}
  var baseline=view["baseline"];var proposal=view["proposal"];double months=step["months"].Double;
  foreach(var resource in step["resource_outputs"].Keys){var a=baseline["audit"].Get("resources",V.Map()).Get(resource,V.Map());var b=proposal["audit"].Get("resources",V.Map()).Get(resource,V.Map());foreach(var measure in new[]{new[]{"produced","月产出"},new[]{"consumed","月消耗"},new[]{"lost","月溢出损失"},new[]{"after","步末库存"}}){var divisor=measure[0]=="after"?1:months;if(CivicDifference(comparison,Resource(resource)+measure[1],a.Get(measure[0],0).Double/divisor,b.Get(measure[0],0).Double/divisor,"",measure[0]=="consumed"||measure[0]=="lost"))changes++;}}
  foreach(var field in new[]{new[]{"heat_used","实际余热利用"},new[]{"pollution_recycled","实际污染回收"}})if(CivicDifference(comparison,field[1],baseline["metrics"].Get(field[0],0).Double,proposal["metrics"].Get(field[0],0).Double))changes++;
  if(changes==0)CivicUI.Label(comparison,"同一步汇总暂无可见数值变化；停运、施工与短缺等当前条件已计入。","c-muted");
  CivicUI.Label(comparison,card["copies"].Long<=0?"目录预览 · 需要领取1份副本后安装":"消耗1份「"+card["name"].Text+"」 · 库存 "+card["copies"].Long+" → "+view["after_copies"].Long,"c-warning");
  CivicUI.Label(comparison,old==""?"不额外消耗通货或材料；确认安装后生效。":"原方案停用，旧副本不返还；另消耗1份新副本。","c-small c-warning");
  var expected=card["copies"].Long;var focused=Runtime.Session.FocusedBlockId;CivicUI.Button(comparison,old==""?"安装副本":"替换安装",()=>{Action install=()=>V3InstallBonus(bid,scope,host,slot,old,expected,focused);if(old!="")Confirm("替换当前方案？","消耗1份「"+card["name"].Text+"」，原方案停用且旧副本不返还。",install);else install();},true,card["copies"].Long>0&&!_blueprintBusy&&Runtime.ViewCanEditFocused,"bonus-install-confirm");
  CivicSource(comparison,"完整比较、规则与实例来源",view["rule_trace"],view["same_step"],view["before_instance_records"],view["after_instance_records"]);
 }
 void CivicSlot(VisualElement body,VisualElement actions){
  var parts=_pageContext.Split('|');if(parts.Length<2||!S["projects"].Has(parts[0])||!S["projects"][parts[0]]["slots"].Has(parts[1])){CivicUI.Empty(body,"槽位已变化","返回建筑重新选择槽位。");return;}
  var host=parts[0];var slot=parts[1];var project=S["projects"][host];var iid=project["slots"][slot].Text;var focused=Runtime.Session.FocusedBlockId;
  var heading=CivicUI.Section(body,G.project_name(host)+" / "+G.slot_label(host,slot),"安装消耗1份真实副本；拆下不返还。重签只改变合同组织，保留原提供方。");
  var slots=CivicUI.Row(heading,"c-wrap");foreach(var value in G.slot_ids(host)){var other=value.Text;CivicUI.Button(slots,G.slot_label(host,other)+" · "+CivicSlotBonusName(host,other),()=>Navigate("slot",host+"|"+other),other==slot);}
  if(iid!=""){
   var instance=S["bonus_instances"][iid];var bid=instance["bonus_id"].Text;var current=CivicUI.Section(body,"已安装 · "+G.c.bonuses[bid]["name"].Text);CivicUI.Pair(current,"原提供方",G.c.operators[instance["provider_org"]]["name"].Text);CivicUI.Pair(current,"合同组织",G.c.operators[instance.Get("contract_org",instance["provider_org"])]["name"].Text);
   CivicUI.Button(current,"拆下当前方案 · 不返还副本",()=>Confirm("拆下当前方案？","旧实例停用，副本不退回；须先清空依赖它的弹性槽。",()=>Command(g=>{if(Runtime.Session.FocusedBlockId!=focused||g.s["projects"].Get(host,V.Map()).Get("slots",V.Map()).Get(slot,"").Text!=iid)return "槽位或街区已变化，请重新查看。";return g.clear_slot(host,slot);})),false,Runtime.ViewCanEditFocused);
   var cost=G.c.balance["recontract_cost"].Double;var owned=instance.Get("contract_org",instance["provider_org"]).Text==project["operator_org"].Text;
   CivicUI.Button(current,owned?"已经由本楼自营":"重签为本楼自营 · "+F(cost,0)+" 通货",()=>Command(g=>{if(Runtime.Session.FocusedBlockId!=focused||g.s["projects"].Get(host,V.Map()).Get("slots",V.Map()).Get(slot,"").Text!=iid)return "槽位或街区已变化，请重新查看。";return g.recontract(host,slot);}),false,!owned&&S["currency"].Double>=cost&&Runtime.ViewCanEditFocused);
   if(!owned&&S["currency"].Double<cost)CivicUI.Label(current,"当前通货不足。","c-warning");CivicSource(current,"已安装规则、来源与实例历史",instance,Rules.bonus_rules(G,bid,Math.Max(1,Bonus.active_count(G,bid,"building",host))));
  }else CivicUI.Label(heading,"当前为空槽。","c-good");
  var columns=CivicUI.Row(body);var inventory=CivicUI.Section(columns,"可用库存");inventory.AddToClassList("c-third");var detail=CivicUI.Column(columns,"c-grow");
  var ids=Rules.ids(S["block"]["bonus_wallet"]).Select(x=>x.Text).Where(bid=>S["block"]["bonus_wallet"][bid].Get("charges",0).Long>0&&Bonus.card(G,bid)["scope"].Text=="building").ToArray();
  if(ids.Length==0)CivicUI.Empty(inventory,"暂无未安装的建筑方案副本");
  foreach(var raw in ids){var bid=raw;var card=Bonus.card(G,bid);var error=G.bonus_eligible(bid,"building",host,slot,iid);var choice=CivicUI.Choice(inventory,card["name"].Text,"库存 "+card["copies"].Long+"\n"+BlueprintEffectSummary(card)+(error!=""?"\n"+error:""),_inventoryChoice==bid,()=>{_inventoryChoice=bid;_blueprintScope="building";_blueprintHost=host;_blueprintSlot=slot;_blueprintVersion="";RenderPage();},"bonus");choice.SetEnabled(error=="");}
  if(ids.Contains(_inventoryChoice)&&_blueprintHost==host&&_blueprintSlot==slot)CivicBonusPreview(detail,Bonus.card(G,_inventoryChoice),"building",host,slot);else CivicUI.Empty(detail,"选择一份方案进行比较","比较来自当前城市的同一步模拟，确认后才扣除库存。");
  CivicUI.Button(actions,"返回建筑",()=>Navigate("building",host));CivicUI.Button(actions,"查看全部 Bonus",()=>Navigate("bonus_home"));
 }
 void CivicSettlement(VisualElement body,VisualElement actions){
  var ending=S["block"].Get("ending",V.Map());var triggered=ending.Get("triggered",false).Bool;var pending=Bonus.pending(G);
  if(_settlementStep==""||!triggered&&(_settlementStep=="ending"||_settlementStep=="legacy"))_settlementStep=pending.Count>0?"rewards":"summary";
  var steps=CivicUI.Row(body);foreach(var entry in new[]{new[]{"summary","1 · 周期回顾"},new[]{"rewards","2 · 领取奖励"},new[]{"ending","3 · 街区定型"},new[]{"legacy","4 · 长期成果"}}){var id=entry[0];if(!triggered&&(id=="ending"||id=="legacy"))continue;CivicUI.Button(steps,entry[1],()=>{_settlementStep=id;RenderPage();},_settlementStep==id);}
  if(_settlementStep=="rewards"){
   var box=CivicUI.Section(body,"按保存顺序领取奖励",pending.Count+" 份待领取");
   if(pending.Count==0){CivicUI.Empty(box,"所有提案已处理","可前往 Bonus 使用已经领取的副本。");CivicUI.Button(box,"前往 Bonus",()=>Navigate("bonus_home"));}
   else {CivicUI.Label(box,BlueprintRewardSource(pending[0])+" · "+pending[0]["choices"].Count+" 选 1","c-muted");CivicUI.Button(box,"打开奖励选择",()=>OpenRewardModal(pending[0]["reward_id"].Text),true,!_blueprintBusy&&Runtime.ViewCanEditFocused);}
  }else if(_settlementStep=="summary"){
   var intro=CivicUI.Section(body,triggered?"高速发展预算已耗尽":"街区发展结算",triggered?"街区可继续常速经营；资格固定于 DT 首次实际归零。":"年度与两年大周期按保存顺序办理。");var readings=CivicUI.Row(intro);
   CivicUI.Value(readings,"当前来生日期",Clock.summary(G)["date"].Text);CivicUI.Value(readings,"街区人口",F(S["metrics"].Get("population",0).Double)+" 人");CivicUI.Value(readings,"已使用 DT",F(S["totals"]["dt_used"].Double,2));if(!triggered)CivicUI.Pair(intro,"下一年度 / 安居边界",Clock.duration(G,Cycles.next_boundary_seconds(G)));
   var summaries=S["block"].Get("cycles",V.Map()).Get("pending_summaries",V.Arr());if(summaries.Count==0)CivicUI.Empty(body,"暂无待回顾的周期结算");
   foreach(var summary in summaries){var box=CivicUI.Section(body,"第 "+summary["year"].Long+" 年"+(summary["major_cycle"].Bool?" / 两年大周期":""),"实际周期结算");CivicUI.Pair(box,"人口变化",Signed(summary["population_delta"].Double)+" 人");CivicUI.Pair(box,"财政变化",Signed(summary["currency_delta"].Double)+" 通货");CivicSource(box,"周期前后完整快照",summary);}
   CivicUI.Button(body,"查看当前事项与待决定合同",()=>Navigate("tasks_home","current"));
  }else if(_settlementStep=="ending"){
   var naming=CivicUI.Section(body,"为街区命名","命名权与地标资格来自首次 DT 实际归零的构筑。");
   if(S["block"].Get("naming_rights",false).Bool){var field=CivicUI.Field(naming,"街区名称",_civicBlockName,value=>_civicBlockName=value);field.name="settlement-block-name";CivicUI.Label(naming,"最多 "+G.c.v040["progression"].Get("name_max_length",24).Long+" 个字符。","c-small c-muted");CivicUI.Button(naming,"保存街区名称",()=>Command(g=>Block.name_block(g,_civicBlockName)),true,Runtime.ViewCanEditFocused);}
   else CivicUI.Label(naming,"街区名称已保存："+S["block"]["display_name"].Text,"c-heading c-good");
   var landmark=Block.capstone_definitions(G).Get(ending.Get("eligible_theme","").Text,V.Map());if(landmark.Count>0){var cap=CivicUI.Section(body,landmark.Get("name","主题定型建筑").Text,"主题已完成 · 具备正式挂牌资格");CivicUI.Icon(cap,"public");CivicUI.Button(cap,ending.Get("capstone_claimed",false).Bool?"已经正式挂牌":"正式挂牌 · 领取定型建筑",()=>Command(g=>Block.claim_capstone(g)),true,!ending.Get("capstone_claimed",false).Bool&&Runtime.ViewCanEditFocused);CivicSource(cap,"完整规则与来源",landmark,ending);}
   else CivicUI.Empty(body,"本次未达成主题奖励条件","继续经营可完成主题，此次地标资格不追补。");
   CivicUI.Button(body,"保存街区记录",()=>Command(g=>{var result=Block.archive(g);return result.Get("ok",false).Bool?"":result.Get("error","记录保存失败。").Text;}),false,Runtime.ViewCanEditFocused);
  }else CivicLegacySettlement(body);
  var mandatory=Runtime.PendingDecisions;CivicUI.Label(body,pending.Count>0?"还有 "+pending.Count+" 份待领奖励。":mandatory?"当前仍有待决定合同。":"结算可以结束；街区继续常速经营。","c-small c-muted");
  if(pending.Count>0&&_settlementStep!="rewards")CivicUI.Button(actions,"前往领取奖励",()=>{_settlementStep="rewards";RenderPage();},true);
  else if(mandatory)CivicUI.Button(actions,"处理当前事项",()=>Navigate("tasks_home","current"));
  CivicUI.Button(actions,triggered?"继续常速经营":"完成结算",()=>{Runtime.ResumeNormal();if(!Runtime.PendingDecisions)CloseArchive();},true,!mandatory);
 }
 void CivicLegacySettlement(VisualElement body){
  var view=Runtime.Session.BuildLegacyView();var chosen=view.Get("chosen",V.Map());var pending=view.Get("pending",V.Arr());var evidence=view.Get("evidence",V.Map());var intro=CivicUI.Section(body,"街区长期成果","首次 DT 实际归零时锁定构筑、蓝图与最后实际供给；正式挂牌后选择一项。");
  if(chosen.Count>0){CivicUI.Label(intro,"已选择："+chosen.Get("name",chosen.Get("profile_id","长期成果")).Text+" · "+LegacyGrade(chosen),"c-heading c-good");CivicUI.Label(intro,chosen.Get("tradeoff","").Text,"c-muted");CivicSource(intro,"已选成果与原地来源",chosen);}
  else if(pending.Count==0)CivicUI.Empty(intro,"当前没有锁定的合格成果","缺少本轮首次归零证据的历史记录不追补资格。");
  else foreach(var candidate in pending){var item=candidate;var id=item.Get("profile_id",item.Get("id","")).Text;var card=CivicUI.Section(intro,item.Get("name",id).Text,"锁定等级 · "+LegacyGrade(item));CivicUI.Label(card,item.Get("tradeoff","").Text,"c-muted");var claimed=S["block"].Get("ending",V.Map()).Get("capstone_claimed",false).Bool;CivicUI.Button(card,claimed?"选择这项长期成果":"先正式挂牌后选择",()=>WorldCommand(session=>session.ChooseBuildLegacy(id)),true,claimed&&Runtime.ViewCanEditFocused);CivicSource(card,"成果资格与构筑依据",item);}
  if(evidence.Count>0){var metrics=evidence.Get("metrics",V.Map());var source=CivicUI.Section(body,"锁定时最后实际供给");foreach(var key in new[]{"heat_used","pollution_recycled","medical","education"})if(metrics.Has(key))CivicUI.Pair(source,V.Map("heat_used","实际余热利用","pollution_recycled","实际污染回收","medical","医疗覆盖","education","教育覆盖")[key].Text,key=="medical"||key=="education"?F(metrics[key].Double*100,1)+"%":F(metrics[key].Double,3));CivicSource(source,"首次归零的完整部署、蓝图与供给证据",evidence);}
  CivicUI.Button(body,"查看已保存成果",()=>Navigate("content"));
 }
}
}
