# BRUTAL Uninstaller — Agents

> File defining agents, their roles, responsibilities and ready-made prompts for `delegate_task`.
> Project: C# / .NET 8 WPF — alternative to Revo Uninstaller.

---

## Stack

| Layer | Technology |
|---------|------------|
| UI Framework | WPF (.NET 8) |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| Logging | Serilog |
| Win32 API | P/Invoke (advapi32, shell32, msi.dll) |
| UWP API | Windows.Management.Deployment |
| Dark/Light | ModernWpfUI (Mica, Acrylic) |
| Tests | xUnit + Moq |
| Installer | WiX Toolset |
| Repo | GitHub, MIT license |

---

## Agent 1: Architect — Solution Architect

**Role:** Makes architectural decisions, designs the solution structure, defines interfaces.

**Responsibilities:**
- Designing class and interface hierarchies
- Decisions on patterns (MVVM, DI, Repository)
- Folder and namespace structure
- Defining contracts between services

**Delegate task prompt:**
```
Act as a C#/.NET solution architect for the "BRUTAL Uninstaller" project — an alternative to Revo Uninstaller.

Task: [describe specific architectural task]

Project context:
- C# .NET 8, WPF, MVVM (CommunityToolkit.Mvvm)
- DI: Microsoft.Extensions.DependencyInjection
- Win32 API via P/Invoke
- UWP via Windows.Management.Deployment
- Serilog, ModernWpfUI, xUnit + Moq
- requireAdministrator in manifest

Requirements:
- Every service has an interface (ISP)
- async/await for I/O operations
- ObservableProperty, RelayCommand
- Structured logging for every operation
- Backup before every destructive operation
```

---

## Agent 2: WPF UI Developer — User Interface

**Role:** Builds all XAML views, styles, themes, converters.

**Responsibilities:**
- MainWindow — application list + tools sidebar
- ScanResultsWindow — trace checkboxes
- Batch Uninstall Progress — progress bar per-app
- Startup Manager / Junk Cleaner / Scheduler views
- Dark/Light theme (ModernWpfUI Mica)
- Converters (BoolToVisibility, SizeToString, etc.)

**Delegate task prompt:**
```
Act as a WPF UI Developer for the "BRUTAL Uninstaller" project.

Task: [describe specific view to build]

UI requirements:
- Win11 style: Mica backdrop, rounded corners, dark/light mode (ModernWpfUI)
- All bindings via x:Bind or {Binding}
- ObservableProperty + RelayCommand from CommunityToolkit.Mvvm
- Async progress reporting via IProgress<T>
- ListView/DataGrid with sorting, filtering, virtualization
- StatusBar with app count and size information
- Sidebar navigation (All / Win32 / Store / Recently Installed + tools)

Source: BRUTAL_UNINSTALLER_SPEC.md in the project folder — section 5 (UI Views).
```

**Ready views to build:**

| View | File | Description |
|-------|------|------|
| MainWindow | `Views/MainWindow.xaml` | Application list + sidebar |
| ScanResultsWindow | `Views/ScanResultsWindow.xaml` | Found traces with checkboxes |
| BatchProgressView | `Views/BatchProgressView.xaml` | Progress bar per-app |
| StartupView | `Views/StartupView.xaml` | Startup manager |
| JunkCleanerView | `Views/JunkCleanerView.xaml` | Junk cleaning |
| SettingsView | `Views/SettingsView.xaml` | Settings |
| SchedulerView | `Views/SchedulerView.xaml` | Scan schedule |

---

## Agent 3: MVVM Core Developer — ViewModels and Logic

**Role:** Builds ViewModels and connects them to services via DI.

**Responsibilities:**
- MainViewModel — application list, filtering, batch uninstall
- ScanResultsViewModel — trace presentation, checkboxes
- StartupViewModel — startup entries list
- JunkCleanerViewModel — scan + cleaning
- SettingsViewModel — configuration
- Navigation between views
- ObservableProperty, RelayCommand, IProgress

**Delegate task prompt:**
```
Act as an MVVM Core Developer C#/.NET for the "BRUTAL Uninstaller" project.

Task: [describe specific ViewModel to build]

Requirements:
- CommunityToolkit.Mvvm: [ObservableProperty], [RelayCommand]
- DI service injection via constructor
- async Task for asynchronous commands
- IProgress<T> / INotifyValueChanged for progress reporting
- ObservableCollection<T> for lists
- FilteredCollectionView for sort/filter
- IDisposable pattern for event subscriptions

Naming conventions:
- MainViewModel, ScanResultsViewModel, StartupViewModel
- All in namespace BrutalUninstaller.App.ViewModels
- XAML: DataContext = {Binding Source={StaticResource ViewModelLocator}}
```

---

## Agent 4: Infrastructure Developer — Win32 / P/Invoke / Native API

**Role:** Implements all low-level Windows API wrappers.

**Responsibilities:**
- RegistryHelper — read/write registry keys (4 hives)
- FileSystemHelper — file and folder operations
- ProcessHelper — process startup, kill, wait
- MsiApi — MsiConfigureProduct, MsiInstallProduct (msi.dll)
- UwpApi — PackageManager, RemovePackageAsync
- SystemRestore — SRSetRestorePointW (srclient.dll)
- FirewallHelper — INetFwPolicy2 (COM)
- ScheduledTasksHelper — SCHTASKS / ITaskScheduler

**Delegate task prompt:**
```
Act as an Infrastructure Developer C#/.NET specializing in Windows Native API.

Task: [describe specific wrapper]

Requirements:
- P/Invoke via CsWin32 (Microsoft.Windows.CsWin32) or manual DllImport
- Safe handle management (SafeHandle)
- Structured logging for every call
- Error handling with Marshal.GetLastWin32Error
- async Task for I/O operations
- Testability via interfaces (IMsiApi, IRegistryHelper, etc.)

For registry:
- RegistryView.Registry64 / Registry32 for Wow6432Node
- RegistryHive for HKLM, HKCU, HKU
- Use Microsoft.Win32.RegistryKey instead of P/Invoke where possible

For MSI:
- msi.dll: MsiConfigureProduct, MsiInstallProduct, MsiOpenProduct, MsiEnumProducts
- DllImport with CharSet = CharSet.Unicode

For UWP:
- Windows.Management.Deployment.PackageManager
- Windows.Foundation.IAsyncOperation -> Task via AsTask()
```

---

## Agent 5: Core Services Developer — Business Logic

**Role:** Implements the main application logic — detection, uninstallation, scanning.

**Responsibilities:**
- **AppDiscoveryService** — enumerate applications from registry (4 hives) + MSI + UWP + deduplication
- **UninstallEngine** — run UninstallString, MSI, UWP, exit code + kill process
- **ScanEngine** — RegistryScanner, FileSystemScanner, ServiceScanner, TaskScanner, FirewallScanner
- **BackupService** — Restore Point, Registry Export, Snapshot
- **StartupManager** — list/toggle/remove/add startup entries (8 locations)
- **JunkCleaner** — Temp, Prefetch, Cache, Logs, Dumps
- **SchedulerService** — BackgroundService, timer, orphan scan, notifications
- **ExportService** — CSV/JSON/HTML reports

**Delegate task prompt:**
```
Act as a Core Services Developer C#/.NET for the "BRUTAL Uninstaller" project.

Task: [describe specific service to build]

Context:
- All services via interface (IAppDiscoveryService, etc.)
- Registered in DI as Singleton
- async/await, IProgress<T>, CancellationToken
- Structured logging (Serilog)
- Backup before every modification

For AppDiscoveryService:
- Read HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall (x64)
- Read HKLM\SOFTWARE\WOW6432Node\...\Uninstall (x86)
- Read HKCU\SOFTWARE\...\Uninstall (user)
- Read HKCU\SOFTWARE\WOW6432Node\...\Uninstall (user x86)
- MSI: MsiEnumProducts + MsiGetProductInfo
- UWP: PackageManager.FindPackages()
- Merge + deduplication by DisplayName
- Cache with refresh on F5

For ScanEngine:
- RegistryScanner: HKCU, HKLM, HKU\UserSIDs, HKCR\CLSID, TypeLib, Interface, AppID
- FileSystemScanner: ProgramData, AppData, ProgramFiles, CommonProgramFiles, UserProfile\Documents, Start Menu
- ServiceScanner: HKLM\SYSTEM\CurrentControlSet\Services
- TaskScanner: ITaskScheduler / schtasks.exe
- FirewallScanner: INetFwPolicy2 (COM)
- ContextMenuScanner: HKCR\*\shell, HKCR\Directory\shell, HKCR\Folder\shell
- EmptyFolderDetector: recursive with minimum 2 levels depth

For UninstallEngine:
- UninstallString: Process.Start + WaitForExit + exit code
- MSI: MsiConfigureProduct(installLevel, INSTALLSTATE_ABSENT)
- UWP: PackageManager.RemovePackageAsync()
- Force kill: ProcessHelper.KillProcessTree()
```

---

## Agent 6: Test Engineer — Unit and Integration Tests

**Role:** Covers code with xUnit + Moq tests, minimum 80% coverage.

**Responsibilities:**
- Unit tests for every service
- Mocking IRegistryHelper, IFileSystemHelper, IProcessHelper
- Integration tests with real registry (in container or VM)
- Edge case tests: empty lists, API errors, timeouts
- Mocking Win32 API

**Delegate task prompt:**
```
Act as a Test Engineer C#/.NET for the "BRUTAL Uninstaller" project.

Task: Write tests for [service/class name].

Stack: xUnit + Moq + FluentAssertions

Requirements:
- Arrange-Act-Assert
- Mock interfaces (not concrete implementations)
- Tests for: success, empty result, error, timeout, CancellationToken
- Theory + InlineData for parametrization
- Fixture/ClassFixture for shared setup
- Async tests: Task<ActionResult>
- Coverage: minimum 80% code lines
- Naming: MethodName_StateUnderTest_ExpectedBehavior
```

---

## Agent 7: Security & Code Reviewer — Code Review

**Role:** Reviews code for security, quality, and specification compliance.

**Responsibilities:**
- Checking P/Invoke DllImport (SafeHandle, Marshal)
- Checking registry (permission scope, RegistryView)
- Checking logging (no data leaks)
- Checking backup (every operation has backup)
- Code style, MVVM pattern compliance
- Checking Dispose pattern, IDisposable

**Delegate task prompt:**
```
Conduct a code review and security review for C#/.NET code of the "BRUTAL Uninstaller" project.

Task: Review [file paths/names]

Check:
- P/Invoke: SafeHandle, Marshal, DllImport with CharSet.Unicode
- Registry: RegistryView.Registry64/Registry32, try/catch on UnauthorizedAccessException
- Process: timeouts, kill tree, exit code validation
- MSI: MsiCloseHandle after every MsiOpen*
- WARNING: Application runs as Administrator — check for no RBAC bypass
- Logging: no user data in logs
- MVVM: no code-behind logic in View (ViewModel only)
- Backup: every modification has backup before it
- Race conditions: async void, fire-and-forget without exception handling

Mark each issue as CRITICAL / HIGH / MEDIUM / LOW.
```

---

## Agent 8: Installer & DevOps — Packaging and Distribution

**Role:** Builds WiX installer, configures CI/CD, prepares releases.

**Responsibilities:**
- WiX Toolset — MSI installer
- Manifest: requireAdministrator
- MSIX packaging for Windows Store (optional)
- GitHub Actions CI/CD
- GitHub Release with assets
- Versioning (SemVer)

**Delegate task prompt:**
```
Act as a DevOps/Installer Engineer for the "BRUTAL Uninstaller" C#/.NET 8 WPF project.

Task: [describe specific task]

Requirements:
- WiX Toolset v4 or v5
- MSI with requireAdministrator in manifest
- Portable copy (xcopy deploy) in addition to MSI
- GitHub Actions: build + test + pack + release
- SemVer: major.minor.patch with git tags
- Code signing cert (optional, future)
```

---

## Workflow: Phases & Agent Assignments

```
Phase 1 — MVP (Core Uninstall)
├── Agent 1 (Architect):  Solution structure, DI, logging, config
├── Agent 2 (UI):         MainWindow — application list
├── Agent 3 (MVVM):       MainViewModel
├── Agent 4 (Infra):      RegistryHelper, ProcessHelper, MsiApi, UwpApi
├── Agent 5 (Core):       AppDiscoveryService, UninstallEngine, ScanEngine, BackupService
├── Agent 6 (Test):       Tests for each of the above
└── Agent 7 (Review):     Code review MVP

Phase 2 — Extensions
├── Agent 5 (Core):       Force Uninstall, Batch Uninstall
├── Agent 2 (UI):         BatchProgressView, StartupView, JunkCleanerView
├── Agent 3 (MVVM):       StartupViewModel, JunkCleanerViewModel
├── Agent 5 (Core):       StartupManager, JunkCleaner
├── Agent 6 (Test):       Phase 2 Tests
└── Agent 7 (Review):     Phase 2 Code Review

Phase 3 — New Features (better than Revo)
├── Agent 5 (Core):       SchedulerService, ExportService
├── Agent 2 (UI):         SchedulerView, SettingsView
├── Agent 3 (MVVM):       SettingsViewModel + SchedulerViewModel
├── Agent 5 (Core):       Reports (CSV/JSON/HTML)
├── Agent 2 (UI):         Dark/Light mode + Mica theme
├── Agent 8 (DevOps):     WiX installer + GitHub Actions
├── Agent 6 (Test):       Phase 3 Tests
└── Agent 7 (Review):     Final code review + security audit
```

---

## Delegation Rules

1. **Always respect the order** — Phase 1 (MVP) first, then Phase 2, then Phase 3
2. **Parallel batch** — max 3 subagents at once
3. **Infrastructure first** — before Core Service, RegistryHelper/ProcessHelper must be ready
4. **Then service** — before ViewModel, the service must be ready
5. **Then UI** — before View, the ViewModel must be ready
6. **Tests alongside code** — TDD: RED (test) -> GREEN (code) -> REFACTOR
7. **Review at the end of each phase** — before moving on

---

## Quality Gates

- [ ] Build passes: `dotnet build --configuration Release`
- [ ] Tests pass: `dotnet test --configuration Release`
- [ ] Code review: no CRITICAL/HIGH
- [ ] Security review: no CRITICAL/HIGH
- [ ] MVP coverage: 80%+ lines
- [ ] Backup works before every operation
- [ ] Logging of every operation (Serilog)
- [ ] Dark/Light mode works
- [ ] requireAdministrator in manifest
- [ ] WiX installer creates valid MSI
