using System;
using System.Linq;
using MET.Core;
using UnityEngine;
namespace MET.City {
public sealed partial class CityInterface {
 RectTransform BonusCard(Transform parent,string bid){var data=Bonus.card(G,bid);var card=Card(parent,data["name"].Text,data["grade"].Text+" · "+V.Map("building","建筑方案","organization","运营商方案","block","本街区方案").Get(data["scope"],data["scope"]).Text,Gold);Metric(card,"库存副本",data["copies"].Long+" 份");Label(card,data["preview_reason"].Text,16,Muted);EffectRows(card,data["effects"]);Fold(card,"完整规则与重复说明",c=>{Label(c,data["stacking_note"].Text,15,Muted,70);Raw(c,Rules.bonus_rules(G,bid,1,data["blueprint_level"].Long>2?2:(int)data["blueprint_level"].Long));});return card;}
 void EffectRows(Transform parent,V rows){foreach(var effect in rows)Metric(parent,effect.Get("label","效果").Text,F(effect.Get("before",0).Double,3)+" → "+F(effect.Get("after",0).Double,3)+effect.Get("unit","").Text);}
 void BonusesPage(){BlueprintInventoryPage();}
 void RewardCard(V reward,bool actionable){BlueprintRewardCard(reward,actionable);}
 void ThemesPage(){
  var view=Themes.view(G);var tab=_pageContext=="growth"?"growth":_pageContext=="completed"?"completed":"current";
  var tabs=Row(_body,"Tasks sections",42);foreach(var entry in new[]{new[]{"current","当前事项"},new[]{"growth","成长路线"},new[]{"completed","已完成"}}){var id=entry[0];Button(tabs,entry[1],()=>Navigate("themes",id),tab==id);}
  if(tab=="completed"){ThemeCompletedPage(view);return;}
  if(tab=="growth"){ThemeGrowthPage(view);return;}
  var columns=WorkspaceColumns("Current city matters",1.1f,1);var matters=columns[0];var commission=columns[1];
  Label(matters,"待决定与进行中的合同",21,Gold);
  if(view["events"].Count==0)Label(matters,"当前没有待决定或进行中的合同。",17,Muted);
  PlanningTaskEvents(matters,view);
  if(view.Get("id","").Text==""){ThemeSelection(commission,view);return;}
  ThemeCommission(commission,view);
  if(Bonus.pending(G).Count>0)WorkspaceActions("领取奖励 · "+Bonus.pending(G).Count,()=>Navigate("blueprints"));
  WorkspaceHint(view["name"].Text+" · "+(view["completed"].Bool?"主题已完成":"M"+view["stage"].Long+" / 3 · "+view["stage_name"].Text));
 }
 void ThemeSelection(Transform parent,V view){
  Label(parent,"选择街区的发展主题",23,Gold);Label(parent,"选择不消耗 DT。改向需要真实重铸权并支付代价。",16,Muted);
  foreach(var choice in view["choices"]){var id=choice["id"].Text;var box=Card(parent,choice["name"].Text,choice["description"].Text,Green);Illustration(box,ThemePicture(id),116);Button(box,"选择此主题",()=>Command(g=>Themes.select(g,id)),true);}
 }
 void ThemeCommission(Transform parent,V view){
  var completed=view["completed"].Bool;var card=Card(parent,view["name"].Text,completed?"主题已完成 · 阶段凭据见已完成":"M"+view["stage"].Long+" / 3 · "+view["stage_name"].Text,completed?Gold:Green);Illustration(card,ThemePicture(view["id"].Text),110);
  if(completed){Button(card,"查看已完成成果",()=>Navigate("themes","completed"));return;}
  foreach(var goal in view["goals"]){double factor=goal["unit"].Text=="%"?100:1;Metric(card,(goal["met"].Bool?"✓ ":"○ ")+goal["label"].Text,F(goal["value"].Double*factor,1)+(goal["comparison"].Text=="max"?" ≤ ":" / ")+F(goal["target"].Double*factor,1)+goal["unit"].Text,"",goal["met"].Bool?Green:Gold);
   if(goal["kind"].Text=="delivery"&&!goal["met"].Bool&&goal["id"].Text.StartsWith("delivery_",StringComparison.Ordinal)){var resource=goal["id"].Text.Substring(9);Button(card,"从库存交付 "+Resource(resource),()=>Command(g=>Themes.deliver(g,resource)),true);}
  }
  if(view["steady_required_seconds"].Double>0)Metric(card,"状态稳定时长",Clock.duration(G,view["steady_seconds"].Double)+" / "+Clock.duration(G,view["steady_required_seconds"].Double));
 }
 void ThemeGrowthPage(V view){
  var columns=WorkspaceColumns("Growth route",1.1f,1);var route=columns[0];var direction=columns[1];
  if(view.Get("id","").Text==""){ThemeSelection(route,view);Label(direction,"选向后按三阶段推进成长。",18,Muted);return;}
  ThemeMilestoneRoute(route,view);
  var progress=Card(direction,"发展方向 / "+view["name"].Text,"已有与待领成果在改向后保留。",Blue);Metric(progress,"重铸权",view["reforge_tokens"].Long+" 份");
  if(view["can_reforge"].Bool){Label(progress,"每次支付1份重铸权 + "+F(view["reforge_dt_cost"].Double,2)+" DT。",16,Gold);foreach(var candidate in view["choices"]){var id=candidate["id"].Text;if(id==view["id"].Text)continue;Button(progress,"改向「"+candidate["name"].Text+"」",()=>Confirm("确认重铸主题？","消耗1份重铸权与 "+F(view["reforge_dt_cost"].Double,2)+" DT，已有和待领奖励保留。",()=>Command(g=>Themes.reforge(g,id))));}}
  else Label(progress,"当前不满足重铸条件。",16,Muted);
  Fold(direction,"主题定义与完整阶段要求",c=>Raw(c,Themes._definition(G,view["id"].Text)));
  WorkspaceHint("成长路线保留三个阶段；当前交付与合同在当前事项处理。");
 }
 void ThemeCompletedPage(V view){
  var columns=WorkspaceColumns("Completed city matters",1,1);var history=columns[0];var evidence=columns[1];Label(history,"主题与合同记录",21,Green);
  var entries=view.Get("history",V.Arr()).Reverse().ToArray();if(entries.Length==0)Label(history,"尚无已保存的主题或合同记录。",17,Muted);
  foreach(var entry in entries){var box=Card(history,entry.Get("name",entry.Get("id","主题记录")).Text,entry.Get("status_label","城市记录").Text,Blue);if(entry.Get("reason","").Text!="")Label(box,entry["reason"].Text,15,Muted);Fold(box,"来源凭据",c=>Raw(c,entry));}
  Label(evidence,"成长成果存档",21,Gold);Label(evidence,view.Get("completed",false).Bool?"✓ 本轮主题已完成":"本轮主题尚未完成",18,view.Get("completed",false).Bool?Green:Muted);Fold(evidence,"已保存的阶段标识",c=>Raw(c,S["block"].Get("theme",V.Map()).Get("milestone_ids",V.Arr())));
  Fold(evidence,"历史城市成果记录",c=>{foreach(var row in Civic.task_rows(G)){Label(c,row["name"].Text+" · "+row["status"].Text,18);Label(c,row["reading"].Text,15,Muted);}Raw(c,S.Get("city_goal",V.Map()));});
  WorkspaceHint("只依据真实保存记录显示完成状态。");
 }

 void ThemeEvents(Transform parent,V view){PlanningTaskEvents(parent,view);}
 void LegacyThemeEvents(Transform parent,V view){foreach(var eventValue in view["events"]){var evt=eventValue;var id=evt["id"].Text;var def=evt.Get("definition",Themes._event_definition(G,id));var offered=evt["status"].Text=="offered";var box=Card(parent,evt["name"].Text,evt["description"].Text,offered?Gold:Blue);Label(box,offered?"等待决定":"合同进行中",18,Gold);var deadline=evt["deadline_seconds"].Double;if(deadline>=0)Metric(box,"真实期限",Clock.duration(G,deadline));else if(offered&&def.Get("choices",V.Arr()).Count==0&&def.Get("deadline_months",0).Double>0)Metric(box,"接受后期限",F(def["deadline_months"].Double)+" 来生月");if(!offered&&evt.Get("target",0).Double>0)Meter(box,"实际进度",evt["progress"].Double*(evt.Get("unit","").Text=="%"?100:1),evt["target"].Double*(evt.Get("unit","").Text=="%"?100:1)," "+evt.Get("unit","单位").Text,Blue);
   if(!offered){
   var cost=def.Get("currency",0).Double+def.Get("deposit",0).Double;
   if(cost>0)Metric(box,offered?"接受时支付":"接受时已支付",F(cost)+"通货");else Label(box,"无即时通货支付。",15,Muted);
   foreach(var resource in def.Get("resources",V.Map()).Keys)Metric(box,(offered?"接受时扣除 ":"接受时已扣 ")+Resource(resource),F(def["resources"][resource].Double));
   if(def.Get("deposit",0).Double>0)Label(box,"成功退还保证金 "+F(def["deposit"].Double)+"通货；逾期只失去机会与已付保证金。",15,Gold,55);
   if(def.Get("cash_reward",0).Double>0)Metric(box,"成功通货奖励",F(def["cash_reward"].Double));
   if(def.Get("reward_kind","").Text!="")Label(box,def["reward_kind"].Text=="research"?"成功后获得蓝图研究机会，按奖励队列领取。":"成功后获得方案副本选择，按奖励队列领取。",16,Green);
   if(def.Get("arrival_people",0).Double>0)Metric(box,"成功后真实迁入",F(def["arrival_people"].Double)+"人（带来真实需求）");
   if(def.Get("delivery_resource","").Text!="")Metric(box,"交付目标",Resource(def["delivery_resource"].Text)+" "+F(def.Get("delivery_amount",0).Double));   }
   var alternatives=offered&&evt["choices"].Count>2?Row(box,"Theme event alternatives",280*TextScale):(RectTransform)box;foreach(var choiceValue in evt["choices"]){var choice=choiceValue;var choiceId=choice["id"].Text;var option=offered?Card(alternatives,choice["label"].Text,choice.Get("description","").Text,choiceId=="decline"?Blue:Gold):box;var costText=choice.Get("cost_text","").Text;if(offered&&costText!="")Label(option,costText,17,Gold,105);Button(option,offered?"选择 · "+choice["label"].Text:choice["label"].Text,()=>Command(g=>Themes.choose_event(g,id,choiceId)),choiceId!="decline",!choice.Get("available",true).Bool);}if(offered)Label(box,"婉拒不支付代价，也不增加惩罚。",15,Muted);if(!offered&&evt["delivery_remaining"].Double>0){var resource=evt["delivery_resource"].Text;Button(box,"交付 "+Resource(resource)+" · 还需 "+F(evt["delivery_remaining"].Double),()=>Command(g=>Themes.deliver(g,resource,-1,id)),true);}Fold(box,"出现条件、成功结果与合同细则",c=>Raw(c,def));}}
 string _settlementStep="";
 void SettlementPage(){
  var ending=S["block"].Get("ending",V.Map());var triggered=ending.Get("triggered",false).Bool;var pending=Bonus.pending(G);
  if(_settlementStep==""||(!triggered&&(_settlementStep=="ending"||_settlementStep=="legacy")))_settlementStep=pending.Count>0?"rewards":"summary";
  var tabs=Row(_body,"Settlement steps",42);foreach(var entry in new[]{new[]{"summary","1 · 周期回顾"},new[]{"rewards","2 · 领取奖励"},new[]{"ending","3 · 街区定型"},new[]{"legacy","4 · 长期成果"}}){var id=entry[0];if(!triggered&&(id=="ending"||id=="legacy"))continue;Button(tabs,entry[1],()=>{_settlementStep=id;RenderPage();},_settlementStep==id);}
  if(_settlementStep=="rewards"){BlueprintOffersPage();return;}
  var columns=WorkspaceColumns("Settlement "+_settlementStep,1,1);var main=columns[0];var detail=columns[1];
  if(_settlementStep=="summary"){
   var intro=Card(main,triggered?"高速发展预算已耗尽":"街区发展结算",triggered?"街区可继续常速经营。资格固定在 DT 首次实际归零时。":"年度与两年大周期按保存顺序结算。",Gold);Metric(intro,"当前来生日期",Clock.summary(G)["date"].Text);Metric(intro,"街区人口",F(S["metrics"].Get("population",0).Double)+" 人");Metric(intro,"已使用 DT",F(S["totals"]["dt_used"].Double,2));if(!triggered)Metric(intro,"下一年度 / 安居边界",Clock.duration(G,Cycles.next_boundary_seconds(G)));
   var summaries=S["block"].Get("cycles",V.Map()).Get("pending_summaries",V.Arr());if(summaries.Count==0)Label(detail,"暂无待回顾的周期结算。",17,Muted);
   foreach(var summary in summaries){var box=Card(detail,"第 "+summary["year"].Long+" 年"+(summary["major_cycle"].Bool?" / 两年大周期":""),"实际周期结算",Blue);Metric(box,"人口变化",Signed(summary["population_delta"].Double)+" 人");Metric(box,"财政变化",Signed(summary["currency_delta"].Double)+" 通货");Fold(box,"周期前后完整快照",c=>Raw(c,summary));}
   Button(main,"查看当前事项与待决定合同",()=>Navigate("themes","current"));
  }else if(_settlementStep=="ending"){
   var intro=Card(main,"为街区命名","命名权与地标资格来自首次 DT 实际归零的构筑。",Gold);
   if(S["block"].Get("naming_rights",false).Bool){var name=InputBox(intro,"","为这个街区命名");name.characterLimit=(int)G.c.v040["progression"].Get("name_max_length",24).Long;Button(intro,"保存街区名称",()=>Command(g=>Block.name_block(g,name.text)),true);}else Label(intro,"街区名称已保存："+S["block"]["display_name"].Text,24,Gold);
   var eligible=ending.Get("eligible_theme","").Text;var landmark=Block.capstone_definitions(G).Get(eligible,V.Map());if(landmark.Count!=0){var cap=Card(detail,landmark.Get("name","主题定型建筑").Text,"主题已完成 · 具备正式挂牌资格",CityWorld.BuildingTypeColor("special"));Mark(cap,"public",CityWorld.BuildingTypeColor("special"),130);Button(cap,ending.Get("capstone_claimed",false).Bool?"已经正式挂牌":"正式挂牌 · 领取定型建筑",()=>Command(g=>Block.claim_capstone(g)),true,ending.Get("capstone_claimed",false).Bool);Fold(cap,"完整规则与来源",c=>Raw(c,landmark));}else Label(detail,"本次未达成主题奖励条件。继续经营可完成主题，但不追补此次地标资格。",17,Muted);
   Button(intro,"保存街区记录",()=>Command(g=>{var result=Block.archive(g);return result.Get("ok",false).Bool?"":result.Get("error","记录保存失败。").Text;}));
  }else{
   BuildLegacyCards(main,true);var next=Card(detail,"同城后续发展","原地标和部署留在原街区；同城成果按来源与真实投入采用。",Green);Button(next,"扩展同城 · 新街区",()=>Navigate("block_start"));Button(next,"查看下一地区任期",()=>Navigate("district_start"));Button(next,"查看地区治理与后续发展",()=>Navigate("governance"),true);
  }
  WorkspaceHint(pending.Count>0?"还有 "+pending.Count+" 份待领奖励；候选按原保存队列领取。":Runtime.PendingDecisions?"当前事项仍有待决定合同。":"结算可以结束；街区继续常速经营。");
  if(pending.Count>0)WorkspaceActions("前往领取奖励",()=>{_settlementStep="rewards";RenderPage();});
  else if(Runtime.PendingDecisions)WorkspaceActions("处理当前事项",()=>Navigate("themes","current"));
  WorkspaceActions(triggered?"继续常速经营":"完成结算",()=>{Runtime.ResumeNormal();if(!Runtime.PendingDecisions)CloseArchive();},Runtime.PendingDecisions);
 }

}
}
