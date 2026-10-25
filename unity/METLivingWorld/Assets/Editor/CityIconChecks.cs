using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace MET.City.Editor {
/// <summary>Focused regression for real imported textures and non-square UI icon hosts.</summary>
public static class CityIconChecks {
 static int assertions;
 static void Check(bool condition,string context){assertions++;if(!condition)throw new Exception(context);}
 public static void Run(){
  GameObject owner=null;
  try{
   var sizes=new[]{new Vector2(24,24),new Vector2(360,24),new Vector2(24,180),new Vector2(32,32),new Vector2(80,80)};
   owner=new GameObject("Isolated icon geometry",typeof(RectTransform));
   var rect=owner.GetComponent<RectTransform>();var glyph=owner.AddComponent<CityHudGlyph>();
   var method=typeof(CityHudMesh).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null);
   foreach(var id in CityIconCatalog.Ids){
    var texture=Resources.Load<Texture2D>("CityIcons/"+id);
    Check(texture!=null,id+" texture missing");Check(texture.width==128&&texture.height==128,id+" export dimensions");
    var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
    Check(importer.alphaIsTransparency&&!importer.mipmapEnabled,id+" alpha/mipmap settings");
    Check(importer.textureCompression==TextureImporterCompression.Uncompressed,id+" compression");
    glyph.Kind=id;Check(glyph.mainTexture==texture,id+" semantic texture selection");
    foreach(var size in sizes){
     rect.sizeDelta=size;using(var vertices=new VertexHelper()){
      method.Invoke(glyph,new object[]{vertices});Check(vertices.currentVertCount==4,id+" textured quad");
      UIVertex a=default,b=default,c=default;vertices.PopulateUIVertex(ref a,0);vertices.PopulateUIVertex(ref b,1);vertices.PopulateUIVertex(ref c,2);
      float side=Mathf.Min(size.x,size.y);Check(Mathf.Abs(b.position.x-a.position.x-side)<.01f&&Mathf.Abs(c.position.y-b.position.y-side)<.01f,id+" square aspect at "+size);
      Check(((Vector2)((a.position+c.position)*.5f)-rect.rect.center).sqrMagnitude<.001f,id+" centered bounds");
      Check(a.uv0.x==0&&a.uv0.y==0&&c.uv0.x==1&&c.uv0.y==1,id+" texture orientation");
     }
    }
   }
   Check(CityIconCatalog.Resolve("service")=="medical","legacy service mapping");
   Check(CityIconCatalog.Resolve("housing")=="home","housing mapping");
   Check(CityIconCatalog.Resolve("CP")=="build","CP mapping");
   Check(!glyph.raycastTarget,"icons do not intercept pointer events");
   Debug.Log("CITY ICON CHECKS PASS: "+CityIconCatalog.Ids.Length+" real imported textures; "+assertions+" checks including wide/tall hosts, alpha, catalog and UVs. No Player visual acceptance claimed.");
   UnityEngine.Object.DestroyImmediate(owner);EditorApplication.Exit(0);
  }catch(Exception error){if(owner!=null)UnityEngine.Object.DestroyImmediate(owner);Debug.LogException(error);EditorApplication.Exit(1);}
 }
}
}
