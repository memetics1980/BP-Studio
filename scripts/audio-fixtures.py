"""Encode the existing synthetic English fixture into genuine common audio formats."""
from pathlib import Path
import shutil
import json
import av

root = Path(__file__).resolve().parents[1]
folder = root / 'test-results/audio-fixtures'
folder.mkdir(parents=True, exist_ok=True)
source = root / 'test-results/speech-smoke.wav'
shutil.copy2(source, folder / '练习音频.wav')
shutil.copy2(source, folder / '练习音频.wave')
for extension, codec, rate in [('mp3', 'libmp3lame', 24000), ('m4a', 'aac', 24000), ('flac', 'flac', 24000), ('ogg', 'libopus', 48000)]:
    with av.open(str(source)) as inp, av.open(str(folder / f'练习音频.{extension}'), 'w') as out:
        stream = out.add_stream(codec, rate=rate)
        stream.layout = 'mono'
        if extension != 'flac':
            stream.bit_rate = 96000
        resampler = av.AudioResampler(format=av.Codec(codec, 'w').audio_formats[0].name, layout='mono', rate=rate)
        for frame in inp.decode(audio=0):
            for converted in resampler.resample(frame):
                for packet in stream.encode(converted):
                    out.mux(packet)
        for converted in resampler.resample(None):
            for packet in stream.encode(converted):
                out.mux(packet)
        for packet in stream.encode(None):
            out.mux(packet)
print(json.dumps({p.name: p.stat().st_size for p in folder.iterdir()}, ensure_ascii=True))
