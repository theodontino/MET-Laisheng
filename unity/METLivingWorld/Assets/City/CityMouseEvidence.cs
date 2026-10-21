using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UI;
using MET.Core;
using UnityEngine;
using UnityEngine.EventSystems;
namespace MET.City {
/// <summary>Opt-in observer for real mouse/keyboard sessions. F12 only captures; it never drives UI or gameplay.</summary>
public sealed class CityMouseEvidence : MonoBehaviour {
 CityRuntime runtime;string directory;int sequence;
 void Start(){Application.runInBackground=true;runtime=GetComponent<CityRuntime>();directory=Environment.GetEnvironmentVariable("MET_UNITY_EVIDENCE");if(string.IsNullOrEmpty(directory))directory=runtime.StorageRoot;Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"manual-start-city.json"),runtime.Session.snapshot().ToJson(true));}
 void Update(){if(!Input.GetKeyDown(KeyCode.F12))return;sequence++;string name="manual-"+sequence.ToString("D2");ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));var focused=EventSystem.current?.currentSelectedGameObject;var hits=new List<RaycastResult>();var ray=FindFirstObjectByType<GraphicRaycaster>();if(ray!=null)ray.Raycast(new PointerEventData(EventSystem.current){position=Input.mousePosition},hits);var hitNames=V.Arr();foreach(var hit in hits)hitNames.Add(hit.gameObject.name);var data=V.Map("input",V.Map("mouse",V.Arr(Input.mousePosition.x,Input.mousePosition.y),"screen",V.Arr(Screen.width,Screen.height),"display_system",V.Arr(Display.main.systemWidth,Display.main.systemHeight),"display_rendering",V.Arr(Display.main.renderingWidth,Display.main.renderingHeight),"current_resolution",Screen.currentResolution.ToString(),"raycast_hits",hitNames,"focused",Application.isFocused),"sequence",sequence,"scope","Actual desktop mouse/keyboard; screenshot observer only; no callback automation.","application_version",Application.version,"width",Screen.width,"height",Screen.height,"focus",focused==null?"":focused.name,"city",runtime.Session.snapshot());File.WriteAllText(Path.Combine(directory,name+".json"),data.ToJson(true));}
}
}
