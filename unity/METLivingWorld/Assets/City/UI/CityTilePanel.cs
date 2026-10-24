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
 V _tileSummary=V.Map();
 readonly Dictionary<string,float> _tilePositions=new Dictionary<string,float>();
 void BuildTilePanel(){
  _context=Surface("Selected tile",_root,Panel);
  Pin(_context,new Vector2(0,0),new Vector2(0,0),new Vector2(12,12),new Vector2(600,520));
  _tileHeader=Node("Tile heading",_context);_tileFooter=Node("Tile actions",_context);
  _tileViewport=Surface("Tile viewport",_context,new Color(.02f,.055f,.065f,.9f));
  _tileViewport.gameObject.AddComponent<RectMask2D>();
  _tileBody=Column(_tileViewport,"Local facts",8);
  _tileBody.anchorMin=new Vector2(0,1);_tileBody.anchorMax=Vector2.one;_tileBody.pivot=new Vector2(.5f,1);_tileBody.sizeDelta=Vector2.zero;
  _tileScroll=_tileViewport.gameObject.AddComponent<ScrollRect>();_tileScroll.viewport=_tileViewport;_tileScroll.content=_tileBody;_tileScroll.horizontal=false;_tileScroll.vertical=true;_tileScroll.scrollSensitivity=48;_tileScroll.movementType=ScrollRect.MovementType.Clamped;
  _context.gameObject.SetActive(false);
 }
 void TileLine(Transform parent,string title,string value,Color? color=null){
  var line=Row(parent,"Tile fact",30*TextScale);var name=Label(line,title,15,Muted);name.GetComponent<LayoutElement>().flexibleWidth=1;
  var text=Label(line,value,16,color??Ink);text.alignment=TextAnchor.MiddleRight;text.GetComponent<LayoutElement>().flexibleWidth=1;
 }
 void TileSection(string title){Label(_tileBody,title,19,Blue,34*TextScale);}
 void RefreshTilePanel(){
  if(_context==null)return;
  if(PointerBusy){_contextDirty=true;return;}_contextDirty=false;
  if(_renderedTile!=""&&_tileScroll!=null&&_context.gameObject.activeSelf)_tilePositions[_renderedTile]=_tileScroll.verticalNormalizedPosition;
  bool visible=!_archiveOpen&&!_mapViewing&&!ConstructionWindowOpen&&SelectedTile!=""&&S["cells"].Has(SelectedTile);
  _context.gameObject.SetActive(visible);if(!visible)return;
  _tileSummary=TileSummary.describe(G,SelectedTile);
  string key=ContextDisplayKey();if(key==_contextKey)return;_contextKey=key;ContextRebuildCount++;
  Clear(_tileHeader);Clear(_tileBody);Clear(_tileFooter);_tileBody.name="Local facts "+SelectedTile;
  var data=_tileSummary;var buildings=data["buildings"];var cell=data["cell"];bool empty=buildings.Count==0;
  var title=Label(_tileHeader,(empty?"地块":buildings[0]["name"].Text)+" · "+SelectedTile,20,Green);
  Pin(title.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(14,-8),new Vector2(510,35*TextScale));
  var close=Button(_tileHeader,"×",DismissLocalSelection);Pin((RectTransform)close.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-8,-6),new Vector2(36,36));
  float header=48*TextScale,footer=(empty?76:104)*TextScale;
  Stretch(_tileHeader,0,0,0,Mathf.Max(0,_context.rect.height-header));
  _tileHeader.anchorMin=new Vector2(0,1);_tileHeader.anchorMax=Vector2.one;_tileHeader.pivot=new Vector2(.5f,1);_tileHeader.sizeDelta=new Vector2(0,header);_tileHeader.anchoredPosition=Vector2.zero;
  _tileFooter.anchorMin=Vector2.zero;_tileFooter.anchorMax=new Vector2(1,0);_tileFooter.pivot=new Vector2(.5f,0);_tileFooter.sizeDelta=new Vector2(-20,footer);_tileFooter.anchoredPosition=new Vector2(0,8);
  Stretch(_tileViewport,8,header,8,footer+16);
  TileSection("基础信息");
  string terrain=cell.Get("terrain","plain").Text;
  TileLine(_tileBody,"位置 / 地形",SelectedTile+" / "+TileTerrain(terrain));
  Label(_tileBody,"地貌 · "+G.c.tags_text(cell.Get("traits",V.Arr())),15,Muted);
  bool water=cell.Get("traits",V.Arr()).Contains("tile.water");
  Label(_tileBody,!Runtime.ViewCanEditFocused?"当前街区只读":!empty?"已有建筑":water?"水域不能建设":"可建设 · 在下方选择建筑",15,water?Gold:Green);
  TileSection("经济信息");var people=data["people"];
  TileLine(_tileBody,"居住人口 / 已就业",F(people["population"].Double)+" / "+F(people["resident_employed"].Double)+" 人");
  TileLine(_tileBody,"本地岗位 / 已使用",F(people["workplace_jobs"].Double)+" / "+F(people["workplace_jobs_filled"].Double)+" 人");
  if(!data["settlement_available"].Bool)Label(_tileBody,"产出、消耗与通货收支 · 待结算",15,Gold);
  else {
   foreach(var row in data["resources"]){
    Label(_tileBody,Resource(row["resource"].Text)+" · 最近周期折合每月",16,Blue);
    TileLine(_tileBody,"生产 / 生产投入",F(row["production"].Double)+" / "+F(row["building_input"].Double));
    TileLine(_tileBody,"居民消费 / 施工耗材",F(row["resident_consumption"].Double)+" / "+F(row["construction_material"].Double));
    if(row["production_lost"].Double>0)TileLine(_tileBody,"溢出损耗",F(row["production_lost"].Double),Gold);
   }
   if(data["resources"].Count==0)Label(_tileBody,"本格暂无资源产消记录",15,Muted);
   TileLine(_tileBody,"税收来源 / 维护支出",F(data["cash"]["income_attributed"].Double)+" / "+F(data["cash"]["upkeep"].Double)+" 通货/月");
   Fold(_tileBody,"本格收支来源",c=>{
    foreach(var row in data["income_sources"])Label(c,"税收 · "+F(row["rate"].Double)+" 通货/月",15);
    foreach(var row in data["fiscal"])Label(c,"维护 · "+F(row["rate"].Double)+" 通货/月",15);
    foreach(var row in data["receipts"])Label(c,Resource(row.Get("resource","").Text)+" · "+(row["kind"].Text=="output"?"产出":"消耗")+" "+F(row.Get("rate",0).Double)+" /月",15);
    if(data["income_sources"].Count+data["fiscal"].Count+data["receipts"].Count==0)Label(c,"尚无可归属本格的记录",15,Muted);
   });
  }
  TileSection("建筑与 Bonus");
  if(empty)Label(_tileBody,"空地 · 尚无建筑",15,Muted);
  foreach(var building in buildings){
   Label(_tileBody,building["name"].Text+" · T"+building["tier"].Text+" · "+building["operator_name"].Text,16,Green);
   TileLine(_tileBody,"状态",building["paused"].Bool?"已暂停":building["status"].Text=="construction"?"施工中":building["upgrade_job"].Count>0?"升级中":"运行中");
   var job=building["upgrade_job"].Count>0?building["upgrade_job"]:building["status"].Text=="construction"?building["job"]:V.Map();
   if(job.Count>0){Meter(_tileBody,"施工进度",job["total"].Double-job["work"].Double,job["total"].Double," CP");TileLine(_tileBody,"尚需材料",F(job["material"].Double));}
   foreach(var slot in building["slots"]){var bid=building["id"].Text;var sid=slot["slot"].Text;var button=Button(_tileBody,G.slot_label(bid,sid)+" · "+slot["name"].Text,()=>Navigate("slot",bid+"|"+sid));Height((RectTransform)button.transform,36*TextScale);}
   if(building["slots"].Count==0)Label(_tileBody,"暂无 Bonus 槽位",15,Muted);
  }
  TileSection("地块环境与服务");
  foreach(var field in data["fields"].Where(x=>x["layer"].Text!="employment")){
   var kind=field["layer"].Text;bool service=new[]{"medical","education","entertainment","commercial"}.Contains(kind);
   string label=LayerLabel(kind)+(service?(people["population"].Double>0?"满足率":"可达度"):"");
   TileLine(_tileBody,label,field["available"].Bool?field["display"].Text:"待结算");
   Fold(_tileBody,LayerLabel(kind)+"来源",c=>RenderTileSources(c,field));
  }
  TileSection("邻接加成");
  if(data["adjacency"].Count==0)Label(_tileBody,"暂无生效邻接加成",15,Muted);
  foreach(var link in data["adjacency"]){var id=link["source_id"].Text;TileLine(_tileBody,G.project_name(id)+" · "+link["tile"].Text,"+"+F(link["amount"].Double*100)+"%");}
  BuildTileActions(empty,water);
  _renderedTile=SelectedTile;Canvas.ForceUpdateCanvases();_tileScroll.StopMovement();_tileScroll.verticalNormalizedPosition=_tilePositions.TryGetValue(_renderedTile,out var position)?position:1;
 }
 void RenderTileSources(RectTransform parent,V field){
  foreach(var row in field["rows"]){var id=row["source_id"].Text;var sourceTile=row["source_tile"].Text;
   string name=S["projects"].Has(id)?G.project_name(id):row["label"].Text;
   TileLine(parent,name,row["display"].Text);
   if(sourceTile!=""&&sourceTile!=SelectedTile){string target=sourceTile;Button(parent,"定位来源 · "+sourceTile,()=>_inspectionWorld?.FocusInspectionSource(target));}
  }
  if(field["rows"].Count==0)Label(parent,"暂无来源",15,Muted);
 }
 void BuildTileActions(bool empty,bool water){
  var actions=Column(_tileFooter,"Sticky tile actions",5);Stretch(actions);actions.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);
  if(empty){
   var row=Row(actions,"Build on selected tile",66*TextScale);
   foreach(var item in G.c.v040["progression"]["tier1"]){string baseId=item.Text;var tile=SelectedTile;var b=Button(row,G.c.projects[baseId]["name"].Text,()=>OpenConstructionWindow(baseId,tile),false,water||!Runtime.ViewCanEditFocused);Height((RectTransform)b.transform,66*TextScale);ConstructionIllustration(b,baseId);}
   return;
  }
  string id=SelectedBuilding;var p=S["projects"].Get(id,V.Map());if(p.Count==0)return;
  bool construction=p["status"].Text=="construction"||p.Get("upgrade_job",V.Map()).Count>0;
  var first=Row(actions,"Tile primary actions",44*TextScale);
  Button(first,p.Get("paused",false).Bool?(construction?"恢复施工":"恢复运营"):(construction?"暂停施工":"暂停运营"),()=>Command(g=>g.set_paused(id,!p.Get("paused",false).Bool)),false,!Runtime.ViewCanEditFocused);
  Button(first,"升级",()=>OpenUpgradeWindow(id),false,construction||!Runtime.ViewCanEditFocused||Development.choices(G,id).Count==0);
  Button(first,"Bonus",()=>Navigate("building",id),false,!Runtime.ViewCanEditFocused);
  var second=Row(actions,"Tile secondary actions",44*TextScale);
  if(construction){var job=p.Get("upgrade_job",V.Map()).Count>0?p["upgrade_job"]:p["job"];foreach(var value in new[]{1,0,-1}){var priority=value;Button(second,(p.Get("construction_priority",0).Long==priority?"✓ ":"")+(priority==1?"优先":priority==0?"普通":"延后"),()=>Command(g=>Construction.set_priority(g,id,priority)),false,!Runtime.ViewCanEditFocused);}}
  else Button(second,"建筑详情与完整来源",()=>Navigate("building",id));
 }
 static string TileTerrain(string terrain)=>terrain switch {"plain"=>"平原","plains"=>"平原","water"=>"水域","river"=>"河流","hill"=>"丘陵","hills"=>"丘陵","mountain"=>"山地","forest"=>"林地",_=>terrain};
}
}
