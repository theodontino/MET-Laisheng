using System.Linq;
using MET.Core;
namespace MET.City {
public sealed partial class CityInterface {
 void MilestonesPage(){_pageContext="growth";ThemesPage();}
 void ThemeMilestoneRoute(UnityEngine.Transform parent,V theme){
  var columns=WorkspaceColumns("Three stage growth route",1,1,1);
  for(int index=0;index<3;index++){
   var stage=theme["stages"][index];var saved=stage["saved"].Bool;var current=stage["current"].Bool;
   var card=Card(columns[index],"M"+(index+1)+" · "+stage["name"].Text,saved?"✓ 已保存达成记录":current?"当前阶段":"未来阶段",saved?Green:current?Gold:Blue);
   Label(card,"奖励 · "+ThemeStageReward(stage),16,Green);
   Label(card,stage["hold_months"].Double>0?"全部状态同时稳定 "+F(stage["hold_months"].Double)+" 来生月":"无额外稳定时长",16,Muted);
   if(current&&stage["hold_months"].Double>0)Metric(card,"已连续稳定",F(stage["held_months"].Double,2)+" 来生月");
   foreach(var goal in stage["goals"]){var factor=goal["unit"].Text=="%"?100:1;var unit=goal["unit"].Text;
    Label(card,goal["label"].Text,17,Ink);Label(card,"要求 "+(goal["comparison"].Text=="max"?"≤ ":"≥ ")+F(goal["target"].Double*factor,1)+unit,16,Gold);
    if(!saved)Label(card,(current?"当前 ":goal["kind"].Text=="state"?"城市参考 ":"阶段开始后累计 · 当前预览 ")+F(goal["value"].Double*factor,1)+unit,15,Muted);
    Label(card,ThemeGoalHint(goal["id"].Text),15,Muted);
   }
   if(saved){var record=theme["history"].First(x=>x.Get("kind","").Text=="milestone"&&x.Get("id","").Text==stage["id"].Text);Fold(card,"已保存的阶段凭据",c=>Raw(c,record));}
   if(current)Button(card,"办理当前目标与交付",()=>Navigate("tasks_home","current"),true);
   if(index==2)Label(card,"DT 实际归零时，已完成主题可取得对应定型资格；在结算查看命名与地标。",15,Muted);
  }
 }
}
}
