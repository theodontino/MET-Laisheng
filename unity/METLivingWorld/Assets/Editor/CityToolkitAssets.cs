using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MET.City.Editor {
// The resource is generated in the Editor so Unity includes its built-in ICU
// data in Player builds. No hand-authored serialization or runtime asset edits.
public static class CityToolkitAssets {
 const string Folder = "Assets/Resources/CityToolkit";
 const string PanelPath = Folder + "/UpgradePanel.asset";
 const string OfficialPanelPath = "Packages/com.unity.render-pipelines.core/Runtime/Debugging/Runtime UI Resources/RuntimeDebugWindow_PanelSettings.asset";

 public static void Prepare() {
  var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Folder + "/UpgradeTheme.tss");
  if (theme == null) throw new InvalidOperationException("Upgrade Toolkit theme is missing.");
  var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
  if (panel == null) {
   // This official, matching URP dependency already references Unity's ICU asset.
   var official = AssetDatabase.LoadAssetAtPath<PanelSettings>(OfficialPanelPath);
   if (official == null) throw new InvalidOperationException("Official UI Toolkit panel resource is missing from the installed URP core package.");
   panel = UnityEngine.Object.Instantiate(official);
   panel.name = "MET Upgrade Panel";
   Directory.CreateDirectory(Folder);
   AssetDatabase.CreateAsset(panel, PanelPath);
  }
  panel.themeStyleSheet = theme;
  panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
  panel.referenceResolution = new Vector2Int(2560, 1440);
  panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
  panel.match = .5f;
  panel.scale = 1f;
  panel.sortingOrder = 200;
  panel.targetTexture = null;
  panel.targetDisplay = 0;
  var serialized = new SerializedObject(panel);
  var icu = serialized.FindProperty("m_ICUDataAsset");
  if (icu == null || icu.objectReferenceValue == null)
   throw new InvalidOperationException("Upgrade Toolkit panel is missing Unity's international text data.");
  EditorUtility.SetDirty(panel);
  AssetDatabase.SaveAssets();
 }
}
}
