using BPStudio.Core;
using System.Net.Http;
using System.Text.Json;

namespace BPStudio.Desktop;

public sealed partial class MainWindow
{
    private readonly ManagedSpeech _managedSpeech = new();
    private SpeechProgress? _speechProgress;
    private bool _autoSpeechAttempted;
    private static bool UsesManagedSpeech(ModelSettings settings) => settings.SpeechProvider == "local" && string.IsNullOrWhiteSpace(settings.PythonPath);
    private IProgress<SpeechProgress> SpeechReporter() => new Progress<SpeechProgress>(progress =>
    {
        if (_job is null || _closingFinal) return;
        _speechProgress = progress; _jobLabel = progress.Message;
    });
    private async Task PrepareManagedSpeechAsync(string model, bool download, CancellationToken token)
    {
        try { await _managedSpeech.EnsureAsync(model, download, SpeechReporter(), token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new InvalidOperationException("模型准备或下载超时，已完成的下载会保留，请重试。"); }
        catch (HttpRequestException) { throw new InvalidOperationException("无法下载语音模型，请检查网络连接后重试。已完成的文件与可续传部分会保留。"); }
        catch (IOException) { throw new InvalidOperationException("语音文件写入失败，请检查磁盘空间或目录权限后重试。"); }
    }
    private void ScheduleDefaultSpeech()
    {
        if (_autoSpeechAttempted) return; _autoSpeechAttempted = true;
        var settings = _store.State.Settings;
        if (!UsesManagedSpeech(settings) || settings.WhisperModel != "base" || (_managedSpeech.RuntimeReady && _managedSpeech.ModelPath("base") is not null)) return;
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (_job is not null || _recorder.IsRecording || _closingFinal) return;
            try { await RunJobAsync("正在准备内置 Base 语音识别…", async token => { await PrepareManagedSpeechAsync("base", false, token); return new { ready = true }; }); }
            catch (OperationCanceledException) { Notify("语音准备已取消，可以在语音设置中重试。"); }
            catch (Exception error) { Notify(error.Message, true); }
        }));
    }
    private async Task<object?> SelectSpeechModelAsync(JsonElement body)
    {
        RequireIdle(); var model = ManagedSpeech.NormalizeModel(Text(body, "model", "base", 100));
        return await RunJobAsync("正在准备语音模型…", async token =>
        {
            await PrepareManagedSpeechAsync(model, Flag(body, "download"), token); token.ThrowIfCancellationRequested();
            var old = Json.Clone(_store.State.Settings);
            try
            {
                _store.State.Settings.WhisperModel = model; _store.State.Settings.SpeechProvider = "local"; _store.State.Settings.PythonPath = "";
                await _store.SaveAsync();
            }
            catch { _store.State.Settings = old; throw; }
            return new { ready = true, model };
        });
    }
    private async Task<ModelSettings> LocalSpeechSettingsAsync(ModelSettings settings, CancellationToken token)
    {
        if (!UsesManagedSpeech(settings)) return settings;
        await PrepareManagedSpeechAsync(settings.WhisperModel, false, token);
        return _managedSpeech.EffectiveSettings(settings);
    }
}
