using System;
using System.Linq;
namespace MET.Core {
public sealed partial class CitySession {
    static double Bounded(double value,double lo=0,double hi=100)=>Math.Max(lo,Math.Min(hi,value));
    public V GovernanceView(string districtId="") {
        var d=District(districtId==""?ActiveDistrictId:districtId);if(d.Count==0)return V.Map();
        double population=0,food=0,services=0,pollution=0,cash=0,flow=0;int stable=0,play=0;var sources=V.Arr();
        foreach(var id in d["block_ids"]){var g=Games[id.Text];if(g.s["phase"].Text!="play")continue;play++;
            double p=Math.Max(1,g.s["metrics"].Get("population",0).Double);population+=p;food+=p*Progress.food_ratio(g);
            double service=(Progress.service_ratio(g,"medical")+Progress.service_ratio(g,"commercial")+Progress.service_ratio(g,"education"))/3;services+=p*service;pollution+=p*g.s["metrics"].Get("pollution",0).Double;cash+=g.s["currency"].Double;flow+=g.s["metrics"].Get("cashflow",0).Double;
            if(Block(id.Text)["status"].Text=="stable")stable++;sources.Add(V.Map("block_id",id,"population",p,"food",Progress.food_ratio(g),"services",service,"pollution",g.s["metrics"].Get("pollution",0),"cashflow",g.s["metrics"].Get("cashflow",0)));
        }
        return V.Map("district_id",d["id"],"status",d["status"],"support",d["support"],"mandate",d["mandate"],"population",population,"food",food/Math.Max(1,population),"services",services/Math.Max(1,population),"pollution",pollution/Math.Max(1,population),"currency",cash,"cashflow",flow,"stable_blocks",stable,"play_blocks",play,"promises",d["promises"].Duplicate(true),"crisis",d["crisis"].Duplicate(true),"sources",sources);
    }
    public string StabilizeBlock(string id) {
        if(!Games.ContainsKey(id)||Block(id)["district_id"].Text!=ActiveDistrictId)return "只有在任地区可以确认街区稳定。";
        if(Block(id)["status"].Text=="stable")return "";var g=Games[id];
        if(g.s["phase"].Text!="play"||g.s["local_hours"].Double<1||!Progress.safe(g))return "需实际经营至少1来生月，并维持粮食、住房、电力和财政安全。";
        Block(id)["status"]="stable";Block(id)["stabilized_external"]=s["external_seconds"];Record("block_stabilized",V.Map("block_id",id,"district_id",ActiveDistrictId,"metrics",g.s["metrics"].Duplicate(true)),id+":stabilized");
        RefreshLegacy(ActiveDistrictId);return "";
    }
    public string HandoverDistrict() {
        if(ActiveDistrictId=="")return "当前没有在任地区。";var v=GovernanceView();var p=arc["governance"];
        if(v["stable_blocks"].Long<p["handover_blocks"].Long)return "交班承诺尚未完成：需要两个实际稳定街区。";
        if(v["food"].Double<p["food_target"].Double||v["currency"].Double<0||v["support"].Double<35||v["mandate"].Double<35)return "地区供给或治理尚不稳定，不能按成果交班。";
        EndTerm("handover","达成地区稳定承诺后主动交班");return "";
    }
    void EndTerm(string kind,string reason) {
        string id=ActiveDistrictId;if(id=="")return;var d=District(id);RefreshLegacy(id);CaptureHistory();
        string eventId=RecordId("district_retired",V.Map("district_id",id,"term",d["term"],"reason",reason,"outcome",kind,"readings",GovernanceView(id)),id+":term:"+d["term"].Text+":end");
        d["run_history"].Add(V.Map("term",d["term"],"started_external",d["term_started_external"],"ended_external",s["external_seconds"],"kind",kind,"reason",reason,"record_id",eventId,"support",d["support"],"mandate",d["mandate"]));
        d["status"]="autonomous";s["active_district_id"]="";s["phase"]="choose_district";
        foreach(var block in d["block_ids"]){var g=Games[block.Text];g.s["settings"]["user_paused"]=false;g.s["beta_time"]["mode"]="normal";}
    }
    public string ReturnDistrict(string id) {
        if(ActiveDistrictId!="")return "先完成当前地区的交班。";var d=District(id);if(d.Count==0||d["status"].Text!="autonomous")return "只能回任已有自治地区。";
        double cost=arc["governance"]["return_money"].Double;string payer=d["block_ids"].Select(x=>x.Text).FirstOrDefault(x=>Games[x].s["phase"].Text=="play"&&Games[x].s["currency"].Double>=cost);
        if(payer==null)return "旧地区需要支付90通货的回任组织费用。";
        Games[payer].s["currency"]=Games[payer].s["currency"].Double-cost;d["status"]="active";d["term"]=d["term"].Long+1;d["term_started_external"]=s["external_seconds"];d["support"]=Bounded(d["support"].Double-arc["governance"]["return_support_cost"].Double);d["mandate"]=Math.Max(40,d["mandate"].Double);d["crisis"]=V.Map("active",false,"reason","","bad_months",0.0);
        s["active_district_id"]=id;s["focused_block_id"]=payer;s["phase"]="play";Record("district_returned",V.Map("district_id",id,"term",d["term"],"paid_by",payer,"money",cost),id+":term:"+d["term"].Text+":start");return "";
    }
    public string RenameDistrict(string id,string name) {
        if(id!=ActiveDistrictId)return "只能命名在任地区。";name=(name??"").Trim();if(name.Length==0||name.Length>40)return "地区名需为1至40个字。";
        var d=District(id);if(d["name"].Text==name)return "";string old=d["name"].Text;d["name"]=name;Record("district_named",V.Map("district_id",id,"before",old,"name",name));return "";
    }
    public void GovernanceTick(V localMonths,double externalSeconds) {
        foreach(string id in s["districts"].Keys.ToArray()) {
            var d=District(id);double months=0;int count=0;foreach(var bid in d["block_ids"]){if(localMonths.Get(bid.Text,0).Double>0){months+=localMonths[bid.Text].Double;count++;}}
            if(count==0)continue;months/=count;var v=GovernanceView(id);var p=arc["governance"];if(v["play_blocks"].Long==0)continue;
            double food=v["food"].Double,service=v["services"].Double,pollution=v["pollution"].Double;
            double fiscal=v["currency"].Double<0?0:v["cashflow"].Double>=0?1:Bounded(v["currency"].Double/Math.Max(1,-v["cashflow"].Double)/4,0,1);
            double target=Bounded(25+food*35+service*25+fiscal*15-Math.Max(0,pollution-p["pollution_limit"].Double)*.7);
            d["support"]=Bounded(d["support"].Double+(target-d["support"].Double)*(1-Math.Exp(-months*p["support_response"].Double)));
            double promise=Math.Min(1,v["stable_blocks"].Double/Math.Max(1,d["promises"]["stable_blocks"].Double));
            double mandate=Bounded(20+food*30+fiscal*25+promise*25-Math.Max(0,pollution-p["pollution_limit"].Double)*.4);
            d["mandate"]=Bounded(d["mandate"].Double+(mandate-d["mandate"].Double)*(1-Math.Exp(-months*p["mandate_response"].Double)));
            bool bad=food<.5||v["currency"].Double<0||d["support"].Double<p["support_floor"].Double||d["mandate"].Double<p["mandate_floor"].Double;
            var crisis=d["crisis"];crisis["bad_months"]=bad?crisis["bad_months"].Double+months:Math.Max(0,crisis["bad_months"].Double-months*.5);
            string reason=food<.5?"持续口粮短缺":v["currency"].Double<0?"持续财政赤字":"支持与授权跌破底线";
            if(bad&&crisis["bad_months"].Double>=p["crisis_months"].Double&&!crisis["active"].Bool){crisis["active"]=true;crisis["reason"]=reason;Record("district_crisis",V.Map("district_id",id,"reason",reason,"readings",v),id+":term:"+d["term"].Text+":crisis");}
            if(!bad&&crisis["bad_months"].Double<=0){crisis["active"]=false;crisis["reason"]="";}
            d["last_readings"]=v;RefreshLegacy(id);
            if(id==ActiveDistrictId&&bad&&crisis["bad_months"].Double>=p["retire_months"].Double)EndTerm("crisis",reason+"，治理权转入自治");
        }
    }
}
}
