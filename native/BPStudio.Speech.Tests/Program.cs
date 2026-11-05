using BPStudio.Core;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var root = Path.GetFullPath(args[0]);
var version = System.Xml.Linq.XDocument.Load(Path.Combine(root, "native", "BPStudio.Desktop", "BPStudio.Desktop.csproj")).Descendants("Version").Single().Value;
var payload = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(root, "release", $"BP-Studio-{version}-Desktop-Windows-x64", "speech");
var test = Path.Combine(root, "test-results", "speech-" + version + "-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(test);
Environment.SetEnvironmentVariable("HF_HOME", Path.Combine(test, "empty-hf")); Environment.SetEnvironmentVariable("HF_HUB_OFFLINE", "1");
var passed = new List<string>();
void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed.Add(name); Console.WriteLine("PASS " + name); }
var transport = new ModelTransport(payload);
using var manager = new ManagedSpeech(payload, Path.Combine(test, "managed"), transport);
Check(!manager.RuntimeReady && manager.ModelPath("base") is null, "fresh profile has no managed runtime/model");
await manager.EnsureAsync("base", false, null, CancellationToken.None);
Check(manager.RuntimeReady && manager.ModelPath("base") is not null && transport.Calls == 0, "base installs and verifies offline without HTTP or system Python");
var effective = manager.EffectiveSettings(new());
Check(effective.PythonPath.StartsWith(test) && effective.WhisperModel.StartsWith(test), "transcription resolves only isolated managed resources");
var speech = new SpeechService();
var probe = await speech.RunAsync(effective, null, CancellationToken.None);
Check(probe.Ready && probe.Message!.Contains("离线"), "portable runtime passes decoder and base load check");
foreach (var format in new[] { "wav", "wave", "mp3", "m4a", "flac", "ogg" })
{
    var result = await speech.RunAsync(effective, Path.Combine(root, "test-results", "audio-fixtures", "练习音频." + format), CancellationToken.None);
    Check(result.Text.Length > 30 && result.Duration > 1, "bundled offline speech recognizes Unicode " + format);
}
var before = transport.Calls;
try { await manager.EnsureAsync("medium", false, null, CancellationToken.None); throw new Exception("unexpected implicit download"); }
catch (InvalidOperationException e) when (e.Message.Contains("点击下载")) { }
Check(transport.Calls == before && manager.ModelPath("medium") is null, "uninstalled larger model never downloads implicitly");
try { await manager.EnsureAsync("../escape", true, null, CancellationToken.None); throw new Exception("unexpected model id"); }
catch (InvalidOperationException e) when (e.Message.Contains("高级设置")) { }
Check(transport.Calls == before, "model path traversal and unknown identifiers rejected before HTTP");
using (var cancellation = new CancellationTokenSource())
{
    transport.Slow = true;
    var progress = new InlineProgress(p => { if (p.Stage == "download" && p.Completed > 2_000_000) cancellation.Cancel(); });
    try { await manager.EnsureAsync("tiny", true, progress, cancellation.Token); throw new Exception("cancel failed"); } catch (OperationCanceledException) { }
}
var partial = Directory.GetFiles(Path.Combine(manager.Root, ".downloads"), "model.bin.partial", SearchOption.AllDirectories).Single();
Check(new FileInfo(partial).Length > 0 && manager.ModelPath("tiny") is null, "cancel retains partial download and never marks model installed");
transport.Slow = false;
await manager.EnsureAsync("tiny", true, null, CancellationToken.None);
Check(manager.ModelPath("tiny") is not null && transport.Resumed, "explicit retry resumes download and activates only after checksum/load validation");
Check(transport.NoCredentials, "model HTTP requests contain no application credentials");
transport.Corrupt = true;
try { await manager.EnsureAsync("small", true, null, CancellationToken.None); throw new Exception("corrupt file accepted"); }
catch (InvalidOperationException e) when (e.Message.Contains("校验失败")) { }
Check(manager.ModelPath("small") is null && manager.ModelPath("base") is not null, "checksum failure preserves working base and prevents bad-model activation");
transport.Corrupt = false;
await manager.EnsureAsync("small", true, null, CancellationToken.None);
Check(manager.ModelPath("small") is not null, "failed model download can be retried successfully");
using (var reload = new ManagedSpeech(payload, manager.Root, new ModelTransport(payload)))
    Check(reload.RuntimeReady && reload.ModelPath("base") is not null && reload.ModelPath("tiny") is not null, "installed resources persist across program restarts");
var custom = manager.EffectiveSettings(new ModelSettings { PythonPath = "C:\\custom-python.exe", WhisperModel = "custom/model" });
Check(custom.PythonPath == "C:\\custom-python.exe" && custom.WhisperModel == "custom/model", "explicit advanced environment is preserved");
await File.WriteAllTextAsync(Path.Combine(test, "report.json"), JsonSerializer.Serialize(new { passed = true, checks = passed, directory = test }, Json.Options));
await File.WriteAllTextAsync(Path.Combine(root, "test-results", "speech-" + version + "-report.json"), JsonSerializer.Serialize(new { passed = true, checks = passed, directory = test }, Json.Options));
Console.WriteLine("All " + passed.Count + " bundled speech checks passed.");

sealed class InlineProgress(Action<SpeechProgress> callback) : IProgress<SpeechProgress> { public void Report(SpeechProgress value) => callback(value); }
sealed class ModelTransport : HttpMessageHandler
{
    private readonly string _base;
    private readonly List<object> _siblings;
    public bool Slow, Corrupt, Resumed, NoCredentials = true;
    public int Calls;
    public ModelTransport(string payload)
    {
        _base = Path.Combine(payload, "models", "base");
        _siblings = Directory.GetFiles(_base).Where(p => Path.GetFileName(p) is "config.json" or "model.bin" or "tokenizer.json" or "vocabulary.txt").Select(path =>
        {
            var bytes = File.ReadAllBytes(path); var size = bytes.LongLength; var name = Path.GetFileName(path);
            var git = SHA1.HashData(Encoding.UTF8.GetBytes("blob " + size + "\0").Concat(bytes).ToArray());
            return (object)new { rfilename = name, size, blobId = Convert.ToHexString(git).ToLowerInvariant(), lfs = name == "model.bin" ? new { sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), size } : null };
        }).ToList();
    }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; NoCredentials &= request.Headers.Authorization is null && !request.Headers.Contains("Cookie");
        if (request.RequestUri!.AbsolutePath.StartsWith("/api/models/")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { sha = new string('a', 40), siblings = _siblings })) });
        var name = request.RequestUri.Segments.Last(); var path = Path.Combine(_base, name);
        var offset = request.Headers.Range?.Ranges.FirstOrDefault()?.From ?? 0; Resumed |= offset > 0;
        Stream stream;
        if (Corrupt && name == "config.json") { var bytes = File.ReadAllBytes(path); bytes[0] ^= 1; stream = new MemoryStream(bytes); }
        else stream = File.OpenRead(path);
        stream.Position = offset; var total = stream.Length;
        if (Slow && name == "model.bin") stream = new SlowReadStream(stream);
        var response = new HttpResponseMessage(offset > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentLength = total - offset;
        if (offset > 0) response.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, total - 1, total);
        return Task.FromResult(response);
    }
}
sealed class SlowReadStream(Stream source) : Stream
{
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => source.Length; public override long Position { get => source.Position; set => throw new NotSupportedException(); }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { await Task.Delay(12, cancellationToken); return await source.ReadAsync(buffer[..Math.Min(buffer.Length, 65536)], cancellationToken); }
    public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
    public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
}
