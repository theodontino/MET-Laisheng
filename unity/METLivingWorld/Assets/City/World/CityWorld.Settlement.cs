using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace MET.City {
public sealed partial class CityWorld {
    // All registered primary models use this orientation. Streets and infill share
    // that authored frame; the simulation's tile positions are left untouched.
    static readonly Quaternion SettlementFrame=Quaternion.Euler(0,30,0);
    static Vector3 SettlementOffset(float x,float z)=>SettlementFrame*new Vector3(x,0,z);
    static Vector3 Horizontal(Vector3 value){value.y=0;return value;}
    Material SettlementPaving=>Surface("SettlementPaving",Srgb("AAA895"));
    Material SettlementCourt=>Surface("SettlementCourt",Srgb("B5B29E"));
    static float SettlementModelScale(string definition) {
        // These three registered models provide the hospital campus, senior
        // school and power-plant skyline. Other buildings stay at town scale.
        return definition=="hospital"||definition=="secondary_school"||definition=="power_plant"?.86f:.74f;
    }
    void BuildSettlementWings(Transform parent,string purpose) {
        bool works=purpose=="industry"||purpose=="extractive"||purpose=="energy"||purpose=="logistics";
        bool garden=purpose=="green",farm=purpose=="agriculture",medical=purpose=="medical",school=purpose=="education",housing=purpose=="housing";
        // Fixed arrangements reflect the registered use, rather than dressing
        // every landmark with the same pair of anonymous huts.
        var slots=works?new[]{new Vector2(0,-.55f)}:
            medical?new[]{new Vector2(-.48f,-.34f),new Vector2(.48f,-.34f)}:
            housing?new[]{new Vector2(-.48f,-.36f),new Vector2(.48f,-.36f)}:
            school?new[]{new Vector2(-.49f,-.40f)}:
            farm?new[]{new Vector2(-.49f,-.42f)}:
            garden?new[]{new Vector2(.48f,-.42f)}:new[]{new Vector2(.48f,-.36f)};
        float height=works?.12f:garden?.08f:farm?.11f:medical?.13f:.15f;
        float ridge=works||medical?.012f:garden?.035f:.045f;
        var size=works?new Vector3(.55f,height,.17f):new Vector3(school?.25f:.24f,height,.15f);
        var roofSize=works?new Vector3(.58f,ridge,.20f):new Vector3(size.x+.025f,ridge,.18f);
        var vertices=new List<Vector3>();var wallTriangles=new List<int>();var roofTriangles=new List<int>();
        var box=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,1,-.5f),new Vector3(.5f,1,-.5f),new Vector3(-.5f,1,.5f),new Vector3(.5f,1,.5f)};
        var boxFaces=new[]{0,4,1,1,4,5,2,3,6,3,7,6,0,2,4,2,6,4,1,5,3,3,5,7,4,6,5,5,6,7};
        var roof=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,1,0),new Vector3(.5f,1,0)};
        var roofFaces=new[]{0,4,1,1,4,5,2,3,4,3,5,4,0,2,4,1,5,3};
        foreach(var desiredSlot in slots) {
            var identity=parent.GetComponent<CityBuildingIdentity>();
            var half=works?new Vector2(size.x*.5f,size.z*.5f):new Vector2(size.z*.5f,size.x*.5f);
            if(!TrySettlementWingSlot(identity.TileId,desiredSlot+settlementSites[identity.TileId].offset,half,out var slot))continue;
            var point=TilePosition(identity.TileId)+SettlementOffset(slot.x,slot.y);
            if(!DryFabricFootprint(point,works?.31f:.15f))continue;
            var rotation=SettlementFrame*Quaternion.Euler(0,works?180:slot.x>0?90:-90,0);
            point.y=SmallSiteFloor(point,new Vector2(size.x,size.z),rotation);
            BuildSlopeSupport(parent,point,new Rect(-size.x*.5f,-size.z*.5f,size.x,size.z),rotation,point.y,"LowWingSlopeSupport");
            if(settlementSites.TryGetValue(identity.TileId,out var site)) {
                site.obstacles.Add(ExpandedSiteRect(new Rect(slot.x-half.x,slot.y-half.y,half.x*2,half.y*2),.09f));
            }
            foreach(int face in boxFaces) {wallTriangles.Add(vertices.Count);vertices.Add(parent.InverseTransformPoint(point+rotation*Vector3.Scale(box[face],size)));}
            foreach(int face in roofFaces) {roofTriangles.Add(vertices.Count);vertices.Add(parent.InverseTransformPoint(point+Vector3.up*height+rotation*Vector3.Scale(roof[face],roofSize)));}
        }
        var mesh=new Mesh{name="UseSpecificLowSettlementWings",vertices=vertices.ToArray(),subMeshCount=2};mesh.SetTriangles(wallTriangles,0);mesh.SetTriangles(roofTriangles,1);mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
        var group=new GameObject("StreetFacingAttachedWings_"+purpose);group.transform.SetParent(parent,false);group.AddComponent<MeshFilter>().sharedMesh=mesh;
        var roofMaterial=BuildingRoofMaterial(purpose);
        var wallMaterial=BuildingWallMaterial(purpose);
        var renderer=group.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{wallMaterial,roofMaterial};renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
    }
    string SettlementAccessSignature(string tile) {
        if(!settlementSites.TryGetValue(tile,out var site))return "";
        return V.Arr(site.footprint.x,site.footprint.y,site.footprint.width,site.footprint.height,site.floor,
            V.From(site.obstacles.Select(r=>V.Arr(r.x,r.y,r.width,r.height)).ToArray())).ToJson();
    }
    // Every completed primary retains its complete rounded footprint ring.
    // Short entrance and annex access branches connect to that shared loop.
    void BuildSettlementAccess(string tile,Vector3 center,Vector3[] portals,int stage,Transform parent) {
        if(!settlementSites.TryGetValue(tile,out var site))return;
        var obstacles=SettlementRoadObstacles(tile);var doors=new List<Vector2>{site.entrance};var inverse=Quaternion.Inverse(SettlementFrame);
        foreach(var id in Ids(FabricPlan["fillers"])) {
            var spec=FabricPlan["fillers"][id];if(!IsSolidParcelInfill(spec)||spec["parcel"].Text!=tile||!settlementInfillPositions.TryGetValue(id,out var point))continue;
            var local=inverse*Horizontal(FabricFillerEntrance(spec,point)-center);doors.Add(new Vector2(local.x,local.z));
        }
        var paths=SettlementRingAccess(tile,center,site,obstacles,doors,portals,out var reached);
        settlementAccessCounts[tile]=new Vector2Int(doors.Count,reached);
        if(reached<doors.Count)settlementUnreachable[tile]=V.Map("unreachable_doors",doors.Count-reached);else settlementUnreachable.Remove(tile);
        settlementParcelPaths[tile]=paths;
        float width=(.09f+.009f*Mathf.Clamp(stage,0,4))*.65f;
        DrawSettlementPaths(parent,paths,width,stage<2?"dirt":buildings.Values.Any(b=>b.TileId==tile&&(b.AssignedPurpose=="industry"||b.AssignedPurpose=="energy"||b.AssignedPurpose=="extractive"||b.AssignedPurpose=="logistics"))?"gravel":"stone",tile,portals);
    }
    sealed class SettlementVisibilityGraph {
        public List<Vector2> nodes;public List<int> terminals;public Vector3[] ground;public float[] distance;public int[] previous;
    }
    SettlementVisibilityGraph SettlementVisibility(string tile,Vector3 center,Vector2 entrance,List<Vector2> targets,List<Rect> obstacles,bool weighted=true) {
        var inverse=Quaternion.Inverse(SettlementFrame);
        var nodes=new List<Vector2>{entrance};var terminals=new List<int>();
        foreach(var target in targets){int index=nodes.FindIndex(p=>(p-target).sqrMagnitude<.000001f);if(index<0){index=nodes.Count;nodes.Add(target);}terminals.Add(index);}
        foreach(var box in obstacles)foreach(var corner in new[]{new Vector2(box.xMin,box.yMin),new Vector2(box.xMax,box.yMin),new Vector2(box.xMax,box.yMax),new Vector2(box.xMin,box.yMax)}) {
            if(!SettlementPointInTile(center+SettlementOffset(corner.x,corner.y),tile,.045f)||obstacles.Any(r=>StrictlyInsideSite(corner,r)))continue;
            if(nodes.All(p=>(p-corner).sqrMagnitude>.000001f))nodes.Add(corner);
        }
        // A few dry perimeter waypoints allow paths around a shore or a pair of
        // overlapping padded footprints, instead of turning every plot into a ring.
        for(int edge=0;edge<6;edge++)foreach(float t in new[]{.25f,.5f,.75f}) {
            var p=inverse*Vector3.Lerp(Corner(edge,.90f),Corner((edge+1)%6,.90f),t);var point=new Vector2(p.x,p.z);
            if(!obstacles.Any(r=>StrictlyInsideSite(point,r))&&DryFabricFootprint(center+SettlementOffset(point.x,point.y),.045f))nodes.Add(point);
        }
        int count=nodes.Count;var ground=nodes.Select(p=>GroundFabric(center+SettlementOffset(p.x,p.y))).ToArray();var weight=new float[count,count];
        for(int a=0;a<count;a++)for(int b=a+1;b<count;b++) {
            if(obstacles.Any(r=>SegmentEntersSite(nodes[a],nodes[b],r)))continue;
            float length=Vector2.Distance(nodes[a],nodes[b]);int samples=Mathf.Max(1,Mathf.CeilToInt(length/.12f));bool dry=true;
            for(int i=0;i<=samples&&dry;i++)dry=DryFabricFootprint(Vector3.Lerp(ground[a],ground[b],i/(float)samples),.045f);
            if(dry){float climb=0,previousHeight=ground[a].y;for(int i=1;weighted&&i<=samples;i++){float h=VisualGroundHeight(Vector3.Lerp(ground[a],ground[b],i/(float)samples));climb+=Mathf.Abs(h-previousHeight);previousHeight=h;}weight[a,b]=weight[b,a]=length+.65f*climb;}
        }
        var distance=Enumerable.Repeat(float.PositiveInfinity,count).ToArray();var previous=Enumerable.Repeat(-1,count).ToArray();var visited=new bool[count];distance[0]=0;
        for(int iteration=0;iteration<count;iteration++) {
            int current=-1;for(int n=0;n<count;n++)if(!visited[n]&&(current<0||distance[n]<distance[current]))current=n;
            if(current<0||float.IsPositiveInfinity(distance[current]))break;visited[current]=true;
            for(int next=0;next<count;next++)if(!visited[next]&&weight[current,next]>0&&distance[current]+weight[current,next]<distance[next]){distance[next]=distance[current]+weight[current,next];previous[next]=current;}
        }
        return new SettlementVisibilityGraph{nodes=nodes,terminals=terminals,ground=ground,distance=distance,previous=previous};
    }
    static bool StrictlyInsideSite(Vector2 point,Rect rect)=>point.x>rect.xMin+.00001f&&point.x<rect.xMax-.00001f&&point.y>rect.yMin+.00001f&&point.y<rect.yMax-.00001f;
    static bool SegmentEntersSite(Vector2 a,Vector2 b,Rect rect) {
        // Open interiors allow a path to follow the expanded footprint boundary.
        float near=0,far=1;var delta=b-a;
        for(int axis=0;axis<2;axis++) {
            float start=axis==0?a.x:a.y,d=axis==0?delta.x:delta.y,low=(axis==0?rect.xMin:rect.yMin)+.00001f,high=(axis==0?rect.xMax:rect.yMax)-.00001f;
            if(Mathf.Abs(d)<.000001f){if(start<=low||start>=high)return false;}
            else {float enter=(low-start)/d,exit=(high-start)/d;if(enter>exit){float swap=enter;enter=exit;exit=swap;}near=Mathf.Max(near,enter);far=Mathf.Min(far,exit);if(near>=far)return false;}
        }
        return far>near;
    }
    Vector3 FabricFillerEntrance(V spec,Vector3 point) {
        if(spec.Has("parcel")) {
            var center=TilePosition(spec["parcel"].Text);var local=Quaternion.Inverse(SettlementFrame)*Horizontal(point-center);
            var toward=Mathf.Abs(local.x)>=Mathf.Abs(local.z)?new Vector2(-Mathf.Sign(local.x),0):new Vector2(0,-Mathf.Sign(local.z));
            // Face the open centre-side lane even after an annex was relocated.
            var entrance=new Vector3(local.x+toward.x*.19f,0,local.z+toward.y*.19f);
            return GroundFabric(center+SettlementFrame*entrance);
        }
        var route=fabricRoutes[spec["edge"].Text];var a=route[0];var b=route[route.Length-1];
        var along=Horizontal(b-a);float t=Mathf.Clamp01(Vector3.Dot(Horizontal(point-a),along)/along.sqrMagnitude);
        return GroundFabric(Vector3.Lerp(a,b,t));
    }
    void BuildSettlementCourt(Transform parent) {
        string purpose=parent.GetComponent<CityBuildingIdentity>().AssignedPurpose;
        // Farms already carry their source-backed rows; parks use the living
        // ground. Neither receives a rectangular presentation plinth.
        if(purpose=="green"||purpose=="agriculture")return;
        bool works=purpose=="industry"||purpose=="extractive"||purpose=="energy"||purpose=="logistics";
        var outline=works?new[]{new Vector2(-.38f,-.63f),new Vector2(.38f,-.63f),new Vector2(.43f,-.43f),new Vector2(.32f,-.27f),new Vector2(-.32f,-.27f),new Vector2(-.43f,-.43f)}:
            purpose=="medical"?new[]{new Vector2(-.29f,.22f),new Vector2(.29f,.22f),new Vector2(.42f,.46f),new Vector2(.30f,.57f),new Vector2(-.30f,.57f),new Vector2(-.42f,.46f)}:
            purpose=="education"?new[]{new Vector2(-.30f,.26f),new Vector2(.18f,.26f),new Vector2(.34f,.48f),new Vector2(.17f,.59f),new Vector2(-.30f,.52f)}:
            purpose=="housing"?new[]{new Vector2(-.22f,.28f),new Vector2(.22f,.28f),new Vector2(.29f,.49f),new Vector2(-.29f,.49f)}:
            new[]{new Vector2(-.27f,.25f),new Vector2(.27f,.25f),new Vector2(.37f,.46f),new Vector2(.25f,.56f),new Vector2(-.25f,.56f),new Vector2(-.37f,.46f)};
        // One convex, use-specific approach surface. The anchor lies inside the
        // polygon, unlike the old city-centre fan beneath a whole plot rectangle.
        var centroid=Vector2.zero;foreach(var p in outline)centroid+=p;centroid/=outline.Length;
        var vertices=new Vector3[outline.Length+1];var dry=new bool[vertices.Length];var triangles=new List<int>();
        var anchor=GroundFabric(parent.position+SettlementOffset(centroid.x,centroid.y),.014f);vertices[0]=parent.InverseTransformPoint(anchor);dry[0]=DryFabricPoint(anchor);
        for(int i=0;i<outline.Length;i++) {
            var p=GroundFabric(parent.position+SettlementOffset(outline[i].x,outline[i].y),.014f);vertices[i+1]=parent.InverseTransformPoint(p);dry[i+1]=DryFabricPoint(p);
        }
        for(int i=0;i<outline.Length;i++) {int next=(i+1)%outline.Length+1;if(dry[0]&&dry[i+1]&&dry[next])triangles.AddRange(new[]{0,next,i+1});}
        if(triangles.Count==0)return;
        var identity=parent.GetComponent<CityBuildingIdentity>();
        if(settlementSites.TryGetValue(identity.TileId,out var site)) {
            float minX=outline.Min(p=>p.x),maxX=outline.Max(p=>p.x),minZ=outline.Min(p=>p.y),maxZ=outline.Max(p=>p.y);
            // Nature clearance only. Approaches and streets may cross the court.
            site.natureKeepClear.Add(new Rect(minX+site.offset.x,minZ+site.offset.y,maxX-minX,maxZ-minZ));
        }
        var mesh=new Mesh{name="GroundFollowingUseSpecificApproach",vertices=vertices,triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
        var court=new GameObject("GroundedPlotCourtyard_"+purpose);court.transform.SetParent(parent,false);court.AddComponent<MeshFilter>().sharedMesh=mesh;ConformToVisibleGround(mesh,court.transform,.019f);
        var renderer=court.AddComponent<MeshRenderer>();renderer.sharedMaterial=works?Surface("SettlementWorkApron",Srgb("A9AC9E")):
            purpose=="medical"?Surface("SettlementServiceCourt",Srgb("C0BEB0")):
            purpose=="education"?Surface("SettlementCampusGround",Srgb("ACB296")):SettlementCourt;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
    }
}
}
