using System;
using System.Globalization;
using System.Linq;
using MET.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
public sealed partial class CityInterface {
 RectTransform OperatingPaint(string name,Transform parent,Color color){var node=Node(name,parent);var image=node.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return node;}
 RectTransform OperatingSection(Transform parent,string title,string aside=""){
  var section=Column(parent,"Operating section "+title,7);section.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,0,8);
  var heading=Row(section,"Operating heading",34*TextScale);Height(heading,34*TextScale);Label(heading,title,16,Ink,34*TextScale);
  if(aside!=""){var note=Label(heading,aside,12,Muted,34*TextScale);note.alignment=TextAnchor.MiddleRight;}
  var divider=OperatingPaint("Operating section divider",section,new Color(.33f,.43f,.44f,.5f));Height(divider,1);divider.GetComponent<Image>().raycastTarget=false;return section;
 }
 RectTransform OperatingGrid(Transform parent,string name){var row=Row(parent,"Operating "+name,1);row.GetComponent<HorizontalLayoutGroup>().spacing=10;return row;}
 RectTransform OperatingCell(Transform parent){var cell=Column(parent,"Operating cell",5);cell.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,4,4);var size=cell.gameObject.AddComponent<LayoutElement>();size.minWidth=0;size.preferredWidth=0;size.flexibleWidth=1;return cell;}
 RectTransform OperatingCard(Transform parent,string name){var card=CompactTaskCard(parent,"Operating "+name);card.GetComponent<Image>().color=new Color(.105f,.16f,.18f,.84f);var layout=card.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(10,10,8,8);layout.spacing=4;var size=card.gameObject.AddComponent<LayoutElement>();size.minWidth=0;size.preferredWidth=0;size.flexibleWidth=1;return card;}
 void OperatingGlyph(Transform parent,string kind,float size=20){parent.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;HudIcon(parent,kind,Green,size);}
 Button OperatingButton(Transform parent,string title,Action action,bool active=false,bool disabled=false){var button=Button(parent,title,action,active,disabled);Height((RectTransform)button.transform,32*TextScale);var text=button.GetComponentInChildren<Text>();text.fontSize=Mathf.RoundToInt(13*TextScale);var size=button.GetComponent<LayoutElement>();size.minWidth=0;size.preferredWidth=0;size.flexibleWidth=1;return button;}
 void OperatingStateNote(){if(S.Get("simulation_tick",0).Long<=0)Label(_body,"待首次实际结算 · 当前库存和容量已就绪，月流、服务分配与施工使用待记录。",13,Muted);}
 void OperatingMeter(Transform parent,string icon,string title,double value,double capacity,string unit,string note,Color tone,bool known=true){
  var meter=Column(parent,"Operating meter "+title,3);meter.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,2,4);
  var heading=Row(meter,"Meter label",27*TextScale);Height(heading,27*TextScale);heading.GetComponent<HorizontalLayoutGroup>().spacing=6;OperatingGlyph(heading,icon,24*TextScale);var label=Label(heading,title,14,Muted,27*TextScale);var labelSize=label.GetComponent<LayoutElement>();labelSize.minWidth=0;labelSize.preferredWidth=0;labelSize.flexibleWidth=1;
  string reading=!known?"待结算":capacity<=0&&value==0?"暂无容量":F(value)+" / "+F(capacity)+" "+unit;var readout=Label(heading,reading,15,known&&value>capacity?Gold:Ink,27*TextScale);readout.alignment=TextAnchor.MiddleRight;var readSize=readout.GetComponent<LayoutElement>();readSize.minWidth=0;readSize.preferredWidth=0;readSize.flexibleWidth=1.3f;
  var track=OperatingPaint("Operating meter track",meter,new Color(.065f,.105f,.125f));Height(track,8);track.GetComponent<Image>().raycastTarget=false;
  if(known&&capacity>0){var fill=OperatingPaint("Operating meter fill",track,tone);fill.anchorMin=Vector2.zero;fill.anchorMax=new Vector2(Mathf.Clamp01((float)(value/capacity)),1);fill.offsetMin=fill.offsetMax=Vector2.zero;fill.GetComponent<Image>().raycastTarget=false;}
  if(known&&value>capacity){Label(meter,"超出容量 "+F(value-capacity)+" "+unit,12,Gold,23*TextScale);}
  if(note!="")Label(meter,note,12,Muted);
 }
 void OperatingFlow(Transform parent,string title,double value,double scale,string unit,Action action=null){
  var item=Column(parent,"Operating flow "+title,2);item.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,2,4);
  var row=Row(item,"Signed contribution",34*TextScale);Height(row,34*TextScale);row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;
  if(action!=null){var button=OperatingButton(row,title+" ›",action);button.GetComponentInChildren<Text>().alignment=TextAnchor.MiddleLeft;button.GetComponent<Image>().color=Color.clear;var titleSize=button.GetComponent<LayoutElement>();titleSize.flexibleWidth=1.25f;}else {var caption=Label(row,title,13,Muted,34*TextScale);var titleSize=caption.GetComponent<LayoutElement>();titleSize.minWidth=0;titleSize.preferredWidth=0;titleSize.flexibleWidth=1.25f;}var track=OperatingPaint("Operating signed flow track",row,new Color(.065f,.105f,.125f));Height(track,14);var size=track.gameObject.AddComponent<LayoutElement>();size.minWidth=0;size.preferredWidth=0;size.flexibleWidth=1;track.GetComponent<Image>().raycastTarget=false;
  var axis=OperatingPaint("Operating flow zero",track,new Color(.52f,.66f,.65f,.6f));axis.anchorMin=new Vector2(.5f,0);axis.anchorMax=new Vector2(.5f,1);axis.offsetMin=new Vector2(-.5f,0);axis.offsetMax=new Vector2(.5f,0);axis.GetComponent<Image>().raycastTarget=false;
  float width=scale>0?Mathf.Clamp01((float)(Math.Abs(value)/scale))*.5f:0;var fill=OperatingPaint("Operating signed contribution",track,value<0?Gold:Green);fill.anchorMin=new Vector2(value<0?.5f-width:.5f,0);fill.anchorMax=new Vector2(value<0?.5f:.5f+width,1);fill.offsetMin=fill.offsetMax=Vector2.zero;fill.GetComponent<Image>().raycastTarget=false;
  var reading=Label(row,Signed(value)+" "+unit,14,value<0?Gold:Green,22*TextScale);Width(reading.rectTransform,110*TextScale);reading.alignment=TextAnchor.MiddleRight;
 }
 void OperatingComposition(Transform parent,string[] names,double[] values){
  double total=0;foreach(var value in values)total+=Math.Max(0,value);var colors=new[]{Green,Blue,Gold,new Color(.71f,.64f,.84f)};var track=OperatingPaint("Operating CP composition",parent,new Color(.065f,.105f,.125f));Height(track,12);track.GetComponent<Image>().raycastTarget=false;double cursor=0;
  for(int i=0;i<names.Length;i++){double positive=Math.Max(0,values[i]);if(total>0&&positive>0){var fill=OperatingPaint("CP composition "+names[i],track,colors[i%colors.Length]);fill.anchorMin=new Vector2((float)(cursor/total),0);fill.anchorMax=new Vector2((float)((cursor+positive)/total),1);fill.offsetMin=fill.offsetMax=Vector2.zero;fill.GetComponent<Image>().raycastTarget=false;}cursor+=positive;}
  var legend=OperatingGrid(parent,"CP composition legend");for(int i=0;i<names.Length;i++)Label(legend,names[i]+" "+F(values[i]),12,colors[i%colors.Length],25*TextScale);
 }
 void V3MarketPage(){
  if(_advancedWorkspace){MarketPage();return;}
  WildTradeSection(_body);
  if(new[]{"food","material","goods"}.Contains(_pageContext))_tradeResource=_pageContext;
  var editor=OperatingSection(_body,"快速限价订单","真实在线货运");
  var resources=OperatingGrid(editor,"Market resources");foreach(var value in new[]{"food","material","goods"}){var resource=value;OperatingButton(resources,Resource(resource),()=>{_tradeResource=resource;_tradeLimit="";_pageContext="";RenderPage();},_tradeResource==resource);}
  var sides=OperatingGrid(editor,"Market side");foreach(var value in new[]{"buy","sell"}){var side=value;OperatingButton(sides,side=="buy"?"买入":"卖出",()=>{_tradeSide=side;_tradeLimit="";RenderPage();},_tradeSide==side);}
  var quote=Market.quote(G,_tradeResource);double price=quote.Get(_tradeSide+"_price",0).Double;if(_tradeLimit=="")_tradeLimit=price.ToString("F3",CultureInfo.InvariantCulture);
  Label(editor,"可用通货 "+F(S.Get("currency",0).Double)+" · "+Resource(_tradeResource)+"库存 "+F(G.inventory_total(_tradeResource))+" · 空仓 "+F(Market._buy_room(G,_tradeResource)),13,Muted);
  var reading=Row(editor,"Market quote reading",30*TextScale);Label(reading,"当前单位报价",14,Muted,30*TextScale);var priceText=Label(reading,F(price,3)+" 通货",16,Ink,30*TextScale);priceText.alignment=TextAnchor.MiddleRight;
  var inputs=OperatingGrid(editor,"Market order fields");var quantityCell=OperatingCell(inputs);Label(quantityCell,"数量",13,Muted,26*TextScale);var quantity=InputBox(quantityCell,_tradeAmount,"输入正数量");Height((RectTransform)quantity.transform,36*TextScale);var limitCell=OperatingCell(inputs);Label(limitCell,"每单位限价",13,Muted,26*TextScale);var limit=InputBox(limitCell,_tradeLimit,"限价可为负");Height((RectTransform)limit.transform,36*TextScale);
  var total=Label(editor,"",14,Ink,30*TextScale);var state=Label(editor,"",13,Muted,30*TextScale);Button submit=null;Action refresh=()=>{bool amountValid=double.TryParse(_tradeAmount,NumberStyles.Float,CultureInfo.InvariantCulture,out var amount)&&!double.IsNaN(amount)&&!double.IsInfinity(amount)&&amount>0;bool limitValid=double.TryParse(_tradeLimit,NumberStyles.Float,CultureInfo.InvariantCulture,out var limitPrice)&&!double.IsNaN(limitPrice)&&!double.IsInfinity(limitPrice);bool freight=Market._total_capacity(G)>.000001;total.text=amountValid?"按当前报价估计通货变化 "+Signed((_tradeSide=="buy"?-1:1)*amount*price):"输入数量后显示报价估计";state.text=!amountValid?"请输入大于零的数量。":!limitValid?"请输入有效限价。":!freight?"需要有正在运行的货运站。":_tradeSide=="buy"?"买价不高于限价时成交；受通货、仓容与运力限制。":"卖价不低于限价时成交；受库存、通货与运力限制。";state.color=amountValid&&limitValid&&freight?Muted:Gold;if(submit!=null)submit.interactable=amountValid&&limitValid&&freight;};
  quantity.onValueChanged.AddListener(value=>{_tradeAmount=value;refresh();});limit.onValueChanged.AddListener(value=>{_tradeLimit=value;refresh();});
  submit=OperatingButton(editor,"创建"+(_tradeSide=="buy"?"买入":"卖出")+Resource(_tradeResource)+"限价订单",()=>{if(!double.TryParse(_tradeAmount,NumberStyles.Float,CultureInfo.InvariantCulture,out var amount)||!double.TryParse(_tradeLimit,NumberStyles.Float,CultureInfo.InvariantCulture,out var limitPrice))return;Command(g=>Market.order(g,_tradeResource,_tradeSide,amount,limitPrice,TradeCounterparty));},true);refresh();
  Label(editor,"下单时不预留库存或通货；实际成交才收付。负报价可使支付方向改变。订单按稳定顺序共享在线运力，本地加速不扩大运力。",13,Muted);
  var freightCapacity=Market._total_capacity(G);var freightSection=OperatingSection(_body,"货运能力",F(freightCapacity)+" 单位 / 现实在线小时");var ledger=S["block"].Get("trade_ledger",V.Arr());if(ledger.Count>0){var last=ledger[-1];OperatingMeter(freightSection,"material","最近货运周期使用",last.Get("used_capacity",0).Double,last.Get("capacity",0).Double,"单位","只表示该实际周期，不与每小时能力混用。",Blue);}else Label(freightSection,"尚无实际成交周期。",14,Muted);
  var orders=S["block"].Get("trade_orders",V.Arr()).Where(order=>order.Get("status","").Text=="open"||order.Get("status","").Text=="partial").ToArray();var pending=OperatingSection(_body,"待成交订单",orders.Length+" 笔");
  foreach(var order in orders){var id=order["id"].Text;var card=OperatingCard(pending,"Open order "+id);var heading=Row(card,"Order title",30*TextScale);Label(heading,(order["side"].Text=="buy"?"买入 ":"卖出 ")+Resource(order["resource"].Text),15,Ink,30*TextScale);OperatingButton(heading,"撤单",()=>Command(g=>Market.cancel(g,id)));OperatingMeter(card,order["resource"].Text,"成交进度",order["amount"].Double-order["remaining"].Double,order["amount"].Double,"单位",id+" · 剩余 "+F(order["remaining"].Double)+" · 限价 "+F(order["limit_price"].Double,3),Blue);}
  if(orders.Length==0)Label(pending,"暂无待成交订单。",14,Muted);
  OperatingButton(_body,"完整市场订单、成交与来源详表 →",()=>OpenAdvancedWorkspace("market",_tradeResource),true);WorkspaceActions("资源与财政账目",()=>OpenAdvancedWorkspace("resources",_tradeResource));
 }

}
}
