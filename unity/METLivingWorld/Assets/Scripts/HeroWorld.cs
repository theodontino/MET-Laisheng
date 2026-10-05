using System;
using System.Diagnostics;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MET.LivingWorld
{
    public sealed class HeroWorld : MonoBehaviour
    {
        public const int GroundLayer = 8;
        public HeroContract Contract { get; private set; }
        public GameObject Geometry { get; private set; }
        public HeroFoliageRenderer Foliage { get; private set; }
        public HeroCameraController CameraController { get; private set; }
        public string SidecarSha256 { get; private set; }
        public int GeometryBuildCount { get; private set; }
        public double InitializationWallMilliseconds { get; private set; }
        public string Failure { get; private set; }
        private TextAsset immutableSidecar;

        private void OnGUI()
        {
            if (Failure != null)
                GUI.Box(new Rect(20, 20, Mathf.Max(800, Screen.width - 40), 240),
                    "Hero Valley initialization failed. This is not a fallback scene.\n" + Failure);
        }

        private void Awake()
        {
            try { Load(); }
            catch (Exception error)
            {
                Failure = error.ToString();
                UnityEngine.Debug.LogException(error);
                if (HeroEvidence.Requested())
                    HeroEvidence.FailStartup(this, error);
            }
        }

        public void Load()
        {
            HeroContract.Require(GeometryBuildCount == 0, "Hero world is loaded exactly once.");
            var stopwatch = Stopwatch.StartNew();
            var pipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
            HeroContract.Require(pipeline is UniversalRenderPipelineAsset,
                "Hero Valley requires the configured URP pipeline; no silent renderer fallback.");
            immutableSidecar = Resources.Load<TextAsset>("Hero/hero_valley");
            var importedWorld = Resources.Load<GameObject>("Hero/world");
            var importedTree = Resources.Load<GameObject>("Hero/foliage");
            HeroContract.Require(immutableSidecar != null && importedWorld != null && importedTree != null,
                "Import Hero/hero_valley.json, world.glb and foliage.glb through the official glTFast Editor importer first.");
            SidecarSha256 = HeroContract.Sha256(immutableSidecar.text);
            Contract = JsonConvert.DeserializeObject<HeroContract>(immutableSidecar.text,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });
            HeroContract.Require(Contract != null, "Hero sidecar is not valid JSON.");
            Contract.Validate();
            Geometry = Instantiate(importedWorld, transform, false);
            Geometry.name = "CommittedHeroGeometry";
            foreach (var filter in Geometry.GetComponentsInChildren<MeshFilter>(true))
            {
                HeroContract.Require(filter.sharedMesh != null && filter.sharedMesh.isReadable,
                    "World geometry must be imported readable for actual mesh validation.");
                if (!IsGroundGeometry(filter.transform)) continue;
                filter.gameObject.layer = GroundLayer;
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
            var treeRoot = new GameObject("InstancedValleyForest");
            treeRoot.transform.SetParent(transform, false);
            Foliage = treeRoot.AddComponent<HeroFoliageRenderer>();
            Foliage.Initialize(importedTree, Contract.foliage);
            CreateCameraAndEnvironment();
            var interaction = gameObject.AddComponent<HeroInteraction>();
            interaction.Initialize(this);
            GeometryBuildCount++;
            Physics.SyncTransforms();
            stopwatch.Stop();
            InitializationWallMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            if (HeroEvidence.Requested())
                gameObject.AddComponent<HeroEvidence>().Initialize(this);
        }

        public bool SidecarUnchanged()
        {
            return immutableSidecar != null && SidecarSha256 == HeroContract.Sha256(immutableSidecar.text);
        }

        public static bool IsGroundGeometry(Transform node)
        {
            for (var current = node; current != null; current = current.parent)
            {
                if (current.name == "TerrainMesh" ||
                    (current.name.StartsWith("Foundation_", StringComparison.Ordinal) &&
                        current.name.EndsWith("_platform_triangles", StringComparison.Ordinal)))
                    return true;
            }
            return false;
        }

        private void CreateCameraAndEnvironment()
        {
            var data = Contract.environment;
            var srgb = data.color_space != "linear";
            var cameraObject = new GameObject("MiniatureCamera");
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CoordinateConvention.Color(data.background, srgb);
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            CameraController = cameraObject.AddComponent<HeroCameraController>();
            CameraController.Initialize(Contract.camera);
            var sunObject = new GameObject("SharedFixedDaylight");
            sunObject.transform.SetParent(transform, false);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = CoordinateConvention.Color(data.sun_color, srgb);
            sun.intensity = data.sun_intensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .8f;
            var direction = CoordinateConvention.Vector(data.sun_direction).normalized;
            HeroContract.Require(direction.sqrMagnitude > .9f, "Sun ray direction is invalid.");
            sun.transform.rotation = Quaternion.LookRotation(direction,
                Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > .99f ? Vector3.forward : Vector3.up);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = CoordinateConvention.Color(data.ambient_color, srgb);
            RenderSettings.ambientIntensity = data.ambient_intensity;
            RenderSettings.fog = data.fog_density > 0;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = data.fog_density;
            RenderSettings.fogColor = camera.backgroundColor;
            var volumeObject = new GameObject("FixedDaylightPostProcessing");
            volumeObject.transform.SetParent(transform, false);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);
            volume.profile = profile;
        }
    }
}
