using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Services;

public class ExportService : IExportService
{
    private readonly ILogger<ExportService> _logger;

    public ExportService(ILogger<ExportService> logger)
    {
        _logger = logger;
    }

    public async Task ExportToCsvAsync(List<UninstallReport> reports, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AppName,Version,Publisher,UninstallDate,Succeeded,ExitCode,TracesFound,TracesRemoved");

        foreach (var report in reports)
        {
            sb.AppendLine($"\"{report.AppName}\",\"{report.AppVersion}\",\"{report.Publisher}\",{report.UninstallDate:yyyy-MM-dd},{report.UninstallSucceeded},{report.ExitCode},{report.RemainingTraces.Count},{report.RemovedTraces.Count}");
        }

        await File.WriteAllTextAsync(path, sb.ToString());
        _logger.LogInformation("Exported CSV report to: {Path}", path);
    }

    public async Task ExportToJsonAsync(List<UninstallReport> reports, string path)
    {
        var json = JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
        _logger.LogInformation("Exported JSON report to: {Path}", path);
    }

    public async Task ExportToHtmlAsync(List<UninstallReport> reports, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>BR Unistaller Report</title>");
        sb.AppendLine("<style>body{font-family:system-ui;background:#1a1a2e;color:#e0e0e0;margin:40px}h1{color:#00d4aa}table{border-collapse:collapse;width:100%}th,td{padding:8px 12px;text-align:left;border-bottom:1px solid #333}th{background:#16213e;color:#00d4aa}.ok{color:#4caf50}.fail{color:#f44336}</style></head><body>");
        sb.AppendLine("<h1>BR Unistaller Report</h1>");
        sb.AppendLine($"<p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine("<table><tr><th>App</th><th>Version</th><th>Date</th><th>Status</th><th>Traces</th></tr>");

        foreach (var report in reports)
        {
            var status = report.UninstallSucceeded ? "<span class='ok'>OK</span>" : "<span class='fail'>FAILED</span>";
            sb.AppendLine($"<tr><td>{report.AppName}</td><td>{report.AppVersion}</td><td>{report.UninstallDate:yyyy-MM-dd}</td><td>{status}</td><td>{report.RemovedTraces.Count} removed / {report.RemainingTraces.Count} remaining</td></tr>");
        }

        sb.AppendLine("</table></body></html>");
        await File.WriteAllTextAsync(path, sb.ToString());
        _logger.LogInformation("Exported HTML report to: {Path}", path);
    }

    public async Task ExportLogAsync(UninstallReport report, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== BR Unistaller Log ===");
        sb.AppendLine($"App: {report.AppName} v{report.AppVersion}");
        sb.AppendLine($"Publisher: {report.Publisher}");
        sb.AppendLine($"Date: {report.UninstallDate:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Status: {(report.UninstallSucceeded ? "Success" : "Failed")} (Exit code: {report.ExitCode})");
        sb.AppendLine($"Backup: {(report.BackupCreated ? $"Created at {report.BackupPath}" : "None")}");
        sb.AppendLine();
        sb.AppendLine("--- Removed Traces ---");
        foreach (var trace in report.RemovedTraces)
            sb.AppendLine($"[{trace.Type}] {trace.Path}");
        sb.AppendLine();
        sb.AppendLine("--- Remaining Traces ---");
        foreach (var trace in report.RemainingTraces)
            sb.AppendLine($"[{trace.Type}] {trace.Path}");

        await File.WriteAllTextAsync(path, sb.ToString());
        _logger.LogInformation("Exported log to: {Path}", path);
    }
}
