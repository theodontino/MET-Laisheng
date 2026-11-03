import test from 'node:test';
import assert from 'node:assert/strict';
import { makeScenario } from '../src/model';
import { AUTO_SAVE_KEY, SAVE_KEY, decodeSave, encodeSave } from '../src/game/storage';

const settings = { uiScale: 100, fontScale: 110 };
test('Web save roundtrip preserves complete city state and future fields', () => {
 const game = Object.assign(makeScenario('construction'), { futureProgress: { seed: 42, earned: ['annual-draft'] } });
 Object.assign(game.tiles[0], { futureDecoration: { color: 'teal' } });
 game.paused = true;
 const text = encodeSave(game, settings);
 const envelope = JSON.parse(text);
 assert.equal(envelope.format, 'met-wild-web');
 assert.equal(envelope.version, 1);
 const restored = decodeSave(text);
 assert.deepEqual(restored.game, JSON.parse(JSON.stringify(game)));
 assert.deepEqual(restored.settings, settings);
 assert.equal(restored.savedAt, envelope.savedAt);
 assert.ok(Number.isFinite(Date.parse(restored.savedAt)));
 assert.notEqual(SAVE_KEY, AUTO_SAVE_KEY);
});

test('restoring a running city pauses it without mutating the original', () => {
 const game = makeScenario('normal');
 game.paused = false;
 assert.equal(decodeSave(encodeSave(game, settings)).game.paused, true);
 assert.equal(game.paused, false);
});

test('invalid JSON, missing state, negative resources and invalid months are rejected', () => {
 assert.throws(() => decodeSave('{'), /有效 JSON/);
 const envelope = JSON.parse(encodeSave(makeScenario('normal'), settings));
 assert.throws(() => decodeSave(JSON.stringify({ ...envelope, game: {} })), /存档内容无效/);
 for (const value of [-1, null, '100']) {
  const bad = structuredClone(envelope);
  bad.game.resources.food = value;
  assert.throws(() => decodeSave(JSON.stringify(bad)), /资源库存/);
 }
 envelope.game.month = -1;
 assert.throws(() => decodeSave(JSON.stringify(envelope)), /月份/);
 const game = makeScenario('normal');
 game.resources.food = Infinity;
 assert.throws(() => encodeSave(game, settings), /资源库存/);
});

test('duplicate IDs and orphaned construction references are rejected', () => {
 const envelope = JSON.parse(encodeSave(makeScenario('shortage'), settings));
 const orphan = structuredClone(envelope);
 orphan.game.projects[0].tileId = 'missing-tile';
 assert.throws(() => decodeSave(JSON.stringify(orphan)), /地块不存在/);
 const duplicateTile = structuredClone(envelope);
 duplicateTile.game.tiles.push(duplicateTile.game.tiles[0]);
 assert.throws(() => decodeSave(JSON.stringify(duplicateTile)), /地块编号/);
 envelope.game.projects.push(envelope.game.projects[0]);
 assert.throws(() => decodeSave(JSON.stringify(envelope)), /项目编号/);
});

test('old demo and unsupported versions give actionable errors', () => {
 assert.throws(() => decodeSave(JSON.stringify({ version: 3, state: makeScenario('normal') })), /旧演示.*不能导入/);
 const envelope = JSON.parse(encodeSave(makeScenario('normal'), settings));
 envelope.version = 2;
 assert.throws(() => decodeSave(JSON.stringify(envelope)), /版本 2.*版本 1/);
});
