using System;
using System.Collections.Generic;

namespace MET.City {
/// <summary>Presentation-only dwell latch. Callers pass a monotonic real-time clock.</summary>
public sealed class DwellIntent {
 readonly HashSet<string> suppressed=new HashSet<string>(StringComparer.Ordinal);
 public double Delay {get;}
 public string Key {get;private set;}="";
 public double ReadyAt {get;private set;}
 public bool Pending {get;private set;}
 public DwellIntent(double delay=2){Delay=Math.Max(0,delay);}
 public bool Begin(string key,double now){
  if(string.IsNullOrEmpty(key)||suppressed.Contains(key))return false;
  if(Key==key)return Pending;
  Key=key;ReadyAt=now+Delay;Pending=true;return true;
 }
 public bool Mature(double now)=>Pending&&now>=ReadyAt;
 public void Presented(){Pending=false;}
 public void Cancel(){Key="";ReadyAt=0;Pending=false;}
 public void Dismiss(){if(Key!="")suppressed.Add(Key);Cancel();}
 public void Suppress(string key){if(!string.IsNullOrEmpty(key))suppressed.Add(key);if(Key==key)Cancel();}
 public void Exit(string key){suppressed.Remove(key);if(Key==key)Cancel();}
 public void Invalidate(string key)=>Exit(key);
 public bool IsSuppressed(string key)=>suppressed.Contains(key??"");
 public void Reset(){Cancel();suppressed.Clear();}
}
public struct PopupBox {
 public double X,Y,Width,Height;
 public PopupBox(double x,double y,double width,double height){X=x;Y=y;Width=Math.Max(0,width);Height=Math.Max(0,height);}
 public double Right=>X+Width;public double Top=>Y+Height;
 public bool Intersects(PopupBox other)=>X<other.Right&&Right>other.X&&Y<other.Top&&Top>other.Y;
 public bool Contains(PopupBox other)=>other.X>=X&&other.Y>=Y&&other.Right<=Right&&other.Top<=Top;
}
/// <summary>Place outside the target and protected HUD rects; minimize overlap if space is insufficient.</summary>
public static class PopupPlacement {
 static double Clamp(double n,double min,double max)=>Math.Max(min,Math.Min(Math.Max(min,max),n));
 static double Overlap(PopupBox a,PopupBox b)=>Math.Max(0,Math.Min(a.Right,b.Right)-Math.Max(a.X,b.X))*Math.Max(0,Math.Min(a.Top,b.Top)-Math.Max(a.Y,b.Y));
 public static PopupBox Place(PopupBox screen,PopupBox anchor,double width,double height,double gap=14,double margin=12,PopupBox[] avoid=null){
  var inside=new PopupBox(screen.X+margin,screen.Y+margin,Math.Max(1,screen.Width-2*margin),Math.Max(1,screen.Height-2*margin));
  width=Math.Min(Math.Max(1,width),inside.Width);height=Math.Min(Math.Max(1,height),inside.Height);
  var candidates=new List<PopupBox>{
   new PopupBox(anchor.X,anchor.Y-gap-height,width,height),
   new PopupBox(anchor.X,anchor.Top+gap,width,height),
   new PopupBox(anchor.Right+gap,anchor.Y+(anchor.Height-height)/2,width,height),
   new PopupBox(anchor.X-gap-width,anchor.Y+(anchor.Height-height)/2,width,height)
  };
  var obstacles=new List<PopupBox>{anchor};
  if(avoid!=null)foreach(var box in avoid)if(box.Width>0&&box.Height>0)obstacles.Add(box);
  if(avoid!=null&&avoid.Length>0){
   // Keep the usual anchor-adjacent placements first, then try each protected
   // region's perimeter. Crossed edge coordinates also find gaps between HUD blocks.
   var xs=new List<double>{anchor.X,inside.X,inside.Right-width};
   var ys=new List<double>{anchor.Y,inside.Y,inside.Top-height};
   foreach(var box in obstacles){
    candidates.Add(new PopupBox(anchor.X,box.Y-gap-height,width,height));
    candidates.Add(new PopupBox(anchor.X,box.Top+gap,width,height));
    candidates.Add(new PopupBox(box.Right+gap,anchor.Y+(anchor.Height-height)/2,width,height));
    candidates.Add(new PopupBox(box.X-gap-width,anchor.Y+(anchor.Height-height)/2,width,height));
    xs.AddRange(new[]{box.X-width-gap,box.Right+gap,box.X-width,box.X,box.Right-width,box.Right});
    ys.AddRange(new[]{box.Y-height-gap,box.Top+gap,box.Y-height,box.Y,box.Top-height,box.Top});
   }
   // Exact edges matter when there is room to avoid a block but not the preferred
   // gap. They are also the breakpoints at which total rectangular overlap can
   // reach its minimum, so the fallback does not assume all blocks can be avoided.
   foreach(var x in xs)foreach(var y in ys)candidates.Add(new PopupBox(x,y,width,height));
  }
  PopupBox best=default(PopupBox);double score=double.PositiveInfinity;
  foreach(var candidate in candidates){
   var clipped=new PopupBox(Clamp(candidate.X,inside.X,inside.Right-width),Clamp(candidate.Y,inside.Y,inside.Top-height),width,height);
   double overlap=0;foreach(var box in obstacles)overlap+=Overlap(clipped,box);
   if(overlap==0)return clipped;
   if(overlap<score){score=overlap;best=clipped;}
  }
  return best;
 }
}
}
