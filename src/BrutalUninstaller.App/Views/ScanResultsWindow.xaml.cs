using System.Windows;
using BrutalUninstaller.App.ViewModels;

namespace BrutalUninstaller.App.Views;

public partial class ScanResultsWindow : Window
{
    public ScanResultsWindow(ScanResultsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseAction = () =>
        {
            DialogResult = true;
            Close();
        };
        Loaded += async (_, _) => await viewModel.LoadTracesCommand.ExecuteAsync(null);
    }
}
