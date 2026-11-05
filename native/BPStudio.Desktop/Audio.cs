using BPStudio.Core;
using Microsoft.Win32;
using System.Text.Json;

namespace BPStudio.Desktop;

public sealed partial class MainWindow
{
    private void SetAudio(string path, bool temporary)
    {
        _player.Pause(); _player.Close(); _playing = false;
        _audioPath = path; _audioRecordId = null; _audioTemporary = temporary;
        _audioDuration = temporary ? _recorder.Clock.Elapsed.TotalSeconds : 0;
        _draft.Duration = _audioDuration;
        _player.Open(new Uri(path, UriKind.Absolute));
    }
    private void ClearAudio()
    {
        _player.Pause(); _player.Close(); _playing = false; _audioPath = null; _audioRecordId = null;
        _audioDuration = 0; _audioTemporary = false; _draft.Duration = 0;
    }
    private void EnsureAudioContext(JsonElement body)
    {
        if (_audioPath is null || _recorder.IsRecording) throw new InvalidOperationException("当前没有可回听的音频。");
        var reviewId = Text(body, "reviewId", "", 100);
        if (reviewId.Length > 0 && reviewId != _audioRecordId) throw new InvalidOperationException("这份历史复盘没有关联音频，请返回练习处理当前音频。");
    }
    private Task<object> ImportAudioAsync(JsonElement body)
    {
        RequireIdle();
        if (_audioPath is not null && !Flag(body, "confirmed")) return Task.FromResult(ConfirmRequest("替换音频来源", "已转写文字会保留，请先另存需要保留的临时录音。", "选择新音频"));
        var extensions = string.Join(';', AudioFiles.Formats.Keys.Select(e => "*" + e));
        var picker = new OpenFileDialog { Title = "导入练习音频", Filter = "音频文件|" + extensions, CheckFileExists = true, Multiselect = false };
        if (picker.ShowDialog(this) != true) return Task.FromResult<object>(new { canceled = true });
        AudioFiles.Validate(picker.FileName, _store.State.Settings.SpeechProvider);
        SetAudio(picker.FileName, false); SendSnapshot();
        return Task.FromResult<object>(new { imported = true });
    }
    private async Task<object> SaveAudioAsync(JsonElement body)
    {
        EnsureAudioContext(body);
        var audioPath = _audioPath!;
        var extension = Path.GetExtension(audioPath);
        var picker = new SaveFileDialog { Filter = "音频文件|*" + extension, FileName = Path.GetFileName(audioPath), DefaultExt = extension };
        if (picker.ShowDialog(this) != true) return new { canceled = true };
        if (!string.Equals(Path.GetFullPath(picker.FileName), Path.GetFullPath(audioPath), StringComparison.OrdinalIgnoreCase))
        {
            await using var source = File.OpenRead(audioPath); await using var target = File.Create(picker.FileName); await source.CopyToAsync(target);
        }
        return new { saved = true };
    }
    private async Task<object?> StartRecordingAsync(JsonElement body)
    {
        RequireIdle();
        if (_audioPath is not null)
        {
            if (!Flag(body, "confirmed")) return ConfirmRequest("重置录音", "当前音频来源将清空，已转写文字保留。重置后再次点击开始录音才会启用麦克风。", "重置");
            ClearAudio(); _store.State.Draft = Json.Clone(_draft); await _store.SaveAsync(); SendSnapshot(); return new { reset = true };
        }
        var devices = BPStudio.Native.RecordingSession.Devices();
        var device = (int)Number(body, "device", 0);
        if (device < 0 || device >= devices.Count) throw new InvalidOperationException("请选择可用的麦克风输入设备。");
        _recorder.Start(device, Path.Combine(_store.DirectoryPath, "temporary-audio"));
        _temporaryFiles.Add(_recorder.Path!); SendSnapshot(); return new { started = true };
    }
    private async Task StopRecordingAsync()
    {
        if (!_recorder.IsRecording || _stopping) return;
        _stopping = true;
        try
        {
            await _recorder.StopAsync();
            if (_recorder.Path is not null && File.Exists(_recorder.Path)) SetAudio(_recorder.Path, true);
            _store.State.Draft = Json.Clone(_draft); await _store.SaveAsync();
        }
        finally { _stopping = false; SendSnapshot(); }
    }
    private async Task<object?> TranscribeAsync(JsonElement body)
    {
        RequireIdle();
        if (_audioPath is null) throw new InvalidOperationException("请先录音或导入音频。");
        if (_draft.Transcript.Length > 0 && !Flag(body, "confirmed")) return ConfirmRequest("追加转写", "识别结果将追加到现有文字。若要替换，请先清空原文。", "继续转写");
        var path = _audioPath; var settings = Json.Clone(_store.State.Settings);
        AudioFiles.Validate(path, settings.SpeechProvider);
        return await RunJobAsync("正在转写音频…", async token =>
        {
            var result = settings.SpeechProvider == "local" ? await _speech.RunAsync(await LocalSpeechSettingsAsync(settings, token), path, token) : await _models.TranscribeAsync(settings, path, _store.GetKey("speechKey"), token);
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(result.Text)) throw new InvalidOperationException("未识别到有效语音，请回听音频并检查语言设置。");
            _draft.Transcript = string.IsNullOrWhiteSpace(_draft.Transcript) ? result.Text.Trim() : _draft.Transcript.Trim() + "\r\n\r\n" + result.Text.Trim();
            _audioDuration = result.Duration ?? _audioDuration; _draft.Duration = _audioDuration;
            _store.State.Draft = Json.Clone(_draft); await _store.SaveAsync();
            return new { transcribed = true };
        });
    }
}
