using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
/// <summary>Read-only map measurements; the simulation and ordinary selection remain authoritative.</summary>
public sealed partial class CityInterface {
 CityWorld _inspectionWorld;
 string _inspectionLock="",_inspectionLayer="",_inspectionPhase="",_inspectionCity="",_inspectionSelected="",_inspectionSignature="";
 double _inspectionNextRefresh;
 double _inspectionMarkerRefreshAt;V _inspectionMarkerData=V.Map();
 bool _mapNumericLabelsVisible,_mapShiftArmed;
 public bool MapNumericLabelsVisible=>_mapNumericLabelsVisible;
 RectTransform _inspectionPanel,_inspectionContent;
 ScrollRect _inspectionScroll;
 Button _inspectionSelectedButton;
 readonly List<RectTransform> _inspectionMarkers=new List<RectTransform>();
 readonly List<Text> _inspectionMarkerText=new List<Text>();
 public bool MapInspectionOpen=>_inspectionLock!="";

 public void AttachMapInspection(CityWorld world) {
  if(_inspectionWorld==world)return;
  if(_inspectionWorld!=null)DismissMapInspection();
  _inspectionWorld=world;
  _inspectionLayer=Layer;_inspectionPhase=MapSourcePhase;_inspectionCity=_cityIdentity;_inspectionSelected=SelectedTile;
  if(world==null||_root==null)return;
  _inspectionPanel=Surface("Map source inspection",_root,new Color(.018f,.05f,.06f,.97f));
  Pin(_inspectionPanel,new Vector2(1,1),new Vector2(1,1),new Vector2(-18,-428),new Vector2(346,482));
  var title=Label(_inspectionPanel,"地格来源",19,Green);
  Pin(title.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(13,-9),new Vector2(275,30));
  var close=Button(_inspectionPanel,"×",CloseMapInspection);
  Pin((RectTransform)close.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-7,-7),new Vector2(35,34));
  var viewport=Surface("Map source viewport",_inspectionPanel,new Color(.025f,.07f,.08f,.72f));
  Stretch(viewport,8,48,8,8);viewport.gameObject.AddComponent<Mask>().showMaskGraphic=true;
  _inspectionContent=Column(viewport,"Map source rows",5);
  Pin(_inspectionContent,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(0,0));
  _inspectionContent.anchorMax=new Vector2(1,1);_inspectionContent.offsetMin=new Vector2(0,0);_inspectionContent.offsetMax=new Vector2(0,0);
  _inspectionScroll=viewport.gameObject.AddComponent<ScrollRect>();_inspectionScroll.viewport=viewport;_inspectionScroll.content=_inspectionContent;
  _inspectionScroll.scrollSensitivity=48;_inspectionScroll.horizontal=false;_inspectionScroll.vertical=true;_inspectionScroll.movementType=ScrollRect.MovementType.Clamped;
  _inspectionPanel.gameObject.SetActive(false);
  _inspectionSelectedButton=Button(_root,"所选来源  I",InspectSelectedTile);
  Pin((RectTransform)_inspectionSelectedButton.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-18,110),new Vector2(180,38*TextScale));
  _inspectionSelectedButton.gameObject.SetActive(false);
  LayoutMapInspectionHud();
 }

 void LayoutMapInspectionHud(){
  if(_inspectionSelectedButton!=null)((RectTransform)_inspectionSelectedButton.transform).sizeDelta=new Vector2(180,38*TextScale);
  if(_inspectionPanel==null)return;
  float scale=Mathf.Clamp((float)Runtime.DisplaySettings.Get("ui_scale",1).Double,.8f,1.2f);
  float top=99+(_planningOverviewOpen?100+188*TextScale:43)+16;
  _inspectionPanel.anchoredPosition=new Vector2(-18,-top);
  _inspectionPanel.sizeDelta=new Vector2(346,Mathf.Min(482,1440/scale-top-170));
 }

 void InspectSelectedTile(){
  if(SelectedTile!=""&&!IsModal&&!MapViewing&&!ConstructionIntentActive)LockMapInspection(SelectedTile);
 }

 void ResetInspectionReadings() {
  _inspectionMarkerRefreshAt=0;_inspectionMarkerData=V.Map();
  foreach(var marker in _inspectionMarkers)if(marker!=null)marker.gameObject.SetActive(false);
 }
 public void DismissMapInspection() {
  _inspectionLock="";_inspectionSignature="";ResetInspectionReadings();
  if(_inspectionPanel!=null)_inspectionPanel.gameObject.SetActive(false);
  _inspectionWorld?.HighlightInspectionSources(null);
 }
 public void CloseMapInspection(){DismissMapInspection();}
 public bool HandleMapInspectionEscape() {
  bool visible=MapInspectionOpen||(_mapNumericLabelsVisible&&Layer!="terrain");
  _mapNumericLabelsVisible=false;_mapShiftArmed=false;
  CloseMapInspection();return visible;
 }
 public bool TryLockMapInspection(string tile) { return false; }
 void LockMapInspection(string tile) {
  // Click selection owns the complete tile panel; there is no second pinned source panel.
  if(tile==""||IsModal)return;
  DismissMapInspection();SelectTile(tile);
 }
 public void UpdateMapInspectionInput() {
  if(_inspectionWorld==null||_root==null)return;
  RefreshMapLegend();
  bool unavailable=!Application.isFocused||IsModal||ConstructionIntentActive;
  if(_inspectionSelectedButton!=null)_inspectionSelectedButton.gameObject.SetActive(false);
  if(unavailable&&MapInspectionOpen)DismissMapInspection();
  if(_inspectionLayer!=Layer||_inspectionPhase!=MapSourcePhase||_inspectionCity!=_cityIdentity) {
   bool cityChanged=_inspectionCity!=_cityIdentity;
   _inspectionLayer=Layer;_inspectionPhase=MapSourcePhase;_inspectionCity=_cityIdentity;
   ResetInspectionReadings();_inspectionSignature="";
   if(cityChanged)DismissMapInspection();
  }
  if(_inspectionSelected!=SelectedTile){_inspectionSelected=SelectedTile;ResetInspectionReadings();}
  bool blocked=unavailable||KeyboardOwned;
  // Toggle on release only after a standalone Shift press. A later L (or any
  // other key/mouse action) cancels it, preserving Shift+L and text ownership.
  bool shiftDown=Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift);
  bool shiftHeld=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
  if(blocked)_mapShiftArmed=false;
  else {
   if(shiftDown)_mapShiftArmed=!Input.GetKey(KeyCode.LeftControl)&&!Input.GetKey(KeyCode.RightControl)&&
       !Input.GetKey(KeyCode.LeftAlt)&&!Input.GetKey(KeyCode.RightAlt)&&!Input.GetKey(KeyCode.LeftCommand)&&!Input.GetKey(KeyCode.RightCommand);
   if(Input.GetMouseButton(0)||Input.GetMouseButton(1)||Input.GetMouseButton(2))_mapShiftArmed=false;
   if(_mapShiftArmed&&Input.anyKeyDown)foreach(KeyCode key in Enum.GetValues(typeof(KeyCode)))
    if(key!=KeyCode.LeftShift&&key!=KeyCode.RightShift&&Input.GetKeyDown(key)){_mapShiftArmed=false;break;}
   if(_mapShiftArmed&&!shiftHeld) {_mapShiftArmed=false;_mapNumericLabelsVisible=!_mapNumericLabelsVisible;_inspectionMarkerRefreshAt=0;}
  }
  if(blocked||!_mapNumericLabelsVisible||Layer=="terrain")HideInspectionMarkers();
  else RefreshInspectionMarkers();
  if(!blocked&&Input.GetKeyDown(KeyCode.I))InspectSelectedTile();
  if(MapInspectionOpen&&Time.realtimeSinceStartupAsDouble>=_inspectionNextRefresh)RefreshInspectionPanel();
 }
 void HideInspectionMarkers(){foreach(var marker in _inspectionMarkers)if(marker!=null)marker.gameObject.SetActive(false);}
 void RefreshInspectionMarkers() {
  if(_inspectionWorld==null)return;
  if(_inspectionMarkerData.Count==0||Time.realtimeSinceStartupAsDouble>=_inspectionMarkerRefreshAt) {
   _inspectionMarkerData=V.Arr();
   foreach(var tile in _inspectionWorld.InspectionTiles)_inspectionMarkerData.Add(_inspectionWorld.InspectionForTile(tile,false));
   _inspectionMarkerRefreshAt=Time.realtimeSinceStartupAsDouble+.25;
  }
  int i=0;
  foreach(var data in _inspectionMarkerData) {
   if(i>=_inspectionMarkers.Count) {
    var marker=Surface("Map numeric reading",_root,new Color(.012f,.025f,.038f,.96f));
    marker.GetComponent<Image>().raycastTarget=false;
    var border=marker.gameObject.AddComponent<Outline>();border.effectColor=new Color(.23f,.63f,.67f,.85f);border.effectDistance=new Vector2(1,-1);
    Pin(marker,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(122,32));
    var text=Label(marker,"",14,Color.white);text.fontStyle=FontStyle.Bold;
    Stretch(text.rectTransform,5,2,5,2);text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
    _inspectionMarkers.Add(marker);_inspectionMarkerText.Add(text);
   }
   string kind=data.Get("reading_kind","").Text;
   string prefix=kind=="employment_ratio"?"就业 ":"";
   PlaceInspectionMarker(i++,data.Get("tile","").Text,data.Get("available",false).Bool?prefix+data.Get("display","无数据").Text:"无数据");
  }
  while(i<_inspectionMarkers.Count)_inspectionMarkers[i++].gameObject.SetActive(false);
 }
 void PlaceInspectionMarker(int index,string tile,string caption) {
  var marker=_inspectionMarkers[index];Vector2 point,local;
  if(tile==""||!_inspectionWorld.InspectionScreenPosition(tile,out point)||
     point.x<0||point.y<0||point.x>Screen.width||point.y>Screen.height||PointerOwned(point)||
     !RectTransformUtility.ScreenPointToLocalPointInRectangle(_root,point,null,out local)) {
   marker.gameObject.SetActive(false);return;
  }
  marker.gameObject.SetActive(true);marker.anchoredPosition=local+new Vector2(0,18);
  _inspectionMarkerText[index].text=caption;
 }
 void RefreshInspectionPanel() {
  _inspectionNextRefresh=Time.realtimeSinceStartupAsDouble+.35;
  if(_inspectionWorld==null||_inspectionContent==null||_inspectionLock=="")return;
  var info=_inspectionWorld.InspectionForTile(_inspectionLock);
  string signature=info.ToJson(false,true);
  if(signature==_inspectionSignature||PointerBusy)return;
  _inspectionSignature=signature;
  float scroll=_inspectionScroll==null?1:_inspectionScroll.verticalNormalizedPosition;
  Clear(_inspectionContent);
  Label(_inspectionContent,"地格 "+_inspectionLock+" · "+info.Get("label",LayerLabel(Layer)).Text,18,Ink,29);
  Label(_inspectionContent,info.Get("available",false).Bool?info.Get("display","无数据").Text:Layer=="terrain"?"地貌与地块特性":"此图层无数据",23,Green,40);
  Label(_inspectionContent,InspectionPhaseLabel(info.Get("source_phase",MapSourcePhase).Text),14,Muted,27);
  var reading=info.Get("reading","").Text;if(reading!="")Label(_inspectionContent,reading,14,Muted);
  var sourceIds=new HashSet<string>(StringComparer.Ordinal);
  int count=0;
  foreach(var entry in info.Get("rows",V.Arr())) {
   count++;
   string source=entry.Get("source_id","").Text,sourceTile=entry.Get("source_tile","").Text;
   bool attributable=sourceTile!=""&&!new[]{"group","denominator","baseline","proposal"}.Contains(entry.Get("kind","").Text);if(attributable)sourceIds.Add(sourceTile);
   var row=Surface("Map source row",_inspectionContent,new Color(.045f,.10f,.11f,.90f));
   Height(row,58);
   var name=Label(row,S.Get("projects",V.Map()).Has(source)?G.project_name(source):entry.Get("label","来源").Text,14,Ink);
   Pin(name.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(7,-4),new Vector2(205,25));
   var value=Label(row,entry.Get("display","无数据").Text,16,Green);
   Pin(value.rectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(-7,-4),new Vector2(104,25));value.alignment=TextAnchor.MiddleRight;
   var focusTarget=attributable?sourceTile:"";
   var foot=Label(row,(sourceTile!=""?"地格 "+sourceTile:"位置未记录")+
       (focusTarget!=""?" · 点击定位":" · 不提供来源定位"),12,Muted);
   Pin(foot.rectTransform,new Vector2(0,0),new Vector2(0,0),new Vector2(7,3),new Vector2(312,25));
   if(focusTarget!="") {
    var button=row.gameObject.AddComponent<Button>();
    button.onClick.AddListener(()=>_inspectionWorld.FocusInspectionSource(focusTarget));
    AddDeskTip(row.gameObject,"来源记录",()=>entry.Get("label","来源").Text+
        "\n来源 ID："+(source!=""?source:"未记录")+
        "\n记录类型："+entry.Get("kind","未记录").Text+
        "\n源地格："+sourceTile+
        (entry.Get("note","").Text!=""?"\n记录说明："+entry["note"].Text:""));
   } else row.GetComponent<Image>().raycastTarget=false;
  }
  if(count==0)Label(_inspectionContent,"没有可追溯的逐项来源记录。",14,Muted);
  foreach(var note in info.Get("notes",V.Arr()))if(note.Text!="")Label(_inspectionContent,note.Text,13,Muted);
  _inspectionWorld.HighlightInspectionSources(sourceIds);
  Canvas.ForceUpdateCanvases();if(_inspectionScroll!=null)_inspectionScroll.verticalNormalizedPosition=scroll;
 }
 static string InspectionPhaseLabel(string phase) {
  return phase=="committed"||phase=="actual"?"最近周期实际":
      phase=="baseline"?"同一步现状预测":phase=="proposal"||phase=="next_tick"?"同一步建成预测":"来源阶段："+phase;
 }
}
}
