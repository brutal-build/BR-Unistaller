# BRUTAL Uninstaller - Specyfikacja Techniczna

## 1. Metadane projektu

| Pole | Wartość |
|------|---------|
| Nazwa | **BRUTAL Uninstaller** |
| Technologia | C# / .NET (WPF) |
| Platforma | Windows 10/11 x64 |
| Uprawnienia | Zawsze Administrator (`requireAdministrator`) |
| UI | Nowoczesny (Win11: Mica, dark/light mode, rounded corners) |
| Licencja | Open Source (MIT) |
| Wzorzec | Alternatywa 1:1 dla Revo Uninstaller (bezpłatna) |

---

## 2. Architektura systemu

```
┌──────────────────────────────────────────────────────┐
│                   BRUTAL Uninstaller                 │
├──────────────────────────────────────────────────────┤
│  UI Layer (WPF)                                      │
│  ┌─────────────┐ ┌─────────────┐ ┌────────────────┐  │
│  │  MainWindow  │ │  HunterBar  │ │  Settings     │  │
│  │  (lista app) │ │  (overlay)  │ │               │  │
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

### 2.1 Stack technologiczny

| Komponent | Technologia | Uzasadnienie |
|-----------|-------------|-------------|
| Framework UI | **WPF (.NET 8)** | Natywny Windows, MVVM, wydajny DataGrid |
| MVVM | **CommunityToolkit.Mvvm** | `[ObservableProperty]`, `[RelayCommand]` |
| DI Container | **Microsoft.Extensions.DependencyInjection** | Wbudowany, lekki |
| Logowanie | **Serilog** | Structured logging |
| Win32 API | **P/Invoke** (advapi32, shell32, msi.dll) | Dostęp do instalatora, rejestru |
| UWP API | **Windows.Management.Deployment** | Zarządzanie Store apps |
| Dark Mode | **WPF-UI / ModernWpf** | Mica, Acrylic, Win11 themes |
| Konfiguracja | **appsettings.json** + JSON serializer | Przenośność ustawień |
| Instalator | **WiX Toolset** / **MSIX** | MSI dla dystrybucji |
| Testy | **xUnit + Moq** | Unit + integration tests |

### 2.2 Struktura solution

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
│   ├── BrutalUninstaller.Core/         # Logika biznesowa
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

## 3. Funkcjonalności (Core Features)

### 3.1 Lista aplikacji (App Discovery)

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

**Źródła odkrywania aplikacji:**

| Źródło | API | Opis |
|--------|-----|------|
| HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall | Registry (x64) | Aplikacje systemowe |
| HKLM\SOFTWARE\WOW6432Node\...\Uninstall | Registry (x86) | Aplikacje 32-bit |
| HKCU\SOFTWARE\...\Uninstall | Registry (user) | Aplikacje per-user |
| HKCU\SOFTWARE\WOW6432Node\...\Uninstall | Registry (user x86) | Aplikacje per-user 32-bit |
| Windows.Management.Deployment.PackageManager | UWP API | Store / Modern apps |
| HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products | MSI DB | MSI produkty |
| Steam / Epic / GOG | (opcjonalnie - future) | Aplikacje z launcherów |

### 3.2 Deinstalacja z pełnym skanowaniem resztek

**Flow deinstalacji:**

```
1. Wybór aplikacji (single / batch)
   │
   ▼
2. Backup (przed usunięciem)
   ├── System Restore Point  ──  SystemRestore.CreateRestorePoint()
   ├── Registry Export        ──  regedit /e {app}_backup.reg
   └── Log stanu              ──  zapis paths przed usunięciem
   │
   ▼
3. Normalna deinstalacja
   ├── Uruchom UninstallString (cicho lub standardowo)
   ├── MSI: MsiConfigureProduct / MsiInstallProduct
   ├── UWP: PackageManager.RemovePackageAsync()
   └── Sprawdź exit code / pozostałe procesy
   │
   ▼
4. Skanowanie resztek (Post-Uninstall Scan)
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
   └── Inne obiekty ───
       ├── Services   ──  HKLM\SYSTEM\CurrentControlSet\Services\{AppService}
       ├── Drivers    ──  HKLM\SYSTEM\CurrentControlSet\Services\{AppDriver}
       ├── Planned Tasks ──  C:\Windows\System32\Tasks\{AppTask}
       ├── Firewall Rules ──  COM: INetFwPolicy2
       └── Context Menu   ──  HKCR\*\shell\{App}, HKCR\Directory\shell
   │
   ▼
5. Prezentacja znalezionych śladów (zaznaczanie / odznaczanie)
   │
   ▼
6. Usunięcie wybranych śladów (z potwierdzeniem)
   │
   ▼
7. Raport (log + eksport)
```

### 3.3 Force Uninstall

Używane gdy aplikacja nie ma właściwego deinstalatora lub standardowy flow zawiódł.

```
1. Analiza resztek aplikacji (tylko registry + filesystem - bez uruchamiania deinstalatora)
2. Wykrycie wszystkich kluczy, plików, serwisów, zadań
3. Full scan + usunięcie wszystkiego związanego z nazwą/wydawcą
4. Opcjonalnie: deep scan (full disk search) dla pozostałości
```

### 3.4 Batch Uninstall (Nowa funkcja vs Revo)

- Zaznaczanie wielu aplikacji jednocześnie (checkboxy + multi-select)
- Sekwencyjna deinstalacja (jedna po drugiej)
- Pool uninstall (max N równolegle, gdzie N konfigurowalne)
- Progress bar per-app + global progress
- Resume after failure (skip lub stop)

### 3.5 Manager Startupu

| Kategoria | Ścieżka |
|-----------|---------|
| Rejestr (HKLM) | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` |
| Rejestr (HKCU) | `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` |
| Rejestr (x64/x86) | jak wyżej + WOW6432Node |
| Folder Startup | `%AppData%\Microsoft\Windows\Start Menu\Programs\Startup` |
| Folder Startup (All) | `%ProgramData%\Microsoft\Windows\Start Menu\Programs\Startup` |
| Scheduled Tasks | `C:\Windows\System32\Tasks\` + SCHTASKS API |
| Services | HKLM\SYSTEM\CurrentControlSet\Services\ (Start=2 = auto) |

Funkcje:
- Lista wpisów z możliwością włączania/wyłączania
- Usuwanie wpisów
- Dodawanie własnych (opcjonalnie)
- Opóźnienie startu (Delay launch)

### 3.6 Backup przed usunięciem

- **System Restore Point** — `SRSetRestorePointW` (srclient.dll)
- **Registry Export** — całe gałęzie do `.reg` przed modyfikacją
- **File list snapshot** — zapis ścieżek przed usunięciem

### 3.7 Czyszczenie Junk Files

```
Skanowane lokalizacje:
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

- Używane API: `Windows.Management.Deployment.PackageManager`
- Lista wszystkich zainstalowanych paczek UWP/Store
- Deinstalacja przez `RemovePackageAsync` lub przez `RemovePackageWithUserAsync`
- Skanowanie resztek dla Store apps (AppData\Local\Packages\{PackageFamilyName}, etc.)

### 3.9 Skan w tle (Scheduler) — Nowa funkcja vs Revo

- Timer w tle (BackgroundService / Timer)
- Co N dni automatycznie uruchamia skan śladów po nieistniejących aplikacjach
- Minimalny impact (niski priorytet I/O, throttling)
- Powiadomienia po wykryciu orphaned traces
- Konfigurowalny harmonogram (co 1/7/14/30 dni)

### 3.10 Eksport raportów — Nowa funkcja vs Revo

| Format | Zawartość |
|--------|-----------|
| **CSV** | Lista aplikacji, usunięte ślady, timestamp |
| **JSON** | Full structured data (machine-readable) |
| **HTML** | Sformatowany raport z kolorami, do przeglądarki |
| **TXT / Log** | Plain text z timestampami |

---

## 4. Modele danych

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

## 5. Widoki UI (WPF)

### 5.1 MainWindow — Lista aplikacji

```
+--------------------------------------------------+
|  BRUTAL Uninstaller         [ Szukaj...        ]  |
+----------------------+---------------------------+
|  [Batch Uninstall]   |                           |
|  [Force Uninstall]   |  Ikona | Nazwa | Wydawca  |
|                      |  ------+-------+----------|
|  ------------------  |   PC   | App1  | PubCo    |
|  > All Applications   |   PC   | App2  | CorpX    |
|  > Win32              |   PKG  | App3  | Store    |
|  > Store Apps         |                           |
|  > Recently Installed |  [Szczegoly]  [Usun]      |
|  ------------------  |                           |
|  Narzedzia            |  Rozmiar: 250 MB          |
|  > Startup Manager    |  Data: 2026-01-15         |
|  > Junk Cleaner       |  Typ: MSI                 |
|  > Scheduler          |  Wersja: 2.3.1            |
|  > Settings           |                           |
|  > About              |  [Raporty]                |
+----------------------+---------------------------+
|  Status: 42 apps found | 2.1 GB total | Ready    |
+--------------------------------------------------+
```

### 5.2 Scan Results Window

```
+--------------------------------------------------+
|  Znalezione slady po: AppName                    |
+--------------------------------------------------+
|  Rejestr (13 znalezionych)                       |
|    [X] HKLM\SOFTWARE\Publisher\AppName           |
|    [X] HKLM\SOFTWARE\Classes\CLSID\{...}         |
|    [ ] HKCU\SOFTWARE\Publisher\AppName           |
|                                                  |
|  Pliki (8 znalezionych)                          |
|    [X] C:\Program Files\AppName\                 |
|    [X] C:\ProgramData\AppName\                   |
|    [X] C:\Users\...\AppData\Roaming\AppName      |
|                                                  |
|  Skroty / Inne (3 znalezionych)                  |
|    [X] Start Menu\AppName.lnk                    |
|    [ ] Service: AppService                       |
|                                                  |
|        [Zaznacz wszystkie]  [Odznacz]            |
|        [Usun zaznaczone]    [Anuluj]             |
+--------------------------------------------------+
```

### 5.3 Batch Uninstall Progress

```
+--------------------------------------------------+
|  Batch Uninstall (5 aplikacji)                   |
+--------------------------------------------------+
|  AppName 1  [####################]  100%  Gotowe   |
|  AppName 2  [####################]  100%  Gotowe   |
|  AppName 3  [##############......]   73%  Skanuje  |
|  AppName 4  [....................]    0%  Oczekuje |
|  AppName 5  [....................]    0%  Oczekuje |
|                                                  |
|  Calkowity postep: [############....]  54%       |
|                                                  |
|        [Wstrzymaj]  [Pomin]  [Anuluj]            |
+--------------------------------------------------+
```

---

## 6. Komponenty kluczowe (klasy)

### 6.1 AppDiscoveryService

```csharp
public class AppDiscoveryService : IAppDiscoveryService
{
    // Otwiera klucze rejestru (4 gałęzie) + MSI + UWP
    // Merge wyniki, usuwa duplikaty
    // Cache wyniki (refresh przy F5)
    // Async enumeracja (IAsyncEnumerable)
}
```

### 6.2 UninstallEngine

```csharp
public class UninstallEngine : IUninstallEngine
{
    // Wykonuje uninstall przez:
    //   - UninstallString (Process.Start + wait)
    //   - MsiConfigureProduct (msi.dll)
    //   - PackageManager.RemovePackageAsync (UWP)
    // Obsługuje exit codes
    // Kill pozostałych procesów (opcjonalnie)
    // Callback progress do UI
}
```

### 6.3 ScanEngine

```csharp
public class ScanEngine : IScanEngine
{
    // RegistryScanner - przeszukuje HKCU, HKLM, HKU\UserSIDs,
    //   HKCR\CLSID, TypeLib, Interface, AppID
    // FileSystemScanner - enumeruje powszechne ścieżki
    // ServiceScanner - HKLM\SYSTEM\CurrentControlSet\Services
    // TaskScanner - Scheduled tasks
    // FirewallScanner - COM: INetFwPolicy2
    // ContextMenuScanner - HKCR\*\shell, HKCR\Directory\shell,
    //   HKCR\Folder\shell
    // EmptyFolderDetector - znajduje puste foldery po usunięciu
}
```

### 6.4 BackupService

```csharp
public class BackupService : IBackupService
{
    // CreateRestorePoint(name) → SRSetRestorePointW
    // ExportRegistryKey(path) → RegExport
    // SnapshotCurrentState(appId) → zapisuje paths
}
```

### 6.5 StartupManager

```csharp
public class StartupManager : IStartupManager
{
    // GetStartupEntries() → lista z 7 lokalizacji
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
    // CalculateSavings() → szacowany rozmiar
}
```

### 6.7 SchedulerService

```csharp
public class SchedulerService : ISchedulerService
{
    // Timer co N dni
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

## 7. Interfejsy (Contracts)

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

## 8. Rejestracja DI (Program.cs / App.xaml.cs)

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

## 9. Harmonogram implementacji

### Faza 1 — MVP (Core Uninstall)

| Krok | Opis | Czas szac. |
|------|------|-----------|
| 1.1 | Struktura solution, DI, logging, config | 2 dni |
| 1.2 | AppDiscoveryService — enumeracja z rejestru + UWP | 3 dni |
| 1.3 | MainWindow + MainViewModel — lista aplikacji | 2 dni |
| 1.4 | UninstallEngine — MSI + EXE + UWP | 3 dni |
| 1.5 | ScanEngine — rejestr + pliki po deinstalacji | 4 dni |
| 1.6 | Scan Results Window — UI + ViewModel | 2 dni |
| 1.7 | BackupService — Restore Point + Registry Export | 2 dni |
| 1.8 | Testy integracyjne MVP | 2 dni |

### Faza 2 — Rozszerzenia

| Krok | Opis | Czas szac. |
|------|------|-----------|
| 2.1 | Force Uninstall (bez deinstalatora) | 3 dni |
| 2.2 | Batch Uninstall + UI progresu | 3 dni |
| 2.3 | Startup Manager | 2 dni |
| 2.4 | Junk Cleaner | 2 dni |

### Faza 3 — Nowe funkcje (lepsze niż Revo)

| Krok | Opis | Czas szac. |
|------|------|-----------|
| 3.1 | Scheduler — skan w tle + powiadomienia | 3 dni |
| 3.2 | Export Service — CSV/JSON/HTML raporty | 2 dni |
| 3.3 | Dark/Light mode, Mica theme | 2 dni |
| 3.4 | Testy, dokumentacja, WiX installer | 3 dni |

**Łącznie szacowany czas: ~38 dni (full-time dev)**

---

## 10. Nowe funkcje vs Revo Uninstaller (podsumowanie)

| Funkcja | Revo (Free) | Revo (Pro) | BRUTAL |
|---------|-------------|------------|--------|
| Lista aplikacji (Win32 + UWP) | ✅ | ✅ | ✅ |
| Normalny uninstall | ✅ | ✅ | ✅ |
| Skan resztek (rejestr + pliki) | ✅ | ✅ | ✅ |
| Force uninstall | ❌ | ✅ | ✅ |
| Backup (Restore Point) | ❌ | ✅ | ✅ |
| Startup Manager | ✅ (Tools) | ✅ | ✅ |
| Junk Cleaner | ✅ (Tools) | ✅ | ✅ |
| Windows Store Apps | ❌ | ✅ | ✅ |
| **Batch Uninstall** | ❌ | ❌ | ✅ 🆕 |
| **Scheduler (skan w tle)** | ❌ | ❌ | ✅ 🆕 |
| **Eksport raportów (CSV/JSON/HTML)** | ❌ | ❌ | ✅ 🆕 |
| **Darmowy (MIT licencja)** | ✅ (częściowo) | ❌ ($24.95+) | ✅ |

---

## 11. Wymagane pakiety NuGet

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.*" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.*" />
<PackageReference Include="Serilog.Sinks.File" Version="5.*" />
<PackageReference Include="Serilog.Sinks.Async" Version="1.*" />
<PackageReference Include="System.Management" Version="8.*" />           <!-- WMI -->
<PackageReference Include="Microsoft.Windows.CsWin32" Version="0.*" /> <!-- P/Invoke gen -->
<PackageReference Include="ModernWpfUI" Version="3.*" />                <!-- WinUI 3 style dla WPF -->
<PackageReference Include="CsvHelper" Version="31.*" />                 <!-- Eksport CSV -->
<PackageReference Include="xUnit" Version="2.*" />                      <!-- Testy -->
<PackageReference Include="Moq" Version="4.*" />                        <!-- Mocking -->
```

---

## 12. Uwagi końcowe

- Program będzie wymagał **praw administratora** — manifest z `requireAdministrator`.
- Aplikacja **przenośna lub instalowana** (WiX installer jako opcja).
- Wszystkie operacje na rejestrze i plikach są logowane dla bezpieczeństwa.
- Przed każdą destrukcyjną operacją tworzony jest backup (System Restore Point).
- UI inspirowany Windows 11 — Mica backdrop, zaokrąglone rogi, ciemny/jasny motyw.
- Kod w pełni otwarty (MIT) na GitHub.
