using BPStudio.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;

namespace BPStudio.Desktop;

public static class PdfReportWriter
{
    public static async Task WriteAsync(Grid owner, CoreWebView2Environment environment, PracticeRecord record, string path, CancellationToken token)
    {
        // A separate renderer never navigates, scales or changes the user's workspace.
        using var view = new WebView2 { Width = 816, Height = 1056, Visibility = Visibility.Hidden, IsHitTestVisible = false };
        owner.Children.Insert(0, view);
        try {
            await view.EnsureCoreWebView2Async(environment).WaitAsync(TimeSpan.FromSeconds(30), token);
            var core = view.CoreWebView2;
            core.Settings.IsScriptEnabled = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            core.NavigationCompleted += (_, e) => { if (e.IsSuccess) loaded.TrySetResult(); else loaded.TrySetException(new InvalidOperationException("报告排版未能完成，请重试导出。")); };
            core.NavigateToString(ReportDocuments.Html(record));
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
            var settings = environment.CreatePrintSettings();
            settings.PageWidth = 8.5; settings.PageHeight = 11;
            settings.MarginTop = settings.MarginBottom = settings.MarginLeft = settings.MarginRight = 1;
            settings.ScaleFactor = 1;
            settings.ShouldPrintBackgrounds = false;
            settings.ShouldPrintHeaderAndFooter = true;
            settings.HeaderTitle = "BP Studio 发言复盘报告";
            settings.FooterUri = " ";
            if (!await core.PrintToPdfAsync(path, settings).WaitAsync(TimeSpan.FromMinutes(2), token))
                throw new InvalidOperationException("PDF 导出未完成，请重试或选择其他格式。");
        }
        finally { owner.Children.Remove(view); }
    }
}
