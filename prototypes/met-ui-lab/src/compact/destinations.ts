import type {Condition, Workspace} from '../types';
export function conditionDestination(c:Condition):[Workspace,string|undefined,string?] {
 if(['medical','education','theme-entertainment'].includes(c.id))return ['residents',c.id==='theme-entertainment'?'entertainment':c.id];
 if(['food','theme-material','theme-currency'].includes(c.id))return ['economy','balance',c.id==='theme-material'?'material':c.id==='theme-currency'?'currency':'food'];
 return [c.destination,c.tab];
}
