using System.Linq;
using MET.Core;
using UnityEngine;
namespace MET.City {
public sealed partial class CityInterface {
 void StartupOpportunityPicker(Transform parent,bool district){
  var offers=Runtime.Session.StartupOpportunities(district,_arcSpecialization);
  if(_arcOpportunity!=""&&!offers.Any(x=>x.Get("id","").Text==_arcOpportunity))_arcOpportunity="";
  var intro=Card(parent,"开局规约 · 单选","只显示匹配当前专业方向的同城成果。原地标与部署留在原街区；规约的额外投入仍由所选供给街区支付。",Blue);
  Button(intro,(_arcOpportunity==""?"✓ ":"")+"无规约 · 常规开局",()=>{_arcOpportunity="";RenderPage();});
  if(offers.Count==0)Label(intro,"当前方向尚无可采用的长期成果。",17,Muted);
  foreach(var item in offers){var offer=item;var id=offer["id"].Text;var box=Card(intro,offer.Get("name",id).Text,"源街区 "+LegacyBlockName(offer.Get("source_block","").Text)+" · 成果等级 "+LegacyGrade(offer),Gold);Label(box,offer.Get("tradeoff","").Text,17,Muted,65);LegacyEffects(box,offer);LegacyCost(box,offer.Get("cost",V.Map()),"额外投入");LegacySource(box,offer);Button(box,(_arcOpportunity==id?"✓ 已选择":"采用这份规约"),()=>{_arcOpportunity=id;RenderPage();},true);Fold(box,"规约完整效果与来源",p=>Raw(p,offer));}
 }
 string LegacyBlockName(string id)=>W["blocks"].Has(id)?BlockName(W["blocks"][id]):id;
 string LegacyGrade(V item){var grade=item.Get("grade",0);return grade.IsNumber?new[]{"基础","一级蓝图","二级蓝图"}[Mathf.Clamp((int)grade.Long,0,2)]:grade.Text;}
 void LegacyCost(Transform parent,V cost,string label){Metric(parent,label+" · 通货",F(cost.Get("money",0).Double));foreach(var pair in cost.Get("stock",V.Map()).Pairs)Metric(parent,label+" · "+Resource(pair.Key),F(pair.Value.Double));}
 void LegacyEffects(Transform parent,V offer){foreach(var rule in offer.Get("rules",V.Arr())){if(rule.Get("key","").Text=="service_capacity_multiplier"){foreach(var value in rule["value"].Pairs)Metric(parent,V.Map("medical","医疗供给","education","教育供给").Get(value.Key,value.Key).Text,"× "+F(value.Value.Double,2));continue;}var field=rule.Get("field","").Text;Metric(parent,V.Map("service.medical","医疗供给","service.education","教育供给","exposure_multiplier","住宅污染暴露","maintenance","维护支出","population_maintenance","居民维护","housing","住宅容量","output.material","工业材料产能","output.goods","工业制成品产能").Get(field,field).Text,"× "+F(rule.Get("value",1).Double,2));}var knowledge=offer.Get("knowledge_multiplier",1).Double;if(knowledge!=1)Metric(parent,"知识产出","× "+F(knowledge,2));}
 void LegacySource(Transform parent,V result){var district=result.Get("source_district","").Text;if(district!="")Metric(parent,"来源地区",W["districts"].Get(district,V.Map()).Get("name",district).Text);var landmark=result.Get("source_landmark",result.Get("landmark",V.Map()));if(landmark.Count>0)Metric(parent,"原地标",landmark.Get("name",landmark.Get("id","地标留在原区")).Text);}
 void BuildLegacyCards(Transform parent,bool selecting){
  var view=Runtime.Session.BuildLegacyView();var chosen=view.Get("chosen",V.Map());var pending=view.Get("pending",V.Arr());var evidence=view.Get("evidence",V.Map());
  var intro=Card(parent,"街区长期成果","首次 DT 实际归零时锁定主题、已部署副本、蓝图等级和最后实际供给。"+(selecting?"正式挂牌后可选择一项本街区已获得的长期成果。":"此处只读查看已保存的成果资格与来源；本街区成果在任务结算中选择。"),Gold);
  if(chosen.Count>0){Label(intro,"已选择："+chosen.Get("name",chosen.Get("profile_id","长期成果")).Text+" · "+LegacyGrade(chosen),22,Gold);Label(intro,chosen.Get("tradeoff","").Text,17,Muted,65);LegacySource(intro,chosen);Fold(intro,"已选成果与原地来源",p=>Raw(p,chosen));}
  else if(pending.Count==0)Label(intro,"当前没有锁定合格成果。旧档缺少本轮首次零证据时不追补资格。",17,Muted,70);
  else foreach(var candidate in pending){var item=candidate;var id=item.Get("profile_id",item.Get("id","")).Text;var box=Card(intro,item.Get("name",id).Text,"锁定等级 · "+LegacyGrade(item),Blue);Label(box,item.Get("tradeoff","").Text,17,Muted,65);if(selecting){bool claimed=S["block"].Get("ending",V.Map()).Get("capstone_claimed",false).Bool;Button(box,claimed?"选择这项街区长期成果":"先正式挂牌后选择",()=>WorldCommand(session=>session.ChooseBuildLegacy(id)),true,!claimed);}Fold(box,"成果资格与构筑依据",p=>Raw(p,item));}
  if(evidence.Count>0){var measured=evidence.Get("metrics",V.Map());Label(intro,"锁定时最后实际供给",18,Green);foreach(var key in new[]{"heat_used","pollution_recycled","medical","education"})if(measured.Has(key))Metric(intro,V.Map("heat_used","实际余热利用","pollution_recycled","实际污染回收","medical","医疗覆盖","education","教育覆盖")[key].Text,key=="medical"||key=="education"?F(measured[key].Double*100,1)+"%":F(measured[key].Double,3));foreach(var instance in evidence.Get("bonus_evidence",V.Arr())){var bid=instance.Get("bonus_id",instance.Get("bonus","")).Text;Label(intro,G.c.bonuses.Get(bid,V.Map()).Get("name",bid).Text+" → "+G.project_name(instance.Get("host_id",instance.Get("host","")).Text)+" · 活跃部署 "+instance.Get("count",1).Long+"份 · "+LegacyGrade(V.Map("grade",instance.Get("level",0))),16,Muted);}foreach(var pair in evidence.Get("blueprint_levels",V.Map()).Pairs)Metric(intro,"锁定蓝图 · "+G.c.bonuses.Get(pair.Key,V.Map()).Get("name",pair.Key).Text,LegacyGrade(V.Map("grade",pair.Value)));Fold(intro,"首次零完整部署、蓝图与供给证据",p=>Raw(p,evidence));}
 }
 string _contentCategory="local",_contentSelection="";
 void ContentPage(){
  var columns=WorkspaceColumns("City content catalogue",.75f,1.5f);var list=columns[0];var detail=columns[1];var view=Runtime.Session.BuildLegacyView();
  foreach(var entry in new[]{new[]{"local","本区长期成果"},new[]{"bonuses","方案图鉴"},new[]{"profiles","规约方向"},new[]{"results","同城成果"}}){var id=entry[0];Button(list,entry[1],()=>{_contentCategory=id;_contentSelection="";RenderPage();},_contentCategory==id);}
  if(_contentCategory=="local"){BuildLegacyCards(detail,false);WorkspaceHint("首次 DT 实际归零的资格与来源只读保留；本街区成果在任务结算中选择。");return;}
  if(_contentCategory=="bonuses"){
   foreach(var bid in new[]{"P09","R19","O08","B10"}){var id=bid;Button(list,G.c.bonuses.Get(id,V.Map()).Get("name",id).Text,()=>{_contentSelection=id;RenderPage();},_contentSelection==id);}
   var selected=_contentSelection==""?"P09":_contentSelection;var data=G.c.bonuses.Get(selected,V.Map());var card=Bonus.card(G,selected);var box=Card(detail,data.Get("name",selected).Text,data.Get("summary","").Text,Gold);BlueprintArt(box,selected,130);Label(box,"首份100%，同对象追加副本每份33⅓%；两级蓝图强化指定收益，容量与维护代价继续生效。",17,Muted);Metric(box,"本区库存 / 蓝图",card.Get("copies",0).Long+"份 / "+card.Get("grade","基础").Text);BlueprintRuleRows(box,card.Get("effects",V.Arr()),selected);Fold(box,"完整规则与副本效果",p=>Raw(p,Rules.bonus_rules(G,selected,1,(int)card.Get("blueprint_level",0).Long)));WorkspaceActions("查看部署与蓝图",()=>{_inventoryChoice=selected;Navigate("bonuses");});return;
  }
  var rows=(_contentCategory=="profiles"?view.Get("profiles",V.Arr()):view.Get("results",V.Arr())).ToArray();
  if(rows.Length==0){Label(detail,"当前还没有同城成果。成果由真实构筑证据决定。",18,Muted);}
  else {for(int i=0;i<rows.Length;i++){var id=i.ToString();var item=rows[i];Button(list,item.Get("name",item.Get("profile_id",item.Get("id","成果"))).Text,()=>{_contentSelection=id;RenderPage();},_contentSelection==id);}int.TryParse(_contentSelection,out var index);var chosen=rows[Mathf.Clamp(index,0,rows.Length-1)];var box=Card(detail,chosen.Get("name",chosen.Get("profile_id",chosen.Get("id","成果"))).Text,_contentCategory=="profiles"?chosen.Get("description","").Text:"源街区 "+LegacyBlockName(chosen.Get("source_block","").Text)+" · "+LegacyGrade(chosen),Green);Label(box,chosen.Get("tradeoff","").Text,17,Muted);LegacySource(box,chosen);Fold(box,_contentCategory=="profiles"?"匹配方向、投入与完整规约":"成果完整来源",p=>Raw(p,chosen));}
  WorkspaceHint("已保存成果与规约来源只读保留；同城扩展暂未开放。");
  if(_contentCategory=="results")Label(detail,"同城新街区与后续地区暂未开放。",17,Muted);
 }

}
}
