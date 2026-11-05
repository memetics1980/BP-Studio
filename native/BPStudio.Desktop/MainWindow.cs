using BPStudio.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace BPStudio.Desktop;

public sealed partial class MainWindow : Window
{
    public const string WindowTitle = "BP Studio · 桌面工作台";
    private const string Origin = "https://bpstudio.local";
    private readonly Store _store = new();
    private readonly Catalog _catalog = Catalog.Load();
    private readonly ModelService _models;
    private readonly SpeechService _speech = new();
    private readonly BPStudio.Native.RecordingSession _recorder = new();
    private readonly MediaPlayer _player = new();
    private readonly WebView2 _web = new();
    private readonly Grid _root = new();
    private readonly TextBlock _boot = new() { Text = "正在打开 BP Studio…", Margin = new Thickness(28), FontSize = 16, TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private Practice _draft = new();
    private PracticeRecord? _review;
    private CancellationTokenSource? _job;
    private string? _jobLabel, _audioPath, _audioRecordId;
    private double _audioDuration;
    private bool _audioTemporary, _playing, _stopping, _ready, _dataReady, _closingFinal, _closePending, _closeAcknowledged;
    private readonly HashSet<string> _temporaryFiles = [];
    private string _theme = "light";

    public MainWindow()
    {
        _models = new(_catalog);
        Title = WindowTitle; Width = Math.Min(1380, SystemParameters.WorkArea.Width); Height = Math.Min(900, SystemParameters.WorkArea.Height); MinWidth = 640; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(Path.Combine(AppContext.BaseDirectory, "icon.ico"))); } catch { }
        Content = _root; _root.Children.Add(_web); _root.Children.Add(_boot);
        Loaded += async (_, _) => { FitCurrentMonitor(); await InitializeAsync(); };
        Closing += OnClosing;
        Closed += (_, _) => DisposeResources();
        SystemEvents.UserPreferenceChanged += SystemPreferencesChanged;
        _timer.Tick += async (_, _) =>
        {
            if (_closingFinal) return;
            if (_recorder.IsRecording && !_stopping && _recorder.Clock.Elapsed.TotalSeconds >= _draft.Target + 30) await StopRecordingAsync();
            if (_ready) Send("runtime", RuntimeState());
        };
        _recorder.DeviceError += message => Dispatcher.BeginInvoke(new Action(async () => { await StopRecordingAsync(); Notify(message, true); }));
        _player.MediaOpened += (_, _) =>
        {
            if (_audioPath is null) return;
            if (_player.NaturalDuration.HasTimeSpan) _audioDuration = _player.NaturalDuration.TimeSpan.TotalSeconds;
            if (!_recorder.IsRecording) _draft.Duration = _audioDuration;
            SendSnapshot();
        };
        _player.MediaEnded += (_, _) => { _playing = false; _player.Position = TimeSpan.Zero; };
        _player.MediaFailed += (_, _) => { _playing = false; Notify("系统播放器无法回听此编码，仍可尝试本地转写。", true); };
        _player.Volume = .8;
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (File.Exists(_store.DataPath))
            {
                var backup = Path.Combine(_store.DirectoryPath, "backups", "before-desktop-0.3.0.json");
                if (!File.Exists(backup)) { Directory.CreateDirectory(Path.GetDirectoryName(backup)!); File.Copy(_store.DataPath, backup); }
            }
            var legacy = Environment.GetEnvironmentVariable("BP_NATIVE_LEGACY");
            if (legacy is null && Environment.GetEnvironmentVariable("BP_NATIVE_PROFILE") is not null) legacy = Path.Combine(_store.DirectoryPath, "no-legacy");
            await _store.LoadAsync(legacy); _dataReady = true;
            _draft = Json.Clone(_store.State.Draft ?? new Practice { Motion = _catalog.Motions[0].Title });
            if (!_catalog.Roles.Any(r => r.Id == _draft.Role)) _draft.Role = "PM";
            if (_draft.Target is not (180 or 300 or 420)) _draft.Target = 420;
            _review = _store.State.Records.FirstOrDefault();
            ApplyTheme();
            var options = new CoreWebView2EnvironmentOptions();
            if (int.TryParse(Environment.GetEnvironmentVariable("BP_DESKTOP_TEST_PORT"), out var port) && port is > 1024 and < 65536)
                options.AdditionalBrowserArguments = $"--remote-debugging-port={port} --remote-debugging-address=127.0.0.1";
            var environment = await CoreWebView2Environment.CreateAsync(BundledWebView.Resolve(), Path.Combine(_store.DirectoryPath, "webview"), options);
            await _web.EnsureCoreWebView2Async(environment);
            var core = _web.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.AreDevToolsEnabled = Environment.GetEnvironmentVariable("BP_DESKTOP_TEST_PORT") is not null;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            ApplyInterfaceScale();
            core.SetVirtualHostNameToFolderMapping("bpstudio.local", Path.Combine(AppContext.BaseDirectory, "wwwroot"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += (_, e) => { if (!IsTrusted(e.Uri)) e.Cancel = true; };
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.WebMessageReceived += Receive;
            core.ProcessFailed += (_, _) => { _boot.Text = "界面进程意外结束。请关闭并重新打开程序，已保存的数据仍在原目录中。"; _boot.Visibility = Visibility.Visible; };
            core.NavigationCompleted += (_, e) => { if (!e.IsSuccess) { _boot.Text = "界面文件未能载入，请保留完整程序目录后重试。"; } };
            ApplyTheme();
            core.Navigate(Origin + "/index.html");
            _timer.Start();
        }
        catch (Exception e)
        {
            Program.Log(_store.DirectoryPath, e);
            _boot.Text = e is WebView2RuntimeNotFoundException ? "未找到 Microsoft Edge WebView2 运行时。请安装微软 WebView2 Evergreen 运行时后重新打开。" : "启动未完成：" + e.Message;
        }
    }

    private static bool IsTrusted(string uri) => Uri.TryCreate(uri, UriKind.Absolute, out var value) && value.Scheme == "https" && value.Host == "bpstudio.local" && value.IsDefaultPort;
    private async void Receive(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!IsTrusted(e.Source)) return;
        string? id = null;
        try
        {
            if (e.WebMessageAsJson.Length > 200000) throw new InvalidOperationException("请求内容过长。");
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var request = document.RootElement;
            id = Text(request, "id", "", 80); if (id.Length < 1) return;
            var command = Text(request, "command", "", 50);
            var body = request.TryGetProperty("body", out var payload) ? payload : JsonSerializer.SerializeToElement(new { });
            var result = await DispatchAsync(command, body);
            Reply(new { id, ok = true, result });
        }
        catch (OperationCanceledException) { Reply(new { id, ok = false, error = "任务已取消，草稿和原音频已保留。" }); }
        catch (Exception error) { Reply(new { id, ok = false, error = error is JsonException or KeyNotFoundException or ArgumentException ? "请求参数无效。" : error.Message }); }
    }
    private void Reply(object message)
    {
        if (_closingFinal || _web.CoreWebView2 is null || !IsTrusted(_web.Source?.AbsoluteUri ?? "")) return;
        try { _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, Json.Options)); } catch (InvalidOperationException) { }
    }
    private void Send(string type, object? data) => Reply(new { type, data });
    private void SendSnapshot() { if (_ready) Send("snapshot", Snapshot()); }
    private void Notify(string text, bool error = false) => Send("notice", new { text, error });
    private object AudioState() => new { name = _audioPath is null ? "" : Path.GetFileName(_audioPath), available = _audioPath is not null, duration = _audioDuration, recordId = _audioRecordId, temporary = _audioTemporary };
    private object RuntimeState() => new { recording = _recorder.IsRecording, paused = _recorder.Paused, elapsed = _recorder.Clock.Elapsed.TotalSeconds, level = _recorder.Level, position = _audioPath is null ? 0 : _player.Position.TotalSeconds, playing = _playing, duration = _audioDuration, busy = _jobLabel, audio = AudioState(), speechProgress = _speechProgress };
    private object Snapshot()
    {
        var s = _store.State.Settings;
        return new
        {
            version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.3.6", draft = _draft,
            settings = new { s.Provider, s.BaseUrl, s.Model, s.SpeechProvider, s.SpeechUrl, s.SpeechModel, s.WhisperModel, s.PythonPath, s.SpeechLanguage, s.FeedbackLanguage },
            preferences = _store.State.NativeUi, theme = _theme, roles = _catalog.Roles,
            motions = _store.State.CustomMotions.Concat(_catalog.Motions).Select(m => new { m.Id, key = m.Key, m.Title, m.Zh, m.Category, m.Level, m.Tags, m.CreatedAt, isCustom = m.IsCustom }),
            history = _store.State.Records.Select(r => new { r.Id, r.CreatedAt, r.Motion, r.Role, r.Model, summary = r.Feedback.Summary[..Math.Min(220, r.Feedback.Summary.Length)] }),
            review = _review, devices = BPStudio.Native.RecordingSession.Devices(),
            secrets = new { apiKey = _store.State.Secrets.GetValueOrDefault("apiKey", "").Length > 0, speechKey = _store.State.Secrets.GetValueOrDefault("speechKey", "").Length > 0 },
            runtime = RuntimeState(), speech = _managedSpeech.Status(s), notice = _store.Notice, sample = _catalog.Sample
        };
    }
    private void ApplyTheme()
    {
        var preference = _store.State.NativeUi.Theme;
        var systemLight = (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) != 0;
        _theme = preference == "system" ? systemLight ? "light" : "dark" : preference;
        var dark = _theme == "dark";
        Background = new SolidColorBrush(dark ? Color.FromRgb(29, 32, 36) : Color.FromRgb(246, 247, 249));
        _boot.Foreground = new SolidColorBrush(dark ? Colors.White : Colors.Black);
        _web.DefaultBackgroundColor = dark ? System.Drawing.Color.FromArgb(29, 32, 36) : System.Drawing.Color.FromArgb(246, 247, 249);
        if (_web.CoreWebView2 is not null) _web.CoreWebView2.Profile.PreferredColorScheme = dark ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
        var darkValue = dark ? 1 : 0; var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero) DwmSetWindowAttribute(handle, 20, ref darkValue, sizeof(int));
        if (_ready) Send("theme", _theme);
    }
    private void ApplyInterfaceScale() => _web.ZoomFactor = _store.State.NativeUi.InterfaceScale;
    private void SystemPreferencesChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(new Action(() => { if (_store.State.NativeUi.Theme == "system") ApplyTheme(); }));
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closingFinal || !_ready) return;
        e.Cancel = true;
        if (_closePending) return;
        _closePending = true; _closeAcknowledged = false; Send("closeRequested", null);
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        watchdog.Tick += async (_, _) =>
        {
            watchdog.Stop(); if (!_closePending || _closeAcknowledged || _closingFinal) return;
            if (MessageBox.Show(this, "界面暂未响应。是否退出？最近已保存的草稿仍会保留。", "退出 BP Studio", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) await CloseAsync();
            else _closePending = false;
        }; watchdog.Start();
    }
    private async Task CloseAsync()
    {
        _job?.Cancel();
        if (_recorder.IsRecording) await StopRecordingAsync();
        if (_dataReady) { _store.State.Draft = Json.Clone(_draft); await _store.SaveAsync(); }
        _closingFinal = true; Close();
    }
    private void DisposeResources()
    {
        _closingFinal = true; _timer.Stop(); _job?.Cancel(); _recorder.Dispose(); _player.Close();
        SystemEvents.UserPreferenceChanged -= SystemPreferencesChanged;
        _web.Dispose(); _models.Dispose(); _managedSpeech.Dispose();
        foreach (var path in _temporaryFiles) { try { File.Delete(path); } catch (IOException) { } }
    }
    private static string Text(JsonElement body, string name, string fallback = "", int max = 50000)
    {
        if (!body.TryGetProperty(name, out var value)) return fallback;
        if (value.ValueKind != JsonValueKind.String || (value.GetString()?.Length ?? 0) > max) throw new InvalidOperationException("参数格式或长度无效。");
        return value.GetString() ?? fallback;
    }
    private static bool Flag(JsonElement body, string name) => body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    private static double Number(JsonElement body, string name, double fallback)
    {
        if (!body.TryGetProperty(name, out var value)) return fallback;
        if (!value.TryGetDouble(out var number) || !double.IsFinite(number)) throw new InvalidOperationException("数值参数无效。");
        return number;
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
