using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace MET.Core {
/// <summary>The active Block, immutable ending eligibility, and completed city records.</summary>
public static class Block {
    public static void ensure(Game g) {
        if (!g.s.Get("block").IsMap) g.s["block"] = V.Map("id","block:1");
        var block=g.s["block"];
        if (!block.Has("id")) block["id"]="block:1";
        if (!block.Has("stage")) block["stage"]=0;
        if (!block.Get("bonus_wallet").IsMap) block["bonus_wallet"]=V.Map();
        if (!block.Get("deployed_bonuses").IsMap) block["deployed_bonuses"]=V.Map();
        if (!block.Get("reward_choices").IsArray) block["reward_choices"]=V.Arr();
        if (!block.Get("commissions").IsMap) block["commissions"]=V.Map("active",V.Map(),"history",V.Arr(),"completed",V.Arr(),"pending_reward","");
        if (!block.Get("trade_orders").IsArray) block["trade_orders"]=V.Arr();
        if (!block.Get("trade_history").IsArray) block["trade_history"]=V.Arr();
        if (!block.Get("trade_ledger").IsArray) block["trade_ledger"]=V.Arr();
        if (!block.Get("market_state").IsMap) block["market_state"]=V.Map("resources",V.Map());
        if (!block.Has("construction_capacity")) block["construction_capacity"]=g.c.v040.Get("progression",V.Map()).Get("construction_capacity",4).Double;
        if (!block.Has("display_name")) block["display_name"]=g.s.Get("block_index",-1).Long<0?"河谷登陆点":"街区 "+(g.s["block_index"].Long+1);
        if (!block.Has("naming_rights")) block["naming_rights"]=false;
        if (!block.Has("naming_reward_granted")) block["naming_reward_granted"]=false;
        g.s["block"]=block;
        Market.ensure(g);
    }
    public static void grant_naming(Game g) { on_dt_zero(g); }
    public static void on_dt_zero(Game g) {
        ensure(g);
        if (g.s["dt"].Double!=0.0 || g.s["phase"]!="play") return;
        if (g.s["block"].Get("ending",V.Map()).Get("triggered",false).Bool) return;
        var theme=g.s["block"].Get("theme",V.Map());
        var eligible=theme.Get("completed",false).Bool?theme.Get("id","").Text:"";
        g.s["block"]["ending"]=V.Map("triggered",true,"eligible_theme",eligible,"capstone_claimed",false,"archive_id","","local_elapsed_seconds",g.s.Get("beta_time",V.Map()).Get("local_elapsed_seconds",0).Double,"theme_at_zero",theme.Get("id","").Text);
        g.s["block"]["naming_rights"]=true;
        g.s["block"]["naming_reward_granted"]=true;
        g.s["settings"]["user_paused"]=true;
        if (g.s.Get("beta_time").IsMap) {g.s["beta_time"]["mode"]="normal";g.s["beta_time"]["pause_reason"]="dt_zero";}
        g.log_event("dt_zero",V.Map("eligible_theme",eligible),"高速发展预算已耗尽。可以为这座城市命名，继续常速经营，或留存记录后独立重开。");
    }
    public static V capstone_definitions(Game g) { return g.c.capstones.IsMap?g.c.capstones.Duplicate(true):V.Map(); }
    public static string claim_capstone(Game g) {
        ensure(g);
        var ending=g.s["block"].Get("ending",V.Map());
        if (!ending.Get("triggered",false).Bool) return "高速发展预算实际耗尽后才能正式挂牌。";
        if (ending.Get("capstone_claimed",false).Bool) return "本区定型建筑已经落成。";
        var theme=ending.Get("eligible_theme","").Text;
        var defs=capstone_definitions(g);
        if (!defs.Has(theme)) return "本次预算耗尽前未完成主题，未获得定型建筑资格。";
        var item=defs[theme].Duplicate(true);
        item["source_id"]=item["id"];
        item["tile_anchor"]="0,0";
        var cells=g.s["cells"].Keys.OrderBy(x=>x,StringComparer.Ordinal);
        if (!g.s["cells"].Has(item["tile_anchor"]) || g.s["cells"][item["tile_anchor"]]["traits"].Contains("tile.water")) {
            foreach (var tile in cells) if (!g.s["cells"][tile]["traits"].Contains("tile.water")) {item["tile_anchor"]=tile;break;}
        }
        item["created_local_seconds"]=g.s["beta_time"]["local_elapsed_seconds"].Double;
        g.s["block"]["capstone"]=item;g.s["block"]["function_role"]=theme;
        ending["capstone_claimed"]=true;
        g.refresh_metrics();
        g.log_event("capstone",V.Map("id",item["id"],"theme",theme,"name",item["name"]),g.s["block"]["display_name"].Text+"的"+item["name"].Text+"正式启用。");
        return "";
    }
    public static V view(Game g) {
        ensure(g);
        var ending=g.s["block"].Get("ending",V.Map()).Duplicate(true);
        var item=capstone_definitions(g).Get(ending.Get("eligible_theme","").Text,V.Map());
        return V.Map("name",g.s["block"]["display_name"].Text,"naming_rights",g.s["block"]["naming_rights"].Bool,"ending",ending,"capstone",g.s["block"].Get("capstone",V.Map()).Duplicate(true),"eligible_capstone",item,"can_archive",ending.Get("triggered",false).Bool);
    }
    public static V archive(Game g) {
        ensure(g);
        if (!g.s["block"].Get("ending",V.Map()).Get("triggered",false).Bool) return V.Map("ok",false,"error","本局高速发展预算尚未耗尽。");
        if (string.IsNullOrWhiteSpace(g.storageRoot)) return V.Map("ok",false,"error","Unity城市记录目录尚未配置，旧城未删除。");
        var id=g.s["block"]["ending"].Get("archive_id","").Text;
        if (id=="") {
            var source=g.s["seed"].Text+":"+g.s["started_at"].Text+":"+g.s["block"]["ending"]["local_elapsed_seconds"].Text;
            using(var hash=SHA256.Create()) id=string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(source)).Select(x=>x.ToString("x2"))).Substring(0,24);
        }
        var directory=Path.Combine(g.storageRoot,"city_records");
        var path=Path.Combine(directory,id+".json");
        try {Directory.CreateDirectory(directory);} catch (IOException) {return V.Map("ok",false,"error","无法创建城市记录目录。");} catch(UnauthorizedAccessException) {return V.Map("ok",false,"error","无法创建城市记录目录。");}
        if (File.Exists(path)) {
            V existing;
            try {existing=V.Parse(File.ReadAllText(path));} catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is Newtonsoft.Json.JsonException) {return V.Map("ok",false,"error","已有城市记录损坏；已保留旧城，请先备份修复记录。");}
            if (!existing.IsMap || !existing.Get("read_only",false).Bool || existing.Get("id","").Text!=id || !existing.Get("city_state").IsMap) return V.Map("ok",false,"error","已有城市记录损坏；已保留旧城，请先备份修复记录。");
            g.s["block"]["ending"]["archive_id"]=id;
            return V.Map("ok",true,"path",path,"record",existing,"existing",true);
        }
        var record=V.Map("record_version",1,"read_only",true,"id",id,"build",g.c.data["build"],"seed",g.s["seed"],"name",g.s["block"]["display_name"],
            "local_elapsed_seconds",g.s["beta_time"]["local_elapsed_seconds"],"active_real_seconds",g.s["beta_time"]["active_real_seconds"],
            "theme",g.s["block"].Get("theme",V.Map()).Duplicate(true),"dt_spent",g.s["totals"]["dt_used"],"population",g.s["metrics"].Get("population",0),
            "currency",g.s["currency"],"capstone",g.s["block"].Get("capstone",V.Map()).Duplicate(true),"blueprint_levels",g.s["block"].Get("blueprint_levels",V.Map()).Duplicate(true),
            "bonus_instances",g.s["bonus_instances"].Duplicate(true),"rewards",g.s["block"]["reward_choices"].Duplicate(true),"history",g.s["log"].Duplicate(true),"city_state",g.s.Duplicate(true));
        try {File.WriteAllText(path+".tmp",record.ToJson(true,true),new UTF8Encoding(false));} catch(Exception e) when(e is IOException || e is UnauthorizedAccessException) {return V.Map("ok",false,"error","城市记录写入失败，旧城未删除。");}
        try {File.Move(path+".tmp",path);} catch(Exception e) when(e is IOException || e is UnauthorizedAccessException) {return V.Map("ok",false,"error","无法保存城市记录，旧城未删除。");}
        g.s["block"]["ending"]["archive_id"]=id;
        return V.Map("ok",true,"path",path,"record",record,"existing",false);
    }
    public static V list_archives(Game g) {
        var result=V.Arr();
        if (string.IsNullOrWhiteSpace(g.storageRoot)) return result;
        var directory=Path.Combine(g.storageRoot,"city_records");
        if (!Directory.Exists(directory)) return result;
        foreach (var path in Directory.GetFiles(directory).OrderBy(x=>x,StringComparer.Ordinal)) {
            if (!path.EndsWith(".json",StringComparison.Ordinal)) continue;
            try {var value=V.Parse(File.ReadAllText(path));if(value.IsMap && value.Get("read_only",false).Bool) result.Add(value);} catch(Newtonsoft.Json.JsonException) { }
        }
        return result;
    }
    public static string name_block(Game g,string name) {
        ensure(g);
        var value=name.Trim();
        var max_length=(int)g.c.v040.Get("progression",V.Map()).Get("name_max_length",24).Long;
        // Godot's String.length counts Unicode scalar characters, not UTF-16 units.
        var length=value.Length-value.Count(char.IsHighSurrogate);
        if (value=="" || length>max_length) return $"街区名称不能为空，且最多{max_length}个字符。";
        if (!g.s["block"].Get("naming_rights",false).Bool) return "本局DT实际归零后获得一次命名权。";
        g.s["block"]["display_name"]=value;g.s["block"]["naming_rights"]=false;
        g.log_event("block_named",V.Map("name",value),"街区命名为「"+value+"」。");
        return "";
    }
}
}
