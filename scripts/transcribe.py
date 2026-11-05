"""Local CPU transcription. Audio stays on this computer; models download on first use."""
import argparse
import json
import sys
import io
import re
import wave
import os

def failure(exc, stage):
    detail = re.sub(r'https?://[^\s)\]\"\']+', '[service URL]', str(exc))
    detail = re.sub(r'(?i)(token|authorization|api[_-]?key|password)([=: ]+)[^\s,;]+', r'\1\2[redacted]', detail)
    message = str(exc).lower()
    if 'metadata_errors' in message:
        code, error = 'AUDIO_VERSION_MISMATCH', '音频组件版本不兼容：faster-whisper 1.2.1 无法使用 PyAV 19。请重新运行新版 setup-local-speech.ps1，安装兼容的 PyAV 18.1.0。'
    elif isinstance(exc, ModuleNotFoundError):
        code, error = 'DEPENDENCY_MISSING', '本地语音依赖未安装完整。请重新运行 setup-local-speech.ps1，或检查设置中的 Python 路径。'
    elif 'socksio' in message:
        code, error = 'PROXY_DEPENDENCY_MISSING', '当前网络代理需要 socksio 组件。请重新运行新版 setup-local-speech.ps1 安装代理支持。'
    elif stage == 'model':
        code, error = 'MODEL_LOAD_FAILED', 'Whisper 模型加载或下载失败。请检查模型下载网络和缓存文件；已保存的录音可在修复后重试。'
    elif stage == 'decode':
        code, error = 'AUDIO_DECODE_FAILED', '本地音频解码检查失败。请重新运行新版语音安装脚本，修复音频组件。'
    else:
        code, error = 'TRANSCRIPTION_FAILED', '本地音频转写失败。请回听录音确认音频有效，并运行“保存并检查语音配置”。'
    return {'error': error, 'code': code, 'detail': detail[:1200]}

def check_decoder():
    from faster_whisper.audio import decode_audio
    audio = io.BytesIO()
    with wave.open(audio, 'wb') as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(16000)
        stream.writeframes(bytes(3200))
    audio.seek(0)
    decode_audio(audio)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("audio", nargs='?')
    parser.add_argument("--model", default="base")
    parser.add_argument("--language", default="en")
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--offline', action='store_true', help='Never download models while checking/transcribing')
    args = parser.parse_args()
    stage = 'dependencies'
    try:
        if args.offline:
            os.environ['HF_HUB_OFFLINE'] = '1'
            os.environ['TRANSFORMERS_OFFLINE'] = '1'
        from faster_whisper import WhisperModel
        stage = 'decode'
        check_decoder()
        stage = 'model'
        if args.check:
            from huggingface_hub.errors import LocalEntryNotFoundError
            try:
                WhisperModel(args.model, device='cpu', compute_type='int8', local_files_only=True)
                result = {'ready': True, 'modelReady': True, 'message': f'本地语音依赖、音频解码与 {args.model} 模型均检查通过，可离线转写。'}
            except LocalEntryNotFoundError:
                result = {'ready': True, 'modelReady': False, 'message': f'语音依赖和音频解码检查通过；{args.model} 尚未缓存，首次转写需联网下载。'}
            print(json.dumps(result, ensure_ascii=True))
            return
        if not args.audio:
            parser.error('audio is required unless --check is used')
        from huggingface_hub.errors import LocalEntryNotFoundError
        try:
            model = WhisperModel(args.model, device='cpu', compute_type='int8', local_files_only=True)
        except LocalEntryNotFoundError:
            if args.offline:
                raise RuntimeError('Selected model is not installed. Download it from speech settings first.')
            model = WhisperModel(args.model, device='cpu', compute_type='int8')
        stage = 'audio'
        segments, info = model.transcribe(args.audio, language=None if args.language == "auto" else args.language, vad_filter=True, beam_size=5)
        result = [{"start": s.start, "end": s.end, "text": s.text.strip()} for s in segments]
        print(json.dumps({"text": " ".join(s["text"] for s in result), "segments": result, "language": info.language, "duration": info.duration}, ensure_ascii=True))
    except Exception as exc:
        print(json.dumps(failure(exc, stage), ensure_ascii=True))
        sys.exit(1)

if __name__ == "__main__":
    main()
