using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace MET.City {
/// <summary>All ordinary explanations use the same real-time dwell policy.</summary>
public sealed class CityDeskTooltip : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler {
 public CityInterface Owner;public string Heading;public Func<string> Body;
 public bool PointerInside {get;private set;}
 public bool FocusInside {get;private set;}
 public void OnPointerEnter(PointerEventData e){PointerInside=true;Owner?.RequestDeskTip(this,true);}
 public void OnPointerExit(PointerEventData e){PointerInside=false;Owner?.LeaveDeskTip(this,true);}
 public void OnSelect(BaseEventData e){FocusInside=true;if(!PointerInside&&!Input.GetMouseButton(0))Owner?.RequestDeskTip(this,false);}
 public void OnDeselect(BaseEventData e){FocusInside=false;Owner?.LeaveDeskTip(this,false);}
 void OnDisable(){PointerInside=FocusInside=false;Owner?.ForgetDeskTip(this);}
}
public sealed class CityDeskTipFocus : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler {
 public CityInterface Owner;public bool TrackPointer=true;
 public void OnPointerEnter(PointerEventData e){if(TrackPointer)Owner?.SetDeskTipPointer(true);}
 public void OnPointerExit(PointerEventData e){if(TrackPointer)Owner?.SetDeskTipPointer(false);}
 public void OnSelect(BaseEventData e){Owner?.KeepDeskTip();}
 public void OnDeselect(BaseEventData e){Owner?.QueueHideDeskTip();}
}
public sealed partial class CityInterface {
 RectTransform _deskTip;CityDeskTooltip _deskTipSource,_pendingDeskTip;Button _deskTipClose;GameObject _tipReturn;ScrollRect _deskTipScroll;
 readonly DwellIntent _deskDwell=new DwellIntent(2);
 double _tipHideAt=-1;bool _tipPinned,_tipRestoring,_tipPointerInside,_tipOpenedByKeyboard,_pendingDeskPointer;
 static double DeskNow=>UnityEngine.Time.realtimeSinceStartupAsDouble;
 static string TipKey(CityDeskTooltip source,bool pointer)=>(pointer?"pointer:":"focus:")+(source==null?"0":source.GetEntityId().ToString());
 void AddDeskTip(GameObject target,string title,string body)=>AddDeskTip(target,title,()=>body);
 void AddDeskTip(GameObject target,string title,Func<string> body){
  var tip=target.GetComponent<CityDeskTooltip>()??target.AddComponent<CityDeskTooltip>();tip.Owner=this;tip.Heading=title;tip.Body=body;
  if(target.GetComponent<Selectable>()==null){var graphic=target.GetComponent<Graphic>();if(graphic==null){graphic=target.AddComponent<Image>();graphic.color=Color.clear;}graphic.raycastTarget=true;var button=target.AddComponent<Button>();button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>ShowDeskTip(tip,true));}
 }
 void BuildDeskTip(){
  _deskTip=Surface("Desk Explanation",_root,HudPanel);Pin(_deskTip,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(430,230));
  var shadow=_deskTip.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.52f);shadow.effectDistance=new Vector2(3,-4);
  var keeper=_deskTip.gameObject.AddComponent<CityDeskTipFocus>();keeper.Owner=this;_deskTip.gameObject.SetActive(false);
 }
 bool DeskSourceAllowed(CityDeskTooltip source){
  if(source==null||!source.isActiveAndEnabled||!source.gameObject.activeInHierarchy||_blueprintBusy||!Application.isFocused)return false;
  if(IsModal){Transform owner=_confirm!=null?_confirm.transform:_archive;if(owner==null||!source.transform.IsChildOf(owner))return false;}
  return true;
 }
 public void RequestDeskTip(CityDeskTooltip source,bool pointer){
  if(_tipRestoring||!DeskSourceAllowed(source))return;
  if(DeskTipOpen){var selected=EventSystem.current?.currentSelectedGameObject;if(_tipPinned||(selected!=null&&selected.transform.IsChildOf(_deskTip)))return;if(_deskTipSource==source){KeepDeskTip();return;}CloseDeskTip(false,false);}
  if(_deskDwell.Begin(TipKey(source,pointer),DeskNow)){_pendingDeskTip=source;_pendingDeskPointer=pointer;}
 }
 // Explicit click / F1 remains available; ordinary callers can no longer bypass dwell.
 public void ShowDeskTip(CityDeskTooltip source,bool locked){if(!locked){RequestDeskTip(source,source!=null&&source.PointerInside);return;}if(_tipRestoring||!DeskSourceAllowed(source))return;_deskDwell.Cancel();_pendingDeskTip=null;PresentDeskTip(source,true,false);}
 public void LeaveDeskTip(CityDeskTooltip source,bool pointer){
  var key=TipKey(source,pointer);_deskDwell.Exit(key);
  if(_pendingDeskTip==source&&_pendingDeskPointer==pointer){_pendingDeskTip=null;_deskDwell.Cancel();}
  if(_deskTipSource==source)QueueHideDeskTip();
 }
 public void ForgetDeskTip(CityDeskTooltip source){
  _deskDwell.Invalidate(TipKey(source,true));_deskDwell.Invalidate(TipKey(source,false));
  if(_pendingDeskTip==source){_pendingDeskTip=null;_deskDwell.Cancel();}
  if(_deskTipSource==source)CloseDeskTip(false,false);
 }
 void PresentDeskTip(CityDeskTooltip source,bool locked,bool keyboard){
  if(!DeskSourceAllowed(source))return;_tipHideAt=-1;
  if(_deskTip==null)BuildDeskTip();Clear(_deskTip);_deskTipSource=source;_tipPinned=locked;_tipOpenedByKeyboard=keyboard;_tipReturn=source.gameObject;_tipPointerInside=false;
  float width=Mathf.Min(430,_root.rect.width-24);
  string explanation=source.Body?.Invoke()??"";float lineWidth=Mathf.Max(12,(width-40)/(15*TextScale));float tipHeight=Mathf.Min(_root.rect.height-24,Mathf.Clamp(86+Mathf.Ceil(explanation.Length/lineWidth)*20*TextScale,130,350));_deskTip.sizeDelta=new Vector2(width,tipHeight);
  var heading=Label(_deskTip,source.Heading,18,HudText);Pin(heading.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-14),new Vector2(width-72,33));
  // Long explanations scroll inside the small overlay rather than expand across the screen.
  var viewport=Node("Explanation Text Viewport",_deskTip);Stretch(viewport,18,52,18,14);viewport.gameObject.AddComponent<RectMask2D>();var viewportImage=viewport.gameObject.AddComponent<Image>();viewportImage.color=Color.clear;
  var copy=Label(viewport,explanation,15,HudMuted);copy.rectTransform.anchorMin=new Vector2(0,1);copy.rectTransform.anchorMax=Vector2.one;copy.rectTransform.pivot=new Vector2(.5f,1);copy.rectTransform.sizeDelta=new Vector2(0,0);copy.rectTransform.anchoredPosition=Vector2.zero;var fit=copy.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
  var scroll=viewport.gameObject.AddComponent<ScrollRect>();_deskTipScroll=scroll;scroll.horizontal=false;scroll.viewport=viewport;scroll.content=copy.rectTransform;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
  _deskTipClose=Button(_deskTip,"×",HideDeskTip);Pin((RectTransform)_deskTipClose.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-8,-8),new Vector2(34,32));var navigation=_deskTipClose.navigation;navigation.mode=Navigation.Mode.None;_deskTipClose.navigation=navigation;var closeKeeper=_deskTipClose.gameObject.AddComponent<CityDeskTipFocus>();closeKeeper.Owner=this;closeKeeper.TrackPointer=false;
  PositionDeskTip(source,width,tipHeight);_deskTip.gameObject.SetActive(true);_deskTip.SetAsLastSibling();
  var group=_deskTip.GetComponent<CanvasGroup>()??_deskTip.gameObject.AddComponent<CanvasGroup>();group.interactable=true;group.blocksRaycasts=true;
  if(locked)EventSystem.current?.SetSelectedGameObject(_deskTipClose.gameObject);
 }
 void PositionDeskTip(CityDeskTooltip source,float width,float height){
  var anchor=DeskTipBounds(source.transform);
  var avoid=new List<PopupBox>();
  foreach(var protectedArea in new RectTransform[]{_planningTop,_planningUrgent,_planningNoticeButton!=null?_planningNoticeButton.transform as RectTransform:null,_planningNoticePanel,_planningTime,_planningBuildMenu})if(protectedArea!=null&&protectedArea.gameObject.activeInHierarchy){var box=DeskTipBounds(protectedArea);if(box.Width>0&&box.Height>0)avoid.Add(box);}
  var r=_root.rect;var placed=PopupPlacement.Place(new PopupBox(r.xMin,r.yMin,r.width,r.height),anchor,width,height,avoid:avoid.ToArray());
  _deskTip.anchoredPosition=new Vector2((float)(placed.X+placed.Width/2),(float)(placed.Y+placed.Height/2));
 }
 PopupBox DeskTipBounds(Transform target){
  var rect=target as RectTransform;var corners=new Vector3[4];
  if(rect!=null)rect.GetWorldCorners(corners);else for(int i=0;i<4;i++)corners[i]=target.position;
  float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
  foreach(var corner in corners){var p=_root.InverseTransformPoint(corner);minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}
  return new PopupBox(minX,minY,maxX-minX,maxY-minY);
 }
 public void SetDeskTipPointer(bool inside){_tipPointerInside=inside;if(inside)KeepDeskTip();else QueueHideDeskTip();}
 public void KeepDeskTip(){_tipHideAt=-1;}
 public void QueueHideDeskTip(){_tipHideAt=DeskNow+.18;}
 void SuppressHeldTip(CityDeskTooltip source){if(source==null)return;if(source.PointerInside)_deskDwell.Suppress(TipKey(source,true));if(source.FocusInside)_deskDwell.Suppress(TipKey(source,false));}
 void CloseDeskTip(bool suppress,bool restore){
  if(suppress){SuppressHeldTip(_pendingDeskTip);SuppressHeldTip(_deskTipSource);}_deskDwell.Cancel();
  _pendingDeskTip=null;_tipHideAt=-1;_tipPinned=false;_tipPointerInside=false;
  var focused=EventSystem.current?.currentSelectedGameObject;bool inside=_deskTip!=null&&focused!=null&&focused.transform.IsChildOf(_deskTip);if(_deskTip!=null)_deskTip.gameObject.SetActive(false);_deskTipSource=null;
  if(restore&&inside){_tipRestoring=true;var target=_tipReturn!=null&&_tipReturn.activeInHierarchy?_tipReturn:null;if(target!=null)EventSystem.current?.SetSelectedGameObject(target);else RefreshModalOwnership(true);_tipRestoring=false;}
 }
 public void HideDeskTip()=>CloseDeskTip(true,true);
 void CancelDeskTipsForPanel(){CloseDeskTip(true,false);}
 void CancelDeskTipsIn(Transform parent){if(parent==null||parent==_deskTip)return;if((_pendingDeskTip!=null&&_pendingDeskTip.transform.IsChildOf(parent))||(_deskTipSource!=null&&_deskTipSource.transform.IsChildOf(parent)))CloseDeskTip(false,false);}
 bool DeskTipOpen=>_deskTip!=null&&_deskTip.gameObject.activeSelf;
 bool DeskTipPending=>_pendingDeskTip!=null&&_deskDwell.Pending;
 bool HandleDeskTipEscape(){if(!DeskTipOpen&&!DeskTipPending)return false;HideDeskTip();return true;}
 void OnApplicationFocus(bool focused){if(!focused){CloseDeskTip(false,false);_deskDwell.Reset();DismissMapInspection();}}
 void UpdateDeskInput(){
  bool toastLive=_toast!=null&&UnityEngine.Time.unscaledTime<_toastUntil;if(_planningToastSurface!=null)_planningToastSurface.gameObject.SetActive(toastLive&&!_archiveOpen);if(_archiveOpen&&_subtitle!=null)_subtitle.text=toastLive&&_toastPage==_page?"通知："+_toast.text:_page=="tender"?(S["draft"].Count==0?"先在地图选择一块空闲陆地":"选位置  ›  选方案  ›  开工"):S["block"].Get("display_name","河谷登陆点").Text+" / "+PageTitle(_page)+"    已暂停 · 来源可展开";
  if(_pendingDeskTip!=null){if(!DeskSourceAllowed(_pendingDeskTip)||(_pendingDeskPointer?!_pendingDeskTip.PointerInside:!_pendingDeskTip.FocusInside)){_pendingDeskTip=null;_deskDwell.Cancel();}else if(_deskDwell.Mature(DeskNow)){var source=_pendingDeskTip;bool keyboard=!_pendingDeskPointer;_pendingDeskTip=null;_deskDwell.Presented();PresentDeskTip(source,false,keyboard);}}
  if(DeskTipOpen&&_deskTipSource!=null&&!DeskSourceAllowed(_deskTipSource))CloseDeskTip(false,false);
  if(DeskTipOpen&&_tipHideAt>=0&&DeskNow>=_tipHideAt){var selected=EventSystem.current?.currentSelectedGameObject;bool focusInside=selected!=null&&selected.transform.IsChildOf(_deskTip);bool anchorHeld=_deskTipSource!=null&&(_deskTipSource.PointerInside||(_tipOpenedByKeyboard&&_deskTipSource.FocusInside));if(!_tipPinned&&!_tipPointerInside&&!focusInside&&!anchorHeld)CloseDeskTip(false,false);_tipHideAt=-1;}
  if(EventSystem.current!=null&&Input.GetKeyDown(KeyCode.F1)){var source=EventSystem.current.currentSelectedGameObject?.GetComponent<CityDeskTooltip>();if(source!=null)ShowDeskTip(source,true);}
  if(DeskTipOpen&&_deskTipScroll!=null){var selected=EventSystem.current?.currentSelectedGameObject;if(selected!=null&&selected.transform.IsChildOf(_deskTip)){float scrollStep=Input.GetKeyDown(KeyCode.PageDown)?-.75f:Input.GetKeyDown(KeyCode.PageUp)?.75f:Input.GetKeyDown(KeyCode.DownArrow)?-.12f:Input.GetKeyDown(KeyCode.UpArrow)?.12f:0;if(scrollStep!=0)_deskTipScroll.verticalNormalizedPosition=Mathf.Clamp01(_deskTipScroll.verticalNormalizedPosition+scrollStep);}}
  UpdateMapInspectionInput();
  if(EventSystem.current==null||!Input.GetKeyDown(KeyCode.Tab))return;
  Transform scope=_confirm!=null?_confirm.transform:_archiveOpen&&!_mapViewing?_archive:_root;
  var candidates=scope.GetComponentsInChildren<Selectable>(false);var controls=new System.Collections.Generic.List<Selectable>();foreach(var c in candidates)if(c.IsInteractable()&&c.gameObject.activeInHierarchy)controls.Add(c);
  if(DeskTipOpen&&_deskTipClose!=null&&!controls.Contains(_deskTipClose))controls.Add(_deskTipClose);if(controls.Count==0)return;
  var index=controls.FindIndex(c=>c.gameObject==EventSystem.current.currentSelectedGameObject);int step=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?-1:1;index=(index+step+controls.Count)%controls.Count;var next=controls[index];EventSystem.current.SetSelectedGameObject(next.gameObject);ScrollDeskFocus(next.transform);
 }
 void ScrollDeskFocus(Transform target){if(!_archiveOpen||!target.IsChildOf(_body))return;Canvas.ForceUpdateCanvases();var scroll=_archiveScroll.GetComponent<ScrollRect>();if(scroll==null)return;var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,target);var viewport=scroll.viewport.rect;float delta=bounds.min.y<viewport.yMin?viewport.yMin-bounds.min.y:bounds.max.y>viewport.yMax?viewport.yMax-bounds.max.y:0;float range=Mathf.Max(0,_body.rect.height-scroll.viewport.rect.height);var position=_body.anchoredPosition;position.y=Mathf.Clamp(position.y+delta,0,range);_body.anchoredPosition=position;}
}
}
