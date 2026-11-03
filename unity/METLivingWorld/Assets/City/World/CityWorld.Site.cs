using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace MET.City {
public sealed partial class CityWorld {
    sealed class SettlementSite {
        public Rect footprint;public Vector3 center;public float floor,relief,clearance;
        public bool construction;public readonly List<Rect> obstacles=new List<Rect>();
        public readonly List<Rect> natureKeepClear=new List<Rect>();
        public Vector2 entrance,offset;public float ringMargin;public string entranceConnection="none";public Vector2[] ring=Array.Empty<Vector2>();
    }
    readonly Dictionary<string,SettlementSite> settlementSites=new Dictionary<string,SettlementSite>(StringComparer.Ordinal);
    readonly Dictionary<string,V> settlementUnreachable=new Dictionary<string,V>(StringComparer.Ordinal);
    readonly Dictionary<string,Vector2Int> settlementAccessCounts=new Dictionary<string,Vector2Int>(StringComparer.Ordinal);
    public V SettlementSiteDiagnostics=>V.Map("terrain_policy","natural_surface_local_supports","primary_sites",settlementSites.Values.Count(s=>!s.construction),
        "slope_repositioned_sites",settlementSites.Values.Count(s=>s.offset.sqrMagnitude>.001f),"slope_supported_sites",settlementSites.Values.Count(s=>s.relief>.028f),"max_footprint_relief",settlementSites.Count==0?0:settlementSites.Values.Max(s=>s.relief),
        "min_floor_clearance",settlementSites.Count==0?0:settlementSites.Values.Min(s=>s.clearance),
        "access_terminal_count",settlementAccessCounts.Values.Sum(s=>s.x),"access_reached_count",settlementAccessCounts.Values.Sum(s=>s.y),
        "unreachable_access_terminals",settlementAccessCounts.Values.Sum(s=>s.x-s.y),"unreachable_details",V.From(settlementUnreachable));
    void PruneSettlementSites() {
        var present=new HashSet<string>(buildings.Values.Select(b=>b.TileId),StringComparer.Ordinal);
        foreach(var tile in settlementSites.Keys.Where(t=>!present.Contains(t)).ToArray()){settlementSites.Remove(tile);settlementAccessCounts.Remove(tile);settlementParcelPaths.Remove(tile);settlementUnreachable.Remove(tile);settlementJunctionCount.Remove("parcel:"+tile);}
    }
    // Called after the official imported geometry has been normalized. Read all
    // mesh bounds in the authored street frame; no simulation position changes.
    void AdaptSettlementBuilding(Transform holder,GameObject imported) {
        bool found=false;var min=Vector3.zero;var max=Vector3.zero;var inverse=Quaternion.Inverse(SettlementFrame);
        foreach(var filter in imported.GetComponentsInChildren<MeshFilter>(true)) {
            if(filter.sharedMesh==null)continue;var bounds=filter.sharedMesh.bounds;
            for(int mask=0;mask<8;mask++) {
                var corner=new Vector3((mask&1)==0?bounds.min.x:bounds.max.x,(mask&2)==0?bounds.min.y:bounds.max.y,(mask&4)==0?bounds.min.z:bounds.max.z);
                var local=inverse*holder.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if(!found){min=max=local;found=true;}else{min=Vector3.Min(min,local);max=Vector3.Max(max,local);}
            }
        }
        AdaptSettlementSite(holder,new Rect(min.x-.008f,min.z-.008f,max.x-min.x+.016f,max.z-min.z+.016f),false);
    }
    void AdaptSettlementConstruction(Transform holder)=>AdaptSettlementSite(holder,new Rect(-.42f,-.42f,.84f,.84f),true);
    void AdaptSettlementSite(Transform holder,Rect footprint,bool construction) {
        var identity=holder.GetComponent<CityBuildingIdentity>();var center=Horizontal(holder.position);var range=SiteGroundRange(center,footprint,SettlementFrame);
        var offset=Vector2.zero;
        if(!construction&&(range.y-range.x>.30f||!SettlementRingFits(identity.TileId,center,footprint,.09f))) {
            var original=footprint;float best=SettlementRingFits(identity.TileId,center,footprint,.09f)?range.y-range.x:float.PositiveInfinity;
            foreach(float radius in new[]{.08f,.16f,.24f,.32f,.40f})for(int i=0;i<12;i++) {
                var shift=new Vector2(Mathf.Cos(i*Mathf.PI/6),Mathf.Sin(i*Mathf.PI/6))*radius;
                var candidate=new Rect(original.x+shift.x,original.y+shift.y,original.width,original.height);
                var corners=new[]{new Vector2(candidate.xMin,candidate.yMin),new Vector2(candidate.xMax,candidate.yMin),new Vector2(candidate.xMax,candidate.yMax),new Vector2(candidate.xMin,candidate.yMax)};
                if(!SettlementRingFits(identity.TileId,center,candidate,.09f))continue;
                var candidateEntrance=center+SettlementOffset(candidate.center.x,candidate.yMax+(ParcelRoadBaselineEvidence?.24f:.045f));
                if(!SettlementPointInTile(candidateEntrance,identity.TileId,.05f)||!DryFabricFootprint(candidateEntrance,.06f))continue;
                var heights=SiteGroundRange(center,candidate,SettlementFrame);float score=heights.y-heights.x+radius*.16f;
                if(score>=best-.015f)continue;best=score;footprint=candidate;range=heights;offset=shift;
            }
        }
        float ringMargin=new[]{.145f,.12f,.105f,.09f}.FirstOrDefault(m=>SettlementRingFits(identity.TileId,center,footprint,m));
        if(ringMargin==0&&!construction)throw new InvalidOperationException("No dry same-hex space for a complete building ring: "+identity.TileId);
        if(ringMargin==0)ringMargin=.09f;
        float floor=range.y+.016f;holder.position=center+SettlementOffset(offset.x,offset.y)+Vector3.up*floor;
        var site=new SettlementSite{center=center,offset=offset,footprint=footprint,floor=floor,relief=range.y-range.x,clearance=floor-range.y,construction=construction,
            entrance=new Vector2(footprint.center.x,footprint.yMax+(ParcelRoadBaselineEvidence?.24f:ringMargin-.045f)),ringMargin=ringMargin,ring=SettlementRing(footprint,ringMargin)};
        if(ParcelRoadBaselineEvidence)site.obstacles.Add(ExpandedSiteRect(footprint,.055f));
        else {
            // The primary doorway has a narrow approach inside the ring. Leave
            // its actual corridor open while retaining clearance around the rest
            // of the building for annex paths and parcel placement.
            var padded=ExpandedSiteRect(footprint,.055f);float top=footprint.yMax+.02f;
            site.obstacles.Add(new Rect(padded.xMin,padded.yMin,padded.width,top-padded.yMin));
            site.obstacles.Add(new Rect(padded.xMin,top,site.entrance.x-.11f-padded.xMin,padded.yMax-top));
            site.obstacles.Add(new Rect(site.entrance.x+.11f,top,padded.xMax-site.entrance.x-.11f,padded.yMax-top));
        }
        settlementSites[identity.TileId]=site;
        BuildSlopeSupport(holder,center,footprint,SettlementFrame,floor,"PrimaryFootprintRetainingSupport");
        BuildSiteEntranceSteps(holder,site);
    }
    static Rect ExpandedSiteRect(Rect rect,float margin)=>new Rect(rect.xMin-margin,rect.yMin-margin,rect.width+margin*2,rect.height+margin*2);
    // Exact maxima/minima of the visible piecewise-linear ground under the
    // rectangle. Clipped triangle vertices include interior hill crests and
    // edge intersections that a four-corner foundation test would miss.
    Vector2 SiteGroundRange(Vector3 center,Rect footprint,Quaternion rotation) {
        var boundary=new[]{center+rotation*new Vector3(footprint.xMin,0,footprint.yMin),center+rotation*new Vector3(footprint.xMax,0,footprint.yMin),
            center+rotation*new Vector3(footprint.xMax,0,footprint.yMax),center+rotation*new Vector3(footprint.xMin,0,footprint.yMax)};
        float low=float.PositiveInfinity,high=float.NegativeInfinity,step=2*LandscapeExtent/LandscapeGridCells;
        int x0=Mathf.Clamp(Mathf.FloorToInt((boundary.Min(p=>p.x)+LandscapeExtent)/step),0,LandscapeGridCells-1),x1=Mathf.Clamp(Mathf.FloorToInt((boundary.Max(p=>p.x)+LandscapeExtent)/step),0,LandscapeGridCells-1);
        int z0=Mathf.Clamp(Mathf.FloorToInt((boundary.Min(p=>p.z)+LandscapeExtent)/step),0,LandscapeGridCells-1),z1=Mathf.Clamp(Mathf.FloorToInt((boundary.Max(p=>p.z)+LandscapeExtent)/step),0,LandscapeGridCells-1);
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++) {
            int cell=z*LandscapeGridCells+x,start=groundCellTriangleStarts[cell],end=start+groundCellTriangleCounts[cell];
            for(int i=start;i<end;i+=3) {
                var polygon=new List<Vector3>{denseSurfaceVertices[visibleGroundTriangles[i]],denseSurfaceVertices[visibleGroundTriangles[i+1]],denseSurfaceVertices[visibleGroundTriangles[i+2]]};
                for(int edge=0;edge<4&&polygon.Count>0;edge++)polygon=ClipToHexEdge(polygon,boundary[edge],boundary[(edge+1)%4]);
                foreach(var point in polygon){low=Mathf.Min(low,point.y);high=Mathf.Max(high,point.y);}
            }
        }
        return new Vector2(low,high);
    }
    float SmallSiteFloor(Vector3 center,Vector2 size,Quaternion rotation)=>SiteGroundRange(center,new Rect(-size.x*.5f,-size.y*.5f,size.x,size.y),rotation).y+.016f;
    void BuildSlopeSupport(Transform parent,Vector3 center,Rect footprint,Quaternion rotation,float floor,string name) {
        var range=SiteGroundRange(center,footprint,rotation);if(range.y-range.x<=.028f)return;
        var corners=new[]{center+rotation*new Vector3(footprint.xMin,0,footprint.yMin),center+rotation*new Vector3(footprint.xMax,0,footprint.yMin),
            center+rotation*new Vector3(footprint.xMax,0,footprint.yMax),center+rotation*new Vector3(footprint.xMin,0,footprint.yMax)};
        var vertices=new List<Vector3>();var triangles=new List<int>();
        Action<Vector3,Vector3,Vector3,Vector3> quad=(a,b,c,d)=>{int i=vertices.Count;vertices.Add(parent.InverseTransformPoint(a));vertices.Add(parent.InverseTransformPoint(b));vertices.Add(parent.InverseTransformPoint(c));vertices.Add(parent.InverseTransformPoint(d));triangles.AddRange(new[]{i,i+2,i+1,i+1,i+2,i+3});};
        quad(new Vector3(corners[0].x,floor-.004f,corners[0].z),new Vector3(corners[1].x,floor-.004f,corners[1].z),new Vector3(corners[3].x,floor-.004f,corners[3].z),new Vector3(corners[2].x,floor-.004f,corners[2].z));
        for(int edge=0;edge<4;edge++) {
            var from=corners[edge];var to=corners[(edge+1)%4];int divisions=Mathf.Max(1,Mathf.CeilToInt(Horizontal(to-from).magnitude/.12f));
            var outward=Vector3.Cross(Vector3.up,Horizontal(to-from).normalized);
            for(int part=0;part<divisions;part++) {
                var a=Vector3.Lerp(from,to,part/(float)divisions);var b=Vector3.Lerp(from,to,(part+1)/(float)divisions);
                float ay=VisualGroundHeight(a)-.009f,by=VisualGroundHeight(b)-.009f;
                // A low projecting course breaks up the downhill retaining face;
                // its bottom follows the slope instead of filling a plot-wide box.
                float course=floor-.11f;
                if(course>Mathf.Max(ay,by)+.04f) {
                    var a0=new Vector3(a.x,ay,a.z)+outward*.012f;var b0=new Vector3(b.x,by,b.z)+outward*.012f;
                    var a1=new Vector3(a0.x,course,a0.z);var b1=new Vector3(b0.x,course,b0.z);
                    quad(a0,b0,a1,b1);quad(a1,b1,new Vector3(a.x,course,a.z),new Vector3(b.x,course,b.z));ay=by=course;
                }
                quad(new Vector3(a.x,ay,a.z),new Vector3(b.x,by,b.z),new Vector3(a.x,floor-.004f,a.z),new Vector3(b.x,floor-.004f,b.z));
            }
        }
        SiteMesh(parent,name,vertices,triangles,SettlementRoadSurface("stone"),true);
    }
    void BuildSiteEntranceSteps(Transform parent,SettlementSite site) {
        var outer=site.center+SettlementOffset(site.entrance.x,site.entrance.y);float ground=VisualGroundHeight(outer)+.026f;
        if(Mathf.Abs(site.floor-ground)<.028f) {
            if(!ParcelRoadBaselineEvidence)BuildSiteEntranceApron(parent,site);
            return;
        }
        site.entranceConnection="stairs";
        int steps=Mathf.Max(2,Mathf.CeilToInt(Mathf.Abs(site.floor-ground)/.045f));
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int i=0;i<steps;i++) {
            float t0=i/(float)steps,t1=(i+1)/(float)steps;
            float z0=Mathf.Lerp(site.entrance.y,site.footprint.yMax-.006f,t0),z1=Mathf.Lerp(site.entrance.y,site.footprint.yMax-.006f,t1);
            var a=site.center+SettlementOffset(site.entrance.x,z0);var b=site.center+SettlementOffset(site.entrance.x,z1);
            var points=new[]{a+SettlementOffset(-.085f,0),a+SettlementOffset(.085f,0),b+SettlementOffset(-.085f,0),b+SettlementOffset(.085f,0)};
            float level=Mathf.Lerp(ground,site.floor,t1);foreach(var p in points)level=Mathf.Max(level,VisualGroundHeight(p)+.004f);
            int n=vertices.Count;foreach(var p in points){var q=p;q.y=level;vertices.Add(parent.InverseTransformPoint(q));}triangles.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2});
            float previous=Mathf.Lerp(ground,site.floor,t0);
            n=vertices.Count;vertices.Add(parent.InverseTransformPoint(new Vector3(a.x,previous,a.z)+SettlementOffset(-.085f,0)));vertices.Add(parent.InverseTransformPoint(new Vector3(a.x,previous,a.z)+SettlementOffset(.085f,0)));
            vertices.Add(parent.InverseTransformPoint(new Vector3(a.x,level,a.z)+SettlementOffset(-.085f,0)));vertices.Add(parent.InverseTransformPoint(new Vector3(a.x,level,a.z)+SettlementOffset(.085f,0)));triangles.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2});
        }
        SiteMesh(parent,"TerrainToDoorEntranceSteps",vertices,triangles,SettlementRoadSurface("stone"),true);
    }
    void BuildSiteEntranceApron(Transform parent,SettlementSite site) {
        // Near-level doors still need paving from the ring's inner approach to
        // the real building threshold; omitting stairs must not omit access.
        site.entranceConnection="apron";
        var vertices=new List<Vector3>();var triangles=new List<int>();
        int segments=Mathf.Max(1,Mathf.CeilToInt((site.entrance.y-site.footprint.yMax+.006f)/.03f));
        for(int i=0;i<=segments;i++) {
            float t=i/(float)segments,z=Mathf.Lerp(site.entrance.y,site.footprint.yMax-.006f,t);
            foreach(float side in new[]{-.085f,.085f}) {
                var p=site.center+SettlementOffset(site.entrance.x+side,z);
                var road=site.center+SettlementOffset(site.entrance.x+side,site.entrance.y);
                p.y=Mathf.Max(Mathf.Lerp(VisualGroundHeight(road)+.026f,site.floor+.002f,t),VisualGroundHeight(p)+.004f);
                vertices.Add(parent.InverseTransformPoint(p));
            }
            if(i>0){int n=vertices.Count-2;triangles.AddRange(new[]{n-2,n-1,n,n-1,n+1,n});}
        }
        SiteMesh(parent,"TerrainToDoorEntranceSteps",vertices,triangles,SettlementRoadSurface("stone"),false);
    }
    void SiteMesh(Transform parent,string name,List<Vector3> vertices,List<int> triangles,Material material,bool shadows) {
        if(triangles.Count==0)return;var mesh=new Mesh{name=name,vertices=vertices.ToArray(),triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
        var root=new GameObject(name);root.transform.SetParent(parent,false);root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=true;
    }
}
}
