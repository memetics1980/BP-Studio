using BPStudio.Core;
using Microsoft.Win32;
using System.Text.Json;

namespace BPStudio.Desktop;

public sealed partial class MainWindow
{
    private object ConfirmRequest(string title, string message, string primary = "继续") => new { confirmation = new { title, message, primary } };
    private void RequireIdle() { if (_job is not null || _recorder.IsRecording) throw new InvalidOperationException("请先结束录音或取消当前任务。"); }
    private async Task<object?> DispatchAsync(string command, JsonElement body)
    {
        if (!_dataReady) throw new InvalidOperationException("数据尚未载入，不能执行操作。");
        switch (command)
        {
            case "load":
                _ready = true; _boot.Visibility = System.Windows.Visibility.Collapsed;
                ScheduleDefaultSpeech();
                if (Environment.GetEnvironmentVariable("BP_NATIVE_TEST") == "1")
                    await File.WriteAllTextAsync(Path.Combine(_store.DirectoryPath, "ready.json"), JsonSerializer.Serialize(new { ready = true, wpf = true, webview = true, react = true, records = _store.State.Records.Count, motions = _store.State.CustomMotions.Count, theme = _theme, dataPath = _store.DataPath, dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX, perMonitorV2 = IsPerMonitorV2(), interfaceScale = _web.ZoomFactor, logicalWidth = _web.ActualWidth, logicalHeight = _web.ActualHeight }, Json.Options));
                return Snapshot();
            case "draft.save":
                await SaveDraftAsync(body); return new { saved = true };
            case "practice.new":
                RequireIdle();
                if ((_draft.Transcript.Length > 0 || _draft.Context.Length > 0 || (_draft.PreviousSpeeches ?? []).Any(s => !string.IsNullOrWhiteSpace(s.Text)) || _audioPath is not null) && !Flag(body, "confirmed")) return ConfirmRequest("开始新的练习", "当前草稿、前序发言和临时音频将清空。已经生成的复盘保留；需要保留的录音请先另存。", "新练习");
                var motionTitle = Text(body, "motion", _draft.Motion, 2000).Trim();
                if (motionTitle.Length < 8) throw new InvalidOperationException("请填写完整辩题。");
                ClearAudio(); _draft = new Practice { Motion = motionTitle, Role = _draft.Role, Target = _draft.Target }; _review = null;
                _store.State.Draft = Json.Clone(_draft); await _store.SaveAsync(); SendSnapshot(); return new { done = true };
            case "practice.motion":
                RequireIdle(); var title = Text(body, "motion", "", 2000).Trim(); if (title.Length < 8) throw new InvalidOperationException("请填写至少 8 个字符的完整辩题。");
                _draft.Motion = title; _store.State.Draft = Json.Clone(_draft); await _store.SaveAsync(); SendSnapshot(); return new { saved = true };
            case "prefs.save":
                var previous = Json.Clone(_store.State.NativeUi);
                try
                {
                    var p = _store.State.NativeUi;
                    var theme = Text(body, "theme", p.Theme, 10);
                    if (theme is not ("light" or "dark" or "system")) throw new InvalidOperationException("主题选项无效。");
                    p.Theme = theme; p.ReaderFontSize = Math.Clamp(Number(body, "readerFontSize", p.ReaderFontSize), 14, 22); p.CompareRatio = Math.Clamp(Number(body, "compareRatio", p.CompareRatio), .25, .7);
                    p.InterfaceScale = Number(body, "interfaceScale", p.InterfaceScale);
                    var librarySize = Number(body, "libraryPageSize", p.LibraryPageSize); var historySize = Number(body, "historyPageSize", p.HistoryPageSize);
                    if (librarySize is not (10 or 20 or 50) || historySize is not (10 or 20 or 50)) throw new InvalidOperationException("请选择 10、20 或 50 条每页。");
                    p.LibraryPageSize = (int)librarySize; p.HistoryPageSize = (int)historySize;
                    await _store.SaveAsync(); ApplyTheme(); ApplyInterfaceScale(); SendSnapshot(); return new { saved = true };
                }
                catch { _store.State.NativeUi = previous; ApplyTheme(); ApplyInterfaceScale(); throw; }
            case "settings.save":
                RequireIdle(); await SaveSettingsAsync(body); SendSnapshot();
                if (UsesManagedSpeech(_store.State.Settings) && _store.State.Settings.WhisperModel == "base")
                    await RunJobAsync("正在准备内置 Base 语音识别…", async token => { await PrepareManagedSpeechAsync("base", false, token); return new { ready = true }; });
                return new { saved = true };
            case "settings.probeModel":
                return await RunJobAsync("正在检查模型连接…", async token => new { message = await _models.ProbeAsync(Json.Clone(_store.State.Settings), _store.State.Settings.Provider == "api" ? _store.GetKey("apiKey") : "", token) });
            case "settings.probeSpeech":
                if (_store.State.Settings.SpeechProvider == "api") return new { message = "已选择语音 API；实际接口将在转写时验证。" };
                return await RunJobAsync("正在检查本地语音与模型…", async token => new { message = (await _speech.RunAsync(await LocalSpeechSettingsAsync(Json.Clone(_store.State.Settings), token), null, token)).Message });
            case "speech.status": return _managedSpeech.Status(_store.State.Settings);
            case "speech.selectModel": return await SelectSpeechModelAsync(body);
            case "motion.save":
                RequireIdle();
                var input = JsonSerializer.Deserialize<Motion>(body.GetProperty("motion").GetRawText(), Json.Options) ?? throw new InvalidOperationException("辩题数据无效。");
                Motion updated;
                if (input.Id.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(input.Key))
                    updated = Json.Clone(_store.State.CustomMotions.FirstOrDefault(m => m.Key == input.Key) ?? throw new InvalidOperationException("只能编辑现有的自建辩题。"));
                else if (input.Id.ValueKind is JsonValueKind.Number) throw new InvalidOperationException("内置辩题不能直接修改，请添加为自己的辩题。");
                else updated = new Motion();
                updated.Title = input.Title; updated.Zh = input.Zh; updated.Category = input.Category; updated.Level = input.Level; updated.Tags = input.Tags;
                await _store.SaveMotionAsync(updated, _catalog); SendSnapshot(); return new { saved = true, key = updated.Key };
            case "motion.delete":
                RequireIdle();
                var motionKey = Text(body, "key", "", 100); var targetMotion = _store.State.CustomMotions.FirstOrDefault(m => m.Key == motionKey) ?? throw new InvalidOperationException("找不到这道自建辩题。");
                if (!Flag(body, "confirmed")) return ConfirmRequest("删除自建辩题", "已有草稿和复盘记录会保留。确认删除这道自建辩题？", "删除");
                _store.State.CustomMotions.Remove(targetMotion); await _store.SaveAsync(); SendSnapshot(); return new { deleted = true };
            case "review.open":
                _review = _store.State.Records.FirstOrDefault(r => r.Id == Text(body, "id", "", 100)) ?? throw new InvalidOperationException("复盘记录不存在。");
                SendSnapshot(); return new { opened = true };
            case "review.delete":
                var recordId = Text(body, "id", "", 100); var record = _store.State.Records.FirstOrDefault(r => r.Id == recordId) ?? throw new InvalidOperationException("复盘记录不存在。");
                if (!Flag(body, "confirmed")) return ConfirmRequest("删除复盘", "删除后无法恢复，当前练习草稿会保留。", "删除");
                _store.State.Records.Remove(record); if (_review?.Id == recordId) _review = null;
                await _store.SaveAsync(); SendSnapshot(); return new { deleted = true };
            case "review.export":
                var exportId = Text(body, "id", _review?.Id ?? "", 100);
                var exportRecord = _store.State.Records.FirstOrDefault(r => r.Id == exportId) ?? throw new InvalidOperationException("请先生成或打开一份复盘。");
                return await ExportReportAsync(exportRecord, Text(body, "format", "", 10));
            case "feedback.generate":
                RequireIdle(); var practice = Json.Clone(_draft); var settings = Json.Clone(_store.State.Settings); Validation.Practice(practice, _catalog);
                _store.State.Draft = Json.Clone(_draft); await _store.SaveAsync();
                return await RunJobAsync("AI 教练正在复盘…", async token =>
                {
                    var item = await _models.FeedbackAsync(settings, practice, settings.Provider == "api" ? _store.GetKey("apiKey") : "", token);
                    token.ThrowIfCancellationRequested();
                    _store.State.Records.Insert(0, item);
                    _review = item; _audioRecordId = _audioPath is not null ? item.Id : null; await _store.SaveAsync(); return new { generated = true };
                });
            case "job.cancel": _job?.Cancel(); return new { canceled = true };
            case "audio.import": return await ImportAudioAsync(body);
            case "audio.save": return await SaveAudioAsync(body);
            case "audio.play":
                EnsureAudioContext(body); if (_playing) _player.Pause(); else _player.Play(); _playing = !_playing; return new { playing = _playing };
            case "audio.seek":
                EnsureAudioContext(body); _player.Position = TimeSpan.FromSeconds(Math.Clamp(Number(body, "position", 0), 0, Math.Max(0, _audioDuration))); return new { done = true };
            case "audio.volume": _player.Volume = Math.Clamp(Number(body, "volume", .8), 0, 1); return new { done = true };
            case "audio.transcribe": return await TranscribeAsync(body);
            case "record.start": return await StartRecordingAsync(body);
            case "record.pause": if (_recorder.IsRecording) _recorder.TogglePause(); return new { done = true };
            case "record.stop": await StopRecordingAsync(); return new { done = true };
            case "app.closeReady": _closeAcknowledged = true; return new { done = true };
            case "app.cancelClose": _closePending = false; return new { done = true };
            case "app.close":
                if ((_recorder.IsRecording || _job is not null || (_audioTemporary && _audioPath is not null)) && !Flag(body, "confirmed")) return ConfirmRequest("退出前确认", "仍有临时录音或任务。退出将结束任务并删除未另存的临时录音，草稿与复盘会保存。", "退出");
                await CloseAsync(); return null;
            default: throw new InvalidOperationException("不支持的操作。");
        }
    }

    private async Task SaveDraftAsync(JsonElement body)
    {
        var input = JsonSerializer.Deserialize<Practice>(body.GetRawText(), Json.Options) ?? throw new InvalidOperationException("草稿无效。");
        if (!_catalog.Roles.Any(r => r.Id == input.Role) || input.Target is not (180 or 300 or 420) || input.Motion.Length is < 8 or > 2000 || input.Transcript.Length > 50000 || input.Context.Length > 30000)
            throw new InvalidOperationException("草稿内容或训练选项无效。");
        Validation.Previous(input);
        input.Duration = _audioPath is not null ? _audioDuration : _draft.Duration;
        input.Extra = _draft.Extra;
        _draft = input; _store.State.Draft = Json.Clone(input); await _store.SaveAsync();
    }
    private async Task SaveSettingsAsync(JsonElement body)
    {
        var settings = JsonSerializer.Deserialize<ModelSettings>(body.GetProperty("settings").GetRawText(), Json.Options) ?? throw new InvalidOperationException("模型设置无效。");
        Validation.Settings(settings);
        var apiKey = Text(body, "apiKey", "", 4000); var speechKey = Text(body, "speechKey", "", 4000);
        var oldSettings = _store.State.Settings; var oldSecrets = new Dictionary<string, string>(_store.State.Secrets);
        try
        {
            settings.Extra = oldSettings.Extra;
            _store.SetKey("apiKey", apiKey, Flag(body, "removeApiKey")); _store.SetKey("speechKey", speechKey, Flag(body, "removeSpeechKey"));
            _store.State.Settings = settings; await _store.SaveAsync();
        }
        catch { _store.State.Settings = oldSettings; _store.State.Secrets = oldSecrets; throw; }
    }
    private async Task<object?> RunJobAsync(string label, Func<CancellationToken, Task<object>> work)
    {
        RequireIdle();
        using var cancellation = new CancellationTokenSource(); _job = cancellation; _jobLabel = label; SendSnapshot();
        try { return await work(cancellation.Token); }
        finally { _job = null; _jobLabel = null; _speechProgress = null; SendSnapshot(); }
    }
}
