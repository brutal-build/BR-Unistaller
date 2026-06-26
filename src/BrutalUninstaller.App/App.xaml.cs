using System;
using System.IO;
using System.Windows;
using BrutalUninstaller.App.ViewModels;
using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Services;
using BrutalUninstaller.Infrastructure.Msi;
using BrutalUninstaller.Infrastructure.Native;
using BrutalUninstaller.Infrastructure.Process;
using BrutalUninstaller.Infrastructure.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace BrutalUninstaller.App;

public partial class App : Application
{
    private IHost? _host;

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host not initialized");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BrutalUninstaller",
            "logs",
            "app-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Async(a => a.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14))
            .CreateLogger();

        try
        {
            _host = CreateHostBuilder(e.Args).Build();
            Log.Information("BR Unistaller starting up");

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to start BR Unistaller");
            MessageBox.Show($"Fatal: {ex.Message}\n\n{ex}", "BR Unistaller", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Infrastructure
                services.AddSingleton<IRegistryHelper, RegistryHelper>();
                services.AddSingleton<IFileSystemHelper, FileSystemHelper>();
                services.AddSingleton<IProcessHelper, ProcessHelper>();
                services.AddSingleton<IMsiApi, MsiApi>();
                services.AddSingleton<IUwpApi, UwpApi>();

                // Core services
                services.AddSingleton<IAppDiscoveryService, AppDiscoveryService>();
                services.AddSingleton<IUninstallEngine, UninstallEngine>();
                services.AddSingleton<IScanEngine, ScanEngine>();
                services.AddSingleton<IBackupService, BackupService>();
                services.AddSingleton<IStartupManager, StartupManager>();
                services.AddSingleton<IJunkCleaner, JunkCleaner>();
                services.AddSingleton<ISchedulerService, SchedulerService>();
                services.AddSingleton<IExportService, ExportService>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<ScanResultsViewModel>();
                services.AddTransient<StartupViewModel>();
                services.AddTransient<JunkCleanerViewModel>();
                services.AddTransient<SettingsViewModel>();

                // Main window
                services.AddSingleton<MainWindow>();
            });

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        _host?.Dispose();
        base.OnExit(e);
    }
}
