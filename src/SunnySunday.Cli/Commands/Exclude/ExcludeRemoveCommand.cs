using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Cli.Commands.Exclude;

/// <summary>
/// Removes an exclusion so the entity appears in future recaps again.
/// Usage: sunny exclude remove highlight|book|author &lt;id&gt;
/// </summary>
public sealed class ExcludeRemoveCommand(SunnyHttpClient client, ILogger<ExcludeRemoveCommand> logger)
    : ServerCommand<ExcludeRemoveCommand.Settings>
{
    protected override ILogger Logger => logger;

    private static readonly string[] ValidTypes = ["highlight", "book", "author"];

    public sealed class Settings : LogCommandSettings
    {
        [CommandArgument(0, "<type>")]
        [Description("Entity type to un-exclude: highlight, book, or author.")]
        public string Type { get; set; } = string.Empty;

        [CommandArgument(1, "<id>")]
        [Description("ID of the entity to un-exclude.")]
        public int Id { get; set; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var type = settings.Type.ToLowerInvariant();

        if (!ValidTypes.Contains(type))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type [yellow]{settings.Type}[/]. Use [green]highlight[/], [green]book[/], or [green]author[/].");
            return 1;
        }

        logger.LogDebug("Removing exclusion for {Type} with ID {Id}", type, settings.Id);

        HttpResponseMessage response;
        try
        {
            response = await client.DeleteExcludeAsync(type, settings.Id, cancellation);
        }
        catch (HttpRequestException ex)
        {
            return HandleServerError(ex);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {type} with ID [yellow]{settings.Id}[/] not found.");
            return 1;
        }

        response.EnsureSuccessStatusCode();
        AnsiConsole.MarkupLine($"[green]✓[/] Removed exclusion for {type} [bold]{settings.Id}[/].");
        return 0;
    }

}
