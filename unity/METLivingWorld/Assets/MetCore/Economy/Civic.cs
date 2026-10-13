using System;
using System.Globalization;
namespace MET.Core {
/// <summary>Bounded actual civic and action history; no retrospective resource generation.</summary>
public static class Civic {
 public static readonly V OUTCOMES=V.Map("food",V.Map("name","稳住粮食供给","desc","居民口粮满足99%，粮食不再净减少，并经营至少0.5来生月。","money",35.0,"dt",0.0),"housing",V.Map("name","安顿新邻居","desc","安置至少24位居民，待安置人口少于0.1。","money",35.0,"dt",0.0),"services",V.Map("name","接通生活服务","desc","医疗和购物实际覆盖分别达到65%。","money",40.0,"dt",0.0),"manufacturing",V.Map("name","第一批生活用品","desc","让真实制成品库存达到6。","money",30.0,"dt",0.0));
 public static readonly V ROUTES=V.Arr("community","delivery","renewal");
 public const int HISTORY_LIMIT=720,ACTION_LIMIT=300;
 static string F(double x,int n)=>x.ToString("F"+n,CultureInfo.InvariantCulture);
 static bool finite(V x)=>x.IsNumber&&!double.IsNaN(x.Double)&&!double.IsInfinity(x.Double);
 public static void ensure(Game g) {
  if(g.s.Has("civic032"))return;var records=V.Map();foreach(var id in OUTCOMES.Keys)records[id]=V.Map("status",g.s.Get("city_goal",V.Map()).Get("awards",V.Arr()).Contains(id)?"completed":"active","accepted_h",null,"finished_h",null);
  g.s["civic032"]=V.Map("version",1,"started_h",g.s.Get("local_hours",0).Double,"history",V.Arr(),"transactions",V.Arr(),"task_history",V.Arr(),"tasks",records,"route_active",!g.s.Get("city_goal",V.Map()).Get("completed",false).Bool,"cash_anchor",g.s["currency"].Double,"dt_anchor",g.s["dt"].Double,"last_population_rate",null,"last_birth_rate",null,"last_arrival_rate",null,"last_cycle_h",0.0,"history_trimmed",false,"chapter_recorded",g.s.Get("city_goal",V.Map()).Get("completed",false).Bool);
  if(!g.s["settings"].Has("user_paused"))g.s["settings"]["user_paused"]=false;
 }
 public static bool permitted(Game g,string id){ensure(g);return g.s["civic032"]["tasks"].Get(id,V.Map()).Get("status","active").Text=="active";}
 public static bool route_permitted(Game g){ensure(g);return g.s["civic032"]["route_active"].Bool;}
 public static void route_selected(Game g,string previous){ensure(g);var now=g.s["city_goal"]["route"].Text;if(previous!=now&&g.s["civic032"]["route_active"].Bool)_task_event(g,"route:"+previous,"switched","改选城市成果");g.s["civic032"]["route_active"]=true;if(previous!=now)_task_event(g,"route:"+now,"accepted","选择城市成果");}
 public static void @event(Game g,string kind,V payload,string text) {
  if(g.preview_mode||g.recording_cycle||!g.s.Has("civic032"))return;var j=g.s["civic032"];var delta=g.s["currency"].Double-j["cash_anchor"].Double;var dt_delta=g.s["dt"].Double-j["dt_anchor"].Double;
  if(Math.Abs(delta)>.000001||Math.Abs(dt_delta)>.000001){j["transactions"].Add(V.Map("h",g.s["local_hours"].Double,"tick",g.s["simulation_tick"].Long,"kind",kind,"label",text!=""?text:kind,"cash",delta,"dt",dt_delta,"balance",g.s["currency"].Double));while(j["transactions"].Count>ACTION_LIMIT)j["transactions"].Items.RemoveAt(0);}
  j["cash_anchor"]=g.s["currency"].Double;j["dt_anchor"]=g.s["dt"].Double;
 }
 public static void cycle(Game g,double h,double previous_population,double arrivals) {
  if(g.preview_mode)return;ensure(g);var j=g.s["civic032"];var m=g.s["metrics"];var count=m.Get("population",0).Double;var change=count-previous_population;
  j["last_population_rate"]=change/Math.Max(1e-15,h);j["last_arrival_rate"]=arrivals/Math.Max(1e-15,h);j["last_birth_rate"]=(change-arrivals)/Math.Max(1e-15,h);j["last_cycle_h"]=h;
  var row=V.Map("h",g.s["local_hours"].Double,"tick",g.s["simulation_tick"].Long,"span",h,"balance",g.s["currency"].Double,"dt",g.s["dt"].Double,"income",m.Get("income",0).Double,"upkeep",m.Get("upkeep",0).Double,"net",m.Get("cashflow",0).Double,"population",count,"population_rate",j["last_population_rate"],"known",true);
  j["history"].Add(row);while(j["history"].Count>HISTORY_LIMIT){j["history"].Items.RemoveAt(0);j["history_trimmed"]=true;}j["cash_anchor"]=g.s["currency"].Double;j["dt_anchor"]=g.s["dt"].Double;
 }
 public static void finalize(Game g) {
  if(g.preview_mode||!g.s.Has("civic032"))return;var j=g.s["civic032"];if(j["history"].Count!=0&&j["history"][-1]["tick"].Long==g.s["simulation_tick"].Long){j["history"][-1]["balance"]=g.s["currency"].Double;j["history"][-1]["dt"]=g.s["dt"].Double;}
  j["cash_anchor"]=g.s["currency"].Double;j["dt_anchor"]=g.s["dt"].Double;
 }
 public static void sync(Game g) {
  if(g.preview_mode)return;ensure(g);var j=g.s["civic032"];foreach(var id in g.s.Get("city_goal",V.Map()).Get("awards",V.Arr())){if(!j["tasks"].Has(id))continue;if(j["tasks"][id]["status"].Text!="completed"){j["tasks"][id]["status"]="completed";j["tasks"][id]["finished_h"]=g.s["local_hours"].Double;_task_event(g,id.Text,"completed","成果已兑现；奖励不会重复领取");}}
  if(g.s.Get("city_goal",V.Map()).Get("completed",false).Bool&&!j.Get("chapter_recorded",false).Bool){j["chapter_recorded"]=true;_task_event(g,"route:"+g.s["city_goal"]["route"].Text,"completed","河谷新章完成");}
 }
 public static string task_action(Game g,string id,string action) {
  if(g.s["phase"].Text!="play")return "当前城市不能调整任务。";ensure(g);var j=g.s["civic032"];if(action!="accept"&&action!="abandon")return "未知任务操作。";
  if(id.StartsWith("route:",StringComparison.Ordinal)){var route=id.Substring(6);if(!ROUTES.Contains(route))return "未知城市成果。";if(g.s["city_goal"].Get("completed",false).Bool)return "这一章已经完成，不能重新领奖。";
   if(action=="abandon"){if(g.s["city_goal"]["route"].Text!=route||!j["route_active"].Bool)return "这个成果并非当前任务。";j["route_active"]=false;_task_event(g,id,"abandoned","停止追求此成果；城市与已有进度保留");}
   else {if(j["route_active"].Bool&&g.s["city_goal"]["route"].Text==route)return "";var previous=g.s["city_goal"]["route"].Text;var err=Progress.select(g,route);if(err!="")return err;if(previous==route)_task_event(g,id,"accepted","重新接取；不追发已领取奖励");}
  }else {if(!OUTCOMES.Has(id))return "未知城市任务。";var record=j["tasks"][id];if(record["status"].Text=="completed"||g.s["city_goal"]["awards"].Contains(id))return "这个成果已经兑现，不可重复领取。";
   if(action=="abandon"){if(record["status"].Text!="active")return "此任务不在进行中。";record["status"]="abandoned";record["finished_h"]=g.s["local_hours"].Double;_task_event(g,id,"abandoned","停止任务与自动奖励；不撤走已有设施");}
   else {if(record["status"].Text=="active")return "";record["status"]="active";record["accepted_h"]=g.s["local_hours"].Double;record["finished_h"]=V.Null;_task_event(g,id,"accepted","重新接取");}}
  g.refresh_metrics();g.log_event("task_"+action,V.Map("id",id),(action=="accept"?"接取":"放弃")+task_name(g,id));return "";
 }
 public static string task_name(Game g,string id)=>id.StartsWith("route:",StringComparison.Ordinal)?g.c.data["city_goals"].Get(id.Substring(6),V.Map()).Get("name",id).Text:OUTCOMES.Get(id,V.Map()).Get("name",id).Text;
 public static void _task_event(Game g,string id,string status,string note){var j=g.s["civic032"];j["task_history"].Add(V.Map("id",id,"name",task_name(g,id),"status",status,"h",g.s["local_hours"].Double,"tick",g.s["simulation_tick"].Long,"note",note));while(j["task_history"].Count>ACTION_LIMIT)j["task_history"].Items.RemoveAt(0);}
 public static V task_rows(Game g) {
  var j=g.s.Get("civic032",V.Map());var goal=g.s.Get("city_goal",V.Map());var rows=V.Arr();if(goal.Count==0)return rows;
  foreach(var id in OUTCOMES.Keys){var def=OUTCOMES[id];var state=j.Get("tasks",V.Map()).Get(id,V.Map()).Get("status","active").Text;if(goal.Get("awards",V.Arr()).Contains(id))state="completed";var progress=0.0;var reading="";var m=g.s["metrics"];
   switch(id){case "food":{var measured=m.Has("flows")&&g.s.Get("simulation_tick",0).Long>0;var flow=m.Get("flows",V.Map()).Get("food",0).Double;
    progress=measured&&flow>=-.01?Math.Min(1,Math.Min(Progress.food_ratio(g)/.99,g.s["local_hours"].Double/.5)):0.0;
    reading=measured?"实际口粮 "+F(100*Progress.food_ratio(g),0)+"% · 净变 "+flow.ToString("+0.0;-0.0;+0.0",CultureInfo.InvariantCulture)+"/h · 运行 "+F(g.s["local_hours"].Double,1)+"/0.5h":"待首次实际结算 · 运行至少0.5h后判定";break;}
    case "housing":progress=Math.Min(1,m.Get("housed",0).Double/24.0);reading="已安置 "+F(m.Get("housed",0).Double,1)+" / 24人 · 待安置 "+F(m.Get("homeless",0).Double,1);break;
    case "services":{var med=Progress.service_ratio(g,"medical");var shopping=Progress.service_ratio(g,"commercial");progress=Math.Min(1,Math.Min(med,shopping)/.65);reading="医疗 "+F(100*med,0)+"% · 购物 "+F(100*shopping,0)+"% / 目标65%";break;}
    case "manufacturing":progress=Math.Min(1,g.inventory_total("goods")/6.0);reading="制成品 "+F(g.inventory_total("goods"),1)+" / 6";break;}
   rows.Add(V.Map("id",id,"name",def["name"],"description",def["desc"],"status",state,"progress",progress,"reading",reading,"reward","历史城市目标（本版奖励由年度与委任提供）","chapter",false));}
  foreach(var routeValue in ROUTES){var route=routeValue.Text;var active=goal.Get("route","community").Text==route&&j.Get("route_active",true).Bool;var status=active?"active":"available";
   if(goal.Get("completed",false).Bool)status=goal["route"].Text==route?"completed":"closed";var progress=route!="delivery"?Math.Min(1,goal.Get(route,0).Double/Math.Max(.1,g.c.balance["goal_duration"].Double)):goal.Get("delivered",false).Bool?1.0:0.0;
   var desc=g.c.data["city_goals"][route].Get("description","").Text;if(desc=="")desc=route=="community"?"健康、幸福达到72；医疗、购物、休闲分别覆盖75%，保持1.2小时。":route=="delivery"?"交付20材料与12制成品，保留至少2小时口粮。":"改善原住区最好的两种服务且保持健康，持续1.2小时。";
   rows.Add(V.Map("id","route:"+route,"name",g.c.data["city_goals"][route]["name"],"description",desc,"status",status,"progress",progress,"reading","共用底线：安置30人、生活稳定1小时；"+(progress>=1&&route=="delivery"?"已交付":route=="delivery"?"等待物资交付":"保持 "+F(goal.Get(route,0).Double,1)+" / "+F(g.c.balance["goal_duration"].Double,1)+"h"),"reward","本章一次：120通货 · 6DT · 1次地区方案机会","chapter",true));}
  return rows;
 }
 public static string validate(V value) {
  if(!value.Has("civic032"))return "";var j=value["civic032"];var settings=value.Get("settings",V.Map());if(settings.IsMap&&settings.Has("user_paused")&&!settings["user_paused"].IsBool)return "暂停状态损坏。";if(!j.IsMap)return "城市记录格式损坏。";
  foreach(var key in new[]{"history","transactions","task_history"}){if(!j.Get(key).IsArray)return "城市记录缺少列表："+key;if(j[key].Count>2000)return "城市记录长度异常。";foreach(var row in j[key]){if(!row.IsMap)return "城市记录条目损坏。";if(!finite(row.Get("h")))return "城市记录时间无效。";}}
  if(!j.Get("tasks").IsMap)return "任务状态损坏。";foreach(var required in OUTCOMES.Keys)if(!j["tasks"].Has(required))return "缺少任务状态："+required;
  if(!j.Get("route_active").IsBool)return "当前成果状态损坏。";foreach(var key in new[]{"last_population_rate","last_birth_rate","last_arrival_rate"})if(!j.Get(key).IsNull&&!finite(j[key]))return "人口变化记录损坏。";
  foreach(var id in j["tasks"].Keys){var record=j["tasks"][id];if(!record.IsMap||!V.Arr("active","abandoned","completed").Contains(record.Get("status","")))return "任务状态无效。";}
  foreach(var key in new[]{"cash_anchor","dt_anchor","started_h"})if(!finite(j.Get(key)))return "城市记录数值无效。";
  foreach(var row in j["history"])foreach(var key in new[]{"tick","span","balance","dt","income","upkeep","net","population","population_rate"})if(!finite(row.Get(key)))return "历史账本数值无效。";
  foreach(var row in j["transactions"]){foreach(var key in new[]{"tick","cash","dt","balance"})if(!finite(row.Get(key)))return "收支记录数值无效。";if(!row.Get("label").IsString||!row.Get("kind").IsString)return "收支记录来源无效。";}
  foreach(var row in j["task_history"]){if(!row.Get("id").IsString||!row.Get("name").IsString||!row.Get("note").IsString)return "任务历史损坏。";if(!V.Arr("accepted","abandoned","completed","switched").Contains(row.Get("status","")))return "任务历史状态无效。";}
  return "";
 }
}
}
