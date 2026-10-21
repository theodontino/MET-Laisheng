using System;
using System.IO;
using MET.City;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MET.City.Editor {
[InitializeOnLoad]
public static class CityProject {
    public const string ScenePath = "Assets/Scenes/City.unity";
    const string PipelinePath = "Assets/Settings/CityURP.asset";
    const string RendererPath = "Assets/Settings/CityRenderer.asset";
    const string PackagePath = "Packages/com.unity.render-pipelines.universal";
    const string AfterReload = "MET.City.GenerateAfterURPReload";
    static readonly bool UrpAtDomainStart = IsURPConfigured();

    static CityProject() {
        EditorApplication.delayCall += () => {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try {
                if (!UrpAtDomainStart || !IsURPConfigured()) {
                    PrepareAssets(); SessionState.SetBool(AfterReload, true);
                    EditorUtility.RequestScriptReload(); return;
                }
                if (SessionState.GetBool(AfterReload, false) || !File.Exists(ScenePath)) {
                    SessionState.SetBool(AfterReload, false); GenerateScene();
                }
            } catch (Exception error) { Debug.LogException(error); }
        };
    }

    public static void PrepareURP() {
        try {
            PrepareAssets();
            if (Application.isBatchMode) EditorApplication.Exit(0);
            else { SessionState.SetBool(AfterReload, true); EditorUtility.RequestScriptReload(); }
        } catch (Exception error) { Fail(error); }
    }

    static void PrepareAssets() {
        RequireEditor();
        Directory.CreateDirectory("Assets/Settings");
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Resources/Rendering");
        AssetDatabase.Refresh();
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer == null) {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            ResourceReloader.ReloadAllNullIn(renderer, PackagePath);
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PackagePath + "/Runtime/Data/PostProcessData.asset");
            Require(renderer.postProcessData != null, "Official URP post-process resources are missing.");
            AssetDatabase.CreateAsset(renderer, RendererPath);
        }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null) {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }
        pipeline.supportsCameraDepthTexture = false;
        pipeline.supportsCameraOpaqueTexture = false;
        pipeline.supportsHDR = true; pipeline.msaaSampleCount = 2;
        pipeline.shadowDistance = 40; pipeline.shadowCascadeCount = 2;
        GraphicsSettings.defaultRenderPipeline = pipeline;
        var previousQuality = QualitySettings.GetQualityLevel();
        for (var i = 0; i < QualitySettings.names.Length; i++) {
            QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 0;
        }
        QualitySettings.SetQualityLevel(previousQuality, false);
        KeepInstancingVariants();
        MaterialAsset("CitySurface", "Universal Render Pipeline/Lit");
        MaterialAsset("CityOverlay", "Universal Render Pipeline/Unlit", true);
        PrepareTerrainDetail();
        foreach (var resource in new[] { "CityLandscapeVertex", "CityRiverVertex" }) {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Rendering/" + resource + ".mat");
            Require(material != null && material.shader != null, "City terrain material is missing: " + resource);
            Require(!ShaderUtil.ShaderHasError(material.shader), "City terrain shader import failed: " + resource);
            material.SetFloat("_DetailStrength", .7f);
            material.SetTexture("_GroundDetail", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/CityTerrain/HandpaintedGrass-v1.png"));
            material.SetTexture("_RockDetail", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/CityTerrain/HandpaintedRock-v1.png"));
            EditorUtility.SetDirty(material);
        }
        CitySurfaceStyles.Prepare();
        foreach(var style in new[]{"Dirt","Stone","Gravel"}) {
            var road=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Rendering/CityRoad"+style+".mat");
            Require(road!=null&&road.shader!=null&&!ShaderUtil.ShaderHasError(road.shader),"Road surface shader failed: "+style);
        }
        PlayerSettings.companyName = "MET"; PlayerSettings.productName = "MET 来生 Unity Beta";
        PlayerSettings.bundleVersion = "0.7.0-ui.5.1-local";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.projectmet.laisheng.unitybeta");
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; PlayerSettings.runInBackground = false;
        var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
        if (Array.IndexOf(defines.Split(';'), "GLTFAST_KEEP_MESH_DATA") < 0)
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone,
                string.IsNullOrEmpty(defines) ? "GLTFAST_KEEP_MESH_DATA" : defines + ";GLTFAST_KEEP_MESH_DATA");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 2);
        EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssets();
        Debug.Log("MET City URP saved. Restart the Editor/process before GLB reimport.");
    }

    static void MaterialAsset(string name, string shaderName, bool transparent = false) {
        string path = "Assets/Resources/Rendering/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) {
            var shader = Shader.Find(shaderName); Require(shader != null, "Official shader missing: " + shaderName);
            material = new Material(shader) { name = name, enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetFloat("_Surface", transparent ? 1 : 0);
        material.SetFloat("_Blend", 0);
        BaseShaderGUI.SetupMaterialBlendMode(material);
        EditorUtility.SetDirty(material);
    }

    static void PrepareTerrainDetail() {
        foreach (var texturePath in new[] {"Assets/Resources/CityTerrain/HandpaintedGrass-v1.png", "Assets/Resources/CityTerrain/HandpaintedRock-v1.png"}) {
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        Require(importer != null, "Original painted ground texture is missing.");
        importer.sRGBTexture = true; importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8; importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.isReadable = false; importer.SaveAndReimport();
        }
    }

    [MenuItem("MET/City/Generate full game scene")]
    public static void GenerateScene() {
        RequireEditor(); Require(UrpAtDomainStart && IsURPConfigured(),
            "Run MET.City.Editor.CityProject.PrepareURP, then restart before importing glTF in the URP domain.");
        foreach (string path in Directory.GetFiles("Assets/Resources/CityModels", "*.glb", SearchOption.AllDirectories)) {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(path) != null, "Official glTFast import failed: " + path);
        }
        CityNatureAssets.Prepare();
        Require(AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/CityFonts/NotoSansSC.ttf") != null,
            "Licensed Chinese font did not import.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("MET_Laisheng_City");
        root.AddComponent<CityRuntime>(); root.AddComponent<CityInterface>(); root.AddComponent<CityWorld>();
        root.AddComponent<CityBootstrap>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("MET full city scene generated. Runtime evidence requires Play Mode or Player execution.");
    }

    public static void GenerateBatch() {
        try { GenerateScene(); EditorApplication.Exit(0); } catch (Exception error) { Fail(error); }
    }
    public static void RunValidation() {
        try {
            Require(CityEvidence.Requested(), "Pass --validate-city or --capture-city.");
            GenerateScene(); EditorApplication.isPlaying = true;
        } catch (Exception error) { Fail(error); }
    }
    [MenuItem("MET/City/Build macOS universal app")]
    public static void BuildMac() {
        try {
            Require(EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSX,
                "Start Unity with -buildTarget OSXUniversal; macOS support must be installed.");
            PrepareAssets();
            GenerateScene();
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.x64ARM64;
            Debug.Log("macOS build architecture: " + UnityEditor.OSXStandalone.UserBuildSettings.architecture);
            var output = Environment.GetEnvironmentVariable("MET_UNITY_MAC_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = "Build/MET Laisheng Unity.app";
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneOSX, options = BuildOptions.None
            });
            Require(result.summary.result == BuildResult.Succeeded, "macOS build failed: " + result.summary.result);
            Debug.Log("MET City macOS universal app built at " + output);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        } catch (Exception error) { Fail(error); }
    }
    static bool IsURPConfigured() => GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset &&
        (QualitySettings.renderPipeline == null || QualitySettings.renderPipeline is UniversalRenderPipelineAsset);
    static void RequireEditor() => Require(Application.unityVersion == "6000.6.5f1", "Full migration uses Unity 6000.6.5f1.");
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Fail(Exception error) { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(2); else throw error; }
    static void KeepInstancingVariants() {
        UnityEngine.Object settings = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset"))
            if (asset is GraphicsSettings) { settings = asset; break; }
        Require(settings != null, "GraphicsSettings asset is missing.");
        var serialized = new SerializedObject(settings);
        var property = serialized.FindProperty("m_InstancingStripping");
        Require(property != null && property.propertyType == SerializedPropertyType.Enum, "Instancing stripping property is unavailable.");
        bool found = false;
        for (var i = 0; i < property.enumNames.Length; i++)
            if (property.enumNames[i] == "KeepAll" || property.enumDisplayNames[i] == "Keep All") {
                property.enumValueIndex = i; found = true; break;
            }
        Require(found, "Keep All instancing variants option is unavailable.");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
}
