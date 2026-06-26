using System.Collections.ObjectModel;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrutalUninstaller.App.ViewModels;

public partial class StartupViewModel : ObservableObject
{
    private readonly IStartupManager _startupManager;

    [ObservableProperty]
    private ObservableCollection<StartupEntry> _entries = new();

    public Action? CloseAction { get; set; }

    public StartupViewModel(IStartupManager startupManager)
    {
        _startupManager = startupManager;
    }

    [RelayCommand]
    private async Task LoadEntriesAsync()
    {
        var entries = await _startupManager.GetStartupEntriesAsync();
        Entries = new ObservableCollection<StartupEntry>(entries);
    }

    [RelayCommand]
    private void Close() => CloseAction?.Invoke();
}
