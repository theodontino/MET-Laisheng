using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UIElements;
using C=MET.City.CivicUI;

namespace MET.City {
public sealed partial class CityInterface {
 CivicToolkitView _civic;
 string _civicOperation="",_civicReward="";
 string _civicNoticeKey="",_civicTileKey="";
 bool _civicRendering,_civicPendingRefresh;
 Action<VisualElement,VisualElement> _civicOperationRender;
 Action _civicOperationClose;
 public bool CivicEnabled=>_civic!=null&&_civic.Ready;
 public CivicToolkitView CivicView=>_civic;
 bool CivicOperationOpen=>CivicEnabled&&_civicOperation!="";
 bool HasCivicManagementPage(string page)=>new[]{"population_home","population","economy_home","resources","construction","market","policies","tasks_home","themes","milestones","bonus_home","bonuses","records","archive_home","sources","content","heritage"}.Contains(page);
 bool CivicManagementPage=>CivicEnabled&&_advancedWorkspace&&HasCivicManagementPage(_page);
 string CivicManagementTitle=>ArchiveCategory(_page)=="population_home"?"住户、就业与服务管理":ArchiveCategory(_page)=="economy_home"?"资源与财政管理":ArchiveCategory(_page)=="construction"?"工程与施工队调度":ArchiveCategory(_page)=="tasks_home"?"任务与成长档案":ArchiveCategory(_page)=="bonus_home"?"Bonus 配装管理":"城市档案";

 void BuildCivicInterface(){
  _civic=gameObject.AddComponent<CivicToolkitView>();_civic.Initialize();
  RefreshCivicHud();ApplyCivicDisplay();SyncCivicVisibility();
 }
 void ApplyCivicDisplay(){if(!CivicEnabled)return;_civic.ApplyDisplay((float)Runtime.DisplaySettings.Get("ui_scale",1).Double,TextScale,Runtime.DisplaySettings.Get("high_contrast",false).Bool);}
 void LateUpdate(){
  if(!CivicEnabled)return;
  SyncCivicVisibility();
  if(_civicPendingRefresh&&!PointerBusy&&!_civic.TextFocused){_civicPendingRefresh=false;CivicCommitted();}
 }
 void CivicCommitted(){
  if(!CivicEnabled)return;
  RefreshCivicHud();RefreshCivicTile();
  if(_civic.TextFocused||PointerBusy){_civicPendingRefresh=true;return;}
  if(_archiveOpen&&!_mapViewing)RenderCivicPage();
  RefreshCivicDrawer();
  if(_civicOperation=="reward")RenderCivicRewardOperation();
 }
 void SyncCivicVisibility(){
  if(!CivicEnabled)return;
  // Retain old entry-point objects while migrating their callers; they neither draw nor receive input.
  // Numeric map labels are a world-space overlay and keep their existing projection.
  foreach(Transform child in _root){
   if(child.name=="Map numeric reading")continue;
   var group=child.GetComponent<CanvasGroup>()??child.gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;group.interactable=false;
  }
  bool operation=CivicOperationOpen||ToolkitUpgradeOpen;
  bool full=_archiveOpen&&!_mapViewing&&_advancedWorkspace&&!operation;
  foreach(var name in new[]{"top","nav","layers","clock"})_civic.Visible("civic-"+name,!full);
  _civic.Visible("civic-workspace",_archiveOpen&&!_mapViewing&&!operation);
  _civic.Visible("civic-workspace-shade",_archiveOpen&&!_mapViewing&&_advancedWorkspace&&!operation);
  _civic.Visible("civic-drawer",ObjectDrawerVisible&&!operation);
  _civic.Visible("civic-tile",(!_archiveOpen||_mapFromCompact)&&(!_mapViewing||_mapFromCompact)&&!operation&&!ObjectDrawerVisible&&SelectedTile!=""&&S["cells"].Has(SelectedTile));
  _civic.Visible("civic-map-return",_mapViewing&&!operation);
  _civic.Visible("civic-guide",!operation&&!ObjectDrawerVisible&&(!_archiveOpen||CompactWorkspaceOpen));
  _civic.Visible("civic-notices",!operation&&(!_archiveOpen||CompactWorkspaceOpen)&&_civic.Q("civic-notices").childCount>0);
  _civic.Visible("civic-toast",UnityEngine.Time.unscaledTime<_toastUntil&&!operation);
  _civic.Visible("civic-operation-shade",CivicOperationOpen&&!ToolkitUpgradeOpen);
  foreach(var name in new[]{"top","nav","layers","clock","workspace","tile","drawer","guide","notices","map-return"})_civic.Q("civic-"+name).SetEnabled(!operation);
  if(_mapViewing){var back=_civic.Q("civic-map-return");if(back.childCount==0)C.Button(back,"返回「"+PageTitle(_page)+"」",ReturnFromMapView,true);}
  else _civic.Q("civic-map-return").Clear();
 }
 void CivicHeader(string region,string title,string subtitle,Action close,bool advanced=false){
  var head=_civic.Q("civic-"+region+"-head");head.Clear();
  if(region=="tile"){var compact=C.Row(head);C.Label(compact,title,"c-heading c-grow");C.Label(compact,subtitle,"c-small c-muted");C.Button(compact,"×",close,false,true,"civic-tile-close").AddToClassList("c-icon-button");_civic.ApplyText(head);return;}
  C.Label(head,subtitle,"c-small c-muted");var line=C.Row(head);C.Label(line,title,"c-hero c-grow");
  if(region=="workspace"&&HasCompactVersion(_page))C.Button(line,advanced?"经营概览":"打开管理页 ↗",()=>{if(_advancedWorkspace)CollapseAdvancedWorkspace();else OpenAdvancedWorkspace();},false,true,"civic-expand");
  C.Button(line,"×",close,false,true,"civic-"+region+"-close").AddToClassList("c-icon-button");_civic.ApplyText(head);
  if(region=="workspace"&&advanced){
   var modules=C.Row(head,"c-management-modules");
   foreach(var item in new[]{new[]{"population","住户与服务"},new[]{"resources","资源与财政"},new[]{"construction","施工调度"},new[]{"tasks_home","任务与成长"},new[]{"bonuses","Bonus 配装"},new[]{"records","城市档案"}}){var route=item;C.Button(modules,route[1],()=>OpenCivicManagementModule(route[0]),ArchiveCategory(_page)==ArchiveCategory(route[0]));}
   _civic.ApplyText(head);
  }
 }
 void OpenCivicManagementModule(string fallback){
  string section=ArchiveCategory(fallback);
  if(ArchiveCategory(_page)==section)return;
  if(_sectionVisits.TryGetValue(_cityIdentity+"/"+section,out var visit)&&HasCivicManagementPage(visit.Page))OpenAdvancedWorkspace(visit.Page,visit.Context);
  else OpenAdvancedWorkspace(fallback);
 }
 void RenderCivicPage(){
  if(!CivicEnabled||_civicRendering)return;
  if(_civicOperation=="reward"){RenderCivicRewardOperation();return;}
  _civicRendering=true;
  try{
   _lastPageContexts[_cityIdentity+"/"+_page]=_pageContext;
   _civic.SetManagement(CivicManagementPage);
   CivicHeader("workspace",CivicManagementPage?CivicManagementTitle:_page=="market"||_page=="policies"?"资源与财政":PageTitle(_page),PageSubtitle,CloseArchive,_advancedWorkspace);
   var panel=_civic.Q("civic-workspace");panel.EnableInClassList("is-wide",_advancedWorkspace);panel.style.right=_advancedWorkspace?new StyleLength(ObjectDrawerVisible?680:16):new StyleLength(StyleKeyword.Null);
   panel.style.left=_advancedWorkspace?16:194+152*(TextScale-1)+32;panel.style.top=_advancedWorkspace?16:112*TextScale+16;
   panel.style.width=_advancedWorkspace?new StyleLength(StyleKeyword.Auto):new StyleLength(820);
   var tabs=_civic.Q("civic-workspace-tabs");tabs.Clear();
   if(ArchiveCategory(_page)=="economy_home")foreach(var pair in new[]{new[]{"economy_home","收支"},new[]{"market","市场"},new[]{"policies","政策"}}){var p=pair;C.Button(tabs,p[1],()=>Navigate(p[0]),_page==p[0]||p[0]=="economy_home"&&_page=="resources");}
   else if(ArchiveCategory(_page)=="tasks_home"&&_page=="tasks_home"&&!CivicManagementPage)foreach(var pair in new[]{new[]{"current","当前事项"},new[]{"growth","成长路线"},new[]{"completed","已完成"}}){var p=pair;C.Button(tabs,p[1],()=>Navigate("tasks_home",p[0]),(_pageContext==""?"current":_pageContext)==p[0]);}
   else if(ArchiveCategory(_page)=="records")foreach(var pair in new[]{new[]{"records","记录"},new[]{"content","成果"},new[]{"heritage","遗产"},new[]{"sources","规则来源"}}){var p=pair;C.Button(tabs,p[1],()=>Navigate(p[0]),_page==p[0]);}
   tabs.style.display=tabs.childCount==0?DisplayStyle.None:DisplayStyle.Flex;
   _civic.Render("workspace",PageStateKey,(body,actions)=>{
    if(!CivicManagementPage&&_navigation.Count>0&&!HasCompactVersion(_page)&&_page!="start")C.Button(body,"‹ 返回"+PageTitle(_navigation.Peek().Key),Back).AddToClassList("c-quiet");
    if(_trackedGoal!=""&&ArchiveCategory(_page)!="tasks_home"){var row=C.Row(body);C.Label(row,"正在处理任务目标","c-small c-warning");C.Button(row,"回到此目标",()=>{_trackedGoal="";Navigate("tasks_home","current");});}
    bool handled=CivicManagementPage&&(RenderCivicManagementOperating(body,actions)||RenderCivicManagementDevelopment(body,actions));
    if(!handled&&!RenderCivicOperating(body,actions)&&!RenderCivicDevelopment(body,actions)&&!RenderCivicRecords(body,actions))C.Empty(body,"当前入口未开放","已有城市与历史记录均保留。");
   });
   _civic.ApplyText(tabs);RefreshCivicNavigation();RefreshCivicDrawer();SyncCivicVisibility();
  }finally{_civicRendering=false;}
 }
 void OpenCivicDrawer(string page,string context){
  if(page!="building"||!S["projects"].Has(context))return;
  _objectPage=page;_objectContext=context;_objectDrawerSuspended=false;Runtime.SetDecisionOpen(true);RefreshCivicDrawer();SyncCivicVisibility();
 }
 void RefreshCivicDrawer(){
  if(!CivicEnabled||!ObjectDrawerOpen)return;
  if(!S["projects"].Has(_objectContext)){CloseObjectDrawer();return;}
  CivicHeader("drawer",G.project_name(_objectContext),"地块 "+S["projects"][_objectContext]["tile"].Text+" · 建筑与来源",()=>CloseObjectDrawer());
  _civic.Render("drawer",_cityIdentity+"/building/"+_objectContext,(body,actions)=>CivicBuildingDetails(body,actions,_objectContext));
  if(_archiveOpen&&_advancedWorkspace)_civic.Q("civic-workspace").style.right=680;
 }
 void RefreshCivicNavigation(){
  if(!CivicEnabled)return;var nav=_civic.Q("civic-nav");nav.Clear();
  var routes=new[]{new[]{"city","city","城市〔锁定〕"},new[]{"population_home","population","居民与服务"},new[]{"economy_home","currency","资源与财政"},new[]{"construction","build","施工"},new[]{"tasks_home","task","任务与成长"},new[]{"bonus_home","bonus","Bonus"},new[]{"records","archive","档案"}};
  for(int i=0;i<routes.Length;i++){
   if(i==1||i==4||i==6)C.Label(nav,i==1?"街区经营":i==4?"发展与构筑":"城市记忆","c-nav-group");
   var route=routes[i];var b=C.Button(nav,"",()=>OpenSection(route[0]),_archiveOpen&&ArchiveCategory(_page)==route[0],route[0]!="city","nav-"+route[0]);
   C.Icon(b,route[1]);C.Label(b,route[2],"c-small");
   if(route[0]=="bonus_home")C.Label(b,S["block"].Get("bonus_wallet",V.Map()).Values.Sum(x=>x.Get("charges",0).Long).ToString(),"c-small c-warning");
  }
  _civic.ApplyText(nav);
 }
 void RefreshCivicHud(){
  if(!CivicEnabled)return;
  var top=_civic.Q("civic-top");top.Clear();C.Button(top,"MET",()=>OpenArchive("settings"),false,true,"civic-menu").AddToClassList("c-brand");
  var m=S["metrics"];bool known=S.Get("simulation_tick",0).Long>0;
  foreach(var key in new[]{"dt","population","food","material","goods","currency"}){
   var id=key;var cell=C.Button(top,"",()=>OpenResourcePage(id),false,true,"hud-"+id);cell.AddToClassList("c-resource");C.Icon(cell,id);var labels=C.Column(cell,"c-grow");
   C.Label(labels,id=="dt"?"DT":id=="population"?"人口":Resource(id),"c-micro c-muted");
   double value=id=="dt"?S["dt"].Double:id=="population"?m.Get("population",0).Double:id=="currency"?S["currency"].Double:G.inventory_total(id);
   C.Label(labels,F(value,id=="population"?0:1),"c-number");
   string detail=id=="dt"?"加速预算":!known?"待结算":id=="population"?F(m.Get("employed",0).Double)+" 人就业":Signed(id=="currency"?m.Get("cashflow",0).Double:m.Get("flows",V.Map()).Get(id,0).Double)+" /月";
   var net=id=="currency"?m.Get("cashflow",0).Double:m.Get("flows",V.Map()).Get(id,0).Double;
   C.Label(labels,detail,"c-micro "+(!known?"c-muted":id!="population"&&id!="dt"&&net<0?"c-warning":"c-good"));cell.tooltip=TopbarResourceSummary(id);
  }
  var capacity=C.Column(top,"c-top-capacity");var last=S["block"].Get("construction_last",V.Map());var reservation=S["block"].Get("city_cp_last",V.Map());double h=last.Get("hours",0).Double;
  double cp=Construction.capacity_breakdown(G)["effective"].Double;double used=h>0?last.Get("cp",0).Double/h:0;double reserved=reservation.Get("hours",0).Double>0?reservation["cp"].Double/reservation["hours"].Double:0;
  var cpMeter=known?C.Meter(capacity,"施工 CP /月",used+reserved,cp):CivicPendingCapacity(capacity,"施工 CP /月");cpMeter.RegisterCallback<ClickEvent>(_=>Navigate("construction"));cpMeter.tooltip=CapacityExplanation("cp");
  var power=known?C.Meter(capacity,"电力 · 需 /供",m.Get("energy_need",0).Double,m.Get("energy",0).Double,"","",m.Get("energy_need",0).Double>m.Get("energy",0).Double?"c-warning":""):CivicPendingCapacity(capacity,"电力 · 需 /供");power.RegisterCallback<ClickEvent>(_=>OpenResourcePage("energy"));power.tooltip=CapacityExplanation("energy");
  RefreshCivicNavigation();RefreshCivicClock();RefreshCivicLayers();RefreshCivicGuide();_civic.ApplyText(top);
 }
 VisualElement CivicPendingCapacity(VisualElement parent,string title){var row=C.Row(parent);row.style.paddingTop=5;row.style.paddingBottom=5;C.Label(row,title,"c-small");C.Label(row,"待结算","c-small c-muted c-align-right");return row;}
 void RefreshCivicClock(){
  var root=_civic.Q("civic-clock");root.Clear();var clock=Clock.summary(G);var text=C.Column(root);C.Label(text,clock.Get("date","").Text,"c-small");C.Label(text,Runtime.MandatoryPending?"待处理决定":Runtime.DecisionOpen?"规划中 · 已暂停":Runtime.WorldPaused?"已暂停":F(clock.Get("acceleration",1).Double,0)+"× 运行中","c-micro c-muted");
  C.Button(root,Runtime.WorldPaused?"继续":"Ⅱ",()=>{if(Runtime.MandatoryPending)OpenArchive("settlement");else Runtime.SetPaused(!Runtime.WorldPaused);},false,true,"civic-pause");
  int index=0;foreach(var mode in G.c.beta["time"]["modes"]){var id=mode["id"].Text;var i=index++;C.Button(root,i==0?"1×":i==1?"›":i==2?"»":"»»",()=>{Runtime.SetMode(id);if(Runtime.MandatoryPending)OpenArchive("settlement");},false,true,"speed-"+id);}
  _civic.ApplyText(root);
 }
 void RefreshCivicLayers(){
  if(!CivicEnabled)return;var root=_civic.Q("civic-layers");root.Clear();
  C.Label(root,LayerLabel(Layer)+" · "+(MapSourcePhase=="committed"?"最近实际":MapSourcePhase=="baseline"?"同一步现状":"同一步方案")+" · L / Shift+L · Shift 数值","c-micro c-layer-source");
  if(Layer!="terrain"){
   double max=Layer=="employment"?1:CityWorld.AnalysisMaximum(Layer);bool signed=Layer=="energy"||Layer=="delta";var reading=_inspectionWorld!=null?_inspectionWorld.Legend:V.Map();var ramp=C.Row(root);ramp.style.height=8;
   for(int i=0;i<21;i++){var swatch=C.Box(ramp);swatch.style.flexGrow=1;swatch.style.marginRight=0;swatch.style.height=8;swatch.style.backgroundColor=CityWorld.AnalysisTint(Layer,max*(signed?i/10.0-1:i/20.0),max,true,reading.Get("delta_metric","pollution").Text);}
   string legend=Layer=="employment"?"居民 0–100% · 空地为岗位可达度（非就业率）":new[]{"medical","education","entertainment","commercial"}.Contains(Layer)?"0% → 100% · 居民实际满足 / 空地可达":(signed?"−"+F(max,0)+" ← 0 → +":"0 → ")+F(max,0);
   C.Label(root,legend,"c-micro c-layer-source");
  }
  var row=C.Box(root,"c-layer-row");foreach(var pair in new[]{new[]{"terrain","地貌"},new[]{"medical","医疗"},new[]{"education","教育"},new[]{"entertainment","娱乐"},new[]{"commercial","消费"},new[]{"employment","就业"},new[]{"pollution","污染"},new[]{"heat","余热"},new[]{"stock","库存"},new[]{"energy","电力"}}){var p=pair;var b=C.Button(row,"",()=>SetLayer(p[0]),Layer==p[0],true,"layer-"+p[0]);C.Icon(b,p[0]);C.Label(b,p[1],"c-micro");}
  _civic.ApplyText(root);
 }
 void RefreshCivicGuide(){
  if(!CivicEnabled)return;var guidance=CityGuidanceView();var guideKey=V.Arr(GuidanceActionKey(guidance),_planningOverviewOpen,Themes.view(G),_feedbackView.Get("summaries",V.Arr()),TextScale).ToJson();if(guideKey==_civicNoticeKey)return;_civicNoticeKey=guideKey;var root=_civic.Q("civic-guide");root.Clear();
  C.Button(root,_planningOverviewOpen?"下一步  ▴":"下一步  ▾",()=>{_planningOverviewOpen=!_planningOverviewOpen;RefreshCivicGuide();},false,true,"civic-next-step").AddToClassList("c-quiet");
  var theme=Themes.view(G);var goal=theme.Get("goals",V.Arr()).FirstOrDefault(x=>!x.Get("met",false).Bool);
  C.Label(root,theme.Get("id","").Text!=""?theme.Get("name","").Text:guidance["title"].Text,"c-heading");
  if(goal!=null){double factor=goal.Get("unit","").Text=="%"?100:1;string value=F(goal["value"].Double*factor)+(goal.Get("comparison","").Text=="max"?" ≤ ":" / ")+F(goal["target"].Double*factor)+goal.Get("unit","").Text;C.Pair(root,goal.Get("label","").Text,value);if(goal.Get("comparison","").Text!="max")C.Progress(root,goal["value"].Double/Math.Max(.00001,goal["target"].Double));}
  if(_planningOverviewOpen){C.Label(root,guidance["reason"].Text,"c-small c-muted");C.Button(root,guidance["action_label"].Text,()=>{if(GuidanceActionKey(CityGuidanceView())!=GuidanceActionKey(guidance)){RefreshCivicGuide();ShowMessage("城市状态已变化，请重新确认。");return;}RunCityGuidanceAction();},true);C.Button(root,"全部任务与成长",()=>OpenArchive("tasks_home"));}
  var notes=_civic.Q("civic-notices");notes.Clear();
  if(Runtime.MandatoryPending)C.Button(notes,"有决定待处理",()=>OpenArchive("settlement"),true);
  foreach(var item in _feedbackView.Get("summaries",V.Arr()).OrderByDescending(x=>x.Get("severity",0).Long).Take(2)){var note=item;C.Button(notes,note.Get("text","").Text,()=>{if(S["projects"].Has(note.Get("building_id","")))SelectBuilding(note["building_id"].Text);SetLayer(note.Get("layer","terrain").Text);if(_archiveOpen)InspectMapFromPage();});}
  _civic.ApplyText(root);_civic.ApplyText(notes);
 }
 void CivicToast(string text){if(!CivicEnabled)return;var root=_civic.Q("civic-toast");root.Clear();C.Label(root,text,"c-small");_civic.ApplyText(root);}
 void OpenCivicOperation(string id,string title,string subtitle,Action<VisualElement,VisualElement> render,Action close,bool small=false){
  _civicOperation=id;_civicOperationRender=render;_civicOperationClose=close;
  Runtime.SetDecisionOpen(true);_civic.Q("civic-operation").EnableInClassList("is-confirm",small);
  CivicHeader("operation",title,subtitle,close);_civic.Render("operation",_cityIdentity+"/"+id,render);SyncCivicVisibility();
 }
 void CivicConfirm(string title,string text,Action action){
  if(_confirm!=null)DismissConfirm();Runtime.Flush();_confirm=new GameObject("Civic confirmation ownership",typeof(RectTransform));_confirm.transform.SetParent(_root,false);
  OpenCivicOperation("confirm",title,"操作确认",(body,actions)=>{C.Label(body,text);C.Button(actions,"取消",DismissConfirm);C.Button(actions,"确认",()=>{DismissConfirm();action();},true);},DismissConfirm,true);
 }
 void CloseCivicOperation(){_civic.Remember("operation");_civicOperation="";_civicOperationRender=null;_civicOperationClose=null;Runtime.SetDecisionOpen(_archiveOpen||ObjectDrawerOpen||ConstructionWindowOpen||ToolkitUpgradeOpen);if(ConstructionWindowOpen)RenderCivicConstruction();SyncCivicVisibility();}
 void OpenCivicReward(string rid){var queue=Bonus.pending(G);if(queue.Count==0||queue[0]["reward_id"].Text!=rid){ShowMessage("奖励队列已变化，请重新查看。");return;}if(_confirm!=null)DismissConfirm();_confirm=new GameObject("Civic reward ownership",typeof(RectTransform));_confirm.transform.SetParent(_root,false);_civicReward=rid;RenderCivicRewardOperation();}
 void RenderCivicRewardOperation(){var queue=Bonus.pending(G);if(queue.Count==0||queue[0]["reward_id"].Text!=_civicReward){if(_confirm!=null)DismissConfirm();return;}var reward=queue[0];OpenCivicOperation("reward","选择奖励",BlueprintRewardSource(reward)+" · 已保存候选",(body,actions)=>RenderCivicReward(body,actions,reward),DismissConfirm);}
 void CivicRewardReceipt(string bid){
  _blueprintBusy=false;string name=bid=="__currency__"?"通货补偿":G.c.bonuses[bid].Get("name",bid).Text;
  if(_confirm!=null)DismissConfirm();_confirm=new GameObject("Civic receipt ownership",typeof(RectTransform));_confirm.transform.SetParent(_root,false);
  OpenCivicOperation("receipt","已领取 · "+name,"领取结果已保存",(body,actions)=>{C.Icon(body,"reward");C.Label(body,bid=="__currency__"?"补偿已入账。":"方案已进入库存，选择建筑与槽位后即可比较安装。","c-good");C.Button(actions,"留在任务",()=>{DismissConfirm();Navigate("tasks_home");});C.Button(actions,"前往 Bonus 使用",()=>{DismissConfirm();_v3BonusFilter="all";Navigate("bonus_home");},true);},DismissConfirm,true);
 }
}
}
