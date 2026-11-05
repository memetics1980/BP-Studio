# 内置语音资源来源

内置语音使用 CPU int8 推理；只随程序附带 Base 权重。运行环境、依赖、模型权重各自保留原许可，不因 BP Studio 分发而变更。

- Python 官方 Windows embeddable package：[Python 3.14.10](https://www.python.org/downloads/release/python-31410/)。许可位于 `speech/engine/LICENSE.txt`。
- faster-whisper：[SYSTRAN/faster-whisper](https://github.com/SYSTRAN/faster-whisper)，MIT。CTranslate2：[OpenNMT/CTranslate2](https://github.com/OpenNMT/CTranslate2)，MIT。
- Base 转换权重：[Systran/faster-whisper-base](https://huggingface.co/Systran/faster-whisper-base)，发布版本固定为 `ebe41f70d5b6dfa9166e2c581c45c9c0cfc57b66`，MIT。
- 原始 Whisper：[openai/whisper](https://github.com/openai/whisper)，MIT。许可副本位于 `speech/models/base/WHISPER-LICENSE.txt`，模型卡位于同目录。
- PyAV、NumPy、ONNX Runtime、Hugging Face 及其他运行依赖的许可保留在各包的 `*.dist-info/licenses` 或 `*.dist-info` 目录。C++ 运行库分发清单位于 `msvc_runtime-*.dist-info/licenses/LICENSE`；二进制保持原样。

`speech/wheel-lock.json` 记录轮子包的校验摘要；`speech/bundle.json` 记录所有分发资源的长度及 SHA-256。发行目录包含这些说明及许可，复制程序时请一并保留。
