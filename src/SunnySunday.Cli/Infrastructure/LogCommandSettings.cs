using System.ComponentModel;
using Serilog.Events;
using Spectre.Console.Cli;

namespace SunnySunday.Cli.Infrastructure;

/// <summary>
/// Base settings class that adds a --logLevel option to any command.
/// Commands should inherit their Settings from this class.
/// </summary>
public class LogCommandSettings : CommandSettings
{
    [CommandOption("--logLevel")]
    [Description("Minimum log level (Verbose, Debug, Information, Warning, Error, Fatal). Default: Warning.")]
    [DefaultValue(LogEventLevel.Warning)]
    public LogEventLevel LogLevel { get; set; } = LogEventLevel.Warning;
}
