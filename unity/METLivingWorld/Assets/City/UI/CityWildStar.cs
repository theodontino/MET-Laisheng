using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MET.City {
public sealed partial class CityInterface {
 WildStarView _wildStar;
 string _wildBuilding="",_wildNode="compound",_wildError="";
 bool _wildSubmitting;
 GameObject _wildPreviousFocus;
 public WildStarView WildStarWindow=>_wildStar;
 public bool WildStarOpen=>_wildStar!=null&&_wildStar.IsOpen;
 bool CanStartWildStar(string id)=>!WildDesign.Enabled(G)&&S["projects"].Has(id)&&G.c.wild["forms"].Has(S["projects"][id].Get("def",""));
 void OfferWildStar(string id){
  Confirm("启用建筑星图？","为当前街区启用 Wild 构筑：保留现有建筑与城市记录，为农居建立设计档案，并发放一次开局低级工业品。基础施工能力改为每月 10 CP；新节点通过实际施工生效。",()=>{
   var error=Runtime.ExecuteWorld(city=>city.EnableWild());
   if(error!=""){ShowMessage(error);return;}
   _civicTileKey="";RefreshContext();OpenWildStar(id);
  });
 }
 public void OpenWildStar(string id) {
  if(!S["projects"].Has(id)||!WildDesign.IsFarm(G,S["projects"][id])||ConstructionWindowOpen||_confirm!=null)return;
  Runtime.Flush();CancelDeskTipsForPanel();DismissMapInspection();EndConstructionIntent();
  _wildPreviousFocus=EventSystem.current?.currentSelectedGameObject;
  EventSystem.current?.SetSelectedGameObject(null);
  if(_wildBuilding!=id)_wildNode="compound";
  _wildBuilding=id;_wildError="";SelectedBuilding=id;SelectedTile=S["projects"][id]["tile"].Text;
  Runtime.SetDecisionOpen(true);
  if(_wildStar==null)_wildStar=gameObject.AddComponent<WildStarView>();
  _wildStar.ApplyDisplay((float)Runtime.DisplaySettings.Get("ui_scale",1).Double,TextScale,Runtime.DisplaySettings.Get("high_contrast",false).Bool);
  _wildStar.Show(WildStarModel(),node=>{_wildNode=node;_wildError="";RefreshWildStar();},()=>SubmitWildNode(false),()=>SubmitWildNode(true),CloseWildStar);
  RefreshModalOwnership();RefreshHudGeometry();SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);
 }
 void CloseWildStar() {
  if(!WildStarOpen)return;
  _wildStar.Hide();_wildSubmitting=false;
  Runtime.SetDecisionOpen(_archiveOpen||ObjectDrawerOpen||ConstructionWindowOpen);
  RefreshObjectDrawer();RefreshContext();RefreshModalOwnership(true);RefreshHudGeometry();
  if(_wildPreviousFocus!=null&&_wildPreviousFocus.activeInHierarchy)EventSystem.current?.SetSelectedGameObject(_wildPreviousFocus);
  _wildPreviousFocus=null;SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);
 }
 void RefreshWildStar() {
  if(!WildStarOpen||_wildSubmitting)return;
  _wildStar.ApplyDisplay((float)Runtime.DisplaySettings.Get("ui_scale",1).Double,TextScale,Runtime.DisplaySettings.Get("high_contrast",false).Bool);
  _wildStar.Refresh(WildStarModel());
 }
 void SubmitWildNode(bool remove) {
  _wildSubmitting=true;var id=_wildBuilding;var node=_wildNode;
  var error=Runtime.Execute(g=>remove?WildDesign.Cancel(g,id,node):WildDesign.Start(g,id,node));
  _wildSubmitting=false;
  if(error!=""){_wildError=error;RefreshWildStar();return;}
  if(remove){_wildError="";RefreshWildStar();ShowMessage("构筑已调整；设计点已返还，资源不返还。");}
  else {CloseWildStar();ShowMessage("节点已开工；回到地图继续时间，施工队会自动推进。");}
 }
 string WildNodeName(string id)=>G.c.wild["nodes"].Get(id,V.Map()).Get("name",id).Text;
 string WildNodeEffect(string id) {
  switch(id){
   case "compound":return "住房容量 +10%\n粮食基础产出 +10%";
   case "housing":return "住房容量再 +10%\n沿这条路线可进阶公寓";
   case "production":return "粮食基础产出再 +10%\n沿这条路线可进阶农场";
   case "organization":return "新增组织专用槽\n槽位完工后可承载组织 Bonus";
   case "link":return "新增联系专用槽\n完工后可沿图中连线衔接后续形态";
   case "fusion":return "新增融合专用槽\n用于跨建筑类型装配";
   case "apartment":return "住房基数 10 → 20\n粮食基数 10 → 5 /月\n继承已完成节点；设计容量 +2";
   case "agriculture":return "住房基数 10 → 5\n粮食基数 10 → 20 /月\n继承已完成节点；设计容量 +2";
   default:return "";
  }
 }
 WildStarWindowModel WildStarModel() {
  var view=WildDesign.View(G,_wildBuilding);var model=new WildStarWindowModel();
  if(!view["ok"].Bool){model.Title="建筑星图";model.Constraint=view["error"].Text;return model;}
  var p=S["projects"][_wildBuilding];var capacity=view["capacity"];
  model.Title=view["name"].Text+" · 建筑星图";
  model.Subtitle="地块 "+p["tile"].Text+"  /  T"+view["tier"].Long+"  /  住房与农业";
  model.CapacityUsed=(float)capacity["used"].Double;model.CapacityTotal=(float)capacity["total"].Double;
  model.StockText="材料 "+F(G.inventory_total("material"))+"   基础制成品 "+F(G.inventory_total("goods"))+"   低级工业品 "+F(S["wild"]["stock"]["low_industrial"].Double,0);
  foreach(var row in view["nodes"]) {
   string state=row["state"].Text;bool complete=state=="complete",pending=state=="construction";
   model.Nodes.Add(new WildStarNodeModel{Id=row["node"].Text,Title=row["name"].Text,Subtitle=row["kind"].Text=="progression"?"T2 · 新形态":row["kind"].Text=="slot"?"专用槽位":"能力改良",State=complete?"completed":pending?"pending":row["ok"].Bool?"available":"locked",StatusText=complete?"已完成":pending?"施工中":row["ok"].Bool?F(row["dp"].Double,0)+" 设计点":"未解锁"});
  }
  foreach(var route in view["routes"])model.Routes.Add(new WildStarRouteModel{From=route["from"].Text,To=route["to"].Text,Bypass=route["bypass"].Bool});
  var quote=WildDesign.Quote(G,_wildBuilding,_wildNode);var def=G.c.wild["nodes"][_wildNode];
  string nodeState=quote["state"].Text;bool done=nodeState=="complete",building=nodeState=="construction";
  model.SelectedId=_wildNode;model.SelectedTitle=WildNodeName(_wildNode);model.Description=WildNodeEffect(_wildNode);
  model.DpCost=F(done||building?p["wild_design"]["nodes"][_wildNode]["dp"].Double:quote["dp"].Double,0)+" 点";
  model.DpFree=F(capacity["free"].Double,0)+" 点可用";
  model.CpCost=done?"已完成":building?F(view["job"]["total"].Double-view["job"]["work"].Double)+" / "+F(view["job"]["total"].Double)+" CP":F(quote["cp"].Double)+" CP";
  if(!quote.Get("pricing_known",true).Bool){model.ResourceCost="后续节点未开放";model.DpCost="—";model.CpCost="—";model.Costs="T2 保留历史节点；后续星图待定义。";}
  else if(!done&&!building){
   model.ResourceCost=quote["material"].Double>0?F(quote["material"].Double,0)+" 材料":quote["goods"].Double>0?F(quote["goods"].Double,0)+" 基础制成品":F(quote["low_industrial"].Double,0)+" 低级工业品";
   double available=quote["material"].Double>0?G.inventory_total("material"):quote["goods"].Double>0?G.inventory_total("goods"):S["wild"]["stock"]["low_industrial"].Double;
   model.ResourceAvailable="库存 "+F(available);
   model.Costs=quote["material"].Double>0?"材料随施工领用；缺料会等待。":"物资在开工时支付；完工后生效。";
  }else model.Costs=done?"效果已经生效，无需再次支付。":"设计点已预留；效果将在施工完成后生效。";
  var predecessors=quote["routes_in"].Select(x=>WildNodeName(x.Text)).ToArray();
  model.Prerequisites=predecessors.Length==0?"从这里开始，选择建筑的发展方向。":"沿连线进入："+string.Join(" / ",predecessors)+"。完成其中一项即可。";
  var cancel=WildDesign.CancelPreview(G,_wildBuilding,_wildNode);
  model.CanRemove=Runtime.ViewCanEditFocused&&cancel["ok"].Bool;
  model.RemovalDescription=cancel["ok"].Bool?"一并取消："+string.Join("、",cancel["removed"].Select(x=>WildNodeName(x.Text)))+"。\n返还 "+F(cancel["refund_dp"].Double,0)+" 点；已用资源不返还。相关 Bonus 退回库存。":cancel["error"].Text;
  model.CanConfirm=Runtime.ViewCanEditFocused&&quote["ok"].Bool;
  model.ConfirmText=done?"已完成":building?"施工中":"确认开工";
  model.Constraint=_wildError!=""?_wildError:!Runtime.ViewCanEditFocused?"当前街区只读。":done?(def["kind"].Text=="progression"?"已完成的 Tier 进阶不可撤销。":""):building?"关闭窗口后继续时间，施工按实际 CP 推进。":quote["error"].Text;
  model.StatusText="基础 "+F(capacity["base"].Double,0)+"  +  科技 "+F(capacity["technology"].Double,0)+"  +  历代成长 "+F(capacity["inherited"].Double,0);
  return model;
 }
}
}
