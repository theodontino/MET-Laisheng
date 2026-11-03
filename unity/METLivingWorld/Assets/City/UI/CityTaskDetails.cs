using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;
namespace MET.City {
public sealed partial class CityInterface {
 string ThemeStageReward(V stage)=>stage["reward_kind"].Text=="copy"?"方案副本选择提案（完成后领取）":"蓝图研究提案（完成后领取）";
 string ThemeGoalHint(string id){
  switch(id){
   case "goods_rate":return "提高工业建筑的真实制成品产出，并检查劳力、原料与供电。";
   case "energy":return "增加可用发电容量，检查停工与供需；目标看实际电力满足率。";
   case "industrial_labor":return "安置居民并提供可达的工业岗位；这里统计有效用工。";
   case "industrial_usage":return "检查工业岗位、原料与运行状态；按产能加权统计利用率。";
   case "housed":return "提供可入住住房，安顿现有居民并等待真实迁入。";
   case "homeless":return "补足住房与安置条件，让待安置人口降到目标以内。";
   case "food":return "补足可达的食物供给；库存充足仍需实际满足居民需求。";
   case "medical":return "补充可达、正在运行且有容量的医疗服务。";
   case "education":return "补充可达、正在运行且有容量的教育服务。";
   case "commercial":return "补充可达的购物服务，并检查商业容量与运行状态。";
   case "entertainment":return "补充可达的休闲服务，检查容量与运行状态。";
   case "health":return "改善食物与医疗等生活条件；这里按实际人口加权。";
   case "happiness":return "改善住房与生活服务；这里按实际人口加权。";
   case "commercial_people":return "让商业服务实际覆盖更多居民；仅建成空置容量不会增加人数。";
   case "commercial_usage":return "让商业容量获得真实需求与用工；按服务容量加权统计。";
   case "commercial_person_months":return "在同期状态条件达标时，按实际购物服务人数逐月累计；本阶段重新计数。";
   case "trade":return "在市场完成真实进口或出口成交；委托量不会直接计入，本阶段重新累计。";
   case "food_stock_months":return "增加库存或降低真实食物需求；口粮月数 = 现有食物 / 每月需求。";
   case "food_self_sufficiency":return "提高本地真实食物月产相对居民月需求的比例；进口不算本地产出。";
   case "currency":return "控制建设和进口支出，保持通货余额非负。";
   case "delivery_material":return "从现有库存主动交付材料；交付后扣除对应库存。";
   case "delivery_goods":return "从现有库存主动交付制成品；交付后扣除对应库存。";
   case "delivery_food":return "主动交付食物，同时保留三来生月的当前口粮。";
   default:return "按实际城市状态推进此目标。";
  }
 }
 void ThemeGoalAction(Transform parent,string id){
  Button(parent,"处理此目标 ›",()=>HandleGoalDestination(id));
 }
 void ThemeCurrentGoal(Transform parent,V theme,V goal){
  var box=CompactTaskCard(parent,"Current theme goal "+goal["id"].Text);var factor=goal["unit"].Text=="%"?100:1;var unit=goal["unit"].Text;var id=goal["id"].Text;
  Label(box,(goal["met"].Bool?"✓ ":"○ ")+goal["label"].Text,18,goal["met"].Bool?Green:Gold);
  Label(box,"当前 "+F(goal["value"].Double*factor,1)+unit+" · 目标 "+(goal["comparison"].Text=="max"?"≤ ":"≥ ")+F(goal["target"].Double*factor,1)+unit,16,Ink);
  Label(box,goal["met"].Bool?"此项目标已满足" :(goal["comparison"].Text=="max"?"仍需降低 ":"还差 ")+F(goal["missing"].Double*factor,1)+unit,15,goal["met"].Bool?Green:Gold);
  Label(box,ThemeGoalHint(id),15,Muted);
  if(goal["kind"].Text=="delivery"&&!goal["met"].Bool){var resource=id.Substring(9);var available=G.inventory_total(resource);var reserve=resource=="food"&&theme["id"].Text=="agricultural"?3*Themes._population_metrics(G).Get("food_demand",0).Double:0;var take=Math.Min(goal["missing"].Double,Math.Max(0,available-reserve));Label(box,"库存 "+F(available)+(reserve>0?" · 保留口粮 "+F(reserve):"")+" · 本次扣除 "+F(take),15,Gold);Button(box,"交付 "+F(take)+" "+Resource(resource),()=>Command(g=>Themes.deliver(g,resource)),true,take<=.000001);}
  else if(!goal["met"].Bool)ThemeGoalAction(box,id);
 }
 void UnifiedTaskContracts(Transform parent,V view){
  // Offered cards retain the illustrated choice comparison. Active contracts expose
  // objective, paid cost and outcome directly rather than behind a source fold.
  foreach(var evt in view["events"]){
   var id=evt["id"].Text;var def=evt["definition"];var offered=evt["status"].Text=="offered";
   var card=Card(parent,(offered?"待决定 · ":"进行中 · ")+evt["name"].Text,evt["description"].Text,offered?Gold:Blue);
   if(offered){foreach(var raw in evt["choices"]){var choice=raw;var cid=choice["id"].Text;var option=Card(card,choice["label"].Text,"",cid=="decline"?Blue:Gold);var selected=cid=="decline"?V.Map():def.Get("choices",V.Arr()).Count>0?Themes._event_choice_definition(def,cid):def;
     if(cid!="decline")Illustration(option,OptionPicture(id,cid,selected),78*TextScale);
     Label(option,TaskChoiceOutcome(selected,cid,evt["description"].Text).Replace(" 月"," 来生月"),16,Ink);
     Label(option,TaskChoiceCost(selected,cid),15,cid=="decline"?Muted:Gold);
     var reason=choice.Get("available",true).Bool?TaskChoiceAffordability(selected,cid):choice.Get("reason","当前不可选").Text;if(reason!="")Label(option,reason,15,Gold);
     var button=Button(option,cid=="decline"?"婉拒":choice["label"].Text,()=>Command(g=>Themes.choose_event(g,id,cid)),cid!="decline",!choice.Get("available",true).Bool||reason!="");button.name="ProposalChoice "+id+"|"+cid;
    }}else{
    if(evt.Get("deadline_seconds",-1).Double>=0)Metric(card,"剩余期限",F(evt["deadline_seconds"].Double/Themes.MONTH_SECONDS,2)+" 来生月");
    if(evt.Get("target",0).Double>0){var factor=evt.Get("unit","").Text=="%"?100:1;Metric(card,"实际进度 / 目标",F(evt["progress"].Double*factor,1)+" / "+F(evt["target"].Double*factor,1)+evt.Get("unit","").Text);}
    Label(card,"接受时的支出："+TaskChoiceCost(def,"accepted").Replace("即时支付 ",""),15,Gold);
    Label(card,TaskChoiceOutcome(def,"accepted",evt["description"].Text).Replace(" 月"," 来生月"),16,Green);
    if(def.Get("deposit",0).Double>0)Label(card,"完成退还保证金 "+F(def["deposit"].Double)+" 通货；逾期失去该机会与已付保证金。",15,Muted);
    if(evt.Get("delivery_remaining",0).Double>0){var resource=evt["delivery_resource"].Text;var reserve=resource=="food"?def.Get("reserve_months",0).Double*Themes._population_metrics(G).Get("food_demand",0).Double:0;var stock=G.inventory_total(resource);var take=Math.Min(evt["delivery_remaining"].Double,Math.Max(0,stock-reserve));Label(card,"库存 "+F(stock)+" · 还需 "+F(evt["delivery_remaining"].Double)+(reserve>0?" · 保留口粮 "+F(reserve):"")+" · 本次扣除 "+F(take),15,Gold);Button(card,"交付 "+F(take)+" "+Resource(resource),()=>Command(g=>Themes.deliver(g,resource,-1,id)),true,take<=.000001);}
    else if(def.Has("trade_amount"))Button(card,"前往市场完成实际成交",()=>HandleGoalDestination("trade"));
    else if(def.Has("metric")||def.Has("person_months"))Button(card,"查看人口与实际服务",()=>HandleGoalDestination(def.Get("metric","commercial_person_months").Text));
   }
   Fold(card,"完整合同与来源",c=>{Raw(c,evt);Raw(c,def);});
  }
 }
}
}
