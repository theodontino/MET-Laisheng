using System;
using UnityEditor;
using UnityEngine;
namespace MET.City.Editor {
/// <summary>Import original authored surfaces and pin shader/texture references in Player.</summary>
public static class CitySurfaceStyles {
    const string Root="Assets/Resources/CityTerrain/SurfaceStyles/";
    public static void Prepare() {
        foreach(var name in new[]{"Meadow","ForestLitter","DryTurf","RoadDirt","RoadStone","RoadGravel"}) {
            var path=Root+name+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Missing original surface: "+path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.maxTextureSize=512;importer.mipmapEnabled=true;importer.isReadable=false;
            importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;
            importer.anisoLevel=8;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        var landscape=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Rendering/CityLandscapeVertex.mat");
        landscape.SetTexture("_GroundDetail",Load("Meadow"));
        landscape.SetTexture("_ForestDetail",Load("ForestLitter"));
        landscape.SetTexture("_DryDetail",Load("DryTurf"));
        EditorUtility.SetDirty(landscape);
        var shader=Shader.Find("MET/City/RoadSurface");
        if(shader==null)throw new InvalidOperationException("City road shader did not import.");
        string[] styles={"Dirt","Stone","Gravel"};
        for(int i=0;i<styles.Length;i++) {
            var style=styles[i];string path="Assets/Resources/Rendering/CityRoad"+style+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;material.name="CityRoad"+style;material.enableInstancing=true;
            material.SetTexture("_BaseMap",Load("Road"+style));
            material.SetFloat("_WorldScale",i==0?.75f:i==1?1.3f:1.8f);
            material.SetFloat("_Smoothness",i==1?.10f:.07f);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }
    static Texture2D Load(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+name+".png");
}
}
