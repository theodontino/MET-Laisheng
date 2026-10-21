using System;
using System.Globalization;
namespace MET.Core {
/// <summary>Six deterministic transaction stages. Rules read a stable tick; receipts commit once. Port of core/simulation.gd.</summary>
public static class Sim {
    public static readonly string[] RES={"food","material","goods"};
    public static V spatial_snapshot(Game g,V compiled) {
        // Includes every access-field input, independently of stocks and counters.
        var projects=V.Arr();
        foreach(var id in Rules.ids(g.s["projects"])) {
            var p=g.s["projects"][id];
            projects.Add(V.Arr(id,p.Get("tile",""),p.Get("status",""),p.Get("paused",false),p.Get("special",false),p.Get("traits",V.Arr())));
        }
        var volunteer=V.Arr();
        foreach(var id in Rules.ids(compiled["config"])) {
            var gate=compiled["config"][id].Get("volunteer_education",V.Map());if(gate.Count==0)continue;
            var eligible=false;
            foreach(var group in g.s["groups"].Values)if(group["host"]==id&&group["education"].Double>=gate["threshold"].Double) { eligible=true;break; }
            volunteer.Add(V.Arr(id,eligible));
        }
        var group_origins=V.Arr();
        foreach(var id in Rules.ids(g.s.Get("authored_fields",V.Map()))) {
            var field=g.s["authored_fields"][id];var source=field.Get("source_id","").Text;
            if(field.Get("origin","").Text==""&&g.s["groups"].Has(source))group_origins.Add(V.Arr(source,g.s["groups"][source]["host"]));
        }
        var key=V.Arr(Rules.ids(g.s["cells"]),projects,compiled["stats"],compiled["config"],compiled["emitters"],compiled["options"],g.s.Get("authored_fields",V.Map()),group_origins,volunteer,g.c.balance["service_kinds"]).ToJson(false,true);
        if(g.spatial_cache.Get("key","").Text==key) {
            var reused=g.spatial_cache["access"];g.spatial_cache=V.Map("key",key,"access",reused,"hit",true);return reused;
        }
        var access=Fields.access(g,compiled);g.spatial_cache=V.Map("key",key,"access",access,"hit",false);return access;
    }
    public static double total(V inventories,string r) {
        double n=0;foreach(var id in Rules.ids(inventories))n+=inventories[id].Get(r,0).Double;return n;
    }
    public static V advance(Game g,double months,bool paid,bool _stop_events=false) {
        var mode=paid?"advance":"normal";var acceleration=paid?25920.0:1.0;
        return Clock.advance(g,months*g.c.beta["units"]["earth_seconds_per_month"].Double/acceleration,mode);
    }
    public static string tick(Game g,double h,V context=null) {
        if(h<=0||double.IsNaN(h)||double.IsInfinity(h))return "结算时间必须为正的有限月数。";
        if(context==null)context=V.Map();var s=g.s;double previous_population=0;
        foreach(var group in s["groups"].Values)previous_population+=group["count"].Double;
        double arrived=0;var next=s.Duplicate(true);var compiled=Rules.compile(g);var stats=compiled["stats"];var config=compiled["config"];
        // 1. Source-aware spatial snapshot.
        var access=spatial_snapshot(g,compiled);
        // 2. Collect offers and claims; resolve explicitly ordered finite sources.
        var labor=Alloc.labor(s,compiled,access);var claims=V.Map();var recipe_claims=V.Map();var population_energy=V.Map();var energy_requests=V.Map();var kitchens=V.Map();
        foreach(var gid in Rules.ids(s["groups"]))kitchens[gid]=0.0;
        foreach(var bid in Rules.ids(s["projects"])) {
            var p=s["projects"][bid];var capacity=config[bid].Get("kitchen_capacity",0).Double;
            if(capacity<=0||p["status"].Text!="running"||p["paused"].Bool)continue;
            foreach(var gid in Rules.ids(s["groups"])) {
                var group=s["groups"][gid];if(Rules.near(s,bid.Text,group["host"].Text)>1)continue;
                var n=Math.Min(capacity,Math.Max(0,group["count"].Double-kitchens[gid].Double));capacity-=n;kitchens[gid]+=n;
            }
        }
        foreach(var bid in Rules.ids(s["projects"])) {
            var p=s["projects"][bid];var st=stats[bid];if(p["special"].Bool)continue;var needs=V.Map();
            if(p["status"].Text=="construction") {
                double work=0;var fraction=work/Math.Max(.000001,p["job"]["work"].Double);needs["material"]=p["job"]["material"].Double*fraction;
                claims["work:"+bid.Text]=V.Map("owner",bid,"needs",needs);energy_requests[bid]=.5;
            } else if(p["status"].Text=="running"&&!p["paused"].Bool) {
                foreach(var r in RES)if(st["input."+r].Double>0)needs[r]=st["input."+r].Double*h;
                claims["run:"+bid.Text]=V.Map("owner",bid,"needs",needs);
                energy_requests[bid]=Math.Max(0,st["energy_demand"].Double-Math.Min(p["next_heat_credit"].Double,st["energy_demand"].Double*st["heat_discount"].Double));
            }
            foreach(var recipe in compiled["recipes"][bid]) {
                if(p["status"].Text!="running"||p["paused"].Bool)continue;
                if(recipe.Get("targets","").Text=="service_area") {
                    var reachable=false;
                    foreach(var kind in g.c.balance["service_kinds"])if(Fields.source_value(access["tiles"],p["tile"].Text,kind.Text,recipe["source_id"].Text)>0)reachable=true;
                    if(!reachable)continue;
                }
                var cid="recipe:"+recipe["uid"].Text;var inputs=V.Map();
                foreach(var resource in recipe["input"].Keys)inputs[resource]=recipe["input"][resource].Double*recipe["rate"].Double*h;
                claims[cid]=V.Map("owner",bid,"needs",inputs);recipe_claims[cid]=V.Map("owner",bid,"recipe",recipe);
            }
        }
        foreach(var gid in Rules.ids(s["groups"])) {
            var group=s["groups"][gid];var st=stats[group["host"]];var n=group["count"].Double;var demand=group["demand"].Double;
            var inputs=V.Map("food",n*st["population_food"].Double*h,"material",n*st["population_material"].Double*h);
            inputs["food"]*=1-.2*kitchens[gid].Double/Math.Max(.001,n);
            if(s["goods_active"].Bool)inputs["goods"]=n*st["population_goods"].Double*demand*h;
            claims["pop:"+gid.Text]=V.Map("owner",group["host"],"needs",inputs);population_energy[gid]=n*st["population_energy"].Double*demand;
        }
        var ceilings=V.Map();
        foreach(var cid in Rules.ids(claims)) {
            var owner=claims[cid]["owner"];var st=stats[owner];var labor_cap=st["jobs"].Double<=0?1:Math.Min(1,labor["effective"].Get(owner,0).Double/st["jobs"].Double);
            ceilings[cid]=cid.Text.StartsWith("pop:",StringComparison.Ordinal)||cid.Text.StartsWith("work:",StringComparison.Ordinal)?1:labor_cap;
        }
        var reserved=Alloc.feasible_resources(s,compiled,claims,access,ceilings);var actual=V.Map();var activity=V.Map();var energy_supplied=g.c.balance["base_energy"].Double;var recipe_energy=V.Map();var energy_sources=V.Map("municipal",energy_supplied);var demand_ceiling=V.Map();
        var happy_energy=compiled["options"].Get("happiness_energy",V.Map());
        if(happy_energy.Count>0)foreach(var group in s["groups"].Values) {
            if(group["happiness"]>=happy_energy["threshold"]) {
                energy_supplied+=group["count"].Double*happy_energy["per_pop"].Double;
                energy_sources[group["host"]]=energy_sources.Get(group["host"],0).Double+group["count"].Double*happy_energy["per_pop"].Double;
            }
        }
        // Resolve generator fuel and labor before electricity demand.
        foreach(var bid in Rules.ids(s["projects"])) {
            var p=s["projects"][bid];var st=stats[bid];
            if(p["special"].Bool||p["status"].Text!="running"||p["paused"].Bool||st["output.energy"].Double<=0)continue;
            var lr=st["jobs"].Double<=0?1:Math.Min(1,labor["effective"][bid].Double/st["jobs"].Double);
            var rr=Alloc.supply_ratio(claims.Get("run:"+bid.Text,V.Map()).Get("needs",V.Map()),reserved["grants"].Get("run:"+bid.Text,V.Map()));
            var supply=st["output.energy"].Double*st["efficiency"].Double*Math.Pow(Math.Min(lr,rr),config[bid].Get("utilization_exponent",st.Get("utilization_exponent",1.08)).Double);
            energy_supplied+=supply;energy_sources[bid]=energy_sources.Get(bid,0).Double+supply;
        }
        foreach(var cid in Rules.ids(recipe_claims)) {
            var item=recipe_claims[cid];var recipe=item["recipe"];if(!recipe["output"].Has("energy"))continue;
            var ratio=Alloc.supply_ratio(claims[cid]["needs"],reserved["grants"][cid]);recipe_energy[cid]=ratio;
            var supply=recipe["output"]["energy"].Double*recipe["rate"].Double*ratio;energy_supplied+=supply;energy_sources[item["owner"]]=energy_sources.Get(item["owner"],0).Double+supply;
        }
        foreach(var bid in Rules.ids(energy_requests)) {
            var p=s["projects"][bid];var st=stats[bid];var cid=(p["status"].Text=="construction"?"work:":"run:")+bid.Text;
            var rr=Alloc.supply_ratio(claims.Get(cid,V.Map()).Get("needs",V.Map()),reserved["grants"].Get(cid,V.Map()));
            var lr=p["status"].Text=="construction"||st["jobs"].Double<=0?1:Math.Min(1,labor["effective"][bid].Double/st["jobs"].Double);
            demand_ceiling[bid]=Math.Min(lr,rr);energy_requests[bid]*=demand_ceiling[bid].Double;
        }
        double energy_need=0;foreach(var v in energy_requests.Values)energy_need+=v.Double;foreach(var v in population_energy.Values)energy_need+=v.Double;
        var batteries=V.Map();var shortage=Math.Max(0,energy_need-energy_supplied)*h;
        foreach(var id in Rules.ids(s["projects"])) {
            var amount=Math.Min(shortage,Math.Max(0,s["projects"][id]["inventory"].Get("energy",0).Double));
            if(stats[id].Get("storage.energy",0).Double>0&&amount>0) {
                batteries[id]=amount;shortage-=amount;energy_supplied+=amount/h;energy_sources[id]=energy_sources.Get(id,0).Double+amount/h;
            }
        }
        var energy_ratio=Math.Min(1,energy_supplied/Math.Max(.000001,energy_need));
        foreach(var cid in Rules.ids(ceilings)) {
            if(cid.Text.StartsWith("pop:",StringComparison.Ordinal)||recipe_energy.Has(cid))continue;
            var owner=claims[cid]["owner"];
            if(energy_requests.Get(owner,0).Double>0)ceilings[cid]=Math.Min(ceilings[cid].Double,energy_ratio*demand_ceiling.Get(owner,1.0).Double);
        }
        reserved=Alloc.feasible_resources(s,compiled,claims,access,ceilings);
        // 3. Utilization: limiting ingredient and nonlinear penalty.
        foreach(var bid in Rules.ids(s["projects"])) {
            var p=s["projects"][bid];var st=stats[bid];var a=V.Map("utilization",0.0,"labor",1.0,"resources",1.0,"energy",1.0,"product_heat",0.0,"outputs",V.Map(),"bottleneck","停运");
            if(p["special"].Bool) { activity[bid]=a;continue; }
            var cid=(p["status"].Text=="construction"?"work:":"run:")+bid.Text;
            if(claims.Has(cid)) {
                a["resources"]=Alloc.supply_ratio(claims[cid]["needs"],reserved["grants"][cid]);a["energy"]=energy_requests.Get(bid,0).Double>0?energy_ratio*demand_ceiling.Get(bid,1.0).Double:1;
                a["labor"]=p["status"].Text=="construction"||st["jobs"].Double<=0?1:Math.Min(1,labor["effective"][bid].Double/st["jobs"].Double);
                var limiting=Math.Min(a["labor"].Double,Math.Min(a["resources"].Double,a["energy"].Double));a["utilization"]=Math.Pow(limiting,config[bid].Get("utilization_exponent",st.Get("utilization_exponent",1.08)).Double);
                if(limiting<.99999)a["utilization"]=Math.Min(a["utilization"].Double,st["jit_cap"].Double);
                a["bottleneck"]=a["labor"].Double<=Math.Min(a["resources"].Double,a["energy"].Double)?"劳力/人员效率":a["resources"]<=a["energy"]?"物资":"能源";
                if(limiting>.999)a["bottleneck"]="投入齐备";
                if(p["status"].Text=="running")foreach(var r in RES) {
                    var expected=st["output."+r].Double*st["efficiency"].Double*a["utilization"].Double*h;if(expected<=0)continue;
                    var room=storage_room(s,compiled,bid.Text,r);
                    if(room<expected) { a["utilization"]*=Math.Max(.1,room/expected);a["bottleneck"]="仓储压力（余量低；残余溢出会损耗）"; }
                }
                actual[cid]=V.Map();foreach(var r in claims[cid]["needs"].Keys)actual[cid][r]=claims[cid]["needs"][r].Double*a["utilization"].Double;
            }
            activity[bid]=a;
        }
        var people_inputs=V.Map();
        foreach(var gid in Rules.ids(s["groups"])) {
            var cid="pop:"+gid.Text;actual[cid]=V.Map();people_inputs[gid]=V.Map("energy",population_energy[gid].Double>0?energy_ratio:1);
            foreach(var r in claims[cid]["needs"].Keys) {
                actual[cid][r]=reserved["grants"][cid].Get(r,0).Double;
                people_inputs[gid][r]=Math.Min(1,actual[cid][r].Double/Math.Max(1e-15,claims[cid]["needs"][r].Double));
            }
        }
        foreach(var cid in Rules.ids(recipe_claims)) {
            var recipe=recipe_claims[cid]["recipe"];var bid=recipe_claims[cid]["owner"];
            var fraction=recipe_energy.Get(cid,Math.Min(Alloc.supply_ratio(claims[cid]["needs"],reserved["grants"][cid]),activity[bid]["utilization"].Double)).Double;
            actual[cid]=V.Map();foreach(var r in claims[cid]["needs"].Keys)actual[cid][r]=claims[cid]["needs"][r].Double*fraction;
        }
        // 4. Settle actual receipts into a private next state and deposit outputs.
        var receipt=Alloc.settle_resources(s,reserved,actual);var inventories=receipt["inventories"];var before=Alloc.stocks(s);var produced=V.Map();var spilled=V.Map();var movements=V.Arr();double kept_production=0;var ledger=V.Map();
        foreach(var bid in Rules.ids(s["projects"]))ledger[bid]=V.Arr();
        foreach(var id in batteries.Keys)inventories[id]["energy"]-=batteries[id];
        foreach(var bid in Rules.ids(inventories))foreach(var r in RES) {
            var cap=storage_cap(compiled,bid.Text,r);var excess=Math.Max(0,inventories[bid].Get(r,0).Double-cap);
            if(excess>0) { inventories[bid][r]-=excess;deposit(s,compiled,inventories,bid.Text,r,excess,spilled,movements); }
        }
        foreach(var bid in Rules.ids(s["projects"])) {
            var p=s["projects"][bid];var st=stats[bid];var a=activity[bid];if(p["special"].Bool)continue;
            next["projects"][bid]["usage"]=a["utilization"];next["projects"][bid]["bottleneck"]=a["bottleneck"];next["projects"][bid]["outputs_last"]=V.Map();next["projects"][bid]["effective_hours"]+=a["utilization"].Double*h;
            if(p["status"].Text=="construction") {
                var work=0.0*a["utilization"].Double;next["projects"][bid]["job"]["work"]=Math.Max(0,p["job"]["work"].Double-work);
                next["projects"][bid]["job"]["material"]=Math.Max(0,p["job"]["material"].Double-actual.Get("work:"+bid.Text,V.Map()).Get("material",0).Double);
            } else if(p["status"].Text=="running"&&!p["paused"].Bool)foreach(var r in RES) {
                var raw=Math.Max(0,st["output."+r].Double*st["efficiency"].Double*a["utilization"].Double);var heat=raw*config[bid].Get("redirect_product_heat",0).Double;a["product_heat"]+=heat;
                var amount=(raw-heat)*h;produced[r]=produced.Get(r,0).Double+amount;a["outputs"][r]=amount/h;next["projects"][bid]["outputs_last"][r]=amount/h;
                var kept=deposit(s,compiled,inventories,bid.Text,r,amount,spilled,movements);kept_production+=kept;
                ledger[bid].Add(V.Map("kind","output","resource",r,"rate",amount/h,"kept",kept/h,"lost",(amount-kept)/h,"source","基础生产与产量方案"));
            }
        }
        foreach(var cid in Rules.ids(recipe_claims)) {
            var recipe=recipe_claims[cid]["recipe"];var bid=recipe_claims[cid]["owner"];var fraction=Alloc.supply_ratio(claims[cid]["needs"],actual[cid]);
            foreach(var r in recipe["output"].Keys) {
                if(r=="energy")continue;var n=recipe["output"][r].Double*recipe["rate"].Double*fraction*h;produced[r]=produced.Get(r,0).Double+n;
                var kept=deposit(s,compiled,inventories,bid.Text,r,n,spilled,movements);kept_production+=kept;
                next["projects"][bid]["outputs_last"][r]=next["projects"][bid]["outputs_last"].Get(r,0).Double+n/h;activity[bid]["outputs"][r]=activity[bid]["outputs"].Get(r,0).Double+n/h;
                ledger[bid].Add(V.Map("kind","output","resource",r,"rate",n/h,"kept",kept/h,"lost",(n-kept)/h,"source",recipe["source"].Text));
            }
        }
        foreach(var bid in energy_sources.Keys) {
            var supply=energy_sources[bid].Double;next["projects"][bid]["outputs_last"]["energy"]=supply;
            ledger[bid].Add(V.Map("kind","output","resource","energy","rate",supply,"kept",supply,"lost",0.0,"source","本期发电 / 余料转换 / 居民贡献"));
        }
        var extra=Math.Max(0,energy_supplied-energy_need)*h;
        foreach(var bid in Rules.ids(s["projects"])) {
            var room=Math.Max(0,stats[bid].Get("storage.energy",0).Double-inventories[bid].Get("energy",0).Double);var n=Math.Min(room,extra);
            if(n>0) { inventories[bid]["energy"]=inventories[bid].Get("energy",0).Double+n;extra-=n; }
        }
        // 5. Actual emissions, source-conserving conversions, finite services and groups.
        var env=Fields.environment(g,compiled,activity,h);double recycled=0,heat_used=0;var heat_receipts=V.Arr();
        foreach(var bid in Rules.ids(s["projects"])) {
            var cfg=config[bid];var p=s["projects"][bid];if(!cfg.Has("field_conversion")||p["status"].Text!="running"||p["paused"].Bool)continue;
            var cv=cfg["field_conversion"];var request=cv["amount"].Double*cv["rate"].Double*h*activity[bid]["utilization"].Double;
            var n=Fields.consume_local_pollution(env,p["tile"].Text,request,h);recycled+=n;
            foreach(var r in cv["output"].Keys) {
                var result=n/cv["amount"].Double*cv["output"][r].Double;produced[r]=produced.Get(r,0).Double+result;
                var kept=deposit(s,compiled,inventories,bid.Text,r,result,spilled,movements);kept_production+=kept;
                next["projects"][bid]["outputs_last"][r]=next["projects"][bid]["outputs_last"].Get(r,0).Double+result/h;activity[bid]["outputs"][r]=activity[bid]["outputs"].Get(r,0).Double+result/h;
                ledger[bid].Add(V.Map("kind","output","resource",r,"rate",result/h,"kept",kept/h,"lost",(result-kept)/h,"source","污染回收"));
            }
            activity[bid]["recycled"]=n;
        }
        foreach(var bid in Rules.ids(s["projects"])) {
            next["projects"][bid]["next_heat_bonus"]=0.0;next["projects"][bid]["next_heat_credit"]=0.0;var p=s["projects"][bid];var st=stats[bid];
            if(p["special"].Bool||p["status"].Text!="running"||p["paused"].Bool)continue;
            double desired=0;if(st["heat_service"].Double>0)desired=st["heat_cap"].Double/st["heat_service"].Double*h;
            if(st["heat_discount"].Double>0)desired+=st["energy_demand"].Double*st["heat_discount"].Double*h;if(desired<=0)continue;
            var allocation=Fields.reserve_heat(env,p["tile"].Text,desired);heat_used+=allocation["amount"].Double;
            if(st["heat_service"].Double>0)next["projects"][bid]["next_heat_bonus"]=Math.Min(st["heat_cap"].Double,allocation["amount"].Double/h*st["heat_service"].Double);
            else next["projects"][bid]["next_heat_credit"]=Math.Min(st["energy_demand"].Double*st["heat_discount"].Double,allocation["amount"].Double/h);
            if(allocation["amount"].Double>0)heat_receipts.Add(V.Map("target",bid,"amount",allocation["amount"],"sources",allocation["sources"]));
        }
        var service=Alloc.services(g,compiled,access,activity);Population.update(g,next,compiled,people_inputs,service,env,labor,h);
        // 6. Fiscal tax bases are actual activity; structural changes commit here.
        var fiscal=V.Arr();double tax=0,trade=0,production=0,people=0,maintenance=0;var taxmode=compiled["options"].Get("tax_mode","head").Text;
        foreach(var group in s["groups"].Values) {
            people+=group["count"].Double;var cost=group["count"].Double*stats[group["host"]]["population_maintenance"].Double;maintenance+=cost*h;
            fiscal.Add(V.Map("kind","expense","source_id",group["host"],"name","居民基础维护","rate",cost));
        }
        foreach(var bid in Rules.ids(s["projects"])) {
            var p=s["projects"][bid];if(p["special"].Bool)continue;
            maintenance+=Math.Max(0,stats[bid]["maintenance"].Double)*h;
            fiscal.Add(V.Map("kind","expense","source_id",bid,"name","建筑固定维护","rate",Math.Max(0,stats[bid]["maintenance"].Double)));
            trade+=service["delivered"].Get(bid.Text+"|commercial",0).Double*stats[bid].Get("commercial_tax_multiplier",1).Double*h;
            trade+=service["delivered"].Get(bid.Text+"|medical",0).Double*config[bid].Get("medical_activity",0).Double*h;
        }
        production=kept_production;
        switch(taxmode) {
            case "head":tax=people*g.c.balance.Get("head_tax",.85).Double*h;break;
            case "production":tax=production*g.c.balance.Get("production_tax",1.35).Double;break;
            case "trade":tax=trade*g.c.balance.Get("trade_tax",.85).Double;break;
        }
        var rent=compiled["options"].Get("land_rent",0).Double*g.playable_ids().Count*h;var pollution_tax=env["pollution_produced"].Double*compiled["options"].Get("pollution_fee",0).Double;
        fiscal.Add(V.Map("kind","income","source_id","municipal","name",V.Map("head","人头税","production","有效生产税","trade","消费税").Get(taxmode,"税收"),"rate",tax/h));
        if(rent>0)fiscal.Add(V.Map("kind","income","source_id","municipal","name","用地收入","rate",rent/h));
        if(pollution_tax>0)fiscal.Add(V.Map("kind","income","source_id","municipal","name","计源排污收入","rate",pollution_tax/h));
        next["currency"]=next["currency"].Double+tax+rent+pollution_tax-maintenance;
        foreach(var id in Rules.ids(inventories)) {
            next["projects"][id]["inventory"]=inventories[id];
            foreach(var r in next["projects"][id].Get("retained_storage",V.Map()).Keys)next["projects"][id]["retained_storage"][r]=Math.Min(next["projects"][id]["retained_storage"][r].Double,inventories[id].Get(r,0).Double);
        }
        foreach(var tile in Rules.ids(next["cells"])) {
            next["cells"][tile]["fields"]=access["tiles"][tile].Duplicate(true);
            foreach(var k in env["tiles"][tile].Keys)next["cells"][tile]["fields"][k]=env["tiles"][tile][k].Duplicate(true);
        }
        next["fields"]=access["fields"].Duplicate(true);next["fields"].Merge(env["fields"]);
        next["local_hours"]=next["local_hours"].Double+h;next["simulation_tick"]=next["simulation_tick"].Long+1;
        foreach(var group in next["groups"].Values)if(group["host"].Text!="homeless"&&group["health"].Double>50&&group["happiness"].Double>40)group["count"]*=Math.Exp(g.c.balance["birth_per_hour"].Double*h);
        var flows=V.Map();foreach(var r in RES)flows[r]=(total(inventories,r)-total(before,r))/h;
        var audit=V.Map("resources",V.Map(),"pollution_generated",env["pollution_produced"],"pollution_recycled",recycled,"heat_generated",env["heat_produced"],"heat_used",heat_used,"hours",h,"phase_order",V.Arr("空间","分配","运行","生产仓储","场与人群","财政与结构"),"population_food_requested",0.0,"commercial_service_people",0.0,"population_counts",V.Map());
        foreach(var gid in Rules.ids(s["groups"])) {
            audit["population_food_requested"]+=claims.Get("pop:"+gid.Text,V.Map()).Get("needs",V.Map()).Get("food",0).Double;
            audit["commercial_service_people"]+=service["covered"].Get(gid,V.Map()).Get("commercial",0).Double;audit["population_counts"][gid]=s["groups"][gid]["count"].Double;
        }
        audit["population_food_demand_rate"]=audit["population_food_requested"].Double/h;
        foreach(var r in RES) {
            var a0=total(before,r);var a1=total(inventories,r);var output=produced.Get(r,0).Double;var input=receipt["consumed"].Get(r,0).Double;var loss=spilled.Get(r,0).Double;
            audit["resources"][r]=V.Map("before",a0,"produced",output,"consumed",input,"lost",loss,"after",a1,"error",a0+output-input-loss-a1);
            if(Math.Abs(audit["resources"][r]["error"].Double)>.00001)return "资源守恒失败："+r;
        }
        if(recycled>env["pollution_produced"].Double+.00001||heat_used>env["heat_produced"].Double+.00001)return "Field额度重复结算。";
        if(g.world_managed) {
            double advancedBefore=0,advancedAfter=0;
            foreach(var p in s["projects"].Values)advancedBefore+=p["inventory"].Get("advanced_components",0).Double;
            foreach(var inventory in inventories.Values)advancedAfter+=inventory.Get("advanced_components",0).Double;
            audit["resources"]["advanced_components"]=V.Map("before",advancedBefore,"produced",0.0,"consumed",0.0,"lost",0.0,"after",advancedAfter,"error",advancedBefore-advancedAfter);
            flows["advanced_components"]=(advancedAfter-advancedBefore)/h;
        }
        next["audit"]=audit;
        next["metrics"]=V.Map("flows",flows,"income",(tax+rent+pollution_tax)/h,"upkeep",maintenance/h,"cashflow",(tax+rent+pollution_tax-maintenance)/h,"energy",energy_supplied,"energy_need",energy_need,
            "service_links",service["links"],"exposure_links",V.Arr(),"employment_links",labor["links"],"allocation",receipt["receipts"],"stock_movements",movements,"spilled",spilled,"utilization",activity,
            "service_pools",service["pools"],"service_covered",service["covered"],"pollution_recycled",recycled/h,"heat_used",heat_used/h,"heat_receipts",heat_receipts,
            "activity_ledger",ledger,"fiscal_ledger",fiscal,"population_energy",population_energy,"energy_sources",energy_sources,"energy_requests",energy_requests,"requested",V.Map(),"energy_ratio",energy_ratio,"reconcile_rounds",reserved.Get("reconcile_rounds",0),
            "tax_basis",V.Map("mode",taxmode,"people",people,"production",production/h,"trade",trade/h,"pollution_fee",pollution_tax/h,"land_rent",rent/h));
        if(g.world_managed) {
            var operated=V.Map();
            foreach(var item in s["bonus_instances"].Pairs)if(item.Value.Get("active",false).Bool) {
                var instance=item.Value;string bonus=instance["bonus_id"].Text;
                operated[item.Key]=V.Map("bonus_id",bonus,"host_type",instance["host_type"],"host_id",instance["host_id"],"level",s["block"]["blueprint_levels"].Get(bonus,0));
            }
            next["metrics"]["bonus_operations"]=operated;
        }
        foreach(var cid in Rules.ids(claims))foreach(var r in claims[cid]["needs"].Keys)next["metrics"]["requested"][r]=next["metrics"]["requested"].Get(r,0).Double+claims[cid]["needs"][r].Double/h;
        foreach(var used in receipt["receipts"]) {
            var owner=claims[used["consumer"]]["owner"];
            ledger[owner].Add(V.Map("kind","input","resource",used["resource"],"rate",used["used"].Double/h,"source",g.project_name(used["source"].Text),"source_id",used["source"],"consumer",used["consumer"]));
        }
        foreach(var bid in energy_requests.Keys)if(energy_requests[bid].Double>0)ledger[bid].Add(V.Map("kind","input","resource","energy","rate",energy_requests[bid].Double*energy_ratio,"source","地区供电（见账本）"));
        foreach(var gid in population_energy.Keys)if(population_energy[gid].Double>0)ledger[s["groups"][gid]["host"]].Add(V.Map("kind","input","resource","energy","rate",population_energy[gid].Double*energy_ratio,"source","住户用电"));
        foreach(var gid in Rules.ids(next["groups"])) {
            var home=next["groups"][gid]["host"];
            foreach(var f in env["tiles"].Get(next["projects"][home]["tile"],V.Map()).Get("pollution",V.Arr()))next["metrics"]["exposure_links"].Add(V.Map("home",home,"source",f["source_id"],"pollution",f["value"],"noise",0.0));
        }
        double happiness=0;foreach(var group in next["groups"].Values)happiness+=group["count"].Double*group["happiness"].Double;happiness/=Math.Max(1,people);
        next["support"]=Math.Max(0,Math.Min(100,s["support"].Double+(happiness-s["support"].Double)*(1-Math.Exp(-h*.15))));
        if(produced.Get("goods",0).Double>0)next["goods_available"]=true;
        if(next.Get("goods_requested_at",-1).Double>=0&&next["local_hours"].Double+.0000001>=next["goods_requested_at"].Double+g.c.balance["goods_grace_hours"].Double)next["goods_active"]=true;
        g.recording_cycle=true;g.s=next;g.compiled=compiled;Construction.tick(g,h);
        g.refresh_metrics();
        Market.tick(g,h,context.Get("external_seconds",h*g.c.beta["units"]["earth_seconds_per_month"].Double/Clock.acceleration(g)).Double);
        var arrival_interval=g.c.beta["economy_candidates"]["arrival_interval_months"].Double;
        while(!g.preview_mode&&g.s["local_hours"].Double+1e-10>=(g.s["event_cursor"].Long+1)*arrival_interval) {
            g.s["event_cursor"]+=1;
            if(!g.s["migration_paused"].Bool) {
                var count=g.c.beta["economy_candidates"]["arrival_people"].Double;Population.create(g,"homeless",count);arrived+=count;
                g.log_event("arrival",V.Map("count",count),count.ToString("F0",CultureInfo.InvariantCulture)+"名新居民抵达，优先填入空余住房。");
            }
        }
        if(!g.preview_mode)Population.rehouse(g);
        var final_metrics=g.s["metrics"].Duplicate(true);final_metrics.Merge(Population.summary(g),true);g.s["metrics"]=final_metrics;
        if(!g.preview_mode) {
            g.recording_cycle=false;Civic.cycle(g,h,previous_population,arrived);Progress.after_commit(g,h);Civic.sync(g);Civic.finalize(g);
        }
        g.recording_cycle=false;return g.validate_state(g.s);
    }
    public static double storage_cap(V c,string id,string r) {
        var cap=Math.Max(0,c["stats"][id].Get("storage."+r,0).Double);
        if(id=="municipal")cap*=c["options"].Get("municipal_storage",1).Double;
        return Math.Max(cap,c["stats"][id].Get("retained_storage."+r,0).Double);
    }
    public static double storage_room(V s,V c,string producer,string r) {
        double capacity=0;
        foreach(var id in Rules.ids(s["projects"])) {
            var p=s["projects"][id];
            if(id.Text!=producer&&(p["status"].Text!="running"||p["paused"].Bool))continue;
            if(id.Text!=producer&&!(id.Text=="municipal"||p["traits"].Contains("building.logistics")||c["config"][id].Get("overflow_relay",false).Bool))continue;
            capacity+=Math.Max(0,storage_cap(c,id.Text,r)-p["inventory"].Get(r,0).Double);
        }
        return capacity;
    }
    public static double deposit(V s,V c,V inventories,string producer,string r,double amount,V spilled,V movements) {
        if(amount<=0)return 0;var targets=V.Arr(producer);var secondary=V.Arr();
        foreach(var id in Rules.ids(s["projects"])) {
            if(id.Text==producer)continue;var p=s["projects"][id];if(p["status"].Text!="running"||p["paused"].Bool)continue;
            int priority=99;
            if(c["config"][id].Get("overflow_relay",false).Bool&&Rules.near(s,producer,id.Text)==1)priority=0;
            else if(p["traits"].Contains("building.logistics"))priority=1;
            else if(id.Text=="municipal")priority=2;
            if(priority<99)secondary.Add(V.Map("id",id,"priority",priority));
        }
        secondary.Items.Sort((a,b)=>a["priority"]!=b["priority"]?a["priority"].Long.CompareTo(b["priority"].Long):StringComparer.Ordinal.Compare(a["id"].Text,b["id"].Text));
        foreach(var x in secondary)targets.Add(x["id"]);var left=amount;
        foreach(var id in targets) {
            var room=Math.Max(0,storage_cap(c,id.Text,r)-inventories[id].Get(r,0).Double);var n=Math.Min(room,left);if(n<=0)continue;
            inventories[id][r]=inventories[id].Get(r,0).Double+n;left-=n;
            if(id.Text!=producer)movements.Add(V.Map("source",producer,"target",id,"resource",r,"amount",n));
            if(left<.000001)break;
        }
        if(left>0)spilled[r]=spilled.Get(r,0).Double+left;return amount-left;
    }
}
}
