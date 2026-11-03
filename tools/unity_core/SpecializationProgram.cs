using System;
using System.IO;
using System.Linq;
using MET.Core;
class SpecializationProgram {
 static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static bool Near(double a,double b)=>Math.Abs(a-b)<1e-8;
 static Game Fresh(Catalog c){var g=new Game(c);g.new_game(8291,1000);g.select_block(0);g.choose_origin(g.s["origin_choices"][0].Text);g.s["currency"]=10000;g.s["projects"]["municipal"]["inventory"]["material"]=1000;return g;}
 static void Upgrade(Game g,string id,string target){Check(Development.start_upgrade(g,id,target)=="","start real upgrade to "+target);Construction.tick(g,100);g.refresh_metrics();Check(g.s["projects"][id]["def"]==target&&g.s["projects"][id]["upgrade_job"].Count==0,"construction completes "+target);}
 static void Branch(Catalog c,string root,string middle,string terminal,params string[] resources){
  var g=Fresh(c);string tile=g.s["cells"].Keys.First(x=>g.s["cells"][x]["building"]==""&&!g.s["cells"][x]["traits"].Contains("tile.water"));var p=g.create_building(root,tile,"commons",true);string id=p["id"].Text;g.refresh_metrics();
  var preview=Development.preview(g,id,middle);
  foreach(string key in resources){var loss=preview["losses"].First(x=>x["stat"]==key);Check(Near(loss["after"].Double,c.projects[root]["stats"][key].Double*.5),middle+" preview retains half "+key);}
  Upgrade(g,id,middle);
  foreach(string key in resources)Check(g.compiled["stats"][id][key].Double>0,middle+" compiled side capacity remains active "+key);
  var access=Fields.access(g,g.compiled);
  foreach(string key in resources.Where(x=>x.StartsWith("service.",StringComparison.Ordinal)))Check(access["fields"].Has("F:"+id+":"+key.Substring(8)+":base"),middle+" retains source-aware "+key+" access");
  var usage=V.Map();foreach(var pid in g.s["projects"].Keys)usage[pid]=V.Map("utilization",1);var service=Alloc.services(g,g.compiled,access,usage);
  foreach(string key in resources.Where(x=>x.StartsWith("service.",StringComparison.Ordinal)))Check(service["pools"][id+"|"+key.Substring(8)]["capacity"].Double>0,middle+" side service enters allocation pool "+key);
  Upgrade(g,id,terminal);
  foreach(string key in resources)Check(Near(g.compiled["stats"][id][key].Double,0),terminal+" fully specializes "+key);
  Check(p["history"].Count==2&&p["tier"].Long==3,terminal+" retains actual upgrade lineage");
 }
 static void Main(string[] args){
  var c=new Catalog(args[0]);
  Check(File.ReadAllText(Path.Combine(args[0],"040/buildings.json"))==File.ReadAllText(Path.Combine(args[1],"040/buildings.json")),"Unity bundled building catalog matches source");
  Branch(c,"homestead","residence","advanced_residence","output.food");
  Branch(c,"homestead","agriculture","modern_agriculture","housing");
  Branch(c,"neighborhood","public_center","commercial_center","service.medical","service.education");
  Branch(c,"neighborhood","public_center","garden","service.medical","service.education");
  Branch(c,"neighborhood","medical_clinic","hospital","service.education","service.entertainment","service.commercial");
  Branch(c,"neighborhood","primary_school","secondary_school","service.medical","service.entertainment","service.commercial");
  Branch(c,"workshop","power_station","power_plant","output.material","output.goods");
  Branch(c,"workshop","extraction","material_factory","output.goods","output.energy");
  Branch(c,"workshop","yard","manufacturing","output.energy");
  Branch(c,"workshop","yard","freight","output.goods","output.energy");
  Check(Near(c.projects["yard"]["stats"]["output.goods"].Double,.3),"yard T2 retains half secondary goods before manufacturing becomes primary");
  var housed=Fresh(c);string home=housed.playable_ids().First(x=>housed.s["projects"][x]["def"]=="homestead").Text;Upgrade(housed,home,"agriculture");Population.rehouse(housed);Check(Near(housed.s["groups"].Values.Where(x=>x["host"]==home).Sum(x=>x["count"].Double),12),"agriculture retains twelve actual residents after rehouse");Upgrade(housed,home,"modern_agriculture");Population.rehouse(housed);Check(!housed.s["groups"].Values.Any(x=>x["host"]==home),"modern agriculture relocates residents when housing reaches zero");
  foreach(var row in new[]{new[]{"residence","housing","40"},new[]{"agriculture","output.food","8"},new[]{"power_station","output.energy","16"},new[]{"extraction","output.material","7"},new[]{"yard","output.material","2"},new[]{"manufacturing","output.goods","8"}})Check(Near(c.projects[row[0]]["stats"][row[1]].Double,double.Parse(row[2],System.Globalization.CultureInfo.InvariantCulture)),row[0]+" main capacity unchanged");
  foreach(var row in new[]{new[]{"public_center","service.entertainment","36"},new[]{"public_center","service.commercial","36"},new[]{"medical_clinic","service.medical","40"},new[]{"primary_school","service.education","36"}})Check(Near(c.projects[row[0]]["stats"][row[1]].Double,double.Parse(row[2],System.Globalization.CultureInfo.InvariantCulture)),row[0]+" main service unchanged");
  Console.WriteLine("SPECIALIZATION PASS "+checks+" assertions; isolated construction fixtures, no Player acceptance.");
 }
}
