using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;
using BrutalUninstaller.Core.Enums;
using BrutalUninstaller.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BrutalUninstaller.Core.Tests.Services;

public class UninstallEngineTests
{
    private readonly Mock<IProcessHelper> _processHelperMock;
    private readonly Mock<IMsiApi> _msiApiMock;
    private readonly Mock<IUwpApi> _uwpApiMock;
    private readonly Mock<IBackupService> _backupServiceMock;
    private readonly Mock<IScanEngine> _scanEngineMock;
    private readonly Mock<ILogger<UninstallEngine>> _loggerMock;
    private readonly UninstallEngine _engine;

    public UninstallEngineTests()
    {
        _processHelperMock = new Mock<IProcessHelper>();
        _msiApiMock = new Mock<IMsiApi>();
        _uwpApiMock = new Mock<IUwpApi>();
        _backupServiceMock = new Mock<IBackupService>();
        _scanEngineMock = new Mock<IScanEngine>();
        _loggerMock = new Mock<ILogger<UninstallEngine>>();

        _engine = new UninstallEngine(
            _processHelperMock.Object,
            _msiApiMock.Object,
            _uwpApiMock.Object,
            _backupServiceMock.Object,
            _scanEngineMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ForceKillProcessAsync_WithValidName_KillsProcess()
    {
        // Arrange
        _processHelperMock
            .Setup(p => p.KillProcess("testapp"))
            .Returns(true);

        // Act
        var result = await _engine.ForceKillProcessAsync("testapp");

        // Assert
        Assert.True(result);
        _processHelperMock.Verify(p => p.KillProcess("testapp"), Times.Once);
    }

    [Fact]
    public async Task ForceKillProcessAsync_WithEmptyName_ReturnsFalse()
    {
        // Act
        var result = await _engine.ForceKillProcessAsync("");

        // Assert
        Assert.False(result);
        _processHelperMock.Verify(p => p.KillProcess(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForceKillProcessAsync_WithNullName_ReturnsFalse()
    {
        // Arrange - force null via a string that IS null
        string? nullName = null;

        // Act
        var result = await _engine.ForceKillProcessAsync(nullName!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UninstallAsync_Win32App_CallsProcessHelper()
    {
        // Arrange
        var app = new InstalledApp
        {
            DisplayName = "TestApp",
            UninstallString = "C:\\Program Files\\TestApp\\uninstall.exe",
            Type = AppType.Win32,
            Publisher = "TestCorp",
            InstallLocation = "C:\\Program Files\\TestApp"
        };

        _processHelperMock
            .Setup(p => p.RunProcessSilent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), out It.Ref<int>.IsAny, out It.Ref<string>.IsAny!))
            .Returns(true);

        _backupServiceMock
            .Setup(b => b.CreateRestorePointAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _backupServiceMock
            .Setup(b => b.SnapshotCurrentStateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("snapshot.reg");

        _scanEngineMock
            .Setup(s => s.ScanForTracesAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScanResult>());

        // Act
        var result = await _engine.UninstallAsync(app);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestApp", result.AppName);
    }

    [Fact]
    public async Task UninstallAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var app = new InstalledApp
        {
            DisplayName = "ProgressApp",
            UninstallString = "uninstall.exe",
            Type = AppType.Win32
        };

        _processHelperMock
            .Setup(p => p.RunProcessSilent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), out It.Ref<int>.IsAny, out It.Ref<string>.IsAny!))
            .Returns(true);

        _backupServiceMock
            .Setup(b => b.CreateRestorePointAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _backupServiceMock
            .Setup(b => b.SnapshotCurrentStateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("snapshot.reg");

        _scanEngineMock
            .Setup(s => s.ScanForTracesAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScanResult>());

        var progressReports = new List<int>();
        var progress = new Progress<int>(p => progressReports.Add(p));

        // Act
        var result = await _engine.UninstallAsync(app, progress);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(progressReports);
        Assert.Contains(0, progressReports);
        Assert.Contains(100, progressReports);
    }
}
