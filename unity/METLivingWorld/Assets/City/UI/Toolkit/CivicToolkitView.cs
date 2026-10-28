using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MET.City {
// A single retained document owns screen layout, focus and per-region reading positions.
public sealed class CivicToolkitView : MonoBehaviour {
 UIDocument document;
 PanelSettings settings;
 readonly Dictionary<string,Vector2> positions=new Dictionary<string,Vector2>();
 readonly Dictionary<string,string> regionKeys=new Dictionary<string,string>();
 readonly Dictionary<string,bool> folds=new Dictionary<string,bool>();
 float uiScale=1,textScale=1,tooltipAt;
 VisualElement tooltip;Label tooltipText;string hoveredTooltip="";
 public VisualElement Root {get;private set;}
 public bool Ready=>Root!=null;
 public bool ManagementMode {get;private set;}
 public VisualElement Q(string name)=>Root?.Q<VisualElement>(name);
 public void Initialize(){
  var template=Resources.Load<VisualTreeAsset>("CityToolkit/CivicShell");
  var asset=Resources.Load<PanelSettings>("CityToolkit/UpgradePanel");
  if(template==null||asset==null)throw new InvalidOperationException("MET Civic UI resources missing.");
  settings=Instantiate(asset);settings.name="MET Civic Runtime Panel";settings.sortingOrder=130;
  var go=new GameObject("MET Civic Interface");go.SetActive(false);go.transform.SetParent(transform,false);
  document=go.AddComponent<UIDocument>();document.panelSettings=settings;document.visualTreeAsset=template;document.sortingOrder=130;go.SetActive(true);
  Root=document.rootVisualElement;Root.pickingMode=PickingMode.Ignore;
  Root.style.color=(Color)new Color32(232,241,242,255);
  var font=Resources.Load<Font>("CityFonts/NotoSansSC");if(font!=null)Root.style.unityFontDefinition=FontDefinition.FromFont(font);
  Q("civic-nav").RegisterCallback<GeometryChangedEvent>(_=>LayoutDock());
  Q("civic-top").RegisterCallback<GeometryChangedEvent>(_=>LayoutDock());
  Q("civic-clock").RegisterCallback<GeometryChangedEvent>(_=>LayoutDock());
  Root.RegisterCallback<GeometryChangedEvent>(_=>LayoutDock());
  Root.RegisterCallback<ChangeEvent<bool>>(e=>{if(e.target is Foldout fold&&e.newValue)fold.schedule.Execute(()=>ApplyText(fold));});
  Root.Query<ScrollView>().ForEach(s=>{s.horizontalScrollerVisibility=ScrollerVisibility.Hidden;s.verticalScrollerVisibility=ScrollerVisibility.Auto;s.mouseWheelScrollSize=48;});
  foreach(var name in new[]{"workspace","workspace-shade","tile","drawer","operation-shade","toast","map-return"})Visible("civic-"+name,false);
  Visible("civic-management-body",false);
  tooltip=new VisualElement{pickingMode=PickingMode.Ignore};tooltip.AddToClassList("c-tooltip");tooltipText=new Label{pickingMode=PickingMode.Ignore};tooltipText.AddToClassList("c-small");tooltip.Add(tooltipText);Root.Add(tooltip);tooltip.style.display=DisplayStyle.None;
  Root.RegisterCallback<PointerOverEvent>(e=>{var v=e.target as VisualElement;string message="";for(var p=v;p!=null&&p!=Root;p=p.parent)if(!string.IsNullOrEmpty(p.tooltip)){message=p.tooltip;break;}if(message==hoveredTooltip)return;hoveredTooltip=message;tooltipAt=Time.unscaledTime+.5f;tooltip.style.display=DisplayStyle.None;});
  Root.RegisterCallback<PointerLeaveEvent>(e=>{hoveredTooltip="";tooltip.style.display=DisplayStyle.None;});
 }
 void Update(){
  if(tooltip==null||Root?.panel==null)return;if(hoveredTooltip==""||Time.unscaledTime<tooltipAt||Input.GetMouseButton(0)||TextFocused){tooltip.style.display=DisplayStyle.None;return;}
  tooltipText.text=hoveredTooltip;tooltipText.style.fontSize=18*textScale;tooltip.style.display=DisplayStyle.Flex;
  var point=RuntimePanelUtils.ScreenToPanel(Root.panel,new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y));
  float width=tooltip.resolvedStyle.width>0?tooltip.resolvedStyle.width:520;
  tooltip.style.left=Mathf.Clamp(point.x+20,12,Mathf.Max(12,Root.worldBound.width-width-16));tooltip.style.top=Mathf.Clamp(point.y+24,12,Mathf.Max(12,Root.worldBound.height-(float.IsNaN(tooltip.resolvedStyle.height)?200:tooltip.resolvedStyle.height)-16));
 }
 public void Visible(string name,bool visible){var e=Q(name);if(e!=null)e.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;}
 public void SetManagement(bool active){
  if(ManagementMode!=active){Remember("workspace");regionKeys.Remove("workspace");}
  ManagementMode=active;Visible("civic-management-body",active);Visible("civic-workspace-scroll",!active);
 }
 VisualElement RegionBody(string region)=>Q(region=="workspace"&&ManagementMode?"civic-management-body":"civic-"+region+"-body");
 public bool TextFocused {
  get {var focused=Root?.panel?.focusController?.focusedElement as VisualElement;return focused!=null&&(focused is TextField||focused.GetFirstAncestorOfType<TextField>()!=null);}
 }
 public bool ReleaseText(){if(!TextFocused)return false;(Root.panel.focusController.focusedElement as VisualElement)?.Blur();return true;}
 public bool OwnsPointer(Vector2 point){
  if(Root?.panel==null)return false;
  var pos=RuntimePanelUtils.ScreenToPanel(Root.panel,new Vector2(point.x,Screen.height-point.y));
  var picked=Root.panel.Pick(pos);
  return picked!=null&&picked!=Root&&picked!=Q("civic-stage");
 }
 public void Remember(string region){
  if(!regionKeys.TryGetValue(region,out var key))return;
  var body=RegionBody(region);if(body==null)return;
  var scroll=Root.Q<ScrollView>("civic-"+region+"-scroll");if(scroll!=null)positions[key]=scroll.scrollOffset;
  VisitFolds(body,key,(fold,path)=>folds[path]=fold.value);
 }
 // Lazy source records create their children when expanded. Restore parents
 // first, then discover their current children; a flat query misses those nodes.
 static List<Foldout> DirectFolds(VisualElement parent){
  var result=new List<Foldout>();foreach(var child in parent.Children()){if(child is Foldout fold)result.Add(fold);else result.AddRange(DirectFolds(child));}return result;
 }
 static void VisitFolds(VisualElement parent,string path,Action<Foldout,string> visit){
  int i=0;foreach(var fold in DirectFolds(parent)){var childPath=path+"/"+(i++)+"/"+fold.text;visit(fold,childPath);VisitFolds(fold.contentContainer,childPath,visit);}
 }
 public void Render(string region,string key,Action<VisualElement,VisualElement> render){
  Remember(region);regionKeys[region]=key;
  var body=RegionBody(region);var actions=Q("civic-"+region+"-actions");body.Clear();actions.Clear();render(body,actions);
  actions.style.display=actions.childCount==0?DisplayStyle.None:DisplayStyle.Flex;
  VisitFolds(body,key,(fold,path)=>{if(folds.TryGetValue(path,out var value))CivicUI.RestoreFold(fold,value);});
  var scroll=Root.Q<ScrollView>("civic-"+region+"-scroll");var pos=positions.TryGetValue(key,out var saved)?saved:Vector2.zero;
  if(scroll!=null){scroll.scrollOffset=pos;scroll.schedule.Execute(()=>{if(regionKeys.TryGetValue(region,out var current)&&current==key)scroll.scrollOffset=pos;});}
  ApplyText(body);ApplyText(actions);
 }
 public void ApplyDisplay(float ui,float text,bool highContrast){
  uiScale=ui;textScale=text;if(settings!=null)settings.scale=ui;
  if(Root==null)return;
  Root.EnableInClassList("c-high-contrast",highContrast);ApplyText(Root);
  float top=112*text;Q("civic-top").style.minHeight=top;Q("civic-top").style.height=top;
  foreach(var name in new[]{"nav","workspace","guide","drawer","map-return"})Q("civic-"+name).style.top=name=="workspace"&&Q("civic-workspace").ClassListContains("is-wide")?16:top+16;
  float railWidth=194+152*(text-1);Q("civic-nav").style.width=railWidth;Q("civic-workspace").style.left=Q("civic-workspace").ClassListContains("is-wide")?16:railWidth+32;Q("civic-map-return").style.left=railWidth+32;
  Root.schedule.Execute(LayoutDock);
  float width=2560/ui;float layersWidth=width-780-440;
  Q("civic-layers").EnableInClassList("c-layers-two-rows",layersWidth<850);
 }
 void LayoutDock(){
  if(Root==null||Q("civic-nav")==null)return;float bottom=Q("civic-nav").worldBound.yMax-Root.worldBound.yMin;float height=Root.worldBound.height;
  if(float.IsNaN(bottom)||height<=0)return;Q("civic-tile").style.height=Math.Min(680,Math.Max(180,height-bottom-32));
  var clock=Q("civic-clock");if(clock.resolvedStyle.height>0)Q("civic-notices").style.bottom=clock.resolvedStyle.height+28;
 }
 public void ApplyText(VisualElement element){
  element.Query<TextElement>().ForEach(e=>{
   float size=e.ClassListContains("c-hero")?34:e.ClassListContains("c-number")?30:e.ClassListContains("c-heading")?24:e.ClassListContains("c-small")?18:e.ClassListContains("c-micro")||e.ClassListContains("c-nav-group")?16:22;
   if(e.GetFirstAncestorOfType<Foldout>()!=null&&e.ClassListContains("unity-toggle__text"))size=20;
   if(e.GetFirstAncestorOfType<TextField>()!=null)size=22;
   for(var p=e.parent;p!=null;p=p.parent)if(p.ClassListContains("c-top-capacity")){size=16;break;}
   e.style.fontSize=size*textScale;
  });
 }
 public void ResetReadingPositions(){positions.Clear();folds.Clear();regionKeys.Clear();}
 void OnDestroy(){if(document!=null)Destroy(document.gameObject);if(settings!=null)Destroy(settings);}
}
}
