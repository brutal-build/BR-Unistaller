using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using BrutalUninstaller.Core.Interfaces;

namespace BrutalUninstaller.Infrastructure.Registry;

public sealed class RegistryHelper : IRegistryHelper
{
    private readonly ILogger<RegistryHelper> _logger;

    public RegistryHelper(ILogger<RegistryHelper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public RegistryKey? OpenRegistryKey(Microsoft.Win32.RegistryHive hive, string subKey, RegistryView view = RegistryView.Default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subKey);

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            var key = baseKey.OpenSubKey(subKey);
            if (key is null)
                _logger.LogDebug("Registry key not found: {Hive}\\{SubKey} (View: {View})", hive, subKey, view);
            else
                _logger.LogTrace("Opened registry key: {Hive}\\{SubKey} (View: {View})", hive, subKey, view);
            return key;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied opening registry key: {Hive}\\{SubKey} (View: {View})", hive, subKey, view);
            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error opening registry key: {Hive}\\{SubKey} (View: {View})", hive, subKey, view);
            return null;
        }
    }

    public string[]? GetSubKeyNames(Microsoft.Win32.RegistryHive hive, string subKey, RegistryView view = RegistryView.Default)
    {
        try
        {
            using var key = OpenRegistryKey(hive, subKey, view);
            return key?.GetSubKeyNames();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error reading subkeys: {Hive}\\{SubKey} (View: {View})", hive, subKey, view);
            return null;
        }
    }

    public string? GetValue(Microsoft.Win32.RegistryHive hive, string subKey, string valueName, RegistryView view = RegistryView.Default)
    {
        try
        {
            using var key = OpenRegistryKey(hive, subKey, view);
            return key?.GetValue(valueName)?.ToString();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error reading value '{ValueName}' from {Hive}\\{SubKey} (View: {View})", valueName, hive, subKey, view);
            return null;
        }
    }

    public bool DeleteKey(Microsoft.Win32.RegistryHive hive, string subKey, RegistryView view = RegistryView.Default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subKey);

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            baseKey.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
            _logger.LogInformation("Deleted registry key: {Hive}\\{SubKey} (View: {View})", hive, subKey, view);
            return true;
        }
        catch (ArgumentException)
        {
            _logger.LogDebug("Registry key not found for deletion: {Hive}\\{SubKey} (View: {View})", hive, subKey, view);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied deleting registry key: {Hive}\\{SubKey} (View: {View})", hive, subKey, view);
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error deleting registry key: {Hive}\\{SubKey} (View: {View})", hive, subKey, view);
            return false;
        }
    }

    public bool DeleteValue(Microsoft.Win32.RegistryHive hive, string subKey, string valueName, RegistryView view = RegistryView.Default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subKey);
        ArgumentException.ThrowIfNullOrEmpty(valueName);

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(subKey, writable: true);
            if (key is null) return false;

            if (key.GetValue(valueName) is null)
            {
                _logger.LogTrace("Value '{ValueName}' not found in {Hive}\\{SubKey} (View: {View})", valueName, hive, subKey, view);
                return false;
            }

            key.DeleteValue(valueName, throwOnMissingValue: false);
            _logger.LogInformation("Deleted registry value '{ValueName}' from {Hive}\\{SubKey} (View: {View})", valueName, hive, subKey, view);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied deleting value '{ValueName}' from {Hive}\\{SubKey} (View: {View})", valueName, hive, subKey, view);
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error deleting value '{ValueName}' from {Hive}\\{SubKey} (View: {View})", valueName, hive, subKey, view);
            return false;
        }
    }
}
