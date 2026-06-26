using Microsoft.Win32;

namespace BrutalUninstaller.Core.Interfaces;

public interface IRegistryHelper
{
    RegistryKey? OpenRegistryKey(RegistryHive hive, string subKey, RegistryView view = RegistryView.Default);
    string[]? GetSubKeyNames(RegistryHive hive, string subKey, RegistryView view = RegistryView.Default);
    string? GetValue(RegistryHive hive, string subKey, string valueName, RegistryView view = RegistryView.Default);
    bool DeleteKey(RegistryHive hive, string subKey, RegistryView view = RegistryView.Default);
    bool DeleteValue(RegistryHive hive, string subKey, string valueName, RegistryView view = RegistryView.Default);
}
