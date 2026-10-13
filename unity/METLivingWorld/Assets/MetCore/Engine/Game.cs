using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace MET.Core {
/// <summary>The original Beta city API, independent of Unity objects and clocks.</summary>
public sealed partial class Game {
    public Catalog c;
    public V s=V.Map(),compiled=V.Map(),spatial_cache=V.Map();
    public string last_error="",storageRoot;
    public bool preview_mode=false,recording_cycle=false;
    public int revision=0,last_committed_revision=-1;
    public Game(Catalog catalog,string storageRoot=""){c=catalog;this.storageRoot=storageRoot;}
    public static bool is_num(V x)=>x.IsNumber&&!double.IsNaN(x.Double)&&!double.IsInfinity(x.Double);
    static double Clamp(double n,double a,double b)=>Math.Min(b,Math.Max(a,n));
    static IEnumerable<string> Ids(V value)=>Rules.ids(value).Select(x=>x.Text);
    public void new_game(long seed_value,double now) {
        long v=Math.Abs(seed_value)%2147483000+1;
        s=V.Map("schema",6,"build",c.data["build"],"seed",seed_value,"phase","select_block","block_index",-1,
            "rng",V.Map("opening",v,"draft",(v+1701)%2147483646+1,"events",(v+5303)%2147483646+1),
            "wall_mark",now,"global_now",now,"started_at",now,"local_hours",0.0,"remainder",0.0,"simulation_tick",0,
            "currency",c.balance["initial_money"].Double,"dt",c.balance["initial_dt"].Double,"support",75.0,"mandate",75.0,
            "block",V.Map("id","block:1","traits",V.Arr("block.river_valley"),"labels",V.Arr(),"policies",V.Map("Tax","tax_head","Labor","jobs_access","Environment","pollution_default","Procurement","procure_near","Land","land_normal")),
            "cells",V.Map(),"sites",V.Map(),"projects",V.Map(),"groups",V.Map(),"organizations",V.Map(),"fields",V.Map(),"authored_fields",V.Map(),"bonus_instances",V.Map(),
            "next_id",1,"origin_choices",V.Arr(),"origin","","draft",V.Map(),"tender_debt",0,"log",V.Arr(),"events",V.Arr(),"event_cursor",0,
            "metrics",V.Map(),"audit",V.Map(),"complete",false,"failed_reason","","crisis",V.Map(),"governance_bad_hours",0.0,
            "totals",V.Map("built",0,"installed",0,"rerolls",0,"dt_used",0.0,"currency_spent",0.0),"block_opportunities",0,"award_level",0,
            "debug_used",false,"settings",V.Map("sound",true,"lab_speed",1.0),"migration_paused",false,"knowledge",0.0,"stage",0,"goods_active",false,"goods_available",false,"quote_bank",V.Map(),"goods_requested_at",-1.0,"city_goal",V.Map(),"arrival_deferred",false);
        foreach(string id in Ids(c.operators))s["organizations"][id]=V.Map("id",id,"traits",c.operators[id]["traits"].Duplicate(),"labels",V.Arr());
        Block.ensure(this);Clock.ensure(this);Themes.ensure(this);Cycles.ensure(this);
        compiled=V.Map();log_event("new_game",V.Map("seed",seed_value),"河谷新章：安顿居民，建立供给，选择这座城的发展方向。");
    }
    public string uid(string prefix){string result=prefix+s["next_id"].Long.ToString("D6",CultureInfo.InvariantCulture);s["next_id"]=s["next_id"].Long+1;return result;}
    public void log_event(string kind,V payload=null,string text="") {
        payload=payload??V.Map();s["log"].Add(V.Map("tick",s["simulation_tick"],"h",s["local_hours"],"kind",kind,"data",payload.Duplicate(true),"text",text));
        if(s["log"].Count>1200)s["log"].Items.RemoveAt(0);Civic.@event(this,kind,payload,text);
    }
    public string select_block(int index) {
        if(s["phase"].Text!="select_block"||index<0||index>2)return "起点已确定。";
        s["block_index"]=index;s["cells"]=Hex.make_map(index,s["seed"].Long);s["block"]["geography"]=Hex.geography(s["cells"],s["seed"].Long,index);s["block"]["display_name"]=new[]{"河湾","林缘旧村","北部高台"}[index];
        foreach(string key in s["cells"].Keys) {
            var cell=s["cells"][key];var traits=V.Arr();string terrain=cell["terrain"].Text;
            if(terrain=="plain")traits.Add("tile.fertile");else if(terrain=="forest")traits.Add("tile.forest");else if(terrain=="hill")traits.Add("tile.highland");else if(terrain=="wet")traits.Add("tile.wet");else if(terrain=="water")traits.Add("tile.water");
            if(cell["waterfront"].Bool)traits.Add("tile.waterfront");
            cell["traits"]=traits;cell["labels"]=V.Arr();cell["fields"]=V.Map();cell["building"]="";
        }
        var shuffled=Rng.shuffled(s,"opening",Rules.ids(c.origins));s["origin_choices"]=V.Arr(shuffled.Take(2).ToArray());s["phase"]="origin";return "";
    }
    public string choose_origin(string id) {
        if(s["phase"].Text!="origin"||!s["origin_choices"].Contains(id))return "不是本次起源。";
        var origin=c.origins[id];s["origin"]=id;s["phase"]="play";s["currency"]=s["currency"].Double+origin["cash"].Double;
        _create_special("municipal","市政储备",false);_create_special("homeless","待安置住区",true);
        foreach(string resource in new[]{"food","material","goods"})s["projects"]["municipal"]["inventory"][resource]=c.balance["initial_stock"][resource].Double+origin.Get(resource,0).Double;
        string home=_create_starter("homestead",V.Arr("0,1"),"commons");
        _create_starter("workshop",V.Arr("0,0"),id=="charter"?"civic":"steel");
        Population.create(this,home,c.balance["initial_people"].Double);
        refresh_metrics();Progress.initialize(this);Civic.ensure(this);s["block"]["cycles"]["baseline"]=Cycles._snapshot(this);
        log_event("origin",V.Map("id",id),"18位居民已安顿。先看粮食和空房，再决定河谷的下一步。");return "";
    }
    public void _create_special(string id,string name,bool home) {
        var b=c.projects["homestead"]["stats"].Duplicate(true);foreach(string key in b.Keys)b[key]=0.0;
        b["efficiency"]=1.0;b["personnel_mult"]=1.0;b["exposure_multiplier"]=1.0;b["shortage_multiplier"]=1.0;b["population_maintenance"]=c.balance["population_maintenance"].Double;
        foreach(string r in new[]{"food","material","goods","energy"})b["population_"+r]=c.balance["population_"+r];
        foreach(string r in new[]{"food","material","goods"})b["storage."+r]=home?0.0:c.balance["municipal_capacity"][r].Double;
        b["housing"]=home?1000000.0:0.0;b["output.energy"]=home?0.0:c.balance["base_energy"].Double;
        s["projects"][id]=V.Map("id",id,"name",name,"def","__"+id,"tile","offmap:"+id,"site","","traits",home?V.Arr("building.residential"):V.Arr("building.warehouse"),"labels",V.Arr(),"operator_org","civic","special",true,"base_stats",b,"inventory",V.Map("food",0.0,"material",0.0,"goods",0.0),"status","running","paused",false,"job",V.Map(),"slots",V.Map(),"usage",1.0,"bottleneck","","next_heat_bonus",0.0,"next_heat_credit",0.0,"outputs_last",V.Map(),"history",V.Arr());
    }
    public string _create_starter(string def_id,V cells,string operator_org="commons")=>create_building(def_id,cells[0].Text,operator_org,true)["id"].Text;
    public V create_building(string def_id,string tile,string operator_org,bool ready=false) {
        var def=c.projects[def_id];string id=uid("B"),site=uid("S");
        var p=V.Map("id",id,"def",def_id,"tile",tile,"site",site,"traits",def["traits"].Duplicate(),"labels",V.Arr(),"operator_org",operator_org,"special",false,"inventory",V.Map("food",0.0,"material",0.0,"goods",0.0),"slots",V.Map(),"slot_types",V.Map(),"work_priority",0,"level",0,"status",ready?"running":"construction","paused",false,"job",V.Map(),"usage",ready?1.0:0.0,"bottleneck","尚未运行","next_heat_bonus",0.0,"next_heat_credit",0.0,"outputs_last",V.Map(),"history",V.Arr(),"effective_hours",0.0,"size_scale",1.0);
        if(!ready)p["job"]=V.Map("work",def["construction_required"].Double,"total",def["construction_required"].Double,"material",def["material"].Double,"energy",0.5,"cp",true);
        Development.ensure_project(this,p);s["projects"][id]=p;s["sites"][site]=V.Map("id",site,"cells",V.Arr(tile),"project_ids",V.Arr(id),"zone",def["zone"]);
        s["cells"][tile]["building"]=id;s["cells"][tile]["site"]=site;s["cells"][tile]["zone"]=def["zone"];
        if(ready)s["totals"]["built"]=s["totals"]["built"].Long+1;return p;
    }
    public V playable_ids(){var result=V.Arr();foreach(string id in Ids(s["projects"]))if(!s["projects"][id]["special"].Bool)result.Add(id);return result;}
    public string project_name(string id) {
        var p=s["projects"].Get(id,V.Map());if(p.Count==0){if(s["cells"].Has(id))return "地块 "+id;if(s["groups"].Has(id))return "人群 "+id;if(c.operators.Has(id))return c.operators[id]["name"].Text;if(id==s["block"]["id"].Text)return s["block"].Get("display_name","河谷地区").Text;return "已移除";}
        return p.Get("name",c.projects.Get(p["def"],V.Map()).Get("name",id)).Text;
    }
    public string create_tender(V cells,string @base)=>Draft.open_build(this,cells,@base);
    public string create_refit(string id,string slot)=>Draft.open_refit(this,id,slot);
    public string create_block_offer(string host_type="block",string host_id="block:1")=>Draft.open_scope(this,host_type,host_id);
    public string accept_offer(string offer_id)=>Draft.accept(this,offer_id);
    public string reroll()=>Draft.reroll(this);
    public string cancel_draft(){if(s["draft"].Count==0)return "";Draft.remember(this);s["draft"]=V.Map();return "";}
    public string bonus_eligible(string bonus_id,string host_type,string host_id,string slot="",string replacing="") {
        if(!c.bonuses.Has(bonus_id))return "未知方案。";var def=c.bonuses[bonus_id];if(!def["hosts"].Contains(host_type))return "该方案不能挂载于此尺度。";
        if(host_type=="building") {
            if(!s["projects"].Has(host_id)||s["projects"][host_id]["special"].Bool)return "无效建筑。";var p=s["projects"][host_id];
            if(slot=="")return "请选择一个已解锁的方案槽。";if(p["status"].Text!="running")return "建筑完工后才能部署方案。";
            if(!Rules.any_trait(Rules.semantic_traits(s,host_id),def["traits"]))return "基础建筑类型不适用。";
            var gate=def.Get("install_gate",def.Get("gate",V.Map()));if(!Rules.condition(s,host_id,gate))return "场地条件不满足。";
            if(gate.Get("has_inputs",false).Bool){bool any=false;foreach(string key in c.projects[p["def"]]["stats"].Keys)if(key.StartsWith("input.",StringComparison.Ordinal)&&c.projects[p["def"]]["stats"][key].Double>0)any=true;if(!any)return "本建筑没有常规原料配方。";}
            if(!p["slots"].Has(slot))return "插槽不存在。";
            if(slot_type(host_id,slot)=="Flex"){if(def["slot"].Text!="Production"&&def["slot"].Text!="Logistics")return "弹性槽只接受生产或物流方案。";}
            else if(!def["allowed_slots"].Contains(slot_type(host_id,slot)))return "方案与插槽类型不匹配。";
        } else {
            if(host_type=="organization"&&!s["organizations"].Has(host_id))return "未知运营商。";if(host_type=="block"&&host_id!=s["block"]["id"].Text)return "未知Block。";
            if(def["traits"].Count>0){bool matched=false;foreach(var id in playable_ids()){if(host_type=="organization"&&s["projects"][id]["operator_org"].Text!=host_id)continue;if(Rules.any_trait(Rules.semantic_traits(s,id.Text),def["traits"]))matched=true;}if(!matched)return "目前没有可受益的建筑。";}
        }
        string duplicate=Bonus.duplicate_error(this,bonus_id,host_type,host_id,replacing);if(duplicate!="")return duplicate;
        foreach(var ins in s["bonus_instances"].Values){if(ins["id"].Text==replacing||!ins["active"].Bool||ins["host_type"].Text!=host_type||ins["host_id"].Text!=host_id)continue;if(ins["bonus_id"].Text==bonus_id)continue;if(def["excludes"].Contains(ins["bonus_id"])||c.bonuses[ins["bonus_id"]]["excludes"].Contains(bonus_id))return "与当前方案互斥。";}
        return "";
    }
    public string mount_bonus(string bonus_id,string host_type,string host_id,string provider,string slot="")=>Bonus.install(this,bonus_id,host_type,host_id,slot);
    public string _install_bonus(string bonus_id,string host_type,string host_id,string provider,string slot="") {
        if(!s["organizations"].Has(provider))return "未知方案提供方。";string existing="";
        if(host_type=="building"&&s["projects"].Has(host_id))existing=s["projects"][host_id]["slots"].Get(slot,"").Text;
        string error=bonus_eligible(bonus_id,host_type,host_id,slot,existing);if(error!="")return error;
        if(host_type=="building"&&existing!="") {var p=s["projects"][host_id];var old=c.bonuses[s["bonus_instances"][existing]["bonus_id"]];foreach(var rule in old["rules"])if(rule["op"].Text=="slot_add"&&p["slots"].Get("Flex","").Text!="")return "请先清空弹性槽，再更换提供它的模块化方案。";_unmount(existing);}
        string iid=uid("I");s["bonus_instances"][iid]=V.Map("id",iid,"bonus_id",bonus_id,"host_type",host_type,"host_id",host_id,"provider_org",provider,"active",true,"slot",slot);
        if(host_type=="building") {var p=s["projects"][host_id];p["slots"][slot]=iid;foreach(var rule in c.bonuses[bonus_id]["rules"])if(rule["op"].Text=="slot_add"&&!p["slots"].Has(rule["name"])){p["slots"][rule["name"]]="";p["slot_types"][rule["name"]]="Flex";}}
        s["totals"]["installed"]=s["totals"]["installed"].Long+1;refresh_metrics();log_event("bonus",V.Map("instance",iid),c.bonuses[bonus_id]["name"].Text+"已挂载。");return "";
    }
    public void _unmount(string iid) {
        if(!s["bonus_instances"].Has(iid))return;var i=s["bonus_instances"][iid];i["active"]=false;
        if(i["host_type"].Text!="building"||!s["projects"].Has(i["host_id"]))return;var p=s["projects"][i["host_id"]];p["slots"][i["slot"]]="";
        foreach(var rule in c.bonuses[i["bonus_id"]]["rules"])if(rule["op"].Text=="slot_add") {bool retained=false;foreach(var other in s["bonus_instances"].Values){if(!other["active"].Bool||other["host_type"].Text!="building"||other["host_id"]!=i["host_id"])continue;foreach(var r in c.bonuses[other["bonus_id"]]["rules"])if(r["op"].Text=="slot_add"&&r["name"]==rule["name"])retained=true;}if(!retained){p["slots"].Erase(rule["name"]);p.Get("slot_types",V.Map()).Erase(rule["name"]);}}
    }
    public string clear_slot(string id,string slot) {
        if(s["phase"].Text!="play"||!s["projects"].Has(id)||!s["projects"][id]["slots"].Has(slot))return "当前不可拆卸。";string iid=s["projects"][id]["slots"][slot].Text;if(iid=="")return "";
        foreach(var r in c.bonuses[s["bonus_instances"][iid]["bonus_id"]]["rules"])if(r["op"].Text=="slot_add"&&s["projects"][id]["slots"].Get(r["name"],"").Text!="")return "请先清空依赖此方案的弹性槽。";
        _unmount(iid);refresh_metrics();Population.rehouse(this);return "";
    }
    public string switch_policy(string policy) {
        if(s["phase"].Text!="play"||!c.policies.Has(policy))return "当前不能更换政策。";var def=c.policies[policy];if(s["block"]["policies"].Get(def["slot"],"").Text==policy)return "";
        if(s["currency"].Double<c.balance["policy_switch_price"].Double)return "政策调整经费不足。";
        s["currency"]=s["currency"].Double-c.balance["policy_switch_price"].Double;s["block"]["policies"][def["slot"]]=policy;refresh_metrics();log_event("policy",V.Map("id",policy),"采用「"+def["name"].Text+"」。原政策失效，不发额外奖励。");return "";
    }
    public string set_paused(string id,bool value){if(s["phase"].Text!="play"||!s["projects"].Has(id)||s["projects"][id]["special"].Bool)return "不能操作这个建筑。";s["projects"][id]["paused"]=value;refresh_metrics();return "";}
    public string demolish(string id) {
        if(s["phase"].Text!="play"||!s["projects"].Has(id)||s["projects"][id]["special"].Bool)return "不能拆除此建筑。";if(s["currency"].Double<20)return "需要20通货处理拆除。";
        s["currency"]=s["currency"].Double-20;var p=s["projects"][id];foreach(var group in s["groups"].Values){if(group["host"].Text==id)group["host"]="homeless";group["employment"].Erase(id);}
        foreach(string iid in Ids(s["bonus_instances"]))if(s["bonus_instances"][iid]["host_type"].Text=="building"&&s["bonus_instances"][iid]["host_id"].Text==id)s["bonus_instances"][iid]["active"]=false;
        var moved=V.Map();var lost=V.Map();var warehouses=new List<string>{"municipal"};
        foreach(var bid in playable_ids()){var b=s["projects"][bid];if(bid.Text==id||b["status"].Text!="running"||b["paused"].Bool)continue;if(b["traits"].Contains("building.logistics")||compiled["config"][bid].Get("overflow_relay",false).Bool)warehouses.Add(bid.Text);}
        foreach(string resource in p["inventory"].Keys) {double remaining=p["inventory"][resource].Double,total=remaining;foreach(string warehouse in warehouses){double cap=stock_cap_for(warehouse,resource),old=s["projects"][warehouse]["inventory"].Get(resource,0).Double,amount=Math.Min(remaining,Math.Max(0,cap-old));s["projects"][warehouse]["inventory"][resource]=old+amount;remaining-=amount;}moved[resource]=total-remaining;lost[resource]=remaining;}
        var cell=s["cells"][p["tile"]];cell["building"]="";cell["site"]="";cell["zone"]="";cell["labels"].Add("former:"+p["def"].Text);
        foreach(string fid in Ids(s["authored_fields"]))if(s["authored_fields"][fid]["source_id"].Text==id)s["authored_fields"].Erase(fid);
        s["sites"].Erase(p["site"]);s["projects"].Erase(id);refresh_metrics();Population.rehouse(this);refresh_metrics();double waste=lost.Values.Sum(x=>x.Double);
        log_event("demolish",V.Map("id",id,"moved",moved,"lost",lost),"原住户保留并重新安置。库存已尝试转仓"+(waste>0.001?"；仓储不足，损耗"+waste.ToString("F1",CultureInfo.InvariantCulture)+"单位。":"，无库存损失。"));return "";
    }
    public void toggle_migration(){s["migration_paused"]=!s["migration_paused"].Bool;}
    public double inventory_total(string resource){double total=0;foreach(string id in Ids(s["projects"]))total+=s["projects"][id]["inventory"].Get(resource,0).Double;return total;}
    public double stock_cap_for(string id,string resource){if(compiled.Count==0)compiled=Rules.compile(this);return Sim.storage_cap(compiled,id,resource);}
    public double stock_cap(string resource){double total=0;foreach(string id in Ids(s["projects"]))total+=stock_cap_for(id,resource);return total;}
    public V project_stats(string id){if(compiled.Count==0)compiled=Rules.compile(this);return compiled["stats"].Get(id,V.Map());}
    public void refresh_metrics() {
        revision++;if(s["projects"].Count==0)return;foreach(var id in playable_ids())s["projects"][id]["labels"].Erase("use.commercial");compiled=Rules.compile(this);bool changed=false;
        foreach(var id in playable_ids())if(compiled["config"][id].Get("commercial_use",false).Bool){s["projects"][id]["labels"].Add("use.commercial");changed=true;}
        if(changed)compiled=Rules.compile(this);var m=s["metrics"].Duplicate(true);m.Merge(Population.summary(this),true);s["metrics"]=m;
    }
    public double temporal_load()=>1555200.0/c.beta["time"]["earth_seconds_per_dt"].Double;
    public V advance_active(double real_seconds)=>Clock.advance(this,real_seconds);
    public V pulse(double months)=>!is_num(V.From(months))||months<=0?V.Map("advanced",0.0,"spent",0.0,"reason","无效推进。"):Clock.advance(this,months*1555200/25920,"advance");
    public void advance_online(double hours){Clock.advance(this,hours*3600,"normal");}
    public void update_global(double now){s["global_now"]=Math.Max(s["global_now"].Double,now);}
    public double resume_time(double now)=>Clock.resume(this,now);
    public void debug_grant(){s["debug_used"]=true;s["currency"]=s["currency"].Double+500;s["dt"]=Math.Min(c.balance["dt_cap"].Double,s["dt"].Double+20);foreach(string r in new[]{"food","material","goods"})s["projects"]["municipal"]["inventory"][r]=Math.Min(stock_cap_for("municipal",r),s["projects"]["municipal"]["inventory"].Get(r,0).Double+20);log_event("debug",V.Map(),"使用调试补给；此局不是正常资源验收。");}
    public string define_field(string source_id,string kind,double strength,int radius,double decay,string origin="") {
        if(kind==""||!is_num(strength)||!is_num(decay)||radius<0)return "";
        if(!s["projects"].Has(source_id)&&!s["cells"].Has(source_id)&&!s["groups"].Has(source_id)&&!s["organizations"].Has(source_id)&&source_id!=s["block"]["id"].Text)return "";if(origin!=""&&!s["cells"].Has(origin))return "";
        string fid=uid("F");s["authored_fields"][fid]=V.Map("id",fid,"type",kind,"source_id",source_id,"origin",origin,"strength",strength,"propagation",V.Map("radius",radius,"decay",decay,"rule","authored"),"active",true);return fid;
    }
    public V slot_ids(string id)=>V.Arr(s["projects"][id]["slots"].Keys.Cast<object>().ToArray());
    public string slot_type(string id,string slot)=>s["projects"][id].Get("slot_types",V.Map()).Get(slot,slot.Split(':')[0]).Text;
    public string slot_label(string id,string slot){string t=slot_type(id,slot),label=Catalog.SLOT_NAMES.Get(t,t).Text;if(slot.Contains(":"))label+=" "+slot.Split(':')[1];return label;}
    public string operator_layout(string org)=>string.Join(" / ",c.operators[org].Get("slot_track",V.Arr("Production","Logistics","Operations")).Select(x=>V.Map("Production","生产","Logistics","物流","Operations","运营")[x].Text));
    public V cohesion(string id){var p=s["projects"][id];int occupied=0,own=0;foreach(string slot in p["slots"].Keys){string iid=p["slots"][slot].Text;if(iid=="")continue;occupied++;var ins=s["bonus_instances"][iid];if(ins.Get("contract_org",ins["provider_org"])==p["operator_org"])own++;}return V.Map("owned",own,"occupied",occupied,"total",p["slots"].Count,"active",own==p["slots"].Count&&own>=3);}
    public string set_priority(string id,int priority){if(s["phase"].Text!="play"||!s["projects"].Has(id)||s["projects"][id]["special"].Bool)return "无法调整。";s["projects"][id]["work_priority"]=Math.Min(1,Math.Max(-1,priority));refresh_metrics();return "";}
    public string recontract(string id,string slot){if(s["phase"].Text!="play"||!s["projects"].Has(id))return "无法重签。";string iid=s["projects"][id]["slots"].Get(slot,"").Text;if(iid=="")return "空槽无需重签。";var ins=s["bonus_instances"][iid];if(ins.Get("contract_org",ins["provider_org"])==s["projects"][id]["operator_org"])return "已是自营方案。";if(s["currency"].Double<c.balance["recontract_cost"].Double)return "重签通货不足。";s["currency"]=s["currency"].Double-c.balance["recontract_cost"].Double;ins["contract_org"]=s["projects"][id]["operator_org"];log_event("recontract",V.Map("instance",iid),"方案已重签；原提供方记录保留，能力不变。");refresh_metrics();return "";}
    public string reforge_slot(string id,string slot,string type)=>"槽位轨道随建筑成长解锁；常规改型已取消。";
    public string change_operator(string id,string org)=>"运营者变更仅由组织事件触发。";
    public bool withdraw_stock(string resource,double amount){if(amount<0||inventory_total(resource)<amount-0.000001)return false;double remaining=amount;foreach(string id in Ids(s["projects"])){double take=Math.Min(remaining,s["projects"][id]["inventory"].Get(resource,0).Double);s["projects"][id]["inventory"][resource]=s["projects"][id]["inventory"].Get(resource,0).Double-take;remaining-=take;}return true;}
    public double next_arrival(){int cursor=(int)s["event_cursor"].Long;return cursor>=c.balance["group_events_hours"].Count?-1:Math.Max(0,c.balance["group_events_hours"][cursor].Double-s["local_hours"].Double);}
    public string enable_goods(){if(s["goods_active"].Bool||s.Get("goods_requested_at",-1).Double>=0)return "";if(!s.Get("goods_available",false).Bool&&inventory_total("goods")<=0)return "先建立制成品供给。";s["goods_requested_at"]=s["local_hours"].Double;log_event("goods_plan",V.Map(),"生活用品计划已公布；2来生月后居民开始消费制成品。");refresh_metrics();return "";}
    public V next_checkpoint(){double hours=3;string name="观察城市运行";foreach(var id in playable_ids()){var p=s["projects"][id];if(p["status"].Text!="construction")continue;double h=p["job"]["work"].Double/Math.Max(0.05,Construction.capacity(this));if(h<hours){hours=h;name=project_name(id.Text)+"预计完工";}}foreach(var row in Construction.queue(this)){if(row["paused"].Bool)continue;double h=row["work"].Double/Math.Max(0.05,Construction.capacity(this));if(h<hours){hours=h;name=row["name"].Text+"预计完工";}}double arrival=next_arrival();if(!s["migration_paused"].Bool&&arrival>=0&&arrival<hours){hours=Math.Max(0.1,arrival);name="下一批居民抵达";}if(s.Get("goods_requested_at",-1).Double>=0&&!s["goods_active"].Bool){double remain=s["goods_requested_at"].Double+c.balance["goods_grace_hours"].Double-s["local_hours"].Double;if(remain>0&&remain<hours){hours=remain;name="生活用品需求生效";}}return V.Map("hours",Math.Floor(Math.Max(0.1,hours)/0.1+0.5)*0.1,"name",name,"cost",hours*temporal_load());}
    public V migrate_state(V value){if(value.Get("schema",0).Long!=6||value.Get("build","")!=c.data["build"]||value.Has("district"))return V.Map("ok",false,"error","这是其他 pre-alpha 版本的存档，请开始新游戏。");string error=validate_state(value);if(error!="")return V.Map("ok",false,"error",error);s=value.Duplicate(true);Clock.ensure(this);Themes.ensure(this);Cycles.ensure(this);s["beta_time"]["mode"]="normal";s["settings"]["user_paused"]=true;compiled=Rules.compile(this);revision++;return V.Map("ok",true,"state",s,"migrated",false);}
    public V snapshot()=>s.Duplicate(true);
}
}
