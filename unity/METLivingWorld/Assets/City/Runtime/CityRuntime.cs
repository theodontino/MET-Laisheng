using System;
using System.Diagnostics;
using System.IO;
using MET.Core;
using UnityEngine;
namespace MET.City {
/// <summary>Native active-time owner. UTC records absence; only monotonic eligible intervals run gameplay.</summary>
[DefaultExecutionOrder(-200)]
public sealed class CityRuntime : MonoBehaviour {
 public Game Game {get;private set;}
 public V Snapshot=>_snapshot.Duplicate(true);
 public event Action<V> Committed;
 public event Action<string> Message;
 public string StorageRoot {get;private set;}
 public string SavePath=>Store.path(StorageRoot);
 public bool DecisionOpen {get;private set;}
 public bool Ready=>Game!=null;
 public bool CanRun=>Ready&&_focused&&!DecisionOpen&&!_closing&&Game.s.Get("phase","").Text=="play"&&!Game.s.Get("settings",V.Map()).Get("user_paused",true).Bool;
 public string StartupMessage {get;private set;}="";
 V _snapshot=V.Map();long _origin;double _pending,_saveElapsed;bool _focused=true,_closing,_saveEnabled=true;
 static double Utc()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000.0;
 static long Monotonic()=>Stopwatch.GetTimestamp();
 void Awake() {
  Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
  Screen.SetResolution(1920,1080,FullScreenMode.FullScreenWindow);Application.runInBackground=false;
  StorageRoot=Environment.GetEnvironmentVariable("MET_UNITY_SAVE_ROOT");if(string.IsNullOrWhiteSpace(StorageRoot))StorageRoot=Application.persistentDataPath;
  var catalog=new Catalog(Path.Combine(Application.streamingAssetsPath,"MetContent"));Game=new Game(catalog,StorageRoot);_origin=Monotonic();
  if(File.Exists(SavePath)||File.Exists(SavePath+".bak")){var loaded=Store.read(SavePath);if(loaded["ok"].Bool){var result=Game.migrate_state(loaded["state"]);if(result["ok"].Bool){Clock.resume(Game,Utc());StartupMessage="城市已载入。时间恢复常速并暂停；准备好后继续。";}else{_saveEnabled=false;StartupMessage=result["error"].Text+" 原存档保留，自动保存已停止。";Game.new_game(DateTime.UtcNow.Ticks%2147483000,Utc());}}else {_saveEnabled=false;StartupMessage=loaded["error"].Text+" 原存档保留，自动保存已停止。";Game.new_game(DateTime.UtcNow.Ticks%2147483000,Utc());}}
  else Game.new_game(DateTime.UtcNow.Ticks%2147483000,Utc());Publish();
 }
 void Start(){Publish();}
 void Capture(){var now=Monotonic();var seconds=Math.Max(0,(now-_origin)/(double)Stopwatch.Frequency);if(CanRun)_pending+=seconds;_origin=now;}
 void Update(){if(!Ready)return;Capture();if(CanRun&&_pending>=.5)Flush();_saveElapsed+=UnityEngine.Time.unscaledDeltaTime;if(_saveElapsed>=5){_saveElapsed=0;SaveInternal();}}
 public void Flush(){if(!Ready)return;Capture();var seconds=_pending;_pending=0;if(seconds>0&&CanRun){var result=Game.advance_active(seconds);if(result.Get("reason","").Text!=""&&Game.last_error!="")Notify(result["reason"].Text);Publish();}}
 public string Execute(Func<Game,string> command){Flush();var error=command(Game)??"";Publish();if(error!="")Notify(error);else SaveInternal();return error;}
 public bool PendingDecisions {get {if(!Ready)return false;if(Bonus.pending(Game).Count!=0)return true;return Themes.view(Game)["events"].Items.Exists(evt=>evt.Get("status","").Text=="offered");}}
 public bool MandatoryPending {get {if(!Ready||Game.s["phase"].Text!="play")return false;var reason=Game.s["beta_time"].Get("pause_reason","").Text;return reason!=""&&!V.Arr("manual","resume","offline","background","返回城市；准备好后继续").Contains(reason);}}
 public void SetDecisionOpen(bool open){if(open==DecisionOpen)return;Flush();DecisionOpen=open;_pending=0;_origin=Monotonic();}
 public string SetMode(string mode){Flush();if(MandatoryPending){var message="请先处理本次年度结算、已保存奖励与待选事件。";Notify(message);return message;}var error=Clock.set_mode(Game,mode);if(error==""){Game.s["settings"]["user_paused"]=false;Game.s["beta_time"]["pause_reason"]="";}else Notify(error);_pending=0;_origin=Monotonic();Publish();SaveInternal();return error;}
 public void SetPaused(bool paused){Flush();if(!paused&&MandatoryPending){Notify("请先在年度 / 尾声页处理结算、奖励与待选事件，再继续经营。");return;}Game.s["settings"]["user_paused"]=paused;if(!MandatoryPending)Game.s["beta_time"]["pause_reason"]=paused?"manual":"";_pending=0;_origin=Monotonic();Publish();SaveInternal();}
 public void ResumeNormal(){Flush();if(PendingDecisions){Notify("请先处理本次已保存的奖励和待选事件。");return;}Clock.set_mode(Game,"normal");Cycles.acknowledge(Game);Game.s["settings"]["user_paused"]=false;Game.s["beta_time"]["pause_reason"]="";_pending=0;_origin=Monotonic();Publish();SaveInternal();}
 public void NewCity(long seed){Flush();_saveEnabled=true;Game.new_game(seed,Utc());_pending=0;_origin=Monotonic();Publish();SaveInternal();}
 public string Load(string path=null){Flush();var loaded=Store.read(path??SavePath);if(!loaded["ok"].Bool){Notify(loaded["error"].Text);return loaded["error"].Text;}var result=Game.migrate_state(loaded["state"]);if(!result["ok"].Bool){Notify(result["error"].Text);return result["error"].Text;}Clock.resume(Game,Utc());_saveEnabled=true;_pending=0;_origin=Monotonic();Publish();SaveInternal();Notify("当前 Unity 城市已载入；常速暂停，准备好后继续。");return "";}
 public string Save(){Flush();var error=SaveInternal();Notify(error==""?"城市已保存。":error);return error;}
 string SaveInternal(){if(!_saveEnabled||!Ready)return "原存档保留；请明确开始新城或载入有效城市后保存。";var now=Utc();Game.s["wall_mark"]=now;Game.s["beta_time"]["last_seen_utc"]=now;return Store.write(Game.snapshot(),SavePath);}
 public string Export(string path){Flush();var error=Store.write(Game.snapshot(),path);Notify(error==""?"城市备份已导出："+path:error);return error;}
 public V DisplaySettings {get;private set;}=V.Map("ui_scale",1.0,"text_scale",1.0,"citizen_bubbles",false,"reduced_motion",false,"high_contrast",false,"all_links",false,"frame_limit",60);
 public static int ResolveFrameLimit(long requested)=>requested==30||requested==60||requested==120?(int)requested:60;
 public int EffectiveFrameLimit=>ResolveFrameLimit(DisplaySettings.Get("frame_limit",60).Long);
 public void LoadDisplaySettings(){var path=Path.Combine(StorageRoot,"display_settings.json");if(!File.Exists(path))return;try{var data=V.Parse(File.ReadAllText(path));if(data.IsMap)DisplaySettings.Merge(data,true);}catch(Newtonsoft.Json.JsonException){Notify("显示设置无法读取，已使用默认值。");}}
 public void SetDisplaySetting(string key,V value){DisplaySettings[key]=value;Directory.CreateDirectory(StorageRoot);File.WriteAllText(Path.Combine(StorageRoot,"display_settings.json"),DisplaySettings.ToJson(true));}
 public void Notify(string text){if(text!="")Message?.Invoke(text);}
 void Publish(){_snapshot=Game.snapshot();Committed?.Invoke(_snapshot.Duplicate(true));}
 void OnApplicationFocus(bool focus){if(!Ready||_closing||_focused==focus)return;if(!focus){Flush();_focused=false;_pending=0;Game.s["wall_mark"]=Utc();SaveInternal();Publish();}else {_focused=true;_pending=0;_origin=Monotonic();Clock.resume(Game,Utc());Publish();SaveInternal();}}
 void OnApplicationPause(bool paused){if(paused)OnApplicationFocus(false);else if(!_focused)OnApplicationFocus(true);}
 void OnApplicationQuit(){if(!Ready)return;Flush();_closing=true;SaveInternal();}
}
}
