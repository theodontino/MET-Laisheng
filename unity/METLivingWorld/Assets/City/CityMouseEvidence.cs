using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UI;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;
namespace MET.City {
/// <summary>Opt-in observer for real mouse/keyboard sessions. F12 only captures; it never drives UI or gameplay.</summary>
public sealed class CityMouseEvidence : MonoBehaviour {
 CityRuntime runtime;string directory;int sequence,hoverSequence;bool hoverActive,hoverMiddle;
 void Start(){Application.runInBackground=true;runtime=GetComponent<CityRuntime>();directory=Environment.GetEnvironmentVariable("MET_UNITY_EVIDENCE");if(string.IsNullOrEmpty(directory))directory=runtime.StorageRoot;Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"manual-start-city.json"),runtime.Session.snapshot().ToJson(true));}
 void ObserveHover(){
  var ui=FindFirstObjectByType<CityInterface>();var progress=ui.GetComponentsInChildren<CityGraphic>(false).FirstOrDefault(g=>g.name=="Elapsed hover");
  bool active=progress!=null&&progress.gameObject.activeInHierarchy;
  if(active&&!hoverActive){hoverSequence++;hoverMiddle=false;}
  if(hoverSequence<=12&&active&&!hoverMiddle&&progress.Ratio>.35f&&progress.Ratio<.8f){hoverMiddle=true;ScreenCapture.CaptureScreenshot(Path.Combine(directory,"hover-"+hoverSequence+"-progress.png"));}
  if(hoverSequence<=12&&!active&&hoverActive){ScreenCapture.CaptureScreenshot(Path.Combine(directory,"hover-"+hoverSequence+"-ended.png"));}
  if(active!=hoverActive)File.AppendAllText(Path.Combine(directory,"hover-observed.jsonl"),V.Map("sequence",hoverSequence,"active",active,"time",Time.realtimeSinceStartupAsDouble,"pointer",V.Arr(Input.mousePosition.x,Input.mousePosition.y)).ToJson()+"\n");
  hoverActive=active;
 }
 void Update(){ObserveHover();if(!Input.GetKeyDown(KeyCode.F12))return;sequence++;string name="manual-"+sequence.ToString("D2");ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));var focused=EventSystem.current?.currentSelectedGameObject;var hits=new List<RaycastResult>();var ray=FindFirstObjectByType<GraphicRaycaster>();if(ray!=null)ray.Raycast(new PointerEventData(EventSystem.current){position=Input.mousePosition},hits);var hitNames=V.Arr();foreach(var hit in hits)hitNames.Add(hit.gameObject.name);var data=V.Map("input",V.Map("mouse",V.Arr(Input.mousePosition.x,Input.mousePosition.y),"screen",V.Arr(Screen.width,Screen.height),"display_system",V.Arr(Display.main.systemWidth,Display.main.systemHeight),"display_rendering",V.Arr(Display.main.renderingWidth,Display.main.renderingHeight),"current_resolution",Screen.currentResolution.ToString(),"raycast_hits",hitNames,"focused",Application.isFocused),"sequence",sequence,"scope","Actual desktop mouse/keyboard; screenshot observer only; no callback automation.","application_version",Application.version,"width",Screen.width,"height",Screen.height,"focus",focused==null?"":focused.name,"scrolls",V.From(FindFirstObjectByType<CityInterface>().GetComponentsInChildren<ScrollRect>().Select(r=>V.Map("name",r.name,"position",r.verticalNormalizedPosition,"sensitivity",r.scrollSensitivity)).ToArray()),"camera_size",Camera.main.orthographicSize,"city",runtime.Session.snapshot());File.WriteAllText(Path.Combine(directory,name+".json"),data.ToJson(true));}
}
}
