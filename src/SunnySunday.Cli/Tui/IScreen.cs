using Spectre.Console.Rendering;

namespace SunnySunday.Cli.Tui;

public interface IScreen
{
    IRenderable Render();

    Task<ScreenResult> HandleKeyAsync(ConsoleKeyInfo key, CancellationToken cancellationToken);

    Task InitializeAsync(CancellationToken cancellationToken);

    string KeyHints { get; }
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
