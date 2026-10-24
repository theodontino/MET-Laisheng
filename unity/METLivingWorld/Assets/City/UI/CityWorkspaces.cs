using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
public sealed partial class CityInterface {
 sealed class PageLayout {
  public readonly float Width,Height;public readonly string Placement,Scope;
  public PageLayout(float width,float height=860,string placement="dock",string scope="街区"){Width=width;Height=height;Placement=placement;Scope=scope;}
 }
 readonly Dictionary<string,PageLayout> _pageLayouts=new Dictionary<string,PageLayout>{
  {"population_home",new PageLayout(640,800)},{"economy_home",new PageLayout(680,800)},{"bonus_home",new PageLayout(760,800)},{"tasks_home",new PageLayout(720,800)},{"archive_home",new PageLayout(560,760)},
  {"overview",new PageLayout(560,800,"dock")},{"construction",new PageLayout(600,800,"dock")},
  {"building",new PageLayout(520,760,"dock")},{"population",new PageLayout(1040)},
  {"resources",new PageLayout(1200)},{"market",new PageLayout(1200)},{"policies",new PageLayout(1120)},
  {"bonuses",new PageLayout(1360)},{"slot",new PageLayout(1360)},{"blueprints",new PageLayout(1200)},
  {"themes",new PageLayout(1080)},{"milestones",new PageLayout(1080)},{"settlement",new PageLayout(1200)},
  {"city",new PageLayout(1360,860,"dock","城市")},{"governance",new PageLayout(960,820,"dock","地区")},
  {"network",new PageLayout(1280,860,"dock","城市")},{"science",new PageLayout(1280,860,"dock","城市")},
  {"city_project",new PageLayout(1040,820,"dock","城市")},{"heritage",new PageLayout(1200,860,"dock","城市")},
  {"records",new PageLayout(1200)},{"record",new PageLayout(1200,860,"dock","历史档案")},{"sources",new PageLayout(1200)},
  {"content",new PageLayout(1200)},{"start",new PageLayout(1240)},{"district_start",new PageLayout(1240,860,"dock","城市")},
  {"block_start",new PageLayout(1240,860,"dock","地区")},{"settings",new PageLayout(680,820,"dock","系统")},
  {"upgrades",new PageLayout(1120)},{"tender",new PageLayout(1120)}
 };
 RectTransform _workspaceActionBar;
 readonly Dictionary<string,string> _lastPageContexts=new Dictionary<string,string>();
 readonly HashSet<string> _openWorkspaceFolds=new HashSet<string>();
 readonly Dictionary<string,ScrollRect> _workspaceScrolls=new Dictionary<string,ScrollRect>();
 readonly Dictionary<string,Dictionary<string,float>> _pageScrollMemory=new Dictionary<string,Dictionary<string,float>>();
 string _renderedPageKey="";
 PageLayout CurrentPageLayout=>_pageLayouts.TryGetValue(_page,out var layout)?layout:new PageLayout(1040);
 string PageStateKey=>_cityIdentity+"/"+_page+"/"+_pageContext;
 string PageScopeLabel(){var scope=CurrentPageLayout.Scope;return scope=="街区"?"街区 · "+S["block"].Get("display_name","当前街区").Text:scope;}
 string PageSubtitle=>PageScopeLabel()+"  /  "+NavigationTrail()+"  ·  已暂停";
 void RememberWorkspace(){
  if(_renderedPageKey=="")return;var values=new Dictionary<string,float>();
  if(_archiveScroll!=null)values["body"]=_archiveScroll.GetComponent<ScrollRect>().verticalNormalizedPosition;
  foreach(var pair in _workspaceScrolls)if(pair.Value!=null)values[pair.Key]=pair.Value.verticalNormalizedPosition;
  _pageScrollMemory[_renderedPageKey]=values;
 }
 void ResetWorkspace(){
  RememberWorkspace();_workspaceScrolls.Clear();
  if(_workspaceActionBar==null)_workspaceActionBar=Row(_archive,"Workspace Actions",44);
  Clear(_workspaceActionBar);_workspaceActionBar.gameObject.SetActive(true);
  _renderedPageKey=PageStateKey;_lastPageContexts[_cityIdentity+"/"+_page]=_pageContext;
 }
 void RestoreWorkspace(){
  Canvas.ForceUpdateCanvases();_pageScrollMemory.TryGetValue(PageStateKey,out var memory);
  var root=_archiveScroll.GetComponent<ScrollRect>();root.StopMovement();root.verticalNormalizedPosition=memory!=null&&memory.TryGetValue("body",out var position)?position:1;
  foreach(var pair in _workspaceScrolls){pair.Value.StopMovement();pair.Value.verticalNormalizedPosition=memory!=null&&memory.TryGetValue(pair.Key,out var p)?p:1;}
 }
 RectTransform[] WorkspaceColumns(string name,params float[] weights){
  Canvas.ForceUpdateCanvases();
  float before=0;foreach(RectTransform child in _body)if(child.gameObject.activeSelf)before+=LayoutUtility.GetPreferredHeight(child)+8;
  float height=Mathf.Max(220,_archiveScroll.rect.height-32-before);
  var row=Row(_body,name,height);
  var layout=row.GetComponent<HorizontalLayoutGroup>();layout.spacing=12;layout.childForceExpandWidth=false;
  Height(row,height);var result=new List<RectTransform>();
  for(int i=0;i<weights.Length;i++){
   var panel=Surface(name+" column "+i,row,new Color(.045f,.085f,.105f,.97f));
   var size=panel.gameObject.AddComponent<LayoutElement>();size.minWidth=0;size.preferredWidth=0;size.flexibleWidth=weights[i];size.minHeight=height;size.preferredHeight=height;
   var viewport=Node(name+" viewport "+i,panel);Stretch(viewport,2,2,10,2);viewport.gameObject.AddComponent<RectMask2D>();viewport.gameObject.AddComponent<Image>().color=Color.clear;
   var content=Column(viewport,name+" content "+i,8);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
   var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=viewport;scroll.content=content;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=48;
   var track=Surface(name+" track "+i,panel,new Color(.10f,.17f,.19f));track.anchorMin=new Vector2(1,0);track.anchorMax=Vector2.one;track.offsetMin=new Vector2(-8,6);track.offsetMax=new Vector2(-3,-6);
   var thumb=Surface("Scroll thumb",track,new Color(.26f,.59f,.55f));Stretch(thumb);var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=thumb;bar.targetGraphic=thumb.GetComponent<Image>();scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
   _workspaceScrolls[name+":"+i]=scroll;result.Add(content);
  }
  return result.ToArray();
 }
 void WorkspaceActions(string caption,Action action,bool disabled=false){var button=Button(_workspaceActionBar,caption,action,true,disabled);Height((RectTransform)button.transform,44*TextScale);}
 void WorkspaceHint(string text){var label=Label(_workspaceActionBar,text,14,Muted,44*TextScale);label.alignment=TextAnchor.MiddleLeft;var size=label.GetComponent<LayoutElement>();size.minWidth=0;size.preferredWidth=0;size.flexibleWidth=2;}
 void OpenResourcePage(string resource){
  if(resource=="dt"){_settingsCategory="city";OpenArchive("settings");return;}
  if(resource=="population"){OpenArchive("population_home");return;}
  if(resource=="currency"){OpenArchive("economy_home");return;}
  Navigate(resource=="cp"?"construction":"resources",resource);
 }
}
}
