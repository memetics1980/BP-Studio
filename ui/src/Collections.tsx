import {useMemo,useState,useEffect,useLayoutEffect,useRef} from 'react';
import {Search,Plus,Shuffle,Pencil,Trash2,ArrowUpRight,Download,ChevronRight,X} from 'lucide-react';
import {Select} from './Select';
import {Pagination,useCollectionState} from './Pagination';
import type {Snapshot,Motion} from './types';
import {Modal,type Action} from './main';
import './collections.css';
const categories=['教育','科技','社会','环境','经济','其他'],levels=['入门','进阶','挑战'];
const collator=new Intl.Collator('en',{numeric:true,sensitivity:'base'}),date=(value:string|null|undefined)=>{const time=Date.parse(value??'');return Number.isFinite(time)?time:0;};
function useNarrow(){const [narrow,setNarrow]=useState(()=>matchMedia('(max-width:1000px)').matches);useEffect(()=>{const media=matchMedia('(max-width:1000px)'),changed=()=>setNarrow(media.matches);media.addEventListener('change',changed);return()=>media.removeEventListener('change',changed);},[]);return narrow;}

export function Library({snapshot:s,action,remove,edit,add,locked}:{snapshot:Snapshot;action:Action;remove:Action;edit:(m:Motion)=>void;add:()=>void;locked:boolean}){
 const [view,setView,rememberScroll]=useCollectionState('library',{search:'',category:'全部',level:'全部',mine:false,sort:'recent',page:1,pageSize:s.preferences.libraryPageSize??20,selected:'',scrollTop:0});
 const [detailOpen,setDetailOpen]=useState(false),narrow=useNarrow(),body=useRef<HTMLDivElement>(null),reveal=useRef(false);
 const list=useMemo(()=>{
  const query=view.search.trim().toLocaleLowerCase();
  return s.motions.filter(m=>(!view.mine||m.isCustom)&&(view.category==='全部'||m.category===view.category)&&(view.level==='全部'||m.level===view.level)&&`${m.title} ${m.zh} ${m.tags.join(' ')}`.toLocaleLowerCase().includes(query)).sort((a,b)=>{
   if(view.sort==='title')return collator.compare(a.title,b.title)||collator.compare(a.key,b.key);
   if(view.sort==='category')return categories.indexOf(a.category)-categories.indexOf(b.category)||levels.indexOf(a.level)-levels.indexOf(b.level)||collator.compare(a.title,b.title)||collator.compare(a.key,b.key);
   return date(b.createdAt)-date(a.createdAt)||Number(b.isCustom)-Number(a.isCustom)||collator.compare(a.key,b.key);
  });
 },[s.motions,view.search,view.category,view.level,view.mine,view.sort]);
 const page=Math.min(view.page,Math.max(1,Math.ceil(list.length/view.pageSize))),rows=list.slice((page-1)*view.pageSize,page*view.pageSize),current=rows.find(m=>m.key===view.selected)??rows[0];
 useEffect(()=>{if(page!==view.page)setView({page,scrollTop:0});},[page,view.page]);
 useLayoutEffect(()=>{if(body.current)body.current.scrollTop=view.scrollTop;},[page,view.pageSize,view.search,view.category,view.level,view.mine,view.sort]);
 useLayoutEffect(()=>{if(reveal.current){reveal.current=false;body.current?.querySelector<HTMLElement>('.motion-row[aria-selected=true]')?.scrollIntoView({block:'nearest'});}});
 const reset=(patch:Partial<typeof view>)=>setView({...patch,page:1,selected:'',scrollTop:0});
 const choose=(motion:Motion)=>{reveal.current=true;setView({selected:motion.key});if(narrow)setDetailOpen(true);};
 const detail=current?<><div className="eyebrow">{current.category} · {current.level} · {current.isCustom?'自建辩题':'内置辩题'}</div><h2>{current.title}</h2>{current.zh&&<p>{current.zh}</p>}<div className="tags">{current.tags.map(t=><span key={t}>{t}</span>)}</div><button className="primary" disabled={locked} onClick={()=>void action('practice.motion',{motion:current.title})}>用这道题练习<ArrowUpRight size={17}/></button>{current.isCustom&&<div className="detail-actions"><button disabled={locked} onClick={()=>{setDetailOpen(false);edit(current);}}><Pencil size={16}/>编辑</button><button className="danger-text" disabled={locked} onClick={()=>{setDetailOpen(false);void remove('motion.delete',{key:current.key});}}><Trash2 size={16}/>删除</button></div>}<div className="detail-note">选择辩题会保留当前草稿和音频。自建辩题保存在本机，支持修改与删除。</div></>:<p className="muted">请选择或添加一道辩题。</p>;
 return <div className="library-layout collection-layout"><div className="page-title"><div><h1>辩题库</h1><p>搜索和筛选整个题库，分页浏览。</p></div><button className="primary" disabled={locked} onClick={add}><Plus size={17}/>添加辩题</button></div>
  <div className="collection-toolbar"><div className="search"><Search size={17}/><input aria-label="搜索辩题" placeholder="搜索辩题、说明或标签" value={view.search} onChange={event=>reset({search:event.target.value})}/>{view.search&&<button className="icon" aria-label="清除辩题搜索" onClick={()=>reset({search:''})}><X size={15}/></button>}</div><Select aria-label="辩题分类" value={view.category} onValueChange={category=>reset({category})}><option value="全部">全部分类</option>{categories.map(c=><option value={c} key={c}>{c}</option>)}</Select><Select aria-label="辩题难度" value={view.level} onValueChange={level=>reset({level})}><option value="全部">全部难度</option>{levels.map(l=><option key={l}>{l}</option>)}</Select><Select aria-label="辩题排序" value={view.sort} onValueChange={sort=>reset({sort})}><option value="recent">最近添加</option><option value="title">辩题 A–Z</option><option value="category">分类与难度</option></Select><label className="check-inline"><input type="checkbox" checked={view.mine} onChange={event=>reset({mine:event.target.checked})}/>我的辩题</label><button disabled={!list.length||locked} onClick={()=>{const index=Math.floor(Math.random()*list.length);reveal.current=true;setView({page:Math.floor(index/view.pageSize)+1,selected:list[index].key,scrollTop:0});if(narrow)setDetailOpen(true);}}><Shuffle size={16}/>随机一题</button></div>
  <div className="library-body"><div ref={body} className="motion-list" role="listbox" aria-label="辩题列表" onScroll={event=>rememberScroll(event.currentTarget.scrollTop)}>{rows.map(m=><button role="option" aria-selected={current?.key===m.key} className={`motion-row ${current?.key===m.key?'selected':''}`} key={m.key} onClick={()=>choose(m)}><span className="motion-row-copy"><strong title={m.title}>{m.title}</strong>{m.zh&&<span className="motion-translation" title={m.zh}>{m.zh}</span>}</span><span className="motion-row-label">{m.category} · {m.level}{m.isCustom&&<small>自建</small>}</span><ChevronRight size={16}/></button>)}{!rows.length&&<div className="empty-inline">没有匹配的辩题，试试其他关键词或筛选。</div>}</div>{!narrow&&<section className="motion-detail">{detail}</section>}</div>
  <Pagination label="辩题库" total={list.length} unfiltered={s.motions.length} page={page} pageSize={view.pageSize} onPage={page=>setView({page,selected:'',scrollTop:0})} onSize={pageSize=>{reset({pageSize});void remove('prefs.save',{libraryPageSize:pageSize});}}/>
  {narrow&&detailOpen&&current&&<Modal title="辩题详情" className="motion-detail-dialog" onClose={()=>setDetailOpen(false)}><div className="motion-detail">{detail}</div></Modal>}
 </div>;
}

export function HistoryPage({snapshot:s,action,open}:{snapshot:Snapshot;action:Action;open:()=>void}){
 const [view,setView,rememberScroll]=useCollectionState('history',{search:'',role:'全部',model:'全部',sort:'newest',page:1,pageSize:s.preferences.historyPageSize??20,scrollTop:0});
 const body=useRef<HTMLDivElement>(null),models=useMemo(()=>Array.from(new Set(s.history.map(r=>r.model))).sort(collator.compare),[s.history]);
 const list=useMemo(()=>{
  const query=view.search.trim().toLocaleLowerCase();
  return s.history.filter(r=>(view.role==='全部'||r.role===view.role)&&(view.model==='全部'||r.model===view.model)&&`${r.motion} ${r.role} ${r.model} ${r.summary} ${r.createdAt}`.toLocaleLowerCase().includes(query)).sort((a,b)=>{
   if(view.sort==='title')return collator.compare(a.motion,b.motion)||date(b.createdAt)-date(a.createdAt)||collator.compare(a.id,b.id);
   return (view.sort==='oldest'?1:-1)*(date(a.createdAt)-date(b.createdAt))||collator.compare(a.id,b.id);
  });
 },[s.history,view.search,view.role,view.model,view.sort]);
 const page=Math.min(view.page,Math.max(1,Math.ceil(list.length/view.pageSize))),rows=list.slice((page-1)*view.pageSize,page*view.pageSize);
 useEffect(()=>{if(view.page!==page)setView({page,scrollTop:0});},[page,view.page]);
 useLayoutEffect(()=>{if(body.current)body.current.scrollTop=view.scrollTop;},[page,view.pageSize,view.search,view.role,view.model,view.sort]);
 const reset=(patch:Partial<typeof view>)=>setView({...patch,page:1,scrollTop:0});
 const show=(id:string)=>void action('review.open',{id}).then(result=>{if(result?.opened)open();});
 return <div className="history-layout collection-layout"><div className="page-title"><div><h1>练习记录</h1><p>已完成的复盘保存在本机，可搜索、筛选与分页查看。</p></div></div>
  <div className="collection-toolbar"><div className="search"><Search size={17}/><input aria-label="搜索练习记录" placeholder="搜索辩题、辩位、模型或复盘内容" value={view.search} onChange={event=>reset({search:event.target.value})}/>{view.search&&<button className="icon" aria-label="清除记录搜索" onClick={()=>reset({search:''})}><X size={15}/></button>}</div><Select aria-label="记录辩位" value={view.role} onValueChange={role=>reset({role})}><option value="全部">全部辩位</option>{s.roles.map(r=><option value={r.id} key={r.id}>{r.id} · {r.name}</option>)}</Select><Select aria-label="记录模型" value={view.model} onValueChange={model=>reset({model})}><option value="全部">全部模型</option>{models.map(m=><option value={m} key={m}>{m||'未标注模型'}</option>)}</Select><Select aria-label="记录排序" value={view.sort} onValueChange={sort=>reset({sort})}><option value="newest">最新练习</option><option value="oldest">最早练习</option><option value="title">辩题 A–Z</option></Select></div>
  <div className="history-table"><div className="record-table-heading"><span>辩题与复盘摘要</span><span>辩位</span><span>练习时间</span><span className="record-model">反馈模型</span><span>操作</span></div><div className="history-list" ref={body} onScroll={event=>rememberScroll(event.currentTarget.scrollTop)}>{rows.map(r=><article className={`record-row ${s.review?.id===r.id?'selected':''}`} key={r.id} data-record-id={r.id}><div className="record-main"><button className="history-title" title={r.motion} onClick={()=>show(r.id)}>{r.motion}</button><p title={r.summary}>{r.summary}</p></div><span className="record-role"><span className="role-badge">{r.role}</span></span><time className="record-date" dateTime={r.createdAt}>{new Date(r.createdAt).toLocaleString('zh-CN',{year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',hour12:false})}</time><span className="record-model" title={r.model}>{r.model||'未标注模型'}</span><div className="record-actions"><button onClick={()=>show(r.id)}>打开<ArrowUpRight size={15}/></button><button className="icon" title="导出复盘" aria-label={`导出 ${r.motion}`} onClick={()=>void action('review.export',{id:r.id})}><Download size={17}/></button><button className="icon danger-text" title="删除复盘" aria-label={`删除 ${r.motion}`} onClick={()=>void action('review.delete',{id:r.id})}><Trash2 size={17}/></button></div></article>)}{!rows.length&&<div className="empty-inline">{s.history.length?'没有匹配的记录，试试其他关键词或筛选。':'生成第一份复盘后，记录会出现在这里。'}</div>}</div></div>
  <Pagination label="练习记录" total={list.length} unfiltered={s.history.length} page={page} pageSize={view.pageSize} onPage={page=>setView({page,scrollTop:0})} onSize={pageSize=>{reset({pageSize});void action('prefs.save',{historyPageSize:pageSize});}}/>
 </div>;
}
