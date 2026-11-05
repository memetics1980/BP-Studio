"""Build a relocatable CPU speech runtime and base weights. No end-user Python/pip required."""
import argparse,hashlib,json,os,pathlib,shutil,subprocess,sys,time,urllib.request,zipfile
ROOT=pathlib.Path(__file__).resolve().parents[1]
CACHE=ROOT/'.cache'/'speech-bundle'
PYTHON='3.14.10';PYTHON_SHA='90905e64a840557d8c1bc4f5b74db3d7ee85e3ffb217611cac1c7813acea4159'
RUNTIME_ID='cpu-py314-20261104-v2'
BASE_REVISION='ebe41f70d5b6dfa9166e2c581c45c9c0cfc57b66'
BASE_SHA='d01c3014881c9c6f3133c182f3d2887eb6ca1c789a7538c5c007196857a0a6a9'
def sha(path):
 h=hashlib.sha256()
 with open(path,'rb') as f:
  for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
 return h.hexdigest()
def fetch(url,path,digest=None):
 if path.exists() and (digest is None or sha(path)==digest):return
 path.parent.mkdir(parents=True,exist_ok=True);temp=path.with_suffix(path.suffix+'.partial')
 with urllib.request.urlopen(url,timeout=60) as source,open(temp,'wb') as target:shutil.copyfileobj(source,target,1024*1024)
 if digest and sha(temp)!=digest:raise RuntimeError('Checksum mismatch: '+path.name)
 os.replace(temp,path)
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--output',default=str(CACHE/'payload'));args=parser.parse_args();output=pathlib.Path(args.output).resolve()
 # Rebuild when the packaging code/requirements change; verify cached bytes before reuse.
 build_key=hashlib.sha256(pathlib.Path(__file__).read_bytes()+(ROOT/'scripts/speech-requirements.txt').read_bytes()).hexdigest()
 marker=output/'bundle.json'
 if marker.exists():
  manifest=json.loads(marker.read_text('utf-8'))
  if manifest.get('runtimeId')==RUNTIME_ID and manifest.get('buildKey')==build_key and all((output/f['path']).is_file() and (output/f['path']).stat().st_size==f['size'] and sha(output/f['path'])==f['sha256'] for f in manifest['files']):
   print('Speech payload ready: '+str(output),flush=True);return
 output.mkdir(parents=True,exist_ok=True);engine=output/'engine';engine.mkdir(exist_ok=True)
 archive=CACHE/f'python-{PYTHON}-embed-amd64.zip';print('Preparing official embedded Python '+PYTHON,flush=True)
 fetch(f'https://www.python.org/ftp/python/{PYTHON}/{archive.name}',archive,PYTHON_SHA)
 with zipfile.ZipFile(archive) as z:z.extractall(engine)
 (engine/'python314._pth').write_text('python314.zip\n.\nLib/site-packages\nimport site\n',encoding='utf-8')
 wheels=CACHE/('wheels-'+RUNTIME_ID);wheels.mkdir(exist_ok=True)
 subprocess.run([sys.executable,'-m','pip','download','--disable-pip-version-check','--only-binary=:all:','--platform','win_amd64','--python-version','3.14','--implementation','cp','--abi','cp314','--dest',str(wheels),'-r',str(ROOT/'scripts/speech-requirements.txt')],check=True)
 packages=engine/'Lib/site-packages';packages.mkdir(parents=True,exist_ok=True);lock=[]
 for wheel in sorted(wheels.glob('*.whl')):
  package,version=wheel.name.split('-')[:2]
  with urllib.request.urlopen(f'https://pypi.org/pypi/{package}/{version}/json',timeout=30) as response:metadata=json.load(response)
  trusted=next(f['digests']['sha256'] for f in metadata['urls'] if f['filename']==wheel.name)
  if sha(wheel)!=trusted:raise RuntimeError('Wheel checksum mismatch: '+wheel.name)
  lock.append({'file':wheel.name,'sha256':trusted})
  with zipfile.ZipFile(wheel) as z:
   for member in z.infolist():
    # Wheels here have purelib/platlib content at root; preserve package notices.
    name=member.filename
    if '.data/' in name:
     tail=name.split('.data/',1)[1]
     if tail.startswith(('purelib/','platlib/')):name=tail.split('/',1)[1]
     elif package=='msvc_runtime' and tail.startswith('data/') and '/' not in tail[5:] and tail.endswith('.dll'):
      with z.open(member) as src,open(engine/tail[5:],'wb') as dst:shutil.copyfileobj(src,dst)
      continue
     else:continue
    target=(packages/name).resolve()
    if not target.is_relative_to(packages.resolve()):raise RuntimeError('Unsafe wheel entry')
    if member.is_dir():target.mkdir(parents=True,exist_ok=True)
    else:
     target.parent.mkdir(parents=True,exist_ok=True)
     with z.open(member) as src,open(target,'wb') as dst:shutil.copyfileobj(src,dst)
 (output/'wheel-lock.json').write_text(json.dumps(lock,indent=2),encoding='utf-8')
 base=output/'models/base';base.mkdir(parents=True,exist_ok=True)
 for name in ['config.json','model.bin','tokenizer.json','vocabulary.txt']:
  target=base/name
  if target.exists() and target.stat().st_size>0:continue
  # Reuse a verified developer cache, copying actual bytes rather than symlinks.
  for hub in [ROOT/'.cache/huggingface/hub',pathlib.Path.home()/'.cache/huggingface/hub']:
   cached=hub/'models--Systran--faster-whisper-base'/'snapshots'/BASE_REVISION/name
   if cached.is_file():shutil.copyfile(cached,target);break
  if not target.is_file():fetch(f'https://huggingface.co/Systran/faster-whisper-base/resolve/{BASE_REVISION}/{name}',target,BASE_SHA if name=='model.bin' else None)
 if sha(base/'model.bin')!=BASE_SHA:raise RuntimeError('Base model checksum mismatch')
 fetch(f'https://huggingface.co/Systran/faster-whisper-base/raw/{BASE_REVISION}/README.md',base/'MODEL_CARD.md')
 fetch('https://raw.githubusercontent.com/openai/whisper/main/LICENSE',base/'WHISPER-LICENSE.txt')
 # Validate the payload without any venv, user site, registry paths, or HF network cache.
 env=dict(os.environ,PYTHONNOUSERSITE='1',HF_HUB_OFFLINE='1',HF_HOME=str(CACHE/'empty-hf'))
 subprocess.run([str(engine/'python.exe'),'-I','-c',"import sys;from faster_whisper import WhisperModel;from faster_whisper.audio import decode_audio;WhisperModel(sys.argv[1],device='cpu',compute_type='int8',local_files_only=True);print('Embedded CPU runtime and bundled base load: OK')",str(base)],check=True,env=env,cwd=engine)
 manifest={'runtimeId':RUNTIME_ID,'buildKey':build_key,'python':PYTHON,'engine':'faster-whisper 1.2.1 / CPU int8','baseRevision':BASE_REVISION,'files':[]}
 for file in sorted(output.rglob('*')):
  if file.is_file() and '__pycache__' not in file.parts and file!=marker:manifest['files'].append({'path':file.relative_to(output).as_posix(),'size':file.stat().st_size,'sha256':sha(file)})
 marker.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
 print(json.dumps({'ready':True,'runtime':RUNTIME_ID,'files':len(manifest['files']),'bytes':sum(f['size'] for f in manifest['files'])}),flush=True)
if __name__=='__main__':main()
