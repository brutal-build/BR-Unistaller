using CommunityToolkit.Mvvm.ComponentModel;

namespace BrutalUninstaller.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _darkMode = true;

    [ObservableProperty]
    private bool _autoScanEnabled;

    [ObservableProperty]
    private int _scanIntervalDays = 7;

    [ObservableProperty]
    private bool _backupEnabled = true;
}
