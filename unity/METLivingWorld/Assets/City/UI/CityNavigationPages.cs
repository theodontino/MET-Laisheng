using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
public sealed partial class CityInterface {
 void HomeSummaryRow(string caption,string value,string note,Color color){
  var row=Column(_body,"Home summary "+caption,2);
  row.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,4,7);
  var heading=Row(row,"Summary reading",30*TextScale);
  Label(heading,caption,15,Muted);var reading=Label(heading,value,20,color);reading.alignment=TextAnchor.MiddleRight;
  if(note!="")Label(row,note,14,Muted,28*TextScale);
 }
 RectTransform HomeDetailLinks(){
  var links=Column(_body,"Open detailed panels",7);
  links.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,10,0);
  Label(links,"详细面板",15,Muted);
  return links;
 }
 string PopulationFilterName(string filter)=>filter=="homeless"?"待安置":filter=="food"?"口粮":filter=="all"||filter==""?"全部住户":Service(filter);
 bool ResidentMatchesFilter(V group,string filter)=>filter=="homeless"?group.Get("host","").Text=="homeless":new[]{"food","medical","education","entertainment","commercial"}.Contains(filter)?group.Get("living",V.Map()).Get(filter,0).Double<.999:true;
 void PopulationHomePage(){
  var m=S["metrics"];var settled=S.Get("simulation_tick",0).Long>0;
  var housing=CompactTaskCard(_body,"Population housing summary");
  Meter(housing,"已安置 / 住房容量",m.Get("housed",0).Double,m.Get("housing",0).Double," 人",Green);
  var homeless=m.Get("homeless",0).Double;Metric(housing,"待安置",F(homeless,0)+" 人","",homeless>0?Gold:Green);
  if(homeless>0)Button(housing,"查看待安置住户 ›",()=>Navigate("population","homeless"));
  Metric(_body,"就业 / 劳动力",F(m.Get("employed",0).Double,0)+" / "+F(m.Get("workforce",0).Double,0)+" 人","",Blue);
  Metric(_body,"健康 / 幸福",F(m.Get("health",0).Double,1)+" / "+F(m.Get("happiness",0).Double,1),"",Green);
  var controls=CompactTaskCard(_body,"Population operating controls");Label(controls,"居民安排",20,Green);
  var residentActions=Row(controls,"Resident actions",46*TextScale);
  Button(residentActions,S.Get("migration_paused",false).Bool?"恢复迁入":"暂停迁入",()=>Command(g=>{g.toggle_migration();return "";}));
  Button(residentActions,S.Get("goods_active",false).Bool?"用品计划已启用":"公布用品计划",()=>Command(g=>g.enable_goods()),false,S.Get("goods_active",false).Bool);
  Button(residentActions,"就业地图",()=>{SetLayer("employment");InspectMapFromPage();});
  Label(_body,"口粮与服务 · 最近实际分配",20,Blue);
  if(!settled)Label(_body,"尚未产生实际分配；完成首次结算后显示满足率与缺口。",16,Muted);
  foreach(var kindValue in new[]{"food","medical","education","entertainment","commercial"}.OrderBy(k=>!settled?1:k=="food"?Progress.food_ratio(G):Progress.service_ratio(G,k))){
   var kind=kindValue;var ratio=kind=="food"?Progress.food_ratio(G):Progress.service_ratio(G,kind);
   var section=OverviewBrief(_body,"Population service "+kind);
   var affected=S["groups"].Values.Count(group=>ResidentMatchesFilter(group,kind));
   var serviceButton=Button(section,PopulationFilterName(kind)+(settled?" · 满足 "+F(ratio*100,1)+"% · "+affected+" 户有缺口":" · 待实际结算")+" ›",()=>Navigate("population",kind));
   serviceButton.GetComponentInChildren<Text>().color=settled&&ratio<.999?Gold:Ink;
  }
  if(settled){
   var shortages=S["groups"].Pairs.SelectMany(pair=>new[]{"food","medical","education","entertainment","commercial"}.Select(kind=>V.Map("group",pair.Key,"kind",kind,"ratio",pair.Value.Get("living",V.Map()).Get(kind,0).Double))).Where(row=>row["ratio"].Double<.999).OrderBy(row=>row["ratio"].Double).Take(3).ToArray();
   Label(_body,"优先查看的缺口",20,Gold);
   if(shortages.Length==0)Label(_body,"当前住户的口粮与上述服务均已满足。",16,Green);
   foreach(var row in shortages){var gid=row["group"].Text;var kind=row["kind"].Text;Button(_body,ResidentHostName(S["groups"][gid]["host"].Text)+" · "+PopulationFilterName(kind)+" "+F(row["ratio"].Double*100,1)+"% ›",()=>{_residentFilterChoices[kind]=gid;if(_residentFilter==kind)_residentChoice=gid;Navigate("population",kind);});}
  }
  WorkspaceActions("查看居民详表 ›",()=>Navigate("population","all"));
 }
 void EconomyHomePage(){
  var m=S["metrics"];var audit=S.Get("audit",V.Map());var months=audit.Get("hours",0).Double;
  var balance=CompactTaskCard(_body,"Economy operating balance");Metric(balance,"通货余额",F(S.Get("currency",0).Double)+" 通货","",Gold);
  Metric(balance,"经营收入",KnownRate(m.Get("income",0).Double));Metric(balance,"维护支出",KnownRate(-m.Get("upkeep",0).Double));Metric(balance,"经营净额",KnownRate(m.Get("cashflow",0).Double),"",m.Get("cashflow",0).Double<0?Gold:Green);
  Label(_body,"经营主要来源 · 月率",20,Blue);
  var principal=m.Get("fiscal_ledger",V.Arr()).GroupBy(row=>row.Get("kind","").Text+"|"+row.Get("name","来源").Text).Select(group=>V.Map("kind",group.First().Get("kind",""),"name",group.First().Get("name","来源"),"rate",group.Sum(row=>row.Get("rate",0).Double))).OrderByDescending(row=>row["rate"].Double).ToArray();
  if(principal.Length==0)Label(_body,"等待首次经营结算。",16,Muted);
  foreach(var source in principal.Take(3))ResourceBalanceRow(_body,source["name"].Text,(source["kind"].Text=="expense"?"− ":"＋ ")+F(source["rate"].Double)+" /月",source["kind"].Text=="expense"?Gold:Green);
  Label(_body,"资源库存与最近周期净变",20,Blue);
  foreach(var resourceValue in new[]{"food","material","goods"}){
   var resource=resourceValue;var receipt=audit.Get("resources",V.Map()).Get(resource,V.Map());var known=months>0&&receipt.Count>0;
   var net=known?(receipt.Get("after",0).Double-receipt.Get("before",0).Double)/months:0;
   Button(_body,Resource(resource)+" · 库存 "+F(G.inventory_total(resource))+" · "+(known?Signed(net)+" /月":"待实际结算")+" ›",()=>Navigate("resources",resource));
  }
  var transactions=CompactTaskCard(_body,"Economy latest transactions");Label(transactions,"最近主动交易",20,Gold);Label(transactions,"逐笔实际通货变化；与经营月率分别记账。",15,Muted);
  var recent=S.Get("civic032",V.Map()).Get("transactions",V.Arr()).Reverse().Take(4).ToArray();
  if(recent.Length==0)Label(transactions,"尚无主动交易记录。",16,Muted);
  foreach(var item in recent){ResourceBalanceRow(transactions,item.Get("label","交易").Text,Signed(item.Get("cash",0).Double)+" 通货",item.Get("cash",0).Double<0?Gold:Green);Label(transactions,F(item.Get("h",0).Double)+" 月 · 余额 "+F(item.Get("balance",0).Double),14,Muted);}
  WorkspaceActions("查看收支详表 ›",()=>Navigate("resources","currency"));
 }
 string _bonusHomeFilter="inventory";
 void OpenOwnedBonus(string id){_inventoryChoice=id;_blueprintScope=Bonus.card(G,id)["scope"].Text;_blueprintHost="";_blueprintSlot="";_installView=V.Map();_blueprintVersion="";Navigate("bonuses");}
 string OwnedBonusSourceName(string source){
  if(source.StartsWith("theme:",StringComparison.Ordinal)){var parts=source.Split(':');return "主题成长 · "+Themes._definition(G,parts[1]).Get("name",parts[1]).Text;}
  if(source.StartsWith("theme_event:",StringComparison.Ordinal)){var id=source.Substring(12);return "委任成果 · "+Themes._event_definition(G,id).Get("name",id).Text;}
  return BlueprintRewardSource(V.Map("source",source));
 }
 void BonusHomePage(){
  var wallet=S["block"].Get("bonus_wallet",V.Map());var instances=S.Get("bonus_instances",V.Map()).Values.Where(item=>item.Get("active",false).Bool).ToArray();
  var tabs=Row(_body,"Owned bonus filters",40*TextScale);
  foreach(var entry in new[]{new[]{"inventory","未安装库存"},new[]{"deployed","已部署"},new[]{"all","全部已拥有"}}){var id=entry[0];Button(tabs,entry[1],()=>{_bonusHomeFilter=id;RenderPage();},_bonusHomeFilter==id);}
  var ids=Rules.ids(wallet).Select(value=>value.Text).Union(instances.Select(item=>item["bonus_id"].Text)).Where(id=>_bonusHomeFilter=="inventory"?wallet.Get(id,V.Map()).Get("charges",0).Long>0:_bonusHomeFilter=="deployed"?instances.Any(item=>item["bonus_id"].Text==id):wallet.Get(id,V.Map()).Get("charges",0).Long>0||instances.Any(item=>item["bonus_id"].Text==id)).ToArray();
  if(ids.Length==0)Label(_body,_bonusHomeFilter=="deployed"?"当前没有已部署方案。选择库存中的方案，比较目标与代价后安装。":"当前没有未安装副本。年度、人口与主题成长会带来提案。",18,Muted);
  foreach(var idValue in ids){
   var id=idValue;var card=Bonus.card(G,id);var owned=wallet.Get(id,V.Map());var deployed=instances.Where(item=>item["bonus_id"].Text==id).ToArray();var box=CompactTaskCard(_body,"Owned bonus "+id);
   Label(box,card["name"].Text,22,Gold);Label(box,card["grade"].Text+" · "+ScopeName(card["scope"].Text)+" · 库存 "+card["copies"].Long+" · 已部署 "+deployed.Length,16,Muted);
   Label(box,BlueprintEffectSummary(card),17,Ink);var adverse=card["effects"].FirstOrDefault(effect=>BlueprintEffectAdverse(effect));if(adverse!=null)Label(box,"代价 · "+Stat(adverse.Get("label","效果").Text)+" "+FormatEffect(adverse,adverse.Get("before",0).Double)+" → "+FormatEffect(adverse,adverse.Get("after",0).Double),15,Gold);
   var target=card.Get("suggested_host",V.Map());Label(box,target.Count>0?"当前适用："+G.project_name(target.Get("id","").Text)+(target.Get("slot","").Text!=""?" / "+G.slot_label(target["id"].Text,target["slot"].Text):""):card["preview_reason"].Text,16,target.Count>0?Green:Muted);
   foreach(var instance in deployed){var at=instance;Button(box,"已部署于 "+G.project_name(at["host_id"].Text)+(at.Get("slot","").Text!=""?" / "+G.slot_label(at["host_id"].Text,at["slot"].Text):" / "+ScopeName(at["host_type"].Text))+" ›",()=>{if(at["host_type"].Text=="building"){SelectBuilding(at["host_id"].Text);Navigate("building",at["host_id"].Text);}else OpenOwnedBonus(id);});}
   var sources=owned.Get("grant_sources",V.Arr());Label(box,"许可来源："+(sources.Count>0?string.Join("、",sources.Take(2).Select(source=>OwnedBonusSourceName(source.Text))):owned.Get("acquired_from","已部署实例").Text),14,Muted);
   Button(box,card["copies"].Long>0?"选择目标并比较安装 ›":"查看方案与部署 ›",()=>OpenOwnedBonus(id),true);
   Fold(box,"完整效果与来源",c=>{EffectRows(c,card["effects"]);Label(c,card["stacking_note"].Text,15,Muted);Raw(c,owned);foreach(var item in deployed)Raw(c,item);});
  }
  var pending=Bonus.pending(G).Count;if(pending>0)WorkspaceActions("任务与决定 · 待领奖励 "+pending+" ›",()=>Navigate("tasks_home"));
 }
 void CommonThemeDelivery(Transform parent,V theme,V goal){
  var resource=goal["id"].Text.Substring(9);var remaining=Math.Max(0,goal["target"].Double-goal["value"].Double);var available=G.inventory_total(resource);
  var reserve=resource=="food"&&theme["id"].Text=="agricultural"?3*Themes._population_metrics(G).Get("food_demand",0).Double:0;
  var take=Math.Min(remaining,Math.Max(0,available-reserve));
  Label(parent,"交付扣除 "+F(take)+" "+Resource(resource)+" · 库存 "+F(available)+(reserve>0?" · 保留三月口粮 "+F(reserve):""),15,Gold);
  Label(parent,"全部阶段条件与稳定时长达成后，获得"+(theme["stage"].Long==1?"方案副本选择提案。":"蓝图研究提案。"),15,Green);
  Button(parent,"从库存交付 "+F(take)+" "+Resource(resource),()=>Command(g=>Themes.deliver(g,resource)),true,take<=.000001);
 }
 void TasksHomePage(){
  var theme=Themes.view(G);var chosen=theme.Get("id","").Text!="";var complete=theme.Get("completed",false).Bool;
  if(Bonus.pending(G).Count>0)WorkspaceActions("领取奖励 · "+Bonus.pending(G).Count,()=>Navigate("blueprints"));
  if(Runtime.MandatoryPending)WorkspaceActions("处理周期结算",()=>Navigate("settlement"));
  var current=CompactTaskCard(_body,"Current theme goals");Label(current,chosen?theme["name"].Text:"选择发展主题",23,Green);
  if(!chosen){Label(current,"选定方向后，按三个阶段推进城市成长。",16,Muted);Button(current,"比较主题并选择 ›",()=>Navigate("themes","current"),true);}
  else if(complete){Label(current,"三个阶段已经完成，成果与来源已保存。",17,Green);Button(current,"查看已完成成果 ›",()=>Navigate("themes","completed"));}
  else{
   Label(current,"M"+theme["stage"].Long+" / 3 · "+theme["stage_name"].Text,17,Muted);
   foreach(var goal in theme["goals"]){var factor=goal["unit"].Text=="%"?100:1;var block=OverviewBrief(current,"Current goal "+goal["id"].Text);Label(block,(goal["met"].Bool?"✓ ":"○ ")+goal["label"].Text,18,goal["met"].Bool?Green:Gold);Label(block,F(goal["value"].Double*factor,1)+(goal["comparison"].Text=="max"?" ≤ ":" / ")+F(goal["target"].Double*factor,1)+goal["unit"].Text,17,Ink);if(goal["kind"].Text=="delivery"&&!goal["met"].Bool&&goal["id"].Text.StartsWith("delivery_",StringComparison.Ordinal))CommonThemeDelivery(block,theme,goal);}
   if(theme["steady_required_seconds"].Double>0)Metric(current,"状态稳定时长",Clock.duration(G,theme["steady_seconds"].Double)+" / "+Clock.duration(G,theme["steady_required_seconds"].Double));
  }
  Label(_body,"当前合同与待决定事项",20,Blue);
  if(theme["events"].Count==0)Label(_body,"当前没有进行中合同或待决定委任。",16,Muted);
  foreach(var evt in theme["events"]){var id=evt["id"].Text;var offered=evt["status"].Text=="offered";var box=CompactTaskCard(_body,"Common task event "+id);Label(box,(offered?"待决定 · ":"进行中 · ")+evt["name"].Text,20,offered?Gold:Blue);Label(box,evt["description"].Text,16,Muted);
   if(evt.Get("deadline_seconds",-1).Double>=0)Metric(box,"剩余期限",Clock.duration(G,evt["deadline_seconds"].Double));
   if(!offered&&evt.Get("target",0).Double>0){var factor=evt.Get("unit","").Text=="%"?100:1;Meter(box,"实际进度",evt.Get("progress",0).Double*factor,evt["target"].Double*factor," "+evt.Get("unit","").Text,Blue);}
   if(!offered&&evt.Get("delivery_remaining",0).Double>0){var resource=evt["delivery_resource"].Text;var def=evt.Get("definition",Themes._event_definition(G,id));var reserve=resource=="food"?def.Get("reserve_months",0).Double*Themes._population_metrics(G).Get("food_demand",0).Double:0;var take=Math.Min(evt["delivery_remaining"].Double,Math.Max(0,G.inventory_total(resource)-reserve));Label(box,"本次扣除 "+F(take)+" "+Resource(resource)+" · 还需 "+F(evt["delivery_remaining"].Double)+(reserve>0?" · 保留口粮 "+F(reserve):""),15,Gold);Label(box,TaskChoiceOutcome(def,"accepted",evt["description"].Text),15,Green);Button(box,"交付 "+F(take)+" "+Resource(resource),()=>Command(g=>Themes.deliver(g,resource,-1,id)),true,take<=.000001);}
   Button(box,offered?"比较选项、代价与奖励 ›":"查看合同条件与来源 ›",()=>Navigate("themes","current"));
  }
  var pending=Bonus.pending(G);if(pending.Count>0){var rewards=CompactTaskCard(_body,"Task pending rewards");Label(rewards,"待领取提案 · "+pending.Count,20,Gold);foreach(var reward in pending.Take(3))Label(rewards,BlueprintRewardSource(reward)+" · "+(reward.Get("kind",reward.Get("requested_kind","copy")).Text=="research"?"蓝图研究":"方案副本选择"),16,Muted);Button(rewards,"比较并领取奖励 ›",()=>Navigate("blueprints"),true);}
  var links=HomeDetailLinks();Button(links,"成长路线与改向 ›",()=>Navigate("themes","growth"));Button(links,"已完成的任务与来源 ›",()=>Navigate("themes","completed"));Button(links,"周期回顾与街区结算 ›",()=>Navigate("settlement"));
 }
 void ArchiveHomePage(){
  var voices=_feedback.history().Count;var alerts=_feedbackView.Get("alerts",V.Arr()).Count;
  Label(_body,"城市留下的记录与成果",17,Blue);
  HomeSummaryRow("本城事件",S.Get("log",V.Arr()).Count+" 条记录","保留实际发生的时间与事件来源。",Ink);
  HomeSummaryRow("城市声音",voices+" 条消息",voices==0?"此视口尚无记录。":"来自本次视口记录的建设、贸易与趋势。",Ink);
  HomeSummaryRow("当前警报",alerts+" 项",alerts>0?"可在记录面板中定位当前问题。":"当前没有已确认的硬警报。",alerts>0?Gold:Green);
  HomeSummaryRow("同城发展",W.Get("districts",V.Map()).Count+" 个地区 · "+W.Get("blocks",V.Map()).Count+" 个街区","历史、成果和完整规则分开查看。",Ink);
  var links=HomeDetailLinks();
  Button(links,"构筑与长期成果 ›",()=>Navigate("content"));
  Button(links,"遗产与世界史 ›",()=>Navigate("heritage"));
  Button(links,"完整规则与来源 ›",()=>Navigate("sources"));
  WorkspaceActions("打开城市记录面板",()=>Navigate("records"));
 }
}
}
