using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
/// <summary>Explicit native validation run in an isolated Unity save directory.</summary>
public sealed class CityEvidence : MonoBehaviour {
    readonly V checks = V.Arr(); readonly List<string> errors = new List<string>();
    readonly List<double> frames = new List<double>();
    V resourceSample = V.Map();
    readonly V growthEvidence = V.Arr();
    V geographyEvidence = V.Map(), riverPickEvidence = V.Arr();
    CityBootstrap city; string directory; bool finished;
    public static bool Requested() => Environment.GetCommandLineArgs().Any(a => a == "--validate-city" || a == "--capture-city");
    void Awake() { Application.logMessageReceived += Logged; }
    void OnDestroy() { Application.logMessageReceived -= Logged; }
    void Logged(string message, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
    }
    IEnumerator Start() {
        city = GetComponent<CityBootstrap>();
        directory = Environment.GetEnvironmentVariable("MET_UNITY_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory)) directory = Path.GetFullPath("Build/CityEvidence");
        Directory.CreateDirectory(directory);
        var sequence = Run();
        while (true) {
            object current = null; bool more = false; Exception failure = null;
            try { more = sequence.MoveNext(); if (more) current = sequence.Current; }
            catch (Exception error) { failure = error; }
            if (failure != null) { errors.Add(failure.ToString()); Complete(false); yield break; }
            if (!more) break;
            yield return current;
        }
        Complete(errors.Count == 0);
    }
    void Check(string name, bool success, object detail = null) {
        checks.Add(V.Map("name", name, "passed", success, "detail", V.From(detail)));
        if (!success) {
            var error = "City native check failed: " + name; errors.Add(error);
            Complete(false); throw new InvalidOperationException(error);
        }
    }
    Button Control(string text) => city.Interface.GetComponentsInChildren<Button>(true)
        .FirstOrDefault(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>()?.text == text);
    void Click(string text) {
        var button = Control(text); Check("active_callback_" + text, button != null && button.IsInteractable());
        button.onClick.Invoke();
    }
    void ClickPrefix(string prefix) {
        var button = city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(b =>
            b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>()?.text.StartsWith(prefix, StringComparison.Ordinal) == true);
        Check("active_callback_prefix_" + prefix, button != null && button.IsInteractable()); button.onClick.Invoke();
    }
    string GameplayState() {
        var state = city.Runtime.Game.snapshot(); state.Remove("wall_mark");
        state["beta_time"].Remove("last_seen_utc");
        return state.ToJson(false, true);
    }
    IEnumerator Run() {
        yield return null;
        Check("isolated_save_root", !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MET_UNITY_SAVE_ROOT")));
        Check("runtime_ready", city.Runtime.Ready);
        Check("world_import", city.World.Failure == "", city.World.Failure);
        Check("camera_audio_listener", city.World.GetComponentsInChildren<AudioListener>().Length == 1);
        Check("chinese_font_asset", Resources.Load<Font>("CityFonts/NotoSansSC-VF") != null);
        Check("urp_shaders_serialized", Resources.Load<Material>("Rendering/CitySurface") != null && Resources.Load<Material>("Rendering/CityOverlay") != null);
        Check("transparent_overlay_serialized", Resources.Load<Material>("Rendering/CityOverlay").IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
        var land = Resources.Load<Material>("Rendering/CityLandscapeVertex");
        var river = Resources.Load<Material>("Rendering/CityRiverVertex");
        Check("landscape_vertex_material_serialized", land != null && land.shader.name == "MET/City/LandscapeVertexPBR");
        Check("river_vertex_material_serialized", river != null && river.shader.name == "MET/City/LandscapeVertexPBR" && river.IsKeywordEnabled("_CITY_RIVER"));
        city.Runtime.NewCity(7040);
        yield return null;
        Click("在此登陆"); yield return null;
        Check("landing_callback", city.Runtime.Game.s["phase"].Text == "origin");
        Click("确立起源 · 安顿居民"); yield return null;
        Check("origin_callback", city.Runtime.Game.s["phase"].Text == "play" && !city.Interface.IsModal);
        Check("full_map", city.World.TileCount == 61, city.World.TileCount);
        Check("core_valid_after_start", city.Runtime.Game.validate_state(city.Runtime.Game.s) == "");
        checks.Add(V.Map("name", "asset_coverage", "passed", true, "detail", city.World.AssetCoverage.Duplicate(true)));
        yield return Capture("city-start.png");

        // All archive navigation invokes the same uGUI callbacks used by the player.
        city.Runtime.SetPaused(true);
        city.Interface.OpenArchive(); yield return null;
        var before = GameplayState();
        foreach (string label in new[] { "资源", "人口", "市场", "政策", "构筑", "主题", "年度 / 尾声", "记录", "设置", "总览" }) {
            Click(label); yield return null;
            Check("page_read_only_" + label, GameplayState() == before);
        }
        yield return Capture("city-archive.png");
        Click("返回地图 · 保留时间状态"); yield return null;
        Check("page_close_preserves_pause", city.Runtime.Game.s["settings"]["user_paused"].Bool);
        Check("page_close_no_offline_record", GameplayState() == before);

        var initialPosition = city.World.CameraController.WorldCamera.transform.position;
        var initialSize = city.World.CameraController.WorldCamera.orthographicSize;
        city.Runtime.SetDisplaySetting("ui_scale", .8); city.Interface.ApplyDisplay();
        Check("ui_scale_independent_camera", city.World.CameraController.WorldCamera.transform.position == initialPosition && city.World.CameraController.WorldCamera.orthographicSize == initialSize);
        city.Runtime.SetDisplaySetting("ui_scale", 1.0); city.Interface.ApplyDisplay();
        foreach (string layer in CityWorld.Layers) {
            city.Interface.SetLayer(layer); yield return null;
            Check("world_layer_" + layer, city.World.Layer == layer && city.World.Legend["city_tiles"].Long == 61);
        }
        city.Interface.SetLayer("terrain");
        Check("layers_leave_game_unchanged", GameplayState() == before);

        var g = city.Runtime.Game;
        int projectsBeforeForecast = g.s["projects"].Count;
        var tile = g.s["cells"].Keys.First(k => g.s["cells"][k]["building"].Text == "" && !g.s["cells"][k]["traits"].Contains("tile.water"));
        city.Interface.SelectTile(tile); yield return null;
        Click("比较运营者投标 ›"); yield return null;
        Check("tender_callback", g.s["draft"].Count > 0);
        Check("comparison_snapshots", city.Interface.ComparisonBaseline.Count > 0 && city.Interface.ComparisonProposal.Count > 0);
        Check("forecast_does_not_spend", g.s["projects"].Count == projectsBeforeForecast);
        yield return Capture("city-construction-comparison.png");
        var forecastState = GameplayState();
        Click("地图 · 建成后"); yield return null;
        Check("forecast_map_exposed", !city.Interface.IsModal && city.Runtime.DecisionOpen && city.Interface.MapSourcePhase == "proposal");
        Check("forecast_same_step_comparison", city.World.Legend["comparison_ready"].Bool);
        var selected = city.Interface.SelectedTile;
        city.Interface.SelectTile("0,1");
        Check("forecast_freezes_real_selection", city.Interface.SelectedTile == selected);
        Check("forecast_world_selection_rejected", !city.World.RequestSelection("0,1") && city.World.SelectedTile == selected && city.Interface.SelectedTile == selected);
        city.World.SetLayer("stock"); city.World.CycleLayer();
        Check("comparison_layer_cycle", city.World.Layer == "delta");
        Check("forecast_read_only", GameplayState() == forecastState);
        yield return Capture("city-forecast-map.png");
        Click("返回方案比较 ›"); yield return null;
        Check("forecast_return_page", city.Interface.IsModal && city.Runtime.DecisionOpen);
        Click("返回地图 · 保留时间状态"); yield return null;
        Check("forecast_cleared_on_close", city.Interface.MapSourcePhase == "committed" && !city.World.Legend["comparison_ready"].Bool && city.World.Layer != "delta");
        Check("forecast_context_restored", city.Interface.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "L2 Site Context" && r.gameObject.activeInHierarchy));
        Check("forecast_selection_restored", city.World.SelectedTile == selected && city.Interface.SelectedTile == selected);
        city.Interface.OpenArchive("tender"); yield return null;
        ClickPrefix("采用并建设 · "); yield return null;
        Check("construction_callback", g.s["projects"].Count == projectsBeforeForecast + 1 && !city.Interface.IsModal);
        var built = g.s["cells"][tile]["building"].Text;
        Check("construction_geometry", built != "" && city.World.BuildingCount == g.playable_ids().Count);

        // Simulation timing is recorded separately from renderer frame intervals.
        city.Runtime.Execute(actual => { actual.cancel_draft(); actual.s["settings"]["user_paused"] = false; Clock.set_mode(actual, "advance"); return ""; });
        var watch = Stopwatch.StartNew();
        var result = g.advance_active(60); watch.Stop();
        Check("native_simulation_advance", g.last_error == "", result);
        city.Runtime.Execute(actual => { actual.s["settings"]["user_paused"] = true; return ""; });
        checks.Add(V.Map("name", "simulation_cpu_ms", "passed", true, "detail", watch.Elapsed.TotalMilliseconds));
        Check("construction_completed", g.s["projects"][built]["status"].Text == "running");
        city.Interface.OpenArchive("themes"); yield return null;
        Click("返回地图 · 保留时间状态"); yield return null;
        city.Interface.SetLayer("terrain");
        city.Interface.OpenArchive("resources"); yield return null;
        yield return Capture("city-resource-history.png");
        Click("返回地图 · 保留时间状态"); yield return null;
        yield return FabricFixtures();
        // Detached captures restore the camera; let ordinary hover/context events settle
        // before measuring the unchanged, paused city.
        yield return new WaitForSecondsRealtime(.35f);
        var builds = city.World.GeometryBuildCount;
        city.Interface.ResetPresentationCounters();
        var process = Process.GetCurrentProcess(); process.Refresh();
        var cpuStart = process.TotalProcessorTime.TotalMilliseconds;
        var samplingTime = Stopwatch.StartNew();
        for (int i = 0; i < 300; i++) { yield return null; frames.Add(UnityEngine.Time.unscaledDeltaTime * 1000.0); }
        samplingTime.Stop(); process.Refresh();
        resourceSample = V.Map("wall_ms", samplingTime.Elapsed.TotalMilliseconds,
            "cpu_ms", process.TotalProcessorTime.TotalMilliseconds - cpuStart,
            "cpu_percent_one_core", (process.TotalProcessorTime.TotalMilliseconds - cpuStart) * 100.0 / samplingTime.Elapsed.TotalMilliseconds,
            "resident_mb", process.WorkingSet64 / (1024.0 * 1024), "application_focused", Application.isFocused,
            "frame_limit", Application.targetFrameRate, "screen", V.Arr(Screen.width, Screen.height),
            "camera_pixels", V.Arr(city.World.CameraController.WorldCamera.pixelWidth, city.World.CameraController.WorldCamera.pixelHeight),
            "presentation", city.Interface.PerformanceCounters);
        Check("bounded_default_frame_rate", Application.targetFrameRate == 60);
        Check("static_presentation_reused", city.Interface.PerformanceCounters.Get("status_rebuilds", 0).Long <= 1 && city.Interface.PerformanceCounters.Get("context_rebuilds", 0).Long <= 1, city.Interface.PerformanceCounters);
        Check("paused_geometry_reused", city.World.GeometryBuildCount == builds);
        Check("no_native_errors", errors.Count == 0, errors);
        yield return Capture("city-native-final.png");
        city.World.CameraController.ResetView();
        yield return new WaitForSecondsRealtime(.25f);
        yield return Capture("city-whole-map.png");
    }
    Dictionary<string,string> MainNodeIds() => city.World.GetComponentsInChildren<CityBuildingIdentity>()
        .ToDictionary(x => x.BuildingId, x => x.gameObject.GetEntityId().ToString(), StringComparer.Ordinal);
    Dictionary<string,string> FabricNodeIds() => city.World.GetComponentsInChildren<CityFabricIdentity>()
        .ToDictionary(x => x.VisualId, x => x.gameObject.GetEntityId().ToString(), StringComparer.Ordinal);
    Dictionary<string,string> TileNodeIds() => city.World.GetComponentsInChildren<CityTileIdentity>()
        .ToDictionary(x => x.TileId, x => x.gameObject.GetEntityId().ToString(), StringComparer.Ordinal);
    static string GeographyKey(V snapshot) {
        var cells = snapshot.Get("cells", snapshot.Get("tiles", V.Map()));
        return V.Arr(snapshot["seed"], snapshot["block"].Get("geography", V.Map()),
            V.From(cells.Keys.OrderBy(x => x, StringComparer.Ordinal).Select(id => V.Arr(id, cells[id]["terrain"],
                cells[id]["elevation01"], cells[id]["moisture01"], cells[id]["river_order"], cells[id]["building"])).ToArray())).ToJson(false, true);
    }
    V GrowthSnapshot(V actual, string[] sites, int phase) {
        var fixture = actual.Duplicate(true);
        fixture["test_injection"] = true; fixture["source_phase"] = "evidence_fixture";
        fixture["projects"] = V.Map(); fixture["groups"] = V.Map(); fixture["sites"] = V.Map();
        fixture["metrics"] = V.Map("utilization", V.Map(), "employment_links", V.Arr(), "service_links", V.Arr());
        fixture["compiled"] = V.Map("stats", V.Map());
        foreach (var cell in fixture["cells"].Values) { cell["building"] = ""; cell["site"] = ""; }
        // Actual catalog tiers/models change across these detached examples. The first home
        // keeps its main building while real hosted people and usage grow its adjacent fabric.
        string[][] definitions = {
            new[] { "homestead", "homestead", "neighborhood", "workshop", "homestead", "neighborhood", "workshop", "workshop", "neighborhood", "workshop" },
            new[] { "homestead", "residence", "public_center", "workshop", "agriculture", "medical_clinic", "primary_school", "power_station", "extraction", "yard" },
            new[] { "homestead", "advanced_residence", "commercial_center", "manufacturing", "modern_agriculture", "hospital", "secondary_school", "power_plant", "material_factory", "freight" }
        };
        var template = actual["projects"].Values.First(p => !p.Get("special", false).Bool).Duplicate(true);
        for (int i = 0; i < sites.Length; i++) {
            string id = "fixture:building:" + i, def = definitions[phase][i];
            var catalog = city.Runtime.Game.c.projects[def]; var p = template.Duplicate(true);
            p["id"] = id; p["def"] = def; p["tile"] = sites[i]; p["site"] = "fixture:site:" + i;
            p["traits"] = catalog["traits"].Duplicate(true); p["tier"] = catalog["tier"];
            p["special"] = false; p["status"] = "running"; p["paused"] = false; p["operator_org"] = "commons";
            p["level"] = 0; p["size_scale"] = 1; p["slots"] = V.Map(); p["labels"] = V.Arr();
            p["job"] = V.Map(); p["upgrade_job"] = V.Map();
            double usage = phase == 0 ? 0 : phase == 1 ? (catalog["tier"].Long == 1 ? .65 : .40) : 1;
            double occupancy = phase == 0 ? 0 : phase == 1 ? (catalog["tier"].Long == 1 ? .60 : .35) : 1;
            p["usage"] = usage; fixture["projects"][id] = p;
            var stats = catalog["stats"].Duplicate(true);
            // Deliberately supplied compiled capacity; this must take precedence over the
            // catalog fallback, and is explicitly recorded as injected evidence data.
            stats["housing"] = stats.Get("housing", 0).Double * (phase == 0 ? 1 : phase == 1 ? 1.25 : 1.5);
            stats["efficiency"] = stats.Get("efficiency", 1);
            fixture["compiled"]["stats"][id] = stats;
            fixture["metrics"]["utilization"][id] = V.Map("utilization", usage);
            fixture["cells"][sites[i]]["building"] = id;
            fixture["cells"][sites[i]]["site"] = p["site"];
            double residents = stats["housing"].Double * stats["efficiency"].Double * occupancy;
            if (residents > 0) fixture["groups"]["fixture:people:" + i] = V.Map("host", id, "count", residents);
            if (phase > 0 && i > 0) {
                fixture["metrics"]["employment_links"].Add(V.Map("group", "fixture:people:0", "target", id, "people", phase == 1 ? 6 : 18));
                fixture["metrics"]["service_links"].Add(V.Map("source", id, "home", "fixture:building:0", "people", phase == 1 ? 6 : 18));
            }
        }
        return fixture;
    }
    IEnumerator FabricFixtures() {
        var game = city.Runtime.Game; var actual = game.snapshot(); actual["compiled"] = game.compiled.Duplicate(true);
        string before = GameplayState(), rng = game.s["rng"].ToJson(false, true), compiled = game.compiled.ToJson(false, true);
        var canvases = city.Interface.GetComponentsInChildren<Canvas>(true);
        var canvasStates = canvases.Select(x => x.enabled).ToArray();
        var controller = city.World.CameraController; var camera = controller.WorldCamera;
        bool cameraControl = controller.enabled; var position = camera.transform.position; var rotation = camera.transform.rotation;
        float size = camera.orthographicSize;
        bool worldInput = city.World.enabled;
        try {
            foreach (var canvas in canvases) canvas.enabled = false;
            city.World.enabled=false;city.World.ClearHover();city.World.SelectTile("",false);
            controller.enabled = false;
            city.World.SetLayer("terrain"); city.World.SetFabricSources(game.c.projects, game.compiled["stats"]);
            city.World.Present(actual);
            var actualDiagnostics = city.World.FabricDiagnostics.Duplicate(true); var actualTiles = TileNodeIds();
            var actualPlan = city.World.FabricPlan.Duplicate(true); string geography = GeographyKey(actual);
            var forestPositions = city.World.GetComponentInChildren<CityFoliage>().CapturePlacements();
            var prediction = new Planning().snapshot(game).Duplicate(true);
            Check("planning_keeps_seeded_geography", prediction["seed"] == actual["seed"] && GeographyKey(prediction) == geography);
            city.World.Present(prediction);
            var forecastDiagnostics = city.World.FabricDiagnostics.Duplicate(true);
            Check("forecast_terrain_and_tile_nodes_retained", !forecastDiagnostics["terrain_rebuilt"].Bool &&
                actualTiles.All(x => TileNodeIds().GetValueOrDefault(x.Key) == x.Value), forecastDiagnostics);
            city.World.Present(actual);
            var restoredDiagnostics = city.World.FabricDiagnostics.Duplicate(true);
            Check("actual_forecast_actual_forest_restored", city.World.FabricPlan == actualPlan &&
                restoredDiagnostics["forest_instance_count"] == actualDiagnostics["forest_instance_count"] &&
                !restoredDiagnostics["terrain_rebuilt"].Bool, restoredDiagnostics);
            Check("actual_forecast_actual_forest_positions_restored", forestPositions.SequenceEqual(city.World.GetComponentInChildren<CityFoliage>().CapturePlacements()));
            geographyEvidence = V.Map("test_injection", false, "geography", actual["block"]["geography"].Duplicate(true),
                "actual", actualDiagnostics, "forecast", forecastDiagnostics, "restored", restoredDiagnostics,
                "forest_scope", "Forecast morphology may clear different trees; returning to the identical actual plan restores the same seeded forest count and keeps terrain/tile objects.");

            var sites = actual["cells"].Keys.Where(id => !VisualFabric.Water(actual["cells"][id]))
                .OrderBy(id => Hex.distance("0,0", id)).ThenBy(id => id, StringComparer.Ordinal).Take(10).ToArray();
            Check("growth_fixture_ten_neighboring_catalog_sites", sites.Length == 10 && sites.Any(id => Hex.neighbors(id).Any(n => sites.Contains(n.Text))));
            var center = sites.Select(CityWorld.TilePosition).Aggregate(Vector3.zero, (a, b) => a + b) / sites.Length;
            var direction = new Vector3(-9, 15.3f, 12).normalized;
            camera.transform.position = center + direction * 27; camera.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
            camera.orthographicSize = 6.2f;
            Dictionary<string,string> previousMain = null, previousFabric = null; int previousFillers = -1;
            V previousFixture = null;
            string[] phases = { "early", "middle", "late" };
            for (int phase = 0; phase < phases.Length; phase++) {
                var fixture = GrowthSnapshot(actual, sites, phase); string sourceBefore = fixture.ToJson(false, true);
                city.World.SetFabricSources(game.c.projects, fixture["compiled"]["stats"]); city.World.Present(fixture);
                var diagnostics = city.World.FabricDiagnostics.Duplicate(true); var plan = city.World.FabricPlan.Duplicate(true);
                var mainIds = MainNodeIds(); var fabricIds = FabricNodeIds();
                Check("growth_" + phases[phase] + "_read_only", fixture.ToJson(false, true) == sourceBefore && GameplayState() == before &&
                    game.s["rng"].ToJson(false, true) == rng && game.compiled.ToJson(false, true) == compiled);
                Check("growth_" + phases[phase] + "_ten_catalog_main_buildings", city.World.BuildingCount == 10 && mainIds.Count == 10);
                int stage = (int)plan["parcels"]["parcel:" + sites[0]]["stage"].Long;
                Check("growth_" + phases[phase] + "_source_stage", phase == 0 ? stage == 0 : phase == 1 ? stage == 2 : stage >= 4, stage);
                Check("growth_" + phases[phase] + "_infill_increases", diagnostics["filler_count"].Long > previousFillers, diagnostics);
                Check("growth_" + phases[phase] + "_compiled_capacity", Math.Abs(plan["levels"]["fixture:building:0"]["housing_capacity"].Double -
                    fixture["compiled"]["stats"]["fixture:building:0"]["housing"].Double * fixture["compiled"]["stats"]["fixture:building:0"]["efficiency"].Double) < .000001);
                if (phase > 0) {
                    var unchanged = fixture["projects"].Keys.Where(id => fixture["projects"][id]["def"] == previousFixture["projects"][id]["def"]).ToArray();
                    var replaced = fixture["projects"].Keys.Where(id => fixture["projects"][id]["def"] != previousFixture["projects"][id]["def"]).ToArray();
                    Check("growth_" + phases[phase] + "_unaffected_main_retained", unchanged.Length > 0 && unchanged.All(id => mainIds[id] == previousMain[id]), V.From(unchanged));
                    Check("growth_" + phases[phase] + "_catalog_upgrade_main_updated", replaced.Length > 0 && replaced.All(id => mainIds[id] != previousMain[id]), V.From(replaced));
                    Check("growth_" + phases[phase] + "_affected_parcel_updated", fabricIds["parcel:" + sites[0]] != previousFabric["parcel:" + sites[0]]);
                    Check("growth_" + phases[phase] + "_same_terrain_retained", !diagnostics["terrain_rebuilt"].Bool);
                }
                previousFillers = (int)diagnostics["filler_count"].Long; previousMain = mainIds; previousFabric = fabricIds; previousFixture = fixture;
                city.World.Present(fixture);
                Check("growth_" + phases[phase] + "_repeat_zero_scene_work", city.World.FabricDiagnostics["scene_work"].Long == 0 &&
                    !city.World.FabricDiagnostics["terrain_rebuilt"].Bool && mainIds.All(x => MainNodeIds()[x.Key] == x.Value), city.World.FabricDiagnostics);
                Check("visible_vertex_terrain_" + phases[phase], city.World.GetComponentsInChildren<MeshRenderer>().Any(r =>
                    r.name == "OnlyVisibleLandscapeSurface_NoExtraGameplay" && r.enabled && r.sharedMaterial.shader.name == "MET/City/LandscapeVertexPBR"));
                growthEvidence.Add(V.Map("phase", phases[phase], "test_injection", true, "hud_hidden", true,
                    "screenshot", "city-growth-" + phases[phase] + ".png", "seed", fixture["seed"], "geography", fixture["block"]["geography"].Duplicate(true),
                    "injected_source_fields", V.Arr("projects: catalog definition, tier, traits, running status, commons operator, usage", "groups: host and count", "compiled.stats: catalog stats with explicit housing multipliers 1/1.25/1.5", "metrics.utilization, employment_links and service_links"),
                    "source_projects", fixture["projects"].Duplicate(true), "source_groups", fixture["groups"].Duplicate(true),
                    "source_metrics", fixture["metrics"].Duplicate(true), "source_compiled", fixture["compiled"].Duplicate(true),
                    "main_instance_ids", V.From(mainIds), "fabric_instance_ids", V.From(fabricIds), "plan", plan, "changes", diagnostics,
                    "repeat", city.World.FabricDiagnostics.Duplicate(true), "scope", "Detached illustration of source-driven growth; no played chronology, save or live gameplay entities were changed."));
                yield return Capture("city-growth-" + phases[phase] + ".png");
                if(phase==2) {
                    var matureFrames=new List<double>();var process=Process.GetCurrentProcess();process.Refresh();
                    double cpu=process.TotalProcessorTime.TotalMilliseconds;var watch=Stopwatch.StartNew();
                    for(int frame=0;frame<120;frame++){yield return null;matureFrames.Add(UnityEngine.Time.unscaledDeltaTime*1000.0);}
                    watch.Stop();process.Refresh();matureFrames.Sort();
                    growthEvidence[growthEvidence.Count-1]["renderer_sample"]=V.Map("samples",120,"wall_ms",watch.Elapsed.TotalMilliseconds,
                        "median_frame_ms",matureFrames[60],"p95_frame_ms",matureFrames[114],"cpu_percent_one_core",(process.TotalProcessorTime.TotalMilliseconds-cpu)*100/watch.Elapsed.TotalMilliseconds,
                        "resident_mb",process.WorkingSet64/(1024.0*1024),"application_focused",Application.isFocused,"frame_limit",Application.targetFrameRate,
                        "scope","Detached 10 main buildings and rendered infill; HUD/input disabled. Renderer fixture, not played large-city acceptance.");
                    Check("grown_renderer_keeps_geometry",city.World.FabricDiagnostics["scene_work"].Long==0);
                }
            }
            var pickFixture = actual.Duplicate(true); pickFixture["seed"] = 8291; pickFixture["block_index"] = 0;
            pickFixture["cells"] = Hex.make_map(0, 8291); pickFixture["block"]["geography"] = Hex.geography(pickFixture["cells"], 8291, 0);
            pickFixture["projects"] = V.Map(); pickFixture["groups"] = V.Map(); pickFixture["metrics"] = V.Map(); pickFixture["compiled"] = V.Map("stats", V.Map());
            foreach (var cell in pickFixture["cells"].Values) cell["building"] = "";
            city.World.SetFabricSources(game.c.projects, V.Map()); city.World.Present(pickFixture);
            camera.transform.position = direction * 27; camera.orthographicSize = 8.75f;
            var cells = pickFixture["cells"];
            // Probe both sides of actual water-water shared edges, where the visible
            // sloping river used to project into the adjacent tile's lower collider.
            foreach (string a in cells.Keys.Where(id => VisualFabric.Water(cells[id])).OrderBy(id => id, StringComparer.Ordinal)) {
                foreach (var neighbor in Hex.neighbors(a)) {
                    string b = neighbor.Text;
                    if (!cells.Has(b) || !VisualFabric.Water(cells[b]) || StringComparer.Ordinal.Compare(a, b) >= 0) continue;
                    foreach (float fraction in new[] { .46f, .54f }) {
                        var point = Vector3.Lerp(CityWorld.TilePosition(a), CityWorld.TilePosition(b), fraction);
                        point.y = city.World.VisualSurfaceHeight(point); var screen = camera.WorldToScreenPoint(point);
                        string expected = fraction < .5f ? a : b, picked = city.World.PickTile(new Vector2(screen.x, screen.y));
                        var row = V.Map("seed", 8291, "landing", 0, "test_injection", true, "edge", V.Arr(a, b), "fraction", fraction,
                            "world", V.Arr(point.x, point.y, point.z), "screen", V.Arr(screen.x, screen.y), "expected", expected, "picked", picked);
                        riverPickEvidence.Add(row); Check("visible_river_boundary_pick_" + a + "_" + b + "_" + fraction, picked == expected, row);
                    }
                }
            }
            Check("river_boundary_pick_samples", riverPickEvidence.Count >= 8, riverPickEvidence.Count);
            pickFixture["block_index"]=1;pickFixture["cells"]=Hex.make_map(1,8291);pickFixture["block"]["geography"]=Hex.geography(pickFixture["cells"],8291,1);
            city.World.Present(pickFixture);city.World.ClearHover();city.World.SelectTile("",false);
            yield return Capture("city-seeded-hills.png");
            Check("all_fixtures_preserve_actual_game_and_rng", GameplayState() == before && game.s["rng"].ToJson(false, true) == rng && game.compiled.ToJson(false, true) == compiled);
        } finally {
            city.World.SetFabricSources(game.c.projects, game.compiled["stats"]); city.World.Present(actual);
            city.World.SetSourcePhase(city.Interface.MapSourcePhase);
            camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size; controller.enabled = cameraControl;
            for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = canvasStates[i];
            city.World.SelectTile(city.Interface.SelectedTile,false);city.World.enabled=worldInput;
        }
        Check("growth_fixture_actual_restored", city.World.BuildingCount == game.playable_ids().Count && GameplayState() == before);
    }
    IEnumerator Capture(string name) {
        yield return new WaitForEndOfFrame();
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        try {
            Check("capture_size_" + name, image != null && image.width == 1920 && image.height == 1080,
                image == null ? "null" : image.width + "x" + image.height);
            var bytes = ImageConversion.EncodeToPNG(image); Check("capture_pixels_" + name, bytes != null && bytes.Length > 1000);
            File.WriteAllBytes(Path.Combine(directory, name), bytes);
        } finally { if (image != null) Destroy(image); }
    }
    void Complete(bool success) {
        if (finished) return; finished = true;
        frames.Sort();
        var report = V.Map("status", success ? "passed" : "failed", "unity_version", Application.unityVersion, "application_version", Application.version,
            "platform", Application.platform.ToString(), "graphics_device", SystemInfo.graphicsDeviceName,
            "graphics_api", SystemInfo.graphicsDeviceType.ToString(), "resolution", V.Arr(Screen.width, Screen.height),
            "checks", checks, "errors", V.From(errors), "frame_intervals_ms", V.Map("samples", frames.Count,
                "median", frames.Count > 0 ? frames[frames.Count / 2] : 0,
                "p95", frames.Count > 0 ? frames[Math.Min(frames.Count - 1, (int)(frames.Count * .95))] : 0),
            "resource_sample", resourceSample,
            "growth_fixtures", growthEvidence, "forecast_geography", geographyEvidence, "visible_river_boundary_picks", riverPickEvidence,
            "measurement_scope", "Paused native renderer frame intervals and process CPU/RSS; simulation CPU separately measured. Automated uGUI callbacks, not human play acceptance.");
        File.WriteAllText(Path.Combine(directory, "city-native-validation.json"), report.ToJson(true));
        UnityEngine.Debug.Log("MET City native evidence: " + report["status"].Text);
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(success ? 0 : 2);
        #else
        Application.Quit(success ? 0 : 2);
        #endif
    }
}
}
