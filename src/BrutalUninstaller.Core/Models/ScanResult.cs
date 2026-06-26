using BrutalUninstaller.Core.Enums;

namespace BrutalUninstaller.Core.Models;

public class ScanResult
{
    public ScanResultType Type { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Selected { get; set; } = true;
    public long EstimatedSize { get; set; }
}
