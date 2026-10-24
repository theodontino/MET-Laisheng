using System;
namespace MET.Core {
/// <summary>Read-only local facts and last committed settlement receipts; never advances a forecast.</summary>
public static class TileSummary {
 static V resource_rows(V ledger) {
  var resources=V.Map();foreach(var row in ledger){var resource=row.Get("resource","").Text;if(resource=="")continue;if(!resources.Has(resource))resources[resource]=V.Map("resource",resource,"production",0.0,"production_kept",0.0,"production_lost",0.0,"building_input",0.0,"resident_consumption",0.0,"construction_material",0.0);
   var item=resources[resource];var rate=row.Get("rate",0).Double;
   if(row["kind"].Text=="output"){item["production"]+=rate;item["production_kept"]+=row.Get("kept",rate).Double;item["production_lost"]+=row.Get("lost",0).Double;}
   else if(row["kind"].Text=="input"){var category=row.Get("category",row.Get("consumer","").Text.StartsWith("pop:",StringComparison.Ordinal)?"resident":"building").Text;item[category=="resident"?"resident_consumption":category=="construction"?"construction_material":"building_input"]+=rate;}
  }var result=V.Arr();foreach(var key in Rules.ids(resources))result.Add(resources[key]);return result;
 }
 static V slots(Game g,V project) {
  var result=V.Arr();foreach(var key in Rules.ids(project.Get("slots",V.Map()))){var iid=project["slots"][key].Text;var instance=g.s["bonus_instances"].Get(iid,V.Map());var bonus=g.c.bonuses.Get(instance.Get("bonus_id",""),V.Map());result.Add(V.Map("slot",key,"type",project.Get("slot_types",V.Map()).Get(key,key),"instance_id",iid,"bonus_id",instance.Get("bonus_id",""),"name",bonus.Get("name","空槽"),"active",instance.Get("active",false),"provider_org",instance.Get("provider_org",""),"contract_org",instance.Get("contract_org",instance.Get("provider_org",""))));}return result;
 }
 public static V describe(Game g,string tile) {
  var s=g.s;var metrics=s.Get("metrics",V.Map());var buildings=V.Arr();var all_receipts=V.Arr();var fiscal=V.Arr();var income_sources=V.Arr();var adjacency=V.Arr();var local_ids=V.Arr();var people=V.Map("population",0.0,"resident_employed",0.0,"workplace_jobs",0.0,"workplace_jobs_filled",0.0,"groups",V.Arr());
  var result=V.Map("tile",tile??"","available",s["cells"].Has(tile??""),"tick",s["simulation_tick"],"local_month",s["local_hours"],"settlement_available",metrics.Has("activity_ledger"),"buildings",buildings,"people",people,"resources",V.Arr(),"receipts",all_receipts,"fiscal",fiscal,"income_sources",income_sources,"cash",V.Map("income_attributed",0.0,"upkeep",0.0),"adjacency",adjacency,"fields",V.Arr(),"notes",V.Arr());
  if(!result["available"].Bool)return result;
  result["cell"]=s["cells"][tile].Duplicate(true);
  foreach(var bid in Rules.ids(s["projects"]))if(s["projects"][bid]["tile"].Text==tile)local_ids.Add(bid);
  foreach(var gid in Rules.ids(s["groups"])) {
   var group=s["groups"][gid];double employed=0;foreach(var job in group.Get("employment",V.Map()).Pairs){employed+=job.Value.Double;if(local_ids.Contains(job.Key))people["workplace_jobs_filled"]+=job.Value.Double;}
   if(local_ids.Contains(group["host"])){people["population"]+=group["count"].Double;people["resident_employed"]+=employed;people["groups"].Add(V.Map("id",gid,"host",group["host"],"population",group["count"],"employed",employed,"employment",group.Get("employment",V.Map()).Duplicate(true)));}
  }
  foreach(var bid in local_ids) {
   var p=s["projects"][bid];var definition=g.c.projects.Get(p["def"],V.Map());var stats=g.compiled.Get("stats",V.Map()).Get(bid,V.Map());var receipts=metrics.Get("activity_ledger",V.Map()).Get(bid,V.Arr());
   foreach(var receipt in receipts){var row=receipt.Duplicate(true);row["building_id"]=bid;all_receipts.Add(row);}
   if(p["status"].Text=="running"&&!p["paused"].Bool)people["workplace_jobs"]+=Math.Max(0,stats.Get("jobs",0).Double);
   var trace=g.compiled.Get("trace",V.Map()).Get(bid,V.Map()).Get("spatial_synergy",V.Map());foreach(var contribution in trace.Get("contributors",V.Arr())){var row=contribution.Duplicate(true);row["target_id"]=bid;adjacency.Add(row);}
   buildings.Add(V.Map("id",bid,"name",g.project_name(bid.Text),"def",p["def"],"family_id",p.Get("family_id",definition.Get("family_id","")),"tier",p.Get("tier",definition.Get("tier",0)),"operator_org",p["operator_org"],"operator_name",g.c.operators.Get(p["operator_org"],V.Map()).Get("name",p["operator_org"]),"status",p["status"],"construction_priority",p.Get("construction_priority",0),"paused",p["paused"],"usage",p["usage"],"bottleneck",p["bottleneck"],"inventory",p["inventory"].Duplicate(true),"traits",p["traits"].Duplicate(true),"slots",slots(g,p),"resources",resource_rows(receipts),"receipts",receipts.Duplicate(true),"adjacency_multiplier",trace.Get("value",1),"job",p.Get("job",V.Map()).Duplicate(true),"upgrade_job",p.Get("upgrade_job",V.Map()).Duplicate(true)));
  }
  foreach(var row in metrics.Get("fiscal_ledger",V.Arr()))if(local_ids.Contains(row["source_id"])){fiscal.Add(row.Duplicate(true));if(row["kind"].Text=="expense")result["cash"]["upkeep"]+=row["rate"].Double;}
  foreach(var row in metrics.Get("fiscal_sources",V.Arr()))if(local_ids.Contains(row["source_id"])){income_sources.Add(row.Duplicate(true));result["cash"]["income_attributed"]+=row["rate"].Double;}
  result["resources"]=resource_rows(all_receipts);
  foreach(var kind in new[]{"employment","medical","education","entertainment","commercial","pollution","heat"})result["fields"].Add(MapInspection.Describe(s,tile,kind,includeNeighbors:false));
  result["notes"].Add("人口、建筑、库存为当前状态；资源与财政速率为最近一次实际结算，每来生月。");
  result["notes"].Add("本地居民就业与本地工作岗位已填分别统计；跨格通勤不会重复计入本地居民。");
  result["notes"].Add("税收按全城原公式统一结算；本格仅列可追溯的税源，不均摊市政收入。");
  if(!metrics.Has("fiscal_sources"))result["notes"].Add("尚无本格税源凭据；首次新结算后可读。");
  if(!metrics.Has("activity_ledger"))result["notes"].Add("尚无实际运行凭据；零值不代表已完成结算。");
  return result;
 }
}
}
