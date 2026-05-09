# Quickstart: Evolve CLI to TUI

**Feature**: 007-evolve-cli-to-tui
**Date**: 2026-05-09

---

## Prerequisites

- .NET 10 SDK installed (`dotnet --version` → `10.x`)
- Repository cloned and on branch `007-evolve-cli-to-tui` (or `task/TXXX-*` task branch)
- Solution builds cleanly from `main`: `dotnet build src/SunnySunday.slnx`
- A running `sunny-server` instance (for manual end-to-end testing)

---

## No New Dependencies

All TUI functionality uses Spectre.Console 0.55.0 (already referenced in `SunnySunday.Cli.csproj`). No new NuGet packages are required.

---

## Build and Run

### Build the entire solution

```bash
dotnet build src/SunnySunday.slnx
```

### Run in TUI mode (no arguments)

```bash
export SUNNY_SERVER=http://localhost:5000
dotnet run --project src/SunnySunday.Cli
```

This launches the interactive TUI with the book list screen.

### Run in CLI mode (with arguments — unchanged)

```bash
export SUNNY_SERVER=http://localhost:5000
dotnet run --project src/SunnySunday.Cli -- sync /path/to/My\ Clippings.txt
dotnet run --project src/SunnySunday.Cli -- status
dotnet run --project src/SunnySunday.Cli -- config show
```

All existing CLI commands continue to work identically.

### Run via Docker

```bash
# TUI mode (interactive — requires -it)
docker run --rm -it -e SUNNY_SERVER=http://host.docker.internal:5000 sunny

# CLI mode
docker run --rm -e SUNNY_SERVER=http://host.docker.internal:5000 sunny status
```

---

## Run Tests

### All tests

```bash
dotnet test src/SunnySunday.slnx
```

### TUI-specific tests only

```bash
dotnet test src/SunnySunday.Tests --filter "FullyQualifiedName~Tui"
```

---

## Project Layout (new files)

```
src/SunnySunday.Cli/
├── Program.cs                          ← MODIFIED: add TUI mode detection
├── Tui/
│   ├── TuiApp.cs                       ← NEW: render loop orchestrator
│   ├── IScreen.cs                      ← NEW: screen interface + ScreenResult
│   ├── StatusChrome.cs                 ← NEW: persistent header (Figlet, version, status)
│   ├── BookListScreen.cs               ← NEW: main screen with book table
│   ├── HighlightDetailScreen.cs        ← NEW: drill-down per book
│   ├── SettingsScreen.cs               ← NEW: settings editor
│   ├── SearchFilter.cs                 ← NEW: client-side search logic
│   └── ViewModels/
│       ├── BookViewModel.cs            ← NEW: book aggregation model
│       └── HighlightViewModel.cs       ← NEW: highlight display model
├── Infrastructure/
│   ├── SunnyHttpClient.cs              ← MODIFIED: add GetHighlightsAsync, DeleteHighlightAsync, PostTestRecapAsync
│   └── SunnyJsonContext.cs             ← MODIFIED: add HighlightsResponse serialization

src/SunnySunday.Tests/
└── Tui/
    ├── ModeDetectionTests.cs           ← NEW: TUI vs CLI mode detection
    ├── BookGroupingTests.cs            ← NEW: highlight → book grouping
    ├── SearchFilterTests.cs            ← NEW: client-side search
    ├── BookListScreenTests.cs          ← NEW: key handling + navigation
    ├── HighlightDetailScreenTests.cs   ← NEW: actions + navigation
    └── SettingsScreenTests.cs          ← NEW: field editing logic
```

---

## Key Shortcuts (TUI Mode)

| Key | Context | Action |
|-----|---------|--------|
| `↑` / `↓` | All screens | Navigate list items |
| `Enter` | Book list | Open highlight detail |
| `Enter` | Settings | Edit selected field |
| `Esc` | Any non-root screen | Go back |
| `S` | Book list | Open settings |
| `/` | Book list | Activate search |
| `T` | Settings | Send test email |
| `Q` | Any screen | Quit TUI |
| `Ctrl+C` | Any screen | Quit TUI |
