export type Theme='system'|'light'|'dark';
export interface PreviousSpeech {role:string;kind:'full'|'excerpt'|'summary';text:string}
export type DiagnosticStatus='complete'|'partial'|'missing'|'insufficient'|'not_applicable';
export interface EvidenceReference {source:string;quote:string;note:string;verified?:boolean}
export interface DiagnosticDetail {label:string;status:DiagnosticStatus;content:string;evidence:EvidenceReference[]}
export interface DeepDiagnostic {id:string;status:DiagnosticStatus;conclusion:string;analysis:string;impact:string;advice:string;evidence:EvidenceReference[];details:DiagnosticDetail[]}
export interface Practice { motion:string; role:string; transcript:string; context:string; previousSpeeches?:PreviousSpeech[]; duration:number; target:number }
export interface Feedback {version?:number;summary:string; dimensions:{name:string;score:number|null;reason:string}[]; strengths:string[]; improvements:{title:string;evidence:string;advice:string}[]; rewrite:string;nextDrill:string;limitations:string;priorityIssues?:string[];diagnostics?:DeepDiagnostic[]}
export interface RecordItem extends Practice { id:string;createdAt:string;model:string;provider:string;feedback:Feedback }
export interface Motion {id:unknown;key:string;title:string;zh:string;category:string;level:string;tags:string[];isCustom:boolean;createdAt?:string|null}
export interface Role {id:string;name:string;english:string;team:string;duty:string;steps:string[]}
export interface Settings {provider:string;baseUrl:string;model:string;speechProvider:string;speechUrl:string;speechModel:string;whisperModel:string;pythonPath:string;speechLanguage:string;feedbackLanguage:string}
export interface Preferences {theme:Theme;readerFontSize:number;compareRatio:number;interfaceScale:number;libraryPageSize:number;historyPageSize:number}
export interface Audio {name:string;available:boolean;duration:number;recordId:string|null;temporary:boolean}
export interface SpeechProgress {model:string;stage:string;message:string;completed:number;total:number;percent:number|null}
export interface ManagedSpeechStatus {runtimeReady:boolean;bundled:boolean;customPython:boolean;selected:string;models:{id:string;label:string;megabytes:number;description:string;installed:boolean;bundled:boolean}[]}
export interface Runtime {recording:boolean;paused:boolean;elapsed:number;level:number;position:number;playing:boolean;duration:number;busy:string|null;audio:Audio;speechProgress?:SpeechProgress|null}
export interface Snapshot {version:string;draft:Practice;settings:Settings;preferences:Preferences;theme:'light'|'dark';roles:Role[];motions:Motion[];history:{id:string;createdAt:string;motion:string;role:string;model:string;summary:string}[];review:RecordItem|null;devices:string[];secrets:{apiKey:boolean;speechKey:boolean};runtime:Runtime;speech:ManagedSpeechStatus;notice:string|null;sample:string}
export interface Confirmation {confirmation:{title:string;message:string;primary:string}}
declare global {interface Window {chrome?:{webview:{postMessage:(message:unknown)=>void;addEventListener:(name:string,handler:(event:{data:any})=>void)=>void}};__BP_THEME?:string}}
