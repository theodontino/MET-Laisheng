using System;
using System.Linq;
using MET.Core;
namespace MET.City {
public sealed partial class CityInterface {
 void MilestonesPage(){_pageContext="growth";ThemesPage();}
 void ThemeMilestoneRoute(UnityEngine.Transform parent,V theme){
  var themeId=theme.Get("id","").Text;var saved=S["block"].Get("theme",V.Map()).Get("milestone_ids",V.Arr());var history=theme.Get("history",V.Arr());var definition=themeId==""?V.Map():Themes._definition(G,themeId);var stages=definition.Get("stages",V.Arr());
  Label(parent,themeId==""?"成长路线待开始":theme.Get("name","城市成长").Text+" · 成长路线",21,Green);
  for(int n=1;n<=3;n++){
   var id=themeId+":M"+n;var met=themeId!=""&&saved.Contains(id);var record=history.FirstOrDefault(x=>x.Get("kind","").Text=="milestone"&&x.Get("id","").Text==id);var stage=stages.Count>=n?stages[n-1]:V.Map();
   var name=record!=null?record.Get("name",stage.Get("name","城市阶段")).Text:stage.Get("name",themeId==""?"待选择方向":"城市阶段").Text;
   var card=Card(parent,(met?"✓ ":"○ ")+"M"+n+" · "+name,met?"已保存达成记录":themeId==""?"先选择主题":"尚未存档",met?Green:Blue);
   if(stage.Get("description","").Text!="")Label(card,stage["description"].Text,16,Muted);
   Fold(card,"阶段要求与达成依据",deep=>{if(stage.Count>0)Raw(deep,stage);if(record!=null)Raw(deep,record);});
  }
  if(themeId=="")Button(parent,"选择发展主题",()=>Navigate("themes","current"),true);
 }

}
}
