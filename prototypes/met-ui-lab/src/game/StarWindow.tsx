import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { Button, Icon, Progress, Tag } from '../ui';
import './star-window.css';

export interface StarNodeView {
 id: string;
 label: string;
 tier: number;
 kind: 'stat' | 'slot' | 'advance';
 description: string;
 requires: string[];
 visualParents?: string[];
 routes?: { from: string; via?: string[] }[];
 connections?: { from: string; conversion?: string }[];
 layout?: { column: number; row: number };
 status: 'available' | 'active' | 'locked' | 'building';
 points: number;
 constructionCP: number | null;
 resourceLines: string[];
 blockedReason?: string;
 changes: string[];
 slotType?: 'organization' | 'link' | 'fusion';
}

export interface StarWindowProps {
 title: string;
 subtitle: string;
 entryTier: number;
 currentTier: number;
 capacity: number;
 used: number;
 nodes: StarNodeView[];
 pendingText?: string;
 onClose: () => void;
 onBuild: (id: string) => void;
 onCancelNode: (id: string) => void;
 cancellation?: (id: string) => { labels: string[]; refundPoints: number; bonusNames: string[] };
}

const statusLabels = { available: '可建造', active: '已启用', locked: '未解锁', building: '建造中' };
const kindLabels = { stat: '属性节点', slot: '槽位节点', advance: 'Tier 进阶' };
const slotLabels = { organization: '组织', link: '联系', fusion: '融合' };

export function StarWindow(props: StarWindowProps) {
 const { title, subtitle, entryTier, currentTier, capacity, used, nodes, pendingText, onClose, onBuild, onCancelNode, cancellation } = props;
 const tiers = [...new Set(nodes.map(node => node.tier))].sort((a, b) => a - b);
 const [tier, setTier] = useState(currentTier);
 const shownTier = tiers.includes(tier) ? tier : tiers.find(value => value >= entryTier);
 const visibleNodes = nodes.filter(node => node.tier === shownTier);
 const [selectedId, setSelectedId] = useState<string | null>(null);
 const selected = visibleNodes.find(node => node.id === selectedId) ?? visibleNodes[0];
 const [cancelId, setCancelId] = useState<string | null>(null);
 const cancelling = !!selected && cancelId === selected.id;
 const plan = cancelling && cancellation ? cancellation(selected.id) : null;
 const dialogRef = useRef<HTMLDivElement>(null);
 const graphRef = useRef<HTMLDivElement>(null);
 const [paths, setPaths] = useState<{ id: string; d: string; conversion?: string; conversionActive: boolean }[]>([]);
 const titleId = useId();
 const graphId = useId();
 const belowEntry = !!selected && selected.tier < entryTier;
 const blocked = selected && selected.status !== 'active' ? belowEntry ? `此建筑从 T${entryTier} 起始，不能补选此前节点。` : selected.blockedReason || (selected.status === 'locked' ? '该节点尚未解锁。' : selected.constructionCP === null ? '建造 CP 尚未配置。' : undefined) : undefined;
 const canBuild = !!selected && selected.status === 'available' && !blocked;

 useEffect(() => {
  const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  const dialog = dialogRef.current;
  dialog?.focus();
  const containFocus = (event: FocusEvent) => {
   if (event.target instanceof Node && !dialog?.contains(event.target)) dialog?.focus();
  };
  document.addEventListener('focusin', containFocus);
  return () => {
   document.removeEventListener('focusin', containFocus);
   if (previous?.isConnected) previous.focus();
  };
 }, []);

 useLayoutEffect(() => {
  const graph = graphRef.current;
  if (!graph) return;
  const draw = () => {
   const bounds = graph.getBoundingClientRect();
   const scale = graph.offsetWidth ? bounds.width / graph.offsetWidth : 1;
   const cards = new Map([...graph.querySelectorAll<HTMLElement>('[data-star-node]')].map(card => [card.dataset.starNode, { card: card.getBoundingClientRect(), orbit: (card.querySelector('.star-node-orbit') ?? card).getBoundingClientRect() }]));
   setPaths(nodes.filter(node => node.tier === shownTier).flatMap(node => (node.connections ?? (node.visualParents ?? node.requires).map(from => ({ from }))).flatMap((connection: { from: string; conversion?: string }) => {
    const source = cards.get(connection.from);
    const target = cards.get(node.id);
    if (!source || !target) return [];
    const x1 = (source.orbit.left + source.orbit.width / 2 - bounds.left) / scale;
    const y1 = (source.orbit.top + source.orbit.height / 2 - bounds.top) / scale;
    const x2 = (target.orbit.left + target.orbit.width / 2 - bounds.left) / scale;
    const y2 = (target.orbit.top - bounds.top) / scale;
    const sameColumn = Math.abs(x2 - x1) < 5;
    const direction = sameColumn ? (x1 < graph.offsetWidth / 2 ? -1 : 1) : Math.sign(x2 - x1);
    // Leave from the orbit's side and bend in the gutter, clear of its label.
    const laneX = sameColumn ? ((direction < 0 ? source.card.left : source.card.right) - bounds.left) / scale + direction * 14 : (x1 + x2) / 2;
    const startX = x1 + direction * source.orbit.width / scale / 2;
    const bendY = y2 - 16;
    return [{ id: `${connection.from}:${node.id}`, d: `M ${startX} ${y1} L ${laneX} ${y1} L ${laneX} ${bendY} L ${x2} ${bendY} L ${x2} ${y2}`, conversion: connection.conversion, conversionActive: !!connection.conversion && nodes.some(item => item.id === connection.conversion && item.status === 'active') }];
   })));

  };
  draw();
  const observer = new ResizeObserver(draw);
  observer.observe(graph);
  return () => observer.disconnect();
 }, [nodes, shownTier]);

 return <div className="star-window-scrim" onClick={event => event.stopPropagation()} onPointerDown={event => event.stopPropagation()}>
  <div className="star-window" ref={dialogRef} role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1} onKeyDown={event => {
   // Escape is owned by the parent window stack.
   if (event.key === 'Escape') { if(cancelId){event.preventDefault();event.stopPropagation();setCancelId(null)} return; }
   event.stopPropagation();
   if (event.key !== 'Tab') return;
   const controls = [...event.currentTarget.querySelectorAll<HTMLElement>('button:not(:disabled), [href], input:not(:disabled), [tabindex="0"]')];
   const first = controls[0];
   const last = controls[controls.length - 1];
   if (event.shiftKey && (document.activeElement === first || document.activeElement === event.currentTarget)) { event.preventDefault(); last?.focus(); }
   else if (!event.shiftKey && (document.activeElement === last || document.activeElement === event.currentTarget)) { event.preventDefault(); first?.focus(); }
  }}>
   <header className="star-window-header">
    <div className="star-window-emblem"><Icon name="layers" size={30}/></div>
    <div className="star-window-identity"><span className="star-eyebrow">建筑星图 · T{entryTier} 起始 / 当前 T{currentTier}</span><h2 id={titleId}>{title}</h2><p>{subtitle}</p></div>
    <Button variant="ghost" aria-label="关闭建筑星图" onClick={onClose}><Icon name="close"/></Button>
   </header>
   <div className="star-capacity"><div><span>设计容量</span><strong>{used} <small>/ {capacity} 点</small></strong></div><Progress label="已用设计容量" value={used} max={capacity}/><span>占用与预留 / 总量</span></div>
   <div className="star-window-main">
    <section className="star-map" aria-label="建筑设计节点">
     <nav className="star-tier-tabs" aria-label="星图 Tier" role="tablist">{tiers.map(value => <button key={value} role="tab" aria-selected={shownTier === value} aria-controls={graphId} disabled={value < entryTier} className={shownTier === value ? 'selected' : ''} title={value < entryTier ? `该建筑从 T${entryTier} 起始` : undefined} onClick={() => { setTier(value); setSelectedId(null); setCancelId(null); }}>T{value}<small>{value < entryTier ? '起始前' : value === currentTier ? '当前' : value > currentTier ? '待进阶' : '已到达'}</small></button>)}</nav>
     <div className="star-map-scroll" id={graphId} role="tabpanel">
      <div className="star-map-caption"><span>选择节点查看变化与建造报价</span><span><i/>已启用 <i className="available"/>可建造 <i className="locked"/>未解锁</span></div>
      <div className="star-graph" ref={graphRef}>
       <svg className="star-connectors" aria-hidden="true">{paths.map(path => <path key={path.id} d={path.d} className={path.conversion ? `star-route-conversion ${path.conversionActive ? 'active' : 'locked'}` : 'star-route-original'} data-conversion={path.conversion} data-conversion-active={path.conversion ? path.conversionActive : undefined}/>)}</svg>
       {visibleNodes.map(node => <button key={node.id} data-star-node={node.id} aria-pressed={selected?.id === node.id} style={node.layout ? { gridColumn: node.layout.column, gridRow: node.layout.row } : undefined} className={`star-node star-kind-${node.kind} ${node.status} ${selected?.id === node.id ? 'selected' : ''}`} onClick={() => { setSelectedId(node.id); setCancelId(null); }}>
        <span className="star-node-orbit"><Icon name={node.kind === 'advance' ? 'arrow' : node.kind === 'slot' ? 'layers' : 'dt'} size={24}/></span>
        <span className="star-node-kind">{kindLabels[node.kind]}{node.slotType ? ` · ${slotLabels[node.slotType]}` : ''}</span>
        <strong>{node.label}</strong><span className="star-node-state">{statusLabels[node.status]}</span>
       </button>)}
       {!visibleNodes.length && <p className="star-empty">此 Tier 暂无已配置节点。</p>}
      </div>
      <p className="star-map-note">沿原路线经过槽位无需购买；转换到另一条路线，须先完成对应转换槽。琥珀虚线表示转换尚未接通，实线表示已接通。</p>
     </div>
    </section>
    <aside className="star-inspector" aria-label="节点详情">
     {selected ? <><div className="star-inspector-heading"><span className="star-eyebrow">T{selected.tier} · {kindLabels[selected.kind]}</span><Tag tone={selected.status === 'active' ? 'good' : ''}>{statusLabels[selected.status]}</Tag></div><h3>{selected.label}</h3><p>{selected.description}</p>
      <section><h4>{selected.status === 'active' ? '已获得效果' : '完成后变化'}</h4>{selected.changes.length ? <ul>{selected.changes.map((line, index) => <li key={index}>{line}</li>)}</ul> : <p className="muted">暂无已配置变化</p>}</section>
      {!!selected.routes?.length && <section className="star-routes"><h4>成长路线 · 满足任一路线</h4><ul>{selected.routes.map((route, index) => {
       const origin = nodes.find(node => node.id === route.from);
       const converters = (route.via ?? []).map(id => nodes.find(node => node.id === id));
       const met = origin?.status === 'active' && converters.every(node => node?.status === 'active');
       const conversion = !!route.via?.length;
       return <li key={index} className={met ? 'met' : ''}><span className="star-route-title">{conversion ? '转换路线' : '原路线'}<span>{met ? '已满足' : '未满足'}</span></span><span>{origin?.label ?? route.from}{origin && `（${statusLabels[origin.status]}）`}</span>{conversion && <span className="star-route-via">须完成：{converters.map((node, i) => `${node?.label ?? route.via![i]}（${node ? statusLabels[node.status] : '未满足'}）`).join('、')}</span>}</li>;
      })}</ul></section>}
      {(selected.requires.length > 0 || !selected.routes?.length) && <section><h4>前置节点 · 全部满足</h4>{selected.requires.length ? <ul>{selected.requires.map(id => { const parent = nodes.find(node => node.id === id); return <li key={id}>{parent ? `T${parent.tier} · ${parent.label}` : id}{parent && <span className={parent.status === 'active' ? 'good' : 'muted'}> · {statusLabels[parent.status]}</span>}</li>; })}</ul> : <p className="muted">无前置节点</p>}</section>}
      {selected.status!=='active'&&<section className="star-quote"><h4>建造报价</h4><dl><div><dt>占用设计容量</dt><dd>{selected.points} 点</dd></div><div><dt>建造量</dt><dd>{selected.constructionCP === null ? '待配置' : `${selected.constructionCP} CP`}</dd></div></dl>{selected.resourceLines.map((line, index) => <p key={index}>{line}</p>)}</section>}
      {blocked && <p className="star-blocked" role="status"><Icon name="alert" size={17}/>{blocked}</p>}
      {selected.status === 'active' && selected.kind !== 'advance' && !belowEntry && <Button variant="ghost" disabled={!cancellation} onClick={() => setCancelId(cancelling ? null : selected.id)}>{cancelling ? '保留此节点' : '取消节点…'}</Button>}
      {selected.status === 'active' && selected.kind !== 'advance' && !cancellation && <p className="muted">取消方案尚未提供。</p>}
      {plan && <section className="star-cancel-confirm" aria-label="确认取消节点"><h4>将取消以下节点</h4><ul>{plan.labels.map((label, index) => <li key={index}>{label}</li>)}</ul><p>归还 {plan.refundPoints} 设计点</p><p>{plan.bonusNames.length ? `退回 Bonus：${plan.bonusNames.join('、')}` : '无已安装 Bonus 需要退回'}</p><p className="star-cancel-material">已投入物资不退回。</p></section>}
     </> : <p className="muted">选择节点查看详情。</p>}
    </aside>
   </div>
   <footer className="star-window-footer"><div aria-live="polite">{pendingText && <p className="star-pending">{pendingText}</p>}{selected ? <><strong>{cancelling && plan ? `归还 ${plan.refundPoints} 设计点` : selected.status === 'active' ? `已占用 ${selected.points} 设计点` : `${selected.points} 设计点 · ${selected.constructionCP === null ? 'CP 待配置' : `${selected.constructionCP} CP`}`}</strong><small>{cancelling ? '请核对连带取消及 Bonus 退回方案后确认。' : selected.status === 'active' ? selected.kind === 'advance' ? '进阶已完成，保留永久占用，不能撤回。' : '此节点已生效；取消时返还设计点，已投入物资不退回。' : blocked || selected.resourceLines.join(' · ') || statusLabels[selected.status]}</small></> : <small>暂无节点可操作</small>}</div><div className="actions"><Button onClick={onClose}>关闭</Button>{cancelling && plan ? <Button variant="danger" onClick={() => { onCancelNode(selected!.id); setCancelId(null); }}>确认取消节点</Button> : <Button variant="primary" icon="build" disabled={!canBuild} onClick={() => selected && onBuild(selected.id)}>{selected?.status === 'active' ? '已完成' : `确认${selected?.kind === 'advance' ? '进阶' : '建造'}`}</Button>}</div></footer>
  </div>
 </div>;
}
