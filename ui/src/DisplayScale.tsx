import {useState,useEffect,useLayoutEffect,useRef} from 'react';
import {createPortal} from 'react-dom';
import {Type,Minus,Plus,RotateCcw} from 'lucide-react';
import type {Preferences} from './types';
import {invoke} from './bridge';
import './display-scale.css';

const scales=[.9,1,1.1,1.25,1.5];
export function DisplayScale({preferences,notify}:{preferences:Preferences;notify:(message:string,error?:boolean)=>void}){
 const value=preferences.interfaceScale??1,[open,setOpen]=useState(false),[saving,setSaving]=useState(false),[position,setPosition]=useState({top:0,left:0});
 const trigger=useRef<HTMLButtonElement>(null),panel=useRef<HTMLDivElement>(null);
 const apply=async(next:number)=>{if(saving)return;setSaving(true);try{await invoke('prefs.save',{interfaceScale:next});}catch(error){notify((error as Error).message,true);}finally{setSaving(false);}};
 useLayoutEffect(()=>{
  if(!open)return;
  const place=()=>{const r=trigger.current!.getBoundingClientRect();setPosition({top:r.bottom+8,left:Math.max(8,Math.min(r.right-260,innerWidth-268))});};
  place();panel.current?.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();
  const outside=(event:PointerEvent)=>{const node=event.target as Node;if(!trigger.current?.contains(node)&&!panel.current?.contains(node))setOpen(false);};
  document.addEventListener('pointerdown',outside,true);window.addEventListener('resize',place);return()=>{document.removeEventListener('pointerdown',outside,true);window.removeEventListener('resize',place);};
 },[open]);
 useEffect(()=>{const handler=(event:KeyboardEvent)=>{if(!event.ctrlKey||event.altKey)return;let next:number|undefined;if(event.key==='+'||event.key==='=')next=scales.find(s=>s>value+.001);if(event.key==='-')next=[...scales].reverse().find(s=>s<value-.001);if(event.key==='0')next=1;if(['+','=','-','0'].includes(event.key))event.preventDefault();if(next!==undefined)void apply(next);};document.addEventListener('keydown',handler);return()=>document.removeEventListener('keydown',handler);},[value,saving]);
 return <><button ref={trigger} className="display-scale-trigger quiet" aria-label="界面大小" aria-haspopup="dialog" aria-expanded={open} title="界面大小 · Ctrl + / − / 0" onClick={()=>setOpen(!open)}><Type size={18}/><span>{Math.round(value*100)}%</span></button>{open&&createPortal(<div className="display-scale-panel" ref={panel} role="dialog" aria-label="界面大小" style={position} onKeyDown={event=>{if(event.key==='Escape'){event.preventDefault();setOpen(false);trigger.current?.focus();}}}><h2>界面大小</h2><p>在 Windows 缩放基础上调整，自动保存。</p><div className="scale-stepper"><button className="icon" aria-label="缩小界面" disabled={saving||value<=scales[0]} onClick={()=>void apply([...scales].reverse().find(s=>s<value-.001)??scales[0])}><Minus size={18}/></button><strong>{Math.round(value*100)}%</strong><button className="icon" aria-label="放大界面" disabled={saving||value>=scales[scales.length-1]} onClick={()=>void apply(scales.find(s=>s>value+.001)??scales[scales.length-1])}><Plus size={18}/></button></div><div className="scale-presets">{scales.map(scale=><button key={scale} disabled={saving} aria-pressed={Math.abs(value-scale)<.001} onClick={()=>void apply(scale)}>{Math.round(scale*100)}%</button>)}</div><button className="text-button" disabled={saving||value===1} onClick={()=>void apply(1)}><RotateCcw size={14}/>恢复默认大小</button></div>,document.body)}</>;
}
