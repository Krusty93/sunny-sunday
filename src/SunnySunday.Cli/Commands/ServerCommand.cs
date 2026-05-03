using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Cli.Commands;

/// <summary>
/// Base class for commands that communicate with the sunny-server.
/// Provides shared error handling for HTTP failures.
/// </summary>
public abstract class ServerCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : LogCommandSettings
{
    protected abstract ILogger Logger { get; }

    protected int HandleServerError(HttpRequestException ex)
    {
        var serverUrl = Environment.GetEnvironmentVariable("SUNNY_SERVER") ?? "unknown";
        Logger.LogError(ex, "Failed to reach server at {ServerUrl}", serverUrl);
        AnsiConsole.MarkupLine($"[red]Error:[/] Cannot reach server at [yellow]{serverUrl}[/]");
        AnsiConsole.MarkupLine($"[grey]{ex.Message}[/]");
        AnsiConsole.MarkupLine("[grey]Check that the server is running and SUNNY_SERVER is correct.[/]");
        return 1;
    }
}
