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
 void StartPage(){if(Runtime.Session.s["phase"].Text=="choose_district"){DistrictStartPage();return;}if(S["phase"].Text=="select_block"){Label(_body,"同一片河谷，不同的第一步。",27,Ink);Label(_body,"选择登陆位置，随后从本次起源中确定最初居民与补给。",18,Muted);var row=Row(_body,"Landing Sites",460);var names=new[]{"河湾","林缘旧村","北部高台"};var notes=new[]{"临水平地，邻近水岸。先铺开生活供给，再选择工业方向。","森林与既有住区相接。地块条件改变后续方案的价值。","高地与开阔地并存。保留水岸与土地特征，按实际场地规划。"};for(var i=0;i<3;i++){var index=i;var box=Card(row,"0"+(i+1)+"  "+names[i],notes[i],i==0?Blue:i==1?Green:Gold);Illustration(box,i==0?3:i==1?1:10,160);Button(box,"在此登陆",()=>Command(g=>g.select_block(index)),true);}}
  else {Label(_body,"为城市选定本次起源",27);var row=Row(_body,"Origins",420);foreach(var choice in S["origin_choices"]){var id=choice.Text;var origin=G.c.origins[id];var box=Card(row,origin.Get("name",id).Text,origin.Get("story",origin.Get("hint","")).Text,Gold);Illustration(box,id=="village"?3:id=="miners"?10:id=="charter"?12:1,130);Metric(box,"起始补充通货",F(origin.Get("cash",0).Double,0));foreach(var resource in new[]{"food","material","goods"})if(origin.Get(resource,0).Double!=0)Metric(box,Resource(resource),Signed(origin[resource].Double));Button(box,"确立起源 · 安顿居民",()=>{Command(g=>g.choose_origin(id));CloseArchive();},true);}}
 }
 RectTransform OverviewBrief(Transform parent,string name){var card=Surface(name,parent,new Color(.055f,.11f,.125f,.96f));var layout=card.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(10,10,5,5);layout.spacing=4;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;card.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;return card;}
 void OverviewPage(){
  var m=S["metrics"];var queue=Construction.queue(G);var guidance=CityGuidanceView();var settled=S.Get("simulation_tick",0).Long>0;
  var focus=OverviewBrief(_body,"Overview Next Action");
  var heading=Row(focus,"Overview Focus Heading",34*TextScale);heading.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;
  var art=Node("Overview Action Art",heading);Width(art,38*TextScale);Height(art,32*TextScale);
  Illustration(art,guidance["id"].Text=="housing"?17:guidance["id"].Text=="food"?3:guidance["id"].Text=="energy"?7:guidance["id"].Text=="material"?12:2,30*TextScale);Stretch((RectTransform)art.GetChild(0));
  var headingText=Label(heading,"下一步 · "+guidance["title"].Text,20,guidance.Get("severity",0).Long>=2?Gold:Green,32*TextScale);headingText.GetComponent<LayoutElement>().flexibleWidth=1;
  Label(focus,guidance["reason"].Text,16,Muted,40*TextScale);
  var action=Button(focus,guidance["action_label"].Text+" ›",RunCityGuidanceAction,true);action.name="Overview Primary Action";

  var homes=OverviewBrief(_body,"Overview Living Summary");
  Label(homes,"居住 · 已安置 "+F(m.Get("housed",0).Double,0)+" / 待 "+F(m.Get("homeless",0).Double,0)+" 人 · "+(settled?"口粮 "+F(Progress.food_ratio(G)*100,1)+"%":"口粮待结算"),17,Green,45*TextScale);

  var supply=OverviewBrief(_body,"Overview Supply Summary");
  Label(supply,"供给 · 粮食 "+F(G.inventory_total("food"))+" · 通货 "+F(S.Get("currency",0).Double)+" · "+(settled?"净额 "+KnownRate(m.Get("cashflow",0).Double):"月率待结算"),17,Blue,45*TextScale);

  var works=OverviewBrief(_body,"Overview Construction Summary");
  Label(works,"建设 · 工程 "+queue.Count+" 项 · CP "+F(Construction.capacity(G))+"/月 · "+(queue.Count>0?queue[0].Get("paused",false).Bool?"首项已暂停":"首项待施工":"可规划新址"),17,Gold,45*TextScale);

  var details=Column(_body,"Overview Detail Folds",4);
  Fold(details,"居住与服务详情",deep=>{Label(deep,settled?"实际口粮满足 "+F(Progress.food_ratio(G)*100,1)+"%":"口粮满足率等待首次结算",16,Muted);Button(deep,"查看居民与服务",()=>Navigate("population"));});
  Fold(details,"供给与来源详情",deep=>{Label(deep,settled?"粮食净变 "+Signed(m.Get("flows",V.Map()).Get("food",0).Double)+" /月 · 电力满足 "+F(m.Get("energy_ratio",1).Double*100,1)+"%":"实际流向与电力满足率等待首次结算",16,Muted);Button(deep,"资源与财政",()=>Navigate("resources"));Button(deep,"市场订单",()=>Navigate("market"));Button(deep,"规则与来源",()=>Navigate("sources"));});
  Fold(details,"施工队列与优先级",deep=>{foreach(var job in Construction.queue(G)){
   var id=job["id"].Text;var line=CompactTaskCard(deep,"Overview Queue Job "+id);
   Label(line,job["name"].Text+(job.Get("paused",false).Bool?" · 已暂停":" · 排队中"),17,Gold);
   Label(line,"已做 "+F(job["total"].Double-job["work"].Double)+" / "+F(job["total"].Double)+" CP  ·  仍需材料 "+F(job["material"].Double),16,Muted);
   var controls=Row(line,"Overview Queue Priority",55*TextScale);foreach(var priority in new[]{-1,0,1}){var p=priority;Button(controls,p<0?"延后":p>0?"优先":"普通",()=>Command(g=>Construction.set_priority(g,id,p)));}
   Button(line,"建筑档案",()=>Navigate("building",id));
  }});
  Fold(details,"方向、方案与记录",deep=>{Button(deep,"城市委任",()=>Navigate("themes"));Button(deep,"Bonus 与蓝图",()=>Navigate("bonuses"));Button(deep,"政策",()=>Navigate("policies"));Button(deep,"城市记录",()=>Navigate("records"));});
 }
 string KnownRate(double value)=>S.Get("simulation_tick",0).Long>0?Signed(value)+" /月":"等待首次实际结算";
 void ResourcesPage(){var m=S["metrics"];var audit=S.Get("audit",V.Map());Label(_body,"当前库存与最近周期实际流向",27);Label(_body,"库存是当前持有量；以下月率来自实际结算，电力使用供需能力口径。",16,Muted);
  foreach(var resource in new[]{"food","material","goods"}){var box=Card(_body,Resource(resource),"本地库存可达性以各建筑和居民分配来源为准。",resource=="food"?Green:resource=="material"?Gold:Blue);Meter(box,"当前街区库存",G.inventory_total(resource),G.stock_cap(resource)," 单位",Blue);var receipt=audit.Get("resources",V.Map()).Get(resource,V.Map());var elapsed=audit.Get("hours",0).Double;
   if(elapsed>0){var chain=Row(box,"Resource Flow",70);foreach(var pair in new[]{new[]{"produced","本地产出"},new[]{"imported","实际进口"},new[]{"consumed","真实消费"},new[]{"exported","实际外运"},new[]{"lost","损耗"}}){var column=Column(chain,"Flow",2);Label(column,pair[1],15,Muted);Label(column,F(receipt.Get(pair[0],0).Double/elapsed)+" /月",21,pair[0]=="produced"||pair[0]=="imported"?Green:Gold);}Metric(box,"净库存变化",KnownRate(m.Get("flows",V.Map()).Get(resource,0).Double));}else Label(box,"尚未产生实际结算记录。",17,Muted);
   var actions=Row(box);Button(actions,"地图定位 · "+Resource(resource),()=>{SetLayer(resource);CloseArchive();});Button(actions,"管理贸易订单",()=>Navigate("market",resource));Fold(box,"各建筑库存与实际活动来源",c=>{foreach(var id in Rules.ids(S["projects"])){var p=S["projects"][id];var inv=p["inventory"].Get(resource,0).Double;var cap=G.stock_cap_for(id.Text,resource);if(inv>0||cap>0)Meter(c,G.project_name(id.Text),inv,cap," 单位",Blue);foreach(var item in m.Get("activity_ledger",V.Map()).Get(id,V.Arr()))if(item.Get("resource","").Text==resource){Label(c,G.project_name(id.Text)+" · "+(item.Get("kind","").Text=="input"?"投入":"产出")+" "+F(item.Get("rate",0).Double,3)+"/月 · 来源 "+item.Get("source",item.Get("source_id","本楼")).Text,15,Muted);Fold(c,"原始活动凭据",raw=>Raw(raw,item));}}});
  }
  var energy=Card(_body,"全城电力供需","电力是供需能力，统一按全城满足比例分配，不是累计库存或空间电网。",Gold);Meter(energy,"实际供给 / 需求",m.Get("energy",0).Double,m.Get("energy_need",0).Double," 能力",Gold);Metric(energy,"满足率",F(100*m.Get("energy_ratio",1).Double)+"%");Button(energy,"查看电力来源图层",()=>{SetLayer("energy");CloseArchive();});
  var cash=Card(_body,"财政与流水","经营现金流、市场成交和主动操作分别记录。",Gold);Metric(cash,"通货余额",F(S["currency"].Double));Metric(cash,"经营收入",KnownRate(m.Get("income",0).Double));Metric(cash,"维护支出",KnownRate(m.Get("upkeep",0).Double));Metric(cash,"经营净额",KnownRate(m.Get("cashflow",0).Double));HistoryChart(cash,S.Get("civic032",V.Map()).Get("history",V.Arr()),"balance","通货历史 · 已有实际记录",Gold);Fold(cash,"主动操作与市场收支",c=>{foreach(var item in S.Get("civic032",V.Map()).Get("transactions",V.Arr()).Reverse().Take(80))Label(c,F(item["h"].Double)+" 月 · "+item["label"].Text+" · "+Signed(item["cash"].Double)+"通货 · 余额 "+F(item["balance"].Double),15,Muted);});
 }
 void HistoryChart(Transform parent,V history,string key,string title,Color color){var records=history.Where(item=>item.Has(key)&&item[key].IsNumber&&item.Get("h").IsNumber).ToArray();if(records.Length<2){Label(parent,title+"：记录尚不足，未知值不画成零。",14,Muted);return;}Label(parent,title+" · 横轴来生月 / 纵轴实际通货",15,Muted);var r=Node("Measured History",parent);Height(r,260);var chart=r.gameObject.AddComponent<XCharts.Runtime.LineChart>();chart.Init();chart.UpdateTheme(XCharts.Runtime.ThemeType.Dark);chart.theme.sharedTheme.font=_font;chart.theme.transparentBackground=true;chart.EnsureChartComponent<XCharts.Runtime.Title>().show=false;chart.EnsureChartComponent<XCharts.Runtime.Tooltip>().show=true;chart.EnsureChartComponent<XCharts.Runtime.Legend>().show=false;var x=chart.EnsureChartComponent<XCharts.Runtime.XAxis>();var y=chart.EnsureChartComponent<XCharts.Runtime.YAxis>();x.type=XCharts.Runtime.Axis.AxisType.Category;y.type=XCharts.Runtime.Axis.AxisType.Value;x.splitNumber=8;x.boundaryGap=false;var grid=chart.EnsureChartComponent<XCharts.Runtime.GridCoord>();grid.left=80;grid.right=25;grid.top=22;grid.bottom=45;chart.RemoveData();var serie=chart.AddSerie<XCharts.Runtime.Line>(title);serie.lineStyle.color=color;serie.animation.enable=!Runtime.DisplaySettings.Get("reduced_motion",false).Bool;foreach(var item in records){chart.AddXAxisData(F(item["h"].Double,2));chart.AddData(0,item[key].Double);}chart.RefreshChart();}

 void PopulationPage(){var m=S["metrics"];var homes=Card(_body,"居民与住房","住户保留身份；迁入按来生本地时间，服务使用真实有限名额。",Green);Meter(homes,"已安置 / 住房容量",m.Get("housed",0).Double,m.Get("housing",0).Double," 人",Green);Metric(homes,"待安置人口",F(m.Get("homeless",0).Double)+" 人");var controls=Row(homes);Button(controls,S.Get("migration_paused",false).Bool?"恢复迁入":"暂停迁入",()=>Command(g=>{g.toggle_migration();return "";}));Button(controls,S.Get("goods_active",false).Bool?"生活用品消费已启用":"公布生活用品计划",()=>Command(g=>g.enable_goods()),false,S.Get("goods_active",false).Bool);
  var living=Card(_body,"生活满足率","覆盖范围不等于实际服务人数；以已收到的口粮和名额为准。",Blue);foreach(var kind in new[]{"food","medical","education","entertainment","commercial"}){var ratio=kind=="food"?Progress.food_ratio(G):Progress.service_ratio(G,kind);Meter(living,kind=="food"?"实际口粮":Service(kind),ratio*100,100," %",ratio<.75?Gold:Blue);}Metric(living,"平均健康 / 幸福",F(m.Get("health",0).Double)+" / "+F(m.Get("happiness",0).Double));
  foreach(var gid in Rules.ids(S["groups"])){var group=S["groups"][gid];var host=group["host"].Text;var card=Card(_body,G.project_name(host)+" · "+F(group["count"].Double)+"人","住户 "+gid.Text,Green);var row=Row(card,"Resident Measures",50);Label(row,"健康 "+F(group["health"].Double),20);Label(row,"幸福 "+F(group["happiness"].Double),20);Label(row,"教育 "+F(group.Get("education",0).Double),20);var before=group.Get("living",V.Map());Metric(card,"失业比例",F(before.Get("unemployment",1).Double*100)+"%");Metric(card,"污染暴露",F(before.Get("pollution",0).Double,3));if(host!="homeless")Button(card,"定位住宅 / 建筑档案",()=>{SelectBuilding(host);Navigate("building",host);});Fold(card,"工作、口粮与服务完整来源",c=>{Raw(c,group);foreach(var link in m.Get("employment_links",V.Arr()))if(link.Get("source",link.Get("group",link.Get("group_id",""))).Text==gid.Text)Raw(c,link);foreach(var link in m.Get("service_links",V.Arr()))if(link.Get("home","").Text==host)Label(c,Service(link.Get("kind","").Text)+" ← "+G.project_name(link.Get("source","").Text)+" · "+F(link.Get("people",0).Double)+"人",15,Muted);});}
  var workers=Card(_body,"实际就业网络","连接展示来源住区、目标岗位与实际分配。",Blue);foreach(var link in m.Get("employment_links",V.Arr())){var row=Row(workers);var gid=link.Get("source",link.Get("group_id","")).Text;var home=S["groups"].Get(gid,V.Map()).Get("host",link.Get("home","")).Text;Label(row,G.project_name(home)+" → "+G.project_name(link.Get("target","").Text),17);Label(row,F(link.Get("people",0).Double)+" 人",20,Blue);}Button(workers,"打开就业图层",()=>{SetLayer("employment");CloseArchive();});
 }
 void RenderStatus(){RenderPlanningStatus();}
 void RefreshContext(){
  if(PointerBusy){_contextDirty=true;return;}
  _contextDirty=false;
  var visible=!_mapViewing&&SelectedTile!=""&&S["cells"].Has(SelectedTile);
  _context.gameObject.SetActive(visible);
  var displayKey=ContextDisplayKey();if(displayKey==_contextKey)return;
  _contextKey=displayKey;ContextRebuildCount++;Clear(_context);if(!visible)return;
  var body=Column(_context,"Context",5);
  body.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(11,11,9,9);
  if(!Runtime.ViewCanEditFocused){
   Label(body,"自治地区 · 实际城市状态",21,Blue,27);
   Label(body,"居民和供给继续运行；回任后可调整建设与构筑。",15,Muted,42);
   var govern=Button(body,"查看回任与治理条件 ›",()=>Navigate("governance",Runtime.Session.Block(Runtime.Session.FocusedBlockId)["district_id"].Text),true);Height((RectTransform)govern.transform,42*TextScale);return;
  }
  if(SelectedBuilding!=""&&S["projects"].Has(SelectedBuilding)){
   var id=SelectedBuilding;var p=S["projects"][id];var st=G.project_stats(id);
   var title=Row(body,"Building Identity",126);title.GetComponent<HorizontalLayoutGroup>().spacing=9;title.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;
   ContextBuildingPortrait(title,id,p["def"].Text);
   var words=Column(title,"Identity",2);words.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);words.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
   var closeContext=Button(title,"×",DismissLocalSelection);Width((RectTransform)closeContext.transform,30);Height((RectTransform)closeContext.transform,30);
   Label(words,G.project_name(id),20,Domain(p),42);
   Label(words,"等级 T"+p.Get("tier",1).Long,14,Ink,19);
   Label(words,G.c.operators[p["operator_org"]]["name"].Text,13,Muted,20);
   string status;Action primaryAction;string primaryText;
   if(p.Get("paused",false).Bool){status="运营已暂停";primaryText="恢复运营";primaryAction=()=>Command(g=>g.set_paused(id,false));}
   else if(p["status"].Text=="construction"||p.Get("upgrade_job",V.Map()).Count!=0){
    status="施工中 · "+p.Get("bottleneck","共享施工中").Text;
    primaryText="施工管理";primaryAction=()=>Navigate("building",id);
   }else{
    status=p["status"].Text=="running"?"正常运营":"状态 · "+p["status"].Text;
    if(st.Get("trade_capacity",0).Double>0){primaryText="打开市场";primaryAction=()=>Navigate("market");}
    else if(Development.choices(G,id).Count!=0){primaryText="成长与升级";primaryAction=()=>Navigate("upgrades",id);}
    else {primaryText="运营管理";primaryAction=()=>Navigate("building",id);}
   }
   Label(words,status,13,p.Get("paused",false).Bool?Gold:Green,32);
   var data=Row(body,"Actual Building Resources",30*TextScale);data.GetComponent<HorizontalLayoutGroup>().spacing=5;
   if(p.Get("paused",false).Bool)ContextDatum(data,"pause","运营已暂停");
   else if(p["status"].Text=="construction"||p.Get("upgrade_job",V.Map()).Count!=0){
    var job=p.Get("upgrade_job",V.Map()).Count!=0?p["upgrade_job"]:p["job"];
    ContextDatum(data,"build",F(job["total"].Double-job["work"].Double)+" / "+F(job["total"].Double)+" CP");
    ContextDatum(data,"material",F(job["material"].Double)+" 待用");
   }else{
    var housing=p["status"].Text=="running"?Math.Max(0,st.Get("housing",0).Double*st.Get("efficiency",1).Double):0;
    if(housing>0){var assigned=S["groups"].Values.Where(x=>x["host"].Text==id).Sum(x=>x["count"].Double);ContextDatum(data,"population",F(assigned)+" / "+F(housing)+" 人");}
    else if(st.Get("trade_capacity",0).Double>0){var stock=new[]{"food","material","goods"}.FirstOrDefault(r=>G.stock_cap_for(id,r)>0);if(stock!=null)ContextDatum(data,stock,Resource(stock)+" "+F(p["inventory"].Get(stock,0).Double)+" / "+F(G.stock_cap_for(id,stock)));}
    else {var outputs=p.Get("outputs_last",V.Map());foreach(var output in outputs.Keys.Take(2))ContextDatum(data,output,Resource(output)+" "+F(outputs[output].Double)+" /月");if(outputs.Count==0){var use=S["metrics"].Get("utilization",V.Map()).Get(id,V.Map());ContextDatum(data,"city","运行 "+F(use.Get("utilization",p.Get("usage",0)).Double*100)+"%");}}
   }
   var slotIds=G.slot_ids(id).Select(x=>x.Text).ToArray();
   if(slotIds.Length>0){
    var installedCount=slotIds.Count(slot=>p["slots"].Get(slot,"").Text!="");
    Label(body,"构筑槽 · 已安装 "+installedCount+" / "+slotIds.Length,13,Muted,19);
    foreach(var kind in new[]{"Production","Logistics","Operations","Flex"}){
     var group=slotIds.Where(slot=>G.slot_type(id,slot)==kind).ToArray();if(group.Length==0)continue;
     ContextSection(body,kind,Catalog.SLOT_NAMES.Get(kind,kind).Text,Domain(p));
     for(var offset=0;offset<group.Length;offset+=2){
      var slots=Row(body,"Real Bonus Slots",36*TextScale);slots.GetComponent<HorizontalLayoutGroup>().spacing=6;
      for(var index=offset;index<Math.Min(offset+2,group.Length);index++){
       var slot=group[index];var iid=p["slots"].Get(slot,"").Text;
       var installed=iid!=""&&S["bonus_instances"].Has(iid)?S["bonus_instances"][iid]:V.Map();
       var bid=installed.Get("bonus_id","").Text;
       var bonusName=bid!=""&&G.c.bonuses.Has(bid)?G.c.bonuses[bid]["name"].Text:iid;
       var caption=G.slot_label(id,slot)+" · "+(iid==""?"未安装 · ＋":"已安装 "+bonusName);
       var slotButton=Button(slots,caption,()=>Navigate("slot",id+"|"+slot));
       Height((RectTransform)slotButton.transform,36*TextScale);slotButton.GetComponentInChildren<Text>().fontSize=Mathf.RoundToInt(13*TextScale);
      }
     }
    }
   }
   var actions=Row(body,"Building Actions",34*TextScale);actions.GetComponent<HorizontalLayoutGroup>().spacing=6;
   var primary=Button(actions,primaryText=="运营管理"||primaryText=="施工管理"?"管理 / 详情 ›":primaryText,primaryAction);Height((RectTransform)primary.transform,34*TextScale);
   if(primaryText!="运营管理"&&primaryText!="施工管理") {var details=Button(actions,"详情 ›",()=>Navigate("building",id));Height((RectTransform)details.transform,34*TextScale);}
  }else{
   var tile=S["cells"][SelectedTile];
   var terrain=Row(body,"Site Terrain",40*TextScale);terrain.GetComponent<HorizontalLayoutGroup>().spacing=6;
   HudIcon(terrain,"terrain",Gold,29);
   Label(terrain,"选址 · "+SelectedTile,21,Gold,38*TextScale);
   var closeSite=Button(terrain,"×",DismissLocalSelection);Width((RectTransform)closeSite.transform,30);Height((RectTransform)closeSite.transform,30);
   Label(body,G.c.tags_text(tile["traits"]),14,Muted,34*TextScale);
   if(tile["traits"].Contains("tile.water")){Label(body,"水域地块不能建设。",16,Muted,26);return;}
   Label(body,"基础建设 · 选择类型",13,Muted,19);
   var choices=G.c.v040["progression"]["tier1"].Select(x=>x.Text).ToArray();
   for(var offset=0;offset<choices.Length;offset+=2){
    var types=Row(body,"Foundations",38*TextScale);types.GetComponent<HorizontalLayoutGroup>().spacing=6;
    for(var index=offset;index<Math.Min(offset+2,choices.Length);index++){
     var baseId=choices[index];var selected=_base==baseId;
     var button=Button(types,(selected?"✓ ":"")+G.c.projects[baseId]["name"].Text,()=>{BeginConstructionIntent();_base=baseId;_contextKey="";RefreshContext();},selected);
     Height((RectTransform)button.transform,38*TextScale);button.GetComponentInChildren<Text>().fontSize=Mathf.RoundToInt(15*TextScale);
     var icon=HudIcon(button.transform,"build",selected?Green:Blue,19);Pin(icon,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(13,0),new Vector2(19,19));
     var caption=button.GetComponentInChildren<Text>();Stretch(caption.rectTransform,37,2,4,2);
    }
   }
   var def=G.c.projects[_base];var preview=_contextPreview;
   Label(body,"假定完工 · "+def["name"].Text,14,Blue,21);
   if(preview.Get("ok",false).Bool){
    var outputs=preview["outputs"];var output=outputs.Keys.FirstOrDefault();
    if(output!=null){var forecast=Row(body,"Site Forecast",29*TextScale);ContextDatum(forecast,output,Resource(output)+" "+F(outputs[output].Double)+" /月");}
    if(preview["harmed"].Double>0)Label(body,"环境影响 · "+F(preview["harmed"].Double,0)+" 位住户暴露上升",13,Gold,21);
   }
   var cost=Row(body,"Actual Site Cost",29*TextScale);cost.GetComponent<HorizontalLayoutGroup>().spacing=4;
   ContextDatum(cost,"currency",F(def["cost"].Double,0));ContextDatum(cost,"material",F(def["material"].Double,0));ContextDatum(cost,"build",F(def["construction_required"].Double)+" CP");
   var tender=Button(body,"选方案并建造 ›",()=>{var tileId=SelectedTile;Command(g=>g.create_tender(V.Arr(tileId),_base),false);if(S["draft"].Count!=0){_offer="";Navigate("tender");}},true);Height((RectTransform)tender.transform,43*TextScale);
  }
 }
}
}
