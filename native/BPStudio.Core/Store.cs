using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace BPStudio.Core;
public sealed class Store
{
    private readonly SemaphoreSlim _writer = new(1, 1);
    public string DirectoryPath { get; }
    public string DataPath => Path.Combine(DirectoryPath, "bp-studio.json");
    public AppState State { get; private set; } = new();
    public string? Notice { get; private set; }
    public Store(string? directory = null) => DirectoryPath = directory ?? Environment.GetEnvironmentVariable("BP_NATIVE_PROFILE") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "bp-studio-native");
    public async Task LoadAsync(string? legacyDirectory = null)
    {
        Directory.CreateDirectory(DirectoryPath);
        var source = Path.Combine(legacyDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "bp-studio"), "bp-studio.json");
        var migrating = !File.Exists(DataPath) && File.Exists(source);
        var load = migrating ? source : DataPath;
        if (!File.Exists(load)) return;
        try
        {
            State = JsonSerializer.Deserialize<AppState>(await File.ReadAllTextAsync(load), Json.Options) ?? throw new JsonException();
            if (State.Settings is null || State.Records is null || State.CustomMotions is null || State.Secrets is null) throw new JsonException();
            State.NativeUi ??= new UiPreferences();
            if (migrating)
            {
                var backup = Path.Combine(DirectoryPath, "legacy-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")); Directory.CreateDirectory(backup);
                File.Copy(source, Path.Combine(backup, "bp-studio.json"));
                var localState = Path.Combine(Path.GetDirectoryName(source)!, "Local State"); if (File.Exists(localState)) File.Copy(localState, Path.Combine(backup, "Local State"));
                State.MigratedFrom = Path.GetDirectoryName(source); State.MigratedAt = DateTimeOffset.Now.ToString("O");
                foreach (var key in State.Secrets.Keys.ToArray()) { try { State.Secrets[key] = SecretProtection.Encrypt(SecretProtection.Decrypt(State.Secrets[key], State.MigratedFrom)); } catch { /* Preserve an unreadable legacy value for recovery. */ } }
                await SaveAsync(); Notice = $"已导入旧版 {State.CustomMotions.Count} 道自建辩题和 {State.Records.Count} 份复盘。旧文件已备份，原数据保持不变。";
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            File.Copy(load, Path.Combine(DirectoryPath, "unreadable-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".json"), true);
            throw new InvalidOperationException("数据文件无法读取，已保留原文件并备份。请检查数据文件，不要覆盖原有记录。", ex);
        }
    }
    public async Task SaveAsync()
    {
        var snapshot = JsonSerializer.Serialize(State, Json.Options);
        await _writer.WaitAsync();
        try { Directory.CreateDirectory(DirectoryPath); await File.WriteAllTextAsync(DataPath + ".tmp", snapshot, new UTF8Encoding(false)); File.Move(DataPath + ".tmp", DataPath, true); }
        finally { _writer.Release(); }
    }
    public string GetKey(string name)
    {
        if (!State.Secrets.TryGetValue(name, out var encrypted) || string.IsNullOrEmpty(encrypted)) return "";
        try { return SecretProtection.Decrypt(encrypted, State.MigratedFrom); }
        catch { throw new InvalidOperationException("此 API Key 无法在当前账户中解密。原凭据已保留，请在模型设置中重新填写该 Key。"); }
    }
    public void SetKey(string name, string? value, bool remove = false)
    {
        if (remove) { State.Secrets.Remove(name); return; }
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.Length > 4000) throw new InvalidOperationException("API Key 过长。");
        State.Secrets[name] = SecretProtection.Encrypt(value.Trim());
    }
    public async Task SaveMotionAsync(Motion motion, Catalog catalog)
    {
        Validation.Motion(motion);
        if (catalog.Motions.Concat(State.CustomMotions).Any(m => m.Key != motion.Key && Validation.TitleKey(m.Title) == Validation.TitleKey(motion.Title))) throw new InvalidOperationException("辩题库中已有相同辩题。");
        var index = State.CustomMotions.FindIndex(m => m.Key == motion.Key && motion.IsCustom);
        if (index < 0) { motion.Id = JsonSerializer.SerializeToElement(Guid.NewGuid().ToString()); motion.CreatedAt = DateTimeOffset.Now.ToString("O"); State.CustomMotions.Insert(0, motion); }
        else State.CustomMotions[index] = motion;
        await SaveAsync();
    }
}
public static class SecretProtection
{
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool CryptProtectData(ref Blob data, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptUnprotectData(ref Blob data, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] Dpapi(byte[] bytes, bool protect)
    {
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) }; Marshal.Copy(bytes, 0, input.Data, bytes.Length);
        Blob output = default;
        try
        {
            var ok = protect ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new CryptographicException(Marshal.GetLastWin32Error());
            var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally { Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) LocalFree(output.Data); }
    }
    public static string Encrypt(string value) => "native:" + Convert.ToBase64String(Dpapi(Encoding.UTF8.GetBytes(value), true));
    public static string Decrypt(string value, string? legacyDirectory)
    {
        if (value.StartsWith("native:", StringComparison.Ordinal)) return Encoding.UTF8.GetString(Dpapi(Convert.FromBase64String(value[7..]), false));
        var bytes = Convert.FromBase64String(value);
        var prefix = bytes.Length > 3 ? Encoding.ASCII.GetString(bytes, 0, 3) : "";
        if (prefix is "v10" or "v11")
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(legacyDirectory ?? throw new CryptographicException(), "Local State")));
            var wrapped = Convert.FromBase64String(document.RootElement.GetProperty("os_crypt").GetProperty("encrypted_key").GetString()!);
            if (!wrapped.AsSpan(0, 5).SequenceEqual("DPAPI"u8)) throw new CryptographicException();
            var key = Dpapi(wrapped[5..], false); var plain = new byte[bytes.Length - 3 - 12 - 16];
            try { using var aes = new AesGcm(key, 16); aes.Decrypt(bytes.AsSpan(3, 12), bytes.AsSpan(15, plain.Length), bytes.AsSpan(bytes.Length - 16), plain); return Encoding.UTF8.GetString(plain); }
            finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
        }
        return Encoding.UTF8.GetString(Dpapi(bytes, false));
    }
}
