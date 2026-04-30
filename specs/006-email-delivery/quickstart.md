# Quickstart: Email Delivery Management

**Feature**: 006-email-delivery
**Date**: 2026-04-30

---

## Prerequisites

- .NET 10 SDK installed (`dotnet --version` → `10.x`)
- Repository cloned and on branch `006-email-delivery` (or `task/TXXX-*` task branch)
- Solution builds cleanly from `main`: `dotnet build src/SunnySunday.slnx`
- Features 004 (REST API) and 005 (Scheduler + Recap Engine) are merged
- A configured SMTP account for manual end-to-end testing (optional — unit/integration tests mock SMTP)

---

## New Dependencies

**None.** All required packages are already installed from previous features:
- `MailKit` — SMTP delivery
- `Polly` — retry policy
- `Spectre.Console` — CLI rendering
- `Dapper` + `Microsoft.Data.Sqlite` — data access

---

## Build and Run

### Build the solution

```bash
dotnet build src/SunnySunday.slnx
```

### Run the server (local development)

```bash
dotnet run --project src/SunnySunday.Server
```

Server starts at `http://localhost:5000` by default.

### Run the CLI

```bash
dotnet run --project src/SunnySunday.Cli -- delivery test
dotnet run --project src/SunnySunday.Cli -- delivery log
dotnet run --project src/SunnySunday.Cli -- delivery status
```

---

## SMTP Configuration (for end-to-end testing)

SMTP settings are configured via environment variables (same as feature 005):

```bash
export SMTP_HOST=smtp.gmail.com
export SMTP_PORT=587
export SMTP_USER=your-email@gmail.com
export SMTP_PASSWORD=your-app-specific-password
export SMTP_FROM_ADDRESS=your-email@gmail.com
```

Or via `src/SunnySunday.Server/appsettings.Development.json`:

```json
{
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-specific-password",
    "FromAddress": "your-email@gmail.com"
  }
}
```

> **Note**: Never commit real credentials to source control.

---

## Run Tests

```bash
dotnet test src/SunnySunday.Tests
```

All tests use the existing `SunnyTestApplicationFactory` with in-memory SQLite. SMTP is not required for tests — `IMailDeliveryService` is replaced with a test double in the test factory.

### Key test files for this feature

| File | Covers |
|------|--------|
| `Tests/Api/SettingsEndpointTests.cs` | Kindle email validation (new test cases) |
| `Tests/Api/TestDeliveryEndpointTests.cs` | POST /test-delivery (new file) |
| `Tests/Api/DeliveryEndpointTests.cs` | GET /deliveries pagination (new file) |
| `Tests/Api/StatusEndpointTests.cs` | smtpReady field in GET /status (new test cases) |
| `Tests/Infrastructure/SmtpReadinessServiceTests.cs` | SMTP settings completeness (new file) |

---

## Manual Testing Guide

### 1. Verify SMTP readiness check

```bash
# Without SMTP configured
curl http://localhost:5000/status | jq '.smtpReady'
# → false

# With SMTP configured (set env vars, restart server)
curl http://localhost:5000/status | jq '.smtpReady'
# → true
```

### 2. Test Kindle email validation

```bash
# Valid Kindle email
curl -X PUT http://localhost:5000/settings \
  -H "Content-Type: application/json" \
  -d '{"kindleEmail": "user@kindle.com"}'
# → 200 OK

# Invalid email domain
curl -X PUT http://localhost:5000/settings \
  -H "Content-Type: application/json" \
  -d '{"kindleEmail": "user@gmail.com"}'
# → 422 with error: "Kindle email must end with @kindle.com or @free.kindle.com."
```

### 3. Test delivery (requires SMTP + Kindle email configured)

```bash
curl -X POST http://localhost:5000/test-delivery
# → 200 { "success": true }  or  200 { "success": false, "error": "..." }
```

### 4. Delivery history

```bash
# First page (default)
curl http://localhost:5000/deliveries
# → { "items": [...], "total": N, "offset": 0, "limit": 20 }

# Second page
curl "http://localhost:5000/deliveries?offset=20&limit=20"
```

---

## CLI Commands Reference

| Command | Description | API Call |
|---------|-------------|----------|
| `sunny delivery test` | Send test EPUB to Kindle | `POST /test-delivery` |
| `sunny delivery log [--page N]` | Show delivery history table | `GET /deliveries?offset=...&limit=20` |
| `sunny delivery status` | Show SMTP readiness + config | `GET /status` |

The CLI reads the server URL from the `SUNNY_SERVER_URL` environment variable (default: `http://localhost:5000`).

---

## Key Design Decisions

1. **No new database tables** — delivery history reads from existing `recap_jobs` table
2. **Test delivery is isolated** — no database writes, no recap state changes
3. **SMTP credentials never exposed** — readiness is a boolean; diagnostic output shows only Host, Port, FromAddress
4. **Kindle email validation at server boundary** — CLI forwards input as-is; server validates and normalizes
5. **Pagination defaults** — `offset=0`, `limit=20`, max `limit=100`
