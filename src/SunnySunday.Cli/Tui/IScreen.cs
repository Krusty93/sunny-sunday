using Terminal.Gui.ViewBase;

namespace SunnySunday.Cli.Tui;

public interface IScreen
{
    View CreateView(Action<ScreenResult> navigate);

    View? CreateToolbarView(Action<ScreenResult> navigate) => null;

    int ToolbarHeight => 0;

    Task InitializeAsync(CancellationToken cancellationToken);

    string Title { get; }

    IReadOnlyList<(string Key, string Label)> KeyHints { get; }
}

public enum ScreenAction
{
    None,
    Push,
    Pop,
    Quit
}

public sealed record ScreenResult(ScreenAction Action, IScreen? Next = null)
{
    public static ScreenResult Stay() => new(ScreenAction.None);

    public static ScreenResult Push(IScreen next) => new(ScreenAction.Push, next);

    public static ScreenResult Pop() => new(ScreenAction.Pop);

    public static ScreenResult Quit() => new(ScreenAction.Quit);
}
