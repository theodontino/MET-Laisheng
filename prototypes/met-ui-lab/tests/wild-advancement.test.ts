import test from 'node:test';
import assert from 'node:assert/strict';
import { effectiveBuilding, makeScenario, runCommand } from '../src/model';
import { designCapacity, designUsed } from '../src/game/star';
import { wildDesignRules } from '../src/game/wild-design';
import { enableWildSession, quoteWildNode } from '../src/game/wild-runtime';
import { decodeSave, encodeSave } from '../src/game/storage';
import type { DemoAction, GameState } from '../src/types';

function execute(state:GameState, action:DemoAction):GameState {
 const result=runCommand(state,action);
 assert.equal(result.ok,true,result.message);
 return result.state;
}
function prepared(branch:'housing'|'primary') {
 let state=enableWildSession(makeScenario('normal'));
 state.paused=false;
 state.migrationPaused=true;
 // Use the confirmed one-time opening supply; no industrial production is assumed.
 state.wild!.housingResearch=1;
 const tileId=state.tiles.find(t=>t.building?.kind==='farm')!.id;
 for(const nodeId of ['t1-foundation','t1-'+branch]){
  state=execute(state,{type:'wild-start-node',tileId,nodeId});
  state=execute(state,{type:'advance-event'});
 }
 return {state,tileId};
}

for(const [branch,form,housing,food] of [
 ['housing','apartment',24,5.5],['primary','farm',5.5,24],
] as const)test(`${form}: inherited percentages apply to confirmed T2 bases only after completion`,()=>{
 const fixture=prepared(branch),tileId=fixture.tileId;
 let state=fixture.state;
 const before=structuredClone(state),nodeId='t1-'+form;
 const quote=quoteWildNode(state,tileId,nodeId);
 assert.equal(quote.allowed,true);assert.equal(quote.pointCost,2);assert.equal(quote.constructionCP,2);
 assert.deepEqual(quote.resourceCost,{lowIndustrial:1});
 assert.deepEqual(state,before,'quoting cannot change the building or inventory');
 state=execute(state,{type:'wild-start-node',tileId,nodeId});
 assert.equal(state.wild!.lowIndustrial,1);
 assert.equal(state.tiles.find(t=>t.id===tileId)!.building!.tier,1);
 assert.deepEqual(state.wild!.unlockedForms,[],'starting the project cannot unlock direct construction');
 assert.equal(quoteWildNode(state,tileId,nodeId).reserved,2);
 state=execute(state,{type:'advance-event'});
 const tile=state.tiles.find(t=>t.id===tileId)!,building=effectiveBuilding(state,tile)!;
 assert.equal(building.tier,2);assert.equal(building.housing,housing);assert.equal(building.flows.food,food);
 const design=state.wild!.designs[building.id];
 assert.equal(designCapacity(design,wildDesignRules(1)),6);
 assert.equal(designUsed(design),4);
 assert.equal(design.advancementPointCosts['tier-2'],2);
 assert.equal(design.advancementNodes![2],nodeId);
 assert.deepEqual(state.wild!.unlockedForms,['farm-t2-'+form]);
 assert.equal(state.projects.length,0);
 const restored=decodeSave(encodeSave(state,{uiScale:100,fontScale:100})).game;
 assert.deepEqual(restored.wild,state.wild);
 assert.equal(effectiveBuilding(restored,restored.tiles.find(t=>t.id===tileId)!)!.housing,housing);
 const repeated=runCommand(restored,{type:'wild-start-node',tileId,nodeId});
 assert.equal(repeated.ok,false);assert.equal(repeated.state,restored);
});

test('advancement needs real industrial stock and a valid route; failure preserves all state',()=>{
 const {state,tileId}=prepared('housing');
 const crossed=runCommand(state,{type:'wild-start-node',tileId,nodeId:'t1-farm'});
 assert.equal(crossed.ok,false);assert.match(crossed.message,/跨路线/);assert.equal(crossed.state,state);
 state.wild!.lowIndustrial=0;
 const original=structuredClone(state);
 const absent=runCommand(state,{type:'wild-start-node',tileId,nodeId:'t1-apartment'});
 assert.equal(absent.ok,false);assert.match(absent.message,/物资库存不足/);assert.deepEqual(state,original);
});

test('cancelling ordinary predecessors after advancement keeps irreversible Tier and its permanent occupancy',()=>{
 const fixture=prepared('housing'),tileId=fixture.tileId;
 let state=execute(fixture.state,{type:'wild-start-node',tileId,nodeId:'t1-apartment'});
 state=execute(state,{type:'advance-event'});
 const resources=structuredClone(state.resources);
 state=execute(state,{type:'wild-cancel-node',tileId,nodeId:'t1-foundation'});
 const tile=state.tiles.find(t=>t.id===tileId)!,design=state.wild!.designs[tile.building!.id];
 assert.deepEqual(design.activeNodeIds,[]);assert.equal(designUsed(design),2);
 assert.equal(design.currentTier,2);assert.equal(designCapacity(design,wildDesignRules(1)),6);
 assert.deepEqual(state.resources,resources);assert.equal(state.wild!.lowIndustrial,1);
 assert.equal(effectiveBuilding(state,tile)!.housing,20);assert.equal(effectiveBuilding(state,tile)!.flows.food,5);
 assert.deepEqual(state.wild!.unlockedForms,['farm-t2-apartment']);
 assert.equal(runCommand(state,{type:'wild-cancel-node',tileId,nodeId:'t1-apartment'}).ok,false);
});

test('opening industrial supply is issued once per new Wild game and is not reissued by loading or enabling again',()=>{
 let state=execute(makeScenario('normal'),{type:'wild-enable'});
 assert.equal(state.wild!.lowIndustrial,2);
 assert.equal(state.records.filter(r=>r.title==='Wild 开局补给').length,1);
 const first=state.tiles.find(t=>t.building?.kind==='farm')!;
 // The actual model tests above cover spending; this fixture represents an already-spent save.
 state.wild!.lowIndustrial=0;
 const restored=decodeSave(encodeSave(state,{uiScale:100,fontScale:100})).game;
 assert.equal(restored.wild!.lowIndustrial,0);
 assert.equal(enableWildSession(restored).wild!.lowIndustrial,0);
 assert.equal(runCommand(restored,{type:'wild-enable'}).ok,false);
 const fresh=execute(restored,{type:'new-game',name:'新河湾'});
 assert.equal(fresh.name,'新河湾');assert.equal(fresh.wild!.lowIndustrial,2);
 assert.equal(fresh.records.filter(r=>r.title==='Wild 开局补给').length,1);
 assert.equal(fresh.wild!.housingResearch,0);
 assert.deepEqual(fresh.wild!.designs[first.building!.id].activeNodeIds,[]);
});
