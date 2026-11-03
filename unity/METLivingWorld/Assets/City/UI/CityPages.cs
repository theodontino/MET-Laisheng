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
 void ResourcesPage(){OperatingResourcesDetailPage();}
 void ResourceBalanceRow(Transform parent,string caption,string value,Color color){var row=Row(parent,"Resource balance "+caption,32*TextScale);Height(row,32*TextScale);Label(row,caption,16,Muted,32*TextScale);var amount=Label(row,value,18,color,32*TextScale);amount.alignment=TextAnchor.MiddleRight;}
 void HistoryChart(Transform parent,V history,string key,string title,Color color){var records=history.Where(item=>item.Has(key)&&item[key].IsNumber&&item.Get("h").IsNumber).ToArray();if(records.Length<2){Label(parent,title+"：记录尚不足，未知值不画成零。",14,Muted);return;}Label(parent,title+" · 横轴来生月 / 纵轴实际通货",15,Muted);var r=Node("Measured History",parent);Height(r,260);var chart=r.gameObject.AddComponent<XCharts.Runtime.LineChart>();chart.Init();chart.UpdateTheme(XCharts.Runtime.ThemeType.Dark);chart.theme.sharedTheme.font=_font;chart.theme.transparentBackground=true;chart.EnsureChartComponent<XCharts.Runtime.Title>().show=false;chart.EnsureChartComponent<XCharts.Runtime.Tooltip>().show=true;chart.EnsureChartComponent<XCharts.Runtime.Legend>().show=false;var x=chart.EnsureChartComponent<XCharts.Runtime.XAxis>();var y=chart.EnsureChartComponent<XCharts.Runtime.YAxis>();x.type=XCharts.Runtime.Axis.AxisType.Category;y.type=XCharts.Runtime.Axis.AxisType.Value;x.splitNumber=8;x.boundaryGap=false;var grid=chart.EnsureChartComponent<XCharts.Runtime.GridCoord>();grid.left=80;grid.right=25;grid.top=22;grid.bottom=45;chart.RemoveData();var serie=chart.AddSerie<XCharts.Runtime.Line>(title);serie.lineStyle.color=color;serie.animation.enable=!Runtime.DisplaySettings.Get("reduced_motion",false).Bool;foreach(var item in records){chart.AddXAxisData(F(item["h"].Double,2));chart.AddData(0,item[key].Double);}chart.RefreshChart();}

 string _residentChoice="",_residentFilter="";
 readonly Dictionary<string,string> _residentFilterChoices=new Dictionary<string,string>();
 string ResidentHostName(string host)=>host=="homeless"?"待安置住户":G.project_name(host);
 void PopulationPage(){OperatingPopulationDetailPage();}
 void RenderStatus(){if(CivicEnabled){if(!PointerBusy)RefreshCivicGuide();return;}RenderPlanningStatus();}
 void RefreshContext(){if(CivicEnabled){RefreshCivicTile();return;}RefreshTilePanel();}
}
}
