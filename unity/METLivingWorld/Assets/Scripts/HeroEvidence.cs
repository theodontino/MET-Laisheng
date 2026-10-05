using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MET.LivingWorld
{
    [Serializable] public sealed class HeroFrameEvidence
    {
        public string scope = "Wall intervals between Unity rendered frames, including engine/driver/scheduling; not GPU-only time.";
        public string simulation_scope = "None. This Unity project consumes an exported visual fixture and has no Gameplay Simulation.";
        public string device, backend;
        public int frames, width, height;
        public double median_ms, p95_ms, maximum_ms;
        public bool geometry_built_once, source_unchanged;
    }

    public sealed class HeroEvidence : MonoBehaviour
    {
        private HeroWorld world;
        private static string runDirectory;
        public void Initialize(HeroWorld value) { world = value; }

        // Step a nested iterator to catch encode/capture/IO exceptions without
        // putting a C# yield statement inside a catch block.
        private IEnumerator Start()
        {
            var sequence = Run();
            while (true)
            {
                var moved = false;
                object next = null;
                Exception failure = null;
                try { moved = sequence.MoveNext(); if (moved) next = sequence.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null) { FailStartup(world, failure); yield break; }
                if (!moved) yield break;
                yield return next;
            }
        }

        private IEnumerator Run()
        {
#if UNITY_EDITOR
            HeroContract.Require(!Application.isBatchMode || (!HasFlag("--capture-hero") && !HasFlag("--measure-hero")),
                "Editor batchmode does not run WaitForEndOfFrame. Capture/measure with a native Player or interactive Editor.");
#endif
            var directory = RunDirectory();
            WriteAtomic(Path.Combine(directory, "unity-hero-running.json"), JsonUtility.ToJson(
                new HeroValidationReport { status = "running", unity_version = Application.unityVersion }, true));
            for (var index = 0; index < 16; ++index) yield return null;
            var report = HeroValidation.Validate(world);
            HeroContract.Require(report.status == "passed", "Actual Unity geometry validation failed:\n" + JsonUtility.ToJson(report, true));
            report.status = "running";

            var camera = world.CameraController.WorldCamera;
            var sourcePosition = CoordinateConvention.Vector(world.Contract.camera.position);
            var sourceTarget = CoordinateConvention.Vector(world.Contract.camera.target);
            AddCheck(report, new HeroCheck("camera_matches_source_pose",
                Vector3.Distance(camera.transform.position, sourcePosition) < .0001f &&
                Vector3.Dot(camera.transform.forward, (sourceTarget - sourcePosition).normalized) > .99999f &&
                Mathf.Abs(camera.orthographicSize * 2 - world.Contract.camera.vertical_span) < .0001f,
                "Independent exported camera position/target/span"));
            var positionBefore = camera.transform.position;
            var spanBefore = camera.orthographicSize;
            world.CameraController.Pan(new Vector2(70, 35));
            for (var index = 0; index < 3; ++index) yield return null;
            AddCheck(report, new HeroCheck("actual_camera_pan_across_frames",
                Vector3.Distance(camera.transform.position, positionBefore) > .0001f,
                "Actual transform moved after three Update frames"));
            world.CameraController.Zoom(.9f);
            for (var index = 0; index < 3; ++index) yield return null;
            AddCheck(report, new HeroCheck("actual_camera_zoom_across_frames",
                Mathf.Abs(camera.orthographicSize - spanBefore) > .0001f,
                "Actual orthographic projection changed after three Update frames"));
            AddCheck(report, new HeroCheck("camera_changes_keep_geometry",
                world.GeometryBuildCount == 1 && world.Foliage.GeometryBuildCount == 1 && world.SidecarUnchanged() &&
                HeroValidation.GeometryFingerprint(world) == report.geometry_sha256,
                "Same actual geometry digest and immutable resource JSON after pan/zoom"));
            world.CameraController.ResetView();
            for (var index = 0; index < 2; ++index) yield return null;
            AddCheck(report, new HeroCheck("camera_reset_matches_golden",
                Vector3.Distance(camera.transform.position, sourcePosition) < .0001f &&
                Mathf.Abs(camera.orthographicSize * 2 - world.Contract.camera.vertical_span) < .0001f,
                "Reset completed before Golden Scene capture"));
            HeroContract.Require(report.checks_failed == 0, "Cross-frame interaction validation failed:\n" + JsonUtility.ToJson(report, true));

            var graphicsAvailable = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            if (HasFlag("--capture-hero"))
            {
                HeroContract.Require(graphicsAvailable && Screen.width == 1920 && Screen.height == 1080,
                    "Native 1920x1080 graphics is required for screenshot evidence.");
                yield return new WaitForEndOfFrame();
                var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                HeroContract.Require(screenshot != null && screenshot.width == 1920 && screenshot.height == 1080,
                    "Native screen capture did not return the required image.");
                byte[] bytes;
                try { bytes = screenshot.EncodeToPNG(); }
                finally { Destroy(screenshot); }
                HeroContract.Require(bytes != null && bytes.Length > 0, "Screenshot PNG encoding failed.");
                WriteAtomic(Path.Combine(directory, "unity-hero-valley.png"), bytes);
                using (var sha = SHA256.Create())
                    report.screenshot_sha256 = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
            if (HasFlag("--measure-hero"))
            {
                HeroContract.Require(graphicsAvailable, "Renderer measurements require a native graphics context.");
                var intervals = new List<double>();
                var timer = Stopwatch.StartNew();
                var last = timer.Elapsed.TotalMilliseconds;
                for (var index = 0; index < 80; ++index)
                {
                    yield return new WaitForEndOfFrame();
                    var now = timer.Elapsed.TotalMilliseconds;
                    if (index >= 8) intervals.Add(now - last);
                    last = now;
                }
                intervals.Sort();
                var frames = new HeroFrameEvidence {
                    device = SystemInfo.graphicsDeviceName, backend = SystemInfo.graphicsDeviceType.ToString(),
                    frames = intervals.Count, width = Screen.width, height = Screen.height,
                    median_ms = intervals[intervals.Count / 2],
                    p95_ms = intervals[Math.Min(intervals.Count - 1, (int)Math.Ceiling(intervals.Count * .95) - 1)],
                    maximum_ms = intervals[intervals.Count - 1],
                    geometry_built_once = world.GeometryBuildCount == 1 && world.Foliage.GeometryBuildCount == 1,
                    source_unchanged = world.SidecarUnchanged()
                };
                HeroContract.Require(frames.geometry_built_once && frames.source_unchanged,
                    "Frame measurement changed source data or rebuilt geometry.");
                WriteAtomic(Path.Combine(directory, "unity-renderer-frame-intervals.json"), JsonUtility.ToJson(frames, true));
            }
            report.status = "passed";
            File.Delete(Path.Combine(directory, "unity-hero-running.json"));
            WriteAtomic(Path.Combine(directory, "unity-hero-validation.json"), JsonUtility.ToJson(report, true));
            Quit(0);
        }

        private static void AddCheck(HeroValidationReport report, HeroCheck check)
        {
            var all = new List<HeroCheck>(report.checks) { check };
            report.checks = all.ToArray();
            if (check.passed) report.checks_passed++; else report.checks_failed++;
        }

        private static void WriteAtomic(string path, string contents)
        { WriteAtomic(path, System.Text.Encoding.UTF8.GetBytes(contents)); }
        private static void WriteAtomic(string path, byte[] bytes)
        {
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path);
        }

        public static bool HasFlag(string flag)
        {
            foreach (var argument in Environment.GetCommandLineArgs()) if (argument == flag) return true;
            return false;
        }
        public static bool Requested()
        { return HasFlag("--validate-hero") || HasFlag("--capture-hero") || HasFlag("--measure-hero"); }
        public static string EvidenceDirectory()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; ++index)
                if (arguments[index] == "--evidence-dir") return Path.GetFullPath(arguments[index + 1]);
            var configured = Environment.GetEnvironmentVariable("MET_EVIDENCE_DIR");
            return string.IsNullOrEmpty(configured) ? Path.GetFullPath("UnityHeroEvidence") : Path.GetFullPath(configured);
        }
        private static string RunDirectory()
        {
            if (runDirectory == null)
                runDirectory = Path.Combine(EvidenceDirectory(), "run-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(runDirectory);
            return runDirectory;
        }
        public static void FailStartup(HeroWorld world, Exception error)
        {
            UnityEngine.Debug.LogException(error);
            try
            {
                var directory = RunDirectory();
                WriteAtomic(Path.Combine(directory, "unity-hero-validation.json"), JsonUtility.ToJson(
                    new HeroValidationReport { status = "failed", error = error.ToString(), unity_version = Application.unityVersion }, true));
            }
            catch (Exception ioFailure) { UnityEngine.Debug.LogException(ioFailure); }
            finally { Quit(2); }
        }
        public static void Quit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
    }
}
