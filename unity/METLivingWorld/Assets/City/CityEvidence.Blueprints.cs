using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MET.City {
public sealed partial class CityEvidence {
    V blueprintUiEvidence=V.Arr();

    static Button BlueprintButton(CityInterface ui,Func<string,bool> predicate,bool interactable=false) =>
        ui.GetComponentsInChildren<Button>(true).FirstOrDefault(button=>
            button.gameObject.activeInHierarchy&&(!interactable||button.IsInteractable())&&
            predicate(button.GetComponentInChildren<Text>()?.text??""));

    static Button BlueprintInventoryButton(CityInterface ui,string bonusId) {
        Func<Button> find=()=>ui.GetComponentsInChildren<Button>(true).FirstOrDefault(button=>
            button.gameObject.activeInHierarchy&&button.IsInteractable()&&button.name=="InventoryChoice "+bonusId);
        var choice=find();if(choice!=null)return choice;
        // The selected blueprint places the inventory behind a local disclosure.
        var disclosure=BlueprintButton(ui,text=>text.StartsWith("其他库存与已部署来源",StringComparison.Ordinal));
        disclosure?.onClick.Invoke();return find();
    }

    static Button BlueprintInstalledLocation(CityInterface ui,string instanceId) {
        Func<Button> find=()=>ui.GetComponentsInChildren<Button>(true).FirstOrDefault(button=>
            button.gameObject.activeInHierarchy&&button.name=="InstalledLocation "+instanceId);
        var location=find();if(location!=null)return location;
        var disclosure=BlueprintButton(ui,text=>text.StartsWith("其他库存与已部署来源",StringComparison.Ordinal));
        disclosure?.onClick.Invoke();return find();
    }

    static string BlueprintUiNumber(double value,int digits=2)=>value.ToString("F"+digits,CultureInfo.InvariantCulture);

    static long BlueprintCharges(Game game,string bid)=>game.s["block"].Get("bonus_wallet",V.Map()).Get(bid,V.Map()).Get("charges",0).Long;

    IEnumerator BlueprintUIJourney() {
        if(Ui5HeadlessOnly)Check("blueprint_ui_offscreen_1920x1080",Ui5OffscreenReady,
            "1920x1080 URP target and screen-space-camera canvas in batchmode");
        else Check("blueprint_ui_fullscreen_1920x1080",Screen.width==1920&&Screen.height==1080&&Screen.fullScreen,
            Screen.width+"x"+Screen.height+" fullscreen="+Screen.fullScreen);
        Check("blueprint_ui_runtime_ready",city.Runtime.Ready&&city.World.Failure=="",city.World.Failure);
        yield return Capture("ui-start-folder.png");
        Check("blueprint_ui_archive_has_modern_dark_surface",city.Interface.GetComponentsInChildren<Image>(true).Any(x=>x.name=="L3 City Side Panel"&&x.sprite==null&&x.color.r<.1f));
        Check("blueprint_ui_original_vector_resource_icons",city.Interface.GetComponentsInChildren<CityHudGlyph>(true).Count()>=20);
        Check("blueprint_ui_no_visible_stationery_props",!city.Interface.GetComponentsInChildren<CityDeskProp>(false).Any());
        var bucketOrder=city.Interface.GetComponentsInChildren<RectTransform>(true).First(x=>x.name=="Bonus Blueprint Bucket");var archiveShield=city.Interface.GetComponentsInChildren<RectTransform>(true).First(x=>x.name=="L3 Archive Shield");
        Check("blueprint_ui_bonus_renders_behind_archive_shield",bucketOrder.GetSiblingIndex()<archiveShield.GetSiblingIndex());

        string reportPath=Environment.GetEnvironmentVariable("MET_ROGUE_INDUSTRY_REPORT");
        string route="industrial";
        if(string.IsNullOrWhiteSpace(reportPath)||!File.Exists(reportPath))
            reportPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../.toolcache/rogue-content/industrial-normal.json"));
        if(string.IsNullOrWhiteSpace(reportPath)||!File.Exists(reportPath)){
            reportPath=Environment.GetEnvironmentVariable("MET_ROGUE_CIVIC_REPORT");route="civic";
        }
        if(string.IsNullOrWhiteSpace(reportPath)||!File.Exists(reportPath)){
            var fallback=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/rogue_content/evidence/content-1/civic-normal.json"));
            if(File.Exists(fallback)&&route=="civic")reportPath=fallback;
        }
        Check("blueprint_ui_earned_route_checkpoint_available",!string.IsNullOrWhiteSpace(reportPath)&&File.Exists(reportPath),reportPath);
        var journey=V.Parse(File.ReadAllText(reportPath));
        Check("blueprint_ui_earned_normal_fresh_city_route",journey.Get("ok",false).Bool&&
            journey.Get("scope","").Text.Contains("normal commands")&&journey.Get("scope","").Text.Contains("fresh city"),journey.Get("scope","missing").Text);
        Check("blueprint_ui_route_has_real_bonus_earning",journey.Get("checks",V.Arr()).Any(x=>x.Text.StartsWith("real_earned_deployed_",StringComparison.Ordinal)),journey.Get("checks",V.Arr()));

        string checkpointKey=route=="civic"?"offered:civic_shared_space":"offered:industrial_byproducts";
        var earnedCity=journey.Get("checkpoints",V.Map()).Get(checkpointKey,V.Map());
        Check("blueprint_ui_checkpoint_is_real_normal_progression",earnedCity.Count>0,checkpointKey);
        RestoreContent(earnedCity,"blueprint_ui_"+route);
        city.Runtime.SetPaused(true);
        Check("blueprint_ui_restored_city_is_valid",city.Runtime.Session.Validate()=="");

        var initialPending=Bonus.pending(city.Runtime.Game);
        Check("blueprint_ui_checkpoint_contains_unclaimed_earned_reward",initialPending.Count>=1,
            "pending="+initialPending.Count);
        var primaryReward=initialPending[0];
        Check("blueprint_ui_first_reward_is_real_earned_copy",primaryReward.Get("kind","").Text=="copy"&&
            primaryReward.Get("source","").Text.StartsWith("annual:",StringComparison.Ordinal),primaryReward.ToJson(false,true));
        Check("blueprint_ui_copy_choices_are_real_building_blueprints",
            primaryReward["choices"].Count(x=>x.Get("scope","").Text=="building")>=2,
            primaryReward["choices"].ToJson(false,true));
        string rewardId=primaryReward["reward_id"].Text;
        var researchChoices=primaryReward["choices"].Where(x=>x.Get("scope","").Text=="building").Take(2).ToArray();

        city.Interface.OpenArchive("themes");yield return null;
        yield return Capture("ui-task-document.png");
        Click("返回地图");yield return null;
        var notices=BlueprintButton(city.Interface,text=>text.StartsWith(Ui5HeadlessOnly?"消息":"通知",StringComparison.Ordinal));
        string beforeNotice=GameplayState();
        if(Ui5HeadlessOnly&&notices==null){
            var noticeControl=city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.name.StartsWith("HUD 消息",StringComparison.Ordinal));
            Check("blueprint_ui_zero_ordinary_notices_collapsed",noticeControl!=null&&!noticeControl.gameObject.activeInHierarchy);
        }else{
            Check("blueprint_ui_notice_foldout_exists",notices!=null);
            notices.onClick.Invoke();yield return null;
            Check("blueprint_ui_notice_open_is_presentation_only",GameplayState()==beforeNotice);
            notices=BlueprintButton(city.Interface,text=>text.StartsWith(Ui5HeadlessOnly?"消息":"通知",StringComparison.Ordinal));notices.onClick.Invoke();yield return null;
            Check("blueprint_ui_notice_close_is_presentation_only",GameplayState()==beforeNotice);
        }
        city.Interface.HideDeskTip();yield return Capture("ui-city-hud.png");

        var hudBonus=city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.name=="Bonus Blueprint Bucket");
        Check("blueprint_ui_hud_bonus_entry_visible",hudBonus!=null,hudBonus==null?"no Bonus proposal button":"");
        hudBonus.onClick.Invoke();yield return null;
        if(Ui5HeadlessOnly){
            Check("blueprint_ui_permit_bucket_opens_owned_inventory",city.Interface.BlueprintDiagnostics.Get("page","").Text=="bonuses"&&
                Bonus.pending(city.Runtime.Game).Count==initialPending.Count);
            city.Interface.OpenBlueprintDesk();yield return null;
            Check("blueprint_ui_explicit_pending_reward_entry",city.Interface.BlueprintDiagnostics.Get("page","").Text=="blueprints");
        }else Check("blueprint_ui_public_desk_entry_opens_proposals",city.Interface.BlueprintDiagnostics!=null&&
            city.Runtime.Game.s["phase"].Text=="play");
        yield return Capture("ui-desk-open.png");
        var firstCandidate=city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.name=="Blueprint Candidate "+rewardId+" "+researchChoices[0]["bonus_id"].Text);
        var secondCandidate=city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.name=="Blueprint Candidate "+rewardId+" "+researchChoices[1]["bonus_id"].Text);
        Check("blueprint_ui_candidate_controls_have_stable_ids",firstCandidate!=null&&secondCandidate!=null,city.Interface.BlueprintDiagnostics.ToJson());
        string firstCandidateName=firstCandidate==null?"":firstCandidate.name;
        string secondCandidateName=secondCandidate==null?"":secondCandidate.name;
        yield return Capture("ui-blueprint-candidates.png");

        var pointer=new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(firstCandidate.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
        EventSystem.current.SetSelectedGameObject(firstCandidate.gameObject);
        ExecuteEvents.Execute(firstCandidate.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.selectHandler);
        firstCandidate.onClick.Invoke();yield return null;
        var selectedFirst=city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.name==firstCandidateName);
        Check("blueprint_ui_first_candidate_fast_select",selectedFirst!=null&&selectedFirst.GetComponent<Outline>()!=null,firstCandidateName);
        secondCandidate=city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.name==secondCandidateName);
        Check("blueprint_ui_second_candidate_still_available",secondCandidate!=null);
        ExecuteEvents.Execute(secondCandidate.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
        EventSystem.current.SetSelectedGameObject(secondCandidate.gameObject);
        secondCandidate.onClick.Invoke();yield return null;
        var selectedSecond=city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.name==secondCandidateName);
        Check("blueprint_ui_fast_switch_moves_single_selection",selectedSecond!=null&&selectedSecond.GetComponent<Outline>()!=null&&
            !city.Interface.GetComponentsInChildren<Button>(true).Any(x=>x.gameObject.activeInHierarchy&&x.name==firstCandidateName&&x.GetComponent<Outline>()!=null));
        Click("取消预选");yield return null;
        Check("blueprint_ui_cancel_clears_candidate_selection",!city.Interface.GetComponentsInChildren<Button>(true).Any(x=>x.gameObject.activeInHierarchy&&x.name.StartsWith("Blueprint Candidate "+rewardId+" ",StringComparison.Ordinal)&&x.GetComponent<Outline>()!=null));

        var previousScale=city.Runtime.DisplaySettings.Get("ui_scale",1).Double;
        var previousText=city.Runtime.DisplaySettings.Get("text_scale",1).Double;
        var previousMotion=city.Runtime.DisplaySettings.Get("reduced_motion",false).Bool;
        city.Interface.OpenArchive("settings");yield return null;
        var scale80=BlueprintButton(city.Interface,t=>t.Contains("界面 80%"));Check("blueprint_ui_scale_control_available",scale80!=null);
        scale80.onClick.Invoke();yield return null;
        Check("blueprint_ui_scale_can_change_independently",Math.Abs(city.Runtime.DisplaySettings["ui_scale"].Double-.8)<.001&&
            Math.Abs(city.Runtime.DisplaySettings["text_scale"].Double-previousText)<.001);
        var text125=BlueprintButton(city.Interface,t=>t.Contains("字号 125%"));Check("blueprint_ui_font_control_available",text125!=null);
        text125.onClick.Invoke();yield return null;
        Check("blueprint_ui_font_scale_independent_of_ui_scale",Math.Abs(city.Runtime.DisplaySettings["text_scale"].Double-1.25)<.001&&
            Math.Abs(city.Runtime.DisplaySettings["ui_scale"].Double-.8)<.001);
        var motion=BlueprintButton(city.Interface,t=>t.StartsWith("减少动态：",StringComparison.Ordinal));Check("blueprint_ui_reduced_motion_control_available",motion!=null);
        if(city.Runtime.DisplaySettings.Get("reduced_motion",false).Bool==previousMotion)motion.onClick.Invoke();
        yield return null;
        Check("blueprint_ui_reduced_motion_changes_display_setting",city.Runtime.DisplaySettings.Get("reduced_motion",false).Bool!=previousMotion);
        var restoreScale=BlueprintButton(city.Interface,t=>t.Contains("界面 "+(int)(previousScale*100)+"%"));Check("blueprint_ui_restore_scale_available",restoreScale!=null);
        if(Math.Abs(city.Runtime.DisplaySettings["ui_scale"].Double-previousScale)>.001)restoreScale.onClick.Invoke();
        yield return null;
        var restoreText=BlueprintButton(city.Interface,t=>t.Contains("字号 "+(int)(previousText*100)+"%"));Check("blueprint_ui_restore_font_available",restoreText!=null);
        if(Math.Abs(city.Runtime.DisplaySettings["text_scale"].Double-previousText)>.001)restoreText.onClick.Invoke();
        yield return null;
        Check("blueprint_ui_display_settings_restored",Math.Abs(city.Runtime.DisplaySettings["ui_scale"].Double-previousScale)<.001&&
            Math.Abs(city.Runtime.DisplaySettings["text_scale"].Double-previousText)<.001);
        city.Runtime.SetDisplaySetting("ui_scale",1.2);city.Interface.ApplyDisplay();if(Ui5HeadlessOnly)RefreshUi5CanvasScale();city.Interface.OpenArchive("settings");yield return null;
        var hudResources=city.Interface.GetComponentsInChildren<RectTransform>(true).First(x=>x.name=="HUD Resources");
        var hudNavigation=city.Interface.GetComponentsInChildren<RectTransform>(true).First(x=>x.name=="HUD Navigation");
        var lastResource=hudResources.GetComponentsInChildren<RectTransform>().First(x=>x.name=="HUD Resource 电力");
        var resourceBox=Ui5ScreenRect(lastResource);var navigationBox=Ui5ScreenRect(hudNavigation);
        Check("blueprint_ui_scale120_resource_navigation_do_not_overlap",resourceBox.xMax<=navigationBox.xMin+1,
            resourceBox.xMax+" <= "+navigationBox.xMin);
        var archiveBounds=city.Interface.GetComponentsInChildren<RectTransform>(true).First(x=>x.name=="L3 City Side Panel");var archiveBox=Ui5ScreenRect(archiveBounds);
        Check("blueprint_ui_scale120_archive_inside_viewport",Ui5Inside(archiveBox),Ui5Box(archiveBox));
        yield return Capture("ui-scale120.png");
        city.Runtime.SetDisplaySetting("ui_scale",previousScale);city.Interface.ApplyDisplay();city.Interface.OpenArchive("settings");yield return null;
        Check("blueprint_ui_settings_do_not_change_city_state",GameplayState()==beforeNotice);
        city.Runtime.SetDisplaySetting("reduced_motion",false);city.Interface.ApplyDisplay();
        Click("返回地图");yield return null;

        city.Interface.OpenBlueprintDesk();yield return null;
        var nextReward=Bonus.pending(city.Runtime.Game).FirstOrDefault(x=>x.Get("reward_id","").Text==rewardId);
        Check("blueprint_ui_real_copy_reward_remains_pending_until_confirmation",nextReward!=null&&nextReward["choices"].Count>0);
        string copyRewardId=nextReward==null?"":nextReward["reward_id"].Text;
        var inventoryCandidate=nextReward==null?V.Map():nextReward["choices"].FirstOrDefault(x=>x.Get("bonus_id","").Text=="P09"&&
            x.Get("scope","").Text=="building"&&x.Get("deployable_now",false).Bool);
        Check("blueprint_ui_copy_choice_P09_is_earned_and_installable",inventoryCandidate!=null, nextReward.ToJson(false,true));
        string bid=inventoryCandidate["bonus_id"].Text;
        Check("blueprint_ui_copy_source_is_progression",nextReward.Get("source","").Text.StartsWith("annual:",StringComparison.Ordinal)||
            nextReward.Get("source","").Text.StartsWith("population:",StringComparison.Ordinal)||
            nextReward.Get("source","").Text.StartsWith("theme:",StringComparison.Ordinal),nextReward.Get("source","").Text);
        long copiesBeforeClaim=BlueprintCharges(city.Runtime.Game,bid);
        city.Interface.OpenBlueprintDesk();yield return null;
        var copyButton=city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&
            x.name=="Blueprint Candidate "+copyRewardId+" "+bid);
        Check("blueprint_ui_copy_candidate_control",copyButton!=null,bid);
        copyButton.onClick.Invoke();yield return null;
        var confirmCopy=Control("确认领取");Check("blueprint_ui_copy_claim_button",confirmCopy!=null&&confirmCopy.IsInteractable());
        var copyConfirmEvent=confirmCopy.onClick;copyConfirmEvent.Invoke();
        Check("blueprint_ui_claim_adds_one_uninstalled_copy",BlueprintCharges(city.Runtime.Game,bid)==copiesBeforeClaim+1,
            bid+" charges "+copiesBeforeClaim+"→"+BlueprintCharges(city.Runtime.Game,bid));
        copyConfirmEvent.Invoke();yield return null;
        yield return Capture("ui-blueprint-approval.png");
        Check("blueprint_ui_duplicate_copy_claim_does_not_duplicate",BlueprintCharges(city.Runtime.Game,bid)==copiesBeforeClaim+1);
        for(int frame=0;frame<100&&city.Interface.BlueprintDiagnostics["busy"].Bool&&!city.Interface.GetComponentsInChildren<RectTransform>().Any(x=>x.name=="Receipt Transfer"&&x.gameObject.activeInHierarchy);frame++)yield return null;
        Check("blueprint_ui_receipt_visibly_transfers_to_bonus",city.Interface.GetComponentsInChildren<RectTransform>().Any(x=>x.name=="Receipt Transfer"&&x.gameObject.activeInHierarchy));
        yield return Capture("ui-blueprint-roll.png");
        for(int i=0;i<100&&city.Interface.BlueprintDiagnostics["busy"].Bool;i++)yield return null;

        city.Interface.OpenBlueprintDesk();yield return null;
        var blockBid=Rules.ids(city.Runtime.Game.s["block"]["bonus_wallet"]).Select(x=>x.Text).FirstOrDefault(x=>BlueprintCharges(city.Runtime.Game,x)>0&&Bonus.card(city.Runtime.Game,x)["scope"].Text=="block"&&BlueprintPreview.For(city.Runtime.Game,x,"block",city.Runtime.Game.s["block"]["id"].Text).Get("ok",false).Bool);
        if(blockBid!=null){string readonlyBefore=GameplayState();BlueprintInventoryButton(city.Interface,blockBid).onClick.Invoke();yield return null;Check("blueprint_ui_block_preview_first_selection",city.Interface.BlueprintDiagnostics["preview"].Get("ok",false).Bool);BlueprintInventoryButton(city.Interface,blockBid).onClick.Invoke();yield return null;Check("blueprint_ui_block_preview_repeat_selection_cache_regression",city.Interface.BlueprintDiagnostics["preview"].Get("ok",false).Bool&&GameplayState()==readonlyBefore);Click("取消安装预览");yield return null;BlueprintInventoryButton(city.Interface,blockBid).onClick.Invoke();yield return null;Check("blueprint_ui_block_preview_cancel_reselect_cache_regression",city.Interface.BlueprintDiagnostics["preview"].Get("ok",false).Bool&&GameplayState()==readonlyBefore);}
        city.Interface.OpenArchive("bonuses");yield return null;
        var inventorySelect=BlueprintInventoryButton(city.Interface,bid);Check("blueprint_ui_inventory_selects_real_copy",inventorySelect!=null);
        inventorySelect.onClick.Invoke();yield return null;
        var hostChoice=BlueprintButton(city.Interface,t=>t.StartsWith("✓ ",StringComparison.Ordinal)&&!t.Contains(" · 可安装")&&!t.Contains(" · 可替换"),true);
        Check("blueprint_ui_compatible_building_choice_available",hostChoice!=null,bid);
        hostChoice.onClick.Invoke();yield return null;
        string host=city.Interface.BlueprintDiagnostics.Get("host","").Text;
        Check("blueprint_ui_host_matches_selected_building",host!=""&&city.Runtime.Game.s["projects"].Has(host),host);
        var slotChoice=BlueprintButton(city.Interface,t=>t.Contains(" · 可安装"),true);
        Check("blueprint_ui_compatible_empty_slot_available",slotChoice!=null,host);
        slotChoice.onClick.Invoke();yield return null;
        string slot=city.Interface.BlueprintDiagnostics.Get("slot","").Text;
        Check("blueprint_ui_slot_scope_selected",slot!=""&&city.Interface.BlueprintDiagnostics.Get("scope","").Text=="building",slot);

        string beforePreview=GameplayState();
        var diagnostics=city.Interface.BlueprintDiagnostics;
        var preview=diagnostics.Get("preview",V.Map());
        var authority=BlueprintPreview.For(city.Runtime.Game,bid,"building",host,slot);
        Check("blueprint_ui_preview_is_valid_pure_forecast",preview.Get("ok",false).Bool&&authority.Get("ok",false).Bool&&GameplayState()==beforePreview);
        Check("blueprint_ui_displayed_preview_matches_authoritative_calculation",
            preview["before_stats"].ToJson(false,true)==authority["before_stats"].ToJson(false,true)&&
            preview["after_stats"].ToJson(false,true)==authority["after_stats"].ToJson(false,true)&&
            preview["stat_rows"].ToJson(false,true)==authority["stat_rows"].ToJson(false,true)&&
            preview["rule_trace"].ToJson(false,true)==authority["rule_trace"].ToJson(false,true)&&
            preview["same_step"].ToJson(false,true)==authority["same_step"].ToJson(false,true),"forecast fields compared; volatile planning token omitted");
        var sameStep=authority["same_step"];
        string cashText=BlueprintUiNumber(sameStep["cashflow"]["before"].Double)+" → "+BlueprintUiNumber(sameStep["cashflow"]["after"].Double)+" /月";
        Check("blueprint_ui_preview_value_visible",city.Interface.GetComponentsInChildren<Text>(true).Any(t=>t.gameObject.activeInHierarchy&&t.text==cashText),cashText);
        var previewConfirm=Control("安装 · 消耗1份");Check("blueprint_ui_preview_confirmation_scrolled_into_view",previewConfirm!=null);var confirmBox=Ui5ScreenRect((RectTransform)previewConfirm.transform);Check("blueprint_ui_confirmation_visible_inside_screen",confirmBox.yMin>=0&&confirmBox.yMax<=Ui5ViewportHeight,Ui5Box(confirmBox));
        yield return Capture("ui-blueprint-preview.png");

        var ruleTip=city.Interface.GetComponentsInChildren<CityDeskTooltip>(true).FirstOrDefault(t=>t.gameObject.activeInHierarchy&&
            t.transform.IsChildOf(city.Interface.transform)&&t.Owner==city.Interface&&t.Heading.Contains("真实乘区")&&t.Body!=null);
        Check("blueprint_ui_tooltip_owner_and_live_body",ruleTip!=null);
        if(!Ui5HeadlessOnly){
        ExecuteEvents.Execute(ruleTip.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);yield return null;
        Check("blueprint_ui_hover_waits_two_real_seconds",!city.Interface.BlueprintDiagnostics.Get("tooltip",false).Bool&&city.Interface.BlueprintDiagnostics.Get("tooltip_pending",false).Bool);
        yield return new WaitForSecondsRealtime(2.05f);
        Check("blueprint_ui_hover_opens_explanation",city.Interface.BlueprintDiagnostics.Get("tooltip",false).Bool);
        var keeper=city.Interface.GetComponentsInChildren<CityDeskTipFocus>(true).FirstOrDefault(x=>x.gameObject.name=="Desk Explanation");
        Check("blueprint_ui_tooltip_has_hover_keeper",keeper!=null);
        ExecuteEvents.Execute(ruleTip.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
        ExecuteEvents.Execute(keeper.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
        yield return new WaitForSecondsRealtime(.25f);
        Check("blueprint_ui_tooltip_survives_pointer_transfer",city.Interface.BlueprintDiagnostics.Get("tooltip",false).Bool);
        city.Interface.HideDeskTip();
        EventSystem.current.SetSelectedGameObject(ruleTip.gameObject);yield return new WaitForSecondsRealtime(2.05f);
        var closeInTip=keeper.GetComponentsInChildren<Button>().First();EventSystem.current.SetSelectedGameObject(closeInTip.gameObject);city.Interface.QueueHideDeskTip();yield return new WaitForSecondsRealtime(.25f);
        Check("blueprint_ui_unpinned_focused_tooltip_survives_old_timer",city.Interface.BlueprintDiagnostics.Get("tooltip",false).Bool&&EventSystem.current.currentSelectedGameObject==closeInTip.gameObject);
        city.Interface.HideDeskTip();city.Interface.ShowDeskTip(ruleTip,true);yield return null;
        var tipFocus=EventSystem.current.currentSelectedGameObject;
        Check("blueprint_ui_explicit_tooltip_focuses_close",tipFocus!=null&&tipFocus.GetComponentInChildren<Text>()?.text=="×");
        city.Interface.QueueHideDeskTip();yield return new WaitForSecondsRealtime(.25f);
        Check("blueprint_ui_focused_tooltip_survives_stale_timer",city.Interface.BlueprintDiagnostics.Get("tooltip",false).Bool&&EventSystem.current.currentSelectedGameObject==tipFocus);
        city.Interface.HideDeskTip();yield return null;
        Check("blueprint_ui_tooltip_close_restores_valid_trigger",EventSystem.current.currentSelectedGameObject==ruleTip.gameObject&&ruleTip.gameObject.activeInHierarchy);
        }

        var cancel=Control("取消安装预览");Check("blueprint_ui_cancel_install_button",cancel!=null&&cancel.IsInteractable());cancel.onClick.Invoke();yield return null;
        Check("blueprint_ui_cancel_install_preserves_all_gameplay_state",GameplayState()==beforePreview);
        Check("blueprint_ui_cancel_clears_preview_context",city.Interface.BlueprintDiagnostics.Get("preview",V.Map()).Count==0);

        city.Interface.OpenBlueprintDesk();yield return null;
        inventorySelect=BlueprintInventoryButton(city.Interface,bid);Check("blueprint_ui_reopen_inventory_after_cancel",inventorySelect!=null);inventorySelect.onClick.Invoke();yield return null;
        hostChoice=BlueprintButton(city.Interface,t=>t.StartsWith("✓ ",StringComparison.Ordinal)&&!t.Contains(" · 可安装")&&!t.Contains(" · 可替换"),true);
        Check("blueprint_ui_reselect_host_after_cancel",hostChoice!=null);hostChoice.onClick.Invoke();yield return null;
        slotChoice=BlueprintButton(city.Interface,t=>t.Contains(" · 可安装"),true);
        Check("blueprint_ui_reselect_empty_slot_after_cancel",slotChoice!=null);slotChoice.onClick.Invoke();yield return null;
        diagnostics=city.Interface.BlueprintDiagnostics;preview=diagnostics.Get("preview",V.Map());
        var install=Control("安装 · 消耗1份");Check("blueprint_ui_install_confirmation_control",install!=null&&install.IsInteractable());
        long copiesBeforeInstall=BlueprintCharges(city.Runtime.Game,bid);
        var installEvent=install.onClick;installEvent.Invoke();
        var instanceId=city.Runtime.Game.s["projects"][host]["slots"].Get(slot,"").Text;
        Check("blueprint_ui_install_uses_exactly_one_copy",BlueprintCharges(city.Runtime.Game,bid)==copiesBeforeInstall-1);
        Check("blueprint_ui_install_matches_preview_stats",instanceId!=""&&
            city.Runtime.Game.compiled["stats"][host].ToJson(false,true)==preview["after_stats"].ToJson(false,true));
        Check("blueprint_ui_install_is_active_separate_instance",city.Runtime.Game.s["bonus_instances"].Has(instanceId)&&
            city.Runtime.Game.s["bonus_instances"][instanceId].Get("active",false).Bool&&
            city.Runtime.Game.s["bonus_instances"][instanceId]["bonus_id"].Text==bid);
        string afterInstall=GameplayState();installEvent.Invoke();yield return null;
        Check("blueprint_ui_repeated_install_confirmation_is_idempotent",GameplayState()==afterInstall&&
            city.Runtime.Game.s["projects"][host]["slots"][slot].Text==instanceId);

        string roundTrip=Path.Combine(directory,"blueprint-ui-earned-roundtrip.json");
        Check("blueprint_ui_export_saved_city",city.Runtime.Export(roundTrip)=="");
        long savedCharges=BlueprintCharges(city.Runtime.Game,bid);string savedInstance=instanceId;
        Check("blueprint_ui_load_saved_city",city.Runtime.Load(roundTrip)=="");yield return null;
        Check("blueprint_ui_load_preserves_wallet_and_installed_instance",BlueprintCharges(city.Runtime.Game,bid)==savedCharges&&
            city.Runtime.Game.s["bonus_instances"].Has(savedInstance)&&city.Runtime.Game.s["projects"][host]["slots"][slot].Text==savedInstance);
        Check("blueprint_ui_loaded_city_valid",city.Runtime.Session.Validate()=="");
        city.Interface.SelectBuilding(host);city.Interface.OpenArchive("building");yield return null;
        yield return Capture("ui-building-pinned.png");
        Check("blueprint_ui_building_detail_marks_installed_instance",
            city.Interface.GetComponentsInChildren<Text>(true).Any(t=>t.gameObject.activeInHierarchy&&t.name=="InstalledSlot "+savedInstance&&t.text.Contains("已部署")));
        city.Interface.OpenArchive("bonuses");yield return null;
        var locate=BlueprintInstalledLocation(city.Interface,savedInstance);
        Check("blueprint_ui_inventory_separates_copy_from_deployed_location",locate!=null&&BlueprintCharges(city.Runtime.Game,bid)==savedCharges&&
            city.Runtime.Game.s["bonus_instances"][savedInstance].Get("active",false).Bool);
        locate.onClick.Invoke();yield return null;
        Check("blueprint_ui_locate_returns_to_host_building",city.Interface.SelectedBuilding==host&&
            city.Interface.BlueprintDiagnostics.Get("host","").Text==host);
        Check("blueprint_ui_pause_building_for_slot_refresh",city.Runtime.Execute(g=>g.set_paused(host,true))=="");yield return null;
        long copiesBeforeClear=BlueprintCharges(city.Runtime.Game,bid);int contextBeforeClear=city.Interface.ContextRebuildCount;
        Check("blueprint_ui_clear_paused_slot_authoritatively",city.Runtime.Execute(g=>g.clear_slot(host,slot))=="");yield return null;
        var siteContext=city.Interface.GetComponentsInChildren<RectTransform>(true).First(x=>x.name=="HUD Site Context");
        Check("blueprint_ui_paused_slot_context_refreshes",city.Interface.ContextRebuildCount>contextBeforeClear&&siteContext.GetComponentsInChildren<Text>(true).Any(x=>x.text==city.Runtime.Game.slot_label(host,slot)+" · 未安装 · ＋"));
        Check("blueprint_ui_uninstall_does_not_refund_copy",BlueprintCharges(city.Runtime.Game,bid)==copiesBeforeClear&&!city.Runtime.Game.s["bonus_instances"][savedInstance].Get("active",false).Bool);
        Check("blueprint_ui_restore_installed_save_after_clear_test",city.Runtime.Load(roundTrip)=="");yield return null;
        city.Interface.OpenArchive("building");yield return null;Click("返回地图");city.Interface.SelectBuilding(host);yield return null;
        yield return Capture("ui-hud-building.png");
        Check("blueprint_ui_selected_building_context_at_lower_left",city.Interface.GetComponentsInChildren<RectTransform>(false).Any(x=>x.name=="HUD Site Context"&&x.anchorMin.x==0&&x.anchorMin.y==0));
        city.Interface.OpenArchive("building");yield return null;
        var study=city.Interface.ShowIllustrationStudy();yield return null;
        Check("blueprint_ui_twenty_purpose_illustrations",study.GetComponentsInChildren<CityBlueprintArt>().Select(x=>x.Style).Distinct().Count()==20);
        yield return Capture("ui-illustration-coverage.png");Destroy(study);yield return null;

        blueprintUiEvidence.Add(V.Map("route",route,"route_scope",journey["scope"],"checkpoint",checkpointKey,
            "reward_id",rewardId,"candidate_choices",V.Arr(researchChoices.Select(x=>x["bonus_id"]).ToArray()),"copy_reward_id",copyRewardId,"copy_choice",bid,
            "host",host,"slot",slot,"instance_id",savedInstance,"copy_count_after_install",savedCharges,
            "preview_matches_authority",true,"saved_city_roundtrip",true,
            "screenshots",V.Arr("ui-city-hud.png","ui-task-document.png","ui-blueprint-candidates.png","ui-blueprint-preview.png","ui-building-pinned.png")));
    }
}
}
