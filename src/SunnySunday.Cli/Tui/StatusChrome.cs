using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Cli.Tui;

public sealed class StatusChrome(string serverUrl, string version)
{
    private static readonly string[] LogoLines =
    [
        "███████╗██╗   ██╗███╗   ██╗███╗   ██╗██╗   ██╗",
        "██╔════╝██║   ██║████╗  ██║████╗  ██║╚██╗ ██╔╝",
        "███████╗██║   ██║██╔██╗ ██║██╔██╗ ██║ ╚████╔╝ ",
        "╚════██║██║   ██║██║╚██╗██║██║╚██╗██║  ╚██╔╝  ",
        "███████║╚██████╔╝██║ ╚████║██║ ╚████║   ██║   ",
        "╚══════╝ ╚═════╝ ╚═╝  ╚═══╝╚═╝  ╚═══╝   ╚═╝   ",
    ];

    private static readonly string SundayLine = "                              s u n d a y";
    private static readonly int LogoTextWidth = LogoLines.Max(l => l.Length);

    public static readonly Color Background = new(30, 30, 30);

    public const int LogoHeight = 8; // 6 logo + sunday + separator

    private Label? _connectionLabel;
    private Label? _warningLabel;

    public bool IsConnected { get; private set; }
    public bool KindleEmailConfigured { get; private set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Views are owned by the parent container hierarchy")]
    public View CreateView()
    {
        var bg = Background;

        var container = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = LogoHeight
        };
        container.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(Color.White, bg)));

        for (var i = 0; i < LogoLines.Length; i++)
        {
            var logoLabel = new Label
            {
                Text = LogoLines[i],
                X = 1,
                Y = i
            };
            logoLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(0, 191, 255), bg)));
            container.Add(logoLabel);
        }

        var sundayLabel = new Label
        {
            Text = SundayLine,
            X = 1,
            Y = LogoLines.Length
        };
        sundayLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(166, 238, 255), bg)));
        container.Add(sundayLabel);

        var separatorLabel = new Label
        {
            Text = new string('─', 300),
            X = 0,
            Y = LogoLines.Length + 1,
            Width = Dim.Fill()
        };
        separatorLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(60, 60, 60), bg)));
        container.Add(separatorLabel);

        var infoFrame = new FrameView
        {
            Title = "Status",
            X = LogoTextWidth + 3,
            Y = 1,
            Width = Dim.Fill(1),
            Height = 5,
            BorderStyle = LineStyle.Rounded
        };
        infoFrame.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(80, 85, 90), bg)));

        var versionLabel = new Label
        {
            Text = $"SunnySunday v{version}",
            X = 1,
            Y = 0
        };
        versionLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(128, 128, 128), bg)));

        _connectionLabel = new Label
        {
            Text = $"⟳ Checking...  {serverUrl}",
            X = 1,
            Y = 1,
            Width = Dim.Fill()
        };

        _warningLabel = new Label
        {
            Text = string.Empty,
            X = 1,
            Y = 2,
            Width = Dim.Fill()
        };

        infoFrame.Add(versionLabel, _connectionLabel, _warningLabel);
        container.Add(infoFrame);

        return container;
    }

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

    public void UpdateLabels()
    {
        var bg = Background;

        if (_connectionLabel is not null)
        {
            _connectionLabel.Text = IsConnected
                ? $"● Connected  {serverUrl}"
                : $"● Disconnected  {serverUrl}";

            _connectionLabel.SetScheme(IsConnected
                ? new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(0, 200, 0), bg))
                : new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(255, 0, 0), bg)));
        }

        if (_warningLabel is not null)
        {
            if (KindleEmailConfigured)
            {
                _warningLabel.Text = string.Empty;
            }
            else
            {
                _warningLabel.Text = "⚠ Kindle email not configured";
                _warningLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(255, 255, 0), bg)));
            }
        }
    }
}
