using System;
using System.Linq;
using MET.Core;
class UI11MapProgram {
 static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static void Main() {
  var view=V.Map("tick",9,"source_phase","committed","cells",V.Map(
   "0,0",V.Map("fields",V.Map("employment",V.Arr(V.Map("source_id","workshop","value",2.4),V.Map("source_id","farm","value",2.48)))),
   "1,0",V.Map("fields",V.Map()),"0,1",V.Map("fields",V.Map("employment",V.Arr(V.Map("source_id","workshop","value",4.52))))),
   "projects",V.Map("workshop",V.Map("tile","0,0"),"farm",V.Map("tile","0,1"),"home",V.Map("tile","1,0")),
   "groups",V.Map("residents",V.Map("host","home","count",10,"employment",V.Map("workshop",6))),"metrics",V.Map());
  string before=view.ToJson(false,true);
  var access=MapInspection.Describe(view,"0,0","employment");
  Check(Math.Abs(access["value"].Double-4.88)<1e-9,"workshop without residents preserves 4.88 access strength");
  Check(access["display"].Text=="4.88 可达度"&&access["unit"].Text=="可达度","access readout is not an employment percent");
  Check(access["reading_kind"].Text=="access"&&access["reading"].Text.Contains("不是就业比例"),"access meaning is explicit");
  Check(access["rows"].All(row=>row["unit"].Text=="可达度")&&Math.Abs(access["rows"].Sum(row=>row["value"].Double)-4.88)<1e-9,"source rows retain access units and reconcile");
  Check(access["rows"][0]["source_tile"].Text=="0,0"&&access["rows"][1]["source_tile"].Text=="0,1","access sources retain correct locations");
  var residents=MapInspection.Describe(view,"1,0","employment");
  Check(residents["display"].Text=="60%"&&residents["reading_kind"].Text=="employment_ratio","resident employment keeps population denominator");
  Check(access["neighbors"].Any(row=>row["tile"].Text=="0,1"&&row["display"].Text=="4.52 可达度"),"empty neighbor uses same access units");
  var proposal=view.Duplicate(true);proposal["source_phase"]="proposal";proposal["cells"]["0,0"]["fields"]["employment"][0]["value"]=3.4;
  var delta=MapInspection.Describe(view,"0,0","delta",view,proposal,"employment",false);
  Check(delta["display"].Text=="1 可达度","employment access comparison preserves units");
  proposal["groups"]["newcomer"]=V.Map("host","workshop","count",10,"employment",V.Map("farm",5));
  Check(!MapInspection.Describe(view,"0,0","delta",view,proposal,"employment",false)["available"].Bool,"do not subtract access strength from resident ratio");
  Check(view.ToJson(false,true)==before,"map explanations and comparison do not mutate source snapshot");
  Console.WriteLine("UI11 MAP PASS "+checks+" assertions; no Player visual/input acceptance performed.");
 }
}
