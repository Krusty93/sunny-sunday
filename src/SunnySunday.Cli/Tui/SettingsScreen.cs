using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Cli.Tui;

public sealed class SettingsScreen(SunnyHttpClient client) : IScreen
{
    private readonly SunnyHttpClient _client = client;

    public string Title => "Settings";

    public IReadOnlyList<(string Key, string Label)> KeyHints =>
    [
        ("Esc", "Back"),
        ("Ctrl+C", "Quit")
    ];

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Views are owned by the parent container hierarchy")]
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

        var stubLabel = new Label
        {
            Text = "Settings screen is ready for the next implementation phase.",
            X = Pos.Center(),
            Y = Pos.Center()
        };

        container.Add(stubLabel);

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
