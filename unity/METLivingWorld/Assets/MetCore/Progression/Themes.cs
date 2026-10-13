using System;
using System.Linq;
namespace MET.Core {
public static class Themes {
    public const double MONTH_SECONDS = 1555200.0;
    public static V _themes(Game g) {
        return g.c.commission_themes;
    }
    public static V _events(Game g) {
        return g.c.commission_events;
    }
    public static V _definition(Game g, string id) {
        foreach (var row in _themes(g)) {
            if ((row["id"].Text == id)) { return row; }
        }
        return V.Map();
    }
    public static V _event_definition(Game g, string id) {
        foreach (var row in _events(g)) {
            if ((row["id"].Text == id)) { return row; }
        }
        return V.Map();
    }
    public static double _reforge_cost(Game g) {
        return g.c.beta.Get("commission_candidates",V.Map()).Get("reforge_dt_cost",7.5).Double;
    }
    public static double _token_chance(Game g) {
        return g.c.beta.Get("commission_candidates",V.Map()).Get("reforge_opportunity_chance_per_major_cycle",0.15).Double;
    }
    public static double _now(Game g) {
        return g.s.Get("beta_time",V.Map()).Get("local_elapsed_seconds",(g.s.Get("local_hours",0).Double * MONTH_SECONDS)).Double;
    }
    public static void ensure(Game g) {
        if (!((g.s.Get("block").IsMap))) { g.s["block"] = V.Map("id", "block:1"); }
        if (!((g.s["block"].Get("theme").IsMap))) {
            g.s["block"]["theme"] = V.Map("id", "", "stage", 1, "completed", false, "steady_seconds", 0.0, "counters", V.Map(), "milestone_ids", V.Arr(), "reforge_tokens", 0, "events", V.Arr(), "event_ids", V.Arr(), "history", V.Arr(), "arrivals", V.Arr(), "trade_cursor", g.s["block"].Get("trade_history",V.Arr()).Count, "last_tick", (int)g.s.Get("simulation_tick",0).Long, "last_major_cycle", 0, "last_metrics", V.Map(), "started_seconds", _now(g));
        }
        V t = g.s["block"]["theme"];
        foreach (var field in V.Arr("events", "event_ids", "history", "milestone_ids", "arrivals")) {
            if (!((t.Get(field).IsArray))) { t[field] = V.Arr(); }
        }
        foreach (var field in V.Arr("counters", "last_metrics")) {
            if (!((t.Get(field).IsMap))) { t[field] = V.Map(); }
        }
        if (!((g.s.Get("rng").IsMap))) { g.s["rng"] = V.Map(); }
        if (!(g.s["rng"].Has("theme_events"))) {
            g.s["rng"]["theme_events"] = V.From((((Math.Abs(g.s.Get("seed",1).Long) + 7919) % 2147483646) + 1));
        }
    }
    public static void _pause(Game g, string reason) {
        if (!((g.s.Get("settings").IsMap))) { g.s["settings"] = V.Map(); }
        g.s["settings"]["user_paused"] = V.From(true);
        if ((g.s.Get("beta_time").IsMap)) { g.s["beta_time"]["pause_reason"] = V.From(reason); }
    }
    public static void _record(Game g, string kind, V detail = null) {
        detail ??= V.Map();
        V item = detail.Duplicate(true);
        item["kind"] = V.From(kind);
        item["local_seconds"] = V.From(_now(g));
        g.s["block"]["theme"]["history"].Add(item);
    }
    public static void _start(Game g, string id) {
        V t = g.s["block"]["theme"];
        t["id"] = V.From(id);
        t["stage"] = V.From(1);
        while ((((int)t["stage"].Long <= 3) && (t["milestone_ids"].Contains(((id + ":M") + t["stage"].Text))))) { t["stage"] = V.From(((int)t["stage"].Long + 1)); }
        t["completed"] = V.From(((int)t["stage"].Long > 3));
        t["stage"] = V.From(Math.Min(3,(int)t["stage"].Long));
        t["steady_seconds"] = V.From(0.0);
        t["counters"] = V.Map();
        t["last_metrics"] = _metrics(g);
        t["trade_cursor"] = V.From(g.s["block"].Get("trade_history",V.Arr()).Count);
        t["last_tick"] = V.From((int)g.s.Get("simulation_tick",0).Long);
        t["started_seconds"] = V.From(_now(g));
    }
    public static string select(Game g, string id) {
        ensure(g);
        if ((g.s.Get("phase","") != "play")) { return "当前不能选择发展主题。"; }
        if ((g.s["block"]["theme"]["id"].Text != "")) { return string.Format(System.Globalization.CultureInfo.InvariantCulture,"已有发展主题；更换需要委任重铸权和{0:F2} DT。",_reforge_cost(g)); }
        if ((_definition(g,id).Count == 0)) { return "未知发展主题。"; }
        _start(g,id);
        _record(g,"selected",V.Map("id", id));
        g.revision += 1;
        return "";
    }
    public static string reforge(Game g, string id) {
        ensure(g);
        V t = g.s["block"]["theme"];
        if ((g.s.Get("phase","") != "play")) { return "当前不能重铸委任。"; }
        if ((t["id"].Text == "")) { return "请先选择发展主题。"; }
        if (t["completed"].Bool) { return "主题已完成，本局发展方向已定型。"; }
        if (((_definition(g,id).Count == 0) || (id == t["id"].Text))) { return "请选择另一个发展主题。"; }
        if (((int)t["reforge_tokens"].Long < 1)) { return string.Format(System.Globalization.CultureInfo.InvariantCulture,"需要一枚稀有委任重铸权；使用还需{0:F2} DT。",_reforge_cost(g)); }
        if (((g.s.Get("dt",0).Double + 1e-09) < _reforge_cost(g))) { return string.Format(System.Globalization.CultureInfo.InvariantCulture,"DT不足{0:F2}，委任与重铸权均未改变。",_reforge_cost(g)); }
        string previous = t["id"].Text;
        t["reforge_tokens"] = V.From(((int)t["reforge_tokens"].Long - 1));
        g.s["dt"] = V.From(Math.Max(0.0,(g.s["dt"].Double - _reforge_cost(g))));
        g.s["totals"]["dt_used"] = V.From((g.s["totals"].Get("dt_used",0).Double + _reforge_cost(g)));
        foreach (var evt in t["events"]) {
            if ((!V.Arr("offered", "active").Contains(evt.Get("status","").Text))) { continue; }
            g.s["currency"] = V.From((g.s["currency"].Double + evt.Get("deposit",0).Double));
            evt["deposit"] = V.From(0.0);
            evt["status"] = V.From("cancelled");
        }
        _start(g,id);
        _record(g,"reforged",V.Map("from", previous, "id", id, "dt_cost", _reforge_cost(g)));
        if ((g.s["dt"].Double <= 0.0)) { Block.on_dt_zero(g); }
        g.revision += 1;
        return "";
    }
    public static V _stage(Game g) {
        V t = g.s["block"]["theme"];
        V definition = _definition(g,t["id"].Text);
        if ((definition.Count == 0)) { return V.Map(); }
        return definition["stages"][Math.Min(2, Math.Max(0,((int)t["stage"].Long - 1)))];
    }
    public static double _current_food_demand(Game g) {
        V covered = V.Map();
        V groups = V.From(g.s.Get("groups",V.Map()).Keys);
        groups.Items.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Text,b.Text));
        foreach (var gid in groups) { covered[gid] = V.From(0.0); }
        V buildings = V.From(g.s.Get("projects",V.Map()).Keys);
        buildings.Items.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Text,b.Text));
        foreach (var bid in buildings) {
            V p = g.s["projects"][bid];
            double capacity = g.compiled.Get("config",V.Map()).Get(bid,V.Map()).Get("kitchen_capacity",0).Double;
            if (((capacity <= 0) || (p.Get("status","").Text != "running") || p.Get("paused",false).Bool)) { continue; }
            foreach (var gid in groups) {
                V group = g.s["groups"][gid];
                if ((Rules.near(g.s,bid.Text,group["host"].Text) > 1)) { continue; }
                double n = Math.Min(capacity,Math.Max(0,(group["count"].Double - covered[gid].Double)));
                capacity -= n;
                covered[gid] += V.From(n);
            }
        }
        double demand = 0.0;
        foreach (var gid in groups) {
            V group = g.s["groups"][gid];
            double n = Math.Max(0,group.Get("count",0).Double);
            V st = g.compiled.Get("stats",V.Map()).Get(group.Get("host","").Text,V.Map());
            demand += ((n * st.Get("population_food",g.c.balance.Get("population_food",0.18)).Double) * (1.0 - ((0.2 * covered[gid].Double) / Math.Max(0.001,n))));
        }
        return demand;
    }
    public static V _population_metrics(Game g) {
        V resultMetrics = V.Map("population", 0.0, "housed", 0.0, "homeless", 0.0, "food_demand", 0.0, "food", 0.0, "medical", 0.0, "education", 0.0, "commercial", 0.0, "entertainment", 0.0, "health", 0.0, "happiness", 0.0, "commercial_people", 0.0);
        foreach (var group in V.From(g.s.Get("groups",V.Map()).Values)) {
            double n = Math.Max(0.0,group.Get("count",0).Double);
            resultMetrics["population"] += V.From(n);
            if ((group.Get("host","").Text == "homeless")) { resultMetrics["homeless"] += V.From(n); }
            else { resultMetrics["housed"] += V.From(n); }
            V st = g.compiled.Get("stats",V.Map()).Get(group.Get("host","").Text,V.Map());
            resultMetrics["food_demand"] += V.From((n * st.Get("population_food",g.c.balance.Get("population_food",0.18)).Double));
            foreach (var field in V.Arr("food", "medical", "education", "commercial", "entertainment")) {
                resultMetrics[field] += V.From((n * Math.Min(1, Math.Max(0,group.Get("living",V.Map()).Get(field,0).Double))));
            }
            resultMetrics["health"] += V.From((n * group.Get("health",0).Double));
            resultMetrics["happiness"] += V.From((n * group.Get("happiness",0).Double));
        }
        resultMetrics["commercial_people"] = V.From(g.s.Get("audit",V.Map()).Get("commercial_service_people",resultMetrics["commercial"]).Double);
        resultMetrics["food_demand"] = V.From(_current_food_demand(g));
        foreach (var field in V.Arr("food", "medical", "education", "commercial", "entertainment", "health", "happiness")) {
            resultMetrics[field] /= V.From(Math.Max(1e-06,resultMetrics["population"].Double));
        }
        return resultMetrics;
    }
    public static V _metrics(Game g) {
        V resultMetrics = _population_metrics(g);
        resultMetrics["currency"] = V.From(g.s.Get("currency",0).Double);
        resultMetrics["energy"] = V.From(g.s.Get("metrics",V.Map()).Get("energy_ratio",0).Double);
        double hours = Math.Max(1e-15,g.s.Get("audit",V.Map()).Get("hours",0).Double);
        V resources = g.s.Get("audit",V.Map()).Get("resources",V.Map());
        resultMetrics["goods_rate"] = V.From((resources.Get("goods",V.Map()).Get("produced",0).Double / hours));
        resultMetrics["food_rate"] = V.From((resources.Get("food",V.Map()).Get("produced",0).Double / hours));
        resultMetrics["food_self_sufficiency"] = V.From((resultMetrics["food_rate"].Double / Math.Max(1e-06,g.s.Get("audit",V.Map()).Get("population_food_demand_rate",resultMetrics["food_demand"]).Double)));
        resultMetrics["food_stock_months"] = V.From((g.inventory_total("food") / Math.Max(1e-06,resultMetrics["food_demand"].Double)));
        resultMetrics["food_surplus"] = V.From((resultMetrics["food_rate"].Double - resultMetrics["food_demand"].Double));
        resultMetrics["vacancies"] = V.From(Math.Max(0,(g.s.Get("metrics",V.Map()).Get("housing",0).Double - resultMetrics["housed"].Double)));
        resultMetrics["industrial_labor"] = V.From(0.0);
        resultMetrics["industrial_usage"] = V.From(0.0);
        resultMetrics["commercial_usage"] = V.From(0.0);
        V weights = V.Map("industrial", 0.0, "commercial", 0.0);
        V effective_labor = V.Map();
        foreach (var link in g.s.Get("metrics",V.Map()).Get("employment_links",V.Arr())) {
            string target = link.Get("target","").Text;
            effective_labor[target] = V.From((effective_labor.Get(target,0).Double + Math.Max(0.0,link.Get("effective",0).Double)));
        }
        foreach (var id in g.playable_ids()) {
            V p = g.s["projects"][id];
            V st = g.compiled.Get("stats",V.Map()).Get(id,V.Map());
            V basis = g.c.projects.Get(p.Get("def","").Text,V.Map()).Get("stats",V.Map());
            bool industrial = (p.Get("traits",V.Arr()).Contains("building.industrial"));
            bool commercial = ((p.Get("traits",V.Arr()).Contains("building.commercial")) || (p.Get("labels",V.Arr()).Contains("use.commercial")));
            if (industrial) {
                double weight = Math.Max(0.0,((basis.Get("output.material",0).Double + basis.Get("output.goods",0).Double) + basis.Get("output.energy",0).Double));
                weights["industrial"] += V.From(weight);
                if (((p.Get("status","") == "running") && !(p.Get("paused",false).Bool))) {
                    resultMetrics["industrial_usage"] += V.From((weight * p.Get("usage",0).Double));
                    resultMetrics["industrial_labor"] += V.From(effective_labor.Get(id.Text,0).Double);
                }
            }
            if (commercial) {
                double weight = Math.Max(0.0,basis.Get("service.commercial",0).Double);
                if ((weight <= 0)) { weight = Math.Max(0.0,st.Get("service.commercial",0).Double); }
                weights["commercial"] += V.From(weight);
                if (((p.Get("status","") == "running") && !(p.Get("paused",false).Bool))) { resultMetrics["commercial_usage"] += V.From((weight * p.Get("usage",0).Double)); }
            }
        }
        resultMetrics["industrial_usage"] /= V.From(Math.Max(1e-06,weights["industrial"].Double));
        resultMetrics["commercial_usage"] /= V.From(Math.Max(1e-06,weights["commercial"].Double));
        return resultMetrics;
    }
    public static bool _met(double value, V goal) {
        if ((goal.Has("min") && ((value + 1e-06) < goal["min"].Double))) { return false; }
        if ((goal.Has("max") && ((value - 1e-06) > goal["max"].Double))) { return false; }
        return true;
    }
    public static bool _state_conditions(V stage, V metrics) {
        foreach (var goal in stage.Get("goals",V.Arr())) {
            if ((goal.Get("kind","state").Text != "state")) { continue; }
            if (!(_met(metrics.Get(goal["metric"].Text,0).Double,goal))) { return false; }
        }
        return true;
    }
    public static V _reward(Game g, string source, string theme_id, string kind, V custom_ids = null) {
        custom_ids ??= V.Arr();
        V ids = (!((custom_ids.Count == 0)) ? custom_ids : _definition(g,theme_id).Get("bonus_ids",V.Arr()));
        return Bonus.draw(g,source,V.Arr(theme_id),V.Map("kind", kind, "bonus_ids", ids));
    }
    public static void _finish_if_ready(Game g, V metrics) {
        V t = g.s["block"]["theme"];
        if (((t["id"].Text == "") || t["completed"].Bool)) { return; }
        V stage = _stage(g);
        if (((t["steady_seconds"].Double + 0.0001) < (stage.Get("hold_months",0).Double * MONTH_SECONDS))) { return; }
        foreach (var goal in stage.Get("goals",V.Arr())) {
            double value = ((goal["kind"].Text == "state") ? metrics.Get(goal["metric"].Text,0).Double : t["counters"].Get(goal["metric"].Text,0).Double);
            if (!(_met(value,goal))) { return; }
        }
        string milestone = ((t["id"].Text + ":M") + t["stage"].Text);
        if (!((t["milestone_ids"].Contains(milestone)))) {
            t["milestone_ids"].Add(milestone);
            _reward(g,("theme:" + milestone),t["id"].Text,(((int)t["stage"].Long == 1) ? "copy" : "research"));
            _record(g,"milestone",V.Map("id", milestone));
            g.log_event("theme_milestone",V.Map("id", milestone),(((_definition(g,t["id"].Text)["name"].Text + "·") + stage["name"].Text) + "目标达成。"));
        }
        if (((int)t["stage"].Long >= 3)) {
            t["completed"] = V.From(true);
            g.log_event("theme_completed",V.Map("id", t["id"]),(_definition(g,t["id"].Text)["name"].Text + "建设目标达成，地区发展有了新的方向。"));
        }
        else {
            t["stage"] = V.From(((int)t["stage"].Long + 1));
            t["steady_seconds"] = V.From(0.0);
            t["counters"] = V.Map();
            t["trade_cursor"] = V.From(g.s["block"].Get("trade_history",V.Arr()).Count);
            t["started_seconds"] = V.From(_now(g));
        }
        _pause(g,"发展主题阶段完成，请查看奖励与下一目标。");
    }
    public static void tick(Game g, double local_seconds) {
        if (((local_seconds <= 0) || g.preview_mode)) { return; }
        ensure(g);
        V t = g.s["block"]["theme"];
        int tick_id = (int)g.s.Get("simulation_tick",0).Long;
        if ((tick_id <= (int)t.Get("last_tick",-(1)).Long)) { return; }
        t["last_tick"] = V.From(tick_id);
        V metrics = _metrics(g);
        V previous_metrics = t["last_metrics"];
        t["last_metrics"] = metrics;
        V history = g.s["block"].Get("trade_history",V.Arr());
        int cursor = Math.Min(history.Count, Math.Max(0,(int)t["trade_cursor"].Long));
        V trades = V.From(history.Items.Skip(cursor));
        t["trade_cursor"] = V.From(history.Count);
        if (((t["id"].Text != "") && !(t["completed"].Bool))) {
            foreach (var row in trades) { t["counters"]["trade"] = V.From((t["counters"].Get("trade",0).Double + Math.Max(0,row.Get("amount",0).Double))); }
            V stage = _stage(g);
            bool conditions = _state_conditions(stage,metrics);
            bool held_before = _state_conditions(stage,previous_metrics);
            t["steady_seconds"] = V.From(((conditions && held_before) ? (t["steady_seconds"].Double + local_seconds) : 0.0));
            if (conditions) {
                t["counters"]["commercial_person_months"] = V.From((t["counters"].Get("commercial_person_months",0).Double + ((metrics["commercial_people"].Double * local_seconds) / MONTH_SECONDS)));
            }
            _finish_if_ready(g,metrics);
        }
        _update_events(g,metrics,local_seconds);
        _arrivals(g);
    }
    public static bool _audit_delivery(Game g, string resource, double amount, string source) {
        if (((amount <= 0) || !(g.withdraw_stock(resource,amount)))) { return false; }
        if (!((g.s["block"].Get("theme_deliveries").IsArray))) { g.s["block"]["theme_deliveries"] = V.Arr(); }
        g.s["block"]["theme_deliveries"].Add(V.Map("source", source, "resource", resource, "amount", amount, "local_seconds", _now(g)));
        V receipt = g.s.Get("audit",V.Map()).Get("resources",V.Map()).Get(resource,V.Map());
        if (!((receipt.Count == 0))) {
            receipt["commission_delivered"] = V.From((receipt.Get("commission_delivered",0).Double + amount));
            receipt["after"] = V.From(g.inventory_total(resource));
            receipt["error"] = V.From((((((((receipt.Get("before",0).Double + receipt.Get("produced",0).Double) + receipt.Get("imported",0).Double) - receipt.Get("consumed",0).Double) - receipt.Get("lost",0).Double) - receipt.Get("exported",0).Double) - receipt["commission_delivered"].Double) - receipt["after"].Double));
        }
        return true;
    }
    public static string deliver(Game g, string resource = "", double amount = -(1.0), string event_id = "") {
        ensure(g);
        if ((g.s.Get("phase","") != "play")) { return "当前不能交付。"; }
        if ((!((!double.IsInfinity(amount) && !double.IsNaN(amount))) || ((amount <= 0) && (amount != -(1.0))))) { return "交付数量无效。"; }
        if ((event_id != "")) {
            string error = _deliver_event(g,event_id,amount);
            if ((error == "")) { g.revision += 1; }
            return error;
        }
        V t = g.s["block"]["theme"];
        if (((t["id"].Text == "") || t["completed"].Bool)) { return "当前没有待交付的主题目标。"; }
        V goal = V.Map();
        foreach (var item in _stage(g).Get("goals",V.Arr())) {
            if ((item.Get("kind","").Text != "delivery")) { continue; }
            if (((resource == "") || (item["metric"].Text == ("delivery_" + resource)))) {
                if (((t["counters"].Get(item["metric"].Text,0).Double + 1e-06) >= item["min"].Double)) { continue; }
                goal = item;
                break;
            }
        }
        if ((goal.Count == 0)) { return "该资源没有未完成的主动交付目标。"; }
        resource = goal["metric"].Text.Substring("delivery_".Length);
        double remaining = Math.Max(0,(goal["min"].Double - t["counters"].Get(goal["metric"].Text,0).Double));
        double available = g.inventory_total(resource);
        if (((resource == "food") && (t["id"].Text == "agricultural"))) { available = Math.Max(0.0,(available - (3.0 * _population_metrics(g)["food_demand"].Double))); }
        double take = Math.Min(remaining,available);
        if ((amount > 0)) { take = Math.Min(take,amount); }
        if ((take <= 1e-06)) { return ((resource == "food") ? "可交付库存不足；农业主题必须保留3月口粮。" : "没有可交付库存。"); }
        if (!(_audit_delivery(g,resource,take,((("theme:" + t["id"].Text) + ":M") + t["stage"].Text)))) { return "库存不足，未交付。"; }
        t["counters"][goal["metric"].Text] = V.From((t["counters"].Get(goal["metric"].Text,0).Double + take));
        _finish_if_ready(g,_metrics(g));
        g.revision += 1;
        return "";
    }
    public static bool _has_production_slot(Game g) {
        foreach (var p in V.From(g.s["projects"].Values)) {
            foreach (var slot in p.Get("slots",V.Map())) {
                if (((p["slots"][slot].Text == "") && (p.Get("slot_types",V.Map()).Get(slot,slot).Text == "Production"))) { return true; }
            }
        }
        return false;
    }
    public static bool _has_service(Game g) {
        foreach (var id in g.playable_ids()) {
            if ((g.s["projects"][id].Get("status","") != "running")) { continue; }
            V st = g.compiled.Get("stats",V.Map()).Get(id,V.Map());
            foreach (var kind in V.Arr("medical", "education", "commercial", "entertainment")) {
                if ((st.Get(("service." + kind),0).Double > 0)) { return true; }
            }
        }
        return false;
    }
    public static double _storage_ratio(Game g) {
        double current = 0.0;
        double cap = 0.0;
        foreach (var id in g.s["projects"]) {
            V st = g.compiled.Get("stats",V.Map()).Get(id,V.Map());
            foreach (var r in V.Arr("food", "material", "goods")) {
                current += g.s["projects"][id].Get("inventory",V.Map()).Get(r,0).Double;
                cap += Math.Max(0.0,st.Get(("storage." + r),0).Double);
            }
        }
        return (current / Math.Max(1e-06,cap));
    }
    public static bool _eligible(Game g, V definition, V metrics) {
        V t = g.s["block"]["theme"];
        if (((definition["theme"].Text != t["id"].Text) || (t["event_ids"].Contains(definition["id"].Text)))) { return false; }
        V trigger = definition.Get("trigger",V.Map());
        foreach (var key in trigger) {
            V expected = trigger[key];
            if (key.Text.EndsWith("_min")) {
                double value = ((key == "storage_ratio_min") ? _storage_ratio(g) : metrics.Get(key.Text.Substring(0,key.Text.Length-"_min".Length),0).Double);
                if (((key == "food_surplus_min") && (value <= 0))) { return false; }
                if (((value + 1e-06) < expected.Double)) { return false; }
            }
            else if (key.Text.EndsWith("_max")) {
                if ((metrics.Get(key.Text.Substring(0,key.Text.Length-"_max".Length),0).Double >= expected.Double)) { return false; }
            }
            else {
                switch (key.Text) {
                    case "milestone": {
                        if (!((t["milestone_ids"].Contains(((t["id"].Text + ":M") + V.From((int)expected.Long).Text))))) { return false; }
                        break;
                    }
                    case "production_slot": {
                        if (!(_has_production_slot(g))) { return false; }
                        break;
                    }
                    case "service_building": {
                        if (!(_has_service(g))) { return false; }
                        break;
                    }
                    case "freight": {
                        if ((Market._total_capacity(g) <= 0)) { return false; }
                        break;
                    }
                    case "has_trade": {
                        if ((g.s["block"].Get("trade_history",V.Arr()).Count == 0)) { return false; }
                        break;
                    }
                    case "actual_overflow": {
                        double spill = 0.0;
                        foreach (var n in V.From(g.s.Get("metrics",V.Map()).Get("spilled",V.Map()).Values)) { spill += n.Double; }
                        if ((spill <= 1e-06)) { return false; }
                        break;
                    }
                    case "research": {
                        if ((Bonus.research_candidates(g,_definition(g,t["id"].Text).Get("bonus_ids",V.Arr()),V.Arr()).Count == 0)) { return false; }
                        break;
                    }
                }
            }
        }
        return true;
    }
    public static void on_major_cycle(Game g, int index) {
        ensure(g);
        V t = g.s["block"]["theme"];
        if ((index <= (int)t["last_major_cycle"].Long)) { return; }
        t["last_major_cycle"] = V.From(index);
        if ((t["id"].Text == "")) { return; }
        foreach (var e in t["events"]) {
            if ((V.Arr("offered", "active").Contains(e.Get("status","").Text))) { return; }
        }
        V candidates = V.Arr();
        V weights = V.Arr();
        V metrics = _metrics(g);
        foreach (var definition in _events(g)) {
            if (_eligible(g,definition,metrics)) {
                candidates.Add(definition);
                weights.Add(definition.Get("weight",1).Double);
            }
        }
        if (!((candidates.Count == 0))) {
            V picked = Rng.pick_weighted(g.s,"theme_events",candidates,weights);
            t["event_ids"].Add(picked["id"].Text);
            t["events"].Add(V.Map("id", picked["id"].Text, "status", "offered", "revealed_seconds", _now(g), "theme", t["id"]));
            _pause(g,"新的主题事件等待选择。");
        }
        if ((!(t["completed"].Bool) && (Rng.next(g.s,"theme_events") < _token_chance(g)))) {
            string id = ("reforge_opportunity:" + V.From(index).Text);
            t["events"].Add(V.Map("id", id, "status", "offered", "revealed_seconds", _now(g), "theme", t["id"], "token", true));
            t["event_ids"].Add(id);
            _pause(g,string.Format(System.Globalization.CultureInfo.InvariantCulture,"发现稀有委任重铸权；使用仍需{0:F2} DT。",_reforge_cost(g)));
        }
    }
    public static V _find_event(Game g, string id) {
        foreach (var evt in g.s["block"]["theme"]["events"]) {
            if ((evt["id"].Text == id)) { return evt; }
        }
        return V.Map();
    }
    public static string choose_event(Game g, string event_id, string choice_id) {
        ensure(g);
        if ((g.s.Get("phase","") != "play")) { return "当前不能处理事件。"; }
        V evt = _find_event(g,event_id);
        if (((evt.Count == 0) || (evt.Get("status","").Text != "offered"))) { return "该事件已经处理或不存在。"; }
        if ((!V.Arr("accept", "decline").Contains(choice_id))) { return "未知事件选择。"; }
        if ((choice_id == "decline")) {
            evt["status"] = V.From("declined");
            _record(g,"event_declined",V.Map("id", event_id));
            g.revision += 1;
            return "";
        }
        if (evt.Get("token",false).Bool) {
            g.s["block"]["theme"]["reforge_tokens"] = V.From(((int)g.s["block"]["theme"]["reforge_tokens"].Long + 1));
            evt["status"] = V.From("completed");
            _record(g,"reforge_token",V.Map("id", event_id, "use_dt_cost", _reforge_cost(g)));
            g.revision += 1;
            return "";
        }
        V definition = _event_definition(g,event_id);
        if ((definition.Count == 0)) { return "事件定义不存在。"; }
        double baseline = _metrics(g).Get(definition.Get("metric","").Text,0).Double;
        if ((definition.Has("improve") && ((baseline + definition["improve"].Double) > 1.000001))) {
            return "当前覆盖率已较高，无法再提高合同要求的百分点；可以免费婉拒。";
        }
        double cost = (definition.Get("currency",0).Double + definition.Get("deposit",0).Double);
        if (((g.s["currency"].Double + 1e-06) < cost)) { return "通货不足，尚未接受事件。"; }
        foreach (var r in definition.Get("resources",V.Map())) {
            if (((g.inventory_total(r.Text) + 1e-06) < definition["resources"][r].Double)) { return "所需库存不足，尚未接受事件。"; }
        }
        g.s["currency"] = V.From((g.s["currency"].Double - cost));
        foreach (var r in definition.Get("resources",V.Map())) { _audit_delivery(g,r.Text,definition["resources"][r].Double,("event:" + event_id)); }
        evt["status"] = V.From("active");
        evt["accepted_seconds"] = V.From(_now(g));
        evt["deposit"] = V.From(definition.Get("deposit",0).Double);
        evt["deadline_at"] = V.From(((definition.Get("deadline_months",0).Double > 0) ? (_now(g) + (definition.Get("deadline_months",0).Double * MONTH_SECONDS)) : -(1.0)));
        evt["trade_cursor"] = V.From(g.s["block"].Get("trade_history",V.Arr()).Count);
        evt["progress"] = V.From(0.0);
        evt["baseline"] = V.From(baseline);
        _record(g,"event_accepted",V.Map("id", event_id, "currency_cost", cost));
        if (definition.Has("arrival_people")) {
            double due = ((Math.Floor((_now(g) / MONTH_SECONDS)) + 1.0) * MONTH_SECONDS);
            g.s["block"]["theme"]["arrivals"].Add(V.Map("event_id", event_id, "people", definition["arrival_people"].Double, "due_seconds", due, "arrived", false));
            _complete_event(g,evt,definition);
        }
        else if ((!(definition.Has("metric")) && !(definition.Has("delivery_resource")) && !(definition.Has("trade_amount")) && !(definition.Has("person_months")))) {
            _complete_event(g,evt,definition);
        }
        g.revision += 1;
        return "";
    }
    public static V _event_pool(Game g, V definition) {
        if (definition.Has("bonus_ids")) { return definition["bonus_ids"]; }
        V ids = V.Arr();
        foreach (var theme_id in definition.Get("reward_themes",V.Arr(definition["theme"].Text))) {
            foreach (var id in _definition(g,theme_id.Text).Get("bonus_ids",V.Arr())) {
                if (!((ids.Contains(id)))) { ids.Add(id); }
            }
        }
        return ids;
    }
    public static void _complete_event(Game g, V evt, V definition) {
        if ((evt["status"].Text != "active")) { return; }
        evt["status"] = V.From("completed");
        evt["finished_seconds"] = V.From(_now(g));
        g.s["currency"] = V.From(((g.s["currency"].Double + evt.Get("deposit",0).Double) + definition.Get("cash_reward",0).Double));
        evt["deposit"] = V.From(0.0);
        if (definition.Has("reward_kind")) {
            _reward(g,("theme_event:" + evt["id"].Text),definition["theme"].Text,definition["reward_kind"].Text,_event_pool(g,definition));
        }
        _record(g,"event_completed",V.Map("id", evt["id"]));
        g.log_event("theme_event_completed",V.Map("id", evt["id"]),(definition["name"].Text + "已完成。"));
    }
    public static void _update_events(Game g, V metrics, double local_seconds) {
        foreach (var evt in g.s["block"]["theme"]["events"]) {
            if ((evt.Get("status","").Text != "active")) { continue; }
            V definition = _event_definition(g,evt["id"].Text);
            if ((definition.Count == 0)) { continue; }
            double deadline = evt.Get("deadline_at",-(1)).Double;
            if (((deadline >= 0) && (_now(g) > (deadline + 0.0001)))) {
                _fail_event(g,evt);
                continue;
            }
            if (definition.Has("trade_amount")) {
                V history = g.s["block"].Get("trade_history",V.Arr());
                foreach (var row in V.From(history.Items.Skip(Math.Min(history.Count, Math.Max(0,(int)evt.Get("trade_cursor",0).Long))))) {
                    if ((definition.Has("trade_side") && (row.Get("side","").Text != definition["trade_side"].Text))) { continue; }
                    if ((definition.Has("trade_resource") && (row.Get("resource","").Text != definition["trade_resource"].Text))) { continue; }
                    evt["progress"] = V.From((evt["progress"].Double + Math.Max(0,row.Get("amount",0).Double)));
                }
                evt["trade_cursor"] = V.From(history.Count);
            }
            if (definition.Has("person_months")) { evt["progress"] = V.From((evt["progress"].Double + ((metrics["commercial_people"].Double * local_seconds) / MONTH_SECONDS))); }
            bool done = false;
            if (definition.Has("metric")) { done = ((metrics.Get(definition["metric"].Text,0).Double + 1e-06) >= (evt["baseline"].Double + definition.Get("improve",0).Double)); }
            if (definition.Has("trade_amount")) { done = ((evt["progress"].Double + 1e-06) >= definition["trade_amount"].Double); }
            if (definition.Has("person_months")) { done = ((evt["progress"].Double + 1e-06) >= definition["person_months"].Double); }
            if (definition.Has("delivery_amount")) { done = ((evt["progress"].Double + 1e-06) >= definition["delivery_amount"].Double); }
            if (done) {
                _complete_event(g,evt,definition);
            }
            else if (((deadline >= 0) && ((_now(g) + 0.0001) >= deadline))) {
                _fail_event(g,evt);
            }
        }
    }
    public static void _fail_event(Game g, V evt) {
        evt["status"] = V.From("failed");
        evt["finished_seconds"] = V.From(_now(g));
        evt["deposit"] = V.From(0.0);
        _record(g,"event_failed",V.Map("id", evt["id"], "reason", "超过本地时间期限；仅失去该机会与已支付保证金。"));
    }
    public static string _deliver_event(Game g, string id, double amount) {
        V evt = _find_event(g,id);
        V definition = _event_definition(g,id);
        if (((evt.Count == 0) || (evt.Get("status","").Text != "active") || !(definition.Has("delivery_resource")))) { return "没有可主动交付的事件合同。"; }
        if (((evt.Get("deadline_at",-(1)).Double >= 0) && (_now(g) > (evt["deadline_at"].Double + 0.0001)))) { return "该合同已经超过本地时间期限。"; }
        string resource = definition["delivery_resource"].Text;
        double available = g.inventory_total(resource);
        if ((resource == "food")) { available -= (definition.Get("reserve_months",0).Double * _population_metrics(g)["food_demand"].Double); }
        double take = Math.Min(Math.Max(0,available),Math.Max(0,(definition["delivery_amount"].Double - evt["progress"].Double)));
        if ((amount > 0)) { take = Math.Min(take,amount); }
        if ((take <= 1e-06)) { return "库存不足或交付后口粮不满足合同要求。"; }
        if (!(_audit_delivery(g,resource,take,("event:" + id)))) { return "库存不足，未交付。"; }
        evt["progress"] = V.From((evt["progress"].Double + take));
        if (((evt["progress"].Double + 1e-06) >= definition["delivery_amount"].Double)) { _complete_event(g,evt,definition); }
        return "";
    }
    public static void _arrivals(Game g) {
        bool arrived = false;
        foreach (var row in g.s["block"]["theme"]["arrivals"]) {
            if ((row.Get("arrived",false).Bool || ((_now(g) + 0.0001) < row["due_seconds"].Double))) { continue; }
            Population.create(g,"homeless",row["people"].Double);
            row["arrived"] = V.From(true);
            arrived = true;
            _record(g,"event_arrival",V.Map("id", row["event_id"], "people", row["people"]));
        }
        if (arrived) {
            Population.rehouse(g);
            g.refresh_metrics();
        }
    }
    public static double next_boundary_seconds(Game g) {
        V t = g.s.Get("block",V.Map()).Get("theme",V.Map());
        if ((t.Count == 0)) { return double.PositiveInfinity; }
        double result = double.PositiveInfinity;
        if (((t.Get("id","").Text != "") && !(t.Get("completed",false).Bool))) {
            V stage = _stage(g);
            double remaining = ((stage.Get("hold_months",0).Double * MONTH_SECONDS) - t.Get("steady_seconds",0).Double);
            if ((remaining > 0.0001)) { result = Math.Min(result,remaining); }
        }
        foreach (var evt in t.Get("events",V.Arr())) {
            if ((evt.Get("status","").Text != "active")) { continue; }
            double remaining = (evt.Get("deadline_at",-(1)).Double - _now(g));
            if ((remaining > 0.0001)) { result = Math.Min(result,remaining); }
        }
        foreach (var row in t.Get("arrivals",V.Arr())) {
            if (row.Get("arrived",false).Bool) { continue; }
            double remaining = (row["due_seconds"].Double - _now(g));
            if ((remaining > 0.0001)) { result = Math.Min(result,remaining); }
        }
        return result;
    }
    public static V _event_view(Game g, V evt) {
        bool token = evt.Get("token",false).Bool;
        V definition = _event_definition(g,evt["id"].Text);
        V row = V.Map("id", evt["id"].Text, "name", (token ? "委任重铸权" : definition.Get("name",evt["id"]).Text), "status", evt.Get("status","").Text, "description", (token ? string.Format(System.Globalization.CultureInfo.InvariantCulture,"获得一次重铸权；使用仍需{0:F2} DT，完成主题后不可更换。",_reforge_cost(g)) : definition.Get("description","").Text), "deadline_seconds", ((evt.Get("deadline_at",-(1)).Double >= 0) ? Math.Max(0,(evt.Get("deadline_at",-(1)).Double - _now(g))) : -(1.0)), "progress", evt.Get("progress",0).Double, "target", definition.Get("delivery_amount",definition.Get("trade_amount",definition.Get("person_months",0))).Double, "choices", V.Arr(), "delivery_resource", definition.Get("delivery_resource","").Text, "delivery_remaining", Math.Max(0,(definition.Get("delivery_amount",0).Double - evt.Get("progress",0).Double)));
        if (definition.Has("metric")) {
            row["progress"] = V.From(_metrics(g).Get(definition["metric"].Text,0).Double);
            row["target"] = V.From((evt.Get("baseline",row["progress"]).Double + definition.Get("improve",0).Double));
            row["unit"] = V.From("%");
            row["description"] += V.From(string.Format(System.Globalization.CultureInfo.InvariantCulture," 目标实际覆盖：{0:F0}%。",(row["target"].Double * 100.0)));
        }
        if ((row["status"] == "offered")) {
            row["choices"] = V.Arr(V.Map("id", "accept", "label", "接受", "cost_text", row["description"]), V.Map("id", "decline", "label", "婉拒", "cost_text", "不支付代价，不附加惩罚。"));
        }
        return row;
    }
    public static V _history_view(Game g, V entry) {
        V row = entry.Duplicate(true);
        string id = row.Get("id","").Text;
        string kind = row.Get("kind","").Text;
        V evt = _event_definition(g,id);
        V parts = V.From(id.Split(":".ToCharArray()));
        V theme = (!((parts.Count == 0)) ? _definition(g,parts[0].Text) : V.Map());
        row["name"] = V.From(evt.Get("name",theme.Get("name",id)).Text);
        if (id.StartsWith("reforge_opportunity:")) { row["name"] = V.From("委任重铸权"); }
        if (((kind == "milestone") && (parts.Count > 1))) {
            V names = V.Map("M1", "立足", "M2", "成形", "M3", "证明");
            row["name"] += V.From((" · " + names.Get(parts[1],parts[1]).Text));
        }
        row["status_label"] = V.Map("selected", "确定发展主题", "reforged", "委任已重铸", "milestone", "阶段达成", "event_declined", "已婉拒", "event_accepted", "已接受", "event_completed", "已完成", "event_failed", "逾期未达成", "event_arrival", "居民已抵达", "reforge_token", "获得重铸权").Get(kind,"城市记录");
        row["reason"] = V.From(row.Get("reason","").Text);
        if ((kind == "reforged")) {
            row["reason"] = V.From(string.Format(System.Globalization.CultureInfo.InvariantCulture,"从「{0}」改向，消耗1份重铸权与{1:F2} DT；已获得和待领取奖励保留。",_definition(g,row.Get("from","").Text).Get("name","原主题").Text,row.Get("dt_cost",0).Double));
        }
        else if ((kind == "event_declined")) { row["reason"] = V.From("未支付代价，没有额外惩罚。"); }
        else if ((kind == "event_accepted")) {
            row["reason"] = V.From(evt.Get("description","").Text);
            if ((row.Get("currency_cost",0).Double > 0)) { row["reason"] += V.From(string.Format(System.Globalization.CultureInfo.InvariantCulture," 已支付{0:F1}通货。",row["currency_cost"].Double)); }
        }
        else if ((kind == "event_arrival")) { row["reason"] = V.From(string.Format(System.Globalization.CultureInfo.InvariantCulture,"本地时间约定已到，{0:F0}位新居民实际抵达。",row.Get("people",0).Double)); }
        else if ((kind == "reforge_token")) { row["reason"] = V.From(string.Format(System.Globalization.CultureInfo.InvariantCulture,"使用仍需{0:F2} DT；获得重铸权不等于免费换向。",row.Get("use_dt_cost",0).Double)); }
        return row;
    }
    public static V view(Game g) {
        V t = g.s.Get("block",V.Map()).Get("theme",V.Map());
        string id = t.Get("id","").Text;
        V definition = _definition(g,id);
        V row = V.Map("id", id, "name", definition.Get("name","尚未选择发展主题").Text, "stage", (int)t.Get("stage",1).Long, "completed", t.Get("completed",false).Bool, "stage_name", "", "goals", V.Arr(), "steady_seconds", t.Get("steady_seconds",0).Double, "steady_required_seconds", 0.0, "reforge_tokens", (int)t.Get("reforge_tokens",0).Long, "reforge_dt_cost", _reforge_cost(g), "reforge_opportunity_chance", _token_chance(g), "can_reforge", ((id != "") && !(t.Get("completed",false).Bool) && ((int)t.Get("reforge_tokens",0).Long > 0) && (g.s.Get("dt",0).Double >= _reforge_cost(g))), "choices", V.Arr(), "events", V.Arr(), "history", V.Arr());
        foreach (var entry in t.Get("history",V.Arr())) { row["history"].Add(_history_view(g,entry)); }
        foreach (var item in _themes(g)) { row["choices"].Add(V.Map("id", item["id"].Text, "name", item["name"].Text, "description", item["description"].Text)); }
        if (!((definition.Count == 0))) {
            V stage = definition["stages"][Math.Min(2, Math.Max(0,((int)row["stage"].Long - 1)))];
            row["stage_name"] = V.From(stage["name"].Text);
            row["steady_required_seconds"] = V.From((stage.Get("hold_months",0).Double * MONTH_SECONDS));
            V metrics = _metrics(g);
            foreach (var goal in stage["goals"]) {
                double value = ((goal["kind"].Text == "state") ? metrics.Get(goal["metric"].Text,0).Double : t.Get("counters",V.Map()).Get(goal["metric"].Text,0).Double);
                row["goals"].Add(V.Map("id", goal["metric"].Text, "label", goal["label"].Text, "value", value, "target", goal.Get("min",goal.Get("max",0)).Double, "unit", goal["unit"].Text, "met", _met(value,goal), "comparison", (goal.Has("max") ? "max" : "min"), "kind", goal["kind"].Text));
            }
        }
        foreach (var evt in t.Get("events",V.Arr())) {
            if ((V.Arr("offered", "active").Contains(evt.Get("status","").Text))) { row["events"].Add(_event_view(g,evt)); }
        }
        return row;
    }
}
}
