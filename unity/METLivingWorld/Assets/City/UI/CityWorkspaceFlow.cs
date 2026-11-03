using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MET.City {
public sealed partial class CityInterface {
 RectTransform _objectDrawer,_objectBody,_objectActions;
 ScrollRect _objectScroll;
 Text _objectTitle;
 string _objectPage="",_objectContext="",_trackedGoal="";
 bool _objectDrawerSuspended;
 public bool ObjectDrawerOpen=>_objectPage!="";
 bool ObjectDrawerVisible=>ObjectDrawerOpen&&!_objectDrawerSuspended;
 float ObjectDrawerWidth=>570;

 bool ReleaseTextInput(){if(CivicEnabled&&_civic.ReleaseText())return true;
  var selected=EventSystem.current?.currentSelectedGameObject;
  var input=selected!=null?selected.GetComponentInParent<InputField>():null;
  if(input==null||!input.isFocused)return false;
  input.DeactivateInputField();EventSystem.current.SetSelectedGameObject(null);return true;
 }
 void OpenObjectDrawer(string page,string context){if(CivicEnabled){OpenCivicDrawer(page,context);return;}
  if(page!="building"||!S["projects"].Has(context))return;
  CancelDeskTipsForPanel();DismissMapInspection();
  bool changed=_objectContext!=context;
  _objectPage=page;_objectContext=context;_objectDrawerSuspended=false;Runtime.SetDecisionOpen(true);
  if(_objectDrawer==null){
   _objectDrawer=Surface("Object inspection drawer",_root,Panel);
   _objectTitle=Label(_objectDrawer,"",21,Ink);
   var close=Button(_objectDrawer,"×",()=>CloseObjectDrawer());
   Pin((RectTransform)close.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-12,-14),new Vector2(36,36));
   var viewport=Surface("Object inspection viewport",_objectDrawer,new Color(.02f,.05f,.06f,.48f));
   Stretch(viewport,12,74,12,112);viewport.gameObject.AddComponent<RectMask2D>();
   _objectScroll=viewport.gameObject.AddComponent<ScrollRect>();_objectScroll.horizontal=false;_objectScroll.scrollSensitivity=48;_objectScroll.movementType=ScrollRect.MovementType.Clamped;_objectScroll.viewport=viewport;
   _objectBody=Column(viewport,"Object inspection contents",8);_objectBody.anchorMin=new Vector2(0,1);_objectBody.anchorMax=Vector2.one;_objectBody.pivot=new Vector2(.5f,1);_objectBody.sizeDelta=Vector2.zero;_objectBody.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(8,8,6,10);_objectScroll.content=_objectBody;
   _objectActions=Row(_objectDrawer,"Object inspection actions",56*TextScale);
  }
  _objectDrawer.gameObject.SetActive(true);RefreshObjectDrawer();if(changed)_objectScroll.verticalNormalizedPosition=1;
  if(_archiveOpen)LayoutArchive();else LayoutObjectDrawer();RefreshModalOwnership(true);
 }
 void RefreshObjectDrawer(){if(CivicEnabled){RefreshCivicDrawer();return;}
  if(!ObjectDrawerOpen||_objectDrawer==null)return;
  if(!S["projects"].Has(_objectContext)){CloseObjectDrawer();return;}
  var scroll=_objectScroll.verticalNormalizedPosition;Clear(_objectBody);Clear(_objectActions);
  _objectTitle.text=G.project_name(_objectContext)+" · "+S["projects"][_objectContext]["tile"].Text;
  // Only the drawing targets are borrowed; workspace route, scrolls and history remain intact.
  var body=_body;var actions=_workspaceActionBar;var context=_pageContext;
  try{_body=_objectBody;_workspaceActionBar=_objectActions;_pageContext=_objectContext;BuildingPage();}
  finally{_body=body;_workspaceActionBar=actions;_pageContext=context;}
  // Three common actions share one fixed row; overflow content stays inside the drawer scroll.
  var row=_objectActions.GetComponent<HorizontalLayoutGroup>();row.spacing=6;row.childControlWidth=row.childControlHeight=true;row.childForceExpandWidth=true;row.childForceExpandHeight=false;
  foreach(var button in _objectActions.GetComponentsInChildren<Button>()){var text=button.GetComponentInChildren<Text>();HudFont(text,14);var size=button.GetComponent<LayoutElement>();size.minWidth=0;size.preferredWidth=0;size.flexibleWidth=1;Height((RectTransform)button.transform,56*TextScale);}
  LayoutObjectDrawer();Canvas.ForceUpdateCanvases();_objectScroll.StopMovement();_objectScroll.verticalNormalizedPosition=scroll;
 }
 void LayoutObjectDrawer(){if(CivicEnabled){SyncCivicVisibility();return;}
  if(_objectDrawer==null)return;
  _objectDrawer.gameObject.SetActive(ObjectDrawerVisible);
  if(!ObjectDrawerOpen)return;
  var width=_root.rect.width;var height=_root.rect.height;float top=64*TextScale+12;
  float left=_archiveOpen?(_advancedWorkspace?width-ObjectDrawerWidth-16:182+CurrentPageLayout.Width+12):width-ObjectDrawerWidth-16;
  Pin(_objectDrawer,new Vector2(0,1),new Vector2(0,1),new Vector2(left,-top),new Vector2(ObjectDrawerWidth,height-top-108));
  Pin(_objectTitle.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-16),new Vector2(ObjectDrawerWidth-78,48*TextScale));
  Stretch(_objectScroll.viewport,12,72*TextScale,12,82*TextScale);
  Pin(_objectActions,new Vector2(0,0),new Vector2(0,0),new Vector2(12,12),new Vector2(ObjectDrawerWidth-24,56*TextScale));
 }
 void CloseObjectDrawer(bool layout=true){
  if(!ObjectDrawerOpen)return;
  CancelDeskTipsForPanel();_objectPage="";_objectContext="";
  if(_objectDrawer!=null){_objectDrawer.gameObject.SetActive(false);Clear(_objectBody);Clear(_objectActions);}
  Runtime.SetDecisionOpen(_archiveOpen||ConstructionWindowOpen||ToolkitUpgradeOpen||CivicOperationOpen);if(CivicEnabled){_civic.Remember("drawer");if(_advancedWorkspace)_civic.Q("civic-workspace").style.right=16;SyncCivicVisibility();}
  if(layout){if(_archiveOpen)LayoutArchive();RefreshModalOwnership(true);RefreshContext();}
 }

 void HandleGoalDestination(string id){
  _trackedGoal=id;
  if(id.StartsWith("delivery_",StringComparison.Ordinal)){_operatingEconomyResource=id.Substring(9);Navigate("market",_operatingEconomyResource);return;}
  if(id=="energy"){_operatingEconomyResource="energy";Navigate("resources","energy");return;}
  if(id=="goods_rate"||id=="industrial_usage"){_operatingEconomyResource="goods";Navigate("resources","goods");return;}
  if(id=="commercial_usage"){_operatingResidentFilter="commercial";Navigate("population_home");return;}
  if(id=="trade"){Navigate("market");return;}
  if(id=="currency"){_operatingEconomyResource="currency";Navigate("economy_home");return;}
  if(id=="food_stock_months"||id=="food_self_sufficiency"||id=="food_surplus"){_operatingEconomyResource="food";Navigate("economy_home");return;}
  if(new[]{"housed","homeless","health","happiness","industrial_labor","commercial_people","commercial_person_months","food","medical","education","commercial","entertainment"}.Contains(id)){
   _operatingResidentFilter=id=="homeless"?"homeless":new[]{"food","medical","education","commercial","entertainment"}.Contains(id)?id:"all";
   Navigate("population_home");return;
  }
  Navigate("construction");
 }
 void RenderGoalContext(){
  if(_trackedGoal==""||ArchiveCategory(_page)=="tasks_home")return;
  var view=Themes.view(G);var goal=view.Get("goals",V.Arr()).FirstOrDefault(g=>g.Get("id","").Text==_trackedGoal);
  var label=goal!=null?goal.Get("label","当前目标").Text:V.Map("energy","电力满足","goods_rate","制成品产出","industrial_usage","工业利用率","industrial_labor","工业用工","commercial_usage","商业利用率","commercial_people","商业服务人数","commercial_person_months","商业累计服务","trade","贸易成交","currency","通货余额").Get(_trackedGoal,"当前任务目标").Text;
  var row=Row(_body,"Tracked task context",38*TextScale);var text=Label(row,"正在处理 · "+label,14,Gold,38*TextScale);text.alignment=TextAnchor.MiddleLeft;
  var back=Button(row,"回到此目标 ›",()=>{_trackedGoal="";Navigate("tasks_home","current");});Width((RectTransform)back.transform,150*TextScale);Height((RectTransform)back.transform,36*TextScale);
 }
}
}
