using System;
using System.Collections;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;
namespace MET.City {
public sealed partial class CityEvidence {
 static bool Ui6Only=>Environment.GetCommandLineArgs().Contains("--validate-ui6");
 IEnumerator Ui6Journey(){
  Application.runInBackground=true;
  Check("ui6_fullscreen",Screen.fullScreen&&Screen.width==1920&&Screen.height==1080);
  if(Environment.GetCommandLineArgs().Contains("--ui6-edge-states")){yield return Ui6EdgeStates();yield break;}
  yield return Capture("ui6-new-city.png");
  string source=Environment.GetEnvironmentVariable("MET_ROGUE_INDUSTRY_REPORT");
  Check("ui6_earned_fixture_exists",!string.IsNullOrEmpty(source)&&File.Exists(source));
  var journey=V.Parse(File.ReadAllText(source));var checkpoints=journey["checkpoints"];
  var earned=checkpoints["offered:industrial_byproducts"];
  RestoreContent(earned,"ui6_earned");yield return null;city.Interface.OpenArchive("overview");yield return null;Click("收起侧栏");yield return null;
  var camera=city.World.CameraController.GetComponent<Camera>();var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize;
  File.WriteAllText(Path.Combine(directory,"road-geometry.json"),city.World.ParcelEntranceGeometryEvidence.ToJson(true));
  yield return Capture("ui6-roads-all.png");
  city.World.SetParcelRoadEvidenceVisibility(false,true);yield return Capture("ui6-roads-steps-only.png");
  city.World.SetParcelRoadEvidenceVisibility(true,false);yield return Capture("ui6-roads-lanes-only.png");
  city.World.SetParcelRoadEvidenceVisibility(true,true);
  Check("ui6_road_camera_unchanged",camera.transform.position==position&&camera.transform.rotation==rotation&&camera.orthographicSize==size);
  Check("ui6_road_access",city.World.SettlementSiteDiagnostics["unreachable_access_terminals"].Long==0,city.World.SettlementSiteDiagnostics);
  if(Environment.GetCommandLineArgs().Contains("--roads-baseline"))yield break;
  Check("ui6_entries_inside_ring",city.World.ParcelEntranceGeometryEvidence["sites"].All(site=>site["entrance_beyond_ring_centerline"].Double<=0));
  yield return Ui6HoverVisualJourney();
  var id=city.Runtime.Game.playable_ids().Select(x=>x.Text).FirstOrDefault(x=>Development.choices(city.Runtime.Game,x).Count>0);if(id!=null)city.Interface.SelectBuilding(id);
  foreach(var page in new[]{"overview","economy_home","bonus_home","tasks_home","archive_home","resources","population","construction","building","upgrades","market","policies","bonuses","blueprints","themes","milestones","settlement","city","governance","network","science","city_project","records","heritage","content","settings","sources"}){
   city.Interface.OpenArchive(page);yield return null;var before=CityGameplayState();yield return Capture("ui6-page-"+page+".png");
   Check("ui6_browse_readonly_"+page,CityGameplayState()==before);
   foreach(var scroll in city.Interface.GetComponentsInChildren<ScrollRect>(false))Check("ui6_scroll_"+page+"_"+scroll.name,scroll.scrollSensitivity>=24);
  }
  yield return Ui6NavigationJourney();
  city.Interface.OpenArchive("resources");yield return null;
  var resource=city.Interface.GetComponentsInChildren<Button>(true).First(x=>x.name=="HUD Resource 材料");resource.onClick.Invoke();yield return null;
  Check("ui6_resource_direct_context",city.Interface.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("材料")&&t.gameObject.activeInHierarchy));
  var met=city.Interface.GetComponentsInChildren<Button>(true).First(x=>x.name=="HUD Brand");met.onClick.Invoke();yield return null;Check("ui6_brand_opens_settings",city.Interface.GetComponentsInChildren<Text>().Any(t=>t.text=="菜单与显示设置"));
  foreach(var scale in new[]{.8,1.0,1.2}){
   city.Runtime.SetDisplaySetting("ui_scale",V.From(scale));city.Interface.ApplyDisplay();city.Interface.OpenArchive("bonuses");yield return null;yield return Capture("ui6-scale-"+(int)(scale*100)+".png");
  }
  city.Runtime.SetDisplaySetting("ui_scale",V.From(1.0));city.Interface.ApplyDisplay();
  foreach(var font in new[]{1.25,1.5}){city.Runtime.SetDisplaySetting("text_scale",V.From(font));city.Interface.OpenArchive("resources");yield return null;yield return Capture("ui6-font-"+(int)(font*100)+".png");}
  city.Runtime.SetDisplaySetting("text_scale",V.From(1.0));city.Interface.ApplyDisplay();

  foreach(var key in new[]{"actual_DTzero_before_claim","paid_before_successor"})if(checkpoints.Has(key)){RestoreContent(checkpoints[key],"ui6_"+key);city.Interface.OpenArchive(key=="actual_DTzero_before_claim"?"settlement":"city");yield return null;yield return Capture("ui6-state-"+key+".png");}
  RestoreContent(earned,"ui6_bonus_check");
  var pending=Bonus.pending(city.Runtime.Game);var reward=pending[0];var candidate=reward["choices"][0];string bid=candidate["bonus_id"].Text;
  city.Interface.OpenArchive("blueprints");yield return null;
  var choice=city.Interface.GetComponentsInChildren<Button>().First(b=>b.name=="Blueprint Candidate "+reward["reward_id"].Text+" "+bid);choice.onClick.Invoke();yield return null;
  Check("ui6_reward_preselect_does_not_claim",Bonus.pending(city.Runtime.Game).Count==pending.Count);
  Click(reward.Get("kind","copy").Text=="research"?"确认研究":"确认领取");yield return new WaitForSecondsRealtime(1.3f);
  Check("ui6_reward_claim_advances_saved_queue",Bonus.pending(city.Runtime.Game).Count==pending.Count-1);
  city.Interface.OpenArchive("bonuses");yield return null;
  string installId=city.Runtime.Game.s["block"]["bonus_wallet"].Keys.First(x=>Bonus.card(city.Runtime.Game,x)["scope"].Text=="block"&&BlueprintCharges(city.Runtime.Game,x)>0);
  BlueprintInventoryButton(city.Interface,installId).onClick.Invoke();yield return null;yield return Capture("ui6-bonus-comparison.png");
  long copies=BlueprintCharges(city.Runtime.Game,installId);Click("安装 · 消耗1份");yield return null;
  Check("ui6_install_consumes_exactly_one_copy",BlueprintCharges(city.Runtime.Game,installId)==copies-1);Check("ui6_install_valid_city",city.Runtime.Session.Validate()=="");
  yield return Capture("ui6-bonus-installed.png");
  yield return Ui6CityArcInputJourney();
  RestoreContent(earned,"ui6_manual_ready");city.Interface.OpenArchive("overview");yield return null;Click("收起侧栏");
  Check("ui6_save_export",city.Runtime.Export(Path.Combine(directory,"ui6-manual-city.json"))=="");
 }

 Button[] Ui6FooterButtons()=>city.Interface.GetComponentsInChildren<Button>(true).Where(b=>b.gameObject.activeInHierarchy&&b.transform.parent?.name=="Archive Footer").ToArray();
 IEnumerator Ui6EdgeStates(){
  city.Runtime.NewCity(7040);yield return null;Click("下一步 · 选择规约");yield return null;Click("下一步 · 选择地点");yield return null;Click("支付启动投入 · 建立地区");yield return null;Click("确立起源 · 安顿居民");city.Runtime.SetPaused(true);yield return null;
  city.Interface.OpenArchive("resources");yield return null;
  Check("ui6_first_settlement_pending",city.Runtime.Game.s["simulation_tick"].Long==0&&city.Interface.GetComponentsInChildren<Text>().Any(t=>t.text=="尚未产生实际结算记录。"));yield return Capture("ui6-unsettled-ledger.png");
  var full=V.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("MET_UI6_CITY_ARC_REPORT")))["city"];RestoreContent(full,"ui6_power_shortage");
  var game=city.Runtime.Game;var providers=game.playable_ids().Select(x=>x.Text).Where(id=>game.project_stats(id).Get("output.energy",0).Double>0).ToArray();
  Check("ui6_shortage_has_real_power_provider",providers.Length>0);
  foreach(var id in providers)Check("ui6_pause_power_provider_"+id,city.Runtime.Execute(g=>g.set_paused(id,true))=="");
  city.Runtime.ResumeNormal();var error=city.Runtime.ExecuteWorld(s=>{var r=s.Advance(.25*s.c.beta["units"]["earth_seconds_per_month"].Double,"normal");return r.Get("steps",0).Long>0?"":r.Get("reason","").Text;});city.Runtime.SetPaused(true);yield return null;
  Check("ui6_actual_power_shortage",error==""&&game.s["metrics"].Get("energy_ratio",1).Double<1,game.s["metrics"]);
  city.Interface.OpenArchive("resources");yield return null;Click("电力");yield return null;var before=CityGameplayState();yield return Capture("ui6-power-shortage-ledger.png");Check("ui6_shortage_browsing_readonly",CityGameplayState()==before);
  RestoreContent(full,"ui6_slope_source");city.Interface.OpenArchive("overview");yield return null;Click("收起侧栏");yield return FabricFixtures();
 }
 IEnumerator Ui6HoverVisualJourney(){
  var target=city.Interface.GetComponentsInChildren<Button>(true).First(b=>b.name=="HUD Resource 材料").GetComponent<CityDeskTooltip>();
  var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
  target.OnPointerEnter(pointer);yield return new WaitForSecondsRealtime(.8f);
  var progress=city.Interface.GetComponentsInChildren<CityGraphic>(false).FirstOrDefault(g=>g.name=="Elapsed hover");
  Check("ui6_hover_progress_visible_pending",progress!=null&&progress.Ratio>.2f&&progress.Ratio<.9f);
  if(progress!=null){var corners=new Vector3[4];progress.rectTransform.GetWorldCorners(corners);Check("ui6_hover_progress_has_screen_area",progress.rectTransform.rect.width>0&&progress.rectTransform.rect.height>0,V.Map("rect",V.Arr(progress.rectTransform.rect.x,progress.rectTransform.rect.y,progress.rectTransform.rect.width,progress.rectTransform.rect.height),"corners",V.From(corners.Select(p=>V.Arr(p.x,p.y,p.z)).ToArray()),"ratio",progress.Ratio));}
  yield return Capture("ui6-hover-pending.png");yield return new WaitForSecondsRealtime(1.3f);
  Check("ui6_hover_progress_clears_on_open",progress!=null&&!progress.gameObject.activeInHierarchy);yield return Capture("ui6-hover-open.png");
  target.OnPointerExit(pointer);city.Interface.HideDeskTip();yield return null;
 }
 string[] Ui6FooterLabels()=>Ui6FooterButtons().Select(b=>b.GetComponentInChildren<Text>().text).ToArray();
 IEnumerator Ui6NavigationJourney(){
  foreach(var page in new[]{"overview","economy_home","bonus_home","tasks_home","archive_home"}){
   var before=CityGameplayState();city.Interface.OpenArchive(page);yield return null;var labels=Ui6FooterLabels();
   Check("ui6_sidebar_only_collapse_"+page,labels.SequenceEqual(new[]{"收起侧栏"}),V.From(labels));Click("收起侧栏");yield return null;
   Check("ui6_sidebar_collapse_closes_"+page,!city.Interface.IsModal&&Ui6FooterButtons().Length==0);
   Check("ui6_sidebar_navigation_readonly_"+page,CityGameplayState()==before);
  }
  foreach(var entry in new[]{new[]{"overview","永久城市 · 地区与长期发展 ›","城市状况"},new[]{"economy_home","打开资源收支面板","经营"},new[]{"bonus_home","打开 Bonus 部署面板","Bonus"},new[]{"tasks_home","打开任务与决定面板","任务"},new[]{"archive_home","打开城市记录面板","档案"}}){
   var before=CityGameplayState();city.Interface.OpenArchive(entry[0]);yield return null;Click(entry[1]);yield return null;
   var back="‹ 返回"+entry[2];var labels=Ui6FooterLabels();Check("ui6_detail_specific_parent_"+entry[0],labels.SequenceEqual(new[]{back,"关闭面板"}),V.From(labels));
   Click(back);yield return null;Check("ui6_detail_returns_to_sidebar_"+entry[0],Ui6FooterLabels().SequenceEqual(new[]{"收起侧栏"}));
   Click(entry[1]);yield return null;Click("关闭面板");yield return null;Check("ui6_detail_close_to_map_"+entry[0],!city.Interface.IsModal&&Ui6FooterButtons().Length==0);
   Check("ui6_detail_navigation_readonly_"+entry[0],CityGameplayState()==before);
  }
  city.Interface.OpenArchive("overview");yield return null;Click("永久城市 · 地区与长期发展 ›");yield return null;Click("查看承诺与治理");yield return null;
  Check("ui6_nested_governance_returns_to_city",Ui6FooterLabels().SequenceEqual(new[]{"‹ 返回永久城市与地区","关闭面板"}),V.From(Ui6FooterLabels()));
  Click("‹ 返回永久城市与地区");yield return null;Check("ui6_city_keeps_its_sidebar_parent",Ui6FooterLabels().SequenceEqual(new[]{"‹ 返回城市状况","关闭面板"}),V.From(Ui6FooterLabels()));Click("关闭面板");yield return null;
 }
 IEnumerator Ui6CityArcInputJourney(){
  var path=Environment.GetEnvironmentVariable("MET_UI6_CITY_ARC_REPORT");
  if(string.IsNullOrWhiteSpace(path)){File.WriteAllText(Path.Combine(directory,"ui6-fullcity-scope.json"),V.Map("executed",false,"reason","MET_UI6_CITY_ARC_REPORT not supplied; multi-region browsing and the new delivery-to-milestone UI journey were not exercised.").ToJson(true));yield break;}
  Check("ui6_fullcity_report_exists",File.Exists(path));var sourceText=File.ReadAllText(path);var report=V.Parse(sourceText);Check("ui6_fullcity_normal_command_report",report.Get("ok",false).Bool&&report.Get("city",V.Map()).Count>0,report.Get("checks",V.Arr()));
  var world=report["city"];RestoreContent(world,"ui6_full_city");yield return null;var session=city.Runtime.Session;
  Check("ui6_fullcity_multiblock_and_autonomy",session.Games.Count>=4&&session.s["districts"].Count>=2&&session.s["districts"].Values.Any(d=>d["id"].Text!=session.ActiveDistrictId&&d["status"].Text=="autonomous"));
  var before=CityGameplayState();foreach(var page in new[]{"city","governance","network"}){city.Interface.OpenArchive(page);yield return null;yield return Capture("ui6-fullcity-"+page+".png");Check("ui6_fullcity_browse_readonly_"+page,CityGameplayState()==before);}
  var autonomous=session.s["districts"].Values.First(d=>d["id"].Text!=session.ActiveDistrictId&&d["status"].Text=="autonomous");city.Interface.OpenArchive("city");yield return null;
  var districtButton=city.Interface.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.GetComponentInChildren<Text>()?.text.Contains(autonomous["name"].Text)==true&&b.GetComponentInChildren<Text>().text.Contains(" · 自治"));
  Check("ui6_fullcity_autonomous_district_ui",districtButton!=null&&districtButton.IsInteractable());districtButton.onClick.Invoke();yield return null;yield return Capture("ui6-fullcity-autonomous-district.png");Check("ui6_fullcity_district_selection_readonly",CityGameplayState()==before);
  Click("查看承诺与治理");yield return null;Check("ui6_fullcity_autonomous_governance_return_action",Control("支付成本 · 回任此地区")!=null);yield return Capture("ui6-fullcity-autonomous-governance.png");Check("ui6_fullcity_autonomous_governance_readonly",CityGameplayState()==before);
  Check("ui6_fullcity_report_source_unchanged",File.ReadAllText(path)==sourceText);
  File.WriteAllText(Path.Combine(directory,"ui6-fullcity-scope.json"),V.Map("executed",true,"source_report",path,"blocks",session.Games.Count,"districts",session.s["districts"].Count,"autonomous_district",autonomous["id"],"scope","Earned whole-city restore, city/governance/network and autonomous-district UI browsing. Snapshot equality includes wallets, RNG, projects, source records and focused block; no map-focus or government command invoked.").ToJson(true));
  yield return Ui6DeliveryMilestoneJourney(world);
 }
 IEnumerator Ui6ClaimTaskRewards(string label){
  int count=0;while(Bonus.pending(city.Runtime.Game).Count>0){
   Check("ui6_task_reward_queue_progress_"+label,count++<24);var game=city.Runtime.Game;var reward=Bonus.pending(game)[0];var rid=reward["reward_id"].Text;var candidate=reward["choices"][0];var bid=candidate["bonus_id"].Text;var copies=bid=="__currency__"?0:BlueprintCharges(game,bid);var level=game.s["block"].Get("blueprint_levels",V.Map()).Get(bid,0).Long;var cash=game.s["currency"].Double;
   city.Interface.OpenArchive("blueprints");yield return null;var button=city.Interface.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.name=="Blueprint Candidate "+rid+" "+bid);Check("ui6_task_reward_candidate_"+rid,button!=null&&button.IsInteractable());button.onClick.Invoke();yield return null;
   Check("ui6_task_reward_preselect_readonly_"+rid,Bonus.pending(game).Any(r=>r["reward_id"].Text==rid));Click(reward.Get("kind","copy").Text=="research"?"确认研究":"确认领取");yield return new WaitForSecondsRealtime(1.3f);
   var saved=game.s["block"]["reward_choices"].First(r=>r["reward_id"].Text==rid);Check("ui6_task_reward_saved_claim_"+rid,saved["claimed"].Bool&&saved["chosen_bonus"].Text==bid&&!Bonus.pending(game).Any(r=>r["reward_id"].Text==rid));
   if(bid=="__currency__")Check("ui6_task_reward_compensation_"+rid,Math.Abs(game.s["currency"].Double-cash-reward.Get("compensation",30).Double)<.000001);
   else if(reward.Get("kind","copy").Text=="research")Check("ui6_task_reward_research_"+rid,game.s["block"]["blueprint_levels"].Get(bid,0).Long==level+1);
   else Check("ui6_task_reward_copy_"+rid,BlueprintCharges(game,bid)==copies+1);
  }
 }
 IEnumerator Ui6DeliveryMilestoneJourney(V earnedWorld){
  RestoreContent(earnedWorld,"ui6_task_earned_world");city.Interface.OpenArchive("themes");yield return null;var game=city.Runtime.Game;
  Check("ui6_task_starts_unselected_and_editable",city.Runtime.Session.CanEditFocused&&Themes.view(game)["id"].Text==""&&game.s["phase"].Text=="play");
  // The earned industrial city has one factory. Pay for a real extra workshop so
  // M2's eight effective industrial workers can be met; no stock or goal is injected.
  var tile=game.s["cells"].Keys.Where(t=>game.s["cells"][t]["building"].Text==""&&!game.s["cells"][t]["traits"].Contains("tile.water")).OrderBy(t=>Hex.distance("0,1",t)).ThenBy(t=>t,StringComparer.Ordinal).First();var cashBefore=game.s["currency"].Double;
  Check("ui6_task_real_workshop_tender",city.Runtime.Execute(g=>g.create_tender(V.Arr(tile),"workshop"))=="");var offer=game.s["draft"]["offers"][0]["id"].Text;Check("ui6_task_real_workshop_payment",city.Runtime.Execute(g=>g.accept_offer(offer))==""&&game.s["currency"].Double<cashBefore);var workshop=game.s["cells"][tile]["building"].Text;
  city.Interface.OpenArchive("themes");yield return null;ClickContentCard("工业基地","选择此主题");yield return null;Check("ui6_task_theme_selected_via_ui",Themes.view(game)["id"].Text=="industrial"&&!Themes.view(game)["completed"].Bool);
  int slices=0;bool ready=false;
  for(int step=0;step<40;step++){
   yield return Ui6ClaimTaskRewards("advance_"+step);
   city.Interface.OpenArchive("themes");yield return null;foreach(var offered in Themes.view(game)["events"].Where(e=>e["status"].Text=="offered").ToArray()){var decline=ContentEventChoice(offered["id"].Text,"decline");Check("ui6_task_decline_unrelated_event_"+offered["id"].Text,decline!=null&&decline.IsInteractable());decline.onClick.Invoke();yield return null;}
   var priorStage=Themes.view(game)["stage"].Long;city.Runtime.ResumeNormal();V advance=V.Map();var error=city.Runtime.ExecuteWorld(s=>{advance=s.Advance(.25*s.c.beta["units"]["earth_seconds_per_month"].Double,"normal");return advance.Get("steps",0).Long>0?"":advance.Get("reason","No committed step").Text;});city.Runtime.SetPaused(true);slices+=(int)advance.Get("steps",0).Long;
   Check("ui6_task_normal_clock_step_"+step,error==""&&advance.Get("steps",0).Long>0,advance);var view=Themes.view(game);
   if(view["stage"].Long>priorStage){city.Interface.OpenArchive("themes");yield return null;yield return Capture("ui6-task-stage-"+priorStage+".png");Check("ui6_task_stage_saved_"+priorStage,game.s["block"]["theme"]["milestone_ids"].Contains("industrial:M"+priorStage));}
   ready=view["stage"].Long==3&&view["goals"].Where(g=>g["kind"].Text=="state").All(g=>g["met"].Bool)&&view["steady_seconds"].Double>=view["steady_required_seconds"].Double;
   if(ready)break;
  }
  Check("ui6_task_reaches_real_delivery_stage",ready&&game.s["projects"][workshop]["status"].Text=="running"&&Themes.view(game)["goals"].Any(g=>g["kind"].Text=="delivery"&&!g["met"].Bool));yield return Ui6ClaimTaskRewards("before_delivery");
  city.Interface.OpenArchive("themes");yield return null;yield return Capture("ui6-task-before-delivery.png");var deliveryStart=game.s["block"].Get("theme_deliveries",V.Arr()).Count;
  foreach(var resource in new[]{"material","goods"}){
   var goal=Themes.view(game)["goals"].First(g=>g["id"].Text=="delivery_"+resource);var remaining=goal["target"].Double-goal["value"].Double;var stock=game.inventory_total(resource);Check("ui6_task_real_stock_before_"+resource,stock+1e-6>=remaining);
   Click("从库存交付 "+Catalog.RESOURCE_NAMES.Get(resource,resource).Text);yield return null;Check("ui6_task_ui_withdraws_"+resource,Math.Abs(stock-game.inventory_total(resource)-remaining)<.000001);Check("ui6_task_delivery_saved_"+resource,game.s["block"]["theme_deliveries"].Any(d=>d["source"].Text=="theme:industrial:M3"&&d["resource"].Text==resource&&Math.Abs(d["amount"].Double-remaining)<.000001));
  }
  Check("ui6_task_delivery_completes_milestone",Themes.view(game)["completed"].Bool&&game.s["block"]["theme"]["milestone_ids"].Contains("industrial:M3")&&game.s["block"]["theme_deliveries"].Count==deliveryStart+2);
  var completionReward=Bonus.pending(game).FirstOrDefault(r=>r.Get("source","").Text=="theme:industrial:M3");Check("ui6_task_completion_earns_saved_reward",completionReward!=null);var rid=completionReward["reward_id"].Text;city.Interface.OpenArchive("themes");yield return null;yield return Capture("ui6-task-stage-achieved.png");
  yield return Ui6ClaimTaskRewards("completion");Check("ui6_task_final_reward_claimed",game.s["block"]["reward_choices"].Any(r=>r["reward_id"].Text==rid&&r["claimed"].Bool));city.Interface.OpenArchive("themes");yield return null;Click("已完成");yield return null;yield return Capture("ui6-task-reward-claimed.png");Check("ui6_task_result_city_valid",city.Runtime.Session.Validate()=="");
  File.WriteAllText(Path.Combine(directory,"ui6-task-flow.json"),V.Map("executed",true,"scope","Earned full-city world; paid tender/accept command adds a real workshop, normal clock commits build it and complete M1/M2, then actual UI callbacks deliver real M3 stock, save milestone and claim its earned reward. No injected stock, goal counters, eligibility or RNG.","committed_slices",slices,"workshop",workshop,"reward_id",rid,"milestones",game.s["block"]["theme"]["milestone_ids"],"deliveries",V.From(game.s["block"]["theme_deliveries"].Skip(deliveryStart)),"claimed_reward",game.s["block"]["reward_choices"].First(r=>r["reward_id"].Text==rid)).ToJson(true));
 }
}
}
