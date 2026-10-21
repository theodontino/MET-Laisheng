using System;
using System.Collections;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;
namespace MET.City {
public sealed partial class CityEvidence {
    V contentEvidence=V.Arr();
    void RestoreContent(V world,string label) {
        Check("content_restore_"+label,city.Runtime.ExecuteWorld(s=>s.Restore(world))=="");
        city.Runtime.SetPaused(true);
        Check("content_world_valid_"+label,city.Runtime.Session.Validate()=="");
    }
    Button ContentCardButton(string card,string label)=>city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.GetComponentInChildren<Text>()?.text==label&&b.transform.parent.GetComponentsInChildren<Text>().Any(t=>t.text==card));
    Button ContentEventChoice(string eventId,string choiceId)=>city.Interface.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.name=="ProposalChoice "+eventId+"|"+choiceId);
    void ClickContentCard(string card,string label) {
        var button=ContentCardButton(card,label);Check("content_ui_"+card,button!=null&&button.IsInteractable());button.onClick.Invoke();
    }
    void FocusContentCard(string heading) {
        Canvas.ForceUpdateCanvases();
        var scroll=city.Interface.GetComponentsInChildren<ScrollRect>(true).First(s=>s.gameObject.activeInHierarchy&&s.vertical);
        var card=scroll.content.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Card "+heading);
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content,card);
        float range=scroll.content.rect.height-scroll.viewport.rect.height;
        if(range>0)scroll.verticalNormalizedPosition=1-Mathf.Clamp01((scroll.content.rect.yMax-bounds.max.y)/range);
        Canvas.ForceUpdateCanvases();
    }
    IEnumerator RogueContentJourney() {
        Check("content_catalog_44_cards_21_events_17_blueprints",city.Runtime.Game.c.bonuses.Count==44&&city.Runtime.Game.c.commission_events.Count==21&&city.Runtime.Game.c.bonus_upgrades["upgrades"].Count==17);
        Check("content_fullscreen_baseline",Screen.width==1920&&Screen.height==1080&&Screen.fullScreen);
        foreach(var route in new[]{"industrial","civic"}) {
            string path=Environment.GetEnvironmentVariable(route=="industrial"?"MET_ROGUE_INDUSTRY_REPORT":"MET_ROGUE_CIVIC_REPORT");
            Check("content_earned_report_exists_"+route,!string.IsNullOrWhiteSpace(path)&&File.Exists(path));
            var journey=V.Parse(File.ReadAllText(path));Check("content_normal_chain_"+route,journey["ok"].Bool&&journey["scope"].Text.Contains("normal commands"),journey["checks"]);
            var checkpoints=journey["checkpoints"];string source=journey["source_block"].Text,profile=journey["profile"].Text;
            foreach(var checkpoint in checkpoints.Pairs.Where(pair=>pair.Key.StartsWith("offered:",StringComparison.Ordinal))) {
                RestoreContent(checkpoint.Value,checkpoint.Key);city.Interface.OpenArchive("themes");yield return null;
                string eventId=checkpoint.Key.Substring(8);var offered=Themes.view(city.Runtime.Game)["events"].First(e=>e["id"].Text==eventId);
                Check("content_event_three_choices_"+eventId,offered["choices"].Count==3);
                foreach(var choice in offered["choices"]) {
                    var control=ContentEventChoice(eventId,choice["id"].Text);
                    Check("content_branch_available_and_cost_"+eventId+"_"+choice["id"].Text,control!=null&&control.IsInteractable()==choice.Get("available",true).Bool&&choice["cost_text"].Text!="");
                }
                FocusContentCard(offered["name"].Text);yield return null;yield return Capture("content-event-"+eventId+".png");
                var candidate=offered["choices"].FirstOrDefault(x=>x["id"].Text!="decline"&&x.Get("available",true).Bool);
                if(candidate!=null) {
                    var definition=Themes._event_choice_definition(Themes._event_definition(city.Runtime.Game,eventId),candidate["id"].Text);
                    double money=city.Runtime.Game.s["currency"].Double;var inventory=V.Map();foreach(var resource in definition.Get("resources",V.Map()).Keys)inventory[resource]=city.Runtime.Game.inventory_total(resource);
                    var selected=ContentEventChoice(eventId,candidate["id"].Text);Check("content_choice_control_"+eventId,selected!=null&&selected.IsInteractable());selected.onClick.Invoke();yield return null;
                    Check("content_branch_actual_payment_"+eventId,Math.Abs(money-city.Runtime.Game.s["currency"].Double-definition.Get("currency",0).Double-definition.Get("deposit",0).Double)<.000001);
                    foreach(var resource in inventory.Keys)Check("content_branch_actual_stock_"+eventId+"_"+resource,Math.Abs(inventory[resource].Double-city.Runtime.Game.inventory_total(resource)-definition["resources"][resource].Double)<.000001);
                    Check("content_branch_persisted_"+eventId,Themes._find_event(city.Runtime.Game,eventId).Get("choice_id","").Text==candidate["id"].Text);
                }
            }
            RestoreContent(checkpoints["actual_DTzero_before_claim"],route+"_ending");city.Interface.OpenArchive("settlement");yield return null;
            Check("content_real_zero_and_candidate_"+route,city.Runtime.Game.s["dt"].Double==0&&city.Runtime.Game.s["block"]["ending"]["build_result"]["candidates"].Any(x=>x["profile_id"].Text==profile));
            FocusContentCard(Block.capstone_definitions(city.Runtime.Game)[city.Runtime.Game.s["block"]["ending"]["eligible_theme"]]["name"].Text);yield return null;yield return Capture("content-"+route+"-ending.png");Click("正式挂牌 · 领取定型建筑");yield return null;
            string name=RogueContent.Profile(city.Runtime.Game.c,profile)["name"].Text;ClickContentCard(name,"选择这项街区长期成果");yield return null;
            Check("content_one_result_after_ui_"+route,city.Runtime.Session.BuildLegacyView()["chosen"]["profile_id"].Text==profile);
            city.Interface.OpenArchive("content");yield return null;string before=CityGameplayState();yield return Capture("content-"+route+"-result.png");Check("content_page_is_read_only_"+route,CityGameplayState()==before);
            RestoreContent(checkpoints["paid_before_successor"],route+"_startup");city.Interface.OpenArchive("block_start");yield return null;
            string spec=journey["city"]["blocks"][journey["successor_block"].Text]["specialization"].Text;
            Click(SpecUiName(spec));yield return null;ClickContentCard(name,"采用这份规约");yield return null;
            var quote=city.Runtime.Session.StartupView(false,spec,source,"build:"+source);Check("content_ui_real_startup_quote_"+route,quote["can_pay"].Bool);
            FocusContentCard("开局规约 · 单选");yield return null;yield return Capture("content-"+route+"-startup.png");double cash=city.Runtime.Session.Games[source].s["currency"].Double;Click("支付启动投入 · 登陆");yield return null;
            Check("content_ui_startup_debits_"+route,Math.Abs(cash-city.Runtime.Session.Games[source].s["currency"].Double-quote["quote"]["money"].Double)<.000001);
            Click("确立起源 · 安顿居民");yield return null;
            Check("content_ui_target_paid_charter_"+route,city.Runtime.Session.Block(city.Runtime.Session.FocusedBlockId)["start_sources"]["opportunity"]["profile_id"].Text==profile&&city.Runtime.Session.Games[source].s["block"]["capstone"].Count>0&&!city.Runtime.Game.s["block"].Has("capstone"));
            string export=Path.Combine(directory,"content-"+route+"-city.json");Check("content_native_export_"+route,city.Runtime.Export(export)=="");Check("content_native_load_"+route,city.Runtime.Load(export)=="");Check("content_native_reload_valid_"+route,city.Runtime.Session.Validate()=="");
            contentEvidence.Add(V.Map("route",route,"scope",journey["scope"],"normal_checks",journey["checks"],"profile",profile,"source_evidence",journey["source_evidence"],"earned_checkpoints_reused",true));
        }
    }
    static string SpecUiName(string spec)=>V.Map("balanced","综合","industrial","工业","agricultural","农业","services","公共服务","research","科研","logistics","物流").Get(spec,spec).Text;
}
}
