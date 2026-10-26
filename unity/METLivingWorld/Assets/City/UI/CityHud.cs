using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
public sealed partial class CityInterface {
    RectTransform _planningTop, _planningObjective, _planningUrgent, _planningNoticePanel,
        _planningLayers, _planningBuildMenu, _planningTime, _planningLegend, _planningToastSurface;
    RectTransform _planningOverview, _planningOverviewBody, _planningRail;
    Text _guidanceTitle,_guidanceReason,_buildHint,_goalSummaryText;CityGraphic _goalProgress;RectTransform _goalSummary;Button _guidanceButton;
    readonly Dictionary<string,Button> _foundationButtons=new Dictionary<string,Button>();
    readonly Dictionary<string,Button> _topRoutes=new Dictionary<string,Button>();
    readonly List<Text> _planningStockValues = new List<Text>();
    Text _planningObjectiveText, _planningClock, _planningTaskCount, _planningLicenseCount,
        _planningBonusCount, _planningPendingCount, _planningCopyCount, _planningBuildToggle;
    Button _planningNoticeButton;
    readonly Dictionary<string,Button> _hudLayerButtons=new Dictionary<string,Button>();
    bool _planningNoticesOpen, _planningOverviewOpen, _planningBuildOpen;
    readonly List<Button> _hudSpeedButtons=new List<Button>();
    Text _hudBonusBadge;
    readonly System.Collections.Generic.List<Text> _navGroupLabels=new System.Collections.Generic.List<Text>();
    readonly Color _hudFill=new Color(.078f,.141f,.153f,.98f);
    readonly Color _hudButtonFill=new Color(.125f,.219f,.208f,.94f);
    readonly Color _hudEdge=new Color(.48f,.66f,.57f,.58f);

    // The shell owns creation order; all values and actions still read the detached game view.
    void BuildPlanningHud() {
        BuildPlanningResources();
        BuildCapacityHud();
        BuildPlanningObjective();
        BuildPlanningReminders();
        BuildPlanningContext();
        BuildPlanningMapFilters();
        BuildPlanningFooter();
        RefreshPlanningHud();
    }

    RectTransform HudSurface(string name,Transform parent,Color? fill=null) {
        var surface=Node(name,parent);
        surface.gameObject.AddComponent<Image>().color=fill??_hudFill;
        var line=Node("HUD hairline",surface);Stretch(line);line.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
        var edge=line.gameObject.AddComponent<CityHudFrame>();edge.color=new Color(.36f,.46f,.40f,.44f);edge.raycastTarget=false;
        return surface;
    }
    void HudFont(Text text,int size){_fontBases[text]=size;text.fontSize=Mathf.RoundToInt(size*TextScale);}
    Button HudButton(Transform parent,string caption,string icon,Action click,float height=42,bool active=false) {
        var surface=HudSurface("HUD "+caption,parent,active?new Color(.045f,.21f,.22f,.82f):_hudButtonFill);
        Height(surface,height);
        var frame=surface.GetComponentInChildren<CityHudFrame>();if(frame!=null)frame.enabled=active;
        var button=surface.gameObject.AddComponent<Button>();
        var colors=button.colors;colors.normalColor=Color.white;
        colors.highlightedColor=new Color(1.17f,1.18f,1.18f);
        colors.pressedColor=new Color(.7f,.83f,.83f);button.colors=colors;
        if(icon!=""){
            var mark=HudIcon(surface,icon,active?new Color(.49f,1f,.88f):Blue,23);
            Pin(mark,new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(25,0),new Vector2(23,23));
        }
        var label=Label(surface,caption,16,active?Ink:Muted);
        Stretch(label.rectTransform,icon==""?9:48,2,8,2);
        label.alignment=icon==""?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft;
        button.onClick.AddListener(()=>{PlayCue("select");click();});
        return button;
    }
    void BuildPlanningResources() {
        _planningTop=HudSurface("HUD Resources",_root,new Color(.067f,.125f,.137f,.98f));
        _planningTop.anchorMin=new Vector2(0,1);_planningTop.anchorMax=Vector2.one;
        _planningTop.pivot=new Vector2(.5f,1);_planningTop.offsetMin=new Vector2(0,-64);_planningTop.offsetMax=Vector2.zero;
        var brand=Label(_planningTop,"MET",24,Ink);Pin(brand.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(18,0),new Vector2(65,40));
        brand.raycastTarget=true;var menu=brand.gameObject.AddComponent<Button>();menu.onClick.AddListener(()=>OpenArchive("settings"));
        AddDeskTip(brand.gameObject,"MET · 系统菜单",()=>TopbarResourceSummary("met"));
        brand.name="HUD Brand";brand.resizeTextForBestFit=true;brand.resizeTextMinSize=18;brand.resizeTextMaxSize=24;brand.verticalOverflow=VerticalWrapMode.Truncate;brand.alignment=TextAnchor.MiddleLeft;
        string[] names={"DT","人口","粮食","材料","制成品","通货"};string[] kinds={"dt","population","food","material","goods","currency"};
        for(int i=0;i<names.Length;i++){
            var name=names[i];var cell=HudSurface("HUD Resource "+name,_planningTop,new Color(0,0,0,0));
            cell.anchorMin=new Vector2(0,0);cell.anchorMax=new Vector2(0,1);cell.pivot=new Vector2(0,.5f);
            cell.GetComponentInChildren<CityHudFrame>().DividerOnly=true;
            var glyph=HudIcon(cell,kinds[i],i==2?Green:i==3||i==5?Gold:Blue,28);
            Pin(glyph,new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(22,0),new Vector2(28,28));
            var caption=Label(cell,name=="电力"?"供 / 需":name,13,Muted);
            Pin(caption.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(46,-4),new Vector2(100,20));
            var value=Label(cell,"—",21,Ink);
            value.name="HUD Value "+name;value.resizeTextForBestFit=true;
            value.resizeTextMinSize=12;value.resizeTextMaxSize=21;
            value.verticalOverflow=VerticalWrapMode.Truncate;
            Pin(value.rectTransform,new Vector2(0,0),new Vector2(0,0),new Vector2(46,22),new Vector2(100,26));value.alignment=TextAnchor.MiddleLeft;
            if(i==0)_dt=value;else if(i==1)_population=value;else if(i==2)_food=value;else _planningStockValues.Add(value);
            var resource=kinds[i];var resourceButton=cell.gameObject.AddComponent<Button>();resourceButton.onClick.AddListener(()=>OpenResourcePage(resource));
            var detail=Label(cell,"",12,Muted);detail.name="HUD Detail "+resource;Pin(detail.rectTransform,new Vector2(0,0),new Vector2(0,0),new Vector2(46,2),new Vector2(100,20));_resourceDetails[resource]=detail;
            AddDeskTip(cell.gameObject,name,()=>TopbarResourceSummary(resource));
        }
        _time=Label(_planningTop,"",14,Muted,24);_time.gameObject.SetActive(false);
    }
    Text PlanningResource(Transform parent,string name) {
        // Kept for integrations that used the resource factory before the compact top bar.
        var cell=HudSurface("HUD Resource "+name,parent);var value=Label(cell,"—",19,Ink);
        Stretch(value.rectTransform,8,4,8,4);AddDeskTip(cell.gameObject,name,()=>PlanningResourceExplanation(name));
        return value;
    }
    void BuildPlanningObjective() {
        _planningOverview=HudSurface("HUD Overview",_root);
        Pin(_planningOverview,new Vector2(1,1),new Vector2(1,1),new Vector2(-18,-99),new Vector2(350,286));
        var header=HudButton(_planningOverview,"下一步  ﹀","",()=>{
            _planningOverviewOpen=!_planningOverviewOpen;_planningOverviewBody.gameObject.SetActive(_planningOverviewOpen);RefreshHudGeometry();
        },42);
        header.GetComponentInChildren<Text>().alignment=TextAnchor.MiddleLeft;
        Pin((RectTransform)header.transform,new Vector2(.5f,1),new Vector2(.5f,1),Vector2.zero,new Vector2(348,42));
        _goalSummary=Column(_planningOverview,"Goal summary",5,false);_goalSummary.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(16,16,6,6);
        _goalSummaryText=Label(_goalSummary,"",14,Ink,48*TextScale);var progress=Node("Goal measured progress",_goalSummary);Height(progress,6);_goalProgress=progress.gameObject.AddComponent<CityGraphic>();_goalProgress.Kind="bar";_goalProgress.color=Green;_goalProgress.raycastTarget=false;
        _planningOverviewBody=Column(_planningOverview,"Overview Groups",10);
        var layout=_planningOverviewBody.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(18,18,10,15);
        Pin(_planningOverviewBody,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-120*TextScale),new Vector2(348,0));
        _planningObjective=_planningOverviewBody;
        _guidanceTitle=Label(_planningOverviewBody,"",18,Ink,42*TextScale);
        _guidanceReason=Label(_planningOverviewBody,"",13,Muted,52*TextScale);
        _guidanceButton=Button(_planningOverviewBody,"查看城市",RunCityGuidanceAction,true);_guidanceButton.name="HUD Next Action";
        _planningOverviewBody.gameObject.SetActive(_planningOverviewOpen);
        AttachCityGuidancePointerGuard();
        var more=HudButton(_planningOverviewBody,"查看当前任务 ›","",()=>OpenArchive("tasks_home"),30*TextScale);more.GetComponentInChildren<Text>().alignment=TextAnchor.MiddleLeft;
    }
    void BuildPlanningReminders() {
        _planningUrgent=Column(_root,"Priority Reminders",5);
        Pin(_planningUrgent,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-99),new Vector2(390,0));
        _planningUrgent.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,0);
        _planningNoticeButton=HudButton(_root,"消息 · 0","info",()=>{_planningNoticesOpen=!_planningNoticesOpen;_statusKey="";RenderPlanningStatus();},39);
        Pin((RectTransform)_planningNoticeButton.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(425,-99),new Vector2(145,39));
        _planningNoticePanel=Column(_root,"Ordinary Notifications",5);
        Pin(_planningNoticePanel,new Vector2(0,1),new Vector2(0,1),new Vector2(425,-145),new Vector2(365,0));
        _planningNoticePanel.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(8,8,8,8);
        _planningNoticePanel.gameObject.AddComponent<Image>().color=_hudFill;_planningNoticePanel.gameObject.SetActive(false);
        _status=_planningUrgent;_speech=_planningNoticePanel;
        _planningToastSurface=HudSurface("HUD Action Receipt",_root);
        Pin(_planningToastSurface,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,158),new Vector2(780,45));
        _toast=Label(_planningToastSurface,"",15,Ink,41);Stretch(_toast.rectTransform,12,2,12,2);_toast.alignment=TextAnchor.MiddleCenter;_planningToastSurface.gameObject.SetActive(false);
    }
    void BuildPlanningContext() {BuildTilePanel();}
    void BuildPlanningMapFilters() {
        _planningLayers=HudSurface("HUD Map Layers",_root);
        Pin(_planningLayers,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,18),new Vector2(560,76));
        foreach(var pair in new[]{new[]{"terrain","地貌"},new[]{"medical","医疗"},new[]{"education","教育"},new[]{"entertainment","休闲"},new[]{"commercial","商业"},new[]{"employment","就业"},new[]{"pollution","污染"},new[]{"heat","余热"},new[]{"stock","库存"},new[]{"energy","电力"}}){
            var layer=pair[0];var button=HudButton(_planningLayers,pair[1],layer,()=>SetLayer(layer),46);_hudLayerButtons[layer]=button;
            var caption=button.GetComponentInChildren<Text>();HudFont(caption,12);caption.alignment=TextAnchor.MiddleLeft;
            AddDeskTip(button.gameObject,pair[1]+"图层",()=>LayerLabel(layer)+" · "+(MapSourcePhase=="committed"?"最近周期实际":MapSourcePhase=="baseline"?"同一步现状预测":"同一步建成预测"));
        }
        _planningLegend=HudSurface("HUD Layer Source",_root,new Color(.04f,.075f,.10f,.94f));
        Pin(_planningLegend,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,103),new Vector2(560,30));
        _legend=Label(_planningLegend,"地貌 · 最近周期实际 · L / Shift+L · Shift 数值",13,Ink,27);Stretch(_legend.rectTransform,8,1,8,1);_legend.alignment=TextAnchor.MiddleCenter;
    }
    void BuildPlanningFooter() {
        _planningRail=HudSurface("HUD Navigation",_root);
        Pin(_planningRail,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-96),new Vector2(100,482));
        var nav=new[]{new[]{"city","城市","city"},new[]{"population_home","居民与服务","population"},new[]{"economy_home","资源与财政","currency"},new[]{"construction","施工","build"},new[]{"tasks_home","任务与成长","task"},new[]{"bonus_home","Bonus","bonus"},new[]{"records","档案","archive"}};
        foreach(var title in new[]{"街区经营","发展与能力","城市记忆"})_navGroupLabels.Add(Label(_planningRail,title,10,Muted));
        for(int i=0;i<nav.Length;i++){
            string route=nav[i][0];var button=HudButton(_planningRail,nav[i][1],nav[i][2],()=>OpenSection(route),40);_topRoutes[route]=button;
            var label=button.GetComponentInChildren<Text>();HudFont(label,route=="population_home"||route=="economy_home"||route=="tasks_home"?12:14);label.alignment=TextAnchor.MiddleLeft;
            if(route=="city"){button.interactable=false;label.text="城市 · 待开放";HudFont(label,11);AddDeskTip(button.gameObject,"永久城市","暂未开放。已有记录可在档案查阅。");}
            if(route=="bonus_home"){
                _hudBonusBadge=Label(button.transform,"0",11,Gold);_hudBonusBadge.name="HUD Bonus License Badge";_hudBonusBadge.alignment=TextAnchor.MiddleCenter;
                AddDeskTip(button.gameObject,"Bonus 部署许可",()=>"未安装副本 "+S["block"].Get("bonus_wallet",V.Map()).Values.Sum(w=>w.Get("charges",0).Long)+" 份；另有 "+Bonus.pending(G).Count+" 份提案待领取。安装消耗副本，拆下不返还。");
            }
        }
        _planningTime=HudSurface("HUD Time Controls",_root);
        Pin(_planningTime,new Vector2(1,0),new Vector2(1,0),new Vector2(-18,18),new Vector2(440,76));
        _planningClock=Label(_planningTime,"",13,Ink);
        Pin(_planningClock.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(12,0),new Vector2(148,58));_planningClock.alignment=TextAnchor.MiddleLeft;
        _pause=HudButton(_planningTime,"继续","",()=>{
            if(_mapViewing){ShowMessage("请先返回方案比较页面。");return;}
            if(Runtime.MandatoryPending){OpenArchive("settlement");return;}
            Runtime.SetPaused(!Runtime.WorldPaused);
        },48,true);
        Pin((RectTransform)_pause.transform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(164,0),new Vector2(61,48));
        HudFont(_pause.GetComponentInChildren<Text>(),15);Stretch(_pause.GetComponentInChildren<Text>().rectTransform,3,2,3,2);
        int speedIndex=0;foreach(var mode in G.c.beta["time"]["modes"]){
            var id=mode["id"].Text;var index=speedIndex++;
            var button=HudButton(_planningTime,index==0?"1×":index==1?"›":index==2?"»":"»»","",()=>{if(_mapViewing){ShowMessage("请先返回方案比较页面。");return;}Runtime.SetMode(id);if(Runtime.MandatoryPending)OpenArchive("settlement");},48);
            _hudSpeedButtons.Add(button);var speedLabel=button.GetComponentInChildren<Text>();HudFont(speedLabel,14);Stretch(speedLabel.rectTransform,3,2,3,2);
            Pin((RectTransform)button.transform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(230+index*51,0),new Vector2(48,48));
            AddDeskTip(button.gameObject,"时间速度",()=>mode.Get("label",id).Text);
        }
    }
    void TogglePlanningBuildMenu() {
        if(ConstructionIntentActive)EndConstructionIntent();else BeginConstructionIntent();RefreshHudGeometry();
    }
    void RefreshPlanningHud() {
        if(_dt==null||G==null||S.Count==0)return;
        _dt.text=F(S.Get("dt",0).Double,2);
        _population.text=F(S.Get("metrics",V.Map()).Get("population",0).Double,0);
        _food.text=F(G.inventory_total("food"),1);
        _planningStockValues[0].text=F(G.inventory_total("material"),1);
        _planningStockValues[1].text=F(G.inventory_total("goods"),1);
        _planningStockValues[2].text=F(S.Get("currency",0).Double,1);
        var metrics=S.Get("metrics",V.Map());
        RefreshResourceDetails();RefreshCapacityHud();
        long copies=0;foreach(var item in S.Get("block",V.Map()).Get("bonus_wallet",V.Map()).Values)copies+=item.Get("charges",0).Long;

        if(_planningCopyCount!=null)_planningCopyCount.text=copies.ToString(CultureInfo.InvariantCulture);
        if(_hudBonusBadge!=null)_hudBonusBadge.text=copies.ToString(CultureInfo.InvariantCulture);
        var clock=Clock.summary(G);var speed=clock.Get("acceleration",0).Double;
        _time.text=clock.Get("date","").Text+" · "+(Runtime.WorldPaused?"已暂停":F(speed,0)+"×");
        _planningClock.text=clock.Get("date","").Text+"\n"+(Runtime.MandatoryPending?"待处理决定":Runtime.WorldPaused?"已暂停":F(speed,0)+"×  运行中");
        _pause.GetComponentInChildren<Text>().text=Runtime.MandatoryPending?"处理":Runtime.WorldPaused?"继续":"暂停";
        if(_legend!=null)_legend.text=LayerLabel(Layer)+" · "+(MapSourcePhase=="committed"?"最近周期实际":MapSourcePhase=="baseline"?"同一步现状预测":"同一步建成预测")+" · L / Shift+L · Shift 数值";
        RefreshPlanningObjective();
        RefreshHudGeometry();
        RenderPlanningStatus();
    }
    void RefreshHudGeometry(){
        if(_planningTop==null)return;
        float ui=Mathf.Clamp((float)Runtime.DisplaySettings.Get("ui_scale",1).Double,.8f,1.2f);
        float width=_root.rect.width>100?_root.rect.width:2560/ui,height=_root.rect.height>100?_root.rect.height:1440/ui;
        float font=TextScale,gap=12,top=64*font,navY=top+gap,navRow=40+16*(font-1),navHeight=8+7*(navRow+4)+66*font;
        _planningTop.offsetMin=new Vector2(0,-top);_planningTop.offsetMax=Vector2.zero;
        var brand=_planningTop.Find("HUD Brand")?.GetComponent<Text>();if(brand!=null){HudFont(brand,22);brand.resizeTextMaxSize=Mathf.RoundToInt(22*font);}
        float cellWidth=Mathf.Min(190,(width-84-324)/6);int i=0;
        foreach(Transform child in _planningTop){
            if(!child.name.StartsWith("HUD Resource "))continue;var cell=(RectTransform)child;cell.anchoredPosition=new Vector2(84+i++*cellWidth,0);cell.sizeDelta=new Vector2(cellWidth,-4);
            var icon=child.GetComponentInChildren<CityHudGlyph>();if(icon!=null)Pin(icon.rectTransform,new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(20,0),new Vector2(24,24));
            foreach(var text in child.GetComponentsInChildren<Text>()){
                bool value=text.name.StartsWith("HUD Value "),detail=text.name.StartsWith("HUD Detail ");int size=value?18:detail?10:11;HudFont(text,size);
                Pin(text.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(38,(detail?-43:value?-19:-1)*font),new Vector2(cellWidth-42,(detail?17:value?28:20)*font));
                if(value){text.resizeTextMinSize=Mathf.RoundToInt(11*font);text.resizeTextMaxSize=Mathf.RoundToInt(18*font);}
            }
        }
        var capacity=_planningTop.Find("HUD Capacity") as RectTransform;if(capacity!=null){Pin(capacity,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-12,0),new Vector2(304,58*font));int index=0;foreach(Transform child in capacity){if(!child.name.StartsWith("Capacity "))continue;Pin((RectTransform)child,new Vector2(0,1),new Vector2(0,1),new Vector2(5,-index++*29*font),new Vector2(294,28*font));foreach(var text in child.GetComponentsInChildren<Text>())text.rectTransform.sizeDelta=new Vector2(text.rectTransform.sizeDelta.x,19*font);}}
        if(_planningRail!=null){Pin(_planningRail,new Vector2(0,1),new Vector2(0,1),new Vector2(gap,-navY),new Vector2(158,navHeight));i=0;int groupIndex=0;float navOffset=4;foreach(var entry in _topRoutes){
            if(i==1||i==4||i==6){var group=_navGroupLabels[groupIndex++];Pin(group.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-navOffset),new Vector2(138,22*font));navOffset+=22*font;}
            var button=entry.Value;Pin((RectTransform)button.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(4,-navOffset),new Vector2(150,navRow));navOffset+=navRow+4;i++;
            var label=button.GetComponentInChildren<Text>();Stretch(label.rectTransform,35,2,entry.Key=="bonus_home"?32:3,2);label.alignment=TextAnchor.MiddleLeft;
            var icon=button.GetComponentInChildren<CityHudGlyph>();if(icon!=null)Pin(icon.rectTransform,new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(18,0),new Vector2(22,22));
            bool active=_archiveOpen&&(_page==entry.Key||entry.Key==ArchiveCategory(_page));button.GetComponent<Image>().color=active?new Color(.12f,.28f,.25f,.96f):Color.clear;
        }}
        if(_hudBonusBadge!=null){HudFont(_hudBonusBadge,11);Pin(_hudBonusBadge.rectTransform,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-3,0),new Vector2(30,navRow-4));}
        if(_bonusBucket!=null&&_topRoutes.TryGetValue("bonus_home",out var bonus)){var anchor=(RectTransform)bonus.transform;_bonusBucket.position=anchor.TransformPoint(anchor.rect.center);_bonusBucket.sizeDelta=anchor.rect.size;_bonusBucket.gameObject.SetActive(false);}
        float contextWidth=640,contextBottom=18,contextHeight=Mathf.Min(520,height-navY-navHeight-gap-contextBottom);
        if(_context!=null)Pin(_context,new Vector2(0,0),new Vector2(0,0),new Vector2(gap,contextBottom),new Vector2(contextWidth,Mathf.Max(160,contextHeight)));
        float timeWidth=360+40*(font-1),timeHeight=68,clockWidth=140+40*(font-1);
        if(_planningTime!=null){Pin(_planningTime,new Vector2(1,0),new Vector2(1,0),new Vector2(-gap,gap),new Vector2(timeWidth,timeHeight));
            if(_planningClock!=null){var clock=Clock.summary(G);_planningClock.text=clock.Get("date","").Text+"\n"+(Runtime.MandatoryPending?"待处理决定":Runtime.DecisionOpen?"规划中 · 已暂停":Runtime.WorldPaused?"已暂停":F(clock.Get("acceleration",0).Double,0)+"×  运行中");HudFont(_planningClock,12);Pin(_planningClock.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(8,0),new Vector2(clockWidth-8,54));}
            if(_pause!=null)Pin((RectTransform)_pause.transform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(clockWidth+4,0),new Vector2(48,46));
            for(i=0;i<_hudSpeedButtons.Count;i++)Pin((RectTransform)_hudSpeedButtons[i].transform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(clockWidth+56+i*39,0),new Vector2(36,46));
        }
        float layerLeft=contextWidth+40,layerRight=width-timeWidth-2*gap,layerSpace=layerRight-layerLeft,layerCell=64+24*(font-1);
        int layerColumns=layerSpace>=10*layerCell?10:5;float layerWidth=layerColumns*layerCell,layerHeight=layerColumns==10?46:92,layerX=(layerLeft+layerRight)/2;
        if(_planningLayers!=null){Pin(_planningLayers,new Vector2(0,0),new Vector2(.5f,0),new Vector2(layerX,gap),new Vector2(layerWidth,layerHeight));i=0;foreach(var entry in _hudLayerButtons){
            var button=entry.Value;Pin((RectTransform)button.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(i%layerColumns*layerCell,-i/layerColumns*46),new Vector2(layerCell-2,44));i++;
            var caption=button.GetComponentInChildren<Text>();HudFont(caption,12);Stretch(caption.rectTransform,27,2,1,2);
            var icon=button.GetComponentInChildren<CityHudGlyph>();if(icon!=null)Pin(icon.rectTransform,new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(13,0),new Vector2(20,20));
            bool active=entry.Key==Layer;button.GetComponent<Image>().color=active?new Color(.12f,.28f,.25f,.96f):Color.clear;caption.color=active?Ink:Muted;button.GetComponentInChildren<CityHudFrame>().enabled=active;
        }}
        if(_planningLegend!=null)Pin(_planningLegend,new Vector2(0,0),new Vector2(.5f,0),new Vector2(layerX,gap+layerHeight+4),new Vector2(layerWidth,(Layer=="terrain"?28:76)*font));
        if(_planningToastSurface!=null)Pin(_planningToastSurface,new Vector2(0,0),new Vector2(.5f,0),new Vector2(layerX,gap+layerHeight+8+(Layer=="terrain"?28:76)*font),new Vector2(Mathf.Min(layerSpace,780),45));
        if(_planningOverview!=null&&_guidanceTitle!=null){
            _planningOverview.gameObject.SetActive((!_archiveOpen||CompactWorkspaceOpen)&&!ObjectDrawerVisible&&!ToolkitUpgradeOpen&&!ConstructionWindowOpen);_planningOverviewBody.gameObject.SetActive(_planningOverviewOpen);
            float titleHeight=HudMeasuredHeight(_guidanceTitle,312,36*font),reasonHeight=HudMeasuredHeight(_guidanceReason,312,46*font);
            Height(_guidanceTitle.rectTransform,titleHeight);Height(_guidanceReason.rectTransform,reasonHeight);Height((RectTransform)_guidanceButton.transform,43*font);
            Pin(_planningOverview,new Vector2(1,1),new Vector2(1,1),new Vector2(-gap,-navY),new Vector2(350,_planningOverviewOpen?120*font+titleHeight+reasonHeight+100*font:120*font));
            Pin(_goalSummary,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-42),new Vector2(350,76*font));Height(_goalSummaryText.rectTransform,48*font);
            Pin(_planningOverviewBody,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-120*font),new Vector2(348,0));
            var header=_planningOverview.GetComponentInChildren<Button>();if(header!=null)header.GetComponentInChildren<Text>().text=_planningOverviewOpen?"下一步  ▴":"下一步  ▾";
        }
        if(_planningUrgent!=null)Pin(_planningUrgent,new Vector2(1,1),new Vector2(1,1),new Vector2(-gap,-navY-(_planningOverview!=null?_planningOverview.sizeDelta.y:43)-gap),new Vector2(350,0));
        if(_planningNoticeButton!=null)Pin((RectTransform)_planningNoticeButton.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-gap,gap+timeHeight+8),new Vector2(160,36));
        if(_planningNoticePanel!=null)Pin(_planningNoticePanel,new Vector2(1,0),new Vector2(1,0),new Vector2(-gap,gap+timeHeight+50),new Vector2(350,0));
        LayoutMapInspectionHud();
    }
    float HudMeasuredHeight(Text label,float width,float minimum){
        var settings=label.GetGenerationSettings(new Vector2(width,0));
        return Mathf.Max(minimum,label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text,settings)/label.pixelsPerUnit);
    }
    string PlanningResourceExplanation(string name) {
        if(name=="DT")return "有限主动加速预算。仅额外推进的本地时间消耗 DT；不与通货混为一谈。剩余："+F(S.Get("dt",0).Double,4);
        if(name=="人口")return "真实人口："+F(S["metrics"].Get("population",0).Double,2)+"；已安置："+F(S["metrics"].Get("housed",0).Double,2)+"。移民使用本地时间。";
        if(name=="通货")return "当前余额："+F(S.Get("currency",0).Double,2)+"；最近周期净经营："+KnownRate(S["metrics"].Get("cashflow",0).Double)+"。建设、交易与运营分别留来源。";
        if(name=="电力")return "供给 / 需求为能力，非累计库存。供给："+F(S["metrics"].Get("energy",0).Double,3)+"；需求："+F(S["metrics"].Get("energy_need",0).Double,3)+"。";
        if(name=="未安装副本")return "只计当前 Block 尚可消耗的许可；已安装实例另外保留，不重复计入。拆下不返还。";
        var resource=name=="粮食"?"food":name=="材料"?"material":"goods";return name+"库存："+F(G.inventory_total(resource),3)+" / "+F(G.stock_cap(resource),3)+"。最近周期净流量："+KnownRate(S["metrics"].Get("flows",V.Map()).Get(resource,0).Double)+"。位置与来源在资源档案。";
    }
    void RefreshPlanningObjective() {
        var guidance=CityGuidanceView();
        _guidanceTitle.text=guidance["title"].Text;_guidanceReason.text=guidance["reason"].Text;
        _guidanceButton.GetComponentInChildren<Text>().text=guidance["action_label"].Text+"  ›";
        _guidanceTitle.color=guidance.Get("severity",0).Long>=2?Gold:Ink;
        if(_goalSummaryText!=null){var theme=Themes.view(G);var goals=theme.Get("goals",V.Arr());var goal=goals.FirstOrDefault(x=>!x.Get("met",false).Bool)??goals.FirstOrDefault();
            if(goal!=null){var factor=goal.Get("unit","").Text=="%"?100:1;var upper=goal.Get("comparison","").Text=="max";var target=goal.Get("target",0).Double;var value=goal.Get("value",0).Double;
                _goalSummaryText.text=theme.Get("name","当前目标").Text+" · "+goal.Get("label","").Text+"\n"+F(value*factor)+(upper?" ≤ ":" / ")+F(target*factor)+goal.Get("unit","").Text;
                _goalProgress.gameObject.SetActive(target>0&&!upper);_goalProgress.Ratio=(float)(target>0?value/target:0);_goalProgress.color=goal.Get("met",false).Bool?Green:Gold;_goalProgress.SetVerticesDirty();
            }else{_goalSummaryText.text=guidance["title"].Text;_goalProgress.gameObject.SetActive(false);}
        }
    }
    string HudNoticeTitle(string text)=>text.Length>13?text.Substring(0,12)+"…":text;
    void RenderPlanningStatus() {
        if(PointerBusy){_statusDirty=true;return;}
        _statusDirty=false;
        var summaries=_feedbackView.Get("summaries",V.Arr());
        var bubbles=_feedbackView.Get("bubbles",V.Arr());
        var key=V.Arr(_statusPending,Runtime.MandatoryPending,StyleKey(),Runtime.DisplaySettings.Get("citizen_bubbles",false),_cityIdentity);
        foreach(var item in summaries)key.Add(V.Arr(item.Get("id",""),item.Get("text",""),item.Get("severity",0),item.Get("building_id",""),item.Get("layer","")));
        foreach(var item in bubbles)key.Add(V.Arr(item.Get("id",""),item.Get("text",""),item.Get("building_id",""),item.Get("layer","")));
        string value=key.ToJson(false,true);if(value==_statusKey)return;_statusKey=value;StatusRebuildCount++;
        Clear(_planningUrgent);Clear(_planningNoticePanel);
        var urgent=summaries.Where(x=>x.Get("severity",0).Long>=2).ToArray();
        var ordinary=summaries.Where(x=>x.Get("severity",0).Long<2).ToArray();
        int ordinaryCount=ordinary.Length+(Runtime.DisplaySettings.Get("citizen_bubbles",false).Bool?bubbles.Count:0);
        _planningNoticeButton.GetComponentInChildren<Text>().text="消息 · "+ordinaryCount+(_planningNoticesOpen?"  ▴":"  ▾");
        if(Runtime.MandatoryPending){var b=HudButton(_planningUrgent,"决策待处理","license",()=>OpenArchive("settlement"),38,true);AddDeskTip(b.gameObject,"需要决定",()=>"游戏已按真实结算规则暂停。奖励、事件或结算尚待处理；关闭面板不会静默跳过。");}
        
        foreach(var item in urgent.Take(Runtime.MandatoryPending?0:1)){
            var alert=item;var button=HudButton(_planningUrgent,HudNoticeTitle(alert.Get("text","需要关注").Text),"info",()=>{if(_archiveOpen)CloseArchive();SelectBuilding(alert.Get("building_id","").Text);SetLayer(alert.Get("layer","terrain").Text);},38*TextScale,true);
            AddDeskTip(button.gameObject,"优先提醒",()=>alert.Get("text","城市状况").Text+" · 查看建筑与实际来源");
        }
        if(urgent.Length>(Runtime.MandatoryPending?0:1))HudButton(_planningUrgent,"需关注 · "+urgent.Length+" 项  ›","info",()=>OpenArchive("records"),34);
        foreach(var item in ordinary){var note=item;HudButton(_planningNoticePanel,note.Get("text","城市消息").Text,"info",()=>{if(_archiveOpen)CloseArchive();SelectBuilding(note.Get("building_id","").Text);SetLayer(note.Get("layer","terrain").Text);},38);}
        if(Runtime.DisplaySettings.Get("citizen_bubbles",false).Bool)foreach(var item in bubbles.Take(4)){var note=item;HudButton(_planningNoticePanel,note.Get("text","城市消息").Text,"info",()=>{if(_archiveOpen)CloseArchive();SelectBuilding(note.Get("building_id","").Text);SetLayer(note.Get("layer","terrain").Text);},38);}
        if(ordinaryCount==0)Label(_planningNoticePanel,"当前没有普通通知",14,Muted,28);
        _planningNoticeButton.gameObject.SetActive(ordinaryCount>0||_planningNoticesOpen);
        if(Runtime.DisplaySettings.Get("citizen_bubbles",false).Bool&&bubbles.Count>4)HudButton(_planningNoticePanel,"其余 "+(bubbles.Count-4)+" 项城市声音","info",()=>OpenArchive("records"),36);
        _planningNoticePanel.gameObject.SetActive(_planningNoticesOpen);
    }
}
}
