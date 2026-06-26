using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Interfaces;

public interface IScanEngine
{
    Task<List<ScanResult>> ScanForTracesAsync(InstalledApp app, CancellationToken ct = default);
    Task<bool> DeleteSelectedTracesAsync(List<ScanResult> traces);
}
