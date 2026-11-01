using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
namespace MET.Core {
/// <summary>Owns the permanent city. Games[id].s is the sole live local state.</summary>
public sealed partial class CitySession {
    public Catalog c;
    public V s=V.Map();
    public readonly Dictionary<string,Game> Games=new Dictionary<string,Game>(StringComparer.Ordinal);
    public readonly string storageRoot;
    readonly V arc;
    Game bootstrap;
    public CitySession(Catalog c,string storageRoot="") {
        this.c=c;this.storageRoot=storageRoot;
        arc=V.Parse(File.ReadAllText(Path.Combine(c.contentRoot,"beta/city_arc.json")));
        New(1,0);
    }
    public string FocusedBlockId=>s.Get("focused_block_id","").Text;
    public string ActiveDistrictId=>s.Get("active_district_id","").Text;
    public Game FocusedGame=>Games.TryGetValue(FocusedBlockId,out var g)?g:bootstrap;
    public bool CanEditFocused=>FocusedBlockId!=""&&ActiveDistrictId!=""&&Block(FocusedBlockId).Get("district_id","").Text==ActiveDistrictId;
    public V Block(string id)=>s.Get("blocks",V.Map()).Get(id,V.Map());
    public V District(string id)=>s.Get("districts",V.Map()).Get(id,V.Map());
    public string Uid(string prefix){long id=s["next_id"].Long;s["next_id"]=id+1;return prefix+id.ToString("D6",CultureInfo.InvariantCulture);}
    public void New(long seed,double utc,string name="来生") {
        Games.Clear();bootstrap=new Game(c);bootstrap.new_game(seed,utc);
        s=V.Map("schema",1,"build",c.data["build"],"city_id","city:"+seed.ToString(CultureInfo.InvariantCulture),"name",name,"seed",seed,"next_id",1,"phase","choose_district","paused",true,"active_district_id","","focused_block_id","","external_seconds",0.0,"last_seen_utc",utc,"offline_seconds",0.0,"treasury",0.0,
            "districts",V.Map(),"blocks",V.Map(),"links",V.Map(),"legacy",V.Map("capabilities",V.Map(),"portable_transfers",V.Arr(),"build_results",V.Map()),"science",V.Map(),"projects",V.Map(),"history",V.Arr(),"receipts",V.Map());
        WorldEnsure();ScienceEnsure();
        Record("city_founded",V.Map("name",name,"seed",seed),"city:founded");
    }
    // Block metadata belongs to the city; each local state has one live owner in
    // Games. Copy metadata separately so restoring an envelope never copies all
    // local states a second time merely to discard them from city metadata.
    static V MetadataSnapshot(V source) {
        var result=V.Map();foreach(var pair in source.Pairs)if(pair.Key!="blocks")result[pair.Key]=pair.Value.Duplicate(true);
        var blocks=V.Map();foreach(var block in source["blocks"].Pairs){var metadata=V.Map();foreach(var field in block.Value.Pairs)if(field.Key!="state")metadata[field.Key]=field.Value.Duplicate(true);blocks[block.Key]=metadata;}result["blocks"]=blocks;return result;
    }
    public V snapshot(){var result=MetadataSnapshot(s);foreach(var pair in Games)result["blocks"][pair.Key]["state"]=pair.Value.snapshot();return result;}
    public string BeginDistrict(int landing,string specialization,string name="")=>BeginDistrict(landing,specialization,name,"");
    public string BeginDistrict(int landing,string specialization,string name,string sponsor,string opportunityId="") {
        if(ActiveDistrictId!="")return "先完成交班；同一城市只能有一个在任地区。";
        string invalid=CheckLanding(landing,specialization);if(invalid!="")return invalid;invalid=CheckOpportunity(specialization,opportunityId);if(invalid!="")return invalid;
        if(sponsor!=""&&(!Games.ContainsKey(sponsor)||!CanFund(sponsor,true,specialization,opportunityId)))return "所选供给街区库存或财政不足。";
        if(sponsor=="")sponsor=Games.Keys.OrderBy(x=>x,StringComparer.Ordinal).FirstOrDefault(id=>Games[id].s["phase"].Text=="play"&&CanFund(id,true,specialization,opportunityId))??"";
        if(Games.Count>0&&sponsor=="")return "旧城库存或财政不足，无法支付新地区启动包。";
        string did=Uid("D");var d=V.Map("id",did,"name",name==""?"地区 "+(s["districts"].Count+1):name,"status","active","run_history",V.Arr(),"block_ids",V.Arr(),"support",75.0,"mandate",75.0,"institutions",V.Map(),"crisis",V.Map("active",false,"reason","","bad_months",0.0),"term",1,"term_started_external",s["external_seconds"],"promises",V.Map("stable_blocks",arc["governance"]["handover_blocks"]),"identity",V.Map());
        s["districts"][did]=d;s["active_district_id"]=did;s["phase"]="play";
        CreateBlock(landing,specialization,sponsor,true,opportunityId);Record("district_started",V.Map("district_id",did,"sponsor",sponsor),did+":term:1:start");return "";
    }
    public string BeginBlock(int landing,string specialization,string sponsor,string opportunityId="") {
        if(ActiveDistrictId=="")return "请先选择新地区或回任旧地区。";
        string invalid=CheckLanding(landing,specialization);if(invalid!="")return invalid;invalid=CheckOpportunity(specialization,opportunityId);if(invalid!="")return invalid;
        if(!Games.ContainsKey(sponsor)||Block(sponsor)["district_id"].Text!=ActiveDistrictId)return "请选择本地区真实经营的供给街区。";
        if(!CanFund(sponsor,false,specialization,opportunityId))return "供给街区库存或财政不足，无法支付启动包。";
        if(Games.Values.Any(g=>g.s["phase"].Text=="origin"))return "请先完成已建立街区的起源选择。";
        CreateBlock(landing,specialization,sponsor,false,opportunityId);return "";
    }
    string CheckLanding(int landing,string specialization)=>landing<0||landing>2?"请选择三个实际登陆地点之一。":!V.Arr("balanced","agricultural","industrial","services","research","logistics").Contains(specialization)?"未知街区专业方向。":"";
    V Startup(bool district,string specialization,string opportunityId="") {
        var p=arc["startup"];double discount=Math.Min(arc["legacy"]["startup_discount_cap"].Double,s["legacy"]["capabilities"].Count*arc["legacy"]["startup_discount_per_capability"].Double);
        var stock=V.Map("food",p["food"].Double,"material",p["material"].Double,"goods",p["goods"].Double);
        // Existing capabilities change the required investment; no new stock is created.
        if(specialization=="industrial")stock["material"]+=p["industrial_material_bonus"];
        if(specialization=="agricultural")stock["food"]+=p["agriculture_food_bonus"];
        if(ActiveDistrictId!="")foreach(var institution in District(ActiveDistrictId)["institutions"].Values){if(institution["kind"].Text=="agricultural")stock["food"]=stock["food"].Double*.9;if(institution["kind"].Text=="industrial")stock["material"]=stock["material"].Double*.9;}
        var quote=V.Map("money",(p["money"].Double+(district?p["district_surcharge"].Double:0))*(1-discount),"stock",stock);
        if(opportunityId!=""){var extra=Opportunity(opportunityId)["cost"];quote["money"]+=extra["money"];foreach(var item in extra["stock"].Pairs)stock[item.Key]=stock.Get(item.Key,0).Double+item.Value.Double;}return quote;
    }
    public V StartupView(bool district,string specialization,string sponsor,string opportunityId="") {
        string invalid=CheckLanding(0,specialization);if(invalid=="")invalid=CheckOpportunity(specialization,opportunityId);if(invalid!="")return V.Map("quote",V.Map(),"can_pay",false,"reason",invalid,"sponsor",sponsor);
        if(district&&Games.Count==0)return V.Map("quote",V.Map("money",0.0,"stock",V.Map()),"can_pay",true,"reason","初始地区由原有城市创立资助。","sponsor","","initial_charter",true);
        var quote=Startup(district,specialization,opportunityId);bool exists=Games.ContainsKey(sponsor);bool belongs=district||exists&&Block(sponsor)["district_id"].Text==ActiveDistrictId;
        bool can=exists&&belongs&&CanFund(sponsor,district,specialization,opportunityId);var available=V.Map();if(exists)foreach(var resource in quote["stock"].Keys)available[resource]=Games[sponsor].inventory_total(resource);
        return V.Map("quote",quote,"can_pay",can,"reason",!exists?"请选择真实供给街区。":!belongs?"新街区需由本地区供给。":can?"启动包将从供给街区真实扣除。":"供给街区库存或财政不足。","sponsor",sponsor,"currency",exists?Games[sponsor].s["currency"]:V.From(0),"available",available,"initial_charter",false,"opportunity",opportunityId==""?V.Map():Opportunity(opportunityId));
    }
    bool CanFund(string sponsor,bool district,string specialization,string opportunityId="") {
        var g=Games[sponsor];if(g.s["phase"].Text!="play")return false;var cost=Startup(district,specialization,opportunityId);
        return g.s["currency"].Double>=cost["money"].Double&&cost["stock"].Pairs.All(p=>g.inventory_total(p.Key)+.000001>=p.Value.Double);
    }
    void CreateBlock(int landing,string specialization,string sponsor,bool district,string opportunityId="") {
        string bid=Uid("K");var g=new Game(c,storageRoot);g.new_game(s["seed"].Long+97*s["next_id"].Long,s["last_seen_utc"].Double);g.select_block(landing);g.s["block"]["city_context"]=true;g.world_managed=true;
        V source=V.Map("kind",sponsor==""?"initial_charter":"paid_transfer","source_block",sponsor,"district_start",district,"settled",sponsor=="","cost",sponsor==""?V.Map():Startup(district,specialization,opportunityId));
        if(opportunityId!="")source["opportunity"]=Opportunity(opportunityId);
        if(sponsor!=""){var old=Games[sponsor];var cost=source["cost"];old.s["currency"]=old.s["currency"].Double-cost["money"].Double;foreach(var item in cost["stock"].Pairs)old.withdraw_stock(item.Key,item.Value.Double);old.refresh_metrics();}
        var meta=V.Map("id",bid,"district_id",ActiveDistrictId,"specialization",specialization,"status","building","start_sources",source,"created_external",s["external_seconds"],"history_cursor",0,"observed_projects",V.Map(),"observed_rewards",V.Map(),"observed_population_band",0,"identity",V.Map());
        Games[bid]=g;s["blocks"][bid]=meta;District(ActiveDistrictId)["block_ids"].Add(bid);s["focused_block_id"]=bid;ScienceEnsure();ApplyWorldEffects();
        Record("block_started",V.Map("block_id",bid,"district_id",ActiveDistrictId,"landing",landing,"specialization",specialization,"source",source),bid+":started");
    }
    public string FocusBlock(string id){if(!Games.ContainsKey(id))return "街区不存在。";s["focused_block_id"]=id;return "";}
    public void AfterLocalCommand(V before) {
        if(!Games.ContainsKey(FocusedBlockId))return;var g=FocusedGame;var source=Block(FocusedBlockId)["start_sources"];
        if(g.s["phase"].Text=="play"&&!source["settled"].Bool) {
            // choose_origin supplies the starter entities; replace its cash/stock by the paid transfer exactly once.
            var cost=source["cost"];g.s["currency"]=cost["money"];
            foreach(var project in g.s["projects"].Values)foreach(string r in project["inventory"].Keys)project["inventory"][r]=0.0;
            foreach(var item in cost["stock"].Pairs)g.s["projects"]["municipal"]["inventory"][item.Key]=item.Value;
            source["settled"]=true;g.refresh_metrics();Record("startup_delivered",V.Map("block_id",FocusedBlockId,"source_block",source["source_block"],"cost",cost),FocusedBlockId+":startup_delivered");
        }
        if(s.Get("wild_mode",false).Bool&&g.s.Get("phase","").Text=="play"&&!WildDesign.Enabled(g))EnableWild();
        CaptureHistory();ApplyWorldEffects();
    }
    public double Resume(double utc){s["paused"]=true;double elapsed=Math.Max(0,utc-s["last_seen_utc"].Double);s["last_seen_utc"]=Math.Max(utc,s["last_seen_utc"].Double);s["offline_seconds"]=s["offline_seconds"].Double+elapsed;foreach(var g in Games.Values)g.resume_time(utc);return elapsed;}
    public string Restore(V state) {
        string error=ValidateEnvelope(c,state);if(error!="")return error;
        var replacement=new Dictionary<string,Game>(StringComparer.Ordinal);foreach(var pair in state["blocks"].Pairs){var g=new Game(c,storageRoot);g.s=pair.Value["state"].Duplicate(true);g.s["block"]["city_context"]=true;g.world_managed=true;g.compiled=g.s["projects"].Count>0?Rules.compile(g):V.Map();replacement[pair.Key]=g;}
        s=MetadataSnapshot(state);if(!s["legacy"].Has("build_results"))s["legacy"]["build_results"]=V.Map();Games.Clear();foreach(var pair in replacement)Games[pair.Key]=pair.Value;
        bootstrap=new Game(c);bootstrap.new_game(s["seed"].Long,s["last_seen_utc"].Double);WorldEnsure();ScienceEnsure();MigrateWildReward();ApplyWorldEffects();return "";
    }
    public string Save()=>CityStore.write(this,CityStore.path(storageRoot));
    public string Load(){var result=CityStore.read(c,CityStore.path(storageRoot));return result["ok"].Bool?Restore(result["state"]):result["error"].Text;}
    public string Validate()=>ValidateEnvelope(c,snapshot());
}
}
