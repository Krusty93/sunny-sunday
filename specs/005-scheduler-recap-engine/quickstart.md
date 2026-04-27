# Quickstart: Scheduler + Recap Engine

**Feature**: 005-scheduler-recap-engine
**Date**: 2026-04-27

---

## Prerequisites

- .NET 10 SDK installed (`dotnet --version` → `10.x`)
- Repository cloned and on branch `005-scheduler-recap-engine` (or `task/TXXX-*` task branch)
- Solution builds cleanly from `main`: `dotnet build src/SunnySunday.slnx`
- A configured SMTP account (Gmail app password, Outlook, or any SMTP relay) for manual delivery testing

---

## New Dependencies

### Server project (`SunnySunday.Server.csproj`)

```bash
dotnet add src/SunnySunday.Server package Quartz.Extensions.Hosting
dotnet add src/SunnySunday.Server package MailKit
```

---

## SMTP Configuration

SMTP credentials are **not** stored in the database. Configure them via `appsettings.Development.json` locally or environment variables in Docker.

### Local development (`src/SunnySunday.Server/appsettings.Development.json`)

```json
{
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-specific-password"
  }
}
```

> **Note**: For Gmail, generate an [App Password](https://support.google.com/accounts/answer/185833) (requires 2FA). Never commit real credentials to source control.

### Docker (environment variables)

```yaml
# docker-compose.yml excerpt
environment:
  - SMTP_HOST=smtp.gmail.com
  - SMTP_PORT=587
  - SMTP_USER=your-email@gmail.com
  - SMTP_PASSWORD=your-app-specific-password
```

---

## Build and Run

```bash
# Build the entire solution
dotnet build src/SunnySunday.slnx

# Run the server (Development mode — Swagger UI enabled)
dotnet run --project src/SunnySunday.Server
```

On startup the server will:
1. Apply schema migrations (create `recap_jobs` table; add `timezone` column to `settings` if absent)
2. Read settings from SQLite
3. Register the Quartz cron trigger for the configured schedule

Server listens on `http://localhost:5000` by default.

---

## Run Tests

```bash
# All tests
dotnet test src/SunnySunday.slnx

# Only recap engine tests (selection, EPUB, retry)
dotnet test src/SunnySunday.Tests --filter "FullyQualifiedName~Recap"

# Only updated endpoint tests
dotnet test src/SunnySunday.Tests --filter "FullyQualifiedName~Api"
```

---

## Manual Testing

### Configure Kindle email and timezone

```bash
curl -X PUT http://localhost:5000/settings \
  -H "Content-Type: application/json" \
  -d '{
    "kindleEmail": "your-kindle-email@kindle.com",
    "timezone": "Europe/Rome",
    "deliveryTime": "18:00",
    "schedule": "daily",
    "count": 5
  }'
```

Expected response (200):
```json
{
  "schedule": "daily",
  "deliveryDay": null,
  "deliveryTime": "18:00",
  "count": 5,
  "kindleEmail": "your-kindle-email@kindle.com",
  "timezone": "Europe/Rome"
}
```

### Check server status and next recap time

```bash
curl http://localhost:5000/status
```

Expected response (200):
```json
{
  "totalHighlights": 42,
  "totalBooks": 7,
  "totalAuthors": 5,
  "excludedHighlights": 0,
  "excludedBooks": 0,
  "excludedAuthors": 0,
  "nextRecap": "2026-04-28T16:00:00Z",
  "lastRecapStatus": null,
  "lastRecapError": null
}
```

> `nextRecap` is UTC. To display in local time in the CLI, parse it and convert using the stored timezone.

### Verify invalid timezone is rejected

```bash
curl -X PUT http://localhost:5000/settings \
  -H "Content-Type: application/json" \
  -d '{"timezone": "Fake/Zone"}'
```

Expected response (422):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 422,
  "errors": {
    "timezone": ["Unrecognized timezone identifier 'Fake/Zone'."]
  }
}
```

### Sync some highlights before testing recap

```bash
curl -X POST http://localhost:5000/sync \
  -H "Content-Type: application/json" \
  -d '{
    "books": [
      {
        "title": "Deep Work",
        "author": "Cal Newport",
        "highlights": [
          { "text": "Professional activities performed in a state of distraction-free concentration." },
          { "text": "Clarity about what matters provides clarity about what does not." },
          { "text": "The key to developing a deep work habit is to move beyond good intentions." }
        ]
      },
      {
        "title": "Atomic Habits",
        "author": "James Clear",
        "highlights": [
          { "text": "You do not rise to the level of your goals. You fall to the level of your systems." },
          { "text": "Every action you take is a vote for the type of person you wish to become." }
        ]
      }
    ]
  }'
```

---

## Observing Quartz Scheduling

When the server starts, Serilog logs the Quartz trigger registration:

```
[INF] Quartz scheduler started. Next recap trigger: 2026-04-28T16:00:00Z (Europe/Rome → 18:00 local)
```

When a recap fires:

```
[INF] RecapJob executing for slot 2026-04-28T16:00:00Z, user_id=1
[INF] Selected 5 highlights. Composing EPUB.
[INF] EPUB composed (3842 bytes). Delivering to your-kindle-email@kindle.com.
[INF] Recap delivered successfully. Updated last_seen for 5 highlights.
```

If delivery fails:

```
[WRN] Delivery attempt 1 failed: Connection refused. Polly scheduled retry 2/3 with exponential backoff.
[WRN] Delivery attempt 2 failed: Authentication failed. Polly scheduled retry 3/3 with exponential backoff.
[ERR] All 3 delivery attempts exhausted. recap_job_id=7, error: Authentication failed.
```

---

## Verifying the EPUB Locally

To inspect a generated EPUB without sending it to a Kindle device:

1. Write a temporary integration test or tool that calls `EpubComposer.Compose(highlights, date)` and writes the result to disk.
2. Rename the `.epub` file to `.zip` and open it with any archive manager.
3. Verify the presence of `mimetype`, `META-INF/container.xml`, `OEBPS/content.opf`, `OEBPS/toc.ncx`, `OEBPS/highlights.xhtml`.
4. Open `OEBPS/highlights.xhtml` in a browser to preview the flat list rendering.

---

## Resetting Recap History

To reset `last_seen` and `delivery_count` on all highlights during development (forces all highlights to rank as "never seen"):

```bash
sqlite3 .data/sunny.db "UPDATE highlights SET last_seen = NULL, delivery_count = 0;"
sqlite3 .data/sunny.db "DELETE FROM recap_jobs;"
```

---

## Swagger UI

Available in Development at `http://localhost:5000/swagger`. Updated endpoint definitions for `PUT /settings` (new `timezone` field) and `GET /status` (new `lastRecapStatus`, `lastRecapError` fields) are visible here.
