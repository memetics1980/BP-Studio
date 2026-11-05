using NAudio.Wave;
using System.Diagnostics;
namespace BPStudio.Native;
public sealed class RecordingSession : IDisposable
{
    private WaveInEvent? _capture;
    private WaveFileWriter? _writer;
    private readonly object _gate = new();
    private TaskCompletionSource? _stopped;
    public Stopwatch Clock { get; } = new();
    public bool IsRecording => _capture is not null;
    public bool Paused { get; private set; }
    public double Level { get; private set; }
    public string? Path { get; private set; }
    public event Action<string>? DeviceError;
    public static List<string> Devices() => Enumerable.Range(0, WaveIn.DeviceCount).Select(i => WaveIn.GetCapabilities(i).ProductName).ToList();
    public void Start(int device, string directory)
    {
        if (_capture is not null) throw new InvalidOperationException("已有录音正在进行。");
        Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "recording-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            _writer = new WaveFileWriter(Path, new WaveFormat(16000, 16, 1));
            _capture = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 50 };
            _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously); Paused = false; Clock.Restart();
            _capture.DataAvailable += (_, e) => { lock (_gate) { if (Paused || _writer is null) return; _writer.Write(e.Buffer, 0, e.BytesRecorded); double sum = 0; for (var i = 0; i + 1 < e.BytesRecorded; i += 2) { var x = BitConverter.ToInt16(e.Buffer, i) / 32768d; sum += x * x; } Level = Math.Min(1, Math.Sqrt(sum / Math.Max(1, e.BytesRecorded / 2)) * 5); } };
            _capture.RecordingStopped += (_, e) => { lock (_gate) { _writer?.Dispose(); _writer = null; } Clock.Stop(); if (e.Exception is not null) DeviceError?.Invoke("麦克风已断开或无法继续录音，请检查设备。"); _stopped?.TrySetResult(); };
            _capture.StartRecording();
        }
        catch { Dispose(); throw new InvalidOperationException("无法启动麦克风。请检查 Windows 麦克风隐私权限，或选择其他输入设备。"); }
    }
    public void TogglePause() { if (_capture is null) return; lock (_gate) { Paused = !Paused; if (Paused) { Clock.Stop(); Level = 0; } else Clock.Start(); } }
    public async Task StopAsync() { var capture = _capture; if (capture is null) return; capture.StopRecording(); if (_stopped is not null) await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(5)); capture.Dispose(); _capture = null; Clock.Stop(); Level = 0; }
    public void Dispose() { try { _capture?.StopRecording(); } catch { } _capture?.Dispose(); _capture = null; lock (_gate) { _writer?.Dispose(); _writer = null; } Clock.Stop(); }
}
