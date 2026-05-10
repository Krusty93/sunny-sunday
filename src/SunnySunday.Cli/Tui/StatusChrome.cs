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

    public IRenderable Render()
    {
        IRenderable banner = Console.WindowWidth >= 60
            ? new FigletText("SunnySunday")
            : new Markup("[bold]SunnySunday[/]");

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
}
