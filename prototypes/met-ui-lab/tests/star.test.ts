import test from 'node:test';
import assert from 'node:assert/strict';
import { addNode, cancellationPlan, commitCancel, designCapacity, designUsed, nodePrerequisitesMet, slotPointCost, type NodeDef } from '../src/game/star';
import { createWildDesign, wildDesignRules, completeWildAdvancement, matchWildBonus, unlockCompletedForm } from '../src/game/wild-design';

// Research counts below exercise confirmed arithmetic, not a proposed research schedule.
const rules = wildDesignRules(20);
const nodes: NodeDef[] = [
 {id:'root',tier:1,kind:'stat',requires:[],points:1,resourceCost:{material:7}},
 {id:'branch',tier:1,kind:'stat',requires:['root'],points:1,resourceCost:{material:9}},
 {id:'slot-a',tier:1,kind:'slot',requires:['branch'],points:0,resourceCost:{basicGoods:1},slotType:'organization'},
 {id:'slot-b',tier:1,kind:'slot',requires:[],points:0,resourceCost:{basicGoods:1},slotType:'link'},
 {id:'slot-c',tier:1,kind:'slot',requires:[],points:0,resourceCost:{basicGoods:1},slotType:'fusion'},
 {id:'next-slot',tier:2,kind:'slot',requires:[],points:0,resourceCost:{basicGoods:1},slotType:'organization'},
 {id:'next-stat',tier:2,kind:'stat',requires:[],points:1,resourceCost:{material:3}},
];

// Geometric passage through unopened slots does not require purchasing them.
// Only switching from housing to the production route uses the middle converter.
const routeNodes: NodeDef[] = [
 {id:'housing',tier:1,kind:'stat',requires:[],points:1,resourceCost:{}},
 {id:'production',tier:1,kind:'stat',requires:[],points:1,resourceCost:{}},
 {id:'middle-slot',tier:1,kind:'slot',requires:[],routes:[{from:'housing'},{from:'production'}],points:0,resourceCost:{basicGoods:1},slotType:'link'},
 {id:'apartment',tier:1,kind:'stat',requires:[],routes:[{from:'housing'}],points:1,resourceCost:{}},
 {id:'farm',tier:1,kind:'stat',requires:[],routes:[{from:'production'},{from:'housing',via:['middle-slot']}],points:1,resourceCost:{}},
 {id:'farm-output',tier:1,kind:'stat',requires:[],routes:[{from:'farm'}],points:1,resourceCost:{}},
];

test('same-route housing to apartment and production to farm pass unopened slots',()=>{
 for(const [from,to] of [['housing','apartment'],['production','farm']]){
  const started=addNode(createWildDesign(1),from,routeNodes,rules).design;
  const completed=addNode(started,to,routeNodes,rules).design;
  assert.deepEqual(completed.activeNodeIds,[from,to]);
  assert.equal(designUsed(completed),2);
 }
});

test('housing to farm switches routes only through a completed converter slot',()=>{
 let d=addNode(createWildDesign(1),'housing',routeNodes,rules).design;
 assert.equal(nodePrerequisitesMet(routeNodes.find(n=>n.id==='farm')!,d.activeNodeIds),false);
 assert.throws(()=>addNode(d,'farm',routeNodes,rules),/前置/);
 d=addNode(d,'middle-slot',routeNodes,rules).design;
 assert.equal(nodePrerequisitesMet(routeNodes.find(n=>n.id==='farm')!,d.activeNodeIds),true);
 assert.deepEqual(addNode(d,'farm',routeNodes,rules).design.activeNodeIds,['housing','middle-slot','farm']);
});

test('routes are alternatives, each needs its origin and every converter, and requires remains ALL',()=>{
 const node:NodeDef={id:'joined',tier:1,kind:'stat',requires:['permit','research'],routes:[{from:'housing',via:['middle-slot','upper-slot']},{from:'production'}],points:1,resourceCost:{}};
 assert.equal(nodePrerequisitesMet(node,['permit','research','housing','middle-slot']),false);
 assert.equal(nodePrerequisitesMet(node,['permit','research','middle-slot','upper-slot']),false);
 assert.equal(nodePrerequisitesMet(node,['permit','research','housing','middle-slot','upper-slot']),true);
 assert.equal(nodePrerequisitesMet(node,['permit','research','production']),true);
 assert.equal(nodePrerequisitesMet(node,['permit','production']),false);
});

test('removing a converter preserves own-route nodes and a valid alternate active route',()=>{
 let d=createWildDesign(1);
 for(const id of ['housing','production','middle-slot','apartment','farm','farm-output'])d=addNode(d,id,routeNodes,rules).design;
 const plan=cancellationPlan(d,'middle-slot',routeNodes,rules);
 assert.deepEqual(plan.nodeIds,['middle-slot']);
 assert.deepEqual(commitCancel(d,plan).activeNodeIds,['housing','production','apartment','farm','farm-output']);
 assert.equal(plan.pointRefund,3);assert.equal(plan.materialRefund,0);
});

test('removing a converter cascades cross-route descendants while preserving own-route nodes',()=>{
 let d=createWildDesign(1);
 for(const id of ['housing','middle-slot','apartment','farm','farm-output'])d=addNode(d,id,routeNodes,rules).design;
 d.slotBonuses={'middle-slot':'link'};
 const plan=cancellationPlan(d,'middle-slot',routeNodes,rules);
 assert.deepEqual(plan.nodeIds,['middle-slot','farm','farm-output']);
 assert.deepEqual(commitCancel(d,plan).activeNodeIds,['housing','apartment']);
 assert.deepEqual(plan.returnedBonuses,[{nodeId:'middle-slot',bonusId:'link'}]);
 assert.equal(plan.pointRefund,5);assert.equal(plan.materialRefund,0);
});

test('T1 starts with two independent points; ordinary nodes cost one',()=>{
 const a=createWildDesign(1),b=createWildDesign(1),r=wildDesignRules(0);
 const first=addNode(a,'root',nodes,r).design;
 assert.equal(designCapacity(a,r),2);assert.equal(designUsed(first),1);assert.equal(designUsed(b),0);
 assert.throws(()=>addNode(first,'slot-b',nodes,r),/不足/);
 assert.deepEqual(a.activeNodeIds,[]);
});

test('research adds two points per completion to old and new buildings without an extra cap',()=>{
 const r=wildDesignRules(2);
 assert.equal(designCapacity(createWildDesign(1),r),6);
 assert.equal(designCapacity(createWildDesign(2),r),4);
 assert.equal(designCapacity(createWildDesign(3),r),4);
 assert.equal(designCapacity(createWildDesign(1),wildDesignRules(20)),42);
});

test('direct advanced builds have zero base points and cannot acquire old Tier nodes',()=>{
 const d=createWildDesign(2);
 assert.equal(designCapacity(d,wildDesignRules(0)),0);
 assert.throws(()=>addNode(d,'root',nodes,rules),/起始 Tier 之前/);
 assert.equal(designUsed(addNode(d,'next-stat',nodes,rules).design),1);
 assert.throws(()=>addNode(createWildDesign(1),'next-stat',nodes,rules),/进阶/);
 assert.throws(()=>addNode(createWildDesign(1),'branch',nodes,rules),/前置/);
});

test('completed advancement adds two capacity and permanently occupies two',()=>{
 const r=wildDesignRules(0),d=completeWildAdvancement(createWildDesign(1),r);
 assert.equal(d.currentTier,2);assert.equal(designCapacity(d,r),4);assert.equal(designUsed(d),2);
 const withStat=addNode(d,'next-stat',nodes,r).design;
 const cancelled=commitCancel(withStat,cancellationPlan(withStat,'next-stat',nodes,r));
 assert.equal(designUsed(cancelled),2);assert.equal(cancelled.currentTier,2);
 assert.throws(()=>cancellationPlan(cancelled,'tier-2',nodes,r),/尚未启用/);
 assert.throws(()=>completeWildAdvancement(addNode(createWildDesign(1),'root',nodes,r).design,r),/空出 2 点/);
});

test('same Tier organization/link/fusion slots cost 3,9,27; fixed materials; next Tier resets',()=>{
 assert.deepEqual([1,2,3].map(i=>slotPointCost(3,i)),[3,9,27]);
 let d=createWildDesign(1);
 for(const id of ['root','branch'])d=addNode(d,id,nodes,rules).design;
 const quotes=['slot-a','slot-b','slot-c'].map(id=>{const result=addNode(d,id,nodes,rules);d=result.design;return result});
 assert.deepEqual(quotes.map(q=>q.pointCost),[3,9,27]);
 assert.deepEqual(quotes.map(q=>q.resourceCost),[{basicGoods:1},{basicGoods:1},{basicGoods:1}]);
 const next=completeWildAdvancement(d,wildDesignRules(30));
 assert.equal(addNode(next,'next-slot',nodes,wildDesignRules(30)).pointCost,3);
});

test('removing any slot reprices remaining slots to 3 + 9; next purchase costs 27',()=>{
 let d=createWildDesign(1);
 for(const id of ['root','branch','slot-a','slot-b','slot-c'])d=addNode(d,id,nodes,rules).design;
 for(const removed of ['slot-a','slot-b','slot-c']){
  const plan=cancellationPlan(d,removed,nodes,rules),after=commitCancel(d,plan);
  assert.equal(plan.pointRefund,27);assert.equal(plan.materialRefund,0);
  assert.equal(designUsed(after),14); // two ordinary nodes plus 12 slot points
  assert.equal(addNode(after,removed,nodes,rules).pointCost,27);
 }
 assert.equal(designUsed(d),41);
});

test('cascade returns Bonus copies, frees dependent nodes, and reprices surviving slots',()=>{
 let d=createWildDesign(1);
 for(const id of ['root','branch','slot-a','slot-b'])d=addNode(d,id,nodes,rules).design;
 d.slotBonuses={'slot-a':'care','slot-b':'link'};
 const original=structuredClone(d),plan=cancellationPlan(d,'root',nodes,rules);
 assert.deepEqual(plan.nodeIds,['root','branch','slot-a']);
 assert.deepEqual(plan.returnedBonuses,[{nodeId:'slot-a',bonusId:'care'}]);
 assert.equal(plan.pointRefund,11);assert.equal(plan.materialRefund,0);
 const after=commitCancel(d,plan);
 assert.equal(designUsed(after),3);assert.deepEqual(after.slotBonuses,{'slot-b':'link'});
 assert.deepEqual(d,original);
});

test('dedicated slots check category and building; fusion never assumes a candidate pool',()=>{
 const bonus={id:'care',category:'organization' as const,buildingTypes:['center']};
 assert.equal(matchWildBonus('organization','center',bonus).allowed,true);
 assert.equal(matchWildBonus('link','center',bonus).allowed,false);
 assert.equal(matchWildBonus('organization','farm',bonus).allowed,false);
 assert.equal(matchWildBonus('fusion','farm',bonus).allowed,false);
 assert.equal(matchWildBonus('fusion','farm',bonus,['care']).allowed,true);
});

test('advanced form unlocks only on actual completed advancement, once',()=>{
 const started={formId:'clinic-t2',fromTier:1,toTier:2,completed:false};
 assert.deepEqual(unlockCompletedForm([],started),[]);
 const done={...started,completed:true};
 const list=unlockCompletedForm([],done);
 assert.deepEqual(list,['clinic-t2']);assert.deepEqual(unlockCompletedForm(list,done),list);
});
