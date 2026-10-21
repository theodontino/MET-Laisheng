using System;
using System.Linq;
namespace MET.Core {
public sealed partial class CitySession {
    public void Record(string kind,V payload,string key="")=>RecordId(kind,payload,key);
    string RecordId(string kind,V payload,string key="") {
        if(key!=""&&s["receipts"].Has(key))return s["receipts"][key].Text;
        string id=Uid("H");s["history"].Add(V.Map("id",id,"kind",kind,"external_seconds",s["external_seconds"],"district_id",ActiveDistrictId,"block_id",FocusedBlockId,"payload",payload.Duplicate(true)));
        if(key!="")s["receipts"][key]=id;return id;
    }
    public void CaptureHistory() {
        foreach(var pair in Games.OrderBy(p=>p.Key,StringComparer.Ordinal)) {
            string id=pair.Key;var g=pair.Value;var meta=Block(id);var projects=meta["observed_projects"];var identity=V.Map("population",g.s["metrics"].Get("population",0),"industry",0,"agriculture",0,"services",0,"waterfront",0,"adjacent_industry",0);
            foreach(var bid in g.playable_ids()) {
                var p=g.s["projects"][bid];string stamp=p["def"].Text+":"+p.Get("level",0).Text+":"+p["status"].Text;
                if(projects.Get(bid.Text,"").Text!=stamp){Record("building_changed",V.Map("block_id",id,"entity_id",bid,"definition",p["def"],"operator",p["operator_org"],"level",p.Get("level",0),"status",p["status"],"tile",p["tile"]));projects[bid.Text]=stamp;}
                if(p["status"].Text!="running")continue;
                var traits=p["traits"];if(traits.Contains("building.industrial"))identity["industry"]+=1;if(traits.Contains("building.agricultural"))identity["agriculture"]+=1;if(traits.Contains("building.service")||traits.Contains("building.public_service"))identity["services"]+=1;
                if(g.s["cells"].Get(p["tile"],V.Map()).Get("waterfront",false).Bool)identity["waterfront"]+=1;
            }
            foreach(string previous in projects.Keys.ToArray())if(!g.s["projects"].Has(previous)){Record("building_removed",V.Map("block_id",id,"entity_id",previous,"last_identity",projects[previous]));projects.Remove(previous);}
            foreach(var reward in g.s["block"]["reward_choices"]) {
                string rid=reward["reward_id"].Text;if(!reward.Get("claimed",false).Bool||meta["observed_rewards"].Has(rid))continue;
                Record("reward_claimed",V.Map("block_id",id,"reward_id",rid,"bonus_id",reward.Get("chosen_bonus",""),"kind",reward.Get("kind","copy")),id+":reward:"+rid);meta["observed_rewards"][rid]=true;
            }
            int band=(int)Math.Floor(identity["population"].Double/25);if(band>meta["observed_population_band"].Long){Record("population_milestone",V.Map("block_id",id,"population",identity["population"],"band",band),id+":population:"+band);meta["observed_population_band"]=band;}
            string tag=identity["industry"].Long>=2?"工业中心":identity["agriculture"].Long>=2?"农业聚落":identity["services"].Long>=2?"公共服务社区":identity["waterfront"].Long>=2?"滨水聚落":"成长中的聚落";identity["label"]=tag;
            if(meta["identity"].Get("label","").Text!=tag)Record("local_identity",V.Map("block_id",id,"identity",identity));meta["identity"]=identity;
            // The journal is bounded locally. Persist the actual last consumed entry, not a mutable array index.
            var log=g.s["log"];string last=meta.Get("history_log_tail","").Text;int start=0;
            if(last!=""){for(int i=log.Count-1;i>=0;i--)if(Store.encode(log[i])==last){start=i+1;break;}}
            for(int i=start;i<log.Count;i++){var item=log[i];long cursor=meta["history_cursor"].Long;Record("local_event",V.Map("block_id",id,"local_cursor",cursor,"event",item),id+":local:"+cursor);meta["history_cursor"]=cursor+1;}
            if(log.Count>0)meta["history_log_tail"]=Store.encode(log[-1]);
        }
        foreach(var d in s["districts"].Values) {
            var identity=V.Map("population",0.0,"industry",0,"agriculture",0,"services",0,"blocks",d["block_ids"].Count,"institutions",d["institutions"].Count);
            foreach(var id in d["block_ids"])foreach(string field in new[]{"population","industry","agriculture","services"})identity[field]+=Block(id.Text)["identity"].Get(field,0);
            identity["label"]=identity["industry"].Long>identity["agriculture"].Long?"工业地区":identity["agriculture"].Long>identity["industry"].Long?"农业地区":"综合地区";d["identity"]=identity;
        }
    }
}
}
