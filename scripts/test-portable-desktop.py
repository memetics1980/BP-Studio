"""Fresh-user checks against a published or ZIP-extracted offline desktop build."""
import argparse,hashlib,json,os,pathlib,subprocess,time,urllib.request,uuid,xml.etree.ElementTree as ET
from playwright.sync_api import sync_playwright
ROOT=pathlib.Path(__file__).resolve().parents[1];VERSION=ET.parse(ROOT/'native/BPStudio.Desktop/BPStudio.Desktop.csproj').findtext('.//Version');OUT=ROOT/f'test-results/desktop-{VERSION}-portable';OUT.mkdir(parents=True,exist_ok=True)
STATE=OUT/'state.json';PORT=19436
BRIDGE="""([command,body])=>new Promise((resolve,reject)=>{const id='portable-'+crypto.randomUUID();const handler=e=>{if(e.data.id!==id)return;window.chrome.webview.removeEventListener('message',handler);e.data.ok?resolve(e.data.result):reject(Error(e.data.error));};window.chrome.webview.addEventListener('message',handler);window.chrome.webview.postMessage({id,command,body});})"""
def call(page,command,body=None):return page.evaluate(BRIDGE,[command,body or {}])
def user_hash():
 p=pathlib.Path(os.environ['APPDATA'])/'bp-studio-native/bp-studio.json'
 return hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None
def connect():
 p=sync_playwright().start();b=p.chromium.connect_over_cdp(f'http://127.0.0.1:{PORT}');page=next(t for c in b.contexts for t in c.pages if 'bpstudio.local' in t.url);return p,b,page
def prepare(exe):
 exe=pathlib.Path(exe).resolve();profile=OUT/('fresh-'+uuid.uuid4().hex[:8]);profile.mkdir()
 env=dict(os.environ,BP_NATIVE_PROFILE=str(profile),BP_NATIVE_TEST='1',BP_DESKTOP_TEST_PORT=str(PORT),BP_SPEECH_ROOT=str(profile/'speech'),HF_HOME=str(profile/'empty-hf'),HF_HUB_OFFLINE='1',HF_DATASETS_OFFLINE='1',PATH=os.environ['SystemRoot']+'\\System32;'+os.environ['SystemRoot'])
 process=subprocess.Popen([str(exe)],cwd=exe.parent,env=env)
 STATE.write_text(json.dumps({'profile':str(profile),'pid':process.pid,'exe':str(exe),'realHash':user_hash(),'initiallyEmpty':True}),encoding='utf-8')
 for _ in range(300):
  try:
   with urllib.request.urlopen(f'http://127.0.0.1:{PORT}/json',timeout=1) as r:targets=json.load(r)
   if any('bpstudio.local' in x.get('url','') for x in targets) and (profile/'ready.json').exists():break
  except Exception:pass
  if process.poll() is not None:raise RuntimeError('Packaged app exited before ready')
  time.sleep(.1)
 else:raise RuntimeError('Packaged app did not become ready')
 p,b,page=connect()
 try:
  deadline=time.monotonic()+90
  while time.monotonic()<deadline:
   s=call(page,'load')
   if s['speech']['runtimeReady'] and next(m for m in s['speech']['models'] if m['id']=='base')['installed'] and not s['runtime']['busy']:break
   time.sleep(.25)
  else:raise RuntimeError('Automatic offline Base preparation did not finish')
  assert s['settings']['speechProvider']=='local' and s['settings']['pythonPath']=='' and s['settings']['whisperModel']=='base'
  assert not s['history'] and not s['secrets']['apiKey'] and not s['secrets']['speechKey']
  assert s['version']==VERSION
  print(json.dumps({'ready':True,'autoBase':True,'noSystemPython':True,'emptyHistoryAndCredentials':True,'browser':b.version,'profile':str(profile)},ensure_ascii=False),flush=True)
 finally:b.close();p.stop()
def inspect():
 state=json.loads(STATE.read_text());checks=[];p,b,page=connect()
 def check(name,value=True):
  if not value:raise AssertionError(name)
  checks.append(name)
 try:
  page.get_by_role('button',name='关于',exact=True).click();dialog=page.get_by_role('dialog',name='关于',exact=True);dialog.wait_for()
  check('About displays exact product, version and developer',all(x in dialog.inner_text() for x in ['BP辩论训练工作台','当前版本：'+VERSION,'开发者：少数派memetics']))
  page.keyboard.press('Escape');check('Escape closes About and restores focus',dialog.count()==0 and page.get_by_role('button',name='关于',exact=True).evaluate('(e)=>document.activeElement===e'))
  for theme in ['light','dark']:
   call(page,'prefs.save',{'theme':theme});page.get_by_role('button',name='关于',exact=True).click();dialog=page.get_by_role('dialog',name='关于',exact=True)
   check('About follows '+theme,page.evaluate('document.documentElement.dataset.theme')==theme)
   page.screenshot(path=str(OUT/('about-'+theme+'.png')));dialog.get_by_role('button',name='关闭',exact=True).click()
  session=page.context.new_cdp_session(page)
  for width,height in [(944,460),(1280,720),(2560,1360)]:
   session.send('Emulation.setDeviceMetricsOverride',{'width':width,'height':height,'deviceScaleFactor':1.5,'mobile':False})
   page.get_by_role('button',name='关于',exact=True).click();dialog=page.get_by_role('dialog',name='关于',exact=True)
   check(f'About readable and inside {width}x{height}',dialog.evaluate('(e)=>{const r=e.getBoundingClientRect();return r.left>=0&&r.top>=0&&r.right<=innerWidth+1&&r.bottom<=innerHeight+1&&e.scrollWidth<=e.clientWidth+1}'))
   dialog.get_by_role('button',name='关闭',exact=True).click()
  session.send('Emulation.clearDeviceMetricsOverride')
  page.get_by_role('button',name='关于',exact=True).click();page.get_by_role('dialog',name='关于',exact=True).get_by_role('button',name='关闭对话框',exact=True).click();check('Titlebar close button works',page.get_by_role('dialog',name='关于',exact=True).count()==0)
  s=call(page,'load');check('Automatic Base ready',s['speech']['runtimeReady'] and next(m for m in s['speech']['models'] if m['id']=='base')['installed']);check('Other models remain optional',all(not m['installed'] for m in s['speech']['models'] if m['id']!='base'))
  check('Real user data unchanged',user_hash()==state['realHash']);check('Bundled browser version selected',b.version.startswith('156.'))
  (OUT/'ui-report.json').write_text(json.dumps({'passed':True,'checks':checks,'profile':state['profile'],'exe':state['exe'],'scope':'Fresh isolated profile, empty HF/speech caches and PATH without Python; actual Windows 11 monitor plus viewport simulation.'},ensure_ascii=False,indent=2),encoding='utf-8');print(f'Passed {len(checks)} portable UI checks.')
 finally:b.close();p.stop()
def action(mode):
 p,b,page=connect()
 try:
  if mode=='import':page.get_by_role('button',name='导入音频',exact=True).click(no_wait_after=True)
  if mode=='transcribe':
   s=call(page,'load');assert s['runtime']['audio']['available'];page.get_by_role('button',name='转写',exact=True).click();page.wait_for_function('()=>document.querySelector(".transcript-editor").value.length>30',timeout=90000);s=call(page,'load');assert len(s['draft']['transcript'])>30;assert not s['history'];state=json.loads(STATE.read_text());assert user_hash()==state['realHash'];(OUT/'transcription-report.json').write_text(json.dumps({'passed':True,'model':'base','offline':True,'text':s['draft']['transcript'],'duration':s['runtime']['audio']['duration'],'exe':state['exe']},ensure_ascii=False,indent=2),encoding='utf-8');print('Actual app offline Base transcription passed.');page.screenshot(path=str(OUT/'transcription.png'))
  if mode=='close':page.evaluate("window.chrome.webview.postMessage({id:'portable-close',command:'app.close',body:{confirmed:true}})")
 finally:b.close();p.stop()
if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('mode',choices=['prepare','inspect','import','transcribe','close']);ap.add_argument('--exe',default=str(ROOT/f'release/BP-Studio-{VERSION}-Desktop-Windows-x64/BPStudio.exe'));a=ap.parse_args();prepare(a.exe) if a.mode=='prepare' else inspect() if a.mode=='inspect' else action(a.mode)
