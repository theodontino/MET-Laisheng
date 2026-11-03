using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
namespace MET.City {
public sealed partial class CityWorld {
    const float SettlementRingOffset=.145f;
    static Vector2[] SettlementRing(Rect footprint,float margin=SettlementRingOffset) {
        var points=new List<Vector2>();
        var corners=new[]{new Vector2(footprint.xMax,footprint.yMax),new Vector2(footprint.xMin,footprint.yMax),new Vector2(footprint.xMin,footprint.yMin),new Vector2(footprint.xMax,footprint.yMin)};
        for(int corner=0;corner<4;corner++) {
            for(int i=0;i<=12;i++) {
                float angle=(corner*90+i*7.5f)*Mathf.Deg2Rad;
                points.Add(corners[corner]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*margin);
            }
        }
        points.Add(points[0]);return points.ToArray();
    }
    bool SettlementRingFits(string tile,Vector3 center,Rect footprint,float margin=SettlementRingOffset) {
        var ring=SettlementRing(footprint,margin);
        for(int i=1;i<ring.Length;i++) {
            int samples=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(ring[i-1],ring[i])/.06f));
            for(int j=0;j<=samples;j++) {
                var p=Vector2.Lerp(ring[i-1],ring[i],j/(float)samples);var world=center+SettlementOffset(p.x,p.y);
                if(!SettlementPointInTile(world,tile,.045f)||!DryFabricFootprint(world,.045f))return false;
            }
        }
        return true;
    }
    static float RingCross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    Vector2 RingPointAlong(SettlementSite site,Vector3 worldDirection) {
        var vector=Quaternion.Inverse(SettlementFrame)*Horizontal(worldDirection).normalized;var direction=new Vector2(vector.x,vector.z);
        var origin=site.footprint.center;
        for(int i=1;i<site.ring.Length;i++) {
            var a=site.ring[i-1];var segment=site.ring[i]-a;float denominator=RingCross(direction,segment);
            if(Mathf.Abs(denominator)<.000001f)continue;
            float distance=RingCross(a-origin,segment)/denominator,t=RingCross(a-origin,direction)/denominator;
            if(distance>=0&&t>=-.00001f&&t<=1.00001f)return a+segment*Mathf.Clamp01(t);
        }
        return site.ring[0];
    }
    Vector3 RingPortal(string tile,string other) {
        var site=settlementSites[tile];var local=RingPointAlong(site,TilePosition(other)-TilePosition(tile));
        return GroundFabric(site.center+SettlementOffset(local.x,local.y));
    }
    bool SettlementSlotAvoidsReservedRoads(string tile,Rect footprint) {
        if(!settlementSites.TryGetValue(tile,out var site)||site.construction)return true;
        var expanded=ExpandedSiteRect(footprint,.06f);
        for(int i=1;i<site.ring.Length;i++)if(SegmentEntersSite(site.ring[i-1],site.ring[i],expanded))return false;
        // Reserve the six short outgoing corridors before secondary volumes grow.
        for(int side=0;side<6;side++) {
            float angle=side*Mathf.PI/3;var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var portal=RingPointAlong(site,direction);var local=Quaternion.Inverse(SettlementFrame)*(direction*.8660254f);
            if(SegmentEntersSite(portal,new Vector2(local.x,local.z),expanded))return false;
        }
        return true;
    }
    List<Vector3[]> SettlementRingAccess(string tile,Vector3 center,SettlementSite site,List<Rect> obstacles,List<Vector2> doors,Vector3[] portals,out int reached) {
        var ring=new List<Vector3>();var inverse=Quaternion.Inverse(SettlementFrame);
        // Use the cross-road's exact world vectors at active junctions. No second
        // offset/frame transform is allowed to introduce a separate endpoint.
        for(int i=1;i<site.ring.Length;i++) {
            var a=site.ring[i-1];var b=site.ring[i];var segment=b-a;
            var extra=portals.Select(world=>new{world,local=inverse*Horizontal(world-center)}).Where(p=>{
                var local=new Vector2(p.local.x,p.local.z);float t=Vector2.Dot(local-a,segment)/segment.sqrMagnitude;
                return t>0&&t<1&&(local-a-segment*t).sqrMagnitude<.0000001f;
            }).OrderBy(p=>(new Vector2(p.local.x,p.local.z)-a).sqrMagnitude);
            var start=GroundFabric(center+SettlementOffset(a.x,a.y));
            foreach(var portal in portals)if(Horizontal(portal-start).sqrMagnitude<.0000000001f){start=portal;break;}
            ring.Add(start);foreach(var p in extra)if(Horizontal(p.world-ring[ring.Count-1]).sqrMagnitude>.0000000001f)ring.Add(p.world);
        }
        ring.Add(ring[0]);var paths=new List<Vector3[]>{ring.ToArray()};reached=0;
        foreach(var door in doors) {
            // Connect each actual doorway to its nearest reachable point on the
            // complete ring. Reuse visibility only for this short access branch.
            var targets=site.ring.Take(site.ring.Length-1).ToList();
            if(!ParcelRoadBaselineEvidence)for(int i=1;i<site.ring.Length;i++) {
                var a=site.ring[i-1];var segment=site.ring[i]-a;
                var point=a+segment*Mathf.Clamp01(Vector2.Dot(door-a,segment)/segment.sqrMagnitude);
                if(targets.All(p=>(p-point).sqrMagnitude>.0000001f))targets.Add(point);
            }
            targets=targets.OrderBy(p=>(p-door).sqrMagnitude).ToList();
            var graph=SettlementVisibility(tile,center,door,targets,obstacles);
            int endpoint=graph.terminals.Where(t=>!float.IsPositiveInfinity(graph.distance[t])).OrderBy(t=>graph.distance[t]).DefaultIfEmpty(-1).First();
            if(endpoint<0)continue;reached++;
            var indices=new List<int>();for(int at=endpoint;at>=0;at=graph.previous[at])indices.Add(at);indices.Reverse();
            var edges=new HashSet<int>();for(int i=1;i<indices.Count;i++)edges.Add(Mathf.Min(indices[i-1],indices[i])*graph.nodes.Count+Mathf.Max(indices[i-1],indices[i]));
            paths.AddRange(RoundedSettlementTree(graph.nodes,edges,graph.nodes.Count,center,obstacles,new HashSet<int>{0,endpoint}));
        }
        return paths;
    }
}
}
