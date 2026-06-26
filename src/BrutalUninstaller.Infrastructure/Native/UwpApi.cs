using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.Logging;
using Windows.Foundation;
using Windows.Management.Deployment;
using BrutalUninstaller.Core.Interfaces;

namespace BrutalUninstaller.Infrastructure.Native;

public sealed class UwpApi : IUwpApi
{
    private readonly ILogger<UwpApi> _logger;
    private readonly PackageManager _packageManager;

    public UwpApi(ILogger<UwpApi> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _packageManager = new PackageManager();
    }

    /// <inheritdoc />
    public async Task<List<(string packageFullName, string displayName, string publisher)>> GetPackagesAsync()
    {
        var packages = new List<(string packageFullName, string displayName, string publisher)>();

        await Task.Run(() =>
        {
            try
            {
                var allPackages = _packageManager.FindPackages();

                foreach (var package in allPackages)
                {
                    try
                    {
                        var fullName = package.Id?.FullName ?? "Unknown";
                        var displayName = package.DisplayName ?? string.Empty;
                        var publisher = package.Id?.Publisher ?? string.Empty;

                        packages.Add((fullName, displayName, publisher));

                        _logger.LogTrace("Enumerated UWP package: {FullName}", fullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read UWP package info");
                    }
                }

                _logger.LogInformation("Enumerated {Count} UWP packages", packages.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate UWP packages");
            }
        });

        return packages;
    }

    /// <inheritdoc />
    public async Task<bool> RemovePackageAsync(string packageFullName)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageFullName);

        try
        {
            _logger.LogInformation("Removing UWP package: {PackageFullName}", packageFullName);

            var deploymentResult = await _packageManager.RemovePackageAsync(packageFullName).AsTask();

            if (deploymentResult.IsRegistered)
            {
                _logger.LogWarning("UWP package still registered after removal: {PackageFullName}", packageFullName);
                return false;
            }

            _logger.LogInformation("Successfully removed UWP package: {PackageFullName}", packageFullName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove UWP package: {PackageFullName}", packageFullName);
            return false;
        }
    }
}
