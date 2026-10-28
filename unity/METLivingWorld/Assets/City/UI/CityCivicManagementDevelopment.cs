using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MET.City {
public sealed partial class CityInterface {
 string _managementTaskTab="current",_managementTaskSelection="",_managementTaskQuery="",_managementCompletedSelection="";
 string _managementRecordSelection="",_managementSourceSelection="",_managementSourceQuery="";
 readonly Dictionary<string,Vector2> _managementDevelopmentScrolls=new Dictionary<string,Vector2>();

 bool RenderCivicManagementDevelopment(VisualElement body,VisualElement actions){
  switch(_page){
   case "tasks_home":case "themes":case "milestones":ManagementDevelopmentTasks(body,actions);return true;
   case "bonus_home":case "bonuses":ManagementDevelopmentBonuses(body,actions);return true;
   case "archive_home":case "records":ManagementDevelopmentRecords(body,actions);return true;
   case "sources":ManagementDevelopmentSources(body);return true;
   case "content":ManagementDevelopmentContent(body,actions);return true;
   case "heritage":ManagementDevelopmentHeritage(body,actions);return true;
   default:return false;
  }
 }
 ScrollView ManagementDevelopmentScroll(VisualElement parent,string key){
  var scroll=new ScrollView(ScrollViewMode.Vertical){name="management-development-"+key,horizontalScrollerVisibility=ScrollerVisibility.Hidden,verticalScrollerVisibility=ScrollerVisibility.Auto,mouseWheelScrollSize=48};
  scroll.AddToClassList("c-management-scroll");scroll.style.flexGrow=1;scroll.style.minHeight=0;parent.Add(scroll);
  var fullKey=_page+"|"+key;var position=_managementDevelopmentScrolls.TryGetValue(fullKey,out var saved)?saved:Vector2.zero;
  // Restore after layout; subscribe afterwards so a detached/rebuilt view cannot erase its saved position.
  scroll.schedule.Execute(()=>{scroll.scrollOffset=position;scroll.verticalScroller.valueChanged+=_=>_managementDevelopmentScrolls[fullKey]=scroll.scrollOffset;});
  return scroll;
 }
 VisualElement ManagementDevelopmentPane(VisualElement parent,string title,float percent){
  var pane=CivicUI.Column(parent);pane.AddToClassList("c-management-list");pane.style.flexBasis=Length.Percent(percent);pane.style.width=Length.Percent(percent);pane.style.flexGrow=1;pane.style.minWidth=0;pane.style.minHeight=0;
  CivicUI.Label(pane,title,"c-management-header c-heading");return pane;
 }
 void ManagementDevelopmentRow(VisualElement parent,bool selected,Action choose,params string[] cells){
  var row=CivicUI.Button(parent,"",choose);row.AddToClassList("c-management-row");row.style.flexDirection=FlexDirection.Row;row.EnableInClassList("is-selected",selected);
  foreach(var text in cells)CivicUI.Label(row,text,"c-management-cell");
 }
 void ManagementDevelopmentHeading(VisualElement parent,params string[] cells){var row=CivicUI.Row(parent,"c-management-header");foreach(var text in cells)CivicUI.Label(row,text,"c-management-cell c-small c-muted");}
 bool ManagementDevelopmentMatch(string text,string query)=>string.IsNullOrWhiteSpace(query)||text.IndexOf(query.Trim(),StringComparison.OrdinalIgnoreCase)>=0;
 string ManagementDevelopmentGoalReading(V goal){var factor=goal.Get("unit","").Text=="%"?100:1;return F(goal["value"].Double*factor,1)+goal["unit"].Text+" / "+(goal["comparison"].Text=="max"?"≤":"≥")+F(goal["target"].Double*factor,1)+goal["unit"].Text;}

 void ManagementDevelopmentTasks(VisualElement body,VisualElement actions){
  var theme=Themes.view(G);var pending=Bonus.pending(G);
  if(_page=="milestones"||_pageContext=="growth")_managementTaskTab="growth";else if(_pageContext=="completed")_managementTaskTab="completed";else if(_pageContext=="current")_managementTaskTab="current";
  var toolbar=CivicUI.Row(body,"c-management-toolbar");
  foreach(var option in new[]{new[]{"current","当前事项"},new[]{"growth","三阶段路线"},new[]{"completed","已保存成果"}}){var id=option[0];CivicSelect(toolbar,option[1],_managementTaskTab==id,()=>{_managementTaskTab=id;_pageContext=id;if(_page=="milestones")_page="tasks_home";RenderPage();});}
  CivicUI.Label(toolbar,theme.Get("name","未选主题").Text+" · "+Clock.summary(G)["date"].Text+" · 待领取 "+pending.Count+" 份","c-small c-muted");
  if(pending.Count>0)CivicUI.Button(actions,"选择奖励 · "+pending.Count,()=>OpenRewardModal(pending[0]["reward_id"].Text),true,!_blueprintBusy&&Runtime.ViewCanEditFocused);
  if(Runtime.MandatoryPending)CivicUI.Button(actions,"处理周期结算",()=>Navigate("settlement"));
  if(theme.Get("id","").Text==""){var choices=ManagementDevelopmentScroll(body,"theme-choices");CivicThemeChoices(choices,theme);return;}
  if(_managementTaskTab=="completed"){ManagementDevelopmentCompleted(body,theme);return;}
  if(_managementTaskTab=="growth"){
   var route=ManagementDevelopmentScroll(body,"growth-route");CivicStageRoute(route,theme,true);
   var reforge=CivicUI.Section(route,"发展方向与重铸","持有 "+theme["reforge_tokens"].Long+" 份重铸权");
   CivicUI.Label(reforge,"消耗1份重铸权与 "+F(theme["reforge_dt_cost"].Double,2)+" DT；已取得与待领取成果保留。","c-warning");
   var options=CivicUI.Row(reforge,"c-wrap");foreach(var choice in theme["choices"]){var id=choice["id"].Text;if(id==theme["id"].Text)continue;CivicUI.Button(options,"改向「"+choice["name"].Text+"」",()=>Confirm("确认重铸主题？","消耗1份重铸权与 "+F(theme["reforge_dt_cost"].Double,2)+" DT，已有与待领奖励保留。",()=>Command(g=>Themes.reforge(g,id))),enabled:theme["can_reforge"].Bool&&Runtime.ViewCanEditFocused);}
   CivicSource(route,"完整主题定义与来源",Themes._definition(G,theme["id"].Text));return;
  }
  var columns=CivicUI.Row(body,"c-management-layout");var roster=ManagementDevelopmentPane(columns,"目标与合同",62);var inspector=ManagementDevelopmentPane(columns,"选中事项 · 条件与办理",36);
  var list=ManagementDevelopmentScroll(roster,"tasks-list");var detail=ManagementDevelopmentScroll(inspector,"tasks-detail");
  var items=new List<V>();
  if(!theme["completed"].Bool){var stage=theme["stages"][(int)theme["stage"].Long-1];
   CivicUI.Label(list,"M"+theme["stage"].Long+" · "+stage["name"].Text+" · "+ThemeStageReward(stage),"c-heading");
   if(stage["hold_months"].Double>0)CivicUI.Meter(list,"连续稳定",stage["held_months"].Double,stage["hold_months"].Double,"来生月","全部状态同时达标才累计；失守会重新计时。");
   foreach(var goal in stage["goals"])items.Add(V.Map("key","goal:"+goal["id"].Text,"type","goal","value",goal));
  }else CivicUI.Label(list,"三个成长阶段已完成；成果以保存凭据为准。","c-good");
  foreach(var evt in theme["events"])items.Add(V.Map("key","event:"+evt["id"].Text,"type","event","value",evt));
  if(items.Count==0){CivicUI.Empty(list,"当前没有待办理事项","已保存的成果可在上方成果账簿查阅。");CivicSource(detail,"主题定义与来源",Themes._definition(G,theme["id"].Text));return;}
  if(!items.Any(x=>x["key"].Text==_managementTaskSelection))_managementTaskSelection=items[0]["key"].Text;
  ManagementDevelopmentHeading(list,"事项","实际进度 / 状态","条件 / 收益");
  foreach(var item in items){var key=item["key"].Text;var data=item["value"];bool goal=item["type"].Text=="goal";
   ManagementDevelopmentRow(list,key==_managementTaskSelection,()=>{_managementTaskSelection=key;RenderPage();},data.Get(goal?"label":"name","事项").Text,goal?ManagementDevelopmentGoalReading(data):data["status"].Text=="offered"?"待决定":data.Get("target",0).Double>0?F(data["progress"].Double,2)+" / "+F(data["target"].Double,2):"进行中",goal?(data["met"].Bool?"已满足":ThemeGoalHint(data["id"].Text)):TaskChoiceOutcome(data["definition"],"accepted",data["description"].Text));
  }
  var selected=items.First(x=>x["key"].Text==_managementTaskSelection);
  if(selected["type"].Text=="goal"){CivicGoal(detail,theme,selected["value"],true);CivicSource(detail,"此目标的完整来源",selected["value"],Themes._definition(G,theme["id"].Text));}
  else CivicContract(detail,selected["value"]);
 }
 void ManagementDevelopmentCompleted(VisualElement body,V theme){
  var toolbar=CivicUI.Row(body,"c-management-toolbar");CivicUI.Field(toolbar,"搜索成果",_managementTaskQuery,v=>_managementTaskQuery=v);CivicUI.Button(toolbar,"搜索",RenderPage);if(_managementTaskQuery!="")CivicUI.Button(toolbar,"清除",()=>{_managementTaskQuery="";RenderPage();});
  var records=theme.Get("history",V.Arr()).Reverse().Where(x=>ManagementDevelopmentMatch(x.ToJson(),_managementTaskQuery)).ToArray();
  var columns=CivicUI.Row(body,"c-management-layout");var list=ManagementDevelopmentScroll(ManagementDevelopmentPane(columns,"阶段、合同与改向账簿 · "+records.Length,62),"completed-list");var detail=ManagementDevelopmentScroll(ManagementDevelopmentPane(columns,"保存凭据",36),"completed-detail");
  if(records.Length==0){CivicUI.Empty(list,"暂无匹配的已保存记录","当前达标读数不会补写历史成果。");return;}
  if(!records.Any(x=>x.ToJson()==_managementCompletedSelection))_managementCompletedSelection=records[0].ToJson();
  ManagementDevelopmentHeading(list,"来生月","成果 / 事项","记录类型");foreach(var record in records){var key=record.ToJson();ManagementDevelopmentRow(list,key==_managementCompletedSelection,()=>{_managementCompletedSelection=key;RenderPage();},F(record.Get("local_seconds",0).Double/Themes.MONTH_SECONDS,2),record.Get("name",record.Get("id","主题记录")).Text,record.Get("status_label",record.Get("kind","已保存")).Text);}
  var chosen=records.First(x=>x.ToJson()==_managementCompletedSelection);CivicUI.Label(detail,chosen.Get("name",chosen.Get("id","主题记录")).Text,"c-heading");
  if(chosen.Get("kind","").Text=="milestone")CivicUI.Label(detail,chosen.Get("id","").Text.EndsWith(":M1",StringComparison.Ordinal)?"阶段奖励：方案副本选择提案":"阶段奖励：蓝图研究提案","c-good");
  CivicUI.Label(detail,chosen.Get("reason","").Text);CivicEvidence(detail,chosen);CivicUI.Button(detail,"领取提案与办理街区定型",()=>Navigate("settlement"));
 }

 void ManagementDevelopmentBonuses(VisualElement body,VisualElement actions){
  var toolbar=CivicUI.Row(body,"c-management-toolbar");foreach(var option in new[]{new[]{"available","库存副本"},new[]{"deployed","已安装"},new[]{"all","全部方案"}}){var id=option[0];CivicSelect(toolbar,option[1],_v3BonusFilter==id,()=>{_v3BonusFilter=id;RenderPage();});}
  CivicUI.Field(toolbar,"搜索方案",_blueprintQuery,v=>_blueprintQuery=v);CivicUI.Button(toolbar,"搜索",RenderPage);if(_blueprintQuery!="")CivicUI.Button(toolbar,"清除",()=>{_blueprintQuery="";RenderPage();});
  var pending=Bonus.pending(G);if(pending.Count>0)CivicUI.Button(actions,"选择奖励 · "+pending.Count,()=>OpenRewardModal(pending[0]["reward_id"].Text),true,Runtime.ViewCanEditFocused&&!_blueprintBusy);
  var columns=CivicUI.Row(body,"c-management-layout");var inventory=ManagementDevelopmentScroll(ManagementDevelopmentPane(columns,"方案库存",29),"bonus-inventory");var targets=ManagementDevelopmentScroll(ManagementDevelopmentPane(columns,"目标建筑 / 运营商与槽位",32),"bonus-targets");var comparison=ManagementDevelopmentScroll(ManagementDevelopmentPane(columns,"安装比较与确认",37),"bonus-comparison");
  var ids=FilteredBlueprintIds();if(ids.Length==0){CivicUI.Empty(inventory,"没有符合筛选的方案","切换全部方案或清除搜索。");return;}
  if(!ids.Contains(_inventoryChoice))_inventoryChoice=ids[0];
  ManagementDevelopmentHeading(inventory,"方案 / 等级","库存 / 已装");foreach(var id in ids){var bid=id;var card=Bonus.card(G,bid);ManagementDevelopmentRow(inventory,bid==_inventoryChoice,()=>{_inventoryChoice=bid;_blueprintVersion="";_installView=V.Map();RenderPage();},card["name"].Text+" · "+card["grade"].Text,card["copies"].Long+" / "+Bonus.active_count(G,bid));}
  var selected=Bonus.card(G,_inventoryChoice);var scope=selected["scope"].Text;_blueprintScope=scope;CivicUI.Label(targets,selected["name"].Text+" · "+ScopeName(scope),"c-heading");CivicUI.Label(targets,BlueprintEffectSummary(selected));
  var valid=ManagementDevelopmentBonusTargets(targets,selected);
  if(valid)CivicBonusPreview(comparison,selected,scope,_blueprintHost,_blueprintSlot);else CivicUI.Empty(comparison,"先选择适用目标","当前比较只读取分离快照，确认安装才消耗副本。");
  CivicUI.Label(comparison,selected.Get("preview_reason","").Text,"c-small c-muted");CivicUI.Label(comparison,selected.Get("stacking_note","").Text,"c-small c-muted");CivicRecordsFold(comparison,"规则说明与完整方案来源",deep=>{CivicEffectRows(deep,selected.Get("effects",V.Arr()));CivicEvidence(deep,G.c.bonuses[selected["bonus_id"].Text]);});
 }
 bool ManagementDevelopmentBonusTargets(VisualElement parent,V card){
  var bid=card["bonus_id"].Text;var scope=card["scope"].Text;
  if(scope=="building"){
   var rows=new List<V>();foreach(var raw in G.playable_ids()){var host=raw.Text;foreach(var slotValue in G.slot_ids(host)){var slot=slotValue.Text;var old=S["projects"][host]["slots"][slot].Text;if(G.bonus_eligible(bid,"building",host,slot,old)!=""&&S["bonus_instances"].Get(old,V.Map()).Get("bonus_id","").Text!=bid)continue;rows.Add(V.Map("host",host,"slot",slot));}}
   if(rows.Count==0){CivicUI.Empty(parent,"暂无适配建筑与槽位","建设适用建筑并开放对应槽位后即可安装。");CivicUI.Button(parent,"前往建设",()=>Navigate("construction"));return false;}
   if(!rows.Any(x=>x["host"].Text==_blueprintHost&&x["slot"].Text==_blueprintSlot)){var first=rows.FirstOrDefault(x=>x["host"].Text==SelectedBuilding)??rows[0];_blueprintHost=first["host"].Text;_blueprintSlot=first["slot"].Text;}
   ManagementDevelopmentHeading(parent,"建筑 / 槽位","已安装");foreach(var row in rows){var host=row["host"].Text;var slot=row["slot"].Text;ManagementDevelopmentRow(parent,host==_blueprintHost&&slot==_blueprintSlot,()=>{_blueprintHost=host;_blueprintSlot=slot;_blueprintVersion="";RenderPage();},G.project_name(host)+" · "+S["projects"][host]["tile"].Text+"\n"+G.slot_label(host,slot),CivicSlotBonusName(host,slot));}
   var links=CivicUI.Row(parent,"c-wrap");var currentHost=_blueprintHost;var currentSlot=_blueprintSlot;CivicUI.Button(links,"定位目标",()=>{SelectBuilding(currentHost);InspectMapFromPage();});CivicUI.Button(links,"管理选中槽位",()=>Navigate("slot",currentHost+"|"+currentSlot));
  }else if(scope=="block"){_blueprintHost=S["block"]["id"].Text;_blueprintSlot="";CivicUI.Label(parent,"安装目标 · "+G.project_name(_blueprintHost),"c-heading");}
  else {var hosts=Rules.ids(S["organizations"]).Select(x=>x.Text).ToArray();if(hosts.Length==0){CivicUI.Empty(parent,"暂无适用运营商");return false;}if(!hosts.Contains(_blueprintHost))_blueprintHost=hosts[0];_blueprintSlot="";foreach(var host in hosts){var id=host;ManagementDevelopmentRow(parent,id==_blueprintHost,()=>{_blueprintHost=id;_blueprintVersion="";RenderPage();},G.project_name(id));}}
  var installed=S["bonus_instances"].Values.Where(x=>x.Get("active",false).Bool&&x.Get("bonus_id","").Text==bid).ToArray();
  if(installed.Length>0){var box=CivicUI.Section(parent,"该方案已安装 · "+installed.Length+" 份");foreach(var instance in installed){CivicUI.Pair(box,G.project_name(instance["host_id"].Text),instance["host_type"].Text=="building"?G.slot_label(instance["host_id"].Text,instance["slot"].Text):ScopeName(instance["host_type"].Text));CivicSource(box,"安装实例与来源",instance);}}
  return true;
 }

 void ManagementDevelopmentRecords(VisualElement body,VisualElement actions){
  var all=AllCityRecords();var toolbar=CivicUI.Row(body,"c-management-toolbar");foreach(var category in new[]{"all","voices","alerts","archives","log","civic"}){var id=category;CivicSelect(toolbar,(id=="all"?"全部":RecordCategoryName(id))+" "+(id=="all"?all.Length:all.Count(x=>x["category"].Text==id)),_recordsCategory==id,()=>{_recordsCategory=id;RenderPage();});}
  CivicUI.Field(toolbar,"搜索记录",_recordsQuery,v=>_recordsQuery=v);CivicUI.Button(toolbar,"搜索",RenderPage);if(_recordsQuery!="")CivicUI.Button(toolbar,"清除",()=>{_recordsQuery="";RenderPage();});
  // Only comparable local timestamps are ordered; timeless records retain their source order.
  var rows=all.Where(x=>(_recordsCategory=="all"||x["category"].Text==_recordsCategory)&&ManagementDevelopmentMatch(RecordCategoryName(x["category"].Text)+x["record"].ToJson(),_recordsQuery)).OrderByDescending(x=>ManagementDevelopmentRecordTime(x["record"])).ToArray();
  var columns=CivicUI.Row(body,"c-management-layout");var list=ManagementDevelopmentScroll(ManagementDevelopmentPane(columns,"时间顺序 · "+rows.Length+" 条",52),"record-list");var detail=ManagementDevelopmentScroll(ManagementDevelopmentPane(columns,"选中记录 · 正文与来源",46),"record-detail");
  if(rows.Length==0){CivicUI.Empty(list,"暂无匹配记录","缩短关键词或切换分类。");return;}
  if(!rows.Any(x=>x.ToJson()==_managementRecordSelection))_managementRecordSelection=rows[0].ToJson();
  ManagementDevelopmentHeading(list,"来生月 / 来源","记录正文");foreach(var row in rows){var key=row.ToJson();var time=ManagementDevelopmentRecordTime(row["record"]);ManagementDevelopmentRow(list,key==_managementRecordSelection,()=>{_managementRecordSelection=key;RenderPage();},(time>=0?F(time,2)+" 月 · ":"")+RecordCategoryName(row["category"].Text),RecordTitle(row));}
  var selected=rows.First(x=>x.ToJson()==_managementRecordSelection);var record=selected["record"];var categoryId=selected["category"].Text;CivicUI.Label(detail,RecordTitle(selected),"c-heading");CivicUI.Label(detail,RecordCategoryName(categoryId),"c-small c-muted");
  if(categoryId=="archives"){CivicUI.Pair(detail,"人口",F(record.Get("population",0).Double)+" 人");CivicUI.Pair(detail,"来生时长",Clock.duration(G,record.Get("local_elapsed_seconds",0).Double));CivicUI.Button(actions,"打开街区结算",()=>{_selectedRecord=record.Duplicate(true);Navigate("record");},true);}
  else if(categoryId=="civic"){CivicUI.Label(detail,record.Get("status","").Text,"c-good");CivicUI.Label(detail,record.Get("reading","").Text);CivicSource(detail,"城市成果来源",S.Get("city_goal",V.Map()));}
  else {if(record.Has("text"))CivicUI.Label(detail,record["text"].Text);if(record.Has("kind"))CivicUI.Pair(detail,"事件类型",record["kind"].Text);var building=record.Get("building_id","").Text;if(building!=""&&S["projects"].Has(building)){CivicUI.Pair(detail,"来源建筑",G.project_name(building));CivicUI.Button(actions,"定位来源",()=>{SelectBuilding(building);SetLayer(record.Get("layer","terrain").Text);InspectMapFromPage();},true);}if(record.Has("evidence"))CivicSource(detail,categoryId=="alerts"?"实际依据与满足率":"变化依据",record["evidence"]);}
  CivicUI.Label(detail,categoryId=="alerts"?"当前警报反映现在的问题。历史消息保留原发生含义。":"保存的历史只读，查看不会改变城市。","c-small c-muted");CivicSource(detail,"完整记录与来源",record);
 }
 double ManagementDevelopmentRecordTime(V record)=>record.Has("h")?record["h"].Double:record.Has("local_seconds")?record["local_seconds"].Double/Themes.MONTH_SECONDS:record.Has("local_elapsed_seconds")?record["local_elapsed_seconds"].Double/Themes.MONTH_SECONDS:-1;

 void ManagementDevelopmentSources(VisualElement body){
  var entries=new List<V>();foreach(var value in Rules.ids(G.c.operators)){var id=value.Text;entries.Add(V.Map("key","operator:"+id,"name",G.c.operators[id]["name"],"kind","运营商","data",G.c.operators[id],"actual",S["organizations"].Get(id,V.Map()),"operator",id));}
  entries.Add(V.Map("key","fields","name","场与逐地格贡献","kind","当前场","data",S["fields"],"actual",S["authored_fields"]));
  entries.Add(V.Map("key","services","name","服务池与就业分配","kind","最近实际周期","data",S["metrics"].Get("service_pools",V.Map()),"actual",V.Map("service_links",S["metrics"].Get("service_links",V.Arr()),"employment_links",S["metrics"].Get("employment_links",V.Arr()))));
  entries.Add(V.Map("key","audit","name","资源守恒与真实结算审计","kind","结算步 "+S["simulation_tick"].Long,"data",S["audit"]));
  foreach(var id in Rules.ids(G.c.projects))entries.Add(V.Map("key","project:"+id.Text,"name",G.c.projects[id].Get("name",id),"kind","建筑定义 · T"+G.c.projects[id].Get("tier",1).Long,"data",G.c.projects[id]));
  entries.Add(V.Map("key","compiled","name","编译后的规则与条件追踪","kind","完整来源","data",G.compiled));
  ManagementDevelopmentSourceBrowser(body,entries,(parent,entry)=>{
   var op=entry.Get("operator","").Text;if(op!=""){CivicUI.Label(parent,entry["data"].Get("summary","").Text);CivicUI.Pair(parent,"槽位成长",string.Join(" → ",entry["data"].Get("slot_track",V.Arr()).Select(x=>Catalog.SLOT_NAMES.Get(x,x).Text)));foreach(var project in S["projects"].Values.Where(x=>x.Get("operator_org","").Text==op&&!x.Get("special",false).Bool)){var bid=project["id"].Text;CivicUI.Button(parent,G.project_name(bid)+" · 定位",()=>{SelectBuilding(bid);InspectMapFromPage();});}}
   if(entry["key"].Text=="fields")foreach(var tile in Rules.ids(S["cells"])){var field=S["cells"][tile].Get("fields",V.Map());if(field.Count>0)CivicSource(parent,"地块 "+tile.Text+" · "+G.c.tags_text(S["cells"][tile]["traits"]),field);}
  });
 }
 void ManagementDevelopmentSourceBrowser(VisualElement body,List<V> entries,Action<VisualElement,V> describe){
  var toolbar=CivicUI.Row(body,"c-management-toolbar");CivicUI.Field(toolbar,"搜索名称与来源",_managementSourceQuery,v=>_managementSourceQuery=v);CivicUI.Button(toolbar,"搜索",RenderPage);if(_managementSourceQuery!="")CivicUI.Button(toolbar,"清除",()=>{_managementSourceQuery="";RenderPage();});
  var rows=entries.Where(x=>string.IsNullOrWhiteSpace(_managementSourceQuery)||ManagementDevelopmentMatch(x.ToJson(),_managementSourceQuery)).ToArray();var columns=CivicUI.Row(body,"c-management-layout");var list=ManagementDevelopmentScroll(ManagementDevelopmentPane(columns,"目录 · "+rows.Length+" 项",42),"source-list");var detail=ManagementDevelopmentScroll(ManagementDevelopmentPane(columns,"所选来源与保存记录",56),"source-detail");
  if(rows.Length==0){CivicUI.Empty(list,"暂无匹配来源");return;}var selectedKey=_page+"|";if(!rows.Any(x=>selectedKey+x["key"].Text==_managementSourceSelection))_managementSourceSelection=selectedKey+rows[0]["key"].Text;
  ManagementDevelopmentHeading(list,"条目","类别 / 来源");foreach(var item in rows){var key=selectedKey+item["key"].Text;ManagementDevelopmentRow(list,key==_managementSourceSelection,()=>{_managementSourceSelection=key;RenderPage();},item["name"].Text,item.Get("kind","").Text);}
  var chosen=rows.First(x=>selectedKey+x["key"].Text==_managementSourceSelection);CivicUI.Label(detail,chosen["name"].Text,"c-heading");CivicUI.Label(detail,chosen.Get("kind","").Text,"c-small c-muted");describe(detail,chosen);CivicSource(detail,"定义 / 保存来源",chosen.Get("data",V.Map()));if(chosen.Has("actual"))CivicSource(detail,"实际记录与完整依据",chosen["actual"]);
 }
 void ManagementDevelopmentContent(VisualElement body,VisualElement actions){
  var view=Runtime.Session.BuildLegacyView();var entries=new List<V>();
  var chosen=view.Get("chosen",V.Map());if(chosen.Count>0)entries.Add(V.Map("key","chosen","name",chosen.Get("name","本区已选成果"),"kind","本区已选择 · "+LegacyGrade(chosen),"data",chosen,"legacy",true));
  foreach(var item in view.Get("pending",V.Arr()))entries.Add(V.Map("key","pending:"+item.Get("profile_id",item.Get("id",item.ToJson())).Text,"name",item.Get("name",item.Get("profile_id","成果")),"kind","本区锁定资格 · "+LegacyGrade(item),"data",item,"legacy",true));
  var evidence=view.Get("evidence",V.Map());if(evidence.Count>0)entries.Add(V.Map("key","evidence","name","首次归零的构筑与实际供给","kind","锁定证据","data",evidence));
  foreach(var bid in G.c.bonuses.Keys){var card=Bonus.card(G,bid);entries.Add(V.Map("key","bonus:"+bid,"name",card["name"],"kind","方案图鉴 · "+card["grade"].Text,"data",G.c.bonuses[bid],"bonus",bid));}
  foreach(var category in new[]{"profiles","results"})foreach(var item in view.Get(category,V.Arr()))entries.Add(V.Map("key",category+":"+item.Get("source_block","").Text+":"+item.Get("profile_id",item.Get("id",item.ToJson())).Text,"name",item.Get("name",item.Get("profile_id",item.Get("id","成果"))),"kind",category=="profiles"?"规约方向":"同城成果 · "+LegacyBlockName(item.Get("source_block","").Text),"data",item,"legacy",true));
  ManagementDevelopmentSourceBrowser(body,entries,(parent,entry)=>{if(entry.Get("legacy",false).Bool)CivicLegacyDetails(parent,entry["data"]);var bid=entry.Get("bonus","").Text;if(bid!=""){var card=Bonus.card(G,bid);CivicUI.Pair(parent,"库存 / 蓝图",card["copies"].Long+" 份 / "+card["grade"].Text);CivicUI.Label(parent,BlueprintEffectSummary(card));CivicUI.Label(parent,card.Get("stacking_note","").Text,"c-muted");CivicEffectRows(parent,card.Get("effects",V.Arr()));CivicUI.Button(parent,"选择此方案并比较部署",()=>{_inventoryChoice=bid;_v3BonusFilter="all";_blueprintQuery="";Navigate("bonuses");});}else if(entry["key"].Text=="evidence"){var metrics=entry["data"].Get("metrics",V.Map());foreach(var key in new[]{"heat_used","pollution_recycled","medical","education"})if(metrics.Has(key))CivicUI.Pair(parent,Stat(key),F(metrics[key].Double,3));}});
  CivicUI.Button(actions,"办理本区成果与结算",()=>Navigate("settlement"));
 }
 void ManagementDevelopmentHeritage(VisualElement body,VisualElement actions){
  var entries=new List<V>();foreach(var evt in W.Get("history",V.Arr()).Reverse())entries.Add(V.Map("key","event:"+evt.ToJson(),"name",HistoryText(evt),"kind",evt.Has("h")?F(evt["h"].Double,2)+" 来生月":"城史事件","data",evt));
  foreach(var block in W["blocks"].Values)foreach(var pair in block.Get("state",V.Map()).Get("block",V.Map()).Get("bonus_wallet",V.Map()).Pairs){var charges=pair.Value.Get("charges",0).Long;if(charges<=0)continue;entries.Add(V.Map("key","wallet:"+block.Get("id",BlockName(block)).Text+":"+pair.Key,"name",G.c.bonuses.Get(pair.Key,V.Map()).Get("name",pair.Key),"kind","许可 · "+BlockName(block)+" · "+charges+" 份","data",pair.Value));}
  entries.Add(V.Map("key","legacy","name","地方、制度与城市能力","kind","永久城市来源","data",W.Get("legacy",V.Map())));entries.Add(V.Map("key","world","name","永久城市完整只读记录","kind","完整保存来源","data",W));
  ManagementDevelopmentSourceBrowser(body,entries,(parent,entry)=>{var payload=entry["data"].Get("payload",V.Map());if(payload.Has("text"))CivicUI.Label(parent,payload["text"].Text);if(payload.Get("event",V.Map()).Has("text"))CivicUI.Label(parent,payload["event"]["text"].Text);CivicUI.Label(parent,"历史保留实际发生的含义；跨街区转移暂未开放。","c-small c-muted");});CivicUI.Button(actions,"长期成果与来源",()=>Navigate("content"));
 }
}
}
