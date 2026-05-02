using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Cli.Parsing;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Cli.Commands;

/// <summary>
/// Parses a Kindle clippings file and syncs highlights to the server.
/// </summary>
public sealed class SyncCommand(SunnyHttpClient client, ILogger<SyncCommand> logger) : AsyncCommand<SyncCommand.Settings>
{
    public sealed class Settings : LogCommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Path to My Clippings.txt. Auto-detected if omitted.")]
        public string? Path { get; set; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var filePath = settings.Path
            ?? KindleDetector.DetectClippingsPath()
            ?? PromptForPath();

        if (filePath is null)
        {
            AnsiConsole.MarkupLine("[yellow]Sync cancelled.[/]");
            return 1;
        }

        logger.LogDebug("Resolved clippings path: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: [yellow]{filePath}[/]");
            return 1;
        }

        ParseResult parseResult;
        try
        {
            parseResult = await ClippingsParser.ParseAsync(filePath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error parsing clippings file:[/] {ex.Message}");
            return 1;
        }

        if (parseResult.Books.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No highlights found in the clippings file.[/]");
            return 0;
        }

        var request = MapToSyncRequest(parseResult);
        logger.LogDebug("Sending {BookCount} books with {HighlightCount} highlights to server",
            request.Books.Count, request.Books.Sum(b => b.Highlights.Count));

        SyncResponse response;
        try
        {
            response = await client.PostSyncAsync(request, cancellation);
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

        DisplaySummary(parseResult, response);
        return 0;
    }

    private static string? PromptForPath()
    {
        AnsiConsole.MarkupLine("[yellow]Kindle not found at default paths.[/]");
        var path = AnsiConsole.Prompt(
            new TextPrompt<string>("[grey]Enter the path to My Clippings.txt, or press Enter to cancel:[/]")
                .AllowEmpty());

        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static SyncRequest MapToSyncRequest(ParseResult result)
    {
        return new SyncRequest
        {
            Books = result.Books.Select(book => new SyncBookRequest
            {
                Title = book.Title,
                Author = book.Author,
                Highlights = book.Highlights.Select(h => new SyncHighlightRequest
                {
                    Text = h.Text,
                    AddedOn = h.AddedOn
                }).ToList()
            }).ToList()
        };
    }

    private static void DisplaySummary(ParseResult parseResult, SyncResponse response)
    {
        var totalHighlights = parseResult.Books.Sum(b => b.Highlights.Count);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(
            new Rows(
                new Markup($"[green]✓[/] Parsed [bold]{totalHighlights}[/] highlights from [bold]{parseResult.Books.Count}[/] books"),
                new Markup($"[green]✓[/] [bold]{response.NewHighlights}[/] new highlights imported ([grey]{response.DuplicateHighlights} duplicates skipped[/])"),
                new Markup($"[green]✓[/] [bold]{response.NewBooks}[/] new books, [bold]{response.NewAuthors}[/] new authors")
            ))
            .Header("[green]Sync Complete[/]")
            .Border(BoxBorder.Rounded));
    }
}
