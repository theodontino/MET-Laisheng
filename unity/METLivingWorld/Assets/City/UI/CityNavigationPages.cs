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
 string _operatingResidentFilter="all",_operatingEconomyResource="food";
 void PopulationHomePage(){OperatingPopulationCompactPage();}
 void EconomyHomePage(){
  var m=S["metrics"];bool settled=S.Get("simulation_tick",0).Long>0;var selected=_operatingEconomyResource;OperatingStateNote();
  var stocks=OperatingSection(_body,"库存与经营月流",settled?"最近实际 / 来生月":"月流待结算");
  var resources=new[]{"food","material","goods","currency"};for(int i=0;i<resources.Length;i+=2){var row=OperatingGrid(stocks,"Stock cards "+i);foreach(var resourceValue in resources.Skip(i).Take(2)){
   var resource=resourceValue;var card=OperatingCard(row,"Resource stock "+resource);var pick=card.gameObject.AddComponent<Button>();pick.onClick.AddListener(()=>{_operatingEconomyResource=resource;RenderPage();});if(selected==resource)card.GetComponent<Image>().color=new Color(.07f,.25f,.24f,.95f);var heading=Row(card,"Stock heading",30*TextScale);Height(heading,30*TextScale);OperatingGlyph(heading,resource,24);var title=Label(heading,Resource(resource),14,Muted,30*TextScale);var titleSize=title.GetComponent<LayoutElement>();titleSize.minWidth=0;titleSize.preferredWidth=0;titleSize.flexibleWidth=1;
   var amount=resource=="currency"?S.Get("currency",0).Double:G.inventory_total(resource);var amountLabel=Label(heading,F(amount),21,Ink,30*TextScale);var amountSize=amountLabel.GetComponent<LayoutElement>();amountSize.minWidth=0;amountSize.preferredWidth=0;amountSize.flexibleWidth=1.2f;amountLabel.alignment=TextAnchor.MiddleRight;double net=resource=="currency"?m.Get("cashflow",0).Double:m.Get("flows",V.Map()).Get(resource,0).Double;Label(card,settled?Signed(net)+" /月 · 点击追查来源 ›":"月流待结算 · 点击查看 ›",13,settled&&net<0?Gold:Muted,26*TextScale);
  }}
  OperatingMeter(stocks,"energy","电力占用",m.Get("energy_need",0).Double,m.Get("energy",0).Double,"容量",settled?"余缺 "+Signed(m.Get("energy",0).Double-m.Get("energy_need",0).Double)+" · 供需按全城能力分配":"实际供需待结算",Blue,settled);OperatingButton(stocks,(selected=="energy"?"✓ ":"")+"供给与用电来源 ›",()=>{_operatingEconomyResource="energy";RenderPage();},selected=="energy");
  var flow=OperatingSection(_body,selected=="energy"?"供给与用电来源":Resource(selected)+" · 月流贡献",selected=="energy"?"← 用电　供电 →":"← 消耗　产出 →");
  var contributions=V.Arr();
  if(settled&&selected=="currency")contributions=OperatingFiscalContributions(m);
  else if(settled&&selected=="energy"){
   foreach(var pair in m.Get("energy_sources",V.Map()).Pairs)contributions.Add(V.Map("label",pair.Key=="municipal"?"市政基础供电":G.project_name(pair.Key),"id",pair.Key,"value",pair.Value.Double));
   foreach(var pair in m.Get("energy_requests",V.Map()).Pairs)contributions.Add(V.Map("label",G.project_name(pair.Key)+" · 用电","id",pair.Key,"value",-pair.Value.Double));
   var peopleEnergy=m.Get("population_energy",V.Map()).Values.Sum(value=>value.Double);if(peopleEnergy>0)contributions.Add(V.Map("label","居民用电","id","","value",-peopleEnergy));
  }else if(settled){
   foreach(var pair in m.Get("activity_ledger",V.Map()).Pairs){var entries=pair.Value.Where(row=>row.Get("resource","").Text==selected);double rate=entries.Sum(row=>row.Get("kind","").Text=="output"?row.Get("kept",row.Get("rate",0)).Double:-row.Get("rate",0).Double);if(Math.Abs(rate)>.000001)contributions.Add(V.Map("label",G.project_name(pair.Key),"id",pair.Key,"value",rate));}
   var net=m.Get("flows",V.Map()).Get(selected,0).Double;var remainder=net-contributions.Sum(row=>row["value"].Double);if(Math.Abs(remainder)>.000001)contributions.Add(V.Map("label","贸易、交付、损耗与周期调整","id","","value",remainder));
  }
  var grouped=contributions.GroupBy(row=>row["id"].Text+"|"+row["label"].Text).Select(group=>V.Map("label",group.First()["label"],"id",group.First()["id"],"value",group.Sum(row=>row["value"].Double))).Where(row=>Math.Abs(row["value"].Double)>.000001).ToArray();double scale=grouped.Length>0?grouped.Max(row=>Math.Abs(row["value"].Double)):0;
  foreach(var row in grouped){var id=row["id"].Text;OperatingFlow(flow,row["label"].Text,row["value"].Double,scale,selected=="energy"?"容量":"/月",S["projects"].Has(id)?(Action)(()=>Navigate("building",id)):null);}
  if(grouped.Length==0)Label(flow,settled?"当前没有非零贡献。":"尚无实际月流凭据。",14,Muted);
  Label(_body,"条长使用统一刻度。库存为当前状态；月流和电力为最近实际结算。经营通货不含主动交易与一次性费用。",13,Muted);
  OperatingButton(_body,"完整收支来源与周期回执 →",()=>OpenAdvancedWorkspace("resources",selected),true);WorkspaceActions("市场订单",()=>Navigate("market",selected=="energy"||selected=="currency"?"":selected));WorkspaceActions("政策完整比较",()=>Navigate("policies"));
 }
 string _bonusHomeFilter="inventory";
 void OpenOwnedBonus(string id){_v3BonusFilter="all";_inventoryChoice=id;_blueprintScope=Bonus.card(G,id)["scope"].Text;_blueprintHost="";_blueprintSlot="";_installView=V.Map();_blueprintVersion="";Navigate("bonuses");}
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
 void TasksHomePage(){ThemesPage();}

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
