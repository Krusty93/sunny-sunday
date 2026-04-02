# Copilot Instructions — Sunny Sunday

## Project overview

Sunny Sunday is a self-hosted, open source tool that delivers periodic recap documents of Kindle highlights directly to the user's Kindle device via Amazon's Send-to-Kindle email service.

Architecture: **client/server**
- `sunny` — client CLI (single-file .NET binary, macOS/Linux/Windows)
- `sunny-server` — server (Docker container, always-on)

## Technology stack

- **Language:** C# / .NET 10
- **Storage:** SQLite (file in Docker volume at `/data/sunny.db`)
- **Logging:** Serilog (file sink + SQLite sink)
- **Email:** MailKit + SMTP
- **Scheduling:** Quartz.NET
- **CLI UX:** Spectre.Console
- **Protocol:** REST HTTP (JSON), no authentication for MVP

## Repository structure

```
src/
  SunnySunday.Cli/        # Client CLI
  SunnySunday.Server/     # Server (REST API + scheduler + email sender)
  SunnySunday.Core/       # Shared models, interfaces, parsers
  SunnySunday.Tests/      # Tests
docs/
  DX.md                   # Developer Experience design
  ARCHITECTURE.md         # Architecture overview and data model
  adr/                    # Architecture Decision Records
specs/
  001-sunny-sunday/       # spec-kit specification
    spec.md
```

## Key domain concepts

- **Highlight** — a text passage marked by the user on their Kindle, sourced from `My Clippings.txt`
- **Recap** — a document containing N highlights (default 3, range 1–15), sent to the user's Kindle on a schedule
- **Spaced repetition** — highlights seen less recently are prioritized; user-defined weights bias selection probability
- **Exclusion** — a highlight, book, or author excluded from all future recaps; exclusions are reversible via `sunny exclude remove`
- **Weight** — an integer (1–5) assigned to a highlight; higher weight = higher probability of appearing in a recap

## Coding conventions

- Follow standard .NET/C# conventions (PascalCase for types, camelCase for variables)
- Keep the server and client deployable independently
- All REST endpoints return JSON
- Errors must be actionable — include what went wrong and how to fix it
- No config file editing for the user — all settings managed via CLI commands and stored server-side

## Active Technologies
- C# / .NET 10 (net10.0 TFM) + Microsoft.Data.Sqlite (schema bootstrap), Serilog + Serilog.Sinks.File + Serilog.Sinks.SQLite (logging), Spectre.Console (CLI host — no commands yet) (002-core-infrastructure)
- SQLite at `/data/sunny.db` (single file, Docker volume) (002-core-infrastructure)

## Recent Changes
- 002-core-infrastructure: Added C# / .NET 10 (net10.0 TFM) + Microsoft.Data.Sqlite (schema bootstrap), Serilog + Serilog.Sinks.File + Serilog.Sinks.SQLite (logging), Spectre.Console (CLI host — no commands yet)
