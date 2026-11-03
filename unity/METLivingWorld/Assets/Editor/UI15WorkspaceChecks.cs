using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MET.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MET.City.Editor {
/// <summary>
/// Focused UI15 regression paths in an isolated, inactive Runtime/Interface fixture.
/// Layout and optional RenderTexture captures use a fixed 2560x1440 desktop reference.
/// They are Editor uGUI fixture evidence, not native Player or mouse/keyboard acceptance.
/// </summary>
[InitializeOnLoad]
public static class UI15WorkspaceChecks {
 const string Pending="MET.UI15WorkspaceChecks.Pending";
 const BindingFlags All=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static readonly Stack<IEnumerator> Routines=new Stack<IEnumerator>();
 static readonly List<string> Errors=new List<string>();
 static readonly List<string> Cases=new List<string>();
 static readonly List<string> Captures=new List<string>();
 static GameObject _owner,_canvas,_cameraObject;
 static CityRuntime _runtime;
 static CityInterface _ui;
 static RectTransform _root;
 static Camera _camera;
 static RenderTexture _renderTexture;
 static string _case="initialization",_storageRoot,_output,_captureUnavailable="";
 static int _lastFrame=-1,_assertions,_renders;
 static float _uiScale=1,_fontScale=1;

 static UI15WorkspaceChecks(){EditorApplication.playModeStateChanged+=PlayState;}
 static FieldInfo Field(object target,string name)=>target.GetType().GetField(name,All)??throw new MissingFieldException(target.GetType().Name,name);
 static T Get<T>(object target,string name)=>(T)Field(target,name).GetValue(target);
 static void Set(object target,string name,object value)=>Field(target,name).SetValue(target,value);
 static void Property(object target,string name,object value)=>(target.GetType().GetProperty(name,All)??throw new MissingMemberException(name)).SetValue(target,value);
 static bool Flag(string name)=>(bool)(_ui.GetType().GetProperty(name,All)??throw new MissingMemberException(name)).GetValue(_ui);
 static object Call(object target,string name,params object[] values){
  try{return (target.GetType().GetMethod(name,All)??throw new MissingMethodException(target.GetType().Name,name)).Invoke(target,values);}
  catch(TargetInvocationException error){throw new Exception(_case+" / "+name,error.InnerException??error);}
 }
 static void Require(bool okay,string message){_assertions++;if(!okay)throw new Exception(_case+": "+message);}
 static void Okay(string error,string command){Require(error=="",command+": "+error);}
 static void Case(string name){_case=name;Cases.Add(name);}
 static string Number(double value,int digits=1)=>value.ToString("F"+digits,CultureInfo.InvariantCulture);
 static string LiveSource()=>_runtime.Session.snapshot().ToJson(false,true);
 static Text[] Texts(Transform root)=>root.GetComponentsInChildren<Text>().Where(t=>t.gameObject.activeInHierarchy).ToArray();
 static RectTransform Named(Transform parent,string name)=>parent.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name==name)??throw new Exception(_case+": missing surface "+name);
 static Button ButtonNamed(string name)=>_root.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.name==name)??throw new Exception(_case+": missing button "+name);

 // Start with an idle Editor; do not add -quit because this asynchronous entrypoint exits itself.
 public static void EntryRun()=>Run();
 public static void Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogError("UI15 checks require an idle batch Editor.");EditorApplication.Exit(1);return;}
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
 }
 static void PlayState(PlayModeStateChange state){
  if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false)||Routines.Count>0)return;
  Errors.Clear();Cases.Clear();Captures.Clear();_assertions=_renders=0;_lastFrame=-1;
  Application.logMessageReceived+=CaptureError;Routines.Push(Checks());EditorApplication.update+=Step;
 }
 static void CaptureError(string message,string trace,LogType type){
  if(type==LogType.Exception&&trace.Contains("UnityEditor.Search.SearchDatabase")&&!trace.Contains("MET.City"))return;
  if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Errors.Add(message);
 }
 static void Step(){
  if(!EditorApplication.isPlaying||UnityEngine.Time.frameCount==_lastFrame)return;_lastFrame=UnityEngine.Time.frameCount;
  try {
   while(Routines.Count>0){var routine=Routines.Peek();if(!routine.MoveNext()){Routines.Pop();continue;}if(routine.Current is IEnumerator child){Routines.Push(child);continue;}return;}
   Finish(null);
  }catch(Exception error){Finish(error);}
 }
 static void Finish(Exception error){
  EditorApplication.update-=Step;Application.logMessageReceived-=CaptureError;SessionState.SetBool(Pending,false);Routines.Clear();
  if(error==null&&Errors.Count>0)error=new Exception("Unexpected Unity errors: "+string.Join(" | ",Errors.Take(5)));
  if(!string.IsNullOrEmpty(_output)){
   Directory.CreateDirectory(_output);var report=V.Map("ok",error==null,"case",_case,"assertions",_assertions,"targeted_renders",_renders,"cases",V.Arr(Cases.Cast<object>().ToArray()),"screenshots",V.Arr(Captures.Cast<object>().ToArray()),
    "scope","Isolated Editor uGUI fixture; fixed 2560x1440 reference. No actual Player, city renderer, or OS mouse/keyboard acceptance.","capture_unavailable",_captureUnavailable,"graphics_device",SystemInfo.graphicsDeviceType.ToString(),"fixture_storage_created",Directory.Exists(_storageRoot??""),"error",error?.ToString()??"");
   File.WriteAllText(Path.Combine(_output,"ui15-workspace-checks.json"),report.ToJson(true));
  }
  if(_cameraObject!=null)UnityEngine.Object.DestroyImmediate(_cameraObject);if(_renderTexture!=null){_renderTexture.Release();UnityEngine.Object.DestroyImmediate(_renderTexture);}
  if(_owner!=null)UnityEngine.Object.DestroyImmediate(_owner);if(_canvas!=null)UnityEngine.Object.DestroyImmediate(_canvas);
  if(error!=null){Debug.LogError("UI15 WORKSPACE CHECKS FAIL at "+_case);Debug.LogException(error);EditorApplication.Exit(1);return;}
  Debug.Log("UI15 WORKSPACE CHECKS PASS: "+_renders+" targeted renders, "+_assertions+" assertions, "+Captures.Count+" isolated fixture PNGs. Report: "+Path.Combine(_output,"ui15-workspace-checks.json")+". No Player/mouse acceptance.");EditorApplication.Exit(0);
 }
 static void BuildFixture(){
  _output=Environment.GetEnvironmentVariable("MET_UI15_CHECK_OUTPUT");if(string.IsNullOrWhiteSpace(_output))_output=Path.Combine(Path.GetTempPath(),"met-ui15-workspace-checks");
  _storageRoot=Path.Combine(Path.GetTempPath(),"met-ui15-unused-store-"+Guid.NewGuid().ToString("N"));
  var session=new CitySession(new Catalog(Path.Combine(Application.streamingAssetsPath,"MetContent")),_storageRoot);session.New(902515,1000);
  Okay(session.BeginDistrict(0,"balanced"),"begin isolated district");var game=session.FocusedGame;var before=game.snapshot();Okay(game.choose_origin(game.s["origin_choices"][0].Text),"choose isolated origin");session.AfterLocalCommand(before);session.s["paused"]=true;
  _owner=new GameObject("UI15 isolated inactive owner");_owner.SetActive(false);_runtime=_owner.AddComponent<CityRuntime>();_ui=_owner.AddComponent<CityInterface>();
  Property(_runtime,"Session",session);Property(_runtime,"StorageRoot",_storageRoot);Set(_runtime,"_saveEnabled",false);Call(_runtime,"Publish",null,null,false);
  Property(_ui,"Runtime",_runtime);Set(_ui,"_font",Resources.Load<Font>("CityFonts/NotoSansSC")??Font.CreateDynamicFontFromOSFont("PingFang SC",18));Call(_ui,"UpdateDetached",_runtime.Snapshot);Call(_ui,"BuildShell");
  _root=Get<RectTransform>(_ui,"_root");_canvas=_root.gameObject;_canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;_canvas.GetComponent<CanvasScaler>().enabled=false;
  _root.SetParent(null,false);_root.localPosition=Vector3.zero;_root.localRotation=Quaternion.identity;_root.localScale=Vector3.one;_root.sizeDelta=new Vector2(2560,1440);Call(_ui,"OnCommitted",_runtime.Snapshot);
  // Render a real active Canvas while the Runtime/UI MonoBehaviours remain inactive.
  if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)_captureUnavailable="Null graphics device; geometry/source assertions only.";
  else {
   _cameraObject=new GameObject("UI15 fixture capture camera");_camera=_cameraObject.AddComponent<Camera>();_camera.orthographic=true;_camera.orthographicSize=720;_camera.aspect=2560f/1440;_camera.transform.position=new Vector3(0,0,-100);_camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=new Color(.025f,.04f,.05f,1);_camera.useOcclusionCulling=false;
   _renderTexture=new RenderTexture(2560,1440,24,RenderTextureFormat.ARGB32);_renderTexture.Create();_camera.targetTexture=_renderTexture;_canvas.GetComponent<Canvas>().worldCamera=_camera;
  }
  Require(!_owner.activeInHierarchy,"Runtime/UI owner became active");Require(!Directory.Exists(_storageRoot),"fixture created a save directory");Require(game.s["simulation_tick"].Long==0,"fixture settled before pending-data checks");
 }
 static void Scale(float ui,float font){
  _uiScale=ui;_fontScale=font;_runtime.DisplaySettings["ui_scale"]=ui;_runtime.DisplaySettings["text_scale"]=font;_root.sizeDelta=new Vector2(2560/ui,1440/ui);if(_camera!=null)_camera.orthographicSize=720/ui;_ui.ApplyDisplay();
 }
 static IEnumerator LayoutFrame(){
  yield return null;Canvas.ForceUpdateCanvases();foreach(var rect in _root.GetComponentsInChildren<RectTransform>().Where(t=>t.gameObject.activeInHierarchy&&t.GetComponent<LayoutGroup>()!=null).Reverse())LayoutRebuilder.ForceRebuildLayoutImmediate(rect);Canvas.ForceUpdateCanvases();yield return null;
 }
 static IEnumerator Checks(){
  BuildFixture();yield return LayoutFrame();string source=LiveSource();
  yield return PendingDataChecks();yield return NavigationChecks();yield return GoalPolarityChecks();yield return DrawerAndMapChecks();yield return StandaloneDrawerMapChecks();
  Require(LiveSource()==source,"read-only workspace paths changed session state or RNG");
  yield return ConstructionChecks();yield return RewardChecks();yield return SettledWorkspaceCaptures();
  Require(!Directory.Exists(_storageRoot),"fixture wrote save/display preferences");Require(!_owner.activeInHierarchy,"Runtime/UI owner became active during checks");
 }
 static IEnumerator PendingDataChecks(){
  Case("unsettled energy detail");Call(_ui,"OpenAdvancedWorkspace","resources","energy");yield return LayoutFrame();_renders++;
  var body=Get<RectTransform>(_ui,"_body");AssertUnknownMeter(Named(body,"Operating meter 供给 / 需求"));Require(Texts(body).Any(t=>t.text.Contains("待结算")),"energy detail omitted pending state");AssertWorkspace();Capture("01-unsettled-energy-editor-fixture.png");
  Case("unsettled population allocation");_ui.OpenArchive("population_home");yield return LayoutFrame();_renders++;body=Get<RectTransform>(_ui,"_body");
  foreach(var name in new[]{"居民就业","本地岗位使用"})AssertUnknownMeter(Named(body,"Operating meter "+name));
  foreach(var kind in new[]{"medical","education","entertainment","commercial"})AssertUnknownMeter(Named(body,"Operating meter "+Catalog.SERVICE_NAMES.Get(kind,kind).Text));
  Call(_ui,"OpenAdvancedWorkspace","population","all");yield return LayoutFrame();_renders++;Require(Texts(Get<RectTransform>(_ui,"_body")).Any(t=>t.text.Contains("待首次实际结算")),"population detail invented an actual allocation");AssertWorkspace();
 }
 static void AssertUnknownMeter(RectTransform meter){
  var texts=Texts(meter);Require(texts.Any(t=>t.text=="待结算"),meter.name+" lacks the actual pending readout");Require(!texts.Any(t=>t.text.Contains("0.0 / 0.0")||t.text.Contains("100.0%")),meter.name+" renders a fabricated zero/100% actual value");Require(!meter.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="Operating meter fill"),meter.name+" draws a measured fill before settlement");
 }
 static IEnumerator NavigationChecks(){
  Case("topbar currency selects fiscal context");Set(_ui,"_operatingEconomyResource","goods");_ui.OpenArchive("economy_home");yield return LayoutFrame();ButtonNamed("HUD Resource 通货").onClick.Invoke();yield return LayoutFrame();_renders++;
  Require(Get<string>(_ui,"_operatingEconomyResource")=="currency","currency click retained a different resource");Require(Get<string>(_ui,"_page")=="economy_home","currency click did not reach fiscal workspace");
  Case("medical goal destination preserves service filter");Call(_ui,"HandleGoalDestination","medical");yield return LayoutFrame();_renders++;
  Require(Get<string>(_ui,"_operatingResidentFilter")=="medical","medical goal lost its service filter");Require(new[]{"population_home","population"}.Contains(Get<string>(_ui,"_page")),"medical goal reached the wrong workspace");
  foreach(var goal in new[]{new[]{"energy","energy"},new[]{"goods_rate","goods"},new[]{"industrial_usage","goods"}}){
   Case(goal[0]+" goal selects its real resource detail");Call(_ui,"HandleGoalDestination",goal[0]);yield return LayoutFrame();_renders++;
   Require(Get<string>(_ui,"_page")=="resources"&&Get<string>(_ui,"_pageContext")==goal[1],goal[0]+" goal did not reach the matching resource detail");Require(Get<string>(_ui,"_operatingEconomyResource")==goal[1],goal[0]+" lost resource selection");
  }
  Case("commercial usage goal selects commercial residents");Call(_ui,"HandleGoalDestination","commercial_usage");yield return LayoutFrame();_renders++;
  Require(Get<string>(_ui,"_page")=="population_home"&&Get<string>(_ui,"_operatingResidentFilter")=="commercial","commercial usage goal lost the consumer service filter");
  Case("policies have compact operating workspace");Call(_ui,"Navigate","policies","");yield return LayoutFrame();_renders++;Require(!Get<bool>(_ui,"_advancedWorkspace"),"policy peer tab still forces advanced mode");Require((bool)Call(_ui,"HasCompactVersion","policies"),"policies lack compact version");
  var body=Get<RectTransform>(_ui,"_body");Require(Texts(body).Any(t=>t.text.Contains("经营政策")),"compact policies omitted current/candidate management");Require(body.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="Policy effect 经营净额 /月"),"policy same-step comparison missing");AssertWorkspace();Capture("02-compact-policies-editor-fixture.png");
 }
 static IEnumerator GoalPolarityChecks(){
  Case("upper-bound objective uses threshold text without increasing progress bar");
  var fixture=new GameObject("UI15 objective polarity fixture",typeof(RectTransform));fixture.transform.SetParent(_root,false);
  var upper=new GameObject("Upper-bound objective fixture",typeof(RectTransform));upper.transform.SetParent(fixture.transform,false);
  Call(_ui,"V3DevMeter",upper.transform,"待安置人口上限",12.0,10.0," 人",false,true);
  Call(_ui,"V3DevMeter",upper.transform,"待安置人口已达上限条件",8.0,10.0," 人",true,true);yield return LayoutFrame();_renders++;
  Require(!upper.GetComponentsInChildren<CityGraphic>().Any(chart=>chart.Kind=="bar"),"upper-bound objective still draws current/target as growing completion");
  Require(Texts(upper.transform).Count(text=>text.text.Contains("≤ "+Number(10)+" 人"))==2,"upper-bound conditions lost their explicit maximum threshold");
  var lower=new GameObject("Lower-bound objective fixture",typeof(RectTransform));lower.transform.SetParent(fixture.transform,false);Call(_ui,"V3DevMeter",lower.transform,"安居人数目标",5.0,10.0," 人",false,false);yield return LayoutFrame();
  Require(lower.GetComponentsInChildren<CityGraphic>().Any(chart=>chart.Kind=="bar"&&Math.Abs(chart.Ratio-.5f)<.001f),"ordinary increasing objective lost its meaningful target progress");
  UnityEngine.Object.Destroy(fixture);yield return null;
 }
 static IEnumerator StandaloneDrawerMapChecks(){
  Scale(1,1);Case("standalone drawer Locate and return do not create an archive shield");Call(_ui,"CloseArchive");yield return LayoutFrame();
  string id=_runtime.Game.playable_ids().Select(v=>v.Text).First(key=>_runtime.Game.s["projects"][key]["status"].Text=="running");Call(_ui,"Navigate","building",id);yield return LayoutFrame();_renders++;
  Require(!Get<bool>(_ui,"_archiveOpen")&&Flag("ObjectDrawerOpen"),"standalone building inspection created a workspace");Require(!Get<GameObject>(_ui,"_shield").activeSelf,"standalone drawer began with an archive shield");AssertDrawer();
  var locate=Get<RectTransform>(_ui,"_objectActions").GetComponentsInChildren<Button>().Single(button=>button.GetComponentInChildren<Text>().text=="定位建筑");locate.onClick.Invoke();yield return LayoutFrame();
  Require(_ui.MapViewing&&_ui.MapInteractionAvailable&&!_ui.IsModal,"standalone Locate retained map input ownership");Require(!Get<RectTransform>(_ui,"_objectDrawer").gameObject.activeSelf,"standalone drawer was not hidden during Locate");Require(!Get<GameObject>(_ui,"_shield").activeSelf,"standalone Locate revealed archive shield");
  var back=Get<Button>(_ui,"_returnComparison");Require(back.gameObject.activeInHierarchy&&back.GetComponentInChildren<Text>().text.Contains("建筑详情"),"standalone Locate lacks a named drawer return");back.onClick.Invoke();yield return LayoutFrame();_renders++;
  Require(!_ui.MapViewing&&!Get<bool>(_ui,"_archiveOpen"),"standalone return created an archive workspace");Require(!Get<GameObject>(_ui,"_shield").activeSelf,"standalone return enabled the archive shield");Require(Flag("ObjectDrawerOpen")&&Get<RectTransform>(_ui,"_objectDrawer").gameObject.activeInHierarchy&&Get<string>(_ui,"_objectContext")==id,"standalone return failed to restore the original drawer");AssertDrawer();
  Call(_ui,"CloseObjectDrawer",true);yield return LayoutFrame();
 }
 static IEnumerator DrawerAndMapChecks(){
  string id=_runtime.Game.playable_ids().Select(v=>v.Text).First(key=>_runtime.Game.s["projects"][key]["status"].Text=="running");string tile=_runtime.Game.s["projects"][id]["tile"].Text;
  foreach(float scale in new[]{.8f,1f,1.2f}){
   Scale(scale,scale==1?1:1.5f);Case("advanced construction object drawer UI "+scale);Call(_ui,"OpenAdvancedWorkspace","construction","");yield return LayoutFrame();
   var body=Get<RectTransform>(_ui,"_body");var children=body.Cast<Transform>().ToArray();var frames=Get<Stack<KeyValuePair<string,string>>>(_ui,"_navigation").ToArray();string page=Get<string>(_ui,"_page"),context=Get<string>(_ui,"_pageContext");
   var workspaceScroll=Get<RectTransform>(_ui,"_archiveScroll").GetComponent<ScrollRect>();float scroll=workspaceScroll.verticalNormalizedPosition;
   Call(_ui,"Navigate","building",id);yield return LayoutFrame();_renders++;
   Require(Flag("ObjectDrawerOpen"),"advanced object inspection did not open drawer");Require(Get<string>(_ui,"_page")==page&&Get<string>(_ui,"_pageContext")==context&&Get<bool>(_ui,"_advancedWorkspace"),"object drawer replaced its source workspace/mode");Require(body.Cast<Transform>().SequenceEqual(children),"opening drawer rebuilt source content");Require(Get<Stack<KeyValuePair<string,string>>>(_ui,"_navigation").SequenceEqual(frames),"drawer modified source navigation stack");AssertDrawer();
   if(scale==1)Capture("03-drawer-over-advanced-editor-fixture.png");if(scale==1.2f)Capture("04-drawer-large-text-editor-fixture.png");
   Call(_ui,"CloseObjectDrawer",true);yield return LayoutFrame();Require(!Flag("ObjectDrawerOpen"),"drawer did not close");Require(Get<string>(_ui,"_page")==page&&Get<bool>(_ui,"_advancedWorkspace")&&body.Cast<Transform>().SequenceEqual(children),"drawer close replaced its advanced workspace");Require(Math.Abs(workspaceScroll.verticalNormalizedPosition-scroll)<.002f,"drawer close lost source scroll");
   Case("advanced locate permits tile interaction and named return UI "+scale);_ui.SelectBuilding(id);Call(_ui,"InspectMapFromPage");yield return LayoutFrame();_renders++;
   Require(_ui.MapViewing&&_ui.MapInteractionAvailable&&!_ui.IsModal,"advanced Locate retains map input lock");Require(!Get<GameObject>(_ui,"_shield").activeSelf,"advanced Locate leaves workspace shield active");Require(Get<RectTransform>(_ui,"_context").gameObject.activeInHierarchy,"advanced Locate hides selected tile actions");
   var returnButton=Get<Button>(_ui,"_returnComparison");Require(returnButton.gameObject.activeInHierarchy&&returnButton.GetComponentInChildren<Text>().text.Contains("施工"),"map return does not name construction source");AssertInside((RectTransform)returnButton.transform,_root,"named map return");AssertInside(Get<RectTransform>(_ui,"_context"),_root,"selected tile panel");
   var another=_runtime.Game.s["cells"].Keys.First(key=>key!=tile);_ui.SelectTile(another);yield return LayoutFrame();Require(_ui.SelectedTile==another,"second tile cannot be selected after advanced Locate");returnButton.onClick.Invoke();yield return LayoutFrame();
   Require(!_ui.MapViewing&&Get<string>(_ui,"_page")==page&&Get<string>(_ui,"_pageContext")==context&&Get<bool>(_ui,"_advancedWorkspace"),"named map return lost advanced source");Require(body.Cast<Transform>().SequenceEqual(children),"map return rebuilt source workspace");
  }
  Scale(1,1);
 }
 static IEnumerator ConstructionChecks(){
  Case("prepare retained real construction tender");Call(_ui,"CloseArchive");var game=_runtime.Game;string tile=game.s["cells"].Keys.First(key=>game.s["cells"][key]["building"].Text==""&&!game.s["cells"][key]["traits"].Contains("tile.water"));
  var before=game.snapshot();Okay(game.create_tender(V.Arr(tile),"homestead"),"prepare isolated tender");_runtime.Session.AfterLocalCommand(before);Call(_runtime,"Publish",null,null,false);Call(_ui,"OnCommitted",_runtime.Snapshot);
  string source=LiveSource(),draft=game.s["draft"].ToJson(false,true);string upgradeId=game.playable_ids().Select(v=>v.Text).First(id=>Development.choices(game,id).Count>0&&game.s["projects"][id]["status"].Text=="running");
  foreach(float scale in new[]{.8f,1f,1.2f})foreach(float font in new[]{1f,1.25f,1.5f}){
   Scale(scale,font);Case("new construction UI "+scale+" font "+font);_ui.OpenConstructionWindow("homestead",tile);yield return LayoutFrame();_renders++;AssertConstruction(false);
   var choices=_root.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Construction Operator ",StringComparison.Ordinal)).ToArray();Require(choices.Length==game.s["draft"]["offers"].Count,"operator candidates differ from retained live offers");
   string first=Get<string>(_ui,"_constructionOffer");choices.First(b=>b.name!="Construction Operator "+first).onClick.Invoke();yield return LayoutFrame();AssertConstruction(false);Require(Get<string>(_ui,"_constructionOffer")!=first,"operator card is not selectable");
   if(scale==1&&font==1)Capture("05-build-editor-fixture.png");if(scale==1.2f&&font==1.5f)Capture("06-build-large-text-editor-fixture.png");ButtonNamed("Construction Window Cancel").onClick.Invoke();yield return LayoutFrame();
   Require(!Flag("ConstructionWindowOpen")&&game.s["draft"].ToJson(false,true)==draft,"cancel changed retained offers");Require(LiveSource()==source,"construction preview/selection/cancel changed live state or RNG");
   Case("upgrade construction UI "+scale+" font "+font);_ui.OpenUpgradeWindow(upgradeId);yield return LayoutFrame();_renders++;AssertConstruction(true);var directions=_root.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Construction Upgrade ",StringComparison.Ordinal)).ToArray();Require(directions.Length==Development.choices(game,upgradeId).Count,"upgrade directions differ from actual rules");
   if(directions.Length>1){string chosen=Get<string>(_ui,"_constructionUpgrade");directions.First(b=>b.name!="Construction Upgrade "+chosen).onClick.Invoke();yield return LayoutFrame();AssertConstruction(true);Require(Get<string>(_ui,"_constructionUpgrade")!=chosen,"upgrade direction card is not selectable");}
   if(scale==1&&font==1)Capture("07-upgrade-editor-fixture.png");ButtonNamed("Construction Window Cancel").onClick.Invoke();yield return LayoutFrame();Require(LiveSource()==source,"upgrade preview/selection/cancel changed live state or RNG");
  }
 }
 static IEnumerator RewardChecks(){
  Case("prepare independent reward operation from actual saved proposal");var game=_runtime.Game;var queue=Bonus.pending(game);
  if(queue.Count==0){
   var before=game.snapshot();Bonus.draw(game,"ui15:reward-layout",V.Arr(),V.Map("bonus_ids",V.Arr("P01","P07","L05")));_runtime.Session.AfterLocalCommand(before);
   Call(_runtime,"Publish",null,null,false);Call(_ui,"OnCommitted",_runtime.Snapshot);queue=Bonus.pending(game);
  }
  Require(queue.Count>0&&queue[0]["choices"].Count>0,"isolated fixture has no real reward candidates");string rid=queue[0]["reward_id"].Text;string source=LiveSource(),wallet=game.s["block"]["bonus_wallet"].ToJson(false,true),rng=game.s["rng"].ToJson(false,true);
  foreach(var settings in new[]{new[]{1f,1f},new[]{1.2f,1.5f}}){
   Scale(settings[0],settings[1]);Case("independent reward modal UI "+settings[0]+" font "+settings[1]);_ui.OpenArchive("tasks_home");yield return LayoutFrame();
   Require(!Get<RectTransform>(_ui,"_body").GetComponentsInChildren<RectTransform>().Any(rect=>rect.name.StartsWith("Reward candidate ",StringComparison.Ordinal)),"candidate cards crowd the task workspace before opening operation");
   Call(_ui,"OpenRewardModal",rid);yield return LayoutFrame();_renders++;var shield=Get<GameObject>(_ui,"_confirm");Require(shield!=null&&shield.activeInHierarchy&&_ui.IsModal,"reward operation lacks shared modal ownership");
   var window=Named(shield.transform,"Reward independent operation");var viewport=Named(window,"Reward scroll");var footer=Named(window,"Reward actions");var body=Named(viewport,"Reward body");var comparison=Named(body,"Reward candidate comparison");
   AssertInside(window,_root,"reward modal window");AssertInside(viewport,window,"reward viewport");AssertInside(footer,window,"reward fixed footer");AssertHorizontal(comparison,viewport,"reward candidate comparison");
   Require(!Relative(viewport,window).Overlaps(Relative(footer,window)),"reward scrolling body overlaps fixed footer");
   var title=Named(window,"Reward modal title").GetComponent<Text>();var sourceLabel=Named(window,"Reward modal source").GetComponent<Text>();
   AssertRewardText(title,window,"reward title");AssertRewardText(sourceLabel,window,"reward source");
   Require(title.text=="选择奖励"&&sourceLabel.text.Contains(queue[0]["choices"].Count+" 选 1"),"modal title/source no longer describes saved reward");
   foreach(var candidate in queue[0]["choices"]){var bid=candidate["bonus_id"].Text;var card=Named(body,"Reward candidate "+bid);AssertHorizontal(card,viewport,"reward candidate "+bid);AssertRewardText(Named(card,"Reward candidate title "+bid).GetComponent<Text>(),card,"candidate name "+bid);AssertRewardText(Named(card,"Reward candidate receipt "+bid).GetComponent<Text>(),card,"candidate receipt "+bid);AssertRewardRules(card,candidate);}
   var cancel=ButtonNamed("Reward modal cancel");var claim=ButtonNamed("Reward modal claim");foreach(var action in new[]{cancel,claim}){AssertInside((RectTransform)action.transform,footer,"reward fixed action");AssertRewardText(action.GetComponentInChildren<Text>(),(RectTransform)action.transform,"reward action label");}
   Require(!claim.interactable,"unselected reward became claimable");AssertHorizontalControls(body,viewport);
   Capture(settings[0]==1?"08-reward-editor-fixture.png":"09-reward-large-text-editor-fixture.png");
   var select=body.GetComponentsInChildren<Button>().First(button=>button.GetComponentInChildren<Text>().text=="预选这项");select.onClick.Invoke();yield return LayoutFrame();_renders++;
   Require(ButtonNamed("Reward modal claim").interactable,"selecting a saved candidate did not enable confirmation");Require(LiveSource()==source,"reward preview or preselection changed source state");
   ButtonNamed("Reward modal cancel").onClick.Invoke();yield return LayoutFrame();Require(Get<GameObject>(_ui,"_confirm")==null&&!Get<bool>(_ui,"_blueprintBusy"),"cancel left reward modal or claim operation active");
   Require(LiveSource()==source&&game.s["block"]["bonus_wallet"].ToJson(false,true)==wallet&&game.s["rng"].ToJson(false,true)==rng,"reward cancel changed saved reward, wallet or RNG");
   Get<Dictionary<string,string>>(_ui,"_rewardSelections").Remove(rid);
  }
  Scale(1,1);
 }
 static IEnumerator SettledWorkspaceCaptures(){
  Scale(1,1);Case("prepare real settled workspace captures in isolated fixture");var game=_runtime.Game;var before=game.snapshot();var theme=Themes.view(game);
  if(theme["id"].Text==""&&theme["choices"].Count>0)Okay(Themes.select(game,theme["choices"][0]["id"].Text),"choose real fixture theme for workspace captures");
  var pending=Bonus.pending(game);if(pending.Count>0&&pending[0]["choices"].Count>0){
   var chosen=pending[0]["choices"].FirstOrDefault(candidate=>game.playable_ids().Any(host=>game.slot_ids(host.Text).Any(slot=>game.bonus_eligible(candidate["bonus_id"].Text,"building",host.Text,slot.Text,game.s["projects"][host]["slots"][slot].Text)=="")))??pending[0]["choices"][0];
   Okay(Bonus.choose(game,pending[0]["reward_id"].Text,chosen["bonus_id"].Text),"claim actual saved fixture reward for Bonus capture");
  }
  Okay(Sim.tick(game,game.c.balance["tick_hours"].Double),"settle real fixture simulation");_runtime.Session.AfterLocalCommand(before);Call(_runtime,"Publish",null,null,false);Call(_ui,"OnCommitted",_runtime.Snapshot);
  Set(_ui,"_v3BonusFilter","available");Set(_ui,"_blueprintQuery","");
  foreach(var page in new[]{new[]{"population_home","10-settled-residents-editor-fixture.png"},new[]{"economy_home","11-settled-economy-editor-fixture.png"},new[]{"construction","12-settled-construction-editor-fixture.png"},new[]{"tasks_home","13-settled-tasks-editor-fixture.png"},new[]{"bonus_home","14-settled-bonus-editor-fixture.png"},new[]{"records","15-settled-archives-editor-fixture.png"}}){
   Case("settled workspace capture "+page[0]);_ui.OpenArchive(page[0]);yield return LayoutFrame();_renders++;if(page[0]=="bonus_home"){var art=Named(Get<RectTransform>(_ui,"_body"),"Inventory drawing");Require(art.rect.height>0,"inventory artwork has no reserved height");AssertInside((RectTransform)art.GetChild(0),art,"inventory artwork");AssertInside(art,(RectTransform)art.parent,"inventory artwork slot");}Capture(page[1]);
  }
 }
 static void AssertRewardRules(RectTransform card,V candidate){
  var effects=candidate.Get("effects",V.Arr());var bid=candidate["bonus_id"].Text;var rules=card.Cast<Transform>().OfType<RectTransform>().Where(rect=>rect.name.StartsWith("Reward rule ",StringComparison.Ordinal)).ToArray();
  Require(rules.Length==effects.Count,"saved candidate rule rows differ from actual effects for "+bid);
  RectTransform prior=null;int index=0;foreach(var effect in effects){
   var row=Named(card,"Reward rule "+bid+" "+index++);var title=Named(row,"Reward rule label").GetComponent<Text>();var value=Named(row,"Reward rule value").GetComponent<Text>();
   string expected=(string)Call(_ui,"FormatEffect",effect,effect.Get("before",0).Double)+" → "+(string)Call(_ui,"FormatEffect",effect,effect.Get("after",0).Double);
   Require(value.text==expected,"reward rule values omit or rewrite saved before/after parameters for "+bid);Require(title.text==(string)Call(_ui,"Stat",effect.Get("label","规则").Text),"reward rule label differs from actual saved effect for "+bid);
   AssertInside(row,card,"reward rule block "+bid);AssertRewardText(title,row,"reward rule label "+bid);AssertRewardText(value,row,"reward rule values "+bid);
   Require(!Relative(title.rectTransform,row).Overlaps(Relative(value.rectTransform,row)),"reward rule label overlaps its value for "+bid);
   Require(value.fontSize>=Mathf.RoundToInt(17*_fontScale),"reward rule values were reduced to fit for "+bid);
   if(prior!=null)Require(!Relative(prior,card).Overlaps(Relative(row,card)),"adjacent reward rules overlap for "+bid);prior=row;
  }
 }
 static void AssertRewardText(Text text,RectTransform container,string role){
  AssertInside(text.rectTransform,container,role);Require(text.preferredHeight<=text.rectTransform.rect.height+2,role+" height clips its actual text: "+text.preferredHeight+" vs "+text.rectTransform.rect.height);
 }
 static void AssertConstruction(bool upgrading){
  Require(Flag("ConstructionWindowOpen"),"construction modal failed to open");var window=Get<RectTransform>(_ui,"_constructionWindow");var viewport=Get<RectTransform>(_ui,"_constructionViewport");var footer=Get<RectTransform>(_ui,"_constructionFooter");var body=Get<RectTransform>(_ui,"_constructionBody");
  AssertInside(window,_root,"construction window");AssertInside(viewport,window,"construction viewport");AssertInside(footer,window,"construction fixed footer");
  var texts=Texts(body);Require(texts.Any(t=>t.text=="此方案的主要改变")&&texts.Any(t=>t.text=="投入与当前约束"),"selected proposal comparison regions missing");Require(texts.Any(t=>t.text=="本区实际变化 · 两边均结算下一步"),"same-step actual scope missing");Require(texts.Any(t=>t.text=="材料现存 / 本次需求")&&texts.Any(t=>t.text=="本区有效施工能力"),"real material/CP constraints missing");
  var cityFold=Named(body,"Construction Disclosure city-impact");Require(!Named(cityFold,"Construction Disclosure Body").gameObject.activeSelf,"city-wide detail no longer defaults folded");
  var start=ButtonNamed("Construction Window Start");var cancel=ButtonNamed("Construction Window Cancel");AssertInside((RectTransform)start.transform,footer,"bounded confirmation");AssertInside((RectTransform)cancel.transform,footer,"cancel");
  Require(((RectTransform)start.transform).rect.width<=footer.rect.width*.30f,"confirmation stretches across footer");Require(!Relative((RectTransform)start.transform,footer).Overlaps(Relative((RectTransform)cancel.transform,footer)),"confirm overlaps cancel");
  foreach(var button in new[]{start,cancel}){var label=button.GetComponentInChildren<Text>();AssertInside(label.rectTransform,(RectTransform)button.transform,"fixed action label");Require(label.preferredWidth<=label.rectTransform.rect.width+2,"fixed action label does not fit on one line");}
  var costs=Named(footer,"Construction Window Costs");AssertInside(costs,footer,"cost/balance reading");
  var game=_runtime.Game;double cash,material,cp;
  if(upgrading){var view=Development.preview(game,Get<string>(_ui,"_constructionId"),Get<string>(_ui,"_constructionUpgrade"));cash=view["cost"].Double;material=view["material"].Double;cp=view["construction_required"].Double;}
  else{var offer=game.s["draft"]["offers"].First(v=>v["id"].Text==Get<string>(_ui,"_constructionOffer"));var cost=Draft.offer_cost(game,offer);cash=cost["currency"].Double;material=cost["material"].Double;cp=cost["cp"].Double;}
  string payment=Texts(costs).Single().text;Require(payment.Contains(Number(cash,0)+" 通货")&&payment.Contains("支付后 "+Number(game.s["currency"].Double-cash,0)),"footer payment/balance differs from real selected quote");
  var materialRow=Named(body,"Construction constraint 材料现存 / 本次需求");Require(Texts(materialRow).Any(t=>t.text==Number(game.inventory_total("material"))+" / "+Number(material)),"material constraint differs from current stock/selected demand");AssertHorizontalControls(body,viewport);
  var comparison=Named(body,"Selected Proposal");AssertHorizontal(comparison,viewport,"major/cost comparison");
  foreach(var candidate in body.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith(upgrading?"Construction Upgrade ":"Construction Operator ",StringComparison.Ordinal))){
   AssertInside((RectTransform)candidate.transform,viewport,"initial candidate card");AssertConstructionText(Named(candidate.transform,"Construction Choice Title").GetComponent<Text>(),"candidate identity",true);
  }
  var names=new[]{"即付通货","施工材料","所需 CP"};var amounts=new[]{cash,material,cp};
  for(int i=0;i<3;i++){var amount=Named(body,"Construction Cost Amount "+names[i]).GetComponent<Text>();Require(amount.text==Number(amounts[i],i==2?1:0),"selected "+names[i]+" number differs from real quote");AssertConstructionText(amount,names[i]+" amount");}
  if(game.s.Get("simulation_tick",0).Long==0){var power=Named(body,"Construction constraint 当前电力 · 供给 / 需求");Require(Texts(power).Any(text=>text.text=="待结算"),"unsettled construction power renders a confirmed zero supply/demand");}
 }
 static void AssertConstructionText(Text text,string role,bool allowWrap=false){
  Require(!string.IsNullOrWhiteSpace(text.text),role+" has no source text");AssertInside(text.rectTransform,(RectTransform)text.transform.parent,role+" text box");
  if(!allowWrap)Require(text.preferredWidth<=text.rectTransform.rect.width+2,role+" '"+text.text+"' does not fit on one line: "+text.preferredWidth+" vs "+text.rectTransform.rect.width);
  Require(text.preferredHeight<=text.rectTransform.rect.height+2,role+" '"+text.text+"' box clips the font's actual line height: "+text.preferredHeight+" vs "+text.rectTransform.rect.height);
  // Scroll masking may cull an offscreen number. Generate with the actual Text
  // settings/rect so a fully truncated first line cannot pass a bounds-only check.
  using(var generated=new TextGenerator()){
   Require(generated.Populate(text.text,text.GetGenerationSettings(text.rectTransform.rect.size)),role+" failed native character generation");
   Require(generated.characterCountVisible>=text.text.Count(character=>!char.IsWhiteSpace(character))&&generated.verts.Any(vertex=>vertex.position.sqrMagnitude>.01f),role+" '"+text.text+"' did not generate its complete visible text ("+generated.characterCountVisible+" characters)");
   Require(generated.lineCount<=(allowWrap?2:1),role+" uses "+generated.lineCount+" lines instead of a readable "+(allowWrap?"one/two-line title":"single-line amount"));
  }
 }
 static void AssertWorkspace(){var workspace=Get<RectTransform>(_ui,"_archive");var viewport=Get<RectTransform>(_ui,"_archiveScroll");AssertInside(workspace,_root,"workspace");AssertInside(viewport,workspace,"workspace viewport");AssertHorizontalControls(Get<RectTransform>(_ui,"_body"),viewport);}
 static void AssertDrawer(){
  var drawer=Get<RectTransform>(_ui,"_objectDrawer");var scroll=Get<ScrollRect>(_ui,"_objectScroll");var actions=Get<RectTransform>(_ui,"_objectActions");Require(drawer.gameObject.activeInHierarchy,"drawer surface hidden");AssertInside(drawer,_root,"drawer");AssertInside(scroll.viewport,drawer,"drawer viewport");AssertInside(actions,drawer,"drawer fixed actions");AssertHorizontalControls(Get<RectTransform>(_ui,"_objectBody"),scroll.viewport);foreach(var button in actions.GetComponentsInChildren<Selectable>())AssertInside((RectTransform)button.transform,actions,"drawer fixed action");
 }
 static Rect Relative(RectTransform rect,RectTransform container){var corners=new Vector3[4];rect.GetWorldCorners(corners);var points=corners.Select(container.InverseTransformPoint).ToArray();return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));}
 static void AssertInside(RectTransform rect,RectTransform container,string role,float tolerance=2){var a=Relative(rect,container);var b=container.rect;Require(Positive(a.width)&&Positive(a.height)&&a.xMin>=b.xMin-tolerance&&a.xMax<=b.xMax+tolerance&&a.yMin>=b.yMin-tolerance&&a.yMax<=b.yMax+tolerance,role+" outside "+container.name+": "+a+" vs "+b);}
 static void AssertHorizontal(RectTransform rect,RectTransform container,string role,float tolerance=2){var a=Relative(rect,container);var b=container.rect;Require(Positive(a.width)&&Positive(a.height)&&a.xMin>=b.xMin-tolerance&&a.xMax<=b.xMax+tolerance,role+" horizontally exceeds "+container.name+": "+a+" vs "+b);}
 static void AssertHorizontalControls(Transform body,RectTransform viewport){foreach(var control in body.GetComponentsInChildren<Selectable>())if(control.gameObject.activeInHierarchy)AssertHorizontal((RectTransform)control.transform,viewport,"scrollable control "+control.name);}
 static bool Positive(float value)=>value>0&&!float.IsNaN(value)&&!float.IsInfinity(value);
 static void Capture(string name){
  if(_renderTexture==null)return;var previous=RenderTexture.active;Texture2D image=null;
  try {
   RenderTexture.active=_renderTexture;image=new Texture2D(2560,1440,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,2560,1440),0,0);image.Apply();var pixels=image.GetPixels32();var first=pixels[0];int differences=0;for(int i=1024;i<pixels.Length;i+=1024)if(Math.Abs(pixels[i].r-first.r)+Math.Abs(pixels[i].g-first.g)+Math.Abs(pixels[i].b-first.b)>12)differences++;
   Require(differences>50,"fixture capture was blank/unrendered");Directory.CreateDirectory(_output);File.WriteAllBytes(Path.Combine(_output,name),image.EncodeToPNG());Captures.Add(name);
  }finally{RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
 }
}
}
