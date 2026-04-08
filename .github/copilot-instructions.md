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
- Solution file at `src/SunnySunday.slnx` (not repository root) — `dotnet new sln` on .NET 10 produces `.slnx` format

## Recent Changes
- 002-core-infrastructure: Added C# / .NET 10 (net10.0 TFM) + Microsoft.Data.Sqlite (schema bootstrap), Serilog + Serilog.Sinks.File + Serilog.Sinks.SQLite (logging), Spectre.Console (CLI host — no commands yet)
- Solution file moved to `src/SunnySunday.slnx` (design change during implementation)

## Implementation Workflow (per-task conventions)

Each task follows this exact sequence — **no exceptions**:

1. **Branch**: `git checkout main && git pull && git checkout -b task/TXXX-short-description`
2. **Implement**: make the code/file changes for the task
3. **Update `tasks.md`**: mark the task `[X]` in `specs/<feature>/tasks.md` **on the same branch, before committing**
4. **Commit & push**: include both the implementation files and `tasks.md` in the same commit (or a follow-up commit on the same branch before the PR is opened)
5. **Open PR**: `gh pr create --title "[TXXX] ..." --body "... Closes #N" --label "feature:00X-..." --base main`
6. **Kanban → In review**: `gh project item-edit --id <ITEM_ID> ... --single-select-option-id df73e18b`
7. **Wait for merge**: do not start the next task until the PR is merged to main
8. **After merge**: pull main, move kanban item → Done (`98236657`), then start next task

### tasks.md update rules
- Mark a task `[X]` **as soon as it is implemented**, on the same branch where the work was done
- Push the `tasks.md` change to the branch so the PR diff always includes the completion marker
- Never leave a task as `[ ]` on a branch where that task's work has already been committed

### Kanban item IDs (feature 002-core-infrastructure)
- Project ID: `PVT_kwHOAHg8ss4BT_OI`
- Status Field ID: `PVTSSF_lAHOAHg8ss4BT_OIzhBKY1M`
- Status options: `Backlog=f75ad846`, `Ready=61e4505c`, `In progress=47fc9ee4`, `In review=df73e18b`, `Done=98236657`

| Task | Issue | Kanban Item ID |
|------|-------|----------------|
| T000a | #1 | PVTI_lAHOAHg8ss4BT_OIzgpcE-M |
| T000b | #2 | PVTI_lAHOAHg8ss4BT_OIzgpcE_E (reused for T000b) |
| T001  | #3 | PVTI_lAHOAHg8ss4BT_OIzgpcE_E |
| T002  | #4 | PVTI_lAHOAHg8ss4BT_OIzgpcFAI |
| T003  | #5 | PVTI_lAHOAHg8ss4BT_OIzgpcFA4 |
| T004  | #6 | PVTI_lAHOAHg8ss4BT_OIzgpcFBU |
| T005  | #7 | PVTI_lAHOAHg8ss4BT_OIzgpcFBs |
| T006  | #8 | PVTI_lAHOAHg8ss4BT_OIzgpcFCM |
| T007  | #9 | PVTI_lAHOAHg8ss4BT_OIzgpcFC4 |
| T008  | #10 | PVTI_lAHOAHg8ss4BT_OIzgpcFDI |
| T009  | #11 | PVTI_lAHOAHg8ss4BT_OIzgpcFDw |
| T010  | #12 | PVTI_lAHOAHg8ss4BT_OIzgpcFEc |

### .slnx convention
- Each project is added to `src/SunnySunday.slnx` **in the same PR that creates it** (not deferred to a separate T006-style task)
- Command: `dotnet sln src/SunnySunday.slnx add src/<ProjectName>/<ProjectName>.csproj`
