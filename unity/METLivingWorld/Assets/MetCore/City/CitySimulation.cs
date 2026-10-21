using System;
using System.Collections.Generic;
namespace MET.Core {
public sealed partial class CitySession {
    public void ApplyWorldEffects() {
        foreach(var pair in Games) {
            var g=pair.Value;var effects=V.Map("sources",V.Map(),"knowledge_multiplier",1.0,"freight_multiplier",1.0,"capabilities",V.Arr());
            string district=Block(pair.Key).Get("district_id","").Text;
            foreach(var id in Rules.ids(District(district).Get("institutions",V.Map()))) {
                var institution=District(district)["institutions"][id];string kind=institution["kind"].Text;var rules=V.Arr();
                if(kind=="agricultural")rules.Add(V.Map("id","food","op","modify","targets","self","field","output.food","mode","multiply","value",1.05,"when",V.Map("traits",V.Arr("building.agricultural"))));
                if(kind=="industrial")foreach(var r in new[]{"material","goods"})rules.Add(V.Map("id",r,"op","modify","targets","self","field","output."+r,"mode","multiply","value",1.05,"when",V.Map("traits",V.Arr("building.industrial"))));
                if(kind=="services")rules.Add(V.Map("id","services","op","modify","targets","self","field","service.*","mode","multiply","value",1.05));
                if(kind=="research")effects["knowledge_multiplier"]=1.05;
                if(kind=="logistics")effects["freight_multiplier"]=1.1;
                effects["sources"]["district:"+district+":"+id.Text]=V.Map("name",kind+"地区制度 · "+district,"record_id",institution.Get("record_id",""),"source_blocks",institution.Get("source_blocks",V.Arr()).Duplicate(true),"rules",rules);
            }
            var opportunity=Block(pair.Key).Get("start_sources",V.Map()).Get("opportunity",V.Map());
            if(opportunity.Count>0){effects["knowledge_multiplier"]=effects["knowledge_multiplier"].Double*opportunity.Get("knowledge_multiplier",1).Double;effects["sources"]["build:"+pair.Key]=V.Map("name",opportunity["name"],"record_id",opportunity["record_id"],"source_blocks",V.Arr(opportunity["source_block"]),"rules",opportunity["rules"].Duplicate(true));}
            foreach(var capability in s.Get("legacy",V.Map()).Get("capabilities",V.Map()).Values)effects["capabilities"].Add(capability.Duplicate(true));
            bool changed=!g.world_managed||g.world_effects!=effects;g.world_managed=true;g.world_effects=effects;
            if(changed&&g.s["projects"].Count>0)g.refresh_metrics();
        }
    }
    sealed class SliceGame {
        public V state,compiled,cache,effects;public int revision,committed;public bool managed,recording;public string error;
        public SliceGame(Game g){state=g.snapshot();compiled=g.compiled;cache=g.spatial_cache;effects=g.world_effects;revision=g.revision;committed=g.last_committed_revision;managed=g.world_managed;recording=g.recording_cycle;error=g.last_error;}
        public void Restore(Game g){g.s=state;g.compiled=compiled;g.spatial_cache=cache;g.world_effects=effects;g.revision=revision;g.last_committed_revision=committed;g.world_managed=managed;g.recording_cycle=recording;g.last_error=error;}
    }
    public V Advance(double externalSeconds,string mode="") {
        var result=V.Map("advanced",0.0,"local_seconds",0.0,"external_seconds",0.0,"active_real_seconds",0.0,"spent",0.0,"steps",0,"local_months",V.Map(),"reason","");
        if(!WorldNumber(externalSeconds)||externalSeconds<=0||!V.Arr("play","choose_district").Contains(s["phase"])){result["reason"]="当前城市不能推进。";return result;}
        if(s.Get("paused",true).Bool){result["reason"]="城市已暂停。";return result;}
        if(mode!=""&&CanEditFocused&&FocusedGame.s["phase"].Text=="play"){string error=Clock.set_mode(FocusedGame,mode);if(error!=""){result["reason"]=error;return result;}}
        double remaining=externalSeconds,month=c.beta["units"]["earth_seconds_per_month"].Double,dtSeconds=c.beta["time"]["earth_seconds_per_dt"].Double;
        while(remaining>0&&!s.Get("paused",false).Bool) {
            var before=s.Duplicate(true);var states=new Dictionary<string,SliceGame>(StringComparer.Ordinal);foreach(var pair in Games)states[pair.Key]=new SliceGame(pair.Value);
            var speeds=V.Map();var months=V.Map();double slice=remaining;string failure="";
            try {
                EnsureNetwork();ApplyWorldEffects();ConfigureScienceContext();
                bool hasLinks=false;foreach(var link in s["links"].Values)if(link["enabled"].Bool||Inflight(link)>0){hasLinks=true;break;}
                if(hasLinks) {
                    slice=Math.Min(slice,3600.0);
                    foreach(var link in s["links"].Values)foreach(var cargo in link["inflight"]) {
                        double due=cargo["arrival_external_seconds"].Double-s.Get("external_seconds",0).Double;
                        if(due>1e-9)slice=Math.Min(slice,due);
                    }
                }
                foreach(var id in Rules.ids(s["blocks"])) {
                    var g=Games[id.Text];if(g.s["phase"].Text!="play")continue;Clock.ensure(g);
                    double speed=id.Text==FocusedBlockId&&CanEditFocused?Clock.acceleration(g):1;
                    if(g.s["dt"].Double<=0){speed=1;g.s["beta_time"]["mode"]="normal";}
                    speeds[id]=speed;slice=Math.Min(slice,Clock._boundary_seconds(g,remaining*speed)/speed);
                    if(speed>1)slice=Math.Min(slice,g.s["dt"].Double*dtSeconds/(speed-1));
                }
                if(speeds.Count==0){result["reason"]="请完成街区起源选择。";break;}
                if(slice<=0||!WorldNumber(slice))throw new InvalidOperationException("共享时间边界无效，本段未执行。");
                foreach(var id in Rules.ids(speeds)) {
                    var g=Games[id.Text];double local=slice*speeds[id].Double;months[id]=local/month;
                    failure=Sim.tick(g,months[id].Double,V.Map("local_seconds",local,"external_seconds",slice,"acceleration",speeds[id]));if(failure!="")throw new InvalidOperationException(failure);
                }
                SettleNetwork(slice);
                foreach(var id in Rules.ids(speeds)) {
                    var g=Games[id.Text];var old=states[id.Text].state;double local=slice*speeds[id].Double,spent=(local-slice)/dtSeconds;
                    if(spent>=old["dt"].Double-1e-10&&speeds[id].Double>1)spent=old["dt"].Double;
                    g.s["beta_time"]["local_elapsed_seconds"]=old["beta_time"]["local_elapsed_seconds"].Double+local;
                    g.s["local_hours"]=g.s["beta_time"]["local_elapsed_seconds"].Double/month;
                    g.s["beta_time"]["active_real_seconds"]=old["beta_time"]["active_real_seconds"].Double+slice;
                    g.s["beta_time"]["segments_committed"]=old["beta_time"]["segments_committed"].Long+1;
                    Themes.tick(g,local);Cycles.tick(g,local,old["metrics"].Get("housed",0).Double);
                    g.s["dt"]=Math.Max(0,old["dt"].Double-spent);g.s["totals"]["dt_used"]=g.s["totals"].Get("dt_used",0).Double+spent;
                    if(old["dt"].Double>0&&g.s["dt"].Double==0)MET.Core.Block.on_dt_zero(g);
                    if(id.Text!=FocusedBlockId||!CanEditFocused){g.s["settings"]["user_paused"]=false;g.s["beta_time"]["pause_reason"]="";g.s["beta_time"]["mode"]="normal";}
                    Civic.finalize(g);g.last_committed_revision=g.revision;
                }
                s["external_seconds"]=before.Get("external_seconds",0).Double+slice;
                ScienceTick(months,slice);GovernanceTick(months,slice);CaptureHistory();
                failure=ValidateNetwork();if(failure!="")throw new InvalidOperationException(failure);
                failure=Validate();if(failure!="")throw new InvalidOperationException(failure);
                if(CanEditFocused&&FocusedGame.s["settings"]["user_paused"].Bool){s["paused"]=true;result["reason"]=FocusedGame.s["beta_time"]["pause_reason"];}
            } catch(Exception error) {
                s=before;foreach(var pair in states)pair.Value.Restore(Games[pair.Key]);result["reason"]=error.Message;break;
            }
            result["steps"]=result["steps"].Long+1;result["external_seconds"]=result["external_seconds"].Double+slice;result["active_real_seconds"]=result["external_seconds"];
            foreach(var id in months.Keys)result["local_months"][id]=result["local_months"].Get(id,0).Double+months[id].Double;
            if(months.Has(FocusedBlockId)){result["local_seconds"]=result["local_seconds"].Double+months[FocusedBlockId].Double*month;result["spent"]=result["spent"].Double+Math.Max(0,states[FocusedBlockId].state["dt"].Double-FocusedGame.s["dt"].Double);}
            remaining=Math.Max(0,remaining-slice);if(remaining<=externalSeconds*1e-14)remaining=0;
        }
        result["advanced"]=result["local_seconds"].Double/month;return result;
    }
    public string ValidateNetwork()=>ValidateWorldEnvelope(s);
    public static string ValidateWorldEnvelope(V s) {
        foreach(var link in s.Get("links",V.Map()).Values) {
            if(!s["blocks"].Has(link.Get("from","").Text)||!s["blocks"].Has(link.Get("to","").Text))return "物流连接归属不存在。";
            foreach(var key in new[]{"rate","cost","travel_seconds","buffer_capacity","buffer","dispatched","delivered","fees"})if(!link.Get(key).IsNumber||!WorldNumber(link[key].Double)||link[key].Double<0)return "物流数值损坏："+key;
            if(Array.IndexOf(WorldResources,link.Get("resource","").Text)<0||!link.Get("enabled").IsBool)return "物流资源或启用状态损坏。";
            if(!link.Get("inflight").IsArray)return "物流在途数据损坏。";
            foreach(var cargo in link["inflight"])if(!cargo.Get("amount").IsNumber||!WorldNumber(cargo["amount"].Double)||cargo["amount"].Double<0||!WorldNumber(cargo.Get("arrival_external_seconds",-1).Double)||cargo.Get("arrival_external_seconds",-1).Double<0)return "物流货物损坏。";
            if(Inflight(link)>link["buffer_capacity"].Double+1e-6||Math.Abs(link["dispatched"].Double-link["delivered"].Double-Inflight(link))>1e-5)return "物流货物守恒失败。";
        }
        foreach(var row in s.Get("world_market",V.Map()).Get("resources",V.Map()).Values)foreach(var key in new[]{"stock","capacity","replenishment_per_hour"})if(!row.Get(key).IsNumber||!WorldNumber(row[key].Double)||row[key].Double<0)return "外部库存数据损坏。";
        var market=s.Get("world_market",V.Map());
        if(market.Count>0) {
            if(!market.Get("capacity_per_hour").IsNumber||!WorldNumber(market["capacity_per_hour"].Double)||market["capacity_per_hour"].Double<0||!market.Get("currency").IsNumber||!WorldNumber(market["currency"].Double)||market["currency"].Double< -1e-6)return "外部市场结算资金或运力损坏。";
            foreach(var r in new[]{"food","material","goods"}) {
                var row=market["resources"].Get(r,V.Map());if(row.Count==0||row["stock"].Double>row["capacity"].Double+1e-6||!row.Get("pricing").IsMap)return "外部市场资源或库容损坏。";
                foreach(var n in row["pricing"].Values)if(n.IsNumber&&!WorldNumber(n.Double))return "外部市场报价损坏。";
            }
        }
        return "";
    }
}
}
