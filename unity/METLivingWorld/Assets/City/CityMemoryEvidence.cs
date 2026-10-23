using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.Profiling;

namespace MET.City {
/// <summary>Clean saved-world memory sampling, independent of walkthroughs and captures.</summary>
public sealed class CityMemoryEvidence : MonoBehaviour {
    readonly V phases = V.Arr();
    readonly List<string> errors = new List<string>();
    CityBootstrap city;
    public static bool Requested() => Environment.GetCommandLineArgs().Contains("--profile-city-memory");
    void Awake() { Application.logMessageReceived += Logged; }
    void OnDestroy() { Application.logMessageReceived -= Logged; }
    void Logged(string text, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack);
    }
    IEnumerator Start() {
        city = GetComponent<CityBootstrap>();
        var sequences = new Stack<IEnumerator>();
        sequences.Push(Run());
        while (sequences.Count > 0) {
            var sequence = sequences.Peek();
            object current = null; bool more = false;
            try { more = sequence.MoveNext(); if (more) current = sequence.Current; }
            catch (Exception error) { errors.Add(error.ToString()); break; }
            if (!more) { sequences.Pop(); continue; }
            if (current is IEnumerator nested) { sequences.Push(nested); continue; }
            yield return current;
        }
        Complete();
    }
    static void Require(bool success, string message) { if (!success) throw new InvalidOperationException(message); }
    IEnumerator Run() {
        yield return null;
        Require(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MET_UNITY_SAVE_ROOT")), "Use an isolated copied save root for memory profiling.");
        Require(city.Runtime.Ready && city.World.Failure == "", "The saved city and renderer must load successfully.");
        Require(File.Exists(city.Runtime.SavePath) && city.Runtime.Session.Games.Count == 4, "Profile requires an existing four-Block city save.");
        Require(Screen.width == 1920 && Screen.height == 1080 && city.Runtime.EffectiveFrameLimit == 60, "Profile requires the fullscreen 1920x1080, 60 FPS baseline.");
        city.Runtime.SetPaused(true);
        yield return new WaitForSecondsRealtime(2);
        phases.Add(Memory("startup_loaded"));
        var pausedWrites = city.Runtime.AutoSaveWriteCount;
        var pausedSkips = city.Runtime.AutoSaveSkipCount;
        yield return Sample("paused_10_seconds", 10);
        Require(city.Runtime.AutoSaveWriteCount == pausedWrites && city.Runtime.AutoSaveSkipCount > pausedSkips,
            "An unchanged paused city must skip redundant automatic saves.");
        Require(!city.Runtime.PendingDecisions && !city.Runtime.MandatoryPending, "Clear saved mandatory decisions before preparing the profiling copy.");
        city.Runtime.ResumeNormal();
        Require(city.Runtime.CanRun, "The loaded city cannot start its actual normal clock.");
        var before = city.Runtime.Session.Games.ToDictionary(p => p.Key, p => p.Value.s["local_hours"].Double);
        var activeWrites = city.Runtime.AutoSaveWriteCount;
        yield return Sample("active_normal_30_seconds", 30, true);
        Require(city.Runtime.AutoSaveWriteCount > activeWrites, "An advancing city must still save automatically.");
        city.Runtime.SetPaused(true);
        Require(before.All(p => city.Runtime.Session.Games[p.Key].s["local_hours"].Double > p.Value), "Every loaded Block must actually advance during the active sample.");
        Require(city.Runtime.Session.Validate() == "", "The city must remain valid after its active sample.");
        // Deliberately outside ordinary-play measurement: identify reclaimable test
        // and allocator memory without making collection part of game performance.
        yield return null;
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        phases.Add(Memory("diagnostic_after_managed_collection"));
        yield return Resources.UnloadUnusedAssets();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        yield return new WaitForSecondsRealtime(2);
        phases.Add(Memory("diagnostic_after_unused_assets_and_collection"));
    }
    IEnumerator Sample(string label, double seconds, bool active = false) {
        var beginning = Memory(label + "_start");
        var frames = new List<double>();
        var watch = Stopwatch.StartNew();
        using (var process = Process.GetCurrentProcess()) {
            process.Refresh(); var cpu = process.TotalProcessorTime.TotalMilliseconds;
            while (watch.Elapsed.TotalSeconds < seconds) {
                yield return null;
                Require(Application.isFocused, "The memory sample lost Player focus.");
                Require(!active || city.Runtime.CanRun, "The normal clock stopped during the active memory sample.");
                frames.Add(UnityEngine.Time.unscaledDeltaTime * 1000.0);
            }
            watch.Stop(); process.Refresh();
            Require(Application.isFocused, "The memory sample lost Player focus.");
            var cpuPercent = (process.TotalProcessorTime.TotalMilliseconds - cpu) * 100 / watch.Elapsed.TotalMilliseconds;
            frames.Sort();
            phases.Add(V.Map("phase", label, "duration_seconds", watch.Elapsed.TotalSeconds, "frames", frames.Count,
                "median_frame_ms", frames[frames.Count / 2], "p95_frame_ms", frames[(int)(frames.Count * .95)],
                "cpu_percent_one_core", cpuPercent, "start", beginning, "end", Memory(label + "_end")));
        }
    }
    V Memory(string label) {
        using (var process = Process.GetCurrentProcess()) {
            process.Refresh();
            return V.Map("phase", label, "resident_bytes", process.WorkingSet64,
                "gc_managed_bytes", GC.GetTotalMemory(false), "mono_used_bytes", Profiler.GetMonoUsedSizeLong(),
                "mono_heap_bytes", Profiler.GetMonoHeapSizeLong(), "unity_allocated_bytes", Profiler.GetTotalAllocatedMemoryLong(),
                "unity_reserved_bytes", Profiler.GetTotalReservedMemoryLong(), "unity_unused_reserved_bytes", Profiler.GetTotalUnusedReservedMemoryLong(),
                "frame_limit", Application.targetFrameRate, "focused", Application.isFocused,
                "auto_save_writes", city.Runtime.AutoSaveWriteCount, "auto_save_skips", city.Runtime.AutoSaveSkipCount,
                "native_objects", V.Arr(Objects<Mesh>(), Objects<Texture2D>(), Objects<RenderTexture>(), Objects<Material>(), Objects<Font>()),
                "world_asset_coverage", city.World.AssetCoverage.Duplicate(true), "world_fabric", city.World.FabricDiagnostics.Duplicate(true),
                "nature", city.World.NatureDiagnostics,
                "ui", city.Interface.PerformanceCounters);
        }
    }
    static V Objects<T>() where T : UnityEngine.Object {
        var objects = Resources.FindObjectsOfTypeAll<T>();
        var entries = new List<Tuple<long, string, string>>();
        long total = 0; var ids = new HashSet<T>();
        foreach (var item in objects) {
            if (item == null || !ids.Add(item)) continue;
            long bytes = Profiler.GetRuntimeMemorySizeLong(item); total += bytes;
            string details = item is Mesh mesh ? "vertices=" + mesh.vertexCount + ";readable=" + mesh.isReadable :
                item is Texture texture ? texture.width + "x" + texture.height : "";
            entries.Add(Tuple.Create(bytes, item.name, details));
        }
        return V.Map("type", typeof(T).Name, "count", entries.Count, "native_bytes", total,
            "size_api_nonzero", entries.Any(x => x.Item1 > 0), "top", V.From(entries.OrderByDescending(x => x.Item1).Take(10)
                .Select(x => V.Map("name", x.Item2, "bytes", x.Item1, "details", x.Item3)).ToArray()));
    }
    void Complete() {
        var directory = Environment.GetEnvironmentVariable("MET_UNITY_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory)) directory = Path.GetFullPath("Build/CityMemoryEvidence");
        Directory.CreateDirectory(directory);
        var report = V.Map("status", errors.Count == 0 ? "passed" : "failed", "unity_version", Application.unityVersion,
            "application_version", Application.version, "platform", Application.platform.ToString(), "graphics_device", SystemInfo.graphicsDeviceName,
            "graphics_api", SystemInfo.graphicsDeviceType.ToString(), "resolution", V.Arr(Screen.width, Screen.height),
            "world_blocks", city.Runtime.Session?.Games.Count ?? 0, "phases", phases, "errors", V.From(errors),
            "scope", "Fresh Player directly loads a copied four-Block save; no fixtures, earned journey, archive navigation or screenshots. Paused 10 seconds, actual normal clock 30 seconds. Collection and asset unloading are separate diagnostics.",
            "memory_notes", "RSS includes runtime/driver/allocator overhead. Mono used includes uncollected objects. Native object sizes are API estimates and zero may indicate unavailable metrics; category sums are not process RSS.");
        File.WriteAllText(Path.Combine(directory, "city-memory-profile.json"), report.ToJson(true));
        UnityEngine.Debug.Log("MET City clean memory profile: " + report["status"].Text);
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(errors.Count == 0 ? 0 : 2);
        #else
        Application.Quit(errors.Count == 0 ? 0 : 2);
        #endif
    }
}
}
