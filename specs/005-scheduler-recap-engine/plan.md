# Implementation Plan: Scheduler + Recap Engine

**Branch**: `005-scheduler-recap-engine` | **Date**: 2026-04-27 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/005-scheduler-recap-engine/spec.md`

## Summary

Implement the automated recap pipeline for Sunny Sunday: a Quartz.NET-based scheduler fires daily (or weekly) at the user's configured local time, selects highlights by `score = age_in_days + weight` (tiebreak: most recently added), composes a Kindle-friendly EPUB 2 flat-list document, and delivers it via MailKit SMTP with a 3-attempt exponential retry (1 min, 5 min). Successful delivery updates `last_seen` and `delivery_count` per highlight; permanent failure leaves history unchanged and exposes the error via `GET /status`. All existing endpoints (`PUT /settings`, `GET /status`) are extended in-place — no new routes are added.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0` TFM)
**New Dependencies**: `Quartz.Extensions.Hosting` (Quartz.NET), `MailKit`
**Existing Dependencies**: Dapper, Microsoft.Data.Sqlite, Serilog, Swashbuckle.AspNetCore (all in `SunnySunday.Server.csproj`)
**Storage**: SQLite at `.data/sunny.db` — existing schema via `SchemaBootstrap`; this feature adds `recap_jobs` table and `timezone` column to `settings`
**Testing**: xUnit + `WebApplicationFactory` (in-memory SQLite) — existing test infrastructure; `IMailDeliveryService` interface for SMTP substitution
**Target Platform**: Linux Docker container (server), cross-platform CLI
**Performance Goals**: Recap generation completes in < 30 seconds for a 10,000-highlight DB (NFR-06)
**Constraints**: Single-user MVP, no authentication, in-memory Quartz job store (not persistent)
**Scale/Scope**: Single user, up to ~10,000 highlights; one recap trigger per day or per week

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Client/Server Separation | **PASS** | All scheduling and delivery runs server-side; CLI reads status |
| II. CLI-First, No GUI | **PASS** | No new UI; settings managed via existing CLI commands |
| III. Zero-Config Onboarding | **PASS** | Default timezone `"UTC"`, default schedule `daily 18:00` — no setup beyond Kindle email required |
| IV. Local Processing Only | **PASS** | SMTP is direct server→Amazon; no third-party cloud APIs |
| V. Tests Ship with Code | **PASS** | Selection, EPUB, retry, and endpoint tests included in same PR |
| VI. Simplicity / YAGNI | **PASS** | In-memory Quartz store (not persistent), manual retry (not Polly), manual EPUB (not library) |
| Tech: C# / .NET 10 only | **PASS** | All new code is C# |
| Tech: SQLite only | **PASS** | `recap_jobs` table in same SQLite file; Quartz uses in-memory store |
| Tech: Serilog logging | **PASS** | Serilog used for delivery events and error logging |
| Tech: REST HTTP + JSON | **PASS** | No new endpoints; existing endpoints extended |
| Tech: Docker distribution | **PASS** | No changes to distribution model; SMTP config via env vars |
| Tech: Quartz.NET | **PASS** | Explicitly listed in project stack |
| Tech: MailKit | **PASS** | Explicitly listed in project stack |
| Exclusion: No web UI | **PASS** | No new UI components |
| Exclusion: No auth for MVP | **PASS** | No authentication changes |

**Post-design re-check**: All gates pass. Two new NuGet packages (`Quartz.Extensions.Hosting`, `MailKit`) are both in the prescribed stack. No new projects introduced. No EF Core, no Polly, no EPUB library dependencies added.

## Project Structure

### Documentation (this feature)

```text
specs/005-scheduler-recap-engine/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Technology decisions and rationale
├── data-model.md        # Schema changes, domain models, contract updates
└── quickstart.md        # Developer quick-start guide
```

### Source Code Changes

```text
src/SunnySunday.Server/
├── Program.cs                                ← Updated: Quartz DI, SmtpSettings, new services/repos
├── Infrastructure/
│   ├── Database/
│   │   └── SchemaBootstrap.cs                ← Updated: recap_jobs table + timezone column migration
│   └── Smtp/
│       └── SmtpSettings.cs                   ← NEW: SMTP configuration POCO
├── Jobs/
│   └── RecapJob.cs                           ← NEW: Quartz IJob (dedup + IRecapService call)
├── Services/
│   ├── IMailDeliveryService.cs               ← NEW: interface + RecapDeliveryPayload record
│   ├── MailDeliveryService.cs                ← NEW: MailKit SmtpClient implementation
│   ├── EpubComposer.cs                       ← NEW: static class — highlights → EPUB byte[]
│   ├── HighlightSelectionService.cs          ← NEW: score formula, ranking, tiebreak
│   ├── IRecapService.cs                      ← NEW: interface
│   ├── RecapService.cs                       ← NEW: pipeline: select → compose → retry → update
│   ├── ISchedulerService.cs                  ← NEW: interface
│   └── SchedulerService.cs                   ← NEW: Quartz scheduler wrapper
├── Data/
│   ├── RecapRepository.cs                    ← NEW: recap_jobs CRUD + candidate selection query
│   └── StatusRepository.cs                   ← Updated: reads last recap job for status fields
├── Models/
│   ├── Settings.cs                           ← Updated: +Timezone property
│   └── RecapJobRecord.cs                     ← NEW: domain model for recap_jobs rows
└── Endpoints/
    ├── SettingsEndpoints.cs                  ← Updated: Timezone in GET/PUT + SchedulerService call
    └── StatusEndpoints.cs                    ← Updated: NextRecap from SchedulerService

src/SunnySunday.Core/Contracts/
├── SettingsResponse.cs                       ← Updated: +Timezone
├── UpdateSettingsRequest.cs                  ← Updated: +Timezone
└── StatusResponse.cs                         ← Updated: +LastRecapStatus, +LastRecapError

src/SunnySunday.Tests/
├── Api/
│   ├── SettingsEndpointTests.cs              ← Updated: new timezone test cases
│   └── StatusEndpointTests.cs                ← Updated: NextRecap + last-recap field test cases
└── Recap/                                    ← NEW folder
    ├── HighlightSelectionServiceTests.cs     ← NEW: score ranking, tiebreak, exclusion filtering
    ├── EpubComposerTests.cs                  ← NEW: EPUB structure and highlight content tests
    └── RecapServiceTests.cs                  ← NEW: retry behavior + history update tests
```

## Complexity Tracking

No constitution violations. No complexity justification needed.

---

## Phase 1: Setup

**Purpose**: Add new NuGet packages required by this feature.

- [ ] T000 Add `Quartz.Extensions.Hosting` NuGet package to `src/SunnySunday.Server/SunnySunday.Server.csproj`: `dotnet add src/SunnySunday.Server package Quartz.Extensions.Hosting`. This package includes Quartz core, the `ISchedulerFactory` / `IScheduler` abstractions, and the `IHostedService` integration that starts and stops the scheduler with the application host.
- [ ] T001 [P] Add `MailKit` NuGet package to `src/SunnySunday.Server/SunnySunday.Server.csproj`: `dotnet add src/SunnySunday.Server package MailKit`. Verify solution builds: `dotnet build src/SunnySunday.slnx`.

---

## Phase 2: Schema, Domain Models, and Contract Updates

**Purpose**: Extend the database schema, domain models, and shared API contracts with all data required by this feature. These changes unblock all subsequent phases.

**⚠️ All later phases depend on this phase being complete.**

- [ ] T002 Update `src/SunnySunday.Server/Infrastructure/Database/SchemaBootstrap.cs`. In `SchemaSql`, append the `CREATE TABLE IF NOT EXISTS recap_jobs` DDL (see data-model.md). Add a new private async helper `MigrateAsync(SqliteConnection connection, CancellationToken ct)` that checks `pragma_table_info('settings')` for the `timezone` column and runs `ALTER TABLE settings ADD COLUMN timezone TEXT NOT NULL DEFAULT 'UTC'` only if absent. Call `MigrateAsync` at the end of `ApplyAsync` after the main schema command. Also add synchronous `Migrate(SqliteConnection connection)` called at the end of the synchronous `Apply` method.
- [ ] T003 [P] Update `src/SunnySunday.Server/Models/Settings.cs`: add `public string Timezone { get; set; } = "UTC";`.
- [ ] T004 [P] Create `src/SunnySunday.Server/Models/RecapJobRecord.cs` with properties: `Id`, `UserId`, `ScheduledFor` (DateTimeOffset), `Status` (string, default `"pending"`), `AttemptCount` (int), `ErrorMessage` (string?), `CreatedAt` (DateTimeOffset), `DeliveredAt` (DateTimeOffset?). Namespace `SunnySunday.Server.Models`.
- [ ] T005 [P] Update `src/SunnySunday.Core/Contracts/SettingsResponse.cs`: add `public string Timezone { get; set; } = string.Empty;` with XML doc comment `/// <summary>IANA timezone identifier for the delivery schedule (e.g., "Europe/Rome").</summary>`.
- [ ] T006 [P] Update `src/SunnySunday.Core/Contracts/UpdateSettingsRequest.cs`: add `public string? Timezone { get; set; }` with XML doc comment `/// <summary>IANA timezone identifier. Validated via TimeZoneInfo.FindSystemTimeZoneById.</summary>`.
- [ ] T007 [P] Update `src/SunnySunday.Core/Contracts/StatusResponse.cs`: add `public string? LastRecapStatus { get; set; }` (XML doc: `"delivered" | "failed" | null`) and `public string? LastRecapError { get; set; }` (XML doc: `Error detail when LastRecapStatus is "failed"; null otherwise`). Verify solution builds.

**Checkpoint**: Schema, models, and contracts updated. `dotnet build src/SunnySunday.slnx` passes.

---

## Phase 3: Infrastructure

**Purpose**: SMTP configuration binding, `SettingsRepository` extension, `RecapRepository`, and Quartz DI stub. These are the data-access and configuration foundations that services depend on.

- [ ] T008 Create `src/SunnySunday.Server/Infrastructure/Smtp/SmtpSettings.cs` with properties: `Host` (string, default `"smtp.gmail.com"`), `Port` (int, default `587`), `Username` (string, empty), `Password` (string, empty), `FromAddress` (string, empty), `UseSsl` (bool, default `true`). Namespace `SunnySunday.Server.Infrastructure.Smtp`.
- [ ] T009 [P] Update `src/SunnySunday.Server/Data/SettingsRepository.cs`. In `GetByUserIdAsync`, add `timezone AS Timezone` to the SELECT column list. In `UpsertAsync`, add `timezone = excluded.timezone` to the ON CONFLICT DO UPDATE SET clause and add `@Timezone` to the INSERT VALUES list. The default (`"UTC"`) is handled by the C# model default, not by a NULL check.
- [ ] T010 [P] Create `src/SunnySunday.Server/Data/RecapRepository.cs`. Constructor takes `IDbConnection`. Methods:
  - `SelectCandidatesAsync(int userId)` — executes the candidate selection query (see data-model.md); returns `IReadOnlyList<SelectionCandidate>` where score and ranking are computed in C# by `HighlightSelectionService`, not in SQL. This method returns raw rows before scoring.
  - `CreateJobAsync(int userId, DateTimeOffset scheduledFor)` — INSERT OR IGNORE into `recap_jobs`; returns the row `id`.
  - `GetJobBySlotAsync(int userId, DateTimeOffset scheduledFor)` — SELECT by `(user_id, scheduled_for)`.
  - `UpdateJobDeliveredAsync(int jobId, DateTimeOffset deliveredAt)` — UPDATE `status='delivered'`, `delivered_at`, `attempt_count`.
  - `UpdateJobFailedAsync(int jobId, string errorMessage, int attemptCount)` — UPDATE `status='failed'`, `error_message`, `attempt_count`.
  - `GetLastJobAsync(int userId)` — SELECT the most recent job by `created_at DESC LIMIT 1`; returns `RecapJobRecord?`.
  - `UpdateHighlightSeenAsync(int highlightId, DateTimeOffset seenAt)` — UPDATE `highlights SET last_seen=@seenAt, delivery_count=delivery_count+1 WHERE id=@highlightId`.
  Namespace `SunnySunday.Server.Data`.

**Checkpoint**: Infrastructure complete. `dotnet build src/SunnySunday.slnx` passes.

---

## Phase 4: Highlight Selection (US-2)

**Purpose**: Implement the scoring, ranking, and tiebreak logic for recap candidate selection.

- [ ] T011 Create `src/SunnySunday.Server/Services/HighlightSelectionService.cs`. Constructor takes `RecapRepository`. Method `SelectAsync(int userId, int count)`:
  1. Call `RecapRepository.SelectCandidatesAsync(userId)` to get all eligible rows.
  2. For each row, compute `ageInDays = last_seen.HasValue ? (int)(UtcNow - last_seen.Value).TotalDays : 3650`.
  3. Compute `score = ageInDays + weight`.
  4. Sort descending by `score`, then descending by `created_at` (tiebreak: most recently added first).
  5. Take top `count` items.
  6. Return `IReadOnlyList<SelectionCandidate>`.
  If zero candidates remain after filtering (empty DB or all excluded), return empty list (no recap).
  Namespace `SunnySunday.Server.Services`.
- [ ] T012 Write `src/SunnySunday.Tests/Recap/HighlightSelectionServiceTests.cs`. Test cases:
  1. Highlights with different ages and weights → verify ranking order matches `score = age + weight` (higher score first).
  2. Two highlights with equal score → verify the more recent `created_at` is selected first.
  3. Excluded highlights, excluded books, excluded authors → verify none are returned.
  4. `last_seen = null` → verify these highlights rank above any highlight with a non-null `last_seen`.
  5. `count = 3` with 10 candidates → verify exactly 3 returned.
  6. Empty eligible set → verify empty list returned (no exception).
  Use in-memory SQLite with seeded data via the `TestWebApplicationFactory` infrastructure (or a lightweight in-process `IDbConnection`).

**Checkpoint**: Selection logic tested and verified. `dotnet test src/SunnySunday.slnx --filter "FullyQualifiedName~Recap.HighlightSelectionService"` passes.

---

## Phase 5: EPUB Composition (US-4)

**Purpose**: Implement the static EPUB 2 composer that converts a ranked highlight list into a Kindle-compatible EPUB byte array.

- [ ] T013 Create `src/SunnySunday.Server/Services/EpubComposer.cs`. Static class with one public method: `byte[] Compose(IReadOnlyList<SelectionCandidate> highlights, DateTimeOffset recapDate)`. Produces an EPUB 2 archive in memory using `System.IO.Compression.ZipArchive` over a `MemoryStream`. Files written:
  - `mimetype` — `application/epub+zip` (stored, not compressed; must be first entry)
  - `META-INF/container.xml` — points to `OEBPS/content.opf`
  - `OEBPS/content.opf` — OPF 2.0 package document; title = `"Sunny Sunday Recap — {recapDate:yyyy-MM-dd}"`; unique-identifier based on `recapDate` ticks
  - `OEBPS/toc.ncx` — minimal NCX with single navPoint
  - `OEBPS/highlights.xhtml` — XHTML 1.1; body contains `<ul>` where each `<li>` has `<p class="highlight">"…text…"</p>` and `<p class="source">— {author}, {title}</p>`; no grouping by book
  Namespace `SunnySunday.Server.Services`.
- [ ] T014 Write `src/SunnySunday.Tests/Recap/EpubComposerTests.cs`. Test cases:
  1. Compose from 3 known highlights → output is non-empty byte array; opening as a ZIP reveals `mimetype`, `META-INF/container.xml`, `OEBPS/content.opf`, `OEBPS/toc.ncx`, `OEBPS/highlights.xhtml`.
  2. XHTML content contains all 3 highlight texts and all 3 source labels (author + title).
  3. Highlights are in the same order as the input list (ranking already applied before composition).
  4. Compose from empty list → produces valid EPUB with empty `<ul>` (no exception).
  5. `mimetype` entry is stored (not deflated) — verify `CompressionLevel` is `NoCompression` for that entry.

**Checkpoint**: EPUB composer tested. `dotnet test src/SunnySunday.slnx --filter "FullyQualifiedName~Recap.EpubComposer"` passes.

---

## Phase 6: Email Delivery with Retry (US-3)

**Purpose**: Implement the MailKit SMTP delivery service and the retry loop. The interface decouples the orchestrator from the transport for testability.

- [ ] T015 Create `src/SunnySunday.Server/Services/IMailDeliveryService.cs`. Define:
  ```csharp
  public interface IMailDeliveryService
  {
      Task SendAsync(RecapDeliveryPayload payload, CancellationToken ct = default);
  }

  public sealed record RecapDeliveryPayload(
      string ToAddress,
      string Subject,
      byte[] EpubBytes,
      string EpubFilename);
  ```
  Namespace `SunnySunday.Server.Services`.
- [ ] T016 Create `src/SunnySunday.Server/Services/MailDeliveryService.cs`. Constructor takes `IOptions<SmtpSettings>`. `SendAsync` implementation: build a `MimeMessage` with `From = settings.FromAddress`, `To = payload.ToAddress`, `Subject = payload.Subject`; attach `payload.EpubBytes` as `application/epub+zip` with filename `payload.EpubFilename`. Connect to `settings.Host:settings.Port` using `MailKit.Net.Smtp.SmtpClient`; authenticate; send; disconnect. Throws on SMTP error (caller handles retry). Namespace `SunnySunday.Server.Services`.
- [ ] T017 Write `src/SunnySunday.Tests/Recap/RecapServiceTests.cs`. For retry behavior, create a `FakeMailDeliveryService` that throws `InvalidOperationException` on the first N calls, then succeeds. Test cases:
  1. First attempt succeeds → `UpdateHighlightSeenAsync` called once per highlight; job status = `'delivered'`.
  2. Attempt 1 fails, attempt 2 succeeds → retry fires once; job status = `'delivered'`; history updated.
  3. All 3 attempts fail → job status = `'failed'`; `error_message` set; `UpdateHighlightSeenAsync` never called.
  4. No eligible highlights → no EPUB composed, no SMTP attempted, job not created.
  5. Verify `attempt_count` is incremented correctly on each attempt.
  Use in-memory SQLite. Wire `RecapService` directly (not through HTTP pipeline) for these tests.

**Checkpoint**: Delivery and retry tested. `dotnet test src/SunnySunday.slnx --filter "FullyQualifiedName~Recap.RecapService"` passes.

---

## Phase 7: Recap Pipeline Orchestration (US-1 + US-2 + US-3 + US-4)

**Purpose**: Wire the full pipeline from Quartz trigger through selection, composition, delivery, and history update.

- [ ] T018 Create `src/SunnySunday.Server/Services/IRecapService.cs` and `RecapService.cs`. `RecapService` constructor takes `HighlightSelectionService`, `EpubComposer` (static — injected as a func or called directly), `IMailDeliveryService`, `RecapRepository`, `UserRepository`, `SettingsRepository`. `ExecuteAsync(int userId, DateTimeOffset scheduledFor)` pipeline:
  1. Call `RecapRepository.CreateJobAsync(userId, scheduledFor)` → get `jobId` (INSERT OR IGNORE; if row already existed and is `'delivered'`, retrieve it and return early).
  2. Get `Settings` via `SettingsRepository.GetByUserIdAsync(userId)` — need `Count`.
  3. Get `User` via `UserRepository.EnsureUserAsync()` — need `kindle_email`.
  4. Call `HighlightSelectionService.SelectAsync(userId, settings.Count)`.
  5. If empty → `UpdateJobFailedAsync(jobId, "No eligible highlights.", 0)`, return.
  6. Call `EpubComposer.Compose(candidates, scheduledFor)`.
  7. Retry loop (max 3 attempts, waits: 1 min, 5 min between attempts):
     - Call `IMailDeliveryService.SendAsync(payload, ct)`.
     - On success: call `RecapRepository.UpdateHighlightSeenAsync` for each delivered highlight, call `UpdateJobDeliveredAsync`, return.
     - On failure: increment attempt count; if attempts < 3, `await Task.Delay(backoff, ct)`; else `UpdateJobFailedAsync(jobId, ex.Message, 3)`, log error.
  Namespace `SunnySunday.Server.Services`.
- [ ] T019 Create `src/SunnySunday.Server/Jobs/RecapJob.cs`. Implements Quartz `IJob`. Constructor takes `IRecapService`, `RecapRepository`, `UserRepository`, `ILogger<RecapJob>`. `Execute(IJobExecutionContext context)`:
  1. Extract `scheduledFor = context.ScheduledFireTimeUtc?.ToDateTimeOffset() ?? DateTimeOffset.UtcNow`.
  2. Call `UserRepository.EnsureUserAsync()` → `userId`.
  3. Check `RecapRepository.GetJobBySlotAsync(userId, scheduledFor)` — if row exists with `status='delivered'`, log warning and return (deduplication guard).
  4. Call `IRecapService.ExecuteAsync(userId, scheduledFor, context.CancellationToken)`.
  Namespace `SunnySunday.Server.Jobs`.

---

## Phase 8: Scheduling and Settings/Status Integration (US-1)

**Purpose**: Implement `SchedulerService`, wire it into `PUT /settings` and `GET /status`, and complete the settings timezone handling.

- [ ] T020 Create `src/SunnySunday.Server/Services/ISchedulerService.cs` and `SchedulerService.cs`. Constructor takes `ISchedulerFactory`. `ScheduleAsync(Settings settings)`:
  - Parse `delivery_time` as `HH:mm` → hour, minute.
  - Resolve `TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone)`.
  - Build cron expression: daily = `"0 {minute} {hour} * * ?"`, weekly = `"0 {minute} {hour} ? * {dayOfWeek}"`.
  - Delete existing job with key `"recap-job"` if present.
  - Define `IJobDetail` for `RecapJob`, `ITrigger` with cron + timezone.
  - Schedule via `IScheduler.ScheduleJob`.
  `GetNextFireTimeAsync()`: retrieve trigger by key, return `trigger.GetNextFireTimeUtc()?.ToDateTimeOffset()`.
  Namespace `SunnySunday.Server.Services`.
- [ ] T021 Update `src/SunnySunday.Server/Endpoints/SettingsEndpoints.cs`:
  - In `GET /settings`: map `settings.Timezone` into `SettingsResponse.Timezone`.
  - In `PUT /settings`: validate `Timezone` if provided — call `TimeZoneInfo.FindSystemTimeZoneById`; catch `TimeZoneNotFoundException`; return `Results.ValidationProblem` with error on `timezone` field if invalid.
  - After saving updated settings, call `ISchedulerService.ScheduleAsync(updatedSettings)` to re-register the Quartz trigger.
  - Inject `ISchedulerService` as a parameter in the endpoint handler.
- [ ] T022 Update `src/SunnySunday.Server/Endpoints/StatusEndpoints.cs`: inject `ISchedulerService`; call `GetNextFireTimeAsync()` and set result as `NextRecap` (UTC ISO 8601 string) in the response.
- [ ] T023 Update `src/SunnySunday.Server/Data/StatusRepository.cs`: add call to `RecapRepository.GetLastJobAsync(userId)` and populate `LastRecapStatus` and `LastRecapError` on `StatusResponse` from the last job row.
- [ ] T024 Update `src/SunnySunday.Server/Program.cs`:
  - Add `builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"))`.
  - Add Quartz: `builder.Services.AddQuartz(q => { q.UseMicrosoftDependencyInjectionJobFactory(); })` + `builder.Services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true)`.
  - Register `RecapRepository`, `HighlightSelectionService`, `RecapService`, `MailDeliveryService`, `SchedulerService` as scoped/singleton as appropriate:
    - Quartz `IJob` implementations must be registered as transient: `services.AddTransient<RecapJob>()`.
    - `MailDeliveryService`: transient.
    - `HighlightSelectionService`: scoped.
    - `RecapService`: scoped.
    - `SchedulerService`: singleton (owns the ISchedulerFactory reference).
    - `RecapRepository`: scoped.
  - After `SchemaBootstrap.ApplyAsync`, read settings and call `SchedulerService.ScheduleAsync(settings)` to register the initial Quartz trigger on startup.

**Checkpoint**: Full pipeline wired. Server starts, Quartz logs next fire time. `dotnet build src/SunnySunday.slnx` passes.

---

## Phase 9: Tests and Polish

**Purpose**: Extend existing endpoint tests, verify full test suite, update architecture docs.

- [ ] T025 Extend `src/SunnySunday.Tests/Api/SettingsEndpointTests.cs`:
  1. `PUT /settings` with `timezone = "Europe/Rome"` → subsequent `GET /settings` returns `timezone = "Europe/Rome"`.
  2. `PUT /settings` with `timezone = "invalid/timezone"` → 422 with validation error on `timezone`.
  3. `PUT /settings` with no `timezone` → existing timezone unchanged.
  4. Default settings → `timezone = "UTC"`.
- [ ] T026 Extend `src/SunnySunday.Tests/Api/StatusEndpointTests.cs`:
  1. After startup with default settings → `nextRecap` is non-null and parses as a valid UTC ISO 8601 datetime.
  2. `lastRecapStatus` and `lastRecapError` are null when no recap has run.
  3. Simulate a failed recap job in DB → `lastRecapStatus = "failed"`, `lastRecapError` is non-null.
  4. Simulate a delivered recap job in DB → `lastRecapStatus = "delivered"`, `lastRecapError = null`.
- [ ] T027 Update `docs/ARCHITECTURE.md`:
  - Add recap engine section under "Server (`sunny-server`)" describing: `SchedulerService`, `RecapJob`, `RecapService`, `HighlightSelectionService`, `EpubComposer`, `MailDeliveryService`.
  - Update the data model table to include `recap_jobs` and the `timezone` column.
  - Update the core query section to reflect the production selection SQL (with JOINs to books/authors) and the C# scoring formula.
- [ ] T028 Run full test suite: `dotnet test src/SunnySunday.slnx`. Confirm no regressions in existing infrastructure, parser, and API tests. All new tests pass.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No prerequisites — start immediately
- **Phase 2 (Schema & Models)**: Depends on Phase 1 — **BLOCKS all subsequent phases**
- **Phase 3 (Infrastructure)**: Depends on Phase 2 — unblocks all service phases
- **Phase 4 (Selection)**: Depends on Phase 3 — parallel with Phases 5 and 6
- **Phase 5 (EPUB)**: Depends on Phase 2 (uses `SelectionCandidate`) — parallel with Phases 4 and 6
- **Phase 6 (Delivery)**: Depends on Phase 3 (`IMailDeliveryService`) — parallel with Phases 4 and 5
- **Phase 7 (Orchestration)**: Depends on Phases 4, 5, 6 — wires them together
- **Phase 8 (Scheduling + Settings/Status)**: Depends on Phase 7 — finalizes integration
- **Phase 9 (Tests & Polish)**: Depends on all preceding phases

### Parallel Opportunities

```
Phase 1 ──► Phase 2 ──┬──► Phase 3 ──┬──► Phase 4 (Selection) ──────┐
                      │              ├──► Phase 5 (EPUB) ─────────────┼──► Phase 7 ──► Phase 8 ──► Phase 9
                      │              └──► Phase 6 (Delivery) ─────────┘
                      │
                      └──► T003–T007 [P within Phase 2]
```

Within Phase 2, tasks T003–T007 are all independent file edits and can be parallelized.
