using System;
using System.Linq;
namespace MET.Core {
/// <summary>Farm-family Wild construction. Queries are detached; benefits commit through the CP queue.</summary>
public static class WildDesign {
 static V Config(Game g)=>g.c.wild;
 public static bool Enabled(Game g)=>g.s.Get("wild",V.Map()).Get("enabled",false).Bool;
 public static bool IsFarmBuild(Game g,string def)=>Enabled(g)&&def=="homestead";
 public static double BuildCP(Game g,string def)=>Enabled(g)&&g.c.projects[def]["tier"].Long==1?Config(g)["base_construction_cp"].Double:g.c.projects[def]["construction_required"].Double;
 public static V BuildQuote(Game g,string def){var d=g.c.projects[def];double cp=BuildCP(g,def);return V.Map("currency",d["cost"],"material",d["material"],"cp",cp,"hours",cp/Math.Max(.01,Construction.capacity(g)),"cp_source",Enabled(g)&&d["tier"].Long==1?"Wild T1 建造基础 CP":"现行建筑目录 CP","price_source","沿用现行建筑目录通货与材料价格");}
 public static bool IsFarm(Game g,V p)=>Enabled(g)&&p.Get("wild_design",V.Map()).Get("family","").Text=="farm";
 static V Initial(Game g,V p)=>V.Map("family","farm","entry_tier",Config(g)["forms"][p["def"]]["tier"],"inherited_points",0,"progression_points",0,"nodes",V.Map());
 public static string Enable(Game g) {
  if(g.s.Get("phase","").Text!="play")return "进入可游玩的地区后才能开始 Wild。";
  if(Enabled(g))return "";
  g.s["block"]["construction_capacity"]=Config(g)["base_cp_per_month"];
  g.s["wild"]=V.Map("enabled",true,"opening_granted",true,"stock",V.Map("low_industrial",Config(g)["opening_low_industrial"]),"unlocked_forms",V.Arr());
  foreach(var id in g.playable_ids())InitializeBuilding(g,g.s["projects"][id]);
  g.log_event("wild_started",V.Map("low_industrial",Config(g)["opening_low_industrial"],"base_cp_per_month",Config(g)["base_cp_per_month"]),"Wild 农居构筑开始；获得开局低级工业品，基础施工供给为每月10 CP。");g.refresh_metrics();Population.rehouse(g);return "";
 }
 public static void InitializeBuilding(Game g,V p) {
  if(!Enabled(g)||p.Get("special",false).Bool||!Config(g)["forms"].Has(p.Get("def",""))||p.Has("wild_design"))return;
  p["wild_design"]=Initial(g,p);
  // Existing operator/slot records remain history. New farms receive no legacy free slots.
  if(p.Get("history",V.Arr()).Count==0&&p.Get("slots",V.Map()).Values.All(x=>x.Text=="")){
   p["slots"]=V.Map();p["slot_types"]=V.Map();p["unlocked_slots"]=0;p["wild_design"]["legacy_slots_suppressed"]=true;
  }
  if(p["status"].Text=="construction") {p["job"]["work"]=BuildCP(g,p["def"].Text);p["job"]["total"]=BuildCP(g,p["def"].Text);}
 }
 static V Design(Game g,V p)=>p.Get("wild_design",Initial(g,p));
 static V Nodes(Game g,V p)=>Design(g,p)["nodes"];
 static bool Done(V nodes,string id)=>nodes.Get(id,V.Map()).Get("state","").Text=="complete";
 static V Incoming(Game g,string node)=>V.Arr(Config(g)["routes"].Where(route=>route["to"].Text==node).Select(route=>route["from"]).ToArray());
 static bool Prerequisites(Game g,V nodes,string node) {
  var incoming=Incoming(g,node);return incoming.Count==0||incoming.Any(predecessor=>Done(nodes,predecessor.Text));
 }
 static double Used(V nodes)=>nodes.Values.Sum(n=>n.Get("dp",0).Double);
 static V Capacity(Game g,V p,V nodes=null) {
  var design=Design(g,p);var cfg=Config(g);double basis=design["entry_tier"].Long==1?cfg["initial_dp"].Double:0;
  double technology=Math.Max(0,g.world_effects.Get("wild_housing_levels",0).Double)*cfg["technology_dp_per_level"].Double;
  double inherited=design.Get("inherited_points",0).Double;double total=basis+technology+inherited,used=Used(nodes??design["nodes"]);
  return V.Map("base",basis,"technology",technology,"inherited",inherited,"total",total,"used",used,"free",total-used);
 }
 public static V Quote(Game g,string id,string node) {
  var result=V.Map("ok",false,"node",node,"error","");
  if(!g.s.Get("projects",V.Map()).Has(id)||!IsFarm(g,g.s["projects"][id])){result["error"]="此建筑未启用 Wild 农居构筑。";return result;}
  var p=g.s["projects"][id];var cfg=Config(g);if(!cfg["nodes"].Has(node)){result["error"]="该节点尚未定义。";return result;}
  var def=cfg["nodes"][node];var kind=def["kind"].Text;var nodes=Nodes(g,p);var existing=nodes.Get(node,V.Map());double dp=0,cp=0,material=0,goods=0,industrial=0;int ordinal=0;
  if(kind=="ordinary"){dp=def["dp"].Double;material=cfg["ordinary_material"].Double;cp=cfg["base_construction_cp"].Double*cfg["node_cp_fraction"].Double*dp;}
  if(kind=="slot"){ordinal=nodes.Keys.Count(k=>cfg["nodes"][k]["kind"].Text=="slot");dp=cfg["slot_dp"][Math.Min(ordinal,2)].Double;goods=cfg["slot_goods"].Double;cp=cfg["base_construction_cp"].Double*cfg["node_cp_fraction"].Double*dp;}
  if(kind=="progression"){dp=cfg["progression_dp"].Double;cp=cfg["progression_cp"].Double;industrial=cfg["progression_low_industrial"].Double;}
  if(existing.Count>0){dp=existing["dp"].Double;ordinal=(int)existing.Get("ordinal",1).Long-1;cp=kind=="progression"?cfg["progression_cp"].Double:cfg["base_construction_cp"].Double*cfg["node_cp_fraction"].Double*dp;}
  result.Merge(V.Map("name",def["name"],"kind",kind,"dp",dp,"cp",cp,"construction_required",cp,"material",material,"goods",goods,"low_industrial",industrial,"cost",0,"ordinal",ordinal+1,"capacity",Capacity(g,p),"pricing_known",p["tier"].Long==1||existing.Count>0,"state",existing.Get("state","available"),"routes_in",Incoming(g,node),"requires",V.Arr(),"requires_any",Incoming(g,node)),true);
  string error="";
  if(g.s["phase"].Text!="play")error="当前不能施工。";
  else if(existing.Count>0)error="该节点已经取得或正在施工。";
  else if(p["tier"].Long!=1)error="当前切片只定义农居 T1 星图；高级形态后续节点报价未知。";
  else if(p["status"].Text!="running"||p.Get("upgrade_job",V.Map()).Count>0)error="请等当前施工完成。";
  else if(!Prerequisites(g,nodes,node))error="请先完成一条相连路线的前置节点。";
  else if(Capacity(g,p)["free"].Double<dp)error="设计容量不足。";
  else if(g.inventory_total("goods")<goods)error="基础制成品不足。";
  else if(g.s["wild"]["stock"].Get("low_industrial",0).Double<industrial)error="低级工业品不足。";
  result["error"]=error;result["ok"]=error=="";return result;
 }
 public static V View(Game g,string id) {
  if(!g.s.Get("projects",V.Map()).Has(id)||!IsFarm(g,g.s["projects"][id]))return V.Map("ok",false,"error","此建筑未启用 Wild 农居构筑。");
  var p=g.s["projects"][id];var nodes=V.Arr();foreach(var node in Config(g)["nodes"].Keys)nodes.Add(Quote(g,id,node));
  return V.Map("ok",true,"id",id,"form",p["def"],"name",Config(g)["forms"][p["def"]]["name"],"tier",p["tier"],"capacity",Capacity(g,p),"nodes",nodes,"routes",Config(g)["routes"].Duplicate(true),"design",Design(g,p).Duplicate(true),"job",p.Get("upgrade_job",V.Map()).Duplicate(true),"direct_quote_known",p["tier"].Long==1);
 }
 public static string Start(Game g,string id,string node) {
  var quote=Quote(g,id,node);if(!quote["ok"].Bool)return quote["error"].Text;
  var p=g.s["projects"][id];var nodes=Nodes(g,p);if(quote["goods"].Double>0)g.withdraw_stock("goods",quote["goods"].Double);
  g.s["wild"]["stock"]["low_industrial"]-=quote["low_industrial"].Double;
  nodes[node]=V.Map("state","construction","dp",quote["dp"],"ordinal",quote["ordinal"],"started_at",g.s["local_hours"]);
  p["upgrade_choice"]="";p["upgrade_job"]=V.Map("wild_node",node,"work",quote["cp"],"total",quote["cp"],"material",quote["material"],"cp",true);
  g.log_event("wild_node_started",V.Map("project",id,"node",node,"dp",quote["dp"],"cp",quote["cp"],"material_scheduled",quote["material"],"goods_spent",quote["goods"],"low_industrial_spent",quote["low_industrial"]),quote["name"].Text+"开始施工。");g.refresh_metrics();return "";
 }
 public static void Complete(Game g,string id) {
  var p=g.s["projects"][id];string node=p["upgrade_job"]["wild_node"].Text;var def=Config(g)["nodes"][node];var design=p["wild_design"];design["nodes"][node]["state"]="complete";
  if(def["kind"].Text=="progression") {
   string target=def["target"].Text;var d=g.c.projects[target];p["history"].Add(V.Map("kind","wild_progression","from",p["def"],"to",target,"h",g.s["local_hours"]));
   p["def"]=target;p["node_id"]=target;p["tier"]=d["tier"];p["traits"]=d["traits"].Duplicate();p["retained_storage"]=p["inventory"].Duplicate(true);
   design["inherited_points"]+=Config(g)["inherited_dp_per_tier"].Double;design["progression_points"]+=Config(g)["progression_dp"].Double;
   g.s["sites"][p["site"]]["zone"]=d["zone"];g.s["cells"][p["tile"]]["zone"]=d["zone"];
   if(!g.s["wild"]["unlocked_forms"].Contains(target))g.s["wild"]["unlocked_forms"].Add(target);
  } else if(def["kind"].Text=="slot") {string slot="wild:"+node;p["slots"][slot]="";p["slot_types"][slot]=def["slot"];}
  p["history"].Add(V.Map("kind","wild_node","node",node,"h",g.s["local_hours"]));p["upgrade_job"]=V.Map();p["upgrade_choice"]="";
  g.log_event("wild_node_completed",V.Map("project",id,"node",node),def["name"].Text+"施工完成。");
 }
 static void Reprice(Game g,V nodes) {
  int ordinal=0;foreach(var node in nodes.Keys.Where(k=>Config(g)["nodes"][k]["kind"].Text=="slot").OrderBy(k=>nodes[k]["ordinal"].Long).ToArray()) {nodes[node]["dp"]=Config(g)["slot_dp"][ordinal];nodes[node]["ordinal"]=++ordinal;}
 }
 public static V CancelPreview(Game g,string id,string node) {
  var result=V.Map("ok",false,"error","","removed",V.Arr(),"refund_dp",0,"resource_refund",0);
  if(!g.s.Get("projects",V.Map()).Has(id)||!IsFarm(g,g.s["projects"][id])){result["error"]="此建筑未启用 Wild 农居构筑。";return result;}
  var p=g.s["projects"][id];var nodes=Nodes(g,p).Duplicate(true);
  if(!nodes.Has(node)){result["error"]="该节点尚未取得。";return result;}
  if(Config(g)["nodes"][node]["kind"].Text=="progression"&&Done(nodes,node)){result["error"]="已完成的 Tier 进阶不可撤销。";return result;}
  if(p.Get("upgrade_job",V.Map()).Count>0){result["error"]="当前存在未完工施工；请先等候完工再撤销构筑。";return result;}
  double before=Used(nodes);nodes.Erase(node);result["removed"].Add(node);bool changed=true;
  while(changed){changed=false;foreach(var current in nodes.Keys){if(Config(g)["nodes"][current]["kind"].Text=="progression"&&Done(nodes,current))continue;if(Prerequisites(g,nodes,current))continue;nodes.Erase(current);result["removed"].Add(current);changed=true;}}
  Reprice(g,nodes);result["refund_dp"]=before-Used(nodes);result["capacity_after"]=Capacity(g,p,nodes);result["remaining_nodes"]=nodes;result["ok"]=true;return result;
 }
 public static string Cancel(Game g,string id,string node) {
  if(g.s.Get("phase","").Text!="play")return "当前不可撤销。";var preview=CancelPreview(g,id,node);if(!preview["ok"].Bool)return preview["error"].Text;var p=g.s["projects"][id];
  foreach(var removed in preview["removed"]){string slot="wild:"+removed.Text;if(p["slots"].Has(slot)){string instance=p["slots"][slot].Text;if(instance!=""&&g.s["bonus_instances"].Has(instance)){string bid=g.s["bonus_instances"][instance]["bonus_id"].Text;g._unmount(instance);Bonus.grant(g,bid,"wild_cancel:"+instance);}p["slots"].Erase(slot);p["slot_types"].Erase(slot);}
   if(p.Get("upgrade_job",V.Map()).Get("wild_node","").Text==removed.Text){p["upgrade_job"]=V.Map();p["upgrade_choice"]="";}}
  p["wild_design"]["nodes"]=preview["remaining_nodes"].Duplicate(true);g.log_event("wild_node_cancelled",V.Map("project",id,"removed",preview["removed"],"refund_dp",preview["refund_dp"]),"构筑已撤销；返还设计容量，已用资源不返还。");g.refresh_metrics();Population.rehouse(g);return "";
 }
 public static void ApplyStats(Game g,V p,V stats) {
  if(!IsFarm(g,p))return;var form=Config(g)["forms"][p["def"]];double housing=0,food=0;
  foreach(var node in Nodes(g,p).Keys)if(Done(Nodes(g,p),node)){var def=Config(g)["nodes"][node];housing+=def.Get("housing_pct",0).Double;food+=def.Get("food_pct",0).Double;}
  stats["housing"]=form["housing"].Double*(1+housing);stats["output.food"]=form["food"].Double*(1+food);
 }
 public static string Validate(Game g,V state) {
  if(!state.Has("wild"))return "";var wild=state["wild"];if(!wild.IsMap||!wild.Get("stock").IsMap||!wild.Get("unlocked_forms").IsArray)return "Wild 状态损坏。";
  foreach(var amount in wild["stock"].Values)if(!Game.is_num(amount)||amount.Double<0)return "Wild 库存损坏。";
  foreach(var form in wild["unlocked_forms"])if(!Config(g)["forms"].Has(form))return "Wild 形态记录损坏。";
  foreach(var p in state["projects"].Values)if(p.Has("wild_design")){var d=p["wild_design"];if(!d.IsMap||d.Get("family","").Text!="farm"||!d.Get("nodes").IsMap||!Config(g)["forms"].Has(p.Get("def","")))return "Wild 建筑记录损坏。";
   foreach(var node in d["nodes"].Keys){var n=d["nodes"][node];if(!Config(g)["nodes"].Has(node)||!Game.is_num(n.Get("dp"))||n["dp"].Double<0||Array.IndexOf(new[]{"complete","construction"},n.Get("state","").Text)<0)return "Wild 节点记录损坏。";}
   if(p.Get("upgrade_job",V.Map()).Has("wild_node")){string node=p["upgrade_job"]["wild_node"].Text;if(!d["nodes"].Has(node)||d["nodes"][node]["state"].Text!="construction")return "Wild 施工引用损坏。";}
  }return "";
 }
}
}
