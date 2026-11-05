import {useState,useEffect} from 'react';
import {ChevronsLeft,ChevronLeft,ChevronRight,ChevronsRight} from 'lucide-react';
import {Select} from './Select';

const memory=new Map<string,unknown>();
export function useCollectionState<T extends {scrollTop:number}>(key:string,initial:T){
 const [value,setValue]=useState<T>(()=>(memory.get(key) as T)??initial);
 const update=(patch:Partial<T>)=>setValue(previous=>{const next={...previous,...(memory.get(key) as T),...patch};memory.set(key,next);return next;});
 const rememberScroll=(top:number)=>memory.set(key,{...(memory.get(key) as T??value),scrollTop:top});
 return [value,update,rememberScroll] as const;
}
export function Pagination({label,total,unfiltered,page,pageSize,onPage,onSize}:{label:string;total:number;unfiltered:number;page:number;pageSize:number;onPage:(page:number)=>void;onSize:(size:number)=>void}){
 const pages=Math.max(1,Math.ceil(total/pageSize)),[jump,setJump]=useState(String(page));
 useEffect(()=>setJump(String(page)),[page]);
 const submit=()=>{const value=Number(jump);if(Number.isInteger(value)&&value>=1){const target=Math.min(pages,value);setJump(String(target));onPage(target);}else setJump(String(page));};
 return <footer className="pagination" aria-label={`${label}分页`}>
  <span className="pagination-count" aria-live="polite">共 <strong>{total.toLocaleString()}</strong> 条{total!==unfiltered&&<span> / 全部 {unfiltered.toLocaleString()} 条</span>}<small>{total?`${(page-1)*pageSize+1}–${Math.min(page*pageSize,total)}`:'0'} 条</small></span>
  <div className="pagination-controls"><Select aria-label={`${label}每页条数`} value={pageSize} onValueChange={value=>onSize(Number(value))}>{[10,20,50].map(size=><option value={size} key={size}>{size} 条 / 页</option>)}</Select>
  <div className="page-buttons"><button className="icon" aria-label={`${label}首页`} disabled={page<=1} onClick={()=>onPage(1)}><ChevronsLeft size={17}/></button><button className="icon" aria-label={`${label}上一页`} disabled={page<=1} onClick={()=>onPage(page-1)}><ChevronLeft size={17}/></button><span>{page} / {pages}</span><button className="icon" aria-label={`${label}下一页`} disabled={page>=pages} onClick={()=>onPage(page+1)}><ChevronRight size={17}/></button><button className="icon" aria-label={`${label}末页`} disabled={page>=pages} onClick={()=>onPage(pages)}><ChevronsRight size={17}/></button></div>
  <form onSubmit={event=>{event.preventDefault();submit();}}><label>跳至<input aria-label={`${label}跳转页码`} type="text" inputMode="numeric" pattern="[0-9]+" maxLength={6} value={jump} onChange={event=>setJump(event.target.value)}/></label><button type="submit">前往</button></form></div>
 </footer>;
}
