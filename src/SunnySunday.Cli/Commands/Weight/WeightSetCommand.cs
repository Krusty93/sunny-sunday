using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Cli.Commands.Weight;

/// <summary>
/// Sets the recap weight for a specific highlight.
/// Usage: sunny weight set &lt;id&gt; &lt;weight&gt;   (weight 1–5)
/// </summary>
public sealed class WeightSetCommand(SunnyHttpClient client, ILogger<WeightSetCommand> logger)
    : ServerCommand<WeightSetCommand.Settings>
{
    protected override ILogger Logger => logger;

    public sealed class Settings : LogCommandSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("ID of the highlight to update.")]
        public int Id { get; set; }

        [CommandArgument(1, "<weight>")]
        [Description("New recap weight (1–5). Higher values increase selection frequency.")]
        public int Weight { get; set; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (settings.Weight < 1 || settings.Weight > 5)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Weight must be between [green]1[/] and [green]5[/] (got [yellow]{settings.Weight}[/]).");
            return 1;
        }

        logger.LogDebug("Setting weight {Weight} on highlight {Id}", settings.Weight, settings.Id);

        HttpResponseMessage response;
        try
        {
            response = await client.PutWeightAsync(settings.Id, new SetWeightRequest { Weight = settings.Weight }, cancellation);
        }
        catch (HttpRequestException ex)
        {
            return HandleServerError(ex);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Highlight with ID [yellow]{settings.Id}[/] not found.");
            return 1;
        }

        response.EnsureSuccessStatusCode();
        AnsiConsole.MarkupLine($"[green]✓[/] Weight for highlight [bold]{settings.Id}[/] set to [bold]{settings.Weight}[/].");
        return 0;
    }
}
