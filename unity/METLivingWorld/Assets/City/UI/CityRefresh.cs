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
  if(_mapViewing||SelectedTile==""||!S["cells"].Has(SelectedTile))return "hidden";
  return V.Arr(_cityIdentity,StyleKey(),SelectedTile,SelectedBuilding,Runtime.ViewCanEditFocused,TileVisibleKey(_tileSummary)).ToJson();
 }
 V TileVisibleKey(V value){
  if(value.IsNumber)return V.From(Math.Round(value.Double,1));
  if(value.IsArray){var array=V.Arr();foreach(var item in value)array.Add(TileVisibleKey(item));return array;}
  if(value.IsMap){var map=V.Map();foreach(var entry in value.Pairs)if(entry.Key!="tick"&&entry.Key!="local_month")map[entry.Key]=TileVisibleKey(entry.Value);return map;}
  return value;
 }
}
}
