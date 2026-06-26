using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Interfaces;

public interface IUninstallEngine
{
    Task<UninstallReport> UninstallAsync(InstalledApp app, IProgress<int>? progress = null, CancellationToken ct = default);
    Task<bool> ForceKillProcessAsync(string processName);
}
