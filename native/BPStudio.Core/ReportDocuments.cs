using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace BPStudio.Core;

// One content model keeps the editable, printable and plain-text reports complete.
public static class ReportDocuments
{
    public sealed record Block(string Style, string Text);
    public static IReadOnlyList<Block> Content(PracticeRecord r)
    {
        var f = r.Feedback;
        var blocks = new List<Block> {
            new("Title", "BP Studio 发言复盘报告"),
            new("Meta", r.DisplayLine),
            new("Normal", "本报告汇总本次发言的原文、复盘反馈与练习建议。训练维度用于学习反馈，不是赛事分数。"),
            new("Heading1", "辩题"), new("Normal", r.Motion),
            new("Heading1", "发言原文"), new("Normal", r.Transcript),
            new("Heading1", "前序背景"), new("Normal", string.IsNullOrWhiteSpace(r.Context) ? "未提供" : r.Context) };
        blocks.Add(new("Heading1", "分角色前序发言"));
        var active = DiagnosticCatalog.ActivePrevious(r).ToList();
        if (active.Count == 0) blocks.Add(new("Normal", "未提供分角色前序发言；回应、延伸与队内关系仅能按已有材料判断。"));
        foreach (var speech in (r.PreviousSpeeches ?? []).Where(s => !string.IsNullOrWhiteSpace(s.Text)).OrderBy(s => Array.IndexOf(DiagnosticCatalog.Roles, s.Role))) {
            var included = active.Any(s => s.Role == speech.Role);
            blocks.Add(new("Heading2", $"{speech.Role} {DiagnosticCatalog.Kind(speech.Kind)}{(included ? "" : "（当前或后续辩位，未参与本次分析）")}"));
            blocks.Add(new("Normal", speech.Text));
        }
        blocks.AddRange(new Block[] { new("Heading1", "总体反馈"), new("Normal", f.Summary),
            new("Heading1", "训练维度"),
            new("Meta", "以下为训练反馈，非赛事分数；证据不足时不评分。") });
        foreach (var d in f.Dimensions) {
            blocks.Add(new("Heading2", d.Name));
            blocks.Add(new("Normal", $"{(d.Score.HasValue ? $"{d.Score}/5" : "证据不足")}\n{d.Reason}"));
        }
        blocks.Add(new("Heading1", "优点"));
        foreach (var s in f.Strengths) blocks.Add(new("Normal", "• " + s));
        if (f.PriorityIssues is { Count: > 0 }) {
            blocks.Add(new("Heading1", "优先改进"));
            foreach (var issue in f.PriorityIssues) blocks.Add(new("Normal", "• " + issue));
        }
        blocks.Add(new("Heading1", "九项深度诊断"));
        if (f.Diagnostics is not { Count: > 0 }) blocks.Add(new("Normal", "此记录生成于旧版，未包含九项深度诊断。原有反馈完整保留；未自动补评。"));
        else foreach (var item in DiagnosticCatalog.Items) {
            var d = f.Diagnostics.FirstOrDefault(x => x.Id == item.Id);
            if (d is null) continue;
            blocks.Add(new("Heading2", item.Name + " · " + DiagnosticCatalog.Status(d.Status)));
            blocks.Add(new("Normal", "判断结论\n" + d.Conclusion));
            AddEvidence(d.Evidence);
            blocks.Add(new("Normal", "分析解释\n" + d.Analysis));
            foreach (var detail in d.Details ?? []) {
                blocks.Add(new("Normal", detail.Label + " · " + DiagnosticCatalog.Status(detail.Status) + "\n" + detail.Content));
                AddEvidence(detail.Evidence);
            }
            blocks.Add(new("Normal", "对说服力的影响\n" + d.Impact));
            blocks.Add(new("Normal", "具体修改建议\n" + d.Advice));
        }
        blocks.Add(new("Heading1", "改进建议"));
        foreach (var i in f.Improvements) {
            blocks.Add(new("Heading2", i.Title));
            blocks.Add(new("Normal", "原文依据\n" + i.Evidence));
            blocks.Add(new("Normal", "建议\n" + i.Advice));
        }
        blocks.AddRange(new Block[] { new("Heading1", "示范改写"), new("Normal", f.Rewrite),
            new("Heading1", "下一步练习"), new("Normal", f.NextDrill),
            new("Heading1", "评价范围"), new("Normal", f.Limitations) });
        return blocks;
        void AddEvidence(IEnumerable<EvidenceReference>? references)
        {
            foreach (var e in references ?? []) {
                var verified = DiagnosticCatalog.ContainsQuote(DiagnosticCatalog.Source(r, e.Source), e.Quote);
                blocks.Add(new("Normal", $"原文依据 [{DiagnosticCatalog.SourceName(e.Source)}]{(verified ? "" : "（引文未核实）")}\n{e.Quote}{(string.IsNullOrWhiteSpace(e.Note) ? "" : "\n" + e.Note)}"));
            }
        }
    }

    public static string Text(PracticeRecord record) => string.Join("\r\n\r\n", Content(record).Select(b => b.Text.ReplaceLineEndings("\r\n"))) + "\r\n";
    public static string Markdown(PracticeRecord record) => string.Join("\n\n", Content(record).Select(b => (b.Style switch { "Title" => "# ", "Heading1" => "## ", "Heading2" => "### ", _ => "" }) + b.Text.ReplaceLineEndings("\n"))) + "\n";

    public static string Html(PracticeRecord record)
    {
        var html = new StringBuilder("""
            <!doctype html><html lang="zh-CN"><head><meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'">
            <title>BP Studio 发言复盘报告</title><style>
            @page { size: Letter portrait; }
            * { box-sizing: border-box; } html { color-scheme: light; }
            body { margin:0; color:#111; background:#fff; font:11.5pt/1.55 'Microsoft YaHei','Segoe UI',sans-serif; }
            h1,h2,h3 { color:#000; font-weight:700; line-height:1.35; break-after:avoid; overflow-wrap:anywhere; }
            h1 { font-size:24pt; margin:0 0 14pt; } h2 { font-size:15pt; margin:20pt 0 8pt; } h3 { font-size:12pt; margin:13pt 0 6pt; }
            p { white-space:pre-wrap; overflow-wrap:anywhere; margin:0 0 9pt; orphans:3; widows:3; }
            .meta { font-size:10pt; color:#444; } </style></head><body>
            """);
        foreach (var b in Content(record)) {
            var tag = b.Style switch { "Title" => "h1", "Heading1" => "h2", "Heading2" => "h3", _ => "p" };
            html.Append('<').Append(tag).Append(b.Style == "Meta" ? " class=\"meta\"" : "").Append('>')
                .Append(WebUtility.HtmlEncode(Clean(b.Text))).Append("</").Append(tag).Append('>');
        }
        return html.Append("</body></html>").ToString();
    }

    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static XElement E(string name, params object[] content) => new(W + name, content);
    private static XAttribute V(string value) => new(W + "val", value);
    // XML 1.0 rejects control characters, but supplementary characters must survive.
    private static string Clean(string value) => string.Concat(value.EnumerateRunes().Where(r => r.Value is 9 or 10 or 13 || r.Value >= 32 && r.Value is not (0xfffe or 0xffff)).Select(r => r.ToString()));
    private static XElement Paragraph(Block b)
    {
        var p = E("p", E("pPr", E("pStyle", V(b.Style))));
        var lines = Clean(b.Text).ReplaceLineEndings("\n").Split('\n');
        for (var n = 0; n < lines.Length; n++) {
            if (n > 0) p.Add(E("r", E("br")));
            p.Add(E("r", E("t", new XAttribute(XNamespace.Xml + "space", "preserve"), lines[n])));
        }
        return p;
    }

    public static void WriteDocx(Stream destination, PracticeRecord record)
    {
        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        void Part(string name, XDocument document) { using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open(); document.Save(stream); }
        XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
        XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
        Part("[Content_Types].xml", new(new XElement(ct + "Types",
            new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
            new[] { ("document", "document.main"), ("styles", "styles"), ("footer1", "footer") }.Select(x => new XElement(ct + "Override", new XAttribute("PartName", $"/word/{x.Item1}.xml"), new XAttribute("ContentType", $"application/vnd.openxmlformats-officedocument.wordprocessingml.{x.Item2}+xml"))))));
        XElement Relationship(string id, string type, string target) => new(rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", R.NamespaceName + "/" + type), new XAttribute("Target", target));
        Part("_rels/.rels", new(new XElement(rel + "Relationships", Relationship("rId1", "officeDocument", "word/document.xml"))));
        Part("word/_rels/document.xml.rels", new(new XElement(rel + "Relationships", Relationship("rId1", "styles", "styles.xml"), Relationship("rId2", "footer", "footer1.xml"))));
        XElement Style(string id, string name, string size, bool bold = false, bool heading = false) => E("style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", id),
            E("name", V(name)), id == "Normal" ? null! : E("basedOn", V("Normal")),
            E("pPr", E("spacing", new XAttribute(W + "before", heading ? "240" : "0"), new XAttribute(W + "after", "140"), new XAttribute(W + "line", "340"), new XAttribute(W + "lineRule", "auto")),
                heading || id == "Title" ? E("keepNext") : null!, E("widowControl"), heading ? E("outlineLvl", V(id == "Heading1" ? "0" : "1")) : null!),
            E("rPr", E("color", V("000000")), E("sz", V(size)), E("szCs", V(size)), bold ? E("b") : null!));
        Part("word/styles.xml", new(E("styles", E("docDefaults", E("rPrDefault", E("rPr", E("rFonts", new XAttribute(W + "ascii", "Calibri"), new XAttribute(W + "hAnsi", "Calibri"), new XAttribute(W + "eastAsia", "Microsoft YaHei")), E("lang", V("en-US"), new XAttribute(W + "eastAsia", "zh-CN"))))),
            Style("Normal", "Normal", "23"), Style("Title", "Title", "48", true), Style("Heading1", "heading 1", "30", true, true), Style("Heading2", "heading 2", "24", true, true), Style("Meta", "Metadata", "20"))));
        var body = E("body", Content(record).Select(Paragraph).ToArray());
        body.Add(E("sectPr", E("footerReference", new XAttribute(W + "type", "default"), new XAttribute(R + "id", "rId2")),
            E("pgSz", new XAttribute(W + "w", "12240"), new XAttribute(W + "h", "15840")),
            E("pgMar", new XAttribute(W + "top", "1440"), new XAttribute(W + "bottom", "1440"), new XAttribute(W + "left", "1440"), new XAttribute(W + "right", "1440"), new XAttribute(W + "header", "720"), new XAttribute(W + "footer", "720"))));
        Part("word/document.xml", new(E("document", new XAttribute(XNamespace.Xmlns + "w", W), new XAttribute(XNamespace.Xmlns + "r", R), body)));
        Part("word/footer1.xml", new(E("ftr", E("p", E("pPr", E("pStyle", V("Meta")), E("jc", V("center"))), E("r", E("t", "BP Studio  ·  ")), E("fldSimple", new XAttribute(W + "instr", "PAGE"), E("r", E("t", "1")))))));
    }
}
