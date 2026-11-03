from pathlib import Path
import json,re,itertools
root=Path(__file__).resolve().parents[4]
w=json.loads((root/'content/beta/wild.json').read_text())
s=(root/'unity/METLivingWorld/Assets/MetCore/City/CityWild.cs').read_text()
assert re.search(r'WildHousingRequiredExports\s*=\s*10\s*;',s)
assert re.search(r'WildHousingCost\s*=\s*60\s*;',s)
for a,b in [('compound','housing'),('housing','organization')]:
 assert any(e['from']==a and e['to']==b and not e['bypass'] for e in w['routes'])
assert w['nodes']['compound']['dp']+w['nodes']['housing']['dp']+w['slot_dp'][0]==5
assert w['initial_dp']+w['technology_dp_per_level']==4
assert w['initial_dp']+2*w['technology_dp_per_level']==6
assert list(itertools.accumulate(w['slot_dp']))==[3,12,39]
assert [(w['forms'][k]['housing'],w['forms'][k]['food']) for k in ['homestead','residence','agriculture']]==[(10,10),(20,5),(5,20)]
assert 'docs/' not in (root/'tools/sync_unity_content.py').read_text()
print('PASS: source I 10/60; actual organization path 5; capacities 4/6 (II hypothetical); slots cumulative 3/12/39; forms; sync excludes docs. Static/spec only.')
