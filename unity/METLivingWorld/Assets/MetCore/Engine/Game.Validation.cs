using System;
namespace MET.Core {
public sealed partial class Game {
    // Same-version state checks retained from game.gd; these also guard transaction commits.
    public string validate_state(V value) {
        string journal=Civic.validate(value);if(journal!="")return journal;
        if(value.Get("schema",0).Long!=6)return "这是其他 pre-alpha 版本的存档，请开始新游戏。";
        foreach(string k in new[]{"projects","groups","fields","authored_fields","bonus_instances","organizations","block","cells","rng","metrics","totals","draft","settings"})if(!value.Get(k).IsMap)return "存档缺少对象："+k;
        foreach(string k in new[]{"currency","dt","local_hours","next_id","wall_mark","simulation_tick","remainder","global_now","event_cursor","block_opportunities","support","mandate"})if(!is_num(value.Get(k)))return "存档数值损坏："+k;
        if(Array.IndexOf(new[]{"select_block","origin","play","failed"},value.Get("phase","").Text)<0)return "未知游戏阶段。";
        if(value["dt"].Double<0||value["local_hours"].Double<0)return "时间预算不能为负。";
        if(!value.Get("beta_time").IsMap)return "缺少Beta时间状态。";
        foreach(string k in new[]{"local_elapsed_seconds","active_real_seconds","offline_recorded_seconds","last_seen_utc","segments_committed"})if(!is_num(value["beta_time"].Get(k))||value["beta_time"][k].Double<0)return "时间记录损坏："+k;
        if(Array.IndexOf(new[]{"normal","observe","advance","burn"},value["beta_time"].Get("mode","").Text)<0)return "时间档位损坏。";
        if(!value["block"].Get("cycles").IsMap)return "年度记录损坏。";var cy=value["block"]["cycles"];
        foreach(string k in new[]{"year","major_cycle"})if(!is_num(cy.Get(k))||cy[k].Double<0)return "周期计数损坏。";
        foreach(string k in new[]{"history","population_awards","pending_summaries"})if(!cy.Get(k).IsArray)return "周期记录损坏："+k;
        if(!cy.Get("population_stability").IsMap)return "人口里程碑记录损坏。";
        foreach(var elapsed in cy["population_stability"].Values)if(!is_num(elapsed)||elapsed.Double<0)return "人口持续时间损坏。";
        foreach(var grade in value["block"].Get("blueprint_levels",V.Map()).Values)if(!is_num(grade)||grade.Double<0||grade.Double>2||grade.Double!=Math.Floor(grade.Double))return "蓝图等级损坏。";
        if(value["block"].Has("city_trade_totals")) {
            if(!value["block"]["city_trade_totals"].IsMap)return "城市贸易累计记录损坏。";
            foreach(var row in value["block"]["city_trade_totals"].Values) {
                foreach(var key in new[]{"buy","sell","adverse_exports"})if(!is_num(row.Get(key))||row[key].Double<0)return "城市贸易累计数量损坏。";
                if(!is_num(row.Get("currency_delta")))return "城市贸易累计资金损坏。";
            }
        }
        var ids=V.Map();var sources=V.Map();
        foreach(var reward in value["block"].Get("reward_choices",V.Arr())) {
            if(!reward.IsMap||!reward.Get("choices").IsArray)return "奖励记录损坏。";
            string rid=reward.Get("reward_id","").Text,source=reward.Get("source","").Text;
            if(rid==""||source==""||ids.Has(rid)||sources.Has(source))return "奖励来源重复或损坏。";ids[rid]=true;sources[source]=true;
        }
        if(value["block"].Has("ending")){var ending=value["block"]["ending"];if(!ending.IsMap)return "尾声记录损坏。";if(ending.Get("triggered",false).Bool&&!is_num(ending.Get("local_elapsed_seconds")))return "尾声时间损坏。";}
        if(value["block"].Has("capstone")){var landmark=value["block"]["capstone"];if(!landmark.IsMap||!Block.capstone_definitions(this).Has(landmark.Get("theme","").Text))return "定型建筑记录损坏。";if(!value["block"].Get("ending",V.Map()).Get("capstone_claimed",false).Bool)return "定型建筑缺少领取记录。";}
        if(!value["block"].Get("policies").IsMap)return "政策记录损坏。";
        foreach(string k in new[]{"quote_bank","city_goal"})if(value.Has(k)&&!value[k].IsMap)return "新版本字段损坏："+k;
        if(value.Has("city_goal")&&value["city_goal"].Count>0) {
            var goal=value["city_goal"];if(!c.data["city_goals"].Has(goal.Get("route","").Text))return "发展方向损坏。";
            foreach(string k in new[]{"stable","community","renewal"})if(!is_num(goal.Get(k))||goal[k].Double<0)return "发展进度损坏。";
            if(!goal.Get("anchors").IsArray||!goal.Get("awards").IsArray||!goal.Get("baseline").IsMap)return "城市成果记录损坏。";
            foreach(var key in goal["anchors"]){if(!goal["baseline"].Get(key).IsMap)return "原住民基线损坏。";foreach(string stat in new[]{"health","pollution","service"})if(!is_num(goal["baseline"][key].Get(stat)))return "原住民基线数值损坏。";}
        }
        foreach(string k in new[]{"bonus_wallet","deployed_bonuses","commissions"})if(!value["block"].Get(k).IsMap)return "地区成长数据损坏："+k;
        foreach(string k in new[]{"reward_choices","trade_orders","trade_history"})if(!value["block"].Get(k).IsArray)return "地区记录损坏："+k;
        foreach(string bid in value["block"]["bonus_wallet"].Keys){var entry=value["block"]["bonus_wallet"][bid];if(!c.bonuses.Has(bid)||!entry.IsMap||!is_num(entry.Get("charges"))||entry["charges"].Double<0)return "方案许可记录损坏。";}
        foreach(var p in value["projects"].Values) {
            if(p.Get("special",false).Bool)continue;if(!p.Get("upgrade_job").IsMap||!p.Get("slot_track").IsArray)return "建筑成长记录损坏。";
            if(p["upgrade_job"].Count>0){if(!c.projects.Get(p.Get("def",""),V.Map()).Get("upgrades",V.Arr()).Contains(p.Get("upgrade_choice","")))return "升级方向损坏。";foreach(string k in new[]{"work","total","material"})if(!is_num(p["upgrade_job"].Get(k))||p["upgrade_job"][k].Double<0)return "升级施工记录损坏。";}
        }
        foreach(string oid in value["organizations"].Keys)if(!c.operators.Has(oid))return "未知运营组织。";
        if(value["phase"].Text=="play"||value["phase"].Text=="failed")foreach(string id in new[]{"homeless","municipal"})if(!value["projects"].Has(id))return "缺少市政/待安置容器。";
        foreach(var tile in value["cells"].Values)if(!tile.IsMap||!tile.Get("traits").IsArray||!tile.Get("fields").IsMap)return "地块记录损坏。";
        foreach(string id in value["projects"].Keys) {
            var p=value["projects"][id];if(!p.IsMap)return "建筑记录不是对象。";
            foreach(string k in new[]{"inventory","slots","job"})if(!p.Get(k).IsMap)return "建筑字段损坏："+k;
            if(p.Get("id","").Text!=id||!p.Get("traits").IsArray||!p.Get("labels").IsArray)return "建筑身份损坏。";
            if(!value["organizations"].Has(p.Get("operator_org","").Text))return "未知运营者。";
            if(p.Get("status","").Text!="construction"&&p.Get("status","").Text!="running")return "建筑状态损坏。";
            if(!p.Get("special",false).Bool){if(!c.projects.Has(p.Get("def","").Text)||!value["cells"].Has(p.Get("tile","").Text))return "建筑类型或位置损坏。";if(value["cells"][p["tile"]].Get("building","").Text!=id)return "地块与建筑引用不一致。";}
            if(p.Has("slot_types")){if(!p["slot_types"].IsMap)return "槽位类型损坏。";foreach(string slot in p["slots"].Keys)if(Array.IndexOf(new[]{"Production","Logistics","Operations","Flex"},p["slot_types"].Get(slot,slot.Split(':')[0]).Text)<0)return "未知方案槽类型。";}
            if(p.Has("work_priority")&&(!is_num(p["work_priority"])||p["work_priority"].Double< -1||p["work_priority"].Double>1))return "用工优先级损坏。";
            foreach(var amount in p["inventory"].Values)if(!is_num(amount)||amount.Double< -0.000001)return "库存不能为负。";
            if(p["status"].Text=="construction")foreach(string k in new[]{"work","total","material"})if(!is_num(p["job"].Get(k))||p["job"][k].Double<0)return "施工数据损坏。";
            foreach(string slot in p["slots"].Keys){string iid=p["slots"][slot].Text;if(iid=="")continue;var ins=value["bonus_instances"].Get(iid,V.Map());if(!ins.IsMap||!ins.Get("active",false).Bool||ins.Get("host_id","").Text!=id||ins.Get("slot","").Text!=slot)return "方案槽引用无效。";}
        }
        foreach(var group in value["groups"].Values) {
            if(!group.IsMap||!group.Get("employment").IsMap||!group.Get("services").IsMap)return "人群记录损坏。";if(!value["projects"].Has(group.Get("host","").Text))return "人群宿主不存在。";
            foreach(string k in new[]{"count","health","education","happiness","efficiency","demand"})if(!is_num(group.Get(k))||group[k].Double<0)return "人群数据损坏。";
            foreach(string k in new[]{"health","education","happiness"})if(group[k].Double>100)return "人群状态超出范围。";
            double employed=0;foreach(string id in group["employment"].Keys){if(!value["projects"].Has(id)||!is_num(group["employment"][id])||group["employment"][id].Double<0)return "就业引用无效。";employed+=group["employment"][id].Double;}if(employed>group["count"].Double+0.00001)return "同一Pop被重复分配。";
        }
        foreach(var i in value["bonus_instances"].Values) {
            if(!i.IsMap||!c.bonuses.Has(i.Get("bonus_id","").Text)||!value["organizations"].Has(i.Get("provider_org","").Text))return "未知Bonus或提供方。";
            if(i.Has("contract_org")&&!value["organizations"].Has(i["contract_org"].Text))return "方案签约组织不存在。";if(!c.bonuses[i["bonus_id"]]["hosts"].Contains(i.Get("host_type","")))return "方案宿主类型不兼容。";if(!i.Get("active",false).Bool)continue;
            if(i["host_type"].Text=="building"){if(!value["projects"].Has(i["host_id"])||value["projects"][i["host_id"]]["slots"].Get(i["slot"],"")!=i["id"])return "活跃方案失去建筑槽。";}
            else if(i["host_type"].Text=="organization"){if(!value["organizations"].Has(i["host_id"]))return "方案运营组织不存在。";}
            else if(i["host_id"]!=value["block"]["id"])return "方案Block不存在。";
        }
        foreach(string slot in value["block"]["policies"].Keys){var pid=value["block"]["policies"][slot];if(!c.policies.Has(pid)||c.policies[pid]["slot"].Text!=slot)return "政策槽无效。";}
        foreach(var field in value["authored_fields"].Values){if(!field.IsMap||!field.Get("propagation").IsMap)return "场定义损坏。";if(!is_num(field.Get("strength")))return "场强度损坏。";foreach(string k in new[]{"radius","decay"})if(!is_num(field["propagation"].Get(k)))return "场传播参数损坏。";}
        return "";
    }
}
}
