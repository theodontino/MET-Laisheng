using System;
namespace MET.Core {
/// <summary>Upgrades preserve project identity, operator track, inventory and history.</summary>
public static class Development {
 public static void ensure_project(Game g,V p) {
  if(p["special"].Bool)return;var d=g.c.projects[p["def"]];if(p.Has("family_id"))return;
  p["family_id"]=d["family_id"];p["node_id"]=p["def"];p["tier"]=d["tier"].Long;p["upgrade_choice"]="";p["upgrade_job"]=V.Map();p["construction_priority"]=0;
  p["slot_track"]=g.c.operators[p["operator_org"]]["slot_track"].Duplicate();p["unlocked_slots"]=0;p["slots"]=V.Map();p["slot_types"]=V.Map();if(p["status"].Text=="running")unlock(p);
 }
 public static void unlock(V p) {
  while(p["unlocked_slots"].Long<p["tier"].Long) {var index=(int)p["unlocked_slots"].Long;var type=p["slot_track"][index].Text;var key=type;var suffix=2;
   while(p["slots"].Has(key)){key=type+":"+suffix;suffix++;}p["slots"][key]="";p["slot_types"][key]=type;p["unlocked_slots"]=index+1;
  }
 }
 public static V choices(Game g,string id)=>!g.s["projects"].Has(id)?V.Arr():g.c.projects.Get(g.s["projects"][id]["def"],V.Map()).Get("upgrades",V.Arr()).Duplicate();
 public static V preview(Game g,string id,string node) {
  if(!choices(g,id).Contains(node))return V.Map("ok",false,"error","这不是该建筑的升级方向。");
  var p=g.s["projects"][id];var d=g.c.projects[node];var old=g.c.projects[p["def"]];var gains=V.Arr();var losses=V.Arr();var keys=old["stats"].Keys;
  foreach(var key in d["stats"].Keys)if(!keys.Contains(key))keys.Add(key);keys.Sort(StringComparer.Ordinal);
  foreach(var key in keys){var delta=d["stats"].Get(key,0).Double-old["stats"].Get(key,0).Double;if(Math.Abs(delta)<.001)continue;
   var row=V.Map("stat",key,"before",old["stats"].Get(key,0),"after",d["stats"].Get(key,0),"delta",delta);if(delta>0)gains.Add(row);else losses.Add(row);}
  return V.Map("ok",true,"node",node,"name",d["name"],"cost",d["cost"],"material",d["material"],"construction_required",d["construction_required"],"eta",d["construction_required"].Double/Math.Max(.01,Construction.capacity(g)),"gains",gains,"losses",losses,"operation_factor",g.c.v040["progression"]["upgrade_operation_factor"],"slot",p["slot_track"][(int)p["tier"].Long]);
 }
 public static string start_upgrade(Game g,string id,string node) {
  var view=preview(g,id,node);if(!view["ok"].Bool)return view["error"].Text;var p=g.s["projects"][id];
  if(g.s["phase"].Text!="play"||p["status"].Text!="running"||p["upgrade_job"].Count!=0)return "请等当前施工完成。";
  if(g.s["currency"].Double<view["cost"].Double)return "升级经费不足。";
  g.s["currency"]-=view["cost"].Double;g.s["totals"]["currency_spent"]+=view["cost"].Double;
  p["upgrade_choice"]=node;p["upgrade_job"]=V.Map("work",view["construction_required"],"total",view["construction_required"],"material",view["material"],"cp",true);
  g.log_event("upgrade_started",V.Map("project",id,"node",node),g.project_name(id)+"开始升级。");g.refresh_metrics();return "";
 }
 public static void complete(Game g,string id) {
  var p=g.s["projects"][id];
  if(p["upgrade_job"].Count!=0){var previous=p["def"];var d=g.c.projects[p["upgrade_choice"]];
   p["history"].Add(V.Map("kind","upgrade","from",previous,"to",d["id"],"h",g.s["local_hours"]));p["def"]=d["id"];p["node_id"]=d["id"];p["tier"]=d["tier"].Long;p["traits"]=d["traits"].Duplicate();p["upgrade_choice"]="";p["upgrade_job"]=V.Map();
   p["retained_storage"]=p["inventory"].Duplicate(true);g.s["sites"][p["site"]]["zone"]=d["zone"];g.s["cells"][p["tile"]]["zone"]=d["zone"];
   g.log_event("upgraded",V.Map("project",id,"node",d["id"]),d["name"].Text+"升级完成，解锁新方案槽。");
  }else {p["status"]="running";p["job"]=V.Map();p["commissioned_tick"]=g.s["simulation_tick"];g.s["totals"]["built"]+=1;g.log_event("built",V.Map("project",id),g.project_name(id)+"建成。");}
  unlock(p);
 }
}
}
