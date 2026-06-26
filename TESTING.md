# Testing Guide — BRUTAL Uninstaller

> **Last updated:** June 2026
> **Target framework:** .NET 8 (Windows)

---

## Table of Contents

1. [Overview](#overview)
2. [Test Project Structure](#test-project-structure)
3. [Running Tests](#running-tests)
   - [Run All Tests](#run-all-tests)
   - [Run Tests for a Specific Project](#run-tests-for-a-specific-project)
   - [Run a Specific Test Class](#run-a-specific-test-class)
   - [Run a Specific Test Method](#run-a-specific-test-method)
   - [Run Tests by Category](#run-tests-by-category)
4. [Test Naming Conventions](#test-naming-conventions)
5. [Mocking Dependencies](#mocking-dependencies)
   - [Available Mock Interfaces](#available-mock-interfaces)
   - [Mocking Patterns](#mocking-patterns)
   - [Verifying Mock Interactions](#verifying-mock-interactions)
6. [Code Coverage](#code-coverage)
   - [Coverage Targets](#coverage-targets)
   - [Running Coverage Reports](#running-coverage-reports)
7. [CI/CD Integration](#cicd-integration)
   - [GitHub Actions Example](#github-actions-example)
   - [Azure DevOps Example](#azure-devops-example)
8. [Best Practices](#best-practices)
9. [Troubleshooting](#troubleshooting)

---

## Overview

BRUTAL Uninstaller uses **xUnit** for unit and integration testing, **Moq** for mocking dependencies, and **FluentAssertions** for expressive assertions. Code coverage is collected with **Coverlet**.

| Component        | Version |
| ---------------- | ------- |
| xUnit            | 2.5.3   |
| Moq              | 4.*     |
| FluentAssertions | 6.*     |
| Coverlet         | 6.0.0   |
| MSTest SDK       | 17.8.0  |

---

## Test Project Structure

The solution contains **two test projects**, mirroring the source projects:

```
src/
├── BrutalUninstaller.App/                  # WPF application (UI)
├── BrutalUninstaller.Core/                 # Core domain & interfaces
└── BrutalUninstaller.Infrastructure/       # Infrastructure implementations

tests/
├── BrutalUninstaller.Core.Tests/           # Tests for Core layer
│   ├── BrutalUninstaller.Core.Tests.csproj
│   ├── AppDiscoveryServiceTests.cs
│   ├── ScanEngineTests.cs
│   └── UninstallEngineTests.cs
│
└── BrutalUninstaller.Infrastructure.Tests/ # Tests for Infrastructure layer
    ├── BrutalUninstaller.Infrastructure.Tests.csproj
    └── ... (implementation-specific tests)
```

Both test projects target `net8.0-windows10.0.19041.0` and follow the same convention — the namespace mirrors the source namespace with a `.Tests` suffix:

| Source Namespace                  | Test Namespace                              |
| --------------------------------- | ------------------------------------------- |
| `BrutalUninstaller.Core.Services` | `BrutalUninstaller.Core.Tests.Services`    |
| `BrutalUninstaller.Infrastructure` | `BrutalUninstaller.Infrastructure.Tests`   |

---

## Running Tests

### Prerequisites

- .NET 8 SDK installed (`dotnet --version` should show `8.x.x`)
- Solution restored (`dotnet restore`)

### Run All Tests

```bash
# From the solution root
dotnet test

# With verbose output
dotnet test -v n
```

### Run Tests for a Specific Project

```bash
dotnet test tests/BrutalUninstaller.Core.Tests
dotnet test tests/BrutalUninstaller.Infrastructure.Tests
```

### Run a Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~AppDiscoveryServiceTests"
dotnet test --filter "FullyQualifiedName~UninstallEngineTests"
```

### Run a Specific Test Method

```bash
dotnet test --filter "FullyQualifiedName~DiscoverAllAppsAsync_WithRegistryData_ReturnsApps"
```

### Run Tests by Category

Tests can be filtered by traits, method name patterns, or any custom category attribute.

```bash
# Fast tests only (unit tests that don't touch the filesystem)
dotnet test --filter "Category=Unit"

# Integration tests only
dotnet test --filter "Category=Integration"

# Exclude slow tests
dotnet test --filter "Category!=Slow"
```

To use category attributes in test methods, add an xUnit `[Trait]` attribute:

```csharp
[Fact]
[Trait("Category", "Unit")]
public async Task ForceKillProcessAsync_WithValidName_KillsProcess()
{
    // ...
}

[Fact]
[Trait("Category", "Integration")]
[Trait("Category", "Slow")]
public async Task FullUninstallPipeline_EndToEnd_Succeeds()
{
    // ...
}
```

---

## Test Naming Conventions

All test methods follow the **`MethodName_Scenario_ExpectedBehavior`** pattern:

```
{MethodName}_{Scenario}_{ExpectedBehavior}
```

### Examples from the codebase

| Method Name                                                       | Description                                  |
| ----------------------------------------------------------------- | -------------------------------------------- |
| `DiscoverAllAppsAsync_WithRegistryData_ReturnsApps`               | Registry has data → apps are returned        |
| `DiscoverAllAppsAsync_WithEmptyRegistry_ReturnsEmptyList`         | No registry data → empty list                |
| `RefreshAsync_AfterDiscovery_ReloadsApps`                         | Refresh after discovery → reloads            |
| `ScanForTracesAsync_WithValidApp_ReturnsResults`                  | Valid app scanned → results returned         |
| `DeleteSelectedTracesAsync_WithEmptyList_ReturnsTrue`             | Empty trace list → returns true (no-op)      |
| `ForceKillProcessAsync_WithValidName_KillsProcess`                | Valid process → killed                       |
| `ForceKillProcessAsync_WithEmptyName_ReturnsFalse`                | Empty name → returns false                   |
| `UninstallAsync_Win32App_CallsProcessHelper`                      | Win32 uninstall → ProcessHelper called       |
| `UninstallAsync_WithProgress_ReportsProgress`                     | Progress callback → progress values reported |

### Test Class Naming

Test class = source class name + `Tests` suffix in the `.Tests` namespace:

```csharp
// Source: BrutalUninstaller.Core.Services.ScanEngine
// Test:   BrutalUninstaller.Core.Tests.Services.ScanEngineTests
```

### Arrange / Act / Assert

Every test method MUST include the three-part AAA comment structure:

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    // ... set up mocks, create objects, prepare inputs ...

    // Act
    // ... invoke the method under test ...

    // Assert
    // ... verify results and mock interactions ...
}
```

---

## Mocking Dependencies

All external dependencies are abstracted behind interfaces, making the entire Core layer fully testable with mocks.

### Available Mock Interfaces

| Interface              | Purpose                                    | Used By                        |
| ---------------------- | ------------------------------------------ | ------------------------------ |
| `IRegistryHelper`      | Registry read/write/delete operations      | AppDiscoveryService, ScanEngine |
| `IProcessHelper`       | Process management (start, kill, query)    | UninstallEngine                |
| `IFileSystemHelper`    | File and directory operations              | ScanEngine, JunkCleaner        |
| `IMsiApi`              | MSI installer database queries             | AppDiscoveryService, UninstallEngine |
| `IUwpApi`              | UWP/AppX package enumeration              | AppDiscoveryService, UninstallEngine |
| `IBackupService`       | Restore point creation and state snapshots | UninstallEngine                |
| `IScanEngine`          | Post-uninstall trace scanning              | UninstallEngine                |
| `IStartupManager`      | Startup program management                 | Core                           |
| `ISchedulerService`    | Scheduled scan/uninstall tasks             | Core                           |
| `IExportService`       | Export reports (CSV, JSON, HTML)          | Core                           |
| `IJunkCleaner`         | Temporary file and cache cleanup           | Core                           |

### Mocking Patterns

#### 1. Constructor-based setup (preferred)

Initialize all mocks in the test class constructor for reuse across tests:

```csharp
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
}
```

#### 2. Setup return values

```csharp
_registryMock
    .Setup(r => r.GetSubKeyNames(
        It.IsAny<RegistryHive>(),
        It.IsAny<string>(),
        It.IsAny<RegistryView>()))
    .Returns(new[] { "{GUID1}", "{GUID2}" });
```

#### 3. Setup async return values

```csharp
_msiApiMock
    .Setup(m => m.EnumProductsAsync())
    .ReturnsAsync(new List<(string, string)>());
```

#### 4. Setup `out` parameters

```csharp
_processHelperMock
    .Setup(p => p.RunProcessSilent(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<int>(),
        out It.Ref<int>.IsAny,
        out It.Ref<string>.IsAny!))
    .Returns(true);
```

#### 5. Setup exceptions

```csharp
_registryMock
    .Setup(r => r.DeleteKey(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<RegistryView>()))
    .Throws<UnauthorizedAccessException>();
```

### Verifying Mock Interactions

Use `Verify()` to assert that dependencies were called (or not called) as expected:

```csharp
// Called exactly once
_registryMock.Verify(r => r.DeleteKey(
    It.IsAny<RegistryHive>(),
    It.IsAny<string>(),
    It.IsAny<RegistryView>()), Times.Once);

// Never called
_registryMock.Verify(r => r.DeleteKey(
    It.IsAny<RegistryHive>(),
    It.IsAny<string>(),
    It.IsAny<RegistryView>()), Times.Never);

// Called at least once
_processHelperMock.Verify(p => p.KillProcess("testapp"), Times.AtLeastOnce);
```

---

## Code Coverage

### Coverage Targets

| Metric      | Target  |
| ----------- | ------- |
| **Lines**   | ≥ 80%   |
| **Branches** | ≥ 70%  |
| **Methods** | ≥ 85%   |

The primary target is **80%+ line coverage** across the Core and Infrastructure projects. The WPF App project is excluded from coverage requirements (UI code is tested separately via manual or integration tests).

### Running Coverage Reports

```bash
# Generate coverage report for all test projects
dotnet test --collect:"XPlat Code Coverage"

# Generate coverage report with a specific output format
dotnet test --collect:"XPlat Code Coverage" \
    --settings coverlet.runsettings

# Generate and merge reports for all test projects
dotnet test /p:CollectCoverage=true \
    /p:CoverletOutput=../coverage/ \
    /p:MergeWith=../coverage/coverage.json \
    /p:CoverletOutputFormat=json,cobertura
```

### Coverlet Configuration (`coverlet.runsettings`)

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>json,cobertura</Format>
          <Exclude>
            [BrutalUninstaller.App]*%2A
            [*.Tests]*%2A
          </Exclude>
          <Include>
            [BrutalUninstaller.Core]*%2A
            [BrutalUninstaller.Infrastructure]*%2A
          </Include>
          <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute</ExcludeByAttribute>
          <ExcludeByFile>**/*.g.cs,**/*.i.cs</ExcludeByFile>
          <Threshold>80</Threshold>
          <ThresholdType>line</ThresholdType>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### Viewing Coverage Reports

```bash
# Install dotnet-reportgenerator-globaltool (once)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate an HTML report
reportgenerator \
    -reports:tests/coverage/coverage.cobertura.xml \
    -targetdir:tests/coverage/report \
    -reporttypes:Html

# Open the report
start tests/coverage/report/index.html
```

---

## CI/CD Integration

### GitHub Actions

```yaml
name: Build & Test

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: windows-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Run tests with coverage
        run: |
          dotnet test --configuration Release `
            --no-build `
            --collect:"XPlat Code Coverage" `
            --settings coverlet.runsettings

      - name: Generate coverage report
        run: |
          dotnet tool install -g dotnet-reportgenerator-globaltool
          reportgenerator `
            -reports:**/coverage.cobertura.xml `
            -targetdir:coverage-report `
            -reporttypes:Html

      - name: Upload coverage artifact
        uses: actions/upload-artifact@v4
        with:
          name: coverage-report
          path: coverage-report/
```

### Azure DevOps Pipeline

```yaml
trigger:
  - main
  - develop

pool:
  vmImage: 'windows-latest'

steps:
  - task: DotNetCoreCLI@2
    displayName: Restore
    inputs:
      command: restore

  - task: DotNetCoreCLI@2
    displayName: Build
    inputs:
      command: build
      arguments: '--configuration Release --no-restore'

  - task: DotNetCoreCLI@2
    displayName: Test with Coverage
    inputs:
      command: test
      arguments: >
        --configuration Release
        --no-build
        --collect:"XPlat Code Coverage"
        --settings coverlet.runsettings
    env:
      DOTNET_ENVIRONMENT: CI

  - task: PublishCodeCoverageResults@2
    displayName: Publish Coverage
    inputs:
      summaryFileLocation: '$(Agent.TempDirectory)/**/coverage.cobertura.xml'
      pathToSources: '$(Build.SourcesDirectory)'
```

---

## Best Practices

### 1. One assertion concept per test

Each test should verify **one behavior**. Use `Assert.Single()` or FluentAssertions chaining for related checks, but don't test unrelated scenarios in a single test method.

### 2. Prefer `[Fact]` over `[Theory]` unless parameterized testing adds value

Use `[Theory]` with `[InlineData]` only when the same test logic applies to multiple inputs.

### 3. Use FluentAssertions for readable assertions

```csharp
// Instead of:
Assert.NotNull(result);
Assert.Equal("TestApp", result.AppName);

// Prefer:
result.Should().NotBeNull();
result.AppName.Should().Be("TestApp");
```

### 4. Name test methods clearly

Bad: `Test1`, `DeleteTraces`, `ScanTest`
Good: `DeleteSelectedTracesAsync_WithUnselectedTraces_SkipsThem`

### 5. Keep mocks minimal

Only mock the dependencies the method under test actually interacts with. If a mock isn't used in a particular test, don't set it up.

### 6. Verify behavior, not implementation

Prefer verifying **outcomes** (return values, observable state changes) over verifying internal mock calls. Use `Verify()` only when the side effect (e.g., deleting a registry key) is the actual behavior being tested.

### 7. Test boundary conditions

- Empty collections
- Null arguments
- Exception paths
- Maximum values
- Timeouts/cancellation

### 8. Keep tests fast

Unit tests should run in **milliseconds**. If a test touches the filesystem, registry, or network, mark it as `[Trait("Category", "Integration")]` and consider using a test fixture.

---

## Troubleshooting

| Issue                                | Solution                                                       |
| ------------------------------------ | -------------------------------------------------------------- |
| Tests fail with `FileNotFoundException` | Ensure the test project targets `net8.0-windows*`            |
| Tests don't show in Test Explorer    | Install `xunit.runner.visualstudio` NuGet package              |
| Coverlet report is empty             | Add `coverlet.collector` to test projects                      |
| `Moq` strict mode failures           | Switch to `MockBehavior.Loose` (default) or add explicit setups |
| Slow test execution                  | Check for integration tests touching disk/registry; add `[Trait("Category", "Slow")]` |
| Registry access denied in CI         | Mock `IRegistryHelper` — never access real registry in tests    |

---

## Quick Reference

```bash
# Run everything
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run a specific test
dotnet test --filter "ForceKillProcessAsync_WithValidName_KillsProcess"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~UninstallEngineTests"

# Run by category
dotnet test --filter "Category=Unit"

# Generate HTML coverage report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html
```

---

*This document is maintained alongside the codebase. Update it when adding new test projects, changing test frameworks, or modifying CI/CD pipelines.*
