using Spectre.Console;
using Spectre.Console.Rendering;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Cli.Tui;

/// <summary>
/// Produces the persistent header region rendered above every TUI screen.
/// Shows a Figlet banner, version, server connection status, and an optional
/// Kindle email configuration warning.
/// </summary>
public sealed class StatusChrome(string serverUrl, string version)
{
    public bool IsConnected { get; private set; }
    public bool KindleEmailConfigured { get; private set; }

    /// <summary>
    /// Refreshes connection and settings state from the server.
    /// Safe to call after any failed API call to update the chrome status.
    /// </summary>
    public async Task RefreshAsync(SunnyHttpClient client, CancellationToken ct = default)
    {
        try
        {
            IsConnected = await client.PingAsync(ct).ConfigureAwait(false);
            if (IsConnected)
            {
                var settings = await client.GetSettingsAsync(ct).ConfigureAwait(false);
                KindleEmailConfigured = !string.IsNullOrWhiteSpace(settings.KindleEmail);
            }
            else
            {
                KindleEmailConfigured = false;
            }
        }
        catch (HttpRequestException)
        {
            IsConnected = false;
            KindleEmailConfigured = false;
        }
    }

    /// <summary>
    /// Plays a brief colour-fade animation on the Figlet banner at startup.
    /// Cycles through a blue-to-cyan gradient inspired by GitHub Copilot CLI.
    /// No-ops when the terminal is too narrow for Figlet or cancellation is requested.
    /// </summary>
    public static async Task PlayStartupAnimationAsync(CancellationToken ct = default)
    {
        if (Console.WindowWidth < 60)
            return;

        Color[] palette =
        [
            Color.NavyBlue,
            Color.Blue,
            Color.DodgerBlue1,
            Color.DeepSkyBlue1,
            Color.Aqua,
            Color.DeepSkyBlue1,
        ];

        try
        {
            await AnsiConsole.Live(BuildFiglet(palette[0]))
                .StartAsync(async ctx =>
                {
                    foreach (var color in palette)
                    {
                        ct.ThrowIfCancellationRequested();
                        ctx.UpdateTarget(BuildFiglet(color));
                        ctx.Refresh();
                        await Task.Delay(70, ct).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // user quit before animation finished — safe to ignore
        }
    }

    public IRenderable Render()
    {
        IRenderable banner = Console.WindowWidth >= 60
            ? BuildFiglet(Color.DeepSkyBlue1)
            : new Markup("[bold][deepskyblue1]SunnySunday[/][/]");

        var connectionStatus = IsConnected
            ? new Markup("[green]● Connected[/]")
            : new Markup("[red]● Disconnected[/]");

        List<IRenderable> rows =
        [
            banner,
            new Markup($"[grey]v{Markup.Escape(version)}[/]  [grey]{Markup.Escape(serverUrl)}[/]"),
            connectionStatus,
        ];

        if (!KindleEmailConfigured)
        {
            rows.Add(new Markup("[yellow]⚠ Kindle email not configured — go to Settings to set it up.[/]"));
        }

        return new Rows(rows);
    }

    private static FigletText BuildFiglet(Color color)
        => new FigletText("SunnySunday").Color(color);
}
