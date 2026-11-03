import test from 'node:test';
import assert from 'node:assert/strict';
import {makeScenario,runCommand,summary} from '../src/model';
import {enableWildSession} from '../src/game/wild-runtime';
import {hopeTechnologyStatus} from '../src/game/wild-trade';
import {designCapacity} from '../src/game/star';
import {createWildDesign,wildDesignRules} from '../src/game/wild-design';
import {encodeSave,decodeSave} from '../src/game/storage';
const initial=()=>enableWildSession(makeScenario('normal'));
const command=(s:ReturnType<typeof initial>,action:any)=>{const r=runCommand(s,action);assert.equal(r.ok,true,r.message);return r.state};

test('only completed HopeCity material exports unlock technology; cancellation and ordinary orders do not',()=>{
 let s=initial();
 s=command(s,{type:'create-order',counterparty:'hope',resource:'material',side:'sell',quantity:10});
 assert.equal(hopeTechnologyStatus(s).unlocked,false);
 s=command(s,{type:'cancel-order',id:s.orders[0].id});
 s=command(s,{type:'create-order',resource:'material',side:'sell',quantity:10});
 s=command(s,{type:'time',paused:false});s=command(s,{type:'advance-event'});
 assert.equal(hopeTechnologyStatus(s).sold,0);
 s=command(s,{type:'create-order',counterparty:'hope',resource:'material',side:'sell',quantity:4});
 s=command(s,{type:'advance-event'});assert.equal(hopeTechnologyStatus(s).sold,4);assert.equal(hopeTechnologyStatus(s).unlocked,false);
 s=command(s,{type:'create-order',counterparty:'hope',resource:'material',side:'sell',quantity:6});
 s=command(s,{type:'advance-event'});assert.equal(hopeTechnologyStatus(s).unlocked,true);
 s=command(s,{type:'advance-event'});assert.equal(hopeTechnologyStatus(s).sold,10,'filled orders are not counted twice');
});

test('technology purchase charges only currency once and gives existing/future buildings capacity',()=>{
 let s=initial();s.wild!.hopeMaterialSold=10;
 const before=structuredClone(s);
 s=command(s,{type:'wild-buy-housing-technology'});
 assert.equal(s.resources.currency,before.resources.currency-60);assert.equal(s.resources.material,before.resources.material);
 assert.equal(s.wild!.housingResearch,1);assert.equal(designCapacity(createWildDesign(1),wildDesignRules(1)),4);
 assert.equal(designCapacity(createWildDesign(2),wildDesignRules(1)),2);
 for(const d of Object.values(s.wild!.designs))assert.equal(designCapacity(d,wildDesignRules(1)),d.baseCapacity+2);
 assert.equal(runCommand(s,{type:'wild-buy-housing-technology'}).ok,false);
 const restored=decodeSave(encodeSave(s,{uiScale:100,fontScale:100})).game;
 assert.equal(hopeTechnologyStatus(restored).owned,true);assert.equal(hopeTechnologyStatus(restored).sold,10);
 assert.equal(runCommand(restored,{type:'wild-buy-housing-technology'}).ok,false);
});

test('purchase requires trade, enough money, writable city and no running research of the same technology',()=>{
 for(const [label,change] of [
  ['trade',(s:any)=>{}],['funds',(s:any)=>{s.wild.hopeMaterialSold=10;s.resources.currency=59}],
  ['readonly',(s:any)=>{s.wild.hopeMaterialSold=10;s.readonly=true}],['research',(s:any)=>{s.wild.hopeMaterialSold=10;s.wild.researchProgress=1}],
 ] as const){const s=initial();change(s);const original=structuredClone(s);const r=runCommand(s,{type:'wild-buy-housing-technology'});assert.equal(r.ok,false,label);assert.deepEqual(s,original);}
});

test('legacy Bonus commands cannot bypass Wild paid slots',()=>{
 const s=initial(),tile=s.tiles.find(t=>t.building?.kind==='farm')!;
 const before=summary(s);
 const r=runCommand(s,{type:'install-bonus',tileId:tile.id,slotId:'production',bonusId:'harvest'});
 assert.equal(r.ok,false);assert.deepEqual(summary(r.state),before);
 assert.equal(runCommand(s,{type:'research-bonus',id:'harvest'}).ok,false);
});
