using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace BPStudio.Desktop;

public sealed partial class MainWindow
{
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(new Action(FitCurrentMonitor));
    }
    private void FitCurrentMonitor()
    {
        var handle = new WindowInteropHelper(this).Handle; if (handle == IntPtr.Zero || WindowState != WindowState.Normal) return;
        var monitor = MonitorFromWindow(handle, 2); var info = new MonitorInformation { Size = Marshal.SizeOf<MonitorInformation>() };
        if (!GetMonitorInfo(monitor, ref info) || !GetWindowRect(handle, out var bounds)) return;
        var dpi = VisualTreeHelper.GetDpi(this); var work = info.Work;
        MinWidth = Math.Min(640, (work.Right - work.Left) / dpi.DpiScaleX); MinHeight = Math.Min(480, (work.Bottom - work.Top) / dpi.DpiScaleY);
        var width = Math.Min(bounds.Right - bounds.Left, work.Right - work.Left); var height = Math.Min(bounds.Bottom - bounds.Top, work.Bottom - work.Top);
        var x = Math.Clamp(bounds.Left, work.Left, work.Right - width); var y = Math.Clamp(bounds.Top, work.Top, work.Bottom - height);
        if (x != bounds.Left || y != bounds.Top || width != bounds.Right - bounds.Left || height != bounds.Bottom - bounds.Top) SetWindowPos(handle, IntPtr.Zero, x, y, width, height, 0x0014);
    }
    private bool IsPerMonitorV2() => AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(new WindowInteropHelper(this).Handle), new IntPtr(-4));
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInformation { public int Size; public NativeRect Monitor, Work; public int Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, int flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInformation info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, int flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
}
