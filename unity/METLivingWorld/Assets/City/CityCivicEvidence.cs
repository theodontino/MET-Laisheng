using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace MET.City {
// Explicit, isolated Player engineering acceptance. These events are synthetic;
// screenshots establish actual rendering, but do not establish human mouse acceptance.
public sealed class CityCivicEvidence : MonoBehaviour {
 public static bool Requested()=>Environment.GetCommandLineArgs().Contains("--civic-ui-checks");
 readonly V checks=V.Arr(),captures=V.Arr(),layouts=V.Arr(),notes=V.Arr();
 readonly List<string> errors=new List<string>();
 CityRuntime runtime;CityInterface ui;string output="",origin="",building="",tile="";bool finished,observeMouse;
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 Game G=>runtime.Game;VisualElement Root=>ui.CivicView.Root;
 void Awake(){Application.logMessageReceived+=Logged;}
 void OnDestroy(){Application.logMessageReceived-=Logged;}
 void Logged(string message,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(message+"\n"+stack);}
 bool Check(bool pass,string name){checks.Add(V.Map("name",name,"passed",pass));if(!pass)errors.Add(name);return pass;}
 void Must(bool pass,string name){if(!Check(pass,name))throw new InvalidOperationException(name);}
 void Command(string error,string name)=>Must(error=="",name+(error==""?"":": "+error));
 object Call(string name,params object[] args){var method=typeof(CityInterface).GetMethod(name,Private);Must(method!=null,"existing interface method "+name);return method.Invoke(ui,args);}
 string Fingerprint(){var state=runtime.Session.snapshot();state.Remove("last_seen_utc");foreach(var block in state["blocks"].Values){block["state"].Remove("wall_mark");block["state"]["beta_time"].Remove("last_seen_utc");}return state.ToJson(false,true);}
 bool Shown(VisualElement e){if(e==null||e.panel==null)return false;for(var p=e;p!=null;p=p.parent)if(p.resolvedStyle.display==DisplayStyle.None||p.resolvedStyle.visibility==Visibility.Hidden)return false;return e.worldBound.width>0&&e.worldBound.height>0;}
 void Press(Button b,string label){Must(b!=null&&b.enabledInHierarchy&&Shown(b),"enabled visible synthetic Toolkit button: "+label);b.Focus();using(var submit=NavigationSubmitEvent.GetPooled()){submit.target=b;b.SendEvent(submit);}}
 void Press(string name)=>Press(Root.Q<Button>(name),name);
 void PressText(string label,VisualElement parent=null)=>Press((parent??Root).Query<Button>().ToList().FirstOrDefault(b=>b.text==label&&Shown(b)),label);
 IEnumerator Frames(){yield return null;yield return null;yield return null;}
 IEnumerator Capture(string name){yield return new WaitForEndOfFrame();TextLayout(name);var texture=ScreenCapture.CaptureScreenshotAsTexture();Must(texture!=null,"screen texture: "+name);Check(texture.width==2560&&texture.height==1440&&Screen.fullScreen,"actual fullscreen 2560x1440: "+name);File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);captures.Add(name+".png");}
 void TextLayout(string sample){
  var compressed=new List<string>();var overlapped=new List<string>();
  // One panel pixel tolerates fractional-width rounding at 120% scaling; it
  // cannot conceal a genuinely missing text line or a zero-height flex item.
  Root.Query<Label>().ForEach(label=>{if(!Shown(label)||!label.ClassListContains("c-text")||string.IsNullOrEmpty(label.text))return;var measured=label.MeasureTextSize(label.text,Math.Max(1,label.contentRect.width+1),VisualElement.MeasureMode.AtMost,0,VisualElement.MeasureMode.Undefined);if(label.contentRect.height+2<measured.y)compressed.Add(label.text+" ["+label.contentRect.height+" < "+measured.y+"]");});
  Root.Query<VisualElement>(className:"c-meter").ForEach(meter=>{if(!Shown(meter))return;var head=meter.Q<VisualElement>(className:"c-meter-head");var track=meter.Q<VisualElement>(className:"c-track");if(head!=null&&track!=null&&head.worldBound.yMax>track.worldBound.yMin+1)overlapped.Add(head.Q<Label>()?.text??"meter");});
  Check(compressed.Count==0,sample+": text keeps natural line height"+(compressed.Count==0?"":": "+string.Join("; ",compressed.Take(8))));Check(overlapped.Count==0,sample+": meter labels clear progress tracks"+(overlapped.Count==0?"":": "+string.Join("; ",overlapped.Take(8))));
 }
 V Bounds(Rect r)=>V.Map("x",r.x,"y",r.y,"width",r.width,"height",r.height);
 bool Within(Rect a,Rect b)=>a.xMin>=b.xMin-1&&a.yMin>=b.yMin-1&&a.xMax<=b.xMax+1&&a.yMax<=b.yMax+1;
 bool Separate(Rect a,Rect b)=>a.xMax<=b.xMin+1||b.xMax<=a.xMin+1||a.yMax<=b.yMin+1||b.yMax<=a.yMin+1;
 void Region(string name,string sample){
  var panel=ui.CivicView.Q("civic-"+name);if(!Shown(panel))return;var head=ui.CivicView.Q("civic-"+name+"-head");var scroll=Root.Q<ScrollView>("civic-"+name+"-scroll");var footer=ui.CivicView.Q("civic-"+name+"-actions");
  var management=name=="workspace"&&ui.CivicView.ManagementMode;var content=management?ui.CivicView.Q("civic-management-body"):scroll?.contentViewport;
  Check(Within(panel.worldBound,Root.worldBound),sample+": "+name+" within screen");
  if(head==null||content==null)return;var view=content.worldBound;
  Check(Shown(content)&&Within(head.worldBound,panel.worldBound)&&Within(view,panel.worldBound),sample+": "+name+" header/content within panel");Check(view.height>24&&head.worldBound.yMax<=view.yMin+1,sample+": "+name+" fixed header outside content");
  if(Shown(footer))Check(Within(footer.worldBound,panel.worldBound)&&view.yMax<=footer.worldBound.yMin+1,sample+": "+name+" fixed footer outside content");
  if(management){
   Check(!Shown(scroll),sample+": management uses dedicated content rather than enlarged compact scroll");
   var panes=content.Query<ScrollView>(className:"c-management-scroll").ToList().Where(Shown).ToArray();Check(panes.Length>0,sample+": management has independent reading panes");
   foreach(var inner in panes){var viewport=inner.contentViewport.worldBound;Check(viewport.width>40&&viewport.height>48&&Within(viewport,view),sample+": inner management viewport retains usable size: "+inner.name);layouts.Add(V.Map("sample",sample,"region","management-inner-scroll","name",inner.name,"viewport",Bounds(viewport),"offset",V.Arr(inner.scrollOffset.x,inner.scrollOffset.y)));}
   var siblings=content.Query<VisualElement>(className:"c-management-layout").ToList().Where(Shown);foreach(var row in siblings){var columns=row.Children().Where(Shown).ToArray();for(int i=0;i<columns.Length;i++)for(int j=i+1;j<columns.Length;j++)Check(Separate(columns[i].worldBound,columns[j].worldBound),sample+": adjacent management panes do not overlap");}
  }
  layouts.Add(V.Map("sample",sample,"region",name,"management",management,"panel",Bounds(panel.worldBound),"header",Bounds(head.worldBound),"content",Bounds(view),"footer",footer==null?V.Map():Bounds(footer.worldBound)));
 }
 void HudLayout(string sample){
  foreach(var id in new[]{"top","nav","tile","layers","clock"}){var e=ui.CivicView.Q("civic-"+id);Check(Shown(e)&&Within(e.worldBound,Root.worldBound),sample+": "+id+" visible within screen");}
  foreach(var pair in new[]{new[]{"tile","layers"},new[]{"tile","clock"},new[]{"layers","clock"},new[]{"nav","tile"},new[]{"top","nav"}}){var a=ui.CivicView.Q("civic-"+pair[0]);var b=ui.CivicView.Q("civic-"+pair[1]);Check(Separate(a.worldBound,b.worldBound),sample+": "+pair[0]+" separate from "+pair[1]);}
  Region("tile",sample);layouts.Add(V.Map("sample",sample,"ui_scale",runtime.DisplaySettings["ui_scale"],"font_scale",runtime.DisplaySettings["text_scale"],"root",Bounds(Root.worldBound),"tile",Bounds(ui.CivicView.Q("civic-tile").worldBound),"layers",Bounds(ui.CivicView.Q("civic-layers").worldBound),"clock",Bounds(ui.CivicView.Q("civic-clock").worldBound)));
 }
 void LegacyHidden(string sample){
  var legacy=typeof(CityInterface).GetField("_root",Private)?.GetValue(ui) as Transform;Must(legacy!=null,"legacy root found");
  foreach(Transform child in legacy){if(child.name=="Map numeric reading")continue;var group=child.GetComponent<CanvasGroup>();Check(group!=null&&group.alpha==0&&!group.blocksRaycasts&&!group.interactable,sample+": legacy hidden and noninteractive: "+child.name);}
  if(EventSystem.current==null){Check(false,sample+": EventSystem available for raycast check");return;}
  foreach(var point in new[]{new Vector2(80,Screen.height-80),new Vector2(Screen.width/2,Screen.height/2),new Vector2(100,150)}){var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);Check(!hits.Any(hit=>hit.gameObject!=null&&hit.gameObject.transform.IsChildOf(legacy)&&hit.gameObject.name!="Map numeric reading"),sample+": legacy receives no UI raycast at "+point);}
 }
 IEnumerator Page(string page,string capture){var before=Fingerprint();ui.OpenArchive(page);yield return Frames();Check(Fingerprint()==before,"browse "+page+" preserves city, RNG and balances");Region("workspace",page);LegacyHidden(page);yield return Capture(capture);}
 void RejectUnsafeStorage(string message){
  // Runtime Awake precedes bootstrap; suppress its quit-save even when the
  // operator forgot the required environment before this component was mounted.
  typeof(CityRuntime).GetField("_saveEnabled",Private)?.SetValue(runtime,false);
  runtime.enabled=false;Debug.LogError(message);
  if(output!=""){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"player-evidence.json"),V.Map("ok",false,"scope","Rejected before fixture mutation; quit-save suppressed.","checks",checks,"captures",captures,"errors",V.Arr(errors.Cast<object>().ToArray())).ToJson(true));}
  Application.Quit(1);
 }
 IEnumerator Start(){
  runtime=GetComponent<CityRuntime>();ui=GetComponent<CityInterface>();var rawOutput=Environment.GetEnvironmentVariable("MET_CIVIC_OUTPUT");var rawSave=Environment.GetEnvironmentVariable("MET_UNITY_SAVE_ROOT");
  // No fixture mutation, directory creation or fallback save path before the two
  // explicit paths have been checked. Existing normal-player storage is never used.
  if(string.IsNullOrWhiteSpace(rawOutput)||string.IsNullOrWhiteSpace(rawSave)){RejectUnsafeStorage("Civic evidence requires MET_CIVIC_OUTPUT and MET_UNITY_SAVE_ROOT nested inside it.");yield break;}
  output=Path.GetFullPath(rawOutput);var save=Path.GetFullPath(rawSave);var prefix=output.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  if(!save.StartsWith(prefix,StringComparison.Ordinal)||Path.GetFullPath(runtime.StorageRoot)!=save){RejectUnsafeStorage("Civic evidence rejected non-isolated save directory.");yield break;}
  Directory.CreateDirectory(output);var routines=new Stack<IEnumerator>();routines.Push(Run(save));
  while(routines.Count>0){object next=null;try{var routine=routines.Peek();if(!routine.MoveNext()){routines.Pop();continue;}next=routine.Current;}catch(Exception error){errors.Add(error.ToString());Finish();yield break;}if(next is IEnumerator nested){routines.Push(nested);continue;}yield return next;}
  Finish();
 }
 IEnumerator Run(string save){
  yield return Frames();Must(ui.CivicEnabled,"full Civic Toolkit shell ready");Must(GetComponent<CityWorld>().Failure=="","city renderer ready");Check(Screen.width==2560&&Screen.height==1440&&Screen.fullScreen,"2K fullscreen baseline");
  runtime.NewCity(20261029);Command(runtime.ExecuteWorld(city=>city.BeginDistrict(0,"balanced","Civic UI 隔离工程验收")),"begin real isolated district");origin=G.s["origin_choices"][0].Text;Command(runtime.Execute(g=>g.choose_origin(origin)),"choose actual offered origin");Command(runtime.ExecuteWorld(city=>city.EnableWild()),"enable actual Wild");runtime.SetPaused(true);
  Call("CloseArchive");yield return Frames();yield return Capture("00-before-first-settlement");
  Command(runtime.Execute(g=>{var inventory=g.s["projects"]["municipal"]["inventory"];var before=inventory.Duplicate(true);double prior=g.s["currency"].Double;inventory["food"]=80;inventory["material"]=80;g.s["currency"]=Math.Max(prior,1000);g.s["debug_used"]=true;g.log_event("civic_ui_engineering_fixture",V.Map("before",before,"after",inventory,"currency_before",prior,"currency_after",g.s["currency"],"reason","isolated synthetic UI fixture; not normal-play economy or pacing evidence"),"隔离 Civic 工程验收补给：食物80、材料80、通货至少1000；不作为正常游玩证据。");g.refresh_metrics();return "";}),"declare fixture supply and debug_used");
  Command(runtime.Execute(g=>Sim.tick(g,.02)),"actual simulation settlement through Runtime.Execute");Check(G.s["simulation_tick"].Long>0,"HUD has actual settled flows");
  building=G.playable_ids().First(id=>G.s["projects"][id]["def"].Text=="homestead"&&G.s["projects"][id]["status"].Text=="running").Text;tile=G.s["projects"][building]["tile"].Text;
  Call("CloseArchive");ui.SelectTile(tile);yield return Frames();HudLayout("baseline");LegacyHidden("baseline");yield return Capture("01-city-hud-tile");
  var browse=Fingerprint();Press("nav-population_home");yield return Frames();Check(Fingerprint()==browse,"synthetic residents navigation is read only");Region("workspace","residents");yield return Capture("02-residents-services");
  yield return Page("economy_home","03-economy");var economy=Fingerprint();PressText("市场",ui.CivicView.Q("civic-workspace-tabs"));yield return Frames();Check(Fingerprint()==economy,"market tab preserves city/RNG/balances");yield return Capture("04-market");PressText("政策",ui.CivicView.Q("civic-workspace-tabs"));yield return Frames();Check(Fingerprint()==economy,"policies tab preserves city/RNG/balances");yield return Capture("05-policies");
  yield return Page("construction","06-construction-queue");
  ui.OpenArchive("tasks_home");yield return Frames();var themeButton=Root.Query<Button>().ToList().FirstOrDefault(b=>b.text=="选择此主题"&&Shown(b));if(themeButton!=null){Press(themeButton,"choose offered theme");yield return Frames();notes.Add(V.Map("action","real offered theme selection via synthetic Toolkit button","theme",Themes.view(G).Get("id","")));}
  var taskState=Fingerprint();yield return Capture("07-tasks-current");PressText("成长路线",ui.CivicView.Q("civic-workspace-tabs"));yield return Frames();Check(Fingerprint()==taskState,"growth task tab is read only");yield return Capture("08-tasks-growth");PressText("已完成",ui.CivicView.Q("civic-workspace-tabs"));yield return Frames();Check(Fingerprint()==taskState,"completed task tab is read only");yield return Capture("09-tasks-completed");
  yield return Page("bonus_home","10-bonus");
  var field=Root.Q<TextField>("bonus-search");Must(field!=null,"Bonus search TextField exists");field.Focus();yield return Frames();Check(ui.CivicView.TextFocused&&ui.KeyboardOwned,"focused Toolkit TextField owns keyboard");Check(ui.CivicView.ReleaseText(),"TextField focus explicitly releasable");yield return Frames();Check(!ui.CivicView.TextFocused,"released TextField no longer owns focus");
  yield return Page("records","11-archive-records");yield return Page("settings","12-settings");
  // Preserve actual reading position when leaving and returning to one section.
  ui.OpenArchive("sources");yield return Frames();var scroll=VisibleReadingScroll("management-development-source-list");Must(scroll!=null,"sources exposes visible inner management list");var max=scroll.verticalScroller.highValue;Check(max>20,"source page offers real scrolling content");scroll.scrollOffset=new Vector2(0,Math.Min(150,max));yield return Frames();var retained=scroll.scrollOffset.y;PressText("资源与财政",ui.CivicView.Q("civic-workspace-head"));yield return Frames();PressText("城市档案",ui.CivicView.Q("civic-workspace-head"));yield return Frames();var restored=VisibleReadingScroll("management-development-source-list");Check(restored!=null&&Math.Abs(restored.scrollOffset.y-retained)<2,"same archive section rebuild restores actual inner management scroll position");
  var inspectState=Fingerprint();Call("InspectMapFromPage");yield return Frames();Check(ui.MapViewing,"archive can inspect actual map");Check(Fingerprint()==inspectState,"map inspection leaves state/RNG/balances unchanged");Call("CloseArchive");ui.SelectTile(tile);Call("Navigate","building",building);yield return Frames();Region("drawer","building");yield return Capture("13-building-drawer");Press("civic-drawer-close");yield return Frames();
  var upgradeState=Fingerprint();ui.OpenUpgradeWindow(building);yield return Frames();Check(ui.WildStarOpen&&ui.KeyboardOwned&&!ui.MapInteractionAvailable,"public upgrade entry opens owned Wild star");Check(Fingerprint()==upgradeState,"upgrade preview preserves state/RNG/balances");yield return Capture("14-building-star");ui.CloseToolkitUpgrade();yield return Frames();
  var workshop=G.playable_ids().FirstOrDefault(id=>G.s["projects"][id]["def"].Text=="workshop");if(workshop!=null){var savedUpgrade=Fingerprint();ui.OpenUpgradeWindow(workshop.Text);yield return Frames();Check(ui.ToolkitUpgradeOpen&&Fingerprint()==savedUpgrade,"existing workshop upgrade preview remains isolated");yield return Capture("18-workshop-upgrade");ui.CloseToolkitUpgrade();yield return Frames();}
  // Creating a first tender is an authorized command. Only cancellation and
  // reopening the saved tender must leave every gameplay field unchanged.
  var free=G.s["cells"].Keys.FirstOrDefault(id=>G.s["cells"][id].Get("building","").Text==""&&!G.s["cells"][id]["traits"].Contains("tile.water"));Must(free!=null,"empty land for actual construction");ui.SelectTile(free);ui.OpenConstructionWindow("homestead",free);yield return Frames();Must(ui.ConstructionWindowOpen,"real construction draft opened");Region("operation","construction");yield return Capture("15-construction-preview");var draft=Fingerprint();var draftOffers=G.s["draft"].ToJson(false,true);var rng=G.s["rng"].ToJson(false,true);double balance=G.s["currency"].Double;Press("construction-cancel");yield return Frames();Check(Fingerprint()==draft,"cancel preserves gameplay/RNG/balances and saved offers");ui.OpenConstructionWindow("homestead",free);yield return Frames();Check(Fingerprint()==draft&&G.s["draft"].ToJson(false,true)==draftOffers&&G.s["rng"].ToJson(false,true)==rng&&G.s["currency"].Double==balance,"reopen same construction draft draws no RNG and charges nothing");
  foreach(var scale in new[]{.8f,1f,1.2f})foreach(var font in new[]{1f,1.25f,1.5f}){
   ui.CloseConstructionWindow();var before=Fingerprint();runtime.DisplaySettings["ui_scale"]=scale;runtime.DisplaySettings["text_scale"]=font;ui.ApplyDisplay();Call("CloseArchive");ui.SelectTile(tile);yield return Frames();var label="ui"+(int)(scale*100)+"-font"+(int)(font*100);HudLayout(label);ui.OpenArchive("economy_home");yield return Frames();Region("workspace",label);Call("CloseArchive");ui.OpenConstructionWindow("homestead",free);yield return Frames();
   Must(ui.ConstructionWindowOpen,"saved draft reopens for layout: "+label);Region("operation",label);ui.CloseConstructionWindow();yield return Frames();
   Check(Fingerprint()==before,"display changes preserve simulation: "+label);
   if((scale==.8f&&font==1f)||(scale==1f&&font==1.25f)||(scale==1.2f&&font==1.5f)){Call("CloseArchive");ui.SelectTile(tile);yield return Frames();yield return Capture("17-layout-"+label);}
  }
  runtime.DisplaySettings["ui_scale"]=1;runtime.DisplaySettings["text_scale"]=1;ui.ApplyDisplay();Call("CloseArchive");ui.SelectTile(tile);yield return Frames();
  ui.OpenConstructionWindow("homestead",free);yield return Frames();Check(Fingerprint()==draft,"all scale/layout inspections preserve saved draft and city");
  var jobs=Construction.queue(G).Count;Press("construction-confirm");yield return Frames();Check(!ui.ConstructionWindowOpen&&Construction.queue(G).Count==jobs+1,"confirmed construction enters real CP queue");Check(G.s["cells"][free].Get("building","").Text!="","real new building occupies selected tile");ui.OpenArchive("construction");yield return Frames();yield return Capture("16-construction-confirmed-queue");
  if(Bonus.pending(G).Count==0){Command(runtime.Execute(g=>{Bonus.draw(g,"civic_ui_engineering_fixture",V.Arr(),V.Map("kind","copy"));return "";}),"declared fixture trigger uses actual persisted reward draw");notes.Add(V.Map("check","reward_fixture","status","synthetic trigger","reason","candidates generated and saved by real Bonus.draw; this is UI coverage, not an earned normal-play reward"));}
  var pending=Bonus.pending(G);if(pending.Count>0){var saved=pending[0].ToJson(false,true);var before=Fingerprint();ui.OpenArchive("blueprints");yield return Frames();var id=pending[0]["choices"][0]["bonus_id"].Text;Press("reward-candidate-"+id);yield return Frames();Check(Fingerprint()==before&&Bonus.pending(G)[0].ToJson(false,true)==saved,"pending reward preselection keeps persisted candidates/state/RNG/balances");yield return Capture("20-saved-reward-candidates");Press("reward-confirm");yield return Frames();Check(Bonus.pending(G).Count==pending.Count-1,"real reward confirmation consumes exactly one pending record");yield return Capture("21-reward-receipt");Call("DismissConfirm");ui.OpenArchive("bonus_home");yield return Frames();yield return Capture("22-bonus-inventory-and-target");Call("CloseArchive");}
  yield return ManagementEvidence();yield return TooltipEvidence();
  Command(runtime.Session.Validate(),"final real city validates");Check(captures.Count>=34&&captures.Count<=48,"actual screenshot artifacts cover compact pages, independent management and decisions");runtime.Save();Call("CloseArchive");ui.SelectTile(tile);yield return Frames();
 }
 VisualElement ManagementBody=>ui.CivicView.Q("civic-management-body");
 ScrollView VisibleReadingScroll(string preferred=""){
  var scope=ui.CivicView.ManagementMode?ManagementBody:ui.CivicView.Q("civic-workspace");
  if(preferred!=""){var named=scope.Q<ScrollView>(preferred);if(Shown(named))return named;}
  return scope.Query<ScrollView>().ToList().Where(Shown).OrderByDescending(x=>x.verticalScroller.highValue).FirstOrDefault();
 }
 string HeaderTitle()=>ui.CivicView.Q("civic-workspace-head").Query<Label>().ToList().FirstOrDefault(x=>x.ClassListContains("c-hero"))?.text??"";
 string ManagementSignature(){
  var data=V.Map("title",HeaderTitle(),"selection",V.Arr(),"dropdowns",V.Arr(),"fields",V.Arr(),"tabs",V.Arr());
  foreach(var row in ManagementBody.Query<VisualElement>(className:"c-management-row").ToList().Where(x=>x.ClassListContains("is-selected")))data["selection"].Add(string.Join(" | ",row.Query<Label>().ToList().Select(x=>x.text)));
  foreach(var field in ManagementBody.Query<DropdownField>().ToList())data["dropdowns"].Add(V.Map("label",field.label,"value",field.value));
  foreach(var field in ManagementBody.Query<TextField>().ToList().Where(x=>!x.isReadOnly))data["fields"].Add(V.Map("label",field.label,"value",field.value));
  foreach(var bar in ManagementBody.Query<VisualElement>(className:"c-management-toolbar").ToList())foreach(var button in bar.Query<Button>().ToList().Where(x=>x.ClassListContains("is-selected")||x.ClassListContains("is-primary")))data["tabs"].Add(button.text);
  return data.ToJson(false,true);
 }
 void OpenManagement(string page)=>Call("OpenAdvancedWorkspace",page,null);
 void PickDropdown(string label,string choice){var field=ManagementBody.Query<DropdownField>().ToList().FirstOrDefault(x=>x.label==label&&Shown(x));Must(field!=null&&field.choices.Contains(choice),"management dropdown offers "+label+" / "+choice);field.value=choice;}
 IEnumerator ManagementCapture(string name){
  yield return Frames();Check(ui.CivicView.ManagementMode&&Shown(ManagementBody),name+": dedicated management mode active");var panel=ui.CivicView.Q("civic-workspace");Check(panel.worldBound.width>Root.worldBound.width*.85f&&panel.worldBound.height>Root.worldBound.height*.85f,name+": management fills screen independently");Check(ui.CivicView.Q("civic-workspace-head").Query<VisualElement>(className:"c-management-modules").ToList().Any(Shown),name+": management has its own module header");Region("workspace",name);yield return Capture(name);
 }
 Vector2 ScreenPoint(Vector2 panelPoint){
  // Invert the public screen-to-panel transform rather than assuming 1x UI scale.
  var p0=RuntimePanelUtils.ScreenToPanel(Root.panel,Vector2.zero);var px=RuntimePanelUtils.ScreenToPanel(Root.panel,new Vector2(Screen.width,0));var py=RuntimePanelUtils.ScreenToPanel(Root.panel,new Vector2(0,Screen.height));
  return new Vector2((panelPoint.x-p0.x)/(px.x-p0.x)*Screen.width,Screen.height-(panelPoint.y-p0.y)/(py.y-p0.y)*Screen.height);
 }
 string ElementPath(VisualElement element){var names=new List<string>();for(var e=element;e!=null&&names.Count<8;e=e.parent)names.Add(e.GetType().Name+"#"+e.name+"."+string.Join(".",e.GetClasses()));return string.Join(" <- ",names);}
 V Raycasts(Vector2 point){var results=V.Arr();if(EventSystem.current==null)return results;var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);foreach(var hit in hits)results.Add(V.Map("object",hit.gameObject==null?"":hit.gameObject.name,"module",hit.module==null?"":hit.module.GetType().Name));return results;}
 void PointerProbe(Button button,string sample){
  Must(button!=null&&Shown(button),sample+": visual button exists for pointer projection");var center=button.worldBound.center;var point=ScreenPoint(center);var panelPoint=RuntimePanelUtils.ScreenToPanel(Root.panel,new Vector2(point.x,Screen.height-point.y));var picked=Root.panel.Pick(panelPoint);
  Check(Vector2.Distance(panelPoint,center)<1,sample+": screen/panel inverse agrees at visual button");Check(ui.CivicView.OwnsPointer(point)&&ui.PointerOwned(point),sample+": actual visual button screen position is UI-owned");Check(picked==button||(picked!=null&&button.Contains(picked)),sample+": panel picking resolves visual button or child");notes.Add(V.Map("check","visual_button_pointer_probe","sample",sample,"synthetic",true,"scope","coordinate/pick evidence; no native mouse input generated","screen_point",V.Arr(point.x,point.y),"panel_point",V.Arr(panelPoint.x,panelPoint.y),"picked",ElementPath(picked),"event_system_hits",Raycasts(point)));
 }
 IEnumerator ManagementEvidence(){
  var before=Fingerprint();ui.OpenArchive("population_home");yield return Frames();Check(!ui.CivicView.ManagementMode&&Shown(Root.Q<ScrollView>("civic-workspace-scroll")),"population compact renderer remains separate");var compactTitle=HeaderTitle();var compactWidth=ui.CivicView.Q("civic-workspace").worldBound.width;Press("civic-expand");yield return Frames();Check(HeaderTitle()!=compactTitle&&ui.CivicView.Q("civic-workspace").worldBound.width>compactWidth*1.5f,"management opens with distinct title and its own screen layout");
  yield return ManagementCapture("30-management-population");PointerProbe(Root.Q<Button>("civic-expand"),"population management header");
  var targetHome=G.s["projects"].Keys.LastOrDefault(id=>G.project_stats(id).Get("housing",0).Double>0);if(targetHome!=null){Call("OpenCivicHomeManagement",targetHome,"all");yield return Frames();var homeState=Call("CivicOMState","population","homes");Check((string)homeState.GetType().GetField("selection").GetValue(homeState)==targetHome&&(string)homeState.GetType().GetField("view").GetValue(homeState)=="homes","direct home management entry retains explicitly requested dwelling");}
  PressText("岗位来源",ManagementBody);yield return Frames();Check(ManagementBody.Query<VisualElement>(className:"c-management-row").ToList().Any(),"population management changes comparison perspective");
  PressText("资源与财政",ui.CivicView.Q("civic-workspace-head"));yield return Frames();PressText("材料",ManagementBody);yield return Frames();PickDropdown("筛选","有库存");yield return Frames();PickDropdown("排序","来源名称 ↑");yield return Frames();
  var rows=ManagementBody.Query<VisualElement>(className:"c-management-row").ToList().Where(x=>x.focusable).ToArray();Must(rows.Length>0,"real material inventory has selectable management source");
  var reserve=rows.FirstOrDefault(x=>x.Query<Label>().ToList().Any(label=>label.text==G.project_name("municipal")));Must(reserve!=null,"public reserve has human-readable source label");using(var key=KeyDownEvent.GetPooled('\n',KeyCode.Return,EventModifiers.None)){key.target=reserve;reserve.SendEvent(key);}yield return Frames();Check(!ManagementBody.Query<Button>().ToList().Any(x=>x.text=="地图定位"),"off-map public reserve does not offer a false map location");
  var producer=G.s["projects"].Keys.First(id=>G.s["cells"].Has(G.s["projects"][id].Get("tile","").Text)&&G.s["projects"][id].Get("inventory",V.Map()).Get("material",0).Double>0);var identity=(string)Call("OperatingProjectIdentity",producer);var selected=ManagementBody.Query<VisualElement>(className:"c-management-row").ToList().First(x=>x.focusable&&x.Query<Label>().ToList().Any(label=>label.text==identity));using(var key=KeyDownEvent.GetPooled('\n',KeyCode.Return,EventModifiers.None)){key.target=selected;selected.SendEvent(key);}yield return Frames();
  // Expand the real producer's nested receipts, not an empty municipal ledger.
  var inspector=ManagementBody.Q<VisualElement>(className:"c-management-inspector");for(int depth=0;depth<3;depth++){foreach(var fold in inspector.Query<Foldout>().ToList())fold.value=true;yield return Frames();}var source=inspector.Q<ScrollView>();Must(source!=null,"selected management source has visible scroll");Check(source.verticalScroller.highValue>20,"real management inspector/list content supports retained scroll");source.scrollOffset=new Vector2(0,Math.Min(140,Math.Max(0,source.verticalScroller.highValue)));yield return Frames();var offset=source.scrollOffset.y;var signature=ManagementSignature();var expandedSources=inspector.Query<Foldout>().ToList().Count(f=>f.value);
  yield return ManagementCapture("31-management-resources-filtered-source");var locate=ManagementBody.Query<Button>().ToList().FirstOrDefault(x=>x.text=="地图定位");Must(locate!=null,"selected actual source offers map locate");Press(locate,"locate selected management source");yield return Frames();Check(ui.MapViewing,"management Locate enters actual map");yield return Capture("32-management-source-located-map");var returnButton=ui.CivicView.Q("civic-map-return").Q<Button>();PointerProbe(returnButton,"return to exact management list");Press(returnButton,"return to exact management list");yield return Frames();
  Check(ui.CivicView.ManagementMode&&ManagementSignature()==signature,"Locate/map/return preserves management view, filter, sort and selection");var restoredScroll=ManagementBody.Q<VisualElement>(className:"c-management-inspector").Q<ScrollView>();Check(restoredScroll!=null&&Math.Abs(restoredScroll.scrollOffset.y-offset)<2,"Locate/map/return preserves actual inner reading offset");
  PressText("住户与服务",ui.CivicView.Q("civic-workspace-head"));yield return Frames();PressText("资源与财政",ui.CivicView.Q("civic-workspace-head"));yield return Frames();Check(ManagementSignature()==signature,"switching management modules preserves resource view/filter/selection");restoredScroll=ManagementBody.Q<VisualElement>(className:"c-management-inspector").Q<ScrollView>();Check(ManagementBody.Q<VisualElement>(className:"c-management-inspector").Query<Foldout>().ToList().Count(f=>f.value)==expandedSources,"nested source records restore their expansion before scrolling");Check(restoredScroll!=null&&Math.Abs(restoredScroll.scrollOffset.y-offset)<2,"switching management modules restores rebuilt inner reading offset");Check(((string)Call("NavigationTrail")).Contains("材料"),"management breadcrumb follows selected material view");
  PressText("市场",ui.CivicView.Q("civic-workspace-tabs"));yield return ManagementCapture("33-management-market");PressText("政策",ui.CivicView.Q("civic-workspace-tabs"));yield return ManagementCapture("34-management-policy-comparison");
  PressText("施工调度",ui.CivicView.Q("civic-workspace-head"));yield return ManagementCapture("35-management-real-construction-queue");Check(ManagementBody.Query<VisualElement>(className:"c-management-row").ToList().Any(),"management construction table contains real queued building");
  PressText("任务与成长",ui.CivicView.Q("civic-workspace-head"));yield return Frames();PressText("当前事项",ManagementBody);yield return ManagementCapture("36-management-tasks-current");PressText("三阶段路线",ManagementBody);yield return ManagementCapture("37-management-tasks-three-stage-route");PressText("已保存成果",ManagementBody);yield return ManagementCapture("38-management-tasks-saved-results");
  var taskSignature=ManagementSignature();PressText("资源与财政",ui.CivicView.Q("civic-workspace-head"));yield return Frames();PressText("任务与成长",ui.CivicView.Q("civic-workspace-head"));yield return Frames();Check(ManagementSignature()==taskSignature,"management module revisit restores saved-results task tab");
  PressText("Bonus 配装",ui.CivicView.Q("civic-workspace-head"));yield return Frames();PressText("库存副本",ManagementBody);yield return Frames();var targets=ManagementBody.Q<ScrollView>("management-development-bonus-targets");var comparison=ManagementBody.Q<ScrollView>("management-development-bonus-comparison");Check(Shown(targets)&&Shown(comparison),"Bonus management has adjacent actual-target and comparison panes");var owned=G.s["block"].Get("bonus_wallet",V.Map()).Pairs.Any(x=>x.Value.Get("charges",0).Long>0);Check(owned,"Bonus management reads actually claimed fixture inventory");yield return ManagementCapture("39-management-bonus-inventory-target-comparison");
  PressText("城市档案",ui.CivicView.Q("civic-workspace-head"));yield return ManagementCapture("40-management-chronological-records");
  runtime.DisplaySettings["ui_scale"]=1.2f;runtime.DisplaySettings["text_scale"]=1.5f;ui.ApplyDisplay();
  foreach(var sample in new[]{new[]{"population","41-management-120-150-population"},new[]{"resources","42-management-120-150-resources"},new[]{"tasks_home","43-management-120-150-growth"},new[]{"bonuses","44-management-120-150-bonus"}}){OpenManagement(sample[0]);yield return Frames();if(sample[0]=="tasks_home"){PressText("三阶段路线",ManagementBody);yield return Frames();}yield return ManagementCapture(sample[1]);}
  runtime.DisplaySettings["ui_scale"]=1;runtime.DisplaySettings["text_scale"]=1;ui.ApplyDisplay();Call("CloseArchive");ui.SelectTile(tile);yield return Frames();Check(Fingerprint()==before,"all independent management browsing preserves gameplay, RNG and balances");
 }
 float Linear(float value)=>value<=.04045f?value/12.92f:Mathf.Pow((value+.055f)/1.055f,2.4f);
 double Luminance(Color value)=>.2126*Linear(value.r)+.7152*Linear(value.g)+.0722*Linear(value.b);
 Color Over(Color front,Color back)=>new Color(front.r*front.a+back.r*(1-front.a),front.g*front.a+back.g*(1-front.a),front.b*front.a+back.b*(1-front.a),1);
 IEnumerator TooltipEvidence(){
  Call("CloseArchive");yield return Frames();var button=Root.Q<Button>("hud-food");PointerProbe(button,"actual HUD tooltip anchor");Must(!string.IsNullOrWhiteSpace(button.tooltip),"HUD tooltip contains actual source explanation");using(var over=PointerOverEvent.GetPooled()){over.target=button;button.SendEvent(over);}yield return new WaitForSecondsRealtime(.65f);yield return Frames();
  var tip=Root.Q<VisualElement>(className:"c-tooltip");var label=tip?.Q<Label>();Must(Shown(tip)&&Shown(label)&&!string.IsNullOrEmpty(label.text),"synthetic PointerOver displays actual HUD tooltip");var back=tip.resolvedStyle.backgroundColor;var foreground=label.resolvedStyle.color;var renderedBack=Over(back,Root.resolvedStyle.backgroundColor);var renderedText=Over(foreground,renderedBack);var a=Luminance(renderedText);var b=Luminance(renderedBack);var ratio=(Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
  Check(foreground.a>.9f&&back.a>.9f&&ratio>=4.5,"resolved tooltip foreground/background meet 4.5:1 text contrast; measured="+ratio.ToString("F2"));Check(Within(tip.worldBound,Root.worldBound),"actual tooltip stays within screen");notes.Add(V.Map("check","tooltip_readability","input","synthetic PointerOver; no native hover generated","contrast_ratio",ratio,"foreground",V.Arr(foreground.r,foreground.g,foreground.b,foreground.a),"background",V.Arr(back.r,back.g,back.b,back.a),"bounds",Bounds(tip.worldBound)));
  yield return Capture("45-hud-tooltip-readable");using(var leave=PointerLeaveEvent.GetPooled()){leave.target=Root;Root.SendEvent(leave);}yield return Frames();
 }
 void EnableMouseObservation(){
  observeMouse=true;Root.RegisterCallback<PointerDownEvent>(e=>ObservedToolkit("Toolkit.PointerDown",e.target as VisualElement,e.position,e.button),TrickleDown.TrickleDown);Root.RegisterCallback<ClickEvent>(e=>ObservedToolkit("Toolkit.Click",e.target as VisualElement,e.position,e.button),TrickleDown.TrickleDown);
  File.AppendAllText(Path.Combine(output,"native-pointer-observation.jsonl"),V.Map("event","observer_started","scope","post-finish hold observation only; no generated mouse input and no typed text recorded","screen",V.Arr(Screen.width,Screen.height),"time",Time.realtimeSinceStartupAsDouble).ToJson()+"\n");
 }
 void ObservedToolkit(string kind,VisualElement target,Vector3 point,int button){if(!observeMouse||!finished)return;File.AppendAllText(Path.Combine(output,"native-pointer-observation.jsonl"),V.Map("event",kind,"scope","post-finish observed event; no automation generated","time",Time.realtimeSinceStartupAsDouble,"button",button,"panel_position",V.Arr(point.x,point.y),"target",ElementPath(target)).ToJson()+"\n");}
 void Update(){
  if(!finished||!observeMouse||Root?.panel==null)return;
  for(int button=0;button<3;button++)if(Input.GetMouseButtonDown(button)){var point=(Vector2)Input.mousePosition;var panelPoint=RuntimePanelUtils.ScreenToPanel(Root.panel,new Vector2(point.x,Screen.height-point.y));var picked=Root.panel.Pick(panelPoint);File.AppendAllText(Path.Combine(output,"native-pointer-observation.jsonl"),V.Map("event","Unity.Input.MouseButtonDown","scope","actual post-finish legacy Input observation only; no generated input","time",Time.realtimeSinceStartupAsDouble,"button",button,"focused",Application.isFocused,"mouse",V.Arr(point.x,point.y),"screen",V.Arr(Screen.width,Screen.height),"display_system",V.Arr(Display.main.systemWidth,Display.main.systemHeight),"display_rendering",V.Arr(Display.main.renderingWidth,Display.main.renderingHeight),"panel_position",V.Arr(panelPoint.x,panelPoint.y),"ui_pointer_owned",ui.PointerOwned(point),"civic_pointer_owned",ui.CivicView.OwnsPointer(point),"panel_pick",ElementPath(picked),"event_system_hits",Raycasts(point)).ToJson()+"\n");}
 }
 void Finish(){if(finished)return;finished=true;var hold=Environment.GetCommandLineArgs().Contains("--civic-ui-hold");File.WriteAllText(Path.Combine(output,"player-evidence.json"),V.Map("ok",errors.Count==0,"scope","Actual 2560x1440 fullscreen Unity Player rendering; synthetic UI Toolkit NavigationSubmit, explicit paused isolated city and declared debug supplies. Not human/OS mouse input or ordinary-play economy/pacing acceptance.","synthetic_input",true,"save_root",runtime.StorageRoot,"output",output,"seed",20261029,"origin",origin,"building",building,"tile",tile,"resolution",Screen.width+"x"+Screen.height,"fullscreen",Screen.fullScreen,"graphics",SystemInfo.graphicsDeviceName,"hold_after_checks",hold,"fixture_supplies",V.Map("food",80,"material",80,"currency_minimum",1000,"debug_used",true,"sim_tick_months",.02),"checks",checks,"captures",captures,"layouts",layouts,"notes",notes,"errors",V.Arr(errors.Cast<object>().ToArray())).ToJson(true));Debug.Log("CIVIC UI PLAYER "+(errors.Count==0?"PASS":"FAIL")+" "+checks.Count+" checks; "+captures.Count+" captures");if(hold)EnableMouseObservation();else Application.Quit(errors.Count==0?0:1);}
}
}
