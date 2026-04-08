# Tasks: Core Infrastructure

**Input**: Design documents from `/specs/002-core-infrastructure/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, quickstart.md ✅

**Tests**: Not explicitly requested in the feature spec — no test tasks generated except for the compilation gate (US1 acceptance criterion).

**Organization**: Tasks grouped by user story. US1 (solution scaffold) is the sole blocking prerequisite for US2 and US3, which can proceed in parallel once US1 is complete.

---

## Phase 0: Prerequisites

**Purpose**: Ensure the devcontainer runs .NET 10 and the repository has the correct .NET-specific ignore rules before any source file is created.

- [ ] T000a Switch `.devcontainer/devcontainer.json` base image from `mcr.microsoft.com/devcontainers/base:trixie` to `mcr.microsoft.com/devcontainers/dotnet:1-10.0` and set `name` to `Sunny Sunday`; keep all other features and extensions unchanged — rebuild the devcontainer after this change
- [ ] T000b Generate the standard .NET `.gitignore` at repository root via `dotnet new gitignore` (covers `bin/`, `obj/`, `*.user`, `*.suo`, NuGet fallback folders, etc.)

---

## Phase 1: Setup

**Purpose**: Create the .NET 10 solution file and project scaffolding.

- [X] T001 Create `src/SunnySunday.slnx` solution file in the `src/` directory with `dotnet new sln -n SunnySunday -o src`
- [X] T002 Create `src/SunnySunday.Core/SunnySunday.Core.csproj` as a class library targeting `net10.0` (SDK: `Microsoft.NET.Sdk`); add to `src/SunnySunday.slnx`
- [ ] T003 [P] Create `src/SunnySunday.Server/SunnySunday.Server.csproj` as a web application targeting `net10.0` (SDK: `Microsoft.NET.Sdk.Web`); add to `src/SunnySunday.slnx`
- [ ] T004 [P] Create `src/SunnySunday.Cli/SunnySunday.Cli.csproj` as a console app targeting `net10.0` (SDK: `Microsoft.NET.Sdk`)
- [ ] T005 [P] Create `src/SunnySunday.Tests/SunnySunday.Tests.csproj` as an xUnit test project targeting `net10.0` with packages: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`
- [X] T006 ~~Add all four projects to `src/SunnySunday.slnx` via `dotnet sln add`~~ — superseded: each project (T002–T005) adds itself to the solution in its own task

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Project references and NuGet packages — must be complete before any user story work.

**⚠️ CRITICAL**: T007–T010 must complete before US1/US2/US3 implementation tasks.

- [ ] T007 Add project reference `SunnySunday.Core` → in `SunnySunday.Server.csproj`
- [ ] T008 [P] Add project reference `SunnySunday.Core` → in `SunnySunday.Cli.csproj`
- [ ] T009 Add project references `SunnySunday.Core`, `SunnySunday.Server`, `SunnySunday.Cli` → in `SunnySunday.Tests.csproj`
- [ ] T010 [P] Add NuGet packages to `SunnySunday.Server.csproj`: `Microsoft.Data.Sqlite`, `Serilog`, `Serilog.Sinks.File`, `Serilog.Sinks.SQLite`, `Serilog.Extensions.Hosting`

**Checkpoint**: All project references and packages restored — user story implementation can begin.

---

## Phase 3: User Story 1 — Solution Builds Cleanly (Priority: P1) 🎯 MVP

**Goal**: All four projects compile with zero errors and zero warnings; `dotnet test` exits 0.

**Independent Test**: `dotnet build` exits 0 on a fresh clone; `dotnet test` exits 0.

- [ ] T011 [US1] Create `src/SunnySunday.Core/Models/Highlight.cs` — plain C# class with properties: `Id`, `UserId`, `BookId`, `Text`, `Weight`, `Excluded`, `LastSeen`, `DeliveryCount`, `CreatedAt`
- [ ] T012 [P] [US1] Create `src/SunnySunday.Core/Models/Book.cs` — plain C# class with properties: `Id`, `UserId`, `AuthorId`, `Title`
- [ ] T013 [P] [US1] Create `src/SunnySunday.Core/Models/Author.cs` — plain C# class with properties: `Id`, `Name`
- [ ] T014 [P] [US1] Create `src/SunnySunday.Core/Models/User.cs` — plain C# class with properties: `Id`, `KindleEmail`, `CreatedAt`
- [ ] T015 [P] [US1] Create `src/SunnySunday.Core/Models/Settings.cs` — plain C# class with properties: `UserId`, `Schedule`, `DeliveryDay`, `DeliveryTime`, `Count`
- [ ] T016 [US1] Create `src/SunnySunday.Cli/Program.cs` — minimal entry point using `Spectre.Console`; add `Spectre.Console` NuGet to `SunnySunday.Cli.csproj`; no commands yet, just application host bootstrap
- [ ] T017 [US1] Verify `dotnet build` exits 0 with no errors and no warnings across all four projects

---

## Phase 4: User Story 2 — SQLite Schema Initialized on Server Startup (Priority: P2)

**Goal**: Server creates `sunny.db` with all 7 domain tables on first run; idempotent on restart.

**Independent Test**: Run server against empty volume → `sqlite3 /data/sunny.db .tables` shows all 7 tables.

- [ ] T018 [US2] Create `src/SunnySunday.Server/Infrastructure/Database/SchemaBootstrap.cs` — service with `ApplyAsync(string dbPath)` method that executes all `CREATE TABLE IF NOT EXISTS` DDL statements using `Microsoft.Data.Sqlite`
- [ ] T019 [US2] Implement the full DDL in `SchemaBootstrap.cs` for all 7 tables: `users`, `authors`, `books`, `highlights`, `excluded_books`, `excluded_authors`, `settings` (exact DDL from `data-model.md`)
- [ ] T020 [US2] Create `src/SunnySunday.Server/Program.cs` — minimal ASP.NET Core host; DB path hardcoded to `.data/sunny.db`; call `SchemaBootstrap.ApplyAsync()` before `app.Run()` (Serilog will be wired before this step in T023, so DB errors will be logged)
- [ ] T021 [US2] Verify idempotency: running `SchemaBootstrap.ApplyAsync()` twice on the same database produces no errors (covered by `CREATE TABLE IF NOT EXISTS` semantics)

---

## Phase 5: User Story 3 — Serilog Writes Structured Logs (Priority: P3)

**Goal**: All log entries written to daily rolling file under `/data/logs/` AND to the `Logs` table in `sunny.db`.

**Independent Test**: Start server, make one HTTP request, verify log file exists and `SELECT COUNT(*) FROM Logs` > 0.

- [ ] T022 [US3] Create `src/SunnySunday.Server/Infrastructure/Logging/SerilogConfiguration.cs` — static helper `ConfigureLogging(WebApplicationBuilder builder, string dbPath)` that: (1) calls `Directory.CreateDirectory(".data/logs")` to ensure the log directory exists before Serilog initializes, (2) configures Serilog with file sink (`.data/logs/sunny-.log`, rolling interval daily, minimum level `Information`) and SQLite sink (`dbPath`, table `Logs`, minimum level `Warning`); both paths are hardcoded constants — no env var
- [ ] T023 [US3] Wire `SerilogConfiguration.ConfigureLogging()` into `src/SunnySunday.Server/Program.cs` — call it as the **first operation** on the host builder, before `SchemaBootstrap.ApplyAsync()`, so that DB initialization errors are captured in both log sinks; use `UseSerilog()` on the host builder
- [ ] T024 [US3] Emit a startup log entry (`Information` level) in `Program.cs` after schema bootstrap completes: `"Sunny Sunday server started. Database: {DbPath}"` — verifies both sinks are wired before any HTTP request

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T025 [P] Add `.data/` to `.gitignore` (local development data directory per `quickstart.md`)
- [ ] T026 [P] Verify `dotnet test` exits 0 (no test content yet, but project must compile and runner must report 0 failures)
- [ ] T027 Update `specs/002-core-infrastructure/spec.md` status from `Draft` to `Implemented`

---

## Dependencies

```
T001 → T002 → T003, T004, T005 → T006
T006 → T007, T008, T009, T010
T010 → T011 → T012, T013, T014, T015 → T016 → T017
T017 → T018 → T019 → T020 → T021
T021 → T022 → T023 → T024
T024 → T025, T026, T027
```

**Story completion order**: US1 (T011–T017) → US2 (T018–T021) and US3 (T022–T024) can run in parallel after US1.

---

## Parallel Execution Examples

**After T010 completes**, these groups can proceed in parallel:
- T011 + T012 + T013 + T014 + T015 (all domain model files in `SunnySunday.Core/Models/` — no interdependencies)
- T007 + T008 (project references — different files)

**After T017 (US1 complete)**, these groups can proceed in parallel:
- US2 thread: T018 → T019 → T020 → T021
- *(US3 depends on Serilog packages added in T010, but also on DB path from T020 — start US3 after T020)*

**After T020 completes**:
- US3 thread: T022 → T023 → T024

**Final phase** (T025, T026, T027): all parallelizable once T024 is done.

---

## Implementation Strategy

**MVP scope = Phase 1 + Phase 2 + Phase 3 (US1)**: a clean-building solution is sufficient for CI to be green and for other features to start branching.

**Incremental delivery**:
1. Phases 1–3 (T001–T017): solution scaffolded, models defined, `dotnet build` green ← merge-ready
2. Phase 4 (T018–T021): schema bootstrap working ← independently testable
3. Phase 5 (T022–T024): Serilog wired ← independently testable
4. Phase 6 (T025–T027): polish ← merge to main
