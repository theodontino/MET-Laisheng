using System;
using System.Linq;
namespace MET.Core {
public static class Bonus {
    public static void _ensure(Game g) {
        if (!((g.s.Get("block").IsMap))) {
            g.s["block"] = V.Map("id", "block:1");
        }
        V b = g.s["block"];
        if (!((b.Get("bonus_wallet").IsMap))) { b["bonus_wallet"] = V.Map(); }
        if (!((b.Get("deployed_bonuses").IsMap))) { b["deployed_bonuses"] = V.Map(); }
        if (!((b.Get("reward_choices").IsArray))) { b["reward_choices"] = V.Arr(); }
        if (!((b.Get("blueprint_levels").IsMap))) { b["blueprint_levels"] = V.Map(); }
        if (!((g.s.Get("rng").IsMap))) { g.s["rng"] = V.Map(); }
        if (!(g.s["rng"].Has("reward"))) {
            long seed_value = g.s.Get("seed",1).Long;
            g.s["rng"]["reward"] = V.From((((((seed_value * 48271) + 104729) % 2147483646 + 2147483646) % 2147483646) + 1));
        }
    }
    public static V _metadata(Game g, string bonus_id) {
        V all = g.c.v040.Get("bonus_economy",V.Map()).Get("metadata",V.Map());
        if ((all.Get(bonus_id).IsMap)) { return all[bonus_id]; }
        V definition = g.c.bonuses.Get(bonus_id,V.Map());
        if ((definition.Count == 0)) { return V.Map(); }
        string scope = "building";
        foreach (var host in definition.Get("hosts",V.Arr())) {
            if ((V.Arr("organization", "block").Contains(host))) { scope = host.Text; break; }
        }
        return V.Map("scope", scope, "persistence", "Local", "tags", V.Arr(), "max_deployments", 1);
    }
    public static bool _tags_match(V meta, V requested) {
        if ((requested.Count == 0)) { return true; }
        V tags = meta.Get("tags",V.Arr());
        foreach (var tag in requested) {
            if ((tags.Contains(tag.Text))) { return true; }
        }
        return false;
    }
    public static int _deployed_count(Game g, string bonus_id) {
        V entry = g.s["block"]["deployed_bonuses"].Get(bonus_id,V.Map());
        int count = (int)entry.Get("count",0).Long;
        count = Math.Max(count,((entry.Get("instances").IsArray) ? entry.Get("instances",V.Arr()).Count : 0));
        return count;
    }
    public static V _find_host(Game g, string bonus_id, V meta) {
        string scope = meta.Get("scope","building").Text;
        if (!(g.c.bonuses.Has(bonus_id))) { return V.Map(); }
        V definition = g.c.bonuses[bonus_id];
        if ((scope == "building")) {
            V projects = g.playable_ids();
            projects.Items.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Text,b.Text));
            foreach (var id in projects) {
                V p = g.s["projects"][id];
                V slots = V.From(p.Get("slots",V.Map()).Keys);
                slots.Items.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Text,b.Text));
                foreach (var slot in slots) {
                    if (_eligible(g,bonus_id,"building",id.Text,slot.Text)) {
                        return V.Map("type", "building", "id", id.Text, "slot", slot.Text);
                    }
                }
            }
        }
        else if ((scope == "organization")) {
            V organizations = V.From(g.s.Get("organizations",V.Map()).Keys);
            organizations.Items.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Text,b.Text));
            foreach (var id in organizations) {
                if (_eligible(g,bonus_id,"organization",id.Text,"")) {
                    return V.Map("type", "organization", "id", id.Text, "slot", "");
                }
            }
        }
        else if ((scope == "block")) {
            string block_id = g.s["block"].Get("id","").Text;
            if (((block_id != "") && _eligible(g,bonus_id,"block",block_id,""))) {
                return V.Map("type", "block", "id", block_id, "slot", "");
            }
        }
        return V.Map();
    }
    public static bool _eligible(Game g, string bonus_id, string host_type, string host_id, string slot) {
        string replacing = ((host_type == "building") ? g.s["projects"].Get(host_id,V.Map()).Get("slots",V.Map()).Get(slot,"").Text : "");
            return (g.bonus_eligible(bonus_id,host_type,host_id,slot,replacing) == "");
    }
    public static bool _future_host(Game g, string bonus_id, V meta) {
        if ((meta.Get("scope","building").Text != "building")) { return false; }
        if (!(g.c.bonuses.Has(bonus_id))) { return false; }
        V bonus = g.c.bonuses[bonus_id];
        V gate = bonus.Get("install_gate",bonus.Get("gate",V.Map()));
        V wanted = bonus.Get("traits",V.Arr());
        V allowed = bonus.Get("allowed_slots",V.Arr());
        foreach (var id in g.playable_ids()) {
            V p = g.s["projects"][id];
            if (((int)p.Get("tier",1).Long >= 3)) { continue; }
            V current = g.c.projects.Get(p.Get("node_id",p.Get("def","")).Text,V.Map());
            foreach (var next_id in current.Get("upgrades",V.Arr())) {
                V next = g.c.projects.Get(next_id.Text,V.Map());
                if (((next.Count == 0) || !(Rules.any_trait(next.Get("traits",V.Arr()),wanted)))) { continue; }
                int next_tier = (int)next.Get("tier",((int)p.Get("tier",1).Long + 1)).Long;
                V track = p.Get("slot_track",V.Arr());
                if (((next_tier < 1) || (next_tier > track.Count))) { continue; }
                string slot_type = track[(next_tier - 1)].Text;
                if ((slot_type == "Flex")) {
                    if (!((V.Arr("Production", "Logistics").Contains(bonus.Get("slot","").Text)))) { continue; }
                }
                else if (!((allowed.Contains(slot_type)))) { continue; }
                bool fits = true;
                V next_traits = next.Get("traits",V.Arr());
                foreach (var required_trait in gate.Get("traits",V.Arr())) {
                    if (!((next_traits.Contains(required_trait)))) { fits = false; }
                }
                if (!(Rules.any_trait(next_traits,gate.Get("any_traits",V.Arr())))) { fits = false; }
                V tile = g.s["cells"].Get(p.Get("tile","").Text,V.Map());
                foreach (var tile_trait in gate.Get("tile_traits",V.Arr())) {
                    if (!((tile.Get("traits",V.Arr()).Contains(tile_trait)))) { fits = false; }
                }
                if (gate.Get("has_inputs",false).Bool) {
                    bool has_input = false;
                    foreach (var stat in next.Get("stats",V.Map())) {
                        if ((stat.Text.StartsWith("input.") && (next["stats"][stat].Double > 0))) { has_input = true; }
                    }
                    if (!(has_input)) { fits = false; }
                }
                foreach (var field in gate.Get("field_min",V.Map())) {
                    double amount = Rules.aggregate(tile.Get("fields",V.Map()).Get(field,V.Arr()),"sum");
                    if ((amount < gate["field_min"][field].Double)) { fits = false; }
                }
                if (fits) { return true; }
            }
        }
        return false;
    }
    public static V _card(Game g, string bonus_id) {
        V meta = _metadata(g,bonus_id);
        V definition = g.c.bonuses.Get(bonus_id,V.Map());
        V host = (!((definition.Count == 0)) ? _find_host(g,bonus_id,meta) : V.Map());
        bool now = !((host.Count == 0));
        bool future = (now ? false : _future_host(g,bonus_id,meta));
        string reason = (now ? "现在可部署" : (future ? "升级后可能部署" : "当前城市尚不适用"));
        return V.Map("bonus_id", bonus_id, "name", definition.Get("name",bonus_id).Text, "scope", meta.Get("scope","building").Text, "persistence", "Local", "tags", meta.Get("tags",V.Arr()).Duplicate(true), "deployable_now", now, "near_future", future, "preview_reason", reason, "suggested_host", host, "copies", (int)g.s.Get("block",V.Map()).Get("bonus_wallet",V.Map()).Get(bonus_id,V.Map()).Get("charges",0).Long, "blueprint_level", blueprint_level(g,bonus_id), "grade", Rules.bonus_content(g)["grades"][blueprint_level(g,bonus_id)].Text, "effects", effect_rows(g,bonus_id,0,1), "stacking_note", "首份100%，以后每份33⅓%；数值代价同时缩放；乘数不低于0、转换比例不高于1。结构效果不重复，无新增收益不消耗副本。");
    }
    public static V _pick(Game g, V rows) {
        if ((rows.Count == 0)) { return V.Map(); }
        V ids = V.Arr();
        V weights = V.Arr();
        foreach (var row in rows) {
            ids.Add(row["bonus_id"].Text);
            weights.Add(Math.Max(0.01,g.c.bonuses.Get(row["bonus_id"],V.Map()).Get("weight",1).Double));
        }
        V selected = Rng.pick_weighted(g.s,"reward",ids,weights);
        foreach (var row in rows) {
            if ((row["bonus_id"].Text == selected.Text)) { return row; }
        }
        return rows[0];
    }
    public static V card(Game g, string bonus_id) {
        return _card(g,bonus_id);
    }
    public static int blueprint_level(Game g, string bonus_id) {
        return Math.Min(2, Math.Max(0,(int)g.s.Get("block",V.Map()).Get("blueprint_levels",V.Map()).Get(bonus_id,0).Long));
    }
    public static int active_count(Game g, string bonus_id, string host_type = "", string host_id = "", string excluding = "") {
        int count = 0;
        foreach (var instance_id in g.s.Get("bonus_instances",V.Map())) {
            V instance = g.s["bonus_instances"][instance_id];
            if (((instance_id.Text == excluding) || !(instance.Get("active",false).Bool))) { continue; }
            if ((instance.Get("bonus_id","").Text != bonus_id)) { continue; }
            if (((host_type != "") && (instance.Get("host_type","").Text != host_type))) { continue; }
            if (((host_id != "") && (instance.Get("host_id","").Text != host_id))) { continue; }
            count += 1;
        }
        return count;
    }
    public static string duplicate_error(Game g, string bonus_id, string host_type, string host_id, string replacing = "") {
        if (((replacing != "") && (g.s.Get("bonus_instances",V.Map()).Get(replacing,V.Map()).Get("bonus_id","").Text == bonus_id))) {
            return "这个槽已有同名方案；原位替换没有新增效果，不消耗副本。";
        }
        int count = active_count(g,bonus_id,host_type,host_id,replacing);
        if (((count > 0) && !(Rules.duplicate_has_benefit(g,bonus_id,count)))) {
            return "同名结构效果不叠加，或数值已达上限；没有新增收益，不消耗副本。";
        }
        return "";
    }
    public static V duplicate_preview(Game g, string bonus_id, string host_type, string host_id) {
        int count = active_count(g,bonus_id,host_type,host_id);
        bool can_add = Rules.duplicate_has_benefit(g,bonus_id,count);
        return V.Map("count", count, "strength", Rules.copy_strength(g,count,bonus_id), "next_strength", Rules.copy_strength(g,(count + 1),bonus_id), "can_add", can_add, "effects", effect_rows(g,bonus_id,count,(count + 1)), "note", (can_add ? "追加数值为首份的33⅓%；副作用同时增加。" : "同名结构幂等或数值已达上限；无新增收益，不消耗副本。"));
    }
    public static V effect_rows(Game g, string bonus_id, int before_count, int after_count, int before_level = -(1), int after_level = -(1)) {
        V before = Rules.bonus_rules(g,bonus_id,Math.Max(1,before_count),before_level);
        V after = Rules.bonus_rules(g,bonus_id,Math.Max(1,after_count),after_level);
        V result = V.Arr();
        V labels = V.Map("input.*", "全部原料投入", "input.material", "材料投入", "energy_demand", "能源需求", "pollution", "污染", "output.goods", "制成品产出", "output.food", "食物产出", "efficiency", "建筑效率", "personnel_mult", "人员效率", "maintenance", "维护", "housing", "住房", "service_decay", "服务衰减", "service.medical", "医疗容量", "service.commercial", "商业容量", "service.entertainment", "娱乐容量", "storage.*", "全部库容", "jobs", "岗位", "exposure_multiplier", "污染暴露", "shortage_multiplier", "短缺敏感度", "population_maintenance", "人口维护", "allocation_priority", "分配优先级", "synergy.same_industrial", "同组织工业邻接", "heat_service", "余热服务", "heat_discount", "余热节能");
        for (int index = 0; index < after.Count; index++) {
            V a = after[index];
            V b = before[index];
            if (((a.Get("op","").Text == "modify") && (V.Arr("add", "multiply").Contains(a.Get("mode","").Text)))) {
                string mode = a["mode"].Text;
                double start = ((before_count > 0) ? b["value"].Double : ((mode == "multiply") ? 1.0 : 0.0));
                result.Add(V.Map("label", labels.Get(a["field"].Text,a["field"].Text).Text, "before", start, "after", a["value"].Double, "unit", ((mode == "multiply") ? "倍" : ""), "mode", mode, "rule_id", a["id"].Text));
            }
            else if (a.Has("fraction")) {
                result.Add(V.Map("label", ((a["op"] == "capacity_convert") ? "容量转换比例" : "容量复制比例"), "before", ((before_count > 0) ? b["fraction"].Double : 0.0), "after", a["fraction"].Double, "unit", "比例", "mode", "fraction", "rule_id", a["id"].Text));
            }
            else if (a.Has("rate")) {
                result.Add(V.Map("label", "配方吞吐强度", "before", ((before_count > 0) ? b["rate"].Double : 0.0), "after", a["rate"].Double, "unit", "倍", "mode", "rate", "rule_id", a["id"].Text));
                if ((a.Get("output").IsMap)) {
                    foreach (var resource in a["output"]) {
                        result.Add(V.Map("label", ("每批产出" + Catalog.RESOURCE_NAMES.Get(resource,resource).Text), "before", b["output"][resource].Double, "after", a["output"][resource].Double, "unit", "", "mode", "recipe_output", "rule_id", a["id"].Text));
                    }
                }
            }
            else if (((a.Get("value").IsNumber) || (a.Get("value").IsInteger))) {
                V policy = Rules.bonus_content(g).Get("rule_policies",V.Map()).Get(a.Get("id","").Text,V.Map());
                if (policy.Has("value_mode")) {
                    string mode = policy["value_mode"].Text;
                    result.Add(V.Map("label", a.Get("key",a.Get("field","数值规则")).Text, "before", ((before_count > 0) ? b["value"].Double : ((mode == "multiply") ? 1.0 : 0.0)), "after", a["value"].Double, "unit", ((mode == "multiply") ? "倍" : ""), "mode", mode, "rule_id", a["id"].Text));
                }
            }
        }
        return result;
    }
    public static bool _owned(Game g, string bonus_id) {
        return (((int)g.s.Get("block",V.Map()).Get("bonus_wallet",V.Map()).Get(bonus_id,V.Map()).Get("charges",0).Long > 0) || (active_count(g,bonus_id) > 0));
    }
    public static V research_candidates(Game g, V bonus_ids = null, V tags = null) {
        bonus_ids ??= V.Arr();
        tags ??= V.Arr();
        V result = V.Arr();
        V ids = (!((bonus_ids.Count == 0)) ? bonus_ids.Duplicate() : V.From(Rules.bonus_content(g).Get("upgrades",V.Map()).Keys));
        ids.Items.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Text,b.Text));
        V seen = V.Map();
        foreach (var raw_id in ids) {
            string bid = raw_id.Text;
            if ((seen.Has(bid) || !(g.c.bonuses.Has(bid)))) { continue; }
            seen[bid] = V.From(true);
            if ((!(Rules.bonus_content(g).Get("upgrades",V.Map()).Has(bid)) || (blueprint_level(g,bid) >= 2) || !(_owned(g,bid)))) { continue; }
            if (!(_tags_match(_metadata(g,bid),tags))) { continue; }
            V choice = _card(g,bid);
            choice["kind"] = V.From("research");
            choice["target_level"] = V.From((blueprint_level(g,bid) + 1));
            choice["target_grade"] = V.From(Rules.bonus_content(g)["grades"][choice["target_level"]].Text);
            choice["effects"] = effect_rows(g,bid,1,1,blueprint_level(g,bid),(int)choice["target_level"].Long);
            choice["preview_reason"] = V.From((("本Block全部同名副本升级为" + choice["target_grade"].Text) + "；不消耗库存副本，不改变部署尺度。"));
            result.Add(choice);
        }
        return result;
    }
    public static V _reward_pool(Game g, V tags, V options) {
        V ids = options.Get("bonus_ids",V.From(g.c.bonuses.Keys)).Duplicate();
        ids.Items.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Text,b.Text));
        V all = V.Arr();
        V matching = V.Arr();
        V seen = V.Map();
        foreach (var raw_id in ids) {
            string bid = raw_id.Text;
            if ((seen.Has(bid) || !(g.c.bonuses.Has(bid)) || (_metadata(g,bid).Count == 0))) { continue; }
            seen[bid] = V.From(true);
            V choice = _card(g,bid);
            choice["kind"] = V.From("copy");
            all.Add(choice);
            if (_tags_match(_metadata(g,bid),tags)) { matching.Add(choice); }
        }
        if (options.Has("bonus_ids")) { return all; }
        return (!((matching.Count == 0)) ? matching : all);
    }
    public static V _choose_cards(Game g, V pool) {
        V immediate = V.Arr();
        V future = V.Arr();
        foreach (var choice in pool) {
            if (choice.Get("deployable_now",false).Bool) { immediate.Add(choice); }
            else if (choice.Get("near_future",false).Bool) { future.Add(choice); }
        }
        V result = V.Arr();
        if (((immediate.Count >= 2) && !((future.Count == 0)))) {
            for (int i = 0; i < 2; i++) {
                V picked = _pick(g,immediate);
                result.Add(picked);
                immediate.Erase(picked);
            }
            result.Add(_pick(g,future));
        }
        else {
            V remaining = pool.Duplicate(true);
            while (((result.Count < 3) && !((remaining.Count == 0)))) {
                V picked = _pick(g,remaining);
                result.Add(picked);
                remaining.Erase(picked);
            }
        }
        return result;
    }
    public static void _set_candidates(Game g, V record) {
        V options = record.Get("options",V.Map());
        string kind = options.Get("kind","copy").Text;
        V tags = record.Get("tags",V.Arr());
        V pool = V.Arr();
        if ((kind == "research")) {
            if ((!(options.Has("bonus_ids")) || !((options["bonus_ids"].Count == 0)))) {
                pool = research_candidates(g,options.Get("bonus_ids",V.Arr()),(options.Has("bonus_ids") ? V.Arr() : tags));
            }
            if ((pool.Count == 0)) { kind = "copy"; }
        }
        if ((kind == "copy")) { pool = _reward_pool(g,tags,options); }
        record["kind"] = V.From(kind);
        record["choices"] = _choose_cards(g,pool);
        if ((record["choices"].Count == 0)) {
            record["kind"] = V.From("currency");
            record["compensation"] = V.From(Rules.bonus_content(g).Get("fallback_currency",30).Double);
            record["choices"] = V.Arr(V.Map("bonus_id", "__currency__", "name", "城市发展补助", "kind", "currency", "compensation", record["compensation"], "preview_reason", string.Format(System.Globalization.CultureInfo.InvariantCulture,"没有候选方案，领取{0:F0}通货；不恢复DT。",record["compensation"])));
        }
    }
    public static V draw(Game g, string source, V tags = null, V options = null) {
        tags ??= V.Arr();
        options ??= V.Map();
        _ensure(g);
        string source_id = source.Trim();
        if ((source_id == "")) { return V.Map(); }
        foreach (var existing_record in g.s["block"]["reward_choices"]) {
            if ((existing_record.Get("source","").Text == source_id)) { return existing_record.Duplicate(true); }
        }
        V record = V.Map("reward_id", g.uid("R"), "source", source_id, "tags", tags.Duplicate(true), "options", options.Duplicate(true), "requested_kind", options.Get("kind","copy").Text, "choices", V.Arr(), "created_tick", (int)g.s.Get("simulation_tick",0).Long, "created_at", g.s.Get("local_hours",0).Double, "claimed", false, "chosen_bonus", "");
        _set_candidates(g,record);
        g.s["block"]["reward_choices"].Add(record);
        return record.Duplicate(true);
    }
    public static V pending(Game g) {
        V result = V.Arr();
        foreach (var record in g.s.Get("block",V.Map()).Get("reward_choices",V.Arr())) {
            if (!(record.Get("claimed",false).Bool)) { result.Add(record.Duplicate(true)); }
        }
        return result;
    }
    public static V _all_reward_cards(Game g) {
        return _reward_pool(g,V.Arr(),V.Map());
    }
    public static string grant(Game g, string bonus_id, string source) {
        _ensure(g);
        string bid = bonus_id;
        if (!(g.c.bonuses.Has(bid))) { return "未知方案。"; }
        string source_id = source.Trim();
        if ((source_id == "")) { return "许可来源缺失。"; }
        V wallet = g.s["block"]["bonus_wallet"];
        V entry = wallet.Get(bid,V.Map());
        if ((!((entry.Count == 0)) && (entry.Get("grant_sources",V.Arr()).Contains(source_id)))) { return "这份许可已经领取。"; }
        V meta = _metadata(g,bid);
        if ((meta.Count == 0)) { return "方案许可信息缺失。"; }
        if ((entry.Count == 0)) {
            entry = V.Map("charges", 0, "acquired_from", source_id, "grant_sources", V.Arr(), "scope", meta.Get("scope","building").Text, "persistence", "Local");
        }
        V sources = entry.Get("grant_sources",V.Arr()).Duplicate(true);
        if ((sources.Contains(source_id))) { return "这份许可已经领取。"; }
        sources.Add(source_id);
        entry["grant_sources"] = sources;
        entry["charges"] = V.From(((int)entry.Get("charges",0).Long + 1));
        entry["acquired_from"] = ((sources.Count == 1) ? V.From(source_id) : entry.Get("acquired_from",source_id));
        entry["scope"] = V.From(meta.Get("scope","building").Text);
        entry["persistence"] = V.From("Local");
        wallet[bid] = entry;
        return "";
    }
    public static string choose(Game g, string reward_id, string bonus_id) {
        _ensure(g);
        int record_index = -(1);
        for (int i = 0; i < g.s["block"]["reward_choices"].Count; i++) {
            if ((g.s["block"]["reward_choices"][i].Get("reward_id","").Text == reward_id)) {
                record_index = i;
                break;
            }
        }
        if ((record_index < 0)) { return "找不到这份奖励。"; }
        V record = g.s["block"]["reward_choices"][record_index];
        if (record.Get("claimed",false).Bool) { return "这份奖励已经领取。"; }
        for (int i = 0; i < record_index; i++) {
            if (!(g.s["block"]["reward_choices"][i].Get("claimed",false).Bool)) { return "请先领取队列前面的奖励。"; }
        }
        bool allowed = false;
        foreach (var choice in record.Get("choices",V.Arr())) {
            if ((choice.Get("bonus_id","").Text == bonus_id)) { allowed = true; }
        }
        if (!(allowed)) { return "请选择这份奖励中的方案。"; }
        string kind = record.Get("kind","copy").Text;
        if ((kind == "currency")) {
            g.s["currency"] = V.From((g.s.Get("currency",0).Double + record.Get("compensation",30).Double));
        }
        else if ((kind == "research")) {
            if (((blueprint_level(g,bonus_id) >= 2) || !(_owned(g,bonus_id)))) {
                _set_candidates(g,record);
                g.s["block"]["reward_choices"][record_index] = record;
                return "蓝图已满级或不再持有；奖励已保存为当前合法研究或主题副本，请重新选择。";
            }
            g.s["block"]["blueprint_levels"][bonus_id] = V.From((blueprint_level(g,bonus_id) + 1));
            record["applied_level"] = g.s["block"]["blueprint_levels"][bonus_id];
        }
        else {
            string error = grant(g,bonus_id,reward_id);
            if ((error != "")) { return error; }
        }
        record["claimed"] = V.From(true);
        record["chosen_bonus"] = V.From(bonus_id);
        g.s["block"]["reward_choices"][record_index] = record;
        V commissions = g.s["block"].Get("commissions",V.Map());
        if ((commissions.Get("pending_reward","").Text == reward_id)) { commissions["pending_reward"] = V.From(""); }
        if (!((commissions.Count == 0))) { g.s["block"]["commissions"] = commissions; }
        if (kind == "research") { g.refresh_metrics(); }
        return "";
    }
    public static string install(Game g, string bonus_id, string host_type, string host, string slot = "") {
        _ensure(g);
        if ((g.s.Get("phase","").Text != "play")) { return "当前不能部署方案。"; }
        string bid = bonus_id;
        if (!(g.c.bonuses.Has(bid))) { return "未知方案。"; }
        V meta = _metadata(g,bid);
        if ((meta.Count == 0)) { return "方案许可信息缺失。"; }
        if ((meta.Get("scope","building").Text != host_type)) { return "方案部署尺度不匹配。"; }
        V wallet = g.s["block"]["bonus_wallet"];
        V license = wallet.Get(bid,V.Map());
        if (((license.Count == 0) || ((int)license.Get("charges",0).Long < 1))) { return "没有可用的部署许可。"; }
        string replacing = ((host_type == "building") ? g.s["projects"].Get(host,V.Map()).Get("slots",V.Map()).Get(slot,"").Text : "");
        string repeat_error = duplicate_error(g,bid,host_type,host,replacing);
        if ((repeat_error != "")) { return repeat_error; }

        V before_ids = V.From(g.s.Get("bonus_instances",V.Map()).Keys);
        string provider = "civic";
        if (((host_type == "building") && g.s["projects"].Has(host))) { provider = g.s["projects"][host].Get("operator_org",provider).Text; }
        else if ((host_type == "organization")) { provider = host; }
        string error = g._install_bonus(bid,host_type,host,provider,slot);
        if ((error != "")) { return error; }
        license["charges"] = V.From(((int)license.Get("charges",0).Long - 1));
        wallet[bid] = license;
        V entry = g.s["block"]["deployed_bonuses"].Get(bid,V.Map());
        if (!((entry.Get("instances").IsArray))) { entry["instances"] = V.Arr(); }
        if (!((entry.Get("history").IsArray))) { entry["history"] = V.Arr(); }
        string instance_id = "";
        V keys = V.From(g.s["bonus_instances"].Keys);
        keys.Items.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Text,b.Text));
        foreach (var iid in keys) {
            if ((before_ids.Contains(iid))) { continue; }
            V instance = g.s["bonus_instances"][iid];
            if (((instance.Get("bonus_id","") == bid) && (instance.Get("host_type","") == host_type) && (instance.Get("host_id","") == host))) {
                instance_id = iid.Text;
                break;
            }
        }
        V stamp = V.Map("instance_id", instance_id, "host_type", host_type, "host", host, "slot", slot, "installed_at", g.s.Get("local_hours",0).Double, "installed_tick", (int)g.s.Get("simulation_tick",0).Long);
        entry["count"] = V.From(((int)entry.Get("count",0).Long + 1));
        entry["instances"].Add(instance_id);
        entry["history"].Add(stamp);
        g.s["block"]["deployed_bonuses"][bid] = entry;
        return "";
    }
}
}
