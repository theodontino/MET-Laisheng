from pathlib import Path
import math, random, json, hashlib, wave, struct
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'assets/world030'; OUT.mkdir(parents=True,exist_ok=True)
MASTERS=ROOT/'art_source/masters';MASTERS.mkdir(exist_ok=True,parents=True)
S=3; W=768; H=768
# Original parametric 2.5D illustrations: consistent orthographic camera, matte facets.
def tint(c,k):
 if isinstance(c,str): c=tuple(int(c[i:i+2],16) for i in (0,2,4))
 return tuple(max(0,min(255,int(v*k))) for v in c)
class Scene:
 def __init__(self):self.faces=[];self.rng=random.Random(12)
 def poly(self,p,c,bias=0):self.faces.append((sum(x+y+z*.55 for x,y,z in p)/len(p)+bias,p,c))
 def box(self,x,y,z,w,d,h,c):
  a=(x,y,z);b=(x+w,y,z);c1=(x+w,y+d,z);d1=(x,y+d,z)
  A=(x,y,z+h);B=(x+w,y,z+h);C=(x+w,y+d,z+h);D=(x,y+d,z+h)
  self.poly([b,c1,C,B],tint(c,.82));self.poly([d1,c1,C,D],tint(c,.94));self.poly([A,B,C,D],tint(c,1.12))
 def roof(self,x,y,z,w,d,r,c):
  A=(x-1,y-1,z);B=(x+w+1,y-1,z);C=(x+w+1,y+d+1,z);D=(x-1,y+d+1,z);L=(x-1,y+d*.5,z+r);R=(x+w+1,y+d*.5,z+r)
  self.poly([B,C,R],tint(c,.79));self.poly([A,B,R,L],tint(c,1.11));self.poly([D,C,R,L],tint(c,.94))
  for i in range(1,8):
   yy=y+d/2+i*d/16;zz=z+r*(1-i/8)
   self.poly([(x-1,yy,zz+.08),(x+w+1,yy,zz+.08),(x+w+1,yy+.22,zz+.02),(x-1,yy+.22,zz+.02)],tint(c,.82),.2)
 def windows(self,x,y,z,w,d,h,n=4,levels=2):
  for j in range(levels):
   zz=z+3+j*max(5,(h-6)/max(1,levels))
   for i in range(n):
    xx=x+3+(w-6)*(i+.15)/max(n,1)
    self.box(xx,y+d+.12,zz,max(2.1,(w-9)/n*.5),.32,3.9,'a1bbb8')
    self.box(xx-.2,y+d+.5,zz-.5,max(2.5,(w-9)/n*.5+.4),.6,.6,'ece4cf')
   for i in range(max(1,n-1)):
    yy=y+3+(d-6)*(i+.15)/max(n-1,1)
    self.box(x+w+.12,yy,zz,.35,2.5,3.9,'739697')
 def house(self,x,y,w,d,h=16,roof='b7754e',wall='e1d8c0'):
  self.box(x,y,0,w,d,h,wall);self.windows(x,y,0,w,d,h,max(2,int(w/9)),max(1,int(h/10)))
  self.box(x+w*.48,y+d+.7,0,4,1,7,'596f6b');self.box(x+w*.43,y+d+.2,7,6,3,.8,'ede5cb')
  self.roof(x,y,h,w,d,7,roof)
 def cylinder(self,x,y,z,r,h,c,n=10):
  pts=[(x+math.cos(i*math.tau/n)*r,y+math.sin(i*math.tau/n)*r,z) for i in range(n)]
  for i in range(n):
   j=(i+1)%n; a=pts[i];b=pts[j]
   if math.cos((i+.5)*math.tau/n)+math.sin((i+.5)*math.tau/n)>-.2:self.poly([a,b,(b[0],b[1],z+h),(a[0],a[1],z+h)],tint(c,.83+.2*math.cos((i+.5)*math.tau/n-1)))
  self.poly([(a,b,z+h) for a,b,_ in pts],tint(c,1.15))
 def tree(self,x,y,r=8,h=23,conifer=False):
  self.box(x-1,y-1,0,2,2,h*.55,'907557')
  if conifer:
   for base,rr,hh in [(7,r,h*.65),(h*.36,r*.77,h*.59),(h*.60,r*.50,h*.52)]:
    n=7
    for i in range(n):
     a=(x+math.cos(i*math.tau/n)*rr,y+math.sin(i*math.tau/n)*rr,base);b=(x+math.cos((i+1)*math.tau/n)*rr,y+math.sin((i+1)*math.tau/n)*rr,base)
     self.poly([a,b,(x,y,base+hh)],tint('638772',.85+.18*math.cos(i-1)))
  else:
   for dx,dy,dz,rr in [(-r*.4,0,h*.7,r*.68),(r*.5,0,h*.8,r*.64),(0,r*.45,h*.6,r*.7),(0,0,h,r*.6)]:
    n=9
    for i in range(n):
     a=(x+dx+math.cos(i*math.tau/n)*rr,y+dy+math.sin(i*math.tau/n)*rr,dz)
     b=(x+dx+math.cos((i+1)*math.tau/n)*rr,y+dy+math.sin((i+1)*math.tau/n)*rr,dz)
     self.poly([a,b,(x+dx,y+dy,dz+rr*.6)],tint('83a377',.93+.13*math.cos(i)))
 def render(self,name,folder='buildings',size=512):
  ss=2; buf=np.zeros((H*ss,W*ss,4),dtype=np.uint8); zb=np.full((H*ss,W*ss),-1e6,dtype=np.float32)
  def pr(v):x,y,z=v;return ((384+(x-y)*3.50)*ss,(430+(x+y)*1.94-z*4.25)*ss,x+y+.912941*z)
  for bias,p,c in self.faces:
   pp=[pr(v) for v in p]
   for i in range(1,len(pp)-1):
    a,b,cc=pp[0],pp[i],pp[i+1]; den=(b[1]-cc[1])*(a[0]-cc[0])+(cc[0]-b[0])*(a[1]-cc[1])
    if abs(den)<.00001:continue
    x0=max(0,int(min(a[0],b[0],cc[0])));x1=min(W*ss,int(max(a[0],b[0],cc[0]))+1)
    y0=max(0,int(min(a[1],b[1],cc[1])));y1=min(H*ss,int(max(a[1],b[1],cc[1]))+1)
    if x1<=x0 or y1<=y0:continue
    yy,xx=np.ogrid[y0:y1,x0:x1]
    w0=((b[1]-cc[1])*(xx-cc[0])+(cc[0]-b[0])*(yy-cc[1]))/den
    w1=((cc[1]-a[1])*(xx-cc[0])+(a[0]-cc[0])*(yy-cc[1]))/den;w2=1-w0-w1
    zz=w0*a[2]+w1*b[2]+w2*cc[2];target=zb[y0:y1,x0:x1];mask=(w0>=-.00001)&(w1>=-.00001)&(w2>=-.00001)&(zz>=target-.0001)
    target[mask]=zz[mask];buf[y0:y1,x0:x1][mask]=(*c,255)
  img=Image.fromarray(buf,'RGBA')
  img=img.resize((W,H),Image.Resampling.LANCZOS)
  # Standard ground origin; maintain whole silhouettes, no dark matte background.
  bounds=img.getbbox();canvas=Image.new('RGBA',(size,size));cropped=img.crop(bounds)
  fit=min(size*.90/cropped.width,size*.78/cropped.height);new=(int(cropped.width*fit),int(cropped.height*fit));cropped=cropped.resize(new,Image.Resampling.LANCZOS)
  xx=(size-new[0])//2; yy=int(size*.79)-new[1];canvas.alpha_composite(cropped,(xx,max(0,yy)))
  path=OUT/folder/f'{name}.png';path.parent.mkdir(exist_ok=True,parents=True);canvas.save(path)
  img.save(MASTERS/f'{name}.png')
  return {'id':name,'path':path.relative_to(ROOT).as_posix(),'size':[size,size],'ground_anchor':[size*.5,size*.70],'bounds':list(canvas.getbbox()),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'method':'original_parametric_illustration','camera':'fixed high oblique, orthographic','source_script':'tools/make_art030.py'}
arts={}
# Ten intact, distinctive building compositions. All shadow/animation/state painted by game.
s=Scene();s.house(-20,-14,23,21,20);s.house(5,-9,20,22,17,'cb9262');s.house(-15,12,20,17,15,'9f674e');s.tree(28,15,5,15);arts['hamlet']=s.render('hamlet')
s=Scene();s.house(-26,-12,24,18,15,'aa7951');s.box(3,-18,0,23,14,9,'bca786');s.roof(3,-18,9,23,14,5,'729789');s.cylinder(24,2,0,4,12,'bcc7ba')
for i in range(5):s.box(-16+i*7,14,0,3.5,25,1.5,'8ba46b');s.box(-15+i*7,16,1.5,1.8,21,1.5,'b1b963')
arts['farm']=s.render('farm')
s=Scene();s.box(-23,-14,0,36,18,13,'baad8d');s.roof(-23,-14,13,36,18,3,'8b9990');s.box(-20,4,0,30,1,10,'776e5b')
for yy in [8,13,18]:
 for i in range(5):s.cylinder(15+i*3.6,yy,0,1.7,3.5,'b39164',7)
for i in range(5):s.box(-22+i*6,9+i%2*4,0,5,7,4+i%3,'b4b5a4')
arts['timber']=s.render('timber')
s=Scene();s.box(-23,-14,0,29,23,16,'d1d4c7');s.windows(-23,-14,0,29,23,16,3,1);s.box(-25,-16,16,33,27,2,'789b9c');s.box(10,-5,0,14,16,10,'9eaea7');s.cylinder(14,17,0,4,11,'a4b4ab');s.cylinder(-1,21,0,2,42,'cccdbc');s.box(-4,19,39,6,5,5,'d2d7c6')
arts['power']=s.render('power')
s=Scene();s.box(-25,-18,0,45,31,18,'b38468');s.windows(-25,-18,0,45,31,18,5,2)
for y in [-19,-8,3]:s.roof(-26,y,18,47,10,6,'748f8d')
s.cylinder(24,-10,0,4.0,37,'af8066');s.cylinder(24,-10,34,4.6,3.5,'8e7160');s.box(-10,13,0,14,1,9,'5f7770');arts['works']=s.render('works')
s=Scene()
for x,y,c in [(-24,-17,'c8915e'),(0,-17,'88a59d'),(-24,8,'98ae86'),(0,8,'d6b268')]:
 for dx,dy in [(1,1),(20,1),(1,15),(20,15)]:s.box(x+dx,y+dy,0,1,1,12,'8c826a')
 s.roof(x,y,12,22,17,4,c);s.box(x+2,y+4,0,17,10,5,'ccb894')
 for j in range(5):s.box(x+3+j*3,y+8,5,2,3,1.4,'a8ad5f' if j%2 else 'c87d59')
arts['market']=s.render('market')
s=Scene();s.house(-28,-21,54,14,19,'91a49d','e3dcc6');s.house(-28,-7,14,33,17,'91a49d','e3dcc6');s.house(12,-7,14,25,17,'91a49d','e3dcc6');s.box(-8,0,.1,15,20,.5,'c9c3a2');s.tree(7,21,5,15);s.box(-9,22,0,6,3,2,'a79976');arts['school']=s.render('school')
s=Scene();s.box(-22,-15,0,37,27,17,'e5e4d6');s.windows(-22,-15,0,37,27,17,4,2);s.box(-23,-16,17,39,29,2,'a0b9b1');s.box(15,-5,0,13,18,12,'d6dbca');s.box(14,-6,12,15,20,1.5,'7ea79e');s.box(-6,12,0,9,1,9,'779998');s.box(-12,12,10,21,9,1.2,'508f88');s.box(-7,4,19,13,5,3,'eef0dc');arts['clinic']=s.render('clinic')
s=Scene();s.tree(-13,-10,13,27);s.tree(15,-8,10,23);s.tree(-2,14,7,20)
for i in range(16):
 x,y=23*math.cos(i*math.tau/16),20*math.sin(i*math.tau/16);s.box(x-3,y-3,0,6,6,.35,'c6c3a1')
for x,y in [(14,20),(-24,7)]:s.box(x,y,0,9,3,3,'937e5d');s.box(x,y,3,9,1,3,'a38b65')
s.cylinder(4,2,0,7,2,'b4c6b6');s.cylinder(4,2,2,5,.3,'79adb0');arts['park']=s.render('park')
s=Scene();s.box(-28,-17,0,52,32,18,'d1cbb8');s.roof(-28,-17,18,52,32,4,'79938e')
for x in [-23,-10,3,16]:
 s.box(x,15,0,8,.6,11,'8a9e93')
 for z in range(2,11,2):s.box(x,15.7,z,8,.15,.25,'bcc7b2')
s.box(-31,-3,0,3,15,1,'b6b2a0');arts['warehouse']=s.render('warehouse')
# Twelve reusable props and eight add-on families.
prop_specs=['tree_broadleaf','tree_conifer','shrub','rocks','bench','lamp','fence','stall','crates','materials','furrows','flowers']
for name in prop_specs:
 s=Scene()
 if name=='tree_broadleaf':s.tree(0,0,11,29)
 elif name=='tree_conifer':s.tree(0,0,9,29,True)
 elif name=='shrub':s.tree(0,0,9,5)
 elif name=='rocks':
  for x,y,h in [(0,0,7),(-6,4,4),(7,1,3)]:s.box(x,y,0,8,7,h,'b8b6a2')
 elif name=='bench':s.box(-8,-2,0,16,4,4,'9c835e');s.box(-8,-2,4,16,1,4,'ac926b')
 elif name=='lamp':s.cylinder(0,0,0,.7,22,'738478');s.box(-2,-2,22,4,4,3,'e8dfb7');s.roof(-2,-2,25,4,4,1.2,'657f78')
 elif name=='fence':
  for x in [-12,0,12]:s.box(x,-1,0,1.3,1.3,8,'b4a07b')
  for z in [3,6]:s.box(-12,-1,z,25,1,1,'c9b88f')
 elif name=='stall':s.house(-8,-5,16,10,8,'c89768')
 elif name=='crates':
  for x,y,z in [(-6,-3,0),(2,-3,0),(-2,-3,6)]:s.box(x,y,z,7,7,6,'b69b71')
 elif name=='materials':
  for x in range(-10,11,5):s.box(x,-10,0,3,22,3,'b29570')
 elif name=='furrows':
  for x in range(-16,17,5):s.box(x,-15,0,2.5,30,1,'8ca370')
 elif name=='flowers':
  s.box(-10,-5,0,20,10,2,'a7ad7a')
  for i in range(15):s.box(s.rng.uniform(-8,8),s.rng.uniform(-4,4),2,1.5,1.5,1.5,['dfa687','ecd9a3','b9bfac'][i%3])
 arts[name]=s.render(name,'props',256)
for name in ['storage','heat','recycle','open_court','reuse','roof_farm','drone','workshop']:
 s=Scene()
 if name=='storage':s.box(-11,-7,0,22,14,11,'b8c2b5');s.roof(-11,-7,11,22,14,3,'85a197')
 elif name=='heat':
  for x in [-7,0,7]:s.cylinder(x,0,0,2.4,13,'b6c4b7');s.box(-9,1,4,19,2,2,'c49a6e')
 elif name=='recycle':
  for x,c in [(-8,'759887'),(0,'a9aa70'),(8,'83a7ae')]:s.box(x,-4,0,6,9,8,c);s.box(x-1,-5,8,8,11,1,'c8ccb1')
 elif name=='open_court':
  s.box(-13,-10,0,26,20,.6,'c3c5a5');s.box(-10,-6,0,5,12,4,'b38f67');s.tree(9,3,5,13)
 elif name=='reuse':
  s.box(-13,-3,0,2,6,14,'bc8964');s.box(11,-3,0,2,6,14,'bc8964');s.box(-13,-3,13,26,6,2,'749d99');s.roof(-12,3,8,24,8,1,'d9b176')
 elif name=='roof_farm':
  for x in [-10,-3,4]:s.box(x,-10,0,5,20,2,'aeab80');s.box(x+1,-9,2,3,18,2,'7a9f71')
 elif name=='drone':s.box(-11,-11,0,22,22,3,'9daea4');s.box(-7,-7,3,14,14,.5,'cdd4b9');s.box(-1,-8,4,2,16,.5,'eff0d5');s.box(-8,-1,4,16,2,.5,'eff0d5')
 elif name=='workshop':s.house(-10,-7,20,14,10,'718e90','b79c81')
 arts[name]=s.render(name,'modules',256)
# Use two or fewer 2048 atlases, transparent 8px gutters; runtime catalogs store rects.
atlas=Image.new('RGBA',(2048,2048));atlas2=Image.new('RGBA',(2048,2048)); page2=False
for i,(name,a) in enumerate(arts.items()):
 im=Image.open(ROOT/a['path']);sz=im.width
 if i<10:x=(i%3)*528+8;y=(i//3)*528+8;page=0;can=atlas
 else:
  j=i-10;x=(j%7)*272+8;y=(j//7)*272+8;page=1;can=atlas2
 can.alpha_composite(im,(x,y));a['atlas_page']=page;a['atlas_rect']=[x,y,sz,sz]
atlas.save(OUT/'atlas0.png');atlas2.save(OUT/'atlas1.png')
# Atlas placement row4 can exceed 2048 if 512 rows 4*528=2112; last building put page1 safely.
# rebuild proper 4x3 building packing using 496 runtime subimages, padding 8.
atlas=Image.new('RGBA',(2048,2048))
for i,name in enumerate(list(arts)[:10]):
 a=arts[name];im=Image.open(ROOT/a['path']).resize((496,496),Image.Resampling.LANCZOS);x=(i%4)*512+8;y=(i//4)*512+8;atlas.alpha_composite(im,(x,y));a['atlas_rect']=[x,y,496,496];a['atlas_page']=0
atlas.save(OUT/'atlas0.png')
catalog={'schema':'met.art.030','style':'humanist_industrial_miniature','generated_concepts_used_as_reference_only':True,'assets':arts,'atlases':['assets/world030/atlas0.png','assets/world030/atlas1.png'],'font':'OS SystemFont only; no font binaries distributed'}
(ROOT/'content/presentation030').mkdir(parents=True,exist_ok=True)
(ROOT/'content/presentation030/art_catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2))
# Contact proof (not a game screenshot).
contact=Image.new('RGB',(1400,700),'#e7e5d9');d=ImageDraw.Draw(contact)
for i,name in enumerate(list(arts)[:10]):
 im=Image.open(ROOT/arts[name]['path']).resize((252,252),Image.Resampling.LANCZOS); x=(i%5)*280+14;y=(i//5)*340;contact.paste(im,(x,y),im);d.text((x+60,y+265),name,fill='#25453f')
contact.save(str(ROOT/'art_source/art_contact.jpg'))
print('Produced',len(arts),'world assets; 2 atlases')
