using System.Diagnostics;
using System.Runtime.InteropServices;
using BrutalUninstaller.Core.Enums;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace BrutalUninstaller.Core.Services;

public class ScanEngine : IScanEngine
{
    private readonly IRegistryHelper _registry;
    private readonly IFileSystemHelper _fileSystem;
    private readonly ILogger<ScanEngine> _logger;

    public ScanEngine(IRegistryHelper registry, IFileSystemHelper fileSystem, ILogger<ScanEngine> logger)
    {
        _registry = registry;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<List<ScanResult>> ScanForTracesAsync(InstalledApp app, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting trace scan for {AppName} ({Publisher})", app.DisplayName, app.Publisher);
        var results = new List<ScanResult>();

        // Run all scans
        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            RegistryScan(app, results);
            ct.ThrowIfCancellationRequested();

            FileSystemScan(app, results);
            ct.ThrowIfCancellationRequested();

            ServiceScan(app, results);
            ct.ThrowIfCancellationRequested();

            TaskScan(app, results);
        }, ct);

        _logger.LogInformation("Scan complete for {AppName}: found {Count} traces", app.DisplayName, results.Count);
        return results;
    }

    public Task<bool> DeleteSelectedTracesAsync(List<ScanResult> traces)
    {
        _logger.LogInformation("Deleting {Count} selected traces", traces.Count);
        var selected = traces.Where(t => t.Selected).ToList();
        var allSucceeded = true;

        foreach (var trace in selected)
        {
            try
            {
                switch (trace.Type)
                {
                    case ScanResultType.RegistryKey:
                        allSucceeded &= DeleteRegistryKey(trace);
                        break;

                    case ScanResultType.RegistryValue:
                        allSucceeded &= DeleteRegistryValue(trace);
                        break;

                    case ScanResultType.File:
                        allSucceeded &= DeleteFile(trace);
                        break;

                    case ScanResultType.Folder:
                    case ScanResultType.EmptyFolder:
                        allSucceeded &= DeleteFolder(trace);
                        break;

                    case ScanResultType.Shortcut:
                        allSucceeded &= DeleteFile(trace);
                        break;

                    case ScanResultType.Service:
                        allSucceeded &= DeleteService(trace);
                        break;

                    case ScanResultType.Driver:
                        allSucceeded &= DeleteService(trace);
                        break;

                    case ScanResultType.ScheduledTask:
                        allSucceeded &= DeleteScheduledTask(trace);
                        break;

                    case ScanResultType.FirewallRule:
                    case ScanResultType.ContextMenuEntry:
                        // These types require specialized handling — log and skip by marking as failed
                        _logger.LogWarning("Delete not implemented for trace type {Type}: {Path}", trace.Type, trace.Path);
                        allSucceeded = false;
                        break;

                    default:
                        _logger.LogWarning("Unknown trace type {Type}: {Path}", trace.Type, trace.Path);
                        allSucceeded = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete trace {Type} at {Path}", trace.Type, trace.Path);
                allSucceeded = false;
            }
        }

        return Task.FromResult(allSucceeded);
    }

    #region Registry Scan

    private void RegistryScan(InstalledApp app, List<ScanResult> results)
    {
        var publisher = Sanitize(app.Publisher);
        var displayName = Sanitize(app.DisplayName);

        if (string.IsNullOrWhiteSpace(publisher) && string.IsNullOrWhiteSpace(displayName))
        {
            _logger.LogWarning("App has neither Publisher nor DisplayName — skipping registry scan");
            return;
        }

        // 1. SOFTWARE\{Publisher}\{DisplayName} in HKCU and HKLM
        ScanRegistryPath(RegistryHive.CurrentUser, $@"SOFTWARE\{publisher}\{displayName}", results);
        ScanRegistryPath(RegistryHive.LocalMachine, $@"SOFTWARE\{publisher}\{displayName}", results);

        // Also check just SOFTWARE\{Publisher}
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            ScanRegistryPath(RegistryHive.CurrentUser, $@"SOFTWARE\{publisher}", results);
            ScanRegistryPath(RegistryHive.LocalMachine, $@"SOFTWARE\{publisher}", results);
        }

        // 2. CLSID search — scan HKCR equivalent (HKLM\SOFTWARE\Classes and HKCU\SOFTWARE\Classes)
        ScanClsidForMatch(RegistryHive.LocalMachine, @"SOFTWARE\Classes\CLSID", app, results);
        ScanClsidForMatch(RegistryHive.CurrentUser, @"SOFTWARE\Classes\CLSID", app, results);

        // 3. Uninstall key matching app.Id
        if (!string.IsNullOrWhiteSpace(app.Id))
        {
            ScanRegistryPath(RegistryHive.CurrentUser, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{app.Id}", results);
            ScanRegistryPath(RegistryHive.LocalMachine, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{app.Id}", results);
            ScanRegistryPath(RegistryHive.LocalMachine, $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{app.Id}", results);
        }

        // 4. App Paths
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            ScanRegistryPath(RegistryHive.CurrentUser, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{displayName}.exe", results);
            ScanRegistryPath(RegistryHive.LocalMachine, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{displayName}.exe", results);
        }

        // 5. HKU — enumerate all user SIDs
        ScanHkuForApp(app, results);

        // 6. Also check CLSID sub-keys by name in TypeLib, Interface, AppID
        ScanClsidSubTree(RegistryHive.LocalMachine, @"SOFTWARE\Classes\TypeLib", app, results);
        ScanClsidSubTree(RegistryHive.LocalMachine, @"SOFTWARE\Classes\Interface", app, results);
        ScanClsidSubTree(RegistryHive.LocalMachine, @"SOFTWARE\Classes\AppID", app, results);
    }

    private void ScanRegistryPath(RegistryHive hive, string subKey, List<ScanResult> results)
    {
        using var key = _registry.OpenRegistryKey(hive, subKey);
        if (key != null)
        {
            long estimatedSize = EstimateRegistryKeySize(key);
            results.Add(new ScanResult
            {
                Type = ScanResultType.RegistryKey,
                Path = $"{hive}\\{subKey}",
                Description = $"Registry key: {subKey}",
                Selected = true,
                EstimatedSize = estimatedSize
            });
            _logger.LogDebug("Found registry key: {Hive}\\{SubKey}", hive, subKey);
        }
    }

    private void ScanClsidForMatch(RegistryHive hive, string clsidPath, InstalledApp app, List<ScanResult> results)
    {
        var subKeys = _registry.GetSubKeyNames(hive, clsidPath);
        if (subKeys == null) return;

        foreach (var guid in subKeys)
        {
            // Check both (default) value and common value names
            var defaultVal = _registry.GetValue(hive, $@"{clsidPath}\{guid}", "");
            var appIdVal = _registry.GetValue(hive, $@"{clsidPath}\{guid}", "AppID");
            var progIdVal = _registry.GetValue(hive, $@"{clsidPath}\{guid}", "ProgID");

            var allValues = new[] { defaultVal, appIdVal, progIdVal };
            if (allValues.Any(v => MatchesApp(v, app)))
            {
                using var key = _registry.OpenRegistryKey(hive, $@"{clsidPath}\{guid}");
                long estimatedSize = key != null ? EstimateRegistryKeySize(key) : 0;

                results.Add(new ScanResult
                {
                    Type = ScanResultType.RegistryKey,
                    Path = $"{hive}\\{clsidPath}\\{guid}",
                    Description = $"CLSID entry matching {app.DisplayName}",
                    Selected = true,
                    EstimatedSize = estimatedSize
                });
                _logger.LogDebug("Found matching CLSID: {Guid} in {Hive}", guid, hive);
            }
        }
    }

    private void ScanClsidSubTree(RegistryHive hive, string basePath, InstalledApp app, List<ScanResult> results)
    {
        var subKeys = _registry.GetSubKeyNames(hive, basePath);
        if (subKeys == null) return;

        foreach (var subKey in subKeys)
        {
            var defaultVal = _registry.GetValue(hive, $@"{basePath}\{subKey}", "");
            if (MatchesApp(defaultVal, app))
            {
                using var key = _registry.OpenRegistryKey(hive, $@"{basePath}\{subKey}");
                long estimatedSize = key != null ? EstimateRegistryKeySize(key) : 0;

                results.Add(new ScanResult
                {
                    Type = ScanResultType.RegistryKey,
                    Path = $"{hive}\\{basePath}\\{subKey}",
                    Description = $"COM/{basePath.Split('\\').Last()} entry matching {app.DisplayName}",
                    Selected = true,
                    EstimatedSize = estimatedSize
                });
            }
        }
    }

    private void ScanHkuForApp(InstalledApp app, List<ScanResult> results)
    {
        var userSids = _registry.GetSubKeyNames(RegistryHive.Users, "");
        if (userSids == null) return;

        var publisher = Sanitize(app.Publisher);
        var displayName = Sanitize(app.DisplayName);

        foreach (var sid in userSids)
        {
            if (sid is "_BITS" or ".DEFAULT" or "S-1-5-18" or "S-1-5-19" or "S-1-5-20") continue;

            if (!string.IsNullOrWhiteSpace(publisher) && !string.IsNullOrWhiteSpace(displayName))
                ScanRegistryPath(RegistryHive.Users, $@"{sid}\SOFTWARE\{publisher}\{displayName}", results);

            if (!string.IsNullOrWhiteSpace(app.Id))
                ScanRegistryPath(RegistryHive.Users, $@"{sid}\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{app.Id}", results);
        }
    }

    private static long EstimateRegistryKeySize(RegistryKey key)
    {
        long size = 0;
        try
        {
            foreach (var valueName in key.GetValueNames())
            {
                var value = key.GetValue(valueName);
                if (value is string str)
                    size += str.Length * 2 + 8;
                else if (value is byte[] bytes)
                    size += bytes.Length + 8;
                else if (value is int || value is long)
                    size += 16;
                else if (value is string[] strs)
                    size += strs.Sum(s => s.Length * 2 + 4);
            }

            size += key.SubKeyCount * 128;
        }
        catch
        {
            size = 4096;
        }

        return Math.Max(size, 256);
    }

    private static bool MatchesApp(string? value, InstalledApp app)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var val = value.AsSpan().Trim();

        if (!string.IsNullOrWhiteSpace(app.DisplayName) &&
            val.Contains(app.DisplayName.AsSpan(), StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(app.Publisher) &&
            val.Contains(app.Publisher.AsSpan(), StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    #endregion

    #region File System Scan

    private void FileSystemScan(InstalledApp app, List<ScanResult> results)
    {
        var publisher = Sanitize(app.Publisher);
        var displayName = Sanitize(app.DisplayName);

        if (string.IsNullOrWhiteSpace(publisher) && string.IsNullOrWhiteSpace(displayName))
            return;

        // Resolve environment-based paths
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var commonProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        var commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);

        // Low AppData — constructed path under Local
        var appDataLocalLow = Path.Combine(Path.GetDirectoryName(appDataLocal) ?? appDataLocal, "LocalLow");

        var pathsToCheck = new List<(string Path, string Description, bool IsFolder)>();

        // %ProgramData%\{Publisher}\{DisplayName}
        if (!string.IsNullOrWhiteSpace(publisher) && !string.IsNullOrWhiteSpace(displayName))
        {
            pathsToCheck.Add((Path.Combine(programData, publisher, displayName),
                $"ProgramData: {publisher}\\{displayName}", true));
        }

        // %AppData%\Roaming\{Publisher}
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            pathsToCheck.Add((Path.Combine(appDataRoaming, publisher),
                $"AppData\\Roaming: {publisher}", true));
        }

        // %AppData%\Local\{Publisher}
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            pathsToCheck.Add((Path.Combine(appDataLocal, publisher),
                $"AppData\\Local: {publisher}", true));
        }

        // %AppData%\LocalLow\{Publisher}
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            pathsToCheck.Add((Path.Combine(appDataLocalLow, publisher),
                $"AppData\\LocalLow: {publisher}", true));
        }

        // %ProgramFiles%\{Publisher}\{DisplayName}
        if (!string.IsNullOrWhiteSpace(publisher) && !string.IsNullOrWhiteSpace(displayName))
        {
            pathsToCheck.Add((Path.Combine(programFiles, publisher, displayName),
                $"ProgramFiles: {publisher}\\{displayName}", true));
        }

        // %ProgramFiles(x86)%\{Publisher}\{DisplayName}
        if (!string.IsNullOrWhiteSpace(publisher) && !string.IsNullOrWhiteSpace(displayName) &&
            programFilesX86 != null && programFilesX86 != programFiles)
        {
            pathsToCheck.Add((Path.Combine(programFilesX86, publisher, displayName),
                $"ProgramFiles(x86): {publisher}\\{displayName}", true));
        }

        // %CommonProgramFiles%\{Publisher}
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            pathsToCheck.Add((Path.Combine(commonProgramFiles, publisher),
                $"CommonProgramFiles: {publisher}", true));
        }

        // %UserProfile%\Documents\{DisplayName}
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            pathsToCheck.Add((Path.Combine(userProfile, "Documents", displayName),
                $"Documents: {displayName}", true));
            pathsToCheck.Add((Path.Combine(documents, displayName),
                $"Documents (alternate): {displayName}", true));
        }

        // Scan each path
        foreach (var (path, description, isFolder) in pathsToCheck)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (isFolder && _fileSystem.DirectoryExists(path))
            {
                long size = _fileSystem.GetDirectorySize(path);
                results.Add(new ScanResult
                {
                    Type = ScanResultType.Folder,
                    Path = path,
                    Description = description,
                    Selected = true,
                    EstimatedSize = size
                });
                _logger.LogDebug("Found folder: {Path}", path);
            }
            else if (!isFolder && _fileSystem.FileExists(path))
            {
                long size = new FileInfo(path).Length;
                results.Add(new ScanResult
                {
                    Type = ScanResultType.File,
                    Path = path,
                    Description = description,
                    Selected = true,
                    EstimatedSize = size
                });
                _logger.LogDebug("Found file: {Path}", path);
            }
        }

        // Check also the InstallLocation directly
        if (!string.IsNullOrWhiteSpace(app.InstallLocation) &&
            _fileSystem.DirectoryExists(app.InstallLocation))
        {
            // Only add if not already found via publisher\displayname path
            var alreadyFound = results.Any(r =>
                r.Path.Equals(app.InstallLocation, StringComparison.OrdinalIgnoreCase));
            if (!alreadyFound)
            {
                long size = _fileSystem.GetDirectorySize(app.InstallLocation);
                results.Add(new ScanResult
                {
                    Type = ScanResultType.Folder,
                    Path = app.InstallLocation,
                    Description = $"Install location: {app.InstallLocation}",
                    Selected = true,
                    EstimatedSize = size
                });
            }
        }

        // Start Menu shortcuts
        ScanStartMenuShortcuts(app, startMenu, results);
        ScanStartMenuShortcuts(app, commonStartMenu, results);
    }

    private void ScanStartMenuShortcuts(InstalledApp app, string startMenuPath, List<ScanResult> results)
    {
        if (!_fileSystem.DirectoryExists(startMenuPath))
            return;

        var displayName = Sanitize(app.DisplayName);
        if (string.IsNullOrWhiteSpace(displayName))
            return;

        // Search for .lnk files with the app name
        var shortcutFiles = _fileSystem.GetFiles(startMenuPath, "*.lnk");
        foreach (var shortcut in shortcutFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(shortcut);
            if (fileName.Contains(displayName, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(app.Publisher) &&
                 fileName.Contains(app.Publisher, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(new ScanResult
                {
                    Type = ScanResultType.Shortcut,
                    Path = shortcut,
                    Description = $"Start Menu shortcut: {shortcut}",
                    Selected = true,
                    EstimatedSize = new FileInfo(shortcut).Length
                });
            }
        }

        // Recurse into subdirectories
        var subDirs = _fileSystem.GetDirectories(startMenuPath);
        foreach (var subDir in subDirs)
        {
            ScanStartMenuShortcuts(app, subDir, results);
        }
    }

    #endregion

    #region Service Scan

    private void ServiceScan(InstalledApp app, List<ScanResult> results)
    {
        const string servicesPath = @"SYSTEM\CurrentControlSet\Services";
        var serviceNames = _registry.GetSubKeyNames(RegistryHive.LocalMachine, servicesPath);
        if (serviceNames == null) return;

        var publisher = Sanitize(app.Publisher);
        var displayName = Sanitize(app.DisplayName);

        foreach (var serviceName in serviceNames)
        {
            // Check if service name or its DisplayName value matches
            var displayNameVal = _registry.GetValue(RegistryHive.LocalMachine,
                $@"{servicesPath}\{serviceName}", "DisplayName");

            bool matches = false;

            if (!string.IsNullOrWhiteSpace(displayName) &&
                serviceName.Contains(displayName, StringComparison.OrdinalIgnoreCase))
                matches = true;

            if (!matches && !string.IsNullOrWhiteSpace(publisher) &&
                serviceName.Contains(publisher, StringComparison.OrdinalIgnoreCase))
                matches = true;

            if (!matches && displayNameVal != null &&
                MatchesApp(displayNameVal, app))
                matches = true;

            if (matches)
            {
                // Check if it's a driver or service
                var startVal = _registry.GetValue(RegistryHive.LocalMachine,
                    $@"{servicesPath}\{serviceName}", "Start");
                var typeVal = _registry.GetValue(RegistryHive.LocalMachine,
                    $@"{servicesPath}\{serviceName}", "Type");

                var traceType = ScanResultType.Service;
                if (typeVal is not null && int.TryParse(typeVal, out var typeInt) && (typeInt & 0x0B) is 1 or 0x0B)
                    traceType = ScanResultType.Driver;

                results.Add(new ScanResult
                {
                    Type = traceType,
                    Path = $@"HKLM\{servicesPath}\{serviceName}",
                    Description = $"{(traceType == ScanResultType.Driver ? "Driver" : "Service")}: {serviceName}",
                    Selected = true,
                    EstimatedSize = 4096
                });
                _logger.LogDebug("Found {Type}: {ServiceName}", traceType, serviceName);
            }
        }
    }

    #endregion

    #region Task Scan

    private void TaskScan(InstalledApp app, List<ScanResult> results)
    {
        var tasksPath = @"C:\Windows\System32\Tasks";
        if (!_fileSystem.DirectoryExists(tasksPath))
            return;

        var displayName = Sanitize(app.DisplayName);
        var publisher = Sanitize(app.Publisher);

        // Scan recursively through task folders
        ScanTasksRecursive(tasksPath, app, displayName, publisher, results);
    }

    private void ScanTasksRecursive(string directory, InstalledApp app, string displayName, string publisher, List<ScanResult> results)
    {
        try
        {
            var files = _fileSystem.GetFiles(directory);
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);

                bool matches = false;
                if (!string.IsNullOrWhiteSpace(displayName) &&
                    fileName.Contains(displayName, StringComparison.OrdinalIgnoreCase))
                    matches = true;

                if (!matches && !string.IsNullOrWhiteSpace(publisher) &&
                    fileName.Contains(publisher, StringComparison.OrdinalIgnoreCase))
                    matches = true;

                if (matches)
                {
                    results.Add(new ScanResult
                    {
                        Type = ScanResultType.ScheduledTask,
                        Path = file,
                        Description = $"Scheduled task: {fileName}",
                        Selected = true,
                        EstimatedSize = new FileInfo(file).Length
                    });
                    _logger.LogDebug("Found scheduled task: {FileName}", fileName);
                }
            }

            var subDirs = _fileSystem.GetDirectories(directory);
            foreach (var subDir in subDirs)
            {
                ScanTasksRecursive(subDir, app, displayName, publisher, results);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to tasks directory: {Directory}", directory);
        }
    }

    #endregion

    #region Delete Operations

    private bool DeleteRegistryKey(ScanResult trace)
    {
        if (!TryParseRegistryPath(trace.Path, out var hive, out var subKey, out var view))
            return false;

        return _registry.DeleteKey(hive, subKey, view);
    }

    private bool DeleteRegistryValue(ScanResult trace)
    {
        // Path format: hive\key\subkey:ValueName
        var colonIdx = trace.Path.LastIndexOf(':');
        if (colonIdx < 0)
            return false;

        var keyPath = trace.Path[..colonIdx];
        var valueName = trace.Path[(colonIdx + 1)..];

        if (!TryParseRegistryPath(keyPath, out var hive, out var subKey, out var view))
            return false;

        return _registry.DeleteValue(hive, subKey, valueName, view);
    }

    private bool DeleteFile(ScanResult trace)
    {
        if (_fileSystem.FileExists(trace.Path))
            return _fileSystem.DeleteFile(trace.Path);

        _logger.LogWarning("File not found: {Path}", trace.Path);
        return false;
    }

    private bool DeleteFolder(ScanResult trace)
    {
        if (_fileSystem.DirectoryExists(trace.Path))
            return _fileSystem.DeleteDirectory(trace.Path, true);

        _logger.LogWarning("Directory not found: {Path}", trace.Path);
        return false;
    }

    private bool DeleteService(ScanResult trace)
    {
        // Extract service name from path: HKLM\SYSTEM\CurrentControlSet\Services\{ServiceName}
        var serviceName = trace.Path.Split('\\').LastOrDefault();
        if (string.IsNullOrWhiteSpace(serviceName))
            return false;

        return RunProcess("sc", $"delete \"{serviceName}\"");
    }

    private bool DeleteScheduledTask(ScanResult trace)
    {
        var taskName = Path.GetFileNameWithoutExtension(trace.Path);
        if (string.IsNullOrWhiteSpace(taskName))
            return false;

        // Build full task path from the file path under C:\Windows\System32\Tasks
        var tasksRoot = @"C:\Windows\System32\Tasks";
        var relativePath = trace.Path;

        if (relativePath.StartsWith(tasksRoot, StringComparison.OrdinalIgnoreCase))
        {
            // Format: \Microsoft\Windows\AppName
            var taskPath = relativePath[tasksRoot.Length..]
                .Replace(Path.DirectorySeparatorChar, '\\')
                .TrimStart('\\');
            var taskFullName = Path.GetFileNameWithoutExtension(taskPath);

            // Use the relative path as the task name in schtasks format
            var folderPart = Path.GetDirectoryName(taskPath)?.Replace('\\', '/');

            if (!string.IsNullOrWhiteSpace(folderPart) && folderPart != ".")
                taskFullName = $"{folderPart}\\{taskFullName}";

            return RunProcess("schtasks", $"/delete /tn \"{taskFullName}\" /f");
        }

        return RunProcess("schtasks", $"/delete /tn \"{taskName}\" /f");
    }

    private static bool RunProcess(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(30000);
            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion

    #region Helpers

    private static string Sanitize(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static bool TryParseRegistryPath(string fullPath, out RegistryHive hive, out string subKey, out RegistryView view)
    {
        hive = RegistryHive.LocalMachine;
        subKey = string.Empty;
        view = RegistryView.Default;

        try
        {
            // Expected format: "Hive\Key\SubKey" where Hive is like HKEY_LOCAL_MACHINE, HKCU, HKLM, etc.
            var parts = fullPath.Split('\\', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;

            var hiveStr = parts[0].ToUpperInvariant();
            subKey = parts[1];

            // Normalize hive strings
            if (hiveStr is "HKEY_LOCAL_MACHINE" or "HKLM")
            {
                hive = RegistryHive.LocalMachine;
            }
            else if (hiveStr is "HKEY_CURRENT_USER" or "HKCU")
            {
                hive = RegistryHive.CurrentUser;
            }
            else if (hiveStr is "HKEY_USERS" or "HKU")
            {
                hive = RegistryHive.Users;
            }
            else if (hiveStr is "HKEY_CLASSES_ROOT" or "HKCR")
            {
                // HKCR is a composite view, map to HKLM\SOFTWARE\Classes
                hive = RegistryHive.LocalMachine;
                subKey = $@"SOFTWARE\Classes\{subKey}";
            }
            else if (hiveStr is "HKEY_CURRENT_CONFIG" or "HKCC")
            {
                hive = RegistryHive.CurrentConfig;
            }
            else
            {
                return false;
            }

            // Detect Wow6432Node in path
            if (subKey.Contains("WOW6432Node", StringComparison.OrdinalIgnoreCase))
                view = RegistryView.Registry32;

            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
