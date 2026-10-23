using System;
namespace MET.Core {
/// <summary>Collect, deterministically resolve, then settle usage receipts. Port of core/allocation.gd.</summary>
public static class Alloc {
    static int text(V a,V b)=>StringComparer.Ordinal.Compare(a.Text,b.Text);
    public static V labor(V s,V c,V access) {
        var people_left=V.Map();var jobs_left=V.Map();var jobs=V.Map();var effective=V.Map();var personnel=V.Map();var links=V.Arr();var offers=V.Arr();
        foreach(var gid in Rules.ids(s["groups"])) {
            var group=s["groups"][gid];people_left[gid]=group["count"].Double;jobs[gid]=V.Map();
            var host=group["host"];var cf=c["config"].Get(host,V.Map());var pe=group["efficiency"].Double;
            if(cf.Has("volunteer_education")&&group["education"].Double>=cf["volunteer_education"]["threshold"].Double)pe*=cf["volunteer_education"]["labor_mult"].Double;
            personnel[gid]=pe;
        }
        foreach(var id in Rules.ids(s["projects"])) {
            var p=s["projects"][id];var st=c["stats"][id];jobs_left[id]=Math.Max(0,st["jobs"].Double);effective[id]=0.0;
            if(p["status"].Text!="running"||p["paused"].Bool||p.Get("special",false).Bool) { jobs_left[id]=0;continue; }
            foreach(var gid in Rules.ids(s["groups"])) {
                var group=s["groups"][gid];var home=s["projects"][group["host"]];
                var reachable=home.Get("special",false).Bool?.05:Fields.source_value(access["tiles"],home["tile"].Text,"employment",id.Text);
                var hc=c["config"][group["host"]];
                if(hc.Get("local_employment",false).Bool&&home["id"]==id)reachable+=2;
                if(hc.Get("neighbor_employment",false).Bool&&Rules.near(s,home["id"].Text,id.Text)==1)reachable+=.25;
                if(hc.Get("family_employment",false).Bool&&home["operator_org"]==p["operator_org"]&&p["traits"].Contains("building.industrial")&&reachable>0)reachable+=.4;
                if(reachable<=0)continue;
                var score=c["options"].Get("labor_mode","access").Text=="merit"?personnel[gid].Double:reachable;
                offers.Add(V.Map("group",gid,"building",id,"score",score,"access",reachable,"existing",group["employment"].Get(id,0).Double>0,"priority",p.Get("work_priority",0).Long));
            }
        }
        offers.Items.Sort((a,b)=> {
            if(a["priority"]!=b["priority"])return b["priority"].Long.CompareTo(a["priority"].Long);
            if(a["score"]!=b["score"])return b["score"].Double.CompareTo(a["score"].Double);
            if(a["access"]!=b["access"])return b["access"].Double.CompareTo(a["access"].Double);
            if(a["existing"]!=b["existing"])return b["existing"].Bool.CompareTo(a["existing"].Bool);
            if(a["group"]!=b["group"])return text(a["group"],b["group"]);
            return text(a["building"],b["building"]);
        });
        foreach(var offer in offers) {
            var gid=offer["group"];var id=offer["building"];var n=Math.Min(people_left[gid].Double,jobs_left[id].Double);
            if(n<=.000001)continue;
            people_left[gid]-=n;jobs_left[id]-=n;jobs[gid][id]=n;
            var labor=n*personnel[gid].Double*c["stats"][id]["personnel_mult"].Double;effective[id]+=labor;
            links.Add(V.Map("group",gid,"source",gid,"target",id,"people",n,"access",offer["access"],"effective",labor));
        }
        return V.Map("employment",jobs,"effective",effective,"links",links,"unemployed",people_left);
    }
    public static V stocks(V s) {
        var result=V.Map();foreach(var id in Rules.ids(s["projects"]))result[id]=s["projects"][id]["inventory"].Duplicate(true);return result;
    }
    public static V resources(V s,V compiled,V claims,V access) {
        var remaining=stocks(s);var grants=V.Map();var transfers=V.Arr();var pairs=V.Arr();
        foreach(var cid in Rules.ids(claims)) {
            grants[cid]=V.Map();var claim=claims[cid];var owner=claim["owner"];var target=s["projects"][owner];
            var st=compiled["stats"][owner];var conf=compiled["config"][owner];
            foreach(var resource in Rules.ids(claim["needs"])) {
                grants[cid][resource]=0.0;
                foreach(var sid in Rules.ids(s["projects"])) {
                    var source=s["projects"][sid];if(source["inventory"].Get(resource,0).Double<=0)continue;
                    double distance=source.Get("special",false).Bool||target.Get("special",false).Bool?100:Hex.distance(source["tile"].Text,target["tile"].Text);
                    if(sid==owner)distance=0;
                    distance*=st["distance_cost"].Double;
                    if(resource.Text=="material")distance*=conf.Get("material_distance",1).Double;
                    if(access["tiles"].Get(target["tile"],V.Map()).Get("drone",V.Arr()).Count>0)distance*=.4;
                    var same_rank=0;
                    if(conf.Get("same_supplier",false).Bool)same_rank=source["operator_org"]==target["operator_org"]?0:1;
                    var score=st["allocation_priority"].Double+claim.Get("priority",0).Double;var group_distance=distance;
                    if(conf.Get("group_procurement",false).Bool)foreach(var other in Rules.ids(claims)) {
                        var op=s["projects"][claims[other]["owner"]];
                        if(op["operator_org"]==target["operator_org"]&&claims[other]["needs"].Get(resource,0).Double>0&&!op.Get("special",false).Bool&&!source.Get("special",false).Bool)group_distance=Math.Min(group_distance,Hex.distance(source["tile"].Text,op["tile"].Text));
                    }
                    pairs.Add(V.Map("consumer",cid,"source",sid,"resource",resource,"priority",score,"same_rank",same_rank,"group_distance",group_distance,"distance",distance));
                }
            }
        }
        pairs.Items.Sort((a,b)=> {
            if(a["resource"]!=b["resource"])return text(a["resource"],b["resource"]);
            if(a["priority"]!=b["priority"])return b["priority"].Double.CompareTo(a["priority"].Double);
            if(a["same_rank"]!=b["same_rank"])return a["same_rank"].Long.CompareTo(b["same_rank"].Long);
            if(a["group_distance"]!=b["group_distance"])return a["group_distance"].Double.CompareTo(b["group_distance"].Double);
            if(a["distance"]!=b["distance"])return a["distance"].Double.CompareTo(b["distance"].Double);
            if(a["source"]!=b["source"])return text(a["source"],b["source"]);
            return text(a["consumer"],b["consumer"]);
        });
        var processed_groups=V.Map();
        foreach(var pair in pairs) {
            var original=pair["consumer"];var sid=pair["source"];var r=pair["resource"];var owner=claims[original]["owner"];
            var group=compiled["config"][owner].Get("group_procurement",false).Bool;var consumers=V.Arr(original);
            if(group) {
                var org=s["projects"][owner]["operator_org"];var group_key=org.Text+"|"+sid.Text+"|"+r.Text;
                if(processed_groups.Has(group_key))continue;
                processed_groups[group_key]=true;var members=V.Arr();
                foreach(var cid in Rules.ids(claims)) {
                    var host=claims[cid]["owner"];
                    if(s["projects"][host]["operator_org"]!=org||!compiled["config"][host].Get("group_procurement",false).Bool||!claims[cid]["needs"].Has(r))continue;
                    double distance=s["projects"][host]["special"].Bool||s["projects"][sid]["special"].Bool?100:Hex.distance(s["projects"][sid]["tile"].Text,s["projects"][host]["tile"].Text);
                    members.Add(V.Map("id",cid,"distance",distance,"priority",compiled["stats"][host]["allocation_priority"].Double));
                }
                members.Items.Sort((a,b)=> {
                    if(a["priority"]!=b["priority"])return b["priority"].Double.CompareTo(a["priority"].Double);
                    if(a["distance"]!=b["distance"])return a["distance"].Double.CompareTo(b["distance"].Double);
                    return text(a["id"],b["id"]);
                });
                consumers=V.Arr();foreach(var x in members)consumers.Add(x["id"]);
            }
            // A procurement group's finite source turn settles members before external buyers.
            foreach(var cid in consumers) {
                var need=Math.Max(0,claims[cid]["needs"][r].Double-grants[cid][r].Double);var available=remaining[sid].Get(r,0).Double;var amount=Math.Min(need,available);
                if(compiled["options"].Get("procure_mode","near").Text=="split")amount=Math.Min(amount,claims[cid]["needs"][r].Double*.6);
                if(amount<=0)continue;
                grants[cid][r]+=amount;remaining[sid][r]-=amount;
                transfers.Add(V.Map("consumer",cid,"source",sid,"resource",r,"reserved",amount,"distance",pair["distance"],"priority",pair["priority"],"procurement_group",group?s["projects"][owner]["operator_org"]:V.From("")));
            }
        }
        return V.Map("grants",grants,"transfers",transfers,"remaining",remaining);
    }
    public static double supply_ratio(V needs,V grants) {
        double result=1;foreach(var r in needs.Keys)if(needs[r].Double>0)result=Math.Min(result,grants.Get(r,0).Double/needs[r].Double);
        return Math.Max(0,Math.Min(1,result));
    }
    public static V settle_resources(V s,V allocation,V actual) {
        var inventory=stocks(s);var consumed=V.Map();var receipts=V.Arr();var left=actual.Duplicate(true);
        foreach(var transfer in allocation["transfers"]) {
            var cid=transfer["consumer"];var r=transfer["resource"];var sid=transfer["source"];
            var n=Math.Min(left.Get(cid,V.Map()).Get(r,0).Double,transfer["reserved"].Double);if(n<=0)continue;
            n=Math.Min(n,inventory[sid].Get(r,0).Double);inventory[sid][r]-=n;left[cid][r]-=n;consumed[r]=consumed.Get(r,0).Double+n;
            var receipt=transfer.Duplicate();receipt["used"]=n;receipts.Add(receipt);
        }
        return V.Map("inventories",inventory,"consumed",consumed,"receipts",receipts);
    }
    public static V services(Game g,V compiled,V access,V usage) {
        var s=g.s;var pools=V.Map();var source_pool=V.Map();var capacities=V.Map();var covered=V.Map();var allocations=V.Map();var pairs=V.Arr();
        foreach(var gid in Rules.ids(s["groups"])) { covered[gid]=V.Map();allocations[gid]=V.Map(); }
        foreach(var kind in g.c.balance["service_kinds"]) {
            var todo=V.Arr();
            foreach(var id in Rules.ids(s["projects"])) {
                var p=s["projects"][id];var capacity=compiled["stats"][id]["service."+kind.Text].Double*usage.Get(id,V.Map()).Get("utilization",0).Double*compiled["stats"][id]["efficiency"].Double;
                var cfg=compiled["config"][id];
                if(kind.Text=="education"&&cfg.Has("volunteer_education"))foreach(var group in s["groups"].Values) {
                    if(group["host"]==id&&group["education"]>=cfg["volunteer_education"]["threshold"])capacity+=group["count"].Double*cfg["volunteer_education"]["capacity_per_pop"].Double;
                }
                capacity*=cfg.Get("service_capacity_multiplier",V.Map()).Get(kind,1).Double;
                if(p["status"].Text=="running"&&!p["paused"].Bool&&capacity>0) { capacities[id.Text+"|"+kind.Text]=capacity;todo.Add(id); }
            }
            while(todo.Count>0) {
                var root=todo[0];todo.Items.RemoveAt(0);var members=V.Arr(root);var frontier=V.Arr(root);
                if(compiled["options"].Get("service_pool",false).Bool)while(frontier.Count>0) {
                    var x=frontier[0];frontier.Items.RemoveAt(0);
                    foreach(var y in todo.Duplicate())if(Rules.near(s,x.Text,y.Text)==1) { members.Add(y);frontier.Add(y);todo.Erase(y); }
                }
                var pid=root.Text+"|"+kind.Text;double total=0;
                foreach(var source in members) { source_pool[source.Text+"|"+kind.Text]=pid;total+=capacities[source.Text+"|"+kind.Text].Double; }
                pools[pid]=V.Map("kind",kind,"members",members,"capacity",total,"remaining",total);
                foreach(var gid in Rules.ids(s["groups"])) {
                    var group=s["groups"][gid];var home=s["projects"][group["host"]];double best=0;var source=root;
                    foreach(var member in members) {
                        var value=Fields.source_value(access["tiles"],home["tile"].Text,kind.Text,member.Text);
                        if(value>0&&compiled["config"][member].Get("adjacent_service_priority",false).Bool&&Rules.near(s,member.Text,home["id"].Text)<=1)value+=.1;
                        if(compiled["options"].Get("service_pool",false).Bool&&members.Count>1) {
                            if(Rules.near(s,member.Text,home["id"].Text)<=1)value*=1.25;else value=Math.Max(0,value-.15);
                        }
                        if(value>best) { best=value;source=member; }
                    }
                    if(best>0)pairs.Add(V.Map("group",gid,"pool",pid,"kind",kind,"source",source,"access",best));
                }
            }
        }
        pairs.Items.Sort((a,b)=> {
            if(a["access"]!=b["access"])return b["access"].Double.CompareTo(a["access"].Double);
            if(a["pool"]!=b["pool"])return text(a["pool"],b["pool"]);
            return text(a["group"],b["group"]);
        });
        var links=V.Arr();var delivered=V.Map();
        foreach(var pair in pairs) {
            var gid=pair["group"];var kind=pair["kind"];var pool=pools[pair["pool"]];
            var remaining=Math.Max(0,s["groups"][gid]["count"].Double-covered[gid].Get(kind,0).Double);var n=Math.Min(remaining,pool["remaining"].Double);
            if(n<=0)continue;
            pool["remaining"]-=n;covered[gid][kind]=covered[gid].Get(kind,0).Double+n;
            if(!allocations[gid].Has(kind))allocations[gid][kind]=V.Map();allocations[gid][kind][pair["pool"]]=n;
            foreach(var member in pool["members"]) {
                var key=member.Text+"|"+kind.Text;var fraction=capacities[key].Double/pool["capacity"].Double;
                delivered[key]=delivered.Get(key,0).Double+n*fraction;
            }
            links.Add(V.Map("source",pair["source"],"home",s["groups"][gid]["host"],"group",gid,"kind",kind,"people",n,"access",pair["access"],"pool",pair["pool"]));
        }
        return V.Map("covered",covered,"allocations",allocations,"links",links,"pools",pools,"delivered",delivered);
    }
    public static V feasible_resources(V s,V compiled,V claims,V access,V ceilings) {
        var bounded=claims.Duplicate(true);
        foreach(var cid in Rules.ids(bounded))foreach(var resource in bounded[cid]["needs"].Keys)bounded[cid]["needs"][resource]=claims[cid]["needs"][resource].Double*ceilings.Get(cid,1.0).Double;
        var allocation=V.Map();int rounds=0;
        for(int iteration=0;iteration<8;iteration++) {
            rounds=iteration+1;allocation=resources(s,compiled,bounded,access);var changed=false;
            foreach(var cid in Rules.ids(bounded))foreach(var resource in Rules.ids(bounded[cid]["needs"])) {
                var limit=ceilings.Get(cid,1.0).Double;
                foreach(var other in claims[cid]["needs"].Keys) {
                    if(other==resource.Text||claims[cid]["needs"][other].Double<=0)continue;
                    limit=Math.Min(limit,allocation["grants"][cid].Get(other,0).Double/claims[cid]["needs"][other].Double);
                }
                var reduced=Math.Min(bounded[cid]["needs"][resource].Double,claims[cid]["needs"][resource].Double*limit);
                if(reduced<bounded[cid]["needs"][resource].Double-.0000001) { bounded[cid]["needs"][resource]=reduced;changed=true; }
            }
            if(!changed) { allocation["reconcile_rounds"]=rounds;return allocation; }
        }
        allocation=resources(s,compiled,bounded,access);allocation["reconcile_rounds"]=rounds;return allocation;
    }
}
}
