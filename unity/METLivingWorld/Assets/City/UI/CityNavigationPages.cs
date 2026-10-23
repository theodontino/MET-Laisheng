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
 void EconomyHomePage(){
  var audit=S.Get("audit",V.Map());var months=audit.Get("hours",0).Double;
  Label(_body,"当前库存与最近周期收支",17,Blue);
  foreach(var resource in new[]{"food","material","goods"}){
   var receipt=audit.Get("resources",V.Map()).Get(resource,V.Map());
   var known=months>0&&receipt.Count>0;
   var net=known?(receipt.Get("after",0).Double-receipt.Get("before",0).Double)/months:0;
   HomeSummaryRow(Resource(resource),F(G.inventory_total(resource))+" 库存",known?"最近周期净变 "+Signed(net)+" /月":"等待首次实际收支结算",known&&net<0?Gold:Ink);
  }
  var cash=S.Get("metrics",V.Map()).Get("cashflow",0).Double;
  HomeSummaryRow("财政",F(S.Get("currency",0).Double)+" 通货","经营净额 "+KnownRate(cash),cash<0?Gold:Green);
  var links=HomeDetailLinks();
  Button(links,"市场与订单 ›",()=>Navigate("market"));
  Button(links,"政策与经营取舍 ›",()=>Navigate("policies"));
  Button(links,"区域物流 ›",()=>Navigate("network"));
  WorkspaceActions("打开资源收支面板",()=>Navigate("resources"));
 }
 void BonusHomePage(){
  var wallet=S["block"].Get("bonus_wallet",V.Map());
  var copies=wallet.Values.Sum(item=>item.Get("charges",0).Long);
  var stocked=wallet.Values.Count(item=>item.Get("charges",0).Long>0);
  var instances=S.Get("bonus_instances",V.Map()).Values.Where(item=>item.Get("active",false).Bool).ToArray();
  var pending=Bonus.pending(G).Count;
  Label(_body,"本街区的方案与部署许可",17,Gold);
  HomeSummaryRow("等待领取",pending+" 份提案",pending>0?"候选已保存，按奖励队列依次领取。":"年度、人口与主题成长会带来新提案。",pending>0?Gold:Muted);
  HomeSummaryRow("未安装库存",copies+" 份 · "+stocked+" 种方案","领取获得副本；安装后才生效。",Ink);
  HomeSummaryRow("当前部署",instances.Length+" 个活跃实例",instances.Length==0?"尚无活跃部署。":"分布在 "+instances.Select(item=>item.Get("host_type","").Text+"|"+item.Get("host_id","").Text).Distinct().Count()+" 个目标；完整来源保留在部署面板。",Green);
  Label(_body,"安装或替换前可比较目标、槽位、实际收益与代价。拆下不会退还副本。",15,Muted);
  WorkspaceActions("打开 Bonus 部署面板",()=>Navigate("bonuses"));
  if(pending>0)WorkspaceActions("领取奖励 · "+pending,()=>Navigate("blueprints"));
 }
 void TasksHomePage(){
  var theme=Themes.view(G);var chosen=theme.Get("id","").Text!="";var complete=theme.Get("completed",false).Bool;
  var goals=theme.Get("goals",V.Arr());var unmet=goals.FirstOrDefault(goal=>!goal.Get("met",false).Bool);
  var offered=theme.Get("events",V.Arr()).Count(item=>item.Get("status","").Text=="offered");
  var active=theme.Get("events",V.Arr()).Count(item=>item.Get("status","").Text!="offered");
  var pending=Bonus.pending(G).Count;
  Label(_body,"当前委任与待决定事项",17,Green);
  HomeSummaryRow("发展主题",chosen?theme.Get("name","当前主题").Text:"尚未选择主题",!chosen?"选择方向后开始本轮三阶段成长。":complete?"本轮主题已完成；成果以保存记录为准。":"M"+theme.Get("stage",1).Long+" / 3 · "+theme.Get("stage_name","").Text,complete?Green:Ink);
  var goal=unmet??goals.FirstOrDefault();
  if(chosen&&!complete&&goal!=null){
   var factor=goal.Get("unit","").Text=="%"?100:1;
   var reading=F(goal.Get("value",0).Double*factor,1)+(goal.Get("comparison","min").Text=="max"?" ≤ ":" / ")+F(goal.Get("target",0).Double*factor,1)+goal.Get("unit","").Text;
   HomeSummaryRow("当前目标",goal.Get("label","阶段目标").Text+" · "+reading,"本阶段已满足 "+goals.Count(item=>item.Get("met",false).Bool)+" / "+goals.Count+" 项；完整条件见任务面板。",unmet==null?Green:Gold);
  }else HomeSummaryRow("阶段目标",complete?"三个阶段已完成":"等待选定发展主题",complete?"成长路线与完成凭据可分别查看。":"主题选择、合同与交付在详细面板中处理。",complete?Green:Muted);
  HomeSummaryRow("待决定",offered+" 份委任 · "+pending+" 份奖励","另有 "+active+" 份合同进行中。",offered+pending>0?Gold:Ink);
  var links=HomeDetailLinks();
  Button(links,"查看成长路线 ›",()=>Navigate("themes","growth"));
  Button(links,"周期回顾与街区结算 ›",()=>Navigate("settlement"));
  WorkspaceActions(chosen?"打开任务与决定面板":"选择发展主题",()=>Navigate("themes","current"));
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
