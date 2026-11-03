import type { Building, CommandResult, DemoAction, GameState, Project, Tile } from '../types';
import { addNode, cancellationPlan, commitCancel, designCapacity, designUsed, nodePrerequisitesMet, slotPointCost, type DesignState, type NodeDef } from './star';
import { completeWildAdvancement, createWildDesign, unlockCompletedForm, wildDesignRules } from './wild-design';
import { upgradeConstructionCP } from './economy-basis';
import { WILD_DESIGN, WILD_FARM_FORMS, WILD_ECONOMY, WILD_RESEARCH, WILD_SLOT_NAMES, type WildCost, type WildSlotType } from './wild-config';

export interface RuntimeConfig {
 ordinaryMaterial: number | null;
 advancementLowIndustrial: number | null;
 tierBuildCP: Partial<Record<number, number>>;
 housingEffectFraction: number | null;
 primaryEffectFraction: number | null;
 research: { currency: number; targetPoints: number; repeatTargetPoints?: number } | null;
}
/** Unconfirmed values remain unavailable; these are not trial balance values. */
export const DEFAULT_WILD_CONFIG: RuntimeConfig = {
 ordinaryMaterial: WILD_ECONOMY.materialPerPeriod, advancementLowIndustrial: WILD_ECONOMY.lowIndustrialPerPeriod, tierBuildCP: { 1: WILD_ECONOMY.tierOneBuildCP },
 housingEffectFraction: WILD_ECONOMY.ordinaryEffectFraction, primaryEffectFraction: WILD_ECONOMY.ordinaryEffectFraction,
 research: { currency: 60, targetPoints: WILD_RESEARCH.firstHousingPoints },
};
export interface WildSession {
 lastCityScienceAtMs?:number;
 hopeMaterialSold?:number;
 designs: Record<string, DesignState>;
 housingResearch: number;
 researchProgress: number | null;
 researchSources: { buildingId: string; kind: 'secondary-school' | 'university' | 'research-institute'; pointsPerCityMonth?: number }[];
 lowIndustrial: number;
 unlockedForms: string[];
 config: RuntimeConfig;
}
export interface RuntimeNode extends NodeDef {
 label: string;
 description: string;
 effect?: { housingFraction?: number; primary?: number };
 missing?: string;
 advancement?: boolean;
 advancementForm?:'apartment'|'farm';
 layout?: {column:number;row:number};
 visualParents?: string[];
 connections?: {from:string;conversion?:string}[];
}
export interface NodeQuote {
 node?: RuntimeNode;
 allowed: boolean;
 reason: string;
 missing?: string;
 pointCost: number;
 constructionCP: number | null;
 resourceCost: WildCost;
 capacity: number;
 used: number;
 reserved: number;
}
const clone = <T>(value: T): T => structuredClone(value);
const configured = (value: number | null | undefined): value is number => typeof value === 'number' && Number.isFinite(value) && value >= 0;
function record(state: GameState, title: string, text: string, tileId?: string) {
 state.records.unshift({ id: `r${state.nextId++}`, category: 'event', title, text, month: state.month, tileId });
}
function designFor(state: GameState, building: Building): DesignState {
 return state.wild?.designs[building.id] ?? createWildDesign(building.tier);
}
export function enableWildSession(input: GameState): GameState {
 const state = clone(input);
 if (state.wild) return state;
 state.wild = {
  designs: {}, housingResearch: 0, researchProgress: null, researchSources: [], lowIndustrial: WILD_ECONOMY.startingLowIndustrial,
  unlockedForms: [], config: clone(DEFAULT_WILD_CONFIG),
 };
 for (const tile of state.tiles) if (tile.building) state.wild.designs[tile.building.id] = createWildDesign(tile.building.tier);
 record(state, 'Wild 开局补给', `获得 ${WILD_ECONOMY.startingLowIndustrial} 件低级工业品，可用于农居进阶；后续任务补给及 T3 自产配方尚待确定。`);
 return state;
}
/** Only the supplied farm T1 topology is connected; other trees remain explicitly pending. */
export function wildNodes(state: GameState, tileId: string): RuntimeNode[] {
 const building = state.tiles.find(tile => tile.id === tileId)?.building;
 if (!state.wild || !building) return [];
 const design = designFor(state, building), config = state.wild.config;
 const result: RuntimeNode[] = [];
 for (let tier = design.entryTier; tier <= Math.max(3, design.currentTier); tier++) {
  const drawn = building.kind === 'farm' && tier === 1;
  const prefix = `t${tier}-`, root = prefix+'foundation', housing = prefix+'housing', primary = prefix+'primary', converter = prefix+'link';
  const structureMissing = drawn ? '' : '此建筑 Tier 的星图结构与效果待确认';
  const statNode = (stat:'foundation'|'housing'|'primary'):RuntimeNode => {
   const housingEffect = stat !== 'primary' ? config.housingEffectFraction : undefined;
   const primaryEffect = stat !== 'housing' ? config.primaryEffectFraction : undefined;
   const missing = [structureMissing, !configured(config.ordinaryMaterial) ? '普通节点材料价格待确认' : '',
    stat !== 'primary' && !configured(housingEffect) ? '住房增幅待确认' : '', stat !== 'housing' && !configured(primaryEffect) ? '主要功能增幅待确认' : ''].filter(Boolean).join('；');
   return { id:prefix+stat, tier, kind:'stat', requires:drawn && stat !== 'foundation' ? [root] : [], points:WILD_DESIGN.ordinaryNodePoints,
    resourceCost:configured(config.ordinaryMaterial)?{material:config.ordinaryMaterial}:{},
    label:stat==='foundation'?'农居改良':stat==='housing'?'住房改善':'农业增产',
    description:'普通节点按百分比提供加值；同类加值相加，并作用于当前形态的基数。完工后生效。',
    effect:{...(configured(housingEffect)?{housingFraction:housingEffect}:{}),...(configured(primaryEffect)?{primary:primaryEffect}:{})},
    missing:missing||undefined, layout:drawn?{column:stat==='foundation'?2:stat==='housing'?1:3,row:stat==='foundation'?1:2}:undefined };
  };
  if(drawn)result.push(statNode('foundation'));
  result.push(statNode('housing'),statNode('primary'));
  for (const [index,slotType] of (['organization','link','fusion'] as WildSlotType[]).entries()) result.push({
   id:prefix+slotType,tier,kind:'slot',requires:[],
   routes:drawn?(slotType==='organization'?[{from:housing}]:slotType==='fusion'?[{from:primary}]:[{from:housing},{from:primary}]):undefined,
   visualParents:drawn?(slotType==='organization'?[housing]:slotType==='fusion'?[primary]:[housing,primary]):undefined,
   layout:drawn?{column:index+1,row:3}:undefined,points:WILD_DESIGN.slotBasePoints,
   resourceCost:tier===1?{basicGoods:WILD_DESIGN.tierOneSlotBasicGoods}:{},slotType,
   label:WILD_SLOT_NAMES[slotType],description:slotType==='link'&&drawn?'完成后开放联系 Bonus 槽，同时接通图中住房与农业两路的转换；不开槽时只能沿原路线继续。':'可沿原路线经过这个位置，不必购买槽位；购买后才开放专用 Bonus 装配。',
   missing:[structureMissing,tier===1?'':'高 Tier 槽位物资价格待确认'].filter(Boolean).join('；')||undefined,
  });
  if(tier<3)for(const form of drawn?['apartment','farm']:['advance']){
   const own=form==='apartment'?housing:primary,other=form==='apartment'?primary:housing;
   result.push({id:prefix+form,tier,kind:'stat',requires:[],points:WILD_DESIGN.advancementNodePoints,
    routes:drawn?[{from:own},{from:other,via:[converter]}]:undefined,
    connections:drawn?[{from:prefix+(form==='apartment'?'organization':'fusion')},{from:converter,conversion:converter}]:undefined,
    layout:drawn?{column:form==='apartment'?1:3,row:4}:undefined,
    resourceCost:configured(config.advancementLowIndustrial)?{lowIndustrial:config.advancementLowIndustrial}:{},
    label:drawn?(form==='apartment'?'T2 · 公寓':'T2 · 农场'):`进阶 T${tier+1}`,
    description:'沿原路线可跳过开槽继续进阶；跨路线必须先完成转换槽。进阶改变形态基数，继承同类百分比加值。',advancement:true,advancementForm:drawn?(form as 'apartment'|'farm'):undefined,
    missing:[!configured(config.advancementLowIndustrial)?'进阶低级工业品价格待确认':'',drawn?'':'进阶形态基数与后续建造报价待确认'].filter(Boolean).join('；')});
  }
 }
 return result;
}
function nodePoints(design: DesignState, node: RuntimeNode, nodes: RuntimeNode[]): number {
 if (node.kind !== 'slot') return node.points;
 const ordinal = design.activeNodeIds.filter(id => nodes.some(item => item.id === id && item.kind === 'slot' && item.tier === node.tier)).length + 1;
 return slotPointCost(WILD_DESIGN.slotBasePoints, ordinal);
}
export function quoteWildNode(state: GameState, tileId: string, nodeId: string): NodeQuote {
 const nodes = wildNodes(state, tileId), node = nodes.find(item => item.id === nodeId);
 const building = state.tiles.find(tile => tile.id === tileId)?.building;
 const quote: NodeQuote = { node, allowed: false, reason: '', pointCost: 0, constructionCP: null, resourceCost: node ? { ...node.resourceCost } : {}, capacity: 0, used: 0, reserved: 0 };
 if (!state.wild || !building || !node) return { ...quote, reason: '未找到 Wild 建筑设计节点' };
 const design = designFor(state, building), rules = wildDesignRules(state.wild.housingResearch);
 quote.capacity = designCapacity(design, rules); quote.used = designUsed(design); quote.pointCost = nodePoints(design, node, nodes);
 const pending = state.projects.find(project => project.tileId === tileId);
 if (pending?.wildNode) { const pendingNode = nodes.find(item => item.id === pending.wildNode); if (pendingNode) quote.reserved = nodePoints(design, pendingNode, nodes); }
 if (pending?.wildAdvance) quote.reserved = WILD_DESIGN.advancementNodePoints;
 const baseCP = state.wild.config.tierBuildCP[design.currentTier];
 if (configured(baseCP) && baseCP > 0) quote.constructionCP = upgradeConstructionCP(baseCP, quote.pointCost);
 const missing = [node.missing, quote.constructionCP === null ? `T${design.currentTier} 建造 CP 待确认` : ''].filter(Boolean).join('；');
 quote.missing = missing || undefined;
 if (state.readonly) quote.reason = '该街区已归档，只能查阅';
 else if (design.activeNodeIds.includes(nodeId)||Object.values(design.advancementNodes??{}).includes(nodeId)) quote.reason = '该节点已完工';
 else if (node.tier > design.currentTier || node.advancement && node.tier !== design.currentTier) quote.reason = '请先完成对应 Tier 的进阶';
 else if (pending) quote.reason = '该建筑已有施工项目';
 else if (!nodePrerequisitesMet(node, design.activeNodeIds)) quote.reason = '原路线前置未完成；跨路线还需完成对应转换槽';
 else if (missing) quote.reason = missing;
 else if (quote.used + quote.pointCost > quote.capacity) quote.reason = '建筑可用设计点不足';
 else if ((quote.resourceCost.material ?? 0) > state.resources.material || (quote.resourceCost.basicGoods ?? 0) > state.resources.goods || (quote.resourceCost.lowIndustrial ?? 0) > state.wild.lowIndustrial) quote.reason = '节点所需物资库存不足';
 else { quote.allowed = true; quote.reason = '可进入施工队列；效果在完工后生效'; }
 return quote;
}

export function handleWildCommand(input: GameState, action: DemoAction): CommandResult | undefined {
 if (!action.type.startsWith('wild-')) return undefined;
 const fail = (message: string): CommandResult => ({ state: input, ok: false, message });
 if (input.readonly) return fail('该街区已归档，只能查阅');
 if (action.type === 'wild-enable') {
  if (input.wild) return fail('Wild 设计模式已经启用');
  if(input.projects.length)return fail('请先完成现有演示工程，再启用 Wild 星图');
  const state = enableWildSession(input); record(state, 'Wild 设计模式', '为现有建筑按当前 Tier 建立独立设计；按已确认规则发放一次开局工业品补给，未追认历史进阶。');
  return { state, ok: true, message: `已开始 Wild 设计；获得 ${WILD_ECONOMY.startingLowIndustrial} 件开局低级工业品` };
 }
 if (!input.wild) return fail('请先启用 Wild 设计模式');
 const state = clone(input), session = state.wild!;
 const tile = state.tiles.find(item => item.id === action.tileId), building = tile?.building;
 if (action.type === 'wild-start-research') {
  const price = housingResearchQuote(state);
  if (!price || !configured(price.currency) || !configured(price.targetPoints) || price.targetPoints <= 0) return fail('后续住房研究报价尚待确认');
  if (session.researchProgress !== null) return fail('已有住房研究正在进行');
  if (researchRate(state) <= 0) return fail('需要运行中的中学、大学或研究所提供研究点');
  if (state.resources.currency < price.currency) return fail('住房研究启动通货不足');
  state.resources.currency -= price.currency; session.researchProgress = 0;
  record(state, '住房研究开始', `支付 ${price.currency} 通货启动；需累计 ${price.targetPoints} 研究点，由学校或研究所按城市时间提供，不随街区 DT 加速。`);
  return { state, ok: true, message: '住房研究已启动；累计足够研究点后，每座建筑增加 2 点设计容量' };
 }
 if (!building || !tile) return fail('请选择一座现有建筑');
 if (action.type === 'wild-start-node') {
  const quote = quoteWildNode(input, tile.id, String(action.nodeId));
  if (!quote.allowed || !quote.node || quote.constructionCP === null) return fail(quote.reason);
  const node = quote.node;
  session.designs[building.id] = clone(designFor(state, building));
  state.resources.goods -= quote.resourceCost.basicGoods ?? 0;
  session.lowIndustrial -= quote.resourceCost.lowIndustrial ?? 0;
  state.projects.push({ id: `p${state.nextId++}`, tileId: tile.id, type: 'upgrade', kind: building.kind,
   specialization: building.specialization, operator: building.operator, targetTier: node.advancement ? building.tier + 1 : building.tier,
   totalCP: quote.constructionCP, doneCP: 0, totalMaterial: quote.resourceCost.material ?? 0, usedMaterial: 0, priority: 2, paused: false, wildNode: node.id });
  const paid = [quote.resourceCost.basicGoods ? `${quote.resourceCost.basicGoods} 基础制成品` : '', quote.resourceCost.lowIndustrial ? `${quote.resourceCost.lowIndustrial} 低级工业品` : ''].filter(Boolean);
  record(state, 'Wild 节点施工', `${node.label}：预留 ${quote.pointCost} 设计点，施工 ${quote.constructionCP} CP；${quote.resourceCost.material ?? 0} 材料随施工消耗${paid.length ? `；开工支付 ${paid.join('、')}` : ''}。`, tile.id);
  return { state, ok: true, message: '节点已进入施工队列；设计点已预留，完工后才生效' };
 }
 if (action.type === 'wild-cancel-node') {
  if (state.projects.some(project => project.tileId === tile.id)) return fail('请先完成该建筑的施工项目');
  const design = designFor(state, building);
  try {
   const plan = cancellationPlan(design, String(action.nodeId), wildNodes(state, tile.id), wildDesignRules(session.housingResearch));
   session.designs[building.id] = commitCancel(design, plan);
   for (const bonus of plan.returnedBonuses) state.bonuses[bonus.bonusId] = (state.bonuses[bonus.bonusId] ?? 0) + 1;
   record(state, 'Wild 节点取消', `取消 ${plan.nodeIds.join('、')}；释放 ${plan.pointRefund} 设计点，物资不退还，已装 Bonus 返回库存。`, tile.id);
   return { state, ok: true, message: '节点已取消；后继节点一并取消，剩余槽位已重新计点' };
  } catch (error) { return fail((error as Error).message); }
 }
 return fail('未提供此 Wild 操作');
}

/** Mutates settlement's detached state; caller removes completed project. */
export function finishWildProject(state: GameState, project: Project): boolean {
 if (!state.wild) return false;
 const tile = state.tiles.find(item => item.id === project.tileId), building = tile?.building;
 if (!tile || !building) return false;
 const session = state.wild, design = designFor(state, building), rules = wildDesignRules(session.housingResearch);
 if (project.wildNode) {
  const nodes = wildNodes(state, tile.id), node = nodes.find(item => item.id === project.wildNode);
  if (!node) throw new Error('Wild 节点工程缺少有效节点');
  if(node.advancement){
   if(!node.advancementForm)throw new Error('进阶形态尚未配置');
   const form=WILD_FARM_FORMS[node.advancementForm],fromTier=design.currentTier;
   const next=completeWildAdvancement(design,rules);
   session.designs[building.id]={...next,advancementNodes:{...design.advancementNodes,[form.tier]:node.id}};
   building.tier=form.tier;building.specialization='wild-'+node.advancementForm;building.name=form.name;
   building.housing=form.housing;building.flows.food=form.food;
   session.unlockedForms=unlockCompletedForm(session.unlockedForms,{formId:'farm-t2-'+node.advancementForm,fromTier,toTier:form.tier,completed:true});
   record(state,'Wild 进阶完工',form.name+'已建成；新增 2 点额度、进阶永久占用 2 点，继承已完成的百分比节点。形态直建资格已解锁，直建报价待确定。',tile.id);
   return true;
  }
  session.designs[building.id] = addNode(design, node.id, nodes, rules).design;
  record(state, 'Wild 节点完工', `${node.label}已生效；占用设计点 ${session.designs[building.id].nodePointCosts[node.id]}。`, tile.id);
  return true;
 }
 if (project.wildAdvance) {
  session.designs[building.id] = completeWildAdvancement(design, rules);
  session.unlockedForms = unlockCompletedForm(session.unlockedForms, { formId: `${project.kind}-t${project.targetTier}-${project.specialization}`, fromTier: design.currentTier, toTier: project.targetTier, completed: true });
  record(state, 'Wild 进阶完工', `T${project.targetTier} 形态已完成并解锁直建；进阶永久占用 2 点设计额度。`, tile.id);
 }
 return false;
}
/** Returns a detached building; old Bonus and operation factors are applied by the model afterwards. */
export function applyWildEffects(state: GameState, tile: Tile, building: Building): Building {
 if (!state.wild) return building;
 const result = clone(building), design = designFor(state, building), nodes = wildNodes(state, tile.id);
 const form=building.kind==='farm'?(building.tier===1?WILD_FARM_FORMS.foundation:building.specialization==='wild-apartment'?WILD_FARM_FORMS.apartment:building.specialization==='wild-farm'?WILD_FARM_FORMS.farm:undefined):undefined;
 if(form){result.housing=form.housing;result.flows.food=form.food;}
 const baseHousing=result.housing,baseFood=result.flows.food;
 let primary = 0;
 for (const id of design.activeNodeIds) {
  const effect = nodes.find(node => node.id === id)?.effect;
  result.housing += baseHousing * (effect?.housingFraction ?? 0); primary += effect?.primary ?? 0;
 }
 if (building.kind === 'center') { const key = building.specialization === 'school' ? 'education' : 'medical'; result.services[key] += building.services[key] * primary; }
 else { const key = building.kind === 'farm' ? 'food' : building.specialization === 'goods' ? 'goods' : 'material'; result.flows[key] += (key==='food'?baseFood:building.flows[key]) * primary; }
 return result;
}
/** Registered facility identities are explicit; the prototype does not infer higher schools. */
export function researchRate(state: GameState): number {
 return state.wild?.researchSources.reduce((sum, source) => {
  const building = state.tiles.find(tile => tile.building?.id === source.buildingId)?.building;
  return sum + (building?.running ? source.pointsPerCityMonth ?? WILD_RESEARCH.annualPoints[source.kind]/12 : 0);
 }, 0) ?? 0;
}
/** Only the city clock may call this; accelerated block months/DT do not enter this API. */
export function advanceWildResearch(state: GameState, cityMonths: number): void {
 const session = state.wild, price = housingResearchQuote(state);
 if (!session || session.researchProgress === null || !price || cityMonths <= 0) return;
 session.researchProgress += researchRate(state) * cityMonths;
 if (session.researchProgress + 1e-9 >= price.targetPoints) {
  session.housingResearch++; session.researchProgress = null;
  record(state, '住房研究完成', `第 ${session.housingResearch} 次住房研究完成；所有现有和未来建筑的设计容量增加 2 点。`);
 }
}

export function housingResearchQuote(state:GameState):{currency:number;targetPoints:number}|null {
 const session=state.wild,price=session?.config.research;
 if(!session||!price)return null;
 const targetPoints=session.housingResearch===0?price.targetPoints:price.repeatTargetPoints;
 return targetPoints===undefined?null:{currency:price.currency,targetPoints};
}
