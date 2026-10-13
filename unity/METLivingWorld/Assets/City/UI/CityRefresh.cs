using System;
using System.Linq;
using MET.Core;
namespace MET.City {
public sealed partial class CityInterface {
 string _statusKey="",_contextKey="",_contextPreviewVersion="";
 V _statusTheme=V.Map(),_contextPreview=V.Map();
 int _statusPending;
 public int StatusRebuildCount {get;private set;}
 public int ContextRebuildCount {get;private set;}
 public int PreviewEvaluationCount {get;private set;}
 public int TrackedTextCount=>_fontBases.Count;
 public V PerformanceCounters=>V.Map("status_rebuilds",StatusRebuildCount,"context_rebuilds",ContextRebuildCount,"preview_evaluations",PreviewEvaluationCount,"tracked_texts",TrackedTextCount,"planner_entries",_planner.cache.Count,"frame_limit",Runtime.EffectiveFrameLimit);
 public void ResetPresentationCounters(){StatusRebuildCount=0;ContextRebuildCount=0;PreviewEvaluationCount=0;}
 void UpdateStatusModel(){
  _feedbackView=_feedback.refresh(G,UnityEngine.Time.realtimeSinceStartup);
  _statusTheme=Themes.view(G);
  _statusPending=Bonus.pending(G).Count;
 }
 // The key contains only visible labels, state colors/actions and bar pixels.
 // Simulation tick, evidence timestamps and unrendered journal entries do not
 // require destroying controls whose visible content is unchanged.
 V MeterKey(double amount,double capacity)=>V.Arr(F(amount),F(capacity),(int)Math.Round(Math.Min(1,Math.Max(0,amount/Math.Max(.000001,capacity)))*360));
 V StyleKey()=>V.Arr(TextScale,Runtime.DisplaySettings.Get("high_contrast",false));
 void ReleaseContextPreview(){
  _contextPreview=V.Map();_contextPreviewVersion="";
  if(!_archiveOpen&&!_comparisonValid&&_planner.cache.Count>0){_planner.cache.Clear();_planner.lru.Clear();_planner.token="";}
 }
 string StatusDisplayKey(){
  var alerts=V.Arr();var summaries=_feedbackView.Get("summaries",V.Arr());
  foreach(var row in summaries.Take(2))alerts.Add(V.Arr(row["text"],row["severity"],row["building_id"],row["layer"]));
  var theme=V.Arr();if(_statusTheme.Get("id","").Text!=""){
   theme=V.Arr(_statusTheme["id"],_statusTheme["name"],_statusTheme["stage"],_statusTheme["stage_name"],_statusTheme["completed"]);
   var goal=_statusTheme["goals"].FirstOrDefault(x=>!x["met"].Bool);
   if(goal!=null)theme.Add(V.Arr(goal["label"],goal["unit"],MeterKey(goal["value"].Double,goal["target"].Double)));
  }
  var bubbles=V.Arr();if(Runtime.DisplaySettings.Get("citizen_bubbles",false).Bool)foreach(var row in _feedbackView.Get("bubbles",V.Arr()))bubbles.Add(V.Arr(row["id"],row["text"],row["building_id"],row["layer"]));
  return V.Arr(_cityIdentity,StyleKey(),alerts,summaries.Count,theme,_statusPending,bubbles).ToJson();
 }
 string ContextDisplayKey(){
  if(_mapViewing||SelectedTile==""||!S["cells"].Has(SelectedTile)){ReleaseContextPreview();return "hidden";}
  var tile=S["cells"][SelectedTile];var key=V.Arr(_cityIdentity,StyleKey(),SelectedTile,SelectedBuilding,tile["building"]);
  if(SelectedBuilding!=""&&S["projects"].Has(SelectedBuilding)){
   ReleaseContextPreview();
   var p=S["projects"][SelectedBuilding];var st=G.project_stats(SelectedBuilding);
   key.Add(V.Arr(p["def"],G.project_name(SelectedBuilding),p.Get("tier",1),p["operator_org"],p.Get("paused",false),p["status"]));
   if(p.Get("paused",false).Bool)return key.ToJson();
   var upgrading=p.Get("upgrade_job",V.Map()).Count!=0;key.Add(upgrading);
   if(p["status"].Text=="construction"||upgrading){
    var job=p[upgrading?"upgrade_job":"job"];
    key.Add(V.Arr(MeterKey(job["total"].Double-job["work"].Double,job["total"].Double),F(job["material"].Double),p.Get("bottleneck","共享施工中")));
   }else{
    double housing=p["status"].Text=="running"?Math.Max(0,st.Get("housing",0).Double*st.Get("efficiency",1).Double):0;
    bool freight=st.Get("trade_capacity",0).Double>0;
    key.Add(V.Arr(housing>0,freight,Development.choices(G,SelectedBuilding).Count!=0));
    if(housing>0)key.Add(MeterKey(S["groups"].Values.Where(x=>x["host"].Text==SelectedBuilding).Sum(x=>x["count"].Double),housing));
    else if(freight)foreach(var resource in new[]{"food","material","goods"})key.Add(V.Arr(resource,MeterKey(p["inventory"].Get(resource,0).Double,G.stock_cap_for(SelectedBuilding,resource))));
    else{
     var outputs=p.Get("outputs_last",V.Map());foreach(var output in outputs.Keys.Take(2))key.Add(V.Arr(output,F(outputs[output].Double)));
     var activity=S["metrics"].Get("utilization",V.Map()).Get(SelectedBuilding,V.Map());
     key.Add(V.Arr(MeterKey(activity.Get("utilization",p.Get("usage",0)).Double,1),activity.Get("bottleneck","")));
    }
   }
  }else{
   key.Add(V.Arr(_base,tile["traits"]));if(tile["traits"].Contains("tile.water"))return key.ToJson();
   // A changed city still gets a real one-step forecast. Its rendered labels
   // decide whether controls need rebuilding; the forecast never writes back.
   var version=_planner.version(G)+":"+_base+":"+SelectedTile;
   if(version!=_contextPreviewVersion){_contextPreview=_planner.compare(G,_base,SelectedTile);_contextPreviewVersion=version;PreviewEvaluationCount++;}
   key.Add(_contextPreview.Get("ok",false));
   if(_contextPreview.Get("ok",false).Bool){var outputs=_contextPreview["outputs"];foreach(var resource in outputs.Keys.Take(2))key.Add(V.Arr(resource,F(outputs[resource].Double)));double harmed=_contextPreview["harmed"].Double;key.Add(V.Arr(harmed>0,F(harmed,0)));}
   var def=G.c.projects[_base];key.Add(V.Arr(F(def["cost"].Double,0),F(def["material"].Double,0),F(def["construction_required"].Double)));
  }
  return key.ToJson();
 }
}
}
