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

    private Color _bannerColor = Color.DeepSkyBlue1;
    private int _bannerRevealWidth = BannerLines.Max(line => line.Length);
    private int _sundayRevealCount = SundayText.Length;

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
            _sundayRevealCount = 0;
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
            refresh();

            for (var index = 1; index <= SundayText.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                _sundayRevealCount = index;
                refresh();
                await Task.Delay(45, ct).ConfigureAwait(false);
            }

            _bannerRevealWidth = maxWidth;
            _sundayRevealCount = SundayText.Length;
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
        var sundayMarkup = _sundayRevealCount > 0
            ? $"\n[italic #7FDBFF]{Markup.Escape(BuildCompactSundayLine())}[/]"
            : string.Empty;

        return new Markup($"[bold {colorName}]{Markup.Escape(visibleText)}[/]{sundayMarkup}");
    }

    private Rows BuildWideBanner()
    {
        var renderables = new List<IRenderable>(BannerLines.Length + 1);
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

        if (_sundayRevealCount > 0)
        {
            renderables.Add(new Markup($"[italic #7FDBFF]{Markup.Escape(BuildWideSundayLine())}[/]"));
        }
        else
        {
            renderables.Add(new Markup(string.Empty));
        }

        return new Rows(renderables);
    }

    private static string ToMarkupColor(Color color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private string BuildCompactSundayLine()
        => SundayText[..Math.Min(_sundayRevealCount, SundayText.Length)];

    private string BuildWideSundayLine()
    {
        var width = BannerLines.Max(line => line.Length);
        var buffer = Enumerable.Repeat(' ', width).ToArray();
        var positions = SpreadPositions(SundayText.Length, width);
        var visibleCount = Math.Min(_sundayRevealCount, SundayText.Length);

        for (var index = 0; index < visibleCount; index++)
        {
            buffer[positions[index]] = SundayText[index];
        }

        return new string(buffer);
    }

    private static int[] SpreadPositions(int count, int width)
    {
        if (count <= 1)
        {
            return [Math.Max(0, width / 2)];
        }

        var padding = 2;
        var usableWidth = Math.Max(count, width - (padding * 2));
        var positions = new int[count];

        for (var index = 0; index < count; index++)
        {
            var position = padding + (int)Math.Round(index * (usableWidth - 1d) / (count - 1d));
            positions[index] = Math.Min(width - 1, position);
        }

        return positions;
    }
}
