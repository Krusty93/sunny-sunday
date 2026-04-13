# Copilot Instructions — Sunny Sunday

> Quick links: [Architecture](../docs/ARCHITECTURE.md) · [DX](../docs/DX.md) · [PRD](../docs/PRD.md)

## Project overview

Self-hosted tool that delivers Kindle highlight recaps to the user's Kindle via Send-to-Kindle email. Architecture: `sunny` CLI (client) + `sunny-server` Docker container (server).

**Stack:** C# / .NET 10 · SQLite (`/data/sunny.db`) · Serilog · MailKit · Quartz.NET · Spectre.Console · REST HTTP (no auth, MVP)

**Solution:** `src/SunnySunday.slnx` → Core · Server · Cli · Tests

## Coding conventions

- .NET/C# conventions (use already installed skills csharp-async, dotnet-best-practices, dotnet-upgrade,etc.)
- All REST endpoints return JSON; errors are actionable 
- TDD where applicable (e.g. API endpoints, parsers) using already installed skills csharp-xunit, etc.; not required for mechanical changes (e.g. NuGet updates, csproj edits)
- When adding new .NET projects: `dotnet sln src/SunnySunday.slnx add src/<Project>/<Project>.csproj` in the same PR
- Diagrams: Mermaid preferred; ASCII only for spatial layouts

## ADR conventions

ADRs live in `docs/adr/`. Statuses: `accepted` · `active` (under decision) · `retired` · `superseded`.
When superseded, both involved ADRs must link to each other.
Register a new ADR whenever a significant architectural decision is made during spec-kit design.

## GitHub Project conventions

**Kanban:** project #2 on `Krusty93/sunny-sunday`. Use `gh` CLI to resolve IDs at runtime:
- Project ID + status field ID: `gh project view 2 --owner Krusty93 --format json`
- Status option IDs: `gh project field-list 2 --owner Krusty93 --format json`
- Item ID for an issue: `gh api graphql -f query='{ repository(owner:"Krusty93", name:"sunny-sunday") { issue(number:N) { projectItems(first:1) { nodes { id } } } } }'`
- Move item: `gh project item-edit --id <ITEM_ID> --project-id <PROJECT_ID> --field-id <FIELD_ID> --single-select-option-id <OPTION_ID>`

Status names: `Backlog` · `Ready` · `In progress` · `In review` · `Done`

### Task lifecycle

**Before starting any task or feature:** move the kanban item to `In progress`, then begin implementation.
**On PR open:** move to `In review`. **On PR merge:** move to `Done`.

### Task structure for spec-kit features

Each feature (e.g. `003-highlight-parser`) has **one parent task** on the kanban with label `feature:00X-name`. The parent task contains:

1. **Design subtask** — runs spec-kit (`/speckit.specify` → `/speckit.plan` → `/speckit.tasks`); produces `specs/00X/spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `tasks.md`; implementation subtasks are created here
2. **Implementation subtasks** — one per phase defined in `tasks.md`; each subtask carries the same label as the parent

### Feature start sequence

When asked to start a feature, follow this exact order **before writing any code or spec**:

1. Create the **Design subtask** issue (label = parent label), add to kanban → move to `In progress`
2. Create the **Implementation subtask** issue (label = parent label), add to kanban → leave in `Backlog`
3. Run spec-kit: `/speckit.specify` → `/speckit.plan` → `/speckit.tasks` — **each step requires user involvement**: ask the user for all decisions, feature preferences, constraints, and clarifications before proceeding to the next step; do not make assumptions on scope or design choices
4. Once `tasks.md` is ready, create **one implementation phase subtask** per phase defined in `tasks.md`; each phase subtask is a child of the Implementation subtask issue (same label); add each to kanban → `Backlog`
5. Mark Design subtask PR → `In review`; on merge → `Done`; move Implementation subtask → `In progress` and begin phase-by-phase implementation

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
4. `gh pr create --title "<descriptive title, no conventional commit prefix>" --body "... Closes #N" --label "..." --base main`
5. Move kanban → `In review`
6. After merge: `git pull main`, move kanban → `Done`

## Versioning conventions

Each component (`core`, `cli`, `server`) is versioned independently via `<Version>` in its `.csproj`.

### Component paths (`.versionize`)

| Name | Path | Artefact |
|------|------|----------|
| `core` | `src/SunnySunday.Core` | library (no binary, no Docker) |
| `cli` | `src/SunnySunday.Cli` | self-contained binary + Docker image |
| `server` | `src/SunnySunday.Server` | Docker image only |

### How to bump a version

Edit `<Version>x.y.z</Version>` in the relevant `.csproj` before opening a PR. Decide bump level (patch / minor / major) yourself — no commit message format is required.

- **Never** run `git push --tags` or `git push --follow-tags` manually; tag creation and pushing are handled exclusively by `post-merge.yml` in CI.
- **Never** run `versionize` locally; it runs only in CI.

### CI gates

- `Version Bump Check` is a required status check on `main`.
- A PR is blocked if any modified component under `src/SunnySunday.*` does not have a bumped `<Version>` compared to `main`.

### Release flow

1. Developer bumps `<Version>` in the relevant `.csproj` files
2. PR opened → CI gate verifies the bump and posts a status comment
3. PR merged → `post-merge.yml` runs `versionize`, creates `<name>/v<version>` tags, pushes them
4. Tag push triggers `release.yml` → builds CLI binaries, creates GitHub Release
5. Published GitHub Release triggers `deploy-cli.yml` (Docker) and `deploy-server.yml` (Docker)

### Tag format

```
core/v1.1.0
cli/v1.3.0
server/v2.0.0
```

### Required secrets

| Secret | Scope | Used by |
|--------|-------|---------|
| `RELEASE_TOKEN` | classic PAT, `repo` | `post-merge.yml` — push tags so downstream workflows trigger |

### Docker images

- CLI: `ghcr.io/krusty93/sunny-sunday/cli:<version>` (also tagged `latest`)
- Server: `ghcr.io/krusty93/sunny-sunday/server:<version>` (also tagged `latest`)

### Adding a new component

1. Create `src/<Name>/<Name>.csproj` with `<Version>0.1.0</Version>`
2. Add `dotnet sln src/SunnySunday.slnx add src/<Name>/<Name>.csproj`
3. Add `{ "name": "<name>", "path": "src/<Name>" }` to `.versionize`
4. If it produces a Docker image, create `deploy-<name>.yml` following the pattern in `deploy-server.yml`
5. No changes to `ci.yml` or `post-merge.yml` are needed — they read `.versionize` dynamically
