import { WILD_DESIGN, type WildCost } from './wild-config';

/** Pure building-design rules. Resource/inventory changes belong to the caller. */
export interface DesignState {
 entryTier: number;
 currentTier: number;
 baseCapacity: number;
 activeNodeIds: string[];
 slotBonuses: Record<string, string>;
 /** Current node occupancy; surviving slots reprice when an earlier slot is removed. */
 nodePointCosts: Record<string, number>;
 advancementPointCosts: Record<string, number>;
 advancementNodes?: Record<number,string>;
}

export interface NodeDef {
 id: string;
 tier: number;
 kind: 'stat' | 'slot';
 requires: string[];
 /** Alternate routes: one completed origin and all of its converter slots must be active. */
 routes?: { from: string; via?: string[] }[];
 points: number;
 resourceCost: WildCost;
 slotType?: 'organization' | 'link' | 'fusion';
}

export interface DesignRules {
 technologyPoints: number;
 slotBasePoints: number;
}

export interface NodePurchase {
 design: DesignState;
 pointCost: number;
 resourceCost: WildCost;
}

export interface CancellationPlan {
 nodeIds: string[];
 pointRefund: number;
 remainingPointCosts: Record<string, number>;
 materialRefund: 0;
 returnedBonuses: { nodeId: string; bonusId: string }[];
}

export function createDesign(entryTier: number, baseCapacity: number): DesignState {
 if (!Number.isInteger(entryTier) || entryTier < 1) throw new Error('建筑起始 Tier 必须为正整数');
 nonnegative(baseCapacity, '建筑设计容量');
 return { entryTier, currentTier: entryTier, baseCapacity, activeNodeIds: [], slotBonuses: {}, nodePointCosts: {}, advancementPointCosts: {} };
}

export function designCapacity(design: DesignState, rules: DesignRules): number {
 return design.baseCapacity + rules.technologyPoints;
}

export function designUsed(design: DesignState): number {
 return design.activeNodeIds.reduce((total, id) => total + design.nodePointCosts[id], 0)
  + Object.values(design.advancementPointCosts).reduce((sum, points) => sum + points, 0);
}

/** One-based ordinal among currently active slots in the same Tier, across all slot types. */
export function slotPointCost(basePoints: number, ordinal: number): number {
 nonnegative(basePoints, '槽位基础设计点');
 if (!Number.isInteger(ordinal) || ordinal < 1) throw new Error('同 Tier 槽位序号必须为正整数');
 return basePoints * WILD_DESIGN.slotPointMultiplier ** (ordinal - 1);
}

/** Required nodes are ALL; eligible routes are OR, with ALL converters required per route. */
export function nodePrerequisitesMet(node: NodeDef, activeNodeIds: readonly string[]): boolean {
 const active = new Set(activeNodeIds);
 return node.requires.every(id => active.has(id))
  && (node.routes === undefined || node.routes.some(route => active.has(route.from) && (route.via ?? []).every(id => active.has(id))));
}

/** Slot points are repriced by active count; material cost is fixed per configured node. */
export function addNode(
 design: DesignState,
 nodeId: string,
 nodes: readonly NodeDef[],
 rules: DesignRules,
): NodePurchase {
 const node = getNode(nodeId, nodes);
 if (design.activeNodeIds.includes(node.id)) throw new Error('该节点已启用');
 if (node.tier < design.entryTier) throw new Error('不能补选建筑起始 Tier 之前的节点');
 if (node.tier > design.currentTier) throw new Error('请先完成对应 Tier 的进阶');
 if (!nodePrerequisitesMet(node, design.activeNodeIds)) throw new Error('请先启用所有前置节点');
 const ordinal = design.activeNodeIds.filter(id => { const existing = getNode(id, nodes); return existing.kind === 'slot' && existing.tier === node.tier; }).length + 1;
 const pointCost = node.kind === 'slot' ? slotPointCost(rules.slotBasePoints, ordinal) : node.points;
 nonnegative(pointCost, '节点设计点');
 for (const amount of Object.values(node.resourceCost)) nonnegative(amount, '节点物资成本');
 if (designUsed(design) + pointCost > designCapacity(design, rules)) throw new Error('建筑可用设计点不足');
 return {
  design: {
   ...design,
   activeNodeIds: [...design.activeNodeIds, node.id],
   slotBonuses: { ...design.slotBonuses },
   nodePointCosts: { ...design.nodePointCosts, [node.id]: pointCost },
  },
  pointCost,
  resourceCost: { ...node.resourceCost },
 };
}

/** Cascades only where no eligible prerequisite route survives, in stable activation order. */
export function cancellationPlan(design: DesignState, nodeId: string, nodes: readonly NodeDef[], rules: DesignRules): CancellationPlan {
 if (!design.activeNodeIds.includes(nodeId)) throw new Error('该节点尚未启用');
 const removed = new Set([nodeId]);
 let changed = true;
 while (changed) {
  changed = false;
  const remaining = design.activeNodeIds.filter(id => !removed.has(id));
  for (const id of design.activeNodeIds) {
   if (!removed.has(id) && !nodePrerequisitesMet(getNode(id, nodes), remaining)) {
    removed.add(id);
    changed = true;
   }
  }
 }
 const nodeIds = design.activeNodeIds.filter(id => removed.has(id));
 const remainingPointCosts: Record<string, number> = {};
 const slotCounts: Record<number, number> = {};
 for (const id of design.activeNodeIds.filter(id => !removed.has(id))) {
  const node = getNode(id, nodes);
  remainingPointCosts[id] = node.kind === 'slot'
   ? slotPointCost(rules.slotBasePoints, slotCounts[node.tier] = (slotCounts[node.tier] || 0) + 1)
   : design.nodePointCosts[id];
 }
 return {
  nodeIds,
  pointRefund: Object.values(design.nodePointCosts).reduce((sum, value) => sum + value, 0) - Object.values(remainingPointCosts).reduce((sum, value) => sum + value, 0),
  remainingPointCosts,
  materialRefund: 0,
  returnedBonuses: nodeIds.filter(id => design.slotBonuses[id] !== undefined)
   .map(id => ({ nodeId: id, bonusId: design.slotBonuses[id] })),
 };
}

/** Apply a plan calculated against this design. The caller returns Bonus copies to its inventory. */
export function commitCancel(design: DesignState, plan: CancellationPlan): DesignState {
 const removed = new Set(plan.nodeIds);
 const slotBonuses = { ...design.slotBonuses };
 const nodePointCosts = { ...plan.remainingPointCosts };
 for (const id of removed) {
  delete slotBonuses[id];
  delete nodePointCosts[id];
 }
 return { ...design, activeNodeIds: design.activeNodeIds.filter(id => !removed.has(id)), slotBonuses, nodePointCosts };
}

/** Advancement is separate from cancellable nodes and can only move forward by one Tier. */
export function advanceTier(design: DesignState, nextTier: number, capacityIncrement: number): DesignState {
 if (nextTier !== design.currentTier + 1) throw new Error('Tier 只能按顺序进阶，不能回退或跳级');
 nonnegative(capacityIncrement, 'Tier 新增设计容量');
 return {
  ...design,
  currentTier: nextTier,
  baseCapacity: design.baseCapacity + capacityIncrement,
  activeNodeIds: [...design.activeNodeIds],
  slotBonuses: { ...design.slotBonuses },
  nodePointCosts: { ...design.nodePointCosts },
 };
}

function getNode(id: string, nodes: readonly NodeDef[]): NodeDef {
 const node = nodes.find(item => item.id === id);
 if (!node) throw new Error(`找不到设计节点：${id}`);
 return node;
}

function nonnegative(value: number, label: string): void {
 if (!Number.isFinite(value) || value < 0) throw new Error(`${label}必须为非负数`);
}
