using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BrutalUninstaller.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace BrutalUninstaller.Core.Services;

public class BackupService : IBackupService
{
    private readonly IRegistryHelper _registry;
    private readonly ILogger<BackupService> _logger;

    // P/Invoke for System Restore
    private const string SrClientDll = "srclient.dll";

    private const int BEGIN_SYSTEM_CHANGE = 100;
    private const int END_SYSTEM_CHANGE = 101;

    [DllImport(SrClientDll, CharSet = CharSet.Unicode)]
    private static extern int SRSetRestorePointW(
        ref RESTOREPOINTINFO pRestorePtSpec,
        out STATEMGRSTATUS pSmgStatus);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RESTOREPOINTINFO
    {
        public int dwEventType;
        public int dwRestorePtType;
        public long llSequenceNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STATEMGRSTATUS
    {
        public int nStatus;
        public long llSequenceNumber;
    }

    // Restore point types
    private const int RESTORE_POINT_TYPE_APPLICATION_INSTALL = 0;
    private const int RESTORE_POINT_TYPE_MODIFY_SETTINGS = 1;
    private const int RESTORE_POINT_TYPE_CANCELLED_OPERATION = 13;

    public BackupService(IRegistryHelper registry, ILogger<BackupService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public Task<bool> CreateRestorePointAsync(string description)
    {
        _logger.LogInformation("Creating system restore point: {Description}", description);

        try
        {
            // First call: BEGIN_SYSTEM_CHANGE
            var beginInfo = new RESTOREPOINTINFO
            {
                dwEventType = BEGIN_SYSTEM_CHANGE,
                dwRestorePtType = RESTORE_POINT_TYPE_APPLICATION_INSTALL,
                llSequenceNumber = 0,
                szDescription = description
            };

            int result = SRSetRestorePointW(ref beginInfo, out var status);
            if (result != 0)
            {
                long seqNum = status.llSequenceNumber;

                // Second call: END_SYSTEM_CHANGE with the sequence number from the first call
                var endInfo = new RESTOREPOINTINFO
                {
                    dwEventType = END_SYSTEM_CHANGE,
                    dwRestorePtType = RESTORE_POINT_TYPE_APPLICATION_INSTALL,
                    llSequenceNumber = seqNum,
                    szDescription = description
                };

                result = SRSetRestorePointW(ref endInfo, out status);
                bool success = result != 0;

                if (success)
                {
                    _logger.LogInformation("Restore point created successfully: {Description} (seq: {Seq})",
                        description, seqNum);
                }
                else
                {
                    _logger.LogWarning("Failed to finalize restore point. Status: {Status}", status.nStatus);
                }

                return Task.FromResult(success);
            }

            _logger.LogWarning("Failed to begin restore point. Status: {Status}", status.nStatus);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create system restore point: {Description}", description);
            return Task.FromResult(false);
        }
    }

    public Task<string?> ExportRegistryKeyAsync(string keyPath)
    {
        _logger.LogInformation("Exporting registry key: {KeyPath}", keyPath);

        try
        {
            var tempFileName = Path.Combine(
                Path.GetTempPath(),
                $"BRUTAL_Backup_{Guid.NewGuid():N}.reg");

            var startInfo = new ProcessStartInfo
            {
                FileName = "regedit",
                Arguments = $"/e \"{tempFileName}\" \"{keyPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start regedit process for exporting registry key");
                return Task.FromResult<string?>(null);
            }

            process.WaitForExit(30000);

            if (process.ExitCode == 0 && File.Exists(tempFileName))
            {
                var fileInfo = new FileInfo(tempFileName);
                if (fileInfo.Length > 0)
                {
                    _logger.LogInformation("Registry key exported to: {TempFile} ({Size} bytes)",
                        tempFileName, fileInfo.Length);
                    return Task.FromResult<string?>(tempFileName);
                }

                // Empty reg file means the key doesn't exist
                _logger.LogWarning("Registry key not found or empty: {KeyPath}", keyPath);
                TryDeleteFile(tempFileName);
                return Task.FromResult<string?>(null);
            }

            _logger.LogWarning("regedit export failed for {KeyPath} with exit code {ExitCode}",
                keyPath, process.ExitCode);
            TryDeleteFile(tempFileName);
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export registry key: {KeyPath}", keyPath);
            return Task.FromResult<string?>(null);
        }
    }

    public Task<string> SnapshotCurrentStateAsync(string appId, string installLocation)
    {
        _logger.LogInformation("Creating snapshot for app {AppId}", appId);

        try
        {
            var snapshotDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BRUTAL_Uninstaller",
                "Snapshots");

            Directory.CreateDirectory(snapshotDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var snapshotFile = Path.Combine(
                snapshotDir,
                $"Snapshot_{SanitizeFileName(appId)}_{timestamp}.txt");

            var sb = new StringBuilder();
            sb.AppendLine("=== BR Unistaller — Snapshot ===");
            sb.AppendLine($"AppId: {appId}");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine();

            // 1. Install location
            sb.AppendLine("--- Install Location ---");
            sb.AppendLine($"Path: {installLocation}");
            sb.AppendLine($"Exists: {Directory.Exists(installLocation)}");

            if (Directory.Exists(installLocation))
            {
                long totalSize = GetDirectorySize(installLocation);
                int fileCount = CountFilesAndDirectories(installLocation, out int dirCount);
                sb.AppendLine($"Total Size: {totalSize} bytes ({FormatSize(totalSize)})");
                sb.AppendLine($"Files: {fileCount}");
                sb.AppendLine($"Directories: {dirCount}");
                sb.AppendLine();

                sb.AppendLine("--- Files ---");
                AppendDirectoryContents(sb, installLocation, 0, 3);
            }

            sb.AppendLine();

            // 2. Registry
            sb.AppendLine("--- Registry ---");
            var registryPaths = GetRegistryPathsForApp(appId);
            foreach (var regPath in registryPaths)
            {
                sb.AppendLine($"Key: {regPath.Path}");
                sb.AppendLine($"Exists: {regPath.Exists}");
                if (regPath.Exists)
                {
                    var values = regPath.Values;
                    if (values.Count > 0)
                    {
                        sb.AppendLine("  Values:");
                        foreach (var (name, value) in values)
                        {
                            sb.AppendLine($"    {name} = {value}");
                        }
                    }
                }
                sb.AppendLine();
            }

            // 3. Environment info
            sb.AppendLine("--- Environment ---");
            sb.AppendLine($"ProgramData: {Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}");
            sb.AppendLine($"AppData(Roaming): {Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}");
            sb.AppendLine($"AppData(Local): {Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}");
            sb.AppendLine($"ProgramFiles: {Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}");
            sb.AppendLine($"UserProfile: {Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}");

            File.WriteAllText(snapshotFile, sb.ToString(), Encoding.UTF8);

            _logger.LogInformation("Snapshot saved to: {SnapshotFile} ({Size} bytes)",
                snapshotFile, new FileInfo(snapshotFile).Length);

            return Task.FromResult(snapshotFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create snapshot for app {AppId}", appId);

            // Create a minimal fallback snapshot
            var fallbackFile = Path.Combine(
                Path.GetTempPath(),
                $"BRUTAL_Snapshot_{SanitizeFileName(appId)}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            try
            {
                File.WriteAllText(fallbackFile,
                    $"BR Unistaller Snapshot (fallback)\nAppId: {appId}\nTimestamp: {DateTime.Now}\nError: {ex.Message}");
            }
            catch
            {
                // Last resort
            }

            return Task.FromResult(fallbackFile);
        }
    }

    #region Snapshot Helpers

    private List<(string Path, bool Exists, List<(string Name, string Value)> Values)> GetRegistryPathsForApp(string appId)
    {
        var results = new List<(string, bool, List<(string, string)>)>();

        // Common registry paths where app uninstall info might be stored
        var pathsToCheck = new[]
        {
            (RegistryHive.LocalMachine, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{appId}"),
            (RegistryHive.LocalMachine, $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{appId}"),
            (RegistryHive.CurrentUser, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{appId}"),
        };

        foreach (var (hive, subKey) in pathsToCheck)
        {
            var displayPath = $"{hive}\\{subKey}";
            using var key = _registry.OpenRegistryKey(hive, subKey);
            if (key != null)
            {
                var values = new List<(string, string)>();
                try
                {
                    foreach (var valueName in key.GetValueNames())
                    {
                        var val = key.GetValue(valueName);
                        values.Add((valueName, val?.ToString() ?? "(null)"));
                    }
                }
                catch
                {
                    values.Add(("(error)", "Could not enumerate values"));
                }

                results.Add((displayPath, true, values));
            }
            else
            {
                results.Add((displayPath, false, new List<(string, string)>()));
            }
        }

        return results;
    }

    private static void AppendDirectoryContents(StringBuilder sb, string directory, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;

        try
        {
            var indent = new string(' ', depth * 2);

            foreach (var file in Directory.GetFiles(directory))
            {
                var fileInfo = new FileInfo(file);
                sb.AppendLine($"{indent}{fileInfo.Name} ({FormatSize(fileInfo.Length)})");
            }

            foreach (var subDir in Directory.GetDirectories(directory))
            {
                var dirInfo = new DirectoryInfo(subDir);
                sb.AppendLine($"{indent}[{dirInfo.Name}]");
                AppendDirectoryContents(sb, subDir, depth + 1, maxDepth);
            }
        }
        catch (UnauthorizedAccessException)
        {
            sb.AppendLine($"{new string(' ', depth * 2)}(access denied)");
        }
    }

    private static long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    size += new FileInfo(file).Length;
                }
                catch
                {
                    // Skip inaccessible files
                }
            }
        }
        catch
        {
            // Skip inaccessible directories
        }
        return size;
    }

    private static int CountFilesAndDirectories(string path, out int dirCount)
    {
        int files = 0;
        dirCount = 0;
        try
        {
            files = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
            dirCount = Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Length;
        }
        catch
        {
            // Skip inaccessible
        }
        return files;
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        return sb.ToString();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    #endregion
}
