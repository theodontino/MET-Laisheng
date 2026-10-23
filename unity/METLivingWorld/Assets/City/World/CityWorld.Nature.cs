using System;
using System.Collections.Generic;
using System.Linq;
using MET.Core;
using UnityEngine;
namespace MET.City {
public sealed partial class CityWorld {
    CityFoliage natureDetails;
    // Original Hero Valley assets remain separate; these CC0 models serve the dynamic city.
    static readonly string[] NatureNames={"CommonTree_3","CommonTree_5","Pine_5","Bush_Common","Rock_Medium_1","Rock_Medium_2"};
    public V NatureDiagnostics=>V.Map("source","Quaternius Stylized Nature MegaKit Standard CC0","tree_variants",3,"detail_variants",3,"tree_instances",foliage?.InstanceCount??0,"detail_instances",natureDetails?.InstanceCount??0,"tree_draw_batches",foliage?.DrawBatchCount??0,"detail_draw_batches",natureDetails?.DrawBatchCount??0,"texture_max_size",512,"leaf_texture_max_size",512,"canopy_display_normals","ellipsoidal crown and density-scaled forest edge","leaf_shadow_receive",false,"original_positions_retained",natureOriginalRetained,"same_tile_relocated",natureRelocated,"no_free_same_tile_space",natureUnplaced,"occupied_overlap_count",natureOccupiedOverlap,"outside_source_tile_count",natureOutsideSourceTile);
    void InitializeNature() {
        var prototypes=NatureNames.Select(name=>{
            var model=Resources.Load<GameObject>("CityNature/Quaternius/"+name);
            if(model==null)throw new InvalidOperationException("Import CityNature/Quaternius/"+name+".gltf with official glTFast before running the city.");
            return model;
        }).ToArray();
        var trees=new GameObject("SharedMixedBroadleafForest");trees.transform.SetParent(transform,false);foliage=trees.AddComponent<CityFoliage>();foliage.Initialize(prototypes.Take(3).ToArray());
        var details=new GameObject("SharedBankAndHillNature");details.transform.SetParent(transform,false);natureDetails=details.AddComponent<CityFoliage>();natureDetails.Initialize(prototypes.Skip(3).ToArray());
    }
    static CityFoliage.Placement NaturePlacement(CityFoliage batch,int variant,Vector3 point,float height,float crown,string id,float maxWidth=0,float embedFraction=0) {
        var bounds=batch.VariantBounds(variant);float size=height/Mathf.Max(.001f,bounds.size.y);
        float horizontal=size*crown;
        if(maxWidth>0)horizontal=Mathf.Min(horizontal,maxWidth/Mathf.Sqrt(bounds.size.x*bounds.size.x+bounds.size.z*bounds.size.z));
        var scale=new Vector3(horizontal,size,horizontal);var rotation=Quaternion.Euler(0,(float)VisualFabric.Unit(id,5)*360,0);
        var offset=rotation*Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),scale);
        point.y-=height*embedFraction;
        return new CityFoliage.Placement(variant,Matrix4x4.TRS(point-offset,rotation,scale));
    }
    int ForestVariant(string id,Vector3 point) {
        float pineChance=HeightAt(point)>.75f?.14f:.06f;
        float choice=(float)VisualFabric.Unit(id,8);return choice<pineChance?2:choice<.57f?0:1;
    }
    void AppendNatureDetails(List<NatureRequest> placements,string seed) {
        foreach(var tile in Ids(Cells(snapshot))) {
            var cell=Cells(snapshot)[tile];if(VisualFabric.Water(cell))continue;
            var center=TilePosition(tile);bool hill=cell.Get("terrain","").Text=="hill";
            for(int i=0;i<5;i++) {
                string id="nature:"+seed+":"+tile+":"+i;
                float angle=(float)VisualFabric.Unit(id,1)*Mathf.PI*2,radius=.32f+(float)VisualFabric.Unit(id,2)*.58f;
                var point=center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                float shoreDistance=RiverDistance(point)-RiverHalfWidth(point);bool bank=shoreDistance>.13f&&shoreDistance<.62f;
                bool toe=hill&&HeightAt(point)<HeightAt(center)+.04f;
                bool edge=ForestDensityAt(point)>.08f&&ForestDensityAt(point)<.65f;
                if(!(bank||toe||edge)||!DryFabricPoint(point))continue;
                if(VisualFabric.Unit(id,3)>(bank?.30f:toe?.25f:.25f))continue;
                int variant=(bank||toe)&&VisualFabric.Unit(id,4)<.55f?1+(VisualFabric.Unit(id,6)<.5?0:1):0;
                point.y=VisualGroundHeight(point);float height=variant==0?.13f+(float)VisualFabric.Unit(id,7)*.14f:.11f+(float)VisualFabric.Unit(id,7)*.16f;
                placements.Add(NatureRequestAt(natureDetails,variant,id,tile,point,height,variant==0?1.05f:1.15f,variant==0?0:.45f,variant==0?0:.32f));
            }
            if(hill) {
                // Read the current shared surface to identify a crest and its exposed downhill shoulder.
                var crest=center;float crestHeight=VisualGroundHeight(center);var shoulder=center;float shoulderHeight=crestHeight;
                for(int i=0;i<6;i++) {
                    var sample=center+Corner(i,.68f);float height=VisualGroundHeight(sample);
                    if(height>crestHeight){crest=sample;crestHeight=height;}
                    if(height<shoulderHeight){shoulder=sample;shoulderHeight=height;}
                }
                AddHillOutcrop(placements,tile,crest,seed+":"+tile+":crest",.22f);
                if(crestHeight-shoulderHeight>.12f&&VisualFabric.Unit("hill-shoulder:"+seed+":"+tile,1)<.30f)AddHillOutcrop(placements,tile,Vector3.Lerp(crest,shoulder,.65f),seed+":"+tile+":shoulder",.18f);
            }
        }
    }
    void AddHillOutcrop(List<NatureRequest> placements,string tile,Vector3 center,string clusterId,float height) {
        if(!DryFabricFootprint(center,.20f))return;
        float angle=(float)VisualFabric.Unit(clusterId,1)*Mathf.PI*2;var along=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
        // A small exposed ledge and one lower companion leave the hill's own silhouette dominant.
        for(int i=0;i<2;i++) {
            string id="outcrop:"+clusterId+":"+i;var point=center+along*(i==0?-.05f:.13f)+new Vector3(-along.z,0,along.x)*(i==0?.035f:-.035f);
            if(!DryFabricPoint(point))continue;
            point.y=VisualGroundHeight(point);float size=height*(i==0?1:.64f+(float)VisualFabric.Unit(id,2)*.12f);
            placements.Add(NatureRequestAt(natureDetails,1+i%2,id,tile,point,size,1.10f,i==0?.40f:.30f,i==0?.40f:.45f));
        }
    }
}
}
