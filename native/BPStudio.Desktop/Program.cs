using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Runtime.InteropServices;
using BPStudio.Core;

namespace BPStudio.Desktop;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var profile = new Store().DirectoryPath;
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(profile).ToUpperInvariant())))[..24];
        using var instance = new Mutex(true, "Local\\BPStudio.Native." + identity, out var first);
        if (!first)
        {
            var existing = FindWindow(null, MainWindow.WindowTitle);
            if (existing == IntPtr.Zero) existing = FindWindow(null, "BP Studio · 原生练习与复盘工作台");
            if (existing != IntPtr.Zero) { ShowWindow(existing, 9); SetForegroundWindow(existing); }
            return;
        }
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (_, e) => { Log(profile, e.Exception); e.Handled = true; };
            app.Run(new MainWindow());
        }
        catch (Exception e) { Log(profile, e); MessageBox.Show("BP Studio 启动未完成。请查看数据目录中的 desktop-error.log。", "BP Studio", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    internal static void Log(string profile, Exception error)
    {
        Directory.CreateDirectory(profile);
        File.AppendAllText(Path.Combine(profile, "desktop-error.log"), DateTimeOffset.Now.ToString("O") + " " + error + Environment.NewLine);
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
}
