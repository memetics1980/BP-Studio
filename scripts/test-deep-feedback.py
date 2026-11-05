"""Isolated packaged-host tests. Only synthetic material is sent to a loopback fixture server."""
import copy,hashlib,json,os,pathlib,re,subprocess,sys,threading,time,urllib.request,uuid
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from playwright.sync_api import sync_playwright

ROOT=pathlib.Path(__file__).resolve().parents[1]
OUT=ROOT/'test-results/feedback-0.4.1';PORT=19440;API_PORT=19340
EXE=ROOT/'release/BP-Studio-0.4.1-Desktop-Windows-x64/BPStudio.exe'
BRIDGE="""([command,body])=>new Promise((resolve,reject)=>{const id='test-'+crypto.randomUUID();const handler=e=>{if(e.data.id!==id)return;window.chrome.webview.removeEventListener('message',handler);e.data.ok?resolve(e.data.result):reject(new Error(e.data.error));};window.chrome.webview.addEventListener('message',handler);window.chrome.webview.postMessage({id,command,body});})"""
def call(page,command,body=None):return page.evaluate(BRIDGE,[command,body or {}])
def real_hash():
 p=pathlib.Path(os.environ['APPDATA'])/'bp-studio-native/bp-studio.json'
 return hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None
def connect():
 p=sync_playwright().start()
 try:
  b=p.chromium.connect_over_cdp(f'http://127.0.0.1:{PORT}');page=next(t for c in b.contexts for t in c.pages if 'bpstudio.local' in t.url);return p,b,page
 except Exception:
  p.stop();raise
def launch(profile):
 env=dict(os.environ,BP_NATIVE_PROFILE=str(profile),BP_NATIVE_TEST='1',BP_DESKTOP_TEST_PORT=str(PORT),BP_SPEECH_ROOT=str(profile/'speech'),NO_PROXY='127.0.0.1,localhost')
 process=subprocess.Popen([str(EXE)],cwd=EXE.parent,env=env,creationflags=subprocess.CREATE_NO_WINDOW)
 for _ in range(200):
  if process.poll() is not None:raise RuntimeError('App exited before ready')
  try:
   with urllib.request.urlopen(f'http://127.0.0.1:{PORT}/json',timeout=1) as r:targets=json.load(r)
   if any('bpstudio.local' in t.get('url','') for t in targets) and (profile/'ready.json').exists():return process
  except Exception:pass
  time.sleep(.1)
 raise RuntimeError('Startup timeout')
def run():
 OUT.mkdir(parents=True,exist_ok=True)
 try:
  previous_p,previous_b,previous_page=connect()
  if call(previous_page,'load')['settings']['model']=='synthetic-fixture':previous_page.evaluate("window.chrome.webview.postMessage({id:'isolated-test-close',command:'app.close',body:{confirmed:true}})")
  previous_b.close();previous_p.stop();time.sleep(.4)
 except Exception:pass
 profile=OUT/('ui-profile-'+uuid.uuid4().hex[:8]);profile.mkdir()
 record=json.loads((OUT/'deep-fixture.json').read_text('utf-8'));legacy=json.loads((OUT/'legacy-fixture.json').read_text('utf-8'))
 record['feedback']['diagnostics'][-1]['details'][0]['evidence']=[{'source':'previous:PM','quote':'Invented evidence must not receive a locate link.','note':'Synthetic invalid-quote test','verified':True}]
 settings={'provider':'api','baseUrl':f'http://127.0.0.1:{API_PORT}/v1','model':'synthetic-fixture','speechProvider':'api','speechUrl':'https://example.com/v1','speechModel':'whisper-1','whisperModel':'base','speechLanguage':'en','feedbackLanguage':'zh','pythonPath':'','futureSetting':'keep'}
 draft={k:record[k] for k in ['motion','role','transcript','context','previousSpeeches','duration','target']};draft['futureDraft']='keep'
 state={'version':1,'settings':settings,'secrets':{},'draft':draft,'records':[record,legacy],'customMotions':[],'nativeUi':{'theme':'light','readerFontSize':18,'compareRatio':.45},'futureRoot':'keep'}
 (profile/'bp-studio.json').write_text(json.dumps(state,ensure_ascii=False),encoding='utf-8');before=real_hash();checks=[];errors=[];requests=[];response={'feedback':record['feedback']}
 class Handler(BaseHTTPRequestHandler):
  def log_message(self,*_):pass
  def do_POST(self):
   requests.append(json.loads(self.rfile.read(int(self.headers['Content-Length']))))
   data=json.dumps({'choices':[{'message':{'content':json.dumps(response['feedback'],ensure_ascii=False)}}]}).encode()
   self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(data)));self.end_headers();self.wfile.write(data)
 server=ThreadingHTTPServer(('127.0.0.1',API_PORT),Handler);threading.Thread(target=server.serve_forever,daemon=True).start()
 process=launch(profile);p,b,page=connect();page.on('pageerror',lambda e:errors.append(str(e)))
 def check(name,value=True):
  if not value:raise AssertionError(name)
  checks.append(name);print('PASS '+name,flush=True)
 def nav(name):page.get_by_role('button',name=name,exact=True).click()
 def select(label,pattern):
  page.get_by_role('combobox',name=label,exact=True).click();page.locator('.select-popup').get_by_role('option',name=re.compile(pattern)).click()
 try:
  check('Packaged version starts with mixed records',call(page,'load')['version']=='0.4.1' and len(call(page,'load')['history'])==2)
  nav('复盘工作台');check('Original five tabs retained and diagnosis added',page.get_by_role('tab').count()==6)
  check('Priority issues visible in overview',page.locator('.priority-issues li').count()==2)
  page.get_by_role('tab',name='深度诊断',exact=True).click();check('All nine diagnostic state controls rendered',page.locator('.diagnostic-grid button').count()==9)
  for button in page.locator('.diagnostic-grid button').all():
   button.click();check('Diagnostic detail '+button.inner_text().split('\n')[0],page.locator('.diagnostic-report').is_visible() and '具体修改建议' in page.locator('.diagnostic-report').inner_text())
  check('Invented quote is warned without a link',page.locator('.evidence-warning').count()==1 and page.locator('.diagnostic-details blockquote button').count()==0)
  page.locator('.diagnostic-grid button').filter(has_text='回应与交锋').click();page.get_by_role('button',name='定位这段依据',exact=True).nth(0).click()
  check('Opponent evidence locates exact preceding speech',page.locator('mark').inner_text()=='Many low-income commuters cannot afford regular tickets.' and 'DPM' in page.get_by_role('combobox',name='原文来源',exact=True).inner_text())
  page.get_by_role('button',name='定位这段依据',exact=True).nth(1).click();check('Student evidence locates current speech',page.locator('mark').inner_text()=='But a zero fare does not create an extra bus.' and '本次发言' in page.get_by_role('combobox',name='原文来源',exact=True).inner_text())
  page.locator('.diagnostic-grid button').filter(has_text='辩位职责').click();page.get_by_role('button',name='定位这段依据',exact=True).first.click();check('Summary evidence labeled as user summary','LO 前序发言 · 用户概述' in page.locator('.source-material-label').inner_text())
  select('原文来源','前序背景');check('Background separately selectable and safe literal text','<script>literal text</script>' in page.locator('.source-text').inner_text() and page.locator('mark').count()==0)
  page.get_by_role('tab',name='改进建议',exact=False).click();page.get_by_role('button',name='在原文中定位',exact=True).first.click();check('Existing improvement location retained','Their main constraint' in page.locator('mark').inner_text())
  for name,needle in [('评价维度','论证深度'),('示范改写','建议补充'),('下次训练','60 秒')]:
   page.get_by_role('tab',name=name,exact=True).click();check('Original feedback '+name,needle in page.locator('.review-document').inner_text())
  call(page,'review.open',{'id':'legacy-fixture'});page.get_by_role('tab',name='深度诊断',exact=True).click();check('Legacy UI clearly distinguishes unavailable diagnostics','原有反馈完整保留，未自动补评' in page.locator('.review-document').inner_text())
  page.get_by_role('tab',name='总览',exact=True).click();check('Legacy original summary still visible',legacy['feedback']['summary'] in page.locator('.review-document').inner_text())
  nav('发言练习');select('前序辩位','^DPM');check('Previous text and type restored',page.get_by_role('textbox',name='DPM 前序发言',exact=True).input_value()==draft['previousSpeeches'][2]['text'])
  select('前序辩位','^PM');select('PM 材料类型','^节选');page.get_by_role('textbox',name='PM 前序发言',exact=True).fill('Lower fares expand access. Edited previous speech fixture.')
  page.wait_for_timeout(750);check('Role input and material type autosave',next(s for s in call(page,'load')['draft']['previousSpeeches'] if s['role']=='PM')['kind']=='excerpt')
  select('辩位','^PM');page.wait_for_timeout(650);check('Switch to PM retains inputs and excludes future-role UI',len(call(page,'load')['draft']['previousSpeeches'])==4 and 'PM 是首位发言者' in page.locator('.previous-section').inner_text())
  select('辩位','^DLO');select('前序辩位','^PM');check('Switch back restores edited previous text','Edited previous speech' in page.get_by_role('textbox',name='PM 前序发言',exact=True).input_value())
  page.get_by_role('button',name='生成复盘',exact=True).click();page.wait_for_function("()=>document.querySelector('.page-review') && !document.querySelector('.job-bar')",timeout=20000)
  snap=call(page,'load');check('Generated diagnosis saved via real host HTTP flow',len(snap['history'])==3 and len(snap['review']['feedback']['diagnostics'])==9)
  sent=json.loads(requests[-1]['messages'][1]['content']);check('Generated request contains tagged previous inputs and omits future speech',[s['role'] for s in sent['previousSpeeches']]==['PM','LO','DPM'] and 'FUTURE-MATERIAL' not in str(sent))
  check('Generated record keeps user inputs and original context',len(snap['review']['previousSpeeches'])==4 and snap['review']['context']==draft['context'])
  response['feedback']=legacy['feedback'];nav('发言练习');page.get_by_role('button',name='生成复盘',exact=True).click();page.get_by_role('alert').filter(has_text='九项深度诊断不完整').wait_for(timeout=20000)
  check('Invalid generation keeps old records and draft',len(call(page,'load')['history'])==3 and call(page,'load')['draft']['transcript']==draft['transcript'])
  page.get_by_role('button',name='关闭提示',exact=True).click()
  for entry in ['复盘页','快捷键','练习记录']:
   if entry=='练习记录':nav('练习记录');page.locator('.record-row').first.get_by_role('button',name=re.compile('导出')).click()
   else:
    nav('复盘工作台')
    if entry=='快捷键':page.keyboard.press('Control+e')
    else:page.get_by_role('button',name='导出报告',exact=True).click()
   dialog=page.get_by_role('dialog',name='选择导出格式',exact=True)
   check(entry+' first opens four-format chooser',dialog.get_by_role('radio').count()==4 and dialog.get_by_role('button',name='选择保存位置',exact=False).is_visible())
   for label in ['Markdown','TXT 纯文本','Word 文档','PDF 文档']:
    radio=dialog.get_by_role('radio',name=re.compile('^'+re.escape(label)));radio.click();check(entry+' selects '+label,radio.get_attribute('aria-checked')=='true')
   dialog.get_by_role('button',name='取消',exact=True).click()
  nav('复盘工作台');page.get_by_role('tab',name='深度诊断',exact=True).click();page.get_by_role('button',name='宽幅复盘',exact=True).click()
  session=page.context.new_cdp_session(page)
  for theme in ['light','dark']:
   call(page,'prefs.save',{'theme':theme})
   for width,height in [(1380,868),(1000,700),(720,528),(540,600)]:
    session.send('Emulation.setDeviceMetricsOverride',{'width':width,'height':height,'deviceScaleFactor':1,'mobile':False})
    for name in ['复盘工作台','发言练习']:
     nav(name);page.wait_for_timeout(90)
     check(f'{theme} {width} {name} has no page overflow',page.evaluate('document.documentElement.scrollWidth<=innerWidth'))
     if name=='发言练习':
      select('前序辩位','^PM');field=page.get_by_role('textbox',name='PM 前序发言',exact=True);field.scroll_into_view_if_needed();check(f'{theme} {width} role input remains reachable',field.is_visible())
    nav('复盘工作台');page.get_by_role('tab',name='深度诊断',exact=True).click();page.screenshot(path=str(OUT/f'deep-{theme}-{width}.png'))
  session.send('Emulation.clearDeviceMetricsOverride');call(page,'prefs.save',{'theme':'light'});nav('发言练习')
  call(page,'draft.save',draft);nav('复盘工作台');call(page,'review.open',{'id':'deep-fixture'});page.get_by_role('tab',name='深度诊断',exact=True).click()
  check('No uncaught React errors',not errors);check('Real user data unchanged',before==real_hash())
  saved=json.loads((profile/'bp-studio.json').read_text('utf-8'));check('Unknown root/draft/settings/legacy fields preserved',saved['futureRoot']=='keep' and saved['draft']['futureDraft']=='keep' and saved['settings']['futureSetting']=='keep' and saved['records'][-1]['futureRecord']=='preserve unknown data')
  (OUT/'ui-state.json').write_text(json.dumps({'profile':str(profile),'pid':process.pid,'realHash':before}),encoding='utf-8')
  (OUT/'ui-report.json').write_text(json.dumps({'passed':True,'count':len(checks),'checks':checks,'errors':errors},ensure_ascii=False,indent=2),encoding='utf-8');print(f'Passed {len(checks)} UI checks. App retained for native dialog test.',flush=True)
 finally:
  server.shutdown();b.close();p.stop()
if __name__=='__main__':run()
