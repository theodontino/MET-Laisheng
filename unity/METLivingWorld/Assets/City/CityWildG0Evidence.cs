using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MET.City {
// Explicit Player-only engineering journey. Requires an isolated save root supplied by the launcher.
public sealed class CityWildG0Evidence : MonoBehaviour {
 public static bool Requested()=>Environment.GetCommandLineArgs().Contains("--wild-g0-checks");
 readonly V _checks=V.Arr(),_captures=V.Arr();readonly List<string> _errors=new List<string>();
 CityRuntime _runtime;CityInterface _ui;string _output;bool _finished;
 void Awake(){Application.logMessageReceived+=Logged;}
 void OnDestroy(){Application.logMessageReceived-=Logged;}
 void Logged(string message,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)_errors.Add(message+"\n"+stack);}
 void Require(bool condition,string name){_checks.Add(V.Map("name",name,"passed",condition));if(!condition)throw new Exception(name);}
 IEnumerator Start(){
  _runtime=GetComponent<CityRuntime>();_ui=GetComponent<CityInterface>();_output=Environment.GetEnvironmentVariable("MET_WILD_G0_OUTPUT");
  if(string.IsNullOrWhiteSpace(_output)||string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MET_UNITY_SAVE_ROOT"))){Debug.LogError("G0 evidence requires explicit isolated output/save paths.");Application.Quit(1);yield break;}
  Directory.CreateDirectory(_output);var routines=new Stack<IEnumerator>();routines.Push(Run());
  while(routines.Count>0){object next=null;try{var r=routines.Peek();if(!r.MoveNext()){routines.Pop();continue;}next=r.Current;}catch(Exception error){_errors.Add(error.ToString());Finish();yield break;}if(next is IEnumerator child){routines.Push(child);continue;}yield return next;}
  Finish();
 }
 string State(){var s=_runtime.Session.snapshot();s.Remove("last_seen_utc");foreach(var block in s["blocks"].Values){block["state"].Remove("wall_mark");block["state"]["beta_time"].Remove("last_seen_utc");}return s.ToJson(false,true);}
 void Press(Button button){Require(button!=null&&button.enabledInHierarchy,"Toolkit control enabled: "+button?.name);using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=button;button.SendEvent(evt);}}
 IEnumerator Frames(){yield return null;yield return null;yield return null;}
 IEnumerator Capture(string name){yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();Require(image!=null&&image.width==2560&&image.height==1440,"2K Player capture: "+name);File.WriteAllBytes(Path.Combine(_output,name+".png"),image.EncodeToPNG());Destroy(image);_captures.Add(name+".png");}
 void CheckLayout(){
  var root=_ui.ToolkitUpgradeView.Root;var window=root.Q("upgrade-window");var bounds=window.worldBound;
  Require(bounds.width>1000&&bounds.height>600,"measured Toolkit window");Require(bounds.xMin>=0&&bounds.yMin>=0&&bounds.xMax<=root.worldBound.xMax+1&&bounds.yMax<=root.worldBound.yMax+1,"window within panel");
  var footer=root.Q<Button>("upgrade-confirm").worldBound;var scroll=root.Q<ScrollView>("upgrade-body").worldBound;
  Require(footer.yMin>=scroll.yMax-1&&footer.yMax<=bounds.yMax,"confirm fixed outside scroll content: footer="+footer+" scroll="+scroll+" window="+bounds);
  Require(root.Q<Label>("upgrade-title").resolvedStyle.unityFontDefinition.font!=null,"bundled Chinese font");
  Require(root.Q<Label>("upgrade-title").worldBound.height>20,"measured title");
  Require(window.resolvedStyle.backgroundColor.a>.99f&&window.resolvedStyle.backgroundColor.r<.15f,"opaque dark Toolkit stylesheet applied");
  var candidates=root.Q("upgrade-candidates");Require(candidates.resolvedStyle.flexDirection==FlexDirection.Row,"candidates compare horizontally");
  Require(root.Q<ScrollView>("upgrade-body").horizontalScroller.resolvedStyle.display==DisplayStyle.None,"vertical scrolling only");
 }
 IEnumerator Run(){
  yield return Frames();Require(GetComponent<CityWorld>().Failure=="","original city renderer ready");Require(_runtime.Game.s["phase"].Text=="play"&&_runtime.WorldPaused,"isolated starting fixture paused");
  var g=_runtime.Game;var id=g.s["projects"].Keys.First(x=>g.s["projects"][x]["status"].Text=="running"&&Development.choices(g,x).Count>1);var tile=g.s["projects"][id]["tile"].Text;
  _ui.SelectTile(tile);yield return Frames();yield return Capture("01-existing-map");
  _ui.SetToolkitUpgradeEnabled(true);var state=State();_ui.OpenUpgradeWindow(id);yield return Frames();
  Require(_ui.ToolkitUpgradeOpen&&!_ui.ConstructionWindowOpen,"Toolkit replaces only upgrade route");Require(_ui.IsModal&&_ui.KeyboardOwned&&!_ui.MapInteractionAvailable&&_ui.PointerOwned(Vector2.zero)&&_runtime.DecisionOpen,"modal owns map and pauses runtime");
  Require(State()==state,"opening preview preserves session including RNG");
  var root=_ui.ToolkitUpgradeView.Root;Require(!root.Q<Foldout>("upgrade-city-comparison").value,"city changes initially folded");yield return Capture("02-toolkit-default");CheckLayout();
  var choices=Development.choices(g,id);Press(root.Q<Button>("candidate-"+choices[1].Text));yield return Frames();Require(State()==state,"selecting candidate preserves live city");
  Require(_ui.ToolkitUpgradeView.Root.Q<Button>("candidate-"+choices[1].Text).ClassListContains("is-selected"),"selected alternative highlighted");yield return Capture("03-alternative");
  foreach(var scale in new[]{.8f,1f,1.2f})foreach(var font in new[]{1f,1.25f,1.5f}){
   _runtime.DisplaySettings["ui_scale"]=scale;_runtime.DisplaySettings["text_scale"]=font;_ui.ApplyDisplay();yield return Frames();CheckLayout();
   if(scale==1.2f&&font==1.5f)yield return Capture("04-large-type");
  }
  _runtime.DisplaySettings["ui_scale"]=1;_runtime.DisplaySettings["text_scale"]=1;_ui.ApplyDisplay();yield return Frames();
  root=_ui.ToolkitUpgradeView.Root;root.Q<Foldout>("upgrade-city-comparison").value=true;yield return Frames();var scroll=root.Q<ScrollView>("upgrade-body");scroll.scrollOffset=new Vector2(0,scroll.verticalScroller.highValue);yield return Frames();Require(scroll.scrollOffset.y>0&&root.Q("upgrade-city-rows").worldBound.yMax<=scroll.contentViewport.worldBound.yMax+1,"expanded comparison scrolls to final row");yield return Capture("05-city-comparison");
  Press(root.Q<Button>("upgrade-cancel"));yield return Frames();Require(!_ui.ToolkitUpgradeOpen&&!_runtime.DecisionOpen&&_ui.MapInteractionAvailable,"cancel releases ownership");Require(State()==state,"cancel did not charge or draw RNG");Require(_ui.SelectedTile==tile,"cancel retains tile");Require(GameObject.Find("HUD Overview")!=null,"cancel restores city overview while paused");
  _ui.OpenUpgradeWindow(id);yield return Frames();root=_ui.ToolkitUpgradeView.Root;Require(!root.Q<Foldout>("upgrade-city-comparison").value&&root.Q<ScrollView>("upgrade-body").scrollOffset.y<1,"reopen restores clean viewport");
  var choice=choices[0].Text;double cost=Development.preview(g,id,choice)["cost"].Double,balance=g.s["currency"].Double;
  Press(root.Q<Button>("upgrade-confirm"));yield return Frames();Require(!_ui.ToolkitUpgradeOpen,"successful command closes modal");Require(Math.Abs(g.s["currency"].Double-(balance-cost))<.0001,"exactly one confirmed payment");Require(g.s["projects"][id]["upgrade_job"].Count>0,"existing upgrade command joined real queue");Require(_runtime.Session.Validate()=="","city validates after command");Require(GameObject.Find("HUD Overview")!=null,"confirmed command restores city overview while paused");yield return Capture("06-upgrade-started");
  _ui.OpenUpgradeWindow(id);yield return Frames();Require(!_ui.ToolkitUpgradeView.Root.Q<Button>("upgrade-confirm").enabledInHierarchy,"in-progress project cannot submit duplicate");yield return Capture("07-in-progress-constraint");_ui.CloseToolkitUpgrade();yield return Frames();
  _ui.SetToolkitUpgradeEnabled(false);_ui.OpenUpgradeWindow(id);yield return Frames();Require(_ui.ConstructionWindowOpen&&!_ui.ToolkitUpgradeOpen,"legacy upgrade remains switchable");_ui.CloseConstructionWindow();
 }
 void Finish(){if(_finished)return;_finished=true;File.WriteAllText(Path.Combine(_output,"player-evidence.json"),V.Map("ok",_errors.Count==0,"scope","Actual macOS Unity Player, original city scene; synthetic UI navigation-submit events, not human mouse acceptance or gameplay judgement","resolution",Screen.width+"x"+Screen.height,"graphics",SystemInfo.graphicsDeviceName,"checks",_checks,"captures",_captures,"errors",V.Arr(_errors.Cast<object>().ToArray())).ToJson(true));Debug.Log("WILD G0 PLAYER "+(_errors.Count==0?"PASS":"FAIL")+" "+_checks.Count+" checks");Application.Quit(_errors.Count==0?0:1);}
}
}
