using System;
using System.Linq;
namespace MET.Core {
public static class Commissions {
    public static void ensure(Game g) {
        if (!((g.s.Get("block").IsMap))) { g.s["block"] = V.Map("id", "block:1"); }
        V block = g.s["block"];
        if (!((block.Get("commissions").IsMap))) {
            block["commissions"] = V.Map("active", V.Map(), "history", V.Arr(), "completed", V.Arr(), "pending_reward", "");
        }
        V state = block["commissions"];
        if (!((state.Get("active").IsMap))) { state["active"] = V.Map(); }
        if (!((state.Get("history").IsArray))) { state["history"] = V.Arr(); }
        if (!((state.Get("completed").IsArray))) { state["completed"] = V.Arr(); }
        if (!(state.Has("pending_reward"))) { state["pending_reward"] = V.From(""); }
        g.s["block"]["commissions"] = state;
        if (!((block.Get("trade_history").IsArray))) { block["trade_history"] = V.Arr(); }
    }
    public static V _definitions(Game g) {
        V value = g.c.v040.Get("commissions",V.Arr());
        return ((value.IsArray) ? value : V.Arr());
    }
    public static V _definition(Game g, string commission_id) {
        foreach (var item in _definitions(g)) {
            if ((item.Get("id","").Text == commission_id)) { return item; }
        }
        return V.Map();
    }
    public static V _active(Game g) {
        ensure(g);
        return g.s["block"]["commissions"]["active"];
    }
    public static double _trade_capacity(Game g) {
        double total = 0.0;
        V stats = g.compiled.Get("stats",V.Map());
        foreach (var id in g.playable_ids()) {
            V p = g.s["projects"][id];
            if (((p.Get("status","") != "running") || p.Get("paused",false).Bool)) { continue; }
            double capacity = stats.Get(id,V.Map()).Get("trade_capacity",g.c.projects.Get(p.Get("def",""),V.Map()).Get("stats",V.Map()).Get("trade_capacity",0)).Double;
            if ((capacity <= 0)) { continue; }
            total += (capacity * Math.Max(0.0,p.Get("usage",1.0).Double));
        }
        return total;
    }
    public static int _max_tier(Game g) {
        int result = 0;
        foreach (var id in g.playable_ids()) { result = Math.Max(result,(int)g.s["projects"][id].Get("tier",1).Long); }
        return result;
    }
    public static double _actual_service_ratio(Game g, string kind) {
        double delivered = 0.0;
        double people = 0.0;
        foreach (var group in V.From(g.s.Get("groups",V.Map()).Values)) {
            double count = group.Get("count",0).Double;
            people += count;
            delivered += (count * group.Get("living",V.Map()).Get(kind,0).Double);
        }
        return (delivered / Math.Max(0.001,people));
    }
    public static double _food_ratio(Game g) {
        double delivered = 0.0;
        double people = 0.0;
        foreach (var group in V.From(g.s.Get("groups",V.Map()).Values)) {
            double count = group.Get("count",0).Double;
            people += count;
            delivered += (count * group.Get("living",V.Map()).Get("food",1).Double);
        }
        return (delivered / Math.Max(0.001,people));
    }
    public static V _history_slice(Game g, V active) {
        V rows = g.s["block"].Get("trade_history",V.Arr());
        int start = Math.Min(rows.Count, Math.Max(0,(int)active.Get("trade_cursor",0).Long));
        return V.From(rows.Items.Skip(start));
    }
    public static double _organization_links(Game g) {
        V edges = V.Map();
        V projects = g.s.Get("projects",V.Map());
        V groups = g.s.Get("groups",V.Map());
        V metrics = g.s.Get("metrics",V.Map());
        foreach (var row in metrics.Get("employment_links",V.Arr())) {
            string home = groups.Get(row.Get("group","").Text,V.Map()).Get("host","").Text;
            string target = row.Get("target","").Text;
            _add_org_edge(edges,projects,home,target,row.Get("people",0).Double);
        }
        foreach (var row in metrics.Get("service_links",V.Arr())) {
            string source = row.Get("source","").Text;
            string home = row.Get("home","").Text;
            _add_org_edge(edges,projects,source,home,row.Get("people",0).Double);
        }
        foreach (var owner in metrics.Get("activity_ledger",V.Map())) {
            foreach (var row in metrics["activity_ledger"][owner]) {
                if ((row.Get("kind","") != "input")) { continue; }
                _add_org_edge(edges,projects,row.Get("source_id","").Text,owner.Text,row.Get("rate",0).Double);
            }
        }
        return edges.Count;
    }
    public static void _add_org_edge(V edges, V projects, string source, string target, double amount) {
        if (((amount <= 1e-06) || (source == target))) { return; }
        if ((!(projects.Has(source)) || !(projects.Has(target)))) { return; }
        V a = projects[source];
        V b = projects[target];
        if ((a.Get("special",false).Bool || b.Get("special",false).Bool)) { return; }
        if ((a.Get("operator_org","").Text != b.Get("operator_org","").Text)) { return; }
        string left = a.Get("id",source).Text;
        string right = b.Get("id",target).Text;
        if ((StringComparer.Ordinal.Compare(left,right) > 0)) {
            string swap = left;
            left = right;
            right = swap;
        }
        edges[((left + "|") + right)] = V.From(true);
    }
    public static double _metric(Game g, string metric, V active) {
        V m = g.s.Get("metrics",V.Map());
        switch (metric) {
            case "food_ratio": {
                return _food_ratio(g);

            }
            case "medical": {
                return _actual_service_ratio(g,"medical");

            }
            case "education": {
                return _actual_service_ratio(g,"education");

            }
            case "commercial": {
                return _actual_service_ratio(g,"commercial");

            }
            case "entertainment": {
                return _actual_service_ratio(g,"entertainment");

            }
            case "tier": {
                return _max_tier(g);

            }
            case "buildings": {
                int count = 0;
                foreach (var id in g.playable_ids()) {
                    if (!(g.s["projects"][id].Get("special",false).Bool)) { count += 1; }
                }
                return count;

            }
            case "trade_capacity": {
                return _trade_capacity(g);

            }
            case "organization_links": {
                return _organization_links(g);

            }
            case "delivery_goods": {
                return active.Get("delivered_goods",0).Double;

            }
            case "adverse_exports": {
                double total = 0.0;
                foreach (var row in _history_slice(g,active)) {
                    if ((row.Get("side","") != "sell")) { continue; }
                    string resource = row.Get("resource","").Text;
                    double basis = g.c.v040.Get("market",V.Map()).Get("resources",V.Map()).Get(resource,V.Map()).Get("base_price",0).Double;
                    if ((row.Get("price",0).Double < basis)) { total += row.Get("amount",0).Double; }
                }
                return total;

            }
            default: {
                if (((m.Get(metric).IsInteger) || (m.Get(metric).IsNumber))) { return m[metric].Double; }
                break;
            }
        }
        return 0.0;
    }
    public static double _trigger_value(Game g, string key) {
        switch (key) {
            case "tier": {
                return _max_tier(g);

            }
            case "buildings": {
                int count = 0;
                foreach (var id in g.playable_ids()) {
                    if (!(g.s["projects"][id].Get("special",false).Bool)) { count += 1; }
                }
                return count;

            }
            case "trade_capacity": {
                return _trade_capacity(g);

            }
            case "housed": {
                return g.s.Get("metrics",V.Map()).Get("housed",0).Double;

            }
            case "population": {
                return g.s.Get("metrics",V.Map()).Get("population",0).Double;

            }
            case "hours": {
                return g.s.Get("local_hours",0).Double;

            }
            default: {
                V value = g.s.Get("metrics",V.Map()).Get(key,0);
                return (((value.IsInteger) || (value.IsNumber)) ? value.Double : 0.0);

            }
        }
    }
    public static V _trigger(Game g, V definition) {
        double gap = 0.0;
        bool near = true;
        foreach (var key in definition.Get("trigger",V.Map())) {
            double target = definition["trigger"][key].Double;
            double current = _trigger_value(g,key.Text);
            double shortfall = Math.Max(0.0,(target - current));
            if ((shortfall <= 1e-06)) { continue; }
            gap += (shortfall / Math.Max(1.0,target));
            double allowance = Math.Max(1.0,(target * 0.25));
            if ((key == "housed")) { allowance = 6.0; }
            if (((key == "tier") || (key == "buildings") || (key == "trade_capacity"))) { allowance = Math.Max(1.0,(target - current)); }
            if ((shortfall > allowance)) { near = false; }
        }
        return V.Map("current", (gap <= 1e-06), "near", near, "gap", gap);
    }
    public static bool _prerequisites_met(V state, V definition) {
        foreach (var required in definition.Get("prerequisites",V.Arr())) {
            if (!((state.Get("completed",V.Arr()).Contains(required.Text)))) { return false; }
        }
        return true;
    }
    public static V _is_available(Game g, V state, V definition) {
        string commission_id = definition.Get("id","").Text;
        if ((commission_id == "")) { return V.Map(); }
        if ((!(definition.Get("repeatable",false).Bool) && (state.Get("completed",V.Arr()).Contains(commission_id)))) { return V.Map(); }
        if (!(_prerequisites_met(state,definition))) { return V.Map(); }
        V status = _trigger(g,definition);
        if ((!(status["current"].Bool) && !(status["near"].Bool))) { return V.Map(); }
        V offer = definition.Duplicate(true);
        offer["current"] = status["current"];
        offer["near_future"] = V.From(!(status["current"].Bool));
        offer["trigger_gap"] = status["gap"];
        return offer;
    }
    public static V available(Game g) {
        ensure(g);
        V state = g.s["block"]["commissions"];
        if ((!((state["active"].Count == 0)) || (state.Get("pending_reward","").Text != ""))) { return V.Arr(); }
        V current = V.Arr();
        V near = V.Arr();
        foreach (var definition in _definitions(g)) {
            V offer = _is_available(g,state,definition);
            if ((offer.Count == 0)) { continue; }
            if (offer["current"].Bool) { current.Add(offer); }
            else { near.Add(offer); }
        }
        current = V.From(current.Items.OrderByDescending(a=>a.Get("weight",0).Double).ThenBy(a=>a["id"].Text,StringComparer.Ordinal));
        near = V.From(near.Items.OrderBy(a=>a.Get("trigger_gap",0).Double).ThenByDescending(a=>a.Get("weight",0).Double).ThenBy(a=>a["id"].Text,StringComparer.Ordinal));
        V result = V.Arr();
        V selected_ids = V.Arr();
        for (int i = 0; i < Math.Min(2,current.Count); i++) {
            result.Add(current[i]);
            selected_ids.Add(current[i]["id"].Text);
        }
        if ((result.Count < 2)) {
            foreach (var item in near) {
                if ((result.Count >= 2)) { break; }
                if ((selected_ids.Contains(item["id"].Text))) { continue; }
                result.Add(item);
                selected_ids.Add(item["id"].Text);
            }
        }
        foreach (var item in near) {
            if ((result.Count >= 3)) { break; }
            if ((selected_ids.Contains(item["id"].Text))) { continue; }
            result.Add(item);
            selected_ids.Add(item["id"].Text);
        }
        if ((result.Count < 3)) {
            foreach (var item in current) {
                if ((result.Count >= 3)) { break; }
                if ((selected_ids.Contains(item["id"].Text))) { continue; }
                result.Add(item);
                selected_ids.Add(item["id"].Text);
            }
        }
        return result;
    }
    public static string accept(Game g, string commission_id) {
        ensure(g);
        if ((g.s.Get("phase","").Text != "play")) { return "当前不能接取委托。"; }
        V state = g.s["block"]["commissions"];
        if (!((state["active"].Count == 0))) { return "请先完成或放弃当前委托。"; }
        if ((state.Get("pending_reward","").Text != "")) { return "先领取上一个委托的奖励。"; }
        V offer = V.Map();
        foreach (var row in available(g)) {
            if ((row.Get("id","").Text == commission_id)) { offer = row; break; }
        }
        if ((offer.Count == 0)) { return "这份委托当前不可接取。"; }
        V trade_rows = g.s["block"].Get("trade_history",V.Arr());
        V active = V.Map("id", commission_id, "name", offer.Get("name",commission_id).Text, "domain", offer.Get("domain","").Text, "started_at", g.s.Get("local_hours",0).Double, "started_tick", (int)g.s.Get("simulation_tick",0).Long, "deadline_at", -(1.0), "goals", offer.Get("goals",V.Arr()).Duplicate(true), "goal_progress", V.Map(), "progress_now", V.Map(), "delivered_goods", 0.0, "trade_cursor", trade_rows.Count, "status", "active");
        double deadline = offer.Get("fail_conditions",V.Map()).Get("deadline_hours",0).Double;
        if ((deadline > 0)) { active["deadline_at"] = V.From((active["started_at"].Double + deadline)); }
        state["active"] = active;
        state["history"].Add(V.Map("id", commission_id, "name", active["name"], "domain", active["domain"], "status", "accepted", "started_at", active["started_at"], "tick", active["started_tick"]));
        g.s["block"]["commissions"] = state;
        return "";
    }
    public static string abandon(Game g) {
        ensure(g);
        V state = g.s["block"]["commissions"];
        if ((state["active"].Count == 0)) { return "当前没有进行中的委托。"; }
        V active = state["active"];
        state["history"].Add(V.Map("id", active.Get("id","").Text, "name", active.Get("name","").Text, "domain", active.Get("domain","").Text, "status", "abandoned", "started_at", active.Get("started_at",0).Double, "finished_at", g.s.Get("local_hours",0).Double, "progress_now", active.Get("progress_now",V.Map()).Duplicate(true)));
        state["active"] = V.Map();
        g.s["block"]["commissions"] = state;
        return "";
    }
    public static bool _goal_met(double value, V goal) {
        if ((goal.Has("min") && ((value + 1e-06) < goal["min"].Double))) { return false; }
        if ((goal.Has("max") && ((value - 1e-06) > goal["max"].Double))) { return false; }
        if ((goal.Has("equals") && (Math.Abs((value - goal["equals"].Double)) > 1e-06))) { return false; }
        return true;
    }
    public static string _fail_reason(Game g, V active, V definition) {
        V fail = definition.Get("fail_conditions",V.Map());
        if ((fail.Has("min_energy_ratio") && (g.s.Get("metrics",V.Map()).Get("energy_ratio",1).Double < fail["min_energy_ratio"].Double))) {
            return "供电低于委托底线。";
        }
        if (((active.Get("deadline_at",-(1)).Double >= 0) && (g.s.Get("local_hours",0).Double > (active["deadline_at"].Double + 1e-06)))) {
            return "委托时限已过。";
        }
        return "";
    }
    public static void _finish(Game g, V state, V active, V definition) {
        string commission_id = active.Get("id","").Text;
        V done = V.Map("id", commission_id, "name", definition.Get("name",commission_id).Text, "domain", definition.Get("domain","").Text, "status", "completed", "started_at", active.Get("started_at",0).Double, "completed_at", g.s.Get("local_hours",0).Double, "progress_now", active.Get("progress_now",V.Map()).Duplicate(true), "goal_progress", active.Get("goal_progress",V.Map()).Duplicate(true));
        state["history"].Add(done);
        if (!((state["completed"].Contains(commission_id)))) { state["completed"].Add(commission_id); }
        int ordinal = 0;
        foreach (var row in state["history"]) {
            if (((row.Get("id","").Text == commission_id) && (row.Get("status","") == "completed"))) { ordinal += 1; }
        }
        string source = ((("commission:" + commission_id) + ":") + V.From(ordinal).Text);
        V reward = Bonus.draw(g,source,definition.Get("reward_pool_tags",V.Arr()));
        state["pending_reward"] = V.From(reward.Get("reward_id","").Text);
        state["active"] = V.Map();
        g.log_event("commission_completed",V.Map("id", commission_id, "reward_id", state["pending_reward"]),(definition.Get("name",commission_id).Text + "已完成，请领取一份方案许可。"));
    }
    public static void tick(Game g, double h) {
        if ((g.preview_mode || (h <= 0))) { return; }
        ensure(g);
        V state = g.s["block"]["commissions"];
        if ((state["active"].Count == 0)) { return; }
        V active = state["active"];
        V definition = _definition(g,active.Get("id","").Text);
        if ((definition.Count == 0)) { return; }
        string failure = _fail_reason(g,active,definition);
        if ((failure != "")) {
            state["history"].Add(V.Map("id", active["id"].Text, "name", active.Get("name","").Text, "domain", active.Get("domain","").Text, "status", "failed", "started_at", active.Get("started_at",0).Double, "finished_at", g.s.Get("local_hours",0).Double, "reason", failure, "progress_now", active.Get("progress_now",V.Map()).Duplicate(true)));
            state["active"] = V.Map();
            g.s["block"]["commissions"] = state;
            return;
        }
        bool all_done = true;
        for (int i = 0; i < active.Get("goals",V.Arr()).Count; i++) {
            V goal = active["goals"][i];
            string key = V.From(i).Text;
            double value = _metric(g,goal.Get("metric","").Text,active);
            active["progress_now"][key] = V.From(value);
            bool met = _goal_met(value,goal);
            double duration = goal.Get("duration",0).Double;
            if ((duration > 0)) {
                active["goal_progress"][key] = V.From((met ? (active["goal_progress"].Get(key,0).Double + h) : 0.0));
                if (((active["goal_progress"][key].Double + 1e-06) < duration)) { all_done = false; }
            }
            else if (!(met)) {
                all_done = false;
            }
        }
        if (all_done) {
            _finish(g,state,active,definition);
        }
        else {
            state["active"] = active;
        }
        g.s["block"]["commissions"] = state;
    }
    public static string deliver(Game g) {
        ensure(g);
        V active = g.s["block"]["commissions"]["active"];
        if ((active.Count == 0)) { return "当前没有进行中的委托。"; }
        double target = 0.0;
        foreach (var goal in active.Get("goals",V.Arr())) {
            if ((goal.Get("metric","").Text == "delivery_goods")) { target = Math.Max(target,goal.Get("min",0).Double); }
        }
        if ((target <= 0)) { return "当前委托没有主动交付目标。"; }
        double remaining = Math.Max(0,(target - active.Get("delivered_goods",0).Double));
        if ((remaining <= 1e-06)) { return "委托所需制成品已全部交付。"; }
        double amount = Math.Min(remaining,g.inventory_total("goods"));
        if ((amount <= 1e-06)) { return "当前没有可交付的制成品。"; }
        if (!(g.withdraw_stock("goods",amount))) { return "制成品库存不足。"; }
        active["delivered_goods"] = V.From((active.Get("delivered_goods",0).Double + amount));
        V state = g.s["block"]["commissions"];
        state["active"] = active;
        g.s["block"]["commissions"] = state;
        if (!((g.s["block"].Get("commission_deliveries").IsArray))) { g.s["block"]["commission_deliveries"] = V.Arr(); }
        g.s["block"]["commission_deliveries"].Add(V.Map("commission_id", active["id"].Text, "resource", "goods", "amount", amount, "h", g.s.Get("local_hours",0).Double, "tick", (int)g.s.Get("simulation_tick",0).Long));
        V audit = g.s.Get("audit",V.Map());
        V receipt = audit.Get("resources",V.Map()).Get("goods",V.Map());
        if (!((receipt.Count == 0))) {
            receipt["commission_delivered"] = V.From((receipt.Get("commission_delivered",0).Double + amount));
            receipt["after"] = V.From(g.inventory_total("goods"));
            receipt["error"] = V.From((((((((receipt.Get("before",0).Double + receipt.Get("produced",0).Double) + receipt.Get("imported",0).Double) - receipt.Get("consumed",0).Double) - receipt.Get("lost",0).Double) - receipt.Get("exported",0).Double) - receipt["commission_delivered"].Double) - receipt["after"].Double));
            audit["resources"]["goods"] = receipt;
            g.s["audit"] = audit;
        }
        g.refresh_metrics();
        return "";
    }
}
}
