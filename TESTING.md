# BRUTAL Uninstaller — Instrukcja testowania

## Wymagania wstępne

- Windows 10/11 x64
- .NET 8 Runtime (zainstaluje się z VS lub osobno)
- Uruchom jako **Administrator** (manifest wymusza)

## Co testować

### 1. Podstawowy flow

| Krok | Co zrobić | Oczekiwany rezultat |
|------|-----------|---------------------|
| 1.1 | Otwórz aplikację | Okno z ciemnym motywem, sidebar, lista aplikacji |
| 1.2 | Poczekaj na załadowanie | Lista aplikacji z ikoną, nazwą, wydawcą, rozmiarem, datą, typem |
| 1.3 | StatusBar | Powinien pokazywać "Found N applications" + rozmiar |
| 1.4 | Kliknij aplikację | Panel detali pokazuje rozmiar, datę, typ, wersję |

### 2. Uninstall

| Krok | Co zrobić | Oczekiwany rezultat |
|------|-----------|---------------------|
| 2.1 | Wybierz aplikację → [ Uninstall ] | Flow: backup → uninstall → skan śladów → raport |
| 2.2 | Nieudany uninstall | Status pokazuje "Uninstall failed (exit code: N)" |
| 2.3 | Udany uninstall | Aplikacja znika z listy |

### 3. Skanowanie śladów (Scan)

| Krok | Co zrobić | Oczekiwany rezultat |
|------|-----------|---------------------|
| 3.1 | Wybierz aplikację → [ Scan ] | Otwiera ScanResultsWindow z podziałem na kategorie |
| 3.2 | Kategorie | Registry, Files, Shortcuts/Other — każde z checkboxami |
| 3.3 | [ Select All ] / [ Deselect All ] | Wszystkie checkboxy się zaznaczają/odznaczają |
| 3.4 | Zaznacz część → [ Delete Selected ] | Zaznaczone ślady usunięte, kategoria aktualizuje licznik |

### 4. Force Uninstall

| Krok | Co zrobić | Oczekiwany rezultat |
|------|-----------|---------------------|
| 4.1 | Wybierz aplikację → [Force Uninstall] | Zabija procesy → deep scan → okno z wynikami |
| 4.2 | Force Uninstall bez deinstalatora | Tylko skan rejestru + plików + serwisów |
| 4.3 | Aplikacja bez procesów | Pomija krok kill, od razu skan |

### 5. Batch Uninstall

| Krok | Co zrobić | Oczekiwany rezultat |
|------|-----------|---------------------|
| 5.1 | Kliknij [Batch Uninstall] | Otwiera BatchProgressView z listą aplikacji |
| 5.2 | Progress bar | Każda aplikacja ma progress bar 0→100% |
| 5.3 | Po zakończeniu | "Done" / "Failed" dla każdej, licznik completed/total |

### 6. Startup Manager

| Krok | Co zrobić | Oczekiwany rezultat |
|------|-----------|---------------------|
| 6.1 | Sidebar → [ > Startup Manager ] | Okno z listą wpisów startup |
| 6.2 | Lista pokazuje | Nazwa, Command, Source (Registry/Folder) |
| 6.3 | [ Refresh ] | Odświeża listę |
| 6.4 | Toggle checkbox | Włącza/wyłącza wpis |

### 7. Junk Cleaner

| Krok | Co zrobić | Oczekiwany rezultat |
|------|-----------|---------------------|
| 7.1 | Sidebar → [ > Junk Cleaner ] | Okno z przyciskami Scan / Clean |
| 7.2 | [ Scan ] | Skanuje Temp, Prefetch, Logs, Dumps |
| 7.3 | Lista pokazuje | Path, Category, Size — z checkboxami |
| 7.4 | Zaznacz → [ Clean Selected ] | Usuwa zaznaczone śmieci |

### 8. Search

| Krok | Co zrobić | Oczekiwany rezultat |
|------|-----------|---------------------|
| 8.1 | Wpisz nazwę w Search | Lista filtrów w czasie rzeczywistym |
| 8.2 | Wyczyść search | Lista wraca do pełnej |

### 9. Kategorie sidebar

| Krok | Co zrobić | Oczekiwany rezultat |
|------|-----------|---------------------|
| 9.1 | Kliknij "Win32" | Tylko aplikacje Win32 |
| 9.2 | Kliknij "Store Apps" | Tylko UWP/Store aplikacje |
| 9.3 | "All Applications" | Wszystkie aplikacje |

### 10. Edge case'y

| Scenariusz | Oczekiwane |
|-----------|-----------|
| Pusta lista aplikacji | Status: "Found 0 applications" |
| Dużo aplikacji (200+) | Lista wirtualizowana, scroll bez lagów |
| Aplikacja bez UninstallString | Force Uninstall tylko |
| Registry bez dostępu | Log warning, nie crash |
| Skanowanie z CancellationToken | Można przerwać |

## Jak uruchomić

```bash
cd "/c/Users/user/Desktop/_projects/Brutal unistaller"
dotnet build --configuration Release
dotnet run --project src/BrutalUninstaller.App
```

## Testy automatyczne

```bash
cd "/c/Users/user/Desktop/_projects/Brutal unistaller"
dotnet test --configuration Release -v normal
```

Oczekiwane: **12/12 passed**

| Test | Co sprawdza |
|------|------------|
| UninstallEngineTests.ForceKillProcessAsync_WithValidName_KillsProcess | Kill procesu działa |
| UninstallEngineTests.ForceKillProcessAsync_WithEmptyName_ReturnsFalse | Empty string nie crashuje |
| UninstallEngineTests.UninstallAsync_Win32App_CallsProcessHelper | Uninstall flow dla Win32 |
| UninstallEngineTests.UninstallAsync_WithProgress_ReportsProgress | Progress reporting |
| ScanEngineTests.ScanForTracesAsync_WithValidApp_ReturnsResults | Skanowanie daje wyniki |
| ScanEngineTests.DeleteSelectedTracesAsync_WithEmptyList_ReturnsTrue | Pusta lista nie crashuje |
| ScanEngineTests.DeleteSelectedTracesAsync_WithSelectedRegistryTraces_DeletesKeys | Usuwanie po rejestrze |
| ScanEngineTests.DeleteSelectedTracesAsync_WithUnselectedTraces_SkipsThem | Tylko zaznaczone |
| AppDiscoveryServiceTests.DiscoverAllAppsAsync_WithRegistryData_ReturnsApps | Odkrywanie z rejestru |
| AppDiscoveryServiceTests.DiscoverAllAppsAsync_WithEmptyRegistry_ReturnsEmptyList | Pusty registry |
| AppDiscoveryServiceTests.RefreshAsync_AfterDiscovery_ReloadsApps | Refresh działa |
