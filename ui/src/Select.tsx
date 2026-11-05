import {Children,isValidElement,useEffect,useId,useLayoutEffect,useMemo,useRef,useState,type ReactNode,type KeyboardEvent} from 'react';
import {createPortal} from 'react-dom';
import {Check,ChevronDown} from 'lucide-react';
import './select.css';

type Option={value:string;label:string;disabled:boolean};
type Props={value:string|number;onValueChange:(value:string)=>void;children:ReactNode;id?:string;className?:string;disabled?:boolean;title?:string;'aria-label'?:string};
function text(node:ReactNode):string{return Children.toArray(node).map(child=>isValidElement<{children?:ReactNode}>(child)?text(child.props.children):String(child)).join('');}

// Draw the popup in the document so WebView2's system popup cannot override its theme.
export function Select({value,onValueChange,children,id,className='',disabled=false,title,'aria-label':label}:Props){
 const options=useMemo(()=>Children.toArray(children).filter(isValidElement).map(child=>{const p=(child as React.ReactElement<{value?:string|number;children?:ReactNode;disabled?:boolean}>).props;const label=text(p.children);return{value:String(p.value??label),label,disabled:!!p.disabled};}),[children]);
 const selected=options.findIndex(option=>option.value===String(value)),selectedOption=options[selected];
 const [open,setOpen]=useState(false),[active,setActive]=useState(0),[position,setPosition]=useState({left:0,top:0,width:0,maxHeight:260});
 const trigger=useRef<HTMLButtonElement>(null),popup=useRef<HTMLDivElement>(null),search=useRef({value:'',time:0});const uid=useId(),listId=uid+'-list';
 const close=()=>setOpen(false);
 const show=()=>{if(disabled||!options.length)return;setActive(selected>=0?selected:Math.max(0,options.findIndex(o=>!o.disabled)));setOpen(true);};
 const choose=(option:Option)=>{if(option.disabled)return;if(option.value!==String(value))onValueChange(option.value);close();trigger.current?.focus();};
 useEffect(()=>{if(disabled)setOpen(false);},[disabled]);
 useLayoutEffect(()=>{
  if(!open)return;
  const place=()=>{const rect=trigger.current!.getBoundingClientRect(),gap=5,margin=8;const desired=Math.min(260,options.length*36+10),below=innerHeight-rect.bottom-margin-gap,above=rect.top-margin-gap,up=below<desired&&above>below;const maxHeight=Math.max(40,Math.min(desired,up?above:below));const width=Math.min(innerWidth-margin*2,Math.max(rect.width,Math.min(360,Math.max(...options.map(o=>o.label.length))*13+60)));setPosition({left:Math.min(Math.max(margin,rect.left),innerWidth-margin-width),top:up?Math.max(margin,rect.top-gap-maxHeight):rect.bottom+gap,width,maxHeight});};
  place();const outside=(event:PointerEvent)=>{const target=event.target as Node;if(!trigger.current?.contains(target)&&!popup.current?.contains(target))close();};const scroll=(event:Event)=>{if(!popup.current?.contains(event.target as Node))place();};
  document.addEventListener('pointerdown',outside,true);window.addEventListener('resize',place);window.addEventListener('scroll',scroll,true);
  return()=>{document.removeEventListener('pointerdown',outside,true);window.removeEventListener('resize',place);window.removeEventListener('scroll',scroll,true);};
 },[open,options]);
 useEffect(()=>{if(open)popup.current?.querySelector<HTMLElement>(`[data-index="${active}"]`)?.scrollIntoView({block:'nearest'});},[open,active]);
 const move=(delta:number)=>{let next=active;for(let i=0;i<options.length;i++){next=(next+delta+options.length)%options.length;if(!options[next].disabled){setActive(next);return;}}};
 const keys=(event:KeyboardEvent<HTMLButtonElement>)=>{
  if(event.key==='Tab'){close();return;}
  if(event.key==='Escape'&&open){event.preventDefault();event.stopPropagation();close();return;}
  if(['ArrowDown','ArrowUp','Home','End','Enter',' '].includes(event.key)){
   event.preventDefault();event.stopPropagation();
   if(!open){show();return;}
   if(event.key==='Enter'||event.key===' '){if(options[active])choose(options[active]);}
   else if(event.key==='Home')setActive(Math.max(0,options.findIndex(o=>!o.disabled)));
   else if(event.key==='End'){const last=options.map(o=>!o.disabled).lastIndexOf(true);if(last>=0)setActive(last);}
   else move(event.key==='ArrowDown'?1:-1);
   return;
  }
  if(event.key.length===1&&!event.ctrlKey&&!event.metaKey&&!event.altKey){event.preventDefault();const now=Date.now();search.current={value:(now-search.current.time<700?search.current.value:'')+event.key.toLocaleLowerCase(),time:now};const found=options.findIndex(o=>!o.disabled&&o.label.toLocaleLowerCase().startsWith(search.current.value));if(found>=0){setOpen(true);setActive(found);}}
 };
 return <><button type="button" ref={trigger} id={id} role="combobox" aria-label={label} aria-haspopup="listbox" aria-controls={open?listId:undefined} aria-expanded={open} aria-activedescendant={open?`${uid}-${active}`:undefined} disabled={disabled} title={title??selectedOption?.label} className={`select-trigger ${className}`} onClick={()=>open?close():show()} onKeyDown={keys} onBlur={close}><span>{selectedOption?.label??'请选择'}</span><ChevronDown size={14}/></button>{open&&createPortal(<div ref={popup} id={listId} role="listbox" aria-label={label??title??'选项'} className="select-popup" style={position} onPointerDown={event=>event.preventDefault()}>{options.map((option,index)=><div key={option.value} id={`${uid}-${index}`} role="option" aria-selected={option.value===String(value)} aria-disabled={option.disabled||undefined} className={`select-option ${index===active?'highlighted':''} ${option.value===String(value)?'selected':''}`} data-index={index} onPointerMove={()=>{if(!option.disabled)setActive(index);}} onClick={()=>choose(option)}><span>{option.label}</span>{option.value===String(value)&&<Check size={15}/>}</div>)}</div>,document.body)}</>;
}
