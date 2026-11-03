import type {CommandResult,DemoAction,GameState,Order} from '../types';
export const HOPE_TECHNOLOGY = {id:'housing-design-1',name:'住房设计 I',materialThreshold:10,currency:60} as const;
export function hopeTechnologyStatus(state:GameState){
 const sold=state.wild?.hopeMaterialSold??0;
 const owned=(state.wild?.housingResearch??0)>0;
 const unlocked=sold>=HOPE_TECHNOLOGY.materialThreshold;
 const reason=!state.wild?'请先启用 Wild 星图':state.readonly?'历史街区只读':owned?'已掌握住房设计 I':!unlocked?'先完成与希望城的材料贸易':state.wild.researchProgress!==null?'这项科技已在研究中':state.resources.currency<HOPE_TECHNOLOGY.currency?'通货不足':'';
 return {sold,owned,unlocked,canBuy:!reason,reason};
}
/** Called only when an open order actually changes to filled. */
export function recordHopeTrade(state:GameState,order:Order){
 if(!state.wild||order.counterparty!=='hope'||order.side!=='sell'||order.resource!=='material')return;
 const before=state.wild.hopeMaterialSold??0;
 state.wild.hopeMaterialSold=before+order.quantity;
 if(before<HOPE_TECHNOLOGY.materialThreshold&&state.wild.hopeMaterialSold>=HOPE_TECHNOLOGY.materialThreshold)
  state.records.unshift({id:`r${state.nextId++}`,category:'achievement',title:'希望城开放技术购买',text:'累计实际出售 10 材料，已开放住房设计 I。支付 60 通货即可获得；材料不会再次扣除。',month:state.month});
}
export function handleHopeCommand(input:GameState,action:DemoAction):CommandResult|undefined{
 if(action.type!=='wild-buy-housing-technology')return;
 const status=hopeTechnologyStatus(input);
 if(!status.canBuy)return {state:input,ok:false,message:status.reason};
 const state=structuredClone(input);
 state.resources.currency-=HOPE_TECHNOLOGY.currency;
 state.wild!.housingResearch++;
 state.records.unshift({id:`r${state.nextId++}`,category:'achievement',title:'从希望城购得住房设计 I',text:'支付 60 通货；全部现有与未来建筑设计容量 +2。不赠送节点，不自动施工。',month:state.month});
 return {state,ok:true,message:'已掌握住房设计 I；每栋建筑设计容量 +2'};
}
