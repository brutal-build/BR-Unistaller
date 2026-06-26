using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Interfaces;

public interface ISchedulerService
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
    TimeSpan Interval { get; set; }
    event EventHandler<List<ScanResult>>? OrphanedTracesFound;
}
