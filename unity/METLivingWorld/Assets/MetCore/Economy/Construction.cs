using System;
namespace MET.Core {
/// <summary>Stable priority queue jointly settles construction capacity and actual material.</summary>
public static class Construction {
 static double base_capacity(Game g) {
  var n=g.s["block"].Get("construction_capacity",g.c.v040["progression"]["construction_capacity"]).Double;
  n+=g.compiled.Get("options",V.Map()).Get("construction_capacity",0).Double;
  foreach(var id in g.playable_ids()){var p=g.s["projects"][id];if(p["status"].Text=="running"&&!p["paused"].Bool)n+=g.compiled["stats"].Get(id,V.Map()).Get("construction_capacity",0).Double*p["usage"].Double;}
  return Math.Max(0,n*g.compiled.Get("options",V.Map()).Get("construction_efficiency",1).Double);
 }
 static V crew_config(Game g)=>g.c.beta.Get("construction_crews",V.Map("nominal_cp_per_month",4.0,"duration_months",1.0,"base_cost",60.0,"cost_multiplier",2.0));
 public static V crews(Game g) {
  var result=V.Arr();var now=g.s.Get("local_hours",0).Double;
  foreach(var crew in g.s["block"].Get("construction_crews",V.Arr()))if(crew["expires_at"].Double>now){var row=crew.Duplicate(true);row["remaining_months"]=crew["expires_at"].Double-now;result.Add(row);}return result;
 }
 public static V crew_quote(Game g) {
  var cfg=crew_config(g);var count=crews(g).Count;var cost=cfg["base_cost"].Double*Math.Pow(cfg["cost_multiplier"].Double,count);
  return V.Map("active_count",count,"cost",cost,"nominal_cp_per_month",cfg["nominal_cp_per_month"],"duration_months",cfg["duration_months"],"affordable",g.s["phase"].Text=="play"&&g.s["currency"].Double>=cost);
 }
 public static string hire_crew(Game g) {
  if(g.s["phase"].Text!="play")return "当前不能雇用施工队。";
  var quote=crew_quote(g);if(!quote["affordable"].Bool)return "雇用经费不足。";
  Civic.ensure(g);var now=g.s["local_hours"].Double;var active=crews(g);var crew=V.Map("id",g.uid("C"),"started_at",now,"expires_at",now+quote["duration_months"].Double,"nominal_cp_per_month",quote["nominal_cp_per_month"],"cost",quote["cost"]);
  active.Add(crew);g.s["block"]["construction_crews"]=active;g.s["currency"]-=quote["cost"].Double;g.s["totals"]["currency_spent"]+=quote["cost"].Double;
  g.log_event("construction_crew",crew,"雇用临时施工队：+4 CP/来生月，持续1来生月；一次付费，不自动续费。");g.revision++;g.refresh_metrics();return "";
 }
 public static V capacity_breakdown(Game g) {
  var basic=g.s["block"].Get("construction_capacity",g.c.v040["progression"]["construction_capacity"]).Double;
  var bonus=g.compiled.Get("options",V.Map()).Get("construction_capacity",0).Double;double buildings=0;
  foreach(var id in g.playable_ids()){var p=g.s["projects"][id];if(p["status"].Text=="running"&&!p["paused"].Bool)buildings+=g.compiled.Get("stats",V.Map()).Get(id,V.Map()).Get("construction_capacity",0).Double*p["usage"].Double;}
  var multiplier=g.compiled.Get("options",V.Map()).Get("construction_efficiency",1).Double;var basis=base_capacity(g);var rows=crews(g);double extra=0;foreach(var crew in rows)extra+=crew["nominal_cp_per_month"].Double;
  var energy=Math.Min(1,Math.Max(0,g.s["metrics"].Get("energy_ratio",1).Double));var reserve=Math.Min(1,Math.Max(0,g.s["block"].Get("city_cp_reserve",0).Double));
  return V.Map("base",basic,"bonus",bonus,"buildings",buildings,"multiplier",multiplier,"base_total",basis,"crew_count",rows.Count,"crews_nominal",extra,"energy_ratio",energy,"total",basis+extra,"effective",(basis+extra)*energy,"local_effective",(basis*(1-reserve)+extra)*energy,"city_effective",basis*reserve*energy);
 }
 public static double capacity(Game g) {double extra=0;foreach(var crew in crews(g))extra+=crew["nominal_cp_per_month"].Double;return base_capacity(g)+extra;}
 public static V queue(Game g) {
  var result=V.Arr();foreach(var id in g.playable_ids()){var p=g.s["projects"][id];var upgrading=p.Get("upgrade_job",V.Map()).Count!=0;if(!upgrading&&p["status"].Text!="construction")continue;var job=p[upgrading?"upgrade_job":"job"];
   result.Add(V.Map("id",id,"name",g.project_name(id.Text),"upgrade",upgrading,"work",job["work"],"total",job["total"],"material",job["material"],"priority",p.Get("construction_priority",0).Long,"paused",p["paused"]));}
  result.Items.Sort((a,b)=>a["priority"].Long!=b["priority"].Long?b["priority"].Long.CompareTo(a["priority"].Long):StringComparer.Ordinal.Compare(a["id"].Text,b["id"].Text));return result;
 }
 public static string set_priority(Game g,string id,int value){if(!g.s["projects"].Has(id))return "项目不存在。";g.s["projects"][id]["construction_priority"]=Math.Min(1,Math.Max(-1,value));g.refresh_metrics();return "";}
 public static void tick(Game g,double h) {
  var energy=Math.Min(1,Math.Max(0,g.s["metrics"].Get("energy_ratio",1).Double));var end=g.s["local_hours"].Double;var start=end-h;
  var available=base_capacity(g)*h*energy;double crew_nominal_cp=0;
  foreach(var crew in g.s["block"].Get("construction_crews",V.Arr()))crew_nominal_cp+=Math.Max(0,Math.Min(end,crew["expires_at"].Double)-Math.Max(start,crew["started_at"].Double))*crew["nominal_cp_per_month"].Double;
  var crew_cp=crew_nominal_cp*energy;
  var reserve=available*Math.Min(1,Math.Max(0,g.s["block"].Get("city_cp_reserve",0).Double));
  if(g.s["block"].Get("city_context",false).Bool)g.s["block"]["city_cp_last"]=V.Map("cp",reserve,"available",available,"hours",h,"tick",g.s["simulation_tick"]);
  var remaining=available-reserve+crew_cp;var used=0.0;var material_used=0.0;
  foreach(var row in queue(g)){if(row["paused"].Bool)continue;var p=g.s["projects"][row["id"]];var job=p[row["upgrade"].Bool?"upgrade_job":"job"];
   var rate=job["material"].Double/Math.Max(1e-15,job["work"].Double);var work=Math.Min(remaining,job["work"].Double);if(rate>0)work=Math.Min(work,g.inventory_total("material")/rate);var material=work*rate;
   if(material>0)g.withdraw_stock("material",material);job["work"]=Math.Max(0,job["work"].Double-work);job["material"]=Math.Max(0,job["material"].Double-material);
   var activity=g.s["metrics"].Get("activity_ledger",V.Map());if(activity.Has(row["id"])&&material>0)activity[row["id"]].Add(V.Map("kind","input","category","construction","resource","material","rate",material/h,"source","本地施工队列实际领料"));
   remaining-=work;used+=work;material_used+=material;if(!row["upgrade"].Bool){p["usage"]=work/Math.Max(1e-15,h*base_capacity(g)+crew_nominal_cp);p["bottleneck"]=work>0?"施工中":"等待施工能力或材料";}
   if(job["work"].Double<=1e-10)Development.complete(g,row["id"].Text);
  }
  g.s["block"]["construction_last"]=V.Map("cp",used,"material",material_used,"hours",h,"crew_cp",crew_cp,"base_cp",available,"city_reserved_cp",reserve);
  if(g.s["block"].Has("construction_crews"))g.s["block"]["construction_crews"]=crews(g);
  if(g.s["audit"].Get("resources",V.Map()).Has("material")){var a=g.s["audit"]["resources"]["material"];a["consumed"]+=material_used;a["after"]=g.inventory_total("material");a["error"]=a["before"].Double+a["produced"].Double-a["consumed"].Double-a["lost"].Double-a["after"].Double;
   g.s["metrics"]["flows"]["material"]=g.s["metrics"]["flows"].Get("material",0).Double-material_used/h;}
 }
}
}
