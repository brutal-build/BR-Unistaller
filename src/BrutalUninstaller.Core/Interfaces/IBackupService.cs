namespace BrutalUninstaller.Core.Interfaces;

public interface IBackupService
{
    Task<bool> CreateRestorePointAsync(string description);
    Task<string?> ExportRegistryKeyAsync(string keyPath);
    Task<string> SnapshotCurrentStateAsync(string appId, string installLocation);
}
