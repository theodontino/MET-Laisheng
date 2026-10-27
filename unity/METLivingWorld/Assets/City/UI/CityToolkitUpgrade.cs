using System;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MET.City {
public sealed partial class CityInterface {
 UpgradeToolkitView _toolkitUpgrade;
 string _toolkitBuilding="",_toolkitChoice="",_toolkitVersion="",_toolkitError="";
 bool _toolkitSubmitting;
 GameObject _toolkitPreviousFocus;
 public bool ToolkitUpgradeOpen=>WildStarOpen||(_toolkitUpgrade!=null&&_toolkitUpgrade.IsOpen);
 public UpgradeToolkitView ToolkitUpgradeView=>_toolkitUpgrade;
 public bool ToolkitUpgradeEnabled=>Runtime.DisplaySettings.Get("toolkit_upgrade",false).Bool;
 public void SetToolkitUpgradeEnabled(bool enabled){Runtime.SetDisplaySetting("toolkit_upgrade",enabled);}

 public void OpenToolkitUpgrade(string id){
  if(!S["projects"].Has(id)||ConstructionWindowOpen||_confirm!=null)return;
  Runtime.Flush();CancelDeskTipsForPanel();DismissMapInspection();EndConstructionIntent();
  _toolkitPreviousFocus=EventSystem.current?.currentSelectedGameObject;
  EventSystem.current?.SetSelectedGameObject(null);
  _toolkitBuilding=id;_toolkitChoice="";_toolkitError="";
  SelectedBuilding=id;SelectedTile=S["projects"][id]["tile"].Text;
  Runtime.SetDecisionOpen(true);
  if(_toolkitUpgrade==null)_toolkitUpgrade=gameObject.AddComponent<UpgradeToolkitView>();
  ApplyToolkitDisplay();
  _toolkitUpgrade.Show(ToolkitUpgradeModel(),choice=>{_toolkitChoice=choice;_toolkitError="";RefreshToolkitUpgrade();},SubmitToolkitUpgrade,CloseToolkitUpgrade);
  RefreshModalOwnership();RefreshHudGeometry();SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);
 }
 public void CloseToolkitUpgrade(){
  if(WildStarOpen){CloseWildStar();return;}
  if(!ToolkitUpgradeOpen)return;
  _toolkitUpgrade.Hide();_toolkitSubmitting=false;
  Runtime.SetDecisionOpen(_archiveOpen||ObjectDrawerOpen||ConstructionWindowOpen);
  RefreshObjectDrawer();RefreshContext();RefreshModalOwnership(true);RefreshHudGeometry();
  if(_toolkitPreviousFocus!=null&&_toolkitPreviousFocus.activeInHierarchy)EventSystem.current?.SetSelectedGameObject(_toolkitPreviousFocus);
  _toolkitPreviousFocus=null;SelectionChanged?.Invoke(SelectedTile,SelectedBuilding);
 }
 void ApplyToolkitDisplay(){if(_toolkitUpgrade!=null)_toolkitUpgrade.ApplyDisplay((float)Runtime.DisplaySettings.Get("ui_scale",1).Double,TextScale,Runtime.DisplaySettings.Get("high_contrast",false).Bool);}
 void RefreshToolkitUpgrade(){if(WildStarOpen){RefreshWildStar();return;}if(ToolkitUpgradeOpen&&!_toolkitSubmitting){ApplyToolkitDisplay();_toolkitUpgrade.Refresh(ToolkitUpgradeModel());}}
 void SubmitToolkitUpgrade(){
  if(!ToolkitUpgradeOpen||_toolkitSubmitting)return;
  var id=_toolkitBuilding;var choice=_toolkitChoice;var expected=_toolkitVersion;
  _toolkitSubmitting=true;
  var error=Runtime.Execute(g=>_constructionPlanner.version(G)!=expected?"城市已变化，请查看刷新后的方案再确认。":Development.start_upgrade(g,id,choice));
  _toolkitSubmitting=false;
  if(error!=""){_toolkitError=error;RefreshToolkitUpgrade();return;}
  CloseToolkitUpgrade();ShowMessage(G.project_name(id)+"已开始升级。施工进度可在地块或施工队列查看。");
 }
 UpgradeWindowModel ToolkitUpgradeModel(){
  var model=new UpgradeWindowModel();
  var id=_toolkitBuilding;var p=S["projects"].Get(id,V.Map());
  model.Title="升级 · "+G.project_name(id);model.Subtitle="地块 "+p.Get("tile","").Text+" · 现有建筑升级规则";model.CurrentTier="T"+p.Get("tier",0).Text;
  if(p.Count==0){model.DisabledReason="该建筑已不可用，请返回地图。";return model;}
  var choices=Development.choices(G,id).Select(x=>x.Text).ToArray();
  if(!choices.Contains(_toolkitChoice))_toolkitChoice=choices.FirstOrDefault()??"";
  model.SelectedId=_toolkitChoice;
  foreach(var node in choices){
   var v=Development.preview(G,id,node);
   var changes=v["gains"].Concat(v["losses"]).OrderBy(x=>ConstructionTraitPriority(x["stat"].Text)).Take(3)
    .Select(x=>Stat(x["stat"].Text)+" "+Signed(x["delta"].Double));
   model.Candidates.Add(new UpgradeCandidateModel{Id=node,Title=v["name"].Text,Tier="T"+(p["tier"].Long+1),Traits=string.Join("\n",changes),Slots="新增 "+Catalog.SLOT_NAMES.Get(v["slot"],v["slot"]).Text+"槽",Cost=F(v["cost"].Double,0)+" 通货 · "+F(v["material"].Double,0)+" 材料 · "+F(v["construction_required"].Double)+" CP"});
  }
  if(choices.Length==0){model.DisabledReason="当前没有后续升级方向。";return model;}
  var view=Development.preview(G,id,_toolkitChoice);var baseline=_constructionPlanner.snapshot(G);var proposal=ConstructionUpgradeForecast(id,_toolkitChoice,view);
  _toolkitVersion=_constructionPlanner.version(G);
  var cash=view["cost"].Double;var material=view["material"].Double;
  var queued=Construction.queue(G).Sum(job=>job["material"].Double);var stock=G.inventory_total("material");
  model.SelectedTitle=view["name"].Text;
  model.SelectedSummary="两边均结算下一步，假定改造已完成。施工期间原建筑运营系数 ×"+F(view["operation_factor"].Double,2)+"；既有库存与历史保留。";
  model.Cost=F(cash,0)+" 通货";model.BalanceAfter=F(S["currency"].Double-cash,0)+" 通货";
  model.MaterialConstraint="材料 "+F(stock)+" / 本次 "+F(material)+"\n已有队列还需 "+F(queued)+"；按施工进度消耗";
  var capacity=Construction.capacity_breakdown(G);
  model.PowerConstraint="本次施工 "+F(view["construction_required"].Double)+" CP · 本区 "+F(capacity["local_effective"].Double)+" CP/月\n电力供 / 需："+(S.Get("simulation_tick",0).Long>0?F(S["metrics"].Get("energy",0).Double)+" / "+F(S["metrics"].Get("energy_need",0).Double):"待结算");
  model.DisabledReason=!Runtime.ViewCanEditFocused?"当前为只读街区，不能启动改造。":p["status"].Text!="running"||p["upgrade_job"].Count!=0?"请先完成当前施工。":S["currency"].Double<cash?"通货不足，还差 "+F(cash-S["currency"].Double,0)+"。":proposal.Get("error","").Text;
  if(_toolkitError!="")model.DisabledReason=_toolkitError;
  model.CanConfirm=model.DisabledReason=="";
  if(!proposal.Get("ok",false).Bool)return model;
  var old=baseline["metrics"];var now=proposal["map_snapshot"]["metrics"];
  Action<string,double,double,string,bool> add=(label,a,b,unit,lower)=>{
   var row=new UpgradeMetricRow{Label=label,Baseline=F(a),Predicted=F(b),Delta=Signed(b-a),Unit=unit,Warning=lower?b>a:b<a};
   model.CityRows.Add(row);if(Math.Abs(b-a)>.001)model.PrimaryRows.Add(row);
  };
  add("住房容量",old.Get("housing",0).Double,now.Get("housing",0).Double,"人",false);
  foreach(var r in new[]{"food","material","goods"})add(Resource(r)+"净流",old["flows"].Get(r,0).Double,now["flows"].Get(r,0).Double,"/月",false);
  foreach(var kind in G.c.balance["service_kinds"])add(Service(kind.Text)+"实际覆盖",ConstructionServicePeople(old,kind.Text),ConstructionServicePeople(now,kind.Text),"人",false);
  add("经营净额",old.Get("cashflow",0).Double,now.Get("cashflow",0).Double,"通货/月",false);
  add("供电能力",old.Get("energy",0).Double,now.Get("energy",0).Double,"容量",false);
  add("用电需求",old.Get("energy_need",0).Double,now.Get("energy_need",0).Double,"容量",true);
  add("平均污染场强",ConstructionPollution(baseline),ConstructionPollution(proposal["map_snapshot"]),"场强",true);
  return model;
 }
}
}
