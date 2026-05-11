# Tasks: Evolve CLI to TUI

**Input**: Design documents from `/specs/007-evolve-cli-to-tui/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: Included — xUnit tests for data/logic layer (mode detection, book grouping, search filter, screen key handling). Rendering output is NOT tested directly per research decision (Terminal.Gui's `Application.Run` requires a real terminal driver; unit tests target logic and state only).

**Organization**: Tasks are grouped by user story so each slice stays independently testable after the shared foundation is in place. The plan defines 7 phases matching 8 user stories: Phase 1 covers US1 (Dual-Mode) + US8 (Render Loop) as foundational infrastructure, Phase 2 covers US2 (Chrome), Phase 3 covers US3 (Book List) + US7 (Search), Phase 4 covers US4 (Highlight Detail), Phase 5 covers US5 (Test Email API), Phase 6 covers US6 (Settings), and Phase 7 handles polish and edge cases.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: Which user story the task belongs to (`US1`–`US8`)
- All file paths are relative to the repository root

---

## Phase 1: Dual-Mode Detection & Render Loop Infrastructure

**Purpose**: Establish the TUI/CLI mode split in `Program.cs` and build the core render loop (`TuiApp`) with screen abstraction, view models, and HTTP client extensions. After this phase, `sunny` (no args) shows a minimal TUI with the render loop running, and `sunny status` still works exactly as before. Covers US1 (Dual-Mode Launch) and US8 (TUI Rendering Approach).

- [X] T001 [P] [US8] Create `src/SunnySunday.Cli/Tui/IScreen.cs`: define `IScreen` interface with `Render() → IRenderable`, `HandleKeyAsync(ConsoleKeyInfo, CancellationToken) → Task<ScreenResult>`, `InitializeAsync(CancellationToken) → Task`, `KeyHints → string`. Define `ScreenAction` enum (`None`, `Push`, `Pop`, `Quit`) and `ScreenResult` record. Namespace `SunnySunday.Cli.Tui`.
- [X] T002 [P] [US8] Create `src/SunnySunday.Cli/Tui/ViewModels/BookViewModel.cs`: record with `Title`, `Author`, `HighlightCount`, `IReadOnlyList<HighlightViewModel> Highlights`. Add static factory method `FromHighlights(IEnumerable<HighlightItemDto> items) → List<BookViewModel>` that groups by `(BookTitle, AuthorName)`.
- [X] T003 [P] [US8] Create `src/SunnySunday.Cli/Tui/ViewModels/HighlightViewModel.cs`: record with `Id`, `Text`, `BookTitle`, `AuthorName`, `IsExcluded`, `Weight`.
- [X] T004 [US8] Create `src/SunnySunday.Cli/Tui/TuiApp.cs`: main orchestrator class. Constructor takes `SunnyHttpClient`, `string serverUrl`, `string version`. Initializes Terminal.Gui `Application`, creates a root `Window` with chrome, content `FrameView`, and footer bar. Manages a `Stack<IScreen>`. Runs the event loop via `Application.Run`. Handles `Q` and `Ctrl+C` to stop cleanly. Public method: `Task RunAsync(CancellationToken ct)`.
- [X] T005 [US1] Modify `src/SunnySunday.Cli/Program.cs`: add TUI mode detection **before** `CommandApp` creation. Logic: if `args.Length == 0 && !Console.IsInputRedirected`, create `TuiApp` and call `RunAsync`; if `args.Length == 0 && Console.IsInputRedirected`, fall through to `CommandApp` (shows help); if `args.Length > 0`, fall through to `CommandApp` (existing behavior).
- [X] T006 [P] Add `GetHighlightsAsync(int page, int pageSize, string? query, CancellationToken ct)` method to `src/SunnySunday.Cli/Infrastructure/SunnyHttpClient.cs`: calls `GET /highlights?page={page}&pageSize={pageSize}&q={query}`, returns `HighlightsResponse`. Add `PostTestEmailAsync(CancellationToken ct)` method: calls `POST /settings/test-email`, returns `HttpResponseMessage` (sends a simple plain-text test email, not a recap). Add `DeleteHighlightAsync(int id, CancellationToken ct)` method: calls `DELETE /highlights/{id}`, returns `HttpResponseMessage`. Add `PingAsync(CancellationToken ct)` method: calls `GET /`, returns `bool` (true if HTTP 200, false otherwise). Keep existing `PostTestRecapAsync` (calls `POST /dev/recap/trigger`) for dev-only use.
- [X] T007 [P] Update `src/SunnySunday.Cli/Infrastructure/SunnyJsonContext.cs`: add `[JsonSerializable(typeof(HighlightsResponse))]` and `[JsonSerializable(typeof(HighlightItemDto))]` for trimming compatibility.
- [X] T008 [P] [US1] Write `src/SunnySunday.Tests/Tui/ModeDetectionTests.cs`: test that empty args + interactive terminal → TUI mode, empty args + piped stdin → CLI mode (help), args present → CLI mode. Test the detection logic extracted as a static helper method.
- [X] T009 [P] [US8] Write `src/SunnySunday.Tests/Tui/BookGroupingTests.cs`: test `BookViewModel.FromHighlights()`: multiple highlights from same book → single `BookViewModel` with correct count, different books → separate entries, empty list → empty result, highlights with same title but different author → separate books.

**Checkpoint**: `sunny` (no args, interactive terminal) enters a render loop showing a placeholder screen. `sunny status` works as before. `dotnet build src/SunnySunday.slnx` and `dotnet test --filter "FullyQualifiedName~Tui"` pass.

---

## Phase 2: Status Chrome (Figlet, Version, Connection, Warnings)

**Purpose**: Build the persistent header region (`StatusChrome`) that appears on every TUI screen: Figlet banner, version, server connection status, and Kindle email warning. Covers US2 (Startup Splash and Always-Visible Chrome).

- [X] T010 [US2] Create `src/SunnySunday.Cli/Tui/StatusChrome.cs`: class that produces a chrome `View`. Constructor takes `string serverUrl`, `string version`. `RefreshAsync(SunnyHttpClient, CancellationToken)` calls `PingAsync` (`GET /`) to check connection; if connected, calls `GetSettingsAsync` to check `KindleEmailConfigured`; catches `HttpRequestException` → disconnected state. `Render()` builds a Terminal.Gui `View` with: ASCII art "SUNNY" banner with colored `Label` rows, version line, connection status (green/red), conditional Kindle email warning (yellow). Exposes `IsConnected` and `KindleEmailConfigured` properties.
- [X] T011 [US2] Wire `StatusChrome` into `TuiApp.cs`: create `StatusChrome` in constructor, call `RefreshAsync` on startup and after each failed API call. Add the chrome `View` to the root window.
- [X] T012 [US2] Add key hint bar rendering to `TuiApp.cs`: render a footer `View` with key hint `Label` pairs sourced from `currentScreen.KeyHints`. Style with dim/grey color.

**Checkpoint**: TUI displays Figlet banner, version, connection status, and (if applicable) Kindle email warning. These persist when navigating between screens.

---

## Phase 3: Book List Screen & Client-Side Search

**Purpose**: Implement the main landing screen showing all imported books in a navigable table, plus the client-side search filter. Covers US3 (Book List Main Screen) and US7 (Client-Side Search).

- [X] T013 [US3] Create `src/SunnySunday.Cli/Tui/BookListScreen.cs`: implements `IScreen`. State: `List<BookViewModel> books`, `List<BookViewModel> filteredBooks`, `int selectedIndex`, `bool isSearchActive`, `string searchQuery`. `InitializeAsync`: fetch all highlights from server (paginate through `GetHighlightsAsync`), group into `BookViewModel` list via `BookViewModel.FromHighlights()`, enrich with exclusions and weights. `CreateView`: build Terminal.Gui `TableView` with Title, Author, Highlights columns; highlight selected row; show empty state if no books. Key bindings: `↑/↓` = navigate, `Enter` = `Push(HighlightDetailScreen)`, `S` = `Push(SettingsScreen)`, `/` = activate search, `R` = refresh, `Q`/`Ctrl+C` = `Quit`. `KeyHints`: `[("\u2191\u2193", "Navigate"), ("Enter", "View"), ("S", "Settings"), ("/", "Search"), ("R", "Refresh"), ("Q", "Quit")]`. Constructor takes `SunnyHttpClient`.
- [X] T014 [P] [US7] Create `src/SunnySunday.Cli/Tui/SearchFilter.cs`: static class with method `List<BookViewModel> Apply(IEnumerable<BookViewModel> books, string query)`. Filters books where title, author, or any highlight text contains the query (case-insensitive, `OrdinalIgnoreCase`). Returns matching books with all their highlights.
- [X] T015 [US7] Integrate search into `BookListScreen`: when search is active, keystrokes append to `searchQuery`, `Esc` clears search, `Backspace` removes last character. After each keystroke, apply `SearchFilter.Apply()` to `filteredBooks` and reset `selectedIndex` to 0. `Render()` uses `filteredBooks` when search is active. Show search input line above table. Show "No results matching '[query]'" for empty results.
- [X] T016 [US3] Wire `BookListScreen` as the initial screen in `TuiApp.RunAsync()`: push `BookListScreen` onto the screen stack on startup.
- [X] T017 [P] [US7] Write `src/SunnySunday.Tests/Tui/SearchFilterTests.cs`: test match by title, match by author, match by highlight text, case-insensitive match, no match returns empty, empty query returns all, partial match works.
- [X] T018 [P] [US3] Write `src/SunnySunday.Tests/Tui/BookListScreenTests.cs`: test key handling: `↑/↓` changes `selectedIndex` within bounds, `Enter` returns `Push` with correct book, `Q` returns `Quit`, `/` activates search mode. Test empty state when no books loaded.

**Checkpoint**: TUI launches with book list populated from server. Arrow keys navigate rows. Search filters the list. `Enter` attempts to push highlight detail screen (implemented next phase). Tests pass.

---

## Phase 4: Highlight Detail Screen

**Purpose**: Implement the drill-down view for a selected book, showing its highlights with an action menu for weight, exclusion, and deletion operations. Covers US4 (Highlight Detail Menu).

- [X] T019 [US4] Create `src/SunnySunday.Cli/Tui/HighlightDetailScreen.cs`: implements `IScreen`. Constructor takes `BookViewModel book`, `SunnyHttpClient client`. State: `int selectedIndex`, `bool actionMenuOpen`, `int actionMenuIndex`. `InitializeAsync`: no-op (data from `BookViewModel`). `Render()`: `Panel` with book title header, list/table of highlights with text (truncated), weight indicator, exclusion indicator; highlight selected row; overlay action menu when open (Modify weight, Exclude/Include highlight, Exclude/Include book, Exclude/Include author, Delete highlight). `HandleKeyAsync`: normal mode `↑/↓` navigate, `Enter` opens menu, `R` = refresh (re-initialize screen data from server), `Esc` returns `Pop`; action menu mode `↑/↓` navigate actions, `Enter` executes action, `Esc` closes menu. Delete action requires confirmation (Y/N). `KeyHints`: `"[↑↓] Navigate · [Enter] Actions · [R] Refresh · [Esc] Back"`.
- [X] T020 [P] [US4] Write `src/SunnySunday.Tests/Tui/HighlightDetailScreenTests.cs`: test navigation within bounds, `Enter` opens action menu, `Esc` from action menu closes it, `Esc` from normal mode returns `Pop`, action menu index navigation.

**Checkpoint**: Selecting a book in the book list opens the highlight detail view. Actions (weight, exclude, delete) call the server API. `Esc` returns to book list. Tests pass.

---

## Phase 5: Test Email API Endpoint

**Purpose**: Add the missing server endpoint required by the TUI settings page to verify SMTP configuration with a plain-text test email. Covers US5 (Test Email Verification Endpoint).

- [X] T021 [US5] Update `src/SunnySunday.Server/Services/IMailDeliveryService.cs` to add a plain-text test email contract, and implement it in `src/SunnySunday.Server/Services/MailDeliveryService.cs` and `src/SunnySunday.Server/Services/DevMailDeliveryService.cs` without recap attachments.
- [X] T022 [P] [US5] Update `src/SunnySunday.Server/Endpoints/SettingsEndpoints.cs` to add `POST /settings/test-email` with success, validation, and SMTP failure handling.
- [X] T023 [P] [US5] Create `src/SunnySunday.Tests/Api/SettingsTestEmailEndpointTests.cs` covering successful send, missing Kindle email, and SMTP failure.

**Checkpoint**: `POST /settings/test-email` is implemented and tested before the TUI settings screen consumes it.

---

## Phase 6: Settings Screen

**Purpose**: Implement the settings page for editing Kindle email, recap schedule, highlight count, and sending a test email. Covers US6 (Settings Page).

- [X] T024 [US6] Create `src/SunnySunday.Cli/Tui/SettingsScreen.cs`: implements `IScreen`. Constructor takes `SunnyHttpClient client`, `bool isDevelopment`. State: `SettingsResponse settings`, `int selectedField`, `bool isEditing`, `string editBuffer`. `InitializeAsync`: call `GetSettingsAsync()`. `Render()`: panel/table showing Kindle Email, Schedule (cadence + time + timezone), Highlights per recap, Send test email action, and (if `isDevelopment`) Trigger recap (dev) action; highlight selected field; show inline text input when editing. `HandleKeyAsync`: normal mode `↑/↓` navigate, `Enter` edit/trigger, `T` send test email (`POST /settings/test-email` — simple plain-text, not a recap), `R` = refresh (re-fetch settings), `Esc` returns `Pop`; edit mode keystrokes append to `editBuffer`, `Enter` validates and submits, `Esc` cancels. Validation: email contains `@`, cadence is `daily`/`weekly`, time matches `HH:mm`, count is integer 1–15. On submit: call `PutSettingsAsync`, refresh from response. Test email: call `PostTestEmailAsync`, display result inline. Trigger recap (dev only): call `PostTestRecapAsync`, display result inline. `KeyHints`: `"[↑↓] Navigate · [Enter] Edit · [T] Test email · [R] Refresh · [Esc] Back"`.
- [X] T025 [P] [US6] Write `src/SunnySunday.Tests/Tui/SettingsScreenTests.cs`: test key handling: navigation, `Esc` returns `Pop`, edit mode activation. Test validation: invalid email rejected, invalid count rejected, valid values accepted.

**Checkpoint**: Pressing `S` from book list opens settings. Settings are displayed and editable. Test email can be triggered. `Esc` returns to book list. Tests pass.

---

## Phase 7: Polish, Edge Cases & Documentation

**Purpose**: Handle edge cases (terminal size, disconnected server, non-interactive terminal), update architecture docs, and run full test suite.

- [ ] T026 [P] Implement terminal size check in `src/SunnySunday.Cli/Tui/TuiApp.cs`: on startup, check `Application.Screen.Bounds`. If below 80×24, render a centered message: "Terminal too small. Please resize to at least 80×24." instead of the normal layout.
- [ ] T027 [P] Update `docs/ARCHITECTURE.md` and `README.md`: add the TUI architecture section and refresh the repository-level project presentation so the README reflects CLI mode, TUI mode, and SMTP verification capabilities.
- [ ] T028 [P] Implement graceful server disconnection handling in TUI screens: when any `SunnyHttpClient` call throws `HttpRequestException` during a TUI action, catch it, update `StatusChrome.IsConnected = false`, and display an inline error message in the content area (e.g., "Cannot reach server. Check connection."). Do not crash the TUI.
- [ ] T029 [P] Verify ASCII art banner in `src/SunnySunday.Cli/Tui/StatusChrome.cs` renders correctly at narrow terminal widths; confirm that the banner truncates gracefully without errors when the terminal is narrower than the banner width.
- [ ] T030 Implement clean exit in `src/SunnySunday.Cli/Tui/TuiApp.cs`: on quit (`Q` or `Ctrl+C`), call `Application.RequestStop()` and ensure `Application.Shutdown()` is called in the `finally` block to restore terminal state cleanly.
- [ ] T031 Run full test suite: `dotnet test src/SunnySunday.slnx`. Confirm no regressions across all existing parser, API, CLI, infrastructure, recap, and settings test-email API tests. All new TUI tests pass.

**Checkpoint**: All edge cases handled. Architecture docs updated. Full test suite green. TUI is feature-complete per spec.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Infra)**: No dependencies — start immediately. **BLOCKS all subsequent phases.**
- **Phase 2 (Chrome)**: Depends on Phase 1 (`TuiApp` + `Layout` structure). **BLOCKS Phase 3** (chrome must exist before content screens).
- **Phase 3 (Book List + Search)**: Depends on Phase 2 (chrome provides the layout context). **BLOCKS Phases 4, 5, and 6.**
- **Phase 4 (Highlight Detail)**: Depends on Phase 3. **Parallel with Phase 5.**
- **Phase 5 (Test Email API)**: Depends on Phase 3. **Parallel with Phase 4.**
- **Phase 6 (Settings)**: Depends on Phase 3 and Phase 5.
- **Phase 7 (Polish)**: Depends on all preceding phases.

### User Story Dependencies

- **US1 (Dual-Mode Launch)**: Phase 1 — foundational, blocks everything.
- **US8 (Render Loop)**: Phase 1 — foundational, blocks everything.
- **US2 (Chrome)**: Phase 2 — depends on render loop from Phase 1.
- **US3 (Book List)**: Phase 3 — depends on chrome from Phase 2.
- **US7 (Search)**: Phase 3 — co-located with book list, depends on chrome from Phase 2.
- **US4 (Highlight Detail)**: Phase 4 — depends on book list from Phase 3. Independent of US5.
- **US5 (Test Email API)**: Phase 5 — depends on book list from Phase 3 for feature sequencing. Independent of US4.
- **US6 (Settings)**: Phase 6 — depends on book list from Phase 3 and the API endpoint from Phase 5.

### Parallel Opportunities

- Within Phase 1: `T001`, `T002`, `T003` (interface + view models) can run in parallel. `T006`, `T007` (HTTP client) can run in parallel. `T008`, `T009` (tests) can run in parallel.
- Within Phase 3: `T014` (SearchFilter) and `T017`, `T018` (tests) can run in parallel with each other.
- Phases 4 and 5 are fully independent once Phase 3 is complete.
- Within Phase 7: `T026`, `T027`, `T028`, `T029` can run in parallel (different files/concerns).

---

## Parallel Example: Phase 1

```text
T001 ──┐
T002 ──┼──► T004 ──► T005
T003 ──┘         ▲
T006 ────────────┘
T007 ────────────┘
T008 (parallel, after T005)
T009 (parallel, after T002)
```

## Parallel Example: Phases 4 & 5 After Phase 3

```text
Phase 3 ──┬──► Phase 4 (T019) ──┐
           └──► Phase 5 (T021, T022, T023) ──► Phase 6 (T024, T025) ──► Phase 7
```

---

## Implementation Strategy

### Incremental Delivery

1. Complete Phase 1 (Infra) — mode detection, render loop, view models, HTTP extensions.
2. Complete Phase 2 (Chrome) — branded header with connection status.
3. Complete Phase 3 (Book List + Search) — main screen with navigation and filtering.
4. Land Phase 4 (Highlight Detail) and Phase 5 (Test Email API) in parallel or either order.
5. Complete Phase 6 (Settings) once the API endpoint exists.
6. Finish with Phase 7 (Polish) — edge cases, docs, full regression suite.

### Suggested MVP Scope

The smallest demonstrable increment is **Phase 1 + Phase 2 + Phase 3** — a user can launch `sunny` with no arguments, see a branded TUI with the book list, navigate rows, and search. All existing CLI commands remain unchanged.

---

## Notes

- Terminal.Gui 2.1.0 added as a new NuGet dependency for TUI rendering. Spectre.Console.Cli is retained for CLI command routing.
- One targeted server-side change is included: `POST /settings/test-email` plus the supporting mail-delivery contract.
- Testing strategy focuses on data/logic layer (mode detection, grouping, search, key handling). Terminal.Gui rendering is NOT unit-tested directly per research decision.
- All new source files live in `src/SunnySunday.Cli/Tui/` and `src/SunnySunday.Tests/Tui/`.
- View models are stateless projections of API responses — no local persistence beyond session memory.
