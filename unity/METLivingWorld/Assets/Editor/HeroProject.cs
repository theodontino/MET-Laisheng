using System;
using System.IO;
using MET.LivingWorld;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MET.LivingWorld.Editor
{
    // All scene/pipeline generation uses Unity and URP's own asset APIs.
    [InitializeOnLoad]
    public static class HeroProject
    {
        public const string ScenePath = "Assets/Scenes/HeroValley.unity";
        private const string PipelinePath = "Assets/Settings/HeroValleyURP.asset";
        private const string RendererPath = "Assets/Settings/HeroValleyRenderer.asset";
        private const string PackagePath = "Packages/com.unity.render-pipelines.universal";
        private const string RebuildAfterReload = "MET.Hero.RebuildAfterURPReload";
        // Captured before any of our code changes render settings. A glTFast cache
        // initialized in a Built-in domain cannot be repaired by force-reimport.
        private static readonly bool UrpAtDomainStart = IsURPConfigured();

        static HeroProject()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                try
                {
                    if (!UrpAtDomainStart || !IsURPConfigured())
                    {
                        PrepareURPAssets();
                        SessionState.SetBool(RebuildAfterReload, true);
                        EditorUtility.RequestScriptReload();
                        return;
                    }
                    if (SessionState.GetBool(RebuildAfterReload, false) || !File.Exists(ScenePath))
                    {
                        SessionState.SetBool(RebuildAfterReload, false);
                        GenerateScene();
                    }
                }
                catch (Exception error) { Debug.LogException(error); }
            };
        }

        [MenuItem("MET/Hero Valley/Rebuild scene and URP settings")]
        public static void RebuildSceneAndSettings()
        {
            PrepareURPAssets();
            SessionState.SetBool(RebuildAfterReload, true);
            EditorUtility.RequestScriptReload();
        }

        // Batch phase one: save URP before a new process imports the GLBs.
        public static void PrepareURP()
        {
            try
            {
                PrepareURPAssets();
                if (Application.isBatchMode) EditorApplication.Exit(0);
                else { SessionState.SetBool(RebuildAfterReload, true); EditorUtility.RequestScriptReload(); }
            }
            catch (Exception error) { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(2); else throw; }
        }

        private static void PrepareURPAssets()
        {
            RequirePinnedEditor();
            Directory.CreateDirectory("Assets/Settings");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Resources/Rendering");
            AssetDatabase.Refresh();
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                ResourceReloader.ReloadAllNullIn(renderer, PackagePath);
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    PackagePath + "/Runtime/Data/PostProcessData.asset");
                HeroContract.Require(renderer.postProcessData != null, "The official URP post-processing resources are missing.");
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 40;
            pipeline.shadowCascadeCount = 2;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            var previousQuality = QualitySettings.GetQualityLevel();
            for (var index = 0; index < QualitySettings.names.Length; ++index)
            {
                QualitySettings.SetQualityLevel(index, false);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.vSyncCount = 0;
            }
            QualitySettings.SetQualityLevel(previousQuality, false);
            KeepInstancingVariants();
            const string overlayPath = "Assets/Resources/Rendering/HeroOverlay.mat";
            var overlay = AssetDatabase.LoadAssetAtPath<Material>(overlayPath);
            if (overlay == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                HeroContract.Require(shader != null, "Official URP Unlit shader is missing.");
                overlay = new Material(shader) { name = "HeroOverlay" };
                overlay.SetColor("_BaseColor", new Color(.9f, .88f, .62f, 1));
                AssetDatabase.CreateAsset(overlay, overlayPath);
            }
            PlayerSettings.companyName = "MET";
            PlayerSettings.productName = "MET Living World · Unity migration";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            if (Array.IndexOf(defines.Split(';'), "GLTFAST_KEEP_MESH_DATA") < 0)
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone,
                    string.IsNullOrEmpty(defines) ? "GLTFAST_KEEP_MESH_DATA" : defines + ";GLTFAST_KEEP_MESH_DATA");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 2); // macOS x64 + arm64 universal
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            Debug.Log("MET URP settings saved. Importing glTF requires a new URP-configured domain/process.");
        }

        public static void GenerateScene()
        {
            RequirePinnedEditor();
            HeroContract.Require(UrpAtDomainStart && IsURPConfigured(),
                "First run MET.LivingWorld.Editor.HeroProject.PrepareURP, then restart the Editor/process before generating or validating. " +
                "The glTFast material-generator cache must start in an URP domain.");
            RequireImportedResources();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("MET_ReadOnlyHeroValley");
            root.AddComponent<HeroWorld>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("MET Hero scene prepared with official URP/glTFast. Runtime evidence still requires Play/Player execution.");
        }

        [MenuItem("MET/Hero Valley/Validate imported geometry")]
        public static void ValidateImportedGeometry()
        {
            RequirePinnedEditor();
            GenerateScene();
            var root = UnityEngine.Object.FindFirstObjectByType<HeroWorld>();
            root.Load();
            var report = HeroValidation.Validate(root);
            var directory = HeroEvidence.EvidenceDirectory();
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "unity-editor-import-validation.json"), JsonUtility.ToJson(report, true));
            // Reload the clean generated scene; don't serialize transient runtime geometry into it.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            HeroContract.Require(report.status == "passed", "Imported geometry validation failed; see evidence JSON.");
            Debug.Log("MET imported geometry validated: " + report.checks_passed + " checks. No screenshot/frame claim is implied.");
        }

        public static void ValidateBatch()
        {
            try { ValidateImportedGeometry(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(2); }
        }

        public static void RunHeroValidation()
        {
            try
            {
                GenerateScene();
                HeroContract.Require(HeroEvidence.Requested(),
                    "Pass --validate-hero, --capture-hero or --measure-hero for a terminating Play Mode run.");
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(2); }
        }

        [MenuItem("MET/Hero Valley/Build macOS universal app")]
        public static void BuildHeroMac()
        {
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
                {
                    HeroContract.Require(!Application.isBatchMode,
                        "Batch macOS builds must start Unity with -buildTarget OSXUniversal; switching target in batchmode is unsupported.");
                    HeroContract.Require(EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone,
                        BuildTarget.StandaloneOSX), "macOS support must be installed before building.");
                }
                ValidateImportedGeometry();
                var output = Environment.GetEnvironmentVariable("MET_UNITY_MAC_OUTPUT");
                if (string.IsNullOrEmpty(output)) output = "Build/MET Living World Unity.app";
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { ScenePath }, locationPathName = output,
                    target = BuildTarget.StandaloneOSX, options = BuildOptions.None
                });
                HeroContract.Require(result.summary.result == BuildResult.Succeeded,
                    "Unity macOS build did not succeed: " + result.summary.result);
                Debug.Log("macOS app built at " + output + "; actual Mac launch/signing evidence is separate.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(2); else throw; }
        }

        private static void RequirePinnedEditor()
        {
            HeroContract.Require(Application.unityVersion == "6000.0.58f2",
                "This comparison is pinned to Unity 6000.0.58f2; do not silently upgrade the Editor.");
        }

        private static void RequireImportedResources()
        {
            var force = ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport;
            AssetDatabase.ImportAsset("Assets/Resources/Hero/world.glb", force);
            AssetDatabase.ImportAsset("Assets/Resources/Hero/foliage.glb", force);
            HeroContract.Require(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Hero/world.glb") != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Hero/foliage.glb") != null &&
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Hero/hero_valley.json") != null,
                "The exported Hero fixture is missing or the official glTFast ScriptedImporter has not produced prefabs.");
        }

        private static bool IsURPConfigured()
        {
            return GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset &&
                (QualitySettings.renderPipeline == null || QualitySettings.renderPipeline is UniversalRenderPipelineAsset);
        }

        private static void KeepInstancingVariants()
        {
            UnityEngine.Object settings = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset"))
                if (asset is GraphicsSettings) { settings = asset; break; }
            HeroContract.Require(settings != null, "Unity GraphicsSettings asset is unavailable.");
            var serialized = new SerializedObject(settings);
            var property = serialized.FindProperty("m_InstancingStripping");
            HeroContract.Require(property != null && property.propertyType == SerializedPropertyType.Enum,
                "Pinned Unity GraphicsSettings instancing-stripping setting is unavailable.");
            var found = false;
            for (var index = 0; index < property.enumNames.Length; ++index)
                if (property.enumNames[index] == "KeepAll" || property.enumDisplayNames[index] == "Keep All")
                { property.enumValueIndex = index; found = true; break; }
            HeroContract.Require(found, "Unity no longer exposes the Keep All instancing variant option.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
