using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MET.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MET.City.Editor {
/// <summary>
/// Actual uGUI/navigation engineering checks with an isolated CitySession.
/// The empty PlayMode scene permits the renderer's deferred Destroy calls;
/// the inactive CityRuntime/CityInterface owner never receives Awake or Update.
/// No Player store, display preferences, or project settings are read or written.
/// </summary>
[InitializeOnLoad]
public static class V3WorkspaceChecks {
 const string Pending="MET.V3WorkspaceChecks.Pending";
 const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static IEnumerator _routine;
 static int _lastFrame=-1,_renders,_assertions;
 static string _case="initialization";
 static readonly List<string> Errors=new List<string>();
 static GameObject _owner,_canvas;
 static CityRuntime _runtime;
 static CityInterface _ui;
 static RectTransform _root;

 static V3WorkspaceChecks(){EditorApplication.playModeStateChanged+=PlayState;}
 static FieldInfo Field(object target,string name)=>target.GetType().GetField(name,Hidden)??throw new MissingFieldException(target.GetType().Name,name);
 static void Set(object target,string name,object value)=>Field(target,name).SetValue(target,value);
 static T Get<T>(object target,string name)=>(T)Field(target,name).GetValue(target);
 static void Property(object target,string name,object value)=>target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(target,value);
 static object Call(object target,string name,params object[] values){
  try{return (target.GetType().GetMethod(name,Hidden)??throw new MissingMethodException(target.GetType().Name,name)).Invoke(target,values);}
  catch(TargetInvocationException error){throw new Exception(_case+" / "+name,error.InnerException??error);}
 }
 static void Require(bool condition,string message){_assertions++;if(!condition)throw new Exception(_case+": "+message);}
 static void Okay(string error,string operation){if(error!="")throw new Exception(operation+": "+error);}

 // Do not add -quit: the async PlayMode checks exit the Editor themselves.
 public static void EntryRun()=>Run();
 public static void Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogError("V3 workspace checks require an idle batch Editor.");EditorApplication.Exit(1);return;}
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
 }
 static void PlayState(PlayModeStateChange state){
  if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false)||_routine!=null)return;
  Errors.Clear();Application.logMessageReceived+=CaptureError;_renders=_assertions=0;_routine=Checks();_lastFrame=-1;EditorApplication.update+=Step;
 }
 static void CaptureError(string message,string trace,LogType type){
  // Unity 6.6's editor search indexer throws in this headless empty scene before
  // the fixture runs. Retain it in the log; it is outside the game/UI graph.
  if(type==LogType.Exception&&trace.Contains("UnityEditor.Search.SearchDatabase")&&!trace.Contains("MET.City")){Debug.LogWarning("Editor search-index startup exception retained above; excluded from isolated game UI assertions.");return;}
  if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Errors.Add(message);
 }
 static void Step(){
  if(!EditorApplication.isPlaying||UnityEngine.Time.frameCount==_lastFrame)return;_lastFrame=UnityEngine.Time.frameCount;
  try{if(!_routine.MoveNext())Finish(null);}catch(Exception error){Finish(error);}
 }
 static void Finish(Exception failure){
  EditorApplication.update-=Step;Application.logMessageReceived-=CaptureError;SessionState.SetBool(Pending,false);_routine=null;
  if(failure==null&&Errors.Count>0)failure=new Exception("Unexpected Unity error logs: "+string.Join(" | ",Errors.Take(5)));
  if(_owner!=null)UnityEngine.Object.DestroyImmediate(_owner);if(_canvas!=null)UnityEngine.Object.DestroyImmediate(_canvas);
  if(failure!=null){Debug.LogError("V3 WORKSPACE CHECKS FAIL at "+_case);Debug.LogException(failure);EditorApplication.Exit(1);return;}
  Debug.Log("V3 WORKSPACE CHECKS PASS: "+_renders+" actual page renders, "+_assertions+" assertions. Isolated session; 2560x1440 reference canvas at UI 80/100/120%; compact/advanced navigation, map return, and visible control geometry. No Player/mouse/screenshot acceptance.");EditorApplication.Exit(0);
 }
 static void BuildFixture(){
  var root=Path.Combine(Path.GetTempPath(),"met-v3-workspace-checks-"+Guid.NewGuid().ToString("N"));
  var session=new CitySession(new Catalog(Path.Combine(Application.streamingAssetsPath,"MetContent")),root);session.New(902510,1000);
  Okay(session.BeginDistrict(0,"balanced"),"begin isolated district");var game=session.FocusedGame;var before=game.snapshot();Okay(game.choose_origin(game.s["origin_choices"][0].Text),"choose isolated origin");session.AfterLocalCommand(before);
  Okay(Themes.select(game,"agricultural"),"select fixture theme");
  // Populate both inventory and installed records through the real commands.
  var match=(from bid in game.c.bonuses.Keys from host in game.playable_ids().Select(v=>v.Text) from slot in game.slot_ids(host).Select(v=>v.Text)
   where Bonus.card(game,bid)["scope"].Text=="building"&&game.bonus_eligible(bid,"building",host,slot)=="" select new {bid,host,slot}).First();
  Okay(Bonus.grant(game,match.bid,"v3-check:installed"),"grant fixture copy");Okay(Bonus.install(game,match.bid,"building",match.host,match.slot),"install fixture copy");Okay(Bonus.grant(game,match.bid,"v3-check:inventory"),"grant spare fixture copy");
  var alternative=(from bid in game.c.bonuses.Keys where bid!=match.bid from host in game.playable_ids().Select(v=>v.Text) from slot in game.slot_ids(host).Select(v=>v.Text)
   where Bonus.card(game,bid)["scope"].Text=="building"&&game.bonus_eligible(bid,"building",host,slot,game.s["projects"][host]["slots"][slot].Text)=="" select bid).First();
  Okay(Bonus.grant(game,alternative,"v3-check:alternative"),"grant fixture alternative");
  Okay(Sim.tick(game,game.c.balance["tick_hours"].Double),"settle fixture month step");Bonus.draw(game,"v3-check:pending-reward");session.s["paused"]=true;
  _owner=new GameObject("V3 workspace isolated inactive owner");_owner.SetActive(false);_runtime=_owner.AddComponent<CityRuntime>();_ui=_owner.AddComponent<CityInterface>();
  Property(_runtime,"Session",session);Property(_runtime,"StorageRoot",root);Set(_runtime,"_saveEnabled",false);Call(_runtime,"Publish",null,null,false);
  Property(_ui,"Runtime",_runtime);Set(_ui,"_font",Resources.Load<Font>("CityFonts/NotoSansSC")??Font.CreateDynamicFontFromOSFont("PingFang SC",18));Call(_ui,"UpdateDetached",_runtime.Snapshot);Call(_ui,"BuildShell");
  _root=Get<RectTransform>(_ui,"_root");_canvas=_root.gameObject;
  // WorldSpace keeps the specified reference size independent of batch Editor's
  // headless GameView size. This is layout engineering, not display acceptance.
  _canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;_canvas.GetComponent<CanvasScaler>().enabled=false;_root.SetParent(null,false);_root.sizeDelta=new Vector2(2560,1440);_root.localScale=Vector3.one;
  Call(_ui,"OnCommitted",_runtime.Snapshot);Require(!_owner.activeInHierarchy,"runtime/UI owner unexpectedly active");Require(!Directory.Exists(root),"fixture wrote a storage directory");
 }
 static IEnumerator Checks(){
  BuildFixture();yield return null;var source=_runtime.Session.snapshot().ToJson(false,true);
  foreach(float scale in new[]{.8f,1f,1.2f}) foreach(float font in new[]{1f,1.25f,1.5f}){
   _runtime.DisplaySettings["text_scale"]=font;
   _runtime.DisplaySettings["ui_scale"]=scale;_root.sizeDelta=new Vector2(2560/scale,1440/scale);_ui.ApplyDisplay();yield return null;
   foreach(var route in new[]{"population_home","economy_home","construction","market","tasks_home","bonus_home"}){
    _case=route+" UI "+scale+" font "+font;_ui.OpenArchive(route);yield return null;AssertRendered(false);Require((string)Call(_ui,"ArchiveCategory",route)==(route=="market"?"economy_home":route),"incorrect sidebar section");
    Call(_ui,"OpenAdvancedWorkspace",null,null);yield return null;AssertRendered(true);
    string advanced=route=="population_home"?"population":route=="economy_home"?"resources":route;Require(Get<string>(_ui,"_page")==advanced,"wrong expanded page");
    var parents=Get<Stack<KeyValuePair<string,string>>>(_ui,"_navigation");if(route=="population_home"||route=="economy_home")Require(parents.Count==1&&parents.Peek().Key==route,"expanded alias lost its canonical parent");
    Call(_ui,"CollapseAdvancedWorkspace");yield return null;AssertRendered(false);Require(Get<string>(_ui,"_page")==route,"collapse did not return to the operating page");
   }
  }
  _case="five resource contexts";
  foreach(var resource in new[]{"food","material","goods","energy","currency"}){
   Set(_ui,"_operatingEconomyResource",resource);_ui.OpenArchive("economy_home");yield return null;AssertRendered(false);
   Call(_ui,"OpenAdvancedWorkspace","resources",resource);yield return null;AssertRendered(true);Require(Get<string>(_ui,"_pageContext")==resource,"expanded resource scope changed");Require(((string)Call(_ui,"NavigationTrail")).Contains(resource=="energy"?"电力":resource=="currency"?"财政":Catalog.RESOURCE_NAMES.Get(resource,resource).Text),"resource missing from navigation trail");
   Call(_ui,"CollapseAdvancedWorkspace");yield return null;Require(Get<string>(_ui,"_operatingEconomyResource")==resource,"resource selection lost on collapse");
  }
  _case="population service filters";
  foreach(var filter in new[]{"all","homeless","medical","education","entertainment","commercial"}){Set(_ui,"_operatingResidentFilter",filter);_ui.OpenArchive("population_home");yield return null;AssertRendered(false);}
  _case="task advanced contexts";
  foreach(var tab in new[]{"growth","completed"}){Call(_ui,"OpenAdvancedWorkspace","tasks_home",tab);yield return null;AssertRendered(true);Require(Get<string>(_ui,"_pageContext")==tab,"task context changed");Call(_ui,"CollapseAdvancedWorkspace");yield return null;Require(Get<string>(_ui,"_pageContext")=="current","task collapse did not restore current matters");}
  _case="Bonus inventory filters";
  foreach(var filter in new[]{"available","deployed","all"}){Set(_ui,"_v3BonusFilter",filter);_ui.OpenArchive("bonus_home");yield return null;AssertRendered(false);}
  // Force an actual owned alternative's target/slot/same-step preview path.
  var installed=_runtime.Game.s["bonus_instances"].Values.First(v=>v.Get("active",false).Bool)["bonus_id"].Text;
  var alternative=_runtime.Game.s["block"]["bonus_wallet"].Keys.First(id=>id!=installed);Set(_ui,"_v3BonusFilter","available");Set(_ui,"_inventoryChoice",alternative);_ui.OpenArchive("bonus_home");yield return null;AssertRendered(false);
  Require(_ui.BlueprintDiagnostics["preview"].Get("ok",false).Bool,"owned compatible Bonus did not produce a command preview");
  _case="map selection and return context";Set(_ui,"_operatingEconomyResource","goods");_ui.OpenArchive("economy_home");yield return null;
  string page=Get<string>(_ui,"_page"),context=Get<string>(_ui,"_pageContext"),key=(string)Call(_ui,"NavigationTrail");var body=Get<RectTransform>(_ui,"_body");int children=body.childCount;var parentFrames=Get<Stack<KeyValuePair<string,string>>>(_ui,"_navigation").ToArray();
  var tiles=_runtime.Game.s["cells"].Keys.Take(2).ToArray();_ui.SelectTile(tiles[0]);yield return null;
  Require(_ui.MapViewing&&_ui.MapInteractionAvailable&&!_ui.IsModal,"compact map handoff still owns map input");Require(!Get<GameObject>(_ui,"_shield").activeSelf,"compact page shield still covers map");Require(Get<RectTransform>(_ui,"_context").gameObject.activeSelf,"local tile panel hidden after map selection");
  _ui.SelectTile(tiles[1]);yield return null;Require(_ui.SelectedTile==tiles[1],"second map selection rejected");Call(_ui,"ReturnFromMapView");yield return null;AssertRendered(false);
  Require(!_ui.MapViewing&&_ui.SelectedTile==tiles[1],"return lost map selection");Require(Get<string>(_ui,"_page")==page&&Get<string>(_ui,"_pageContext")==context&&Get<string>(_ui,"_operatingEconomyResource")=="goods","map return lost operating context");Require((string)Call(_ui,"NavigationTrail")==key&&body.childCount==children,"map return rebuilt or changed page content");Require(Get<Stack<KeyValuePair<string,string>>>(_ui,"_navigation").SequenceEqual(parentFrames),"map return changed parent stack");
  Call(_ui,"OpenAdvancedWorkspace","resources","goods");yield return null;var selected=_ui.SelectedTile;_ui.SelectTile(tiles[0]);Require(_ui.SelectedTile==selected&&!_ui.MapInteractionAvailable,"advanced desk failed to retain keyboard/map ownership");
  _case="section visit restoration";Call(_ui,"OpenSection","tasks_home");yield return null;Call(_ui,"OpenSection","economy_home");yield return null;Require(Get<string>(_ui,"_page")=="resources"&&Get<string>(_ui,"_pageContext")=="goods"&&Get<bool>(_ui,"_advancedWorkspace"),"section visit lost expanded resource context");
  Require(_runtime.Session.snapshot().ToJson(false,true)==source,"presentation/navigation changed live session data or RNG");Require(!Directory.Exists(_runtime.StorageRoot),"checks wrote fixture save/display data");Require(!_owner.activeInHierarchy,"runtime/UI Awake owner became active");
 }
 static void AssertRendered(bool advanced){
  _renders++;Require(Get<bool>(_ui,"_advancedWorkspace")==advanced,"incorrect compact/advanced state");var archive=Get<RectTransform>(_ui,"_archive");var body=Get<RectTransform>(_ui,"_body");var viewport=Get<RectTransform>(_ui,"_archiveScroll");
  LayoutRebuilder.ForceRebuildLayoutImmediate(archive);LayoutRebuilder.ForceRebuildLayoutImmediate(body);Canvas.ForceUpdateCanvases();
  Require(body.GetComponentsInChildren<Text>().Any(t=>!string.IsNullOrWhiteSpace(t.text)),"page rendered no content");Require(((string)Call(_ui,"PageScopeLabel")).StartsWith("街区",StringComparison.Ordinal),"operating page lost block scope");
  foreach(var rect in new[]{archive,viewport})Require(Positive(rect.rect.width)&&Positive(rect.rect.height),"non-positive workspace/viewport dimensions at "+rect.name);
  Require(Get<GameObject>(_ui,"_shield").GetComponent<Image>().raycastTarget==advanced,"shield ownership disagrees with workspace size");
  foreach(var control in archive.GetComponentsInChildren<Selectable>())if(control.gameObject.activeInHierarchy){var rect=(RectTransform)control.transform;Require(Positive(rect.rect.width)&&Positive(rect.rect.height),"non-positive visible control: "+PathOf(rect)+" = "+rect.rect.size);}
  // Zero-width fills at a genuine 0% and empty layout containers are valid;
  // scope this geometry check to the workspace and actual visible controls.
 }
 static bool Positive(float value)=>value>0&&!float.IsInfinity(value)&&!float.IsNaN(value);
 static string PathOf(Transform node){string path=node.name;for(var parent=node.parent;parent!=null&&parent!=_root;parent=parent.parent)path=parent.name+"/"+path;return path;}
}
}
