using System;
namespace MET.Core {
public static class Rng {
 public static double next(V state,string stream) {var streams=state["rng"];long v=streams.Get(stream,1).Long;v=v*16807%2147483647;streams[stream]=v;return (v-1)/2147483646.0;}
 public static V pick_weighted(V state,string stream,V items,V weights) {if(items.Count==0)return V.Null;double total=0;foreach(var w in weights)total+=Math.Max(w.Double,0);if(total<=0)return items[0];double point=next(state,stream)*total;for(int i=0;i<items.Count;i++){point-=Math.Max(weights[i].Double,0);if(point<0)return items[i];}return items[-1];}
 public static V shuffled(V state,string stream,V source) {var r=source.Duplicate(true);for(int i=r.Count-1;i>0;i--){int j=Math.Min((int)(next(state,stream)*(i+1)),i);var t=r[i];r[i]=r[j];r[j]=t;}return r;}
}
}
