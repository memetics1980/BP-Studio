import {useState} from 'react';
import {ArrowUpRight} from 'lucide-react';
import {diagnosticItems,statusNames,sources,findQuote} from './diagnostics';
import type {RecordItem,EvidenceReference} from './types';

export function DeepDiagnostics({record,locate}:{record:RecordItem;locate:(quote:string,source?:string)=>void}){
 const [selected,setSelected]=useState('role');
 const diagnostics=record.feedback.diagnostics??[],materials=sources(record);
 const item=diagnostics.find(d=>d.id===selected);
 const evidence=(refs:EvidenceReference[]|undefined)=>refs?.length?<div className="diagnostic-evidence">{refs.map((e,i)=>{const source=materials.find(s=>s.id===e.source),found=source&&findQuote(source.text,e.quote);return <blockquote key={i}><strong>{source?.label??'来源不可用'}</strong><p className="prose">{e.quote}</p>{e.note&&<p className="muted small prose">{e.note}</p>}{found?<button className="text-button" onClick={()=>locate(e.quote,e.source)}><ArrowUpRight size={14}/>定位这段依据</button>:<p className="evidence-warning" role="note">引文未核实：未能在标注来源中找到，请核对后采用该判断。</p>}</blockquote>;})}</div>:<p className="muted small">未提供可定位的直接引文；缺失项与评价范围见分析。</p>;
 if(!diagnostics.length)return <><div className="document-kicker">九项深度诊断</div><h2>此记录尚无深度诊断</h2><p className="prose">此记录生成于旧版，原有反馈完整保留，未自动补评。新生成的复盘将包含九项深度诊断。</p></>;
 return <><div className="document-kicker">九项深度诊断 · {record.role}</div><h2>从证据理解每项判断</h2><p className="muted small">证据不足表示材料不够；不适用表示本辩位或任务无需完成该项。点击项目查看分析。</p>
 <div className="diagnostic-grid" aria-label="九项诊断状态">{diagnosticItems.map(([id,name],i)=>{const d=diagnostics.find(x=>x.id===id);return <button className={selected===id?'selected':''} key={id} aria-pressed={selected===id} onClick={()=>setSelected(id)}><span>{i+1}. {name}</span><small className={`diagnostic-status ${d?.status??'insufficient'}`}>{d?statusNames[d.status]:'未包含此项'}</small></button>;})}</div>
 {item&&<section className="diagnostic-report" aria-label={diagnosticItems.find(x=>x[0]===selected)?.[1]}><div className="diagnostic-heading"><h3>{diagnosticItems.find(x=>x[0]===selected)?.[1]}</h3><span className={`diagnostic-status ${item.status}`}>{statusNames[item.status]}</span></div>
 <h4>判断结论</h4><p className="prose lead">{item.conclusion}</p><h4>原文依据</h4>{evidence(item.evidence)}
 <h4>分析解释</h4><p className="prose">{item.analysis}</p>
 {!!item.details?.length&&<div className="diagnostic-details">{item.details.map((detail,i)=><section key={i}><div className="diagnostic-heading"><h4>{detail.label}</h4><span className={`diagnostic-status ${detail.status}`}>{statusNames[detail.status]}</span></div><p className="prose">{detail.content}</p>{evidence(detail.evidence)}</section>)}</div>}
 <h4>对说服力的影响</h4><p className="prose">{item.impact}</p><h4>具体修改建议</h4><p className="prose">{item.advice}</p></section>}
 </>;
}
