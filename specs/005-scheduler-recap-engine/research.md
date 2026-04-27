# Research: Scheduler + Recap Engine

**Feature**: 005-scheduler-recap-engine
**Phase**: 0 — Research
**Date**: 2026-04-27

---

## Research Tasks & Findings

### 1. Scheduling: Quartz.NET Job Store (In-Memory vs Persistent)

**Decision**: In-memory job store with DB-backed deduplication via the `recap_jobs` table

**Rationale**:
- Quartz.NET's ADO.NET persistent store requires additional schema setup and configuration. For a single recurring daily job in a single-user MVP, the overhead is unjustified.
- On server restart, the Quartz scheduler is re-initialized at startup by reading `settings` from SQLite and re-registering the recap trigger. This is simple and reliable.
- The "duplicate trigger after restart/clock drift" edge case (spec) is handled by the `recap_jobs` table: before executing, the `RecapJob` (Quartz `IJob`) checks for an existing `status = 'delivered'` row for the current slot key (`user_id + scheduled_for`). If found, it exits immediately.
- A unique index on `(user_id, scheduled_for)` in `recap_jobs` prevents concurrent double-insertion even under pathological conditions.
- `Quartz.Extensions.Hosting` provides a `IHostedService` integration that starts the scheduler on `IHost` startup and shuts it down cleanly.

**Quartz cron schedule**:
- Daily: `0 {minute} {hour} * * ?`  (Quartz uses 6-field cron: seconds, minutes, hours, day-of-month, month, day-of-week)
- Weekly: `0 {minute} {hour} ? * {dayOfWeek}` — day-of-week derived from `settings.delivery_day`

**Alternatives considered**:
- **ADO.NET persistent store**: survives restarts without re-registration but requires Quartz schema tables in the SQLite DB, more complex initialization, and more NuGet packages. Overkill for one job.
- **Hangfire**: full-featured background job system with dashboard. Too heavy for a single recurring recap job.
- **`System.Threading.PeriodicTimer`**: simple but doesn't support timezone-aware cron semantics natively; implementing DST-correct scheduling manually would add significant complexity.

---

### 2. EPUB Generation: Manual ZIP Construction vs Library

**Decision**: Manual EPUB 2 construction using `System.IO.Compression.ZipArchive` — no new NuGet package

**Rationale**:
- An EPUB file is a ZIP archive with a defined directory structure. For a single-chapter flat-list document (the recap), the total template is under 200 lines across four files.
- The .NET BCL `System.IO.Compression.ZipArchive` covers all required ZIP operations without any external dependency.
- All reviewed NuGet EPUB writer libraries (EpubSharp, VersOne.Epub, EpubCore) are primarily reader-focused. Their writer APIs require learning a new object model for a document that is simpler to produce from a template string.
- The manual approach gives full control over the output, deterministic byte content (important for tests), zero new dependencies, and aligns with constitution principle VI (YAGNI).
- EPUB 2 (not EPUB 3) is used for maximum Kindle compatibility across all device generations.

**EPUB 2 structure produced**:
```
mimetype
META-INF/container.xml
OEBPS/content.opf        ← package document: metadata, manifest, spine
OEBPS/toc.ncx            ← navigation control (required by EPUB 2)
OEBPS/highlights.xhtml   ← single content file: flat list of highlights
```

Each highlight item in `highlights.xhtml` renders as:
```html
<p class="highlight">"…text…"</p>
<p class="source">— Author Name, Book Title</p>
```

**Alternatives considered**:
- **EpubSharp**: last meaningful commit 2019; not actively maintained; writer support is incomplete.
- **VersOne.Epub**: solid reader, writer support was added later but is less tested; adds ~300 KB dependency.
- **EPUB 3**: better semantics but redundant for Kindle which reads EPUB 2 natively on all generations; EPUB 3 adds a `nav.xhtml` navigation document requirement with no practical benefit here.

---

### 3. SMTP Delivery: MailKit Configuration

**Decision**: MailKit `SmtpClient` with configuration from `appsettings.json` / environment variables via `IOptions<SmtpSettings>`

**Rationale**:
- MailKit is explicitly listed in the project stack (`docs/ARCHITECTURE.md`). It provides full SMTP support including STARTTLS, OAUTH2, and modern authentication — significantly more reliable than the deprecated .NET BCL `SmtpClient`.
- SMTP credentials (host, port, username, password, from address) are **server-side operational configuration**, not user preferences. They must **not** be stored in the SQLite DB. They belong in `appsettings.json` with environment variable overrides.
- The Kindle recipient email (`users.kindle_email`) is stored in SQLite — it is user data, not server config.
- `IOptions<SmtpSettings>` is the standard .NET options pattern; environment variables override with double-underscore separator (`Smtp__Host`) — standard Docker-compose pattern.
- `IMailDeliveryService` abstracts MailKit for testability. Tests inject a fake implementation; production registers `MailDeliveryService`.

**SmtpSettings configuration keys** (in `appsettings.json` / env vars):
```
Smtp:Host         (default: "smtp.gmail.com")
Smtp:Port         (default: 587)
Smtp:Username     (required)
Smtp:Password     (required — use env var in production, never hardcode)
Smtp:FromAddress  (required)
Smtp:UseSsl       (default: true)
```

**Alternatives considered**:
- **.NET BCL `SmtpClient`**: deprecated since .NET Core 2.0; lacks modern auth support; documented as not recommended by Microsoft.
- **SendGrid / Mailgun SDK**: external cloud services — violates constitution principle IV (local processing only, no third-party data transmission).

---

### 4. Retry Policy: Polly vs Manual Loop

**Decision**: Manual retry loop with `await Task.Delay(...)` — no new NuGet package

**Rationale**:
- The retry spec is fixed and simple: 3 total attempts, wait 1 minute between attempt 1 and 2, wait 5 minutes between attempt 2 and 3.
- A manual loop with `Task.Delay` is approximately 20 lines and carries no library dependency.
- Polly (or `Microsoft.Extensions.Resilience`) is appropriate when retry rules are configurable, need circuit-breaking, or must be shared across many call sites. None apply here.
- Constitution principle VI: do not add a library for something achievable in 20 lines.

**Retry schedule**:
1. Attempt 1 — immediate
2. Wait 1 minute (`TimeSpan.FromMinutes(1)`)
3. Attempt 2
4. Wait 5 minutes (`TimeSpan.FromMinutes(5)`)
5. Attempt 3
6. All attempts exhausted → log error, leave `recap_jobs` status as `'failed'`, leave `last_seen`/`delivery_count` unchanged

**Success**: At any attempt, on confirmed SMTP success → set `recap_jobs.status = 'delivered'`, update `last_seen` and `delivery_count` on each delivered highlight.

**Alternatives considered**:
- **Polly / `Microsoft.Extensions.Resilience`**: excellent libraries, but unjustified for a fixed 3-attempt sequential retry with known delays.
- **Quartz retry trigger**: reschedule the failed job as a new Quartz trigger. Unnecessarily complex; blurs the line between scheduling and delivery retry.

---

### 5. Score Formula: Age Unit

**Decision**: Age measured in **whole days** (floor of elapsed time since `last_seen`)

**Rationale**:
- The spec defines `score = antiquity (time since last_seen) + weight`.
- `weight` ranges 1–5. If age is measured in hours: a 24-hour-old highlight has age 24, and weight contributes only ~4–17% of the score — essentially irrelevant.
- With age in days: a 1-day-old highlight scores between `1 + 1 = 2` and `1 + 5 = 6`. Weight contributes 14–83% in the first week, making the weight setting genuinely useful.
- `last_seen = null` is treated as a "never seen" age of **3,650 days** (10 years). This ensures never-seen highlights always rank above any seen highlight regardless of weight.
- Tiebreak (equal score): `created_at DESC` — the most recently **added** highlight wins.

**Formula**:
```csharp
const int NeverSeenAge = 3650;
var ageInDays = highlight.LastSeen.HasValue
    ? (int)(now - highlight.LastSeen.Value).TotalDays
    : NeverSeenAge;
var score = ageInDays + highlight.Weight;
```

**Alternatives considered**:
- **Hours**: score dominated by age alone; weight setting becomes practically meaningless.
- **Fractional days**: adds floating-point complexity; whole-day granularity is sufficient for a daily schedule.
- **Configurable unit**: YAGNI — the formula is clearly specified and can be revisited if needed.

---

### 6. Timezone Handling

**Decision**: Store `timezone` (IANA identifier) alongside `delivery_time` (HH:mm) in `settings`; Quartz fires in the server's UTC using a timezone-aware trigger; all DB timestamps stored as UTC ISO 8601

**Rationale**:
- Storing only a UTC offset would break during Daylight Saving Time transitions: the stored offset would drift from the user's intended local time. IANA identifiers (e.g., `"Europe/Rome"`) correctly represent DST-aware zones.
- `TimeZoneInfo.FindSystemTimeZoneById(ianaId)` works on .NET 10 on Linux/macOS (IANA natively) and Windows (via `TimeZoneConverter` or .NET 6+ built-in IANA support on Windows). Since the server runs on Linux (Docker), IANA works natively.
- The server stores `delivery_time` as `HH:mm` in the user's local time and `timezone` as IANA string. At scheduling time, these two values are combined with `TimeZoneInfo` to build the Quartz trigger in the correct UTC-equivalent cron expression.
- The client sends `timezone` via `PUT /settings`; the server validates it with `TimeZoneInfo.FindSystemTimeZoneById`.
- All `DateTimeOffset` values in the DB are stored as UTC ISO 8601 strings (consistent with existing schema).

**Quartz timezone configuration**:
```csharp
var tz = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone); // e.g., "Europe/Rome"
var trigger = TriggerBuilder.Create()
    .WithCronSchedule($"0 {minute} {hour} * * ?",
        x => x.InTimeZone(tz))
    .Build();
```

**FR alignment**: FR-005-16 ("persist UTC values") is satisfied by persisting all execution timestamps (`scheduled_for`, `delivered_at`) as UTC in `recap_jobs`. The user's schedule preference itself is stored as a local-time intent (`delivery_time` + `timezone`), which is the canonical representation.

**Alternatives considered**:
- **Convert to UTC offset and store**: breaks on DST change; user's intended "18:00" becomes wrong time.
- **Store UTC HH:mm**: same problem — UTC HH:mm drifts from local intent across DST.
- **Client sends full UTC datetime**: requires the client to know the server's scheduling logic; not appropriate.

---

### 7. recap_jobs Table Design

**Decision**: Dedicated `recap_jobs` table with unique index on `(user_id, scheduled_for)` for idempotency

**Rationale**:
- Provides the deduplication anchor for "duplicate trigger after restart or clock drift" (spec edge case).
- Acts as the recap history store for `GET /status` (`LastRecapStatus`, `LastRecapError`, `NextRecap` derivation).
- Pre-flight check: before executing, `RecapJob` queries for `status = 'delivered'` on the current slot. If found, it skips execution. If a `'pending'` row exists (previous run crashed mid-execution), it proceeds (idempotent re-execution is safe because history updates only occur post-delivery).
- A unique index on `(user_id, scheduled_for)` prevents double-insertion.
- Minimal column set: `id`, `user_id`, `scheduled_for`, `status`, `attempt_count`, `error_message`, `created_at`, `delivered_at`.

**Alternatives considered**:
- **In-memory set of executed slots**: lost on restart — does not solve the restart race condition.
- **Quartz persistent store misfire handling**: handles missed triggers but doesn't provide a clean user-visible history; adds complexity.

---

### 8. Testing Strategy

**Decision**: Unit tests for selection service and EPUB composer; `WebApplicationFactory`-based integration tests for settings/status endpoint changes; `IMailDeliveryService` interface for SMTP substitution in tests

**Rationale**:
- **Selection**: deterministic algorithm + DB interaction → integration test using in-memory SQLite (same `TestWebApplicationFactory` pattern as feature 004); seed known data, verify score ranking and tiebreak.
- **EPUB**: pure function (highlights → byte[]) → unit tests; verify ZIP structure, required EPUB files, and highlight/source text content.
- **Delivery + retry**: `IMailDeliveryService` interface allows injecting a fake that throws on the first N calls, then succeeds; verify attempt count and history update behavior.
- **Scheduler**: verify `GetNextFireTimeAsync` returns a value close to the expected UTC slot after `ScheduleAsync` is called; avoid testing actual Quartz clock firing (brittle).
- **Settings/Status endpoints**: extend existing `SettingsEndpointTests` / `StatusEndpointTests` in `SunnySunday.Tests` with new test cases for `timezone` and `LastRecapStatus`/`LastRecapError` fields.

**Alternatives considered**:
- **Testing actual Quartz firing**: requires wall-clock waiting or virtual-time injection; too brittle for CI.
- **MailKit test server**: MailKit provides a `MockSmtpClient` in its test utilities, but the `IMailDeliveryService` abstraction is simpler and sufficient.
