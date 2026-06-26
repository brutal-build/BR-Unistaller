using Microsoft.Extensions.Logging;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Services;

public class StartupManager : IStartupManager
{
    private readonly ILogger<StartupManager> _logger;
    private readonly IRegistryHelper _registry;

    public StartupManager(ILogger<StartupManager> logger, IRegistryHelper registry)
    {
        _logger = logger;
        _registry = registry;
    }

    public Task<List<StartupEntry>> GetStartupEntriesAsync()
    {
        var entries = new List<StartupEntry>();
        var id = 0;

        // Registry Run keys
        ScanRegistryRunKeys(ref id, entries);

        // Startup folders
        ScanStartupFolders(ref id, entries);

        return Task.FromResult(entries);
    }

    private void ScanRegistryRunKeys(ref int id, List<StartupEntry> entries)
    {
        var runPaths = new[]
        {
            (Microsoft.Win32.RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", StartupEntryType.RegistryHKLM),
            (Microsoft.Win32.RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", StartupEntryType.RegistryHKCU),
        };

        foreach (var (hive, path, type) in runPaths)
        {
            var names = _registry.GetSubKeyNames(hive, path);
            if (names is null) continue;

            foreach (var name in names)
            {
                var value = _registry.GetValue(hive, path, name);
                entries.Add(new StartupEntry
                {
                    Id = $"reg-{id++}",
                    Name = name,
                    Command = value ?? string.Empty,
                    Location = $"{hive}\\{path}",
                    Enabled = true,
                    EntryType = type
                });
            }
        }
    }

    private static void ScanStartupFolders(ref int id, List<StartupEntry> entries)
    {
        var folderPaths = new[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupEntryType.StartupFolder),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupEntryType.StartupFolderAll),
        };

        foreach (var (path, type) in folderPaths)
        {
            if (!Directory.Exists(path)) continue;
            foreach (var file in Directory.GetFiles(path, "*.lnk"))
            {
                entries.Add(new StartupEntry
                {
                    Id = $"folder-{id++}",
                    Name = Path.GetFileNameWithoutExtension(file),
                    Command = file,
                    Location = path,
                    Enabled = true,
                    EntryType = type
                });
            }
        }
    }

    public Task<bool> ToggleEntryAsync(string id, bool enabled) => Task.FromResult(true);
    public Task<bool> RemoveEntryAsync(string id) => Task.FromResult(true);
}
