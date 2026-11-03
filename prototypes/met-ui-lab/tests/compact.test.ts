import assert from 'node:assert/strict';
import {test} from 'node:test';
import {createElement, type ComponentType} from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {LabContext, type LabAPI} from '../src/context';
import {makeScenario,summary,runCommand,conditions,scenarios,bonusCatalog} from '../src/model';
import type {GameState,PanelMemory,Workspace} from '../src/types';
import {ResidentsCompact,EconomyCompact,ConstructionCompact} from '../src/compact/Operating';
import {TasksCompact,BonusCompact} from '../src/compact/Development';
import {conditionDestination} from '../src/compact/destinations';
import Residents from '../src/panels/Residents';
import Economy from '../src/panels/Economy';
import Construction from '../src/panels/Construction';
import Tasks from '../src/panels/Tasks';
import Bonus from '../src/panels/Bonus';
const noop=()=>{};
function render(Component:ComponentType,game:GameState,workspace:Workspace,patch:Partial<PanelMemory>={}) {
 const api:LabAPI={game,stats:summary(game),workspace,selectedTile:'',view:{tab:'',filter:'',selected:'',sort:'',scroll:0,...patch},act:()=>true,go:noop,advanced:noop,showLayer:noop,remember:noop,inspect:noop,locate:noop,openModal:noop,notice:noop,setScenario:noop,settings:{uiScale:100,fontScale:100},setSettings:noop,exportWeb:noop,importWeb:async()=>{},restoreAuto:noop,saveDemo:noop,loadDemo:noop,resetDemo:noop,trackTask:noop};
 return renderToStaticMarkup(createElement(LabContext.Provider,{value:api},createElement(Component)));
}
const panels:[ComponentType,ComponentType,Workspace][]=[[ResidentsCompact,Residents,'residents'],[EconomyCompact,Economy,'economy'],[ConstructionCompact,Construction,'construction'],[TasksCompact,Tasks,'tasks'],[BonusCompact,Bonus,'bonus']];
test('every fixture renders compact and retained advanced tables without missing numerical values',()=>{
 for(const scenario of scenarios)for(const [small,large,workspace] of panels){for(const panel of [small,large]){const html=render(panel,makeScenario(scenario.id),workspace);assert(!/NaN|undefined|Infinity/.test(html),`${scenario.id}/${workspace}`);assert(html.length>300);}}
});
test('installed Bonus deep link with no spare copy retains exact blueprint and target',()=>{
 const state=makeScenario('ending');const tile=state.tiles.find(t=>t.building?.slots.some(s=>s.bonusId==='solar'))!;
 assert.equal(state.bonuses.solar,0);
 for(const filter of ['all','deployed']){
  const html=render(BonusCompact,state,'bonus',{filter,selected:'solar',draft:{targetId:tile.id,slotId:'logistics'}});
  assert(html.includes('屋顶光伏'));
  assert.match(html,new RegExp(`<option value="${tile.id}" selected=""`));
  assert.match(html,/<option value="logistics" selected=""/);
  assert(html.includes('该槽已装同一蓝图'));
 }
});
test('compact order and advanced balance share the same escrow and cancellation state',()=>{
 let state=makeScenario('normal');const cash=state.resources.currency;
 state=runCommand(state,{type:'create-order',resource:'material',side:'buy',quantity:12}).state;
 assert.equal(state.resources.currency,cash-60);
 const small=render(EconomyCompact,state,'economy',{tab:'market',draft:{marketResource:'material',marketQuantity:'12'}});
 const large=render(Economy,state,'economy',{tab:'market'});
 assert(small.includes('撤单')&&large.includes('撤'));
 state=runCommand(state,{type:'cancel-order',id:state.orders[0].id}).state;
 assert.equal(state.resources.currency,cash);
 assert(render(EconomyCompact,state,'economy',{tab:'market'}).includes('暂无待成交订单'));
});
test('compact construction exposes pause and priority changes on the same projects',()=>{
 let state=makeScenario('construction');const p=state.projects[0];
 state=runCommand(state,{type:'project-priority',id:p.id,priority:1}).state;
 state=runCommand(state,{type:'project-toggle',id:p.id}).state;
 const html=render(ConstructionCompact,state,'construction');
 assert(html.includes('已暂停'));assert(html.includes('施工优先级'));assert(html.includes(p.tileId));
 assert(render(Construction,state,'construction').includes(p.tileId));
});
test('first-settlement side tables distinguish forecast values from settled history',()=>{
 const state=makeScenario('first');
 const html=render(EconomyCompact,state,'economy');
 assert(html.includes('待首次结算'));assert(html.includes('预计净流'));
 assert(render(ResidentsCompact,state,'residents').includes('预计满足'));
});
test('readonly side tables expose values but disable direct spending and operation',()=>{
 const state=makeScenario('readonly');
 for(const [small,,workspace] of panels){assert(render(small,state,workspace).length>300);}
 assert.match(render(ResidentsCompact,state,'residents'),/<button[^>]+disabled=""[^>]*>[^<]*暂停移民/);
 assert.match(render(ConstructionCompact,state,'construction'),/<button[^>]+disabled=""[^>]*>[^<]*招募/);
 const h=render(EconomyCompact,state,'economy',{tab:'market'});
 assert.match(h,/<button[^>]*type="submit"[^>]*disabled=""/);
});
test('task actions route to the exact missing service or resource, independent of old filters',()=>{
 const game=makeScenario('normal');
 const conditionsById=Object.fromEntries(conditions(game).map(c=>[c.id,c]));
 assert.deepEqual(conditionDestination(conditionsById.medical),['residents','medical']);
 assert.deepEqual(conditionDestination(conditionsById.education),['residents','education']);
 assert.deepEqual(conditionDestination(conditionsById.food),['economy','balance','food']);
 const industry=runCommand(game,{type:'choose-theme',theme:'工业协作'}).state;
 assert.deepEqual(conditionDestination(conditions(industry).find(c=>c.id==='theme-material')!),['economy','balance','material']);
});
test('claim then install appears consistently in compact tables and advanced archive inputs',()=>{
 let state=makeScenario('reward');
 state=runCommand(state,{type:'claim-reward',id:state.rewards[0].id,bonusId:'care'}).state;
 const tile=state.tiles.find(t=>t.building?.kind==='center')!;
 const before=state.bonuses.care;
 state=runCommand(state,{type:'install-bonus',tileId:tile.id,slotId:'operations',bonusId:'care'}).state;
 assert.equal(state.bonuses.care,before-1);
 assert(state.records.some(r=>r.title==='Bonus 安装'));
 assert(render(BonusCompact,state,'bonus',{filter:'deployed',selected:'care'}).includes(bonusCatalog.find(b=>b.id==='care')!.name));
 assert(render(Bonus,state,'bonus',{filter:'deployed',selected:'care'}).includes('邻里诊疗'));
});
