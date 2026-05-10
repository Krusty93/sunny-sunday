# Research: Evolve CLI to TUI

**Feature**: 007-evolve-cli-to-tui
**Phase**: 0 — Research
**Date**: 2026-05-09

---

## Research Tasks & Findings

### 1. Spectre.Console TUI Rendering Strategy (Custom Render Loop)

**Decision**: Build a custom render loop using `AnsiConsole.Live` + `Console.ReadKey(true)` to create a persistent interactive TUI within Spectre.Console

**Rationale**:
- Spectre.Console 0.55.0 provides `Layout`, `Panel`, `Table`, `FigletText`, and `LiveDisplay` — all the building blocks needed for a persistent TUI.
- Spectre.Console does **not** provide a built-in event loop, persistent keyboard input handling, or screen management. These must be implemented manually.
- The pattern: `AnsiConsole.Live(rootLayout).StartAsync(async ctx => { while (!quit) { key = Console.ReadKey(true); handle(key); ctx.Refresh(); } })`.
- `LiveDisplay` manages cursor positioning and differential updates, minimizing flicker. The `ctx.Refresh()` call triggers a re-render of the entire layout tree.
- `Layout` provides split regions (e.g., top = chrome, center = content, bottom = key hints). Each region can be independently updated by swapping its `IRenderable` content.
- Terminal raw mode is entered implicitly when using `Console.ReadKey(true)` (intercept mode — keys are not echoed).

**Key implementation details**:
- `Console.ReadKey(true)` is blocking — the render loop runs on the main thread, alternating between waiting for input and refreshing.
- `Ctrl+C` must be handled explicitly: set `Console.TreatControlCAsInput = true` at TUI start, restore on exit.
- `CancellationTokenSource` for coordinating shutdown — signal cancellation on `Q` or `Ctrl+C`, then exit the loop.
- Screen transitions: each "screen" produces an `IRenderable` for the content area. Switching screens = swapping the content panel in the layout.

**Alternatives considered**:
- **Terminal.Gui**: Full TUI framework with event loop, widgets, and layout engine. However, adding it violates FR-007-14 (Spectre.Console only) and constitution principle VI (YAGNI). It would also conflict with Spectre's ANSI output.
- **gui.cs**: Same as Terminal.Gui (renamed). Same concerns apply.
- **Spectre.Console `Prompt`-based flow**: Using sequential prompts (SelectionPrompt, TextPrompt) for each screen. Simpler but does not provide a persistent layout with always-visible chrome — each prompt clears the screen. Rejected for not meeting US-2 (persistent banner).
- **Raw ANSI escape codes**: Full control but enormous effort and fragile cross-platform behavior. Spectre.Console already abstracts this.

---

### 2. Dual-Mode Detection (TUI vs CLI)

**Decision**: Detect mode by checking whether `args` contains a known command before passing to `CommandApp`

**Rationale**:
- Spectre.Console.Cli's `CommandApp.RunAsync(args)` handles command routing internally. When `args` is empty, it shows help text by default.
- For TUI mode, we intercept empty args **before** calling `CommandApp`. The detection logic:
  1. If `args.Length == 0` and `Console.IsInputRedirected == false` → start TUI.
  2. If `args.Length == 0` and `Console.IsInputRedirected == true` → show help (FR-007-20).
  3. If `args.Length > 0` → delegate to `CommandApp.RunAsync(args)` (existing CLI behavior).
- This approach requires **zero changes** to the existing `CommandApp` configuration or any command classes.
- Edge cases: `--help` and `--version` are handled by `CommandApp` internally (args.Length > 0), so they bypass TUI detection correctly.

**Alternatives considered**:
- **Custom default command**: Register a `TuiCommand` as the default command in `CommandApp`. Rejected because Spectre.Console.Cli default commands still require explicit registration and the Settings class mechanism doesn't fit a persistent TUI loop.
- **Subclass `CommandApp`**: Override `RunAsync` to intercept empty args. More invasive and couples TUI logic to Spectre internals.

---

### 3. Screen Architecture Pattern

**Decision**: Define an `IScreen` interface with `Render()` returning `IRenderable` and `HandleKeyAsync()` returning a screen transition result. The `TuiApp` orchestrator manages a screen stack.

**Rationale**:
- Each TUI page (book list, highlight detail, settings) has distinct layout, data, and key bindings. An interface-based approach provides clean separation.
- The screen stack pattern supports `Esc` = pop (go back) naturally. Pushing a new screen overlays the previous; popping restores it.
- Screens own their data fetching and state. The orchestrator only manages transitions and the chrome.
- This pattern is testable: screens can be unit-tested by calling `HandleKeyAsync` with synthetic `ConsoleKeyInfo` values and asserting the returned transition or state change.

**Interface shape**:
```csharp
interface IScreen
{
    IRenderable Render();
    Task<ScreenResult> HandleKeyAsync(ConsoleKeyInfo key, CancellationToken ct);
    Task InitializeAsync(CancellationToken ct); // data loading on screen entry
}

enum ScreenAction { None, Push, Pop, Quit }
record ScreenResult(ScreenAction Action, IScreen? Next = null);
```

**Alternatives considered**:
- **State machine with enum**: A single switch-case on `CurrentScreen` enum. Simpler for 3 screens but doesn't scale and mixes all screen logic in one class.
- **Full MVVM**: ViewModel + View separation. Over-engineered for a CLI TUI with 3 screens and no data binding.

---

### 4. Data Fetching for Book List

**Decision**: Use the existing `GET /highlights?page=1&pageSize=200` endpoint to fetch all highlights, then group client-side by book title + author to produce the book list

**Rationale**:
- There is no dedicated `/books` endpoint on the server. The server exposes highlights with book title and author name embedded in each `HighlightItemDto`.
- For the MVP single-user scenario, fetching all highlights (up to a few hundred) and grouping client-side is simple and sufficient.
- The `HighlightsResponse` provides `Total` for pagination. If `Total > pageSize`, the TUI fetches additional pages until all highlights are loaded.
- Client-side grouping produces `BookViewModel { Title, Author, HighlightCount, Highlights[] }` — exactly what the book list table needs.
- The same fetched data serves both the book list (grouped) and the highlight detail view (filtered by book) — no second API call needed when drilling into a book.
- Search (US-6) also operates on this client-side cache, filtering by title, author, and highlight text.

**Alternatives considered**:
- **New `/books` endpoint**: Would require server-side changes, violating the spec constraint "no API changes" and FR-007-18.
- **Fetch on demand per book**: Would require a book ID-based endpoint that doesn't exist. Also slower for the book list view.

---

### 5. SunnyHttpClient Extensions for TUI

**Decision**: Add `GetHighlightsAsync`, `PostTestEmailAsync`, `PingAsync`, and `DeleteHighlightAsync` methods to the existing `SunnyHttpClient`

**Rationale**:
- The TUI needs several API calls not currently in `SunnyHttpClient`:
  1. `GET /highlights` (paginated) — needed for the book list and highlight detail views.
  2. `POST /settings/test-email` — needed for the "send test email" action in settings. Sends a simple plain-text verification email, not a recap. **This endpoint does not yet exist on the server** and is documented as a needed addition on issue #188.
  3. `GET /` — needed for connectivity checks (HTTP 200 = connected). Used by `StatusChrome` to determine server reachability.
  4. `DELETE /highlights/{id}` — needed for the highlight deletion action.
  5. `POST /dev/recap/trigger` — existing endpoint for triggering a recap, available only when the .NET environment is `Development`. The TUI hides this action in non-Development environments.
- Adding methods to the existing typed client keeps the HTTP layer consolidated. No new HTTP client class needed.
- The `SunnyJsonContext` needs `HighlightsResponse` and `HighlightItemDto` added for trimming compatibility.

**Alternatives considered**:
- **Separate `TuiHttpClient`**: Unnecessary duplication. The existing client already handles auth-free REST calls with retry.

---

### 6. Testing Strategy for TUI Components

**Decision**: Test the data/logic layer (screen models, search filter, mode detection) with xUnit. Do not attempt to test Spectre.Console rendering output.

**Rationale**:
- Spectre.Console's `LiveDisplay` writes directly to the console via ANSI sequences. Capturing and asserting rendered output is fragile and environment-dependent.
- The testable surface is:
  1. **Mode detection**: Given args, does the app enter TUI mode or CLI mode?
  2. **Book grouping**: Given a list of `HighlightItemDto`, does grouping produce correct `BookViewModel` entries?
  3. **Search filter**: Given a query and book list, does the filter return correct matches?
  4. **Screen key handling**: Given a `ConsoleKeyInfo`, does the screen return the correct `ScreenResult`?
  5. **StatusChrome data**: Given a `StatusResponse` and `SettingsResponse`, does the chrome model produce correct status text?
- Integration tests with `Spectre.Console.Testing.TestConsole` are possible for simple rendering but not for `LiveDisplay`. We skip rendering tests.

**Alternatives considered**:
- **Snapshot testing with `TestConsole`**: Spectre's `TestConsole` can capture output from `AnsiConsole.Write()` calls, but `LiveDisplay` bypasses it. Only useful for isolated renderable tests, not the full TUI.
- **End-to-end process testing**: Launch the CLI as a subprocess and send keystrokes via stdin. Extremely fragile and slow. Rejected.
