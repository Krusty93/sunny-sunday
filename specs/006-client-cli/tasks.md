# Tasks: Client CLI

**Input**: Design documents from `/specs/006-client-cli/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: Included — xUnit command tests with RichardSzalay.MockHttp for typed HttpClient mocking per the design docs and repository conventions.

**Organization**: Tasks are grouped by user story so each slice stays independently testable after the shared foundation is in place.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: Which user story the task belongs to (`US1`, `US2`, `US3`, `US4`, `US5`, `US6`, `US7`, `US8`)
- All file paths are relative to the repository root

---

## Phase 1: Setup

**Purpose**: Add NuGet packages, implement the DI/host bootstrap, typed HTTP client with Polly retry, and the Spectre.Console DI bridge.

- [X] T001 Update `src/SunnySunday.Cli/SunnySunday.Cli.csproj` to add package references for `Spectre.Console.Cli` (0.55.0), `Microsoft.Extensions.Http` (10.0.6), `Microsoft.Extensions.DependencyInjection` (10.0.6); add `RichardSzalay.MockHttp` (7.0.0) to `src/SunnySunday.Tests/SunnySunday.Tests.csproj`; verify the solution builds from `src/SunnySunday.slnx`.
- [X] T002 [P] Create `src/SunnySunday.Cli/Infrastructure/TypeRegistrar.cs` implementing `ITypeRegistrar` and `ITypeResolver` bridging Spectre.Console.Cli to `Microsoft.Extensions.DependencyInjection.IServiceProvider`.
- [X] T003 [P] Create `src/SunnySunday.Cli/Infrastructure/SunnyHttpClient.cs` as a typed HTTP client with methods: `PostSyncAsync`, `GetSettingsAsync`, `PutSettingsAsync`, `GetStatusAsync`, `PostExcludeAsync`, `DeleteExcludeAsync`, `GetExclusionsAsync`, `PutWeightAsync`, `GetWeightsAsync`; uses `System.Net.Http.Json` and contracts from `SunnySunday.Core`.
- [X] T004 [P] Create `src/SunnySunday.Cli/Infrastructure/HttpClientResilienceExtensions.cs` with a Polly retry pipeline: handle `HttpRequestException`, retry on 408/429/5xx, max 3 attempts, exponential backoff (1s, 2s, 4s).
- [X] T005 Rewrite `src/SunnySunday.Cli/Program.cs` to validate `SUNNY_SERVER` env var, build `IServiceCollection` with typed `SunnyHttpClient` and Polly resilience, create `TypeRegistrar`, and run `CommandApp` with the command tree.

**Checkpoint**: CLI starts, validates `SUNNY_SERVER`, and exits with `--help`. `dotnet build src/SunnySunday.slnx` passes.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Kindle auto-detection utility required by the sync command and shared by potential future commands.

**⚠️ CRITICAL**: No user story work should start until this phase is complete.

- [X] T006 Create `src/SunnySunday.Cli/Infrastructure/KindleDetector.cs` with static `DetectClippingsPath()` probing macOS (`/Volumes/Kindle/`), Linux (`/media/*/Kindle/`, `/run/media/*/Kindle/`), and Windows (drives D–G) for `documents/My Clippings.txt`.
- [X] T007 [P] Create `src/SunnySunday.Tests/Cli/KindleDetectorTests.cs` verifying the detector returns null when no Kindle paths exist and returns a valid path when a temp directory matches the expected layout.

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel.

---

## Phase 3: User Story 1 - Sync Highlights to Server (Priority: P1) 🎯 MVP

**Goal**: Parse a Kindle clippings file and upload all highlights to the server in a single `sunny sync` command.

**Independent Test**: Run `sunny sync /path/to/My Clippings.txt` against a mock server and verify highlights are posted and a summary is displayed.

### Tests for User Story 1

- [X] T008 [P] [US1] Create `src/SunnySunday.Tests/Cli/SyncCommandTests.cs` covering: successful sync displays summary, server unreachable returns exit code 1 with server URL in error, file not found returns exit code 1, empty file displays "No highlights found".

### Implementation for User Story 1

- [X] T009 [US1] Create `src/SunnySunday.Cli/Commands/SyncCommand.cs` as `AsyncCommand<SyncCommand.Settings>`: resolve path (argument → KindleDetector → Spectre prompt), parse via `ClippingsParser`, map to `SyncRequest`, call `PostSyncAsync`, display rich summary panel.
- [X] T010 [US1] Register `SyncCommand` in the command tree in `src/SunnySunday.Cli/Program.cs`.

**Checkpoint**: `sunny sync /path/to/file` works end-to-end against a mock server. Tests pass.

---

## Phase 4: User Story 8 - Server URL Configuration (Priority: P1)

**Goal**: The CLI resolves the server URL from `SUNNY_SERVER` on every invocation and fails fast with actionable errors if missing or malformed.

**Independent Test**: Unset `SUNNY_SERVER` and verify any command exits with a clear error; set a malformed URL and verify a validation error.

### Tests for User Story 8

- [ ] T011 [P] [US8] Create `src/SunnySunday.Tests/Cli/ServerUrlValidationTests.cs` covering: missing env var exits with actionable error, malformed URL exits with validation error, valid URL allows command execution.

### Implementation for User Story 8

- [ ] T012 [US8] Verify `src/SunnySunday.Cli/Program.cs` validation logic: missing `SUNNY_SERVER` → Spectre error markup + exit 1; malformed URI (not absolute, not HTTP/HTTPS) → validation error + exit 1. (Logic implemented in T005; this task adds edge-case handling and test coverage.)

**Checkpoint**: Server URL validation is robust and tested. No command executes without a valid `SUNNY_SERVER`.

---

## Phase 5: User Story 7 - View Server Status (Priority: P2)

**Goal**: Display server health, totals, last sync, next recap, and version with `sunny status`.

**Independent Test**: Run `sunny status` against a mock server and verify the table contains expected values with local-time conversion.

### Tests for User Story 7

- [ ] T013 [P] [US7] Create `src/SunnySunday.Tests/Cli/StatusCommandTests.cs` covering: normal response displays table, server unreachable shows error with URL, UTC timestamps are converted to local time.

### Implementation for User Story 7

- [ ] T014 [US7] Create `src/SunnySunday.Cli/Commands/StatusCommand.cs` as `AsyncCommand`: call `GetStatusAsync`, build Spectre `Table` with Total Highlights, Total Books, Total Authors, Excluded counts, Next Recap (local time), Last Recap Status, Last Recap Error.
- [ ] T015 [US7] Register `StatusCommand` in the command tree in `src/SunnySunday.Cli/Program.cs`.

**Checkpoint**: `sunny status` displays server state. Tests pass.

---

## Phase 6: User Story 2 - Configure Schedule (Priority: P1)

**Goal**: Configure when recap emails are delivered using `sunny config schedule`.

**Independent Test**: Run `sunny config schedule daily 08:00` against a mock server and verify the PUT payload includes cadence, time, and local timezone.

### Tests for User Story 2

- [X] T016 [P] [US2] Add schedule tests to `src/SunnySunday.Tests/Cli/ConfigCommandTests.cs` covering: `config schedule daily 08:00` sends correct PUT with timezone, `config schedule daily 25:00` triggers validation error without HTTP call, `config schedule show` fetches and displays current schedule.

### Implementation for User Story 2

- [X] T017 [US2] Create `src/SunnySunday.Cli/Commands/Config/ConfigScheduleCommand.cs` as `AsyncCommand<Settings>`: validate time format (HH:mm regex), validate cadence (daily/weekly), handle "show" subpath, send `PUT /settings` with `Schedule`, `DeliveryTime`, `Timezone = TimeZoneInfo.Local.Id`, display confirmation.
- [X] T018 [US2] Register the `config` branch with `schedule` subcommand in `src/SunnySunday.Cli/Program.cs`.

**Checkpoint**: Schedule configuration works with local timezone propagation. Tests pass.

---

## Phase 7: User Story 3 - Configure Highlight Count (Priority: P2)

**Goal**: Configure the number of highlights per recap with `sunny config count`.

**Independent Test**: Run `sunny config count 10` against a mock server and verify the PUT payload.

### Tests for User Story 3

- [X] T019 [P] [US3] Add count tests to `src/SunnySunday.Tests/Cli/ConfigCommandTests.cs` covering: `config count 10` sends correct PUT, `config count 0` triggers validation error, `config count 20` triggers validation error, `config count show` fetches current count.

### Implementation for User Story 3

- [X] T020 [US3] Create `src/SunnySunday.Cli/Commands/Config/ConfigCountCommand.cs` as `AsyncCommand<Settings>`: validate count is integer 1–15, handle "show" subpath, send `PUT /settings` with `Count`, display confirmation.
- [X] T021 [US3] Register `count` subcommand under the `config` branch in `src/SunnySunday.Cli/Program.cs`.

**Checkpoint**: Count configuration works with range validation. Tests pass.

---

## Phase 8: User Story 4 - View All Settings (Priority: P2)

**Goal**: Display all current configuration in one place using `sunny config show`.

**Independent Test**: Run `sunny config show` against a mock server and verify a table with schedule, count, and Kindle email is displayed.

### Tests for User Story 4

- [ ] T022 [P] [US4] Add config show tests to `src/SunnySunday.Tests/Cli/ConfigCommandTests.cs` covering: `config show` displays all settings from mock response, UTC times are converted to local.

### Implementation for User Story 4

- [ ] T023 [US4] Create `src/SunnySunday.Cli/Commands/Config/ConfigShowCommand.cs` as `AsyncCommand`: call `GetSettingsAsync`, display table with Schedule, Delivery Day (if weekly), Delivery Time (local), Count, Kindle Email, Timezone.
- [ ] T024 [US4] Register `show` subcommand under the `config` branch in `src/SunnySunday.Cli/Program.cs`.

**Checkpoint**: `sunny config show` displays all settings. Tests pass.

---

## Phase 9: User Story 5 - Manage Exclusions (Priority: P2)

**Goal**: Exclude highlights, books, or authors from future recaps via `sunny exclude`.

**Independent Test**: Run `sunny exclude highlight 5` and `sunny exclude list` against a mock server; verify POST is sent and grouped table is displayed.

### Tests for User Story 5

- [ ] T025 [P] [US5] Create `src/SunnySunday.Tests/Cli/ExcludeCommandTests.cs` covering: `exclude highlight 5` sends POST, `exclude remove book 3` sends DELETE, `exclude list` displays grouped table, server 404 displays not-found error.

### Implementation for User Story 5

- [ ] T026 [P] [US5] Create `src/SunnySunday.Cli/Commands/Exclude/ExcludeAddCommand.cs` as `AsyncCommand<Settings>`: validate type (highlight/book/author), parse id, call `PostExcludeAsync`, handle 404, display confirmation.
- [ ] T027 [P] [US5] Create `src/SunnySunday.Cli/Commands/Exclude/ExcludeRemoveCommand.cs` as `AsyncCommand<Settings>`: validate type, parse id, call `DeleteExcludeAsync`, display confirmation or error.
- [ ] T028 [P] [US5] Create `src/SunnySunday.Cli/Commands/Exclude/ExcludeListCommand.cs` as `AsyncCommand`: call `GetExclusionsAsync`, display three grouped tables (Highlights, Books, Authors) with "None" for empty groups.
- [ ] T029 [US5] Register exclude commands in the command tree: `exclude` branch with default add, `remove` subcommand, and `list` subcommand in `src/SunnySunday.Cli/Program.cs`.

**Checkpoint**: Exclude commands work. Tests pass.

---

## Phase 10: User Story 6 - Manage Weights (Priority: P3)

**Goal**: Adjust highlight weights to influence selection frequency via `sunny weight`.

**Independent Test**: Run `sunny weight set 5 3` against a mock server and verify PUT is sent; run `sunny weight list` and verify table display.

### Tests for User Story 6

- [ ] T030 [P] [US6] Create `src/SunnySunday.Tests/Cli/WeightCommandTests.cs` covering: `weight set 5 3` sends PUT with weight=3, `weight set 5 0` triggers validation error, `weight set 5 6` triggers validation error, `weight list` displays table.

### Implementation for User Story 6

- [ ] T031 [P] [US6] Create `src/SunnySunday.Cli/Commands/Weight/WeightSetCommand.cs` as `AsyncCommand<Settings>`: validate weight range (1–5), call `PutWeightAsync`, display confirmation.
- [ ] T032 [P] [US6] Create `src/SunnySunday.Cli/Commands/Weight/WeightListCommand.cs` as `AsyncCommand`: call `GetWeightsAsync`, display table with ID, Text (truncated), Book, Weight; show "No custom weights" if empty.
- [ ] T033 [US6] Register weight commands in the command tree: `weight` branch with `set` and `list` subcommands in `src/SunnySunday.Cli/Program.cs`.

**Checkpoint**: Weight commands work. Tests pass.

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Uniform error handling, exit codes, architecture docs, and full regression validation.

- [ ] T034 [P] Ensure all commands handle HTTP errors uniformly in `src/SunnySunday.Cli/Commands/`: connection refused → "Cannot reach server at {url}. Check SUNNY_SERVER environment variable."; 4xx → parse error body and display field messages; 5xx after retries → "Server error. Try again later."
- [ ] T035 [P] Ensure all commands return exit code 0 on success, 1 on error; verify edge cases end-to-end.
- [ ] T036 [P] Update `docs/ARCHITECTURE.md` to add CLI section describing command tree, DI setup, HTTP client with Polly retry, and Kindle detection.
- [ ] T037 Run full test suite: `dotnet test src/SunnySunday.slnx`. Confirm no regressions. All new CLI tests pass alongside existing parser, infrastructure, API, and recap tests.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately. **BLOCKS all subsequent phases.**
- **Phase 2 (Foundational)**: Depends on Phase 1. **BLOCKS all user story phases.**
- **Phase 3 (US1 Sync)**: Depends on Phase 2.
- **Phase 4 (US8 Server URL)**: Depends on Phase 1 (validation lives in Program.cs). Parallel with Phases 2–3.
- **Phase 5 (US7 Status)**: Depends on Phase 2. Parallel with Phases 3, 6–10.
- **Phase 6 (US2 Schedule)**: Depends on Phase 2. Parallel with Phases 3, 5, 7–10.
- **Phase 7 (US3 Count)**: Depends on Phase 2. Parallel with Phases 3, 5–6, 8–10.
- **Phase 8 (US4 Config Show)**: Depends on Phase 2. Parallel with Phases 3, 5–7, 9–10.
- **Phase 9 (US5 Exclude)**: Depends on Phase 2. Parallel with Phases 3, 5–8, 10.
- **Phase 10 (US6 Weight)**: Depends on Phase 2. Parallel with Phases 3, 5–9.
- **Phase 11 (Polish)**: Depends on all preceding phases.

### User Story Dependencies

- **US1 (Sync)**: Depends on KindleDetector (Phase 2) and Infrastructure (Phase 1).
- **US8 (Server URL)**: Only depends on Phase 1 bootstrap — can be validated first.
- **US7 (Status)**: Independent from other user stories.
- **US2 (Schedule)**: Independent from other user stories.
- **US3 (Count)**: Independent from other user stories.
- **US4 (Config Show)**: Independent from other user stories.
- **US5 (Exclude)**: Independent from other user stories.
- **US6 (Weight)**: Independent from other user stories.

### Parallel Opportunities

- `T002`, `T003`, and `T004` can run in parallel once `T001` lands.
- `T006` and `T007` can run in parallel once Phase 1 is done.
- Phases 3–10 (all user stories) can proceed in parallel after Phase 2, each touching different command files.
- Within US5: `T026`, `T027`, and `T028` can run in parallel (different files).
- Within US6: `T031` and `T032` can run in parallel (different files).

---

## Parallel Example: Phase 1

```text
T001 ──┬──► T002 ──┐
       ├──► T003 ──┼──► T005
       └──► T004 ──┘
```

## Parallel Example: User Story 5

```text
T025 ──┬──► T026 ──┐
       ├──► T027 ──┼──► T029
       └──► T028 ──┘
```

## Parallel Example: User Stories After Phase 2

```text
Phase 2 ──┬──► Phase 3  (US1 Sync) ─────────┐
           ├──► Phase 5  (US7 Status) ────────┤
           ├──► Phase 6  (US2 Schedule) ──────┤
           ├──► Phase 7  (US3 Count) ─────────┼──► Phase 11 (Polish)
           ├──► Phase 8  (US4 Config Show) ───┤
           ├──► Phase 9  (US5 Exclude) ───────┤
           └──► Phase 10 (US6 Weight) ────────┘
```

---

## Implementation Strategy

### Incremental Delivery

1. Complete Setup (Phase 1) to establish DI, HTTP client, and Polly resilience.
2. Complete Foundational (Phase 2) for KindleDetector.
3. Land US1 (Sync) and US8 (Server URL) — these form the minimum usable CLI.
4. Land US7 (Status), US2 (Schedule), US3 (Count), US4 (Config Show) in any order.
5. Land US5 (Exclude) and US6 (Weight) — lower-priority management commands.
6. Finish with error-handling polish, exit-code verification, and doc alignment.

### Suggested MVP Scope

The smallest demonstrable increment is **Phase 1 + Phase 2 + US1 (Sync) + US8 (Server URL)** — a user can sync highlights to a server with one command. All other user stories add management capabilities on top of this working baseline.

---

## Notes

- All commands use constructor-injected `SunnyHttpClient` resolved via the `TypeRegistrar` DI bridge.
- No `IHost` / generic host — lightweight `IServiceCollection` → `IServiceProvider` per research decision.
- Polly retry at `DelegatingHandler` level is transparent to command implementations; commands see either success or final failure.
- UTC-to-local conversion uses `TimeZoneInfo.Local` for display throughout all commands.
- Tests use `RichardSzalay.MockHttp` at the `HttpMessageHandler` level and Spectre `TestConsole` for output verification.
