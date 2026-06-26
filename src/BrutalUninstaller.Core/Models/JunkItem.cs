namespace BrutalUninstaller.Core.Models;

public class JunkItem
{
    public string Path { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public long EstimatedSize { get; set; }
    public bool Selected { get; set; } = true;
}
