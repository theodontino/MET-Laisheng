using System.Collections.Generic;
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

/// <summary>Tinted original icon textures in a centered square. Kind is a semantic ID.</summary>
public sealed class CityHudGlyph : CityHudMesh {
 string _kind="info";
 public string Kind { get=>_kind;set {if(_kind==value)return;_kind=value;SetAllDirty();} }
 static readonly Dictionary<string,Texture2D> Textures=new Dictionary<string,Texture2D>();
 Texture2D IconTexture {
  get {
   string id=CityIconCatalog.Resolve(Kind);
   var texture=LoadIcon(id);
   return texture!=null||id=="info"?texture:LoadIcon("info");
  }
 }
 static Texture2D LoadIcon(string id){
  if(!Textures.TryGetValue(id,out var texture)){
   texture=Resources.Load<Texture2D>("CityIcons/"+id);
   Textures[id]=texture;
   if(texture==null)Debug.LogWarning("MET city icon missing: CityIcons/"+id);
  }
  return texture;
 }
 public override Texture mainTexture=>IconTexture??Texture2D.whiteTexture;
 protected override void Draw(){
  // A layout may give the icon extra width; artwork always uses a centered square.
  Bounds=new Rect(Bounds.center-Vector2.one*Unit*.5f,Vector2.one*Unit);
  if(IconTexture!=null){
   var uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
   for(int i=0;i<uv.Length;i++){var v=UIVertex.simpleVert;v.color=color;v.position=P(uv[i].x,uv[i].y);v.uv0=uv[i];Mesh.AddVert(v);}
   Mesh.AddTriangle(0,1,2);Mesh.AddTriangle(0,2,3);return;
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
