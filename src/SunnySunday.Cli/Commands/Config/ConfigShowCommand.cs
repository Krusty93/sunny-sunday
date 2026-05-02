using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Cli.Commands.Config;

/// <summary>
/// Displays all current server settings in a unified table.
/// Usage: sunny config show
/// </summary>
public sealed class ConfigShowCommand(SunnyHttpClient client, ILogger<ConfigShowCommand> logger)
    : AsyncCommand<ConfigShowCommand.Settings>
{
    public sealed class Settings : LogCommandSettings;

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        logger.LogDebug("Fetching all settings from server");

        SettingsResponse response;
        try
        {
            response = await client.GetSettingsAsync(cancellation);
        }
        catch (HttpRequestException ex)
        {
            var serverUrl = Environment.GetEnvironmentVariable("SUNNY_SERVER") ?? "unknown";
            logger.LogError(ex, "Failed to reach server at {ServerUrl}", serverUrl);
            AnsiConsole.MarkupLine($"[red]Error:[/] Cannot reach server at [yellow]{serverUrl}[/]");
            AnsiConsole.MarkupLine($"[grey]{ex.Message}[/]");
            AnsiConsole.MarkupLine("[grey]Check that the server is running and SUNNY_SERVER is correct.[/]");
            return 1;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Setting");
        table.AddColumn("Value");

        table.AddRow("Schedule", response.Schedule);
        if (response.DeliveryDay is not null)
            table.AddRow("Delivery Day", response.DeliveryDay);
        table.AddRow("Delivery Time", response.DeliveryTime);
        table.AddRow("Count", response.Count.ToString());
        table.AddRow("Kindle Email", response.KindleEmail);
        table.AddRow("Timezone", response.Timezone);

        AnsiConsole.Write(table);
        return 0;
    }
}
