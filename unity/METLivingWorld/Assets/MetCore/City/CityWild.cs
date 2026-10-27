using System;
namespace MET.Core {
public sealed partial class CitySession {
    public const string WildHopeCounterparty="hope";
    public const double WildHousingRequiredExports=10;
    public const double WildHousingCost=60;
    public string EnableWild() {
        if(!CanEditFocused||FocusedGame.s.Get("phase","").Text!="play")return "先进入可经营的街区。";
        string error=WildDesign.Enable(FocusedGame);if(error!="")return error;
        if(!s.Get("wild_technology").IsMap) {
            s["wild_technology"]=V.Map("enabled",true,"material_exported",0.0,"housing_levels",0,"eligibility_history",V.Arr(),"purchase",V.Map());
            Record("wild_enabled",V.Map("block_id",FocusedBlockId),"wild:enabled");
        }
        ApplyWorldEffects();return "";
    }
    // This consumes the exact settlement receipt, before ledger compaction. No
    // history cursor or order amount is reused to infer completed exports.
    void RecordWildTrade(V trade) {
        var tech=s.Get("wild_technology",V.Map());
        if(!tech.Get("enabled",false).Bool||trade.Get("counterparty","").Text!=WildHopeCounterparty||trade.Get("resource","").Text!="material"||trade.Get("side","").Text!="sell")return;
        double before=tech["material_exported"].Double;tech["material_exported"]=before+trade["amount"].Double;
        if(before+1e-6<WildHousingRequiredExports&&tech["material_exported"].Double+1e-6>=WildHousingRequiredExports) {
            var receipt=V.Map("technology","HousingDesignI","counterparty",WildHopeCounterparty,"material_exported",tech["material_exported"],"required_exports",WildHousingRequiredExports,"block_id",trade["block_id"],"trade_id",trade["id"],"order_id",trade["order_id"],"external_seconds",trade["external_seconds"]);
            receipt["record_id"]=RecordId("wild_housing_eligible",receipt,"wild:HousingDesignI:eligible");tech["eligibility_history"].Add(receipt);
        }
    }
    public V WildView() {
        var tech=s.Get("wild_technology",V.Map());bool enabled=tech.Get("enabled",false).Bool,purchased=tech.Get("housing_levels",0).Long>0;
        double exported=tech.Get("material_exported",0).Double,currency=FocusedGame.s.Get("currency",0).Double;
        string reason=!enabled?"Wild 设计尚未启用。":purchased?"住房设计 I 已购入，全城建筑设计容量 +2。":!CanEditFocused||FocusedGame.s.Get("phase","").Text!="play"?"先进入可经营的街区。":exported+1e-6<WildHousingRequiredExports?"向希望城实际出口累计 10 材料后开放购买。":currency<WildHousingCost?"购买需要 60 通货。":"可以购买；已出口的材料不会再次扣除。";
        bool can=enabled&&!purchased&&CanEditFocused&&FocusedGame.s.Get("phase","").Text=="play"&&exported+1e-6>=WildHousingRequiredExports&&currency>=WildHousingCost;
        return V.Map("enabled",enabled,"technology","HousingDesignI","name","住房设计 I","counterparty",WildHopeCounterparty,"counterparty_name","希望城","material_exported",exported,"required_exports",WildHousingRequiredExports,"cost",WildHousingCost,"purchased",purchased,"housing_levels",tech.Get("housing_levels",0),"can_purchase",can,"reason",reason,"currency",currency,"capacity_bonus",2*tech.Get("housing_levels",0).Long,"freight_capacity_per_hour",Games.ContainsKey(FocusedBlockId)?Market._total_capacity(FocusedGame):0,"eligibility_history",tech.Get("eligibility_history",V.Arr()).Duplicate(true),"purchase",tech.Get("purchase",V.Map()).Duplicate(true));
    }
    public string BuyWildHousing() {
        var view=WildView();if(!view["can_purchase"].Bool)return view["reason"].Text;
        var g=FocusedGame;g.s["currency"]-=WildHousingCost;g.s["totals"]["currency_spent"]+=WildHousingCost;
        var tech=s["wild_technology"];tech["housing_levels"]=1;
        var purchase=V.Map("technology","HousingDesignI","counterparty",WildHopeCounterparty,"cost",WildHousingCost,"material_exported",tech["material_exported"],"source_block",FocusedBlockId,"external_seconds",s["external_seconds"],"capacity_bonus",2);
        purchase["record_id"]=RecordId("wild_housing_purchased",purchase,"wild:HousingDesignI:purchase");tech["purchase"]=purchase;
        g.log_event("wild_technology_bought",purchase,"购入住房设计 I，全城建筑设计容量 +2。");ApplyWorldEffects();return "";
    }
    static string ValidateWildEnvelope(V state) {
        if(!state.Has("wild_technology"))return "";
        var tech=state["wild_technology"];
        if(!tech.IsMap||!tech.Get("enabled").IsBool||!Number(tech.Get("material_exported"),0)||!V.Arr(0,1).Contains(tech.Get("housing_levels"))||!tech.Get("eligibility_history").IsArray||!tech.Get("purchase").IsMap)return "Wild 城市技术状态损坏。";
        if(tech["housing_levels"].Long>0&&(tech["material_exported"].Double+1e-6<WildHousingRequiredExports||tech["purchase"].Get("technology","").Text!="HousingDesignI"||!state["blocks"].Has(tech["purchase"].Get("source_block","").Text)))return "住房设计 I 缺少出口或购买来源。";
        return "";
    }
}
}
