using System;
using System.Collections.Generic;
namespace MET.Core {
public readonly struct HexCoord { public readonly int x,y;public HexCoord(int q,int r){x=q;y=r;} }
public static class Hex {
 public static readonly HexCoord[] DIRS={new HexCoord(1,0),new HexCoord(1,-1),new HexCoord(0,-1),new HexCoord(-1,0),new HexCoord(-1,1),new HexCoord(0,1)};
 [ThreadStatic] static Dictionary<string,HexCoord> coordinates;
 [ThreadStatic] static Dictionary<string,Dictionary<string,int>> distances;
 [ThreadStatic] static int entries;
 static Dictionary<string,HexCoord> coordinateCache=>coordinates??(coordinates=new Dictionary<string,HexCoord>());
 static Dictionary<string,Dictionary<string,int>> distanceCache=>distances??(distances=new Dictionary<string,Dictionary<string,int>>());
 public static void clear_math_cache(){coordinateCache.Clear();distanceCache.Clear();entries=0;}
 public static string key(int q,int r)=>q.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+r.ToString(System.Globalization.CultureInfo.InvariantCulture);
 public static HexCoord coords(string k){if(coordinateCache.TryGetValue(k,out var p))return p;var s=k.Split(',');p=new HexCoord(int.Parse(s[0],System.Globalization.CultureInfo.InvariantCulture),int.Parse(s[1],System.Globalization.CultureInfo.InvariantCulture));if(coordinateCache.Count>=4096)coordinateCache.Clear();coordinateCache[k]=p;return p;}
 public static V neighbors(string k){var p=coords(k);var r=V.Arr();foreach(var d in DIRS)r.Add(key(p.x+d.x,p.y+d.y));return r;}
 public static bool connected(V cells){if(cells.Count==0)return false;var seen=new HashSet<string>{cells[0].Text};var queue=new Stack<string>();queue.Push(cells[0].Text);while(queue.Count>0){var p=queue.Pop();foreach(var n in neighbors(p))if(cells.Contains(n)&&seen.Add(n.Text))queue.Push(n.Text);}return seen.Count==cells.Count;}
 public static bool adjacent(V a,V b){foreach(var p in a)foreach(var n in neighbors(p.Text))if(b.Contains(n))return true;return false;}
 // Geography has its own integer field derivation: it never advances gameplay RNG.
 // Keep these constants/operations identical to core/hex.gd.
 static int Field(long seed,int q,int r,int salt){const long prime=2147483647;long n=((seed%prime)+prime)%prime;n=(n+(q+17)*92821L+(r+17)*68917L+salt*19349663L)%prime;n=n*48271%prime;n=n*n%prime;return (int)(n%1000);}
 static int Smooth(long seed,int q,int r,int salt){int total=Field(seed,q,r,salt);foreach(var d in DIRS)total+=Field(seed,q+d.x,r+d.y,salt);return total/7;}
 static int Radius(int q,int r)=>Math.Max(Math.Abs(q),Math.Max(Math.Abs(r),Math.Abs(q+r)));
 static int Bump(int q,int r,int cq,int cr,int falloff)=>Math.Max(0,1000-Radius(q-cq,r-cr)*falloff);
 public static V make_map(int kind,long seed=0){
     long geoSeed=((seed%2147483647)+2147483647)%2147483647+kind*104729L;
     var river=new List<HexCoord>();int riverQ=Field(geoSeed,0,0,1)%3;
     river.Add(new HexCoord(riverQ,-4));
     // One adjacent water cell per axial row. Alternating the two downhill
     // directions creates bends, while the central starter clearing stays dry.
     for(int r=-3;r<=4;r++){
         bool left=Field(geoSeed,r,0,2)<530;
         if(r<=-1&&riverQ>-1&&riverQ+1>= -r)left=true;
         int next=riverQ-(left?1:0),minimum=Math.Max(-3,-4-r);
         riverQ=Math.Max(minimum,next);if(r>=-1)riverQ=Math.Min(-1,riverQ);
         river.Add(new HexCoord(riverQ,r));
     }
     int ridgeQ=2+Field(geoSeed,0,0,3)%3,ridgeR=-2+Field(geoSeed,0,0,4)%3;
     int northQ=Field(geoSeed,0,0,5)%3,northR=-3;
     int groveQ=1+Field(geoSeed,0,0,6)%2,groveR=1+Field(geoSeed,0,0,7)%2;
     int westQ=-3+Field(geoSeed,0,0,8)%2,westR=Field(geoSeed,0,0,9)%3;
     var cells=V.Map();
     for(int q=-4;q<=4;q++)for(int r=-4;r<=4;r++){
         if(Radius(q,r)>4)continue;
         int nearest=0,riverDistance=99,order=-1;
         for(int i=0;i<river.Count;i++){int d=Radius(q-river[i].x,r-river[i].y);if(d<riverDistance){nearest=i;riverDistance=d;}if(d==0)order=i;}
         int ridge=Math.Max(Bump(q,r,ridgeQ,ridgeR,240),Bump(q,r,northQ,northR,290)*(kind==2?100:65)/100);
         int grove=Math.Max(Bump(q,r,groveQ,groveR,240),Bump(q,r,westQ,westR,270));
         if(kind==1)grove=Math.Max(grove,Bump(q,r,1,0,240));
         int riverHeight=200-nearest*12;
         int height=Math.Min(940,riverHeight+45+riverDistance*45+ridge*42/100+(Smooth(geoSeed,q,r,10)-500)/8);
         int moisture=Math.Max(0,Math.Min(1000,320+Math.Max(0,3-riverDistance)*130+grove*22/100+(Smooth(geoSeed,q,r,11)-500)/4));
         string terrain="plain";
         if(order>=0){terrain="water";height=200-order*12;moisture=1000;}
         else if(ridge>=580)terrain="hill";
         else if(riverDistance==1&&height<430&&moisture>=650)terrain="wet";
         else if(grove+(Smooth(geoSeed,q,r,12)-500)/3>=(kind==1?450:520))terrain="forest";
         if(q==0&&(r==0||r==1))terrain="plain";
         cells[key(q,r)]=V.Map("q",q,"r",r,"terrain",terrain,"zone","","site","","hidden","","elevation01",height/1000.0,"moisture01",moisture/1000.0,"river_order",order);
     }
     foreach(var k in cells.Keys){cells[k]["waterfront"]=false;foreach(var n in neighbors(k))if(cells.Has(n)&&cells[n]["terrain"].Text=="water")cells[k]["waterfront"]=true;}
     return cells;
 }
 public static V geography(V cells,long seed,int kind){
     var river=V.Arr();var ordered=new SortedDictionary<int,string>();foreach(string id in cells.Keys)if(cells[id]["river_order"].Long>=0)ordered[(int)cells[id]["river_order"].Long]=id;
     foreach(string id in ordered.Values)river.Add(id);
     return V.Map("version",1,"seed",seed,"landing",kind,"river_ids",river,"elevation_unit","normalized_0_1");
 }
 public static int distance(string a,string b){if(distanceCache.TryGetValue(a,out var row)&&row.TryGetValue(b,out int cached))return cached;var x=coords(a);var y=coords(b);int q=x.x-y.x,r=x.y-y.y;int result=Math.Max(Math.Abs(q),Math.Max(Math.Abs(r),Math.Abs(q+r)));if(entries>=8192){distanceCache.Clear();entries=0;}if(!distanceCache.TryGetValue(a,out row)){row=new Dictionary<string,int>();distanceCache[a]=row;}row[b]=result;entries++;return result;}
}
}
