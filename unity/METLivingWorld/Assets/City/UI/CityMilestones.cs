using System;
using System.Linq;
using MET.Core;
namespace MET.City {
public sealed partial class CityInterface {
 void MilestonesPage(){
  var theme=Themes.view(G);var themeId=theme.Get("id","").Text;
  var saved=S["block"].Get("theme",V.Map()).Get("milestone_ids",V.Arr());
  var history=theme.Get("history",V.Arr());var definition=themeId==""?V.Map():Themes._definition(G,themeId);
  var stages=definition.Get("stages",V.Arr());
  var summary=CompactTaskCard(_body,"Milestones Summary");
  Label(summary,themeId==""?"城市成长":theme.Get("name","城市成长").Text,20,Green,31*TextScale);
  var ordinals=new[]{"第一","第二","第三"};
  for(int n=1;n<=3;n++){
   var id=themeId+":M"+n;var met=themeId!=""&&saved.Contains(id);
   var record=history.FirstOrDefault(x=>x.Get("kind","").Text=="milestone"&&x.Get("id","").Text==id);
   var stage=stages.Count>=n?stages[n-1]:V.Map();
   var name=record!=null?record.Get("name",stage.Get("name","城市阶段")).Text:stage.Get("name",themeId==""?"待选择方向":"城市阶段").Text;
   var row=CompactTaskCard(summary,"Milestone Stage "+n);
   Label(row,(met?"✓ ":"○ ")+ordinals[n-1]+"阶段 · "+name+" · "+(met?"已存档":themeId==""?"未开始":"尚未存档"),17,met?Green:Muted,43*TextScale);
  }
  var action=Button(summary,themeId==""?"选择发展主题":theme.Get("completed",false).Bool?"查看主题成果":"查看当前任务",()=>Navigate("themes"),true);action.name="Milestones Theme Action";
  var details=Column(_body,"Milestones Detail Folds",4);
  Fold(details,"阶段状态说明",deep=>Label(deep,themeId==""?"尚未选定主题，三个阶段等待开始。":theme.Get("completed",false).Bool?"主题已完成；勾选只依据保存的阶段记录。":"当前目标读数和阶段存档分别展示，未存档不等于尚未满足。",16,Muted));
  Fold(details,"当前阶段目标",deep=>{
   if(themeId==""){Label(deep,"先选择发展主题，随后查看真实目标。",16,Muted);return;}
   if(theme.Get("completed",false).Bool){Label(deep,"主题已完成；阶段达成以保存记录为准。",16,Green);return;}
   foreach(var goal in theme.Get("goals",V.Arr())){var factor=goal.Get("unit","").Text=="%"?100:1;
    Label(deep,(goal.Get("met",false).Bool?"✓ ":"○ ")+goal.Get("label","目标").Text+"  "+F(goal.Get("value",0).Double*factor,1)+(goal.Get("comparison","min").Text=="max"?" ≤ ":" / ")+F(goal.Get("target",0).Double*factor,1)+goal.Get("unit","").Text,16,goal.Get("met",false).Bool?Green:Gold);
   }
   if(theme.Get("steady_required_seconds",0).Double>0)Label(deep,"稳定时长 "+Clock.duration(G,theme.Get("steady_seconds",0).Double)+" / "+Clock.duration(G,theme.Get("steady_required_seconds",0).Double),16,Muted);
  });
  Fold(details,"阶段依据与主题定义",deep=>{for(int n=1;n<=3;n++){
   var id=themeId+":M"+n;var record=history.FirstOrDefault(x=>x.Get("kind","").Text=="milestone"&&x.Get("id","").Text==id);
   if(record!=null){Label(deep,record.Get("name","阶段记录").Text,17,Green);Raw(deep,record);}
  }Raw(deep,S["block"].Get("theme",V.Map()).Get("milestone_ids",V.Arr()));Raw(deep,history);if(definition.Count>0)Raw(deep,definition);});
  Fold(details,"城市完整记录",deep=>{Button(deep,"城市声音与历史",()=>Navigate("records"));Button(deep,"地区与城市遗产",()=>Navigate("heritage"));});
 }
}
}
