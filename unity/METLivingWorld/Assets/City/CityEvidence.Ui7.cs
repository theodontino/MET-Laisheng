using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace MET.City {
public sealed partial class CityEvidence {
 static bool Ui7Only=>Environment.GetCommandLineArgs().Contains("--validate-ui7");
 const BindingFlags Ui7Private=BindingFlags.Instance|BindingFlags.NonPublic;
 T Ui7Field<T>(string name)=>(T)typeof(CityInterface).GetField(name,Ui7Private).GetValue(city.Interface);
 void Ui7Call(string name,params object[] args)=>typeof(CityInterface).GetMethod(name,Ui7Private).Invoke(city.Interface,args);
 string Ui7Page=>Ui7Field<string>("_page");
 string Ui7Context=>Ui7Field<string>("_pageContext");
 bool Ui7TipOpen=>Ui7Field<RectTransform>("_deskTip").gameObject.activeInHierarchy;
 int Ui7Parents=>Ui7Field<Stack<KeyValuePair<string,string>>>("_navigation").Count;
 string Ui7Owner=>(string)typeof(CityInterface).GetMethod("ArchiveCategory",Ui7Private).Invoke(city.Interface,new object[]{Ui7Page});
 string[] Ui7Text()=>city.Interface.GetComponentsInChildren<Text>(false).Select(t=>t.text).ToArray();
 void Ui7Rail(string section){var button=Ui7Field<Dictionary<string,Button>>("_topRoutes")[section];Check("ui7_rail_enabled_"+section,button.IsInteractable());button.onClick.Invoke();}
 void Ui7Back(){var button=Ui7Field<Button>("_pageBack");Check("ui7_header_back_available",button.gameObject.activeInHierarchy&&button.IsInteractable());button.onClick.Invoke();}
 void Ui7Top(string label){var control=city.Interface.GetComponentsInChildren<Button>(false).First(b=>b.name=="HUD Resource "+label);Check("ui7_topbar_enabled_"+label,control.IsInteractable());control.onClick.Invoke();}
 void Ui7NavigationCheck(string page,string owner,int parents){Check("ui7_navigation_"+page,Ui7Page==page&&Ui7Owner==owner&&Ui7Parents==parents,V.Map("page",Ui7Page,"context",Ui7Context,"owner",Ui7Owner,"parents",Ui7Parents));}
 IEnumerator Ui7Checks(){
  Application.runInBackground=true;
  Check("ui7_fullscreen_1920_1080",Screen.fullScreen&&Screen.width==1920&&Screen.height==1080,V.Map("fullscreen",Screen.fullScreen,"width",Screen.width,"height",Screen.height));
  Check("ui7_focused_real_window",Application.isFocused&&!Application.isBatchMode);
  // First-city flow is still a supported system page, distinct from locked successor pages.
  city.Runtime.NewCity(7047);yield return null;
  Click("下一步 · 选择规约");yield return null;Click("下一步 · 选择地点");yield return null;Click("支付启动投入 · 建立地区");yield return null;Click("确立起源 · 安顿居民");city.Runtime.SetPaused(true);yield return null;
  Check("ui7_first_city_playable",city.Runtime.Game.s["phase"].Text=="play"&&city.Runtime.Session.Games.Count==1);
  city.Interface.OpenArchive("economy_home");yield return null;
  Check("ui7_first_tick_is_pending",city.Runtime.Game.s["simulation_tick"].Long==0&&city.Interface.TopbarResourceSummary("currency").Contains("待结算")&&city.Interface.TopbarResourceSummary("food").Contains("待结算")&&Ui7Text().Any(t=>t.Contains("待实际结算")));
  yield return Capture("ui7-first-tick-pending.png");
  string path=Environment.GetEnvironmentVariable("MET_ROGUE_INDUSTRY_REPORT");
  Check("ui7_earned_report_exists",!string.IsNullOrWhiteSpace(path)&&File.Exists(path));var sourceText=File.ReadAllText(path);var report=V.Parse(sourceText);
  Check("ui7_fixture_normal_command_provenance",report.Get("ok",false).Bool&&report.Get("scope","").Text.Contains("normal commands"));
  var checkpoints=report["checkpoints"];var earned=checkpoints["offered:industrial_byproducts"];
  RestoreContent(earned,"ui7_earned");yield return null;
  var before=CityGameplayState();
  foreach(var page in new[]{"population_home","construction","economy_home","bonus_home","tasks_home","records"}){
   city.Interface.OpenArchive(page);yield return null;Ui7NavigationCheck(page,page,0);
   Check("ui7_no_footer_"+page,!Ui7Field<RectTransform>("_footer").gameObject.activeInHierarchy);
   yield return Capture("ui7-common-"+page+".png");
  }
  Check("ui7_common_roots_readonly",CityGameplayState()==before);
  city.Interface.OpenArchive("tasks_home");yield return null;var activeTheme=Themes.view(city.Runtime.Game);
  Check("ui7_task_common_shows_earned_progress",activeTheme.Get("id","").Text!=""&&(activeTheme.Get("completed",false).Bool?Ui7Text().Any(t=>t.Contains("三个阶段已经完成")):activeTheme["goals"].All(goal=>Ui7Text().Any(t=>t.Contains(goal["label"].Text)))));
  yield return Capture("ui7-task-earned-progress.png");
  Ui7Call("CloseArchive");yield return null;
  typeof(CityInterface).GetField("_mapNumericLabelsVisible",Ui7Private).SetValue(city.Interface,false);
  Check("ui7_hidden_map_numbers_do_not_consume_escape",!city.Interface.HandleMapInspectionEscape());
  string previousLayer=city.Interface.Layer;city.Interface.SetLayer("employment");
  typeof(CityInterface).GetField("_mapNumericLabelsVisible",Ui7Private).SetValue(city.Interface,true);
  Check("ui7_visible_map_numbers_consume_escape",city.Interface.HandleMapInspectionEscape());
  Check("ui7_escape_hides_map_numbers",!city.Interface.MapNumericLabelsVisible);
  city.Interface.SetLayer(previousLayer);
  yield return Ui7NavigationChecks();
  yield return Ui7HoverChecks();
  yield return Ui7CommonActionChecks();
  yield return Ui7LayoutChecks();
  yield return Ui7RewardChecks(earned);
  if(checkpoints.Has("actual_DTzero_before_claim")){
   RestoreContent(checkpoints["actual_DTzero_before_claim"],"ui7_real_dt_zero");city.Interface.OpenArchive("tasks_home");yield return null;
   Check("ui7_earned_dt_zero",city.Runtime.Game.s["dt"].Double==0);yield return Capture("ui7-task-dt-zero.png");
   Click("周期回顾与街区结算 ›");yield return null;Ui7NavigationCheck("settlement","tasks_home",1);yield return Capture("ui7-task-capstone-ready.png");
   var claim=Control("正式挂牌 · 领取定型建筑");if(claim!=null&&claim.IsInteractable()){claim.onClick.Invoke();yield return null;Check("ui7_earned_capstone_claim_saved",city.Runtime.Game.s["block"].Get("capstone",V.Map()).Count>0&&Ui7Owner=="tasks_home");yield return Capture("ui7-task-capstone-claimed.png");}
  }
  var fullPath=Environment.GetEnvironmentVariable("MET_UI6_CITY_ARC_REPORT");
  Check("ui7_archived_world_fixture_exists",!string.IsNullOrWhiteSpace(fullPath)&&File.Exists(fullPath));var fullText=File.ReadAllText(fullPath);var fullReport=V.Parse(fullText);
  Check("ui7_archived_world_earned",fullReport.Get("ok",false).Bool&&fullReport.Get("city",V.Map()).Count>0);RestoreContent(fullReport["city"],"ui7_archived_world");yield return null;city.Interface.OpenArchive("records");yield return null;
  var archivedBefore=CityGameplayState();string pageBefore=Ui7Page;bool pausedBefore=city.Runtime.WorldPaused;bool decisionBefore=city.Runtime.DecisionOpen;
  foreach(var locked in new[]{"city","governance","network","science","city_project","district_start","block_start","overview"}){
   city.Interface.OpenArchive(locked);Ui7Call("Navigate",locked,"");Check("ui7_locked_route_"+locked,Ui7Page==pageBefore&&city.Runtime.WorldPaused==pausedBefore&&city.Runtime.DecisionOpen==decisionBefore&&CityGameplayState()==archivedBefore);
  }
  yield return Capture("ui7-archived-world-preserved.png");
  Check("ui7_source_reports_unchanged",File.ReadAllText(path)==sourceText&&File.ReadAllText(fullPath)==fullText);
  RestoreContent(earned,"ui7_manual_ready");city.Interface.OpenArchive("population_home");yield return null;Ui7Call("CloseArchive");
  Check("ui7_manual_export",city.Runtime.Export(Path.Combine(directory,"ui7-manual-city.json"))=="");
  File.WriteAllText(Path.Combine(directory,"ui7-scope.json"),V.Map("source_report",path,"archived_world_report",fullPath,"scope","UI7 native fullscreen Player, actual rendered screenshots, earned fixture restores, uGUI callback navigation and synthetic pointer/focus events with real unscaled dwell. No OS mouse/keyboard or human usability claim; fixtures never alter stock, eligibility, RNG or goal counters for presentation.").ToJson(true));
 }
 IEnumerator Ui7NavigationChecks(){
  var before=CityGameplayState();city.Interface.OpenArchive("population_home");yield return null;
  ClickPrefix("医疗 · 满足");yield return null;Check("ui7_population_shortage_filter",Ui7Page=="population"&&Ui7Context=="medical"&&Ui7Parents==1);
  Ui7Rail("construction");yield return null;Ui7Top("材料");yield return null;Ui7NavigationCheck("resources","economy_home",1);
  Click("粮食");yield return null;Click("制成品");yield return null;Check("ui7_resource_switch_has_one_parent",Ui7Parents==1&&Ui7Context=="goods");
  Ui7Back();yield return null;Ui7NavigationCheck("economy_home","economy_home",0);
  Ui7Rail("population_home");yield return null;Check("ui7_category_restores_filter",Ui7Page=="population"&&Ui7Context=="medical");
  Ui7Rail("population_home");yield return null;Ui7NavigationCheck("population_home","population_home",0);
  Ui7Top("材料");yield return null;var fiscalRoute=Ui7Field<Dictionary<string,Button>>("_topRoutes")["economy_home"];
  Check("ui7_resource_has_fiscal_rail_ownership",Ui7Owner=="economy_home"&&fiscalRoute.GetComponent<Image>().color!=Ui7Field<Dictionary<string,Button>>("_topRoutes")["construction"].GetComponent<Image>().color);
  var source=city.Interface.GetComponentsInChildren<Button>(false).FirstOrDefault(b=>b.GetComponentInChildren<Text>()?.text=="建筑与来源");
  Check("ui7_resource_has_building_source",source!=null);source.onClick.Invoke();yield return null;Check("ui7_building_keeps_fiscal_context",Ui7Page=="building"&&Ui7Owner=="economy_home");
  Ui7Back();yield return null;Check("ui7_local_back_restores_resource",Ui7Page=="resources"&&Ui7Context=="material"&&Ui7Parents==1);
  var selectedId=city.Runtime.Game.playable_ids().First().Text;city.Interface.SelectBuilding(selectedId);var tile=city.Interface.SelectedTile;
  Canvas.ForceUpdateCanvases();var scroll=city.Interface.GetComponentsInChildren<ScrollRect>(false).Where(r=>r.vertical&&r.content!=null&&r.viewport!=null).OrderByDescending(r=>r.content.rect.height-r.viewport.rect.height).First();Check("ui7_scrollable_resource_details",scroll.content.rect.height>scroll.viewport.rect.height+1);scroll.verticalNormalizedPosition=.37f;Ui7Call("InspectMapFromPage");yield return null;
  Check("ui7_map_view_preserves_selection",city.Interface.MapViewing&&city.Interface.SelectedTile==tile);Ui7Call("ReturnFromMapView");yield return null;
  Check("ui7_map_return_keeps_context_selection_scroll",Ui7Page=="resources"&&Ui7Context=="material"&&city.Interface.SelectedTile==tile&&Math.Abs(scroll.verticalNormalizedPosition-.37f)<.03f,V.Map("scroll",scroll.verticalNormalizedPosition,"selection",city.Interface.SelectedTile));
  Ui7Rail("tasks_home");yield return null;Click("成长路线");yield return null;Check("ui7_growth_peer_has_no_parent",Ui7Context=="growth"&&Ui7Parents==0);Click("已完成");yield return null;Check("ui7_completed_peer_has_no_parent",Ui7Context=="completed"&&Ui7Parents==0);
  Check("ui7_navigation_preserves_gameplay",CityGameplayState()==before);yield return Capture("ui7-navigation-resource-owner.png");
 }
 IEnumerator Ui7HoverChecks(){
  city.Interface.OpenArchive("economy_home");yield return null;city.Interface.HideDeskTip();EventSystem.current?.SetSelectedGameObject(null);
  var tip=city.Interface.GetComponentsInChildren<CityDeskTooltip>(false).First(t=>t.name=="HUD Resource 材料");var pointer=new PointerEventData(EventSystem.current);
  var before=CityGameplayState();double began=UnityEngine.Time.realtimeSinceStartupAsDouble;tip.OnPointerEnter(pointer);yield return new WaitForSecondsRealtime(.7f);
  var progress=city.Interface.GetComponentsInChildren<CityGraphic>(false).FirstOrDefault(g=>g.name=="Elapsed hover");
  Check("ui7_modal_topbar_real_dwell_pending",!Ui7TipOpen&&progress!=null&&progress.Ratio>.1f&&progress.Ratio<.9f);yield return Capture("ui7-hover-0700ms-pending.png");
  double remaining=2.15-(UnityEngine.Time.realtimeSinceStartupAsDouble-began);if(remaining>0)yield return new WaitForSecondsRealtime((float)remaining);yield return null;
  Check("ui7_modal_topbar_dwell_opens_after_two_seconds",Ui7TipOpen&&UnityEngine.Time.realtimeSinceStartupAsDouble-began>=2&&progress!=null&&!progress.gameObject.activeInHierarchy);
  var overlay=Ui7Field<RectTransform>("_deskTip");var close=Ui7Field<Button>("_deskTipClose");Check("ui7_tooltip_readable_close",overlay.GetComponent<Image>().color.a==1&&overlay.GetComponent<CanvasGroup>().alpha==1&&close.IsInteractable()&&overlay.rect.width<=480.1f&&overlay.rect.height<=460.1f&&Ui7Field<Text>("_deskTipCopy").text==tip.Body());
  yield return Capture("ui7-hover-2150ms-open.png");tip.OnPointerExit(pointer);city.Interface.SetDeskTipPointer(true);yield return new WaitForSecondsRealtime(.55f);
  Check("ui7_overlay_pointer_can_read",Ui7TipOpen);EventSystem.current?.SetSelectedGameObject(close.gameObject);close.onClick.Invoke();yield return null;
  Check("ui7_close_restores_source_focus",!Ui7TipOpen&&EventSystem.current.currentSelectedGameObject==tip.gameObject);
  city.Interface.HideDeskTip();EventSystem.current?.SetSelectedGameObject(null);
  Ui7Call("Confirm","UI7 input ownership","Read-only confirmation ownership check",(Action)(()=>{}));tip.OnPointerEnter(pointer);yield return new WaitForSecondsRealtime(2.15f);
  Check("ui7_confirmation_owns_tooltip_input",!Ui7TipOpen);tip.OnPointerExit(pointer);Click("取消");yield return null;
  foreach(var label in new[]{"人口","粮食","材料","制成品","通货"}){
   var quick=city.Interface.GetComponentsInChildren<CityDeskTooltip>(false).First(t=>t.name=="HUD Resource "+label);
   city.Interface.ShowDeskTip(quick,true);yield return null;Canvas.ForceUpdateCanvases();
   var copy=Ui7Field<Text>("_deskTipCopy");var viewport=Ui7Field<ScrollRect>("_deskTipScroll").viewport;
   Check("ui7_quick_summary_no_scroll_"+label,copy.rectTransform.rect.height<=viewport.rect.height+1,V.Map("content",copy.rectTransform.rect.height,"viewport",viewport.rect.height));
   if(label=="粮食")yield return Capture("ui7-food-quick-summary.png");
   city.Interface.HideDeskTip();EventSystem.current?.SetSelectedGameObject(null);
  }
  Check("ui7_hover_preserves_gameplay",CityGameplayState()==before);
  File.WriteAllText(Path.Combine(directory,"ui7-dwell-timeline.json"),V.Map("clock","Unity realtimeSinceStartupAsDouble","delay_seconds",2,"elapsed_seconds",UnityEngine.Time.realtimeSinceStartupAsDouble-began,"input","synthetic Unity IPointerEnter/Exit callbacks; production focus guard retained","screenshots",V.Arr("ui7-hover-0700ms-pending.png","ui7-hover-2150ms-open.png")).ToJson(true));
 }
 IEnumerator Ui7CommonActionChecks(){
  Ui7Top("人口");yield return null;Ui7NavigationCheck("population_home","population_home",0);
  var beforePaused=city.Runtime.Game.s.Get("migration_paused",false).Bool;Click(beforePaused?"恢复迁入":"暂停迁入");yield return null;
  Check("ui7_common_population_real_migration_action",city.Runtime.Game.s["migration_paused"].Bool!=beforePaused);Click(beforePaused?"暂停迁入":"恢复迁入");yield return null;
  Ui7Top("通货");yield return null;Ui7NavigationCheck("economy_home","economy_home",0);
  Check("ui7_finance_common_has_actual_operating_data",Ui7Text().Contains("经营收入")&&Ui7Text().Contains("维护支出")&&Ui7Text().Contains("经营净额"));
  Click("查看收支详表 ›");yield return null;Check("ui7_finance_detail_context",Ui7Page=="resources"&&Ui7Context=="currency"&&Ui7Parents==1);
  var state=CityGameplayState();foreach(var resource in new[]{"dt","population","food","material","goods","currency","cp","energy","met"}){
   string summary=city.Interface.TopbarResourceSummary(resource);Check("ui7_contextual_summary_"+resource,resource=="met"?summary.Contains("系统菜单"):summary.Contains("当前街区：")&&summary.Contains("点击："));
  }
  Check("ui7_summaries_readonly",CityGameplayState()==state);
 }
 IEnumerator Ui7LayoutChecks(){
  foreach(var scale in new[]{.8,1.0,1.2}){
   city.Runtime.SetDisplaySetting("ui_scale",V.From(scale));city.Interface.ApplyDisplay();city.Interface.OpenArchive("population_home");yield return null;yield return Capture("ui7-ui-scale-"+(int)(scale*100)+".png");
   Check("ui7_scale_keeps_fullscreen_"+(int)(scale*100),Screen.fullScreen&&Screen.width==1920&&Screen.height==1080);
  }
  city.Runtime.SetDisplaySetting("ui_scale",V.From(1.0));city.Interface.ApplyDisplay();
  foreach(var font in new[]{1.25,1.5}){city.Runtime.SetDisplaySetting("text_scale",V.From(font));city.Interface.ApplyDisplay();city.Interface.OpenArchive("economy_home");yield return null;yield return Capture("ui7-font-"+(int)(font*100)+".png");}
  city.Runtime.SetDisplaySetting("text_scale",V.From(1.0));city.Interface.ApplyDisplay();
 }
 IEnumerator Ui7RewardChecks(V earned){
  RestoreContent(earned,"ui7_reward_source");yield return null;var game=city.Runtime.Game;var pending=Bonus.pending(game);Check("ui7_earned_pending_rewards",pending.Count>0);
  city.Interface.OpenArchive("tasks_home");yield return null;Click("比较并领取奖励 ›");yield return null;
  var reward=pending[0];var candidate=reward["choices"][0];string bid=candidate["bonus_id"].Text,rid=reward["reward_id"].Text;var before=CityGameplayState();
  var choice=city.Interface.GetComponentsInChildren<Button>(false).First(b=>b.name=="Blueprint Candidate "+rid+" "+bid);choice.onClick.Invoke();yield return null;Check("ui7_reward_preselection_readonly",CityGameplayState()==before);
  Click(reward.Get("kind","copy").Text=="research"?"确认研究":"确认领取");yield return new WaitForSecondsRealtime(1.3f);
  Check("ui7_reward_saved_and_stays_tasks",Bonus.pending(game).Count==pending.Count-1&&Ui7Owner=="tasks_home"&&game.s["block"]["reward_choices"].Any(r=>r["reward_id"].Text==rid&&r["claimed"].Bool));yield return Capture("ui7-task-reward-claimed.png");
  Ui7Rail("bonus_home");yield return null;Ui7NavigationCheck("bonus_home","bonus_home",0);
  string installId=game.s["block"]["bonus_wallet"].Keys.FirstOrDefault(x=>Bonus.card(game,x)["scope"].Text=="block"&&BlueprintCharges(game,x)>0);Check("ui7_earned_installable_copy",installId!=null);
  var card=city.Interface.GetComponentsInChildren<RectTransform>(false).First(r=>r.name=="Owned bonus "+installId);var install=card.GetComponentsInChildren<Button>(false).First(b=>b.GetComponentInChildren<Text>()?.text=="选择目标并比较安装 ›");install.onClick.Invoke();yield return null;
  Check("ui7_bonus_install_explicit_context",Ui7Page=="bonuses"&&Ui7Owner=="bonus_home");yield return Capture("ui7-bonus-install-comparison.png");
  long copies=BlueprintCharges(game,installId);Click("安装 · 消耗1份");yield return null;Check("ui7_install_one_earned_copy",BlueprintCharges(game,installId)==copies-1&&city.Runtime.Session.Validate()=="");yield return Capture("ui7-bonus-installed.png");
 }
}
}
