namespace BrutalUninstaller.Core.Interfaces;

public interface IFileSystemHelper
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    long GetDirectorySize(string path);
    bool DeleteDirectory(string path, bool recursive = true);
    bool DeleteFile(string path);
    string[] GetDirectories(string path, string searchPattern = "*");
    string[] GetFiles(string path, string searchPattern = "*");
    bool IsDirectoryEmpty(string path);
}
