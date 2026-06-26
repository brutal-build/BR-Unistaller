using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using BrutalUninstaller.Core.Enums;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Services;

public partial class UninstallEngine : IUninstallEngine
{
    private readonly IProcessHelper _processHelper;
    private readonly IMsiApi _msiApi;
    private readonly IUwpApi _uwpApi;
    private readonly IBackupService _backupService;
    private readonly IScanEngine _scanEngine;
    private readonly ILogger<UninstallEngine> _logger;

    public UninstallEngine(
        IProcessHelper processHelper,
        IMsiApi msiApi,
        IUwpApi uwpApi,
        IBackupService backupService,
        IScanEngine scanEngine,
        ILogger<UninstallEngine> logger)
    {
        _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
        _msiApi = msiApi ?? throw new ArgumentNullException(nameof(msiApi));
        _uwpApi = uwpApi ?? throw new ArgumentNullException(nameof(uwpApi));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _scanEngine = scanEngine ?? throw new ArgumentNullException(nameof(scanEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Performs a full uninstall pipeline: Backup → Uninstall → Scan → Delete traces → Report.
    /// </summary>
    public async Task<UninstallReport> UninstallAsync(
        InstalledApp app,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        var report = new UninstallReport
        {
            AppName = app.DisplayName,
            AppVersion = app.Version,
            Publisher = app.Publisher,
            UninstallDate = DateTime.Now,
        };

        _logger.LogInformation(
            "====== Starting uninstall of {AppName} (type: {AppType}, id: {AppId}) ======",
            app.DisplayName, app.Type, app.Id);

        try
        {
            ct.ThrowIfCancellationRequested();

            // ════════════════════════════════════════════════════════════════
            // Phase 1 – Backup (0% → 20%)
            // ════════════════════════════════════════════════════════════════
            progress?.Report(0);
            _logger.LogInformation("[Phase 1/5] Creating backup for {AppName}...", app.DisplayName);

            // 1a. System Restore Point
            bool restorePointCreated = await _backupService.CreateRestorePointAsync(
                $"BR Unistaller - Before uninstall: {app.DisplayName}");

            // 1b. Registry export (if uninstall info exists in registry)
            string? registryBackupPath = null;
            try
            {
                string regKeyPath = $@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{app.Id}";
                registryBackupPath = await _backupService.ExportRegistryKeyAsync(regKeyPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to export registry key for {AppName} (non-fatal)", app.DisplayName);
            }

            // 1c. Filesystem snapshot
            string snapshotPath = await _backupService.SnapshotCurrentStateAsync(app.Id, app.InstallLocation);

            report.BackupCreated = restorePointCreated;
            report.BackupPath = snapshotPath
                ?? registryBackupPath
                ?? string.Empty;

            _logger.LogInformation(
                "Backup complete — RestorePoint: {Rp}, Registry: {Reg}, Snapshot: {Snap}",
                restorePointCreated, registryBackupPath ?? "N/A", snapshotPath ?? "N/A");

            ct.ThrowIfCancellationRequested();

            // ════════════════════════════════════════════════════════════════
            // Phase 2 – Uninstall (20% → 50%)
            // ════════════════════════════════════════════════════════════════
            progress?.Report(20);
            _logger.LogInformation("[Phase 2/5] Executing uninstall for {AppName}...", app.DisplayName);

            int exitCode = -1;
            bool uninstallSucceeded = false;

            switch (app.Type)
            {
                case AppType.Win32:
                    exitCode = await UninstallWin32Async(app, ct);
                    uninstallSucceeded = exitCode == 0;
                    break;

                case AppType.MSI:
                    _logger.LogInformation("Uninstalling MSI product {ProductCode}...", app.Id);
                    exitCode = await _msiApi.ConfigureProductAsync(app.Id, 0, -1);
                    uninstallSucceeded = exitCode == 0;
                    _logger.LogInformation(
                        "MSI uninstall completed — exit code: {ExitCode}", exitCode);
                    break;

                case AppType.UWP:
                    _logger.LogInformation("Removing UWP package {PackageFullName}...", app.Id);
                    uninstallSucceeded = await _uwpApi.RemovePackageAsync(app.Id);
                    exitCode = uninstallSucceeded ? 0 : -1;
                    _logger.LogInformation(
                        "UWP removal completed — success: {Success}", uninstallSucceeded);
                    break;

                default:
                    _logger.LogWarning(
                        "Unknown app type {AppType} for {AppName}, falling back to Win32 uninstall",
                        app.Type, app.DisplayName);
                    exitCode = await UninstallWin32Async(app, ct);
                    uninstallSucceeded = exitCode == 0;
                    break;
            }

            report.UninstallSucceeded = uninstallSucceeded;
            report.ExitCode = exitCode;

            progress?.Report(50);
            ct.ThrowIfCancellationRequested();

            // ════════════════════════════════════════════════════════════════
            // Phase 3 – Scan for traces (50% → 70%)
            // ════════════════════════════════════════════════════════════════
            if (uninstallSucceeded)
            {
                _logger.LogInformation("[Phase 3/5] Scanning for leftover traces of {AppName}...", app.DisplayName);

                List<ScanResult> scanResults;
                try
                {
                    scanResults = await _scanEngine.ScanForTracesAsync(app, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Trace scan failed for {AppName}", app.DisplayName);
                    scanResults = new List<ScanResult>();
                }

                progress?.Report(70);
                ct.ThrowIfCancellationRequested();

                // ════════════════════════════════════════════════════════════
                // Phase 4 – Delete traces (70% → 90%)
                // ════════════════════════════════════════════════════════════
                var selectedTraces = scanResults.Where(r => r.Selected).ToList();
                var remainingTraces = scanResults.Where(r => !r.Selected).ToList();

                if (selectedTraces.Count > 0)
                {
                    _logger.LogInformation(
                        "[Phase 4/5] Deleting {Count} selected traces...", selectedTraces.Count);

                    try
                    {
                        bool tracesRemoved = await _scanEngine.DeleteSelectedTracesAsync(selectedTraces);
                        if (tracesRemoved)
                        {
                            report.RemovedTraces = selectedTraces;
                            _logger.LogInformation(
                                "Successfully removed {Count} traces", selectedTraces.Count);
                        }
                        else
                        {
                            // Some or all traces could not be removed; report as remaining
                            report.RemainingTraces.AddRange(selectedTraces);
                            report.RemainingTraces.AddRange(remainingTraces);
                            _logger.LogWarning(
                                "Some traces could not be removed for {AppName}", app.DisplayName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete traces for {AppName}", app.DisplayName);
                        report.RemainingTraces.AddRange(scanResults);
                    }
                }

                report.RemainingTraces.AddRange(remainingTraces);

                progress?.Report(90);
            }
            else
            {
                _logger.LogWarning(
                    "Uninstall reported failure (exit code: {ExitCode}), skipping trace scan",
                    exitCode);
            }

            // ════════════════════════════════════════════════════════════════
            // Phase 5 – Finalize report (90% → 100%)
            // ════════════════════════════════════════════════════════════════
            _logger.LogInformation(
                "[Phase 5/5] Uninstall of {AppName} completed. " +
                "Success: {Success}, ExitCode: {ExitCode}, " +
                "Traces removed: {Removed}, Traces remaining: {Remaining}",
                app.DisplayName, uninstallSucceeded, exitCode,
                report.RemovedTraces.Count, report.RemainingTraces.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Uninstall of {AppName} was cancelled by user", app.DisplayName);
            report.UninstallSucceeded = false;
            report.ExitCode = -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uninstall of {AppName} failed with unexpected error", app.DisplayName);
            report.UninstallSucceeded = false;
            report.ExitCode = -1;
        }

        progress?.Report(100);
        return report;
    }

    /// <summary>
    /// Kills all processes matching the given name. Returns true if at least one
    /// process was successfully terminated.
    /// </summary>
    public Task<bool> ForceKillProcessAsync(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return Task.FromResult(false);
        }

        _logger.LogInformation("Force killing process(es) matching: {ProcessName}", processName);

        bool killed = _processHelper.KillProcess(processName);

        if (killed)
        {
            _logger.LogInformation("Successfully terminated process(es): {ProcessName}", processName);
        }
        else
        {
            _logger.LogWarning("No running processes found for: {ProcessName}", processName);
        }

        return Task.FromResult(killed);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Handles uninstallation of a Win32 application by executing its UninstallString
    /// (or QuietUninstallString if available). Falls back to msiexec via IMsiApi if the
    /// command is an MSI invocation.
    /// </summary>
    private async Task<int> UninstallWin32Async(InstalledApp app, CancellationToken ct)
    {
        // Prefer quiet/silent uninstall string for non-interactive execution
        string commandLine = !string.IsNullOrWhiteSpace(app.QuietUninstallString)
            ? app.QuietUninstallString
            : app.UninstallString;

        if (string.IsNullOrWhiteSpace(commandLine))
        {
            _logger.LogWarning(
                "No UninstallString or QuietUninstallString available for {AppName}",
                app.DisplayName);
            return -1;
        }

        // Handle msiexec-style commands via the MSI API
        if (ContainsMsiexec(commandLine))
        {
            var match = MsiexecProductCodeRegex().Match(commandLine);
            if (match.Success)
            {
                string productCode = match.Groups[1].Value;
                _logger.LogInformation(
                    "Detected msiexec command for {AppName}, delegating to MSI API (product: {ProductCode})",
                    app.DisplayName, productCode);

                int msiExitCode = await _msiApi.ConfigureProductAsync(productCode, 0, -1);
                return msiExitCode;
            }

            _logger.LogWarning(
                "Could not extract product code from msiexec command: {Command}",
                commandLine);
            return -1;
        }

        // Parse command line into executable path and arguments
        var (fileName, arguments) = ParseCommandLine(commandLine);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogWarning(
                "Failed to parse executable from UninstallString for {AppName}: {Command}",
                app.DisplayName, commandLine);
            return -1;
        }

        _logger.LogInformation(
            "Running uninstaller for {AppName}: {FileName} {Arguments}",
            app.DisplayName, fileName, arguments);

        bool result = _processHelper.RunProcess(fileName, arguments, waitForExit: true, out int exitCode);

        if (!result)
        {
            _logger.LogError(
                "Failed to start uninstall process for {AppName}: {FileName}",
                app.DisplayName, fileName);
            return -1;
        }

        _logger.LogInformation(
            "Uninstaller for {AppName} exited with code: {ExitCode}",
            app.DisplayName, exitCode);

        return exitCode;
    }

    /// <summary>
    /// Parses a command-line string into an executable path and its arguments.
    /// Handles quoted paths (e.g. "C:\Program Files\App\uninstall.exe" /S).
    /// </summary>
    private static (string fileName, string arguments) ParseCommandLine(string commandLine)
    {
        commandLine = commandLine.Trim();

        if (commandLine.Length == 0)
            return (string.Empty, string.Empty);

        if (commandLine[0] == '"')
        {
            // Quoted executable path: "C:\Program Files\App\uninstall.exe" /S
            int endQuote = commandLine.IndexOf('"', 1);
            if (endQuote > 0)
            {
                string file = commandLine[1..endQuote];
                string args = commandLine[(endQuote + 1)..].Trim();
                return (file, args);
            }
        }

        // Unquoted: split on first space
        int firstSpace = commandLine.IndexOf(' ');
        if (firstSpace > 0)
        {
            string file = commandLine[..firstSpace];
            string args = commandLine[(firstSpace + 1)..].Trim();
            return (file, args);
        }

        // No arguments — entire string is the executable path
        return (commandLine, string.Empty);
    }

    private static bool ContainsMsiexec(string commandLine)
    {
        return commandLine.Contains("msiexec", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"/[ix]\s*\{?([a-fA-F0-9\-]+)\}?", RegexOptions.IgnoreCase)]
    private static partial Regex MsiexecProductCodeRegex();
}
