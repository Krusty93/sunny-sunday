# Implementation Plan: Core Infrastructure

**Branch**: `002-core-infrastructure` | **Date**: 2026-04-07 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-core-infrastructure/spec.md`

## Summary

Scaffold the .NET 10 solution with four projects (`SunnySunday.Core`, `SunnySunday.Server`, `SunnySunday.Cli`, `SunnySunday.Tests`), define shared domain models in Core, create the SQLite schema idempotently on server startup using plain SQL scripts, and wire Serilog with file-rolling and SQLite sinks. No business logic, no REST endpoints, no CLI commands.

## Technical Context

**Language/Version**: C# / .NET 10 (net10.0 TFM)
**Primary Dependencies**: Microsoft.Data.Sqlite (schema bootstrap), Serilog + Serilog.Sinks.File + Serilog.Sinks.SQLite (logging), Spectre.Console (CLI host — no commands yet)
**Storage**: SQLite at `/data/sunny.db` (single file, Docker volume)
**Testing**: xUnit — no test content in this feature; project must compile and `dotnet test` must exit 0
**Target Platform**: Linux (Docker container) for server; macOS/Linux/Windows for client CLI
**Project Type**: Multi-project .NET solution (library + web service + CLI + test host)
**Performance Goals**: Schema bootstrap in < 5 s on cold start
**Constraints**: Schema creation must be idempotent (safe to run on every server start). No EF Core for MVP — plain SQL.
**Scale/Scope**: Single-user, single-file SQLite. No indexes at this stage.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Client/Server Separation | PASS | Four-project split enforces server-only DB access; CLI has no data access here |
| II. CLI-First, No GUI | PASS | No web UI added; Spectre.Console referenced but no commands yet |
| III. Zero-Config Onboarding | PASS | Schema auto-created on startup — zero manual DB setup required |
| IV. Local Processing Only | PASS | No external services touched in this feature |
| V. Tests Ship with Code | PASS | SunnySunday.Tests project included; compilation gate verified by CI |
| VI. Simplicity Over Premature Generalization | PASS | Plain SQL over EF Core Migrations; no indexes; no Repository pattern |
| Tech: .NET 10 | PASS | net10.0 TFM in all projects |
| Tech: Serilog file + SQLite sinks | PASS | Both sinks configured in server startup |
| Tech: No raw Console.WriteLine | PASS | All logging through Serilog; all CLI output through Spectre.Console |
| Tech: Docker-only server | PASS | No native server packaging |

**Gate result: PASS — no violations.**

## Project Structure

### Documentation (this feature)

```text
specs/002-core-infrastructure/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── SunnySunday.Core/
│   ├── SunnySunday.Core.csproj
│   └── Models/
│       ├── Highlight.cs
│       ├── Book.cs
│       ├── Author.cs
│       ├── User.cs
│       └── Settings.cs
├── SunnySunday.Server/
│   ├── SunnySunday.Server.csproj
│   ├── Program.cs
│   └── Infrastructure/
│       ├── Database/
│       │   └── SchemaBootstrap.cs
│       └── Logging/
│           └── SerilogConfiguration.cs
├── SunnySunday.Cli/
│   ├── SunnySunday.Cli.csproj
│   └── Program.cs
└── SunnySunday.Tests/
    ├── SunnySunday.Tests.csproj
    └── Infrastructure/
        └── SchemaBootstrapTests.cs

SunnySunday.slnx
```

**Structure Decision**: Multi-project solution under `src/`. `SunnySunday.Core` is a class library with no dependencies. Server and CLI reference Core. Tests reference all three. This matches the constitution's Client/Server Separation principle and enables independent deployment of server (Docker) and CLI (single-file binary).

## Complexity Tracking

> No constitution violations — this section is empty.
