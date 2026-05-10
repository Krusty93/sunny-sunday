using Spectre.Console;
using Spectre.Console.Rendering;

namespace SunnySunday.Cli.Tui;

public sealed class TuiApp(SunnySunday.Cli.Infrastructure.SunnyHttpClient client, string serverUrl, string version)
{
    private readonly SunnySunday.Cli.Infrastructure.SunnyHttpClient _client = client;
    private readonly Stack<IScreen> _screens = new();

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _ = _client;

        if (_screens.Count == 0)
        {
            var initialScreen = new PlaceholderScreen();
            await initialScreen.InitializeAsync(cancellationToken).ConfigureAwait(false);
            _screens.Push(initialScreen);
        }

        var layout = new Layout("Root");
        layout.SplitRows(
            new Layout("Chrome"),
            new Layout("Content"),
            new Layout("KeyHints"));

        var previousTreatControlCAsInput = Console.TreatControlCAsInput;

        try
        {
            Console.TreatControlCAsInput = true;

            await AnsiConsole.Live(layout).StartAsync(async context =>
            {
                while (!cancellationToken.IsCancellationRequested && _screens.Count > 0)
                {
                    var currentScreen = _screens.Peek();

                    layout["Chrome"].Update(BuildChrome());
                    layout["Content"].Update(BuildContent(currentScreen));
                    layout["KeyHints"].Update(new Markup($"[grey]{Markup.Escape(currentScreen.KeyHints)}[/]"));
                    context.Refresh();

                    var key = Console.ReadKey(intercept: true);
                    var result = await currentScreen.HandleKeyAsync(key, cancellationToken).ConfigureAwait(false);
                    await HandleScreenResultAsync(result, cancellationToken).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            Console.TreatControlCAsInput = previousTreatControlCAsInput;
        }
    }

    private IRenderable BuildChrome()
        => new Rows(
            new Markup("[bold]SunnySunday[/]"),
            new Markup($"[grey]v{Markup.Escape(version)}[/]"),
            new Markup($"[grey]{Markup.Escape(serverUrl)}[/]"));

    private static IRenderable BuildContent(IScreen currentScreen)
    {
        if (Console.WindowWidth < 80 || Console.WindowHeight < 24)
        {
            return new Panel(new Markup("[yellow]Terminal too small. Please resize to at least 80x24.[/]"));
        }

        return currentScreen.Render();
    }

    private async Task HandleScreenResultAsync(ScreenResult result, CancellationToken cancellationToken)
    {
        switch (result.Action)
        {
            case ScreenAction.None:
                return;
            case ScreenAction.Push:
                if (result.Next is null)
                {
                    throw new InvalidOperationException("Push requires a next screen instance.");
                }

                await result.Next.InitializeAsync(cancellationToken).ConfigureAwait(false);
                _screens.Push(result.Next);
                return;
            case ScreenAction.Pop:
                if (_screens.Count > 1)
                {
                    _screens.Pop();
                }

                return;
            case ScreenAction.Quit:
                _screens.Clear();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(result.Action), result.Action, null);
        }
    }

    private sealed class PlaceholderScreen : IScreen
    {
        public string KeyHints => "[Q] Quit";

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public IRenderable Render()
            => new Panel(new Markup("[grey]TUI bootstrap ready. Phase 1 placeholder screen.[/]"))
            {
                Header = new PanelHeader("Phase 1")
            };

        public Task<ScreenResult> HandleKeyAsync(ConsoleKeyInfo key, CancellationToken cancellationToken)
        {
            var isQuit = key.Key == ConsoleKey.Q
                || (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control));

            return Task.FromResult(isQuit ? ScreenResult.Quit() : ScreenResult.Stay());
        }
    }
}
