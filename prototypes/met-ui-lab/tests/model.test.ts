import test from 'node:test';
import assert from 'node:assert/strict';
import { makeScenario, runCommand, summary, conditions, comparisons, bonusComparison, tileFacts, themesCatalog, buildingChoices } from '../src/model';
import type { GameState } from '../src/types';
const run=(s:GameState,type:string,extra={})=>{const r=runCommand(s,{type,...extra});assert.equal(r.ok,true,r.message);return r.state;};
const advance=(s:GameState)=>run(run(s,'time',{paused:false}),'advance-event');
test('fixtures are independent and aggregate complete city totals',()=>{
 const a=makeScenario('normal'),b=makeScenario('normal');a.resources.food=0;assert.equal(b.resources.food,180);assert.equal(summary(b).population,48);assert.ok(b.tiles.length>=19);assert.ok(summary(b).net.food>0);
 const first=makeScenario('first');assert.equal(first.settled,false);assert.equal(first.records.some(r=>r.category==='settlement'),false);
 assert.equal(makeScenario('shortage').tiles.filter(t=>t.building&&!t.building.running).length,3);
});
test('orders reserve assets, cancellation returns escrow, only explicit advance fills',()=>{
 let s=makeScenario('normal');const cash=s.resources.currency,food=s.resources.food;
 s=run(s,'create-order',{resource:'food',side:'buy',quantity:10});assert.equal(s.resources.currency,cash-30);assert.equal(s.resources.food,food);assert.equal(s.orders[0].status,'open');assert.equal(runCommand(s,{type:'advance-event'}).ok,false);
 s=run(s,'cancel-order',{id:s.orders[0].id});assert.equal(s.resources.currency,cash);assert.equal(s.orders[0].status,'cancelled');
 s=run(s,'trade',{resource:'material',side:'sell',quantity:10});assert.equal(s.resources.material,250);s=run(s,'cancel-order',{id:s.orders[1].id});assert.equal(s.resources.material,260);
 s=run(s,'create-order',{resource:'food',side:'buy',quantity:10});s.migrationPaused=true;const flow=summary(s).net.food;s=advance(s);assert.equal(s.resources.food,food+10+flow);assert.equal(s.orders[2].status,'filled');
});
test('construction has no new output before completion, upgrade retains 65 percent',()=>{
 let s=makeScenario('normal');s.migrationPaused=true;const empty=s.tiles.find(t=>!t.building)!;const before=summary(s);
 s=run(s,'build',{tileId:empty.id,kind:'farm',operator:'街区公营'});assert.equal(summary(s).net.food,before.net.food);assert.equal(s.resources.currency,820);
 s=advance(s);assert.equal(s.projects[0].doneCP,6);assert.equal(s.tiles.find(t=>t.id===empty.id)!.building,undefined);
 s=advance(s);assert.equal(s.projects.length,0);assert.equal(summary(s).net.food,before.net.food+32);assert.equal(s.tiles.find(t=>t.id===empty.id)!.building!.population,0);
 const farm=s.tiles.find(t=>t.building?.kind==='farm')!;const total=summary(s).net.food;s=run(s,'upgrade',{tileId:farm.id,specialization:'irrigation'});assert.equal(summary(s).net.food,total-32*.35);
 assert.ok(comparisons(s,farm.id,undefined,'irrigation').some(r=>r.label==='食物月净流'&&r.after>r.before));
});
test('crew price doubles and contribution expires after one month without refund',()=>{
 let s=makeScenario('normal');s=run(s,'hire-crew');s=run(s,'hire-crew');assert.equal(s.resources.currency,720);assert.equal(summary(s).cpCrews,8);
 const empty=s.tiles.find(t=>!t.building)!;s=run(s,'build',{tileId:empty.id,kind:'farm'});const before=s.resources.currency,flow=summary(s).net.currency;s=advance(s);assert.equal(s.projects.length,0);assert.equal(s.crews.length,0);assert.equal(summary(s).cpCrews,0);assert.equal(s.resources.currency,before+flow);
});
test('bonus stock, installation and research update actual city totals',()=>{
 let s=makeScenario('normal');const farm=s.tiles.find(t=>t.building?.kind==='farm')!,before=summary(s).net.food;
 assert.equal(bonusComparison(s,farm.id,'production','harvest').find(r=>r.label==='食物月净流')!.after,before+12);
 s=run(s,'install-bonus',{tileId:farm.id,slotId:'production',bonusId:'harvest'});assert.equal(s.bonuses.harvest,0);assert.equal(summary(s).net.food,before+12);
 assert.equal(runCommand(s,{type:'install-bonus',tileId:farm.id,slotId:'logistics',bonusId:'harvest'}).ok,false);
 s=run(s,'research-bonus',{id:'harvest'});assert.equal(s.bonusLevels.harvest,2);assert.equal(summary(s).net.food,before+24);
});
test('stage records and rewards require explicit confirmation',()=>{
 let s=makeScenario('normal');assert.ok(conditions(s).every(c=>c.met));assert.deepEqual(s.completedStages,[]);assert.equal(s.rewards.length,0);
 s=run(s,'complete-stage');assert.deepEqual(s.completedStages,[1]);assert.equal(s.stage,2);assert.equal(s.holdMonths,0);assert.equal(s.rewards.length,1);
 const id=s.rewards[0].id;s=run(s,'claim-reward',{id,bonusId:'harvest'});assert.equal(s.bonuses.harvest,2);assert.equal(s.rewards.length,0);assert.equal(runCommand(s,{type:'claim-reward',id,bonusId:'harvest'}).ok,false);assert.equal(runCommand(s,{type:'complete-stage'}).ok,false);
});
test('contract payouts require explicit stock-backed delivery',()=>{
 let s=makeScenario('normal');s=run(s,'accept-contract',{id:'c1'});const before=s.resources.currency;s=run(s,'deliver-contract',{id:'c1'});assert.equal(s.resources.food,120);assert.equal(s.resources.currency,before+100);assert.equal(s.contracts[0].status,'completed');assert.equal(s.rewards.length,1);assert.equal(runCommand(s,{type:'deliver-contract',id:'c1'}).ok,false);
});
test('ending records preserve playability while readonly fixture forbids mutations',()=>{
 assert.equal(runCommand(makeScenario('normal'),{type:'name-block',name:'过早命名'}).ok,false);
 let s=makeScenario('ending');assert.equal(runCommand(s,{type:'archive-block'}).ok,false);s=run(s,'name-block',{name:'河湾新城'});s=run(s,'choose-capstone',{tileId:s.tiles.find(t=>t.building)!.id});s=run(s,'archive-block');assert.equal(s.readonly,false);s=advance(s);assert.equal(s.dt,0);s=makeScenario('readonly');
 for(const a of [{type:'hire-crew'},{type:'time',paused:false},{type:'create-order',resource:'food',side:'buy',quantity:1},{type:'apply-policy',id:'food'}]){const r=runCommand(s,a);assert.equal(r.ok,false);assert.deepEqual(r.state,s);}
 assert.equal(runCommand(s,{type:'new-game',name:'新开拓'}).state.readonly,false);
});

test('DT is spent only on acceleration; zero DT can continue and hire crews',()=>{
 let s=makeScenario('normal');const dt=s.dt;s=advance(s);assert.equal(s.dt,dt);s=run(s,'time',{speed:2});s=advance(s);assert.equal(s.dt,dt-1);
 s=makeScenario('ending');s=run(s,'hire-crew');s=advance(s);assert.equal(s.dt,0);assert.equal(s.month,17);
});
test('CP reflects power, material, and fractional crew time; reserved engineering stays zero',()=>{
 let s=makeScenario('normal');const empty=s.tiles.find(t=>!t.building)!;s=run(s,'build',{tileId:empty.id,kind:'farm'});s.crews=[{id:'partial',cp:4,remainingMonths:.25}];assert.equal(summary(s).cpCrews,1);assert.equal(summary(s).cpReserved,0);
 s.resources.material=2.5;assert.equal(summary(s).cpUsed,1);s.tiles.find(t=>t.building?.kind==='farm')!.building!.running=false;assert.ok(summary(s).cpTotal<7);
});
test('upgrade can reduce secondary housing without deleting residents',()=>{
 let s=makeScenario('normal');s.migrationPaused=true;const tile=s.tiles.find(t=>t.building?.kind==='farm')!;s=run(s,'upgrade',{tileId:tile.id,specialization:'irrigation'});for(let i=0;i<8&&s.projects.length;i++)s=advance(s);const b=s.tiles.find(t=>t.id===tile.id)!.building!;assert.equal(b.tier,2);assert.equal(b.population,8);assert.equal(b.housing,6);
});

test('tile facts separate empty spatial reach, actual service satisfaction and local consumption',()=>{
 const s=makeScenario('normal');const empty=s.tiles.find(t=>!t.building)!,farm=s.tiles.find(t=>t.building?.kind==='farm')!;assert.ok(empty.reach.medical>0);assert.equal(tileFacts(s,empty.id).services.medical,0);assert.equal(tileFacts(s,empty.id).net.food,0);const facts=tileFacts(s,farm.id);assert.equal(facts.services.medical,.9);assert.equal(facts.net.food,32-8*.8);
});
test('Bonus replacement consumes the new copy and destroys the previous installation',()=>{
 let s=makeScenario('reward');const farm=s.tiles.find(t=>t.building?.kind==='farm')!;s=run(s,'install-bonus',{tileId:farm.id,slotId:'production',bonusId:'harvest'});assert.equal(s.bonuses.harvest,1);s=run(s,'install-bonus',{tileId:farm.id,slotId:'production',bonusId:'harvest'});assert.equal(s.bonuses.harvest,0);assert.equal(summary(s).net.food,summary(makeScenario('reward')).net.food+12);
});

test('zero DT permits construction but ending qualifications require all three stages',()=>{
 let s=makeScenario('normal');s.dt=0;assert.equal(runCommand(s,{type:'name-block',name:'未达标'}).ok,false);assert.equal(runCommand(s,{type:'choose-capstone',tileId:s.tiles.find(t=>t.building)!.id}).ok,false);
 const empty=s.tiles.find(t=>!t.building)!;s=run(s,'build',{tileId:empty.id,kind:'farm'});const farm=s.tiles.find(t=>t.building?.kind==='farm')!;s=run(s,'upgrade',{tileId:farm.id,specialization:'irrigation'});assert.equal(s.projects.length,2);
});

test('every fixture has a resident-backed allocation of workers and filled jobs',()=>{
 for(const id of ['first','normal','shortage','construction','reward','ending','readonly'] as const){const s=makeScenario(id),stats=summary(s);assert.ok(stats.employed<=stats.population);assert.ok(stats.filledJobs<=stats.jobs);assert.equal(stats.employed,stats.filledJobs);for(const t of s.tiles)if(t.building){assert.ok(t.building.employed<=t.building.population);assert.ok(t.building.filledJobs<=t.building.jobs);}}
 const first=summary(makeScenario('first'));assert.equal(first.population,24);assert.equal(first.employed,12);
});
test('ended fixtures expire old contracts and stale commands check deadlines immediately',()=>{
 for(const id of ['ending','readonly'] as const)assert.ok(makeScenario(id).contracts.every(c=>c.status==='expired'));
 let s=makeScenario('normal');s.month=10;const before=structuredClone(s.resources);assert.equal(runCommand(s,{type:'accept-contract',id:'c1'}).ok,false);s.contracts[0].status='active';const out=runCommand(s,{type:'deliver-contract',id:'c1'});assert.equal(out.ok,false);assert.deepEqual(out.state.resources,before);
 s.month=9;s=run(s,'deliver-contract',{id:'c1'});assert.equal(s.contracts[0].status,'completed');
});
test('owned reward Bonus copies always expose a researchable blueprint',()=>{
 let s=makeScenario('reward');assert.equal(s.bonusLevels.craft,1);const money=s.resources.currency;s=run(s,'research-bonus',{id:'craft'});assert.equal(s.bonusLevels.craft,2);assert.equal(s.resources.currency,money-80);
});
test('orchard goods follow the advertised 50 percent secondary output then zero at T3',()=>{
 let s=makeScenario('normal');s.migrationPaused=true;const tile=s.tiles.find(t=>t.building?.kind==='farm')!;s=run(s,'upgrade',{tileId:tile.id,specialization:'orchard'});for(let i=0;i<8&&s.projects.length;i++)s=advance(s);let b=s.tiles.find(t=>t.id===tile.id)!.building!;assert.equal(b.tier,2);assert.equal(b.flows.goods,8*1.6*.5);
 s=run(s,'upgrade',{tileId:tile.id,specialization:'orchard'});for(let i=0;i<8&&s.projects.length;i++)s=advance(s);b=s.tiles.find(t=>t.id===tile.id)!.building!;assert.equal(b.tier,3);assert.equal(b.flows.goods,0);assert.ok(buildingChoices('farm',2).find(c=>c.id==='orchard')!.effects.some(e=>e.includes('50%')));
});
test('five themes change actual goals and available contract direction, retaining accepted work',()=>{
 assert.equal(themesCatalog.length,5);const goalIds=new Set<string>();for(const theme of themesCatalog){const s=run(makeScenario('normal'),'choose-theme',{theme:theme.name});const c=conditions(s,3).find(c=>c.id.startsWith('theme-'))!;goalIds.add(c.id);assert.ok(c.label.includes(theme.name));assert.equal(c.target,theme.targets[2]);assert.equal(s.contracts[0].resource,theme.contracts[0].resource);assert.equal(s.contracts[0].quantity,theme.contracts[0].quantity);assert.equal(s.holdMonths,0);}assert.equal(goalIds.size,5);
 let s=run(makeScenario('normal'),'accept-contract',{id:'c1'});const accepted=structuredClone(s.contracts[0]);s=run(s,'choose-theme',{theme:'工业协作'});assert.deepEqual(s.contracts[0],accepted);assert.equal(s.contracts[1].resource,'goods');assert.equal(runCommand(s,{type:'choose-theme',theme:'虚构主题'}).ok,false);
 s=run(s,'choose-theme',{theme:'生态共生'});s.tiles[0].pollution=100;assert.equal(conditions(s,3).find(c=>c.id==='theme-pollution')!.met,false);
});
test('city service satisfaction equals population-weighted reachable allocation',()=>{
 const s=makeScenario('normal');const home=s.tiles.find(t=>t.building)!;home.reach.medical=.2;const stats=summary(s);const allocated=s.tiles.reduce((sum,t)=>sum+tileFacts(s,t.id).services.medical*(t.building?.population||0),0)/stats.population;assert.ok(Math.abs(stats.services.medical-allocated)<1e-10);assert.ok(stats.services.medical<.9);assert.equal(tileFacts(s,home.id).services.medical,.2);
});
