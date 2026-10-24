using System;
using System.Globalization;
using System.Linq;
namespace MET.Core {
/// <summary>Stable-world rule collection followed by deterministic reduction. Port of core/rules.gd.</summary>
public static class Rules {
    public static readonly string[] SUPPORTED={"modify","configure","capacity_convert","capacity_copy","grant_recipe","field_convert","emit","relate","slot_add"};
    // Catalog owns immutable content loaded from the caller's content root.
    public static V bonus_content(Game g)=>g.c.bonus_upgrades;
    public static double copy_strength(Game g,int count,string bonus_id="") {
        if(count<=0)return 0;
        var coefficient=bonus_content(g).Get("subsequent_copy_weight",1.0/3.0).Double;
        if(g.c.beta.IsMap)coefficient=g.c.beta.Get("bonus",V.Map()).Get("subsequent_copy_weight",coefficient).Double;
        if(g.c.bonuses.Has(bonus_id))coefficient=g.c.bonuses[bonus_id].Get("subsequent_copy_weight",coefficient).Double;
        return 1.0+(count-1)*Math.Max(0,coefficient);
    }
    public static double _weighted(double value,string mode,double strength,V maximum=null) {
        var result=mode=="add"?value*strength:1.0+(value-1.0)*strength;
        if(mode=="multiply")result=Math.Max(0,result);
        if(maximum!=null&&!maximum.IsNull)result=Math.Min(maximum.Double,result);
        return result;
    }
    public static V bonus_rules(Game g,string bonus_id,int count=1,int level_override=-1) {
        if(!g.c.bonuses.Has(bonus_id))return V.Arr();
        var content=bonus_content(g);
        var level=Math.Max(0,Math.Min(2,(int)g.s.Get("block",V.Map()).Get("blueprint_levels",V.Map()).Get(bonus_id,0).Long));
        if(level_override>=0)level=Math.Max(0,Math.Min(2,level_override));
        var patches=content.Get("upgrades",V.Map()).Get(bonus_id,V.Map());
        var policies=content.Get("rule_policies",V.Map());
        var strength=copy_strength(g,count,bonus_id);var result=V.Arr();
        foreach(var original in g.c.bonuses[bonus_id].Get("rules",V.Arr())) {
            var rule=original.Duplicate(true);var rule_id=rule.Get("id","").Text;
            foreach(var key in patches.Get(rule_id,V.Map()).Keys) {
                var levels=patches[rule_id][key];var replacement=levels[Math.Min(level,levels.Count-1)];
                rule[key]=replacement.IsMap?replacement.Duplicate(true):replacement;
            }
            var policy=policies.Get(rule_id,V.Map());
            if(rule.Get("op","").Text=="modify"&&(rule.Get("mode","").Text=="add"||rule.Get("mode","").Text=="multiply"))
                rule["value"]=_weighted(rule["value"].Double,rule["mode"].Text,strength);
            else if(policy.Has("value_mode"))rule["value"]=_weighted(rule["value"].Double,policy["value_mode"].Text,strength,policy.Get("maximum"));
            else if(policy.Has("dictionary_values")) {
                foreach(var key in policy["dictionary_values"].Keys)rule["value"][key]=_weighted(rule["value"][key].Double,policy["dictionary_values"][key].Text,strength);
            } else if(policy.Has("strength_field")) {
                var field=policy["strength_field"].Text;
                rule[field]=_weighted(rule.Get(field,1.0).Double,"add",strength,policy.Get("maximum"));
            }
            result.Add(rule);
        }
        return result;
    }
    public static bool duplicate_has_benefit(Game g,string bonus_id,int existing_count) {
        if(existing_count<=0)return true;
        var benefit_ids=bonus_content(g).Get("benefit_rules",V.Map()).Get(bonus_id,V.Arr());
        var before=bonus_rules(g,bonus_id,existing_count);var after=bonus_rules(g,bonus_id,existing_count+1);
        for(int i=0;i<Math.Min(before.Count,after.Count);i++)if(benefit_ids.Contains(before[i].Get("id",""))&&before[i]!=after[i])return true;
        return false;
    }
    public static V ids(V d)=>V.From(d.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray());
    public static int near(V s,string a,string b) {
        if(!s["projects"].Has(a)||!s["projects"].Has(b))return 999;
        var x=s["projects"][a]["tile"].Text;var y=s["projects"][b]["tile"].Text;
        if(!s["cells"].Has(x)||!s["cells"].Has(y))return 999;
        return Hex.distance(x,y);
    }
    public static bool any_trait(V have,V want) {
        if(want.Count==0)return true;
        foreach(var t in want)if(have.Contains(t))return true;
        return false;
    }
    public static V semantic_traits(V s,string id) {
        var ts=s["projects"][id]["traits"].Duplicate();
        if(s["projects"][id].Get("labels",V.Arr()).Contains("use.commercial")&&!ts.Contains("building.commercial"))ts.Add("building.commercial");
        return ts;
    }
    public static bool condition(V s,string id,V when,string source="",string provider="") {
        var p=s["projects"][id];var traits=semantic_traits(s,id);
        foreach(var t in when.Get("traits",V.Arr()))if(!traits.Contains(t))return false;
        if(!any_trait(traits,when.Get("any_traits",V.Arr())))return false;
        var tile=s["cells"].Get(p["tile"],V.Map());
        foreach(var t in when.Get("tile_traits",V.Arr()))if(!tile.Get("traits",V.Arr()).Contains(t))return false;
        if(when.Get("same_provider",false).Bool&&(provider==""||provider!=p["operator_org"].Text))return false;
        foreach(var k in when.Get("field_min",V.Map()).Keys)
            if(aggregate(tile.Get("fields",V.Map()).Get(k,V.Arr()),"sum")<when["field_min"][k].Double)return false;
        return true;
    }
    public static double aggregate(V contributions,string mode="sum") {
        if(contributions.Count==0)return 0;
        var values=contributions.Items.Select(x=>x["value"].Double).ToArray();
        switch(mode) {
            case "max":return values.Max();case "min":return values.Min();
            case "product":return values.Aggregate(1.0,(v,x)=>v*x);
            case "mean": { double total=0;foreach(var value in values)total+=value;return total/values.Length; }
            default: { double total=0;foreach(var value in values)total+=value;return total; }
        }
    }
    public static V reduce_number(double basis,V modifiers) {
        double add=0,mult=1,lo=double.NegativeInfinity,hi=double.PositiveInfinity;
        var chosen=V.Map();var trail=V.Arr();
        var ordered=modifiers.Items.OrderBy(x=>x["uid"].Text,StringComparer.Ordinal);
        foreach(var m in ordered) {
            var value=m["value"].Double;
            switch(m["mode"].Text) {
                case "add":add+=value;break;case "multiply":mult*=value;break;
                case "floor":lo=Math.Max(lo,value);break;case "cap":hi=Math.Min(hi,value);break;
                case "override":if(chosen.Count==0||wins(m,chosen))chosen=m;break;
            }
            trail.Add(V.Map("source",m["source"],"rule",m["uid"],"mode",m["mode"],"value",value));
        }
        var result=(basis+add)*mult;if(lo>hi)lo=hi;result=Math.Max(lo,Math.Min(hi,result));
        if(chosen.Count>0)result=chosen["value"].Double;
        return V.Map("value",result,"base",basis,"add",add,"multiply",mult,"cap",double.IsInfinity(hi)?null:(object)hi,"floor",double.IsInfinity(lo)?null:(object)lo,"override",chosen.Get("source",""),"rules",trail);
    }
    public static bool wins(V a,V b) {
        if(a["specificity"].Long!=b["specificity"].Long)return a["specificity"].Long>b["specificity"].Long;
        if(a["priority"].Long!=b["priority"].Long)return a["priority"].Long>b["priority"].Long;
        return StringComparer.Ordinal.Compare(a["uid"].Text,b["uid"].Text)<0;
    }
    public static V selections(V s,V context,V rule) {
        var sources=V.Arr();var ht=context["host_type"].Text;var host=context["host"].Text;
        if(ht=="building") { if(s["projects"].Has(host))sources.Add(host); }
        else foreach(var id in ids(s["projects"])) {
            var p=s["projects"][id];
            if(p.Get("special",false).Bool&&!rule.Get("include_special",false).Bool)continue;
            if(ht=="organization"&&(p["operator_org"].Text!=host||p.Get("operator_unresolved",false).Bool))continue;
            if(any_trait(semantic_traits(s,id.Text),context.Get("eligibility",V.Arr())))sources.Add(id);
        }
        var result=V.Map();var shape=rule.Get("targets","self").Text;
        foreach(var source in sources) {
            if(!any_trait(semantic_traits(s,source.Text),context.Get("eligibility",V.Arr())))continue;
            if(shape=="self")result[source]=source;
            else {
                if(s["projects"][source]["status"].Text!="running"||s["projects"][source]["paused"].Bool)continue;
                foreach(var target in ids(s["projects"])) {
                    if(s["projects"][target].Get("special",false).Bool&&!rule.Get("include_special",false).Bool)continue;
                    var distance=near(s,source.Text,target.Text);
                    if(shape=="adjacent"&&distance!=1)continue;
                    if(shape=="radius"&&distance>rule.Get("radius",2).Long)continue;
                    result[target]=source;
                }
            }
        }
        var rows=V.Arr();
        foreach(var target in ids(result))if(condition(s,target.Text,rule.Get("when",V.Map()),result[target].Text,context.Get("provider","").Text))rows.Add(V.Map("id",target,"source_id",result[target]));
        return rows;
    }
    public static V compile(Game g) {
        var s=g.s;var c=g.c;var contexts=V.Arr();var stats=V.Map();var configs=V.Map();var recipes=V.Map();var emitters=V.Map();var trace=V.Map();var numeric=V.Map();var transforms=V.Map();var options=V.Map();
        foreach(var id in ids(s["projects"])) {
            var p=s["projects"][id];stats[id]=c.projects.Has(p["def"])?c.projects[p["def"]]["stats"].Duplicate(true):p["base_stats"].Duplicate(true);
            if(p.Get("upgrade_job",V.Map()).Count>0) {
                var factor=c.v040["progression"]["upgrade_operation_factor"].Double;stats[id]["efficiency"]*=factor;
                foreach(var key in stats[id].Keys)if(key.StartsWith("service.",StringComparison.Ordinal)||key=="output.energy")stats[id][key]*=factor;
            }
            if(g.world_managed) {
                var def=p.Get("def","").Text;
                var advanced=id.Text=="municipal"?20.0:def=="freight"?80.0:def=="manufacturing"?20.0:0.0;
                if(advanced>0)stats[id]["storage.advanced_components"]=advanced;
            }
            configs[id]=V.Map();recipes[id]=V.Arr();emitters[id]=V.Arr();trace[id]=V.Map();numeric[id]=V.Map();transforms[id]=V.Arr();
            foreach(var r in p.Get("retained_storage",V.Map()).Keys)stats[id]["retained_storage."+r]=p["retained_storage"][r].Double;
        }
        contexts.Add(V.Map("host_type","block","host",s["block"]["id"],"rules",c.data["world_rules"],"specificity",0,"name","基础世界","provider","","uid","world","eligibility",V.Arr()));
        if(g.world_managed)foreach(var source in ids(g.world_effects.Get("sources",V.Map()))) {
            var effect=g.world_effects["sources"][source];
            contexts.Add(V.Map("host_type","block","host",s["block"]["id"],"rules",effect["rules"],"specificity",10,"name",effect["name"],"provider","","uid",source,"eligibility",V.Arr()));
        }
        foreach(var slot in ids(s["block"]["policies"])) {
            var policy=c.policies[s["block"]["policies"][slot]];
            contexts.Add(V.Map("host_type","block","host",s["block"]["id"],"rules",policy["rules"],"specificity",10,"name",policy["name"],"provider","","uid","policy:"+policy["id"].Text,"eligibility",V.Arr()));
        }
        foreach(var org_id in ids(s["organizations"])) {
            var org=c.operators[org_id];
            contexts.Add(V.Map("host_type","organization","host",org_id,"rules",org["rules"],"specificity",20,"name",org["name"],"provider",org_id,"uid","org:"+org_id.Text,"eligibility",V.Arr()));
        }
        var capstone=s["block"].Get("capstone",V.Map());
        if(capstone.Count>0&&capstone.Get("rules").IsArray)contexts.Add(V.Map("host_type","block","host",s["block"]["id"],"rules",capstone["rules"],"specificity",10,"name",capstone.Get("name","主题地标"),"provider","","uid",capstone.Get("id","capstone"),"eligibility",V.Arr()));
        // Copies collapse before reduction; blueprint changes apply before copy weighting.
        var bonus_groups=V.Map();
        foreach(var instance_id in ids(s["bonus_instances"])) {
            var instance=s["bonus_instances"][instance_id];
            if(!instance.Get("active",false).Bool||!c.bonuses.Has(instance.Get("bonus_id","")))continue;
            var key=instance["host_type"].Text+"|"+instance["host_id"].Text+"|"+instance["bonus_id"].Text;
            if(!bonus_groups.Has(key))bonus_groups[key]=V.Map("instance",instance,"uid",instance_id,"count",0);
            bonus_groups[key]["count"]+=1;
        }
        foreach(var key in ids(bonus_groups)) {
            var group=bonus_groups[key];var instance=group["instance"];var def=c.bonuses[instance["bonus_id"]];
            var level=Math.Max(0,Math.Min(2,(int)s["block"].Get("blueprint_levels",V.Map()).Get(instance["bonus_id"],0).Long));var label=def["name"].Text;
            if(level>0)label+=" · "+bonus_content(g)["grades"][level].Text;
            if(group["count"].Long>1)label+=" ×"+group["count"].Long.ToString(CultureInfo.InvariantCulture)+"（合成强度 "+copy_strength(g,(int)group["count"].Long,instance["bonus_id"].Text).ToString("F3",CultureInfo.InvariantCulture)+"；数值下限0，比例上限1）";
            contexts.Add(V.Map("host_type",instance["host_type"],"host",instance["host_id"],"rules",bonus_rules(g,instance["bonus_id"].Text,(int)group["count"].Long),"specificity",V.Map("building",30,"organization",20,"block",10)[instance["host_type"]],"name",label,"provider",instance.Get("contract_org",instance.Get("provider_org","")),"uid",group["uid"],"eligibility",instance["host_type"].Text=="building"?V.Arr():def["traits"]));
        }
        foreach(var ctx in contexts)foreach(var rule in ctx["rules"]) {
            var op=rule["op"].Text;
            if(!SUPPORTED.Contains(op))throw new InvalidOperationException("Unsupported Rule operator: "+op);
            var selected=selections(s,ctx,rule);
            if(op=="configure"&&ctx["host_type"].Text=="block") {
                var contender=V.Map("value",rule["value"],"specificity",ctx["specificity"],"priority",rule.Get("priority",0).Long,"uid",ctx["uid"].Text+":"+rule["id"].Text,"source",ctx["name"]);
                if(!options.Has(rule["key"])||wins(contender,options[rule["key"]]))options[rule["key"]]=contender;
            }
            foreach(var sel in selected) {
                var id=sel["id"];
                var meta=V.Map("specificity",ctx["specificity"].Long+(rule.Get("when",V.Map()).Count>0?1:0),"priority",rule.Get("priority",0).Long,"uid",ctx["uid"].Text+":"+rule["id"].Text+":"+id.Text,"source",ctx["name"]);
                if(op=="modify") {
                    var fields=V.Arr();var field=rule["field"].Text;
                    if(field.EndsWith("*",StringComparison.Ordinal)) {
                        foreach(var k in stats[id].Keys)if(k.StartsWith(field.Substring(0,field.Length-1),StringComparison.Ordinal))fields.Add(k);
                    } else fields.Add(field);
                    foreach(var k in fields) {
                        if(!numeric[id].Has(k))numeric[id][k]=V.Arr();var mod=meta.Duplicate();mod["mode"]=rule["mode"];mod["value"]=rule["value"];numeric[id][k].Add(mod);
                    }
                } else if(op=="configure") {
                    var value=meta.Duplicate();value["value"]=rule["value"];
                    if(!configs[id].Has(rule["key"])||wins(value,configs[id][rule["key"]]))configs[id][rule["key"]]=value;
                } else if(op=="grant_recipe") {
                    var entry=rule.Duplicate(true);entry["uid"]=meta["uid"];entry["source"]=ctx["name"];entry["source_id"]=sel["source_id"];recipes[id].Add(entry);
                } else if(op=="emit") {
                    var entry=rule.Duplicate(true);entry["uid"]=meta["uid"];entry["source"]=ctx["name"];emitters[id].Add(entry);
                } else {
                    var entry=rule.Duplicate(true);entry["meta"]=meta;entry["source_id"]=sel["source_id"];transforms[id].Add(entry);
                }
            }
        }
        foreach(var id in ids(stats)) {
            foreach(var k in ids(numeric[id])) {
                var result=reduce_number(stats[id].Get(k,0).Double,numeric[id][k]);stats[id][k]=result["value"];trace[id][k]=result;
            }
            foreach(var k in ids(configs[id]))configs[id][k]=configs[id][k]["value"];
            foreach(var r in transforms[id])switch(r["op"].Text) {
                case "capacity_convert":case "capacity_copy":
                    var value=stats[id].Get(r["from"],0).Double*r["fraction"].Double;
                    if(r["op"].Text=="capacity_convert")stats[id][r["from"]]-=value;
                    stats[id][r["to"]]=stats[id].Get(r["to"],0).Double+value;
                    trace[id][r["to"]]=V.Map("base",c.projects.Get(s["projects"][id]["def"],V.Map()).Get("stats",V.Map()).Get(r["to"],0),"value",stats[id][r["to"]],"rules",V.Arr(V.Map("source",r["meta"]["source"],"mode",r["op"],"value",value)));break;
                case "field_convert":configs[id]["field_conversion"]=r;break;
                case "relate":configs[id][r["relation"]]=true;break;
                case "slot_add":configs[id]["slot_added"]=r;break;
            }
            double count=0;var contributors=V.Arr();
            foreach(var other in ids(stats)) {
                if(near(s,id.Text,other.Text)!=1||s["projects"][other]["status"].Text!="running"||s["projects"][other]["paused"].Bool)continue;
                var t=semantic_traits(s,other.Text);var own=semantic_traits(s,id.Text);
                var compatible=(own.Contains("building.industrial")&&t.Contains("building.industrial"))||(own.Contains("building.commercial")&&t.Contains("building.industrial"));
                if((configs[id].Get("medical_commercial_relation",false).Bool&&t.Contains("building.commercial"))||(configs[other].Get("medical_commercial_relation",false).Bool&&own.Contains("building.commercial")))compatible=true;
                var amount=compatible?.1:.1*configs[id].Get("universal_adjacency",0).Double;
                if(compatible&&own.Contains("building.industrial")&&s["projects"][id]["operator_org"]==s["projects"][other]["operator_org"])amount*=stats[id].Get("synergy.same_industrial",1).Double;
                if(compatible&&own.Contains("building.commercial")&&t.Contains("building.industrial"))amount*=configs[id].Get("industrial_adjacency",1).Double;
                count+=amount;if(amount!=0)contributors.Add(V.Map("source_id",other,"tile",s["projects"][other]["tile"],"amount",amount,"compatible",compatible));
            }
            if(count>0) {
                stats[id]["efficiency"]*=1+count;
                trace[id]["spatial_synergy"]=V.Map("base",1.0,"value",1+count,"contributors",contributors,"rules",V.Arr(V.Map("source","相邻建筑关系","mode","multiply","value",1+count)));
            }
            if(!s["projects"][id].Get("special",false).Bool&&g.cohesion(id.Text)["active"].Bool) {
                var reward=c.operators[s["projects"][id]["operator_org"]].Get("cohesion_efficiency",.03).Double;stats[id]["efficiency"]*=1+reward;
                trace[id]["cohesion"]=V.Map("base",1.0,"value",1+reward,"rules",V.Arr(V.Map("source","整套自营方案","mode","multiply","value",1+reward)));
            }
            var heat_bonus=s["projects"][id].Get("next_heat_bonus",0).Double;stats[id]["efficiency"]*=1+heat_bonus;
            if(heat_bonus>0)trace[id]["heat"]=V.Map("base",1.0,"value",1+heat_bonus,"rules",V.Arr(V.Map("source","上周期实际收到的余热","mode","multiply","value",1+heat_bonus)));
            foreach(var k in stats[id].Keys)if(double.IsNaN(stats[id][k].Double)||double.IsInfinity(stats[id][k].Double))Console.Error.WriteLine("Non-finite compiled stat "+id.Text+":"+k);
        }
        foreach(var k in options.Keys)options[k]=options[k]["value"];
        return V.Map("stats",stats,"config",configs,"recipes",recipes,"emitters",emitters,"trace",trace,"options",options);
    }
}
}
