# BRUTAL Uninstaller - Technical Specification

## 1. Project Metadata

| Field | Value |
|------|---------|
| Name | **BRUTAL Uninstaller** |
| Technology | C# / .NET (WPF) |
| Platform | Windows 10/11 x64 |
| Permissions | Always Administrator (`requireAdministrator`) |
| UI | Modern (Win11: Mica, dark/light mode, rounded corners) |
| License | Open Source (MIT) |
| Pattern | Free 1:1 alternative to Revo Uninstaller |

---

## 2. System Architecture

```
┌──────────────────────────────────────────────────────┐
│                   BRUTAL Uninstaller                 │
├──────────────────────────────────────────────────────┤
│  UI Layer (WPF)                                      │
│  ┌─────────────┐ ┌─────────────┐ ┌────────────────┐  │
│  │  MainWindow  │ │  HunterBar  │ │  Settings     │  │
│  │  (app list)  │ │  (overlay)  │ │               │  │
│  └──────┬───────┘ └──────┬──────┘ └───────┬───────┘  │
│         │                │                │          │
├─────────┼────────────────┼────────────────┼──────────┤
│  ViewModels / MVVM Toolkit                           │
│  ┌──────┴───────┐ ┌──────┴──────┐ ┌───────┴───────┐  │
│  │ AppListVM    │ │ HunterVM    │ │ SettingsVM    │  │
│  └──────┬───────┘ └──────┬──────┘ └───────┬───────┘  │
├─────────┼────────────────┼────────────────┼──────────┤
│  Services Layer                                      │
│  ┌──────┴───────┐ ┌──────┴──────┐ ┌───────┴───────┐  │
│  │ AppDiscovery │ │ Uninstall   │ │ ScanEngine    │  │
│  │ Service      │ │ Engine      │ │ (Registry +   │  │
│  │              │ │             │ │  FileSystem)  │  │
│  └──────┬───────┘ └──────┬──────┘ └───────┬───────┘  │
│  ┌──────┴───────┐ ┌──────┴──────┐ ┌───────┴───────┐  │
│  │ Backup       │ │ Scheduler   │ │ Export        │  │
│  │ Service      │ │ Service     │ │ Service       │  │
│  └──────┬───────┘ └──────┬──────┘ └───────┬───────┘  │
├─────────┼────────────────┼────────────────┼──────────┤
│  Core / Infrastructure                               │
│  ┌──────┴───────┐ ┌──────┴──────┐ ┌───────┴───────┐  │
│  │ Registry     │ │ FileSystem  │ │ Process       │  │
│  │ Helper       │ │ Helper      │ │ Helper        │  │
│  └──────────────┘ └─────────────┘ └───────────────┘  │
│  ┌──────────────┐ ┌─────────────┐ ┌───────────────┐  │
│  │ MSI / Win32  │ │ UWP / Store │ │ COM / WMI     │  │
│  │ API Wrapper  │ │ API Wrapper │ │ Wrapper       │  │
│  └──────────────┘ └─────────────┘ └───────────────┘  │
│  ┌──────────────────────────────────────────────┐    │
│  │             Logging / Diagnostics            │    │
│  └──────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────┘
```

### 2.1 Technology Stack

| Component | Technology | Justification |
|-----------|-------------|-------------|
| UI Framework | **WPF (.NET 8)** | Native Windows, MVVM, performant DataGrid |
| MVVM | **CommunityToolkit.Mvvm** | `[ObservableProperty]`, `[RelayCommand]` |
| DI Container | **Microsoft.Extensions.DependencyInjection** | Built-in, lightweight |
| Logging | **Serilog** | Structured logging |
| Win32 API | **P/Invoke** (advapi32, shell32, msi.dll) | Installer and registry access |
| UWP API | **Windows.Management.Deployment** | Store apps management |
| Dark Mode | **WPF-UI / ModernWpf** | Mica, Acrylic, Win11 themes |
| Configuration | **appsettings.json** + JSON serializer | Settings portability |
| Installer | **WiX Toolset** / **MSIX** | MSI for distribution |
| Tests | **xUnit + Moq** | Unit + integration tests |

### 2.2 Solution Structure

```
BrutalUninstaller.sln
├── src/
│   ├── BrutalUninstaller.App/          # WPF UI
│   │   ├── App.xaml(.cs)
│   │   ├── MainWindow.xaml(.cs)
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Controls/
│   │   ├── Converters/
│   │   ├── Themes/
│   │   │   ├── Dark.xaml
│   │   │   └── Light.xaml
│   │   └── Resources/
│   ├── BrutalUninstaller.Core/         # Business logic
│   │   ├── Models/
│   │   ├── Services/
│   │   ├── Interfaces/
│   │   └── Enums/
│   └── BrutalUninstaller.Infrastructure/  # Win32/COM/WMI
│       ├── Native/
│       ├── Registry/
│       ├── Process/
│       └── Msi/
└── tests/
    ├── BrutalUninstaller.Core.Tests/
    └── BrutalUninstaller.Infrastructure.Tests/
```

---

## 3. Functionalities (Core Features)

### 3.1 Application List (App Discovery)

```csharp
public interface IAppDiscoveryService
{
    Task<List<InstalledApp>> DiscoverAllAppsAsync();
    Task<InstalledApp> GetAppDetailsAsync(string appId);
    event EventHandler<AppListChangedEventArgs> AppListChanged;
}

public class InstalledApp
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Publisher { get; set; }
    public string Version { get; set; }
    public string InstallDate { get; set; }
    public ulong EstimatedSize { get; set; }
    public string UninstallString { get; set; }
    public string InstallLocation { get; set; }
    public AppType Type { get; set; }           // MSI, EXE, UWP, Steam, etc.
    public string IconPath { get; set; }
}
```

**Application Discovery Sources:**

| Source | API | Description |
|--------|-----|------|
| HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall | Registry (x64) | System applications |
| HKLM\SOFTWARE\WOW6432Node\...\Uninstall | Registry (x86) | 32-bit applications |
| HKCU\SOFTWARE\...\Uninstall | Registry (user) | Per-user applications |
| HKCU\SOFTWARE\WOW6432Node\...\Uninstall | Registry (user x86) | Per-user 32-bit applications |
| Windows.Management.Deployment.PackageManager | UWP API | Store / Modern apps |
| HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products | MSI DB | MSI products |
| Steam / Epic / GOG | (optional - future) | Applications from launchers |

### 3.2 Uninstall with Full Residual Scan

**Uninstall Flow:**

```
1. Select application (single / batch)
   │
   ▼
2. Backup (before removal)
   ├── System Restore Point  ──  SystemRestore.CreateRestorePoint()
   ├── Registry Export        ──  regedit /e {app}_backup.reg
   └── State log              ──  save paths before removal
   │
   ▼
3. Normal uninstall
   ├── Run UninstallString (silent or standard)
   ├── MSI: MsiConfigureProduct / MsiInstallProduct
   ├── UWP: PackageManager.RemovePackageAsync()
   └── Check exit code / remaining processes
   │
   ▼
4. Residual Scan (Post-Uninstall Scan)
   ├── Registry Scan ─── (HKCU, HKLM, HKU\SID)
   │   ├── SOFTWARE\{Publisher}\{AppName}
   │   ├── SOFTWARE\Classes\{CLSID}, TypeLib, Interface
   │   ├── SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{guid}
   │   └── SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths
   │
   ├── Filesystem Scan ───
   │   ├── %ProgramData%\{Publisher}\{AppName}
   │   ├── %AppData%\{Local,LocalLow,Roaming}\{Publisher}
   │   ├── %ProgramFiles%\{Publisher}\{AppName}
   │   ├── %CommonProgramFiles%\{Publisher}
   │   ├── %UserProfile%\Documents\{AppName}
   │   └── C:\ProgramData\Microsoft\Windows\Start Menu\{AppName}.lnk
   │
   └── Other objects ───
       ├── Services   ──  HKLM\SYSTEM\CurrentControlSet\Services\{AppService}
       ├── Drivers    ──  HKLM\SYSTEM\CurrentControlSet\Services\{AppDriver}
       ├── Planned Tasks ──  C:\Windows\System32\Tasks\{AppTask}
       ├── Firewall Rules ──  COM: INetFwPolicy2
       └── Context Menu   ──  HKCR\*\shell\{App}, HKCR\Directory\shell
   │
   ▼
5. Display found traces (check/uncheck)
   │
   ▼
6. Delete selected traces (with confirmation)
   │
   ▼
7. Report (log + export)
```

### 3.3 Force Uninstall

Used when the application has no proper uninstaller or the standard flow failed.

```
1. Analyze application residues (registry + filesystem only - without running the uninstaller)
2. Detect all keys, files, services, tasks
3. Full scan + delete everything related to name/publisher
4. Optional: deep scan (full disk search) for residues
```

### 3.4 Batch Uninstall (New feature vs Revo)

- Select multiple applications at once (checkboxes + multi-select)
- Sequential uninstall (one after another)
- Pool uninstall (max N parallel, where N is configurable)
- Progress bar per-app + global progress
- Resume after failure (skip or stop)

### 3.5 Startup Manager

| Category | Path |
|-----------|---------|
| Registry (HKLM) | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` |
| Registry (HKCU) | `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` |
| Registry (x64/x86) | as above + WOW6432Node |
| Startup Folder | `%AppData%\Microsoft\Windows\Start Menu\Programs\Startup` |
| Startup Folder (All) | `%ProgramData%\Microsoft\Windows\Start Menu\Programs\Startup` |
| Scheduled Tasks | `C:\Windows\System32\Tasks\` + SCHTASKS API |
| Services | HKLM\SYSTEM\CurrentControlSet\Services\ (Start=2 = auto) |

Features:
- List of entries with enable/disable capability
- Delete entries
- Add custom entries (optional)
- Startup delay (Delay launch)

### 3.6 Backup before removal

- **System Restore Point** — `SRSetRestorePointW` (srclient.dll)
- **Registry Export** — entire branches to `.reg` before modification
- **File list snapshot** — save paths before removal

### 3.7 Junk Files Cleanup

```
Scanned locations:
├── %Temp%
├── C:\Windows\Temp
├── %AppData%\Local\Temp
├── Prefetch (C:\Windows\Prefetch)
├── Recycle Bin
├── Browser Cache (Chrome, Firefox, Edge)
├── Windows Update Cache (C:\Windows\SoftwareDistribution\Download)
├── Thumbnail Cache
├── Log Files (C:\Windows\Logs, CBS)
└── Memory Dumps (*.dmp, *.mdmp)
```

### 3.8 Windows Apps / Store Apps

- API used: `Windows.Management.Deployment.PackageManager`
- List of all installed UWP/Store packages
- Uninstall via `RemovePackageAsync` or via `RemovePackageWithUserAsync`
- Residual scan for Store apps (AppData\Local\Packages\{PackageFamilyName}, etc.)

### 3.9 Background Scan (Scheduler) — New feature vs Revo

- Background timer (BackgroundService / Timer)
- Every N days, automatically scans traces of non-existent applications
- Minimal impact (low I/O priority, throttling)
- Notifications when orphaned traces are detected
- Configurable schedule (every 1/7/14/30 days)

### 3.10 Report Export — New feature vs Revo

| Format | Content |
|--------|-----------|
| **CSV** | Application list, removed traces, timestamp |
| **JSON** | Full structured data (machine-readable) |
| **HTML** | Formatted report with colors, for browser |
| **TXT / Log** | Plain text with timestamps |

---

## 4. Data Models

```csharp
public enum AppType
{
    Win32,
    MSI,
    UWP,
    Steam,
    Unknown
}

public enum ScanResultType
{
    RegistryKey,
    RegistryValue,
    File,
    Folder,
    Shortcut,
    Service,
    Driver,
    ScheduledTask,
    FirewallRule,
    ContextMenuEntry,
    EmptyFolder
}

public class ScanResult
{
    public ScanResultType Type { get; set; }
    public string Path { get; set; }
    public string Description { get; set; }
    public bool Selected { get; set; } = true;
    public long EstimatedSize { get; set; }
}
```

---

## 5. UI Views (WPF)

### 5.1 MainWindow — Application List

```
+--------------------------------------------------+
|  BRUTAL Uninstaller         [ Search...       ]  |
+----------------------+---------------------------+
|  [Batch Uninstall]   |                           |
|  [Force Uninstall]   |  Icon | Name    | Publisher|
|                      |  ------+--------+----------|
|  ------------------  |   PC   | App1   | PubCo    |
|  > All Applications   |   PC   | App2   | CorpX    |
|  > Win32              |   PKG  | App3   | Store    |
|  > Store Apps         |                           |
|  > Recently Installed |  [Details]  [Remove]      |
|  ------------------  |                           |
|  Tools                |  Size: 250 MB              |
|  > Startup Manager    |  Date: 2026-01-15         |
|  > Junk Cleaner       |  Type: MSI                |
|  > Scheduler          |  Version: 2.3.1           |
|  > Settings           |                           |
|  > About              |  [Reports]                |
+----------------------+---------------------------+
|  Status: 42 apps found | 2.1 GB total | Ready    |
+--------------------------------------------------+
```

### 5.2 Scan Results Window

```
+--------------------------------------------------+
|  Found traces of: AppName                        |
+--------------------------------------------------+
|  Registry (13 found)                             |
|    [X] HKLM\SOFTWARE\Publisher\AppName           |
|    [X] HKLM\SOFTWARE\Classes\CLSID\{...}         |
|    [ ] HKCU\SOFTWARE\Publisher\AppName           |
|                                                  |
|  Files (8 found)                                 |
|    [X] C:\Program Files\AppName\                 |
|    [X] C:\ProgramData\AppName\                   |
|    [X] C:\Users\...\AppData\Roaming\AppName      |
|                                                  |
|  Shortcuts / Other (3 found)                     |
|    [X] Start Menu\AppName.lnk                    |
|    [ ] Service: AppService                       |
|                                                  |
|        [Select all]  [Deselect]                  |
|        [Remove selected]  [Cancel]               |
+--------------------------------------------------+
```

### 5.3 Batch Uninstall Progress

```
+--------------------------------------------------+
|  Batch Uninstall (5 applications)                |
+--------------------------------------------------+
|  AppName 1  [####################]  100%  Done    |
|  AppName 2  [####################]  100%  Done    |
|  AppName 3  [##############......]   73%  Scanning|
|  AppName 4  [....................]    0%  Waiting |
|  AppName 5  [....................]    0%  Waiting |
|                                                  |
|  Total progress: [############....]  54%         |
|                                                  |
|        [Pause]  [Skip]  [Cancel]                 |
+--------------------------------------------------+
```

---

## 6. Key Components (Classes)

### 6.1 AppDiscoveryService

```csharp
public class AppDiscoveryService : IAppDiscoveryService
{
    // Opens registry keys (4 branches) + MSI + UWP
    // Merges results, removes duplicates
    // Caches results (refresh on F5)
    // Async enumeration (IAsyncEnumerable)
}
```

### 6.2 UninstallEngine

```csharp
public class UninstallEngine : IUninstallEngine
{
    // Performs uninstall via:
    //   - UninstallString (Process.Start + wait)
    //   - MsiConfigureProduct (msi.dll)
    //   - PackageManager.RemovePackageAsync (UWP)
    // Handles exit codes
    // Kill remaining processes (optional)
    // Progress callback to UI
}
```

### 6.3 ScanEngine

```csharp
public class ScanEngine : IScanEngine
{
    // RegistryScanner - searches HKCU, HKLM, HKU\UserSIDs,
    //   HKCR\CLSID, TypeLib, Interface, AppID
    // FileSystemScanner - enumerates common paths
    // ServiceScanner - HKLM\SYSTEM\CurrentControlSet\Services
    // TaskScanner - Scheduled tasks
    // FirewallScanner - COM: INetFwPolicy2
    // ContextMenuScanner - HKCR\*\shell, HKCR\Directory\shell,
    //   HKCR\Folder\shell
    // EmptyFolderDetector - finds empty folders after removal
}
```

### 6.4 BackupService

```csharp
public class BackupService : IBackupService
{
    // CreateRestorePoint(name) → SRSetRestorePointW
    // ExportRegistryKey(path) → RegExport
    // SnapshotCurrentState(appId) → saves paths
}
```

### 6.5 StartupManager

```csharp
public class StartupManager : IStartupManager
{
    // GetStartupEntries() → list from 7 locations
    // ToggleEntry(id, enabled)
    // RemoveEntry(id)
    // AddEntry(entry)
}
```

### 6.6 JunkCleaner

```csharp
public class JunkCleaner : IJunkCleaner
{
    // ScanJunkLocations() → List<JunkItem>
    // CleanSelected(items)
    // CalculateSavings() → estimated size
}
```

### 6.7 SchedulerService

```csharp
public class SchedulerService : ISchedulerService
{
    // Timer every N days
    // Background scan orphaned traces
    // Notify on found or schedule next scan
    // Low I/O priority
}
```

### 6.8 ExportService

```csharp
public class ExportService : IExportService
{
    // ExportToCsv(data, path)
    // ExportToJson(data, path)
    // ExportToHtml(data, path)
    // ExportUninstallLog(report)
}
```

---

## 7. Interfaces (Contracts)

```csharp
// --- Core ---
public interface IAppDiscoveryService { ... }
public interface IUninstallEngine { ... }
public interface IScanEngine { ... }
public interface IBackupService { ... }
public interface IStartupManager { ... }
public interface IJunkCleaner { ... }
public interface ISchedulerService { ... }
public interface IExportService { ... }

// --- Infrastructure ---
public interface IRegistryHelper { ... }
public interface IFileSystemHelper { ... }
public interface IProcessHelper { ... }
public interface IMsiApi { ... }
public interface IUwpApi { ... }
```

---

## 8. DI Registration (Program.cs / App.xaml.cs)

```csharp
services.AddSingleton<IAppDiscoveryService, AppDiscoveryService>();
services.AddSingleton<IUninstallEngine, UninstallEngine>();
services.AddSingleton<IScanEngine, ScanEngine>();
services.AddSingleton<IBackupService, BackupService>();
services.AddSingleton<IStartupManager, StartupManager>();
services.AddSingleton<IJunkCleaner, JunkCleaner>();
services.AddSingleton<ISchedulerService, SchedulerService>();
services.AddSingleton<IExportService, ExportService>();

services.AddTransient<MainViewModel>();
services.AddTransient<ScanResultsViewModel>();
services.AddTransient<StartupViewModel>();
services.AddTransient<JunkCleanerViewModel>();
services.AddTransient<SettingsViewModel>();
```

---

## 9. Implementation Schedule

### Phase 1 — MVP (Core Uninstall)

| Step | Description | Est. Time |
|------|------|-----------|
| 1.1 | Solution structure, DI, logging, config | 2 days |
| 1.2 | AppDiscoveryService — enumeration from registry + UWP | 3 days |
| 1.3 | MainWindow + MainViewModel — application list | 2 days |
| 1.4 | UninstallEngine — MSI + EXE + UWP | 3 days |
| 1.5 | ScanEngine — registry + files after uninstallation | 4 days |
| 1.6 | Scan Results Window — UI + ViewModel | 2 days |
| 1.7 | BackupService — Restore Point + Registry Export | 2 days |
| 1.8 | MVP integration tests | 2 days |

### Phase 2 — Extensions

| Step | Description | Est. Time |
|------|------|-----------|
| 2.1 | Force Uninstall (without uninstaller) | 3 days |
| 2.2 | Batch Uninstall + progress UI | 3 days |
| 2.3 | Startup Manager | 2 days |
| 2.4 | Junk Cleaner | 2 days |

### Phase 3 — New Features (better than Revo)

| Step | Description | Est. Time |
|------|------|-----------|
| 3.1 | Scheduler — background scan + notifications | 3 days |
| 3.2 | Export Service — CSV/JSON/HTML reports | 2 days |
| 3.3 | Dark/Light mode, Mica theme | 2 days |
| 3.4 | Tests, documentation, WiX installer | 3 days |

**Total estimated time: ~38 days (full-time dev)**

---

## 10. New Features vs Revo Uninstaller (Summary)

| Feature | Revo (Free) | Revo (Pro) | BRUTAL |
|---------|-------------|------------|--------|
| Application list (Win32 + UWP) | ✅ | ✅ | ✅ |
| Normal uninstall | ✅ | ✅ | ✅ |
| Residual scan (registry + files) | ✅ | ✅ | ✅ |
| Force uninstall | ❌ | ✅ | ✅ |
| Backup (Restore Point) | ❌ | ✅ | ✅ |
| Startup Manager | ✅ (Tools) | ✅ | ✅ |
| Junk Cleaner | ✅ (Tools) | ✅ | ✅ |
| Windows Store Apps | ❌ | ✅ | ✅ |
| **Batch Uninstall** | ❌ | ❌ | ✅ 🆕 |
| **Scheduler (background scan)** | ❌ | ❌ | ✅ 🆕 |
| **Report export (CSV/JSON/HTML)** | ❌ | ❌ | ✅ 🆕 |
| **Free (MIT license)** | ✅ (partial) | ❌ ($24.95+) | ✅ |

---

## 11. Required NuGet Packages

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.*" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.*" />
<PackageReference Include="Serilog.Sinks.File" Version="5.*" />
<PackageReference Include="Serilog.Sinks.Async" Version="1.*" />
<PackageReference Include="System.Management" Version="8.*" />           <!-- WMI -->
<PackageReference Include="Microsoft.Windows.CsWin32" Version="0.*" /> <!-- P/Invoke gen -->
<PackageReference Include="ModernWpfUI" Version="3.*" />                <!-- WinUI 3 style for WPF -->
<PackageReference Include="CsvHelper" Version="31.*" />                 <!-- CSV Export -->
<PackageReference Include="xUnit" Version="2.*" />                      <!-- Tests -->
<PackageReference Include="Moq" Version="4.*" />                        <!-- Mocking -->
```

---

## 12. Final Notes

- The program will require **administrator privileges** — manifest with `requireAdministrator`.
- Application **portable or installable** (WiX installer as an option).
- All registry and file operations are logged for safety.
- Before each destructive operation, a backup is created (System Restore Point).
- UI inspired by Windows 11 — Mica backdrop, rounded corners, dark/light theme.
- Fully open source code (MIT) on GitHub.
