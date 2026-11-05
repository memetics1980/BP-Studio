using System.Text.Json;
using System.Text.Json.Serialization;
namespace BPStudio.Core;
public static class Json
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;
}
public sealed class ModelSettings
{
    public string Provider { get; set; } = "ollama";
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";
    public string Model { get; set; } = "qwen3:8b";
    public string SpeechProvider { get; set; } = "local";
    public string SpeechUrl { get; set; } = "https://api.openai.com/v1";
    public string SpeechModel { get; set; } = "whisper-1";
    public string WhisperModel { get; set; } = "base";
    public string PythonPath { get; set; } = "";
    public string SpeechLanguage { get; set; } = "en";
    public string FeedbackLanguage { get; set; } = "zh";
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
public class Practice
{
    public string Motion { get; set; } = "";
    public string Role { get; set; } = "PM";
    public string Transcript { get; set; } = "";
    public string Context { get; set; } = "";
    public List<PreviousSpeech> PreviousSpeeches { get; set; } = [];
    public double Duration { get; set; }
    public int Target { get; set; } = 420;
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
public sealed class PracticeRecord : Practice
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CreatedAt { get; set; } = DateTimeOffset.Now.ToString("O");
    public string Model { get; set; } = "";
    public string Provider { get; set; } = "";
    public Feedback Feedback { get; set; } = new();
    [JsonIgnore] public string DisplayDate => DateTimeOffset.TryParse(CreatedAt, out var date) ? date.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : CreatedAt;
    [JsonIgnore] public string DisplayLine => $"{DisplayDate}  ·  {Role}  ·  {Model}";
    public override string ToString() => $"{DisplayDate} · {Role}  {Motion}";
}
public sealed class Feedback
{
    public int Version { get; set; } = 1;
    public string Summary { get; set; } = "";
    public List<Dimension> Dimensions { get; set; } = [];
    public List<string> Strengths { get; set; } = [];
    public List<Improvement> Improvements { get; set; } = [];
    public string Rewrite { get; set; } = "";
    public string NextDrill { get; set; } = "";
    public string Limitations { get; set; } = "";
    public List<string> PriorityIssues { get; set; } = [];
    public List<DeepDiagnostic> Diagnostics { get; set; } = [];
}
public sealed class PreviousSpeech
{
    public string Role { get; set; } = "PM";
    public string Kind { get; set; } = "full";
    public string Text { get; set; } = "";
}
public sealed class EvidenceReference
{
    // transcript | context | previous:PM ... previous:GW
    public string Source { get; set; } = "transcript";
    public string Quote { get; set; } = "";
    public string Note { get; set; } = "";
    public bool Verified { get; set; }
}
public sealed class DiagnosticDetail
{
    public string Label { get; set; } = "";
    public string Status { get; set; } = "";
    public string Content { get; set; } = "";
    public List<EvidenceReference> Evidence { get; set; } = [];
}
public sealed class DeepDiagnostic
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public string Conclusion { get; set; } = "";
    public string Analysis { get; set; } = "";
    public string Impact { get; set; } = "";
    public string Advice { get; set; } = "";
    public List<EvidenceReference> Evidence { get; set; } = [];
    public List<DiagnosticDetail> Details { get; set; } = [];
}
public sealed class Dimension { public string Name { get; set; } = ""; public int? Score { get; set; } public string Reason { get; set; } = ""; }
public sealed class Improvement { public string Title { get; set; } = ""; public string Evidence { get; set; } = ""; public string Advice { get; set; } = ""; }
public sealed class Motion
{
    public JsonElement Id { get; set; }
    public string Title { get; set; } = "";
    public string Zh { get; set; } = "";
    public string Category { get; set; } = "其他";
    public string Level { get; set; } = "入门";
    public List<string> Tags { get; set; } = [];
    public string? CreatedAt { get; set; }
    [JsonIgnore] public bool IsCustom => Id.ValueKind == JsonValueKind.String;
    [JsonIgnore] public string Key => Id.ToString();
    public override string ToString() => $"{Category} · {Level}  {Title}";
}
public sealed class RoleInfo
{
    public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string English { get; set; } = ""; public string Team { get; set; } = ""; public string Duty { get; set; } = ""; public List<string> Steps { get; set; } = [];
    public override string ToString() => $"{Id} · {Name}";
}
public sealed class Catalog
{
    public List<Motion> Motions { get; set; } = []; public List<RoleInfo> Roles { get; set; } = []; public string Sample { get; set; } = "";
    public static Catalog Load() { using var stream = typeof(Catalog).Assembly.GetManifestResourceStream("BPStudio.Core.catalog.json")!; return JsonSerializer.Deserialize<Catalog>(stream, Json.Options)!; }
}
public sealed class UiPreferences
{
    public double ReaderFontSize { get; set; } = 18;
    public double CompareRatio { get; set; } = .45;
    private double _interfaceScale = 1;
    public double InterfaceScale { get => _interfaceScale; set => _interfaceScale = double.IsFinite(value) ? Math.Clamp(value, .9, 1.5) : 1; }
    private int _libraryPageSize = 20, _historyPageSize = 20;
    public int LibraryPageSize { get => _libraryPageSize; set => _libraryPageSize = value is 10 or 20 or 50 ? value : 20; }
    public int HistoryPageSize { get => _historyPageSize; set => _historyPageSize = value is 10 or 20 or 50 ? value : 20; }
    private string _theme = "system";
    public string Theme { get => _theme; set => _theme = value is "light" or "dark" ? value : "system"; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
public sealed class AppState
{
    public int Version { get; set; } = 1;
    public ModelSettings Settings { get; set; } = new();
    public Dictionary<string, string> Secrets { get; set; } = [];
    public List<PracticeRecord> Records { get; set; } = [];
    public List<Motion> CustomMotions { get; set; } = [];
    public Practice? Draft { get; set; }
    public UiPreferences NativeUi { get; set; } = new();
    public string? MigratedFrom { get; set; }
    public string? MigratedAt { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
public sealed class SpeechResult { public string Text { get; set; } = ""; public double? Duration { get; set; } public string? Message { get; set; } public bool Ready { get; set; } }
public static class Validation
{
    public static readonly string[] Categories = ["教育", "科技", "社会", "环境", "经济", "其他"];
    public static readonly string[] Levels = ["入门", "进阶", "挑战"];
    public static string TitleKey(string title) => System.Text.RegularExpressions.Regex.Replace(title.Normalize(System.Text.NormalizationForm.FormKC).Trim(), @"\s+", " ").ToLowerInvariant();
    public static Uri Endpoint(string address, string suffix)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) throw new InvalidOperationException("服务地址无效，不能包含账号、查询参数或片段。");
        if (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) throw new InvalidOperationException("远程服务请使用 HTTPS，本机回环地址支持 HTTP。");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + suffix);
    }
    public static void Settings(ModelSettings s)
    {
        if (s.Provider is not ("api" or "ollama") || s.SpeechProvider is not ("local" or "api")) throw new InvalidOperationException("请选择支持的模型类型。");
        Endpoint(s.BaseUrl, ""); Endpoint(s.SpeechUrl, "");
        foreach (var name in new[] { s.Model, s.SpeechModel, s.WhisperModel }) if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new InvalidOperationException("请填写有效模型名称。");
        if (s.SpeechLanguage is not ("en" or "zh" or "auto") || s.FeedbackLanguage is not ("en" or "zh")) throw new InvalidOperationException("请选择支持的语言。");
        if (s.PythonPath.Length > 1000) throw new InvalidOperationException("Python 路径过长。");
    }
    public static void Practice(Practice p, Catalog catalog)
    {
        if (!catalog.Roles.Any(r => r.Id == p.Role)) throw new InvalidOperationException("请选择有效的 BP 辩位。");
        if (p.Motion.Trim().Length < 8 || p.Motion.Length > 2000) throw new InvalidOperationException("辩题需为 8–2,000 个字符。");
        if (p.Transcript.Trim().Length < 30 || p.Transcript.Length > 50000) throw new InvalidOperationException("发言需为 30–50,000 个字符，请先完成或校对转写。");
        if (p.Context.Length > 30000) throw new InvalidOperationException("前序背景不能超过 30,000 个字符。");
        Previous(p);
        if (p.Target is not (180 or 300 or 420)) throw new InvalidOperationException("请选择 3、5 或 7 分钟训练。");
    }
    public static void Previous(Practice p)
    {
        var speeches = p.PreviousSpeeches;
        if (speeches is null || speeches.Count > 7 || speeches.Any(s => s is null || !DiagnosticCatalog.Roles.Contains(s.Role) || s.Kind is not ("full" or "excerpt" or "summary") || s.Text is null || s.Text.Length > 30000) || speeches.Select(s => s.Role).Distinct().Count() != speeches.Count || speeches.Sum(s => s.Text.Length) > 90000)
            throw new InvalidOperationException("前序发言需按有效辩位填写，每个辩位仅一份，单份最多 30,000 字符，合计最多 90,000 字符。");
    }
    public static void Motion(Motion m)
    {
        m.Title = m.Title.Trim(); m.Zh = m.Zh.Trim();
        if (m.Title.Length < 8 || m.Title.Length > 2000 || m.Zh.Length > 2000) throw new InvalidOperationException("请填写 8–2,000 个字符的辩题，中文说明不超过 2,000 字。");
        if (!Categories.Contains(m.Category) || !Levels.Contains(m.Level)) throw new InvalidOperationException("请选择有效分类和难度。");
        if (m.Tags.Count > 8 || m.Tags.Any(t => t.Length > 20)) throw new InvalidOperationException("最多 8 个标签，每个标签不超过 20 个字符。");
        m.Tags = m.Tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().ToList();
    }
}
