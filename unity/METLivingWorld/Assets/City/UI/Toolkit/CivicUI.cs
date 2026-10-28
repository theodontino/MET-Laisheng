using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MET.City {
// Shared presentation vocabulary. Rules and commands belong to CityInterface/Core.
public static class CivicUI {
 public static VisualElement Box(VisualElement parent,string classes="") { var v=new VisualElement(); Classes(v,classes);parent.Add(v);return v; }
 public static VisualElement Row(VisualElement parent,string classes="")=>Box(parent,"c-row "+classes);
 public static VisualElement Column(VisualElement parent,string classes="")=>Box(parent,"c-column "+classes);
 public static Label Label(VisualElement parent,string text,string classes="") {var v=new Label(text??"");Classes(v,"c-text "+classes);parent.Add(v);return v;}
 public static Button Button(VisualElement parent,string text,Action action,bool primary=false,bool enabled=true,string name="") {
  var b=new Button(action){text=text??"",name=name};Classes(b,"c-button"+(primary?" is-primary":""));b.SetEnabled(enabled);parent.Add(b);return b;
 }
 public static VisualElement Section(VisualElement parent,string title,string hint="") {var section=Column(parent,"c-section");var head=Row(section,"c-section-head");Label(head,title,"c-heading");if(hint!="")Label(head,hint,"c-small c-muted c-align-right");return section;}
 public static VisualElement Value(VisualElement parent,string title,string value,string hint="",string tone="") {var v=Column(parent,"c-value "+tone);Label(v,title,"c-small c-muted");Label(v,value,"c-number");if(hint!="")Label(v,hint,"c-small c-muted");return v;}
 public static VisualElement Meter(VisualElement parent,string title,double value,double capacity,string unit="",string hint="",string tone="") {
  var v=Column(parent,"c-meter "+tone);var head=Row(v,"c-meter-head");Label(head,title,"c-small");Label(head,value.ToString("0.#")+" / "+capacity.ToString("0.#")+(unit==""?"":" "+unit),"c-metric c-align-right");
  var track=Box(v,"c-track");var fill=Box(track,"c-fill");fill.style.width=Length.Percent((float)Math.Max(0,Math.Min(1,capacity>0?value/capacity:0))*100);if(hint!="")Label(v,hint,"c-small c-muted");return v;
 }
 public static VisualElement Progress(VisualElement parent,double ratio,string tone="") {var track=Box(parent,"c-track "+tone);var fill=Box(track,"c-fill");fill.style.width=Length.Percent((float)Math.Max(0,Math.Min(1,ratio))*100);return track;}
 sealed class LazyFoldout : Foldout {
  public Action<VisualElement> RenderContent;bool built;
  public void Populate(){if(built)return;built=true;RenderContent(contentContainer);}
 }
 public static Foldout Fold(VisualElement parent,string title,Action<VisualElement> render,bool open=false) {var f=new LazyFoldout{text=title,RenderContent=render};f.AddToClassList("c-fold");parent.Add(f);f.RegisterValueChangedCallback(e=>{if(e.target==f&&e.newValue)f.Populate();});RestoreFold(f,open);return f;}
 public static void RestoreFold(Foldout fold,bool open){
  // Change events may be queued behind a navigation click. Build synchronously
  // so the view can restore nested records before layout restores scroll offset.
  if(fold is LazyFoldout lazy){if(open)lazy.Populate();fold.SetValueWithoutNotify(open);}else fold.value=open;
 }
 public static TextField Field(VisualElement parent,string label,string value,Action<string> changed=null) {var f=new TextField(label){value=value??""};f.AddToClassList("c-field");if(changed!=null)f.RegisterValueChangedCallback(e=>changed(e.newValue));parent.Add(f);return f;}
 public static DropdownField Select(VisualElement parent,string label,List<string> options,int selected,Action<int> changed) {var f=new DropdownField(label,options,Mathf.Clamp(selected,0,Math.Max(0,options.Count-1)));f.AddToClassList("c-field");f.RegisterValueChangedCallback(e=>changed(f.index));parent.Add(f);return f;}
 public static Toggle Toggle(VisualElement parent,string label,bool value,Action<bool> changed) {var f=new Toggle(label){value=value};f.AddToClassList("c-toggle");f.RegisterValueChangedCallback(e=>changed(e.newValue));parent.Add(f);return f;}
 public static VisualElement Choice(VisualElement parent,string title,string note,bool selected,Action action,string icon="") {var b=Button(parent,"",action);b.AddToClassList("c-choice");b.EnableInClassList("is-selected",selected);if(icon!="")Icon(b,icon);Label(b,title,"c-heading");if(note!="")Label(b,note,"c-small c-muted");return b;}
 public static void Empty(VisualElement parent,string title,string description="") {var e=Column(parent,"c-empty");Label(e,title,"c-heading");if(description!="")Label(e,description,"c-muted");}
 public static VisualElement Pair(VisualElement parent,string label,string value,string tone="") {var r=Row(parent,"c-pair");Label(r,label,"c-muted");Label(r,value,"c-align-right "+tone);return r;}
 public static VisualElement Table(VisualElement parent,string[] headings,IEnumerable<string[]> rows) {var table=Column(parent,"c-table");TableRow(table,headings,true);foreach(var row in rows)TableRow(table,row);return table;}
 public static VisualElement TableRow(VisualElement parent,string[] cells,bool header=false) {var r=Row(parent,"c-table-row"+(header?" c-table-head":""));foreach(var cell in cells)Label(r,cell,"c-table-cell");return r;}
 public static VisualElement Icon(VisualElement parent,string id,string classes="") {var e=Box(parent,"c-icon "+classes);var t=Resources.Load<Texture2D>("CityIcons/"+CityIconCatalog.Resolve(id));if(t!=null)e.style.backgroundImage=new StyleBackground(t);e.pickingMode=PickingMode.Ignore;return e;}
 public static void Classes(VisualElement e,string classes) {foreach(var c in classes.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries))e.AddToClassList(c);}
}
}
