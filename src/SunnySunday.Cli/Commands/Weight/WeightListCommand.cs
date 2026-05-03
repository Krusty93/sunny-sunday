using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Cli.Commands.Weight;

/// <summary>
/// Lists all highlights that have a non-default recap weight.
/// Usage: sunny weight list
/// </summary>
public sealed class WeightListCommand(SunnyHttpClient client, ILogger<WeightListCommand> logger)
    : ServerCommand<WeightListCommand.Settings>
{
    protected override ILogger Logger => logger;

    public sealed class Settings : LogCommandSettings;

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        logger.LogDebug("Fetching weighted highlights from server");

        List<Core.Contracts.WeightedHighlightDto> weights;
        try
        {
            weights = await client.GetWeightsAsync(cancellation);
        }
        catch (HttpRequestException ex)
        {
            return HandleServerError(ex);
        }

        if (weights.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No custom weights set.[/]");
            return 0;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn("Text");
        table.AddColumn("Book");
        table.AddColumn("Weight");

        foreach (var h in weights)
            table.AddRow(h.Id.ToString(), Markup.Escape(h.Text), Markup.Escape(h.BookTitle), h.Weight.ToString());

        AnsiConsole.Write(table);
        return 0;
    }
}
