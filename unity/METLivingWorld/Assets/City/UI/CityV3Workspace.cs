using System;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
public sealed partial class CityInterface {
 bool _advancedWorkspace, _mapFromCompact;
 Button _workspaceExpand;
 RectTransform _workspaceHeading;
 bool CompactWorkspaceOpen => _archiveOpen&&!_mapViewing&&!_advancedWorkspace&&HasCompactVersion(_page);
 public bool MapInteractionAvailable => !IsModal&&(!_mapViewing||_mapFromCompact);
 bool HasCompactVersion(string page)=>page=="population_home"||page=="population"||page=="economy_home"||page=="resources"||page=="construction"||page=="market"||page=="tasks_home"||page=="bonus_home"||page=="bonuses";
 bool IsAdvancedRoute(string page)=>page=="population"||page=="resources"||page=="bonuses"||(!HasCompactVersion(page)&&page!="settings");
 void OpenAdvancedWorkspace(string page=null,string context=null){
  page=page??_page;context=context??(_page==page?_pageContext:"");
  if(page=="population_home")page="population";
  if(page=="economy_home"){page="resources";if(context=="")context=_operatingEconomyResource;}
  Navigate(page,context);RememberWorkspace();_advancedWorkspace=true;RenderPage();
 }
 void CollapseAdvancedWorkspace(){
  RememberWorkspace();string page=_page;
  if(page=="population")page="population_home";
  if(page=="resources"){_operatingEconomyResource=_pageContext==""?"food":_pageContext;page="economy_home";}
  if(page=="bonuses")page="bonus_home";
  if(_comparisonValid)ClearComparison();_advancedWorkspace=false;_contextSection="";_page=page;
  if(page=="tasks_home")_pageContext="current";else if(page=="economy_home"||page=="population_home"||page=="bonus_home")_pageContext="";
  SetCanonicalParents(page);RenderPage();
 }
 void ShowLocalMapFromCompact(){
  RememberWorkspace();CancelDeskTipsForPanel();DismissMapInspection();
  _mapFromCompact=true;_mapViewing=true;_shield.SetActive(false);
  _returnComparison.GetComponentInChildren<Text>().text="返回「"+PageTitle(_page)+"」 ›";
  _returnComparison.gameObject.SetActive(true);RefreshHudGeometry();RefreshModalOwnership();
 }
 void LayoutV3Workspace(){
  if(_archive==null)return;
  float ui=Mathf.Clamp((float)Runtime.DisplaySettings.Get("ui_scale",1).Double,.8f,1.2f);
  float width=_root.rect.width>100?_root.rect.width:2560/ui,height=_root.rect.height>100?_root.rect.height:1440/ui;
  float font=TextScale,left=182,top=64*font+12,pad=_advancedWorkspace?20:14;
  bool compact=!_advancedWorkspace&&HasCompactVersion(_page),canExpand=HasCompactVersion(_page);
  float panelWidth=_advancedWorkspace?width-left-16:CurrentPageLayout.Width;
  float panelHeight=Mathf.Min(_advancedWorkspace?height-top-108:CurrentPageLayout.Height,height-top-108);
  Pin(_archive,new Vector2(0,1),new Vector2(0,1),new Vector2(left,-top),new Vector2(panelWidth,panelHeight));
  _archive.GetComponent<Image>().color=Panel;
  var shield=_shield.GetComponent<Image>();shield.color=compact?Color.clear:new Color(.005f,.015f,.02f,.22f);shield.raycastTarget=!compact;
  if(_workspaceHeading==null){
   _workspaceHeading=Surface("Workspace heading",_archive,new Color(.137f,.235f,.224f,.99f));_workspaceHeading.SetAsFirstSibling();_workspaceHeading.GetComponent<Image>().color=new Color(.137f,.235f,.224f,.99f);_workspaceHeading.GetComponent<Image>().raycastTarget=false;
   _workspaceExpand=Button(_archive,"详表",()=>{if(_advancedWorkspace)CollapseAdvancedWorkspace();else OpenAdvancedWorkspace();});
  }
  float heading=86*font;
  Pin(_workspaceHeading,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(panelWidth,heading));
  HudFont(_title,compact?21:24);HudFont(_subtitle,12);
  Pin(_subtitle.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(pad,-9),new Vector2(panelWidth-pad-150,22*font));
  Pin(_title.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(pad,-34*font),new Vector2(panelWidth-pad-150,38*font));
  var close=_archive.Find("Button ×") as RectTransform;
  if(close!=null)Pin(close,new Vector2(1,1),new Vector2(1,1),new Vector2(-pad,-25*font),new Vector2(38,36*font));
  _workspaceExpand.gameObject.SetActive(canExpand);
  _workspaceExpand.GetComponentInChildren<Text>().text=_advancedWorkspace?"收回侧栏":"详表 ↗";HudFont(_workspaceExpand.GetComponentInChildren<Text>(),12);
  Pin((RectTransform)_workspaceExpand.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-pad-46,-25*font),new Vector2(82,36*font));
  _pageNav.gameObject.SetActive(false);_footer.gameObject.SetActive(false);
  bool back=_navigation.Count>0&&!(_advancedWorkspace&&canExpand);
  _pageBack.gameObject.SetActive(back);
  float bodyTop=heading+10;
  if(back){_pageBack.GetComponentInChildren<Text>().text="‹ 返回"+PageTitle(_navigation.Peek().Key);Pin((RectTransform)_pageBack.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(pad,-bodyTop),new Vector2(panelWidth-2*pad,34*font));bodyTop+=40*font;}
  _localNav.gameObject.SetActive(HasLocalTabs);
  if(HasLocalTabs){Pin(_localNav,new Vector2(0,1),new Vector2(0,1),new Vector2(pad,-bodyTop),new Vector2(panelWidth-2*pad,38*font));bodyTop+=46*font;}
  bool actions=_workspaceActionBar!=null&&_workspaceActionBar.childCount>0;
  float bottom=actions?62*font:16;
  if(_workspaceActionBar!=null){_workspaceActionBar.gameObject.SetActive(actions);Pin(_workspaceActionBar,new Vector2(0,0),new Vector2(0,0),new Vector2(pad,10),new Vector2(panelWidth-2*pad,44*font));}
  Stretch(_archiveScroll,pad,bodyTop,pad+6,bottom);
  _archiveScrollTrack.anchorMin=new Vector2(1,0);_archiveScrollTrack.anchorMax=Vector2.one;_archiveScrollTrack.offsetMin=new Vector2(-8,bottom);_archiveScrollTrack.offsetMax=new Vector2(-4,-bodyTop);
  var layout=_body.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(compact?2:10,compact?2:10,4,8);layout.spacing=compact?8:10;
  if(_returnComparison!=null)Pin((RectTransform)_returnComparison.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(left,-top),new Vector2(310,42*font));
 }
}
}
