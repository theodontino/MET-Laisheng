#!/usr/bin/env python3
"""Linux-only physical mouse smoke test via XTest. No gameplay commands injected.
Run under xvfb-run. Reads test-only button geometry exported by --input-smoke.
"""
import ctypes as C, json, os, subprocess, sys, time
from pathlib import Path
from PIL import ImageGrab
E=Path(os.environ['MET_EVIDENCE_DIR']);E.mkdir(exist_ok=True,parents=True)
user=Path(os.environ.get('XDG_DATA_HOME',str(Path.home()/'.local/share')))/'MET-RiverValley-031'
for p in user.glob('input_smoke031.json*'):p.unlink()
if (E/'input_layout.json').exists():(E/'input_layout.json').unlink()
X=C.CDLL('libX11.so.6');T=C.CDLL('libXtst.so.6')
X.XOpenDisplay.restype=C.c_void_p;display=X.XOpenDisplay(None)
X.XDefaultRootWindow.argtypes=[C.c_void_p];X.XDefaultRootWindow.restype=C.c_ulong
X.XQueryTree.argtypes=[C.c_void_p,C.c_ulong,C.POINTER(C.c_ulong),C.POINTER(C.c_ulong),C.POINTER(C.POINTER(C.c_ulong)),C.POINTER(C.c_uint)]
X.XGetGeometry.argtypes=[C.c_void_p,C.c_ulong,C.POINTER(C.c_ulong),C.POINTER(C.c_int),C.POINTER(C.c_int),C.POINTER(C.c_uint),C.POINTER(C.c_uint),C.POINTER(C.c_uint),C.POINTER(C.c_uint)]
X.XFlush.argtypes=[C.c_void_p]
X.XSetInputFocus.argtypes=[C.c_void_p,C.c_ulong,C.c_int,C.c_ulong]
T.XTestFakeMotionEvent.argtypes=[C.c_void_p,C.c_int,C.c_int,C.c_int,C.c_ulong]
T.XTestFakeButtonEvent.argtypes=[C.c_void_p,C.c_uint,C.c_int,C.c_ulong]
root=X.XDefaultRootWindow(display)
log=(E/'physical-input-runtime.log').open('w')
p=subprocess.Popen([sys.argv[1],'--rendering-method','gl_compatibility','--audio-driver','Dummy','--','--input-smoke'],stdout=log,stderr=subprocess.STDOUT)
checks=[];offset=(0,0)
def check(condition,name):
 checks.append({'name':name,'pass':bool(condition)});print('PASS' if condition else 'FAIL',name)
 if not condition:raise AssertionError(name)
def layout():
 for _ in range(200):
  try:return json.loads((E/'input_layout.json').read_text())
  except (FileNotFoundError,json.JSONDecodeError):time.sleep(.15)
 raise RuntimeError('No input geometry received')
def wait():time.sleep(1.35)
def phase(value):
 for _ in range(20):
  if layout()["phase"]==value:return True
  time.sleep(.5)
 return False
def move(x,y):T.XTestFakeMotionEvent(display,-1,int(x+offset[0]),int(y+offset[1]),0);X.XFlush(display)
def press(down):T.XTestFakeButtonEvent(display,1,int(down),0);X.XFlush(display)
def click_point(x,y):move(x,y);time.sleep(.1);press(True);time.sleep(.08);press(False);wait()
def click(label,starts=False):
 candidates=[b for b in layout()['buttons'] if (b['text'].startswith(label) if starts else b['text']==label) and not b['disabled']]
 check(bool(candidates),'click target: '+label)
 b=candidates[0];print('POINT',b,'OFFSET',offset);click_point(b['x'],b['y'])
try:
 time.sleep(3)
 layout()
 r=C.c_ulong();parent=C.c_ulong();children=C.POINTER(C.c_ulong)();n=C.c_uint()
 X.XQueryTree(display,root,C.byref(r),C.byref(parent),C.byref(children),C.byref(n))
 for i in range(n.value):
  x=C.c_int();y=C.c_int();w=C.c_uint();h=C.c_uint();border=C.c_uint();depth=C.c_uint()
  X.XGetGeometry(display,children[i],C.byref(r),C.byref(x),C.byref(y),C.byref(w),C.byref(h),C.byref(border),C.byref(depth))
  print('WINDOW',children[i],x.value,y.value,w.value,h.value)
  if w.value>=1180 and h.value>=740:
   offset=(x.value,y.value);X.XSetInputFocus(display,children[i],1,0);X.XFlush(display)
 click('从这里开始');check(phase('origin'),'real mouse reaches origin')
 click('接手 · ',True);check(phase('play'),'real mouse chooses origin')
 click('生产');click('基础农场')
 pos=layout()['cells'];click_point(*pos['0,-1'])
 check(len(layout()['selection'])==1,'single tile selected through physical mouse, without zoning')
 click('比较这里的投标');check(any(b['text'].startswith('采用并建设 · ') for b in layout()['buttons']),'real input opens proposal cards')
 before=json.loads(json.loads((user/'input_smoke031.json').read_text())['payload'])
 check(len(before['draft']['offers'])==3,'actual save contains three same-chassis Operator + Bonus offers')
 # Back out, compare a second location, and reopen the SAME quote using physical input.
 original_hand=before['draft']['offers']
 click('返回地图')
 click('重新选位置')
 pos=layout()['cells'];click_point(*pos['1,-1'])
 click('比较这里的投标')
 reopened=json.loads(json.loads((user/'input_smoke031.json').read_text())['payload'])
 check(reopened['draft']['offers']==original_hand and reopened['rng']==before['rng'] and reopened['currency']==before['currency'],'physical back/relocate/reopen is free and does not redraw')
 click('采用并建设 · ',True)
 time.sleep(1)
 state=json.loads(json.loads((user/'input_smoke031.json').read_text())['payload'])
 check(len(state['projects'])==len(before['projects'])+1 and any(v['status']=='construction' for v in state['projects'].values()),'mouse acceptance saved a real construction project')
 ImageGrab.grab(xdisplay=os.environ['DISPLAY']).save(E/'031-real-mouse-construction.png')
 # This does not accelerate clocks or force a result. Click the real DT button.
 click('推进到下一变化')
 state=json.loads(json.loads((user/'input_smoke031.json').read_text())['payload'])
 spent=state['totals']['dt_used']-before['totals']['dt_used']
 rewards=sum(float(x['data'].get('dt',0)) for x in state['log'][len(before['log']):] if x['kind']=='city_outcome')
 check(state['local_hours']>0 and spent>0 ,'real DT action advances local simulation and records actual spending')
 check(not state['debug_used'],'physical input smoke used no debug resources')
 check(all(o['base']==before['draft']['base'] for o in before['draft']['offers']),'mouse tender never substituted a different basic building')
 newest=[b for b in state['projects'].values() if not b['special']][-1]
 check(len(newest['slots'])==3 and newest['operator_org'] in state['organizations'],'chosen Operator is a relationship, alongside three Bonus slots')
 # Actual clicks on the new fixed tabs and socket, not direct game method calls.
 bid=newest['id'];pt=layout()['cells'][newest['tile']];click_point(*pt)
 click('方案')
 snap=json.loads(json.loads((user/'input_smoke031.json').read_text())['payload'])
 for attempt in range(12):
  if any(b['text']=='＋  征集方案' for b in layout()['buttons']):break
  move(1230,620)
  for _ in range(3):
   T.XTestFakeButtonEvent(display,5,1,0);T.XTestFakeButtonEvent(display,5,0,0)
  X.XFlush(display);wait()
 click('＋  征集方案')
 check(any('征集这个槽' in b['text'] for b in layout()['buttons']),'empty socket opens actual slot configuration')
 click('征集这个槽',True)
 check(any('采用' in b['text'] for b in layout()['buttons']),'slot request opens native card tender')
 unchanged=json.loads(json.loads((user/'input_smoke031.json').read_text())['payload'])
 check(unchanged['currency']==snap['currency'] and unchanged['dt']==snap['dt'],'viewing uncommitted slot offers does not charge currency or DT')
 click('返回地图')
 check(any(b['text']=='概况' for b in layout()['buttons']),'free card return restores inspector')
 click('明细');click('概况')
 ImageGrab.grab(xdisplay=os.environ['DISPLAY']).save(E/'031-real-mouse-inspector.png')

finally:
 ImageGrab.grab(xdisplay=os.environ['DISPLAY']).save(E/'physical-last.png')
 p.terminate()
 try:p.wait(timeout=5)
 except subprocess.TimeoutExpired:p.kill();p.wait()
 log.close()
 (E/'physical-input.json').write_text(json.dumps({'checks':checks,'passed':sum(c['pass'] for c in checks),'failed':sum(not c['pass'] for c in checks),'method':'XTest mouse events into real Godot X11 window; geometry read-only; dedicated test save'},ensure_ascii=False,indent=2))
