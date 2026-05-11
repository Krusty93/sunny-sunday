using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace SunnySunday.Cli.Tui;

public sealed class TuiApp(SunnySunday.Cli.Infrastructure.SunnyHttpClient client, string serverUrl, string version)
{
    private static readonly string[] SplashLines =
    [
        "███████╗██╗   ██╗███╗   ██╗███╗   ██╗██╗   ██╗",
        "██╔════╝██║   ██║████╗  ██║████╗  ██║╚██╗ ██╔╝",
        "███████╗██║   ██║██╔██╗ ██║██╔██╗ ██║ ╚████╔╝ ",
        "╚════██║██║   ██║██║╚██╗██║██║╚██╗██║  ╚██╔╝  ",
        "███████║╚██████╔╝██║ ╚████║██║ ╚████║   ██║   ",
        "╚══════╝ ╚═════╝ ╚═╝  ╚═══╝╚═╝  ╚═══╝   ╚═╝   ",
    ];

    private static readonly string SundayText = "                              s u n d a y";

    private static readonly Color[] LineColors =
    [
        new(30, 80, 220),
        new(0, 120, 255),
        new(0, 160, 255),
        new(0, 191, 255),
        new(100, 220, 255),
        new(0, 160, 255),
    ];

    private readonly SunnySunday.Cli.Infrastructure.SunnyHttpClient _client = client;
    private readonly StatusChrome _chrome = new(serverUrl, version);
    private readonly Stack<IScreen> _screens = new();
    private IApplication? _app;
    private FrameView? _contentFrame;
    private StatusBar? _statusBar;
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

            _statusBar = new StatusBar();
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

        var labels = new Label[SplashLines.Length];
        for (var i = 0; i < SplashLines.Length; i++)
        {
            labels[i] = new Label
            {
                Text = string.Empty,
                X = Pos.Center(),
                Y = Pos.Center() - SplashLines.Length / 2 + i - 1
            };
            labels[i].SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(LineColors[i], StatusChrome.Background)));
            splashWindow.Add(labels[i]);
        }

        var sundayLabel = new Label
        {
            Text = string.Empty,
            X = Pos.Center(),
            Y = Pos.Center() + SplashLines.Length / 2
        };
        sundayLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(166, 238, 255), StatusChrome.Background)));
        splashWindow.Add(sundayLabel);

        var versionLabel = new Label
        {
            Text = string.Empty,
            X = Pos.Center(),
            Y = Pos.Center() + SplashLines.Length / 2 + 2
        };
        versionLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(128, 128, 128), StatusChrome.Background)));
        splashWindow.Add(versionLabel);

        var token = _app.Begin(splashWindow);

        try
        {
            var maxWidth = SplashLines.Max(line => line.Length);

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
            await Task.Delay(800, ct).ConfigureAwait(false);
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
        if (_screens.Count == 0 || _contentFrame is null)
        {
            return;
        }

        var screen = _screens.Peek();
        _contentFrame.RemoveAll();
        _contentFrame.Title = screen.Title;

        var view = screen.CreateView(Navigate);
        _contentFrame.Add(view);
        view.SetFocus();

        UpdateStatusBar(screen.KeyHints);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Shortcuts are owned by the StatusBar")]
    private void UpdateStatusBar(IReadOnlyList<(string Key, string Label)> hints)
    {
        if (_statusBar is null)
        {
            return;
        }

        _statusBar.RemoveAll();

        foreach (var (key, label) in hints)
        {
            _statusBar.Add(new Shortcut
            {
                Text = label,
                HelpText = $"<{key}>"
            });
        }
    }
}
