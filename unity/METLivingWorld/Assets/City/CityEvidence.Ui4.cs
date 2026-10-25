using System;
using System.Collections;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MET.City {
/// <summary>
/// Opt-in diagnostic fixture, added only to the separately named QA build.
/// It never sends OS input. The ordinary presentation/input implementation is unchanged.
/// </summary>
public sealed partial class CityEvidence {
    static bool Ui4RevisionOnly => Environment.GetCommandLineArgs().Contains("--validate-ui4-revision");
    V ui4RevisionEvidence=V.Map();

    IEnumerator Ui4RevisionPrelude() {
        ui4RevisionEvidence=V.Map(
            "build_scope","Delivered UI4.2 Player launched with an opt-in diagnostic flag and an isolated QA save. Same application bytes as the normal UI4.2 package.",
            "input_scope","Programmatic Unity callbacks/events only; no OS mouse or keyboard events.",
            "map_scope","Evidence-only helper sets the inspection tile/mature flag and calls the existing lock/render methods. This is rendering/data evidence, not hover/click input acceptance.",
            "screenshots",V.Arr());
        string expected=Environment.GetEnvironmentVariable("MET_UI4_EXPECTED_VERSION");
        if(string.IsNullOrWhiteSpace(expected))expected="0.7.0-ui.4.2-local";
        Check("ui4_revision_application_version",Application.version==expected,
            V.Map("expected",expected,"actual",Application.version));
        // Normal app startup may take a few frames to acquire focus. This waits only;
        // it neither activates an application nor changes the production focus guard.
        double until=Time.realtimeSinceStartupAsDouble+5;
        while(!Application.isFocused&&Time.realtimeSinceStartupAsDouble<until)yield return null;
        Check("ui4_revision_player_is_focused",Application.isFocused,
            "Tooltip and map production Application.isFocused guards remain enabled.");
    }

    IEnumerator Ui4RevisionJourney() {
        var ui=city.Interface;var game=city.Runtime.Game;
        city.Runtime.SetPaused(true);
        ui.Ui4EvidenceCloseArchive();ui.HideDeskTip();
        yield return null;
        string before=GameplayState();
        string host=game.playable_ids().Select(x=>x.Text)
            .Where(id=>game.s["projects"].Has(id)&&game.slot_ids(id).Count>0)
            .OrderByDescending(id=>game.s["projects"][id]["slots"].Values.Count(x=>x.Text!=""))
            .ThenByDescending(id=>game.slot_ids(id).Count).FirstOrDefault();
        Check("ui4_revision_real_slotted_building_exists",!string.IsNullOrEmpty(host),host);
        ui.SetLayer("terrain");ui.SelectBuilding(host);
        city.World.FocusSelected();
        yield return new WaitForSecondsRealtime(.6f);
        Canvas.ForceUpdateCanvases();
        var context=ui.Ui4EvidenceContext();
        Check("ui4_revision_context_uses_real_selected_building",context["active"].Bool&&context["host"].Text==host,context);
        Check("ui4_revision_context_is_lower_left_and_inside_screen",context["lower_left"].Bool&&context["inside_screen"].Bool,context);
        Check("ui4_revision_context_has_actual_model_camera_image",context["actual_model_images"].Long>0,context);
        Check("ui4_revision_context_groups_actual_slot_types",context["slot_category_count"].Long==context["expected_slot_category_count"].Long&&context["slot_category_count"].Long>0,context);
        yield return Capture("ui4-building-context.png");
        ui4RevisionEvidence["building_context"]=context;
        ui4RevisionEvidence["screenshots"].Add("ui4-building-context.png");

        ui.OpenArchive("themes");yield return null;Canvas.ForceUpdateCanvases();
        var tasks=ui.Ui4EvidenceTasks();
        Check("ui4_revision_task_main_categories",tasks["main_categories"].Count==5,tasks);
        Check("ui4_revision_task_sidebar_keeps_map_visible",tasks["inside_screen"].Bool&&tasks["screen_width"].Double<Screen.width*.60,tasks);
        Check("ui4_revision_task_uses_saved_milestone_record",tasks["saved_milestone_ids"].Count==game.s["block"].Get("theme",V.Map()).Get("milestone_ids",V.Arr()).Count,tasks);
        Check("ui4_revision_completed_task_records_initially_folded",!tasks["completed"].Bool||tasks["completion_disclosure_folded"].Bool,tasks);
        yield return Capture("ui4-task-sidebar.png");
        ui4RevisionEvidence["tasks"]=tasks;ui4RevisionEvidence["screenshots"].Add("ui4-task-sidebar.png");
        ui.Ui4EvidenceCloseArchive();ui.HideDeskTip();yield return null;

        var resource=ui.GetComponentsInChildren<CityDeskTooltip>(false)
            .FirstOrDefault(x=>x.name=="HUD Resource 粮食"&&x.Owner==ui);
        Check("ui4_revision_real_food_resource_tooltip_exists",resource!=null);
        // These are Unity handler calls, not desktop pointer movement.
        ExecuteEvents.Execute(resource.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
        if(EventSystem.current.currentSelectedGameObject==resource.gameObject)EventSystem.current.SetSelectedGameObject(null);
        double entered=Time.realtimeSinceStartupAsDouble;
        ExecuteEvents.Execute(resource.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
        yield return null;
        Check("ui4_revision_tooltip_is_pending_before_two_seconds",ui.BlueprintDiagnostics["tooltip_pending"].Bool&&!ui.BlueprintDiagnostics["tooltip"].Bool);
        while(Time.realtimeSinceStartupAsDouble-entered<1.90)yield return null;
        Check("ui4_revision_tooltip_not_open_at_1_9_seconds",!ui.BlueprintDiagnostics["tooltip"].Bool);
        while(Time.realtimeSinceStartupAsDouble-entered<2.08)yield return null;
        var tip=ui.Ui4EvidenceTooltip(resource);
        Check("ui4_revision_tooltip_opens_after_two_real_seconds",tip["open"].Bool,
            V.Map("elapsed_real_seconds",Time.realtimeSinceStartupAsDouble-entered,"geometry",tip));
        Check("ui4_revision_tooltip_avoids_full_resource_anchor",!tip["anchor_overlap"].Bool,tip);
        Check("ui4_revision_tooltip_inside_screen",tip["inside_screen"].Bool,tip);
        Check("ui4_revision_tooltip_avoids_resource_strip",!tip["resource_strip_overlap"].Bool,tip);
        Check("ui4_revision_tooltip_avoids_priority_reminders",!tip["priority_reminders_overlap"].Bool,tip);
        Check("ui4_revision_tooltip_avoids_notifications_button",!tip["notification_button_overlap"].Bool,tip);
        yield return Capture("ui4-tooltip-two-second.png");
        ui4RevisionEvidence["tooltip"]=tip;ui4RevisionEvidence["screenshots"].Add("ui4-tooltip-two-second.png");
        var close=ui.Ui4EvidenceTooltipClose();
        Check("ui4_revision_tooltip_close_control_exists",close!=null);
        ExecuteEvents.Execute(resource.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
        // A stale hide timer must not close an explanation after focus enters it.
        ui.QueueHideDeskTip();EventSystem.current.SetSelectedGameObject(close.gameObject);
        yield return new WaitForSecondsRealtime(.25f);
        Check("ui4_revision_tooltip_focused_after_old_timer",ui.BlueprintDiagnostics["tooltip"].Bool&&EventSystem.current.currentSelectedGameObject==close.gameObject);
        Check("ui4_revision_escape_handler_consumes_open_tooltip",ui.Ui4EvidenceEscapeTooltip());
        Check("ui4_revision_escape_handler_restores_trigger_focus",EventSystem.current.currentSelectedGameObject==resource.gameObject&&resource.gameObject.activeInHierarchy);
        ui4RevisionEvidence["tooltip_escape_scope"]="Called the existing Escape handler directly; actual desktop Escape key not tested.";
        EventSystem.current.SetSelectedGameObject(null);
        ExecuteEvents.Execute(resource.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);

        string tile="",layer="";V reading=V.Map();
        foreach(var candidateLayer in new[]{"pollution","heat","energy","stock","service","employment"}) {
            ui.SetLayer(candidateLayer);
            var candidate=game.s["cells"].Keys.Select(id=>city.World.InspectionForTile(id))
                .Where(x=>x.Get("available",false).Bool&&x.Get("neighbors",V.Arr()).Count>0&&
                    x.Get("rows",V.Arr()).Any(r=>Ui4AttributableSource(r)))
                .OrderByDescending(x=>Math.Abs(x.Get("value",0).Double)>1e-9)
                .ThenByDescending(x=>x.Get("neighbors",V.Arr()).Count)
                .ThenByDescending(x=>Math.Abs(x.Get("value",0).Double)).FirstOrDefault();
            if(candidate==null)continue;
            tile=candidate["tile"].Text;layer=candidateLayer;reading=candidate;break;
        }
        Check("ui4_revision_actual_attributed_map_reading_available",tile!="",reading);
        city.World.FocusInspectionSource(tile);yield return new WaitForSecondsRealtime(.6f);
        var frameKeeper=ui.gameObject.AddComponent<CityUi4EvidenceMapFrame>();frameKeeper.Owner=ui;frameKeeper.Tile=tile;
        ui.Ui4EvidenceRenderInspection(tile);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
        var map=ui.Ui4EvidenceInspection();
        Check("ui4_revision_map_source_panel_locked",map["locked_tile"].Text==tile&&map["panel_active"].Bool,map);
        Check("ui4_revision_map_center_and_neighbor_numbers_rendered",map["visible_marker_count"].Long>=2&&map["center_active"].Bool,map);
        Check("ui4_revision_map_markers_do_not_capture_pointer",map["marker_raycast_targets"].Long==0,map);
        Check("ui4_revision_map_source_rows_match_actual_data",map["source_row_count"].Long==reading["rows"].Count,map);
        int expectedSources=reading["rows"].Where(Ui4AttributableSource).Select(x=>x["source_tile"].Text).Distinct().Count();
        int drawnSources=city.World.GetComponentsInChildren<LineRenderer>(true).Count(x=>x.name.StartsWith("InspectionSource_",StringComparison.Ordinal)&&x.enabled);
        Check("ui4_revision_map_actual_source_tiles_highlighted",expectedSources>0&&drawnSources==expectedSources,V.Map("expected",expectedSources,"drawn",drawnSources));
        yield return Capture("ui4-map-values-and-sources.png");
        ui4RevisionEvidence["map"]=V.Map("layer",layer,"tile",tile,"actual_reading",reading,"render",map,"highlighted_source_tiles",expectedSources,
            "hover_or_click_tested",false,"helper_bypass","Enabled map numeric labels and invoked existing LockMapInspection/RefreshInspectionMarkers in LateUpdate; no OS input or eligibility/state mutation.");
        ui4RevisionEvidence["screenshots"].Add("ui4-map-values-and-sources.png");
        string firstSource=reading["rows"].Where(Ui4AttributableSource).Select(row=>row["source_tile"].Text).FirstOrDefault(sourceTile=>sourceTile!=tile)??reading["rows"].First(Ui4AttributableSource)["source_tile"].Text;
        Check("ui4_revision_map_locates_a_different_real_source",firstSource!=tile,V.Map("inspected_tile",tile,"source_tile",firstSource));
        int cameraRevision=city.World.CameraController.Revision;
        Check("ui4_revision_map_source_location_callback",city.World.FocusInspectionSource(firstSource)&&city.World.CameraController.Revision>cameraRevision,firstSource);
        yield return new WaitForSecondsRealtime(.6f);
        yield return Capture("ui4-map-source-location.png");
        ui4RevisionEvidence["screenshots"].Add("ui4-map-source-location.png");
        frameKeeper.enabled=false;Destroy(frameKeeper);ui.DismissMapInspection();
        Check("ui4_revision_render_checks_preserve_all_gameplay_state",GameplayState()==before);
        Check("ui4_revision_no_logged_native_errors",errors.Count==0,V.From(errors));
        ui4RevisionEvidence["all_render_checks_preserve_gameplay_state"]=true;
        File.WriteAllText(Path.Combine(directory,"ui4-render-scope.json"),ui4RevisionEvidence.ToJson(true));
    }

    static bool Ui4AttributableSource(V row)=>row.Get("source_tile","").Text!=""&&
        !new[]{"group","denominator","baseline","proposal"}.Contains(row.Get("kind","").Text);
}

/// <summary>Only exists in an explicit diagnostic run; repairs no production state.</summary>
public sealed class CityUi4EvidenceMapFrame : MonoBehaviour {
    public CityInterface Owner;public string Tile;
    void LateUpdate(){if(Owner!=null)Owner.Ui4EvidenceRenderInspection(Tile);}
}

public sealed partial class CityInterface {
    static bool Ui4EvidenceEnabled=>Environment.GetCommandLineArgs().Contains("--validate-ui4-revision");
    public void Ui4EvidenceCloseArchive(){if(Ui4EvidenceEnabled&&_archiveOpen)CloseArchive();}
    public bool Ui4EvidenceEscapeTooltip()=>Ui4EvidenceEnabled&&HandleDeskTipEscape();
    public Button Ui4EvidenceTooltipClose()=>Ui4EvidenceEnabled?_deskTipClose:null;

    static Rect Ui4ScreenRect(RectTransform target){
        if(target==null)return Rect.zero;var corners=new Vector3[4];target.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(p=>p.x),corners.Min(p=>p.y),corners.Max(p=>p.x),corners.Max(p=>p.y));
    }
    static bool Ui4Inside(Rect r)=>r.xMin>=-.5f&&r.yMin>=-.5f&&r.xMax<=Screen.width+.5f&&r.yMax<=Screen.height+.5f;
    static V Ui4Box(Rect r)=>V.Arr(r.xMin,r.yMin,r.width,r.height);
    public V Ui4EvidenceContext(){
        if(!Ui4EvidenceEnabled)return V.Map();
        var rect=Ui4ScreenRect(_context);var categories=_context.GetComponentsInChildren<RectTransform>(false).Where(x=>x.name.StartsWith("Slot Category ",StringComparison.Ordinal)).ToArray();
        int expected=SelectedBuilding!=""?G.slot_ids(SelectedBuilding).Select(x=>G.slot_type(SelectedBuilding,x.Text)).Distinct().Count():0;
        return V.Map("active",_context.gameObject.activeInHierarchy,"host",SelectedBuilding,"tile",SelectedTile,"screen_rect",Ui4Box(rect),"inside_screen",Ui4Inside(rect),
            "lower_left",_context.anchorMin==Vector2.zero,"actual_model_images",_context.GetComponentsInChildren<RawImage>(false).Count(x=>x.name=="Committed Model Camera Image"&&x.texture!=null),
            "slot_category_count",categories.Length,"expected_slot_category_count",expected,"slot_categories",V.From(categories.Select(x=>x.name).ToArray()));
    }
    public V Ui4EvidenceTasks(){
        if(!Ui4EvidenceEnabled)return V.Map();var rect=Ui4ScreenRect(_archive);var theme=S["block"].Get("theme",V.Map());
        var recordButton=_body.GetComponentsInChildren<Button>(false).FirstOrDefault(x=>(x.GetComponentInChildren<Text>()?.text??"").StartsWith("完成记录",StringComparison.Ordinal));
        bool folded=recordButton!=null&&!recordButton.transform.parent.Find("DisclosureBody").gameObject.activeSelf;
        return V.Map("page",_page,"inside_screen",Ui4Inside(rect),"screen_rect",Ui4Box(rect),"screen_width",rect.width,
            "main_categories",V.From(_archiveTabs.Keys.ToArray()),"completed",theme.Get("completed",false),"saved_milestone_ids",theme.Get("milestone_ids",V.Arr()).Duplicate(true),
            "completion_disclosure_folded",folded,"live_goal_rows",_body.GetComponentsInChildren<RectTransform>(false).Count(x=>x.name=="Commission goal"));
    }
    public V Ui4EvidenceTooltip(CityDeskTooltip source){
        if(!Ui4EvidenceEnabled)return V.Map();var rect=Ui4ScreenRect(_deskTip);var anchor=Ui4ScreenRect(source.transform as RectTransform);
        var resources=_root.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(x=>x.name=="HUD Resources");
        var reminders=_root.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(x=>x.name=="Priority Reminders");
        var notification=_planningNoticeButton!=null?_planningNoticeButton.transform as RectTransform:null;
        return V.Map("open",DeskTipOpen,"screen_rect",Ui4Box(rect),"anchor_rect",Ui4Box(anchor),"anchor_overlap",rect.Overlaps(anchor),"inside_screen",Ui4Inside(rect),
            "resource_strip_overlap",resources!=null&&rect.Overlaps(Ui4ScreenRect(resources)),"priority_reminders_overlap",reminders!=null&&rect.Overlaps(Ui4ScreenRect(reminders)),"notification_button_overlap",notification!=null&&rect.Overlaps(Ui4ScreenRect(notification)),
            "scope","Programmatic Unity pointer enter plus a real-time dwell; no desktop pointer input.");
    }
    public void Ui4EvidenceRenderInspection(string tile){
        if(!Ui4EvidenceEnabled||!Application.isFocused||_inspectionWorld==null||string.IsNullOrEmpty(tile))return;
        // Deliberately bypass only the Shift input gate for deterministic render QA.
        // The existing data lookup, panel construction, positioning and source highlights run unchanged.
        _inspectionLayer=Layer;_inspectionPhase=MapSourcePhase;_inspectionCity=_cityIdentity;_inspectionSelected=SelectedTile;
        _mapNumericLabelsVisible=true;_inspectionMarkerData=V.Arr();_inspectionMarkerRefreshAt=0;
        if(_inspectionLock!=tile)LockMapInspection(tile);else RefreshInspectionPanel();
        RefreshInspectionMarkers();
    }
    public V Ui4EvidenceInspection(){
        if(!Ui4EvidenceEnabled)return V.Map();
        return V.Map("locked_tile",_inspectionLock,"panel_active",_inspectionPanel!=null&&_inspectionPanel.gameObject.activeInHierarchy,
            "source_row_count",_inspectionContent.GetComponentsInChildren<RectTransform>(false).Count(x=>x.name=="Map source row"),
            "visible_marker_count",_inspectionMarkers.Count(x=>x.gameObject.activeInHierarchy),"center_active",_inspectionMarkers.Count>0&&_inspectionMarkers[0].gameObject.activeInHierarchy,
            "marker_raycast_targets",_inspectionMarkers.SelectMany(x=>x.GetComponentsInChildren<Graphic>(false)).Count(x=>x.raycastTarget),
            "visible_marker_text",V.From(_inspectionMarkerText.Where(x=>x.gameObject.activeInHierarchy).Select(x=>x.text).ToArray()));
    }
}
}
