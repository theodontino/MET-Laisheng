using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace MET.City.Editor {
/// <summary>Configure shared source textures, then delegate all geometry/material imports to official glTFast.</summary>
public static class CityNatureAssets {
    const string Root="Assets/Resources/CityNature/Quaternius";
    public static void Prepare() {
        var canopyShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/City/World/Shaders/CityCanopy.shader");
        if(canopyShader==null||ShaderUtil.ShaderHasError(canopyShader))throw new InvalidOperationException("CityCanopy URP shader import failed.");
        foreach(var path in Directory.GetFiles(Root,"*.png")) {
            var texture=(TextureImporter)AssetImporter.GetAtPath(path);
            bool normal=path.EndsWith("_Normal.png",StringComparison.Ordinal),leaf=Path.GetFileName(path).StartsWith("Leave",StringComparison.Ordinal)||Path.GetFileName(path).StartsWith("Leaf",StringComparison.Ordinal);
            texture.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            texture.sRGBTexture=!normal;texture.mipmapEnabled=true;texture.isReadable=false;
            texture.maxTextureSize=512;texture.textureCompression=TextureImporterCompression.Compressed;
            texture.alphaSource=TextureImporterAlphaSource.FromInput;texture.alphaIsTransparency=leaf;
            texture.mipMapsPreserveCoverage=leaf;texture.alphaTestReferenceValue=.14f;
            texture.wrapMode=TextureWrapMode.Repeat;texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=2;
            texture.SaveAndReimport();
        }
        foreach(var path in Directory.GetFiles(Root,"*.gltf")) {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            var prototype=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prototype==null)throw new InvalidOperationException("Official glTFast nature import failed: "+path);
            foreach(var mesh in prototype.GetComponentsInChildren<MeshFilter>(true))if(!mesh.sharedMesh.isReadable)
                throw new InvalidOperationException("Enable existing GLTFAST_KEEP_MESH_DATA before importing "+path);
            // Official importer remains authoritative for geometry/UV/texture; pin the display shader through Resources materials.
            foreach(var renderer in prototype.GetComponentsInChildren<MeshRenderer>(true))foreach(var original in renderer.sharedMaterials) {
                if(!CityFoliage.IsCanopyMaterial(original))continue;
                string materialPath="Assets/Resources/Rendering/CityCanopy_"+original.name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(material==null){material=new Material(canopyShader){name="CityCanopy_"+original.name};AssetDatabase.CreateAsset(material,materialPath);}
                else material.shader=canopyShader;
                material.shaderKeywords=Array.Empty<string>();
                material.SetTexture("_BaseMap",original.GetTexture("baseColorTexture"));
                material.SetTextureScale("_BaseMap",original.GetTextureScale("baseColorTexture"));
                material.SetTextureOffset("_BaseMap",original.GetTextureOffset("baseColorTexture"));
                CityFoliage.TuneCanopyMaterial(material);EditorUtility.SetDirty(material);
            }
        }
        AssetDatabase.SaveAssets();
    }
}
}
