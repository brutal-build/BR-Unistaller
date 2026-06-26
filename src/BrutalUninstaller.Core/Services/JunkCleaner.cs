using Microsoft.Extensions.Logging;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Services;

public class JunkCleaner : IJunkCleaner
{
    private readonly ILogger<JunkCleaner> _logger;
    private readonly IFileSystemHelper _fileSystem;

    public JunkCleaner(ILogger<JunkCleaner> logger, IFileSystemHelper fileSystem)
    {
        _logger = logger;
        _fileSystem = fileSystem;
    }

    public Task<List<JunkItem>> ScanJunkLocationsAsync(CancellationToken ct = default)
    {
        var items = new List<JunkItem>();

        // Temp folders
        ScanDirectory(Path.GetTempPath(), "Temp Files", items, ct);
        ScanDirectory(@"C:\Windows\Temp", "Windows Temp", items, ct);
        ScanDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"), "AppData Temp", items, ct);

        // Prefetch
        ScanDirectory(@"C:\Windows\Prefetch", "Prefetch", items, ct);

        // Recycle Bin (just marker)
        items.Add(new JunkItem
        {
            Path = "shell:RecycleBinFolder",
            Category = "Recycle Bin",
            EstimatedSize = 0,
            Selected = true
        });

        // Log files
        ScanDirectory(@"C:\Windows\Logs", "Windows Logs", items, ct, "*.log");

        // Memory dumps
        ScanDirectory(@"C:\Windows", "Memory Dumps", items, ct, "*.dmp");
        ScanDirectory(@"C:\Windows", "Memory Dumps", items, ct, "*.mdmp");

        return Task.FromResult(items);
    }

    private void ScanDirectory(string path, string category, List<JunkItem> items, CancellationToken ct, string pattern = "*")
    {
        if (ct.IsCancellationRequested) return;
        try
        {
            if (!Directory.Exists(path)) return;

            long totalSize = 0;
            int fileCount = 0;

            foreach (var file in Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories))
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    var info = new FileInfo(file);
                    totalSize += info.Length;
                    fileCount++;
                }
                catch { }
            }

            items.Add(new JunkItem
            {
                Path = path,
                Category = category,
                EstimatedSize = totalSize,
                Selected = true
            });
        }
        catch (UnauthorizedAccessException) { }
    }

    public Task<bool> CleanSelectedAsync(List<JunkItem> items)
    {
        foreach (var item in items.Where(i => i.Selected))
        {
            try
            {
                if (Directory.Exists(item.Path))
                    Directory.Delete(item.Path, true);
                else if (File.Exists(item.Path))
                    File.Delete(item.Path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean: {Path}", item.Path);
            }
        }
        return Task.FromResult(true);
    }

    public long CalculateSavings(List<JunkItem> items) =>
        items.Where(i => i.Selected).Sum(i => i.EstimatedSize);
}
