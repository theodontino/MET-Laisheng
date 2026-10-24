using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MET.Core {
/// <summary>Read-only, source-aware explanation of the exact scalar used by the city map.</summary>
public static class MapInspection {
    static readonly string[] Services={"service","medical","education","entertainment","commercial"};
    static V Cells(V v)=>v?.Get("cells",v.Get("tiles",V.Map()))??V.Map();
    static V Projects(V v)=>v?.Get("projects",V.Map())??V.Map();
    static V Groups(V v)=>v?.Get("groups",V.Map())??V.Map();
    static V Metrics(V v)=>v?.Get("metrics",V.Map())??V.Map();
    static IEnumerable<string> Ids(V map)=>map.IsMap?map.Keys.OrderBy(x=>x,StringComparer.Ordinal):Enumerable.Empty<string>();
    static string T(V v,string key,string fallback="")=>v.Get(key,fallback).Text;
    static string Resource(string layer)=>layer=="stock"?"food":layer;
    static string ServiceKind(string layer)=>layer=="service"?"medical":layer;
    static bool IsService(string layer)=>Services.Contains(layer,StringComparer.Ordinal);
    static bool IsStock(string layer)=>layer=="stock"||layer=="food"||layer=="material"||layer=="goods";
    static bool Ready(V a,V b)=>a!=null&&b!=null&&a.IsMap&&b.IsMap&&a.Has("tick")&&b.Has("tick")&&a["tick"]==b["tick"]&&Cells(a).Count>0&&Cells(b).Count>0;
    static IEnumerable<string> LocalGroups(V view,string tile) {
        var projects=Projects(view);var groups=Groups(view);
        foreach(var id in Ids(groups))if(T(projects.Get(T(groups[id],"host"),V.Map()),"tile")==tile)yield return id;
    }
    static IEnumerable<string> LocalProjects(V view,string tile) {
        var projects=Projects(view);
        foreach(var id in Ids(projects))if(T(projects[id],"tile")==tile)yield return id;
    }
    public static double Value(V view,string tile,string layer,V baseline=null,V proposal=null,string deltaMetric="pollution") {
        if(!Cells(view).Has(tile??""))return 0;
        if(layer=="delta")return Ready(baseline,proposal)?Value(proposal,tile,deltaMetric)-Value(baseline,tile,deltaMetric):0;
        if(layer=="terrain")return 0;
        var fields=Cells(view)[tile].Get("fields",V.Map());var groups=Groups(view);var metrics=Metrics(view);
        if(layer=="employment") {
            double employed=0,people=0;
            foreach(var gid in LocalGroups(view,tile)){var group=groups[gid];people+=group.Get("count",0).Double;foreach(var count in group.Get("employment",V.Map()).Values)employed+=count.Double;}
            if(people>0)return Math.Min(1,employed/people);
            return fields.Get("employment",V.Arr()).Sum(x=>x.Get("value",0).Double);
        }
        if(layer=="pollution"||layer=="heat")return fields.Get(layer,V.Arr()).Sum(x=>x.Get("value",0).Double);
        if(IsService(layer)) {
            string kind=ServiceKind(layer);double covered=0,people=0;
            foreach(var gid in LocalGroups(view,tile)){people+=groups[gid].Get("count",0).Double;covered+=metrics.Get("service_covered",V.Map()).Get(gid,V.Map()).Get(kind,0).Double;}
            if(people>0)return Math.Min(1,covered/people);
            var bySource=new Dictionary<string,double>(StringComparer.Ordinal);
            foreach(var entry in fields.Get(kind,V.Arr())) {string source=T(entry,"source_id");double value=entry.Get("value",0).Double;bySource[source]=Math.Max(bySource.TryGetValue(source,out var prior)?prior:0,value);}
            return bySource.Count>0?bySource.Values.Max():0;
        }
        double total=0;var projects=Projects(view);
        foreach(var bid in LocalProjects(view,tile)) {
            if(layer=="energy") {
                total+=metrics.Get("energy_sources",V.Map()).Get(bid,0).Double-metrics.Get("energy_requests",V.Map()).Get(bid,0).Double;
                foreach(var gid in Ids(groups))if(T(groups[gid],"host")==bid)total-=metrics.Get("population_energy",V.Map()).Get(gid,0).Double;
            } else total+=projects[bid].Get("inventory",V.Map()).Get(Resource(layer),0).Double;
        }
        return total;
    }
    static string Label(string layer)=>layer switch {"terrain"=>"地貌","service"=>"医疗服务","medical"=>"医疗","education"=>"教育","entertainment"=>"娱乐","commercial"=>"消费","employment"=>"就业","pollution"=>"污染","heat"=>"余热","stock"=>"粮食库存","food"=>"粮食库存","material"=>"材料库存","goods"=>"制成品库存","energy"=>"本格能源净量","delta"=>"同一步变化",_=>layer};
    static string Unit(string layer)=>IsService(layer)||layer=="employment"?"%":layer=="pollution"?"场强":layer=="heat"?"热量":IsStock(layer)?"单位":layer=="energy"?"能源/周期":"";
    static string Format(double n,string unit) {var number=n.ToString(Math.Abs(n)>=100?"0.#":"0.##",CultureInfo.InvariantCulture);return unit=="%"?(n*100).ToString("0.#",CultureInfo.InvariantCulture)+"%":unit==""?number:number+" "+unit;}
    static string Phase(V view)=>view.Get("source_phase",view.Get("forecast",false).Bool?"next_tick":"actual").Text;
    static string SourceTile(V view,string id) {
        if(string.IsNullOrEmpty(id))return "";
        var cells=Cells(view);if(cells.Has(id))return id;
        var projects=Projects(view);if(projects.Has(id))return T(projects[id],"tile");
        var groups=Groups(view);if(groups.Has(id)){var host=T(groups[id],"host");return projects.Has(host)?T(projects[host],"tile"):"";}
        var authored=view.Get("authored_fields",V.Map());if(authored.Has(id)){var origin=T(authored[id],"origin");if(cells.Has(origin))return origin;string source=T(authored[id],"source_id");return source==id?"":SourceTile(view,source);}
        var fields=view.Get("fields",V.Map());if(fields.Has(id)){string source=T(fields[id],"source_id");return source==id?"":SourceTile(view,source);}
        return "";
    }
    static void Add(V rows,string label,double value,string unit,string id,string sourceTile,string kind,string note="")=>rows.Add(V.Map("label",label,"display",Format(value,unit),"value",value,"unit",unit,"source_id",id,"source_tile",sourceTile,"kind",kind,"note",note));
    static void Note(V notes,string value)=>notes.Add(value);
    static V DescribeOne(V view,string tile,string layer,V baseline,V proposal,string deltaMetric) {
        var rows=V.Arr();var notes=V.Arr();var cells=Cells(view);var cell=cells.Get(tile??"",V.Map());var projects=Projects(view);var groups=Groups(view);var metrics=Metrics(view);
        string effective=layer=="delta"?deltaMetric:layer,unit=Unit(effective),label=layer=="delta"?"同一步"+Label(effective)+"变化":Label(layer),reading="";
        bool available=cells.Has(tile??"")&&cell.IsMap;
        if(layer=="delta")available=available&&Ready(baseline,proposal)&&Cells(baseline).Has(tile)&&Cells(proposal).Has(tile)&&Available(baseline,tile,deltaMetric)&&Available(proposal,tile,deltaMetric);
        else available=available&&Available(view,tile,layer);
        double value=available?Value(view,tile,layer,baseline,proposal,deltaMetric):0;
        if(!available&&layer!="terrain")Note(notes,layer=="delta"?"缺少同一步且两侧均可读的基线/方案快照；无法计算变化。":"快照缺少该格或该图层所需字段；读数不可用。");
        if(layer=="terrain"&&cells.Has(tile)) {
            available=false;reading="地形与地块特性；此图层没有数值读数";
            string terrain=T(cell,"terrain","plain");Note(notes,"地形："+terrain);
            foreach(var trait in cell.Get("traits",V.Arr()))Note(notes,"特性："+trait.Text);
        } else if(layer=="delta") {
            reading="同一步方案减基线";
            if(available)Note(notes,"差值按两份快照的图层汇总值计算；跨快照来源变动无法仅从汇总值逐项归因。");
            if(available){var a=Value(baseline,tile,deltaMetric);var b=Value(proposal,tile,deltaMetric);Add(rows,"方案",b,unit,T(proposal,"snapshot_id"),tile,"proposal","方案快照汇总值");Add(rows,"基线",-a,unit,T(baseline,"snapshot_id"),tile,"baseline","基线快照汇总值，按减项显示");}
        } else if(available&&(layer=="pollution"||layer=="heat")) {
            reading="本格场贡献之和";
            foreach(var field in cell.Get("fields",V.Map()).Get(layer,V.Arr())) {
                string id=T(field,"source_id"),fieldId=T(field,"field_id"),via=T(field,"via");
                string location=SourceTile(view,id);if(location==""&&fieldId!="")location=SourceTile(view,fieldId);
                Add(rows,"场来源 "+(id==""?fieldId:id),field.Get("value",0).Double,unit,id,location,"field",via==""?fieldId:"场 "+fieldId+"；经中继 "+via);
                if(location=="")Note(notes,"场 "+fieldId+" 的源位置未随快照提供。");
            }
        } else if(available&&(IsService(layer)||layer=="employment")) {
            string kind=IsService(layer)?ServiceKind(layer):"employment";double people=0,raw=0;var local=LocalGroups(view,tile).ToArray();foreach(var gid in local)people+=groups[gid].Get("count",0).Double;
            if(people>0) {
                reading=IsService(layer)?"居民已交付覆盖率":"居民就业比例";
                foreach(var gid in local) {
                    double n=IsService(layer)?metrics.Get("service_covered",V.Map()).Get(gid,V.Map()).Get(kind,0).Double:groups[gid].Get("employment",V.Map()).Values.Sum(x=>x.Double);
                    raw+=n;Add(rows,"住户 "+gid+" 已"+(IsService(layer)?"覆盖":"就业"),n,"人",gid,tile,"group","住户人数 "+Format(groups[gid].Get("count",0).Double,"人"));
                }
                Add(rows,"居民合计",people,"人","",tile,"denominator");
                V links=metrics.Get(IsService(layer)?"service_links":"employment_links",V.Arr());double linked=0;
                foreach(var link in links) {
                    string gid=T(link,"group");if(!local.Contains(gid,StringComparer.Ordinal)||IsService(layer)&&T(link,"kind")!=kind)continue;
                    double n=link.Get("people",0).Double;linked+=n;string source=IsService(layer)?T(link,"source"):T(link,"target");
                    Add(rows,"关联来源 "+source,n,"人",source,SourceTile(view,source),"link","住户 "+gid+(IsService(layer)?"；来源池可能含多个成员":""));
                }
                if(Math.Abs(linked-raw)>1e-6)Note(notes,"关联线合计与实际"+(IsService(layer)?"覆盖":"就业")+"人数相差 "+Format(raw-linked,"人")+"；以住户实际值为准，未将差额归给任何来源。");
                if(raw>people+1e-6)Note(notes,"比例显示封顶 100%；原始分子大于居民人数。");
            } else {
                reading=IsService(layer)?"空格：最佳服务来源可达度":"空格：就业来源可达度之和";
                var entries=cell.Get("fields",V.Map()).Get(kind,V.Arr());
                if(IsService(layer)) {
                    var bySource=new Dictionary<string,V>(StringComparer.Ordinal);
                    foreach(var entry in entries){string source=T(entry,"source_id");if(!bySource.TryGetValue(source,out var prior)||entry.Get("value",0).Double>prior.Get("value",0).Double)bySource[source]=entry;}
                    foreach(var pair in bySource.OrderBy(x=>x.Key,StringComparer.Ordinal)){string sourceTile=SourceTile(view,pair.Key);Add(rows,"来源 "+pair.Key,pair.Value.Get("value",0).Double,"%",pair.Key,sourceTile,"reach","此来源取最强场；格子总值取各来源最大值");if(sourceTile=="")Note(notes,"来源 "+pair.Key+" 的位置未随快照提供。");}
                } else foreach(var entry in entries){string source=T(entry,"source_id"),sourceTile=SourceTile(view,source);Add(rows,"就业来源 "+source,entry.Get("value",0).Double,"%",source,sourceTile,"reach",T(entry,"field_id"));if(sourceTile=="")Note(notes,"来源 "+source+" 的位置未随快照提供。");}
            }
        } else if(available&&layer=="energy") {
            reading="本格项目供给减项目及住户需求";
            foreach(var bid in LocalProjects(view,tile)) {
                var supply=metrics.Get("energy_sources",V.Map());var requests=metrics.Get("energy_requests",V.Map());
                if(supply.Has(bid))Add(rows,"项目供给",supply[bid].Double,unit,bid,tile,"supply");
                if(requests.Has(bid))Add(rows,"项目需求",-requests[bid].Double,unit,bid,tile,"demand");
                foreach(var gid in Ids(groups))if(T(groups[gid],"host")==bid&&metrics.Get("population_energy",V.Map()).Has(gid))Add(rows,"住户需求",-metrics["population_energy"][gid].Double,unit,gid,tile,"demand");
            }
            if(metrics.Get("energy_sources",V.Map()).Has("municipal"))Note(notes,"市政供给 "+Format(metrics["energy_sources"]["municipal"].Double,unit)+" 记在全城能源账本，不分配到此格。");
            if(rows.Count==0)Note(notes,"此格没有可定位的项目能源来源或需求。");
        } else if(available&&IsStock(layer)) {
            reading="本格项目现存库存";string resource=Resource(layer);
            foreach(var bid in LocalProjects(view,tile)) {var inv=projects[bid].Get("inventory",V.Map());if(inv.Has(resource))Add(rows,"项目库存",inv[resource].Double,unit,bid,tile,"inventory");}
            if(rows.Count==0)Note(notes,"此格没有该资源的项目库存记录。");
        } else if(available) {reading="本格项目库存汇总";foreach(var bid in LocalProjects(view,tile)){var inv=projects[bid].Get("inventory",V.Map());if(inv.Has(layer))Add(rows,"项目库存",inv[layer].Double,unit,bid,tile,"inventory");}}
        return V.Map("tile",tile??"","layer",layer??"","available",available,"value",available?V.From(value):V.Null,"display",available?Format(value,unit):"—","label",label,"unit",unit,"reading",reading,"source_phase",Phase(view),"rows",rows,"notes",notes,"neighbors",V.Arr());
    }
    static bool Available(V view,string tile,string layer) {
        var cells=Cells(view);if(!cells.Has(tile??""))return false;
        if(layer=="terrain")return false;
        if(layer=="pollution"||layer=="heat"||layer=="employment"||IsService(layer)) {
            var local=LocalGroups(view,tile).ToArray();double people=local.Sum(gid=>Groups(view)[gid].Get("count",0).Double);
            if(layer=="employment"&&people>0)return local.All(gid=>Groups(view)[gid].Has("employment"));
            if(IsService(layer)&&people>0)return Metrics(view).Has("service_covered")&&local.All(gid=>Metrics(view)["service_covered"].Has(gid));
            return cells[tile].Has("fields")&&cells[tile]["fields"].IsMap;
        }
        if(layer=="energy")return view.Has("projects")&&view.Has("groups")&&Metrics(view).Has("energy_sources")&&Metrics(view).Has("energy_requests")&&Metrics(view).Has("population_energy");
        return view.Has("projects")&&LocalProjects(view,tile).All(id=>Projects(view)[id].Has("inventory"));
    }
    public static V Describe(V view,string tile,string layer,V baseline=null,V proposal=null,string deltaMetric="pollution",bool includeNeighbors=true) {
        view=view??V.Map();tile=tile??"";layer=layer??"";deltaMetric=deltaMetric??"pollution";
        var result=DescribeOne(view,tile,layer,baseline,proposal,deltaMetric);
        if(includeNeighbors&&Cells(view).Has(tile))foreach(var neighbor in Hex.neighbors(tile)) {
            string id=neighbor.Text;if(!Cells(view).Has(id))continue;
            var item=DescribeOne(view,id,layer,baseline,proposal,deltaMetric);
            result["neighbors"].Add(V.Map("tile",id,"available",item["available"],"value",item["value"],"display",item["display"],"label",item["label"],"unit",item["unit"]));
        }
        return result;
    }
}
}
