import { useLab } from '../context';
import { serviceNames, kindNames, fmt, effectiveBuilding, tileFacts } from '../model';
import type { Service } from '../types';
import { Button, Empty, Fold, Icon, Progress, SectionTitle, SourceLine, Stat, Tag } from '../ui';
import './economy.css';

const services: Service[] = ['medical', 'education', 'entertainment', 'commercial'];
export default function Residents() {
 const { game, stats, view, remember, act, inspect, locate, go } = useLab();
 const homes = game.tiles.filter(t => t.building && (t.building.population > 0 || (effectiveBuilding(game,t)?.housing || 0) > 0));
 const filter = services.includes(view.filter as Service) ? view.filter as Service : '';
 const shown = homes.filter(t => !filter || tileFacts(game,t.id).services[filter] < 1);
 const selected = shown.find(t => t.id === view.selected) || shown[0];
 const building = selected ? effectiveBuilding(game, selected) : undefined;
 const localServices = selected ? tileFacts(game,selected.id).services : null;
 const pending = game.projects.filter(p => !p.paused).length;
 const selectService = (next:Service|'') => {
  const draft = {...view.draft,[`resident:${filter||'all'}`]:selected?.id||view.selected};
  const available = homes.filter(t=>!next||tileFacts(game,t.id).services[next]<1);
  const saved = draft[`resident:${next||'all'}`] || view.selected;
  remember({filter:next,selected:available.some(t=>t.id===saved)?saved:available[0]?.id||'',draft});
 };
 const selectResident = (id:string) => remember({selected:id,draft:{...view.draft,[`resident:${filter||'all'}`]:id}});
 const readReason = game.readonly ? '历史街区只读，不能调整移民或物资供应。' : '';
 return <div className="panel-content residents-panel">
  <div className="page-intro"><div><span className="eyebrow">城市运行 · 人口与服务</span><h2>先看居民过得怎样</h2><p className="muted">从受影响住区追溯服务覆盖、供给建筑与下一步行动。</p></div><Tag tone={stats.foodRatio < 1 ? 'bad' : 'good'}>{stats.foodRatio < 1 ? '食物供应不足' : '食物供应正常'}</Tag></div>
  {!game.settled&&<p className="note">待首次结算：服务满足与经营状态为当前快照的预估，实际结算回执尚未生成。</p>}
  <div className="stats-row population-stats"><Stat label="常住人口" value={fmt(stats.population)} sub={`住房 ${fmt(stats.housing)} · ${stats.population>stats.housing ? `超出容量 ${fmt(stats.population-stats.housing)}` : `空余 ${fmt(stats.housing-stats.population)}`}`} tone={stats.population>stats.housing?'gold':''}/><Stat label="就业人数" value={`${fmt(stats.employed)} / ${fmt(stats.population)}`} sub={`岗位 ${fmt(stats.jobs)} · 已填补 ${fmt(stats.filledJobs)}`}/><Stat label="平均健康" value={`${fmt(stats.health)}%`} sub="当前居民状态"/><Stat label="平均幸福" value={`${fmt(stats.happiness)}%`} sub="当前居民状态"/></div>
  <div className="service-strip" aria-label="按服务筛选住区">{services.map(s => { const affected = homes.filter(t=>tileFacts(game,t.id).services[s]<1).length;return <button key={s} className={`service-pill ${filter===s?'active':''}`} onClick={()=>selectService(filter===s?'':s)}><Icon name={s}/><span><strong>{serviceNames[s]}</strong><small>{game.settled?'全城满足':'预计全城满足'} {fmt(stats.services[s]*100)}% · {affected ? `${affected} 处满足不足` : '全部满足'}</small></span><span className={stats.services[s]<1?'gold':'good'}>{fmt(game.tiles.reduce((n,t)=>n+(effectiveBuilding(game,t)?.services[s]||0),0))}<small>有效容量</small></span></button>})}</div>
  <div className="toolbar resident-toolbar"><div className="actions"><Button variant={!filter?'active':''} onClick={()=>selectService('')}>全部住区 <span className="count">{homes.length}</span></Button>{filter&&<Tag>{serviceNames[filter]}满足不足</Tag>}</div><label className="field-inline">排序<select aria-label="住区排序" value={view.sort||'name'} onChange={e=>remember({sort:e.target.value})}><option value="name">建筑名称</option><option value="population">人口较多优先</option><option value="health">健康较低优先</option></select></label></div>
  <div className="resident-diagnostic">
   <div className="resident-index">{shown.length ? [...shown].sort((a,b)=>view.sort==='population' ? b.building!.population-a.building!.population : view.sort==='health' ? a.building!.health-b.building!.health : a.building!.name.localeCompare(b.building!.name,'zh')).map(t=><button className={`resident-row ${selected?.id===t.id?'selected':''}`} onClick={()=>selectResident(t.id)} key={t.id}><span className="resident-mark"><Icon name="home" size={21}/></span><span className="resident-row-main"><strong>{t.building!.name}</strong><small>{kindNames[t.building!.kind]} · {t.building!.population} 人 · {t.id}</small><span className="mini-services">{services.map(s=><span key={s} title={`${serviceNames[s]}可达度 ${fmt(t.reach[s]*100)}%`} className={t.reach[s]<1?'bad':'muted'}><Icon name={s} size={13}/>{fmt(t.reach[s]*100)}%</span>)}</span></span><Icon name="arrow" size={16}/></button>):<Empty title="没有这类服务缺口">所有住区的该项服务均满足需求。</Empty>}</div>
   <div className="resident-detail">{building&&selected ? <>
    <div className="detail-heading"><div><span className="eyebrow">住区详情 · {selected.id}</span><h3>{building.name}</h3><p>{kindNames[building.kind]} · {building.specialization} · {building.operator}</p></div><Button icon="pin" onClick={()=>locate(selected.id)}>地图定位</Button></div>
    <div className="household-metrics"><div><small>入住 / 容量</small><strong>{building.population} <em>/ {building.housing}</em></strong><Progress value={building.population} max={building.housing} label="住房入住率"/></div><div><small>就业 / 居民</small><strong>{building.employed} <em>/ {building.population}</em></strong><Progress value={building.employed} max={building.population} label="居民就业率"/></div><div><small>本地岗位</small><strong>{building.filledJobs} <em>/ {building.jobs}</em></strong><Progress value={building.filledJobs} max={building.jobs} label="岗位填补率"/></div></div>
    {building.population>building.housing&&<p className="note gold">现有 {fmt(building.population)} 位居民仍在住区；当前住房容量 {fmt(building.housing)}，超出容量 {fmt(building.population-building.housing)} 人。</p>}
    <SectionTitle aside={<span className="muted">能力与可达度不等于实际满足率</span>}>服务诊断</SectionTitle>
    <table className="data-table service-table"><thead><tr><th>服务</th><th>本地有效容量</th><th>{game.settled?'住区实际满足':'住区预计满足'}</th><th>本地可达度</th><th>当前判断</th></tr></thead><tbody>{services.map(s=><tr key={s}><td><span className="icon-label"><Icon name={s} size={16}/>{serviceNames[s]}</span></td><td>{fmt(building.services[s])}</td><td className={(localServices?.[s]||0)<1?'gold':'good'}>{fmt((localServices?.[s]||0)*100)}%</td><td className={selected.reach[s]<1?'bad':'good'}>{fmt(selected.reach[s]*100)}%</td><td>{selected.reach[s]<1?'可达不足':'可达完整'}</td></tr>)}</tbody></table>
    <p className="note">健康 {fmt(building.health)}% · 幸福 {fmt(building.happiness)}% 是当前状态。全城供给按有效容量与人口计算；住区实际满足取全城供给与本地可达度中的较小值。有效容量已计运营、升级与 Bonus 调整；待建项目完成后才会贡献供给。</p>
    <Fold title="查看服务供应来源" open>{services.filter(s=>!filter||s===filter).map(s=><div className="provider-group" key={s}><strong className="icon-label"><Icon name={s} size={15}/>{serviceNames[s]}</strong>{game.tiles.filter(t=>t.building&&(effectiveBuilding(game,t)?.services[s]||0)>0).map(t=><div className="provider-row" key={t.id}><button className="text-link" onClick={()=>inspect(t.id)}>{t.building!.name}</button><span>有效容量 {fmt(effectiveBuilding(game,t)!.services[s])}</span><Button icon="pin" aria-label={`在地图定位${t.building!.name}`} onClick={()=>locate(t.id)}>定位</Button></div>)}</div>)}</Fold>
    <SourceLine>当前月份 {game.month} · 同步于当前城市快照 · {pending} 项施工待完成</SourceLine>
    <div className="actions"><Button onClick={()=>inspect(selected.id)}>完整建筑档案</Button><Button onClick={()=>go('construction')}>查看施工与扩容</Button></div>
   </>:<Empty title="暂无住区">住宅建成后将在此显示人口与服务状态。</Empty>}</div>
  </div>
  <div className="panel-actions resident-controls"><div><strong>城市供应设置</strong><span className="muted">{readReason||'变更在演示城市中立即记录；实际月流将在下次结算反映。'}</span></div><Button disabled={game.readonly} icon={game.migrationPaused?'play':'pause'} onClick={()=>act({type:'toggle-migration'})}>{game.migrationPaused?'恢复移民':'暂停移民'}</Button><Button disabled={game.readonly||game.goodsActive} onClick={()=>act({type:'enable-goods'})}>{game.goodsActive?'生活物资已启用':'启用生活物资'}</Button></div>
 </div>;
}
