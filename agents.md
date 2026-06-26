# BRUTAL Uninstaller — Agents

> Plik definiujący agentów, ich role, odpowiedzialności i gotowe prompty do `delegate_task`.
> Projekt: C# / .NET 8 WPF — alternatywa dla Revo Uninstaller.

---

## Stack

| Warstwa | Technologia |
|---------|------------|
| Framework UI | WPF (.NET 8) |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| Logging | Serilog |
| Win32 API | P/Invoke (advapi32, shell32, msi.dll) |
| UWP API | Windows.Management.Deployment |
| Dark/Light | ModernWpfUI (Mica, Acrylic) |
| Testy | xUnit + Moq |
| Instalator | WiX Toolset |
| Repo | GitHub, licencja MIT |

---

## Agent 1: Architect — Architekt rozwiazania

**Rola:** Podejmuje decyzje architektoniczne, projektuje strukture rozwiazania, definiuje interfejsy.

**Odpowiedzialnosci:**
- Projektowanie hierarchii klas i interfejsow
- Decyzje o wzorcach (MVVM, DI, Repository)
- Struktura folderow i namespace'ow
- Definiowanie kontraktow miedzy serwisami

**Delegate task prompt:**
```
Dzialaj jako architekt rozwiazania C#/.NET dla projektu "BRUTAL Uninstaller" — alternatywy Revo Uninstaller.

Zadanie: [opisz konkretne zadanie architektoniczne]

Kontekst projektu:
- C# .NET 8, WPF, MVVM (CommunityToolkit.Mvvm)
- DI: Microsoft.Extensions.DependencyInjection
- Win32 API przez P/Invoke
- UWP przez Windows.Management.Deployment
- Serilog, ModernWpfUI, xUnit + Moq
- requireAdministrator w manifestcie

Wymagania:
- Kazdy serwis ma interfejs (ISP)
- async/await dla operacji I/O
- ObservableProperty, RelayCommand
- Structured logging kazdej operacji
- Backup przed kazda destrukcyjna operacja
```

---

## Agent 2: WPF UI Developer — Interfejs uzytkownika

**Rola:** Buduje wszystkie widoki XAML, style, motywy, konwertery.

**Odpowiedzialnosci:**
- MainWindow — lista aplikacji + sidebar narzedzi
- ScanResultsWindow — checkboxy sledow
- Batch Uninstall Progress — progress bar per-app
- Startup Manager / Junk Cleaner / Scheduler widoki
- Dark/Light theme (ModernWpfUI Mica)
- Konwertery (BoolToVisibility, SizeToString, itp.)

**Delegate task prompt:**
```
Dzialaj jako WPF UI Developer dla projektu "BRUTAL Uninstaller".

Zadanie: [opisz konkretny widok do zbudowania]

Wymagania UI:
- Win11 styl: Mica backdrop, zaokraglone rogi, dark/light mode (ModernWpfUI)
- Wszystkie bindingi przez x:Bind lub {Binding}
- ObservableProperty + RelayCommand z CommunityToolkit.Mvvm
- Async progress reporting przez IProgress<T>
- ListView/DataGrid z sortowaniem, filtrowaniem, wirtualizacja
- StatusBar z informacjami o liczbie aplikacji, rozmiarze
- Sidebar nawigacyjny (All / Win32 / Store / Recently Installed + narzedzia)

Zrodlo: BRUTAL_UNINSTALLER_SPEC.md w folderze projektu — sekcja 5 (Widoki UI).
```

**Gotowe widoki do zbudowania:**

| Widok | Plik | Opis |
|-------|------|------|
| MainWindow | `Views/MainWindow.xaml` | Lista aplikacji + sidebar |
| ScanResultsWindow | `Views/ScanResultsWindow.xaml` | Znalezione slady z checkboxami |
| BatchProgressView | `Views/BatchProgressView.xaml` | Progress bar per-app |
| StartupView | `Views/StartupView.xaml` | Manager startupu |
| JunkCleanerView | `Views/JunkCleanerView.xaml` | Czyszczenie smieci |
| SettingsView | `Views/SettingsView.xaml` | Ustawienia |
| SchedulerView | `Views/SchedulerView.xaml` | Harmonogram skanowania |

---

## Agent 3: MVVM Core Developer — ViewModels i logika

**Rola:** Buduje ViewModele i laczy je z serwisami przez DI.

**Odpowiedzialnosci:**
- MainViewModel — lista aplikacji, filtrowanie, batch uninstall
- ScanResultsViewModel — prezentacja sledow, checkboxy
- StartupViewModel — lista startup entries
- JunkCleanerViewModel — scan + czyszczenie
- SettingsViewModel — konfiguracja
- Nawigacja miedzy widokami
- ObservableProperty, RelayCommand, IProgress

**Delegate task prompt:**
```
Dzialaj jako MVVM Core Developer C#/.NET dla projektu "BRUTAL Uninstaller".

Zadanie: [opisz konkretny ViewModel do zbudowania]

Wymagania:
- CommunityToolkit.Mvvm: [ObservableProperty], [RelayCommand]
- DI wstrzykiwanie serwisow przez konstruktor
- async Task dla komend asynchronicznych
- IProgress<T> / INotifyValueChanged dla progress raportowania
- ObservableCollection<T> dla list
- FilteredCollectionView dla sort/filter
- IDisposable pattern dla subskrypcji eventow

Konwencje nazewnicze:
- MainViewModel, ScanResultsViewModel, StartupViewModel
- Wszystkie w namespace BrutalUninstaller.App.ViewModels
- XAML: DataContext = {Binding Source={StaticResource ViewModelLocator}}
```

---

## Agent 4: Infrastructure Developer — Win32 / P/Invoke / Native API

**Rola:** Implementuje wszystkie niskopoziomowe wrappery na Windows API.

**Odpowiedzialnosci:**
- RegistryHelper — odczyt/zapis kluczy rejestru (4 galęzie)
- FileSystemHelper — operacje na plikach i folderach
- ProcessHelper — uruchamianie procesow, kill, wait
- MsiApi — MsiConfigureProduct, MsiInstallProduct (msi.dll)
- UwpApi — PackageManager, RemovePackageAsync
- SystemRestore — SRSetRestorePointW (srclient.dll)
- FirewallHelper — INetFwPolicy2 (COM)
- ScheduledTasksHelper — SCHTASKS / ITaskScheduler

**Delegate task prompt:**
```
Dzialaj jako Infrastructure Developer C#/.NET specjalizujacy sie w Windows Native API.

Zadanie: [opisz konkretny wrapper]

Wymagania:
- P/Invoke przez CsWin32 (Microsoft.Windows.CsWin32) lub reczne DllImport
- Bezpieczna obsluga uchwytow (SafeHandle)
- Structured logging kazdego wywolania
- Obsluga bledow z Marshal.GetLastWin32Error
- async Task dla operacji I/O
- Testowalnosc przez interfejsy (IMsiApi, IRegistryHelper itp.)

Dla rejestru:
- RegistryView.Registry64 / Registry32 dla Wow6432Node
- RegistryHive dla HKLM, HKCU, HKU
- Uzyj Microsoft.Win32.RegistryKey zamiast P/Invoke gdzie mozna

Dla MSI:
- msi.dll: MsiConfigureProduct, MsiInstallProduct, MsiOpenProduct, MsiEnumProducts
- DllImport z CharSet = CharSet.Unicode

Dla UWP:
- Windows.Management.Deployment.PackageManager
- Windows.Foundation.IAsyncOperation -> Task przez AsTask()
```

---

## Agent 5: Core Services Developer — Logika biznesowa

**Rola:** Implementuje glowna logike aplikacji — wykrywanie, deinstalacje, skanowanie.

**Odpowiedzialnosci:**
- **AppDiscoveryService** — enumeracja aplikacji z rejestru (4 galęzie) + MSI + UWP + deduplikacja
- **UninstallEngine** — uruchamianie UninstallString, MSI, UWP, exit code + kill process
- **ScanEngine** — RegistryScanner, FileSystemScanner, ServiceScanner, TaskScanner, FirewallScanner
- **BackupService** — Restore Point, Registry Export, Snapshot
- **StartupManager** — lista/toggle/remove/add startup entries (8 lokalizacji)
- **JunkCleaner** — Temp, Prefetch, Cache, Logs, Dumps
- **SchedulerService** — BackgroundService, timer, orphan scan, notifications
- **ExportService** — CSV/JSON/HTML raporty

**Delegate task prompt:**
```
Dzialaj jako Core Services Developer C#/.NET dla projektu "BRUTAL Uninstaller".

Zadanie: [opisz konkretny serwis do zbudowania]

Kontekst:
- Wszystkie serwisy przez interfejs (IAppDiscoveryService, itp.)
- Rejestracja w DI jako Singleton
- async/await, IProgress<T>, CancellationToken
- Structured logging (Serilog)
- Backup przed kazda modyfikacja

Dla AppDiscoveryService:
- Sczytaj HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall (x64)
- Sczytaj HKLM\SOFTWARE\WOW6432Node\...\Uninstall (x86)
- Sczytaj HKCU\SOFTWARE\...\Uninstall (user)
- Sczytaj HKCU\SOFTWARE\WOW6432Node\...\Uninstall (user x86)
- MSI: MsiEnumProducts + MsiGetProductInfo
- UWP: PackageManager.FindPackages()
- Merge + deduplikacja po DisplayName
- Cache z odswiezeniem na F5

Dla ScanEngine:
- RegistryScanner: HKCU, HKLM, HKU\UserSIDs, HKCR\CLSID, TypeLib, Interface, AppID
- FileSystemScanner: ProgramData, AppData, ProgramFiles, CommonProgramFiles, UserProfile\Documents, Start Menu
- ServiceScanner: HKLM\SYSTEM\CurrentControlSet\Services
- TaskScanner: ITaskScheduler / schtasks.exe
- FirewallScanner: INetFwPolicy2 (COM)
- ContextMenuScanner: HKCR\*\shell, HKCR\Directory\shell, HKCR\Folder\shell
- EmptyFolderDetector: recursive z minimum 2 poziomami glebokosci

Dla UninstallEngine:
- UninstallString: Process.Start + WaitForExit + exit code
- MSI: MsiConfigureProduct(installLevel, INSTALLSTATE_ABSENT)
- UWP: PackageManager.RemovePackageAsync()
- Force kill: ProcessHelper.KillProcessTree()
```

---

## Agent 6: Test Engineer — Testy jednostkowe i integracyjne

**Rola:** Pokrywa kod testami xUnit + Moq, minimum 80% coverage.

**Odpowiedzialnosci:**
- Unit testy dla kazdego serwisu
- Mockowanie IRegistryHelper, IFileSystemHelper, IProcessHelper
- Testy integracyjne z prawdziwym rejestrem (w kontenerze lub VM)
- Testy edge case'ow: puste listy, bledy API, timeouty
- Mockowanie Win32 API

**Delegate task prompt:**
```
Dzialaj jako Test Engineer C#/.NET dla projektu "BRUTAL Uninstaller".

Zadanie: Napisz testy dla [nazwa serwisu/klasy].

Stack: xUnit + Moq + FluentAssertions

Wymagania:
- Arrange-Act-Assert
- Mock interfejsow (nie konkretnych implementacji)
- Testy dla: sukces, pusty wynik, blad, timeout, CancellationToken
- Theory + InlineData dla parametryzacji
- Fixture/ClassFixture dla wspolnego setupu
- Testy async: Task<ActionResult>
- Coverage: minimum 80% linii kodu
- Nazewnictwo: MethodName_StateUnderTest_ExpectedBehavior
```

---

## Agent 7: Security & Code Reviewer — Przeglad kodu

**Rola:** Review kodu pod katem bezpieczenstwa, jakosci i zgodnosci ze specyfikacja.

**Odpowiedzialnosci:**
- Sprawdzanie P/Invoke o DllImport (SafeHandle, Marshal)
- Sprawdzanie rejestru (zakres uprawnien, RegistryView)
- Sprawdzanie logowania (czy nie wyciekaja dane)
- Sprawdzanie backupu (czy kazda operacja ma backup)
- Code style, MVVM pattern compliance
- Sprawdzanie Dispose pattern, IDisposable

**Delegate task prompt:**
```
Przeprowadz code review i security review dla kodu C#/.NET projektu "BRUTAL Uninstaller".

Zadanie: Przejrzyj [sciezki/nazwy plikow]

Sprawdz:
- P/Invoke: SafeHandle, Marshal, DllImport z CharSet.Unicode
- Registry: RegistryView.Registry64/Registry32, try/catch na UnauthorizedAccessException
- Process: timeouty, kill tree, exit code validation
- MSI: MsiCloseHandle po kazdym MsiOpen*
- UWAGA: Aplikacja dziala jako Administrator — sprawdz czy nie ma RBAC bypass
- Logging: brak danych uzytkownika w logach
- MVVM: brak code-behind logiki w View (tylko ViewModel)
- Backup: czy kazda modyfikacja ma backup przed soba
- Race conditions: async void, fire-and-forget bez exception handling

Oznacz kazdy problem jako CRITICAL / HIGH / MEDIUM / LOW.
```

---

## Agent 8: Installer & DevOps — Pakowanie i dystrybucja

**Rola:** Buduje instalator WiX, konfiguruje CI/CD, przygotowuje release.

**Odpowiedzialnosci:**
- WiX Toolset — MSI installer
- Manifest: requireAdministrator
- MSIX packaging dla Windows Store (opcjonalnie)
- GitHub Actions CI/CD
- GitHub Release z assets
- Versioning (SemVer)

**Delegate task prompt:**
```
Dzialaj jako DevOps/Installer Engineer dla projektu "BRUTAL Uninstaller" C#/.NET 8 WPF.

Zadanie: [opisz konkretne zadanie]

Wymagania:
- WiX Toolset v4 lub v5
- MSI z requireAdministrator w manifestcie
- Kopia portable (xcopy deploy) oprocz MSI
- GitHub Actions: build + test + pack + release
- SemVer: major.minor.patch z git tags
- Code signing cert (opcjonalnie, future)
```

---

## Workflow: Phases & Agent Assignments

```
Faza 1 — MVP (Core Uninstall)
├── Agent 1 (Architect):  Struktura solution, DI, logging, config
├── Agent 2 (UI):         MainWindow — lista aplikacji
├── Agent 3 (MVVM):       MainViewModel
├── Agent 4 (Infra):      RegistryHelper, ProcessHelper, MsiApi, UwpApi
├── Agent 5 (Core):       AppDiscoveryService, UninstallEngine, ScanEngine, BackupService
├── Agent 6 (Test):       Testy dla kazdego z powyzszych
└── Agent 7 (Review):     Code review MVP

Faza 2 — Rozszerzenia
├── Agent 5 (Core):       Force Uninstall, Batch Uninstall
├── Agent 2 (UI):         BatchProgressView, StartupView, JunkCleanerView
├── Agent 3 (MVVM):       StartupViewModel, JunkCleanerViewModel
├── Agent 5 (Core):       StartupManager, JunkCleaner
├── Agent 6 (Test):       Testy Fazy 2
└── Agent 7 (Review):     Code review Fazy 2

Faza 3 — Nowe funkcje (lepsze niz Revo)
├── Agent 5 (Core):       SchedulerService, ExportService
├── Agent 2 (UI):         SchedulerView, SettingsView
├── Agent 3 (MVVM):       SettingsViewModel + SchedulerViewModel
├── Agent 5 (Core):       Raporty (CSV/JSON/HTML)
├── Agent 2 (UI):         Dark/Light mode + Mica theme
├── Agent 8 (DevOps):     WiX installer + GitHub Actions
├── Agent 6 (Test):       Testy Fazy 3
└── Agent 7 (Review):     Final code review + security audit
```

---

## Delegation Rules

1. **Zawsze dzieki kolejnosc** — Faza 1 (MVP) pierwsza, potem Faza 2, potem Faza 3
2. **Parallel batch** — max 3 subagenty na raz
3. **Najpierw infrastruktura** — zanim Core Service, musi byc gotowy RegistryHelper/ProcessHelper
4. **Potem serwis** — zanim ViewModel, musi byc gotowy serwis
5. **Potem UI** — zanim Widok, musi byc gotowy ViewModel
6. **Testy razem z kodem** — TDD: RED (test) -> GREEN (kod) -> REFACTOR
7. **Review na koncu kazdej fazy** — zanim przejdziesz dalej

---

## Quality Gates

- [ ] Build przechodzi: `dotnet build --configuration Release`
- [ ] Testy przechodza: `dotnet test --configuration Release`
- [ ] Code review: brak CRITICAL/HIGH
- [ ] Security review: brak CRITICAL/HIGH
- [ ] MVP coverage: 80%+ linii
- [ ] Backup dziala przed kazda operacja
- [ ] Logowanie kazdej operacji (Serilog)
- [ ] Dark/Light mode dziala
- [ ] requireAdministrator w manifestcie
- [ ] WiX installer tworzy poprawny MSI
