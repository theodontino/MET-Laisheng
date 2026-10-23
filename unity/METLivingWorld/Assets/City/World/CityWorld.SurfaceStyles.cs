using System;
using UnityEngine;
namespace MET.City {
public sealed partial class CityWorld {
    // Evaluated once when terrain vertices are built. UV2 contains forest/dry weights;
    // actual slopes provide exposed-rock coverage in the shared material shader.
    Vector2 SurfaceBiomeWeights(Vector3 point,Vector3 normal) {
        float forest=ForestDensityAt(point);
        float dry=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,.75f,LandNoise(point,.82f)));
        float wet=0,total=0;
        foreach(var sample in landSamples) {
            float dx=point.x-sample.point.x,dz=point.z-sample.point.z;
            float weight=Mathf.Exp(-(dx*dx+dz*dz)*1.45f);
            total+=weight;if(sample.terrain=="wet"||sample.terrain=="water")wet+=weight;
        }
        float wetCover=total>0?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.68f,wet/total)):0;
        float shore=RiverHalfWidth(point);
        float bankFade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(shore+.12f,shore+.72f,RiverDistance(point)));
        dry*=(1-forest)*(1-wetCover)*bankFade;
        return new Vector2(forest,dry);
    }
    // One owned material per road style for the world's lifetime, no camera/frame work.
    // World coordinates remain continuous after ribbon subdivision/ground clipping.
    Material SettlementRoadSurface(string style) {
        string resource=style=="stone"?"CityRoadStone":style=="gravel"?"CityRoadGravel":"CityRoadDirt";
        string key="RoadSurface_"+resource;
        if(surfaceCache.TryGetValue(key,out var material))return material;
        var template=Resources.Load<Material>("Rendering/"+resource);
        if(template==null)throw new InvalidOperationException("Missing serialized city road surface: "+resource);
        material=new Material(template){name=resource,enableInstancing=true};
        ownedMaterials.Add(material);surfaceCache[key]=material;return material;
    }
}
}
