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
    private static readonly string[] SundayBannerLines =
    [
        "   ▄▄▄   █ █   █▄█   ▄▄█   ▄▄█   █ █",
        " █▄▄    ▀▄▀   █ █   █▄█   █ █    █  ",
    ];
    private static readonly Color[] SundayBannerColors =
    [
        new(185, 242, 255),
        new(127, 219, 255),
    ];

    private Color _bannerColor = Color.DeepSkyBlue1;
    private int _bannerRevealWidth = BannerLines.Max(line => line.Length);
    private int _sundayRevealWidth = SundayBannerLines.Max(line => line.Length);

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
            _sundayRevealWidth = 0;
            var maxWidth = BannerLines.Max(line => line.Length);
            var sundayMaxWidth = SundayBannerLines.Max(line => line.Length);

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
            refresh();

            for (var index = 1; index <= SundayText.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                _sundayRevealWidth = Math.Min(index * 6, sundayMaxWidth);
                refresh();
                await Task.Delay(45, ct).ConfigureAwait(false);
            }

            _bannerRevealWidth = maxWidth;
            _sundayRevealWidth = sundayMaxWidth;
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
        var sundayMarkup = _sundayRevealWidth > 0
            ? $"\n[italic #7FDBFF]{Markup.Escape(BuildCompactSundayLine())}[/]"
            : string.Empty;

        return new Markup($"[bold {colorName}]{Markup.Escape(visibleText)}[/]{sundayMarkup}");
    }

    private Rows BuildWideBanner()
    {
        var renderables = new List<IRenderable>(BannerLines.Length + SundayBannerLines.Length);
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

        if (_sundayRevealWidth > 0)
        {
            foreach (var line in BuildWideSundayLines())
            {
                renderables.Add(line);
            }
        }
        else
        {
            renderables.Add(new Markup(string.Empty));
            renderables.Add(new Markup(string.Empty));
        }

        return new Rows(renderables);
    }

    private static string ToMarkupColor(Color color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private string BuildCompactSundayLine()
        => SundayText[..Math.Min(Math.Max(1, (int)Math.Ceiling((double)_sundayRevealWidth / SundayBannerLines.Max(line => line.Length) * SundayText.Length)), SundayText.Length)];

    private IEnumerable<Markup> BuildWideSundayLines()
    {
        var width = BannerLines.Max(line => line.Length);
        var edgeColor = "#DFFBFF";

        for (var index = 0; index < SundayBannerLines.Length; index++)
        {
            var centeredLine = SundayBannerLines[index].PadLeft((width + SundayBannerLines[index].Length) / 2).PadRight(width);
            var visibleWidth = Math.Min(_sundayRevealWidth, centeredLine.Length);
            var baseColor = ToMarkupColor(SundayBannerColors[index]);

            if (visibleWidth <= 0)
            {
                yield return new Markup(string.Empty);
                continue;
            }

            var edgeStart = Math.Max(0, visibleWidth - 3);
            var stableSegment = centeredLine[..edgeStart];
            var leadingEdge = centeredLine[edgeStart..visibleWidth];

            yield return new Markup(
                $"[italic {baseColor}]{Markup.Escape(stableSegment)}[/][bold italic {edgeColor}]{Markup.Escape(leadingEdge)}[/]");
        }
    }
}
