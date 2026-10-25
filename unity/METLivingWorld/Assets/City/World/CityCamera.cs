using System;
using UnityEngine;
namespace MET.City {
/// <summary>Independent orthographic map scale. UI scale never changes this camera.</summary>
[RequireComponent(typeof(Camera))]
public sealed class CityCamera : MonoBehaviour {
    public Camera WorldCamera {get;private set;}
    public float ZoomSpan=>targetSpan;
    public int Revision {get;private set;}
    Func<bool> keyboardOwned;
    Func<Vector2,bool> pointerOwned;
    Vector3 target,current,lastMouse;
    float targetSpan=10.5f;
    bool dragging;
    readonly Vector3 direction=new Vector3(-9,15.3f,12).normalized;
    public void Initialize(Func<bool> keyboard=null,Func<Vector2,bool> pointer=null) {
        keyboardOwned=keyboard;pointerOwned=pointer;WorldCamera=GetComponent<Camera>();
        WorldCamera.orthographic=true;WorldCamera.nearClipPlane=.05f;WorldCamera.farClipPlane=150;
        WorldCamera.orthographicSize=targetSpan*.5f;Apply();
    }
    public void ResetView() {target=current=Vector3.zero;targetSpan=17.5f;WorldCamera.orthographicSize=targetSpan*.5f;Apply();Revision++;}
    public void ShowSettlement(Vector3 center) {target=current=new Vector3(center.x,0,center.z);targetSpan=10.5f;WorldCamera.orthographicSize=targetSpan*.5f;Apply();Revision++;}
    public void Focus(Vector3 center) {target=new Vector3(center.x,0,center.z);Revision++;}
    public void Pan(Vector2 pixels) {
        var right=Vector3.ProjectOnPlane(transform.right,Vector3.up).normalized;
        var up=Vector3.ProjectOnPlane(transform.up,Vector3.up).normalized;
        target+=(-right*pixels.x-up*pixels.y)*(targetSpan/Mathf.Max(1,Screen.height));
        target.x=Mathf.Clamp(target.x,-12,12);target.z=Mathf.Clamp(target.z,-12,12);Revision++;
    }
    public void Zoom(float multiplier) {targetSpan=Mathf.Clamp(targetSpan*multiplier,6,24);Revision++;}
    void Update() {
        if(WorldCamera==null)return;
        bool blocked=keyboardOwned!=null&&keyboardOwned();var pointer=(Vector2)Input.mousePosition;bool overUI=pointerOwned!=null&&pointerOwned(pointer);
        if(!overUI) {
            if(!blocked&&Input.GetKeyDown(KeyCode.Home))ResetView();
            if(Input.GetMouseButtonDown(1)||Input.GetMouseButtonDown(2)) {dragging=true;lastMouse=Input.mousePosition;}
            if(dragging&&(Input.GetMouseButton(1)||Input.GetMouseButton(2))) {var mouse=Input.mousePosition;Pan(mouse-lastMouse);lastMouse=mouse;}
            var scroll=Input.mouseScrollDelta.y;if(Mathf.Abs(scroll)>.01f)Zoom(Mathf.Pow(.9f,scroll));
        } else dragging=false;
        if(!Input.GetMouseButton(1)&&!Input.GetMouseButton(2))dragging=false;
        if((current-target).sqrMagnitude>.0000001f||Mathf.Abs(WorldCamera.orthographicSize-targetSpan*.5f)>.00001f) {
            float blend=1-Mathf.Exp(-12*Time.unscaledDeltaTime);
            current=Vector3.Lerp(current,target,blend);WorldCamera.orthographicSize=Mathf.Lerp(WorldCamera.orthographicSize,targetSpan*.5f,blend);Apply();
        }

    }
    void Apply() {transform.position=current+direction*27;transform.rotation=Quaternion.LookRotation(-direction,Vector3.up);}
}
}
