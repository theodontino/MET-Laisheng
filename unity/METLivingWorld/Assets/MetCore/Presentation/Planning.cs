using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
namespace MET.Core {
/// <summary>Read-only forecasts run the real engine on detached state, at the same next step.</summary>
public sealed class Planning {
 public V cache=V.Map(),lru=V.Arr(),timings=V.Arr();public string token="";
 public string version(Game g) {
  var projects=V.Arr();foreach(var id in Rules.ids(g.s.Get("projects",V.Map()))){var p=g.s["projects"][id];projects.Add(V.Arr(id,p.Get("inventory",V.Map()),p.Get("retained_storage",V.Map()),p.Get("status",""),p.Get("paused",false),p.Get("work_priority",0),p.Get("job",V.Map()),p.Get("upgrade_job",V.Map()),p.Get("usage",0),p.Get("next_heat_bonus",0),p.Get("next_heat_credit",0)));}
  var block=g.s.Get("block",V.Map());var orders=V.Arr();foreach(var order in block.Get("trade_orders",V.Arr()))if(V.Arr("open","partial").Contains(order.Get("status","")))orders.Add(V.Arr(order.Get("id",""),order.Get("resource",""),order.Get("side",""),order.Get("remaining",0),order.Get("limit_price",0),order.Get("sequence",0)));
  var state=V.Arr(RuntimeHelpers.GetHashCode(g),RuntimeHelpers.GetHashCode(g.c),g.revision,g.world_managed,g.world_effects,g.s.Get("simulation_tick",0),g.s.Get("phase",""),g.s.Get("currency",0),g.s.Get("dt",0),g.s.Get("local_hours",0),g.s.Get("beta_time",V.Map()).Get("mode","normal"),g.s.Get("goods_active",false),g.s.Get("goods_requested_at",-1),g.s.Get("migration_paused",false),projects,orders,block.Get("market_state",V.Map()),block.Get("construction_capacity",0),block.Get("policies",V.Map()),block.Get("blueprint_levels",V.Map()),block.Get("capstone",V.Map()),g.c.balance.Get("tick_hours",0),g.c.beta.Get("external_market_candidate",V.Map()));
  return state.ToJson(sortKeys:true);
 }
 public Game copy_game(Game g){var other=new Game(g.c,g.storageRoot);other.s=g.s.Duplicate(true);other.preview_mode=true;other.world_managed=g.world_managed;other.world_effects=g.world_effects.Duplicate(true);other.refresh_metrics();return other;}
 public void reset_for(Game g){var key=version(g);if(key!=token){cache=V.Map();token=key;lru=V.Arr();}}
 public V snapshot(Game g) {
  reset_for(g);if(cache.Has("world"))return cache["world"];
  if(!V.Arr("play","failed").Contains(g.s["phase"])||g.s["projects"].Count==0)return V.Map("seed",g.s.Get("seed",0),"block",g.s["block"].Duplicate(true),"metrics",g.s["metrics"],"audit",V.Map(),"projects",g.s["projects"],"groups",g.s["groups"],"tiles",g.s["cells"],"error","");
  var clone=copy_game(g);var err=Sim.tick(clone,g.c.balance["tick_hours"].Double);var result=V.Map("seed",clone.s.Get("seed",0),"block",clone.s["block"].Duplicate(true),"metrics",clone.s["metrics"],"audit",clone.s["audit"],"projects",clone.s["projects"],"groups",clone.s["groups"],"tiles",clone.s["cells"],"compiled",clone.compiled,"error",err,"forecast",true,"tick",clone.s["simulation_tick"],"snapshot_id",version(g)+":forecast","source_phase","next_tick");cache["world"]=result;return result;
 }
 public V compare(Game g,string @base,string tile,V offer=null,string host="",string slot="") {
  offer=offer??V.Map();reset_for(g);var key=V.Arr(@base,tile,offer,host,slot).ToJson();if(cache.Has(key))return cache[key];var began=Stopwatch.GetTimestamp();var reference=snapshot(g);var clone=copy_game(g);var target=host;
  if(host==""){if(!clone.s["cells"].Has(tile)||clone.s["cells"][tile]["building"].Text!=""||clone.s["cells"][tile]["traits"].Contains("tile.water"))return V.Map("ok",false,"error","请选择未开发的陆地。");var op=offer.Get("operator_org","commons").Text;target=clone.create_building(@base,tile,op,true)["id"].Text;if(offer.Count==0)clone.s["projects"][target]["operator_unresolved"]=true;}
  if(offer.Count!=0&&offer.Has("bonus_id")){var chosen_slot=offer.Get("slot",slot).Text;var install_error=clone._install_bonus(offer["bonus_id"].Text,"building",target,offer.Get("provider_org","commons").Text,chosen_slot);if(install_error!="")return V.Map("ok",false,"error",install_error);}
  clone.refresh_metrics();var err=Sim.tick(clone,g.c.balance["tick_hours"].Double);if(err!="")return V.Map("ok",false,"error",err);var st=clone.project_stats(target);var a=clone.s["metrics"]["utilization"].Get(target,V.Map());var services=V.Map();var additional=V.Map();
  foreach(var kind in g.c.balance["service_kinds"]){var own=0.0;var total_now=0.0;var total_before=0.0;foreach(var link in clone.s["metrics"]["service_links"])if(link["kind"]==kind){total_now+=link["people"].Double;if(link["source"].Text==target)own+=link["people"].Double;}foreach(var link in reference["metrics"].Get("service_links",V.Arr()))if(link["kind"]==kind)total_before+=link["people"].Double;services[kind]=own;additional[kind]=total_now-total_before;}
  var harmed=0.0;var exposure_delta=0.0;foreach(var gid in g.s["groups"].Keys){var old=reference["groups"].Get(gid,V.Map());var now=clone.s["groups"].Get(gid,V.Map());if(old.Count==0||now.Count==0)continue;var diff=now["living"].Get("pollution",0).Double-old["living"].Get("pollution",0).Double;if(diff>.015)harmed+=g.s["groups"][gid]["count"].Double;exposure_delta+=diff*g.s["groups"][gid]["count"].Double;}
  var inputs=V.Map();var retained=V.Map();var losses=V.Map();foreach(var entry in clone.s["metrics"]["activity_ledger"].Get(target,V.Arr())){var resource=entry["resource"].Text;if(entry["kind"].Text=="input")inputs[resource]=inputs.Get(resource,0).Double+entry["rate"].Double;else {retained[resource]=retained.Get(resource,0).Double+entry["kept"].Double;losses[resource]=losses.Get(resource,0).Double+entry["lost"].Double;}}
  var workers=0.0;foreach(var link in clone.s["metrics"]["employment_links"])if(link["target"].Text==target)workers+=link["people"].Double;var effects=V.Arr();foreach(var bid in g.playable_ids()){var before=reference["metrics"].Get("utilization",V.Map()).Get(bid,V.Map()).Get("utilization",0).Double;var after=clone.s["metrics"]["utilization"].Get(bid,V.Map()).Get("utilization",0).Double;if(after<before-.1)effects.Add(V.Map("id",bid,"delta",after-before));}
  var result=V.Map("ok",true,"target",target,"stats",st,"activity",a,"outputs",clone.s["projects"][target]["outputs_last"],"retained",retained,"losses",losses,"inputs",inputs,"services",services,"added_service",additional,"workers",workers,
   "housing",clone.s["metrics"]["housing"].Double-reference["metrics"].Get("housing",0).Double,"cashflow",clone.s["metrics"]["cashflow"].Double-reference["metrics"].Get("cashflow",0).Double,
   "energy_demand",clone.s["metrics"]["energy_need"].Double-reference["metrics"].Get("energy_need",0).Double,"energy_supply",clone.s["metrics"]["energy"].Double-reference["metrics"].Get("energy",0).Double,
   "energy_ratio",clone.s["metrics"]["energy_ratio"],"harmed",harmed,"exposure_delta",exposure_delta,"affected",effects,
   "existing_food_produced",reference["audit"].Get("resources",V.Map()).Get("food",V.Map()).Get("produced",0).Double/g.c.balance["tick_hours"].Double,
   "existing_food_consumed",reference["audit"].Get("resources",V.Map()).Get("food",V.Map()).Get("consumed",0).Double/g.c.balance["tick_hours"].Double,
   "recycled",clone.s["metrics"]["pollution_recycled"].Double-reference["metrics"].Get("pollution_recycled",0).Double,"tiles",clone.s["cells"],"trace",clone.compiled["trace"].Get(target,V.Map()),"world",clone.s["metrics"]);
  if(host=="")result["construction_quote"]=WildDesign.BuildQuote(g,@base);
  result["map_delta"]=map_diff(reference,clone);result["map_snapshot"]=display_copy(clone);result["snapshot_id"]=version(g);result["candidate_key"]=key;result["forecast"]=true;result["elapsed_ms"]=(Stopwatch.GetTimestamp()-began)*1000.0/Stopwatch.Frequency;timings.Add(result["elapsed_ms"]);if(timings.Count>200)timings.Items.RemoveAt(0);remember(key,result);return result;
 }
 public V policy(Game g,string id) {
  reset_for(g);var key="policy:"+id;if(cache.Has(key))return cache[key];var reference=snapshot(g);var clone=copy_game(g);clone.s["block"]["policies"][clone.c.policies[id]["slot"]]=id;var err=Sim.tick(clone,g.c.balance["tick_hours"].Double);
  var result=V.Map("ok",err=="","error",err,"cashflow",clone.s["metrics"]["cashflow"],"delta",clone.s["metrics"]["cashflow"].Double-reference["metrics"].Get("cashflow",0).Double,"tax",clone.s["metrics"]["income"],"map_delta",map_diff(reference,clone),"map_snapshot",display_copy(clone),"snapshot_id",version(g));remember(key,result);return result;
 }
 public V terrain(Game g,string @base) {
  reset_for(g);var key="terrain:"+@base;if(cache.Has(key))return cache[key];var result=V.Map();if(!g.c.projects.Has(@base))return result;
  foreach(var tile in Rules.ids(g.s["cells"])){if(g.s["cells"][tile]["building"].Text!=""||g.s["cells"][tile]["traits"].Contains("tile.water"))continue;var clone=copy_game(g);var target=clone.create_building(@base,tile.Text,"commons",true)["id"].Text;clone.s["projects"][target]["operator_unresolved"]=true;clone.refresh_metrics();var st=clone.compiled["stats"][target];var original=g.c.projects[@base]["stats"];var ratio=1.0;var name="通用地块";
   foreach(var field in new[]{"output.food","output.material","output.goods","output.energy","service.entertainment"})if(original.Get(field,0).Double>0){var x=st.Get(field,0).Double/original[field].Double;if(x>ratio){ratio=x;name="额定产能 ×"+ratio.ToString("F2",CultureInfo.InvariantCulture);}}
   if(@base=="works"&&st["energy_demand"].Double<original["energy_demand"].Double){ratio=1.25;name="水岸冷却 · 用电减少20%";}result[tile]=V.Map("ratio",ratio,"label",name);
  }remember(key,result);return result;
 }
 public void remember(string key,V result){cache[key]=result;lru.Erase(key);lru.Add(key);while(lru.Count>16){var oldest=lru[0];lru.Items.RemoveAt(0);cache.Erase(oldest);}}
 public V display_copy(Game g)=>V.Map("seed",g.s.Get("seed",0),"block",g.s["block"].Duplicate(true),"metrics",g.s["metrics"],"audit",g.s["audit"],"projects",g.s["projects"],"groups",g.s["groups"],"tiles",g.s["cells"],"compiled",g.compiled,"forecast",true,"tick",g.s["simulation_tick"],"source_phase","next_tick","error","");
 public V map_diff(V before,Game after) {
  var result=V.Map();foreach(var tile in after.s["cells"].Keys){var entry=V.Map("services",V.Map(),"pollution",0.0,"utilization",0.0,"resources",V.Map());foreach(var kind in after.c.balance["service_kinds"]){var a=0.0;var b=0.0;
   foreach(var link in before["metrics"].Get("service_links",V.Arr()))if(link["kind"]==kind&&before["projects"].Get(link["home"].Text,V.Map()).Get("tile","").Text==tile)a+=link["people"].Double;
   foreach(var link in after.s["metrics"].Get("service_links",V.Arr()))if(link["kind"]==kind&&after.s["projects"].Get(link["home"].Text,V.Map()).Get("tile","").Text==tile)b+=link["people"].Double;entry["services"][kind]=b-a;}
   entry["pollution"]=Rules.aggregate(after.s["cells"][tile]["fields"].Get("pollution",V.Arr()))-Rules.aggregate(before["tiles"].Get(tile,V.Map()).Get("fields",V.Map()).Get("pollution",V.Arr()));var bid=after.s["cells"][tile]["building"].Text;
   if(bid!=""&&before["projects"].Has(bid))entry["utilization"]=after.s["metrics"]["utilization"].Get(bid,V.Map()).Get("utilization",0).Double-before["metrics"].Get("utilization",V.Map()).Get(bid,V.Map()).Get("utilization",0).Double;result[tile]=entry;
  }return result;
 }
 public V scope(Game g,V offer,string host_type,string host) {
  reset_for(g);var key="scope:"+offer.Get("bonus_id","").Text+":"+host_type+":"+host+":"+offer.Get("provider_org","").Text;if(cache.Has(key))return cache[key];var reference=snapshot(g);var clone=copy_game(g);var err=clone._install_bonus(offer["bonus_id"].Text,host_type,host,offer["provider_org"].Text,"");if(err!="")return V.Map("ok",false,"error",err);err=Sim.tick(clone,g.c.balance["tick_hours"].Double);
  var result=V.Map("ok",err=="","error",err,"map_delta",map_diff(reference,clone),"map_snapshot",display_copy(clone),"snapshot_id",version(g),"cashflow",clone.s["metrics"]["cashflow"].Double-reference["metrics"].Get("cashflow",0).Double);remember(key,result);return result;
 }
}
}
