using BPStudio.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Synthetic Chromium-compatible test data; no Electron installation or real user keys required.
internal static class LegacyFixture
{
    public static void Create(string directory)
    {
        Directory.CreateDirectory(directory);
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            var wrapped = ProtectRawKey(key);
            var localState = new { os_crypt = new { encrypted_key = Convert.ToBase64String("DPAPI"u8.ToArray().Concat(wrapped).ToArray()) } };
            File.WriteAllText(Path.Combine(directory, "Local State"), JsonSerializer.Serialize(localState));
            var draft = new Practice { Motion = "This House would make public transport free.", Transcript = "Madam Speaker, public transport should be free because it helps students travel to school and reduces traffic congestion.", Target = 420, Duration = 13 };
            var feedback = new Feedback {
                Summary = string.Concat(Enumerable.Repeat("这份合成反馈仅用于验证原生迁移与服务接口，不代表真实学生表现。需要比较机制、影响及双方世界。", 5)),
                Dimensions = new[] { "辩位履职", "论证深度", "反驳回应", "比较权衡", "语言表达" }.Select(name => new Dimension { Name = name, Score = name == "反驳回应" ? null : 3, Reason = "合成测试材料，证据有限。" }).ToList(),
                Strengths = ["提出明确的政策立场。"],
                Improvements = [new Improvement { Title = "解释机制", Evidence = "public transport should be free", Advice = "补充政策如何帮助学生通勤的具体机制。" }],
                Rewrite = "Free public transport helps students reach school while reducing traffic congestion.", NextDrill = "用一分钟解释政策机制。", Limitations = "仅有一段合成讲稿，无法判断整场比赛或语音表现。"
            };
            var state = new AppState {
                Settings = new ModelSettings { Model = "migration-test-model" }, Draft = draft,
                Secrets = new() { ["apiKey"] = Encrypt("native-migration-feedback-test", "v10", key), ["speechKey"] = Encrypt("native-migration-speech-test", "v11", key) },
                Records = [new PracticeRecord { Id = "migration-record", Motion = draft.Motion, Transcript = draft.Transcript, Target = draft.Target, Duration = draft.Duration, Feedback = feedback, Model = "migration-test-model" }],
                CustomMotions = [new Motion { Id = JsonSerializer.SerializeToElement("migration-motion"), Title = "This House would fund student journalism.", Category = "教育", Level = "进阶", Tags = ["校园"] }],
                Extra = new() { ["legacyExtra"] = JsonSerializer.SerializeToElement(new { preserve = true }) }
            };
            File.WriteAllText(Path.Combine(directory, "bp-studio.json"), JsonSerializer.Serialize(state, Json.Options));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    private static string Encrypt(string text, string prefix, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); var plain = Encoding.UTF8.GetBytes(text);
        var cipher = new byte[plain.Length]; var tag = new byte[16];
        using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String(Encoding.ASCII.GetBytes(prefix).Concat(nonce).Concat(cipher).Concat(tag).ToArray());
    }
    private static byte[] ProtectRawKey(byte[] key)
    {
        var input = new Blob { Length = key.Length, Data = System.Runtime.InteropServices.Marshal.AllocHGlobal(key.Length) };
        System.Runtime.InteropServices.Marshal.Copy(key, 0, input.Data, key.Length); Blob output = default;
        try {
            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)) throw new CryptographicException(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            var bytes = new byte[output.Length]; System.Runtime.InteropServices.Marshal.Copy(output.Data, bytes, 0, bytes.Length); return bytes;
        } finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) LocalFree(output.Data); }
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
    [System.Runtime.InteropServices.DllImport("crypt32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)] private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
