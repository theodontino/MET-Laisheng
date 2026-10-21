using UnityEngine;
using UnityEngine.UI;
namespace MET.City {
/// <summary>Original vector marks and measured charts; no gameplay access.</summary>
public sealed class CityGraphic : MaskableGraphic {
 public string Kind="building";public float Ratio=1;
 static readonly Color Muted=new Color(.24f,.34f,.33f,1);
 void Quad(VertexHelper vh,float x,float y,float w,float h,Color c){int n=vh.currentVertCount;vh.AddVert(new Vector3(x,y),c,new Vector2(0,0));vh.AddVert(new Vector3(x,y+h),c,new Vector2(0,1));vh.AddVert(new Vector3(x+w,y+h),c,new Vector2(1,1));vh.AddVert(new Vector3(x+w,y),c,new Vector2(1,0));vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
 void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color ink){int n=vh.currentVertCount;vh.AddVert(a,ink,Vector2.zero);vh.AddVert(b,ink,Vector2.zero);vh.AddVert(c,ink,Vector2.zero);vh.AddTriangle(n,n+1,n+2);}
 protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();var r=rectTransform.rect;float w=r.width,h=r.height,x=r.x,y=r.y;
  if(Kind=="bar"){Quad(vh,x,y,w,h,Muted);Quad(vh,x,y,w*Mathf.Clamp01(Ratio),h,color);return;}
  if(Kind=="ring"){float radius=Mathf.Min(w,h)*.42f;var center=r.center;int count=60;for(int i=0;i<count;i++){float t=i/(float)count,t1=(i+1)/(float)count;var a=new Vector2(Mathf.Cos(t*Mathf.PI*2),Mathf.Sin(t*Mathf.PI*2));var b=new Vector2(Mathf.Cos(t1*Mathf.PI*2),Mathf.Sin(t1*Mathf.PI*2));var ink=t<=Mathf.Clamp01(Ratio)?color:Muted;Triangle(vh,center+a*radius,center+b*radius,center+b*(radius-7),ink);Triangle(vh,center+a*radius,center+b*(radius-7),center+a*(radius-7),ink);}return;}
  // A site marker combines an original hex crest with a simple functional silhouette.
  var c=r.center;float q=Mathf.Min(w,h)*.45f;for(int i=0;i<6;i++){float a=(i*60+30)*Mathf.Deg2Rad,b=((i+1)*60+30)*Mathf.Deg2Rad;Triangle(vh,c,c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*q,c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*q,new Color(color.r*.28f,color.g*.28f,color.b*.28f,.9f));}
  Quad(vh,c.x-q*.55f,c.y-q*.35f,q*1.1f,q*.65f,color);Triangle(vh,new Vector2(c.x-q*.67f,c.y+q*.3f),new Vector2(c.x,c.y+q*.75f),new Vector2(c.x+q*.67f,c.y+q*.3f),color);
  Quad(vh,c.x-q*.13f,c.y-q*.35f,q*.26f,q*.43f,new Color(.08f,.13f,.13f,1));if(Kind=="industrial"){Quad(vh,c.x+q*.28f,c.y+q*.3f,q*.16f,q*.55f,color);}else if(Kind=="public"){Quad(vh,c.x-q*.1f,c.y+q*.33f,q*.2f,q*.3f,color);}
 }
}
}
