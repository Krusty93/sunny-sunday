using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace SunnySunday.Cli.Tui;

public sealed class TuiApp(SunnySunday.Cli.Infrastructure.SunnyHttpClient client, string serverUrl, string version)
{
    private const int SplashTopPadding = 1;

    private static readonly string[] SplashLines =
    [
        "███████╗██╗   ██╗███╗   ██╗███╗   ██╗██╗   ██╗",
        "██╔════╝██║   ██║████╗  ██║████╗  ██║╚██╗ ██╔╝",
        "███████╗██║   ██║██╔██╗ ██║██╔██╗ ██║ ╚████╔╝ ",
        "╚════██║██║   ██║██║╚██╗██║██║╚██╗██║  ╚██╔╝  ",
        "███████║╚██████╔╝██║ ╚████║██║ ╚████║   ██║   ",
        "╚══════╝ ╚═════╝ ╚═╝  ╚═══╝╚═╝  ╚═══╝   ╚═╝   ",
    ];

    private static readonly string SundayText = "s u n d a y";

    private static readonly Color[] LineColors =
    [
        new(30, 80, 220),
        new(0, 120, 255),
        new(0, 160, 255),
        new(0, 191, 255),
        new(100, 220, 255),
        new(0, 160, 255),
    ];

    private static readonly Color FooterKeyColor = new(110, 200, 255);
    private static readonly Color FooterLabelColor = new(190, 190, 190);

    private readonly SunnySunday.Cli.Infrastructure.SunnyHttpClient _client = client;
    private readonly StatusChrome _chrome = new(serverUrl, version);
    private readonly Stack<IScreen> _screens = new();
    private IApplication? _app;
    private FrameView? _contentFrame;
    private View? _toolbarView;
    private View? _statusBar;
    private Window? _window;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_screens.Count == 0)
        {
            var initialScreen = new BookListScreen(_client);
            await initialScreen.InitializeAsync(cancellationToken).ConfigureAwait(false);
            _screens.Push(initialScreen);
        }

        _app = Application.Create();
        _app.Init();

        try
        {
            await RunSplashAsync(cancellationToken).ConfigureAwait(false);

            _window = new Window
            {
                Title = "SunnySunday",
                BorderStyle = LineStyle.None
            };
            _window.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(Color.White, StatusChrome.Background)));

            using var headerView = _chrome.CreateView();
            _window.Add(headerView);

            _contentFrame = new FrameView
            {
                X = 0,
                Y = StatusChrome.LogoHeight,
                Width = Dim.Fill(),
                Height = Dim.Fill(1),
                Title = _screens.Peek().Title,
                CanFocus = true
            };
            _contentFrame.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(60, 100, 140), StatusChrome.Background)));
            _window.Add(_contentFrame);

            _statusBar = new View
            {
                X = 0,
                Y = Pos.AnchorEnd(1),
                Width = Dim.Fill(),
                Height = 1,
                CanFocus = false
            };
            _statusBar.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(180, 180, 180), StatusChrome.Background)));
            _window.Add(_statusBar);

            ShowCurrentScreen();

            _ = Task.Run(async () =>
            {
                await _chrome.RefreshAsync(_client, cancellationToken);
                _app.Invoke(() => _chrome.UpdateLabels());
            });

            _app.Run(_window);
        }
        finally
        {
            _window?.Dispose();
            (_app as IDisposable)?.Dispose();
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Views are owned by the splash window")]
    private async Task RunSplashAsync(CancellationToken ct)
    {
        if (_app is null)
        {
            return;
        }

        using var splashWindow = new Window
        {
            BorderStyle = LineStyle.None
        };
        splashWindow.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(Color.White, StatusChrome.Background)));

        var maxWidth = SplashLines.Max(line => line.Length);
        var splashContentWidth = Math.Max(maxWidth, SundayText.Length);

        var splashContent = new View
        {
            X = 0,
            Y = SplashTopPadding,
            Width = splashContentWidth,
            Height = SplashLines.Length + 3,
            CanFocus = false
        };
        splashWindow.Add(splashContent);

        void CenterSplashContent()
        {
            var viewportWidth = splashWindow.Viewport.Width;
            if (viewportWidth <= 0)
            {
                return;
            }

            splashContent.X = Math.Max(0, (viewportWidth - splashContentWidth) / 2);
        }

        splashWindow.ViewportChanged += (_, _) => CenterSplashContent();

        var labels = new Label[SplashLines.Length];
        for (var i = 0; i < SplashLines.Length; i++)
        {
            labels[i] = new Label
            {
                Text = string.Empty,
                X = 0,
                Y = i,
                Width = splashContentWidth,
                TextAlignment = Alignment.Center
            };
            labels[i].SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(LineColors[i], StatusChrome.Background)));
            splashContent.Add(labels[i]);
        }

        var sundayLabel = new Label
        {
            Text = string.Empty,
            X = 0,
            Y = SplashLines.Length,
            Width = splashContentWidth,
            TextAlignment = Alignment.Center
        };
        sundayLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(166, 238, 255), StatusChrome.Background)));
        splashContent.Add(sundayLabel);

        var versionLabel = new Label
        {
            Text = string.Empty,
            X = 0,
            Y = SplashLines.Length + 2,
            Width = splashContentWidth,
            TextAlignment = Alignment.Center
        };
        versionLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(128, 128, 128), StatusChrome.Background)));
        splashContent.Add(versionLabel);

        var token = _app.Begin(splashWindow);

        try
        {
            CenterSplashContent();
            _app.LayoutAndDraw(true);

            for (var width = 2; width <= maxWidth; width += 2)
            {
                ct.ThrowIfCancellationRequested();
                var w = Math.Min(width, maxWidth);

                for (var i = 0; i < SplashLines.Length; i++)
                {
                    var line = SplashLines[i];
                    labels[i].Text = line[..Math.Min(w, line.Length)];
                }

                _app.LayoutAndDraw(true);
                await Task.Delay(40, ct).ConfigureAwait(false);
            }

            for (var i = 0; i < SplashLines.Length; i++)
            {
                labels[i].Text = SplashLines[i];
            }

            _app.LayoutAndDraw(true);

            for (var charIndex = 1; charIndex <= SundayText.Length; charIndex++)
            {
                ct.ThrowIfCancellationRequested();
                sundayLabel.Text = SundayText[..charIndex];
                _app.LayoutAndDraw(true);
                await Task.Delay(25, ct).ConfigureAwait(false);
            }

            versionLabel.Text = $"v{version}";
            _app.LayoutAndDraw(true);
            await Task.Delay(500, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // user quit before animation finished — safe to ignore
        }
        finally
        {
            if (token is not null)
            {
                _app.End(token);
            }
        }
    }

    private void Navigate(ScreenResult result)
    {
        switch (result.Action)
        {
            case ScreenAction.None:
                return;
            case ScreenAction.Push when result.Next is not null:
                _ = Task.Run(async () =>
                {
                    await result.Next.InitializeAsync(CancellationToken.None);
                    _app?.Invoke(() =>
                    {
                        _screens.Push(result.Next);
                        ShowCurrentScreen();
                    });
                });
                break;
            case ScreenAction.Pop:
                if (_screens.Count > 1)
                {
                    _screens.Pop();
                    ShowCurrentScreen();
                }

                break;
            case ScreenAction.Quit:
                _app?.RequestStop();

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result.Action), result.Action, null);
        }
    }

    private void ShowCurrentScreen()
    {
        if (_screens.Count == 0 || _contentFrame is null || _window is null)
        {
            return;
        }

        var screen = _screens.Peek();

        // Remove previous toolbar if any
        if (_toolbarView is not null)
        {
            _window.Remove(_toolbarView);
            _toolbarView = null;
        }

        // Add toolbar if the screen provides one
        var toolbarHeight = screen.ToolbarHeight;
        _toolbarView = screen.CreateToolbarView(Navigate);

        if (_toolbarView is not null && toolbarHeight > 0)
        {
            _toolbarView.X = 0;
            _toolbarView.Y = StatusChrome.LogoHeight;
            _toolbarView.Width = Dim.Fill();
            _toolbarView.Height = toolbarHeight;
            _window.Add(_toolbarView);
        }
        else
        {
            toolbarHeight = 0;
        }

        // Adjust content frame position
        _contentFrame.Y = StatusChrome.LogoHeight + toolbarHeight;

        _contentFrame.RemoveAll();
        _contentFrame.Title = screen.Title;

        var view = screen.CreateView(Navigate);
        _contentFrame.Add(view);
        view.SetFocus();

        UpdateStatusBar(screen.KeyHints);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Labels are owned by the footer view")]
    private void UpdateStatusBar(IReadOnlyList<(string Key, string Label)> hints)
    {
        if (_statusBar is null)
        {
            return;
        }

        _statusBar.RemoveAll();

        var cursorX = 1;

        foreach (var (key, label) in hints)
        {
            if (string.Equals(key, "Ctrl+C", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var keyText = key.Contains('↑') || key.Contains('↓')
                ? key
                : $"<{key}>";

            var keyLabel = new Label
            {
                X = cursorX,
                Y = 0,
                Width = keyText.Length,
                Height = 1,
                Text = keyText,
                CanFocus = false
            };
            keyLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(FooterKeyColor, StatusChrome.Background)));
            _statusBar.Add(keyLabel);
            cursorX += keyText.Length;

            if (!string.IsNullOrWhiteSpace(label) && !(key.Contains('↑') || key.Contains('↓')))
            {
                var labelText = $" {label}";
                var descriptionLabel = new Label
                {
                    X = cursorX,
                    Y = 0,
                    Width = labelText.Length,
                    Height = 1,
                    Text = labelText,
                    CanFocus = false
                };
                descriptionLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(FooterLabelColor, StatusChrome.Background)));
                _statusBar.Add(descriptionLabel);
                cursorX += labelText.Length;
            }

            cursorX += 2;
        }
    }
}
