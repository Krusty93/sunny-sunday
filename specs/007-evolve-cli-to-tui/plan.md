# Implementation Plan: Evolve CLI to TUI

**Branch**: `007-evolve-cli-to-tui` | **Date**: 2026-05-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/007-evolve-cli-to-tui/spec.md`

## Summary

Evolve the existing `relego` CLI into a dual-mode application: when invoked without arguments, it launches a persistent interactive TUI built with Terminal.Gui v2. When invoked with arguments, the existing Spectre.Console.Cli `CommandApp` processes commands identically to today. The TUI provides a branded splash screen, always-visible status chrome, a navigable book list, highlight detail management, settings editing, and client-side search. One targeted server-side addition supports the TUI: a dedicated `POST /settings/test-email` endpoint for plain-text SMTP verification.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0` TFM)
**Primary Dependencies**: Terminal.Gui 2.1.0 (new dependency) — `Application`, `Window`, `FrameView`, `View`, `Label`, `TableView`. Spectre.Console.Cli (already in project) continues to be used for CLI command routing.
**Storage**: N/A — TUI is stateless; all data fetched from server REST API and cached in memory for the session.
**Testing**: xUnit (existing `Relego.Tests` project) — test data/logic layer, not rendering.
**Target Platform**: Cross-platform CLI (Windows, macOS, Linux) — interactive terminals with ≥80×24.
**Project Type**: CLI application with one supporting server endpoint addition
**Performance Goals**: TUI startup < 3s (SC-007-01), search filtering < 200ms (SC-007-07), book list scrolling smooth with 100+ books (SC-007-05).
**Constraints**: Terminal.Gui 2.1.0 added as a new NuGet dependency for TUI rendering (supersedes FR-007-14). No changes to existing API endpoints (FR-007-18). One new server endpoint required: `POST /settings/test-email` (documented on #188). Single-user MVP. Terminal.Gui provides a native event loop — no custom render loop needed.
**Scale/Scope**: Single user, 3 screens, ~12 new source files in `Tui/` folder, 1 server endpoint addition, ~7 new test files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Client/Server Separation | **PASS** | TUI is purely client-side; all data from server API |
| II. CLI-First, No GUI | **PASS** | TUI is a terminal interface, not a GUI. All CLI commands preserved unchanged |
| III. Zero-Config Onboarding | **PASS** | TUI auto-detects when no args provided; only `RELEGO_SERVER` needed |
| IV. Local Processing Only | **PASS** | All rendering and search are local. No third-party services |
| V. Tests Ship with Code | **PASS** | Unit tests for data/logic layer included per phase |
| VI. Simplicity / YAGNI | **PASS** | No new frameworks, no new projects, no local caching beyond session |
| Tech: C# / .NET 10 only | **PASS** | All new code is C# |
| Tech: Terminal.Gui (TUI) | **PASS** | All TUI rendering via Terminal.Gui v2 APIs; Spectre.Console.Cli retained for CLI routing |
| Tech: REST HTTP + JSON | **PASS** | TUI uses existing `SunnyHttpClient` methods |
| Tech: Docker distribution | **PASS** | TUI runs in Docker with `-it` flag |
| Exclusion: No web UI | **PASS** | TUI is terminal-based, not web |
| Exclusion: No auth for MVP | **PASS** | No authentication added |

**Post-design re-check**: All gates pass. Terminal.Gui 2.1.0 added as a new NuGet dependency to provide the event loop, widget system, and layout engine for TUI rendering. Spectre.Console.Cli is retained for CLI command routing. Three new methods added to `SunnyHttpClient` (using existing HTTP infrastructure). `SunnyJsonContext` extended with two types for trimming compatibility. One new server-side endpoint (`POST /settings/test-email`) identified and documented on #188.

## Project Structure

### Documentation (this feature)

```text
specs/007-evolve-cli-to-tui/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Technology decisions and rationale
├── data-model.md        # View models and screen architecture
├── quickstart.md        # Developer quick-start guide
└── tasks.md             # Phase-by-phase task list (created by /speckit.tasks)
```

### Source Code Changes

```text
src/Relego.Cli/
├── Program.cs                          ← MODIFIED: add TUI mode detection before CommandApp
├── Tui/
│   ├── TuiApp.cs                       ← NEW: render loop orchestrator (LiveDisplay + ReadKey)
│   ├── IScreen.cs                      ← NEW: screen interface + ScreenResult + ScreenAction
│   ├── StatusChrome.cs                 ← NEW: persistent header (Figlet, version, connection, warning)
│   ├── BookListScreen.cs               ← NEW: main screen — book table with navigation + search
│   ├── HighlightDetailScreen.cs        ← NEW: drill-down per book — highlight list + action menu
│   ├── SettingsScreen.cs               ← NEW: settings editor with inline editing
│   ├── SearchFilter.cs                 ← NEW: client-side search logic (title, author, text match)
│   └── ViewModels/
│       ├── BookViewModel.cs            ← NEW: book aggregation model
│       └── HighlightViewModel.cs       ← NEW: highlight display model
├── Infrastructure/
│   ├── SunnyHttpClient.cs              ← MODIFIED: add GetHighlightsAsync, DeleteHighlightAsync, PostTestEmailAsync, PingAsync
│   └── SunnyJsonContext.cs             ← MODIFIED: add HighlightsResponse + HighlightItemDto
├── Commands/                           ← UNCHANGED: all existing commands preserved
└── Parsing/                            ← UNCHANGED

src/Relego.Tests/
└── Tui/                                ← NEW folder
    ├── ModeDetectionTests.cs           ← NEW: TUI vs CLI mode detection
    ├── BookGroupingTests.cs            ← NEW: highlight → book view model grouping
    ├── SearchFilterTests.cs            ← NEW: client-side search matching
    ├── BookListScreenTests.cs          ← NEW: key handling and navigation state
    ├── HighlightDetailScreenTests.cs   ← NEW: action dispatch + navigation
    └── SettingsScreenTests.cs          ← NEW: field editing logic

src/Relego.Server/
├── Endpoints/
│   └── SettingsEndpoints.cs            ← MODIFIED: add POST /settings/test-email
├── Services/
│   ├── IMailDeliveryService.cs         ← MODIFIED: add plain-text test email contract
│   ├── MailDeliveryService.cs          ← MODIFIED: implement plain-text test email delivery
│   └── DevMailDeliveryService.cs       ← MODIFIED: implement plain-text test email delivery for dev SMTP relay

src/Relego.Tests/
└── Api/
  └── SettingsTestEmailEndpointTests.cs ← NEW: POST /settings/test-email behavior
```

**Structure Decision**: All TUI code lives in a new `Tui/` folder within the existing `Relego.Cli` project, cleanly separated from `Commands/` (CLI) and `Infrastructure/` (shared). No new .NET projects are created. Test files live in `src/Relego.Tests/Tui/`.

## Complexity Tracking

No constitution violations. No complexity justification needed.

---

## Phase 1: Dual-Mode Detection & Render Loop Infrastructure

**Purpose**: Establish the TUI/CLI mode split in `Program.cs` and build the core render loop (`TuiApp`) with screen abstraction. After this phase, `relego` (no args) shows a minimal TUI with the render loop running, and `relego status` still works exactly as before.

- [ ] T000 Create `src/Relego.Cli/Tui/IScreen.cs`: define `IScreen` interface with `Render() → IRenderable`, `HandleKeyAsync(ConsoleKeyInfo, CancellationToken) → Task<ScreenResult>`, `InitializeAsync(CancellationToken) → Task`, `KeyHints → string`. Define `ScreenAction` enum (`None`, `Push`, `Pop`, `Quit`) and `ScreenResult` record. Namespace `Relego.Cli.Tui`.
- [ ] T001 Create `src/Relego.Cli/Tui/ViewModels/BookViewModel.cs`: record with `Title`, `Author`, `HighlightCount`, `IReadOnlyList<HighlightViewModel> Highlights`. Add static factory method `FromHighlights(IEnumerable<HighlightItemDto> items) → List<BookViewModel>` that groups by `(BookTitle, AuthorName)`. Namespace `Relego.Cli.Tui.ViewModels`.
- [ ] T002 Create `src/Relego.Cli/Tui/ViewModels/HighlightViewModel.cs`: record with `Id`, `Text`, `BookTitle`, `AuthorName`, `IsExcluded`, `Weight`. Namespace `Relego.Cli.Tui.ViewModels`.
- [ ] T003 Create `src/Relego.Cli/Tui/TuiApp.cs`: the main orchestrator class. Constructor takes `SunnyHttpClient`, `string serverUrl`, `string version`. Key responsibilities:
  1. Initialize Terminal.Gui `Application` and create the root `Window`.
  2. Manage a `Stack<IScreen>` — push initial screen on startup.
  3. Use Terminal.Gui's event loop (`Application.Run`) with `IScreen.CreateView()` producing `View` instances that are swapped into the content `FrameView` on navigation.
  4. Handle `Q` and `Ctrl+C` via Terminal.Gui key bindings to stop `Application.Run` cleanly.
  5. Handle terminal size check: if < 80×24, display resize message instead of content.
  Public method: `Task RunAsync(CancellationToken ct)`.
- [ ] T004 Modify `src/Relego.Cli/Program.cs`: add TUI mode detection **before** `CommandApp` creation. Logic: if `args.Length == 0 && !Console.IsInputRedirected`, create `TuiApp` and call `RunAsync`. If `args.Length == 0 && Console.IsInputRedirected`, fall through to `CommandApp` (shows help). If `args.Length > 0`, fall through to `CommandApp` (existing behavior). Import `Relego.Cli.Tui` namespace.
- [ ] T005 Add `GetHighlightsAsync(int page, int pageSize, string? query, CancellationToken ct)` method to `src/Relego.Cli/Infrastructure/SunnyHttpClient.cs`: calls `GET /highlights?page={page}&pageSize={pageSize}&q={query}`, returns `HighlightsResponse`. Add `PostTestEmailAsync(CancellationToken ct)` method: calls `POST /settings/test-email`, returns `HttpResponseMessage` (sends a simple plain-text test email, not a recap). Add `DeleteHighlightAsync(int id, CancellationToken ct)` method: calls `DELETE /highlights/{id}`, returns `HttpResponseMessage`. Add `PingAsync(CancellationToken ct)` method: calls `GET /`, returns `bool` (true if HTTP 200, false otherwise). Keep existing `PostTestRecapAsync` (calls `POST /dev/recap/trigger`) for dev-only use.
- [ ] T006 Update `src/Relego.Cli/Infrastructure/SunnyJsonContext.cs`: add `[JsonSerializable(typeof(HighlightsResponse))]` and `[JsonSerializable(typeof(HighlightItemDto))]` for trimming compatibility.
- [ ] T007 Write `src/Relego.Tests/Tui/ModeDetectionTests.cs`: test that empty args + interactive terminal → TUI mode, empty args + piped stdin → CLI mode (help), args present → CLI mode. Test the detection logic extracted as a static helper method for testability.
- [ ] T008 Write `src/Relego.Tests/Tui/BookGroupingTests.cs`: test `BookViewModel.FromHighlights()`: multiple highlights from same book → single `BookViewModel` with correct count, different books → separate entries, empty list → empty result, highlights with same title but different author → separate books.

**Checkpoint**: `relego` (no args, interactive terminal) enters a render loop showing a placeholder screen. `relego status` works as before. `dotnet build src/Relego.slnx` and `dotnet test --filter "FullyQualifiedName~Tui"` pass.

---

## Phase 2: Status Chrome (Figlet, Version, Connection, Warnings)

**Purpose**: Build the persistent header region (`StatusChrome`) that appears on every TUI screen: Figlet banner, version, server connection status, and Kindle email warning.

- [ ] T009 Create `src/Relego.Cli/Tui/StatusChrome.cs`: class that produces the chrome `View`. Constructor takes `string serverUrl`, `string version`. Methods:
  1. `async Task RefreshAsync(SunnyHttpClient client, CancellationToken ct)` — calls `PingAsync` (`GET /`) to check connection (HTTP 200 = connected). If connected, calls `GetSettingsAsync` to check `KindleEmailConfigured`. Catches `HttpRequestException` → sets disconnected state.
  2. `View Render()` — builds a Terminal.Gui `View` containing:
     - ASCII art "SUNNY" banner with colored `Label` rows.
     - Version line.
     - Connection status: green `● Connected to {url}` or red `● Disconnected — cannot reach {url}`.
     - Conditional warning: yellow `⚠ Kindle email not configured — recaps cannot be delivered` (only if `KindleEmailConfigured == false`).
  Properties: `bool IsConnected`, `bool KindleEmailConfigured`.
- [ ] T010 Wire `StatusChrome` into `TuiApp.cs`: create `StatusChrome` in constructor, call `RefreshAsync` on startup and after each failed API call. Add the chrome `View` to the root window layout.
- [ ] T011 Add key hint bar rendering to `TuiApp.cs`: render a footer `View` with key hint labels sourced from `currentScreen.KeyHints`. Style with dim/grey color.

**Checkpoint**: TUI displays Figlet banner, version, connection status, and (if applicable) Kindle email warning. These persist when navigating between screens.

---

## Phase 3: Book List Screen

**Purpose**: Implement the main landing screen showing all imported books in a navigable table. This is the primary TUI content screen (US-3).

- [ ] T012 Create `src/Relego.Cli/Tui/BookListScreen.cs`: implements `IScreen`. State: `List<BookViewModel> books`, `List<BookViewModel> filteredBooks`, `int selectedIndex`, `bool isSearchActive`, `string searchQuery`.
  1. `InitializeAsync`: fetch all highlights from server (paginate through `GetHighlightsAsync` until all loaded), group into `BookViewModel` list via `BookViewModel.FromHighlights()`. Also fetch exclusions and weights to enrich `HighlightViewModel.IsExcluded` and `Weight` fields.
  2. `Render()`: build a Terminal.Gui `TableView` with columns Title, Author, Highlights. Highlight the selected row with a distinct style. If search is active, show search input above table. If no books, show empty state message: "No highlights found. Run `relego sync` to import."
  3. `HandleKeyAsync`: `↑` = decrement selectedIndex (clamp), `↓` = increment (clamp), `Enter` = return `Push(new HighlightDetailScreen(selectedBook))`, `S` = return `Push(new SettingsScreen())`, `/` = activate search mode, `R` = refresh (re-fetch data from server and re-render), `Q` = return `Quit`, `Ctrl+C` = return `Quit`.
  4. `KeyHints`: `"[↑↓] Navigate · [Enter] View · [S] Settings · [/] Search · [R] Refresh · [Q] Quit"`.
  Constructor takes `SunnyHttpClient`.
- [ ] T013 Create `src/Relego.Cli/Tui/SearchFilter.cs`: static class with method `List<BookViewModel> Apply(IEnumerable<BookViewModel> books, string query)`. Filters books where title, author, or any highlight text contains the query (case-insensitive, `OrdinalIgnoreCase`). Returns matching books with all their highlights (not filtered highlights).
- [ ] T014 Integrate search into `BookListScreen`: when search is active, keystrokes append to `searchQuery`, `Esc` clears search, `Backspace` removes last character. After each keystroke, apply `SearchFilter.Apply()` to produce `filteredBooks` and reset `selectedIndex` to 0. `Render()` uses `filteredBooks` when search is active.
- [ ] T015 Wire `BookListScreen` as the initial screen in `TuiApp.RunAsync()`: push `BookListScreen` onto the screen stack on startup.
- [ ] T016 Write `src/Relego.Tests/Tui/SearchFilterTests.cs`: test cases: match by title, match by author, match by highlight text, case-insensitive match, no match returns empty, empty query returns all, partial match works.
- [ ] T017 Write `src/Relego.Tests/Tui/BookListScreenTests.cs`: test key handling: `↑/↓` changes `selectedIndex` within bounds, `Enter` returns `Push` with correct book, `Q` returns `Quit`, `/` activates search mode. Test empty state when no books loaded.

**Checkpoint**: TUI launches with book list populated from server. Arrow keys navigate rows. Search filters the list. `Enter` attempts to push highlight detail screen (implemented next phase). Tests pass.

---

## Phase 4: Highlight Detail Screen

**Purpose**: Implement the drill-down view for a selected book, showing its highlights with an action menu for weight, exclusion, and deletion operations (US-4).

- [ ] T018 Create `src/Relego.Cli/Tui/HighlightDetailScreen.cs`: implements `IScreen`. Constructor takes `BookViewModel book`, `SunnyHttpClient client`. State: `int selectedIndex`, `bool actionMenuOpen`, `int actionMenuIndex`.
  1. `InitializeAsync`: no-op (data already loaded in `BookViewModel`).
  2. `Render()`: build a `Panel` with book title as header. Inside, render a `Table` or list of highlights with text (truncated if long), weight indicator, exclusion indicator. Highlight selected row. If `actionMenuOpen`, overlay an action menu listing: Modify weight, Exclude/Include highlight, Exclude/Include book, Exclude/Include author, Delete highlight.
  3. `HandleKeyAsync`:
     - Normal mode: `↑/↓` = navigate highlights, `Enter` = open action menu, `R` = refresh (re-initialize screen data from server), `Esc` = return `Pop`.
     - Action menu mode: `↑/↓` = navigate actions, `Enter` = execute selected action, `Esc` = close menu.
  4. Action execution: each action calls the appropriate `SunnyHttpClient` method, then refreshes the highlight data. For "Delete highlight", display a confirmation step (re-render with "Press Y to confirm, N to cancel").
  5. `KeyHints`: `"[↑↓] Navigate · [Enter] Actions · [R] Refresh · [Esc] Back"`.
- [ ] T019 Write `src/Relego.Tests/Tui/HighlightDetailScreenTests.cs`: test key handling: navigation within bounds, `Enter` opens action menu, `Esc` from action menu closes it, `Esc` from normal mode returns `Pop`. Test action menu index navigation.

**Checkpoint**: Selecting a book in the book list opens the highlight detail view. Actions (weight, exclude, delete) call the server API. `Esc` returns to book list. Tests pass.

---

## Phase 5: Test Email API Endpoint

**Purpose**: Add the missing server endpoint used by the TUI settings page to verify SMTP delivery without triggering a recap.

- [ ] T021 Update `src/Relego.Server/Services/IMailDeliveryService.cs`: add a plain-text test email contract and implement plain-text test email delivery in `MailDeliveryService.cs` and `DevMailDeliveryService.cs` using the configured SMTP transport, without recap attachment or recap generation.
- [ ] T022 Update `src/Relego.Server/Endpoints/SettingsEndpoints.cs`: add `POST /settings/test-email` that validates Kindle email presence, sends a plain-text verification email, returns a success payload on 200, validation error when Kindle email is missing, and actionable failure on SMTP error.
- [ ] T023 Write `src/Relego.Tests/Api/SettingsTestEmailEndpointTests.cs`: cover success, missing Kindle email, and SMTP failure cases for `POST /settings/test-email`.

**Checkpoint**: `POST /settings/test-email` is available and covered by API tests. The endpoint sends a plain-text verification email without generating a recap.

---

## Phase 6: Settings Screen

**Purpose**: Implement the settings page for editing Kindle email, recap schedule, highlight count, and sending a test email (US-6).

- [ ] T024 Create `src/Relego.Cli/Tui/SettingsScreen.cs`: implements `IScreen`. Constructor takes `SunnyHttpClient client`, `bool isDevelopment`. State: `SettingsResponse settings`, `int selectedField`, `bool isEditing`, `string editBuffer`.
  1. `InitializeAsync`: call `GetSettingsAsync()` to load current settings.
  2. `Render()`: build a `Table` or panel layout showing fields:
     - Kindle Email: `{value}` (or "[not set]")
     - Schedule: `{cadence}` at `{time}` (`{timezone}`)
     - Highlights per recap: `{count}`
     - [Send test email]
     Highlight selected field. If `isEditing`, show an inline text input for the selected field.
  3. `HandleKeyAsync`:
     - Normal mode: `↑/↓` = navigate fields, `Enter` = start editing or trigger action, `T` = send test email (`POST /settings/test-email`), `R` = refresh (re-fetch settings), `Esc` = return `Pop`.
     - Edit mode: keystrokes append to `editBuffer`, `Enter` = validate and submit, `Esc` = cancel edit.
  4. Validation:
     - Kindle email: basic format check (contains `@`).
     - Schedule: cadence must be `daily`/`weekly`, time must match `HH:mm`.
     - Count: integer 1–15.
  5. On submit: call `PutSettingsAsync` with updated value, refresh settings from response.
  6. Send test email: call `PostTestEmailAsync` (`POST /settings/test-email`), display success/error inline. This sends a simple plain-text verification email, not a recap.
  6a. Trigger recap (dev only): if the .NET environment is `Development`, show an additional option to call `PostTestRecapAsync` (`POST /dev/recap/trigger`). This option is hidden in non-Development environments.
  7. `KeyHints`: `"[↑↓] Navigate · [Enter] Edit · [T] Test email · [R] Refresh · [Esc] Back"`.
- [ ] T025 Write `src/Relego.Tests/Tui/SettingsScreenTests.cs`: test key handling: navigation, `Esc` returns `Pop`, edit mode activation. Test validation logic: invalid email rejected, invalid count rejected, valid values accepted.

**Checkpoint**: Pressing `S` from book list opens settings. Settings are displayed and editable. Test email can be triggered. `Esc` returns to book list. Tests pass.

---

## Phase 7: Polish, Edge Cases & Documentation

**Purpose**: Handle edge cases (terminal size, disconnected server, non-interactive terminal), update architecture docs, and run full test suite.

- [ ] T026 Implement terminal size check in `TuiApp.cs`: on each render cycle, check `Console.WindowWidth` and `Console.WindowHeight`. If below 80×24, render a centered message: "Terminal too small. Please resize to at least 80×24." instead of the normal layout.
- [ ] T027 Update `docs/ARCHITECTURE.md` and `README.md`: add the TUI to the architecture and refresh the project-level presentation in the README so the repository overview covers CLI mode, TUI mode, and the new SMTP verification flow.
- [ ] T028 Implement graceful server disconnection handling: when any `SunnyHttpClient` call throws `HttpRequestException` during a TUI action, catch it, update `StatusChrome.IsConnected = false`, and display an inline error message in the content area (e.g., "Cannot reach server. Check connection."). Do not crash the TUI.
- [ ] T029 Implement Figlet fallback in `StatusChrome`: if `Console.WindowWidth < 60`, render plain `Markup("[bold]Relego[/]")` instead of `FigletText`.
- [ ] T030 Implement clean exit in `TuiApp`: on quit (`Q` or `Ctrl+C`), restore `Console.TreatControlCAsInput = false`, clear the live display, and return control to the terminal cleanly.
- [ ] T031 Run full test suite: `dotnet test src/Relego.slnx`. Confirm no regressions across all existing parser, API, CLI, infrastructure, recap, and new settings-email API tests. All new TUI tests pass.

**Checkpoint**: All edge cases handled. Architecture docs updated. Full test suite green. TUI is feature-complete per spec.

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Infra) ──► Phase 2 (Chrome) ──► Phase 3 (Book List) ──┬──► Phase 4 (Highlight Detail)
                                                                 ├──► Phase 5 (Test Email API)
                                                                 └──► Phase 6 (Settings) ──► Phase 7 (Polish)
```

- **Phase 1 (Infra)**: No prerequisites — start immediately. **BLOCKS all subsequent phases.**
- **Phase 2 (Chrome)**: Depends on Phase 1 (`TuiApp` + `Layout` structure). **BLOCKS Phase 3** (chrome must exist before content screens).
- **Phase 3 (Book List)**: Depends on Phase 2 (chrome provides the layout context). **BLOCKS Phases 4, 5, and 6**.
- **Phase 4 (Highlight Detail)**: Depends on Phase 3. Parallel with Phase 5.
- **Phase 5 (Test Email API)**: Depends on Phase 3 for feature flow alignment. Parallel with Phase 4.
- **Phase 6 (Settings)**: Depends on Phase 3 and Phase 5.
- **Phase 7 (Polish)**: Depends on all preceding phases.

### Parallel Opportunities

Phases 4 and 5 are independent once Phase 3 is complete. Phase 6 depends on Phase 5 because the settings UI consumes the new test-email endpoint.
