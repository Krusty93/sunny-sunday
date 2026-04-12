# Copilot Instructions — Sunny Sunday

> Quick links: [Architecture](../docs/ARCHITECTURE.md) · [DX](../docs/DX.md) · [PRD](../docs/PRD.md)

## Project overview

Self-hosted tool that delivers Kindle highlight recaps to the user's Kindle via Send-to-Kindle email. Architecture: `sunny` CLI (client) + `sunny-server` Docker container (server).

**Stack:** C# / .NET 10 · SQLite (`/data/sunny.db`) · Serilog · MailKit · Quartz.NET · Spectre.Console · REST HTTP (no auth, MVP)

**Solution:** `src/SunnySunday.slnx` → Core · Server · Cli · Tests

## Coding conventions

- .NET/C# conventions (PascalCase types, camelCase variables)
- All REST endpoints return JSON; errors are actionable
- TDD where applicable (e.g. API endpoints, parsers); not required for mechanical changes (e.g. NuGet updates, csproj edits)
- When adding new .NET projects: `dotnet sln src/SunnySunday.slnx add src/<Project>/<Project>.csproj` in the same PR
- Diagrams: Mermaid preferred; ASCII only for spatial layouts

## ADR conventions

ADRs live in `docs/adr/`. Statuses: `accepted` · `active` (under decision) · `retired` · `superseded`.
When superseded, both involved ADRs must link to each other.
Register a new ADR whenever a significant architectural decision is made during spec-kit design.

## GitHub Project conventions

**Kanban:** project #2 `PVT_kwHOAHg8ss4BT_OI` · field `PVTSSF_lAHOAHg8ss4BT_OIzhBKY1M`
Statuses: `Backlog=f75ad846` · `Ready=61e4505c` · `In progress=47fc9ee4` · `In review=df73e18b` · `Done=98236657`

### Task structure for spec-kit features

Each feature (e.g. `003-highlight-parser`) has **one parent task** on the kanban with label `feature:00X-name`. The parent task contains:

1. **Design subtask** — runs spec-kit (`/speckit.specify` → `/speckit.plan` → `/speckit.tasks`); produces `specs/00X/spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `tasks.md`; implementation subtasks are created here
2. **Implementation subtasks** — one per phase defined in `tasks.md`; each subtask carries the same label as the parent

For non-feature tasks (e.g. CI/CD pipeline), check existing labels first. If no label matches, ask the user before proceeding.

Task descriptions must be self-contained: an agent must be able to implement a task by reading only the repo docs and the task description.

### PR ↔ Task link rules

- Every PR must close a GitHub issue via `Closes #N` in the body
- PR labels must match the linked task's label
- Opening a PR → move task to `In review`
- Merging a PR → move task to `Done`
- If no issue exists, create one first, add it to the kanban project, then open the PR

### tasks.md rules

- Mark a task `[X]` on the same branch where the work was done, before pushing
- Never leave `[ ]` on a branch where that task's work is already committed

## Implementation workflow (per PR)

1. `git checkout main && git pull && git checkout -b task/TXXX-short-description`
2. Implement; mark `[X]` in `tasks.md`; commit both together
3. If applicable, update living docs (`ARCHITECTURE.md`, etc.) in the same PR
4. `gh pr create --title "[TXXX] ..." --body "... Closes #N" --label "..." --base main`
5. Move kanban → `In review`
6. After merge: `git pull main`, move kanban → `Done`
