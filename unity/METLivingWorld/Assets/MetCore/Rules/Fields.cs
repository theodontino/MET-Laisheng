using System;
using System.Linq;
namespace MET.Core {
/// <summary>Source-aware field entities; tiles are projections, not authority. Port of core/fields.gd.</summary>
public static class Fields {
    public static V empty_tiles(V s) {
        var tiles=V.Map();foreach(var id in Rules.ids(s["cells"]))tiles[id]=V.Map();return tiles;
    }
    public static void sample(V tiles,string tile,string kind,string id,string source,double value,string via="") {
        if(!tiles.Has(tile))return;
        if(!tiles[tile].Has(kind))tiles[tile][kind]=V.Arr();
        tiles[tile][kind].Add(V.Map("field_id",id,"source_id",source,"value",value,"via",via));
    }
    public static V make_field(string source,string kind,double strength,int radius,double decay,string rule="base")=>
        V.Map("id","F:"+source+":"+kind+":"+rule,"type",kind,"source_id",source,"strength",strength,"propagation",V.Map("radius",radius,"decay",decay,"rule",rule),"active",true);
    public static double access_strength(int distance,int radius,double decay) {
        if(distance>radius)return 0;return Math.Max(0,1.0-Math.Max(0,distance-1)*decay);
    }
    public static V road_component(V s,string start) {
        var result=V.Arr();var todo=V.Arr(start);
        while(todo.Count>0) {
            var id=todo[0];todo.Items.RemoveAt(0);
            if(result.Contains(id)||!s["projects"].Has(id))continue;
            var p=s["projects"][id];
            if(!p["traits"].Contains("building.road")||p["paused"].Bool||p["status"].Text!="running")continue;
            result.Add(id);
            foreach(var other in Rules.ids(s["projects"]))if(!result.Contains(other)&&Rules.near(s,id.Text,other.Text)==1)todo.Add(other);
        }
        return result;
    }
    public static double source_value(V tiles,string tile,string kind,string source) {
        double best=0;
        foreach(var x in tiles.Get(tile,V.Map()).Get(kind,V.Arr()))if(x["source_id"].Text==source)best=Math.Max(best,x["value"].Double);
        return best;
    }
    public static V access(Game g,V compiled) {
        var s=g.s;var tiles=empty_tiles(s);var entities=V.Map();var st=compiled["stats"];
        foreach(var id in Rules.ids(s["projects"])) {
            var p=s["projects"][id];
            if(p.Get("special",false).Bool||p["status"].Text!="running"||p["paused"].Bool)continue;
            var kinds=V.Arr();
            if(st[id]["jobs"].Double>0)kinds.Add("employment");
            foreach(var k in g.c.balance["service_kinds"])if(st[id]["service."+k.Text].Double>0)kinds.Add(k);
            if(compiled["config"][id].Has("volunteer_education"))foreach(var group in s["groups"].Values) {
                if(group["host"]==id&&group["education"]>=compiled["config"][id]["volunteer_education"]["threshold"]&&!kinds.Contains("education"))kinds.Add("education");
            }
            foreach(var kind in kinds) {
                var rad=(int)st[id][kind.Text=="employment"?"employment_range":"service_range"].Long;
                var decay=st[id][kind.Text=="employment"?"employment_decay":"service_decay"].Double;
                var component=compiled["options"].Get("access_network",false).Bool?road_component(s,id.Text):V.Arr();
                var f=make_field(id.Text,kind.Text,1,rad,decay);entities[f["id"]]=f;
                foreach(var key in Rules.ids(s["cells"])) {
                    var dist=Hex.distance(p["tile"].Text,key.Text);var value=access_strength(dist,rad,decay);
                    foreach(var other in component) {
                        var network_distance=Hex.distance(s["projects"][other]["tile"].Text,key.Text);
                        value=Math.Max(value,access_strength(network_distance,rad,decay*.6)*.9);
                    }
                    if(value>0)sample(tiles,key.Text,kind.Text,f["id"].Text,id.Text,value);
                }
            }
            foreach(var e in compiled["emitters"][id]) {
                var f=make_field(id.Text,e["type"].Text,e["strength"].Double,(int)e["range"].Long,e["decay"].Double,e["uid"].Text);entities[f["id"]]=f;
                foreach(var key in Rules.ids(s["cells"])) {
                    var value=access_strength(Hex.distance(p["tile"].Text,key.Text),(int)e["range"].Long,e["decay"].Double)*e["strength"].Double;
                    if(value!=0)sample(tiles,key.Text,e["type"].Text,f["id"].Text,id.Text,value);
                }
            }
        }
        foreach(var fid in Rules.ids(s.Get("authored_fields",V.Map()))) {
            var f=s["authored_fields"][fid];if(!f["active"].Bool)continue;
            var origin=f.Get("origin","").Text;var source=f["source_id"].Text;
            if(origin=="") {
                if(s["cells"].Has(source))origin=source;
                else if(s["projects"].Has(source))origin=s["projects"][source]["tile"].Text;
                else if(s["groups"].Has(source))origin=s["projects"][s["groups"][source]["host"]]["tile"].Text;
            }
            if(!s["cells"].Has(origin))continue;
            entities[fid]=f.Duplicate(true);
            foreach(var tile in Rules.ids(s["cells"])) {
                var value=f["strength"].Double*access_strength(Hex.distance(origin,tile.Text),(int)f["propagation"]["radius"].Long,f["propagation"]["decay"].Double);
                if(value!=0)sample(tiles,tile.Text,f["type"].Text,fid.Text,source,value);
            }
        }
        // Relay reads one detached snapshot and preserves the original provider ID.
        var original=tiles.Duplicate(true);
        foreach(var id in Rules.ids(s["projects"])) {
            var p=s["projects"][id];var relay=compiled["config"][id].Get("service_relay",0).Double;
            if(relay<=0||p["status"].Text!="running"||p["paused"].Bool)continue;
            foreach(var kind in g.c.balance["service_kinds"])foreach(var x in original.Get(p["tile"],V.Map()).Get(kind,V.Arr())) {
                if(x["source_id"]==id||Rules.near(s,id.Text,x["source_id"].Text)!=1)continue;
                foreach(var key in Rules.ids(s["cells"]))if(Hex.distance(p["tile"].Text,key.Text)<=1)sample(tiles,key.Text,kind.Text,x["field_id"].Text,x["source_id"].Text,x["value"].Double*relay,id.Text);
            }
        }
        return V.Map("fields",entities,"tiles",tiles);
    }
    public static V environment(Game g,V compiled,V activity,double h) {
        var tiles=empty_tiles(g.s);var fields=V.Map();var budgets=V.Map();double pollution_produced=0,heat_produced=0;
        foreach(var id in Rules.ids(g.s["projects"])) {
            var p=g.s["projects"][id];
            if(p.Get("special",false).Bool||p["status"].Text!="running"||p["paused"].Bool)continue;
            var a=activity.Get(id,V.Map());var st=compiled["stats"][id];var cfg=compiled["config"][id];var u=a.Get("utilization",0).Double;
            var pollution=st["pollution"].Double*u;var redirected=pollution*cfg.Get("redirect_pollution_heat",0).Double;pollution-=redirected;
            var heat=a.Get("product_heat",0).Double+redirected;
            pollution_produced+=Math.Max(0,pollution)*h;heat_produced+=Math.Max(0,heat)*h;
            foreach(var kind in new[]{"pollution","heat"}) {
                var strength=kind=="pollution"?pollution:heat;if(Math.Abs(strength)<.000001)continue;
                var radius=kind=="pollution"?(int)st["pollution_range"].Long:2;var decay=kind=="pollution"?st["pollution_decay"].Double:.5;
                var f=make_field(id.Text,kind,strength,radius,decay);fields[f["id"]]=f;var weights=V.Map();double sum=0;
                var component=kind=="heat"&&compiled["options"].Get("heat_network",false).Bool?road_component(g.s,id.Text):V.Arr();
                foreach(var tile in Rules.ids(g.s["cells"])) {
                    var dist=Hex.distance(p["tile"].Text,tile.Text);double w=0;
                    if(kind=="pollution"&&dist<=radius) {
                        var d=g.s["cells"][tile]["traits"].Contains("tile.waterfront")?decay*cfg.Get("water_pollution_decay",1).Double:decay;
                        w=Math.Pow(Math.Max(.001,1-d),dist);
                    } else if(kind=="heat") {
                        w=access_strength(dist,radius,decay);
                        foreach(var other in component)w=Math.Max(w,.8*access_strength(Hex.distance(g.s["projects"][other]["tile"].Text,tile.Text),2,.5));
                    }
                    if(w>0) { weights[tile]=w;sum+=w; }
                }
                var local=V.Map();
                foreach(var tile in weights.Keys) {
                    // Pollution mass partitions; heat reach shares one finite source pool.
                    var value=kind=="pollution"?strength*weights[tile].Double/Math.Max(.001,sum):strength*weights[tile].Double;
                    sample(tiles,tile,kind,f["id"].Text,id.Text,value);local[tile]=value*h;
                }
                budgets[f["id"]]=V.Map("type",kind,"source_id",id,"total",Math.Max(0,strength*h),"remaining",Math.Max(0,strength*h),"local",local);
            }
        }
        foreach(var fid in Rules.ids(g.s.Get("authored_fields",V.Map()))) {
            var f=g.s["authored_fields"][fid];
            if(!f["active"].Bool||(f["type"].Text!="pollution"&&f["type"].Text!="heat"))continue;
            var origin=f.Get("origin","").Text;var source=f["source_id"].Text;
            if(origin=="") {
                if(g.s["cells"].Has(source))origin=source;
                else if(g.s["projects"].Has(source))origin=g.s["projects"][source]["tile"].Text;
                else if(g.s["groups"].Has(source))origin=g.s["projects"][g.s["groups"][source]["host"]]["tile"].Text;
            }
            if(!g.s["cells"].Has(origin))continue;
            var weights=V.Map();double weight_sum=0;var local=V.Map();var strength=f["strength"].Double;
            foreach(var tile in Rules.ids(g.s["cells"])) {
                var w=access_strength(Hex.distance(origin,tile.Text),(int)f["propagation"]["radius"].Long,f["propagation"]["decay"].Double);
                if(w>0) { weights[tile]=w;weight_sum+=w; }
            }
            fields[fid]=f.Duplicate(true);
            foreach(var tile in weights.Keys) {
                var contribution=strength*weights[tile].Double;if(f["type"].Text=="pollution")contribution/=Math.Max(.000001,weight_sum);
                sample(tiles,tile,f["type"].Text,fid.Text,source,contribution);local[tile]=Math.Max(0,contribution*h);
            }
            budgets[fid]=V.Map("type",f["type"],"source_id",source,"total",Math.Max(0,strength*h),"remaining",Math.Max(0,strength*h),"local",local);
            if(f["type"].Text=="pollution")pollution_produced+=Math.Max(0,strength)*h;else heat_produced+=Math.Max(0,strength)*h;
        }
        return V.Map("fields",fields,"tiles",tiles,"budgets",budgets,"pollution_produced",pollution_produced,"heat_produced",heat_produced,"hours",h);
    }
    public static double consume_local_pollution(V env,string tile,double amount,double h) {
        double taken=0;
        foreach(var fid in Rules.ids(env["budgets"])) {
            var pool=env["budgets"][fid];if(pool["type"].Text!="pollution")continue;
            var n=Math.Min(amount-taken,Math.Min(pool["remaining"].Double,Math.Max(0,pool["local"].Get(tile,0).Double)));
            if(n<=0)continue;
            pool["remaining"]-=n;pool["local"][tile]-=n;taken+=n;
            foreach(var entry in env["tiles"].Get(tile,V.Map()).Get("pollution",V.Arr()))if(entry["field_id"]==fid)entry["value"]-=n/Math.Max(1e-15,h);
            if(taken>=amount-.000001)break;
        }
        return taken;
    }
    public static V reserve_heat(V env,string tile,double requested) {
        double got=0;var sources=V.Arr();var options=env["tiles"].Get(tile,V.Map()).Get("heat",V.Arr()).Duplicate();
        options.Items.Sort((a,b)=>a["value"].Double!=b["value"].Double?b["value"].Double.CompareTo(a["value"].Double):StringComparer.Ordinal.Compare(a["field_id"].Text,b["field_id"].Text));
        foreach(var e in options) {
            var pool=env["budgets"][e["field_id"]];var n=Math.Min(requested-got,Math.Min(pool["remaining"].Double,Math.Max(0,pool["local"].Get(tile,0).Double)));
            if(n<=0)continue;
            pool["remaining"]-=n;pool["local"][tile]-=n;got+=n;sources.Add(V.Map("source",pool["source_id"],"amount",n));
            if(got>=requested-.000001)break;
        }
        return V.Map("amount",got,"sources",sources);
    }
}
}
