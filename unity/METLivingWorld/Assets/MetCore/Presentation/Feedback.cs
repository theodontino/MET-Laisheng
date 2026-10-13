using System;
using System.Globalization;
using System.Runtime.CompilerServices;
namespace MET.Core {
/// <summary>Actual committed city feedback with hysteresis, merged causes and bounded city voices.</summary>
public sealed class Feedback {
 public const double ENTER=.95,EXIT=.98,LOCAL_HOLD=.02,BUBBLE_TTL=8.0,BUBBLE_GAP=18.0;public const int HISTORY_LIMIT=24;
 public static readonly V RESOURCE_NAMES=V.Map("food","粮食","material","材料","goods","制成品","energy","电力");
 string _identity="",_capstone="";long _tick=-1;int _revision=-1,_commercial_year=-1;double _local=0,_next_bubble=0,_commercial_served=0;bool _initialized=false;
 V _watches=V.Map(),_groups=V.Map(),_medical_changes=V.Map(),_projects=V.Map(),_heard=V.Map(),_bubbles=V.Arr(),_history=V.Arr(),_alerts=V.Arr();
 static string F(double n,int digits)=>n.ToString("F"+digits,CultureInfo.InvariantCulture);
 public void reset(){_tick=-1;_revision=-1;_local=0;_watches.Clear();_groups.Clear();_medical_changes.Clear();_projects.Clear();_heard.Clear();_bubbles.Clear();_history.Clear();_alerts.Clear();_next_bubble=0;_capstone="";_initialized=false;_commercial_year=-1;_commercial_served=0.0;}
 public V refresh(Game g,double real_seconds,V snapshot=null) {
  snapshot=snapshot??V.Map();var identity=RuntimeHelpers.GetHashCode(g)+":"+g.s.Get("seed",0).Text+":"+g.s.Get("started_at",0).Text;if(identity!=_identity){reset();_identity=identity;}
  if(snapshot.Get("forecast",false).Bool)return view(real_seconds);var tick=g.s.Get("simulation_tick",0).Long;var local=g.s.Get("local_hours",0).Double;if(tick<_tick||local<_local)reset();
  if(tick!=_tick||g.revision!=_revision){var delta=_initialized?Math.Max(0,local-_local):0.0;_update_alerts(g,delta);_update_voices(g,real_seconds);_tick=tick;_revision=g.revision;_local=local;_initialized=true;}_update_journal(g);_update_capstone(g);return view(real_seconds);
 }
 public V view(double real_seconds) {
  var remaining=V.Arr();foreach(var row in _bubbles)if(row["expires_at"].Double>real_seconds)remaining.Add(row);_bubbles=remaining;var causes=V.Map();
  foreach(var row in _alerts){if(!causes.Has(row["cause"])){causes[row["cause"]]=row.Duplicate(true);causes[row["cause"]]["count"]=0;}causes[row["cause"]]["count"]+=1;}
  var summaries=V.From(causes.Values);summaries.Items.Sort(ComparePriority);foreach(var row in summaries)if(row["count"].Long>1)row["text"]=row["text"].Text+" · "+row["count"].Long+"处受限";
  return V.Map("alerts",_alerts.Duplicate(true),"summaries",summaries,"main",summaries.Count!=0?summaries[0]:V.Map(),"bubbles",_bubbles.Duplicate(true),"chirps",_history.Duplicate(true));
 }
 public V history()=>_history.Duplicate(true);
 public static bool _priority(V a,V b){if(a["severity"]!=b["severity"])return a["severity"].Long>b["severity"].Long;if(a["ratio"]!=b["ratio"])return a["ratio"].Double<b["ratio"].Double;return StringComparer.Ordinal.Compare(a["id"].Text,b["id"].Text)<0;}
 static int ComparePriority(V a,V b)=>_priority(a,b)?-1:_priority(b,a)?1:0;
 public static V source(Game g,string id,string layer,string kind="building"){var p=g.s.Get("projects",V.Map()).Get(id,V.Map());return V.Map("source_id",id,"building_id",id,"tile",p.Get("tile","").Text,"layer",layer,"kind",kind);}
 public V _candidate(Game g,string id,string cause,string text,double ratio,int severity,string layer,V evidence){var row=source(g,id,layer);row.Merge(V.Map("id",id+":"+cause,"cause",cause,"text",text,"ratio",ratio,"severity",severity,"evidence",evidence),true);return row;}
 public void _update_alerts(Game g,double delta) {
  var metrics=g.s.Get("metrics",V.Map());if(!metrics.Has("activity_ledger"))return;var candidates=V.Arr();
  foreach(var id in g.s["projects"].Keys){var p=g.s["projects"][id];if(p.Get("status","").Text!="running"||p.Get("paused",false).Bool||p.Get("special",false).Bool)continue;var a=metrics.Get("utilization",V.Map()).Get(id,V.Map());if(a.Count==0)continue;var utilization=a.Get("utilization",1).Double;
   foreach(var cause in new[]{"energy","resources","labor"}){var ratio=a.Get(cause,1).Double;if(ratio>=EXIT||utilization>=EXIT)continue;var label=cause=="energy"?"电力不足":cause=="resources"?"投入物资不足":"缺少有效人手";var layer=cause=="energy"?"energy":cause=="resources"?"material":"employment";
    if(cause=="resources"){var st=g.compiled.Get("stats",V.Map()).Get(id,V.Map());var inputs=V.Arr();foreach(var resource in new[]{"food","material","goods"})if(st.Get("input."+resource,0).Double>0)inputs.Add(resource);if(inputs.Count==1){layer=inputs[0].Text;label="缺少"+RESOURCE_NAMES[layer].Text;}}
    var evidence=V.Map("tick",g.s["simulation_tick"],"satisfaction",ratio,"utilization",utilization,"bottleneck",a.Get("bottleneck","").Text);candidates.Add(_candidate(g,id,cause,label,ratio,utilization<.05&&ratio<.05?2:1,layer,evidence));}
   var lost=0.0;foreach(var item in metrics.Get("activity_ledger",V.Map()).Get(id,V.Arr()))lost+=item.Get("lost",0).Double;if(lost>.000001)candidates.Add(_candidate(g,id,"storage","仓储不足 · 已有溢出",0,1,"goods",V.Map("tick",g.s["simulation_tick"],"actual_lost",lost)));
  }
  foreach(var gid in g.s.Get("groups",V.Map()).Keys){var group=g.s["groups"][gid];var id=group.Get("host","").Text;if(!g.s["projects"].Has(id)||g.s["projects"][id].Get("special",false).Bool)continue;var food=group.Get("living",V.Map()).Get("food",1).Double;
   if(food<EXIT&&group.Get("count",0).Double>0){var row=_candidate(g,id,"food","居民口粮不足",food,food<.5?2:1,"food",V.Map("tick",g.s["simulation_tick"],"group_id",gid,"actual_food_satisfaction",food));row["group_id"]=gid;candidates.Add(row);}}
  foreach(var row in _import_alerts(g))candidates.Add(row);var seen=V.Map();var active_by_building=V.Map();
  foreach(var row in candidates){if(seen.Has(row["id"]))continue;seen[row["id"]]=true;var watch=_watches.Get(row["id"],V.Map("held",0.0,"active",false));if(row["ratio"].Double<ENTER)watch["held"]=watch["held"].Double+delta;else if(!watch["active"].Bool)watch["held"]=0.0;
   if(row["severity"].Long==2||watch["held"].Double>=LOCAL_HOLD)watch["active"]=true;_watches[row["id"]]=watch;if(!watch["active"].Bool)continue;if(!active_by_building.Has(row["building_id"]))active_by_building[row["building_id"]]=V.Arr();active_by_building[row["building_id"]].Add(row);}
  foreach(var key in _watches.Keys)if(!seen.Has(key))_watches.Erase(key);_alerts=V.Arr();foreach(var id in active_by_building.Keys){var rows=active_by_building[id];rows.Items.Sort(ComparePriority);_alerts.Add(rows[0]);if(rows.Count>1&&rows[1]["severity"].Long==2)_alerts.Add(rows[1]);}_alerts.Items.Sort(ComparePriority);
 }
 public V _import_alerts(Game g) {
  var result=V.Arr();var elapsed=g.s.Get("audit",V.Map()).Get("hours",0).Double;if(elapsed<=0)return result;var freight="";foreach(var id in g.s["projects"].Keys)if(g.compiled.Get("stats",V.Map()).Get(id,V.Map()).Get("trade_capacity",0).Double>0){freight=id;break;}if(freight=="")return result;
  foreach(var resource in new[]{"food","material","goods"}){var pending=V.Map();foreach(var order in g.s.Get("block",V.Map()).Get("trade_orders",V.Arr()))if(order.Get("resource","").Text==resource&&order.Get("side","").Text=="buy"&&V.Arr("open","partial").Contains(order.Get("status",""))&&order.Get("remaining",0).Double>0){pending=order;break;}if(pending.Count==0)continue;
   var receipt=g.s.Get("audit",V.Map()).Get("resources",V.Map()).Get(resource,V.Map());var produced=receipt.Get("produced",0).Double/elapsed;var requested=Math.Max(receipt.Get("consumed",0).Double/elapsed,g.s["metrics"].Get("requested",V.Map()).Get(resource,0).Double);var dependence=Math.Max(0,requested-produced);var imports=receipt.Get("imported",0).Double/elapsed;var deficit=dependence-imports;if(dependence<=0||deficit<=0)continue;
   var stock=0.0;foreach(var p in g.s["projects"].Values)stock+=p.Get("inventory",V.Map()).Get(resource,0).Double;var months=stock/deficit;if(months>=1)continue;
   var row=_candidate(g,freight,"import_"+resource,RESOURCE_NAMES[resource].Text+"进口跟不上当前消耗",imports/dependence,requested>receipt.Get("consumed",0).Double/elapsed+.000001&&stock<=.000001?2:1,resource,V.Map("tick",g.s["simulation_tick"],"order_id",pending.Get("id",""),"stock",stock,"local_months_remaining",months,"required_imports_per_month",dependence,"actual_imports_per_month",imports,"limit_price",pending.Get("limit_price",0)));row["kind"]="trade";result.Add(row);
  }return result;
 }
 public void _say(Game g,string gid,string text,string reason,V evidence,double now) {
  if(now<_next_bubble||_bubbles.Count>=2)return;var group=g.s["groups"].Get(gid,V.Map());var host=group.Get("host","").Text;if(!g.s["projects"].Has(host)||g.s["projects"][host].Get("special",false).Bool)return;
  var layers=V.Map("medical","medical","employment","employment","shopping","commercial","pollution","pollution","housing","population");var row=source(g,host,layers.Get(reason,"population").Text,"population");row.Merge(V.Map("id",gid+":"+reason,"group_id",gid,"text",text,"reason",reason,"evidence",evidence.Duplicate(true),"expires_at",now+BUBBLE_TTL),true);_bubbles.Add(row);_next_bubble=now+BUBBLE_GAP;
 }
 public void _chirp(string id,string text,V origin,V evidence) {
  if(_heard.Has(id))return;_heard[id]=true;while(_heard.Count>512)_heard.Erase(_heard.Keys[0]);var row=origin.Duplicate(true);row.Merge(V.Map("id",id,"text",text,"evidence",evidence.Duplicate(true)),true);_history.Add(row);while(_history.Count>HISTORY_LIMIT)_history.Items.RemoveAt(0);
 }
 public void _update_voices(Game g,double now) {
  var metrics=g.s.Get("metrics",V.Map());foreach(var id in g.s["projects"].Keys){var p=g.s["projects"][id];if(_projects.Has(id)&&_projects[id].Text=="construction"&&p.Get("status","").Text=="running"){var st=g.compiled.Get("stats",V.Map()).Get(id,V.Map());_chirp("opening:"+id,p.Get("name",g.c.projects.Get(p.Get("def",""),V.Map()).Get("name",id)).Text+"已竣工，可提供"+F(st.Get("jobs",0).Double,0)+"个岗位。",source(g,id,"employment"),V.Map("tick",g.s["simulation_tick"],"status","running","jobs",st.Get("jobs",0).Double));}_projects[id]=p.Get("status","");}
  foreach(var gid in g.s.Get("groups",V.Map()).Keys){var group=g.s["groups"][gid];var living=group.Get("living",V.Map());var current=V.Map("host",group.Get("host",""),"medical",living.Get("medical",0).Double,"shopping",living.Get("commercial",0).Double,"unemployment",living.Get("unemployment",1).Double,"pollution",living.Get("pollution",0).Double);
   if(_groups.Has(gid)){var previous=_groups[gid];var evidence=V.Map("previous",previous,"current",current,"tick",g.s["simulation_tick"]);
    if(previous["host"].Text=="homeless"&&current["host"].Text!="homeless")_say(g,gid,"这次终于能安顿下来了。","housing",evidence,now);
    else if(current["medical"].Double-previous["medical"].Double>=.1&&!_medical_changes.Has(gid))_medical_changes[gid]=V.Map("baseline",previous["medical"],"since",g.s.Get("local_hours",0).Double,"evidence",evidence.Duplicate(true));
    else if(previous["unemployment"].Double-current["unemployment"].Double>=.1)_say(g,gid,"最近能找到活干了。","employment",evidence,now);
    else if(current["pollution"].Double-previous["pollution"].Double>=.1)_say(g,gid,"厂子忙起来了，空气也不一样了。","pollution",evidence,now);
    else if(current["shopping"].Double<.5&&previous["shopping"].Double<.5)_say(g,gid,"买点日用品还真不容易。","shopping",evidence,now);}
   if(_medical_changes.Has(gid)){var change=_medical_changes[gid];if(current["medical"].Double<change["baseline"].Double+.1)_medical_changes.Erase(gid);else if(g.s.Get("local_hours",0).Double-change["since"].Double>=LOCAL_HOLD&&now>=_next_bubble){var proof=change["evidence"].Duplicate(true);proof["current"]=current.Duplicate(true);proof["held_local_months"]=g.s["local_hours"].Double-change["since"].Double;_say(g,gid,"这回看诊不用到处找地方了。","medical",proof,now);_medical_changes.Erase(gid);}}_groups[gid]=current;
  }
  foreach(var trade in metrics.Get("market_trades",V.Arr())){if(trade.Get("side","").Text!="sell"||trade.Get("amount",0).Double<=0)continue;var id="export:"+trade.Get("id",g.s["simulation_tick"].Text+":"+trade.Get("order_id","").Text).Text;var logistics_id=trade.Get("source_id","municipal").Text;var capacity_sources=trade.Get("capacity_sources",V.Arr());if(capacity_sources.Count!=0)logistics_id=capacity_sources[0].Get("project_id",logistics_id).Text;var origin=source(g,logistics_id,trade.Get("resource","goods").Text,"trade");_chirp(id,"本区完成一批"+RESOURCE_NAMES.Get(trade.Get("resource","").Text,"物资").Text+"外运。",origin,trade);}
  var year=(int)Math.Floor(g.s.Get("local_hours",0).Double/12.0);var served=0.0;var commercial_source="";foreach(var link in metrics.Get("service_links",V.Arr())){if(link.Get("kind","").Text!="commercial")continue;served+=link.Get("people",0).Double;if(link.Get("people",0).Double>0&&commercial_source=="")commercial_source=link.Get("source","").Text;}
  if(year!=_commercial_year){if(_commercial_year>=0&&served>_commercial_served+1.0&&commercial_source!="")_chirp("commercial-year:"+year,"比上一年度结算时，更多居民能在附近买到日用品。",source(g,commercial_source,"commercial"),V.Map("previous_actual_served",_commercial_served,"actual_served",served,"tick",g.s["simulation_tick"]));_commercial_year=year;_commercial_served=served;}
 }
 public void _update_journal(Game g) {
  foreach(var evt in g.s.Get("log",V.Arr())){if(!V.Arr("commission_completed","commission_theme_completed","theme_completed").Contains(evt.Get("kind","")))continue;var payload=evt.Get("data",V.Map());_chirp("commission:"+evt.Get("tick",0).Text+":"+payload.Get("id",payload.Get("theme","")).Text,evt.Get("text","城市建设目标达成。").Text,V.Map("source_id",payload.Get("id",payload.Get("theme","")).Text,"building_id","","tile","","layer","normal","kind","commission"),evt);}
 }
 public void _update_capstone(Game g) {
  var capstone=g.s.Get("block",V.Map()).Get("capstone",V.Map());if(capstone.Count==0)return;var id=capstone.Get("source_id",capstone.Get("id","")).Text;if(id==""||id==_capstone)return;_capstone=id;
  _chirp(id,g.s["block"].Get("display_name","本区").Text+"的"+capstone.Get("name","地区主题建筑").Text+"正式启用。",V.Map("source_id",id,"building_id","","tile",capstone.Get("tile_anchor","").Text,"layer","normal","kind","capstone"),capstone);
 }
}
}
