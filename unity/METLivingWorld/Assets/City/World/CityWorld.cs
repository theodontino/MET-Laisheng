using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace MET.City {
/// <summary>Detached city-snapshot projection. This component has no Game or persistence handle.</summary>
public sealed partial class CityWorld : MonoBehaviour {
    public const int GroundLayer=8;
    public static readonly string[] Layers={"terrain","service","employment","pollution","heat","stock"};
    public CityCamera CameraController {get;private set;}
    public string SelectedTile {get;private set;}="";
    public string HoveredTile {get;private set;}="";
    public string Layer {get;private set;}="terrain";
    public string SourcePhase {get;private set;}="actual";
    public string DeltaMetric {get;private set;}="pollution";
    public string Failure {get;private set;}="";
    public int GeometryBuildCount {get;private set;}
    public int TileCount=>tiles.Count;
    public int BuildingCount=>buildings.Count;
    public int RelationCount {get;private set;}
    public int OffMapRelationCount {get;private set;}
    public int SelfRelationCount {get;private set;}
    public bool AllLinks {get;private set;}
    public int DisplayedRelationCount {get;private set;}
    public int HiddenRelationCount {get;private set;}
    public V SourceRelations {get;private set;}=V.Arr();
    public event Action<string> LayerChanged;
    public event Action<string> HoverChanged;
    // Returning true consumes only a deliberate, mature map-inspection click.
    public Func<string,bool> TryInspectClick;
    public V AssetCoverage {get;private set;}=V.Map();
    public V SelectionInfo=>TileInfo(SelectedTile);
    public V Legend {get;private set;}=V.Map();
    Func<bool> keyboardOwned,selectionAllowed;
    Func<Vector2,bool> pointerOwned;
    Action<string> selectTile;
    V snapshot=V.Map(),baseline=V.Map(),proposal=V.Map();
    string structure="";
    Transform geometryRoot,analysisRoot;
    readonly Dictionary<string,CityTileIdentity> tiles=new Dictionary<string,CityTileIdentity>(StringComparer.Ordinal);
    readonly Dictionary<string,CityBuildingIdentity> buildings=new Dictionary<string,CityBuildingIdentity>(StringComparer.Ordinal);
    readonly Dictionary<string,GameObject> models=new Dictionary<string,GameObject>(StringComparer.Ordinal);
    readonly Dictionary<string,Material> terrainMaterials=new Dictionary<string,Material>(StringComparer.Ordinal);
    readonly List<Material> ownedMaterials=new List<Material>();
    readonly List<Mesh> ownedMeshes=new List<Mesh>();
    readonly List<LineRenderer> relations=new List<LineRenderer>();
    Material stone,road,bank,overlay,lineMaterial,construction,metal;
    LineRenderer hoverOutline,selectionOutline;
    readonly List<LineRenderer> inspectionOutlines=new List<LineRenderer>();
    readonly List<string> inspectionSourceTiles=new List<string>();
    CityFoliage foliage;
    VolumeProfile profile;
    static IEnumerable<string> Ids(V map)=>map.Keys.OrderBy(x=>x,StringComparer.Ordinal);
    static V Cells(V view)=>view.Get("cells",view.Get("tiles",V.Map()));
    V assetRegistry=V.Map();
    V Bindings=>assetRegistry.Get("bindings",V.Map());
    public void Initialize(Func<V> getSnapshot,Action<string> select,Func<bool> keyboard=null,Func<Vector2,bool> pointer=null,Func<bool> canSelect=null) {
        if(CameraController!=null)return;
        selectTile=select;keyboardOwned=keyboard;pointerOwned=pointer;selectionAllowed=canSelect;
        try {
            LoadMaterials();LoadModels();CreateEnvironment();
            geometryRoot=NewRoot("CommittedCityGeometry");analysisRoot=NewRoot("SourceAwareAnalysis");
            InitializeNature();
            hoverOutline=MakeLine("TileHover",.027f,Srgb("E9DDB9"),transform);selectionOutline=MakeLine("TileSelection",.041f,Srgb("F8E8A4"),transform);hoverOutline.loop=selectionOutline.loop=true;
            hoverOutline.enabled=selectionOutline.enabled=false;
            if(getSnapshot!=null)Present(getSnapshot());
        } catch(Exception error) {Failure=error.Message;Debug.LogException(error);}
    }
    public void SetSourcePhase(string value) {SourcePhase=value;UpdateLegend();}
    public void SetComparison(V sameStepBaseline,V sameStepProposal) {baseline=sameStepBaseline??V.Map();proposal=sameStepProposal??V.Map();if(Layer=="delta")RefreshAnalysis();}
    public void SetDeltaMetric(string value) {DeltaMetric=value;if(Layer=="delta")RefreshAnalysis();}
    public void Present(V detached) {
        if(detached==null||!detached.IsMap||geometryRoot==null)return;
        snapshot=detached;SourcePhase=detached.Get("source_phase",detached.Get("forecast",false).Bool?"next_tick":"actual").Text;
        PresentGeometryIncremental();
        if(SelectedTile!=""&&!tiles.ContainsKey(SelectedTile))SelectedTile="";
        SetOutline(selectionOutline,SelectedTile);RefreshAnalysis();
        RefreshInspectionSources();
    }
    string StructureKey(V view) {
        var cells=V.Arr();var projects=V.Arr();
        foreach(var id in Ids(Cells(view))) {
            var p=Cells(view)[id];cells.Add(V.Arr(id,p.Get("terrain","plain"),p.Get("traits",V.Arr()),p.Get("building","")));
        }
        foreach(var id in Ids(view.Get("projects",V.Map()))) {
            var p=view["projects"][id];if(p.Get("special",false).Bool)continue;
            projects.Add(V.Arr(id,p.Get("tile",""),p.Get("def",""),p.Get("status",""),p.Get("paused",false),p.Get("operator_org",""),p.Get("slots",V.Map()),p.Get("labels",V.Arr()),p.Get("level",0),p.Get("size_scale",1),p.Get("upgrade_job",V.Map()).Count>0));
        }
        return V.Arr(cells,projects).ToJson(false,true);
    }
    void RebuildGeometry() {
        ClearHover();
        if(settlementRoot==null){settlementRoot=NewRoot("CityPlotsAndStreets");settlementRoot.SetParent(geometryRoot,false);}
        foreach(Transform child in settlementRoot){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        foreach(var mesh in ownedMeshes)Destroy(mesh);ownedMeshes.Clear();buildings.Clear();
        EnsureLandscape();var coverage=V.Map();
        BuildRoads();
        foreach(var id in Ids(snapshot.Get("projects",V.Map()))) {
            var p=snapshot["projects"][id];if(p.Get("special",false).Bool||!tiles.ContainsKey(p.Get("tile","").Text))continue;
            CreateBuilding(id,p,coverage);
        }
        ScatterTrees();GeometryBuildCount++;
        var registered=assetRegistry["assets"];
        var primary=Bindings.Values.Select(x=>x["resource"].Text).Distinct(StringComparer.Ordinal).ToArray();
        var visible=coverage.Values.Where(x=>x.Get("rendered_primary",false).Bool).Select(x=>x["resource"].Text).Distinct(StringComparer.Ordinal).ToArray();
        AssetCoverage=V.Map("registered_assets",registered.Count,"loaded_prefab_count",models.Count,"loaded_resource_keys",V.From(models.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray()),"registered_cc0_assets",registered.Values.Count(x=>x.Get("origin","").Text=="kenney"),"registered_original_building_models",registered.Values.Count(x=>x.Get("origin","").Text=="project-original"&&x.Get("kind","building").Text=="building"),"original_tree_models",registered.Values.Count(x=>x.Get("origin","").Text=="project-original"&&x.Get("kind","").Text=="foliage"),"catalog_definition_bindings",Bindings.Count,"distinct_primary_models",primary.Length,"distinct_cc0_primary_models",primary.Count(x=>registered[x]["origin"].Text=="kenney"),"distinct_original_primary_models",primary.Count(x=>registered[x]["origin"].Text=="project-original"),"visible_distinct_primary_models",visible.Length,"visible_cc0_primary_models",visible.Count(x=>registered[x]["origin"].Text=="kenney"),"visible_definition_bindings",coverage,"registry_bindings",Bindings.Duplicate(true),"full_catalog_unique_models",primary.Length==Bindings.Count,"missing_assets",V.Arr(),"note",assetRegistry["scope_note"]);

        if(!openingFramed&&buildings.Count>0){CameraController.ShowSettlement(buildings.Values.Select(x=>TilePosition(x.TileId)).Aggregate(Vector3.zero,(a,b)=>a+b)/buildings.Count);openingFramed=true;}
        Physics.SyncTransforms();
    }
    public static Vector3 TilePosition(string id) {
        var coord=Hex.coords(id);
        // glTFast mirrors model geometry once. Place the axial map in Unity coordinates; never mirror models again.
        return new Vector3(-Mathf.Sqrt(3)*(coord.x+coord.y*.5f),0,-1.5f*coord.y);
    }
    static Vector3 Corner(int index,float radius) {float a=(30+60*index)*Mathf.Deg2Rad;return new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);}
    float GroundHeight(V cell)=>VisualGroundHeight(CellPosition(cell));
    float LocalHeight(V cell,Vector3 local)=>VisualGroundHeight(CellPosition(cell)+new Vector3(local.x,0,local.z));
    void CreateTile(string id,V cell) {
        string terrain=cell.Get("terrain","plain").Text;var holder=new GameObject("Tile_"+id);holder.transform.SetParent(groundRoot,false);holder.transform.localPosition=TilePosition(id);holder.layer=GroundLayer;
        var identity=holder.AddComponent<CityTileIdentity>();identity.TileId=id;identity.SurfaceHeight=VisualSurfaceHeight(TilePosition(id));identity.Terrain=terrain;tiles[id]=identity;
        var mesh=LandscapeHex(id,false);holder.AddComponent<MeshFilter>().sharedMesh=mesh;
        var render=holder.AddComponent<MeshRenderer>();render.sharedMaterial=terrainMaterials["continuous"];render.receiveShadows=true;render.shadowCastingMode=ShadowCastingMode.Off;render.enabled=false;
        holder.AddComponent<MeshCollider>().sharedMesh=mesh;
        var surface=new GameObject("AnalysisSurface");surface.transform.SetParent(holder.transform,false);surface.transform.localPosition=Vector3.up*.006f;
        surface.AddComponent<MeshFilter>().sharedMesh=LandscapeHex(id,true);identity.Analysis=surface.AddComponent<MeshRenderer>();identity.Analysis.sharedMaterial=overlay;identity.Analysis.shadowCastingMode=ShadowCastingMode.Off;identity.Analysis.receiveShadows=false;identity.Analysis.enabled=false;
    }
    void BuildRoads() {
        var projects=snapshot.Get("projects",V.Map());
        foreach(var id in Ids(projects)) {
            var p=projects[id];string tile=p.Get("tile","").Text;if(p.Get("special",false).Bool||!tiles.ContainsKey(tile)||!p.Get("traits",V.Arr()).Contains("building.road"))continue;
            foreach(var other in Ids(projects)) {
                if(StringComparer.Ordinal.Compare(id,other)>=0)continue;var q=projects[other];string end=q.Get("tile","").Text;
                if(q.Get("special",false).Bool||!tiles.ContainsKey(end)||!q.Get("traits",V.Arr()).Contains("building.road")||Hex.distance(tile,end)!=1)continue;
                var from=TilePosition(tile);var to=TilePosition(end);var points=new Vector3[9];
                for(int i=0;i<points.Length;i++){float t=i/8f;var point=Vector3.Lerp(from,to,t);string nearest=t<=.5f?tile:end;point.y=LocalHeight(Cells(snapshot)[nearest],point-TilePosition(nearest))+.015f;points[i]=point;}
                Ribbon("Street_"+id+"_"+other,points,.13f,road,settlementRoot);
            }
        }
    }
    void CreateBuilding(string id,V p,V coverage) {
        string def=p.Get("def","").Text;string tile=p["tile"].Text;var binding=BindingFor(p);string resource=binding["resource"].Text;var source=assetRegistry["assets"][resource];
        var holder=new GameObject("Building_"+id);holder.transform.SetParent(settlementRoot,false);holder.transform.localPosition=TilePosition(tile)+Vector3.up*(tiles[tile].SurfaceHeight+.012f);
        var identity=holder.AddComponent<CityBuildingIdentity>();identity.BuildingId=id;identity.TileId=tile;identity.DefinitionId=def;identity.ModelResource=resource;identity.Origin=source["origin"].Text;identity.License=source["license"].Text;identity.AssignedPurpose=binding.Get("purpose",Purpose(p)).Text;identity.DirectModel=identity.Origin=="project-original"&&resource=="CityModels/"+def;identity.Status=p.Get("status","running").Text;identity.OperatorId=p.Get("operator_org","").Text;identity.SlotCount=p.Get("slots",V.Map()).Values.Count(x=>x.Text!="");buildings[id]=identity;
        float visualScale=SettlementModelScale(def);
        float width=(float)binding.Get("width",1.08).Double*Mathf.Clamp((float)p.Get("size_scale",1).Double,.8f,1.1f)*visualScale;float height=(float)binding.Get("max_height",1.25).Double*visualScale;
        if(identity.Status=="construction"){AdaptSettlementConstruction(holder.transform);BuildSettlementCourt(holder.transform);BuildScaffolding(holder.transform);}
        else {
            var model=Instantiate(GetModel(resource),holder.transform,false);model.name="Imported_"+resource.Substring(resource.LastIndexOf('/')+1);identity.VisualHeight=FitImportedModel(model,holder.transform,width,height,(float)binding.Get("rotation",30).Double);
            AdaptSettlementBuilding(holder.transform,model);BuildSettlementCourt(holder.transform);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true)){HarmonizeImportedPalette(renderer,resource,identity.AssignedPurpose);renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;}
            BuildUseDetails(holder.transform,binding);
            BuildSettlementWings(holder.transform,identity.AssignedPurpose);
            if(p.Get("upgrade_job",V.Map()).Count>0)BuildScaffolding(holder.transform);
        }
        // Purpose, operator, slots and state remain on identity/UI. At town
        // scale the authored landmark and its low wings carry the map silhouette.
        if(p.Get("paused",false).Bool) {
            var barrier=GroundFabric(holder.transform.position+SettlementOffset(0,.55f),.035f);
            FabricBox("PausedEntranceBarrier",holder.transform,barrier,new Vector3(.25f,.035f,.045f),Surface("PausedEntrance",Srgb("917561")),30);
        }
        coverage[def]=V.Map("resource",resource,"origin",identity.Origin,"license",identity.License,"direct_original_model",identity.DirectModel,"purpose",identity.AssignedPurpose,"palette_family",BuildingPaletteFamily(identity.AssignedPurpose),"roof_color_srgb",ColorUtility.ToHtmlStringRGB(BuildingTypeColor(identity.AssignedPurpose)),"wall_color_srgb",ColorUtility.ToHtmlStringRGB(BuildingWallTone(identity.AssignedPurpose)),"assignment",binding.Get("assignment","Legacy family fallback"),"props",binding.Get("props",V.Arr()).Duplicate(true),"rendered_primary",identity.Status!="construction","normalized_height",identity.VisualHeight,"presentation_scale",visualScale,"rendered_props",V.Arr(),"simplified_wings",identity.Status!="construction");
    }
    V BindingFor(V p) {
        string def=p.Get("def","").Text;if(Bindings.Has(def))return Bindings[def];
        var traits=p.Get("traits",V.Arr());string fallback=traits.Contains("building.residential")?"residence":traits.Contains("building.agricultural")?"homestead":traits.Contains("building.service")?"neighborhood":"workshop";
        return V.Map("resource","CityModels/"+fallback,"purpose",Purpose(p),"width",1.0,"max_height",1.2);
    }
    // Bounds are read from official imported geometry after node transforms; this is not a GLB parser.
    public static Bounds GroundBounds(GameObject imported,Transform localSpace) {
        var filters=imported.GetComponentsInChildren<MeshFilter>(true);bool found=false;var bounds=new Bounds();
        foreach(var filter in filters) {
            if(filter.sharedMesh==null)continue;var box=filter.sharedMesh.bounds;
            for(int mask=0;mask<8;mask++) {
                var corner=new Vector3((mask&1)==0?box.min.x:box.max.x,(mask&2)==0?box.min.y:box.max.y,(mask&4)==0?box.min.z:box.max.z);
                var point=localSpace.InverseTransformPoint(filter.transform.TransformPoint(corner));if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
            }
        }
        if(!found)throw new InvalidOperationException("Imported city asset has no static mesh geometry: "+imported.name);return bounds;
    }
    static float FitImportedModel(GameObject imported,Transform parent,float width,float maxHeight,float angle) {
        imported.transform.localRotation=Quaternion.Euler(0,angle,0)*imported.transform.localRotation;
        var bounds=GroundBounds(imported,parent);float scale=Mathf.Min(width/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.z)),maxHeight/Mathf.Max(.001f,bounds.size.y));
        var originalPosition=imported.transform.localPosition;imported.transform.localScale*=scale;imported.transform.localPosition=originalPosition*scale-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;return bounds.size.y*scale;
    }
    void PlaceImportedProp(Transform parent,V prop) {
        string resource=prop["resource"].Text;var instance=Instantiate(GetModel(resource),parent,false);instance.name="PurposeProp_"+resource.Substring(resource.LastIndexOf('/')+1);FitImportedModel(instance,parent,(float)prop.Get("width",.24).Double,(float)prop.Get("max_height",.45).Double,(float)prop.Get("rotation",0).Double);
        var point=prop["position"];instance.transform.localPosition+=new Vector3((float)point[0].Double,(float)point[1].Double,(float)point[2].Double);
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)){renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;}
    }
    void BuildUseDetails(Transform parent,V binding) {
        string detail=binding.Get("surface_detail","").Text;var center=parent.position;
        var identity=parent.GetComponent<CityBuildingIdentity>();
        if(identity==null||!settlementSites.TryGetValue(identity.TileId,out var site))return;
        if(detail=="farmland"||detail=="modern_farmland") {
            // A compact side field leaves the primary door and its stairs open.
            // Reserve it before wings/infill/roads are resolved; rows remain terrain-following.
            var half=Vector2.zero;var slot=Vector2.zero;bool found=false;
            var sideSlots=new[]{new Vector2(-.66f,0),new Vector2(.66f,0),new Vector2(-.66f,-.20f),new Vector2(.66f,-.20f),new Vector2(-.66f,.20f),new Vector2(.66f,.20f)};
            foreach(var size in new[]{new Vector2(.12f,.245f),new Vector2(.09f,.16f),new Vector2(.07f,.115f)}) {
                var candidates=sideSlots.Concat(SettlementEmptyCandidates(sideSlots[0]).Where(v=>Mathf.Abs(v.x)>=Mathf.Abs(v.y)*.85f));
                foreach(var candidate in candidates) {
                    var patch=new Rect(candidate.x-size.x,candidate.y-size.y,size.x*2,size.y*2);
                    if(site.obstacles.Any(r=>r.Overlaps(ExpandedSiteRect(patch,.025f))))continue;
                    if(!SettlementSlotAvoidsReservedRoads(identity.TileId,patch))continue;
                    var corners=new[]{new Vector2(patch.xMin,patch.yMin),new Vector2(patch.xMax,patch.yMin),new Vector2(patch.xMax,patch.yMax),new Vector2(patch.xMin,patch.yMax)};
                    if(corners.Any(v=>!SettlementPointInTile(site.center+SettlementOffset(v.x,v.y),identity.TileId,.025f)))continue;
                    if(ExpandedSiteRect(patch,.12f).Contains(site.entrance))continue;
                    if(corners.Any(v=>!DryFabricPoint(site.center+SettlementOffset(v.x,v.y))))continue;
                    half=size;slot=candidate;site.obstacles.Add(ExpandedSiteRect(patch,.055f));found=true;break;
                }
                if(found)break;
            }
            if(!found)return;
            var soil=Surface("CultivatedSoil",Srgb("8F7959"));var crop=Surface("CropRows",Srgb("86975B"));
            for(int row=0;row<3;row++) {
                float x=slot.x+(row-1)*(half.x-.032f);
                var from=site.center+SettlementOffset(x,slot.y-half.y+.025f);var to=site.center+SettlementOffset(x,slot.y+half.y-.025f);
                FabricRibbon("FieldSoil",new[]{GroundFabric(from),GroundFabric(to)},Mathf.Min(.055f,half.x*.4f),soil,parent);
                var crops=FabricRibbon("CropRow",new[]{GroundFabric(from),GroundFabric(to)},Mathf.Min(.018f,half.x*.2f),crop,parent);crops.transform.position+=Vector3.up*.008f;
            }
        } else if(detail=="school_courtyard") {
            site.natureKeepClear.Add(new Rect(-.32f+site.offset.x,.40f+site.offset.y,.60f,.16f));
            var court=MakeLine("SchoolPlayCourt",.010f,Srgb("DFD9BC"),parent);court.loop=true;court.positionCount=4;
            court.SetPositions(new[]{GroundFabric(center+SettlementOffset(-.32f,.40f),.034f),GroundFabric(center+SettlementOffset(.28f,.40f),.034f),GroundFabric(center+SettlementOffset(.28f,.56f),.034f),GroundFabric(center+SettlementOffset(-.32f,.56f),.034f)});
        }
    }
    static string Purpose(V p) {
        var t=p.Get("traits",V.Arr());string def=p.Get("def","").Text;
        if(t.Contains("building.green"))return "green";if(t.Contains("building.logistics"))return "logistics";if(t.Contains("building.medical"))return "medical";if(t.Contains("building.education"))return "education";if(t.Contains("building.energy"))return "energy";if(t.Contains("building.residential"))return "housing";if(t.Contains("building.agricultural"))return "agriculture";if(t.Contains("building.commercial"))return "commercial";return "industry";
    }
    void BuildPurposeMarker(Transform parent,V p,bool unfinished,float modelHeight,string assignedPurpose) {
        string purpose=assignedPurpose;Color color=PurposeColor(purpose);float y=unfinished?.27f:.23f;
        var pole=MakeLine("PurposePole",.014f,Srgb("665F50"),parent);pole.positionCount=2;pole.SetPositions(new[]{parent.position+new Vector3(.47f,.02f,.52f),parent.position+new Vector3(.47f,y+.07f,.52f)});
        var material=Surface("Purpose_"+purpose,color);var position=parent.position+new Vector3(.47f,y,.52f);
        if(purpose=="medical") {
            var crossA=Ribbon("ClinicHorizontal",position+Vector3.left*.055f,position+Vector3.right*.055f,.028f,material,settlementRoot);crossA.transform.RotateAround(position,Vector3.right,90);
            var crossB=Ribbon("ClinicVertical",position+Vector3.back*.055f,position+Vector3.forward*.055f,.028f,material,settlementRoot);crossB.transform.RotateAround(position,Vector3.right,90);
        } else {
            MakeFlag(parent,color,"Purpose_"+purpose,y);
            if(purpose=="education") {
                var book=MakeLine("SchoolBookEmblem",.011f,Srgb("E8E7D5"),parent);book.positionCount=5;book.SetPositions(new[]{position+new Vector3(-.060f,-.022f,-.003f),position+new Vector3(-.060f,.035f,-.003f),position+new Vector3(0,.018f,-.003f),position+new Vector3(.060f,.035f,-.003f),position+new Vector3(.060f,-.022f,-.003f)});
            }
        }
    }
    void MakeFlag(Transform parent,Color color,string label,float y) {
        var mesh=new Mesh {name=label,vertices=new[]{new Vector3(.42f,y-.045f,.52f),new Vector3(.56f,y-.045f,.52f),new Vector3(.56f,y+.045f,.52f),new Vector3(.42f,y+.045f,.52f)},triangles=new[]{0,2,1,0,3,2}};mesh.RecalculateNormals();ownedMeshes.Add(mesh);
        var flag=new GameObject(label);flag.transform.SetParent(parent,false);flag.AddComponent<MeshFilter>().sharedMesh=mesh;var material=Surface(label,color);material.SetFloat("_Cull",(float)CullMode.Off);flag.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    void BuildScaffolding(Transform parent) {
        PlaceImportedProp(parent,V.Map("resource","CityModels/Kenney/Factory/crane","position",V.Arr(.45,0,-.35),"width",.24,"max_height",.43,"rotation",0));
        PlaceImportedProp(parent,V.Map("resource","CityModels/Kenney/Factory/box-small","position",V.Arr(-.43,0,.40),"width",.19,"max_height",.19,"rotation",0));
        var foundation=GameObject.CreatePrimitive(PrimitiveType.Cylinder);foundation.name="ConstructionFoundation";foundation.transform.SetParent(parent,false);foundation.transform.localPosition=Vector3.up*.018f;foundation.transform.localScale=new Vector3(1.18f,.018f,1.18f);Destroy(foundation.GetComponent<Collider>());foundation.GetComponent<Renderer>().sharedMaterial=construction;
        for(int i=0;i<6;i++) {
            var point=Corner(i,.56f);var line=MakeLine("ScaffoldPost",.019f,Srgb("94704B"),parent);line.positionCount=2;line.SetPositions(new[]{parent.position+point,parent.position+point+Vector3.up*.47f});
            var next=Corner((i+1)%6,.56f);var brace=MakeLine("ScaffoldRail",.016f,Srgb("B58B5C"),parent);brace.positionCount=2;brace.SetPositions(new[]{parent.position+point+Vector3.up*.33f,parent.position+next+Vector3.up*.33f});
        }
    }
    void ScatterTrees() {
        var placements=new List<Matrix4x4>();
        foreach(var id in tiles.Keys) {
            var cell=Cells(snapshot)[id];if(tiles[id].Terrain=="water"||cell.Get("building","").Text!="")continue;
            int candidates=tiles[id].Terrain=="forest"?14:tiles[id].Terrain=="hill"?2:2;var localPoints=new List<Vector3>();
            for(int i=0;i<candidates;i++) {
                if(tiles[id].Terrain!="forest"&&VisualUnit(id,i*7+3)>.34f)continue;
                float angle=(i*.6180339f+VisualUnit(id,1)*.12f)*Mathf.PI*2;float radius=.18f+VisualUnit(id,i*7+2)*.58f;
                var local=new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);if(localPoints.Any(x=>(x-local).sqrMagnitude<.12f))continue;
                localPoints.Add(local);var position=TilePosition(id)+local;position.y=LocalHeight(cell,local);
                float scale=.96f+VisualUnit(id,i*7+4)*.24f;placements.Add(Matrix4x4.TRS(position,Quaternion.Euler(0,VisualUnit(id,i*7+5)*360,0),Vector3.one*scale));
            }
        }
        foreach(var building in buildings.Values)if(building.AssignedPurpose=="green"&&building.Status!="construction") {
            foreach(var local in new[]{new Vector3(-.52f,0,-.24f),new Vector3(.43f,0,-.40f),new Vector3(-.30f,0,.51f)}) {
                var position=TilePosition(building.TileId)+local;position.y=LocalHeight(Cells(snapshot)[building.TileId],local);placements.Add(Matrix4x4.TRS(position,Quaternion.identity,Vector3.one*.98f));
            }
        }
        foliage.SetPlacements(placements);
    }
    static float VisualUnit(string id,int salt) {
        unchecked {uint value=2166136261;foreach(char c in id)value=(value^c)*16777619;value=(value^(uint)salt)*16777619;value^=value>>13;value*=1274126177;return (value&0xffffff)/16777216f;}
    }
    public void SetLayer(string value) {
        string next=NormalizeLayer(value);if(next==Layer)return;Layer=next;RefreshAnalysis();LayerChanged?.Invoke(Layer);
    }
    static string NormalizeLayer(string value) {
        switch(value) {
            case "terrain":case "service":case "employment":case "pollution":case "heat":case "stock":
            case "energy":case "food":case "material":case "goods":case "medical":case "education":case "entertainment":case "commercial":case "delta":return value;
            default:return "terrain";
        }
    }
    public void SetAllLinks(bool enabled) {
        if(AllLinks==enabled)return;AllLinks=enabled;DrawRelations();UpdateLegend();
    }
    public void CycleLayer(int direction=1) {
        string group=Layer=="food"||Layer=="material"||Layer=="goods"?"stock":Layer=="medical"||Layer=="education"||Layer=="entertainment"||Layer=="commercial"?"service":Layer;
        var available=ComparisonReady()?Layers.Concat(new[]{"delta"}).ToArray():Layers;
        int index=Array.IndexOf(available,group);if(index<0)index=0;SetLayer(available[(index+(direction<0?available.Length-1:1))%available.Length]);
    }
    public bool RequestSelection(string id,bool focus=true) {
        if(selectionAllowed!=null&&!selectionAllowed())return false;
        if(!tiles.ContainsKey(id??""))return false;
        selectTile?.Invoke(id);SelectTile(id,focus);return true;
    }
    public void SelectTile(string id,bool focus=true) {
        SelectedTile=tiles.ContainsKey(id??"")?id:"";SetOutline(selectionOutline,SelectedTile);
        if(focus&&SelectedTile!="")CameraController.Focus(TilePosition(SelectedTile));
        DrawRelations();UpdateLegend();
    }
    public void FocusSelected() {if(SelectedTile!=""&&tiles.ContainsKey(SelectedTile))CameraController.Focus(TilePosition(SelectedTile));}
    public string PickTile(Vector2 screenPoint) {
        if(CameraController==null)return "";var ray=CameraController.WorldCamera.ScreenPointToRay(screenPoint);
        if(!Physics.Raycast(ray,out var hit,150,1<<GroundLayer,QueryTriggerInteraction.Ignore))return "";
        var identity=hit.collider.GetComponent<CityTileIdentity>();return identity!=null?identity.TileId:"";
    }
    public void ClearHover() {if(HoveredTile!=""){HoveredTile="";HoverChanged?.Invoke("");}if(hoverOutline!=null)hoverOutline.enabled=false;}
    void Update() {
        if(CameraController==null||Failure!="")return;
        bool keyboard=keyboardOwned!=null&&keyboardOwned();var pointer=(Vector2)Input.mousePosition;
        if(!keyboard&&Input.GetKeyDown(KeyCode.L))CycleLayer(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?-1:1);
        if(keyboard||(pointerOwned!=null&&pointerOwned(pointer))) {ClearHover();return;}
        string hovered=PickTile(pointer);if(hovered!=HoveredTile){HoveredTile=hovered;SetOutline(hoverOutline,hovered);HoverChanged?.Invoke(hovered);}
        if(Input.GetMouseButtonDown(0)&&hovered!=""&&!(TryInspectClick?.Invoke(hovered)??false))RequestSelection(hovered,true);
    }
    void SetOutline(LineRenderer line,string id) {
        if(line==null)return;line.enabled=id!=""&&tiles.ContainsKey(id);if(!line.enabled)return;
        var center=TilePosition(id);const int steps=8;line.positionCount=6*steps;
        for(int edge=0;edge<6;edge++)for(int step=0;step<steps;step++){var local=Vector3.Lerp(Corner(edge,.985f),Corner((edge+1)%6,.985f),step/(float)steps);local.y=VisualSurfaceHeight(center+local)+.045f;line.SetPosition(edge*steps+step,center+local);}

    }
    public V InspectionForTile(string id) => MapInspection.Describe(snapshot,id,Layer,baseline,proposal,DeltaMetric,true);
    public bool InspectionScreenPosition(string id,out Vector2 screen) {
        screen=Vector2.zero;
        if(CameraController==null||!tiles.TryGetValue(id??"",out var ground))return false;
        var position=TilePosition(id)+Vector3.up*(ground.SurfaceHeight+.20f);
        var projected=CameraController.WorldCamera.WorldToScreenPoint(position);
        if(projected.z<=0)return false;
        screen=new Vector2(projected.x,projected.y);return true;
    }
    public void HighlightInspectionSources(IEnumerable<string> sources) {
        inspectionSourceTiles.Clear();
        if(sources!=null)foreach(var source in sources) {
            var tile=SourceTile(source??"");
            if(tiles.ContainsKey(tile)&&!inspectionSourceTiles.Contains(tile))inspectionSourceTiles.Add(tile);
        }
        RefreshInspectionSources();
    }
    void RefreshInspectionSources() {
        for(int i=0;i<inspectionSourceTiles.Count;i++) {
            LineRenderer line;
            if(i<inspectionOutlines.Count)line=inspectionOutlines[i];
            else {line=MakeLine("InspectionSource_"+i,.055f,Srgb("69D9CD"),transform);line.loop=true;inspectionOutlines.Add(line);}
            SetOutline(line,inspectionSourceTiles[i]);
        }
        for(int i=inspectionSourceTiles.Count;i<inspectionOutlines.Count;i++)inspectionOutlines[i].enabled=false;
    }
    public bool FocusInspectionSource(string source) {
        var tile=SourceTile(source??"");
        if(!tiles.ContainsKey(tile)||CameraController==null)return false;
        CameraController.Focus(TilePosition(tile));return true;
    }
    public V TileInfo(string id) {
        if(!Cells(snapshot).Has(id??""))return V.Map();var cell=Cells(snapshot)[id];var info=V.Map("tile",id,"terrain",cell.Get("terrain","plain"),"fields",cell.Get("fields",V.Map()).Duplicate(true),"source_phase",SourcePhase,"layer",Layer,"value",LayerValue(snapshot,id,Layer),"projects",V.Arr());
        foreach(var bid in Ids(snapshot.Get("projects",V.Map())))if(snapshot["projects"][bid].Get("tile","").Text==id)info["projects"].Add(V.Map("id",bid,"def",snapshot["projects"][bid]["def"],"inventory",snapshot["projects"][bid].Get("inventory",V.Map()).Duplicate(true),"operator",snapshot["projects"][bid].Get("operator_org",""),"status",snapshot["projects"][bid].Get("status","")));
        return info;
    }
    double LayerValue(V view,string tile,string layer) {
        if(layer=="delta"&&!ComparisonReady())return 0;
        return MapInspection.Value(view,tile,layer,baseline,proposal,DeltaMetric);
    }
    bool ComparisonReady()=>baseline.IsMap&&proposal.IsMap&&baseline.Has("tick")&&proposal.Has("tick")&&baseline["tick"]==proposal["tick"]&&Cells(baseline).Count>0&&Cells(proposal).Count>0;

    void RefreshAnalysis() {
        if(analysisRoot==null)return;
        double max=Layer=="service"||Layer=="medical"||Layer=="education"||Layer=="entertainment"||Layer=="commercial"?1:Layer=="pollution"?2:Layer=="heat"?5:Layer=="employment"?3:Layer=="energy"?10:Layer=="delta"?1:30;
        var block=new MaterialPropertyBlock();
        foreach(var pair in tiles) {
            pair.Value.Analysis.enabled=Layer!="terrain";if(Layer=="terrain")continue;
            double value=LayerValue(snapshot,pair.Key,Layer);float t=Mathf.Clamp01((float)Math.Abs(value)/Mathf.Max(.001f,(float)max));
            bool burden=DeltaMetric=="pollution";
            Color low=Srgb("798A75"),high=Layer=="pollution"?Srgb("B95642"):Layer=="heat"?Srgb("DB9545"):Layer=="delta"?((burden?value>0:value<0)?Srgb("A65745"):Srgb("5C9C90")):Layer=="energy"?(value<0?Srgb("A65745"):Srgb("5C9C90")):Srgb("599792");
            var color=Color.Lerp(low,high,t);color.a=.53f;block.SetColor("_BaseColor",color);pair.Value.Analysis.SetPropertyBlock(block);
        }
        DrawRelations();UpdateLegend();
    }
    void UpdateLegend() {
        if(!snapshot.IsMap)return;
        int affected=0;double aggregate=0;
        foreach(var id in tiles.Keys){double value=LayerValue(snapshot,id,Layer);if(Math.Abs(value)>1e-9)affected++;aggregate+=value;}
        Legend=V.Map("layer",Layer,"source_phase",SourcePhase,"city_tiles",tiles.Count,"affected_tiles",affected,"area_fraction",tiles.Count>0?(double)affected/tiles.Count:0,"value_sum",aggregate,"relation_count",RelationCount,"offmap_relations",OffMapRelationCount,"self_relations",SelfRelationCount,"all_links",AllLinks,"displayed_relations",DisplayedRelationCount,"hidden_relations",HiddenRelationCount,"relation_scope",AllLinks?"city":SelectedTile!=""?"selected":"city_preview","comparison_ready",ComparisonReady(),"delta_metric",DeltaMetric,"service_reading","Delivered coverage for occupied tiles; best source reach for unoccupied tiles.","employment_reading","Employment coverage for occupied tiles; reachable provider access for unoccupied tiles.","energy_reading","Local source capacity minus local demand; municipal supply remains in the city ledger.");
        Legend["delta_label"]=DeltaMetric=="pollution"?"污染场强变化":"同一步 "+DeltaMetric+" 变化";
        Legend["delta_polarity"]=DeltaMetric=="pollution"?"红色增加污染，绿色减少污染":"绿色增加供给，红色减少供给";
    }
    void DrawRelations() {
        foreach(var line in relations)line.enabled=false;int index=0;RelationCount=OffMapRelationCount=SelfRelationCount=DisplayedRelationCount=HiddenRelationCount=0;SourceRelations=V.Arr();var metrics=snapshot.Get("metrics",V.Map());
        string kind=Layer=="service"?"medical":Layer;
        if(kind=="medical"||kind=="education"||kind=="entertainment"||kind=="commercial")foreach(var link in metrics.Get("service_links",V.Arr())) {
            if(link.Get("kind","").Text!=kind)continue;DrawRelation(link.Get("source","").Text,link.Get("home","").Text,Srgb("88BDB1"),ref index);
        } else if(kind=="employment")foreach(var link in metrics.Get("employment_links",V.Arr())) {
            DrawRelation(link.Get("source",link.Get("group","")).Text,link.Get("target","").Text,Srgb("D9B366"),ref index);
        } else if(kind=="pollution")foreach(var link in metrics.Get("exposure_links",V.Arr())) {
            DrawRelation(link.Get("source","").Text,link.Get("home","").Text,Srgb("C77D66"),ref index);
        } else if(kind=="heat")foreach(var receipt in metrics.Get("heat_receipts",V.Arr()))foreach(var source in receipt.Get("sources",V.Arr())) {
            DrawRelation(source.Get("source","").Text,receipt.Get("target","").Text,Srgb("E5A658"),ref index);
        } else if(kind=="stock"||kind=="food"||kind=="material"||kind=="goods") {
            string resource=kind=="stock"?"food":kind;
            foreach(var link in metrics.Get("allocation",V.Arr())) {
                if(link.Get("resource","").Text!=resource)continue;string consumer=link.Get("consumer","").Text;string target=ConsumerOwner(consumer);
                DrawRelation(link.Get("source","").Text,target,Srgb("BFCA91"),ref index);
            }
            foreach(var link in metrics.Get("stock_movements",V.Arr()))if(link.Get("resource","").Text==resource)DrawRelation(link.Get("source","").Text,link.Get("target","").Text,Srgb("D1D9A7"),ref index);
        }
    }
    string ConsumerOwner(string consumer) {
        if(consumer.StartsWith("pop:",StringComparison.Ordinal))return consumer.Substring(4);
        if(consumer.StartsWith("run:",StringComparison.Ordinal))return consumer.Substring(4);
        if(consumer.StartsWith("work:",StringComparison.Ordinal))return consumer.Substring(5);
        // Recipe UID ends in the selected building ID; preserve that owner, not a nearby guess.
        if(consumer.StartsWith("recipe:",StringComparison.Ordinal)) {int last=consumer.LastIndexOf(':');return last>=0?consumer.Substring(last+1):"";}return consumer;
    }
    bool SourcePosition(string source,out Vector3 position) {
        var projects=snapshot.Get("projects",V.Map());var groups=snapshot.Get("groups",V.Map());if(groups.Has(source))source=groups[source]["host"].Text;
        string tile=projects.Has(source)?projects[source].Get("tile","").Text:source;
        if(tiles.TryGetValue(tile,out var ground)){position=TilePosition(tile)+Vector3.up*(ground.SurfaceHeight+.12f);return true;}position=Vector3.zero;return false;
    }
    string SourceTile(string source) {
        var projects=snapshot.Get("projects",V.Map());var groups=snapshot.Get("groups",V.Map());if(groups.Has(source))source=groups[source]["host"].Text;
        return projects.Has(source)?projects[source].Get("tile","").Text:source;
    }
    void DrawRelation(string source,string target,Color color,ref int index) {
        string fromTile=SourceTile(source),toTile=SourceTile(target);
        var provenance=V.Map("source",source,"target",target,"source_tile",fromTile,"target_tile",toTile,"layer",Layer,"displayed",false);
        SourceRelations.Add(provenance);
        if(!SourcePosition(source,out var from)||!SourcePosition(target,out var to)){OffMapRelationCount++;return;}
        if((from-to).sqrMagnitude<.001f){SelfRelationCount++;return;}RelationCount++;
        bool selected=SelectedTile==""||fromTile==SelectedTile||toTile==SelectedTile;
        // Keep every source record; default presentation bounds the selected relationships to six lines.
        if(!AllLinks&&(!selected||index>=6)){HiddenRelationCount++;return;}
        provenance["displayed"]=true;DisplayedRelationCount++;
        LineRenderer line;if(index<relations.Count)line=relations[index];else {line=MakeLine("SourceRelation_"+index,.014f,color,analysisRoot);relations.Add(line);}
        TintLine(line,color);line.enabled=true;line.positionCount=9;
        for(int i=0;i<9;i++){float t=i/8f;line.SetPosition(i,Vector3.Lerp(from,to,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.18f);}index++;
    }
    Transform NewRoot(string label) {var root=new GameObject(label);root.transform.SetParent(transform,false);return root.transform;}
    static Color Srgb(string html) {ColorUtility.TryParseHtmlString("#"+html,out var value);return value;}
    static Color SurfaceColor(Color srgb)=>QualitySettings.activeColorSpace==ColorSpace.Linear?srgb.linear:srgb;
    static Color OperatorColor(string id) {
        switch(id) {case "commons":return Srgb("729785");case "steel":return Srgb("8C9BA3");case "civic":return Srgb("C3AC79");case "meridian":return Srgb("AD8973");default:return Srgb("A39F83");}
    }
    static Color PurposeColor(string purpose)=>BuildingTypeColor(purpose);
    readonly Dictionary<string,Material> surfaceCache=new Dictionary<string,Material>(StringComparer.Ordinal);
    Material Surface(string name,Color color) {
        if(surfaceCache.TryGetValue(name,out var found))return found;
        var template=Resources.Load<Material>(name=="ContinuousLivingValley"?"Rendering/CityTerrainSurface":"Rendering/CitySurface");var shader=template!=null?template.shader:Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)throw new InvalidOperationException("The official URP Lit surface shader is unavailable.");
        var material=template!=null?new Material(template):new Material(shader);material.name="City_"+name;material.SetColor("_BaseColor",SurfaceColor(color));material.SetFloat("_Smoothness",.16f);material.enableInstancing=true;ownedMaterials.Add(material);surfaceCache[name]=material;return material;
    }
    void LoadMaterials() {
        terrainMaterials["plain"]=Surface("Grassland",Srgb("87926B"));terrainMaterials["forest"]=Surface("ForestGround",Srgb("738265"));terrainMaterials["hill"]=Surface("Highland",Srgb("9D9A7D"));terrainMaterials["wet"]=Surface("Wetland",Srgb("758B79"));terrainMaterials["water"]=Surface("RiverWater",Srgb("73BAC9"));terrainMaterials["water"].SetFloat("_Smoothness",.64f);
        stone=Surface("ValleySides",Srgb("807F6D"));road=Surface("Streets",Srgb("C9B994"));bank=Surface("SandyBank",Srgb("C7B78D"));construction=Surface("Construction",Srgb("9C9380"));metal=Surface("InstalledScheme",Srgb("E0D1A0"));metal.SetFloat("_Metallic",.35f);
        var template=Resources.Load<Material>("Rendering/CityOverlay")??Resources.Load<Material>("Rendering/HeroOverlay");var shader=template!=null?template.shader:Shader.Find("Universal Render Pipeline/Unlit");
        if(shader==null)throw new InvalidOperationException("The official URP Unlit analysis shader is unavailable.");
        overlay=template!=null?new Material(template):new Material(shader);overlay.name="ColliderFreeCityAnalysis";
        overlay.SetFloat("_Surface",1);overlay.SetFloat("_Blend",0);overlay.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);overlay.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);overlay.SetFloat("_ZWrite",0);overlay.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");overlay.renderQueue=(int)RenderQueue.Transparent;ownedMaterials.Add(overlay);
        lineMaterial=new Material(overlay);lineMaterial.name="CitySourceRelations";lineMaterial.SetColor("_BaseColor",Color.white);ownedMaterials.Add(lineMaterial);
    }
    void LoadModels() {
        var registry=Resources.Load<TextAsset>("CityModels/Kenney/unity_city_assets");if(registry==null)throw new InvalidOperationException("The Unity city asset registry is missing.");assetRegistry=V.Parse(registry.text);
    }
    GameObject GetModel(string resource) {
        if(models.TryGetValue(resource,out var cached))return cached;
        var prefab=Resources.Load<GameObject>(resource);if(prefab==null)throw new InvalidOperationException("Import "+resource+".glb with the official glTFast Editor importer before running the city.");models[resource]=prefab;return prefab;
    }
    void CreateEnvironment() {
        var pipeline=QualitySettings.renderPipeline!=null?QualitySettings.renderPipeline:GraphicsSettings.defaultRenderPipeline;
        if(!(pipeline is UniversalRenderPipelineAsset))throw new InvalidOperationException("The city requires the configured official URP pipeline.");
        var cameraObject=new GameObject("CityStrategicCamera");cameraObject.transform.SetParent(transform,false);var camera=cameraObject.AddComponent<Camera>();camera.tag="MainCamera";cameraObject.AddComponent<AudioListener>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Srgb("CEE2DC");camera.allowHDR=true;camera.allowMSAA=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        CameraController=cameraObject.AddComponent<CityCamera>();CameraController.Initialize(keyboardOwned,pointerOwned);
        var daylight=new GameObject("FullCityNeutralDaylight");daylight.transform.SetParent(transform,false);var sun=daylight.AddComponent<Light>();sun.type=LightType.Directional;sun.color=Srgb("FFFDF6");sun.intensity=1.12f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.76f;sun.shadowBias=.035f;sun.transform.rotation=Quaternion.LookRotation(new Vector3(-.55f,-.72f,-.42f),Vector3.up);RenderSettings.sun=sun;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Srgb("9EADB7");RenderSettings.ambientEquatorColor=Srgb("89928F");RenderSettings.ambientGroundColor=Srgb("68685F");
        RenderSettings.fog=false;
        var volumeObject=new GameObject("FullCityDaylightToneMap");volumeObject.transform.SetParent(transform,false);var volume=volumeObject.AddComponent<Volume>();volume.isGlobal=true;profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(0);color.saturation.Override(0);color.contrast.Override(8);volume.profile=profile;
    }
    static Color ReadColor(V color)=>new Color((float)color[0].Double,(float)color[1].Double,(float)color[2].Double,color.Count>3?(float)color[3].Double:1);
    LineRenderer MakeLine(string name,float width,Color color,Transform parent) {
        var holder=new GameObject(name);holder.transform.SetParent(parent,false);var line=holder.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;line.useWorldSpace=true;line.widthMultiplier=width;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.numCapVertices=2;line.numCornerVertices=2;TintLine(line,color);return line;
    }
    static void TintLine(LineRenderer line,Color color) {var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);line.SetPropertyBlock(block);line.startColor=line.endColor=Color.white;}
    GameObject Ribbon(string name,Vector3 a,Vector3 b,float width,Material material,Transform parent)=>Ribbon(name,new[]{a,b},width,material,parent);
    GameObject Ribbon(string name,Vector3[] points,float width,Material material,Transform parent) {
        var vertices=new Vector3[points.Length*2];var triangles=new int[(points.Length-1)*6];
        for(int i=0;i<points.Length;i++) {
            var before=Horizontal(points[i]-points[Mathf.Max(0,i-1)]).normalized;
            var after=Horizontal(points[Mathf.Min(points.Length-1,i+1)]-points[i]).normalized;
            if(i==0)before=after;if(i==points.Length-1)after=before;
            var incoming=Vector3.Cross(Vector3.up,before);var outgoing=Vector3.Cross(Vector3.up,after);
            var join=(incoming+outgoing).normalized;
            // Intersect both sides of a corner instead of turning one vertex pair
            // toward the next segment, which shears a broad triangular wedge.
            float alignment=Vector3.Dot(join,outgoing);
            var right=alignment>.25f?join*(width*.5f/Mathf.Max(.5f,alignment)):outgoing*(width*.5f);
            vertices[i*2]=parent.InverseTransformPoint(points[i]-right);vertices[i*2+1]=parent.InverseTransformPoint(points[i]+right);
            if(i+1<points.Length){int k=i*6,v=i*2;triangles[k]=v;triangles[k+1]=v+2;triangles[k+2]=v+1;triangles[k+3]=v+1;triangles[k+4]=v+2;triangles[k+5]=v+3;}
        }
        var mesh=new Mesh {name=name,vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);var holder=new GameObject(name);holder.transform.SetParent(parent,false);holder.AddComponent<MeshFilter>().sharedMesh=mesh;var render=holder.AddComponent<MeshRenderer>();render.sharedMaterial=material;render.shadowCastingMode=ShadowCastingMode.Off;return holder;
    }
    void OnDestroy() {DisposeImportedPalette();DestroyLandscape();if(plotCourtMesh!=null)Destroy(plotCourtMesh);foreach(var mesh in ownedMeshes)if(mesh!=null)Destroy(mesh);foreach(var material in ownedMaterials)if(material!=null)Destroy(material);if(profile!=null)Destroy(profile);}
}
/// <summary>Tile authority is its stable ID, independent of scene node names and world coordinates.</summary>
public sealed class CityTileIdentity : MonoBehaviour {
    public string TileId,Terrain;
    public float SurfaceHeight;
    public MeshRenderer Analysis;
}
public sealed class CityBuildingIdentity : MonoBehaviour {
    public string BuildingId,TileId,DefinitionId,ModelResource,Status,OperatorId,Origin,License,AssignedPurpose;
    public float VisualHeight;
    public bool DirectModel;
    public int SlotCount;
}
}
