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
    private const string BannerText = "SunnySunday";
    private Color _bannerColor = Color.DeepSkyBlue1;
    private int _bannerCharacterCount = BannerText.Length;

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
    /// Plays the startup animation inside the active TUI live session.
    /// The banner is written one character at a time with a high-contrast blue-to-cyan ramp.
    /// </summary>
    public async Task PlayStartupAnimationAsync(Action refresh, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(refresh);

        var palette = new[]
        {
            Color.Blue1,
            Color.DodgerBlue1,
            Color.DeepSkyBlue1,
            Color.Aqua,
            Color.White,
            Color.DeepSkyBlue1,
        };

        try
        {
            _bannerCharacterCount = 0;

            for (var index = 1; index <= BannerText.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                _bannerCharacterCount = index;
                _bannerColor = palette[Math.Min(index - 1, palette.Length - 1)];
                refresh();
                await Task.Delay(120, ct).ConfigureAwait(false);
            }

            foreach (var color in palette.Skip(2))
            {
                ct.ThrowIfCancellationRequested();
                _bannerColor = color;
                refresh();
                await Task.Delay(110, ct).ConfigureAwait(false);
            }

            _bannerCharacterCount = BannerText.Length;
            _bannerColor = Color.DeepSkyBlue1;
            refresh();
        }
        catch (OperationCanceledException)
        {
            // user quit before animation finished — safe to ignore
        }
    }

    public IRenderable Render()
    {
        IRenderable banner = Console.WindowWidth >= 60
            ? BuildFiglet(_bannerCharacterCount, _bannerColor)
            : BuildCompactBanner();

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

    private Markup BuildCompactBanner()
    {
        var visibleText = BannerText[..Math.Max(_bannerCharacterCount, 1)];
        var colorName = ToMarkupColor(_bannerColor);
        return new Markup($"[bold {colorName}]{Markup.Escape(visibleText)}[/]");
    }

    private static FigletText BuildFiglet(int characterCount, Color color)
    {
        var visibleText = BannerText[..Math.Max(characterCount, 1)];
        return new FigletText(visibleText).Color(color);
    }

    private static string ToMarkupColor(Color color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
