using UnityEngine;
using UnityEngine.UI;
namespace MET.City {
public sealed partial class CityInterface {
 RectTransform _hoverProgress;CityGraphic _hoverProgressFill;
 void UpdateHoverProgress(){
  bool desk=DeskTipPending;float progress=0;Vector2 position=Vector2.zero;
  if(desk){progress=(float)(1-(_deskDwell.ReadyAt-DeskNow)/_deskDwell.Delay);var bounds=DeskTipBounds(_pendingDeskTip.transform);position=new Vector2((float)(bounds.X+bounds.Width*.5),(float)(bounds.Y-7));}
  if(!desk){if(_hoverProgress!=null)_hoverProgress.gameObject.SetActive(false);return;}
  if(_hoverProgress==null){_hoverProgress=Surface("Hover wait progress",_root,new Color(.05f,.11f,.13f,.95f));_hoverProgress.GetComponent<Image>().raycastTarget=false;var fill=Node("Elapsed hover",_hoverProgress);Stretch(fill);_hoverProgressFill=fill.gameObject.AddComponent<CityGraphic>();_hoverProgressFill.Kind="bar";_hoverProgressFill.color=Green;_hoverProgressFill.raycastTarget=false;}
  var rect=_root.rect;position.x=Mathf.Clamp(position.x,rect.xMin+34,rect.xMax-34);position.y=Mathf.Clamp(position.y,rect.yMin+8,rect.yMax-8);
  Pin(_hoverProgress,new Vector2(.5f,.5f),new Vector2(.5f,.5f),position,new Vector2(60,4));_hoverProgressFill.Ratio=Mathf.Clamp01(progress);_hoverProgressFill.SetVerticesDirty();_hoverProgress.gameObject.SetActive(true);_hoverProgress.SetAsLastSibling();
 }
}
}
