using System;
using System.Linq;
using MET.Core;
using UnityEngine;
namespace MET.City {
public sealed partial class CityInterface {
 // One detached world per commit, created only when an archive page needs it.
 V _worldView;
 V W=>_worldView??(_worldView=Runtime.SessionSnapshot);
 string _arcSpecialization="balanced",_arcName="",_arcSponsor="",_arcOpportunity="",_linkFrom="",_linkTo="",_linkResource="food";
 double _linkRate=6,_linkCost=.15;
 string _arcDistrict="",_arcBlock="",_arcTechnology="",_arcLink="";
 int _districtStartStage,_blockStartStage,_arcLanding;
 string BlockName(V b)=>b.Get("state",V.Map()).Get("block",V.Map()).Get("display_name",b.Get("id","街区")).Text;
 string SpecName(string id)=>V.Map("balanced","综合","agricultural","农业","industrial","工业","services","公共服务","research","科研","logistics","物流").Get(id,id).Text;
 void WorldCommand(Func<CitySession,string> command,Action after=null){var error=Runtime.ExecuteWorld(command);PlayCue(error==""?"confirm":"notice");if(error==""){after?.Invoke();if(_archiveOpen)RenderPage();RefreshContext();}}
 void ArcNavigation(){var nav=Row(_body,"City archive navigation",50);foreach(var item in new[]{new[]{"heritage","遗产与城史"},new[]{"content","构筑与成果"}}){var p=item[0];Tab(nav,item[1],p,()=>Navigate(p),_page);}Label(nav,"永久城市管理暂未开放",16,Muted);}
 void CityManagementUnavailablePage(string title){
  var box=Card(_body,title+" · 暂未开放","永久城市管理暂未开放。已有历史与来源可在档案中只读查看。",Muted);
  Button(box,"查看遗产与城史",()=>Navigate("heritage"));
 }
 void DistrictStartPage(){StartupWorkspace(true);}
 void StartupWorkspace(bool district){
  if(!district||W["blocks"].Count>0||W["districts"].Count>0){CityManagementUnavailablePage(district?"后续地区":"新街区");return;}
  var stage=district?_districtStartStage:_blockStartStage;var world=W;var columns=WorkspaceColumns(district?"District startup choices":"Block startup choices",1.1f,1);var choices=columns[0];var summary=columns[1];Label(choices,district?"开启地区任期":"建立新街区",25,Gold);
  var tabs=Row(choices,"Startup stages",46);for(int i=0;i<3;i++){var step=i;Button(tabs,new[]{"1 方向","2 规约","3 地点"}[i],()=>{if(district)_districtStartStage=step;else _blockStartStage=step;RenderPage();},stage==i);}
  if(stage==0){if(district){Label(choices,"地区名称",18,Muted);var name=InputBox(choices,_arcName,"地区名称（可稍后更改）");name.onValueChanged.AddListener(v=>_arcName=v);}SpecializationPicker(choices);if(world["blocks"].Count==0)_arcSponsor="";else{Label(choices,"启动供给来源",19,Gold);if(!world["blocks"].Has(_arcSponsor))_arcSponsor=world.Get("focused_block_id","").Text;foreach(var block in world["blocks"].Values){var id=block["id"].Text;Button(choices,(_arcSponsor==id?"✓ ":"")+BlockName(block),()=>{_arcSponsor=id;RenderPage();},_arcSponsor==id);}}}
  else if(stage==1)StartupOpportunityPicker(choices,district);
  else {Label(choices,"选择真实登陆地形",20,Gold);for(var i=0;i<3;i++){var landing=i;var box=Card(choices,new[]{"河湾","林缘旧村","北部高台"}[i],"地点决定实际地形；所选方向与同城能力共同影响开局。",i==0?Blue:i==1?Green:Gold);Button(box,_arcLanding==i?"✓ 已选地点":"选择此地点",()=>{_arcLanding=landing;RenderPage();},_arcLanding==i);}}
  Label(summary,"本次开局",25,Gold);Metric(summary,"方向",SpecName(_arcSpecialization));Metric(summary,"供给来源",_arcSponsor==""?"城市最初授权":LegacyBlockName(_arcSponsor));Metric(summary,"登陆地点",new[]{"河湾","林缘旧村","北部高台"}[_arcLanding]);StartupQuote(summary,district);Fold(summary,"开局来源与规则",p=>Raw(p,world.Get("legacy",V.Map())));
  if(stage<2)WorkspaceActions(stage==0?"下一步 · 选择规约":"下一步 · 选择地点",()=>{if(district)_districtStartStage=stage+1;else _blockStartStage=stage+1;RenderPage();});
  else WorkspaceActions(district?"支付启动投入 · 建立地区":"支付启动投入 · 建立街区",()=>WorldCommand(session=>district?session.BeginDistrict(_arcLanding,_arcSpecialization,_arcName,_arcSponsor,_arcOpportunity):session.BeginBlock(_arcLanding,_arcSpecialization,_arcSponsor,_arcOpportunity)));
  WorkspaceHint("按方向、规约、地点完成选择；右侧始终显示真实启动投入和来源。");
 }
 void StartupQuote(Transform parent,bool district){var quote=Runtime.Session.StartupView(district,_arcSpecialization,_arcSponsor,_arcOpportunity);var box=Card(parent,"启动投入",quote.Get("reason","").Text,Gold);if(quote.Get("initial_charter",false).Bool){Label(box,"首地区来自城市最初授权，居民与基础建筑沿用当前起源规则。",17,Muted);return;}Metric(box,"通货总投入 / 来源持有",F(quote.Get("quote",V.Map()).Get("money",0).Double)+" / "+F(quote.Get("currency",0).Double));var opportunity=quote.Get("opportunity",V.Map());if(opportunity.Count>0){Label(box,"含规约「"+opportunity.Get("name","").Text+"」的额外投入；以下总额仍从当前来源真实扣除。",17,Gold,65);Label(box,opportunity.Get("tradeoff","").Text,17,Muted,65);LegacyEffects(box,opportunity);LegacyCost(box,opportunity.Get("cost",V.Map()),"规约额外部分");}foreach(var r in quote.Get("quote",V.Map()).Get("stock",V.Map()).Keys)Metric(box,Resource(r),F(quote["quote"]["stock"][r].Double)+" / 来源持有 "+F(quote.Get("available",V.Map()).Get(r,0).Double));}
 void SpecializationPicker(Transform parent){
  Label(parent,"专业方向",19,Gold);
  foreach(var spec in new[]{"balanced","agricultural","industrial","services","research","logistics"}){var id=spec;Button(parent,(_arcSpecialization==id?"✓ ":"")+SpecName(id),()=>{_arcSpecialization=id;_arcOpportunity="";RenderPage();},_arcSpecialization==id);}
 }
 void CityArcPage(){CityManagementUnavailablePage("城市与地区");}
 void NewBlockPage(){StartupWorkspace(false);}
 void GovernancePage(){CityManagementUnavailablePage("地区治理");}
 void NetworkPage(){CityManagementUnavailablePage("区域物流");}
 void SciencePage(){CityManagementUnavailablePage("城市科研与工业");}
 void CityProjectPage(){CityManagementUnavailablePage("全城工程");}
 string HistoryText(V evt){var payload=evt.Get("payload",V.Map());if(evt["kind"].Text=="local_event")return payload.Get("event",V.Map()).Get("text","街区事件").Text;return V.Map("city_founded","城市建立","district_started","开启地区任期","block_started","建立新街区","district_retired","地区交班 / 自治","district_crisis","地区进入治理危机","district_returned","付代价回任","district_named","地区命名","block_stabilized","街区达到稳定条件","institution_founded","地方经验形成永久制度","city_capability","形成城市能力","portable_transferred","许可转移","building_changed","建筑建设 / 升级","building_removed","建筑拆除","population_milestone","居民发展里程碑","local_identity","地方身份变化","research_started","城市研究启动","research_complete","城市技术完成","city_project_started","托卡马克之冠开工","city_project_delivery","城市工程实际交付","city_project_complete","托卡马克之冠建成","reward_claimed","领取成长方案").Get(evt["kind"],"城市记录").Text;}
 void HeritagePage(){
  var world=W;var columns=WorkspaceColumns("City history and portable licenses",1.15f,1);var history=columns[0];var licenses=columns[1];Label(history,"同一座城市的历史",25,Blue);Label(history,"记录实际开局、建设、任期、研究与交付。",17,Muted);foreach(var evt in world.Get("history",V.Arr()).Reverse().Take(140)){Label(history,HistoryText(evt),18);Fold(history,"事件与实体来源",p=>Raw(p,evt));}Fold(history,"地方、制度与城市能力",p=>Raw(p,world.Get("legacy",V.Map())));Fold(history,"永久城市完整只读存档",p=>Raw(p,world));
  Label(licenses,"许可来源记录",25,Gold);Label(licenses,"只读档案 · 跨街区许可转移暂未开放。",17,Muted,65);int available=0;
  foreach(var source in world["blocks"].Values){var wallet=source.Get("state",V.Map()).Get("block",V.Map()).Get("bonus_wallet",V.Map());foreach(var pair in wallet.Pairs){long charges=pair.Value.Get("charges",0).Long;if(charges<=0)continue;available++;var box=Card(licenses,G.c.bonuses.Get(pair.Key,V.Map()).Get("name",pair.Key).Text,"来源："+BlockName(source)+" · 保存库存 "+charges+" 份",Muted);Fold(box,"完整库存来源",p=>Raw(p,pair.Value));}}
  if(available==0)Label(licenses,"尚无保存的许可库存记录。",18,Muted);WorkspaceActions("查看长期成果与来源",()=>Navigate("content"));WorkspaceHint("历史与库存来源只读展示；永久城市管理暂未开放。");
 }
}
}
