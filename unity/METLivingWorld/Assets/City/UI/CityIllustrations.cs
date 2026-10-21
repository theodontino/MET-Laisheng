using UnityEngine;
using UnityEngine.UI;
using MET.Core;
namespace MET.City {
public sealed partial class CityInterface {
 public GameObject ShowIllustrationStudy(){
  var sheet=Surface("Illustration Study Evidence",_root,Color.white);Pin(sheet,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1720,1000));var stack=Column(sheet,"Twenty Original Purpose Studies",7);Stretch(stack,12,8,12,8);Label(stack,"城市用途插图 / 20种构图 · 分类复用",24,PaperInk,38);
  for(int row=0;row<4;row++){var line=Row(stack,"Illustration Study Row",217);for(int column=0;column<5;column++){int style=row*5+column;var cell=Column(line,"Study "+style,2);Label(cell,CityIllustrationCatalog.NameForStyle(style),16,PaperInk,25);Illustration(cell,style,162);}}
  sheet.SetAsLastSibling();return sheet.gameObject;
 }
 void Illustration(Transform parent,int style,float height=100,float fill=1){
  var node=Node("Purpose Illustration "+style,parent);Height(node,height);var art=node.gameObject.AddComponent<CityBlueprintArt>();art.ShowDrafting=false;art.Style=style;art.Fill=fill;art.Accent=CityIllustrationCatalog.AccentForStyle(style);art.color=parent.GetComponentInParent<CityDeskTone>()?.Light==true?new Color(.48f,.52f,.47f):Color.white;art.raycastTarget=false;
 }
 int ThemePicture(string id)=>id.Contains("industrial")?0:id.Contains("agri")?3:id.Contains("civic")||id.Contains("liv")?4:id.Contains("commercial")?6:id.Contains("logistics")?8:id.Contains("research")?11:2;
 int OptionPicture(string eventId,string choiceId,V definition){
  if(choiceId=="decline")return 9;
  var rewards=definition.Get("bonus_ids",V.Arr());if(rewards.Count>0)return CityIllustrationCatalog.StyleForBonus(rewards[0].Text);
  var hint=(eventId+" "+choiceId).ToLowerInvariant();
  if(hint.Contains("school")||hint.Contains("education"))return 5;
  if(hint.Contains("medical")||hint.Contains("care"))return 4;
  if(hint.Contains("equipment")||hint.Contains("industry"))return 12;
  if(hint.Contains("order")||hint.Contains("freight")||hint.Contains("trade"))return 8;
  if(hint.Contains("housing")||hint.Contains("resident"))return 17;
  if(hint.Contains("energy")||hint.Contains("power"))return 7;
  return ThemePicture(eventId);
 }
 void ConstructionIllustration(Button button,string baseId){
  var label=button.GetComponentInChildren<Text>();Stretch(label.rectTransform,63,4,8,4);label.alignment=TextAnchor.MiddleLeft;
  var node=Node("Foundation Illustration "+baseId,button.transform);Pin(node,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(7,0),new Vector2(53,42));
  var art=node.gameObject.AddComponent<CityBlueprintArt>();art.ShowDrafting=false;art.Style=CityIllustrationCatalog.StyleForBuilding(baseId);art.Accent=CityIllustrationCatalog.AccentForStyle(art.Style);art.Fill=1;art.color=new Color(.45f,.49f,.43f);art.raycastTarget=false;
 }
}
}
