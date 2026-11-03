using System;
using System.Linq;
namespace MET.Core {
public sealed partial class CitySession {
    V BuildResults=>s.Get("legacy",V.Map()).Get("build_results",V.Map());
    public string ChooseBuildLegacy(string profileId) {
        if(!CanEditFocused)return "只能为当前获授权街区选择构筑成果。";
        var g=FocusedGame;var ending=g.s["block"].Get("ending",V.Map());var evidence=ending.Get("build_result",V.Map());
        if(!ending.Get("capstone_claimed",false).Bool||!g.s["block"].Get("capstone").IsMap)return "先领取原街区主题地标，再选择构筑成果。";
        string key="build:"+FocusedBlockId;if(BuildResults.Has(key))return "本街区已经选择过构筑成果。";
        var candidate=evidence.Get("candidates",V.Arr()).FirstOrDefault(x=>x.Get("profile_id",x.Get("id","")).Text==profileId);
        var profile=RogueContent.Profile(c,profileId);
        if(candidate==null||profile.Count==0||evidence.Get("eligible_theme","").Text!=ending.Get("eligible_theme","").Text||profile["theme"].Text!=ending.Get("eligible_theme","").Text)return "这项成果不在首次预算耗尽时锁定的候选中。";
        var record=V.Map("id",key,"source_block",FocusedBlockId,"source_district",ActiveDistrictId,"source_capstone",g.s["block"]["capstone"]["id"],"source_landmark",g.s["block"]["capstone"].Duplicate(true),"profile_id",profileId,"grade",candidate["grade"],"name",profile["name"],"tradeoff",profile["tradeoff"],"created_external",s["external_seconds"]);
        record["record_id"]=RecordId("build_legacy_chosen",record,key);
        if(!s["legacy"].Has("build_results"))s["legacy"]["build_results"]=V.Map();s["legacy"]["build_results"][key]=record;return "";
    }
    static V BuildOpportunity(Catalog c,V result) {
        var profile=RogueContent.Profile(c,result.Get("profile_id","").Text);if(profile.Count==0)return V.Map();int grade=(int)result.Get("grade",0).Long;
        var rules=profile.Get("charter_rules",V.Arr()).Duplicate(true);foreach(var rule in rules){if(rule.Get("values").IsArray)rule["value"]=rule["values"][grade];rule.Remove("values");}
        return V.Map("id",result["id"],"name",profile["name"],"source_block",result["source_block"],"source_district",result["source_district"],"source_capstone",result["source_capstone"],"source_landmark",result["source_landmark"].Duplicate(true),"profile_id",result["profile_id"],"grade",grade,"record_id",result["record_id"],"cost",profile["cost_by_grade"][grade].Duplicate(true),"rules",rules,"knowledge_multiplier",profile.Get("knowledge_multiplier",V.Arr(1.0,1.0,1.0))[grade],"tradeoff",profile["tradeoff"]);
    }
    V Opportunity(string id)=>BuildResults.Has(id)?BuildOpportunity(c,BuildResults[id]):V.Map();
    string CheckOpportunity(string specialization,string id) {
        if(id=="")return "";if(!BuildResults.Has(id))return "请选择已在同城记录的真实构筑成果。";
        return RogueContent.Profile(c,BuildResults[id]["profile_id"].Text).Get("specializations",V.Arr()).Contains(specialization)?"":"这项规约不适用于所选街区方向。";
    }
    public V StartupOpportunities(bool district,string specialization) {
        var result=V.Arr();foreach(var record in BuildResults.Values)if(CheckOpportunity(specialization,record["id"].Text)=="")result.Add(BuildOpportunity(c,record));return result;
    }
    public V BuildLegacyView() {
        var evidence=FocusedGame.s.Get("block",V.Map()).Get("ending",V.Map()).Get("build_result",V.Map()).Duplicate(true);var chosen=BuildResults.Get("build:"+FocusedBlockId,V.Map()).Duplicate(true);var pending=V.Arr();
        if(chosen.Count==0)foreach(var item in evidence.Get("candidates",V.Arr())){var candidate=item.Duplicate(true);string id=candidate.Get("profile_id",candidate.Get("id","")).Text;var profile=RogueContent.Profile(c,id);candidate["id"]=id;candidate["profile_id"]=id;candidate["name"]=profile.Get("name",id);candidate["tradeoff"]=profile.Get("tradeoff","");pending.Add(candidate);}
        var results=V.From(BuildResults.Values.Select(x=>x.Duplicate(true)).ToArray());var opportunities=V.From(BuildResults.Values.Select(x=>BuildOpportunity(c,x)).ToArray());
        return V.Map("pending",pending,"chosen",chosen,"results",results,"profiles",RogueContent.Profiles(c).Duplicate(true),"evidence",evidence,"opportunities",opportunities);
    }
    static string ValidateBuildLegacy(Catalog c,V state,V history) {
        var results=state["legacy"].Get("build_results",V.Map());if(!results.IsMap)return "构筑成果记录损坏。";var records=V.Map();
        foreach(var pair in results.Pairs){var r=pair.Value;string source=r.Get("source_block","").Text,district=r.Get("source_district","").Text,profileId=r.Get("profile_id","").Text;var profile=RogueContent.Profile(c,profileId);
            if(!r.IsMap||pair.Key!="build:"+source||r.Get("id","").Text!=pair.Key||!state["blocks"].Has(source)||state["blocks"][source]["district_id"].Text!=district||profile.Count==0||!Number(r.Get("grade"),0)||r["grade"].Double>2||r["grade"].Double!=Math.Floor(r["grade"].Double))return "构筑成果失去原街区或合法等级。";
            string rid=r.Get("record_id","").Text;if(!history.Has(rid)||records.Has(rid)||state["receipts"].Get(pair.Key,"").Text!=rid)return "构筑成果历史来源重复或缺失。";records[rid]=true;
            var local=state["blocks"][source]["state"]["block"];var ending=local.Get("ending",V.Map());var evidence=ending.Get("build_result",V.Map());
            if(!ending.Get("triggered",false).Bool||!ending.Get("capstone_claimed",false).Bool||local.Get("capstone",V.Map()).Get("id","").Text!=r.Get("source_capstone","").Text||profile["theme"].Text!=ending.Get("eligible_theme","").Text||evidence.Get("eligible_theme","").Text!=profile["theme"].Text||!evidence.Get("candidates",V.Arr()).Any(x=>x.Get("profile_id",x.Get("id","")).Text==profileId&&x.Get("grade",-1)==r["grade"]))return "构筑成果失去首次零锁定资格或原地标。";
            if(!r.Get("source_landmark").IsMap||r["source_landmark"]!=local["capstone"])return "构筑成果地标来源损坏。";
            var evt=state["history"].FirstOrDefault(x=>x["id"].Text==rid);if(evt==null||evt["kind"].Text!="build_legacy_chosen"||evt["payload"].Get("source_block","").Text!=source||evt["payload"].Get("profile_id","").Text!=profileId||evt["payload"].Get("grade",-1)!=r["grade"])return "构筑成果历史与选择不一致。";
        }
        foreach(var block in state["blocks"].Values){var source=block["start_sources"];if(!source.Has("opportunity"))continue;var o=source["opportunity"];string id=o.Get("id","").Text;
            if(source["kind"].Text!="paid_transfer"||!results.Has(id)||o!=BuildOpportunity(c,results[id])||!RogueContent.Profile(c,o["profile_id"].Text)["specializations"].Contains(block["specialization"]))return "启动规约失去真实成果来源或适用方向。";
            if(source["cost"]["money"].Double<o["cost"]["money"].Double)return "启动规约缺少实际投入。";foreach(var stock in o["cost"]["stock"].Pairs)if(source["cost"]["stock"].Get(stock.Key,0).Double<stock.Value.Double)return "启动规约缺少实际物资。";
        }
        return "";
    }
}
}
