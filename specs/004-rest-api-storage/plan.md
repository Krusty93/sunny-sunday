# Implementation Plan: REST API & Storage Layer

**Branch**: `004-rest-api-storage` | **Date**: 2026-04-18 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/004-rest-api-storage/spec.md`

## Summary

Implement the server-side REST API and SQLite-backed storage layer for Sunny Sunday. The server exposes 14 Minimal API endpoints organized into 5 domain groups (sync, settings, status, exclusions, weights). Data access uses Dapper over the existing `Microsoft.Data.Sqlite` connection against the schema already bootstrapped by feature 002. Request/response contracts are separate DTOs from domain models. Validation is inline; errors use RFC 9457 ProblemDetails. Tests use `WebApplicationFactory` with in-memory SQLite.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0` TFM)
**Primary Dependencies**: ASP.NET Minimal API (built-in), Dapper (new), Swashbuckle.AspNetCore (new), Microsoft.Data.Sqlite (existing), Serilog (existing)
**Storage**: SQLite at `.data/sunny.db` — schema already exists via `SchemaBootstrap` (feature 002)
**Testing**: xUnit + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) with in-memory SQLite
**Target Platform**: Linux Docker container (primary), cross-platform .NET 10
**Project Type**: Web service (REST API)
**Performance Goals**: 1,000-highlight import in < 5 seconds (SC-001)
**Constraints**: Single-user MVP, no authentication, no indexes
**Scale/Scope**: Single user, ~1,000–10,000 highlights, 14 endpoints

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Client/Server Separation | **PASS** | All endpoints are server-side; CLI calls them via HTTP |
| II. CLI-First, No GUI | **PASS** | REST API consumed by CLI; no web UI |
| III. Zero-Config Onboarding | **PASS** | User auto-created on first request; only Kindle email required (via settings) |
| IV. Local Processing Only | **PASS** | All data in local SQLite; no third-party services |
| V. Tests Ship with Code | **PASS** | Integration tests for all endpoints in same PR |
| VI. Simplicity / YAGNI | **PASS** | Dapper (not EF Core), manual validation (not FluentValidation), no auth, no indexes |
| Tech: C# / .NET 10 only | **PASS** | All code is C# |
| Tech: SQLite only | **PASS** | Single SQLite file, no second database |
| Tech: Serilog logging | **PASS** | Existing Serilog configuration reused |
| Tech: REST HTTP + JSON | **PASS** | Minimal API endpoints returning JSON |
| Tech: Docker distribution | **PASS** | No changes to distribution model |
| Exclusion: No web UI | **PASS** | API only |
| Exclusion: No auth for MVP | **PASS** | No authentication middleware |

**Post-design re-check**: All gates still pass. Dapper is the only new NuGet package for Server; `Microsoft.AspNetCore.Mvc.Testing` added to Tests only. No new projects introduced — all code goes into existing `SunnySunday.Server` and `SunnySunday.Tests`.

## Project Structure

### Documentation (this feature)

```text
specs/004-rest-api-storage/
├── plan.md              # This file
├── research.md          # Technology decisions and rationale
├── data-model.md        # DTO definitions and mapping
├── quickstart.md        # Developer quick-start guide
├── contracts/
│   └── api.md           # Full API contract documentation
└── tasks.md             # Implementation tasks (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/SunnySunday.Server/
├── Program.cs                          # Updated: DI + MapGroup composition + Swagger (Dev only)
├── Models/                             # MOVED from SunnySunday.Core — server-side domain only
│   ├── Author.cs
│   ├── Book.cs
│   ├── Highlight.cs
│   ├── Settings.cs
│   └── User.cs
├── Data/                               # NEW — Dapper data access
│   ├── UserRepository.cs
│   ├── SyncRepository.cs
│   ├── SettingsRepository.cs
│   ├── StatusRepository.cs
│   ├── ExclusionRepository.cs
│   └── WeightRepository.cs
├── Endpoints/                          # NEW — Minimal API endpoint groups
│   ├── SyncEndpoints.cs
│   ├── StatusEndpoints.cs
│   ├── SettingsEndpoints.cs
│   ├── ExclusionEndpoints.cs
│   └── WeightEndpoints.cs
└── Infrastructure/                     # EXISTING
    ├── Database/
    │   └── SchemaBootstrap.cs
    └── Logging/
        └── SerilogConfiguration.cs

src/SunnySunday.Core/
└── Contracts/                          # NEW — request/response DTOs (shared with CLI)
    ├── SyncRequest.cs
    ├── SyncResponse.cs
    ├── SettingsResponse.cs
    ├── UpdateSettingsRequest.cs
    ├── StatusResponse.cs
    ├── ExclusionsResponse.cs
    ├── SetWeightRequest.cs
    └── WeightedHighlightDto.cs

src/SunnySunday.Tests/
├── Api/                                # NEW — integration tests
│   ├── TestWebApplicationFactory.cs
│   ├── SyncEndpointTests.cs
│   ├── StatusEndpointTests.cs
│   ├── SettingsEndpointTests.cs
│   ├── ExclusionEndpointTests.cs
│   └── WeightEndpointTests.cs
├── Infrastructure/                     # EXISTING
│   └── SchemaBootstrapTests.cs
└── Parsing/                            # EXISTING
    └── ClippingsParserTests.cs
```

**Structure Decision**: All new code goes into the existing `SunnySunday.Server` and `SunnySunday.Tests` projects. No new projects are added. Server code is organized by concern: `Contracts/` for DTOs, `Data/` for Dapper-based repositories, `Endpoints/` for route definitions. Tests use `WebApplicationFactory` for full-pipeline integration testing with in-memory SQLite.

## Complexity Tracking

No constitution violations. No complexity justification needed.
