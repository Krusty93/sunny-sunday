# Developer Quickstart: Core Infrastructure

**Feature**: 002-core-infrastructure
**Target audience**: Contributor setting up the repo for the first time

## Prerequisites

- The devcontainer (`.devcontainer/devcontainer.json`) includes .NET 10 via `mcr.microsoft.com/devcontainers/dotnet` — no manual SDK install needed
- Docker (for server integration tests — optional for this feature)
- A SQLite client for inspection (`sqlite3` CLI or DB Browser for SQLite)

## 1. Clone & build

```bash
git checkout 002-core-infrastructure
dotnet build
```

Expected output: `Build succeeded. 0 Error(s). 0 Warning(s).`

## 2. Run tests

```bash
dotnet test
```

Expected output: all tests pass (zero tests exist at this stage, but the project must compile).

## 3. Run the server locally

The server requires a writable `/data/` directory. For local development, use a `.data/` folder in the repo root (already in `.gitignore`):

```bash
dotnet run --project src/SunnySunday.Server
```

On first run the server will:
1. Create `.data/logs/` if it does not exist, then start Serilog
2. Create `.data/sunny.db` with all 7 schema tables (errors logged if this fails)
3. Start an HTTP listener (port TBD — configured in feature 004)

Verify the schema:
```bash
sqlite3 .data/sunny.db .tables
# Expected: authors  books  excluded_authors  excluded_books  highlights  settings  users
```

## 4. Inspect logs

```bash
# File sink
tail -f .data/logs/sunny-$(date +%Y%m%d).log

# SQLite sink
sqlite3 .data/sunny.db "SELECT Timestamp, Level, RenderedMessage FROM Logs ORDER BY Timestamp DESC LIMIT 10;"
```

## 5. Project structure at a glance

| Project | SDK | Purpose |
|---------|-----|---------|
| `SunnySunday.Core` | `Microsoft.NET.Sdk` (class library) | Domain models — shared by all projects |
| `SunnySunday.Server` | `Microsoft.NET.Sdk.Web` | HTTP server — schema bootstrap, Serilog, future REST API |
| `SunnySunday.Cli` | `Microsoft.NET.Sdk` (console) | Client binary — no commands yet |
| `SunnySunday.Tests` | `Microsoft.NET.Sdk` (xUnit) | Tests — references all three above |

## Notes for implementers

- **Schema changes** in this feature use `CREATE TABLE IF NOT EXISTS`. If you need to change a column later, add a migration script and update `SchemaBootstrap.cs`.
- **No EF Core** — all SQL is plain ADO.NET via `Microsoft.Data.Sqlite`.
- **Serilog minimum level**: `Information` for file sink, `Warning` for SQLite sink.
- **Do not add REST endpoints** in this feature — that is feature 004.
- **Do not add CLI commands** in this feature — that is feature 007.
