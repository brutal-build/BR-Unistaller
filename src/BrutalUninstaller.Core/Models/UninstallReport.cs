using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Models;

public class UninstallReport
{
    public string AppName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public DateTime UninstallDate { get; set; } = DateTime.Now;
    public bool UninstallSucceeded { get; set; }
    public int ExitCode { get; set; }
    public List<ScanResult> RemainingTraces { get; set; } = new();
    public List<ScanResult> RemovedTraces { get; set; } = new();
    public bool BackupCreated { get; set; }
    public string BackupPath { get; set; } = string.Empty;
}
