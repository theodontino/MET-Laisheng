using System;
namespace MET.Core {
public sealed partial class CitySession {
    static readonly string[] WorldResources={"food","material","goods","advanced_components"};
    static bool WorldNumber(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
    public void WorldEnsure()=>EnsureNetwork();
    double CargoBatchSeconds=>Math.Max(1,arc.Get("economy",V.Map()).Get("cargo_batch_seconds",60).Double);
    static void AddAmounts(V target,V source) {foreach(var key in source.Keys)target[key]=target.Get(key,0).Double+source[key].Double;}
    static void AddSources(V target,V source) {
        foreach(var row in source) {
            V found=V.Null;foreach(var old in target)if(old["project_id"]==row["project_id"]){found=old;break;}
            if(found.IsNull)target.Add(row.Duplicate(true));else found["amount"]+=row["amount"];
        }
    }
    static void MergeTrade(V target,V source) {
        double prior=target["amount"].Double,amount=source["amount"].Double,total=prior+amount;
        foreach(var field in new[]{"price","unit_price","mid_price"})target[field]=(target[field].Double*prior+source[field].Double*amount)/total;
        target["amount"]=total;target["currency_delta"]+=source["currency_delta"];target["through_external_seconds"]=source.Get("through_external_seconds",source["external_seconds"]);
        AddSources(target["capacity_sources"],source["capacity_sources"]);
        target["fills"]=target.Get("fills",1).Long+source.Get("fills",1).Long;
    }
    static void MergeRows(V target,V source,string key) {
        foreach(var row in source) {
            V found=V.Null;foreach(var old in target)if(old[key]==row[key]&&(key!="order_id"||old["block_id"]==row["block_id"])){found=old;break;}
            if(found.IsNull)target.Add(row.Duplicate(true));else if(key=="order_id")MergeTrade(found,row);
            else {found["amount"]+=row["amount"];if(row.Has("fee"))found["fee"]=found.Get("fee",0).Double+row["fee"].Double;AddSources(found["capacity_sources"],row["capacity_sources"]);}
        }
    }
    void AppendLedger(V ledger,V receipt) {
        double start=Math.Floor((receipt["at"].Double-1e-9)/CargoBatchSeconds)*CargoBatchSeconds;
        if(ledger.Count==0||ledger[-1].Get("window_start",-1).Double!=start) {
            var row=receipt.Duplicate(true);row["window_start"]=start;row["through_external_seconds"]=receipt["at"];row["slices"]=1;ledger.Add(row);
        } else {
            var row=ledger[-1];row["through_external_seconds"]=receipt["at"];row["slices"]=row["slices"].Long+1;
            foreach(var field in new[]{"external_seconds","replenished","outside_capacity","outside_used"})row[field]+=receipt[field];
            foreach(var field in new[]{"node_capacity","node_used","replenished_by_resource"})AddAmounts(row[field],receipt[field]);
            row["outside_after"]=receipt["outside_after"].Duplicate(true);
            MergeRows(row["deliveries"],receipt["deliveries"],"link_id");MergeRows(row["dispatches"],receipt["dispatches"],"link_id");MergeRows(row["outside_trades"],receipt["outside_trades"],"order_id");
        }
        while(ledger.Count>600)ledger.Items.RemoveAt(0);
    }
    void RememberTrade(Game g,V trade,V live) {
        var block=g.s["block"];double start=Math.Floor((trade["external_seconds"].Double-1e-9)/CargoBatchSeconds)*CargoBatchSeconds;
        V found=V.Null;for(int i=block["trade_history"].Count-1;i>=0;i--) {
            var old=block["trade_history"][i];if(old.Get("window_start",-1).Double!=start)break;
            if(old["order_id"]==trade["order_id"]){found=old;break;}
        }
        if(found.IsNull){var copy=trade.Duplicate(true);copy["window_start"]=start;copy["through_external_seconds"]=trade["external_seconds"];copy["fills"]=1;block["trade_history"].Add(copy);}else MergeTrade(found,trade);
        while(block["trade_history"].Count>1200)block["trade_history"].Items.RemoveAt(0);
        if(!block.Get("city_trade_totals").IsMap)block["city_trade_totals"]=V.Map();
        string r=trade["resource"].Text,side=trade["side"].Text;var totals=block["city_trade_totals"];
        if(!totals.Has(r))totals[r]=V.Map("buy",0.0,"sell",0.0,"adverse_exports",0.0,"currency_delta",0.0);
        totals[r][side]+=trade["amount"];totals[r]["currency_delta"]+=trade["currency_delta"];
        if(side=="sell"&&trade["price"].Double<c.v040["market"]["resources"][r]["base_price"].Double)totals[r]["adverse_exports"]+=trade["amount"];
        live["currency_delta_total"]=live.Get("currency_delta_total",0).Double+trade["currency_delta"].Double;
    }
    void EnsureNetwork() {
        if(!s.Get("links").IsMap)s["links"]=V.Map();
        if(!s.Get("network").IsMap)s["network"]=V.Map("ledger",V.Arr(),"last_tick",V.Map());
        if(!s.Get("world_market").IsMap) {
            var resources=V.Map();
            foreach(var r in new[]{"food","material","goods"}) {
                var pricing=c.v040["market"]["resources"][r].Duplicate(true);
                resources[r]=V.Map("stock",arc.Get("economy",V.Map()).Get("outside_initial_stock",120).Double,"capacity",arc.Get("economy",V.Map()).Get("outside_stock_capacity",240).Double,"replenishment_per_hour",arc.Get("economy",V.Map()).Get("outside_replenishment_per_hour",12).Double,"pricing",pricing);
            }
            s["world_market"]=V.Map("resources",resources,"capacity_per_hour",arc.Get("economy",V.Map()).Get("outside_capacity_per_hour",24).Double,"currency",arc.Get("economy",V.Map()).Get("outside_initial_currency",10000).Double,"ledger",V.Arr(),"last_tick",V.Map());
        }
    }
    public string CreateLink(string from,string to,string resource,double rate,double cost) {
        EnsureNetwork();if(!CanEditFocused)return "请先回任地区再安排物流。";
        if(from==to||!Games.ContainsKey(from)||!Games.ContainsKey(to))return "请选择两个真实且不同的街区。";
        if(Block(from)["district_id"].Text!=ActiveDistrictId&&Block(to)["district_id"].Text!=ActiveDistrictId)return "连接至少一端须属于当前治理地区。";
        if(Array.IndexOf(WorldResources,resource)<0)return "这种资源无法货运。";
        if(!WorldNumber(rate)||!WorldNumber(cost)||rate<=0||cost<0)return "运量必须为正，运费不能为负。";
        string id=Uid("link");bool cross=Block(from)["district_id"]!=Block(to)["district_id"];
        s["links"][id]=V.Map("id",id,"from",from,"to",to,"resource",resource,"rate",rate,"cost",cost,"enabled",true,"cross_district",cross,"travel_seconds",arc.Get("economy",V.Map()).Get(cross?"inter_district_travel_seconds":"inter_block_travel_seconds",cross?7200:3600).Double,"buffer_capacity",rate*arc.Get("economy",V.Map()).Get("buffer_hours",4).Double,"buffer",0.0,"inflight",V.Arr(),"delivered",0.0,"dispatched",0.0,"fees",0.0,"last_reason","");
        Record("link_created",s["links"][id].Duplicate(true));return "";
    }
    public string SetLinkEnabled(string id,bool enabled) {
        if(!CanEditFocused)return "请先回任地区再安排物流。";if(!s.Get("links",V.Map()).Has(id))return "没有这条物流连接。";
        var link=s["links"][id];if(Block(link["from"].Text)["district_id"].Text!=ActiveDistrictId&&Block(link["to"].Text)["district_id"].Text!=ActiveDistrictId)return "这条连接不属于当前治理地区。";
        link["enabled"]=enabled;Record("link_enabled",V.Map("id",id,"enabled",enabled));return "";
    }
    public V NetworkView() {
        var market=s.Get("world_market",V.Map()).Duplicate(true);var nodes=V.Arr();
        foreach(var id in Rules.ids(s.Get("blocks",V.Map())))if(Games.ContainsKey(id.Text)) {
            var g=Games[id.Text];nodes.Add(V.Map("id",id,"name",Block(id.Text).Get("name",id),"district_id",Block(id.Text)["district_id"],"capacity_per_hour",g.s["phase"].Text=="play"?Market._total_capacity(g):0.0,"sources",g.s["phase"].Text=="play"?Market._freight_sources(g):V.Arr()));
        }
        return V.Map("external_seconds",s.Get("external_seconds",0),"nodes",nodes,"links",s.Get("links",V.Map()).Duplicate(true),"world_market",market,"last_tick",s.Get("network",V.Map()).Get("last_tick",V.Map()).Duplicate(true),"cargo_batch_seconds",CargoBatchSeconds,"units","运量/现实小时；货物按固定 "+CargoBatchSeconds+" 秒收集批次到站，收集延迟不超过一个批次；跨地区运费×2");
    }
    static double Available(V sources) {double n=0;foreach(var row in sources)n+=row["available"].Double;return n;}
    static double Inflight(V link) {double n=link["buffer"].Double;foreach(var row in link["inflight"])n+=row["amount"].Double;return n;}
    void FreightAudit(Game g,string r,string side,double amount,double cash) {
        double h=g.s.Get("audit",V.Map()).Get("hours",1).Double;
        Market._audit_trade(g,r,side,amount,cash,h);
        g.revision++;Civic.finalize(g);
    }
    V WorldQuote(string r) {
        var x=s["world_market"]["resources"][r]["pricing"];double mid=x["base_price"].Double+x["amplitude"].Double*Math.Tanh(x.Get("pressure",0).Double/Math.Max(.000001,x["market_depth"].Double));
        double spread=Math.Max(.000001,x.Get("spread",0).Double+2*Math.Max(0,x.Get("fee",0).Double));
        return V.Map("mid",mid,"buy",mid+spread/2,"sell",mid-spread/2);
    }
    // Called exactly once for a committed shared External slice. Links ship real stock; arrival backlog remains owned by the link.
    public void SettleNetwork(double seconds) {
        if(seconds<=0||!WorldNumber(seconds))return;EnsureNetwork();double hours=seconds/3600,now=s.Get("external_seconds",0).Double;
        var market=s["world_market"];var worldBefore=V.Map();var replenished=V.Map();double minted=0;
        foreach(var r in market["resources"].Keys) {
            var x=market["resources"][r];worldBefore[r]=x["stock"];double add=Math.Min(Math.Max(0,x["capacity"].Double-x["stock"].Double),x["replenishment_per_hour"].Double*hours);
            x["stock"]+=add;replenished[r]=add;minted+=add;x["pricing"]["pressure"]=x["pricing"].Get("pressure",0).Double*Math.Exp(-seconds/c.beta["external_market_candidate"]["pressure_relaxation_time_constant_real_seconds"].Double);
        }
        var quotes=V.Map();foreach(var r in market["resources"].Keys)quotes[r]=WorldQuote(r);
        var nodes=V.Map();var nodeTotals=V.Map();
        foreach(var id in Rules.ids(s["blocks"]))if(Games.ContainsKey(id.Text)&&Games[id.Text].s["phase"].Text=="play") {
            var sources=Market._freight_sources(Games[id.Text]);foreach(var row in sources)row["available"]=row["available"].Double*hours;nodes[id]=sources;nodeTotals[id]=Available(sources);
        }
        var receipt=V.Map("external_seconds",seconds,"at",now+seconds,"node_capacity",nodeTotals,"node_used",V.Map(),"deliveries",V.Arr(),"dispatches",V.Arr(),"outside_trades",V.Arr(),"replenished",minted,"replenished_by_resource",replenished,"outside_capacity",market["capacity_per_hour"].Double*hours,"outside_used",0.0,"outside_before",worldBefore,"outside_after",V.Map());
        var edgeBudgets=V.Map();foreach(var id in Rules.ids(s["links"]))edgeBudgets[id]=s["links"][id]["rate"].Double*hours;
        foreach(var id in Rules.ids(s["links"])) {
            var link=s["links"][id];var pending=V.Arr();
            foreach(var cargo in link["inflight"]) {if(cargo["arrival_external_seconds"].Double<=now+seconds+1e-8)link["buffer"]+=cargo["amount"];else pending.Add(cargo);}link["inflight"]=pending;
            string to=link["to"].Text,r=link["resource"].Text;link["last_reason"]="";
            if(link["buffer"].Double<=1e-9)continue;
            if(!nodes.Has(to)){link["last_reason"]="收货街区尚未经营";continue;}var target=Games[to];
            double unitCost=link["cost"].Double*(link["cross_district"].Bool?arc.Get("economy",V.Map()).Get("cross_district_cost_multiplier",2).Double:1);
            double amount=Math.Min(link["buffer"].Double,Math.Min(edgeBudgets[id].Double,Math.Min(Available(nodes[to]),Market._buy_room(target,r))));
            if(unitCost>0)amount=Math.Min(amount,Math.Max(0,target.s["currency"].Double)/unitCost);
            if(amount<=1e-9){link["last_reason"]=Market._buy_room(target,r)<=1e-9?"收货仓储已满":Available(nodes[to])<=1e-9?"收货节点运力不足":"收货运费不足";continue;}
            amount=Market._deposit(target,r,amount);var allocation=Market._consume_capacity(nodes[to],amount);double fee=amount*unitCost;
            target.s["currency"]-=fee;s["treasury"]=s.Get("treasury",0).Double+fee;link["buffer"]-=amount;link["delivered"]+=amount;link["fees"]+=fee;edgeBudgets[id]-=amount;
            FreightAudit(target,r,"buy",amount,-fee);receipt["deliveries"].Add(V.Map("link_id",id,"from",link["from"],"to",to,"resource",r,"amount",amount,"fee",fee,"capacity_sources",allocation["sources"]));
        }
        foreach(var id in Rules.ids(s["links"])) {
            var link=s["links"][id];if(!link["enabled"].Bool)continue;string from=link["from"].Text,to=link["to"].Text,r=link["resource"].Text;
            if(!nodes.Has(from)||!nodes.Has(to)){link["last_reason"]="两端必须已开始经营";continue;}var source=Games[from];
            double amount=Math.Min(edgeBudgets[id].Double,Math.Min(Available(nodes[from]),Math.Min(source.inventory_total(r),Math.Max(0,link["buffer_capacity"].Double-Inflight(link)))));
            if(amount<=1e-9){if(link["last_reason"].Text=="")link["last_reason"]=source.inventory_total(r)<=1e-9?"源街区库存不足":Available(nodes[from])<=1e-9?"源节点运力不足":"连接缓冲已满";continue;}
            if(!source.withdraw_stock(r,amount))throw new InvalidOperationException("物流源库存不足");var allocation=Market._consume_capacity(nodes[from],amount);
            double arrival=Math.Ceiling((now+seconds+link["travel_seconds"].Double)/CargoBatchSeconds)*CargoBatchSeconds;
            V bucket=link["inflight"].Count>0?link["inflight"][-1]:V.Null;
            if(!bucket.IsNull&&bucket["arrival_external_seconds"].Double==arrival) {
                bucket["amount"]+=amount;bucket["last_departed_external_seconds"]=now+seconds;AddSources(bucket["source_capacity"],allocation["sources"]);
            } else link["inflight"].Add(V.Map("amount",amount,"departed_external_seconds",now+seconds,"last_departed_external_seconds",now+seconds,"arrival_external_seconds",arrival,"source_block_id",from,"source_capacity",allocation["sources"].Duplicate(true)));
            link["dispatched"]+=amount;edgeBudgets[id]-=amount;
            FreightAudit(source,r,"sell",amount,0);receipt["dispatches"].Add(V.Map("link_id",id,"from",from,"to",to,"resource",r,"amount",amount,"capacity_sources",allocation["sources"]));
        }
        double outside=market["capacity_per_hour"].Double*hours;
        foreach(var id in Rules.ids(nodes)) {
            var g=Games[id.Text];Market.ensure(g);var block=g.s["block"];var trades=V.Arr();double cashTotal=0,used=0;
            foreach(var order in Market._orders_in_stable_order(block["trade_orders"])) {
                if(!V.Arr("open","partial").Contains(order["status"]))continue;string r=order["resource"].Text,side=order["side"].Text;if(!quotes.Has(r))continue;
                var quote=quotes[r];double price=quote[side].Double;var live=V.Null;foreach(var row in block["trade_orders"])if(row["id"]==order["id"]){live=row;break;}
                live["last_attempt_tick"]=g.s["simulation_tick"];
                if(side=="buy"?price>live["limit_price"].Double+1e-6:price+1e-6<live["limit_price"].Double){live["last_reason"]="未达到限价";continue;}
                var world=market["resources"][r];double amount=Math.Min(live["remaining"].Double,Math.Min(outside,Available(nodes[id])));
                amount=Math.Min(amount,side=="buy"?Math.Min(world["stock"].Double,Market._buy_room(g,r)):Math.Min(g.inventory_total(r),Math.Max(0,world["capacity"].Double-world["stock"].Double)));
                double payerPrice=side=="buy"?price:-price;if(payerPrice>0)amount=Math.Min(amount,Math.Max(0,g.s["currency"].Double)/payerPrice);else if(payerPrice<0)amount=Math.Min(amount,Math.Max(0,market["currency"].Double)/-payerPrice);
                if(amount<=1e-9){live["last_reason"]=outside<=1e-9?"外部总运力已用尽":Available(nodes[id])<=1e-9?"节点运力已用尽":side=="buy"&&world["stock"].Double<=1e-9?"外部库存不足":"库存、库容或结算资金不足";continue;}
                if(side=="buy"){amount=Market._deposit(g,r,amount);world["stock"]-=amount;}else {if(!g.withdraw_stock(r,amount))throw new InvalidOperationException("外部订单源库存不足");world["stock"]+=amount;}
                var allocation=Market._consume_capacity(nodes[id],amount);outside-=amount;receipt["outside_used"]+=amount;double cash=side=="buy"?-amount*price:amount*price;g.s["currency"]+=cash;market["currency"]-=cash;cashTotal+=cash;used+=amount;
                world["pricing"]["pressure"]=world["pricing"].Get("pressure",0).Double+(side=="buy"?amount:-amount);
                live["remaining"]=Math.Max(0,live["remaining"].Double-amount);live["filled"]=live.Get("filled",0).Double+amount;live["status"]=live["remaining"].Double<=1e-6?"filled":"partial";live["last_reason"]="";
                var trade=V.Map("id",g.uid("X"),"order_id",live["id"],"block_id",id,"resource",r,"side",side,"amount",amount,"price",price,"unit_price",price,"mid_price",quote["mid"],"limit_price",live["limit_price"],"currency_delta",cash,"tick",g.s["simulation_tick"],"h",g.s["local_hours"],"external_seconds",now+seconds,"capacity_sources",allocation["sources"],"source","city_outside_market");
                trade["counterparty"]=live.Get("counterparty","");
                trades.Add(trade);RememberTrade(g,trade,live);receipt["outside_trades"].Add(trade);FreightAudit(g,r,side,amount,cash);RecordWildTrade(trade);
            }
            // Read-side market prices mirror the unique world; no second local settlement runs.
            foreach(var r in market["resources"].Keys) {var pricing=market["resources"][r]["pricing"].Duplicate(true);var q=WorldQuote(r);pricing["mid"]=q["mid"];pricing["buy_price"]=q["buy"];pricing["sell_price"]=q["sell"];block["market_state"]["resources"][r]=pricing;}
            if(trades.Count>0) {
                double window=Math.Floor((now+seconds-1e-9)/CargoBatchSeconds)*CargoBatchSeconds;var ledger=block["trade_ledger"];
                if(ledger.Count==0||ledger[-1].Get("window_start",-1).Double!=window)ledger.Add(V.Map("window_start",window,"through_external_seconds",now+seconds,"external_seconds",seconds,"economic_months",g.s.Get("audit",V.Map()).Get("hours",0),"trades",trades.Duplicate(true),"currency_delta",cashTotal,"used_capacity",used,"capacity",nodeTotals[id]));
                else {var row=ledger[-1];row["through_external_seconds"]=now+seconds;row["external_seconds"]+=seconds;row["economic_months"]+=g.s.Get("audit",V.Map()).Get("hours",0);row["currency_delta"]+=cashTotal;row["used_capacity"]+=used;row["capacity"]+=nodeTotals[id];MergeRows(row["trades"],trades,"order_id");}
                while(ledger.Count>600)ledger.Items.RemoveAt(0);
            }
            g.s["metrics"]["market_trades"]=trades;g.s["metrics"]["market_capacity"]=V.Map("last",nodeTotals[id],"used",nodeTotals[id].Double-Available(nodes[id]));
        }
        foreach(var id in nodes.Keys)receipt["node_used"][id]=nodeTotals[id].Double-Available(nodes[id]);
        foreach(var r in market["resources"].Keys)receipt["outside_after"][r]=market["resources"][r]["stock"];
        s["network"]["last_tick"]=receipt;AppendLedger(s["network"]["ledger"],receipt);
        market["last_tick"]=receipt;AppendLedger(market["ledger"],receipt);
    }
}
}
