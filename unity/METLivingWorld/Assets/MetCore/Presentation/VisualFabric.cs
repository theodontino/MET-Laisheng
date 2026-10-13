using System;
using System.Linq;
namespace MET.Core {
/// <summary>Read-only town morphology. IDs describe visual parcels and shared edges, never simulation entities.</summary>
public static class VisualFabric {
 public static string Pair(string a,string b)=>StringComparer.Ordinal.Compare(a,b)<0?a+"|"+b:b+"|"+a;
 public static double Unit(string identity,int salt=0) { uint value=2166136261;foreach(char c in identity+":"+salt){value=(value^c)*16777619;}return (value&0xffffff)/16777215.0; }
 static double Clamp(double x,double max=1)=>Math.Max(0,Math.Min(max,x));
 static bool Has(V level,string trait)=>level.Get("traits",V.Arr()).Contains(trait);
 public static bool Water(V cell) { var t=cell.Get("terrain","").Text;return t=="water"||t=="sea"||t=="ocean"||t=="river"||t=="channel"||cell.Get("water",false).Bool||cell.Get("is_water",false).Bool||cell.Get("traits",V.Arr()).Contains("tile.water")||cell.Get("traits",V.Arr()).Contains("tile.river"); }
 static bool Residential(V a)=>Has(a,"building.residential")||a["domain"].Text=="residential"||a["domain"].Text=="community";
 static bool Industrial(V a)=>a["domain"].Text=="industrial"||a["domain"].Text=="infrastructure"||new[]{"building.industrial","building.extractive","building.logistics","building.energy"}.Any(t=>Has(a,t));
 static string Kind(V a,V b,double development) {
  if(Residential(a)&&Residential(b))return development>=1.05||Math.Max((int)a["tier"].Long,(int)b["tier"].Long)>=3?"residential_annex":"residential_walkway";
  if(Industrial(a)&&Industrial(b))return Has(a,"building.logistics")||Has(b,"building.logistics")||Has(a,"building.energy")||Has(b,"building.energy")?"industrial_gallery":"industrial_shed";
  return Has(a,"building.commercial")||Has(b,"building.commercial")||a["domain"].Text=="commercial"||b["domain"].Text=="commercial"||Has(a,"building.public_service")||Has(b,"building.public_service")||a["domain"].Text=="public"||b["domain"].Text=="public"?"street_shop":"mixed_court";
 }
 static void Add(V relations,string a,string b,double amount) { if(a==""||b==""||a==b||amount<=0)return;string id=Pair(a,b);relations[id]=Clamp(relations.Get(id,0).Double+amount); }
 static string Owner(string id) {foreach(var prefix in new[]{"pop:","run:","work:"})if(id.StartsWith(prefix,StringComparison.Ordinal))return id.Substring(prefix.Length);if(id.StartsWith("recipe:",StringComparison.Ordinal))return id.Substring(id.LastIndexOf(':')+1);return id;}
 public static V Derive(V snapshot,V definitions=null,V committedStats=null) {
  definitions=definitions??V.Map();var projects=snapshot.Get("projects",V.Map());var groups=snapshot.Get("groups",V.Map());var cells=snapshot.Get("cells",snapshot.Get("tiles",V.Map()));var metrics=snapshot.Get("metrics",V.Map());var stats=snapshot.Get("compiled",V.Map()).Get("stats",committedStats??V.Map());
  var residents=V.Map();foreach(var g in groups.Values){string host=g.Get("host","").Text;residents[host]=residents.Get(host,0).Double+g.Get("count",0).Double;}
  var levels=V.Map();var parcels=V.Map();var relations=V.Map();var edges=V.Map();var fillers=V.Map();
  foreach(var id in projects.Keys.OrderBy(x=>x,StringComparer.Ordinal)) {
   var p=projects[id];if(p.Get("special",false).Bool)continue;var d=definitions.Get(p.Get("def","").Text,V.Map());var st=stats.Get(id,d.Get("stats",V.Map()));double housing=st.Get("housing",0).Double,capacity=Math.Max(.001,housing*st.Get("efficiency",1).Double);double occupancy=Clamp(housing>0?residents.Get(id,0).Double/capacity:p.Get("usage",0).Double),utilization=Clamp(metrics.Get("utilization",V.Map()).Get(id,V.Map()).Get("utilization",p.Get("usage",0)).Double);
   if(p.Get("paused",false).Bool)utilization=0;int tier=Math.Max(1,Math.Min(3,(int)p.Get("tier",d.Get("tier",1)).Long));double development=Clamp((tier-1)*.62+occupancy*.22+utilization*.16,2.25);var level=V.Map("tier",tier,"occupancy",occupancy,"residents",residents.Get(id,0),"housing_capacity",capacity,"utilization",utilization,"development",development,"domain",d.Get("domain",p.Get("domain","")),"traits",p.Get("traits",d.Get("traits",V.Arr())).Duplicate(true),"completed",p.Get("status","running").Text!="construction","def",p.Get("def",""),"operator",p.Get("operator_org",""));levels[id]=level;
   string tile=p.Get("tile","").Text;if(!cells.Has(tile)||Water(cells[tile]))continue;double density=Clamp(development*.45+occupancy*.3+utilization*.2);int stage=level["completed"].Bool?(int)Math.Floor(density*5.999):0;
   parcels["parcel:"+tile]=V.Map("id","parcel:"+tile,"tile",tile,"project",id,"kind",Residential(level)?"residential_annex":Industrial(level)?"industrial_shed":Has(level,"building.commercial")?"street_shop":"mixed_court","stage",stage,"density",density,"completed",level["completed"],"operator",level["operator"],"tier",tier);
   if(!level["completed"].Bool)continue;for(int i=0;i<stage;i++){string fid="parcel:"+tile+":infill:"+i;fillers[fid]=V.Map("id",fid,"parcel",tile,"project",id,"kind",parcels["parcel:"+tile]["kind"],"index",i,"tier",tier,"operator",level["operator"]);}
  }
  foreach(var link in metrics.Get("employment_links",V.Arr()))Add(relations,groups.Get(link.Get("group","").Text,V.Map()).Get("host","").Text,link.Get("target","").Text,Math.Min(.30,link.Get("people",0).Double*.018));
  foreach(var link in metrics.Get("service_links",V.Arr()))Add(relations,link.Get("source","").Text,link.Get("home","").Text,Math.Min(.30,link.Get("people",0).Double*.015));
  foreach(var owner in metrics.Get("activity_ledger",V.Map()).Keys)foreach(var entry in metrics["activity_ledger"][owner])if(entry.Get("kind","").Text=="input")Add(relations,entry.Get("source_id","").Text,owner,Math.Min(.24,entry.Get("rate",0).Double*.04));
  foreach(var link in metrics.Get("allocation",V.Arr()))Add(relations,link.Get("source","").Text,Owner(link.Get("consumer","").Text),Math.Min(.12,link.Get("amount",link.Get("granted",0)).Double*.012));
  foreach(var link in metrics.Get("stock_movements",V.Arr()))Add(relations,link.Get("source","").Text,link.Get("target","").Text,Math.Min(.16,link.Get("amount",0).Double*.016));
  foreach(var tile in cells.Keys.OrderBy(x=>x,StringComparer.Ordinal)) {
   string a=cells[tile].Get("building","").Text;if(!levels.Has(a)||!levels[a]["completed"].Bool||Water(cells[tile]))continue;
   foreach(var neighborValue in Hex.neighbors(tile)) {string neighbor=neighborValue.Text;if(!cells.Has(neighbor)||StringComparer.Ordinal.Compare(tile,neighbor)>=0||Water(cells[neighbor]))continue;string b=cells[neighbor].Get("building","").Text;if(a==b||!levels.Has(b)||!levels[b]["completed"].Bool)continue;
    var x=levels[a];var y=levels[b];double relation=relations.Get(Pair(a,b),0).Double;bool same=x["operator"].Text!=""&&x["operator"].Text==y["operator"].Text;double development=(x["development"].Double+y["development"].Double)*.5;double connection=Clamp((relation+(same?.12:0))*.72+development*.18+Math.Min(x["occupancy"].Double,y["occupancy"].Double)*.1);string edge="edge:"+Pair(tile,neighbor);int stage=(int)Math.Floor(Clamp(connection*1.1+development*.35)*4.999);string kind=Kind(x,y,development);
    edges[edge]=V.Map("id",edge,"a",tile,"b",neighbor,"project_a",a,"project_b",b,"kind",kind,"stage",stage,"strength",connection,"relation",relation,"same_operator",same,"development",development,"tier_a",x["tier"],"tier_b",y["tier"],"operator",same?x["operator"]:V.From(""),"height_mode","terrain_adaptive","variant",(int)(Unit(edge)*3));
    for(int i=0;i<stage;i++){string fid=edge+":infill:"+i;fillers[fid]=V.Map("id",fid,"edge",edge,"a",tile,"b",neighbor,"kind",kind,"index",i,"tier",Math.Max((int)x["tier"].Long,(int)y["tier"].Long),"operator",same?x["operator"]:V.From(""));}
   }
  }
  return V.Map("levels",levels,"parcels",parcels,"connections",relations,"connectors",edges,"fillers",fillers);
 }
}
}
