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
    V fabricDefinitionSource;
    readonly Dictionary<string,Vector3[]> fabricRoutes=new Dictionary<string,Vector3[]>(StringComparer.Ordinal);
    readonly Dictionary<Vector3,string> plotGroundStamps=new Dictionary<Vector3,string>();
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
    public void SetFabricSources(V definitions,V compiledStats) {
        // Catalog definitions are immutable during a session. Keep their one
        // detached copy; changing compiled capacities still refreshes each commit.
        if(!ReferenceEquals(fabricDefinitionSource,definitions)){fabricDefinitionSource=definitions;fabricDefinitions=definitions?.Duplicate(true)??V.Map();}
        fabricStats=compiledStats?.Duplicate(true)??V.Map();
    }
    string GroundStamp(Vector3 point)=>Mathf.RoundToInt(VisualGroundHeight(point)*10000).ToString();
    string PlotGroundStamp(Vector3 center) {
        if(plotGroundStamps.TryGetValue(center,out var cached))return cached;
        var heights=new int[9];int i=0;
        for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)heights[i++]=Mathf.RoundToInt(VisualGroundHeight(center+SettlementOffset(x*.60f,z*.60f))*10000);
        var stamp=string.Join(",",heights);plotGroundStamps[center]=stamp;return stamp;
    }
    void PresentGeometryIncremental() {
        addedNodes=updatedNodes=deletedNodes=addedObjects=updatedObjects=deletedObjects=forestUploads=0;
        if(settlementRoot==null){settlementRoot=NewRoot("CityPlotsAndStreets");settlementRoot.SetParent(geometryRoot,false);}
        var previousGround=groundRoot;EnsureLandscape();bool terrainRebuilt=groundRoot!=previousGround;if(terrainRebuilt)fabricTerrainBuildCount++;
        var plan=VisualFabric.Derive(snapshot,fabricDefinitions,fabricStats);FabricPlan=plan;
        var projects=snapshot.Get("projects",V.Map());var present=new HashSet<string>(StringComparer.Ordinal);
        foreach(var id in Ids(projects)) {
            var p=projects[id];string tile=p.Get("tile","").Text;if(p.Get("special",false).Bool||!tiles.ContainsKey(tile))continue;present.Add(id);
            string stamp=V.Arr(p.Get("tile",""),p.Get("def",""),p.Get("status",""),p.Get("paused",false),p.Get("operator_org",""),p.Get("slots",V.Map()),p.Get("level",0),p.Get("size_scale",1),p.Get("upgrade_job",V.Map()).Count>0,PlotGroundStamp(TilePosition(tile))).ToJson(false,true);
            if(fabricBuildings.TryGetValue(id,out var existing)&&existing.signature==stamp)continue;
            bool update=existing!=null;if(update)RemoveFabricNode(existing,false);
            var previous=new HashSet<Transform>();foreach(Transform child in settlementRoot)previous.Add(child);
            var coverage=V.Map();CreateBuilding(id,p,coverage);var root=buildings[id].gameObject;
            var children=new List<Transform>();foreach(Transform child in settlementRoot)if(!previous.Contains(child)&&child!=root.transform)children.Add(child);
            foreach(var child in children)child.SetParent(root.transform,true);
            int count=root.GetComponentsInChildren<Transform>(true).Length;fabricBuildings[id]=new FabricNode{root=root,signature=stamp,objects=count};fabricCoverage[id]=coverage;RecordFabricChange(update,count);
        }
        foreach(var id in fabricBuildings.Keys.Where(id=>!present.Contains(id)).ToArray()){RemoveFabricNode(fabricBuildings[id],true);fabricBuildings.Remove(id);fabricCoverage.Remove(id);buildings.Remove(id);}
        PruneSettlementSites();
        ResolveSettlementLayout(plan);
        var desired=new Dictionary<string,Tuple<string,Func<GameObject>>>(StringComparer.Ordinal);
        foreach(var id in Ids(plan["parcels"])) {
            var spec=plan["parcels"][id];string tile=spec["tile"].Text;var center=TilePosition(tile);var portals=FabricParcelPortals(plan,tile);
            var stamp=V.Arr(spec["stage"],spec["completed"],PlotGroundStamp(center),SettlementAccessSignature(tile),SettlementInfillSignature(tile),V.From(portals.Select(p=>V.Arr(p.x,Mathf.Round(p.y*10000)/10000,p.z)).ToArray())).ToJson();
            desired[id]=Tuple.Create(stamp,(Func<GameObject>)(()=>BuildFabricParcel(spec,portals)));
        }
        foreach(var id in Ids(plan["connectors"])) {
            var spec=plan["connectors"][id];if(!fabricRoutes.TryGetValue(id,out var points))continue;
            // Relation changes retain the same path until a visible morphology threshold changes.
            string stamp=V.Arr(spec["kind"],spec["stage"],spec["operator"],V.From(points.Select(p=>V.Arr(p.x,Mathf.Round(p.y*10000)/10000,p.z)).ToArray())).ToJson();
            desired[id]=Tuple.Create(stamp,(Func<GameObject>)(()=>BuildFabricConnector(spec,points)));
        }
        foreach(var id in Ids(plan["fillers"])) {
            var spec=plan["fillers"][id];if(IsSolidParcelInfill(spec)&&!settlementInfillPositions.ContainsKey(id))continue;
            var point=FabricFillerPosition(spec);if(!DryFabricFootprint(point,.16f))continue;
            if(spec.Has("edge")&&!desired.ContainsKey(spec["edge"].Text))continue;
            string stamp=V.Arr(spec,point.x,point.z,PlotGroundStamp(point)).ToJson(false,true);desired[id]=Tuple.Create(stamp,(Func<GameObject>)(()=>BuildFabricFiller(spec,point)));
        }
        foreach(var id in fabricNodes.Keys.Where(id=>!desired.ContainsKey(id)).ToArray()){RemoveFabricNode(fabricNodes[id],true);fabricNodes.Remove(id);}
        foreach(var entry in desired) {
            bool update=fabricNodes.TryGetValue(entry.Key,out var existing);if(update&&existing.signature==entry.Value.Item1)continue;if(update)RemoveFabricNode(existing,false);
            var root=entry.Value.Item2();int count=root.GetComponentsInChildren<Transform>(true).Length;fabricNodes[entry.Key]=new FabricNode{root=root,signature=entry.Value.Item1,objects=count};RecordFabricChange(update,count);
        }
        CollectSettlementRoadPaths();
        RefreshFabricForest(plan);UpdateFabricCoverage();
        int work=addedNodes+updatedNodes+deletedNodes+forestUploads+(terrainRebuilt?1:0);if(work>0){GeometryBuildCount++;Physics.SyncTransforms();}
        totalAdded+=addedObjects;totalUpdated+=updatedObjects;totalDeleted+=deletedObjects;
        FabricDiagnostics=V.Map("parcel_count",plan["parcels"].Count,"filler_count",fabricNodes.Keys.Count(x=>x.Contains(":infill:")),"connector_count",fabricNodes.Keys.Count(x=>x.StartsWith("edge:",StringComparison.Ordinal)&&!x.Contains(":infill:")),"building_count",fabricBuildings.Count,"forest_instance_count",foliage.InstanceCount,"forest_added_instances",forestAdded,"forest_removed_instances",forestRemoved,"forest_retained_instances",forestRetained,"added_nodes",addedNodes,"updated_nodes",updatedNodes,"deleted_nodes",deletedNodes,"added_objects",addedObjects,"updated_objects",updatedObjects,"deleted_objects",deletedObjects,"forest_uploads",forestUploads,"total_added_objects",totalAdded,"total_updated_objects",totalUpdated,"total_deleted_objects",totalDeleted,"scene_work",work,"stable_node_count",fabricBuildings.Count+fabricNodes.Count,"source_phase",SourcePhase,"levels",plan["levels"].Duplicate(true),"node_ids",V.From(fabricNodes.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray()));
        var fillerForms=fabricNodes.Where(pair=>pair.Key.Contains(":infill:")).Select(pair=>pair.Value.root.GetComponent<CityFabricIdentity>()).ToArray();
        FabricDiagnostics["filler_count_unit"]="stable presentation nodes, including open courts";
        FabricDiagnostics["filler_building_volume_count"]=fillerForms.Count(form=>form!=null&&form.HasBuildingVolume);
        FabricDiagnostics["open_court_count"]=fillerForms.Count(form=>form!=null&&form.PresentationForm=="open_court");
        FabricDiagnostics["terrain_rebuilt"]=terrainRebuilt;FabricDiagnostics["terrain_build_count"]=fabricTerrainBuildCount;FabricDiagnostics["terrain_update_scope"]="cached whole shared ground and tile colliders only when terrain/geography change";FabricDiagnostics["retained_nodes"]=fabricBuildings.Count+fabricNodes.Count-addedNodes-updatedNodes;
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
    bool DryFabricRoute(Vector3[] points)=>points.All(p=>DryFabricFootprint(p,.022f));
    bool DryFabricFootprint(Vector3 point,float radius)=>new[]{point,point+new Vector3(radius,0,radius),point+new Vector3(radius,0,-radius),point+new Vector3(-radius,0,radius),point+new Vector3(-radius,0,-radius)}.All(DryFabricPoint);
    GameObject FabricRibbon(string name,Vector3[] points,float width,Material material,Transform parent) {
        var root=Ribbon(name,points,width,material,parent);var mesh=root.GetComponent<MeshFilter>().sharedMesh;
        ConformToVisibleGround(mesh,root.transform,name=="SharedOperatorEdge"?.038f:name=="SharedCourt"?.030f:.026f);return root;
    }
    Vector3 GroundFabric(Vector3 p,float raise=.02f) {p.y=VisualGroundHeight(p)+raise;return p;}
    Vector3[] FabricParcelPortals(V plan,string tile) {
        var portals=new List<Vector3>();var center=TilePosition(tile);
        foreach(var id in Ids(plan["connectors"])) {
            var edge=plan["connectors"][id];string other=edge["a"].Text==tile?edge["b"].Text:edge["b"].Text==tile?edge["a"].Text:"";
            if(other==""||!fabricRoutes.TryGetValue(id,out var points))continue;
            portals.Add(edge["a"].Text==tile?points[0]:points[points.Length-1]);
        }
        return portals.ToArray();
    }
    GameObject BuildFabricConnector(V spec,Vector3[] points) {
        var root=FabricRoot(spec["id"].Text);root.GetComponent<CityFabricIdentity>().Kind=spec["kind"].Text;
        int stage=(int)spec["stage"].Long;
        FabricRibbon("SharedEdgeStreet",points,.09f+.009f*stage,SettlementRoadSurface(stage<2?"dirt":spec["kind"].Text.StartsWith("industrial",StringComparison.Ordinal)?"gravel":"stone"),root.transform);
        // Relation/operator history stays on the source plan and identity. One
        // continuous road owns the crossing; no transverse court/color patch.
        return root;
    }
    GameObject BuildFabricParcel(V spec,Vector3[] portals) {
        var root=FabricRoot(spec["id"].Text);root.GetComponent<CityFabricIdentity>().Kind="parcel_access";if(!spec["completed"].Bool){settlementAccessCounts.Remove(spec["tile"].Text);settlementParcelPaths.Remove(spec["tile"].Text);settlementUnreachable.Remove(spec["tile"].Text);settlementJunctionCount.Remove(spec["id"].Text);return root;}
        var center=TilePosition(spec["tile"].Text);int stage=(int)spec["stage"].Long;
        BuildSettlementAccess(spec["tile"].Text,center,portals,stage,root.transform);
        return root;
    }
    Vector3 FabricFillerPosition(V spec) {
        if(settlementInfillPositions.TryGetValue(spec["id"].Text,out var resolved))return resolved;
        int i=(int)spec["index"].Long;Vector3 point;
        if(spec.Has("parcel")) {
            var center=TilePosition(spec["parcel"].Text);var tile=Hex.coords(spec["parcel"].Text);
            float side=((tile.x+tile.y)&1)==0?1:-1;
            // Two homes form one short street-side group. The opposite side and
            // rear remain open courts: no five-house ring around every plot.
            var slots=new[]{new Vector2(side*.74f,.19f),new Vector2(side*.74f,-.19f),new Vector2(-side*.74f,.19f),new Vector2(-side*.74f,-.19f),new Vector2(0,-.76f)};
            var slot=slots[i%slots.Length];point=center+SettlementOffset(slot.x,slot.y);
        }
        else {
            var a=TilePosition(spec["a"].Text);var b=TilePosition(spec["b"].Text);var ca=Hex.coords(spec["a"].Text);var cb=Hex.coords(spec["b"].Text);
            bool gallery=i==3&&(ca.x+ca.y+cb.x+cb.y)%3==0;
            var along=Horizontal(b-a).normalized;var side=Vector3.Cross(Vector3.up,along);
            point=gallery?(a+b)*.5f:(a+b)*.5f+side*((i%2==0?1:-1)*(.25f+(i/2)*.21f))+along*((i/2==0?1:-1)*.055f);
        }
        return GroundFabric(point,.015f);
    }
    void FabricBox(string name,Transform parent,Vector3 position,Vector3 scale,Material material,float angle=0) {
        var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent,false);box.transform.position=position;box.transform.localScale=scale;box.transform.rotation=Quaternion.Euler(0,angle,0);Destroy(box.GetComponent<Collider>());box.GetComponent<Renderer>().sharedMaterial=material;
    }
    GameObject BuildFabricFiller(V spec,Vector3 point) {
        var root=FabricRoot(spec["id"].Text);string kind=spec["kind"].Text;var identity=root.GetComponent<CityFabricIdentity>();identity.Kind=kind;
        var entrance=FabricFillerEntrance(spec,point);var towardStreet=Horizontal(entrance-point).normalized;
        float rotation=Mathf.Atan2(towardStreet.x,towardStreet.z)*Mathf.Rad2Deg;int index=(int)spec["index"].Long;
        bool open=!spec.Has("parcel")||index>=2||kind=="mixed_court"||kind=="residential_walkway";
        identity.PresentationForm=open?"open_court":"street_building";identity.HasBuildingVolume=!open;
        if(open) {
            // This node represents source-backed unbuilt shared space. The
            // actual terrain is its presentation: no repeated green cube or
            // isolated access slash. Identity and diagnostics remain explicit.
            return root;
        }
        float h=.14f+.025f*Mathf.Clamp((int)spec["tier"].Long-1,0,2);var walls=BuildingWallMaterial(kind=="street_shop"?"commercial":"housing");var roof=BuildingRoofMaterial(kind=="street_shop"?"commercial":"housing");
        if(kind=="industrial_shed"||kind=="industrial_gallery"){walls=BuildingWallMaterial("industry");roof=BuildingRoofMaterial("industry");h*=.82f;}
        point.y=SmallSiteFloor(point,new Vector2(.22f,.10f),Quaternion.Euler(0,rotation,0));
        BuildSlopeSupport(root.transform,point,new Rect(-.11f,-.05f,.22f,.10f),Quaternion.Euler(0,rotation,0),point.y,"InfillFootprintRetainingSupport");
        FabricBox(kind+"_walls",root.transform,point+Vector3.up*h*.5f,new Vector3(.22f,h,.10f),walls,rotation);
        BuildFabricRoof(root.transform,point+Vector3.up*h,new Vector3(.24f,.045f,.13f),roof,rotation);
        var doorAccess=new[]{GroundFabric(point+towardStreet*.055f,.022f),entrance};
        if(DryFabricRoute(doorAccess))FabricRibbon("InfillDoorAccess",doorAccess,.038f,SettlementRoadSurface(kind=="industrial_shed"||kind=="industrial_gallery"?"gravel":"stone"),root.transform);
        return root;
    }
    void BuildFabricRoof(Transform parent,Vector3 position,Vector3 size,Material material,float angle) {
        // Separate roof faces keep the low ridge and eaves crisp, matching the
        // hard-edged imported landmarks rather than smoothing a six-vertex tent.
        var corners=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,1,0),new Vector3(.5f,1,0)};
        var faces=new[]{0,4,1,1,4,5,2,3,4,3,5,4,0,2,4,1,5,3};var vertices=faces.Select(i=>corners[i]).ToArray();
        var mesh=new Mesh{name="OriginalFabricGabledRoof",vertices=vertices,triangles=Enumerable.Range(0,vertices.Length).ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
        var roof=new GameObject("OriginalGabledRoof");roof.transform.SetParent(parent,false);roof.transform.position=position;roof.transform.rotation=Quaternion.Euler(0,angle,0);roof.transform.localScale=size;roof.AddComponent<MeshFilter>().sharedMesh=mesh;roof.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    void RefreshFabricForest(V plan) {
        forestAdded=forestRemoved=0;forestRetained=forestIds.Count;
        var parks=buildings.Values.Where(b=>b.AssignedPurpose=="green"&&b.Status!="construction").OrderBy(b=>b.BuildingId,StringComparer.Ordinal).ToArray();
        string signature=V.Arr(snapshot.Get("seed",0),terrainKey,V.From(plan["parcels"].Values.Select(p=>V.Arr(p["tile"],p["stage"],p["completed"])).ToArray()),V.From(parks.Select(p=>V.Arr(p.BuildingId,p.TileId)).ToArray()),NatureOccupancySignature()).ToJson();if(signature==forestSignature)return;forestSignature=signature;
        var placements=new List<NatureRequest>();var centers=tiles.Keys.Select(TilePosition).ToArray();string seed=snapshot.Get("seed",0).Text;
        float minX=centers.Length>0?centers.Min(p=>p.x)-1:0,maxX=centers.Length>0?centers.Max(p=>p.x)+1:0,minZ=centers.Length>0?centers.Min(p=>p.z)-1:0,maxZ=centers.Length>0?centers.Max(p=>p.z)+1:0;
        // Stable staggered samples form loose clusters; source-driven clearing never shifts surviving trees.
        const float spacing=.39f;var ids=new HashSet<string>(StringComparer.Ordinal);string terrainSignature=seed+"|"+terrainKey;
        if(forestTerrainSignature!=terrainSignature) {
            forestTerrainSignature=terrainSignature;forestCandidates.Clear();
            float clusterOffset=(float)VisualFabric.Unit("forest-clusters:"+seed,1)*100;
            for(int z=Mathf.FloorToInt(minZ/spacing);z<=Mathf.CeilToInt(maxZ/spacing);z++)for(int x=Mathf.FloorToInt(minX/spacing);x<=Mathf.CeilToInt(maxX/spacing);x++) {
                string id="forest:"+seed+":"+x+","+z;
                var point=new Vector3((x+(z%2==0?0:.5f)+((float)VisualFabric.Unit(id,1)-.5f)*.90f)*spacing,0,(z+((float)VisualFabric.Unit(id,2)-.5f)*.90f)*spacing);
                if(centers.All(p=>(p-point).sqrMagnitude>1.22f))continue;string nearest=tiles.Keys.OrderBy(tile=>(TilePosition(tile)-point).sqrMagnitude).First();if(VisualFabric.Water(Cells(snapshot)[nearest])||!DryFabricPoint(point))continue;
                float density=ForestDensityAt(point),cluster=Mathf.PerlinNoise(point.x*.72f+clusterOffset,point.z*.72f+clusterOffset);
                if(VisualFabric.Unit(id,3)>density*Mathf.Lerp(.82f,1.30f,cluster))continue;
                if(forestCandidates.Any(candidate=>{var delta=candidate.Item2-point;return delta.x*delta.x+delta.z*delta.z<.035f;}))continue;
                point.y=VisualGroundHeight(point);forestCandidates.Add(Tuple.Create(id,point,density));
            }
        }
        foreach(var candidate in forestCandidates) {
            string id=candidate.Item1;var point=candidate.Item2;string tile=tiles.Keys.OrderBy(t=>Horizontal(TilePosition(t)-point).sqrMagnitude).ThenBy(t=>t,StringComparer.Ordinal).First();
            int variant=ForestVariant(id,point);
            float edgeScale=Mathf.Lerp(.70f,1,Mathf.SmoothStep(0,1,candidate.Item3));
            float height=(.52f+(float)VisualFabric.Unit(id,4)*.30f)*edgeScale,crown=variant==2?.98f:1.14f+(float)VisualFabric.Unit(id,9)*.14f;
            placements.Add(NatureRequestAt(foliage,variant,id,tile,point,height,crown));
        }
        foreach(var park in parks)for(int i=0;i<3;i++) {
            string id="park:"+seed+":"+park.BuildingId+":"+i;var local=new[]{new Vector3(-.52f,0,-.24f),new Vector3(.43f,0,-.40f),new Vector3(-.30f,0,.51f)}[i];var point=TilePosition(park.TileId)+local;if(!DryFabricPoint(point))continue;point.y=VisualGroundHeight(point);
            placements.Add(NatureRequestAt(foliage,i%2,id,park.TileId,point,.48f+(float)VisualFabric.Unit(id,4)*.17f,1.02f));
        }
        AppendNatureDetails(placements,seed);ids=PlaceSettlementNature(placements);
        forestAdded=ids.Count(id=>!forestIds.Contains(id));forestRemoved=forestIds.Count(id=>!ids.Contains(id));forestRetained=ids.Count-forestAdded;forestIds=ids;
        forestUploads++;
    }
    void UpdateFabricCoverage() {
        var coverage=V.Map();foreach(var id in fabricCoverage.Keys.OrderBy(x=>x,StringComparer.Ordinal))coverage.Merge(fabricCoverage[id],true);
        var registered=assetRegistry["assets"];var primary=Bindings.Values.Select(x=>x["resource"].Text).Distinct(StringComparer.Ordinal).ToArray();var visible=coverage.Values.Where(x=>x.Get("rendered_primary",false).Bool).Select(x=>x["resource"].Text).Distinct(StringComparer.Ordinal).ToArray();
        AssetCoverage=V.Map("registered_assets",registered.Count,"loaded_prefab_count",models.Count,"loaded_resource_keys",V.From(models.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray()),"registered_cc0_assets",registered.Values.Count(x=>x.Get("origin","").Text=="kenney"),"registered_original_building_models",registered.Values.Count(x=>x.Get("origin","").Text=="project-original"&&x.Get("kind","building").Text=="building"),"original_tree_models",registered.Values.Count(x=>x.Get("origin","").Text=="project-original"&&x.Get("kind","").Text=="foliage"),"catalog_definition_bindings",Bindings.Count,"distinct_primary_models",primary.Length,"distinct_cc0_primary_models",primary.Count(x=>registered[x]["origin"].Text=="kenney"),"distinct_original_primary_models",primary.Count(x=>registered[x]["origin"].Text=="project-original"),"visible_distinct_primary_models",visible.Length,"visible_cc0_primary_models",visible.Count(x=>registered[x]["origin"].Text=="kenney"),"visible_definition_bindings",coverage,"registry_bindings",Bindings.Duplicate(true),"full_catalog_unique_models",primary.Length==Bindings.Count,"missing_assets",V.Arr(),"note",assetRegistry["scope_note"]);
    }
}
/// <summary>Stable renderer identity, deliberately separate from building IDs and simulation entities.</summary>
public sealed class CityFabricIdentity : MonoBehaviour {public string VisualId,Kind,PresentationForm;public bool HasBuildingVolume;}
}
