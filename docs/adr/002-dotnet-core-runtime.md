# ADR-002: .NET 10 as Language/Runtime

**Status:** Accepted
**Date:** 2026-03-31

## Context

Both the server and client CLI require cross-platform support (macOS, Linux, Windows). The client must be distributable as a self-contained binary with no runtime dependency on the end user's machine. The server runs in Docker and benefits from a mature ecosystem for SMTP, scheduling, CLI UX, and structured logging.

## Decision

Use **.NET 10 (C#)** for both the server and client components.

Key libraries:
- **MailKit** — SMTP email delivery
- **Quartz.NET** — cron-style scheduling for recap delivery
- **Spectre.Console** — rich CLI output (tables, progress, colors)
- **Serilog** — structured logging with file sink and SQLite sink
- **Microsoft.Data.Sqlite** — SQLite access

Distribute the client as a single-file AOT-compiled binary for macOS (arm64, amd64), Linux (amd64), and Windows (amd64).

## Consequences

- C# is widely known — low barrier for open source contributors.
- Single-file publish produces zero-dependency binaries; end users need no runtime installed.
- Docker image for the server is compact with the .NET runtime included.
- The team must maintain cross-platform build pipelines (GitHub Actions matrix build).
- Both components share the same language, models, and tooling — no polyglot complexity.
- Serilog logs are queryable via the SQLite sink, useful for debugging delivery issues.
