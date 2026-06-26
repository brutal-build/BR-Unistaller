# BR Unistaller

> A free, open-source alternative to Revo Uninstaller — built with C# / .NET 8 WPF.

![Version](https://img.shields.io/badge/version-0.0.1-blue)
![License](https://img.shields.io/badge/license-MIT-green)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)

---

## Overview

**BR Unistaller** is a powerful Windows application manager and uninstaller that goes beyond the standard Windows "Add or Remove Programs" panel. It discovers installed applications (Win32, MSI, UWP/Store), handles silent and forced uninstalls, scans for leftover registry keys and files, and manages startup entries — all with a modern, dark-themed UI.

Inspired by Revo Uninstaller but free and open source (MIT).

---

## Features

| Feature | Description |
|---------|-------------|
| **App Discovery** | Scans 4 registry branches + MSI database + UWP packages |
| **Normal Uninstall** | Runs the application's own uninstaller with progress tracking |
| **Post-Uninstall Scan** | Finds leftover registry keys, files, services, and scheduled tasks |
| **Force Uninstall** | Kills processes, then deep-scans and removes all traces without running the uninstaller |
| **Batch Uninstall** | Uninstalls multiple applications with per-app progress bars |
| **Startup Manager** | Lists and toggles startup entries (registry, folders, services) |
| **Junk Cleaner** | Scans and cleans temporary files, prefetch, logs, and memory dumps |
| **Export Reports** | Saves uninstall logs as CSV, JSON, or HTML |
| **Search & Filter** | Real-time search and category filtering (All / Win32 / Store Apps) |

---

## Screenshots

*(Add screenshots here)*

---

## Requirements

- **OS:** Windows 10 or Windows 11 (x64)
- **Runtime:** [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- **Permissions:** Administrator (required for registry access and app uninstallation)

---

## Installation

### Option 1: Download Release

1. Go to the [Releases](https://github.com/brutalbuild/BR-Unistaller/releases) page
2. Download the latest `BR-Unistaller.zip`
3. Extract and run `BR Unistaller.exe` as Administrator

### Option 2: Build from Source

```bash
# Clone
git clone https://github.com/brutalbuild/BR-Unistaller.git
cd BR-Unistaller

# Build
dotnet build -c Release

# Publish
dotnet publish src/BrutalUninstaller.App -c Release -o publish

# Run (as Administrator)
cd publish
.\BrutalUninstaller.App.exe
```

### Quick Build Script

```powershell
.\build-run.ps1
```

---

## Usage

1. **Launch** the app as Administrator
2. **Browse** the list of installed applications
3. **Select** an app and choose:
   - `Uninstall` — standard removal with post-uninstall trace scan
   - `Force Uninstall` — kill processes + deep scan + remove all traces
   - `Batch Uninstall` — queue multiple apps with progress tracking
4. **Scan** — inspect traces before/after uninstall (registry, files, services)
5. **Tools** — Startup Manager, Junk Cleaner, Scheduler (sidebar)

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 8 WPF |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| Logging | Serilog |
| API | P/Invoke, Windows.Management.Deployment |
| Testing | xUnit + Moq + FluentAssertions |

---

## Project Structure

```
BR-Unistaller/
├── src/
│   ├── BrutalUninstaller.App/          # WPF UI (Views, ViewModels, Converters)
│   ├── BrutalUninstaller.Core/         # Business logic (Services, Models, Interfaces)
│   └── BrutalUninstaller.Infrastructure/  # Win32/Registry/MSI/UWP wrappers
├── tests/
│   ├── BrutalUninstaller.Core.Tests/
│   └── BrutalUninstaller.Infrastructure.Tests/
├── publish/                            # Built executable output
├── build-run.ps1                       # One-click build + run script
├── BRUTAL_UNINSTALLER_SPEC.md          # Technical specification
├── agents.md                           # Agent definitions for automated development
└── README.md
```

---

## Running Tests

```bash
dotnet test -c Release -v normal
```

Current test coverage: **12 unit tests** (core services).

---

## Roadmap

- [x] App discovery (registry + MSI + UWP)
- [x] Normal uninstall with trace scanning
- [x] Force uninstall
- [x] Batch uninstall with progress UI
- [x] Startup manager
- [x] Junk cleaner
- [x] Export reports (CSV/JSON/HTML)
- [ ] Scheduled background scanning
- [ ] Dark/Light theme toggle
- [ ] WiX installer
- [ ] Hunter mode (drag-and-drop uninstall)

---

## License

MIT License — see [LICENSE](LICENSE).

---

## Author

**Brutal** ([@brutalbuild](https://github.com/brutalbuild))  
Built with C# / .NET 8 WPF
