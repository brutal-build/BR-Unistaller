namespace BrutalUninstaller.Core.Models;

public class InstalledApp
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InstallDate { get; set; } = string.Empty;
    public ulong EstimatedSize { get; set; }
    public string UninstallString { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string QuietUninstallString { get; set; } = string.Empty;
    public Enums.AppType Type { get; set; } = Enums.AppType.Unknown;
    public string IconPath { get; set; } = string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(DisplayName);
}
