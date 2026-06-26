namespace BrutalUninstaller.Core.Interfaces;

public interface IUwpApi
{
    Task<List<(string packageFullName, string displayName, string publisher)>> GetPackagesAsync();
    Task<bool> RemovePackageAsync(string packageFullName);
}
