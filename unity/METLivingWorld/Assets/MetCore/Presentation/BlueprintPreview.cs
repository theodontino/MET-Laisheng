using System;
using System.Linq;
namespace MET.Core {
/// <summary>Detached, inventory-aware Bonus command forecast. No command is applied to the caller.</summary>
public static class BlueprintPreview {
    static V Pair(double before,double after)=>V.Map("before",before,"after",after);
    static int Copies(Game g,string bid)=>(int)g.s.Get("block",V.Map()).Get("bonus_wallet",V.Map()).Get(bid,V.Map()).Get("charges",0).Long;
    static V Instances(Game g,string bid,string type,string host) {
        var rows=V.Arr();
        foreach(var id in Rules.ids(g.s.Get("bonus_instances",V.Map()))) {
            var instance=g.s["bonus_instances"][id];
            if(instance.Get("active",false).Bool&&instance.Get("bonus_id","").Text==bid&&instance.Get("host_type","").Text==type&&instance.Get("host_id","").Text==host)rows.Add(instance.Duplicate(true));
        }
        return rows;
    }
    static V Stats(Game g,string type,string host)=>type=="building"?g.compiled.Get("stats",V.Map()).Get(host,V.Map()).Duplicate(true):V.Map();
    static V Trace(Game g,string type,string host)=>type=="building"?g.compiled.Get("trace",V.Map()).Get(host,V.Map()).Duplicate(true):V.Map();
    static double Served(Game g,string kind) {
        double total=0;foreach(var home in g.s["metrics"].Get("service_covered",V.Map()).Values)total+=home.Get(kind,0).Double;return total;
    }
    static double Capacity(Game g,string kind) {
        double total=0;foreach(var pool in g.s["metrics"].Get("service_pools",V.Map()).Values)if(pool.Get("kind","").Text==kind)total+=pool.Get("capacity",0).Double;return total;
    }
    static double Output(Game g,string resource,double step)=>g.s.Get("audit",V.Map()).Get("resources",V.Map()).Get(resource,V.Map()).Get("produced",0).Double/step;
    public static V For(Game g,string bid,string hostType,string hostId,string slot="") {
        if(g==null)return V.Map("ok",false,"error","城市尚未就绪。");
        var planner=new Planning();var token=planner.version(g);var before=planner.copy_game(g);var after=planner.copy_game(g);
        var beforeStats=Stats(before,hostType,hostId);var beforeTrace=Trace(before,hostType,hostId);var beforeInstances=Instances(before,bid,hostType,hostId);
        var result=V.Map("ok",false,"error","","bonus_id",bid,"host_type",hostType,"host_id",hostId,"slot",slot,"token",token,"version",token,
            "before_stats",beforeStats,"after_stats",V.Map(),"stat_rows",V.Arr(),"before_instances",beforeInstances.Count,"after_instances",beforeInstances.Count,
            "before_instance_records",beforeInstances,"after_instance_records",beforeInstances.Duplicate(true),"before_copies",Copies(before,bid),"after_copies",Copies(before,bid),
            "rule_trace",V.Map("before",beforeTrace,"after",V.Map()),"baseline",V.Map(),"proposal",V.Map(),"same_step",V.Map());
        string error=Bonus.install(after,bid,hostType,hostId,slot);
        if(error!=""){result["error"]=error;return result;}
        // Capture immediate rule compilation separately from subsequent operation and shortage effects.
        var afterStats=Stats(after,hostType,hostId);var afterInstances=Instances(after,bid,hostType,hostId);var rows=V.Arr();
        foreach(string field in beforeStats.Keys.Union(afterStats.Keys).OrderBy(x=>x,StringComparer.Ordinal)) {
            var a=beforeStats.Get(field,0);var b=afterStats.Get(field,0);
            if((a.IsNumber||a.IsInteger)&&(b.IsNumber||b.IsInteger)&&a.Double!=b.Double)rows.Add(V.Map("field",field,"before",a,"after",b));
        }
        result["after_stats"]=afterStats;result["stat_rows"]=rows;result["after_instances"]=afterInstances.Count;result["after_instance_records"]=afterInstances;result["after_copies"]=Copies(after,bid);
        result["rule_trace"]["after"]=Trace(after,hostType,hostId);
        double step=g.c.balance["tick_hours"].Double;
        if(step<=0||double.IsInfinity(step)||double.IsNaN(step)){result["error"]="模拟步长无效。";return result;}
        error=Sim.tick(before,step);if(error!=""){result["error"]="基线预测失败："+error;return result;}
        error=Sim.tick(after,step);if(error!=""){result["error"]="安装预测失败："+error;return result;}
        var bm=before.s["metrics"];var am=after.s["metrics"];var services=V.Map();var capacities=V.Map();var outputs=V.Map();
        foreach(var kind in g.c.balance["service_kinds"]){services[kind]=Pair(Served(before,kind.Text),Served(after,kind.Text));capacities[kind]=Pair(Capacity(before,kind.Text),Capacity(after,kind.Text));}
        foreach(string resource in before.s.Get("audit",V.Map()).Get("resources",V.Map()).Keys.Union(after.s.Get("audit",V.Map()).Get("resources",V.Map()).Keys).OrderBy(x=>x,StringComparer.Ordinal))outputs[resource]=Pair(Output(before,resource,step),Output(after,resource,step));
        result["same_step"]=V.Map("months",step,"cashflow",Pair(bm.Get("cashflow",0).Double,am.Get("cashflow",0).Double),
            "energy_supply",Pair(bm.Get("energy",0).Double,am.Get("energy",0).Double),"energy_need",Pair(bm.Get("energy_need",0).Double,am.Get("energy_need",0).Double),
            "services",services,"service_capacity",capacities,"resource_outputs",outputs,"source_phase","next_tick");
        result["baseline"]=planner.display_copy(before).Duplicate(true);result["proposal"]=planner.display_copy(after).Duplicate(true);result["ok"]=true;
        return result;
    }
}
}
