using System;
using System.Linq;
using MET.Core;
class UI15Program {
 static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static Game Fresh(Catalog catalog){
  var game=new Game(catalog);game.new_game(8291,1000000);
  Check(game.select_block(0)==""&&game.choose_origin(game.s["origin_choices"][0].Text)=="","real city opens from deterministic catalogue fixture");
  game.s["block"]["reward_choices"]=V.Arr();game.s["block"]["bonus_wallet"]=V.Map();game.s["block"]["blueprint_levels"]=V.Map();
  return game;
 }
 static string Workshop(Game game){
  var tile=Rules.ids(game.s["cells"]).First(x=>game.s["cells"][x].Get("building","").Text==""&&!game.s["cells"][x]["traits"].Contains("tile.water")).Text;
  var project=game.create_building("workshop",tile,"commons",true);project["slots"]=V.Map("preview:production","");project["slot_types"]=V.Map("preview:production","Production");game.refresh_metrics();return project["id"].Text;
 }
 static void ZeroCopyCatalogue(Catalog catalog){
  var game=Fresh(catalog);var host=Workshop(game);var saved=game.s.ToJson(false,true);var compiled=game.compiled.ToJson(false,true);var rng=game.s["rng"].Duplicate(true);
  var inspection=BlueprintPreview.Inspect(game,"P01","building",host,"preview:production");
  Check(inspection["ok"].Bool&&inspection["inspection_only"].Bool,"unowned catalogue card produces an inspection");
  var goods=inspection["stat_rows"].FirstOrDefault(row=>row["field"].Text=="output.goods");
  Check(goods!=null&&goods["after"].Double>goods["before"].Double,"inspection applies actual P01 output rules to detached target");
  Check(inspection["same_step"]["source_phase"].Text=="next_tick"&&inspection["baseline"].Count>0&&inspection["proposal"].Count>0,"inspection contains real same-step baseline and proposal");
  Check(inspection["before_copies"].Long==0&&inspection["after_copies"].Long==0,"inspection reports zero copies before and after");
  Check(inspection["baseline"]["block"]["bonus_wallet"].Count==0&&inspection["proposal"]["block"]["bonus_wallet"].Count==0,"both returned forecasts retain empty actual wallets");
  Check(inspection["after_instance_records"].Count==1&&inspection["after_instance_records"][0]["bonus_id"].Text=="P01","hypothetical installed instance supplies real rule provenance");
  var commandForecast=BlueprintPreview.For(game,"P01","building",host,"preview:production");
  Check(!commandForecast["ok"].Bool&&commandForecast["error"].Text=="没有可用的部署许可。","owned command forecast still rejects zero-copy install");
  Check(game.s.ToJson(false,true)==saved&&game.compiled.ToJson(false,true)==compiled,"catalogue and rejected command forecasts leave all original state and compiled rules unchanged");
  Check(game.s["rng"]==rng&&game.s["block"]["bonus_wallet"].Count==0,"catalogue forecasts preserve original RNG and empty inventory");
 }
 static void Replacement(Catalog catalog){
  var game=Fresh(catalog);var host=Workshop(game);var slot="preview:production";
  Check(Bonus.grant(game,"P01","ui15:real-copy")==""&&Bonus.install(game,"P01","building",host,slot)=="","replacement fixture consumes a real existing P01 copy");
  var original=game.s["projects"][host]["slots"][slot].Text;
  var candidate=catalog.bonuses.Keys.OrderBy(id=>id,StringComparer.Ordinal).First(id=>id!="P01"&&Bonus.card(game,id)["scope"].Text=="building"&&game.bonus_eligible(id,"building",host,slot,original)=="");
  var saved=game.s.ToJson(false,true);var rng=game.s["rng"].Duplicate(true);
  var inspection=BlueprintPreview.Inspect(game,candidate,"building",host,slot);
  Check(inspection["ok"].Bool,"compatible zero-copy replacement inspection succeeds: "+candidate);
  var installed=inspection["after_instance_records"][0];
  Check(installed["bonus_id"].Text==candidate&&installed["id"].Text!=original&&inspection["proposal"]["projects"][host]["slots"][slot].Text==installed["id"].Text,"replacement proposal actually replaces occupied slot with candidate instance");
  Check(inspection["before_copies"].Long==0&&inspection["after_copies"].Long==0&&!inspection["proposal"]["block"]["bonus_wallet"].Has(candidate),"replacement preview fabricates no candidate wallet or copies");
  var invalid=BlueprintPreview.Inspect(game,"P07","building",host,slot);
  Check(!invalid["ok"].Bool&&invalid["error"].Text=="基础建筑类型不适用。","replacement still rejects agricultural card on industrial target");
  Check(game.s.ToJson(false,true)==saved&&game.s["rng"]==rng&&game.s["bonus_instances"][original]["active"].Bool&&game.s["projects"][host]["slots"][slot].Text==original,"valid and rejected replacements preserve live installed source, state and RNG");
 }
 static void Main(string[] args){var catalog=new Catalog(args[0]);ZeroCopyCatalogue(catalog);Replacement(catalog);Console.WriteLine("UI15 CORE PASS "+checks+" assertions; no Player visual/input acceptance performed.");}
}
