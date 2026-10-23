using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace MET.City {
public sealed partial class CityWorld {
    const float LandscapeExtent=22f;
    const int LandscapeGridCells=256;
    Vector3[] denseSurfaceVertices=Array.Empty<Vector3>();
    int[] visibleGroundTriangles=Array.Empty<int>();
    int[] groundCellTriangleStarts=Array.Empty<int>();
    int[] groundCellTriangleCounts=Array.Empty<int>();
    Vector3[] visibleRiverVertices=Array.Empty<Vector3>();
    int[] visibleRiverTriangles=Array.Empty<int>();
    Material boundaryGridMaterial;
    const float RiverSurface=.025f; // Legacy fixture water height; seeded worlds read their source elevation.
    Transform groundRoot,settlementRoot;
    readonly List<Mesh> groundMeshes=new List<Mesh>();
    readonly List<LandSample> landSamples=new List<LandSample>();
    readonly List<Vector3> riverCurve=new List<Vector3>();
    Vector3[] riverBends=Array.Empty<Vector3>();
    readonly List<HillRidge> hillRidges=new List<HillRidge>();
    struct HillRidge {public Vector3 center,along;public float halfLength;}
    string terrainKey="";
    bool openingFramed;
    struct LandSample {public Vector3 point;public float height;public Color color;public string terrain;public bool sourceElevation;}
    static Vector3 CellPosition(V cell)=>new Vector3(-Mathf.Sqrt(3)*((float)cell.Get("q",0).Double+(float)cell.Get("r",0).Double*.5f),0,-1.5f*(float)cell.Get("r",0).Double);
    void EnsureLandscape() {
        var cells=Cells(snapshot);var geography=snapshot.Get("block",V.Map()).Get("geography",V.Map());string key=V.Arr(V.From(Ids(cells).Select(id=>V.Arr(id,cells[id].Get("terrain","plain"),cells[id].Get("elevation01",-1),cells[id].Get("moisture01",-1))).ToArray()),geography).ToJson();
        if(groundRoot!=null&&key==terrainKey)return;
        DestroyLandscape();tiles.Clear();landSamples.Clear();riverCurve.Clear();terrainKey=key;
        groundRoot=NewRoot("ContinuousRiverValley_NoExtraGameplayTiles");groundRoot.SetParent(geometryRoot,false);
        var riverAnchors=new List<Vector3>();
        foreach(var id in Ids(cells)) {
            var cell=cells[id];string type=cell.Get("terrain","plain").Text;var position=TilePosition(id);
            landSamples.Add(new LandSample {point=position,height=cell.Has("elevation01")?.10f+1.8f*(float)cell["elevation01"].Double:type=="hill"?1.35f:type=="forest"?.20f:type=="wet"?.07f:.13f,color=LandColor(type),terrain=type,sourceElevation=cell.Has("elevation01")});
            if(type=="water"){position.y=cell.Has("elevation01")?.10f+1.8f*(float)cell["elevation01"].Double:RiverSurface;riverAnchors.Add(position);}
        }
        var orderedRiver=geography.Get("river_ids",V.Arr());
        if(orderedRiver.Count>1) {
            riverAnchors.Clear();foreach(var riverId in orderedRiver){if(!cells.Has(riverId.Text))continue;var cell=cells[riverId.Text];var point=TilePosition(riverId.Text);point.y=cell.Has("elevation01")?.10f+1.8f*(float)cell["elevation01"].Double:RiverSurface;riverAnchors.Add(point);}
        } else if(cells.Values.Any(cell=>cell.Get("river_order",-1).Double>=0)) {
            riverAnchors.Clear();foreach(var id in Ids(cells).Where(id=>cells[id].Get("river_order",-1).Double>=0).OrderBy(id=>cells[id]["river_order"].Double)){var point=TilePosition(id);point.y=cells[id].Has("elevation01")?.10f+1.8f*(float)cells[id]["elevation01"].Double:RiverSurface;riverAnchors.Add(point);}
        } else riverAnchors.Sort((a,b)=>b.z.CompareTo(a.z));
        BuildRiverCurve(riverAnchors);BuildRiverBends();BuildHillRidges();
        terrainMaterials["continuous"]=LandscapeMaterial("CityLandscapeVertex");
        terrainMaterials["water"]=LandscapeMaterial("CityRiverVertex");
        BuildBackgroundLandscape();
        BuildRiverSurface();
        foreach(var id in Ids(cells))CreateTile(id,cells[id]);
        BuildBoundaryGrid();
    }
    Material LandscapeMaterial(string resource) {
        string key="Landscape_"+resource;
        if(surfaceCache.TryGetValue(key,out var cached))return cached;
        var template=Resources.Load<Material>("Rendering/"+resource);
        if(template==null)throw new InvalidOperationException("The serialized city landscape material is missing: "+resource);
        var material=new Material(template){name=resource,enableInstancing=true};ownedMaterials.Add(material);surfaceCache[key]=material;return material;
    }
    static Color LandColor(string type) {
        switch(type) {case "hill":return Srgb("899553");case "forest":return Srgb("436638");case "wet":return Srgb("56824A");case "water":return Srgb("AE9D77");default:return Srgb("819C4D");}
    }
    void BuildRiverCurve(List<Vector3> anchors) {
        if(anchors.Count<2)return;
        var first=anchors[0];var last=anchors[anchors.Count-1];
        var upstream=first-anchors[1];upstream.y=0;upstream.Normalize();
        for(int i=16;i>0;i--)riverCurve.Add(first+upstream*(15*i/16f));
        for(int i=0;i<anchors.Count-1;i++) {
            var p1=anchors[i];var p2=anchors[i+1];var p0=i>0?anchors[i-1]:2*p1-p2;var p3=i+2<anchors.Count?anchors[i+2]:2*p2-p1;
            const int steps=16;
            for(int j=0;j<steps;j++){float t=j/(float)steps;float t2=t*t,t3=t2*t;var point=.5f*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t2+(-p0+3*p1-3*p2+p3)*t3);point.y=Mathf.Lerp(p1.y,p2.y,t);riverCurve.Add(point);}
        }
        riverCurve.Add(last);var downstream=last-anchors[anchors.Count-2];downstream.y=0;downstream.Normalize();
        for(int i=1;i<=16;i++)riverCurve.Add(last+downstream*(15*i/16f));
    }
    void BuildRiverBends() {
        riverBends=new Vector3[riverCurve.Count];
        for(int i=0;i<riverCurve.Count;i++) {
            var before=Horizontal(riverCurve[i]-riverCurve[Mathf.Max(0,i-6)]).normalized;
            var after=Horizontal(riverCurve[Mathf.Min(riverCurve.Count-1,i+6)]-riverCurve[i]).normalized;
            riverBends[i]=after-before;
        }
    }
    void RiverProjection(Vector3 point,out float distance,out float height) => NearestRiverSegment(point,out distance,out height,out _,out _);
    int NearestRiverSegment(Vector3 point,out float distance,out float height,out Vector3 difference,out float fraction) {
        float nearest=float.PositiveInfinity;height=RiverSurface;point.y=0;difference=Vector3.zero;fraction=0;int segment=-1;
        for(int i=0;i<riverCurve.Count-1;i++) {
            var a=riverCurve[i];var delta=riverCurve[i+1]-a;float h0=a.y,dh=delta.y;a.y=0;delta.y=0;
            float t=Mathf.Clamp01(Vector3.Dot(point-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));var offset=point-(a+delta*t);float d2=offset.sqrMagnitude;
            if(d2<nearest){nearest=d2;height=h0+dh*t;difference=offset;segment=i;fraction=t;}
        }
        distance=Mathf.Sqrt(nearest);return segment;
    }
    void RiverProjection(Vector3 point,out float distance,out float height,out float innerBank) {
        NearestRiverSegment(point,out distance,out height,out _,out _);
        // Blend bend influence in world space. A nearest-segment bank profile has
        // medial-axis creases on the inside of a tight turn even with curve interpolation.
        float side=0,weights=0;
        for(int i=0;i<riverCurve.Count;i+=16) {
            var delta=Horizontal(point-riverCurve[i]);float d2=delta.sqrMagnitude,w=1/(.7f+d2*d2);
            side+=Vector3.Dot(delta,riverBends[i])*w;weights+=w;
        }
        innerBank=weights>0?Mathf.Clamp01(.5f+side/weights*1.3f):.5f;
    }
    float RiverDistance(Vector3 point) {RiverProjection(point,out var distance,out var height);return distance;}
    float RiverHeightAt(Vector3 point) {RiverProjection(point,out var distance,out var height);return height;}
    static float RiverHalfWidth(Vector3 point)=>.37f+.19f*Mathf.PerlinNoise(point.x*.17f+31,point.z*.17f+17);
    static float LandNoise(Vector3 point,float scale)=>Mathf.PerlinNoise(point.x*scale+117.43f,point.z*scale+83.17f);
    void BuildHillRidges() {
        hillRidges.Clear();var pending=landSamples.Where(sample=>sample.terrain=="hill").Select(sample=>sample.point).ToList();
        while(pending.Count>0) {
            var group=new List<Vector3>{pending[0]};pending.RemoveAt(0);
            for(int i=0;i<group.Count;i++)for(int j=pending.Count-1;j>=0;j--)if((pending[j]-group[i]).sqrMagnitude<3.1f){group.Add(pending[j]);pending.RemoveAt(j);}
            var center=group.Aggregate(Vector3.zero,(sum,p)=>sum+p)/group.Count;float xx=0,xz=0,zz=0;
            foreach(var p in group){var d=p-center;xx+=d.x*d.x;xz+=d.x*d.z;zz+=d.z*d.z;}
            float angle=group.Count==1?.38f+LandNoise(center,.12f)*.65f:.5f*Mathf.Atan2(2*xz,xx-zz);
            var along=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));float length=group.Max(p=>Mathf.Abs(Vector3.Dot(p-center,along)));
            hillRidges.Add(new HillRidge{center=center,along=along,halfLength=length+.48f});
        }
    }
    float RawHeightAt(Vector3 point) {
        float height=.13f+.032f*Mathf.Sin(point.x*.75f+point.z*.48f)+.025f*Mathf.Sin(point.z*1.15f-point.x*.3f);
        bool hasElevation=landSamples.Count>0&&landSamples[0].sourceElevation;
        if(hasElevation) {
            float weighted=0,weights=0;foreach(var sample in landSamples){float dx=point.x-sample.point.x,dz=point.z-sample.point.z,d2=dx*dx+dz*dz,w=1/(.025f+d2*d2);weighted+=sample.height*w;weights+=w;}
            height=weighted/weights;
        }
        // Adjacent hill cells share one spine. Overlaps take the highest shoulder, rather than summing cell-sized domes.
        float uplift=0;
        foreach(var ridge in hillRidges) {
            var delta=point-ridge.center;float along=Vector3.Dot(delta,ridge.along),across=Vector3.Dot(delta,new Vector3(-ridge.along.z,0,ridge.along.x));
            float crestOffset=.24f*Mathf.Sin(along*1.05f+ridge.center.x*.3f)+.09f;
            float end=Mathf.Max(0,Mathf.Abs(along)-ridge.halfLength);
            float core=Mathf.Clamp01(1-Mathf.Sqrt(end*end*.65f+(across-crestOffset)*(across-crestOffset)/.85f));
            float foot=Mathf.Clamp01(1-Mathf.Sqrt(end*end*.48f+across*across*.54f)/1.35f);
            float shoulderA=Mathf.Clamp01(1-Mathf.Sqrt((along+.44f)*(along+.44f)*.56f+(across+.57f)*(across+.57f)*1.5f)/1.16f);
            float shoulderB=Mathf.Clamp01(1-Mathf.Sqrt((along-.61f)*(along-.61f)*.72f+(across-.60f)*(across-.60f)*1.2f)/1.10f);
            // A narrow crest and broad, uneven flanks read as one connected landform.
            // Linear-to-power flanks keep a recognizable ridge instead of a smooth dome.
            float crest=.80f+.20f*Mathf.Sin(along*1.65f+.8f);
            float spine=1.00f*Mathf.Pow(core,1.28f)*crest;
            if(ridge.halfLength>1.0f) {
                float saddle=1-Mathf.SmoothStep(0,1,Mathf.Abs(along-ridge.halfLength*.12f)/.48f);
                spine*=1-.30f*saddle;
            }
            float drainage=LandNoise(new Vector3(along*2.1f+across*.7f,0,across*.38f),1);
            float flank=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,.45f,Mathf.Abs(across-crestOffset)));
            spine-=.14f*drainage*flank*core;
            float lower=Mathf.Max(.43f*shoulderA*shoulderA,.35f*shoulderB*shoulderB);
            uplift=Mathf.Max(uplift,.20f*foot*foot+Mathf.Max(spine,lower));
        }
        height+=uplift;
        if(!hasElevation)foreach(var sample in landSamples)if(sample.terrain=="wet") {
            float distance=Horizontal(point-sample.point).magnitude;
            height=Mathf.Lerp(height,.075f,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1.1f,distance)));
        }
        height+=(LandNoise(point,1.2f)-.5f)*.024f;
        return height;
    }
    float NaturalHeightAt(Vector3 point) {
        float height=RawHeightAt(point);
        RiverProjection(point,out float riverDistance,out float riverHeight,out float innerBank);float shore=RiverHalfWidth(point);
        float bankWidth=Mathf.Lerp(.19f,.48f,innerBank),valleyWidth=Mathf.Lerp(.80f,1.40f,innerBank);
        if(riverDistance<shore+.05f)height=Mathf.Lerp(riverHeight-.17f,riverHeight-.015f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(shore-.13f,shore+.05f,riverDistance)));
        else {
            float edge=riverDistance-shore;
            float bench=Mathf.Lerp(riverHeight-.015f,riverHeight+.10f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,bankWidth,edge)));
            float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(bankWidth,bankWidth+valleyWidth,edge));
            height=Mathf.Lerp(Mathf.Min(height,bench),height,rise);
        }
        return height;
    }
    // Buildings sit on local retaining supports. Their presence never changes
    // the natural valley/crest or makes neighboring plots share one plane.
    float HeightAt(Vector3 point)=>NaturalHeightAt(point);
    public float VisualGroundHeight(Vector3 point) {
        if(denseSurfaceVertices.Length==0)return HeightAt(point);
        float step=2*LandscapeExtent/LandscapeGridCells,x=(point.x+LandscapeExtent)/step,z=(point.z+LandscapeExtent)/step;
        int ix=Mathf.Clamp(Mathf.FloorToInt(x),0,LandscapeGridCells-1),iz=Mathf.Clamp(Mathf.FloorToInt(z),0,LandscapeGridCells-1);
        point.x=Mathf.Clamp(point.x,-LandscapeExtent,LandscapeExtent);point.z=Mathf.Clamp(point.z,-LandscapeExtent,LandscapeExtent);
        int cell=iz*LandscapeGridCells+ix,start=groundCellTriangleStarts[cell],end=start+groundCellTriangleCounts[cell];
        for(int i=start;i<end;i+=3) {
            var a=denseSurfaceVertices[visibleGroundTriangles[i]];var b=denseSurfaceVertices[visibleGroundTriangles[i+1]];var c=denseSurfaceVertices[visibleGroundTriangles[i+2]];
            float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
            float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/denominator;
            float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/denominator;
            if(u>=-.00001f&&v>=-.00001f&&u+v<=1.00001f)return u*a.y+v*b.y+(1-u-v)*c.y;
        }
        return HeightAt(point);
    }
    public float ForestDensityAt(Vector3 point) {
        float forest=0,total=0;
        var warped=point+new Vector3(Mathf.Sin(point.z*2.1f+point.x*.6f)*.10f,0,Mathf.Sin(point.x*1.9f-point.z*.7f)*.10f);
        foreach(var sample in landSamples){float dx=warped.x-sample.point.x,dz=warped.z-sample.point.z,w=Mathf.Exp(-(dx*dx+dz*dz)*1.45f);total+=w;if(sample.terrain=="forest")forest+=w;}
        float density=total>0?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.82f,forest/total)):0;
        float shore=Mathf.SmoothStep(0,1,Mathf.InverseLerp(RiverHalfWidth(point)+.15f,RiverHalfWidth(point)+.42f,RiverDistance(point)));
        return density*shore;
    }
    public float VisualSurfaceHeight(Vector3 point) {
        float height=VisualGroundHeight(point);
        for(int i=0;i<visibleRiverTriangles.Length;i+=3) {
            var a=visibleRiverVertices[visibleRiverTriangles[i]];var b=visibleRiverVertices[visibleRiverTriangles[i+1]];var c=visibleRiverVertices[visibleRiverTriangles[i+2]];
            float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
            if(Mathf.Abs(denominator)<.000001f)continue;
            float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/denominator;
            float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/denominator;
            if(u>=0&&v>=0&&u+v<=1)height=Mathf.Max(height,u*a.y+v*b.y+(1-u-v)*c.y);
        }
        return height;
    }
    Color AlbedoAt(Vector3 point,Vector3 normal) {
        // A narrow, shaped boundary between source terrain types. This is color classification only.
        var warped=point+new Vector3(Mathf.Sin(point.z*2.1f+point.x*.6f)*.10f,0,Mathf.Sin(point.x*1.9f-point.z*.7f)*.10f);
        float nearest=float.PositiveInfinity,second=float.PositiveInfinity;LandSample first=new LandSample(),other=new LandSample();
        foreach(var sample in landSamples) {
            float dx=warped.x-sample.point.x,dz=warped.z-sample.point.z,distance=Mathf.Sqrt(dx*dx+dz*dz);
            if(distance<nearest){second=nearest;other=first;nearest=distance;first=sample;}
            else if(distance<second){second=distance;other=sample;}
        }
        var color=landSamples.Count>0?first.color:LandColor("plain");
        if(first.terrain!=other.terrain)color=Color.Lerp(color,other.color,.5f*(1-Mathf.SmoothStep(0,1,(second-nearest)/.24f)));
        float patch=LandNoise(point,.92f)*.65f+LandNoise(point,2.8f)*.35f;
        color=Color.Lerp(color,Srgb("B3A15C"),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.53f,.80f,patch))*.24f);
        float slope=1-normal.y;
        if(point.y>.30f) {
            float rock=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.32f,slope+(patch-.5f)*.065f));
            color=Color.Lerp(color,Srgb("AAA18C"),rock*.97f);
            // Broad alternating warm ledges and cool cracks remain legible at strategic scale.
            float seams=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.54f,.77f,LandNoise(point+Vector3.one*point.y*.4f,3.2f)));
            color=Color.Lerp(color,Srgb("696F69"),rock*seams*.14f);
        }
        RiverProjection(point,out float distanceToRiver,out _,out float bankSide);
        float shore=RiverHalfWidth(point),bankVariation=LandNoise(point,.8f),bankScale=Mathf.Lerp(.72f,1.65f,bankSide);
        float gravel=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(shore+.14f,shore+.30f*bankScale+(bankVariation-.5f)*.12f,distanceToRiver));
        color=Color.Lerp(color,Srgb("A49F89"),gravel*.85f);
        float sand=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(shore+.02f,shore+.15f*bankScale+bankVariation*.06f,distanceToRiver));
        color=Color.Lerp(color,Srgb("C0B080"),sand);color.a=1;return SurfaceColor(color);
    }
    Vector2 GroundUV(Vector3 point)=>new Vector2(point.x/(2*LandscapeExtent)+.5f,point.z/(2*LandscapeExtent)+.5f);
    void ConformToVisibleGround(Mesh mesh,Transform frame,float raise) {
        // Sampling only road vertices leaves its interior spanning a different
        // plane from the ground. Clip the decal to the actual visible triangles
        // so steep foundation transitions cannot cut triangular holes in it.
        var input=mesh.vertices;var faces=mesh.triangles;
        var vertices=new List<Vector3>();var triangles=new List<int>();
        float step=2*LandscapeExtent/LandscapeGridCells;
        for(int face=0;face<faces.Length;face+=3) {
            var boundary=new[]{frame.TransformPoint(input[faces[face]]),frame.TransformPoint(input[faces[face+1]]),frame.TransformPoint(input[faces[face+2]])};
            if(HexEdgeSide(boundary[0],boundary[1],boundary[2])<0){var swap=boundary[1];boundary[1]=boundary[2];boundary[2]=swap;}
            float minX=boundary.Min(p=>p.x),maxX=boundary.Max(p=>p.x),minZ=boundary.Min(p=>p.z),maxZ=boundary.Max(p=>p.z);
            int x0=Mathf.Clamp(Mathf.FloorToInt((minX+LandscapeExtent)/step),0,LandscapeGridCells-1),x1=Mathf.Clamp(Mathf.FloorToInt((maxX+LandscapeExtent)/step),0,LandscapeGridCells-1);
            int z0=Mathf.Clamp(Mathf.FloorToInt((minZ+LandscapeExtent)/step),0,LandscapeGridCells-1),z1=Mathf.Clamp(Mathf.FloorToInt((maxZ+LandscapeExtent)/step),0,LandscapeGridCells-1);
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++) {
                int cell=z*LandscapeGridCells+x,start=groundCellTriangleStarts[cell],end=start+groundCellTriangleCounts[cell];
                for(int i=start;i<end;i+=3) {
                    var polygon=new List<Vector3>{denseSurfaceVertices[visibleGroundTriangles[i]],denseSurfaceVertices[visibleGroundTriangles[i+1]],denseSurfaceVertices[visibleGroundTriangles[i+2]]};
                    for(int edge=0;edge<3&&polygon.Count>0;edge++)polygon=ClipToHexEdge(polygon,boundary[edge],boundary[(edge+1)%3]);
                    if(polygon.Count<3)continue;
                    int first=vertices.Count;
                    foreach(var point in polygon)vertices.Add(frame.InverseTransformPoint(point+Vector3.up*raise));
                    for(int j=1;j<polygon.Count-1;j++)triangles.AddRange(new[]{first,first+j,first+j+1});
                }
            }
        }
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
    }
    Mesh LandscapeHex(string id,bool analysis) {
        // Clip the visible adaptive-ground triangles in XYZ. Every new vertex remains on its source plane.
        var center=TilePosition(id);var boundary=new Vector3[6];for(int i=0;i<6;i++)boundary[i]=center+Corner(i,1);
        float minX=boundary.Min(p=>p.x),maxX=boundary.Max(p=>p.x),minZ=boundary.Min(p=>p.z),maxZ=boundary.Max(p=>p.z),step=2*LandscapeExtent/LandscapeGridCells;
        int x0=Mathf.Clamp(Mathf.FloorToInt((minX+LandscapeExtent)/step),0,LandscapeGridCells-1),x1=Mathf.Clamp(Mathf.FloorToInt((maxX+LandscapeExtent)/step),0,LandscapeGridCells-1);
        int z0=Mathf.Clamp(Mathf.FloorToInt((minZ+LandscapeExtent)/step),0,LandscapeGridCells-1),z1=Mathf.Clamp(Mathf.FloorToInt((maxZ+LandscapeExtent)/step),0,LandscapeGridCells-1);
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        Action<Vector3,Vector3,Vector3> append=(a,b,c)=> {
            var polygon=new List<Vector3>{a,b,c};
            for(int edge=0;edge<6&&polygon.Count>0;edge++)polygon=ClipToHexEdge(polygon,boundary[edge],boundary[(edge+1)%6]);
            if(polygon.Count<3)return;int start=vertices.Count;
            foreach(var point in polygon){vertices.Add(point-center);uv.Add(GroundUV(point));}
            for(int i=1;i<polygon.Count-1;i++)triangles.AddRange(new[]{start,start+i,start+i+1});
        };
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++) {
            int cell=z*LandscapeGridCells+x,start=groundCellTriangleStarts[cell],end=start+groundCellTriangleCounts[cell];
            for(int i=start;i<end;i+=3)append(denseSurfaceVertices[visibleGroundTriangles[i]],denseSurfaceVertices[visibleGroundTriangles[i+1]],denseSurfaceVertices[visibleGroundTriangles[i+2]]);
        }
        // The nearest ray hit must be the same visible surface, including water over the riverbed.
        for(int i=0;i<visibleRiverTriangles.Length;i+=3) {
            var a=visibleRiverVertices[visibleRiverTriangles[i]];var b=visibleRiverVertices[visibleRiverTriangles[i+1]];var c=visibleRiverVertices[visibleRiverTriangles[i+2]];
            if(Mathf.Max(a.x,Mathf.Max(b.x,c.x))<minX||Mathf.Min(a.x,Mathf.Min(b.x,c.x))>maxX||Mathf.Max(a.z,Mathf.Max(b.z,c.z))<minZ||Mathf.Min(a.z,Mathf.Min(b.z,c.z))>maxZ)continue;
            append(a,b,c);
        }
        var mesh=new Mesh{name=analysis?"AnalysisClippedFromVisibleGroundTriangles":"TileColliderClippedFromVisibleGroundTriangles"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();groundMeshes.Add(mesh);return mesh;
    }
    static List<Vector3> ClipToHexEdge(List<Vector3> polygon,Vector3 a,Vector3 b) {
        var result=new List<Vector3>();var previous=polygon[polygon.Count-1];float previousSide=HexEdgeSide(a,b,previous);bool previousInside=previousSide>=0;
        foreach(var current in polygon) {
            float currentSide=HexEdgeSide(a,b,current);bool currentInside=currentSide>=0;
            if(currentInside!=previousInside)result.Add(Vector3.LerpUnclamped(previous,current,previousSide/(previousSide-currentSide)));
            if(currentInside)result.Add(current);previous=current;previousSide=currentSide;previousInside=currentInside;
        }
        return result;
    }
    static float HexEdgeSide(Vector3 a,Vector3 b,Vector3 point)=>(b.x-a.x)*(point.z-a.z)-(b.z-a.z)*(point.x-a.x);
    void BuildBackgroundLandscape() {
        // One surface supplies terrain, the 61 colliders and clipped analysis triangles.
        const int cells=LandscapeGridCells,shoreSubdivisions=4;float step=2*LandscapeExtent/cells,fineStep=step/shoreSubdivisions;
        var refined=new bool[cells*cells];
        for(int z=0;z<cells;z++)for(int x=0;x<cells;x++) {
            var point=new Vector3(-LandscapeExtent+(x+.5f)*step,0,-LandscapeExtent+(z+.5f)*step);
            refined[z*cells+x]=RiverDistance(point)<RiverHalfWidth(point)+1.15f+step;
        }
        var vertices=new List<Vector3>();var triangles=new List<int>();var indices=new Dictionary<int,int>();
        groundCellTriangleStarts=new int[cells*cells];groundCellTriangleCounts=new int[cells*cells];
        Func<int,int,int> vertex=(x,z)=> {
            int key=z*(cells*shoreSubdivisions+1)+x;if(indices.TryGetValue(key,out int index))return index;
            var point=new Vector3(-LandscapeExtent+x*fineStep,0,-LandscapeExtent+z*fineStep);point.y=HeightAt(point);
            index=vertices.Count;vertices.Add(point);indices.Add(key,index);return index;
        };
        for(int z=0;z<cells;z++)for(int x=0;x<cells;x++) {
            int cell=z*cells+x,start=triangles.Count,x0=x*shoreSubdivisions,z0=z*shoreSubdivisions;
            groundCellTriangleStarts[cell]=start;
            if(refined[cell]) {
                for(int dz=0;dz<shoreSubdivisions;dz++)for(int dx=0;dx<shoreSubdivisions;dx++) {
                    int a=vertex(x0+dx,z0+dz),b=vertex(x0+dx,z0+dz+1),c=vertex(x0+dx+1,z0+dz),d=vertex(x0+dx+1,z0+dz+1);
                    triangles.AddRange(new[]{a,b,c,c,b,d});
                }
            } else {
                bool left=x>0&&refined[cell-1],top=z+1<cells&&refined[cell+cells],right=x+1<cells&&refined[cell+1],bottom=z>0&&refined[cell-cells];
                if(left||top||right||bottom) {
                    // Neighboring fine edges share every vertex; the outer transition has no T junctions.
                    var ring=new List<int>();
                    for(int i=0;i<shoreSubdivisions;i+=left?1:shoreSubdivisions)ring.Add(vertex(x0,z0+i));
                    for(int i=0;i<shoreSubdivisions;i+=top?1:shoreSubdivisions)ring.Add(vertex(x0+i,z0+shoreSubdivisions));
                    for(int i=0;i<shoreSubdivisions;i+=right?1:shoreSubdivisions)ring.Add(vertex(x0+shoreSubdivisions,z0+shoreSubdivisions-i));
                    for(int i=0;i<shoreSubdivisions;i+=bottom?1:shoreSubdivisions)ring.Add(vertex(x0+shoreSubdivisions-i,z0));
                    int center=vertex(x0+shoreSubdivisions/2,z0+shoreSubdivisions/2);
                    for(int i=0;i<ring.Count;i++)triangles.AddRange(new[]{center,ring[i],ring[(i+1)%ring.Count]});
                } else {
                    int a=vertex(x0,z0),b=vertex(x0,z0+shoreSubdivisions),c=vertex(x0+shoreSubdivisions,z0),d=vertex(x0+shoreSubdivisions,z0+shoreSubdivisions);
                    triangles.AddRange(new[]{a,b,c,c,b,d});
                }
            }
            groundCellTriangleCounts[cell]=triangles.Count-start;
        }
        denseSurfaceVertices=vertices.ToArray();visibleGroundTriangles=triangles.ToArray();
        var normals=new Vector3[vertices.Count];var colors=new Color[vertices.Count];var uv=new Vector2[vertices.Count];var uv2=new Vector2[vertices.Count];
        for(int i=0;i<visibleGroundTriangles.Length;i+=3) {
            int a=visibleGroundTriangles[i],b=visibleGroundTriangles[i+1],c=visibleGroundTriangles[i+2];
            var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);normals[a]+=normal;normals[b]+=normal;normals[c]+=normal;
        }
        for(int i=0;i<vertices.Count;i++){normals[i].Normalize();colors[i]=AlbedoAt(vertices[i],normals[i]);uv[i]=GroundUV(vertices[i]);uv2[i]=SurfaceBiomeWeights(vertices[i],normals[i]);}
        var mesh=new Mesh{name="SingleVisibleVertexColorLandscape",indexFormat=IndexFormat.UInt32,vertices=denseSurfaceVertices,normals=normals,colors=colors,uv=uv,uv2=uv2,triangles=visibleGroundTriangles};mesh.RecalculateBounds();groundMeshes.Add(mesh);CreateGroundRenderer("OnlyVisibleLandscapeSurface_NoExtraGameplay",mesh,terrainMaterials["continuous"],true);mesh.UploadMeshData(true);
    }
    void CreateGroundRenderer(string label,Mesh mesh,Material material,bool castShadow=false) {
        var holder=new GameObject(label);holder.transform.SetParent(groundRoot,false);holder.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=holder.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=castShadow?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=true;
    }
    void BuildRiverSurface() {
        if(riverCurve.Count<2)return;const int across=16;var vertices=new Vector3[riverCurve.Count*(across+1)];var colors=new Color[vertices.Length];var uv=new Vector2[vertices.Length];var triangles=new List<int>();float length=0;
        for(int i=0;i<riverCurve.Count;i++) {
            var point=riverCurve[i];if(i>0)length+=Vector3.Distance(point,riverCurve[i-1]);var direction=i+1<riverCurve.Count?riverCurve[i+1]-point:point-riverCurve[i-1];var side=Vector3.Cross(Vector3.up,direction).normalized;float width=RiverHalfWidth(point)+.04f;
            for(int j=0;j<=across;j++) {
                float t=j/(float)across;var position=point+side*(t*2-1)*width;position.y=RiverHeightAt(position);int index=i*(across+1)+j;vertices[index]=position;uv[index]=new Vector2(t,length);
                float edge=Mathf.Abs(t*2-1),shallow=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.66f,1,edge));
                var color=Color.Lerp(Srgb("235C68"),Srgb("82AD95"),shallow);colors[index]=SurfaceColor(color);
                if(i+1<riverCurve.Count&&j<across){int a=index,b=index+1,c=index+across+1,d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});}
            }
        }
        visibleRiverVertices=vertices;visibleRiverTriangles=triangles.ToArray();
        var mesh=new Mesh{name="SourceDerivedRiverDeepChannelNarrowShallows",vertices=vertices,colors=colors,uv=uv,triangles=visibleRiverTriangles};mesh.RecalculateNormals();mesh.RecalculateBounds();groundMeshes.Add(mesh);CreateGroundRenderer("ContinuousValleyRiver_NoGameplay",mesh,terrainMaterials["water"]);mesh.UploadMeshData(true);
    }
    void BuildBoundaryGrid() {
        var vertices=new List<Vector3>();var triangles=new List<int>();var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var id in tiles.Keys){var center=TilePosition(id);for(int edge=0;edge<6;edge++) {
            var a=center+Corner(edge,1);var b=center+Corner((edge+1)%6,1);string ka=Mathf.RoundToInt(a.x*1000)+","+Mathf.RoundToInt(a.z*1000),kb=Mathf.RoundToInt(b.x*1000)+","+Mathf.RoundToInt(b.z*1000);string key=StringComparer.Ordinal.Compare(ka,kb)<0?ka+"/"+kb:kb+"/"+ka;if(!seen.Add(key))continue;
            var along=(b-a).normalized;var across=Vector3.Cross(Vector3.up,along)*.006f;const int steps=8;
            for(int step=0;step<steps;step++){var from=Vector3.Lerp(a,b,step/(float)steps);var to=Vector3.Lerp(a,b,(step+1)/(float)steps);int index=vertices.Count;foreach(var point in new[]{from-across,from+across,to-across,to+across}){var p=point;p.y=VisualSurfaceHeight(p)+.032f;vertices.Add(p);}triangles.AddRange(new[]{index,index+2,index+1,index+1,index+2,index+3});}
        }}
        var mesh=new Mesh{name="SubdividedUniqueGameplayGridEdges"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();groundMeshes.Add(mesh);
        if(boundaryGridMaterial==null){boundaryGridMaterial=new Material(overlay){name="SoftTerrainBoundaryGrid"};var color=Srgb("63805E");color.a=.24f;boundaryGridMaterial.SetColor("_BaseColor",SurfaceColor(color));ownedMaterials.Add(boundaryGridMaterial);}
        CreateGroundRenderer("GameplayHexBoundaries",mesh,boundaryGridMaterial);mesh.UploadMeshData(true);
    }
    Mesh plotCourtMesh;
    void BuildPlotCourt(Transform parent) {
        if(plotCourtMesh==null) {
            var vertices=new Vector3[13];var triangles=new int[36];vertices[0]=Vector3.up*.006f;
            for(int i=0;i<12;i++){float angle=i*Mathf.PI/6;vertices[i+1]=new Vector3(Mathf.Cos(angle)*.64f,.006f,Mathf.Sin(angle)*.60f);triangles[i*3]=0;triangles[i*3+1]=(i+1)%12+1;triangles[i*3+2]=i+1;}
            plotCourtMesh=new Mesh{name="SharedGroundLevelPlotCourtyard",vertices=vertices,triangles=triangles};plotCourtMesh.RecalculateNormals();plotCourtMesh.RecalculateBounds();plotCourtMesh.UploadMeshData(true);
        }
        var court=new GameObject("GroundedPlotCourtyard");court.transform.SetParent(parent,false);court.AddComponent<MeshFilter>().sharedMesh=plotCourtMesh;var renderer=court.AddComponent<MeshRenderer>();renderer.sharedMaterial=Surface("CourtyardGravel",Srgb("C5B899"));renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
    }
    void DestroyLandscape() {
        plotGroundStamps.Clear();
        denseSurfaceVertices=Array.Empty<Vector3>();
        visibleGroundTriangles=Array.Empty<int>();groundCellTriangleStarts=Array.Empty<int>();groundCellTriangleCounts=Array.Empty<int>();
        visibleRiverVertices=Array.Empty<Vector3>();visibleRiverTriangles=Array.Empty<int>();
        if(groundRoot!=null){groundRoot.gameObject.SetActive(false);Destroy(groundRoot.gameObject);groundRoot=null;}
        foreach(var mesh in groundMeshes)if(mesh!=null)Destroy(mesh);groundMeshes.Clear();
    }
}
}
