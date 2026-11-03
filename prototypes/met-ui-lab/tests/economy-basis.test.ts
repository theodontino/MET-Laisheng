import test from 'node:test';
import assert from 'node:assert/strict';
import { upgradeConstructionCP, quoteNodeInstallation } from '../src/game/economy-basis';

test('upgrade multiplier uses this upgrade occupancy, not building total capacity', () => {
 // 100 is only an arithmetic fixture, not a product building price.
 assert.equal(upgradeConstructionCP(100, 1), 10);
 assert.equal(upgradeConstructionCP(100, 2), 20);
 assert.equal(upgradeConstructionCP(100, 3), 30);
});

test('component scaling preserves fractional quantities without rounding or compounding', () => {
 assert.ok(Math.abs(upgradeConstructionCP(7, 1) - .7) < 1e-10);
 assert.equal(upgradeConstructionCP(0, 2), 0);
});

test('slot CP grows with points but its fixed goods price does not multiply', () => {
 const goods = { basicGoods: 1 };
 const quotes = [3,9,27].map(points => quoteNodeInstallation(10, points, goods));
 assert.deepEqual(quotes.map(q => q.constructionCP), [3,9,27]);
 assert.deepEqual(quotes.map(q => q.resourceCost), [{basicGoods:1},{basicGoods:1},{basicGoods:1}]);
 quotes[0].resourceCost.basicGoods = 2;
 assert.equal(goods.basicGoods, 1);
 assert.equal(quotes[1].resourceCost.basicGoods, 1);
});
