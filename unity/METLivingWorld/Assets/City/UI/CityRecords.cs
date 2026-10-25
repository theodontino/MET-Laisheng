using System;
using System.Globalization;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
namespace MET.City {
public sealed partial class CityInterface {
 V _selectedRecord=V.Map();string _importPath="",_exportPath="";
 string _recordsCategory="voices";int _recordIndex;
 void RecordsPage(){
  var tabs=Row(_body,"Record categories",42);foreach(var entry in new[]{new[]{"voices","城市声音"},new[]{"alerts","当前警报"},new[]{"archives","街区结算"},new[]{"log","事件记录"},new[]{"civic","历史成果"}}){var id=entry[0];Button(tabs,entry[1],()=>{_recordsCategory=id;_recordIndex=0;RenderPage();},_recordsCategory==id);}
  var columns=WorkspaceColumns("City records",.85f,1.4f);var list=columns[0];var detail=columns[1];
  if(_recordsCategory=="civic"){Label(list,"历史城市成果",21,Green);Label(list,"实际保存的成果与完成读数。",16,Muted);foreach(var row in Civic.task_rows(G)){var box=Card(detail,row["name"].Text,row["status"].Text,Green);Label(box,row["reading"].Text,17,Muted);}Fold(detail,"完整城市成果来源",c=>Raw(c,S.Get("city_goal",V.Map())));return;}
  var rows=_recordsCategory=="voices"?_feedback.history().Reverse().ToArray():_recordsCategory=="alerts"?_feedbackView.Get("alerts",V.Arr()).ToArray():_recordsCategory=="archives"?Block.list_archives(G).ToArray():S["log"].Reverse().Take(160).ToArray();
  Label(list,_recordsCategory=="alerts"?"当前可处理的问题":"历史记录",21,_recordsCategory=="alerts"?Gold:Blue);
  Label(list,_recordsCategory=="alerts"?"按原因合并的当前警报，可定位来源。":"选择记录查看完整内容与来源。",16,Muted);
  if(rows.Length==0){Label(detail,_recordsCategory=="alerts"?"当前没有已确认的硬警报。":"此分类尚无记录；不补造旧消息。",18,Muted);return;}
  _recordIndex=Mathf.Clamp(_recordIndex,0,rows.Length-1);
  for(int i=0;i<rows.Length;i++){var index=i;var item=rows[i];var caption=_recordsCategory=="archives"?item.Get("name","城市").Text:item.Get("text",item.Get("kind","城市记录")).Text;Button(list,caption,()=>{_recordIndex=index;RenderPage();},_recordIndex==index);}
  var selected=rows[_recordIndex];var title=_recordsCategory=="archives"?selected.Get("name","城市").Text:selected.Get("text",selected.Get("kind","城市记录")).Text;
  var card=Card(detail,title,_recordsCategory=="alerts"?"当前实际警报":"保存的历史记录",_recordsCategory=="alerts"?Gold:Blue);
  if(_recordsCategory=="archives"){Metric(card,"街区人口",F(selected.Get("population",0).Double)+" 人");Metric(card,"来生时长",Clock.duration(G,selected.Get("local_elapsed_seconds",0).Double));WorkspaceActions("查看完整只读记录",()=>{_selectedRecord=selected.Duplicate(true);Navigate("record");});}
  else {if(selected.Has("h"))Metric(card,"事件时间",F(selected["h"].Double,2)+" 来生月");var building=selected.Get("building_id","").Text;if(building!=""&&S["projects"].Has(building)){Label(card,"来源建筑："+G.project_name(building),17,Muted);WorkspaceActions(_recordsCategory=="alerts"?"定位问题来源":"定位记录来源",()=>{SelectBuilding(building);SetLayer(selected.Get("layer","terrain").Text);InspectMapFromPage();});}if(selected.Has("evidence"))Fold(card,_recordsCategory=="alerts"?"实际依据与满足率":"变化依据",c=>Raw(c,selected["evidence"]));}
  Fold(detail,"完整记录与来源",c=>Raw(c,selected));WorkspaceHint(_recordsCategory=="alerts"?"警报反映当前问题；历史消息保留原发生含义。":"历史记录只读，查看不会改变当前城市。");
 }

 void RecordPage(){
  var record=_selectedRecord;if(record.Count==0){Label(_body,"未选择街区记录。",22);return;}var columns=WorkspaceColumns("Archived block detail",1,1);var summary=columns[0];var historyColumn=columns[1];var intro=Card(summary,record.Get("name","城市").Text,"只读街区结算",Gold);Metric(intro,"本局人口",F(record.Get("population",0).Double)+" 人");Metric(intro,"本局通货",F(record.Get("currency",0).Double));Metric(intro,"已用 DT",F(record.Get("dt_spent",0).Double,2));Metric(intro,"来生时长",Clock.duration(G,record.Get("local_elapsed_seconds",0).Double));Metric(intro,"实际在线时长",F(record.Get("active_real_seconds",0).Double/3600,2)+" 现实小时");var theme=record.Get("theme",V.Map());Label(intro,Themes._definition(G,theme.Get("id","").Text).Get("name","未选择主题").Text+" · "+(theme.Get("completed",false).Bool?"主题完成":"主题未完成"),20,Green);
  var rewards=Card(summary,"奖励与构筑快照","保留当时的候选与领取状态。",Blue);foreach(var item in record.Get("rewards",V.Arr()))Label(rewards,item.Get("source","成长奖励").Text+" · "+(item.Get("claimed",false).Bool?"已领取 "+G.c.bonuses.Get(item.Get("chosen_bonus",""),V.Map()).Get("name",item.Get("chosen_bonus","")).Text:"未领取 · 候选保留"),17);var capstone=record.Get("capstone",V.Map());if(capstone.Count!=0){var card=Card(summary,capstone.Get("name","主题地标").Text,"当时已正式挂牌",CityWorld.BuildingTypeColor("special"));Fold(card,"地标完整规则",c=>Raw(c,capstone));}
  var history=Card(historyColumn,"城史","按记录保存的真实事件顺序。",Blue);foreach(var entry in record.Get("history",V.Arr()))Label(history,F(entry.Get("h",0).Double,2)+" 来生月 · "+entry.Get("text",entry.Get("kind","城市事件")).Text,17);Fold(historyColumn,"原始只读记录与完整城市快照",c=>Raw(c,record));WorkspaceHint("只读记录 / 不继续模拟或覆盖当前城");WorkspaceActions("返回记录目录",()=>Navigate("records"));
 }

 void SourcesPage(){var identity=Card(_body,"来源与规则档案","完整规则、场、运营商和结算记录按主体展开；不把原始 JSON 当成城市总览。",Blue);Label(identity,"当前来源：最近周期实际 / simulation tick "+S["simulation_tick"].Long,18,Green);foreach(var operatorValue in Rules.ids(G.c.operators)){var orgId=operatorValue.Text;var op=G.c.operators[orgId];var box=Card(_body,"运营商 / "+op["name"].Text,op.Get("summary","").Text,Gold);Label(box,"槽轨 "+string.Join(" → ",op.Get("slot_track",V.Arr()).Select(x=>Catalog.SLOT_NAMES.Get(x,x).Text)),18,Blue);var buildings=S["projects"].Values.Where(x=>x.Get("operator_org","").Text==orgId&&!x.Get("special",false).Bool).ToArray();Metric(box,"本区运营建筑",buildings.Length+" 座");foreach(var project in buildings){var id=project["id"].Text;Button(box,G.project_name(id)+" · 建筑来源 ›",()=>Navigate("building",id));}Fold(box,"组织、投标偏好与完整定义",c=>{Raw(c,op);Raw(c,S["organizations"].Get(orgId,V.Map()));});}
  Fold(_body,"当前场与逐地格贡献",c=>{Raw(c,S["fields"]);Raw(c,S["authored_fields"]);foreach(var tile in Rules.ids(S["cells"])){var fields=S["cells"][tile].Get("fields",V.Map());if(fields.Count==0)continue;Label(c,"地块 "+tile.Text+" · "+G.c.tags_text(S["cells"][tile]["traits"]),18,Blue);Raw(c,fields);}});
  Fold(_body,"服务池与真实分配",c=>{Raw(c,S["metrics"].Get("service_pools",V.Map()));Raw(c,S["metrics"].Get("service_links",V.Arr()));Raw(c,S["metrics"].Get("employment_links",V.Arr()));});
  Fold(_body,"资源守恒与真实结算审计",c=>Raw(c,S["audit"]));
  Fold(_body,"全部建筑定义与成长树",c=>{foreach(var id in Rules.ids(G.c.projects)){var def=G.c.projects[id];Label(c,def.Get("name",id).Text+" · T"+def.Get("tier",1).Long,19,Gold);Raw(c,def);}});
  Fold(_body,"编译后的规则、条件与来源追踪",c=>Raw(c,G.compiled));
 }
 string _settingsCategory="display";
 void SettingsPage(){
  var categories=Row(_body,"Settings categories",42);foreach(var entry in new[]{new[]{"display","显示"},new[]{"input","输入与辅助"},new[]{"save","存档"},new[]{"city","生命周期"}}){var id=entry[0];Button(categories,entry[1],()=>{_settingsCategory=id;RenderPage();},_settingsCategory==id);}
  var content=WorkspaceColumns("Settings "+_settingsCategory,1)[0];
  if(_settingsCategory=="display"){
   var display=Card(content,"界面与画面","默认全屏 2560×1440（2K）；界面、地图镜头和字号独立。",Blue);var scales=Row(display);foreach(var value in new[]{.8,1.0,1.2}){var v=value;Button(scales,(Math.Abs(Runtime.DisplaySettings["ui_scale"].Double-v)<.01?"✓ ":"")+"界面 "+(int)(v*100)+"%",()=>{Runtime.SetDisplaySetting("ui_scale",v);ApplyDisplay();RenderPage();});}var text=Row(display);foreach(var value in new[]{1.0,1.25,1.5}){var v=value;Button(text,"字号 "+(int)(v*100)+"%",()=>{Runtime.SetDisplaySetting("text_scale",v);RenderPage();RebuildAmbient();});}
   Label(display,"帧率上限",18,Gold);var fps=Row(display);foreach(var rate in new[]{0,30,60,120}){var r=rate;Button(fps,r==0?"自动 · 60":r+" FPS",()=>{Runtime.SetDisplaySetting("frame_limit",r);ApplyDisplay();RenderPage();});}
  }else if(_settingsCategory=="input"){
   var input=Card(content,"输入与辅助","输入框和档案持有键盘时不切换地图。",Blue);foreach(var entry in new[]{new[]{"citizen_bubbles","市民气氛泡泡"},new[]{"reduced_motion","减少动态"},new[]{"high_contrast","高对比度"},new[]{"all_links","完整关系线"}}){var id=entry[0];Button(input,entry[1]+"："+(Runtime.DisplaySettings.Get(id,false).Bool?"开":"关"),()=>{Runtime.SetDisplaySetting(id,!Runtime.DisplaySettings.Get(id,false).Bool);ApplyDisplay();RenderPage();});}Button(input,"声音："+(S["settings"].Get("sound",true).Bool?"开":"关"),()=>Command(g=>{g.s["settings"]["sound"]=!g.s["settings"].Get("sound",true).Bool;return "";}));Label(input,"鼠标点击选择地格；滚轮独立缩放地图；L / Shift+L 循环图层；单按 Shift 开关地图数值；P 暂停；Esc 逐级返回。",16,Muted);
   Fold(input,"建筑识别色",c=>{Label(c,"住宅 · 绿色",18,CityWorld.BuildingTypeColor("housing"));Label(c,"工业、电力与物流 · 黄色",18,CityWorld.BuildingTypeColor("industry"));Label(c,"社区、商业、医疗与教育服务 · 蓝色",18,CityWorld.BuildingTypeColor("community"));Label(c,"特殊建筑与独特工程 · 红色",18,CityWorld.BuildingTypeColor("special"));Label(c,"农业和绿化保留自然配色。识别色主要呈现在屋顶，建筑形状和名称仍区分具体用途。",17,Muted);});
  }else if(_settingsCategory=="save"){
   var save=Card(content,"存档与城市备份","Unity 使用独立保存格式和目录，保留主存档与上一份有效备份。",Green);var controls=Row(save);Button(controls,"保存当前城市",()=>Runtime.Save(),true);Button(controls,"载入当前存档",()=>{if(Runtime.Load()==""){_feedback.reset();CloseArchive();}});Label(save,"保存位置："+Runtime.SavePath,15,Muted);if(_exportPath=="")_exportPath=Path.Combine(Runtime.StorageRoot,"exports","MET-Unity-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");var export=InputBox(save,_exportPath,"输入导出路径");export.onValueChanged.AddListener(v=>_exportPath=v);Button(save,"导出城市备份",()=>Runtime.Export(_exportPath));var import=InputBox(save,_importPath,"输入 Unity 城市备份完整路径");import.onValueChanged.AddListener(v=>_importPath=v);Button(save,"载入此城市备份",()=>{if(string.IsNullOrWhiteSpace(_importPath)){ShowMessage("请输入备份文件完整路径。");return;}if(Runtime.Load(_importPath)==""){_feedback.reset();CloseArchive();}});
  }else{
   var city=Card(content,"时间与生命周期","后台和关闭只记录离开时长，不补算资源、移民、事件、DT或奖励。",Gold);var summary=Clock.summary(G);Metric(city,"真实活跃在线",F(summary["active_real_seconds"].Double/3600,2)+" 小时");Metric(city,"记录的离开时长",F(summary["offline_recorded_seconds"].Double/3600,2)+" 小时");Metric(city,"当前暂停原因",summary.Get("pause_reason","").Text);Button(city,"查看年度 / 街区结算",()=>Navigate("settlement"));Button(city,"另建一座独立城市…",()=>Confirm("另建独立城市并替换当前会话？","此操作离开当前永久城市，并将当前存档写入上一份有效备份。独立城市不继承地区、资源、方案、蓝图、地标或长期成果。同城扩展暂未开放；当前街区可先保存只读结算记录。",()=>{Runtime.NewCity(DateTime.UtcNow.Ticks%2147483000);_feedback.reset();SelectedTile="";SelectedBuilding="";OpenArchive("start");}));Button(city,"保存并退出",()=>{Runtime.Save();Application.Quit();});
  }
 }

 void RebuildAmbient(){foreach(var pair in _fontBases.ToArray()){if(pair.Key==null){_fontBases.Remove(pair.Key);continue;}pair.Key.fontSize=Mathf.RoundToInt(pair.Value*TextScale);}RefreshContext();UpdateHud();RenderStatus();}

}
}
