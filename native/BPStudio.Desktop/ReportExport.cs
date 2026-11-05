using BPStudio.Core;
using Microsoft.Win32;
using System.Text;

namespace BPStudio.Desktop;

public sealed partial class MainWindow
{
    private bool _exporting;
    private async Task<object> ExportReportAsync(PracticeRecord record, string format)
    {
        RequireIdle();
        var label = format switch { "md" => "Markdown 文档", "txt" => "纯文本", "docx" => "Word 文档", "pdf" => "PDF 文档", _ => throw new InvalidOperationException("请先选择 Markdown、TXT、Word 或 PDF 导出格式。") };
        if (_exporting) throw new InvalidOperationException("请等待当前报告导出完成。");
        _exporting = true;
        try {
            var save = new SaveFileDialog {
                Title = "保存 " + label, Filter = $"{label} (*.{format})|*.{format}",
                FileName = "BP-" + record.Role + "-" + DateTime.Now.ToString("yyyyMMdd"),
                DefaultExt = "." + format, AddExtension = true, OverwritePrompt = true, CheckPathExists = true
            };
            if (save.ShowDialog(this) != true) return new { canceled = true };
            var extension = Path.GetExtension(save.FileName).ToLowerInvariant();
            if (extension != "." + format) throw new InvalidOperationException($"当前选择了 {label}，请使用 .{format} 文件扩展名，或返回重新选择格式。");
            // Write beside the destination, then replace only after successful completion.
            // A canceled or failed export cannot truncate an existing report.
            var temporary = Path.Combine(Path.GetDirectoryName(save.FileName)!, ".bp-export-" + Guid.NewGuid().ToString("N") + extension);
            try {
                await RunJobAsync("正在导出 " + extension[1..].ToUpperInvariant() + " 报告…", async token => {
                    switch (extension) {
                        case ".md": await File.WriteAllTextAsync(temporary, Reports.Markdown(record), new UTF8Encoding(false), token); break;
                        case ".txt": await File.WriteAllTextAsync(temporary, ReportDocuments.Text(record), new UTF8Encoding(true), token); break;
                        case ".docx":
                            await Task.Run(() => { using var file = File.Create(temporary); ReportDocuments.WriteDocx(file, record); }, token); break;
                        case ".pdf": await PdfReportWriter.WriteAsync(_root, _web.CoreWebView2.Environment, record, temporary, token); break;
                    }
                    token.ThrowIfCancellationRequested();
                    File.Move(temporary, save.FileName, overwrite: true);
                    return new { saved = true };
                });
            }
            finally { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            Notify("已导出 " + extension[1..].ToUpperInvariant() + " 复盘报告");
            return new { saved = true, format = extension[1..] };
        }
        finally { _exporting = false; }
    }
}
