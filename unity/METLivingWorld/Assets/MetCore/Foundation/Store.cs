using System;
using System.IO;
namespace MET.Core {
/// <summary>Unity's own JSON format. No legacy engine payload or player folder dependency.</summary>
public static class Store {
 public const string SAVE="met_unity_v1.json";
 public static V canonical(V value){if(value.IsMap){var result=V.Map();var keys=value.Keys;keys.Sort(StringComparer.Ordinal);foreach(var k in keys)result[k]=canonical(value[k]);return result;}if(value.IsArray){var result=V.Arr();foreach(var x in value)result.Add(canonical(x));return result;}if(value.IsNumber&&!value.IsInteger){double d=value.Double;if(!double.IsNaN(d)&&!double.IsInfinity(d)&&Math.Abs(d)<9007199254.0&&d==Math.Floor(d))return V.From((long)d);}return value;}
 public static string encode(V state)=>canonical(state).ToJson(false,true);
 public static V decode(string text){try{var state=V.Parse(text);if(!state.IsMap)return V.Map("ok",false,"error","存档内容无效。");if(state.Get("rng").IsMap)foreach(var k in state["rng"].Keys)state["rng"][k]=state["rng"][k].Long;foreach(var k in new[]{"schema","seed","next_id","simulation_tick","block_index","tender_debt","event_cursor","award_level","block_opportunities","stage"})if(state.Has(k))state[k]=state[k].Long;return V.Map("ok",true,"state",state);}catch(Newtonsoft.Json.JsonException){return V.Map("ok",false,"error","存档JSON无效。");}}
 public static string write(V state,string path){try{string text=encode(state);var directory=Path.GetDirectoryName(path);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);File.WriteAllText(path+".tmp",text);if(!decode(File.ReadAllText(path+".tmp"))["ok"].Bool)return "临时存档校验失败；旧档未动。";if(File.Exists(path)){var prior=decode(File.ReadAllText(path));if(prior["ok"].Bool){if(File.Exists(path+".bak"))File.Delete(path+".bak");File.Move(path,path+".bak");}else File.Move(path,path+".corrupt-"+DateTime.UtcNow.Ticks);}File.Move(path+".tmp",path);return "";}catch(IOException e){return "无法写入存档："+e.Message;}catch(UnauthorizedAccessException e){return "无法写入存档："+e.Message;}}
 public static V read(string path){foreach(var candidate in new[]{path,path+".bak"}){if(!File.Exists(candidate))continue;var r=decode(File.ReadAllText(candidate));if(r["ok"].Bool){r["from_backup"]=candidate!=path;return r;}}return V.Map("ok",false,"error","没有可用存档，或主备份均损坏。");}
 public static string path(string root,string name=SAVE)=>Path.Combine(root,name);
}
}
