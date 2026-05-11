using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Cli.Tui.ViewModels;

namespace SunnySunday.Cli.Tui;

public sealed class HighlightDetailScreen(BookViewModel book, SunnyHttpClient client) : IScreen
{
    private readonly SunnyHttpClient _client = client;

    public BookViewModel Book { get; } = book;

    public string Title => $"Highlights - {Book.Title}";

    public IReadOnlyList<(string Key, string Label)> KeyHints =>
    [
        ("Esc", "Back"),
        ("Ctrl+C", "Quit")
    ];

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public View CreateView(Action<ScreenResult> navigate)
    {
        var container = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        var titleLabel = new Label
        {
            Text = Book.Title,
            X = 0,
            Y = 0
        };

        var authorLabel = new Label
        {
            Text = Book.Author,
            X = 0,
            Y = 1
        };

        var stubLabel = new Label
        {
            Text = "Highlight detail screen is ready for the next implementation phase.",
            X = 0,
            Y = 3
        };

        container.Add(titleLabel, authorLabel, stubLabel);

        container.KeyDown += (_, key) =>
        {
            if (key.KeyCode == KeyCode.Esc)
            {
                navigate(ScreenResult.Pop());
                key.Handled = true;
            }
            else if (key.KeyCode == (KeyCode.C | KeyCode.CtrlMask))
            {
                navigate(ScreenResult.Quit());
                key.Handled = true;
            }
        };

        return container;
    }
}
