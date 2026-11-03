import { useLab } from '../context';
import { StarWindow, type StarNodeView } from './StarWindow';
import { wildNodes, quoteWildNode } from './wild-runtime';
import { cancellationPlan, designCapacity, designUsed } from './star';
import { createWildDesign, wildDesignRules } from './wild-design';
import { fmt } from '../model';
import {WILD_FARM_FORMS, type WildMaterial} from './wild-config';
const materialNames:Record<WildMaterial,string>={material:'材料',basicGoods:'基础制成品',lowIndustrial:'低级工业品',industrial:'工业品',precisionIndustrial:'精密工业品'};
export default function WildStarPanel({tileId,onClose}:{tileId:string;onClose:()=>void}) {
 const {game,act}=useLab();
 const tile=game.tiles.find(t=>t.id===tileId),building=tile?.building,session=game.wild;
 if(!building||!session)return null;
 const design=session.designs[building.id]||createWildDesign(building.tier),rules=wildDesignRules(session.housingResearch);
 const nodes=wildNodes(game,tileId),pending=game.projects.find(p=>p.tileId===tileId);
 const reserved=pending?.wildNode?quoteWildNode(game,tileId,pending.wildNode).reserved:0;
 const views:StarNodeView[]=nodes.map(node=>{
  const quote=quoteWildNode(game,tileId,node.id),active=design.activeNodeIds.includes(node.id)||Object.values(design.advancementNodes??{}).includes(node.id);
  const changes:string[]=[];
  if(node.effect?.housingFraction!==undefined)changes.push(`住房基数 ×（1＋同类加值），本节点 +${fmt(node.effect.housingFraction*100)}%`);
  if(node.effect?.primary!==undefined)changes.push(`主要能力加值 +${fmt(node.effect.primary*100)}%，与同类加值相加`);
  if(node.kind==='slot')changes.push('完工后开放一个专用 Bonus 槽；Bonus 需另外获得');
  if(node.advancementForm){const form=WILD_FARM_FORMS[node.advancementForm];changes.push('形态基数：住房 '+form.housing+' 人 · 粮食 '+form.food+'／月；继承加值另计')}
  if(node.advancement)changes.push('晋级额度 +2，进阶持续占用 2 点；完工解锁形态直建');
  return {id:node.id,label:node.label,tier:node.tier,kind:node.advancement?'advance':node.kind,description:node.description,requires:node.requires,routes:node.routes,visualParents:node.visualParents,connections:node.connections,layout:node.layout,slotType:node.slotType,
   status:active?'active':pending?.wildNode===node.id?'building':quote.allowed?'available':'locked',
   points:active?(node.advancement?2:design.nodePointCosts[node.id]):quote.pointCost,constructionCP:quote.constructionCP===null?null:Number(quote.constructionCP.toFixed(4)),
   resourceLines:Object.entries(quote.resourceCost).map(([key,value])=>{
    const stock=key==='material'?game.resources.material:key==='basicGoods'?game.resources.goods:key==='lowIndustrial'?session.lowIndustrial:undefined;
    return `${materialNames[key as WildMaterial]} ${fmt(value!)}${stock===undefined?'':` / 库存 ${fmt(stock)}`}${key==='material'?' · 随施工消耗':' · 开工支付'}`;
   }),
   blockedReason:active?undefined:quote.allowed?undefined:quote.reason,changes};
 });
 return <StarWindow title={`${building.name} · ${tile.q}, ${tile.r}`} subtitle={`${game.name} · 普通节点增加百分比，进阶改变基数；未确认报价明确锁定`} entryTier={design.entryTier} currentTier={design.currentTier}
  capacity={designCapacity(design,rules)} used={designUsed(design)+reserved} nodes={views}
  pendingText={pending?`施工 ${fmt(pending.doneCP)} / ${fmt(pending.totalCP)} CP；预留 ${reserved} 点。可在施工栏目调整优先级或暂停。`:`已掌握住房设计 ${session.housingResearch} 项 · 每项增加 2 点设计额度`}
  onClose={onClose} onBuild={id=>{act({type:'wild-start-node',tileId,nodeId:id})}}
  onCancelNode={id=>{act({type:'wild-cancel-node',tileId,nodeId:id})}}
  cancellation={game.readonly||pending?undefined:id=>{const plan=cancellationPlan(design,id,nodes,rules);return {labels:plan.nodeIds.map(n=>nodes.find(x=>x.id===n)?.label||n),refundPoints:plan.pointRefund,bonusNames:plan.returnedBonuses.map(b=>b.bonusId)}}}/>
}
