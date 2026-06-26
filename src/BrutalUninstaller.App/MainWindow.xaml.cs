using System.Windows;
using BrutalUninstaller.App.ViewModels;

namespace BrutalUninstaller.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            // Trigger fade-in animation
            var fadeIn = TryFindResource("FadeInList") as System.Windows.Media.Animation.Storyboard;
            fadeIn?.Begin(this);

            await viewModel.LoadAppsCommand.ExecuteAsync(null);
        };
    }
}
