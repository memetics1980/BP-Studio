using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BPStudio.Core;

public sealed record SpeechModelInfo(string Id, string Label, int Megabytes, string Description);
public sealed record SpeechProgress(string Model, string Stage, string Message, long Completed, long Total)
{
    public double? Percent => Total > 0 ? Math.Clamp(100d * Completed / Total, 0, 100) : null;
}
public sealed class SpeechFile
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string? GitSha1 { get; set; }
}
public sealed class SpeechBundle
{
    public string RuntimeId { get; set; } = "";
    public string BaseRevision { get; set; } = "";
    public List<SpeechFile> Files { get; set; } = [];
}
public sealed class InstalledSpeechModel
{
    public string Revision { get; set; } = "";
    public List<SpeechFile> Files { get; set; } = [];
}

/// <summary>Installs the shipped CPU runtime/base offline; other models require an explicit download command.</summary>
public sealed class ManagedSpeech : IDisposable
{
    public static readonly SpeechModelInfo[] Models = [
        new("base", "Base", 145, "随程序附带，适合日常练习；首次使用自动准备。"),
        new("tiny", "Tiny", 75, "更小、更快，适合较低配置的电脑。"),
        new("small", "Small", 484, "更完整的识别能力，需要更多内存和处理时间。"),
        new("medium", "Medium", 1530, "更大的多语言模型，CPU 转写可能较慢。"),
        new("large-v3", "Large v3", 3090, "大型模型，建议内存充足且能接受较长处理时间。"),
        new("large-v3-turbo", "Large v3 Turbo", 1620, "大型模型的加速版本，仍需要较多内存。")
    ];
    private static readonly HashSet<string> AllowedFiles = ["config.json", "preprocessor_config.json", "model.bin", "tokenizer.json", "vocabulary.txt", "vocabulary.json"];
    private static readonly string[] RequiredFiles = ["config.json", "model.bin", "tokenizer.json"];
    private readonly string _bundleDirectory, _bundleHash;
    private readonly SpeechBundle _bundle;
    private readonly HttpClient _http;
    public string Root { get; }
    public string PythonPath => Path.Combine(Root, "runtimes", _bundle.RuntimeId + "-" + _bundleHash[..12], "python.exe");
    public bool BundleAvailable => _bundle.Files.Count > 0 && _bundle.Files.Any(f => f.Path == "engine/python.exe") && _bundle.Files.Any(f => f.Path == "models/base/model.bin");
    public bool RuntimeReady => BundleAvailable && File.Exists(PythonPath) && File.Exists(Path.Combine(Path.GetDirectoryName(PythonPath)!, "installed.json"));
    public ManagedSpeech(string? bundleDirectory = null, string? root = null, HttpMessageHandler? handler = null)
    {
        _bundleDirectory = Path.GetFullPath(bundleDirectory ?? Path.Combine(AppContext.BaseDirectory, "speech"));
        Root = Path.GetFullPath(root ?? Environment.GetEnvironmentVariable("BP_SPEECH_ROOT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BPStudio", "speech", "managed"));
        var file = Path.Combine(_bundleDirectory, "bundle.json");
        var raw = File.Exists(file) ? File.ReadAllBytes(file) : "{}"u8.ToArray();
        _bundle = JsonSerializer.Deserialize<SpeechBundle>(raw, Json.Options) ?? new();
        if (_bundle.Files.Count > 0 && (!Regex.IsMatch(_bundle.RuntimeId, "^[a-zA-Z0-9-]{1,80}$") || !ValidRevision(_bundle.BaseRevision))) throw new InvalidOperationException("内置语音清单无效，请保留完整发布目录。");
        _bundleHash = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
        _http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("BPStudio/0.3.2");
    }
    private static bool ValidRevision(string? revision) => revision is not null && Regex.IsMatch(revision, "^[0-9a-f]{40}$");
    public static string NormalizeModel(string name) => name == "large" ? "large-v3" : name == "turbo" ? "large-v3-turbo" : name;
    public bool Supports(string model) => Models.Any(m => m.Id == NormalizeModel(model));
    private static string SafePath(string root, string relative)
    {
        var result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (Path.IsPathRooted(relative) || !result.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("语音资源路径无效。");
        return result;
    }
    public string? ModelPath(string model)
    {
        model = NormalizeModel(model); if (!Supports(model)) return null;
        var parent = Path.Combine(Root, "models", model);
        try
        {
            var info = JsonSerializer.Deserialize<InstalledSpeechModel>(File.ReadAllText(Path.Combine(parent, "active.json")), Json.Options);
            if (info is null || info.Files is null || !ValidRevision(info.Revision) || !RequiredFiles.All(n => info.Files.Any(f => f.Path == n))) return null;
            var directory = Path.Combine(parent, info.Revision);
            return info.Files.All(f => AllowedFiles.Contains(f.Path) && File.Exists(SafePath(directory, f.Path)) && new FileInfo(SafePath(directory, f.Path)).Length == f.Size) ? directory : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }
    public object Status(ModelSettings settings) => new
    {
        runtimeReady = RuntimeReady, bundled = BundleAvailable, customPython = !string.IsNullOrWhiteSpace(settings.PythonPath),
        selected = settings.WhisperModel,
        models = Models.Select(m => new { m.Id, m.Label, m.Megabytes, m.Description, installed = ModelPath(m.Id) is not null, bundled = m.Id == "base" && BundleAvailable })
    };
    private async Task<FileStream> LockAsync(CancellationToken token)
    {
        Directory.CreateDirectory(Root);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return new FileStream(Path.Combine(Root, ".install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { await Task.Delay(250, token); }
        }
    }
    private static void Report(IProgress<SpeechProgress>? progress, string model, string stage, string message, long done = 0, long total = 0) => progress?.Report(new(model, stage, message, done, total));
    private static async Task VerifyAsync(string file, SpeechFile item, CancellationToken token)
    {
        await using var stream = File.OpenRead(file);
        if (stream.Length != item.Size) throw new InvalidOperationException("语音资源长度不完整，请重试。" );
        string actual;
        if (item.Sha256.Length == 64) actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
        else if (item.GitSha1?.Length == 40)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            hash.AppendData(Encoding.UTF8.GetBytes("blob " + item.Size + "\0"));
            var buffer = new byte[131072]; int count;
            while ((count = await stream.ReadAsync(buffer, token)) > 0) hash.AppendData(buffer, 0, count);
            actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        else throw new InvalidOperationException("模型服务没有提供可核验的文件摘要。");
        if (!string.Equals(actual, item.Sha256.Length == 64 ? item.Sha256 : item.GitSha1, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("模型文件校验失败，请重试下载。");
    }
    private async Task EnsureRuntimeAsync(IProgress<SpeechProgress>? progress, CancellationToken token)
    {
        if (RuntimeReady) return;
        if (!BundleAvailable) throw new InvalidOperationException("缺少内置语音资源，请使用包含 speech 文件夹的完整发布目录。");
        var target = Path.GetDirectoryName(PythonPath)!; var staging = target + ".install-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(staging);
        try
        {
            var files = _bundle.Files.Where(f => f.Path.StartsWith("engine/", StringComparison.Ordinal)).ToList();
            var total = files.Sum(f => f.Size); long done = 0;
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested(); var destination = SafePath(staging, file.Path[7..]); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using (var source = File.OpenRead(SafePath(_bundleDirectory, file.Path)))
                await using (var output = File.Create(destination)) await source.CopyToAsync(output, token);
                await VerifyAsync(destination, file, token); done += file.Size; Report(progress, "base", "runtime", "正在准备内置语音环境…", done, total);
            }
            await File.WriteAllTextAsync(Path.Combine(staging, "installed.json"), JsonSerializer.Serialize(new { runtimeId = _bundle.RuntimeId, manifest = _bundleHash }, Json.Options), token);
            if (Directory.Exists(target)) Directory.Move(target, target + ".incomplete-" + Guid.NewGuid().ToString("N"));
            Directory.Move(staging, target);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
    private async Task ValidateModelAsync(string model, string path, IProgress<SpeechProgress>? progress, CancellationToken token)
    {
        Report(progress, model, "verify", "正在验证模型与音频组件…");
        var settings = new ModelSettings { PythonPath = PythonPath, WhisperModel = path };
        var result = await new SpeechService().RunAsync(settings, null, token);
        if (!result.Ready) throw new InvalidOperationException("模型未通过本地加载验证。");
    }
    private async Task ActivateFilesAsync(string model, string revision, List<SpeechFile> files, string staging, IProgress<SpeechProgress>? progress, CancellationToken token)
    {
        if (!RequiredFiles.All(n => files.Any(f => f.Path == n)) || !files.Any(f => f.Path.StartsWith("vocabulary.", StringComparison.Ordinal))) throw new InvalidOperationException("模型文件不完整。");
        await ValidateModelAsync(model, staging, progress, token); token.ThrowIfCancellationRequested();
        var parent = Path.Combine(Root, "models", model); Directory.CreateDirectory(parent); var destination = Path.Combine(parent, revision);
        if (Directory.Exists(destination)) Directory.Move(destination, destination + ".incomplete-" + Guid.NewGuid().ToString("N"));
        Directory.Move(staging, destination);
        var marker = Path.Combine(parent, "active.json"); var temp = marker + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(new InstalledSpeechModel { Revision = revision, Files = files }, Json.Options), token); File.Move(temp, marker, true);
    }
    public async Task EnsureAsync(string model, bool allowDownload, IProgress<SpeechProgress>? progress, CancellationToken token)
    {
        model = NormalizeModel(model);
        if (!Supports(model)) throw new InvalidOperationException("这个模型需使用高级设置中的自定义 Python 环境。请先选择内置列表中的模型。");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromHours(2));
        await using var installLock = await LockAsync(deadline.Token);
        await EnsureRuntimeAsync(progress, deadline.Token);
        if (ModelPath(model) is not null) { Report(progress, model, "ready", "本地模型已就绪。", 1, 1); return; }
        if (model == "base")
        {
            var files = _bundle.Files.Where(f => f.Path.StartsWith("models/base/", StringComparison.Ordinal) && AllowedFiles.Contains(f.Path[12..])).Select(f => new SpeechFile { Path = f.Path[12..], Size = f.Size, Sha256 = f.Sha256 }).ToList();
            var staging = Path.Combine(Root, ".downloads", "base-" + _bundle.BaseRevision); Directory.CreateDirectory(staging);
            long done = 0; var total = files.Sum(f => f.Size);
            foreach (var file in files)
            {
                deadline.Token.ThrowIfCancellationRequested(); var target = SafePath(staging, file.Path);
                await using (var source = File.OpenRead(SafePath(_bundleDirectory, "models/base/" + file.Path)))
                await using (var output = File.Create(target)) await source.CopyToAsync(output, deadline.Token);
                await VerifyAsync(target, file, deadline.Token); done += file.Size; Report(progress, model, "install", "正在安装随附的 Base 模型…", done, total);
            }
            await ActivateFilesAsync(model, _bundle.BaseRevision, files, staging, progress, deadline.Token);
        }
        else
        {
            if (!allowDownload) throw new InvalidOperationException("此模型尚未安装。请在“语音识别”中点击下载并安装；不会在转写时自动下载大型模型。");
            await DownloadAsync(model, progress, deadline.Token);
        }
        Report(progress, model, "ready", "语音模型已安装，可离线转写。", 1, 1);
    }
    private async Task DownloadAsync(string model, IProgress<SpeechProgress>? progress, CancellationToken token)
    {
        var repo = model == "large-v3-turbo" ? "mobiuslabsgmbh/faster-whisper-large-v3-turbo" : "Systran/faster-whisper-" + model;
        Report(progress, model, "metadata", "正在获取模型清单…");
        using var metadataTimeout = CancellationTokenSource.CreateLinkedTokenSource(token); metadataTimeout.CancelAfter(TimeSpan.FromSeconds(60));
        using var response = await _http.GetAsync("https://huggingface.co/api/models/" + repo + "?blobs=true", metadataTimeout.Token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(metadataTimeout.Token)); var root = document.RootElement;
        var revision = root.GetProperty("sha").GetString() ?? ""; if (!ValidRevision(revision)) throw new InvalidOperationException("模型版本号无效。");
        var files = new List<SpeechFile>();
        foreach (var sibling in root.GetProperty("siblings").EnumerateArray())
        {
            var name = sibling.GetProperty("rfilename").GetString() ?? ""; if (!AllowedFiles.Contains(name)) continue;
            var lfs = sibling.TryGetProperty("lfs", out var large) && large.ValueKind == JsonValueKind.Object;
            var size = lfs ? large.GetProperty("size").GetInt64() : sibling.GetProperty("size").GetInt64();
            if (size <= 0 || size > 8L * 1024 * 1024 * 1024) throw new InvalidOperationException("模型文件大小无效。");
            files.Add(new() { Path = name, Size = size, Sha256 = lfs ? large.GetProperty("sha256").GetString() ?? "" : "", GitSha1 = lfs ? null : sibling.GetProperty("blobId").GetString() });
        }
        if (!RequiredFiles.All(n => files.Any(f => f.Path == n))) throw new InvalidOperationException("模型下载清单不完整。");
        var staging = Path.Combine(Root, ".downloads", model + "-" + revision); Directory.CreateDirectory(staging);
        long completed = 0; var total = files.Sum(f => f.Size);
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested(); var final = SafePath(staging, file.Path); var partial = final + ".partial";
            if (File.Exists(final)) { try { await VerifyAsync(final, file, token); completed += file.Size; continue; } catch (InvalidOperationException) { File.Delete(final); } }
            var existing = File.Exists(partial) ? new FileInfo(partial).Length : 0;
            if (existing == file.Size)
            {
                try { await VerifyAsync(partial, file, token); File.Move(partial, final, true); completed += file.Size; continue; }
                catch (InvalidOperationException) { File.Delete(partial); existing = 0; }
            }
            if (existing > file.Size) { File.Delete(partial); existing = 0; }
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://huggingface.co/" + repo + "/resolve/" + revision + "/" + file.Path + "?download=true");
            if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);
            using var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(token); headerTimeout.CancelAfter(TimeSpan.FromSeconds(60));
            using var download = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headerTimeout.Token); download.EnsureSuccessStatusCode();
            if (download.StatusCode != HttpStatusCode.PartialContent) existing = 0;
            else if (download.Content.Headers.ContentRange?.From != existing) throw new InvalidOperationException("服务器返回的续传位置无效，请重试。");
            await using (var input = await download.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(partial, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var buffer = new byte[131072]; long received = existing; var clock = Stopwatch.StartNew(); long previous = -300;
                while (true)
                {
                    using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(token); readTimeout.CancelAfter(TimeSpan.FromSeconds(60));
                    var count = await input.ReadAsync(buffer, readTimeout.Token); if (count == 0) break;
                    received += count; if (received > file.Size) throw new InvalidOperationException("下载文件大小超出清单。"); await output.WriteAsync(buffer.AsMemory(0, count), token);
                    if (clock.ElapsedMilliseconds - previous >= 200) { previous = clock.ElapsedMilliseconds; Report(progress, model, "download", "正在下载 " + Models.First(m => m.Id == model).Label + "…", completed + received, total); }
                }
            }
            try { await VerifyAsync(partial, file, token); } catch (InvalidOperationException) { File.Delete(partial); throw; }
            File.Move(partial, final, true); completed += file.Size;
        }
        await ActivateFilesAsync(model, revision, files, staging, progress, token);
    }
    public ModelSettings EffectiveSettings(ModelSettings original)
    {
        if (!string.IsNullOrWhiteSpace(original.PythonPath)) return Json.Clone(original);
        var model = ModelPath(original.WhisperModel);
        if (!RuntimeReady || model is null) throw new InvalidOperationException("本地模型尚未就绪，请在语音识别设置中准备或下载模型。");
        var settings = Json.Clone(original); settings.PythonPath = PythonPath; settings.WhisperModel = model; return settings;
    }
    public void Dispose() => _http.Dispose();
}
