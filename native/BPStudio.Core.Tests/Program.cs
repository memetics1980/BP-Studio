using BPStudio.Core;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Security.Cryptography;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : "../../..");
var profile = Path.Combine(root, "test-results", "native-core-" + Guid.NewGuid().ToString("N"));
var fixturePath = Environment.GetEnvironmentVariable("BP_NATIVE_LEGACY_FIXTURE") ?? profile + "-legacy";
if (Environment.GetEnvironmentVariable("BP_NATIVE_LEGACY_FIXTURE") is null) LegacyFixture.Create(fixturePath);
var passed = new List<string>();
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Pass(string name) { passed.Add(name); Console.WriteLine("PASS " + name); }
var catalog = Catalog.Load(); Check(catalog.Motions.Count == 12 && catalog.Roles.Count == 8, "Catalog incomplete"); Pass("all original motions and eight BP roles preserved");
Check(SecretProtection.Decrypt(SecretProtection.Encrypt("synthetic-native-key"), null) == "synthetic-native-key", "Native DPAPI roundtrip failed"); Pass("native Windows encrypted credential roundtrip");
var sourceHash = SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(fixturePath, "bp-studio.json")));
var store = new Store(profile); await store.LoadAsync(fixturePath);
Check(store.State.Records.Count == 1 && store.State.CustomMotions.Count == 1 && store.State.Draft!.Transcript.Length > 30 && store.State.Settings.Model == "migration-test-model", "Legacy data lost");
Check(store.State.Extra!["legacyExtra"].GetProperty("preserve").GetBoolean(), "Unknown fields lost");
Check(sourceHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(fixturePath, "bp-studio.json")))), "Legacy file modified");
Check(Directory.GetDirectories(profile, "legacy-backup-*").Length == 1, "Backup missing"); Pass("migration preserves old data, unknown fields and immutable legacy backup");
Check(store.GetKey("apiKey") == "native-migration-feedback-test" && store.GetKey("speechKey") == "native-migration-speech-test", "Electron credential decryption failed"); Pass("Chromium-compatible v10/v11 encrypted keys migrate without Electron");
store.SetKey("apiKey", "updated-native-key"); await store.SaveAsync(); var disk = await File.ReadAllTextAsync(store.DataPath); Check(!disk.Contains("updated-native-key"), "Plaintext key on disk");
var reload = new Store(profile); await reload.LoadAsync(); Check(reload.GetKey("apiKey") == "updated-native-key" && reload.GetKey("speechKey") == "native-migration-speech-test", "Reload lost credentials"); Pass("native and legacy credentials coexist and survive reload without plaintext storage");

var oldPreferences = JsonSerializer.Deserialize<UiPreferences>("{\"readerFontSize\":19,\"compareRatio\":0.4}", Json.Options)!;
Check(oldPreferences.Theme == "system" && oldPreferences.ReaderFontSize == 19, "Older appearance settings did not default to system");
var settingsBeforeTheme = JsonSerializer.Serialize(store.State.Settings, Json.Options);
var draftBeforeTheme = JsonSerializer.Serialize(store.State.Draft, Json.Options);
store.State.NativeUi = oldPreferences;
foreach (var theme in new[] { "light", "dark", "system" }) {
    store.State.NativeUi.Theme = theme; await store.SaveAsync(); var themeReload = new Store(profile); await themeReload.LoadAsync();
    Check(themeReload.State.NativeUi.Theme == theme && themeReload.State.NativeUi.ReaderFontSize == 19 && themeReload.State.NativeUi.CompareRatio == .4, "Appearance preference did not survive restart");
    Check(JsonSerializer.Serialize(themeReload.State.Settings, Json.Options) == settingsBeforeTheme && JsonSerializer.Serialize(themeReload.State.Draft, Json.Options) == draftBeforeTheme, "Appearance save changed model settings or draft");
}
Check(JsonSerializer.Deserialize<UiPreferences>("{\"theme\":\"unsupported\"}", Json.Options)!.Theme == "system", "Invalid theme did not fall back to system");
Pass("appearance migration and three-theme persistence preserve other settings and draft");

var motion = new Motion { Title = "This House would provide free school meals to all students.", Category = "教育", Level = "入门", Tags = ["校园"] }; await store.SaveMotionAsync(motion, catalog); Check(motion.IsCustom && store.State.CustomMotions.Count == 2, "Custom motion creation failed");
try { await store.SaveMotionAsync(new Motion { Title = motion.Title.ToUpperInvariant(), Category = "教育", Level = "入门" }, catalog); throw new Exception("Duplicate accepted"); } catch (InvalidOperationException) { }
motion.Zh = "本院将为学生提供免费校餐。"; await store.SaveMotionAsync(motion, catalog); Check(store.State.CustomMotions.Count == 2, "Edit duplicated item"); Pass("custom motion creation, duplicate detection and update");
foreach (var address in new[] { "http://example.com", "https://user:secret@example.com", "https://example.com?token=a" }) { try { Validation.Endpoint(address, "/chat/completions"); throw new Exception("Unsafe address accepted"); } catch (InvalidOperationException) { } } Pass("endpoint validation preserves service prefixes and protects remote credentials");
var feedbackJson = JsonSerializer.Serialize(store.State.Records[0].Feedback, Json.Options); Check(ModelService.ParseFeedback("```json\n" + feedbackJson + "\n```").Dimensions.Count == 5, "JSON parsing failed");
try { ModelService.ParseFeedback("{}"); throw new Exception("Invalid feedback accepted"); } catch (InvalidOperationException) { } Pass("structured feedback validation and nullable formative dimensions");
// Generated feedback now requires all nine diagnostics; the persisted legacy fixture remains unchanged.
var generatedFixture = Json.Clone(store.State.Records[0].Feedback);
generatedFixture.Version = 2; generatedFixture.Diagnostics = FeedbackFixture.Create().Feedback.Diagnostics;
generatedFixture.PriorityIssues = FeedbackFixture.Create().Feedback.PriorityIssues;
feedbackJson = JsonSerializer.Serialize(generatedFixture, Json.Options);
var port = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); port.Start(); var number = ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{number}/"); listener.Start(); var captured = new List<string>(); using var serverCancel = new CancellationTokenSource();
var server = Task.Run(async () => { while (!serverCancel.IsCancellationRequested) { HttpListenerContext context; try { context = await listener.GetContextAsync().WaitAsync(serverCancel.Token); } catch { break; } var body = await new StreamReader(context.Request.InputStream).ReadToEndAsync(); captured.Add(body); string response; if (context.Request.Url!.AbsolutePath == "/api/tags") response = "{\"models\":[{\"name\":\"test-model\"}]}"; else if (context.Request.Url.AbsolutePath.Contains("transcriptions")) response = "{\"text\":\"Public transport should be free.\",\"duration\":13}"; else response = JsonSerializer.Serialize(new { message = new { content = feedbackJson }, choices = new[] { new { message = new { content = feedbackJson } } } }); var bytes = Encoding.UTF8.GetBytes(response); context.Response.ContentType = "application/json"; await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close(); } });
using var models = new ModelService(catalog); var settings = new ModelSettings { BaseUrl = $"http://127.0.0.1:{number}", SpeechUrl = $"http://127.0.0.1:{number}/v1", Model = "test-model" };
Check((await models.ProbeAsync(settings, "", default)).Contains("已连接"), "Ollama probe failed"); var practiceRecord = await models.FeedbackAsync(settings, store.State.Draft!, "", default); Check(practiceRecord.Feedback.Dimensions.Count == 5, "Ollama response failed"); settings.Provider = "api"; settings.BaseUrl += "/v1"; practiceRecord = await models.FeedbackAsync(settings, store.State.Draft!, "synthetic-key", default); Check(practiceRecord.Feedback.Summary.Length > 100, "API response failed"); Pass("real HTTP Ollama and compatible API adapters");
foreach (var ext in new[] { "mp3", "wav", "wave", "m4a", "flac", "ogg" }) { var audio = Path.Combine(root, "test-results", "audio-fixtures", "练习音频." + ext); var result = await models.TranscribeAsync(settings, audio, "synthetic-speech-key", default); Check(result.Duration == 13, "Audio response failed"); Check(captured[^1].Contains("speech." + (ext == "wave" ? "wav" : ext)), "Wrong multipart extension"); } Pass("six-format streamed multipart API uploads retain actual extension");
var speechSettings = new ModelSettings(); var speech = new SpeechService();
if (!args.Contains("--quick")) { Check((await speech.RunAsync(speechSettings, null, default)).Ready, "Speech probe failed"); foreach (var ext in new[] { "mp3", "wav", "wave", "m4a", "flac", "ogg" }) { var audio = Path.Combine(root, "test-results", "audio-fixtures", "练习音频." + ext); var result = await speech.RunAsync(speechSettings, audio, default); Check(result.Text.Contains("public transport", StringComparison.OrdinalIgnoreCase), "Actual speech recognition failed for " + ext); } Pass("real local Whisper base decoding and recognition for six formats"); }
using (var canceled = new CancellationTokenSource()) { canceled.Cancel(); try { await speech.RunAsync(speechSettings, null, canceled.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { } } Pass("speech process cancellation");
Check(Reports.Markdown(practiceRecord).Contains("## 发言原文") && !Reports.Markdown(practiceRecord).Contains("synthetic-key"), "Report invalid"); Pass("complete report export without credentials");
serverCancel.Cancel(); listener.Stop(); await server;
if (args.Contains("--audit-real")) { var oldDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "bp-studio"); var oldFile = Path.Combine(oldDirectory, "bp-studio.json"); if (File.Exists(oldFile)) { var bytes = await File.ReadAllBytesAsync(oldFile); var old = JsonSerializer.Deserialize<AppState>(bytes, Json.Options)!; var audit = new Store(Path.Combine(root, "test-results", "native-real-audit-" + Guid.NewGuid().ToString("N"))); await audit.LoadAsync(oldDirectory); Check(JsonSerializer.Serialize(old.Records, Json.Options) == JsonSerializer.Serialize(audit.State.Records, Json.Options), "Real history mismatch"); Check(JsonSerializer.Serialize(old.CustomMotions, Json.Options) == JsonSerializer.Serialize(audit.State.CustomMotions, Json.Options), "Real motions mismatch"); Check(JsonSerializer.Serialize(old.Draft, Json.Options) == JsonSerializer.Serialize(audit.State.Draft, Json.Options), "Real draft mismatch"); Check(JsonSerializer.Serialize(old.Settings, Json.Options) == JsonSerializer.Serialize(audit.State.Settings, Json.Options), "Real settings mismatch"); foreach (var key in old.Secrets.Keys) { if (string.IsNullOrEmpty(old.Secrets[key])) continue; Check(audit.GetKey(key) == SecretProtection.Decrypt(old.Secrets[key], oldDirectory), "Real credential mismatch"); } Check(bytes.SequenceEqual(await File.ReadAllBytesAsync(oldFile)), "Real source modified"); Pass("actual user legacy data matches native migration and source remains unchanged"); } }
await File.WriteAllTextAsync(Path.Combine(root, "test-results", args.Contains("--quick") ? "native-regression-report.json" : "native-core-report.json"), JsonSerializer.Serialize(new { passed = true, cases = passed, count = passed.Count, testedAt = DateTimeOffset.Now }, Json.Options));
Console.WriteLine($"All {passed.Count} native service checks passed.");

