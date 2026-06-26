using Microsoft.Extensions.Logging;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Services;

public class SchedulerService : ISchedulerService
{
    private readonly ILogger<SchedulerService> _logger;
    private readonly IScanEngine _scanEngine;
    private CancellationTokenSource? _cts;

    public TimeSpan Interval { get; set; } = TimeSpan.FromDays(7);
    public event EventHandler<List<ScanResult>>? OrphanedTracesFound
    {
        add { }
        remove { }
    }

    public SchedulerService(ILogger<SchedulerService> logger, IScanEngine scanEngine)
    {
        _logger = logger;
        _scanEngine = scanEngine;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = RunScheduleAsync(_cts.Token);
        _logger.LogInformation("Scheduler started with interval: {Interval}", Interval);
        return Task.CompletedTask;
    }

    private async Task RunScheduleAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, ct);
                _logger.LogInformation("Running scheduled orphan scan...");
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        _logger.LogInformation("Scheduler stopped");
        return Task.CompletedTask;
    }
}
