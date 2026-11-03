using System;
using System.Linq;
namespace MET.Core {
/// <summary>Opt-in deterministic acceptance journey. Uses the same player commands,
/// investments and committed clocks as the city; no entities, stock or technology injected.</summary>
public static class CityArcWalkthrough {
 sealed class Journey {
  public readonly CitySession city;readonly Action<string> trace;readonly System.Diagnostics.Stopwatch elapsed=System.Diagnostics.Stopwatch.StartNew();long calls,steps;public V checks=V.Arr();
  public Journey(Catalog catalog,double utc,Action<string> trace){city=new CitySession(catalog);city.New(20261014,utc,"城市发展弧验收");this.trace=trace;}
  public void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks.Add(name);trace?.Invoke("PASS "+name);}
  public void Command(string error,string name){if(error!="")throw new InvalidOperationException(name+"："+error+" "+Status()+" safe="+Progress.safe(city.FocusedGame)+" stock="+city.FocusedGame.inventory_total("food")+" metrics="+Population.summary(city.FocusedGame).ToJson());}
  public void Claim() {
   foreach(var g in city.Games.Values) {
    if(!city.CanEditFocused||g!=city.FocusedGame)continue;
    Cycles.acknowledge(g);
    while(Bonus.pending(g).Count>0){var reward=Bonus.pending(g)[0];var choice=reward["choices"][0];Command(Bonus.choose(g,reward["reward_id"].Text,choice.Get("bonus_id","").Text),"领取实际奖励");}
    g.s["settings"]["user_paused"]=false;g.s["beta_time"]["pause_reason"]="";
   }
   city.s["paused"]=false;
  }
  public void Step(double months=.25) {
   Claim();var result=city.Advance(months*city.c.beta["units"]["earth_seconds_per_month"].Double,"normal");
   calls++;steps+=result["steps"].Long;
   if(result["steps"].Long==0)throw new InvalidOperationException("实际时间未推进："+result["reason"].Text);
   if(result["reason"].Text!=""&&result["advanced"].Double<months-1e-6&&city.CanEditFocused&&!city.FocusedGame.s["settings"]["user_paused"].Bool)throw new InvalidOperationException("城市结算失败："+result["reason"].Text);
  }
  public void Until(Func<bool> ready,string name,int maximum=600) {
   int steps=0;while(!ready()){if(steps++>=maximum)throw new InvalidOperationException("等待超时："+name+" "+Status());if(steps%100==0)trace?.Invoke("WAIT "+name+" "+Status());Step();}
  }
  string Status()=>city.FocusedBlockId+" month="+city.FocusedGame.s["local_hours"].Text+" money="+city.FocusedGame.s["currency"].Text+" energy="+city.FocusedGame.s["metrics"].Get("energy_ratio",1).Text+" governance="+city.GovernanceView(city.Block(city.FocusedBlockId).Get("district_id","").Text).ToJson();
  void EnsureAuthority() {
   if(city.CanEditFocused)return;string block=city.FocusedBlockId;string district=city.Block(block)["district_id"].Text;
   if(city.ActiveDistrictId!="")throw new InvalidOperationException("焦点街区只有只读权限。");
   Until(()=>Progress.safe(city.Games[block])&&city.Games[block].s["currency"].Double>=90,"真实危机后自治恢复");
   var prior=city.District(district)["run_history"];Check(prior.Count>0&&prior[-1]["kind"].Text=="crisis","real_supply_crisis_retires_and_preserves_district");
   Command(city.ReturnDistrict(district),"自治恢复后付代价回任");Command(city.FocusBlock(block),"回任恢复原焦点");
  }
  public string Origin() {
   var g=city.FocusedGame;var before=g.snapshot();string origin=g.s["origin_choices"].Items.OrderByDescending(id=>g.c.origins[id]["cash"].Double).First().Text;
   Command(g.choose_origin(origin),"起源选择");city.AfterLocalCommand(before);return city.FocusedBlockId;
  }
  public string Build(string node) {
   var g=city.FocusedGame;Until(()=>g.s["currency"].Double>=g.c.projects[node]["cost"].Double,"筹措"+node);
   EnsureAuthority();
   Until(()=>g.s["currency"].Double>=g.c.projects[node]["cost"].Double,"回任后筹措"+node);
   string tile=g.s["cells"].Keys.Where(id=>g.s["cells"][id]["building"].Text==""&&!g.s["cells"][id]["traits"].Contains("tile.water")).OrderBy(id=>Hex.distance("0,1",id)).ThenBy(id=>id,StringComparer.Ordinal).First();
   var before=g.snapshot();Command(g.create_tender(V.Arr(tile),node),"新建投标");Command(g.accept_offer(g.s["draft"]["offers"][0]["id"].Text),"接受投标");city.AfterLocalCommand(before);
   string bid=g.s["cells"][tile]["building"].Text;Until(()=>g.s["projects"][bid]["status"].Text=="running","实际施工"+node);return bid;
  }
  public void Upgrade(string bid,string node) {
   var g=city.FocusedGame;Until(()=>g.s["currency"].Double>=g.c.projects[node]["cost"].Double,"筹措升级"+node);
   EnsureAuthority();
   Until(()=>g.s["currency"].Double>=g.c.projects[node]["cost"].Double,"回任后筹措升级"+node);
   var before=g.snapshot();Command(Development.start_upgrade(g,bid,node),"升级"+node);city.AfterLocalCommand(before);
   Until(()=>g.s["projects"][bid]["def"].Text==node,"施工升级"+node);
  }
  public void Settlement(bool industry) {
   var g=city.FocusedGame;string home=g.playable_ids().Items.First(id=>g.s["projects"][id]["def"].Text=="homestead").Text;
   Build("homestead");string farm=Build("homestead");Upgrade(farm,"agriculture");Upgrade(home,"residence");
   Until(()=>g.s["metrics"]["population"].Double>=46,"居民实际到达");if(!g.s["migration_paused"].Bool)g.toggle_migration();
   if(!industry){string station=Build("workshop");Upgrade(station,"power_station");return;}
   string extraction=Build("workshop");Upgrade(extraction,"extraction");Upgrade(extraction,"material_factory");
   string power=Build("workshop");Upgrade(power,"power_station");Upgrade(power,"power_plant");
   string workshop=g.playable_ids().Items.First(id=>g.s["projects"][id]["def"].Text=="workshop").Text;Upgrade(workshop,"yard");Upgrade(workshop,"manufacturing");Command(g.enable_goods(),"投入真实生活用品供给");
   string freight=Build("workshop");Upgrade(freight,"yard");Upgrade(freight,"freight");Command(Market.order(g,"goods","sell",500,0),"出口真实低级制成品以维持制造吞吐");
   string school=Build("neighborhood");Upgrade(school,"primary_school");Upgrade(school,"secondary_school");
   Until(()=>g.s["knowledge"].Double>0,"教育实际产出知识");
  }
  public void Research(string id) {
   var g=city.FocusedGame;Until(()=>g.s["knowledge"].Double>0&&g.s["currency"].Double>0&&g.inventory_total("material")>0,"研究投入可用");
   EnsureAuthority();
   Command(city.StartResearch(id,city.FocusedBlockId),"启动"+id);
   if(id=="precision_engineering") {
    var schools=g.playable_ids().Items.Where(b=>g.project_stats(b.Text).Get("service.education",0).Double>0).Select(b=>b.Text).ToArray();
    foreach(var school in schools)Command(g.set_paused(school,true),"暂停教育运行");double progress=city.s["science"]["research"][id]["progress"].Double;Step();Check(city.s["science"]["research"][id]["progress"].Double==progress,"research_without_operating_education_does_not_progress");foreach(var school in schools)Command(g.set_paused(school,false),"恢复教育运行");
   }
   Until(()=>city.s["science"]["technologies"].Has(id),"完成真实研究"+id);Check(true,"real_research_"+id);
  }
  public V Run() {
   Command(city.BeginDistrict(0,"industrial","河谷工业地区"),"新地区");string first=Origin();string district1=city.ActiveDistrictId;
   Check(city.StartProject()!="","project_rejects_missing_prerequisites");Check(city.StartResearch("fusion_physics",first)!="","research_rejects_missing_prerequisites");
   var initial=city.snapshot().ToJson();city.ScienceView();city.ProjectView();Check(initial==city.snapshot().ToJson(),"science_project_views_are_read_only");
   Settlement(true);Research("precision_engineering");
   string factory=city.FocusedGame.playable_ids().Items.First(b=>city.FocusedGame.s["projects"][b]["def"].Text=="manufacturing").Text;
   Command(city.FocusedGame.set_paused(factory,true),"暂停制造运行");double advanced=city.FocusedGame.inventory_total("advanced_components");Step();Check(city.FocusedGame.inventory_total("advanced_components")==advanced,"advanced_without_operating_factory_does_not_progress");Command(city.FocusedGame.set_paused(factory,false),"恢复制造运行");
   Research("superconductors");Research("fusion_physics");
   Until(()=>city.FocusedGame.inventory_total("advanced_components")>=10,"真实高级部件生产");Check(city.FocusedGame.inventory_total("goods")!=city.FocusedGame.inventory_total("advanced_components"),"advanced_components_are_independent_stock");
   if(city.FocusedGame.s["metrics"].Get("homeless",0).Double>=.1)Build("homestead");
   if(Progress.food_ratio(city.FocusedGame)<.95){string reserveFarm=Build("homestead");Upgrade(reserveFarm,"agriculture");}
   Until(()=>Progress.safe(city.FocusedGame),"首街区真实稳定");Command(city.StabilizeBlock(first),"首街区稳定");
   Until(()=>city.FocusedGame.s["currency"].Double>=250&&city.FocusedGame.inventory_total("food")>=15&&city.FocusedGame.inventory_total("material")>=15&&city.FocusedGame.inventory_total("goods")>=3,"真实接续启动包");
   double sourceMoney=city.FocusedGame.s["currency"].Double;Command(city.BeginBlock(1,"balanced",first),"同地区新街区");string second=Origin();Check(city.Games[first].s["currency"].Double<sourceMoney,"block_startup_withdraws_real_source");
   if(!city.FocusedGame.s["migration_paused"].Bool)city.FocusedGame.toggle_migration();Until(()=>city.FocusedGame.s["local_hours"].Double>=1,"新街区真实经营");Command(city.StabilizeBlock(second),"第二街区稳定");
   Command(city.HandoverDistrict(),"主动交班");Check(city.District(district1)["status"].Text=="autonomous","district_handover_preserves_autonomy");
   Until(()=>city.Games[first].s["currency"].Double>=240&&city.Games[first].inventory_total("food")>=15&&city.Games[first].inventory_total("material")>=15&&city.Games[first].inventory_total("goods")>=3,"永久新地区的真实启动包");
   Command(city.BeginDistrict(2,"research","高地学园地区"),"新永久地区");string third=Origin();string district2=city.ActiveDistrictId;
   Settlement(false);string school=Build("neighborhood");Upgrade(school,"primary_school");Upgrade(school,"secondary_school");Until(()=>city.FocusedGame.s["knowledge"].Double>0,"新地区知识生产");
   string freight=Build("workshop");Upgrade(freight,"yard");Upgrade(freight,"freight");
   EnsureAuthority();
   Command(city.CreateLink(first,third,"advanced_components",2,.1),"建立跨地区高级部件运输");string link=city.s["links"].Keys.Last();double recipientBefore=city.Games[third].inventory_total("advanced_components");
   Step(.01);Command(city.SetLinkEnabled(link,false),"完成运输计划后停运");Step(.01);
   Check(city.s["links"][link]["delivered"].Double>0&&city.Games[third].inventory_total("advanced_components")>recipientBefore&&city.s["links"][link]["fees"].Double>0,"cross_district_advanced_stock_arrives_with_real_freight_cost");
   Until(()=>city.FocusedGame.s["currency"].Double>=200,"学园工程与本地施工投资");Command(Market.order(city.FocusedGame,"material","buy",30,20),"实际买入工程与本地施工材料");Until(()=>city.FocusedGame.inventory_total("material")>=20&&city.FocusedGame.s["currency"].Double>=75,"材料订单实际到货并保留本地施工经费");
   Command(city.StartProject(),"启动托卡马克之冠");double beforeMaterial=city.FocusedGame.inventory_total("material");Command(city.ContributeProject(third,"material",10),"第二地区工程材料交付");Check(Math.Abs(beforeMaterial-city.FocusedGame.inventory_total("material")-10)<1e-6,"project_delivery_withdraws_real_stock");
   var target=city.FocusedGame;string queueTile=target.s["cells"].Keys.First(id=>target.s["cells"][id]["building"].Text==""&&!target.s["cells"][id]["traits"].Contains("tile.water"));
   var queueBefore=target.snapshot();Command(target.create_tender(V.Arr(queueTile),"homestead"),"同时安排本地施工");Command(target.accept_offer(target.s["draft"]["offers"][0]["id"].Text),"支付本地施工");city.AfterLocalCommand(queueBefore);
   var noDelivery=city.ProjectView()["project"]["work"].Double;Step();Check(city.ProjectView()["project"]["work"].Double==noDelivery,"project_no_advanced_delivery_no_progress");
   double transported=city.Games[third].inventory_total("advanced_components");Command(city.ContributeProject(third,"advanced_components",Math.Min(10,transported)),"学园交付实际到货高级部件");
   Check(city.Games.Values.All(g=>g.s["block"].Get("construction_last",V.Map()).Get("cp",0).Double+g.s["block"].Get("city_cp_last",V.Map()).Get("cp",0).Double<=g.s["block"].Get("city_cp_last",V.Map()).Get("available",0).Double+1e-8),"city_cp_reserve_cannot_double_spend_local_queue");
   Check(target.s["block"]["construction_last"]["cp"].Double>0&&target.s["block"]["city_cp_last"]["cp"].Double>0,"city_and_local_construction_share_actual_capacity");
   Command(city.StabilizeBlock(third),"学园街区稳定");Until(()=>city.FocusedGame.s["currency"].Double>=220&&city.FocusedGame.inventory_total("food")>=15&&city.FocusedGame.inventory_total("material")>=15&&city.FocusedGame.inventory_total("goods")>=3,"学园接续供给");
   Command(city.BeginBlock(0,"balanced",third),"学园第二街区");string fourth=Origin();if(!city.FocusedGame.s["migration_paused"].Bool)city.FocusedGame.toggle_migration();Until(()=>city.FocusedGame.s["local_hours"].Double>=1,"学园第二街区经营");Command(city.StabilizeBlock(fourth),"学园第二街区稳定");Command(city.HandoverDistrict(),"学园交班");
   double returnMoney=city.Games[first].s["currency"].Double;Command(city.ReturnDistrict(district1),"付代价回任");Command(city.FocusBlock(first),"返回工业街区");Check(city.Games[first].s["currency"].Double<returnMoney,"return_pays_actual_organization_cost");
   Until(()=>city.Games[first].inventory_total("material")>=110,"工程材料实际积累");Command(city.ContributeProject(first,"material",110),"工业地区材料交付");
   while(city.ProjectView()["project"]["delivered"]["advanced_components"].Double<30-1e-8) {
    Until(()=>city.Games[first].inventory_total("advanced_components")>=1,"高级部件真实产出");double quantity=Math.Min(city.Games[first].inventory_total("advanced_components"),30-city.ProjectView()["project"]["delivered"]["advanced_components"].Double);
    Command(city.ContributeProject(first,"advanced_components",quantity),"高级部件真实交付");
   }
   Until(()=>city.ProjectView()["project"]["status"].Text=="complete","全城实际施工");Check(city.ProjectView()["endless"].Bool,"same_city_endless_after_tokamak");
   double made=city.s["science"]["production_totals"].Values.Sum(row=>row["advanced_components"].Double);double inventory=city.Games.Values.Sum(g=>g.inventory_total("advanced_components"));double inTransit=0;
   foreach(var route in city.s["links"].Values)if(route["resource"].Text=="advanced_components"){inTransit+=route["buffer"].Double;foreach(var cargo in route["inflight"])inTransit+=cargo["amount"].Double;}
   Check(Math.Abs(made-inventory-inTransit-city.ProjectView()["project"]["consumed"]["advanced_components"].Double)<1e-6,"advanced_production_stock_transport_project_are_conserved");
   Check(city.s["science"]["production_totals"].Values.All(row=>Math.Abs(row["material"].Double-row["advanced_components"].Double*2)<1e-6&&Math.Abs(row["goods"].Double-row["advanced_components"].Double)<1e-6&&Math.Abs(row["currency"].Double-row["advanced_components"].Double*3)<1e-6&&Math.Abs(row["energy"].Double-row["advanced_components"].Double*2)<1e-6),"advanced_actual_material_goods_money_energy_costs");
   int completions=city.s["history"].Count(row=>row["kind"].Text=="city_project_complete");string identity=city.s["city_id"].Text;double operated=city.Games[first].s["local_hours"].Double;Step();
   Check(city.s["city_id"].Text==identity&&city.Games[first].s["local_hours"].Double>operated,"completed_city_keeps_operating");Check(city.s["history"].Count(row=>row["kind"].Text=="city_project_complete")==completions&&completions==1,"project_feedback_completes_once");
   string beforeRepeat=city.snapshot().ToJson();city.ScienceTick(V.Map(first,.25),0);Check(city.snapshot().ToJson()==beforeRepeat,"repeated_science_callback_cannot_mint_or_spend_twice");
   var saved=city.snapshot();var restored=new CitySession(city.c);Command(restored.Restore(saved),"整城回读");Check(restored.snapshot().ToJson()==saved.ToJson(),"whole_city_science_project_roundtrip");
   return V.Map("ok",true,"checks",checks,"city",saved,"districts",city.s["districts"].Count,"blocks",city.Games.Count,"external_seconds",city.s["external_seconds"],"elapsed_seconds",elapsed.Elapsed.TotalSeconds,"advance_calls",calls,"committed_slices",steps,"note","实际玩家命令与经济结算的逻辑时间验收；不是数月真人游玩或原生引擎验收");
  }
 }
 public static V Execute(Catalog catalog,double utc=0,Action<string> trace=null)=>new Journey(catalog,utc,trace).Run();
}
}
