# Research: Core Infrastructure

**Feature**: 002-core-infrastructure
**Phase**: 0 — Pre-design research
**Date**: 2026-04-07

## Questions researched

### 1. Schema bootstrap: plain SQL vs EF Core Migrations vs Fluent Migrator

**Decision**: Plain SQL scripts executed by `SchemaBootstrap` service on server startup.

**Rationale**:
- EF Core Migrations adds ~ 10 NuGet packages, entity-annotation or fluent config overhead, and a migration history table (`__EFMigrationsHistory`). For a single-user MVP with 8 tables and no schema evolution required at launch, this is pure overhead.
- Fluent Migrator is a reasonable alternative but still a third-party dependency solving a problem that plain SQL handles in < 50 lines at this scope.
- Plain SQL: `CREATE TABLE IF NOT EXISTS` makes each statement idempotent at the DDL level. No history table. No ORM dependency. Fully readable schema in one file. Migration path to a proper migration tool is trivially possible later.

**Alternatives considered**: EF Core Migrations, Fluent Migrator, Dapper + inline SQL.

**Implementation note**: A single `schema.sql` embedded resource (or inline constant) executed by `SchemaBootstrap.ApplyAsync()` called from `Program.cs` before the HTTP pipeline is built.

---

### 2. Serilog SQLite sink: which package?

**Decision**: `Serilog.Sinks.SQLite` (NuGet: `Serilog.Sinks.SQLite`).

**Rationale**:
- The most widely used SQLite sink for Serilog; compatible with `Microsoft.Data.Sqlite`.
- Automatically creates the `Logs` table with standard columns: `Timestamp`, `Level`, `Exception`, `RenderedMessage`, `Properties`.
- Supports batch writing, reducing hot-path latency.

**Alternatives considered**: `Serilog.Sinks.Sqlite` (different casing, older fork, less maintained).

**Implementation note**: Configure minimum level `Information` for file sink; `Warning` for SQLite sink to avoid Logs table bloat.

---

### 3. Microsoft.Data.Sqlite vs System.Data.SQLite

**Decision**: `Microsoft.Data.Sqlite` (NuGet: `Microsoft.Data.Sqlite`).

**Rationale**:
- Official Microsoft package, ships as part of the EF Core family but usable standalone.
- Targets .NET 10 natively; no native SQLite binaries bundled — uses the OS-provided or bundled SQLitePCLRaw.
- Lighter than `System.Data.SQLite` (no COM registration, no win32 DLL extraction).
- Well-maintained, consistent updates alongside .NET releases.

**Alternatives considered**: `System.Data.SQLite` (heavier, COM-based on Windows), `SQLitePCLRaw.bundle_e_sqlite3` (low-level, no ADO.NET wrapper).

---

### 4. .NET 10 project SDK for Server: `Microsoft.NET.Sdk.Web` vs `Microsoft.NET.Sdk`

**Decision**: `Microsoft.NET.Sdk.Web` for `SunnySunday.Server`.

**Rationale**:
- REST API server will use ASP.NET Core Minimal APIs (feature 004). Scaffolding with `.Web` SDK now avoids a project file change later.
- Adds `IWebHostEnvironment`, `IHostedService`, and `IHostApplicationBuilder` integration out of the box.
- No cost for a project that will become an API server.

**Implementation note**: `SunnySunday.Core` uses `Microsoft.NET.Sdk` (class library). `SunnySunday.Cli` uses `Microsoft.NET.Sdk` (console app). `SunnySunday.Tests` uses `Microsoft.NET.Sdk` (xUnit test project).

---

### 5. xUnit version for .NET 10

**Decision**: xUnit v2 (`xunit` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk`), pinned to latest stable.

**Rationale**:
- xUnit v2 is the ecosystem standard; v3 is in preview and not yet production-stable for .NET 10 as of Q1 2026.
- `dotnet test` integration is seamless with `Microsoft.NET.Test.Sdk`.

---

### 6. Serilog minimum version for .NET 10

**Decision**: Serilog 4.x (latest stable in the 4.x series).

**Rationale**:
- Serilog 4.0+ dropped `netstandard2.0` fallback in favour of `net6.0`+ targets; fully compatible with .NET 10.
- `Serilog.Sinks.File` 5.x and `Serilog.Sinks.SQLite` 3.x are compatible.

---

## All NEEDS CLARIFICATION resolved

No `NEEDS CLARIFICATION` items remain in the Technical Context.
