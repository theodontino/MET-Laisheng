using System;
using System.Globalization;
namespace MET.Core {
/// <summary>Existing city outcome tracking, distinct from finite-DT naming eligibility.</summary>
public static class Progress {
 static string F(double x,int n)=>x.ToString("F"+n,CultureInfo.InvariantCulture);
 public static void initialize(Game g) {
  if(!g.s.Has("city_goal"))g.s["city_goal"]=V.Map();if(g.s["city_goal"].Count!=0)return;var anchors=V.Arr();var baseline=V.Map();
  foreach(var gid in Rules.ids(g.s["groups"])){var p=g.s["groups"][gid];if(p["host"].Text=="homeless")continue;anchors.Add(gid);baseline[gid]=V.Map("health",p["health"],"pollution",p["living"].Get("pollution",0),"service",best_two(p["living"]));}
  g.s["city_goal"]=V.Map("route","community","stable",0.0,"community",0.0,"renewal",0.0,"delivered",false,"completed",false,"awards",V.Arr(),"anchors",anchors,"baseline",baseline);
 }
 public static double best_two(V living){var a=new double[4];var kinds=new[]{"medical","education","entertainment","commercial"};for(var i=0;i<4;i++)a[i]=living.Get(kinds[i],0).Double;Array.Sort(a);return (a[2]+a[3])*.5;}
 public static double service_ratio(Game g,string kind) {var sum=0.0;var people=0.0;foreach(var p in g.s["groups"].Values){sum+=p["count"].Double*p["living"].Get(kind,0).Double;people+=p["count"].Double;}return sum/Math.Max(.001,people);}
 public static double food_ratio(Game g) {var sum=0.0;var people=0.0;foreach(var p in g.s["groups"].Values){sum+=p["count"].Double*p["living"].Get("food",1).Double;people+=p["count"].Double;}return sum/Math.Max(.001,people);}
 public static bool safe(Game g){var m=g.s["metrics"];return m.Get("homeless",0).Double<.1&&food_ratio(g)>=.95&&m.Get("energy_ratio",1).Double>=.95&&g.s["currency"].Double>=0&&(m.Get("cashflow",0).Double>=0||g.s["currency"].Double/Math.Max(.01,-m["cashflow"].Double)>=4.0);}
 public static bool renewal_ok(Game g) {
  var goal=g.s["city_goal"];var found=0;foreach(var gid in goal["anchors"]){if(!g.s["groups"].Has(gid))continue;var p=g.s["groups"][gid];var baseline=goal["baseline"][gid];if(p["host"].Text=="homeless")return false;var service=best_two(p["living"]);
   if(service<.7||service<baseline["service"].Double+.25||p["health"].Double<baseline["health"].Double-1)return false;found++;}return found>0;
 }
 public static string select(Game g,string route){if(!g.c.data["city_goals"].Has(route))return "未知发展成果。";initialize(g);var previous=g.s["city_goal"]["route"].Text;g.s["city_goal"]["route"]=route;Civic.route_selected(g,previous);g.refresh_metrics();return "";}
 public static void after_commit(Game g,double h=.1) {
  initialize(g);var goal=g.s["city_goal"];var m=g.s["metrics"];if(g.s["local_hours"].Double>=g.c.balance["service_start_hours"].Double)g.s["stage"]=1;if(g.s["goods_active"].Bool)g.s["stage"]=2;
  foreach(var id in g.playable_ids())g.s["stage"]=Math.Max(g.s["stage"].Long,g.s["projects"][id].Get("tier",1).Long-1);g.s["block"]["stage"]=g.s["stage"];
  var is_safe=safe(g);goal["stable"]=is_safe?goal["stable"].Double+h:0.0;
  var pleasant=is_safe&&m["happiness"].Double>=72&&m["health"].Double>=72&&service_ratio(g,"medical")>=.75&&service_ratio(g,"commercial")>=.75&&service_ratio(g,"entertainment")>=.75;
  goal["community"]=pleasant?goal["community"].Double+h:0.0;goal["renewal"]=is_safe&&renewal_ok(g)?goal["renewal"].Double+h:0.0;var ready=false;
  switch(goal["route"].Text){case "community":ready=goal["community"].Double>=g.c.balance["goal_duration"].Double;break;case "delivery":ready=goal["delivered"].Bool;break;case "renewal":ready=goal["renewal"].Double>=g.c.balance["goal_duration"].Double;break;}
  if(!goal["completed"].Bool&&Civic.route_permitted(g)&&ready&&m["housed"].Double>=g.c.balance["target_people"].Double&&goal["stable"].Double>=g.c.balance["target_stable_hours"].Double){goal["completed"]=true;g.s["complete"]=true;g.s["currency"]+=120;g.log_event("community_complete",V.Map("route",goal["route"]),"河谷新章完成：这座社区能独立运转了。获得120通货；DT与命名资格不变。");}
 }
 public static string deliver(Game g) {
  initialize(g);if(g.s["city_goal"]["delivered"].Bool)return "交付已完成。";if(!Civic.route_permitted(g)||g.s["city_goal"]["route"].Text!="delivery")return "请先在任务面板接取供给交付成果。";
  var mat=g.c.balance["goal_delivery_material"].Double;var goods=g.c.balance["goal_delivery_goods"].Double;if(!safe(g)||g.s["city_goal"]["stable"].Double<g.c.balance["target_stable_hours"].Double)return "先让住房、粮食、电力与财政稳定1来生月。";
  if(g.inventory_total("material")<mat||g.inventory_total("goods")<goods)return "需要20材料和12制成品，交付时真正扣除。";var foodneed=0.0;foreach(var p in g.s["groups"].Values)foodneed+=p["count"].Double*g.compiled["stats"][p["host"]]["population_food"].Double;
  if(g.inventory_total("food")<foodneed*2)return "交付前留足当前居民至少2来生月的粮食。";g.withdraw_stock("material",mat);g.withdraw_stock("goods",goods);g.s["city_goal"]["delivered"]=true;
  g.log_event("delivery",V.Map("material",mat,"goods",goods),"发展物资已交付，基础生活储备保留。");g.refresh_metrics();after_commit(g,0);return "";
 }
 public static V checklist(Game g) {
  initialize(g);var m=g.s["metrics"];var goal=g.s["city_goal"];var route=goal["route"].Text;var done=route=="community"?goal["community"].Double>=g.c.balance["goal_duration"].Double:route=="delivery"?goal["delivered"].Bool:goal["renewal"].Double>=g.c.balance["goal_duration"].Double;
  return V.Arr(V.Map("name","安顿30位居民，无人待安置","done",m.Get("housed",0).Double>=30&&m.Get("homeless",0).Double<.1,"value",F(m.Get("housed",0).Double,0)+" / 30人"),V.Map("name","生活与财政稳定1来生月","done",goal["stable"].Double>=1,"value",F(goal["stable"].Double,1)+" / 1.0月"),V.Map("name",g.c.data["city_goals"][route]["name"],"done",done,"value",done?"已达成":route=="delivery"?"交付20材料 + 12制成品":F(goal[route].Double,1)+" / "+F(g.c.balance["goal_duration"].Double,1)+"月"));
 }
 public static string crisis(Game g,string choice)=>"本 Beta 不使用旧治理危机结算；DT 归零后可留城或独立重开。";
 public static V requirements(Game g) {
  initialize(g);var m=g.s["metrics"];var route=g.s["city_goal"]["route"].Text;var rows=V.Arr();
  if(route=="community"){
   rows.Add(V.Map("name","健康 / 幸福","value",F(m.Get("health",0).Double,0)+" / "+F(m.Get("happiness",0).Double,0)+"，分别需要72","done",m.Get("health",0).Double>=72&&m.Get("happiness",0).Double>=72,"action","population"));
   foreach(var pair in new[]{new[]{"medical","医疗","medical_clinic"},new[]{"commercial","购物","commercial_center"},new[]{"entertainment","休闲","garden"}}){var ratio=service_ratio(g,pair[0]);rows.Add(V.Map("name",pair[1]+"实际覆盖","value",F(100*ratio,0)+"% / 75%","done",ratio>=.75,"action",pair[2]));}
  }else if(route=="delivery"){
   rows.Add(V.Map("name","可交付材料","value",F(g.inventory_total("material"),1)+" / 20","done",g.inventory_total("material")>=20,"action","extraction"));rows.Add(V.Map("name","可交付制成品","value",F(g.inventory_total("goods"),1)+" / 12","done",g.inventory_total("goods")>=12,"action","manufacturing"));rows.Add(V.Map("name","交付前保留两来生月口粮","value","交付只扣材料和制成品，不扣粮食","done",safe(g),"action","agriculture"));
  }else foreach(var gid in g.s["city_goal"]["anchors"]){if(!g.s["groups"].Has(gid))continue;var group=g.s["groups"][gid];var before=g.s["city_goal"]["baseline"][gid];var ratio=best_two(group["living"]);var target=Math.Max(.7,before["service"].Double+.25);
   rows.Add(V.Map("name",g.project_name(group["host"].Text)+"生活改善","value","最好的两种服务 "+F(100*ratio,0)+"% / "+F(100*target,0)+"%；健康 "+F(group["health"].Double,0)+" / "+F(before["health"].Double-1,0),"done",ratio>=target&&group["health"].Double>=before["health"].Double-1,"action","population"));}
  return rows;
 }
}
}
