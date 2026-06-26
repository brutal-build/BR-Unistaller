using Microsoft.Extensions.Logging;
using BrutalUninstaller.Core.Interfaces;

namespace BrutalUninstaller.Infrastructure.Native;

public sealed class FileSystemHelper : IFileSystemHelper
{
    private readonly ILogger<FileSystemHelper> _logger;

    public FileSystemHelper(ILogger<FileSystemHelper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool DirectoryExists(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var exists = Directory.Exists(path);
        _logger.LogTrace("DirectoryExists({Path}) = {Exists}", path, exists);
        return exists;
    }

    /// <inheritdoc />
    public bool FileExists(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var exists = File.Exists(path);
        _logger.LogTrace("FileExists({Path}) = {Exists}", path, exists);
        return exists;
    }

    /// <inheritdoc />
    public long GetDirectorySize(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!Directory.Exists(path))
        {
            _logger.LogDebug("Directory not found for size calculation: {Path}", path);
            return 0;
        }

        try
        {
            long totalSize = 0;

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += fileInfo.Length;
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogTrace("Access denied reading file size: {File}", file);
                }
                catch (IOException ex)
                {
                    _logger.LogTrace(ex, "IO error reading file size: {File}", file);
                }
            }

            _logger.LogDebug("Directory size for {Path}: {Size} bytes", path, totalSize);
            return totalSize;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied calculating directory size: {Path}", path);
            return 0;
        }
        catch (DirectoryNotFoundException)
        {
            _logger.LogDebug("Directory not found during size calculation: {Path}", path);
            return 0;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error calculating directory size: {Path}", path);
            return 0;
        }
    }

    /// <inheritdoc />
    public bool DeleteDirectory(string path, bool recursive = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!Directory.Exists(path))
        {
            _logger.LogDebug("Directory not found for deletion: {Path}", path);
            return false;
        }

        try
        {
            Directory.Delete(path, recursive);
            _logger.LogInformation("Deleted directory: {Path} (Recursive: {Recursive})", path, recursive);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied deleting directory: {Path}", path);
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            _logger.LogDebug("Directory not found (already deleted): {Path}", path);
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "IO error deleting directory: {Path} (may be in use)", path);
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error deleting directory: {Path}", path);
            return false;
        }
    }

    /// <inheritdoc />
    public bool DeleteFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!File.Exists(path))
        {
            _logger.LogDebug("File not found for deletion: {Path}", path);
            return false;
        }

        try
        {
            File.Delete(path);
            _logger.LogInformation("Deleted file: {Path}", path);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied deleting file: {Path}", path);
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "IO error deleting file: {Path} (may be in use)", path);
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error deleting file: {Path}", path);
            return false;
        }
    }

    /// <inheritdoc />
    public string[] GetDirectories(string path, string searchPattern = "*")
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        try
        {
            if (!Directory.Exists(path))
            {
                _logger.LogDebug("Directory not found for listing directories: {Path}", path);
                return Array.Empty<string>();
            }

            var directories = Directory.GetDirectories(path, searchPattern);
            _logger.LogTrace("GetDirectories({Path}, {Pattern}) = {Count} entries", path, searchPattern, directories.Length);
            return directories;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied listing directories: {Path}", path);
            return Array.Empty<string>();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error listing directories: {Path}", path);
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public string[] GetFiles(string path, string searchPattern = "*")
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        try
        {
            if (!Directory.Exists(path))
            {
                _logger.LogDebug("Directory not found for listing files: {Path}", path);
                return Array.Empty<string>();
            }

            var files = Directory.GetFiles(path, searchPattern);
            _logger.LogTrace("GetFiles({Path}, {Pattern}) = {Count} entries", path, searchPattern, files.Length);
            return files;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied listing files: {Path}", path);
            return Array.Empty<string>();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error listing files: {Path}", path);
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public bool IsDirectoryEmpty(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!Directory.Exists(path))
        {
            _logger.LogDebug("Directory not found for emptiness check: {Path}", path);
            return true;
        }

        try
        {
            var isEmpty = !Directory.EnumerateFileSystemEntries(path).Any();
            _logger.LogTrace("IsDirectoryEmpty({Path}) = {IsEmpty}", path, isEmpty);
            return isEmpty;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied checking if directory is empty: {Path}", path);
            return true; // Treat as empty if we can't access it
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error checking if directory is empty: {Path}", path);
            return true;
        }
    }
}
