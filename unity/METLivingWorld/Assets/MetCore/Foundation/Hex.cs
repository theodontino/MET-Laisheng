using System;
using System.Collections.Generic;
namespace MET.Core {
public readonly struct HexCoord { public readonly int x,y;public HexCoord(int q,int r){x=q;y=r;} }
public static class Hex {
 public static readonly HexCoord[] DIRS={new HexCoord(1,0),new HexCoord(1,-1),new HexCoord(0,-1),new HexCoord(-1,0),new HexCoord(-1,1),new HexCoord(0,1)};
 static readonly Dictionary<string,HexCoord> coordinateCache=new Dictionary<string,HexCoord>();
 static readonly Dictionary<string,Dictionary<string,int>> distanceCache=new Dictionary<string,Dictionary<string,int>>();static int entries;
 public static void clear_math_cache(){coordinateCache.Clear();distanceCache.Clear();entries=0;}
 public static string key(int q,int r)=>q.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+r.ToString(System.Globalization.CultureInfo.InvariantCulture);
 public static HexCoord coords(string k){if(coordinateCache.TryGetValue(k,out var p))return p;var s=k.Split(',');p=new HexCoord(int.Parse(s[0],System.Globalization.CultureInfo.InvariantCulture),int.Parse(s[1],System.Globalization.CultureInfo.InvariantCulture));if(coordinateCache.Count>=4096)coordinateCache.Clear();coordinateCache[k]=p;return p;}
 public static V neighbors(string k){var p=coords(k);var r=V.Arr();foreach(var d in DIRS)r.Add(key(p.x+d.x,p.y+d.y));return r;}
 public static bool connected(V cells){if(cells.Count==0)return false;var seen=new HashSet<string>{cells[0].Text};var queue=new Stack<string>();queue.Push(cells[0].Text);while(queue.Count>0){var p=queue.Pop();foreach(var n in neighbors(p))if(cells.Contains(n)&&seen.Add(n.Text))queue.Push(n.Text);}return seen.Count==cells.Count;}
 public static bool adjacent(V a,V b){foreach(var p in a)foreach(var n in neighbors(p.Text))if(b.Contains(n))return true;return false;}
 public static V make_map(int kind){var cells=V.Map();for(int q=-4;q<=4;q++)for(int r=-4;r<=4;r++){if(Math.Max(Math.Abs(q),Math.Max(Math.Abs(r),Math.Abs(q+r)))>4)continue;string terrain="plain";if(q==-2&&r>=-2&&r<=3)terrain="water";else if(q==-1&&r==-3)terrain="water";else if(q==0&&r==-4)terrain="water";else if(q>=2&&r<=0)terrain="hill";else if(q==1&&r>=1||q==-3&&r>=1)terrain="forest";else if(q<=-3&&r<=0)terrain="wet";if(kind==1&&q>=1&&terrain=="plain")terrain="forest";if(kind==2&&q>=1&&terrain=="forest")terrain="hill";cells[key(q,r)]=V.Map("q",q,"r",r,"terrain",terrain,"zone","","site","","hidden","");}foreach(var k in cells.Keys){cells[k]["waterfront"]=false;foreach(var n in neighbors(k))if(cells.Has(n)&&cells[n]["terrain"].Text=="water")cells[k]["waterfront"]=true;}return cells;}
 public static int distance(string a,string b){if(distanceCache.TryGetValue(a,out var row)&&row.TryGetValue(b,out int cached))return cached;var x=coords(a);var y=coords(b);int q=x.x-y.x,r=x.y-y.y;int result=Math.Max(Math.Abs(q),Math.Max(Math.Abs(r),Math.Abs(q+r)));if(entries>=8192){distanceCache.Clear();entries=0;}if(!distanceCache.TryGetValue(a,out row)){row=new Dictionary<string,int>();distanceCache[a]=row;}row[b]=result;entries++;return result;}
}
}
