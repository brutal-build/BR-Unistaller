using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Interfaces;

public interface IAppDiscoveryService
{
    Task<List<InstalledApp>> DiscoverAllAppsAsync();
    Task<InstalledApp?> GetAppDetailsAsync(string appId);
    event EventHandler? AppListChanged;
    Task RefreshAsync();
}
