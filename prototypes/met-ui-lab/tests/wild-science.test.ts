import test from 'node:test';
import assert from 'node:assert/strict';
import { makeScenario, runCommand } from '../src/model';
import { enableWildSession, researchRate, type WildSession } from '../src/game/wild-runtime';
import { CITY_MONTH_REAL_MS, CITY_YEAR_REAL_DAYS, quoteScienceDT, spendScienceDT, syncCityScience } from '../src/game/wild-science';

const start = 1_000_000;
function fixture() {
 const state = enableWildSession(makeScenario('construction'));
 const school = state.tiles.find(tile => tile.building?.kind === 'center')!.building!;
 state.wild!.researchSources = [{ buildingId: school.id, kind: 'secondary-school' }];
 state.wild!.researchProgress = 0;
 state.dt = 20;
 return state;
}
function stamp(state: ReturnType<typeof fixture>) {
 return (state.wild as WildSession & { lastCityScienceAtMs?: number }).lastCityScienceAtMs;
}

test('city calendar uses 216 real days per year and 18 per month; online and offline agree', () => {
 assert.equal(CITY_YEAR_REAL_DAYS, 216);
 assert.equal(CITY_MONTH_REAL_MS * 12, CITY_YEAR_REAL_DAYS * 86400000);
 const input = fixture(), baseline = syncCityScience(input, start);
 assert.equal(input.wild!.researchProgress, 0); assert.equal(stamp(input), undefined);
 assert.equal(stamp(baseline), start); assert.equal(baseline.wild!.researchProgress, 0, 'First load does not backfill');
 let online = baseline;
 for (let month = 1; month <= 12; month++) online = syncCityScience(online, start + month * CITY_MONTH_REAL_MS);
 const offline = syncCityScience(baseline, start + 12 * CITY_MONTH_REAL_MS);
 assert.ok(Math.abs(online.wild!.researchProgress! - 1) < 1e-10);
 assert.ok(Math.abs(online.wild!.researchProgress! - offline.wild!.researchProgress!) < 1e-10);
 assert.equal(offline.wild!.housingResearch, 0);
});

test('paused block still researches; natural clock changes no block resources, DT or construction', () => {
 const input = syncCityScience(fixture(), start); input.paused = true;
 const state = syncCityScience(input, start + CITY_MONTH_REAL_MS);
 assert.equal(state.wild!.researchProgress, 1 / 12);
 assert.equal(state.dt, input.dt); assert.equal(state.month, input.month);
 assert.deepEqual(state.resources, input.resources); assert.deepEqual(state.projects, input.projects);
 assert.deepEqual(state.tiles, input.tiles); assert.equal(state.paused, true);
 const block = runCommand({ ...input, paused: false, speed: 4 }, { type: 'advance-event' }).state;
 assert.equal(block.wild!.researchProgress, 0, 'Block DT cannot advance city science');
 const noDT = syncCityScience({ ...input, dt: 0, speed: 1 }, start + CITY_MONTH_REAL_MS);
 assert.equal(noDT.wild!.researchProgress, state.wild!.researchProgress);
});

test('restoring the same timestamp and clock rollback never replay elapsed science', () => {
 const once = syncCityScience(syncCityScience(fixture(), start), start + CITY_MONTH_REAL_MS);
 const restored = structuredClone(once);
 assert.equal(syncCityScience(restored, stamp(restored)!), restored);
 assert.equal(syncCityScience(restored, start), restored);
 const later = syncCityScience(restored, start + 2 * CITY_MONTH_REAL_MS);
 assert.equal(later.wild!.researchProgress, 2 / 12);
});

test('inactive facilities accrue no banked science and archived states freeze', () => {
 let state = syncCityScience(fixture(), start);
 const school = state.tiles.find(tile => tile.building?.id === state.wild!.researchSources[0].buildingId)!.building!;
 school.running = false;
 state = syncCityScience(state, start + CITY_MONTH_REAL_MS);
 assert.equal(state.wild!.researchProgress, 0);
 state.tiles.find(tile => tile.building?.id === school.id)!.building!.running = true;
 state = syncCityScience(state, start + 2 * CITY_MONTH_REAL_MS);
 assert.equal(state.wild!.researchProgress, 1 / 12);
 state.readonly = true;
 assert.equal(syncCityScience(state, start + 3 * CITY_MONTH_REAL_MS), state);
 assert.equal(spendScienceDT(state, 1).state, state);
 assert.equal(quoteScienceDT(state, 1).allowed, false);
});

test('science DT spends the shared wallet and changes only research plus its record', () => {
 const input = syncCityScience(fixture(), start), before = structuredClone(input);
 const quote = quoteScienceDT(input, 1);
 assert.equal(quote.allowed, true); assert.equal(quote.cityMonths, .8);
 assert.equal(quote.pointsAdded, researchRate(input) * .8);
 assert.deepEqual(input, before);
 const result = spendScienceDT(input, 1), state = result.state;
 assert.equal(result.ok, true); assert.equal(result.dtSpent, 1); assert.equal(state.dt, input.dt - 1);
 assert.equal(state.wild!.researchProgress, quote.pointsAdded);
 assert.equal(state.month, input.month); assert.equal(state.paused, input.paused);
 assert.equal(stamp(state), stamp(input)); assert.deepEqual(state.resources, input.resources);
 assert.deepEqual(state.projects, input.projects); assert.deepEqual(state.tiles, input.tiles);
 assert.deepEqual(state.wild!.designs, input.wild!.designs);
 assert.equal(state.records.length, input.records.length + 1); assert.deepEqual(input, before);
});

test('science DT caps at completion and charges only the useful fraction without starting another research', () => {
 const input = fixture(); input.wild!.researchProgress = 1.99; input.dt = .2;
 const quote = quoteScienceDT(input, 100);
 assert.equal(quote.allowed, true); assert.ok(Math.abs(quote.dtSpent - .15) < 1e-10);
 const result = spendScienceDT(input, 100);
 assert.ok(Math.abs(result.state.dt - .05) < 1e-10);
 assert.equal(result.state.wild!.housingResearch, 1); assert.equal(result.state.wild!.researchProgress, null);
 assert.equal(result.state.records.filter(record => record.title === '住房研究完成').length, 1);
 assert.equal(quoteScienceDT(result.state, 1).allowed, false);
});

test('invalid amounts, insufficient shared DT, inactive research or facilities do not mutate', () => {
 const input = fixture(), before = structuredClone(input);
 for (const amount of [0, -1, NaN, Infinity]) {
  assert.equal(quoteScienceDT(input, amount).allowed, false);
  assert.equal(spendScienceDT(input, amount).state, input);
 }
 input.dt = .5;
 assert.equal(spendScienceDT(input, 1).ok, false); assert.equal(input.wild!.researchProgress, 0);
 input.dt = before.dt; input.wild!.researchProgress = null;
 assert.equal(quoteScienceDT(input, 1).allowed, false);
 input.wild!.researchProgress = 0; input.wild!.researchSources = [];
 assert.equal(quoteScienceDT(input, 1).allowed, false);
 assert.deepEqual(input.resources, before.resources); assert.deepEqual(input.projects, before.projects);
});
