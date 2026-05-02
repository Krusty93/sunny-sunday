# Implementation Plan: Client CLI

**Branch**: `006-client-cli` | **Date**: 2026-05-02 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/006-client-cli/spec.md`

## Summary

Implement the `sunny` CLI binary using Spectre.Console.Cli `CommandApp` with a typed command tree. The CLI wraps all REST API operations (sync, config, exclude, weight, status) with rich Spectre output, Polly-based retry for transient HTTP errors, auto-detection of Kindle mount paths, and local timezone propagation. The CLI connects to the server via the `SUNNY_SERVER` environment variable, validates inputs locally before HTTP calls, and converts all UTC timestamps to local time for display.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0` TFM)
**Primary Dependencies**: Spectre.Console 0.55.0 (UX), Spectre.Console.Cli (command tree), Polly 8.6.6 (retry), Microsoft.Extensions.DependencyInjection (DI), Microsoft.Extensions.Http (typed HttpClient), Microsoft.Extensions.Logging.Abstractions (logging)
**Storage**: N/A — CLI is stateless; all persistence is server-side
**Testing**: xUnit + RichardSzalay.MockHttp (typed HttpClient mocking) — existing test project `SunnySunday.Tests`
**Target Platform**: Cross-platform CLI (Windows, macOS, Linux) — distributed via Docker
**Project Type**: CLI application
**Performance Goals**: All commands complete in < 5 seconds under normal network conditions (SC-006-02)
**Constraints**: Single-user MVP, no local config file, server URL from env var only
**Scale/Scope**: Single user, ~8 commands, ~12 source files in CLI project

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Client/Server Separation | **PASS** | CLI handles user interactions only; no scheduling, no email delivery |
| II. CLI-First, No GUI | **PASS** | All interactions via Spectre.Console CLI commands |
| III. Zero-Config Onboarding | **PASS** | Only `SUNNY_SERVER` env var required; Kindle path auto-detected |
| IV. Local Processing Only | **PASS** | Clippings parsing is local; data sent only to user's own server |
| V. Tests Ship with Code | **PASS** | Integration tests with MockHttp included per phase |
| VI. Simplicity / YAGNI | **PASS** | No local config file, no caching, no background sync |
| Tech: C# / .NET 10 only | **PASS** | All new code is C# |
| Tech: Spectre.Console | **PASS** | All user-facing output via Spectre |
| Tech: REST HTTP + JSON | **PASS** | HttpClient calls to server REST API |
| Tech: Docker distribution | **PASS** | CLI distributed via `Dockerfile.cli` |
| Exclusion: No web UI | **PASS** | No UI components |
| Exclusion: No auth for MVP | **PASS** | No authentication |

**Post-design re-check**: All gates pass. New NuGet packages (`Spectre.Console.Cli`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.DependencyInjection`, `RichardSzalay.MockHttp` for tests) align with technology constraints. No new projects introduced beyond the existing `SunnySunday.Cli`.

## Project Structure

### Documentation (this feature)

```text
specs/006-client-cli/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Technology decisions and rationale
├── data-model.md        # Domain models and command tree
└── quickstart.md        # Developer quick-start guide
```

### Source Code Changes

```text
src/SunnySunday.Cli/
├── Program.cs                          ← Rewritten: Generic host + CommandApp bootstrap
├── SunnySunday.Cli.csproj              ← Updated: new package references
├── Infrastructure/
│   ├── SunnyHttpClient.cs              ← NEW: typed HttpClient with base URL + JSON helpers
│   ├── HttpClientResilienceExtensions.cs ← NEW: Polly retry pipeline registration
│   ├── KindleDetector.cs               ← NEW: cross-platform Kindle mount detection
│   └── TypeRegistrar.cs                ← NEW: Spectre.Console DI bridge (ITypeRegistrar)
├── Commands/
│   ├── SyncCommand.cs                  ← NEW: sunny sync [path]
│   ├── StatusCommand.cs                ← NEW: sunny status
│   ├── Config/
│   │   ├── ConfigShowCommand.cs        ← NEW: sunny config show
│   │   ├── ConfigScheduleCommand.cs    ← NEW: sunny config schedule <cadence> <time>
│   │   └── ConfigCountCommand.cs       ← NEW: sunny config count <n>
│   ├── Exclude/
│   │   ├── ExcludeAddCommand.cs        ← NEW: sunny exclude <type> <id>
│   │   ├── ExcludeRemoveCommand.cs     ← NEW: sunny exclude remove <type> <id>
│   │   └── ExcludeListCommand.cs       ← NEW: sunny exclude list
│   └── Weight/
│       ├── WeightSetCommand.cs         ← NEW: sunny weight set <id> <value>
│       └── WeightListCommand.cs        ← NEW: sunny weight list
└── Parsing/                            ← EXISTING: no changes
    ├── ClippingsParser.cs
    ├── ParsedBook.cs
    ├── ParsedHighlight.cs
    ├── ParseResult.cs
    └── RawClipping.cs

src/SunnySunday.Tests/
└── Cli/                                ← NEW folder
    ├── SyncCommandTests.cs             ← NEW: sync + MockHttp
    ├── StatusCommandTests.cs           ← NEW: status display
    ├── ConfigCommandTests.cs           ← NEW: config commands
    ├── ExcludeCommandTests.cs          ← NEW: exclude operations
    ├── WeightCommandTests.cs           ← NEW: weight operations
    └── KindleDetectorTests.cs          ← NEW: mount path detection
```

**Structure Decision**: All CLI code lives in the existing `src/SunnySunday.Cli` project. Commands are organized in a `Commands/` folder with subfolders for grouped verbs (`Config/`, `Exclude/`, `Weight/`). Infrastructure (HTTP client, DI bridge, Kindle detection) lives in `Infrastructure/`. No new .NET project is created.

## Complexity Tracking

No constitution violations. No complexity justification needed.

---

## Phase 1: Setup & Infrastructure

**Purpose**: Add NuGet packages, implement the DI/host bootstrap, typed HTTP client with Polly retry, and the Spectre.Console DI bridge. After this phase, the CLI starts and can resolve services.

- [ ] T000 Update `src/SunnySunday.Cli/SunnySunday.Cli.csproj`: add package references for `Spectre.Console.Cli` (0.55.0), `Microsoft.Extensions.Http` (10.0.6), `Microsoft.Extensions.DependencyInjection` (10.0.6), `Microsoft.Extensions.Hosting` (10.0.6). Add `RichardSzalay.MockHttp` (7.0.0) to `src/SunnySunday.Tests/SunnySunday.Tests.csproj`. Verify solution builds: `dotnet build src/SunnySunday.slnx`.
- [ ] T001 Create `src/SunnySunday.Cli/Infrastructure/TypeRegistrar.cs`: implement `ITypeRegistrar` and `ITypeResolver` bridging Spectre.Console.Cli to `Microsoft.Extensions.DependencyInjection.IServiceProvider`. The registrar wraps an `IServiceCollection`, builds the provider on first resolution. Namespace `SunnySunday.Cli.Infrastructure`.
- [ ] T002 Create `src/SunnySunday.Cli/Infrastructure/SunnyHttpClient.cs`: a typed HTTP client class with constructor taking `HttpClient`. Properties: none (base address set at registration). Methods:
  - `PostSyncAsync(SyncRequest request, CancellationToken ct)` → `SyncResponse`
  - `GetSettingsAsync(CancellationToken ct)` → `SettingsResponse`
  - `PutSettingsAsync(UpdateSettingsRequest request, CancellationToken ct)` → `SettingsResponse`
  - `GetStatusAsync(CancellationToken ct)` → `StatusResponse`
  - `PostExcludeAsync(string type, int id, CancellationToken ct)` → `HttpResponseMessage`
  - `DeleteExcludeAsync(string type, int id, CancellationToken ct)` → `HttpResponseMessage`
  - `GetExclusionsAsync(CancellationToken ct)` → `ExclusionsResponse`
  - `PutWeightAsync(int highlightId, SetWeightRequest request, CancellationToken ct)` → `HttpResponseMessage`
  - `GetWeightsAsync(CancellationToken ct)` → `List<WeightedHighlightDto>`
  All methods throw `HttpRequestException` on non-success status codes (except 4xx which are returned for caller handling). Uses `System.Net.Http.Json` extension methods. Namespace `SunnySunday.Cli.Infrastructure`.
- [ ] T003 Create `src/SunnySunday.Cli/Infrastructure/HttpClientResilienceExtensions.cs`: static extension method `AddSunnyResilience(this IHttpClientBuilder builder)` that configures Polly retry pipeline: handle `HttpRequestException`, retry on status 408/429/5xx, max 3 attempts, exponential backoff (1s, 2s, 4s). Uses `Microsoft.Extensions.Http.Resilience` or raw Polly `ResiliencePipelineBuilder<HttpResponseMessage>`. Namespace `SunnySunday.Cli.Infrastructure`.
- [ ] T004 Rewrite `src/SunnySunday.Cli/Program.cs`:
  1. Read `SUNNY_SERVER` from environment; if missing or malformed URI, write Spectre error markup and `return 1`.
  2. Build `IServiceCollection`: register `SunnyHttpClient` as typed client with base address from `SUNNY_SERVER`, apply Polly retry via `AddSunnyResilience()`.
  3. Create `TypeRegistrar` wrapping the service collection.
  4. Create `CommandApp` with `TypeRegistrar`.
  5. Configure command tree (commands added in subsequent phases — for now register a placeholder `status` command or leave empty).
  6. `return await app.RunAsync(args)`.

**Checkpoint**: CLI starts, validates `SUNNY_SERVER`, and exits with `--help`. `dotnet build src/SunnySunday.slnx` passes.

---

## Phase 2: Kindle Detection & Sync Command (US-1)

**Purpose**: Implement the Kindle auto-detection and the `sunny sync` command — the most important user-facing feature.

- [ ] T005 Create `src/SunnySunday.Cli/Infrastructure/KindleDetector.cs`. Static class with method `string? DetectClippingsPath()`:
  - macOS: check `/Volumes/Kindle/documents/My Clippings.txt`
  - Linux: glob `/media/*/Kindle/documents/My Clippings.txt` and `/run/media/*/Kindle/documents/My Clippings.txt`
  - Windows: iterate drives D–G checking `{drive}:\documents\My Clippings.txt`
  - Return first existing path, or `null` if none found.
  Namespace `SunnySunday.Cli.Infrastructure`.
- [ ] T006 Create `src/SunnySunday.Cli/Commands/SyncCommand.cs`. Spectre.Console.Cli `AsyncCommand<SyncCommand.Settings>`:
  - Settings class: `[CommandArgument(0, "[path]")] public string? Path { get; set; }`
  - `ExecuteAsync`:
    1. Resolve path: argument → KindleDetector → Spectre prompt (TextPrompt) → error if still null.
    2. Validate file exists.
    3. Call `ClippingsParser.ParseAsync(path)`.
    4. Map `ParseResult` → `SyncRequest` (convert `ParsedBook`/`ParsedHighlight` to `SyncBookRequest`/`SyncHighlightRequest`).
    5. Call `SunnyHttpClient.PostSyncAsync(request)`.
    6. Display summary panel: new highlights, duplicates, books, authors.
    7. Return 0 on success, 1 on error.
  Register in command tree as `app.Configure(c => c.AddCommand<SyncCommand>("sync"))`.
- [ ] T007 Write `src/SunnySunday.Tests/Cli/SyncCommandTests.cs`:
  1. Successful sync: mock returns `SyncResponse(NewHighlights=5, DuplicateHighlights=2, NewBooks=3, NewAuthors=2)` → verify exit code 0.
  2. Server unreachable: mock throws `HttpRequestException` → verify exit code 1 and error message contains server URL.
  3. File not found: verify exit code 1 and error message mentions file path.
  4. Empty file: verify exit code 0 and "No highlights found" message.
  Use `CommandAppTester` from Spectre.Console.Testing or direct invocation with mocked `SunnyHttpClient`.
- [ ] T008 Write `src/SunnySunday.Tests/Cli/KindleDetectorTests.cs`: test that the detector returns null when no Kindle paths exist (the default in CI). Platform-specific tests can use a temp directory structure to simulate the expected path layout.

**Checkpoint**: `sunny sync /path/to/file` works end-to-end against a mock server. `dotnet test --filter "FullyQualifiedName~Cli.Sync"` passes.

---

## Phase 3: Status Command (US-7)

**Purpose**: Implement `sunny status` to display server health and state.

- [ ] T009 Create `src/SunnySunday.Cli/Commands/StatusCommand.cs`. Spectre.Console.Cli `AsyncCommand` (no settings needed):
  - `ExecuteAsync`:
    1. Call `SunnyHttpClient.GetStatusAsync()`.
    2. Build a Spectre `Table` with rows: Total Highlights, Total Books, Total Authors, Excluded (highlights/books/authors), Next Recap (converted to local time or "Not scheduled"), Last Recap Status, Last Recap Error (if any).
    3. Render table.
    4. Return 0.
  - On connection error: display actionable error with `SUNNY_SERVER` value and suggestion to check the variable.
  Register as `app.Configure(c => c.AddCommand<StatusCommand>("status"))`.
- [ ] T010 Write `src/SunnySunday.Tests/Cli/StatusCommandTests.cs`:
  1. Normal status response → verify table contains expected values.
  2. Server unreachable → verify error message includes server URL.
  3. Timestamps converted from UTC to local.

**Checkpoint**: `sunny status` displays server state. Tests pass.

---

## Phase 4: Config Commands (US-2, US-3, US-4)

**Purpose**: Implement schedule, count, and show configuration commands.

- [ ] T011 Create `src/SunnySunday.Cli/Commands/Config/ConfigShowCommand.cs`. `AsyncCommand` registered as `sunny config show`:
  - Fetch `GET /settings`.
  - Display table: Schedule, Delivery Day (if weekly), Delivery Time (local), Count, Kindle Email, Timezone.
- [ ] T012 Create `src/SunnySunday.Cli/Commands/Config/ConfigScheduleCommand.cs`. `AsyncCommand<ConfigScheduleCommand.Settings>`:
  - Settings: `[CommandArgument(0, "<cadence>")] public string Cadence` (daily/weekly), `[CommandArgument(1, "<time>")] public string Time` (HH:mm).
  - `show` subcommand: if first argument is "show", fetch and display current schedule only.
  - Validate time format locally (regex `^([01]\d|2[0-3]):[0-5]\d$`); reject invalid with Spectre error.
  - Validate cadence is `daily` or `weekly`.
  - Send `PUT /settings` with `Schedule`, `DeliveryTime`, `Timezone = TimeZoneInfo.Local.Id`.
  - Display confirmation with updated schedule.
- [ ] T013 Create `src/SunnySunday.Cli/Commands/Config/ConfigCountCommand.cs`. `AsyncCommand<ConfigCountCommand.Settings>`:
  - Settings: `[CommandArgument(0, "<count>")] public string CountArg` (string to handle "show").
  - If argument is "show": fetch and display current count.
  - Validate count is integer 1–15.
  - Send `PUT /settings` with `Count`.
  - Display confirmation.
- [ ] T014 Register config commands in command tree as a branch: `config` → `show`, `schedule`, `count`.
- [ ] T015 Write `src/SunnySunday.Tests/Cli/ConfigCommandTests.cs`:
  1. `config show` → displays all settings from mock response.
  2. `config schedule daily 08:00` → sends correct PUT payload with local timezone.
  3. `config schedule daily 25:00` → validation error, no HTTP call.
  4. `config count 10` → sends correct PUT payload.
  5. `config count 0` → validation error.
  6. `config count 20` → validation error.

**Checkpoint**: All config commands work. Tests pass.

---

## Phase 5: Exclude Commands (US-5)

**Purpose**: Implement exclusion management commands.

- [ ] T016 Create `src/SunnySunday.Cli/Commands/Exclude/ExcludeAddCommand.cs`. `AsyncCommand<ExcludeAddCommand.Settings>`:
  - Settings: `[CommandArgument(0, "<type>")] public string Type` (highlight/book/author), `[CommandArgument(1, "<id>")] public string Id`.
  - Validate type is one of the three allowed values.
  - Parse id as integer.
  - Call `SunnyHttpClient.PostExcludeAsync(type, id)`.
  - On 404: display "Not found" with the provided identifier.
  - On success: display confirmation.
- [ ] T017 Create `src/SunnySunday.Cli/Commands/Exclude/ExcludeRemoveCommand.cs`. `AsyncCommand<ExcludeRemoveCommand.Settings>`:
  - Settings: `[CommandArgument(0, "<type>")]`, `[CommandArgument(1, "<id>")]`.
  - Call `SunnyHttpClient.DeleteExcludeAsync(type, id)`.
  - Display confirmation or error.
- [ ] T018 Create `src/SunnySunday.Cli/Commands/Exclude/ExcludeListCommand.cs`. `AsyncCommand`:
  - Call `SunnyHttpClient.GetExclusionsAsync()`.
  - Display three grouped tables (Highlights, Books, Authors) with Spectre markup. Show "None" for empty groups.
- [ ] T019 Register exclude commands: `exclude` → default (add), `remove` subcommand, `list` subcommand.
- [ ] T020 Write `src/SunnySunday.Tests/Cli/ExcludeCommandTests.cs`:
  1. `exclude highlight 5` → sends POST to `/highlights/5/exclude`.
  2. `exclude remove book 3` → sends DELETE to `/books/3/exclude`.
  3. `exclude list` → displays grouped table from mock response.
  4. Server returns 404 → displays not-found error.

**Checkpoint**: Exclude commands work. Tests pass.

---

## Phase 6: Weight Commands (US-6)

**Purpose**: Implement weight management commands.

- [ ] T021 Create `src/SunnySunday.Cli/Commands/Weight/WeightSetCommand.cs`. `AsyncCommand<WeightSetCommand.Settings>`:
  - Settings: `[CommandArgument(0, "<id>")] public int Id`, `[CommandArgument(1, "<weight>")] public int Weight`.
  - Validate weight range (1–5).
  - Call `SunnyHttpClient.PutWeightAsync(id, new SetWeightRequest { Weight = weight })`.
  - Display confirmation.
- [ ] T022 Create `src/SunnySunday.Cli/Commands/Weight/WeightListCommand.cs`. `AsyncCommand`:
  - Call `SunnyHttpClient.GetWeightsAsync()`.
  - Display table: ID, Text (truncated), Book, Weight.
  - Show "No custom weights" if empty.
- [ ] T023 Register weight commands: `weight` → `set`, `list`.
- [ ] T024 Write `src/SunnySunday.Tests/Cli/WeightCommandTests.cs`:
  1. `weight set 5 3` → sends PUT with weight=3 to `/highlights/5/weight`.
  2. `weight set 5 0` → validation error, no HTTP call.
  3. `weight set 5 6` → validation error.
  4. `weight list` → displays table from mock response.

**Checkpoint**: Weight commands work. Tests pass.

---

## Phase 7: Polish & Integration

**Purpose**: Final wiring, error handling polish, architecture docs update, full test suite validation.

- [ ] T025 Ensure all commands handle HTTP errors uniformly: connection refused → "Cannot reach server at {url}. Check SUNNY_SERVER environment variable."; 4xx → parse error body and display field messages; 5xx after retries → "Server error. Try again later."
- [ ] T026 Ensure all commands return exit code 0 on success, 1 on error. Test a few edge cases end-to-end.
- [ ] T027 Update `docs/ARCHITECTURE.md`: add CLI section describing command tree, DI setup, HTTP client with Polly retry, Kindle detection. Reference the command hierarchy diagram.
- [ ] T028 Run full test suite: `dotnet test src/SunnySunday.slnx`. Confirm no regressions. All new CLI tests pass alongside existing parser, infrastructure, API, and recap tests.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup & Infrastructure)**: No prerequisites — start immediately. **BLOCKS all subsequent phases.**
- **Phase 2 (Sync)**: Depends on Phase 1.
- **Phase 3 (Status)**: Depends on Phase 1. Parallel with Phase 2.
- **Phase 4 (Config)**: Depends on Phase 1. Parallel with Phases 2–3.
- **Phase 5 (Exclude)**: Depends on Phase 1. Parallel with Phases 2–4.
- **Phase 6 (Weight)**: Depends on Phase 1. Parallel with Phases 2–5.
- **Phase 7 (Polish)**: Depends on all preceding phases.

### Parallel Opportunities

```
Phase 1 ──┬──► Phase 2 (Sync) ─────────────────────┐
           ├──► Phase 3 (Status) ───────────────────┤
           ├──► Phase 4 (Config) ───────────────────┼──► Phase 7 (Polish)
           ├──► Phase 5 (Exclude) ──────────────────┤
           └──► Phase 6 (Weight) ───────────────────┘
```

Phases 2–6 are independent once the infrastructure (Phase 1) is in place. Each phase introduces its own command(s) and tests without depending on other commands.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
