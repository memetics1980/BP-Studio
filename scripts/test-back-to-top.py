"""Packaged UI checks for feedback-pane scroll navigation, using isolated synthetic records."""
import importlib.util,json,pathlib,time,uuid

ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('base',pathlib.Path(__file__).with_name('test-deep-feedback.py'));base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
base.PORT=19441;base.EXE=ROOT/'release/BP-Studio-0.4.1-Desktop-Windows-x64/BPStudio.exe'
OUT=ROOT/'test-results/back-to-top-0.4.1';OUT.mkdir(parents=True,exist_ok=True)

def run():
 profile=OUT/('profile-'+uuid.uuid4().hex[:8]);profile.mkdir()
 record=json.loads((ROOT/'test-results/feedback-0.4.1/deep-fixture.json').read_text('utf-8'))
 legacy=json.loads((ROOT/'test-results/feedback-0.4.1/legacy-fixture.json').read_text('utf-8'))
 record['feedback']['summary']+='\n\n'+('\n\n'.join(f'{i+1}. Synthetic long feedback paragraph. 逐段检查阅读区滚动及回到顶部，不使用真实学生资料。' for i in range(35)))
 record['feedback']['rewrite']='\n\n'.join(['Synthetic rewrite paragraph for navigation checks.']*50)
 settings={'provider':'api','baseUrl':'https://example.com/v1','model':'scroll-fixture','speechProvider':'api','speechUrl':'https://example.com/v1','speechModel':'whisper-1','whisperModel':'base','speechLanguage':'en','feedbackLanguage':'zh','pythonPath':''}
 state={'settings':settings,'records':[record,legacy],'secrets':{},'customMotions':[],'draft':{k:record[k] for k in ['motion','role','transcript','context','previousSpeeches','duration','target']},'nativeUi':{'theme':'light','readerFontSize':18,'compareRatio':.45}}
 (profile/'bp-studio.json').write_text(json.dumps(state,ensure_ascii=False),encoding='utf-8');real_hash=base.real_hash()
 process=base.launch(profile);p,b,page=base.connect();checks=[];errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 def check(name,value=True):
  if not value:raise AssertionError(name)
  checks.append(name);print('PASS '+name,flush=True)
 def scroll(top):page.locator('.review-scroll').evaluate('(e,top)=>e.scrollTop=top',top);page.wait_for_timeout(70)
 def top():return page.locator('.review-scroll').evaluate('(e)=>e.scrollTop')
 button=page.get_by_role('button',name='回到反馈顶部',exact=True)
 try:
  page.get_by_role('button',name='复盘工作台',exact=True).click();check('Updated packaged version loads',base.call(page,'load')['version']=='0.4.1')
  check('Initially hidden',button.count()==0)
  scroll(299);check('Hidden before threshold',button.count()==0)
  scroll(300);check('Hidden at 300 px',button.count()==0)
  scroll(301);check('Appears after 300 px',button.is_visible())
  button.click();page.wait_for_function("()=>document.querySelector('.review-scroll').scrollTop===0")
  check('Click returns feedback to top and hides arrow',top()==0 and button.count()==0)
  check('Keyboard focus returns to reading pane',page.locator('.review-scroll').evaluate('(e)=>e===document.activeElement'))
  page.get_by_role('button',name='原文对照',exact=True).click()
  page.locator('.source-text').evaluate("e=>e.textContent+='\\n'+e.textContent.repeat(30)")
  page.locator('.source-scroll').evaluate('(e)=>e.scrollTop=420');page.wait_for_timeout(70);left=page.locator('.source-scroll').evaluate('(e)=>e.scrollTop')
  check('Source scrolling alone does not show feedback arrow',left>300 and button.count()==0)
  scroll(480);button.click();page.wait_for_function("()=>document.querySelector('.review-scroll').scrollTop===0")
  check('Feedback return leaves source scroll unchanged',page.locator('.source-scroll').evaluate('(e)=>e.scrollTop')==left)
  scroll(450);page.get_by_role('tab',name='示范改写',exact=True).click();check('Fresh tab hides arrow at top',button.count()==0 and top()==0)
  scroll(600);check('Arrow works on rewrite tab',button.is_visible())
  page.get_by_role('tab',name='总览',exact=True).click();check('Restored tab scroll restores arrow state',top()==450 and button.is_visible())
  page.get_by_role('tab',name='下次训练',exact=True).click();check('Short feedback tab has no arrow',button.count()==0)
  base.call(page,'review.open',{'id':'legacy-fixture'});page.wait_for_timeout(70);check('Switching record clears stale arrow',button.count()==0 and top()==0)
  base.call(page,'review.open',{'id':'deep-fixture'});page.get_by_role('button',name='宽幅复盘',exact=True).click();page.wait_for_timeout(70)
  scroll(550);button.focus();button.press('Enter');page.wait_for_function("()=>document.querySelector('.review-scroll').scrollTop===0");check('Keyboard activates return to top',top()==0)
  page.emulate_media(reduced_motion='reduce');scroll(500);check('Reduced-motion preference disables entrance animation',button.evaluate('(e)=>getComputedStyle(e).animationName')=='none');button.click();check('Reduced-motion return is immediate',top()==0);page.emulate_media(reduced_motion='no-preference')
  session=page.context.new_cdp_session(page)
  for theme in ['light','dark']:
   base.call(page,'prefs.save',{'theme':theme})
   for width,height in [(1380,868),(900,700),(720,528),(540,600)]:
    session.send('Emulation.setDeviceMetricsOverride',{'width':width,'height':height,'deviceScaleFactor':1,'mobile':False});scroll(600)
    check(f'{theme} {width} arrow visible inside feedback above player',button.is_visible() and button.evaluate("e=>{const r=e.getBoundingClientRect(),pane=document.querySelector('.review-split').getBoundingClientRect(),player=document.querySelector('.player').getBoundingClientRect();return r.left>=pane.left&&r.right<=pane.right&&r.bottom<=pane.bottom&&r.bottom<=player.top&&r.width>=44&&r.height>=44;}") )
    page.screenshot(path=str(OUT/f'arrow-{theme}-{width}.png'));button.click();page.wait_for_function("()=>document.querySelector('.review-scroll').scrollTop===0");check(f'{theme} {width} return works',button.count()==0)
  session.send('Emulation.setDeviceMetricsOverride',{'width':720,'height':528,'deviceScaleFactor':1,'mobile':False});page.get_by_role('button',name='原文对照',exact=True).click();scroll(500)
  page.get_by_role('button',name='查看原文',exact=True).click();check('Narrow source-only view hides feedback arrow',not button.is_visible())
  page.get_by_role('button',name='查看复盘',exact=True).click();check('Returning to feedback restores arrow',button.is_visible());button.click();page.wait_for_function("()=>document.querySelector('.review-scroll').scrollTop===0")
  session.send('Emulation.clearDeviceMetricsOverride');check('No uncaught UI errors',not errors);check('Real user profile unchanged',real_hash==base.real_hash())
  (OUT/'report.json').write_text(json.dumps({'passed':True,'count':len(checks),'checks':checks,'errors':errors},ensure_ascii=False,indent=2),encoding='utf-8');print(f'Passed {len(checks)} back-to-top checks.',flush=True)
 finally:
  page.evaluate("window.chrome.webview.postMessage({id:'scroll-check-close',command:'app.close',body:{confirmed:true}})");b.close();p.stop()

if __name__=='__main__':run()
