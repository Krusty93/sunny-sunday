using Serilog.Core;
using Spectre.Console.Cli;

namespace SunnySunday.Cli.Infrastructure;

/// <summary>
/// Spectre.Console.Cli interceptor that adjusts Serilog's LoggingLevelSwitch
/// based on the --logLevel option parsed from command settings.
/// </summary>
public sealed class LogInterceptor(Func<LoggingLevelSwitch> getLevelSwitch) : ICommandInterceptor
{
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        if (settings is LogCommandSettings logSettings)
        {
            getLevelSwitch().MinimumLevel = logSettings.LogLevel;
        }
    }
}
