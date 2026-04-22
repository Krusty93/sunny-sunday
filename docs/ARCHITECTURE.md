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
- Optional Docker image for no-install usage: `ghcr.io/krusty93/sunnysunday.cli`
- Connects to the server via `SUNNY_SERVER` environment variable (no authentication — local network trusted)
- Responsibilities:
  - Parse and sync highlights from `My Clippings.txt` to the server
  - Manage user settings via CLI commands (schedule, count, weights, exclusions)
  - Display server status

#### Parsing subsystem (`SunnySunday.Cli/Parsing/`)

Responsible for transforming raw Kindle export text into structured data before syncing to the server.

- **Entry point**: `ClippingsParser.ParseAsync(string filePath, ILogger? logger = null)` — file-path overload; `ClippingsParser.ParseAsync(TextReader, ILogger? logger = null)` — streaming overload for testability
- **Output types**:
  - `ParseResult` — top-level result: list of `ParsedBook`, total entries processed, duplicates removed
  - `ParsedBook` — `(Title, Author?, IReadOnlyList<ParsedHighlight> Highlights)`
  - `ParsedHighlight` — `(Text, Location?, AddedOn?)`
- **Design decisions**:
  - Pure static class — no state, no DI, no side effects beyond optional `ILogger`
  - Streaming: reads lines one-by-one via `ReadLineAsync()`; no full file in memory
  - Skip-and-warn: malformed entries are skipped with an `ILogger.LogWarning`; never throws
  - Deduplication: `HashSet<(Title, Author, Text)>` — exact case-sensitive match, first occurrence kept
  - Notes as highlights: entries of type "Note" are emitted as highlights with `[my note] ` prefix on their text
  - Bookmarks: entries of type "Bookmark" are silently dropped

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

#### REST API layer (`SunnySunday.Server/`)

The server currently exposes the MVP storage API as ASP.NET Minimal APIs.

- Composition root: `Program.cs`
- Endpoint modules: `Endpoints/`
- Data access: `Data/`
- Shared request/response contracts: `SunnySunday.Core/Contracts/`
- OpenAPI: Swagger UI is enabled only in Development

The application registers a scoped `IDbConnection` backed by `Microsoft.Data.Sqlite`, opens the connection per request, enables SQLite foreign keys via `PRAGMA foreign_keys = ON`, and resolves thin repository classes over that connection.

Endpoint groups currently implemented:

- Sync: bulk import via `POST /sync`
- Settings: `GET /settings`, `PUT /settings`
- Status: `GET /status`
- Exclusions: highlight/book/author include-exclude operations plus `GET /exclusions`
- Weights: `PUT /highlights/{id}/weight`, `GET /highlights/weights`

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
books            (id, user_id, author_id, title)
highlights       (id, user_id, book_id, text, weight[1-5], excluded, last_seen, delivery_count, created_at)
excluded_books   (id, user_id, book_id, excluded_at)
excluded_authors (id, user_id, author_id, excluded_at)
settings         (user_id, schedule['daily'|'weekly'], delivery_day, delivery_time[default:'18:00'], count[1-15, default:3])
```

> **MVP note:** Single-user only. The server auto-creates or reuses user `id = 1` on demand for every API request.

Current uniqueness constraints used by the REST layer:

- `authors(name)`
- `books(user_id, author_id, title)`
- `highlights(user_id, book_id, text)`

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

### Data access pattern

The REST layer uses Dapper with explicit SQL rather than EF Core.

- Each repository encapsulates one domain slice and receives `IDbConnection` via DI
- Queries stay close to the endpoint behavior they support
- Sync import uses a database transaction to keep author, book, and highlight insertion consistent
- Read models returned by list endpoints are projected directly into DTOs rather than materializing richer domain aggregates

Current repository split:

- `UserRepository`: implicit MVP user bootstrap and user email persistence
- `SyncRepository`: bulk import and deduplication
- `SettingsRepository`: settings read/upsert
- `StatusRepository`: aggregate counters
- `ExclusionRepository`: inclusion/exclusion mutations and exclusion listings
- `WeightRepository`: weight updates and weighted highlight listings

### Error handling

The API returns JSON-only responses.

- Validation failures use `Results.ValidationProblem(...)` and return HTTP `422`
- Missing entities use `Results.Problem(...)` and return HTTP `404`
- Successful mutations that do not need a body return HTTP `204`
- Successful reads return HTTP `200` with DTO payloads from `SunnySunday.Core/Contracts/`

This keeps the client protocol small, explicit, and aligned with the quickstart `curl` flows.

## Project structure

```
src/SunnySunday.Core/
└── Contracts/          # Shared request/response DTOs for CLI and server

src/SunnySunday.Server/
├── Data/               # Dapper repositories over SQLite
├── Endpoints/          # Minimal API endpoint modules
├── Infrastructure/     # Database bootstrap and logging
├── Models/             # Server-side domain models
└── Program.cs          # Composition root and DI wiring

src/SunnySunday.Tests/
├── Api/                # End-to-end HTTP integration tests via WebApplicationFactory
├── Infrastructure/     # Database/bootstrap tests
└── Parsing/            # CLI parser tests
```

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
