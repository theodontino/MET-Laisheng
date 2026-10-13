using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MET.Core;
// Pure .NET geography checks. Pass content root and optional JSON evidence path.
class GeographyFocusedChecks {
 static int checks;
 static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
 static int Component(V cells,string start){var seen=new HashSet<string>{start};var queue=new Stack<string>();queue.Push(start);while(queue.Count>0)foreach(var n in Hex.neighbors(queue.Pop()))if(cells.Has(n)&&cells[n]["terrain"]!="water"&&seen.Add(n.Text))queue.Push(n.Text);return seen.Count;}
 static int Main(string[] args){try{
  var catalog=new Catalog(args[0]);var examples=V.Arr();var summary=V.Arr();
  for(int kind=0;kind<3;kind++){
   var maps=new HashSet<string>();var terrainMaps=new HashSet<string>();var rivers=new HashSet<string>();var counts=new Dictionary<string,int>{{"plain",0},{"forest",0},{"hill",0},{"wet",0},{"water",0}};int minPlayable=61;
   for(int i=0;i<128;i++){
    long seed=i==127?-8291:i*7919L+41;var cells=Hex.make_map(kind,seed);var geo=Hex.geography(cells,seed,kind);var river=geo["river_ids"];
    Check(cells.Count==61,"61 real cells");Check(cells==Hex.make_map(kind,seed),"same-seed exact repeat");
    Check(river.Count==9&&Hex.connected(river),"connected source-to-mouth river");Check(Hex.coords(river[0].Text).y==-4&&Hex.coords(river[8].Text).y==4,"river reaches map boundary at both ends");
    for(int j=0;j<river.Count;j++){
     var cell=cells[river[j]];Check(cell["terrain"]=="water"&&cell["river_order"].Long==j,"ordered water source");
     if(j>0){Check(Hex.distance(river[j-1].Text,river[j].Text)==1,"successive river cells adjacent");Check(cells[river[j-1]]["elevation01"].Double>cell["elevation01"].Double,"river runs downhill");}
    }
    foreach(string id in cells.Keys){var cell=cells[id];var q=Hex.coords(id);Check(cell["q"].Long==q.x&&cell["r"].Long==q.y,"stable cell identity");Check(cell["elevation01"].Double>=0&&cell["elevation01"].Double<=1&&cell["moisture01"].Double>=0&&cell["moisture01"].Double<=1,"normalized elevation and moisture");bool waterfront=Hex.neighbors(id).Any(n=>cells.Has(n)&&cells[n]["terrain"]=="water");Check(cell["waterfront"].Bool==waterfront,"actual-water-neighbor waterfront");if(cell["terrain"]!="water")Check(cell["river_order"].Long==-1,"land outside river order");counts[cell["terrain"].Text]++;}
    Check(cells["0,0"]["terrain"]=="plain"&&cells["0,1"]["terrain"]=="plain"&&Hex.adjacent(V.Arr("0,0"),V.Arr("0,1")),"buildable connected starters");int playable=Component(cells,"0,0");minPlayable=Math.Min(minPlayable,playable);
    var terrain=string.Join("|",cells.Keys.Select(k=>k+":"+cells[k]["terrain"].Text));maps.Add(cells.ToJson());terrainMaps.Add(terrain);rivers.Add(river.ToJson());
    if(i<3||i==127)examples.Add(V.Map("seed",seed,"landing",kind,"cells",cells,"geography",geo));
   }
   Check(terrainMaps.Count>80&&rivers.Count>8,"many distinct terrain/river layouts, not three templates");foreach(var n in counts)Check(n.Value>0,"all natural biomes present");summary.Add(V.Map("landing",kind,"tested_seeds",128,"distinct_maps",maps.Count,"distinct_terrain_maps",terrainMaps.Count,"distinct_rivers",rivers.Count,"minimum_starter_land_component",minPlayable,"biome_cells",V.From(counts)));
  }
  foreach(int kind in new[]{0,1,2}){
   var g=new Game(catalog);g.new_game(8291,1000000);var expectedState=g.s.Duplicate(true);var expected=Rng.shuffled(expectedState,"opening",Rules.ids(catalog.origins));var draft=g.s["rng"]["draft"];var events=g.s["rng"]["events"];
   Check(g.select_block(kind)=="","select seeded landing");Check(g.s["rng"]==expectedState["rng"]&&g.s["rng"]["draft"]==draft&&g.s["rng"]["events"]==events,"geography leaves original opening/draft/events draws unchanged");Check(g.s["origin_choices"]==V.Arr(expected.Take(2).ToArray()),"original origin result preserved");Check(g.choose_origin(g.s["origin_choices"][0].Text)==""&&g.s["cells"]["0,0"]["building"]!=""&&g.s["cells"]["0,1"]["building"]!="","both starter buildings run");
   string map=Store.encode(g.s["cells"]);var decoded=Store.decode(Store.encode(g.s));var loaded=g.migrate_state(decoded["state"]);Check(loaded["ok"].Bool&&Store.encode(g.s["cells"])==map,"saved geography roundtrip uses saved cells");
   // The historical map fixture has no new geography fields. Loading must not generate or replace it.
   foreach(string id in g.s["cells"].Keys){g.s["cells"][id].Remove("elevation01");g.s["cells"][id].Remove("moisture01");g.s["cells"][id].Remove("river_order");}g.s["block"].Remove("geography");map=Store.encode(g.s["cells"]);loaded=g.migrate_state(Store.decode(Store.encode(g.s))["state"]);Check(loaded["ok"].Bool&&Store.encode(g.s["cells"])==map&&!g.s["block"].Has("geography"),"existing save map remains unchanged");
  }
  var evidence=V.Map("scope","pure .NET geography; Godot cross-language/runtime pending","checks",checks,"summary",summary,"parity_examples",examples);if(args.Length>1)File.WriteAllText(args[1],evidence.ToJson());Console.WriteLine(V.Map("checks",checks,"summary",summary).ToJson());return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
