namespace BrutalUninstaller.Core.Interfaces;

public interface IProcessHelper
{
    bool RunProcess(string fileName, string arguments, bool waitForExit, out int exitCode);
    bool RunProcessSilent(string fileName, string arguments, int timeoutMs, out int exitCode, out string stdOut);
    bool KillProcess(string processName);
    bool KillProcessTree(int processId);
    bool IsProcessRunning(string processName);
}
