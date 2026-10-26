using UnityEngine;

namespace MET.City {
/// <summary>Connects the native simulation host to detached UI and world projections.</summary>
[RequireComponent(typeof(CityRuntime), typeof(CityInterface), typeof(CityWorld))]
public sealed class CityBootstrap : MonoBehaviour {
    public CityRuntime Runtime { get; private set; }
    public CityInterface Interface { get; private set; }
    public CityWorld World { get; private set; }

    void Start() {
        Runtime = GetComponent<CityRuntime>();
        Interface = GetComponent<CityInterface>();
        World = GetComponent<CityWorld>();
        Interface.MapProjectionChanged += Present;
        Interface.ComparisonChanged += World.SetComparison;
        Interface.SelectionChanged += Select;
        Interface.LayerChanged += World.SetLayer;
        World.LayerChanged += Interface.SetLayer;
        World.SetFabricSources(Runtime.Game.c.projects, Runtime.Game.compiled.Get("stats", MET.Core.V.Map()));
        World.Initialize(() => Interface.MapSnapshot, Interface.SelectTile,
            () => Interface.KeyboardOwned, Interface.PointerOwned,
            () => Interface.MapInteractionAvailable);
        Interface.AttachMapInspection(World);
        World.TryInspectClick = Interface.TryLockMapInspection;
        World.SetSourcePhase(Interface.MapSourcePhase);
        World.SetLayer(Interface.Layer);
        Interface.DisplayChanged += Display;
        Display(Runtime.DisplaySettings);
        if (World.Failure != "") Interface.ShowMessage("地图资源导入失败：" + World.Failure);
        if (CityMemoryEvidence.Requested() || CityEvidence.Requested() || CityWildG0Evidence.Requested()) Application.runInBackground = true;
        if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--manual-ui-evidence")>=0)gameObject.AddComponent<CityMouseEvidence>();
        if (CityWildG0Evidence.Requested()) gameObject.AddComponent<CityWildG0Evidence>();
        else if (CityMemoryEvidence.Requested()) gameObject.AddComponent<CityMemoryEvidence>();
        else if (CityEvidence.Requested()) gameObject.AddComponent<CityEvidence>();
    }

    void Present(MET.Core.V snapshot) {
        World.SetFabricSources(Runtime.Game.c.projects, Runtime.Game.compiled.Get("stats", MET.Core.V.Map()));
        World.Present(snapshot);
        World.SetSourcePhase(Interface.MapSourcePhase);
    }
    void Select(string tile, string building) { World.SelectTile(tile, true); }
    void Display(MET.Core.V settings) { World.SetAllLinks(settings.Get("all_links", false).Bool); }
    void OnDestroy() {
        if (Interface == null || World == null) return;
        Interface.MapProjectionChanged -= Present;
        Interface.ComparisonChanged -= World.SetComparison;
        Interface.SelectionChanged -= Select;
        Interface.LayerChanged -= World.SetLayer;
        World.LayerChanged -= Interface.SetLayer;
        World.TryInspectClick = null;
        Interface.DismissMapInspection();
        Interface.DisplayChanged -= Display;
    }
}
}
