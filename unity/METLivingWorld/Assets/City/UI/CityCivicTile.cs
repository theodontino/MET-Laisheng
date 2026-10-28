using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UIElements;
using C=MET.City.CivicUI;
namespace MET.City {
public sealed partial class CityInterface {
 void RefreshCivicTile(){
  if(!CivicEnabled)return;
  if(PointerBusy){_contextDirty=true;return;}_contextDirty=false;
  bool visible=(!_archiveOpen||_mapFromCompact)&&(!_mapViewing||_mapFromCompact)&&!CivicOperationOpen&&!ToolkitUpgradeOpen&&SelectedTile!=""&&S["cells"].Has(SelectedTile);
  _civic.Visible("civic-tile",visible);if(!visible)return;
  var data=TileSummary.describe(G,SelectedTile);var key=V.Arr(TileVisibleKey(data),TextScale,Runtime.ViewCanEditFocused).ToJson();if(_civicTileKey==key)return;_civicTileKey=key;
  var buildings=data["buildings"];bool empty=buildings.Count==0,water=data["cell"].Get("traits",V.Arr()).Contains("tile.water");var p=empty?V.Map():buildings[0];string id=p.Get("id","").Text;
  string status=!Runtime.ViewCanEditFocused?"只读街区":water?"水域 · 不可建设":empty?"可选址":p["paused"].Bool?"已暂停":p["status"].Text=="construction"?"新建中 · 完工后投产":p["upgrade_job"].Count>0?"升级中 · 原楼降效运营":"运行中";
  CivicHeader("tile",(empty?"未开发地块":p["name"].Text)+" · "+SelectedTile,status,DismissLocalSelection);
  _civic.Render("tile",TileStateKey,(body,actions)=>{
   var identity=C.Row(body,"c-tile-identity");
   if(!empty){var portrait=new UnityEngine.UIElements.Image{image=_inspectionWorld?.PortraitForBuilding(id),scaleMode=ScaleMode.ScaleAndCrop};portrait.AddToClassList("c-portrait");identity.Add(portrait);}else C.Icon(identity,"terrain","c-portrait");
   var info=C.Column(identity);C.Label(info,TileTerrain(data["cell"].Get("terrain","plain").Text)+" · "+G.c.tags_text(data["cell"].Get("traits",V.Arr())),"c-small c-muted");
   C.Label(info,empty?"选择这片土地的用途":p["operator_name"].Text,"c-heading");C.Label(info,empty?(water?"该地形不支持建设":!Runtime.ViewCanEditFocused?"历史街区保留原状":"从下方选择建筑类型"):"T"+p["tier"].Text,"c-small c-good");
   if(!empty){var people=data["people"];var stats=G.compiled.Get("stats",V.Map()).Get(id,V.Map());double housing=p["status"].Text=="running"?Math.Max(0,stats.Get("housing",0).Double*stats.Get("efficiency",1).Double):0;
    var capacities=C.Row(body);C.Meter(capacities,"住房入住",people["population"].Double,housing,"人");C.Meter(capacities,"居民就业",people["resident_employed"].Double,people["population"].Double,"人");C.Meter(capacities,"岗位使用",people["workplace_jobs_filled"].Double,people["workplace_jobs"].Double,"岗位");
    var job=p["upgrade_job"].Count>0?p["upgrade_job"]:p["status"].Text=="construction"?p["job"]:V.Map();if(job.Count>0){C.Meter(body,"施工进度",job["total"].Double-job["work"].Double,job["total"].Double,"CP","还需 "+F(job["material"].Double)+" 材料");var priorities=C.Row(body);foreach(var n in new[]{1,0,-1}){int v=n;C.Button(priorities,n==1?"优先":n==0?"普通":"延后",()=>Command(g=>Construction.set_priority(g,id,v)),p.Get("construction_priority",0).Long==n,Runtime.ViewCanEditFocused);}}
   }
   var overview=C.Row(body);overview.style.alignItems=Align.FlexStart;var net=C.Column(overview,"c-tile-net");var service=C.Column(overview,"c-tile-services");C.Label(net,"净收入 /月","c-heading");bool known=data["settlement_available"].Bool;
   foreach(var resource in new[]{"food","material","goods","currency"}){var r=data["resources"].FirstOrDefault(x=>x["resource"].Text==resource)??V.Map();double amount=resource=="currency"?data["cash"]["income_attributed"].Double-data["cash"]["upkeep"].Double:r.Get("production",0).Double-r.Get("building_input",0).Double-r.Get("resident_consumption",0).Double-r.Get("construction_material",0).Double-r.Get("production_lost",0).Double;C.Pair(net,Resource(resource),known?Signed(amount):"待结算",amount<0?"c-warning":"c-good");}
   C.Label(service,"服务与环境","c-heading");C.Label(service,data["people"]["population"].Double>0?"居民实际满足":"当地服务可达","c-micro c-muted");
   var services=new[]{"medical","education","entertainment","commercial"};for(int i=0;i<4;i+=2){var row=C.Row(service);foreach(var kind in services.Skip(i).Take(2)){var f=data["fields"].FirstOrDefault(x=>x["layer"].Text==kind)??V.Map();if(f.Get("available",false).Bool)C.Meter(row,kind=="commercial"?"消费":Service(kind),f["value"].Double*100,100,"%","",f["value"].Double<.95?"c-warning":"");else C.Value(row,Service(kind),"待结算");}}
   var env=C.Row(service);foreach(var kind in new[]{"pollution","heat"}){var f=data["fields"].FirstOrDefault(x=>x["layer"].Text==kind)??V.Map();C.Label(env,(kind=="pollution"?"污染 ":"余热 ")+(f.Get("available",false).Bool?F(f["value"].Double,2):"待结算"),"c-small c-muted");}
   if(!empty){var slots=C.Row(body,"c-wrap");foreach(var slot in p["slots"]){string sid=slot["slot"].Text;var b=C.Button(slots,G.slot_label(id,sid).Replace("方案","")+"\n"+(slot["instance_id"].Text!=""?slot["name"].Text:"＋ 选择 Bonus"),()=>Navigate("slot",id+"|"+sid));b.AddToClassList("c-slot");}if(p["slots"].Count==0)C.Label(body,"暂无已解锁槽位","c-small c-muted");}
   C.Fold(body,"地块来源与邻接",source=>{
    C.Label(source,data["settlement_available"].Bool?"最近实际周期折合每月；库存另受物流调拨影响。":"等待实际结算，不补造历史数据。","c-small c-muted");
    foreach(var r in data["receipts"])C.Pair(source,Resource(r["resource"].Text)+" · "+(r["kind"].Text=="output"?"生产":r.Get("category","").Text=="resident"?"居民消费":r.Get("category","").Text=="construction"?"施工领料":"生产投入"),F(r.Get("rate",0).Double)+" /月");
    foreach(var r in data["income_sources"])C.Pair(source,r.Get("name","收入来源").Text,Signed(r["rate"].Double)+" 通货/月");foreach(var r in data["fiscal"])C.Pair(source,r.Get("name","维护支出").Text,"−"+F(r["rate"].Double)+" 通货/月");
    foreach(var f in data["fields"])C.Fold(source,LayerLabel(f["layer"].Text)+" · 来源",s=>CivicBuildingFieldSources(s,f));
    C.Fold(source,"居民与岗位来源",s=>{foreach(var g in data["people"]["groups"]){C.Pair(s,ResidentHostName(g["host"].Text),F(g["population"].Double)+" 人 · 就业 "+F(g["employed"].Double)+" 人");foreach(var j in g["employment"].Pairs)C.Pair(s,"→ "+G.project_name(j.Key),F(j.Value.Double)+" 人");}});
    C.Label(source,"邻接加成","c-heading");if(data["adjacency"].Count==0)C.Label(source,"暂无生效邻接加成","c-muted");foreach(var link in data["adjacency"]){var tile=link["tile"].Text;C.Button(source,G.project_name(link["source_id"].Text)+" · "+Signed(link["amount"].Double*100)+"% · 定位",()=>_inspectionWorld?.FocusInspectionSource(tile));}_civic.ApplyText(source);
   });
   if(empty){foreach(var baseValue in G.c.v040["progression"]["tier1"]){string baseId=baseValue.Text,tile=SelectedTile;var b=C.Button(actions,"",()=>OpenConstructionWindow(baseId,tile),false,!water&&Runtime.ViewCanEditFocused,"build-"+baseId);C.Icon(b,baseId);C.Label(b,G.c.projects[baseId]["name"].Text,"c-small");b.AddToClassList("c-tile-build");}}
   else {bool construction=p["status"].Text=="construction"||p["upgrade_job"].Count>0;C.Button(actions,p["paused"].Bool?(construction?"恢复施工":"恢复运营"):(construction?"暂停施工":"暂停运营"),()=>Command(g=>g.set_paused(id,!p["paused"].Bool)),enabled:Runtime.ViewCanEditFocused);bool wild=WildDesign.IsFarm(G,S["projects"][id]);C.Button(actions,wild?"建筑星图":"升级",()=>OpenUpgradeWindow(id),true,wild||(!construction&&Runtime.ViewCanEditFocused&&Development.choices(G,id).Count>0));C.Button(actions,"建筑详表 ↗",()=>Navigate("building",id));}
  });SyncCivicVisibility();
 }
}
}
