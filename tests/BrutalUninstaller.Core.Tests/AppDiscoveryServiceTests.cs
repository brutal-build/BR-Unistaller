using BrutalUninstaller.Core.Interfaces;
using BrutalUninstaller.Core.Models;
using BrutalUninstaller.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BrutalUninstaller.Core.Tests.Services;

public class AppDiscoveryServiceTests
{
    private readonly Mock<IRegistryHelper> _registryMock;
    private readonly Mock<IMsiApi> _msiApiMock;
    private readonly Mock<IUwpApi> _uwpApiMock;
    private readonly Mock<ILogger<AppDiscoveryService>> _loggerMock;
    private readonly AppDiscoveryService _service;

    public AppDiscoveryServiceTests()
    {
        _registryMock = new Mock<IRegistryHelper>();
        _msiApiMock = new Mock<IMsiApi>();
        _uwpApiMock = new Mock<IUwpApi>();
        _loggerMock = new Mock<ILogger<AppDiscoveryService>>();

        _service = new AppDiscoveryService(
            _registryMock.Object,
            _msiApiMock.Object,
            _uwpApiMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DiscoverAllAppsAsync_WithRegistryData_ReturnsApps()
    {
        // Arrange
        _registryMock
            .Setup(r => r.GetSubKeyNames(It.IsAny<Microsoft.Win32.RegistryHive>(), It.IsAny<string>(), It.IsAny<Microsoft.Win32.RegistryView>()))
            .Returns(new[] { "{GUID1}", "{GUID2}" });

        _registryMock
            .Setup(r => r.GetValue(It.IsAny<Microsoft.Win32.RegistryHive>(), It.IsAny<string>(), "DisplayName", It.IsAny<Microsoft.Win32.RegistryView>()))
            .Returns("FoundApp");

        _registryMock
            .Setup(r => r.GetValue(It.IsAny<Microsoft.Win32.RegistryHive>(), It.IsAny<string>(), "Publisher", It.IsAny<Microsoft.Win32.RegistryView>()))
            .Returns("FoundCorp");

        _msiApiMock
            .Setup(m => m.EnumProductsAsync())
            .ReturnsAsync(new List<(string, string)>());

        _uwpApiMock
            .Setup(u => u.GetPackagesAsync())
            .ReturnsAsync(new List<(string, string, string)>());

        // Act
        var apps = await _service.DiscoverAllAppsAsync();

        // Assert
        Assert.NotEmpty(apps);
        Assert.Contains(apps, a => a.DisplayName == "FoundApp");
    }

    [Fact]
    public async Task DiscoverAllAppsAsync_WithEmptyRegistry_ReturnsEmptyList()
    {
        // Arrange
        _registryMock
            .Setup(r => r.GetSubKeyNames(It.IsAny<Microsoft.Win32.RegistryHive>(), It.IsAny<string>(), It.IsAny<Microsoft.Win32.RegistryView>()))
            .Returns(Array.Empty<string>());

        _msiApiMock
            .Setup(m => m.EnumProductsAsync())
            .ReturnsAsync(new List<(string, string)>());

        _uwpApiMock
            .Setup(u => u.GetPackagesAsync())
            .ReturnsAsync(new List<(string, string, string)>());

        // Act
        var apps = await _service.DiscoverAllAppsAsync();

        // Assert
        Assert.Empty(apps);
    }

    [Fact]
    public async Task RefreshAsync_AfterDiscovery_ReloadsApps()
    {
        // Arrange
        _registryMock
            .Setup(r => r.GetSubKeyNames(It.IsAny<Microsoft.Win32.RegistryHive>(), It.IsAny<string>(), It.IsAny<Microsoft.Win32.RegistryView>()))
            .Returns(new[] { "{GUID}" });

        _registryMock
            .Setup(r => r.GetValue(It.IsAny<Microsoft.Win32.RegistryHive>(), It.IsAny<string>(), "DisplayName", It.IsAny<Microsoft.Win32.RegistryView>()))
            .Returns("App");

        _registryMock
            .Setup(r => r.GetValue(It.IsAny<Microsoft.Win32.RegistryHive>(), It.IsAny<string>(), "Publisher", It.IsAny<Microsoft.Win32.RegistryView>()))
            .Returns("Corp");

        _msiApiMock
            .Setup(m => m.EnumProductsAsync())
            .ReturnsAsync(new List<(string, string)>());

        _uwpApiMock
            .Setup(u => u.GetPackagesAsync())
            .ReturnsAsync(new List<(string, string, string)>());

        var firstBatch = await _service.DiscoverAllAppsAsync();

        // Change data for refresh
        _registryMock
            .Setup(r => r.GetSubKeyNames(It.IsAny<Microsoft.Win32.RegistryHive>(), It.IsAny<string>(), It.IsAny<Microsoft.Win32.RegistryView>()))
            .Returns(Array.Empty<string>());

        // Act
        await _service.RefreshAsync();
        var afterRefresh = await _service.DiscoverAllAppsAsync();

        // Assert
        Assert.NotEmpty(firstBatch);
        Assert.Empty(afterRefresh);
    }
}
