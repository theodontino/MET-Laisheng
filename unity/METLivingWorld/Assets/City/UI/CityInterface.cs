using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace MET.City {
/// <summary>Player interface: ambient HUD, committed alerts, one contextual action, and a full management archive.</summary>
[DefaultExecutionOrder(-100)]
public sealed partial class CityInterface : MonoBehaviour {
 public CityRuntime Runtime {get;private set;}
 public string SelectedTile {get;private set;}="";
 public string SelectedBuilding {get;private set;}="";
 public string Layer {get;private set;}="terrain";
 public string MapSourcePhase {get;private set;}="committed";
 public V MapSnapshot=>(MapSourcePhase=="committed"?_state:_mapSnapshot).Duplicate(true);
 public bool IsModal=>(_archiveOpen&&!_mapViewing)||_confirm!=null;
 public bool MapViewing=>_mapViewing;
 public bool KeyboardOwned=>IsModal||(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.GetComponentInParent<InputField>()!=null);
 public event Action<string,string> SelectionChanged;
 public event Action<string> LayerChanged;
 public event Action<V> MapProjectionChanged;
 public event Action<V> DisplayChanged;
 readonly Dictionary<Text,int> _fontBases=new Dictionary<Text,int>();AudioSource _audio;bool _seenTheme,_completedTheme;string _cityIdentity="";
 readonly Planning _planner=new Planning();readonly Feedback _feedback=new Feedback();
 readonly Stack<KeyValuePair<string,string>> _navigation=new Stack<KeyValuePair<string,string>>();
 readonly Color Ink=new Color(.88f,.91f,.84f),Muted=new Color(.56f,.65f,.62f),Gold=new Color(.88f,.69f,.34f),Green=new Color(.46f,.74f,.56f),Blue=new Color(.42f,.68f,.78f),Red=new Color(.87f,.4f,.34f),Panel=new Color(.075f,.12f,.12f,.97f),Raised=new Color(.13f,.2f,.19f,.98f);
 Canvas _canvas;CanvasScaler _scaler;GraphicRaycaster _raycaster;Font _font;
 PointerEventData _pointerData;readonly List<RaycastResult> _pointerHits=new List<RaycastResult>();
 int _pointerFrame=-1;Vector2 _pointerPosition;bool _pointerResult;
 RectTransform _root,_context,_status,_speech,_archive,_body,_pageNav,_footer;GameObject _shield,_confirm;
 bool _statusDirty,_contextDirty;bool PointerBusy=>Input.GetMouseButton(0)||Input.GetMouseButtonUp(0);
 Button _returnComparison;bool _mapViewing,_comparisonValid;int _comparisonRevision;long _comparisonTick;string _layerBeforeComparison="terrain";
 Text _dt,_population,_food,_time,_legend,_toast,_title,_subtitle;Button _pause;bool _archiveOpen,_refreshing;string _page="overview",_pageContext="",_base="homestead",_offer="",_previewVersion="";V _mapSnapshot=V.Map(),_baseline=V.Map(),_proposal=V.Map(),_feedbackView=V.Map();float _toastUntil,_nextFeedback;
 Game _viewGame;V _state=V.Map();
 Game G=>_viewGame;V S=>_state;
 float TextScale=>Mathf.Clamp((float)Runtime.DisplaySettings.Get("text_scale",1).Double,1,1.5f);
 static string F(double n,int d=1)=>n.ToString("F"+d,CultureInfo.InvariantCulture);
 static string Signed(double n,int d=1)=>n.ToString("+0."+new string('0',d)+";-0."+new string('0',d)+";0."+new string('0',d),CultureInfo.InvariantCulture);
 string Resource(string id)=>Catalog.RESOURCE_NAMES.Get(id,id).Text;
 string Service(string id)=>Catalog.SERVICE_NAMES.Get(id,id).Text;
 string Stat(string id)=>V.Map("housing","住房","jobs","岗位","efficiency","建筑效率","personnel_mult","人员效率","energy_demand","用电需求","pollution","污染","maintenance","维护","construction_capacity","施工能力","trade_capacity","货运能力","output.food","粮食产出","output.material","材料产出","output.goods","制成品产出","output.energy","供电能力","input.food","粮食投入","input.material","材料投入","input.goods","制成品投入","storage.food","粮食仓容","storage.material","材料仓容","storage.goods","制成品仓容","service.medical","医疗容量","service.education","教育容量","service.commercial","商业容量","service.entertainment","娱乐容量").Get(id,id).Text;
 Color Domain(V p){var domain=G.c.projects.Get(p.Get("def",""),V.Map()).Get("domain","public").Text;return domain=="industrial"?Gold:domain=="residential"?Green:domain=="commercial"?Blue:new Color(.67f,.61f,.83f);}
 void Awake(){Runtime=GetComponent<CityRuntime>()??FindAnyObjectByType<CityRuntime>();Runtime.LoadDisplaySettings();UpdateDetached(Runtime.Snapshot);_font=Resources.Load<Font>("CityFonts/NotoSansSC");if(_font==null)_font=Resources.Load<Font>("CityFonts/NotoSansSC-VF");if(_font==null)_font=Font.CreateDynamicFontFromOSFont("PingFang SC",18);_audio=gameObject.AddComponent<AudioSource>();_audio.playOnAwake=false;BuildShell();Runtime.Committed+=OnCommitted;Runtime.Message+=ShowMessage;}
 void Start(){OnCommitted(Runtime.Snapshot);if(Runtime.StartupMessage!="")ShowMessage(Runtime.StartupMessage);}
 void OnDestroy(){if(Runtime!=null){Runtime.Committed-=OnCommitted;Runtime.Message-=ShowMessage;}}
 void Update(){if(_toast!=null)_toast.gameObject.SetActive(UnityEngine.Time.unscaledTime<_toastUntil);if(UnityEngine.Time.unscaledTime>=_nextFeedback){_nextFeedback=UnityEngine.Time.unscaledTime+.3f;if(Runtime.Ready&&S["phase"].Text=="play"){_feedbackView=_feedback.view(UnityEngine.Time.realtimeSinceStartup);RenderStatus();}}
  if(!PointerBusy){if(_statusDirty)RenderStatus();if(_contextDirty)RefreshContext();}
  if(Input.GetKeyDown(KeyCode.Escape)){if(_confirm!=null)DismissConfirm();else if(_archiveOpen)Back();else if(SelectedTile!=""){SelectedTile="";SelectedBuilding="";RefreshContext();SelectionChanged?.Invoke("","");}else OpenArchive("overview");}
  if(!KeyboardOwned&&Input.GetKeyDown(KeyCode.P))Runtime.SetPaused(!S["settings"].Get("user_paused",true).Bool);
 }
 public bool PointerOwned(Vector2 position){
  if(IsModal)return true;
  if(_pointerFrame==UnityEngine.Time.frameCount&&_pointerPosition==position)return _pointerResult;
  _pointerData=_pointerData??new PointerEventData(EventSystem.current);
  _pointerData.position=position;_pointerHits.Clear();_raycaster.Raycast(_pointerData,_pointerHits);
  _pointerFrame=UnityEngine.Time.frameCount;_pointerPosition=position;_pointerResult=_pointerHits.Count>0;return _pointerResult;
 }
 RectTransform Node(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
 RectTransform Surface(string name,Transform parent,Color color){var node=Node(name,parent);node.gameObject.AddComponent<Image>().color=color;return node;}
 void Pin(RectTransform r,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size){r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size;}
 void Stretch(RectTransform r,float left=0,float top=0,float right=0,float bottom=0){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);}
 RectTransform Column(Transform parent,string name="Column",float gap=12,bool fit=true){var r=Node(name,parent);var l=r.gameObject.AddComponent<VerticalLayoutGroup>();l.spacing=gap;l.padding=new RectOffset(16,16,14,14);l.childControlWidth=true;l.childControlHeight=true;l.childForceExpandWidth=true;l.childForceExpandHeight=false;if(fit){var f=r.gameObject.AddComponent<ContentSizeFitter>();f.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}return r;}
 RectTransform Row(Transform parent,string name="Row",float height=44){var r=Node(name,parent);var l=r.gameObject.AddComponent<HorizontalLayoutGroup>();l.spacing=12;l.childControlWidth=true;l.childControlHeight=true;l.childForceExpandWidth=true;l.childForceExpandHeight=false;var minimum=r.gameObject.AddComponent<LayoutElement>();minimum.minHeight=height;return r;}
 void Height(RectTransform r,float h){var e=r.GetComponent<LayoutElement>()??r.gameObject.AddComponent<LayoutElement>();e.minHeight=h;e.preferredHeight=h;}
 void Width(RectTransform r,float w){var e=r.GetComponent<LayoutElement>()??r.gameObject.AddComponent<LayoutElement>();e.minWidth=w;e.preferredWidth=w;e.flexibleWidth=0;}
 Text Label(Transform parent,string value,int size=18,Color? color=null,float h=0){var r=Node("Text",parent);var t=r.gameObject.AddComponent<Text>();t.font=_font;_fontBases[t]=size;t.text=value;t.fontSize=Mathf.RoundToInt(size*TextScale);t.color=color??Ink;t.raycastTarget=false;if(Runtime.DisplaySettings.Get("high_contrast",false).Bool){var outline=r.gameObject.AddComponent<Outline>();outline.effectColor=Color.black;outline.effectDistance=new Vector2(1,-1);}t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;Height(r,h>0?h:Math.Max(28,(size+10)*TextScale));return t;}
 Button Button(Transform parent,string text,Action click,bool primary=false,bool disabled=false){var r=Surface("Button "+text,parent,primary?new Color(.25f,.4f,.32f):Raised);Height(r,48*TextScale);var b=r.gameObject.AddComponent<Button>();var colors=b.colors;colors.highlightedColor=new Color(1.17f,1.17f,1.13f);colors.pressedColor=new Color(.74f,.85f,.78f);colors.disabledColor=new Color(.5f,.5f,.5f,.65f);b.colors=colors;b.interactable=!disabled;var t=Label(r,text,16,primary?new Color(.98f,.92f,.73f):Ink);Stretch(t.rectTransform,12,4,12,4);t.alignment=TextAnchor.MiddleCenter;b.onClick.AddListener(()=>{PlayCue("select");click();});return b;}
 InputField InputBox(Transform parent,string value,string placeholder,float width=0){var r=Surface("Input "+placeholder,parent,new Color(.04f,.08f,.09f));Height(r,48*TextScale);if(width>0)Width(r,width);var input=r.gameObject.AddComponent<InputField>();var t=Label(r,"",17);Stretch(t.rectTransform,12,7,12,7);input.textComponent=t;var p=Label(r,placeholder,16,Muted);Stretch(p.rectTransform,12,7,12,7);input.placeholder=p;input.text=value;input.characterLimit=512;return input;}
 RectTransform Card(Transform parent,string heading,string note="",Color? color=null){var outer=Surface("Card "+heading,parent,Raised);var body=Column(outer);var fitter=outer.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;var layout=outer.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;Label(body,heading,22,color??Ink);if(note!="")Label(body,note,15,Muted,Math.Max(28,Mathf.Ceil(note.Length/72f)*25*TextScale));return body;}
 void Metric(Transform parent,string label,string value,string note="",Color? color=null){var row=Row(parent,"Metric",42*TextScale);Label(row,label,17,Muted);var t=Label(row,value,23,color??Ink);t.alignment=TextAnchor.MiddleRight;if(note!="")Label(parent,note,14,Muted,Math.Max(25,Mathf.Ceil(note.Length/80f)*23*TextScale));}
 void Meter(Transform parent,string label,double value,double capacity,string suffix="",Color? color=null){Metric(parent,label,F(value)+" / "+F(capacity)+suffix,"",color);var node=Node("Meter "+label,parent);Height(node,9);var g=node.gameObject.AddComponent<CityGraphic>();g.Kind="bar";g.Ratio=(float)(value/Math.Max(.000001,capacity));g.color=color??Green;g.raycastTarget=false;}
 void Mark(Transform parent,string kind,Color color,float size=98){var r=Node("Site mark",parent);Width(r,size);Height(r,size);var g=r.gameObject.AddComponent<CityGraphic>();g.Kind=kind;g.color=color;g.raycastTarget=false;}
 void Fold(Transform parent,string name,Action<RectTransform> render){
  var box=Column(parent,"Disclosure",6);var content=Column(box,"DisclosureBody",7);content.gameObject.SetActive(false);bool rendered=false;
  var b=Button(box,name+" ›",()=>{if(!rendered){render(content);rendered=true;}content.gameObject.SetActive(!content.gameObject.activeSelf);RefreshModalOwnership();});
  b.transform.SetAsFirstSibling();
 }
 void Raw(Transform parent,V value){var text=value.ToJson(true,true);foreach(var block in ChunkText(text,4000))Label(parent,block,13,Muted,Math.Max(40,block.Split('\n').Length*21*TextScale));}
 IEnumerable<string> ChunkText(string text,int limit){for(var offset=0;offset<text.Length;offset+=limit)yield return text.Substring(offset,Math.Min(limit,text.Length-offset));}
 void ForgetFonts(Transform parent){foreach(var text in parent.GetComponentsInChildren<Text>(true))_fontBases.Remove(text);}
 void Clear(Transform parent){for(var i=parent.childCount-1;i>=0;i--){var child=parent.GetChild(i);foreach(var text in child.GetComponentsInChildren<Text>(true))_fontBases.Remove(text);child.gameObject.SetActive(false);child.SetParent(null);Destroy(child.gameObject);}}
 void BuildShell(){if(EventSystem.current==null){var es=new GameObject("City Event System",typeof(EventSystem),typeof(StandaloneInputModule));}
  var canvasObject=new GameObject("City Command Interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasObject.transform.SetParent(transform,false);_canvas=canvasObject.GetComponent<Canvas>();_canvas.renderMode=RenderMode.ScreenSpaceOverlay;_canvas.sortingOrder=100;_scaler=canvasObject.GetComponent<CanvasScaler>();_scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;_scaler.matchWidthOrHeight=.5f;_root=(RectTransform)canvasObject.transform;_raycaster=canvasObject.GetComponent<GraphicRaycaster>();ApplyDisplay();
  var hud=Surface("L0 City Instruments",_root,Panel);Pin(hud,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-20),new Vector2(1120,74));var row=Row(hud,"HUD",74);Stretch(row,16,8,16,8);_dt=Label(row,"DT",25,Gold);_population=Label(row,"人口",23);_food=Label(row,"粮食",23);_time=Label(row,"时间",17);Width(_time.rectTransform,190);_pause=Button(row,"继续",()=>{if(_mapViewing){ShowMessage("请先返回比较页面并完成当前方案选择。");return;}if(S["settings"]["user_paused"].Bool&&Runtime.MandatoryPending){OpenArchive("settlement");return;}Runtime.SetPaused(!S["settings"]["user_paused"].Bool);});Width((RectTransform)_pause.transform,88);
  foreach(var mode in G.c.beta["time"]["modes"]){var id=mode["id"].Text;var b=Button(row,mode.Get("label",id).Text,()=>{if(_mapViewing){ShowMessage("请先返回比较页面并完成当前方案选择。");return;}Runtime.SetMode(id);if(Runtime.MandatoryPending)OpenArchive("settlement");});Width((RectTransform)b.transform,76);}
  var menu=Button(_root,"城市档案  ≡",()=>OpenArchive());Pin((RectTransform)menu.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-22),new Vector2(180,60));
  _status=Column(_root,"L1 City Status",9);Pin(_status,new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-115),new Vector2(330,0));_speech=Column(_root,"City Voices",7);Pin(_speech,new Vector2(0,0),new Vector2(0,0),new Vector2(24,140),new Vector2(340,0));
  _context=Surface("L2 Site Context",_root,Panel);Pin(_context,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,98),new Vector2(388,0));var cf=_context.gameObject.AddComponent<ContentSizeFitter>();cf.verticalFit=ContentSizeFitter.FitMode.PreferredSize;var cl=_context.gameObject.AddComponent<VerticalLayoutGroup>();cl.childControlWidth=cl.childControlHeight=true;cl.childForceExpandHeight=false;_context.gameObject.SetActive(false);
  var layers=Surface("Map Layers",_root,Panel);Pin(layers,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,18),new Vector2(1000,60));var lr=Row(layers,"Layers",60);Stretch(lr,12,7,12,7);foreach(var pair in new[]{new[]{"terrain","地貌"},new[]{"service","服务"},new[]{"employment","就业"},new[]{"pollution","环境"},new[]{"heat","余热"},new[]{"stock","库存"}}){var id=pair[0];Button(lr,pair[1],()=>SetLayer(id));}var legendBox=Surface("Source-aware map legend",_root,Panel);Pin(legendBox,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,82),new Vector2(820,36));_legend=Label(legendBox,"地貌 · 最近周期实际   L / Shift+L 切换",15,Ink);Stretch(_legend.rectTransform,12,4,12,4);_legend.alignment=TextAnchor.MiddleCenter;
  _toast=Label(_root,"",18,Ink);var toastBg=_toast.gameObject.AddComponent<Shadow>();toastBg.effectColor=Color.black;Pin(_toast.rectTransform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,112),new Vector2(1000,60));_toast.alignment=TextAnchor.MiddleCenter;
  BuildArchive();_returnComparison=Button(_root,"返回方案比较 ›",ReturnFromMapView,true);Pin((RectTransform)_returnComparison.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-94),new Vector2(260,52));_returnComparison.gameObject.SetActive(false);_toast.transform.SetAsLastSibling();
 }
 void BuildArchive(){_shield=Surface("L3 Archive Shield",_root,new Color(.015f,.03f,.03f,.82f)).gameObject;Stretch((RectTransform)_shield.transform);_archive=Surface("L3 City Archive",_shield.transform,Panel);Pin(_archive,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1520,890));
  _title=Label(_archive,"城市档案",30,Gold);Pin(_title.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(30,-22),new Vector2(1300,48));_subtitle=Label(_archive,"河谷现场 / 城市档案",15,Muted);Pin(_subtitle.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(30,-78),new Vector2(1350,40));var close=Button(_archive,"×",()=>CloseArchive());Pin((RectTransform)close.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-22,-22),new Vector2(52,48));
  _pageNav=Row(_archive,"Archive Navigation",46);Pin(_pageNav,new Vector2(0,1),new Vector2(0,1),new Vector2(30,-120),new Vector2(1460,46));var pages=new[]{new[]{"overview","总览"},new[]{"resources","资源"},new[]{"population","人口"},new[]{"market","市场"},new[]{"policies","政策"},new[]{"bonuses","构筑"},new[]{"themes","主题"},new[]{"settlement","年度 / 尾声"},new[]{"records","记录"},new[]{"settings","设置"}};foreach(var p in pages){var id=p[0];Button(_pageNav,p[1],()=>Navigate(id));}
  var scroll=Node("Archive Scroll",_archive);Stretch(scroll,28,180,28,82);var image=scroll.gameObject.AddComponent<Image>();image.color=new Color(.05f,.09f,.09f,.45f);scroll.gameObject.AddComponent<RectMask2D>();var sr=scroll.gameObject.AddComponent<ScrollRect>();sr.horizontal=false;sr.movementType=ScrollRect.MovementType.Clamped;sr.viewport=scroll;_body=Column(scroll,"Archive Body",14);_body.anchorMin=new Vector2(0,1);_body.anchorMax=new Vector2(1,1);_body.pivot=new Vector2(.5f,1);_body.sizeDelta=new Vector2(-12,0);sr.content=_body;
  _footer=Row(_archive,"Archive Footer",48);Pin(_footer,new Vector2(0,0),new Vector2(0,0),new Vector2(30,20),new Vector2(1460,48));Button(_footer,"‹ 返回",Back);Button(_footer,"返回地图 · 保留时间状态",CloseArchive);_shield.SetActive(false);
 }
 public void ApplyDisplay(){var factor=Mathf.Clamp((float)Runtime.DisplaySettings.Get("ui_scale",1).Double,.8f,1.2f);if(_scaler!=null)_scaler.referenceResolution=new Vector2(1920/factor,1080/factor);Application.targetFrameRate=Runtime.EffectiveFrameLimit;if(_root!=null){var contrast=Runtime.DisplaySettings.Get("high_contrast",false).Bool;foreach(var text in _root.GetComponentsInChildren<Text>(true)){var outline=text.GetComponent<Outline>();if(contrast&&outline==null)outline=text.gameObject.AddComponent<Outline>();if(outline!=null){outline.enabled=contrast;outline.effectColor=Color.black;outline.effectDistance=new Vector2(1,-1);}}}DisplayChanged?.Invoke(Runtime.DisplaySettings.Duplicate(true));}
 public void ShowMessage(string text){if(_toast==null||text=="")return;_toast.transform.SetAsLastSibling();_toast.text=text;_toastUntil=UnityEngine.Time.unscaledTime+8;_toast.gameObject.SetActive(true);}
 public void SelectTile(string tile){if(_mapViewing||IsModal||!S["cells"].Has(tile))return;SelectedTile=tile;SelectedBuilding=S["cells"][tile].Get("building","").Text;RefreshContext();SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);}
 public void SelectBuilding(string id){if(_mapViewing||!S["projects"].Has(id))return;SelectedBuilding=id;SelectedTile=S["projects"][id].Get("tile","").Text;RefreshContext();SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);}
 public void SetLayer(string layer){if(layer==Layer)return;Layer=layer;_legend.text=LayerLabel(layer)+" · "+(MapSourcePhase=="committed"?"最近周期实际":MapSourcePhase=="baseline"?"同一步现状预测":"同一步方案建成后预测")+"   L / Shift+L";LayerChanged?.Invoke(layer);}
 string LayerLabel(string layer)=>V.Map("terrain","地貌","service","服务人数与来源","employment","真实就业关系","pollution","环境场强 · 全城地块口径","heat","余热来源","stock","本地库存","delta","本次污染场强改变 · 正值负担增加 / 负值减轻","energy","全城电力能力").Get(layer,Resource(layer)).Text;
 void UpdateDetached(V snapshot){_state=snapshot.Duplicate(true);_viewGame=_viewGame??new Game(Runtime.Game.c,Runtime.StorageRoot);_viewGame.s=_state;_viewGame.compiled=Runtime.Game.compiled.Duplicate(true);_viewGame.preview_mode=true;_viewGame.revision=Runtime.Game.revision;_viewGame.last_committed_revision=Runtime.Game.last_committed_revision;}
 void OnCommitted(V snapshot){var identity=snapshot.Get("seed",0).Text+":"+snapshot.Get("started_at",0).Text;var cityChanged=_cityIdentity!=""&&_cityIdentity!=identity;if(_cityIdentity!=""&&_cityIdentity!=identity){SelectedTile="";SelectedBuilding="";_feedback.reset();_contextPreview=V.Map();_contextPreviewVersion="";_statusKey="";_contextKey="";SelectionChanged?.Invoke("","");_seenTheme=false;}_cityIdentity=identity;UpdateDetached(snapshot);if(_comparisonValid&&(cityChanged||_comparisonRevision!=Runtime.Game.revision||_comparisonTick!=S["simulation_tick"].Long)){if(_mapViewing)ReturnFromMapView();ClearComparison();}var complete=S["block"].Get("theme",V.Map()).Get("completed",false).Bool;if(_seenTheme&&complete&&!_completedTheme)PlayCue("complete");_completedTheme=complete;_seenTheme=true;if(_refreshing)return;_refreshing=true;try{UpdateHud();UpdateStatusModel();RenderStatus();if(MapSourcePhase=="committed")MapProjectionChanged?.Invoke(snapshot.Duplicate(true));RefreshContext();if(S["phase"].Text=="select_block"||S["phase"].Text=="origin"){if(!_archiveOpen||_page!="start")OpenArchive("start");else RenderPage();}else if(!_archiveOpen&&Runtime.MandatoryPending)OpenArchive("settlement");}finally{_refreshing=false;}}
 void UpdateHud(){var t=Clock.summary(G);_dt.text="DT  "+F(t["dt"].Double,2);_population.text="人口  "+F(S["metrics"].Get("population",0).Double,0);_food.text="粮食  "+F(G.inventory_total("food"),0);_time.text=t["date"].Text+"\n"+(t["paused"].Bool?"暂停":F(t["acceleration"].Double,0)+"× 常速");_pause.GetComponentInChildren<Text>().text=t["paused"].Bool?"继续":"暂停";}
 public void OpenArchive(string page="overview"){if(_mapViewing)ReturnFromMapView();if(_page!=page&&_comparisonValid)ClearComparison();if(!_archiveOpen){_archiveOpen=true;Runtime.SetDecisionOpen(true);}_shield.SetActive(true);_navigation.Clear();_page=page;_pageContext="";RenderPage();}
 void Navigate(string page,string context=""){if(_page!=page&&_comparisonValid)ClearComparison();if(!_archiveOpen){OpenArchive(page);_pageContext=context;RenderPage();return;}_navigation.Push(new KeyValuePair<string,string>(_page,_pageContext));_page=page;_pageContext=context;RenderPage();}
 void Back(){if(_mapViewing){ReturnFromMapView();return;}if(_navigation.Count!=0){if(_comparisonValid)ClearComparison();var back=_navigation.Pop();_page=back.Key;_pageContext=back.Value;RenderPage();}else CloseArchive();}
 void CloseArchive(){if(S["phase"].Text!="play"){ShowMessage("请先选择登陆点与起源。");return;}_mapViewing=false;_returnComparison.gameObject.SetActive(false);_archiveOpen=false;_shield.SetActive(false);_navigation.Clear();Clear(_body);_selectedRecord=V.Map();ClearComparison();Runtime.SetDecisionOpen(false);RefreshContext();RefreshModalOwnership(true);}
 void PublishComparison(){if(_baseline.Count==0||ComparisonProposal.Count==0){ClearComparison();return;}if(!_comparisonValid)_layerBeforeComparison=Layer=="delta"?"terrain":Layer;_comparisonValid=true;_comparisonRevision=Runtime.Game.revision;_comparisonTick=S["simulation_tick"].Long;ComparisonChanged?.Invoke(ComparisonBaseline,ComparisonProposal);}
 void ClearComparison(){_planner.cache.Clear();_planner.lru.Clear();_planner.token="";_comparisonValid=false;_baseline=V.Map();_proposal=V.Map();ComparisonChanged?.Invoke(V.Map(),V.Map());if(Layer=="delta")SetLayer(_layerBeforeComparison);SetMapProjection("committed",null);}
 void ShowForecastMap(string phase,V snapshot){SetMapProjection(phase,snapshot);_mapViewing=true;_shield.SetActive(false);_returnComparison.gameObject.SetActive(true);_context.gameObject.SetActive(false);RefreshModalOwnership(true);}
 void ReturnFromMapView(){_mapViewing=false;_returnComparison.gameObject.SetActive(false);_shield.SetActive(true);RefreshContext();RefreshModalOwnership(true);}
 void SetMapProjection(string source,V data){MapSourcePhase=source;_mapSnapshot=data??V.Map();_legend.text=LayerLabel(Layer)+" · "+(source=="committed"?"最近周期实际":source=="baseline"?"同一步现状预测":"同一步方案建成后预测");MapProjectionChanged?.Invoke(MapSnapshot);}
 void Command(Func<Game,string> command,bool refresh=true){var error=Runtime.Execute(command);PlayCue(error==""?"confirm":"notice");if(error==""&&refresh){if(_archiveOpen)RenderPage();RefreshContext();}}
 public void PlayCue(string id){if(_audio==null||!S.Get("settings",V.Map()).Get("sound",true).Bool)return;var clip=Resources.Load<AudioClip>("CityAudio/"+id);if(clip!=null)_audio.PlayOneShot(clip,.3f);}
 void Confirm(string title,string description,Action action){if(_confirm!=null)DismissConfirm();Runtime.Flush();_confirm=Surface("Confirmation Shield",_root,new Color(0,0,0,.82f)).gameObject;Stretch((RectTransform)_confirm.transform);var card=Surface("Confirm",_confirm.transform,Panel);Pin(card,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(680,330));var body=Column(card);Stretch(body);Label(body,title,26,Gold);Label(body,description,18,Muted,150);var row=Row(body);Button(row,"取消",DismissConfirm);Button(row,"确认",()=>{DismissConfirm();action();},true);RefreshModalOwnership(true);}
 void DismissConfirm(){if(_confirm==null)return;ForgetFonts(_confirm.transform);_confirm.SetActive(false);Destroy(_confirm);_confirm=null;RefreshModalOwnership(true);}
 void RefreshModalOwnership(bool focus=false){
  _pointerFrame=-1;_pointerHits.Clear();
  // CanvasGroup ownership excludes background controls from uGUI automatic navigation,
  // including controls added later when committed alerts/context are refreshed.
  foreach(Transform child in _root){
   var group=child.GetComponent<CanvasGroup>()??child.gameObject.AddComponent<CanvasGroup>();
   if(child.gameObject==_confirm)group.interactable=true;
   else if(child.gameObject==_shield)group.interactable=_archiveOpen&&!_mapViewing&&_confirm==null;
   else group.interactable=!IsModal;
  }
  if(!focus||EventSystem.current==null)return;
  EventSystem.current.SetSelectedGameObject(null);
  Transform scope=_confirm!=null?_confirm.transform:_archiveOpen&&!_mapViewing?_archive:_mapViewing?_returnComparison.transform:_root;
  Selectable selected=null;
  if(scope==_archive)selected=_body.GetComponentsInChildren<Selectable>(false).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.IsInteractable());
  selected=selected??scope.GetComponentsInChildren<Selectable>(false).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.IsInteractable());
  if(selected!=null)EventSystem.current.SetSelectedGameObject(selected.gameObject);
 }
 void RenderPage(){Clear(_body);_body.anchoredPosition=Vector2.zero;_pageNav.gameObject.SetActive(_page!="start");_title.text=PageTitle(_page);_subtitle.text=(S["block"].Get("display_name","河谷登陆点").Text)+" / "+PageTitle(_page)+"    本地时间已暂停 · 数据与来源可展开";
  switch(_page){case "start":StartPage();break;case "overview":OverviewPage();break;case "resources":ResourcesPage();break;case "population":PopulationPage();break;case "market":MarketPage();break;case "policies":PoliciesPage();break;case "bonuses":BonusesPage();break;case "themes":ThemesPage();break;case "settlement":SettlementPage();break;case "records":RecordsPage();break;case "settings":SettingsPage();break;case "building":BuildingPage();break;case "upgrades":UpgradePage();break;case "slot":SlotPage();break;case "tender":TenderPage();break;case "sources":SourcesPage();break;case "record":RecordPage();break;default:OverviewPage();break;}
  RefreshModalOwnership(true);
 }
 string PageTitle(string page)=>V.Map("start","河谷登陆 / 起源","overview","城市档案","resources","资源与财政","population","居民与服务","market","外部市场","policies","地区政策","bonuses","方案副本与蓝图","themes","委任主题","settlement","年度 / 大周期 / 城市尾声","records","城市声音与记录","settings","菜单与显示设置","building","建筑档案","upgrades","成长与升级","slot","方案槽与安装","tender","运营者投标比较","sources","完整规则与来源","record","只读城市记录").Get(page,page).Text;
}
}
