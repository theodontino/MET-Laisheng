using System;
using System.Linq;
namespace MET.Core {
/// <summary>Population groups preserve lineage and employment provenance through housing moves. Port of core/population.gd.</summary>
public static class Population {
    public static string create(Game g,string host,double count,V template=null) {
        if(template==null)template=V.Map();var id=g.uid("G");
        var p=V.Map("id",id,"count",count,"host",host,"health",78.0,"education",15.0,"happiness",73.0,"efficiency",1.0,"demand",1.0,"employment",V.Map(),"services",V.Map(),"living",V.Map(),"lineage",V.Arr(id));
        foreach(var key in new[]{"health","education","happiness","efficiency","demand"})if(template.Has(key))p[key]=template[key];
        g.s["groups"][id]=p;return id;
    }
    public static string home_group(V s,string host) {
        foreach(var gid in Rules.ids(s["groups"]))if(s["groups"][gid]["host"].Text==host)return gid.Text;return "";
    }
    public static void transfer(Game g,string gid,string target,double count) {
        if(!g.s["groups"].Has(gid)||count<=0)return;
        var source=g.s["groups"][gid];count=Math.Min(count,source["count"].Double);var fraction=count/Math.Max(.000001,source["count"].Double);var jobs=V.Map();
        foreach(var b in source["employment"].Keys)jobs[b]=source["employment"][b].Double*fraction;
        var recipient=target!="homeless"?home_group(g.s,target):"";
        if(target=="homeless")foreach(var candidate in Rules.ids(g.s["groups"])) {
            var other=g.s["groups"][candidate];if(candidate.Text==gid||other["host"].Text!="homeless")continue;
            var left=source["lineage"].Items.OrderBy(x=>x.Text,StringComparer.Ordinal);var right=other["lineage"].Items.OrderBy(x=>x.Text,StringComparer.Ordinal);
            if(left.SequenceEqual(right)) { recipient=candidate.Text;break; }
        }
        if(recipient==gid)return;
        if(count>=source["count"].Double-.000001&&recipient=="") { source["host"]=target;return; }
        if(recipient=="") { recipient=create(g,target,0,source);g.s["groups"][recipient]["lineage"]=source["lineage"].Duplicate(); }
        var dest=g.s["groups"][recipient];var old=dest["count"].Double;
        foreach(var key in new[]{"health","education","happiness","efficiency","demand"})dest[key]=(dest[key].Double*old+source[key].Double*count)/Math.Max(.000001,old+count);
        dest["count"]+=count;
        foreach(var b in jobs.Keys) { dest["employment"][b]=dest["employment"].Get(b,0).Double+jobs[b].Double;source["employment"][b]-=jobs[b]; }
        foreach(var origin in source["lineage"])if(!dest["lineage"].Contains(origin))dest["lineage"].Add(origin);
        source["count"]-=count;if(source["count"].Double<=.000001)g.s["groups"].Erase(gid);
    }
    public static void rehouse(Game g) {
        var stats=g.compiled["stats"];
        foreach(var bid in Rules.ids(g.s["projects"])) {
            var p=g.s["projects"][bid];if(p["special"].Bool)continue;
            var capacity=p["status"].Text=="running"?Math.Max(0,stats[bid]["housing"].Double*stats[bid]["efficiency"].Double):0;
            var gid=home_group(g.s,bid.Text);
            if(gid!=""&&g.s["groups"][gid]["count"].Double>capacity)transfer(g,gid,"homeless",g.s["groups"][gid]["count"].Double-capacity);
        }
        foreach(var gid in Rules.ids(g.s["groups"])) {
            if(!g.s["groups"].Has(gid)||g.s["groups"][gid]["host"].Text!="homeless")continue;
            foreach(var bid in Rules.ids(g.s["projects"])) {
                var p=g.s["projects"][bid];if(p["special"].Bool||p["status"].Text!="running")continue;
                var resident=home_group(g.s,bid.Text);var used=resident==""?0:g.s["groups"][resident]["count"].Double;
                var room=Math.Max(0,stats[bid]["housing"].Double*stats[bid]["efficiency"].Double-used);if(room<=0)continue;
                transfer(g,gid.Text,bid.Text,Math.Min(room,g.s["groups"][gid]["count"].Double));
                if(!g.s["groups"].Has(gid)||g.s["groups"][gid]["host"].Text!="homeless")break;
            }
        }
    }
    public static V summary(Game g) {
        double people=0,housed=0,health=0,happiness=0,education=0,employed=0,housing=0,jobs=0;
        foreach(var id in g.playable_ids()) {
            var p=g.s["projects"][id];var st=g.compiled["stats"][id];
            if(p["status"].Text=="running")housing+=Math.Max(0,st["housing"].Double*st["efficiency"].Double);
            if(p["status"].Text=="running"&&!p["paused"].Bool)jobs+=Math.Max(0,st["jobs"].Double);
        }
        foreach(var gid in Rules.ids(g.s["groups"])) {
            var group=g.s["groups"][gid];var n=group["count"].Double;people+=n;
            if(group["host"].Text!="homeless")housed+=n;
            health+=n*group["health"].Double;happiness+=n*group["happiness"].Double;education+=n*group["education"].Double;
            foreach(var j in group["employment"].Values)employed+=j.Double;
        }
        return V.Map("population",people,"housed",housed,"homeless",people-housed,"housing",housing,"vacancies",Math.Max(0,housing-housed),"workforce",people,"jobs",jobs,"employed",employed,"employment",Math.Min(1,employed/Math.Max(1,people)),"health",health/Math.Max(1,people),"happiness",happiness/Math.Max(1,people),"education",education/Math.Max(1,people));
    }
    static double clamp(double value,double lo,double hi)=>Math.Max(lo,Math.Min(hi,value));
    public static void update(Game g,V next,V c,V inputs,V service,V env,V labor,double h) {
        // Economic month response constants match the original integrator.
        var stage=(int)g.s["stage"].Long;
        foreach(var gid in Rules.ids(g.s["groups"])) {
            var old=g.s["groups"][gid];var p=next["groups"][gid];var host=g.s["projects"][old["host"]];var st=c["stats"][old["host"]];var n=old["count"].Double;
            p["employment"]=labor["employment"][gid].Duplicate(true);p["services"]=service["allocations"].Get(gid,V.Map()).Duplicate(true);
            var supplied=inputs.Get(gid,V.Map());var live=V.Map();
            foreach(var resource in new[]{"food","material","goods","energy"})live[resource]=supplied.Get(resource,1).Double;
            foreach(var kind in g.c.balance["service_kinds"])live[kind]=Math.Min(1,service["covered"].Get(gid,V.Map()).Get(kind,0).Double/Math.Max(.001,n));
            var pollution=Rules.aggregate(env["tiles"].Get(host["tile"],V.Map()).Get("pollution",V.Arr()),c["config"][old["host"]].Get("pollution_aggregate","sum").Text);
            var exposure=Math.Max(0,pollution)*st["exposure_multiplier"].Double;var unemployment=labor["unemployed"].Get(gid,0).Double/Math.Max(1,n);double med_happiness=0;
            foreach(var link in service["links"])if(link["group"]==gid&&link["kind"].Text=="medical"&&c["config"][link["source"]].Get("medical_happiness",true).Bool)med_happiness+=link["people"].Double/Math.Max(1,n);
            var foodlack=1-live["food"].Double;var materiallack=1-live["material"].Double;var energylack=1-live["energy"].Double;var goodslack=1-live["goods"].Double;
            var health_target=80.0-42*foodlack-6*materiallack-6*energylack-9*exposure+15*live["medical"].Double;
            var happiness_target=76.0-45*foodlack-8*materiallack-8*energylack-12*goodslack-12*unemployment-Math.Max(0,65-p["health"].Double)*.25-4*exposure;
            var service_ramp=clamp((g.s["local_hours"].Double-g.c.balance["service_start_hours"].Double)/g.c.balance["service_grace_hours"].Double,0,1);
            if(stage>=1)happiness_target+=service_ramp*st["shortage_multiplier"].Double*(10*live["commercial"].Double+12*live["entertainment"].Double+4*med_happiness-14);
            if(old["host"].Text=="homeless") { health_target-=12;happiness_target-=24; }
            p["health"]=clamp(old["health"].Double+(health_target-old["health"].Double)*(1-Math.Exp(-h*.22)),0,100);
            p["happiness"]=clamp(old["happiness"].Double+(happiness_target-old["happiness"].Double)*(1-Math.Exp(-h*.35)),0,100);
            var learning_rate=.05*live["education"].Double;
            if(learning_rate>0) {
                var education_target=100.0-.03/learning_rate;p["education"]=clamp(education_target+(old["education"].Double-education_target)*Math.Exp(-learning_rate*h),0,100);
            } else p["education"]=clamp(old["education"].Double-.03*h,0,100);
            p["efficiency"]=clamp((.6+.4*p["health"].Double/78.0)*(1.0+.002*(p["education"].Double-15.0)),.45,1.3);
            p["demand"]=clamp(1+(p["education"].Double-15)*.004,.85,1.4);
            live["pollution"]=pollution;live["unemployment"]=unemployment;live["health_target"]=clamp(health_target,0,100);live["happiness_target"]=clamp(happiness_target,0,100);p["living"]=live;
            next["knowledge"]+=service["covered"].Get(gid,V.Map()).Get("education",0).Double*.02*h;
        }
    }
}
}
