"""Original MET parameterized albedo drawing. No source images or external assets.
Run with Python + Pillow + numpy; fixed seeds, periodic stamps, flat diffuse colors.
LicenseRef-MET-Project-Original (same project asset terms as original terrain).
"""
from pathlib import Path
import random, math
import numpy as np
from PIL import Image, ImageDraw
ROOT = Path(__file__).resolve().parents[1] / "unity/METLivingWorld/Assets/Resources/CityTerrain/SurfaceStyles"
SIZE = 512

def draw(name, base, seed, kind):
    rng = random.Random(seed)
    im = Image.new('RGB', (SIZE, SIZE), base)
    d = ImageDraw.Draw(im)
    def stamp(points, fill, line=False, width=1):
        # Every shape wraps at both edges; no baked lighting/shadows.
        for ox in (-SIZE, 0, SIZE):
            for oy in (-SIZE, 0, SIZE):
                p = [(x+ox,y+oy) for x,y in points]
                if line: d.line(p, fill=fill, width=width)
                else: d.polygon(p, fill=fill)
    def tone(delta): return tuple(max(0,min(255,c+delta)) for c in base)
    if kind in ('meadow','dry'):
        # Small blade clusters carry turf detail without equal-sized soil polka dots.
        for i in range(2400):
            x,y=rng.randrange(SIZE),rng.randrange(SIZE)
            length=rng.randrange(3,12);width=rng.randrange(1,3)
            delta=rng.randrange(-9,12)
            stamp([(x-width,y),(x+width,y+length),(x+width+1,y+length//2)],tone(delta))
        if kind=='dry':
            # Thin, scattered bare-soil strokes instead of large dark polygon spots.
            for i in range(190):
                x,y=rng.randrange(SIZE),rng.randrange(SIZE);length=rng.randrange(5,16)
                stamp([(x,y),(x+length,y+3),(x+length-3,y+5),(x-1,y+2)],(141,133,91))
            for i in range(85):
                x,y=rng.randrange(SIZE),rng.randrange(SIZE)
                stamp([(x,y),(x+5,y-2),(x+10,y+2),(x+15,y+7)],(134,125,87),True,1)
                stamp([(x+10,y+2),(x+15,y-3)],(134,125,87),True,1)
    elif kind=='forest':
        for i in range(500):
            x,y=rng.randrange(SIZE),rng.randrange(SIZE);r=rng.randrange(10,26)
            fill=rng.choice([(117,108,70),(132,115,72),(100,106,68),(146,126,81),(109,100,69)])
            angle=rng.uniform(0,math.pi)
            leaf=[(-r,0),(-r*.5,-r*.32),(r*.2,-r*.4),(r,0),(r*.4,r*.35),(-r*.3,r*.27)]
            stamp([(x+u*math.cos(angle)-v*math.sin(angle),y+u*math.sin(angle)+v*math.cos(angle)) for u,v in leaf],fill)
        for i in range(140):
            x,y=rng.randrange(SIZE),rng.randrange(SIZE)
            stamp([(x,y),(x+12,y+7),(x+25,y+8)],tone(-16),True,2)
    elif kind=='dirt':
        # Low ruts and irregular compacted earth plates; no directional UV reliance.
        for i in range(650):
            x,y=rng.randrange(SIZE),rng.randrange(SIZE);r=rng.randrange(4,18)
            stamp([(x-r,y),(x,y-r//3),(x+r,y+1),(x+2,y+r//3)],tone(rng.randrange(-12,16)))
        for i in range(170):
            x,y=rng.randrange(SIZE),rng.randrange(SIZE)
            stamp([(x,y),(x+15,y+3),(x+30,y+3)],tone(-13),True,1)
    elif kind=='stone':
        # Long rectangular paving, half-brick stagger. Shared boundaries keep joints
        # narrow; per-row widths and slightly irregular corners avoid a checkerboard.
        for row in range(16):
            bounds=[0]+[col*64+rng.randrange(-5,6) for col in range(1,8)]+[512]
            for col in range(8):
                x=bounds[col]+(row%2)*32;y=row*32;w=bounds[col+1]-bounds[col]
                points=[(x+1,y+1),(x+w-1,y+1.5),(x+w-1.5,y+30.8),(x+1.5,y+31)]
                stone=rng.randrange(145,165)
                stamp(points,(stone+9,stone+3,stone-12))
                stamp([(x+8,y+10),(x+w*.4,y+8),(x+w*.65,y+15)],(stone+12,stone+6,stone-9),True,2)
    elif kind=='gravel':
        for i in range(3600):
            x,y=rng.randrange(SIZE),rng.randrange(SIZE);r=rng.randrange(2,6);v=rng.randrange(110,165)
            stamp([(x-r,y),(x,y-r),(x+r,y),(x+1,y+r)],(v+7,v+5,v))
    # Very mild periodic grain does not dominate the authored meso shapes.
    arr=np.array(im).astype(float);yy,xx=np.mgrid[:SIZE,:SIZE]
    grain=(np.sin(xx*2*math.pi/8)*np.cos(yy*2*math.pi/16))*1.1
    arr=np.clip(arr+grain[:,:,None],0,255).astype('uint8')
    Image.fromarray(arr).save(ROOT/(name+'.png'))

for spec in [('Meadow',(132,147,89),51,'meadow'),('ForestLitter',(108,104,74),62,'forest'),('DryTurf',(150,143,96),73,'dry'),('RoadDirt',(143,126,96),84,'dirt'),('RoadStone',(122,117,106),95,'stone'),('RoadGravel',(120,121,115),106,'gravel')]: draw(*spec)
