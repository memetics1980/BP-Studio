using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace BPStudio.Desktop;

internal static class BundledWebView
{
    public static string? Resolve()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "webview-runtime");
        if (!Directory.Exists(folder)) return null; // Development can use the installed Evergreen runtime.
        if (!File.Exists(Path.Combine(folder, "msedgewebview2.exe")))
            throw new InvalidOperationException("程序的界面运行环境不完整。请将压缩包完整解压到本机文件夹后再启动。");
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) {
            // Microsoft's fixed-runtime deployment requires these read-only AppContainer
            // permissions on Windows 10. Apply only to this app's runtime directory.
            var directory = new DirectoryInfo(folder);
            var security = directory.GetAccessControl();
            bool changed = false;
            foreach (var sid in new[] { "S-1-15-2-1", "S-1-15-2-2" }) {
                var identity = new SecurityIdentifier(sid);
                var rights = FileSystemRights.ReadAndExecute;
                var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                bool exists = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Any(r =>
                    r.IdentityReference == identity && r.AccessControlType == AccessControlType.Allow &&
                    (r.FileSystemRights & rights) == rights && (r.InheritanceFlags & inheritance) == inheritance);
                if (exists) continue;
                security.AddAccessRule(new FileSystemAccessRule(identity, rights, inheritance, PropagationFlags.None, AccessControlType.Allow));
                changed = true;
            }
            if (changed) directory.SetAccessControl(security);
        }
        return folder;
    }
}
