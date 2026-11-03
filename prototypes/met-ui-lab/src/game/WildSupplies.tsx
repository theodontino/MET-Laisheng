import {useLab} from '../context';
import {fmt} from '../model';
import {Fold, Icon} from '../ui';
import './wild-supplies.css';

/** A separate construction stock, never a monthly operating flow. */
export function WildSupplies(){
 const {game,locate}=useLab();
 if(!game.wild)return null;
 const records=game.records.filter(r=>r.title==='Wild 开局补给'||r.title==='Wild 节点施工'&&r.text.includes('低级工业品'));
 return <section className="wild-supplies" aria-label="低级工业品储备">
  <div className="wild-supply-strip">
   <span className="wild-supply-mark"><Icon name="build" size={25}/></span>
   <div className="wild-supply-stock"><span>低级工业品</span><strong>{fmt(game.wild.lowIndustrial)}<small>可用</small></strong></div>
   <div className="wild-supply-purpose"><span>用于农居进阶 · T3 可建立自产（配方待定）</span><small>与基础制成品分开储存 · 进阶开工时扣除，不计经营月流</small></div>
  </div>
  <Fold title={`补给与使用记录 · ${records.length}`}>
   {records.length?records.map(r=><div className="wild-supply-record" key={r.id}><span>第 {r.month} 月</span><div><strong>{r.title}</strong><p>{r.text}</p></div>{r.tileId&&<button className="text-link" onClick={()=>locate(r.tileId!)}>定位</button>}</div>):<p>暂无相关记录；可用量取当前库存。</p>}
  </Fold>
 </section>;
}
