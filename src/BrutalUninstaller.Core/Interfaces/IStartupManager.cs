using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Interfaces;

public interface IStartupManager
{
    Task<List<StartupEntry>> GetStartupEntriesAsync();
    Task<bool> ToggleEntryAsync(string id, bool enabled);
    Task<bool> RemoveEntryAsync(string id);
}
