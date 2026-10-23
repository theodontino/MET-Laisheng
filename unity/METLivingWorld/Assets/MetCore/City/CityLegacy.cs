using System;
using System.Linq;
namespace MET.Core {
public sealed partial class CitySession {
    public string TransferPortable(string sourceBlock,string bonusId,string targetBlock) {
        if(!Games.ContainsKey(sourceBlock)||!Games.ContainsKey(targetBlock)||sourceBlock==targetBlock)return "请选择两个不同的真实街区。";
        if(targetBlock!=FocusedBlockId||!CanEditFocused)return "目标必须是当前获授权的焦点街区。";
        if(!arc["legacy"]["portable"].Contains(bonusId))return "这是 Local 方案，留在原街区。";
        var from=Games[sourceBlock];var to=Games[targetBlock];Bonus._ensure(from);Bonus._ensure(to);
        var license=from.s["block"]["bonus_wallet"].Get(bonusId,V.Map());if(license.Get("charges",0).Long<=0)return "原街区没有尚未部署的许可。";
        string tid=Uid("T");string error=Bonus.grant(to,bonusId,"portable:"+tid);if(error!="")return error;
        license["charges"]=license["charges"].Long-1;license["persistence"]="Portable";to.s["block"]["bonus_wallet"][bonusId]["persistence"]="Portable";
        var move=V.Map("id",tid,"source_block",sourceBlock,"target_block",targetBlock,"bonus_id",bonusId,"external_seconds",s["external_seconds"],"source",license.Get("acquired_from",""));
        move["record_id"]=RecordId("portable_transferred",move,tid);s["legacy"]["portable_transfers"].Add(move);return "";
    }
    void RefreshLegacy(string districtId) {
        var d=District(districtId);foreach(string kind in new[]{"agricultural","industrial","services","research","logistics"}) {
            var blocks=V.Arr();foreach(var id in d["block_ids"]){if(Block(id.Text)["status"].Text!="stable"||Block(id.Text)["specialization"].Text!=kind)continue;
                var g=Games[id.Text];if(g.s["phase"].Text!="play")continue;bool actual=false;
                foreach(var bid in g.playable_ids()){var p=g.s["projects"][bid];if(p["status"].Text!="running"||p["paused"].Bool||p.Get("effective_hours",0).Double<=0)continue;var stats=g.project_stats(bid.Text);
                    if(kind=="agricultural"&&stats.Get("output.food",0).Double>0||kind=="industrial"&&(stats.Get("output.material",0).Double>0||stats.Get("output.goods",0).Double>0)||kind=="services"&&(stats.Get("service.medical",0).Double>0||stats.Get("service.commercial",0).Double>0)||kind=="research"&&stats.Get("service.education",0).Double>0||kind=="logistics"&&stats.Get("trade_capacity",stats.Get("trade.capacity",0)).Double>0)actual=true;
                }
                if(actual)blocks.Add(id);
            }
            if(blocks.Count==0)continue;string iid=districtId+":"+kind;
            if(!d["institutions"].Has(iid)){string rid=RecordId("institution_founded",V.Map("district_id",districtId,"kind",kind,"source_blocks",blocks),iid+":institution");d["institutions"][iid]=V.Map("id",iid,"kind",kind,"source_blocks",blocks.Duplicate(true),"record_id",rid);}
            if(!s["legacy"]["capabilities"].Has(iid)){string rid=RecordId("city_capability",V.Map("source_district",districtId,"kind",kind,"source_blocks",blocks),iid+":capability");s["legacy"]["capabilities"][iid]=V.Map("id",iid,"kind",kind,"source_district",districtId,"source_blocks",blocks.Duplicate(true),"record_id",rid,"created_external",s["external_seconds"]);}
        }
    }
    public V LegacyView()=>V.Map("city",s["legacy"].Duplicate(true),"districts",V.From(s["districts"].Values.Select(d=>V.Map("id",d["id"],"name",d["name"],"institutions",d["institutions"].Duplicate(true))).ToArray()),"portable_ids",arc["legacy"]["portable"].Duplicate(true));
}
}
