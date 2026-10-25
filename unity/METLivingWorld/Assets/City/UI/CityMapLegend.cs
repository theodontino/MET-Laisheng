using System;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;
namespace MET.City {
public sealed partial class CityInterface {
 RectTransform _legendScale;
 string _legendScaleKey="";
 void RefreshMapLegend(){
  if(_planningLegend==null||_legend==null)return;
  var reading=_inspectionWorld!=null?_inspectionWorld.Legend:V.Map();
  var max=Layer=="employment"?1:CityWorld.AnalysisMaximum(Layer);
  var reach=reading.Get("employment_reach_max",1).Double;
  var metric=reading.Get("delta_metric","pollution").Text;
  var key=Layer+"/"+metric+"/"+F(reach)+"/"+TextScale;
  if(_legendScale==null){_legendScale=Column(_planningLegend,"Actual map color scale",2,false);_legendScale.GetComponent<VerticalLayoutGroup>().padding=new RectOffset();}
  _legendScale.gameObject.SetActive(Layer!="terrain");
  if(Layer=="terrain"){Stretch(_legend.rectTransform,8,1,8,1);return;}
  Pin(_legend.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(8,-2),new Vector2(_planningLegend.rect.width-16,26*TextScale));
  Stretch(_legendScale,12,29*TextScale,12,5);
  if(key==_legendScaleKey)return;_legendScaleKey=key;Clear(_legendScale);
  bool signed=Layer=="energy"||Layer=="delta";
  var ramp=Row(_legendScale,"Renderer matched scale",9);ramp.GetComponent<HorizontalLayoutGroup>().spacing=0;Height(ramp,9);
  for(int i=0;i<21;i++){double value=max*(signed?(i/10.0-1):i/20.0);var cell=Node("Scale swatch",ramp);var image=cell.gameObject.AddComponent<Image>();image.color=CityWorld.AnalysisTint(Layer,value,max,true,metric);image.raycastTarget=false;var size=cell.gameObject.AddComponent<LayoutElement>();size.minWidth=0;size.preferredWidth=0;size.flexibleWidth=1;Height(cell,9);}
  string text;
  if(Layer=="employment")text="居民就业 0–100%（青） · 无居民：可达度 0–"+F(reach)+"（蓝，非比例）";
  else if(Layer=="medical"||Layer=="education"||Layer=="entertainment"||Layer=="commercial")text="0% → 100% · 有居民：实际满足；空地：服务可达";
  else if(Layer=="energy")text="−"+F(max,0)+" 缺口 ← 0 → +"+F(max,0)+" 余量 · 本地供需能力";
  else if(Layer=="delta")text="−"+F(max)+" ← 0 → +"+F(max)+" · "+reading.Get("delta_polarity","同一步变化").Text;
  else text="0 → "+F(max,0)+(Layer=="pollution"?" 污染场强":Layer=="heat"?" 余热场强":" 库存单位")+" · 超出色带范围仍显示实际数值";
  var label=Label(_legendScale,text,11,Ink,26*TextScale);label.alignment=TextAnchor.MiddleCenter;
 }
}
}
