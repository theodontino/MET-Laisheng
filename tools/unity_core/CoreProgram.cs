using System;
using System.IO;
using MET.Core;
class CoreProgram {
    static int checkpoints;
    static void Equal(V actual,V expected,string path) {
        if(expected.IsNumber&&actual.IsNumber) {
            double a=actual.Double,b=expected.Double;
            bool exact=path.Contains(".rng.")||path.EndsWith(".seed")||path.EndsWith(".next_id")||path.EndsWith(".simulation_tick");
            if(exact?a!=b:Math.Abs(a-b)>1e-9*Math.Max(1,Math.Abs(b)))throw new Exception(path+" number mismatch: "+a.ToString("R")+" vs "+b.ToString("R"));return;
        }
        if(expected.IsMap&&actual.IsMap) {if(actual.Count!=expected.Count)throw new Exception(path+" key count differs: "+actual.Count+" vs "+expected.Count);foreach(string k in expected.Keys){if(!actual.Has(k))throw new Exception(path+" missing key "+k);Equal(actual[k],expected[k],path+"."+k);}return;}
        if(expected.IsArray&&actual.IsArray){if(actual.Count!=expected.Count)throw new Exception(path+" array count differs: "+actual.Count+" vs "+expected.Count);for(int i=0;i<expected.Count;i++)Equal(actual[i],expected[i],path+"["+i+"]");return;}
        if(actual!=expected)throw new Exception(path+" value mismatch: "+actual.ToJson()+" vs "+expected.ToJson());
    }
    static V Run(Game g,string command,V a) {
        switch(command) {
            case "new_game":g.new_game(a[0].Long,a[1].Double);return V.Null;
            case "select_block":return g.select_block((int)a[0].Long);
            case "choose_origin":return g.choose_origin(a[0].Text);
            case "theme_select":return Themes.select(g,a[0].Text);
            case "advance":return Clock.advance(g,a[0].Double,a[1].Text);
            case "tender":return g.create_tender(a[0],a[1].Text);
            case "accept":return g.accept_offer(a[0].Text);
            case "reward":return Bonus.choose(g,a[0].Text,a[1].Text);
            case "reward_draw":return Bonus.draw(g,a[0].Text,a[1],a[2]);
            case "bonus_install":return Bonus.install(g,a[0].Text,a[1].Text,a[2].Text,a[3].Text);
            case "upgrade":return Development.start_upgrade(g,a[0].Text,a[1].Text);
            case "market_order":return Market.order(g,a[0].Text,a[1].Text,a[2].Double,a[3].Double);
            case "fixture_inject":
                // Test preconditions are replayed field-by-field; never load the golden state.
                var injection=a[0];g.s["currency"]=injection["currency"];
                g.s["projects"]["municipal"]["inventory"]=injection["stock"].Duplicate(true);
                var ids=V.Arr();foreach(var row in injection["buildings"])
                    ids.Add(g.create_building(row["definition"].Text,row["tile"].Text,row["operator"].Text,true)["id"]);
                g.refresh_metrics();return ids;
            case "mechanism_receipt":
                double bought=0,sold=0;foreach(var trade in g.s["block"]["trade_history"])
                    if(trade["side"]=="buy")bought+=trade["amount"].Double;else sold+=trade["amount"].Double;
                string home=a[0].Text,freight=a[1].Text;
                return V.Map("node",g.s["projects"][home]["node_id"],"active_copies",Bonus.active_count(g,"P07","building",home),
                    "blueprint_level",Bonus.blueprint_level(g,"P07"),"housing",g.compiled["stats"][home]["housing"],
                    "freight_storage",g.compiled["stats"][freight]["storage.goods"],"bought",bought,"sold",sold);
            case "acknowledge":Cycles.acknowledge(g);return V.Null;
            case "unpause":g.s["settings"]["user_paused"]=false;g.s["beta_time"]["pause_reason"]="";return V.Null;
            case "resume":return g.resume_time(a[0].Double);
            case "reload":var decoded=Store.decode(Store.encode(g.s));var loaded=g.migrate_state(decoded["state"]);return V.Map("ok",loaded["ok"],"migrated",loaded.Get("migrated",false));
            default:throw new Exception("Unknown command "+command);
        }
    }
    static void Parity(Catalog catalog,string fixture) {
        var file=V.Parse(File.ReadAllText(fixture));
        foreach(var scenario in file["scenarios"]) {
            var g=new Game(catalog);int index=0;
            foreach(var row in scenario["commands"]) {
                string label=scenario["id"].Text+"/"+index+++"/"+row["command"].Text;
                var result=Run(g,row["command"].Text,row["args"]);
                Equal(result,row["result"],label+".result");Equal(g.s,row["state"],label+".state");checkpoints++;
            }
            Console.WriteLine("PARITY PASS "+scenario["id"].Text+" "+index+" complete-state checkpoints");
        }
    }
    static int Main(string[] args) {
        try {
            string content=args[0],records=args[1];var catalog=new Catalog(content);
            Console.WriteLine("FOUNDATION "+FoundationChecks.run(content).ToJson());
            Console.WriteLine("RULES "+METRulesFocusedChecks.Run().ToJson());
            Console.WriteLine("SIMULATION "+METRulesFocusedChecks.RunSimulation(content).ToJson());
            if(MetProgressionFocusedChecks.Run(catalog,records)>0)return 1;
            if(args.Length>2)Parity(catalog,args[2]);
            Console.WriteLine("CORE CHECKS PASS; parity checkpoints="+checkpoints);return 0;
        } catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
