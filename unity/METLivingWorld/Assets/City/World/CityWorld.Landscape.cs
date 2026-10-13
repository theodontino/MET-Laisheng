using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace MET.City {
public sealed partial class CityWorld {
    const float LandscapeExtent=22f;
    const int LandscapeGridCells=176;
    Vector3[] denseSurfaceVertices=Array.Empty<Vector3>();
    const float RiverSurface=.025f;
    Transform groundRoot,settlementRoot;
    readonly List<Mesh> groundMeshes=new List<Mesh>();
    readonly List<LandSample> landSamples=new List<LandSample>();
    readonly List<Vector3> riverCurve=new List<Vector3>();
    Texture2D groundAlbedo,waterAlbedo;
    string terrainKey="";
    bool openingFramed;
    struct LandSample {public Vector3 point;public float height;public Color color;}
    static Vector3 CellPosition(V cell)=>new Vector3(-Mathf.Sqrt(3)*((float)cell.Get("q",0).Double+(float)cell.Get("r",0).Double*.5f),0,-1.5f*(float)cell.Get("r",0).Double);
    void EnsureLandscape() {
        var cells=Cells(snapshot);string key=V.From(Ids(cells).Select(id=>V.Arr(id,cells[id].Get("terrain","plain"))).ToArray()).ToJson();
        if(groundRoot!=null&&key==terrainKey)return;
        DestroyLandscape();tiles.Clear();landSamples.Clear();riverCurve.Clear();terrainKey=key;
        groundRoot=NewRoot("ContinuousRiverValley_NoExtraGameplayTiles");groundRoot.SetParent(geometryRoot,false);
        var riverAnchors=new List<Vector3>();
        foreach(var id in Ids(cells)) {
            var cell=cells[id];string type=cell.Get("terrain","plain").Text;var position=TilePosition(id);
            landSamples.Add(new LandSample {point=position,height=type=="hill"?1.15f:type=="forest"?.18f:type=="wet"?.075f:.13f,color=LandColor(type)});
            if(type=="water")riverAnchors.Add(position);
        }
        riverAnchors.Sort((a,b)=>b.z.CompareTo(a.z));BuildRiverCurve(riverAnchors);
        CreateGroundAlbedo();
        terrainMaterials["continuous"]=Surface("ContinuousLivingValley",Color.white);terrainMaterials["continuous"].SetTexture("_BaseMap",groundAlbedo);terrainMaterials["continuous"].SetFloat("_Smoothness",.05f);
        BuildBackgroundLandscape();
        foreach(var id in Ids(cells))CreateTile(id,cells[id]);
        BuildRiverSurface();BuildBoundaryGrid();
    }
    static Color LandColor(string type) {
        switch(type) {case "hill":return Srgb("A79D82");case "forest":return Srgb("547D51");case "wet":return Srgb("729274");case "water":return Srgb("A4A18A");default:return Srgb("88A365");}
    }
    void BuildRiverCurve(List<Vector3> anchors) {
        if(anchors.Count<2)return;
        var first=anchors[0];var last=anchors[anchors.Count-1];anchors.Insert(0,first+(first-anchors[1]).normalized*15);anchors.Add(last+(last-anchors[anchors.Count-2]).normalized*15);
        for(int i=0;i<anchors.Count-1;i++) {
            var p0=anchors[Mathf.Max(0,i-1)];var p1=anchors[i];var p2=anchors[i+1];var p3=anchors[Mathf.Min(anchors.Count-1,i+2)];
            int steps=i==0||i==anchors.Count-2?16:8;
            for(int j=0;j<steps;j++){float t=j/(float)steps;float t2=t*t,t3=t2*t;riverCurve.Add(.5f*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t2+(-p0+3*p1-3*p2+p3)*t3));}
        }
        riverCurve.Add(anchors[anchors.Count-1]);
    }
    float RiverDistance(Vector3 point) {
        float nearest=float.PositiveInfinity;
        for(int i=0;i<riverCurve.Count-1;i++) {
            var a=riverCurve[i];var delta=riverCurve[i+1]-a;float t=Mathf.Clamp01(Vector3.Dot(point-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));var difference=point-(a+delta*t);nearest=Mathf.Min(nearest,difference.x*difference.x+difference.z*difference.z);
        }
        return Mathf.Sqrt(nearest);
    }
    static float RiverHalfWidth(Vector3 point)=>.46f+.045f*Mathf.Sin(point.z*.75f+point.x*.22f);
    float HeightAt(Vector3 point) {
        float height=0,weights=0;
        foreach(var sample in landSamples) {float dx=point.x-sample.point.x,dz=point.z-sample.point.z,d2=dx*dx+dz*dz;float w=1/(.9f+d2*d2);height+=sample.height*w;weights+=w;}
        height=weights>0?height/weights:.13f;
        height+=(Mathf.PerlinNoise(point.x*.21f+9,point.z*.21f+3)-.5f)*.24f;
        float distance=RiverDistance(point);float shore=RiverHalfWidth(point);
        float shoreline=shore+.22f;
        if(distance<shoreline)height=Mathf.Lerp(-.14f,RiverSurface,Mathf.SmoothStep(0,1,Mathf.InverseLerp(shore-.18f,shoreline,distance)));
        else if(distance<shoreline+.65f)height=Mathf.Lerp(RiverSurface,Mathf.Max(RiverSurface+.055f,height),Mathf.SmoothStep(0,1,Mathf.InverseLerp(shoreline,shoreline+.65f,distance)));
        return height;
    }
    Vector3 GroundNormal(Vector3 point) {
        const float d=.04f;return new Vector3(HeightAt(point+Vector3.left*d)-HeightAt(point+Vector3.right*d),2*d,HeightAt(point+Vector3.back*d)-HeightAt(point+Vector3.forward*d)).normalized;
    }
    Color AlbedoAt(Vector3 point) {
        Color color=Color.black;float weights=0;
        foreach(var sample in landSamples) {float dx=point.x-sample.point.x,dz=point.z-sample.point.z,d2=dx*dx+dz*dz,w=1/(.28f+d2*d2);color+=sample.color*w;weights+=w;}
        color=weights>0?color/weights:LandColor("plain");
        float broad=Mathf.PerlinNoise(point.x*.65f+17,point.z*.65f+5),grain=Mathf.PerlinNoise(point.x*7.3f+31,point.z*7.3f+11),fine=Mathf.PerlinNoise(point.x*14.5f+3,point.z*14.5f+9);
        color*=.82f+broad*.24f+grain*.10f+fine*.04f;
        float patch=Mathf.SmoothStep(.58f,.79f,Mathf.PerlinNoise(point.x*1.3f+6,point.z*1.3f+23));color=Color.Lerp(color,Srgb("9E997B"),patch*.24f);
        float distance=RiverDistance(point),shore=RiverHalfWidth(point);float bankBlend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(shore+.06f,shore+.40f,distance));color=Color.Lerp(color,Srgb("C6B99B"),bankBlend*.85f);
        float edge=Mathf.Clamp01((Mathf.Max(Mathf.Abs(point.x),Mathf.Abs(point.z))-10)/12);color=Color.Lerp(color,Srgb("97A786"),edge*.4f);color.a=1;return color;
    }
    void CreateGroundAlbedo() {
        const int size=512;groundAlbedo=new Texture2D(size,size,TextureFormat.RGBA32,true,false){name="SharedProceduralValleyAlbedo_NotBakedLighting",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};var colors=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){var point=new Vector3((x/(float)(size-1)*2-1)*LandscapeExtent,0,(y/(float)(size-1)*2-1)*LandscapeExtent);colors[y*size+x]=AlbedoAt(point);}
        groundAlbedo.SetPixels(colors);groundAlbedo.Apply(true,true);
    }
    Vector2 GroundUV(Vector3 point)=>new Vector2(point.x/(2*LandscapeExtent)+.5f,point.z/(2*LandscapeExtent)+.5f);
    Mesh LandscapeHex(string id,bool analysis) {
        // Clip the visible dense-grid triangles in XYZ. Every new vertex remains on its source plane.
        var center=TilePosition(id);var boundary=new Vector3[6];for(int i=0;i<6;i++)boundary[i]=center+Corner(i,1);
        float minX=boundary.Min(p=>p.x),maxX=boundary.Max(p=>p.x),minZ=boundary.Min(p=>p.z),maxZ=boundary.Max(p=>p.z),step=2*LandscapeExtent/LandscapeGridCells;
        int x0=Mathf.Clamp(Mathf.FloorToInt((minX+LandscapeExtent)/step),0,LandscapeGridCells-1),x1=Mathf.Clamp(Mathf.FloorToInt((maxX+LandscapeExtent)/step),0,LandscapeGridCells-1);
        int z0=Mathf.Clamp(Mathf.FloorToInt((minZ+LandscapeExtent)/step),0,LandscapeGridCells-1),z1=Mathf.Clamp(Mathf.FloorToInt((maxZ+LandscapeExtent)/step),0,LandscapeGridCells-1);
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();bool water=analysis&&Cells(snapshot)[id].Get("terrain","").Text=="water";
        Action<Vector3,Vector3,Vector3> append=(a,b,c)=> {
            var polygon=new List<Vector3>{a,b,c};
            for(int edge=0;edge<6&&polygon.Count>0;edge++)polygon=ClipToHexEdge(polygon,boundary[edge],boundary[(edge+1)%6]);
            if(polygon.Count<3)return;int start=vertices.Count;
            foreach(var point in polygon){var local=point-center;if(water)local.y=Mathf.Max(local.y,RiverSurface);vertices.Add(local);uv.Add(GroundUV(point));}
            for(int i=1;i<polygon.Count-1;i++)triangles.AddRange(new[]{start,start+i,start+i+1});
        };
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++) {
            int i=z*(LandscapeGridCells+1)+x;var a=denseSurfaceVertices[i];var b=denseSurfaceVertices[i+LandscapeGridCells+1];var c=denseSurfaceVertices[i+1];var d=denseSurfaceVertices[i+LandscapeGridCells+2];append(a,b,c);append(c,b,d);
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
        // One visible surface for both the real city and its visual extension. Hex meshes only pick and analyse.
        const int cells=LandscapeGridCells;var vertices=new Vector3[(cells+1)*(cells+1)];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];var triangles=new int[cells*cells*6];
        for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++){int i=z*(cells+1)+x;var point=new Vector3((x/(float)cells*2-1)*LandscapeExtent,0,(z/(float)cells*2-1)*LandscapeExtent);point.y=HeightAt(point);vertices[i]=point;normals[i]=GroundNormal(point);uv[i]=GroundUV(point);}
        for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){int i=z*(cells+1)+x,t=(z*cells+x)*6;triangles[t]=i;triangles[t+1]=i+cells+1;triangles[t+2]=i+1;triangles[t+3]=i+1;triangles[t+4]=i+cells+1;triangles[t+5]=i+cells+2;}
        denseSurfaceVertices=vertices;
        var mesh=new Mesh{name="SingleVisibleContinuousLandscape_QuarterUnitGrid",vertices=vertices,normals=normals,uv=uv,triangles=triangles};mesh.RecalculateBounds();groundMeshes.Add(mesh);CreateGroundRenderer("OnlyVisibleLandscapeSurface_NoExtraGameplay",mesh,terrainMaterials["continuous"]);mesh.UploadMeshData(true);
    }
    void CreateGroundRenderer(string label,Mesh mesh,Material material) {
        var holder=new GameObject(label);holder.transform.SetParent(groundRoot,false);holder.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=holder.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
    }
    void CreateWaterAlbedo() {
        const int width=128,height=256;waterAlbedo=new Texture2D(width,height,TextureFormat.RGBA32,true,false){name="SharedRiverDepthAndFlowAlbedo_NotBakedLighting",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};var colors=new Color[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
            float u=x/(float)(width-1),v=y/(float)(height-1),bank=Mathf.Pow(Mathf.Abs(u*2-1),2.6f),phase=v*Mathf.PI*2;
            var color=Color.Lerp(Srgb("387384"),Srgb("82AAA6"),bank);float turbulence=Mathf.PerlinNoise(u*11+Mathf.Sin(phase)*2+3,Mathf.Cos(phase)*3+7)-.5f;
            float ripple=Mathf.Pow(Mathf.Max(0,Mathf.Sin(phase*3+u*7+Mathf.Sin(phase)*1.3f)),12)*.008f;color*=.97f+turbulence*.10f;color+=new Color(ripple*.6f,ripple,ripple,0);color.a=1;colors[y*width+x]=color;
        }
        waterAlbedo.SetPixels(colors);waterAlbedo.Apply(true,true);terrainMaterials["water"].SetTexture("_BaseMap",waterAlbedo);terrainMaterials["water"].SetColor("_BaseColor",Color.white);terrainMaterials["water"].SetFloat("_Smoothness",.28f);
    }
    void BuildRiverSurface() {
        if(riverCurve.Count<2)return;CreateWaterAlbedo();const int across=8;var vertices=new Vector3[riverCurve.Count*(across+1)];var uv=new Vector2[vertices.Length];var triangles=new List<int>();float length=0;
        for(int i=0;i<riverCurve.Count;i++) {
            var point=riverCurve[i];if(i>0)length+=Vector3.Distance(point,riverCurve[i-1]);var direction=i+1<riverCurve.Count?riverCurve[i+1]-point:point-riverCurve[i-1];var side=Vector3.Cross(Vector3.up,direction).normalized;float width=RiverHalfWidth(point)+.25f;
            for(int j=0;j<=across;j++){float t=j/(float)across;var position=point+side*(t*2-1)*width;position.y=RiverSurface;int index=i*(across+1)+j;vertices[index]=position;uv[index]=new Vector2(t,length*.45f);if(i+1<riverCurve.Count&&j<across){int a=index,b=index+1,c=index+across+1,d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});}}
        }
        var mesh=new Mesh{name="SourceDerivedRiverWithShallowMargins",vertices=vertices,uv=uv,triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();groundMeshes.Add(mesh);CreateGroundRenderer("ContinuousValleyRiver_NoGameplay",mesh,terrainMaterials["water"]);mesh.UploadMeshData(true);
    }
    void BuildBoundaryGrid() {
        var vertices=new List<Vector3>();var triangles=new List<int>();var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var id in tiles.Keys){var center=TilePosition(id);for(int edge=0;edge<6;edge++) {
            var a=center+Corner(edge,1);var b=center+Corner((edge+1)%6,1);string ka=Mathf.RoundToInt(a.x*1000)+","+Mathf.RoundToInt(a.z*1000),kb=Mathf.RoundToInt(b.x*1000)+","+Mathf.RoundToInt(b.z*1000);string key=StringComparer.Ordinal.Compare(ka,kb)<0?ka+"/"+kb:kb+"/"+ka;if(!seen.Add(key))continue;
            var along=(b-a).normalized;var across=Vector3.Cross(Vector3.up,along)*.006f;const int steps=8;
            for(int step=0;step<steps;step++){var from=Vector3.Lerp(a,b,step/(float)steps);var to=Vector3.Lerp(a,b,(step+1)/(float)steps);int index=vertices.Count;foreach(var point in new[]{from-across,from+across,to-across,to+across}){var p=point;p.y=HeightAt(p)+.032f;vertices.Add(p);}triangles.AddRange(new[]{index,index+2,index+1,index+1,index+2,index+3});}
        }}
        var mesh=new Mesh{name="SubdividedUniqueGameplayGridEdges"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();groundMeshes.Add(mesh);var material=new Material(overlay){name="SoftTerrainBoundaryGrid"};var color=Srgb("63805E");color.a=.24f;material.SetColor("_BaseColor",SurfaceColor(color));ownedMaterials.Add(material);CreateGroundRenderer("GameplayHexBoundaries",mesh,material);mesh.UploadMeshData(true);
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
        denseSurfaceVertices=Array.Empty<Vector3>();
        if(groundRoot!=null){groundRoot.gameObject.SetActive(false);Destroy(groundRoot.gameObject);groundRoot=null;}
        foreach(var mesh in groundMeshes)if(mesh!=null)Destroy(mesh);groundMeshes.Clear();if(groundAlbedo!=null)Destroy(groundAlbedo);groundAlbedo=null;if(waterAlbedo!=null)Destroy(waterAlbedo);waterAlbedo=null;
    }
}
}
