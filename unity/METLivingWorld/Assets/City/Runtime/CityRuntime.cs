using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MET.Core;
using UnityEngine;
namespace MET.City {
/// <summary>Native active-time owner. UTC records absence; only monotonic eligible intervals run gameplay.</summary>
[DefaultExecutionOrder(-200)]
public sealed class CityRuntime : MonoBehaviour {
 public CitySession Session {get;private set;}
 public Game Game=>Session?.FocusedGame;
 public V Snapshot=>_snapshot.Duplicate(true);
 public V SessionSnapshot=>_sessionSnapshot.Duplicate(true);
 public event Action<V> Committed;
 public event Action<string> Message;
 public string StorageRoot {get;private set;}
 public string SavePath=>Path.Combine(StorageRoot,CityStore.SAVE);
 public bool DecisionOpen {get;private set;}
 public bool WildBuild=>Environment.GetCommandLineArgs().Contains("--wild-g1")||File.Exists(Path.Combine(Application.streamingAssetsPath,"WildG1.txt"));
 public bool Ready=>Session!=null;
 public bool ViewCanEditFocused=>_sessionSnapshot.Get("focused_block_id","").Text!=""&&_sessionSnapshot.Get("active_district_id","").Text!=""&&_sessionSnapshot.Get("blocks",V.Map()).Get(_sessionSnapshot["focused_block_id"].Text,V.Map()).Get("district_id","")==_sessionSnapshot["active_district_id"];
 public bool WorldPaused=>_sessionSnapshot.Get("paused",true).Bool;
 public bool CanRun=>Ready&&_focused&&!DecisionOpen&&!_closing&&_hasPlayableBlock&&!WorldPaused;
 public string StartupMessage {get;private set;}="";
 public int AutoSaveWriteCount {get;private set;}
 public int AutoSaveSkipCount {get;private set;}
 V _snapshot=V.Map(),_sessionSnapshot=V.Map();long _origin;double _pending,_saveElapsed;bool _focused=true,_closing,_saveEnabled=true,_saveDirty=true;string _lastSaveError="";
 Task<V> _advance;bool _hasPlayableBlock;
 static double Utc()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000.0;
 static long Monotonic()=>Stopwatch.GetTimestamp();
 void Awake() {
  Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
  Screen.SetResolution(2560,1440,FullScreenMode.FullScreenWindow);Application.runInBackground=false;
  StorageRoot=Environment.GetEnvironmentVariable("MET_UNITY_SAVE_ROOT");if(string.IsNullOrWhiteSpace(StorageRoot))StorageRoot=WildBuild?Path.Combine(Application.persistentDataPath,"Wild-G1"):Application.persistentDataPath;
  var catalog=new Catalog(Path.Combine(Application.streamingAssetsPath,"MetContent"));Session=new CitySession(catalog,StorageRoot);_origin=Monotonic();
  Session.New(DateTime.UtcNow.Ticks%2147483000,Utc());if(WildBuild)Session.s["wild_mode"]=true;
  if(File.Exists(SavePath)||File.Exists(SavePath+".bak")||File.Exists(SavePath+".tmp")){var loaded=CityStore.read(catalog,SavePath);var error=loaded["ok"].Bool?Session.Restore(loaded["state"]):loaded["error"].Text;if(error==""){Session.Resume(Utc());StartupMessage=(loaded.Get("from_backup",false).Bool?"主档无效，已恢复完整城市备份。":"城市已载入。")+"时间恢复常速并暂停；准备好后继续。";}else{_saveEnabled=false;StartupMessage=error+" 原存档保留，自动保存已停止。";Session.New(DateTime.UtcNow.Ticks%2147483000,Utc());if(WildBuild)Session.s["wild_mode"]=true;}}
  Publish();
 }
 void Start(){Publish(world:_sessionSnapshot,stateChanged:false);}
 void Capture(){var now=Monotonic();var seconds=Math.Max(0,(now-_origin)/(double)Stopwatch.Frequency);if(CanRun)_pending+=seconds;_origin=now;}
 void Update(){if(!Ready)return;Capture();if(_advance!=null&&_advance.IsCompleted)CompleteAdvance();if(_advance==null&&CanRun&&_pending>=1){var seconds=_pending;_pending=0;_advance=Task.Run(()=>{var r=Session.Advance(seconds);return V.Map("result",r,"world",Session.snapshot());});}_saveElapsed+=UnityEngine.Time.unscaledDeltaTime;if(_saveElapsed>=5&&_advance==null){_saveElapsed=0;if(_saveDirty){if(SaveInternal()=="")AutoSaveWriteCount++;}else AutoSaveSkipCount++;}}
 void CompleteAdvance(){var task=_advance;if(task==null)return;V delivery;try{delivery=task.GetAwaiter().GetResult();}finally{_advance=null;}var result=delivery["result"];if(result.Get("reason","").Text!="")Notify(result["reason"].Text);if(delivery["world"].Get("paused",false).Bool)_pending=0;Publish(world:delivery["world"]);}
 // The worker exclusively owns the live session during an advance. Commands,
 // persistence and lifecycle transitions join it before touching that session;
 // the map and UI continue to read the last detached committed projection.
 public void Flush(){if(!Ready)return;Capture();var seconds=_pending;_pending=0;CompleteAdvance();if(seconds>0&&CanRun){var result=Session.Advance(seconds);if(result.Get("reason","").Text!="")Notify(result["reason"].Text);Publish();}_origin=Monotonic();}
 public string Execute(Func<Game,string> command){Flush();if(!Session.CanEditFocused){var denied="这个地区正在自治。请在城市页支付回任成本后调整经营。";Notify(denied);return denied;}var before=Game.snapshot();var error=command(Game)??"";if(error=="")Session.AfterLocalCommand(before);Publish();if(error!="")Notify(error);else SaveInternal();return error;}
 public string ExecuteWorld(Func<CitySession,string> command){Flush();var error=command(Session)??"";_pending=0;_origin=Monotonic();Publish();if(error!="")Notify(error);else SaveInternal();return error;}
 public bool PendingDecisions {get {if(!Ready)return false;if(Bonus.pending(Game).Count!=0)return true;return Themes.view(Game)["events"].Items.Exists(evt=>evt.Get("status","").Text=="offered");}}
 public bool MandatoryPending {get {if(!Ready||!Session.CanEditFocused||Game.s["phase"].Text!="play")return false;var reason=Game.s["beta_time"].Get("pause_reason","").Text;return reason!=""&&!V.Arr("manual","resume","offline","background","返回城市；准备好后继续").Contains(reason);}}
 public void SetDecisionOpen(bool open){if(open==DecisionOpen)return;Flush();DecisionOpen=open;_pending=0;_origin=Monotonic();}
 public string SetMode(string mode){Flush();if(!Session.CanEditFocused&&mode!="normal"){var denied="自治地区只能查看常速运行；回任后可以使用 DT。";Notify(denied);return denied;}if(MandatoryPending){var message="请先处理本次年度结算、已保存奖励与待选事件。";Notify(message);return message;}var error=Clock.set_mode(Game,mode);if(error==""){Session.s["paused"]=false;Game.s["settings"]["user_paused"]=false;Game.s["beta_time"]["pause_reason"]="";}else Notify(error);_pending=0;_origin=Monotonic();Publish();SaveInternal();return error;}
 public void SetPaused(bool paused){Flush();if(!paused&&MandatoryPending){Notify("请先在年度 / 尾声页处理结算、奖励与待选事件，再继续经营。");return;}Session.s["paused"]=paused;Game.s["settings"]["user_paused"]=paused;if(!MandatoryPending)Game.s["beta_time"]["pause_reason"]=paused?"manual":"";_pending=0;_origin=Monotonic();Publish();SaveInternal();}
 public void ResumeNormal(){if(!Session.CanEditFocused){Message?.Invoke("历史街区仅可查看。");return;}Flush();if(Session.CanEditFocused&&PendingDecisions){Notify("请先处理本次已保存的奖励和待选事件。");return;}Clock.set_mode(Game,"normal");Cycles.acknowledge(Game);Session.s["paused"]=false;Game.s["settings"]["user_paused"]=false;Game.s["beta_time"]["pause_reason"]="";_pending=0;_origin=Monotonic();Publish();SaveInternal();}
 public void NewCity(long seed){Flush();_saveEnabled=true;Session.New(seed,Utc());if(WildBuild)Session.s["wild_mode"]=true;_pending=0;_origin=Monotonic();Publish();SaveInternal();}
 public string Load(string path=null){Flush();var loaded=CityStore.read(Game.c,path??SavePath);if(!loaded["ok"].Bool){Notify(loaded["error"].Text);return loaded["error"].Text;}var error=Session.Restore(loaded["state"]);if(error!=""){Notify(error);return error;}Session.Resume(Utc());_saveEnabled=true;_pending=0;_origin=Monotonic();Publish();SaveInternal();Notify("完整城市已载入；常速暂停，准备好后继续。");return "";}
 public string Save(){Flush();var error=SaveInternal();Notify(error==""?"城市已保存。":error);return error;}
 // Only automatic saves are gated. Explicit saves and lifecycle boundaries still
 // write a fresh UTC departure mark even when gameplay has remained paused.
 string SaveInternal(){if(!_saveEnabled||!Ready)return "原存档保留；请明确开始新城或载入有效城市后保存。";var now=Utc();Session.s["last_seen_utc"]=now;foreach(var g in Session.Games.Values){g.s["wall_mark"]=now;g.s["beta_time"]["last_seen_utc"]=now;}var error=CityStore.write(Session,SavePath);if(error=="")_saveDirty=false;if(error!=""&&error!=_lastSaveError)Notify(error);_lastSaveError=error;return error;}
 public string Export(string path){Flush();var error=CityStore.write(Session,path);Notify(error==""?"完整城市备份已导出："+path:error);return error;}
 public V DisplaySettings {get;private set;}=V.Map("ui_scale",1.0,"text_scale",1.0,"citizen_bubbles",false,"reduced_motion",false,"high_contrast",false,"all_links",false,"frame_limit",60);
 public static int ResolveFrameLimit(long requested)=>requested==30||requested==60||requested==120?(int)requested:60;
 public int EffectiveFrameLimit=>ResolveFrameLimit(DisplaySettings.Get("frame_limit",60).Long);
 public void LoadDisplaySettings(){var path=Path.Combine(StorageRoot,"display_settings.json");if(!File.Exists(path))return;try{var data=V.Parse(File.ReadAllText(path));if(data.IsMap)DisplaySettings.Merge(data,true);}catch(Newtonsoft.Json.JsonException){Notify("显示设置无法读取，已使用默认值。");}}
 public void SetDisplaySetting(string key,V value){DisplaySettings[key]=value;Directory.CreateDirectory(StorageRoot);File.WriteAllText(Path.Combine(StorageRoot,"display_settings.json"),DisplaySettings.ToJson(true));}
 public void Notify(string text){if(text!="")Message?.Invoke(text);}
 void Publish(V local=null,V world=null,bool stateChanged=true){if(stateChanged)_saveDirty=true;_sessionSnapshot=world??Session.snapshot();var focused=_sessionSnapshot.Get("focused_block_id","").Text;_snapshot=local??(focused!=""?_sessionSnapshot["blocks"][focused]["state"]:Game.snapshot());_hasPlayableBlock=_sessionSnapshot["blocks"].Values.Any(b=>b.Get("state",V.Map()).Get("phase","").Text=="play");Committed?.Invoke(_snapshot.Duplicate(true));}
 void OnApplicationFocus(bool focus){if(!Ready||_closing||_focused==focus)return;if(!focus){Flush();_focused=false;_pending=0;SaveInternal();Publish(stateChanged:false);}else {_focused=true;_pending=0;_origin=Monotonic();Session.Resume(Utc());Publish();SaveInternal();}}
 void OnApplicationPause(bool paused){if(paused)OnApplicationFocus(false);else if(!_focused)OnApplicationFocus(true);}
 void OnApplicationQuit(){if(!Ready)return;Flush();_closing=true;SaveInternal();}
}
}
