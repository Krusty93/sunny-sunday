# Architecture — Sunny Sunday

**Version:** 0.1 — Draft
**Date:** 2026-03-31
**Status:** Draft

---

## System Overview

Sunny Sunday follows a client/server architecture with two independently deployable components.

```
User Laptop                              Home Server / NAS / Pi
-----------                              ----------------------
sunny CLI  ── REST HTTP ──────────────▶  sunny-server (Docker)
    │                                       ├── Scheduler (Quartz.NET)
    │ USB                                   ├── SMTP sender (MailKit)
    ▼                                       └── SQLite (Docker volume)
Kindle / My Clippings.txt                              │
                                                       │ SMTP
                                                       ▼
                                            Amazon Send-to-Kindle
                                                       │
                                                       ▼
                                                  Kindle (e-ink)
```

---

## Components

### Client CLI (`sunny`)

- Distributed as a self-contained binary (macOS/Linux/Windows) or runnable via Docker
- Connects to the server via `SUNNY_SERVER` environment variable (no authentication — local network trusted)
- Responsibilities:
  - Parse and sync highlights from `My Clippings.txt` to the server
  - Manage user settings via CLI commands (schedule, count, weights, exclusions)
  - Display server status

### Server (`sunny-server`)

- Distributed as a Docker container
- Published to GHCR as `ghcr.io/krusty93/sunnysunday.server`
- Always-on, handles all automated operations
- Responsibilities:
  - Store highlights, recap history, weights, exclusions, settings in SQLite
  - Run scheduled recap generation (daily or weekly, configurable time)
  - Select highlights via spaced repetition algorithm
  - Compose recap document and send via SMTP to Kindle email address
  - Expose REST HTTP API consumed by the client CLI

---

## Technology Stack

| Component | Technology | Rationale |
|---|---|---|
| Language / runtime | .NET 10 (C#) | Cross-platform, self-contained binaries, rich ecosystem |
| Client distribution | Single-file binary / Docker | Zero runtime dependency for end users |
| Server distribution | Docker container | Self-hosted, single command to deploy |
| Storage | SQLite (file in Docker volume) | Zero config, single file, no extra container |
| Client/server protocol | REST HTTP | Simple, debuggable, universally supported |
| Email delivery | MailKit + SMTP | Industry standard, supports Send-to-Kindle |
| Logging | Serilog (file + SQLite sink) | Structured logging, persistent, queryable |
| Scheduling | Quartz.NET | Mature .NET scheduler, cron-style expressions |
| CLI UX | Spectre.Console | Rich terminal output, tables, progress bars |

---

## Data Model

```
users            (id, kindle_email, created_at)
authors          (id, name)
books            (id, title, author_id)
highlights       (id, user_id, book_id, text, weight[1-5], excluded, last_seen, delivery_count, created_at)
excluded_books   (user_id, book_id)
excluded_authors (user_id, author_id)
settings         (user_id, schedule['daily'|'weekly'], delivery_time[default:'18:00'], count[1-15, default:3])
```

> **MVP note:** Single-user only. No indexes for MVP. To be added post-MVP when multi-user support is introduced: `highlights(user_id)` and `highlights(user_id, last_seen)`.

---

## Core Query — Recap Selection

```sql
SELECT h.*
FROM highlights h
WHERE h.user_id = @userId
  AND h.excluded = 0
  AND h.book_id NOT IN (SELECT book_id FROM excluded_books WHERE user_id = @userId)
  AND h.author_id NOT IN (SELECT author_id FROM excluded_authors WHERE user_id = @userId)
ORDER BY (h.weight * RANDOM()) DESC, h.last_seen ASC
LIMIT @count
```

---

## REST API Surface

| Method | Path | Description |
|---|---|---|
| `POST` | `/sync` | Bulk import highlights from client |
| `GET` | `/status` | Server status, next recap, highlight stats |
| `GET` | `/settings` | Read current settings |
| `PUT` | `/settings` | Update settings |
| `POST` | `/highlights/{id}/exclude` | Exclude a highlight |
| `DELETE` | `/highlights/{id}/exclude` | Re-include a highlight |
| `POST` | `/books/{id}/exclude` | Exclude a book |
| `DELETE` | `/books/{id}/exclude` | Re-include a book |
| `POST` | `/authors/{id}/exclude` | Exclude an author |
| `DELETE` | `/authors/{id}/exclude` | Re-include an author |
| `GET` | `/exclusions` | List all exclusions |
| `PUT` | `/highlights/{id}/weight` | Set highlight weight |
| `GET` | `/highlights/weights` | List weighted highlights |

---

## ADR Index

| ADR | Decision |
|---|---|
| [ADR-001](adr/001-client-server-architecture.md) | Client/server architecture |
| [ADR-002](adr/002-dotnet-core-runtime.md) | .NET Core as language/runtime |
| [ADR-003](adr/003-sqlite-storage.md) | SQLite as storage engine |
| [ADR-004](adr/004-rest-http-protocol.md) | REST HTTP as client/server protocol |
| [ADR-005](adr/005-my-clippings-txt-highlight-source.md) | `My Clippings.txt` as MVP highlight source |
| [ADR-006](adr/006-docker-only-distribution.md) | Docker-only server distribution |
