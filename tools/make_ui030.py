from pathlib import Path
import json, math, wave, struct
R=Path(__file__).resolve().parents[1]; A=R/'assets/ui030'; A.mkdir(parents=True,exist_ok=True)
paths={
'home':'M4 14 16 4 28 14 M7 13v15h18V13 M13 28V18h6v10',
'build':'M4 26 22 8l3 3L7 29Z M17 5l6-2 6 6-2 6-4-4-3 1Z',
'production':'M4 28V13l8 4v-6l9 5V7h5v21Z M8 23h3m4 0h3m4 0h2',
'logistics':'M3 8h17v16H3Z M20 13h5l4 5v6h-9 M7 25a3 3 0 1 0 6 0 M21 25a3 3 0 1 0 6 0',
'operations':'M16 3a6 6 0 1 0 0 12a6 6 0 1 0 0-12 M5 29v-6q0-6 11-6t11 6v6',
'food':'M16 29V6 M16 13Q6 12 8 5Q16 5 16 13 M16 19Q26 18 24 11Q16 11 16 19 M16 25Q6 24 8 17Q16 17 16 25',
'material':'M4 12 16 5 28 12 16 19Z M4 12v12l12 7 12-7V12 M16 19v12',
'goods':'M5 7h22v22H5Z M5 7l4-4h14l4 4 M12 7v8l4-2 4 2V7',
'energy':'M18 2 6 18h9l-1 12 13-18h-9Z',
'medical':'M16 28C-4 15 4 1 16 10C28 1 36 15 16 28Z',
'education':'M3 6q7-3 13 1q6-4 13-1v22q-7-3-13 1q-6-4-13-1Z M16 7v22',
'commercial':'M4 12h24v17H4Z M2 12l4-9h20l4 9 M2 12q3 6 7 0q3 6 7 0q3 6 7 0q4 6 7 0 M12 29V19h8v10',
'entertainment':'M16 3 6 15h5l-8 8h11v7h4v-7h11l-8-8h5Z',
'pollution':'M5 23q-6-7 2-10q-1-10 10-8q8-5 11 4q9 1 4 9q4 8-5 9H8 M10 15v4m7-8v7m7-4v7',
'heat':'M8 29q-6-6 0-12t0-13 M16 29q-6-6 0-12t0-13 M24 29q-6-6 0-12t0-13',
'employment':'M3 10h26v18H3Z M11 10V5h10v5 M3 17h26 M13 17v4h6v-4',
'layers':'M2 10 16 3 30 10 16 17Z M3 17l13 7 13-7 M3 23l13 7 13-7',
'eye':'M2 16q14-19 28 0q-14 19-28 0Z M16 11a5 5 0 1 0 0 10a5 5 0 1 0 0-10',
'focus':'M11 3H3v8 M21 3h8v8 M3 21v8h8 M29 21v8h-8 M16 10a6 6 0 1 0 0 12a6 6 0 1 0 0-12',
'world':'M4 7 12 3 21 7 28 3v22l-7 4-9-4-8 4Z M12 3v22 M21 7v22',
'lock':'M7 14h18v15H7Z M11 14V8a5 5 0 0 1 10 0v6 M16 19v5',
'chevron':'M11 6l10 10-10 10',
'back':'M21 6 11 16l10 10',
'close':'M7 7l18 18 M25 7 7 25',
'check':'M5 16l7 7L27 6',
'warning':'M16 3 30 28H2Z M16 11v8 M16 23v1',
'clock':'M16 3a13 13 0 1 0 0 26a13 13 0 1 0 0-26 M16 7v9l7 4',
'play':'M9 4v24l19-12Z',
'pause':'M9 5h4v22H9Z M20 5h4v22h-4Z',
'ledger':'M5 3h22v26H5Z M10 9h12 M10 15h12 M10 21h6',
'policy':'M4 29h24 M6 12h20 M3 10 16 3 29 10Z M8 14v10 M16 14v10 M24 14v10',
'settings':'M4 8h24 M4 16h24 M4 24h24 M11 4v8 M22 12v8 M9 20v8'
}
for n,p in paths.items():
 (A/f'{n}.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32"><path d="{p}" fill="none" stroke="#24473f" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>')
ops=json.loads((R/'content/core020.json').read_text())['operators'];print(type(ops))
# Source operators are keyed dictionaries or arrays. Original simple heraldic geometry, not third-party marks.
ids=list(ops) if isinstance(ops,dict) else [x['id'] for x in ops]
for i,oid in enumerate(ids):
 col=['417c71','708857','96734d','697f97','b1825b','779aaa','987379','778171'][i%8]
 emblem=list(paths.values())[i%12]
 (A/f'org_{oid}.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 48 48"><path d="M24 2L43 12v23L24 46 5 35V12Z" fill="#{col}"/><g transform="translate(10 8) scale(.87)" fill="none" stroke="#fff9e9" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="{emblem}"/></g></svg>')
# Short original tones; no external music assets.
audio=R/'assets/audio030';audio.mkdir(exist_ok=True)
for name,freqs in [('select',[660]),('confirm',[523.25,659.25,784]),('complete',[523.25,784,1046.5]),('notice',[440,587.33]),('layer',[392,523.25]),('back',[523.25,392])]:
 samples=[];rate=22050
 for f in freqs:
  for i in range(int(rate*.09)):
   t=i/rate;env=min(1,t/.008)*math.exp(-t*32);samples.append(int(2100*env*(math.sin(math.tau*f*t)+.23*math.sin(math.tau*f*2*t))))
 with wave.open(str(audio/f'{name}.wav'),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes(b''.join(struct.pack('<h',v) for v in samples))
print('32 SVG icons,',len(ids),'badges, 6 sounds')
