using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
namespace MET.City {
public sealed partial class CityWorld {
    struct NatureCircle {public Vector3 point;public float radius;public NatureCircle(Vector3 p,float r){point=p;radius=r;}}
    readonly List<NatureCircle> settlementNatureReserved=new List<NatureCircle>();
    int natureOriginalRetained,natureRelocated,natureUnplaced,natureOccupiedOverlap,natureOutsideSourceTile;
    // Presentation occupancy only: no parcel-wide exclusion and no simulation writes.
    bool SettlementSpaceAvailable(Vector3 point,float radius) {
        var inverse=Quaternion.Inverse(SettlementFrame);
        foreach(var site in settlementSites.Values) {
            var local=inverse*Horizontal(point-site.center);
            foreach(var rect in site.obstacles.Concat(site.natureKeepClear)) {
                float dx=local.x-Mathf.Clamp(local.x,rect.xMin,rect.xMax);
                float dz=local.z-Mathf.Clamp(local.z,rect.yMin,rect.yMax);
                if(dx*dx+dz*dz<radius*radius)return false;
            }
        }
        foreach(var position in settlementInfillPositions.Values)
            if(Horizontal(position-point).sqrMagnitude<(radius+.14f)*(radius+.14f))return false;
        foreach(var path in settlementRoadPaths)for(int i=1;i<path.Length;i++) {
            var a=Horizontal(path[i-1]);var delta=Horizontal(path[i]-path[i-1]);
            float t=delta.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(Horizontal(point)-a,delta)/delta.sqrMagnitude):0;
            if((Horizontal(point)-a-delta*t).sqrMagnitude<(radius+.085f)*(radius+.085f))return false;
        }
        return true;
    }
    bool InsideNatureTile(string tile,Vector3 point) {
        var local=Horizontal(point-TilePosition(tile));
        const float apothem=.8660254f;
        for(int i=0;i<6;i++) {
            float angle=i*60*Mathf.Deg2Rad;
            if(local.x*Mathf.Cos(angle)+local.z*Mathf.Sin(angle)>apothem)return false;
        }
        return true;
    }
    bool NatureReservedSpace(Vector3 point,float radius) {
        foreach(var circle in settlementNatureReserved)
            if(Horizontal(point-circle.point).sqrMagnitude<(radius+circle.radius)*(radius+circle.radius))return false;
        return true;
    }
    bool NatureRelocationGround(string tile,Vector3 point,float radius) {
        if(!InsideNatureTile(tile,point)||VisualFabric.Water(Cells(snapshot)[tile])||!DryFabricFootprint(point,Mathf.Min(radius,.20f)))return false;
        const float sample=.08f;
        float dx=(VisualGroundHeight(point+Vector3.right*sample)-VisualGroundHeight(point-Vector3.right*sample))/(2*sample);
        float dz=(VisualGroundHeight(point+Vector3.forward*sample)-VisualGroundHeight(point-Vector3.forward*sample))/(2*sample);
        return dx*dx+dz*dz<=.65f*.65f;
    }
    bool TryPlaceSettlementNature(string tile,Vector3 desired,float radius,out Vector3 point) {
        point=desired;
        if(NatureRelocationGround(tile,desired,radius)&&SettlementSpaceAvailable(desired,radius)&&NatureReservedSpace(desired,radius)) {
            point.y=VisualGroundHeight(point);settlementNatureReserved.Add(new NatureCircle(point,radius));return true;
        }
        var center=TilePosition(tile);var candidates=new List<Vector3>();
        string seed=tile+":"+Mathf.RoundToInt(desired.x*10000)+":"+Mathf.RoundToInt(desired.z*10000);
        float rotation=(float)VisualFabric.Unit(seed,1)*Mathf.PI*2;
        // Nearby alternatives first, then the full same-cell interior. Candidate
        // order depends on source identity/position, never the previous forecast.
        for(int ring=1;ring<=4;ring++)for(int i=0;i<12;i++) {
            float angle=rotation+i*Mathf.PI/6;
            candidates.Add(desired+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(ring*.16f));
        }
        candidates.Add(center);
        for(int ring=1;ring<=5;ring++)for(int i=0;i<12;i++) {
            float angle=rotation+i*Mathf.PI/6;
            candidates.Add(center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(ring*.17f));
        }
        foreach(var candidate in candidates.OrderBy(p=>Horizontal(p-desired).sqrMagnitude)) {
            if(!NatureRelocationGround(tile,candidate,radius)||!SettlementSpaceAvailable(candidate,radius)||!NatureReservedSpace(candidate,radius))continue;
            point=candidate;point.y=VisualGroundHeight(point);settlementNatureReserved.Add(new NatureCircle(point,radius));return true;
        }
        return false;
    }
    string NatureOccupancySignature() {
        var sites=settlementSites.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>V.Arr(p.Key,p.Value.center.x,p.Value.center.z,
            V.From(p.Value.obstacles.Select(r=>V.Arr(r.x,r.y,r.width,r.height)).ToArray()),
            V.From(p.Value.natureKeepClear.Select(r=>V.Arr(r.x,r.y,r.width,r.height)).ToArray()))).ToArray();
        var roads=settlementRoadPaths.Select(path=>V.From(path.Select(p=>V.Arr(p.x,p.y,p.z)).ToArray())).ToArray();
        var infill=settlementInfillPositions.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>V.Arr(p.Key,p.Value.x,p.Value.y,p.Value.z)).ToArray();
        return V.Arr(V.From(sites),V.From(roads),V.From(infill)).ToJson();
    }
    sealed class NatureRequest {
        public string id,tile;public Vector3 desired;public CityFoliage batch;public int variant;
        public float height,crown,radius,maxWidth,embed;public bool placed;public Vector3 point;
    }
    NatureRequest NatureRequestAt(CityFoliage batch,int variant,string id,string tile,Vector3 point,float height,float crown,float maxWidth=0,float embed=0) {
        var bounds=batch.VariantBounds(variant);float horizontal=height/Mathf.Max(.001f,bounds.size.y)*crown;
        float diagonal=Mathf.Sqrt(bounds.size.x*bounds.size.x+bounds.size.z*bounds.size.z);
        if(maxWidth>0)horizontal=Mathf.Min(horizontal,maxWidth/Mathf.Max(.001f,diagonal));
        return new NatureRequest{batch=batch,variant=variant,id=id,tile=tile,desired=point,height=height,crown=crown,maxWidth=maxWidth,embed=embed,radius=diagonal*horizontal*.5f+.025f};
    }
    HashSet<string> PlaceSettlementNature(List<NatureRequest> requests) {
        settlementNatureReserved.Clear();natureOriginalRetained=natureRelocated=natureUnplaced=natureOccupiedOverlap=natureOutsideSourceTile=0;
        var ordered=requests.OrderBy(r=>r.id,StringComparer.Ordinal).ToArray();
        // Reserve every unimpacted original before moving anything. Existing
        // forest crowns may overlap naturally; a relocated crown may not evict them.
        foreach(var request in ordered)if(InsideNatureTile(request.tile,request.desired)&&DryFabricPoint(request.desired)&&SettlementSpaceAvailable(request.desired,request.radius)) {
            request.placed=true;request.point=request.desired;request.point.y=VisualGroundHeight(request.point);
            settlementNatureReserved.Add(new NatureCircle(request.point,request.radius));natureOriginalRetained++;
        }
        foreach(var request in ordered)if(!request.placed) {
            if(TryPlaceSettlementNature(request.tile,request.desired,request.radius,out request.point)){request.placed=true;natureRelocated++;}
            else natureUnplaced++;
        }
        var trees=new List<CityFoliage.Placement>();var details=new List<CityFoliage.Placement>();var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var request in ordered)if(request.placed) {
            if(!SettlementSpaceAvailable(request.point,request.radius))natureOccupiedOverlap++;
            if(!InsideNatureTile(request.tile,request.point))natureOutsideSourceTile++;
            var placement=NaturePlacement(request.batch,request.variant,request.point,request.height,request.crown,request.id,request.maxWidth,request.embed);
            if(request.batch==foliage){trees.Add(placement);ids.Add(request.id);}else details.Add(placement);
        }
        foliage.SetPlacements(trees);natureDetails.SetPlacements(details);return ids;
    }
}
}
