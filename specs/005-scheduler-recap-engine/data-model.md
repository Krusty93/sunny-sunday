# Data Model: Scheduler + Recap Engine

**Feature**: 005-scheduler-recap-engine
**Phase**: 1 — Design
**Date**: 2026-04-27

---

## Schema Changes

### 1. Add `timezone` Column to `settings`

The `settings` table acquires a `timezone` column to store the client's IANA timezone identifier (e.g., `"Europe/Rome"`, `"America/New_York"`). The server uses this value to build the Quartz cron trigger that fires at the correct UTC-equivalent of the user's intended local time, including DST handling.

**Migration** (idempotent — applied in `SchemaBootstrap`):
```sql
-- Applied only if column does not already exist (checked via pragma_table_info)
ALTER TABLE settings ADD COLUMN timezone TEXT NOT NULL DEFAULT 'UTC';
```

**Idempotency check** (in C# before running `ALTER TABLE`):
```sql
SELECT COUNT(*) FROM pragma_table_info('settings') WHERE name = 'timezone'
```
Execute `ALTER TABLE` only if the count is 0.

**Updated `settings` schema**:

| Column | Type | Default | Notes |
|--------|------|---------|-------|
| `user_id` | INTEGER PK | — | FK → `users` |
| `schedule` | TEXT | `'daily'` | `'daily'` or `'weekly'` |
| `delivery_day` | TEXT | NULL | Day name (e.g., `'Monday'`); used when `schedule='weekly'` |
| `delivery_time` | TEXT | `'18:00'` | Client local time in `HH:mm` format |
| `count` | INTEGER | `3` | 1–15 |
| `timezone` | TEXT | `'UTC'` | IANA timezone identifier (**NEW**) |

---

### 2. New `recap_jobs` Table

Tracks one row per scheduled execution slot. Provides deduplication (idempotency anchor for restarts/clock drift), delivery status, and last-recap history exposed via `GET /status`.

```sql
CREATE TABLE IF NOT EXISTS recap_jobs (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id       INTEGER NOT NULL REFERENCES users(id),
    scheduled_for TEXT    NOT NULL,   -- UTC ISO 8601 (slot key, e.g. "2026-04-27T16:00:00Z")
    status        TEXT    NOT NULL DEFAULT 'pending',  -- 'pending' | 'delivered' | 'failed'
    attempt_count INTEGER NOT NULL DEFAULT 0,
    error_message TEXT    NULL,
    created_at    TEXT    NOT NULL,   -- UTC ISO 8601
    delivered_at  TEXT    NULL        -- UTC ISO 8601; NULL until confirmed delivery
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_recap_jobs_user_slot
    ON recap_jobs(user_id, scheduled_for);
```

**Status lifecycle**: `'pending'` (row inserted when job starts) → `'delivered'` (SMTP confirmed) or `'failed'` (all retries exhausted).

---

## Domain Models

### Updated: `Settings.cs`

Location: `src/SunnySunday.Server/Models/Settings.cs`

Add `Timezone` property (default `"UTC"`):

```csharp
public class Settings
{
    public int UserId { get; set; }
    public string Schedule { get; set; } = "daily";
    public string? DeliveryDay { get; set; }
    public string DeliveryTime { get; set; } = "18:00";
    public int Count { get; set; } = 3;
    public string Timezone { get; set; } = "UTC";   // NEW
}
```

### New: `RecapJobRecord.cs`

Location: `src/SunnySunday.Server/Models/RecapJobRecord.cs`

> **Naming note**: Class is `RecapJobRecord` (not `RecapJob`) to avoid collision with the Quartz `IJob` implementation class named `RecapJob`.

```csharp
public class RecapJobRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTimeOffset ScheduledFor { get; set; }
    public string Status { get; set; } = "pending";
    public int AttemptCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}
```

### Internal: `SelectionCandidate`

Not persisted. Used transiently within `HighlightSelectionService` to carry per-highlight computed score before final ranking.

```csharp
internal sealed record SelectionCandidate(
    int Id,
    string Text,
    string BookTitle,
    string AuthorName,
    int Weight,
    DateTimeOffset? LastSeen,
    DateTimeOffset CreatedAt,
    int Score   // age_in_days + weight
);
```

---

## API Contract Changes

All changes are to existing types in `src/SunnySunday.Core/Contracts/`. No new contract files are required.

### Updated: `SettingsResponse`

Add `Timezone` property:

| Property | Type | Notes |
|----------|------|-------|
| `Schedule` | `string` | *(existing)* |
| `DeliveryDay` | `string?` | *(existing)* |
| `DeliveryTime` | `string` | *(existing)* |
| `Count` | `int` | *(existing)* |
| `KindleEmail` | `string` | *(existing)* |
| `Timezone` | `string` | **NEW** — IANA timezone identifier (e.g., `"Europe/Rome"`) |

### Updated: `UpdateSettingsRequest`

Add `Timezone` property:

| Property | Type | Validation | Notes |
|----------|------|------------|-------|
| `Schedule` | `string?` | *(existing)* | |
| `DeliveryDay` | `string?` | *(existing)* | |
| `DeliveryTime` | `string?` | *(existing)* | |
| `Count` | `int?` | *(existing)* | |
| `KindleEmail` | `string?` | *(existing)* | |
| `Timezone` | `string?` | **NEW** — validated via `TimeZoneInfo.FindSystemTimeZoneById`; 422 if unrecognized | |

### Updated: `StatusResponse`

Add last-recap fields to surface actionable failure reasons (FR-005-14, US-08 alignment):

| Property | Type | Notes |
|----------|------|-------|
| `TotalHighlights` | `int` | *(existing)* |
| `TotalBooks` | `int` | *(existing)* |
| `TotalAuthors` | `int` | *(existing)* |
| `ExcludedHighlights` | `int` | *(existing)* |
| `ExcludedBooks` | `int` | *(existing)* |
| `ExcludedAuthors` | `int` | *(existing)* |
| `NextRecap` | `string?` | *(existing)* — UTC ISO 8601; **now populated** by scheduler |
| `LastRecapStatus` | `string?` | **NEW** — `'delivered'` \| `'failed'` \| null |
| `LastRecapError` | `string?` | **NEW** — error detail when `LastRecapStatus='failed'`; null otherwise |

---

## SMTP Configuration (Server-Side Only — Not Persisted in DB)

Located: `src/SunnySunday.Server/Infrastructure/Smtp/SmtpSettings.cs`

Bound via `builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"))`.

```csharp
public sealed class SmtpSettings
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
}
```

**Docker environment variable format**:
```
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__Username=sender@gmail.com
Smtp__Password=app-specific-password
Smtp__FromAddress=sender@gmail.com
Smtp__UseSsl=true
```

---

## Service Interfaces

Located: `src/SunnySunday.Server/Services/`

### `IMailDeliveryService`

Abstracts MailKit SMTP delivery. Injected via DI; substitutable in tests.

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

### `IRecapService`

Orchestrates the full recap pipeline (select → compose → deliver → update history).

```csharp
public interface IRecapService
{
    Task ExecuteAsync(int userId, DateTimeOffset scheduledFor, CancellationToken ct = default);
}
```

### `ISchedulerService`

Manages Quartz.NET job registration and exposes next fire time for `GET /status`.

```csharp
public interface ISchedulerService
{
    Task ScheduleAsync(Settings settings, CancellationToken ct = default);
    Task<DateTimeOffset?> GetNextFireTimeAsync(CancellationToken ct = default);
}
```

---

## Selection Query

The `RecapRepository.SelectCandidatesAsync` query joins highlights with books and authors to retrieve all data needed for recap composition and score computation.

```sql
SELECT
    h.id,
    h.text,
    b.title  AS BookTitle,
    a.name   AS AuthorName,
    h.weight,
    h.last_seen   AS LastSeen,
    h.created_at  AS CreatedAt
FROM highlights h
JOIN books b   ON h.book_id   = b.id
JOIN authors a ON b.author_id = a.id
WHERE h.user_id = @UserId
  AND h.excluded = 0
  AND h.book_id NOT IN (
      SELECT book_id FROM excluded_books WHERE user_id = @UserId)
  AND b.author_id NOT IN (
      SELECT author_id FROM excluded_authors WHERE user_id = @UserId)
```

Score computation and ranking occur in C# (not SQL), so that the `NeverSeenAge = 3650` sentinel for null `last_seen` and the tiebreak on `created_at DESC` are expressed clearly in code and are testable.

---

## Project Structure Additions

```
src/SunnySunday.Server/
├── Program.cs                                ← Updated: Quartz DI, SmtpSettings, new services
├── Infrastructure/
│   ├── Database/
│   │   └── SchemaBootstrap.cs                ← Updated: recap_jobs table + timezone column
│   └── Smtp/
│       └── SmtpSettings.cs                   ← NEW: SMTP configuration POCO
├── Jobs/
│   └── RecapJob.cs                           ← NEW: Quartz IJob — dedup check + call IRecapService
├── Services/
│   ├── IMailDeliveryService.cs               ← NEW: interface + RecapDeliveryPayload
│   ├── MailDeliveryService.cs                ← NEW: MailKit SmtpClient implementation
│   ├── EpubComposer.cs                       ← NEW: static class, highlights → EPUB byte[]
│   ├── HighlightSelectionService.cs          ← NEW: score formula + ranking + tiebreak
│   ├── IRecapService.cs                      ← NEW: interface
│   ├── RecapService.cs                       ← NEW: pipeline orchestration + retry loop
│   ├── ISchedulerService.cs                  ← NEW: interface
│   └── SchedulerService.cs                   ← NEW: Quartz scheduler wrapper
├── Data/
│   ├── RecapRepository.cs                    ← NEW: recap_jobs CRUD + SelectCandidatesAsync
│   └── StatusRepository.cs                   ← Updated: LastRecapStatus + LastRecapError
├── Models/
│   ├── Settings.cs                           ← Updated: +Timezone
│   └── RecapJobRecord.cs                     ← NEW: domain model
└── Endpoints/
    ├── SettingsEndpoints.cs                  ← Updated: Timezone field + SchedulerService call
    └── StatusEndpoints.cs                    ← Updated: NextRecap from SchedulerService

src/SunnySunday.Core/Contracts/
├── SettingsResponse.cs                       ← Updated: +Timezone
├── UpdateSettingsRequest.cs                  ← Updated: +Timezone
└── StatusResponse.cs                         ← Updated: +LastRecapStatus, +LastRecapError

src/SunnySunday.Tests/
├── Api/
│   ├── SettingsEndpointTests.cs              ← Updated: new timezone test cases
│   └── StatusEndpointTests.cs                ← Updated: new NextRecap + last-recap field tests
├── Recap/                                    ← NEW
│   ├── HighlightSelectionServiceTests.cs     ← NEW: score ranking and tiebreak tests
│   ├── EpubComposerTests.cs                  ← NEW: EPUB structure and content tests
│   └── RecapServiceTests.cs                  ← NEW: retry + history update tests
```
