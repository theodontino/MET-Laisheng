using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
public sealed partial class CityInterface {
 // Construction owns a modal surface and scroll state; archive pages never lend their controls.
 GameObject _constructionShield;
 RectTransform _constructionWindow,_constructionHeader,_constructionBody,_constructionFooter,_constructionViewport,_constructionTrack;
 ScrollRect _constructionScroll;
 Text _constructionTitle,_constructionSubtitle;
 readonly Planning _constructionPlanner=new Planning();
 readonly HashSet<string> _constructionFolds=new HashSet<string>();
 bool _constructionOpen,_constructionSubmitting;
 string _constructionMode="",_constructionId="",_constructionTile="",_constructionKey="",_constructionOffer="",_constructionUpgrade="";
 V _constructionBaseline=V.Map(),_constructionProposal=V.Map();
 public bool ConstructionWindowOpen=>_constructionOpen;
 internal GameObject ConstructionWindowShield=>_constructionShield;
 internal RectTransform ConstructionWindowRoot=>_constructionWindow;

 public void OpenConstructionWindow(string baseId,string tile){
  if(!G.c.projects.Has(baseId)||!S["cells"].Has(tile))return;
  Runtime.Flush();
  var draft=S["draft"];
  // Reopening an unchanged draft does not call the tender generator or consume RNG.
  if(draft.Get("kind","").Text!="development"||draft.Get("base","").Text!=baseId||draft.Get("tile","").Text!=tile||draft.Get("offers",V.Arr()).Count==0){
   var error=Runtime.Execute(g=>g.create_tender(V.Arr(tile),baseId));
   if(error!="")return;
  }
  _base=baseId;BeginConstructionWindow("build",baseId,tile);
 }
 public void OpenUpgradeWindow(string id){
  if(!S["projects"].Has(id))return;
  Runtime.Flush();BeginConstructionWindow("upgrade",id,S["projects"][id]["tile"].Text);
 }
 void BeginConstructionWindow(string mode,string id,string tile){
  CancelDeskTipsForPanel();DismissMapInspection();EndConstructionIntent();
  BuildConstructionWindow();
  var key=mode+":"+id+":"+tile;
  if(key!=_constructionKey){_constructionFolds.Clear();_constructionOffer="";_constructionUpgrade="";_constructionScroll.verticalNormalizedPosition=1;}
  _constructionMode=mode;_constructionId=id;_constructionTile=tile;_constructionKey=key;
  SelectedTile=tile;SelectedBuilding=S["cells"][tile].Get("building","").Text;
  _constructionOpen=true;_constructionShield.SetActive(true);_constructionShield.transform.SetAsLastSibling();
  Runtime.SetDecisionOpen(true);RefreshConstructionWindow();RefreshModalOwnership(true);
  SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);
 }
 public void CloseConstructionWindow(){
  if(!_constructionOpen)return;
  CancelDeskTipsForPanel();_constructionOpen=false;_constructionSubmitting=false;
  _constructionScroll.StopMovement();_constructionShield.SetActive(false);
  // The retained draft is deliberately untouched: close / Esc never charges or redraws offers.
  if(S["cells"].Has(_constructionTile)){SelectedTile=_constructionTile;SelectedBuilding=S["cells"][_constructionTile].Get("building","").Text;}
  Runtime.SetDecisionOpen(_archiveOpen);RefreshContext();RefreshModalOwnership(true);
  SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);
 }
 void BuildConstructionWindow(){
  if(_constructionWindow!=null)return;
  _constructionShield=Surface("Construction Window Shield",_root,new Color(.005f,.012f,.016f,.69f)).gameObject;
  Stretch((RectTransform)_constructionShield.transform);
  _constructionWindow=Surface("Construction Window",_constructionShield.transform,Panel);
  _constructionTitle=Label(_constructionWindow,"建造方案",27,Ink);_constructionTitle.name="Construction Window Title";
  _constructionSubtitle=Label(_constructionWindow,"",15,Muted);
  var close=Button(_constructionWindow,"×",CloseConstructionWindow);close.name="Construction Window Close";
  Pin((RectTransform)close.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-18,-16),new Vector2(42,42));
  _constructionHeader=Column(_constructionWindow,"Construction Window Header",3,false);
  _constructionHeader.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);
  _constructionFooter=Column(_constructionWindow,"Construction Window Footer",4,false);
  _constructionFooter.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,6,0);
  _constructionViewport=Surface("Construction Window Viewport",_constructionWindow,new Color(.01f,.025f,.03f,.15f));
  _constructionViewport.gameObject.AddComponent<RectMask2D>();
  _constructionScroll=_constructionViewport.gameObject.AddComponent<ScrollRect>();_constructionScroll.horizontal=false;
  _constructionScroll.scrollSensitivity=48;_constructionScroll.movementType=ScrollRect.MovementType.Clamped;_constructionScroll.viewport=_constructionViewport;
  _constructionBody=Column(_constructionViewport,"Construction Window Body",12);_constructionBody.anchorMin=new Vector2(0,1);_constructionBody.anchorMax=new Vector2(1,1);_constructionBody.pivot=new Vector2(.5f,1);_constructionBody.sizeDelta=new Vector2(-12,0);
  _constructionBody.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(12,12,8,14);_constructionScroll.content=_constructionBody;
  _constructionTrack=Surface("Construction Window Scroll Track",_constructionWindow,new Color(.10f,.17f,.19f));
  var thumb=Surface("Construction Window Scroll Thumb",_constructionTrack,new Color(.26f,.59f,.55f));Stretch(thumb);
  var bar=_constructionTrack.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=thumb;bar.targetGraphic=thumb.GetComponent<Image>();
  _constructionScroll.verticalScrollbar=bar;_constructionScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
  RefreshConstructionWindowLayout();_constructionShield.SetActive(false);
 }
 public void RefreshConstructionWindowLayout(){
  if(_constructionWindow==null)return;
  Pin(_constructionWindow,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1280,760));
  Pin(_constructionTitle.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-16),new Vector2(1180,40*TextScale));
  Pin(_constructionSubtitle.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-18-40*TextScale),new Vector2(1180,25*TextScale));
  var headerTop=25+65*TextScale;var headerHeight=33*TextScale;var footerHeight=124*TextScale;
  Pin(_constructionHeader,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-headerTop),new Vector2(1232,headerHeight));
  Pin(_constructionFooter,new Vector2(0,0),new Vector2(0,0),new Vector2(24,14),new Vector2(1232,footerHeight));
  float top=headerTop+headerHeight+8,bottom=footerHeight+26;
  Stretch(_constructionViewport,18,top,30,bottom);
  _constructionTrack.anchorMin=new Vector2(1,0);_constructionTrack.anchorMax=new Vector2(1,1);_constructionTrack.pivot=new Vector2(1,.5f);_constructionTrack.offsetMin=new Vector2(-22,bottom);_constructionTrack.offsetMax=new Vector2(-16,-top);
 }
 public void RefreshConstructionWindow(){
  if(!_constructionOpen||_constructionSubmitting)return;
  float scroll=_constructionScroll.verticalNormalizedPosition;
  Clear(_constructionHeader);Clear(_constructionBody);Clear(_constructionFooter);RefreshConstructionWindowLayout();
  if(_constructionMode=="build")RenderConstructionOffers();else RenderConstructionUpgrades();
  Canvas.ForceUpdateCanvases();_constructionScroll.StopMovement();_constructionScroll.verticalNormalizedPosition=scroll;
  RefreshModalOwnership();
 }
 RectTransform[] ConstructionColumns(Transform parent,string name,int count){
  var row=Row(parent,name,0);var layout=row.GetComponent<HorizontalLayoutGroup>();layout.childForceExpandHeight=true;
  var result=new RectTransform[count];
  for(int i=0;i<count;i++){
   var outer=Surface(name+" Candidate "+i,row,Raised);var element=outer.gameObject.AddComponent<LayoutElement>();element.minWidth=0;element.preferredWidth=0;element.flexibleWidth=1;
   var group=outer.gameObject.AddComponent<VerticalLayoutGroup>();group.childControlWidth=group.childControlHeight=true;group.childForceExpandHeight=false;
   result[i]=Column(outer,name+" Content "+i,5);
  }
  return result;
 }
 void ConstructionFold(Transform parent,string key,string caption,Action<RectTransform> render){
  var box=Column(parent,"Construction Disclosure "+key,4);box.GetComponent<VerticalLayoutGroup>().padding=new RectOffset();
  var content=Column(box,"Construction Disclosure Body",6);content.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(8,8,8,8);
  bool open=_constructionFolds.Contains(key),rendered=false;content.gameObject.SetActive(open);
  if(open){render(content);rendered=true;}
  Button toggle=null;
  toggle=Button(box,caption+(open?" ⌄":" ›"),()=>{
   open=!open;if(open&&!rendered){render(content);rendered=true;}content.gameObject.SetActive(open);
   if(open)_constructionFolds.Add(key);else _constructionFolds.Remove(key);
   toggle.GetComponentInChildren<Text>().text=caption+(open?" ⌄":" ›");RefreshModalOwnership();
  });toggle.transform.SetAsFirstSibling();toggle.GetComponentInChildren<Text>().alignment=TextAnchor.MiddleLeft;
 }
 void ConstructionCosts(double cash,double material,double cp,string blocked,Action submit,string action){
  double after=S["currency"].Double-cash;
  var cost=Label(_constructionFooter,"即付 "+F(cash,0)+" 通货  ·  支付后 "+F(after,0)+" 通货  ·  施工 "+F(material,0)+" 材料 / "+F(cp)+" CP",17,after>=0?Ink:Gold,30*TextScale);cost.name="Construction Window Costs";
  var reason=Label(_constructionFooter,blocked!=""?blocked:"材料随施工消耗；与全城工程共享施工能力。效果是假定完工后的同一步预测。",14,blocked!=""?Gold:Muted,27*TextScale);reason.name="Construction Window Blocked Reason";
  var row=Row(_constructionFooter,"Construction Window Actions",46*TextScale);row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;
  var cancel=Button(row,"取消",CloseConstructionWindow);cancel.name="Construction Window Cancel";Width((RectTransform)cancel.transform,170);
  var start=Button(row,action,submit,true,blocked!="");start.name="Construction Window Start";var element=start.GetComponent<LayoutElement>();element.minWidth=0;element.preferredWidth=0;element.flexibleWidth=1;
 }
 void ConstructionSucceeded(string message){
  var tile=_constructionTile;CloseConstructionWindow();if(_archiveOpen)CloseArchive();
  SelectedTile=tile;SelectedBuilding=S["cells"].Get(tile,V.Map()).Get("building","").Text;
  RefreshContext();SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);ShowMessage(message);
 }
}
}
