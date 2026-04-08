# Copilot Instructions — Sunny Sunday

## Project overview

Sunny Sunday is a self-hosted, open source tool that delivers periodic recap documents of Kindle highlights directly to the user's Kindle device via Amazon's Send-to-Kindle email service.

Architecture: **client/server**
- `sunny` — client CLI (single-file .NET binary, macOS/Linux/Windows)
- `sunny-server` — server (Docker container, always-on)

## Technology stack

- **Language:** C# / .NET 10
- **Storage:** SQLite (file in Docker volume at `.data/sunny.db`)
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

## Implementation Workflow (per-task conventions)

Each task follows this exact sequence — **no exceptions**:

1. **Branch**: `git checkout main && git pull && git checkout -b task/TXXX-short-description`
2. **Implement**: make the code/file changes for the task
3. **Update `tasks.md`**: mark the task `[X]` in `specs/<feature>/tasks.md` **on the same branch, before committing**
4. **Commit & push**: include both the implementation files and `tasks.md` in the same branch but on separated commits
5. **Ensure issue exists**: every PR must link to a GitHub issue via `Closes #N`. If no issue exists for the work (e.g. chore, hotfix, unplanned task), create one first: `gh issue create --title "..." --body "..." --label "..."`, add it to the kanban project, then use its number in the PR body
6. **Open PR**: `gh pr create --title "[TXXX] ..." --body "... Closes #N" --label "feature:00X-..." --base main`
7. **Kanban → In review**: `gh project item-edit --id <ITEM_ID> ... --single-select-option-id df73e18b`
8. **Wait for merge**: do not start the next task until the PR is merged to main
9. **After merge**: pull main, move kanban item → Done (`98236657`), then start next task

### tasks.md update rules
- Mark a task `[X]` **as soon as it is implemented**, on the same branch where the work was done
- Push the `tasks.md` change to the branch so the PR diff always includes the completion marker
- Never leave a task as `[ ]` on a branch where that task's work has already been committed

### .slnx convention
- When a new dotnet project is created, add it to `src/SunnySunday.slnx` **in the same PR that creates it**
- Command: `dotnet sln src/SunnySunday.slnx add src/<ProjectName>/<ProjectName>.csproj`
