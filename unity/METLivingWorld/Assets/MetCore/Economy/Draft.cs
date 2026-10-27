using System;
namespace MET.Core {
/// <summary>Operator tender; retained offers survive changing the selected tile.</summary>
public static class Draft {
 public static string common(Game g)=>g.s["phase"].Text!="play"?"当前不可建设。":"";
 public static string open_build(Game g,V cells,string @base) {
  var e=common(g);if(e!="")return e;
  if(cells.Count!=1)return "请选择一个未开发的陆地地块。";
  var tile=cells[0].Text;
  if(!g.s["cells"].Has(tile)||g.s["cells"][tile]["building"].Text!=""||g.s["cells"][tile]["traits"].Contains("tile.water"))return "请选择未开发的陆地。";
  if(!g.c.v040["progression"]["tier1"].Contains(@base))return "新建项目请从农居、邻里中心或作坊开始。";
  remember(g);g.s["draft"]=V.Map("kind","development","base",@base,"tile",tile,"host_type","building","host","","slot","","offers",V.Arr());restore_or_draw(g);return "";
 }
 public static string open_refit(Game g,string id,string slot)=>"方案副本来自年度、安居里程碑与主题奖励，请从库存中安装。";
 public static string open_scope(Game g,string host_type,string host)=>"地区与组织方案从已有奖励副本安装。";
 public static V draw(Game g,V d) {
  if(WildDesign.IsFarmBuild(g,d["base"].Text))return V.Arr(V.Map("id","offer:0","base",d["base"],"wild_direct",true,"name","农居建设","operator_org","civic","provider_org","civic","slot_track",V.Arr()));
  var offers=V.Arr();var used=V.Arr();var domain=g.c.projects[d["base"]].Get("domain","public").Text;
  for(var i=0;i<3;i++) {
   var options=V.Arr();var weights=V.Arr();
   foreach(var org in Rules.ids(g.c.operators)) {if(used.Contains(org))continue;options.Add(org);weights.Add(g.c.operators[org]["domain_weights"].Get(domain,2).Double);}
   var selected=Rng.pick_weighted(g.s,"draft",options,weights).Text;used.Add(selected);
   offers.Add(V.Map("id","offer:"+i,"base",d["base"],"operator_org",selected,"provider_org",selected,"slot_track",g.c.operators[selected]["slot_track"].Duplicate()));
  }return offers;
 }
 public static V reroll_cost(Game g) {var n=g.s["draft"].Get("reroll_count",g.s["tender_debt"]).Long;return V.Map("currency",12.0+9.0*n,"dt",0.0);}
 public static string reroll(Game g) {
  if(g.s["draft"].Count==0)return "尚未开标。";if(WildDesign.IsFarmBuild(g,g.s["draft"].Get("base","").Text))return "Wild 农居直接建设，无需征集运营方。";var cost=reroll_cost(g);
  if(g.s["currency"].Double<cost["currency"].Double)return "重投预算不足。";
  g.s["currency"]-=cost["currency"];g.s["tender_debt"]+=1;g.s["draft"]["reroll_count"]=g.s["draft"].Get("reroll_count",0).Long+1;g.s["totals"]["rerolls"]+=1;
  g.s["draft"]["offers"]=draw(g,g.s["draft"]);g.log_event("reroll",V.Map(),"重新征集一组投标方案");return "";
 }
 public static V offer_cost(Game g,V offer) {
  var d=g.s["draft"];
  if(d["kind"].Text=="development")return WildDesign.BuildQuote(g,d["base"].Text);
  return V.Map("currency",g.c.balance[d["kind"].Text=="scope"?"block_offer_price":"bonus_offer_price"].Double,"material",0.0,"hours",0.0);
 }
 public static string accept(Game g,string oid) {
  if(g.s["phase"].Text!="play"||g.s["draft"].Count==0)return "没有待处理的运营商报价。";
  var chosen=V.Map();foreach(var offer in g.s["draft"]["offers"])if(offer["id"].Text==oid){chosen=offer;break;}
  if(chosen.Count==0)return "这不是当前报价。";var d=g.s["draft"];
  if(d["kind"].Text!="development")return "旧方案报价已转入奖励选择。";var cost=offer_cost(g,chosen);
  if(g.s["currency"].Double<cost["currency"].Double)return "通货不足。报价会保留，可继续经营。";
  if(g.s["cells"][d["tile"]]["building"].Text!="")return "场地已被占用。";
  g.create_building(d["base"].Text,d["tile"].Text,chosen["operator_org"].Text);
  g.s["currency"]-=cost["currency"];g.s["totals"]["currency_spent"]+=cost["currency"];
  g.s["quote_bank"].Erase(quote_key(d));g.s["draft"]=V.Map();g.s["tender_debt"]=0;g.refresh_metrics();
  g.log_event("accepted",V.Map("offer",chosen,"kind","development","cost",cost),chosen.Get("wild_direct",false).Bool?"农居直接建设开始；沿用现行通货与材料价格，无运营方选择。":"签约「"+g.c.operators[chosen["operator_org"]]["name"].Text+"」，开始建设。");return "";
 }
 public static string quote_key(V d)=>d.Get("kind","").Text=="development"?"build:"+d["base"].Text:d.Get("kind","").Text+":"+d.Get("host","").Text+":"+d.Get("slot","").Text;
 public static void remember(Game g) {if(!g.s.Has("quote_bank"))g.s["quote_bank"]=V.Map();if(g.s["draft"].Count!=0)g.s["quote_bank"][quote_key(g.s["draft"])]=g.s["draft"].Duplicate(true);}
 public static void restore_or_draw(Game g) {
  var key=quote_key(g.s["draft"]);
  if(g.s["draft"]["kind"].Text=="development"&&WildDesign.IsFarmBuild(g,g.s["draft"]["base"].Text)){g.s["draft"]["offers"]=draw(g,g.s["draft"]);g.s["draft"]["reroll_count"]=0;}
  else if(g.s["quote_bank"].Has(key)){var cached=g.s["quote_bank"][key];g.s["draft"]["offers"]=cached["offers"].Duplicate(true);g.s["draft"]["reroll_count"]=cached.Get("reroll_count",0).Long;}
  else {g.s["draft"]["offers"]=draw(g,g.s["draft"]);g.s["draft"]["reroll_count"]=0;}
  g.s["tender_debt"]=g.s["draft"]["reroll_count"].Long;remember(g);
 }
}
}
