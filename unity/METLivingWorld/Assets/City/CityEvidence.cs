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
