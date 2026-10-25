using UnityEngine;
using UnityEngine.UI;
namespace MET.City {
// Kept for callers that still test this component while the desk pages migrate.
public sealed class CityDeskTone : MonoBehaviour { public bool Light; }
public sealed partial class CityInterface {
 static readonly Color HudPanel = new Color(.078f,.141f,.153f,.99f);
 static readonly Color HudRaised = new Color(.11f,.20f,.19f,.99f);
 static readonly Color HudCyan = new Color(.32f,.44f,.38f,.55f);
 static readonly Color HudText = new Color(.89f,.92f,.90f);
 static readonly Color HudMuted = new Color(.62f,.73f,.68f);
 static readonly Color HudGold = new Color(.87f,.74f,.48f);

 void DressSurface(RectTransform node,string name,Color requested){
  var image=node.GetComponent<Image>();
  if(image==null)return;
  // The modal shield still needs its own dimming opacity. Scroll bars need no frame.
  if(name.Contains("Shield")){image.sprite=null;image.type=Image.Type.Simple;image.color=requested;return;}
  bool button=name.StartsWith("Button ");
  bool scroll=name.Contains("scroll ");
  bool panel=!button&&!scroll;
  image.sprite=null;image.type=Image.Type.Simple;
  image.color=scroll||button?requested:name=="L3 City Side Panel"?HudPanel:HudRaised;
  image.raycastTarget=true;
  var tone=node.GetComponent<CityDeskTone>()??node.gameObject.AddComponent<CityDeskTone>();
  tone.Light=false;
  if(scroll)return;
  // A separate non-interactive mesh keeps the Image available as a Button target.
  var edge=Node("HUD hairline",node);Stretch(edge);
  edge.SetAsFirstSibling();
  edge.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
  var frame=edge.gameObject.AddComponent<CityHudFrame>();
  frame.color=panel?HudCyan:new Color(.38f,.70f,.65f,.42f);
  frame.enabled=panel||requested.g>requested.r*2.5f;
  frame.raycastTarget=false;
  frame.ShowAccent=name=="L3 City Side Panel";
 }
 Color DeskText(Transform parent,Color wanted){
  // Legacy desk labels were written for light paper. Keep semantic warning colours.
  if(wanted==Muted||wanted==PaperMuted)return new Color(HudMuted.r,HudMuted.g,HudMuted.b,wanted.a);
  if(wanted==Ink||wanted==PaperInk)return new Color(HudText.r,HudText.g,HudText.b,wanted.a);
  if(wanted.r>.8f&&wanted.g<.7f)return new Color(1f,.58f,.47f,wanted.a);
  if(wanted.r>.8f&&wanted.g>.57f&&wanted.b<.6f)return new Color(HudGold.r,HudGold.g,HudGold.b,wanted.a);
  if(wanted.b>wanted.r+.13f)return new Color(.64f,.86f,.96f,wanted.a);
  if(wanted.g>wanted.r+.12f)return new Color(.57f,.87f,.72f,wanted.a);
  if(wanted.grayscale<.62f)return new Color(HudMuted.r,HudMuted.g,HudMuted.b,wanted.a);
  return new Color(HudText.r,HudText.g,HudText.b,wanted.a);
 }
 RectTransform DeskProp(Transform parent,string kind,Vector2 anchor,Vector2 position,Vector2 size){
  // Empty transform preserves receipt/slot positioning without drawing stationery.
  var node=Node("HUD legacy anchor "+kind,parent);
  var ignore=node.gameObject.AddComponent<LayoutElement>();ignore.ignoreLayout=true;
  Pin(node,anchor,new Vector2(.5f,.5f),position,size);
  return node;
 }
 void InstrumentPaper(RectTransform cell){
  var image=cell.GetComponent<Image>()??cell.gameObject.AddComponent<Image>();
  image.sprite=null;image.type=Image.Type.Simple;image.color=new Color(.025f,.065f,.078f,.38f);
  image.raycastTarget=false;
  var tone=cell.GetComponent<CityDeskTone>()??cell.gameObject.AddComponent<CityDeskTone>();tone.Light=false;
  var divider=Node("HUD cell divider",cell);Stretch(divider);
  divider.SetAsFirstSibling();divider.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
  var line=divider.gameObject.AddComponent<CityHudFrame>();
  line.color=new Color(.36f,.68f,.70f,.34f);line.raycastTarget=false;line.DividerOnly=true;
 }
}
}
