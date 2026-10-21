using System;
using System.Globalization;
using System.IO;
using System.Linq;
using MET.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
class JsonProgram {
    static int count,parseCount;
    static string Previous(V value,bool pretty,bool sorted)=>value.ToToken(sorted).ToString(pretty?Formatting.Indented:Formatting.None);
    static void Compare(V value,string name) {
        foreach(bool pretty in new[]{false,true})foreach(bool sorted in new[]{false,true}) {
            var before=Previous(value,pretty,sorted);var after=value.ToJson(pretty,sorted);
            if(before!=after)throw new Exception("JSON differs: "+name+" pretty="+pretty+" sorted="+sorted+"\nold="+before+"\nnew="+after);
            count++;
        }
    }
    static V PreviousParse(string text) {using(var reader=new JsonTextReader(new StringReader(text))){reader.DateParseHandling=DateParseHandling.None;return V.From(JToken.ReadFrom(reader));}}
    static bool SameTypes(V a,V b) {
        if(a.IsMap!=b.IsMap||a.IsArray!=b.IsArray||a.IsInteger!=b.IsInteger||a.IsNumber!=b.IsNumber||a.IsString!=b.IsString||a.IsBool!=b.IsBool||a.IsNull!=b.IsNull)return false;
        if(a.IsMap)return a.Keys.SequenceEqual(b.Keys)&&a.Pairs.All(p=>SameTypes(p.Value,b[p.Key]));
        if(a.IsArray)return a.Count==b.Count&&a.Items.Select((v,i)=>SameTypes(v,b[i])).All(x=>x);
        return !a.IsNumber||a.IsInteger?a.Text==b.Text:BitConverter.DoubleToInt64Bits(a.Double)==BitConverter.DoubleToInt64Bits(b.Double);
    }
    static void CompareParse(string text,string name) {
        V old=null,current=null;Exception oldError=null,currentError=null;
        try{old=PreviousParse(text);}catch(Exception error){oldError=error;}
        try{current=V.Parse(text);}catch(Exception error){currentError=error;}
        if(oldError!=null||currentError!=null){if(oldError?.GetType()!=currentError?.GetType())throw new Exception("Parse exception differs: "+name+" old="+oldError?.GetType()+" new="+currentError?.GetType());}
        else {
            if(!SameTypes(old,current)||old.ToJson(true)!=current.ToJson(true))throw new Exception("Parse value/type/order differs: "+name);
            string canonical=current.ToJson(true,true);if(V.Parse(canonical).ToJson(true,true)!=canonical)throw new Exception("Parse roundtrip differs: "+name);
        }
        parseCount++;
    }
    static V ParseAllocation(V payload,string name) {
        string text=payload.ToJson(false,true);PreviousParse(text);V.Parse(text);
        long start=GC.GetAllocatedBytesForCurrentThread();var old=PreviousParse(text);long previous=GC.GetAllocatedBytesForCurrentThread()-start;
        start=GC.GetAllocatedBytesForCurrentThread();var direct=V.Parse(text);long current=GC.GetAllocatedBytesForCurrentThread()-start;
        if(!SameTypes(old,direct)||current>=previous)throw new Exception("Parse allocation comparison failed: "+name);
        return V.Map("payload",name,"characters",text.Length,"old_thread_allocated_bytes",previous,"direct_thread_allocated_bytes",current,"reduction_percent",100.0*(previous-current)/previous);
    }
    static V Allocation(V payload,string name,bool pretty,bool sorted) {
        // Warm both methods, then measure only synchronous serialization allocations.
        Previous(payload,pretty,sorted);payload.ToJson(pretty,sorted);
        long start=GC.GetAllocatedBytesForCurrentThread();var old=Previous(payload,pretty,sorted);long previous=GC.GetAllocatedBytesForCurrentThread()-start;
        start=GC.GetAllocatedBytesForCurrentThread();var current=payload.ToJson(pretty,sorted);long direct=GC.GetAllocatedBytesForCurrentThread()-start;
        if(old!=current||direct>=previous)throw new Exception("Serialization allocation comparison failed: "+name);
        return V.Map("payload",name,"pretty",pretty,"sorted",sorted,"characters",current.Length,"old_thread_allocated_bytes",previous,"direct_thread_allocated_bytes",direct,"reduction_percent",100.0*(previous-direct)/previous);
    }
    static int Main(string[] args) {
        try {
            var culture=CultureInfo.CurrentCulture;CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");
            foreach(var value in new[]{V.Null,V.From("中文\"\\\n\r\t\b\f\u0000\u2028\u2029😀"),V.From(true),V.From(false),V.From(long.MinValue),V.From(long.MaxValue),V.From(0),V.From(-0.0),V.From(1.0),V.From(1e-30),V.From(1e30),V.From(double.Epsilon),V.From(double.MaxValue),V.From(double.NaN),V.From(double.PositiveInfinity),V.From(double.NegativeInfinity),V.Map(),V.Arr()})Compare(value,"scalar or empty");
            Compare(V.Map("中文\"",V.Arr("\n",V.Null,1.0,true,V.Map("z",2,"a","😀")),"a",V.Arr(1,2),"Z",-1.25),"nested escaping and ordering");
            var catalog=new Catalog(args[0]);
            Compare(catalog.data,"actual catalog");
            var saved=CityStore.read(catalog,args[1]);if(!saved["ok"].Bool||saved["state"]["blocks"].Count!=4)throw new Exception("Expected valid actual four-Block save");
            var city=saved["state"];Compare(city,"actual four-Block save");
            CompareParse(File.ReadAllText(args[1]),"actual four-Block save");CompareParse(catalog.data.ToJson(),"actual catalog");
            foreach(var text in new[]{"null","true","false","0","1","1.0","-0.0","-9223372036854775808","9223372036854775807","1e-30","1e30","\"中文\\n\\\"\\\\😀\"","\"2026-10-14T00:00:00Z\"","{}","[]","{\"a\":1,\"b\":2,\"a\":{\"中文\":[true,null,1.25]}}","{\"a\":/* comment */1}","[1,/* comment */2]","/* root comment */{}","{} trailing",null,"","   ","{","[1,","{\"a\":","{\"a\":1","\"unterminated","{\"a\" 1}","[1 2]","[1,}]","\"\\q\""})CompareParse(text,"ordinary/scalar/duplicate/empty/damaged input");
            foreach(var file in Directory.EnumerateFiles(args[0],"*.json",SearchOption.AllDirectories)){string text=File.ReadAllText(file);Compare(V.Parse(text),file);CompareParse(text,file);}
            CultureInfo.CurrentCulture=culture;
            var report=V.Map("status","passed","exact_output_checks",count,"parse_value_type_order_exception_checks",parseCount,"allocations",V.Arr(Allocation(city,"actual four-Block save",true,false),Allocation(city,"actual four-Block save",false,true),Allocation(catalog.data,"actual catalog",true,false),Allocation(catalog.data,"actual catalog",false,true)),"parse_allocations",V.Arr(ParseAllocation(city,"actual four-Block save"),ParseAllocation(catalog.data,"actual catalog")),"scope","Single-thread .NET serialization/parsing allocations; identical output and parsed values, scalar types, property order and exception classes. These values are not Player RSS.");
            if(args.Length>2){Directory.CreateDirectory(Path.GetDirectoryName(args[2]));File.WriteAllText(args[2],report.ToJson(true));}
            Console.WriteLine(report.ToJson(true));return 0;
        } catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
