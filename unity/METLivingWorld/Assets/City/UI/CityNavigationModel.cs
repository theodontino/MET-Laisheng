using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace MET.City {
public sealed partial class CityInterface {
 enum PageRole { Common, Detail, Local, System, Locked }
 sealed class PageDefinition {
  public readonly string Section,Parent;public readonly PageRole Role;
  public PageDefinition(string section,PageRole role=PageRole.Common,string parent=""){Section=section;Role=role;Parent=parent;}
 }
 readonly Dictionary<string,PageDefinition> _pages=new Dictionary<string,PageDefinition>{
  {"population_home",new PageDefinition("population_home")},{"population",new PageDefinition("population_home",PageRole.Detail,"population_home")},
  {"construction",new PageDefinition("construction")},
  {"economy_home",new PageDefinition("economy_home")},{"market",new PageDefinition("economy_home")},{"policies",new PageDefinition("economy_home")},
  {"resources",new PageDefinition("economy_home",PageRole.Detail,"economy_home")},
  {"bonus_home",new PageDefinition("bonus_home")},{"bonuses",new PageDefinition("bonus_home",PageRole.Detail,"bonus_home")},
  {"tasks_home",new PageDefinition("tasks_home")},{"themes",new PageDefinition("tasks_home",PageRole.Detail,"tasks_home")},
  {"blueprints",new PageDefinition("tasks_home",PageRole.Detail,"tasks_home")},{"settlement",new PageDefinition("tasks_home",PageRole.Detail,"tasks_home")},
  {"records",new PageDefinition("records")},{"record",new PageDefinition("records",PageRole.Detail,"records")},
  {"sources",new PageDefinition("records",PageRole.Detail,"records")},{"heritage",new PageDefinition("records",PageRole.Detail,"records")},{"content",new PageDefinition("records",PageRole.Detail,"records")},
  {"building",new PageDefinition("construction",PageRole.Local,"construction")},{"upgrades",new PageDefinition("construction",PageRole.Local,"construction")},
  {"slot",new PageDefinition("bonus_home",PageRole.Local,"bonus_home")},{"tender",new PageDefinition("construction",PageRole.Local)},
  {"settings",new PageDefinition("settings",PageRole.System)},{"start",new PageDefinition("start",PageRole.System)},
  {"overview",new PageDefinition("city",PageRole.Locked)},{"city",new PageDefinition("city",PageRole.Locked)},
  {"governance",new PageDefinition("city",PageRole.Locked)},{"network",new PageDefinition("city",PageRole.Locked)},
  {"science",new PageDefinition("city",PageRole.Locked)},{"city_project",new PageDefinition("city",PageRole.Locked)},
  {"district_start",new PageDefinition("city",PageRole.Locked)},{"block_start",new PageDefinition("city",PageRole.Locked)}
 };
 sealed class SectionVisit {public string Page,Context,Owner;public bool Advanced;public KeyValuePair<string,string>[] Parents;}
 readonly Dictionary<string,bool> _parentPageModes=new Dictionary<string,bool>();
 readonly Dictionary<string,SectionVisit> _sectionVisits=new Dictionary<string,SectionVisit>();
 string _contextSection="";Button _pageBack;
 PageDefinition Definition(string page)=>_pages.TryGetValue(page,out var spec)?spec:new PageDefinition("records",PageRole.Locked);
 string ArchiveCategory(string page){var definition=Definition(page);return definition.Role==PageRole.Local&&_contextSection!=""?_contextSection:definition.Section;}
 bool IsSidebarPage(string page)=>Definition(page).Role==PageRole.Common;
 string SidebarForPage(string page)=>ArchiveCategory(page);
 bool HasLocalTabs=>Definition(_page).Role!=PageRole.Local&&(ArchiveCategory(_page)=="economy_home"||(ArchiveCategory(_page)=="tasks_home"&&_advancedWorkspace)||ArchiveCategory(_page)=="records");
 void UpdateArchiveTabs(){foreach(var item in _topRoutes){bool selected=_archiveOpen&&item.Key==ArchiveCategory(_page);item.Value.GetComponent<Image>().color=selected?new Color(.10f,.25f,.27f,.98f):_hudButtonFill;}}
 void UpdateLocalArchiveTabs(){
  Clear(_localNav);_localNav.gameObject.SetActive(HasLocalTabs);if(!HasLocalTabs)return;
  string[][] routes;string active=_page;
  switch(ArchiveCategory(_page)){
   case "economy_home":routes=new[]{new[]{"economy_home","收支",""},new[]{"market","市场",""},new[]{"policies","政策",""}};if(_page=="resources")active="economy_home";break;
   case "tasks_home":routes=new[]{new[]{"tasks_home","当前事项","current"},new[]{"tasks_home","成长路线","growth"},new[]{"tasks_home","已完成","completed"}};active=_pageContext=="growth"||_pageContext=="completed"?_pageContext:"current";break;
   default:routes=new[]{new[]{"records","记录",""},new[]{"content","成果",""},new[]{"heritage","遗产",""},new[]{"sources","规则与来源",""}};if(_page=="record")active="records";break;
  }
  foreach(var route in routes){var id=route[0];var context=route[2];string key=context==""?id:context;var button=Tab(_localNav,route[1],key,()=>{bool expanded=_advancedWorkspace;Navigate(id,context);if(expanded&&!_advancedWorkspace){_advancedWorkspace=true;RenderPage();}},active);Height((RectTransform)button.transform,39*TextScale);HudFont(button.GetComponentInChildren<Text>(),15);}
 }
 string NormalizePage(string page)=>page=="archive_home"?"records":page=="tasks"||page=="themes"||page=="milestones"?"tasks_home":page;
 string TaskContext(string page,string context)=>context=="current"||context=="growth"||context=="completed"?context:page=="milestones"?"growth":"current";
 bool TryOpenConstructionRoute(string page,string context=""){
  if(page=="upgrades"){OpenUpgradeWindow(context!=""?context:SelectedBuilding);return true;}
  if(page!="tender")return false;
  var draft=S["draft"];if(draft.Count==0){ShowMessage("先选择一块空闲陆地，再打开建造方案。");return true;}
  OpenConstructionWindow(draft["base"].Text,draft["tile"].Text);return true;
 }
 bool PageAvailable(string page){if(Definition(page).Role!=PageRole.Locked)return true;ShowMessage("永久城市管理暂未开放；已有城市记录保留在档案中。");return false;}
 void RememberSection(){
  if(!_archiveOpen)return;RememberWorkspace();var section=ArchiveCategory(_page);
  if(section=="settings"||section=="start"||section=="city")return;
  _sectionVisits[_cityIdentity+"/"+section]=new SectionVisit{Page=_page,Context=_pageContext,Owner=_contextSection,Parents=_navigation.ToArray(),Advanced=_advancedWorkspace};
 }
 void PrepareNavigation(string page){
  CloseObjectDrawer(false);CancelDeskTipsForPanel();DismissMapInspection();EndConstructionIntent();if(ConstructionWindowOpen)CloseConstructionWindow();if(_mapViewing)ReturnFromMapView();
  if(_page!=page&&_comparisonValid)ClearComparison();
  if(!_archiveOpen){_archiveEntryFocus=EventSystem.current?.currentSelectedGameObject;_archiveOpen=true;Runtime.SetDecisionOpen(true);}_shield.SetActive(true);
 }
 void SetCanonicalParents(string page,string context=""){
  _navigation.Clear();var parents=new List<string>();var parent=Definition(page).Parent;
  while(parent!=""){parents.Add(parent);parent=Definition(parent).Parent;}
  for(int i=parents.Count-1;i>=0;i--){var id=parents[i];_navigation.Push(new KeyValuePair<string,string>(id,_lastPageContexts.TryGetValue(_cityIdentity+"/"+id,out var saved)?saved:""));}
 }
 public void OpenArchive(string page="population_home"){
  if(TryOpenConstructionRoute(page))return;
  string requested=page;page=NormalizePage(page);if(!PageAvailable(page))return;
  string context=_lastPageContexts.TryGetValue(_cityIdentity+"/"+page,out var saved)?saved:"";
  if(page=="tasks_home")context=TaskContext(requested,requested=="tasks_home"?context:"");
  RememberSection();PrepareNavigation(page);_advancedWorkspace=IsAdvancedRoute(page);_contextSection="";SetCanonicalParents(page,context);_page=page;_pageContext=context;RenderPage();
 }
 void OpenSection(string section){
  if(!PageAvailable(section))return;bool same=_archiveOpen&&ArchiveCategory(_page)==section;RememberSection();
  if(!same&&_sectionVisits.TryGetValue(_cityIdentity+"/"+section,out var visit)){
   PrepareNavigation(visit.Page);_advancedWorkspace=visit.Advanced;_page=visit.Page;_pageContext=visit.Context;_contextSection=visit.Owner;_navigation.Clear();
   for(int i=visit.Parents.Length-1;i>=0;i--)_navigation.Push(visit.Parents[i]);RenderPage();return;
  }
  if(section=="tasks_home")_lastPageContexts[_cityIdentity+"/tasks_home"]="current";
  OpenArchive(section);
 }
 void Navigate(string page,string context=""){
  if(page=="building"){OpenObjectDrawer(page,context);return;}
  if(TryOpenConstructionRoute(page,context))return;
  string requested=page;page=NormalizePage(page);if(page=="tasks_home")context=TaskContext(requested,context);if(!PageAvailable(page))return;
  bool local=Definition(page).Role==PageRole.Local,opened=_archiveOpen;string owner=opened?ArchiveCategory(_page):Definition(page).Section;
  bool keepManagement=CivicManagementPage&&HasCivicManagementPage(page);
  RememberSection();PrepareNavigation(page);
  if(local){
   if(opened&&_navigation.Any(frame=>frame.Key==page&&frame.Value==context)){while(_navigation.Count>0){var parent=_navigation.Pop();if(parent.Key==page&&parent.Value==context)break;}}
   else if(opened&&_page!=page){_parentPageModes[_cityIdentity+"/"+_page+"/"+_pageContext]=_advancedWorkspace;_navigation.Push(new KeyValuePair<string,string>(_page,_pageContext));}
   else if(!opened)SetCanonicalParents(page);
   _contextSection=owner;
  }else{_contextSection="";SetCanonicalParents(page,context);}
  _advancedWorkspace=IsAdvancedRoute(page)||keepManagement;_page=page;_pageContext=context;RenderPage();
 }
 void Back(){
  if(ObjectDrawerOpen&&!ConstructionWindowOpen&&!_mapViewing){CloseObjectDrawer();return;}
  if(ConstructionWindowOpen){CloseConstructionWindow();return;}if(_mapViewing){ReturnFromMapView();return;}RememberSection();
  if(_advancedWorkspace&&HasCompactVersion(_page)){CollapseAdvancedWorkspace();return;}if(_navigation.Count==0){CloseArchive();return;}
  if(_comparisonValid)ClearComparison();var parent=_navigation.Pop();_page=parent.Key;_pageContext=parent.Value;
  if(Definition(_page).Role!=PageRole.Local)_contextSection="";_advancedWorkspace=_parentPageModes.TryGetValue(_cityIdentity+"/"+_page+"/"+_pageContext,out var advanced)?advanced:IsAdvancedRoute(_page);RenderPage();
 }
 string NavigationTrail(){
  string section=ArchiveCategory(_page);string sectionName=section=="economy_home"?"资源与财政":section=="tasks_home"?"任务与成长":PageTitle(section);
  if(section=="start"||section=="settings")return PageTitle(_page);
  string suffix=_page=="tasks_home"?(_pageContext=="growth"?"成长路线":_pageContext=="completed"?"已完成":"当前事项"):_page==section?"":PageTitle(_page);
  if(_page=="resources"){var resource=_pageContext==""?(CivicManagementPage?CivicOMState("economy","food").view:"food"):_pageContext;suffix+=" · "+(resource=="energy"?"电力供需":resource=="currency"?"财政":Resource(resource));}
  if(Definition(_page).Role==PageRole.Local&&_navigation.Count>0&&_navigation.Peek().Key!=section)suffix=PageTitle(_navigation.Peek().Key)+" › "+suffix;
  return sectionName+(suffix==""?"":" › "+suffix);
 }
}
}
