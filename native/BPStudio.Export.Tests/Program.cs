using BPStudio.Core;
using BPStudio.Desktop;
using Microsoft.Web.WebView2.Core;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        var app = new Application(); var grid = new Grid(); var window = new Window { Content = grid, Width = 640, Height = 480, ShowInTaskbar = false, ShowActivated = false, Title = "BP export tests" };
        int result = 0;
        window.Loaded += async (_, _) => {
            var checks = new List<string>();
            void Check(bool value, string name) { if (!value) throw new Exception(name); checks.Add(name); }
            try {
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(output, "renderer"));
                foreach (var name in new[] { "双语报告", "长篇报告", "深度诊断报告" }) {
                    var paragraph = "Public transport should be free for all citizens. Affordable fares connect people to schools, hospitals and employment. Compare alternatives and explain the causal mechanism.\n公共交通应当帮助更多人获得教育、医疗和工作机会。需要比较替代政策，并说明政策机制如何带来影响。";
                    var record = new PracticeRecord {
                        Id = name, CreatedAt = "2026-11-04T12:00:00+08:00", Role = "PM", Model = "export-fixture", Provider = "api",
                        Motion = "This House would make public transport free. 免费公共交通", Transcript = name == "长篇报告" ? string.Join("\n\n", Enumerable.Range(1, 16).Select(n => $"{n:00} {paragraph}")) : paragraph,
                        Context = "Opposition: subsidies have an opportunity cost.\n反方认为补贴存在机会成本。\nLiteral text: <script>window.injected = true</script> & \"quoted\" 😊",
                        Feedback = new Feedback {
                            Summary = "The position is clear. Explain why reduced fares improve access and compare this impact with the costs.\n立场清晰，应补充机制与权衡。",
                            Dimensions = new() { new() { Name = "论证深度", Score = 3, Reason = "The causal link needs one concrete example. 补充一个具体例子。" }, new() { Name = "反驳回应", Score = null, Reason = "Evidence is limited to this speech. 仅根据所提供的发言判断。" } },
                            Strengths = new() { "Clear opening position. 开篇立场清晰。" }, Improvements = new() { new() { Title = "解释因果机制", Evidence = "Affordable fares connect people to schools.", Advice = "Identify who benefits and why.\n说明哪些群体受益，以及原因。" } },
                            Rewrite = "Affordable fares expand opportunity by reducing the cost of reaching essential services.\n\n较低票价能够降低使用公共服务的成本。",
                            NextDrill = "Give a 60 second explanation using one example.\n用一个例子完成 60 秒机制说明。", Limitations = "Synthetic export fixture. 本报告为导出功能测试样本。END-OF-REPORT"
                        }
                    };
                    if (name == "深度诊断报告") record = FeedbackFixture.Create();
                    File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(record, Json.Options));
                    File.WriteAllText(Path.Combine(output, name + ".md"), Reports.Markdown(record), new UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(output, name + ".txt"), ReportDocuments.Text(record), new UTF8Encoding(true));
                    var docx = Path.Combine(output, name + ".docx");
                    using (var file = File.Create(docx)) ReportDocuments.WriteDocx(file, record);
                    using (var zip = ZipFile.OpenRead(docx)) {
                        var document = XDocument.Load(zip.GetEntry("word/document.xml")!.Open());
                        var text = string.Concat(document.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value));
                        Check(name == "深度诊断报告" ? text.Contains("END-OF-DEEP-REPORT") && DiagnosticCatalog.Items.All(i => text.Contains(i.Name)) && text.Contains("用户概述") && text.Contains("未参与本次分析") : text.Contains("END-OF-REPORT") && text.Contains("证据不足") && text.Contains("<script>") && text.Contains("😊"), name + " DOCX complete Unicode and literal content");
                        foreach (var entry in zip.Entries) using (var stream = entry.Open()) XDocument.Load(stream);
                        Check(true, name + " OOXML parts well formed");
                    }
                    var html = ReportDocuments.Html(record);
                    Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;"), name + " PDF content escaped");
                    var pdf = Path.Combine(output, name + ".pdf");
                    await PdfReportWriter.WriteAsync(grid, environment, record, pdf, CancellationToken.None);
                    Check(new FileInfo(pdf).Length > 20000 && Encoding.ASCII.GetString(File.ReadAllBytes(pdf)[..5]) == "%PDF-", name + " PDF written by production renderer");
                }
                using (var stream = new MemoryStream()) ReportDocuments.WriteDocx(stream, new PracticeRecord { Transcript = "control\u0001safe\n" + new string('X', 2000) });
                Check(true, "XML controls and long unbroken text supported");
                File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new { passed = true, checks }, Json.Options));
                Console.WriteLine($"Passed {checks.Count} export checks.");
            } catch (Exception e) { result = 1; Console.Error.WriteLine(e); }
            finally { window.Close(); app.Shutdown(result); }
        };
        app.Run(window); return result;
    }
}
