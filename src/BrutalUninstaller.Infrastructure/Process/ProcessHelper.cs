using System.Diagnostics;
using System.Management;
using Microsoft.Extensions.Logging;
using BrutalUninstaller.Core.Interfaces;

namespace BrutalUninstaller.Infrastructure.Process;

public sealed class ProcessHelper : IProcessHelper
{
    private readonly ILogger<ProcessHelper> _logger;

    public ProcessHelper(ILogger<ProcessHelper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool RunProcess(string fileName, string arguments, bool waitForExit, out int exitCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        exitCode = -1;

        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            _logger.LogDebug("Starting process: {FileName} {Arguments} (WaitForExit: {WaitForExit})", fileName, arguments, waitForExit);

            if (!process.Start())
            {
                _logger.LogError("Failed to start process: {FileName} {Arguments}", fileName, arguments);
                return false;
            }

            if (waitForExit)
            {
                process.WaitForExit();
                exitCode = process.ExitCode;
                _logger.LogTrace("Process exited with code {ExitCode}: {FileName}", exitCode, fileName);
            }
            else
            {
                _logger.LogTrace("Process started (detached): {FileName} (PID: {PID})", fileName, process.Id);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error running process: {FileName} {Arguments}", fileName, arguments);
            return false;
        }
    }

    /// <inheritdoc />
    public bool RunProcessSilent(string fileName, string arguments, int timeoutMs, out int exitCode, out string stdOut)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        exitCode = -1;
        stdOut = string.Empty;

        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                },
                EnableRaisingEvents = true
            };

            _logger.LogDebug("Starting silent process: {FileName} {Arguments} (Timeout: {TimeoutMs}ms)", fileName, arguments, timeoutMs);

            if (!process.Start())
            {
                _logger.LogError("Failed to start silent process: {FileName} {Arguments}", fileName, arguments);
                return false;
            }

            // Read both stdout and stderr concurrently to avoid deadlocks
            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            if (process.WaitForExit(timeoutMs > 0 ? timeoutMs : Timeout.Infinite))
            {
                exitCode = process.ExitCode;
                stdOut = stdOutTask.Result;

                var stdErr = stdErrTask.Result;
                if (!string.IsNullOrEmpty(stdErr))
                {
                    _logger.LogWarning("Silent process stderr output: {StdErr}", stdErr);
                }

                _logger.LogTrace("Silent process exited with code {ExitCode}: {FileName}", exitCode, fileName);
                return true;
            }

            // Timeout — kill the process tree
            _logger.LogWarning("Silent process timed out after {TimeoutMs}ms, killing: {FileName} {}", timeoutMs, fileName, arguments);

            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception killEx)
            {
                _logger.LogError(killEx, "Failed to kill timed-out process: {FileName}", fileName);
            }

            exitCode = process.ExitCode;
            stdOut = stdOutTask.IsCompleted ? stdOutTask.Result : string.Empty;
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error running silent process: {FileName} {Arguments}", fileName, arguments);
            return false;
        }
    }

    /// <inheritdoc />
    public bool KillProcess(string processName)
    {
        ArgumentException.ThrowIfNullOrEmpty(processName);

        try
        {
            // Strip .exe extension if present for consistency
            var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;

            var processes = System.Diagnostics.Process.GetProcessesByName(name);

            if (processes.Length == 0)
            {
                _logger.LogDebug("No running processes found: {ProcessName}", processName);
                return false;
            }

            _logger.LogInformation("Killing {Count} instance(s) of {ProcessName}", processes.Length, processName);

            foreach (var process in processes)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                    _logger.LogTrace("Killed process {ProcessName} (PID: {PID})", processName, process.Id);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    _logger.LogWarning(ex, "Access denied killing {ProcessName} (PID: {PID})", processName, process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error killing process: {ProcessName}", processName);
            return false;
        }
    }

    /// <inheritdoc />
    public bool KillProcessTree(int processId)
    {
        try
        {
            var parentProcess = System.Diagnostics.Process.GetProcessById(processId);
            _logger.LogInformation("Killing process tree for PID {ProcessId} ({Name})", processId, parentProcess.ProcessName);

            // Kill child processes first (using WMI to enumerate children)
            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_Process WHERE ParentProcessId = {processId}");

            foreach (var obj in searcher.Get())
            {
                try
                {
                    var childId = Convert.ToInt32(obj["ProcessId"]);
                    KillProcessTree(childId); // recursive
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enumerate/kill child process of PID {ProcessId}", processId);
                }
            }

            // Kill the parent process itself
            try
            {
                parentProcess.Kill(entireProcessTree: true);
                parentProcess.WaitForExit(3000);
                _logger.LogTrace("Killed process tree root: PID {ProcessId}", processId);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogWarning(ex, "Access denied killing PID {ProcessId}", processId);
                return false;
            }

            return true;
        }
        catch (ArgumentException)
        {
            _logger.LogDebug("Process with PID {ProcessId} not found", processId);
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error killing process tree for PID {ProcessId}", processId);
            return false;
        }
    }

    /// <inheritdoc />
    public bool IsProcessRunning(string processName)
    {
        ArgumentException.ThrowIfNullOrEmpty(processName);

        try
        {
            var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;

            var processes = System.Diagnostics.Process.GetProcessesByName(name);
            var isRunning = processes.Length > 0;

            foreach (var p in processes) p.Dispose();

            _logger.LogTrace("Process {ProcessName} is running: {IsRunning}", processName, isRunning);
            return isRunning;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error checking if process is running: {ProcessName}", processName);
            return false;
        }
    }
}
