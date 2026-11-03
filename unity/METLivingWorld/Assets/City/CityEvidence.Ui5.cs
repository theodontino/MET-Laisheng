using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MET.City {
/// <summary>Explicit batchmode-only native UI5 evidence. Never sends OS input.</summary>
public sealed partial class CityEvidence {
    static bool Ui5HeadlessOnly => Environment.GetCommandLineArgs().Contains("--validate-ui5-headless");
    const int Ui5Width=1920, Ui5Height=1080;
    V ui5HeadlessEvidence=V.Map();
    RenderTexture ui5Target;
    RenderTexture ui5UiTarget;
    Camera ui5Camera;
    Camera ui5UiCamera;
    Canvas ui5Canvas;
    RenderTexture ui5OriginalCameraTarget;
    int ui5OriginalWorldCullingMask;
    Camera ui5OriginalCanvasCamera;
    RenderMode ui5OriginalCanvasMode;
    float ui5OriginalPlaneDistance;
    CanvasScaler ui5Scaler;
    CanvasScaler.ScaleMode ui5OriginalScaleMode;
    float ui5OriginalScaleFactor;
    float ui5LastDiagnosticScale=float.NaN;
    bool ui5CompositeProved;
    readonly List<string> ui5Captures=new List<string>();
    float[] ui5Decode;
    byte[] ui5Encode;
    int ui5Layer=-1;
    readonly Dictionary<GameObject,int> ui5OriginalLayers=new Dictionary<GameObject,int>();
    int Ui5ViewportHeight=>Ui5HeadlessOnly?Ui5Height:Screen.height;
    bool Ui5OffscreenReady=>Application.isBatchMode&&ui5Target!=null&&ui5Target.IsCreated()&&
        ui5UiTarget!=null&&ui5UiTarget.IsCreated()&&ui5Camera!=null&&ui5Camera.targetTexture==ui5Target&&
        ui5UiCamera!=null&&ui5UiCamera.targetTexture==ui5UiTarget&&ui5Canvas!=null&&
        ui5Canvas.renderMode==RenderMode.ScreenSpaceCamera&&ui5Canvas.worldCamera==ui5UiCamera;

    IEnumerator Ui5HeadlessJourney(){
        Check("ui5_requires_batchmode",Application.isBatchMode,
            "--validate-ui5-headless requires the Player -batchmode argument");
        Check("ui5_application_version",Application.version=="0.7.0-ui.5.1-local",Application.version);
        BeginUi5Offscreen();
        yield return null;
        Check("ui5_offscreen_render_target_ready",Ui5OffscreenReady);
        Check("ui5_offscreen_camera_pixels",ui5Camera.pixelWidth==Ui5Width&&ui5Camera.pixelHeight==Ui5Height&&
            ui5UiCamera.pixelWidth==Ui5Width&&ui5UiCamera.pixelHeight==Ui5Height,
            V.Arr(ui5Camera.pixelWidth,ui5Camera.pixelHeight,ui5UiCamera.pixelWidth,ui5UiCamera.pixelHeight));
        ui5HeadlessEvidence=V.Map(
            "build_scope","UI5.1 native Player, batchmode diagnostic flag, isolated save root; version 0.7.0-ui.5.1-local.",
            "capture_scope","Three URP SingleCameraRequest renders at 1920x1080: the original world camera keeps its production postprocessing, while the existing overlay uGUI canvas is temporarily bound to a no-postprocess UI camera rendered over black and white mattes. Actual CityFoliage.LateUpdate draw submissions are replayed immediately before each manual world/portrait render, because coroutine captures precede ordinary LateUpdate. The two UI mattes reconstruct coverage in linear color and are composited over the world image. This is a diagnostic offscreen composite, not a Player window screenshot.",
            "input_scope","Programmatic Unity Button callbacks only. No OS window, mouse, keyboard, foreground activation, fullscreen, or human input claim.",
            "pending_real_input",V.Arr("Visible fullscreen 1920x1080 presentation","OS mouse hit targets and hover/dwell tooltips","OS keyboard navigation, L / Shift+L and Esc with focus ownership","map pointer hover, source marker and click gate","human readability and usability","GPU frame pacing and system load"));

        // Preserve the previous real earned progression, copy, preview and save/load regression.
        // UI4 focus/dwell checks are deliberately not imported into this batchmode route.
        yield return BlueprintUIJourney();
        yield return Ui51EarnedArchivePages();
        Click("返回地图");yield return null;
        Check("ui5_selected_building_context",!string.IsNullOrEmpty(city.Interface.SelectedBuilding)&&
            city.Interface.GetComponentsInChildren<RawImage>(true).Any(x=>x.name=="Committed Model Camera Image"&&x.gameObject.activeInHierarchy));
        CheckUi5MapSourceGeometry("selected_building");
        yield return Capture("ui5-selected-building-context.png");
        city.Interface.OpenArchive("milestones");yield return null;
        Check("ui5_milestones_route",city.Interface.Ui5EvidencePage()=="milestones");
        var milestoneState=GameplayState();
        var milestoneView=city.Interface.Ui5EvidenceMilestones();
        Check("ui5_milestones_three_saved_record_groups",milestoneView.Get("stage_count",0).Long==3,
            milestoneView);
        Check("ui51_milestones_saved_states_match",milestoneView.Get("saved_states_match",false).Bool,milestoneView);
        CheckUi5ArchivePage("milestones_default",new[]{"Milestone Stage 1","Milestone Stage 2","Milestone Stage 3","Milestones Theme Action"});
        Check("ui5_milestones_read_only",GameplayState()==milestoneState);
        yield return Capture("ui5-milestones.png");

        city.Runtime.NewCity(7040);yield return null;
        Click("建立地区 · 在此登陆");yield return null;
        Click("确立起源 · 安顿居民");yield return null;
        Check("ui5_fresh_city_play_phase",city.Runtime.Game.s["phase"].Text=="play");
        city.Runtime.SetPaused(true);yield return null;
        var freshProjects=city.Runtime.Game.s["projects"];
        var freshIds=city.Runtime.Game.playable_ids().Select(x=>x.Text).ToArray();
        var visibleBindings=city.World.GetComponentsInChildren<CityBuildingIdentity>(true)
            .Where(x=>x.gameObject.activeInHierarchy).ToArray();
        Check("ui5_fresh_city_world_matches_current_projects",city.World.BuildingCount==freshIds.Length&&
            visibleBindings.Length==freshIds.Length&&freshIds.All(id=>visibleBindings.Any(x=>
                x.BuildingId==id&&x.DefinitionId==freshProjects[id]["def"].Text&&
                x.TileId==freshProjects[id]["tile"].Text))&&
            ui5Camera==city.World.CameraController.WorldCamera,
            V.Map("current_projects",V.From(freshIds.Select(id=>V.Arr(id,freshProjects[id]["def"],freshProjects[id]["tile"])).ToArray()),
                "world_bindings",V.From(visibleBindings.Select(x=>V.Arr(x.BuildingId,x.DefinitionId,x.TileId,x.ModelResource)).ToArray()),
                "world_count",city.World.BuildingCount));
        city.Interface.HideDeskTip();
        CheckUi5Hud("default");
        yield return Capture("ui5-unselected-hud.png");

        var ui=city.Interface;
        var beforeGuidance=GameplayState();
        var guidance=ui.CityGuidanceView();
        var bound=ui.Ui5EvidenceGuidance();
        Check("ui5_guidance_has_actual_fields",new[]{"id","title","reason","action_label","route","layer","base_id","severity"}.All(guidance.Has),guidance);
        Check("ui5_guidance_hud_matches_detached_decision",bound.Get("title","").Text==guidance.Get("title","").Text&&
            bound.Get("reason","").Text==guidance.Get("reason","").Text&&
            bound.Get("action_label","").Text==guidance.Get("action_label","").Text+"  ›",V.Map("view",guidance,"hud",bound));
        Check("ui5_guidance_read_only",GameplayState()==beforeGuidance);
        var action=ui.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="HUD Next Action"&&b.gameObject.activeInHierarchy);
        Check("ui5_guidance_action_callback_present",action!=null&&action.IsInteractable());
        if(guidance.Get("route","").Text=="resume"&&!city.Runtime.MandatoryPending){
            Check("ui5_guidance_paused_before_resume",city.Runtime.WorldPaused);
            action.onClick.Invoke();yield return null;
            Check("ui5_guidance_explicit_resume",!city.Runtime.WorldPaused);
            city.Runtime.SetPaused(true);yield return null;
        }else{
            action.onClick.Invoke();yield return null;
            Check("ui5_guidance_action_opens_destination",ui.IsModal);
            Click("返回地图");yield return null;
        }

        var cityButton=Ui5HudButton("HUD Navigation","城市");
        Check("ui5_city_navigation_callback",cityButton!=null&&cityButton.IsInteractable());
        cityButton.onClick.Invoke();yield return null;
        Check("ui5_city_opens_overview",ui.IsModal&&ui.Ui5EvidencePage()=="overview");
        Check("ui5_overview_contains_city_situation",ui.Ui5EvidenceOverview().Get("city_situation",false).Bool,
            ui.Ui5EvidenceOverview());
        CheckUi5ArchivePage("overview_default",new[]{"Overview Next Action","Overview Living Summary","Overview Supply Summary","Overview Construction Summary"});
        yield return Capture("ui5-city-overview.png");
        Click("返回地图");yield return null;
        Check("ui5_city_close_returns_to_map",!ui.IsModal);

        var beforeLayers=GameplayState();
        var service=Ui5HudButton("HUD Map Layers","服务");
        Check("ui5_map_layer_callback",service!=null&&service.IsInteractable());
        service.onClick.Invoke();yield return null;
        Check("ui5_map_layer_source_is_committed",ui.Layer=="service"&&ui.MapSourcePhase=="committed"&&
            ui.MapSnapshot.Count>0&&GameplayState()==beforeLayers);
        yield return Capture("ui5-service-layer.png");
        Ui5HudButton("HUD Map Layers","地貌").onClick.Invoke();yield return null;
        Check("ui5_map_layer_returns_to_terrain",ui.Layer=="terrain"&&GameplayState()==beforeLayers);

        foreach(var baseId in city.Runtime.Game.c.v040["progression"]["tier1"].Select(x=>x.Text)){
            var choice=ui.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="HUD Build Choice "+baseId&&b.gameObject.activeInHierarchy);
            Check("ui5_build_choice_present_"+baseId,choice!=null&&choice.IsInteractable());
            var beforeChoice=GameplayState();choice.onClick.Invoke();yield return null;
            var selection=ui.Ui5EvidenceConstructionIntent();
            Check("ui5_build_choice_sets_intent_"+baseId,selection.Get("active",false).Bool&&selection.Get("base_id","").Text==baseId&&GameplayState()==beforeChoice,selection);
            if(baseId=="homestead")yield return Capture("ui5-foundation-choice.png");
        }
        ui.OpenArchive("overview");yield return null;
        Check("ui5_open_archive_clears_build_intent",!ui.ConstructionIntentActive);
        Click("返回地图");yield return null;

        // A real draft supplies the same-step source preview; only UI callbacks are synthetic.
        var game=city.Runtime.Game;
        var tile=game.s["cells"].Keys.First(k=>game.s["cells"][k]["building"].Text==""&&
            !game.s["cells"][k]["traits"].Contains("tile.water"));
        string tender=city.Runtime.Execute(actual=>actual.create_tender(V.Arr(tile),"homestead"));
        Check("ui5_real_tender_created",tender==""&&game.s["draft"].Get("offers",V.Arr()).Count==3,tender);
        ui.OpenArchive("tender");yield return null;
        var tenderState=GameplayState();
        Check("ui5_tender_live_source_comparison",ui.ComparisonBaseline.Count>0&&ui.ComparisonProposal.Count>0,
            V.Map("baseline_count",ui.ComparisonBaseline.Count,"proposal_count",ui.ComparisonProposal.Count));
        CheckUi5ArchivePage("tender_default",game.s["draft"]["offers"].Select(x=>"OperatorChoice "+x["id"].Text).ToArray());
        yield return Capture("ui5-tender.png");
        city.Runtime.SetDisplaySetting("ui_scale",1.2);
        city.Runtime.SetDisplaySetting("text_scale",1.5);ui.ApplyDisplay();RefreshUi5CanvasScale();ui.OpenArchive("tender");yield return null;
        CheckUi5ArchivePage("tender_ui120_text150");
        var tenderFooter=ui.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(x=>x.name=="Construction Decision Footer"&&x.gameObject.activeInHierarchy);
        var tenderPrimary=ui.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(x=>x.name=="Construction Primary"&&x.gameObject.activeInHierarchy);
        Check("ui51_tender_primary_fixed_visible",tenderFooter!=null&&tenderPrimary!=null&&
            Ui5Inside(Ui5ScreenRect(tenderPrimary))&&Ui5Contained(Ui5ScreenRect(tenderPrimary),Ui5ScreenRect(tenderFooter)));
        yield return Capture("ui51-tender-ui120-text150.png");
        city.Runtime.SetDisplaySetting("ui_scale",1);
        city.Runtime.SetDisplaySetting("text_scale",1);ui.ApplyDisplay();RefreshUi5CanvasScale();ui.OpenArchive("tender");yield return null;
        var mapFold=ui.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&
            b.GetComponentInChildren<Text>()?.text=="在地图上比较 ›");
        Check("ui5_map_comparison_disclosure",mapFold!=null&&mapFold.IsInteractable());
        mapFold.onClick.Invoke();yield return null;
        Click("地图 · 建成后");yield return null;
        Check("ui5_proposal_preview_is_read_only",ui.MapSourcePhase=="proposal"&&
            ui.MapSnapshot.ToJson(false,true)==ui.ComparisonProposal.ToJson(false,true)&&GameplayState()==tenderState);
        yield return Capture("ui5-proposal-map.png");
        Click("返回方案比较 ›");yield return null;
        Check("ui5_source_preview_returns_to_tender",ui.IsModal&&GameplayState()==tenderState);
        var defer=ui.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="Construction Defer"&&b.gameObject.activeInHierarchy);
        Check("ui5_tender_defer_callback",defer!=null&&defer.IsInteractable());
        defer.onClick.Invoke();yield return null;
        Check("ui5_tender_defer_returns_to_map",!ui.IsModal&&game.s["draft"].Count==0);

        city.Runtime.SetDisplaySetting("ui_scale",.8);
        city.Runtime.SetDisplaySetting("text_scale",1);ui.ApplyDisplay();yield return null;
        CheckUi5Hud("ui80_text100");
        yield return Capture("ui5-ui80-text100.png");
        city.Runtime.SetDisplaySetting("ui_scale",1);
        city.Runtime.SetDisplaySetting("text_scale",1);ui.ApplyDisplay();yield return null;
        CheckUi5Hud("ui100_text100_roundtrip");
        yield return Capture("ui5-ui100-text100.png");
        ui.OpenArchive("settings");yield return null;
        var ui120=BlueprintButton(ui,t=>t.Contains("界面 120%"));
        Check("ui5_ui120_settings_callback_available",ui120!=null);
        ui120.onClick.Invoke();yield return null;
        var text150=BlueprintButton(ui,t=>t.Contains("字号 150%"));
        Check("ui5_text150_settings_callback_available",text150!=null);
        text150.onClick.Invoke();yield return null;
        Click("返回地图");yield return null;
        CheckUi5Hud("ui120_text150");
        var largeFonts=ui.Ui5EvidenceHudFonts();
        Check("ui5_text150_hud_font_targets",largeFonts.Get("layer",0).Long==21&&
            largeFonts.Get("navigation",0).Long==22&&largeFonts.Get("pause",0).Long==22,largeFonts);
        yield return Capture("ui5-ui120-text150.png");
        var brandText=ui.GetComponentsInChildren<Text>(true).FirstOrDefault(x=>x.name=="HUD Brand"&&x.gameObject.activeInHierarchy);
        var speedButton=ui.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.name=="HUD 1×"&&x.gameObject.activeInHierarchy);
        var speedText=speedButton==null?null:speedButton.GetComponentInChildren<Text>();
        Check("ui5_text150_brand_met_single_line_all_visible",Ui5OneRenderedLine(brandText,"MET"),Ui5RenderedTextDetail(brandText));
        Check("ui5_text150_speed_one_single_line_all_visible",Ui5OneRenderedLine(speedText,"1×"),Ui5RenderedTextDetail(speedText));
        ui.OpenArchive("settings");yield return null;
        var ui100=BlueprintButton(ui,t=>t.Contains("界面 100%"));
        Check("ui5_ui100_settings_callback_available",ui100!=null);
        ui100.onClick.Invoke();yield return null;
        var text100=BlueprintButton(ui,t=>t.Contains("字号 100%"));
        Check("ui5_text100_settings_callback_available",text100!=null);
        text100.onClick.Invoke();yield return null;
        Click("返回地图");yield return null;
        var restoredFonts=ui.Ui5EvidenceHudFonts();
        Check("ui5_text100_hud_font_targets_restored",restoredFonts.Get("layer",0).Long==14&&
            restoredFonts.Get("navigation",0).Long==15&&restoredFonts.Get("pause",0).Long==15,restoredFonts);
        Check("ui5_display_roundtrip_80_100_120",Math.Abs(city.Runtime.DisplaySettings.Get("ui_scale",0).Double-1)<.001&&
            Math.Abs(city.Runtime.DisplaySettings.Get("text_scale",0).Double-1)<.001);
        Check("ui5_city_state_valid",city.Runtime.Session.Validate()=="");
        Check("ui5_no_native_errors",errors.Count==0,V.From(errors));
        ui5HeadlessEvidence["screenshots"]=V.From(ui5Captures);
    }

    // Run while BlueprintUIJourney's earned, saved city is still loaded. Page callbacks
    // and disclosure buttons must leave the serialized game (including RNG) untouched.
    IEnumerator Ui51EarnedArchivePages(){
        var ui=city.Interface;var game=city.Runtime.Game;
        city.Runtime.SetPaused(true);
        Check("ui51_earned_page_content",Themes.view(game).Get("id","").Text!=""&&
            (game.s["block"].Get("bonus_wallet",V.Map()).Count>0||game.s.Get("bonus_instances",V.Map()).Count>0));
        city.Runtime.SetDisplaySetting("ui_scale",1);
        city.Runtime.SetDisplaySetting("text_scale",1);ui.ApplyDisplay();RefreshUi5CanvasScale();yield return null;
        foreach(var page in new[]{"themes","bonuses"}){
            var before=GameplayState();ui.OpenArchive(page);yield return null;
            Check("ui51_earned_open_read_only_"+page,GameplayState()==before);
            CheckUi5ArchivePage(page+"_default");
            yield return Capture("ui51-"+(page=="themes"?"tasks":"bonuses")+"-default.png");
            var fold=Ui5ArchiveFold(page=="themes"?"完整委任、改向与历史":"其他库存与已部署来源");
            Check("ui51_earned_fold_available_"+page,fold!=null&&fold.IsInteractable());
            fold.onClick.Invoke();yield return null;
            Check("ui51_earned_fold_read_only_"+page,GameplayState()==before);
            var scroll=ui.GetComponentsInChildren<ScrollRect>(true).FirstOrDefault(x=>x.name=="Archive Scroll");
            if(scroll!=null)scroll.verticalNormalizedPosition=0;
            yield return null;
            CheckUi5ArchivePage(page+"_default_expanded");
            yield return Capture("ui51-"+(page=="themes"?"tasks":"bonuses")+"-default-detail.png");
        }
        city.Runtime.SetDisplaySetting("ui_scale",1.2);
        city.Runtime.SetDisplaySetting("text_scale",1.5);ui.ApplyDisplay();RefreshUi5CanvasScale();yield return null;
        foreach(var page in new[]{"overview","milestones","themes","bonuses"}){
            var before=GameplayState();ui.OpenArchive(page);yield return null;
            Check("ui51_combined_open_read_only_"+page,GameplayState()==before);
            CheckUi5ArchivePage(page+"_ui120_text150");
            if(page=="overview")Check("ui51_combined_overview_semantics",ui.Ui5EvidenceOverview().Get("city_situation",false).Bool);
            if(page=="milestones")Check("ui51_combined_milestone_semantics",ui.Ui5EvidenceMilestones().Get("saved_states_match",false).Bool);
            var shortName=page=="themes"?"tasks":page;
            yield return Capture("ui51-"+shortName+"-ui120-text150.png");
            if(page!="themes"&&page!="bonuses")continue;
            var fold=Ui5ArchiveFold(page=="themes"?"完整委任、改向与历史":"其他库存与已部署来源");
            Check("ui51_combined_fold_available_"+page,fold!=null&&fold.IsInteractable());
            fold.onClick.Invoke();yield return null;
            Check("ui51_combined_fold_read_only_"+page,GameplayState()==before);
            var scroll=ui.GetComponentsInChildren<ScrollRect>(true).FirstOrDefault(x=>x.name=="Archive Scroll");
            if(scroll!=null)scroll.verticalNormalizedPosition=0;
            yield return null;
            CheckUi5ArchivePage(page+"_ui120_text150_expanded");
            yield return Capture("ui51-"+shortName+"-ui120-text150-detail.png");
        }
        city.Runtime.SetDisplaySetting("ui_scale",1);
        city.Runtime.SetDisplaySetting("text_scale",1);ui.ApplyDisplay();RefreshUi5CanvasScale();yield return null;
    }

    void BeginUi5Offscreen(){
        ui5Camera=city.World.CameraController.WorldCamera;
        Check("ui5_world_camera_available",ui5Camera!=null);
        ui5Canvas=city.Interface.GetComponentsInChildren<Canvas>(true).FirstOrDefault(c=>c.name=="City Command Interface");
        Check("ui5_existing_ugui_canvas_available",ui5Canvas!=null);
        ui5OriginalCameraTarget=ui5Camera.targetTexture;
        ui5OriginalWorldCullingMask=ui5Camera.cullingMask;
        ui5OriginalCanvasCamera=ui5Canvas.worldCamera;
        ui5OriginalCanvasMode=ui5Canvas.renderMode;
        ui5OriginalPlaneDistance=ui5Canvas.planeDistance;
        ui5Scaler=ui5Canvas.GetComponent<CanvasScaler>();
        Check("ui5_existing_canvas_scaler_available",ui5Scaler!=null);
        ui5OriginalScaleMode=ui5Scaler.uiScaleMode;
        ui5OriginalScaleFactor=ui5Scaler.scaleFactor;
        ui5Target=new RenderTexture(Ui5Width,Ui5Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        ui5Target.name="UI5 Headless World 1920x1080";
        ui5Target.Create();
        ui5Camera.targetTexture=ui5Target;
        ui5UiTarget=new RenderTexture(Ui5Width,Ui5Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        ui5UiTarget.name="UI5 Headless UI 1920x1080";
        ui5UiTarget.Create();
        var uiCameraObject=new GameObject("UI5 Headless UI Camera");
        uiCameraObject.transform.SetParent(transform,false);
        ui5UiCamera=uiCameraObject.AddComponent<Camera>();
        ui5UiCamera.CopyFrom(ui5Camera);
        ui5UiCamera.transform.SetPositionAndRotation(ui5Camera.transform.position,ui5Camera.transform.rotation);
        ui5UiCamera.enabled=false;
        ui5UiCamera.clearFlags=CameraClearFlags.SolidColor;
        ui5UiCamera.backgroundColor=new Color(0,0,0,0);
        var occupiedWorldLayers=new HashSet<int>(city.World.GetComponentsInChildren<Transform>(true).Select(x=>x.gameObject.layer));
        ui5Layer=Enumerable.Range(24,8).Reverse().FirstOrDefault(x=>!occupiedWorldLayers.Contains(x));
        Check("ui5_dedicated_ui_layer_available",ui5Layer>=24&&!occupiedWorldLayers.Contains(ui5Layer),ui5Layer);
        ui5UiCamera.cullingMask=1<<ui5Layer;
        ui5Camera.cullingMask=ui5OriginalWorldCullingMask&~(1<<ui5Layer);
        ui5UiCamera.allowHDR=false;
        ui5UiCamera.allowMSAA=false;
        ui5UiCamera.targetTexture=ui5UiTarget;
        var uiCameraData=ui5UiCamera.GetUniversalAdditionalCameraData();
        uiCameraData.renderPostProcessing=false;
        uiCameraData.antialiasing=AntialiasingMode.None;
        ui5Canvas.renderMode=RenderMode.ScreenSpaceCamera;
        ui5Canvas.worldCamera=ui5UiCamera;
        ui5Canvas.planeDistance=1f;
        ui5Scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
        RefreshUi5CanvasScale();
        RefreshUi5Layer();
        ui5Decode=new float[256];ui5Encode=new byte[4097];
        for(int i=0;i<256;i++){float value=i/255f;ui5Decode[i]=value<=.04045f?value/12.92f:Mathf.Pow((value+.055f)/1.055f,2.4f);}
        for(int i=0;i<ui5Encode.Length;i++){float value=i/4096f;float srgb=value<=.0031308f?12.92f*value:1.055f*Mathf.Pow(value,1f/2.4f)-.055f;ui5Encode[i]=(byte)Mathf.Clamp(Mathf.RoundToInt(srgb*255),0,255);}
        Canvas.ForceUpdateCanvases();
    }

    void CleanupUi5Offscreen(){
        if(ui5Canvas!=null){
            ui5Canvas.renderMode=ui5OriginalCanvasMode;
            ui5Canvas.worldCamera=ui5OriginalCanvasCamera;
            ui5Canvas.planeDistance=ui5OriginalPlaneDistance;
        }
        if(ui5Scaler!=null){ui5Scaler.uiScaleMode=ui5OriginalScaleMode;ui5Scaler.scaleFactor=ui5OriginalScaleFactor;}
        if(ui5Camera!=null)ui5Camera.targetTexture=ui5OriginalCameraTarget;
        if(ui5Camera!=null)ui5Camera.cullingMask=ui5OriginalWorldCullingMask;
        foreach(var pair in ui5OriginalLayers)if(pair.Key!=null)pair.Key.layer=pair.Value;
        ui5OriginalLayers.Clear();
        if(ui5Target!=null){ui5Target.Release();Destroy(ui5Target);ui5Target=null;}
        if(ui5UiTarget!=null){ui5UiTarget.Release();Destroy(ui5UiTarget);ui5UiTarget=null;}
        if(ui5UiCamera!=null){Destroy(ui5UiCamera.gameObject);ui5UiCamera=null;}
    }

    Texture2D RenderUi5Texture(Camera camera,RenderTexture target,bool uiLayer){
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
        Check("ui5_urp_render_request_supported_"+(uiLayer?"ui":"world"),RenderPipeline.SupportsRenderRequest(camera,request));
        var oldActive=RenderTexture.active;
        var pipeline=(QualitySettings.renderPipeline!=null?QualitySettings.renderPipeline:GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
        Check("ui5_offscreen_urp_asset",pipeline!=null);
        float oldScale=pipeline.renderScale;
        var image=new Texture2D(Ui5Width,Ui5Height,TextureFormat.RGB24,false);
        try{
            if(uiLayer){pipeline.renderScale=1f;RefreshUi5Layer();}
            else SubmitUi5TransientWorldDraws();
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,Ui5Width,Ui5Height),0,0,false);
            image.Apply(false,false);
            return image;
        }catch{Destroy(image);throw;}
        finally{pipeline.renderScale=oldScale;RenderTexture.active=oldActive;}
    }
    void RefreshUi5Layer(){
        if(ui5Canvas==null||ui5Layer<0)return;
        foreach(var child in ui5Canvas.GetComponentsInChildren<Transform>(true)){
            var go=child.gameObject;
            if(!ui5OriginalLayers.ContainsKey(go))ui5OriginalLayers.Add(go,go.layer);
            go.layer=ui5Layer;
        }
    }
    void SubmitUi5TransientWorldDraws(){
        // Coroutine captures run before normal LateUpdate. Instanced vegetation
        // is a per-frame draw submission, not a MeshRenderer in the scene cull.
        // Replay the existing production draw method; do not create scenery,
        // change placements/materials, or modify the ordinary render path.
        foreach(var foliage in city.World.GetComponentsInChildren<CityFoliage>(true))
            if(foliage.isActiveAndEnabled)foliage.SendMessage("LateUpdate",SendMessageOptions.RequireReceiver);
    }
    void RefreshUi5CanvasScale(){
        if(ui5Scaler==null||ui5Canvas==null)return;
        float factor=Mathf.Clamp((float)city.Runtime.DisplaySettings.Get("ui_scale",1).Double,.8f,1.2f);
        if(Mathf.Abs(ui5LastDiagnosticScale-factor)<.0001f&&
            Mathf.Abs(ui5Scaler.scaleFactor-factor)<.0001f&&
            Mathf.Abs(ui5Canvas.scaleFactor-factor)<.0001f)return;
        ui5Scaler.scaleFactor=factor;
        ui5Canvas.scaleFactor=factor;
        ui5LastDiagnosticScale=factor;
        foreach(var label in ui5Canvas.GetComponentsInChildren<Text>(true))label.SetAllDirty();
        Canvas.ForceUpdateCanvases();
    }

    void CaptureUi5Offscreen(string name){
        Check("ui5_capture_context_"+name,Ui5OffscreenReady);
        Texture2D image=null,world=null,uiBlack=null,uiWhite=null;
        try{
            city.Interface.HideDeskTip();
            RefreshUi5CanvasScale();
            float expectedScale=Mathf.Clamp((float)city.Runtime.DisplaySettings.Get("ui_scale",1).Double,.8f,1.2f);
            Check("ui5_capture_full_resolution_font_scale_"+name,Mathf.Abs(ui5Canvas.scaleFactor-expectedScale)<.005f,
                V.Map("canvas_scale",ui5Canvas.scaleFactor,"expected",expectedScale,"batch_screen",V.Arr(Screen.width,Screen.height),"target",V.Arr(Ui5Width,Ui5Height)));
            RefreshUi5ModelPortrait(name);
            if(!ui5HeadlessEvidence.Has("nature_captures"))ui5HeadlessEvidence["nature_captures"]=V.Map();
            ui5HeadlessEvidence["nature_captures"][name]=city.World.NatureDiagnostics.Duplicate(true);
            world=RenderUi5Texture(ui5Camera,ui5Target,false);
            ui5UiCamera.backgroundColor=Color.black;
            uiBlack=RenderUi5Texture(ui5UiCamera,ui5UiTarget,true);
            ui5UiCamera.backgroundColor=Color.white;
            uiWhite=RenderUi5Texture(ui5UiCamera,ui5UiTarget,true);
            if(!ui5CompositeProved){
                int[] xs={200,1750,50};int[] ys={1040,1040,50};int covered=0;
                for(int i=0;i<xs.Length;i++){
                    var black=uiBlack.GetPixel(xs[i],ys[i]);var white=uiWhite.GetPixel(xs[i],ys[i]);
                    if(Mathf.Abs(white.r-black.r)+Mathf.Abs(white.g-black.g)+Mathf.Abs(white.b-black.b)<2.4f)covered++;
                }
                Check("ui5_capture_ui_mattes_show_hud_coverage",covered>=2,
                    V.Map("covered_hud_sample_points",covered,"sample_points",xs.Length));
                ui5CompositeProved=true;
            }
            var background=world.GetPixels32();var blackPixels=uiBlack.GetPixels32();var whitePixels=uiWhite.GetPixels32();
            Check("ui5_capture_pixel_layers_match_"+name,background.Length==blackPixels.Length&&
                background.Length==whitePixels.Length&&background.Length==Ui5Width*Ui5Height);
            for(int i=0;i<background.Length;i++){
                var back=background[i];var black=blackPixels[i];var white=whitePixels[i];
                background[i]=new Color32(Ui5Compose(black.r,white.r,back.r),
                    Ui5Compose(black.g,white.g,back.g),Ui5Compose(black.b,white.b,back.b),255);
            }
            image=new Texture2D(Ui5Width,Ui5Height,TextureFormat.RGB24,false);
            image.SetPixels32(background);image.Apply(false,false);
            Check("capture_size_"+name,image.width==Ui5Width&&image.height==Ui5Height);
            var bytes=ImageConversion.EncodeToPNG(image);
            Check("capture_pixels_"+name,bytes!=null&&bytes.Length>1000,
                bytes==null?0:bytes.Length);
            File.WriteAllBytes(Path.Combine(directory,name),bytes);
            ui5Captures.Add(name);
            ui5HeadlessEvidence["screenshots"]=V.From(ui5Captures);
        }finally{if(world!=null)Destroy(world);if(uiBlack!=null)Destroy(uiBlack);if(uiWhite!=null)Destroy(uiWhite);if(image!=null)Destroy(image);}
    }
    byte Ui5Compose(byte black,byte white,byte world){
        float value=ui5Decode[black]+(ui5Decode[white]-ui5Decode[black])*ui5Decode[world];
        return ui5Encode[Mathf.Clamp(Mathf.RoundToInt(value*4096),0,4096)];
    }
    void RefreshUi5ModelPortrait(string name){
        var portrait=city.Interface.GetComponentsInChildren<RawImage>(true)
            .FirstOrDefault(x=>x.name=="Committed Model Camera Image"&&x.gameObject.activeInHierarchy);
        if(portrait==null){
            if(name=="ui-hud-building.png"||name=="ui5-selected-building-context.png")
                Check("ui5_model_portrait_visible_"+name,false);
            return;
        }
        var camera=city.World.GetComponentsInChildren<Camera>(true).FirstOrDefault(x=>x.name=="City Building Portrait Camera");
        var target=portrait.texture as RenderTexture;
        Check("ui5_model_portrait_camera_and_target_"+name,camera!=null&&target!=null&&camera.targetTexture==target);
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
        Check("ui5_model_portrait_request_supported_"+name,RenderPipeline.SupportsRenderRequest(camera,request));
        SubmitUi5TransientWorldDraws();
        RenderPipeline.SubmitRenderRequest(camera,request);
    }

    Rect Ui5ScreenRect(RectTransform target){
        if(target==null)return Rect.zero;
        if(Ui5HeadlessOnly)RefreshUi5CanvasScale();
        var corners=new Vector3[4];target.GetWorldCorners(corners);
        var points=corners.Select(p=>Ui5HeadlessOnly?RectTransformUtility.WorldToScreenPoint(ui5UiCamera,p):new Vector2(p.x,p.y)).ToArray();
        return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
    }
    bool Ui5Inside(Rect box)=>box.width>0&&box.height>0&&box.xMin>=-1&&box.yMin>=-1&&
        box.xMax<=(Ui5HeadlessOnly?Ui5Width:Screen.width)+1&&box.yMax<=Ui5ViewportHeight+1;
    static bool Ui5Contained(Rect box,Rect parent)=>box.width>0&&box.height>0&&
        box.xMin>=parent.xMin-1&&box.xMax<=parent.xMax+1&&box.yMin>=parent.yMin-1&&box.yMax<=parent.yMax+1;
    static bool Ui5Separate(Rect a,Rect b)=>a.xMax<=b.xMin+1||b.xMax<=a.xMin+1||a.yMax<=b.yMin+1||b.yMax<=a.yMin+1;
    void Ui51LayoutCheck(string name,bool passed,object detail=null){
        checks.Add(V.Map("name",name,"passed",passed,"detail",V.From(detail)));
        if(!passed)errors.Add("UI5.1 layout check failed: "+name);
    }
    void CheckUi5ArchivePage(string stage,string[] firstViewportNodes=null){
        RefreshUi5CanvasScale();Canvas.ForceUpdateCanvases();
        var ui=city.Interface;var roots=ui.GetComponentsInChildren<RectTransform>(true);
        var panel=roots.FirstOrDefault(x=>x.name=="L3 City Side Panel"&&x.gameObject.activeInHierarchy);
        var scroll=roots.FirstOrDefault(x=>x.name=="Archive Scroll"&&x.gameObject.activeInHierarchy);
        Ui51LayoutCheck("ui5_archive_layout_roots_"+stage,panel!=null&&scroll!=null);
        var panelBox=Ui5ScreenRect(panel);var viewport=Ui5ScreenRect(scroll);
        Ui51LayoutCheck("ui5_archive_panel_inside_"+stage,Ui5Inside(panelBox),Ui5Box(panelBox));
        if(firstViewportNodes!=null){
            var found=firstViewportNodes.Select(name=>roots.FirstOrDefault(x=>x.name==name&&x.gameObject.activeInHierarchy&&x.transform.IsChildOf(scroll))).ToArray();
            Ui51LayoutCheck("ui5_first_viewport_"+stage,found.All(x=>x!=null&&Ui5Contained(Ui5ScreenRect(x),viewport)),
                V.From(found.Select((x,i)=>V.Map("node",firstViewportNodes[i],"rect",x==null?V.Map():Ui5Box(Ui5ScreenRect(x)))).ToArray()));
        }
        var overviewArt=roots.FirstOrDefault(x=>x.name=="Overview Action Art"&&x.gameObject.activeInHierarchy);
        if(overviewArt!=null&&overviewArt.childCount>0)Ui51LayoutCheck("ui51_overview_art_contained_"+stage,
            Ui5Contained(Ui5ScreenRect((RectTransform)overviewArt.GetChild(0)),Ui5ScreenRect(overviewArt)));
        bool displayed(RectTransform r){var box=Ui5ScreenRect(r);return !r.IsChildOf(scroll)||Ui5Contained(box,viewport);}
        var text=panel.GetComponentsInChildren<Text>(true).Where(x=>x.gameObject.activeInHierarchy&&x.text!=""&&displayed(x.rectTransform)).ToArray();
        var clipped=text.Where(x=>x.preferredHeight>x.rectTransform.rect.height+3f).Take(8).ToArray();
        Ui51LayoutCheck("ui5_public_text_fits_"+stage,clipped.Length==0,
            V.From(clipped.Select(x=>V.Map("text",x.text,"preferred",x.preferredHeight,"height",x.rectTransform.rect.height)).ToArray()));
        var overlaps=new List<V>();
        foreach(var layout in panel.GetComponentsInChildren<HorizontalOrVerticalLayoutGroup>(true)){
            if(!layout.gameObject.activeInHierarchy)continue;
            var children=layout.transform.Cast<Transform>().OfType<RectTransform>()
                .Where(x=>x.gameObject.activeInHierarchy&&displayed(x)&&!(x.GetComponent<LayoutElement>()?.ignoreLayout??false)).ToArray();
            for(int i=0;i<children.Length;i++)for(int j=i+1;j<children.Length;j++){
                var a=Ui5ScreenRect(children[i]);var b=Ui5ScreenRect(children[j]);
                if(!Ui5Separate(a,b))overlaps.Add(V.Map("parent",layout.name,"a",children[i].name,"b",children[j].name,"a_box",Ui5Box(a),"b_box",Ui5Box(b)));
            }
        }
        Ui51LayoutCheck("ui5_public_siblings_separate_"+stage,overlaps.Count==0,V.From(overlaps.Take(8).ToArray()));
    }
    Button Ui5ArchiveFold(string prefix)=>city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>
        b.gameObject.activeInHierarchy&&b.GetComponentInChildren<Text>()?.text.StartsWith(prefix,StringComparison.Ordinal)==true);
    Button Ui5HudButton(string group,string caption){
        var root=city.Interface.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(x=>x.name==group);
        return root==null?null:root.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>
            b.gameObject.activeInHierarchy&&b.GetComponentInChildren<Text>()?.text==caption);
    }
    void CheckUi5Hud(string stage){
        RefreshUi5CanvasScale();
        Canvas.ForceUpdateCanvases();
        var ui=city.Interface;
        var root=ui.GetComponentsInChildren<RectTransform>(true);
        RectTransform named(string name)=>root.FirstOrDefault(r=>r.name==name&&r.gameObject.activeInHierarchy);
        var resources=named("HUD Resources");var nav=named("HUD Navigation");var bucket=named("Bonus Blueprint Bucket");
        var build=named("HUD Construction");var layers=named("HUD Map Layers");var time=named("HUD Time Controls");
        var overview=named("HUD Overview");
        Check("ui5_hud_parts_present_"+stage,resources!=null&&nav!=null&&bucket!=null&&build!=null&&layers!=null&&time!=null&&overview!=null);
        var resourceIds=resources.GetComponentsInChildren<RectTransform>(true).Where(x=>x.name.StartsWith("HUD Resource ",StringComparison.Ordinal)).Select(x=>x.name).ToArray();
        Check("ui5_seven_live_resource_cells_"+stage,resourceIds.Length==7&&resourceIds.Contains("HUD Resource 电力")&&
            !resourceIds.Contains("HUD Resource 未安装副本"),V.From(resourceIds));
        var charges=city.Runtime.Game.s["block"].Get("bonus_wallet",V.Map()).Values.Sum(w=>w.Get("charges",0).Long);
        var bucketText=bucket.GetComponentsInChildren<Text>(true).Select(t=>t.text).ToArray();
        Check("ui5_bonus_bucket_actual_wallet_copies_"+stage,bucketText.Any(t=>t.Contains(charges.ToString())),
            V.Map("charges",charges,"display",V.From(bucketText)));
        var r=Ui5ScreenRect(resources);var n=Ui5ScreenRect(nav);var b=Ui5ScreenRect(build);var l=Ui5ScreenRect(layers);var t=Ui5ScreenRect(time);
        var k=Ui5ScreenRect(bucket);var o=Ui5ScreenRect(overview);
        Check("ui5_hud_footprints_inside_"+stage,new[]{r,n,b,l,t,k,o}.All(Ui5Inside),
            V.Map("resources",Ui5Box(r),"navigation",Ui5Box(n),"bucket",Ui5Box(k),"construction",Ui5Box(b),"layers",Ui5Box(l),"time",Ui5Box(t),"overview",Ui5Box(o)));
        var last=resources.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(x=>x.name=="HUD Resource 电力");
        Check("ui5_hud_top_cells_clear_navigation_"+stage,last!=null&&Ui5ScreenRect(last).xMax<=n.xMin+1);
        Check("ui5_hud_bottom_groups_separate_"+stage,Ui5Separate(b,l)&&Ui5Separate(l,t)&&Ui5Separate(b,t));
        var navButtons=nav.GetComponentsInChildren<Button>(true).Where(x=>x.gameObject.activeInHierarchy).ToArray();
        Check("ui5_hud_top_controls_separate_"+stage,Ui5Separate(k,o)&&
            navButtons.Length==4&&navButtons.All(x=>Ui5Separate(k,Ui5ScreenRect((RectTransform)x.transform))),
            V.Map("bucket",Ui5Box(k),"navigation_buttons",V.From(navButtons.Select(x=>Ui5Box(Ui5ScreenRect((RectTransform)x.transform))).ToArray())));
        var choices=build.GetComponentsInChildren<Button>(true).Where(x=>x.name.StartsWith("HUD Build Choice ",StringComparison.Ordinal)).Select(x=>x.name).ToArray();
        var actualTierOne=city.Runtime.Game.c.v040["progression"]["tier1"].Select(x=>x.Text).ToArray();
        Check("ui5_hud_tier_one_build_choices_"+stage,actualTierOne.Length==3&&
            actualTierOne.All(id=>choices.Contains("HUD Build Choice "+id)),
            V.Map("config",V.From(actualTierOne),"controls",V.From(choices)));
        var layerCount=layers.GetComponentsInChildren<Button>(true).Count(x=>x.gameObject.activeInHierarchy);
        Check("ui5_hud_seven_map_layers_"+stage,layerCount==7,layerCount);
        var guidance=ui.Ui5EvidenceGuidance();
        Check("ui5_hud_next_action_visible_"+stage,guidance.Get("active",false).Bool&&guidance.Get("inside",false).Bool,guidance);
        CheckUi5MapSourceGeometry(stage);
    }
    void CheckUi5MapSourceGeometry(string stage){
        Canvas.ForceUpdateCanvases();
        var roots=city.Interface.GetComponentsInChildren<RectTransform>(true);
        var sourceButton=city.Interface.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(x=>x.GetComponentInChildren<Text>()?.text=="所选来源  I");
        var panel=roots.FirstOrDefault(x=>x.name=="Map source inspection");
        var overview=roots.FirstOrDefault(x=>x.name=="HUD Overview");
        var time=roots.FirstOrDefault(x=>x.name=="HUD Time Controls");
        var layers=roots.FirstOrDefault(x=>x.name=="HUD Map Layers");
        var build=roots.FirstOrDefault(x=>x.name=="HUD Construction");
        Check("ui5_source_geometry_controls_present_"+stage,
            sourceButton!=null&&panel!=null&&overview!=null&&time!=null&&layers!=null&&build!=null);
        var buttonBox=Ui5ScreenRect((RectTransform)sourceButton.transform);
        var panelBox=Ui5ScreenRect(panel);var overviewBox=Ui5ScreenRect(overview);
        var timeBox=Ui5ScreenRect(time);var layersBox=Ui5ScreenRect(layers);var buildBox=Ui5ScreenRect(build);
        var detail=V.Map("source_button",Ui5Box(buttonBox),"source_panel",Ui5Box(panelBox),
            "guidance",Ui5Box(overviewBox),"time",Ui5Box(timeBox),"layers",Ui5Box(layersBox),
            "construction",Ui5Box(buildBox),"button_active",sourceButton.gameObject.activeInHierarchy,
            "panel_active",panel.gameObject.activeInHierarchy);
        Check("ui5_source_button_clear_of_bottom_controls_"+stage,Ui5Inside(buttonBox)&&
            Ui5Separate(buttonBox,timeBox)&&Ui5Separate(buttonBox,layersBox)&&Ui5Separate(buttonBox,buildBox),detail);
        Check("ui5_source_panel_clear_of_guidance_and_docks_"+stage,Ui5Inside(panelBox)&&
            Ui5Separate(panelBox,overviewBox)&&panelBox.yMin>=timeBox.yMax+1,detail);
    }
    static V Ui5Box(Rect r)=>V.Map("x",r.x,"y",r.y,"width",r.width,"height",r.height);
    static bool Ui5OneRenderedLine(Text label,string expected)=>label!=null&&label.text==expected&&
        label.cachedTextGenerator.lineCount==1&&label.cachedTextGenerator.characterCountVisible==expected.Length;
    static V Ui5RenderedTextDetail(Text label)=>label==null?V.Map("present",false):
        V.Map("present",true,"text",label.text,"font_size",label.fontSize,
            "line_count",label.cachedTextGenerator.lineCount,
            "visible_characters",label.cachedTextGenerator.characterCountVisible,
            "rect",Ui5Box(label.rectTransform.rect));
}

/// <summary>Read-only diagnostics available only to the explicit UI5 batchmode flag.</summary>
public sealed partial class CityInterface {
    static bool Ui5EvidenceEnabled=>Environment.GetCommandLineArgs().Contains("--validate-ui5-headless")&&Application.isBatchMode;
    public string Ui5EvidencePage()=>Ui5EvidenceEnabled?_page:"";
    public V Ui5EvidenceGuidance(){
        if(!Ui5EvidenceEnabled)return V.Map();
        var button=_guidanceButton==null?null:_guidanceButton.GetComponentInChildren<Text>();
        var rect=_guidanceButton==null?Rect.zero:Ui5EvidenceRect((RectTransform)_guidanceButton.transform);
        var parent=_planningOverview==null?Rect.zero:Ui5EvidenceRect(_planningOverview);
        return V.Map("title",_guidanceTitle==null?"":_guidanceTitle.text,
            "reason",_guidanceReason==null?"":_guidanceReason.text,
            "action_label",button==null?"":button.text,
            "active",_guidanceButton!=null&&_guidanceButton.gameObject.activeInHierarchy,
            "inside",rect.width>0&&rect.height>0&&rect.xMin>=parent.xMin-1&&rect.xMax<=parent.xMax+1&&rect.yMin>=parent.yMin-1&&rect.yMax<=parent.yMax+1);
    }
    public V Ui5EvidenceConstructionIntent()=>Ui5EvidenceEnabled?
        V.Map("active",ConstructionIntentActive,"base_id",_base):V.Map();
    public V Ui5EvidenceOverview()=>Ui5EvidenceEnabled?
        V.Map("page",_page,"city_situation",_page=="overview"&&new[]{"Overview Next Action","Overview Living Summary","Overview Supply Summary","Overview Construction Summary"}
            .All(name=>_body.GetComponentsInChildren<RectTransform>(false).Any(x=>x.name==name&&x.gameObject.activeInHierarchy))):V.Map();
    public V Ui5EvidenceMilestones(){
        if(!Ui5EvidenceEnabled)return V.Map();
        var rows=_body.GetComponentsInChildren<RectTransform>(false);
        int stages=Enumerable.Range(1,3).Count(n=>rows.Any(x=>x.name=="Milestone Stage "+n&&x.gameObject.activeInHierarchy));
        var themeId=S["block"].Get("theme",V.Map()).Get("id","").Text;
        var saved=S["block"].Get("theme",V.Map()).Get("milestone_ids",V.Arr());
        bool states=Enumerable.Range(1,3).All(n=>{var row=rows.FirstOrDefault(x=>x.name=="Milestone Stage "+n);var reading=row==null?"":row.GetComponentInChildren<Text>()?.text??"";return reading.Contains(themeId!=""&&saved.Contains(themeId+":M"+n)?"已存档":themeId==""?"未开始":"尚未存档");});
        return V.Map("page",_page,"stage_count",stages,"saved_states_match",states,
            "saved_ids",S["block"].Get("theme",V.Map()).Get("milestone_ids",V.Arr()).Duplicate(true));
    }
    public V Ui5EvidenceHudFonts(){
        if(!Ui5EvidenceEnabled)return V.Map();
        var layer=_hudLayerButtons.TryGetValue("terrain",out var terrain)?terrain.GetComponentInChildren<Text>():null;
        var navigation=_topRoutes.TryGetValue("overview",out var overview)?overview.GetComponentInChildren<Text>():null;
        var pause=_pause==null?null:_pause.GetComponentInChildren<Text>();
        return V.Map("layer",layer==null?0:layer.fontSize,"navigation",navigation==null?0:navigation.fontSize,
            "pause",pause==null?0:pause.fontSize);
    }
    Rect Ui5EvidenceRect(RectTransform target){
        if(target==null)return Rect.zero;
        var corners=new Vector3[4];target.GetWorldCorners(corners);
        var points=corners.Select(x=>RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera,x)).ToArray();
        return Rect.MinMaxRect(points.Min(x=>x.x),points.Min(x=>x.y),points.Max(x=>x.x),points.Max(x=>x.y));
    }
}
}
