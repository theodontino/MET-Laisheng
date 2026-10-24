using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MET.City {
// Captures the recommendation that was visible when a pointer press began.
public sealed class CityGuidancePointerGuard : MonoBehaviour, IPointerDownHandler {
 public Action Capture;
 public void OnPointerDown(PointerEventData eventData){Capture?.Invoke();}
}
public sealed partial class CityInterface {
 string _pressedGuidanceKey="";
 bool _guidancePointerPressed;
 string GuidanceActionKey(V view)=>V.Arr(_cityIdentity,view.Get("id",""),view.Get("title",""),view.Get("reason",""),view.Get("action_label",""),view.Get("route",""),view.Get("layer",""),view.Get("base_id",""),view.Get("severity",0)).ToJson(false,true);
 bool GuidanceMatchesHud(V view)=>_guidanceTitle!=null&&_guidanceReason!=null&&_guidanceButton!=null&&
  _guidanceTitle.text==view.Get("title","").Text&&_guidanceReason.text==view.Get("reason","").Text&&
  _guidanceButton.GetComponentInChildren<Text>().text==view.Get("action_label","").Text+"  ›";
 void CaptureCityGuidancePointerDown(){
  _guidancePointerPressed=true;var current=CityGuidanceView();
  _pressedGuidanceKey=GuidanceMatchesHud(current)?GuidanceActionKey(current):"";
 }
 void AttachCityGuidancePointerGuard(){
  var guard=_guidanceButton.gameObject.GetComponent<CityGuidancePointerGuard>()??_guidanceButton.gameObject.AddComponent<CityGuidancePointerGuard>();
  guard.Capture=CaptureCityGuidancePointerDown;
 }
 // A detached, read-only decision for both the ambient HUD and city overview.
 V Guidance(string id,string title,string reason,string action,string route,string layer="terrain",string baseId="",int severity=0)=>
  V.Map("id",id,"title",title,"reason",reason,"action_label",action,"route",route,"layer",layer,"base_id",baseId,"severity",severity);

 public V CityGuidanceView(){
  if(G==null||S.Count==0)return Guidance("loading","正在读取城市","等待已提交的城市状态。","查看城市","tasks_home");
  if(S.Get("phase","").Text!="play")return Guidance("start","安顿这片河谷","先确定登陆地点与居民起源。","继续起源","start");
  if(!Runtime.ViewCanEditFocused)return Guidance("autonomous","当前街区只读","地区处于自治状态；永久城市管理暂未开放。可查看已保存历史。","查看只读城史","heritage","terrain","",1);
  var pending=Bonus.pending(G);var theme=Themes.view(G);
  var offered=theme.Get("events",V.Arr()).FirstOrDefault(e=>e.Get("status","").Text=="offered");
  if(Runtime.MandatoryPending){
   if(pending.Count>0)return Guidance("decision_reward","奖励等待决定","先处理最早一份已保存奖励。","选择奖励","settlement","terrain","",3);
   if(offered!=null)return Guidance("decision_event","事件等待决定",offered.Get("name","城市事件").Text+"需要选择。","处理事件","settlement","terrain","",3);
   if(S["block"].Get("cycles",V.Map()).Get("pending_summaries",V.Arr()).Count>0)return Guidance("decision_cycle","年度结算待确认","查看实际年度结算后继续。","处理结算","settlement","terrain","",3);
   return Guidance("decision","有必须处理的城市决定","查看当前结算决定后继续。","处理结算","settlement","terrain","",3);
  }
  var m=S.Get("metrics",V.Map());var measured=S.Get("simulation_tick",0).Long>0;
  // Feedback is updated on commit. Require matching evidence tick so an earlier block
  // cannot drive a recommendation after focus changes.
  var alert=_feedbackView.Get("alerts",V.Arr()).FirstOrDefault(a=>a.Get("severity",0).Long>=2&&S.Get("projects",V.Map()).Has(a.Get("building_id","").Text)&&a.Get("evidence",V.Map()).Get("tick",-1).Long==S.Get("simulation_tick",0).Long);
  if(alert!=null){var id=alert.Get("building_id","").Text;var layer=alert.Get("layer","terrain").Text;
   return Guidance("alert","城市供给需要关注",alert.Get("text","实际供给受限").Text+"；查看来源。","查看来源",S.Get("projects",V.Map()).Has(id)?"building":"resources",layer,id,3);}
  var homeless=m.Get("homeless",0).Double;
  if(homeless>.001)return Guidance("housing","有居民等待安置",F(homeless,homeless<1?1:0)+" 位居民尚无住宅；查看住区与可用地。","查看住房","population","service","",3);
  if(measured){
   var food=Progress.food_ratio(G);
   // A mild fluctuation stays in the overview. Feedback.ENTER is the existing
   // shortage entry boundary; severe committed alerts above have priority.
   if(food<Feedback.ENTER)return Guidance("food","口粮尚未覆盖所有居民","实际口粮满足 "+F(food*100,1)+"%；查看供给来源。","检查粮食来源","resources","food","",2);
   var energy=m.Get("energy_ratio",1).Double;
   if(energy<Feedback.ENTER)return Guidance("energy","全城电力供给不足","实际电力满足 "+F(energy*100,1)+"%；查看供需来源。","检查电力供需","resources","energy","",2);
  }
  var queue=Construction.queue(G);
  if(offered!=null)return Guidance("event","城市事件待选择",offered.Get("name","城市事件").Text+" 已出现；查看可选结果。","查看事件","themes","terrain","",2);
  if(pending.Count>0)return Guidance("reward","有已挣得的奖励",pending.Count+" 份方案奖励待选；先看最早一份。","选择奖励","blueprints","terrain","",2);
  if(measured&&queue.Any(j=>!j.Get("paused",false).Bool&&j.Get("material",0).Double>.001)&&G.inventory_total("material")<.001)
   return Guidance("material","材料库存已空","排队工程仍需材料；查看来源。","检查材料来源","resources","material","",2);
  if(queue.Count>0){
   var first=queue[0];var id=first.Get("id","").Text;var name=first.Get("name","工程").Text;
   if(first.Get("paused",false).Bool)return Guidance("paused_queue","工程已暂停",name+" 暂停；查看建筑决定是否恢复。","查看工程","building","terrain",id,2);
   if(Construction.capacity(G)<.001)return Guidance("capacity_queue","施工能力为零",name+" 仍在队列；查看施工来源。","查看工程","building","terrain",id,2);
   if(Runtime.WorldPaused)return Guidance("paused_time","城市已暂停",name+" 仍在队列；继续经营后才会施工。","继续经营","resume","terrain",id,1);
  }
  if(!measured)return Runtime.WorldPaused?
   Guidance("first_tick_paused","城市等待首次运转","尚无实际结算；继续经营以观察真实供给。","继续经营","resume"):
   Guidance("first_tick","城市等待首次结算","尚无实际月率；查看当前库存与供需。","查看供给","resources");
  if(Runtime.WorldPaused)return Guidance("voluntary_pause","城市已暂停","继续经营，让城市时间推进。","继续经营","resume");
  if(theme.Get("id","").Text=="")return Guidance("theme_choice","选择城市的发展方向","当前尚未选定主题。查看可选委任，再决定这片河谷如何成长。","选择发展主题","themes");
  if(!theme.Get("completed",false).Bool){
   var goal=theme.Get("goals",V.Arr()).FirstOrDefault(x=>!x.Get("met",false).Bool);
   if(goal!=null){var factor=goal.Get("unit","").Text=="%"?100:1;var comparator=goal.Get("comparison","min").Text=="max"?" ≤ ":" / ";
    return Guidance("theme_goal",theme.Get("name","城市委任").Text,goal.Get("label","阶段目标").Text+" "+F(goal.Get("value",0).Double*factor,1)+comparator+F(goal.Get("target",0).Double*factor,1)+goal.Get("unit","").Text,"查看主题目标","themes");}
   if(theme.Get("steady_required_seconds",0).Double>theme.Get("steady_seconds",0).Double)return Guidance("theme_steady","维持城市当前状态","目标已达；稳定时间还需继续累计。","查看稳定进度","themes");
   return Guidance("theme_stage","等待主题阶段记录","目标已达；查看保存的阶段进度。","查看主题进度","themes");
  }
  if(queue.Count>0){var first=queue[0];return Guidance("queue","工程列队中",first.Get("name","工程").Text+" 尚需 "+F(first.Get("work",0).Double)+" CP。","查看工程","building","terrain",first.Get("id","").Text,1);}
  return Guidance("calm","继续塑造这座城市","主题已完成；查看成果与下一处发展方向。","查看发展成果","themes");
 }

 public void RunCityGuidanceAction(){
  var current=CityGuidanceView();
  if(!_archiveOpen){
   var changed=!GuidanceMatchesHud(current)||(_guidancePointerPressed&&_pressedGuidanceKey!=GuidanceActionKey(current));
   _guidancePointerPressed=false;_pressedGuidanceKey="";
   if(changed){RefreshPlanningObjective();ShowMessage("城市状态已变化，请再确认下一步。");return;}
  }
  var route=current.Get("route","tasks_home").Text;
  if(route=="resume"){
   // Resuming is an explicit button action. Close the overview first so its
   // decision-open hold does not leave the clock apparently stuck.
   if(_archiveOpen)CloseArchive();
   if(!Runtime.MandatoryPending&&Runtime.WorldPaused)Runtime.SetPaused(false);
   return;
  }
  var layer=current.Get("layer","terrain").Text;
  if(layer!="terrain")SetLayer(layer);
  if(route=="building"){
   var id=current.Get("base_id","").Text;
   if(S.Get("projects",V.Map()).Has(id)){Navigate("building",id);return;}
   route="tasks_home";
  }
  Navigate(route);
 }
}
}
