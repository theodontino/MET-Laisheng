using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace MET.Core {
/// <summary>JSON-shaped values. Containers retain references unless explicitly duplicated.</summary>
public sealed class V : IEnumerable<V>, IEquatable<V> {
    readonly object value;
    readonly List<string> keyOrder;
    V(object value) { this.value=value;if(value is Dictionary<string,V> map)keyOrder=map.Keys.ToList(); }
    public static readonly V Null=new V(null);
    public bool IsMap=>value is Dictionary<string,V>;
    public bool IsArray=>value is List<V>;
    public bool IsNumber=>value is long || value is double;
    public bool IsNull=>value==null;
    public bool IsString=>value is string;
    public bool IsBool=>value is bool;
    public bool IsInteger=>value is long;
    Dictionary<string,V> Dict=>(Dictionary<string,V>)value;
    public List<V> Items=>(List<V>)value;
    public List<string> Keys=>new List<string>(keyOrder);
    public List<V> Values=>keyOrder.Select(k=>Dict[k]).ToList();
    public IEnumerable<KeyValuePair<string,V>> Pairs=>keyOrder.Select(k=>new KeyValuePair<string,V>(k,Dict[k]));
    public int Count=>IsMap?Dict.Count:IsArray?Items.Count:0;
    public double Double=>value==null?0:value is bool b?(b?1:0):Convert.ToDouble(value,CultureInfo.InvariantCulture);
    public long Long=>value==null?0:value is double d?(long)d:Convert.ToInt64(value,CultureInfo.InvariantCulture);
    public string Text=>value==null?"<null>":value is bool b?(b?"true":"false"):IsMap||IsArray?ToJson():Convert.ToString(value,CultureInfo.InvariantCulture);
    public bool Bool=>value!=null&&(value is bool b?b:IsNumber?Double!=0:value is string s?s.Length>0:Count>0);
    public V this[string key] { get=>Dict[key]; set {if(!Dict.ContainsKey(key))keyOrder.Add(key);Dict[key]=value??Null;} }
    public V this[int index] { get=>Items[index<0?Items.Count+index:index]; set=>Items[index<0?Items.Count+index:index]=value??Null; }
    public V this[V key] { get=>IsMap?this[key.Text]:this[(int)key.Long]; set { if(IsMap)this[key.Text]=value;else this[(int)key.Long]=value; } }
    public V Get(string key,object defaultValue=null)=>IsMap&&Dict.TryGetValue(key,out var found)?found:From(defaultValue);
    public V Get(V key,object defaultValue=null)=>Get(key.Text,defaultValue);
    public bool Has(string key)=>IsMap&&Dict.ContainsKey(key);
    public bool Has(V key)=>Has(key.Text);
    public void Set(string key,object item)=>this[key]=From(item);
    public bool Remove(string key){bool removed=Dict.Remove(key);if(removed)keyOrder.Remove(key);return removed;}
    public void Add(object item)=>Items.Add(From(item));
    public bool Erase(object item)=>IsMap?Remove(From(item).Text):Items.Remove(From(item));
    public void Clear() { if(IsMap){Dict.Clear();keyOrder.Clear();}else Items.Clear(); }
    public void Merge(V other,bool overwrite=false) { foreach(var pair in other.Pairs)if(overwrite||!Has(pair.Key))this[pair.Key]=pair.Value; }
    public bool Contains(object item)=>IsMap?Has(From(item).Text):Items.Contains(From(item));
    public V Duplicate(bool deep=false) { if(IsMap){var r=Map();foreach(var p in Pairs)r[p.Key]=deep?p.Value.Duplicate(true):p.Value;return r;}if(IsArray){var r=Arr();foreach(var x in Items)r.Add(deep?x.Duplicate(true):x);return r;}return this; }
    public static V Map(params object[] pairs) { var r=new V(new Dictionary<string,V>(StringComparer.Ordinal));for(int i=0;i<pairs.Length;i+=2)r[From(pairs[i]).Text]=From(pairs[i+1]);return r; }
    public static V Arr(params object[] items)=>new V(items.Select(From).ToList());
    public static V From(object item) { if(item==null)return Null;if(item is V v)return v;if(item is JToken t)return FromToken(t);if(item is string||item is bool)return new V(item);if(item is float||item is double||item is decimal)return new V(Convert.ToDouble(item,CultureInfo.InvariantCulture));if(item is byte||item is short||item is int||item is long||item is uint||item is ushort)return new V(Convert.ToInt64(item));if(item is IDictionary dict){var r=Map();foreach(DictionaryEntry p in dict)r[Convert.ToString(p.Key,CultureInfo.InvariantCulture)]=From(p.Value);return r;}if(item is IEnumerable list){var r=Arr();foreach(var x in list)r.Add(x);return r;}throw new ArgumentException("Unsupported MET value: "+item.GetType()); }
    public static V Parse(string json){using(var reader=new JsonTextReader(new System.IO.StringReader(json))){reader.DateParseHandling=DateParseHandling.None;return FromToken(JToken.ReadFrom(reader));}}
    static V FromToken(JToken t) { if(t is JObject o){var r=Map();foreach(var p in o.Properties())r[p.Name]=FromToken(p.Value);return r;}if(t is JArray a){var r=Arr();foreach(var x in a)r.Add(FromToken(x));return r;}if(t.Type==JTokenType.Null)return Null;return From(((JValue)t).Value); }
    public JToken ToToken(bool sortKeys=false) { if(IsMap){var o=new JObject();IEnumerable<string> keys=Keys;if(sortKeys)keys=keys.OrderBy(x=>x,StringComparer.Ordinal);foreach(var k in keys)o[k]=this[k].ToToken(sortKeys);return o;}if(IsArray)return new JArray(Items.Select(x=>x.ToToken(sortKeys)));return value==null?JValue.CreateNull():new JValue(value); }
    public string ToJson(bool pretty=false,bool sortKeys=false)=>ToToken(sortKeys).ToString(pretty?Formatting.Indented:Formatting.None);
    public IEnumerator<V> GetEnumerator()=>IsMap?Keys.Select(x=>From(x)).GetEnumerator():Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    public bool Equals(V other) { if(ReferenceEquals(this,other))return true;if(ReferenceEquals(other,null))return IsNull;if(IsNumber&&other.IsNumber)return Double==other.Double;if(IsMap&&other.IsMap)return Count==other.Count&&Pairs.All(p=>other.Has(p.Key)&&p.Value.Equals(other[p.Key]));if(IsArray&&other.IsArray)return Items.SequenceEqual(other.Items);return object.Equals(value,other.value); }
    public override bool Equals(object other)=>other is V v&&Equals(v);
    public override int GetHashCode()=>IsNumber?Double.GetHashCode():IsMap||IsArray?0:value?.GetHashCode()??0;
    public override string ToString()=>Text;
    public static implicit operator V(string x)=>From(x);public static implicit operator V(int x)=>From(x);public static implicit operator V(long x)=>From(x);public static implicit operator V(double x)=>From(x);public static implicit operator V(bool x)=>From(x);
    public static explicit operator string(V x)=>x.Text;public static explicit operator int(V x)=>(int)x.Long;public static explicit operator long(V x)=>x.Long;public static explicit operator double(V x)=>x.Double;public static explicit operator bool(V x)=>x.Bool;
    public static bool operator ==(V a,V b)=>ReferenceEquals(a,null)?ReferenceEquals(b,null)||b.IsNull:a.Equals(b);
    public static bool operator !=(V a,V b)=>!(a==b);
    public static bool operator <(V a,V b)=>a.IsNumber&&b.IsNumber?a.Double<b.Double:StringComparer.Ordinal.Compare(a.Text,b.Text)<0;
    public static bool operator >(V a,V b)=>b<a;public static bool operator <=(V a,V b)=>!(a>b);public static bool operator >=(V a,V b)=>!(a<b);
    public static V operator +(V a,V b)=>a.IsString||b.IsString?From(a.Text+b.Text):a.IsInteger&&b.IsInteger?From(a.Long+b.Long):From(a.Double+b.Double);
    public static V operator -(V a,V b)=>a.IsInteger&&b.IsInteger?From(a.Long-b.Long):From(a.Double-b.Double);
    public static V operator *(V a,V b)=>a.IsInteger&&b.IsInteger?From(a.Long*b.Long):From(a.Double*b.Double);
    public static V operator /(V a,V b)=>a.IsInteger&&b.IsInteger?From(a.Long/b.Long):From(a.Double/b.Double);
    public static V operator %(V a,V b)=>a.IsInteger&&b.IsInteger?From(a.Long%b.Long):From(a.Double%b.Double);
    public static V operator -(V a)=>a.IsInteger?From(-a.Long):From(-a.Double);
}
}
