using System;
using System.Linq;
using System.Text;
using MET.Core;
namespace MET.City {
public sealed partial class CityInterface {
 // Quick readings use the detached committed game. Full receipts remain in the ledger.
 public string TopbarResourceSummary(string resource){
  if(resource=="met")return "保存与读取游戏\n调整显示、输入和时间设置\n\n点击：系统菜单";
  var m=S.Get("metrics",V.Map());var block=S.Get("block",V.Map());var audit=S.Get("audit",V.Map());
  var text=new StringBuilder("当前街区："+block.Get("display_name","当前街区").Text+"\n\n");
  bool settled=S.Get("simulation_tick",0).Long>0;
  Func<string,string> reading=key=>m.Get(key).IsNumber?F(m[key].Double,1):"待结算";
  Func<string,string> rate=key=>settled&&m.Get(key).IsNumber?Signed(m[key].Double)+" /月":"待结算";
  if(resource=="dt"){
   var clock=Clock.summary(G);
   text.Append("剩余加速预算  "+F(clock["dt"].Double,1)+" DT\n当前速度  "+F(clock["acceleration"].Double,0)+"×\n");
   text.Append("加速时消耗预算，常速不消耗。\n"+(clock["dt"].Double<=0?"预算已用完，仍可常速经营。":"预算用完后，仍可常速经营。")+"\n\n点击：时间与生命周期");return text.ToString();
  }
  if(resource=="population"){
   var civic=S.Get("civic032",V.Map());
   text.Append("人口  "+reading("population")+" 人  ·  "+(settled&&civic.Get("last_population_rate").IsNumber?Signed(civic["last_population_rate"].Double)+" /月":"变化待结算")+"\n");
   text.Append("住房  "+reading("housed")+" / "+reading("housing")+" 人  ·  待安置 "+reading("homeless")+"\n就业  "+reading("employed")+" 人 / "+reading("jobs")+" 岗位\n健康  "+reading("health")+"  ·  幸福 "+reading("happiness")+"\n");
   if(m.Get("population",0).Double<=0)text.Append("暂无居民\n");
   else if(settled){var lowest=new[]{"medical","education","commercial","entertainment"}.Select(k=>new {Key=k,Ratio=Progress.service_ratio(G,k)}).OrderBy(x=>x.Ratio).First();text.Append(lowest.Ratio<1?"最缺服务："+Service(lowest.Key)+"，满足 "+F(lowest.Ratio*100,0)+"%\n":"各项服务均已满足\n");}
   else text.Append("服务情况待结算\n");
   text.Append("\n点击：居民与服务");return text.ToString();
  }
  if(resource=="currency"){
   text.Append("余额  "+F(S.Get("currency",0).Double,1)+" 通货\n经营收入  "+rate("income")+"\n维护支出  "+(settled&&m.Get("upkeep").IsNumber?F(m["upkeep"].Double,1)+" /月":"待结算")+"\n经营净额  "+rate("cashflow")+"\n");
   var transaction=S.Get("civic032",V.Map()).Get("transactions",V.Arr()).LastOrDefault();
   if(transaction!=null)text.Append("最近一笔（另计）  "+transaction.Get("label","交易").Text+" "+Signed(transaction.Get("cash",0).Double)+"\n");
   text.Append("\n点击：财政收支");return text.ToString();
  }
  if(resource=="cp"){
   var last=block.Get("construction_last",V.Map());var reserve=block.Get("city_cp_last",V.Map());var queue=Construction.queue(G);var capacity=Construction.capacity_breakdown(G);var crews=Construction.crews(G);double h=last.Get("hours",0).Double,rh=reserve.Get("hours",0).Double;
   text.Append("当前施工能力  "+F(capacity["effective"].Double,1)+" CP/月\n本区可用  "+F(capacity["local_effective"].Double,1)+" · 城市预留 "+F(capacity["city_effective"].Double,1)+" CP/月\n");
   text.Append("常设 "+F(capacity["base_total"].Double,1)+" ＋ 临时队 "+F(capacity["crews_nominal"].Double,1)+" CP/月（名义）\n供电满足 "+F(capacity["energy_ratio"].Double*100,0)+"%"+(settled?"":"（尚无实际结算）")+"\n");
   text.Append("临时施工队  "+crews.Count+" 队 · 不乘方案效率\n");
   foreach(var crew in crews.Take(3))text.Append(crew["id"].Text+"  剩余 "+CrewRemaining(crew)+"\n");
   if(crews.Count>3)text.Append("其余 "+(crews.Count-3)+" 队详见施工管理\n");
   text.Append("最近本区实际使用  "+(h>0?F(last.Get("cp",0).Double/h,1)+" CP/月":"待结算")+"\n最近城市工程预留  "+(rh>0?F(reserve.Get("cp",0).Double/rh,1)+" CP/月":"待结算")+"\n施工 "+queue.Count(x=>!x.Get("paused",false).Bool)+" 项  ·  暂停 "+queue.Count(x=>x.Get("paused",false).Bool)+" 项\n");
   text.Append("队列尚需 "+F(queue.Sum(x=>x["work"].Double),1)+" CP / "+F(queue.Sum(x=>x["material"].Double),1)+" 材料\n");
   var first=queue.FirstOrDefault(x=>!x.Get("paused",false).Bool);if(first!=null)text.Append("优先施工："+first["name"].Text+"\n"+S["projects"][first["id"]].Get("bottleneck","等待施工").Text+"\n");
   text.Append("\n点击：施工队列与临时施工队");return text.ToString();
  }
  if(resource=="energy"){
   if(!settled){text.Append("供电情况待结算\n\n点击：电力供需详表");return text.ToString();}
   var margin=m.Get("energy",0).Double-m.Get("energy_need",0).Double;
   text.Append((margin>=0?"供电充足  ·  余量 ":"供电不足  ·  缺口 ")+F(Math.Abs(margin),1)+"\n供给  "+reading("energy")+"  /  需求 "+reading("energy_need")+"\n用电满足  "+F(m.Get("energy_ratio",0).Double*100,0)+"%\n");
   foreach(var key in new[]{"energy_sources","energy_requests"}){var rows=m.Get(key,V.Map());if(rows.Count>0){var largest=rows.Pairs.OrderByDescending(x=>x.Value.Double).First();text.Append((key=="energy_sources"?"主要供电：":"主要建筑用电：")+(largest.Key=="municipal"?"市政基础":G.project_name(largest.Key))+" "+F(largest.Value.Double,1)+"\n");}}
   text.Append("\n点击：电力供需详表");return text.ToString();
  }
  text.Append("库存  "+F(G.inventory_total(resource),1)+" / "+F(G.stock_cap(resource),1)+"\n");
  var receipt=audit.Get("resources",V.Map()).Get(resource,V.Map());double elapsed=audit.Get("hours",0).Double;
  if(elapsed>0&&receipt.Count>0){
   text.Append("最近净变化  "+Signed((receipt.Get("after",0).Double-receipt.Get("before",0).Double)/elapsed)+" /月\n");
   text.Append("产出 "+F(receipt.Get("produced",0).Double/elapsed,1)+"  ·  消耗 "+F(receipt.Get("consumed",0).Double/elapsed,1)+" /月\n");
  }else text.Append("收支变化待结算\n");
  if(resource=="food")text.Append(settled?"口粮供应  "+F(Progress.food_ratio(G)*100,0)+"%"+(Progress.food_ratio(G)<1?" · 不足":" · 已满足")+"\n":"口粮供应待结算\n");
  if(resource=="material")text.Append("施工队列还需  "+F(Construction.queue(G).Sum(x=>x["material"].Double),1)+" 材料\n");
  if(resource=="goods"){double requested=S.Get("goods_requested_at",-1).Double;text.Append("生活用品计划  "+(S.Get("goods_active",false).Bool?"已生效":requested>=0?F(Math.Max(0,2-(S.Get("local_hours",0).Double-requested)),1)+" 月后生效":"未公布")+"\n");}
  if(elapsed>0&&receipt.Count>0)text.Append("月变化按最近结算折算\n");
  text.Append("\n点击："+Resource(resource)+"收支详表");return text.ToString();
 }
}
}
