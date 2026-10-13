using System;
namespace MET.Core {
/// <summary>Stable priority queue jointly settles construction capacity and actual material.</summary>
public static class Construction {
 public static double capacity(Game g) {
  var n=g.s["block"].Get("construction_capacity",g.c.v040["progression"]["construction_capacity"]).Double;
  n+=g.compiled.Get("options",V.Map()).Get("construction_capacity",0).Double;
  foreach(var id in g.playable_ids()){var p=g.s["projects"][id];if(p["status"].Text=="running"&&!p["paused"].Bool)n+=g.compiled["stats"].Get(id,V.Map()).Get("construction_capacity",0).Double*p["usage"].Double;}
  return Math.Max(0,n*g.compiled.Get("options",V.Map()).Get("construction_efficiency",1).Double);
 }
 public static V queue(Game g) {
  var result=V.Arr();foreach(var id in g.playable_ids()){var p=g.s["projects"][id];var upgrading=p.Get("upgrade_job",V.Map()).Count!=0;if(!upgrading&&p["status"].Text!="construction")continue;var job=p[upgrading?"upgrade_job":"job"];
   result.Add(V.Map("id",id,"name",g.project_name(id.Text),"upgrade",upgrading,"work",job["work"],"total",job["total"],"material",job["material"],"priority",p.Get("construction_priority",0).Long,"paused",p["paused"]));}
  result.Items.Sort((a,b)=>a["priority"].Long!=b["priority"].Long?b["priority"].Long.CompareTo(a["priority"].Long):StringComparer.Ordinal.Compare(a["id"].Text,b["id"].Text));return result;
 }
 public static string set_priority(Game g,string id,int value){if(!g.s["projects"].Has(id))return "项目不存在。";g.s["projects"][id]["construction_priority"]=Math.Min(1,Math.Max(-1,value));g.refresh_metrics();return "";}
 public static void tick(Game g,double h) {
  var remaining=capacity(g)*h*Math.Min(1,Math.Max(0,g.s["metrics"].Get("energy_ratio",1).Double));var used=0.0;var material_used=0.0;
  foreach(var row in queue(g)){if(row["paused"].Bool)continue;var p=g.s["projects"][row["id"]];var job=p[row["upgrade"].Bool?"upgrade_job":"job"];
   var rate=job["material"].Double/Math.Max(1e-15,job["work"].Double);var work=Math.Min(remaining,job["work"].Double);if(rate>0)work=Math.Min(work,g.inventory_total("material")/rate);var material=work*rate;
   if(material>0)g.withdraw_stock("material",material);job["work"]=Math.Max(0,job["work"].Double-work);job["material"]=Math.Max(0,job["material"].Double-material);
   remaining-=work;used+=work;material_used+=material;if(!row["upgrade"].Bool){p["usage"]=work/Math.Max(1e-15,h*capacity(g));p["bottleneck"]=work>0?"施工中":"等待施工能力或材料";}
   if(job["work"].Double<=1e-10)Development.complete(g,row["id"].Text);
  }
  g.s["block"]["construction_last"]=V.Map("cp",used,"material",material_used,"hours",h);
  if(g.s["audit"].Get("resources",V.Map()).Has("material")){var a=g.s["audit"]["resources"]["material"];a["consumed"]+=material_used;a["after"]=g.inventory_total("material");a["error"]=a["before"].Double+a["produced"].Double-a["consumed"].Double-a["lost"].Double-a["after"].Double;
   g.s["metrics"]["flows"]["material"]=g.s["metrics"]["flows"].Get("material",0).Double-material_used/h;}
 }
}
}
