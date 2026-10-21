using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City
{
    /// <summary>Small cached, deterministic desktop materials. All textures belong to presentation only.</summary>
    public static class CityDeskMaterials
    {
        private const int Size = 128;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Use paper, folder, leather, metal, or blueprint. Image.color remains available for tinting.</summary>
        public static void Apply(Image image, string kind)
        {
            if (image == null) return;
            image.sprite = Get(kind);
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.preserveAspect = false;
        }

        public static Sprite Get(string kind)
        {
            kind = string.IsNullOrEmpty(kind) ? "paper" : kind.ToLowerInvariant();
            if (kind != "paper" && kind != "folder" && kind != "leather" && kind != "metal" && kind != "blueprint" && kind != "label") kind = "paper";
            Sprite sprite;
            if (Cache.TryGetValue(kind, out sprite) && sprite != null) return sprite;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.name = "MET desk " + kind;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++) pixels[y * Size + x] = Pixel(kind, x, y);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
            sprite.name = "MET desk " + kind;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[kind] = sprite;
            return sprite;
        }

        // Integer hash avoids UnityEngine.Random and never touches the simulation's random stream.
        private static float Noise(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 65535u) / 65535f;
            }
        }
        private static Color Pixel(string kind, int x, int y)
        {
            float grain = Noise(x, y, 17) - .5f;
            float broad = Noise(x / 7, y / 5, 41) - .5f;
            float edge = Mathf.Min(Mathf.Min(x, Size - 1 - x), Mathf.Min(y, Size - 1 - y));
            Color c;
            if (kind == "leather")
            {
                c = new Color(.25f, .18f, .13f);
                float pore = Noise(x / 2, y / 2, 9);
                c *= 1f + grain * .14f + broad * .08f - (pore > .85f ? .08f : 0f);
                if (edge < 9f) c *= .7f + edge * .026f;
                // Recessed piping and warm thread stitches along all four edges.
                if (edge >= 8f && edge < 10f) c *= .58f;
                if (edge >= 11f && edge < 12f && (((x + y) / 4) % 2 == 0)) c = Color.Lerp(c, new Color(.67f, .48f, .29f), .7f);
            }
            else if (kind == "label")
            {
                c = new Color(.86f,.82f,.71f);
                c *= 1f + grain * .012f;
                if(edge<2)c *= .66f;
                else if(edge<3)c=Color.Lerp(c,Color.white,.45f);
                else if(edge<5)c *= .96f;
            }
            else if (kind == "metal")
            {
                float brush = (Noise(0, y, 5) - .5f) * .035f;
                float shine = .08f * Mathf.Sin(y * .045f + .4f);
                c = new Color(.60f + shine + brush, .59f + shine + brush, .53f + shine + brush);
                c *= 1f + grain * .025f;
                if (edge < 3f) c *= .60f;
                else if (edge < 5f) c = Color.Lerp(c, new Color(.91f, .89f, .79f), .72f);
                else if (edge < 7f) c *= .83f;
                // Recessed screw heads live inside the non-stretching corners.
                if (Screw(x, y, 11, 11) || Screw(x, y, 116, 11) || Screw(x, y, 11, 116) || Screw(x, y, 116, 116)) c *= .53f;
            }
            else
            {
                c = kind == "folder" ? new Color(.78f, .70f, .55f) : kind == "blueprint" ? new Color(.19f, .34f, .40f) : new Color(.91f, .87f, .75f);
                // Nine-slice stretches the central 96px area: keep paper grain subdued so
                // large panels read as fibres instead of magnified patches.
                c *= 1f + grain * .022f + broad * .006f;
                // Sparse horizontal and diagonal cellulose fibres remain visible when enlarged.
                if (Noise(x / 6, y, 28) > .94f) c *= .989f;
                if (Noise(x + y / 3, y / 8, 64) > .97f) c = Color.Lerp(c, Color.white, .012f);
                float irregular = Noise(x / 3, y / 3, 11) * 1.1f;
                if (edge < 2f + irregular) c *= .64f;
                else if (edge < 4f) c *= .83f;
                else if (edge < 5f) c = Color.Lerp(c, Color.white, .15f);
                // Thin backing sheet and inset pressure marks suggest stacked stationery.
                if ((y == 7 || x == 7) && edge >= 6) c *= .84f;
                if (kind == "folder")
                {
                    if (y >= 12 && y <= 14) c *= .80f;
                    if (y == 15) c = Color.Lerp(c, Color.white, .18f);
                    if (x >= 8 && x <= 10) c *= .91f;
                }
                else if (kind == "paper")
                {
                    // A folded upper-right corner, entirely inside the fixed 16px slice.
                    int corner = (Size - 1 - x) + (Size - 1 - y);
                    if (corner < 15)
                    {
                        c = new Color(.77f, .72f, .59f);
                        if (corner > 12) c *= .66f;
                        else c = Color.Lerp(c, new Color(.98f, .95f, .85f), .6f);
                    }
                }
                else if (kind == "blueprint")
                {
                    if (x % 16 == 0 || y % 16 == 0) c = Color.Lerp(c, new Color(.66f, .80f, .79f), .028f);
                }
            }
            // The outer fringe lets the underlying surface show at tiny irregularities.
            c.a = edge < 1 ? .38f + Noise(x, y, 99) * .35f : edge < 2 ? .88f : 1f;
            return c;
        }
        private static bool Screw(int x, int y, int cx, int cy)
        {
            int dx = x - cx, dy = y - cy;
            int radius = dx * dx + dy * dy;
            return (radius >= 7 && radius <= 12) || (Math.Abs(dy) <= 0 && Math.Abs(dx) <= 2);
        }
    }

    /// <summary>Native lightweight desk objects. Kind: pin, clip, bucket, roll, stamp. Keep raycastTarget off for decorative use.</summary>
    [AddComponentMenu("MET/City/Desk Prop")]
    public class CityDeskProp : MaskableGraphic
    {
        public string Kind = "pin";
        private VertexHelper mesh;
        private Rect bounds;
        private string previousKind;
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        private void LateUpdate() { if (previousKind != Kind) { previousKind = Kind; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); mesh = vh; bounds = GetPixelAdjustedRect();
            string kind = string.IsNullOrEmpty(Kind) ? "pin" : Kind.ToLowerInvariant();
            if (kind == "bucket") Bucket();
            else if (kind == "clip") Clip();
            else if (kind == "roll") HorizontalRoll();
            else if (kind == "stamp") Stamp();
            else Pin();
            mesh = null;
        }
        private void HorizontalRoll()
        {
            Oval(.51f, .27f, .43f, .14f, C(.10f,.08f,.04f,.22f));
            // Parallel strips form a broad paper cylinder, with a lit crown and shaded underside.
            const int strips = 22;
            for (int i = 0; i < strips; i++)
            {
                float t = i / (float)strips;
                float top = (i + 1) / (float)strips;
                float light = Mathf.Sin(t * Mathf.PI) * .18f;
                Color tone = C(.69f + light, .65f + light, .50f + light);
                if (t > .55f) tone = Color.Lerp(tone, C(.97f,.94f,.79f), (t-.55f)*.9f);
                Quad(.12f, .29f+t*.43f, .87f, .29f+top*.43f, tone);
            }
            Stroke(.16f,.64f,.84f,.64f,.012f,C(.99f,.96f,.82f,.72f));
            Stroke(.14f,.32f,.86f,.32f,.012f,C(.49f,.45f,.32f,.72f));
            // Elliptical end grain and three nested paper rings make the rolled form legible at flight size.
            Oval(.12f,.505f,.047f,.215f,C(.59f,.55f,.41f));
            Oval(.114f,.514f,.040f,.192f,C(.93f,.89f,.73f));
            Oval(.114f,.514f,.027f,.145f,C(.68f,.63f,.47f));
            Oval(.112f,.520f,.021f,.119f,C(.96f,.92f,.77f));
            Oval(.111f,.522f,.011f,.077f,C(.33f,.35f,.28f));
            Oval(.87f,.505f,.048f,.215f,C(.98f,.94f,.79f));
            Oval(.876f,.505f,.035f,.172f,C(.67f,.63f,.47f));
            Oval(.878f,.511f,.028f,.147f,C(.94f,.89f,.72f));
            Oval(.882f,.512f,.016f,.102f,C(.39f,.42f,.33f));
            Oval(.888f,.521f,.008f,.071f,C(.18f,.24f,.22f));
            // Blueprint-blue paper band follows the same cylinder lighting.
            Quad(.455f,.285f,.565f,.725f,C(.23f,.39f,.43f));
            Quad(.455f,.55f,.565f,.68f,C(.39f,.55f,.57f));
            Quad(.455f,.29f,.565f,.36f,C(.14f,.28f,.32f));
            Stroke(.468f,.34f,.468f,.67f,.008f,C(.76f,.81f,.71f,.6f));
            Stroke(.551f,.34f,.551f,.67f,.008f,C(.10f,.23f,.27f,.7f));
            Stroke(.485f,.46f,.538f,.46f,.009f,C(.82f,.86f,.75f,.78f));
            Stroke(.485f,.50f,.538f,.50f,.009f,C(.82f,.86f,.75f,.78f));
        }
        private void Stamp()
        {
            Color red = C(.61f,.20f,.13f,.86f);
            // Two independently distressed outlines; empty centre is reserved for caller-supplied text.
            StampEdge(P(.04f,.12f),P(.96f,.12f),.026f,red,1);
            StampEdge(P(.96f,.12f),P(.96f,.88f),.026f,red,2);
            StampEdge(P(.96f,.88f),P(.04f,.88f),.026f,red,3);
            StampEdge(P(.04f,.88f),P(.04f,.12f),.026f,red,4);
            Color inner = C(.64f,.22f,.14f,.77f);
            StampEdge(P(.075f,.19f),P(.925f,.19f),.011f,inner,5);
            StampEdge(P(.925f,.19f),P(.925f,.81f),.011f,inner,6);
            StampEdge(P(.925f,.81f),P(.075f,.81f),.011f,inner,7);
            StampEdge(P(.075f,.81f),P(.075f,.19f),.011f,inner,8);
            // Deterministic dry-ink flecks remain near the border so they do not obscure lettering.
            for(int i=0;i<24;i++)
            {
                float x=.09f+((i*37)%83)/100f;
                float y=i%2==0?.155f:.845f;
                Quad(x,y,x+.006f,y+.012f,C(.65f,.24f,.16f,.20f+(i%4)*.07f));
            }
        }
        private void StampEdge(Vector2 a,Vector2 b,float width,Color ink,int seed)
        {
            const int segments=35;
            for(int i=0;i<segments;i++)
            {
                int wear=(i*17+seed*11)%31;
                if(wear==0 || wear==3 || wear==8) continue;
                Vector2 from=Vector2.Lerp(a,b,i/(float)segments);
                Vector2 to=Vector2.Lerp(a,b,(i+.94f)/segments);
                Color faded=ink; faded.a*=.60f+(wear%7)*.06f;
                Stroke(from.x,from.y,to.x,to.y,width*(.80f+(wear%3)*.1f),faded);
            }
        }
        private void Pin()
        {
            Oval(.54f, .35f, .27f, .13f, C(.10f, .08f, .05f, .23f));
            Stroke(.49f, .46f, .42f, .12f, .045f, C(.37f, .36f, .30f));
            Stroke(.49f, .46f, .43f, .14f, .015f, C(.83f, .81f, .70f));
            Oval(.48f, .51f, .27f, .15f, C(.30f, .12f, .07f));
            Oval(.46f, .58f, .25f, .19f, C(.65f, .28f, .16f));
            Oval(.44f, .63f, .22f, .16f, C(.77f, .39f, .22f));
            Oval(.38f, .68f, .085f, .045f, C(.96f, .71f, .45f, .72f));
            Oval(.49f, .46f, .13f, .045f, C(.24f, .12f, .08f, .65f));
        }
        private void Clip()
        {
            Quad(.18f, .11f, .86f, .27f, C(.10f, .08f, .06f, .25f));
            Poly(new[] { P(.16f,.20f), P(.84f,.20f), P(.73f,.61f), P(.27f,.61f) }, C(.24f,.25f,.23f));
            Quad(.19f, .21f, .81f, .25f, C(.57f,.57f,.50f));
            Quad(.27f, .56f, .73f, .62f, C(.46f,.47f,.42f));
            // Bent steel wire handles, with darker underwire and an offset highlight.
            Wire(.34f, .54f, .29f, .88f); Wire(.29f, .88f, .38f, .95f);
            Wire(.38f, .95f, .62f, .95f); Wire(.62f, .95f, .71f, .88f); Wire(.71f, .88f, .66f, .54f);
            Wire(.33f, .24f, .27f, .06f); Wire(.27f, .06f, .73f, .06f); Wire(.73f, .06f, .67f, .24f);
        }
        private void Bucket()
        {
            Oval(.56f, .10f, .36f, .075f, C(.08f,.07f,.05f,.3f));
            // Separate paper rolls sit behind the rim and preserve a clear paper interior.
            Roll(.30f, .46f, .11f, .50f, -.06f);
            Roll(.56f, .48f, .12f, .46f, .08f);
            Roll(.45f, .50f, .10f, .37f, -.02f);
            Poly(new[] { P(.18f,.57f), P(.82f,.57f), P(.75f,.13f), P(.65f,.075f), P(.34f,.075f), P(.25f,.13f) }, C(.39f,.41f,.36f));
            Poly(new[] { P(.20f,.55f), P(.38f,.53f), P(.39f,.10f), P(.28f,.14f) }, C(.48f,.49f,.41f));
            Poly(new[] { P(.65f,.54f), P(.80f,.55f), P(.73f,.14f), P(.65f,.10f) }, C(.26f,.29f,.26f));
            for (int i = 0; i < 6; i++) Stroke(.30f + i * .08f, .17f, .28f + i * .088f, .47f, .009f, C(.23f,.27f,.23f,.50f));
            Oval(.50f,.57f,.33f,.085f,C(.64f,.65f,.55f));
            Oval(.50f,.58f,.285f,.057f,C(.14f,.19f,.17f));
            // Visible short roll necks restore overlap in front of the dark bucket opening.
            Roll(.32f,.55f,.095f,.18f,-.02f);
            Roll(.58f,.57f,.11f,.17f,.02f);
            Stroke(.22f,.55f,.27f,.18f,.012f,C(.72f,.71f,.59f,.8f));
            Quad(.37f,.27f,.63f,.37f,C(.67f,.62f,.46f));
            Stroke(.41f,.29f,.41f,.35f,.009f,C(.37f,.38f,.31f));
            Stroke(.45f,.29f,.45f,.35f,.009f,C(.37f,.38f,.31f));
            Stroke(.49f,.29f,.58f,.35f,.009f,C(.37f,.38f,.31f));
        }
        private void Roll(float x, float y, float width, float height, float lean)
        {
            Poly(new[] { P(x-width*.5f,y), P(x+width*.5f,y), P(x+lean+width*.5f,y+height), P(x+lean-width*.5f,y+height) }, C(.83f,.80f,.65f));
            Stroke(x-width*.3f,y+.01f,x+lean-width*.3f,y+height,.012f,C(.98f,.94f,.79f));
            Stroke(x+width*.37f,y+.01f,x+lean+width*.37f,y+height,.014f,C(.49f,.57f,.53f));
            Oval(x+lean,y+height,width*.5f,width*.22f,C(.98f,.94f,.81f));
            Oval(x+lean,y+height,width*.23f,width*.105f,C(.40f,.43f,.35f));
            Stroke(x-width*.45f,y+height*.57f,x+width*.45f,y+height*.57f,.008f,C(.40f,.55f,.57f,.6f));
        }
        private void Wire(float x1,float y1,float x2,float y2)
        {
            Stroke(x1,y1,x2,y2,.035f,C(.19f,.20f,.18f));
            Stroke(x1-.006f,y1+.006f,x2-.006f,y2+.006f,.018f,C(.80f,.80f,.70f));
        }
        private static Color C(float r,float g,float b,float a=1f) { return new Color(r,g,b,a); }
        private static Vector2 P(float x,float y) { return new Vector2(x,y); }
        private Vector2 Map(Vector2 p) { return new Vector2(bounds.xMin+p.x*bounds.width,bounds.yMin+p.y*bounds.height); }
        private void Quad(float left,float bottom,float right,float top,Color c) { Poly(new[] { P(left,bottom),P(right,bottom),P(right,top),P(left,top) },c); }
        private void Poly(Vector2[] points,Color c)
        {
            int start=mesh.currentVertCount;
            for(int i=0;i<points.Length;i++) mesh.AddVert(Map(points[i]),c*color,Vector2.zero);
            for(int i=1;i<points.Length-1;i++) mesh.AddTriangle(start,start+i,start+i+1);
        }
        private void Oval(float x,float y,float rx,float ry,Color c)
        {
            const int segments=28;
            int start=mesh.currentVertCount;
            mesh.AddVert(Map(P(x,y)),c*color,Vector2.zero);
            for(int i=0;i<=segments;i++)
            {
                float a=i*Mathf.PI*2f/segments;
                mesh.AddVert(Map(P(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry)),c*color,Vector2.zero);
                if(i>0) mesh.AddTriangle(start,start+i,start+i+1);
            }
        }
        private void Stroke(float x1,float y1,float x2,float y2,float width,Color c)
        {
            Vector2 a=Map(P(x1,y1)),b=Map(P(x2,y2)),delta=b-a;
            if(delta.sqrMagnitude<.000001f)return;
            Vector2 n=new Vector2(-delta.y,delta.x).normalized*width*Mathf.Min(bounds.width,bounds.height)*.5f;
            int i=mesh.currentVertCount;
            mesh.AddVert(a-n,c*color,Vector2.zero);mesh.AddVert(a+n,c*color,Vector2.zero);
            mesh.AddVert(b+n,c*color,Vector2.zero);mesh.AddVert(b-n,c*color,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
    }
}
