import { useLab } from '../context';
import { Button, Tag, Tabs, SectionTitle, Search, Empty, SourceLine } from '../ui';
import type { RecordEntry } from '../types';
import './development.css';
const categoryNames:Record<RecordEntry['category'],string>={event:'城市事件',settlement:'月度结算',achievement:'成就记录',rule:'规则与来源'};
export default function Archive(){
 const {game,view,remember,locate,inspect,openModal}=useLab();
 const category=['event','settlement','achievement','rule'].includes(view.tab)?view.tab:'all';
 const query=view.filter||'';
 const records=game.records.filter(r=>(category==='all'||r.category===category)&&`${r.title}${r.text}${categoryNames[r.category]}`.includes(query));
 const selected=records.find(r=>r.id===view.selected)||records[0];
 return <div className="development-panel archive-panel">
  <div className="development-intro"><div><span className="eyebrow">城市记忆 · ARCHIVE</span><h2>{game.readonly?'永久街区档案':'城市档案'}</h2><p className="muted">查阅月度结算、经营事件与成就。这里保留发生过的事，以及它们的来源。</p></div><Tag tone={game.readonly?'gold':''}>{game.readonly?'永久只读':'持续记录中'}</Tag></div>
  {game.readonly&&<div className="card archive-epilogue"><div><span className="eyebrow">已归档街区</span><h3>{game.name}</h3><p className="muted">{game.origin} · {game.theme} · 经营至第 {game.month} 月</p><p>街区已经结束，城市与完整经营记录保留供查阅。</p></div><Button variant="primary" onClick={()=>openModal({kind:'newgame'})}>独立开始新演示</Button></div>}
  <Tabs value={category} onChange={tab=>remember({tab,selected:''})} items={[{id:'all',label:'全部记录',count:game.records.length},...(['event','settlement','achievement','rule'] as const).map(id=>({id,label:categoryNames[id],count:game.records.filter(r=>r.category===id).length}))]}/>
  <div className="toolbar"><Search value={query} onChange={filter=>remember({filter})} placeholder="搜索标题、正文或来源"/><span className="muted">找到 {records.length} 条记录</span>{query&&<Button onClick={()=>remember({filter:''})}>清除搜索</Button>}</div>
  {!game.settled&&<p className="note">本街区尚未完成首次月度结算；结算结果为「待结算」。推进首月后才会生成真实演示记录。</p>}
  {records.length?<div className="archive-browser"><div className="card archive-record-list" role="list" aria-label="档案记录">{records.map(r=><button role="listitem" className={`archive-record ${selected?.id===r.id?'selected':''}`} key={r.id} onClick={()=>remember({selected:r.id})}><div><Tag tone={r.category==='achievement'?'gold':''}>{categoryNames[r.category]}</Tag><span className="muted">第 {r.month} 月</span></div><h4>{r.title}</h4><p>{r.text}</p>{r.tileId&&<small>{r.tileId}</small>}</button>)}</div><article className="card archive-detail" key={selected.id}><div className="archive-detail-meta"><Tag tone={selected.category==='achievement'?'gold':''}>{categoryNames[selected.category]}</Tag><span className="muted">第 {selected.month} 月 · {selected.id}</span></div><h2>{selected.title}</h2><div className="archive-detail-body">{selected.text.split('\n').map((p,i)=><p key={i}>{p||'　'}</p>)}</div>{selected.tileId&&<div className="archive-record-location"><SectionTitle>关联地块</SectionTitle><p>{selected.tileId}</p><div className="actions"><Button icon="pin" onClick={()=>locate(selected.tileId!)}>在地图定位</Button><Button onClick={()=>inspect(selected.tileId!)}>查阅地块详情</Button></div></div>}<SourceLine>来源：{selected.category==='rule'?'本地演示规则与来源说明':selected.category==='settlement'?'本地演示月度结算':selected.category==='achievement'?'已确认的城市成长与奖励操作':'本地演示中已执行的经营操作'}。记录为发生时的快照，当前城市变化不会重写其正文。</SourceLine></article></div>:<section className="card"><Empty title={query?'未找到匹配记录':'此分类暂无记录'}>{query?'尝试更短的关键词，或切换其他分类。':'执行相应经营操作后，记录会自动出现在这里。'}</Empty></section>}
 </div>
}
