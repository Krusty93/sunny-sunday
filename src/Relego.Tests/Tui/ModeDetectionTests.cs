using Relego.Cli.Tui;

namespace Relego.Tests.Tui;

public sealed class ModeDetectionTests
{
    [Fact]
    public void Detect_EmptyArgsInInteractiveTerminal_ReturnsTui()
    {
        var mode = TuiModeDetector.Detect([], isInputRedirected: false);

        Assert.Equal(StartupMode.Tui, mode);
    }

    [Fact]
    public void Detect_EmptyArgsWithRedirectedInput_ReturnsCli()
    {
        var mode = TuiModeDetector.Detect([], isInputRedirected: true);

        Assert.Equal(StartupMode.Cli, mode);
    }

    [Fact]
    public void Detect_CommandArgumentsPresent_ReturnsCli()
    {
        var mode = TuiModeDetector.Detect(["status"], isInputRedirected: false);

        Assert.Equal(StartupMode.Cli, mode);
    }
}
