import { useLab } from '../context';
import { fmt, kindNames } from '../model';
import { Button, Empty, Icon, Progress, SectionTitle, SourceLine, Stat, Tag } from '../ui';
import './economy.css';

export default function Construction() {
 const { game, stats, act, locate, inspect, openModal, view, remember } = useLab();
 const queue = [...game.projects].sort((a,b)=>a.priority-b.priority);
 const materialNeed = queue.reduce((sum,p)=>sum+p.totalMaterial-p.usedMaterial,0);
 const freePower = stats.powerSupply-stats.powerDemand;
 const crewCost = 60 * 2 ** game.crews.length;
 const active = queue.filter(p=>!p.paused).length;
 return <div className="panel-content construction-panel">
  <div className="page-intro"><div><span className="eyebrow">城市运行 · 建设施工</span><h2>把有限施工能力用在最重要的地方</h2><p className="muted">项目按优先级分配施工能力；材料与供电状态共同决定扩建节奏。</p></div><Tag>{active} 项进行中</Tag></div>
  <div className="construction-capacity"><div className="cp-primary"><Icon name="build" size={28}/><div><small>每月施工能力</small><strong>{fmt(stats.cpTotal)} <span>CP / 月</span></strong></div><Progress value={stats.cpUsed} max={stats.cpTotal} label="施工能力使用量"/><p>预计分配 {fmt(stats.cpUsed)} · 预留 {fmt(stats.cpReserved)} CP</p></div><div className="cp-breakdown"><div><span>城市基础</span><b>{fmt(stats.cpBase)} CP / 月</b></div><div><span>建筑贡献</span><b>{fmt(stats.cpBuilding)} CP / 月</b></div><div><span>临时工程队</span><b>{fmt(stats.cpCrews)} CP / 月</b></div><p className="muted">来源为原始能力；总量已计供电约束，预计分配已计材料限制。</p></div></div>
  <div className="stats-row construction-limits"><Stat label="材料库存" value={fmt(game.resources.material)} sub={`队列剩余需求 ${fmt(materialNeed)}`} tone={game.resources.material<materialNeed?'gold':''}/><Stat label="电力余量" value={fmt(freePower)} sub={`供给 ${fmt(stats.powerSupply)} / 需求 ${fmt(stats.powerDemand)}`} tone={freePower<0?'bad':'good'}/><Stat label="全部剩余工程" value={`${fmt(queue.reduce((s,p)=>s+p.totalCP-p.doneCP,0))} CP`} sub={`${queue.length} 项待完工项目`}/></div>
  {game.readonly&&<p className="note">历史街区只读，施工队列与工程队信息保留查阅，不能调整或招募。</p>}
  <SectionTitle aside={<div className="actions"><label className="field-inline">显示<select aria-label="施工队列筛选" value={view.filter||'all'} onChange={e=>remember({filter:e.target.value})}><option value="all">全部项目</option><option value="active">进行中</option><option value="paused">已暂停</option></select></label></div>}>施工队列</SectionTitle>
  <div className="project-queue">{queue.filter(p=>view.filter==='active'?!p.paused:view.filter==='paused'?p.paused:true).map((p,i)=>{const tile=game.tiles.find(t=>t.id===p.tileId);const progress=p.totalCP?p.doneCP/p.totalCP:0;return <article className={`project-item ${p.paused?'is-paused':''}`} key={p.id}>
   <div className="project-rank">{String(i+1).padStart(2,'0')}</div><div className="project-main"><div className="project-heading"><div><span className="eyebrow">{p.type==='new'?'新建':'升级'} · {p.tileId}</span><h3>{tile?.building?.name || kindNames[p.kind]} <span className="muted">→ {p.targetTier} 级</span></h3></div><Tag tone={p.paused?'':'good'}>{p.paused?'已暂停':'等待每月结算'}</Tag></div><p className="muted">{p.specialization} · 运营方 {p.operator}</p><div className="project-progress"><Progress value={p.doneCP} max={p.totalCP} label={`${kindNames[p.kind]}施工进度`}/><strong>{fmt(progress*100)}%</strong></div><div className="project-materials"><span>工程 <b>{fmt(p.doneCP)} / {fmt(p.totalCP)} CP</b></span><span>剩余 <b>{fmt(p.totalCP-p.doneCP)} CP</b></span><span>材料 <b>{fmt(p.usedMaterial)} / {fmt(p.totalMaterial)}</b></span><span className={game.resources.material<p.totalMaterial-p.usedMaterial?'gold':''}>还需 {fmt(p.totalMaterial-p.usedMaterial)} 材料</span></div><div className="project-actions"><label className="field-inline">优先级<select aria-label={`调整${kindNames[p.kind]}施工优先级`} value={p.priority} disabled={game.readonly} onChange={e=>act({type:'project-priority',id:p.id,priority:Number(e.target.value)})}><option value={1}>高 · 1</option><option value={2}>中 · 2</option><option value={3}>低 · 3</option>{![1,2,3].includes(p.priority)&&<option value={p.priority}>{p.priority}</option>}</select></label><Button disabled={game.readonly} icon={p.paused?'play':'pause'} onClick={()=>act({type:'project-toggle',id:p.id})}>{p.paused?'继续施工':'暂停施工'}</Button><span className="spacer"/><Button icon="eye" onClick={()=>inspect(p.tileId)}>地块详情</Button><Button icon="pin" onClick={()=>locate(p.tileId)}>地图定位</Button></div></div>
  </article>})}{!queue.length&&<Empty title="施工队列为空">在地图选择一块空地新建建筑，或从已有建筑发起升级。</Empty>}</div>
  <SectionTitle aside={<Button variant="primary" disabled={game.readonly||game.resources.currency<crewCost} icon="plus" onClick={()=>openModal({kind:'crew'})}>招募工程队 · {fmt(crewCost)} 通货</Button>}>临时工程队</SectionTitle>
  <div className="crew-list">{game.crews.length ? game.crews.map((c,i)=><div className="crew-row" key={c.id}><div className="crew-icon"><Icon name="people"/></div><div><strong>工程队 {i+1}</strong><small>{c.id}</small></div><strong>{fmt(c.cp)} <span className="muted">CP / 月</span></strong><span>剩余 {c.remainingMonths} 个月</span></div>):<div className="crew-placeholder"><Icon name="people"/><span>暂无临时工程队。可花费通货补充每月施工能力。</span></div>}</div>
  <p className="note">下一支工程队招募费 {fmt(crewCost)} 通货（60 × 2<sup>{game.crews.length}</sup>）。{game.resources.currency<crewCost?'当前通货不足。':'招募前将展示费用与持续时间。'}</p>
  <SourceLine>施工与工程队来源均为本演示当前快照 · 材料按施工进度消耗 · 每月结算推进</SourceLine>
 </div>;
}
