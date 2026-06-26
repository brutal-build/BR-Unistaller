using System.Collections.ObjectModel;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;
using BrutalUninstaller.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace BrutalUninstaller.App.ViewModels;

public partial class ScanResultsViewModel : ObservableObject
{
    private readonly IScanEngine _scanEngine;
    private readonly ILogger<ScanResultsViewModel> _logger;
    private InstalledApp? _targetApp;

    public Action? CloseAction { get; set; }

    [ObservableProperty]
    private string _appName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ScanResultGroup> _groupedResults = new();

    [ObservableProperty]
    private int _totalTraces;

    [ObservableProperty]
    private int _selectedTraces;

    [ObservableProperty]
    private string _statusText = "Scanning...";

    public ScanResultsViewModel(IScanEngine scanEngine, ILogger<ScanResultsViewModel> logger)
    {
        _scanEngine = scanEngine;
        _logger = logger;
    }

    public void SetTarget(InstalledApp app)
    {
        _targetApp = app;
        AppName = app.DisplayName;
    }

    public void LoadTracesDirectly(List<ScanResult> traces)
    {
        var grouped = traces
            .GroupBy(t => GetCategory(t.Type))
            .Select(g => new ScanResultGroup
            {
                Category = g.Key,
                CategoryColor = GetCategoryColor(g.Key),
                Items = new ObservableCollection<ScanResult>(g.ToList())
            })
            .ToList();

        GroupedResults = new ObservableCollection<ScanResultGroup>(grouped);
        TotalTraces = traces.Count;
        SelectedTraces = traces.Count(t => t.Selected);
        StatusText = $"Found {TotalTraces} traces";
    }

    [RelayCommand]
    private async Task LoadTracesAsync()
    {
        if (_targetApp is null) return;

        try
        {
            StatusText = $"Scanning traces for {_targetApp.DisplayName}...";
            var traces = await _scanEngine.ScanForTracesAsync(_targetApp);

            var grouped = traces
                .GroupBy(t => GetCategory(t.Type))
                .Select(g => new ScanResultGroup
                {
                    Category = g.Key,
                    CategoryColor = GetCategoryColor(g.Key),
                    Items = new ObservableCollection<ScanResult>(g)
                })
                .ToList();

            GroupedResults = new ObservableCollection<ScanResultGroup>(grouped);
            TotalTraces = traces.Count;
            SelectedTraces = traces.Count(t => t.Selected);

            StatusText = $"Found {TotalTraces} traces";
            _logger.LogInformation("Found {Count} traces for {App}", TotalTraces, _targetApp.DisplayName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan traces");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var group in GroupedResults)
        foreach (var item in group.Items)
            item.Selected = true;

        UpdateSelectedCount();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var group in GroupedResults)
        foreach (var item in group.Items)
            item.Selected = false;

        UpdateSelectedCount();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selected = GroupedResults
            .SelectMany(g => g.Items)
            .Where(i => i.Selected)
            .ToList();

        if (selected.Count == 0) return;

        try
        {
            StatusText = $"Deleting {selected.Count} traces...";
            var success = await _scanEngine.DeleteSelectedTracesAsync(selected);

            if (success)
            {
                // Remove deleted items from groups
                foreach (var group in GroupedResults)
                {
                    var toRemove = group.Items.Where(i => i.Selected).ToList();
                    foreach (var item in toRemove)
                        group.Items.Remove(item);
                }

                // Remove empty groups
                var emptyGroups = GroupedResults.Where(g => g.Items.Count == 0).ToList();
                foreach (var g in emptyGroups)
                    GroupedResults.Remove(g);

                UpdateSelectedCount();
                StatusText = $"Deleted {selected.Count} traces";
                _logger.LogInformation("Deleted {Count} traces", selected.Count);
            }
            else
            {
                StatusText = "Failed to delete some traces";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete traces");
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedTraces = GroupedResults
            .SelectMany(g => g.Items)
            .Count(i => i.Selected);
    }

    private static string GetCategory(ScanResultType type) => type switch
    {
        ScanResultType.RegistryKey or ScanResultType.RegistryValue => "Registry",
        ScanResultType.File or ScanResultType.Folder or ScanResultType.EmptyFolder => "Files",
        _ => "Shortcuts / Other"
    };

    private static string GetCategoryColor(string category) => category switch
    {
        "Registry" => "#4fc3f7",
        "Files" => "#ffb74d",
        _ => "#ce93d8"
    };
}

public class ScanResultGroup
{
    public string Category { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = "#4fc3f7";
    public ObservableCollection<ScanResult> Items { get; set; } = new();
}
