using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UIElements;
using C = MET.City.CivicUI;

namespace MET.City {
public sealed partial class CityInterface {
 // These pages only read the detached view. All edits continue through Runtime commands.
 bool RenderCivicOperating(VisualElement body,VisualElement actions) {
  switch(_page) {
   case "population_home": case "population": CivicPopulation(body,actions);return true;
   case "economy_home": case "resources": CivicEconomy(body,actions);return true;
   case "market": CivicMarket(body,actions);return true;
   case "policies": CivicPolicies(body,actions);return true;
   case "construction": CivicConstruction(body,actions);return true;
   default:return false;
  }
 }
 void CivicPending(VisualElement parent) {if(!OperatingSettled)C.Label(parent,"待首次实际结算 · 库存与容量已就绪，月流、就业和服务使用尚未记录。","c-small c-warning");}
 void CivicTabs(VisualElement parent,IEnumerable<string> ids,Func<string,string> title,string selected,Action<string> choose) {
  var row=C.Row(parent,"c-wrap");foreach(var item in ids){var id=item;var b=C.Button(row,title(id),()=>choose(id));b.EnableInClassList("is-selected",selected==id);}
 }
 void CivicObjectActions(VisualElement parent,string id) {
  if(!S["projects"].Has(id))return;var r=C.Row(parent);
  C.Button(r,"检查建筑",()=>Navigate("building",id));C.Button(r,"地图定位",()=>{SelectBuilding(id);InspectMapFromPage();});
 }
 void CivicKnownMeter(VisualElement parent,string title,double value,double capacity,string unit,string hint="",bool known=true) {
  if(known)C.Meter(parent,title,value,capacity,unit,hint,value>capacity?"c-warning":"");
  else C.Value(parent,title,"待结算",hint);
 }
 void CivicAdvancedLink(VisualElement parent,string route,string context="") {if(!_advancedWorkspace)C.Button(parent,"完整来源与详表 ↗",()=>OpenAdvancedWorkspace(route,context));}

 void CivicPopulation(VisualElement body,VisualElement actions) {
  if(_page=="population"&&new[]{"all","homeless","food","medical","education","entertainment","commercial"}.Contains(_pageContext))_operatingResidentFilter=_pageContext;
  var m=S["metrics"];var settled=OperatingSettled;CivicPending(body);
  var overview=C.Section(body,"人口与安居",settled?"最近实际分配":"容量现状");var readings=C.Row(overview,"c-wrap");
  C.Value(readings,"居民",F(m.Get("population",0).Double)+" 人");C.Value(readings,"健康",F(m.Get("health",0).Double,1));C.Value(readings,"幸福",F(m.Get("happiness",0).Double,1));
  var capacities=C.Row(overview);CivicKnownMeter(C.Column(capacities,"c-half"),"住房入住",m.Get("housed",0).Double,m.Get("housing",0).Double,"人","待安置 "+F(m.Get("homeless",0).Double)+" 人");
  CivicKnownMeter(C.Column(capacities,"c-half"),"居民就业",m.Get("employed",0).Double,m.Get("population",0).Double,"人","全城岗位 "+F(m.Get("jobs",0).Double),settled);
  CivicKnownMeter(overview,"本地岗位使用",m.Get("employed",0).Double,m.Get("jobs",0).Double,"岗位",settled?"空余 "+F(Math.Max(0,m.Get("jobs",0).Double-m.Get("employed",0).Double))+" 岗位":"实际就业分配待结算",settled);
  var services=C.Section(body,"口粮与公共服务",settled?"覆盖比例":"实际覆盖待结算");var serviceRows=C.Row(services,"c-wrap");
  foreach(var keyValue in new[]{"food","medical","education","entertainment","commercial"}) {
   var key=keyValue;var cell=C.Column(serviceRows,"c-third");var ratio=key=="food"?Progress.food_ratio(G):Progress.service_ratio(G,key);
   CivicKnownMeter(cell,key=="food"?"口粮":Service(key),ratio*100,100,"%",settled?S["groups"].Values.Count(g=>ResidentMatchesFilter(g,key))+" 组不足":"",settled);
   C.Button(cell,"追查不足",()=>{_operatingResidentFilter=key;_pageContext="";if(key!="food")SetLayer(key);RenderPage();});
  }
  var list=C.Section(body,"住区运行",PopulationFilterName(_operatingResidentFilter));
  CivicTabs(list,new[]{"all","homeless","food","medical","education","entertainment","commercial"},PopulationFilterName,_operatingResidentFilter,key=>{_operatingResidentFilter=key;_pageContext="";_residentChoice="";RenderPage();});
  CivicTabs(list,new[]{"buildings","groups"},key=>key=="buildings"?"住宅容量":"实际住户组",_operatingResidentView,key=>{_operatingResidentView=key;RenderPage();});
  CivicTabs(list,new[]{"population","vacancy","health","happiness"},key=>V.Map("population","人口 ↓","vacancy","空房 ↓","health","健康 ↑","happiness","幸福 ↑")[key].Text,_operatingResidentSort,key=>{_operatingResidentSort=key;RenderPage();});
  var filter=_operatingResidentFilter;
  var groups=SortOperatingGroups(S["groups"].Keys.Where(id=>(settled||filter=="all"||filter=="homeless")&&ResidentMatchesFilter(S["groups"][id],filter)).ToArray());
  if(_operatingResidentView=="buildings"&&filter!="homeless") {
   var homes=OperatingHomes(filter);var visible=_advancedWorkspace?homes:homes.Take(5).ToArray();
   if(_advancedWorkspace)C.TableRow(list,new[]{"住宅 / 地块","入住 / 容量","健康 / 幸福","操作"},true);
   foreach(var idValue in visible){var id=idValue;var people=OperatingHomePeople(id);var home=C.Column(list,"c-section");
    if(_advancedWorkspace){var r=C.TableRow(home,new[]{OperatingProjectIdentity(id),F(people)+" / "+F(OperatingHomeCapacity(id)),people>0?F(OperatingHomeScore(id,"health"))+" / "+F(OperatingHomeScore(id,"happiness")):"暂无住户"});C.Button(r,_operatingHomeChoice==id?"已选":"住户来源",()=>{_operatingHomeChoice=id;_residentChoice="";RenderPage();});}
    else {C.Label(home,OperatingProjectIdentity(id),"c-heading");C.Meter(home,"入住",people,OperatingHomeCapacity(id),"人");}
    CivicObjectActions(home,id);if(!_advancedWorkspace)C.Button(home,"查看住户分配",()=>OpenCivicHomeManagement(id,filter));
   }
   if(homes.Length==0)C.Empty(list,"当前筛选没有住宅",settled?"可切换至实际住户组。":"不足筛选待首次实际结算。");
   if(!_advancedWorkspace&&homes.Length>visible.Length)C.Label(list,"另有 "+(homes.Length-visible.Length)+" 栋住宅，详表可查看全部。","c-small c-muted");
   if(_advancedWorkspace){if(!homes.Contains(_operatingHomeChoice))_operatingHomeChoice=homes.FirstOrDefault()??"";groups=groups.Where(id=>S["groups"][id].Get("host","").Text==_operatingHomeChoice).ToArray();}
  }
  if(_advancedWorkspace||_operatingResidentView=="groups"||filter=="homeless") {
   if(!groups.Contains(_residentChoice))_residentChoice=groups.FirstOrDefault()??"";
   C.Label(list,"实际住户组 · "+groups.Length+" 组","c-heading");foreach(var idValue in (_advancedWorkspace?groups:groups.Take(5))){var id=idValue;var group=S["groups"][id];C.Choice(list,ResidentHostName(group.Get("host","").Text)+" · "+F(group.Get("count",0).Double)+" 人",id,_residentChoice==id,()=>{_residentChoice=id;_residentFilterChoices[filter]=id;RenderPage();});}
   if(groups.Length==0)C.Empty(list,"此筛选下暂无住户组");
   if(_residentChoice!="")CivicResident(body,_residentChoice);
  }
  C.Button(actions,S.Get("migration_paused",false).Bool?"恢复迁入":"暂停迁入",()=>Command(g=>{g.toggle_migration();return "";}));
  C.Button(actions,S.Get("goods_active",false).Bool?"用品计划已启用":"公布用品计划",()=>Command(g=>g.enable_goods()),false,!S.Get("goods_active",false).Bool);
  C.Button(actions,"就业图层",()=>{SetLayer("employment");InspectMapFromPage();});CivicAdvancedLink(body,"population",filter);
 }
 void CivicResident(VisualElement body,string gid) {
  var group=S["groups"][gid];var m=S["metrics"];var detail=C.Section(body,"住户分配 · "+gid,ResidentHostName(group.Get("host","").Text));
  C.Pair(detail,"住户人数",F(group.Get("count",0).Double)+" 人");C.Pair(detail,"健康 / 幸福 / 教育",F(group.Get("health",0).Double)+" / "+F(group.Get("happiness",0).Double)+" / "+F(group.Get("education",0).Double));CivicObjectActions(detail,group.Get("host","").Text);
  if(OperatingSettled) {
   var living=group.Get("living",V.Map());CivicKnownMeter(detail,"就业",group.Get("employment",V.Map()).Values.Sum(v=>v.Double),group.Get("count",0).Double,"人");
   C.Pair(detail,"失业 / 污染暴露",F(living.Get("unemployment",1).Double*100)+"% / "+F(living.Get("pollution",0).Double,3));
   foreach(var key in new[]{"food","medical","education","entertainment","commercial"})C.Meter(detail,key=="food"?"口粮":Service(key),living.Get(key,0).Double*100,100,"%");
   C.Fold(detail,"就业去向与服务提供者",sources=> {
    foreach(var link in m.Get("employment_links",V.Arr()).Where(x=>x.Get("group",x.Get("source",x.Get("group_id",""))).Text==gid)){var id=link.Get("target","").Text;C.Pair(sources,OperatingProjectIdentity(id),F(link.Get("people",0).Double)+" 人就业");CivicObjectActions(sources,id);}
    foreach(var entry in m.Get("service_pools",V.Map()).Pairs){var pool=entry.Value;var received=m.Get("service_links",V.Arr()).Where(link=>link.Get("pool","").Text==entry.Key&&link.Get("group","").Text==gid).Sum(link=>link.Get("people",0).Double);var capacity=pool.Get("capacity",0).Double;
     var section=C.Section(sources,Service(pool.Get("kind","").Text),"本组覆盖 "+F(received)+" 人");C.Meter(section,"池容量使用",capacity-pool.Get("remaining",capacity).Double,capacity,"人");foreach(var member in pool.Get("members",V.Arr())){C.Label(section,OperatingProjectIdentity(member.Text));CivicObjectActions(section,member.Text);}
    }
   },_advancedWorkspace);
  }else C.Label(detail,"就业与服务来源待首次实际结算。","c-muted");
  if(_advancedWorkspace){CivicEvidence(detail,"住户完整凭据",group);CivicEvidence(detail,"本组就业凭据",V.From(m.Get("employment_links",V.Arr()).Where(x=>x.Get("group",x.Get("source",x.Get("group_id",""))).Text==gid)));CivicEvidence(detail,"本组服务凭据",V.From(m.Get("service_links",V.Arr()).Where(x=>x.Get("group","").Text==gid)));}
 }

 V CivicContributions(string resource) {
  var m=S["metrics"];var rows=V.Arr();if(!OperatingSettled)return rows;if(resource=="currency")return OperatingFiscalContributions(m);
  if(resource=="energy") {
   foreach(var p in m.Get("energy_sources",V.Map()).Pairs)rows.Add(V.Map("id",p.Key,"label",p.Key=="municipal"?"市政基础供电":OperatingProjectIdentity(p.Key),"value",p.Value.Double));
   foreach(var p in m.Get("energy_requests",V.Map()).Pairs)rows.Add(V.Map("id",p.Key,"label",OperatingProjectIdentity(p.Key)+" · 用电","value",-p.Value.Double));
   var people=m.Get("population_energy",V.Map()).Values.Sum(v=>v.Double);if(people>0)rows.Add(V.Map("id","","label","居民用电","value",-people));return rows;
  }
  foreach(var p in m.Get("activity_ledger",V.Map()).Pairs){var rate=p.Value.Where(x=>x.Get("resource","").Text==resource).Sum(x=>x.Get("kind","").Text=="output"?x.Get("kept",x.Get("rate",0)).Double:-x.Get("rate",0).Double);if(Math.Abs(rate)>.000001)rows.Add(V.Map("id",p.Key,"label",OperatingProjectIdentity(p.Key),"value",rate));}
  var remainder=m.Get("flows",V.Map()).Get(resource,0).Double-rows.Sum(x=>x["value"].Double);if(Math.Abs(remainder)>.000001)rows.Add(V.Map("id","","label","贸易、交付、损耗与周期调整","value",remainder));return rows;
 }
 void CivicSignedFlow(VisualElement parent,string title,double value,double scale,string unit,string id="") {
  var row=C.Row(parent);var caption=C.Column(row,"c-half");if(S["projects"].Has(id))C.Button(caption,title,()=>Navigate("building",id));else C.Label(caption,title,"c-small");
  var track=C.Box(row,"c-track c-grow");track.style.alignSelf=Align.Center;track.style.position=Position.Relative;track.style.height=10;
  var axis=C.Box(track);axis.style.position=Position.Absolute;axis.style.left=Length.Percent(50);axis.style.width=1;axis.style.top=0;axis.style.bottom=0;axis.style.backgroundColor=new Color(.5f,.65f,.65f,.65f);
  var fill=C.Box(track,"c-fill");fill.style.position=Position.Absolute;var width=(float)(scale>0?Math.Min(1,Math.Abs(value)/scale)*50:0);fill.style.left=Length.Percent(value<0?50-width:50);fill.style.width=Length.Percent(width);fill.style.top=0;fill.style.bottom=0;if(value<0)fill.style.backgroundColor=new Color(.82f,.63f,.28f);
  C.Label(row,Signed(value)+" "+unit,"c-align-right "+(value<0?"c-warning":"c-good"));
 }
 void CivicEconomy(VisualElement body,VisualElement actions) {
  var resources=new[]{"food","material","goods","currency","energy"};if(_page=="resources"&&resources.Contains(_pageContext))_operatingEconomyResource=_pageContext;var selected=_operatingEconomyResource;var m=S["metrics"];CivicPending(body);
  var overview=C.Section(body,"库存与月流",OperatingSettled?"最近实际 / 来生月":"月流待结算");var cards=C.Row(overview,"c-wrap");
  foreach(var resourceValue in resources){var r=resourceValue;var current=r=="currency"?S.Get("currency",0).Double:r=="energy"?m.Get("energy",0).Double:G.inventory_total(r);var net=r=="currency"?m.Get("cashflow",0).Double:r=="energy"?m.Get("energy",0).Double-m.Get("energy_need",0).Double:m.Get("flows",V.Map()).Get(r,0).Double;
   var cell=C.Column(cards,"c-half");C.Choice(cell,Resource(r),r=="energy"?(OperatingSettled?"供给 "+F(current)+" · 需求 "+F(m.Get("energy_need",0).Double):"实际供需待结算"):"当前 "+F(current)+(OperatingSettled?" · "+Signed(net)+" /月":" · 月流待结算"),selected==r,()=>{_operatingEconomyResource=r;_pageContext="";RenderPage();},r);
   if(r!="currency"&&r!="energy")C.Meter(cell,"仓容",current,G.stock_cap(r),"单位");
  }
  CivicKnownMeter(overview,"电力占用",m.Get("energy_need",0).Double,m.Get("energy",0).Double,"能力","全城统一分配",OperatingSettled);
  var flow=C.Section(body,Resource(selected)+" · 来源贡献",selected=="energy"?"← 用电　供电 →":"← 消耗　产出 →");var contributions=CivicContributions(selected).OrderByDescending(x=>Math.Abs(x["value"].Double)).ToArray();var scale=contributions.Length>0?contributions.Max(x=>Math.Abs(x["value"].Double)):0;
  foreach(var item in (_advancedWorkspace?contributions:contributions.Take(6)))CivicSignedFlow(flow,item["label"].Text,item["value"].Double,scale,selected=="energy"?"能力":"/月",item["id"].Text);
  if(contributions.Length==0)C.Empty(flow,OperatingSettled?"当前没有非零贡献":"尚无实际月流凭据");
  C.Label(flow,"条长使用统一刻度。库存是当前状态；月流、电力是最近实际结算。经营通货不含主动交易与一次性费用。","c-small c-muted");
  if(_advancedWorkspace)CivicEconomyDetails(body,selected);
  CivicAdvancedLink(body,"resources",selected);C.Button(actions,"市场订单",()=>Navigate("market",new[]{"food","material","goods"}.Contains(selected)?selected:""));C.Button(actions,"政策比较",()=>Navigate("policies"));
  if(selected!="currency")C.Button(actions,"资源图层",()=>{SetLayer(selected);InspectMapFromPage();});
 }
 void CivicEconomyDetails(VisualElement body,string resource) {
  var m=S["metrics"];var section=C.Section(body,Resource(resource)+" · 完整来源对照");
  if(resource=="energy") {
   var supply=m.Get("energy_sources",V.Map());var demand=m.Get("energy_requests",V.Map());var people=m.Get("population_energy",V.Map());
   C.TableRow(section,new[]{"来源 / 地块","供给","建筑需求","住户需求","供需余缺"},true);
   foreach(var id in supply.Keys.Union(demand.Keys).Union(people.Keys.Select(gid=>S["groups"].Get(gid,V.Map()).Get("host","").Text)).Where(id=>id!="")) {
    var resident=people.Pairs.Where(p=>S["groups"].Get(p.Key,V.Map()).Get("host","").Text==id).Sum(p=>p.Value.Double);C.TableRow(section,new[]{id=="municipal"?"市政基础供电":OperatingProjectIdentity(id),F(supply.Get(id,0).Double),F(demand.Get(id,0).Double),F(resident),Signed(supply.Get(id,0).Double-demand.Get(id,0).Double-resident)});CivicObjectActions(section,id);
   }
   C.Label(section,"来源归属的供需差；供电仍在全城统一分配。","c-small c-muted");CivicEvidence(section,"供电来源凭据",supply);CivicEvidence(section,"建筑需求凭据",demand);CivicEvidence(section,"住户需求凭据",people);return;
  }
  if(resource=="currency") {
   C.Pair(section,"经营收入",OperatingSettled?F(m.Get("income",0).Double)+" /月":"待结算");C.Pair(section,"维护支出",OperatingSettled?F(m.Get("upkeep",0).Double)+" /月":"待结算");
   var receipts=m.Get("fiscal_sources",V.Arr());var expenses=m.Get("fiscal_ledger",V.Arr()).Where(x=>x.Get("kind","").Text=="expense").ToArray();C.TableRow(section,new[]{"来源","税收","用地","排污","维护","归属净额"},true);
   foreach(var id in receipts.Select(x=>x.Get("source_id","").Text).Union(expenses.Select(x=>x.Get("source_id","").Text))) {
    var own=receipts.Where(x=>x.Get("source_id","").Text==id);double tax=own.Where(x=>x.Get("aggregate","").Text=="tax").Sum(x=>x.Get("rate",0).Double),land=own.Where(x=>x.Get("aggregate","").Text=="land_rent").Sum(x=>x.Get("rate",0).Double),pollution=own.Where(x=>x.Get("aggregate","").Text=="pollution_fee").Sum(x=>x.Get("rate",0).Double),expense=expenses.Where(x=>x.Get("source_id","").Text==id).Sum(x=>x.Get("rate",0).Double);
    C.TableRow(section,new[]{OperatingProjectIdentity(id),Signed(tax),Signed(land),Signed(pollution),F(expense),Signed(tax+land+pollution-expense)});CivicObjectActions(section,id);
   }
   C.Pair(section,"市政及未归属经营收入",Signed(m.Get("income",0).Double-receipts.Sum(x=>x.Get("rate",0).Double))+" /月");
   C.Fold(section,"主动操作与市场通货流水",p=>{C.TableRow(p,new[]{"事项","来生月","实际收付","余额"},true);foreach(var t in S.Get("civic032",V.Map()).Get("transactions",V.Arr()).Reverse())C.TableRow(p,new[]{t.Get("label","").Text,F(t.Get("h",0).Double),Signed(t.Get("cash",0).Double),F(t.Get("balance",0).Double)});});
   C.Fold(section,"通货历史 · 实际记录",p=>{var history=S.Get("civic032",V.Map()).Get("history",V.Arr());C.Table(p,new[]{"来生月","通货余额"},history.Select(x=>new[]{F(x.Get("h",0).Double),F(x.Get("balance",0).Double)}));});
   CivicEvidence(section,"财政总账凭据",m.Get("fiscal_ledger",V.Arr()));CivicEvidence(section,"财政归属凭据",receipts);return;
  }
  var audit=S.Get("audit",V.Map());var receipt=audit.Get("resources",V.Map()).Get(resource,V.Map());var elapsed=audit.Get("hours",0).Double;
  if(OperatingSettled&&elapsed>0&&receipt.Count>0){var balance=C.Section(section,"最近周期回执","同一周期换算 / 来生月");foreach(var key in new[]{"produced","imported","consumed","exported","commission_delivered","lost"})C.Pair(balance,CivicEvidenceName(key),F(receipt.Get(key,0).Double/elapsed,3)+" /月");C.Pair(balance,"实际净变",Signed((receipt.Get("after",0).Double-receipt.Get("before",0).Double)/elapsed)+" /月");CivicEvidence(balance,"周期原始数量凭据",receipt);}
  C.TableRow(section,new[]{"建筑 / 地块","保留产出","生产投入","住户消费","施工领料","净额","库存 / 容量"},true);
  foreach(var id in S["projects"].Keys) {
   var inventory=S["projects"][id].Get("inventory",V.Map()).Get(resource,0).Double;var capacity=G.stock_cap_for(id,resource);var entries=m.Get("activity_ledger",V.Map()).Get(id,V.Arr()).Where(x=>x.Get("resource","").Text==resource).ToArray();if(inventory<=0&&capacity<=0&&entries.Length==0)continue;
   var output=entries.Where(x=>x.Get("kind","").Text=="output").Sum(x=>x.Get("kept",x.Get("rate",0)).Double);var resident=entries.Where(x=>x.Get("kind","").Text=="input"&&x.Get("category","").Text=="resident").Sum(x=>x.Get("rate",0).Double);var construction=entries.Where(x=>x.Get("kind","").Text=="input"&&x.Get("category","").Text=="construction").Sum(x=>x.Get("rate",0).Double);var input=entries.Where(x=>x.Get("kind","").Text=="input").Sum(x=>x.Get("rate",0).Double)-resident-construction;
   C.TableRow(section,new[]{OperatingProjectIdentity(id),OperatingSettled?F(output):"待结算",OperatingSettled?F(input):"待结算",OperatingSettled?F(resident):"待结算",OperatingSettled?F(construction):"待结算",OperatingSettled?Signed(output-input-resident-construction):"待结算",F(inventory)+" / "+F(capacity)});CivicObjectActions(section,id);
  }
  C.Label(section,"净额为本地活动月流；贸易、交付与损耗见全城周期回执。","c-small c-muted");CivicEvidence(section,"完整投入、产出及活动凭据",m.Get("activity_ledger",V.Map()));
 }

 void CivicWildTrade(VisualElement body) {
  if(!WildDesign.Enabled(G))return;var tech=W.Get("wild_technology",V.Map());var exported=tech.Get("material_exported",0).Double;var purchased=tech.Get("housing_levels",0).Long>0;var eligible=exported+1e-6>=CitySession.WildHousingRequiredExports;
  var section=C.Section(body,"希望城 · 技术贸易","住房设计 I");C.Label(section,"作坊升级货运站 → 向希望城出口材料 → 购买科技","c-small c-muted");C.Meter(section,"累计实际出口",exported,CitySession.WildHousingRequiredExports,"材料","挂单和撤单不计入");
  CivicTabs(section,new[]{"hope",""},id=>id=="hope"?"希望城":"普通市场",_wildCounterparty,id=>{_wildCounterparty=id;RenderPage();});
  C.Button(section,purchased?"住房设计 I · 已掌握":"购买住房设计 I · 60 通货",()=>Confirm("购买住房设计 I？","支付 60 通货，余额将为 "+F(S["currency"].Double-60)+"。每栋现有及未来建筑增加 2 设计点；已出口材料不会再次扣除。",()=>{var error=Runtime.ExecuteWorld(city=>city.BuyWildHousing());if(error!="")ShowMessage(error);else ShowMessage("住房设计 I 已掌握；全城建筑设计容量 +2。");RenderPage();}),true,eligible&&!purchased&&S["currency"].Double>=60&&Runtime.ViewCanEditFocused);
  C.Label(section,purchased?"每栋现有及未来建筑设计容量 +2":eligible?"出口条件已满足，购买后每栋设计容量 +2":"还需实际出口 "+F(Math.Max(0,CitySession.WildHousingRequiredExports-exported))+" 材料","c-small c-muted");C.Pair(section,"低级工业品开局储备",F(S["wild"]["stock"]["low_industrial"].Double,0)+" 件 · 农居升阶使用");
 }
 void CivicMarket(VisualElement body,VisualElement actions) {
  CivicWildTrade(body);if(new[]{"food","material","goods"}.Contains(_pageContext))_tradeResource=_pageContext;
  if(_advancedWorkspace){var quotes=C.Section(body,"共享市场报价","当前报价，不代表成交价");C.Table(quotes,new[]{"资源","单位买价","单位卖价","当前库存"},new[]{"food","material","goods"}.Select(r=>{var q=Market.quote(G,r);return new[]{Resource(r),F(q.Get("buy_price",0).Double,3),F(q.Get("sell_price",0).Double,3),F(G.inventory_total(r))};}));foreach(var r in new[]{"food","material","goods"})CivicEvidence(quotes,Resource(r)+"报价完整来源",Market.quote(G,r));}
  var editor=C.Section(body,"限价订单","真实在线货运");CivicTabs(editor,new[]{"food","material","goods"},Resource,_tradeResource,id=>{_tradeResource=id;_tradeLimit="";_pageContext="";RenderPage();});CivicTabs(editor,new[]{"buy","sell"},id=>id=="buy"?"买入":"卖出",_tradeSide,id=>{_tradeSide=id;_tradeLimit="";RenderPage();});
  var quote=Market.quote(G,_tradeResource);double price=quote.Get(_tradeSide+"_price",0).Double;if(_tradeLimit=="")_tradeLimit=price.ToString("F3",CultureInfo.InvariantCulture);
  var metrics=C.Row(editor);C.Value(metrics,"当前单位报价",F(price,3),"通货 / 单位");C.Value(metrics,"可用通货",F(S.Get("currency",0).Double));C.Value(metrics,"当前库存 / 空仓",F(G.inventory_total(_tradeResource))+" / "+F(Market._buy_room(G,_tradeResource)));
  var status=C.Label(editor,"","c-small");var estimate=C.Label(editor,"");UnityEngine.UIElements.Button submit=null;
  Action refresh=()=>{double amount,limit;var validAmount=double.TryParse(_tradeAmount,NumberStyles.Float,CultureInfo.InvariantCulture,out amount)&&!double.IsNaN(amount)&&!double.IsInfinity(amount)&&amount>0;var validLimit=double.TryParse(_tradeLimit,NumberStyles.Float,CultureInfo.InvariantCulture,out limit)&&!double.IsNaN(limit)&&!double.IsInfinity(limit);var freight=Market._total_capacity(G)>.000001;
   status.text=!validAmount?"请输入大于零的有效数量。":!validLimit?"请输入有效限价，可为负数。":!freight?"需要正在运行的货运站。":_tradeSide=="buy"?"买价不高于限价时成交；受通货、仓容和共享运力限制。":"卖价不低于限价时成交；受库存、通货和共享运力限制。";estimate.text=validAmount?"按当前报价估计通货变化 "+Signed((_tradeSide=="buy"?-1:1)*amount*price):"输入数量以查看报价估计";if(submit!=null)submit.SetEnabled(validAmount&&validLimit&&freight);
  };
  var inputs=C.Row(editor);C.Field(inputs,"数量",_tradeAmount,value=>{_tradeAmount=value;refresh();});C.Field(inputs,"每单位限价",_tradeLimit,value=>{_tradeLimit=value;refresh();});
  submit=C.Button(editor,"创建"+(_tradeSide=="buy"?"买入":"卖出")+Resource(_tradeResource)+"限价订单",()=>{if(!double.TryParse(_tradeAmount,NumberStyles.Float,CultureInfo.InvariantCulture,out var amount)||!double.TryParse(_tradeLimit,NumberStyles.Float,CultureInfo.InvariantCulture,out var limit))return;Command(g=>Market.order(g,_tradeResource,_tradeSide,amount,limit,TradeCounterparty));},true);refresh();
  C.Label(editor,"下单不预留库存或通货，实际成交才收付。负报价可改变支付方向；本地加速不扩大在线运力。","c-small c-muted");
  var freightSection=C.Section(body,"货运能力",F(Market._total_capacity(G))+" 单位 / 现实在线小时");var ledger=S["block"].Get("trade_ledger",V.Arr());if(ledger.Count>0)C.Meter(freightSection,"最近实际周期使用",ledger[-1].Get("used_capacity",0).Double,ledger[-1].Get("capacity",0).Double,"单位","该周期数量，不与每小时能力混用");else C.Empty(freightSection,"尚无实际货运周期");
  if(_advancedWorkspace)CivicMarketHistory(body);else {
   var orders=S["block"].Get("trade_orders",V.Arr()).Where(x=>x.Get("status","").Text=="open"||x.Get("status","").Text=="partial").ToArray();var pending=C.Section(body,"待成交订单",orders.Length+" 笔");foreach(var order in orders.Take(5)){var id=order["id"].Text;var line=C.Section(pending,(order["side"].Text=="buy"?"买入 ":"卖出 ")+Resource(order["resource"].Text),id);C.Meter(line,"成交进度",order["amount"].Double-order["remaining"].Double,order["amount"].Double,"单位","限价 "+F(order["limit_price"].Double,3));C.Button(line,"撤单",()=>Command(g=>Market.cancel(g,id)));}if(orders.Length==0)C.Empty(pending,"暂无待成交订单");
  }
  CivicAdvancedLink(body,"market",_tradeResource);C.Button(actions,"资源与财政账目",()=>OpenAdvancedWorkspace("resources",_tradeResource));if(_advancedWorkspace)CivicEvidence(freightSection,"全部运力结算与来源",ledger);
 }
 void CivicPager(VisualElement parent,int count,int page,int pages,Action<int> change) {var row=C.Row(parent);C.Button(row,"上一页",()=>change(-1),false,page>0);C.Label(row,count+" 条 · "+(page+1)+" / "+pages,"c-muted");C.Button(row,"下一页",()=>change(1),false,page+1<pages);}
 void CivicMarketHistory(VisualElement body) {
  var orders=C.Section(body,"订单管理");CivicTabs(orders,new[]{"all","open","filled","cancelled"},id=>V.Map("all","全部","open","待成交 / 部分","filled","已完成","cancelled","已取消")[id].Text,_operatingOrderFilter,id=>{_operatingOrderFilter=id;_operatingOrderPage=0;RenderPage();});
  var all=S["block"].Get("trade_orders",V.Arr()).Reverse().Where(x=>_operatingOrderFilter=="all"||(_operatingOrderFilter=="open"?(x.Get("status","").Text=="open"||x.Get("status","").Text=="partial"):x.Get("status","").Text==_operatingOrderFilter)).ToArray();var pages=Math.Max(1,(all.Length+OperatingHistoryPageSize-1)/OperatingHistoryPageSize);_operatingOrderPage=Math.Max(0,Math.Min(_operatingOrderPage,pages-1));
  C.TableRow(orders,new[]{"订单 / 方向 / 资源","原始数量","已成交 / 剩余","单位限价","限价估值","状态"},true);
  foreach(var order in all.Skip(_operatingOrderPage*OperatingHistoryPageSize).Take(OperatingHistoryPageSize)){var id=order["id"].Text;var status=order.Get("status","").Text;var r=C.TableRow(orders,new[]{id+" · "+(order["side"].Text=="buy"?"买入 ":"卖出 ")+Resource(order["resource"].Text),F(order["amount"].Double),F(order["amount"].Double-order["remaining"].Double)+" / "+F(order["remaining"].Double),F(order["limit_price"].Double,3),F(order["amount"].Double*order["limit_price"].Double,3),CivicEvidenceValue("status",status)});if(status=="open"||status=="partial")C.Button(r,"撤单",()=>Command(g=>Market.cancel(g,id)));CivicEvidence(orders,"订单完整凭据 · "+id,order);}
  if(all.Length==0)C.Empty(orders,"此筛选下暂无订单");CivicPager(orders,all.Length,_operatingOrderPage,pages,delta=>{_operatingOrderPage+=delta;RenderPage();});C.Label(orders,"限价估值是原单数量 × 限价，实际收付见成交记录。","c-small c-muted");
  var section=C.Section(body,"真实成交历史");var trades=S["block"].Get("trade_history",V.Arr()).Reverse().ToArray();var historyPages=Math.Max(1,(trades.Length+OperatingHistoryPageSize-1)/OperatingHistoryPageSize);_operatingTradeHistoryPage=Math.Max(0,Math.Min(_operatingTradeHistoryPage,historyPages-1));C.TableRow(section,new[]{"方向 / 资源","数量","成交单价","实际通货收付","订单"},true);
  foreach(var trade in trades.Skip(_operatingTradeHistoryPage*OperatingHistoryPageSize).Take(OperatingHistoryPageSize)){C.TableRow(section,new[]{(trade["side"].Text=="buy"?"买入 ":"卖出 ")+Resource(trade["resource"].Text),F(trade["amount"].Double,3),F(trade["unit_price"].Double,3),Signed(trade["currency_delta"].Double),trade.Get("order_id",trade.Get("order","—")).Text});CivicEvidence(section,"此笔成交与运力来源",trade);}
  if(trades.Length==0)C.Empty(section,"尚无实际成交");CivicPager(section,trades.Length,_operatingTradeHistoryPage,historyPages,delta=>{_operatingTradeHistoryPage+=delta;RenderPage();});
 }

 void CivicPolicies(VisualElement body,VisualElement actions) {
  var selection=C.Section(body,"经营政策","通货 "+F(S.Get("currency",0).Double));CivicTabs(selection,new[]{"Tax","Labor","Environment","Procurement","Land"},OperatingPolicySlotName,_policySlot,key=>{_policySlot=key;_policyChoice="";if(_comparisonValid)ClearComparison();RenderPage();});
  var currentId=S["block"]["policies"].Get(_policySlot,"").Text;var current=G.c.policies.Get(currentId,V.Map());var ids=G.c.policies.Keys.Where(id=>G.c.policies[id]["slot"].Text==_policySlot).ToArray();if(!ids.Contains(_policyChoice))_policyChoice=ids.FirstOrDefault(id=>id!=currentId)??ids.FirstOrDefault()??"";
  C.Label(selection,"当前 · "+current.Get("name","本类别未采用政策").Text,"c-heading c-good");C.Label(selection,current.Get("summary","").Text,"c-muted");
  foreach(var idValue in ids){var id=idValue;C.Choice(selection,G.c.policies[id]["name"].Text+(id==currentId?" · 当前":""),G.c.policies[id].Get("summary","").Text,_policyChoice==id,()=>{_policyChoice=id;if(_comparisonValid)ClearComparison();RenderPage();});}
  if(_policyChoice==""){C.Empty(body,"本类别暂无政策");return;}
  var selected=_policyChoice;var policy=G.c.policies[selected];var baseline=_planner.snapshot(G);var preview=_planner.policy(G,selected);var version=_planner.version(G);var price=G.c.balance["policy_switch_price"].Double;var isCurrent=currentId==selected;
  var comparison=C.Section(body,"候选 · "+policy["name"].Text,"同一步下一结算");C.Label(comparison,policy.Get("summary","").Text,"c-muted");
  if(preview.Get("ok",false).Bool){var rows=OperatingPolicyMetricRows(baseline,preview["map_snapshot"]).ToArray();var changed=rows.Where(x=>Math.Abs(x["after"].Double-x["before"].Double)>.000001).ToArray();C.Table(comparison,new[]{"指标","不更换","采用后","变化"},(_advancedWorkspace?rows:changed).Select(x=>new[]{x["title"].Text,F(x["before"].Double),F(x["after"].Double),Signed(x["after"].Double-x["before"].Double)}));
   if(!_advancedWorkspace&&changed.Length==0)C.Label(comparison,"当前城市下一步结算无数值变化，规则和实施费用仍有效。","c-muted");
   if(!_advancedWorkspace&&changed.Length<rows.Length)C.Fold(comparison,"未变化指标",p=>C.Table(p,new[]{"指标","不更换","采用后"},rows.Except(changed).Select(x=>new[]{x["title"].Text,F(x["before"].Double),F(x["after"].Double)})));
   var map=C.Row(comparison);C.Button(map,"地图 · 不更换预测",()=>{_baseline=baseline;_proposal=V.Map();ShowForecastMap("baseline",baseline);});C.Button(map,"地图 · 采用后预测",()=>{_baseline=baseline;_proposal=preview;PublishComparison();ShowForecastMap("proposal",preview["map_snapshot"]);});
  }else C.Label(comparison,preview.Get("error","预测不可用").Text,"c-warning");
  C.Label(comparison,"两列为同一步真实结算预测。实施费应用时一次支付，不加入经营月率。","c-small c-muted");C.Pair(comparison,isCurrent?"当前政策已采用":"更换即付",isCurrent?"无更换费用":F(price)+" 通货 · 支付后 "+F(S.Get("currency",0).Double-price));
  C.Button(actions,isCurrent?"已采用":"实施「"+policy["name"].Text+"」",()=>{if(version!=_planner.version(G)){RenderPage();ShowMessage("城市已变化，请核对刷新后的预测。");return;}Command(g=>g.switch_policy(selected));},true,!isCurrent&&preview.Get("ok",false).Bool&&S.Get("currency",0).Double>=price);
  CivicEvidence(comparison,"候选完整规则",policy);if(current.Count>0)CivicEvidence(selection,"当前完整规则",current);CivicAdvancedLink(body,"policies");
 }
 void CivicConstruction(VisualElement body,VisualElement actions) {
  var queue=Construction.queue(G);var capacity=Construction.capacity_breakdown(G);var crews=Construction.crews(G);var quote=Construction.crew_quote(G);var material=G.inventory_total("material");var last=S["block"].Get("construction_last",V.Map());var elapsed=last.Get("hours",0).Double;CivicPending(body);
  var overview=C.Section(body,"施工能力",queue.Count(x=>!x["paused"].Bool)+" 项启用");var values=C.Row(overview);C.Value(values,"本区有效施工",F(capacity["local_effective"].Double),"CP / 来生月");C.Value(values,"城市工程预留",F(capacity["city_effective"].Double),"CP / 来生月");
  CivicKnownMeter(overview,"最近周期 · 本区能力使用",elapsed>0?last.Get("cp",0).Double/elapsed:0,elapsed>0?(last.Get("base_cp",0).Double-last.Get("city_reserved_cp",0).Double+last.Get("crew_cp",0).Double)/elapsed:0,"CP/月","使用量取同一实际周期",elapsed>0);
  var composition=C.Row(overview,"c-wrap");var nominal=new[]{capacity["base"].Double,capacity["bonus"].Double,capacity["buildings"].Double,capacity["crews_nominal"].Double};var total=nominal.Sum(v=>Math.Max(0,v));var names=new[]{"基础","方案","建筑","临时队"};for(int i=0;i<nominal.Length;i++)C.Meter(C.Column(composition,"c-half"),names[i]+"名义能力",nominal[i],total,"CP/月");
  C.Label(overview,"常设来源乘方案效率 ×"+F(capacity["multiplier"].Double,2)+"，再与临时队合并，受供电与城市工程预留影响。","c-small c-muted");
  CivicKnownMeter(overview,"供电满足",capacity["energy_ratio"].Double*100,100,"%","",OperatingSettled);var needed=queue.Sum(x=>x["material"].Double);if(needed>0)C.Meter(overview,"队列材料备齐",Math.Min(material,needed),needed,"材料","库存 "+F(material)+" · 全城工程共享");else C.Pair(overview,"材料库存",F(material)+" · 暂无施工需求");
  var projects=C.Section(body,"施工队列",queue.Count+" 项");CivicTabs(projects,new[]{"all","active","paused"},id=>id=="all"?"全部":id=="active"?"进行中":"已暂停",_operatingConstructionFilter,id=>{_operatingConstructionFilter=id;RenderPage();});var shown=queue.Where(x=>_operatingConstructionFilter=="active"?!x["paused"].Bool:_operatingConstructionFilter=="paused"?x["paused"].Bool:true).ToArray();
  foreach(var job in (_advancedWorkspace?shown:shown.Take(5))) {
   var id=job["id"].Text;var paused=job["paused"].Bool;var line=C.Section(projects,ConstructionProjectIdentity(job),paused?"已暂停":capacity["energy_ratio"].Double<=0?"等待电力":capacity["local_effective"].Double<=0?"等待施工能力":job["material"].Double>0&&material<=0?"等待材料":"共享能力与材料");C.Label(line,ConstructionProjectDirection(job),"c-small c-muted");C.Meter(line,"完工进度",job["total"].Double-job["work"].Double,job["total"].Double,"CP","还需 "+F(job["work"].Double)+" CP / "+F(job["material"].Double)+" 材料");
   var controls=C.Row(line,"c-wrap");foreach(var priorityValue in new[]{1,0,-1}){var priority=priorityValue;var b=C.Button(controls,priority>0?"优先":priority<0?"延后":"普通",()=>Command(g=>Construction.set_priority(g,id,priority)));b.EnableInClassList("is-selected",job["priority"].Long==priority);}C.Button(controls,paused?"继续施工":"暂停施工",()=>Command(g=>g.set_paused(id,!paused)));CivicObjectActions(line,id);if(_advancedWorkspace)CivicEvidence(line,"工程完整凭据",job);
  }
  if(shown.Length==0)C.Empty(projects,queue.Count==0?"暂无施工项目":"此筛选下暂无工程","在地图上选择地块新建或选择建筑升级。");if(!_advancedWorkspace&&shown.Length>5)C.Label(projects,"另有 "+(shown.Length-5)+" 项工程，详表可管理全部。","c-small c-muted");
  var staff=C.Section(body,"临时施工队",crews.Count+" 队");foreach(var crew in crews)C.Meter(staff,crew["id"].Text+" · +"+F(crew["nominal_cp_per_month"].Double)+" CP/月",crew["remaining_months"].Double,quote["duration_months"].Double,"月","剩余 "+CrewRemaining(crew));if(crews.Count==0)C.Empty(staff,"暂无临时施工队");
  C.Label(staff,"一次付费，不退费、不自动续费；空队列仍计时，暂停和离线不计时。临时队不乘方案效率，受供电影响，仅用于本区。","c-small c-muted");
  C.Button(actions,"招募施工队 · "+F(quote["cost"].Double,0)+" 通货",ConfirmHireConstructionCrew,true,quote["affordable"].Bool);C.Button(actions,"材料收支",()=>OpenAdvancedWorkspace("resources","material"));CivicAdvancedLink(body,"construction");if(_advancedWorkspace){CivicEvidence(overview,"施工能力完整来源",capacity);CivicEvidence(overview,"最近实际施工周期",last);CivicEvidence(staff,"全部施工队凭据",crews);CivicEvidence(staff,"当前招募报价",quote);}
 }

 // Full records remain inspectable as labelled native rows, without a JSON wall.
 void CivicEvidence(VisualElement parent,string title,V value)=>C.Fold(parent,title,p=>CivicEvidenceRows(p,value));
 void CivicEvidenceRows(VisualElement parent,V value) {
  if(value.IsMap){if(value.Count==0){C.Label(parent,"暂无记录","c-small c-muted");return;}foreach(var pair in value.Pairs){var key=pair.Key;var item=pair.Value;if(item.IsMap||item.IsArray)CivicEvidence(parent,CivicEvidenceName(key)+" · "+item.Count+" 项",item);else C.Pair(parent,CivicEvidenceName(key),CivicEvidenceValue(key,item));}}
  else if(value.IsArray){if(value.Count==0)C.Label(parent,"暂无记录","c-small c-muted");for(int i=0;i<value.Count;i++){var item=value[i];if(item.IsMap||item.IsArray)CivicEvidence(parent,"记录 "+(i+1),item);else C.Pair(parent,"记录 "+(i+1),CivicEvidenceValue("",item));}}
  else C.Label(parent,CivicEvidenceValue("",value),"c-small");
 }
 static readonly V CivicEvidenceValues=V.Map("buy","买入","sell","卖出","open","待成交","partial","部分成交","filled","已完成","cancelled","已取消","income","收入","expense","支出","input","投入","output","产出","resident","住户","construction","施工","hope","希望城","municipal","市政","tax","税收","land_rent","用地收入","pollution_fee","排污收入","medical","医疗","education","教育","entertainment","娱乐","commercial","商业");
 string CivicEvidenceValue(string key,V value) {
  if(value.IsNull)return "未记录";if(value.IsBool)return value.Bool?"是":"否";if(value.IsNumber)return F(value.Double,4);var text=value.Text;
  if(key=="resource")return Resource(text);if(key=="slot"&&new[]{"Tax","Labor","Environment","Procurement","Land"}.Contains(text))return OperatingPolicySlotName(text);
  if(new[]{"source_id","target","home","host"}.Contains(key)&&S["projects"].Has(text))return OperatingProjectIdentity(text);
  return CivicEvidenceValues.Get(text,text).Text;
 }
 static readonly V CivicEvidenceNames=V.Map("id","编号","name","名称","label","事项","kind","类型","category","用途","resource","资源","source","来源","source_id","来源建筑","target","目标建筑","group","住户组","group_id","住户组","home","所属住宅","host","住宅","pool","服务池","members","提供者成员","capacity","容量","remaining","剩余","people","人数","count","人数","rate","月率","kept","保留产出","amount","原始数量","unit_price","成交单价","limit_price","限价","currency_delta","实际通货收付","status","状态","side","方向","order","订单","order_id","订单","h","来生月","hours","结算周期（月）","online_hours","现实在线小时","used_capacity","周期使用运力","records","成交记录","sources","来源明细","operator","经营者","provider","提供者","counterparty","贸易对象","cash","通货收付","balance","通货余额","basis","税基","aggregate","收入类别","produced","本地产出","imported","实际进口","consumed","消费与施工","exported","实际外运","commission_delivered","委任交付","lost","损耗","before","周期开始","after","周期结束","employment","就业分配","living","生活分配","health","健康","happiness","幸福","education","教育","food","口粮","medical","医疗","commercial","商业","entertainment","娱乐","unemployment","失业比例","pollution","污染暴露","energy","电力","material","材料","goods","用品","currency","通货","slot","政策类别","summary","说明","effects","效果","options","规则选项","base","基础能力","bonus","方案能力","buildings","建筑能力","crews_nominal","临时队名义能力","base_total","常设施工能力","multiplier","常设效率","energy_ratio","供电满足率","local_effective","本区有效能力","city_effective","城市工程预留","remaining_months","剩余来生月","nominal_cp_per_month","名义 CP/月","duration_months","持续来生月","cost","费用","affordable","可支付","work","剩余施工 CP","total","总施工 CP","paused","已暂停","priority","优先级","upgrade","升级工程","cp","实际施工 CP","base_cp","常设施工 CP","city_reserved_cp","城市工程预留 CP","crew_cp","临时队施工 CP","tick","结算编号","economic_months","经济周期（来生月）","external_seconds","现实在线秒数","capacity_sources","运力来源","trades","实际成交","bought","已买入","sold","已卖出","filled","已成交数量","created_at","创建来生月","updated_at","更新来生月","last_attempt_tick","上次尝试结算","price","单位报价","mid","市场中价","mid_price","成交时中价","buy_price","单位买价","sell_price","单位卖价","path","作用字段","op","计算方式","value","数值");
 string CivicEvidenceName(string key) {
  if(S["projects"].Has(key))return OperatingProjectIdentity(key);
  return CivicEvidenceNames.Get(key,"记录字段 · "+key).Text;
 }
}
}
