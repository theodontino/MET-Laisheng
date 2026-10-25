using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;
namespace MET.City {
public sealed partial class CityInterface {
 RectTransform _tileHeader,_tileBody,_tileFooter,_tileViewport;
 ScrollRect _tileScroll;
 string _renderedTile="";
 V _tileSummary=V.Map(),_tileEnergy=V.Map();
 readonly Dictionary<string,float> _tilePositions=new Dictionary<string,float>();
 readonly HashSet<string> _tileDisclosures=new HashSet<string>();
 void BuildTilePanel(){
  _context=Surface("Selected tile",_root,Panel);
  Pin(_context,new Vector2(0,0),new Vector2(0,0),new Vector2(12,12),new Vector2(600,520));
  _tileHeader=Node("Tile heading",_context);_tileFooter=Node("Tile actions",_context);
  _tileViewport=Surface("Tile viewport",_context,new Color(.02f,.055f,.065f,.9f));
  _tileViewport.GetComponent<Image>().color=new Color(.035f,.065f,.085f,.97f);_tileViewport.gameObject.AddComponent<RectMask2D>();
  _tileBody=Column(_tileViewport,"Local facts",5);_tileBody.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(6,6,6,8);
  _tileBody.anchorMin=new Vector2(0,1);_tileBody.anchorMax=Vector2.one;_tileBody.pivot=new Vector2(.5f,1);_tileBody.sizeDelta=Vector2.zero;
  _tileScroll=_tileViewport.gameObject.AddComponent<ScrollRect>();_tileScroll.viewport=_tileViewport;_tileScroll.content=_tileBody;_tileScroll.horizontal=false;_tileScroll.vertical=true;_tileScroll.scrollSensitivity=48;_tileScroll.movementType=ScrollRect.MovementType.Clamped;
  _context.gameObject.SetActive(false);
 }
 void TileLine(Transform parent,string title,string value,Color? color=null)=>TileMatrixRow(parent,new[]{title,value},new[]{0f,.68f,1f},false,color);
 void RefreshTilePanel(){
  if(_context==null)return;if(PointerBusy){_contextDirty=true;return;}_contextDirty=false;
  if(_renderedTile!=""&&_context.gameObject.activeSelf)_tilePositions[_renderedTile]=_tileScroll.verticalNormalizedPosition;
  bool visible=!_archiveOpen&&!_mapViewing&&!ConstructionWindowOpen&&SelectedTile!=""&&S["cells"].Has(SelectedTile);
  _context.gameObject.SetActive(visible);if(!visible)return;
  _tileSummary=TileSummary.describe(G,SelectedTile);_tileEnergy=MapInspection.Describe(S,SelectedTile,"energy",includeNeighbors:false);string key=ContextDisplayKey();if(key==_contextKey)return;
  ContextRebuildCount++;Clear(_tileHeader);Clear(_tileBody);Clear(_tileFooter);_tileBody.name="Local facts "+SelectedTile;
  var data=_tileSummary;var buildings=data["buildings"];var cell=data["cell"];bool empty=buildings.Count==0,water=cell.Get("traits",V.Arr()).Contains("tile.water");
  var title=TileText(_tileHeader,(empty?"地块":buildings[0]["name"].Text)+" · "+SelectedTile,21,Green);Pin(title.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(14,-5),new Vector2(510,36*TextScale));
  var close=Button(_tileHeader,"×",DismissLocalSelection);Pin((RectTransform)close.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-8,-5),new Vector2(36,36));
  float header=43*TextScale,footer=(empty?62:44)*TextScale;
  _tileHeader.anchorMin=new Vector2(0,1);_tileHeader.anchorMax=Vector2.one;_tileHeader.pivot=new Vector2(.5f,1);_tileHeader.sizeDelta=new Vector2(0,header);_tileHeader.anchoredPosition=Vector2.zero;
  _tileFooter.anchorMin=Vector2.zero;_tileFooter.anchorMax=new Vector2(1,0);_tileFooter.pivot=new Vector2(.5f,0);_tileFooter.sizeDelta=new Vector2(-20,footer);_tileFooter.anchoredPosition=new Vector2(0,8);
  Stretch(_tileViewport,8,header,8,footer+15);
  RenderTileIdentity(data,empty,water);
  var overview=TileBlock(_tileBody,"Net income and local services",208);
  var economy=Column(overview,"Local net income",4,false);TileBounds(economy,0,.4f);economy.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);
  var services=Column(overview,"Local services and environment",4,false);TileBounds(services,.4f,1);services.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);
  RenderTileEconomy(economy,data,empty);
  RenderTileFields(services,data);
  RenderTileSlots(data);
  RenderTileAdjacency(data);
  BuildTileActions(empty,water);
  _contextKey=key;_renderedTile=TileStateKey;Canvas.ForceUpdateCanvases();_tileScroll.StopMovement();_tileScroll.verticalNormalizedPosition=_tilePositions.TryGetValue(_renderedTile,out var position)?position:1;
 }
 void RenderTileIdentity(V data,bool empty,bool water){
  var strip=TileBlock(_tileBody,"Identity portrait and metrics",120);
  var portrait=Node("Portrait region",strip);TileBounds(portrait,0,.25f,0,0);
  if(!empty){var building=data["buildings"][0];ContextBuildingPortrait(portrait,building["id"].Text,building["def"].Text);Stretch((RectTransform)portrait.GetChild(0),0,0,0,0);}
  else {var icon=HudIcon(portrait,"terrain",water?Blue:Green,70);Stretch(icon,14,12,14,12);}
  var identity=Node("Identity facts",strip);TileBounds(identity,.25f,.62f);
  var cell=data["cell"];var terrain=TileTerrain(cell.Get("terrain","plain").Text);var traits=G.c.tags_text(cell.Get("traits",V.Arr()));
  var terrainLabel=TileText(identity,terrain+" · "+traits,14,Blue);terrainLabel.rectTransform.anchorMin=new Vector2(0,.62f);terrainLabel.rectTransform.anchorMax=Vector2.one;terrainLabel.rectTransform.offsetMin=Vector2.zero;terrainLabel.rectTransform.offsetMax=Vector2.zero;
  string status=!Runtime.ViewCanEditFocused?"只读街区":water?"水域 · 不可建造":empty?"空地 · 可建造":data["buildings"][0]["paused"].Bool?"已暂停":data["buildings"][0]["status"].Text=="construction"?"施工中":data["buildings"][0]["upgrade_job"].Count>0?"升级中 · 原楼降效运营":"运行中";
  string details=empty?(!Runtime.ViewCanEditFocused?"该街区当前仅可查看":water?"水域不可建设":"选择下方建筑类型"):data["buildings"][0]["operator_name"].Text+"\nT"+data["buildings"][0]["tier"].Text+" · "+status;
  var operatorLabel=TileText(identity,details,14,empty?Muted:Green);operatorLabel.rectTransform.anchorMin=Vector2.zero;operatorLabel.rectTransform.anchorMax=new Vector2(1,.61f);operatorLabel.rectTransform.offsetMin=Vector2.zero;operatorLabel.rectTransform.offsetMax=Vector2.zero;
  if(empty){var condition=TileText(identity,status,14,water?Gold:Green);condition.rectTransform.anchorMin=new Vector2(0,.34f);condition.rectTransform.anchorMax=new Vector2(1,.62f);condition.rectTransform.offsetMin=condition.rectTransform.offsetMax=Vector2.zero;operatorLabel.rectTransform.anchorMax=new Vector2(1,.33f);}
  AddDeskTip(identity.gameObject,"地块与运营",terrain+" · "+traits+"\n"+details);
  var metrics=Node("People and workplace matrix",strip);TileBounds(metrics,.62f,1);
  var people=data["people"];string[] names={"居住人口","居民就业","本地岗位","岗位使用"};double[] values={people["population"].Double,people["resident_employed"].Double,people["workplace_jobs"].Double,people["workplace_jobs_filled"].Double};
  for(int i=0;i<4;i++){var metric=TileTint(metrics,"People metric "+i,_tileBand);metric.anchorMin=new Vector2((i%2)*.5f,i<2?.5f:0);metric.anchorMax=new Vector2((i%2+1)*.5f,i<2?1:.5f);metric.offsetMin=new Vector2(2,2);metric.offsetMax=new Vector2(-2,-2);TileStatCell(metric,names[i],F(values[i])+"人");
   var disclosure=i<2?"residents":"workplace";metric.GetComponent<Image>().raycastTarget=true;var control=metric.gameObject.AddComponent<Button>();control.onClick.AddListener(()=>ToggleTileDisclosure(disclosure));
   AddDeskTip(metric.gameObject,names[i],"点击查看本地人数与就业来源。");
  }
  TileInlineSource("residents",parent=>{
   TileHeading(parent,"本地居民与就业");
   foreach(var group in people["groups"]){TileLine(parent,ResidentHostName(group["host"].Text),F(group["population"].Double)+" 人 / 就业 "+F(group["employed"].Double)+" 人");foreach(var job in group["employment"].Pairs)TileLine(parent,"→ "+G.project_name(job.Key),F(job.Value.Double)+" 人");}
   if(people["groups"].Count==0)TileLine(parent,"本地居民","无人居住");
  });
  TileInlineSource("workplace",parent=>{
   TileHeading(parent,"本地岗位与使用");TileLine(parent,"岗位容量 / 已使用",F(people["workplace_jobs"].Double)+" / "+F(people["workplace_jobs_filled"].Double)+" 人");
   foreach(var group in S["groups"].Pairs)foreach(var job in group.Value.Get("employment",V.Map()).Pairs)if(data["buildings"].Any(b=>b["id"].Text==job.Key))TileLine(parent,ResidentHostName(group.Value["host"].Text)+" → "+G.project_name(job.Key),F(job.Value.Double)+" 人");
  });
  if(!empty){var p=data["buildings"][0];var job=p["upgrade_job"].Count>0?p["upgrade_job"]:p["status"].Text=="construction"?p["job"]:V.Map();if(job.Count>0){
   TileHeading(_tileBody,p["upgrade_job"].Count>0?"升级进度 · 原楼继续运营":"新建进度 · 完工后投产",F(job["total"].Double-job["work"].Double)+" / "+F(job["total"].Double)+" CP");
   var track=TileBlock(_tileBody,"Local construction progress",6);var bg=TileTint(track,"Progress base",new Color(.2f,.3f,.32f,1));Stretch(bg);var fill=TileTint(track,"Completed work",Green);fill.anchorMin=Vector2.zero;fill.anchorMax=new Vector2((float)Math.Max(0,1-job["work"].Double/Math.Max(.000001,job["total"].Double)),1);fill.offsetMin=fill.offsetMax=Vector2.zero;
   var priority=Row(_tileBody,"Project priority",30*TextScale);priority.GetComponent<HorizontalLayoutGroup>().spacing=5;
   var remaining=TileText(priority,"还需 "+F(job["material"].Double)+" 材料",14,Muted);remaining.GetComponent<LayoutElement>().flexibleWidth=1;
   foreach(var n in new[]{1,0,-1}){int value=n;string id=p["id"].Text;var b=Button(priority,(p.Get("construction_priority",0).Long==n?"✓ ":"")+(n==1?"优先":n==0?"普通":"延后"),()=>Command(g=>Construction.set_priority(g,id,value)),false,!Runtime.ViewCanEditFocused);Height((RectTransform)b.transform,30*TextScale);HudFont(b.GetComponentInChildren<Text>(),14);}
  }}
 }
 void RenderTileEconomy(RectTransform parent,V data,bool empty){
  bool settled=data["settlement_available"].Bool;
  TileHeading(parent,"净收入","来源 ›",()=>ToggleTileDisclosure("economy"));
  foreach(var resource in new[]{"food","material","goods"}){
   var row=data["resources"].FirstOrDefault(x=>x["resource"].Text==resource)??V.Map();
   double net=row.Get("production",0).Double-row.Get("building_input",0).Double-row.Get("resident_consumption",0).Double-row.Get("construction_material",0).Double-row.Get("production_lost",0).Double;
   TileMatrixRow(parent,new[]{Resource(resource),settled?TileSigned(net):"待结算"},new[]{0f,.54f,1f},true,net<0?Gold:Green);
  }
  var cash=data["cash"];double cashNet=cash["income_attributed"].Double-cash["upkeep"].Double;
  TileMatrixRow(parent,new[]{"通货",settled?TileSigned(cashNet):"待结算"},new[]{0f,.54f,1f},true,cashNet<0?Gold:Green);
  var note=TileText(parent,"最近周期折合每月",12,Muted);Height(note.rectTransform,24*TextScale);
  TileInlineSource("economy",source=>{
   TileMatrixRow(source,new[]{"来源","通货 / 月"},new[]{0f,.7f,1f},true);
   foreach(var row in data["income_sources"])TileLine(source,row.Get("name","收入来源").Text,TileSigned(row["rate"].Double));
   foreach(var row in data["fiscal"])TileLine(source,row.Get("name","维护支出").Text,"−"+F(row["rate"].Double),Gold);
   foreach(var building in data["buildings"])foreach(var pair in building["inventory"].Pairs)TileLine(source,Resource(pair.Key)+"当前库存",F(pair.Value.Double));
   Label(source,"表内为最近周期折合每月的本格产消；库存还受物流调拨影响。收入来源计入街区财政。",14,Muted,42*TextScale);
   foreach(var row in data["receipts"].Where(x=>x["resource"].Text!="energy"))TileLine(source,Resource(row["resource"].Text)+" · "+(row["kind"].Text=="output"?"生产":row.Get("category","").Text=="resident"?"居民消费":row.Get("category","").Text=="construction"?"施工领料":"生产投入"),F(row.Get("rate",0).Double)+" /月");
  });
 }
 void RenderTileFields(RectTransform parent,V data){
  var fields=data["fields"];bool residents=data["people"]["population"].Double>0;
  TileHeading(parent,"服务与环境",residents?"实际满足":"可达度");
  var services=new[]{"medical","education","entertainment","commercial"};var names=new[]{"医疗","教育","娱乐","消费"};
  int serviceColumns=2;for(int r=0;r<4/serviceColumns;r++){var row=TileBlock(parent,"Service matrix "+r,42);for(int col=0;col<serviceColumns;col++){int i=r*serviceColumns+col;var field=fields.First(x=>x["layer"].Text==services[i]);TileFieldCell(row,field,col,serviceColumns,names[i],true);}}
  foreach(var kind in services){var field=fields.First(x=>x["layer"].Text==kind);TileInlineSource("field:"+kind,source=>RenderTileSources(source,field));}
  var environment=TileBlock(parent,"Environment matrix",35);
  TileFieldCell(environment,fields.First(x=>x["layer"].Text=="pollution"),0,2,"污染",false);TileFieldCell(environment,fields.First(x=>x["layer"].Text=="heat"),1,2,"余热",false);
  foreach(var kind in new[]{"pollution","heat"}){var field=fields.First(x=>x["layer"].Text==kind);TileInlineSource("field:"+kind,source=>RenderTileSources(source,field));}
  if(data["buildings"].Count>0){
   var energy=_tileEnergy;double supply=energy["rows"].Where(x=>x["kind"].Text=="supply").Sum(x=>x["value"].Double),demand=-energy["rows"].Where(x=>x["kind"].Text=="demand").Sum(x=>x["value"].Double);
   var row=TileBlock(parent,"Local electricity",28);var text=TileText(row,energy["available"].Bool?"供电 "+F(supply)+" / 用电 "+F(demand):"电力能力 · 待结算",14,Blue);Stretch(text.rectTransform,6,0,55,0);
   var source=Button(row,"来源 ›",()=>ToggleTileDisclosure("energy"));Pin((RectTransform)source.transform,new Vector2(1,.5f),new Vector2(1,.5f),Vector2.zero,new Vector2(52,26*TextScale));HudFont(source.GetComponentInChildren<Text>(),13);
   TileInlineSource("energy",source=>RenderTileSources(source,energy));
  }
 }
 void RenderTileSlots(V data){
  var slots=data["buildings"].SelectMany(b=>b["slots"].Select(s=>new {Building=b["id"].Text,Slot=s})).ToArray();
  if(data["buildings"].Count==0)return;
  TileHeading(_tileBody,"Bonus 槽位",slots.Count(x=>x.Slot["instance_id"].Text!="")+" / "+slots.Length+" 已部署");
  if(slots.Length==0){TileLine(_tileBody,"当前建筑","暂无槽位");return;}
  int columns=TextScale>1.2f?3:4;
  for(int offset=0;offset<slots.Length;offset+=columns){var row=TileBlock(_tileBody,"Bonus socket strip "+offset,65);int count=Math.Min(columns,slots.Length-offset);
   for(int col=0;col<count;col++){var item=slots[offset+col];string id=item.Building,sid=item.Slot["slot"].Text;bool installed=item.Slot["instance_id"].Text!="";
    var button=Button(row,"",()=>Navigate("slot",id+"|"+sid));TileBounds((RectTransform)button.transform,(float)col/columns,(float)(col+1)/columns,1,1);
    var label=button.GetComponentInChildren<Text>();HudFont(label,13);label.text=G.slot_label(id,sid).Replace("方案","")+"\n"+(installed?item.Slot["name"].Text:"＋ 空槽");Stretch(label.rectTransform,5,22*TextScale,5,1);label.verticalOverflow=VerticalWrapMode.Truncate;
    var icon=HudIcon(button.transform,installed?"bonus":"build",installed?Green:Muted,19);Pin(icon,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-3),new Vector2(19,19));
    AddDeskTip(button.gameObject,G.slot_label(id,sid),installed?item.Slot["name"].Text+"\n点击管理、替换与查看来源。":"空槽 · 点击选择可用 Bonus 并比较安装。");
   }
  }
 }
 void RenderTileAdjacency(V data){
  var adjacency=data["adjacency"];double amount=adjacency.Sum(x=>x["amount"].Double);
  TileHeading(_tileBody,"邻接加成",adjacency.Count==0?"暂无生效加成":TileSigned(amount*100)+"% · "+adjacency.Count+" 处来源",adjacency.Count==0?(Action)null:()=>ToggleTileDisclosure("adjacency"));
  TileInlineSource("adjacency",parent=>{foreach(var link in adjacency)TileLine(parent,G.project_name(link["source_id"].Text)+" · "+link["tile"].Text,TileSigned(link["amount"].Double*100)+"%",Green);});
 }
 void RenderTileSources(RectTransform parent,V field){
  foreach(var row in field["rows"]){var id=row["source_id"].Text;var sourceTile=row["source_tile"].Text;string name=S["projects"].Has(id)?G.project_name(id):row["label"].Text;
   var block=TileBlock(parent,"Field source row",28);var caption=TileText(block,name,14,Muted);TileBounds(caption.rectTransform,0,.63f);var value=TileText(block,row["display"].Text,14,Ink,TextAnchor.MiddleRight);TileBounds(value.rectTransform,.63f,1);
   if(sourceTile!=""&&sourceTile!=SelectedTile){var image=block.gameObject.AddComponent<Image>();image.color=Color.clear;var button=block.gameObject.AddComponent<Button>();string target=sourceTile;button.onClick.AddListener(()=>_inspectionWorld?.FocusInspectionSource(target));AddDeskTip(block.gameObject,"定位来源","点击定位地块 "+sourceTile+"。当前面板保持不变。");}
  }
  if(field["rows"].Count==0)TileLine(parent,"来源","暂无记录");
 }
 void BuildTileActions(bool empty,bool water){
  var row=Row(_tileFooter,"Fixed tile actions",(empty?62:44)*TextScale);Stretch(row);row.GetComponent<HorizontalLayoutGroup>().spacing=6;
  if(empty){foreach(var item in G.c.v040["progression"]["tier1"]){string baseId=item.Text,tile=SelectedTile;var b=Button(row,G.c.projects[baseId]["name"].Text,()=>OpenConstructionWindow(baseId,tile),false,water||!Runtime.ViewCanEditFocused);Height((RectTransform)b.transform,62*TextScale);ConstructionIllustration(b,baseId);}return;}
  string id=SelectedBuilding;var p=S["projects"].Get(id,V.Map());if(p.Count==0)return;bool construction=p["status"].Text=="construction"||p.Get("upgrade_job",V.Map()).Count>0;
  Button(row,p.Get("paused",false).Bool?(construction?"恢复施工":"恢复运营"):(construction?"暂停施工":"暂停运营"),()=>Command(g=>g.set_paused(id,!p.Get("paused",false).Bool)),false,!Runtime.ViewCanEditFocused);
  Button(row,"升级",()=>OpenUpgradeWindow(id),false,construction||!Runtime.ViewCanEditFocused||Development.choices(G,id).Count==0);
  Button(row,"建筑详表 ›",()=>Navigate("building",id));
  foreach(var button in row.GetComponentsInChildren<Button>())Height((RectTransform)button.transform,44*TextScale);
 }
 static string TileTerrain(string terrain)=>terrain switch {"plain"=>"平原","plains"=>"平原","water"=>"水域","river"=>"河流","hill"=>"丘陵","hills"=>"丘陵","mountain"=>"山地","forest"=>"林地","wet"=>"湿地",_=>terrain};
}
}
