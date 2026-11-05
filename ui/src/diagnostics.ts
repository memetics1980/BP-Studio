import type {Practice,DiagnosticStatus} from './types';

export const roleOrder=['PM','LO','DPM','DLO','MG','MO','GW','OW'];
export const diagnosticItems=[['role','辩位职责'],['structure','辩词结构'],['argument','论证框架与过程'],['characterization','情境描述'],['logic','逻辑谬误与推理漏洞'],['rhetoric','修辞与类比'],['response','回应与交锋'],['stakeholders','利益相关方与影响'],['core','核心争点与证明责任']];
export const statusNames:Record<DiagnosticStatus,string>={complete:'完成较好',partial:'部分完成',missing:'未完成',insufficient:'证据不足',not_applicable:'不适用'};
export const kindNames={full:'完整原文',excerpt:'节选',summary:'用户概述'};
export function activePrevious(p:Practice){return (p.previousSpeeches??[]).filter(s=>roleOrder.includes(s.role)&&roleOrder.indexOf(s.role)<roleOrder.indexOf(p.role)&&s.text.trim()).sort((a,b)=>roleOrder.indexOf(a.role)-roleOrder.indexOf(b.role));}
export function sources(p:Practice){return [{id:'transcript',label:'本次发言原文',text:p.transcript},{id:'context',label:'前序背景（未标辩位）',text:p.context},...activePrevious(p).map(s=>({id:'previous:'+s.role,label:`${s.role} 前序发言 · ${kindNames[s.kind]}`,text:s.text}))];}

// Return offsets into the original text, even when whitespace/case are folded.
export function findQuote(text:string,quote:string):[number,number]|null{
 const clean=quote.trim().replace(/^[“”"‘’']+|[“”"‘’']+$/g,'').trim();if(!clean)return null;
 let at=text.indexOf(clean);if(at>=0)return[at,at+clean.length];
 const fold=(value:string)=>{let normalized='',indices:number[]=[];for(let i=0;i<value.length;i++){const c=value[i];if(/\s/.test(c)){if(normalized.endsWith(' '))continue;normalized+=' ';}else normalized+=c.toLowerCase();indices.push(i);}return{normalized,indices};};
 const source=fold(text),needle=fold(clean).normalized.trim();at=source.normalized.indexOf(needle);return at<0?null:[source.indices[at],source.indices[at+needle.length-1]+1];
}
