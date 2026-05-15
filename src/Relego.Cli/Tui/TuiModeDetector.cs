namespace Relego.Cli.Tui;

public enum StartupMode
{
    Cli,
    Tui
}

public static class TuiModeDetector
{
    public static StartupMode Detect(IReadOnlyList<string> args, bool isInputRedirected)
        => args.Count == 0 && !isInputRedirected ? StartupMode.Tui : StartupMode.Cli;
}
