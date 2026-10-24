using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;
namespace MET.City {
public sealed partial class CityInterface {
 public V ComparisonBaseline=>_baseline.Duplicate(true);
 public V ComparisonProposal=>_proposal.Get("map_snapshot",V.Map()).Duplicate(true);
 public event Action<V,V> ComparisonChanged;
 void StartPage(){
  if(Runtime.Session.s["phase"].Text=="choose_district"){DistrictStartPage();return;}
  if(S["phase"].Text=="select_block"){
   WorkspaceHint("先比较地形，再确定登陆位置；下一步选择起源。 ");
   var columns=WorkspaceColumns("Landing comparison",1,1,1);var names=new[]{"河湾","林缘旧村","北部高台"};var notes=new[]{"临水平地，邻近水岸。先铺开生活供给，再选择工业方向。","森林与既有住区相接。地块条件改变后续方案的价值。","高地与开阔地并存。保留水岸与土地特征，按实际场地规划。"};
   for(var i=0;i<3;i++){var index=i;Label(columns[i],"0"+(i+1)+"  "+names[i],25,i==0?Blue:i==1?Green:Gold);Illustration(columns[i],i==0?3:i==1?1:10,200);Label(columns[i],notes[i],18,Muted);Button(columns[i],"在此登陆",()=>Command(g=>g.select_block(index)),true);}
  }else{
   var choices=S["origin_choices"].ToArray();if(choices.Length==0){Label(_body,"等待本次起源候选。",22);return;}
   WorkspaceHint("每个起源提供不同居民故事与起始补给，选择后进入城市。");var columns=WorkspaceColumns("Origin comparison",choices.Select(_=>1f).ToArray());
   for(var i=0;i<choices.Length;i++){var id=choices[i].Text;var origin=G.c.origins[id];var col=columns[i];Label(col,origin.Get("name",id).Text,25,Gold);Illustration(col,id=="village"?3:id=="miners"?10:id=="charter"?12:1,170);Label(col,origin.Get("story",origin.Get("hint","")).Text,18,Muted);Metric(col,"起始补充通货",F(origin.Get("cash",0).Double,0));foreach(var resource in new[]{"food","material","goods"})if(origin.Get(resource,0).Double!=0)Metric(col,Resource(resource),Signed(origin[resource].Double));Button(col,"确立起源 · 安顿居民",()=>{Command(g=>g.choose_origin(id));CloseArchive();},true);}
  }
 }
 RectTransform OverviewBrief(Transform parent,string name){var card=Surface(name,parent,new Color(.055f,.11f,.125f,.96f));var layout=card.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(10,10,5,5);layout.spacing=4;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;card.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;return card;}
 void OverviewSummary(string name,string title,string value,string link,Action action,Color accent){
  var card=OverviewBrief(_body,name);var heading=Row(card,"Summary heading",30*TextScale);Height(heading,30*TextScale);var layout=heading.GetComponent<HorizontalLayoutGroup>();layout.childForceExpandWidth=false;var caption=Label(heading,title,16,accent,30*TextScale);caption.GetComponent<LayoutElement>().flexibleWidth=1;var button=Button(heading,link+" ›",action);Width((RectTransform)button.transform,100*TextScale);Height((RectTransform)button.transform,30*TextScale);button.GetComponentInChildren<Text>().fontSize=Mathf.RoundToInt(14*TextScale);Label(card,value,18,Ink,28*TextScale);
 }
 void OverviewPage(){
  var m=S["metrics"];var queue=Construction.queue(G);var guidance=CityGuidanceView();var settled=S.Get("simulation_tick",0).Long>0;
  OverviewSummary("Overview Living Summary","居住与居民","已安置 "+F(m.Get("housed",0).Double,0)+" · 待安置 "+F(m.Get("homeless",0).Double,0)+" 人","住户服务",()=>Navigate("population"),Green);
  OverviewSummary("Overview Supply Summary","供给与库存","粮食 "+F(G.inventory_total("food"))+" · "+(settled?"口粮 "+F(Progress.food_ratio(G)*100,1)+"%":"口粮待结算"),"资源收支",()=>Navigate("resources","food"),Blue);
  OverviewSummary("Overview Finance Summary","财政与经营",F(S.Get("currency",0).Double)+" 通货 · "+(settled?"经营 "+KnownRate(m.Get("cashflow",0).Double):"月率待结算"),"财政账目",()=>Navigate("resources","currency"),Gold);
  OverviewSummary("Overview Construction Summary","施工与进度",queue.Count+" 项工程 · "+F(Construction.capacity(G))+" CP/月","施工队列",()=>Navigate("construction"),Blue);
  var focus=OverviewBrief(_body,"Overview Next Action");Label(focus,guidance["title"].Text,19,guidance.Get("severity",0).Long>=2?Gold:Green,28*TextScale);Label(focus,guidance["reason"].Text,15,Muted,40*TextScale);var act=Button(focus,guidance["action_label"].Text+" ›",RunCityGuidanceAction,true);Height((RectTransform)act.transform,32*TextScale);act.GetComponentInChildren<Text>().fontSize=Mathf.RoundToInt(15*TextScale);
  Fold(_body,"问题与可采取的行动",c=>{
   if(m.Get("homeless",0).Double>0){Label(c,F(m["homeless"].Double,0)+" 人等待安置",17,Gold);Button(c,"查看住户与住房",()=>Navigate("population"));}
   if(settled&&Progress.food_ratio(G)<.999){Label(c,"口粮满足 "+F(Progress.food_ratio(G)*100,1)+"%",17,Gold);Button(c,"核对粮食来源",()=>Navigate("resources","food"));}
   if(settled&&m.Get("energy_ratio",1).Double<.999){Label(c,"电力满足 "+F(m.Get("energy_ratio",1).Double*100,1)+"%",17,Gold);Button(c,"查看供电能力",()=>Navigate("resources","energy"));}
   if(queue.Any(x=>x.Get("paused",false).Bool)){Label(c,"有工程暂停施工",17,Gold);Button(c,"管理暂停工程",()=>Navigate("construction"));}
   Button(c,"市场订单",()=>Navigate("market"));Button(c,"城市委任",()=>Navigate("themes"));Button(c,"Bonus 与蓝图",()=>Navigate("bonuses"));Button(c,"政策",()=>Navigate("policies"));Button(c,"城市记录",()=>Navigate("records"));Button(c,"规则与来源",()=>Navigate("sources"));
  });
  WorkspaceActions("永久城市 · 地区与长期发展 ›",()=>Navigate("city"));
 }
 string KnownRate(double value)=>S.Get("simulation_tick",0).Long>0?Signed(value)+" /月":"等待首次实际结算";
 void ResourcesPage(){
  var options=new[]{"food","material","goods","energy","currency"};var resource=options.Contains(_pageContext)?_pageContext:"food";var m=S["metrics"];var audit=S.Get("audit",V.Map());
  var columns=WorkspaceColumns("Resource balance workspace",.22f,.48f,.30f);var selector=columns[0];var balance=columns[1];var sources=columns[2];Label(selector,"资源账目",20,Muted);
  foreach(var option in options){var selected=option;var title=option=="energy"?"电力":option=="currency"?"财政":Resource(option);Button(selector,(resource==option?"● ":"")+title,()=>Navigate("resources",selected),resource==option);if(option!="energy"&&option!="currency")Label(selector,F(G.inventory_total(option))+" 库存",15,Muted);}
  if(resource=="energy"){
   Label(balance,"全城电力供需",26,Gold);Label(balance,"按全城满足比例分配的供需能力。",17,Muted);Meter(balance,"实际供给 / 需求",m.Get("energy",0).Double,m.Get("energy_need",0).Double," 能力",Gold);Metric(balance,"满足率",F(100*m.Get("energy_ratio",1).Double)+"%");Label(sources,"供电与用电建筑",21,Blue);
   var energySources=m.Get("energy_sources",V.Map());var energyRequests=m.Get("energy_requests",V.Map());foreach(var projectId in energySources.Keys.Union(energyRequests.Keys)){var supply=energySources.Get(projectId,0).Double;var demand=energyRequests.Get(projectId,0).Double;var block=CompactTaskCard(sources,"Energy source "+projectId);Label(block,projectId=="municipal"?"市政基础供电":G.project_name(projectId),18);Label(block,"实际供给 "+F(supply)+" · 有效需求 "+F(demand)+" 能力",15,Muted);if(S["projects"].Has(projectId))Button(block,"运行与来源",()=>Navigate("building",projectId));}var populationEnergy=m.Get("population_energy",V.Map());if(populationEnergy.Count>0)Metric(sources,"居民用电需求",F(populationEnergy.Values.Sum(x=>x.Double))+" 能力");Fold(sources,"全城电力完整口径",c=>Raw(c,m));WorkspaceActions("电力图层",()=>{SetLayer("energy");InspectMapFromPage();});return;
  }
  if(resource=="currency"){
   Label(balance,"财政与经营",26,Gold);Metric(balance,"当前通货余额",F(S["currency"].Double));Label(balance,"经营月率",20,Blue);Metric(balance,"经营收入",KnownRate(m.Get("income",0).Double));Metric(balance,"维护支出",KnownRate(m.Get("upkeep",0).Double));Metric(balance,"经营净额",KnownRate(m.Get("cashflow",0).Double));HistoryChart(balance,S.Get("civic032",V.Map()).Get("history",V.Arr()),"balance","通货历史 · 已有实际记录",Gold);Fold(balance,"经营税费与维护来源",c=>Raw(c,m.Get("fiscal_ledger",V.Arr())));
   Label(sources,"主动操作与市场收支",21,Gold);Label(sources,"逐笔实际通货变动；不计入左侧经营月率。",16,Muted);foreach(var item in S.Get("civic032",V.Map()).Get("transactions",V.Arr()).Reverse().Take(80)){var line=CompactTaskCard(sources,"Cash transaction");Label(line,item["label"].Text,17);Label(line,F(item["h"].Double)+" 月 · "+Signed(item["cash"].Double)+" 通货",16,Gold);Label(line,"余额 "+F(item["balance"].Double),15,Muted);}WorkspaceActions("市场与订单",()=>Navigate("market"));WorkspaceActions("政策经营对比",()=>Navigate("policies"));return;
  }
  Label(balance,Resource(resource)+" · 实际收支",26,resource=="food"?Green:resource=="material"?Gold:Blue,36*TextScale);Meter(balance,"当前库存",G.inventory_total(resource),G.stock_cap(resource)," 单位",Blue);
  var receipt=audit.Get("resources",V.Map()).Get(resource,V.Map());var elapsed=audit.Get("hours",0).Double;
  if(elapsed>0&&receipt.Count>0){Label(balance,"最近周期 · "+F(elapsed,3)+" 来生月",20,Blue,30*TextScale);var ledger=Column(balance,"Compact resource balance",0);ledger.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);var labels=new[]{"周期前库存","＋ 本地产出","＋ 实际进口","－ 实际消费 / 施工","－ 实际外运","－ 委任交付","－ 损耗","＝ 周期后库存"};var keys=new[]{"before","produced","imported","consumed","exported","commission_delivered","lost","after"};for(var i=0;i<keys.Length;i++){var key=keys[i];ResourceBalanceRow(ledger,labels[i],F(receipt.Get(key,0).Double,3),i==1||i==2?Green:i==7?Blue:Ink);}ResourceBalanceRow(ledger,"实际净变 / 月",Signed((receipt.Get("after",0).Double-receipt.Get("before",0).Double)/elapsed)+" /月",Blue);Label(balance,"当前库存包含结算后的主动操作；上表保留最近周期凭据。",15,Muted);Fold(balance,"结算凭据",c=>Raw(c,receipt));}else Label(balance,"尚未产生实际结算记录。",18,Muted);
  Label(sources,"库存与活动来源",21,Blue);foreach(var id in Rules.ids(S["projects"])){var projectId=id.Text;var p=S["projects"][id];var inventory=p["inventory"].Get(resource,0).Double;var cap=G.stock_cap_for(projectId,resource);var entries=m.Get("activity_ledger",V.Map()).Get(id,V.Arr()).Where(x=>x.Get("resource","").Text==resource).ToArray();if(cap<=0&&inventory<=0&&entries.Length==0)continue;var source=CompactTaskCard(sources,"Resource source "+projectId);Label(source,G.project_name(projectId),18);if(cap>0||inventory>0)Meter(source,"库存",inventory,cap," 单位",Blue);foreach(var item in entries){Label(source,(item.Get("kind","").Text=="input"?"投入":"产出")+" "+F(item.Get("rate",0).Double,3)+" /月",16,Muted);Label(source,"来源 "+item.Get("source",item.Get("source_id","本楼")).Text,14,Muted);Fold(source,"活动凭据",raw=>Raw(raw,item));}Button(source,"建筑与来源",()=>Navigate("building",projectId));}
  WorkspaceActions("地图定位 · "+Resource(resource),()=>{SetLayer(resource);InspectMapFromPage();});WorkspaceActions("管理此资源订单",()=>Navigate("market",resource));WorkspaceHint("收支为真实周期数量；月率按该周期时长换算。");
 }
 void ResourceBalanceRow(Transform parent,string caption,string value,Color color){var row=Row(parent,"Resource balance "+caption,32*TextScale);Height(row,32*TextScale);Label(row,caption,16,Muted,32*TextScale);var amount=Label(row,value,18,color,32*TextScale);amount.alignment=TextAnchor.MiddleRight;}
 void HistoryChart(Transform parent,V history,string key,string title,Color color){var records=history.Where(item=>item.Has(key)&&item[key].IsNumber&&item.Get("h").IsNumber).ToArray();if(records.Length<2){Label(parent,title+"：记录尚不足，未知值不画成零。",14,Muted);return;}Label(parent,title+" · 横轴来生月 / 纵轴实际通货",15,Muted);var r=Node("Measured History",parent);Height(r,260);var chart=r.gameObject.AddComponent<XCharts.Runtime.LineChart>();chart.Init();chart.UpdateTheme(XCharts.Runtime.ThemeType.Dark);chart.theme.sharedTheme.font=_font;chart.theme.transparentBackground=true;chart.EnsureChartComponent<XCharts.Runtime.Title>().show=false;chart.EnsureChartComponent<XCharts.Runtime.Tooltip>().show=true;chart.EnsureChartComponent<XCharts.Runtime.Legend>().show=false;var x=chart.EnsureChartComponent<XCharts.Runtime.XAxis>();var y=chart.EnsureChartComponent<XCharts.Runtime.YAxis>();x.type=XCharts.Runtime.Axis.AxisType.Category;y.type=XCharts.Runtime.Axis.AxisType.Value;x.splitNumber=8;x.boundaryGap=false;var grid=chart.EnsureChartComponent<XCharts.Runtime.GridCoord>();grid.left=80;grid.right=25;grid.top=22;grid.bottom=45;chart.RemoveData();var serie=chart.AddSerie<XCharts.Runtime.Line>(title);serie.lineStyle.color=color;serie.animation.enable=!Runtime.DisplaySettings.Get("reduced_motion",false).Bool;foreach(var item in records){chart.AddXAxisData(F(item["h"].Double,2));chart.AddData(0,item[key].Double);}chart.RefreshChart();}

 string _residentChoice="",_residentFilter="";
 readonly Dictionary<string,string> _residentFilterChoices=new Dictionary<string,string>();
 string ResidentHostName(string host)=>host=="homeless"?"待安置住户":G.project_name(host);
 void PopulationPage(){
  var m=S["metrics"];var columns=WorkspaceColumns("Population master detail",.38f,.62f);var list=columns[0];var detail=columns[1];
  Fold(list,"全城居住与服务汇总",citySummary=>{Label(citySummary,"居住与服务",24,Green);Meter(citySummary,"已安置 / 住房容量",m.Get("housed",0).Double,m.Get("housing",0).Double," 人",Green);Metric(citySummary,"待安置",F(m.Get("homeless",0).Double)+" 人");Metric(citySummary,"健康 / 幸福",F(m.Get("health",0).Double)+" / "+F(m.Get("happiness",0).Double));foreach(var kind in new[]{"food","medical","education","entertainment","commercial"}){var ratio=kind=="food"?Progress.food_ratio(G):Progress.service_ratio(G,kind);Meter(citySummary,kind=="food"?"实际口粮":Service(kind),ratio*100,100," %",ratio<.75?Gold:Blue);}});
  var filter=new[]{"homeless","food","medical","education","entertainment","commercial"}.Contains(_pageContext)?_pageContext:"all";
  Label(list,"住户名册 · "+PopulationFilterName(filter),21,Blue);
  var filterTabs=Row(list,"Resident filters primary",38*TextScale);foreach(var keyValue in new[]{"all","homeless","food"}){var key=keyValue;Button(filterTabs,PopulationFilterName(key),()=>Navigate("population",key),filter==key);}
  var serviceTabs=Row(list,"Resident filters services",38*TextScale);foreach(var keyValue in new[]{"medical","education","entertainment","commercial"}){var key=keyValue;Button(serviceTabs,PopulationFilterName(key),()=>Navigate("population",key),filter==key);}
  var allIds=Rules.ids(S["groups"]).Select(x=>x.Text).ToArray();var ids=allIds.Where(gid=>ResidentMatchesFilter(S["groups"][gid],filter)).ToArray();
  if(_residentFilter!=filter){if(_residentFilter!=""&&_residentChoice!="")_residentFilterChoices[_residentFilter]=_residentChoice;_residentFilter=filter;_residentChoice=_residentFilterChoices.TryGetValue(filter,out var remembered)&&ids.Contains(remembered)?remembered:ids.FirstOrDefault()??"";}
  else if(!ids.Contains(_residentChoice))_residentChoice=ids.FirstOrDefault()??"";
  Label(list,filter=="all"?ids.Length+" 户":ids.Length+" 户符合筛选 · 按最近实际分配",15,Muted);
  foreach(var gid in ids){var groupId=gid;var group=S["groups"][gid];var reading=filter!="all"&&filter!="homeless"?" · "+F(group.Get("living",V.Map()).Get(filter,0).Double*100,1)+"%":"";Button(list,(_residentChoice==gid?"● ":"")+ResidentHostName(group["host"].Text)+" · "+F(group["count"].Double)+" 人"+reading,()=>{_residentChoice=groupId;_residentFilterChoices[filter]=groupId;RenderPage();},_residentChoice==gid);}
  if(ids.Length==0)Label(list,"没有符合当前筛选的住户。",16,Muted);
  if(_residentChoice!=""){
   var gid=_residentChoice;var group=S["groups"][gid];var host=group["host"].Text;Label(detail,ResidentHostName(host)+" · 住户 "+gid,25,Green);Metric(detail,"实际人口",F(group["count"].Double)+" 人");foreach(var key in new[]{"health","happiness","education"})Metric(detail,V.Map("health","健康","happiness","幸福","education","教育").Get(key,key).Text,F(group.Get(key,0).Double)+" /100");var living=group.Get("living",V.Map());Metric(detail,"失业比例",F(living.Get("unemployment",1).Double*100)+"%");Metric(detail,"污染暴露",F(living.Get("pollution",0).Double,3));
   Label(detail,"口粮与服务实际分配",20,Blue);foreach(var kind in new[]{"food","medical","education","entertainment","commercial"})Meter(detail,kind=="food"?"口粮":Service(kind),living.Get(kind,0).Double*100,100," %",Blue);
   if(host!="homeless")Button(detail,"定位住宅 / 建筑档案",()=>{SelectBuilding(host);Navigate("building",host);});
   Label(detail,"就业去向",20,Blue);var links=m.Get("employment_links",V.Arr()).Where(x=>x.Get("source",x.Get("group",x.Get("group_id",""))).Text==gid).ToArray();if(links.Length==0)Label(detail,"暂无实际分配岗位。",16,Muted);foreach(var link in links){Label(detail,G.project_name(link.Get("target","").Text)+" · "+F(link.Get("people",0).Double)+" 人",17);Fold(detail,"就业分配凭据",c=>Raw(c,link));}
   Label(detail,"服务来源",20,Blue);foreach(var link in m.Get("service_links",V.Arr()))if(link.Get("group","").Text==gid){Label(detail,Service(link.Get("kind","").Text)+" ← "+G.project_name(link.Get("source","").Text)+" · "+F(link.Get("people",0).Double)+" 人",16,Muted);Fold(detail,"服务分配凭据",c=>Raw(c,link));}Fold(detail,"住户完整记录",c=>Raw(c,group));
  }else Label(detail,allIds.Length==0?"尚无住户，迁入后可逐户查看生活与就业。":"当前筛选没有住户。可切换其他服务或查看全部住户。",20,Muted);
  Fold(detail,"全城实际就业网络",c=>{foreach(var link in m.Get("employment_links",V.Arr())){var gid=link.Get("source",link.Get("group_id","")).Text;var home=S["groups"].Get(gid,V.Map()).Get("host",link.Get("home","")).Text;Label(c,ResidentHostName(home)+" → "+G.project_name(link.Get("target","").Text)+" · "+F(link.Get("people",0).Double)+" 人",16);}});
  WorkspaceActions(S.Get("migration_paused",false).Bool?"恢复迁入":"暂停迁入",()=>Command(g=>{g.toggle_migration();return "";}));WorkspaceActions(S.Get("goods_active",false).Bool?"生活用品已启用":"公布生活用品计划",()=>Command(g=>g.enable_goods()),S.Get("goods_active",false).Bool);WorkspaceActions("就业图层",()=>{SetLayer("employment");InspectMapFromPage();});
 }
 void RenderStatus(){RenderPlanningStatus();}
 void RefreshContext(){RefreshTilePanel();}
}
}
