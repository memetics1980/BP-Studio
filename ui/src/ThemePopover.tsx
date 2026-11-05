import {useState,useRef,useEffect,useLayoutEffect,useCallback,useId,type KeyboardEvent} from 'react';
import {createPortal} from 'react-dom';
import {Sun,Moon,Monitor,Check} from 'lucide-react';
import type {Theme} from './types';
import './theme-popover.css';

const choices=[{value:'system',label:'跟随系统',Icon:Monitor},{value:'light',label:'浅色',Icon:Sun},{value:'dark',label:'深色',Icon:Moon}] as const;

export function ThemePopover({value,onChange,onError}:{value:Theme;onChange:(theme:Theme)=>Promise<unknown>;onError:(text:string)=>void}){
 const [open,setOpen]=useState(false),[closing,setClosing]=useState(false),[saving,setSaving]=useState(false),[position,setPosition]=useState({top:0,left:0});
 const trigger=useRef<HTMLButtonElement>(null),popup=useRef<HTMLDivElement>(null),timer=useRef<ReturnType<typeof setTimeout>|null>(null),focusIndex=useRef(0);const id=useId();
 const close=useCallback((restoreFocus=false)=>{setClosing(true);if(timer.current)clearTimeout(timer.current);timer.current=setTimeout(()=>{setOpen(false);setClosing(false);timer.current=null;},120);if(restoreFocus)trigger.current?.focus();},[]);
 const show=(index?:number)=>{if(timer.current){clearTimeout(timer.current);timer.current=null;}focusIndex.current=index??Math.max(0,choices.findIndex(c=>c.value===value));setClosing(false);setOpen(true);};
 useEffect(()=>()=>{if(timer.current)clearTimeout(timer.current);},[]);
 useLayoutEffect(()=>{
  if(!open||closing)return;
  const place=()=>{const rect=trigger.current!.getBoundingClientRect();setPosition({top:rect.bottom+8,left:Math.max(8,Math.min(rect.right-208,innerWidth-216))});};
  place();popup.current?.querySelectorAll<HTMLButtonElement>('[role=menuitemradio]')[focusIndex.current]?.focus();
  const outside=(event:PointerEvent)=>{const node=event.target as Node;if(!trigger.current?.contains(node)&&!popup.current?.contains(node))close();};
  const blur=()=>close();
  document.addEventListener('pointerdown',outside,true);window.addEventListener('resize',place);window.addEventListener('blur',blur);
  return()=>{document.removeEventListener('pointerdown',outside,true);window.removeEventListener('resize',place);window.removeEventListener('blur',blur);};
 },[open,closing,close]);
 const select=async(theme:Theme)=>{
  if(saving)return;
  if(theme===value){close(true);return;}
  setSaving(true);
  try{await onChange(theme);close(true);}catch(error){onError((error as Error).message);}finally{setSaving(false);}
 };
 const keys=(event:KeyboardEvent<HTMLDivElement>)=>{
  const items=Array.from(popup.current?.querySelectorAll<HTMLButtonElement>('[role=menuitemradio]')??[]),current=items.indexOf(document.activeElement as HTMLButtonElement);
  if(event.key==='Escape'){event.preventDefault();event.stopPropagation();close(true);}
  else if(event.key==='Tab'){close(true);}
  else if(['ArrowDown','ArrowUp','Home','End'].includes(event.key)){event.preventDefault();const next=event.key==='Home'?0:event.key==='End'?items.length-1:(current+(event.key==='ArrowDown'?1:-1)+items.length)%items.length;items[next]?.focus();}
 };
 const CurrentIcon=choices.find(choice=>choice.value===value)?.Icon??Monitor;
 return <><button ref={trigger} className="icon theme-toggle" aria-label="外观" title={`外观：${choices.find(choice=>choice.value===value)?.label}`} aria-haspopup="menu" aria-expanded={open&&!closing} aria-controls={open?id:undefined} onClick={()=>open&&!closing?close():show()} onKeyDown={event=>{if(event.key==='ArrowDown'||event.key==='ArrowUp'){event.preventDefault();show(event.key==='ArrowDown'?0:2);}}}><CurrentIcon size={17}/></button>{open&&createPortal(<div ref={popup} id={id} role="menu" aria-label="外观选择" aria-hidden={closing||undefined} aria-busy={saving} className={`theme-flyout ${closing?'leaving':''}`} style={position} onKeyDown={keys} onBlur={event=>{const next=event.relatedTarget;if(next&&!popup.current?.contains(next as Node)&&next!==trigger.current)close();}}><div className="theme-flyout-heading">外观</div>{choices.map(({value:theme,label,Icon})=><button key={theme} type="button" tabIndex={-1} role="menuitemradio" aria-checked={value===theme} disabled={saving} onClick={()=>void select(theme)}><Icon size={17}/><span>{label}</span>{value===theme&&<Check className="theme-check" size={16}/>}</button>)}</div>,document.body)}</>;
}
