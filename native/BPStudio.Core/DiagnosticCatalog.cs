using System.Text.RegularExpressions;

namespace BPStudio.Core;

public static class DiagnosticCatalog
{
    public static readonly string[] Roles = ["PM", "LO", "DPM", "DLO", "MG", "MO", "GW", "OW"];
    public static readonly string[] Statuses = ["complete", "partial", "missing", "insufficient", "not_applicable"];
    public static readonly (string Id, string Name)[] Items = [
        ("role", "辩位职责"), ("structure", "辩词结构"), ("argument", "论证框架与过程"),
        ("characterization", "情境描述"), ("logic", "逻辑谬误与推理漏洞"), ("rhetoric", "修辞与类比"),
        ("response", "回应与交锋"), ("stakeholders", "利益相关方与影响"), ("core", "核心争点与证明责任")];
    public static string Name(string id) => Items.FirstOrDefault(i => i.Id == id).Name ?? id;
    public static string Status(string status) => status switch {
        "complete" => "完成较好", "partial" => "部分完成", "missing" => "未完成", "insufficient" => "证据不足", "not_applicable" => "不适用", _ => status };
    public static string Kind(string kind) => kind switch { "full" => "完整原文", "excerpt" => "节选", "summary" => "用户概述", _ => kind };
    // Changing the current role must not delete already-entered context. Only earlier roles enter analysis.
    public static IEnumerable<PreviousSpeech> ActivePrevious(Practice p) => (p.PreviousSpeeches ?? [])
        .Where(s => s is not null && Array.IndexOf(Roles, s.Role) >= 0 && Array.IndexOf(Roles, s.Role) < Array.IndexOf(Roles, p.Role) && !string.IsNullOrWhiteSpace(s.Text))
        .OrderBy(s => Array.IndexOf(Roles, s.Role));
    public static string? Source(Practice p, string source) => source switch {
        "transcript" => p.Transcript,
        "context" => p.Context,
        _ when source.StartsWith("previous:", StringComparison.Ordinal) => ActivePrevious(p).FirstOrDefault(s => "previous:" + s.Role == source)?.Text,
        _ => null };
    public static string SourceName(string source) => source switch { "transcript" => "本次发言", "context" => "前序背景", _ when source.StartsWith("previous:") => source[9..] + " 前序发言", _ => "未知来源" };
    private static string Fold(string value) => Regex.Replace(value.Trim(), @"\s+", " ").ToLowerInvariant();
    public static bool ContainsQuote(string? text, string quote) => !string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(quote) && Fold(text).Contains(Fold(quote), StringComparison.Ordinal);
    public static void VerifyEvidence(Feedback f, Practice p)
    {
        foreach (var e in f.Diagnostics.SelectMany(d => d.Evidence.Concat(d.Details.SelectMany(x => x.Evidence)))) {
            e.Verified = ContainsQuote(Source(p, e.Source), e.Quote);
            if (!e.Verified) e.Note = string.IsNullOrWhiteSpace(e.Quote) ? "未提供直接引文；请结合分析核对。" : "引文未能在标注来源中核实，请核对后采用该判断。";
        }
    }
}
