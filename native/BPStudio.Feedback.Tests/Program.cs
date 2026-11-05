using BPStudio.Core;
using System.Net;
using System.Text;
using System.Text.Json;

var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
var checks = new List<string>();
void Check(bool value, string name) { if (!value) throw new Exception(name); checks.Add(name); Console.WriteLine("PASS " + name); }
void Reject(Action action, string name) { try { action(); } catch (InvalidOperationException) { Check(true, name); return; } throw new Exception("Accepted invalid input: " + name); }
var record = FeedbackFixture.Create(); var catalog = Catalog.Load();
var raw = JsonSerializer.Serialize(record.Feedback, Json.Options);
var parsed = ModelService.ParseFeedback(raw, true);
Check(parsed.Diagnostics.Count == 9 && parsed.Dimensions.Count == 5 && parsed.Improvements.Count == 2 && parsed.PriorityIssues.Count == 2, "Nine diagnostics retain all original coaching fields");
Check(parsed.Diagnostics.All(d => d.Evidence.All(e => !e.Verified)), "Model cannot self-certify evidence");
DiagnosticCatalog.VerifyEvidence(parsed, record);
Check(parsed.Diagnostics.SelectMany(d => d.Evidence).All(e => e.Verified), "All fixture quotes checked against correct source");
var broken = Json.Clone(record.Feedback); broken.Diagnostics.RemoveAt(0);
Reject(() => ModelService.ParseFeedback(JsonSerializer.Serialize(broken, Json.Options), true), "Missing diagnostic is rejected");
broken = Json.Clone(record.Feedback); broken.Diagnostics[1].Id = broken.Diagnostics[0].Id;
Reject(() => ModelService.ParseFeedback(JsonSerializer.Serialize(broken, Json.Options), true), "Duplicate diagnostic is rejected");
broken = Json.Clone(record.Feedback); broken.Diagnostics[0].Status = "bad";
Reject(() => ModelService.ParseFeedback(JsonSerializer.Serialize(broken, Json.Options), true), "Invalid assessment status is rejected");
broken = Json.Clone(record.Feedback); broken.Diagnostics[0].Evidence[0].Source = "previous:NOT-A-ROLE";
Reject(() => ModelService.ParseFeedback(JsonSerializer.Serialize(broken, Json.Options), true), "Invalid evidence source is rejected");
broken = Json.Clone(record.Feedback); broken.Diagnostics[0].Details[0].Content = "";
Reject(() => ModelService.ParseFeedback(JsonSerializer.Serialize(broken, Json.Options), true), "Incomplete detail is rejected");
var wrong = new EvidenceReference { Source = "previous:PM", Quote = "Many low-income commuters cannot afford regular tickets.", Verified = true };
var fake = new EvidenceReference { Source = "transcript", Quote = "This quote was invented.", Verified = true };
var future = new EvidenceReference { Source = "previous:MG", Quote = "FUTURE-MATERIAL-MUST-NOT-REACH-MODEL", Verified = true };
broken = Json.Clone(record.Feedback); broken.Diagnostics[0].Evidence = [wrong, fake, future];
DiagnosticCatalog.VerifyEvidence(broken, record);
Check(broken.Diagnostics[0].Evidence.All(e => !e.Verified && e.Note.Contains("未能")), "Wrong-source, invented and future quotes are not verified");
Check(DiagnosticCatalog.ContainsQuote(record.Transcript, "consider LOW-INCOME\n commuters on the urban fringe."), "Quote matching tolerates case and whitespace without changing source");
Check(DiagnosticCatalog.ActivePrevious(record).Select(s => s.Role).SequenceEqual(new[] { "PM", "LO", "DPM" }), "Only actual preceding roles enter analysis in order");
var roleCopy = Json.Clone(record); roleCopy.Role = "PM";
Check(!DiagnosticCatalog.ActivePrevious(roleCopy).Any() && roleCopy.PreviousSpeeches.Count == 4, "Role change retains future material but excludes it from analysis");
Validation.Practice(record, catalog);
var badPractice = Json.Clone(record); badPractice.PreviousSpeeches.Add(Json.Clone(record.PreviousSpeeches[0]));
Reject(() => Validation.Previous(badPractice), "Duplicate previous-role inputs rejected");
badPractice = Json.Clone(record); badPractice.PreviousSpeeches[0].Kind = "unknown";
Reject(() => Validation.Previous(badPractice), "Unknown material kind rejected");
badPractice = Json.Clone(record); badPractice.PreviousSpeeches[0].Text = new string('x', 30001);
Reject(() => Validation.Previous(badPractice), "Per-role input size checked");
badPractice = Json.Clone(record); foreach (var s in badPractice.PreviousSpeeches) s.Text = new string('x', 25000);
Reject(() => Validation.Previous(badPractice), "Aggregate input size checked");
badPractice = Json.Clone(record); badPractice.PreviousSpeeches = null!;
Reject(() => Validation.Previous(badPractice), "Null previous inputs rejected safely");
var oldJson = JsonSerializer.SerializeToNode(record, Json.Options)!;
oldJson.AsObject().Remove("previousSpeeches"); var oldFeedback = oldJson["feedback"]!.AsObject();
oldFeedback.Remove("version"); oldFeedback.Remove("diagnostics"); oldFeedback.Remove("priorityIssues");
oldJson["futureRecord"] = "preserve unknown data";
var legacy = oldJson.Deserialize<PracticeRecord>(Json.Options)!; legacy.Id = "legacy-fixture";
Check(legacy.PreviousSpeeches.Count == 0 && legacy.Feedback.Diagnostics.Count == 0 && legacy.Feedback.Version == 1, "Legacy records default to empty diagnostics and previous input");
Check(ModelService.ParseFeedback(oldFeedback.ToJsonString()).Dimensions.Count == 5, "Legacy feedback still parses outside new generation");
Reject(() => ModelService.ParseFeedback(oldFeedback.ToJsonString(), true), "New generation cannot silently accept legacy-only feedback");
var store = new Store(Path.Combine(output, "profile")); store.State.Draft = Json.Clone<Practice>(record); store.State.Records = [record, legacy];
store.State.Extra = new() { ["futureRoot"] = JsonSerializer.SerializeToElement("keep") };
await store.SaveAsync(); var reload = new Store(store.DirectoryPath); await reload.LoadAsync();
Check(reload.State.Records.Count == 2 && reload.State.Records[0].Feedback.Diagnostics.Count == 9 && reload.State.Draft!.PreviousSpeeches.Count == 4 && reload.State.Records[1].Feedback.Diagnostics.Count == 0, "New and legacy records and draft survive save/reload together");
Check(reload.State.Records[1].Extra!["futureRecord"].GetString() == "preserve unknown data" && reload.State.Extra!["futureRoot"].GetString() == "keep", "Unknown fields survive mixed-version save/reload");
foreach (var text in new[] { Reports.Markdown(record), ReportDocuments.Text(record), ReportDocuments.Html(record) }) {
    Check(DiagnosticCatalog.Items.All(i => text.Contains(i.Name)) && text.Contains(record.PreviousSpeeches[1].Text) && text.Contains("END-OF-DEEP-REPORT") && text.Contains("用户概述"), "Shared export contains nine diagnoses, previous material and original sections");
}
Check(Reports.Markdown(legacy).Contains("此记录生成于旧版") && Reports.Markdown(legacy).Contains(legacy.Feedback.Rewrite), "Legacy export explains unavailable diagnostics and retains feedback");
Check(!ReportDocuments.Html(record).Contains("<script>") && ReportDocuments.Html(record).Contains("&lt;script&gt;"), "Report HTML escapes literal source content");

var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port); listener.Start();
using var models = new ModelService(catalog);
foreach (var provider in new[] { "api", "ollama" }) {
    var server = Task.Run(async () => {
        using var client = await listener.AcceptTcpClientAsync(); using var stream = client.GetStream();
        var headerBytes = new List<byte>(); var one = new byte[1];
        while (true) { if (await stream.ReadAsync(one) == 0) throw new IOException("Incomplete HTTP header"); headerBytes.Add(one[0]); if (headerBytes.Count >= 4 && headerBytes.TakeLast(4).SequenceEqual(new byte[] {13,10,13,10})) break; }
        var requestHeader = Encoding.ASCII.GetString(headerBytes.ToArray());
        var length = int.Parse(requestHeader.Split("\r\n").First(x => x.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))[15..].Trim());
        var payload = new byte[length]; await stream.ReadExactlyAsync(payload);
        var body = Encoding.UTF8.GetString(payload);
        var response = JsonSerializer.Serialize(new { message = new { content = raw }, choices = new[] { new { message = new { content = raw } } } });
        var bytes = Encoding.UTF8.GetBytes(response); var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header); await stream.WriteAsync(bytes); return body;
    });
    var result = await models.FeedbackAsync(new() { Provider = provider, BaseUrl = $"http://127.0.0.1:{port}", Model = "fixture" }, record, "", default);
    var sent = await server;
    using var request = JsonDocument.Parse(sent); var userText = request.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
    Check(!userText.Contains("FUTURE-MATERIAL") && result.PreviousSpeeches.Count == 4, provider + " sends earlier roles only but preserves all saved inputs");
    Check(result.Feedback.Diagnostics.Count == 9 && result.Feedback.Diagnostics.SelectMany(d => d.Evidence).All(e => e.Verified), provider + " actual HTTP roundtrip validates and verifies deep feedback");
}
listener.Stop();
foreach (var role in DiagnosticCatalog.Roles) {
    var p = Json.Clone(record); p.Role = role;
    Check(models.SystemPrompt(p, "zh").Contains("Speaker " + role) && models.SystemPrompt(p, "en").Contains("English"), role + " uses selected role and feedback language");
}
Check(models.SystemPrompt(record, "zh").Contains("Missing material is not missing ability") && models.SystemPrompt(record, "zh").Contains("no unique prewritten answer") && models.SystemPrompt(record, "zh").Contains("not automatically a slippery slope"), "Coaching safeguards cover context, alternative cases and cautious fallacy judgments");
await File.WriteAllTextAsync(Path.Combine(output, "deep-fixture.json"), JsonSerializer.Serialize(record, Json.Options));
await File.WriteAllTextAsync(Path.Combine(output, "legacy-fixture.json"), JsonSerializer.Serialize(legacy, Json.Options));
await File.WriteAllTextAsync(Path.Combine(output, "core-report.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count, checks }, Json.Options));
Console.WriteLine($"Passed {checks.Count} feedback checks.");
