<div align="center">

# BR Unistaller

**A free, open-source alternative to Revo Uninstaller — built with C# / .NET 8 WPF.**

[Releases](https://github.com/brutal-build/BR-Unistaller/releases) ·
[Features](#features) ·
[Installation](#installation) ·
[Stack](#stack)

![version](https://img.shields.io/badge/version-0.0.1-blue)![license](https://img.shields.io/badge/license-MIT-white)![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)![dotnet](https://img.shields.io/badge/.NET-8.0-purple)

</div>

---

## Overview

BR Unistaller is a powerful Windows application manager and uninstaller that goes beyond the standard Windows "Add or Remove Programs" panel. It discovers installed applications (Win32, MSI, UWP/Store), handles silent and forced uninstalls, scans for leftover registry keys and files, and manages startup entries — all with a modern dark UI.

Inspired by Revo Uninstaller but free and open source (MIT).

## Features

- **App Discovery** — Scans 4 registry branches + MSI database + UWP packages with detailed metadata
- **Normal Uninstall** — Runs the application's own uninstaller with real-time progress tracking
- **Post-Uninstall Scan** — Finds leftover registry keys, files, services, and scheduled tasks
- **Force Uninstall** — Kills processes, then deep-scans and removes all traces without running the uninstaller
- **Batch Uninstall** — Uninstalls multiple applications with per-app progress bars
- **Startup Manager** — Lists and toggles startup entries (registry, folders, services)
- **Junk Cleaner** — Scans and cleans temporary files, prefetch, logs, and memory dumps
- **Search & Filter** — Real-time search and category filtering (All / Win32 / Store Apps)
- **Export Reports** — Saves uninstall logs as CSV, JSON, or HTML

## Installation

```bash
git clone https://github.com/brutal-build/BR-Unistaller.git
cd BR-Unistaller
dotnet build -c Release
dotnet publish src/BrutalUninstaller.App -c Release -o publish
cd publish
.\BrutalUninstaller.App.exe
```

Or download the latest release from [Releases](https://github.com/brutal-build/BR-Unistaller/releases).

> **Requires Windows 10/11 and [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). Must be run as Administrator.**

### Quick Build Script

```powershell
.\build-run.ps1
```

## Usage

1. Launch the app as Administrator
2. Browse the list of installed applications
3. Select an app and choose:
   - `Uninstall` — standard removal with post-uninstall trace scan
   - `Force Uninstall` — kill processes + deep scan + remove all traces
   - `Batch Uninstall` — queue multiple apps with progress tracking
4. Scan — inspect traces before/after uninstall (registry, files, services)
5. Use Tools from the sidebar — Startup Manager, Junk Cleaner

## Running Tests

```bash
dotnet test -c Release -v normal
```

## Stack

| Technology | Purpose |
|---|---|
| .NET 8 WPF | Desktop framework |
| CommunityToolkit.Mvvm | MVVM pattern |
| Microsoft.Extensions.DI | Dependency injection |
| Serilog | Logging |
| P/Invoke | Win32 API access |
| Windows.Management.Deployment | UWP package management |
| xUnit + Moq | Testing |

## License

MIT — free to use, modify, and distribute.

---

Built by [@brutalbuild](https://github.com/brutal-build)
