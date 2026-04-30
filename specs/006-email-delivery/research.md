# Research: Email Delivery Management

**Feature**: 006-email-delivery
**Phase**: 0 — Research
**Date**: 2026-04-30

---

## Research Tasks & Findings

### 1. Kindle Email Validation: Domain Allowlist Approach

**Decision**: Validate Kindle email addresses using a domain suffix allowlist (`@kindle.com`, `@free.kindle.com`) applied after normalization (trim + lowercase).

**Rationale**:
- Amazon's Send-to-Kindle service only accepts delivery to addresses ending in `@kindle.com` or `@free.kindle.com`. Any other domain will silently fail or bounce.
- The existing `IsValidEmail` method in `SettingsEndpoints.cs` uses a general-purpose RFC 5322 regex. For the `kindleEmail` field specifically, we need a tighter check that also verifies the domain suffix.
- Normalization (trim whitespace + lowercase) prevents common user input errors. Email local parts are technically case-sensitive per RFC 5321 §2.4, but Amazon treats them case-insensitively, so lowercasing is safe.
- Validation must happen at the REST boundary (`PUT /settings`) to prevent invalid addresses from being stored. The CLI is not responsible for validation — it forwards input to the server.

**Implementation approach**:
- Replace the generic `IsValidEmail` call for `kindleEmail` in `SettingsEndpoints.cs` with a new `IsValidKindleEmail` method.
- The new method: (1) trims + lowercases, (2) checks the result matches the existing RFC regex, (3) checks that the domain part ends with `kindle.com` or `free.kindle.com`.
- The 422 error message explicitly names the accepted domains so the user knows exactly what format is required (FR-006-02).

**Alternatives considered**:
- **Regex-only validation**: A single regex combining RFC format + Kindle domain would be fragile and hard to maintain. Two-step validation (format then domain) is clearer.
- **MX record lookup**: Would validate that the domain can receive email, but adds network I/O to a synchronous settings update — too slow and brittle for a validation step. Also would not work in offline Docker setups.
- **Validation in CLI**: Would duplicate logic and could drift from server validation. Server-side only is canonical.

---

### 2. SMTP Readiness: Settings Completeness Check

**Decision**: Implement a stateless `SmtpReadinessService` that inspects `IOptions<SmtpSettings>` and reports whether all required fields are present and non-empty.

**Rationale**:
- The existing `SmtpSettings` POCO has defaults (e.g., `Host = "smtp.gmail.com"`, `Port = 587`) but `Username`, `Password`, and `FromAddress` default to `string.Empty`. The service checks all five fields for non-empty values.
- A stateless check on `IOptions<SmtpSettings>` is sufficient because SMTP config comes from environment variables / `appsettings.json` and doesn't change at runtime.
- The readiness result is consumed in two places: `GET /status` (adds `smtpReady` boolean) and at server startup (logs a warning if not ready, per FR-006-04).
- The service also reports *which* fields are missing — needed for the CLI `sunny delivery status` command (FR-006-16) and for the test delivery pre-condition check (FR-006-09).

**Implementation approach**:
- `ISmtpReadinessService` with two members: `bool IsReady` property and `IReadOnlyList<string> MissingFields` property.
- Registered as singleton (settings are read once from config; `IOptionsMonitor` not needed for MVP).
- `StatusEndpoints` injects `ISmtpReadinessService` and maps `IsReady` → `StatusResponse.SmtpReady`.
- At startup in `Program.cs`, resolve the service and log a warning if `!IsReady`.

**Alternatives considered**:
- **Extension method on SmtpSettings**: Simpler but not injectable, making it harder to test and mock. A service with an interface is more consistent with the existing DI pattern.
- **Health check endpoint**: ASP.NET `IHealthCheck` is a good pattern but adds complexity (health check middleware, separate endpoint path). A field on the existing `/status` response is simpler and more discoverable for CLI consumers.
- **Connection probe at startup**: Actually attempting an SMTP connection would catch credential errors early, but would fail in environments where SMTP is only reachable at delivery time (e.g., Docker networks). A field-presence check is reliable and fast.

---

### 3. Test Delivery: Endpoint Design & Isolation

**Decision**: `POST /test-delivery` generates a minimal test EPUB (using `EpubComposer` with synthetic highlights), sends it via the existing `IMailDeliveryService` + Polly retry, and returns a success/failure result. No database writes, no recap state changes.

**Rationale**:
- The spec requires test delivery to NOT modify recap history, highlight selection state, `last_seen`, or `delivery_count` (FR-006-07). This means we must not call `RecapService` (which updates highlight state). Instead, the endpoint directly composes a test EPUB and sends it.
- Using `EpubComposer.Compose()` with a small set of hard-coded test highlights produces a valid EPUB that exercises the full SMTP pipeline without touching the database.
- The Polly retry policy (`RecapDeliveryPolicy.Create`) should be reused for consistency — if SMTP is flaky, the test delivery should retry the same way as real deliveries.
- The endpoint must check two pre-conditions before attempting send: (1) SMTP is configured (via `SmtpReadinessService`), (2) Kindle email is set (via `UserRepository`). Both return 422 with actionable messages.

**Implementation approach**:
- New `TestDeliveryEndpoints.cs` with a single `POST /test-delivery` route.
- Pre-condition checks: SMTP ready → Kindle email set → compose test EPUB → send via `IMailDeliveryService` → return result.
- The test EPUB contains 2-3 synthetic highlights with placeholder text (e.g., "This is a test highlight from Sunny Sunday") to verify the full pipeline.
- Response DTO: `TestDeliveryResponse { bool Success, string? Error }`.
- On `MailKit.Net.Smtp.SmtpCommandException` or `SmtpProtocolException`, catch and return the error message in the response (not a 500 — delivery failures are expected outcomes).
- Catch-all for unexpected exceptions returns a generic "delivery failed" message without leaking internals.

**Alternatives considered**:
- **Reuse RecapService with a "dry-run" flag**: Would require modifying RecapService's contract and adding conditional logic. Violates separation of concerns and adds risk to the production delivery path.
- **Send a plain-text email instead of EPUB**: Would not exercise the same code path as real delivery (EPUB attachment). A test should be as close to production as possible.
- **Store test delivery results in recap_jobs**: Spec explicitly forbids affecting recap history. A separate `test_deliveries` table would be over-engineering for a diagnostic tool.

---

### 4. Delivery History: Pagination Pattern

**Decision**: Use offset/limit pagination with a `total` count, consistent with the existing REST patterns in the project.

**Rationale**:
- The spec requires `GET /deliveries?offset=0&limit=20` with a `total` count (FR-006-10, FR-006-11).
- Offset/limit is the simplest pagination model and is appropriate for the scale (single user, up to thousands of recap jobs over years of use).
- SQLite handles `LIMIT ... OFFSET ...` efficiently. For the expected data volumes (1-2 records per day × years = low thousands), cursor-based pagination provides no meaningful advantage.
- The `total` count is computed via a separate `SELECT COUNT(*)` in the same query round-trip.

**Implementation approach**:
- Add `GetDeliveriesAsync(int userId, int offset, int limit)` to `RecapRepository` returning `(IReadOnlyList<RecapJobRecord> Items, int Total)`.
- Query: `SELECT ... FROM recap_jobs WHERE user_id = @UserId ORDER BY scheduled_for DESC LIMIT @Limit OFFSET @Offset`.
- Count: `SELECT COUNT(*) FROM recap_jobs WHERE user_id = @UserId`.
- New endpoint `GET /deliveries` with query params `offset` (default 0), `limit` (default 20, max 100).
- Response DTO: `DeliveryResponse { IReadOnlyList<DeliveryRecord> Items, int Total, int Offset, int Limit }`.
- `DeliveryRecord` maps from `RecapJobRecord`: `ScheduledFor`, `Status`, `AttemptCount`, `ErrorMessage`, `DeliveredAt`.

**Alternatives considered**:
- **Cursor-based pagination**: More efficient for large datasets but adds complexity (encoding cursor, handling edge cases). Unnecessary for single-user MVP.
- **Combined query with CTE**: `WITH cte AS (...) SELECT *, COUNT(*) OVER() ...` would be a single query, but SQLite's window function support with Dapper requires extra handling. Two simple queries are clearer.
- **No pagination**: Spec explicitly requires it (FR-006-11). Even at small scale, it's good practice for the CLI table rendering.

---

### 5. CLI Commands: Spectre.Console Integration

**Decision**: Add a `delivery` command group to the existing CLI with three subcommands: `test`, `log`, `status`. Use `System.Net.Http.HttpClient` for API calls and Spectre.Console `Table` for rendering.

**Rationale**:
- The CLI (`sunny`) currently has minimal structure (just a `Program.cs` printing a message). Feature 006 is the first to add real CLI commands.
- Spectre.Console is already a project dependency (per constitution). Its `Table` class provides rich terminal rendering with borders, colors, and alignment.
- The CLI calls REST endpoints on the server — no direct database access (constitution principle I).
- `HttpClient` is sufficient for synchronous CLI calls. No need for `IHttpClientFactory` in a short-lived CLI process.

**Implementation approach**:
- `sunny delivery test` → `POST /test-delivery` → display success/error with `AnsiConsole.MarkupLine`.
- `sunny delivery log [--page N]` → `GET /deliveries?offset=((N-1)*20)&limit=20` → render `Spectre.Console.Table` with columns: Date, Status, Attempts, Error, Delivered At.
- `sunny delivery status` → `GET /status` → display SMTP readiness, from-address (not credentials), Kindle email status.
- Server base URL configurable via `SUNNY_SERVER_URL` environment variable (default: `http://localhost:5000`).
- For the MVP, commands use top-level statements / simple branching. A full command framework (e.g., `System.CommandLine`, `Spectre.Console.Cli`) can be adopted later if the CLI grows.

**Alternatives considered**:
- **Spectre.Console.Cli (CommandApp)**: Full command-line parsing framework built into Spectre.Console. Provides a proper command tree, argument parsing, and help generation. Would be the right choice long-term, but for three subcommands in MVP, simple `args` parsing is sufficient and avoids learning the `CommandApp` API.
- **System.CommandLine**: Microsoft's CLI framework. More powerful but also more verbose. Not needed at this scale.
- **Direct database access from CLI**: Violates constitution principle I (client/server separation). CLI must go through REST.

---

### 6. SMTP Credential Security in Responses

**Decision**: All API responses and CLI output explicitly exclude SMTP credentials (Username, Password). Only non-sensitive fields (Host, Port, FromAddress) may appear in diagnostic output.

**Rationale**:
- FR-006-13 and FR-006-16 require that credentials never appear in responses or CLI output.
- The `SmtpReadinessService` computes readiness from all fields but only exposes field names (not values) in the missing-fields list.
- The `StatusResponse.SmtpReady` field is a boolean — no credential data.
- The CLI `sunny delivery status` displays Host, Port, and FromAddress for diagnostic purposes, but never Username or Password.
- The `GET /deliveries` endpoint returns only `RecapJobRecord` fields — no infrastructure details.

**Alternatives considered**: None needed — this is a security constraint, not a design choice. Implemented consistently across all response DTOs and CLI output.
