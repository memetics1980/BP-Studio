"""Explicitly requested offline desktop ZIP; never run implicitly during normal builds."""
import hashlib,json,pathlib,re,shutil,zipfile,xml.etree.ElementTree as ET
ROOT=pathlib.Path(__file__).resolve().parents[1]
def digest(path):
 with path.open('rb') as source:return hashlib.file_digest(source,'sha256').hexdigest()
def main():
 version=ET.parse(ROOT/'native/BPStudio.Desktop/BPStudio.Desktop.csproj').findtext('.//Version')
 folder=ROOT/f'release/BP-Studio-{version}-Desktop-Windows-x64'
 required=['BPStudio.exe','BPStudio.dll','coreclr.dll','hostfxr.dll','WebView2Loader.dll','wwwroot/index.html','speech/bundle.json','speech/engine/python.exe','speech/models/base/model.bin','webview-runtime/msedgewebview2.exe','webview-runtime/bundle.json']
 for name in required:
  if not (folder/name).is_file():raise RuntimeError('Incomplete offline release: '+name)
 speech=json.loads((folder/'speech/bundle.json').read_text('utf-8'))
 for item in speech['files']:
  path=folder/'speech'/item['path']
  if not path.is_file() or path.stat().st_size!=item['size'] or digest(path)!=item['sha256']:raise RuntimeError('Speech payload checksum failed: '+item['path'])
 webview=json.loads((folder/'webview-runtime/bundle.json').read_text('utf-8'))
 for name,sha in webview['files'].items():
  if not (folder/'webview-runtime'/name).is_file() or digest(folder/'webview-runtime'/name)!=sha:raise RuntimeError('WebView2 payload checksum failed: '+name)
 for name in ['首次使用说明.txt','桌面工作台使用说明.md','桌面工作台验证记录.md','THIRD_PARTY_SPEECH.md','THIRD_PARTY_DESKTOP.md']:
  shutil.copy2(ROOT/name,folder/name)
 licenses=folder/'licenses';licenses.mkdir(exist_ok=True)
 sources={
  'WebView2-SDK-LICENSE.txt':ROOT/'.cache/nuget/microsoft.web.webview2/1.0.3179.45/LICENSE.txt',
  'WebView2-SDK-NOTICE.txt':ROOT/'.cache/nuget/microsoft.web.webview2/1.0.3179.45/NOTICE.txt',
  'NAudio-LICENSE.txt':ROOT/'.cache/nuget/naudio/2.2.1/license.txt',
  'DotNet-LICENSE.txt':ROOT/'.cache/nuget/microsoft.netcore.app.runtime.win-x64/8.0.33/LICENSE.TXT',
  'DotNet-THIRD-PARTY-NOTICES.txt':ROOT/'.cache/nuget/microsoft.netcore.app.runtime.win-x64/8.0.33/THIRD-PARTY-NOTICES.TXT',
  'React-LICENSE.txt':ROOT/'ui/node_modules/react/LICENSE',
  'ReactDOM-LICENSE.txt':ROOT/'ui/node_modules/react-dom/LICENSE',
  'Lucide-LICENSE.txt':ROOT/'ui/node_modules/lucide-react/LICENSE',
  'WebView2-Runtime-LICENSE.html':ROOT/'assets/licenses/WebView2-Runtime-LICENSE.html',
  'WebView2-Runtime-LICENSE.txt':ROOT/'assets/licenses/WebView2-Runtime-LICENSE.txt',
 }
 for name,path in sources.items():
  if not path.is_file():raise RuntimeError('Missing redistribution license: '+str(path))
  shutil.copy2(path,licenses/name)
 files=[]
 forbidden={'bp-studio.json','local state','ready.json','startup-error.log','.env'}
 for path in sorted(folder.rglob('*')):
  if not path.is_file():continue
  rel=path.relative_to(folder)
  if path.name.lower() in forbidden or any(p.lower() in {'__pycache__','webview','test-results','.git'} for p in rel.parts):raise RuntimeError('User/cache/test data must not be distributed: '+str(rel))
  if path.suffix.lower() in {'.pdb','.pyc','.partial'} or path.name=='package-files.sha256':continue
  files.append(path)
 checksums=folder/'package-files.sha256'
 checksums.write_text(''.join(digest(p)+'  '+p.relative_to(folder).as_posix()+'\n' for p in files),encoding='utf-8');files.append(checksums)
 archive=folder.parent/(folder.name+'.zip');temporary=archive.with_suffix('.zip.partial')
 print(f'Creating {archive.name}: {len(files)} files, {sum(p.stat().st_size for p in files)/1024**2:.1f} MiB unpacked',flush=True)
 with zipfile.ZipFile(temporary,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6,allowZip64=True) as output:
  for path in files:output.write(path,folder.name+'/'+path.relative_to(folder).as_posix())
 with zipfile.ZipFile(temporary) as check:
  invalid=check.testzip()
  if invalid:raise RuntimeError('ZIP integrity failed: '+invalid)
 temporary.replace(archive);sha=digest(archive)
 archive.with_suffix('.zip.sha256').write_text(sha+'  '+archive.name+'\n',encoding='utf-8')
 print(f'ZIP verified: {archive.stat().st_size/1024**2:.1f} MiB; SHA256 {sha}',flush=True)
if __name__=='__main__':main()
