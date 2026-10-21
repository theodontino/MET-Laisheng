using System;
using System.Linq;
namespace MET.Core {
public sealed partial class CitySession {
 public static V CityProjectRequirements()=>V.Map("id","tokamak_crown","name","托卡马克之冠","districts",2,"material",120.0,"advanced_components",30.0,"cp",96.0,"reserve_fraction",.25,"technology",V.Arr("precision_engineering","superconductors","fusion_physics"));
 V ProjectCapabilities() {
  var industry=V.Arr();var electricity=V.Arr();var research=V.Arr();var supporting=V.Arr();
  foreach(var pair in Games.OrderBy(x=>x.Key,StringComparer.Ordinal)) {
   var g=pair.Value;var district=Block(pair.Key)["district_id"].Text;
   foreach(var id in g.playable_ids()) {
    var p=g.s["projects"][id];if(p["status"].Text!="running"||p["paused"].Bool||p.Get("usage",0).Double<=.1)continue;
    var def=p["def"].Text;var target=def=="manufacturing"?industry:def=="power_plant"?electricity:def=="secondary_school"?research:null;
    if(target==null)continue;
    if(def=="power_plant"&&g.s["metrics"].Get("energy_sources",V.Map()).Get(id,0).Double<=1)continue;
    if(def=="secondary_school"&&g.s["knowledge"].Double<=0)continue;
    if(!target.Contains(district))target.Add(district);if(!supporting.Contains(district))supporting.Add(district);
   }
  }
  var reason="";var requirements=CityProjectRequirements();
  foreach(var tech in requirements["technology"])if(!HasTechnology(tech.Text)){reason="缺少城市技术："+ResearchDefinitions()[tech]["name"].Text;break;}
  if(reason==""&&s.Get("districts",V.Map()).Count<requirements["districts"].Long)reason="需要至少两个永久地区";
  if(reason==""&&industry.Count==0)reason="需要实际运行的制造工厂";
  if(reason==""&&electricity.Count==0)reason="需要实际供电的发电厂";
  if(reason==""&&research.Count==0)reason="需要知识与实际运行的中学校";
  if(reason==""&&supporting.Count<requirements["districts"].Long)reason="工业、电力、科研能力须来自至少两个地区";
  return V.Map("industry_districts",industry,"electricity_districts",electricity,"research_districts",research,"supporting_districts",supporting,"ready",reason=="","reason",reason);
 }
 public string StartProject() {
  ScienceEnsure();if(!CanEditFocused)return "先进入有治理权的街区。";
  if(s["projects"].Has("tokamak_crown"))return "托卡马克之冠已启动或完成。";
  var capabilities=ProjectCapabilities();if(!capabilities["ready"].Bool)return capabilities["reason"].Text;
  s["projects"]["tokamak_crown"]=V.Map("id","tokamak_crown","name","托卡马克之冠","status","running","work",0.0,"delivered",V.Map("material",0.0,"advanced_components",0.0),"consumed",V.Map("material",0.0,"advanced_components",0.0),"deliveries",V.Arr(),"contributors",V.Arr(),"cp_ticks",V.Map(),"cp_sources",V.Map(),"reason","等待两地区真实交付与施工");
  ConfigureScienceContext();Record("city_project_started",V.Map("project_id","tokamak_crown","capabilities",capabilities));return "";
 }
 public string ContributeProject(string blockId,string resource,double amount) {
  ScienceEnsure();var permission=ResearchPermission(blockId);if(permission!="")return permission;
  var p=s["projects"].Get("tokamak_crown",V.Map());if(p.Count==0||p["status"].Text!="running")return "城市工程尚未启动或已经完成。";
  if(resource!="material"&&resource!="advanced_components")return "工程接受材料与高级部件；生活制成品不能替代高级部件。";
  if(double.IsNaN(amount)||double.IsInfinity(amount)||amount<=0)return "交付数量必须为正数。";
  var remaining=CityProjectRequirements()[resource].Double-p["delivered"][resource].Double;if(amount>remaining+1e-8)return "交付超过工程剩余需要。";
  var g=Games[blockId];if(g.inventory_total(resource)+1e-8<amount)return "该街区实际库存不足。";
  g.withdraw_stock(resource,amount);p["delivered"][resource]+=amount;
  var audit=g.s.Get("audit",V.Map()).Get("resources",V.Map()).Get(resource,V.Map());
  if(audit.Count>0){audit["commission_delivered"]=audit.Get("commission_delivered",0).Double+amount;ScienceResourceReceipt(g,resource,0,Math.Max(1e-15,g.s["audit"].Get("hours",1).Double));}
  var district=Block(blockId)["district_id"].Text;if(!p["contributors"].Contains(district))p["contributors"].Add(district);
  var delivery=V.Map("block_id",blockId,"district_id",district,"resource",resource,"amount",amount,"local_month",g.s["local_hours"],"tick",g.s["simulation_tick"]);p["deliveries"].Add(delivery);
  Record("city_project_delivery",delivery);g.revision++;return "";
 }
 void ProjectTick(V localMonths) {
  var p=s["projects"].Get("tokamak_crown",V.Map());if(p.Count==0||p["status"].Text!="running")return;
  var cp=0.0;var receipts=V.Map();
  foreach(var blockId in localMonths.Keys.OrderBy(x=>x,StringComparer.Ordinal)) {
   if(!Games.ContainsKey(blockId))continue;var g=Games[blockId];var receipt=g.s["block"].Get("city_cp_last",V.Map());var tick=receipt.Get("tick",-1).Long;
   if(tick<0||tick==p["cp_ticks"].Get(blockId,-1).Long)continue;p["cp_ticks"][blockId]=tick;
   var reserved=receipt.Get("cp",0).Double;cp+=reserved;receipts[blockId]=reserved;
  }
  var capabilities=ProjectCapabilities();if(!capabilities["ready"].Bool){p["reason"]=capabilities["reason"];return;}
  if(p["contributors"].Count<2){p["reason"]="需要至少两个地区的真实物资交付";return;}
  var requirements=CityProjectRequirements();var work=Math.Min(cp,requirements["cp"].Double-p["work"].Double);
  foreach(var resource in new[]{"material","advanced_components"})work=Math.Min(work,Math.Max(0,p["delivered"][resource].Double-p["consumed"][resource].Double)*requirements["cp"].Double/requirements[resource].Double);
  if(work<=1e-12){p["reason"]="等待预留施工能力、材料或高级部件";return;}
  p["work"]+=work;foreach(var resource in new[]{"material","advanced_components"})p["consumed"][resource]+=work*requirements[resource].Double/requirements["cp"].Double;
  foreach(var blockId in receipts.Keys)p["cp_sources"][blockId]=p["cp_sources"].Get(blockId,0).Double+work*receipts[blockId].Double/Math.Max(cp,1e-15);
  p["reason"]="全城实际施工推进";
  if(p["work"].Double>=requirements["cp"].Double-1e-8) {
   p["work"]=requirements["cp"];p["status"]="complete";p["reason"]="托卡马克之冠建成；同一城市进入无限经营";
   foreach(var resource in new[]{"material","advanced_components"})p["consumed"][resource]=requirements[resource];
   if(!s.Get("city").IsMap)s["city"]=V.Map();s["city"]["endless"]=true;s["city"]["tokamak_crown"]=V.Map("project_id","tokamak_crown","completed_external_seconds",s.Get("external_seconds",0));
   Record("city_project_complete",V.Map("project_id","tokamak_crown","deliveries",p["deliveries"].Duplicate(true),"cp_sources",p["cp_sources"].Duplicate(true)),"city_project:tokamak_crown");
  }
 }
 public V ProjectView()=>V.Map("requirements",CityProjectRequirements(),"capabilities",ProjectCapabilities(),"project",s.Get("projects",V.Map()).Get("tokamak_crown",V.Map()).Duplicate(true),"endless",s.Get("city",V.Map()).Get("endless",false));
 public string ValidateScience()=>ValidateScienceEnvelope(s);
 public static string ValidateScienceEnvelope(V state) {
  var science=state.Get("science",V.Map());var blocks=state.Get("blocks",V.Map());
  if(!science.Get("technologies",V.Map()).IsMap||!science.Get("research",V.Map()).IsMap||!science.Get("production",V.Map()).IsMap)return "城市科研状态损坏。";
  var definitions=ResearchDefinitions();
  foreach(var id in science.Get("technologies",V.Map()).Keys) {
   if(!definitions.Has(id))return "未知城市技术。";var tech=science["technologies"][id];var record=science["research"].Get(id,V.Map());
   if(!tech.IsMap||record.Get("status","").Text!="complete"||record.Get("progress",0).Double!=1||tech.Get("block_id","")!=record.Get("block_id","")||!blocks.Has(tech.Get("block_id","").Text)||tech.Get("district_id","")!=blocks[tech["block_id"]].Get("district_id",""))return "城市技术缺少真实研究来源。";
   foreach(var required in definitions[id]["requires"])if(!science["technologies"].Has(required))return "城市技术前置断裂。";
  }
  foreach(var r in science.Get("research",V.Map()).Values) {
   if(!definitions.Has(r.Get("id","").Text)||!blocks.Has(r.Get("block_id","").Text))return "城市研究来源无效。";
   foreach(var key in new[]{"progress","knowledge","currency","material","elapsed_months"})if(!Game.is_num(r.Get(key))||r[key].Double<0)return "城市研究投入或进度无效。";
   if(r["progress"].Double>1.000001)return "城市研究进度超过完成值。";
   var def=definitions[r["id"]];foreach(var input in new[]{"knowledge","currency","material"})if(Math.Abs(r[input].Double-def[input].Double*r["progress"].Double)>1e-6)return "城市研究投资不守恒。";
   if(r["status"].Text!="complete"&&r["status"].Text!="running")return "城市研究状态无效。";
   if(r["status"].Text=="complete"&&!science["technologies"].Has(r["id"]))return "已完成研究缺少城市技术。";
  }
  var p=state.Get("projects",V.Map()).Get("tokamak_crown",V.Map());if(p.Count==0)return state.Get("city",V.Map()).Get("endless",false).Bool?"无限经营缺少完成工程。":"";
  if(!Game.is_num(p.Get("work"))||p["work"].Double<0||p["work"].Double>CityProjectRequirements()["cp"].Double+1e-6)return "城市工程施工进度无效。";
  foreach(var resource in new[]{"material","advanced_components"})if(!Game.is_num(p.Get("delivered",V.Map()).Get(resource))||!Game.is_num(p.Get("consumed",V.Map()).Get(resource))||p["consumed"][resource].Double<0||p["delivered"][resource].Double+1e-6<p["consumed"][resource].Double)return "城市工程交付账不守恒。";
  if(!p.Get("deliveries").IsArray||!p.Get("contributors").IsArray||!p.Get("cp_sources").IsMap)return "城市工程来源记录缺失。";
  var delivered=V.Map("material",0.0,"advanced_components",0.0);var districts=V.Arr();
  foreach(var delivery in p["deliveries"]) {
   string block=delivery.Get("block_id","").Text,resource=delivery.Get("resource","").Text;
   if(!blocks.Has(block)||!delivered.Has(resource)||!Game.is_num(delivery.Get("amount"))||delivery["amount"].Double<=0||delivery.Get("district_id","")!=blocks[block]["district_id"])return "城市工程物资来源无效。";
   delivered[resource]+=delivery["amount"];if(!districts.Contains(delivery["district_id"]))districts.Add(delivery["district_id"]);
  }
  foreach(var resource in delivered.Keys) {
   if(Math.Abs(delivered[resource].Double-p["delivered"][resource].Double)>1e-6||p["delivered"][resource].Double>CityProjectRequirements()[resource].Double+1e-6)return "城市工程物资交付与记录不一致。";
   if(Math.Abs(p["consumed"][resource].Double-p["work"].Double*CityProjectRequirements()[resource].Double/CityProjectRequirements()["cp"].Double)>1e-6)return "城市工程消耗与施工不一致。";
  }
  if(districts.Count!=p["contributors"].Count||districts.Any(d=>!p["contributors"].Contains(d)))return "城市工程地区来源不一致。";
  var cp=0.0;foreach(var pair in p["cp_sources"].Pairs){if(!blocks.Has(pair.Key)||!Game.is_num(pair.Value)||pair.Value.Double<0)return "城市施工来源无效。";cp+=pair.Value.Double;}
  if(Math.Abs(cp-p["work"].Double)>1e-6)return "城市共享施工账不守恒。";
  if(p["status"].Text!="running"&&p["status"].Text!="complete")return "城市工程状态无效。";
  if(p["status"].Text=="complete"&&(p["work"]!=CityProjectRequirements()["cp"]||districts.Count<2||!state.Get("city",V.Map()).Get("endless",false).Bool))return "城市工程完成与无限经营结果不一致。";
  if(state.Get("city",V.Map()).Get("endless",false).Bool&&p["status"].Text!="complete")return "无限经营缺少完成工程。";
  return "";
 }
}
}
