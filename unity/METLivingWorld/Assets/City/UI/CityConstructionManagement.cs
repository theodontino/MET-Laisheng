using System.Linq;
using MET.Core;
namespace MET.City {
public sealed partial class CityInterface {
 string CrewRemaining(V crew)=>Clock.duration(G,crew["remaining_months"].Double*G.c.beta["units"]["earth_seconds_per_month"].Double);
 void ConfirmHireConstructionCrew(){
  Runtime.Flush();var quote=Construction.crew_quote(G);
  Confirm("雇用临时施工队？","支付 "+F(quote["cost"].Double,0)+" 通货，支付后余额 "+F(S["currency"].Double-quote["cost"].Double,0)+" 通货。增加 "+F(quote["nominal_cp_per_month"].Double,0)+" CP/来生月，持续 "+F(quote["duration_months"].Double,0)+" 来生月。\n实际能力受供电影响；仅投入本区队列。\n一次付费，不退费、不自动续费。空队列仍计时；暂停和离线不计时。",()=>Command(g=>Construction.hire_crew(g)));
 }
 string ConstructionProjectIdentity(V job){var p=S["projects"][job["id"]];var targetId=job["upgrade"].Bool?p.Get("upgrade_choice",p["def"]).Text:p["def"].Text;var target=G.c.projects.Get(targetId,V.Map());return job["name"].Text+" → T"+target.Get("tier",p.Get("tier",1)).Long+" · "+p.Get("tile","").Text;}
 string ConstructionProjectDirection(V job){var p=S["projects"][job["id"]];var targetId=job["upgrade"].Bool?p.Get("upgrade_choice",p["def"]).Text:p["def"].Text;return (job["upgrade"].Bool?"升级":"新建")+" · "+G.c.projects.Get(targetId,V.Map()).Get("name",targetId).Text+" · "+G.c.operators.Get(p.Get("operator_org","").Text,V.Map()).Get("name",p.Get("operator_org","")).Text+" · "+job["id"].Text;}
 string _operatingConstructionFilter="all";
 void ConstructionPage(){
  if(_advancedWorkspace){ConstructionAdvancedPage();return;}
  var queue=Construction.queue(G);var capacity=Construction.capacity_breakdown(G);var crewRows=Construction.crews(G);var quote=Construction.crew_quote(G);double inventory=G.inventory_total("material");var last=S["block"].Get("construction_last",V.Map());double elapsed=last.Get("hours",0).Double;double used=elapsed>0?last.Get("cp",0).Double/elapsed:0;double lastCapacity=elapsed>0?(last.Get("base_cp",0).Double-last.Get("city_reserved_cp",0).Double+last.Get("crew_cp",0).Double)/elapsed:0;
  OperatingStateNote();var overview=OperatingSection(_body,"施工能力",queue.Count(row=>!row["paused"].Bool)+" 项启用");
  Label(overview,"当前本区有效 "+F(capacity["local_effective"].Double)+" CP/月 · 城市工程预留 "+F(capacity["city_effective"].Double)+" CP/月",17,Blue);
  OperatingMeter(overview,"build","最近周期 · 本区能力使用",used,lastCapacity,"CP/月",elapsed>0?"同周期闲置 "+F(System.Math.Max(0,lastCapacity-used))+" CP/月":"实际使用待首次结算",Blue,elapsed>0);
  OperatingComposition(overview,new[]{"基础","方案","建筑","临时队"},new[]{capacity["base"].Double,capacity["bonus"].Double,capacity["buildings"].Double,capacity["crews_nominal"].Double});
  var constraints=OperatingGrid(overview,"Construction constraints");OperatingMeter(OperatingCell(constraints),"energy","供电满足",capacity["energy_ratio"].Double*100,100,"%","常设效率 ×"+F(capacity["multiplier"].Double,2),Blue,OperatingSettled);
  double materialNeed=queue.Sum(row=>row["material"].Double);if(materialNeed>0)OperatingMeter(OperatingCell(constraints),"material","队列材料备齐",System.Math.Min(inventory,materialNeed),materialNeed,"材料","库存 "+F(inventory)+(inventory<materialNeed?" · 缺 "+F(materialNeed-inventory):" · 可覆盖队列"),inventory<materialNeed?Gold:Green);else {var cell=OperatingCell(constraints);Label(cell,"材料库存　"+F(inventory),17,Ink);Label(cell,"当前无施工需求",13,Muted);}
  Label(overview,"色带为名义能力构成；常设能力乘方案效率后与临时队合并，受供电和城市工程预留影响。使用量取最近实际周期。",13,Muted);
  var projects=OperatingSection(_body,"施工队列",queue.Count+" 项");var tabs=OperatingGrid(projects,"Construction queue filters");foreach(var value in new[]{"all","active","paused"}){var filter=value;OperatingButton(tabs,filter=="all"?"全部":filter=="active"?"进行中":"已暂停",()=>{_operatingConstructionFilter=filter;RenderPage();},_operatingConstructionFilter==filter);}
  var shown=queue.Where(row=>_operatingConstructionFilter=="active"?!row["paused"].Bool:_operatingConstructionFilter=="paused"?row["paused"].Bool:true).ToArray();
  foreach(var job in shown){var id=job["id"].Text;bool paused=job["paused"].Bool;var card=OperatingCard(projects,"Project "+id);string status=paused?"已暂停":capacity["energy_ratio"].Double<=0?"等待电力":capacity["local_effective"].Double<=0?"等待施工能力":job["material"].Double>0&&inventory<=0?"等待材料":"共享能力与材料";var heading=Row(card,"Project heading",32*TextScale);Label(heading,ConstructionProjectIdentity(job),16,Ink,32*TextScale);Label(heading,status,13,paused?Gold:Muted,32*TextScale);
   Label(card,ConstructionProjectDirection(job),13,Muted);
   OperatingMeter(card,"build","完工进度",job["total"].Double-job["work"].Double,job["total"].Double,"CP","还需 "+F(job["work"].Double)+" CP / "+F(job["material"].Double)+" 材料",paused?Gold:Blue);
   var priorities=OperatingGrid(card,"Project controls");foreach(var value in new[]{1,0,-1}){var priority=value;OperatingButton(priorities,priority>0?"优先":priority<0?"延后":"普通",()=>Command(g=>Construction.set_priority(g,id,priority)),job["priority"].Long==priority);}
   OperatingButton(priorities,paused?"继续":"暂停",()=>Command(g=>g.set_paused(id,!paused)));OperatingButton(priorities,"定位",()=>{SelectBuilding(id);InspectMapFromPage();});OperatingButton(priorities,"检查",()=>Navigate("building",id));
  }
  if(shown.Length==0)Label(projects,queue.Count==0?"暂无施工项目。在地图选择地块新建或升级。":"筛选下暂无工程。",15,Muted);
  var crews=OperatingSection(_body,"临时工程队",crewRows.Count+" 队");foreach(var crew in crewRows)OperatingMeter(crews,"population",crew["id"].Text+" · +"+F(crew["nominal_cp_per_month"].Double)+" CP/月",crew["remaining_months"].Double,quote["duration_months"].Double,"月","剩余 "+CrewRemaining(crew)+" · 到期自动离岗",Gold);
  if(crewRows.Count==0)Label(crews,"暂无工程队。招募前可查看费用与持续时间。",14,Muted);OperatingButton(crews,"招募 · "+F(quote["cost"].Double,0)+" 通货",ConfirmHireConstructionCrew,true,!quote["affordable"].Bool);Label(crews,"一次付费，不自动续费；空队列仍计时，暂停和离线不计时。",13,Muted);
  OperatingButton(_body,"完整施工、来源与工程队详表 →",()=>OpenAdvancedWorkspace("construction"),true);WorkspaceActions("材料收支",()=>Navigate("resources","material"));
 }
 void ConstructionAdvancedPage(){
  var queue=Construction.queue(G);var capacity=Construction.capacity_breakdown(G);var crewRows=Construction.crews(G);var quote=Construction.crew_quote(G);var inventory=G.inventory_total("material");var summary=OverviewBrief(_body,"Construction shared summary");
  Label(summary,"本区施工 · "+F(capacity["local_effective"].Double)+" CP/月",23,Blue);
  Label(summary,"常设施工 "+F(capacity["base_total"].Double)+" ＋ 临时队 "+F(capacity["crews_nominal"].Double)+" CP/月",18,Ink);
  Label(summary,"常设来源：基础 "+F(capacity["base"].Double)+" ＋ 方案 "+F(capacity["bonus"].Double)+" ＋ 建筑 "+F(capacity["buildings"].Double)+"，效率 ×"+F(capacity["multiplier"].Double,2),16,Muted);
  Label(summary,(OperatingSettled?"供电 "+F(capacity["energy_ratio"].Double*100,0)+"%":"供电实际分配待结算")+" · 城市工程预留 "+F(capacity["city_effective"].Double)+" CP/月 · 材料库存 "+F(inventory),16,Muted);
  Label(summary,"队列剩余 "+F(queue.Sum(x=>x["work"].Double))+" CP / "+F(queue.Sum(x=>x["material"].Double))+" 材料 · "+queue.Count(x=>!x["paused"].Bool)+" 项施工 / "+queue.Count(x=>x["paused"].Bool)+" 项暂停",17,Gold);
  var last=S["block"].Get("construction_last",V.Map());if(last.Count>0){var elapsed=last.Get("hours",0).Double;var available=last.Get("base_cp",0).Double-last.Get("city_reserved_cp",0).Double+last.Get("crew_cp",0).Double;Label(summary,"最近周期闲置 "+F(System.Math.Max(0,available-last.Get("cp",0).Double))+" CP",16,Muted);Label(summary,"最近周期实际施工 "+F(last.Get("cp",0).Double)+" CP · 领料 "+F(last.Get("material",0).Double),16,Muted);Fold(summary,"实际施工凭据",c=>Raw(c,last));}
  var crews=CompactTaskCard(_body,"Temporary construction crews");Label(crews,"临时施工队 · "+crewRows.Count+" 队",21,Gold);
  if(crewRows.Count==0)Label(crews,"当前没有临时施工队。",17,Muted);
  foreach(var crew in crewRows)Label(crews,crew["id"].Text+"  ＋"+F(crew["nominal_cp_per_month"].Double,0)+" CP/月 · 剩余 "+CrewRemaining(crew),17,Ink);
  Label(crews,"临时队的名义能力不乘方案效率，受供电影响，仅用于本区施工。",16,Muted);
  Label(crews,"持续1来生月；一次付费，不退费、不自动续费。空队列仍计时；暂停、离线不计时。",16,Muted);
  Button(crews,"雇用一队 · "+F(quote["cost"].Double,0)+" 通货",ConfirmHireConstructionCrew,true,!quote["affordable"].Bool);
  if(!quote["affordable"].Bool)Label(crews,"余额不足当前报价。现有队伍到期后，后续雇用报价随活跃队数降低。",15,Gold);
  Label(_body,"施工队列 · "+queue.Count+" 项",22,Gold);if(queue.Count==0)Label(_body,"当前没有施工项目。点击地图上的空地选择建筑。",19,Muted);
  foreach(var job in queue){
   var id=job["id"].Text;var line=CompactTaskCard(_body,"Construction queue "+id);var paused=job["paused"].Bool;
   Label(line,ConstructionProjectIdentity(job)+(paused?" · 已暂停":""),21,Gold);
   Label(line,ConstructionProjectDirection(job),16,Muted);
   Meter(line,"已完成",job["total"].Double-job["work"].Double,job["total"].Double," CP",Blue);Metric(line,"尚需施工",F(job["work"].Double)+" CP");Metric(line,"尚需材料",F(job["material"].Double));
   var reason=paused?"工程已暂停，保留当前进度":capacity["energy_ratio"].Double<=0?"供电为零，等待恢复":capacity["local_effective"].Double<=0?"本区没有可用施工能力":job["material"].Double>0&&inventory<=0?"材料库存为零，等待补给":"按优先级共享施工能力与材料";
   Label(line,"当前限制 · "+reason,16,paused||capacity["local_effective"].Double<=0||job["material"].Double>0&&inventory<=0?Gold:Muted);
   if(job["material"].Double>inventory)Label(line,"完成本项还缺 "+F(job["material"].Double-inventory)+" 材料（库存由全城工程共享）",15,Gold);
   var priorities=Row(line,"Construction priority",46*TextScale);foreach(var priority in new[]{1,0,-1}){var value=priority;Button(priorities,(job["priority"].Long==value?"✓ ":"")+(value>0?"优先":value<0?"延后":"普通"),()=>Command(g=>Construction.set_priority(g,id,value)));}
   var actions=Row(line);Button(actions,paused?"恢复施工":"暂停施工",()=>Command(g=>g.set_paused(id,!paused)));Button(actions,"定位工程",()=>{SelectBuilding(id);InspectMapFromPage();});Button(actions,"建筑与来源",()=>Navigate("building",id));
  }
  WorkspaceActions("前往财政 · 材料收支 ›",()=>Navigate("resources","material"));
 }
}
}
