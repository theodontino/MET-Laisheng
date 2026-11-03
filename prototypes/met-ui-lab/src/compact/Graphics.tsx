import type {ReactNode} from 'react';
import {fmt,signed} from '../model';
import {Icon} from '../ui';
import './graphics.css';

/** A capacity has a real denominator; zero capacity is explicitly unavailable. */
export function Meter({label,value,max,unit='',note,icon,tone='',onClick,overflowWarning=true}:{label:string;value:number;max:number;unit?:string;note?:ReactNode;icon?:string;tone?:string;overflowWarning?:boolean;onClick?:()=>void}){
 const width=max>0?Math.min(100,Math.max(0,value/max*100)):0;
 const text=max<=0&&value===0?'暂无容量':`${fmt(value)} / ${fmt(max)}${unit?` ${unit}`:''}`;
 const body=<><div className="viz-meter-title"><span>{icon&&<Icon name={icon} size={16}/>} {label}</span><strong>{text}</strong></div><div className={`viz-track ${tone} ${overflowWarning&&value>max?'overflow':''}`} role="meter" aria-label={label} aria-valuemin={0} aria-valuemax={Math.max(max,1)} aria-valuenow={Math.min(Math.max(value,0),Math.max(max,1))} aria-valuetext={`${text}${max<=0?'，暂无容量':''}`}><i style={{width:`${width}%`}}/>{overflowWarning&&value>max&&<b>!</b>}</div>{note&&<small>{note}</small>}</>;
 return onClick?<button className="viz-meter clickable" onClick={onClick}>{body}</button>:<div className="viz-meter">{body}</div>;
}
export function FlowBar({label,value,scale,onClick,action,unit='/ 月'}:{label:string;value:number;scale:number;unit?:string;onClick?:()=>void;action?:ReactNode}){
 const width=scale>0?Math.min(50,Math.abs(value)/scale*50):0;
 return <div className="viz-flow"><button className="viz-flow-name" onClick={onClick} disabled={!onClick}>{label}</button><div className="viz-flow-track" aria-label={`${label} ${signed(value)} ${unit}`}><i className={value<0?'negative':'positive'} style={{left:value<0?`${50-width}%`:'50%',width:`${width}%`}}/><span/></div><b className={value<0?'bad':'good'}>{signed(value)}</b>{action}</div>;
}
export function Composition({parts}:{parts:{label:string;value:number}[]}){const total=parts.reduce((n,p)=>n+p.value,0);return <div className="viz-composition"><div className="viz-stack" aria-label={parts.map(p=>`${p.label} ${fmt(p.value)} CP/月`).join('，')}>{parts.map((p,i)=><i key={p.label} className={`part-${i}`} style={{width:`${total?p.value/total*100:0}%`}}/>)}</div><div className="viz-stack-legend">{parts.map((p,i)=><span key={p.label}><i className={`part-${i}`}/>{p.label} <b>{fmt(p.value)}</b></span>)}</div></div>}
