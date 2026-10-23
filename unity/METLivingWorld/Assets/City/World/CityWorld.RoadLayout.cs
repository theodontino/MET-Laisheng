using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace MET.City {
public sealed partial class CityWorld {
    readonly List<Vector3[]> settlementRoadPaths=new List<Vector3[]>();
    readonly Dictionary<string,List<Vector3[]>> settlementParcelPaths=new Dictionary<string,List<Vector3[]>>(StringComparer.Ordinal);
    readonly Dictionary<string,Vector3> settlementInfillPositions=new Dictionary<string,Vector3>(StringComparer.Ordinal);
    int relocatedSettlementWings,relocatedSettlementInfill;
    int ringOutsideHexPoints,ringWetPoints,ringPrimaryOverlapPoints,ringExitMismatch;
    string ringAuditSignature="";
    readonly Dictionary<string,int> roundedSharedExits=new Dictionary<string,int>(StringComparer.Ordinal);
    readonly Dictionary<string,int> sharedExitFillets=new Dictionary<string,int>(StringComparer.Ordinal);
    public V SettlementRoadDiagnostics=>V.Map("candidate_neighbors",FabricPlan["connectors"].Count,"drawn_shared_roads",fabricRoutes.Count,
        "rounded_shared_exit_count",settlementParcelPaths.Keys.Sum(t=>roundedSharedExits.TryGetValue(t,out var n)?n:0),"shared_exit_fillets",settlementParcelPaths.Keys.Sum(t=>sharedExitFillets.TryGetValue(t,out var n)?n:0),
        "closed_ring_count",settlementParcelPaths.Values.Count(paths=>paths.Any(p=>p.Length>2&&p[0]==p[p.Length-1])),"ring_policy","complete rounded footprint loop; six hex-direction exits","ring_outside_hex_points",ringOutsideHexPoints,"ring_wet_points",ringWetPoints,"ring_primary_overlap_points",ringPrimaryOverlapPoints,"ring_exit_mismatch_count",ringExitMismatch,
        "selection","relation >= .12 or same operator and relation >= .04; adjacent completed dry parcels only",
        "selected_edges",V.From(fabricRoutes.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray()),"relocated_infill",relocatedSettlementInfill,
        "rounded_path_count",settlementParcelPaths.Values.Sum(p=>p.Count),"junction_count",settlementJunctionCount.Values.Sum(x=>x));
    readonly Dictionary<string,int> settlementJunctionCount=new Dictionary<string,int>(StringComparer.Ordinal);
    static bool SettlementPointInTile(Vector3 point,string tile,float margin) {
        var d=Horizontal(point-TilePosition(tile));float limit=.8660254f-margin;
        for(int side=0;side<3;side++){float a=side*Mathf.PI/3;if(Mathf.Abs(d.x*Mathf.Cos(a)+d.z*Mathf.Sin(a))>limit)return false;}return true;
    }
    static bool IsSolidParcelInfill(V spec)=>spec.Has("parcel")&&spec["index"].Long<2&&spec["kind"].Text!="mixed_court"&&spec["kind"].Text!="residential_walkway";
    IEnumerable<Vector2> SettlementEmptyCandidates(Vector2 desired) {
        yield return desired;
        foreach(float radius in new[]{.54f,.68f,.79f})for(int i=0;i<18;i++){float a=i*Mathf.PI/9;yield return new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius);}
    }
    bool TrySettlementWingSlot(string tile,Vector2 desired,Vector2 half,out Vector2 result) {
        var site=settlementSites[tile];var candidates=SettlementEmptyCandidates(desired).OrderBy(p=>(p-desired).sqrMagnitude);
        foreach(var p in candidates) {
            var footprint=new Rect(p.x-half.x,p.y-half.y,half.x*2,half.y*2);
            if(site.obstacles.Any(r=>r.Overlaps(ExpandedSiteRect(footprint,.02f)))||!SettlementSlotAvoidsReservedRoads(tile,footprint))continue;
            if(new[]{new Vector2(footprint.xMin,footprint.yMin),new Vector2(footprint.xMax,footprint.yMin),new Vector2(footprint.xMax,footprint.yMax),new Vector2(footprint.xMin,footprint.yMax)}.Any(v=>!SettlementPointInTile(site.center+SettlementOffset(v.x,v.y),tile,.025f)))continue;
            var world=site.center+SettlementOffset(p.x,p.y);if(!DryFabricFootprint(world,Mathf.Max(half.x,half.y)))continue;
            // Keep the main entrance free. Its stairs belong to the primary model.
            if(ExpandedSiteRect(footprint,.12f).Contains(site.entrance))continue;
            result=p;if((p-desired).sqrMagnitude>.0001f)relocatedSettlementWings++;return true;
        }
        result=desired;return false;
    }
    Rect SettlementInfillRect(V spec,Vector3 point,float margin) {
        var local=Quaternion.Inverse(SettlementFrame)*Horizontal(point-TilePosition(spec["parcel"].Text));
        var half=Mathf.Abs(local.x)>=Mathf.Abs(local.z)?new Vector2(.065f,.12f):new Vector2(.12f,.065f);
        return ExpandedSiteRect(new Rect(local.x-half.x,local.z-half.y,half.x*2,half.y*2),margin);
    }
    string settlementLayoutSignature="";
    void ResolveSettlementLayout(V plan) {
        // Geography, occupied footprints and visible thresholds determine layout.
        // Numeric economy updates below those thresholds reuse it without pathfinding.
        var signature=V.Arr(terrainKey,V.From(settlementSites.Keys.OrderBy(x=>x,StringComparer.Ordinal).Select(x=>V.Arr(x,SettlementAccessSignature(x))).ToArray()),
            V.From(plan["parcels"].Values.Select(x=>V.Arr(x["tile"],x["stage"],x["kind"],x["completed"])).ToArray()),
            V.From(plan["connectors"].Values.Select(x=>V.Arr(x["id"],x["stage"],x["relation"].Double>=.12||(x["same_operator"].Bool&&x["relation"].Double>=.04))).ToArray())).ToJson();
        if(signature==settlementLayoutSignature)return;settlementLayoutSignature=signature;
        var completed=new HashSet<string>(plan["parcels"].Values.Where(p=>p["completed"].Bool).Select(p=>p["tile"].Text),StringComparer.Ordinal);
        foreach(var tile in settlementParcelPaths.Keys.Where(t=>!completed.Contains(t)).ToArray()){settlementParcelPaths.Remove(tile);settlementAccessCounts.Remove(tile);settlementUnreachable.Remove(tile);}
        settlementInfillPositions.Clear();ResolveSettlementRoads(plan);ResolveSettlementInfill(plan);
    }
    void ResolveSettlementInfill(V plan) {
        settlementInfillPositions.Clear();relocatedSettlementInfill=0;
        foreach(var id in Ids(plan["fillers"])) {
            var spec=plan["fillers"][id];if(!IsSolidParcelInfill(spec))continue;
            string tile=spec["parcel"].Text;var center=TilePosition(tile);var original=FabricFillerPosition(spec);
            var local=Quaternion.Inverse(SettlementFrame)*Horizontal(original-center);var desired=new Vector2(local.x,local.z);
            foreach(var p in SettlementEmptyCandidates(desired).OrderBy(v=>(v-desired).sqrMagnitude)) {
                var point=GroundFabric(center+SettlementOffset(p.x,p.y));if(!SettlementPointInTile(point,tile,.16f)||!DryFabricFootprint(point,.16f))continue;
                var rect=SettlementInfillRect(spec,point,.03f);if(!SettlementSlotAvoidsReservedRoads(tile,rect))continue;
                if(settlementSites.TryGetValue(tile,out var site)&&(site.obstacles.Any(r=>r.Overlaps(rect))||ExpandedSiteRect(rect,.14f).Contains(site.entrance)))continue;
                var entrance=FabricFillerEntrance(spec,point);var door=Quaternion.Inverse(SettlementFrame)*Horizontal(entrance-center);var door2=new Vector2(door.x,door.z);
                if(!DryFabricFootprint(entrance,.045f)||!SettlementPointInTile(entrance,tile,.045f)||SettlementRoadObstacles(tile).Any(r=>StrictlyInsideSite(door2,r)))continue;
                if(settlementInfillPositions.Any(pair=>{var other=FabricPlan["fillers"][pair.Key];if(other["parcel"].Text!=tile)return false;var e=Quaternion.Inverse(SettlementFrame)*Horizontal(FabricFillerEntrance(other,pair.Value)-center);return ExpandedSiteRect(rect,.075f).Contains(new Vector2(e.x,e.z));}))continue;
                if(settlementInfillPositions.Values.Any(v=>Horizontal(v-point).sqrMagnitude<.29f*.29f))continue;
                if(!SettlementInfillReachable(spec,point))continue;
                settlementInfillPositions[id]=point;if(Horizontal(point-original).sqrMagnitude>.0001f)relocatedSettlementInfill++;break;
            }
        }
    }
    bool SettlementInfillReachable(V candidate,Vector3 point) {
        string tile=candidate["parcel"].Text;var center=TilePosition(tile);var inverse=Quaternion.Inverse(SettlementFrame);
        var obstacles=SettlementRoadObstacles(tile);obstacles.Add(SettlementInfillRect(candidate,point,.075f));var targets=new List<Vector2>();
        var door=inverse*Horizontal(FabricFillerEntrance(candidate,point)-center);targets.Add(new Vector2(door.x,door.z));
        foreach(var pair in settlementInfillPositions) {
            var spec=FabricPlan["fillers"][pair.Key];if(spec["parcel"].Text!=tile)continue;
            var local=inverse*Horizontal(FabricFillerEntrance(spec,pair.Value)-center);targets.Add(new Vector2(local.x,local.z));
        }
        var graph=SettlementVisibility(tile,center,settlementSites[tile].entrance,targets,obstacles,false);
        return graph.terminals.All(t=>!float.IsPositiveInfinity(graph.distance[t]));
    }
    string SettlementInfillSignature(string tile)=>V.From(settlementInfillPositions.Where(x=>FabricPlan["fillers"][x.Key]["parcel"].Text==tile).Select(x=>V.Arr(x.Key,x.Value.x,x.Value.z)).ToArray()).ToJson();
    List<Rect> SettlementRoadObstacles(string tile) {
        var obstacles=settlementSites.TryGetValue(tile,out var site)?new List<Rect>(site.obstacles):new List<Rect>();
        foreach(var pair in settlementInfillPositions){var spec=FabricPlan["fillers"][pair.Key];if(spec["parcel"].Text==tile)obstacles.Add(SettlementInfillRect(spec,pair.Value,.075f));}
        return obstacles;
    }
    void ResolveSettlementRoads(V plan) {
        fabricRoutes.Clear();
        foreach(var id in Ids(plan["connectors"])) {
            var edge=plan["connectors"][id];double relation=edge["relation"].Double;
            if(relation<.12&&!(edge["same_operator"].Bool&&relation>=.04))continue;
            string a=edge["a"].Text,b=edge["b"].Text;
            if(!settlementSites.TryGetValue(a,out var first)||!settlementSites.TryGetValue(b,out var second)||first.construction||second.construction)continue;
            float half=(.09f+.009f*(int)edge["stage"].Long)*.5f;
            var from=RingPortal(a,b);var to=RingPortal(b,a);var middle=(TilePosition(a)+TilePosition(b))*.5f;
            var points=Enumerable.Range(0,25).Select(i=>GroundFabric(i<=12?Vector3.Lerp(from,middle,i/12f):Vector3.Lerp(middle,to,(i-12)/12f))).ToArray();
            // The same world endpoints are consumed by the closed parcel rings.
            points[0]=from;points[points.Length-1]=to;
            if(points.Any(p=>!DryFabricFootprint(p,half)))continue;
            bool blocked=false;
            foreach(var tile in new[]{a,b}) {
                var inverse=Quaternion.Inverse(SettlementFrame);var center=TilePosition(tile);
                for(int i=1;i<points.Length&&!blocked;i++) {
                    var p=inverse*Horizontal(points[i-1]-center);var q=inverse*Horizontal(points[i]-center);
                    blocked=SettlementRoadObstacles(tile).Any(r=>SegmentEntersSite(new Vector2(p.x,p.z),new Vector2(q.x,q.z),r));
                }
            }
            if(!blocked)fabricRoutes[id]=points;
        }
    }
    void CollectSettlementRoadPaths() {
        if(ringAuditSignature!=settlementLayoutSignature) {
            ringAuditSignature=settlementLayoutSignature;
        ringOutsideHexPoints=ringWetPoints=ringPrimaryOverlapPoints=ringExitMismatch=0;
        foreach(var entry in settlementParcelPaths) {
            if(!settlementSites.TryGetValue(entry.Key,out var site))continue;
            foreach(var point in entry.Value.Where(p=>p.Length>2&&p[0]==p[p.Length-1]).SelectMany(p=>p)) {
                if(!SettlementPointInTile(point,entry.Key,.035f))ringOutsideHexPoints++;
                if(!DryFabricFootprint(point,.035f))ringWetPoints++;
                var local=Quaternion.Inverse(SettlementFrame)*Horizontal(point-site.center);
                if(StrictlyInsideSite(new Vector2(local.x,local.z),site.footprint))ringPrimaryOverlapPoints++;
            }
        }
        foreach(var id in fabricRoutes.Keys) {
            var edge=FabricPlan["connectors"][id];var path=fabricRoutes[id];
            foreach(var pair in new[]{Tuple.Create(edge["a"].Text,path[0]),Tuple.Create(edge["b"].Text,path[path.Length-1])})
                if(!settlementParcelPaths.TryGetValue(pair.Item1,out var paths)||!paths.Any(p=>p.Length>2&&p[0]==p[p.Length-1]&&p.Contains(pair.Item2)))ringExitMismatch++;
        }
        }
        settlementRoadPaths.Clear();settlementRoadPaths.AddRange(fabricRoutes.Values);
        foreach(var paths in settlementParcelPaths.Values)settlementRoadPaths.AddRange(paths);
        foreach(var pair in settlementInfillPositions){var spec=FabricPlan["fillers"][pair.Key];settlementRoadPaths.Add(new[]{pair.Value,FabricFillerEntrance(spec,pair.Value)});}
    }
    List<Vector3[]> RoundedSettlementTree(List<Vector2> nodes,HashSet<int> edges,int count,Vector3 center,List<Rect> obstacles,HashSet<int> terminals) {
        var neighbors=Enumerable.Range(0,count).Select(_=>new List<int>()).ToArray();foreach(int e in edges){neighbors[e/count].Add(e%count);neighbors[e%count].Add(e/count);}
        var used=new HashSet<int>();var chains=new List<Vector3[]>();
        foreach(int start in Enumerable.Range(0,count).Where(i=>neighbors[i].Count!=2||terminals.Contains(i)))foreach(int next in neighbors[start]) {
            int key=Mathf.Min(start,next)*count+Mathf.Max(start,next);if(!used.Add(key))continue;
            var chain=new List<Vector2>{nodes[start]};int previous=start,current=next;
            while(true){chain.Add(nodes[current]);if(neighbors[current].Count!=2||terminals.Contains(current))break;int to=neighbors[current][0]==previous?neighbors[current][1]:neighbors[current][0];used.Add(Mathf.Min(current,to)*count+Mathf.Max(current,to));previous=current;current=to;}
            var rounded=new List<Vector2>{chain[0]};
            for(int i=1;i+1<chain.Count;i++) {
                var p=chain[i];float trim=Mathf.Min(.16f,Vector2.Distance(p,chain[i-1])*.38f,Vector2.Distance(p,chain[i+1])*.38f);
                Vector2[] curve=null;
                for(int attempt=0;attempt<8;attempt++,trim*=.5f) {
                    var incoming=p+(chain[i-1]-p).normalized*trim;var outgoing=p+(chain[i+1]-p).normalized*trim;
                    curve=Enumerable.Range(0,7).Select(j=>{float t=j/6f;return (1-t)*(1-t)*incoming+2*(1-t)*t*p+t*t*outgoing;}).ToArray();
                    if(curve.All(v=>!obstacles.Any(r=>StrictlyInsideSite(v,ExpandedSiteRect(r,-.01f)))&&DryFabricFootprint(center+SettlementOffset(v.x,v.y),.045f)))break;
                }
                rounded.AddRange(curve);
            }
            rounded.Add(chain[chain.Count-1]);var points=new List<Vector3>();
            for(int i=0;i+1<rounded.Count;i++){int samples=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(rounded[i],rounded[i+1])/.10f));for(int j=0;j<samples;j++){var p=Vector2.Lerp(rounded[i],rounded[i+1],j/(float)samples);points.Add(GroundFabric(center+SettlementOffset(p.x,p.y)));}}
            var last=rounded[rounded.Count-1];points.Add(GroundFabric(center+SettlementOffset(last.x,last.y)));chains.Add(points.ToArray());
        }
        return chains;
    }
    // Fill the two inside corners between the outgoing road and the ring with
    // tangent quadratic curves. Keep these in the existing parcel road mesh.
    bool AddSharedExitFillet(Transform parent,Vector3 point,Vector3 first,float firstHalf,Vector3 second,float secondHalf,float trim,List<Vector3> vertices,List<int> triangles) {
        first=Horizontal(first).normalized;second=Horizontal(second).normalized;
        if(Vector3.Cross(first,second).y>0){var swap=first;first=second;second=swap;float half=firstHalf;firstHalf=secondHalf;secondHalf=half;}
        var a=new Vector2(first.x,first.z);var b=new Vector2(second.x,second.z);float denominator=RingCross(a,b);
        if(denominator<.05f)return false;
        var startEdge=new Vector2(-a.y,a.x)*firstHalf;var endEdge=new Vector2(b.y,-b.x)*secondHalf;
        var corner=startEdge+a*(RingCross(endEdge-startEdge,b)/denominator);
        var start=corner+a*trim;var end=corner+b*trim;int n=vertices.Count;
        vertices.Add(parent.InverseTransformPoint(point));
        for(int i=0;i<=12;i++) {
            float t=i/12f;var local=(1-t)*(1-t)*start+2*(1-t)*t*corner+t*t*end;
            vertices.Add(parent.InverseTransformPoint(point+new Vector3(local.x,0,local.y)));
            if(i>0)triangles.AddRange(new[]{n,n+i+1,n+i});
        }
        return true;
    }
    void DrawSettlementPaths(Transform parent,List<Vector3[]> paths,float width,string surface,string tile,Vector3[] portals) {
        var vertices=new List<Vector3>();var triangles=new List<int>();var endpoints=new Dictionary<Vector3,int>();
        foreach(var path in paths) {
            for(int i=0;i<path.Length;i++) {
                bool closed=path.Length>2&&path[0]==path[path.Length-1];
                var tangent=closed&&(i==0||i==path.Length-1)?Horizontal(path[1]-path[path.Length-2]).normalized:Horizontal(path[Mathf.Min(path.Length-1,i+1)]-path[Mathf.Max(0,i-1)]).normalized;var across=Vector3.Cross(Vector3.up,tangent)*width*.5f;
                int n=vertices.Count;vertices.Add(parent.InverseTransformPoint(path[i]-across));vertices.Add(parent.InverseTransformPoint(path[i]+across));
                if(i>0)triangles.AddRange(new[]{n-2,n,n-1,n-1,n,n+1});
            }
            if(path[0]!=path[path.Length-1])foreach(var point in new[]{path[0],path[path.Length-1]})endpoints[point]=endpoints.TryGetValue(point,out var number)?number+1:1;
        }
        // Rounded mouths close seams; shared branch mouths become a small junction.
        foreach(var endpoint in endpoints) {
            float radius=width*(endpoint.Value>=3?.95f:.55f);int n=vertices.Count;vertices.Add(parent.InverseTransformPoint(endpoint.Key));
            for(int i=0;i<16;i++){float a=i*Mathf.PI/8;vertices.Add(parent.InverseTransformPoint(endpoint.Key+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius)));}
            for(int i=0;i<16;i++)triangles.AddRange(new[]{n,n+1+(i+1)%16,n+1+i});
        }
        int mouths=0,fillets=0;var ring=paths[0];
        foreach(var portal in portals) {
            int at=Array.IndexOf(ring,portal);if(at<0)continue;
            int previous=at==0?ring.Length-2:at-1,next=at==ring.Length-1?1:at+1;
            foreach(var edgeId in fabricRoutes.Keys) {
                var route=fabricRoutes[edgeId];Vector3 outgoing;
                if(route[0]==portal)outgoing=route[1]-portal;else if(route[route.Length-1]==portal)outgoing=route[route.Length-2]-portal;else continue;
                float roadWidth=.09f+.009f*(int)FabricPlan["connectors"][edgeId]["stage"].Long;int added=0;
                foreach(var direction in new[]{ring[previous]-portal,ring[next]-portal})
                    if(AddSharedExitFillet(parent,portal,direction,width*.5f,outgoing,roadWidth*.5f,Mathf.Min(width,roadWidth)*.85f,vertices,triangles)){added++;fillets++;}
                if(added==2)mouths++;break;
            }
        }
        roundedSharedExits[tile]=mouths;sharedExitFillets[tile]=fillets;
        var id=parent.GetComponent<CityFabricIdentity>().VisualId;settlementJunctionCount[id]=endpoints.Count(x=>x.Value>=3)+mouths;
        if(triangles.Count==0)return;
        var mesh=new Mesh{name="RoundedEntranceTreeAndJunctions",vertices=vertices.ToArray(),triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
        var root=new GameObject("MergedParcelEntranceAndSideLanes");root.transform.SetParent(parent,false);root.AddComponent<MeshFilter>().sharedMesh=mesh;ConformToVisibleGround(mesh,root.transform,.026f);
        var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=SettlementRoadSurface(surface);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
    }
}
}
