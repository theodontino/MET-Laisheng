using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MET.City {
// Explicit Player engineering fixture. NavigationSubmit is synthetic UI input;
// the simulation steps below are accelerated fixture work, not normal play pacing.
public sealed class CityWildG1Evidence : MonoBehaviour {
 public static bool Requested()=>Environment.GetCommandLineArgs().Contains("--wild-g1-checks");
 readonly V _checks=V.Arr(),_captures=V.Arr(),_layouts=V.Arr(),_advances=V.Arr();
 readonly List<string> _errors=new List<string>();
 CityRuntime _runtime;CityInterface _ui;string _output,_building,_tile,_origin;bool _finished;
 const double StepMonths=.02;
 void Awake(){Application.logMessageReceived+=Logged;}
 void OnDestroy(){Application.logMessageReceived-=Logged;}
 void Logged(string message,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)_errors.Add(message+"\n"+stack);}
 void Require(bool value,string name){_checks.Add(V.Map("name",name,"passed",value));if(!value)throw new Exception(name);}
 void Near(double actual,double expected,string name)=>Require(Math.Abs(actual-expected)<.00001,name+" actual="+actual+" expected="+expected);
 void Command(string error,string name)=>Require(error=="",name+(error==""?"":": "+error));
 IEnumerator Start(){
  _runtime=GetComponent<CityRuntime>();_ui=GetComponent<CityInterface>();
  _output=Environment.GetEnvironmentVariable("MET_WILD_G1_OUTPUT");var save=Environment.GetEnvironmentVariable("MET_UNITY_SAVE_ROOT");
  if(string.IsNullOrWhiteSpace(_output)||string.IsNullOrWhiteSpace(save)){Debug.LogError("G1 evidence requires MET_WILD_G1_OUTPUT and an isolated MET_UNITY_SAVE_ROOT inside it.");Application.Quit(1);yield break;}
  _output=Path.GetFullPath(_output);Directory.CreateDirectory(_output);
  var routines=new Stack<IEnumerator>();routines.Push(Run(Path.GetFullPath(save)));
  while(routines.Count>0){object next=null;try{var routine=routines.Peek();if(!routine.MoveNext()){routines.Pop();continue;}next=routine.Current;}catch(Exception error){_errors.Add(error.ToString());Finish();yield break;}if(next is IEnumerator nested){routines.Push(nested);continue;}yield return next;}
  Finish();
 }
 string State(){var state=_runtime.Session.snapshot();state.Remove("last_seen_utc");foreach(var block in state["blocks"].Values){block["state"].Remove("wall_mark");block["state"]["beta_time"].Remove("last_seen_utc");}return state.ToJson(false,true);}
 Game G=>_runtime.Game;
 VisualElement Root=>_ui.WildStarWindow.Root;
 void Press(string name){var button=Root.Q<Button>(name);Require(button!=null&&button.enabledInHierarchy&&button.resolvedStyle.display!=DisplayStyle.None,"enabled Toolkit control: "+name);button.Focus();using(var submit=NavigationSubmitEvent.GetPooled()){submit.target=button;button.SendEvent(submit);}}
 IEnumerator Frames(){yield return null;yield return null;yield return null;}
 IEnumerator Capture(string name){yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();Require(image!=null&&image.width==2560&&image.height==1440&&Screen.fullScreen,"fullscreen 2560x1440 capture: "+name);File.WriteAllBytes(Path.Combine(_output,name+".png"),image.EncodeToPNG());Destroy(image);_captures.Add(name+".png");}
 V Bounds(Rect bounds)=>V.Map("x",bounds.x,"y",bounds.y,"width",bounds.width,"height",bounds.height);
 bool Within(Rect inner,Rect outer)=>inner.xMin>=outer.xMin-1&&inner.yMin>=outer.yMin-1&&inner.xMax<=outer.xMax+1&&inner.yMax<=outer.yMax+1;
 void CheckLayout(float scale,float font){
  var window=Root.Q("wild-window");var bounds=window.worldBound;var panel=Root.worldBound;
  Require(bounds.width>900&&bounds.height>500,"measured star window at "+scale+" / "+font);
  Require(Within(bounds,panel),"star window within panel at "+scale+" / "+font);
  var confirm=Root.Q<Button>("wild-confirm");var cancel=Root.Q<Button>("wild-cancel");
  var graph=Root.Q<ScrollView>("wild-graph-scroll");var inspector=Root.Q<ScrollView>("wild-inspector");
  Require(Within(confirm.worldBound,bounds)&&Within(cancel.worldBound,bounds),"footer controls inside window");
  Require(confirm.worldBound.yMin>=inspector.worldBound.yMax-1&&confirm.worldBound.yMin>=graph.worldBound.yMax-1,"footer stays outside scroll content");
  Require(graph.contentViewport.worldBound.width>300&&inspector.contentViewport.worldBound.width>220,"both panes retain usable viewport");
  Require(inspector.horizontalScroller.resolvedStyle.display==DisplayStyle.None,"inspector scrolls vertically");
  var title=Root.Q<Label>("wild-title");Require(title.resolvedStyle.unityFontDefinition.font!=null&&title.worldBound.height>20,"Chinese title font loaded and measured");
  var canvas=Root.Q("wild-graph");foreach(var node in new[]{"compound","housing","production","organization","link","fusion","apartment","agriculture"}){
   var button=Root.Q<Button>("wild-node-"+node);Require(Within(button.worldBound,canvas.worldBound),"node inside scroll canvas: "+node);
   foreach(var label in button.Query<Label>().ToList())Require(label.worldBound.width>0&&label.worldBound.height>0&&Within(label.worldBound,button.worldBound),"node text inside card: "+node+" / "+label.name);
  }
  _layouts.Add(V.Map("ui_scale",scale,"text_scale",font,"panel",Bounds(panel),"window",Bounds(bounds),"confirm",Bounds(confirm.worldBound),"graph_viewport",Bounds(graph.contentViewport.worldBound),"inspector_viewport",Bounds(inspector.contentViewport.worldBound)));
 }
 IEnumerator AdvanceConstruction(string node){
  double material=0,cp=0;int steps=0;var start=G.s["local_hours"].Double;
  while(G.s["projects"][_building]["upgrade_job"].Count>0&&steps<100){
   Command(_runtime.Execute(g=>Sim.tick(g,StepMonths)),"engineering Sim.tick: "+node);steps++;
   material+=G.s["block"]["construction_last"]["material"].Double;cp+=G.s["block"]["construction_last"]["cp"].Double;
   if(steps%3==0)yield return null;
  }
  Require(G.s["projects"][_building]["upgrade_job"].Count==0,"real CP queue completes "+node);
  Require(G.s["projects"][_building]["wild_design"]["nodes"][node]["state"].Text=="complete","completion committed "+node);
  Near(material,10,"construction ledger consumes 10 material: "+node);Near(cp,1,"construction ledger consumes 1 CP: "+node);
  _advances.Add(V.Map("node",node,"method","direct Sim.tick through Runtime.Execute in paused isolated engineering fixture","steps",steps,"step_months",StepMonths,"local_months",G.s["local_hours"].Double-start,"cp",cp,"material",material));
  yield return Frames();
 }
 IEnumerator Run(string save){
  yield return Frames();
  Require(save.StartsWith(_output.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.Ordinal)&&Path.GetFullPath(_runtime.StorageRoot)==save,"explicit isolated save root nested under evidence output");
  Require(GetComponent<CityWorld>().Failure=="","original city renderer ready");
  Require(Screen.width==2560&&Screen.height==1440&&Screen.fullScreen,"fullscreen 2K fixture baseline");
  // A reproducible new city replaces only the explicitly isolated fixture save.
  _runtime.NewCity(20261026);Command(_runtime.ExecuteWorld(city=>city.BeginDistrict(0,"balanced","Wild G1 工程验收")),"begin isolated district");
  _origin=G.s["origin_choices"][0].Text;Command(_runtime.Execute(g=>g.choose_origin(_origin)),"choose actual offered origin");
  Command(_runtime.ExecuteWorld(city=>city.EnableWild()),"explicitly enable Wild");_runtime.SetPaused(true);
  // Two 10-material jobs exceed the normal opening stock. This declared fixture
  // supply keeps engineering checks independent of a normal-play waiting loop.
  Command(_runtime.Execute(g=>{var inventory=g.s["projects"]["municipal"]["inventory"];var prior=inventory.Duplicate(true);inventory["food"]=80;inventory["material"]=40;g.s["debug_used"]=true;g.log_event("wild_g1_engineering_fixture",V.Map("before",prior,"after",inventory,"reason","isolated scripted UI/CP acceptance; not ordinary-play resource or pacing evidence"),"隔离 G1 工程验收补给：食物80、材料40；不作为正常游玩证据。");g.refresh_metrics();return "";}),"declare engineering fixture supplies");
  _building=G.playable_ids().First(id=>G.s["projects"][id]["def"].Text=="homestead"&&G.s["projects"][id]["status"].Text=="running").Text;_tile=G.s["projects"][_building]["tile"].Text;
  Require(_runtime.WorldPaused&&WildDesign.Enabled(G),"isolated Wild fixture paused");Near(WildDesign.View(G,_building)["capacity"]["total"].Double,2,"opening farm has 2 DP");
  _ui.SelectTile(_tile);yield return Frames();yield return Capture("01-wild-map-fixture");
  var state=State();_ui.OpenWildStar(_building);yield return Frames();
  Require(_ui.WildStarOpen&&_ui.IsModal&&_ui.KeyboardOwned&&!_ui.MapInteractionAvailable&&_ui.PointerOwned(Vector2.zero)&&_runtime.DecisionOpen,"star owns map and keyboard and stops runtime");Require(State()==state,"opening star preserves live city and RNG");yield return Capture("02-wild-star");
  Press("wild-node-housing");yield return Frames();Require(Root.Q<Button>("wild-node-housing").ClassListContains("is-selected")&&!Root.Q<Button>("wild-confirm").enabledInHierarchy,"locked housing selectable for explanation without confirmation");Require(State()==state,"candidate selection preserves live state");
  foreach(var scale in new[]{.8f,1f,1.2f})foreach(var font in new[]{1f,1.25f}){
   _runtime.DisplaySettings["ui_scale"]=scale;_runtime.DisplaySettings["text_scale"]=font;_ui.ApplyDisplay();yield return Frames();CheckLayout(scale,font);Require(State()==state,"display setting leaves simulation untouched");yield return Capture("03-layout-ui"+(int)(scale*100)+"-font"+(int)(font*100));
  }
  _runtime.DisplaySettings["ui_scale"]=1;_runtime.DisplaySettings["text_scale"]=1;_ui.ApplyDisplay();yield return Frames();Press("wild-cancel");yield return Frames();
  Require(!_ui.WildStarOpen&&!_runtime.DecisionOpen&&_ui.MapInteractionAvailable,"back to city releases ownership");Require(State()==state,"cancel preview leaves city and RNG unchanged");Require(_ui.SelectedTile==_tile,"cancel retains selected tile");
  _ui.OpenWildStar(_building);yield return Frames();Press("wild-node-compound");yield return Frames();
  double housingBefore=G.project_stats(_building)["housing"].Double,foodBefore=G.project_stats(_building)["output.food"].Double,stockBefore=G.inventory_total("material");
  Press("wild-confirm");yield return Frames();Require(!_ui.WildStarOpen,"confirmed start closes modal");Near(WildDesign.View(G,_building)["capacity"]["used"].Double,1,"start reserves one DP");Near(G.inventory_total("material"),stockBefore,"material is scheduled and not prepaid");
  Require(Construction.queue(G).Any(row=>row["id"].Text==_building),"node joins actual construction queue");Near(G.project_stats(_building)["housing"].Double,housingBefore,"pending node gives no housing benefit");Near(G.project_stats(_building)["output.food"].Double,foodBefore,"pending node gives no food benefit");yield return Capture("04-compound-started");
  _ui.OpenWildStar(_building);yield return Frames();Require(Root.Q<Button>("wild-node-compound").ClassListContains("is-pending")&&!Root.Q<Button>("wild-confirm").enabledInHierarchy,"pending card cannot submit twice");yield return Capture("05-compound-pending");_ui.CloseToolkitUpgrade();yield return Frames();
  yield return AdvanceConstruction("compound");Near(G.project_stats(_building)["housing"].Double,housingBefore*1.1,"completed compound adds housing 10 percent");Near(G.project_stats(_building)["output.food"].Double,foodBefore*1.1,"completed compound adds food 10 percent with terrain retained");
  _ui.OpenWildStar(_building);yield return Frames();Require(Root.Q<Button>("wild-node-compound").ClassListContains("is-completed"),"completed compound rendered");Press("wild-node-housing");yield return Frames();Require(Root.Q<Button>("wild-confirm").enabledInHierarchy,"housing unlocked after real completion");yield return Capture("06-housing-unlocked");Press("wild-confirm");yield return Frames();
  Near(WildDesign.View(G,_building)["capacity"]["used"].Double,2,"second start reserves final DP");yield return AdvanceConstruction("housing");Near(G.project_stats(_building)["housing"].Double,housingBefore*1.2,"housing percentages additive");Near(G.project_stats(_building)["output.food"].Double,foodBefore*1.1,"housing branch leaves food percentage unchanged");
  _ui.OpenWildStar(_building);yield return Frames();Press("wild-node-compound");yield return Frames();yield return Capture("07-two-completed-nodes");var beforeRemove=State();double spentStock=G.inventory_total("material");
  Press("wild-remove");yield return Frames();Require(Root.Q("wild-remove-confirmation").resolvedStyle.display!=DisplayStyle.None,"dependency removal asks for confirmation");Require(State()==beforeRemove,"removal preview leaves domain state unchanged");yield return Capture("08-cascade-removal-confirmation");Press("wild-remove-yes");yield return Frames();
  Near(WildDesign.View(G,_building)["capacity"]["used"].Double,0,"cascade returns both design points");Near(G.inventory_total("material"),spentStock,"spent materials not refunded");Require(G.s["projects"][_building]["wild_design"]["nodes"].Count==0,"cascade removes housing descendant");Near(G.project_stats(_building)["housing"].Double,housingBefore,"cancel removes housing effects");Near(G.project_stats(_building)["output.food"].Double,foodBefore,"cancel removes food effects");
  Require(_ui.WildStarOpen,"removal returns to star for continued planning");yield return Capture("09-capacity-returned");_ui.CloseToolkitUpgrade();yield return Frames();Require(!_runtime.DecisionOpen&&_ui.MapInteractionAvailable,"final close releases input");Command(_runtime.Session.Validate(),"complete city state validates");yield return Capture("10-final-map");
 }
 void Finish(){if(_finished)return;_finished=true;File.WriteAllText(Path.Combine(_output,"player-evidence.json"),V.Map("ok",_errors.Count==0,"scope","Actual Unity Player original city; synthetic NavigationSubmit events and declared isolated fixture supplies; direct Sim.tick CP advancement. Not OS mouse/human acceptance, normal-play pacing or hope-city trade evidence.","resolution",Screen.width+"x"+Screen.height,"fullscreen",Screen.fullScreen,"graphics",SystemInfo.graphicsDeviceName,"save_root",_runtime.StorageRoot,"seed",20261026,"origin",_origin,"building",_building,"fixture_supplies",V.Map("food",80,"material",40,"debug_used",true),"checks",_checks,"captures",_captures,"layouts",_layouts,"engineering_advances",_advances,"errors",V.Arr(_errors.Cast<object>().ToArray())).ToJson(true));Debug.Log("WILD G1 PLAYER "+(_errors.Count==0?"PASS":"FAIL")+" "+_checks.Count+" checks");Application.Quit(_errors.Count==0?0:1);}
}
}
