using System;
using System.Linq;
namespace MET.Core {
public sealed partial class CitySession {
 // Candidate pacing values. Technology adds a finishing process to the existing
 // manufacturing node; it neither replaces its goods output nor adds building nodes.
 public static V ResearchDefinitions()=>V.Map(
  "precision_engineering",V.Map("name","精密工程","requires",V.Arr(),"knowledge",2.0,"currency",45.0,"material",6.0,"months",3.0),
  "superconductors",V.Map("name","超导材料","requires",V.Arr("precision_engineering"),"knowledge",4.0,"currency",80.0,"material",10.0,"months",4.0),
  "fusion_physics",V.Map("name","聚变物理","requires",V.Arr("superconductors"),"knowledge",6.0,"currency",120.0,"material",15.0,"months",6.0));
 public void ScienceEnsure() {
  if(!s.Get("science").IsMap)s["science"]=V.Map();
  foreach(var key in new[]{"technologies","research","production","production_totals"})if(!s["science"].Get(key).IsMap)s["science"][key]=V.Map();
  if(!s.Get("projects").IsMap)s["projects"]=V.Map();
  ConfigureScienceContext();
 }
 public void ConfigureScienceContext() {
  var working=s.Get("projects",V.Map()).Get("tokamak_crown",V.Map()).Get("status","").Text=="running";
  foreach(var g in Games.Values) {g.s["block"]["city_context"]=true;g.s["block"]["city_cp_reserve"]=working?CityProjectRequirements()["reserve_fraction"].Double:0.0;}
 }
 bool HasTechnology(string id)=>s.Get("science",V.Map()).Get("technologies",V.Map()).Has(id);
 string ResearchPermission(string blockId) {
  if(!Games.ContainsKey(blockId))return "研究街区不存在。";
  if(blockId!=FocusedBlockId||!CanEditFocused)return "先回任并选择有治理权的研究街区。";
  return "";
 }
 double ResearchOperation(Game g,bool mature=false) {
  var best=0.0;
  foreach(var id in g.playable_ids()) {
   var p=g.s["projects"][id];if(p["status"].Text!="running"||p["paused"].Bool)continue;
   if(mature?p["def"].Text!="secondary_school":g.project_stats(id.Text).Get("service.education",0).Double<=0)continue;
   best=Math.Max(best,p.Get("usage",0).Double);
  }
  return best;
 }
 public string StartResearch(string id,string blockId) {
  ScienceEnsure();var permission=ResearchPermission(blockId);if(permission!="")return permission;
  var defs=ResearchDefinitions();if(!defs.Has(id))return "未知城市技术。";
  if(HasTechnology(id))return "这项城市技术已完成。";
  foreach(var r in s["science"]["research"].Values)if(r["status"].Text=="running")return "先完成当前城市研究。";
  foreach(var required in defs[id]["requires"])if(!HasTechnology(required.Text))return "缺少前置技术："+defs[required]["name"].Text+"。";
  var g=Games[blockId];if(ResearchOperation(g,id=="fusion_physics")<=0)return id=="fusion_physics"?"聚变研究需要实际运行的中学校。":"需要实际运行的教育设施。";
  if(g.s.Get("knowledge",0).Double<=0)return "教育尚未产出可投入的知识。";
  if(g.s["currency"].Double<=0||g.inventory_total("material")<=0)return "研究需要实际通货与材料投资。";
  s["science"]["research"][id]=V.Map("id",id,"block_id",blockId,"status","running","progress",0.0,"knowledge",0.0,"currency",0.0,"material",0.0,"elapsed_months",0.0,"last_tick",g.s["simulation_tick"],"reason","等待研究结算");
  Record("research_started",V.Map("technology",id,"block_id",blockId));return "";
 }
 public void ScienceTick(V localMonths,double externalSeconds) {
  ScienceEnsure();
  foreach(var blockId in localMonths.Keys.OrderBy(x=>x,StringComparer.Ordinal)) {
   if(!Games.ContainsKey(blockId))continue;var g=Games[blockId];var months=localMonths[blockId].Double;if(months<=0)continue;
   // One receipt per actual simulation tick. Views or duplicate callbacks produce nothing.
   var receipt=s["science"]["production"].Get(blockId,V.Map());var tick=g.s["simulation_tick"].Long;
   if(receipt.Get("tick",-1).Long==tick)continue;
   var knowledgeBonus=0.0;
   foreach(var covered in g.s["metrics"].Get("service_covered",V.Map()).Values)knowledgeBonus+=covered.Get("education",0).Double*.02*months;
   knowledgeBonus*=Math.Max(0,g.world_effects.Get("knowledge_multiplier",1).Double-1);
   g.s["knowledge"]+=knowledgeBonus;
   var produced=ProduceAdvanced(g,months);produced["knowledge_institution_bonus"]=knowledgeBonus;produced["tick"]=tick;s["science"]["production"][blockId]=produced;
   if(!s["science"]["production_totals"].Has(blockId))s["science"]["production_totals"][blockId]=V.Map("advanced_components",0.0,"material",0.0,"goods",0.0,"currency",0.0,"energy",0.0,"knowledge_institution_bonus",0.0);
   foreach(var input in s["science"]["production_totals"][blockId].Keys)s["science"]["production_totals"][blockId][input]+=produced[input];
   foreach(var research in s["science"]["research"].Values) {
    if(research["status"].Text!="running"||research["block_id"].Text!=blockId)continue;
    var def=ResearchDefinitions()[research["id"]];var operation=ResearchOperation(g,research["id"].Text=="fusion_physics");
    var portion=Math.Min(1-research["progress"].Double,months/def["months"].Double*operation);
    portion=Math.Min(portion,Math.Max(0,g.s["knowledge"].Double)/def["knowledge"].Double);
    portion=Math.Min(portion,Math.Max(0,g.s["currency"].Double)/def["currency"].Double);
    portion=Math.Min(portion,g.inventory_total("material")/def["material"].Double);
    research["last_tick"]=tick;research["elapsed_months"]+=months;
    if(portion<=1e-12){research["reason"]="教育运行、知识、通货或材料不足";continue;}
    g.withdraw_stock("material",def["material"].Double*portion);ScienceResourceReceipt(g,"material",def["material"].Double*portion,months);g.s["knowledge"]-=def["knowledge"].Double*portion;g.s["currency"]-=def["currency"].Double*portion;
    g.s["totals"]["currency_spent"]+=def["currency"].Double*portion;
    foreach(var input in new[]{"knowledge","currency","material"})research[input]+=def[input].Double*portion;
    research["progress"]=Math.Min(1,research["progress"].Double+portion);research["reason"]="实际教育与投资推进";
    if(research["progress"].Double>=1-1e-10) {
     research["progress"]=1.0;research["status"]="complete";research["reason"]="完成";
     s["science"]["technologies"][research["id"]]=V.Map("block_id",blockId,"district_id",Block(blockId)["district_id"],"completed_tick",tick,"investment",research.Duplicate(true));
     Record("research_complete",V.Map("technology",research["id"],"block_id",blockId),"research:"+research["id"].Text);
    }
   }
   g.revision++;
  }
  ProjectTick(localMonths);ConfigureScienceContext();
 }
 V ProduceAdvanced(Game g,double months) {
  var result=V.Map("advanced_components",0.0,"material",0.0,"goods",0.0,"currency",0.0,"energy",0.0,"months",months,"reason","需要精密工程技术");
  if(!HasTechnology("precision_engineering"))return result;
  var energy=Math.Max(0,g.s["metrics"].Get("energy",0).Double-g.s["metrics"].Get("energy_need",0).Double)*months;
  foreach(var id in g.playable_ids()) {
   var p=g.s["projects"][id];if(p["def"].Text!="manufacturing"||p["status"].Text!="running"||p["paused"].Bool)continue;
   if(g.s["metrics"].Get("utilization",V.Map()).Get(id,V.Map()).Get("outputs",V.Map()).Get("goods",0).Double<=0)continue;
   // Goods are the factory's real low-grade output; precision finishing consumes them.
   var room=Math.Max(0,g.stock_cap_for(id.Text,"advanced_components")-p["inventory"].Get("advanced_components",0).Double);
   var n=Math.Min(months*p.Get("usage",0).Double,room);
   n=Math.Min(n,Math.Min(g.inventory_total("material")/2,g.inventory_total("goods")));
   n=Math.Min(n,Math.Min(Math.Max(0,g.s["currency"].Double)/3,energy/2));if(n<=1e-12)continue;
   g.withdraw_stock("material",n*2);g.withdraw_stock("goods",n);ScienceResourceReceipt(g,"material",n*2,months);ScienceResourceReceipt(g,"goods",n,months);g.s["currency"]-=n*3;g.s["totals"]["currency_spent"]+=n*3;energy-=n*2;
   p["inventory"]["advanced_components"]=p["inventory"].Get("advanced_components",0).Double+n;
   var advancedAudit=g.s.Get("audit",V.Map()).Get("resources",V.Map()).Get("advanced_components",V.Map());
   if(advancedAudit.Count>0){advancedAudit["produced"]+=n;ScienceResourceReceipt(g,"advanced_components",0,months);}
   var flows=g.s["metrics"].Get("flows",V.Map());flows["advanced_components"]=flows.Get("advanced_components",0).Double+n/months;
   result["advanced_components"]+=n;result["material"]+=n*2;result["goods"]+=n;result["currency"]+=n*3;result["energy"]+=n*2;
  }
  result["reason"]=result["advanced_components"].Double>0?"实际制造投入与剩余供电":"制造运行、材料、制成品、通货、剩余供电或库容不足";
  return result;
 }
 void ScienceResourceReceipt(Game g,string resource,double consumed,double months) {
  var audit=g.s.Get("audit",V.Map()).Get("resources",V.Map()).Get(resource,V.Map());
  if(audit.Count>0){audit["consumed"]+=consumed;audit["after"]=g.inventory_total(resource);audit["error"]=audit["before"].Double+audit["produced"].Double+audit.Get("imported",0).Double-audit["consumed"].Double-audit["lost"].Double-audit.Get("exported",0).Double-audit.Get("commission_delivered",0).Double-audit["after"].Double;}
  var flows=g.s["metrics"].Get("flows",V.Map());if(flows.Has(resource))flows[resource]=flows[resource].Double-consumed/months;
 }
 public V ScienceView() {
  var definitions=ResearchDefinitions();var result=V.Map("technologies",V.Arr(),"stocks",V.Arr(),"research",s.Get("science",V.Map()).Get("research",V.Map()).Duplicate(true),"production_totals",s.Get("science",V.Map()).Get("production_totals",V.Map()).Duplicate(true));
  foreach(var id in definitions.Keys){var row=definitions[id].Duplicate(true);row["id"]=id;row["complete"]=HasTechnology(id);result["technologies"].Add(row);}
  foreach(var pair in Games.OrderBy(x=>x.Key,StringComparer.Ordinal))result["stocks"].Add(V.Map("block_id",pair.Key,"knowledge",pair.Value.s.Get("knowledge",0),"advanced_components",pair.Value.inventory_total("advanced_components"),"production",s.Get("science",V.Map()).Get("production",V.Map()).Get(pair.Key,V.Map()).Duplicate(true)));
  return result;
 }
}
}
