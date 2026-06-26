using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Interfaces;

public interface IExportService
{
    Task ExportToCsvAsync(List<UninstallReport> reports, string path);
    Task ExportToJsonAsync(List<UninstallReport> reports, string path);
    Task ExportToHtmlAsync(List<UninstallReport> reports, string path);
    Task ExportLogAsync(UninstallReport report, string path);
}
