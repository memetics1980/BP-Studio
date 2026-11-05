"""Prepare the pinned official x64 fixed WebView2 runtime for offline distribution."""
import hashlib,json,pathlib,shutil,subprocess,urllib.request,uuid
ROOT=pathlib.Path(__file__).resolve().parents[1]
CACHE=ROOT/'.cache/webview-bundle'
VERSION='156.0.4314.58'
URL='https://msedge.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/b7919cc9-680d-4253-bf32-732988e68aa6/Microsoft.WebView2.FixedVersionRuntime.156.0.4314.58.x64.cab'
SHA256='857dcfd6f7035508a29645fa2978c3a6a841f220df77cc366d318894e19f4792'
def digest(path):
 with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def prepare():
 CACHE.mkdir(parents=True,exist_ok=True);payload=CACHE/'payload';manifest=payload/'bundle.json'
 if manifest.is_file():
  data=json.loads(manifest.read_text('utf-8'))
  if data.get('version')==VERSION and data.get('archiveSha256')==SHA256 and all((payload/n).is_file() and digest(payload/n)==h for n,h in data['files'].items()):
   print('Pinned WebView2 payload verified:',payload);return
 archive=CACHE/f'Microsoft.WebView2.FixedVersionRuntime.{VERSION}.x64.cab'
 if not archive.exists():
  partial=archive.with_suffix('.partial')
  with urllib.request.urlopen(URL,timeout=120) as source,partial.open('wb') as target:shutil.copyfileobj(source,target,1024*1024)
  if digest(partial)!=SHA256:raise RuntimeError('Official WebView2 cabinet checksum mismatch')
  partial.replace(archive)
 if digest(archive)!=SHA256:raise RuntimeError('WebView2 cabinet checksum mismatch')
 stage=CACHE/('unpack-'+uuid.uuid4().hex[:8]);stage.mkdir()
 result=subprocess.run(['expand.exe',str(archive),'-F:*',str(stage)],capture_output=True)
 if result.returncode:raise RuntimeError('WebView2 extraction failed: '+result.stderr.decode(errors='replace')[-600:])
 candidates=list(stage.rglob('msedgewebview2.exe'))
 if len(candidates)!=1:raise RuntimeError('WebView2 cabinet layout is not recognized')
 browser=candidates[0];quoted=str(browser).replace("'","''")
 command=f"$ErrorActionPreference='Stop'; $s=Get-AuthenticodeSignature -LiteralPath '{quoted}'; @{{status=[string]$s.Status;subject=$s.SignerCertificate.Subject}} | ConvertTo-Json -Compress"
 result=subprocess.run([shutil.which('pwsh.exe') or 'powershell.exe','-NoProfile','-Command',command],capture_output=True,text=True,check=True)
 signature=json.loads(result.stdout)
 if signature['status']!='Valid' or 'Microsoft Corporation' not in signature['subject']:raise RuntimeError('WebView2 Microsoft signature validation failed')
 if payload.exists():raise RuntimeError('Existing WebView2 payload failed verification; inspect it before replacing it')
 shutil.copytree(browser.parent,payload)
 files={p.relative_to(payload).as_posix():digest(p) for p in sorted(payload.rglob('*')) if p.is_file()}
 manifest.write_text(json.dumps({'version':VERSION,'architecture':'x64','source':URL,'archiveSha256':SHA256,'signature':signature,'files':files},indent=2),encoding='utf-8')
 print(f'WebView2 {VERSION} prepared and signed; {len(files)} files, {sum(p.stat().st_size for p in payload.rglob("*") if p.is_file())/1024**2:.1f} MiB')
if __name__=='__main__':prepare()
