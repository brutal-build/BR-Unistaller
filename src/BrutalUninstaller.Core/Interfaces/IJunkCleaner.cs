using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Interfaces;

public interface IJunkCleaner
{
    Task<List<JunkItem>> ScanJunkLocationsAsync(CancellationToken ct = default);
    Task<bool> CleanSelectedAsync(List<JunkItem> items);
    long CalculateSavings(List<JunkItem> items);
}
