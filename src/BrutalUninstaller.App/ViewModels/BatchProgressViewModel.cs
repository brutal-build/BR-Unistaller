using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrutalUninstaller.App.ViewModels;

public partial class BatchProgressViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<BatchItem> _items = new();

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _totalCount;

    public Action? CloseAction { get; set; }

    public void AddApp(string appName)
    {
        Items.Add(new BatchItem { AppName = appName, Status = "Queued", ProgressPercent = 0 });
        TotalCount = Items.Count;
    }

    public void UpdateProgress(string appName, int percent, string status)
    {
        var item = Items.FirstOrDefault(i => i.AppName == appName);
        if (item is null) return;
        item.ProgressPercent = percent;
        item.Status = status;
    }

    public void MarkCompleted(string appName, bool success)
    {
        var item = Items.FirstOrDefault(i => i.AppName == appName);
        if (item is null) return;
        item.ProgressPercent = 100;
        item.Status = success ? "Done" : "Failed";
        CompletedCount = Items.Count(i => i.Status is "Done" or "Failed");
    }

    [RelayCommand]
    private void Close() => CloseAction?.Invoke();
}

public partial class BatchItem : ObservableObject
{
    [ObservableProperty] private string _appName = string.Empty;
    [ObservableProperty] private string _status = "Queued";
    [ObservableProperty] private int _progressPercent;
}
