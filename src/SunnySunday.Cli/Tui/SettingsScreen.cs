using Spectre.Console;
using Spectre.Console.Rendering;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Cli.Tui;

public sealed class SettingsScreen(SunnyHttpClient client) : IScreen
{
    private readonly SunnyHttpClient _client = client;

    public string KeyHints => "[Esc] Back";

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public IRenderable Render()
        => new Panel(new Markup("[grey]Settings screen is ready for the next implementation phase.[/]"))
        {
            Header = new PanelHeader("Settings")
        };

    public Task<ScreenResult> HandleKeyAsync(ConsoleKeyInfo key, CancellationToken cancellationToken)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            return Task.FromResult(ScreenResult.Pop());
        }

        if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            return Task.FromResult(ScreenResult.Quit());
        }

        return Task.FromResult(ScreenResult.Stay());
    }
}
