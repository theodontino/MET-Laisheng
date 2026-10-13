using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace MET.City {
public sealed partial class CityWorld {
    sealed class FabricNode {public GameObject root;public string signature;public int objects;}
    readonly Dictionary<string,FabricNode> fabricBuildings=new Dictionary<string,FabricNode>(StringComparer.Ordinal);
    readonly Dictionary<string,FabricNode> fabricNodes=new Dictionary<string,FabricNode>(StringComparer.Ordinal);
    readonly Dictionary<string,V> fabricCoverage=new Dictionary<string,V>(StringComparer.Ordinal);
    V fabricDefinitions=V.Map(),fabricStats=V.Map();
    string forestSignature="";
    string forestTerrainSignature="";
    readonly List<Tuple<string,Vector3,float>> forestCandidates=new List<Tuple<string,Vector3,float>>();
    HashSet<string> forestIds=new HashSet<string>(StringComparer.Ordinal);
    int forestAdded,forestRemoved,forestRetained;
    int addedNodes,updatedNodes,deletedNodes,addedObjects,updatedObjects,deletedObjects,forestUploads;
    int fabricTerrainBuildCount;
    long totalAdded,totalUpdated,totalDeleted;
    public V FabricDiagnostics {get;private set;}=V.Map();
    public V FabricPlan {get;private set;}=V.Map();
    public void SetFabricSources(V definitions,V compiledStats) {fabricDefinitions=definitions?.Duplicate(true)??V.Map();fabricStats=compiledStats?.Duplicate(true)??V.Map();}
    string GroundStamp(Vector3 point)=>Mathf.RoundToInt(VisualGroundHeight(point)*10000).ToString();
    void PresentGeometryIncremental() {
        addedNodes=updatedNodes=deletedNodes=addedObjects=updatedObjects=deletedObjects=forestUploads=0;
        if(settlementRoot==null){settlementRoot=NewRoot("CityPlotsAndStreets");settlementRoot.SetParent(geometryRoot,false);}
        var previousGround=groundRoot;EnsureLandscape();bool terrainRebuilt=groundRoot!=previousGround;if(terrainRebuilt)fabricTerrainBuildCount++;
        var plan=VisualFabric.Derive(snapshot,fabricDefinitions,fabricStats);FabricPlan=plan;
        var projects=snapshot.Get("projects",V.Map());var present=new HashSet<string>(StringComparer.Ordinal);
        foreach(var id in Ids(projects)) {
            var p=projects[id];string tile=p.Get("tile","").Text;if(p.Get("special",false).Bool||!tiles.ContainsKey(tile))continue;present.Add(id);
            string stamp=V.Arr(p.Get("tile",""),p.Get("def",""),p.Get("status",""),p.Get("paused",false),p.Get("operator_org",""),p.Get("slots",V.Map()),p.Get("level",0),p.Get("size_scale",1),p.Get("upgrade_job",V.Map()).Count>0,GroundStamp(TilePosition(tile))).ToJson(false,true);
            if(fabricBuildings.TryGetValue(id,out var existing)&&existing.signature==stamp)continue;
            bool update=existing!=null;if(update)RemoveFabricNode(existing,false);
            var previous=new HashSet<Transform>();foreach(Transform child in settlementRoot)previous.Add(child);
            var coverage=V.Map();CreateBuilding(id,p,coverage);var root=buildings[id].gameObject;
            var children=new List<Transform>();foreach(Transform child in settlementRoot)if(!previous.Contains(child)&&child!=root.transform)children.Add(child);
            foreach(var child in children)child.SetParent(root.transform,true);
            int count=root.GetComponentsInChildren<Transform>(true).Length;fabricBuildings[id]=new FabricNode{root=root,signature=stamp,objects=count};fabricCoverage[id]=coverage;RecordFabricChange(update,count);
        }
        foreach(var id in fabricBuildings.Keys.Where(id=>!present.Contains(id)).ToArray()){RemoveFabricNode(fabricBuildings[id],true);fabricBuildings.Remove(id);fabricCoverage.Remove(id);buildings.Remove(id);}
        var desired=new Dictionary<string,Tuple<string,Func<GameObject>>>(StringComparer.Ordinal);
        foreach(var id in Ids(plan["parcels"])) {
            var spec=plan["parcels"][id];string tile=spec["tile"].Text;var center=TilePosition(tile);var stamp=V.Arr(spec["stage"],spec["completed"],GroundStamp(center)).ToJson();
            desired[id]=Tuple.Create(stamp,(Func<GameObject>)(()=>BuildFabricParcel(spec)));
        }
        foreach(var id in Ids(plan["connectors"])) {
            var spec=plan["connectors"][id];var points=FabricRoute(spec);if(!DryFabricRoute(points))continue;
            // Relation changes retain the same path until a visible morphology threshold changes.
            string stamp=V.Arr(spec["kind"],spec["stage"],spec["operator"],V.From(points.Select(p=>V.Arr(p.x,Mathf.Round(p.y*10000)/10000,p.z)).ToArray())).ToJson();
            desired[id]=Tuple.Create(stamp,(Func<GameObject>)(()=>BuildFabricConnector(spec,points)));
        }
        foreach(var id in Ids(plan["fillers"])) {
            var spec=plan["fillers"][id];var point=FabricFillerPosition(spec);if(!DryFabricFootprint(point,.16f))continue;
            if(spec.Has("edge")&&!desired.ContainsKey(spec["edge"].Text))continue;
            string stamp=V.Arr(spec,GroundStamp(point)).ToJson(false,true);desired[id]=Tuple.Create(stamp,(Func<GameObject>)(()=>BuildFabricFiller(spec,point)));
        }
        foreach(var id in fabricNodes.Keys.Where(id=>!desired.ContainsKey(id)).ToArray()){RemoveFabricNode(fabricNodes[id],true);fabricNodes.Remove(id);}
        foreach(var entry in desired) {
            bool update=fabricNodes.TryGetValue(entry.Key,out var existing);if(update&&existing.signature==entry.Value.Item1)continue;if(update)RemoveFabricNode(existing,false);
            var root=entry.Value.Item2();int count=root.GetComponentsInChildren<Transform>(true).Length;fabricNodes[entry.Key]=new FabricNode{root=root,signature=entry.Value.Item1,objects=count};RecordFabricChange(update,count);
        }
        RefreshFabricForest(plan);UpdateFabricCoverage();
        int work=addedNodes+updatedNodes+deletedNodes+forestUploads+(terrainRebuilt?1:0);if(work>0){GeometryBuildCount++;Physics.SyncTransforms();}
        totalAdded+=addedObjects;totalUpdated+=updatedObjects;totalDeleted+=deletedObjects;
        FabricDiagnostics=V.Map("parcel_count",plan["parcels"].Count,"filler_count",fabricNodes.Keys.Count(x=>x.Contains(":infill:")),"connector_count",fabricNodes.Keys.Count(x=>x.StartsWith("edge:",StringComparison.Ordinal)&&!x.Contains(":infill:")),"building_count",fabricBuildings.Count,"forest_instance_count",foliage.InstanceCount,"forest_added_instances",forestAdded,"forest_removed_instances",forestRemoved,"forest_retained_instances",forestRetained,"added_nodes",addedNodes,"updated_nodes",updatedNodes,"deleted_nodes",deletedNodes,"added_objects",addedObjects,"updated_objects",updatedObjects,"deleted_objects",deletedObjects,"forest_uploads",forestUploads,"total_added_objects",totalAdded,"total_updated_objects",totalUpdated,"total_deleted_objects",totalDeleted,"scene_work",work,"stable_node_count",fabricBuildings.Count+fabricNodes.Count,"source_phase",SourcePhase,"levels",plan["levels"].Duplicate(true),"node_ids",V.From(fabricNodes.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray()));
        FabricDiagnostics["terrain_rebuilt"]=terrainRebuilt;FabricDiagnostics["terrain_build_count"]=fabricTerrainBuildCount;FabricDiagnostics["terrain_update_scope"]="cached whole shared ground and tile colliders only when terrain/geography/occupied plots change";FabricDiagnostics["retained_nodes"]=fabricBuildings.Count+fabricNodes.Count-addedNodes-updatedNodes;
        if(!openingFramed&&buildings.Count>0){CameraController.ShowSettlement(buildings.Values.Select(x=>TilePosition(x.TileId)).Aggregate(Vector3.zero,(a,b)=>a+b)/buildings.Count);openingFramed=true;}
    }
    void RecordFabricChange(bool update,int count) {if(update){updatedNodes++;updatedObjects+=count;}else{addedNodes++;addedObjects+=count;}}
    void RemoveFabricNode(FabricNode node,bool deletion) {
        if(node.root==null)return;
        foreach(var filter in node.root.GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh!=null&&ownedMeshes.Remove(filter.sharedMesh))Destroy(filter.sharedMesh);
        node.root.SetActive(false);Destroy(node.root);if(deletion){deletedNodes++;deletedObjects+=node.objects;}
    }
    GameObject FabricRoot(string id) {var root=new GameObject("Fabric_"+id);root.transform.SetParent(settlementRoot,false);var identity=root.AddComponent<CityFabricIdentity>();identity.VisualId=id;return root;}
    bool DryFabricPoint(Vector3 point)=>RiverDistance(point)>RiverHalfWidth(point)+.08f;
    bool DryFabricRoute(Vector3[] points)=>points.All(DryFabricPoint);
    bool DryFabricFootprint(Vector3 point,float radius)=>new[]{point,point+new Vector3(radius,0,radius),point+new Vector3(radius,0,-radius),point+new Vector3(-radius,0,radius),point+new Vector3(-radius,0,-radius)}.All(DryFabricPoint);
    GameObject FabricRibbon(string name,Vector3[] points,float width,Material material,Transform parent) {
        var root=Ribbon(name,points,width,material,parent);var mesh=root.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
        for(int i=0;i<vertices.Length;i++){var vertex=vertices[i];vertex.y=VisualGroundHeight(vertex)+(name=="SharedOperatorEdge"?.038f:.026f);vertices[i]=vertex;}mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();return root;
    }
    Vector3 GroundFabric(Vector3 p,float raise=.02f) {p.y=VisualGroundHeight(p)+raise;return p;}
    Vector3[] FabricRoute(V edge) {
        var a=TilePosition(edge["a"].Text);var b=TilePosition(edge["b"].Text);var forward=(b-a).normalized;var side=Vector3.Cross(Vector3.up,forward);var middle=(a+b)*.5f;
        // Leave each main model at its street-facing plot boundary; meet through the actual shared edge.
        var route=new[]{a+forward*.57f,middle-side*.13f,middle+side*.13f,b-forward*.57f};var points=new List<Vector3>();
        for(int span=0;span<3;span++)for(int i=0;i<5;i++)points.Add(GroundFabric(Vector3.Lerp(route[span],route[span+1],i/5f)));points.Add(GroundFabric(route[3]));return points.ToArray();
    }
    GameObject BuildFabricConnector(V spec,Vector3[] points) {
        var root=FabricRoot(spec["id"].Text);root.GetComponent<CityFabricIdentity>().Kind=spec["kind"].Text;
        FabricRibbon("SharedEdgeStreet",points,.085f+.012f*(int)spec["stage"].Long,road,root.transform);
        // Retain modest street access for unconnected neighbours; operating relations add the shared court.
        if((int)spec["stage"].Long>0) {
            var a=TilePosition(spec["a"].Text);var b=TilePosition(spec["b"].Text);var center=(a+b)*.5f;var side=Vector3.Cross(Vector3.up,(b-a).normalized);
            var courtPoints=new[]{GroundFabric(center-side*.35f),GroundFabric(center+side*.35f)};if(courtPoints.All(p=>DryFabricFootprint(p,.11f)))FabricRibbon("SharedCourt",courtPoints,.20f,Surface("FabricCourt",Srgb("BBAA85")),root.transform);
            if(spec["operator"].Text!="")FabricRibbon("SharedOperatorEdge",new[]{GroundFabric(center-side*.18f,.035f),GroundFabric(center+side*.18f,.035f)},.025f,Surface("Operator_"+spec["operator"].Text,OperatorColor(spec["operator"].Text)),root.transform);
        }
        return root;
    }
    GameObject BuildFabricParcel(V spec) {
        var root=FabricRoot(spec["id"].Text);root.GetComponent<CityFabricIdentity>().Kind="parcel_access";if(!spec["completed"].Bool)return root;
        var center=TilePosition(spec["tile"].Text);int stage=(int)spec["stage"].Long;
        var points=new[]{GroundFabric(center+new Vector3(.12f,0,.50f)),GroundFabric(center+new Vector3(.27f,0,.68f)),GroundFabric(center+new Vector3(.46f,0,.67f))};if(DryFabricRoute(points))FabricRibbon("EntranceFootpath",points,.045f+.005f*stage,road,root.transform);
        return root;
    }
    Vector3 FabricFillerPosition(V spec) {
        int i=(int)spec["index"].Long;Vector3 point;
        if(spec.Has("parcel")) {
            var center=TilePosition(spec["parcel"].Text);
            var frontage=new[]{new Vector3(-.61f,0,.49f),new Vector3(.63f,0,.47f),new Vector3(.73f,0,-.11f),new Vector3(-.68f,0,-.31f),new Vector3(.29f,0,-.71f)};
            string id=spec["id"].Text;var jitter=new Vector3((float)VisualFabric.Unit(id,7)-.5f,0,(float)VisualFabric.Unit(id,8)-.5f)*.055f;
            point=center+frontage[i%frontage.Length]+jitter;
        }
        else {var a=TilePosition(spec["a"].Text);var b=TilePosition(spec["b"].Text);var along=(b-a).normalized;var side=Vector3.Cross(Vector3.up,along);point=(a+b)*.5f+side*((i%2==0?1:-1)*(.24f+(i/2)*.22f))+along*((i/2==0?1:-1)*.055f);}
        return GroundFabric(point,.015f);
    }
    void FabricBox(string name,Transform parent,Vector3 position,Vector3 scale,Material material,float angle=0) {
        var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent,false);box.transform.position=position;box.transform.localScale=scale;box.transform.rotation=Quaternion.Euler(0,angle,0);Destroy(box.GetComponent<Collider>());box.GetComponent<Renderer>().sharedMaterial=material;
    }
    GameObject BuildFabricFiller(V spec,Vector3 point) {
        var root=FabricRoot(spec["id"].Text);string kind=spec["kind"].Text;root.GetComponent<CityFabricIdentity>().Kind=kind;
        int i=(int)spec["index"].Long;float rotation=spec.Has("a")?Mathf.Atan2((TilePosition(spec["b"].Text)-TilePosition(spec["a"].Text)).x,(TilePosition(spec["b"].Text)-TilePosition(spec["a"].Text)).z)*Mathf.Rad2Deg:((i%5)*72+26);
        float h=.15f+.045f*Mathf.Clamp((int)spec["tier"].Long-1,0,2);var walls=Surface("FabricWarmPlaster",Srgb("D5C6A9"));var roof=Surface("FabricSlate",Srgb("96A3A2"));
        if(kind=="mixed_court"||kind=="residential_walkway") {
            FabricBox("SharedBench",root.transform,point+Vector3.up*.055f,new Vector3(.18f,.025f,.06f),Surface("FabricTimber",Srgb("8F7C5D")),rotation);
            var tree=Instantiate(GetModel("CityModels/valley_tree_01"),root.transform,false);tree.name="NeighbourhoodStreetTree";FitImportedModel(tree,root.transform,.16f,.25f,rotation);tree.transform.localPosition+=point;
        } else {
            if(kind=="industrial_shed"||kind=="industrial_gallery"){walls=Surface("FabricWorkshopWalls",Srgb("B1BEB7"));roof=Surface("FabricWorkshopRoof",Srgb("99A298"));h*=.78f;}
            FabricBox(kind+"_walls",root.transform,point+Vector3.up*h*.5f,new Vector3(.22f,h,.15f),walls,rotation);
            BuildFabricRoof(root.transform,point+Vector3.up*h,new Vector3(.26f,.055f,.19f),roof,rotation);
            if(kind=="street_shop")FabricBox("ShopCanvasAwning",root.transform,point+Vector3.up*(h*.63f)+Quaternion.Euler(0,rotation,0)*Vector3.forward*.10f,new Vector3(.22f,.025f,.08f),Surface("FabricShopAwning",Srgb("AD8167")),rotation);
            if(kind=="industrial_gallery")FabricBox("LogisticsLoadingCanopy",root.transform,point+Vector3.up*.12f+Quaternion.Euler(0,rotation,0)*Vector3.forward*.10f,new Vector3(.22f,.025f,.12f),roof,rotation);
            var direction=Quaternion.Euler(0,rotation,0)*Vector3.forward;var access=new[]{GroundFabric(point+direction*.09f,.022f),GroundFabric(point+direction*.21f,.022f)};if(DryFabricRoute(access))FabricRibbon("InfillDoorAccess",access,.028f,road,root.transform);
        }
        return root;
    }
    void BuildFabricRoof(Transform parent,Vector3 position,Vector3 size,Material material,float angle) {
        var vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,1,0),new Vector3(.5f,1,0)};
        var mesh=new Mesh{name="OriginalFabricGabledRoof",vertices=vertices,triangles=new[]{0,4,1,1,4,5,2,3,4,3,5,4,0,2,4,1,5,3}};mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
        var roof=new GameObject("OriginalGabledRoof");roof.transform.SetParent(parent,false);roof.transform.position=position;roof.transform.rotation=Quaternion.Euler(0,angle,0);roof.transform.localScale=size;roof.AddComponent<MeshFilter>().sharedMesh=mesh;roof.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    void RefreshFabricForest(V plan) {
        forestAdded=forestRemoved=0;forestRetained=forestIds.Count;
        var parks=buildings.Values.Where(b=>b.AssignedPurpose=="green"&&b.Status!="construction").OrderBy(b=>b.BuildingId,StringComparer.Ordinal).ToArray();
        string signature=V.Arr(snapshot.Get("seed",0),terrainKey,V.From(plan["parcels"].Values.Select(p=>V.Arr(p["tile"],p["stage"],p["completed"])).ToArray()),V.From(parks.Select(p=>V.Arr(p.BuildingId,p.TileId)).ToArray())).ToJson();if(signature==forestSignature)return;forestSignature=signature;
        var placements=new List<Matrix4x4>();var centers=tiles.Keys.Select(TilePosition).ToArray();var parcelCenters=plan["parcels"].Values.Select(p=>Tuple.Create(TilePosition(p["tile"].Text),p)).ToArray();string seed=snapshot.Get("seed",0).Text;
        float minX=centers.Length>0?centers.Min(p=>p.x)-1:0,maxX=centers.Length>0?centers.Max(p=>p.x)+1:0,minZ=centers.Length>0?centers.Min(p=>p.z)-1:0,maxZ=centers.Length>0?centers.Max(p=>p.z)+1:0;
        // One jittered world lattice supplies cross-edge spacing; removing trees never shuffles neighbours.
        const float spacing=.43f;var ids=new HashSet<string>(StringComparer.Ordinal);string terrainSignature=seed+"|"+terrainKey;
        if(forestTerrainSignature!=terrainSignature) {
            forestTerrainSignature=terrainSignature;forestCandidates.Clear();
            for(int z=Mathf.FloorToInt(minZ/spacing);z<=Mathf.CeilToInt(maxZ/spacing);z++)for(int x=Mathf.FloorToInt(minX/spacing);x<=Mathf.CeilToInt(maxX/spacing);x++) {
                string id="forest:"+seed+":"+x+","+z;var point=new Vector3((x+(float)VisualFabric.Unit(id,1)*.25f)*spacing,0,(z+(float)VisualFabric.Unit(id,2)*.25f)*spacing);
                if(centers.All(p=>(p-point).sqrMagnitude>1.22f))continue;string nearest=tiles.Keys.OrderBy(tile=>(TilePosition(tile)-point).sqrMagnitude).First();if(VisualFabric.Water(Cells(snapshot)[nearest])||!DryFabricPoint(point))continue;
                float density=ForestDensityAt(point);if(VisualFabric.Unit(id,3)>density)continue;point.y=VisualGroundHeight(point);forestCandidates.Add(Tuple.Create(id,point,density));
            }
        }
        var prototype=GetModel("CityModels/valley_tree_01");var treeBounds=GroundBounds(prototype,prototype.transform);
        foreach(var candidate in forestCandidates) {
            string id=candidate.Item1;var point=candidate.Item2;var horizontal=new Vector3(point.x,0,point.z);
            bool cleared=false;foreach(var parcel in parcelCenters){float radius=.65f+.044f*(int)parcel.Item2["stage"].Long;if((horizontal-parcel.Item1).sqrMagnitude<radius*radius){cleared=true;break;}}if(cleared)continue;
            float height=.75f+(float)VisualFabric.Unit(id,4)*.35f;float scale=height/Mathf.Max(.001f,treeBounds.size.y);var rotation=Quaternion.Euler(0,(float)VisualFabric.Unit(id,5)*360,0);var offset=rotation*new Vector3(treeBounds.center.x,treeBounds.min.y,treeBounds.center.z)*scale;placements.Add(Matrix4x4.TRS(point-offset,rotation,Vector3.one*scale));ids.Add(id);
        }
        foreach(var park in parks)for(int i=0;i<3;i++) {
            string id="park:"+seed+":"+park.BuildingId+":"+i;var local=new[]{new Vector3(-.52f,0,-.24f),new Vector3(.43f,0,-.40f),new Vector3(-.30f,0,.51f)}[i];var point=TilePosition(park.TileId)+local;if(!DryFabricPoint(point))continue;point.y=VisualGroundHeight(point);float scale=.85f/Mathf.Max(.001f,treeBounds.size.y);var offset=new Vector3(treeBounds.center.x,treeBounds.min.y,treeBounds.center.z)*scale;placements.Add(Matrix4x4.TRS(point-offset,Quaternion.identity,Vector3.one*scale));ids.Add(id);
        }
        forestAdded=ids.Count(id=>!forestIds.Contains(id));forestRemoved=forestIds.Count(id=>!ids.Contains(id));forestRetained=ids.Count-forestAdded;forestIds=ids;
        foliage.SetPlacements(placements);forestUploads++;
    }
    void UpdateFabricCoverage() {
        var coverage=V.Map();foreach(var id in fabricCoverage.Keys.OrderBy(x=>x,StringComparer.Ordinal))coverage.Merge(fabricCoverage[id],true);
        var registered=assetRegistry["assets"];var primary=Bindings.Values.Select(x=>x["resource"].Text).Distinct(StringComparer.Ordinal).ToArray();var visible=coverage.Values.Where(x=>x.Get("rendered_primary",false).Bool).Select(x=>x["resource"].Text).Distinct(StringComparer.Ordinal).ToArray();
        AssetCoverage=V.Map("registered_assets",registered.Count,"loaded_prefab_count",models.Count,"loaded_resource_keys",V.From(models.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray()),"registered_cc0_assets",registered.Values.Count(x=>x.Get("origin","").Text=="kenney"),"registered_original_building_models",registered.Values.Count(x=>x.Get("origin","").Text=="project-original"&&x.Get("kind","building").Text=="building"),"original_tree_models",registered.Values.Count(x=>x.Get("origin","").Text=="project-original"&&x.Get("kind","").Text=="foliage"),"catalog_definition_bindings",Bindings.Count,"distinct_primary_models",primary.Length,"distinct_cc0_primary_models",primary.Count(x=>registered[x]["origin"].Text=="kenney"),"distinct_original_primary_models",primary.Count(x=>registered[x]["origin"].Text=="project-original"),"visible_distinct_primary_models",visible.Length,"visible_cc0_primary_models",visible.Count(x=>registered[x]["origin"].Text=="kenney"),"visible_definition_bindings",coverage,"registry_bindings",Bindings.Duplicate(true),"full_catalog_unique_models",primary.Length==Bindings.Count,"missing_assets",V.Arr(),"note",assetRegistry["scope_note"]);
    }
}
/// <summary>Stable renderer identity, deliberately separate from building IDs and simulation entities.</summary>
public sealed class CityFabricIdentity : MonoBehaviour {public string VisualId,Kind;}
}
