using System;
using System.Linq;
namespace MET.Core {
/// <summary>Read-only evidence of a Block's actually deployed and operating build.
/// Eligibility is captured once at real DT zero, never reconstructed on load.</summary>
public static class RogueContent {
    public static V Profiles(Catalog c)=>c.rogue_content.Get("ending_profiles",V.Arr());
    public static V Profile(Catalog c,string id)=>Profiles(c).FirstOrDefault(p=>p["id"].Text==id)??V.Map();
    public static V Evidence(Game g) {
        var groups=V.Map();var operations=g.s.Get("metrics",V.Map()).Get("bonus_operations",V.Map());
        foreach(var pair in g.s.Get("bonus_instances",V.Map()).Pairs) {
            var instance=pair.Value;string bonus=instance.Get("bonus_id","").Text;if(!instance.Get("active",false).Bool||!g.c.bonuses.Has(bonus))continue;
            string host=instance.Get("host_id","").Text,type=instance.Get("host_type","").Text;
            var operation=operations.Get(pair.Key,V.Map());if(operation.Get("bonus_id","").Text!=bonus||operation.Get("host_type","").Text!=type||operation.Get("host_id","").Text!=host)continue;
            var operating=g.playable_ids().Where(id=>{var p=g.s["projects"][id];return p["status"].Text=="running"&&!p["paused"].Bool&&g.s["metrics"].Get("utilization",V.Map()).Get(id,V.Map()).Get("utilization",0).Double>0&&(type=="block"||type=="building"&&id.Text==host||type=="organization"&&p["operator_org"].Text==host);}).ToArray();
            if(operating.Length==0)continue;string key=type+":"+host+":"+bonus;
            if(!groups.Has(key))groups[key]=V.Map("bonus_id",bonus,"name",g.c.bonuses[bonus]["name"],"host_type",type,"host_id",host,"provider",instance.Get("contract_org",instance.Get("provider_org","")),"count",0,"level",operation.Get("level",0),"operating_projects",V.From(operating),"instances",V.Arr());
            groups[key]["count"]+=1;groups[key]["instances"].Add(pair.Key);
        }
        foreach(var row in groups.Values){row["strength"]=Rules.copy_strength(g,(int)row["count"].Long,row["bonus_id"].Text);row["rules"]=Rules.bonus_rules(g,row["bonus_id"].Text,(int)row["count"].Long,(int)row["level"].Long);}
        return V.From(groups.Values.OrderBy(x=>x["bonus_id"].Text,StringComparer.Ordinal).ThenBy(x=>x["host_id"].Text,StringComparer.Ordinal).ToArray());
    }
    static bool Matches(V rule,V gate)=>gate.Get("rule_fields",V.Arr()).Contains(rule.Get("field",""))||gate.Get("rule_keys",V.Arr()).Contains(rule.Get("key",""))||gate.Get("rule_to",V.Arr()).Contains(rule.Get("to",""))||gate.Get("rule_input_types",V.Arr()).Contains(rule.Get("input_type",""));
    static double DeliveredService(Game g,string project,string kind,Func<V,bool> eligibleLink=null) {
        var metrics=g.s.Get("metrics",V.Map());double delivered=0;
        foreach(var link in metrics.Get("service_links",V.Arr())) {
            if(link.Get("kind","").Text!=kind||link.Get("people",0).Double<=0)continue;
            if(eligibleLink!=null&&!eligibleLink(link))continue;
            var pool=metrics.Get("service_pools",V.Map()).Get(link.Get("pool","").Text,V.Map());
            if(!pool.Get("members",V.Arr()).Contains(project))continue;
            // Pool members contribute in proportion to actual operating capacity.
            var stats=g.compiled["stats"][project];var config=g.compiled["config"][project];
            double capacity=stats.Get("service."+kind,0).Double*stats.Get("efficiency",1).Double*metrics.Get("utilization",V.Map()).Get(project,V.Map()).Get("utilization",0).Double;
            if(kind=="education"&&config.Has("volunteer_education"))foreach(var group in g.s["groups"].Values)if(group["host"].Text==project&&group["education"]>=config["volunteer_education"]["threshold"])capacity+=group["count"].Double*config["volunteer_education"]["capacity_per_pop"].Double;
            capacity*=config.Get("service_capacity_multiplier",V.Map()).Get(kind,1).Double;
            if(pool.Get("capacity",0).Double>0)delivered+=link["people"].Double*capacity/pool["capacity"].Double;
        }
        return delivered;
    }
    static V Contributions(Game g,V row,V gate) {
        var result=V.Arr();string bonus=row["bonus_id"].Text;
        var context=V.Map("host_type",row["host_type"],"host",row["host_id"],"provider",row["provider"],"eligibility",row["host_type"].Text=="building"?V.Arr():g.c.bonuses[bonus]["traits"]);
        foreach(var rule in row["rules"]) {
            if(!Matches(rule,gate))continue;string field=rule.Get("field","").Text,key=rule.Get("key","").Text;double value=rule.Get("value",0).IsNumber?rule.Get("value",0).Double:0;
            bool protection=field=="exposure_multiplier"||field=="shortage_multiplier";
            if(protection&&(rule.Get("mode","").Text!="multiply"||value>=1))continue;
            if(field=="service_decay"&&(rule.Get("mode","").Text!="multiply"||value>=1))continue;
            if(field.StartsWith("service.",StringComparison.Ordinal)&&(rule.Get("mode","").Text=="multiply"?value<=1:value<=0))continue;
            foreach(var selection in Rules.selections(g.s,context,rule)) {
                string target=selection["id"].Text;if(!row["operating_projects"].Contains(selection["source_id"]))continue;
                double amount=0;string kind="";
                if(protection){amount=g.s["groups"].Values.Where(group=>group["host"].Text==target).Sum(group=>group.Get("count",0).Double);kind="protected_residents";}
                else if(field=="service_decay"||key=="adjacent_service_priority") {
                    amount=DeliveredService(g,target,"medical",link=>field=="service_decay"?Rules.near(g.s,target,link["home"].Text)>1:link["source"].Text==target&&Rules.near(g.s,target,link["home"].Text)<=1);
                    kind=field=="service_decay"?"extended_medical_reach":"adjacent_medical_priority";
                }
                else if(field.StartsWith("service.",StringComparison.Ordinal)||rule.Get("to","").Text.StartsWith("service.",StringComparison.Ordinal)||key=="volunteer_education") {
                    string service=key=="volunteer_education"?"education":(field.StartsWith("service.",StringComparison.Ordinal)?field:rule["to"].Text).Substring(8);
                    amount=DeliveredService(g,target,service);kind="delivered_"+service;
                }
                else if(rule.Get("input_type","").Text=="pollution"){amount=g.s["metrics"].Get("utilization",V.Map()).Get(target,V.Map()).Get("recycled",0).Double;kind="pollution_recycled";}
                else if(field=="heat_service"||field=="heat_discount"||key=="redirect_product_heat"||key=="redirect_pollution_heat") {
                    foreach(var receipt in g.s["metrics"].Get("heat_receipts",V.Arr())) {
                        if(field.StartsWith("heat_",StringComparison.Ordinal)&&receipt["target"].Text==target)amount+=receipt["amount"].Double;
                        else if(key.StartsWith("redirect_",StringComparison.Ordinal))amount+=receipt.Get("sources",V.Arr()).Where(source=>source["source"].Text==target).Sum(source=>source["amount"].Double);
                    }
                    kind="heat_delivered";
                }
                if(amount>0)result.Add(V.Map("rule",rule["id"],"source_project",selection["source_id"],"target",target,"kind",kind,"amount",amount));
            }
        }
        return result;
    }
    public static V CaptureEnding(Game g,string eligibleTheme) {
        var evidence=Evidence(g);var metrics=Themes._metrics(g);var audit=g.s.Get("audit",V.Map());double hours=audit.Get("hours",0).Double;
        metrics["heat_used"]=hours>0?audit.Get("heat_used",0).Double/hours:0;
        metrics["pollution_recycled"]=hours>0?audit.Get("pollution_recycled",0).Double/hours:0;
        var candidates=V.Arr();
        foreach(var profile in Profiles(g.c)) {
            if(profile["theme"].Text!=eligibleTheme)continue;var gate=profile["gate"];string signal=gate["metric"].Text;double observed=metrics.Get(signal,0).Double;
            var sources=evidence.Where(row=>{var proof=Contributions(g,row,gate);if(proof.Count==0)return false;if(!row.Has("contributions"))row["contributions"]=V.Map();row["contributions"][profile["id"].Text]=proof;return true;}).ToArray();
            if(sources.Length==0||metrics["population"].Double<1||observed+1e-9<gate["minimum"].Double)continue;
            int grade=Math.Min(2,sources.Max(row=>(int)row["level"].Long));
            candidates.Add(V.Map("id",profile["id"],"profile_id",profile["id"],"name",profile["name"],"grade",grade,"signal",signal,"minimum",gate["minimum"],"observed",observed,"source_bonus_ids",V.From(sources.Select(row=>row["bonus_id"].Text).Distinct(StringComparer.Ordinal).ToArray())));
        }
        return V.Map("version",g.c.rogue_content["version"],"eligible_theme",eligibleTheme,"locked_local_seconds",g.s["beta_time"]["local_elapsed_seconds"],"simulation_tick",g.s.Get("simulation_tick",0),"metrics",metrics,"bonus_evidence",evidence,"blueprint_levels",g.s["block"].Get("blueprint_levels",V.Map()).Duplicate(true),"candidates",candidates);
    }
}
}
