import {useState} from 'react';
import {useLab} from '../context';
import {Button,Icon,Progress,InlineConfirm,Fold,Tag} from '../ui';
import {fmt,prices} from '../model';
import {hopeTechnologyStatus,HOPE_TECHNOLOGY} from './wild-trade';
import {housingResearchQuote,researchRate} from './wild-runtime';
import {quoteScienceDT} from './wild-science';
import './wild-technology.css';

export function HopeExchange(){
 const {game,act,go}=useLab();
 if(!game.wild)return null;
 const status=hopeTechnologyStatus(game),pending=game.orders.filter(o=>o.counterparty==='hope'&&o.status==='open');
 const remaining=Math.max(0,HOPE_TECHNOLOGY.materialThreshold-status.sold);
 return <section className="hope-exchange" aria-label="希望城技术贸易">
  <header><div className="hope-city-mark"><Icon name="home" size={38}/><i/><Icon name="education" size={22}/></div><div><span className="hope-eyebrow">外城往来 / 技术交流</span><h2>来自希望城的蓝图</h2><p>先建立贸易，再把新知识带回街区。</p></div></header>
  <div className="hope-journey"><span className="done"><Icon name="material"/><small>材料贸易</small></span><i className={status.unlocked?'done':''}/><span className={status.unlocked?'done':''}><Icon name="archive"/><small>购买资格</small></span><i className={status.owned?'done':''}/><span className={status.owned?'done':''}><Icon name="home"/><small>住房设计 I</small></span></div>
  {!status.unlocked&&<div className="hope-trade-step"><div><strong>向希望城出售材料</strong><span>{fmt(status.sold)} / 10</span></div><Progress value={status.sold} max={10} label="希望城实际材料成交"/>
   {pending.length?<div className="hope-pending">{pending.map(o=><div key={o.id}><Icon name="clock"/><span>{fmt(o.quantity)} 材料 · 待成交</span><Button variant="small" disabled={game.readonly} onClick={()=>act({type:'cancel-order',id:o.id})}>撤单</Button></div>)}<small>推进街区月份完成演示成交；挂单和撤单不计贸易进度。</small></div>:<InlineConfirm label={'向希望城出售 '+fmt(remaining)+' 材料'} disabled={game.readonly||game.resources.material<remaining} onConfirm={()=>act({type:'create-order',counterparty:'hope',resource:'material',side:'sell',quantity:remaining})}><p>预留 {fmt(remaining)} 材料，成交收入 {fmt(remaining*prices.material)} 通货。只有实际成交才开放技术购买。</p></InlineConfirm>}
  </div>}
  <div className={'hope-blueprint '+(status.owned?'owned':'')}><div className="hope-blueprint-mark"><Icon name="home" size={40}/><b>+2</b></div><div><Tag tone={status.owned?'good':''}>{status.owned?'已掌握':status.unlocked?'可购买':'贸易解锁'}</Tag><h3>住房设计 I</h3><p>每栋建筑设计容量 <strong>+2 点</strong></p><small>现有建筑、新建建筑均享受；节点仍需自行施工。</small></div></div>
  {status.unlocked&&!status.owned&&<div className="hope-purchase"><span><strong>60 通货</strong><small>支付后 {fmt(game.resources.currency-60)} 通货</small></span><InlineConfirm label="购买住房设计 I" disabled={!status.canBuy} onConfirm={()=>act({type:'wild-buy-housing-technology'})}><p>支付 60 通货，获得住房设计 I。已成交材料不再扣除。</p></InlineConfirm></div>}
  {!status.canBuy&&status.unlocked&&!status.owned&&<p className="gold">{status.reason}</p>}
  {status.owned&&<Button variant="ghost" onClick={()=>go('tasks','science')}>查看已掌握科技 ›</Button>}
 </section>;
}

export function WildResearch(){
 const {game,act,go}=useLab();const [amount,setAmount]=useState('1');
 const s=game.wild;if(!s)return null;
 const quote=housingResearchQuote(game),rate=researchRate(game),boost=quoteScienceDT(game,Number(amount));
 const active=s.researchProgress!==null;
 return <div className="wild-research" aria-label="城市科技">
  <header><span className="hope-eyebrow">城市成长 / 知识积累</span><h2>让新知识改变城市</h2><p>早期从希望城购买技术，之后以学校和研究设施积累研究能力。</p></header>
  <div className="hope-blueprint"><div className="hope-blueprint-mark"><Icon name="home" size={40}/><b>+2</b></div><div><Tag tone={s.housingResearch?'good':''}>{s.housingResearch?'已掌握':'尚未掌握'}</Tag><h3>住房设计 I</h3><p>每栋建筑增加 2 点设计容量</p><Button variant="ghost" onClick={()=>go('economy','market')}>{s.housingResearch?'查看希望城贸易记录':'前往希望城取得购买资格'} ›</Button></div></div>
  {quote&&<section className="research-project"><div className="research-orbit"><Icon name="education" size={34}/></div><h3>{active?'研究进行中':'自主研究'}</h3><p>{active?fmt(s.researchProgress!,4)+' / '+quote.targetPoints+' 研究点':quote.currency+' 通货启动 · 需要 '+quote.targetPoints+' 研究点'}</p><Progress value={s.researchProgress??0} max={quote.targetPoints} label="住房设计研究进度"/>
   <p>{fmt(rate*12)} 研究点／城市年</p>
   {!active?<InlineConfirm label="开始住房研究" disabled={game.readonly||rate<=0||game.resources.currency<quote.currency} onConfirm={()=>act({type:'wild-start-research'})}><p>支付 {quote.currency} 通货启动；由现有研究设施随城市时间填充进度。</p></InlineConfirm>:<div className="research-boost"><label>独立投入 DT<input aria-label="科研 DT 数量" type="number" min="0.01" step="0.1" value={amount} onChange={e=>setAmount(e.target.value)}/></label><p>{boost.allowed?'消耗 '+fmt(boost.dtSpent,4)+' DT，增加 '+fmt(boost.pointsAdded,4)+' 研究点':boost.reason}</p><InlineConfirm label="投入科研 DT" disabled={!boost.allowed} onConfirm={()=>act({type:'wild-science-dt',amount:Number(amount)})}><p>扣除 {fmt(boost.dtSpent,4)} DT，只推进科研 {fmt(boost.cityMonths,4)} 城市月；街区和施工不推进。</p></InlineConfirm></div>}
  </section>}
  <div className="research-facilities"><div><Icon name="education" size={26}/><strong>中学</strong><span>1 点／年</span></div><div><Icon name="home" size={26}/><strong>大学</strong><span>20 点／年</span></div><div><Icon name="layers" size={26}/><strong>研究所</strong><span>20 点／年</span></div></div>
  {rate===0&&<p className="source-line">当前没有运行中的研究设施。中学沿“邻里中心 → 初级学校 → 中学”取得；学校星图和报价仍待确定。</p>}
  <Fold title="研究时间与来源"><p>1 城市年＝216 现实天。退出后继续积累科研；街区暂停、倍速和“＋月”不推进城市科研。</p><p>1 DT 可单独推进 0.8 城市月科研，使用同一个 DT 余额。当前科技完成后停止，余量不自动研究下一项。</p>{s.researchSources.map(source=><p key={source.buildingId}>{game.tiles.find(t=>t.building?.id===source.buildingId)?.building?.name||source.buildingId} · {source.kind==='secondary-school'?'中学':source.kind==='university'?'大学':'研究所'}</p>)}</Fold>
 </div>;
}
