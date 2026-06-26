using Microsoft.Extensions.Logging;
using Microsoft.Win32;

using BrutalUninstaller.Core.Enums;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;

namespace BrutalUninstaller.Core.Services;

public class AppDiscoveryService : IAppDiscoveryService
{
    private readonly IRegistryHelper _registry;
    private readonly IMsiApi _msiApi;
    private readonly IUwpApi _uwpApi;
    private readonly ILogger<AppDiscoveryService> _logger;
    private List<InstalledApp>? _cache;

    public event EventHandler? AppListChanged;

    public AppDiscoveryService(
        IRegistryHelper registry,
        IMsiApi msiApi,
        IUwpApi uwpApi,
        ILogger<AppDiscoveryService> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _msiApi = msiApi ?? throw new ArgumentNullException(nameof(msiApi));
        _uwpApi = uwpApi ?? throw new ArgumentNullException(nameof(uwpApi));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<InstalledApp>> DiscoverAllAppsAsync()
    {
        _logger.LogInformation("Starting application discovery (registry + MSI + UWP)...");

        // Dictionary keyed by DisplayName (case-insensitive) for deduplication
        var apps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

        // ── 1. Scan 4 registry branches ──────────────────────────────────
        var registryBranches = new[]
        {
            (Hive: RegistryHive.LocalMachine,
             SubKey: @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
             View: RegistryView.Registry64),
            (Hive: RegistryHive.LocalMachine,
             SubKey: @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
             View: RegistryView.Registry32),
            (Hive: RegistryHive.CurrentUser,
             SubKey: @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
             View: RegistryView.Registry64),
            (Hive: RegistryHive.CurrentUser,
             SubKey: @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
             View: RegistryView.Registry32),
        };

        foreach (var (hive, subKey, view) in registryBranches)
        {
            int countBefore = apps.Count;
            ScanRegistryBranch(hive, subKey, view, apps);
            _logger.LogDebug(
                "Scanned {Hive}\\{SubKey} ({View}): found {Count} apps (total: {Total})",
                hive, subKey, view, apps.Count - countBefore, apps.Count);
        }

        // ── 2. Scan MSI products ─────────────────────────────────────────
        try
        {
            var msiProducts = await _msiApi.EnumProductsAsync();
            _logger.LogDebug("Enumerated {Count} MSI products", msiProducts.Count);

            foreach (var (productCode, productName) in msiProducts)
            {
                if (string.IsNullOrWhiteSpace(productName))
                    continue;

                if (apps.TryGetValue(productName, out var existing))
                {
                    // Upgrade from Win32 to MSI if the registry entry was actually installed via MSI
                    if (existing.Type == AppType.Win32 && _msiApi.IsMsiProduct(productCode))
                    {
                        existing.Type = AppType.MSI;
                        existing.Id = productCode;
                        existing.UninstallString = $"msiexec /x {productCode}";
                    }
                }
                else
                {
                    string? publisher = null;
                    string? version = null;
                    string? installDate = null;
                    string? installLocation = null;

                    try
                    {
                        publisher = await _msiApi.GetProductInfoAsync(productCode, "Publisher");
                        version = await _msiApi.GetProductInfoAsync(productCode, "VersionString");
                        installDate = await _msiApi.GetProductInfoAsync(productCode, "InstallDate");
                        installLocation = await _msiApi.GetProductInfoAsync(productCode, "InstallLocation");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read MSI product info for {ProductCode}", productCode);
                    }

                    apps[productName] = new InstalledApp
                    {
                        Id = productCode,
                        DisplayName = productName,
                        Publisher = publisher ?? string.Empty,
                        Version = version ?? string.Empty,
                        InstallDate = installDate ?? string.Empty,
                        InstallLocation = installLocation ?? string.Empty,
                        UninstallString = $"msiexec /x {productCode}",
                        Type = AppType.MSI,
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate MSI products");
        }

        // ── 3. Scan UWP packages ─────────────────────────────────────────
        try
        {
            var uwpPackages = await _uwpApi.GetPackagesAsync();
            _logger.LogDebug("Enumerated {Count} UWP packages", uwpPackages.Count);

            foreach (var (fullName, displayName, publisher) in uwpPackages)
            {
                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                if (!apps.ContainsKey(displayName))
                {
                    apps[displayName] = new InstalledApp
                    {
                        Id = fullName,
                        DisplayName = displayName,
                        Publisher = publisher ?? string.Empty,
                        Type = AppType.UWP,
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate UWP packages");
        }

        // ── 4. Cache and return ──────────────────────────────────────────
        _cache = apps.Values
            .Where(a => a.IsValid)
            .OrderBy(a => a.DisplayName)
            .ToList();

        _logger.LogInformation(
            "Application discovery completed: {Count} apps found",
            _cache.Count);

        return new List<InstalledApp>(_cache);
    }

    private void ScanRegistryBranch(
        RegistryHive hive,
        string subKey,
        RegistryView view,
        Dictionary<string, InstalledApp> apps)
    {
        string[]? subKeyNames;
        try
        {
            subKeyNames = _registry.GetSubKeyNames(hive, subKey, view);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read subkeys from {Hive}\\{SubKey} ({View})", hive, subKey, view);
            return;
        }

        if (subKeyNames is null || subKeyNames.Length == 0)
            return;

        foreach (var name in subKeyNames)
        {
            try
            {
                string fullSubKey = $@"{subKey}\{name}";

                // Skip system components, updates, or hotfixes
                var systemComponent = _registry.GetValue(hive, fullSubKey, "SystemComponent", view);
                if (systemComponent == "1")
                    continue;

                // Skip Windows updates and hotfixes (KB entries without DisplayName are handled below)
                var parentDisplayName = _registry.GetValue(hive, fullSubKey, "ParentDisplayName", view);
                if (!string.IsNullOrWhiteSpace(parentDisplayName))
                    continue;

                string? displayName = _registry.GetValue(hive, fullSubKey, "DisplayName", view);
                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                string? publisher = _registry.GetValue(hive, fullSubKey, "Publisher", view) ?? string.Empty;
                string? version = _registry.GetValue(hive, fullSubKey, "DisplayVersion", view) ?? string.Empty;
                string? installDate = _registry.GetValue(hive, fullSubKey, "InstallDate", view) ?? string.Empty;
                string? sizeStr = _registry.GetValue(hive, fullSubKey, "EstimatedSize", view) ?? "0";
                string? uninstallStr = _registry.GetValue(hive, fullSubKey, "UninstallString", view) ?? string.Empty;
                string? quietUninstallStr = _registry.GetValue(hive, fullSubKey, "QuietUninstallString", view) ?? string.Empty;
                string? installLoc = _registry.GetValue(hive, fullSubKey, "InstallLocation", view) ?? string.Empty;
                string? iconPath = _registry.GetValue(hive, fullSubKey, "DisplayIcon", view) ?? string.Empty;

                ulong estimatedSize = 0;
                if (ulong.TryParse(sizeStr, out var parsedSize))
                    estimatedSize = parsedSize;

                var appType = AppType.Win32;

                // Detect MSI products by checking if the product code is known to MSI
                if (_msiApi.IsMsiProduct(name))
                {
                    appType = AppType.MSI;
                }

                var app = new InstalledApp
                {
                    Id = name,
                    DisplayName = displayName,
                    Publisher = publisher,
                    Version = version,
                    InstallDate = installDate,
                    EstimatedSize = estimatedSize * 1024, // Registry stores EstimatedSize in KB, convert to bytes
                    UninstallString = uninstallStr,
                    QuietUninstallString = quietUninstallStr,
                    InstallLocation = installLoc,
                    IconPath = iconPath,
                    Type = appType,
                };

                // Deduplicate: keep the first occurrence (typically from HKLM x64) unless
                // the new one has more info (e.g., it's from HKCU with user-specific data)
                if (!apps.ContainsKey(displayName))
                {
                    apps[displayName] = app;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read registry app entry {Name} from {Hive}\\{SubKey}",
                    name, hive, $@"{subKey}\{name}");
            }
        }
    }

    public Task<InstalledApp?> GetAppDetailsAsync(string appId)
    {
        if (_cache is null)
        {
            return Task.FromResult<InstalledApp?>(null);
        }

        var app = _cache.FirstOrDefault(a =>
            string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(app);
    }

    public async Task RefreshAsync()
    {
        _logger.LogInformation("Refreshing application list...");
        _cache = null;
        await DiscoverAllAppsAsync();
        AppListChanged?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Application list refreshed: {Count} apps", _cache?.Count ?? 0);
    }
}
