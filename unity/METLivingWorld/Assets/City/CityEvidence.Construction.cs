using System;
using System.Collections;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
/// <summary>Opt-in construction decision acceptance in the delivered native Player.</summary>
public sealed partial class CityEvidence {
    static bool ConstructionUxOnly => Environment.GetCommandLineArgs().Contains("--validate-construction-ux");
    V constructionUxEvidence=V.Map();

    IEnumerator ConstructionUxJourney() {
        var ui=city.Interface;
        city.Runtime.NewCity(7040);yield return null;
        Click("建立地区 · 在此登陆");yield return null;
        Click("确立起源 · 安顿居民");yield return null;
        Check("construction_fresh_city_play_phase",city.Runtime.Game.s["phase"].Text=="play");
        city.Runtime.SetPaused(true);
        var g=city.Runtime.Game;
        var tile=g.s["cells"].Keys.First(k=>g.s["cells"][k]["building"].Text==""&&!g.s["cells"][k]["traits"].Contains("tile.water"));
        const string baseId="homestead";
        ui.SelectTile(tile);yield return null;
        Check("construction_real_tile_selected",ui.SelectedTile==tile&&g.s["cells"][tile]["building"].Text=="",tile);
        int initialProjects=g.s["projects"].Count;
        double initialCash=g.s["currency"].Double;
        string create=city.Runtime.Execute(actual=>actual.create_tender(V.Arr(tile),baseId));
        Check("construction_real_tender_created",create==""&&g.s["draft"].Get("offers",V.Arr()).Count==3,create);
        var originalIds=g.s["draft"]["offers"].Select(x=>x["id"].Text).ToArray();
        var originalOffers=g.s["draft"]["offers"].Select(x=>x.ToJson(false,true)).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var firstCost=Draft.offer_cost(g,g.s["draft"]["offers"][0]);
        Check("construction_cost_from_actual_offer",firstCost.Get("currency",-1).Double>=0&&firstCost.Get("material",-1).Double>=0&&firstCost.Get("cp",-1).Double>=0,firstCost);
        ui.OpenArchive("tender");yield return null;Canvas.ForceUpdateCanvases();
        var initial=ui.ConstructionEvidenceLayout();
        Check("construction_1920x1080",Screen.width==1920&&Screen.height==1080&&Screen.fullScreen,V.Arr(Screen.width,Screen.height,Screen.fullScreen));
        Check("construction_identity_benefits_costs_from_live_draft",initial["identity"].Bool&&initial["benefits"].Bool&&initial["costs"].Bool&&initial["cost_matches"].Bool&&initial["benefit_cashflow_matches"].Bool,initial);
        Check("construction_three_real_operator_choices",initial["operator_choice_count"].Long==originalIds.Length&&initial["operator_ids_match"].Bool&&initial["selected_choice_matches"].Bool&&initial["identity_operator_matches"].Bool,initial);
        Check("construction_fixed_controls_fit_and_nav_hidden",initial["fixed_fit"].Bool&&initial["global_nav_hidden"].Bool&&initial["local_nav_hidden"].Bool&&initial["ordinary_footer_hidden"].Bool,initial);
        Check("construction_primary_and_defer_visible",initial["primary_visible"].Bool&&initial["defer_visible"].Bool&&initial["primary_single_line"].Bool&&initial["subtitle_persistent"].Bool,initial);
        yield return Capture("construction-overview.png");
        constructionUxEvidence=V.Map("tile",tile,"base",baseId,"initial_cost",firstCost,"initial_layout",initial,
            "input_scope","Actual native rendering and programmatic Unity Button callbacks. No OS mouse or keyboard input.",
            "screenshots",V.Arr("construction-overview.png"));

        var beforeChoice=GameplayState();
        var choices=ui.GetComponentsInChildren<Button>(true).Where(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith("OperatorChoice ",StringComparison.Ordinal)).ToArray();
        Check("construction_second_operator_control",choices.Length>=2);
        choices[1].onClick.Invoke();yield return null;Canvas.ForceUpdateCanvases();
        Check("construction_operator_switch_no_spend_or_rng_mutation",GameplayState()==beforeChoice&&g.s["currency"].Double==initialCash);
        var changedChoice=ui.ConstructionEvidenceLayout();
        Check("construction_selected_operator_is_real_offer",changedChoice["operator_ids_match"].Bool&&changedChoice["selected_choice_matches"].Bool&&changedChoice["identity_operator_matches"].Bool,changedChoice);

        var full=ui.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.GetComponentInChildren<Text>()?.text=="全部变化与计算依据 ›");
        Check("construction_full_source_disclosure_exists",full!=null);
        ui.ConstructionEvidenceScrollTo(full);yield return null;Canvas.ForceUpdateCanvases();
        Check("construction_full_source_disclosure_reachable",ui.ConstructionEvidenceControlVisible(full));
        full.onClick.Invoke();yield return null;Canvas.ForceUpdateCanvases();
        Check("construction_full_source_expands",ui.ConstructionEvidenceDisclosureExpanded(full)&&GameplayState()==beforeChoice);
        ui.ConstructionEvidenceScrollBottom();yield return null;Canvas.ForceUpdateCanvases();
        var scrolled=ui.ConstructionEvidenceLayout();
        Check("construction_header_footer_pinned_at_scroll_end",scrolled["fixed_fit"].Bool&&
            Math.Abs(scrolled["header_y"].Double-initial["header_y"].Double)<1&&
            Math.Abs(scrolled["footer_y"].Double-initial["footer_y"].Double)<1,scrolled);
        yield return Capture("construction-details-scrolled.png");
        constructionUxEvidence["scrolled_layout"]=scrolled;constructionUxEvidence["screenshots"].Add("construction-details-scrolled.png");

        var mapFold=ui.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.GetComponentInChildren<Text>()?.text=="在地图上比较 ›");
        Check("construction_map_comparison_disclosure_exists",mapFold!=null);
        ui.ConstructionEvidenceScrollTo(mapFold);yield return null;
        Check("construction_map_disclosure_reachable",ui.ConstructionEvidenceControlVisible(mapFold));
        mapFold.onClick.Invoke();yield return null;
        Check("construction_map_comparison_expands",ui.ConstructionEvidenceDisclosureExpanded(mapFold));
        Click("地图 · 建成后");yield return null;
        Check("construction_map_preview_read_only",!ui.IsModal&&city.Runtime.DecisionOpen&&ui.MapSourcePhase=="proposal"&&GameplayState()==beforeChoice);
        Click("返回方案比较 ›");yield return null;
        Check("construction_map_return_keeps_actual_offers",ui.IsModal&&g.s["draft"]["offers"].Select(x=>x["id"].Text).SequenceEqual(originalIds)&&GameplayState()==beforeChoice);

        city.Runtime.SetDisplaySetting("ui_scale",1.2);city.Runtime.SetDisplaySetting("text_scale",1.5);ui.ApplyDisplay();ui.OpenArchive("tender");yield return null;Canvas.ForceUpdateCanvases();
        var large=ui.ConstructionEvidenceLayout();
        Check("construction_large_text_fixed_controls_fit",large["fixed_fit"].Bool&&large["primary_visible"].Bool&&large["defer_visible"].Bool&&large["primary_single_line"].Bool&&large["subtitle_persistent"].Bool,large);
        Check("construction_large_text_live_cost",large["cost_matches"].Bool,large);
        yield return Capture("construction-large-text.png");
        constructionUxEvidence["large_text_layout"]=large;constructionUxEvidence["screenshots"].Add("construction-large-text.png");
        city.Runtime.SetDisplaySetting("ui_scale",1);city.Runtime.SetDisplaySetting("text_scale",1);ui.ApplyDisplay();ui.OpenArchive("tender");yield return null;

        var oldConfirm=ui.ConstructionEvidenceNamedButton("Construction Primary").onClick;
        var existing=g.playable_ids().First().Text;
        Check("construction_external_state_change_fixture",city.Runtime.Execute(actual=>actual.set_paused(existing,!actual.s["projects"][existing].Get("paused",false).Bool))=="");
        oldConfirm.Invoke();yield return null;
        Check("construction_stale_preview_rejected_with_visible_reason",g.s["currency"].Double==initialCash&&g.s["projects"].Count==initialProjects&&ui.ConstructionEvidenceNotice().Contains("城市或报价已变化"),ui.ConstructionEvidenceNotice());
        ui.ConstructionEvidenceExpireNotice();yield return null;
        Check("construction_steps_return_after_notice",ui.ConstructionEvidenceNotice()=="选位置  ›  选方案  ›  开工",ui.ConstructionEvidenceNotice());

        var defer=ui.ConstructionEvidenceNamedButton("Construction Defer");
        Check("construction_defer_control",defer!=null&&defer.IsInteractable());
        defer.onClick.Invoke();yield return null;
        Check("construction_defer_clears_active_draft_without_spend",g.s["draft"].Count==0&&g.s["currency"].Double==initialCash&&g.s["projects"].Count==initialProjects);
        Check("construction_defer_preserves_quote_memory",g.s["quote_bank"].Count>0);
        string reopen=city.Runtime.Execute(actual=>actual.create_tender(V.Arr(tile),baseId));
        Check("construction_reopen_real_tender",reopen==""&&g.s["draft"].Get("offers",V.Arr()).Count==3,reopen);
        var reopenedOffers=g.s["draft"]["offers"].Select(x=>x.ToJson(false,true)).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        Check("construction_reopen_restores_actual_offers",reopenedOffers.SequenceEqual(originalOffers),
            V.Map("before",V.From(originalOffers),"after",V.From(reopenedOffers)));
        ui.OpenArchive("tender");yield return null;Canvas.ForceUpdateCanvases();
        Check("construction_reopened_offer_controls",ui.ConstructionEvidenceLayout()["operator_ids_match"].Bool);
        Check("construction_reopen_only_draft_and_memory_changed",g.s["currency"].Double==initialCash&&g.s["projects"].Count==initialProjects);

        var selectedId=ui.ConstructionEvidenceLayout()["selected_offer"].Text;
        var accepted=g.s["draft"]["offers"].First(x=>x["id"].Text==selectedId);
        var acceptedCost=Draft.offer_cost(g,accepted);
        var primary=ui.ConstructionEvidenceNamedButton("Construction Primary");
        Check("construction_primary_uses_live_cost_and_is_actionable",primary!=null&&primary.IsInteractable()&&ui.ConstructionEvidenceLayout()["cost_matches"].Bool,acceptedCost);
        var stalePrimaryCallback=primary.onClick;
        stalePrimaryCallback.Invoke();yield return null;
        Check("construction_confirm_spends_once_and_builds_one",g.s["projects"].Count==initialProjects+1&&
            Math.Abs(g.s["currency"].Double-(initialCash-acceptedCost["currency"].Double))<.001&&
            g.s["cells"][tile]["building"].Text!=""&&g.s["draft"].Count==0,
            V.Map("cash_before",initialCash,"cash_after",g.s["currency"],"cost",acceptedCost,"project_count",g.s["projects"].Count));
        var committed=GameplayState();
        stalePrimaryCallback.Invoke();yield return null;
        Check("construction_stale_second_callback_no_extra_spend",GameplayState()==committed);
        Check("construction_core_state_valid",g.validate_state(g.s)=="",g.last_error);
        yield return Capture("construction-started.png");
        constructionUxEvidence["screenshots"].Add("construction-started.png");
        constructionUxEvidence["accepted_cost"]=acceptedCost;
        constructionUxEvidence["final_project"]=g.s["cells"][tile]["building"].Text;
        Check("construction_no_native_errors",errors.Count==0,V.From(errors));
        File.WriteAllText(Path.Combine(directory,"construction-ux-scope.json"),constructionUxEvidence.ToJson(true));
    }
}

/// <summary>Read-only geometry and named controls, available only to the explicit QA flag.</summary>
public sealed partial class CityInterface {
    static bool ConstructionEvidenceEnabled=>Environment.GetCommandLineArgs().Contains("--validate-construction-ux");
    public string ConstructionEvidenceNotice()=>ConstructionEvidenceEnabled?_subtitle.text:"";
    public void ConstructionEvidenceExpireNotice(){if(ConstructionEvidenceEnabled)_toastUntil=0;}
    public Button ConstructionEvidenceNamedButton(string name)=>ConstructionEvidenceEnabled?
        GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.name==name):null;
    static Rect ConstructionEvidenceRect(RectTransform target){
        if(target==null)return Rect.zero;var corners=new Vector3[4];target.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(p=>p.x),corners.Min(p=>p.y),corners.Max(p=>p.x),corners.Max(p=>p.y));
    }
    static bool ConstructionEvidenceInside(Rect box)=>box.width>0&&box.height>0&&box.xMin>=-.5&&box.yMin>=-.5&&box.xMax<=Screen.width+.5&&box.yMax<=Screen.height+.5;
    public bool ConstructionEvidenceControlVisible(Button control){
        if(!ConstructionEvidenceEnabled||control==null||!control.gameObject.activeInHierarchy)return false;
        var box=ConstructionEvidenceRect((RectTransform)control.transform);
        var viewport=ConstructionEvidenceRect(_archiveScroll);
        return ConstructionEvidenceInside(box)&&box.yMin>=viewport.yMin-1&&box.yMax<=viewport.yMax+1;
    }
    public void ConstructionEvidenceScrollBottom(){
        if(!ConstructionEvidenceEnabled)return;var scroll=_archiveScroll.GetComponent<ScrollRect>();scroll.StopMovement();scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
    }
    public void ConstructionEvidenceScrollTo(Button control){
        if(!ConstructionEvidenceEnabled||control==null)return;
        var scroll=_archiveScroll.GetComponent<ScrollRect>();
        for(int i=0;i<=20&&!ConstructionEvidenceControlVisible(control);i++){scroll.verticalNormalizedPosition=1-i/20f;Canvas.ForceUpdateCanvases();}
    }
    public bool ConstructionEvidenceDisclosureExpanded(Button button){
        if(!ConstructionEvidenceEnabled||button==null)return false;
        var box=button.transform.parent;return box!=null&&box.GetComponentsInChildren<RectTransform>(true)
            .Any(x=>x.name=="DisclosureBody"&&x.gameObject.activeSelf&&x.childCount>0);
    }
    public V ConstructionEvidenceLayout(){
        if(!ConstructionEvidenceEnabled)return V.Map();Canvas.ForceUpdateCanvases();
        var draft=S["draft"];Rect head=ConstructionEvidenceRect(_decisionHeader),foot=ConstructionEvidenceRect(_decisionFooter),scroll=ConstructionEvidenceRect(_archiveScroll),panel=ConstructionEvidenceRect(_archive);
        var primary=ConstructionEvidenceNamedButton("Construction Primary");var defer=ConstructionEvidenceNamedButton("Construction Defer");
        var choices=GetComponentsInChildren<Button>(true).Where(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith("OperatorChoice ",StringComparison.Ordinal)).ToArray();
        var offerIds=draft.Count>0?draft.Get("offers",V.Arr()).Select(x=>x["id"].Text).ToArray():new string[0];
        var costText=GetComponentsInChildren<Text>(true).FirstOrDefault(t=>t.gameObject.activeInHierarchy&&t.name=="Construction Costs");
        var cost=draft.Count>0&&offerIds.Length>0?Draft.offer_cost(G,draft["offers"].First(x=>x["id"].Text==_offer)):V.Map();
        double cash=cost.Count>0?cost["currency"].Double:0,balance=S["currency"].Double;
        string expectedCost=balance>=cash?"支付后余 "+F(balance-cash,0)+" 通货 · 施工需 "+F(cost["material"].Double,0)+" 材料":
            "还差 "+F(cash-balance,0)+" 通货 · 可保留方案稍后建造";
        bool costMatches=costText!=null&&cost.Count>0&&costText.text==expectedCost&&primary!=null&&
            primary.GetComponentInChildren<Text>()?.text=="开始建造 · "+F(cash,0)+" 通货";
        var selected=draft.Count>0&&offerIds.Length>0?draft["offers"].First(x=>x["id"].Text==_offer):V.Map();
        var operatorName=selected.Count>0?G.c.operators[selected["operator_org"]]["name"].Text:"";
        bool selectedChoiceMatches=choices.Count(b=>b.GetComponentInChildren<Text>()?.text.StartsWith("✓",StringComparison.Ordinal)==true)==1&&
            choices.Any(b=>b.name=="OperatorChoice "+_offer&&b.GetComponentInChildren<Text>()?.text.StartsWith("✓",StringComparison.Ordinal)==true);
        bool identityOperatorMatches=_decisionHeader.GetComponentsInChildren<Text>(true).Any(t=>t.gameObject.activeInHierarchy&&t.text=="运营者："+operatorName);
        var benefit=GetComponentsInChildren<RectTransform>(true).FirstOrDefault(x=>x.name=="Construction Benefits"&&x.gameObject.activeInHierarchy);
        bool benefitCashflowMatches=benefit!=null&&benefit.GetComponentsInChildren<Text>(true)
            .Any(t=>t.gameObject.activeInHierarchy&&t.text==Signed(_proposal["cashflow"].Double)+" 通货/月");
        var primaryText=primary==null?null:primary.GetComponentInChildren<Text>();
        var primaryBox=primary==null?Rect.zero:ConstructionEvidenceRect((RectTransform)primary.transform);
        var deferBox=defer==null?Rect.zero:ConstructionEvidenceRect((RectTransform)defer.transform);
        bool primaryContained=primary!=null&&ConstructionEvidenceInside(primaryBox)&&primaryBox.xMin>=foot.xMin-1&&primaryBox.xMax<=foot.xMax+1&&primaryBox.yMin>=foot.yMin-1&&primaryBox.yMax<=foot.yMax+1;
        bool deferContained=defer!=null&&ConstructionEvidenceInside(deferBox)&&deferBox.xMin>=foot.xMin-1&&deferBox.xMax<=foot.xMax+1&&deferBox.yMin>=foot.yMin-1&&deferBox.yMax<=foot.yMax+1;
        return V.Map("identity",_decisionHeader.GetComponentsInChildren<RectTransform>(true).Any(x=>x.name=="Construction Identity"&&x.gameObject.activeInHierarchy),
            "benefits",benefit!=null,"benefit_cashflow_matches",benefitCashflowMatches,
            "costs",costText!=null,"cost_matches",costMatches,
            "operator_choice_count",choices.Length,"operator_ids_match",choices.Length==offerIds.Length&&offerIds.All(id=>choices.Any(b=>b.name=="OperatorChoice "+id)),
            "selected_choice_matches",selectedChoiceMatches,"identity_operator_matches",identityOperatorMatches,
            "fixed_fit",ConstructionEvidenceInside(panel)&&ConstructionEvidenceInside(head)&&ConstructionEvidenceInside(foot)&&ConstructionEvidenceInside(scroll)&&head.yMin>=scroll.yMax-1&&foot.yMax<=scroll.yMin+1,
            "header_y",head.yMin,"footer_y",foot.yMin,"primary_visible",primaryContained,"defer_visible",deferContained,
            "primary_single_line",primaryText!=null&&primaryText.preferredWidth<=primaryText.rectTransform.rect.width+1,
            "subtitle_persistent",_subtitle!=null&&_subtitle.text=="选位置  ›  选方案  ›  开工",
            "global_nav_hidden",!_pageNav.gameObject.activeInHierarchy,"local_nav_hidden",!_localNav.gameObject.activeInHierarchy,"ordinary_footer_hidden",!_footer.gameObject.activeInHierarchy,
            "cost_text",costText==null?"":costText.text,"selected_offer",_offer);
    }
}
}
