# Research: Evolve CLI to TUI

**Feature**: 007-evolve-cli-to-tui
**Phase**: 0 — Research
**Date**: 2026-05-09

---

## Research Tasks & Findings

### 1. TUI Rendering Strategy (Terminal.Gui v2)

**Decision**: Use Terminal.Gui v2 (`Terminal.Gui` NuGet package, version 2.1.0) as the TUI framework

**Rationale**:
- Terminal.Gui v2 provides a native event loop (`Application.Run`), widget system (`View`, `Window`, `FrameView`, `Label`, `TableView`), and layout engine — removing the need for a custom render loop entirely.
- `IScreen.CreateView()` returns a Terminal.Gui `View` that is mounted into a `FrameView` on screen push/pop. The framework handles all re-rendering, cursor management, and keyboard dispatch.
- Screen transitions (push/pop) are implemented by replacing the content `FrameView`'s subview and calling `Application.Refresh()`.
- Terminal.Gui v2 provides correct handling of `Ctrl+C`, terminal resize, and raw/cooked mode across all target platforms (Windows, macOS, Linux).
- The `Application.Create()` → `_app.Init()` → `Application.Run(topLevel)` → `Application.Shutdown()` lifecycle integrates cleanly with `async/await` by running the event loop on the main thread.

**Key implementation details**:
- `Application.Create()` + `_app.Init()` set up the terminal driver before any view is created.
- Each `IScreen` produces a `View` via `CreateView(Action<ScreenResult> navigate)` and optionally a toolbar `View` via `CreateToolbarView(...)`.
- Navigation callbacks (`navigate(ScreenResult.Push(...))`, `navigate(ScreenResult.Pop())`) are invoked from within view key handlers.
- `Application.Shutdown()` is called in a `finally` block to guarantee terminal restoration on exit.

**Alternatives considered**:
- **Spectre.Console custom render loop**: `AnsiConsole.Live` + `Console.ReadKey(true)`. Spectre.Console does not provide a built-in event loop, keyboard dispatch, or widget system. A fully custom render loop would be required, adding significant complexity for scrolling, focus management, and modal overlays. Rejected in favour of Terminal.Gui.
- **Spectre.Console `Prompt`-based flow**: Sequential `SelectionPrompt` / `TextPrompt` screens. Does not support a persistent layout with always-visible chrome (each prompt clears the screen). Rejected for not meeting US-2.
- **Raw ANSI escape codes**: Full control but enormous effort and fragile cross-platform behaviour. Terminal.Gui already abstracts this.

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

**Decision**: Define an `IScreen` interface with `CreateView(Action<ScreenResult>)` returning a Terminal.Gui `View` and a `navigate` callback for screen transitions. The `TuiApp` orchestrator manages a screen stack.

**Rationale**:
- Each TUI page (book list, highlight detail, settings) has distinct layout, data, and key bindings. An interface-based approach provides clean separation.
- The screen stack pattern supports `Esc` = pop (go back) naturally. Pushing a new screen replaces the content frame; popping restores the previous view.
- Screens own their data fetching and state. The orchestrator only manages transitions and the chrome.
- This pattern is testable: screens can be unit-tested by verifying state mutations triggered by key bindings registered on the view.

**Interface shape** (as implemented):
```csharp
interface IScreen
{
    View CreateView(Action<ScreenResult> navigate);
    View? CreateToolbarView(Action<ScreenResult> navigate) => null;
    int ToolbarHeight => 0;
    Task InitializeAsync(CancellationToken cancellationToken);
    string Title { get; }
    IReadOnlyList<(string Key, string Label)> KeyHints { get; }
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
