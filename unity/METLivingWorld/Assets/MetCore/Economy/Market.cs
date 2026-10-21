using System;
namespace MET.Core {
/// <summary>Real-time freight with shared capacity, frozen settlement quotes and deferred net pressure.</summary>
public static class Market {
 public static void ensure(Game g) {
  if(!g.s.Get("block").IsMap)g.s["block"]=V.Map("id","block:1");var block=g.s["block"];
  if(!block.Get("market_state").IsMap)block["market_state"]=V.Map("resources",V.Map());var market_state=block["market_state"];
  if(!market_state.Get("resources").IsMap){var prior=market_state.Duplicate(true);market_state=V.Map("resources",V.Map());foreach(var key in prior.Keys){if(key=="resources"||key=="last_tick")continue;if(prior[key].IsMap)market_state["resources"][key]=prior[key];}}
  var state=market_state["resources"];var configured=g.c.v040.Get("market",V.Map()).Get("resources",V.Map());
  foreach(var resource in configured.Keys){if(!state.Get(resource).IsMap)state[resource]=configured[resource].Duplicate(true);else foreach(var key in configured[resource].Keys)if(!state[resource].Has(key))state[resource][key]=configured[resource][key];}
  if(!block.Get("trade_orders").IsArray)block["trade_orders"]=V.Arr();if(!block.Get("trade_history").IsArray)block["trade_history"]=V.Arr();if(!block.Get("trade_ledger").IsArray)block["trade_ledger"]=V.Arr();
  market_state["resources"]=state;block["market_state"]=market_state;g.s["block"]=block;
 }
 public static V quote(Game g,string resource) {
  var item=g.s.Get("block",V.Map()).Get("market_state",V.Map()).Get("resources",V.Map()).Get(resource,V.Map());if(item.Count==0)return V.Map();
  var base_price=item.Get("base_price",0).Double;var depth=Math.Max(.000001,item.Get("market_depth",1).Double);var pressure=item.Get("pressure",0).Double;
  var mid=base_price+item.Get("amplitude",0).Double*Math.Tanh(pressure/depth);var spread=Math.Max(0,item.Get("spread",0).Double);var fee=Math.Max(0,item.Get("fee",0).Double);
  var buy=mid+spread*.5+fee;var sell=mid-spread*.5-fee;if(buy<=sell)buy=sell+.000001;
  return V.Map("resource",resource,"base_price",base_price,"pressure",pressure,"market_depth",depth,"mid",mid,"spread",spread,"fee",fee,"buy",buy,"sell",sell,"buy_price",buy,"sell_price",sell);
 }
 public static V _stats(Game g,string project_id) {
  var p=g.s["projects"].Get(project_id,V.Map());var compiled=g.compiled!=null&&g.compiled.IsMap?g.compiled:V.Map();var stats=compiled.Get("stats",V.Map()).Get(project_id,V.Map());
  if(stats.Count==0)stats=g.c.projects.Get(p.Get("def","").Text,V.Map()).Get("stats",V.Map());return stats;
 }
 public static V _freight_sources(Game g) {
  var result=V.Arr();if(!g.s.Get("projects").IsMap)return result;var ids=g.playable_ids().Duplicate();ids.Items.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Text,b.Text));
  foreach(var id in ids){var p=g.s["projects"][id];if(p.Get("status","").Text!="running"||p.Get("paused",false).Bool)continue;var stats=_stats(g,id.Text);var candidate=g.c.beta["external_market_candidate"];
   var capacity=Math.Max(0,stats.Get("trade_capacity",0).Double)/candidate["legacy_capacity_reference"].Double*candidate["freight_capacity_per_real_hour"].Double*Math.Max(0,candidate["coupling_multiplier"].Double);
   var usage=Math.Min(1,Math.Max(0,p.Get("usage",1).Double));var available=capacity*usage*(g.world_managed?g.world_effects.Get("freight_multiplier",1).Double:1);if(available<=.000001)continue;result.Add(V.Map("id",id.Text,"capacity",capacity,"usage",usage,"available",available));}
  return result;
 }
 public static double _total_capacity(Game g){var total=0.0;foreach(var row in _freight_sources(g))total+=row["available"].Double;return total;}
 public static string order(Game g,string resource,string side,double amount,double limit_price) {
  ensure(g);if(g.s.Get("phase","").Text!="play")return "当前不能下贸易订单。";if(resource!="food"&&resource!="material"&&resource!="goods")return "当前市场不交易这种资源。";
  if(side!="buy"&&side!="sell")return "请选择买入或卖出。";if(double.IsNaN(amount)||double.IsInfinity(amount)||amount<=0)return "订单数量必须大于零。";if(double.IsNaN(limit_price)||double.IsInfinity(limit_price))return "限价无效。";
  if(_total_capacity(g)<=.000001)return "需要有正在运行的货运站。";var id=g.uid("T");var quote_now=quote(g,resource);
  var row=V.Map("id",id,"resource",resource,"side",side,"amount",amount,"remaining",amount,"limit_price",limit_price,"created_tick",g.s.Get("simulation_tick",0).Long,"sequence",long.Parse(id.StartsWith("T",StringComparison.Ordinal)?id.Substring(1):id),"created_at",g.s.Get("local_hours",0).Double,"status","open","last_attempt_tick",-1,"quote_at_order",quote_now);
  g.s["block"]["trade_orders"].Add(row);g.revision++;return "";
 }
 public static string cancel(Game g,string order_id) {
  ensure(g);for(var i=0;i<g.s["block"]["trade_orders"].Count;i++){var row=g.s["block"]["trade_orders"][i];if(row.Get("id","").Text!=order_id)continue;
   if(row.Get("status","").Text=="filled"||row.Get("status","").Text=="cancelled")return "这笔订单已经结束。";row["status"]="cancelled";row["cancelled_at"]=g.s.Get("local_hours",0).Double;g.s["block"]["trade_orders"][i]=row;g.revision++;return "";}return "找不到这笔订单。";
 }
 public static double _storage_cap(Game g,string project_id,string resource) {
  var stats=_stats(g,project_id);var cap=Math.Max(0,stats.Get("storage."+resource,0).Double);
  if(project_id=="municipal"){var options=g.compiled!=null&&g.compiled.IsMap?g.compiled.Get("options",V.Map()):V.Map();cap*=options.Get("municipal_storage",1).Double;}
  return Math.Max(cap,g.s["projects"][project_id].Get("retained_storage",V.Map()).Get(resource,0).Double);
 }
 public static double _buy_room(Game g,string resource) {var room=0.0;foreach(var id in g.s["projects"].Keys)room+=Math.Max(0,_storage_cap(g,id,resource)-g.s["projects"][id].Get("inventory",V.Map()).Get(resource,0).Double);return room;}
 public static double _deposit(Game g,string resource,double amount) {
  var remaining=amount;var ids=g.s["projects"].Keys;ids.Sort(StringComparer.Ordinal);
  foreach(var id in ids){var p=g.s["projects"][id];var inventory=p.Get("inventory",V.Map());var current=inventory.Get(resource,0).Double;var room=Math.Max(0,_storage_cap(g,id,resource)-current);var add=Math.Min(room,remaining);if(add<=0)continue;
   inventory[resource]=current+add;p["inventory"]=inventory;g.s["projects"][id]=p;remaining-=add;if(remaining<=.000001)break;}return amount-remaining;
 }
 public static V _consume_capacity(V sources,double amount) {
  var remaining=amount;var used=V.Arr();foreach(var source in sources){var take=Math.Min(remaining,source["available"].Double);if(take<=.000001)continue;source["available"]=source["available"].Double-take;used.Add(V.Map("project_id",source["id"],"amount",take));remaining-=take;if(remaining<=.000001)break;}return V.Map("amount",amount-remaining,"sources",used);
 }
 public static V _orders_in_stable_order(V orders) {
  var result=orders.Duplicate(true);result.Items.Sort((a,b)=>{var aseq=a.Get("sequence",0).Long;var bseq=b.Get("sequence",0).Long;return aseq!=bseq?aseq.CompareTo(bseq):StringComparer.Ordinal.Compare(a.Get("id","").Text,b.Get("id","").Text);});return result;
 }
 public static void _audit_trade(Game g,string resource,string side,double amount,double cash_delta,double h) {
  if(!g.s.Get("audit",V.Map()).Get("resources",V.Map()).Get(resource).IsMap)return;var audit=g.s["audit"];var receipt=audit["resources"][resource];
  foreach(var key in new[]{"imported","exported","commission_delivered"})if(!receipt.Has(key))receipt[key]=0.0;
  if(side=="buy")receipt["imported"]=receipt["imported"].Double+amount;else receipt["exported"]=receipt["exported"].Double+amount;
  receipt["after"]=g.inventory_total(resource);receipt["error"]=receipt.Get("before",0).Double+receipt.Get("produced",0).Double+receipt["imported"].Double-receipt.Get("consumed",0).Double-receipt.Get("lost",0).Double-receipt["exported"].Double-receipt["commission_delivered"].Double-receipt["after"].Double;
  audit["resources"][resource]=receipt;g.s["audit"]=audit;
  if(g.s.Get("metrics",V.Map()).Get("flows").IsMap){var flow=g.s["metrics"]["flows"];flow[resource]=(receipt["after"].Double-receipt.Get("before",receipt["after"]).Double)/Math.Max(1e-15,h);g.s["metrics"]["flows"]=flow;}
  g.s["metrics"]["market_cash_delta"]=g.s["metrics"].Get("market_cash_delta",0).Double+cash_delta;
 }
 public static void tick(Game g,double h,double external_seconds=-1.0) {
  if(g.world_managed||h<=0)return;if(external_seconds<0)external_seconds=h*g.c.beta["units"]["earth_seconds_per_month"].Double;if(double.IsNaN(external_seconds)||double.IsInfinity(external_seconds)||external_seconds<0)return;
  ensure(g);var block=g.s["block"];var market_state=block["market_state"];var market=market_state["resources"];
  foreach(var resource in market.Keys){var state=market[resource];var tau=Math.Max(.000001,g.c.beta["external_market_candidate"]["pressure_relaxation_time_constant_real_seconds"].Double);state["pressure"]=state.Get("pressure",0).Double*Math.Exp(-external_seconds/tau);market[resource]=state;}
  market_state["resources"]=market;block["market_state"]=market_state;g.s["block"]=block;var quotes=V.Map();foreach(var resource in market.Keys)quotes[resource]=quote(g,resource);
  var sources=_freight_sources(g);var total_capacity=0.0;foreach(var source in sources){source["available"]=source["available"].Double*external_seconds/3600.0;total_capacity+=source["available"].Double;}
  var records=V.Arr();var per_resource_pressure=V.Map();var ledger=V.Map("tick",g.s.Get("simulation_tick",0).Long,"h",g.s.Get("local_hours",0).Double,"hours",h,"economic_months",h,"external_seconds",external_seconds,"bought",V.Map(),"sold",V.Map(),"currency_delta",0.0,"capacity",total_capacity,"used_capacity",0.0,"trades",V.Arr());
  foreach(var order_row in _orders_in_stable_order(g.s["block"]["trade_orders"])) {
   var order_id=order_row.Get("id","").Text;var order_index=-1;
   for(var i=0;i<g.s["block"]["trade_orders"].Count;i++)if(g.s["block"]["trade_orders"][i].Get("id","").Text==order_id){order_index=i;break;}
   if(order_index<0)continue;var live=g.s["block"]["trade_orders"][order_index];var status=live.Get("status","").Text;if(status!="open"&&status!="partial")continue;
   var resource=live.Get("resource","").Text;if(!quotes.Has(resource))continue;var side=live.Get("side","").Text;var quote_row=quotes[resource];var unit_price=quote_row[side=="buy"?"buy_price":"sell_price"].Double;
   var crosses=side=="buy"?unit_price<=live.Get("limit_price",0).Double+.000001:unit_price+.000001>=live.Get("limit_price",0).Double;live["last_attempt_tick"]=g.s.Get("simulation_tick",0).Long;
   if(!crosses){g.s["block"]["trade_orders"][order_index]=live;continue;}
   var remaining_capacity=0.0;foreach(var source in sources)remaining_capacity+=source["available"].Double;var amount=Math.Min(live.Get("remaining",0).Double,remaining_capacity);
   amount=Math.Min(amount,side=="buy"?_buy_room(g,resource):g.inventory_total(resource));if(side=="buy"&&unit_price>0)amount=Math.Min(amount,Math.Max(0,g.s["currency"].Double)/unit_price);if(side=="sell"&&unit_price<0)amount=Math.Min(amount,Math.Max(0,g.s["currency"].Double)/-unit_price);
   amount=Math.Max(0,amount);if(amount<=.000001){g.s["block"]["trade_orders"][order_index]=live;continue;}
   var allocation=_consume_capacity(sources,amount);amount=Math.Min(amount,allocation["amount"].Double);if(amount<=.000001){g.s["block"]["trade_orders"][order_index]=live;continue;}
   if(side=="buy"){amount=_deposit(g,resource,amount);if(amount<=.000001){g.s["block"]["trade_orders"][order_index]=live;continue;}}
   else if(!g.withdraw_stock(resource,amount)){g.s["block"]["trade_orders"][order_index]=live;continue;}
   var cash_delta=side=="buy"?-amount*unit_price:amount*unit_price;g.s["currency"]=g.s["currency"].Double+cash_delta;
   var trade=V.Map("id",g.uid("X"),"order_id",order_id,"resource",resource,"side",side,"amount",amount,"price",unit_price,"unit_price",unit_price,"mid_price",quote_row["mid"].Double,"limit_price",live["limit_price"].Double,"currency_delta",cash_delta,"tick",g.s.Get("simulation_tick",0).Long,"h",g.s.Get("local_hours",0).Double,"capacity_sources",allocation["sources"]);
   records.Add(trade);ledger["trades"].Add(trade);ledger["used_capacity"]=ledger["used_capacity"].Double+amount;ledger["currency_delta"]=ledger["currency_delta"].Double+cash_delta;
   if(side=="buy"){ledger["bought"][resource]=ledger["bought"].Get(resource,0).Double+amount;per_resource_pressure[resource]=per_resource_pressure.Get(resource,0).Double+amount;}
   else {ledger["sold"][resource]=ledger["sold"].Get(resource,0).Double+amount;per_resource_pressure[resource]=per_resource_pressure.Get(resource,0).Double-amount;}
   live["remaining"]=Math.Max(0,live.Get("remaining",0).Double-amount);live["filled"]=live.Get("filled",0).Double+amount;live["status"]=live["remaining"].Double<=.000001?"filled":"partial";live["updated_at"]=g.s.Get("local_hours",0).Double;g.s["block"]["trade_orders"][order_index]=live;
   _audit_trade(g,resource,side,amount,cash_delta,h);if(!g.s["block"]["trade_history"].IsArray)g.s["block"]["trade_history"]=V.Arr();g.s["block"]["trade_history"].Add(trade);
  }
  foreach(var resource in per_resource_pressure.Keys){var state=g.s["block"]["market_state"]["resources"][resource];state["pressure"]=state.Get("pressure",0).Double+per_resource_pressure[resource].Double;g.s["block"]["market_state"]["resources"][resource]=state;}
  foreach(var resource in g.s["block"]["market_state"]["resources"].Keys){var quote_row=quote(g,resource);var state=g.s["block"]["market_state"]["resources"][resource];state["mid"]=quote_row["mid"];state["buy_price"]=quote_row["buy_price"];state["sell_price"]=quote_row["sell_price"];g.s["block"]["market_state"]["resources"][resource]=state;}
  if(records.Count!=0){g.s["block"]["trade_ledger"].Add(ledger);if(g.s["block"]["trade_ledger"].Count>1200)g.s["block"]["trade_ledger"].Items.RemoveAt(0);
   if(g.s.Get("metrics",V.Map()).Get("market_trades").IsArray){foreach(var record in records)g.s["metrics"]["market_trades"].Add(record);}else g.s["metrics"]["market_trades"]=records.Duplicate(true);
   if(g.s.Get("metrics",V.Map()).Get("market_capacity").IsMap){g.s["metrics"]["market_capacity"]["last"]=total_capacity;g.s["metrics"]["market_capacity"]["used"]=ledger["used_capacity"].Double;}else g.s["metrics"]["market_capacity"]=V.Map("last",total_capacity,"used",ledger["used_capacity"].Double);
   var journal=g.s.Get("civic032",V.Map());if(journal.Count!=0){journal["transactions"].Add(V.Map("h",g.s.Get("local_hours",0).Double,"tick",g.s.Get("simulation_tick",0).Long,"kind","trade","label","外部市场贸易","cash",ledger["currency_delta"].Double,"dt",0.0,"balance",g.s["currency"].Double));while(journal["transactions"].Count>300)journal["transactions"].Items.RemoveAt(0);journal["cash_anchor"]=journal.Get("cash_anchor",0).Double+ledger["currency_delta"].Double;g.s["civic032"]=journal;}
  }
 }
}
}
