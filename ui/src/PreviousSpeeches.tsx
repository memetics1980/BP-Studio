import {useEffect,useState} from 'react';
import {Select} from './Select';
import {roleOrder,kindNames} from './diagnostics';
import type {Practice,PreviousSpeech,Role} from './types';

export function PreviousSpeeches({draft,roles,disabled,onChange}:{draft:Practice;roles:Role[];disabled:boolean;onChange:(patch:Partial<Practice>)=>void}){
 const available=roleOrder.slice(0,Math.max(0,roleOrder.indexOf(draft.role)));
 const [selected,setSelected]=useState(available.at(-1)??'');
 useEffect(()=>{if(!available.includes(selected))setSelected(available.at(-1)??'');},[draft.role,selected]);
 const current=(draft.previousSpeeches??[]).find(s=>s.role===selected)??{role:selected,kind:'full' as const,text:''};
 const patch=(change:Partial<PreviousSpeech>)=>onChange({previousSpeeches:[...(draft.previousSpeeches??[]).filter(s=>s.role!==selected),{...current,...change}]});
 const hidden=(draft.previousSpeeches??[]).filter(s=>!available.includes(s.role)&&s.text.trim());
 const otherLength=(draft.previousSpeeches??[]).filter(s=>s.role!==selected).reduce((n,s)=>n+s.text.length,0);
 const teammate=selected&&roles.find(r=>r.id===selected)?.team===roles.find(r=>r.id===draft.role)?.team;
 // Bench membership matters for closing as well as the speaker's own team.
 const sameBench=selected&&roleOrder.indexOf(selected)%2===roleOrder.indexOf(draft.role)%2;
 return <section className="previous-section"><div className="panel-heading"><h2>分角色前序发言</h2><span>选填</span></div>
  {available.length?<><label>前序辩位<Select aria-label="前序辩位" value={selected} disabled={disabled} onValueChange={setSelected}>{available.map(id=><option value={id} key={id}>{id} · {roles.find(r=>r.id===id)?.name}{(draft.previousSpeeches??[]).some(s=>s.role===id&&s.text.trim())?' · 已填写':''}</option>)}</Select></label>
  <p className="previous-relation">{teammate?'本队队友':sameBench?'同持方另一队':'对方持方'} · 按实际提供的内容判断</p>
  <label>材料类型<Select aria-label={`${selected} 材料类型`} value={current.kind} disabled={disabled} onValueChange={value=>patch({kind:value as PreviousSpeech['kind']})}>{Object.entries(kindNames).map(([key,label])=><option value={key} key={key}>{label}</option>)}</Select></label>
  <textarea aria-label={`${selected} 前序发言`} disabled={disabled} value={current.text} maxLength={Math.max(0,Math.min(30000,90000-otherLength))} onChange={e=>patch({text:e.target.value})} placeholder={`填写 ${selected} 的发言；概述请标为“用户概述”。`}/><small>{current.text.length.toLocaleString()} / 30,000 字符 · 所有辩位合计最多 90,000</small></>:<p className="muted small">PM 是首位发言者，无需提供前序辩位。</p>}
  {hidden.length>0&&<p className="previous-retained">已保留 {hidden.map(s=>s.role).join('、')} 的材料，因属于当前或后续辩位，不参与本次分析。</p>}
 </section>;
}
