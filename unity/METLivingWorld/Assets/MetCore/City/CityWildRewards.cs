using System;
using System.Linq;
using System.Collections.Generic;
namespace MET.Core {
public sealed partial class CitySession {
 const string LegacyWildReward="wild:hope-design-ii:v1";
 static bool RewardText(V v)=>v.IsString&&!string.IsNullOrWhiteSpace(v.Text)&&v.Text==v.Text.Trim();
 static bool RewardNumber(V v,double min)=>v.IsNumber&&!double.IsNaN(v.Double)&&!double.IsInfinity(v.Double)&&v.Double>=min;
 static string ValidateChoices(Catalog c,V choices) {
  if(!choices.IsArray||choices.Count==0)return "任务奖励需要有效选项。";
  var ids=new HashSet<string>(StringComparer.Ordinal);
  foreach(var x in choices) {
   if(!x.IsMap||!RewardText(x.Get("id"))||!ids.Add(x["id"].Text)||!RewardText(x.Get("name"))||!RewardNumber(x.Get("amount"),0)||x["amount"].Double<=0)return "任务奖励选项字段损坏。";
   string kind=x.Get("kind","").Text;
   if(kind=="resource") {if(!RewardText(x.Get("resource"))||!Catalog.RESOURCE_NAMES.Has(x["resource"].Text))return "未知奖励资源。";}
   else if(kind=="bonus") {if(!RewardText(x.Get("bonus_id"))||!c.bonuses.Has(x["bonus_id"].Text)||x["amount"].Double!=1)return "奖励方案必须是已注册方案的一份库存。";}
   else return "未知奖励类型。";
  }
  return "";
 }
 public static string ValidateWildMilestones(Catalog c) {
  var housing=c.wild.Get("housing_design_ii");if(!RewardNumber(housing.Get("required_exports"),0)||!RewardNumber(housing.Get("cost"),0)||housing["cost"].Double<=0)return "住房设计 II 配置损坏。";
  var defs=c.wild.Get("milestones");if(!defs.IsArray)return "任务配置必须是列表。";
  var ids=new HashSet<string>(StringComparer.Ordinal);
  foreach(var d in defs) {
   var r=d.Get("requirements");
   if(!d.IsMap||!RewardText(d.Get("id"))||!ids.Add(d["id"].Text)||!RewardText(d.Get("name"))||!r.IsMap||r.Count!=2||!RewardNumber(r.Get("housing_levels"),0)||r["housing_levels"].Double>2||r["housing_levels"].Double%1!=0||!RewardNumber(r.Get("material_exported"),0))return "任务定义或条件损坏。";
   string error=ValidateChoices(c,d.Get("choices"));if(error!="")return error;
  }
  return "";
 }
 static V SavedWildRewards(V state) {
  var tech=state.Get("wild_technology",V.Map());var result=tech.Get("rewards",V.Map()).Duplicate(true);
  if(tech.Has("reward")&&!result.Has(LegacyWildReward))result[LegacyWildReward]=tech["reward"].Duplicate(true);
  return result;
 }
 void MigrateWildReward() {
  var tech=s.Get("wild_technology",V.Map());if(!tech.Has("reward"))return;
  tech["rewards"]=SavedWildRewards(s);tech.Remove("reward");
 }
 static string ValidateWildRewards(Catalog c,V state) {
  var tech=state.Get("wild_technology",V.Map());
  if(tech.Has("rewards")&&!tech["rewards"].IsMap)return "任务奖励列表损坏。";
  if(tech.Has("reward")&&(tech["reward"].Get("id","").Text!=LegacyWildReward||tech.Get("housing_levels",0).Long!=2||tech.Get("rewards",V.Map()).Has(LegacyWildReward)))return "旧任务奖励来源或重复记录损坏。";
  foreach(var pair in SavedWildRewards(state).Pairs) {
   var r=pair.Value;
   if(!r.IsMap||!RewardText(r.Get("id"))||pair.Key!=r["id"].Text||!r.Get("claimed").IsBool||!r.Get("chosen").IsString||!RewardText(r.Get("source_block"))||!state["blocks"].Has(r["source_block"].Text))return "任务奖励记录损坏。";
   string error=ValidateChoices(c,r.Get("choices"));if(error!="")return error;
   if(r["claimed"].Bool?!r["choices"].Any(x=>x["id"].Text==r["chosen"].Text):r["chosen"].Text!="")return "任务领取记录损坏。";
  }
  return "";
 }
 // Called by successful progression commands, never by a view or load. Offers
 // retain their exact options even when the catalog is later edited or removed.
 public string EnsureWildReward(string sourceBlock=null) {
  string error=ValidateWildMilestones(c);if(error!="")return error;
  var tech=s.Get("wild_technology",V.Map());if(!tech.Get("enabled",false).Bool)return "";
  string source=sourceBlock??FocusedBlockId;if(!s["blocks"].Has(source))return "任务来源街区不存在。";
  MigrateWildReward();var rewards=tech.Get("rewards",V.Map());
  foreach(var d in c.wild["milestones"]) {
   var req=d["requirements"];
   if(rewards.Has(d["id"].Text)||tech["housing_levels"].Double<req["housing_levels"].Double||tech["material_exported"].Double<req["material_exported"].Double)continue;
   rewards[d["id"].Text]=V.Map("id",d["id"],"name",d["name"],"source_block",source,"claimed",false,"chosen","","choices",d["choices"].Duplicate(true));
  }
  if(rewards.Count>0)tech["rewards"]=rewards;return "";
 }
 public static V WildRewardsView(Catalog c,V state,string focused,bool canEdit,bool playing=true) {
  var result=V.Arr();
  foreach(var r in SavedWildRewards(state).Values) {
   r["name"]=r.Get("name","掌握住房设计 II");
   r["can_claim"]=canEdit&&playing&&!r.Get("claimed",false).Bool&&r.Get("source_block","").Text==focused&&ValidateChoices(c,r.Get("choices"))=="";
   result.Add(r);
  }
  return result;
 }
 public V WildRewardView()=>SavedWildRewards(s).Get(LegacyWildReward,V.Map());
 public string ClaimWildReward(string choice)=>ClaimWildReward(LegacyWildReward,choice);
 public string ClaimWildReward(string rewardId,string choice) {
  if(!CanEditFocused||FocusedGame.s.Get("phase","").Text!="play")return "先进入可经营街区。";
  string error=ValidateWildRewards(c,s);if(error!="")return error;
  var reward=SavedWildRewards(s).Get(rewardId,V.Map());
  if(reward.Count==0)return "尚未完成此任务。";
  if(reward["claimed"].Bool)return "此任务奖励已经领取。";
  if(reward["source_block"].Text!=FocusedBlockId)return "请在完成任务的街区领取。";
  var selected=reward["choices"].FirstOrDefault(x=>x["id"].Text==choice);if(selected==null)return "未知奖励选项。";
  var g=FocusedGame;
  if(selected["kind"].Text=="bonus") {
   error=Bonus.grant(g,selected["bonus_id"].Text,reward["id"].Text);if(error!="")return error;
  } else {
   string resource=selected["resource"].Text;double amount=selected["amount"].Double;
   var inventory=g.s["projects"]["municipal"]["inventory"];
   if(inventory.Get(resource,0).Double+amount>g.stock_cap_for("municipal",resource))return "市政仓储不足；奖励保留，请腾出空间后重试。";
   inventory[resource]=inventory.Get(resource,0).Double+amount;
  }
  MigrateWildReward();reward["chosen"]=choice;reward["claimed"]=true;s["wild_technology"]["rewards"][rewardId]=reward;
  g.log_event("wild_task_reward",V.Map("reward_id",reward["id"],"choice",choice),"已领取任务奖励；方案进入库存，需手动装配。");
  g.refresh_metrics();return "";
 }
}
}
