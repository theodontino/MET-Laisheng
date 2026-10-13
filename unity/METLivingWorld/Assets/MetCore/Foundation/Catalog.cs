using System;
using System.IO;
using System.Linq;
namespace MET.Core {
public sealed class Catalog {
 public readonly string contentRoot;
 public V data,balance,projects,bonuses,operators,policies,origins,v040,beta;
 public V bonus_upgrades,commission_themes,commission_events,capstones,contents;
 public static readonly V SLOT_NAMES=V.Map("Production","生产方案","Logistics","物流方案","Operations","运营方案","Flex","弹性方案");
 public static readonly V RESOURCE_NAMES=V.Map("food","食物","material","材料","goods","低级制成品","energy","能源");
 public static readonly V SERVICE_NAMES=V.Map("medical","医疗","education","教育","entertainment","娱乐","commercial","消费");
 public Catalog(string contentRoot){this.contentRoot=contentRoot;contents=V.Map();data=Read("core020.json");balance=data["balance"];projects=data["buildings"];bonuses=data["bonuses"];operators=data["operators"];policies=data["policies"];origins=V.Map();foreach(var o in data["original_origins"])origins[o["id"]]=o;v040=V.Map();foreach(var name in new[]{"buildings","progression","operators","bonus_economy","commissions","market","policies"})v040[name]=Read("040/"+name+".json");projects=v040["buildings"];data["city_goals"]=v040["progression"]["goals"];foreach(var id in v040["operators"].Keys)operators[id].Merge(v040["operators"][id],true);foreach(var id in v040["policies"].Keys)policies[id].Merge(v040["policies"][id],true);foreach(var bonus in bonuses.Values)bonus["install_gate"]=bonus.Get("gate",V.Map()).Duplicate(true);beta=Read("beta/parameters.json");bonus_upgrades=Read("beta/bonus_upgrades.json");commission_themes=Read("beta/commission_themes.json");commission_events=Read("beta/commission_events.json");capstones=Read("beta/capstones.json");balance["initial_dt"]=beta["time"]["initial_dt"].Double;balance["dt_cap"]=balance["initial_dt"];balance["offline_dt_per_hour"]=0.0;balance["initial_people"]=beta["economy_candidates"]["initial_people"].Long;balance["arrival_count"]=beta["economy_candidates"]["arrival_people"].Long;balance["birth_per_hour"]=Math.Log(1+beta["economy_candidates"]["natural_growth_per_year"].Double)/12;v040["progression"]["construction_capacity"]=beta["economy_candidates"]["base_construction_cp_per_month"].Double;var domains=V.Arr();foreach(var building in projects.Values){string domain=building.Get("domain","public").Text;if(!domains.Contains(domain))domains.Add(domain);}foreach(var op in operators.Values)foreach(var domain in domains)if(!op["domain_weights"].Has(domain))op["domain_weights"][domain]=2.0;data["build"]="0.5.0-beta.2";}
 V Read(string path){var value=V.Parse(File.ReadAllText(Path.Combine(contentRoot,path)));contents[path]=value;return value;}
 static readonly V TagNames=V.Map("building.industrial","工业","building.residential","住宅","building.medical","医疗","building.education","教育","building.agricultural","农业","building.producer","生产","building.service","服务","building.public_service","公共服务","building.commercial","商业","building.green","绿色","building.energy","能源","building.extractive","开采","building.logistics","物流","building.road","道路接入","tile.waterfront","水岸","tile.fertile","肥沃","tile.forest","森林","tile.highland","高地","tile.wet","湿地","tile.water","水域");
 public string tag_name(string tag)=>TagNames.Get(tag,tag).Text;
 public string tags_text(V tags)=>string.Join(" · ",tags.Select(t=>tag_name(t.Text)));
}
}
