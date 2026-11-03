import test from 'node:test';
import assert from 'node:assert/strict';
import { effectiveBuilding, makeScenario, runCommand } from '../src/model';
import { addNode, designCapacity, designUsed } from '../src/game/star';
import { createWildDesign, wildDesignRules } from '../src/game/wild-design';
import { advanceWildResearch, applyWildEffects, DEFAULT_WILD_CONFIG, enableWildSession, finishWildProject, handleWildCommand, quoteWildNode, wildNodes, type RuntimeConfig } from '../src/game/wild-runtime';

// Explicit test-only prices/effects exercise plumbing; none are product defaults.
const fixtureConfig: RuntimeConfig = { ordinaryMaterial: 4, advancementLowIndustrial: 2, tierBuildCP: { 1: 60, 2: 90 }, housingEffectFraction: .25, primaryEffectFraction: .2, research: { currency: 35, targetPoints: 10 } };
function fixture() {
 const state = enableWildSession(makeScenario('normal'));
 state.wild!.config = structuredClone(fixtureConfig);
 return state;
}
function command(state: ReturnType<typeof fixture>, type: string, fields: Record<string, unknown> = {}) {
 const result = handleWildCommand(state, { type, ...fields })!;
 assert.equal(result.ok, true, result.message);
 return result.state;
}

test('opt-in copies existing buildings as direct tier, leaves resources and unknown settings unchanged', () => {
 const input = makeScenario('ending'), original = structuredClone(input), state = enableWildSession(input);
 assert.deepEqual(input, original); assert.deepEqual(state.resources, input.resources);
 assert.deepEqual(state.wild!.config, DEFAULT_WILD_CONFIG); assert.equal(state.wild!.lowIndustrial, 2);
 const advanced = state.tiles.find(tile => tile.building?.tier === 3)!;
 assert.equal(state.wild!.designs[advanced.building!.id].baseCapacity, 0);
 const tile = state.tiles.find(tile => tile.building?.tier === 1 || tile.building?.tier === 2)!;
 assert.equal(quoteWildNode(state, tile.id, `t${tile.building!.tier}-housing`).allowed, false);
 assert.match(quoteWildNode(state, tile.id, `t${tile.building!.tier}-housing`).reason, /前置|待确认/);
});

test('queue reserves design points; settlement consumes material and only completion applies effects', () => {
 const input = fixture(), tile = input.tiles.find(tile => tile.building?.kind === 'farm')!;
 input.wild!.config.tierBuildCP[1] = 200; // Test-only long project to observe partial construction.
 const baseline = effectiveBuilding(input, tile)!;
 const queued = command(input, 'wild-start-node', { tileId: tile.id, nodeId: 't1-foundation' });
 assert.equal(queued.resources.material, input.resources.material);
 assert.equal(queued.projects[0].totalCP, 20); assert.equal(queued.projects[0].totalMaterial, 4);
 assert.equal(designUsed(queued.wild!.designs[tile.building!.id]), 0);
 assert.equal(quoteWildNode(queued, tile.id, 't1-primary').reserved, 1);
 assert.equal(effectiveBuilding(queued, queued.tiles.find(item => item.id === tile.id)!)!.housing, baseline.housing);
 let state = runCommand(queued, { type: 'time', paused: false }).state;
 state = runCommand(state, { type: 'advance-event' }).state;
 assert.ok(state.projects[0].usedMaterial > 0);
 assert.equal(effectiveBuilding(state, state.tiles.find(item => item.id === tile.id)!)!.housing, baseline.housing);
 state = runCommand(state, { type: 'advance-event' }).state;
 assert.equal(state.projects.length, 0);
 const completedTile = state.tiles.find(item => item.id === tile.id)!;
 assert.equal(effectiveBuilding(state, completedTile)!.housing, baseline.housing * 1.25);
 assert.equal(state.wild!.designs[tile.building!.id].nodePointCosts['t1-foundation'], 1);
 assert.equal(input.projects.length, 0);
});

test('T1 slot goods use existing inventory, cancellation returns Bonus and reprices remaining slots', () => {
 const input = fixture(), tile = input.tiles.find(tile => tile.building?.kind === 'farm')!;
 input.wild!.housingResearch = 20;
 for(const id of ['t1-foundation','t1-housing','t1-primary']) input.wild!.designs[tile.building!.id] = addNode(input.wild!.designs[tile.building!.id],id,wildNodes(input,tile.id),wildDesignRules(20)).design;
 let state = command(input, 'wild-start-node', { tileId: tile.id, nodeId: 't1-organization' });
 assert.equal(state.resources.goods, input.resources.goods - 1);
 assert.equal(quoteWildNode(state, tile.id, 't1-link').reserved, 3);
 const firstProject = state.projects[0];
 assert.equal(finishWildProject(state, firstProject), true); state.projects = [];
 for (const nodeId of ['t1-link', 't1-fusion']) {
  state = command(state, 'wild-start-node', { tileId: tile.id, nodeId });
  assert.equal(finishWildProject(state, state.projects[0]), true); state.projects = [];
 }
 const design = state.wild!.designs[tile.building!.id];
 assert.deepEqual(Object.values(design.nodePointCosts), [1, 1, 1, 3, 9, 27]);
 design.slotBonuses['t1-organization'] = 'care';
 const resources = structuredClone(state.resources), beforeBonus = state.bonuses.care ?? 0;
 state = command(state, 'wild-cancel-node', { tileId: tile.id, nodeId: 't1-organization' });
 assert.deepEqual(state.resources, resources); assert.equal(state.bonuses.care, beforeBonus + 1);
 assert.deepEqual(Object.values(state.wild!.designs[tile.building!.id].nodePointCosts), [1, 1, 1, 3, 9]);
 assert.equal(quoteWildNode(state, tile.id, 't1-organization').pointCost, 27);
});

test('research starts with currency, then fills points from facilities on city time', () => {
 const input = fixture(), school = input.tiles.find(tile => tile.building?.kind === 'center')!.building!;
 input.wild!.researchSources = [{ buildingId: school.id, kind: 'secondary-school', pointsPerCityMonth: 4 }];
 const state = command(input, 'wild-start-research');
 const tile = state.tiles.find(tile => !!tile.building)!;
 assert.equal(state.resources.currency, input.resources.currency - 35);
 assert.equal(state.resources.goods, input.resources.goods);
 assert.equal(state.wild!.researchProgress, 0);
 assert.equal(designCapacity(state.wild!.designs[tile.building!.id], wildDesignRules(state.wild!.housingResearch)), 2);
 advanceWildResearch(state, 1); assert.equal(state.wild!.researchProgress, 4); assert.equal(state.wild!.housingResearch, 0);
 const accelerated = runCommand({ ...state, speed: 4, paused: false }, { type: 'advance-event' }).state;
 assert.equal(accelerated.wild!.researchProgress, 4);
 assert.equal(accelerated.dt, state.dt - 3);
 state.tiles.find(tile => tile.building?.id === school.id)!.building!.running = false;
 advanceWildResearch(state, 10); assert.equal(state.wild!.researchProgress, 4);
 state.tiles.find(tile => tile.building?.id === school.id)!.building!.running = true;
 // Block DT, speed and elapsed block months cannot change the city-time rate.
 state.dt = 1; state.speed = 4; state.month = 900;
 advanceWildResearch(state, .5); assert.equal(state.wild!.researchProgress, 6);
 advanceWildResearch(state, 1); assert.equal(state.wild!.housingResearch, 1);
 assert.equal(state.wild!.researchProgress, null);
 assert.equal(state.resources.currency, input.resources.currency - 35);
 assert.equal(designCapacity(createWildDesign(1), wildDesignRules(state.wild!.housingResearch)), 4);
 assert.equal(designCapacity(createWildDesign(3), wildDesignRules(state.wild!.housingResearch)), 2);
});

test('research needs an actual running registered school or institute and sufficient currency', () => {
 const state = fixture(), before = structuredClone(state);
 let result = handleWildCommand(state, { type: 'wild-start-research' })!;
 assert.equal(result.ok, false); assert.match(result.message, /中学、大学或研究所/); assert.deepEqual(state, before);
 state.wild!.researchSources = [{ buildingId: 'not-built', kind: 'research-institute', pointsPerCityMonth: 4 }];
 assert.equal(handleWildCommand(state, { type: 'wild-start-research' })!.ok, false);
 const school = state.tiles.find(tile => tile.building?.kind === 'center')!.building!;
 state.wild!.researchSources[0].buildingId = school.id; school.running = false;
 assert.equal(handleWildCommand(state, { type: 'wild-start-research' })!.ok, false);
 school.running = true; state.resources.currency = 34;
 result = handleWildCommand(state, { type: 'wild-start-research' })!;
 assert.equal(result.ok, false); assert.match(result.message, /通货不足/); assert.equal(state.wild!.researchProgress, null);
});

test('node quotes and previews do not mutate; primary effect adds base output without compounding', () => {
 const state = fixture(), tile = state.tiles.find(tile => tile.building?.kind === 'farm')!, b = tile.building!;
 state.wild!.designs[b.id] = addNode(state.wild!.designs[b.id], 't1-foundation', wildNodes(state,tile.id), wildDesignRules(0)).design;
 state.wild!.designs[b.id] = addNode(state.wild!.designs[b.id], 't1-primary', wildNodes(state, tile.id), wildDesignRules(0)).design;
 const before = structuredClone(state);
 quoteWildNode(state, tile.id, 't1-housing'); wildNodes(state, tile.id);
 const preview = applyWildEffects(state, tile, b);
 assert.equal(preview.flows.food, 10 * 1.4);
 assert.equal(applyWildEffects(state, tile, b).flows.food, preview.flows.food);
 assert.deepEqual(state, before);
});

test('archived actions do not mutate and missing form rules block advancement', () => {
 const state = fixture(), tile = state.tiles.find(tile => !!tile.building)!;
 const nonFarm=state.tiles.find(t=>t.building?.kind==='center')!;assert.match(quoteWildNode(state, nonFarm.id, 't1-advance').missing!, /形态基数/);
 state.readonly = true; const before = structuredClone(state);
 for (const type of ['wild-enable', 'wild-start-research', 'wild-start-node', 'wild-cancel-node']) {
  const result = handleWildCommand(state, { type, tileId: tile.id, nodeId: 't1-housing' })!;
  assert.equal(result.ok, false); assert.equal(result.state, state);
 }
 assert.deepEqual(state, before);
 assert.equal(handleWildCommand(state, { type: 'toggle-building', tileId: tile.id }), undefined);
});


test('confirmed farm entry can be built at 10 material and 1 CP; prerequisite routes remain separate from slots',()=>{
 let state=enableWildSession(makeScenario('normal'));
 const tile=state.tiles.find(t=>t.building?.kind==='farm')!,id=tile.building!.id;
 const quote=quoteWildNode(state,tile.id,'t1-foundation');
 assert.equal(quote.allowed,true);assert.equal(quote.constructionCP,1);assert.deepEqual(quote.resourceCost,{material:10});
 state=command(state,'wild-start-node',{tileId:tile.id,nodeId:'t1-foundation'});
 state=runCommand({...state,paused:false},{type:'advance-event'}).state;
 assert.ok(state.wild!.designs[id].activeNodeIds.includes('t1-foundation'));
 state=command(state,'wild-start-node',{tileId:tile.id,nodeId:'t1-housing'});
 state=runCommand(state,{type:'advance-event'}).state;
 assert.equal(designUsed(state.wild!.designs[id]),2);
 assert.match(quoteWildNode(state,tile.id,'t1-apartment').reason,/设计点不足/);
 assert.match(quoteWildNode(state,tile.id,'t1-farm').reason,/跨路线/);
 assert.deepEqual(state.wild!.designs[id].activeNodeIds,['t1-foundation','t1-housing']);
});

test('confirmed yearly science rates and first technology target; block months never progress science',()=>{
 for(const [kind,months] of [['secondary-school',24],['university',1.2],['research-institute',1.2]] as const){
  let state=enableWildSession(makeScenario('normal'));
  const b=state.tiles.find(t=>t.building?.kind==='center')!.building!;
  state.wild!.researchSources=[{buildingId:b.id,kind}];
  state=command(state,'wild-start-research');
  advanceWildResearch(state,months/2);assert.equal(state.wild!.housingResearch,0);assert.ok(Math.abs(state.wild!.researchProgress!-1)<1e-8);
  advanceWildResearch(state,months/2);assert.equal(state.wild!.housingResearch,1);assert.equal(state.wild!.researchProgress,null);
  assert.equal(handleWildCommand(state,{type:'wild-start-research'})!.ok,false,'Later technology target must be approved separately');
 }
});

test('existing legacy construction must complete before switching to Wild',()=>{
 const state=makeScenario('construction');
 const result=handleWildCommand(state,{type:'wild-enable'})!;
 assert.equal(result.ok,false);assert.equal(result.state,state);assert.match(result.message,/现有演示工程/);
});
