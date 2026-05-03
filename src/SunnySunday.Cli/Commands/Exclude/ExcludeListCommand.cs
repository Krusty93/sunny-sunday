using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Cli.Commands.Exclude;

/// <summary>
/// Lists all current exclusions grouped by type.
/// Usage: sunny exclude list
/// </summary>
public sealed class ExcludeListCommand(SunnyHttpClient client, ILogger<ExcludeListCommand> logger)
    : AsyncCommand<ExcludeListCommand.Settings>
{
    public sealed class Settings : LogCommandSettings;

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        logger.LogDebug("Fetching exclusions from server");

        Core.Contracts.ExclusionsResponse response;
        try
        {
            response = await client.GetExclusionsAsync(cancellation);
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

        AnsiConsole.MarkupLine("[bold]Excluded Highlights[/]");
        if (response.Highlights.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]None[/]");
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("ID");
            table.AddColumn("Text");
            table.AddColumn("Book");
            foreach (var h in response.Highlights)
                table.AddRow(h.Id.ToString(), Markup.Escape(h.Text), Markup.Escape(h.BookTitle));
            AnsiConsole.Write(table);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Excluded Books[/]");
        if (response.Books.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]None[/]");
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("ID");
            table.AddColumn("Title");
            table.AddColumn("Author");
            table.AddColumn("Highlights");
            foreach (var b in response.Books)
                table.AddRow(b.Id.ToString(), Markup.Escape(b.Title), Markup.Escape(b.AuthorName), b.HighlightCount.ToString());
            AnsiConsole.Write(table);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Excluded Authors[/]");
        if (response.Authors.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]None[/]");
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("ID");
            table.AddColumn("Name");
            table.AddColumn("Books");
            foreach (var a in response.Authors)
                table.AddRow(a.Id.ToString(), Markup.Escape(a.Name), a.BookCount.ToString());
            AnsiConsole.Write(table);
        }

        return 0;
    }
}
