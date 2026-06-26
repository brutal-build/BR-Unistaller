using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;
using BrutalUninstaller.Core.Enums;
using BrutalUninstaller.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BrutalUninstaller.Core.Tests.Services;

public class ScanEngineTests
{
    private readonly Mock<IRegistryHelper> _registryMock;
    private readonly Mock<IFileSystemHelper> _fileSystemMock;
    private readonly Mock<ILogger<ScanEngine>> _loggerMock;
    private readonly ScanEngine _engine;

    public ScanEngineTests()
    {
        _registryMock = new Mock<IRegistryHelper>();
        _fileSystemMock = new Mock<IFileSystemHelper>();
        _loggerMock = new Mock<ILogger<ScanEngine>>();

        _engine = new ScanEngine(
            _registryMock.Object,
            _fileSystemMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ScanForTracesAsync_WithValidApp_ReturnsResults()
    {
        // Arrange
        var app = new InstalledApp
        {
            DisplayName = "TestApp",
            Publisher = "TestCorp"
        };

        // Act
        var result = await _engine.ScanForTracesAsync(app);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DeleteSelectedTracesAsync_WithEmptyList_ReturnsTrue()
    {
        // Act
        var result = await _engine.DeleteSelectedTracesAsync(new List<ScanResult>());

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteSelectedTracesAsync_WithSelectedRegistryTraces_DeletesKeys()
    {
        // Arrange
        _registryMock
            .Setup(r => r.DeleteKey(It.IsAny<Microsoft.Win32.RegistryHive>(), It.IsAny<string>(), It.IsAny<Microsoft.Win32.RegistryView>()))
            .Returns(true);

        var traces = new List<ScanResult>
        {
            new()
            {
                Type = ScanResultType.RegistryKey,
                Path = @"HKLM\SOFTWARE\TestCorp\TestApp",
                Selected = true
            }
        };

        // Act
        var result = await _engine.DeleteSelectedTracesAsync(traces);

        // Assert
        Assert.True(result);
        _registryMock.Verify(r => r.DeleteKey(It.IsAny<Microsoft.Win32.RegistryHive>(), It.IsAny<string>(), It.IsAny<Microsoft.Win32.RegistryView>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeleteSelectedTracesAsync_WithUnselectedTraces_SkipsThem()
    {
        // Arrange
        var traces = new List<ScanResult>
        {
            new()
            {
                Type = ScanResultType.RegistryKey,
                Path = @"HKLM\SOFTWARE\TestCorp\TestApp",
                Selected = false
            }
        };

        // Act
        var result = await _engine.DeleteSelectedTracesAsync(traces);

        // Assert
        Assert.True(result);
        _registryMock.Verify(r => r.DeleteKey(It.IsAny<Microsoft.Win32.RegistryHive>(), It.IsAny<string>(), It.IsAny<Microsoft.Win32.RegistryView>()), Times.Never);
    }
}
