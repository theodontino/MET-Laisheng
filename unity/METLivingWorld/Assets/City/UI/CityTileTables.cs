using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using MET.Core;
namespace MET.City {
public sealed partial class CityInterface {
 readonly Color _tileBand=new Color(.035f,.075f,.09f,.92f);
 readonly Color _tileRule=new Color(.25f,.48f,.50f,.35f);
 RectTransform TileBlock(Transform parent,string name,float height){
  var node=Node(name,parent);Height(node,height*TextScale);return node;
 }
 RectTransform TileTint(Transform parent,string name,Color color){
  var node=Node(name,parent);var image=node.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return node;
 }
 Text TileText(Transform parent,string value,int size=14,Color? color=null,TextAnchor alignment=TextAnchor.MiddleLeft){
  var text=Label(parent,value,size,color??Ink,22*TextScale);text.alignment=alignment;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
 }
 void TileBounds(RectTransform r,float from,float to,float top=0,float bottom=0){
  r.anchorMin=new Vector2(from,0);r.anchorMax=new Vector2(to,1);r.offsetMin=new Vector2(6,bottom);r.offsetMax=new Vector2(-6,-top);
 }
 void TileHeading(Transform parent,string title,string note="",Action action=null){
  var node=TileBlock(parent,"Tile section "+title,25);var line=TileTint(node,"Section divider",_tileRule);Stretch(line,0,24*TextScale,0,0);
  var caption=TileText(node,title,16,Blue);TileBounds(caption.rectTransform,0,.55f);
  if(action!=null){var button=Button(node,note,action);Pin((RectTransform)button.transform,new Vector2(1,.5f),new Vector2(1,.5f),Vector2.zero,new Vector2(82,24*TextScale));HudFont(button.GetComponentInChildren<Text>(),13);Stretch(button.GetComponentInChildren<Text>().rectTransform,4,0,4,0);}
  else {var hint=TileText(node,note,13,Muted,TextAnchor.MiddleRight);TileBounds(hint.rectTransform,.55f,1);}
 }
 // All rows share the same column boundaries. The expense group spans three columns.
 static readonly float[] TileResourceColumns={0,.22f,.37f,.52f,.67f,.82f,1};
 void TileResourceHeader(Transform parent){
  var header=TileBlock(parent,"Resource matrix grouped header",42);var band=TileTint(header,"Header band",_tileBand);Stretch(band);
  var labels=new[]{"资源","产出","生产","居民","施工","净产消"};
  for(int i=0;i<6;i++){
   var t=TileText(header,labels[i],13,Muted,i==0?TextAnchor.MiddleLeft:TextAnchor.MiddleRight);TileBounds(t.rectTransform,TileResourceColumns[i],TileResourceColumns[i+1],i>=2&&i<=4?21*TextScale:0);
  }
  var expense=TileText(header,"消 耗",13,Muted,TextAnchor.MiddleCenter);expense.rectTransform.anchorMin=new Vector2(.37f,.5f);expense.rectTransform.anchorMax=new Vector2(.82f,1);expense.rectTransform.offsetMin=Vector2.zero;expense.rectTransform.offsetMax=Vector2.zero;
  var rule=TileTint(header,"Expense divider",_tileRule);rule.anchorMin=new Vector2(.37f,.5f);rule.anchorMax=new Vector2(.82f,.5f);rule.sizeDelta=new Vector2(-10,1);
 }
 void TileMatrixRow(Transform parent,string[] values,float[] columns,bool stripe=false,Color? lastColor=null){
  var row=TileBlock(parent,"Matrix row "+values[0],27);if(stripe){var band=TileTint(row,"Alternate row",_tileBand);Stretch(band);}
  for(int i=0;i<values.Length;i++){
   var text=TileText(row,values[i],14,i==values.Length-1?lastColor??Ink:i==0?Muted:Ink,i==0?TextAnchor.MiddleLeft:TextAnchor.MiddleRight);
   TileBounds(text.rectTransform,columns[i],columns[i+1]);
  }
  var line=TileTint(row,"Row rule",_tileRule);line.anchorMin=Vector2.zero;line.anchorMax=new Vector2(1,0);line.sizeDelta=new Vector2(0,1);
 }
 string TileNumber(double n)=>Math.Abs(n)<.00005?"—":Math.Abs(n)>=10000?(n/10000).ToString("0.#")+"万":F(n);
 string TileSigned(double n)=>Math.Abs(n)<.00005?"—":(n>0?"+":"")+TileNumber(n);
 RectTransform TileGridCell(RectTransform row,string name,int index,int count){
  var cell=TileTint(row,name,_tileBand);TileBounds(cell,(float)index/count,(float)(index+1)/count,1,1);return cell;
 }
 void TileStatCell(Transform parent,string title,string value,Color? color=null){
  var caption=TileText(parent,title,13,Muted);caption.rectTransform.anchorMin=new Vector2(0,.6f);caption.rectTransform.anchorMax=Vector2.one;caption.rectTransform.offsetMin=new Vector2(6,0);caption.rectTransform.offsetMax=new Vector2(-6,0);
  var amount=TileText(parent,value,17,color??Ink);amount.lineSpacing=1;amount.verticalOverflow=VerticalWrapMode.Overflow;amount.rectTransform.anchorMin=Vector2.zero;amount.rectTransform.anchorMax=new Vector2(1,.6f);amount.rectTransform.offsetMin=new Vector2(6,1);amount.rectTransform.offsetMax=new Vector2(-6,0);
 }
 void TileFieldCell(RectTransform row,V field,int index,int count,string label,bool service){
  var cell=TileGridCell(row,"Field "+field["layer"].Text,index,count);
  var icon=HudIcon(cell,field["layer"].Text,Blue,19);Pin(icon,new Vector2(0,1),new Vector2(0,1),new Vector2(5,-5),new Vector2(18,18));
  var name=TileText(cell,label,13,Muted);name.rectTransform.anchorMin=new Vector2(0,.35f);name.rectTransform.anchorMax=Vector2.one;name.rectTransform.offsetMin=new Vector2(29,0);name.rectTransform.offsetMax=new Vector2(-8,0);
  var available=field.Get("available",false).Bool;var text=available?(service?F(field["value"].Double*100,0)+"%":F(field["value"].Double,2)):"待结算";
  var value=TileText(cell,text,17,available&&service&&field["value"].Double<.95?Gold:Ink,TextAnchor.MiddleRight);TileBounds(value.rectTransform,.43f,1,0,13*TextScale);
  if(service){var track=TileTint(cell,"Coverage track",new Color(.2f,.3f,.32f,1));track.anchorMin=Vector2.zero;track.anchorMax=new Vector2(1,0);track.offsetMin=new Vector2(8,5);track.offsetMax=new Vector2(-8,9);var fill=TileTint(track,"Coverage",available&&field["value"].Double<.95?Gold:Green);fill.anchorMin=Vector2.zero;fill.anchorMax=new Vector2(available?Mathf.Clamp01((float)field["value"].Double):0,1);fill.offsetMin=fill.offsetMax=Vector2.zero;}
  var image=cell.GetComponent<Image>();image.raycastTarget=true;var button=cell.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>ToggleTileDisclosure("field:"+field["layer"].Text));
  AddDeskTip(cell.gameObject,label+"来源","点击展开本地来源与数值。");
 }
 void TileInlineSource(string key,Action<RectTransform> render){
  if(!_tileDisclosures.Contains(TileStateKey+"/"+key))return;
  var box=Column(_tileBody,"Expanded "+key,3);box.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(8,8,4,6);render(box);
 }
 string TileStateKey=>_cityIdentity+"/"+SelectedTile;
 void ToggleTileDisclosure(string key){var state=TileStateKey+"/"+key;if(!_tileDisclosures.Add(state))_tileDisclosures.Remove(state);_contextKey="";RefreshContext();}
}
}
