using System;
using System.Linq;
namespace MET.Core {
public static class Inventory {
 public static string signature(string resource,V tags)=>resource+"|";
 public static double add(V stock,string resource,V tags,double qty,double cap){double accepted=Math.Max(0,Math.Min(qty,cap-total(stock,resource)));string k=signature(resource,tags);stock[k]=stock.Get(k,0.0).Double+accepted;return accepted;}
 public static bool matches(string k,string resource,V required)=>k.Split('|')[0]==resource;
 public static double total(V stock,string resource,V required=null){double n=0;foreach(var k in stock.Keys)if(matches(k,resource,required))n+=stock[k].Double;return n;}
 public static V take(V stock,string resource,V required,double quantity){var result=V.Map("taken",0.0,"batches",V.Map());foreach(var k in stock.Keys.OrderBy(k=>k.Length).ThenBy(k=>k,StringComparer.Ordinal)){if(!matches(k,resource,required))continue;double x=Math.Min(stock[k].Double,Math.Max(0,quantity-result["taken"].Double));if(x>0){stock[k]=stock[k].Double-x;result["batches"][k]=x;result["taken"]=result["taken"].Double+x;}if(result["taken"].Double>=quantity-0.000001)break;}return result;}
 public static void refund(V stock,V batches,double fraction){foreach(var k in batches.Keys)stock[k]=stock.Get(k,0.0).Double+batches[k].Double*fraction;}
 public static double consume_recipe(V stock,V recipe,double scale){if(recipe.Count==0||scale<=0)return 1;var ordered=V.From(recipe.Items.OrderByDescending(x=>x["tags"].Count).Select(x=>x.Duplicate(true)).ToList());double upper=1;foreach(var input in ordered){double need=input["rate"].Double*scale;if(need>0)upper=Math.Min(upper,total(stock,input["resource"].Text,input.Get("tags",V.Arr()))/need);}upper=Math.Max(0,Math.Min(1,upper));var trial=_try_recipe(stock,ordered,scale*upper);if(trial["ok"].Bool){stock.Clear();stock.Merge(trial["stock"],true);return upper;}double low=0,high=upper;for(int i=0;i<16;i++){double middle=(low+high)*.5;trial=_try_recipe(stock,ordered,scale*middle);if(trial["ok"].Bool)low=middle;else high=middle;}trial=_try_recipe(stock,ordered,scale*low);stock.Clear();stock.Merge(trial["stock"],true);return low;}
 public static V _try_recipe(V stock,V recipe,double scale){var temp=stock.Duplicate();foreach(var input in recipe){double need=input["rate"].Double*scale;var removed=take(temp,input["resource"].Text,input.Get("tags",V.Arr()),need);if(removed["taken"].Double+0.00000001<need)return V.Map("ok",false,"stock",temp);}return V.Map("ok",true,"stock",temp);}
}
}
