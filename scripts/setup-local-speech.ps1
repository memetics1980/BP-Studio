$ErrorActionPreference = 'Stop'
$speechRoot = Join-Path $env:LOCALAPPDATA 'BPStudio\speech'
New-Item -ItemType Directory -Force -Path $speechRoot | Out-Null
python -m venv (Join-Path $speechRoot '.venv')
if ($LASTEXITCODE -ne 0) { throw 'Python 3.10+ is required. Install Python and run this script again.' }
$speechPython = Join-Path $speechRoot '.venv\Scripts\python.exe'
& $speechPython -m pip install --upgrade pip
& $speechPython -m pip install 'faster-whisper==1.2.1' 'av==18.1.0' 'httpx[socks]'
if ($LASTEXITCODE -ne 0) { throw 'Installation failed. Please check your network connection.' }
& $speechPython -c "import io,wave; from faster_whisper.audio import decode_audio; b=io.BytesIO(); w=wave.open(b,'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(16000); w.writeframes(bytes(3200)); w.close(); b.seek(0); decode_audio(b); print('Audio decoding check: OK')"
if ($LASTEXITCODE -ne 0) { throw 'The audio decoding check failed. Please keep this output for troubleshooting.' }
Write-Host "Local speech is ready. Python path: $speechPython"
Write-Host 'The selected Whisper model will download on the first transcription.'
