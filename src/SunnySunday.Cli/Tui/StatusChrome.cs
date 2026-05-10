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
    private const string BannerText = "SUNNY";
    private const string SundayText = "sunday";
    private static readonly string[] BannerLines =
    [
        "███████╗██╗   ██╗███╗   ██╗███╗   ██╗██╗   ██╗",
        "██╔════╝██║   ██║████╗  ██║████╗  ██║╚██╗ ██╔╝",
        "███████╗██║   ██║██╔██╗ ██║██╔██╗ ██║ ╚████╔╝ ",
        "╚════██║██║   ██║██║╚██╗██║██║╚██╗██║  ╚██╔╝  ",
        "███████║╚██████╔╝██║ ╚████║██║ ╚████║   ██║   ",
        "╚══════╝ ╚═════╝ ╚═╝  ╚═══╝╚═╝  ╚═══╝   ╚═╝   ",
    ];
    private static readonly Color[] BannerLineColors =
    [
        Color.Blue1,
        Color.DodgerBlue1,
        Color.DeepSkyBlue1,
        Color.Aqua,
        Color.White,
        Color.DeepSkyBlue1,
    ];
    // sunday rendered via FigletText for readability at ~1/3 SUNNY height
    private static readonly Color SundayColor = new(166, 238, 255);
    private static readonly Color SeparatorColor = new(100, 200, 240);

    private Color _bannerColor = Color.DeepSkyBlue1;
    private int _bannerRevealWidth = BannerLines.Max(line => line.Length);
    private bool _sundayVisible;

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
            Color.DeepSkyBlue1,
        };

        try
        {
            _bannerRevealWidth = 0;
            _sundayVisible = false;
            var maxWidth = BannerLines.Max(line => line.Length);

            for (var width = 1; width <= maxWidth; width += 2)
            {
                ct.ThrowIfCancellationRequested();
                _bannerRevealWidth = Math.Min(width, maxWidth);
                _bannerColor = palette[Math.Min((width - 1) / 4, palette.Length - 1)];
                refresh();
                await Task.Delay(55, ct).ConfigureAwait(false);
            }

            _bannerRevealWidth = maxWidth;
            _bannerColor = Color.DeepSkyBlue1;
            _sundayVisible = true;
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
            ? BuildWideBanner()
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
        var maxWidth = BannerLines.Max(line => line.Length);
        var visibleChars = Math.Max(1, (int)Math.Ceiling((double)_bannerRevealWidth / maxWidth * BannerText.Length));
        var visibleText = BannerText[..Math.Min(visibleChars, BannerText.Length)];
        var colorName = ToMarkupColor(_bannerColor);
        var sundayMarkup = _sundayVisible
            ? $"\n[italic #A6EEFF]{SundayText}[/]"
            : string.Empty;

        return new Markup($"[bold {colorName}]{Markup.Escape(visibleText)}[/]{sundayMarkup}");
    }

    private Rows BuildWideBanner()
    {
        var renderables = new List<IRenderable>(BannerLines.Length + 2);
        var edgeColor = ToMarkupColor(_bannerColor);

        for (var index = 0; index < BannerLines.Length; index++)
        {
            var line = BannerLines[index];
            var visibleWidth = Math.Min(_bannerRevealWidth, line.Length);
            var baseColor = ToMarkupColor(BannerLineColors[index]);

            if (visibleWidth <= 0)
            {
                renderables.Add(new Markup(string.Empty));
                continue;
            }

            var edgeStart = Math.Max(0, visibleWidth - 2);
            var stableSegment = line[..edgeStart];
            var leadingEdge = line[edgeStart..visibleWidth];

            renderables.Add(new Markup(
                $"[{baseColor}]{Markup.Escape(stableSegment)}[/][bold {edgeColor}]{Markup.Escape(leadingEdge)}[/]"));
        }

        if (_sundayVisible)
        {
            renderables.Add(new FigletText(SundayText).Color(SundayColor));
            var separatorWidth = BannerLines.Max(line => line.Length);
            renderables.Add(new Markup($"[{ToMarkupColor(SeparatorColor)}]{new string('─', separatorWidth)}[/]"));
        }

        return new Rows(renderables);
    }

    private static string ToMarkupColor(Color color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

}
