# Implementation Plan: Email Delivery Management

**Branch**: `006-email-delivery` | **Date**: 2026-04-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/006-email-delivery/spec.md`

## Summary

Implement the email delivery management layer for Sunny Sunday: Kindle email validation on `PUT /settings` (format check + normalization), an SMTP readiness service exposed in `GET /status`, a `POST /test-delivery` endpoint that sends a test EPUB without affecting recap state, a `GET /deliveries` paginated delivery history endpoint, and three CLI commands (`sunny delivery test`, `sunny delivery log`, `sunny delivery status`) built with Spectre.Console. All infrastructure (MailKit, Polly, EpubComposer, RecapRepository, SmtpSettings) already exists from feature 005 — this feature wires it into user-facing surfaces.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0` TFM)
**Primary Dependencies**: MailKit, Polly, Dapper, Microsoft.Data.Sqlite, Quartz.NET, Spectre.Console, Serilog (all already in project)
**New Dependencies**: None
**Storage**: SQLite at `.data/sunny.db` — existing schema; no new tables needed (reads from `recap_jobs`)
**Testing**: xUnit + `WebApplicationFactory` (in-memory SQLite) via existing `SunnyTestApplicationFactory`
**Target Platform**: Linux Docker container (server), cross-platform CLI
**Project Type**: Web service (server) + CLI (client)
**Performance Goals**: Test delivery completes within SMTP timeout (30s); delivery list query < 100ms for 10,000 rows
**Constraints**: Single-user MVP, no authentication, SMTP credentials never exposed in responses
**Scale/Scope**: Single user, up to ~10,000 recap job records

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Client/Server Separation | **PASS** | Server handles validation, SMTP check, test delivery, history query; CLI only calls REST endpoints |
| II. CLI-First, No GUI | **PASS** | Three new CLI commands via Spectre.Console; no web UI |
| III. Zero-Config Onboarding | **PASS** | SMTP readiness check provides clear guidance; test delivery verifies setup before waiting for scheduled recap |
| IV. Local Processing Only | **PASS** | SMTP is direct server→user relay; no third-party cloud services |
| V. Tests Ship with Code | **PASS** | Endpoint tests for validation, test delivery, delivery list; integration tests for SMTP readiness |
| VI. Simplicity / YAGNI | **PASS** | Reuses all existing infrastructure; no new abstractions, no new NuGet packages, no new database tables |
| Tech: C# / .NET 10 only | **PASS** | All new code is C# |
| Tech: SQLite only | **PASS** | Reads existing `recap_jobs` table; no new tables |
| Tech: Serilog logging | **PASS** | Serilog used for SMTP readiness warnings at startup |
| Tech: MailKit | **PASS** | Reuses existing `MailDeliveryService` for test delivery |
| Tech: REST HTTP + JSON | **PASS** | Two new JSON endpoints: `POST /test-delivery`, `GET /deliveries` |
| Tech: Docker distribution | **PASS** | No changes to distribution; SMTP config via existing env vars |
| Tech: Spectre.Console | **PASS** | CLI commands use Spectre.Console for table rendering |
| Exclusion: No web UI | **PASS** | No UI components |
| Exclusion: No auth for MVP | **PASS** | No authentication changes |

**Post-design re-check**: All gates pass. No new NuGet packages. No new database tables. No new projects. Kindle email validation tightens existing `PUT /settings` validation. SMTP readiness is a computed check on existing `SmtpSettings`. Test delivery reuses `EpubComposer` + `MailDeliveryService`.

## Project Structure

### Documentation (this feature)

```text
specs/006-email-delivery/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (API contracts)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code Changes

```text
src/SunnySunday.Server/
├── Program.cs                                ← Updated: register SmtpReadinessService, map new endpoints, startup SMTP warning
├── Services/
│   ├── SmtpReadinessService.cs               ← NEW: checks SmtpSettings completeness, reports missing fields
│   └── ISmtpReadinessService.cs              ← NEW: interface
├── Endpoints/
│   ├── SettingsEndpoints.cs                  ← Updated: Kindle email validation (tighten IsValidEmail to @kindle.com/@free.kindle.com)
│   ├── StatusEndpoints.cs                    ← Updated: add smtpReady field from SmtpReadinessService
│   ├── TestDeliveryEndpoints.cs              ← NEW: POST /test-delivery
│   └── DeliveryEndpoints.cs                  ← NEW: GET /deliveries (paginated)
├── Data/
│   └── RecapRepository.cs                    ← Updated: add GetDeliveriesAsync (paginated query with total count)
└── Models/
    (no changes — RecapJobRecord already has all needed fields)

src/SunnySunday.Core/Contracts/
├── StatusResponse.cs                         ← Updated: +SmtpReady boolean
├── TestDeliveryResponse.cs                   ← NEW: success/failure indicator + error message
├── DeliveryResponse.cs                       ← NEW: paginated delivery list DTO
└── DeliveryRecord.cs                         ← NEW: single delivery row DTO

src/SunnySunday.Cli/
├── Program.cs                                ← Updated: add delivery command group (test, log, status)

src/SunnySunday.Tests/
├── Api/
│   ├── SettingsEndpointTests.cs              ← Updated: Kindle email validation test cases
│   ├── TestDeliveryEndpointTests.cs          ← NEW: test delivery endpoint tests
│   ├── DeliveryEndpointTests.cs              ← NEW: delivery history endpoint tests
│   └── StatusEndpointTests.cs                ← Updated: smtpReady field tests
└── Infrastructure/
    └── SmtpReadinessServiceTests.cs          ← NEW: unit tests for SMTP completeness logic
```

**Structure Decision**: Follows the established multi-project solution layout. All server changes stay in `SunnySunday.Server`, shared DTOs in `SunnySunday.Core/Contracts`, CLI in `SunnySunday.Cli`, tests in `SunnySunday.Tests`. No new projects required.

## Complexity Tracking

No constitution violations. No complexity justification needed.
