using Spectre.Console;
using Spectre.Console.Rendering;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Cli.Tui.ViewModels;

namespace SunnySunday.Cli.Tui;

public sealed class HighlightDetailScreen(BookViewModel book, SunnyHttpClient client) : IScreen
{
    private readonly SunnyHttpClient _client = client;

    public BookViewModel Book { get; } = book;

    public string KeyHints => "[Esc] Back";

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public IRenderable Render()
        => new Panel(new Markup($"[bold]{Markup.Escape(Book.Title)}[/]\n[grey]{Markup.Escape(Book.Author)}[/]\n\n[grey]Highlight detail screen is ready for the next implementation phase.[/]"))
        {
            Header = new PanelHeader("Highlights")
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
