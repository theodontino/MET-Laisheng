using System;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City {
/// <summary>Original, atlas-free vector language for the command HUD.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public abstract class CityHudMesh : MaskableGraphic {
 protected VertexHelper Mesh;
 protected Rect Bounds;
 protected float Unit;
 protected override void Awake(){base.Awake();raycastTarget=false;}
 protected override void OnPopulateMesh(VertexHelper vh){
  vh.Clear();Mesh=vh;Bounds=GetPixelAdjustedRect();Unit=Mathf.Min(Bounds.width,Bounds.height);
  if(Unit>0)Draw();Mesh=null;
 }
 protected abstract void Draw();
 protected Vector2 P(float x,float y)=>new Vector2(Bounds.xMin+x*Bounds.width,Bounds.yMin+y*Bounds.height);
 protected void Poly(params Vector2[] points){
  if(points.Length<3)return;
  int start=Mesh.currentVertCount;
  for(int i=0;i<points.Length;i++){var v=UIVertex.simpleVert;v.color=color;v.position=P(points[i].x,points[i].y);Mesh.AddVert(v);}
  for(int i=1;i<points.Length-1;i++)Mesh.AddTriangle(start,start+i,start+i+1);
 }
 protected void Box(float x0,float y0,float x1,float y1)=>Poly(new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1));
 protected void Line(float x0,float y0,float x1,float y1,float pixels=1.8f){
  var a=P(x0,y0);var b=P(x1,y1);var d=b-a;
  if(d.sqrMagnitude<.001f)return;
  var n=new Vector2(-d.y,d.x).normalized*pixels*.5f;
  int start=Mesh.currentVertCount;
  foreach(var p in new[]{a-n,a+n,b+n,b-n}){var v=UIVertex.simpleVert;v.color=color;v.position=p;Mesh.AddVert(v);}
  Mesh.AddTriangle(start,start+1,start+2);Mesh.AddTriangle(start,start+2,start+3);
 }
 protected void Ring(float x,float y,float radius,float pixels=1.8f,int count=24){
  for(int i=0;i<count;i++){
   float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
   Line(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius,pixels);
  }
 }
 protected void Disc(float x,float y,float radius,int count=20){
  for(int i=0;i<count;i++){
   float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
   Poly(new Vector2(x,y),new Vector2(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius),new Vector2(x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius));
  }
 }
 protected void Path(float pixels,params Vector2[] points){for(int i=1;i<points.Length;i++)Line(points[i-1].x,points[i-1].y,points[i].x,points[i].y,pixels);}
 protected static Vector2 V(float x,float y)=>new Vector2(x,y);
}

/// <summary>One-pixel panel border and short cyan datum; never intercepts pointer input.</summary>
public sealed class CityHudFrame : CityHudMesh {
 public bool ShowAccent;
 public bool DividerOnly;
 protected override void Draw(){
  if(DividerOnly){Line(.985f,.12f,.985f,.88f,1);return;}
  float x=1f/Mathf.Max(1,Bounds.width),y=1f/Mathf.Max(1,Bounds.height);
  Line(x,y,1-x,y,1);Line(1-x,y,1-x,1-y,1);Line(1-x,1-y,x,1-y,1);Line(x,1-y,x,y,1);
  if(ShowAccent){Box(x,.68f,x+Mathf.Max(2f/Mathf.Max(1,Bounds.width),.005f),.94f);Line(.045f,1-y,.16f,1-y,2);}
 }
}

/// <summary>Consistent 24-unit silhouettes and strokes. Kind is a semantic ID.</summary>
public sealed class CityHudGlyph : CityHudMesh {
 public string Kind="info";
 protected override void Draw(){
  switch((Kind??"info").ToLowerInvariant()){
   case "dt": Poly(V(.5f,.07f),V(.85f,.27f),V(.85f,.72f),V(.5f,.92f),V(.15f,.72f),V(.15f,.27f));Line(.5f,.13f,.5f,.84f,1.4f);Line(.18f,.31f,.5f,.49f,1.4f);Line(.82f,.31f,.5f,.49f,1.4f);break;
   case "population": case "employment":
    Disc(.5f,.69f,.13f);Disc(.22f,.61f,.10f);Disc(.78f,.61f,.10f);Box(.35f,.19f,.65f,.49f);Box(.10f,.18f,.29f,.43f);Box(.71f,.18f,.90f,.43f);
    if(string.Equals(Kind,"employment",StringComparison.OrdinalIgnoreCase)){Poly(V(.46f,.48f),V(.54f,.48f),V(.5f,.25f));}break;
   case "food":
    Line(.47f,.14f,.55f,.86f,2);for(int i=0;i<3;i++){float y=.33f+i*.17f;Poly(V(.5f,y),V(.18f,y+.07f),V(.45f,y+.18f));Poly(V(.52f,y+.06f),V(.82f,y+.15f),V(.58f,y+.22f));}break;
   case "material":
    Line(.17f,.26f,.73f,.74f,6);Line(.30f,.20f,.85f,.67f,6);Disc(.17f,.26f,.10f);Disc(.30f,.20f,.10f);Ring(.17f,.26f,.065f,1);break;
   case "goods":
    Ring(.5f,.5f,.27f,3);Disc(.5f,.5f,.09f);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(.5f+Mathf.Cos(a)*.27f,.5f+Mathf.Sin(a)*.27f,.5f+Mathf.Cos(a)*.40f,.5f+Mathf.Sin(a)*.40f,4);}break;
   case "currency":
    for(int j=0;j<3;j++){float y=.22f+j*.18f;Box(.22f,y,.79f,y+.12f);Line(.22f,y+.12f,.79f,y+.12f,1);}break;
   case "energy": case "heat":
    if(Kind=="energy")Poly(V(.59f,.92f),V(.23f,.43f),V(.47f,.43f),V(.40f,.08f),V(.79f,.58f),V(.55f,.58f));
    else for(int i=0;i<3;i++){float x=.25f+i*.25f;Path(2,V(x,.15f),V(x+.06f,.34f),V(x-.02f,.52f),V(x+.06f,.72f),V(x,.88f));}break;
   case "bonus":
    Poly(V(.5f,.89f),V(.87f,.69f),V(.5f,.49f),V(.13f,.69f));Path(2,V(.13f,.51f),V(.5f,.31f),V(.87f,.51f));Path(2,V(.13f,.32f),V(.5f,.12f),V(.87f,.32f));break;
   case "city":
    Poly(V(.10f,.48f),V(.5f,.88f),V(.90f,.48f),V(.80f,.48f),V(.80f,.14f),V(.20f,.14f),V(.20f,.48f));Box(.44f,.14f,.58f,.42f);break;
   case "build":
    Poly(V(.18f,.70f),V(.46f,.87f),V(.68f,.67f),V(.58f,.54f),V(.42f,.64f));Line(.55f,.58f,.82f,.19f,5);break;
   case "license": case "task":
    Path(2,V(.27f,.13f),V(.27f,.89f),V(.67f,.89f),V(.78f,.78f),V(.78f,.13f),V(.27f,.13f));
    if(Kind=="task"){for(int i=0;i<2;i++){float y=.62f-i*.22f;Path(2,V(.34f,y),V(.40f,y-.06f),V(.49f,y+.07f));Line(.55f,y,.70f,y,1.6f);}}
    else{Line(.37f,.62f,.67f,.62f,1.6f);Line(.37f,.49f,.65f,.49f,1.6f);Line(.37f,.36f,.58f,.36f,1.6f);}break;
   case "terrain":
    Poly(V(.10f,.19f),V(.35f,.78f),V(.52f,.42f),V(.67f,.68f),V(.91f,.19f));Line(.29f,.64f,.39f,.64f,1.4f);break;
   case "service": case "medical":
    Box(.40f,.13f,.60f,.87f);Box(.13f,.40f,.87f,.60f);break;
   case "education":
    Path(2,V(.12f,.18f),V(.12f,.80f),V(.50f,.70f),V(.88f,.80f),V(.88f,.18f),V(.50f,.10f),V(.12f,.18f));Line(.50f,.10f,.50f,.70f,2);break;
   case "entertainment":
    Poly(V(.50f,.92f),V(.62f,.64f),V(.92f,.61f),V(.69f,.40f),V(.76f,.10f),V(.50f,.26f),V(.24f,.10f),V(.31f,.40f),V(.08f,.61f),V(.38f,.64f));break;
   case "commercial":
    Path(2,V(.16f,.15f),V(.16f,.64f),V(.84f,.64f),V(.84f,.15f),V(.16f,.15f));Path(2,V(.34f,.64f),V(.34f,.85f),V(.66f,.85f),V(.66f,.64f));break;
   case "pollution":
    Disc(.33f,.43f,.16f);Disc(.50f,.60f,.21f);Disc(.70f,.43f,.17f);Box(.24f,.27f,.78f,.43f);break;
   case "stock":
    Poly(V(.50f,.88f),V(.85f,.69f),V(.50f,.50f),V(.15f,.69f));Poly(V(.15f,.65f),V(.50f,.46f),V(.50f,.12f),V(.15f,.32f));Poly(V(.85f,.65f),V(.50f,.46f),V(.50f,.12f),V(.85f,.32f));break;
   case "close":Line(.22f,.22f,.78f,.78f,2.5f);Line(.22f,.78f,.78f,.22f,2.5f);break;
   case "pause":Box(.28f,.19f,.43f,.81f);Box(.57f,.19f,.72f,.81f);break;
   case "play":Poly(V(.31f,.17f),V(.79f,.50f),V(.31f,.83f));break;
   case "speed":Poly(V(.14f,.20f),V(.52f,.50f),V(.14f,.80f));Poly(V(.48f,.20f),V(.87f,.50f),V(.48f,.80f));break;
   case "chevron":Path(2.5f,V(.36f,.19f),V(.65f,.50f),V(.36f,.81f));break;
   case "info": default:Ring(.5f,.5f,.36f,2);Disc(.5f,.70f,.04f);Box(.46f,.27f,.54f,.60f);break;
  }
 }
}

public sealed partial class CityInterface {
 public RectTransform HudIcon(Transform parent,string kind,Color color,float size=24){
  var node=Node("HUD icon "+kind,parent);
  node.sizeDelta=new Vector2(size,size);
  var layout=node.gameObject.AddComponent<LayoutElement>();
  layout.minWidth=layout.preferredWidth=size;layout.minHeight=layout.preferredHeight=size;
  layout.flexibleWidth=layout.flexibleHeight=0;
  var glyph=node.gameObject.AddComponent<CityHudGlyph>();glyph.Kind=kind;glyph.color=color;glyph.raycastTarget=false;
  return node;
 }
}
}
