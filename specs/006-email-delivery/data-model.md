# Data Model: Email Delivery Management

**Feature**: 006-email-delivery
**Phase**: 1 — Design
**Date**: 2026-04-30

---

## Schema Changes

**None.** This feature does not introduce new tables or modify existing schema. All data access uses existing tables and columns from features 004 and 005:

- `users.kindle_email` — already stores the Kindle email address (set via `PUT /settings`)
- `recap_jobs` — already stores delivery history with all fields needed for the delivery log
- `settings` — read for validation context; no changes needed

---

## Domain Models

### Existing Models (no changes)

#### `RecapJobRecord` (`src/SunnySunday.Server/Models/RecapJobRecord.cs`)

Already contains all fields needed for the delivery history endpoint:

| Property | Type | Maps to |
|----------|------|---------|
| `Id` | `int` | `recap_jobs.id` |
| `UserId` | `int` | `recap_jobs.user_id` |
| `ScheduledFor` | `DateTimeOffset` | `recap_jobs.scheduled_for` |
| `Status` | `string` | `recap_jobs.status` (`pending`, `delivered`, `failed`) |
| `AttemptCount` | `int` | `recap_jobs.attempt_count` |
| `ErrorMessage` | `string?` | `recap_jobs.error_message` |
| `CreatedAt` | `DateTimeOffset` | `recap_jobs.created_at` |
| `DeliveredAt` | `DateTimeOffset?` | `recap_jobs.delivered_at` |

#### `SmtpSettings` (`src/SunnySunday.Server/Infrastructure/Smtp/SmtpSettings.cs`)

Already contains all SMTP configuration fields:

| Property | Type | Default |
|----------|------|---------|
| `Host` | `string` | `"smtp.gmail.com"` |
| `Port` | `int` | `587` |
| `Username` | `string` | `string.Empty` |
| `Password` | `string` | `string.Empty` |
| `FromAddress` | `string` | `string.Empty` |

---

## New Services

### `ISmtpReadinessService` / `SmtpReadinessService`

Stateless singleton that inspects `IOptions<SmtpSettings>` and reports configuration completeness.

```csharp
public interface ISmtpReadinessService
{
    bool IsReady { get; }
    IReadOnlyList<string> MissingFields { get; }
}
```

**Logic**: Check that `Host`, `Username`, `Password`, and `FromAddress` are all non-null and non-whitespace. `Port` is always valid (has a default). Report each missing field by name.

---

## New Contract DTOs

### `TestDeliveryResponse` (`src/SunnySunday.Core/Contracts/`)

```csharp
public sealed record TestDeliveryResponse
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}
```

### `DeliveryRecord` (`src/SunnySunday.Core/Contracts/`)

Read projection of a `recap_jobs` row for the delivery history. SMTP credentials and internal IDs are excluded.

```csharp
public sealed record DeliveryRecord
{
    public string ScheduledFor { get; init; } = string.Empty;  // ISO 8601
    public string Status { get; init; } = string.Empty;        // "pending" | "delivered" | "failed"
    public int AttemptCount { get; init; }
    public string? ErrorMessage { get; init; }
    public string? DeliveredAt { get; init; }                   // ISO 8601, null if not delivered
}
```

### `DeliveryResponse` (`src/SunnySunday.Core/Contracts/`)

Paginated wrapper for delivery history.

```csharp
public sealed record DeliveryResponse
{
    public IReadOnlyList<DeliveryRecord> Items { get; init; } = [];
    public int Total { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; }
}
```

---

## Updated Contract DTOs

### `StatusResponse` — add `SmtpReady`

```csharp
// Add to existing StatusResponse:
public bool SmtpReady { get; set; }
```

This boolean is populated by `StatusEndpoints` from `ISmtpReadinessService.IsReady`. No credentials are exposed.

---

## Data Access Changes

### `RecapRepository` — add paginated delivery query

New method for delivery history:

```csharp
public async Task<(IReadOnlyList<RecapJobRecord> Items, int Total)> GetDeliveriesAsync(
    int userId, int offset, int limit)
{
    var total = await connection.QuerySingleAsync<int>(
        "SELECT COUNT(*) FROM recap_jobs WHERE user_id = @UserId",
        new { UserId = userId });

    var items = await connection.QueryAsync<RecapJobRecord>(
        """
        SELECT id AS Id, user_id AS UserId, scheduled_for AS ScheduledFor,
               status AS Status, attempt_count AS AttemptCount,
               error_message AS ErrorMessage, created_at AS CreatedAt,
               delivered_at AS DeliveredAt
        FROM recap_jobs
        WHERE user_id = @UserId
        ORDER BY scheduled_for DESC
        LIMIT @Limit OFFSET @Offset
        """,
        new { UserId = userId, Limit = limit, Offset = offset });

    return (items.ToList(), total);
}
```

---

## Validation Changes

### `SettingsEndpoints` — Kindle email domain validation

Tighten the existing `IsValidEmail` check for the `kindleEmail` field to require `@kindle.com` or `@free.kindle.com` domain suffix:

```csharp
private static bool IsValidKindleEmail(string value, out string normalized)
{
    normalized = value.Trim().ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(normalized))
        return false;

    if (!EmailRegex().IsMatch(normalized))
        return false;

    return normalized.EndsWith("@kindle.com", StringComparison.Ordinal)
        || normalized.EndsWith("@free.kindle.com", StringComparison.Ordinal);
}
```

The existing generic `IsValidEmail` call for `kindleEmail` is replaced with `IsValidKindleEmail`. The 422 error message is updated to: `"Kindle email must end with @kindle.com or @free.kindle.com."`.

---

## Entity Relationships

```
SmtpSettings (config)          Users (DB)
    │                              │
    ▼                              ▼
SmtpReadinessService        kindle_email field
    │                              │
    ├── GET /status (smtpReady)    ├── PUT /settings (validation)
    └── POST /test-delivery        └── POST /test-delivery
              │                              │
              ▼                              ▼
        EpubComposer ──► MailDeliveryService ──► SMTP
              │
        (test highlights, no DB writes)

RecapJobRecord (DB: recap_jobs)
    │
    └── GET /deliveries (paginated read)
```

---

## State Transitions

### Test Delivery Flow

```
Request received
    → Check SMTP ready (SmtpReadinessService)
        → NOT READY: return 422 { success: false, error: "SMTP not configured: missing [fields]" }
    → Check Kindle email set (UserRepository)
        → NOT SET: return 422 { success: false, error: "Kindle email not configured" }
    → Compose test EPUB (EpubComposer with synthetic highlights)
    → Send via MailDeliveryService (with Polly retry)
        → SUCCESS: return 200 { success: true }
        → SMTP ERROR: return 200 { success: false, error: "[actionable message]" }
```

No database state is modified during test delivery.
