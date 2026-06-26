using System.Collections.ObjectModel;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrutalUninstaller.App.ViewModels;

public partial class JunkCleanerViewModel : ObservableObject
{
    private readonly IJunkCleaner _junkCleaner;

    [ObservableProperty]
    private ObservableCollection<JunkItem> _items = new();

    [ObservableProperty]
    private long _totalSavings;

    [ObservableProperty]
    private string _statusText = "Ready";

    public Action? CloseAction { get; set; }

    public JunkCleanerViewModel(IJunkCleaner junkCleaner)
    {
        _junkCleaner = junkCleaner;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        StatusText = "Scanning junk locations...";
        var items = await _junkCleaner.ScanJunkLocationsAsync();
        Items = new ObservableCollection<JunkItem>(items);
        TotalSavings = _junkCleaner.CalculateSavings(items.Where(i => i.Selected).ToList());
        StatusText = $"Found {items.Count} junk items";
    }

    [RelayCommand]
    private async Task CleanAsync()
    {
        var selected = Items.Where(i => i.Selected).ToList();
        if (selected.Count == 0) return;
        StatusText = $"Cleaning {selected.Count} items...";
        await _junkCleaner.CleanSelectedAsync(selected);
        foreach (var item in selected) Items.Remove(item);
        StatusText = $"Cleaned {selected.Count} items";
    }

    [RelayCommand]
    private void Close() => CloseAction?.Invoke();
}
