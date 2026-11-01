using System;
using System.IO;
using System.Linq;
namespace MET.Core {
/// <summary>Independent whole-city envelope. Every candidate is semantically checked before use.</summary>
public static class CityStore {
    public const string SAVE="met_city_v1.json";
    public static string path(string root,string name=SAVE)=>Path.Combine(root,name);
    public static V decode(Catalog c,string text) {
        try {var state=V.Parse(text);string error=CitySession.ValidateEnvelope(c,state);return error==""?V.Map("ok",true,"state",state):V.Map("ok",false,"error",error);}
        catch(Newtonsoft.Json.JsonException){return V.Map("ok",false,"error","整城存档 JSON 损坏。");}
    }
    public static string write(CitySession session,string path) {
        var state=session.snapshot();string validation=CitySession.ValidateEnvelope(session.c,state);if(validation!="")return "整城状态未保存："+validation;
        try {
            string directory=Path.GetDirectoryName(path);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
            File.WriteAllText(path+".tmp",Store.encode(state));var temp=decode(session.c,File.ReadAllText(path+".tmp"));if(!temp["ok"].Bool)return "临时整城存档语义校验失败；旧档保留。";
            if(File.Exists(path)) {
                var prior=decode(session.c,File.ReadAllText(path));
                if(prior["ok"].Bool){if(File.Exists(path+".bak")){var backup=decode(session.c,File.ReadAllText(path+".bak"));if(!backup["ok"].Bool)File.Move(path+".bak",path+".bak.corrupt-"+DateTime.UtcNow.Ticks);else File.Delete(path+".bak");}File.Move(path,path+".bak");}
                else File.Move(path,path+".corrupt-"+DateTime.UtcNow.Ticks);
            }
            File.Move(path+".tmp",path);return "";
        }catch(IOException e){return "整城存档写入失败："+e.Message;}catch(UnauthorizedAccessException e){return "整城存档写入失败："+e.Message;}
    }
    public static V read(Catalog c,string path) {
        try {string errors="";foreach(string candidate in new[]{path,path+".tmp",path+".bak"}) {
            if(!File.Exists(candidate))continue;var value=decode(c,File.ReadAllText(candidate));if(!value["ok"].Bool){errors+=Path.GetFileName(candidate)+"："+value["error"].Text+" ";continue;}
            value["from_backup"]=candidate!=path;value["source_path"]=candidate;
            // The caller restores only this fully checked envelope; damaged candidates remain available for inspection.
            return value;
        }return V.Map("ok",false,"error",errors==""?"没有整城存档。":"整城主档、临时档和备份均不可用。 "+errors);}
        catch(IOException e){return V.Map("ok",false,"error","整城存档读取失败："+e.Message);}catch(UnauthorizedAccessException e){return V.Map("ok",false,"error","整城存档读取失败："+e.Message);}
    }
}
public sealed partial class CitySession {
    public static string ValidateEnvelope(Catalog catalog,V state) {
        try{return CheckEnvelope(catalog,state);}catch(Exception e) when(e is InvalidCastException||e is FormatException||e is OverflowException||e is ArgumentException||e is System.Collections.Generic.KeyNotFoundException){return "整城存档字段结构损坏。";}
    }
    static bool Number(V n,double min=double.NegativeInfinity)=>Game.is_num(n)&&n.Double>=min;
    static string CheckEnvelope(Catalog c,V s) {
        if(!s.IsMap||s.Get("schema",0)!=V.From(1)||s.Get("build","")!=c.data["build"])return "这是其他版本的整城存档。";
        if(!FiniteTree(s))return "整城含非有限数值。";
        foreach(string field in new[]{"districts","blocks","links","legacy","science","projects","receipts","network","world_market"})if(!s.Get(field).IsMap)return "整城缺少对象："+field;
        if(!s["network"].Get("ledger").IsArray||!s["network"].Get("last_tick").IsMap||!s["world_market"].Get("resources").IsMap||!s["world_market"].Get("ledger").IsArray||!s["world_market"].Get("last_tick").IsMap||!Number(s["world_market"].Get("capacity_per_hour"),0)||!Number(s["world_market"].Get("currency"),0))return "世界供应或物流账本损坏。";
        foreach(string resource in new[]{"food","material","goods"}){var stock=s["world_market"]["resources"].Get(resource);if(!stock.IsMap||!stock.Get("pricing").IsMap||!Number(stock.Get("stock"),0)||!Number(stock.Get("capacity"),0)||stock["stock"].Double>stock["capacity"].Double+.000001)return "世界库存或价格来源损坏。";foreach(string field in new[]{"base_price","amplitude","market_depth","spread","fee"})if(!Number(stock["pricing"].Get(field)))return "世界价格字段损坏。";}
        foreach(string field in new[]{"technologies","research","production"})if(!s["science"].Get(field).IsMap)return "科研字段缺失："+field;
        if(!s.Get("history").IsArray||!s.Get("paused").IsBool)return "整城历史或暂停状态损坏。";
        foreach(string field in new[]{"seed","next_id","external_seconds","last_seen_utc","offline_seconds","treasury"})if(!Number(s.Get(field),field=="seed"||field=="treasury"?double.NegativeInfinity:0))return "整城数值损坏："+field;
        if(s["next_id"].Double<1||s["next_id"].Double!=Math.Floor(s["next_id"].Double)||!s.Get("city_id").IsString||!s.Get("name").IsString)return "整城身份损坏。";
        string active=s.Get("active_district_id","").Text,focus=s.Get("focused_block_id","").Text,phase=s.Get("phase","").Text;
        if(phase!="choose_district"&&phase!="play")return "整城阶段损坏。";
        if((active=="")!=(phase=="choose_district"))return "在任地区与城市阶段不一致。";
        if(focus!=""&&!s["blocks"].Has(focus)||s["blocks"].Count>0&&focus=="")return "焦点街区不存在。";
        var assigned=V.Map();int activeCount=0;var allHistory=V.Map();long greatestId=0;
        foreach(var row in s["history"]) {
            string id=row.Get("id","").Text;if(!row.IsMap||id==""||allHistory.Has(id)||!row.Get("payload").IsMap||!Number(row.Get("external_seconds"),0)||!row.Get("kind").IsString)return "城市历史身份或来源损坏。";
            allHistory[id]=true;TrackId(id,ref greatestId);
        }
        foreach(var receipt in s["receipts"].Values)if(!receipt.IsString||!allHistory.Has(receipt.Text))return "城市历史去重游标失去事件。";
        foreach(var pair in s["districts"].Pairs) {
            var d=pair.Value;string id=pair.Key;TrackId(id,ref greatestId);
            if(!d.IsMap||d.Get("id","").Text!=id||!d.Get("name").IsString||!d.Get("block_ids").IsArray||!d.Get("run_history").IsArray||!d.Get("institutions").IsMap||!d.Get("promises").IsMap||!d.Get("crisis").IsMap||!d.Get("identity").IsMap)return "地区归属或记录损坏。";
            string status=d.Get("status","").Text;if(status=="active"){activeCount++;if(id!=active)return "城市存在未登记的在任地区。";}else if(status!="autonomous")return "地区状态损坏。";
            if(!Number(d.Get("support"),0)||d["support"].Double>100||!Number(d.Get("mandate"),0)||d["mandate"].Double>100||!Number(d.Get("term"),1)||!Number(d.Get("term_started_external"),0))return "地区治理数值损坏。";
            if(!d["crisis"].Get("active").IsBool||!Number(d["crisis"].Get("bad_months"),0)||!d["crisis"].Get("reason").IsString||!Number(d["promises"].Get("stable_blocks"),1))return "地区危机或承诺损坏。";
            if(d["block_ids"].Count==0)return "地区没有街区。";
            foreach(var bid in d["block_ids"]){if(!bid.IsString||!s["blocks"].Has(bid.Text)||assigned.Has(bid.Text)||s["blocks"][bid.Text].Get("district_id","").Text!=id)return "街区归属缺失或重复。";assigned[bid.Text]=true;}
            foreach(var term in d["run_history"])if(!term.IsMap||!allHistory.Has(term.Get("record_id","").Text)||!Number(term.Get("term"),1)||!Number(term.Get("started_external"),0)||!Number(term.Get("ended_external"),0)||term["ended_external"].Double<term["started_external"].Double||term["term"].Long>=d["term"].Long&&status=="active")return "地区任期历史损坏。";
            foreach(var institution in d["institutions"].Values){string error=CheckProvenance(s,institution,allHistory,id);if(error!="")return error;}
        }
        if(activeCount!=(active==""?0:1)||active!=""&&!s["districts"].Has(active))return "必须恰好有一个登记的在任地区。";
        foreach(var pair in s["blocks"].Pairs) {
            string id=pair.Key;var b=pair.Value;TrackId(id,ref greatestId);
            if(!b.IsMap||b.Get("id","").Text!=id||!assigned.Has(id)||!b.Get("state").IsMap||!b.Get("start_sources").IsMap||!b.Get("observed_projects").IsMap||!b.Get("observed_rewards").IsMap||!b.Get("identity").IsMap)return "街区元数据或完整状态损坏。";
            if(!V.Arr("building","stable").Contains(b.Get("status","").Text)||!V.Arr("balanced","agricultural","industrial","services","research","logistics").Contains(b.Get("specialization","").Text)||!Number(b.Get("created_external"),0)||!Number(b.Get("history_cursor"),0)||!Number(b.Get("observed_population_band"),0))return "街区状态或历史游标损坏。";
            var source=b["start_sources"];string kind=source.Get("kind","").Text;if(!source.Get("settled").IsBool||!source.Get("district_start").IsBool||!source.Get("cost").IsMap)return "街区启动来源损坏。";
            if(kind=="paid_transfer"){string donor=source.Get("source_block","").Text;if(!s["blocks"].Has(donor)||donor==id||!Number(source["cost"].Get("money"),0)||!source["cost"].Get("stock").IsMap)return "启动包失去真实供给来源。";foreach(var stock in source["cost"]["stock"].Values)if(!Number(stock,0))return "启动物资损坏。";if(b["state"]["phase"].Text=="play"&&!source["settled"].Bool)return "启动包尚未完成一次性交付。";}
            else if(kind!="initial_charter"||!source["settled"].Bool||source.Get("source_block","").Text!="")return "街区启动来源无效。";
            if(b["state"].Get("build","")!=c.data["build"])return "街区版本与整城不一致。";var g=new Game(c);string local=g.validate_state(b["state"]);if(local!="")return "街区 "+id+"："+local;
            foreach(var claimed in b["observed_rewards"].Pairs)if(!b["state"]["block"]["reward_choices"].Any(r=>r.Get("reward_id","").Text==claimed.Key&&r.Get("claimed",false).Bool))return "奖励消费游标失去真实奖励。";
        }
        if(s["blocks"].Values.Count(b=>b["start_sources"].Get("kind","").Text=="initial_charter")>1)return "重复的免费初始地区。";
        if(!s["legacy"].Get("capabilities").IsMap||!s["legacy"].Get("portable_transfers").IsArray)return "分层遗产损坏。";
        foreach(var capability in s["legacy"]["capabilities"].Values){string did=capability.Get("source_district","").Text;if(!s["districts"].Has(did))return "城市能力失去地区来源。";string error=CheckProvenance(s,capability,allHistory,did);if(error!="")return error;}
        var transfers=V.Map();foreach(var move in s["legacy"]["portable_transfers"]){string id=move.Get("id","").Text;TrackId(id,ref greatestId);if(!move.IsMap||id==""||transfers.Has(id)||!s["blocks"].Has(move.Get("source_block","").Text)||!s["blocks"].Has(move.Get("target_block","").Text)||move["source_block"]==move["target_block"]||!c.bonuses.Has(move.Get("bonus_id","").Text)||!allHistory.Has(move.Get("record_id","").Text))return "Portable 许可转移记录损坏。";transfers[id]=true;var wallet=s["blocks"][move["target_block"].Text]["state"]["block"]["bonus_wallet"];if(!wallet.Get(move["bonus_id"].Text,V.Map()).Get("grant_sources",V.Arr()).Contains("portable:"+id))return "Portable 转移缺少真实许可来源。";}
        string buildError=ValidateBuildLegacy(c,s,allHistory);if(buildError!="")return buildError;
        foreach(var pair in s["links"].Pairs){var link=pair.Value;if(!link.IsMap||link.Get("id",pair.Key).Text!=pair.Key||!s["blocks"].Has(link.Get("from","").Text)||!s["blocks"].Has(link.Get("to","").Text)||link["from"]==link["to"])return "物流边归属损坏。";if(!link.Get("enabled").IsBool||!link.Get("cross_district").IsBool||!V.Arr("food","material","goods","advanced_components").Contains(link.Get("resource","").Text)||link["cross_district"].Bool!=(s["blocks"][link["from"].Text]["district_id"]!=s["blocks"][link["to"].Text]["district_id"]))return "物流资源或地区尺度损坏。";foreach(var field in link.Pairs)if(field.Value.IsNumber&&!Number(field.Value,0))return "物流边数值损坏。";}
        string networkError=ValidateWorldEnvelope(s);if(networkError!="")return networkError;
        string scienceError=ValidateScienceEnvelope(s);if(scienceError!="")return scienceError;
        string wildError=ValidateWildEnvelope(c,s);if(wildError!="")return wildError;
        if(s["next_id"].Long<=greatestId)return "整城 ID 游标会重用已存在身份。";
        return "";
    }
    static bool FiniteTree(V value){if(value.IsNumber)return Game.is_num(value);if(value.IsMap)return value.Values.All(FiniteTree);if(value.IsArray)return value.All(FiniteTree);return true;}
    static void TrackId(string id,ref long greatest){if(id.Length>1&&long.TryParse(id.Substring(1),out var suffix))greatest=Math.Max(greatest,suffix);}
    static string CheckProvenance(V s,V record,V history,string district) {
        if(!record.IsMap||!record.Get("id").IsString||!V.Arr("agricultural","industrial","services","research","logistics").Contains(record.Get("kind","").Text)||!record.Get("source_blocks").IsArray||record["source_blocks"].Count==0||!history.Has(record.Get("record_id","").Text))return "永久制度或城市能力来源损坏。";
        foreach(var bid in record["source_blocks"])if(!s["blocks"].Has(bid.Text)||s["blocks"][bid.Text].Get("district_id","").Text!=district||s["blocks"][bid.Text].Get("status","").Text!="stable")return "永久制度失去实际稳定街区来源。";return "";
    }
}
}
