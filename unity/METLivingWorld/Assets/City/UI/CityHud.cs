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
    Text _guidanceTitle,_guidanceReason,_buildHint;Button _guidanceButton;
    readonly Dictionary<string,Button> _foundationButtons=new Dictionary<string,Button>();
    readonly Dictionary<string,Button> _topRoutes=new Dictionary<string,Button>();
    readonly List<Text> _planningStockValues = new List<Text>();
    Text _planningObjectiveText, _planningClock, _planningTaskCount, _planningLicenseCount,
        _planningBonusCount, _planningPendingCount, _planningCopyCount, _planningBuildToggle;
    Button _planningNoticeButton;
    readonly Dictionary<string,Button> _hudLayerButtons=new Dictionary<string,Button>();
    bool _planningNoticesOpen, _planningOverviewOpen=true, _planningBuildOpen;
    readonly Color _hudFill=new Color(.045f,.075f,.10f,.98f);
    readonly Color _hudButtonFill=new Color(.075f,.12f,.15f,.94f);
    readonly Color _hudEdge=new Color(.22f,.78f,.76f,.58f);

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
        var edge=line.gameObject.AddComponent<CityHudFrame>();edge.color=new Color(.36f,.49f,.55f,.32f);edge.raycastTarget=false;
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
        _planningTop=HudSurface("HUD Resources",_root,new Color(.045f,.073f,.10f,.98f));
        _planningTop.anchorMin=new Vector2(0,1);_planningTop.anchorMax=Vector2.one;
        _planningTop.pivot=new Vector2(.5f,1);_planningTop.offsetMin=new Vector2(0,-76);_planningTop.offsetMax=Vector2.zero;
        var brand=Label(_planningTop,"MET",24,Ink);Pin(brand.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(18,0),new Vector2(65,40));
        brand.raycastTarget=true;var menu=brand.gameObject.AddComponent<Button>();menu.onClick.AddListener(()=>OpenArchive("settings"));
        brand.name="HUD Brand";brand.resizeTextForBestFit=true;brand.resizeTextMinSize=18;brand.resizeTextMaxSize=24;brand.verticalOverflow=VerticalWrapMode.Truncate;brand.alignment=TextAnchor.MiddleLeft;
        string[] names={"DT","人口","粮食","材料","制成品","通货"};string[] kinds={"dt","population","food","material","goods","currency"};
        for(int i=0;i<names.Length;i++){
            var name=names[i];var cell=HudSurface("HUD Resource "+name,_planningTop,new Color(0,0,0,0));
            cell.anchorMin=new Vector2(0,0);cell.anchorMax=new Vector2(0,1);cell.pivot=new Vector2(0,.5f);
            cell.GetComponentInChildren<CityHudFrame>().DividerOnly=true;
            var glyph=HudIcon(cell,kinds[i],i==2?Green:i==3||i==5||i==6?Gold:Blue,28);
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
            AddDeskTip(cell.gameObject,name,()=>PlanningResourceExplanation(name));
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
        _planningOverviewBody=Column(_planningOverview,"Overview Groups",10);
        var layout=_planningOverviewBody.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(18,18,10,15);
        Pin(_planningOverviewBody,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-43),new Vector2(348,0));
        _planningObjective=_planningOverviewBody;
        _guidanceTitle=Label(_planningOverviewBody,"",22,Ink,55*TextScale);
        _guidanceReason=Label(_planningOverviewBody,"",15,Muted,66*TextScale);
        _guidanceButton=Button(_planningOverviewBody,"查看城市",RunCityGuidanceAction,true);_guidanceButton.name="HUD Next Action";
        AttachCityGuidancePointerGuard();
        var more=HudButton(_planningOverviewBody,"查看城市状况 ›","",()=>OpenArchive("overview"),30*TextScale);more.GetComponentInChildren<Text>().alignment=TextAnchor.MiddleLeft;
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
    void BuildPlanningContext() {
        _context=HudSurface("HUD Site Context",_root);
        Pin(_context,new Vector2(0,0),new Vector2(0,0),new Vector2(18,154),new Vector2(480,0));
        _context.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var layout=_context.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=4;layout.padding=new RectOffset(8,8,7,7);layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;
        _context.gameObject.SetActive(false);
    }
    void BuildPlanningMapFilters() {
        _planningLayers=HudSurface("HUD Map Layers",_root);
        Pin(_planningLayers,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,18),new Vector2(560,76));
        var row=Row(_planningLayers,"Map Layer Buttons",62);Stretch(row,6,6,6,6);row.GetComponent<HorizontalLayoutGroup>().spacing=2;
        foreach(var pair in new[]{new[]{"terrain","地貌"},new[]{"service","服务"},new[]{"employment","就业"},new[]{"pollution","环境"},new[]{"heat","余热"},new[]{"stock","库存"},new[]{"energy","电力"}}){
            var layer=pair[0];var button=HudButton(row,pair[1],layer,()=>SetLayer(layer),62);_hudLayerButtons[layer]=button;
            var caption=button.GetComponentInChildren<Text>();HudFont(caption,14);caption.alignment=TextAnchor.LowerCenter;Stretch(caption.rectTransform,0,28,0,3);
            var icon=button.GetComponentInChildren<CityHudGlyph>().rectTransform;Pin(icon,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-8),new Vector2(24,24));
            AddDeskTip(button.gameObject,pair[1]+"图层",()=>LayerLabel(layer)+" · "+(MapSourcePhase=="committed"?"最近周期实际":MapSourcePhase=="baseline"?"同一步现状预测":"同一步建成预测"));
        }
        _planningLegend=HudSurface("HUD Layer Source",_root,new Color(.04f,.075f,.10f,.94f));
        Pin(_planningLegend,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,103),new Vector2(560,30));
        _legend=Label(_planningLegend,"地貌 · 最近周期实际 · L / Shift+L",13,Ink,27);Stretch(_legend.rectTransform,8,1,8,1);_legend.alignment=TextAnchor.MiddleCenter;
    }
    void BuildPlanningFooter() {
        _planningRail=HudSurface("HUD Navigation",_root);
        Pin(_planningRail,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-96),new Vector2(100,344));
        var nav=new[]{new[]{"overview","城市","city"},new[]{"resources","经营","stock"},new[]{"bonuses","Bonus","bonus"},new[]{"themes","任务","task"},new[]{"records","档案","license"}};
        for(int i=0;i<nav.Length;i++){
            string route=nav[i][0];var button=HudButton(_planningRail,nav[i][1],nav[i][2],()=>OpenArchive(SidebarForPage(route)),64);_topRoutes[route]=button;
            Pin((RectTransform)button.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(4,-4-i*68),new Vector2(92,64));
            var label=button.GetComponentInChildren<Text>();HudFont(label,15);Stretch(label.rectTransform,4,32,4,2);label.alignment=TextAnchor.LowerCenter;
            var icon=button.GetComponentInChildren<CityHudGlyph>().rectTransform;Pin(icon,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-7),new Vector2(23,23));
        }
        _planningBuildMenu=HudSurface("HUD Construction",_root);
        Pin(_planningBuildMenu,new Vector2(0,0),new Vector2(0,0),new Vector2(18,18),new Vector2(420,124));
        _buildHint=Label(_planningBuildMenu,"建设 · 先选类型，再点空地",13,Muted,22);
        Pin(_buildHint.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-6),new Vector2(395,24));
        int foundationIndex=0;
        foreach(var choice in G.c.v040["progression"]["tier1"]){
            var baseId=choice.Text;var name=G.c.projects[baseId]["name"].Text;
            var button=HudButton(_planningBuildMenu,name,"",()=>{
                if(!Runtime.ViewCanEditFocused){OpenArchive("governance");return;}
                BeginConstructionIntent();_base=baseId;_contextKey="";RefreshContext();RefreshHudGeometry();
            },86);button.name="HUD Build Choice "+baseId;_foundationButtons[baseId]=button;
            Pin((RectTransform)button.transform,new Vector2(0,0),new Vector2(0,0),new Vector2(8+foundationIndex++*136,6),new Vector2(132,84));
            var label=button.GetComponentInChildren<Text>();HudFont(label,15);label.alignment=TextAnchor.LowerCenter;Stretch(label.rectTransform,4,48,4,3);
            var artNode=Node("Foundation Illustration "+baseId,button.transform);Pin(artNode,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-2),new Vector2(95,49));
            var art=artNode.gameObject.AddComponent<CityBlueprintArt>();art.ShowDrafting=false;art.Style=CityIllustrationCatalog.StyleForBuilding(baseId);art.Accent=CityIllustrationCatalog.AccentForStyle(art.Style);art.Fill=1;art.color=Color.white;art.raycastTarget=false;
            AddDeskTip(button.gameObject,name,()=>ConstructionPurpose(G.c.projects[baseId])+"。选择类型后点一块空闲陆地，查看实际报价与建成效果。");
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
            var speedLabel=button.GetComponentInChildren<Text>();HudFont(speedLabel,14);Stretch(speedLabel.rectTransform,3,2,3,2);
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
        var clock=Clock.summary(G);var speed=clock.Get("acceleration",0).Double;
        _time.text=clock.Get("date","").Text+" · "+(Runtime.WorldPaused?"已暂停":F(speed,0)+"×");
        _planningClock.text=clock.Get("date","").Text+"\n"+(Runtime.MandatoryPending?"待处理决定":Runtime.WorldPaused?"已暂停":F(speed,0)+"×  运行中");
        _pause.GetComponentInChildren<Text>().text=Runtime.MandatoryPending?"处理":Runtime.WorldPaused?"继续":"暂停";
        if(_legend!=null)_legend.text=LayerLabel(Layer)+" · "+(MapSourcePhase=="committed"?"最近周期实际":MapSourcePhase=="baseline"?"同一步现状预测":"同一步建成预测")+" · L / Shift+L";
        RefreshPlanningObjective();
        RefreshHudGeometry();
        RenderPlanningStatus();
    }
    void RefreshHudGeometry(){
        if(_planningTop==null)return;
        if(_planningClock!=null){var clock=Clock.summary(G);_planningClock.text=clock.Get("date","").Text+"\n"+(Runtime.MandatoryPending?"待处理决定":Runtime.DecisionOpen?"规划中 · 已暂停":Runtime.WorldPaused?"已暂停":F(clock.Get("acceleration",0).Double,0)+"×  运行中");}
        float factor=Mathf.Clamp((float)Runtime.DisplaySettings.Get("ui_scale",1).Double,.8f,1.2f);float cellWidth=Mathf.Min(190,(1920/factor-420)/6);int i=0;
        var brand=_planningTop.Find("HUD Brand")?.GetComponent<Text>();if(brand!=null)brand.resizeTextMaxSize=Mathf.RoundToInt(24*TextScale);
        foreach(Transform child in _planningTop){
            if(!child.name.StartsWith("HUD Resource "))continue;var cell=(RectTransform)child;cell.anchoredPosition=new Vector2(84+i++*cellWidth,0);cell.sizeDelta=new Vector2(cellWidth,-6);
            foreach(var text in child.GetComponentsInChildren<Text>()){
                var rect=text.rectTransform;rect.sizeDelta=new Vector2(cellWidth-49,rect.sizeDelta.y);
                if(text.name.StartsWith("HUD Value ")){text.resizeTextMinSize=Mathf.RoundToInt(12*TextScale);text.resizeTextMaxSize=Mathf.RoundToInt(21*TextScale);}
            }
        }
        foreach(var entry in _hudLayerButtons){bool active=entry.Key==Layer;entry.Value.GetComponent<Image>().color=active?new Color(.12f,.28f,.25f,.96f):Color.clear;entry.Value.GetComponentInChildren<Text>().color=active?Ink:Muted;entry.Value.GetComponentInChildren<CityHudFrame>().enabled=active;}
        foreach(var entry in _foundationButtons){bool active=ConstructionIntentActive&&entry.Key==_base;entry.Value.GetComponent<Image>().color=active?new Color(.12f,.28f,.25f,.96f):Color.clear;entry.Value.GetComponentInChildren<CityHudFrame>().enabled=active;}
        foreach(var entry in _topRoutes){bool active=_archiveOpen&&(_page==entry.Key||entry.Key==ArchiveCategory(_page));entry.Value.GetComponent<Image>().color=active?new Color(.12f,.28f,.25f,.96f):Color.clear;}
        if(_bonusBucket!=null){Pin(_bonusBucket,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-450),new Vector2(100,48));}
        if(_buildHint!=null)_buildHint.text=ConstructionIntentActive?G.c.projects[_base]["name"].Text+" · 点空地选址 / Esc 取消":"建设 · 先选类型，再点空地";
        if(_planningOverview!=null&&_guidanceTitle!=null){
            _planningOverview.gameObject.SetActive(!_archiveOpen);
            float titleHeight=HudMeasuredHeight(_guidanceTitle,312,36*TextScale),reasonHeight=HudMeasuredHeight(_guidanceReason,312,46*TextScale);
            Height(_guidanceTitle.rectTransform,titleHeight);Height(_guidanceReason.rectTransform,reasonHeight);Height((RectTransform)_guidanceButton.transform,43*TextScale);
            _planningOverview.sizeDelta=new Vector2(350,_planningOverviewOpen?98+titleHeight+reasonHeight+73*TextScale:43);
        }
        if(_planningUrgent!=null){float y=115+(_planningOverviewOpen?_planningOverview.sizeDelta.y:43);Pin(_planningUrgent,new Vector2(1,1),new Vector2(1,1),new Vector2(-18,-y),new Vector2(350,0));}
        if(_planningNoticeButton!=null)Pin((RectTransform)_planningNoticeButton.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-18,110),new Vector2(160,36));
        if(_planningNoticePanel!=null)Pin(_planningNoticePanel,new Vector2(1,0),new Vector2(1,0),new Vector2(-18,154),new Vector2(350,0));
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
