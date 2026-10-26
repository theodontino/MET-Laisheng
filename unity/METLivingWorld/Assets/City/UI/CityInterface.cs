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
 public bool IsModal=>ToolkitUpgradeOpen||ConstructionWindowOpen||(_archiveOpen&&!_mapViewing&&!CompactWorkspaceOpen)||_confirm!=null;
 public bool MapViewing=>_mapViewing;
 bool _constructionIntent;
 public bool ConstructionIntentActive=>_constructionIntent||_planningBuildOpen;
 void BeginConstructionIntent(){_constructionIntent=true;CancelDeskTipsForPanel();DismissMapInspection();}
 void EndConstructionIntent(){_constructionIntent=false;_planningBuildOpen=false;if(_planningBuildMenu!=null)_planningBuildMenu.gameObject.SetActive(true);if(_planningBuildToggle!=null)_planningBuildToggle.text="建设";RefreshHudGeometry();}
 public bool KeyboardOwned=>IsModal||DeskTipOpen||(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.GetComponentInParent<InputField>()!=null);
 public event Action<string,string> SelectionChanged;
 public event Action<string> LayerChanged;
 public event Action<V> MapProjectionChanged;
 public event Action<V> DisplayChanged;
 readonly Dictionary<Text,int> _fontBases=new Dictionary<Text,int>();AudioSource _audio;bool _seenTheme,_completedTheme;string _cityIdentity="";
 readonly Planning _planner=new Planning();readonly Feedback _feedback=new Feedback();
 readonly Stack<KeyValuePair<string,string>> _navigation=new Stack<KeyValuePair<string,string>>();
 // Dark strategy HUD; shared thin borders keep the city in view.
 readonly Color Ink=new Color(.89f,.92f,.90f),Muted=new Color(.62f,.73f,.68f),Gold=new Color(.87f,.74f,.48f),Green=new Color(.52f,.76f,.65f),Blue=new Color(.60f,.76f,.70f),Red=new Color(.88f,.54f,.45f),Panel=new Color(.078f,.141f,.153f,.99f),Raised=new Color(.11f,.20f,.19f,.99f);
 readonly Dictionary<string,Button> _archiveTabs=new Dictionary<string,Button>();
 GameObject _archiveEntryFocus;
 Canvas _canvas;CanvasScaler _scaler;GraphicRaycaster _raycaster;Font _font;
 PointerEventData _pointerData;readonly List<RaycastResult> _pointerHits=new List<RaycastResult>();
 int _pointerFrame=-1;Vector2 _pointerPosition;bool _pointerResult;
 RectTransform _root,_context,_status,_speech,_archive,_body,_pageNav,_localNav,_footer,_decisionHeader,_decisionFooter,_archiveScroll,_archiveScrollTrack;GameObject _shield,_confirm;
 bool _statusDirty,_contextDirty;bool PointerBusy=>Input.GetMouseButton(0)||Input.GetMouseButtonUp(0);
 Button _returnComparison;bool _mapViewing,_comparisonValid;int _comparisonRevision;long _comparisonTick;string _layerBeforeComparison="terrain";
 Text _dt,_population,_food,_time,_legend,_toast,_title,_subtitle;Button _pause;bool _archiveOpen,_refreshing;string _page="population_home",_pageContext="",_base="homestead",_offer="",_previewVersion="";V _mapSnapshot=V.Map(),_baseline=V.Map(),_proposal=V.Map(),_feedbackView=V.Map();float _toastUntil,_nextFeedback;string _toastPage="";
 Game _viewGame;V _state=V.Map();
 Game G=>_viewGame;V S=>_state;
 float TextScale=>Mathf.Clamp((float)Runtime.DisplaySettings.Get("text_scale",1).Double,1,1.5f);
 static string F(double n,int d=1)=>n.ToString("F"+d,CultureInfo.InvariantCulture);
 static string Signed(double n,int d=1)=>n.ToString("+0."+new string('0',d)+";-0."+new string('0',d)+";0."+new string('0',d),CultureInfo.InvariantCulture);
 string Resource(string id)=>id=="currency"?"通货":id=="advanced_components"?"高级部件":Catalog.RESOURCE_NAMES.Get(id,id).Text;
 string Service(string id)=>Catalog.SERVICE_NAMES.Get(id,id).Text;
 string Stat(string id)=>V.Map("municipal_storage","市政仓容","housing","住房","jobs","岗位","efficiency","建筑效率","personnel_mult","人员效率","energy_demand","用电需求","pollution","污染","maintenance","维护","construction_capacity","施工能力","trade_capacity","货运能力","output.food","粮食产出","output.material","材料产出","output.goods","制成品产出","output.energy","供电能力","input.food","粮食投入","input.material","材料投入","input.goods","制成品投入","storage.food","粮食仓容","storage.material","材料仓容","storage.goods","制成品仓容","service.medical","医疗容量","service.education","教育容量","service.commercial","商业容量","service.entertainment","娱乐容量").Get(id,id).Text;
 Color Domain(V p){
  var definition=G.c.projects.Get(p.Get("def",""),V.Map());var domain=definition.Get("domain","public").Text;var traits=definition.Get("traits",V.Arr());
  string purpose=traits.Contains("building.logistics")||traits.Contains("building.energy")||traits.Contains("building.extractive")||domain=="industrial"||domain=="infrastructure"?"industry":
   domain=="agriculture"?"agriculture":domain=="community"||domain=="residential"?"housing":p.Get("def","").Text=="garden"?"green":"community";
  return CityWorld.BuildingTypeColor(purpose);
 }
 void Awake(){Runtime=GetComponent<CityRuntime>()??FindAnyObjectByType<CityRuntime>();Runtime.LoadDisplaySettings();UpdateDetached(Runtime.Snapshot);_font=Resources.Load<Font>("CityFonts/NotoSansSC");if(_font==null)_font=Font.CreateDynamicFontFromOSFont("PingFang SC",18);_audio=gameObject.AddComponent<AudioSource>();_audio.playOnAwake=false;BuildShell();Runtime.Committed+=OnCommitted;Runtime.Message+=ShowMessage;}
 void Start(){OnCommitted(Runtime.Snapshot);if(Runtime.StartupMessage!="")ShowMessage(Runtime.StartupMessage);}
 void OnDestroy(){if(_toolkitUpgrade!=null)_toolkitUpgrade.Hide();CloseDeskTip(false,false);DismissMapInspection();if(Runtime!=null){Runtime.Committed-=OnCommitted;Runtime.Message-=ShowMessage;}}
 void Update(){UpdateDeskInput();UpdateHoverProgress();if(_toast!=null)_toast.gameObject.SetActive(UnityEngine.Time.unscaledTime<_toastUntil);if(UnityEngine.Time.unscaledTime>=_nextFeedback){_nextFeedback=UnityEngine.Time.unscaledTime+.3f;if(Runtime.Ready&&S["phase"].Text=="play"){_feedbackView=_feedback.view(UnityEngine.Time.realtimeSinceStartup);RenderStatus();}}
  if(!PointerBusy){if(_statusDirty)RenderStatus();if(_contextDirty)RefreshContext();}
  if(Input.GetKeyDown(KeyCode.Escape)){if(ReleaseTextInput()){}else if(HandleDeskTipEscape()){}else if(HandleMapInspectionEscape()){}else if(_receipt!=null)_receiptSkip=true;else if(_confirm!=null)DismissConfirm();else if(ToolkitUpgradeOpen)CloseToolkitUpgrade();else if(ConstructionWindowOpen)CloseConstructionWindow();else if(ObjectDrawerVisible)CloseObjectDrawer();else if(_mapFromCompact&&SelectedTile!=""){DismissLocalSelection();}else if(_archiveOpen)Back();else if(ConstructionIntentActive){EndConstructionIntent();}else if(SelectedTile!=""){EndConstructionIntent();SelectedTile="";SelectedBuilding="";RefreshContext();SelectionChanged?.Invoke("","");}else OpenArchive("settings");}
  if(!KeyboardOwned&&Input.GetKeyDown(KeyCode.P))Runtime.SetPaused(!Runtime.WorldPaused);
 }
 public bool PointerOwned(Vector2 position){
  if(IsModal)return true;
  if(_pointerFrame==UnityEngine.Time.frameCount&&_pointerPosition==position)return _pointerResult;
  _pointerData=_pointerData??new PointerEventData(EventSystem.current);
  _pointerData.position=position;_pointerHits.Clear();_raycaster.Raycast(_pointerData,_pointerHits);
  _pointerFrame=UnityEngine.Time.frameCount;_pointerPosition=position;_pointerResult=_pointerHits.Count>0;return _pointerResult;
 }
 RectTransform Node(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
 RectTransform Surface(string name,Transform parent,Color color){var node=Node(name,parent);node.gameObject.AddComponent<Image>().color=color;DressSurface(node,name,color);return node;}
 void Pin(RectTransform r,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size){r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size;}
 void Stretch(RectTransform r,float left=0,float top=0,float right=0,float bottom=0){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);}
 RectTransform Column(Transform parent,string name="Column",float gap=12,bool fit=true){var r=Node(name,parent);var l=r.gameObject.AddComponent<VerticalLayoutGroup>();l.spacing=gap;l.padding=new RectOffset(16,16,14,14);l.childControlWidth=true;l.childControlHeight=true;l.childForceExpandWidth=true;l.childForceExpandHeight=false;if(fit){var f=r.gameObject.AddComponent<ContentSizeFitter>();f.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}return r;}
 RectTransform Row(Transform parent,string name="Row",float height=44){var r=Node(name,parent);var l=r.gameObject.AddComponent<HorizontalLayoutGroup>();l.spacing=12;l.childControlWidth=true;l.childControlHeight=true;l.childForceExpandWidth=true;l.childForceExpandHeight=false;var minimum=r.gameObject.AddComponent<LayoutElement>();minimum.minHeight=height;if(name=="City arc navigation")r.gameObject.SetActive(false);return r;}
 void Height(RectTransform r,float h){var e=r.GetComponent<LayoutElement>()??r.gameObject.AddComponent<LayoutElement>();e.minHeight=h;e.preferredHeight=h;}
 void Width(RectTransform r,float w){var e=r.GetComponent<LayoutElement>()??r.gameObject.AddComponent<LayoutElement>();e.minWidth=w;e.preferredWidth=w;e.flexibleWidth=0;}
 Text Label(Transform parent,string value,int size=18,Color? color=null,float h=0){var r=Node("Text",parent);var t=r.gameObject.AddComponent<Text>();t.font=_font;_fontBases[t]=size;t.text=value;t.fontSize=Mathf.RoundToInt(size*TextScale);t.lineSpacing=1.18f;t.fontStyle=size>=22?FontStyle.Bold:FontStyle.Normal;t.color=DeskText(parent,color??Ink);t.raycastTarget=false;if(Runtime.DisplaySettings.Get("high_contrast",false).Bool){var outline=r.gameObject.AddComponent<Outline>();outline.effectColor=Color.black;outline.effectDistance=new Vector2(1,-1);}t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;if(h>0)Height(r,h);else {var layout=r.gameObject.AddComponent<LayoutElement>();layout.minHeight=Math.Max(30,(size+13)*TextScale);}return t;}
 Button Button(Transform parent,string text,Action click,bool primary=false,bool disabled=false){var r=Surface("Button "+text,parent,primary?new Color(.065f,.25f,.23f,.98f):Raised);Height(r,46*TextScale);var b=r.gameObject.AddComponent<Button>();var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.3f,1.5f,1.5f);colors.selectedColor=new Color(1.25f,1.6f,1.6f);colors.pressedColor=new Color(.6f,.9f,.85f);colors.disabledColor=new Color(.45f,.5f,.5f,.7f);b.colors=colors;b.interactable=!disabled;var t=Label(r,text,17,primary?new Color(.72f,1f,.93f):Ink);Stretch(t.rectTransform,12,4,12,4);t.alignment=TextAnchor.MiddleCenter;t.fontStyle=primary?FontStyle.Bold:FontStyle.Normal;b.onClick.AddListener(()=>{PlayCue("select");click();});return b;}
 Button Tab(Transform parent,string text,string id,Action click,string current){var active=id==current;var b=Button(parent,text,click,active);b.GetComponent<Image>().color=active?new Color(.07f,.26f,.24f,.98f):Raised;var t=b.GetComponentInChildren<Text>();t.color=active?new Color(.60f,1f,.88f):Muted;t.fontStyle=active?FontStyle.Bold:FontStyle.Normal;return b;}
 InputField InputBox(Transform parent,string value,string placeholder,float width=0){var r=Surface("Input "+placeholder,parent,new Color(.04f,.08f,.09f));Height(r,48*TextScale);if(width>0)Width(r,width);var input=r.gameObject.AddComponent<InputField>();var t=Label(r,"",17);Stretch(t.rectTransform,12,7,12,7);input.textComponent=t;var p=Label(r,placeholder,16,Muted);Stretch(p.rectTransform,12,7,12,7);input.placeholder=p;input.text=value;input.characterLimit=512;return input;}
 RectTransform Card(Transform parent,string heading,string note="",Color? color=null){var outer=Surface("Card "+heading,parent,Raised);var body=Column(outer);var fitter=outer.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;var layout=outer.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;Label(body,heading,22,color??Ink);if(note!="")Label(body,note,17,Muted);return body;}
 void Metric(Transform parent,string label,string value,string note="",Color? color=null){var row=Row(parent,"Metric",42*TextScale);Label(row,label,18,Muted);var t=Label(row,value,23,color??Ink);t.alignment=TextAnchor.MiddleRight;if(note!="")Label(parent,note,16,Muted);}
 void Meter(Transform parent,string label,double value,double capacity,string suffix="",Color? color=null){Metric(parent,label,F(value)+" / "+F(capacity)+suffix,"",color);var node=Node("Meter "+label,parent);Height(node,9);var g=node.gameObject.AddComponent<CityGraphic>();g.Kind="bar";g.Ratio=(float)(value/Math.Max(.000001,capacity));g.color=color??Green;g.raycastTarget=false;}
 void Mark(Transform parent,string kind,Color color,float size=98){var r=Node("Site mark",parent);Width(r,size);Height(r,size);var g=r.gameObject.AddComponent<CityGraphic>();g.Kind=kind;g.color=color;g.raycastTarget=false;}
 void Fold(Transform parent,string name,Action<RectTransform> render){
  var ancestry="";for(var node=parent;node!=null&&node!=_archive;node=node.parent)ancestry=node.name+"/"+ancestry;string foldKey=PageStateKey+"/"+ancestry+name+"/"+parent.childCount;
  var box=Column(parent,"Disclosure",4);box.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);
  var content=Column(box,"DisclosureBody",7);content.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(12,8,8,8);content.gameObject.SetActive(false);bool rendered=false;
  Button b=null;
  b=Button(box,name+" ›",()=>{if(!rendered){render(content);rendered=true;}bool open=!content.gameObject.activeSelf;content.gameObject.SetActive(open);if(open)_openWorkspaceFolds.Add(foldKey);else _openWorkspaceFolds.Remove(foldKey);b.GetComponentInChildren<Text>().text=name+(open?" ⌄":" ›");RefreshModalOwnership();});
  b.transform.SetAsFirstSibling();b.GetComponentInChildren<Text>().alignment=TextAnchor.MiddleLeft;
  b.GetComponent<Image>().color=new Color(.07f,.115f,.14f,.78f);
  if(_openWorkspaceFolds.Contains(foldKey)){render(content);rendered=true;content.gameObject.SetActive(true);b.GetComponentInChildren<Text>().text=name+" ⌄";}

 }
 void Raw(Transform parent,V value){var text=value.ToJson(true,true);foreach(var block in ChunkText(text,4000))Label(parent,block,13,Muted,Math.Max(40,block.Split('\n').Length*21*TextScale));}
 IEnumerable<string> ChunkText(string text,int limit){for(var offset=0;offset<text.Length;offset+=limit)yield return text.Substring(offset,Math.Min(limit,text.Length-offset));}
 void ForgetFonts(Transform parent){foreach(var text in parent.GetComponentsInChildren<Text>(true))_fontBases.Remove(text);}
 void Clear(Transform parent){CancelDeskTipsIn(parent);for(var i=parent.childCount-1;i>=0;i--){var child=parent.GetChild(i);foreach(var text in child.GetComponentsInChildren<Text>(true))_fontBases.Remove(text);child.gameObject.SetActive(false);child.SetParent(null);Destroy(child.gameObject);}}
 void BuildShell(){if(EventSystem.current==null){var es=new GameObject("City Event System",typeof(EventSystem),typeof(StandaloneInputModule));}
  var canvasObject=new GameObject("City Command Interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasObject.transform.SetParent(transform,false);_canvas=canvasObject.GetComponent<Canvas>();_canvas.renderMode=RenderMode.ScreenSpaceOverlay;_canvas.sortingOrder=100;_scaler=canvasObject.GetComponent<CanvasScaler>();_scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;_scaler.matchWidthOrHeight=.5f;_root=(RectTransform)canvasObject.transform;_raycaster=canvasObject.GetComponent<GraphicRaycaster>();ApplyDisplay();
  BuildPlanningHud();
  BuildBonusBucket();BuildArchive();BuildDeskTip();_returnComparison=Button(_root,"返回方案比较 ›",ReturnFromMapView,true);Pin((RectTransform)_returnComparison.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-94),new Vector2(260,52));_returnComparison.gameObject.SetActive(false);_toast.transform.SetAsLastSibling();
 }
 void BuildArchive(){
  _shield=Surface("L3 Archive Shield",_root,new Color(.005f,.015f,.02f,.07f)).gameObject;Stretch((RectTransform)_shield.transform);
  _archive=Surface("L3 City Side Panel",_shield.transform,Panel);Pin(_archive,new Vector2(1,1),new Vector2(1,1),new Vector2(-18,-88),new Vector2(900,870));
  _title=Label(_archive,"城市",27,Ink);Pin(_title.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(20,-12),new Vector2(700,41));
  _subtitle=Label(_archive,"",14,Muted);Pin(_subtitle.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(20,-57),new Vector2(710,30));
  var close=Button(_archive,"×",CloseArchive);Pin((RectTransform)close.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-16,-14),new Vector2(42,39));
  _pageNav=Row(_archive,"Archive Categories",42);Pin(_pageNav,new Vector2(0,1),new Vector2(0,1),new Vector2(20,-102),new Vector2(860,42));_pageNav.GetComponent<HorizontalLayoutGroup>().spacing=6;

  _pageBack=Button(_archive,"‹ 返回",Back);
  _localNav=Row(_archive,"Archive Section Tabs",39);Pin(_localNav,new Vector2(0,1),new Vector2(0,1),new Vector2(20,-154),new Vector2(860,39));_localNav.GetComponent<HorizontalLayoutGroup>().spacing=5;
  var scroll=Node("Archive Scroll",_archive);_archiveScroll=scroll;Stretch(scroll,20,204,22,68);var image=scroll.gameObject.AddComponent<Image>();image.color=new Color(.01f,.025f,.03f,.15f);scroll.gameObject.AddComponent<RectMask2D>();var sr=scroll.gameObject.AddComponent<ScrollRect>();sr.horizontal=false;sr.scrollSensitivity=48;sr.movementType=ScrollRect.MovementType.Clamped;sr.viewport=scroll;_body=Column(scroll,"Archive Body",9);_body.anchorMin=new Vector2(0,1);_body.anchorMax=new Vector2(1,1);_body.pivot=new Vector2(.5f,1);_body.sizeDelta=new Vector2(-12,0);sr.content=_body;
  var track=Surface("Archive scroll track",_archive,new Color(.10f,.17f,.19f));_archiveScrollTrack=track;track.anchorMin=new Vector2(1,0);track.anchorMax=new Vector2(1,1);track.pivot=new Vector2(1,.5f);track.offsetMin=new Vector2(-14,68);track.offsetMax=new Vector2(-9,-204);var thumb=Surface("Archive scroll thumb",track,new Color(.26f,.59f,.55f));Stretch(thumb);var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=thumb;bar.targetGraphic=thumb.GetComponent<Image>();sr.verticalScrollbar=bar;sr.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
  _footer=Row(_archive,"Archive Footer",42);Pin(_footer,new Vector2(0,0),new Vector2(0,0),new Vector2(20,12),new Vector2(860,42));_shield.SetActive(false);
  _decisionHeader=Column(_archive,"Construction Decision Header",6,false);Pin(_decisionHeader,new Vector2(0,1),new Vector2(0,1),new Vector2(20,-94),new Vector2(860,126));_decisionHeader.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(8,8,8,8);
  _decisionFooter=Column(_archive,"Construction Decision Footer",6,false);Pin(_decisionFooter,new Vector2(0,0),new Vector2(0,0),new Vector2(20,12),new Vector2(860,108));_decisionFooter.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(8,8,8,8);
  _decisionHeader.gameObject.SetActive(false);_decisionFooter.gameObject.SetActive(false);
 }
 public void ApplyDisplay(){var factor=Mathf.Clamp((float)Runtime.DisplaySettings.Get("ui_scale",1).Double,.8f,1.2f);if(_scaler!=null)_scaler.referenceResolution=new Vector2(2560/factor,1440/factor);RefreshHudGeometry();Application.targetFrameRate=Runtime.EffectiveFrameLimit;if(_root!=null){var contrast=Runtime.DisplaySettings.Get("high_contrast",false).Bool;foreach(var text in _root.GetComponentsInChildren<Text>(true)){var outline=text.GetComponent<Outline>();if(contrast&&outline==null)outline=text.gameObject.AddComponent<Outline>();if(outline!=null){outline.enabled=contrast;outline.effectColor=Color.black;outline.effectDistance=new Vector2(1,-1);}}}if(_archiveOpen&&_archive!=null)LayoutArchive();if(ConstructionWindowOpen)RefreshConstructionWindow();RefreshToolkitUpgrade();LayoutObjectDrawer();DisplayChanged?.Invoke(Runtime.DisplaySettings.Duplicate(true));}
 public void ShowMessage(string text){if(_toast==null||text=="")return;_toast.transform.SetAsLastSibling();if(_planningToastSurface!=null)_planningToastSurface.SetAsLastSibling();_toast.text=text;_toastPage=_archiveOpen?_page:"";_toastUntil=UnityEngine.Time.unscaledTime+8;_toast.gameObject.SetActive(true);}
 void DismissLocalSelection(){EndConstructionIntent();SelectedTile="";SelectedBuilding="";RefreshContext();SelectionChanged?.Invoke("","");}
 public void SelectTile(string tile){if(IsModal||(_mapViewing&&!_mapFromCompact)||!S["cells"].Has(tile))return;if(CompactWorkspaceOpen)ShowLocalMapFromCompact();SelectedTile=tile;SelectedBuilding=S["cells"][tile].Get("building","").Text;if(SelectedBuilding!="")EndConstructionIntent();RefreshContext();SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);}
 public void SelectBuilding(string id){if((_mapViewing&&!_mapFromCompact)||!S["projects"].Has(id))return;EndConstructionIntent();DismissMapInspection();SelectedBuilding=id;SelectedTile=S["projects"][id].Get("tile","").Text;RefreshContext();SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);}
 public void SetLayer(string layer){if(layer=="service")layer="medical";if(layer==Layer)return;CancelDeskTipsForPanel();DismissMapInspection();Layer=layer;RefreshHudGeometry();_legend.text=LayerLabel(layer)+" · "+(MapSourcePhase=="committed"?"最近周期实际":MapSourcePhase=="baseline"?"同一步本地现状预测":"同一步本地建成后预测")+"   L / Shift+L · Shift 数值";LayerChanged?.Invoke(layer);}
 string LayerLabel(string layer)=>V.Map("terrain","地貌","service","医疗","medical","医疗","education","教育","entertainment","娱乐","commercial","消费","employment","就业：居民比例 / 无居民可达度","pollution","环境场强 · 当前街区地格口径","heat","余热来源","stock","本地库存","delta","本次污染场强改变 · 正值负担增加 / 负值减轻","energy","全城电力能力").Get(layer,Resource(layer)).Text;
 void UpdateDetached(V snapshot){_state=snapshot.Duplicate(true);_viewGame=_viewGame??new Game(Runtime.Game.c,Runtime.StorageRoot);_viewGame.s=_state;_viewGame.compiled=Runtime.Game.compiled.Duplicate(true);_viewGame.preview_mode=true;_viewGame.world_managed=Runtime.Game.world_managed;_viewGame.world_effects=Runtime.Game.world_effects.Duplicate(true);_viewGame.revision=Runtime.Game.revision;_viewGame.last_committed_revision=Runtime.Game.last_committed_revision;}
 void OnCommitted(V snapshot){_worldView=null;var identity=Runtime.Session.FocusedBlockId+":"+snapshot.Get("seed",0).Text+":"+snapshot.Get("started_at",0).Text;var cityChanged=_cityIdentity!=""&&_cityIdentity!=identity;if(_cityIdentity!=""&&_cityIdentity!=identity){CloseObjectDrawer(false);_trackedGoal="";if(ToolkitUpgradeOpen)CloseToolkitUpgrade();if(ConstructionWindowOpen)CloseConstructionWindow();CancelDeskTipsForPanel();DismissMapInspection();EndConstructionIntent();SelectedTile="";SelectedBuilding="";_feedback.reset();_rewardSelections.Clear();_inventoryChoice="";_blueprintHost="";_blueprintSlot="";_installView=V.Map();_blueprintVersion="";_contextPreview=V.Map();_contextPreviewVersion="";_statusKey="";_contextKey="";SelectionChanged?.Invoke("","");_seenTheme=false;}_cityIdentity=identity;UpdateDetached(snapshot);if(_comparisonValid&&(cityChanged||_comparisonRevision!=Runtime.Game.revision||_comparisonTick!=S["simulation_tick"].Long)){if(_mapViewing)ReturnFromMapView();ClearComparison();}var complete=S["block"].Get("theme",V.Map()).Get("completed",false).Bool;if(_seenTheme&&complete&&!_completedTheme)PlayCue("complete");_completedTheme=complete;_seenTheme=true;if(_refreshing)return;_refreshing=true;try{UpdateStatusModel();UpdateHud();RenderStatus();if(MapSourcePhase=="committed")MapProjectionChanged?.Invoke(snapshot.Duplicate(true));RefreshContext();if(ConstructionWindowOpen)RefreshConstructionWindow();RefreshToolkitUpgrade();if((Runtime.Session.s["phase"].Text=="choose_district"&&Runtime.Session.Games.Count==0)||S["phase"].Text=="select_block"||S["phase"].Text=="origin"){if(!_archiveOpen||_page!="start")OpenArchive("start");else RenderPage();}else if(_archiveOpen&&(_page=="start"||_page=="district_start"||_page=="block_start"))CloseArchive();else if(!_archiveOpen&&Runtime.MandatoryPending)OpenArchive("settlement");}finally{_refreshing=false;}}
 void UpdateHud(){RefreshPlanningHud();RefreshBucket();}

 void CloseArchive(){CloseObjectDrawer(false);RememberSection();_mapFromCompact=false;_renderedPageKey="";CancelDeskTipsForPanel();DismissMapInspection();EndConstructionIntent();if(S["phase"].Text!="play"){ShowMessage("请先选择登陆点与起源。");return;}_mapViewing=false;_returnComparison.gameObject.SetActive(false);_archiveOpen=false;_shield.SetActive(false);RefreshHudGeometry();_planningRail.GetComponent<Image>().color=_hudFill;_navigation.Clear();Clear(_body);Clear(_decisionHeader);Clear(_decisionFooter);_archiveScroll.GetComponent<ScrollRect>().StopMovement();_selectedRecord=V.Map();ClearComparison();Runtime.SetDecisionOpen(ConstructionWindowOpen);RefreshContext();RefreshModalOwnership(true);RefreshHudGeometry();}
 void PublishComparison(){if(_baseline.Count==0||ComparisonProposal.Count==0){ClearComparison();return;}if(!_comparisonValid)_layerBeforeComparison=Layer=="delta"?"terrain":Layer;_comparisonValid=true;_comparisonRevision=Runtime.Game.revision;_comparisonTick=S["simulation_tick"].Long;ComparisonChanged?.Invoke(ComparisonBaseline,ComparisonProposal);}
 void ClearComparison(){_planner.cache.Clear();_planner.lru.Clear();_planner.token="";_comparisonValid=false;_baseline=V.Map();_proposal=V.Map();ComparisonChanged?.Invoke(V.Map(),V.Map());if(Layer=="delta")SetLayer(_layerBeforeComparison);SetMapProjection("committed",null);}
 void InspectMapFromPage(){_objectDrawerSuspended=true;if(MapSourcePhase!="committed")ClearComparison();_mapFromCompact=true;RememberWorkspace();CancelDeskTipsForPanel();DismissMapInspection();_mapViewing=true;_shield.SetActive(false);_returnComparison.GetComponentInChildren<Text>().text=_archiveOpen?"返回「"+PageTitle(_page)+"」 ›":"返回建筑详情 ›";_returnComparison.gameObject.SetActive(true);LayoutObjectDrawer();RefreshContext();RefreshHudGeometry();RefreshModalOwnership(true);}
 void ShowForecastMap(string phase,V snapshot){_objectDrawerSuspended=true;LayoutObjectDrawer();_mapFromCompact=false;CancelDeskTipsForPanel();DismissMapInspection();SetMapProjection(phase,snapshot);_returnComparison.GetComponentInChildren<Text>().text="返回方案比较 ›";_mapViewing=true;_shield.SetActive(false);_returnComparison.gameObject.SetActive(true);_context.gameObject.SetActive(false);RefreshHudGeometry();RefreshModalOwnership(true);}
 void ReturnFromMapView(){_objectDrawerSuspended=false;_mapFromCompact=false;CancelDeskTipsForPanel();DismissMapInspection();_mapViewing=false;_returnComparison.gameObject.SetActive(false);_shield.SetActive(_archiveOpen);if(_archiveOpen)LayoutArchive();LayoutObjectDrawer();RefreshContext();RefreshHudGeometry();RefreshModalOwnership(true);}
 void SetMapProjection(string source,V data){MapSourcePhase=source;_mapSnapshot=data??V.Map();_legend.text=LayerLabel(Layer)+" · "+(source=="committed"?"最近周期实际":source=="baseline"?"同一步本地现状预测":"同一步本地建成后预测");MapProjectionChanged?.Invoke(MapSnapshot);}
 void Command(Func<Game,string> command,bool refresh=true){var error=Runtime.Execute(command);PlayCue(error==""?"confirm":"notice");if(error==""&&refresh){if(_archiveOpen)RenderPage();else RefreshObjectDrawer();RefreshContext();}}
 public void PlayCue(string id){if(_audio==null||!S.Get("settings",V.Map()).Get("sound",true).Bool)return;var clip=Resources.Load<AudioClip>("CityAudio/"+id);if(clip!=null)_audio.PlayOneShot(clip,.3f);}
 void Confirm(string title,string description,Action action){CancelDeskTipsForPanel();DismissMapInspection();if(_confirm!=null)DismissConfirm();Runtime.Flush();_confirm=Surface("Confirmation Shield",_root,new Color(0,0,0,.82f)).gameObject;Stretch((RectTransform)_confirm.transform);var card=Surface("Confirm",_confirm.transform,Panel);Pin(card,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(680,330));var body=Column(card);Stretch(body);Label(body,title,26,Gold);Label(body,description,18,Muted,150);var row=Row(body);Button(row,"取消",DismissConfirm);Button(row,"确认",()=>{DismissConfirm();action();},true);RefreshModalOwnership(true);}
 void DismissConfirm(){CancelDeskTipsForPanel();if(_confirm==null)return;ForgetFonts(_confirm.transform);_confirm.SetActive(false);Destroy(_confirm);_confirm=null;RefreshModalOwnership(true);}
 void RefreshModalOwnership(bool focus=false){
  _pointerFrame=-1;_pointerHits.Clear();
  if((_archiveOpen&&!_mapFromCompact)||ConstructionWindowOpen||ToolkitUpgradeOpen){_context.gameObject.SetActive(false);_bonusBucket.gameObject.SetActive(false);}else _bonusBucket.gameObject.SetActive(false);
  // CanvasGroup ownership excludes background controls from uGUI automatic navigation,
  // including controls added later when committed alerts/context are refreshed.
  foreach(Transform child in _root){
   var group=child.GetComponent<CanvasGroup>();if(group==null)group=child.gameObject.AddComponent<CanvasGroup>();group.alpha=1;
   if(ToolkitUpgradeOpen){group.interactable=false;continue;}
   if(child.gameObject==_confirm||(_deskTip!=null&&child==_deskTip))group.interactable=true;
   else if(child==_objectDrawer)group.interactable=ObjectDrawerVisible&&_confirm==null&&!ConstructionWindowOpen;
   else if(child.gameObject==ConstructionWindowShield)group.interactable=ConstructionWindowOpen&&_confirm==null;
   else if(child==_planningRail||child==_planningTop)group.interactable=_confirm==null&&(!_mapViewing||_mapFromCompact)&&!ConstructionWindowOpen;
   else if(child.gameObject==_shield)group.interactable=_archiveOpen&&!_mapViewing&&_confirm==null&&!ConstructionWindowOpen;
   else {group.interactable=!IsModal;group.alpha=IsModal?.65f:1f;}
  }
  if(_archiveOpen&&!_mapViewing){_planningRail.SetAsLastSibling();_planningTop.SetAsLastSibling();}
  if(ObjectDrawerVisible)_objectDrawer.SetAsLastSibling();if(ConstructionWindowOpen)ConstructionWindowShield.transform.SetAsLastSibling();if(_confirm!=null)_confirm.transform.SetAsLastSibling();if(_deskTip!=null&&_deskTip.gameObject.activeSelf)_deskTip.SetAsLastSibling();
  if(ToolkitUpgradeOpen||!focus||EventSystem.current==null)return;
  EventSystem.current.SetSelectedGameObject(null);
  Transform scope=_confirm!=null?_confirm.transform:ConstructionWindowOpen?ConstructionWindowRoot:ObjectDrawerVisible?_objectDrawer:_archiveOpen&&!_mapViewing?_archive:_mapViewing?_returnComparison.transform:_root;
  Selectable selected=null;if(scope==_root){var entry=_archiveEntryFocus!=null&&_archiveEntryFocus.activeInHierarchy?_archiveEntryFocus.GetComponent<Selectable>():null;selected=entry!=null&&entry.IsInteractable()?entry:_pause;}
  if(scope==_archive)selected=_body.GetComponentsInChildren<Selectable>(false).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.IsInteractable());
  if(scope==ConstructionWindowRoot)selected=_constructionBody.GetComponentsInChildren<Selectable>(false).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.IsInteractable());
  selected=selected??scope.GetComponentsInChildren<Selectable>(false).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.IsInteractable());
  if(selected!=null)EventSystem.current.SetSelectedGameObject(selected.gameObject);
 }
 void LayoutArchive(){LayoutV3Workspace();}

 void RenderPage(){if(TryOpenConstructionRoute(_page,_pageContext))return;CancelDeskTipsForPanel();DismissMapInspection();ResetWorkspace();LayoutArchive();Clear(_body);Clear(_decisionHeader);Clear(_decisionFooter);_body.anchoredPosition=Vector2.zero;_pageNav.gameObject.SetActive(false);_footer.gameObject.SetActive(false);_decisionHeader.gameObject.SetActive(false);_decisionFooter.gameObject.SetActive(false);UpdateArchiveTabs();UpdateLocalArchiveTabs();_title.text=PageTitle(_page);_subtitle.text=PageSubtitle;RenderGoalContext();
  switch(_page){case "population_home":PopulationHomePage();break;case "economy_home":EconomyHomePage();break;case "bonus_home":if(_advancedWorkspace)BonusesPage();else V3BonusPage();break;case "tasks_home":if(_advancedWorkspace)PlanningTasksPage();else V3TasksPage();break;case "archive_home":ArchiveHomePage();break;case "construction":ConstructionPage();break;case "city":CityArcPage();break;case "district_start":DistrictStartPage();break;case "block_start":NewBlockPage();break;case "governance":GovernancePage();break;case "network":NetworkPage();break;case "science":SciencePage();break;case "city_project":CityProjectPage();break;case "heritage":HeritagePage();break;case "content":ContentPage();break;case "start":StartPage();break;case "overview":break;case "resources":ResourcesPage();break;case "population":PopulationPage();break;case "market":V3MarketPage();break;case "policies":OperatingPoliciesPage();break;case "bonuses":BonusesPage();break;case "themes":PlanningTasksPage();break;case "milestones":MilestonesPage();break;case "blueprints":BlueprintOffersPage();break;case "settlement":SettlementPage();break;case "records":RecordsPage();break;case "settings":SettingsPage();break;case "building":BuildingPage();break;case "upgrades":UpgradePage();break;case "slot":NativeSlotPage();break;case "tender":TenderPage();break;case "sources":SourcesPage();break;case "record":RecordPage();break;default:Label(_body,"此页面暂未开放。",20,Muted);break;}
  RefreshHudGeometry();LayoutArchive();Canvas.ForceUpdateCanvases();var scroll=_archiveScroll.GetComponent<ScrollRect>();scroll.StopMovement();RestoreWorkspace();RefreshObjectDrawer();RefreshModalOwnership(true);
 }
 string PageTitle(string page)=>V.Map("population_home","居民与服务","economy_home","资源与财政","bonus_home","Bonus","tasks_home","任务与成长","archive_home","档案","construction","施工队列","city","永久城市与地区","district_start","下一地区任期","block_start","新街区与供给来源","governance","地区治理与交班","network","区域物流","science","科研与高级工业","city_project","托卡马克之冠","heritage","分层遗产与世界史","content","街区构筑与长期成果","start","河谷登陆 / 起源","overview","城市〔暂未开放〕","resources","收支平衡表","population","居民详表","market","外部市场","policies","经营政策","bonuses","部署比较","blueprints","领取奖励","themes","任务详情","milestones","城市里程碑","settlement","年度 / 大周期 / 街区结算","records","档案","settings","菜单与显示设置","building","建筑档案","upgrades","成长与升级","slot","方案槽与安装","tender","建造方案","sources","完整规则与来源","record","只读街区结算记录").Get(page,page).Text;
}
}
