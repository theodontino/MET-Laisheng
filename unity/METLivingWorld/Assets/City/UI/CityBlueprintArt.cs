using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MET.City
{
    /// <summary>Replaceable, procedural axonometric study. No game data or external texture required.</summary>
    [AddComponentMenu("MET/City/Blueprint Art")]
    [RequireComponent(typeof(CanvasRenderer))]
public class CityBlueprintArt : MaskableGraphic
    {
        public int Style;
        public bool ShowDrafting = true;
        [Range(0f, 1f)] public float Fill;
        public Color Accent = new Color(.78f, .85f, .62f, 1f);

        private VertexHelper mesh;
        private Rect drawingRect;
        private float unit;
        private float painted;
        private Color ink;
        private int lastStyle;
        private float lastFill;
        private Color lastAccent;

        protected override void Awake() { base.Awake(); raycastTarget = false; }
        private void LateUpdate()
        {
            if (lastStyle == Style && Mathf.Approximately(lastFill, Fill) && lastAccent == Accent) return;
            lastStyle = Style; lastFill = Fill; lastAccent = Accent;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); mesh = vh;
            Rect r = GetPixelAdjustedRect();
            unit = Mathf.Min(r.width / 290f, r.height / 220f);
            drawingRect = new Rect(r.center.x - 145f * unit, r.center.y - 110f * unit, 290f * unit, 220f * unit);
            painted = Mathf.Clamp01(Fill);
            ink = new Color(.72f, .84f, .86f, .87f);
            Color grid = new Color(.56f, .75f, .79f, .12f);
            if (ShowDrafting) {
            for (int x = 20; x <= 270; x += 15) Line(P(x, 12), P(x, 208), grid, .42f);
            for (int y = 13; y <= 208; y += 15) Line(P(20, y), P(270, y), grid, .42f);
            }
            Color guide = new Color(.61f, .78f, .80f, .28f);
            if (ShowDrafting) {
            Dash(P(20, 169), P(140, 105), guide);
            Dash(P(140, 105), P(270, 172), guide);
            Dash(P(270, 172), P(141, 216), guide);
            Dash(P(141, 216), P(20, 169), guide);
            Dash(P(145, 12), P(145, 208), guide);
            Dash(P(20, 84), P(270, 84), guide);
            }
            switch ((Style % 20 + 20) % 20)
            {
                case 0: Industrial(); break;
                case 1: Residential(); break;
                case 2: Civic(); break;
                case 3: Farm(); break;
                case 4: Clinic(); break;
                case 5: School(); break;
                case 6: Market(); break;
                case 7: Energy(); break;
                case 8: Logistics(); break;
                case 9: Park(); break;
                case 10: Extraction(); break;
                case 11: Research(); break;
                case 12: Workshop(); break;
                case 13: HeatNetwork(); break;
                case 14: Recycling(); break;
                case 15: Mobility(); break;
                case 16: Kitchen(); break;
                case 17: DenseHousing(); break;
                case 18: Utilities(); break;
                default: Modular(); break;
            }
            if (ShowDrafting) {
            // Dimension ticks and a small drafting registration mark.
            Line(P(252, 105), P(252, 181), guide, .65f);
            Line(P(248, 105), P(256, 105), guide, .65f);
            Line(P(248, 181), P(256, 181), guide, .65f);
            Line(P(25, 23), P(35, 23), guide, .65f);
            Line(P(30, 18), P(30, 28), guide, .65f);
            }
            mesh = null;
        }

        private void Industrial()
        {
            Block(55, 103, 101, 78, 146, 102, 54, true);
            Block(147, 85, 187, 63, 230, 86, 66, false);
            Block(167, 36, 180, 29, 194, 36, 48, false);
            // Roof seams, ventilation louvres, and loading doors.
            Detail(65, 102, 101, 83); Detail(101, 83, 137, 102);
            Detail(159, 141, 179, 152); Detail(201, 145, 221, 134);
            Detail(173, 45, 179, 48); Detail(173, 50, 179, 53);
            Window(P(63, 117), P(93, 134), P(93, 147), P(63, 130), true);
            Window(P(110, 133), P(137, 118), P(137, 131), P(110, 146), false);
            Window(P(157, 110), P(179, 122), P(179, 134), P(157, 122), true);
            Window(P(201, 119), P(221, 108), P(221, 120), P(201, 131), false);
            Detail(76, 136, 76, 160); Detail(84, 141, 84, 165);
        }

        private void Residential()
        {
            House(57, 99, 100, 76, 142, 99, 49, 58);
            House(144, 116, 190, 91, 230, 113, 42, 74);
            Window(P(69, 119), P(87, 129), P(87, 143), P(69, 133), true);
            Window(P(112, 130), P(130, 120), P(130, 137), P(112, 147), false);
            Window(P(156, 137), P(173, 147), P(173, 161), P(156, 151), false);
            Window(P(200, 141), P(219, 130), P(219, 145), P(200, 156), true);
            Detail(78, 124, 78, 138); Detail(121, 125, 121, 141);
            // A planted street edge drawn with faceted foliage rather than a texture.
            Poly(new[] { P(28, 146), P(27, 134), P(36, 126), P(47, 128), P(55, 139), P(50, 153), P(37, 158) },
                new Color(.49f, .67f, .55f, .55f * FullPaint()), ink);
            Detail(40, 146, 40, 173); Detail(40, 155, 33, 148);
            Detail(40, 151, 47, 142);
        }

        private void Civic()
        {
            Block(64, 99, 145, 55, 220, 97, 57, false);
            Poly(new[] { P(55, 99), P(145, 38), P(229, 94), P(140, 144) }, Roof(true), ink);
            Detail(145, 38, 140, 144);
            Window(P(76, 119), P(92, 128), P(92, 143), P(76, 134), true);
            Window(P(104, 135), P(121, 145), P(121, 160), P(104, 150), false);
            Window(P(154, 146), P(170, 137), P(170, 152), P(154, 161), true);
            Window(P(181, 130), P(197, 121), P(197, 136), P(181, 145), false);
            Window(P(205, 115), P(215, 109), P(215, 124), P(205, 130), true);
            Poly(new[] { P(158, 174), P(158, 161), P(174, 152), P(174, 165) }, Wall(.75f), ink);
            Detail(178, 64, 178, 22);
            Poly(new[] { P(178, 23), P(204, 28), P(178, 39) }, Roof(false), ink);
            Detail(153, 178, 178, 164); Detail(153, 182, 178, 168);
        }

        // The silhouettes below are purpose-specific planning studies, not a shared building with a new badge.
        private void Farm()
        {
            // Furrowed plot, gabled barn and a glazed growing house.
            Poly(new[] { P(32,154),P(102,114),P(172,154),P(103,195) }, Wall(.78f), ink);
            for(int i=0;i<6;i++)
            {
                float x=43+i*10;
                Detail(x,154-i*5.7f,x+59,188-i*5.7f);
                for(int j=0;j<3;j++) Sprout(x+15+j*15,155-i*5.7f+j*8.5f);
            }
            House(62,85,100,64,136,84,37,44);
            Window(P(81,111),P(97,120),P(97,140),P(81,131),true);
            Poly(new[] { P(151,124),P(151,91),P(177,66),P(210,84),P(232,114),P(199,134),P(199,157) }, Wall(.9f), ink);
            Poly(new[] { P(151,91),P(177,66),P(210,84),P(184,109) }, Roof(true), ink);
            Detail(184,109,199,134); Detail(199,134,232,114);
            for(int i=0;i<4;i++) { Detail(158+i*9,85+i*5,191+i*9,105+i*5); Detail(160+i*9,96+i*5,160+i*9,126+i*5); }
            Detail(199,134,199,157); Tree(240,163,24);
        }
        private void Clinic()
        {
            Block(48,110,113,74,175,108,50,false);
            Block(155,126,188,108,229,130,35,false);
            Block(80,79,112,61,144,79,14,true);
            // Raised medical cross is architectural signage, visible even without colour.
            Poly(new[] { P(100,88),P(108,92),P(108,101),P(117,106),P(117,114),P(108,109),P(108,118),P(100,114),P(100,105),P(91,100),P(91,92),P(100,97) }, Roof(true), ink);
            Window(P(61,125),P(78,135),P(78,147),P(61,137),false);
            Window(P(130,128),P(150,117),P(150,134),P(130,145),true);
            Window(P(178,148),P(194,157),P(194,170),P(178,161),false);
            Detail(120,159,120,141); Detail(120,141,130,136);
            for(int i=0;i<3;i++) Detail(102+i*4,178+i*2,149+i*4,152+i*2);
            Tree(40,171,27); Tree(234,179,22);
        }
        private void School()
        {
            House(42,108,100,76,157,107,44,57);
            Block(157,113,190,95,231,118,44,false);
            // Clock tower and its hands make education immediately distinct.
            Block(103,60,119,51,136,60,51,true);
            Ellipse(118,81,10,13,new Color(.9f,.88f,.70f,FullPaint()),ink);
            Detail(118,81,118,72); Detail(118,81,124,87);
            for(int i=0;i<3;i++) Window(P(55+i*27,125+i*15),P(69+i*27,133+i*15),P(69+i*27,143+i*15),P(55+i*27,135+i*15),i==0);
            Detail(191,100,191,53); Detail(191,54,219,60); Detail(219,60,191,69);
            // Open book on the schoolyard plinth.
            Poly(new[] { P(142,178),P(158,161),P(175,169),P(191,166),P(176,186),P(159,185) }, Roof(true), ink);
            Detail(175,169,159,185); Detail(148,177,158,168); Detail(174,177,182,171);
        }
        private void Market()
        {
            Block(74,91,124,64,174,91,56,false);
            // Striped projecting awning, an open stall, and produce crates.
            Poly(new[] { P(44,105),P(122,147),P(135,129),P(58,87) }, Roof(true), ink);
            for(int i=0;i<7;i++) Detail(52+i*10,100+i*5.4f,64+i*10,83+i*5.4f);
            Detail(45,106,45,163); Detail(122,147,122,188);
            Poly(new[] { P(45,157),P(77,139),P(123,164),P(91,182) }, Wall(.86f),ink);
            Detail(45,157,45,169); Detail(91,182,91,194); Detail(123,164,123,176);
            for(int i=0;i<4;i++) Ellipse(66+i*11,154+i*5,5,4,Roof(i%2==0),ink);
            Crate(167,151,23); Crate(198,166,23); Crate(198,141,19);
            Detail(182,88,182,136); Detail(182,88,219,109); Detail(219,109,219,138);
            Poly(new[] { P(180,86),P(202,74),P(238,95),P(216,107) },Roof(false),ink);
        }
        private void Energy()
        {
            Block(140,132,180,109,222,132,36,false);
            Block(166,79,180,71,195,79,45,false);
            Detail(180,78,180,44); Ellipse(180,43,9,5,Roof(true),ink);
            // Three-blade turbine and two tilted photovoltaic panels.
            Detail(76,74,76,171); Detail(71,171,81,171);
            Ellipse(76,73,5,5,Roof(true),ink);
            Poly(new[] { P(74,71),P(72,26),P(81,33),P(80,71) },Roof(true),ink);
            Poly(new[] { P(73,76),P(38,102),P(40,92),P(71,71) },Roof(false),ink);
            Poly(new[] { P(80,72),P(112,98),P(100,100),P(75,77) },Roof(false),ink);
            for(int p=0;p<2;p++)
            {
                int x=101+p*53,y=157+p*27;
                Poly(new[] { P(x,y),P(x+35,y-20),P(x+56,y-5),P(x+21,y+15) },Roof(p==0),ink);
                Detail(x+18,y-10,x+39,y+5); Detail(x+10,y+7,x+45,y-13);
                Detail(x+21,y+15,x+21,y+23);
            }
            Detail(151,148,168,158); Detail(191,159,210,148);
        }
        private void Logistics()
        {
            House(38,107,97,75,157,108,48,60);
            Window(P(63,137),P(97,156),P(97,182),P(63,163),true);
            for(int i=0;i<4;i++) Detail(65,143+i*5,94,160+i*5);
            Crate(162,130,28); Crate(193,146,28); Crate(164,104,23);
            // Low freight truck, with separately drawn cab and wheels.
            Block(166,169,188,157,221,175,17,false);
            Block(216,172,230,164,245,173,16,true);
            Ellipse(184,191,6,8,Wall(.45f),ink); Ellipse(230,192,6,8,Wall(.45f),ink);
            Window(P(231,170),P(240,175),P(240,181),P(231,176),true);
            Detail(46,190,137,139); Detail(49,194,140,143);
        }
        private void Park()
        {
            Poly(new[] { P(28,150),P(135,90),P(251,155),P(142,216) },Wall(.8f),ink);
            // Pond, winding path and two benches, framed by different trees.
            Ellipse(141,161,42,23,new Color(.44f,.67f,.70f,.65f*FullPaint()),ink);
            Ellipse(142,160,30,14,new Color(.67f,.80f,.74f,.34f*FullPaint()),ink);
            Detail(120,158,135,151); Detail(151,170,168,162);
            Poly(new[] { P(66,163),P(99,182),P(121,177),P(87,157) },Roof(true),ink);
            Detail(75,171,75,183); Detail(104,184,104,195);
            Tree(75,139,58); Tree(185,125,56); Tree(221,158,40); Tree(144,100,32);
            Detail(117,198,107,191); Detail(107,191,95,188); Detail(95,188,77,180);
            Detail(163,137,176,145); Detail(176,145,190,147); Detail(190,147,202,153);
        }
        private void Extraction()
        {
            // Open terraced pit, suspended gantry and a stockpile are unique silhouettes.
            Poly(new[] { P(31,154),P(113,109),P(192,153),P(109,199) },Wall(.72f),ink);
            Poly(new[] { P(47,155),P(112,119),P(176,155),P(110,191) },Wall(.52f),ink);
            Poly(new[] { P(64,156),P(111,130),P(159,157),P(111,184) },Wall(.35f),ink);
            Poly(new[] { P(83,157),P(111,142),P(141,158),P(112,175) },Roof(true),ink);
            Detail(48,155,64,156); Detail(176,155,159,157); Detail(110,191,111,184);
            Detail(179,153,179,53); Detail(179,53,229,81); Detail(229,81,229,166);
            Detail(179,63,229,91); Detail(179,53,229,91); Detail(179,63,229,81);
            Detail(206,73,206,126); Detail(206,126,198,132); Detail(198,132,206,138);
            Poly(new[] { P(183,179),P(211,145),P(242,176),P(213,193) },Roof(false),ink);
            Detail(211,145,213,193); Detail(207,159,230,180);
        }
        private void Research()
        {
            Block(51,129,110,96,174,131,37,false);
            // Observatory dome with longitude ribs on a glass laboratory base.
            Ellipse(111,96,40,21,Wall(.85f),ink);
            Vector2[] dome=new Vector2[19];
            for(int i=0;i<19;i++) { float a=Mathf.PI-i*Mathf.PI/18; dome[i]=P(111+40*Mathf.Cos(a),96-49*Mathf.Sin(a)); }
            Poly(dome,Roof(true),ink);
            Detail(111,47,111,96); Detail(111,47,93,84); Detail(93,84,94,105); Detail(111,47,129,84); Detail(129,84,129,105);
            Block(176,116,201,102,227,116,55,false);
            for(int i=0;i<3;i++) Window(P(184,133+i*13),P(197,140+i*13),P(197,147+i*13),P(184,140+i*13),i==0);
            Window(P(65,141),P(95,158),P(95,171),P(65,154),false);
            // Angled telescope assembly on the observation terrace.
            Poly(new[] { P(147,69),P(179,49),P(190,57),P(158,78) },Roof(true),ink);
            Ellipse(184,53,7,6,Wall(.45f),ink); Detail(164,74,164,95); Detail(153,101,175,89);
        }
        private void Workshop()
        {
            House(52,104,101,77,148,103,47,57);
            Window(P(66,125),P(94,141),P(94,161),P(66,145),true);
            // Outdoor assembly bench with a toothed workpiece, vice and timber rack.
            Poly(new[] { P(143,151),P(180,130),P(228,157),P(190,178) },Roof(true),ink);
            Detail(148,156,148,182); Detail(190,178,190,202); Detail(225,161,225,187);
            Cog(185,149,15);
            Block(202,148,212,142,222,148,8,false);
            Detail(215,146,229,138); Detail(229,135,229,142);
            for(int i=0;i<3;i++) { Detail(42+i*7,168+i*4,69+i*7,153+i*4); Detail(45+i*7,174+i*4,72+i*7,159+i*4); }
            Detail(47,170,47,185); Detail(77,165,77,184);
        }
        private void HeatNetwork()
        {
            Block(49,105,90,82,133,106,53,false);
            // Two boiler cylinders and insulated branching heat pipes.
            Tank(157,113,20,60,true); Tank(207,139,20,60,false);
            Pipe(105,153,146,176); Pipe(146,176,190,151); Pipe(146,176,146,198);
            Pipe(190,151,210,163); Pipe(157,145,157,169);
            Ellipse(146,175,6,4,Roof(true),ink);
            for(int i=0;i<3;i++) Detail(104,125+i*8,120,116+i*8);
            Detail(82,91,82,58); Detail(87,94,87,61); Ellipse(84,57,7,4,Roof(false),ink);
        }
        private void Recycling()
        {
            Block(47,112,98,84,151,113,39,false);
            // Three sorting bins under a conveyor and a large loop of material arrows.
            for(int i=0;i<3;i++) { Block(131+i*35,154+i*9,145+i*35,146+i*9,162+i*35,155+i*9,25,i==0); }
            Poly(new[] { P(94,141),P(112,130),P(203,148),P(185,159) },Wall(.75f),ink);
            for(int i=0;i<7;i++) Detail(108+i*11,138+i*2,99+i*11,144+i*2);
            Arrow(P(152,61),P(196,61),P(194,51));
            Arrow(P(201,70),P(216,101),P(225,91));
            Arrow(P(204,109),P(164,109),P(169,119));
            Arrow(P(156,100),P(141,75),P(132,84));
            Crate(58,162,18); Crate(84,176,18);
        }
        private void Mobility()
        {
            // Station platform, control cabin and an elevated drone above loading rails.
            Block(52,144,123,104,224,160,12,false);
            Block(58,103,87,87,117,104,35,true);
            Detail(120,143,184,178); Detail(126,139,190,174);
            for(int i=0;i<7;i++) Detail(117+i*9,145+i*5,129+i*9,138+i*5);
            Detail(162,130,162,89); Detail(162,89,205,113); Detail(205,113,205,155);
            // Four rotor arms and parcel beneath a compact aerial chassis.
            Poly(new[] { P(154,57),P(170,48),P(185,57),P(169,66) },Roof(true),ink);
            Detail(158,54,139,43); Detail(179,54,198,43); Detail(158,61,139,73); Detail(179,61,198,73);
            Ellipse(138,43,13,5,Wall(.85f),ink); Ellipse(199,43,13,5,Wall(.85f),ink);
            Ellipse(138,73,13,5,Wall(.85f),ink); Ellipse(199,73,13,5,Wall(.85f),ink);
            Detail(163,66,163,80); Detail(177,66,177,80); Crate(157,84,18);
        }
        private void Kitchen()
        {
            House(52,104,104,75,158,105,43,55);
            Block(87,61,101,53,115,61,25,false);
            Window(P(64,125),P(88,139),P(88,151),P(64,137),true);
            // Community table, bowls and stools establish shared food rather than farming.
            Poly(new[] { P(132,153),P(174,129),P(237,164),P(195,188) },Roof(true),ink);
            Detail(136,158,136,177); Detail(195,188,195,207); Detail(233,169,233,187);
            for(int i=0;i<3;i++) { Ellipse(159+i*21,153+i*12,8,5,Wall(.9f),ink); Detail(156+i*21,151+i*12,164+i*21,146+i*12); }
            Crate(151,188,12); Crate(210,142,12);
            Tree(37,158,27);
        }
        private void DenseHousing()
        {
            Block(53,62,100,36,146,62,111,true);
            Block(147,100,187,78,228,101,77,false);
            for(int f=0;f<5;f++)
            {
                for(int col=0;col<2;col++)
                    Window(P(64+col*20,87+col*11+f*16),P(77+col*20,94+col*11+f*16),P(77+col*20,103+col*11+f*16),P(64+col*20,96+col*11+f*16),f==1&&col==0);
                Window(P(111,96+f*16),P(134,83+f*16),P(134,92+f*16),P(111,105+f*16),false);
            }
            for(int f=0;f<3;f++) Window(P(161,125+f*16),P(179,135+f*16),P(179,144+f*16),P(161,134+f*16),f==0);
            Tree(37,177,32); Tree(236,184,26);
        }
        private void Utilities()
        {
            // Cutaway service trench reveals parallel conduits below a removable pavement slab.
            Poly(new[] { P(36,141),P(127,90),P(245,155),P(153,207) },Wall(.6f),ink);
            Poly(new[] { P(47,144),P(126,100),P(232,158),P(153,197) },Wall(.32f),ink);
            for(int i=0;i<3;i++) Pipe(71+i*19,146+i*11,149+i*19,103+i*11);
            Poly(new[] { P(88,76),P(147,43),P(225,86),P(165,120) },Roof(true),ink);
            Detail(88,76,88,84); Detail(88,84,165,128); Detail(165,128,225,94); Detail(225,94,225,86); Detail(165,120,165,128);
            for(int i=0;i<4;i++) Detail(106+i*20,77+i*11,144+i*20,56+i*11);
            Dash(P(89,87),P(69,127),ink); Dash(P(223,97),P(233,142),ink);
            Ellipse(193,88,12,7,Wall(.6f),ink);
        }
        private void Modular()
        {
            // Exploded prefabricated room modules and corner alignment rails.
            Block(53,147,105,118,158,147,33,false);
            Block(130,102,172,79,215,103,29,true);
            Poly(new[] { P(52,83),P(106,53),P(159,83),P(105,113) },Roof(true),ink);
            Detail(52,83,52,92); Detail(52,92,105,122); Detail(105,122,159,92); Detail(159,92,159,83);
            Window(P(65,161),P(89,174),P(89,188),P(65,175),true);
            Window(P(147,119),P(165,129),P(165,141),P(147,131),false);
            Dash(P(52,94),P(53,144),ink); Dash(P(105,122),P(105,174),ink); Dash(P(159,94),P(158,144),ink);
            for(int i=0;i<3;i++) { Detail(223,149+i*13,240,159+i*13); Detail(223,149+i*13,212,156+i*13); }
            Detail(223,149,223,184); Detail(240,159,240,194);
        }
        private void Sprout(float x,float y)
        {
            Detail(x,y,x,y-7); Detail(x,y-3,x-4,y-6); Detail(x,y-3,x+4,y-6);
        }
        private void Tree(float x,float y,float height)
        {
            Detail(x,y,x,y-height*.52f);
            Poly(new[] { P(x-height*.33f,y-height*.45f),P(x-height*.25f,y-height*.82f),P(x,y-height),P(x+height*.27f,y-height*.81f),P(x+height*.33f,y-height*.46f),P(x,y-height*.30f) },
                new Color(.49f,.67f,.51f,.72f*FullPaint()),ink);
            Detail(x,y-height*.34f,x,y-height*.75f);
            Detail(x,y-height*.50f,x-height*.17f,y-height*.66f);
        }
        private void Crate(float x,float y,float size)
        {
            Block(x,y,x+size*.5f,y-size*.28f,x+size,y,size*.7f,false);
            Detail(x+2,y+4,x+size*.48f,y+size*.94f); Detail(x+2,y+size*.65f,x+size*.48f,y+size*.32f);
            Detail(x+size*.53f,y+size*.31f,x+size-2,y+size*.65f);
        }
        private void Ellipse(float x,float y,float rx,float ry,Color fill,Color stroke)
        {
            Vector2[] ring=new Vector2[28];
            for(int i=0;i<ring.Length;i++) { float a=i*Mathf.PI*2f/ring.Length; ring[i]=P(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry); }
            Poly(ring,fill,stroke);
        }
        private void Tank(float x,float y,float radius,float height,bool partial)
        {
            Poly(new[] { P(x-radius,y),P(x+radius,y),P(x+radius,y+height),P(x-radius,y+height) },Wall(.82f),ink);
            Ellipse(x,y+height,radius,radius*.5f,Wall(.65f),ink);
            Ellipse(x,y,radius,radius*.5f,Roof(partial),ink);
            Detail(x-radius+5,y+5,x-radius+5,y+height-4);
            Detail(x+radius-5,y+5,x+radius-5,y+height-4);
        }
        private void Pipe(float x1,float y1,float x2,float y2)
        {
            Line(P(x1,y1),P(x2,y2),ink,7f);
            Color tube=Roof(true); tube.a=Mathf.Max(.05f,tube.a);
            Line(P(x1,y1),P(x2,y2),tube,4.5f);
            Line(P(x1-1,y1-1),P(x2-1,y2-1),new Color(ink.r,ink.g,ink.b,.6f),.8f);
        }
        private void Arrow(Vector2 a,Vector2 b,Vector2 wing)
        {
            Line(a,b,ink,2f); Line(b,wing,ink,2f);
            Vector2 other=b+(b-wing)*.55f+(a-b).normalized*8f;
            Poly(new[] { b,wing,other },Roof(true),ink);
        }
        private void Cog(float x,float y,float radius)
        {
            Vector2[] ring=new Vector2[40];
            for(int i=0;i<ring.Length;i++) { float a=i*Mathf.PI*2f/ring.Length; float r=i%4<2?radius:radius*.78f; ring[i]=P(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r*.57f); }
            Poly(ring,Roof(true),ink); Ellipse(x,y,radius*.36f,radius*.21f,Wall(.45f),ink);
        }

        private void House(float lx, float ly, float bx, float by, float rx, float ry, float h, float ridge)
        {
            Block(lx, ly, bx, by, rx, ry, h, false);
            Poly(new[] { P(lx - 8, ly + 1), P(bx, ridge), P(rx + 8, ry), P(bx, ly + 25) }, Roof(true), ink);
            Detail(bx, ridge, bx, ly + 25);
        }

        private void Block(float lx, float ly, float bx, float by, float rx, float ry, float h, bool accent)
        {
            float fx = lx + rx - bx, fy = ly + ry - by;
            Poly(new[] { P(lx, ly), P(fx, fy), P(fx, fy + h), P(lx, ly + h) }, Wall(.68f), ink);
            Poly(new[] { P(fx, fy), P(rx, ry), P(rx, ry + h), P(fx, fy + h) }, Wall(.9f), ink);
            Poly(new[] { P(lx, ly), P(bx, by), P(rx, ry), P(fx, fy) }, Roof(accent), ink);
        }

        private float FullPaint() { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.38f, 1f, painted)); }
        private Color Wall(float shade) { return new Color(Accent.r * shade, Accent.g * shade, Accent.b * shade, FullPaint() * .88f); }
        private Color Roof(bool partial)
        {
            float a = partial ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.04f, .35f, painted)) : FullPaint();
            return new Color(Accent.r, Accent.g, Accent.b, a * .84f);
        }
        private void Window(Vector2 a, Vector2 b, Vector2 c, Vector2 d, bool partial)
        {
            float aFill = partial ? Mathf.Clamp01(painted / .35f) : FullPaint();
            Poly(new[] { a, b, c, d }, new Color(.97f, .83f, .55f, aFill * .85f), new Color(ink.r, ink.g, ink.b, .72f));
        }
        private void Detail(float x1, float y1, float x2, float y2) { Line(P(x1, y1), P(x2, y2), new Color(ink.r, ink.g, ink.b, .64f), .8f); }
        private static Vector2 P(float x, float y) { return new Vector2(x, y); }
        private Vector2 Map(Vector2 p) { return new Vector2(drawingRect.xMin + p.x * unit, drawingRect.yMax - p.y * unit); }
        private Color Tint(Color c) { return c * color; }
        private void Poly(Vector2[] points, Color fill, Color stroke)
        {
            if (fill.a > .001f)
            {
                int start = mesh.currentVertCount;
                for (int i = 0; i < points.Length; i++) mesh.AddVert(Map(points[i]), Tint(fill), Vector2.zero);
                Triangulate(points, start);
            }
            for (int i = 0; i < points.Length; i++) Line(points[i], points[(i + 1) % points.Length], stroke, 1f);
        }
        // Ear clipping also handles medical crosses, gear teeth, and the cutaway greenhouse.
        // A triangle fan would paint across concave notches and hide those identifying details.
        private void Triangulate(Vector2[] points, int start)
        {
            var remaining = new List<int>(points.Length);
            float area = 0f;
            for (int i=0;i<points.Length;i++)
            {
                remaining.Add(i);
                Vector2 a=points[i], b=points[(i+1)%points.Length];
                area += a.x*b.y-b.x*a.y;
            }
            float winding = area >= 0f ? 1f : -1f;
            int guard = points.Length*points.Length;
            while(remaining.Count>2 && guard-->0)
            {
                bool clipped=false;
                for(int i=0;i<remaining.Count;i++)
                {
                    int before=remaining[(i+remaining.Count-1)%remaining.Count];
                    int current=remaining[i], after=remaining[(i+1)%remaining.Count];
                    Vector2 a=points[before],b=points[current],c=points[after];
                    if(Cross(b-a,c-b)*winding<=.00001f) continue;
                    bool occupied=false;
                    for(int j=0;j<remaining.Count;j++)
                    {
                        int test=remaining[j];
                        if(test==before||test==current||test==after)continue;
                        Vector2 p=points[test];
                        if(Cross(b-a,p-a)*winding>=-.00001f && Cross(c-b,p-b)*winding>=-.00001f && Cross(a-c,p-c)*winding>=-.00001f)
                        { occupied=true; break; }
                    }
                    if(occupied)continue;
                    mesh.AddTriangle(start+before,start+current,start+after);
                    remaining.RemoveAt(i); clipped=true; break;
                }
                if(!clipped)break;
            }
        }
        private static float Cross(Vector2 a,Vector2 b) { return a.x*b.y-a.y*b.x; }
        private void Dash(Vector2 a, Vector2 b, Color c)
        {
            float length = Vector2.Distance(a, b);
            if (length < .001f) return;
            for (float p = 0; p < length; p += 7f)
                Line(Vector2.Lerp(a, b, p / length), Vector2.Lerp(a, b, Mathf.Min(p + 3f, length) / length), c, .6f);
        }
        private void Line(Vector2 a, Vector2 b, Color c, float width)
        {
            Vector2 from = Map(a), to = Map(b), direction = to - from;
            if (direction.sqrMagnitude < .000001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * unit * .5f;
            int i = mesh.currentVertCount;
            Color tint = Tint(c);
            mesh.AddVert(from - normal, tint, Vector2.zero); mesh.AddVert(from + normal, tint, Vector2.zero);
            mesh.AddVert(to + normal, tint, Vector2.zero); mesh.AddVert(to - normal, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
