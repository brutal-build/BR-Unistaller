namespace BrutalUninstaller.Core.Models;

public class StartupEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public StartupEntryType EntryType { get; set; }
}

public enum StartupEntryType
{
    RegistryHKLM,
    RegistryHKCU,
    RegistryHKLMX86,
    RegistryHKCUX86,
    StartupFolder,
    StartupFolderAll,
    ScheduledTask,
    Service
}
