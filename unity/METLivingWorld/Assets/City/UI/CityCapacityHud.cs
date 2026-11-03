using System;
using System.Collections.Generic;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace MET.City {
public sealed class CityCapacityHover : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler {
 public Text Reading;public void OnPointerEnter(PointerEventData e){if(Reading!=null)Reading.gameObject.SetActive(true);}
 public void OnPointerExit(PointerEventData e){if(Reading!=null)Reading.gameObject.SetActive(false);}
}
public sealed partial class CityInterface {
 readonly Dictionary<string,Text> _resourceDetails=new Dictionary<string,Text>();
 readonly Dictionary<string,CityGraphic> _capacityBars=new Dictionary<string,CityGraphic>();
 RectTransform _cpReservation;
 readonly Dictionary<string,Text> _capacityReadings=new Dictionary<string,Text>();
 void BuildCapacityHud(){
  var panel=HudSurface("HUD Capacity",_planningTop);Pin(panel,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-12,0),new Vector2(304,70));
  int index=0;foreach(var key in new[]{"cp","energy"}){
   var id=key;var row=HudSurface("Capacity "+id,panel,Color.clear);Pin(row,new Vector2(0,1),new Vector2(0,1),new Vector2(5,-3-index++*33),new Vector2(294,32));
   var label=Label(row,id=="cp"?"施工 CP / 月":"电力 · 需 / 供",11,Muted);Pin(label.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(4,0),new Vector2(132,19));
   var track=Surface("Capacity track",row,new Color(.18f,.27f,.25f));Pin(track,new Vector2(0,0),new Vector2(0,0),new Vector2(4,3),new Vector2(286,6));
   var fill=Node("Capacity fill",track);Stretch(fill);var bar=fill.gameObject.AddComponent<CityGraphic>();bar.Kind="bar";bar.color=Green;bar.raycastTarget=false;_capacityBars[id]=bar;
   if(id=="cp"){_cpReservation=Surface("City project CP reservation",track,Gold);_cpReservation.GetComponent<Image>().raycastTarget=false;}
   var reading=Label(row,"",11,Ink);Pin(reading.rectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(-4,0),new Vector2(152,19));reading.alignment=TextAnchor.MiddleRight;_capacityReadings[id]=reading;
   reading.gameObject.SetActive(true);
   var button=row.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>OpenResourcePage(id));AddDeskTip(row.gameObject,id=="cp"?"施工能力":"电力供需",()=>CapacityExplanation(id));
  }
 }
 void RefreshResourceDetails(){
  bool known=S.Get("simulation_tick",0).Long>0;var civic=S.Get("civic032",V.Map());
  foreach(var entry in _resourceDetails){string text;
   if(entry.Key=="dt")text="加速预算";
   else if(entry.Key=="population")text=civic.Get("last_population_rate").IsNumber?Signed(civic["last_population_rate"].Double)+"/月":"待结算";
   else if(entry.Key=="currency")text=known?Signed(S["metrics"].Get("cashflow",0).Double)+"/月":"待结算";
   else text=known?Signed(S["metrics"].Get("flows",V.Map()).Get(entry.Key,0).Double)+"/月":"待结算";
   entry.Value.text=text;
  }
 }
 void RefreshCapacityHud(){
  var m=S.Get("metrics",V.Map());var last=S["block"].Get("construction_last",V.Map());var reservation=S["block"].Get("city_cp_last",V.Map());
  double h=last.Get("hours",0).Double,used=h>0?last.Get("cp",0).Double/h:0;
  double reserved=reservation.Get("hours",0).Double>0?reservation.Get("cp",0).Double/reservation["hours"].Double:0;
  double cp=Construction.capacity_breakdown(G)["effective"].Double;
  SetCapacity("cp",used,cp,h>0?F(used+reserved,1)+" / "+F(cp,1):"待结算");
  if(_cpReservation!=null){float start=cp>0?Mathf.Clamp01((float)(used/cp)):0,end=cp>0?Mathf.Clamp01((float)((used+reserved)/cp)):0;_cpReservation.anchorMin=new Vector2(start,0);_cpReservation.anchorMax=new Vector2(end,1);_cpReservation.offsetMin=_cpReservation.offsetMax=Vector2.zero;_cpReservation.gameObject.SetActive(end>start);}

  double supply=m.Get("energy",0).Double,need=m.Get("energy_need",0).Double;SetCapacity("energy",need,supply,S.Get("simulation_tick",0).Long>0?F(need,1)+" / "+F(supply,1):"待结算");
 }
 void SetCapacity(string id,double demand,double supply,string text){if(!_capacityBars.TryGetValue(id,out var bar))return;bar.Ratio=supply>0?(float)Math.Min(1,demand/supply):0;bar.color=demand>supply+0.0001?Red:demand>0&&demand>=supply*.85?Gold:Green;bar.SetVerticesDirty();_capacityReadings[id].text=text;}
 string CapacityExplanation(string id)=>TopbarResourceSummary(id);
}
}
