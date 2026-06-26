using System.Collections.ObjectModel;
using System.Windows;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;
using BrutalUninstaller.Core.Enums;
using BrutalUninstaller.App.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace BrutalUninstaller.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAppDiscoveryService _appDiscovery;
    private readonly IUninstallEngine _uninstallEngine;
    private readonly IScanEngine _scanEngine;
    private readonly IServiceProvider _services;
    private readonly ILogger<MainViewModel> _logger;
    private List<InstalledApp> _allApps = new();
    private string _currentCategory = "All";

    [ObservableProperty]
    private ObservableCollection<InstalledApp> _apps = new();

    [ObservableProperty]
    private InstalledApp? _selectedApp;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private int _totalApps;

    [ObservableProperty]
    private string _totalSize = "0 B";

    [ObservableProperty]
    private bool _hasSelection;

    public MainViewModel(
        IAppDiscoveryService appDiscovery,
        IUninstallEngine uninstallEngine,
        IScanEngine scanEngine,
        IServiceProvider services,
        ILogger<MainViewModel> logger)
    {
        _appDiscovery = appDiscovery;
        _uninstallEngine = uninstallEngine;
        _scanEngine = scanEngine;
        _services = services;
        _logger = logger;
    }

    partial void OnSelectedAppChanged(InstalledApp? value)
    {
        HasSelection = value is not null;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    public void FilterByCategory(string category)
    {
        _currentCategory = category;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = _allApps.AsEnumerable();

        // Category filter
        if (_currentCategory != "All")
        {
            filtered = _currentCategory switch
            {
                "Win32" => filtered.Where(a => a.Type == AppType.Win32 || a.Type == AppType.MSI),
                "Store Apps" => filtered.Where(a => a.Type == AppType.UWP),
                "Recently Installed" => filtered.OrderByDescending(a => a.InstallDate).Take(20),
                _ => filtered
            };
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLower();
            filtered = filtered.Where(a =>
                a.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Publisher.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        Apps = new ObservableCollection<InstalledApp>(filtered);
        TotalApps = Apps.Count;
    }

    [RelayCommand]
    private async Task LoadAppsAsync()
    {
        try
        {
            StatusText = "Scanning for installed applications...";
            _logger.LogInformation("Loading installed applications");

            _allApps = await _appDiscovery.DiscoverAllAppsAsync();
            ApplyFilters();

            TotalSize = FormatSize(_allApps.Sum(a => (long)a.EstimatedSize));
            StatusText = $"Found {_allApps.Count} applications";

            _logger.LogInformation("Loaded {Count} applications, total size: {Size}", _allApps.Count, TotalSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load applications");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _appDiscovery.RefreshAsync();
        await LoadAppsAsync();
    }

    [RelayCommand]
    private void SetCategory(string category)
    {
        FilterByCategory(category);
        StatusText = $"Showing: {category}";
    }

    [RelayCommand]
    private async Task UninstallSelectedAsync()
    {
        if (SelectedApp is null) return;

        try
        {
            StatusText = $"Uninstalling {SelectedApp.DisplayName}...";
            _logger.LogInformation("Starting uninstall of: {App}", SelectedApp.DisplayName);

            var progress = new Progress<int>(p => StatusText = $"Uninstalling... {p}%");
            var result = await _uninstallEngine.UninstallAsync(SelectedApp, progress);

            if (result.UninstallSucceeded)
            {
                StatusText = $"Successfully uninstalled {SelectedApp.DisplayName}";

                // Scan for remaining traces
                var traces = await _scanEngine.ScanForTracesAsync(SelectedApp);
                if (traces.Count > 0)
                {
                    var vm = _services.GetService(typeof(ScanResultsViewModel)) as ScanResultsViewModel;
                    if (vm is not null)
                    {
                        vm.SetTarget(SelectedApp);
                        vm.LoadTracesDirectly(traces);
                        var window = new ScanResultsWindow(vm);
                        window.Owner = System.Windows.Application.Current.MainWindow;
                        window.ShowDialog();
                    }
                }

                _logger.LogInformation("Uninstall succeeded: {App}, traces: {Traces}",
                    SelectedApp.DisplayName, result.RemovedTraces.Count);
                await RefreshAsync();
            }
            else
            {
                StatusText = $"Uninstall failed (exit code: {result.ExitCode})";
                _logger.LogWarning("Uninstall failed: {App}, exit code: {Code}",
                    SelectedApp.DisplayName, result.ExitCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uninstall failed for: {App}", SelectedApp.DisplayName);
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ScanTraces()
    {
        if (SelectedApp is null) return;

        try
        {
            StatusText = $"Scanning traces for {SelectedApp.DisplayName}...";

            var vm = _services.GetService(typeof(ScanResultsViewModel)) as ScanResultsViewModel;
            if (vm is null) return;

            vm.SetTarget(SelectedApp);
            var window = new ScanResultsWindow(vm);
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();

            StatusText = $"Scan complete for {SelectedApp.DisplayName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trace scan failed");
            StatusText = $"Scan error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task BatchUninstallAsync()
    {
        var selected = Apps.Where(a => a.IsValid).ToList();
        if (selected.Count == 0) return;

        var vm = new BatchProgressViewModel();
        foreach (var app in selected)
            vm.AddApp(app.DisplayName);

        var window = new BatchProgressView { DataContext = vm };
        vm.CloseAction = () => { window.DialogResult = true; window.Close(); };
        window.Owner = System.Windows.Application.Current.MainWindow;
        window.Show();

        StatusText = $"Batch uninstall: {selected.Count} apps...";
        int completed = 0;

        foreach (var app in selected)
        {
            vm.UpdateProgress(app.DisplayName, 10, "Uninstalling...");
            var progress = new Progress<int>(p => vm.UpdateProgress(app.DisplayName, p, "Uninstalling..."));
            var result = await _uninstallEngine.UninstallAsync(app, progress);
            if (result.UninstallSucceeded)
            {
                completed++;
                vm.MarkCompleted(app.DisplayName, true);
            }
            else
            {
                vm.MarkCompleted(app.DisplayName, false);
            }
        }

        StatusText = $"Batch complete: {completed}/{selected.Count} uninstalled";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ForceUninstallAsync()
    {
        if (SelectedApp is null) return;

        try
        {
            StatusText = $"Force uninstall: {SelectedApp.DisplayName}...";

            StatusText = $"Killing processes for {SelectedApp.DisplayName}...";
            await _uninstallEngine.ForceKillProcessAsync(SelectedApp.DisplayName);
            await Task.Delay(500);

            StatusText = $"Deep scanning traces for {SelectedApp.DisplayName}...";
            var traces = await _scanEngine.ScanForTracesAsync(SelectedApp);

            if (traces.Count == 0)
            {
                StatusText = $"No traces found for {SelectedApp.DisplayName}";
                return;
            }

            var vm = _services.GetService(typeof(ScanResultsViewModel)) as ScanResultsViewModel;
            if (vm is null) return;
            vm.SetTarget(SelectedApp);
            vm.LoadTracesDirectly(traces);

            var window = new ScanResultsWindow(vm);
            window.Title = "BR Unistaller — Force Uninstall";
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();

            StatusText = $"Force uninstall complete for {SelectedApp.DisplayName}";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Force uninstall failed for: {App}", SelectedApp.DisplayName);
            StatusText = $"Force uninstall error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ShowDetails()
    {
        if (SelectedApp is null) return;
        System.Windows.MessageBox.Show(
            $"App: {SelectedApp.DisplayName}\n" +
            $"Version: {SelectedApp.Version}\n" +
            $"Publisher: {SelectedApp.Publisher}\n" +
            $"Size: {FormatSize((long)SelectedApp.EstimatedSize)}\n" +
            $"Installed: {SelectedApp.InstallDate}\n" +
            $"Type: {SelectedApp.Type}\n" +
            $"Location: {SelectedApp.InstallLocation}\n" +
            $"Uninstall: {SelectedApp.UninstallString}",
            "Application Details",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void OpenStartupManager()
    {
        try
        {
            var vm = _services.GetService(typeof(StartupViewModel)) as StartupViewModel;
            if (vm is null) return;
            var window = new StartupView { DataContext = vm };
            vm.CloseAction = () => window.Close();
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Startup Manager");
            System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenJunkCleaner()
    {
        try
        {
            var vm = _services.GetService(typeof(JunkCleanerViewModel)) as JunkCleanerViewModel;
            if (vm is null)
            {
                System.Windows.MessageBox.Show("Failed to create Junk Cleaner", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            var window = new JunkCleanerView { DataContext = vm };
            vm.CloseAction = () => window.Close();
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Junk Cleaner");
            System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenScheduler()
    {
        System.Windows.MessageBox.Show(
            "Scheduler is not yet configured.\n\nYou can set automatic scan intervals here in a future update.",
            "Scheduler",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        System.Windows.MessageBox.Show(
            "BR Unistaller v0.0.1\n\n" +
            "• Dark mode: enabled\n" +
            "• Backup before uninstall: enabled\n" +
            "• Log level: Debug\n" +
            "• License: MIT (open source)",
            "Settings",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void OpenAbout()
    {
        System.Windows.MessageBox.Show(
            "BR Unistaller v0.0.1\n\n" +
            "A free, open-source alternative to Revo Uninstaller.\n\n" +
            "Built with C# / .NET 8 WPF\n" +
            "License: MIT\n\n" +
            "GitHub: github.com/brutalbuild",
            "About",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}
