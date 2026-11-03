using System.Linq;
using MET.Core;
using UnityEngine;
namespace MET.City {
public sealed partial class CityWorld {
    // Only the isolated UI6 evidence runner may reproduce the pre-fix geometry.
    static readonly bool ParcelRoadBaselineEvidence=System.Environment.GetCommandLineArgs().Contains("--validate-ui6")&&System.Environment.GetCommandLineArgs().Contains("--roads-baseline");
    // Evidence-only visibility changes. The same scene, camera and simulation
    // snapshot can be captured with either road component hidden independently.
    public V SetParcelRoadEvidenceVisibility(bool parcelLanesVisible,bool entranceStepsVisible) {
        int lanes=0,steps=0;
        foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true)) {
            if(renderer.name=="MergedParcelEntranceAndSideLanes"){renderer.enabled=parcelLanesVisible;lanes++;}
            else if(renderer.name=="TerrainToDoorEntranceSteps"){renderer.enabled=entranceStepsVisible;steps++;}
        }
        return V.Map("parcel_lanes_visible",parcelLanesVisible,"parcel_lane_meshes",lanes,
            "entrance_steps_visible",entranceStepsVisible,"entrance_step_meshes",steps);
    }
    public V ParcelEntranceGeometryEvidence=>V.Map("geometry",ParcelRoadBaselineEvidence?"pre_fix_ui6_baseline":"inward_entry_segment_connection","sites",V.From(settlementSites.OrderBy(pair=>pair.Key).Select(pair=>{
        var site=pair.Value;return V.Map("tile",pair.Key,"construction",site.construction,
            "footprint",V.Arr(site.footprint.xMin,site.footprint.yMin,site.footprint.xMax,site.footprint.yMax),
            "ring_margin",site.ringMargin,"entrance",V.Arr(site.entrance.x,site.entrance.y),
            "entrance_beyond_ring_centerline",site.entrance.y-site.footprint.yMax-site.ringMargin,
            "entrance_connection",site.entranceConnection,"floor",site.floor,"ground_at_entrance",VisualGroundHeight(site.center+SettlementOffset(site.entrance.x,site.entrance.y)));
    }).ToArray()));
}
}
