using System;
using System.Linq;
namespace MET.Core {
/// <summary>Opt-in deterministic acceptance journey. Uses the same player commands,
/// investments and committed clocks as the city; no entities, stock or technology injected.</summary>
public static class RogueContentWalkthrough {
 public static V LastAttempt;
 sealed class Journey {
  public readonly CitySession city;readonly Action<string> trace;readonly System.Diagnostics.Stopwatch elapsed=System.Diagnostics.Stopwatch.StartNew();long calls,steps;bool civicJourney;V checkpoints=V.Map();public V checks=V.Arr();
  public Journey(Catalog catalog,double utc,Action<string> trace){city=new CitySession(catalog);city.New(20261014,utc,"Rogue循环工艺正常命令验收");this.trace=trace;}
  public void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks.Add(name);trace?.Invoke("PASS "+name);}
  public void Command(string error,string name){if(error!="")throw new InvalidOperationException(name+"："+error+" "+Status()+" safe="+Progress.safe(city.FocusedGame)+" stock="+city.FocusedGame.inventory_total("food")+" metrics="+Population.summary(city.FocusedGame).ToJson());}
  public void Claim() {
   foreach(var g in city.Games.Values) {
    if(!city.CanEditFocused||g!=city.FocusedGame)continue;
    Cycles.acknowledge(g);
    foreach(var evt in Themes.view(g)["events"].Items.ToArray())if(evt["status"].Text=="offered") {
     if(evt["id"].Text=="industrial_byproducts"||evt["id"].Text=="industrial_layout"||evt["id"].Text=="industrial_tech_review"||evt["id"].Text=="civic_shared_space"||evt["id"].Text=="civic_density_review")if(!checkpoints.Has("offered:"+evt["id"].Text))checkpoints["offered:"+evt["id"].Text]=city.snapshot();
     string choice="decline";if(evt["id"].Text=="industrial_byproducts")choice="recovery";if(evt["id"].Text=="industrial_layout")choice="cluster";if(evt["id"].Text=="industrial_tech_review")choice="recover";if(evt["id"].Text=="civic_shared_space")choice="school";if(evt["id"].Text=="civic_density_review")choice="density";
     string error=Themes.choose_event(g,evt["id"].Text,choice);if(error!="")Command(Themes.choose_event(g,evt["id"].Text,"decline"),"事件婉拒");
    }
    while(Bonus.pending(g).Count>0){var reward=Bonus.pending(g)[0];var choice=reward["choices"].Items.OrderByDescending(x=>civicJourney&&x["bonus_id"].Text=="B10"?130:civicJourney&&x["bonus_id"].Text=="O08"?120:x["bonus_id"].Text=="P06"?100:x["bonus_id"].Text=="P09"?90:x["bonus_id"].Text=="R19"?80:0).First();Command(Bonus.choose(g,reward["reward_id"].Text,choice.Get("bonus_id","").Text),"领取实际奖励");}
    foreach(string bonus in civicJourney?new[]{"P06","O08","B10"}:new[]{"P06","P09"}) {
     if(g.s["block"]["bonus_wallet"].Get(bonus,V.Map()).Get("charges",0).Long<1||Bonus.active_count(g,bonus)>0)continue;
     var host=Bonus.card(g,bonus)["suggested_host"];if(host.Get("id","").Text=="")continue;
     string error=Bonus.install(g,bonus,host.Get("type","building").Text,host.Get("id","").Text,host.Get("slot","").Text);if(error=="")Check(true,"real_earned_deployed_"+bonus);
    }
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
  string Status(){LastAttempt=city.snapshot();return city.FocusedBlockId+" month="+city.FocusedGame.s["local_hours"].Text+" money="+city.FocusedGame.s["currency"].Text+" energy="+city.FocusedGame.s["metrics"].Get("energy_ratio",1).Text+" governance="+city.GovernanceView(city.Block(city.FocusedBlockId).Get("district_id","").Text).ToJson()+" theme="+Themes.view(city.FocusedGame).ToJson();}
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
   Command(city.BeginDistrict(0,"industrial","循环工艺正常路线"),"新地区");string source=Origin();Settlement(true);string extra=Build("workshop");Upgrade(extra,"yard");Upgrade(extra,"manufacturing");var g=city.FocusedGame;Command(Themes.select(g,"industrial"),"选择工业主题");
   Until(()=>g.s["block"]["theme"]["stage"].Long>=3,"正常工业主题M1和M2");
   Until(()=>g.inventory_total("material")>=24&&g.inventory_total("goods")>=18,"正常主题交付真实材料制成品");Command(Themes.deliver(g,"material",24),"主题真实材料交付");Command(Themes.deliver(g,"goods",18),"主题真实制成品交付");Until(()=>g.s["block"]["theme"]["completed"].Bool,"正常工业主题完成");Check(true,"real_industrial_theme_completed");
   Until(()=>Bonus.active_count(g,"P06")>0,"正常奖励实际获得并部署P06",1200);Until(()=>g.s["audit"].Get("pollution_recycled",0).Double/Math.Max(1e-9,g.s["audit"].Get("hours",0).Double)>=.2,"真实污染回收供给");
   return Finish(source,"circular_industry","industrial");
  }
  public V RunCivic() {
   civicJourney=true;Command(city.BeginDistrict(0,"services","安居学园正常路线"),"新安居地区");string source=Origin();Settlement(true);string extraFactory=Build("workshop");Upgrade(extraFactory,"yard");Upgrade(extraFactory,"manufacturing");var g=city.FocusedGame;
   string hospital=Build("neighborhood");Upgrade(hospital,"medical_clinic");Upgrade(hospital,"hospital");
   string center=Build("neighborhood");Upgrade(center,"public_center");Upgrade(center,"commercial_center");
   string secondSchool=Build("neighborhood");Upgrade(secondSchool,"primary_school");Upgrade(secondSchool,"secondary_school");
   string garden=Build("neighborhood");Upgrade(garden,"public_center");Upgrade(garden,"garden");
   string extraFarm=Build("homestead");Upgrade(extraFarm,"agriculture");Upgrade(extraFarm,"modern_agriculture");
   foreach(var farm in g.playable_ids().Items.Where(x=>g.s["projects"][x]["def"].Text=="agriculture").Select(x=>x.Text).ToArray())Upgrade(farm,"modern_agriculture");
   foreach(var home in g.playable_ids().Items.Where(x=>g.s["projects"][x]["def"].Text=="residence").Select(x=>x.Text).ToArray())Upgrade(home,"advanced_residence");
   string extraHome=Build("homestead");Upgrade(extraHome,"residence");Upgrade(extraHome,"advanced_residence");
   if(g.s["migration_paused"].Bool)g.toggle_migration();Until(()=>g.s["metrics"]["population"].Double>=96,"安居真实移民达到96");if(!g.s["migration_paused"].Bool)g.toggle_migration();
   Command(Themes.select(g,"civic"),"选择安居主题");Until(()=>g.s["block"]["theme"]["completed"].Bool,"正常安居主题M1-M3",1200);Check(true,"real_civic_theme_completed");Until(()=>Bonus.active_count(g,"B10")>0,"正常奖励实际获得并部署夜校",1600);Until(()=>Themes._metrics(g)["education"].Double>=.75,"真实教育供给达标");return Finish(source,"learning_neighborhood","research");
  }
  V Finish(string source,string profile,string specialization) {var g=city.FocusedGame;
   string fast=city.c.beta["time"]["modes"].Items.OrderByDescending(x=>x["acceleration"].Double).First()["id"].Text;
   int zeroSteps=0;while(g.s["dt"].Double>0){if(zeroSteps++>3000)throw new InvalidOperationException("正常DT耗尽超时");Claim();var result=city.Advance(.1*city.c.beta["units"]["earth_seconds_per_month"].Double,fast);if(result["steps"].Long==0)throw new InvalidOperationException("DT推进失败 "+result["reason"].Text);}
   LastAttempt=city.snapshot();checkpoints["actual_DTzero_before_claim"]=LastAttempt;trace?.Invoke("DTZERO "+g.s["block"]["ending"].ToJson());Check(g.s["block"]["ending"]["build_result"]["candidates"].Any(x=>x["profile_id"].Text==profile),"real_DT0_running_deployed_"+profile);Command(Block.claim_capstone(g),"原街区工业地标");Command(city.ChooseBuildLegacy(profile),"循环工艺成果");Check(city.ChooseBuildLegacy(profile)!="","real_source_chooses_once");
   Until(()=>city.StartupView(false,specialization,source,"build:"+source)["can_pay"].Bool,"真实累积接续启动和规约成本");var quote=city.StartupView(false,specialization,source,"build:"+source)["quote"];var sourceStocks=V.Map();foreach(var resource in quote["stock"].Keys)sourceStocks[resource]=g.inventory_total(resource);double money=g.s["currency"].Double;checkpoints["paid_before_successor"]=city.snapshot();Command(city.BeginBlock(1,specialization,source,"build:"+source),"付费同城继承");string successor=Origin();Check(Math.Abs(money-g.s["currency"].Double-quote["money"].Double)<1e-6,"real_successor_debits_full_quote");foreach(var resource in sourceStocks.Keys)Check(Math.Abs(sourceStocks[resource].Double-g.inventory_total(resource)-quote["stock"][resource].Double)<1e-6,"real_successor_debits_"+resource);Check(g.s["block"]["capstone"].Count>0&&!city.FocusedGame.s["block"].Has("capstone"),"real_landmark_stays_original_block");
   var saved=city.snapshot();var restored=new CitySession(city.c);Command(restored.Restore(saved),"正常新链整城回读");Check(saved.ToJson()==restored.snapshot().ToJson(),"real_rogue_city_roundtrip_no_reissue");
   return V.Map("ok",true,"scope","normal commands, fresh city; no injected resources, completed themes or ending eligibility","checks",checks,"checkpoints",checkpoints,"city",saved,"source_block",source,"profile",profile,"successor_block",successor,"source_evidence",g.s["block"]["ending"]["build_result"],"advance_calls",calls,"committed_slices",steps,"elapsed_seconds",elapsed.Elapsed.TotalSeconds);
  }
 }
 public static V ExecuteCivic(Catalog catalog,double utc=0,Action<string> trace=null)=>new Journey(catalog,utc,trace).RunCivic();
 public static V Execute(Catalog catalog,double utc=0,Action<string> trace=null)=>new Journey(catalog,utc,trace).Run();
}
}
