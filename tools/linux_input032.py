#!/usr/bin/env python3
"""Physical XTest input into native Godot. The geometry channel is read-only.
Uses a dedicated smoke-test save, never the player's current save.
"""
import ctypes as C, json, os, subprocess, sys, time
from pathlib import Path
from PIL import ImageGrab
E=Path(os.environ['MET_EVIDENCE_DIR']);E.mkdir(parents=True,exist_ok=True)
user=Path(os.environ.get('XDG_DATA_HOME',str(Path.home()/'.local/share')))/'MET-RiverValley-032'
for p in user.glob('input_smoke032.json*'):p.unlink()
(E/'input_layout.json').unlink(missing_ok=True)
X=C.CDLL('libX11.so.6');T=C.CDLL('libXtst.so.6');X.XOpenDisplay.restype=C.c_void_p;d=X.XOpenDisplay(None)
assert d,'No X11 display'
X.XDefaultRootWindow.argtypes=[C.c_void_p];X.XDefaultRootWindow.restype=C.c_ulong
X.XQueryTree.argtypes=[C.c_void_p,C.c_ulong,C.POINTER(C.c_ulong),C.POINTER(C.c_ulong),C.POINTER(C.POINTER(C.c_ulong)),C.POINTER(C.c_uint)]
X.XGetGeometry.argtypes=[C.c_void_p,C.c_ulong,C.POINTER(C.c_ulong),C.POINTER(C.c_int),C.POINTER(C.c_int),C.POINTER(C.c_uint),C.POINTER(C.c_uint),C.POINTER(C.c_uint),C.POINTER(C.c_uint)]
X.XFlush.argtypes=[C.c_void_p];X.XSetInputFocus.argtypes=[C.c_void_p,C.c_ulong,C.c_int,C.c_ulong]
T.XTestFakeMotionEvent.argtypes=[C.c_void_p,C.c_int,C.c_int,C.c_int,C.c_ulong]
T.XTestFakeButtonEvent.argtypes=[C.c_void_p,C.c_uint,C.c_int,C.c_ulong]
root=X.XDefaultRootWindow(d);offset=(0,0);checks=[]
log=(E/'physical-runtime.log').open('w')
p=subprocess.Popen([sys.argv[1],'--audio-driver','Dummy','--','--input032'],stdout=log,stderr=subprocess.STDOUT)
def check(ok,name):
 checks.append({'pass':bool(ok),'name':name});print(('PASS ' if ok else 'FAIL ')+name,flush=True)
 if not ok:raise AssertionError(name)
def geo():
 for _ in range(160):
  try:return json.loads((E/'input_layout.json').read_text())
  except (OSError,json.JSONDecodeError):time.sleep(.15)
 raise RuntimeError('No read-only geometry dump')
def wait(t=1.25):time.sleep(t)
def state():
 for _ in range(40):
  try:return json.loads(json.loads((user/'input_smoke032.json').read_text())['payload'])
  except (OSError,json.JSONDecodeError):wait(.1)
 raise RuntimeError('No test save')
def move(x,y):T.XTestFakeMotionEvent(d,-1,int(x+offset[0]),int(y+offset[1]),0);X.XFlush(d)
def press(down):T.XTestFakeButtonEvent(d,1,int(down),0);X.XFlush(d)
def point(x,y):move(x,y);wait(.1);press(True);wait(.08);press(False);wait()
def find(key,by='name',starts=False):
 bs=[b for b in geo()['buttons'] if (b.get(by,'').startswith(key) if starts else b.get(by)==key) and not b['disabled']]
 return bs[0] if bs else None
def click(key,by='name',starts=False):
 b=find(key,by,starts);check(b is not None,'click '+key);point(b['x'],b['y'])
def wheel(n=4,x=1320,y=590):
 move(x,y)
 for _ in range(abs(n)):
  key=5 if n>0 else 4;T.XTestFakeButtonEvent(d,key,1,0);T.XTestFakeButtonEvent(d,key,0,0)
 X.XFlush(d);wait()
def seek(key,by='name'):
 for _ in range(18):
  if find(key,by):return
  wheel(3)
 raise AssertionError('Cannot scroll to '+key)
def shot(name):ImageGrab.grab(xdisplay=os.environ['DISPLAY']).crop((offset[0],offset[1],offset[0]+1440,offset[1]+900)).save(E/(name+'.png'))
def close():click('CityPageClose032')
try:
 wait(3);geo()
 rr=C.c_ulong();par=C.c_ulong();children=C.POINTER(C.c_ulong)();n=C.c_uint();X.XQueryTree(d,root,C.byref(rr),C.byref(par),C.byref(children),C.byref(n))
 for i in range(n.value):
  x=C.c_int();y=C.c_int();w=C.c_uint();h=C.c_uint();border=C.c_uint();depth=C.c_uint()
  X.XGetGeometry(d,children[i],C.byref(rr),C.byref(x),C.byref(y),C.byref(w),C.byref(h),C.byref(border),C.byref(depth))
  if w.value==1440 and h.value==900:offset=(x.value,y.value);X.XSetInputFocus(d,children[i],1,0);X.XFlush(d)
 click('从这里开始','text');click('接手 · ','text',True);check(geo()['phase']=='play','physical origin selection')
 click('Budget032');check(geo()['desk']=='finance','budget is a real detail entry');shot('physical-budget-new');close()
 for r in ['food','material','goods','energy']:
  click('Resource_'+r);check(geo()['desk']=='resource' and geo()['layer']==r,'resource opens corresponding details and overlay '+r);close()
 click('Population032');check(geo()['desk']=='population' and geo()['layer']=='population','population opens joint housing/employment overlay')
 click('CityPrimary032');check(find('‹ 人口总览','text') is not None,'population needs drilldown is available');shot('physical-population-needs');close()
 click('DTDetails032');check(geo()['desk']=='time','DT opens actual time control details');close()
 click('Pause032');check(geo()['paused'],'pause click stores manual pause')
 before=state();wait(2)
 check(any(b['name']=='Advance0_5' and b['disabled'] for b in geo()['buttons']),'paid advance button disabled while paused')
 check(state()['local_hours']==before['local_hours'],'paused city did not run')
 click('Pause032');click('Advance0_5');after=state()
 check(after['local_hours']>before['local_hours'] and after['totals']['dt_used']>before['totals']['dt_used'],'top-right paid advance really runs and spends DT')
 check(len(after['civic032']['history'])>=5,'actual cycles now appear in budget history')
 click('Budget032');click('预算余额','text');shot('physical-budget-history');close()
 click('Policies032');check(geo()['desk']=='policies','single policy page entered')
 # Test actual background drag, not invoking ScrollContainer methods.
 oldscroll=geo()['scroll'];move(1340,600);wait(.2);press(True)
 for y in range(600,230,-30):move(1340,y);wait(.04)
 press(False);wait();check(geo()['scroll']>oldscroll,'policy page physically drags to lower categories')
 shot('physical-policy-drag');wheel(-30,x=1340,y=250)
 click('Policy_tax_production' if find('Policy_tax_production') else 'Policy_tax_output' if find('Policy_tax_output') else '生产税','name' if find('Policy_tax_production') or find('Policy_tax_output') else 'text')
 wait();before=state();click('CityPrimary032');after=state()
 check(abs(after['currency']-before['currency']+16)<.00001 and after['dt']==before['dt'],'policy confirmation only charges existing money cost')
 check(after['rng']==before['rng'] and geo()['desk']=='policies','policy change preserves RNG and remains on same page');close()
 click('Tasks032');seek('TaskAbandon_housing');click('TaskAbandon_housing')
 check(state()['civic032']['tasks']['housing']['status']=='active','abandon confirmation does not yet change task')
 click('CityPrimary032');check(state()['civic032']['tasks']['housing']['status']=='abandoned','confirm abandonment changes persisted task state')
 click('当前可接','text');seek('TaskAccept_housing');click('TaskAccept_housing');check(state()['civic032']['tasks']['housing']['status']=='active','reaccept from available tasks')
 click('历史任务','text');shot('physical-task-history');close()
 # Construction and existing card workflow remain usable with the relocated HUD.
 click('生产','text');click('基础农场','text');point(*geo()['cells']['0,-1'])
 click('比较这里的投标','text');check(any('采用并建设' in b['text'] for b in geo()['buttons']),'native card tender still reachable')
 before=state();click('采用并建设 · ','text',True);after=state()
 check(len(after['projects'])==len(before['projects'])+1 and after['currency']<before['currency'],'actual tender confirms building, no mock action')
 click('Advance032');check(state()['local_hours']>after['local_hours'],'relocated next-change button completes actual advance')
 check(not state()['debug_used'],'all physical interactions without debug resources')
 shot('physical-final')
finally:
 try:shot('physical-last')
 except Exception:pass
 p.terminate()
 try:p.wait(timeout=5)
 except subprocess.TimeoutExpired:p.kill();p.wait()
 log.close()
 (E/'physical032.json').write_text(json.dumps({'checks':checks,'passed':sum(c['pass'] for c in checks),'failed':sum(not c['pass'] for c in checks),'method':'XTest mouse events; read-only geometry; dedicated032 save'},ensure_ascii=False,indent=2))
