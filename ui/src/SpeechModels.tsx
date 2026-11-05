import {Check,Download,AudioLines,LoaderCircle} from 'lucide-react';
import type {Action} from './main';
import type {ManagedSpeechStatus,Settings,SpeechProgress} from './types';
import './speech.css';

export function SpeechModels({status,settings,progress,locked,action,selected}:{status:ManagedSpeechStatus;settings:Settings;progress?:SpeechProgress|null;locked:boolean;action:Action;selected:(model:string)=>void}){
 const choose=async(model:string,download:boolean)=>{const result=await action('speech.selectModel',{model,download});if(result?.ready)selected(result.model);};
 return <section className="speech-models" aria-label="本地语音模型管理">
  <div className="speech-engine"><AudioLines size={20}/><div><strong>内置离线语音识别</strong><p>{settings.pythonPath?'正在使用高级选项中的自定义环境。':status.runtimeReady?'运行环境已准备，无需安装 Python。':'首次使用自动准备随附的语音运行环境。'}</p></div><span className={`speech-state ${status.runtimeReady?'ready':''}`}>{status.runtimeReady?'环境就绪':'待准备'}</span></div>
  <p className="speech-intro">Base 随程序附带，无需联网下载。其他模型仅在你点击下载时安装；大小为近似值，实际以下载进度为准。</p>
  {!status.bundled&&<p className="form-error" role="alert">缺少随附语音资源，请使用包含 speech 文件夹的完整发布目录。</p>}
  <div className="speech-model-list">{status.models.map(model=>{const active=settings.whisperModel===model.id&&!settings.pythonPath,working=progress?.model===model.id;
   return <div key={model.id} className={`speech-model-row ${active?'active':''}`}><div className="speech-model-description"><div><strong>{model.label}</strong>{model.bundled&&<small className="speech-tag">随附</small>}<span className="speech-size">约 {model.megabytes>=1000?(model.megabytes/1000).toFixed(1)+' GB':model.megabytes+' MB'}</span></div><p>{model.description}</p><span className={`speech-model-state ${model.installed?'ready':''}`}>{working?<><LoaderCircle size={12} className="spin"/>{progress.message}</>:model.installed?<><Check size={12}/>已安装 · 可离线使用</>:model.bundled?'首次选择时自动安装':'未安装'}</span></div><button className={active&&model.installed?'quiet':model.id==='base'?'primary':''} disabled={locked||(!status.bundled)||active&&model.installed} aria-label={`${model.installed?'使用':model.bundled?'准备':'下载并使用'} ${model.label}`} aria-pressed={active&&model.installed} onClick={()=>void choose(model.id,!model.bundled&&!model.installed)}>{active&&model.installed?<><Check size={14}/>已启用</>:model.installed?'使用模型':model.bundled?'准备并使用':<><Download size={14}/>下载并使用</>}</button></div>;
  })}</div>
  {progress&&<div className="speech-progress" role="status"><div><span>{progress.message}</span><strong>{progress.percent===null?'':Math.round(progress.percent)+'%'}</strong></div><progress aria-label="语音模型准备进度" max={100} value={progress.percent??undefined}/><div><span>{progress.total>0?`${(progress.completed/1048576).toFixed(1)} / ${(progress.total/1048576).toFixed(1)} MB`:'完成校验后即可使用，较大模型需要更多时间。'}</span><button className="text-button" onClick={()=>void action('job.cancel')}>取消准备</button></div></div>}
 </section>;
}
