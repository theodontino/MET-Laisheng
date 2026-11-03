using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MET.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace MET.City.Editor {
// Calls the actual panel renderer in an isolated edit-mode object graph, not a Player/save.
public static class TilePanelChecks {
 const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Set(object target,string field,object value)=>target.GetType().GetField(field,Hidden).SetValue(target,value);
 static object Call(object target,string method,params object[] values)=>target.GetType().GetMethod(method,Hidden).Invoke(target,values);
 public static void Run(){
  GameObject owner=null,canvas=null;
  try {
   var g=new Game(new Catalog(Path.Combine(Application.streamingAssetsPath,"MetContent")));g.new_game(8291,1000);g.select_block(0);g.choose_origin(g.s["origin_choices"][0].Text);
   owner=new GameObject("Isolated tile regression");owner.SetActive(false);
   var runtime=owner.AddComponent<CityRuntime>();var ui=owner.AddComponent<CityInterface>();typeof(CityInterface).GetProperty("Runtime").SetValue(ui,runtime);
   canvas=new GameObject("Isolated tile canvas",typeof(RectTransform),typeof(Canvas));canvas.SetActive(false);var root=(RectTransform)canvas.transform;root.sizeDelta=new Vector2(1920,1080);
   Set(ui,"_root",root);Set(ui,"_font",Resources.Load<Font>("CityFonts/NotoSansSC"));Set(ui,"_viewGame",g);Set(ui,"_state",g.s);
   Call(ui,"BuildTilePanel");int checks=0;
   foreach(bool editable in new[]{true,false}){
    Set(runtime,"_sessionSnapshot",V.Map("focused_block_id","block","active_district_id",editable?"district":"","blocks",V.Map("block",V.Map("district_id","district"))));
    foreach(string tile in g.s["cells"].Keys){
     var id=g.s["projects"].Keys.FirstOrDefault(x=>g.s["projects"][x]["tile"].Text==tile)??"";
     typeof(CityInterface).GetProperty("SelectedTile").SetValue(ui,tile);typeof(CityInterface).GetProperty("SelectedBuilding").SetValue(ui,id);Set(ui,"_contextKey","");
     Call(ui,"RefreshTilePanel");var footer=(RectTransform)typeof(CityInterface).GetField("_tileFooter",Hidden).GetValue(ui);var buttons=footer.GetComponentsInChildren<Button>(true);
     if(buttons.Length!=3)throw new Exception("Missing actions on "+tile);
     bool water=g.s["cells"][tile]["traits"].Contains("tile.water");
     if(id==""&&buttons.Any(x=>x.interactable!=(editable&&!water)))throw new Exception("Incorrect construction availability on "+tile);
     checks++;
    }
   }
   Debug.Log("TILE PANEL CHECKS PASS "+checks+" actual panel renders; empty/occupied/water and editable/read-only. No Player acceptance.");
   UnityEngine.Object.DestroyImmediate(canvas);UnityEngine.Object.DestroyImmediate(owner);EditorApplication.Exit(0);
  }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
 }
}
}
