import {WildResearch} from '../game/WildTechnology';
import {conditionDestination} from './destinations';
import { Fragment } from 'react';
import { useLab } from '../context';
import { bonusCatalog, bonusComparison, conditions, conditionGap, fmt, signed, kindNames, resourceNames, themesCatalog } from '../model';
import type { Condition } from '../types';
import { Button, Tag, Progress, InlineConfirm, Icon } from '../ui';
import './development.css';
import {Meter} from './Graphics';

const stageNames = ['开拓立足', '协作发展', '街区繁荣'];
const slotNames = { production:'生产槽', operations:'运营槽', logistics:'后勤槽' };
const cv = (c:Condition,n:number) => `${fmt(c.unit==='%'?n*100:n)}${c.unit}`;
const howTo:Record<string,string> = {
 population:'在居民面板开放移民；住房不足时建设农居或安装共享起居。',
 food:'恢复农居运营、建设或升级农居；食物保障按月产出计算，购买库存不会提高此比例。',
 medical:'恢复邻里中心运营、建设诊疗专业化中心，或安装邻里诊疗；覆盖同时受当地可达性限制。',
 education:'恢复邻里中心运营，或将中心升级为社区学堂；服务容量与当地可达性共同决定覆盖。',
 health:'在经济政策中实施公共关怀可增加健康；先保障当月口粮产出。',
 happiness:'公共关怀可增加幸福；启用制成品消费时保持制成品库存。',
 hold:'其余目标全部达标后，使用右下时间控制推进月份。任何月份未达标，连续计时会归零。',
 'theme-housing':'建设农居或安装共享起居，增加总住房相对现有人口的余量。',
 'theme-material':'恢复作坊运营、升级材料加工，或在经济政策中实施生产协作；此目标按材料月净流计算。',
 'theme-pollution':'为作坊安装循环回收，或暂停高污染建筑；全城平均污染以全部城市地块为分母。',
 'theme-entertainment':'恢复邻里中心运营或增建中心；娱乐覆盖还受到各居住地可达性的限制。',
 'theme-currency':'恢复建筑运营、检查政策的每月费用与收支来源；出售库存不会增加持续的通货月净流。',
};

export function TasksCompact(){
 const { game, view, remember, act, go, advanced, openModal, trackTask } = useLab();
 const rows=conditions(game), met=rows.filter(c=>c.met).length;
 const handleCondition=(c:Condition)=>{trackTask();go(...conditionDestination(c))};
 const contracts=game.contracts.filter(c=>c.status==='active'||c.status==='available').sort((a,b)=>Number(b.status==='active')-Number(a.status==='active'));
 const techNav=game.wild&&<nav className="wild-development-tabs"><Button variant={view.tab!=='science'?'primary':''} onClick={()=>remember({tab:'current'})}>当前事项</Button><Button variant={view.tab==='science'?'primary':''} onClick={()=>remember({tab:'science'})}>科技</Button></nav>;
 if(game.wild&&view.tab==='science')return <div className="compact-development compact-tasks">{techNav}<WildResearch/></div>;
 return <div className="compact-development compact-tasks">{techNav}
  <div className="cd-heading"><div><h2>任务与委托</h2><span className="cd-muted">第 {game.month} 月 · DT {game.dt} · {game.theme||'未选主题'}</span></div><Button onClick={()=>advanced('tasks','growth')}>成长详表 ↗</Button></div>
  
  {game.rewards.length>0&&<section className="cd-section"><h3>待领取奖励 <Tag tone="gold">{game.rewards.length}</Tag></h3>{game.rewards.map(r=><div className="cd-reward" key={r.id}><span><strong>{r.source}</strong><small className="cd-muted">{r.choices.length} 选 1 · 获得 Bonus 库存副本</small></span><Button variant="primary" disabled={game.readonly} onClick={()=>openModal({kind:'reward',rewardId:r.id})}>选择奖励</Button></div>)}</section>}
  {game.stage<=3?<section className="cd-section"><div className="cd-heading"><h3>阶段 {game.stage} · {stageNames[game.stage-1]}</h3><Tag tone={met===rows.length?'good':''}>{met}/{rows.length} 达标</Tag></div><Progress value={met} max={rows.length} label="当前阶段达标进度"/>
   <div className="viz-objectives">{rows.map(c=><div className="viz-objective" key={c.id}><div><Meter label={`${c.met?'✓ ':''}${c.label}${c.id==='theme-pollution'?' · 上限':''}`} value={c.unit==='%'?c.current*100:c.current} max={c.unit==='%'?c.target*100:c.target} unit={c.unit} overflowWarning={c.id==='theme-pollution'} tone={c.met?'':'gold'}/><details><summary>{c.met?'已达标':`还差 ${cv(c,conditionGap(c))}`} · 如何达成</summary><p>{c.help}</p><p>{howTo[c.id]||'打开对应管理面板，调整相关建设、政策或运营；目标按当前城市汇总计算。'}</p></details></div>{c.id==='hold'?<span className="cd-muted">随月结算</span>:<Button onClick={()=>handleCondition(c)}>处理</Button>}</div>)}</div>
   <div className="cd-stage-action"><span className="cd-muted">完成奖励：1 次 Bonus 选择资格<br/>保持 {game.holdMonths}/{rows.find(c=>c.id==='hold')?.target||1} 月 · 随月结算</span><div className="actions"><Button onClick={trackTask}>追踪</Button><Button variant="primary" disabled={game.readonly||met!==rows.length} onClick={()=>act({type:'complete-stage'})}>完成阶段</Button></div></div>
  </section>:<section className="cd-section"><h3>三个成长阶段已完成</h3><p className="cd-muted">可继续经营与完成委托。DT 归零后，为街区命名并选择纪念地标。</p></section>}
  {game.dt<=0&&<div className="cd-ending"><span><strong>{game.named?game.name:'终局命名'} · {game.capstone?'地标已选定':'待选纪念地标'}</strong><small className="cd-muted">{game.completedStages.length>=3?'已完成阶段，可保存终局记录。':'完成三个阶段后开放终局落款。'}</small></span><Button onClick={()=>advanced('tasks','current')}>命名与地标 ↗</Button></div>}
  <section className="cd-section"><div className="cd-heading"><h3>主题委托</h3><select aria-label="街区主题" value={game.theme||''} disabled={game.readonly} onChange={e=>act({type:'choose-theme',theme:e.target.value})}><option value="" disabled>选择主题</option>{themesCatalog.map(t=><option key={t.name}>{t.name}</option>)}</select></div><p className="cd-muted cd-tight">接受时不扣库存；交付扣物资，奖励通货与 1 次 Bonus 选择。</p>
   {contracts.length?<table className="cd-table cd-contracts"><thead><tr><th>委托 / 截止</th><th>库存 / 交付</th><th>奖励</th><th>操作</th></tr></thead><tbody>{contracts.map(c=>{const enough=game.resources[c.resource]>=c.quantity;return <tr key={c.id}><td><details><summary>{c.title}</summary><p>{c.description}</p></details><small className="cd-muted">{c.status==='active'?'进行中':'可接取'} · 剩 {Math.max(0,c.deadline-game.month)} 月</small></td><td className={enough?'':'gold'}>{resourceNames[c.resource]}<br/>{fmt(game.resources[c.resource])} / {fmt(c.quantity)}</td><td>+{fmt(c.reward)} 通货<small className="cd-muted">+ Bonus 三选一</small></td><td><Button variant={c.status==='active'?'primary':''} disabled={game.readonly||(c.status==='active'&&!enough)} onClick={()=>act({type:c.status==='active'?'deliver-contract':'accept-contract',id:c.id})}>{c.status==='active'?'交付':'接受'}</Button></td></tr>})}</tbody></table>:<p className="cd-muted">暂无可接受或进行中的委托。完成与过期记录可在「成长与记录」查看。</p>}
  </section><p className="cd-source">独立演示数据 · 目标读取当前城市汇总，连续保持随月结算。</p>
 </div>;
}

export function BonusCompact(){
 const { game, view, remember, selectedTile, act, go, advanced, inspect, locate }=useLab();
 const buildings=game.tiles.filter(t=>t.building);
 const deployed=(id:string)=>buildings.reduce((n,t)=>n+t.building!.slots.filter(s=>s.bonusId===id).length,0);
 const filter=['available','deployed','all'].includes(view.filter)?view.filter:'available';
 const items=bonusCatalog.filter(b=>filter==='available'?(game.bonuses[b.id]||0)>0:filter==='deployed'?deployed(b.id)>0:true);
 const selected=items.find(b=>b.id===view.selected)||items[0];
 const eligible=selected?buildings.filter(t=>selected.target.includes(t.building!.kind)&&t.building!.slots.some(s=>s.kind===selected.kind)):[];
 const deployedTargets=filter==='deployed'?eligible.filter(t=>t.building!.slots.some(s=>s.bonusId===selected?.id)):eligible;
 const target=deployedTargets.find(t=>t.id===(view.draft?.targetId||selectedTile))||deployedTargets[0];
 const slots=selected?target?.building!.slots.filter(s=>s.kind===selected.kind)||[]:[];
 const slot=slots.find(s=>s.id===view.draft?.slotId)||(filter==='deployed'?slots.find(s=>s.bonusId===selected?.id):slots[0])||slots[0];
 const old=bonusCatalog.find(b=>b.id===slot?.bonusId);
 const changes=selected&&target&&slot?bonusComparison(game,target.id,slot.id,selected.id).filter(r=>Math.abs(r.after-r.before)>.001):[];
 const draft=(patch:Record<string,string>)=>remember({draft:{...view.draft,...patch}});
 const install=()=>{if(selected&&target&&slot&&act({type:'install-bonus',tileId:target.id,slotId:slot.id,bonusId:selected.id}))remember({filter:'all',selected:selected.id,draft:{...view.draft,targetId:target.id,slotId:slot.id}})};
 if(game.wild)return <div className="compact-development"><h2>组织 · 联系 · 融合</h2><p>专用槽位在建筑星图中建设。Wild Bonus 的效果与装配规则正在确认，旧演示槽位不参与新的构筑。</p><Button onClick={()=>advanced('bonus')}>查看原演示库存记录 ↗</Button></div>;
 return <div className="compact-development compact-bonus"><div className="cd-heading"><div><h2>Bonus 安装</h2><span className="cd-muted">库存 {Object.values(game.bonuses).reduce((a,b)=>a+b,0)} 份 · 已安装 {buildings.reduce((n,t)=>n+t.building!.slots.filter(s=>s.bonusId).length,0)} 份</span></div><Button onClick={()=>advanced('bonus','',selected?.id)}>蓝图研究与来源 ↗</Button></div>
  
  <div className="cd-heading cd-filter"><div className="actions"><Button aria-pressed={filter==='available'} variant={filter==='available'?'primary':''} onClick={()=>remember({filter:'available',selected:''})}>库存副本</Button><Button aria-pressed={filter==='deployed'} variant={filter==='deployed'?'primary':''} onClick={()=>remember({filter:'deployed',selected:''})}>已安装</Button><Button aria-pressed={filter==='all'} variant={filter==='all'?'primary':''} onClick={()=>remember({filter:'all',selected:''})}>全部方案</Button></div><Button onClick={()=>go('tasks','current')}>待领奖励 {game.rewards.length}</Button></div>
  {items.length?<div className="viz-bonus-grid">{items.map(b=><button className={`viz-bonus ${selected?.id===b.id?'selected':''}`} key={b.id} aria-pressed={selected?.id===b.id} onClick={()=>remember({selected:b.id,draft:{...view.draft,slotId:''}})}><div className="viz-bonus-icon"><Icon name={b.kind==='production'?'build':b.kind==='operations'?'people':'layers'} size={25}/><span>Lv.{game.bonusLevels[b.id]||'—'}</span></div><div><strong>{b.name}</strong><small>{b.effect}</small><span className="viz-bonus-count">库存 <b>{game.bonuses[b.id]||0}</b> · 已装 {deployed(b.id)} · {slotNames[b.kind]}</span></div></button>)}</div>:<div className="cd-empty"><strong>{filter==='available'?'暂无库存副本':'暂无已安装副本'}</strong><p className="cd-muted">{filter==='available'?'完成任务并领取奖励后，可在这里直接安装。':'从库存中选择副本，再指定建筑与槽位。'}</p></div>}
  {selected&&<section className="cd-section"><div className="cd-heading"><h3>{selected.name}</h3><span className="cd-muted">适用：{selected.target.map(k=>kindNames[k]).join('、')}</span></div><p className="cd-muted cd-tight">{selected.description} · 当前蓝图 Lv.{game.bonusLevels[selected.id]||1}</p>
   <div className="cd-target-fields"><label>目标建筑<select aria-label="Bonus 目标建筑" value={target?.id||''} onChange={e=>draft({targetId:e.target.value,slotId:''})}>{!deployedTargets.length&&<option value="">无适配建筑</option>}{deployedTargets.map(t=><option key={t.id} value={t.id}>{t.building!.name} T{t.building!.tier} · {t.id}{t.building!.running?'':' · 停运'}</option>)}</select></label><label>目标槽位<select aria-label="Bonus 目标槽位" value={slot?.id||''} onChange={e=>draft({slotId:e.target.value})}>{!slots.length&&<option value="">无适配槽位</option>}{slots.map(s=><option key={s.id} value={s.id}>{slotNames[s.kind]} · {s.bonusId?bonusCatalog.find(b=>b.id===s.bonusId)?.name||s.bonusId:'空槽'}</option>)}</select></label></div>
   {target&&slot?<Fragment><div className="cd-heading cd-target-caption"><span>{filter==='deployed'?`${target.building!.name} · 已安装 ${selected.name}`:old?`${old.name} → ${selected.name}`:`空槽 → ${selected.name}`}</span><div className="actions"><Button onClick={()=>locate(target.id)}>定位</Button><Button onClick={()=>inspect(target.id)}>建筑</Button></div></div>
    {changes.length?<table className="cd-table"><thead><tr><th>城市变化项</th><th>当前</th><th>安装后</th><th>变化</th></tr></thead><tbody>{changes.map(r=><tr key={r.label}><td>{r.label}<small className="cd-muted">{r.unit}</small></td><td>{fmt(r.before)}</td><td>{fmt(r.after)}</td><td className={(r.after-r.before)*(r.lowerBetter?-1:1)>0?'good':'bad'}>{signed(r.after-r.before)}</td></tr>)}</tbody></table>:<p className="cd-note">{slot.bonusId===selected.id?'该槽已装同一蓝图，无需重复安装。':'汇总数值无变化：停运与施工等现有条件已计入预览。'}</p>}
    <div className="cd-install"><span>消耗 <strong>1 份 {selected.name}</strong><small className="cd-muted">库存 {game.bonuses[selected.id]||0} 份 · 不消耗通货或材料</small>{old&&<small className="gold">原「{old.name}」副本销毁，不返还。</small>}</span>{old?<InlineConfirm key={`${selected.id}-${target.id}-${slot.id}`} label="替换安装" disabled={game.readonly||!(game.bonuses[selected.id]>0)||slot.bonusId===selected.id} onConfirm={install}><p>消耗 1 份「{selected.name}」，销毁原「{old.name}」副本。</p></InlineConfirm>:<Button variant="primary" disabled={game.readonly||!(game.bonuses[selected.id]>0)} onClick={install}>安装副本</Button>}</div>
    {!(game.bonuses[selected.id]>0)&&<p className="cd-muted">无库存副本；已安装记录仍可查阅。领取同蓝图副本后可继续安装。</p>}
   </Fragment>:<p className="cd-note">暂无适配建筑。建设{selected.target.map(k=>kindNames[k]).join('或')}后即可安装。</p>}
  </section>}<p className="cd-source">安装比较读取同一城市快照与当前蓝图等级，不推进月份。</p>
 </div>;
}
