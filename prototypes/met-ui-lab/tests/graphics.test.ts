import assert from 'node:assert/strict';
import {test} from 'node:test';
import {createElement} from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {Meter,FlowBar} from '../src/compact/Graphics';
import App from '../src/App';

test('capacity overflow retains true demand and a bounded visual bar; no capacity is distinct from empty capacity',()=>{
 const over=renderToStaticMarkup(createElement(Meter,{label:'住房',value:12,max:8,unit:'人'}));
 assert(over.includes('12 / 8 人'));assert(over.includes('width:100%'));
 assert(over.includes('aria-valuenow="8"'));assert(over.includes('overflow'));
 const absent=renderToStaticMarkup(createElement(Meter,{label:'岗位',value:0,max:0}));
 assert(absent.includes('暂无容量'));assert(!absent.includes('NaN'));
 const empty=renderToStaticMarkup(createElement(Meter,{label:'入住',value:0,max:12}));
 assert(empty.includes('0 / 12'));assert(!empty.includes('暂无容量'));
});
test('source chart uses the same zero center and magnitude scale for income and consumption',()=>{
 const negative=renderToStaticMarkup(createElement(FlowBar,{label:'居民消耗',value:-5,scale:10}));
 const positive=renderToStaticMarkup(createElement(FlowBar,{label:'农居产出',value:10,scale:10}));
 assert(negative.includes('left:25%;width:25%'));assert(negative.includes('-5 / 月'));
 assert(positive.includes('left:50%;width:50%'));assert(positive.includes('+10 / 月'));
});
test('V3 initial presentation opens a map-local tile inspector without a global workspace',()=>{
 const html=renderToStaticMarkup(createElement(App));
 assert(html.includes('aria-label="地图地块信息"'));
 assert(html.includes('住房入住'));assert(html.includes('岗位使用'));
 assert(!html.includes('data-presentation='));
 assert(!/NaN|undefined|Infinity/.test(html));
});
