import type {Snapshot,Runtime} from './types';
type Message={id?:string;ok?:boolean;result?:any;error?:string;type?:string;data?:any};
const pending=new Map<string,{resolve:(value:any)=>void;reject:(error:Error)=>void}>();
const listeners=new Set<(message:Message)=>void>();
window.chrome?.webview.addEventListener('message',event=>{
  const message=event.data as Message;
  if(message.id){const request=pending.get(message.id);if(!request)return;pending.delete(message.id);message.ok?request.resolve(message.result):request.reject(new Error(message.error||'操作未完成'));}
  else listeners.forEach(listener=>listener(message));
});
export function invoke<T=any>(command:string,body:unknown={}):Promise<T>{return new Promise((resolve,reject)=>{if(!window.chrome?.webview){reject(new Error('请从 BP Studio 桌面程序打开此界面。'));return;}const id=crypto.randomUUID();pending.set(id,{resolve,reject});window.chrome.webview.postMessage({id,command,body});});}
export function listen(handler:(message:Message)=>void){listeners.add(handler);return()=>{listeners.delete(handler);};}
export type {Snapshot,Runtime};
