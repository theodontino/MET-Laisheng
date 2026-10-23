using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MET.City {
/// <summary>One short-lived render of the actual placed model and its city surroundings.</summary>
public sealed class CityBuildingPortrait : MonoBehaviour {
    Camera portraitCamera;
    RenderTexture portrait;
    Coroutine capture;
    string capturedId="";
    int capturedGeometry=-1;

    public Texture Capture(CityCamera source, CityBuildingIdentity building, int geometryVersion) {
        if(source==null||source.WorldCamera==null||building==null)return null;
        if(portrait!=null&&capturedId==building.BuildingId&&capturedGeometry==geometryVersion)return portrait;
        if(portrait==null){
            portrait=new RenderTexture(512,320,24,RenderTextureFormat.ARGB32) {name="City Building Context Portrait"};
            portrait.Create();
        }
        if(portraitCamera==null){
            var holder=new GameObject("City Building Portrait Camera");
            holder.transform.SetParent(transform,false);
            portraitCamera=holder.AddComponent<Camera>();
            portraitCamera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        }
        if(capture!=null)StopCoroutine(capture);
        portraitCamera.CopyFrom(source.WorldCamera);
        portraitCamera.targetTexture=portrait;
        portraitCamera.clearFlags=CameraClearFlags.SolidColor;
        portraitCamera.backgroundColor=new Color(.73f,.84f,.82f,1);
        portraitCamera.orthographic=true;
        portraitCamera.orthographicSize=1.22f;
        portraitCamera.nearClipPlane=.05f;
        portraitCamera.farClipPlane=120f;
        var focus=building.transform.position+Vector3.up*Mathf.Max(.42f,building.VisualHeight*.43f);
        portraitCamera.transform.position=focus+new Vector3(2.2f,1.9f,-2.6f);
        portraitCamera.transform.LookAt(focus);
        capturedId=building.BuildingId;
        capturedGeometry=geometryVersion;
        // URP renders an enabled camera into its target during the frame. Disable it
        // immediately afterward so this UI portrait has no per-frame world pass.
        portraitCamera.enabled=true;
        capture=StartCoroutine(DisableAfterFrame());
        return portrait;
    }

    IEnumerator DisableAfterFrame(){
        yield return new WaitForEndOfFrame();
        if(portraitCamera!=null)portraitCamera.enabled=false;
        capture=null;
    }

    void OnDestroy(){
        if(capture!=null)StopCoroutine(capture);
        if(portraitCamera!=null){portraitCamera.enabled=false;portraitCamera.targetTexture=null;Destroy(portraitCamera.gameObject);}
        if(portrait!=null){portrait.Release();Destroy(portrait);}
    }
}

public sealed partial class CityWorld {
    /// <summary>Reads only the committed 3D projection; no simulation or save access.</summary>
    public Texture PortraitForBuilding(string id){
        if(CameraController==null||!buildings.TryGetValue(id,out var building)||building==null)return null;
        var renderer=GetComponent<CityBuildingPortrait>()??gameObject.AddComponent<CityBuildingPortrait>();
        return renderer.Capture(CameraController,building,GeometryBuildCount);
    }
}

public sealed partial class CityInterface {
    void ContextSection(Transform parent,string kind,string title,Color color){
        var heading=Node("Slot Category "+kind,parent);
        Height(heading,19*TextScale);
        var layout=heading.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing=5;layout.childAlignment=TextAnchor.MiddleLeft;
        layout.childControlWidth=true;layout.childControlHeight=true;
        layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
        var symbol=kind=="Production"?"build":kind=="Logistics"?"stock":kind=="Operations"?"city":"bonus";
        HudIcon(heading,symbol,color,16);
        Label(heading,title,13,color,18*TextScale);
    }

    void ContextBuildingPortrait(Transform parent,string id,string definition){
        var frame=Surface("Actual City Building Portrait",parent,new Color(.04f,.105f,.12f,.98f));
        Width(frame,176);Height(frame,116);
        var world=GetComponent<CityWorld>();
        var texture=world!=null?world.PortraitForBuilding(id):null;
        if(texture!=null){
            var image=Node("Committed Model Camera Image",frame).gameObject.AddComponent<RawImage>();
            image.texture=texture;image.color=Color.white;image.raycastTarget=false;
            Stretch(image.rectTransform,2,2,2,2);
        }else{
            // Honest fallback until the world has built the selected model.
            var drawing=Node("Model Preview Unavailable",frame).gameObject.AddComponent<CityBlueprintArt>();
            drawing.Style=CityIllustrationCatalog.StyleForBuilding(definition);
            drawing.Accent=Blue;drawing.Fill=.5f;drawing.color=Blue;drawing.raycastTarget=false;
            Stretch(drawing.rectTransform,4,4,4,24);
            var caption=Label(frame,"模型预览暂不可用",11,Muted,18);
            Pin(caption.rectTransform,new Vector2(0,0),new Vector2(1,0),new Vector2(6,2),new Vector2(-12,18));
        }
        var edge=Node("Portrait border",frame);Stretch(edge);
        var border=edge.gameObject.AddComponent<CityHudFrame>();border.color=new Color(.39f,.66f,.66f,.8f);border.raycastTarget=false;
    }

    void ContextDatum(Transform parent,string icon,string amount){
        var datum=Node("Actual value "+icon,parent);
        Height(datum,29*TextScale);
        var layout=datum.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing=4;layout.childAlignment=TextAnchor.MiddleLeft;
        layout.childControlWidth=true;layout.childControlHeight=true;
        layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
        HudIcon(datum,icon,icon=="currency"||icon=="material"?Gold:Blue,19);
        var value=Label(datum,amount,13,Ink,27*TextScale);
        value.alignment=TextAnchor.MiddleLeft;
    }
}
}
