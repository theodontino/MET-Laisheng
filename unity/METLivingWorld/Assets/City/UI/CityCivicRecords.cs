using System;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MET.City {
public sealed partial class CityInterface {
 // Civic pages read the same committed projection as the map. Commands keep the
 // existing runtime boundary, including its flush, validation and persistence.
 bool RenderCivicRecords(VisualElement body,VisualElement actions) {
  switch(_page) {
   case "archive_home":case "records":CivicRecords(body,actions);break;
   case "record":CivicRecord(body,actions);break;
   case "sources":CivicSources(body);break;
   case "content":CivicContent(body,actions);break;
   case "heritage":CivicHeritage(body,actions);break;
   case "settings":CivicSettings(body,actions);break;
   case "start":CivicStart(body,actions);break;
   case "building":CivicBuildingDetails(body,actions,_pageContext!=""?_pageContext:SelectedBuilding);break;
   default:return false;
  }
  return true;
 }
 void CivicEvidence(VisualElement parent,V data) {
  var field=new TextField {value=(data??V.Map()).ToJson(true),multiline=true,isReadOnly=true};
  field.AddToClassList("c-field");field.AddToClassList("c-small");field.style.whiteSpace=WhiteSpace.Normal;parent.Add(field);
 }
 Foldout CivicRecordsFold(VisualElement parent,string title,Action<VisualElement> render)=>CivicUI.Fold(parent,title,render);
 void CivicSource(VisualElement parent,string title,V data)=>CivicRecordsFold(parent,title,c=>CivicEvidence(c,data));
 void CivicSelect(VisualElement parent,string label,bool selected,Action action) {var b=CivicUI.Button(parent,label,action);b.EnableInClassList("is-selected",selected);}
 void CivicRecords(VisualElement body,VisualElement actions) {
  var all=AllCityRecords();var tabs=CivicUI.Row(body,"c-wrap");
  foreach(var category in new[]{"all","voices","alerts","archives","log","civic"}) {
   var id=category;var count=id=="all"?all.Length:all.Count(r=>r["category"].Text==id);
   CivicSelect(tabs,(id=="all"?"全部":RecordCategoryName(id))+" "+count,_recordsCategory==id,()=>{_recordsCategory=id;_recordIndex=0;_recordPage=0;RenderPage();});
  }
  var search=CivicUI.Row(body);CivicUI.Field(search,"搜索记录",_recordsQuery,v=>_recordsQuery=v).AddToClassList("c-grow");
  CivicUI.Button(search,"搜索",()=>{_recordIndex=0;_recordPage=0;RenderPage();},true);
  if(_recordsQuery!="")CivicUI.Button(search,"清除",()=>{_recordsQuery="";_recordIndex=0;_recordPage=0;RenderPage();});
  var query=_recordsQuery.Trim();var rows=all.Where(r=>(_recordsCategory=="all"||r["category"].Text==_recordsCategory)&&(query==""||(RecordCategoryName(r["category"].Text)+" "+r["record"].ToJson()).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
  CivicUI.Label(body,"匹配 "+rows.Length+" 条 · 已保存 / 当前记录共 "+all.Length+" 条","c-small c-muted");
  if(rows.Length==0){CivicUI.Empty(body,"暂无匹配记录",query==""?"此分类尚无实际记录。":"缩短关键词或切换分类后重试。");return;}
  _recordIndex=Mathf.Clamp(_recordIndex,0,rows.Length-1);var pages=(rows.Length+RecordsPerPage-1)/RecordsPerPage;_recordPage=Mathf.Clamp(_recordPage,0,pages-1);
  var columns=CivicUI.Row(body);var list=CivicUI.Column(columns,"c-third");var detail=CivicUI.Column(columns,"c-grow");
  var pager=CivicUI.Row(list);CivicUI.Button(pager,"上一页",()=>{_recordPage--;_recordIndex=_recordPage*RecordsPerPage;RenderPage();},enabled:_recordPage>0);CivicUI.Label(pager,(_recordPage+1)+" / "+pages,"c-small");CivicUI.Button(pager,"下一页",()=>{_recordPage++;_recordIndex=_recordPage*RecordsPerPage;RenderPage();},enabled:_recordPage<pages-1);
  for(int i=_recordPage*RecordsPerPage;i<Math.Min(rows.Length,(_recordPage+1)*RecordsPerPage);i++){var index=i;var entry=rows[i];CivicUI.Choice(list,RecordTitle(entry),RecordCategoryName(entry["category"].Text),_recordIndex==index,()=>{_recordIndex=index;RenderPage();});}
  var selectedEntry=rows[_recordIndex];var record=selectedEntry["record"];var categoryId=selectedEntry["category"].Text;
  var card=CivicUI.Section(detail,RecordTitle(selectedEntry),RecordCategoryName(categoryId));
  if(categoryId=="archives") {
   CivicUI.Pair(card,"街区人口",F(record.Get("population",0).Double)+" 人");CivicUI.Pair(card,"来生时长",Clock.duration(G,record.Get("local_elapsed_seconds",0).Double));
   CivicUI.Button(actions,"打开街区结算",()=>{_selectedRecord=record.Duplicate(true);Navigate("record");},true);
  }else if(categoryId=="civic") {
   CivicUI.Label(card,record.Get("status","").Text,"c-good");CivicUI.Label(card,record.Get("reading","").Text);CivicSource(card,"城市成果来源",S.Get("city_goal",V.Map()));
  }else {
   if(record.Has("text"))CivicUI.Label(card,record["text"].Text);
   if(record.Has("h"))CivicUI.Pair(card,"事件时间",F(record["h"].Double,2)+" 来生月");
   if(record.Has("kind"))CivicUI.Pair(card,"事件类型",record["kind"].Text);
   var building=record.Get("building_id","").Text;
   if(building!=""&&S["projects"].Has(building)) {
    CivicUI.Pair(card,"来源建筑",G.project_name(building));CivicUI.Button(actions,"定位来源",()=>{SelectBuilding(building);SetLayer(record.Get("layer","terrain").Text);InspectMapFromPage();},true);
   }
   if(record.Has("evidence"))CivicSource(card,categoryId=="alerts"?"实际依据与满足率":"变化依据",record["evidence"]);
  }
  CivicSource(detail,"完整记录与来源",record);CivicUI.Label(detail,categoryId=="alerts"?"当前警报反映现在的问题。历史消息保留原发生含义。":"历史记录只读，查看不会改变当前城市。","c-small c-muted");
 }
 void CivicRecord(VisualElement body,VisualElement actions) {
  var record=_selectedRecord;if(record.Count==0){CivicUI.Empty(body,"未选择街区记录");return;}
  var intro=CivicUI.Section(body,record.Get("name","城市").Text,"只读街区结算");var values=CivicUI.Row(intro,"c-wrap");
  CivicUI.Value(values,"人口",F(record.Get("population",0).Double)+" 人");CivicUI.Value(values,"通货",F(record.Get("currency",0).Double));CivicUI.Value(values,"已用 DT",F(record.Get("dt_spent",0).Double,2));
  CivicUI.Pair(intro,"来生时长",Clock.duration(G,record.Get("local_elapsed_seconds",0).Double));CivicUI.Pair(intro,"实际在线",F(record.Get("active_real_seconds",0).Double/3600,2)+" 小时");
  var theme=record.Get("theme",V.Map());CivicUI.Pair(intro,"主题",Themes._definition(G,theme.Get("id","").Text).Get("name","未选择主题").Text+" · "+(theme.Get("completed",false).Bool?"已完成":"未完成"));
  var rewards=CivicUI.Section(body,"奖励与构筑快照","保留当时的候选与领取状态");
  foreach(var item in record.Get("rewards",V.Arr()))CivicUI.Pair(rewards,item.Get("source","成长奖励").Text,item.Get("claimed",false).Bool?"已领取 · "+G.c.bonuses.Get(item.Get("chosen_bonus",""),V.Map()).Get("name",item.Get("chosen_bonus","")).Text:"未领取 · 候选保留");
  var capstone=record.Get("capstone",V.Map());if(capstone.Count>0){CivicUI.Label(rewards,"正式挂牌 · "+capstone.Get("name","主题地标").Text,"c-heading");CivicSource(rewards,"地标规则与来源",capstone);}
  var history=CivicUI.Section(body,"城史","按保存顺序");foreach(var entry in record.Get("history",V.Arr()))CivicUI.Pair(history,F(entry.Get("h",0).Double,2)+" 来生月",entry.Get("text",entry.Get("kind","城市事件")).Text);
  CivicSource(body,"原始只读记录与城市快照",record);CivicUI.Button(actions,"返回记录",()=>Navigate("records"));
 }
 void CivicSources(VisualElement body) {
  var intro=CivicUI.Section(body,"来源与规则","最近周期实际 · 结算步 "+S["simulation_tick"].Long);CivicUI.Label(intro,"按运营商、场和结算来源核对当前城市。");
  foreach(var value in Rules.ids(G.c.operators)) {
   var id=value.Text;var op=G.c.operators[id];var box=CivicUI.Section(body,op["name"].Text,op.Get("summary","").Text);
   CivicUI.Pair(box,"槽位成长",string.Join(" → ",op.Get("slot_track",V.Arr()).Select(x=>Catalog.SLOT_NAMES.Get(x,x).Text)));
   var projects=S["projects"].Values.Where(x=>x.Get("operator_org","").Text==id&&!x.Get("special",false).Bool).ToArray();CivicUI.Pair(box,"本区建筑",projects.Length+" 座");
   var links=CivicUI.Row(box,"c-wrap");foreach(var p in projects){var bid=p["id"].Text;CivicUI.Button(links,G.project_name(bid),()=>Navigate("building",bid));}
   CivicRecordsFold(box,"组织定义与实际组织记录",c=>{CivicEvidence(c,op);CivicEvidence(c,S["organizations"].Get(id,V.Map()));});
  }
  CivicRecordsFold(body,"当前场与逐地格贡献",c=>{CivicEvidence(c,S["fields"]);CivicEvidence(c,S["authored_fields"]);foreach(var tile in Rules.ids(S["cells"])){var f=S["cells"][tile].Get("fields",V.Map());if(f.Count==0)continue;CivicUI.Label(c,"地块 "+tile.Text+" · "+G.c.tags_text(S["cells"][tile]["traits"]),"c-heading");CivicEvidence(c,f);}});
  CivicRecordsFold(body,"服务池、服务与就业分配",c=>{CivicEvidence(c,S["metrics"].Get("service_pools",V.Map()));CivicEvidence(c,S["metrics"].Get("service_links",V.Arr()));CivicEvidence(c,S["metrics"].Get("employment_links",V.Arr()));});
  CivicSource(body,"资源守恒与真实结算审计",S["audit"]);
  CivicRecordsFold(body,"全部建筑定义与成长树",c=>{foreach(var id in Rules.ids(G.c.projects)){var def=G.c.projects[id];CivicUI.Label(c,def.Get("name",id).Text+" · T"+def.Get("tier",1).Long,"c-heading");CivicEvidence(c,def);}});CivicSource(body,"编译后的规则、条件与来源追踪",G.compiled);
 }
 void CivicLegacyDetails(VisualElement parent,V item) {
  CivicUI.Label(parent,item.Get("tradeoff","").Text,"c-muted");
  var district=item.Get("source_district","").Text;if(district!="")CivicUI.Pair(parent,"来源地区",W["districts"].Get(district,V.Map()).Get("name",district).Text);
  var landmark=item.Get("source_landmark",item.Get("landmark",V.Map()));if(landmark.Count>0)CivicUI.Pair(parent,"原地标",landmark.Get("name",landmark.Get("id","地标留在原区")).Text);
  foreach(var rule in item.Get("rules",V.Arr())) {
   if(rule.Get("key","").Text=="service_capacity_multiplier") {foreach(var pair in rule["value"].Pairs)CivicUI.Pair(parent,Service(pair.Key)+"供给","× "+F(pair.Value.Double,2));}
   else {var field=rule.Get("field","").Text;CivicUI.Pair(parent,Stat(field),"× "+F(rule.Get("value",1).Double,2));}
  }
  if(item.Get("knowledge_multiplier",1).Double!=1)CivicUI.Pair(parent,"知识产出","× "+F(item["knowledge_multiplier"].Double,2));
 }
 void CivicLocalLegacy(VisualElement body) {
  var view=Runtime.Session.BuildLegacyView();var chosen=view.Get("chosen",V.Map());var pending=view.Get("pending",V.Arr());var evidence=view.Get("evidence",V.Map());
  var intro=CivicUI.Section(body,"本区长期成果","首次 DT 实际归零时锁定资格");CivicUI.Label(intro,"资格保留当时主题、活跃部署、蓝图和最后实际供给。本街区成果在任务结算中选择。","c-muted");
  if(chosen.Count>0){var box=CivicUI.Section(intro,"已选择 · "+chosen.Get("name",chosen.Get("profile_id","长期成果")).Text,LegacyGrade(chosen));CivicLegacyDetails(box,chosen);CivicSource(box,"已选成果与原地来源",chosen);}
  else if(pending.Count==0)CivicUI.Empty(intro,"尚无锁定成果","旧档缺少首次归零证据时不追补资格。");
  else foreach(var item in pending){var box=CivicUI.Section(intro,item.Get("name",item.Get("profile_id",item.Get("id","成果"))).Text,LegacyGrade(item));CivicLegacyDetails(box,item);CivicSource(box,"成果资格与构筑依据",item);}
  if(evidence.Count>0){var box=CivicUI.Section(body,"锁定时最后实际供给");var m=evidence.Get("metrics",V.Map());foreach(var key in new[]{"heat_used","pollution_recycled","medical","education"})if(m.Has(key))CivicUI.Pair(box,V.Map("heat_used","余热利用","pollution_recycled","污染回收","medical","医疗覆盖","education","教育覆盖")[key].Text,key=="medical"||key=="education"?F(m[key].Double*100,1)+"%":F(m[key].Double,3));foreach(var ins in evidence.Get("bonus_evidence",V.Arr()))CivicUI.Label(box,G.c.bonuses.Get(ins.Get("bonus_id",ins.Get("bonus","")),V.Map()).Get("name","方案").Text+" → "+G.project_name(ins.Get("host_id",ins.Get("host","")).Text)+" · 活跃 "+ins.Get("count",1).Long+" 份");foreach(var pair in evidence.Get("blueprint_levels",V.Map()).Pairs)CivicUI.Pair(box,G.c.bonuses.Get(pair.Key,V.Map()).Get("name",pair.Key).Text,LegacyGrade(V.Map("grade",pair.Value)));CivicSource(box,"首次归零的完整证据",evidence);}
 }
 void CivicContent(VisualElement body,VisualElement actions) {
  var tabs=CivicUI.Row(body,"c-wrap");foreach(var entry in new[]{new[]{"local","本区成果"},new[]{"bonuses","方案图鉴"},new[]{"profiles","规约方向"},new[]{"results","同城成果"}}){var id=entry[0];CivicSelect(tabs,entry[1],_contentCategory==id,()=>{_contentCategory=id;_contentSelection="";RenderPage();});}
  if(_contentCategory=="local"){CivicLocalLegacy(body);return;}
  var columns=CivicUI.Row(body);var list=CivicUI.Column(columns,"c-third");var detail=CivicUI.Column(columns,"c-grow");
  if(_contentCategory=="bonuses") {
   foreach(var bid in new[]{"P09","R19","O08","B10"}){var id=bid;CivicUI.Choice(list,G.c.bonuses.Get(id,V.Map()).Get("name",id).Text,id,(_contentSelection==""?"P09":_contentSelection)==id,()=>{_contentSelection=id;RenderPage();});}
   var selected=_contentSelection==""?"P09":_contentSelection;var def=G.c.bonuses.Get(selected,V.Map());var card=Bonus.card(G,selected);var box=CivicUI.Section(detail,def.Get("name",selected).Text,def.Get("summary","").Text);
   CivicUI.Pair(box,"库存 / 蓝图",card.Get("copies",0).Long+" 份 / "+card.Get("grade","基础").Text);CivicUI.Label(box,card.Get("stacking_note","首份100%，同对象追加副本每份33⅓%；容量与维护代价继续生效。").Text,"c-muted");
   foreach(var effect in card.Get("effects",V.Arr()))CivicUI.Pair(box,effect.Get("label","效果").Text,FormatEffect(effect,effect.Get("before",0).Double)+" → "+FormatEffect(effect,effect.Get("after",0).Double));
   CivicSource(box,"完整规则与副本效果",Rules.bonus_rules(G,selected,1,(int)card.Get("blueprint_level",0).Long));CivicUI.Button(actions,"部署与蓝图",()=>{_inventoryChoice=selected;Navigate("bonuses");},true);return;
  }
  var view=Runtime.Session.BuildLegacyView();var rows=(_contentCategory=="profiles"?view.Get("profiles",V.Arr()):view.Get("results",V.Arr())).ToArray();
  if(rows.Length==0){CivicUI.Empty(detail,"尚无成果","成果由真实构筑证据决定。");return;}
  int.TryParse(_contentSelection,out var selectedIndex);selectedIndex=Mathf.Clamp(selectedIndex,0,rows.Length-1);
  for(int i=0;i<rows.Length;i++){var index=i;var item=rows[i];CivicUI.Choice(list,item.Get("name",item.Get("profile_id",item.Get("id","成果"))).Text,_contentCategory=="profiles"?"规约方向":"来源 · "+LegacyBlockName(item.Get("source_block","").Text),selectedIndex==index,()=>{_contentSelection=index.ToString();RenderPage();});}
  var chosen=rows[selectedIndex];var intro=CivicUI.Section(detail,chosen.Get("name",chosen.Get("profile_id",chosen.Get("id","成果"))).Text,_contentCategory=="profiles"?chosen.Get("description","").Text:LegacyGrade(chosen));CivicLegacyDetails(intro,chosen);CivicSource(intro,"完整规约与成果来源",chosen);CivicUI.Label(detail,"同城新街区与后续地区暂未开放。","c-small c-muted");
 }
 void CivicHeritage(VisualElement body,VisualElement actions) {
  var world=W;var columns=CivicUI.Row(body);var history=CivicUI.Column(columns,"c-half");var licenses=CivicUI.Column(columns,"c-half");
  var timeline=CivicUI.Section(history,"同一座城市的历史","实际开局、建设、任期、研究与交付");var events=world.Get("history",V.Arr()).Reverse().Take(140).ToArray();if(events.Length==0)CivicUI.Empty(timeline,"尚无保存的城史");
  foreach(var evt in events){var box=CivicUI.Section(timeline,HistoryText(evt));var payload=evt.Get("payload",V.Map());if(payload.Has("text"))CivicUI.Label(box,payload["text"].Text);if(evt.Has("h"))CivicUI.Pair(box,"来生月",F(evt["h"].Double,2));CivicSource(box,"事件与实体来源",evt);}
  CivicSource(history,"地方、制度与城市能力",world.Get("legacy",V.Map()));CivicSource(history,"永久城市完整只读记录",world);
  var wallet=CivicUI.Section(licenses,"许可来源记录","只读档案 · 跨街区转移暂未开放");int count=0;foreach(var source in world["blocks"].Values)foreach(var pair in source.Get("state",V.Map()).Get("block",V.Map()).Get("bonus_wallet",V.Map()).Pairs){var charges=pair.Value.Get("charges",0).Long;if(charges<=0)continue;count++;var box=CivicUI.Section(wallet,G.c.bonuses.Get(pair.Key,V.Map()).Get("name",pair.Key).Text,"来源 · "+BlockName(source));CivicUI.Pair(box,"保存库存",charges+" 份");CivicSource(box,"完整库存来源",pair.Value);}if(count==0)CivicUI.Empty(wallet,"尚无保存的许可库存");CivicUI.Button(actions,"长期成果与来源",()=>Navigate("content"));
 }
 void CivicSettings(VisualElement body,VisualElement actions) {
  var tabs=CivicUI.Row(body,"c-wrap");foreach(var entry in new[]{new[]{"display","显示"},new[]{"input","输入与辅助"},new[]{"save","存档"},new[]{"city","生命周期"}}){var id=entry[0];CivicSelect(tabs,entry[1],_settingsCategory==id,()=>{_settingsCategory=id;RenderPage();});}
  if(_settingsCategory=="display") {
   var display=CivicUI.Section(body,"界面与画面","全屏 2560×1440（2K）");CivicUI.Label(display,"界面大小、地图镜头缩放和字号各自独立。","c-muted");
   CivicUI.Pair(display,"当前显示",Screen.width+" × "+Screen.height+" · "+(Screen.fullScreen?"全屏":"窗口"));CivicUI.Button(display,"恢复全屏 2K",()=>{Screen.SetResolution(2560,1440,FullScreenMode.FullScreenWindow);ApplyDisplay();RenderPage();});
   var scales=CivicUI.Section(display,"界面大小");var scaleRow=CivicUI.Row(scales);foreach(var scale in new[]{.8,1.0,1.2}){var value=scale;CivicSelect(scaleRow,(int)(value*100)+"%",Math.Abs(Runtime.DisplaySettings.Get("ui_scale",1).Double-value)<.01,()=>{Runtime.SetDisplaySetting("ui_scale",value);ApplyDisplay();RenderPage();});}
   var text=CivicUI.Section(display,"文字大小");var textRow=CivicUI.Row(text);foreach(var scale in new[]{1.0,1.25,1.5}){var value=scale;CivicSelect(textRow,(int)(value*100)+"%",Math.Abs(Runtime.DisplaySettings.Get("text_scale",1).Double-value)<.01,()=>{Runtime.SetDisplaySetting("text_scale",value);ApplyDisplay();RenderPage();RebuildAmbient();});}
   var frame=CivicUI.Section(display,"帧率上限");var frameRow=CivicUI.Row(frame);foreach(var rate in new[]{0,30,60,120}){var value=rate;CivicSelect(frameRow,rate==0?"自动 · 60":rate+" FPS",Runtime.DisplaySettings.Get("frame_limit",60).Long==value,()=>{Runtime.SetDisplaySetting("frame_limit",value);ApplyDisplay();RenderPage();});}
  }else if(_settingsCategory=="input") {
   var input=CivicUI.Section(body,"输入与辅助","输入框和操作窗口持有键盘");foreach(var entry in new[]{new[]{"citizen_bubbles","市民气氛泡泡"},new[]{"reduced_motion","减少动态"},new[]{"high_contrast","高对比度"},new[]{"all_links","完整关系线"}}){var key=entry[0];CivicUI.Toggle(input,entry[1],Runtime.DisplaySettings.Get(key,false).Bool,value=>{Runtime.SetDisplaySetting(key,value);ApplyDisplay();RenderPage();});}
   CivicUI.Toggle(input,"声音",S["settings"].Get("sound",true).Bool,value=>Command(g=>{g.s["settings"]["sound"]=value;return "";}));
   var keys=CivicUI.Section(body,"地图操作");CivicUI.Table(keys,new[]{"输入","操作"},new[]{new[]{"鼠标点击","选择地格"},new[]{"滚轮","独立缩放地图"},new[]{"L / Shift+L","向前 / 向后切换图层"},new[]{"单按 Shift","开关地图数值"},new[]{"P","暂停 / 继续"},new[]{"Esc","逐级返回"}});
   CivicRecordsFold(body,"建筑识别色",c=>{CivicUI.Pair(c,"住宅","绿色");CivicUI.Pair(c,"工业、电力与物流","黄色");CivicUI.Pair(c,"社区、商业、医疗与教育","蓝色");CivicUI.Pair(c,"特殊建筑与独特工程","红色");CivicUI.Label(c,"农业和绿化保留自然配色；建筑形状和名称区分具体用途。","c-muted");});
  }else if(_settingsCategory=="save") {
   var save=CivicUI.Section(body,"存档与城市备份","Unity 独立保存格式与目录");CivicUI.Label(save,"保留当前主存档与上一份有效备份。","c-muted");CivicUI.Pair(save,"保存位置",Runtime.SavePath);
   var row=CivicUI.Row(save);CivicUI.Button(row,"保存当前城市",()=>Runtime.Save(),true);CivicUI.Button(row,"载入当前存档",()=>{if(Runtime.Load()==""){_feedback.reset();CloseArchive();}});
   if(_exportPath=="")_exportPath=Path.Combine(Runtime.StorageRoot,"exports","MET-Unity-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");
   CivicUI.Field(save,"导出完整路径",_exportPath,v=>_exportPath=v);CivicUI.Button(save,"导出城市备份",()=>Runtime.Export(_exportPath));CivicUI.Field(save,"导入完整路径",_importPath,v=>_importPath=v);
   CivicUI.Button(save,"载入此城市备份",()=>{if(string.IsNullOrWhiteSpace(_importPath)){ShowMessage("请输入备份文件完整路径。");return;}if(Runtime.Load(_importPath)==""){_feedback.reset();CloseArchive();}});
  }else {
   var city=CivicUI.Section(body,"时间与生命周期");CivicUI.Label(city,"后台和关闭只记录离开时长，不补算资源、移民、事件、DT 或奖励。","c-muted");var summary=Clock.summary(G);
   CivicUI.Pair(city,"真实活跃在线",F(summary["active_real_seconds"].Double/3600,2)+" 小时");CivicUI.Pair(city,"记录的离开时长",F(summary["offline_recorded_seconds"].Double/3600,2)+" 小时");CivicUI.Pair(city,"当前暂停原因",summary.Get("pause_reason","").Text);
   CivicUI.Button(city,"年度 / 街区结算",()=>Navigate("settlement"));CivicUI.Button(city,"另建独立城市…",()=>Confirm("另建独立城市并替换当前会话？","当前存档将写入上一份有效备份。新城市不继承地区、资源、方案、蓝图、地标或长期成果。同城扩展暂未开放。",()=>{Runtime.NewCity(DateTime.UtcNow.Ticks%2147483000);_feedback.reset();SelectedTile="";SelectedBuilding="";OpenArchive("start");}));
   CivicUI.Button(actions,"保存并退出",()=>{if(Runtime.Save()=="")Application.Quit();});
  }
 }
 void CivicStart(VisualElement body,VisualElement actions) {
  if(Runtime.Session.s["phase"].Text=="choose_district") {CivicInitialDistrict(body,actions);return;}
  if(S["phase"].Text=="select_block") {
   CivicUI.Label(body,"比较地形，再确定登陆位置。下一步选择起源。","c-muted");var choices=CivicUI.Row(body);
   var names=new[]{"河湾","林缘旧村","北部高台"};var notes=new[]{"临水平地，邻近水岸。先铺开生活供给，再选择工业方向。","森林与既有住区相接。地块条件改变后续方案的价值。","高地与开阔地并存。保留水岸与土地特征，按实际场地规划。"};
   for(int i=0;i<3;i++){var index=i;var box=CivicUI.Section(choices,names[i],"登陆地形 0"+(i+1));box.AddToClassList("c-third");CivicUI.Label(box,notes[i]);CivicUI.Button(box,"在此登陆",()=>Command(g=>g.select_block(index)),true);}return;
  }
  if(S["phase"].Text!="origin"){CivicUI.Empty(body,"城市已开始","关闭此页继续经营。");CivicUI.Button(actions,"返回城市",CloseArchive,true);return;}
  var origins=S["origin_choices"].ToArray();if(origins.Length==0){CivicUI.Empty(body,"等待本次起源候选");return;}
  CivicUI.Label(body,"选择已保存的起源候选，安顿居民并领取真实起始补给。","c-muted");var row=CivicUI.Row(body,"c-wrap");
  foreach(var choice in origins){var id=choice.Text;var origin=G.c.origins[id];var card=CivicUI.Section(row,origin.Get("name",id).Text);card.AddToClassList("c-third");CivicUI.Label(card,origin.Get("story",origin.Get("hint","")).Text);CivicUI.Pair(card,"补充通货",F(origin.Get("cash",0).Double,0));foreach(var resource in new[]{"food","material","goods"})if(origin.Get(resource,0).Double!=0)CivicUI.Pair(card,Resource(resource),Signed(origin[resource].Double));CivicUI.Button(card,"确立起源 · 安顿居民",()=>{Command(g=>g.choose_origin(id));if(S["phase"].Text=="play")CloseArchive();},true);}
 }
 void CivicInitialDistrict(VisualElement body,VisualElement actions) {
  if(W["blocks"].Count>0||W["districts"].Count>0){CivicUI.Empty(body,"后续地区暂未开放","已有历史与来源可在档案中查看。");return;}
  var stage=Mathf.Clamp(_districtStartStage,0,2);var nav=CivicUI.Row(body);for(int i=0;i<3;i++){var step=i;CivicSelect(nav,new[]{"1 方向","2 规约","3 地点"}[i],stage==i,()=>{_districtStartStage=step;RenderPage();});}
  var columns=CivicUI.Row(body);var choices=CivicUI.Column(columns,"c-half");var summary=CivicUI.Section(columns,"本次开局");summary.AddToClassList("c-half");_arcSponsor="";
  if(stage==0) {
   CivicUI.Field(choices,"地区名称",_arcName,v=>_arcName=v);var specs=CivicUI.Section(choices,"专业方向");foreach(var spec in new[]{"balanced","agricultural","industrial","services","research","logistics"}){var id=spec;CivicSelect(specs,SpecName(id),_arcSpecialization==id,()=>{_arcSpecialization=id;_arcOpportunity="";RenderPage();});}
  }else if(stage==1) {
   var offers=Runtime.Session.StartupOpportunities(true,_arcSpecialization);if(_arcOpportunity!=""&&!offers.Any(x=>x.Get("id","").Text==_arcOpportunity))_arcOpportunity="";
   CivicUI.Choice(choices,"无规约","常规开局",_arcOpportunity=="",()=>{_arcOpportunity="";RenderPage();});foreach(var offer in offers){var item=offer;var id=item["id"].Text;CivicUI.Choice(choices,item.Get("name",id).Text,item.Get("tradeoff","").Text,_arcOpportunity==id,()=>{_arcOpportunity=id;RenderPage();});CivicSource(choices,"规约效果与来源",item);}if(offers.Count==0)CivicUI.Label(choices,"当前方向尚无可采用的长期成果。","c-muted");
  }else {
   for(int i=0;i<3;i++){var landing=i;CivicUI.Choice(choices,new[]{"河湾","林缘旧村","北部高台"}[i],"地点决定真实地形",_arcLanding==i,()=>{_arcLanding=landing;RenderPage();});}
  }
  CivicUI.Pair(summary,"方向",SpecName(_arcSpecialization));CivicUI.Pair(summary,"供给来源","城市最初授权");CivicUI.Pair(summary,"登陆地点",new[]{"河湾","林缘旧村","北部高台"}[Mathf.Clamp(_arcLanding,0,2)]);
  var quote=Runtime.Session.StartupView(true,_arcSpecialization,_arcSponsor,_arcOpportunity);if(quote.Get("initial_charter",false).Bool)CivicUI.Label(summary,"首地区来自城市最初授权，居民与基础建筑沿用当前起源规则。","c-muted");else {CivicUI.Pair(summary,"通货投入 / 持有",F(quote.Get("quote",V.Map()).Get("money",0).Double)+" / "+F(quote.Get("currency",0).Double));foreach(var pair in quote.Get("quote",V.Map()).Get("stock",V.Map()).Pairs)CivicUI.Pair(summary,Resource(pair.Key),F(pair.Value.Double)+" / "+F(quote.Get("available",V.Map()).Get(pair.Key,0).Double));}CivicUI.Label(summary,quote.Get("reason","").Text,"c-warning");CivicSource(summary,"启动报价与规则来源",quote);
  if(stage<2)CivicUI.Button(actions,"下一步 · "+(stage==0?"选择规约":"选择地点"),()=>{_districtStartStage=stage+1;RenderPage();},true);
  else CivicUI.Button(actions,"建立首地区",()=>WorldCommand(session=>session.BeginDistrict(_arcLanding,_arcSpecialization,_arcName,_arcSponsor,_arcOpportunity)),true);
 }
 void CivicBuildingDetails(VisualElement body,VisualElement actions,string id) {
  if(!S["projects"].Has(id)){CivicUI.Empty(body,"请选择一座建筑");return;}
  SelectedBuilding=id;var p=S["projects"][id];var st=G.project_stats(id);var data=TileSummary.describe(G,p["tile"].Text);var settled=data.Get("settlement_available",false).Bool&&S.Get("simulation_tick",0).Long>0;bool editable=Runtime.ViewCanEditFocused;
  var identity=CivicUI.Section(body,G.project_name(id),"T"+p.Get("tier",1).Long+" · "+G.c.operators[p["operator_org"]]["name"].Text+" · "+p["tile"].Text);
  var upgrading=p.Get("upgrade_job",V.Map()).Count>0;var construction=p["status"].Text=="construction"||upgrading;CivicUI.Label(identity,p.Get("paused",false).Bool?"已暂停":construction?(upgrading?"升级施工 · 原楼降效运营":"正在建设"):"当前运营",p.Get("paused",false).Bool?"c-warning":"c-good");
  var cell=data.Get("cell",V.Map());CivicUI.Pair(identity,"地形与特征",TileTerrain(cell.Get("terrain","plain").Text)+" · "+G.c.tags_text(cell.Get("traits",V.Arr())));
  if(construction) {
   var job=upgrading?p["upgrade_job"]:p["job"];var box=CivicUI.Section(body,upgrading?"升级进度":"建设进度");CivicUI.Meter(box,"已完成",job["total"].Double-job["work"].Double,job["total"].Double,"CP");CivicUI.Pair(box,"尚需材料",F(job["material"].Double));CivicUI.Pair(box,"共享施工能力",F(Construction.capacity(G))+" CP/月");var row=CivicUI.Row(box);foreach(var priority in new[]{-1,0,1}){var value=priority;var button=CivicUI.Button(row,value<0?"延后":value>0?"优先":"普通",()=>Command(g=>Construction.set_priority(g,id,value)),enabled:editable);button.EnableInClassList("is-selected",p.Get("construction_priority",0).Long==value);}CivicUI.Button(box,"全城施工队列",()=>Navigate("construction"));
  }
  var use=S["metrics"].Get("utilization",V.Map()).Get(id,V.Map());var run=CivicUI.Section(body,"运行与瓶颈",settled?"最近实际周期":"等待首次实际结算");if(settled)CivicUI.Meter(run,"实际运行",use.Get("utilization",p.Get("usage",0)).Double,1);else CivicUI.Label(run,"实际运行 · 待结算","c-muted");CivicUI.Label(run,use.Get("bottleneck",p.Get("bottleneck","等待实际结算")).Text,"c-warning");CivicUI.Pair(run,"岗位容量",F(st.Get("jobs",0).Double)+" 人");
  var labor=CivicUI.Row(run);foreach(var priority in new[]{-1,0,1}){var value=priority;var b=CivicUI.Button(labor,value<0?"让出人手":value>0?"优先供人":"普通供人",()=>Command(g=>g.set_priority(id,value)),enabled:editable);b.EnableInClassList("is-selected",p.Get("work_priority",0).Long==value);}
  var people=data["people"];var capacities=CivicUI.Row(run,"c-wrap");CivicUI.Value(capacities,"本地居民",F(people["population"].Double)+" 人");CivicUI.Value(capacities,"居民就业",settled?F(people["resident_employed"].Double)+" 人":"待结算");CivicUI.Value(capacities,"岗位已使用",settled?F(people["workplace_jobs_filled"].Double)+" 人":"待结算");
  var economy=CivicUI.Section(body,"本地净收支","最近实际周期 · 每来生月");foreach(var resource in new[]{"food","material","goods"}) {var r=data["resources"].FirstOrDefault(x=>x["resource"].Text==resource)??V.Map();double net=r.Get("production",0).Double-r.Get("building_input",0).Double-r.Get("resident_consumption",0).Double-r.Get("construction_material",0).Double-r.Get("production_lost",0).Double;CivicUI.Pair(economy,Resource(resource),settled?Signed(net)+" /月":"待结算",settled&&net<0?"c-warning":"");}
  var cash=data["cash"];CivicUI.Pair(economy,"通货",settled?Signed(cash["income_attributed"].Double-cash["upkeep"].Double)+" /月":"待结算");CivicUI.Label(economy,"本格收入仅列可追溯税源；库存也受物流调拨影响。","c-small c-muted");
  CivicRecordsFold(economy,"真实收入、支出与活动明细",c=>{if(!settled)CivicUI.Label(c,"等待实际结算。","c-muted");foreach(var r in data["income_sources"])CivicUI.Pair(c,r.Get("name","收入来源").Text,Signed(r["rate"].Double)+" /月");foreach(var r in data["fiscal"])CivicUI.Pair(c,r.Get("name","维护支出").Text,"−"+F(r["rate"].Double)+" /月");foreach(var r in data["receipts"])CivicUI.Pair(c,(r["kind"].Text=="input"?"投入 → ":"产出 ← ")+Resource(r["resource"].Text),F(r.Get("rate",0).Double,3)+" /月"+(r["kind"].Text=="output"?" · 留 "+F(r.Get("kept",0).Double,3)+" / 损 "+F(r.Get("lost",0).Double,3):""));CivicSource(c,"活动原始凭据",data["receipts"]);});
  foreach(var resource in new[]{"food","material","goods"})if(G.stock_cap_for(id,resource)>0)CivicUI.Meter(run,Resource(resource)+"库存",p["inventory"].Get(resource,0).Double,G.stock_cap_for(id,resource));
  var environment=CivicUI.Section(body,"服务、环境与邻接",settled?"实际来源":"实际供给待结算");foreach(var kind in G.c.balance["service_kinds"]) {var cap=st.Get("service."+kind.Text,0).Double;if(cap<=0)continue;var actual=S["metrics"].Get("service_links",V.Arr()).Where(x=>x.Get("source","").Text==id&&x.Get("kind","")==kind).Sum(x=>x.Get("people",0).Double);if(settled){CivicUI.Pair(environment,Service(kind.Text)+"经本楼接入",F(actual)+" 人");CivicUI.Label(environment,"接入所属服务池的人数；不等同于本楼实际供给。额定容量 "+F(cap)+" 人。","c-small c-muted");}else CivicUI.Pair(environment,Service(kind.Text)+"供给能力",F(cap)+" 人 · 实际待结算");}
  foreach(var field in data["fields"]){var name=V.Map("medical","医疗","education","教育","entertainment","娱乐","commercial","消费","pollution","污染","heat","余热","employment","就业").Get(field["layer"],field["layer"]).Text;CivicRecordsFold(environment,name+" · 来源",c=>CivicBuildingFieldSources(c,field));}
  var energy=MapInspection.Describe(S,p["tile"].Text,"energy",includeNeighbors:false);CivicRecordsFold(environment,"电力供需 · 来源",c=>CivicBuildingFieldSources(c,energy));
  var adjacency=data["adjacency"];CivicUI.Pair(environment,"邻接加成",adjacency.Count==0?"暂无生效加成":Signed(adjacency.Sum(x=>x["amount"].Double)*100)+"% · "+adjacency.Count+" 处来源");foreach(var link in adjacency)CivicUI.Pair(environment,G.project_name(link["source_id"].Text)+" · "+link["tile"].Text,Signed(link["amount"].Double*100)+"%");
  var slots=CivicUI.Section(body,"构筑槽","真实安装实例与原提供方");var slotValues=G.slot_ids(id).ToArray();if(slotValues.Length==0)CivicUI.Label(slots,"当前建筑暂无槽位。","c-muted");foreach(var slotValue in slotValues){var slot=slotValue.Text;var instanceId=p["slots"].Get(slot,"").Text;var instance=S["bonus_instances"].Get(instanceId,V.Map());var bonus=instance.Get("bonus_id","").Text;var box=CivicUI.Section(slots,G.slot_label(id,slot),bonus==""?"空槽":G.c.bonuses.Get(bonus,V.Map()).Get("name",bonus).Text);CivicUI.Button(box,bonus==""?"选择蓝图":"管理 / 替换",()=>Navigate("slot",id+"|"+slot));if(instance.Count>0){CivicUI.Pair(box,"原提供方",G.c.operators.Get(instance.Get("provider_org",""),V.Map()).Get("name",instance.Get("provider_org","")).Text);CivicUI.Pair(box,"合同组织",G.c.operators.Get(instance.Get("contract_org",instance.Get("provider_org","")),V.Map()).Get("name",instance.Get("contract_org",instance.Get("provider_org",""))).Text);CivicSource(box,"实例来源与完整历史",instance);}}
  var residents=S["groups"].Values.Where(x=>x["host"].Text==id).ToArray();if(residents.Length>0)CivicRecordsFold(body,"本楼住户与实际生活",home=>{var total=residents.Sum(x=>x["count"].Double);CivicUI.Meter(home,"入住 / 住房",total,p["status"].Text=="running"?Math.Max(0,st.Get("housing",0).Double*st.Get("efficiency",1).Double):0,"人");foreach(var key in new[]{"health","happiness","education"})CivicUI.Pair(home,V.Map("health","健康","happiness","幸福","education","教育")[key].Text,F(residents.Sum(x=>x["count"].Double*x.Get(key,0).Double)/Math.Max(.001,total))+" /100");if(settled)foreach(var kind in new[]{"food","medical","education","commercial","entertainment"})CivicUI.Meter(home,kind=="food"?"实际口粮":Service(kind),residents.Sum(x=>x["count"].Double*x.Get("living",V.Map()).Get(kind,0).Double),total,"人");else CivicUI.Label(home,"实际口粮与服务 · 待结算","c-muted");CivicUI.Button(home,"居民与服务名册",()=>Navigate("population"));});
  var history=CivicUI.Section(body,"建筑历史");var events=p.Get("history",V.Arr());if(events.Count==0)CivicUI.Label(history,"尚无保存的历史事件。","c-muted");foreach(var entry in events)CivicUI.Pair(history,entry.Has("h")?F(entry["h"].Double,2)+" 来生月":"历史记录",CivicBuildingHistoryText(entry));
  CivicRecordsFold(body,"住户、规则与完整来源",c=>{foreach(var group in residents)CivicEvidence(c,group);CivicEvidence(c,G.compiled.Get("trace",V.Map()).Get(id,V.Map()));CivicEvidence(c,events);CivicEvidence(c,p);CivicEvidence(c,data);});
  CivicRecordsFold(body,"拆除建筑",c=>{CivicUI.Label(c,"支付20通货；住户重新安置，库存尝试转仓，仓容不足时记录实际损耗。","c-muted");CivicUI.Button(c,"拆除（需确认）",()=>Confirm("拆除「"+G.project_name(id)+"」？","支付20通货处理拆除。住户保留并重新安置；库存转仓不足的损耗会记录。",()=>{Command(g=>g.demolish(id),false);if(!S["projects"].Has(id)){SelectedBuilding="";CloseArchive();}}),enabled:editable);});
  bool wild=WildDesign.IsFarm(G,p);if(wild||Development.choices(G,id).Count>0)CivicUI.Button(actions,wild?"建筑星图":"成长与升级",()=>OpenUpgradeWindow(id),true,editable&&(wild||!construction));
  CivicUI.Button(actions,p.Get("paused",false).Bool?(construction?"恢复施工":"恢复运营"):(construction?"暂停施工":"暂停运营"),()=>Command(g=>g.set_paused(id,!p.Get("paused",false).Bool)),enabled:editable);CivicUI.Button(actions,"定位建筑",()=>{SelectBuilding(id);InspectMapFromPage();});
 }
 string CivicBuildingHistoryText(V entry) {
  if(entry.Has("text"))return entry["text"].Text;var kind=entry.Get("kind","").Text;
  if(entry.Has("from")&&entry.Has("to"))return (kind=="wild_progression"?"成长":"升级")+" · "+G.c.projects.Get(entry["from"],V.Map()).Get("name",entry["from"]).Text+" → "+G.c.projects.Get(entry["to"],V.Map()).Get("name",entry["to"]).Text;
  if(kind=="wild_node")return "完成建筑星图节点 · "+entry.Get("node","").Text;return entry.Get("kind","建筑事件").Text;
 }
 void CivicBuildingFieldSources(VisualElement parent,V field) {
  var rows=field.Get("rows",V.Arr());if(!field.Get("available",false).Bool)CivicUI.Label(parent,"实际数据待结算。","c-muted");
  foreach(var row in rows){var id=row.Get("source_id","").Text;var name=S["projects"].Has(id)?G.project_name(id):row.Get("label","来源").Text;CivicUI.Pair(parent,name,row.Get("display",row.Get("value","—")).Text);var tile=row.Get("source_tile","").Text;if(tile!=""&&S["cells"].Has(tile))CivicUI.Button(parent,"定位来源 · "+tile,()=>{InspectMapFromPage();if(S["projects"].Has(id))SelectBuilding(id);else SelectTile(tile);});}
  if(rows.Count==0)CivicUI.Label(parent,"暂无实际来源记录。","c-muted");CivicSource(parent,"完整来源与数值",field);
 }
}
}
